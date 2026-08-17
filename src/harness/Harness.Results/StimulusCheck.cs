using Harness.Map;
using Harness.Wire;

namespace Harness.Results;

/// <summary>Whether the object under test appeared in the download's own load manifest (§9a).</summary>
public enum ManifestPresence
{
    /// <summary>TIA reported it loaded. Cheap, and it separates "wrong" from "never loaded".</summary>
    Loaded,

    /// <summary>The manifest was read and the object is not in it. The block under test is not on the device.</summary>
    Absent,

    /// <summary>
    /// No manifest was available. <b>Deliberately not the same as Loaded</b> — §9c forbids inferring
    /// transfer from absence, and this is that rule as a value rather than as a silent default.
    /// </summary>
    NotAvailable,
}

/// <summary>Why liveness was or was not established. Each calls for a different action.</summary>
public enum StimulusOutcome
{
    /// <summary>The input arrived, the block ran, and the counter advanced by at least what was expected.</summary>
    Confirmed,

    /// <summary>
    /// The scan counter did not move at all. The CPU is stopped, or the mirror is frozen — and a frozen
    /// mirror is perfectly self-consistent, so nothing about the content distinguishes this from a pass.
    /// </summary>
    CounterFrozen,

    /// <summary>
    /// The counter moved, but by less than the run's own work implies. <b>"Moved" is not "advanced by the
    /// expected amount"</b>, and this is the state that exists to keep those apart.
    /// </summary>
    CounterAdvancedTooLittle,

    /// <summary>The wave never commanded this slot at this index — D26a's null. Nothing about the block was tested.</summary>
    NeverRan,

    /// <summary>
    /// The slot was commanded and the echo says its block never saw its start condition (X-E). The test
    /// did not happen; reading it as a failing test would send an agent editing correct logic.
    /// </summary>
    CommandedButDidNotRun,

    /// <summary>The object under test was not in the download manifest. This separates "wrong" from "never loaded".</summary>
    NotLoaded,

    /// <summary>The running program is not the build this result was derived against, or its version never settled.</summary>
    WrongBuildRunning,

    /// <summary>
    /// No liveness evidence was supplied at all. <b>Not a pass</b>: it is the state that reads exactly
    /// like a healthy run in every other field, which is why it is a value and not an absent one.
    /// </summary>
    NotChecked,
}

/// <summary>
/// How much the scan counter must have advanced for the run to be believable.
///
/// <para><b>Required, with no default.</b> D36's discipline: a deferred measurement must never acquire
/// a default that could be mistaken for one. "The counter moved" is satisfied by a counter that ticked
/// once during a run that issued forty round trips, which is exactly the case this exists to catch.</para>
/// </summary>
public sealed record StimulusExpectation(int MinimumScanAdvance)
{
    /// <summary>
    /// The conservative floor: <b>at least one scan per round trip the client issued.</b>
    ///
    /// <para>Physically this is very weak — §12a derivation 1 puts a round trip at 3.3 scans typical and
    /// 8.6 at the p99 — and it is chosen deliberately, because the stronger form divides by the scan
    /// PERIOD, which is a property of the program under test rather than of the link and is therefore
    /// not this check's to assume. What matters is that the floor SCALES WITH THE WORK DONE: a counter
    /// that advanced by fewer scans than the client made round trips is not free-running, whatever the
    /// scan period is.</para>
    /// </summary>
    public static StimulusExpectation AtLeastOneScanPerRoundTrip(int roundTrips)
    {
        if (roundTrips < 1)
            throw new ArgumentOutOfRangeException(nameof(roundTrips), roundTrips, "a run that issued no round trips observed nothing.");

        return new StimulusExpectation(roundTrips);
    }
}

