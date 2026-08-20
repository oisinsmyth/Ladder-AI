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
    /// A command button's CHANNEL PREFIX (<c>data-hmi-cmd</c>) — the HMI tag name up to, but not
    /// including, the <c>_Code</c> / <c>_Seq</c> suffix.
    ///
    /// 🔴 THE PREFIX, NOT THE TAG, AND THAT IS THE WHOLE POINT. The PLC's command channel does not
    /// take a command bit; a command is a CODE that is read when the SEQUENCE NUMBER CHANGES. So a
    /// command button is always TWO writes in a FIXED ORDER — code first, then the sequence bump —
    /// and getting the order wrong yields a controller that acts on the PREVIOUS code.
    ///
    /// Naming the channel rather than the two tags makes the pair inseparable and the order
    /// unexpressible-wrongly: the author cannot write a code without bumping the sequence, cannot
    /// bump the sequence without a code, and cannot put them the wrong way round. That is a
    /// correctness property held by construction rather than by documentation.
    /// </summary>
    [JsonPropertyName("cmd")] public string? Cmd { get; init; }

    /// <summary>The command CODE (<c>data-hmi-cmd-code</c>) written to <c>&lt;Cmd&gt;_Code</c>.</summary>
    [JsonPropertyName("cmdCode")] public string? CmdCode { get; init; }

    /// <summary>
    /// Optional command operands (<c>data-hmi-cmd-int1</c> … <c>-real2</c>), written to
    /// <c>&lt;Cmd&gt;_Int1</c> etc. BEFORE the sequence bump, for the same reason the code is.
    /// </summary>
    [JsonPropertyName("cmdInt1")] public string? CmdInt1 { get; init; }

    /// <inheritdoc cref="CmdInt1"/>
    [JsonPropertyName("cmdInt2")] public string? CmdInt2 { get; init; }

    /// <inheritdoc cref="CmdInt1"/>
    [JsonPropertyName("cmdReal1")] public string? CmdReal1 { get; init; }

    /// <inheritdoc cref="CmdInt1"/>
    [JsonPropertyName("cmdReal2")] public string? CmdReal2 { get; init; }

    /// <summary>
    /// A SINGLE tag write on a button press (<c>data-hmi-set="&lt;Tag&gt;=&lt;value&gt;"</c>), with
    /// the same <c>@</c> convention as a command operand: <c>=3</c> writes the literal 3,
    /// <c>=@Other_Tag</c> copies that tag's live value at the press.
    ///
    /// 🔴 IT IS DELIBERATELY ONE WRITE, AND IT DELIBERATELY CANNOT NAME A <c>_Seq</c> OR A
    /// <c>_Code</c>. This exists to STAGE AN OPERAND - to arm a value that some LATER, separate
    /// press will act on - and that is the whole of its remit. Allowing several writes, or a write
    /// to the handshake tags, would let an author hand-build a command channel write out of parts,
    /// in an order of their choosing; <see cref="Cmd"/>'s entire correctness argument is that the
    /// order is not expressible wrongly, and this must not be the hole in it.
    ///
    /// The motivating case: a recipe chooser row that ARMS a recipe number and navigates back,
    /// leaving the act to the START button on the screen the operator returns to. A press that both
    /// chose and started could not be undone, and the same screen has to be safe to merely BROWSE.
    /// </summary>
    [JsonPropertyName("setTag")] public string? SetTag { get; init; }

    /// <summary>
    /// Declares an IOField as a STRING display of this many characters (<c>data-hmi-string="32"</c>),
    /// rather than a number.
    ///
    /// ⚠️ SEPARATE FROM <see cref="Format"/> AND MUTUALLY EXCLUSIVE WITH IT, because the two carry
    /// different <c>DataFormat</c> values and a field that declared both would be exactly the
    /// self-contradiction the coherence gate exists to catch. The emitted pattern is this many
    /// asterisks and <c>FieldLength</c> is its length, which is the invariant a mismatched
    /// <c>FieldLength</c> already crashed Portal over once.
    /// </summary>
    [JsonPropertyName("stringLength")] public string? StringLength { get; init; }

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


    /// <summary>
    /// Name of the TEXT LIST that resolves this field's numeric value into a word
    /// (<c>data-hmi-textlist</c>). Required on a SymbolicIOField.
    ///
    /// This is the half that makes a coded value readable. Nine of nineteen fields on the first real
    /// screen delivered were bare numbers standing in for words - state, hold cause, moisture stage -
    /// because the emitter had no type that could resolve them. The text list itself is a project
    /// object the engineer creates; this only names it.
    /// </summary>
    [JsonPropertyName("textList")] public string? TextList { get; init; }

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

    /// <summary>
    /// WHEN this item is on the screen at all - a <c>Hmi.Dynamic.VisibilityAnimation</c> driven by a
    /// tag, showing or hiding the object as that tag's value enters a numeric range.
    ///
    /// 🔴 THIS IS BEHAVIOUR, NOT DECORATION, and until 2026-08-20 the IR had no field for it. That
    /// mattered the moment a person edited one of our screens in TIA: they built a paged list by
    /// stacking rows and hiding all but one, and nothing on our side could read the result back or
    /// state what it did. An item carrying one of these looks identical in every geometric check and
    /// is invisible to the operator most of the time.
    ///
    /// ⚠️ POPULATED BY <c>to-ir</c> ONLY at present. There is no <c>data-hmi-*</c> attribute for it,
    /// so the HTML path cannot author one - a deliberate limit rather than an oversight: the
    /// authoring vocabulary belongs to docs/17 and adding three attributes here would put it in the
    /// tool ahead of the rule. The EMITTER writes it, so a document read in keeps it on the way out.
    /// </summary>
    [JsonPropertyName("visibility")] public IrVisibility? Visibility { get; init; }

    /// <summary>
    /// The named layer this item belongs to (<c>data-hmi-layer</c>), or null for the base layer.
    ///
    /// 🔴 A LAYER IS A GROUPING, AND THE GROUPING IS WHAT CARRIES THE VISIBILITY RULE.
    ///
    /// A classic screen layer CANNOT be hidden at runtime — measured across 26 real screens, a
    /// <c>ScreenLayer</c> carries exactly <c>Index</c>, <c>Name</c> and <c>VisibleES</c>, no tag
    /// link and no animation, and <c>VisibleES</c> is visibility in the TIA EDITOR. So a popup
    /// cannot be "a layer shown by a condition", which is what the screen specs describe.
    ///
    /// What a layer IS good for is the other half: a person editing the screen in TIA can hide the
    /// dialog to work on what is behind it. So the layer does the authoring job, and the emitter
    /// puts the layer's <see cref="IrVisibility"/> on EVERY MEMBER, which does the runtime job.
    ///
    /// Declaring the rule once, on the group, is the point rather than a convenience: an object
    /// that belongs to a dialog and is missing the condition stays on the glass after the dialog
    /// closes, and nothing about the document would look wrong.
    /// </summary>
    [JsonPropertyName("layer")] public string? Layer { get; init; }

    /// <summary>
    /// On a <c>data-hmi="Layer"</c> DECLARATION: the layer's index. Index 0 is the base layer and
    /// cannot be claimed. Two layers at one index is a refusal, not a merge.
    /// </summary>
    [JsonPropertyName("layerIndex")] public string? LayerIndex { get; init; }

    /// <summary>
    /// On a declaration: the tag whose value hides this layer's members
    /// (<c>data-hmi-layer-hide-when</c>), with <c>data-hmi-layer-hide-range</c> as
    /// <c>low..high</c>.
    ///
    /// ⚠️ HIDE rather than SHOW, deliberately. The only form measured against Portal is
    /// <c>Visible=false</c> INSIDE the range — that is what TIA itself wrote on a hand-built
    /// animation and what round-tripped. So a dialog that should appear while a prompt is standing
    /// is authored as "hidden while the prompt id is 0..0", which is the same statement in the
    /// shape that is proven.
    /// </summary>
    [JsonPropertyName("layerHideWhen")] public string? LayerHideWhen { get; init; }

    /// <inheritdoc cref="LayerHideWhen"/>
    [JsonPropertyName("layerHideRange")] public string? LayerHideRange { get; init; }
}

