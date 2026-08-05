using System.Text.RegularExpressions;

namespace Converter.RelationReconcile;

// Focused readers of the four relation-bearing artifacts. Strict by design, following the TargetScan
// precedent: parse the documented shape, and when the shape is not found say so LOUDLY rather than
// returning zero rows. Every regex below was measured against the real artifacts — where the SKILL
// prose and the artifact disagreed, the artifact won (ids are `C1`, not `C-01`; the ledger's first
// header cell is `relation`; an empty precondition cell is an em-dash).
public static class RelationArtifactParsers
{
    // `- **C1**` / `- **P12**` — unhyphenated, unpadded.
    private static readonly Regex SpecBullet = new(@"^\s*-\s*\*\*([CP]\d+)\*\*", RegexOptions.Compiled);

    // `## Ledger — `FilterUnitInst2` (Dust Filter Unit 1)`
    private static readonly Regex LedgerHeading = new(@"^##\s+Ledger\s*[-–—]\s*`([^`]+)`", RegexOptions.Compiled);

    // `| relation | disposition | evidence | precondition class |`
    private static readonly Regex LedgerHeader = new(@"^\|\s*relation\s*\|", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex LedgerRow = new(@"^\|\s*([CP]\d+)\s*\|(.*)$", RegexOptions.Compiled);

    // `### `FilterUnitInst2` — Dust Filter Unit 1 (FilterUnitSystem)`
    private static readonly Regex RegisterHeading = new(@"^###\s+`([^`]+)`", RegexOptions.Compiled);

    // `| REQ-001 | C1 | text | class | provenance | open |`
    private static readonly Regex RegisterRow = new(@"^\|\s*REQ-\d+\s*\|\s*([CP]\d+)\s*\|", RegexOptions.Compiled);

    // A D3 render term tag: `[C2]` or `[P1,P2]`.
    private static readonly Regex RenderTermTag = new(@"\[([CP]\d+(?:\s*,\s*[CP]\d+)*)\]", RegexOptions.Compiled);

    // The D3 render's per-network instance line: `NETWORK "…" instance: `FilterUnitInst2``. Hoisted
    // here 2026-08-05 (audit F-41) — it used to be compiled inline inside ParseRender's per-line loop,
    // the one pattern in this file that paid the Regex construction cost once per line of every
    // artifact read rather than once per process.
    private static readonly Regex RenderInstance = new(@"instance:\s*`?([A-Za-z_][A-Za-z0-9_]*)`?", RegexOptions.Compiled);

    private static readonly Regex BacktickToken = new(@"`([^`]+)`", RegexOptions.Compiled);

    private static readonly Regex Identifier = new(@"^[%A-Za-z_][A-Za-z0-9_.%\[\]]*$", RegexOptions.Compiled);

    // A stopped D3 announces itself in a HEADING. Both real forms are in the committed corpus:
    //   `## **STOPPED — all six instances. No render produced.**`   (rerun2, rerun3)
    //   `> ## RUN STATUS — D3 (render) STOPPED. D0, D1, D2 COMPLETE.` (rerun, blockquoted, and the
    //    word is mid-sentence rather than straight after the `##`)
    // Tightened 2026-08-05 (audit F-41): this was a bare `STOPPED` substring match over the whole
    // file, so the uppercase word anywhere in prose or in a table cell ("the conveyor must be
    // STOPPED before…") flipped the runner's "verify this is intended" warning into a reassuring
    // "legitimately stopped". Scoping to heading lines is as tight as the real corpus allows —
    // anchoring to `^##\s+\*\*STOPPED` specifically, as first proposed, would have mis-reported
    // gen/PlantAutoControl-bench-rerun/code-structure.md, whose stop is declared in a blockquoted
    // `## RUN STATUS` heading. Neither branch gates the exit code; this only changes what the note says.
    private static readonly Regex StoppedMarker =
        new(@"^[ \t>]*#{1,6}[^\n]*\bSTOPPED\b", RegexOptions.Compiled | RegexOptions.Multiline);

    // One spec file per instance; the instance is the FILENAME, which is the only place it is stated
    // unambiguously (the heading carries a prose title too).
    public static Leg ParseSpecs(string specsDir)
    {
        var keys = new List<RelationKey>();
        var files = Directory.EnumerateFiles(specsDir, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        if (files.Count == 0)
        {
            throw new RelationReconcileFormatException($"specs: no .md files under '{specsDir}'.");
        }

        foreach (var file in files)
        {
            var instance = Path.GetFileNameWithoutExtension(file);
            foreach (var line in File.ReadLines(file))
            {
                var m = SpecBullet.Match(line);
                if (m.Success)
                {
                    keys.Add(new RelationKey(instance, m.Groups[1].Value));
                }
            }
        }

        if (keys.Count == 0)
        {
            throw new RelationReconcileFormatException(
                $"specs: parsed 0 relations from {files.Count} file(s) under '{specsDir}' — expected '- **C1**' bullets. " +
                "Refusing to report a clean reconcile from an unparsed leg.");
        }

        return new Leg(LegKind.Specs, true, keys);
    }

    // The D2 ledger, plus the raw cells needed for the citation check.
    public static (Leg Leg, IReadOnlyList<(RelationKey Key, string Disposition, string Evidence, string Precondition)> Rows)
        ParseLedger(string ledgerPath)
    {
        var keys = new List<RelationKey>();
        var rows = new List<(RelationKey, string, string, string)>();
        string? instance = null;
        var sawHeader = false;

        foreach (var line in File.ReadLines(ledgerPath))
        {
            var heading = LedgerHeading.Match(line);
            if (heading.Success)
            {
                instance = heading.Groups[1].Value;
                continue;
            }

            if (LedgerHeader.IsMatch(line))
            {
                sawHeader = true;
                continue;
            }

            var row = LedgerRow.Match(line);
            if (!row.Success || instance is null)
            {
                continue;
            }

            var cells = row.Groups[2].Value.Split('|').Select(c => c.Trim()).ToList();
            var key = new RelationKey(instance, row.Groups[1].Value);
            keys.Add(key);
            rows.Add((key,
                Normalize(Cell(cells, 0)),
                Cell(cells, 1),
                Normalize(Cell(cells, 2))));
        }

        if (!sawHeader)
        {
            throw new RelationReconcileFormatException(
                $"ledger: no '| relation |' table header found in '{ledgerPath}'. " +
                "Refusing to report a clean reconcile from an unparsed leg.");
        }

        if (keys.Count == 0)
        {
            throw new RelationReconcileFormatException(
                $"ledger: found the table header but parsed 0 rows in '{ledgerPath}'.");
        }

        return (new Leg(LegKind.Ledger, true, keys), rows);
    }

    // The derived register's per-instance REQ tables. This fixture's register is a 6-column table whose
    // `Rel` column carries the relation id; it declares its own deviation from the heading-block form.
    public static Leg ParseRegister(string registerPath)
    {
        var keys = new List<RelationKey>();
        string? instance = null;

        foreach (var line in File.ReadLines(registerPath))
        {
            var heading = RegisterHeading.Match(line);
            if (heading.Success)
            {
                instance = heading.Groups[1].Value;
                continue;
            }

            var row = RegisterRow.Match(line);
            if (row.Success && instance is not null)
            {
                keys.Add(new RelationKey(instance, row.Groups[1].Value));
            }
        }

        if (keys.Count == 0)
        {
            throw new RelationReconcileFormatException(
                $"register: parsed 0 relations from '{registerPath}' — expected per-instance '### `Instance`' " +
                "headings over '| REQ-nnn | C1 | …' rows. Refusing to report a clean reconcile from an unparsed leg.");
        }

        return new Leg(LegKind.Register, true, keys);
    }

    // The D3 render. Absent (a stopped rung) is a legitimate state and is reported as such — never as a
    // leg that reconciles trivially.
    public static Leg ParseRender(string ledgerPath)
    {
        var text = File.ReadAllText(ledgerPath);
        var keys = new List<RelationKey>();
        string? instance = null;

        foreach (var line in text.Split('\n'))
        {
            var heading = RenderInstance.Match(line);
            if (heading.Success)
            {
                instance = heading.Groups[1].Value;
            }

            if (instance is null)
            {
                continue;
            }

            foreach (Match tag in RenderTermTag.Matches(line))
            {
                foreach (var id in tag.Groups[1].Value.Split(',').Select(s => s.Trim()))
                {
                    keys.Add(new RelationKey(instance, id));
                }
            }
        }

        if (keys.Count == 0)
        {
            // Distinguish "stopped, by design" from "we could not read it".
            return new Leg(LegKind.Render, Present: false, Array.Empty<RelationKey>());
        }

        return new Leg(LegKind.Render, true, keys.Distinct().ToList());
    }

    // Every backticked identifier-shaped token in an evidence cell. The cell is free prose containing a
    // file name, a network label and expression fragments as well as the cited member — so DO NOT guess
    // which one is "the tag". Extract them all; the caller classifies each and reports per token.
    public static IReadOnlyList<string> EvidenceTokens(string evidence) =>
        BacktickToken.Matches(evidence)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(t => Identifier.IsMatch(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public static bool LooksStopped(string ledgerPath) => StoppedMarker.IsMatch(File.ReadAllText(ledgerPath));

    private static string Cell(IReadOnlyList<string> cells, int index) =>
        index < cells.Count ? cells[index] : string.Empty;

    // `**rebind**` renders bold; an empty precondition cell is an em-dash.
    private static string Normalize(string cell)
    {
        var trimmed = cell.Trim().Trim('*').Trim();
        return trimmed is "—" or "-" or "–" ? string.Empty : trimmed;
    }
}