/// <summary>Everything the liveness check is allowed to look at. Content is deliberately not among it.</summary>
/// <param name="Commanded">Whether the wave raised this slot's start bool at this index.</param>
/// <param name="Executed">Whether the echo says the block's own start condition was seen high (X-E).</param>
/// <param name="ScanAdvance">Scans the counter advanced between T=0 and the observation.</param>
/// <param name="RoundTrips">Round trips the run issued. The expectation is derived from this, not from the data.</param>
/// <param name="Manifest">Whether the object under test was in the download manifest (§9a).</param>
/// <param name="Version">The post-download version check, or null if none was performed.</param>
public sealed record StimulusEvidence(
    bool Commanded,
    bool Executed,
    long ScanAdvance,
    int RoundTrips,
    ManifestPresence Manifest,
    VersionReport? Version);

/// <summary>The liveness verdict, with the reason and what it does NOT mean.</summary>
/// <param name="Manifest">
/// 🔴 <b>§9a's manifest presence, RETAINED rather than consumed — and it is one of DB-8's seven named
/// package contents.</b>
///
/// <para>It used to arrive on <see cref="StimulusEvidence"/>, decide one branch of
/// <see cref="StimulusCheck.Check"/>, and then be dropped: nothing downstream of this class could say
/// whether the object under test was in the download's own manifest, so the first result package that
/// ever existed could not carry it. <b>A value a check consumes and does not publish is a value the
/// package cannot report</b>, however carefully the check reasons about it.</para>
///
/// <para><b>Null is NOT <see cref="ManifestPresence.NotAvailable"/>.</b> <c>NotAvailable</c> is
/// "a manifest was consulted and it named nothing downloadable"; null is "no liveness evidence was
/// supplied at all, so the question was never asked". Collapsing them would let a run that measured
/// nothing render identically to one that looked and found nothing — the absent-versus-empty
/// distinction this repository keeps re-earning. Required rather than defaulted for the same reason:
/// an optional parameter is an invitation to omit it.</para>
/// </param>
public sealed record StimulusReport(StimulusOutcome Outcome, string Detail, ManifestPresence? Manifest)
{
    public bool Confirmed => Outcome == StimulusOutcome.Confirmed;

    /// <summary>§9a's answer, or the fact that it was never asked. Never a silent <c>NotAvailable</c>.</summary>
    public string ManifestDetail => Manifest switch
    {
        ManifestPresence.Loaded => "the object(s) under test appear in the download's own load manifest.",
        ManifestPresence.Absent => "the manifest was read and the object(s) under test are NOT in it — this result is about whatever WAS on the device.",
        ManifestPresence.NotAvailable => "no load manifest was available, so transfer is NOT positively evidenced (section 9c forbids inferring it from the absence of a failure).",
        _ => "NOT ASKED — no liveness evidence was supplied to this result at all, so nothing here looked for a manifest. This is not the same fact as a manifest that was consulted and answered nothing.",
    };
}

