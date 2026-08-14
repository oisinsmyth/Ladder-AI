namespace Harness.MirrorView;

/// <summary>
/// How many holding registers a mirror element occupies, and how its bytes are read back.
///
/// <para><b>Derived from the ADDRESS, cross-checked against the TYPE NAME.</b> The address is the
/// authority — <c>%MD</c> is two registers whatever the type name says — and the type name decides the
/// <i>interpretation</i>. When the two disagree the map is refused by name rather than resolved in
/// favour of either, because a <c>Time</c> sitting at a <c>%MW</c> is a map somebody built wrong and
/// picking a winner would hide it.</para>
/// </summary>
public enum MirrorWidth
{
    /// <summary>One bit inside one register.</summary>
    Bit,

    /// <summary>One whole register — <c>%MW</c>.</summary>
    Word,

    /// <summary>Two registers — <c>%MD</c>. Reassembled HIGH-WORD-FIRST (measured on this rig).</summary>
    DoubleWord,
}

/// <summary>
/// One tag from the committed mirror tag table, placed on the Modbus register grid.
///
/// <para><b>Every field here came out of <c>ir/&lt;project&gt;/HarnessMirror.ir</c>.</b> Nothing in this
/// assembly carries a built-in copy of the map: a hardcoded table would be wrong the first time the copy
/// layer is regenerated, and would look right while being wrong.</para>
/// </summary>
/// <param name="Name">Tag name, verbatim.</param>
/// <param name="TypeName">IR type name, verbatim (<c>Bool</c>, <c>Int</c>, <c>Time</c>, <c>DWord</c>, <c>DInt</c>, …).</param>
/// <param name="Address">IR/TIA address, verbatim (<c>%MD1000</c>, <c>%MW1012</c>, <c>%M1009.0</c>).</param>
/// <param name="Register">Holding-register index of the FIRST register the element occupies.</param>
/// <param name="Width">How many registers, and how they are read.</param>
/// <param name="BitInRegister">
/// For <see cref="MirrorWidth.Bit"/>: which bit of the 16-bit register value the tag is.
/// <b>Derived by inverting <c>Harness.Map.MirrorGeometry.BitAddressOf</c>, which that class marks
/// <c>[I]</c> — INFERRED, NOT MEASURED.</b> The register index does NOT depend on that inference (both
/// candidate bit mappings put the bit in the same register); which bit inside it does. The view prints
/// the whole raw word beside every Bool so a reader is never asked to take the bit position on trust.
/// </param>
/// <param name="Comment">The tag's own COMMENT text — the "meaning" column, straight from the artifact.</param>
/// <param name="SourceLine">1-based line number in the source file, so a reader can go and look.</param>
public sealed record MirrorTag(
    string Name,
    string TypeName,
    string Address,
    int Register,
    MirrorWidth Width,
    int BitInRegister,
    string Comment,
    int SourceLine)
{
    /// <summary>Registers this element occupies.</summary>
    public int RegisterCount => Width == MirrorWidth.DoubleWord ? 2 : 1;

    /// <summary>Last register index this element occupies.</summary>
    public int LastRegister => Register + RegisterCount - 1;

    /// <summary>True when <paramref name="register"/> is inside this element.</summary>
    public bool Covers(int register) => register >= Register && register <= LastRegister;
}
