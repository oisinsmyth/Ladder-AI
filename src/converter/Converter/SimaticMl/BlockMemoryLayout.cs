using System.Xml.Linq;

namespace Converter.SimaticMl;

/// <summary>
/// A block's memory layout — optimized vs standard block access, the SimaticML
/// <c>&lt;MemoryLayout&gt;</c> element inside a block's own <c>&lt;AttributeList&gt;</c>.
/// Shared by DBs (<see cref="DbSource"/>) and code blocks (<see cref="BlockSource"/>): the
/// element is byte-identical in both, and it is backed by one property,
/// <c>Siemens.Engineering.SW.Blocks.PlcBlock.MemoryLayout</c>, which is on <c>PlcBlock</c> and
/// therefore not DB-specific.
///
/// **Why it is carried at all (2026-08-12).** Classic S7comm cannot see an OPTIMIZED block: the
/// block is not reported as an error, it is simply absent, and the failure surfaces at the first
/// DATA read. The S7-1200 default is Optimized. Until this file existed the attribute was
/// invisible to the whole converter — absent from `ir/SPEC.md`, never written by
/// <see cref="DbSourceWriter"/>, never read by <see cref="DbSourceParser"/>, and on
/// <see cref="Normalizer"/>'s ignore list — so a real Standard DB round-tripped
/// export -> to-ir -> to-xml came back with NO MemoryLayout element at all, the import stated no
/// opinion, TIA applied its default, and every check in the pipeline stayed green. Measured on a
/// real export, 2026-08-12; the first symptom was a runtime Modbus status code.
///
/// **Absent means "no opinion", never a default.** Every `.ir` written before this existed carries
/// no layout, and emitting a default for those would silently restate the layout of every DB in the
/// corpus — replacing one silent corruption with a broader one. So: present in the IR, emit it;
/// absent, emit nothing, exactly as before. The defect closes because an IR that CAME FROM an
/// export now carries the attribute, so the information survives the trip it previously did not.
///
/// **The value set is closed and confirmed**, not guessed: <c>Siemens.Engineering.SW.Blocks.MemoryLayout</c>
/// is an enum with exactly <c>Standard</c> and <c>Optimized</c> (reflected on the installed V20
/// assembly — `src/openness-cli/README.md`, `block-layout`), and both values are present in real
/// exports (Standard on the 2026-08-12 grounding export, Optimized across the whole committed
/// `simatic-ml/` corpus). Anything else is a hard error rather than a value passed blindly through
/// to TIA.
///
/// **There is nothing else in the document to derive it from.** Investigated 2026-08-12 on the
/// owner's question of whether a Standard layout could be COMPUTED from member order and types
/// instead of stored: SimaticML carries no per-member byte/bit offsets at all. A real
/// `WithDefaults` export of a Standard DB has zero occurrences of an offset or address, and across
/// the 900 <c>&lt;Member&gt;</c> elements in the committed corpus the only attributes that exist
/// are Name/Datatype/Remanence/Accessibility/Version/Informative. So there is no offset channel
/// for TIA to infer a layout from, and none for a derivation to be verified against — the CPU
/// memory layout the owner described is computed by TIA and never serialized. This element is the
/// only carrier the file format has.
/// </summary>
public static class BlockMemoryLayout
{
    public const string ElementName = "MemoryLayout";

    public const string Standard = "Standard";

    public const string Optimized = "Optimized";

    public static bool IsKnownValue(string value) => value is Standard or Optimized;

    /// <summary>
    /// Reads an optional <c>&lt;MemoryLayout&gt;</c> from a block/DB <c>&lt;AttributeList&gt;</c>.
    /// Null when the source has none — the pre-2026-08-12 shape every converter-written document
    /// still has, and the "no opinion" case.
    /// </summary>
    public static string? ReadOptional(XElement attributeList, string context)
    {
        var element = attributeList.Elements().FirstOrDefault(e => e.Name.LocalName == ElementName);
        if (element is null)
        {
            return null;
        }

        var value = element.Value;
        if (!IsKnownValue(value))
        {
            throw new UnsupportedConstructException(
                $"{context} has MemoryLayout '{value}' — only \"{Standard}\" and \"{Optimized}\" exist in " +
                "Siemens.Engineering.SW.Blocks.MemoryLayout.");
        }

        return value;
    }

    /// <summary>
    /// Appends the element when the model carries a layout, and nothing at all when it does not —
    /// the backward-compatibility rule above, in the one place both writers go through.
    /// Positioned by the caller: every real export places it immediately before <c>&lt;Name&gt;</c>.
    /// </summary>
    public static void AppendIfPresent(List<XElement> attributeListChildren, string? memoryLayout)
    {
        if (memoryLayout is not null)
        {
            attributeListChildren.Add(new XElement(ElementName, memoryLayout));
        }
    }
}
