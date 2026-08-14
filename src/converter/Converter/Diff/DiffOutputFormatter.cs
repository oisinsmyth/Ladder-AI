using System.Text;
using System.Text.Json;

namespace Converter.Diff;

// Mirrors ReviewOutputFormatter/PreflightOutputFormatter/TagStatusOutputFormatter's
// "one record, two renderers" pattern.
public static class DiffOutputFormatter
{
    public static string FormatText(DiffReport report)
    {
        var sb = new StringBuilder();

        if (report.BlockNameMismatch)
        {
            sb.Append("WARNING: block name changed: '").Append(report.OtherBlockName)
              .Append("' -> '").Append(report.BlockName).Append("' (diffing by network number regardless)\n");
        }

        sb.Append("SUMMARY: ").Append(report.Networks.Count).Append(" network(s): ")
          .Append(report.ChangedCount).Append(" changed, ")
          .Append(report.AddedCount).Append(" added, ")
          .Append(report.RemovedCount).Append(" removed, ")
          .Append(report.IdenticalCount).Append(" identical\n");

        if (report.Header.Changed)
        {
            sb.Append("HEADER changed:");
            if (report.Header.TitleChanged)
            {
                sb.Append(" title(").Append(report.Header.TitleBefore ?? "<none>").Append(" -> ")
                  .Append(report.Header.TitleAfter ?? "<none>").Append(')');
            }

            if (report.Header.CommentChanged)
            {
                sb.Append(" comment");
            }

            if (report.Header.InterfaceChanged)
            {
                sb.Append(" interface");
            }

            sb.Append('\n');
        }

        foreach (var network in report.Networks.Where(n => n.Kind != NetworkChangeKind.Identical))
        {
            sb.Append('\n').Append(KindLabel(network.Kind)).Append(" network ").Append(network.Number);
            if (!string.IsNullOrEmpty(network.Title))
            {
                sb.Append(" \"").Append(network.Title).Append('"');
            }

            sb.Append('\n');

            if (network.BeforeText is not null)
            {
                AppendIndented(sb, "- ", network.BeforeText);
            }

            if (network.AfterText is not null)
            {
                AppendIndented(sb, "+ ", network.AfterText);
            }
        }

        if (report.AllowedNetworks.Count > 0)
        {
            if (report.HasInvarianceViolation)
            {
                sb.Append("\nINVARIANCE VIOLATION: change(s) outside --only {")
                  .Append(string.Join(", ", report.AllowedNetworks.OrderBy(n => n)))
                  .Append('}');

                if (report.InvarianceViolations.Count > 0)
                {
                    sb.Append(": network(s) ")
                      .Append(string.Join(", ", report.InvarianceViolations.Select(v => v.Number)));
                }

                // Named separately from the network list because the ACTION differs: a stray network
                // is an edit to undo, an unclaimed header change is usually an edit to DECLARE.
                if (report.HasUnclaimedHeaderChange)
                {
                    sb.Append(report.InvarianceViolations.Count > 0 ? "; " : ": ")
                      .Append(HeaderViolationReason(report))
                      .Append(" and --only names networks only. Pass --allow-header if that was the point of the change.");
                }

                sb.Append('\n');
            }
            else
            {
                // States what was EXAMINED, not merely that it passed. The old wording claimed "all
                // changes confined to --only {N}" while an unexamined header change sat printed two
                // lines above it — a report disagreeing with itself, with the exit code following the
                // wrong half.
                sb.Append("\nINVARIANCE OK: all changes confined to --only {")
                  .Append(string.Join(", ", report.AllowedNetworks.OrderBy(n => n)))
                  .Append(HeaderClause(report))
                  .Append('\n');
            }

            // Reported whether the run passed or failed, on its own line, naming what changed — a
            // comment repair is exactly how a stale comment gets fixed, and this project has been
            // misled by stale comments twice in two days. Non-gating is not invisible.
            if (report.HasNonGatingCommentChange)
            {
                sb.Append("HEADER COMMENT CHANGED (does not gate): the block comment differs and nothing else\n")
                  .Append("       in the header does. A comment cannot alter what the PLC does, so it cannot be a\n")
                  .Append("       change outside --only — but READ IT: this is where a stale comment gets repaired,\n")
                  .Append("       and it is equally where a correct one gets silently discarded.\n");
            }

            // 🔴 THE DENOMINATOR OF THE INVARIANCE CLAIM. "All changes confined to --only {1}" over a
            // block with ONE network proves nothing at all: the remainder is empty. Measured on
            // FB_Comms_ModbusServer, which has exactly one network, where `--only 1` reported OK with
            // `identical: 0`. Same shape as drift-check's COMPARED: <n> — printed always, so a green
            // that examined nothing looks different from a green that examined ninety.
            sb.Append("UNCHANGED REMAINDER: ").Append(report.IdenticalCount)
              .Append(" network(s) proven identical outside --only");
            if (report.IdenticalCount == 0)
            {
                sb.Append("  — NOTHING WAS PROVEN. Every network in this block is inside the --only set, so the\n")
                  .Append("       invariance claim has an empty remainder and carries no information.");
            }

            sb.Append('\n');
        }

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(DiffReport report)
    {
        var payload = new
        {
            blockName = report.BlockName,
            blockNameMismatch = report.BlockNameMismatch,
            otherBlockName = report.OtherBlockName,
            hasAnyChange = report.HasAnyChange,
            header = new
            {
                changed = report.Header.Changed,
                titleChanged = report.Header.TitleChanged,
                titleBefore = report.Header.TitleBefore,
                titleAfter = report.Header.TitleAfter,
                commentChanged = report.Header.CommentChanged,
                commentBefore = report.Header.CommentBefore,
                commentAfter = report.Header.CommentAfter,
                interfaceChanged = report.Header.InterfaceChanged,
            },
            summary = new
            {
                total = report.Networks.Count,
                changed = report.ChangedCount,
                added = report.AddedCount,
                removed = report.RemovedCount,
                identical = report.IdenticalCount,
            },
            networks = report.Networks.Select(n => new
            {
                number = n.Number,
                kind = n.Kind.ToString(),
                title = n.Title,
                beforeText = n.BeforeText,
                afterText = n.AfterText,
            }),
            allowedNetworks = report.AllowedNetworks,
            invarianceViolations = report.InvarianceViolations.Select(n => n.Number),
            // Both halves of the verdict, separately, so a machine reader can tell a stray NETWORK
            // from an unclaimed HEADER change — different edits, different fixes.
            headerChangeAllowed = report.HeaderChangeAllowed,
            hasUnclaimedHeaderChange = report.HasUnclaimedHeaderChange,
            // The comment-only carve-out, and the denominator of the invariance claim. Emitted so a
            // machine reader can distinguish "nothing changed outside --only" from "there was nothing
            // outside --only to change" — the header block above already carried commentChanged and
            // interfaceChanged separately, and the old verdict simply threw that distinction away.
            hasNonGatingCommentChange = report.HasNonGatingCommentChange,
            unchangedRemainder = report.IdenticalCount,
            hasInvarianceViolation = report.HasInvarianceViolation,
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    // Names the SPECIFIC thing that gated, never the generic "header". A reader who is told "the
    // header changed" checks the whole header; one who is told "an interface member changed" checks
    // the one thing that can break a caller.
    private static string HeaderViolationReason(DiffReport report)
    {
        if (report.BlockNameMismatch)
        {
            return "the block itself was renamed";
        }

        var parts = new List<string>();
        if (report.Header.InterfaceChanged)
        {
            parts.Add("an INTERFACE member changed");
        }

        if (report.Header.TitleChanged)
        {
            parts.Add("the block TITLE changed");
        }

        var reason = string.Join(" and ", parts);

        // A comment change reaches here only ALONGSIDE a behaviour-bearing one; on its own it does not
        // gate at all. Said anyway, and said as an aside, so a reader cannot come away thinking the
        // comment repair is what was refused — that misreading is what would send them to delete it.
        return report.Header.CommentChanged
            ? reason + " (the block comment also changed; on its own that would not gate)"
            : reason;
    }

    // What the OK verdict examined in the header, spelled out rather than left to be assumed.
    private static string HeaderClause(DiffReport report)
    {
        if (report.HeaderChangeAllowed && (report.Header.Changed || report.BlockNameMismatch))
        {
            return "} plus the header change, declared via --allow-header";
        }

        if (report.Header.CommentOnlyChange)
        {
            return "}, header behaviour-bearing fields unchanged (block comment differs — see below)";
        }

        return "}, header unchanged";
    }

    private static string KindLabel(NetworkChangeKind kind) => kind switch
    {
        NetworkChangeKind.Changed => "CHANGED",
        NetworkChangeKind.Added => "ADDED",
        NetworkChangeKind.Removed => "REMOVED",
        NetworkChangeKind.Identical => "IDENTICAL",
        _ => kind.ToString().ToUpperInvariant(),
    };

    private static void AppendIndented(StringBuilder sb, string marker, string text)
    {
        foreach (var line in text.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            sb.Append("  ").Append(marker).Append(line).Append('\n');
        }
    }
}
