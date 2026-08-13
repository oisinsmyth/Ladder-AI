namespace Harness.Skeleton;

/// <summary>What the model says the block should produce, and how many scans it should take.</summary>
public sealed record TrivialBlockPrediction(int Count, int Done, int Scans);

/// <summary>How one observation compared with the model.</summary>
public sealed record TrivialBlockVerdict(bool Held, TrivialBlockPrediction Predicted, int ObservedCount, int ObservedDone, string Detail);

/// <summary>
/// Build-plan item 2.3's other half — the one trivial model.
///
/// <para><b>It is written from the block's SPECIFICATION, not from the block's IR.</b> That distinction
/// is the whole reason it exists. A model derived from the implementation is a correlated check: it
/// agrees with the code by construction and cannot fail with it, which is the failure this project was
/// built around. Read the loop below against the network titles in <see cref="TrivialBlock"/> — "add one
/// step to the count each scan UNTIL IT REACHES the limit" — and note that it never mentions an operator
/// or a rung.</para>
///
/// <para><b>What the model does NOT know:</b> which comparison the block uses, how many networks it has,
/// what order they run in, where its tags live, or that a copy layer exists. If any of those had to be
/// consulted to predict the answer, the prediction would be a restatement rather than a check.</para>
/// </summary>
public static class TrivialBlockModel
{
    /// <summary>
    /// Predict the block's settled outputs for one vector.
    ///
    /// <para>Refuses a step of zero or less, which would never reach any positive limit: the model saying
    /// "this never terminates" is a fact about the VECTOR, and a model that returned a number anyway
    /// would hide it.</para>
    /// </summary>
    public static TrivialBlockPrediction Predict(int step, int limit)
    {
        if (step <= 0)
            throw new ArgumentOutOfRangeException(nameof(step), step, "a step of zero or less never reaches a positive limit; this vector has no settled outcome to predict.");

        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "a negative limit is already reached at rest, which makes the vector's intent ambiguous rather than trivial.");

        var count = 0;
        var scans = 0;

        while (count < limit)
        {
            count += step;
            scans++;
        }

        return new TrivialBlockPrediction(count, Done: 1, Scans: scans);
    }

    /// <summary>Compare one observation of the result registers with the prediction.</summary>
    public static TrivialBlockVerdict Judge(int step, int limit, ushort[] results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var predicted = Predict(step, limit);

        if (results.Length <= TrivialBlock.DoneRegister)
        {
            return new TrivialBlockVerdict(false, predicted, 0, 0,
                $"the slot published {results.Length} result register(s); the model needs {TrivialBlock.DoneRegister + 1}. An unread register is not a passing one.");
        }

        var count = unchecked((short)results[TrivialBlock.CountRegister]);
        var done = unchecked((short)results[TrivialBlock.DoneRegister]);

        var problems = new List<string>();
        if (count != predicted.Count)
            problems.Add($"count is {count}, the model predicts {predicted.Count}");
        if (done != predicted.Done)
            problems.Add($"done is {done}, the model predicts {predicted.Done}");

        return problems.Count == 0
            ? new TrivialBlockVerdict(true, predicted, count, done,
                $"step {step} to limit {limit}: count {count}, done {done}, as predicted.")
            : new TrivialBlockVerdict(false, predicted, count, done,
                $"step {step} to limit {limit}: " + string.Join("; ", problems) + ".");
    }
}
