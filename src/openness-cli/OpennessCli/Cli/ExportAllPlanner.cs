using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpennessCli.Model;

namespace OpennessCli.Cli;

// FI-70. `drift-check` compares ir/*.ir against simatic-ml/*.xml and NEITHER SIDE IS THE CONTROLLER,
// so a file edited on disk and never imported — or a block changed in TIA and never exported — is
// invisible to every automated check this project has. The converter half of the fix is built; this
// is the half that produces something to compare AGAINST, and it lives here rather than in the
// converter because the converter is a pure in-process file transformer that never touches the
// environment (FI-24, held on purpose).
//
// The decision of WHAT to export is separated from the act of exporting so it can be tested without
// a Portal: what gets a file, what is refused, where each lands, and — the part that matters most —
// what the report must not stay silent about.
public static class ExportAllPlanner
{
    public enum ItemKind
    {
        Block,
        Type,
        TagTable,
    }

    // Refused is a first-class outcome, not an omission. A safety block MUST NOT be exported (hard
    // rule 2), but a dump that quietly contained one fewer file than the project has blocks would be
    // handed to a completeness check that reads absence as "not in the controller" — turning a
    // correct refusal into a false finding. So it is planned, named, and reported.
    public sealed record PlannedItem(string Name, ItemKind Kind, string Path, string? OutPath, string? RefusedReason)
    {
        public bool WillExport => OutPath is not null;
    }

    public sealed record Plan(IReadOnlyList<PlannedItem> Items)
    {
        public IEnumerable<PlannedItem> ToExport => Items.Where(i => i.WillExport);

        public IEnumerable<PlannedItem> Refused => Items.Where(i => !i.WillExport);
    }

    /// <summary>
    /// Names collide across kinds far more easily than within one: a UDT and a DB may legitimately
    /// share a name, and both would land on "&lt;name&gt;.xml". The pairing `drift-check` does is by
    /// basename, so a collision would silently overwrite one export with the other and the comparison
    /// would then be against the wrong object — a false MATCH or a false DRIFT with nothing to
    /// indicate which. Detected and refused rather than resolved by a suffix, because a suffix would
    /// break the basename pairing that makes the two-command recipe work at all.
    /// </summary>
    public static Plan Build(
        IReadOnlyList<BlockInfo> blocks,
        IReadOnlyList<PlcTypeInfo> types,
        IReadOnlyList<TagTableInfo> tagTables,
        string outDir,
        bool includeTagTables)
    {
        var items = new List<PlannedItem>();
        var claimed = new Dictionary<string, PlannedItem>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, ItemKind kind, string path, string? refusedReason)
        {
            if (refusedReason is not null)
            {
                items.Add(new PlannedItem(name, kind, path, OutPath: null, refusedReason));
                return;
            }

            if (claimed.TryGetValue(name, out var existing))
            {
                items.Add(new PlannedItem(
                    name, kind, path, OutPath: null,
                    $"name collides with the {existing.Kind} of the same name ({existing.Path}); both would write " +
                    $"'{name}.xml' and drift-check pairs by basename, so one would silently compare against the other"));
                return;
            }

            var planned = new PlannedItem(name, kind, path, Path.Combine(outDir, name + ".xml"), RefusedReason: null);
            claimed[name] = planned;
            items.Add(planned);
        }

        foreach (var block in blocks.OrderBy(b => b.Name, StringComparer.Ordinal))
        {
            Add(block.Name, ItemKind.Block, block.Path,
                block.IsSafety
                    ? "classifies as safety content and is never exported by this pipeline (hard rule 2)"
                    : null);
        }

        // A UDT carries no ProgrammingLanguage at all, so there is nothing for the safety classifier
        // to test and no safety refusal exists on this path (ExportType's own reasoning).
        foreach (var type in types.OrderBy(t => t.Name, StringComparer.Ordinal))
        {
            Add(type.Name, ItemKind.Type, type.Path, refusedReason: null);
        }

        if (includeTagTables)
        {
            foreach (var table in tagTables.OrderBy(t => t.Name, StringComparer.Ordinal))
            {
                Add(table.Name, ItemKind.TagTable, table.Path, refusedReason: null);
            }
        }

        return new Plan(items);
    }
}
