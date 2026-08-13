using Harness.Results;

namespace Harness.Loop;

/// <summary>What a wave set is FOR, in X-I rule 2's terms. <b><see cref="Unstated"/> is unusable.</b></summary>
public enum WaveSetRole
{
    /// <summary>Nobody said. A set whose role is unknown cannot be ordered against anything, so it is refused.</summary>
    Unstated = 0,

    /// <summary>A MODEL under test. A consumer may only be believed once its model has a PASSING result.</summary>
    Model,

    /// <summary>A CONSUMER of a model. Its results mean nothing if the model it rests on failed.</summary>
    Consumer,
}

/// <summary>One wave set in a sequence.</summary>
/// <param name="DependsOn">
/// The <see cref="Id"/>s of the model sets this one rests on. <b>Empty on a model; a consumer naming
/// none is refused</b> — a consumer that depends on nothing is not a consumer, and reading it as one
/// would let the ordering be satisfied by saying less.
/// </param>
public sealed record WaveSetSubmission(string Id, WaveSetRole Role, LoopRequest Request, IReadOnlyList<string>? DependsOn = null)
{
    public IReadOnlyList<string> Dependencies => DependsOn ?? Array.Empty<string>();
}

/// <summary>How one wave set in the sequence came out. <b>Five values, and NOT-RUN is three of them.</b></summary>
public enum WaveSetOutcome
{
    /// <summary>Unusable zero value.</summary>
    Unstated = 0,

    /// <summary>The wave ran and produced at least one conclusive package, none of which failed.</summary>
    Ran,

    /// <summary>The wave ran and at least one package is conclusively <see cref="ResultVerdict.Fail"/>.</summary>
    RanAndFailed,

    /// <summary>
    /// The wave did not run, or ran and learned nothing. <b>Not the same as failing</b> — a model that
    /// produced six <c>Stale</c> packages has no failures and no passing result either, and X-I rule 2
    /// needs a PASSING one.
    /// </summary>
    RanAndLearnedNothing,

    /// <summary>
    /// *** THE STOP. *** This set was never handed to the loop, because a model it rests on did not
    /// produce a passing result. <b>No device was touched and no package exists.</b>
    /// </summary>
    NotRunBecauseAModelDidNotPass,

    /// <summary>The submission could not be ordered at all — an unstated role, an unknown dependency, a cycle.</summary>
    Refused,
}

/// <summary>One wave set's place in the sequence and what became of it.</summary>
public sealed record WaveSetResult(string Id, WaveSetRole Role, WaveSetOutcome Outcome, LoopResult? Result, string Detail)
{
    /// <summary>A model result X-I rule 2 would accept: it ran, and something conclusive came back clean.</summary>
    public bool Passed => Outcome == WaveSetOutcome.Ran;
}

/// <summary>The whole sequence. <b>There is no <c>Passed</c> here either</b> — read the sets.</summary>
public sealed record SequenceResult(IReadOnlyList<WaveSetResult> Sets, string Detail)
{
    /// <summary>True when at least one set was not run because a model it rests on did not pass.</summary>
    public bool Stopped => Sets.Any(s => s.Outcome == WaveSetOutcome.NotRunBecauseAModelDidNotPass);

    /// <summary>Sets that were never handed to the loop. <b>Their absence of results is a fact, not a gap.</b></summary>
    public IReadOnlyList<WaveSetResult> NotRun =>
        Sets.Where(s => s.Outcome == WaveSetOutcome.NotRunBecauseAModelDidNotPass).ToArray();
}

/// <summary>
/// *** X-I RULE 2's RUN-LOOP HALF: THE LOOP REFUSES TO CONTINUE PAST A MODEL THAT DID NOT PASS. ***
///
/// <para><b>Why this had to exist before reading (b) was permitted.</b> X-I rule 2 has two readings:
/// (a) a model must ALREADY have a passing result before a consumer is admitted, and (b) model and
/// consumer may be submitted together, with wave-set ordering enforcing it. Wave sets run sequentially,
/// <b>but a FAILED model wave set does not stop the consumer's unless the run loop refuses to
/// continue</b> — and nothing described that gate. Without it the consumer's results are produced and
/// BELIEVED after its model failed: not a missing check, <b>a wrong answer that looks like a
/// result</b>.</para>
///
/// <para><b>ADMISSION CANNOT DO THIS, WHICH IS WHY IT IS HERE.</b> A gate at admission can only refuse
/// to admit; it cannot stop a run already under way. <c>Ladder.Wave</c>'s
/// <c>StopOnFailedWaveSetGate</c> is the declaration side, and its own documentation says what it
/// cannot do: it cannot execute the loop, cannot observe it stopping, and <b>cannot make a false
/// declaration true</b>. <i>This class is what makes such a declaration true rather than asserted, and
/// a declaration should cite it.</i></para>
///
/// <para><b>A NOT-RUN SET IS A REPORTED FACT, NEVER AN ABSENCE.</b> Every set appears in the result
/// with an outcome; a consumer that was stopped says so, names the model, and carries no packages.
/// Dropping it from the list would make "we stopped" indistinguishable from "there was nothing to
/// run".</para>
/// </summary>
public static class WaveSetSequence
{
    /// <summary>The identity of this run loop, for a <c>StopOnFailedWaveSetGate</c> declaration to name.</summary>
    /// <remarks>
    /// It changes when the STOPPING BEHAVIOUR changes, not when anything else in the harness does — the
    /// declaration is checked against it, so a version that moved for an unrelated reason would invalidate
    /// declarations that are still true.
    /// </remarks>
    public const string RunLoopVersion = "Harness.Loop.WaveSetSequence/1";

