using System.Globalization;
using System.Text;
using System.Text.Json;
using Harness.Device;
using Harness.Map;

namespace Harness.Batch;

/// <summary>What the caller asked for. <c>--neighbours &lt;mode&gt;</c>, and there is no third value.</summary>
public enum NeighbourMode
{
    /// <summary>No <c>--neighbours</c> flag. The pre-Y1 state; it does not gate, and it does not print as a green.</summary>
    NotAsked,

    /// <summary>Read the corpus. Every way this can fail to arrive REFUSES.</summary>
    Derive,

    /// <summary>🔴 The one named escape, and <see cref="NeighbourEscapeLog">it is counted</see>.</summary>
    DeclaredOnly,
}

/// <summary>What state the neighbour list is in. <b>Four of the five are NOT DERIVED, and they differ.</b></summary>
public enum NeighbourState
{
    /// <summary>
    /// No derivation was supplied to the planner. The state every plan was in before workbench Y1, and
    /// it is <b>said out loud</b> rather than left to look like a check that ran and found nothing.
    /// </summary>
    NotAsked,

    /// <summary>
    /// The one named escape — <c>--neighbours declared-only</c>. Deliberate, non-gating, and
    /// <b>counted</b>: see <see cref="NeighbourEscapeLog"/>.
    /// </summary>
    DeclaredOnly,

    /// <summary>
    /// A derivation was ASKED FOR and did not arrive — the converter did not start, timed out, answered
    /// something unreadable, or answered honestly that it could not read the whole corpus. <b>This
    /// gates.</b>
    /// </summary>
    NotDerived,

    /// <summary>A defect IN THE CORPUS that the producer refused to interpret. <b>This gates too.</b></summary>
    Refused,

    /// <summary>The corpus was read. <c>Neighbours</c> is non-null — <b>and may legitimately be empty.</b></summary>
    Derived,
}

/// <summary>
/// One <c>%M</c> claim the producer found inside the area, in the producer's own vocabulary and units.
///
/// <para><b>Spans are in <c>%M</c> BYTES</b>, exactly as <c>converter neighbours</c> emits them. The
/// byte→register conversion is <see cref="MirrorGeometry.ReservingBytes"/>'s and is not repeated here:
/// it rounds OUTWARD for a reason that belongs to the mirror (a byte the mirror shares with a neighbour
/// costs the neighbour's whole register), and a second conversion is how the two would come to disagree.</para>
/// </summary>
/// <param name="Owner">
/// The declaring object, in the producer's words, carried <b>AS-IS</b> into
/// <see cref="ReservedRegion.Owner"/>. Re-wording it here would put the provenance of a refusal in two
/// places — and <c>ReservedRegion</c>'s whole argument is that a refusal which cannot say WHOSE space
/// was hit sends the reader to the mirror, the one place the problem is not.
/// </param>
/// <param name="Container">
/// The object the claim lives in — a tag table's name, or a block's. <b>This is what the mirror's own
/// objects are excluded on</b>, against the closed set from <see cref="CopyLayerNaming"/>.
/// </param>
public sealed record DerivedNeighbour(
    string Owner,
    string Kind,
    string Container,
    string Name,
    string Address,
    int StartByte,
    int ByteLength,
    int EndByteExclusive,
    string File,
    int Line);

