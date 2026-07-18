using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Review;

// One method per rule, following this codebase's own established "hand-rolled per-kind" style
// (Ir/TagReferences.cs's own doc comment; confirmed consistent across IrParser/IrSerializer/
// GraphReducer, and an explicit 2026-07-14 audit already deferred generalizing this shape) —
// no shared rule-check abstraction, each function is independent and reads its own rule text
// directly from docs/06-lad-conventions.md.
public static class Rules
{
    // C-003 (warn) — Prefixes: FB_/FC_/DB_/UDT_; instance DBs named iDB_<FBName>_<Instance>.
    public static IEnumerable<Finding> CheckC003BlockPrefix(IrBlock block)
    {
        var requiredPrefix = block.Kind switch
        {
            "FB" => "FB_",
            "FC" => "FC_",
            _ => null, // OB and anything else: C-003 names no required prefix for these.
        };

        if (requiredPrefix is not null && !block.Name.StartsWith(requiredPrefix, StringComparison.Ordinal))
        {
            yield return new Finding(
                "C-003",
                FindingSeverity.Warn,
                block.Name,
                null,
                $"Block name '{block.Name}' does not start with the required '{requiredPrefix}' prefix for a {block.Kind}.",
                $"Rename to '{requiredPrefix}{block.Name}' (coordinate with any callers/references first).");
        }
    }

    public static IEnumerable<Finding> CheckC003DbPrefix(DbSource db)
    {
        if (db.InstanceOfName is not null)
        {
            if (!db.Name.StartsWith("iDB_", StringComparison.Ordinal))
            {
                yield return new Finding(
                    "C-003",
                    FindingSeverity.Warn,
                    db.Name,
                    null,
                    $"Instance DB name '{db.Name}' (of '{db.InstanceOfName}') does not follow the required 'iDB_<FBName>_<Instance>' pattern.",
                    $"Rename to a form like 'iDB_{db.InstanceOfName}_<instance>' (coordinate with any callers first).");
            }
        }
        else if (!db.Name.StartsWith("DB_", StringComparison.Ordinal))
        {
            yield return new Finding(
                "C-003",
                FindingSeverity.Warn,
                db.Name,
                null,
                $"DB name '{db.Name}' does not start with the required 'DB_' prefix.",
                $"Rename to 'DB_{db.Name}' (coordinate with any callers/references first).");
        }
    }