    /// <summary>
    /// Run the sets in order, stopping any consumer whose model did not pass.
    /// </summary>
    /// <param name="run">
    /// How one set is executed. <b>Injected so the stop path can be driven directly</b> — if nothing in
    /// a test could produce a failed model wave set, the stop path would be unreachable and its test
    /// would prove nothing. It defaults to the real <see cref="LoopRun.Execute"/>, and the suite
    /// exercises BOTH: a genuinely defective block through the real loop, and a scripted failure here.
    /// </param>
    public static SequenceResult Execute(
        IReadOnlyList<WaveSetSubmission> sets,
        IDeviceGateway gateway,
        Func<long>? nowMs = null,
        Func<WaveSetSubmission, LoopResult>? run = null)
    {
        ArgumentNullException.ThrowIfNull(sets);
        ArgumentNullException.ThrowIfNull(gateway);

        var execute = run ?? (s => LoopRun.Execute(s.Request, gateway, nowMs));

        if (sets.Count == 0)
        {
            return new SequenceResult(Array.Empty<WaveSetResult>(),
                "no wave sets were submitted. Empty is not clean: a sequence that ran nothing has not shown that the stop works, and has not shown anything else either.");
        }

        var refusals = Order(sets);
        if (refusals.Count > 0)
        {
            return new SequenceResult(
                sets.Select(s => new WaveSetResult(s.Id, s.Role, WaveSetOutcome.Refused, null,
                    "the sequence could not be ordered, so nothing was run: " + string.Join(" | ", refusals))).ToArray(),
                "the sequence could not be ordered: " + string.Join(" | ", refusals));
        }

        var results = new List<WaveSetResult>();
        var passed = new HashSet<string>(StringComparer.Ordinal);
        var stoppedBy = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var set in sets)
        {
            // *** THE STOP, AND IT IS BEFORE THE LOOP IS CALLED. *** A set whose model did not pass is
            // never handed to LoopRun at all: no copy layer, no deployment, no device, no packages. A
            // stop that ran the wave and discarded the answer afterwards would still have stopped the
            // CPU and still have written to the controller.
            var blocking = set.Dependencies
                .Where(d => !passed.Contains(d))
                .ToArray();

            if (blocking.Length > 0)
            {
                var reasons = blocking.Select(d => $"'{d}' ({DescribeBlocker(d, results, stoppedBy)})");

                results.Add(new WaveSetResult(set.Id, set.Role, WaveSetOutcome.NotRunBecauseAModelDidNotPass, null,
                    $"NOT RUN. This set depends on {string.Join(", ", reasons)}, and X-I rule 2 requires a PASSING model result before a consumer's results mean anything. "
                    + "Running it would have produced results that were believed after the model they rest on failed — a wrong answer that looks like a result, which is why the loop refuses rather than reporting one."));

                stoppedBy[set.Id] = "was itself not run";
                continue;
            }

            var result = execute(set);
            var outcome = Classify(result);

            if (outcome == WaveSetOutcome.Ran)
                passed.Add(set.Id);
            else
                stoppedBy[set.Id] = outcome == WaveSetOutcome.RanAndFailed ? "ran and FAILED" : "ran and learned nothing";

            results.Add(new WaveSetResult(set.Id, set.Role, outcome, result, DescribeOutcome(outcome, result)));
        }

        var stopped = results.Count(r => r.Outcome == WaveSetOutcome.NotRunBecauseAModelDidNotPass);

