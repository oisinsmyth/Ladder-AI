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

    /// <summary>Per timer: <c>PT / (k x scan_period)</c>, k ~ 5. <b>X-D says this one often binds first.</b></summary>
    Timer,

    /// <summary>The model's own declared <c>comp_stable</c> (M3/M4).</summary>
    Model,

    /// <summary>
    /// The third bound, from the 0.4 sweep: a preset that is a LITERAL does not scale, so compressing the
    /// behaviours around it changes the PROPORTION rather than the logic.
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
/// The ratio-distortion threshold: how large an UNSCALED literal may become as a fraction of the SHORTEST
/// COMPRESSED behaviour before the block starts passing or failing for reasons of proportion.
/// <b>The specification names no value for this</b> — it works an example (0.003% becoming 2.5%) and calls
/// the good state "negligible" without saying where negligible ends. So it is a required input with no
/// default, and a plan that does not state one reports the bound <see cref="CompressionBoundState.NotDeclared"/>
/// rather than quietly picking a number and calling it derived.
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
/// one round trip per slot against a 23.33 ms scan. <b>Compression shortens the real-time separation of the
/// events being observed</b>, so past a point it pushes an assertion below the sampling floor and
/// <i>every check still reports green because the assertion was simply never sampled.</i></para>
///
/// <para><b>THE CEILING IS LOWER THAN X-D ASSUMED, AND IT IS THE TERM X-D SAYS BINDS FIRST.</b>
/// X-D carried <c>~10 ms</c> for the scan and <c>~100 ms</c> for the poll; §12a derivation 5 replaces both.
/// The timer floor is <c>k x scan = 5 x 23.33 = 116.7 ms</c>, not 50 ms, so <b>on X-D's own 500 ms preset
/// <c>comp_max(timer)</c> is 4.3x and not the 10x originally assumed</b>.
///
/// 🔴 <b>THE 4.29x IS HALF-MEASURED, AND CALLING IT "MEASURED" WAS WRONG.</b> The scan period is measured;
/// <b><c>k ~ 5</c> IS X-D'S OWN NUMBER AND NEVER HAS BEEN</b> — and it is the term X-D says binds first, so
/// the assumed half is the load-bearing one. Read 116.7 ms as <b>DERIVED FROM ONE MEASURED AND ONE ASSUMED
/// INPUT</b>, not as a measurement. It is a plausible engineering figure and it is not evidence; what would
/// settle it is a preset scaled to five scans on a 1214C, observed to still behave like a timer. No verdict in X-D's worked
/// example flips — a 4-hour behaviour in a 60 s budget needs 240x and is refused either way — but every
/// marginal case moves toward REFUSE.</para>
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
    /// X-D's <c>k</c>: how many scan periods a preset must remain above once scaled. <b>k ~ 5</b>, so the
    /// floor is <c>5 x 23.33 = 116.7 ms</c>. A preset scaled below a few scan times stops behaving like a
    /// timer — it rounds toward zero, and the block then passes or fails for reasons unrelated to its logic.
    ///
    /// <para>🔴 <b>[A] — ASSUMED. THIS IS X-D'S NUMBER AND NOTHING HAS MEASURED IT</b>, which matters more
    /// than it looks: it multiplies the one measured term to produce the floor, and X-D says the timer term
    /// binds first. Every figure downstream of it — the 116.7 ms floor, the 4.29x cap on a 500 ms preset —
    /// is therefore DERIVED FROM ONE MEASURED AND ONE ASSUMED INPUT and must not be described as measured.</para>
    /// </summary>
    public const int TimerScanMultiple = 5;

    /// <summary>
    /// The timer floor in milliseconds: <c>k x scan_period</c>. <b>116.7 ms, not X-D's original 50.</b>
    /// [D, §12a derivation 5] — <b>D for DERIVED, and one of its two inputs is
    /// <see cref="TimerScanMultiple"/>, which is ASSUMED.</b> Not a measurement, whatever its precision suggests.
    /// </summary>
    public static double TimerFloorMs => TimerScanMultiple * WireTiming.ScanPeriodMs;

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

    /// <summary>Compute the whole plan: <c>comp_min</c>, every ceiling, and the verdict.</summary>
    public static CompressionPlan Plan(CompressionRequest request, double floorScans)
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
                case PresetSource.Data:
                    bounds.Add(new CompressionBound(CompressionBoundKind.Timer, CompressionBoundState.Computed, preset.Name,
                        preset.PresetMs / TimerFloorMs,
                        $"'{preset.Name}' is a DATA preset of {preset.PresetMs:0.#} ms, so the factor scales it: PT / (k x scan) = {preset.PresetMs:0.#} / {TimerFloorMs:0.#} = {preset.PresetMs / TimerFloorMs:0.##}x. *** THE FLOOR IS 116.7 ms, NOT X-D's ORIGINAL 50 *** (§12a derivation 5), so a 500 ms preset caps compression at 4.3x rather than 10x. X-D says this term often binds first."));
                    break;

                case PresetSource.Literal when request.NegligibleFraction is not { } fraction:
                    bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.RatioDistortion, preset.Name,
                        $"'{preset.Name}' is an UNSCALED literal of {preset.PresetMs:0.#} ms and no negligible-fraction threshold was declared, so the ratio-distortion ceiling could not be computed. The specification works an example (0.003% of a 4-hour interval becoming 2.5% of a 20-second one) and never says where 'negligible' ends, so this input has no default and is not invented here."));
                    break;

                case PresetSource.Literal when double.IsNaN(shortestBehaviourPlantMs):
                    bounds.Add(CompressionBound.NotDeclared(CompressionBoundKind.RatioDistortion, preset.Name,
                        $"'{preset.Name}' is an UNSCALED literal of {preset.PresetMs:0.#} ms, and no expectation declares a window, so there is no SHORTEST COMPRESSED BEHAVIOUR to measure it against."));
                    break;

                case PresetSource.Literal:
                    bounds.Add(new CompressionBound(CompressionBoundKind.RatioDistortion, CompressionBoundState.Computed, preset.Name,
                        request.NegligibleFraction!.Value * shortestBehaviourPlantMs / preset.PresetMs,
                        $"'{preset.Name}' is a LITERAL of {preset.PresetMs:0.#} ms and DOES NOT SCALE. The shortest behaviour is {shortestBehaviourPlantMs:0.#} ms of plant time, so keeping the literal within {request.NegligibleFraction!.Value:P2} of it caps the factor at {request.NegligibleFraction!.Value * shortestBehaviourPlantMs / preset.PresetMs:0.##}x. Past that the block passes or fails for reasons of CHANGED PROPORTION rather than changed logic — a different failure from losing observability, and one no other term catches."));
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
