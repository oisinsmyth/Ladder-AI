using System.Text.Json;
using System.Text.Json.Serialization;
using Harness.Results;

namespace Harness.Gate;

/// <summary>Exit codes for <c>harness-gate compress</c>, the same shape as <c>derive</c> and <c>converter reachable-state</c>.</summary>
public static class CompressExit
{
    /// <summary>A usable table was written. Every declared preset either scaled or is a literal.</summary>
    public const int Computed = 0;

    /// <summary>Usage, an unreadable input, or a REFUSAL — a preset that cannot be scaled at this factor.</summary>
    public const int Refused = 1;

    /// <summary>
    /// <b>NOTHING COMPUTED.</b> The submission declares no <c>blockCompression</c> presets, so there is
    /// nothing to scale and no evidence has been produced about anything.
    ///
    /// <para>Empty is not clean, and here it is the exact hole this command exists to fill: a caller
    /// reading exit 0 over an empty table would deploy an UNCOMPRESSED program while believing it had
    /// applied a factor — and the wave, whose backstop already re-expresses at that factor, would then
    /// report a spurious TIMED-OUT on a healthy block.</para>
    /// </summary>
    public const int NothingComputed = 2;
}

/// <summary>
/// 🔴 <b><c>harness-gate compress</c> — the half of time compression that was never built.</b>
///
/// <para><see cref="TimeCompression.Plan"/> has computed <c>comp_min</c> and all four ceilings for a long
/// time, gates 10a and 10b check them, and <c>ScanBudget</c> re-expresses every declared duration at the
/// factor. <b>Nothing ever changed a preset on the device.</b> So the factor governed only how long the
/// harness was willing to WAIT, and raising it against an unscaled plant would have produced a spurious
/// TIMED-OUT on a healthy test — the failure this project calls worse than a spurious FAILED, because it
/// is believed.</para>
///
/// <para><b>It emits a FILE and not a number.</b> The deploy applies concrete values to the block's
/// parameters; re-deriving a factor at deploy time from a figure in another document is how the two
/// drift. The table carries the factor, the floor, and the plan it came from.</para>
///
/// <para><b>What it deliberately does NOT do:</b> touch the device, edit any IR, or choose which presets
/// gate the behaviour under test. The last one is a judgement about the block — a real call tree contains
/// presets already below the floor at 1×, so a blanket scale refuses immediately — and it is declared by
/// whoever knows the block.</para>
/// </summary>
public static class CompressCli
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static int Run(string[] args, TextWriter output, Func<string, string> readFile, Action<string, string> writeFile)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(writeFile);

        var submissionPath = Value(args, "--submission");
        var outPath = Value(args, "--out");
        var factorText = Value(args, "--factor");
        var budgetText = Value(args, "--budget-ms");

        if (submissionPath is null || outPath is null)
        {
            output.WriteLine("usage: harness-gate compress --submission <file> --out <file> [--factor <n> | --budget-ms <n>]");
            output.WriteLine("  --factor      run at exactly this factor, and REFUSE if the DECLARED presets do not admit it.");
            output.WriteLine("  --budget-ms   derive the factor from the budget (comp_min). X-D's rule is USE comp_min, NOT comp_max.");
            output.WriteLine();
            output.WriteLine("  Checked here: the absolute timer floor per preset, and the ratio-distortion bound against the largest");
            output.WriteLine("  DECLARED literal. NOT checked here: X-D's assertion ceiling, which needs the vectors' observation");
            output.WriteLine("  windows — that is gate 10a's, and `harness-gate check` is what runs it.");
            return CompressExit.Refused;
        }

        SubmissionDocument document;
        try
        {
            document = SubmissionDocument.Read(readFile(submissionPath));
        }
        catch (Exception e)
        {
            output.WriteLine($"NOTHING COMPUTED - could not read the submission '{submissionPath}': {e.GetType().Name}: {e.Message}");
            return CompressExit.NothingComputed;
        }

        var inputs = GateCli.ToCompressionInputs(document);

        if (inputs is null || inputs.Presets.Count == 0)
        {
            output.WriteLine("NOTHING COMPUTED - the submission declares no `blockCompression.presets`, so there is nothing to scale.");
            output.WriteLine("  *** THIS IS NOT 'no compression is needed'. *** A table over zero presets would deploy an UNCOMPRESSED program");
            output.WriteLine("  while the wave's backstop re-expresses at the factor, and the symptom is a spurious TIMED-OUT on a healthy block.");
            output.WriteLine("  Declare the presets that GATE the behaviour under test, each with its source (Data or Literal).");
            return CompressExit.NothingComputed;
        }

        // ---- the factor: taken, or derived from the budget --------------------------------------------
        int factor;
        string factorBasis;

        if (factorText is not null)
        {
            if (!int.TryParse(factorText, out factor) || factor < 1)
            {
                output.WriteLine($"--factor '{factorText}' is not a whole number of 1 or more.");
                return CompressExit.Refused;
            }

            factorBasis = $"stated on the command line as --factor {factor}";
        }
        else if (budgetText is not null)
        {
            if (!double.TryParse(budgetText, out var budgetMs) || budgetMs <= 0)
            {
                output.WriteLine($"--budget-ms '{budgetText}' is not a positive number of milliseconds.");
                return CompressExit.Refused;
            }

            if (inputs.PlantMs is not { } plantMs || plantMs <= 0)
            {
                output.WriteLine("NOTHING COMPUTED - --budget-ms needs the behaviour's plant time, and `blockCompression.plantMs` is absent.");
                return CompressExit.NothingComputed;
            }

            // *** comp_min, NOT comp_max. *** X-D's rule: the ceiling is a limit and never a target, so the
            // factor is the LEAST that fits the budget. Compressing harder than necessary spends ceiling
            // headroom for no gain and pushes assertions toward the sampling floor.
            factor = (int)Math.Ceiling(TimeCompression.MinimumFor(plantMs, budgetMs));
            factor = Math.Max(1, factor);
            factorBasis = $"derived as comp_min from plant {plantMs:0} ms against a budget of {budgetMs:0} ms";
        }
        else
        {
            output.WriteLine("neither --factor nor --budget-ms was given, so there is no factor to apply.");
            output.WriteLine("  There is deliberately no default: 1 would silently emit an uncompressed table that reads like a compressed one.");
            return CompressExit.Refused;
        }

        var table = CompressedPresets.For(inputs.Presets, factor);

        output.WriteLine($"FACTOR: {factor} — {factorBasis}.");
        output.WriteLine(table.Summary);

        foreach (var p in table.Presets)
            output.WriteLine($"  [{p.Outcome,-18}] {p.Name}: {p.Detail}");

        output.WriteLine(table.RatioDetail);

        if (table.RatioDistorted)
        {
            output.WriteLine();
            output.WriteLine($"REFUSED at comp {factor} by the ratio-distortion bound. Nothing was written.");
            return CompressExit.Refused;
        }

        if (table.Refused.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"REFUSED at comp {factor}: {table.Refused.Count} preset(s) cannot be scaled. Nothing was written.");
            output.WriteLine("  *** A PARTIALLY APPLIED COMPRESSION IS A DIFFERENT PLANT, NOT A FASTER ONE. *** Lower the factor, or");
            output.WriteLine("  revisit whether an already-sub-floor preset belongs in the gating set at all.");
            return CompressExit.Refused;
        }

        if (!table.Usable)
        {
            output.WriteLine();
            output.WriteLine($"REFUSED - the table is not usable at comp {factor}: nothing would be deployed, so applying it would leave the");
            output.WriteLine("  program uncompressed while every consumer believes otherwise.");
            return CompressExit.Refused;
        }

        var payload = new
        {
            factor,
            factorBasis,
            floorMs = table.FloorMs,
            summary = table.Summary,
            presets = table.Presets.Select(p => new
            {
                name = p.Name,
                source = p.Source.ToString(),
                nominalMs = p.NominalMs,
                outcome = p.Outcome.ToString(),
                scaledMs = double.IsNaN(p.ScaledMs) ? (double?)null : p.ScaledMs,
                detail = p.Detail,
            }).ToArray(),
        };

        var text = JsonSerializer.Serialize(payload, Json);
        writeFile(outPath, text);

        output.WriteLine();
        output.WriteLine($"WROTE {outPath} — {table.Deployable.Count} preset(s) for the deploy to apply.");
        output.WriteLine("  *** THIS FILE IS NOT APPLIED BY ANYTHING HERE. *** It is the deploy's input: the values go into the block's");
        output.WriteLine("  parameters, which changes the program hash and therefore the BUILD STAMP — correctly, because a differently");
        output.WriteLine("  compressed program is a different program and the verifying gateway should refuse a mismatch.");

        return CompressExit.Computed;
    }

    private static string? Value(string[] args, string flag)
    {
        var i = Array.IndexOf(args, flag);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
