using System.Text.Json.Serialization;

namespace HmiCli;

/// <summary>One flattened element: absolute integer geometry plus the properties a check needs.</summary>
public sealed record IrItem
{
    [JsonPropertyName("index")] public int Index { get; init; }

    [JsonPropertyName("tag")] public string Tag { get; init; } = "";

    /// <summary>The declared HMI item type from <c>data-hmi</c>, or null when the element is layout-only.</summary>
    [JsonPropertyName("type")] public string? Type { get; init; }

    [JsonPropertyName("left")] public double Left { get; init; }

    [JsonPropertyName("top")] public double Top { get; init; }

    [JsonPropertyName("width")] public double Width { get; init; }

    [JsonPropertyName("height")] public double Height { get; init; }

    [JsonPropertyName("zTier")] public int ZTier { get; init; }

    [JsonPropertyName("interactive")] public bool Interactive { get; init; }

    [JsonPropertyName("leaf")] public bool Leaf { get; init; }

    /// <summary>True for item types that legitimately carry no geometry - H-505's exception.</summary>
    [JsonPropertyName("geometryless")] public bool Geometryless { get; init; }

    /// <summary>Declared layout-only: this element is deliberately NOT a screen object.</summary>
    [JsonPropertyName("ignored")] public bool Ignored { get; init; }

    /// <summary>
    /// `data-hmi-override="H-104: the site's standard requires green for running"`.
    ///
    /// The house rules GUIDE the generator; the engineer directs it. An override names the rule and
    /// gives a reason, and is always REPORTED rather than silently honoured - a suppression nobody
    /// can see is indistinguishable from a checker that does not work.
    /// </summary>
    [JsonPropertyName("overrideSpec")] public string? OverrideSpec { get; init; }

    /// <summary>H-601: "chrome" or "process". Declared, never inferred - a checker cannot tell
    /// which is which from geometry and must not guess.</summary>
    [JsonPropertyName("zone")] public string? Zone { get; init; }

    /// <summary>H-108: "start" | "stop" | "reset". A COMMAND ACCENT - static, small, and about
    /// which control this is rather than what the plant is doing.</summary>
    [JsonPropertyName("accentRole")] public string? AccentRole { get; init; }

    /// <summary>H-109: the id of the control this accent marks, so its area can be checked
    /// against it. Without it the size half of H-109 cannot run.</summary>
    [JsonPropertyName("accentFor")] public string? AccentFor { get; init; }

    [JsonPropertyName("elementId")] public string? ElementId { get; init; }

    /// <summary>
    /// Which diagonal of its bounding box a Line runs along: "down" (top-left to bottom-right,
    /// the default) or "up" (bottom-left to top-right).
    ///
    /// A box has two diagonals and CSS cannot express which one a div "is" - so without this, only
    /// one of them was reachable and any shape needing a pair of opposed diagonals (a cone, a roof,
    /// a chevron) could not be drawn at all.
    /// </summary>
    [JsonPropertyName("lineDirection")] public string? LineDirection { get; init; }

    [JsonPropertyName("safetyCritical")] public bool SafetyCritical { get; init; }

    /// <summary>Declares H-205's carve-out: this element's motion IS the unacknowledged-alarm channel.</summary>
    [JsonPropertyName("alarmFlash")] public bool AlarmFlash { get; init; }

    /// <summary>
    /// The PLC tag this item's value is connected to (<c>data-hmi-bind</c>).
    ///
    /// 🔴 Captured since the flattener was written and DISCARDED by the emitter until 2026-08-17 -
    /// the name was read into the IR and then dropped on the floor by a ternary whose two branches
    /// were identical. Every screen this tool produced before that date was a STATIC PICTURE, and
    /// nothing said so. It is now the ProcessValue dynamization on an IOField and the value
    /// dynamization on a Text.
    /// </summary>
    [JsonPropertyName("bind")] public string? Bind { get; init; }

    /// <summary>
    /// Screen this button navigates to (<c>data-hmi-goto</c>), emitted as an <c>ActivateScreen</c>
    /// system function on the button's <c>KeyUp</c> event.
    ///
    /// <c>KeyUp</c> is harvested from a real Classic export - all four navigation buttons on the
    /// reference screen use it. There is no <c>Click</c>.
    /// </summary>
    [JsonPropertyName("goto")] public string? GoTo { get; init; }

    /// <summary>
    /// IOField direction (<c>data-hmi-mode</c>): <c>Output</c> (display only), <c>Input</c> or
    /// <c>InOutput</c>.
    ///
    /// DEFAULTS TO <c>Output</c> DELIBERATELY. An Input field lets an operator write to the PLC, so
    /// defaulting to writable would make a display field into a control by omission - the kind of
    /// surprise that is only discovered by someone changing a setpoint they meant to read.
    /// Writability is opted into, never inherited.
    /// </summary>
    [JsonPropertyName("mode")] public string? Mode { get; init; }

