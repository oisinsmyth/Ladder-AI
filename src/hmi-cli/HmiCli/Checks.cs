using System.Globalization;
using System.Text.RegularExpressions;

namespace HmiCli;

/// <summary>
/// What KIND of rule this is, which decides whether an engineer may override it.
/// </summary>
/// <remarks>
/// The house rules exist to guide a generator that has no taste, not to overrule an engineer who
/// does. But "overridable" is not one thing: a screen drawn off the edge of the panel is BROKEN,
/// while green-for-running is a JUDGEMENT this project happens to hold and a site standard may
/// legitimately contradict.
/// </remarks>
public enum RuleClass
{
    /// <summary>The artifact is wrong, not different. Cannot be overridden - there is nothing to prefer.</summary>
    Correctness,

    /// <summary>Safety-bearing. Overridable, but only with a stated reason, and always reported.</summary>
    Safety,

    /// <summary>House taste. The engineer's call; an override needs only the rule ID.</summary>
    Design,
}

public static class RuleClasses
{
    private static readonly Dictionary<string, RuleClass> Map = new(StringComparer.Ordinal)
    {
        // Correctness - a screen that breaks these is broken on any panel, to anyone's taste.
        ["H-501"] = RuleClass.Correctness,   // off-canvas
        ["H-502"] = RuleClass.Correctness,   // sub-pixel geometry
        ["H-505"] = RuleClass.Correctness,   // zero-size / degenerate
        // A box too short for its own text CLIPS THE TEXT. That is not a taste difference: the
        // operator reads a truncated word, or a digit with its descender cut off, and there is no
        // panel or preference on which that is correct. Same class as off-canvas - the screen is
        // broken, not different - so it cannot be overridden away.
        ["H-306"] = RuleClass.Correctness,   // text box shorter than its font needs

        // Safety - about whether a person can operate the thing under pressure.
        ["H-401"] = RuleClass.Safety,        // >= 9 mm interactive
        ["H-403"] = RuleClass.Safety,        // >= 20 mm safety-critical
        ["H-404"] = RuleClass.Safety,        // gap between interactives
        ["H-503"] = RuleClass.Safety,        // interactive overlap
        ["H-107"] = RuleClass.Safety,        // red STOP
        ["H-605"] = RuleClass.Safety,        // never brand-colour a STOP

        // Everything else is design: colour, form, type, branding, alignment.
    };

    public static RuleClass Of(string ruleId) =>
        Map.TryGetValue(ruleId, out var c) ? c : RuleClass.Design;
}

/// <summary>
/// Applies `data-hmi-override` to a finding set. Overridden findings are REMOVED from the gate and
/// REPORTED separately - never silently dropped.
/// </summary>
public static class Overrides
{
    public sealed record Applied(IReadOnlyList<Finding> Remaining, IReadOnlyList<string> Honoured,
                                 IReadOnlyList<Finding> Refused);

    public static Applied Apply(IReadOnlyList<Finding> findings, ScreenIr ir)
    {
        var byIndex = ir.Items.ToDictionary(i => i.Index, i => i.OverrideSpec);
        var remaining = new List<Finding>();
        var honoured = new List<string>();
        var refused = new List<Finding>();

        foreach (var f in findings)
        {
            var spec = f.ItemIndex is { } idx && byIndex.TryGetValue(idx, out var sp) ? sp : null;
            if (string.IsNullOrWhiteSpace(spec) || !Mentions(spec!, f.RuleId))
            {
                remaining.Add(f);
                continue;
            }

            var cls = RuleClasses.Of(f.RuleId);
            var reason = ReasonFrom(spec!);

            if (cls == RuleClass.Correctness)
            {
                // Nothing to prefer here: the artifact is wrong rather than different.
                refused.Add(f with { Message = f.Message + "  [OVERRIDE REFUSED: " + f.RuleId
                                               + " is a CORRECTNESS rule - the screen is broken, not different]" });
                continue;
            }

            if (cls == RuleClass.Safety && string.IsNullOrWhiteSpace(reason))
            {
                refused.Add(f with { Message = f.Message + "  [OVERRIDE REFUSED: " + f.RuleId
                                               + " is SAFETY-bearing and needs a stated reason, e.g. "
                                               + "data-hmi-override=\"" + f.RuleId + ": why\"]" });
                continue;
            }

            honoured.Add($"{f.RuleId} on item {f.ItemIndex} ({cls})"
                         + (string.IsNullOrWhiteSpace(reason) ? "" : $" - {reason}"));
        }

        return new Applied(remaining, honoured, refused);
    }

