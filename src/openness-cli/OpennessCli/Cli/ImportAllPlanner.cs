using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;

namespace OpennessCli.Cli;

// The other half of export-all. `import` takes N files, but it takes them as ONE kind (blocks, or
// --type, or --tagtable), in the order given, and stops at the first failure — which is fine for the
// two or three files a change touches and useless for putting a whole program back. A whole program
// is a mixed bag: tag tables, UDTs that contain other UDTs, FBs, the instance DBs of those FBs. The
// order is a dependency order, it is not knowable from the filenames, and getting it wrong does not
// produce a diagnosable error — it produces "Data type X is unknown" on a file that was perfectly
// good and would have imported ten seconds later.
//
// So the planner does the part that can be decided from the files alone: WHAT each file is, and a
// starting order. What it deliberately does NOT do is compute a dependency graph. The executor
// retries failures until a pass makes no progress, which converges from any starting order and does
// not need to understand a single datatype reference. The starting order is an optimisation that
// makes the common case one pass; correctness comes from the fixpoint, not from the sort.
public static class ImportAllPlanner
{
    public enum ItemKind
    {
        TagTable,
        Type,
        Block,
    }

    /// <param name="Phase">
    /// Ascending. Files in a lower phase are attempted (and retried to a fixpoint) before any file
    /// in a higher one, because those dependencies are one-directional across the whole corpus: a
    /// block may use a UDT, a UDT never uses a block.
    /// </param>
    /// <param name="Order">Starting order within a phase. An optimisation only — see the header.</param>
    public sealed record PlannedFile(string Path, string Name, ItemKind Kind, string RootElement, int Phase, int Order);

    public sealed record Rejected(string Path, string Reason);

    public sealed record Plan(IReadOnlyList<PlannedFile> Files, IReadOnlyList<Rejected> Rejections)
    {
        public bool IsUsable => Rejections.Count == 0;
    }

    /// <summary>
    /// Expands directories to their top-level <c>*.xml</c> (not recursive — an export directory sits
    /// beside its own superseded variants and older re-export dumps often enough that recursion would
    /// quietly import a stale copy of a block over a current one), classifies every file by its
    /// SimaticML root element, and orders the result.
    ///
    /// A file it cannot classify is a REJECTION, not a skip. This command exists to restore a whole
    /// program; a file silently left out of that is the one failure mode the operator cannot see,
    /// because what they get back looks complete.
    /// </summary>
    public static Plan Build(IReadOnlyList<string> paths)
    {
        var files = new List<string>();
        var rejections = new List<Rejected>();

        foreach (var path in paths)
        {
            if (Directory.Exists(path))
            {
                var found = Directory.GetFiles(path, "*.xml", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
                var before = files.Count;
                files.AddRange(found);
                if (files.Count == before)
                {
                    rejections.Add(new Rejected(path, "directory contains no .xml files"));
                }
            }
            else if (File.Exists(path))
            {
                files.Add(path);
            }
            else
            {
                rejections.Add(new Rejected(path, "no such file or directory"));
            }
        }

        var planned = new List<PlannedFile>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            string root;
            try
            {
                root = ReadRootElement(file);
            }
            catch (Exception ex)
            {
                rejections.Add(new Rejected(file, $"could not be read as SimaticML ({ex.GetType().Name}: {ex.Message})"));
                continue;
            }

            var kind = ClassifyRoot(root);
            if (kind is null)
            {
                rejections.Add(new Rejected(
                    file,
                    $"root element '{root}' is not a block, PLC data type or tag table (expected SW.Blocks.*, " +
                    "SW.Types.* or SW.Tags.PlcTagTable)"));
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(file);
            if (seen.TryGetValue(name, out var firstPath))
            {
                rejections.Add(new Rejected(
                    file,
                    $"'{name}' was already supplied by '{firstPath}'; importing both would make the later one " +
                    "silently overwrite the earlier, and which wins would depend on argument order"));
                continue;
            }

            seen[name] = file;
            planned.Add(new PlannedFile(file, name, kind.Value, root, PhaseOf(kind.Value), OrderOf(root)));
        }

        var ordered = planned
            .OrderBy(f => f.Phase)
            .ThenBy(f => f.Order)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new Plan(ordered, rejections);
    }

    /// <summary>
    /// Reads the first element inside &lt;Document&gt;. Cheap on purpose: these files reach 3 MB and
    /// nothing here needs their content, only their kind.
    /// </summary>
    public static string ReadRootElement(string path)
    {
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit };
        using var reader = XmlReader.Create(path, settings);

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.Name == "Document")
            {
                continue;
            }

            // <Engineering version="V20" /> is a sibling header, not the payload.
            if (reader.Name == "Engineering")
            {
                continue;
            }

            return reader.Name;
        }

        throw new InvalidOperationException("no element found inside <Document>");
    }

    public static ItemKind? ClassifyRoot(string rootElement)
    {
        if (rootElement.StartsWith("SW.Blocks.", StringComparison.Ordinal))
        {
            return ItemKind.Block;
        }

        if (rootElement.StartsWith("SW.Types.", StringComparison.Ordinal))
        {
            return ItemKind.Type;
        }

        if (rootElement.StartsWith("SW.Tags.", StringComparison.Ordinal))
        {
            return ItemKind.TagTable;
        }

        return null;
    }

    private static int PhaseOf(ItemKind kind) => kind switch
    {
        ItemKind.TagTable => 0,
        ItemKind.Type => 1,
        _ => 2,
    };

    // Within the block phase: an instance DB cannot resolve until the FB it instantiates exists, and
    // that is the one intra-phase dependency that is certain rather than probable. The rest is a
    // best guess at fewest retries and costs nothing if it is wrong.
    private static int OrderOf(string rootElement) => rootElement switch
    {
        "SW.Blocks.FB" => 0,
        "SW.Blocks.FC" => 1,
        "SW.Blocks.GlobalDB" => 2,
        "SW.Blocks.OB" => 3,
        "SW.Blocks.InstanceDB" => 4,
        "SW.Blocks.ArrayDB" => 4,
        _ => 5,
    };
}
