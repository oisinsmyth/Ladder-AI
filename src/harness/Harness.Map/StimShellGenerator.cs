using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>One emitted shell network.</summary>
/// <param name="Id">Its stable skeleton id (<c>S1</c>..<c>S18</c>) — the same on every head.</param>
/// <param name="Number">Its network number in the emitted block. Contiguous from 1.</param>
/// <param name="Title">The shell's title for it. Identical across heads by design.</param>
/// <param name="Rungs">The statements, in emission order.</param>
public sealed record StimShellNetwork(string Id, int Number, string Title, IReadOnlyList<string> Rungs);

/// <summary>The shell, and everything the author still owes.</summary>
/// <param name="Networks">The emitted networks, numbered 1..N contiguously.</param>
/// <param name="Ir">The same thing as IR text, ready to be the head's first N networks.</param>
/// <param name="RequiredUdtMembers">The UDT members the shell references and does not create.</param>
/// <param name="RequiredStatics">The statics the shell references and does not create.</param>
/// <param name="Obligations">
/// What the generator cannot do and a person must — chiefly comments and the disarm lever. Stated as
/// output rather than left in a README, because a caveat only a reader of the design meets has already
/// failed the person it was written for.
/// </param>
public sealed record StimShellResult(
    IReadOnlyList<StimShellNetwork> Networks,
    string Ir,
    IReadOnlyList<string> RequiredUdtMembers,
    IReadOnlyList<string> RequiredStatics,
    IReadOnlyList<string> Obligations);

/// <summary>
/// 🔴 <b>The 18-network stimulus shell, generated from a declared head spec.</b>
///
/// <para><b>This is mechanism, not a claim about a plant.</b> The shell is the clock, the phases, the
/// cleardowns, the arming window, and the publication and bookkeeping of outcomes. It contains exactly
/// ONE write to the block under test (the fault reset, S9). Everything that makes a head a head — the
/// disarm lever, the drives, the scenario timeline, the plant model — is authored, stays authored, and is
/// <c>lad-coder</c>'s under hard rule 8's mechanism/plant-model line.</para>
///
/// <para><b>Ported under an explicit per-item data-boundary permission (2026-08-23), MECHANISM ONLY.</b>
/// The shell's structure, its refusals and the shape of a head spec came out; every input and every piece
/// of evidence stayed in the job folder. 🔴 <b>The committed test corpus is therefore an INVENTED head, and
/// a green committed suite is NOT evidence that this reproduces a real one.</b> That evidence exists and is
/// re-run where the real specs live. The leak shape here is VOCABULARY, not copied code: this generator
/// names nothing it did not invent.</para>
///
/// <para><b>Why it is worth generating.</b> A conformance lane's third block is an 18-network shell whose
/// every network is derived exactly from three declared lists — measured, not asserted: the source
/// renderer reproduces one existing head's shell at 18 of 18 byte-for-byte and the other at 15 of 18, the
/// three differences enumerated and all cosmetic. Hand-building it is what makes a third lane cost a day,
/// and a third lane is what "N lanes" has been waiting on.</para>
///
/// <para><b>Contract copied from <see cref="CopyLayerGenerator"/>:</b> pure text-out, no filesystem, no
/// Portal, and every uncertainty a refusal rather than a guess.</para>
/// </summary>
public static class StimShellGenerator
{
    private static readonly Regex MemberPath =
        new("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)+$", RegexOptions.Compiled);

