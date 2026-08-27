using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Loop;

/// <summary>
/// How far the loop got. <b>None of these is a verdict about a block</b> — the packages carry those, and
/// the loop is deliberately unable to produce one.
/// </summary>
public enum LoopOutcome
{
    /// <summary>The map could not be derived, so the gate could not even run. Nothing was spent.</summary>
    NotDerivable,

    /// <summary>
    /// 🔴 <b>A VECTOR NAMES A SLOT NO BINDING DECLARES.</b> Nothing generated, nothing deployed.
    ///
    /// <para><b>Distinct from <see cref="NotDerivable"/>: the map derived perfectly.</b> The two
    /// documents disagree about which slots exist — and they are written by parties who deliberately do
    /// not read each other's, which is what makes them an independent pair and also what leaves a typo,
    /// a rename or an unresolved slot partition invisible until something uses both.</para>
    ///
    /// <para><b>Measured 2026-08-14, and the exception was never the defect — the POSITION was.</b>
    /// Before this existed the run threw <c>ArgumentException: no slot 'X' in this map</c> out of step 7,
    /// with the gateway recording <b>one deployment and one open transport already spent</b>. A
    /// cross-reference failure that costs a whole download is a check in the wrong place.</para>
    /// </summary>
    NotBound,

    /// <summary>
    /// 🔴 <b>THE VECTORS CANNOT BE PUT IN ONE ORDER.</b> The copy layer generated; <b>nothing was
    /// deployed and no wave was run.</b>
    ///
    /// <para><b>Distinct from <see cref="NotBound"/>: every vector's slot resolves.</b> What is missing is
    /// the SEQUENCE. Once several specification slot ids serve one mirror slot, their vectors merge into
    /// one tensor — and the submission carries no total order, because every group restarts <c>index</c>
    /// at 0. A merge made anyway gives one vector per group reading <c>Results[0]</c> and the rest
    /// carrying another vector's run, <b>reported confidently, with no error</b>.</para>
    ///
    /// <para><b>It stops the run rather than generation, because the order is a property of the WAVE and
    /// the copy layer is a pure function of the binding.</b> So <c>--generate-only</c> still produces the
    /// IR to read and prints this refusal beside it; a deploying run stops here, before the device
    /// boundary, having spent nothing but PC time.</para>
    /// </summary>
    NotOrdered,

    /// <summary>
    /// 🔴 <b>A VECTOR NEEDS A CPU RESTART TO HAPPEN IN THE MIDDLE OF IT, AND THIS LOOP RUNS ONE INLINE
    /// SEQUENCE.</b> The copy layer generated; <b>nothing was deployed and no wave was run.</b>
    ///
    /// <para><b>Distinct from <see cref="NotOrdered"/>, and the difference is the REMEDY.</b> An unstated
    /// order is fixed by stating one. This is fixed by running the group around the download boundary its
    /// own vectors declare — <i>"RIDES AN ALREADY-SCHEDULED DISRUPTIVE BOUNDARY… the boundary stops and
    /// restarts the CPU"</i>, <i>"THE HARNESS IS DISCONNECTED ACROSS THE DOWNLOAD and nobody is polling
    /// while the first scan happens"</i> — which is a deployment-sequencing act, not a loop setting.</para>
    ///
    /// <para><b>Explicitly unrunnable beats implicitly mis-run.</b> Folded into the inline sequence these
    /// vectors would execute with no restart, and every group after them would run against a freshly
    /// cleared accumulator and a reconnected harness. Every rung would run, every package would arrive,
    /// and the experiment would not be the one anybody asked for.</para>
    /// </summary>
    NotSchedulable,

    /// <summary>The submission was refused at the gate. <b>Nothing was generated and nothing was deployed.</b></summary>
    NotAdmissible,

    /// <summary>The generated harness objects failed the non-retentive assertion (0.1b). Nothing was deployed.</summary>
    NotAssertable,

