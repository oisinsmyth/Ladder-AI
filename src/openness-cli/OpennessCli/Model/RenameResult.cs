using System;
using System.Collections.Generic;
using System.Linq;
using OpennessCli.Openness;

namespace OpennessCli.Model;

/// <summary>
/// What a <c>rename</c> acts on. Each kind is a different Openness composition with its own
/// <c>Name</c> setter — confirmed by reflecting on the installed V20 <c>Siemens.Engineering.dll</c>
/// (2026-11-02): <c>PlcBlock.Name</c>, <c>PlcType.Name</c>,
/// <c>TechnologicalInstanceDB.Name</c>, <c>HmiUnified.HmiTags.HmiTag.Name</c> and
/// <c>HmiUnified.HmiAlarm.HmiDiscreteAlarm.Name</c> all have a PUBLIC setter. A setter existing in
/// the metadata is not proof TIA accepts every value at runtime; a refusal is
/// <see cref="RenameRefusedException"/>, never a silent pass.
/// </summary>
public enum RenameTargetKind
{
    /// <summary>A program block: OB, FB, FC, global DB or instance DB.</summary>
    Block,

    /// <summary>A PLC data type (UDT).</summary>
    Type,

    /// <summary>A technology object (e.g. a PID_Compact instance) — <c>PlcSoftware.TechnologicalObjectGroup</c>.</summary>
    TechnologicalObject,

    /// <summary>A WinCC Unified HMI tag (any tag table; a structure tag is renamed as a whole).</summary>
    HmiTag,

    /// <summary>A WinCC Unified discrete alarm.</summary>
    HmiAlarm,
}

/// <summary>
/// The outcome of a confirmed <c>rename</c>. Every "after" value is read back from the object
/// RE-RESOLVED BY ITS NEW NAME after saving — never an echo of the request. Same rule, same reason as
/// <see cref="BlockLayoutResult"/> and <see cref="ObCycleResult"/>.
/// </summary>
/// <param name="Kind">What was renamed.</param>
/// <param name="Path">Where it was found: the group path for PLC objects (matching <c>list</c>'s Path
/// column), the device path plus tag table for an HMI tag, the device path for an alarm.</param>
/// <param name="OldName">The name it was resolved by.</param>
/// <param name="RequestedName">The <c>--to</c> value.</param>
/// <param name="NameAfter">The name read back from the object found by <paramref name="RequestedName"/>
/// after saving; <c>null</c> when nothing (or more than one thing) answered to the new name.</param>
/// <param name="NumberBefore">Block number / alarm ID before; <c>null</c> for kinds without one.</param>
/// <param name="NumberAfter">The same, read back from the re-resolved object.</param>
/// <param name="OldNameStillPresent">True when the OLD name still resolves after saving — a rename
/// that left the original in place is not a rename.</param>
public sealed record RenameResult(
    RenameTargetKind Kind,
    string Path,
    string OldName,
    string RequestedName,
    string? NameAfter,
    int? NumberBefore,
    int? NumberAfter,
    bool OldNameStillPresent)
{
    /// <summary>What the number column means for this kind: a block number, an alarm ID, or nothing.</summary>
    public string? NumberLabel => RenameRules.NumberLabel(Kind);

    /// <summary>The new name read back exactly (ordinal) as requested.</summary>
    public bool NameMatches => string.Equals(NameAfter, RequestedName, StringComparison.Ordinal);

    /// <summary>The number (or alarm ID) moved — a rename must never renumber.</summary>
    public bool NumberChanged => NumberBefore != NumberAfter;

    /// <summary>
    /// Whether the rename is proved. The CLI gates its exit code on ITS OWN <c>--to</c> value, not on
    /// <see cref="RequestedName"/>, so a result that misreports its request still fails closed.
    /// </summary>
    public bool Verified => NameMatches && !NumberChanged && !OldNameStillPresent;
}

/// <summary>One name already present in the composition the renamed object lives in.</summary>
/// <param name="Name">The existing name.</param>
/// <param name="Description">What holds it and where, e.g. "block at PLC_1/Program blocks".</param>
public sealed record ExistingName(string Name, string Description);

