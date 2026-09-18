namespace GoldenHarness;

/// <summary>
/// The actual reference-project round-trip proof (S1 item 1 — docs/notes/stage-gates.md), first
/// run 2026-07-10 against `NodeStatusAlarms`, extended the same day with three DBs
/// (`CommsProcessData`, `AlarmWords`, `EquipmentStatus`), extended 2026-07-11 with
/// `PerimeterSafetyAlarms` (S1 item 7 Phase A: OR-merge + negated contacts) and `TimerSample`/
/// `DB_Timers` (S1 item 8: TON, both instance scopes) — each pair under `ir/reference/` /
/// `simatic-ml/reference/`. Extended again 2026-07-14 (instruction-coverage corpus growth —
/// `docs/audit/2026-07-14-code-quality-and-docs-audit.md`'s "proven live-TIA round-trips aren't
/// protected by any permanent regression suite" finding) with `ThresholdAlarms` (comparisons),
/// `SignalConditioning`/`DataHandling` (arithmetic/box family), `BooleanExtras` (standalone Not,
/// SCoil/RCoil), `FBTimers`/`ScaleValue`/`TimingAndCalls` (TONR/TOF/CALL). `TimerSample`/
/// `DB_Timers` themselves were only added to *this* list at the same time — present in the
/// committed corpus since 2026-07-11 but never previously wired into automated re-verification,
/// a small pre-existing gap of the same kind fixed alongside the rest. Same "manual/live, not CI"
/// reasoning as <see cref="RoundTripRunner.RunFull"/> itself — needs a live Portal session and the
/// actual reference TIA project, so deliberately not an always-running [Fact]. Call
/// <see cref="RunAll"/> by hand (or from a throwaway test with a [Fact] attribute added
/// temporarily) whenever the reference project needs re-verifying, e.g. after a converter
/// change. DBs must compile before the blocks that depend on them (dependency-order lesson,
/// docs/notes/openness-quirks.md) — this list is already in that order. Note: batching many
/// independent blocks in one run can surface TIA's own `IsConsistent` cascade (re-importing a
/// callee/dependency re-flags its callers/dependents even when nothing observably changed) —
/// confirmed benign 2026-07-14 (`TimingAndCalls` vs. its `FBTimers`/`ScaleValue` dependencies);
/// re-running a block alone after a fresh compile always clears it. Not a correctness issue with
/// the committed content itself, just a quirk of re-verifying many interdependent blocks in one
/// sitting.
///
/// Project path and group path are specific to this machine's reference project (not committed
/// anywhere else) — update here if the project moves.
/// </summary>
public static class ReferenceProjectRoundTrip
{
    private const string ProjectPath = @"C:\Users\<user>\Desktop\AI Ladder Project\SampleProject\SampleProject.ap20";
    private const string Device = "S7-1200 station_1";
    private const string GroupPath = "S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0";

    private static readonly string[] BlockNamesInDependencyOrder =
    {
        "CommsProcessData",
        "AlarmWords",
        "EquipmentStatus",
        "NodeStatusAlarms",
        "PerimeterSafetyAlarms",
        "DB_Timers",
        "TimerSample",
        "ThresholdAlarms",
        "SignalConditioning",
        "DataHandling",
        "BooleanExtras",
        "FBTimers",
        "ScaleValue",
        "TimingAndCalls",
    };

    public static RoundTripReport Run(string blockName, string workDir)
    {
        var runner = new RoundTripRunner();
        return runner.RunFull(ProjectPath, blockName, Device, GroupPath, workDir);
    }

    public static IReadOnlyDictionary<string, RoundTripReport> RunAll(string workDir)
    {
        var runner = new RoundTripRunner();
        return runner.RunAllSettled(ProjectPath, BlockNamesInDependencyOrder, Device, GroupPath, workDir);
    }
}
