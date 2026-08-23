using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// TIA-SIDE EXPANSIONS OF INFORMATION THE CONVERTER DOES NOT AUTHOR (2026-08-23).
///
/// <para><b>The defect this closes.</b> `drift-check` on a real deployment reported
/// <c>5 drifted, 8 match</c> and blocked it. Every one of the five was a rendering difference:
/// TIA's export INLINES a typed member's members under it, regenerates an instance DB's member
/// list from its FB, and attaches a system-maintained <c>&lt;AttributeList&gt;</c> to a member
/// whose IR form carries none. The converter emits none of that. So the gate could not pass on
/// any corpus containing an FB with a UDT-typed static or a multi-instance — i.e. on most real
/// blocks. Its own successful verdict was unreachable: a CLOSED check.</para>
///
/// <para><b>And that is not merely noise.</b> Measured on the same corpus: shrinking the served
/// Modbus area from <c>WORD 1024</c> to <c>WORD 576</c> — a genuine semantic regression, and the
/// one this deployment exists to prevent — changed the report by NOT ONE CHARACTER, because the
/// block carrying the width was ALREADY permanently red from the structural false positive. An
/// object that always drifts is an object whose drift can no longer be read: the false positives
/// had DISABLED the gate on the five objects they landed on, one of which carried the width.</para>
///
/// <para><b>The precedent this is modelled on</b> is <see cref="Normalizer"/>'s MemoryLayout
/// treatment: a cross-document decision taken once, before stripping, and pushed down as a plan —
/// not a blanket entry in <c>VolatileElementNames</c>. Like MemoryLayout, every rule here is
/// ONE-SIDED-ABSENCE ONLY: if both documents declare the thing, it is compared, and the comparison
/// sharpens by itself as `.ir` files are re-derived from their exports.</para>
///
/// <para><b>The test each rule had to pass</b> — the criterion <see cref="Normalizer"/> already
/// states for <c>VolatileElementNames</c>, "what TIA itself regenerates regardless of input
/// content" — plus one more, because a one-sided absence is only equivalence if the absent side
/// genuinely cannot author it. A named type's member list is not authored HERE: it is a PROJECTION
/// of another object, which `drift-check` compares at its own definition site (or, when that object
/// is not in the corpus, reports by name as EXPORT-ONLY / not judged). So these rules add no
/// blindness that the tool does not already name. What they DO cost is written out per rule below,
/// because over-ignoring does not fail loudly — it produces a permanent green.</para>
///
/// <para><b>What is deliberately NOT here.</b> An inline <c>Struct</c> member's body: it has no
/// definition site anywhere else, so its absence is a converter gap and must drift. A GLOBAL DB's
/// empty member list: a global DB's members are authored, not derived. An FB's own empty Static
/// section: likewise authored.</para>
/// </summary>
public sealed class DerivedInterfacePlan
{
    /// <summary>
    /// The empty plan — nothing is dropped. Used wherever only one document is in hand
    /// (hashing, dumping a normalized form), because every rule here is a statement about what
    /// the OTHER document declares.
    /// </summary>
    public static readonly DerivedInterfacePlan Nothing = new(
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal),
        new HashSet<string>(StringComparer.Ordinal));

    private readonly HashSet<string> dropSectionsUnderMember;
    private readonly HashSet<string> dropAttributeListUnderMember;
    private readonly HashSet<string> dropMembersOfSection;

    private DerivedInterfacePlan(
        HashSet<string> dropSectionsUnderMember,
        HashSet<string> dropAttributeListUnderMember,
        HashSet<string> dropMembersOfSection)
    {
        this.dropSectionsUnderMember = dropSectionsUnderMember;
        this.dropAttributeListUnderMember = dropAttributeListUnderMember;
        this.dropMembersOfSection = dropMembersOfSection;
    }

    /// <summary>
    /// Decide, once, which derived expansions the two documents disagree about only by ABSENCE.
    /// Public so `converter compare` builds the identical plan — two derivations of one rule that
    /// can disagree is a defect class this project has already paid for (GateParityTests), and a
    /// `compare` that reported differences `drift-check` no longer counted would be exactly that.
    /// </summary>
    public static DerivedInterfacePlan For(XElement first, XElement second)
    {
        var firstMembers = CollectMembers(first);
        var secondMembers = CollectMembers(second);

        var dropSections = new HashSet<string>(StringComparer.Ordinal);
        var dropAttributeList = new HashSet<string>(StringComparer.Ordinal);

        foreach (var (path, a) in firstMembers)
        {
            if (!secondMembers.TryGetValue(path, out var b))
            {
                // The member exists on one side only. Say nothing: the Member element itself is
                // then a difference and must stay one. Planning a drop here is the one way these
                // rules could manufacture a false pass, so the presence test is the guard.
                continue;
            }

            // ---------------------------------------------------------------- RULE 1
            // TYPE EXPANSION. A member whose Datatype NAMES A TYPE — a quoted user type
            // (`"UDT_Valve"`, `"FB_Valve"`) or a versioned system/library type (`MB_SERVER` 5.3,
            // `TCON_IP_v4` 1.0, `TON_TIME` 1.0) — carries, in a TIA export, a <Sections> child
            // holding that type's whole member list. The converter emits the reference and stops.
            //
            // Equivalent because the expansion is a COPY of an object that is itself in the
            // comparison, and because TIA regenerates it from the type on every export whatever
            // was imported: no edit to THIS .ir can change it and no edit to it can survive.
            //
            // 🔴 BLIND TO: a member declared bare in the .ir whose type expansion in the
            // controller is not what the type says it should be. That divergence is only visible
            // at the TYPE's own object — `UDT_Valve.ir` vs `UDT_Valve.xml` — so it is caught there
            // if the type is in the corpus, and reported as EXPORT-ONLY / not judged if it is not.
            // Also blind to a member whose .ir DOES inline a body while the export carries none;
            // that has never been observed (TIA always expands) and the rule is symmetric only
            // because an asymmetric one would be a claim about TIA nobody has measured.
            if (a.HasSections != b.HasSections && (a.HasSections ? a : b).DeclaresNamedType)
            {
                dropSections.Add(path);
            }

            // ---------------------------------------------------------------- RULE 2
            // SYSTEM-MAINTAINED ATTRIBUTE LIST. TIA attaches
            // ExternalAccessible/ExternalVisible/ExternalWritable/SetPoint to a typed member, every
            // one marked SystemDefined="true". The `BAREPARAM` IR form emits no <AttributeList> at
            // all, and that is the form that IMPORTS — so the .ir has no way to state these and
            // could not be corrected into stating them.
            //
            // Narrowed three ways, each on measured evidence:
            //   - ALL children must be BooleanAttribute with SystemDefined="true". One author-set
            //     attribute and the whole list is compared.
            //   - The member must declare a NAMED TYPE. A plain `Bool` losing its AttributeList
            //     still drifts.
            //   - One-sided only.
            //
            // 🔴 BLIND TO: SetPoint / External* on a BAREPARAM'd typed member. This is a REAL loss
            // and worth stating plainly: SystemDefined="true" does NOT mean "TIA chose the value" —
            // measured on a real instance DB, an author-set `SETPOINT` reads back as
            // `<BooleanAttribute Name="SetPoint" SystemDefined="true">true</BooleanAttribute>`,
            // identical in form to a system-chosen false. So the marker says "TIA maintains this
            // slot", not "TIA decided it". What makes the drop defensible is not the marker but the
            // absence: the .ir side emitted no list, and the IR shape that emitted none cannot
            // emit one. If `BAREPARAM` ever learns to carry SETPOINT, this rule must be narrowed.
            if (a.HasSystemDefinedOnlyAttributeList != b.HasSystemDefinedOnlyAttributeList
                && a.HasAttributeList != b.HasAttributeList
                && (a.HasAttributeList ? a : b).DeclaresNamedType)
            {
                dropAttributeList.Add(path);
            }
        }

        // ---------------------------------------------------------------------- RULE 3
        // AN INSTANCE DB'S MEMBER LIST IS ITS FB'S INTERFACE. TIA regenerates it at import — that
        // is exactly what `openness-cli create-instance-db` relies on — so an `.ir` may legitimately
        // declare `INSTANCEOF <FB>` and an EMPTY `MEMBERS`. Restricted to a TOP-LEVEL interface
        // section of an SW.Blocks.InstanceDB on BOTH sides, and only when one side's section is
        // empty and the other's is not.
        //
        // 🔴 BLIND TO: the whole member list of an instance DB whose .ir declares none — including
        // the case where the controller's instance DB was built from a DIFFERENT FB than the one
        // the .ir names. That last one is narrower than it sounds: `InstanceOfName` is a plain
        // element and is still compared, so the FB the DB claims to instantiate is checked; what is
        // not checked is TIA's own derivation from it. An `.ir` that DOES declare its members (the
        // corpus has both styles) is compared member by member as before — same self-sharpening
        // property as the MemoryLayout rule.
        var dropSectionMembers = new HashSet<string>(StringComparer.Ordinal);
        if (IsInstanceDb(first) && IsInstanceDb(second))
        {
            var firstSections = CollectTopLevelSections(first);
            var secondSections = CollectTopLevelSections(second);
            foreach (var (path, hasMembers) in firstSections)
            {
                if (secondSections.TryGetValue(path, out var otherHasMembers) && hasMembers != otherHasMembers)
                {
                    dropSectionMembers.Add(path);
                }
            }
        }

        return new DerivedInterfacePlan(dropSections, dropAttributeList, dropSectionMembers);
    }

    /// <summary>
    /// Whether <paramref name="child"/> is a derived expansion this plan drops. Takes the PARENT
    /// (and the parent's interface path) because every rule is about what a Member or Section
    /// carries, not about the child's own name.
    /// </summary>
    internal bool Drops(XElement parent, string parentPath, XElement child)
    {
        if (dropSectionsUnderMember.Count == 0
            && dropAttributeListUnderMember.Count == 0
            && dropMembersOfSection.Count == 0)
        {
            return false;
        }

        if (parent.Name.LocalName == "Member")
        {
            return (child.Name.LocalName == "Sections" && dropSectionsUnderMember.Contains(parentPath))
                || (child.Name.LocalName == "AttributeList" && dropAttributeListUnderMember.Contains(parentPath));
        }

        return parent.Name.LocalName == "Section"
            && child.Name.LocalName == "Member"
            && dropMembersOfSection.Contains(parentPath);
    }

    // ------------------------------------------------------------------------------ collection

    private readonly record struct MemberFacts(
        bool HasSections,
        bool HasAttributeList,
        bool HasSystemDefinedOnlyAttributeList,
        bool DeclaresNamedType);

    private static Dictionary<string, MemberFacts> CollectMembers(XElement root)
    {
        var facts = new Dictionary<string, MemberFacts>(StringComparer.Ordinal);
        Walk(root, InterfacePath.Root);
        return facts;

        void Walk(XElement element, string path)
        {
            foreach (var child in element.Elements())
            {
                var childPath = InterfacePath.Extend(path, child);
                if (child.Name.LocalName == "Member")
                {
                    var attributeList = child.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");

                    // Member names are unique within a section, so the Name-derived path is an
                    // identity, not a position — deliberately NOT an index. An index would shift
                    // for every member after an inserted or deleted one and could align two
                    // unrelated members, which is the one way a drop could hide a real difference.
                    // Duplicate keys cannot arise from valid SimaticML; if one ever did, the LAST
                    // wins and the pair simply fails to plan, which errs toward comparing.
                    facts[childPath] = new MemberFacts(
                        HasSections: child.Elements().Any(e => e.Name.LocalName == "Sections"),
                        HasAttributeList: attributeList is not null,
                        HasSystemDefinedOnlyAttributeList: attributeList is not null && IsSystemDefinedOnly(attributeList),
                        DeclaresNamedType: DeclaresNamedType(child));
                }

                Walk(child, childPath);
            }
        }
    }

    private static Dictionary<string, bool> CollectTopLevelSections(XElement root)
    {
        var sections = new Dictionary<string, bool>(StringComparer.Ordinal);
        Walk(root, InterfacePath.Root);
        return sections;

        void Walk(XElement element, string path)
        {
            foreach (var child in element.Elements())
            {
                var childPath = InterfacePath.Extend(path, child);

                // TOP-LEVEL ONLY: exactly one path segment. A section nested inside a typed
                // member's expansion is already covered by rule 1, and widening rule 3 to reach it
                // would make an instance DB's authored nesting droppable too.
                if (child.Name.LocalName == "Section" && InterfacePath.SegmentCount(childPath) == 1)
                {
                    sections[childPath] = child.Elements().Any(e => e.Name.LocalName == "Member");
                }

                Walk(child, childPath);
            }
        }
    }

    private static bool IsInstanceDb(XElement root) =>
        root.DescendantsAndSelf().Any(e => e.Name.LocalName == "SW.Blocks.InstanceDB");

    // A Datatype that names a type declared somewhere else: quoted for a project type
    // (`"UDT_Valve"` — XLinq hands back the unescaped value, so the quote characters are real), or
    // carrying a Version for a system/library type (`MB_SERVER` 5.3). `Struct`, `Bool`,
    // `Array[0..3] of Struct` and `String[254]` have neither and are therefore never eligible —
    // which is the point: an inline Struct's body has no other definition site.
    private static bool DeclaresNamedType(XElement member) =>
        member.Attribute("Version") is not null
        || ((string?)member.Attribute("Datatype"))?.Contains('"') == true;

    private static bool IsSystemDefinedOnly(XElement attributeList) =>
        attributeList.HasElements
        && attributeList.Elements().All(a =>
            a.Name.LocalName == "BooleanAttribute"
            && string.Equals((string?)a.Attribute("SystemDefined"), "true", StringComparison.Ordinal));
}

