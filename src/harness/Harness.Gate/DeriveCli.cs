using System.Text.Json;
using Harness.Results;

namespace Harness.Gate;

/// <summary>Exit codes for <c>harness-gate derive</c>, deliberately the same shape as <c>converter reachable-state</c>.</summary>
public static class DeriveExit
{
    /// <summary>Every derivable field the submission carries is attributed to a named producer and artifact.</summary>
    public const int Derived = 0;

    /// <summary>Usage, an unreadable input, or a refusal — a field that could not be attributed.</summary>
    public const int Refused = 1;

    /// <summary>
    /// <b>NOTHING DERIVED.</b> The submission carries no derivable field at all, so this tool has produced
    /// no evidence about anything. Empty is not clean: a run that stamped nothing must not report success,
    /// because a caller reading exit 0 would take it as "the provenance is complete".
    /// </summary>
    public const int NothingDerived = 2;

    /// <summary>
    /// Emitted, but at least one field was <b>WITHHELD BY NAME</b> under
    /// <c>--withhold-unattributable</c>. The output is a real submission and is missing something the
    /// input had.
    /// </summary>
    public const int WithheldSomething = 3;
}

/// <summary>What one derive run did, so the CLI and the tests read the same outcome.</summary>
/// <param name="Document">The stamped submission.</param>
/// <param name="Stamped">Fields that got a provenance record.</param>
/// <param name="Withheld">Fields removed because nothing could attribute them.</param>
/// <param name="Unattributable">Fields present, not attributable, and NOT withheld — the refusal set.</param>
/// <param name="Rejected">
/// 🔴 <b>Fields whose ARTIFACT was refused — the wrong kind, or the right kind reporting failure.</b>
/// Kept apart from <paramref name="Unattributable"/> because they are different mistakes with
/// different fixes: one means "you supplied nothing", this means "what you supplied is not an answer".
/// </param>
/// <param name="Mismatched">
/// Fields whose recomputed value DISAGREED with the authored one. The strongest finding this tool can
/// make, and the reason 1.1 recomputes rather than merely citing.
/// </param>
/// <param name="Compared">
/// 🔴 <b>WHAT EACH COMPARISON THAT AGREED ACTUALLY COMPARED — the denominator behind every
/// <c>COMPUTED</c>.</b>
///
/// <para>*** A MATCH USED TO BE ONE WORD ON ONE LINE, AND IT SAID NOTHING ABOUT ITS OWN SCOPE. *** The
/// comparisons produce that detail already — how many signals were checked, how many objects the
/// download loaded and WHICH AUTHORITY the manifest was read off, what the binding provides that the map
/// does not list — and none of it reached the operator unless the comparison FAILED. "It agreed" is true
/// of a comparison over 99 objects and of one over a single row; only one of those is worth the word
/// COMPUTED, and the reader has to be able to tell which they got.</para>
/// </param>
public sealed record DeriveOutcome(
    SubmissionDocument Document,
    IReadOnlyList<string> Stamped,
    IReadOnlyList<string> Withheld,
    IReadOnlyList<string> Unattributable,
    IReadOnlyList<string> Rejected,
    IReadOnlyList<string> Mismatched,
    IReadOnlyList<string> Compared);

