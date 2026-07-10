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
    };

    public static bool AreSemanticallyEquivalent(XDocument original, XDocument reExported)
    {
        if (original.Root is null || reExported.Root is null)
        {
            throw new InvalidOperationException("Cannot compare a document with no root element.");
        }

        return XNode.DeepEquals(Strip(original.Root), Strip(reExported.Root));
    }

    public static XElement Strip(XElement element)
    {
        var clone = new XElement(element.Name, element.Attributes());
        foreach (var child in element.Elements())
        {
            if (VolatileElementNames.Contains(child.Name.LocalName))
            {
                continue;
            }

            clone.Add(Strip(child));
        }

        if (!element.HasElements)
        {
            clone.Value = element.Value;
        }

        return clone;
    }
}