    /// <summary>
    /// 🔴 <b>A VECTOR VALUE DOES NOT FIT THE MIRROR ELEMENT DECLARED TO CARRY IT.</b> Nothing was
    /// generated and nothing was deployed.
    ///
    /// <para><b>Its own outcome because the alternative is a confident wrong answer.</b> A duration that
    /// overflows a single register does not error on the controller — <c>75 000 ms</c> arrives as
    /// <c>9 464 ms</c>, every boundary keyed on it fires early, and the run comes back FAIL against a
    /// block that may be perfectly correct. Measured on the deliverable vector set: <b>81 duration values
    /// exceed 65 535 ms.</b></para>
    ///
    /// <para>Note the asymmetry this exists to correct: a swapped 32-bit WORD ORDER announces itself
    /// (75 s becomes about 7 days and the scenario times out), while a truncated WIDTH passes quietly.
    /// <b>The quiet one is the one that had to be made loud.</b></para>
    /// </summary>
    NotRepresentable,

    /// <summary>
    /// 🔴 <b>A PUBLISHED SIGNAL HAS NO DECLARED RESTING VALUE, SO THE INERT PHASE WOULD BE GATED ON AN
    /// EXPECTATION NOBODY STATED.</b> The copy layer generated; <b>nothing was deployed and no wave was
    /// run.</b>
    ///
    /// <para><b>What it replaces:</b> <c>ToWireVector</c> built the inert declaration as
    /// <c>Range(0, ResultRegistersNeeded).ToDictionary(i =&gt; i, _ =&gt; (ushort)0)</c> — every result
    /// register asserted to rest at zero, hardcoded, under a type whose own summary says the expectation
    /// <i>must be declared</i>.</para>
    ///
    /// <para><b>Why a refusal and not a default.</b> The hardcoded zero fails in both directions on real
    /// hardware. It refuses a signal resting at a <c>-1</c> sentinel — loud, and blamed on the block. And
    /// where <c>0</c> is a measured PASS verdict it ACCEPTS the previous index's leftover result as an
    /// inert start state — <b>silent, and it is the inert check passing over exactly the state it exists to
    /// catch.</b> A default whose failure modes include a false pass cannot be the default.</para>
    ///
    /// <para><b>The remedy is in the BINDING</b>, which is where every other instrumentation fact already
    /// lives: an <c>inertRest</c> per result signal — a value, or <c>excluded</c> with a reason — or, for a
    /// slot that cannot be re-declared yet, the slot-level <c>assumedZeroRest</c> claim, which says so out
    /// loud and has every register it covers reported as DEFAULTED.</para>
    /// </summary>
    RestNotDeclared,

    /// <summary>The deployment did not happen, or the device did not load what it was given.</summary>
    NotDeployed,

    /// <summary>The version register did not confirm the build this loop generated. The wave was not run.</summary>
    NotConfirmed,

    /// <summary>
    /// The wave ran. <b>Read the PACKAGES, not this</b> — this says the experiment happened, and says
    /// nothing whatever about how it came out.
    /// </summary>
    Ran,

    /// <summary>
    /// 🔴 <b>THE LANE DECLARED PART OF ITS TEST SIDE AND THE DECLARATION COULD NOT BE HONOURED.</b> Nothing
    /// was generated and nothing was deployed.
    ///
    /// <para><b>Distinct from <see cref="NotDerivable"/>, which is about the MAP.</b> Everything here
    /// derived; what failed is a declaration — a slot FC named without a block number, a head spec whose
    /// phases do not include a reset, a shell that sets a bit its own re-arm list does not clear.</para>
    ///
    /// <para><b>And distinct from declaring NOTHING, which is not a refusal at all.</b> An absent
    /// declaration means the object is authored, is reported as authored, and the lane runs exactly as it
    /// did before the field existed. Only a declaration that ASKS for something the generator will not
    /// invent lands here — <i>every uncertainty is a refusal rather than a guess</i>, which is the contract
    /// <c>CopyLayerGenerator</c> sets and both of these generators copy.</para>
    ///
    /// <para><b>It stops generation, deliberately.</b> A lane whose slot FC was refused and whose copy layer
    /// was emitted anyway is the orphan with the paperwork filed: the copy layer IS called, its start echo
    /// reports "commanded, observed to run" from both halves of itself, and the block under test never
    /// executes. That cost a wave and three hours.</para>
    /// </summary>
    NotGeneratable,
}

/// <summary>A known gap this run rests on, carried with the result rather than closed silently.</summary>
public sealed record LoopCaveat(string Id, string Detail);

