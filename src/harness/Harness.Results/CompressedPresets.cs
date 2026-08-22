namespace Harness.Results;

/// <summary>What happened to one preset when the compression factor was applied to it.</summary>
public enum PresetScaleOutcome
{
    /// <summary>The zero value, and unusable — an outcome nobody computed cannot be whichever is first.</summary>
    Unstated = 0,

    /// <summary>Scaled. <see cref="ScaledPreset.ScaledMs"/> is the value to deploy.</summary>
    Scaled,

    /// <summary>
    /// The preset is a LITERAL in the block and does not scale at all.
    ///
    /// <para><b>Not an error on its own</b> — the ratio-distortion bound is what governs whether the
    /// surrounding behaviour may be compressed around it. It is reported so that a table of scaled values
    /// cannot be mistaken for a complete account of the block's timing.</para>
    /// </summary>
    Literal,

    /// <summary>
    /// 🔴 <b>Scaling would put this preset under the ruled absolute floor, so it is REFUSED rather than
    /// clamped.</b>
    ///
    /// <para>Clamping is the tempting answer and it is wrong: holding one preset at the floor while its
    /// neighbours scale changes the RATIO between them, which is a different plant, not a faster one. The
    /// floor asks "how short may a timer get before it stops behaving like a timer"; a clamp answers a
    /// question nobody asked and does it silently.</para>
    /// </summary>
    BelowFloor,

    /// <summary>
    /// The preset was already under the floor before any scaling — a debounce or a one-shot that is short
    /// by design.
    ///
    /// <para><b>Distinct from <see cref="BelowFloor"/>, and the distinction is the actionable one:</b>
    /// that outcome says the factor is too high, this one says the preset should probably not have been
    /// declared as gating at all. Lowering the factor fixes the first and never fixes the second.</para>
    /// </summary>
    AlreadyBelowFloor,
}

/// <summary>One preset, and what compression does to it.</summary>
/// <param name="ScaledMs">
/// The value to deploy, when <see cref="Outcome"/> is <see cref="PresetScaleOutcome.Scaled"/>.
/// <b><see cref="double.NaN"/> otherwise</b>, rather than the nominal value — a refused preset that
/// silently carried its original number would deploy as though nothing had happened.
/// </param>
public sealed record ScaledPreset(string Name, double NominalMs, PresetSource Source, PresetScaleOutcome Outcome, double ScaledMs, string Detail)
{
    public bool Deployable => Outcome == PresetScaleOutcome.Scaled;
}