/// <summary>
/// 🔴 <b><c>harness-gate derive</c> — the tool that makes gate 0c satisfiable.</b>
///
/// <para>*** WHAT THIS DOES, STATED NARROWLY ON PURPOSE. *** It ATTRIBUTES each derivable field the
/// submission carries to a named producer and a named artifact, and records the artifact's hash. It does
/// <b>NOT</b> compute the fields' values. That distinction matters and is not being blurred: a tool that
/// claimed to compute the map, the conflict graph and the compression ceilings while actually copying
/// them would be the same transcription problem with a longer command line.</para>
///
/// <para><b>Why attribution alone is still worth having.</b> Gate 0c refuses a derivable field carrying no
/// record; only this tool writes records; a record must name a producer from a closed set and an artifact
/// that still hashes to what was recorded. So a field cannot reach the gate as an unattributed opinion,
/// and the artifact it came from is named and re-checkable. What remains possible — pointing the tool at
/// the wrong artifact — is a visible, reviewable act rather than an invisible one.</para>
///
/// <para><b>What is deliberately NOT built yet:</b> genuine computation of <c>map</c> from the binding
/// (the copy-layer generator can do it, and comparing a computed map against the authored one would be
/// strictly stronger than attributing it); and the reachable-state and compression composers, which live
/// in a different solution. Those are named in the plan rather than half-built here, because a map
/// computed slightly wrong is worse than a map attributed correctly.</para>
/// </summary>
public static class DeriveCli
{
    /// <summary>
    /// Stamp provenance onto a submission.
    ///
    /// <para><b>Pure over its inputs</b> — the file reader is injected, nothing is written here, and the
    /// result is a new document. That is what lets the gate's own tests build a genuinely-derived fixture
    /// by calling the production path rather than by hand-writing a <c>derivation</c> block that could
    /// drift from what the tool actually emits.</para>
    /// </summary>
    /// <param name="document">The authored submission.</param>
    /// <param name="artifactByField">field → artifact path. Fields absent from this map are unattributable.</param>
    /// <param name="producerByField">field → producer name. Must be in <see cref="DerivationProducer.Known"/>.</param>
    /// <param name="readFile">Reads an artifact. A throw is treated as unreadable, which is a refusal here — the deriver, unlike the gate, has no business stamping a hash it could not compute.</param>
    /// <param name="withholdUnattributable">Remove an unattributable field instead of refusing over it.</param>
    public static DeriveOutcome Stamp(
        SubmissionDocument document,
        IReadOnlyDictionary<string, string> artifactByField,
        IReadOnlyDictionary<string, string> producerByField,
        Func<string, string> readFile,
        bool withholdUnattributable,
        Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(artifactByField);
        ArgumentNullException.ThrowIfNull(producerByField);
        ArgumentNullException.ThrowIfNull(readFile);

        var stamped = new List<string>();
        var withheld = new List<string>();
        var unattributable = new List<string>();
        var rejected = new List<string>();
        var mismatched = new List<string>();
        var compared = new List<string>();
        var records = new List<DerivationDocument>();

        foreach (var field in document.DerivableFieldsPresent())
        {
            var producer = producerByField.TryGetValue(field, out var p) ? p : DefaultProducerFor(field);
            var supplied = artifactByField.TryGetValue(field, out var artifact) && !string.IsNullOrWhiteSpace(artifact);

            // ---- the by-rule field, which has no artifact and must not pretend to ------------------
            if (DerivationProducer.KindFor(producer) == ArtifactKind.None)
            {
                if (supplied)
                {
                    rejected.Add($"{field}: '{producer}' produces no artifact, so --artifact {field}=... attributes it to a "
                        + "file that cannot be its source. Remove the flag; this field is settled by rule.");
                    continue;
                }

                var rule = RuleFor(document, field);
                if (rule is { } refusal)
                {
                    unattributable.Add($"{field}: {refusal}");
                    continue;
                }

                records.Add(new DerivationDocument
                {
                    Field = field,
                    Producer = producer,
                    Artifact = string.Empty,
                    ArtifactSha256 = string.Empty,
                    Verified = DerivationVerification.ByRule,
                });

                stamped.Add(field);
                continue;
            }

            if (!supplied)
            {
                if (withholdUnattributable)
                {
                    Withhold(document, field);
                    withheld.Add(field);
                }
                else
                {
                    unattributable.Add($"{field}: no readable artifact was supplied for it. Pass --artifact {field}=<path>.");
                }

                continue;
            }

            string content;
            try
            {
                content = readFile(artifact!);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                // *** THE DERIVER REFUSES WHERE THE GATE ONLY REPORTS. *** The gate, finding an artifact
                // unreadable, says it could not verify — that is honest, because the record already
                // existed. Here the record is being CREATED, and stamping a hash for a file we could not
                // open would be manufacturing the evidence.
                unattributable.Add($"{field}: the artifact '{artifact}' could not be read.");
                continue;
            }

            // ---- 1.5: the artifact must be the right KIND, and must not be a report of failure -----
            var verdict = ArtifactCheck.Inspect(DerivationProducer.KindFor(producer), content);
            if (!verdict.Accepted)
            {
                rejected.Add($"{field}: {verdict.Detail}");
                continue;
            }

            // ---- 1.1: recompute where we can, and COMPARE ------------------------------------------
            //
            // *** ONLY A COMPARISON THAT ACTUALLY RAN EARNS `Computed`. *** NotComparable is stamped
            // Attributed, not quietly promoted — labelling a field "computed" when nothing was compared
            // would be this project's signature failure committed by the tool built to prevent it.
            var comparison = Recompute.Check(field, document, content);

            if (comparison.Outcome == Recompute.Outcome.Differs)
            {
                mismatched.Add($"{field}: {comparison.Detail}");
                continue;
            }

            var verified = comparison.Outcome == Recompute.Outcome.Matched
                ? DerivationVerification.Computed
                : DerivationVerification.Attributed;

            // *** THE DENOMINATOR BEHIND THE WORD `COMPUTED`, CARRIED OUT SO THE OPERATOR SEES IT. ***
            // Only a comparison that RAN says anything: a NotComparable field's detail is a reason nothing
            // happened, and it belongs with the ATTRIBUTED line rather than being dressed as a result.
            if (comparison.Outcome == Recompute.Outcome.Matched)
                compared.Add($"{field}: {comparison.Detail}");

            var (hash, overBytes) = Hash(artifact!, content, readBytes);

            records.Add(new DerivationDocument
            {
                Field = field,
                Producer = producer,
                Artifact = artifact!,
                ArtifactSha256 = hash,
                Verified = verified,
                HashedOverBytes = overBytes,
            });

            stamped.Add(field);
        }

        document.Derivation = records;
        return new DeriveOutcome(document, stamped, withheld, unattributable, rejected, mismatched, compared);
    }

