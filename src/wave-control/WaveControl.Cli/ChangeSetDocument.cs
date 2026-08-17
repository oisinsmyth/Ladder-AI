using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ladder.Wave.Cli
{
    /// <summary>One row of a change set: an object, what happened to it, and where that came from.</summary>
    public sealed class ChangeSetEntry
    {
        internal ChangeSetEntry(string name, ChangeNature nature, string source, bool isChange, string note)
        {
            Name = name;
            Nature = nature;
            Source = source;
            IsChange = isChange;
            Note = note;
        }

        /// <summary>The object's name, as the producing document names it.</summary>
        public string Name { get; }

        /// <summary>What happened to it. <see cref="ChangeNature.Unknown"/> is a refusal downstream.</summary>
        public ChangeNature Nature { get; }

        /// <summary>The row of the producing document this came from.</summary>
        public string Source { get; }

        /// <summary>
        /// FALSE for a row the producer emitted that is NOT a change (a drift-check MATCH). Carried
        /// rather than filtered out at the source, so the denominator can name what it excluded — a
        /// change set of 4 out of 43 rows and a change set of 4 out of 4 are different facts.
        /// </summary>
        public bool IsChange { get; }

        /// <summary>Why it is not a change, or empty.</summary>
        public string Note { get; }
    }

    /// <summary>What a change set read produced, or why it produced nothing.</summary>
    public sealed class ChangeSetResult
    {
        internal ChangeSetResult(bool ok, IReadOnlyList<ChangeSetEntry> entries, string provenance, string refusal)
        {
            Ok = ok;
            Entries = entries;
            Provenance = provenance;
            Refusal = refusal;
        }

        /// <summary>TRUE when a change set was read.</summary>
        public bool Ok { get; }

        /// <summary>Every row, changes and non-changes alike.</summary>
        public IReadOnlyList<ChangeSetEntry> Entries { get; }

        /// <summary>Where the change set came from. Required, never blank on a successful read.</summary>
        public string Provenance { get; }

        /// <summary>Why nothing was read.</summary>
        public string Refusal { get; }

        internal static ChangeSetResult Refuse(string refusal) =>
            new ChangeSetResult(false, new ChangeSetEntry[0], string.Empty, refusal);
    }

    /// <summary>
    /// What the exports directory a <c>drift-check</c> report was produced against actually WAS. The
    /// converter's own note is the reason this exists: "the exports directory means two different things
    /// depending on what filled it, and the tool cannot tell them apart from the inside".
    /// </summary>
    /// <remarks>
    /// 🔴 <b>AND NEITHER CAN A CONSUMER, BECAUSE THE JSON DOES NOT RECORD WHETHER <c>--complete</c> WAS
    /// PASSED.</b> Measured 2026-08-17 against a real report: the document's keys are
    /// <c>entries · comparedCount · examinedNothing · projectDir · exportsDir · hasDrift</c>, and none of
    /// them says which question was asked. A <c>Skipped</c> row therefore means "added since the last
    /// export, nothing is wrong" against a committed corpus and "THIS BLOCK IS NOT IN THE CONTROLLER"
    /// against a fresh dump — opposite consequences from an identical row. So the caller must SAY, and
    /// there is deliberately no default: a wrong default here silently turns 17 non-events into 17
    /// pending downloads, or the reverse.
    /// </remarks>
    public enum ExportsMeaning
    {
        /// <summary>Not stated. Refused.</summary>
        Unstated = 0,

        /// <summary>A committed export corpus that may legitimately lag the <c>.ir</c> (FI-26's use).</summary>
        CommittedCorpus = 1,

        /// <summary>A fresh <c>export-all</c> dump — the whole picture (FI-70's <c>--complete</c> use).</summary>
        ControllerDump = 2,
    }

    /// <summary>
    /// Reads a change set from a <c>converter drift-check --json</c> report (COMPUTED), or from an
    /// operator's <c>--changed</c> list (DECLARED).
    ///
    /// <para>The two paths are mutually exclusive by refusal, for <see cref="ReachableStateDocument"/>'s
    /// reason: taking both would mean silently choosing which to believe. The declared path is kept
    /// because a real, load-bearing change set can exist only as a RECORDED MEASUREMENT — the
    /// 2026-08-14 controller reconciliation needed TIA Portal, which one lane holds at a time — and a
    /// recorded fact and a fresh one are different evidence. So <c>--changed-from</c> is required, and
    /// its whole job is to make "I took this" and "this is written down" distinguishable in the output.</para>
    /// </summary>
    public static class ChangeSetDocument
    {
        /// <summary>Read a <c>drift-check --json</c> report.</summary>
        public static ChangeSetResult FromDriftCheck(string path, ExportsMeaning meaning)
        {
            if (meaning == ExportsMeaning.Unstated)
            {
                return ChangeSetResult.Refuse(
                    "--exports-are was not given. A drift-check report does not record whether --complete " +
                    "was passed, and a SKIPPED row means 'added since the last export, nothing pending' " +
                    "against a committed corpus and 'this object is NOT in the controller' against a fresh " +
                    "dump. There is no safe default: one reading turns non-events into downloads and the " +
                    "other drops real ones. Pass --exports-are committed-corpus | controller-dump.");
            }

            if (string.IsNullOrWhiteSpace(path))
            {
                return ChangeSetResult.Refuse("--drift-check needs a path to a `converter drift-check --json` report.");
            }

            if (!File.Exists(path))
            {
                return ChangeSetResult.Refuse("No drift-check report at '" + path + "'.");
            }

            JsonElement root;
            try
            {
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    root = document.RootElement.Clone();
                }
            }
            catch (JsonException ex)
            {
                return ChangeSetResult.Refuse("'" + path + "' is not readable JSON: " + ex.Message);
            }
            catch (IOException ex)
            {
                return ChangeSetResult.Refuse("'" + path + "' could not be read: " + ex.Message);
            }

            JsonElement entries;
            if (!root.TryGetProperty("entries", out entries) || entries.ValueKind != JsonValueKind.Array)
            {
                return ChangeSetResult.Refuse(
                    "'" + path + "' carries no `entries` array, so it is not a drift-check report. ABSENT is " +
                    "not EMPTY: reading a missing array as zero changes would report a clean change set " +
                    "about a document that was never examined.");
            }

            // The producer's own anti-vacuity number, carried through rather than recomputed. A report
            // that compared nothing is not a report of no changes.
            JsonElement compared;
            var comparedCount = root.TryGetProperty("comparedCount", out compared) && compared.TryGetInt32(out var c)
                ? c
                : -1;

            if (comparedCount == 0)
            {
                return ChangeSetResult.Refuse(
                    "'" + path + "' reports comparedCount = 0. NOTHING WAS COMPARED, so it is evidence " +
                    "about nothing — not a change set of size zero.");
            }

            var rows = new List<ChangeSetEntry>();

            foreach (var entry in entries.EnumerateArray())
            {
                JsonElement nameElement;
                var name = entry.TryGetProperty("name", out nameElement) && nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : string.Empty;

                JsonElement statusElement;
                var status = entry.TryGetProperty("status", out statusElement) && statusElement.ValueKind == JsonValueKind.String
                    ? statusElement.GetString()
                    : string.Empty;

                rows.Add(Translate(name ?? string.Empty, status ?? string.Empty, meaning));
            }

            if (rows.Count == 0)
            {
                return ChangeSetResult.Refuse("'" + path + "' has an empty `entries` array. Nothing to route.");
            }

            var provenance =
                "converter drift-check --json, read from '" + path + "'; " +
                comparedCount.ToString(CultureInfo.InvariantCulture) + " object(s) compared; the exports " +
                "directory is declared to be a " +
                (meaning == ExportsMeaning.ControllerDump
                    ? "FRESH export-all DUMP (the whole picture)"
                    : "COMMITTED export corpus (may legitimately lag the .ir)");

            return new ChangeSetResult(true, rows, provenance, string.Empty);
        }

        /// <summary>
        /// Translate one drift row into a change nature.
        ///
        /// <para>*** DRIFT IS NOT CHANGE, AND THE GAP BETWEEN THEM IS WHERE A ROUTER WOULD INVENT
        /// SOMETHING. *** A DRIFTED row says the two documents disagree; only a MODIFIED reading of it is
        /// defensible. An EXPORT-ONLY row says the object exists in the export and no .ir describes it —
        /// which is a DELETION if the intent is to make the controller match the IR, and is nothing at
        /// all if somebody simply has not written the .ir yet. Both readings are plausible and the
        /// document cannot settle it, so the nature is UNKNOWN and the classifier refuses it by name.</para>
        /// </summary>
        internal static ChangeSetEntry Translate(string name, string status, ExportsMeaning meaning)
        {
            switch (status)
            {
                case "Match":
                    return new ChangeSetEntry(name, ChangeNature.Unknown, status, false,
                        "the .ir and the export agree — not a change");

                case "Drifted":
                    return new ChangeSetEntry(name, ChangeNature.Modified, status, true, string.Empty);

                case "Skipped":
                    return meaning == ExportsMeaning.ControllerDump
                        ? new ChangeSetEntry(name, ChangeNature.Added, status, true, string.Empty)
                        : new ChangeSetEntry(name, ChangeNature.Unknown, status, false,
                            "no paired export in a COMMITTED corpus — an ordinary 'added since the last " +
                            "export'; it says nothing about the controller, so it is not a change here");

                case "ExportOnly":
                    return new ChangeSetEntry(name, ChangeNature.Unknown, status, true,
                        "in the exports and no .ir describes it");

                case "PairingFailure":
                    return new ChangeSetEntry(name, ChangeNature.Unknown, status, true,
                        "two files claim this identity — nothing could be compared");

                case "Error":
                    return new ChangeSetEntry(name, ChangeNature.Unknown, status, true,
                        "the comparison could not be RUN — an absence one level in, never a match");

                default:
                    return new ChangeSetEntry(name, ChangeNature.Unknown, status, true,
                        "unrecognised drift-check status '" + status + "'");
            }
        }

        /// <summary>
        /// Read an operator's declared change set: <c>--changed &lt;name&gt;=&lt;nature&gt;</c> repeated,
        /// plus a required <c>--changed-from</c>.
        /// </summary>
        public static ChangeSetResult FromDeclaration(IReadOnlyList<string> declarations, string provenance)
        {
            if (declarations == null || declarations.Count == 0)
            {
                return ChangeSetResult.Refuse("--changed was given no entries.");
            }

            if (string.IsNullOrWhiteSpace(provenance))
            {
                return ChangeSetResult.Refuse(
                    "--changed-from is required with --changed. A declared change set is a TRANSFERRED " +
                    "RESPONSIBILITY, not a measurement: this tool cannot re-take it, so the least it can " +
                    "do is refuse to print a verdict that does not say where the change set came from or " +
                    "whether anyone took it today.");
            }

            var rows = new List<ChangeSetEntry>();

            foreach (var declaration in declarations)
            {
                var split = (declaration ?? string.Empty).Split(new[] { '=' }, 2);
                if (split.Length != 2 || split[0].Trim().Length == 0)
                {
                    return ChangeSetResult.Refuse(
                        "--changed takes <name>=<added|deleted|modified|unknown>; got '" + declaration + "'. " +
                        "The nature is not optional — DB-1's table is keyed on it, and an entry that omitted " +
                        "it would have to be given a default, which is the thing this verb exists not to do.");
                }

                ChangeNature nature;
                switch (split[1].Trim().ToLowerInvariant())
                {
                    case "added": nature = ChangeNature.Added; break;
                    case "deleted": nature = ChangeNature.Deleted; break;
                    case "modified": nature = ChangeNature.Modified; break;
                    case "unknown": nature = ChangeNature.Unknown; break;
                    default:
                        return ChangeSetResult.Refuse(
                            "'" + split[1].Trim() + "' is not a change nature. Use added, deleted, modified, " +
                            "or unknown — and 'unknown' is a REFUSAL downstream, not a wildcard.");
                }

                rows.Add(new ChangeSetEntry(split[0].Trim(), nature, "--changed", true, string.Empty));
            }

            var duplicate = rows
                .GroupBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicate != null)
            {
                return ChangeSetResult.Refuse(
                    "'" + duplicate.Key + "' appears " + duplicate.Count() + " times in --changed. Two " +
                    "natures for one object is not a change set; one of them would silently win.");
            }

            return new ChangeSetResult(true, rows, "DECLARED via --changed: " + provenance.Trim(), string.Empty);
        }
    }
}