    /// <summary>
    /// IOField display pattern (<c>data-hmi-format</c>), e.g. <c>99999.999</c>. Harvested shape: the
    /// digit count before the point sets the integer width and after it the decimals.
    /// </summary>
    [JsonPropertyName("format")] public string? Format { get; init; }

    /// <summary>
    /// Engineering unit (<c>data-hmi-unit</c>). H-303 requires a unit on every process value, so this
    /// exists to make satisfying it a property of the field rather than a separate text item that can
    /// drift away from the number it belongs to.
    /// </summary>
    [JsonPropertyName("unit")] public string? Unit { get; init; }

    [JsonPropertyName("text")] public string? Text { get; init; }

    /// <summary>
    /// This element's text was longer than the flattener will carry. Emit REFUSES rather than
    /// shortening it: a truncated caption renders, looks deliberate, and loses whichever half came
    /// second — which on a safety statement is the half that mattered.
    /// </summary>
    [JsonPropertyName("textTruncated")] public bool TextTruncated { get; init; }

    [JsonPropertyName("backColor")] public string? BackColor { get; init; }

    [JsonPropertyName("foreColor")] public string? ForeColor { get; init; }

    [JsonPropertyName("borderColor")] public string? BorderColor { get; init; }

    [JsonPropertyName("backgroundImage")] public string? BackgroundImage { get; init; }

    [JsonPropertyName("boxShadow")] public string? BoxShadow { get; init; }

    [JsonPropertyName("textShadow")] public string? TextShadow { get; init; }

    [JsonPropertyName("borderRadius")] public string? BorderRadius { get; init; }

    [JsonPropertyName("animationName")] public string? AnimationName { get; init; }

    [JsonPropertyName("fontVariantNumeric")] public string? FontVariantNumeric { get; init; }

    /// <summary>Computed CSS font-size in px. SimaticML's FontSize is also PIXELS, so this maps straight through.</summary>
    [JsonPropertyName("fontSizePx")] public double FontSizePx { get; init; }

    [JsonPropertyName("fontWeight")] public int FontWeight { get; init; }

    /// <summary>CSS font-style: normal | italic | oblique.</summary>
    [JsonPropertyName("fontStyleCss")] public string? FontStyleCss { get; init; }

    /// <summary>The family that ACTUALLY rendered, measured - not the requested CSS stack.</summary>
    [JsonPropertyName("fontFamilyUsed")] public string? FontFamilyUsed { get; init; }

    [JsonPropertyName("fontFamilyCss")] public string? FontFamilyCss { get; init; }

    /// <summary>Captured because an uppercased label in the browser and a mixed-case payload in the
    /// panel look like a font difference when they are really a text difference.</summary>
    [JsonPropertyName("textTransform")] public string? TextTransform { get; init; }

    [JsonPropertyName("letterSpacing")] public string? LetterSpacing { get; init; }
}

/// <summary>
/// The flattened screen. <see cref="Panel"/> is mandatory and travels with the artifact (H-407) -
/// every downstream check needs it to convert a millimetre threshold, and none of them may guess.
/// </summary>
public sealed record ScreenIr
{
    [JsonPropertyName("panel")] public string Panel { get; init; } = "";

    [JsonPropertyName("family")] public string Family { get; init; } = "Classic";

    [JsonPropertyName("canvasWidth")] public int CanvasWidth { get; init; }

    [JsonPropertyName("canvasHeight")] public int CanvasHeight { get; init; }

    [JsonPropertyName("source")] public string Source { get; init; } = "";

    [JsonPropertyName("chromeVersion")] public string ChromeVersion { get; init; } = "";

    [JsonPropertyName("items")] public List<IrItem> Items { get; init; } = new();
}

public enum Severity
{
    Advisory,
    Error,
}

public sealed record Finding(string RuleId, Severity Severity, string Message, int? ItemIndex = null)
{
    public override string ToString()
    {
        var where = ItemIndex is null ? "" : $" [item {ItemIndex}]";
        return $"{(Severity == Severity.Error ? "ERROR" : "ADVISORY")} {RuleId}{where}: {Message}";
    }
}

/// <summary>
/// A check result that carries its own denominator. Every tool in this repo that reported a pass
/// over zero comparisons eventually reported a false pass - drift-check, compare, reuse-scan,
/// undriven-scan, compile-all. Five for five. So <see cref="Examined"/> is printed on every run and
/// an empty examination is never a pass.
/// </summary>
public sealed record CheckResult(string CheckName, int Examined, IReadOnlyList<Finding> Findings)
{
    public bool NothingExamined => Examined == 0;

    public int ErrorCount => Findings.Count(f => f.Severity == Severity.Error);
}