/// <summary>
/// <b>The table a deploy applies — the missing half of time compression.</b>
///
/// <para>🔴 <b>The planner has been complete for a long time and nothing ever applied it.</b>
/// <see cref="TimeCompression.Plan"/> computes <c>comp_min</c> and all four ceilings, gates 10a and 10b
/// check them, <c>ScanBudget</c> and <c>WireTiming.BackstopMs</c> re-express every declared duration at
/// the factor — and <b>no code anywhere changes a preset on the device.</b> The copy layer's own caveat
/// list says "no time compression" outright. So raising <c>runtimeCompression</c> shortened only the
/// harness's PATIENCE, against a plant still running at 1×, and the symptom would have been a spurious
/// TIMED-OUT on a healthy test.</para>
///
/// <para><b>What this produces is a FILE, deliberately.</b> The deploy has to apply concrete values to
/// the block's parameters, and re-deriving a factor at deploy time from a number in another document is
/// how the two drift apart. The table carries the factor it was computed at so the wave and the deploy
/// can be shown to be talking about the same compression.</para>
///
/// <para>⚠️ <b>Only DECLARED presets are scaled, and that is a necessity rather than a simplification.</b>
/// A real block's call tree contains presets that are already below the floor at 1× — debounces, one-shot
/// delays — and a blanket scale over everything refuses immediately on those. The set that gates the
/// behaviour under test is declared by whoever knows the block; nothing here infers it.</para>
/// </summary>
public sealed record CompressedPresetTable(
    int Factor,
    double FloorMs,
    IReadOnlyList<ScaledPreset> Presets)
{
    /// <summary>Presets the deploy must apply. Empty is a legitimate answer only at factor 1.</summary>
    public IReadOnlyList<ScaledPreset> Deployable => Presets.Where(p => p.Deployable).ToArray();

    /// <summary>Presets that could not be scaled. <b>Any of these makes the table unusable.</b></summary>
    public IReadOnlyList<ScaledPreset> Refused =>
        Presets.Where(p => p.Outcome is PresetScaleOutcome.BelowFloor or PresetScaleOutcome.AlreadyBelowFloor).ToArray();

    /// <summary>Presets that are literals and therefore untouched.</summary>
    public IReadOnlyList<ScaledPreset> Literals => Presets.Where(p => p.Outcome == PresetScaleOutcome.Literal).ToArray();

    /// <summary>The largest declared literal, which is the one the ratio-distortion bound keys on. Null when there is none.</summary>
    public double? LargestLiteralMs => Literals.Count == 0 ? null : Literals.Max(p => p.NominalMs);

    /// <summary>The shortest compressed behaviour this table produces. Null when nothing scaled.</summary>
    public double? ShortestScaledMs => Deployable.Count == 0 ? null : Deployable.Min(p => p.ScaledMs);

    /// <summary>
    /// 🔴 <b>X-D's ratio-distortion bound, checked here because this table is the only place that knows
    /// both halves.</b> A literal does not scale, so compressing the behaviour AROUND it changes the
    /// proportion between them rather than the rate of the whole — and past a point the block is running
    /// a different design, not a faster one.
    ///
    /// <para>The ruled companion is <see cref="TimeCompression.LiteralHeadroomMultiple"/>: the compressed
    /// behaviour must stay at least that multiple of the largest PARTICIPATING unscaled literal.
    /// <b>"Participating" is decided by DECLARATION</b> — a literal in this table counts, one nobody
    /// declared does not — which puts the judgement where somebody can see it.</para>
    /// </summary>
    public bool RatioDistorted =>
        LargestLiteralMs is { } literal
        && ShortestScaledMs is { } shortest
        && shortest < literal * TimeCompression.LiteralHeadroomMultiple;

    /// <summary>Why the ratio bound refused, or that it did not bind. Never silent.</summary>
    public string RatioDetail =>
        LargestLiteralMs is not { } literal
            ? "RATIO: no literal was declared, so the ratio-distortion bound does not bind. That is a statement about the DECLARATION, not about the block — an undeclared literal in the call tree is one nothing here can see."
        : ShortestScaledMs is not { } shortest
            ? $"RATIO: nothing scaled, so there is no compressed behaviour to compare against the {literal:0} ms literal."
        : RatioDistorted
            ? $"RATIO REFUSED: the shortest compressed behaviour is {shortest:0.#} ms and the largest declared literal is {literal:0} ms, "
              + $"which needs at least {literal * TimeCompression.LiteralHeadroomMultiple:0} ms of headroom ({TimeCompression.LiteralHeadroomMultiple:0}x). "
              + "A literal does not scale, so compressing around it changes the PROPORTION rather than the rate — past this point the block is "
              + "running a different design and not a faster one."
            : $"RATIO: shortest compressed behaviour {shortest:0.#} ms against a {literal:0} ms literal — clear of the "
              + $"{TimeCompression.LiteralHeadroomMultiple:0}x headroom.";

    /// <summary>
    /// <b>Whether a deploy may use this table.</b> False when any preset was refused — a partially
    /// applied compression is a plant whose timings no longer relate to each other in the declared way —
    /// and false when the ratio-distortion bound binds.
    /// </summary>
    public bool Usable => Refused.Count == 0 && !RatioDistorted && (Factor == 1 || Deployable.Count > 0);

    /// <summary>
    /// The denominator, on every run. <b>Every other number here is a reason a preset was NOT scaled</b>;
    /// this one says how many were examined at all, which is the line that distinguishes a table that
    /// scaled nothing from one that had nothing to scale.
    /// </summary>
    public string Summary =>
        $"EXAMINED: {Presets.Count} declared preset(s) at comp {Factor}, floor {FloorMs:0} ms — "
        + $"{Deployable.Count} scaled, {Literals.Count} literal (unscalable by nature), {Refused.Count} refused.";
}

