using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// Strips known-volatile elements before comparing two exports for semantic equivalence
/// (docs/08-testing-strategy.md Layer 1: SimaticML'' == SimaticML after normalization).
/// Narrower than originally anticipated: the IR sidecar (ADR-0001) is designed to preserve
/// exact source UIds on regeneration, so what's left to normalize is only what TIA itself
/// regenerates regardless of input content.
///
/// Ported into the converter (2026-07-19, ADR-0005 follow-on) so `to-ir --no-sidecar` can
/// self-verify that a block's derived form is semantically equivalent to its source export
/// before omitting the stored sidecar — the same check the golden harness trusts, now a single
/// shared implementation (the golden test project references this copy).
/// </summary>
public static class Normalizer
{
    /// <summary>
    /// Local element names TIA regenerates on every compile/export regardless of source
    /// content. Each one here is a claim that the difference is benign
    /// (docs/08-testing-strategy.md's own requirement) — informed by the PlcBlock properties
    /// reflected on in docs/notes/openness-api-surface-v20.md, but **unverified against a real
    /// re-export** until the live proof (S1 walking-skeleton plan step 4) actually runs; may
    /// need extending once real output is seen.
    /// </summary>
    private static readonly HashSet<string> VolatileElementNames = new(StringComparer.Ordinal)
    {
        "CreationDate",
        "ModifiedDate",
        "CompileDate",
        "CodeModifiedDate",
        "InterfaceModifiedDate",
        "StructureModified",
        "ParameterModified",
        "HeaderVersion",

        // Confirmed real, 2026-07-10 (first live reference-project round-trip): a whole-export
        // envelope wrapping TIA/Openness/product version info — not written by our own
        // BlockSourceWriter at all (single-block regeneration never produces one), and not
        // semantic content either way.
        "DocumentInfo",

        // Block-level configuration TIA assigns sensible defaults for on Import() regardless of
        // source content — none of these are written by BlockSourceWriter, none affect this
        // converter slice's Contact/Coil network semantics. Confirmed real, 2026-07-10.
        // HeaderAuthor/HeaderFamily/HeaderName specifically: confirmed empty on every block seen
        // so far (spot-checked, not just assumed) — genuinely benign here, though a real,
        // non-empty value on some future block would be a metadata loss worth revisiting, not a
        // logic change.
        "AutoNumber",
        "HeaderAuthor",
        "HeaderFamily",
        "HeaderName",
        "IsIECCheckEnabled",
        "SetENOAutomatically",
        "UDABlockProperties",
        "UDAEnableTagReadback",

        // DB-specific block-level configuration — confirmed real, 2026-07-10, same reasoning as
        // the FC/FB set above (TIA-assigned defaults on Import(), not written by DbSourceWriter,
        // don't affect member semantics).
        "DBAccessibleFromOPCUA",
        "IsOnlyStoredInLoadMemory",
        "IsRetainMemResEnabled",
        "IsWriteProtectedInAS",
        "MemoryReserve",
    };

