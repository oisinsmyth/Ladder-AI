using System.Globalization;
using Harness.Wire;

namespace Harness.Results;

/// <summary>One vector's scaled scenario coordinates, for the report.</summary>
public sealed record ScenarioScaleEntry(string VectorId, string Input, long DeclaredMs, long ScaledMs);

/// <summary>What the scaling did, or why it could not.</summary>
public sealed record ScenarioScaleResult(
    IReadOnlyList<SubmissionVector> Vectors,
    IReadOnlyList<string> Refusals,
    IReadOnlyList<ScenarioScaleEntry> Scaled,
    string Report)
{
    public bool Ok => Refusals.Count == 0;
}

/// <summary>
/// 🔴 <b>SCALE THE SCENARIO WITH THE BLOCK, OR NOTHING GETS FASTER AND THE TEST STOPS MEANING WHAT IT
/// SAYS.</b>
///
/// <para><b>The trap, and it was nearly walked into.</b> Compressing a block's timer presets makes its
/// windows run <c>n</c> times faster. But the stimulus model plays its scenario against an IEC timer read
/// in REAL milliseconds — the scenario coordinates are plant times compared against elapsed time, not a
/// tick count. So compressing the presets ALONE leaves the scenario playing at 1x while the block runs at
/// <c>n</c>x: the run takes exactly as long as before, <b>and every event the vector placed relative to a
/// window now lands somewhere else.</b> The verdicts would still be produced, and they would be about a
/// test nobody designed.</para>
///
/// <para><b>Why not scale the model's tick instead</b> — the obvious-looking alternative. The tick drives
/// a dither whose period sets the model's own stability ceiling, and the averaging window under test has
/// to contain both dither levels for the model's declared behaviour to hold. Shortening the tick removes
/// the very term that makes that ceiling calculable, and drives the achieved period toward one scan,
/// which is a different stimulus again. <b>The scenario coordinates are the right knob and the tick is
/// not.</b></para>
///
/// <para><b>Which inputs are scenario coordinates cannot be inferred</b> — a stimulus model's inputs are
/// a mix of times, selectors and levels, and the model's own tick is itself time-valued and must NOT
/// scale. So the set is declared, and this class scales exactly what it was given.</para>
/// </summary>
public static class ScenarioScale
{
    /// <summary>
    /// Re-express each declared scenario coordinate at <paramref name="compression"/>.
    ///
    /// <para><b>At factor 1 this is not skipped</b>: it returns the vectors unchanged with a report saying
    /// so, which makes "the scenario was not compressed" a positive statement rather than an absence.</para>
    /// </summary>
    public static ScenarioScaleResult Apply(
        IReadOnlyList<SubmissionVector> vectors,
        RuntimeCompression compression,
        IReadOnlyList<string>? declaredInputs)
    {
        ArgumentNullException.ThrowIfNull(vectors);
        ArgumentNullException.ThrowIfNull(compression);

        var factor = compression.Factor;

        if (factor == 1)
        {
            return new ScenarioScaleResult(vectors, Array.Empty<string>(), Array.Empty<ScenarioScaleEntry>(),
                "SCENARIO SCALE: the wave runs at comp 1, so no scenario coordinate is re-expressed and every value reaches the device "
                + "exactly as the vector declared it. Stated rather than skipped.");
        }

        if (declaredInputs is null)
        {
            return new ScenarioScaleResult(vectors,
                new[]
                {
                    $"the wave runs at comp {factor} and the submission does not say which of its inputs are scenario coordinates. "
                    + "*** COMPRESSING THE BLOCK WITHOUT THE SCENARIO IS THE WORST OF THE THREE OUTCOMES: *** the run takes exactly as long "
                    + "as before, and every event the vector placed relative to a window lands somewhere else, so the verdicts are about a "
                    + "test nobody designed. Declare `scenarioTimeInputs` — or declare it EMPTY to claim positively that this stimulus has "
                    + "no time-valued scenario data.",
                },
                Array.Empty<ScenarioScaleEntry>(),
                "SCENARIO SCALE: NOT DECLARED.");
        }

        if (declaredInputs.Count == 0)
        {
            return new ScenarioScaleResult(vectors, Array.Empty<string>(), Array.Empty<ScenarioScaleEntry>(),
                $"SCENARIO SCALE: the submission declares EMPTY at comp {factor} — the positive claim that this stimulus has no "
                + "time-valued scenario data, so there is nothing whose scale could disagree with the block's. That is a real claim about "
                + "the model and not an omission; a ramp-to-limit stimulus, whose completion is a count reaching a limit, is the case it "
                + "is for.");
        }

        var refusals = new List<string>();
        var scaled = new List<ScenarioScaleEntry>();
        var rewritten = new List<SubmissionVector>(vectors.Count);

        // A name matching nothing anywhere is a typo, and it is worth saying separately: per-vector
        // refusals below would report it once per vector and bury the one fact that explains all of them.
        foreach (var name in declaredInputs)
        {
            if (!vectors.Any(v => v.Inputs.ContainsKey(name)))
            {
                refusals.Add(
                    $"`{name}` is declared as a scenario coordinate and no vector in this submission has an input by that name. "
                    + "Nothing would be scaled under it, and the declaration would read as though something had been.");
            }
        }

        foreach (var vector in vectors)
        {
            var inputs = new Dictionary<string, string>(vector.Inputs, StringComparer.Ordinal);
            var changed = false;

            foreach (var name in declaredInputs)
            {
                if (!inputs.TryGetValue(name, out var text))
                {
                    // *** REFUSED, NOT SKIPPED. *** A vector missing one declared coordinate runs a scenario
                    // whose events are at MIXED scales — some compressed, some not — which is a worse state
                    // than either extreme and is invisible in every artifact.
                    refusals.Add(
                        $"vector '{vector.Id}' declares no input `{name}`, which the submission lists as a scenario coordinate. "
                        + "Scaling the rest would leave this vector's scenario at mixed scales.");
                    continue;
                }

                if (!long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var declared))
                {
                    refusals.Add($"vector '{vector.Id}' input `{name}` reads \"{text}\", which is not a whole number of milliseconds.");
                    continue;
                }

                if (declared % factor != 0)
                {
                    // Rounding here would move an event by up to half a scan and say nothing. These are the
                    // coordinates the assertions are placed against, so a silent shift is a silent change to
                    // what the test measures.
                    refusals.Add(
                        $"vector '{vector.Id}' input `{name}` is {declared} ms, which does not divide by {factor}. "
                        + $"Rounding would move the event by up to {factor - 1} ms without saying so, and these are the coordinates the "
                        + "assertions are placed against. Choose a declared value that divides, or a factor that divides it.");
                    continue;
                }

                var value = declared / factor;
                inputs[name] = value.ToString(CultureInfo.InvariantCulture);
                scaled.Add(new ScenarioScaleEntry(vector.Id, name, declared, value));
                changed = true;
            }

            rewritten.Add(changed ? vector with { Inputs = inputs } : vector);
        }

        var report = refusals.Count > 0
            ? $"SCENARIO SCALE: REFUSED at comp {factor}. " + string.Join(" ", refusals)
            : $"SCENARIO SCALE: {scaled.Count} coordinate(s) across {vectors.Count} vector(s) re-expressed at comp {factor} — "
              + string.Join("; ", scaled.Select(s => $"{s.VectorId}.{s.Input} {s.DeclaredMs} -> {s.ScaledMs} ms"))
              + ". *** THE VECTORS STILL DECLARE PLANT TIME; THIS IS WHAT REACHES THE DEVICE. *** The gates read the declared values, so "
              + "a backstop bounded against a scenario's own end is bounded in the same units the author wrote it in.";

        return new ScenarioScaleResult(
            refusals.Count > 0 ? vectors : rewritten,
            refusals,
            scaled,
            report);
    }
}
