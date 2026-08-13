namespace Harness.Results;

/// <summary>Why one expectation's observability was or was not supportable. Each names a different fix.</summary>
public enum ObservabilityOutcome
{
    /// <summary>The mode can answer a question about this signal's nature, the map provides it, and any window clears the floor.</summary>
    Supportable,

    /// <summary>
    /// The mode cannot answer a question about a signal of this nature AT ANY RATE. Sampling a one-scan
    /// event, or sampling a same-scan coincidence — no protocol choice changes it.
    /// </summary>
    ModeCannotAnswerThisNature,

    /// <summary>The map does not provide that instrumentation for that signal. The copy layer would have to generate it, before the download.</summary>
    MapDoesNotProvideIt,

    /// <summary>The signal is not in the map's observability declarations at all.</summary>
    SignalNotInMap,

    /// <summary>A sampled window shorter than the observability floor. At the floor you get nothing; near it you get something plausible.</summary>
    WindowBelowFloor,

    /// <summary>
    /// <b>Sampling a NEVER assertion.</b> Its pass is produced by having seen nothing, and a poll gap
    /// produces exactly that - so a miss reads as a PASS. Distinct from
    /// <see cref="ModeCannotAnswerThisNature"/> because the reason and the fix are different: the signal
    /// may be perfectly readable, and it is the SHAPE OF THE CLAIM that sampling cannot support.
    /// </summary>
    SampledCannotAnswerANeverAssertion,

    /// <summary>
    /// The window clears the floor as DECLARED and not at the compression factor the run will use.
    /// Sound at authoring time and silently void at run time.
    /// </summary>
    WindowBelowFloorAtRuntimeCompression,

    /// <summary>A sampled expectation with no declared window. Undeclared is not exempt.</summary>
    WindowNotDeclared,

    /// <summary>The map's observability declarations were empty, so the check ran against nothing.</summary>
    NothingToCheckAgainst,
}

/// <summary>One expectation's verdict, with the arithmetic that produced it.</summary>
public sealed record ObservabilityFinding(string Signal, ObservabilityOutcome Outcome, string Detail)
{
    public bool Supportable => Outcome == ObservabilityOutcome.Supportable;
}

/// <summary>The whole vector's observability verdict.</summary>
public sealed record ObservabilityReport(IReadOnlyList<ObservabilityFinding> Findings, double FloorScans)
{
    /// <summary>Supportable only when something was examined AND every finding is supportable.</summary>
    public bool Supported => Findings.Count > 0 && Findings.All(f => f.Supportable);

    public IReadOnlyList<ObservabilityFinding> Refusals => Findings.Where(f => !f.Supportable).ToArray();
}

/// <summary>
/// Contract gate 5 — <b>the one with teeth — as a COMPUTATION.</b>
///
/// <para><b>It was a caller-supplied bool.</b> <c>Admissibility.Check</c> took
/// <c>observabilitySupported</c> as a parameter, which means the gate the contract leans on hardest was
/// an ARGUMENT: a caller could assert the answer. That is the same defect as every other one found in
/// this project — a check whose subject supplies its verdict — and it is the gate that decides whether
/// a green can mean anything at all.</para>
///
/// <para><b>It is now derived from three things the caller cannot assert:</b> the vector's declared
/// signal nature and mode, what the MAP actually provides for that signal, and §12a derivation 1's
/// floor at the compression factor the run will use.</para>
///
/// <para><b>The floor is READ from §12a, never carried here.</b> It has moved twice, and a restated
/// constant will one day refuse the wrong vectors with great confidence.</para>
/// </summary>
public static class ObservabilityCheck
{
    /// <summary>
    /// <b>Which instrumentation modes can answer a question about a signal of each nature.</b>
    ///
    /// <para>Every cell below is quoted from the contract or from §12, except one, which is marked.</para>
    /// <list type="bullet">
    /// <item><b>Coincidence → Stamped only.</b> "A same-scan coincidence is unobservable by sampling at
    /// all"; the program must record the scan number of each event so the assertion becomes a comparison
    /// of two persistent integers. A latch says it happened, never that two things coincided.</item>
    /// <item><b>Transient → Latched or Stamped.</b> "A one-scan event is unobservable at any polling
    /// rate" — structurally invisible, and no protocol choice changes it — so Sampled is out. A latch
    /// holds until cleared at test start and cannot fall in a gap.
    /// <b>[I] — that STAMPED also suffices for occurrence is INFERRED, not quoted:</b> the contract
    /// gives stamps as answering "when, relative to T=0", and a stamp existing implies the event
    /// occurred. It is the one cell of this table the contract does not state, and it is marked here
    /// rather than absorbed.</item>
    /// <item><b>PersistentState → any of the three</b>, with Sampled subject to the floor. This is the
    /// only nature for which the window check does any work.</item>
    /// </list>
    /// </summary>
    /// <param name="form">
    /// <b>F-3, RULED 2026-08-13: an unlatched SAMPLED assertion is admissible for
    /// <see cref="SignalNature.PersistentState"/> ONLY, and never for an <see cref="AssertionForm.Never"/>
    /// assertion whatever the signal's nature.</b> The grant is given exactly where it is free and refused
    /// exactly where it would manufacture false greens.
    /// </param>
    public static IReadOnlySet<InstrumentationMode> ModesThatCanAnswer(SignalNature nature, AssertionForm form)
    {
        var modes = nature switch
        {
            SignalNature.Coincidence => new HashSet<InstrumentationMode> { InstrumentationMode.Stamped },
            SignalNature.Transient => new HashSet<InstrumentationMode> { InstrumentationMode.Latched, InstrumentationMode.Stamped },
            SignalNature.PersistentState => new HashSet<InstrumentationMode>
            {
                InstrumentationMode.Latched, InstrumentationMode.Sampled, InstrumentationMode.Stamped,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(nature), nature, "unknown signal nature."),
        };

        // *** F-3: SAMPLING CANNOT ANSWER A "NEVER". *** The assertion "never saw the forbidden state" is
        // SATISFIED BY NEVER HAVING LOOKED, so a poll gap produces a PASS rather than a miss - a false
        // green, which is the worst available direction. It applies whatever the signal's nature: a
        // perfectly persistent forbidden state that appears and clears inside one gap is still unseen.
        if (form == AssertionForm.Never)
            modes.Remove(InstrumentationMode.Sampled);

        return modes;
    }

