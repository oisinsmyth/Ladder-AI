using System.Xml.Linq;

namespace GoldenHarness;

/// <summary>
/// Strips known-volatile elements before comparing two exports for semantic equivalence
/// (docs/08-testing-strategy.md Layer 1: SimaticML'' == SimaticML after normalization).
/// Narrower than originally anticipated: the IR sidecar (ADR-0001) is designed to preserve
/// exact source UIds on regeneration, so what's left to normalize is only what TIA itself
/// regenerates regardless of input content.
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
        "MemoryLayout",
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
    };

    public static bool AreSemanticallyEquivalent(XDocument original, XDocument reExported)
    {
        if (original.Root is null || reExported.Root is null)
        {
            throw new InvalidOperationException("Cannot compare a document with no root element.");
        }

        return XNode.DeepEquals(Strip(original.Root), Strip(reExported.Root));
    }

    public static XElement Strip(XElement element) => Strip(element, BuildAccessContentKeyMap(element));

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
            var value = access.Descendants().FirstOrDefault(e => e.Name.LocalName == "ConstantValue")?.Value ?? string.Empty;
            return $"const:{value}";
        }

        // The full <Symbol> (component names plus any slice/array modifiers) so two Access
        // elements only compare equal when truly identical, not just same top-level path.
        var symbol = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
        return $"tag:{scope}:{symbol?.ToString(SaveOptions.DisableFormatting)}";
    }

    private static XElement Strip(XElement element, Dictionary<string, string> accessContentKeyByUId)
    {
        // UId numbering restarts at the beginning of every network (each <FlgNet> is its own
        // numbering scope) — the content-key map must be rebuilt per network too, not flattened
        // across the whole document, or the same number ("22", "23", ...) reused in a different
        // network silently clobbers an unrelated entry. Caught live, 2026-07-11, comparing a
        // real 3-network export (FC TimerSample) — a single-network test fixture would never
        // have exposed this.
        if (element.Name.LocalName == "FlgNet")
        {
            accessContentKeyByUId = BuildAccessContentKeyMap(element);
        }

        IEnumerable<XAttribute> attributes;
        if ((element.Name.LocalName == "Access" || element.Name.LocalName == "IdentCon")
            && (string?)element.Attribute("UId") is string uid && accessContentKeyByUId.TryGetValue(uid, out var key))
        {
            attributes = element.Attributes().Select(a => a.Name.LocalName == "UId" ? new XAttribute("UId", key) : a);
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
        var children = element.Elements().Where(c => !IsVolatile(c)).Select(c => Strip(c, accessContentKeyByUId)).ToList();

        // <Wire> order within <Wires>, and <Access>/<Part> order within <Parts>, are not
        // semantically meaningful — confirmed real, 2026-07-10 (Wire) and 2026-07-11 (Parts,
        // same TON grounding that surfaced the Access-UId finding above): TIA relocates/renumbers
        // freely, only the topology matters. Each element's own volatile UId is already resolved
        // by this point, so sort by remaining content for an order-independent comparison.
        // Deliberately narrow: <Component> order within <Symbol> (and everything else) still
        // matters positionally and must never be reordered.
        if (element.Name.LocalName is "Wires" or "Parts")
        {
            children = children.OrderBy(c => c.ToString()).ToList();
        }

        foreach (var child in children)
        {
            clone.Add(child);
        }

        if (!element.HasElements)
        {
            clone.Value = element.Value;
        }

        return clone;
    }

    private static bool IsVolatile(XElement child)
    {
        if (VolatileElementNames.Contains(child.Name.LocalName))
        {
            return true;
        }

        // "Title" isn't its own element name — it's a MultilingualText distinguished only by its
        // own CompositionName attribute (same shape as the "Comment" one right next to it), so it
        // can't go in VolatileElementNames the way a real element name can. Confirmed real,
        // 2026-07-10, always empty (BlockSourceParser.RequireEmptyTitle hard-errors otherwise —
        // safe to skip here because a non-empty one never reaches this point).
        if (child.Name.LocalName == "MultilingualText" && (string?)child.Attribute("CompositionName") == "Title")
        {
            return true;
        }

        // FC/FB "Interface" (parameter declarations) is safe to skip ONLY because
        // BlockSourceParser.RequireDefaultInterface already hard-errored upstream if it was
        // anything but the standard parameterless-FC boilerplate — by the time a document reaches
        // here, a code block's Interface is known-boilerplate. A DB's "Interface" is the real
        // member declarations (DbSourceParser has no equivalent boilerplate guard — there's
        // nothing to default to) and must never be skipped. Structural test, not a blanket name
        // match: a DB's Interface always has a "Static" Section, a code block's never does.
        // Caught live, 2026-07-10: the original blanket "Interface" strip would have made every
        // DB round-trip trivially pass without ever comparing member content.
        if (child.Name.LocalName == "Interface")
        {
            return !child.Descendants().Any(e => e.Name.LocalName == "Section" && (string?)e.Attribute("Name") == "Static");
        }

        return false;
    }
}