/// <summary>
/// The identity of a Member or Section within an interface: the chain of its own and its ancestors'
/// <c>Name</c> attributes. Order-independent by construction, so two documents that agree on WHICH
/// members exist agree on these keys even when one of them omits a derived expansion in between.
/// </summary>
internal static class InterfacePath
{
    internal const string Root = "";

    internal static string Extend(string path, XElement element)
    {
        if ((string?)element.Attribute("Name") is not string name)
        {
            return path;
        }

        return element.Name.LocalName switch
        {
            "Section" => path + "/S:" + name,
            "Member" => path + "/M:" + name,
            _ => path,
        };
    }

    internal static int SegmentCount(string path) => path.Count(c => c == '/');
}

/// <summary>
/// TIA RE-RENDERS A REAL LITERAL AND THE CONVERTER PASSES THE AUTHOR'S TEXT THROUGH: `0.10` in the
/// `.ir` comes back as `0.1` from TIA. Measured on a real FB, and it is the only VALUE-level
/// difference among the five objects that blocked the deployment.
///
/// <para><b>Why this is a canonicalization and not an ignore, which is the whole argument.</b> The
/// rest of <see cref="DerivedInterfacePlan"/> DROPS content, and every drop is a class of real
/// divergence the gate can no longer see. This drops nothing. `0.10` and `0.1` are the same decimal
/// number written two ways; `0.1` and `0.2` are not, and stay different. Its blind spot is empty —
/// which is exactly the property <see cref="Normalizer"/>'s Access/Part content-keys have, and the
/// reason those are rewrites rather than strips.</para>
///
/// <para><b>Type-gated, so it cannot reach anything but a number.</b> Only a
/// <c>&lt;ConstantValue&gt;</c> whose sibling <c>&lt;ConstantType&gt;</c> says Real/LReal, or a
/// <c>&lt;StartValue&gt;</c> whose Member's <c>Datatype</c> says Real/LReal. A Time literal
/// (`T#200MS`), a quoted string, a hex or binary literal, an Int, and an untyped constant are all
/// out of reach. `StartValue` is included by ARGUMENT rather than by measurement — it is the same
/// literal in the same document under the same type gate — and that widening is defensible here
/// precisely because a value-preserving rewrite cannot hide a difference the way an ignore can.</para>
///
/// <para><b>Trailing fractional zeros only.</b> Not a parse-and-compare: parsing to `double` would
/// equate two LInt literals that differ beyond 53 bits, and `1e10` vs `10000000000` is not a
/// difference anyone has measured. Stripping trailing zeros after a decimal point is provably
/// value-preserving over the decimals, and it fails closed on every form it does not match. One
/// fractional digit is always kept, so `1.0` never becomes `1` — that would equate a Real literal
/// with an Int one, which is a genuine type difference.</para>
/// </summary>
internal static class NumericLiteral
{
    private static readonly Regex PlainDecimal = new(@"^[+-]?[0-9]+\.[0-9]+$", RegexOptions.Compiled);

    internal static string Canonicalize(XElement element)
    {
        var text = element.Value;

        var isFloatLiteral = element.Name.LocalName switch
        {
            "ConstantValue" => IsFloatingType(element.Parent?.Elements()
                .FirstOrDefault(e => e.Name.LocalName == "ConstantType")?.Value),
            "StartValue" => IsFloatingType((string?)element.Parent?.Attribute("Datatype")),
            _ => false,
        };

        if (!isFloatLiteral || !PlainDecimal.IsMatch(text))
        {
            return text;
        }

        var trimmed = text.TrimEnd('0');
        return trimmed.EndsWith('.') ? trimmed + "0" : trimmed;
    }

    private static bool IsFloatingType(string? datatype) =>
        string.Equals(datatype, "Real", StringComparison.OrdinalIgnoreCase)
        || string.Equals(datatype, "LReal", StringComparison.OrdinalIgnoreCase);
}
