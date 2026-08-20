using Harness.Wire;

namespace Harness.Results;

/// <summary>
/// Which of X-D's ceilings a bound came from. <b><see cref="Unstated"/> is the zero value and is
/// unusable</b>, so a bound nobody classified cannot be silently filed under whichever member happened to
/// be first.
/// </summary>
public enum CompressionBoundKind
{
    /// <summary>Nothing said which ceiling this is. <c>default(CompressionBoundKind)</c> is deliberately not a real one.</summary>
    Unstated = 0,

    /// <summary>Per assertion: <c>T_event / scan_period</c> if latched, <c>T_event / poll_period</c> if sampled.</summary>
    Assertion,

    /// <summary>
    /// Per timer: <c>PT / floor</c>, where the floor is the RULED absolute
    /// <see cref="TimeCompression.AbsoluteTimerFloorMs"/> (2026-08-18), which subsumes the scan-derived
    /// <c>k x scan_period</c>. <b>X-D says this one often binds first, and under the absolute floor it
    /// binds harder.</b>
    /// </summary>
    Timer,

    /// <summary>The model's own declared <c>comp_stable</c> (M3/M4).</summary>
    Model,

    /// <summary>
    /// The third bound, from the 0.4 sweep: a preset that is a LITERAL does not scale, so compressing the
    /// behaviours around it changes the PROPORTION rather than the logic.
    ///
    /// <para><b>Its arithmetic changed on 2026-08-18 and its name did not.</b> The threshold is now the
    /// RULED <see cref="TimeCompression.LiteralHeadroomMultiple"/> — the compressed behaviour must stay at
    /// least 10x the literal — rather than a caller-supplied <c>negligibleFraction</c> the specification
    /// never gave a value for. The kind is unchanged so that results and reports written against it keep
    /// meaning the same thing.</para>
    /// </summary>
    RatioDistortion,
}

/// <summary>Whether a ceiling was computed, or could not be. <b>Never "assumed not to bind".</b></summary>
public enum CompressionBoundState
{
    /// <summary>The zero value, and unusable.</summary>
    Unstated = 0,

    /// <summary>A number was computed from a declared input.</summary>
    Computed,

    /// <summary>
    /// The input this ceiling needs was not supplied. <b>It is NOT a ceiling of infinity</b> — a bound
    /// nobody computed constrains nothing, and treating that as "unbounded" is the permissive default this
    /// whole enum exists to refuse.
    /// </summary>
    NotDeclared,
}

/// <summary>One of X-D's ceilings, with the arithmetic that produced it.</summary>
/// <param name="CompMax">
/// The ceiling, when <see cref="State"/> is <see cref="CompressionBoundState.Computed"/>.
/// <b><see cref="double.NaN"/> when it is not</b>, rather than a large number that would silently win a
/// <c>Min</c>.
/// </param>
public sealed record CompressionBound(CompressionBoundKind Kind, CompressionBoundState State, string Subject, double CompMax, string Detail)
{
    public bool Binds => State == CompressionBoundState.Computed;

    public static CompressionBound NotDeclared(CompressionBoundKind kind, string subject, string detail) =>
        new(kind, CompressionBoundState.NotDeclared, subject, double.NaN, detail);
}

/// <summary>The plan's verdict. There is no "probably fine".</summary>
public enum CompressionOutcome
{
    /// <summary>The zero value, and unusable.</summary>
    Unstated = 0,

    /// <summary><c>comp_min &lt;= comp_max</c>. The wave fits its budget without pushing anything below a floor.</summary>
    Runnable,

    /// <summary><c>comp_min &gt; comp_max</c>. <b>REFUSED SHOWING BOTH NUMBERS</b> — that refusal is a model task or a test-design task.</summary>
    Refused,

    /// <summary>
    /// A ceiling this plan needs could not be computed at all, so <c>comp_max</c> is unknown. <b>Not
    /// runnable</b>: an unknown ceiling is not a high one.
    /// </summary>
    NotComputable,
}

/// <summary>Where a timer's preset comes from, which decides whether compression scales it.</summary>
public enum PresetSource
{
    /// <summary>
    /// Nothing said. <b>Refused rather than guessed</b> — the two real answers push in OPPOSITE directions
    /// (a data preset lowers the timer ceiling, a literal one lowers the ratio-distortion ceiling), so
    /// there is no fail-safe guess available.
    /// </summary>
    Unstated = 0,

    /// <summary>The preset is DATA (DB-12's requirement), so the factor scales it and X-D's timer bound applies.</summary>
    Data,

    /// <summary>The preset is a LITERAL in the block. It does not scale at all, and it feeds the ratio-distortion bound.</summary>
    Literal,
}

/// <summary>One dwell/debounce preset in the block under test.</summary>
public sealed record TimerPreset(string Name, double PresetMs, PresetSource Source);