    private static bool Mentions(string spec, string ruleId) =>
        spec.Contains(ruleId, StringComparison.OrdinalIgnoreCase);

    private static string ReasonFrom(string spec)
    {
        var i = spec.IndexOf(':');
        return i >= 0 && i + 1 < spec.Length ? spec[(i + 1)..].Trim() : string.Empty;
    }
}

/// <summary>T2 - geometry and physical sizing, from the IR. Rules H-4xx and H-5xx of docs/17.</summary>
public static class Linter
{
    public static CheckResult Run(ScreenIr ir, Panel panel)
    {
        var f = new List<Finding>();
        var items = ir.Items;

        foreach (var it in items)
        {
            // H-505's exception: absent geometry is not zero geometry. A Classic SoftKey maps to a
            // physical bezel key and legitimately has no Left/Top/Width/Height at all.
            if (it.Geometryless)
            {
                continue;
            }

            if (it.Width <= 0 || it.Height <= 0)
            {
                f.Add(new Finding("H-505", Severity.Error, $"zero or degenerate size ({it.Width}x{it.Height})", it.Index));
                continue;
            }

            if (!IsInt(it.Left) || !IsInt(it.Top) || !IsInt(it.Width) || !IsInt(it.Height))
            {
                f.Add(new Finding("H-502", Severity.Error,
                    $"sub-pixel geometry ({it.Left:0.##},{it.Top:0.##} {it.Width:0.##}x{it.Height:0.##})", it.Index));
            }

            if (it.Left < -0.5 || it.Top < -0.5 ||
                it.Left + it.Width > ir.CanvasWidth + 0.5 ||
                it.Top + it.Height > ir.CanvasHeight + 0.5)
            {
                f.Add(new Finding("H-501", Severity.Error,
                    $"extends beyond the {ir.CanvasWidth}x{ir.CanvasHeight} screen", it.Index));
            }

            // H-306 - A TEXT BOX MUST BE TALL ENOUGH FOR THE TEXT IN IT.
            //
            // Reported by the owner looking at built screens: "the textboxes have been sized too
            // small compared to the text they hold and has resulted in cut-off text at the bottom."
            // It was systematic, not incidental, and nothing here caught it - the box is a legal
            // size, on the canvas, not overlapping, so every geometry rule passed while the glyphs
            // were clipped.
            //
            // THE FLOORS ARE MEASURED FROM A REAL TIA EXPORT, NOT REASONED FROM FONT METRICS.
            // Height / FontSize across 66 text-bearing objects:
            //     TextField  n=37  min 1.35 (23/17)  median 1.54
            //     IOField    n=23  min 1.92          median 1.92   max 2.00
            //     Button     n=6   min 2.67
            // A real IOField is nearly TWICE its font size - the field carries margins, a border and
            // a focus rectangle that a plain label does not. Screens authored here sat at 1.41, which
            // is why the values clipped worst.
            //
            // The floor is the corpus MINIMUM rather than its median: below it is a size no real
            // screen uses, which is a defensible line. Above it is a judgement about density, and
            // that is the author's.
            var minRatio = it.Type switch
            {
                "IOField" => 1.92,
                "Button" => 1.35,   // a button's own corpus floor is 2.67, but that reflects touch
                                    // sizing (H-402) rather than legibility, and H-401/H-402 already
                                    // bind it far above this. Held at the text floor so the two
                                    // rules do not contradict each other.
                _ => 1.35,
            };

            if (it.FontSizePx >= 1 && !string.IsNullOrWhiteSpace(it.Text) || it.Type == "IOField")
            {
                var font = it.FontSizePx >= 1 ? it.FontSizePx : 15;
                var needed = Math.Ceiling(font * minRatio);
                if (it.Height + 0.5 < needed)
                {
                    f.Add(new Finding("H-306", Severity.Error,
                        $"{it.Height:0} px tall for a {font:0} px font - needs >= {needed:0} "
                        + $"({minRatio:0.00}x, the smallest a real {it.Type} uses). Text clips at the bottom.",
                        it.Index));
                }
            }

            if (it.Interactive)
            {
                // H-406: the MINOR axis uses the VERTICAL px/mm, which is the tighter one on a
                // non-square-pixel panel. Using one scalar here understates height by ~7% and
                // height is what a touch target usually has least of.
                var wMm = panel.PxToMmH(it.Width);
                var hMm = panel.PxToMmV(it.Height);
                var minorMm = Math.Min(wMm, hMm);

                var floor = it.SafetyCritical ? 20.0 : 9.0;
                var rule = it.SafetyCritical ? "H-403" : "H-401";

                if (minorMm < floor)
                {
                    f.Add(new Finding(rule, Severity.Error,
                        $"interactive target {wMm:0.0} x {hMm:0.0} mm ({it.Width:0}x{it.Height:0} px) - "
                        + $"minor axis {minorMm:0.0} mm is below the {floor:0} mm floor on {panel.Name}", it.Index));
                }
            }
        }

        // H-109 - an accent is a MARKER, not a FILL. This is the half of the rule that actually
        // separates a label from a coloured region, and it needs the accent paired to the control
        // it marks (data-hmi-accent-for). Without that pairing the check CANNOT RUN, and saying so
        // is the point: an unrunnable check reported as a pass is the failure this repo keeps
        // closing.
        var byId = items.Where(i => !string.IsNullOrWhiteSpace(i.ElementId))
                        .GroupBy(i => i.ElementId!)
                        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var it in items.Where(i => !string.IsNullOrWhiteSpace(i.AccentRole)))
        {
            if (string.IsNullOrWhiteSpace(it.AccentFor))
            {
                f.Add(new Finding("H-109", Severity.Error,
                    $"'{it.AccentRole}' accent does not declare which control it marks "
                    + "(data-hmi-accent-for). The marker-not-fill check cannot run without it.", it.Index));
                continue;
            }

            if (!byId.TryGetValue(it.AccentFor!, out var control))
            {
                f.Add(new Finding("H-109", Severity.Error,
                    $"'{it.AccentRole}' accent points at '{it.AccentFor}', which is not on this screen", it.Index));
                continue;
            }

            var accentArea = it.Width * it.Height;
            var controlArea = control.Width * control.Height;
            if (controlArea <= 0)
            {
                continue;
            }

            var share = accentArea / controlArea;
            if (share > CommandAccents.MaxShareOfControl)
            {
                f.Add(new Finding("H-109", Severity.Error,
                    $"'{it.AccentRole}' accent covers {share:P0} of '{it.AccentFor}' "
                    + $"(max {CommandAccents.MaxShareOfControl:P0}). That is a FILL, not a marker - a "
                    + "coloured region competes with the alarm palette, a small marker reads as a label.",
                    it.Index));
            }
        }