/// <summary>
/// What the program corpus says lives in the mirror's <c>%M</c> area — or every reason it says nothing.
/// </summary>
/// <param name="Neighbours">
/// 🔴 <b>NULL UNLESS <see cref="NeighbourState.Derived"/>, mirroring the producer's own rule that a list
/// nobody derived OMITS THE KEY.</b> An empty list is a positive claim — the corpus was read and
/// declared nothing in the area — and it must never be reachable from a path that read nothing.
/// <b>Gate on the state and on this being non-null, never on the count.</b>
/// </param>
/// <param name="Why">
/// The sentence a reader acts on. On a derived fact it is the producer's own denominator, carried
/// verbatim; on every other state it is which of the several nothings this was.
/// </param>
public sealed record NeighbourFact(
    NeighbourState State,
    IReadOnlyList<DerivedNeighbour>? Neighbours,
    int TagTablesScanned,
    int BlocksScanned,
    IReadOnlyList<string> Unparseable,
    IReadOnlyList<string> Refusals,
    string Why)
{
    public bool Derived => State == NeighbourState.Derived;

    /// <summary>
    /// 🔴 <b>Whether this is a REFUSAL INPUT THAT WENT MISSING.</b>
    ///
    /// <para><c>UnionPreflight</c> (W5) falls back to a weaker check and says so, and that is right for a
    /// reachability REPORT. It is wrong here: a neighbour list is the only thing standing between a
    /// computed mirror extent and somebody else's registers, so "could not be consulted" cannot be
    /// allowed to render as "nothing found". <see cref="NeighbourState.NotAsked"/> and
    /// <see cref="NeighbourState.DeclaredOnly"/> do not gate — nobody asked for a derivation in the
    /// first one, and the second is the single named escape — but both print NOT DERIVED.</para>
    /// </summary>
    public bool Gates => State is NeighbourState.NotDerived or NeighbourState.Refused;

    public static NeighbourFact NotAsked { get; } = new(
        NeighbourState.NotAsked, null, 0, 0, Array.Empty<string>(), Array.Empty<string>(),
        "no `--neighbours derive` was asked for, so the program corpus was not read for %M occupants. Whatever "
        + "reservations appear below are DECLARED, not derived.");

    public static NeighbourFact DeclaredOnly { get; } = new(
        NeighbourState.DeclaredOnly, null, 0, 0, Array.Empty<string>(), Array.Empty<string>(),
        "the escape `--neighbours declared-only` was named, so the neighbour list is whatever the bindings "
        + "declared and NOTHING DERIVED IT. An undeclared occupant of this area is invisible to this run.");

    public static NeighbourFact NotDerivedBecause(
        string why, IReadOnlyList<string>? unparseable = null, int tagTables = 0, int blocks = 0) =>
        new(NeighbourState.NotDerived, null, tagTables, blocks,
            unparseable ?? Array.Empty<string>(), Array.Empty<string>(), why);
}