    /// <summary>
    /// Element names whose own "ID"/"UId" attribute is volatile.
    ///
    /// MultilingualText/MultilingualTextItem: confirmed real, 2026-07-10 — TIA assigns its own
    /// IDs on import, never preserving the synthetic ones BlockSourceWriter generates (100000+,
    /// "not believed to carry semantic meaning beyond 'must exist and be unique'" per its own
    /// comment).
    ///
    /// Wire: confirmed real, 2026-07-10, on the *first* live reference-project round-trip —
    /// contrary to the original assumption (a wire's UId is reassigned by TIA on every
    /// import/compile cycle, not preserved the way Part/Access UId is). The rail wire's UId
    /// changed from whatever the source/sidecar held to a fresh TIA-assigned one, and every
    /// other wire's UId shifted too, even though the *set* of connections (which Access/Part
    /// UIds each wire references) stayed exactly the same. A wire's real identity is its
    /// endpoint set, not its own UId — those referenced UIds are a different thing and remain
    /// significant (see IsVolatile / DifferingWireEndpoint test).
    /// </summary>
    private static readonly Dictionary<string, string> ElementsWithVolatileId = new(StringComparer.Ordinal)
    {
        ["MultilingualText"] = "ID",
        ["MultilingualTextItem"] = "ID",
        ["Wire"] = "UId",

        // An <Instance> (a timer/CALL Part's instance sub-element) and an <OpenCon> (a wire's open
        // endpoint, e.g. a timer's unused ET) each carry a UId that TIA reassigns on import, exactly
        // like Wire/Access/Part — surfaced 2026-07-18 by the parity harness (TimingAndCalls: a fresh
        // synthesis mints them from small numbers, the real export has larger ones). Neither is
        // referenced by UId from elsewhere: an Instance's identity is its Scope+Component path (kept),
        // an OpenCon is already described positionally as "OPEN" by the wire refinement — so a plain
        // strip is safe and only ever makes two topologically-identical graphs compare equal.
        ["Instance"] = "UId",
        ["OpenCon"] = "UId",

        // A CompileUnit's own block-scoped ID is volatile too — surfaced 2026-07-18 by the offline
        // synthesis-parity harness (SynthesisParityRunner). A real export numbers CompileUnits with
        // arbitrary block-scoped IDs (e.g. 3, 8); a freshly *synthesized* sidecar mints them from
        // the network number (1, 2). SynthesizerLiveCheck proves the synthesized block (IDs 1, 2)
        // imports and compiles unchanged, so the ID value carries no meaning TIA preserves — same
        // reassigned-on-import class as Wire/Access/Part/MultilingualText. Nothing within the block
        // references a CompileUnit by this ID (unlike Access, which IdentCon points at), so a plain
        // strip is enough; network identity for comparison is document position + content, not ID.
        ["SW.Blocks.CompileUnit"] = "ID",
    };

    /// <summary>
    /// MemoryLayout is compared as an OPTIONAL ASSERTION (2026-08-12): a difference between two
    /// documents that BOTH declare one is real and is reported; a document that declares none is
    /// stating no opinion and is not held to the other's value.
    ///
    /// It sat in <see cref="VolatileElementNames"/> until 2026-08-12 under the comment "block-level
    /// configuration TIA assigns sensible defaults for on Import()" — which made a layout change
    /// undetectable in either direction, so a real Standard DB could round-trip into an Optimized
    /// one with `drift-check` reporting *** MATCH ***. Un-ignoring it only became correct once the
    /// converter could EMIT the attribute (`BlockMemoryLayout`, same commit): before that, every
    /// real-export-vs-converter-output comparison in the project would have differed at once.
    ///
    /// Why "both must declare" rather than a plain strict compare: every `.ir` in the committed
    /// corpus predates the emit side, so it carries no layout while its paired export carries one.
    /// A strict compare would report ~30 blocks as drifted for the benign reason that the IR simply
    /// does not state a layout — the same wholesale-red outcome, just reached one step later.
    /// The comparison sharpens by itself as blocks are re-derived from their exports: an `.ir` that
    /// CAME FROM an export declares a layout, and from then on it is held to it.
    /// </summary>
    public static bool AreSemanticallyEquivalent(XDocument original, XDocument reExported)
    {
        if (original.Root is null || reExported.Root is null)
        {
            throw new InvalidOperationException("Cannot compare a document with no root element.");
        }

        var compareMemoryLayout = DeclaresMemoryLayout(original.Root) && DeclaresMemoryLayout(reExported.Root);
        var derived = DerivedInterfacePlan.For(original.Root, reExported.Root);
        return XNode.DeepEquals(
            Strip(original.Root, compareMemoryLayout, derived),
            Strip(reExported.Root, compareMemoryLayout, derived));
    }

    private static bool DeclaresMemoryLayout(XElement root) =>
        root.DescendantsAndSelf().Any(e => e.Name.LocalName == BlockMemoryLayout.ElementName);

    /// <summary>
    /// Single-document canonicalization (hashing, diffing, dumping a normalized form beside a
    /// failing comparison). Drops MemoryLayout, since with only one document in hand there is no
    /// other side to have declared one — <see cref="AreSemanticallyEquivalent"/> is the caller that
    /// knows whether the attribute is being compared and opts in. Same for the derived-interface
    /// plan: every one of its rules is a statement about what the OTHER document does or does not
    /// declare, so with one document in hand there is nothing to plan and nothing is dropped.
    /// </summary>
    public static XElement Strip(XElement element) => Strip(element, compareMemoryLayout: false);

