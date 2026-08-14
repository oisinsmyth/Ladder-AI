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

    /// <summary>The deployment did not happen, or the device did not load what it was given.</summary>
    NotDeployed,

    /// <summary>The version register did not confirm the build this loop generated. The wave was not run.</summary>
    NotConfirmed,

    /// <summary>
    /// The wave ran. <b>Read the PACKAGES, not this</b> — this says the experiment happened, and says
    /// nothing whatever about how it came out.
    /// </summary>
    Ran,
}

/// <summary>A known gap this run rests on, carried with the result rather than closed silently.</summary>
public sealed record LoopCaveat(string Id, string Detail);

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
    WaveOrderReport? Order = null)
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
        CopyLayerResult? copyLayer = null) =>
        new(outcome, gate, sizeReport, null, default, copyLayer, retention, caveats, detail);
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
    string Detail)
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
    /// <para><b>A deliverable, not an apology.</b> It is on the result rather than in a document because
    /// a caveat somebody has to go and look up is a caveat nobody reads — the same reasoning as DB-8's
    /// validity stamp.</para>
    /// </summary>
    public static IReadOnlyList<string> OwedOnTheDevice { get; } = new[]
    {
        "DEPLOYMENT ITSELF — STILL OWED, AND THE REASON CHANGED. An IDeviceGateway that imports, gates and downloads NOW EXISTS (Harness.Device.OpennessDeviceGateway): it stages the IR, runs `converter to-xml`, `openness-cli import-all`, the Standard-layout re-assertion, `compile-all`, `sanity-check` and `download-probe --disruptive`, in that order. What is proven is the ORDERING, the ARGUMENT VECTORS (checked against the binaries' own argument parsers, not their READMEs) and the EXIT-CODE READING. What is NOT proven is that any of it runs: NO STEP OF IT HAS EVER BEEN EXECUTED against Portal or a controller, and every test of it substitutes the process runner. A gateway that compiles is not a gateway that deploys.",
        "THE LOAD MANIFEST — RECOVERABLE, NOT YET RECOVERED LIVE. ManifestPresence is fed from the gateway's report, and the real gateway now derives one by parsing `download-probe --json`'s embedded log through Ladder.Download (exercised against RECORDED logs from live rig downloads, so the parse is real evidence). Two gaps remain: nothing has yet driven that path end to end on a live download, and the gateway is a separate PROCESS, so it cannot reach DownloadResultAdapter — the live path ProbeLogReader's own documentation names — and is confined to the probe's RENDERING of the result. Anything the renderer drops is invisible to the loop.",
        "F-1's PREMISE. That MB_SERVER serves a wide multi-slot read as coherently as a narrow single-slot one is reasoned, not measured. Results read out of a group carry it as a caveat on their own stamp.",
        "THE START-BOOL BIT ORDER. STILL [I], AND DELIBERATELY SO AFTER A PARTIAL READING. On 2026-08-14 the bit was read on the device and was FALSE, which RULES OUT the byte-swapped mapping without CONFIRMING the intended one - a false reading is equally consistent with 'the mapping is wrong and the bit we landed on is also false'. The simulator and MirrorGeometry.BitAddressOf still agree FROM THE SAME PREMISE, so their agreement remains worth nothing, and replacing a weak inference with a slightly-less-weak one is not progress. The POSITIVE test is unchanged and unrun: write 16#0001 and see which bit rises.",
        "THE 32-BIT WORD ORDER - *** DISCHARGED, MEASURED TWICE. *** HighWordFirst, confirmed on 2026-08-13 against a known pattern (16#00001111) and again on 2026-08-14 by decoding the deployed build stamp at registers 0-1; both patterns have distinguishable halves, and the second was read through the ordinary production path on a value nobody chose for the experiment. The transform stays configurable and VersionCheck still reports a halves-swapped match as its own outcome - not leftover caution: MB_SERVER's presentation is Siemens' behaviour, and measuring one instance of it does not make it ours.",
        "A5 - that two slots genuinely do not interfere on a 1214C. What is shown PC-side is that the harness does not itself couple them and that a coupling which exists is caught.",
        "THE ECHO LATCH's NECESSITY. The copy layer re-drives the start condition every scan, so within an index a level coil reads identically to the SCOIL. The latch is a precaution against a start condition the copy layer does not drive, and nothing has exercised it.",
        "SCAN-ACCURATE SETTLING. The settling check here compares the recorded value against the value some scans later, which catches a value still moving; it cannot see one that moved and came back.",
    };

    /// <summary>A summary that names the OUTCOME first, so it cannot be skimmed past.</summary>
    public string Summary()
    {
        var head = $"{Outcome.ToString().ToUpperInvariant()} — {Detail}";

        if (Outcome != LoopOutcome.Ran)
            return head + $" [{Caveats.Count} caveat(s); nothing was learned about the block]";

        var byVerdict = Packages
            .GroupBy(p => p.Verdict)
            .OrderBy(g => g.Key.ToString(), StringComparer.Ordinal)
            .Select(g => $"{g.Count()} {g.Key.ToString().ToUpperInvariant()}");

        return head
            + $" — {Packages.Count} package(s): {string.Join(", ", byVerdict)}."
            + $" {Conclusive.Count} conclusive about the block. [{Caveats.Count} caveat(s)]";
    }
}
