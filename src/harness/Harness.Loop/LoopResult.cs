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

    /// <summary>The submission was refused at the gate. <b>Nothing was generated and nothing was deployed.</b></summary>
    NotAdmissible,

    /// <summary>The generated harness objects failed the non-retentive assertion (0.1b). Nothing was deployed.</summary>
    NotAssertable,

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
        "THE START-BOOL BIT ORDER. Still [I]: the simulator and MirrorGeometry.BitAddressOf agree FROM THE SAME PREMISE, so their agreement is worth nothing. One write of 16#0001 settles it.",
        "THE 32-BIT WORD ORDER for the version register and the scan counter. A configurable transform, uncalibrated; one known bit pattern settles it.",
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
