using System;
using System.IO;
using DownloadProbe;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// EXPERIMENT 1.7 — the delegate throw. <b>The capability, not the act.</b>
///
/// The working agreement puts the real delegate throw on the STOP list: it needs the owner present,
/// because a POST-delegate throw can leave the CPU stopped. Building the mechanism is not on that
/// list; firing it at a device is. So everything here exercises the injection with no Portal, no
/// device and no download — which is all that can be asserted, and is therefore all the assurance
/// this feature has.
///
/// What these tests are actually protecting, in order of how badly it would go wrong:
/// <list type="number">
/// <item><b>It must be impossible to enable by accident.</b> This flag exists to break a download.</item>
/// <item><b>The two phases must never conflate.</b> PRE fires before anything transfers; POST fires
/// after, and <c>StartModules</c> is raised in the POST delegate — so a throw there can strand a
/// stopped CPU. One flag firing from whichever delegate came first would give an unattributable
/// result with two very different blast radii behind it.</item>
/// <item><b>An injected failure must never read as a real one.</b> Its own exception type, its own
/// exit code, and the word "deliberate" in the log.</item>
/// </list>
/// </summary>
public class ThrowInjectionTests
{
    private const string ScratchProject = @"C:\work\Widget Line scratch.ap20";

    private static ProbeParseResult Parse(params string[] extra)
    {
        var args = new[] { ScratchProject, "--options", "Software" };
        var all = new string[args.Length + extra.Length];
        args.CopyTo(all, 0);
        extra.CopyTo(all, args.Length);
        return ProbeArgumentParser.Parse(all, null, Path.GetTempPath());
    }

    // ---- it cannot be enabled by accident --------------------------------------------------

    [Fact]
    public void NoFlag_ArmsNothing()
    {
        Assert.Null(Assert.IsType<ProbeParseResult.Success>(Parse()).Arguments.InjectThrow);
    }

    /// <summary>
    /// Every near miss is a USAGE ERROR, never a silent enable and never a silent ignore. `--throw`
    /// is on this list deliberately: it is the flag someone would guess, and it is exactly the
    /// single-flag form that would conflate the two experiments.
    /// </summary>
    [Theory]
    [InlineData("--throw")]
    [InlineData("--throw-from-pre-delegate=true")]
    [InlineData("--Throw-From-Pre-Delegate")]
    [InlineData("--throw-pre")]
    [InlineData("--inject-throw")]
    public void NearMissSpellings_AreUsageErrors(string flag)
    {
        Assert.IsType<ProbeParseResult.Failure>(Parse(flag));
    }

    [Fact]
    public void PreFlag_ArmsThePreDelegateOnly()
    {
        var args = Assert.IsType<ProbeParseResult.Success>(Parse("--throw-from-pre-delegate")).Arguments;
        Assert.Equal(DelegatePhase.Pre, args.InjectThrow);
    }

    [Fact]
    public void PostFlag_ArmsThePostDelegateOnly()
    {
        var args = Assert.IsType<ProbeParseResult.Success>(Parse("--throw-from-post-delegate")).Arguments;
        Assert.Equal(DelegatePhase.Post, args.InjectThrow);
    }

    // ---- the two phases never conflate -----------------------------------------------------