/// <summary>
/// Everything X-D's arithmetic needs. <b>Nothing here has a default</b>: every input is a decision, and a
/// defaulted one is a decision nobody made.
/// </summary>
/// <param name="PlantMs">
/// <c>T_plant</c> — how long the behaviour under test takes in the plant.
/// </param>
/// <param name="BudgetMs">
/// <c>T_budget</c> — how long the wave may spend on it. Together these give <c>comp_min</c>, and
/// <b>comp_min is the number to use</b>.
/// </param>
/// <param name="Expectations">The vector's observability declarations. Each contributes an assertion ceiling.</param>
/// <param name="DeclaredCompression">The <c>comp</c> the expectations' window counts were stated at.</param>
/// <param name="SlotsPerPollCycle">
/// <c>K</c> — reads per poll cycle. The SAMPLED poll period is <c>K x RTT_p99</c>, so the sampled ceiling
/// falls as a wave set gets wider. <b>Keyed on the p99 because a ceiling is BOUND-shaped</b> (§12a F-5).
/// </param>
/// <param name="Presets">The block's dwell presets, each declared DATA or LITERAL.</param>
/// <param name="ModelCompStable">
/// The model's declared <c>comp_stable</c>. <b>Null is treated by
/// <see cref="TimeCompression.Plan"/> and the treatment depends on whether compression is actually being
/// applied</b> — see there.
/// </param>
/// <param name="NegligibleFraction">
/// 🔴 <b>SUPERSEDED 2026-08-18 AND NO LONGER CONSULTED. Kept so submissions written against it still
/// parse; <see cref="TimeCompression.Plan"/> reports on its own detail line that it was ignored.</b>
///
/// <para>It was the ratio-distortion threshold: how large an UNSCALED literal may become as a fraction of
/// the shortest compressed behaviour. <b>The specification names no value for it</b> — it works an example
/// (0.003% becoming 2.5%) and calls the good state "negligible" without saying where negligible ends — so
/// it was a required input with no default, and a plan that omitted it was refused. Two costs followed: a
/// number invented per submission, and a run-hour totaliser in the call tree driving the ceiling to
/// 0.0006x, which forced an undeclarable judgement about which literals "participate".</para>
///
/// <para>The replacement is <see cref="TimeCompression.AbsoluteTimerFloorMs"/> — <i>no timer preset may be
/// compressed below ~500 ms</i> — with <see cref="TimeCompression.LiteralHeadroomMultiple"/> as the
/// companion for unscaled literals. Both are RULED, so neither is declared per submission.</para>
/// </param>
/// <param name="RuntimeCompression">
/// *** THE FACTOR THE WAVE ACTUALLY RUNS AT, AND THE ONE THE CONTRACT RULES ON. ***
///
/// <para>Required, because without it this arithmetic asked the wrong question. Every "is anything being
/// scaled?" branch below used to key on <c>comp_min</c> — which is derived from
/// <c>T_plant / T_budget</c> and says what the plan NEEDS, not what the wave DOES. <b>A wave at
/// <c>runtimeCompression = 8</c> whose budget happens to give <c>comp_min = 1</c> therefore passed with no
/// <c>comp_stable</c> declared at all</b>: the model was being driven at 8x and the plan reported that
/// nothing was being scaled.</para>
///
/// <para>The branches now key on <c>max(comp_min, runtime)</c>. Taking the maximum rather than the runtime
/// alone can only make the check stricter — a plan needing 240x is still asking a model to run at 240x
/// even if somebody set the wave to 1.</para>
/// </param>
public sealed record CompressionRequest(
    double PlantMs,
    double BudgetMs,
    IReadOnlyList<ObservabilityDeclaration> Expectations,
    int DeclaredCompression,
    int SlotsPerPollCycle,
    IReadOnlyList<TimerPreset> Presets,
    double? ModelCompStable,
    double? NegligibleFraction,
    int RuntimeCompression);