    /// <summary>
    /// Hash the artifact's BYTES when a byte reader is available, its text otherwise — <b>and report
    /// which</b>, so the weaker check is never silently substituted for the stronger one.
    /// </summary>
    private static (string Hash, bool OverBytes) Hash(string artifact, string content, Func<string, byte[]>? readBytes)
    {
        if (readBytes is not null)
        {
            try
            {
                return (DerivationHash.OfBytes(readBytes(artifact)), true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                // Fall through to the text hash rather than refusing: the artifact demonstrably READ a
                // moment ago, so this is a byte-reader problem and the weaker hash is still a real check.
                // It is recorded as the weaker one, which is the part that matters.
            }
        }

        return (DerivationHash.Of(content), false);
    }

    /// <summary>
    /// 🔴 <b>THE ONE FIELD SETTLED BY RULE RATHER THAN BY RECOMPUTATION — and the rule is stated, not
    /// buried.</b>
    ///
    /// <para><c>runtimeCompression</c> has no producing artifact anywhere: <c>TimeCompression.Plan</c>
    /// takes the declared factor as an INPUT and returns bounds, and gate 10b already grades those
    /// bounds. Re-grading them here would be a second opinion on one question. So the deriver's job is
    /// narrower and sharper: <b>a compression factor above 1 with no declared bounds behind it is an
    /// unbacked number, and unbacked numbers are what this whole mechanism exists to stop.</b></para>
    ///
    /// <para>Returns null when the rule is satisfied, or the refusal text when it is not.</para>
    /// </summary>
    private static string? RuleFor(SubmissionDocument document, string field)
    {
        if (!string.Equals(field, DerivableField.RuntimeCompression, StringComparison.Ordinal))
            return null;

        if (document.BlockCompression is not null)
            return null;

        return document.RuntimeCompression <= 1
            ? null
            : $"the submission declares runtimeCompression {document.RuntimeCompression} with no blockCompression "
              + "bounds behind it. Uncompressed needs no bounds; a factor above 1 is a claim about how far the "
              + "plant's timing was squeezed, and nothing here supports it. Declare blockCompression, or run at 1.";
    }

    /// <summary>
    /// The producer a field comes from when the caller does not override it.
    ///
    /// <para><b>Every entry is a real tool that really produces that field</b>, which is why the map is
    /// credited to the copy-layer generator and storage is not — the generator knows how a signal is
    /// watched and only the reference graph knows where it lives.</para>
    /// </summary>
    public static string DefaultProducerFor(string field) => field switch
    {
        DerivableField.Map => DerivationProducer.CopyLayerGenerator,
        DerivableField.Storage => DerivationProducer.ReachableState,
        DerivableField.ConflictEdges => DerivationProducer.SlotConflictDerivation,
        DerivableField.ComputedConflicts => DerivationProducer.SlotConflictDerivation,
        DerivableField.BlockCompression => DerivationProducer.TimeCompression,
        DerivableField.RuntimeCompression => DerivationProducer.TimeCompression,
        DerivableField.Deployment => DerivationProducer.DeviceGateway,
        DerivableField.TagMapPath => DerivationProducer.S7TagMap,
        _ => string.Empty,
    };

    /// <summary>
    /// Remove a field nothing could attribute.
    ///
    /// <para><b>Removal is the honest outcome, not vandalism:</b> an unattributable derivable field is an
    /// unsourced assertion, and leaving it in would let it be graded. What follows downstream is a gate
    /// reporting NOT CHECKED for whatever consumed it, which is the true state of affairs. It happens only
    /// under an explicit flag, and every removal is named in the report.</para>
    /// </summary>
    private static void Withhold(SubmissionDocument document, string field)
    {
        switch (field)
        {
            case DerivableField.Map: document.Map = null; break;
            case DerivableField.Storage: if (document.Map is not null) document.Map.Storage = null; break;
            case DerivableField.ConflictEdges: document.ConflictEdges = null; break;
            case DerivableField.ComputedConflicts: document.ComputedConflicts = null; break;
            case DerivableField.BlockCompression: document.BlockCompression = null; break;
            case DerivableField.Deployment: document.Deployment = null; break;
            case DerivableField.TagMapPath: document.TagMapPath = null; break;

            // 🔴 *** runtimeCompression CANNOT BE WITHHELD. *** It is an int with a default, so removing
            // it restores the default 1 — a VALUE, indistinguishable from an author typing 1, which is
            // the precise thing the key-presence flag exists to tell apart. Withholding it would not
            // withhold anything; it would forge a quieter claim.
            //
            // *** THIS BRANCH IS NOW STRUCTURALLY UNREACHABLE, AND IT STAYS ANYWAY. *** The by-rule path
            // settles this field before withholding is ever considered, so nothing today can arrive
            // here. It is kept because the hazard is silent: if a later change gave this producer an
            // artifact kind, the field would fall through to withholding and forge the claim with no
            // symptom. A guard against a silent failure is worth its unreachability.
            case DerivableField.RuntimeCompression:
                throw new InvalidOperationException(
                    "runtimeCompression cannot be withheld: it has a default, so removing the key leaves the value 1 in "
                    + "place and the submission would claim uncompressed rather than claim nothing. Attribute it or refuse.");

            default:
                throw new InvalidOperationException($"no withholding rule for derivable field '{field}'.");
        }
    }

    /// <summary>The runnable command. <c>harness-gate derive --submission &lt;in&gt; --out &lt;out&gt; [...]</c>.</summary>
    public static int Run(
        IReadOnlyList<string> args,
        TextWriter output,
        Func<string, string> readFile,
        Action<string, string> writeFile,
        Func<string, byte[]>? readBytes = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(writeFile);

        string? submissionPath = null;
        string? outPath = null;
        var withhold = false;
        var artifacts = new Dictionary<string, string>(StringComparer.Ordinal);
        var producers = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var i = 1; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--submission" when i + 1 < args.Count: submissionPath = args[++i]; break;
                case "--out" when i + 1 < args.Count: outPath = args[++i]; break;
                case "--withhold-unattributable": withhold = true; break;

                case "--artifact" when i + 1 < args.Count:
                {
                    var pair = args[++i];
                    var split = pair.IndexOf('=');
                    if (split <= 0 || split == pair.Length - 1)
                    {
                        output.WriteLine($"--artifact expects <field>=<path>, got '{pair}'.");
                        return DeriveExit.Refused;
                    }

                    var field = pair[..split];
                    if (!DerivableField.IsDerivable(field))
                    {
                        output.WriteLine(
                            $"'{field}' is not a derivable field. Gate 0c governs: {string.Join(", ", DerivableField.All)}.");
                        return DeriveExit.Refused;
                    }

                    artifacts[field] = pair[(split + 1)..];
                    break;
                }

                case "--producer" when i + 1 < args.Count:
                {
                    var pair = args[++i];
                    var split = pair.IndexOf('=');
                    if (split <= 0 || split == pair.Length - 1)
                    {
                        output.WriteLine($"--producer expects <field>=<name>, got '{pair}'.");
                        return DeriveExit.Refused;
                    }

                    producers[pair[..split]] = pair[(split + 1)..];
                    break;
                }

                default:
                    output.WriteLine($"unrecognised argument '{args[i]}'.");
                    return DeriveExit.Refused;
            }
        }