        return new SequenceResult(results,
            stopped == 0
                ? $"all {results.Count} wave set(s) were run in order; no model failed, so nothing was stopped. (That is the UNAFFECTED case: a sequence with no failure must be untouched by this gate, or it becomes noise and gets switched off.)"
                : $"{stopped} of {results.Count} wave set(s) were NOT RUN because a model they rest on did not produce a passing result. Their absence of packages is a REPORTED FACT, not a gap.");
    }

    /// <summary>
    /// A model result X-I rule 2 accepts is one that RAN and learned something clean.
    ///
    /// <para><b>Three not-passing states, kept apart</b>: failed, ran-but-learned-nothing, and never
    /// ran. <c>LoopResult.LearnedAnything</c> is already deliberately not "no failures" — a run that
    /// produced six <c>Stale</c> packages has no failures and has learned nothing, and a model in that
    /// state has not been shown to work.</para>
    /// </summary>
    private static WaveSetOutcome Classify(LoopResult? result)
    {
        if (result is null)
            return WaveSetOutcome.RanAndLearnedNothing;

        if (result.Outcome != LoopOutcome.Ran)
            return WaveSetOutcome.RanAndLearnedNothing;

        if (result.Packages.Any(p => p.ConclusiveAboutTheBlock && p.Verdict == ResultVerdict.Fail))
            return WaveSetOutcome.RanAndFailed;

        return result.LearnedAnything ? WaveSetOutcome.Ran : WaveSetOutcome.RanAndLearnedNothing;
    }

    private static string DescribeOutcome(WaveSetOutcome outcome, LoopResult result) => outcome switch
    {
        WaveSetOutcome.Ran => "ran and produced a conclusive result with no failures. " + result.Summary(),
        WaveSetOutcome.RanAndFailed =>
            "*** RAN AND FAILED. *** Anything depending on this set is not run, because its results would be believed after the model they rest on failed. " + result.Summary(),
        WaveSetOutcome.RanAndLearnedNothing =>
            "ran and learned nothing conclusive about the block, which is NOT the same as passing — X-I rule 2 needs a PASSING result, and 'no failures' is what a run of six Stale packages also looks like. " + result.Summary(),
        _ => result.Summary(),
    };

    private static string DescribeBlocker(string id, IReadOnlyList<WaveSetResult> results, IReadOnlyDictionary<string, string> stoppedBy)
    {
        var result = results.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.Ordinal));

        // The three not-passing states read differently on purpose. "It failed", "it learned nothing"
        // and "it was itself not run" send a reader to three different places, and collapsing them into
        // "did not pass" would hide which hop of a cascade actually broke.
        return result?.Outcome switch
        {
            WaveSetOutcome.RanAndFailed => "ran and FAILED",
            WaveSetOutcome.RanAndLearnedNothing => "ran and learned nothing conclusive",
            WaveSetOutcome.NotRunBecauseAModelDidNotPass => "was itself not run",
            WaveSetOutcome.Refused => "was refused before anything ran",
            _ => stoppedBy.TryGetValue(id, out var why) ? why : "has not run",
        };
    }

    /// <summary>
    /// Everything wrong with the sequence's SHAPE, or empty. Checked before anything runs, so a
    /// mis-declared sequence costs no device time.
    /// </summary>
    private static IReadOnlyList<string> Order(IReadOnlyList<WaveSetSubmission> sets)
    {
        var problems = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var set in sets)
        {
            if (string.IsNullOrWhiteSpace(set.Id))
                problems.Add("a wave set has no id, so nothing can depend on it and nothing can report about it.");
            else if (!seen.Add(set.Id))
                problems.Add($"two wave sets are called '{set.Id}'. A dependency naming it would resolve to whichever came first.");

            if (set.Role == WaveSetRole.Unstated)
                problems.Add($"'{set.Id}' declares no role. A set that is neither model nor consumer cannot be ordered, and treating it as either would be the ordering deciding what nobody said.");

            // A consumer depending on nothing is not a consumer. Accepting it would let the ordering be
            // satisfied by saying LESS, which is the shape of every denominator attack in this project.
            if (set.Role == WaveSetRole.Consumer && set.Dependencies.Count == 0)
                problems.Add($"'{set.Id}' is a CONSUMER and names no model it depends on. X-I rule 2's ordering would then be vacuous for it — a consumer that depends on nothing cannot be stopped by anything.");
        }

        var ids = sets.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var before = new HashSet<string>(StringComparer.Ordinal);

        foreach (var set in sets)
        {
            foreach (var dependency in set.Dependencies)
            {
                if (!ids.Contains(dependency))
                    problems.Add($"'{set.Id}' depends on '{dependency}', which is not in this sequence. A dependency on nothing cannot stop anything.");
                else if (!before.Contains(dependency))
                    problems.Add($"'{set.Id}' depends on '{dependency}', which comes LATER in the sequence. Wave sets run in the order given, so the model would run after the consumer that rests on it.");
            }

            before.Add(set.Id);
        }

        return problems;
    }
}