/// <summary>
/// The three X-D ceilings that are properties of the BLOCK and the MODEL rather than of the vectors —
/// <b>the inputs contract §2 gives an author nowhere to state.</b>
///
/// <para>It exists so that gate 10b names a remedy that can actually be supplied. A gate whose refusal
/// says "compute them and supply them" against an API with no way to supply them is a dead end wearing the
/// costume of a build list.</para>
/// </summary>
/// <param name="PlantMs">How long the behaviour under test takes in the plant, for <c>comp_min</c>.</param>
/// <param name="BudgetMs">How long the wave may spend on it.</param>
/// <param name="NegligibleFraction">
/// 🔴 <b>SUPERSEDED 2026-08-18 AND NO LONGER CONSULTED</b> — see <see cref="CompressionRequest"/>'s
/// parameter of the same name. Still carried so a submission written before the ruling parses; the plan
/// says out loud that a declared value was not used.
/// </param>
public sealed record BlockCompressionInputs(
    double? PlantMs,
    double? BudgetMs,
    IReadOnlyList<TimerPreset> Presets,
    double? ModelCompStable,
    double? NegligibleFraction)
{
    /// <summary>
    /// Fields that are missing, or empty. <b>An incomplete object must reach gate 10b as INCOMPLETE.</b>
    ///
    /// <para><c>PlantMs</c> and <c>BudgetMs</c> used to be non-nullable doubles, so an omitted pair
    /// arrived as <c>0</c>, <see cref="TimeCompression.MinimumFor"/> threw, and the CLI caught it as an
    /// unreadable document — <b>NOTHING EXAMINED</b>. Both directions fail closed, so nothing was ever
    /// admitted wrongly; what was wrong is the DIAGNOSIS. The operator was told the document could not be
    /// read when the document was fine and one number was missing.</para>
    /// </summary>
    public IReadOnlyList<string> Missing
    {
        get
        {
            var missing = new List<string>();

            if (PlantMs is not { } plant || double.IsNaN(plant) || plant <= 0)
                missing.Add("plantMs (how long the behaviour takes in the plant) — comp_min is T_plant / T_budget and cannot be formed without it");

            if (BudgetMs is not { } budget || double.IsNaN(budget) || budget <= 0)
                missing.Add("budgetMs (how long the wave may spend on it) — a budget of zero admits no test, and dividing by it would report an infinite comp_min as though it were a number");

            return missing;
        }
    }
}

/// <summary>
/// The whole X-D verdict. <b>There is no member that recommends <c>comp_max</c>.</b>
/// </summary>
/// <param name="CompMin">The LEAST compression that fits the budget. <b>This is the one to run at.</b></param>
/// <param name="CompMax">The least of the computed ceilings, or <see cref="double.NaN"/> when a needed ceiling was not computable.</param>
public sealed record CompressionPlan(
    CompressionOutcome Outcome,
    double CompMin,
    double CompMax,
    CompressionBoundKind BindingBound,
    IReadOnlyList<CompressionBound> Bounds,
    string Detail)
{
    /// <summary>
    /// <b>USE comp_min, NOT comp_max</b> (X-D, verbatim). Compression is a fidelity risk, so take the least
    /// that meets the budget and bank the remainder as margin.
    ///
    /// <para>This property returns <see cref="CompMin"/> and there is deliberately nothing on this type
    /// that returns <see cref="CompMax"/> as a recommendation. Running at the ceiling merely because the
    /// ceiling permits it is the failure X-D's rule exists to prevent, and the rule is now carried by the
    /// SHAPE OF THE TYPE rather than by a sentence somebody has to remember.</para>
    /// </summary>
    public double Recommended => CompMin;

    /// <summary>The ceilings that could NOT be computed. Named, because an uncomputed ceiling is not an absent one.</summary>
    public IReadOnlyList<CompressionBound> NotDeclared => Bounds.Where(b => b.State == CompressionBoundState.NotDeclared).ToArray();

    public bool Runnable => Outcome == CompressionOutcome.Runnable;

    /// <summary>The refusal X-D asks for: <b>both numbers, always</b>, so the reader can see which side to move.</summary>
    public string Render() =>
        $"{Outcome.ToString().ToUpperInvariant()} — comp_min = {CompMin:0.##}, comp_max = "
        + (double.IsNaN(CompMax) ? "NOT COMPUTABLE" : $"{CompMax:0.##} (binding: {BindingBound})")
        + $". Recommended: {Recommended:0.##} — X-D's rule is USE comp_min, NOT comp_max. {Detail}";
}