/// <summary>
/// 🔴 <b>THE NEIGHBOUR LIST, OBTAINED RATHER THAN REMEMBERED (workbench Y1).</b>
///
/// <para><b>The defect, measured live on 2026-08-23.</b> A generated mirror and a hand-authored virtual
/// panel both claimed registers 256–323 of one <c>%M</c> area and <b>53 tags collided bit for bit</b>,
/// the panel's master enable among them. <see cref="ReservedRegion"/> and
/// <see cref="MirrorGeometry.Intrusions"/> now guard that on every path — <b>but only for a neighbour
/// somebody wrote down</b>. <c>BindingDocument.cs:63-66</c> states the limit exactly: the reservation
/// field is <i>"a place to put the knowledge rather than a way to obtain it."</i> This obtains it.</para>
///
/// <para><b>A document over a subprocess, and the project reference is DELIBERATELY not taken.</b> The
/// converter owns the only IR parser this project has; <c>C2</c> permits a reference from the harness to
/// it and this item declines to spend it, because the fact needed is naturally a document with
/// provenance and the first consumer of that reference should not also be the change that turns the
/// converter from an Exe into a library. <see cref="ServedAreaProbe"/> and <see cref="UnionPreflight"/>
/// set the shape.</para>
///
/// <para>🔴 <b>WHAT THIS CANNOT SEE, and none of it is closed by any arrangement of this code:</b>
/// <b>(1)</b> an occupant that reaches <c>%M</c> WITHOUT DECLARING IT — an indirect access, a
/// runtime-computed pointer, an offset arrived at by arithmetic. The derivation is over DECLARATIONS in
/// the IR, not over execution. <b>(2)</b> anything outside the corpus it was handed. <b>(3)</b> whether
/// that corpus is the program on the controller — it reads files, never the CPU, which is the same limit
/// <see cref="ServedAreaProbe"/> carries and the reason the build stamp exists. <b>A zero here means
/// "nothing in the corpus I read DECLARED a claim", never "the area is free".</b></para>
/// </summary>
public static class NeighbourProbe
{
    /// <summary>
    /// Runs <c>converter neighbours</c> over the lanes' program paths, for the area
    /// <see cref="ServedAreaProbe"/> established.
    /// </summary>
    /// <param name="baseByte">The <c>%M</c> byte the area starts at — from <c>served-area</c>, never authored here.</param>
    /// <param name="registers">The served width — likewise.</param>
    public static NeighbourFact Derive(
        string converterExe,
        IReadOnlyList<string> programPaths,
        int baseByte,
        int registers,
        IProcessRunner runner,
        TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(programPaths);
        ArgumentNullException.ThrowIfNull(runner);

        if (programPaths.Count == 0)
        {
            return NeighbourFact.NotDerivedBecause(
                "the batch names no program paths, so there was no corpus to derive %M occupancy from. An empty "
                + "corpus is NOT an empty area.");
        }

        var arguments = new List<string> { "neighbours" };
        foreach (var path in programPaths)
        {
            arguments.Add("--project");
            arguments.Add(path);
        }

        // 🔴 THE AREA IS THE ONE `served-area` DERIVED, never the one the binding declares. Two
        // derivations describing two different areas would let the neighbour check clear a window the
        // mirror does not live in — a check that examines something real which is not the thing at risk,
        // which is the closed-check shape this whole phase is about.
        arguments.Add("--base");
        arguments.Add(baseByte.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--registers");
        arguments.Add(registers.ToString(CultureInfo.InvariantCulture));
        arguments.Add("--json");

        var result = runner.Run(converterExe, arguments, timeout ?? TimeSpan.FromMinutes(2));

        if (!result.Started)
            return NeighbourFact.NotDerivedBecause("the converter did not start: " + result.Detail);

        if (result.TimedOut)
            return NeighbourFact.NotDerivedBecause("the converter timed out, so the program corpus was not read.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException)
        {
            return NeighbourFact.NotDerivedBecause(
                "the converter's output was not readable JSON, so the program corpus was not read.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var denominator = root.TryGetProperty("denominator", out var d) ? d.GetString() : null;
            var (tagTables, blocks) = Scanned(root);

            var refusals = Strings(root, "refusals");
            if (refusals.Count > 0)
            {
                return new NeighbourFact(NeighbourState.Refused, null, tagTables, blocks,
                    Array.Empty<string>(), refusals,
                    denominator ?? "the producer REFUSED to derive the neighbour list.");
            }

            var derived = root.TryGetProperty("derived", out var flag) && flag.ValueKind == JsonValueKind.True;

            // 🔴 *** GATED ON THE KEY'S PRESENCE, NEVER ON THE LIST'S LENGTH. *** The producer omits
            // `neighbours` entirely when it did not derive one, precisely so that `[]` can mean the
            // EARNED ZERO — the corpus was read and declared nothing. A consumer that read a missing key
            // as an empty list would turn "nobody looked" into "the area is free", which is the silent
            // pass both halves of this item exist to prevent.
            var present = root.TryGetProperty("neighbours", out var array) && array.ValueKind == JsonValueKind.Array;

            if (!derived || !present)
            {
                var unparseable = Strings(root, "unparseable");
                var why = root.TryGetProperty("notDerived", out var nd) ? nd.GetString() : null;

                return NeighbourFact.NotDerivedBecause(
                    (why ?? "the producer stated no neighbour list.")
                    + (unparseable.Count > 0
                        ? " Unparseable: " + string.Join(", ", unparseable)
                          + ". An unread file can declare a claim on this area, so a SHORT list is not a clean one."
                        : string.Empty),
                    unparseable, tagTables, blocks);
            }

            var claims = new List<DerivedNeighbour>();
            foreach (var item in array.EnumerateArray())
            {
                claims.Add(new DerivedNeighbour(
                    Text(item, "owner"),
                    Text(item, "kind"),
                    Text(item, "container"),
                    Text(item, "name"),
                    Text(item, "address"),
                    Number(item, "startByte"),
                    Number(item, "byteLength"),
                    Number(item, "endByteExclusive"),
                    Text(item, "file"),
                    Number(item, "line")));
            }

            return new NeighbourFact(NeighbourState.Derived, claims, tagTables, blocks,
                Array.Empty<string>(), Array.Empty<string>(),
                denominator ?? $"{claims.Count} %M claim(s) in the area.");
        }
    }

    private static (int TagTables, int Blocks) Scanned(JsonElement root) =>
        root.TryGetProperty("scanned", out var scanned) && scanned.ValueKind == JsonValueKind.Object
            ? (Number(scanned, "tagTables"), Number(scanned, "blocks"))
            : (0, 0);

    private static IReadOnlyList<string> Strings(JsonElement root, string property) =>
        root.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array
            ? array.EnumerateArray().Select(e => e.GetString() ?? e.ToString()).ToArray()
            : Array.Empty<string>();

    private static string Text(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;

    private static int Number(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;
}

/// <summary>
/// The derived set, the declared set, and what each says about the other.
/// </summary>
/// <param name="Derived">
/// What the mirror may not enter <b>because the program says somebody else is there</b> — in HOLDING
/// REGISTERS, converted through <see cref="MirrorGeometry.ReservingBytes"/>, with the parts already
/// covered by a declared region removed. Ready to hand straight to
/// <see cref="MirrorGeometry.Reserving(ReservedRegion[])"/>.
/// </param>
/// <param name="ExcludedAsOurs">The mirror's own claims, dropped — and counted, never silently.</param>
/// <param name="Reports">
/// Declared regions the derivation did not corroborate. <b>Reports, not refusals</b>: see
/// <see cref="NeighbourReconciler"/>.
/// </param>
public sealed record NeighbourReconciliation(
    IReadOnlyList<ReservedRegion> Derived,
    IReadOnlyList<DerivedNeighbour> ExcludedAsOurs,
    int DeclaredCount,
    int CorroboratedCount,
    string Denominator,
    IReadOnlyList<string> Reports);

/// <summary>
/// 🔴 <b>THE THREE OUTCOMES, AND THEY MUST NOT COLLAPSE INTO ONE ANOTHER.</b>
///
/// <list type="bullet">
/// <item><b>A derived region nobody declared is USED AND NAMED</b> — it becomes a
/// <see cref="ReservedRegion"/> carrying the producer's owner string, and the mirror is refused out of
/// it. This is the point of the item.</item>
/// <item><b>A declared region the derivation does not corroborate is REPORTED.</b> Not refused: the
/// declaration may be stale, or it may name exactly the occupant this derivation cannot see (an indirect
/// or pointer-computed access). Refusing it would punish the only party who wrote anything down, and
/// would make declaring a neighbour a liability.</item>
/// <item><b>A conflict with the mirror is REFUSED</b> — by <see cref="MirrorGeometry.Intrusions"/>,
/// which already does exactly that once it is given something to see. No second refusal is written here.</item>
/// </list>
/// </summary>
public static class NeighbourReconciler
{
    /// <summary>
    /// Reconcile a derivation against what the bindings declared.
    /// </summary>
    /// <param name="geometry">
    /// The mirror's geometry <b>before any reservation is applied</b> — it supplies the base byte the
    /// conversion is relative to, and its <see cref="MirrorGeometry.ReservedRegions"/> must be empty or
    /// the derived spans would be read back out of the wrong end of the list.
    /// </param>
    /// <param name="naming">
    /// 🔴 The closed set the mirror's OWN objects are excluded on. The two names come from
    /// <see cref="CopyLayerNaming"/> and never from a list this file knows — <c>BuildStamp.cs:192-199</c>
    /// is the precedent and it states the reason: a renamed copy layer stays excluded and an unrelated
    /// block never is.
    /// </param>
    public static NeighbourReconciliation Of(
        NeighbourFact fact,
        MirrorGeometry geometry,
        CopyLayerNaming naming,
        IReadOnlyList<ReservedRegion> declared)
    {
        ArgumentNullException.ThrowIfNull(fact);
        ArgumentNullException.ThrowIfNull(geometry);
        ArgumentNullException.ThrowIfNull(naming);
        ArgumentNullException.ThrowIfNull(declared);

        var ours = new List<DerivedNeighbour>();
        var regions = new List<ReservedRegion>();
        var corroborated = new HashSet<int>();
        var reports = new List<string>();

        // Only well-formed declarations can absorb a derived claim. A malformed one (register -1, length
        // 0) is on its way to a geometry refusal by name, and subtracting against it would quietly narrow
        // a real derived band using a declaration nobody can read.
        var usable = declared.Where(r => r.Refusals.Count == 0).ToArray();

        if (fact.Neighbours is { } claims)
        {
            foreach (var claim in claims)
            {
                // 🔴 THE MIRROR'S OWN TAG TABLE AND COPY LAYER ARE NOT NEIGHBOURS OF THE MIRROR. Left in,
                // every map would refuse against its own tags — and on the committed reference corpus that
                // is not a corner case: all 26 %M claims inside %M1000..%M1073 belong to `HarnessMirror`.
                if (string.Equals(claim.Container, naming.TagTableName, StringComparison.Ordinal) ||
                    string.Equals(claim.Container, naming.BlockName, StringComparison.Ordinal))
                {
                    ours.Add(claim);
                    continue;
                }

                // The conversion, DERIVED and not repeated: ReservingBytes owns the outward rounding and
                // the refusal for a span below the base.
                var converted = geometry.ReservingBytes(claim.StartByte, claim.ByteLength, claim.Owner).ReservedRegions;
                regions.Add(converted[^1]);
            }
        }

        // 🔴 DERIVED CLAIMS ARE COALESCED WITH EACH OTHER, AND THAT IS NOT TIDYING. Two Bools in one %M
        // word round outward onto the SAME register, and MirrorGeometry.Refusals reads two owners on one
        // register as "one of these declarations is wrong". Between two DECLARATIONS that is right — two
        // authors disagree. Between two DERIVED FACTS it is a false refusal: the program really does put
        // both of them there. So overlapping derived spans merge, and the merged label names every owner
        // in it, because the refusal has to stay able to say whose space was hit.
        var merged = Coalesce(regions);

        // Corroboration, and then SUBTRACTION. A declared band and the derived claims inside it are the
        // same occupant seen twice; added as separate reservations they would overlap and the geometry
        // would refuse the pair. Removing the already-declared part keeps the declaration's (usually
        // wider) protection intact and adds only what nobody had written down.
        var used = new List<ReservedRegion>();
        foreach (var region in merged)
        {
            var remaining = new List<(int Start, int End)> { (region.Register, region.End) };

            for (var i = 0; i < usable.Length; i++)
            {
                var band = usable[i];
                if (!band.Overlaps(region.Range))
                    continue;

                corroborated.Add(i);
                remaining = Subtract(remaining, band.Register, band.End);
            }

            foreach (var (start, end) in remaining.Where(p => p.End > p.Start))
                used.Add(new ReservedRegion(start, end - start, region.Owner));
        }

        for (var i = 0; i < usable.Length; i++)
        {
            if (corroborated.Contains(i))
                continue;

            reports.Add(
                $"the binding declares {usable[i].Describe()} and THE DERIVATION DID NOT CORROBORATE IT — nothing in the "
                + "corpus declares a %M tag or an area pointer inside those registers. Reported, not refused: the "
                + "declaration may be stale, or it may name exactly the occupant this derivation cannot see (an "
                + "indirect or pointer-computed access). The reservation still binds.");
        }

        // Malformed declarations are not corroborable and are not silently absent from the count either:
        // they are on their way to a geometry refusal by name, and saying nothing here would leave the
        // declared total disagreeing with the binding.
        var declaredCount = declared.Count;

        return new NeighbourReconciliation(
            used, ours, declaredCount, corroborated.Count,
            Denominator(fact, used.Count, ours, declaredCount, corroborated.Count),
            reports);
    }

    /// <summary>
    /// 🔴 <b>THE DENOMINATOR, RENDERED ON EVERY RUN INCLUDING THE ZERO.</b> A green that does not say
    /// what it examined is unreadable as evidence, and a zero neighbour count is the run whose reader is
    /// likeliest to widen it into "the area is free".
    /// </summary>
    private static string Denominator(
        NeighbourFact fact, int derived, IReadOnlyList<DerivedNeighbour> ours, int declared, int corroborated)
    {
        static string N(int value) => value.ToString(CultureInfo.InvariantCulture);

        var sb = new StringBuilder();

        if (fact.Derived)
        {
            sb.Append("neighbours: ").Append(N(derived)).Append(" region(s) derived from ")
              .Append(N(fact.TagTablesScanned)).Append(" tag table(s) + ")
              .Append(N(fact.BlocksScanned)).Append(" block(s); ")
              .Append(N(fact.Unparseable.Count)).Append(" file(s) unparseable; ")
              .Append(N(declared)).Append(" declared, ").Append(N(corroborated)).Append(" corroborated.");
        }
        else
        {
            sb.Append("NEIGHBOURS: NOT DERIVED — ").Append(fact.Why)
              .Append(" [").Append(N(declared))
              .Append(" declared, NONE corroborated — nothing was derived to corroborate them.]");
        }

        // 🔴 NAMED, NOT JUST COUNTED. On the committed reference corpus this line is the difference
        // between "the derivation found nothing" and "the derivation found 26 claims and every one of
        // them was ours" — and the second is the only one a reader can check.
        if (ours.Count > 0)
        {
            sb.Append('\n').Append("            ").Append(N(ours.Count))
              .Append(" %M claim(s) in the area were EXCLUDED as the mirror's OWN objects (")
              .Append(string.Join(", ", ours.Select(o => o.Container).Distinct(StringComparer.Ordinal).OrderBy(c => c, StringComparer.Ordinal)))
              .Append(") — the closed set from CopyLayerNaming, its block and its tag table, never a hardcoded list.");
        }

        return sb.ToString();
    }

    /// <summary>Overlapping or touching regions merged, owners joined. See the call site for why this exists.</summary>
    private static IReadOnlyList<ReservedRegion> Coalesce(IReadOnlyList<ReservedRegion> regions)
    {
        var ordered = regions.Where(r => r.Length > 0).OrderBy(r => r.Register).ThenBy(r => r.Length).ToList();

        // A malformed derived region cannot happen through ReservingBytes for a claim inside the area —
        // but a claim BELOW the base derives a negative register, and that must reach the geometry's own
        // refusal rather than being dropped here.
        var malformed = regions.Where(r => r.Length <= 0 || r.Register < 0).ToList();

        var merged = new List<ReservedRegion>();
        foreach (var region in ordered)
        {
            if (merged.Count > 0 && merged[^1].End >= region.Register)
            {
                var last = merged[^1];
                var end = Math.Max(last.End, region.End);
                var owner = last.Owner.Contains(region.Owner, StringComparison.Ordinal)
                    ? last.Owner
                    : last.Owner + " + " + region.Owner;

                merged[^1] = new ReservedRegion(last.Register, end - last.Register, owner);
                continue;
            }

            merged.Add(region);
        }

        merged.AddRange(malformed);
        return merged;
    }

    /// <summary>What is left of a set of half-open spans once <c>[start, end)</c> is taken out of them.</summary>
    private static List<(int Start, int End)> Subtract(List<(int Start, int End)> spans, int start, int end)
    {
        var result = new List<(int Start, int End)>();

        foreach (var (s, e) in spans)
        {
            if (e <= start || s >= end)
            {
                result.Add((s, e));
                continue;
            }

            if (s < start)
                result.Add((s, start));

            if (e > end)
                result.Add((end, e));
        }

        return result;
    }
}

/// <summary>
/// 🔴 <b>THE ESCAPE IS COUNTED, BECAUSE AN ESCAPE THAT BECOMES ROUTINE IS THE GUARD SWITCHED OFF WITH
/// NOBODY NOTICING.</b>
///
/// <para>A single run that declines to derive is a judgement call. A queue whose last forty plans all
/// declined is the pre-Y1 state with a flag on it, and nothing in a per-run report would ever say so —
/// each individual line reads as a reasonable one-off. So the tally is DURABLE and lives beside the
/// queue: append-only, one line per use, printed as a running count wherever the escape is exercised.</para>
///
/// <para><b>Only a deliberate decline is counted.</b> A plan that never asked for a derivation does not
/// move this number: inflating it with every ordinary plan would make the figure that is supposed to
/// raise an eyebrow unreadable. That plan is not silent either — it prints the same NOT DERIVED line.</para>
/// </summary>
public static class NeighbourEscapeLog
{
    private const string FileName = "neighbours-declared-only.log";

    /// <summary>One recorded use, and the sentence to print for it.</summary>
    public sealed record Use(int Uses, string Line);

    /// <summary>Record a use against <paramref name="queueDirectory"/> and return the running total.</summary>
    public static Use Record(string queueDirectory, string verb)
    {
        ArgumentNullException.ThrowIfNull(queueDirectory);

        var uses = Count(queueDirectory) + 1;
        var stamp = DateTimeOffset.UtcNow.ToString("u", CultureInfo.InvariantCulture);

        try
        {
            Directory.CreateDirectory(queueDirectory);
            File.AppendAllText(Path.Combine(queueDirectory, FileName), $"{stamp}\t{verb}\n");
        }
        catch (IOException e)
        {
            // The tally failing to persist must not stop the run — but it must not silently report a
            // count it could not read or write either. Said, in the line the operator sees.
            return new Use(uses, Line(uses, stamp) + $" (⚠️ THIS USE WAS NOT RECORDED: {e.Message} — the running count above is unreliable.)");
        }

        return new Use(uses, Line(uses, stamp));
    }

    /// <summary>How many times the escape has been taken against this queue. Zero when nothing was ever recorded.</summary>
    public static int Count(string queueDirectory)
    {
        ArgumentNullException.ThrowIfNull(queueDirectory);

        var path = Path.Combine(queueDirectory, FileName);

        try
        {
            return File.Exists(path)
                ? File.ReadAllLines(path).Count(l => !string.IsNullOrWhiteSpace(l))
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    private static string Line(int uses, string stamp) =>
        $"ESCAPE `--neighbours declared-only` TAKEN — use #{uses} against this queue ({stamp}). "
        + "If this becomes routine the guard is back to declared-only and nobody will notice.";
}