/// <summary>Applies a compression factor to a block's declared presets. Pure: no device, no clock.</summary>
public static class CompressedPresets
{
    /// <summary>
    /// Scale <paramref name="presets"/> by <paramref name="factor"/>.
    ///
    /// <para><b>A factor of 1 is not a no-op to be skipped</b> — it produces a table stating that every
    /// preset keeps its nominal value, which is what makes "the deploy applied no compression" a positive
    /// claim rather than an absence.</para>
    /// </summary>
    public static CompressedPresetTable For(IReadOnlyList<TimerPreset> presets, int factor)
    {
        ArgumentNullException.ThrowIfNull(presets);

        if (factor < 1)
            throw new ArgumentOutOfRangeException(nameof(factor), factor, "a compression factor below 1 would make the plant SLOWER than real time, which is not what this is for.");

        var floor = TimeCompression.EffectiveTimerFloorMs;

        var scaled = presets.Select(p =>
        {
            if (p.Source == PresetSource.Literal)
            {
                return new ScaledPreset(p.Name, p.PresetMs, p.Source, PresetScaleOutcome.Literal, double.NaN,
                    $"a LITERAL in the block: it does not scale, so the behaviour around it changes proportion rather than rate. "
                    + $"It stays at {p.PresetMs:0} ms and the ratio-distortion bound is what governs whether that is acceptable at comp {factor}.");
            }

            if (p.Source != PresetSource.Data)
            {
                return new ScaledPreset(p.Name, p.PresetMs, p.Source, PresetScaleOutcome.Literal, double.NaN,
                    "the preset's source was never stated, and the two real answers push in OPPOSITE directions — a data preset lowers the "
                    + "timer ceiling, a literal one lowers the ratio-distortion ceiling. Treated as unscalable because there is no fail-safe guess.");
            }

            if (p.PresetMs < floor)
            {
                return new ScaledPreset(p.Name, p.PresetMs, p.Source, PresetScaleOutcome.AlreadyBelowFloor, double.NaN,
                    $"already {p.PresetMs:0} ms at comp 1, below the {floor:0} ms floor before any scaling. *** LOWERING THE FACTOR CANNOT FIX THIS. *** "
                    + "A preset this short is a debounce or a one-shot rather than a behaviour under test, and declaring it as a GATING preset is "
                    + "what needs revisiting.");
            }

            var value = p.PresetMs / factor;

            if (value < floor)
            {
                return new ScaledPreset(p.Name, p.PresetMs, p.Source, PresetScaleOutcome.BelowFloor, double.NaN,
                    $"{p.PresetMs:0} ms / {factor} = {value:0.#} ms, under the ruled {floor:0} ms floor. *** REFUSED RATHER THAN CLAMPED: *** holding this "
                    + $"one at the floor while its neighbours scale changes the RATIO between them, which is a different plant and not a faster one. "
                    + $"The highest factor this preset admits is {Math.Floor(p.PresetMs / floor):0}.");
            }

            return new ScaledPreset(p.Name, p.PresetMs, p.Source, PresetScaleOutcome.Scaled, value,
                $"{p.PresetMs:0} ms / {factor} = {value:0.#} ms, at or above the {floor:0} ms floor.");
        }).ToArray();

        return new CompressedPresetTable(factor, floor, scaled);
    }
}