        if (submissionPath is null || outPath is null)
        {
            Usage(output);
            return DeriveExit.Refused;
        }

        SubmissionDocument document;
        try
        {
            // *** THE DOCUMENT'S OWN READER, NEVER A SECOND SET OF OPTIONS. *** This first shipped with a
            // private options object that silently lacked the enum converter, so a valid submission threw
            // on parse and the caller saw "nothing to derive" rather than an error.
            document = SubmissionDocument.Read(readFile(submissionPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        {
            output.WriteLine($"the submission could not be read: {ex.Message}");
            return DeriveExit.Refused;
        }

        var present = document.DerivableFieldsPresent();
        if (present.Count == 0)
        {
            output.WriteLine("NOTHING DERIVED - this is not a pass.");
            output.WriteLine(
                "  The submission carries no derivable field, so this run produced no provenance about anything. "
                + "That is reported rather than exited 0, because exit 0 here would read as 'the provenance is complete'.");
            return DeriveExit.NothingDerived;
        }

        DeriveOutcome outcome;
        try
        {
            outcome = Stamp(document, artifacts, producers, readFile, withhold, readBytes);
        }
        catch (InvalidOperationException ex)
        {
            output.WriteLine(ex.Message);
            return DeriveExit.Refused;
        }

        // The denominator, on every run. Every other number below is a reason something did NOT get
        // stamped; this one says how many were in scope at all.
        output.WriteLine($"EXAMINED: {present.Count} derivable field(s) present in the submission.");

        foreach (var record in document.Derivation ?? new List<DerivationDocument>())
        {
            var how = record.Verified switch
            {
                DerivationVerification.Computed => "COMPUTED  ",
                DerivationVerification.ByRule => "BY RULE   ",
                _ => "ATTRIBUTED",
            };

            var source = string.IsNullOrEmpty(record.Artifact) ? "(no artifact - settled by rule)" : record.Artifact;
            var over = string.IsNullOrEmpty(record.Artifact) ? string.Empty : record.HashedOverBytes ? " [bytes]" : " [text]";
            output.WriteLine($"  {how} {record.Field} <- {source} ({record.Producer}){over}");
        }

        // *** WHAT EACH MATCH ACTUALLY COMPARED. *** `COMPUTED deployment` alone is true of a comparison
        // against 99 loaded objects and of one against a single row, and it does not say which authority
        // the manifest came from - the probe's live DownloadResult, or its own rendering of it.
        foreach (var line in outcome.Compared)
            output.WriteLine($"  COMPARED   {line}");

        foreach (var field in outcome.Withheld)
            output.WriteLine($"  WITHHELD   {field} - nothing attributed it, and --withhold-unattributable was given.");

        foreach (var line in outcome.Mismatched)
            output.WriteLine($"  MISMATCH   {line}");

        foreach (var line in outcome.Rejected)
            output.WriteLine($"  BAD SOURCE {line}");

        foreach (var line in outcome.Unattributable)
            output.WriteLine($"  REFUSED    {line}");

        var refusals = outcome.Unattributable.Count + outcome.Rejected.Count + outcome.Mismatched.Count;
        if (refusals > 0)
        {
            // *** THE THREE ARE COUNTED TOGETHER AND REPORTED APART. *** They are one decision - nothing
            // is written - but three different mistakes: nothing supplied, the wrong thing supplied, and
            // the right thing disagreeing with what the submission claims.
            output.WriteLine(
                $"REFUSED: {refusals} field(s) - {outcome.Unattributable.Count} unattributed, "
                + $"{outcome.Rejected.Count} with an unusable artifact, {outcome.Mismatched.Count} whose recomputed value "
                + "disagreed. Nothing was written - a submission stamped with a partial provenance would pass gate 0c "
                + "for the fields it happened to cover.");
            return DeriveExit.Refused;
        }

        try
        {
            writeFile(outPath, SubmissionDocument.Write(document));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            output.WriteLine($"the derived submission could not be written: {ex.Message}");
            return DeriveExit.Refused;
        }

        if (outcome.Withheld.Count > 0)
        {
            output.WriteLine(
                $"EMITTED, {outcome.Withheld.Count} FIELD(S) WITHHELD BY NAME. The output is missing something the input "
                + "had, and the gates that consume those fields will report NOT CHECKED.");
            return DeriveExit.WithheldSomething;
        }

        var records = document.Derivation ?? new List<DerivationDocument>();
        output.WriteLine(
            $"DERIVED: {outcome.Stamped.Count} field(s) - "
            + $"{records.Count(r => r.Verified == DerivationVerification.Computed)} recomputed and matched, "
            + $"{records.Count(r => r.Verified == DerivationVerification.ByRule)} settled by rule, "
            + $"{records.Count(r => r.Verified == DerivationVerification.Attributed)} cited to an artifact but not recomputed.");
        return DeriveExit.Derived;
    }

    private static void Usage(TextWriter output)
    {
        output.WriteLine("usage: harness-gate derive --submission <in.json> --out <out.json>");
        output.WriteLine("                           [--artifact <field>=<path>]... [--producer <field>=<name>]...");
        output.WriteLine("                           [--withhold-unattributable]");
        output.WriteLine();
        output.WriteLine("Stamps provenance onto every derivable field the submission carries, so gate 0c can tell a");
        output.WriteLine("field a tool produced from one somebody typed. ATTRIBUTES rather than COMPUTES - see the class");
        output.WriteLine("summary; it does not recalculate the values.");
        output.WriteLine();
        output.WriteLine($"derivable fields: {string.Join(", ", DerivableField.All)}");
        output.WriteLine($"producers:        {string.Join(", ", DerivationProducer.Known)}");
        output.WriteLine();
        output.WriteLine($"exit {DeriveExit.Derived} = derived   {DeriveExit.Refused} = refused   "
            + $"{DeriveExit.NothingDerived} = NOTHING DERIVED   {DeriveExit.WithheldSomething} = emitted, something withheld");
    }

}
