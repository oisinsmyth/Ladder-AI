namespace Harness.MirrorView;

/// <summary>
/// The register map, as it exists in the committed IR — nowhere else.
/// </summary>
/// <param name="BaseByte">
/// <c>%M</c> byte address of holding register 0, read from the <c>MB_HOLD_REG</c> area pointer
/// (<c>P#M&lt;base&gt;.0 WORD n</c>) in the Modbus server block. Register r is the word at
/// <c>base + 2r</c>.
/// </param>
/// <param name="DeclaredRegisters">The <c>WORD n</c> half of the same pointer: registers <c>0..n-1</c>.</param>
/// <param name="Tags">Every tag in the mirror tag table, placed on the grid.</param>
/// <param name="TagTableSource">Path the tag table was read from.</param>
/// <param name="AreaPointerSource">Path the area pointer was read from.</param>
public sealed record MirrorMap(
    int BaseByte,
    int DeclaredRegisters,
    IReadOnlyList<MirrorTag> Tags,
    string TagTableSource,
    string AreaPointerSource)
{
    /// <summary>The tag covering <paramref name="register"/>, or null when nothing in the map does.</summary>
    public MirrorTag? TagAt(int register) => Tags.FirstOrDefault(t => t.Covers(register));

    /// <summary>
    /// Registers inside the declared area that NO tag describes.
    ///
    /// <para>Reported, never hidden. A register the server exposes and the map does not name is a fact
    /// about the map, and a view that silently omitted those rows would be answering a narrower question
    /// than the one on the screen.</para>
    /// </summary>
    public IReadOnlyList<int> UnmappedRegisters =>
        Enumerable.Range(0, DeclaredRegisters).Where(r => TagAt(r) is null).ToList();
}

/// <summary>
/// The outcome of loading the map.
///
/// <para>🔴 <b>THERE IS NO FALLBACK TABLE, AND THAT IS THE POINT.</b> When the artifact cannot be
/// parsed this carries <see cref="Ok"/> == false and a list of reasons, and the tool REFUSES to start.
/// A built-in map would be wrong the first time the copy layer is regenerated and would look right
/// while being wrong — which is this project's most expensive failure shape, not a convenience.</para>
/// </summary>
/// <param name="Ok">True only when a whole, self-consistent map was built from the files.</param>
/// <param name="Map">The map. Null unless <paramref name="Ok"/>.</param>
/// <param name="Refusals">Why not. Empty only when <paramref name="Ok"/>.</param>
public sealed record MirrorMapLoad(bool Ok, MirrorMap? Map, IReadOnlyList<string> Refusals)
{
    public static MirrorMapLoad Loaded(MirrorMap map) => new(true, map, Array.Empty<string>());

    public static MirrorMapLoad Refused(params string[] refusals) => new(false, null, refusals);

    public static MirrorMapLoad Refused(IReadOnlyList<string> refusals) => new(false, null, refusals);
}
