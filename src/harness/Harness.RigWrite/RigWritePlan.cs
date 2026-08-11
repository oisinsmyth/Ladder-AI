using DeviceGuard;
using Harness.S7;

namespace Harness.RigWrite;

/// <summary>What a planned step would do, if the run were armed.</summary>
public enum StepStatus
{
    /// <summary>Decided here, offline, and it passes.</summary>
    Ready,

    /// <summary>Cannot be decided without contacting the device. Not a pass — an open question.</summary>
    Deferred,

    /// <summary>Decided here, offline, and it FAILS. The run would stop at the first of these.</summary>
    Blocked,

    /// <summary>Never reached, because an earlier step blocks or because this build cannot arm.</summary>
    NotReached,
}

/// <summary>One step of the sequence, with what is known about it right now.</summary>
public sealed record PlanStep(int Number, string Name, StepStatus Status, string Detail)
{
    public string Describe() => $"{Number}. [{Label(Status)}] {Name}\n     {Detail}";

    private static string Label(StepStatus s) => s switch
    {
        StepStatus.Ready => "READY   ",
        StepStatus.Deferred => "DEFERRED",
        StepStatus.Blocked => "BLOCKED ",
        _ => "NOT RUN ",
    };
}

/// <summary>
/// The result of planning a governed write WITHOUT contacting anything.
///
/// <para><b>What a plan is and is not.</b> It is the fence's own verdict on every question that can be
/// answered from files — the allowlist, the identity CONFIGURATION, the run's scope, the restore point
/// on disk — plus an explicit list of the questions that only the device can answer. It is not a
/// prediction that the write will succeed: a plan whose every offline step is Ready still has
/// <see cref="StepStatus.Deferred"/> steps, and each of those can refuse.</para>
///
/// <para><b>The identity gate is the interesting deferral.</b> A dry run cannot read the device, so
/// the fence is asked its question with the identity the allowlist DECLARES standing in for the one
/// the device would report. That is deliberately optimistic — it is the only assumption in the whole
/// plan — and it is stated as <see cref="IdentityAssumed"/> so that a Ready verdict is never mistaken
/// for a verified one. Every other gate is decided for real.</para>
/// </summary>
public sealed record RigWritePlan(
    RigWriteRequest Request,
    string? AllowlistPath,
    AllowlistEntry? Entry,
    IdentitySourcePlan? IdentityPlan,
    WriteDecision? Decision,
    bool IdentityAssumed,
    IReadOnlyList<PlanStep> Steps,
    IReadOnlyList<string> Blockers)
{
    /// <summary>
    /// Nothing that could be decided from files refuses this write. NOT "the write would succeed" —
    /// the <see cref="StepStatus.Deferred"/> steps are still open and any of them can refuse.
    /// </summary>
    public bool OfflineClear => Blockers.Count == 0;

    /// <summary>True only when nothing decidable offline blocks, and the build could arm.</summary>
    public bool WouldProceed => OfflineClear && Arming.CompiledIn;

    /// <summary>The fence's first refusing gate, or null when the fence allows.</summary>
    public WriteRefusal? BlockingGate =>
        Decision is null ? null : Decision.Allowed ? null : Decision.Reason;

    public string Render()
    {
        var lines = new List<string>
        {
            $"GOVERNED WRITE — DRY RUN (no device was contacted)",
            $"  target  : {Request.Target}",
            $"  write   : {Request.Describe()}",
            $"  area    : '{Request.Area}'   purpose: '{Request.Purpose}'",
            $"  list    : {AllowlistPath ?? "<none configured>"}",
            string.Empty,
        };

        foreach (var step in Steps) lines.Add(step.Describe());

        lines.Add(string.Empty);

        if (Blockers.Count == 0)
        {
            lines.Add("NOTHING DECIDABLE OFFLINE BLOCKS THIS WRITE.");
            lines.Add("  The deferred steps above are still open: each can refuse once the device answers.");
        }
        else
        {
            lines.Add($"THIS WRITE WOULD NOT HAPPEN. {Blockers.Count} blocker(s):");
            foreach (var b in Blockers) lines.Add($"  - {b}");
        }

        lines.Add(string.Empty);
        lines.Add(Arming.CompiledIn
            ? "This build can arm."
            : "ARMING: " + Arming.WhyNot);

        return string.Join(Environment.NewLine, lines);
    }
}
