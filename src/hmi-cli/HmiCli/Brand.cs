using System.Globalization;
using System.Text.RegularExpressions;

namespace HmiCli;

/// <summary>
/// H-602 / H-603 / H-605 — is a site's colour usable as a panel theme, and what goes on it?
/// </summary>
/// <remarks>
/// <para>
/// Site owners supply a logo and expect their identity on the panel. The constraint is not that
/// branding is unwelcome — it is that <b>colour is the alarm channel</b>, and a brand colour close
/// to red or amber either teaches the operator to ignore it or competes with it. So the question a
/// site's colour has to answer is: can the eye tell this apart from an alarm, instantly, in the
/// dark, at arm's length?
/// </para>
/// <para>
/// The second question is legibility. A brand colour is chosen for a logo on white paper, not for
/// 15 px type behind it on a sunlit panel. Assuming white text works is how a header ends up
/// unreadable in the one condition that matters.
/// </para>
/// </remarks>
public static class Brand
{
    /// <summary>Alarm hues the brand colour must stay clear of: red at 0°, amber at 35°.</summary>
    public const double RedHue = 0;
    public const double AmberHue = 35;
    public const double MinimumSeparationDegrees = 40;
    public const double MinimumContrast = 4.5;

    public sealed record Verdict(
        double Hue,
        double Saturation,
        double Lightness,
        double DistanceFromRed,
        double DistanceFromAmber,
        bool AccentAllowed,
        string ChromeTextColour,
        double ChromeTextContrast,
        IReadOnlyList<string> Findings);

    public static Verdict? Check(string colour)
    {
        var rgb = ParseRgb(colour);
        if (rgb is null)
        {
            return null;
        }

        var (r, g, b) = rgb.Value;
        var (h, sat, l) = ToHsl(r, g, b);

        var dRed = HueDistance(h, RedHue);
        var dAmber = HueDistance(h, AmberHue);
        var findings = new List<string>();

        // A near-grey brand colour cannot collide with an alarm hue no matter where its hue sits,
        // so the separation rule is scoped to colours that actually read as coloured.
        var reads_as_colour = sat >= 0.18;
        var tooCloseToAlarm = reads_as_colour && (dRed < MinimumSeparationDegrees || dAmber < MinimumSeparationDegrees);

        if (tooCloseToAlarm)
        {
            findings.Add($"H-602: hue {h:0}° is only {Math.Min(dRed, dAmber):0}° from the alarm band "
                       + $"(red 0°, amber 35°; {MinimumSeparationDegrees:0}° required). NOT usable as an accent — "
                       + "an operator who learns to ignore this colour has been trained to ignore red. "
                       + "Use it desaturated in chrome only, and tell the site why.");
        }

        // White or black on the brand colour, whichever is legible - and if neither is, that is a
        // finding rather than a coin toss.
        var whiteContrast = Contrast(r, g, b, 1, 1, 1);
        var blackContrast = Contrast(r, g, b, 0, 0, 0);
        var useWhite = whiteContrast >= blackContrast;
        var chromeText = useWhite ? "255, 255, 255" : "0, 0, 0";
        var chromeContrast = Math.Max(whiteContrast, blackContrast);

        if (chromeContrast < MinimumContrast)
        {
            findings.Add($"H-603: best available text contrast on this colour is {chromeContrast:0.0}:1 "
                       + $"({MinimumContrast:0.0} required). Neither white nor black is legible on it. "
                       + "Darken or lighten the chrome colour — a logo colour is chosen for paper, not for a "
                       + "sunlit panel.");
        }

        findings.Add($"H-605: never apply this to a STOP control — that control is red (H-107), and a "
                   + "brand-coloured stop is the one confusion this whole scheme exists to prevent.");

        return new Verdict(h, sat, l, dRed, dAmber, !tooCloseToAlarm, chromeText, chromeContrast, findings);
    }

    private static double HueDistance(double a, double b)
    {
        var d = Math.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }

    private static (double R, double G, double B)? ParseRgb(string colour)
    {
        var text = colour.Trim();

        var hex = Regex.Match(text, @"^#?([0-9a-fA-F]{6})$");
        if (hex.Success)
        {
            var v = hex.Groups[1].Value;
            return (Convert.ToInt32(v[..2], 16) / 255.0,
                    Convert.ToInt32(v.Substring(2, 2), 16) / 255.0,
                    Convert.ToInt32(v.Substring(4, 2), 16) / 255.0);
        }

        var triple = Regex.Match(text, @"(\d+)\D+(\d+)\D+(\d+)");
        if (triple.Success)
        {
            return (int.Parse(triple.Groups[1].Value, CultureInfo.InvariantCulture) / 255.0,
                    int.Parse(triple.Groups[2].Value, CultureInfo.InvariantCulture) / 255.0,
                    int.Parse(triple.Groups[3].Value, CultureInfo.InvariantCulture) / 255.0);
        }

        return null;
    }

    private static (double H, double S, double L) ToHsl(double r, double g, double b)
    {
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

    /// <summary>WCAG relative-luminance contrast ratio.</summary>
    private static double Contrast(double r1, double g1, double b1, double r2, double g2, double b2)
    {
        var l1 = Luminance(r1, g1, b1);
        var l2 = Luminance(r2, g2, b2);
        var hi = Math.Max(l1, l2);
        var lo = Math.Min(l1, l2);
        return (hi + 0.05) / (lo + 0.05);
    }

    private static double Luminance(double r, double g, double b) =>
        0.2126 * Channel(r) + 0.7152 * Channel(g) + 0.0722 * Channel(b);

    private static double Channel(double c) =>
        c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}
