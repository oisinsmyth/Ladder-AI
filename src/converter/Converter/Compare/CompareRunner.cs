using System.Xml;
using System.Xml.Linq;
using Converter.SimaticMl;

namespace Converter.Compare;

/// <summary>
/// Normalizer-based comparison of two SimaticML documents that reports WHAT differs, not merely
/// THAT something does.
///
/// <para><b>Why not <see cref="Normalizer.AreSemanticallyEquivalent"/> alone.</b> It answers
/// yes/no, which is all `drift-check` needs (it reports per-block, and the block name is the whole
/// answer). The confirm loop's value is the opposite: one block, and the question is which element
/// TIA changed. A bare verdict would send the operator to a manual XML diff — through UId churn
/// the Normalizer exists to absorb — which is the state this command replaces.</para>
///
/// <para><b>The comparison walks the NORMALIZED trees</b>, i.e. exactly the trees
/// <see cref="Normalizer.AreSemanticallyEquivalent"/> hands to <c>XNode.DeepEquals</c>. That is not
/// a convenience: comparing the raw documents would fire on every reassigned Part/Wire/Access UId
/// (TIA reassigns them unprompted, recorded across this project) and byte-comparison is therefore
/// unusable as a gate. It also means the walk inherits the Normalizer's content-order sorting of
/// <c>Wires</c>/<c>Parts</c>, so pairing children positionally within a name is meaningful.</para>
///
/// <para><b>The verdict is cross-checked against the Normalizer</b>: if the walk localizes nothing
/// but <c>AreSemanticallyEquivalent</c> says the documents differ, that is reported as NotCompared,
/// never as a pass. The walk knows about elements, attributes and text and deliberately not about
/// comments or processing instructions; a difference living only there would otherwise become a
/// silent green — the exact failure shape this whole loop exists to close.</para>
/// </summary>
public static class CompareRunner
{
    public static CompareReport Run(string firstPath, string secondPath, bool allowSilentLayout = false)
    {
        // A comparison of a file with itself is trivially equivalent and proves nothing about the
        // round trip — and it is a realistic orchestration slip (the same path built twice from a
        // template). FI-44: refuse rather than return the most reassuring possible answer.
        if (SameFile(firstPath, secondPath))
        {
            return NotCompared(firstPath, secondPath,
                "both paths resolve to the same file — a document compared against itself is equivalent by " +
                "construction and says nothing about the round trip.");
        }

        if (!TryLoad(firstPath, out var first, out var firstError))
        {
            return NotCompared(firstPath, secondPath, firstError!);
        }

        if (!TryLoad(secondPath, out var second, out var secondError))
        {
            return NotCompared(firstPath, secondPath, secondError!);
        }

        var firstRoot = first!.Root!;
        var secondRoot = second!.Root!;

        // MemoryLayout is compared as an OPTIONAL ASSERTION inside the Normalizer: both sides must
        // declare one for a difference to be held against them (see Normalizer's own note — every
        // committed .ir predates the emit side, so a strict compare would turn the corpus red for a
        // benign reason).
        //
        // In the CONFIRM LOOP that weakness cannot arise: both inputs are TIA-produced exports and a
        // TIA export always states a layout. So the loop gets full strictness for free — *provided
        // the premise holds*. A silent side means the premise failed and the comparison is quietly
        // weaker than the operator believes, which is precisely the shape of the defect being hunted.
        // So it FAILS CLOSED with a named escape (same pattern as `to-xml --allow-blind-types`,
        // FI-71) rather than warning — a warning competes with a success line and loses.
        var firstLayout = DeclaredMemoryLayout(firstRoot);
        var secondLayout = DeclaredMemoryLayout(secondRoot);
        var compareLayout = firstLayout is not null && secondLayout is not null;
        var layout = new MemoryLayoutObservation(firstLayout, secondLayout, compareLayout);

        if (!compareLayout && !(firstLayout is null && secondLayout is null) && !allowSilentLayout)
        {
            var silent = firstLayout is null ? firstPath : secondPath;
            var declared = firstLayout ?? secondLayout;
            return NotCompared(firstPath, secondPath, layout,
                $"only one document declares a MemoryLayout ('{declared}'); {silent} states none. The Normalizer " +
                "holds neither side to the other's value when one is silent, so the layout would go UNCOMPARED — " +
                "the exact blindness that let a Standard DB round-trip into an Optimized one with every check " +
                "green. Two TIA exports both declare one, so this means an input is not what the loop assumes. " +
                "Pass --allow-silent-layout if one side is deliberately converter output.");
        }

        var differences = new List<CompareDifference>();
        var strippedFirst = Normalizer.Strip(firstRoot, compareLayout);
        var strippedSecond = Normalizer.Strip(secondRoot, compareLayout);

        if (strippedFirst.Name != strippedSecond.Name)
        {
            differences.Add(new CompareDifference(
                DifferenceKind.RootElementDiffers,
                "/" + strippedFirst.Name.LocalName,
                strippedFirst.Name.ToString(),
                strippedSecond.Name.ToString()));
        }
        else
        {
            CompareElements(strippedFirst, strippedSecond, "/" + strippedFirst.Name.LocalName, differences);
        }

        var normalizerVerdict = Normalizer.AreSemanticallyEquivalent(first, second);
        if (differences.Count == 0 && !normalizerVerdict)
        {
            return NotCompared(firstPath, secondPath, layout,
                "the Normalizer reports these documents as NOT equivalent, but the difference walk could not " +
                "localize any element, attribute or text difference — so something differs that this walk does " +
                "not model (an XML comment or processing instruction is the known candidate). Reporting a pass " +
                "here would be a false green; inspect the two files directly.");
        }

        return new CompareReport(
            firstPath,
            secondPath,
            differences.Count == 0 ? CompareStatus.Equivalent : CompareStatus.Differs,
            differences,
            layout);
    }