    /// <summary>
    /// Evaluate a vector's observability against the map and the floor.
    /// </summary>
    /// <param name="floorScans">
    /// §12a derivation 1's floor, in scans, for the tensor width the wave will run at. Supplied by the
    /// caller because it is a property of the WAVE SET, not of the vector — and computed by
    /// <c>WireTiming.ObservabilityFloorScans</c>, never carried in this file.
    /// </param>
    /// <param name="declaredCompression">The <c>comp</c> the vector's scan counts are stated at.</param>
    /// <param name="runtimeCompression">The <c>comp</c> the wave will actually use.</param>
    public static ObservabilityReport Evaluate(
        IReadOnlyList<ObservabilityDeclaration> expectations,
        AssertionForm form,
        MirrorObservability map,
        double floorScans,
        int declaredCompression,
        int runtimeCompression)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(map);

        if (declaredCompression < 1 || runtimeCompression < 1)
            throw new ArgumentOutOfRangeException(nameof(declaredCompression), "a compression factor below 1 is not a compression factor.");

        if (floorScans <= 0)
            throw new ArgumentOutOfRangeException(nameof(floorScans), floorScans, "a floor of zero scans would admit a one-scan event, which is unobservable at any rate.");

        var findings = new List<ObservabilityFinding>();

        // Empty is not clean, on both sides. A vector declaring nothing has nothing to support; a map
        // declaring nothing supports nothing, and checking against it would admit everything.
        if (expectations.Count == 0)
        {
            findings.Add(new ObservabilityFinding("<none>", ObservabilityOutcome.NothingToCheckAgainst,
                "this vector declares no observability at all, so there was nothing to check. A vector with no declared expectation cannot be refused for being unobservable, which is not the same as being observable."));
            return new ObservabilityReport(findings, floorScans);
        }

        if (map.IsEmpty)
        {
            findings.Add(new ObservabilityFinding("<map>", ObservabilityOutcome.NothingToCheckAgainst,
                "the map declares no observability for any signal, so every expectation was checked against nothing. An empty map admits everything while reading exactly like a check that ran."));
            return new ObservabilityReport(findings, floorScans);
        }

        foreach (var expectation in expectations)
        {
            findings.Add(Evaluate(expectation, form, map, floorScans, declaredCompression, runtimeCompression));
        }