/// <summary>
/// A tag-driven visibility rule: <c>Visible</c> INSIDE the range <c>[RangeStart, RangeEnd]</c>,
/// the opposite outside it.
///
/// The bounds are carried as WRITTEN rather than parsed to numbers. They are round-tripped into a
/// document TIA reads, and a value that took a trip through a double is a value that can come back
/// spelled differently - which is a diff nobody caused.
/// </summary>
public sealed record IrVisibility
{
    /// <summary>The trigger tag (<c>Hmi.Dynamic.TagElementTrigger</c>). An animation without one
    /// cannot be evaluated, so the emitter refuses rather than writing a rule that never fires.</summary>
    [JsonPropertyName("tag")] public string Tag { get; init; } = "";

    [JsonPropertyName("rangeStart")] public string RangeStart { get; init; } = "0";

    [JsonPropertyName("rangeEnd")] public string RangeEnd { get; init; } = "0";

    /// <summary>What happens INSIDE the range. TIA's own default on a hand-built animation is
    /// <c>false</c> - the range is the HIDDEN band - which reads backwards and is why it is stated.</summary>
    [JsonPropertyName("visible")] public bool Visible { get; init; }
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

    /// <summary>WHERE this IR came from - the authored .html, or the .xml it was read back out of.
    /// A path, not a kind; see <see cref="SourceKind"/> for the kind.</summary>
    [JsonPropertyName("source")] public string Source { get; init; } = "";

    /// <summary>
    /// WHICH PATH produced this IR: <c>"html"</c> (flatten, the forward path) or <c>"simaticml"</c>
    /// (<c>to-ir</c>, the reverse one).
    ///
    /// 🔴 IT IS HERE BECAUSE THE TWO ARE NOT THE SAME DOCUMENT AND MUST NOT BE READ AS ONE.
    /// SimaticML is neither a superset nor a subset of this IR - it is an OVERLAP. It states dozens
    /// of attributes the IR has no field for, and the IR carries a dozen things SimaticML never
    /// states (the CSS font stack, the shadow, the z tier, the declared zone, whether an element was
    /// a leaf). So an IR marked <c>simaticml</c> is PARTIAL BY CONSTRUCTION, and the fields it could
    /// not fill are listed in <see cref="Unpopulatable"/> rather than filled with plausible values.
    ///
    /// ⚠️ CONSEQUENCE FOR THE CHECKERS: <c>lint</c> and <c>check</c> read <c>interactive</c>,
    /// <c>leaf</c>, <c>zone</c> and <c>zTier</c>, none of which survive the round trip - so running
    /// them against a <c>simaticml</c> IR examines a smaller screen than it appears to. That is
    /// exactly the "green over an incomplete denominator" this repo keeps closing, so the fact is
    /// carried IN the artifact rather than left to whoever remembers.
    /// </summary>
    [JsonPropertyName("sourceKind")] public string SourceKind { get; init; } = "html";

    /// <summary>The IR fields the producing path could not fill, each with the reason. Empty on the
    /// forward path; see <see cref="SourceKind"/>.</summary>
    [JsonPropertyName("unpopulatable")] public List<string> Unpopulatable { get; init; } = new();

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