    [Fact]
    public void BothFlagsTogether_AreRefused_BecauseOnlyTheFirstCouldEverBeObserved()
    {
        var failure = Assert.IsType<ProbeParseResult.Failure>(
            Parse("--throw-from-pre-delegate", "--throw-from-post-delegate"));

        Assert.Contains("TWO DIFFERENT EXPERIMENTS", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// MEASURED, 2026-08-13: the folder overload is
    /// <c>Download(DirectoryInfo, DownloadConfigurationDelegate)</c> — <b>one</b> delegate, the pre
    /// one. Arming POST there would arm something that can never fire, and the run would exit having
    /// tested nothing while looking like it had. Refused by name rather than accepted as a no-op.
    /// </summary>
    [Fact]
    public void PostThrow_WithToFolder_IsRefused_BecauseThatOverloadHasNoPostDelegate()
    {
        var failure = Assert.IsType<ProbeParseResult.Failure>(
            Parse("--to-folder", @"C:\out", "--throw-from-post-delegate"));

        Assert.Contains("no post delegate", failure.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The rehearsal route, and it must stay open: the PRE delegate IS reached on the folder path
    /// (measured — it raises `ConsistentBlocksDownload` there), so the whole PRE throw can be
    /// exercised with nothing on the wire before a device is ever involved.
    /// </summary>
    [Fact]
    public void PreThrow_WithToFolder_IsAllowed_BecauseThatIsTheRehearsal()
    {
        var args = Assert.IsType<ProbeParseResult.Success>(
            Parse("--to-folder", @"C:\out", "--throw-from-pre-delegate")).Arguments;

        Assert.Equal(DelegatePhase.Pre, args.InjectThrow);
        Assert.Equal(@"C:\out", args.ToFolder);
    }

    // ---- the injection itself --------------------------------------------------------------

    [Fact]
    public void AnUnarmedInjection_NeverThrows()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);

        ThrowInjection.None.MaybeThrow(DelegatePhase.Pre, "ConsistentBlocksDownload", log);
        ThrowInjection.None.MaybeThrow(DelegatePhase.Post, "StartModules", log);

        Assert.Null(ThrowInjection.None.Fired);
    }

    [Fact]
    public void AnArmedInjection_DoesNotFireFromTheOtherDelegate()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var injection = new ThrowInjection(DelegatePhase.Post);

        injection.MaybeThrow(DelegatePhase.Pre, "ConsistentBlocksDownload", log);

        Assert.Null(injection.Fired);
    }

    [Fact]
    public void AnArmedInjection_FiresOnTheFirstInvocationOfItsOwnDelegate()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var injection = new ThrowInjection(DelegatePhase.Pre);

        var ex = Assert.Throws<DeliberateProbeInjectionException>(
            () => injection.MaybeThrow(DelegatePhase.Pre, "ConsistentBlocksDownload", log));

        Assert.Equal(DelegatePhase.Pre, ex.Phase);
        Assert.Equal(1, ex.Ordinal);
        Assert.Equal("ConsistentBlocksDownload", ex.ConfigurationType);
        Assert.Same(ex, injection.Fired);
    }

    // One throw per run. Openness may call the delegate again after catching; a second throw would
    // overwrite the record of the first and make the outcome unattributable to an invocation.
    [Fact]
    public void AnArmedInjection_FiresOnlyOnce()
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var injection = new ThrowInjection(DelegatePhase.Pre);

        var first = Assert.Throws<DeliberateProbeInjectionException>(
            () => injection.MaybeThrow(DelegatePhase.Pre, "ConsistentBlocksDownload", log));

        injection.MaybeThrow(DelegatePhase.Pre, "AlarmTextLibrariesDownload", log);