/// <summary>
/// 🔴 <b>WHAT THE RUN'S INERT EXPECTATION IS MADE OF, PER SLOT AND WITH ITS DENOMINATOR.</b>
///
/// <para>The rule this exists to make visible: <b>a defaulted expectation must never be indistinguishable
/// from a declared one.</b> Before this, every result register was expected to rest at zero by a hardcoded
/// <c>ToDictionary</c> in the wave builder, and nothing anywhere said so — not the run's output, not the
/// result package, not the failure text when the check fired.</para>
///
/// <para><b>It is emitted on every run, not only the refused one.</b> A report that appears only on bad
/// news teaches its reader that its absence means it was not run.</para>
/// </summary>
public sealed record InertRestReport(IReadOnlyList<InertRestPlan> Plans)
{
    /// <summary>True only when every slot's band is covered by something somebody is answerable for.</summary>
    public bool Planned => Plans.Count > 0 && Plans.All(p => p.Planned);

    /// <summary>Every reason a slot could not be planned, slot-qualified.</summary>
    public IReadOnlyList<string> Refusals =>
        Plans.SelectMany(p => p.Refusals.Select(r => $"slot '{p.SlotId}': {r}")).ToArray();

    /// <summary>Observations that gate nothing — an escape claim covering no register, and the like.</summary>
    public IReadOnlyList<string> Notes => Plans.SelectMany(p => p.Notes).ToArray();

    /// <summary>The declaration for one slot, or a throw. <b>No caller may default it.</b></summary>
    public InertDeclaration For(string slotId) =>
        Plans.FirstOrDefault(p => string.Equals(p.SlotId, slotId, StringComparison.Ordinal))?.Require()
        ?? throw new InvalidOperationException(
            $"no inert declaration was computed for slot '{slotId}'. The loop refuses an undeclared resting value above the "
            + "device boundary, so reaching here means a wave was built from a plan that was never made.");

    /// <summary>
    /// The aggregate, <b>with the denominator first</b>: how many registers were declared, excluded,
    /// defaulted and derived, over how many slots.
    /// </summary>
    public string Summary()
    {
        if (Plans.Count == 0)
            return "INERT REST: NOTHING PLANNED — no slot was examined. Empty is not clean: this is an unexamined run, not a clean one.";

        var registers = Plans.Sum(p => p.Registers.Count);

        return $"INERT REST: {registers} result register(s) over {Plans.Count} slot(s) — "
            + $"{Plans.Sum(p => p.DeclaredCount)} DECLARED, "
            + $"{Plans.Sum(p => p.ExcludedCount)} EXCLUDED by declaration, "
            + $"{Plans.Sum(p => p.DefaultedCount)} DEFAULTED, "
            + $"{Plans.Sum(p => p.DerivedCount)} DERIVED (latch band)."

            // 🔴 *** THE INERT CHECK'S WAIT, REPORTED ON EVERY RUN INCLUDING THE CLEAN ONE. *** It was
            // permanently 1 for the whole life of the feature — `LoopRun` called `InertRestPlan.For` with
            // two arguments and the third defaulted — and NOTHING PRINTED IT, so the run that sampled a
            // model one scan (~24 ms) into a ~9-second settle looked exactly like the run that waited.
            + Quiescence()
            + (Planned ? string.Empty : $" NOT PLANNED: {Refusals.Count} refusal(s).");
    }

    /// <summary>
    /// The declared quiescence across the planned slots. <b>A range rather than a single number</b>,
    /// because it is per slot and the inert phase takes the MAX across the slots active at an index — so
    /// one slow model raises the wait for every slot sharing that phase, and a reader must be able to see
    /// that it did.
    /// </summary>
    private string Quiescence()
    {
        var declared = Plans.Where(p => p.Declaration is not null).Select(p => p.Declaration!.QuiescenceScans).ToArray();

        if (declared.Length == 0)
            return string.Empty;

        return declared.Min() == declared.Max()
            ? $" QUIESCENCE: {declared.Min()} scan(s) on every slot."
            : $" QUIESCENCE: {declared.Min()}-{declared.Max()} scan(s); the inert phase waits the MAX of the slots active at an index.";
    }
}

