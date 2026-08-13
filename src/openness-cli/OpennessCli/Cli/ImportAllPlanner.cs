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
                root = ReadObjectElement(file);
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
                    $"object element '{root}' is not a block, PLC data type or tag table (expected SW.Blocks.*, " +
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
    /// FINDS THE <c>SW.*</c> OBJECT. Does not "read the root element", which is what this used to do
    /// and why nothing exported by TIA could be imported.
    ///
    /// 🔴 **THE DEFECT (2026-08-13).** This walked elements, skipped exactly two names it knew about
    /// — <c>Document</c> and <c>Engineering</c> — and returned whatever came next. A real TIA export
    /// carries a THIRD header element:
    /// <code>
    /// &lt;Document&gt;
    ///   &lt;Engineering version="V20" /&gt;
    ///   &lt;DocumentInfo&gt;…&lt;/DocumentInfo&gt;      &lt;-- never on the skip list
    ///   &lt;SW.Blocks.FC ID="0"&gt;…
    /// </code>
    /// so it returned <c>"DocumentInfo"</c>, which classifies as nothing, and every file was
    /// rejected. Measured: a `export-all` of 126 items, then `import-all --dry-run` over that exact
    /// directory — <b>0 planned, 126 rejected</b>. NOT ONE FILE OF A COMPLETE RESTORE POINT WAS
    /// READABLE BY THE TOOL WHOSE JOB IS TO PUT IT BACK, and had been so since `import-all` was
    /// written.
    ///
    /// **Why the tests did not catch it: the fixtures were the problem.** They hand-wrote
    /// <c>&lt;Document&gt;&lt;Engineering/&gt;&lt;SW.Blocks.FB/&gt;&lt;/Document&gt;</c> — no
    /// <c>DocumentInfo</c> — so they described a document TIA does not produce, and passed. The
    /// end-to-end test beside them now uses real `export-all` output for exactly this reason.
    ///
    /// **Why single-file `import` was fine all week:** it never classifies. The caller states the
    /// kind with <c>--type</c>/<c>--tagtable</c>, so this code path is not on it — which is what made
    /// the failure look like bad files rather than a bad reader.
    ///
    /// **The fix is a LOCATE, not a longer skip list.** A deny-list is only ever as complete as the
    /// documents someone happened to look at; searching for what we want cannot be broken by a header
    /// element nobody has seen yet. This is also what `src/converter` has always done
    /// (<c>BlockSourceParser</c>: <c>Descendants().FirstOrDefault(e =&gt; e.Name.LocalName
    /// .StartsWith("SW.Blocks."))</c>), and the converter consumes these same exports without
    /// trouble — one rule, one behaviour, and the one that was already proven against real files.
    ///
    /// Document order, at any depth, is deliberate and safe: the outer object always precedes its own
    /// nested <c>SW.*</c> children (a real FC has <c>SW.Blocks.FC</c> at line 54 and
    /// <c>SW.Blocks.CompileUnit</c> at line 95). <c>LocalName</c>, not <c>Name</c>, so a namespace
    /// prefix cannot hide it.
    ///
    /// Still cheap: <c>XmlReader</c>, forward-only, stopping at the first match. These files reach
    /// 3 MB and nothing here needs their content, only their kind.
    /// </summary>
    public static string ReadObjectElement(string path)
    {
        var settings = new XmlReaderSettings { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit };
        using var reader = XmlReader.Create(path, settings);

        // Kept so a failure can NAME what the file did contain. "no SW.* element" over a 3 MB file
        // is not diagnosable; "saw Document, Engineering, DocumentInfo" is the whole answer.
        var seen = new List<string>();

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            if (reader.LocalName.StartsWith("SW.", StringComparison.Ordinal))
            {
                return reader.LocalName;
            }

            if (reader.Depth <= 1 && seen.Count < 10 && !seen.Contains(reader.LocalName))
            {
                seen.Add(reader.LocalName);
            }
        }

        throw new InvalidOperationException(
            "contains no SW.* object element (expected SW.Blocks.*, SW.Types.* or SW.Tags.PlcTagTable). " +
            $"Top-level elements seen: {(seen.Count == 0 ? "(none)" : string.Join(", ", seen))}");
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
