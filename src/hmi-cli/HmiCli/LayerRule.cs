using System.Globalization;

namespace HmiCli;

/// <summary>
/// A layer's runtime visibility rule, reduced to the only question the geometry checks need to ask:
/// <b>can these two things be on the glass at the same moment?</b>
///
/// <para>
/// This exists because "they are on different layers" was being used as proof that two controls
/// never coexist, and it is not proof of anything. Different layers means keyed on different rules.
/// A dialog's three answer buttons sit on three layers, keyed on three tags, and appear together
/// constantly — so the geometry rules had quietly stopped comparing exactly the controls a popup
/// crowds most tightly, while still printing a full denominator.
/// </para>
/// <para>
/// Exclusivity is provable in one situation only: two layers reading the <b>same tag</b> whose
/// visible sets do not intersect. Everything else must be compared. When in doubt this class says
/// "yes, they can coexist", because a false comparison costs a finding to look at and a false
/// exemption costs a control nobody ever checked.
/// </para>
/// </summary>
internal sealed record LayerRule(string Tag, long Low, long High, bool VisibleInside)
{
    /// <summary>The rule a layer declaration carries, or null when it declares none (pure grouping).</summary>
    public static LayerRule? From(IrItem declaration)
    {
        var show = !string.IsNullOrWhiteSpace(declaration.LayerShowWhen);
        var hide = !string.IsNullOrWhiteSpace(declaration.LayerHideWhen);

        // Both is refused at emit; here it simply is not evidence, so it proves nothing.
        if (show == hide)
        {
            return null;
        }

        var tag = (show ? declaration.LayerShowWhen : declaration.LayerHideWhen)!.Trim();
        var raw = ((show ? declaration.LayerShowRange : declaration.LayerHideRange) ?? string.Empty).Trim();

        var parts = raw.Split("..", StringSplitOptions.None);
        if (parts.Length != 2
            || !long.TryParse(parts[0].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var low)
            || !long.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var high))
        {
            // A malformed range is refused at emit. Until then it is unreadable, not exclusive.
            return null;
        }

        return low <= high ? new LayerRule(tag, low, high, show) : null;
    }

    /// <summary>
    /// Is there any value of the shared tag at which BOTH layers are visible?
    ///
    /// <para>
    /// Three cases, and only the middle one does real work:
    /// <list type="bullet">
    /// <item><b>show / show</b> — the two ranges must overlap.</item>
    /// <item><b>show / hide</b> — the shown range must not sit entirely inside the hidden one. This
    /// is the case that makes a greyed stand-in and its live control provably exclusive.</item>
    /// <item><b>hide / hide</b> — always true. Both ranges are finite and the tag's domain is not,
    /// so a value above both leaves everything visible.</item>
    /// </list>
    /// </para>
    /// </summary>
    public bool CanBeVisibleWith(LayerRule other)
    {
        if (!VisibleInside && !other.VisibleInside)
        {
            return true;
        }

        if (VisibleInside && other.VisibleInside)
        {
            return Math.Max(Low, other.Low) <= Math.Min(High, other.High);
        }

        var shown = VisibleInside ? this : other;
        var hidden = VisibleInside ? other : this;

        return shown.Low < hidden.Low || shown.High > hidden.High;
    }
}