/// <summary>
/// DB-8's stimulus check — <b>evidence that the input actually arrived and the block actually ran.</b>
///
/// <para><b>This is the element with teeth, and here is the reason.</b>
/// <i>"Every register agrees" is also what a mirror no write ever reached looks like.</i> A frozen
/// mirror is PERFECTLY SELF-CONSISTENT: it passes every coherence check, every comparison between
/// registers, every "did the values agree" test — because nothing ever changed them. Phase 1 walked
/// into exactly this from the other end and the spike client was built to refuse it.</para>
///
/// <para><b>So liveness is established INDEPENDENTLY OF CONTENT.</b> Nothing in this class looks at a
/// result value. It looks at whether the slot was commanded, whether its block was observed to run,
/// whether the counter advanced BY THE EXPECTED AMOUNT rather than merely moved, whether the object was
/// in the download manifest, and whether the build running is the one this result was derived against.</para>
///
/// <para><b>Without it "no output" is ambiguous between "the logic is wrong" and "the test never
/// started", and an agent asked to distinguish those from a bare fail will guess.</b></para>
/// </summary>
public static class StimulusCheck
{
    /// <summary>Establish liveness, or say precisely which kind of nothing happened.</summary>
    public static StimulusReport Check(StimulusEvidence? evidence, StimulusExpectation? expectation, BuildStamp expectedBuild)
    {
        if (evidence is null || expectation is null)
        {
            // Manifest is NULL rather than NotAvailable: nobody looked. NotAvailable would claim a
            // manifest was consulted and answered nothing, which is a stronger statement than this
            // branch can make.
            return new StimulusReport(StimulusOutcome.NotChecked,
                "no liveness evidence was supplied. An absence of evidence reads exactly like a healthy run in every other field, so it is reported as its own outcome and can never be a pass.",
                Manifest: null);
        }

        // Ordered from "the experiment did not exist" outward, because each earlier state makes the
        // later ones unaskable rather than merely also-true.
        if (!evidence.Commanded)
        {
            return new StimulusReport(StimulusOutcome.NeverRan,
                "this slot's start bool was never raised at this index — D26a's null. Nothing about the block was tested, and this is not a failing test.",
                evidence.Manifest);
        }

        if (!evidence.Executed)
        {
            return new StimulusReport(StimulusOutcome.CommandedButDidNotRun,
                "the slot was commanded and the echo says its block never saw its start condition (X-E). The test did not happen. Reading this as a failure would send an agent editing correct logic.",
                evidence.Manifest);
        }

        if (evidence.Manifest == ManifestPresence.Absent)
        {
            return new StimulusReport(StimulusOutcome.NotLoaded,
                "the object under test does not appear in the download's load manifest. It is not on the device, so this result is about whatever WAS on the device.",
                evidence.Manifest);
        }

        if (evidence.Version is { } version)
        {
            if (!version.Confirmed)
            {
                return new StimulusReport(StimulusOutcome.WrongBuildRunning,
                    $"the version register did not confirm the build this result was derived against ({version.Outcome}): {version.Detail}",
                    evidence.Manifest);
            }

            // A version report CONFIRMING a different build than this result expects is the one shape a
            // confirmed report can still be wrong in: it says "the program is what I asked about", and
            // this asks whether it asked about the right thing.
            if (version.Expected != expectedBuild.Value)
            {
                return new StimulusReport(StimulusOutcome.WrongBuildRunning,
                    $"the version check confirmed build 16#{version.Expected:X8}, but this result was derived against 16#{expectedBuild.Value:X8}. A confirmation of the wrong question is not a confirmation.",
                    evidence.Manifest);
            }
        }

        if (evidence.ScanAdvance <= 0)
        {
            return new StimulusReport(StimulusOutcome.CounterFrozen,
                $"the scan counter did not advance across a run that issued {evidence.RoundTrips} round trip(s). A frozen mirror is perfectly self-consistent, so nothing in the content would have said so.",
                evidence.Manifest);
        }

        if (evidence.ScanAdvance < expectation.MinimumScanAdvance)
        {
            return new StimulusReport(StimulusOutcome.CounterAdvancedTooLittle,
                $"the scan counter advanced {evidence.ScanAdvance} scan(s) across {evidence.RoundTrips} round trip(s), where at least {expectation.MinimumScanAdvance} was expected. MOVED is not ADVANCED BY THE EXPECTED AMOUNT, and a counter that stutters is a counter that is not tracking scans.",
                evidence.Manifest);
        }

        var manifestNote = evidence.Manifest == ManifestPresence.NotAvailable
            ? " No load manifest was available, so transfer is not positively evidenced here — see the validity stamp."
            : " The object appears in the download's load manifest.";

        return new StimulusReport(StimulusOutcome.Confirmed,
            $"commanded, observed to run, and the scan counter advanced {evidence.ScanAdvance} scan(s) across {evidence.RoundTrips} round trip(s) against a floor of {expectation.MinimumScanAdvance}." + manifestNote,
            evidence.Manifest);
    }
}