/// <summary>
/// <b>Build-plan 6.6 — X-D's <c>comp_min</c> calculation, and the four ceilings it must not exceed.</b>
///
/// <para>Two parts of the design pull against each other and neither mentions the other: DB-12 requires
/// models to run COMPRESSED so long tests are runnable at all, and §12 establishes that polling observes at
/// one round trip per slot against a 24.931 ms scan. <b>Compression shortens the real-time separation of the
/// events being observed</b>, so past a point it pushes an assertion below the sampling floor and
/// <i>every check still reports green because the assertion was simply never sampled.</i></para>
///
/// <para>🔴 <b>THE TIMER TERM IS NOW AN ABSOLUTE FLOOR, RULED BY THE PROJECT OWNER 2026-08-18: NO TIMER
/// PRESET MAY BE COMPRESSED BELOW ~500 ms.</b> It replaces the ratio-distortion FRACTION as the binding
/// constraint, and it is a different QUESTION rather than a different number — the fraction asked <i>what
/// proportion of the behaviour may a fixed timer occupy</i>, the floor asks <i>how short may a timer get
/// before it stops behaving like a timer</i>, which is the question the hardware answers.
///
/// <b>It SUBSUMES both measured floors</b> — <c>k x scan = 5 x 24.931 = 124.7 ms</c> and the p99
/// sampled-observability floor — so it is the single term that binds, and
/// <see cref="TimeCompression.EffectiveTimerFloorMs"/> takes the maximum rather than trusting that
/// ordering to hold forever. <b>On a 2-second shortest preset <c>comp_max(timer)</c> is 4.0x</b>, against
/// the 4.3x the scan-derived floor gave and the 10x X-D originally assumed.
///
/// <b>The companion is ruled too:</b> the compressed behaviour must remain at least 10x the largest
/// PARTICIPATING unscaled literal (<see cref="TimeCompression.LiteralHeadroomMultiple"/>), which catches
/// the case the floor alone cannot see — a literal does not scale, so compressing around it changes the
/// proportion rather than the logic. <b>What this dissolves:</b> under the fraction rule a run-hour
/// totaliser in the call tree drove the ceiling to 0.0006x, forcing an undeclarable judgement about which
/// literals participate.
///
/// ⚠️ <b><c>k = 5</c> IS NOW RATIFIED RATHER THAN ASSUMED, AND RATIFIED IS STILL NOT MEASURED.</b> Figures
/// derived from it may be quoted as RULED and not as measured; what would settle it remains a preset
/// scaled to five scans on a 1214C, observed still behaving like a timer. It no longer decides anything on
/// its own, since 500 ms is four times larger.</para>
///
/// <para><b>Every constant is read from <c>WireTiming</c>, which transcribes §12a.</b> Nothing is chosen
/// here. The two that matter take different bands and the difference is not a style choice: the poll period
/// is <c>K x RTT_p99</c> because <b>a ceiling is BOUND-shaped</b> — sizing it with <c>RTT_p90</c> would
/// permit a compression at which 10% of poll cycles no longer resolve the behaviour, and the symptom is a
/// green that was never sampled.</para>
/// </summary>
public static class TimeCompression
{
    /// <summary>
    /// X-D's <c>k</c>: how many scan periods a preset must remain above once scaled. <b>k = 5 —
    /// RATIFIED BY THE PROJECT OWNER, 2026-08-18.</b> A preset scaled below a few scan times stops behaving
    /// like a timer — it rounds toward zero, and the block then passes or fails for reasons unrelated to
    /// its logic.
    ///
    /// <para>⚠️ <b>RATIFIED IS NOT MEASURED, and the distinction is kept.</b> This was carried as
    /// <c>[A] — ASSUMED</c> because it is X-D's number and nothing had measured it; it is now a DECIDED
    /// constant rather than an open assumption, so figures derived from it may be quoted as RULED. They
    /// still may not be quoted as measured, and what would settle it remains the same experiment: a preset
    /// scaled to five scans on a 1214C, observed still behaving like a timer.</para>
    ///
    /// <para><b>It no longer decides anything on its own.</b> Since 2026-08-18 the binding term is
    /// <see cref="AbsoluteTimerFloorMs"/>, which subsumes <c>k x scan</c> at every scan period this rig has
    /// produced — see <see cref="EffectiveTimerFloorMs"/>.</para>
    /// </summary>
    public const int TimerScanMultiple = 5;

    /// <summary>
    /// 🔴 <b>THE ABSOLUTE TIMER FLOOR: NO TIMER PRESET MAY BE COMPRESSED BELOW ~500 ms. RULED BY THE
    /// PROJECT OWNER, 2026-08-18, AND IT REPLACES THE RATIO-DISTORTION FRACTION AS THE BINDING
    /// CONSTRAINT.</b>
    ///
    /// <para><b>It is a different QUESTION, not a different number.</b> The fraction rule asked <i>"what
    /// proportion of the behaviour under test may a fixed timer occupy?"</i> — an arithmetic property of
    /// the test. This asks <i>"how short may a timer get before it stops behaving like a timer?"</i>, which
    /// is the question the HARDWARE actually answers, and the one a scan-period floor was already reaching
    /// for.</para>
    ///
    /// <para><b>It SUBSUMES both measured floors, which is why it is the single binding constraint:</b>
    /// <c>k x scan = 5 x 24.931 = 124.7 ms</c>, and the sampled-observability floor at the p99. 500 is above
    /// both with margin, so a plan cleared here has cleared them — and <see cref="EffectiveTimerFloorMs"/>
    /// takes the MAXIMUM rather than assuming the ordering, so a rig whose scan period ever exceeds 100 ms
    /// follows the physics instead of silently keeping a number that has stopped being conservative.</para>
    ///
    /// <para><b>What it dissolves.</b> Under the fraction rule a run-hour totaliser sitting in the call
    /// tree drove the ceiling to 0.0006x, which forced somebody to declare — with nothing to declare it
    /// from — which literals "participate". The floor asks nothing about literals at all; the companion
    /// (<see cref="LiteralHeadroomMultiple"/>) restores the one case the floor alone would miss, at a ruled
    /// multiple rather than an invented fraction.</para>
    ///
    /// <para><b>Worked, so it can be checked:</b> a 2-second shortest preset caps compression at
    /// <c>2000 / 500 = 4.0x</c>.</para>
    /// </summary>
    public const double AbsoluteTimerFloorMs = 500.0;