        // H-503 - interactive overlap. Deliberately restricted to interactive pairs: a decorative
        // overlap is often intentional, and the prototype's any-pair version false-positived seven
        // times on one arm where the overlap was the design.
        var inter = items.Where(i => i.Interactive && !i.Geometryless).ToList();
        for (var a = 0; a < inter.Count; a++)
        {
            for (var b = a + 1; b < inter.Count; b++)
            {
                if (Overlaps(inter[a], inter[b]))
                {
                    f.Add(new Finding("H-503", Severity.Error,
                        $"interactive items {inter[a].Index} and {inter[b].Index} overlap", inter[a].Index));
                }
            }
        }

        // H-404 - gap between interactives. Same axis-aware conversion as H-401.
        var minGapH = panel.MmToPxH(3.0);
        var minGapV = panel.MmToPxV(3.0);
        for (var a = 0; a < inter.Count; a++)
        {
            for (var b = a + 1; b < inter.Count; b++)
            {
                if (Overlaps(inter[a], inter[b]))
                {
                    continue;
                }

                var gap = SeparationPx(inter[a], inter[b], out var horizontal);
                var need = horizontal ? minGapH : minGapV;
                if (gap < need)
                {
                    var mm = horizontal ? panel.PxToMmH(gap) : panel.PxToMmV(gap);
                    f.Add(new Finding("H-404", Severity.Error,
                        $"items {inter[a].Index} and {inter[b].Index} are {mm:0.0} mm apart - below the 3 mm floor", inter[a].Index));
                }
            }
        }