    private static readonly Regex Identifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private static readonly Regex TimeLiteral = new("^T#", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// 🔴 <b>Boundary statics the shell references BY FIXED NAME, so the spec must declare them.</b> S6
    /// reads <c>HeadEnd</c> and S16 reads <c>TailEnd</c>. A spec that names its phases anything else emits
    /// rungs referring to statics that do not exist — which the converter cannot know is wrong, and which
    /// surfaces as a compile error at best and as a resolution to the wrong thing at worst.
    /// </summary>
    private static readonly string[] RequiredBoundaries = { "HeadEnd", "TailEnd" };

    /// <summary>Phase membership bits the shell references by fixed name (S8/S9/S11/S12/S13/S15).</summary>
    private static readonly string[] RequiredPhaseBits =
        { "InScenario", "InHeadDisarm", "InHeadVerify", "InTailVerify" };

    /// <summary>
    /// 🔴 <b>What the shell SETS and therefore what S17 must clear — DERIVED, so the two cannot disagree.</b>
    /// The README's rule is prose: <i>"miss one and it never clears"</i>. This computes the set instead, so
    /// the refusal is exact rather than a reminder. <c>Stim.CycleCounted</c> is conditional on there being
    /// any counting edge, which is why the set is a method and not a constant.
    /// </summary>
    private static IReadOnlyList<string> BitsTheShellSets(StimHeadSpec spec)
    {
        var bits = new List<string>
        {
            "Stim.ArrivedDirty",    // S11
            "Stim.InertAtStart",    // S12
            "Stim.RecoverFailed",   // S13
            "Stim.InertAtEnd",      // S13
            "Stim.ScenarioDone",    // S16
        };

        if (spec.CycleEdges.Count > 0)
            bits.Add("Stim.CycleCounted");   // S14, and only when S14 is emitted at all

        return bits;
    }

    public static StimShellResult Generate(StimHeadSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        Validate(spec);

        var built = new (string Id, string Title, IReadOnlyList<string> Rungs)[]
        {
            ("S1",  "Index Latch",               S1(spec)),
            ("S2",  "Index Timer",               S2(spec)),
            ("S3",  "Elapsed Index Time",        S3(spec)),
            ("S4",  "Cleardown Dwells",          S4(spec)),
            ("S5",  "Index Phase Boundaries",    S5(spec)),
            ("S6",  "Scenario Elapsed Time",     S6(spec)),
            ("S7",  "Index Phases",              S7(spec)),
            ("S8",  "Reset Windows",             S8(spec)),
            ("S9",  "Drive The Fault Reset",     S9(spec)),
            ("S10", "Latched Cause Standing",    S10(spec)),
            ("S11", "Arrival Inert Check",       S11(spec)),
            ("S12", "Head Cleardown Outcome",    S12(spec)),
            ("S13", "Recovery Outcome",          S13(spec)),
            ("S14", "Operation Counted",         S14(spec)),
            ("S15", "Observation Window",        S15(spec)),
            ("S16", "Index Complete",            S16(spec)),
            ("S17", "Re-Arm For The Next Index", S17(spec)),
            ("S18", "Published Phase",           S18(spec)),
        };

        // A network with no rungs is OMITTED, not emitted empty — S14 when the head counts nothing. The
        // numbering then stays contiguous, which is what keeps "network 12 is the head cleardown outcome"
        // true across heads that differ.
        var networks = new List<StimShellNetwork>();
        foreach (var (id, title, rungs) in built)
        {
            if (rungs.Count == 0)
                continue;

            networks.Add(new StimShellNetwork(id, networks.Count + 1, title, rungs));
        }

        return new StimShellResult(
            networks,
            RenderIr(networks),
            RequiredUdtMembers(spec),
            RequiredStatics(spec),
            Obligations(spec));
    }

    // --- the eighteen ------------------------------------------------------------------------------

    private static string[] S1(StimHeadSpec s) =>
        new[] { "COIL Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone" };

    private static string[] S2(StimHeadSpec s) =>
        new[] { $"TON(RunTimer, IN := Running, PT := {s.Watchdog})" };

    private static string[] S3(StimHeadSpec s) =>
        new[] { "MOVE(EN := TRUE, IN := RunTimer.ET) => RunT" };

    private static string[] S4(StimHeadSpec s) => new[]
    {
        $"MOVE(EN := TRUE, IN := {s.Dwell}) => DisarmDwell",
        $"MOVE(EN := TRUE, IN := {s.Dwell}) => ResetDwell",
        $"MOVE(EN := TRUE, IN := {s.Dwell}) => VerifyDwell",
    };

    /// <summary>Each boundary is the previous one plus this phase's duration; the first is the duration.</summary>
    private static string[] S5(StimHeadSpec s)
    {
        var rungs = new List<string>();
        string? previous = null;

        foreach (var phase in s.Phases)
        {
            rungs.Add(previous is null
                ? $"MOVE(EN := TRUE, IN := {phase.Duration}) => {phase.End}"
                : $"ADD(EN := TRUE, IN1 := {previous}, IN2 := {phase.Duration}) => {phase.End}");

            previous = phase.End;
        }

        rungs.Add("ADD(EN := TRUE, IN1 := Stim.ResetAt, IN2 := ResetDwell) => ResetPulseEnd");
        return rungs.ToArray();
    }

    private static string[] S6(StimHeadSpec s) => new[]
    {
        "MOVE(EN := RunT < HeadEnd, IN := T#0S) => ScenT",
        "SUB(EN := RunT >= HeadEnd, IN1 := RunT, IN2 := HeadEnd) => ScenT",
    };

    private static string[] S7(StimHeadSpec s)
    {
        var rungs = new List<string>();
        string? previous = null;

        foreach (var phase in s.Phases)
        {
            rungs.Add(previous is null
                ? $"COIL {phase.Bit} := Running AND RunT < {phase.End}"
                : $"COIL {phase.Bit} := Running AND RunT >= {previous} AND RunT < {phase.End}");

            previous = phase.End;
        }

        return rungs.ToArray();
    }

    private static string[] S8(StimHeadSpec s) => new[]
    {
        "COIL ResetPulse := " + string.Join(" OR ", s.Phases.Where(p => p.Kind == StimPhaseKind.Reset).Select(p => p.Bit)),
        "COIL ScenarioReset := Stim.ResetMode = 2 OR Stim.ResetMode = 1 "
            + "AND ScenT >= Stim.ResetAt AND ScenT < ResetPulseEnd",
    };

    /// <summary>🔴 The shell's ONLY write to the block under test, and the only network that may make it.</summary>
    private static string[] S9(StimHeadSpec s) =>
        new[] { $"COIL {s.UutReset} := ResetPulse OR InScenario AND ScenarioReset" };

    private static string[] S10(StimHeadSpec s) =>
        new[] { "COIL CauseStanding := " + (s.Causes.Count == 0 ? "FALSE" : string.Join(" OR ", s.Causes)) };

    private static string[] S11(StimHeadSpec s) =>
        new[] { "SCOIL Stim.ArrivedDirty := InHeadDisarm AND CauseStanding" };

    private static string[] S12(StimHeadSpec s) => new[]
    {
        "SCOIL Stim.InertAtStart := InHeadVerify AND NOT CauseStanding",
        "RCOIL Stim.InertAtStart := InHeadVerify AND CauseStanding",
    };

    private static string[] S13(StimHeadSpec s) => new[]
    {
        "SCOIL Stim.RecoverFailed := InTailVerify AND CauseStanding",
        "SCOIL Stim.InertAtEnd := InTailVerify AND NOT CauseStanding",
        "RCOIL Stim.InertAtEnd := InTailVerify AND CauseStanding",
    };

    /// <summary>Omitted entirely when the head counts nothing — an empty network would be a lie about coverage.</summary>
    private static string[] S14(StimHeadSpec s)
    {
        if (s.CycleEdges.Count == 0)
            return Array.Empty<string>();

        var edges = string.Join(" OR ", s.CycleEdges);
        var body = s.CycleEdges.Count > 1 ? $"({edges})" : edges;
        return new[] { $"SCOIL Stim.CycleCounted := {body} AND Running" };
    }

    private static string[] S15(StimHeadSpec s) =>
        new[] { "COIL Stim.Armed := InScenario AND ScenT >= Stim.ArmAt AND ScenT < Stim.ArmUntil" };

    private static string[] S16(StimHeadSpec s) =>
        new[] { "SCOIL Stim.ScenarioDone := Running AND RunT >= TailEnd" };

    private static string[] S17(StimHeadSpec s) =>
        s.OutcomeBits.Select(b => $"RCOIL {b} := NOT Stim.Start AND NOT Running").ToArray();

    /// <summary>
    /// Phase codes are POSITIONAL and they already mean different phases on different heads. Publish the
    /// mapping with the head; never hard-code a phase number in a shared decoder.
    /// </summary>
    private static string[] S18(StimHeadSpec s)
    {
        var rungs = new List<string> { "MOVE(EN := TRUE, IN := 0) => Stim.Phase" };
        rungs.AddRange(s.Phases.Select((p, i) => $"MOVE(EN := {p.Bit}, IN := {i + 1}) => Stim.Phase"));
        return rungs.ToArray();
    }

    // --- refusals ------------------------------------------------------------------------------------

    private static void Validate(StimHeadSpec spec)
    {
        if (spec.Phases is null || spec.Phases.Count == 0)
            throw new ArgumentException("a head with no phases has no index shell at all.", nameof(spec));

        // 🔴 Without a reset phase S8's ResetPulse has no terms, so the head can NEVER clear the block —
        // and every cleardown verification then reports whatever was already standing.
        if (spec.Phases.All(p => p.Kind != StimPhaseKind.Reset))
        {
            throw new ArgumentException(
                "no phase is of kind Reset, so the reset pulse would have no terms and this head could never clear "
                + "the block under test. Every cleardown outcome would then report whatever was already standing.",
                nameof(spec));
        }

        foreach (var (name, value) in new[] { ("watchdog", spec.Watchdog), ("dwell", spec.Dwell) })
        {
            if (string.IsNullOrWhiteSpace(value) || !TimeLiteral.IsMatch(value))
                throw new ArgumentException($"{name} must be a Time literal (T#...); got '{value}'.", nameof(spec));
        }

        ValidatePhases(spec);

        // 🔴 S9 owns the block's fault reset. If the author also lists it as a phase bit, an outcome bit or
        // a cause, some other emitted network writes or reads it as shell state — single-writer broken by
        // construction, which is the failure you cannot see from a compile.
        if (!MemberPath.IsMatch(spec.UutReset ?? string.Empty))
        {
            throw new ArgumentException(
                $"uutReset must be a full member path into the block under test; got '{spec.UutReset}'.", nameof(spec));
        }

        var shellNames = spec.Phases.Select(p => p.Bit)
            .Concat(spec.Phases.Select(p => p.End))
            .Concat(spec.OutcomeBits)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (shellNames.Contains(spec.UutReset!))
        {
            throw new ArgumentException(
                $"'{spec.UutReset}' is the block's fault reset AND appears as shell state. S9 is its only writer; "
                + "naming it anywhere else means another emitted network drives it too.", nameof(spec));
        }

        ValidatePaths(spec.Causes, "causes", nameof(spec));
        ValidatePaths(spec.CycleEdges, "cycleEdges", nameof(spec));

        ValidateOutcomeBits(spec);
    }

    private static void ValidatePhases(StimHeadSpec spec)
    {
        foreach (var phase in spec.Phases)
        {
            if (!Identifier.IsMatch(phase.Bit ?? string.Empty))
                throw new ArgumentException($"'{phase.Bit}' is not a usable phase membership bit.", nameof(spec));

            if (!Identifier.IsMatch(phase.End ?? string.Empty))
                throw new ArgumentException($"'{phase.End}' is not a usable phase boundary static.", nameof(spec));

            // A literal here would put a duration in the rungs where a tunable belongs, and the index could
            // then only be re-timed by regenerating the block.
            if (string.IsNullOrWhiteSpace(phase.Duration) || TimeLiteral.IsMatch(phase.Duration))
            {
                throw new ArgumentException(
                    $"phase '{phase.Bit}' must take its duration from a static or a UDT member, not a literal "
                    + $"('{phase.Duration}') — otherwise the index can only be re-timed by regenerating the block.",
                    nameof(spec));
            }
        }

        foreach (var (kind, names) in new[]
                 {
                     ("membership bit", spec.Phases.Select(p => p.Bit)),
                     ("boundary static", spec.Phases.Select(p => p.End)),
                 })
        {
            var duplicate = names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
            if (duplicate is not null)
            {
                throw new ArgumentException(
                    $"two phases share the {kind} '{duplicate.Key}'. Phases are positional and their bits are how "
                    + "every later network tells them apart; sharing one merges two phases silently.", nameof(spec));
            }
        }

        // 🔴 THE FIXED NAMES. The shell references these in rungs it writes itself, so a spec that does not
        // declare them emits IR pointing at statics that do not exist.
        var bits = spec.Phases.Select(p => p.Bit).ToHashSet(StringComparer.Ordinal);
        var ends = spec.Phases.Select(p => p.End).ToHashSet(StringComparer.Ordinal);

        var missing = RequiredPhaseBits.Where(b => !bits.Contains(b))
            .Concat(RequiredBoundaries.Where(e => !ends.Contains(e)))
            .ToArray();

        if (missing.Length > 0)
        {
            throw new ArgumentException(
                $"the shell references {missing.Length} name(s) this spec does not declare: {string.Join(", ", missing)}. "
                + "These are written into rungs the generator emits itself (S6 reads HeadEnd, S16 reads TailEnd, and "
                + "S8/S9/S11/S12/S13/S15 read the phase bits), so a head that names its phases otherwise emits IR "
                + "pointing at statics that do not exist.", nameof(spec));
        }
    }

    private static void ValidateOutcomeBits(StimHeadSpec spec)
    {
        if (spec.OutcomeBits is null || spec.OutcomeBits.Count == 0)
        {
            throw new ArgumentException(
                "outcomeBits is empty. A head that publishes nothing cannot be observed at all — every vector against "
                + "it would read the same thing whatever the block did.", nameof(spec));
        }

        // 🔴 COMPUTED, NOT REMEMBERED. The README's rule is prose — "miss one and it never clears" — and the
        // shell knows exactly which bits it sets, so the check is exact.
        var declared = spec.OutcomeBits.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uncleared = BitsTheShellSets(spec).Where(b => !declared.Contains(b)).ToArray();

        if (uncleared.Length > 0)
        {
            throw new ArgumentException(
                $"the shell SETS {uncleared.Length} bit(s) that S17 would never clear: {string.Join(", ", uncleared)}. "
                + "A bit set on one index and not cleared at re-arm reads as set on every index after it, and nothing "
                + "in a green run says so.", nameof(spec));
        }
    }

    private static void ValidatePaths(IReadOnlyList<string> paths, string what, string parameter)
    {
        foreach (var path in paths ?? Array.Empty<string>())
        {
            if (!MemberPath.IsMatch(path ?? string.Empty))
            {
                throw new ArgumentException(
                    $"{what} entry '{path}' is not a full member path. A bare name would resolve against the head's own "
                    + "statics rather than the block under test, which compiles and observes the wrong thing.", parameter);
            }
        }
    }

    // --- what the author still owes -------------------------------------------------------------------

    private static IReadOnlyList<string> RequiredUdtMembers(StimHeadSpec spec) => new[]
    {
        "Start", "EndAt", "ResetMode", "ResetAt", "ArmAt", "ArmUntil",
        "Phase", "Armed", "ScenarioDone", "ArrivedDirty", "InertAtStart", "InertAtEnd", "RecoverFailed",
    }.Concat(spec.CycleEdges.Count > 0 ? new[] { "CycleCounted" } : Array.Empty<string>()).ToArray();

    /// <summary>
    /// Derived from what the shell actually references, plus the per-phase pair. Deliberately not a count:
    /// the source README says "12 fixed + 2 per phase", and a number in a document is a thing that goes
    /// stale silently the first time a network changes.
    /// </summary>
    private static IReadOnlyList<string> RequiredStatics(StimHeadSpec spec) => new[]
        {
            "Running", "RunTimer", "RunT", "ScenT",
            "DisarmDwell", "ResetDwell", "VerifyDwell", "ResetPulseEnd",
            "ResetPulse", "ScenarioReset", "CauseStanding",
        }
        .Concat(spec.Phases.Select(p => p.Bit))
        .Concat(spec.Phases.Select(p => p.End))
        .Distinct(StringComparer.Ordinal)
        .ToArray();

    private static IReadOnlyList<string> Obligations(StimHeadSpec spec)
    {
        var owed = new List<string>
        {
            "WRITE THE COMMENTS. This generator emits none, deliberately: a copied comment that is subtly wrong "
            + "for your head is worse than none, and C-202 asks what the logic does — which only you know.",

            "THE DISARM LEVER is yours and the shell cannot supply any of it. Every cleardown phase assumes "
            + "nothing can create or re-create a latched cause while it runs. Get it wrong and the reset pulse "
            + "lands on a cause being re-set every scan, and no cleardown in the block ever works.",

            "THE DRIVES are yours: every input of the block under test, held inert outside InScenario.",

            "THE SCENARIO TIMELINE is yours. It is the one place existing heads disagree at the DESIGN level, "
            + "so templating it would pick a winner for a head nobody has designed yet.",

            "ANSWER THE STALENESS QUESTION IN WRITING before deploying: does the block under test contain any "
            + "detector watching a signal for standing unchanged, or a counter for advancing? For each one, what "
            + "does this head present to it, and does that presentation move? If nothing needs to move, say why. "
            + "An unanswered version of this question is what silently disarms the block under test.",

            "PUBLISH THE PHASE MAPPING with the head. Phase codes are positional and already mean different "
            + "phases on different heads; never hard-code a phase number in a shared decoder.",
        };

        // Reported, never refused. An empty cause list is legitimate; it is also indistinguishable from an
        // unfinished one, and only the author can tell which this is.
        if (spec.Causes.Count == 0)
        {
            owed.Add("NO CAUSES WERE DECLARED, so S10 holds CauseStanding permanently FALSE and every cleardown "
                   + "outcome will report inert. That is correct for a block that latches nothing and catastrophic "
                   + "for one that does — it reads as a clean bill of health, in green, forever. Confirm which.");
        }

        if (spec.CycleEdges.Count == 0)
        {
            owed.Add("NO COUNTING EDGES WERE DECLARED, so S14 is omitted entirely and Stim.CycleCounted is never "
                   + "published. Vectors must not assert on it.");
        }

        return owed;
    }

    private static string RenderIr(IReadOnlyList<StimShellNetwork> networks)
    {
        var ir = new StringBuilder();

        foreach (var network in networks)
        {
            if (ir.Length > 0)
                ir.Append('\n');

            ir.Append($"NETWORK {network.Number} \"{network.Title}\"\n");
            foreach (var rung in network.Rungs)
                ir.Append("  ").Append(rung).Append('\n');
        }

        return ir.ToString();
    }
}