    /// <summary>
    /// 🔴 <b>THE COMPANION TO THE FLOOR: the compressed behaviour must remain at least 10x the largest
    /// PARTICIPATING unscaled literal.</b> RULED 2026-08-18, alongside <see cref="AbsoluteTimerFloorMs"/>.
    ///
    /// <para><b>It catches what the floor alone would miss.</b> The floor governs presets that SCALE; a
    /// literal does not scale at all, so compressing the behaviour around it changes the PROPORTION rather
    /// than the logic, and no timer floor can see that. This is the old ratio-distortion inequality with
    /// its free variable RULED instead of declared: the previous form took a caller-supplied
    /// <c>negligibleFraction</c> that the specification never named a value for, so the bound was either
    /// invented or refused as NOT DECLARED.</para>
    ///
    /// <para><b>The PARTICIPATING set is the presets the submission supplies, and nothing here widens
    /// it.</b> That is the declaration this arithmetic is entitled to read; whether a preset in the call
    /// tree participates in the behaviour under test is a question for the author, and it is no longer
    /// forced by a bound that would otherwise collapse to 0.0006x.</para>
    /// </summary>
    public const double LiteralHeadroomMultiple = 10.0;

    /// <summary>
    /// The scan-derived timer floor: <c>k x scan_period</c> — <b>124.7 ms at the measured 24.931 ms scan</b>
    /// (it read 116.7 ms while the scan constant was 7% low). [D, §12a derivation 5]
    ///
    /// <para><b>Reported, and no longer binding on its own.</b> <see cref="EffectiveTimerFloorMs"/> is what
    /// a ceiling is computed from. This is kept because the SUBSUMPTION is a claim about two numbers, and a
    /// claim with only one of them in the code is unfalsifiable.</para>
    /// </summary>
    public static double TimerFloorMs => TimerScanMultiple * WireTiming.ScanPeriodMs;

    /// <summary>
    /// 🔴 <b>THE FLOOR A SCALED PRESET IS ACTUALLY HELD TO: the greater of
    /// <see cref="AbsoluteTimerFloorMs"/> and <see cref="TimerFloorMs"/>.</b>
    ///
    /// <para><b>A maximum rather than a constant, deliberately.</b> The ruling is that 500 ms subsumes
    /// <c>k x scan</c> — true at 24.931 ms/scan with a factor of four in hand, and FALSE the moment a
    /// program's scan period passes 100 ms. Hardcoding 500 would then be a floor that has quietly stopped
    /// being conservative, on a constant this repository has already had to correct once for being 7% low
    /// in the permissive direction.</para>
    /// </summary>
    public static double EffectiveTimerFloorMs => Math.Max(AbsoluteTimerFloorMs, TimerFloorMs);

    /// <summary>
    /// The SAMPLED poll period: <c>K x RTT_p99</c>, and <b>>= 201 ms even at K = 1</b>. [D, §12a derivation 5]
    ///
    /// <para><b>BOUND-shaped, so it keeps the p99 and must never be re-keyed on the p90.</b> This is the
    /// term the 2026-08-13 correction tightened by 14%, and it is the term latching removes entirely.</para>
    /// </summary>
    public static double PollPeriodMs(int slotsPerPollCycle) =>
        slotsPerPollCycle >= 1
            ? slotsPerPollCycle * (double)WireTiming.RttP99Ms
            : throw new ArgumentOutOfRangeException(nameof(slotsPerPollCycle), slotsPerPollCycle, "a poll cycle covers at least one read.");

    /// <summary>
    /// <c>comp_min = T_plant / T_budget</c> — the LEAST compression that fits the wave.
    /// </summary>
    public static double MinimumFor(double plantMs, double budgetMs)
    {
        if (double.IsNaN(plantMs) || plantMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(plantMs), plantMs, "a behaviour that takes no plant time is not a behaviour under test.");

        if (double.IsNaN(budgetMs) || budgetMs <= 0)
            throw new ArgumentOutOfRangeException(nameof(budgetMs), budgetMs, "a budget of zero admits no test at all, and dividing by it would report an infinite comp_min as though it were a number.");

        // Never below 1: "compressing" by less than 1 is dilation, which no part of this design does, and
        // reporting 0.4 as comp_min would make a plan look runnable at a factor nothing runs at.
        return Math.Max(1.0, plantMs / budgetMs);
    }