    public static XElement Strip(XElement element, bool compareMemoryLayout) =>
        Strip(element, compareMemoryLayout, DerivedInterfacePlan.Nothing);

    public static XElement Strip(XElement element, bool compareMemoryLayout, DerivedInterfacePlan derived) =>
        Strip(element, BuildAccessContentKeyMap(element), new Dictionary<string, string>(),
            compareMemoryLayout, derived, InterfacePath.Root);

    // An Access element's own UId is volatile too — confirmed real, 2026-07-11 (TON grounding,
    // FC TimerSample): TIA reassigns Access UIds on its own Import()/Compile()/Export() cycle,
    // exactly parallel to the already-documented Wire UId finding (docs/notes/openness-quirks.md),
    // contrary to the earlier assumption that Part/Access UIds stay stable. Unlike Wire, an
    // Access is *referenced* elsewhere (every <IdentCon>), so a plain strip isn't enough — both
    // the Access element itself and every IdentCon pointing at it are rewritten to a
    // content-derived key (Scope+Symbol, or the literal constant value) instead of the raw
    // number, so two documents compare equal regardless of which arbitrary number TIA assigned
    // to which Access. Built per-document (each call to the public single-argument Strip), not
    // shared across both sides being compared — nothing requires the two maps to agree on
    // numbering, only that each document's own numbering resolves to the same content keys.
    private static Dictionary<string, string> BuildAccessContentKeyMap(XElement root)
    {
        var map = new Dictionary<string, string>();
        foreach (var access in root.DescendantsAndSelf().Where(e => e.Name.LocalName == "Access"))
        {
            if ((string?)access.Attribute("UId") is string uid)
            {
                map[uid] = AccessContentKey(access);
            }
        }

        return map;
    }

    private static string AccessContentKey(XElement access)
    {
        var scope = (string?)access.Attribute("Scope") ?? string.Empty;
        if (scope == "TypedConstant")
        {
            // Canonicalized, not raw. An Access's content key becomes its UId in the stripped tree
            // and is what every IdentCon pointing at it is rewritten to, so a key taken from the
            // raw literal text puts the re-rendered form BACK into the comparison after
            // NumericLiteral removed it — `0.10` and `0.1` would compare equal at the
            // <ConstantValue> and unequal at three UIds. Found by a synthetic fixture, not by the
            // real corpus: TIA writes a Real literal under Scope="LiteralConstant", which takes the
            // `tag:` branch below, so the measured case never reached this line.
            var value = access.Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue");
            return $"const:{(value is null ? string.Empty : NumericLiteral.Canonicalize(value))}";
        }

        // The full <Symbol> (component names plus any slice/array modifiers) so two Access
        // elements only compare equal when truly identical, not just same top-level path.
        var symbol = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
        return $"tag:{scope}:{symbol?.ToString(SaveOptions.DisableFormatting)}";
    }

