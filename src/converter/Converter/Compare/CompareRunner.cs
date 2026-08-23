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

        // The derived-interface plan is built ONCE from both documents and handed to both Strip
        // calls, exactly as Normalizer.AreSemanticallyEquivalent does it. Not an optimisation: the
        // plan is a cross-document decision (which one-sided TIA expansions are absences rather
        // than differences), so a per-side plan would be a second derivation of the same rule, and
        // this walk is cross-checked against AreSemanticallyEquivalent a few lines below — the two
        // must be looking at the same trees or that cross-check starts firing on itself.
        var derived = DerivedInterfacePlan.For(firstRoot, secondRoot);
        var strippedFirst = Normalizer.Strip(firstRoot, compareLayout, derived);
        var strippedSecond = Normalizer.Strip(secondRoot, compareLayout, derived);

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

            // A wire reversal survives normalization but does not survive it LEGIBLY under the
            // positional pairing below — see TryCompareWiresAsDirection. Only that shape is
            // intercepted; everything else falls through unchanged.
            if (first.Name.LocalName == "Wires" && name.LocalName == "Wire"
                && TryCompareWiresAsDirection(mine, theirs, path, differences))
            {
                continue;
            }

            // An interface's <Section> siblings are identified by their Name attribute, not by
            // position — see TryCompareSectionsByName. Same interception shape as wires above.
            if (name.LocalName == "Section" && TryCompareSectionsByName(mine, theirs, path, differences))
            {
                continue;
            }

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

    // ------------------------------------------------------------ wires: direction awareness

    /// <summary>
    /// Reports a producer/consumer REVERSAL as one finding that says so, instead of the
    /// per-attribute noise the generic walk produces for it. Returns false — changing nothing —
    /// whenever the difference is not confidently a pure reversal, leaving the generic walk to
    /// report it exactly as before.
    ///
    /// <para><b>Why the generic walk mis-reads this.</b> A <c>&lt;Wire&gt;</c>'s FIRST endpoint is
    /// its producer and the rest are its consumers; nothing else in a SimaticML document encodes
    /// direction (2026-08-12: 106 <c>(part, port)</c> pairs across 34 real exports, zero appearing
    /// in both slots). The Normalizer therefore pins endpoint 0 and sorts only the tail, so a
    /// reversal SURVIVES to this walk. But the Normalizer also sorts <c>&lt;Wires&gt;</c> BY each
    /// wire's rendered content — and a reversal changes that content, so the flipped wire moves to
    /// a different index. The positional pairing then compares two DIFFERENT wires against each
    /// other. Measured on a single flipped CALL-output wire: FOUR <c>ATTR-DIFFERS</c> lines in
    /// which two wires appear to swap their port names and their operands. Every line of that is
    /// true and a reader can act on it, but it reads like a REWIRING — a materially different
    /// defect to go hunting for than a direction reversal.</para>
    ///
    /// <para><b>Why the classification is all-or-nothing.</b> It fires only when the two containers
    /// hold the same wires AS ENDPOINT SETS and differ solely in which endpoint is first. Any other
    /// edit — an endpoint changed, a wire added or removed, an attribute retyped — fails the
    /// multiset test and falls straight through. That is deliberate and it is the whole safety
    /// argument: detection is never weakened to improve the message, because an unexplained real
    /// difference beats a confidently mislabelled one. The cost is that a reversal arriving
    /// ALONGSIDE another change in the same network still reports as per-attribute noise.</para>
    ///
    /// <para>Given equal endpoint multisets, a surviving order difference can only be at endpoint 0
    /// — the tail is already sorted — so "same set, different order" IS "different producer". That
    /// is asserted per pair rather than assumed: a pair that differs while its producers match is a
    /// shape this rule does not describe, and abandons the classification for the whole
    /// container.</para>
    /// </summary>
    private static bool TryCompareWiresAsDirection(
        List<XElement> mine, List<XElement> theirs, string path, List<CompareDifference> differences)
    {
        if (mine.Count == 0 || mine.Count != theirs.Count)
        {
            return false;
        }

        var mineByKey = mine.GroupBy(UnorderedWireKey).ToDictionary(g => g.Key, g => g.ToList());
        var theirsByKey = theirs.GroupBy(UnorderedWireKey).ToDictionary(g => g.Key, g => g.ToList());

        if (mineByKey.Count != theirsByKey.Count
            || mineByKey.Any(kv => !theirsByKey.TryGetValue(kv.Key, out var other) || other.Count != kv.Value.Count))
        {
            // Not the same wires as endpoint sets, so the difference is not purely one of direction.
            return false;
        }

        // XElement does not override Equals/GetHashCode, so this keys on reference identity — which
        // is what is wanted: two wires can render identically and still be distinct elements.
        var indexOf = new Dictionary<XElement, int>();
        for (var i = 0; i < mine.Count; i++)
        {
            indexOf[mine[i]] = i;
        }

        var found = new List<(int Index, CompareDifference Difference)>();

        foreach (var (key, mineGroup) in mineByKey)
        {
            var theirsGroup = theirsByKey[key];
            for (var i = 0; i < mineGroup.Count; i++)
            {
                var before = mineGroup[i];
                var after = theirsGroup[i];
                if (Render(before) == Render(after))
                {
                    continue;
                }

                var producerBefore = before.Elements().FirstOrDefault();
                var producerAfter = after.Elements().FirstOrDefault();
                if (producerBefore is null || producerAfter is null
                    || Render(producerBefore) == Render(producerAfter))
                {
                    return false;
                }

                var index = indexOf[before];
                found.Add((index, new CompareDifference(
                    DifferenceKind.WireDirectionDiffers,
                    path + "/Wire" + (mine.Count > 1 ? $"[{index + 1}]" : string.Empty),
                    DescribeWire(before),
                    DescribeWire(after))));
            }
        }

        if (found.Count == 0)
        {
            // Equal multisets and every pair identical: the containers match. Fall through so the
            // ordinary walk says so itself rather than this returning a pass on its own authority.
            return false;
        }

        differences.AddRange(found.OrderBy(f => f.Index).Select(f => f.Difference));
        return true;
    }

    // -------------------------------------------------------- interface sections: pair by NAME

    /// <summary>
    /// Pairs <c>&lt;Section&gt;</c> siblings by their <c>Name</c> attribute instead of by position, so
    /// one section present on one side and absent on the other reports as ONE difference rather than
    /// misaligning every section after it.
    ///
    /// <para><b>Why the generic walk cannot do this.</b> It pairs children by ELEMENT NAME and then by
    /// position within that name — which is right nearly everywhere, because SimaticML's repeated
    /// siblings (<c>Member</c>, <c>Component</c>) are genuinely ordered and carry no identity of their
    /// own. An interface's sections are the exception: every <c>&lt;Section&gt;</c> shares one element
    /// name and is identified by an attribute from a closed vocabulary
    /// (Input/Output/InOut/Static/Temp/Constant/Return/None). Position therefore pairs Input with
    /// Input only by luck, and stops doing so the moment either side omits an optional section.</para>
    ///
    /// <para><b>Measured, 2026-08-13.</b> <c>DbSourceWriter</c> emitted Input, Output, Static for an
    /// instance DB where TIA emits Input, Output, InOut, Static. The single missing EMPTY element slid
    /// <c>Static</c> into <c>InOut</c>'s slot, and the walk then compared our whole Static section
    /// against TIA's empty InOut: *** 26 differences on iDB_MotorFwdRevSystem_Shredder ***, one
    /// ATTR-DIFFERS plus 24 phantom added members plus one phantom missing section, for a block whose
    /// committed export was independently confirmed current. By name it is one ELEMENT-MISSING that
    /// names the section — which is the finding a reader can act on.</para>
    ///
    /// <para><b>This is diagnosis, not forgiveness.</b> A missing section is still a difference and
    /// still fails the comparison; <see cref="Normalizer.AreSemanticallyEquivalent"/> is untouched and
    /// still holds the two documents to <c>XNode.DeepEquals</c>. Teaching the comparator that an
    /// absent section equals an empty one would have made the writer defect invisible instead of
    /// legible — the shape that let the <c>MemoryLayout</c> hole survive a green <c>drift-check</c>.
    /// Section ORDER is content too, so a pure reorder is reported in its own right
    /// (<see cref="DifferenceKind.SectionOrderDiffers"/>) rather than silently absorbed by the
    /// name-keyed pairing.</para>
    ///
    /// <para>Returns false — changing nothing — unless every section on both sides carries a distinct
    /// non-empty <c>Name</c>. A repeated or unnamed section is a shape this rule does not describe, and
    /// the positional walk handles it exactly as before.</para>
    /// </summary>
    private static bool TryCompareSectionsByName(
        List<XElement> mine, List<XElement> theirs, string path, List<CompareDifference> differences)
    {
        if (!TryKeyByName(mine, out var mineByName) || !TryKeyByName(theirs, out var theirsByName))
        {
            return false;
        }

        // First document's order, then any section only the second has, in its own order — so the
        // report reads in the order a person opening the first file would meet them.
        var names = mine.Select(SectionName)
            .Concat(theirs.Select(SectionName).Where(n => !mineByName.ContainsKey(n)))
            .ToList();

        foreach (var name in names)
        {
            var sectionPath = $"{path}/Section[@Name='{name}']";
            var inFirst = mineByName.TryGetValue(name, out var first);
            var inSecond = theirsByName.TryGetValue(name, out var second);

            if (inFirst && !inSecond)
            {
                differences.Add(new CompareDifference(DifferenceKind.ElementMissing, sectionPath, Render(first!), null));
            }
            else if (!inFirst && inSecond)
            {
                differences.Add(new CompareDifference(DifferenceKind.ElementAdded, sectionPath, null, Render(second!)));
            }
            else
            {
                CompareElements(first!, second!, sectionPath, differences);
            }
        }

        // The sections both sides share, in each side's own document order. Pairing by name is blind
        // to a reorder by construction, so it is asserted here instead of assumed away.
        var commonInFirstOrder = mine.Select(SectionName).Where(theirsByName.ContainsKey).ToList();
        var commonInSecondOrder = theirs.Select(SectionName).Where(mineByName.ContainsKey).ToList();
        if (!commonInFirstOrder.SequenceEqual(commonInSecondOrder, StringComparer.Ordinal))
        {
            differences.Add(new CompareDifference(
                DifferenceKind.SectionOrderDiffers,
                path + "/Section",
                string.Join(", ", commonInFirstOrder),
                string.Join(", ", commonInSecondOrder)));
        }

        return true;
    }

    private static string SectionName(XElement section) => (string?)section.Attribute("Name") ?? string.Empty;

    private static bool TryKeyByName(List<XElement> sections, out Dictionary<string, XElement> byName)
    {
        byName = new Dictionary<string, XElement>(StringComparer.Ordinal);
        foreach (var section in sections)
        {
            var name = SectionName(section);
            if (name.Length == 0 || !byName.TryAdd(name, section))
            {
                byName = new Dictionary<string, XElement>(StringComparer.Ordinal);
                return false;
            }
        }

        return true;
    }

    /// <summary>The wire's own attributes plus its endpoint SET — identical for a wire and its reversal.</summary>
    private static string UnorderedWireKey(XElement wire) =>
        "{" + string.Join(",", wire.Attributes()
                  .Select(a => a.Name.LocalName + "=" + a.Value)
                  .OrderBy(s => s, StringComparer.Ordinal)) + "}["
            + string.Join("|", wire.Elements().Select(Render).OrderBy(s => s, StringComparer.Ordinal)) + "]";

    private static string DescribeWire(XElement wire)
    {
        var endpoints = wire.Elements().ToList();
        if (endpoints.Count == 0)
        {
            return "(no endpoints)";
        }

        var producer = DescribeEndpoint(endpoints[0]);
        var consumers = endpoints.Skip(1).Select(DescribeEndpoint).ToList();
        return consumers.Count == 0
            ? producer + " drives nothing"
            : producer + " drives " + string.Join(", ", consumers);
    }

    /// <summary>
    /// A human-readable endpoint. Deliberately does NOT print the normalized <c>UId</c>: for an
    /// <c>IdentCon</c> it is the Access content key (which embeds a whole <c>&lt;Symbol&gt;</c>
    /// element) and for a <c>NameCon</c> it is a topology hash — neither reads as anything to an
    /// operator, and printing them is what made the per-attribute output unreadable in the first
    /// place. The port name and the operand path are what actually locate the wire.
    /// </summary>
    private static string DescribeEndpoint(XElement endpoint) => endpoint.Name.LocalName switch
    {
        "Powerrail" => "the power rail",
        "OpenCon" => "an open connector",
        "NameCon" => (string?)endpoint.Attribute("Name") is string port && port.Length > 0
            ? $"port '{port}'"
            : "an unnamed port",
        "IdentCon" => DescribeAccessKey((string?)endpoint.Attribute("UId")),
        _ => endpoint.Name.LocalName,
    };

    /// <summary>
    /// Unpacks a Normalizer Access content key — <c>const:&lt;value&gt;</c> or
    /// <c>tag:&lt;scope&gt;:&lt;Symbol xml&gt;</c> — back into something an operator recognises.
    /// Any shape it does not recognise degrades to "an operand" rather than guessing.
    /// </summary>
    private static string DescribeAccessKey(string? key)
    {
        if (key is null)
        {
            return "an operand";
        }

        if (key.StartsWith("const:", StringComparison.Ordinal))
        {
            return $"the literal {key["const:".Length..]}";
        }

        var parts = key.Split(':', 3);
        if (parts.Length == 3 && parts[0] == "tag")
        {
            try
            {
                var components = XElement.Parse(parts[2])
                    .Elements()
                    .Where(e => e.Name.LocalName == "Component")
                    .Select(e => (string?)e.Attribute("Name"))
                    .Where(n => !string.IsNullOrEmpty(n))
                    .ToList();

                if (components.Count > 0)
                {
                    return $"operand '{string.Join(".", components)}'";
                }
            }
            catch (XmlException)
            {
                // Fall through — an unparseable key is reported as an unnamed operand, never guessed at.
            }
        }

        return "an operand";
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
