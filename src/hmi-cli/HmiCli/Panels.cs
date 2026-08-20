namespace HmiCli;

/// <summary>
/// Panel geometry. The reason this type exists at all is H-405: thresholds are declared in
/// millimetres and converted per panel, because 48 px is 9 mm on exactly one panel this project
/// touches.
/// </summary>
/// <remarks>
/// <para>
/// H-406: <b>px/mm is a PAIR, not a scalar.</b> The Basic panels do not have square pixels — a
/// KTP700 Basic is 154.1 x 85.9 mm carrying an 800 x 480 grid, so its physical aspect is 1.794
/// against a pixel aspect of 1.667. A pixel is about 7.7% wider than it is tall. The Unified MTP
/// panels ARE square, which is exactly how this project first got it wrong: the value was inferred
/// from a verified neighbouring panel and the inference did not hold.
/// </para>
/// <para>
/// Consequence: a minor-axis touch-target check must use <see cref="PxPerMmV"/>, the tighter axis.
/// </para>
/// </remarks>
public sealed record Panel(
    string Name,
    string OrderNumber,
    double WidthMm,
    double HeightMm,
    int WidthPx,
    int HeightPx)
{
    public double PxPerMmH => WidthPx / WidthMm;

    public double PxPerMmV => HeightPx / HeightMm;

    /// <summary>Pixels for a millimetre threshold on the given axis, rounded up — a threshold is a floor.</summary>
    public int MmToPxH(double mm) => (int)Math.Ceiling(mm * PxPerMmH);

    public int MmToPxV(double mm) => (int)Math.Ceiling(mm * PxPerMmV);

    public double PxToMmH(double px) => px / PxPerMmH;

    public double PxToMmV(double px) => px / PxPerMmV;
}

public static class Panels
{
    // Active-area figures are from the Siemens device datasheets (2026-08-17). The two Basic rows
    // are the ones that matter here; the MTP rows are retained because they are what the
    // square-pixel assumption was (wrongly) generalised from, and deleting them would delete the
    // evidence for why the pair exists.
    public static readonly Panel Ktp700Basic = new("KTP700 Basic", "6AV2123-2GB03-0AX0", 154.1, 85.9, 800, 480);
    public static readonly Panel Ktp900Basic = new("KTP900 Basic", "6AV2123-2JB03-0AX0", 198.0, 111.7, 800, 480);
    public static readonly Panel Ktp400Basic = new("KTP400 Basic", "6AV2123-2DB03-0AX0", 95.0, 53.9, 480, 272);
    public static readonly Panel Mtp700 = new("MTP700 Unified", "-", 152.0, 91.0, 800, 480);
    public static readonly Panel Mtp1000 = new("MTP1000 Unified", "-", 217.0, 136.0, 1280, 800);
    public static readonly Panel Mtp1200 = new("MTP1200 Unified", "-", 261.0, 163.0, 1280, 800);

    /// <summary>Every panel in the table. The one place a new row has to be added.</summary>
    public static readonly IReadOnlyList<Panel> All = new[]
    {
        Ktp700Basic, Ktp900Basic, Ktp400Basic, Mtp700, Mtp1000, Mtp1200,
    };

    private static readonly Dictionary<string, Panel> ByName = BuildIndex();

    /// <summary>
    /// 🔴 EVERY PANEL ANSWERS TO ITS OWN <see cref="Panel.Name"/>, AND THAT LINE IS THE BUG FIX.
    ///
    /// <para>
    /// The short aliases below are hand-written and were the only keys. But <c>flatten</c> stamps
    /// <c>Panel = panel.Name</c> into the IR — the literal <c>"KTP700 Basic"</c>, with the space —
    /// and <c>lint</c>/<c>check</c> resolve that string back through this table. No key matched it,
    /// so <b>the tool could not read its own output</b>: a <c>flatten</c> → <c>check</c> round trip
    /// through a <c>.json</c> failed on every panel in the table.
    /// </para>
    /// <para>
    /// It had already been worked around by hand rather than reported — a real job carries paired
    /// <c>*-ir.json</c> and <c>*-ir-panelfix.json</c> files differing only in that string.
    /// </para>
    /// <para>
    /// Deriving the alias from <see cref="All"/> rather than adding six more literals is deliberate:
    /// the failure was a hand-maintained list falling out of step with the records it indexes, and a
    /// seventh literal would leave the eighth panel to reintroduce it.
    /// </para>
    /// </summary>
    private static Dictionary<string, Panel> BuildIndex()
    {
        var index = new Dictionary<string, Panel>(StringComparer.OrdinalIgnoreCase)
        {
            ["KTP700"] = Ktp700Basic,
            ["KTP700Basic"] = Ktp700Basic,
            ["KTP900"] = Ktp900Basic,
            ["KTP900Basic"] = Ktp900Basic,
            ["KTP400"] = Ktp400Basic,
            ["KTP400Basic"] = Ktp400Basic,
            ["MTP700"] = Mtp700,
            ["MTP1000"] = Mtp1000,
            ["MTP1200"] = Mtp1200,
        };

        foreach (var panel in All)
        {
            index[panel.Name] = panel;
        }

        return index;
    }

    /// <summary>Every spelling this table accepts — both the short aliases and the full names.</summary>
    public static IReadOnlyCollection<string> KnownNames => ByName.Keys;

    /// <summary>
    /// H-407: an artifact that does not declare its panel is a REFUSAL, not a default. The anchor
    /// and the target share a resolution and differ ~28% physically, so a guessed panel is a
    /// quarter-scale error that passes every pixel-based check silently.
    /// </summary>
    public static bool TryResolve(string? name, out Panel panel, out string error)
    {
        panel = null!;
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "No panel declared. --panel is required and has no default: the KTP700 and KTP900 "
                  + "Basic share a resolution but differ ~28% physically, so guessing one is a "
                  + "quarter-scale sizing error that passes every pixel check. Known panels: "
                  + string.Join(", ", KnownNames);
            return false;
        }

        if (!ByName.TryGetValue(name.Trim(), out var found))
        {
            error = $"Unknown panel '{name}'. Known panels: {string.Join(", ", KnownNames)}. "
                  + "An unknown panel is refused rather than approximated - see hmi/target-differences.md.";
            return false;
        }

        panel = found;
        error = string.Empty;
        return true;
    }
}