        // H-504 - alignment near-miss. An edge within 3 px of a line shared by >=2 other items is
        // either on it or deliberately off it; 1-2 px is neither.
        f.AddRange(AlignmentNearMisses(items, i => i.Left, "left"));
        f.AddRange(AlignmentNearMisses(items, i => i.Top, "top"));

        return new CheckResult("lint", items.Count, f);
    }

    private static IEnumerable<Finding> AlignmentNearMisses(List<IrItem> items, Func<IrItem, double> edge, string name)
    {
        var live = items.Where(i => !i.Geometryless).ToList();
        var clusters = live.GroupBy(i => (int)Math.Round(edge(i)))
                           .Where(g => g.Count() >= 2)
                           .Select(g => g.Key)
                           .ToHashSet();

        foreach (var it in live)
        {
            var v = (int)Math.Round(edge(it));
            if (clusters.Contains(v))
            {
                continue;
            }

            foreach (var c in clusters)
            {
                var d = Math.Abs(v - c);
                if (d is >= 1 and <= 3)
                {
                    yield return new Finding("H-504", Severity.Error,
                        $"{name} edge {v} is {d} px from an alignment line at {c} shared by 2+ items", it.Index);
                    break;
                }
            }
        }
    }

    private static bool IsInt(double v) => Math.Abs(v - Math.Round(v)) < 0.01;

    private static bool Overlaps(IrItem a, IrItem b) =>
        a.Left < b.Left + b.Width && b.Left < a.Left + a.Width &&
        a.Top < b.Top + b.Height && b.Top < a.Top + a.Height;

    private static double SeparationPx(IrItem a, IrItem b, out bool horizontal)
    {
        var dx = Math.Max(0, Math.Max(a.Left - (b.Left + b.Width), b.Left - (a.Left + a.Width)));
        var dy = Math.Max(0, Math.Max(a.Top - (b.Top + b.Height), b.Top - (a.Top + a.Height)));
        horizontal = dx >= dy;
        return horizontal ? dx : dy;
    }
}

