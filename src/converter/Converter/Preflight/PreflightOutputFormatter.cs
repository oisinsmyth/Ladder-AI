using System.Text;
using System.Text.Json;

namespace Converter.Preflight;

// Mirrors ReviewOutputFormatter/DigestOutputFormatter's "one record, two renderers" pattern.
public static class PreflightOutputFormatter
{
    public static string FormatText(PreflightReport report)
    {
        var sb = new StringBuilder();

        foreach (var file in report.Files)
        {
            sb.Append("FILE: ").Append(file.FilePath).Append('\n');
            if (file.Name is not null)
            {
                sb.Append("  NAME: ").Append(file.Name).Append('\n');
            }

            // "CLEAN" is about the GATING findings — a file whose only entries are the reported
            // harness-scope ones has passed, and must say so while still printing them.
            if (!file.Findings.Any(x => x.Gates))
            {
                sb.Append("  CLEAN\n");
            }

            foreach (var finding in file.Findings)
            {
                sb.Append(finding.Gates ? "  [" : "  [INFO, does not gate] [").Append(finding.Check).Append("] ").Append(finding.Description).Append('\n');
            }

            sb.Append('\n');
        }

        foreach (var warning in report.IndexWarnings)
        {
            sb.Append("INDEX WARNING: ").Append(warning).Append('\n');
        }

        var totalFindings = report.Files.Sum(f => f.Findings.Count(x => x.Gates));
        var nonGating = report.Files.Sum(f => f.Findings.Count(x => !x.Gates));
        sb.Append("SUMMARY: ").Append(report.Files.Count).Append(" file(s), ").Append(totalFindings).Append(" finding(s)");
        // Always appended, including the zero: an absent clause cannot distinguish "nothing was
        // exempted" from "the harness scope never ran".
        sb.Append(", ").Append(nonGating).Append(" reported but not gating (harness-scope)\n");

        // *** THE DENOMINATOR. *** Deliberately NOT bolted onto the SUMMARY line above: "1 file(s)"
        // there is the BATCH SIZE, so `1 file(s) of 43` would assert that one of forty-three project
        // files was pre-flighted — the exact category slip this change exists to remove — and that
        // line is something scripts and skills grep (TagStatusOutputFormatter's "append, never
        // insert"). Placed directly after it instead, which is where drift-check's COMPARED: and
        // reuse-scan's own retrofitted denominator sit.
        //
        // The paths ride beside the counts on drift-check's precedent (`COMPARED: … (project=…)`),
        // because the whole defect being closed is that the answer depends on --project: the same
        // file at the same instant reported 2 findings against a 43-file export and 5 against a
        // 15-file one, with nothing in the output naming either corpus.
        var corpus = report.Corpus;
        sb.Append("CORPUS: ").Append(corpus.ProjectFileCount).Append(" project file(s) + ")
          .Append(corpus.BatchFileCount).Append(" batch file(s)")
          .Append("  (project=").Append(corpus.ProjectDir).Append(")\n");

        // ONE LINE, FIVE COUNTS, EACH TAGGED WITH THE FINDING CLASS IT DECIDES — interface-check's
        // `EXAMINED: n … <per-section breakdown>` shape. There are FOUR corpus walks behind this
        // report and they index different things, so a single number could only be honest under one
        // of them: "does not resolve to any BLOCK in the project" is falsifiable against the
        // block-name count and nothing else, and the member-path findings never touch ProjectIndex.
        //
        // Printed including the zeroes, for the reason the harness-scope clause above already is:
        // an absent count cannot distinguish "the corpus held none" from "the walk never ran".
        sb.Append("RESOLVED AGAINST: ")
          .Append(corpus.BlockNameCount).Append(" block name(s) [call, instanceof]; ")
          .Append(corpus.TagRootNameCount).Append(" tag/DB root name(s) [tag root]; ")
          .Append(corpus.MemberBodyCount).Append(" DB/UDT body(ies) [member path, literal-fit]; ")
          .Append(corpus.CalleeInterfaceCount).Append(" callee interface(s) [convert: wired CALL]; ")
          .Append(corpus.HarnessCorpusFileCount).Append(" file(s) classified [review:harness-scope]\n");

        // reuse-scan's ABSENT: analogue — the one shape where the counts above are all structurally
        // uninformative. NOT an exit code, and that asymmetry with reuse-scan is deliberate: exit 0
        // there LICENSES "nothing to reuse, write a new block", whereas a narrow or empty corpus
        // makes preflight NOISIER, not quieter — its false answer is a false accusation on exit 1,
        // and no exit code repairs a false accusation. Only the denominator under it does. A
        // self-contained batch (a new block plus its new DB, pre-flighted before either exists in
        // the project) is a legitimate run and must not be failed for it.
        if (corpus.ProjectContributedNothing)
        {
            sb.Append("PROJECT CORPUS EMPTY: --project contributed 0 .ir file(s), so every name above was ")
              .Append("resolved against the batch alone — neither a finding nor a CLEAN verdict here is ")
              .Append("evidence about the project.\n");
        }

        sb.Append("PRE-FLIGHT ONLY: a static filter in front of the compile gate — import + block-level compile still decides (hard rule 4).\n");

        return sb.ToString().TrimEnd('\n', '\r');
    }

    public static string FormatJson(PreflightReport report)
    {
        var payload = new
        {
            files = report.Files.Select(f => new
            {
                filePath = f.FilePath,
                name = f.Name,
                findings = f.Findings.Select(x => new { check = x.Check, description = x.Description, gates = x.Gates }),
            }),
            indexWarnings = report.IndexWarnings,

            // The payload carried NO summary object at all until 2026-08-23: a consumer counted
            // array elements and could reach no denominator, which is a worse position than the
            // text reader's — at least the text reader could see the --project argument scroll past.
            // Same record, same numbers, same per-finding-class split as the text renderer.
            corpus = new
            {
                projectDir = report.Corpus.ProjectDir,
                projectFileCount = report.Corpus.ProjectFileCount,
                batchFileCount = report.Corpus.BatchFileCount,
                projectContributedNothing = report.Corpus.ProjectContributedNothing,
                blockNameCount = report.Corpus.BlockNameCount,
                tagRootNameCount = report.Corpus.TagRootNameCount,
                typeNameCount = report.Corpus.TypeNameCount,
                memberBodyCount = report.Corpus.MemberBodyCount,
                calleeInterfaceCount = report.Corpus.CalleeInterfaceCount,
                harnessCorpusFileCount = report.Corpus.HarnessCorpusFileCount,
            },
        };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
