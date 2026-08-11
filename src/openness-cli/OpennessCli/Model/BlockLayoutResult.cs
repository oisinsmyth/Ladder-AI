using System;

namespace OpennessCli.Model;

/// <summary>
/// A block's memory layout — "optimized" vs "standard" block access.
///
/// Mirrors <c>Siemens.Engineering.SW.Blocks.MemoryLayout</c> (members <c>Standard</c> and
/// <c>Optimized</c>, confirmed by reflecting on the installed V20 assembly and in its own
/// <c>Siemens.Engineering.xml</c>: "Determines if a block access is optimized or not"). Declared
/// separately here for the same reason every other model type is — <c>IOpennessGateway</c> is
/// Siemens-free, so the CLI and its tests can be exercised with no Portal session and no COM.
///
/// The distinction is not cosmetic. Classic S7comm (the protocol a PC-side test harness reaches a
/// controller with) CANNOT SEE AN OPTIMIZED BLOCK AT ALL — the block is not reported as an error,
/// it is simply absent, and the failure surfaces at the first DATA read rather than at connect. On
/// an S7-1200 the TIA default is <see cref="Optimized"/>, so a DB authored to be read that way has
/// to be switched deliberately.
/// </summary>
public enum MemoryLayoutKind
{
    /// <summary>Non-optimized ("standard") block access — addressable over classic S7comm.</summary>
    Standard,

    /// <summary>Optimized block access — the S7-1200 default, and invisible to classic S7comm.</summary>
    Optimized,
}

/// <summary>
/// The outcome of a <c>block-layout</c> read or set.
///
/// <see cref="Layout"/> is always the layout the block ACTUALLY HAS at the end of the command: on a
/// read that is simply the current value, and on a set it is the value READ BACK after saving, not
/// the value that was requested. Keeping those separate is the whole point of the record — a set
/// that silently no-ops is the failure this command exists to prevent, and a result type that only
/// carried the request could not tell the two apart.
/// </summary>
/// <param name="Name">The block's own name, as resolved.</param>
/// <param name="Path">Its group path, matching <c>list</c>'s Path column.</param>
/// <param name="Type">OB/FB/FC/DB.</param>
/// <param name="Layout">What the block's layout is NOW — read back from the project.</param>
/// <param name="PreviousLayout">What it was before the set; <c>null</c> for a read-only invocation.</param>
/// <param name="RequestedLayout">What the set asked for; <c>null</c> for a read-only invocation.</param>
public sealed record BlockLayoutResult(
    string Name,
    string Path,
    BlockType Type,
    MemoryLayoutKind Layout,
    MemoryLayoutKind? PreviousLayout,
    MemoryLayoutKind? RequestedLayout)
{
    /// <summary>True when this result describes a set rather than a read.</summary>
    public bool IsSet => RequestedLayout is not null;

    /// <summary>
    /// Whether the read-back matches what was asked for. A read is vacuously verified — it asked
    /// for nothing. Note the CLI gates its exit code on its OWN requested value rather than on this
    /// property, so that a result which misreports what it was asked still fails closed.
    /// </summary>
    public bool Verified => RequestedLayout is null || Layout == RequestedLayout.Value;

    /// <summary>True when the set actually moved the block from one layout to the other.</summary>
    public bool Changed => PreviousLayout is not null && Layout != PreviousLayout.Value;
}

/// <summary>
/// The block's memory layout could not be read or written. Not every PLC block kind carries an
/// access mode, and Openness reports that by throwing rather than by returning something empty —
/// so this wraps the throw with the block's name and which half of the operation raised it.
///
/// Classified as a user-fixable command error rather than an internal fault: the correction is to
/// name a different block (a DB or an FB), which is exactly the shape of every other exit-7 error.
/// </summary>
public sealed class BlockMemoryLayoutUnavailableException : Exception
{
    public BlockMemoryLayoutUnavailableException(string blockName, string operation, Exception inner)
        : base($"Could not {operation} the memory layout of block '{blockName}': {inner.GetType().Name}: {inner.Message} " +
               "Not every block kind exposes an access mode — memory layout is a property of data blocks and " +
               "function blocks. Check the block kind with `openness-cli list`.", inner)
    {
    }
}