    // ------------------------------------------------------------------------------ the walk

    private static void CompareElements(XElement first, XElement second, string path, List<CompareDifference> differences)
    {
        CompareAttributes(first, second, path, differences);

        // Children are paired BY NAME then by position within that name, rather than by raw
        // position: an element inserted or removed in the middle would otherwise misalign every
        // sibling after it and report a whole tail of spurious differences. Within a name, position
        // is meaningful — the Normalizer has already sorted the two orderless containers
        // (Wires/Parts and a Wire's own endpoints) by content, and everywhere else SimaticML order
        // is genuinely positional (Section members, Symbol components).
        var firstChildren = first.Elements().ToList();
        var secondChildren = second.Elements().ToList();

        var names = firstChildren.Select(e => e.Name)
            .Concat(secondChildren.Select(e => e.Name))
            .Distinct()
            .ToList();

        foreach (var name in names)
        {
            var mine = firstChildren.Where(e => e.Name == name).ToList();
            var theirs = secondChildren.Where(e => e.Name == name).ToList();
            var count = Math.Max(mine.Count, theirs.Count);
            var indexed = count > 1;

            for (var i = 0; i < count; i++)
            {
                var childPath = path + "/" + name.LocalName + (indexed ? $"[{i + 1}]" : string.Empty);

                if (i >= theirs.Count)
                {
                    differences.Add(new CompareDifference(DifferenceKind.ElementMissing, childPath, Render(mine[i]), null));
                }
                else if (i >= mine.Count)
                {
                    differences.Add(new CompareDifference(DifferenceKind.ElementAdded, childPath, null, Render(theirs[i])));
                }
                else
                {
                    CompareElements(mine[i], theirs[i], childPath, differences);
                }
            }
        }

        // Text is compared only where both sides are leaves. Where one has children and the other
        // does not, every child is already reported added/missing above, and adding a text
        // difference on top would double-report one change.
        if (!first.HasElements && !second.HasElements && first.Value != second.Value)
        {
            differences.Add(new CompareDifference(DifferenceKind.ValueDiffers, path, first.Value, second.Value));
        }
    }

    private static void CompareAttributes(XElement first, XElement second, string path, List<CompareDifference> differences)
    {
        var mine = first.Attributes().ToDictionary(a => a.Name, a => a.Value);
        var theirs = second.Attributes().ToDictionary(a => a.Name, a => a.Value);

        foreach (var name in mine.Keys.Concat(theirs.Keys).Distinct().OrderBy(n => n.ToString(), StringComparer.Ordinal))
        {
            var attributePath = path + "/@" + name.LocalName;
            var inFirst = mine.TryGetValue(name, out var firstValue);
            var inSecond = theirs.TryGetValue(name, out var secondValue);

            if (inFirst && !inSecond)
            {
                differences.Add(new CompareDifference(DifferenceKind.AttributeMissing, attributePath, firstValue, null));
            }
            else if (!inFirst && inSecond)
            {
                differences.Add(new CompareDifference(DifferenceKind.AttributeAdded, attributePath, null, secondValue));
            }
            else if (firstValue != secondValue)
            {
                differences.Add(new CompareDifference(DifferenceKind.AttributeDiffers, attributePath, firstValue, secondValue));
            }
        }
    }

    private static string Render(XElement element) => element.ToString(SaveOptions.DisableFormatting);

    // ------------------------------------------------------------------------------ loading

    private static bool TryLoad(string path, out XDocument? document, out string? error)
    {
        document = null;
        error = null;

        if (!File.Exists(path))
        {
            error = $"file not found: {path}";
            return false;
        }

        try
        {
            document = XDocument.Load(path);
        }
        catch (XmlException ex)
        {
            error = $"{path} is not parseable XML: {ex.Message}";
            return false;
        }
        catch (IOException ex)
        {
            error = $"{path} could not be read: {ex.Message}";
            return false;
        }

        if (document.Root is null)
        {
            error = $"{path} has no root element — there is nothing to compare.";
            return false;
        }

        // FI-44, "empty is not clean". Two files that parse as XML but carry no SimaticML object at
        // all would walk to zero differences and exit 0 — a comparison of nothing, reported as a
        // pass. Every SimaticML export carries an SW.* object (SW.Blocks.*, SW.Types.*, SW.Tags.*).
        if (!document.Root.DescendantsAndSelf().Any(e => e.Name.LocalName.StartsWith("SW.", StringComparison.Ordinal)))
        {
            error = $"{path} contains no SimaticML object element (SW.Blocks.* / SW.Types.* / SW.Tags.*) — " +
                    "it is not a block export, and comparing it would compare nothing.";
            document = null;
            return false;
        }

        return true;
    }

    private static bool SameFile(string first, string second)
    {
        try
        {
            return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // An unusable path is caught properly by TryLoad's file-not-found, with a better message.
            return false;
        }
    }

    private static string? DeclaredMemoryLayout(XElement root) =>
        root.DescendantsAndSelf()
            .FirstOrDefault(e => e.Name.LocalName == BlockMemoryLayout.ElementName)
            ?.Value;

    private static CompareReport NotCompared(string first, string second, string detail) =>
        NotCompared(first, second, new MemoryLayoutObservation(null, null, false), detail);

    private static CompareReport NotCompared(string first, string second, MemoryLayoutObservation layout, string detail) =>
        new(first, second, CompareStatus.NotCompared, Array.Empty<CompareDifference>(), layout, detail);
}