        Assert.Same(first, injection.Fired);
    }

    /// <summary>The message has to identify itself as deliberate before anything else it says.</summary>
    [Fact]
    public void TheInjectedException_SaysItIsDeliberate_AndNotADefect()
    {
        var ex = new DeliberateProbeInjectionException(DelegatePhase.Post, 2, "StartModules");

        Assert.Contains("DELIBERATE TEST INJECTION", ex.Message, StringComparison.Ordinal);
        Assert.Contains("not a defect", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ---- classification, by object identity ------------------------------------------------

    private static ThrowInjection Armed(out DeliberateProbeInjectionException fired)
    {
        using var log = new ProbeLog(new StringWriter(), filePath: null);
        var injection = new ThrowInjection(DelegatePhase.Pre);
        fired = Assert.Throws<DeliberateProbeInjectionException>(
            () => injection.MaybeThrow(DelegatePhase.Pre, "ConsistentBlocksDownload", log));
        return injection;
    }

    [Fact]
    public void ArmedButNeverInvoked_IsNeverFired_NotAPass()
    {
        var (outcome, _) = new ThrowInjection(DelegatePhase.Post).Classify(thrown: null);

        Assert.Equal(InjectionOutcome.NeverFired, outcome);
    }

    /// <summary>
    /// The most consequential outcome, which is why it is asserted rather than assumed: a
    /// <c>Download</c> that returns normally after the callback threw means the API discarded the
    /// exception — and therefore that a callback CANNOT refuse a download by failing.
    /// </summary>
    [Fact]
    public void FiredAndDownloadReturnedNormally_IsSwallowed()
    {
        var injection = Armed(out _);

        var (outcome, detail) = injection.Classify(thrown: null);

        Assert.Equal(InjectionOutcome.Swallowed, outcome);
        Assert.Contains("CANNOT refuse a download by failing", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void FiredAndTheSameObjectCameBack_IsPropagatedVerbatim()
    {
        var injection = Armed(out var fired);

        Assert.Equal(InjectionOutcome.PropagatedVerbatim, injection.Classify(fired).Outcome);
    }

    [Fact]
    public void FiredAndCarriedInsideAnother_IsWrapped()
    {
        var injection = Armed(out var fired);
        var wrapper = new InvalidOperationException("outer", new InvalidOperationException("middle", fired));

        var (outcome, detail) = injection.Classify(wrapper);

        Assert.Equal(InjectionOutcome.Wrapped, outcome);
        Assert.Contains("2 level(s) deep", detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// What actually happened on the measured rehearsal: Openness threw
    /// <c>NonRecoverableException("Unexpected exception - no exception message available.")</c> and
    /// the injected exception was nowhere in its chain. Distinct from <see cref="InjectionOutcome.Wrapped"/>
    /// because the reason was DESTROYED, not merely nested.
    /// </summary>
    [Fact]
    public void FiredAndSomethingElseCameBack_IsReplaced_NotWrapped()
    {
        var injection = Armed(out _);

        var (outcome, detail) = injection.Classify(new InvalidOperationException("something else entirely"));

        Assert.Equal(InjectionOutcome.ReplacedByAnotherFailure, outcome);
        Assert.Contains("NOWHERE", detail, StringComparison.Ordinal);
    }

    // ---- defect 4: the guard that did not fire ---------------------------------------------

    /// <summary>
    /// *** THE GUARD FAILING IN THE BINARY WRITTEN TO CARRY IT. ***
    ///
    /// Measured on a real run: <c>--throw-from-post-delegate</c> was ARMED, the download raised ZERO
    /// post configurations, the delegate was NEVER INVOKED, the download completed — <b>and the tool
    /// exited 0</b>, where its own contract says 12. The lane caught it by reading the configuration
    /// list; the exit code said everything was fine. That is *"if you get zero configurations you
    /// have not tested what you think you are testing"* failing to fire, in the one program written
    /// to enforce it.
    ///
    /// The cause was a MISSING arm, not a wrong one — the completion path tested
    /// <c>Fired is not null</c> and had no branch for armed-and-never-fired at all. Which is why
    /// this test exists at the level it does: <c>Classify</c> is the only place the distinction is
    /// decidable without Portal, and it must return <c>NeverFired</c> rather than anything a caller
    /// could round to success.
    /// </summary>
    [Fact]
    public void ArmedAndNeverInvoked_ClassifiesAsNeverFired_NotAsAnythingSuccessful()
    {
        var injection = new ThrowInjection(DelegatePhase.Post);

        var (outcome, detail) = injection.Classify(thrown: null);

        Assert.Equal(InjectionOutcome.NeverFired, outcome);
        Assert.Contains("NEVER INVOKED", detail, StringComparison.Ordinal);
        Assert.Null(injection.Fired);
    }

    /// <summary>
    /// The distinction the exit codes rest on: "armed and never fired" and "fired and swallowed" both
    /// arrive with <c>thrown == null</c>, and they are opposite findings. If these ever collapsed
    /// into one outcome, a run that tested nothing would be reported as a run that discovered
    /// something.
    /// </summary>
    [Fact]
    public void NeverFiredAndSwallowed_AreDistinguishedByWhetherTheThrowHappened()
    {
        var neverFired = new ThrowInjection(DelegatePhase.Pre).Classify(thrown: null).Outcome;
        var swallowed = Armed(out _).Classify(thrown: null).Outcome;

        Assert.Equal(InjectionOutcome.NeverFired, neverFired);
        Assert.Equal(InjectionOutcome.Swallowed, swallowed);
        Assert.NotEqual(neverFired, swallowed);
    }

    // THE "DID NOT RUN" CASE for the guard itself: an UNARMED run must never earn either injection
    // code. A guard that fires when nobody asked for it is as useless as one that never fires.
    [Fact]
    public void AnUnarmedRun_IsNeitherFiredNorNeverFired_InAWayThatCouldGateIt()
    {
        var injection = ThrowInjection.None;

        Assert.False(injection.IsArmed);
        Assert.Null(injection.Fired);

        // NeverFired is reported, but IsArmed is what the session branches on — so an ordinary run
        // cannot be reclassified into exit 12 by this.
        Assert.Equal(InjectionOutcome.NeverFired, injection.Classify(thrown: null).Outcome);
    }

    // ---- defect 4, at the level the bug actually lived --------------------------------------

    /// <summary>
    /// *** THE ASSERTION THAT WOULD HAVE CAUGHT IT. ***
    ///
    /// The two tests above exercise <c>Classify</c>, which was <b>already correct</b> — the bug was a
    /// missing branch in the session's precedence chain, and that chain lived inline in a method
    /// needing Portal, a device and a download, so nothing could assert it. The chain is now a value
    /// decided by <see cref="ProbeVerdict.Decide"/>, and this is the case that was silently absent.
    /// </summary>
    [Fact]
    public void ArmedAndNeverFired_EarnsTwelve_EvenWhenTheDownloadCompleted()
    {
        Assert.Equal(
            ProbeExitCodes.InjectionNeverFired,
            ProbeVerdict.Decide(failedToApplyCount: 0, injectionArmed: true, injectionFired: false));
    }

    // THE "DID NOT RUN" CASES for the chain itself — all three must leave the download's own verdict
    // alone. A precedence rule that fires when it should not is the same defect pointed the other way.
    [Theory]
    [InlineData(false, false)]  // ordinary run: nothing armed
    [InlineData(true, true)]    // armed and fired: the injection reporter owns the outcome
    public void TheChain_LeavesAnOrdinaryRunAlone(bool armed, bool fired)
    {
        Assert.Null(ProbeVerdict.Decide(failedToApplyCount: 0, injectionArmed: armed, injectionFired: fired));
    }

    /// <summary>
    /// A failed apply outranks the injection verdict: "the run proves nothing at all" is strictly
    /// stronger than "this particular experiment did not run".
    /// </summary>
    [Fact]
    public void AFailedApply_OutranksAnUnfiredInjection()
    {
        Assert.Equal(
            ProbeExitCodes.SelectionApplyFailed,
            ProbeVerdict.Decide(failedToApplyCount: 1, injectionArmed: true, injectionFired: false));
    }

    // ---- the exit codes stay distinct ------------------------------------------------------

    // A deliberate failure must never share a code with a real one, or with "nothing happened".
    [Fact]
    public void InjectionExitCodes_AreDistinctFromEveryOtherOutcome()
    {
        int[] codes =
        {
            ProbeExitCodes.Completed, ProbeExitCodes.UsageError, ProbeExitCodes.EnvironmentError,
            ProbeExitCodes.RefusedByPath, ProbeExitCodes.NoProvider, ProbeExitCodes.NoDownloadTarget,
            ProbeExitCodes.SafetyRefused, ProbeExitCodes.AbortedByUnhandledConfiguration,
            ProbeExitCodes.CompletedWithErrors, ProbeExitCodes.UnexpectedError,
            ProbeExitCodes.SelectionApplyFailed, ProbeExitCodes.InjectedThrowFired,
            ProbeExitCodes.InjectionNeverFired,
        };

        Assert.Equal(codes.Length, new System.Collections.Generic.HashSet<int>(codes).Count);
    }
}