/// <summary>
/// The pure decisions behind <c>rename</c>, kept out of the gateway so they are unit-testable
/// without a Portal session.
/// </summary>
public static class RenameRules
{
    /// <summary>The user-facing noun for each kind.</summary>
    public static string Label(RenameTargetKind kind) => kind switch
    {
        RenameTargetKind.Block => "block",
        RenameTargetKind.Type => "PLC data type",
        RenameTargetKind.TechnologicalObject => "technology object",
        RenameTargetKind.HmiTag => "HMI tag",
        RenameTargetKind.HmiAlarm => "HMI discrete alarm",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>The command-line flag that selects each kind.</summary>
    public static string Flag(RenameTargetKind kind) => kind switch
    {
        RenameTargetKind.Block => "--block",
        RenameTargetKind.Type => "--type",
        RenameTargetKind.TechnologicalObject => "--techobject",
        RenameTargetKind.HmiTag => "--hmitag",
        RenameTargetKind.HmiAlarm => "--hmialarm",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>What a kind's number means, or <c>null</c> when it has none.</summary>
    public static string? NumberLabel(RenameTargetKind kind) => kind switch
    {
        RenameTargetKind.Block or RenameTargetKind.TechnologicalObject => "number",
        RenameTargetKind.HmiAlarm => "alarm ID",
        _ => null,
    };

    /// <summary>
    /// Why <paramref name="newName"/> is not usable, or <c>null</c> when it is. Deliberately only the
    /// rules this tool can state without guessing at TIA's own grammar: a name is taken VERBATIM and
    /// matched ordinal everywhere in this CLI, so whitespace at either end or an invisible character
    /// would produce a name no later command can find by typing it. A double quote is TIA's own
    /// symbolic-name delimiter (<c>"DB_Example".Member</c>). Anything else TIA refuses surfaces as
    /// <see cref="RenameRefusedException"/> from the setter itself, with TIA's own message.
    /// </summary>
    public static string? ValidateNewName(string? newName)
    {
        if (string.IsNullOrEmpty(newName))
        {
            return "the new name is empty.";
        }

        if (newName!.Trim().Length == 0)
        {
            return "the new name is only whitespace.";
        }

        if (char.IsWhiteSpace(newName[0]) || char.IsWhiteSpace(newName[newName.Length - 1]))
        {
            return "the new name has leading or trailing whitespace. Names are matched exactly and never trimmed, so it could not be typed back.";
        }

        foreach (var c in newName)
        {
            if (char.IsControl(c) || IsInvisible(c))
            {
                return $"the new name contains an invisible or control character (U+{(int)c:X4}).";
            }

            if (c == '"')
            {
                return "the new name contains a double quote, TIA's symbolic-name delimiter.";
            }
        }

        return null;
    }

    /// <summary>
    /// Every existing name the new one would collide with. Compared CASE-INSENSITIVELY on purpose: it
    /// is not measured whether TIA treats two names differing only in case as distinct, and refusing a
    /// rename TIA would have allowed costs a re-run, while allowing one it would have refused (or
    /// worse, accepted into an ambiguous project) costs more. The caller passes the composition's
    /// names WITHOUT the object being renamed, so a case-only rename of an object to itself is not a
    /// collision.
    /// </summary>
    public static IReadOnlyList<ExistingName> FindCollisions(string newName, IEnumerable<ExistingName> existing) =>
        existing.Where(e => string.Equals(e.Name, newName, StringComparison.OrdinalIgnoreCase)).ToList();

    // The same set NameDiagnostics folds away: zero-width space/joiners, word joiner, BOM, NBSP.
    private static bool IsInvisible(char c) =>
        c is '​' or '‌' or '‍' or '⁠' or '﻿' or ' ';
}

/// <summary>
/// No object of the requested kind answers to the name. Carries how many of that kind exist and,
/// via the shared <see cref="NameDiagnostics"/>, any near match differing only by whitespace,
/// invisible characters or case. A user-fixable command error (exit 7).
/// </summary>
public sealed class RenameSourceNotFoundException : Exception
{
    public RenameSourceNotFoundException(RenameTargetKind kind, string name, IReadOnlyCollection<string> present)
        : base($"No {RenameRules.Label(kind)} named '{name}' found ({present.Count} {RenameRules.Label(kind)}(s) present" +
               (kind == RenameTargetKind.Block ? "; technology objects are not blocks here - use --techobject" : string.Empty) +
               ")." + (NameDiagnostics.DescribeNearMatch(name, present) ?? string.Empty))
    {
    }
}

/// <summary>The name resolves in more than one place. Exit 7: pass <c>--device</c>.</summary>
public sealed class AmbiguousRenameSourceException : Exception
{
    public AmbiguousRenameSourceException(RenameTargetKind kind, string name, IEnumerable<string> paths)
        : base($"{RenameRules.Label(kind)} '{name}' exists in more than one place: {string.Join(", ", paths)}. Pass --device to disambiguate.")
    {
    }
}

/// <summary>
/// The new name is already taken in the composition the object lives in. Refused BEFORE anything is
/// set, so nothing changed. Exit 7.
/// </summary>
public sealed class RenameTargetExistsException : Exception
{
    public RenameTargetExistsException(RenameTargetKind kind, string oldName, string newName, IReadOnlyList<ExistingName> collisions)
        : base($"Refusing to rename {RenameRules.Label(kind)} '{oldName}' to '{newName}': that name is already used by " +
               string.Join("; ", collisions.Select(c => $"'{c.Name}' ({c.Description})")) +
               ". Compared case-insensitively. Nothing was changed.")
    {
        Collisions = collisions;
    }

    public IReadOnlyList<ExistingName> Collisions { get; }
}

/// <summary>
/// Openness REFUSED to set the name — the setter threw. Its own exit code (20) rather than 7 or 5:
/// nothing was named wrongly, and it is a modelled outcome ("this object cannot be renamed through
/// this API, or not to this name") rather than an internal fault. Nothing was saved by this command.
/// The inner exception carries TIA's own reason.
/// </summary>
public sealed class RenameRefusedException : Exception
{
    public RenameRefusedException(RenameTargetKind kind, string oldName, string newName, Exception inner)
        : base($"Openness refused to rename {RenameRules.Label(kind)} '{oldName}' to '{newName}': {inner.GetType().Name}: {inner.Message} " +
               "This command did not save the project. Rename it in TIA Portal by hand if it must change.",
               inner)
    {
    }
}