/// <summary>T3 - house-rule conformance, machine-scored. Rules H-1xx to H-3xx of docs/17.</summary>
public static class StyleChecker
{
    public static CheckResult Run(ScreenIr ir)
    {
        var f = new List<Finding>();
        var examined = 0;

        foreach (var it in ir.Items)
        {
            examined++;

            // H-601: the brand colour is permitted in CHROME. Chrome identifies the plant; alarms do
            // not live there, so colour costs the operator nothing. The zone is DECLARED because
            // geometry cannot reveal it and guessing would be worse than not checking.
            var isChrome = string.Equals(it.Zone, "chrome", StringComparison.OrdinalIgnoreCase);

            // H-108: a COMMAND ACCENT is about which control this is, not what the plant is doing.
            // It is static and small, which is exactly why it does not compete with the alarm
            // palette - see H-109 for the size half, checked separately.
            var accent = CommandAccents.Role(it.AccentRole);
            if (accent is not null)
            {
                var hsl = Hsl.Parse(it.BackColor);
                if (hsl is null)
                {
                    f.Add(new Finding("H-108", Severity.Error,
                        $"declared as the '{it.AccentRole}' command accent but has no readable colour", it.Index));
                }
                else if (!accent.Value.Accepts(hsl.Value.H))
                {
                    f.Add(new Finding("H-108", Severity.Error,
                        $"'{it.AccentRole}' accent is hue {hsl.Value.H:0}deg; {accent.Value.Name} expects "
                        + $"{accent.Value.Low:0}-{accent.Value.High:0}deg. Command accents are fixed by FUNCTION "
                        + "and identical on every screen - an operator learns them once.", it.Index));
                }
            }

            foreach (var (colour, where) in new[]
                     {
                         (it.BackColor, "background"), (it.ForeColor, "text"), (it.BorderColor, "border"),
                     })
            {
                // Chrome and command accents are exempt from the colour rules by design, not by
                // oversight: H-102/H-104/H-105 govern the PROCESS AREA and STATE.
                if (isChrome || accent is not null)
                {
                    continue;
                }

                var hsl = Hsl.Parse(colour);
                if (hsl is null)
                {
                    continue;
                }

                var h = hsl.Value.H;
                var s = hsl.Value.S;

                if (s < 0.18)
                {
                    continue; // grey enough to carry no colour meaning
                }

                // H-104 - green for running. The single most common unguided violation.
                if (h is >= 80 and <= 165)
                {
                    f.Add(new Finding("H-104", Severity.Error,
                        $"{where} colour {colour} is green (hue {h:0}) - running is the normal state and by H-102 it is grey", it.Index));
                }
                else if (!(h <= 25 || h >= 335 || (h > 25 && h <= 55)))
                {
                    // H-105 - anything saturated that is not red (alarm), amber (warning) or grey.
                    // The STOP carve-out (H-107) is red and so lands in the allowed band already.
                    f.Add(new Finding("H-105", Severity.Error,
                        $"{where} colour {colour} is an accent hue ({h:0}) - colour is reserved for the abnormal", it.Index));
                }
            }

            if (!string.IsNullOrWhiteSpace(it.BackgroundImage) && it.BackgroundImage != "none")
            {
                f.Add(new Finding("H-201", Severity.Error, $"gradient or background image ({it.BackgroundImage})", it.Index));
            }

            if (IsSet(it.BoxShadow))
            {
                f.Add(new Finding("H-202", Severity.Error, $"box shadow ({it.BoxShadow})", it.Index));
            }

            if (IsSet(it.TextShadow))
            {
                f.Add(new Finding("H-202", Severity.Error, $"text shadow ({it.TextShadow})", it.Index));
            }

            // H-203 forbids ROUNDED CORNERS on rectangular controls. It must not fire on a Circle:
            // `border-radius: 50%` is simply HOW a circle is written in CSS, and flagging it told
            // the author to un-round a circle. A rule that fires on the correct way to do something
            // trains people to ignore it.
            if (it.Type != "Circle" && IsSet(it.BorderRadius) && it.BorderRadius != "0px")
            {
                f.Add(new Finding("H-203", Severity.Error, $"corner radius ({it.BorderRadius})", it.Index));
            }

            // H-205 carries a carve-out - motion IS allowed on an unacknowledged alarm - and the
            // first build of this checker did not implement it, making the tool stricter than the
            // rule it cites. An element must DECLARE the exception (data-hmi-alarm) to claim it;
            // undeclared motion is still a finding, because "it's the alarm flash" is exactly the
            // explanation an undeclared decorative animation would also offer.
            if (IsSet(it.AnimationName) && !it.AlarmFlash)
            {
                f.Add(new Finding("H-205", Severity.Error,
                    $"animation '{it.AnimationName}' - motion is reserved for unacknowledged alarms "
                    + "(mark the element data-hmi-alarm if that is what this is)", it.Index));
            }
        }

        return new CheckResult("style-check", examined, f);
    }