    // C-005 (error) — names use letters, digits, and underscore only, starting with a letter.
    // Runs per dot-separated path component (array-index suffixes stripped, %Xn/%Bn/%Wn
    // slice-address components skipped entirely — that's addressing syntax, not a user-chosen
    // name) — never against a whole dotted path or an Expr.Literal value (TagReferences.AllTagPaths
    // already excludes literals).
    public static IEnumerable<Finding> CheckC005Charset(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var path in TagReferences.AllTagPaths(network))
            {
                foreach (var finding in CheckPathCharset(path, block.Name, network.Number))
                {
                    yield return finding;
                }
            }
        }
    }

    public static IEnumerable<Finding> CheckC005CharsetDbMembers(string ownerName, IReadOnlyList<DbMember> members)
    {
        foreach (var member in members)
        {
            if (!IsValidIdentifier(member.Name))
            {
                yield return new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    ownerName,
                    null,
                    $"Member name '{member.Name}' contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename '{member.Name}' to use only letters, digits, and underscore, starting with a letter.");
            }

            if (member.NestedMembers is { Count: > 0 })
            {
                foreach (var finding in CheckC005CharsetDbMembers(ownerName, member.NestedMembers))
                {
                    yield return finding;
                }
            }
        }
    }

    private static IEnumerable<Finding> CheckPathCharset(string path, string blockName, int networkNumber)
    {
        foreach (var rawComponent in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (rawComponent.StartsWith('%'))
            {
                // %Xn/%Bn/%Wn slice-address syntax, not a user-chosen name — not C-005's concern.
                continue;
            }

            var bracketIndex = rawComponent.IndexOf('[');
            var name = bracketIndex >= 0 ? rawComponent[..bracketIndex] : rawComponent;

            if (!IsValidIdentifier(name))
            {
                yield return new Finding(
                    "C-005",
                    FindingSeverity.Error,
                    blockName,
                    networkNumber,
                    $"Name component '{name}' (from '{path}') contains characters other than letters/digits/underscore, or doesn't start with a letter.",
                    $"Rename '{name}' to use only letters, digits, and underscore, starting with a letter.");
            }
        }
    }

    private static bool IsValidIdentifier(string component)
    {
        if (component.Length == 0 || !char.IsAsciiLetter(component[0]))
        {
            return false;
        }

        foreach (var c in component)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '_')
            {
                return false;
            }
        }

        return true;
    }

    // C-201 (error) — every network has a title; every block has a header comment. Two separate
    // sub-checks, deliberately: the header-comment concept applies to any content kind that has
    // its own Comment field (BLOCK and DB both do); the network-title concept only exists for
    // BLOCK-kind files, since DB/TYPE/TAGTABLE files have no networks at all.
    public static IEnumerable<Finding> CheckC201HeaderComment(string ownerName, string? comment)
    {
        if (string.IsNullOrEmpty(comment))
        {
            yield return new Finding(
                "C-201",
                FindingSeverity.Error,
                ownerName,
                null,
                $"'{ownerName}' has no header comment.",
                "Add a COMMENT stating the block's purpose (and, per C-201, author/revision — not captured by this IR extraction; flagged as a known gap, not silently assumed satisfied).");
        }
    }

    public static IEnumerable<Finding> CheckC201NetworkTitles(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            if (network.IsEmpty)
            {
                // A network with zero Parts has nothing to title - not this rule's concern
                // (matches the corpus's own trailing-[empty]-network pattern, e.g. TimerSample
                // Network 4).
                continue;
            }

            if (string.IsNullOrEmpty(network.Title))
            {
                yield return new Finding(
                    "C-201",
                    FindingSeverity.Error,
                    block.Name,
                    network.Number,
                    $"Network {network.Number} has real logic but no title.",
                    "Add a NETWORK title describing what this network does.");
            }
        }
    }

    // C-301 (error) — no absolute addressing; symbolic access only, except: alarm words (per
    // C-501's own two-part condition), comms mapping, and documented data-handling blocks (C-105).
    // Phase-1 scope, stated honestly: this checks slice-access usage (.%Xn/.%Bn/.%Wn) against the
    // three documented exceptions. It does NOT independently detect raw %M/%DBx.DBWy absolute
    // addressing — whether that shape is even representable in the current IR model is unconfirmed
    // (no grounding comment/test/fixture anywhere shows this codebase has seen one), so claiming to
    // catch it would overstate what's actually verified.
    //
    // The comms-mapping and data-handling exceptions share one mechanism here (deliberately loose,
    // Phase-1-appropriate): a block "self-identifies" via its own name or header comment containing
    // a small set of keywords. This is NOT a full C-105 compliance check (C-105 itself — e.g. its
    // own "header comment states it contains indexed access" requirement — is Bucket B/C, out of
    // Phase-1 scope); it only decides whether C-301's exception applies here.
    private static readonly string[] SelfIdentifyingKeywords = { "data-handling", "data handling", "comms", "communication", "recipe" };

    public static IEnumerable<Finding> CheckC301AbsoluteAddressing(IrBlock block)
    {
        if (IsSelfIdentifiedExemptBlock(block))
        {
            yield break;
        }

        foreach (var network in block.Networks)
        {
            var sliceWrites = SliceAccessWritesInNetwork(network).ToList();
            if (sliceWrites.Count == 0)
            {
                continue;
            }

            var satisfiesAlarmWordException = sliceWrites.Count == 1 && !string.IsNullOrEmpty(network.Title);
            if (satisfiesAlarmWordException)
            {
                continue;
            }

            yield return new Finding(
                "C-301",
                FindingSeverity.Error,
                block.Name,
                network.Number,
                $"Network {network.Number} writes {sliceWrites.Count} slice-access bit(s) ({string.Join(", ", sliceWrites)}) without satisfying the C-501 alarm-word exception (exactly one bit, network titled).",
                sliceWrites.Count > 1
                    ? "Split into one network per alarm bit, each titled with its own alarm text, or move this logic into a self-identified data-handling/comms block (C-105)."
                    : "Add a network title stating the alarm text, or move this logic into a self-identified data-handling/comms block (C-105).");

            yield return new Finding(
                "C-501",
                FindingSeverity.Warn,
                block.Name,
                network.Number,
                $"Network {network.Number}'s slice-access alarm bit(s) don't satisfy C-501's own conditions (exactly one bit per network, network title states the alarm text).",
                "Either restructure to satisfy C-501 as written, or — if this packed/summarized form is intentionally fine — propose it as a documented exception in docs/06-lad-conventions.md rather than leaving the rule and the practice disagreeing.");
        }
    }

    private static bool IsSelfIdentifiedExemptBlock(IrBlock block)
    {
        var haystack = $"{block.Name} {block.Comment}".ToLowerInvariant();
        return SelfIdentifyingKeywords.Any(keyword => haystack.Contains(keyword, StringComparison.Ordinal));
    }

    private static IEnumerable<string> SliceAccessWritesInNetwork(IrNetwork network)
    {
        foreach (var assignment in network.Assignments)
        {
            if (IsSliceAccessTag(assignment.CoilTag))
            {
                yield return assignment.CoilTag;
            }
        }
    }

    private static bool IsSliceAccessTag(string tag)
    {
        var lastDot = tag.LastIndexOf('.');
        return lastDot >= 0 && lastDot + 1 < tag.Length && tag[lastDot + 1] == '%';
    }

    // C-406 (error) — TON is the only timer instruction used; TOF/TONR are violations. Checked in
    // two genuinely different places: the *declaration* form (a DbMember whose own Datatype is
    // "TONR_TIME"/"TOF_TIME" — what's actually sitting in FBTimers.ir, which has zero networks)
    // and the *usage* form (TimerBinding.Kind inside a network's own Timers list — what
    // TimingAndCalls.ir calls). A check against only one form silently misses real violations that
    // only show up in the other.
    public static IEnumerable<Finding> CheckC406TimerUsage(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            foreach (var timer in network.Timers)
            {
                if (timer.Kind is TimerKind.Tonr or TimerKind.Tof)
                {
                    yield return new Finding(
                        "C-406",
                        FindingSeverity.Error,
                        block.Name,
                        network.Number,
                        $"Network {network.Number} calls '{timer.InstancePath}' as a {timer.Kind} timer - only TON is permitted.",
                        "Replace with TON plus explicit inversion/edge logic per C-406's own construction guidance (off-delay/retentive behavior built from TON, not TOF/TONR).");
                }
            }
        }
    }

    public static IEnumerable<Finding> CheckC406TimerDeclarations(string ownerName, IReadOnlyList<DbMember> members)
    {
        foreach (var member in members)
        {
            if (member.Datatype is "TONR_TIME" or "TOF_TIME")
            {
                yield return new Finding(
                    "C-406",
                    FindingSeverity.Error,
                    ownerName,
                    null,
                    $"Member '{member.Name}' is declared as {member.Datatype} - only TON_TIME is permitted.",
                    "Replace with a TON_TIME instance plus explicit inversion/edge logic per C-406.");
            }

            if (member.NestedMembers is { Count: > 0 })
            {
                foreach (var finding in CheckC406TimerDeclarations(ownerName, member.NestedMembers))
                {
                    yield return finding;
                }
            }
        }
    }

    // C-102 (error) — no jumps (JMP/LBL). C-401 (error) — no counter instructions
    // (CTU/CTD/CTUD). C-404 (error) — no built-in edge instructions (-|P|-/-|N|-). All three:
    // genuinely vacuous against this converter's IR model today, not "no violations found in this
    // corpus." No Jump/Label, Counter, or P_TRIG/N_TRIG construct exists anywhere in
    // Converter.Ir.Model - the parser's own FlgNetParser.SupportedPartNames whitelist rejects any
    // of these with UnsupportedConstructException before a .ir file can even exist (confirmed
    // directly against FlgNetParser.cs during S4 planning). A file that reaches this checker is
    // structurally incapable of violating any of the three, the same way a fish can't be checked
    // for "doesn't have legs" - these always return no findings, unconditionally, and the caller
    // reports their status as CheckedVacuous rather than Checked for every file, not just when a
    // scan happens to find nothing. Built anyway: cheap, and real defense-in-depth if
    // WAIT/Jump/counters (already-known open questions, see AITODO.md) ever get built into the IR
    // model later - at that point these checks start actually verifying something and their status
    // should be revisited.
    public static IEnumerable<Finding> CheckC102NoJumps(IrBlock block) => Enumerable.Empty<Finding>();

    public static IEnumerable<Finding> CheckC401NoCounters(IrBlock block) => Enumerable.Empty<Finding>();

    public static IEnumerable<Finding> CheckC404NoBuiltInEdgeInstructions(IrBlock block) => Enumerable.Empty<Finding>();

    // C-408 (error) — a timer's ET is never compared to produce a boolean trigger; each time
    // threshold is its own named timer, activated via Q (staged sequences chain timers). Reading ET
    // as a *value* (HMI/diagnostics/proportional) is permitted — that form MOVEs ET to a named
    // variable and carries no comparison, so it never matches here. Scope: ANY comparison operand
    // that is a `.ET` reference, not only comparison against a literal constant (the skill recipe's
    // broader, intent-matching form — owner-confirmed 2026-07-18).
    public static IEnumerable<Finding> CheckC408EtComparison(IrBlock block)
    {
        foreach (var network in block.Networks)
        {
            var etPaths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var expr in TagReferences.AllExpressions(network))
            {
                foreach (var path in EtOperandsInComparisons(expr, insideCompare: false))
                {
                    if (seen.Add(path))
                    {
                        etPaths.Add(path);
                    }
                }
            }

            if (etPaths.Count == 0)
            {
                continue;
            }

            yield return new Finding(
                "C-408",
                FindingSeverity.Error,
                block.Name,
                network.Number,
                $"Network {network.Number} compares timer ET ({string.Join(", ", etPaths)}) to produce a boolean trigger - C-408 forbids this. Use a named timer's Q (staged sequences chain timers: Q of stage n enables stage n+1's timer); read ET only as a value copied to a named variable.",
                "Replace the ET comparison with a named timer whose Q gives the trigger; if the ET value itself is needed (display/diagnostics/proportional), MOVE it to a named variable instead of wiring ET mid-rung.");
        }
    }

    // Yields every `.ET` tag reference that appears inside a comparison subtree. `insideCompare`
    // flips true when descending into a Compare's operands, so a `.ET` read outside any comparison
    // (the permitted value read) is never yielded.
    private static IEnumerable<string> EtOperandsInComparisons(Expr expr, bool insideCompare)
    {
        switch (expr)
        {
            case Expr.TagRef tagRef:
                if (insideCompare && tagRef.Path.EndsWith(".ET", StringComparison.Ordinal))
                {
                    yield return tagRef.Path;
                }

                break;
            case Expr.And and:
                foreach (var operand in and.Operands)
                {
                    foreach (var path in EtOperandsInComparisons(operand, insideCompare))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Or or:
                foreach (var operand in or.Operands)
                {
                    foreach (var path in EtOperandsInComparisons(operand, insideCompare))
                    {
                        yield return path;
                    }
                }

                break;
            case Expr.Not not:
                foreach (var path in EtOperandsInComparisons(not.Operand, insideCompare))
                {
                    yield return path;
                }

                break;
            case Expr.Compare compare:
                foreach (var path in EtOperandsInComparisons(compare.Left, insideCompare: true))
                {
                    yield return path;
                }

                foreach (var path in EtOperandsInComparisons(compare.Right, insideCompare: true))
                {
                    yield return path;
                }

                break;
            case Expr.Literal:
                break;
        }
    }
}
