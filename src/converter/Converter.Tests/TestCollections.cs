using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 <b>The two groups of tests in this suite that CANNOT run while anything else runs, and why the
/// mechanism is a collection rather than a tuned threshold.</b>
///
/// <para>xunit runs test collections in parallel. A test whose subject is a PROPERTY OF THE MACHINE —
/// what two OS processes do when they contend, or what a process-global <c>Console.Out</c> holds — is
/// not measuring its subject when 1,600 other tests are competing for the same four cores and the same
/// single console. Both failures below were measured, not anticipated, and both were intermittent:
/// green in isolation, red under load, which is the worst shape a test can have because it teaches the
/// reader to re-run rather than to read.</para>
///
/// <para><b><c>DisableParallelization = true</c> is verified behaviour here, not a hopeful attribute.</b>
/// Probed on xunit 2.5.3 with a throwaway three-test fixture: two classes in ordinary collections
/// overlapped exactly (2026-08-24T03:02:27.679→29.690, both) — the positive control, proving the probe
/// could see concurrency at all — while the class in a <c>DisableParallelization</c> collection ran
/// strictly afterwards (29.698→31.714), overlapping neither. A collection WITHOUT that flag would not
/// have done it: separate collections are exactly what xunit runs concurrently.</para>
/// </summary>
public static class TestCollections
{
    /// <summary>
    /// <b><c>LeaseProcessRaceTests</c> and <c>ClaimProcessRaceTests</c>.</b> They launch eight real
    /// converter processes and assert that the processes OVERLAPPED — the assertion that makes "exactly
    /// one won" mean anything. Under full-suite parallelism on four cores the launches spread out past
    /// the lifetime of the first racer, the overlap budget is exhausted, and the test goes red for a
    /// reason that has nothing to do with the lock it is testing.
    ///
    /// <para>🔴 <b>The alternative was to raise the retry budget until the red went away, and that is a
    /// test tuned to pass rather than to measure.</b> Nothing here is loosened: the overlap assertion is
    /// unchanged and still reddens when the racers are serialised. What changed is that the test is now
    /// given the machine it needs in order to observe the thing it exists to observe.</para>
    /// </summary>
    public const string ProcessRace = "process-race";

    /// <summary>
    /// <b>Every class that captures output by swapping the process-global <c>Console.Out</c></b> —
    /// <c>NeighbourTests</c>, <c>ServedAreaTests</c>, <c>ConvertOutputSafetyTests</c>,
    /// <c>InterfaceCheckSubjectTests</c>.
    ///
    /// <para>Measured 2026-08-24: running <c>NeighbourTests</c> and <c>ServedAreaTests</c> together and
    /// nothing else produced <b>six failures in one run</b> (and none in the two runs before it), always
    /// in pairs, one from each class. <c>Console.SetOut</c> is process-global: two classes capturing
    /// concurrently steal each other's writer, and each one's restore hands back a writer the other is
    /// still using. Nothing about either test is wrong; they simply cannot both be in flight.</para>
    ///
    /// <para><b>Why the whole collection is serialised against the suite rather than merely against
    /// itself.</b> Grouping the four capturers alone would stop them stealing from each other and would
    /// NOT stop a test in any other collection writing to the console mid-capture — every CLI-shaped
    /// test in this suite writes to <c>Console</c>, and that output would land inside somebody's
    /// <c>StringWriter</c>. The hazard is the global, so the fence has to be against everything. It is
    /// cheap: these four classes run in well under a second between them.</para>
    ///
    /// <para><b>The better fix is not available from here.</b> Capturing without the global swap means
    /// the commands taking a <c>TextWriter</c> instead of writing to <c>Console</c> directly — a change
    /// to <c>Program</c>, i.e. production code, and out of this change's remit. Worth doing: it would
    /// remove the hazard rather than schedule around it, and it would let these tests run in parallel
    /// again. Until then this collection is the honest fence, and this comment is the pointer to the
    /// real fix.</para>
    /// </summary>
    public const string ConsoleCapture = "console-capture";
}

[CollectionDefinition(TestCollections.ProcessRace, DisableParallelization = true)]
public sealed class ProcessRaceCollection { }

[CollectionDefinition(TestCollections.ConsoleCapture, DisableParallelization = true)]
public sealed class ConsoleCaptureCollection { }
