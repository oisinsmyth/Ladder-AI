namespace GoldenHarness;

/// <summary>
/// The actual reference-project round-trip proof (S1 item 1 — docs/notes/stage-gates.md), first
/// run 2026-07-10 against `NodeStatusAlarms`, extended the same day with three DBs
/// (`CommsProcessData`, `AlarmWords`, `EquipmentStatus`), extended 2026-07-11 with
/// `PerimeterSafetyAlarms` (S1 item 7 Phase A: OR-merge + negated contacts) — each pair under
/// `ir/reference/` / `simatic-ml/reference/`. Same "manual/live, not CI" reasoning as
/// <see cref="RoundTripRunner.RunFull"/> itself — needs a live Portal session and the actual
/// reference TIA project, so deliberately not an always-running [Fact]. Call
/// <see cref="RunAll"/> by hand (or from a throwaway test with a [Fact] attribute added
/// temporarily) whenever the reference project needs re-verifying, e.g. after a converter
/// change. DBs must compile before the blocks that depend on them (dependency-order lesson,
/// docs/notes/openness-quirks.md) — this list is already in that order.
///
/// Project path and group path are specific to this machine's reference project (not committed
/// anywhere else) — update here if the project moves.
/// </summary>
public static class ReferenceProjectRoundTrip
{
    private const string ProjectPath = @"C:\Users\User\Desktop\AI Ladder Project\SampleProject\SampleProject.ap20";
    private const string Device = "S7-1200 station_1";
    private const string GroupPath = "S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0";

    private static readonly string[] BlockNamesInDependencyOrder =
    {
        "CommsProcessData",
        "AlarmWords",
        "EquipmentStatus",
        "NodeStatusAlarms",
        "PerimeterSafetyAlarms",
        "ThresholdAlarms",
        "SignalConditioning",
        "DataHandling",
    };

    public static RoundTripReport Run(string blockName, string workDir)
    {
        var runner = new RoundTripRunner();
        return runner.RunFull(ProjectPath, blockName, Device, GroupPath, workDir);
    }

    public static IReadOnlyDictionary<string, RoundTripReport> RunAll(string workDir)
    {
        var results = new Dictionary<string, RoundTripReport>();
        foreach (var blockName in BlockNamesInDependencyOrder)
        {
            results[blockName] = Run(blockName, workDir);
        }

        return results;
    }
}
