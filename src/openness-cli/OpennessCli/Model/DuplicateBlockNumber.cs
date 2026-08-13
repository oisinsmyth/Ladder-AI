using System;
using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

/// <summary>One block in a colliding group. Identification only — nothing here reads block content.</summary>
public sealed record DuplicateBlockNumberEntry(string Name, string Path, string Language, bool IsSafety);

/// <summary>
/// Two or more blocks holding the same number, on the same device, of the same block type.
///
/// <b>Measured 2026-08-13.</b> An artifact declaring <c>&lt;Number&gt;910&lt;/Number&gt;</c> was
/// imported into a project where another FC already held 910. <b>TIA accepted it and the project then
/// contained two blocks at FC 910</b> — and every gate in the chain passed: `import` exited 0, the
/// per-block `compile` exited 0 reporting *"successfully compiled"* and <c>CONSISTENT: yes</c>, the
/// device compile reported <c>Success, errors=0</c>, and `sanity-check` reported
/// <c>OVERALL: HEALTHY</c>, <c>INCONSISTENT: 0</c>, exit 0.
///
/// Hard rule 4 names `sanity-check` as THE gate, so this was a hole in the gate itself. It is the
/// FI-52/FI-62 family once more — a check reporting a clean result over a question it never asked —
/// with one extra twist that made it worse than either. `sanity-check` DID fire once immediately
/// after the colliding import, but as <c>INCONSISTENT: 1</c> — the wrong signal, about the wrong
/// property — and <b>a single per-block compile erased it</b>, after which the same check returned
/// HEALTHY with the duplicate still sitting there. A signal that is only true until you fix something
/// else is not a check.
/// </summary>
/// <param name="Device">
/// The device the blocks live on. Part of the key because block numbers are scoped to a PLC: two PLCs
/// in one project may each legitimately hold an FC 910, and grouping without this would manufacture a
/// finding out of a perfectly correct project.
/// </param>
public sealed record DuplicateBlockNumber(
    string Device,
    BlockType Type,
    int Number,
    IReadOnlyList<DuplicateBlockNumberEntry> Blocks);

/// <summary>
/// Groups an enumerated block list by (device, block type, number) and reports every group larger
/// than one.
///
/// Separated from the gateway so the rule can be exercised without a Portal — the enumeration half
/// cannot be, and a duplicate-number project is not something a unit test can ask TIA to build.
/// </summary>
public static class DuplicateBlockNumberFinder
{
    /// <summary>
    /// Every group of two or more blocks sharing one number. Deterministically ordered (device, type,
    /// number, then name) so a report can be diffed across runs.
    ///
    /// <b>Safety blocks are included</b>, and that is deliberate. Nothing here goes past what `list`
    /// already prints for one — name, type, number, path — so hard rule 2 is untouched; and excluding
    /// them would put a hole in the very check that exists because holes in this gate ship defects.
    /// A collision involving safety content is reported and nothing more is done with it: the
    /// engineer resolves it in TIA Portal.
    /// </summary>
    public static IReadOnlyList<DuplicateBlockNumber> Find(IEnumerable<BlockInfo> blocks)
    {
        if (blocks is null)
        {
            throw new ArgumentNullException(nameof(blocks));
        }

        return blocks
            .GroupBy(b => (Device: DeviceOf(b.Path), b.Type, b.Number))
            .Where(g => g.Count() > 1)
            .OrderBy(g => g.Key.Device, StringComparer.Ordinal)
            .ThenBy(g => g.Key.Type)
            .ThenBy(g => g.Key.Number)
            .Select(g => new DuplicateBlockNumber(
                g.Key.Device,
                g.Key.Type,
                g.Key.Number,
                g.OrderBy(b => b.Name, StringComparer.Ordinal)
                    .Select(b => new DuplicateBlockNumberEntry(b.Name, b.Path, b.Language, b.IsSafety))
                    .ToList()))
            .ToList();
    }

    /// <summary>
    /// The device a block's <see cref="BlockInfo.Path"/> starts with.
    ///
    /// <c>EnumerateBlocks</c> seeds the walk with <c>device.Name</c> and appends one segment per
    /// device item and block group, so the first segment IS the device, always. Read that way rather
    /// than adding a field to <see cref="BlockInfo"/>: every producer of a <c>BlockInfo</c> — the real
    /// gateway, the import path, the fakes — already fills Path, and a second field carrying the same
    /// fact is a field that can disagree with it.
    /// </summary>
    public static string DeviceOf(string path)
    {
        var slash = path.IndexOf('/');
        return slash < 0 ? path : path.Substring(0, slash);
    }
}