/// <summary>
/// 🔴 <b>What steps 1–4 produced, with the device boundary NOT crossed.</b>
///
/// <para><b>This type exists because the copy layer could not be obtained without deploying it.</b>
/// <c>LoopRun.Execute</c> was the only route to a generated layer and it goes on to hand that layer to a
/// gateway — so inspecting the artifact, diffing two generations, or measuring the mirror's width all
/// required a device fence to be satisfied for work that touches no device. <i>A component that can only
/// be asserted about has a class of defect no assertion reaches.</i></para>
///
/// <para><b><see cref="Stopped"/> is the outcome the loop WOULD have reported</b>, or null when the layer
/// was generated. It is not a bool: "the map would not derive" and "the gate refused" are different
/// facts, and collapsing them is how a caller comes to report one as the other.</para>
/// </summary>
public sealed record LoopGeneration(
    LoopOutcome? Stopped,
    SubmissionReport? Gate,
    SlotSizeReport? SizeReport,
    RegisterMap? Map,
    BuildStamp Stamp,
    CopyLayerResult? CopyLayer,
    RetentionVerdict? Retention,
    IReadOnlyList<LoopCaveat> Caveats,
    string Detail,

    /// <summary>
    /// 🔴 <b>Where every vector sits in its slot's merged run — or the reasons no such order exists.</b>
    ///
    /// <para><b>Computed here and GATED in <see cref="LoopRun.Execute"/>, which is not a warning/gate
    /// split but a scope one.</b> The order decides how vectors merge into a tensor; it decides nothing
    /// about the copy layer, which is a pure function of the binding. So generation reports it and the
    /// path that reaches a device refuses on it — and both read the SAME report, so the two cannot drift
    /// into disagreeing about whether a submission is runnable.</para>
    ///
    /// <para>Null only when the run stopped before it could be computed at all.</para>
    /// </summary>
    WaveOrderReport? Order = null,

    /// <summary>
    /// 🔴 <b>What each slot's inert expectation is made of — declared, excluded, defaulted or derived.</b>
    ///
    /// <para><b>Computed here and GATED in <see cref="LoopRun.Execute"/></b>, the same split
    /// <see cref="Order"/> gets and for the same reason: the resting values decide nothing about the copy
    /// layer, which is a pure function of the binding, so <c>--generate-only</c> prints the IR AND this
    /// report, while the path that reaches a device refuses on it.</para>
    ///
    /// <para><b>Null is NOT COMPUTED, never "nothing to say"</b> — it means the run stopped before the
    /// bindings were examined at all.</para>
    /// </summary>
    InertRestReport? InertRest = null,

    /// <summary>
    /// 🔴 <b>THE OBSERVABILITY FLOOR THIS WAVE SET IS JUDGED AGAINST, COMPUTED ONCE — <c>reads x RTT_p99 /
    /// scan</c>, where <c>reads</c> is <c>RegisterMap.ReadsPerPollCycle</c>.</b>
    ///
    /// <para><b>It is carried rather than re-derived because the two derivations disagreed.</b> The GATE
    /// computed it from the map; the delivered RESULT PACKAGE passed a hardcoded <c>1</c>. On any wave
    /// needing more than one read per poll cycle the package's floor was too small BY THAT FACTOR, in the
    /// permissive direction — so a window the gate would refuse was rendered <c>Supportable</c> in the
    /// artifact handed to the block author, and the floor printed beside it was not the floor of the wave
    /// it describes. A one-read wave set is unaffected, which is why nobody had seen it.</para>
    ///
    /// <para><b>NaN when generation stopped before the map existed</b>, and never 0 — a floor of zero
    /// would admit a one-scan event, which is unobservable at any rate.</para>
    /// </summary>
    double ObservabilityFloorScans = double.NaN,

    /// <summary>
    /// What <see cref="Stamp"/> was computed over. Carried from the derivation rather than re-derived —
    /// a second walk over the same objects is a second opinion about what was hashed, and the manifest's
    /// whole value is that it cannot disagree with the stamp beside it.
    ///
    /// <para><b>Appended at the END of this list deliberately.</b> Inserting it mid-list silently
    /// re-bound three positional arguments at a call site and the compiler caught it; a record with this
    /// many parameters is one where position is load-bearing.</para>
    /// </summary>
    ProgramManifest? Manifest = null,

    /// <summary>
    /// 🔴 <b>THE VECTORS AS THEY WILL BE WRITTEN — scenario coordinates re-expressed at the wave's
    /// factor. THE CALLER MUST BUILD ITS TENSORS FROM THESE AND NOT FROM ITS OWN REQUEST.</b>
    ///
    /// <para><b>This field exists because of a measured defect on the rig, 2026-08-22.</b>
    /// <c>Generate</c> re-expressed the coordinates by reassigning its own <c>request</c> parameter — a
    /// LOCAL — while <c>Execute</c> went on building the wave from the request IT still held. So the gate
    /// reported "18 coordinates re-expressed at comp 4" and the device received every one of them
    /// UNSCALED: the block ran its timers four times faster against a full-length scenario, index 0 hit
    /// its backstop with 5 of 5 assertions holding, and the next index could not establish inert because
    /// the plant was still mid-run.</para>
    ///
    /// <para><b>It is the exact failure this file warns about elsewhere</b> — a check that examines the
    /// right value beside a writer that writes a different one, where the check's green then reads as
    /// evidence about the writer. Returning the vectors makes the correct value the only one a caller
    /// has, rather than the one it has to remember to ask for.</para>
    ///
    /// <para>Null when generation stopped before the vectors were re-expressed.</para>
    /// </summary>
    IReadOnlyList<SubmissionVector>? Vectors = null,

    /// <summary>
    /// 🔴 <b>THE REST OF THE LANE'S TEST SIDE — the slot FC and the stimulus shell, generated rather than
    /// typed.</b>
    ///
    /// <para><c>Harness.Map.SlotFcGenerator</c> and <c>Harness.Map.StimShellGenerator</c> were reachable
    /// only from their own tests until this field existed. Both emit objects whose entire content is
    /// derived from a handful of declared names and three declared lists, and both were being hand-authored
    /// per lane anyway — which is what made a third lane cost a day.</para>
    ///
    /// <para><b>Null is NOT COMPUTED and an EMPTY result is NOTHING DECLARED; neither is "nothing to
    /// generate".</b> A lane that declares no generation keeps working exactly as it did, and
    /// <see cref="Harness.Map.LaneGenerationResult.NotDeclared"/> carries the sentence that says so per
    /// slot. The one reading that must never be available is the reverse — an authored object counted as
    /// generated.</para>
    ///
    /// <para>🔴 <b>The slot FCs on this ARE in the build stamp</b>, unlike the copy layer. The copy layer is
    /// excluded because it embeds the stamp and hashing it would be circular; a slot FC embeds nothing, and
    /// it executes on the controller — so leaving it out would let the declaration change the deployed
    /// program without moving the stamp. The shell fragments are NOT, and cannot be: they are networks, not
    /// blocks, and nothing imports them.</para>
    /// </summary>
    LaneGenerationResult? Lane = null,

    /// <summary>
    /// 🔴 <b>THE PROGRAM-LEVEL ARTIFACTS — the cyclic OB and the instance DBs, generated rather than typed.</b>
    ///
    /// <para><c>Harness.Map.InstanceDbGenerator</c> and <c>Harness.Map.CyclicObGenerator</c> close the half of
    /// a lane's hand-authored IR that belongs to the PROGRAM rather than to one slot. An instance DB is a
    /// mechanical projection of its FB's interface — the committed <c>iDB_HopperBlockageStim.ir</c> is 52
    /// typed lines its FB determines in full, and is already stale against it — <b>except for its start
    /// values, which are presets, and a preset is a claim about the plant</b>. Those are declared or the
    /// generation refuses.</para>
    ///
    /// <para>🔴 <b>And the OB is where <c>SlotFcGenerator</c>'s obligation becomes a check</b>: generated in
    /// the same pass as the slot FCs, it is refused if it does not call one of them, or calls it after the
    /// copy layer.</para>
    ///
    /// <para><b>Null is NOT COMPUTED and an EMPTY result is NOTHING DECLARED; neither is "nothing to
    /// generate".</b> <see cref="Harness.Map.ProgramGenerationResult.NotDeclared"/> carries the sentence that
    /// says which artifacts remain AUTHORED.</para>
    ///
    /// <para>🔴 <b>These objects ARE in the build stamp.</b> The OB executes and the instance DBs are loaded;
    /// neither embeds the stamp, so hashing them is not circular the way the copy layer is, and leaving them
    /// out would let a declaration change the deployed program without moving the stamp.</para>
    /// </summary>
    ProgramGenerationResult? Program = null)
{
    /// <summary>True only when a copy layer exists. Equivalent to <c>Stopped is null</c> by construction.</summary>
    public bool Generated => Stopped is null;

    /// <summary>
    /// The generated objects, or <b>an empty list that is never a clean one</b> — read
    /// <see cref="Generated"/> first, which is why <see cref="Require"/> exists beside it.
    /// </summary>
    public IReadOnlyList<HarnessObject> Objects => CopyLayer?.Objects ?? Array.Empty<HarnessObject>();

    /// <summary>The plan, or an exception naming where the loop stopped. For callers that must not read an empty list as a clean one.</summary>
    public CopyLayerPlan Require() =>
        CopyLayer?.Plan ?? throw new InvalidOperationException(
            $"no copy layer was generated ({Stopped}). {Detail}");

    /// <summary>Every reason generation stopped, whether it was refused at the gate or by the generator itself.</summary>
    public IReadOnlyList<string> Refusals => CopyLayer?.Refusals ?? Array.Empty<string>();

    internal static LoopGeneration Stop(
        LoopOutcome outcome,
        SubmissionReport? gate,
        SlotSizeReport? sizeReport,
        RetentionVerdict? retention,
        IReadOnlyList<LoopCaveat> caveats,
        string detail,
        CopyLayerResult? copyLayer = null,

        // Carried onto the stop path too: a run refused for a lane-generation reason must be able to PRINT
        // the refusal, and a run refused for an unrelated reason must still be able to say which parts of
        // the lane were declared. A stop that drops the report answers "what is still hand-built?" with
        // silence at exactly the moment somebody is reading closely.
        LaneGenerationResult? lane = null,

        // Carried onto the stop path for the same reason `lane` is: a run refused for a program-generation
        // reason must be able to PRINT the refusal, and a run refused for an unrelated reason must still be
        // able to say which of its OB and instance DBs were declared and which remain hand-authored.
        ProgramGenerationResult? program = null) =>
        new(outcome, gate, sizeReport, null, default, copyLayer, retention, caveats, detail,
            Lane: lane, Program: program);
}