    private static bool IsSet(string? v) =>
        !string.IsNullOrWhiteSpace(v) && v != "none" && v != "normal" && v != "0px";
}

/// <summary>
/// H-108 - the three command accents, fixed by function and constant across every screen and
/// project. An operator learns three markers once; varying them per project would be the same
/// mistake as varying what red means.
/// </summary>
public static class CommandAccents
{
    public readonly record struct Band(string Name, double Low, double High)
    {
        /// <summary>
        /// Hue is a CIRCLE, and red sits on the seam. A band of 340-375 means "340 through 15",
        /// so a hue of 5 is inside it — but only if the comparison wraps.
        ///
        /// The first version did not wrap. It carried a comment saying it did, and then rejected
        /// rgb(176, 42, 30) — a textbook red — as "hue 5deg, red expects 340-375deg". A comment
        /// describing behaviour the code does not have is worse than no comment, because it stops
        /// the reader looking.
        /// </summary>
        public bool Accepts(double hue)
        {
            var h = ((hue % 360) + 360) % 360;
            return (h >= Low && h <= High) || (High > 360 && h + 360 >= Low && h + 360 <= High);
        }
    }

    private static readonly Dictionary<string, Band> Roles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["start"] = new Band("green", 90, 165),
        ["run"] = new Band("green", 90, 165),
        ["stop"] = new Band("red", 340, 375),        // wraps the 0 seam - see Band.Accepts
        ["estop"] = new Band("red", 340, 375),
        ["reset"] = new Band("light blue", 185, 225),
        ["ack"] = new Band("light blue", 185, 225),
    };

    public static Band? Role(string? role)
    {
        if (string.IsNullOrWhiteSpace(role) || !Roles.TryGetValue(role.Trim(), out var b))
        {
            return null;
        }

        return b;
    }

    /// <summary>H-109: an accent is a MARKER, not a FILL. Above this share of the control it stops
    /// reading as a label and becomes a coloured region competing with the alarm palette.</summary>
    public const double MaxShareOfControl = 0.15;
}

public static class Hsl
{
    private static readonly Regex Rgb = new(@"rgba?\(\s*([\d.]+)[,\s]+([\d.]+)[,\s]+([\d.]+)(?:[,/\s]+([\d.]+))?\s*\)", RegexOptions.Compiled);

    public static (double H, double S, double L)? Parse(string? css)
    {
        if (string.IsNullOrWhiteSpace(css))
        {
            return null;
        }

        var m = Rgb.Match(css);
        if (!m.Success)
        {
            return null;
        }

        var r = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) / 255.0;
        var g = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) / 255.0;
        var b = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture) / 255.0;
        var a = m.Groups[4].Success ? double.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 1.0;

        if (a < 0.05)
        {
            return null; // fully transparent carries no colour
        }

        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));
        var l = (max + min) / 2.0;
        double h = 0, s = 0;

        if (Math.Abs(max - min) > 1e-9)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2.0 - max - min) : d / (max + min);
            if (Math.Abs(max - r) < 1e-9)
            {
                h = ((g - b) / d + (g < b ? 6 : 0)) * 60;
            }
            else if (Math.Abs(max - g) < 1e-9)
            {
                h = ((b - r) / d + 2) * 60;
            }
            else
            {
                h = ((r - g) / d + 4) * 60;
            }
        }

        return (h, s, l);
    }
}
