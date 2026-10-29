using System;
using System.Collections.Generic;

namespace OpennessCli.Model;

/// <summary>
/// The outcome of an <c>ob-cycle</c> read or set: a cyclic-interrupt OB's cycle time and phase
/// shift, in milliseconds — the unit Openness itself reports on an S7-1200 (measured 2026-10-29: an
/// OB TIA shows as "100 ms" reads back <c>CyclicTime = 100</c>).
///
/// This exists because nothing else in the toolchain can see the cycle time. A TIA V20 export of a
/// cyclic-interrupt OB carries NO cycle-time element (measured 2026-10-29: the AttributeList holds
/// <c>SecondaryType=CyclicInterrupt</c> and nothing else about the event), so the converter has
/// nothing to read or write and <c>compare</c> nothing to diff. It is a property of the block
/// object, reachable only through Openness's dynamic attributes.
///
/// <see cref="CyclicTimeMs"/>/<see cref="PhaseShiftMs"/> are always what the block ACTUALLY HAS at
/// the end of the command — on a set, the value read back from a freshly re-resolved block after
/// saving, never an echo of the request. Same rule, same reason as <see cref="BlockLayoutResult"/>.
/// </summary>
/// <param name="Name">The block's own name, as resolved.</param>
/// <param name="Path">Its group path, matching <c>list</c>'s Path column.</param>
/// <param name="CyclicTimeMs">The cycle time NOW, read back from the project.</param>
/// <param name="PhaseShiftMs">The phase shift NOW, read back from the project.</param>
/// <param name="PreviousCyclicTimeMs">Before the set; <c>null</c> for a read.</param>
/// <param name="PreviousPhaseShiftMs">Before the set; <c>null</c> for a read.</param>
/// <param name="RequestedCyclicTimeMs">What the set asked for; <c>null</c> for a read.</param>
/// <param name="RequestedPhaseShiftMs">What the set asked for; <c>null</c> when the set left it alone.</param>
public sealed record ObCycleResult(
    string Name,
    string Path,
    long CyclicTimeMs,
    long PhaseShiftMs,
    long? PreviousCyclicTimeMs,
    long? PreviousPhaseShiftMs,
    long? RequestedCyclicTimeMs,
    long? RequestedPhaseShiftMs)
{
    /// <summary>True when this result describes a set rather than a read.</summary>
    public bool IsSet => RequestedCyclicTimeMs is not null;

    /// <summary>
    /// Whether the read-back matches what was asked for. A read is vacuously verified. The CLI
    /// gates its exit code on its OWN requested values, not on this, so a result that misreports
    /// its request still fails closed.
    /// </summary>
    public bool Verified =>
        (RequestedCyclicTimeMs is null || CyclicTimeMs == RequestedCyclicTimeMs.Value) &&
        (RequestedPhaseShiftMs is null || PhaseShiftMs == RequestedPhaseShiftMs.Value);

    /// <summary>True when the set actually moved either value.</summary>
    public bool Changed =>
        (PreviousCyclicTimeMs is not null && CyclicTimeMs != PreviousCyclicTimeMs.Value) ||
        (PreviousPhaseShiftMs is not null && PhaseShiftMs != PreviousPhaseShiftMs.Value);
}

/// <summary>
/// The block has no cycle-time attribute — it is not a cyclic-interrupt OB, or Openness refused the
/// read or write. Names the attributes the block DOES expose, so a wrong guess about the attribute
/// name is diagnosable from the message alone rather than needing a second probe.
///
/// A user-fixable command error (exit 7): the correction is to name a cyclic-interrupt OB.
/// </summary>
public sealed class ObCycleUnavailableException : Exception
{
    public ObCycleUnavailableException(string blockName, string operation, IReadOnlyList<string> availableAttributes, Exception? inner = null)
        : base($"Could not {operation} the cycle time of block '{blockName}'" +
               (inner is null ? "" : $": {inner.GetType().Name}: {inner.Message}") +
               ". Only a cyclic-interrupt OB has one - check the block kind with `openness-cli list`. " +
               $"Attributes this block exposes: {(availableAttributes.Count == 0 ? "(none reported)" : string.Join(", ", availableAttributes))}.",
               inner)
    {
        AvailableAttributes = availableAttributes;
    }

    public IReadOnlyList<string> AvailableAttributes { get; }
}