    /// <summary>
    /// The ceiling one SAMPLED expectation imposes, expressed the way the observability floor expresses it.
    ///
    /// <para><b>This is the same inequality as
    /// <see cref="ObservabilityOutcome.WindowBelowFloorAtRuntimeCompression"/>, from the other side</b>, and
    /// that is deliberate: <c>window.At(comp) &gt;= floor</c> is exactly <c>comp &lt;= window.PlantScans /
    /// floor</c>. The two must not be able to disagree, so they are one division rather than two
    /// derivations — a test asserts the agreement across a swept range, and breaking either side reddens
    /// it.</para>
    /// </summary>
    public static double SampledCeiling(ScanBudget window, double floorScans)
    {
        ArgumentNullException.ThrowIfNull(window);

        if (floorScans <= 0)
            throw new ArgumentOutOfRangeException(nameof(floorScans), floorScans, "a floor of zero scans would admit a one-scan event, which is unobservable at any rate.");

        return window.PlantScans / floorScans;
    }

    /// <summary>
    /// The ceiling one LATCHED or STAMPED expectation imposes: the event must still occupy a scan.
    ///
    /// <para><b>[I] — that STAMPED takes the scan-period term is inferred, not quoted.</b> X-D names LATCHED
    /// and SAMPLED and says nothing about stamps. A stamp records the scan number of an event, so an event
    /// compressed below one scan has no scan to record and the same term applies. It is marked here rather
    /// than absorbed, matching the one inferred cell already marked in <see cref="ObservabilityCheck"/>.</para>
    /// </summary>
    public static double LatchedCeiling(ScanBudget window)
    {
        ArgumentNullException.ThrowIfNull(window);
        return window.PlantScans;
    }

    /// <summary>
    /// Compute the whole plan: <c>comp_min</c>, every ceiling, and the verdict.
    ///
    /// <para>🔴 <b>A DECLARED <c>negligibleFraction</c> IS NO LONGER CONSULTED, AND THE PLAN SAYS SO OUT
    /// LOUD.</b> The ratio-distortion fraction was replaced on 2026-08-18 by the absolute floor plus the
    /// ruled <see cref="LiteralHeadroomMultiple"/>. A submission that still carries the field is not
    /// refused — it was correct when it was written — but an input that is read as though it still governs
    /// is exactly the silently-ignored field this project keeps finding, so the supersession is appended to
    /// the plan's own detail rather than left in a doc comment.</para>
    /// </summary>
    public static CompressionPlan Plan(CompressionRequest request, double floorScans)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = PlanOf(request, floorScans);

