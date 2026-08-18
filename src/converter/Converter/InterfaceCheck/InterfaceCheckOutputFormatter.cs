using System.Text;
using System.Text.Json;

namespace Converter.InterfaceCheck;

// One record set, two renderers — the shape every other subcommand here uses (Digest/TagStatus/
// ReachableState). The JSON form is what a harness gate consumes: it carries the ir-hash STAMP, so a
// consumer can tell "nobody ran it" from "ran it against a different version of the block".
public static class InterfaceCheckOutputFormatter
{
    public static string FormatText(InterfaceCheckReport report)
    {
        var sb = new StringBuilder();

        sb.Append("INTERFACE-CHECK ").Append(report.Block).Append('\n');
        sb.Append("CORPUS: ").Append(report.CorpusSummary).Append('\n');

        if (!report.Checked)
        {
            // Provenance is printed HERE TOO. It used to appear only on a run that completed, so the
            // outcome most likely to have been caused by the wrong input file was the one that never
            // said which file it read.
            if (report.RequirementsSource.Length > 0)
            {
                sb.Append("REQUIRED FROM: ").Append(report.RequirementsSource).Append('\n');
            }

            sb.Append("NOT CHECKED — ").Append(report.NotCheckedReason).Append('\n');
            sb.Append("*** NOT CHECKED IS NOT A PASS AND IS NOT A FAIL AGAINST THE BLOCK. *** Nothing downstream may\n")
              .Append("    cite this run as evidence that the block does or does not carry a required signal.\n");
            return sb.ToString().TrimEnd('\n');
        }

        sb.Append("BLOCK FILE: ").Append(report.BlockFile).Append('\n');
        sb.Append("IR-HASH: ").Append(report.IrHash)
          .Append("   <- the stamp this result was established against; carry it, never a bool\n");
        sb.Append("REQUIRED FROM: ").Append(report.RequirementsSource).Append('\n');

        sb.Append("EXAMINED: ").Append(report.ExaminedMembers.Count).Append(" interface member name(s) — ")
          .Append(string.Join(", ", report.SectionCounts.Select(kv => $"{kv.Key} {kv.Value}")))
          .Append('\n');
        sb.Append("EXCLUDED: ").Append(report.ExcludedTempMembers.Count)
          .Append(" TEMP member(s) — a temp does not survive the scan and cannot be observed from outside the block")
          .Append(report.ExcludedTempMembers.Count == 0 ? "" : $" ({string.Join(", ", report.ExcludedTempMembers)})")
          .Append('\n');

        if (report.OpaqueMembers.Count > 0)
        {
            sb.Append("OPAQUE: ").Append(report.OpaqueMembers.Count)
              .Append(" member(s) could not be opened — ")
              .Append(string.Join("; ", report.OpaqueMembers.Select(o => $"{o.Member} : {o.Datatype} ({o.Reason})")))
              .Append('\n');
        }

        sb.Append('\n');
        foreach (var requirement in report.Requirements)
        {
            sb.Append(requirement.Status switch
                {
                    RequirementStatus.Present => "  PRESENT ",
                    RequirementStatus.Missing => "  MISSING ",
                    _ => "  UNJUDGEABLE ",
                })
              .Append(requirement.Name);

            if (requirement.FoundAt.Length > 0)
            {
                sb.Append("  @ ").Append(requirement.FoundAt);
            }

            sb.Append('\n');
            sb.Append("      ").Append(requirement.Detail).Append('\n');
        }

        sb.Append('\n');
        if (report.BlockFails)
        {
            sb.Append("*** FAIL AGAINST THE BLOCK: ").Append(report.Missing.Count).Append(" of ")
              .Append(report.Requirements.Count)
              .Append(" required response signal(s) are absent from the block's interface. ***\n")
              .Append("    THE SUBMISSION IS NOT AT FAULT. Every other outcome in this pipeline blames the submission\n")
              .Append("    — a REFUSED sends an author to edit the vector. This one does not: the vector names a signal\n")
              .Append("    the specification names, and the BLOCK does not provide it.\n");
        }

        // *** BOTH HALVES ARE PRINTED WHEN BOTH ARE TRUE. *** A real FAIL and an unjudgeable input are
        // independent facts, and an `else` here would have hidden whichever came second — the reader
        // obeys the most actionable half, so the half that is missing is the one that gets acted on.
        if (report.Unjudgeable.Count > 0)
        {
            sb.Append(report.BlockFails ? "ALSO NOT CHECKED — " : "NOT CHECKED — ")
              .Append(report.Unjudgeable.Count).Append(" of ")
              .Append(report.Requirements.Count)
              .Append(" required name(s) could not be judged, so this run does not answer the whole question it was asked")
              .Append(report.BlockFails ? " — the FAIL above stands on the names that WERE judged.\n" : ".\n");
        }

        if (!report.BlockFails && report.Unjudgeable.Count == 0)
        {
            sb.Append("PASS — all ").Append(report.Requirements.Count)
              .Append(" required response signal(s) are present in the block's interface.\n")
              .Append("    THIS COVERS NAMES ONLY. It says nothing about whether the block DRIVES them correctly,\n")
              .Append("    which is what a conformance run is for.\n");
        }

        return sb.ToString().TrimEnd('\n');
    }

    public static string FormatJson(InterfaceCheckReport report)
    {
        var payload = new
        {
            block = report.Block,
            blockFile = report.BlockFile,
            irHash = report.IrHash,
            @checked = report.Checked,
            notCheckedReason = report.NotCheckedReason,
            blockFails = report.BlockFails,
            requirementsSource = report.RequirementsSource,
            corpus = report.CorpusSummary,
            examinedCount = report.ExaminedMembers.Count,
            examinedMembers = report.ExaminedMembers,
            excludedTempMembers = report.ExcludedTempMembers,
            sectionCounts = report.SectionCounts,
            opaqueMembers = report.OpaqueMembers.Select(o => new { member = o.Member, datatype = o.Datatype, reason = o.Reason }),
            requirements = report.Requirements.Select(r => new
            {
                name = r.Name,
                status = r.Status.ToString(),
                foundAt = r.FoundAt,
                detail = r.Detail,
            }),
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
