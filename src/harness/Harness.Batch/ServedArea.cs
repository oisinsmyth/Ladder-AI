using System.Text.Json;
using Harness.Device;

namespace Harness.Batch;

/// <summary>
/// What the program corpus says the Modbus server serves — or every reason it says nothing.
/// </summary>
/// <param name="Derived">
/// True ONLY when the producer read both homes of the number and they agreed. False is three different
/// facts (no comms block, the converter could not be consulted, the corpus refused) and
/// <see cref="Denominator"/> is what tells them apart. <b>None of the three is a pass.</b>
/// </param>
/// <param name="Denominator">
/// The producer's own sentence, carried verbatim. Re-wording it here would put the provenance of a
/// number in two places, which is the defect this whole item exists to remove.
/// </param>
/// <param name="Refusals">
/// A defect IN THE CORPUS — the two homes of the width disagreeing, two MB_SERVER calls, an area this
/// producer will not interpret. Distinct from "not derived": these GATE.
/// </param>
public sealed record ServedAreaFact(
    bool Derived,
    int BaseByte,
    int Registers,
    string Denominator,
    IReadOnlyList<string> Refusals)
{
    public static ServedAreaFact NotDerived(string why) =>
        new(false, 0, 0, "NOT DERIVED — " + why, Array.Empty<string>());

    /// <summary>
    /// Nobody asked. The default when a caller supplies no derivation at all — said out loud rather
    /// than left to look like a check that ran and found nothing wrong.
    /// </summary>
    public static ServedAreaFact NotAsked { get; } = NotDerived(
        "no derivation was supplied to the planner, so the program corpus was not read. The width below is "
        + "DECLARED, not derived.");
}

/// <summary>
/// 🔴 <b>THE WIDTH, ASKED OF THE PROGRAM RATHER THAN OF THE AUTHOR.</b>
///
/// <para><b>The defect.</b> <c>declaredRegisters</c> was authored by hand, per lane, and required.
/// It flows into <c>MirrorGeometry.ForCpu1214C</c>, into <c>MapAllocator</c>, into
/// <c>RegisterMap.MapHash</c>'s canonical form and therefore into the build stamp — so the stamp does
/// hash a declared width, and the gap is narrower than it first looks. What the stamp is blind to is
/// not the number: it is <b>whether the number is true of the program</b>. The committed binding at
/// <c>gen/test-project001/hopper-blockage-alarm/harness-binding.json</c> carries a
/// <c>_declaredRegistersNote</c> saying in as many words that 37 was READ FROM the comms block — by a
/// person, once. A hand-typed derivable field is only an opportunity to disagree with reality.</para>
///
/// <para><b>Why a subprocess and not a reference.</b> The converter owns the only IR parser this project
/// has, and the harness is deliberately kept off it — the same trade <c>ReachabilityParity</c> already
/// documents. <c>UnionPreflight</c> set the shape: ask the converter over a process boundary, consume
/// its JSON, and treat "could not consult it" as a REPORT rather than a refusal.</para>
///
/// <para>🔴 <b>WHAT THIS CANNOT SEE: whether the block the converter read is the block on the
/// controller.</b> It reads the staged corpus, never the CPU. The mirror's widening to 1024 registers
/// was established by probing the device from both sides and nothing here substitutes for that. The
/// claim bought is strictly smaller and must be stated that way: <b>a binding can no longer disagree
/// with the program that was staged.</b></para>
/// </summary>
public static class ServedAreaProbe
{
    public static ServedAreaFact Derive(
        string converterExe, IReadOnlyList<string> programPaths, IProcessRunner runner, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(programPaths);
        ArgumentNullException.ThrowIfNull(runner);

        if (programPaths.Count == 0)
        {
            return ServedAreaFact.NotDerived(
                "the batch names no program paths, so there was no corpus to read the served width out of. "
                + "The width is DECLARED, not derived.");
        }

        // The union corpus is materialised by `run`, not by `plan`, so the scope repeats rather than
        // being pointed at one directory — otherwise the check would only be available after the point
        // it is needed.
        var arguments = new List<string> { "served-area" };
        foreach (var path in programPaths)
        {
            arguments.Add("--project");
            arguments.Add(path);
        }

        arguments.Add("--json");

        var result = runner.Run(converterExe, arguments, timeout ?? TimeSpan.FromMinutes(2));

        if (!result.Started)
            return ServedAreaFact.NotDerived("the converter did not start: " + result.Detail + ". The width is DECLARED, not derived.");

        if (result.TimedOut)
            return ServedAreaFact.NotDerived("the converter timed out, so the program corpus was not read. The width is DECLARED, not derived.");

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(result.StandardOutput);
        }
        catch (JsonException)
        {
            return ServedAreaFact.NotDerived(
                "the converter's output was not readable JSON, so the program corpus was not read. The width is "
                + "DECLARED, not derived.");
        }

        using (doc)
        {
            var root = doc.RootElement;
            var denominator = root.TryGetProperty("denominator", out var d) ? d.GetString() : null;

            var refusals = new List<string>();
            if (root.TryGetProperty("refusals", out var array) && array.ValueKind == JsonValueKind.Array)
            {
                refusals.AddRange(array.EnumerateArray().Select(e => e.GetString() ?? e.ToString()));
            }

            if (refusals.Count > 0)
            {
                return new ServedAreaFact(false, 0, 0,
                    denominator ?? "REFUSED — the producer refused to derive a served area.", refusals);
            }

            var derived = root.TryGetProperty("derived", out var flag) && flag.ValueKind == JsonValueKind.True;

            // 🔴 The two numbers are read ONLY on the derived path. A producer that did not derive omits
            // them rather than writing 0, and a probe that defaulted them to 0 would turn "nobody
            // computed this" into a comparison that fails against every honest binding.
            if (!derived
                || !root.TryGetProperty("baseByte", out var baseByte) || baseByte.ValueKind != JsonValueKind.Number
                || !root.TryGetProperty("registers", out var registers) || registers.ValueKind != JsonValueKind.Number)
            {
                return new ServedAreaFact(false, 0, 0,
                    denominator ?? "NOT DERIVED — the producer stated no served area.", Array.Empty<string>());
            }

            return new ServedAreaFact(
                true,
                baseByte.GetInt32(),
                registers.GetInt32(),
                denominator ?? $"served area: base {baseByte.GetInt32()}, {registers.GetInt32()} register(s)",
                Array.Empty<string>());
        }
    }
}