/// <summary>
/// What one turn of loop 1 produced.
///
/// <para><b>There is deliberately no <c>Passed</c>, and no verdict of any kind.</b> DB-8 owns verdicts,
/// in a precedence this type honours rather than re-derives; a loop that computed its own would be a
/// second opinion on the one question the design has already answered carefully.</para>
/// </summary>
public sealed record LoopResult(
    LoopOutcome Outcome,
    SubmissionReport? Gate,
    SlotSizeReport? SizeReport,
    RetentionVerdict? Retention,
    DeploymentOutcome? Deployment,
    VersionReport? Version,
    WaveResult? Wave,
    IReadOnlyList<ResultPackage> Packages,
    IReadOnlyList<LoopCaveat> Caveats,
    string Detail,

    /// <summary>
    /// 🔴 <b>Every SUBMITTED vector and what became of it — the run's real denominator.</b>
    ///
    /// <para><b>Required, not optional, and it is the last thing that should ever acquire a default.</b>
    /// The defect it closes is that <see cref="Packages"/> was the denominator: a run that produced two
    /// packages from twenty-two submitted vectors reported <i>"0 of 2 package(s) say anything about the
    /// block"</i> and said nothing whatever about the other twenty. A default here would let a future
    /// construction site reintroduce exactly that silence.</para>
    /// </summary>
    RunAccount Account,

    /// <summary>
    /// 🔴 <b>What the inert expectation was made of — declared, excluded, defaulted or derived, with its
    /// denominator.</b>
    ///
    /// <para>On the RESULT and not only on the generation, because <b>a caveat that lives where only a
    /// reader of the design meets it has already failed the person it was written for.</b> A run whose
    /// start state was gated on assumed zeros is a run whose every verdict rests on them.</para>
    ///
    /// <para>Null means NOT COMPUTED — the run stopped before the bindings were examined.</para>
    /// </summary>
    InertRestReport? InertRest = null,

    /// <summary>
    /// 🔴 <b>WHAT THE BUILD STAMP WAS COMPUTED OVER — so this run can be re-run.</b>
    ///
    /// <para>*** MEASURED: A WAVE THAT RAN GREEN COULD NOT BE RE-RUN. *** A later attempt was refused on
    /// a stamp mismatch and nothing recorded which program set the successful run had stamped. Two
    /// candidate sets were tried, produced two different stamps, and neither was the device's. A hash
    /// cannot be inverted, so the run became unreproducible the moment its command line was gone — and
    /// the result package, the artifact meant to OUTLIVE the run, had kept the outcome and not the
    /// input.</para>
    ///
    /// <para><b>Null means the run stopped before the stamp was computed</b>, which is a real state and
    /// not an empty manifest — a run that hashed nothing says so through
    /// <see cref="ProgramManifest.HashedNothing"/> instead.</para>
    /// </summary>
    ProgramManifest? ProgramManifest = null)
{
    /// <summary>Packages that say something about the block. Only <c>Pass</c> and <c>Fail</c> do.</summary>
    public IReadOnlyList<ResultPackage> Conclusive =>
        Packages.Where(p => p.ConclusiveAboutTheBlock).ToArray();

    /// <summary>
    /// True only when the wave ran AND at least one package is conclusive about the block.
    ///
    /// <para><b>Not "no failures".</b> A run that produced six <c>Stale</c> packages has no failures and
    /// has learned nothing, and a caller reaching for <c>!Packages.Any(Fail)</c> would count that as a
    /// success — the same trap <c>ConclusiveAboutTheBlock</c> exists to close, one level up.</para>
    /// </summary>
    public bool LearnedAnything => Outcome == LoopOutcome.Ran && Conclusive.Count > 0;

    /// <summary>
    /// The packages, or an exception. For callers that must not read an empty list as a clean one.
    /// </summary>
    public IReadOnlyList<ResultPackage> RequireRan() =>
        Outcome == LoopOutcome.Ran
            ? Packages
            : throw new InvalidOperationException(
                $"the wave did not run ({Outcome}), so there are no results to read. {Detail}");

    /// <summary>
    /// What must still be done on a device before any of this is evidence about hardware.
    ///
    /// <para>🔴 <b>IT IS A REGISTER IN SOURCE, AND IT USED TO CLAIM OTHERWISE.</b> This comment read
    /// <i>"it is on the result rather than in a document because a caveat somebody has to go and look up
    /// is a caveat nobody reads"</i> — and <c>git grep OwedOnTheDevice</c> finds the declaration, four
    /// <c>.md</c> files, two XML-doc cross-references and one test asserting the strings are PRESENT.
    /// <b>No production code emits it.</b> It IS a lookup, and the sentence justifying its existence was
    /// itself the defect it warns about: prose asserting a system state that nothing could check.</para>
    ///
    /// <para><b>It is not emitted, and that is now deliberate rather than accidental.</b> Eight paragraphs
    /// of standing prose on every result package is a caveat nobody reads — this codebase's own rule — and
    /// emitting text that cannot self-correct would spread the staleness rather than fix it. What travels
    /// on the result is the subset that can be OBSERVED per run: see the <c>device-gateway</c> caveat,
    /// which names the gateway that actually deployed a given run.</para>
    ///
    /// <para>⚠️ <b>SO NOTHING HERE MAY ASSERT HISTORY.</b> Items 4 and 5 were corrected by commit
    /// <c>1b4d433</c> while items 1 and 2 — in the same array, in the same commit, under the title
    /// <i>"three stale caveats corrected"</i> — went untouched. An item that depends on somebody noticing
    /// will go stale again. Where a claim rests on what has or has not happened, it names the tracked
    /// record and DATES its reading, so a reader can re-take the measurement instead of trusting a
    /// constant.</para>
    /// </summary>
    public static IReadOnlyList<string> OwedOnTheDevice { get; } = new[]
    {
        "DEPLOYMENT ITSELF — STILL OWED, AND THIS CONSTANT IS NOT WHAT ESTABLISHES THAT. An IDeviceGateway that imports, gates and downloads EXISTS (Harness.Device.OpennessDeviceGateway): it stages the IR, runs `converter to-xml`, `openness-cli import-all`, the Standard-layout re-assertion, `compile-all`, `sanity-check` and `download-probe --disruptive`, in that order. PROVEN STRUCTURALLY, and unable to go stale: the ORDERING, the ARGUMENT VECTORS (checked against the binaries' own argument parsers, not their READMEs), the EXIT-CODE READING — and that NO TEST OF IT IS EVIDENCE THAT IT RUNS, because the design shells out to net48 binaries it cannot link and every test substitutes the process runner. A gateway that compiles is not a gateway that deploys. WHETHER IT HAS EVER RUN IS A FACT ABOUT HISTORY AND NO CONSTANT HERE CAN OBSERVE ONE: for THIS run, read the `device-gateway` caveat, which names the gateway that actually deployed it; for the record of every run, read docs/notes/test-log.tsv. *** THAT RECORD, READ 2026-08-23, CONTAINS NO ROW NAMING THIS GATEWAY: *** the 2026-08-14 45-object load is logged against `download-probe` and the Portal steps against `openness-cli`, each driven separately, which is the sequence this gateway ORCHESTRATES rather than the gateway having run.",
        "THE LOAD MANIFEST — RECOVERABLE, NOT YET RECOVERED THROUGH THIS GATEWAY. ManifestPresence is fed from the gateway's report, and the real gateway derives one by parsing `download-probe --json`'s embedded log through Ladder.Download (exercised against RECORDED logs from live rig downloads, so the parse is real evidence). THE GAP IS STRUCTURAL AND DOES NOT MOVE WITH EVENTS: the gateway is a separate PROCESS, so it cannot reach DownloadResultAdapter — the live path ProbeLogReader's own documentation names — and is confined to the probe's RENDERING of the result. Anything the renderer drops is invisible to the loop, and nothing has driven that parse end to end on a live download. *** DO NOT DISCHARGE THIS BECAUSE ITEM 1 LOOKS DISCHARGED. *** They sit next to each other and they are different questions: the 2026-08-14 manifest is recorded as first-hand from DownloadResultAdapter, which is exactly the in-process path a separate-process gateway CANNOT take — so that event is evidence for the probe and against this item, not for it.",
        "F-1's PREMISE. That MB_SERVER serves a wide multi-slot read as coherently as a narrow single-slot one is reasoned, not measured. Results read out of a group carry it as a caveat on their own stamp.",
        "THE START-BOOL BIT ORDER. STILL [I], AND DELIBERATELY SO AFTER A PARTIAL READING. On 2026-08-14 the bit was read on the device and was FALSE, which RULES OUT the byte-swapped mapping without CONFIRMING the intended one - a false reading is equally consistent with 'the mapping is wrong and the bit we landed on is also false'. The simulator and MirrorGeometry.BitAddressOf still agree FROM THE SAME PREMISE, so their agreement remains worth nothing, and replacing a weak inference with a slightly-less-weak one is not progress. The POSITIVE test is unchanged and unrun: write 16#0001 and see which bit rises.",
        "THE 32-BIT WORD ORDER - *** DISCHARGED, MEASURED TWICE. *** HighWordFirst, confirmed on 2026-08-13 against a known pattern (16#00001111) and again on 2026-08-14 by decoding the deployed build stamp at registers 0-1; both patterns have distinguishable halves, and the second was read through the ordinary production path on a value nobody chose for the experiment. The transform stays configurable and VersionCheck still reports a halves-swapped match as its own outcome - not leftover caution: MB_SERVER's presentation is Siemens' behaviour, and measuring one instance of it does not make it ours.",
        "A5 - that two slots genuinely do not interfere on a 1214C. What is shown PC-side is that the harness does not itself couple them and that a coupling which exists is caught.",
        "THE ECHO LATCH's NECESSITY. The copy layer re-drives the start condition every scan, so within an index a level coil reads identically to the SCOIL. The latch is a precaution against a start condition the copy layer does not drive, and nothing has exercised it.",
        "SCAN-ACCURATE SETTLING. The settling check here compares the recorded value against the value some scans later, which catches a value still moving; it cannot see one that moved and came back.",
    };

    /// <summary>
    /// A summary that names the OUTCOME first, so it cannot be skimmed past — <b>and counts against the
    /// SUBMITTED vectors, never against the packages that happened to be produced.</b>
    /// </summary>
    public string Summary()
    {
        var head = $"{Outcome.ToString().ToUpperInvariant()} — {Detail}";

        if (Outcome != LoopOutcome.Ran)
        {
            return head
                + $" [{Account.Submitted} vector(s) submitted, NONE attempted; {Caveats.Count} caveat(s);"
                + " nothing was learned about the block]";
        }

        var byVerdict = Packages
            .GroupBy(p => p.Verdict)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
            .Select(g => $"{g.Count()} {g.Key.ToString().ToUpperInvariant()}");

        return head
            + $" — {Account.Ran} of {Account.Submitted} submitted vector(s) ran"
            + (Account.NeverAttempted > 0 ? $", {Account.NeverAttempted} NEVER ATTEMPTED" : string.Empty)
            + $"; {Packages.Count} package(s): {string.Join(", ", byVerdict)}."
            + $" {Conclusive.Count} of {Account.Submitted} conclusive about the block. [{Caveats.Count} caveat(s)]";
    }
}