        return new ObservabilityReport(findings, floorScans);
    }

    private static ObservabilityFinding Evaluate(
        ObservabilityDeclaration expectation,
        AssertionForm form,
        MirrorObservability map,
        double floorScans,
        int declaredCompression,
        int runtimeCompression)
    {
        // 1. Can this MODE answer a question about a signal of this NATURE? Asked first, because it is
        //    the only one no instrumentation choice can fix — it is physics, not provisioning.
        var canAnswer = ModesThatCanAnswer(expectation.Nature, form);

        // F-3's refusal is reported separately from the nature refusal, because the signal may be
        // perfectly readable and it is the SHAPE OF THE CLAIM that sampling cannot support.
        if (form == AssertionForm.Never && expectation.Mode == InstrumentationMode.Sampled)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.SampledCannotAnswerANeverAssertion,
                $"{expectation.Signal} is SAMPLED and the cited assertion is a NEVER. A NEVER assertion PASSES BY HAVING SEEN NOTHING, and a poll gap produces exactly that - so an occurrence inside a gap is MISSED ENTIRELY AND READS AS A PASS. "
                + "It is rate-dependent, rare and unreproducible: one 2,216 ms outlier per ~2,000 round trips is ~95 scans blind, so a suite passes hundreds of times and misses the one occurrence. "
                + "And the result package cannot tell the two apart - a sampled assertion that saw nothing is Held, identical to one that saw nothing BECAUSE NOTHING HAPPENED. Latch it (F-3, ruled by the owner 2026-08-13).");
        }

        if (!canAnswer.Contains(expectation.Mode))
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.ModeCannotAnswerThisNature,
                $"'{expectation.Signal}' is declared {expectation.Nature} and instrumented {expectation.Mode}. "
                + (expectation.Nature == SignalNature.Coincidence
                    ? "A same-scan coincidence is unobservable by sampling AT ALL, at any rate: the program must record the scan number of each event so the assertion becomes a comparison of two persistent integers."
                    : "A one-scan event is unobservable at ANY polling rate — a poll IS one round trip, and there is no rate to turn up.")
                + $" Modes that could answer: {string.Join(", ", canAnswer.OrderBy(m => m.ToString(), StringComparer.Ordinal))}.");
        }

        // 2. Does the MAP provide it? A mode the copy layer never generated is not available by wishing.
        var provided = map.For(expectation.Signal);
        if (provided.Count == 0)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.SignalNotInMap,
                $"'{expectation.Signal}' does not appear in the map's observability declarations, so nothing in the copy layer publishes it. The declaration must exist BEFORE the download that generates the copy layer, and the set is frozen for the wave set.");
        }

        if (!provided.Contains(expectation.Mode))
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.MapDoesNotProvideIt,
                $"'{expectation.Signal}' is instrumented {string.Join(", ", provided.OrderBy(m => m.ToString(), StringComparer.Ordinal))} in this map, and this expectation needs {expectation.Mode}. "
                + "Adding it means regenerating the copy layer, which means waiting a full download boundary — and DO NOT fix it by adding a status output to the block: that collides with D13 and whether an author may change a block's interface purely to make it testable is open with the owner (contract section 9.1).");
        }

        // 3. Latched and Stamped are exempt from the floor — a latch holds until cleared at test start,
        //    and a stamp is a persistent integer. Only Sampled has a window to clear.
        if (expectation.Mode != InstrumentationMode.Sampled)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.Supportable,
                $"{expectation.Mode} is exempt from the observability floor: it does not have to be caught in a poll gap.");
        }

        if (expectation.WindowScans <= 0)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.WindowNotDeclared,
                $"'{expectation.Signal}' is sampled and declares no window. Undeclared is not exempt — the floor applies fully to sampling, and a window nobody stated cannot be compared to it.");
        }

        // 4. THE COMPRESSION RE-CHECK. A window declared in scans is only valid at the comp it was
        //    computed at: a behaviour occupying 20 scans at comp = 1 occupies 2 at comp = 10, crossing
        //    the floor with nobody editing the vector.
        var effective = expectation.WindowScans * (double)declaredCompression / runtimeCompression;

        if (expectation.WindowScans < floorScans)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.WindowBelowFloor,
                $"'{expectation.Signal}' declares a {expectation.WindowScans}-scan window against a floor of {floorScans:0.0} scans. Below the floor you observe nothing; AT it you observe something plausible, which is the worse outcome.");
        }

        if (effective < floorScans)
        {
            return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.WindowBelowFloorAtRuntimeCompression,
                $"'{expectation.Signal}' declares {expectation.WindowScans} scans at comp={declaredCompression}, which is {effective:0.0} scans at the comp={runtimeCompression} this wave will run — below the {floorScans:0.0}-scan floor. The declaration was sound when it was written and is void at run time, and nobody edited the vector.");
        }

        return new ObservabilityFinding(expectation.Signal, ObservabilityOutcome.Supportable,
            $"sampled over {expectation.WindowScans} scan(s) at comp={declaredCompression} = {effective:0.0} scan(s) at comp={runtimeCompression}, against a floor of {floorScans:0.0}.");
    }
}
