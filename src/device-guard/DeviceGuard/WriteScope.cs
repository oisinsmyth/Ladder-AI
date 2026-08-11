namespace DeviceGuard;

/// <summary>
/// What a single test run intends to write, declared up front.
///
/// WHY PER-RUN AND NOT PER-DEVICE. The writable surface is a property of what a test needs, not of
/// the box — owner ruling, 2026-08-11. A campaign that only feeds simulated instrument readings has
/// no business reaching a command area, even on a rig where writing commands is perfectly legal. So
/// the run declares its own scope and the guard holds it to that.
///
/// The allowlist entry may still set an outer cap (<see cref="AllowlistEntry.WritableAreas"/>); where
/// both exist the effective permission is the INTERSECTION. Where the entry sets no cap, the run's
/// declaration stands alone — but a run must always declare something. An empty scope grants nothing,
/// the same posture the allowlist itself takes.
///
/// This is deliberately NOT a security boundary on its own. It is a blast-radius limiter: the device
/// fence (test-rig + write-eligible + physically isolated + identity-verified) is what authorizes at
/// all. The scope decides how much of an authorized device a given run may touch.
/// </summary>
public sealed record WriteScope(string Purpose, IReadOnlyList<string> Areas)
{
    /// <summary>A scope that permits nothing. The default, and what an absent declaration means.</summary>
    public static WriteScope Nothing { get; } = new("(undeclared)", Array.Empty<string>());

    public static WriteScope For(string purpose, params string[] areas) =>
        new(purpose, areas ?? Array.Empty<string>());

    public bool IsEmpty => Areas is null || Areas.Count == 0;

    /// <summary>Case-insensitive exact membership. No wildcards, no prefixes — same reasoning as the
    /// allowlist's exact address matching: a range match is how a surface gets widened by accident.</summary>
    public bool Includes(string? area) =>
        !string.IsNullOrWhiteSpace(area)
        && Areas is not null
        && Areas.Any(a => string.Equals(a?.Trim(), area.Trim(), StringComparison.OrdinalIgnoreCase));

    public string Describe() =>
        IsEmpty ? "(nothing)" : string.Join(", ", Areas.Select(a => $"'{a}'"));
}
