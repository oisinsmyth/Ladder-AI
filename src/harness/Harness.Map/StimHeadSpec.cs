namespace Harness.Map;

/// <summary>
/// What a phase is, for the reader. <b>Only <see cref="Reset"/> is READ by the generator</b> — it builds
/// the reset pulse from the phases carrying it. The rest document intent, and documenting intent is worth
/// a field: a phase list is the one design decision that settles four networks at once.
/// </summary>
public enum StimPhaseKind
{
    Unstated = 0,

    /// <summary>Hold the block's detectors so nothing can create or re-create a latched cause.</summary>
    Disarm = 1,

    /// <summary>Pulse the block's fault reset. <b>The only kind the generator reads.</b></summary>
    Reset = 2,

    /// <summary>Look at whether the causes actually cleared.</summary>
    Verify = 3,

    /// <summary>The part under test.</summary>
    Scenario = 4,

    /// <summary>Let the block settle before asking it anything — e.g. seating a retained reading.</summary>
    Settle = 5,
}

/// <summary>One phase of the index.</summary>
/// <param name="Bit">The membership bit this phase drives (a static Bool).</param>
/// <param name="End">The boundary static holding the time this phase ends at.</param>
/// <param name="Duration">How long it lasts — a static or a UDT member, never a literal.</param>
/// <param name="Kind">See <see cref="StimPhaseKind"/>.</param>
public sealed record StimPhase(string Bit, string End, string Duration, StimPhaseKind Kind);

/// <summary>
/// 🔴 <b>A stimulus head, declared — the input the 18-network shell is derived from.</b>
///
/// <para>Everything here is a claim the head's author makes and the generator cannot check against the
/// plant. What it CAN check is internal consistency, and it checks a great deal of it: see
/// <see cref="StimShellGenerator"/>'s refusals, several of which are computed from the emitted rungs
/// rather than from a list somebody maintains.</para>
/// </summary>
/// <param name="HeadName">For the report only. Never emitted into the rungs.</param>
/// <param name="UutReset">
/// The block-under-test's fault-reset input, as a full member path. <b>Owned by S9 and written by nothing
/// else</b> — enforced, not merely stated.
/// </param>
/// <param name="Watchdog">S2's preset, as a Time literal. Far longer than any index this head can run.</param>
/// <param name="Dwell">The three cleardown dwells, as a Time literal.</param>
/// <param name="Phases">Ordered. The design decision that settles S5, S7, S8 and S18 by itself.</param>
/// <param name="Causes">
/// Every latched cause the block can hold. 🔴 <b>The most dangerous parameter here: a cause left out is a
/// cause that <c>InertAtStart</c>, <c>InertAtEnd</c> and <c>RecoverFailed</c> all report as absent,
/// forever, in green.</b> Empty is permitted — a head whose block latches nothing is legitimate — and it
/// is REPORTED, because "nothing to clear" and "nobody listed anything" must not look the same.
/// </param>
/// <param name="CycleEdges">Every one-scan counting edge. Empty omits S14 entirely.</param>
/// <param name="OutcomeBits">
/// Every published bit S17 must clear at re-arm. <b>Checked against what the shell actually SETS</b>, so a
/// missing one is refused rather than left to never clear.
/// </param>
public sealed record StimHeadSpec(
    string HeadName,
    string UutReset,
    string Watchdog,
    string Dwell,
    IReadOnlyList<StimPhase> Phases,
    IReadOnlyList<string> Causes,
    IReadOnlyList<string> CycleEdges,
    IReadOnlyList<string> OutcomeBits);