        return request.NegligibleFraction is not { } superseded
            ? plan
            : plan with
            {
                Detail = plan.Detail
                    + $" ⚠️ THIS SUBMISSION DECLARES negligibleFraction = {superseded:P2} AND IT WAS NOT USED. The ratio-distortion "
                    + $"FRACTION was replaced on 2026-08-18 by the absolute {AbsoluteTimerFloorMs:0.#} ms timer floor plus the ruled "
                    + $"{LiteralHeadroomMultiple:0.#}x literal-headroom companion. Nothing here is invalidated by the declaration and "
                    + "nothing was computed from it; it is named because a stated input that quietly governs nothing is worse than an absent one.",
            };
    }

    private static CompressionPlan PlanOf(CompressionRequest request, double floorScans)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Expectations);
        ArgumentNullException.ThrowIfNull(request.Presets);

        var compMin = MinimumFor(request.PlantMs, request.BudgetMs);

        // *** WHAT IS ACTUALLY BEING SCALED. *** comp_min says what the plan NEEDS; the runtime factor
        // says what the wave DOES. Keying the "is anything compressed?" branches on comp_min alone let a
        // wave at runtime=8 with comp_min=1 report that nothing was being scaled — and pass with no
        // comp_stable declared, while the model was driven at 8x. The maximum can only tighten.
        var applied = Math.Max(compMin, Math.Max(1, request.RuntimeCompression));
        var appliedBy = applied > compMin ? $"the wave runs at comp={request.RuntimeCompression}" : $"comp_min is {compMin:0.##}x";

        var bounds = new List<CompressionBound>();

        // ---- per assertion ---------------------------------------------------------------------------
        if (request.Expectations.Count == 0)
        {
            bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.Assertion, "<no expectations>",
                "this vector declares no observability, so no assertion ceiling could be computed. Empty is not clean: a vector that asserts nothing does not thereby become compressible without limit."));
        }

        foreach (var expectation in request.Expectations)
        {
            if (expectation.Mode == InstrumentationMode.Sampled && expectation.WindowScans < 1)
            {
                bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.Assertion, expectation.Signal,
                    $"'{expectation.Signal}' is SAMPLED and declares no window, so T_event is unknown and its ceiling could not be computed. Undeclared is not exempt."));
                continue;
            }

            if (expectation.WindowScans < 1)
            {
                // Latched and stamped may legitimately omit a window — the floor does not apply to them —
                // but then they say nothing about T_event either, so they impose no computable ceiling and
                // that is stated rather than silently skipped.
                bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.Assertion, expectation.Signal,
                    $"'{expectation.Signal}' is {expectation.Mode} and declares no window. It is exempt from the observability FLOOR, which is why a window is optional there — but with no T_event there is no assertion ceiling to compute either, and an uncomputed ceiling is not an absent one."));
                continue;
            }

            var window = new ScanBudget(expectation.WindowScans, request.DeclaredCompression);

            var ceiling = expectation.Mode == InstrumentationMode.Sampled
                ? SampledCeiling(window, floorScans)
                : LatchedCeiling(window);

            bounds.Add(new CompressionBound(CompressionBoundKind.Assertion, CompressionBoundState.Computed, expectation.Signal, ceiling,
                expectation.Mode == InstrumentationMode.Sampled
                    ? $"'{expectation.Signal}' is SAMPLED over {window}, which is {window.PlantScans:0.#} plant scan(s), against a floor of {floorScans:0.0} scan(s): T_event / poll_period = {ceiling:0.##}x. The poll period is K x RTT_p99 = {PollPeriodMs(request.SlotsPerPollCycle):0} ms and keys on the p99 because a ceiling is BOUND-shaped."
                    : $"'{expectation.Signal}' is {expectation.Mode} over {window} = {window.PlantScans:0.#} plant scan(s): T_event / scan_period = {ceiling:0.##}x. Latching removes the SAMPLED term entirely, which is X-D's throughput lever and F-3's argument."));
        }

        // ---- per timer, and the ratio-distortion bound the literals feed --------------------------------
        var shortestBehaviourPlantMs = request.Expectations
            .Where(e => e.WindowScans >= 1)
            .Select(e => new ScanBudget(e.WindowScans, request.DeclaredCompression).PlantMs)
            .DefaultIfEmpty(double.NaN)
            .Min();

        foreach (var preset in request.Presets)
        {
            switch (preset.Source)
            {
                // 🔴 *** THE ABSOLUTE FLOOR, RULED 2026-08-18: NO TIMER PRESET MAY BE COMPRESSED BELOW
                // ~500 ms. *** It replaced `PT / (k x scan)` as the binding term because it asks the
                // question the hardware answers — how short may a timer get before it stops behaving like
                // a timer — rather than what proportion of a test a fixed timer may occupy. It SUBSUMES
                // k x scan (124.7 ms at the measured scan) and the p99 sampled-observability floor, and
                // EffectiveTimerFloorMs takes the maximum rather than trusting that ordering forever.
                case PresetSource.Data:
                    bounds.Add(new CompressionBound(CompressionBoundKind.Timer, CompressionBoundState.Computed, preset.Name,
                        preset.PresetMs / EffectiveTimerFloorMs,
                        $"'{preset.Name}' is a DATA preset of {preset.PresetMs:0.#} ms, so the factor scales it: PT / floor = {preset.PresetMs:0.#} / {EffectiveTimerFloorMs:0.#} = {preset.PresetMs / EffectiveTimerFloorMs:0.##}x. *** THE FLOOR IS THE ABSOLUTE {AbsoluteTimerFloorMs:0.#} ms (ruled 2026-08-18), NOT the scan-derived k x scan = {TimerScanMultiple} x {WireTiming.ScanPeriodMs:0.###} = {TimerFloorMs:0.#} ms, which it subsumes *** — so a 2-second preset caps compression at {2000.0 / EffectiveTimerFloorMs:0.0}x. X-D says this term often binds first, and under the absolute floor it binds harder."));
                    break;

                case PresetSource.Literal when double.IsNaN(shortestBehaviourPlantMs):
                    bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.RatioDistortion, preset.Name,
                        $"'{preset.Name}' is an UNSCALED literal of {preset.PresetMs:0.#} ms, and no expectation declares a window, so there is no SHORTEST COMPRESSED BEHAVIOUR to measure it against."));
                    break;

                // 🔴 *** THE COMPANION TO THE FLOOR, RULED 2026-08-18: the compressed behaviour must
                // remain at least 10x the largest PARTICIPATING unscaled literal. *** Same inequality as
                // the old ratio-distortion bound with its free variable RULED instead of caller-declared —
                // `negligibleFraction` had no value in the specification, so the bound was either invented
                // or reported NOT DECLARED and refused the plan. Taking the MINIMUM over the supplied
                // presets is the same thing as keying on the largest literal, computed per row so the
                // report names which one binds.
                case PresetSource.Literal:
                    bounds.Add(new CompressionBound(CompressionBoundKind.RatioDistortion, CompressionBoundState.Computed, preset.Name,
                        shortestBehaviourPlantMs / (LiteralHeadroomMultiple * preset.PresetMs),
                        $"'{preset.Name}' is a LITERAL of {preset.PresetMs:0.#} ms and DOES NOT SCALE. The shortest behaviour is {shortestBehaviourPlantMs:0.#} ms of plant time, and the ruled companion requires the COMPRESSED behaviour to stay at least {LiteralHeadroomMultiple:0.#}x this literal, which caps the factor at {shortestBehaviourPlantMs / (LiteralHeadroomMultiple * preset.PresetMs):0.##}x. Past that the block passes or fails for reasons of CHANGED PROPORTION rather than changed logic — a different failure from losing observability, and one no other term catches. *** THE MULTIPLE IS RULED, NOT DECLARED (2026-08-18): *** the caller-supplied `negligibleFraction` this replaced had no value anywhere in the specification, so it was invented per submission or the plan was refused for want of it."));
                    break;

                default:
                    bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.Timer, preset.Name,
                        $"'{preset.Name}' does not say whether its preset is DATA or a LITERAL, so it could be either ceiling and the two push in OPPOSITE directions — a data preset lowers the timer ceiling, a literal one lowers the ratio-distortion ceiling. There is no fail-safe guess, so it is refused rather than assumed."));
                    break;
            }
        }

        // ---- the model's own comp_stable ---------------------------------------------------------------
        // *** THE TREATMENT OF AN ABSENT comp_stable DEPENDS ON WHETHER ANYTHING IS BEING COMPRESSED, AND
        // BOTH BRANCHES ARE DECIDED RATHER THAN DEFAULTED. *** At comp_min = 1 nothing is scaled, so the
        // model's stability under scaling cannot bind and reporting the bound as NOT DECLARED is a true
        // statement about a ceiling that does not apply. Above 1 the plan is asking a model to run at a
        // factor nobody declared it stable at, and that is the check needing an input to proceed: refused.
        if (request.ModelCompStable is { } stable)
        {
            bounds.Add(new CompressionBound(CompressionBoundKind.Model, CompressionBoundState.Computed, "<model>", stable,
                $"the model declares comp_stable = {stable:0.##}x (M3/M4). It is the model author's number and is not derived from anything in §12a."));
        }
        else
        {
            bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.Model, "<model>",
                applied > 1
                    ? $"{appliedBy}, so the model IS being driven at a factor, and it declares no comp_stable — nothing establishes that it behaves there. An undeclared stability ceiling is not an infinite one. *** THIS BRANCH USED TO KEY ON comp_min ALONE, so a wave at comp=8 whose budget gave comp_min=1 reported that nothing was being scaled and passed with no comp_stable at all. ***"
                    : "the model declares no comp_stable. Neither comp_min nor the runtime factor exceeds 1, so nothing is being scaled and this ceiling cannot bind — it is reported rather than silently omitted, because an absent line reads as a check that passed."));
        }

        // ---- the verdict --------------------------------------------------------------------------------
        var computed = bounds.Where(b => b.Binds).ToArray();
        var missing = bounds.Where(b => b.State == CompressionBoundState.NotDeclared).ToArray();

        // A ceiling that could not be computed only blocks the plan when compression is actually being
        // applied. At comp_min = 1 nothing is scaled and no ceiling of X-D's can bind, so an uncomputed one
        // is honest bookkeeping rather than a refusal.
        if (applied > 1 && missing.Length > 0)
        {
            return new CompressionPlan(CompressionOutcome.NotComputable, compMin, double.NaN, CompressionBoundKind.Unstated, bounds,
                $"{appliedBy}, so compression IS being applied, and {missing.Length} ceiling(s) could not be computed: "
                + string.Join(" | ", missing.Select(b => $"{b.Kind}/{b.Subject} — {b.Detail}"))
                + " An unknown ceiling is not a high one, so this is not runnable.");
        }

        if (computed.Length == 0)
        {
            return new CompressionPlan(CompressionOutcome.NotComputable, compMin, double.NaN, CompressionBoundKind.Unstated, bounds,
                "no ceiling could be computed at all, so comp_max is unknown. Empty is not clean: a plan checked against no ceiling is not a plan that cleared them.");
        }

        var binding = computed.MinBy(b => b.CompMax)!;

        return binding.CompMax >= compMin
            ? new CompressionPlan(CompressionOutcome.Runnable, compMin, binding.CompMax, binding.Kind, bounds,
                $"comp_min {compMin:0.##}x fits under comp_max {binding.CompMax:0.##}x, whose binding term is {binding.Kind} '{binding.Subject}'. RUN AT comp_min: the remainder is margin, and compression is a fidelity risk.")
            : new CompressionPlan(CompressionOutcome.Refused, compMin, binding.CompMax, binding.Kind, bounds,
                $"comp_min {compMin:0.##}x EXCEEDS comp_max {binding.CompMax:0.##}x. The binding ceiling is {binding.Kind} '{binding.Subject}': {binding.Detail} "
                + "This refusal is a MODEL TASK or a TEST-DESIGN task, not a number to raise — and section 7's DEFERRED bucket carries the sub-case.");
    }
}