    // A bare Part (Contact/Coil/TON/etc.) has no distinguishing content of its own the way Access
    // does via its own Symbol path — two Contacts in the same network can be byte-identical XML
    // except for UId. TIA reassigns Part UId on import/compile too (confirmed real 2026-07-14,
    // full export/convert/import/compile/re-export cycle against every SampleProject block — 7 of
    // 47 blocks affected), so a Part's real identity has to come from graph position: its own
    // content (kind, DisabledENO, Version, Instance path, etc.) plus which already-stable things
    // (Access content-keys, Powerrail, OpenCon) or other Parts it's wired to.
    //
    // Computed via iterative structural refinement (Weisfeiler-Leman-style color refinement):
    // start from each Part's own content key, then repeatedly fold in every wired neighbor's
    // *current* key into a new key, until the whole set of keys stops changing or a safety cap
    // (bounded by the standard 1-WL result that a partition can refine at most partCount-1 times
    // before stabilizing) is hit. Two Parts converge to the same final key only if truly
    // interchangeable throughout the whole network's topology, not just superficially alike —
    // and if they're *genuinely* symmetric (a true graph automorphism, no anchor distinguishes
    // them even in principle), swapping their identities produces an equivalent graph anyway, so
    // the comparison stays correct even without a fully unique key per Part in that edge case.
    private static Dictionary<string, string> BuildPartContentKeyMap(XElement flgNet, IReadOnlyDictionary<string, string> accessContentKeyByUId)
    {
        // "Part" and "Call" are both producers in <Parts> referenced by <NameCon UId="…">, so both
        // need topology-based UId normalization (a <Call>'s own UId is volatile too — TimingAndCalls,
        // 2026-07-18). "Call" is included here and rewritten alongside Part/NameCon in Strip; an
        // <Access> is handled separately (content-key), everything else in <Parts> stays as-is.
        var parts = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Parts")?.Elements()
            .Where(e => e.Name.LocalName is "Part" or "Call").ToList() ?? new List<XElement>();
        if (parts.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        var wires = flgNet.Elements().FirstOrDefault(e => e.Name.LocalName == "Wires")?.Elements()
            .Where(e => e.Name.LocalName == "Wire").ToList() ?? new List<XElement>();

        // For each Part UId: every (port name, other endpoints sharing that same wire) it
        // participates in — gathered once, reused unchanged every refinement round (only the
        // *resolved description* of each neighbor changes round to round, not the adjacency
        // itself).
        var portTouches = parts
            .Select(p => (string)p.Attribute("UId")!)
            .ToDictionary(uid => uid, _ => new List<(string Port, List<XElement> OtherEndpoints)>());

        foreach (var wire in wires)
        {
            var endpoints = wire.Elements().ToList();
            foreach (var endpoint in endpoints)
            {
                if (endpoint.Name.LocalName != "NameCon" || (string?)endpoint.Attribute("UId") is not string partUid
                    || !portTouches.TryGetValue(partUid, out var touches))
                {
                    continue;
                }

                var port = (string?)endpoint.Attribute("Name") ?? string.Empty;
                var others = endpoints.Where(e => !ReferenceEquals(e, endpoint)).ToList();
                touches.Add((port, others));
            }
        }

        // Each round's signature is hashed down to a compact, fixed-length digest before it
        // becomes the *next* round's neighbor-lookup input — real bug, found live 2026-07-14
        // (MotorStarter, big enough to hit it): carrying the full, ever-growing descriptive
        // string forward round to round embeds the entire previous signature as a substring of
        // the next one, so signature length grows multiplicatively with each round and overflows
        // Int32 (`string.Join` -> `ArgumentOutOfRangeException`, "minimumLength ... must be a
        // non-negative value") on a real network with enough Parts/rounds. Hashing keeps every
        // round's representation the same small size regardless of how much history it encodes —
        // this is how color refinement is meant to work (a compact per-round "color," not a
        // literal running concatenation), not an approximation of it.
        var signature = parts.ToDictionary(p => (string)p.Attribute("UId")!, p => Hash(PartOwnContentKey(p)));

        for (var round = 0; round < parts.Count; round++)
        {
            var next = new Dictionary<string, string>(signature.Count);
            foreach (var (uid, touches) in portTouches)
            {
                var portDescriptions = touches
                    .Select(t => $"{t.Port}=[{string.Join(",", t.OtherEndpoints.Select(e => DescribeEndpoint(e, accessContentKeyByUId, signature)).OrderBy(s => s, StringComparer.Ordinal))}]")
                    .OrderBy(s => s, StringComparer.Ordinal);
                next[uid] = Hash(signature[uid] + "||" + string.Join(";", portDescriptions));
            }

            var converged = next.All(kv => signature[kv.Key] == kv.Value);
            signature = next;
            if (converged)
            {
                break;
            }
        }

        return signature;
    }

    // Stable across processes/runs (unlike string.GetHashCode(), which .NET deliberately
    // randomizes per-process) — needed since AreSemanticallyEquivalent compares two independent
    // Strip() calls (potentially different process invocations) that must still agree on which
    // Parts are "the same" whenever they truly are.
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static string DescribeEndpoint(XElement endpoint, IReadOnlyDictionary<string, string> accessContentKeyByUId, IReadOnlyDictionary<string, string> currentPartSignature) =>
        endpoint.Name.LocalName switch
        {
            "Powerrail" => "RAIL",
            "OpenCon" => "OPEN",
            "IdentCon" when (string?)endpoint.Attribute("UId") is string accessUid && accessContentKeyByUId.TryGetValue(accessUid, out var accessKey)
                => $"ACCESS:{accessKey}",
            "NameCon" when (string?)endpoint.Attribute("UId") is string partUid && currentPartSignature.TryGetValue(partUid, out var partSig)
                => $"PART:{partSig}@{(string?)endpoint.Attribute("Name")}",
            _ => $"UNKNOWN:{endpoint.Name.LocalName}:{(string?)endpoint.Attribute("UId")}",
        };

    // A Part's own content, excluding *every* UId in its subtree (not just its own top-level
    // one) — not just the Part's own kind/attributes, but any child too (e.g. a TON/CALL/
    // Modbus_Master's own <Instance>). Instance's own UId isn't independently confirmed volatile
    // or stable either way, so it's excluded defensively rather than risking a false mismatch —
    // Instance's real identity is its Scope+Component path, mirroring Access's own reasoning,
    // not the arbitrary number next to it.
    private static string PartOwnContentKey(XElement part) => StripAllUIds(part).ToString(SaveOptions.DisableFormatting);

    private static XElement StripAllUIds(XElement element)
    {
        var attributes = element.Attributes().Where(a => a.Name.LocalName != "UId");
        var clone = new XElement(element.Name, attributes);
        foreach (var child in element.Elements())
        {
            clone.Add(StripAllUIds(child));
        }

        if (!element.HasElements)
        {
            clone.Value = element.Value;
        }

        return clone;
    }

    private static XElement Strip(
        XElement element,
        Dictionary<string, string> accessContentKeyByUId,
        Dictionary<string, string> partContentKeyByUId,
        bool compareMemoryLayout,
        DerivedInterfacePlan derived,
        string interfacePath)
    {
        // UId numbering restarts at the beginning of every network (each <FlgNet> is its own
        // numbering scope) — the content-key map must be rebuilt per network too, not flattened
        // across the whole document, or the same number ("22", "23", ...) reused in a different
        // network silently clobbers an unrelated entry. Caught live, 2026-07-11, comparing a
        // real 3-network export (FC TimerSample) — a single-network test fixture would never
        // have exposed this. Part's own map is rebuilt alongside Access's for the same reason —
        // and depends on Access's own map already being rebuilt first, since Part identity is
        // partly derived from which Access content-keys a Part is wired to.
        if (element.Name.LocalName == "FlgNet")
        {
            accessContentKeyByUId = BuildAccessContentKeyMap(element);
            partContentKeyByUId = BuildPartContentKeyMap(element, accessContentKeyByUId);
        }

        IEnumerable<XAttribute> attributes;
        if ((element.Name.LocalName == "Access" || element.Name.LocalName == "IdentCon")
            && (string?)element.Attribute("UId") is string uid && accessContentKeyByUId.TryGetValue(uid, out var key))
        {
            attributes = element.Attributes().Select(a => a.Name.LocalName == "UId" ? new XAttribute("UId", key) : a);
        }
        else if ((element.Name.LocalName is "Part" or "Call" or "NameCon")
            && (string?)element.Attribute("UId") is string partUid && partContentKeyByUId.TryGetValue(partUid, out var partKey))
        {
            attributes = element.Attributes().Select(a => a.Name.LocalName == "UId" ? new XAttribute("UId", partKey) : a);
        }
        else if (ElementsWithVolatileId.TryGetValue(element.Name.LocalName, out var volatileAttrName))
        {
            attributes = element.Attributes().Where(a => a.Name.LocalName != volatileAttrName);
        }
        else
        {
            attributes = element.Attributes();
        }

        var clone = new XElement(element.Name, attributes);
        var children = element.Elements()
            .Where(c => !IsVolatile(c, compareMemoryLayout) && !derived.Drops(element, interfacePath, c))
            .Select(c => Strip(c, accessContentKeyByUId, partContentKeyByUId, compareMemoryLayout,
                derived, InterfacePath.Extend(interfacePath, c)))
            .ToList();

        // <Wire> order within <Wires>, <Access>/<Part> order within <Parts>, and an individual
        // <Wire>'s own endpoint order (<IdentCon>/<NameCon>/<Powerrail>/<OpenCon>) are all not
        // semantically meaningful — confirmed real, 2026-07-10 (Wire-vs-Wire) and 2026-07-11
        // (Parts, same TON grounding that surfaced the Access-UId finding above): TIA
        // relocates/renumbers freely, only the topology matters. The endpoint-order case was
        // confirmed real later, 2026-07-14: a converter-only round trip (to-ir → to-xml, no live
        // TIA involved) of a real multi-endpoint-wire network (MotorDOL/MotorStarter) reported a
        // false mismatch purely from two electrically-identical wires listing the same endpoints
        // in a different order — a wire's own endpoint set, not its listed order, is what a wire
        // actually means, same principle as everything else in this comment, just one level
        // deeper (previously untested because no prior comparison ran a converter-only regenerate
        // twice against a network with genuine wire fan-out). Each element's own volatile UId is
        // already resolved by this point, so sort by remaining content for an order-independent
        // comparison. Deliberately narrow: <Component> order within <Symbol> (and everything
        // else) still matters positionally and must never be reordered.
        //
        // 🔴 A WIRE'S FIRST ENDPOINT IS EXEMPT FROM THAT SORT (2026-08-12) — it is the wire's
        // PRODUCER, and its position is the ONLY thing in the document that says so. There is no
        // attribute, no child element and no other marker: `<IdentCon>` first means the Access
        // drives the port, `<NameCon>` first means the port drives the Access. Sorting the whole
        // endpoint list therefore normalized an INPUT wire and an OUTPUT wire on the same port to
        // the same thing, and `compare`, `drift-check` and the `--no-sidecar` derivability check
        // all inherited that blindness because all three run through here.
        //
        // In the ordinary case direction still survived via the `<NameCon>`'s own `Name`, because
        // a port is conventionally always-input or always-output — measured across all 34 real TIA
        // exports in `simatic-ml/`, all 106 (part, port) pairs sit exclusively at index 0 or
        // exclusively in the tail, none in both. The gap is any port where the same name can be
        // BOTH, which is exactly the InOut case: at a call site nothing marks a parameter as InOut
        // (`ir/SPEC.md` §Interface), so wire order cannot distinguish an input from an InOut
        // either, and a CALL's parameter names are block-author-chosen rather than drawn from that
        // clean instruction-port vocabulary.
        //
        // The 2026-07-14 fan-out measurement this sort was added for is UNAFFECTED, and that was
        // checked rather than assumed: it was about *** two electrically-identical wires listing
        // the same endpoints in a different order *** on a converter-only round trip of
        // MotorStarter (`docs/evidence/stage-S1.md`, "S1 item 26 continued again"). Those are the
        // CONSUMERS of a fanned-out wire — a wire has exactly one producer, so a reordering that
        // preserves the endpoint SET can only ever permute the tail. Sorting the tail alone still
        // collapses it, while a genuine producer/consumer swap now survives to the comparison.
        if (element.Name.LocalName is "Wires" or "Parts")
        {
            children = children.OrderBy(c => c.ToString()).ToList();
        }
        else if (element.Name.LocalName == "Wire" && children.Count > 2)
        {
            children = children.Take(1).Concat(children.Skip(1).OrderBy(c => c.ToString())).ToList();
        }

        foreach (var child in children)
        {
            clone.Add(child);
        }

        // `.Length > 0` matters, and it is not a micro-optimization. XElement's Value setter appends
        // an XText node even for the empty string, so `<Section Name="Static" />` became an element
        // carrying one empty text node while a `<Section Name="Static">` whose only children were
        // DROPPED became an element carrying nothing — and XNode.DeepEquals distinguishes those two.
        // That is exactly the pair rule 3 creates, so with the guard absent the plan produced a
        // difference of its own making: `AreSemanticallyEquivalent` said "not equivalent" while
        // `compare`'s walk found nothing to name, which surfaced as NOT COMPARED rather than as a
        // false green. Measured on two real instance DBs. Skipping the
        // set when there is no text to carry makes an emptied element and a natively empty one the
        // same shape, and changes nothing for any element that has text.
        if (!element.HasElements && element.Value.Length > 0)
        {
            clone.Value = NumericLiteral.Canonicalize(element);
        }

        return clone;
    }

    private static bool IsVolatile(XElement child, bool compareMemoryLayout)
    {
        if (VolatileElementNames.Contains(child.Name.LocalName))
        {
            return true;
        }

        // MemoryLayout is kept — and therefore compared — only when both documents declare one;
        // see AreSemanticallyEquivalent for why. It is NOT in VolatileElementNames any more: a
        // layout difference between two documents that each state a layout is a real difference.
        if (child.Name.LocalName == BlockMemoryLayout.ElementName)
        {
            return !compareMemoryLayout;
        }

        // "Title" isn't its own element name — it's a MultilingualText distinguished only by its
        // own CompositionName attribute (same shape as the "Comment" one right next to it), so it
        // can't go in VolatileElementNames the way a real element name can.
        //
        // Deliberately skipped regardless of content — title is documentation, not logic, so a
        // changed title shouldn't make AreSemanticallyEquivalent report a real difference. This
        // comment originally (2026-07-10) justified the skip on "Title is always empty" grounds;
        // that assumption was disproven 2026-07-12 (S1 items 16/17 — MotorVSDSystem/AirStar both carry
        // a real, non-empty Title, and the format now fully supports writing one, S3's own
        // write path). The skip itself is still correct, just for the reason stated above, not
        // the original one. NOTE the asymmetry: "Comment" (structurally identical, right next to
        // this) is NOT given the same treatment below — real Comment content genuinely is a
        // semantic difference this check is meant to catch.
        if (child.Name.LocalName == "MultilingualText" && (string?)child.Attribute("CompositionName") == "Title")
        {
            return true;
        }

        // 🔴 "Interface" IS NO LONGER SKIPPED AT ALL (2026-08-13). AN INTERFACE IS COMPARED.
        //
        // THE ORIGINAL PREMISE, AND EXACTLY WHY IT EXPIRED. The rule here used to be: skip a code
        // block's Interface, keep a DB's, told apart structurally by whether a "Static" Section is
        // present. The skip was justified — in this file's own words — by
        // `BlockSourceParser.RequireDefaultInterface` having "already hard-errored upstream if it
        // was anything but the standard parameterless-FC boilerplate", so "by the time a document
        // reaches here, a code block's Interface is known-boilerplate". That was true when written
        // (2026-07-10) and it is the whole load the skip was carrying.
        //
        // *** THAT GUARD NO LONGER EXISTS. *** FC/FB parameter-interface modelling landed on
        // 2026-07-12 (S1 item 20: Input/Output/InOut/Constant sections), which is precisely the
        // change that made a code block's Interface real content — and `RequireDefaultInterface`
        // was removed with it. Its only surviving trace in this repository was the sentence above,
        // citing a guarantee nothing had provided for a month. A premise that expires silently is
        // worse than one that was never true, because the comment goes on justifying it.
        //
        // WHAT THE STRUCTURAL TEST ACTUALLY CAUGHT, measured 2026-08-13 against real exports:
        //     DB  -> Static (+ nothing else)                          kept    (correct)
        //     FB  -> Input/Output/InOut/Static/Temp/Constant/None     kept    (BY ACCIDENT: an FB
        //                                                                      happens to have Static)
        //     FC  -> Input/Output/InOut/Temp/Constant/Return          DISCARDED
        //     UDT -> None                                             DISCARDED
        // So the two categories that fell through are *** AN FC WITH PARAMETERS *** and
        // *** A UDT, WHICH IS NOTHING BUT ITS INTERFACE. *** Measured consequences: `ScaleValue`'s
        // `ScaleFactor` retyped Real -> Int compared EQUIVALENT against its real export, and
        // `drift-check` printed MATCH for `UDT_PusherIO` and `UDT_ShredderSequencerIO` while each
        // declares an `AutoStartSignal : Bool` member that is absent from its committed export.
        //
        // WHAT THE ORIGINAL EVIDENCE WAS PROTECTING, AND WHETHER IT STILL NEEDS PROTECTING. The
        // structural test existed ONLY to carve the DB case out of a BLANKET skip — this file
        // records that the blanket strip "would have made every DB round-trip trivially pass
        // without ever comparing member content", caught live 2026-07-10. That protection is
        // preserved here in full and then some: with no skip at all, a DB's members are still
        // compared, and so is everything the carve-out never reached. The original evidence is not
        // discarded by this change; it is the reason the change is a deletion rather than a wider
        // exception. (Contrast the wire-endpoint sort, which was added on evidence that a naive
        // fix would throw away — there is no such evidence here. The only thing the skip protected
        // was a guarantee that no longer exists.)
        //
        // Nothing replaces it: if two documents' interfaces differ, that IS a difference. A
        // volatile attribute *inside* an Interface would belong in VolatileElementNames on its own
        // measured evidence, not behind a blanket discard of the whole element.

        return false;
    }
}
