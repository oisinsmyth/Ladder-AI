using Harness.Gate;
using Harness.Results;
using Harness.Run;
using Harness.Skeleton;

namespace Harness.Loop.Tests;

/// <summary>
/// 🔴 <b>THE SETTLING DECLARATION IS ANSWERED BY THE SIGNALS IT NAMES — AND UNTIL 2026-08-20 IT WAS
/// ANSWERED BY THE WHOLE SLOT BAND.</b>
///
/// <para><c>LoopRun.Settling</c> read
/// <c>client.ReadResults(slotIndex).SequenceEqual(run.Results)</c>. <c>ReadResults</c> returns every
/// register the slot publishes, so a vector saying <i>"settle on these three signals"</i> was answered
/// with <i>"were all N registers bit-identical"</i>. <c>SettlingDeclaration.Signals</c> was resolved to a
/// register BY NOTHING anywhere in the system — <c>SignalJoin</c> recorded that as a named limit and
/// named this change as the thing that would end it.</para>
///
/// <para><b>THE MEASURED CONSEQUENCE.</b> A slot whose binding declares five registers as having NO
/// resting value — a presenter tick and a counter that run OUTSIDE the index, and a one-scan pulse —
/// cannot satisfy a whole-band comparison at all: the band differed on <b>54–69% of adjacent poll
/// pairs</b>, while the three declared signals had been stable for ~2,000 scans. <b>Sixteen of sixteen
/// assertions held and every vector came back UNSETTLED.</b></para>
///
/// <para><b>THE FIXTURE, AND WHY IT IS THIS ONE.</b> <c>TrivialBlockDefect.DoneWhileStillRunning</c> is a
/// block that latches its done flag and goes on changing its count, so its band is moving after
/// completion while a signal in that same band is not. The binding adds a THIRD result source — the limit
/// setpoint echoed back — which is stable throughout. That gives the two halves the defect needs and no
/// pre-existing fixture had: <b>a register that moves and a declared signal that does not, in one
/// band.</b> Every existing settling test declares the moving register, so all four were green on both
/// sides of this change and none of them could see it.</para>
/// </summary>
public class SettlingPerSignalTests
{
    private const int MirrorBase = 4000;
    private const int ProgramBase = 3000;

    private const string ClauseId = "REQ-014";
    private const string AssertionText = "WHEN the step is applied THEN the count reaches the limit";
    private static readonly string AssertionIdValue = AssertionId.Compute(ClauseId, AssertionText);

    /// <summary>The limit setpoint, mirrored BACK as a result. A readback, and the band's one stable register.</summary>
    private const string LimitEcho = TrivialBlock.LimitTag;

    private static string Submission(string settlingSignals, int unchangedForScans = 3) => $$"""
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 3,
      "conflictEdges": [],
      "deployment": { "noS7Transport": true },
      "model": {
        "id": "M_Ramp", "represents": ["ramp-to-limit"], "doesNotRepresent": ["overshoot"],
        "validatedAgainstPlantData": true, "declaredBy": "agent-m"
      },
      "enumeration": {
        "clauses": ["{{ClauseId}}"],
        "assertions": ["{{AssertionIdValue}}"],
        "forms": { "{{AssertionIdValue}}": "When" },
        "enumerator": "agent-c",
        "normalisedTexts": { "{{AssertionIdValue}}": "{{AssertionText}}" },
        "requiredObservations": { "{{AssertionIdValue}}": ["{{TrivialBlock.CountTag}}"] },
        "bounds": { "ramp_limit": "10" }
      },
      "map": {
        "harnessOnly": ["{{TrivialBlock.CountTag}}", "{{TrivialBlock.DoneTag}}", "{{LimitEcho}}"],
        "providedFor": { "{{TrivialBlock.CountTag}}": ["Sampled"] }
      },
      "vectors": [{
        "id": "V-1", "slot": "S0", "index": 0, "author": "agent-b",
        "clause": "{{ClauseId}}", "assertion": "{{AssertionIdValue}}",
        "inputs": { "{{TrivialBlock.StepTag}}": "5", "{{TrivialBlock.LimitTag}}": "10" },
        "startBool": "{{TrivialBlock.StartTag}}",
        "assertionForm": "When",
        "expectations": [
          { "signal": "{{TrivialBlock.CountTag}}", "nature": "PersistentState", "mode": "Sampled", "windowScans": 20, "expected": "10" }
        ],
        "settlingCondition": "the named signals are unchanged across consecutive scans",
        "settlingSignals": [{{settlingSignals}}],
        "settlingUnchangedForScans": {{unchangedForScans}},
        "maxDurationScans": 20,
        "completionSignal": "{{TrivialBlock.DoneTag}}",
        "completionValue": 1,
        "compressionFactor": 1,
        "assertedBehaviours": ["ramp-to-limit"],
        "blacklist": [],
        "kills": "a ramp that overshoots the limit by one step",
        "boundsUsed": { "ramp_limit": "10" }
      }]
    }
    """;

    /// <summary>
    /// Three result registers: the count, the done flag, <b>and the limit setpoint read back</b>.
    ///
    /// <para>The echo rests at 10 rather than 0 — the inert phase writes the next test's vector BEFORE it
    /// takes either observation (D33 consequence 1), so the setpoint is already in place when the check
    /// looks. Stating it as 10 is also what keeps this fixture out of the trap
    /// <c>InertRestPlanTests</c> names: a band where everything rests at zero cannot tell a declared zero
    /// from an assumed one.</para>
    /// </summary>
    private static readonly string Binding = $$"""
    {
      "blockName": "FC_HarnessCopyLayer",
      "blockNumber": 900,
      "baseByte": {{MirrorBase}},
      "slots": [{
        "slotId": "S0",
        "startCondition": "{{TrivialBlock.StartTag}}",
        "vectorTargets": [
          { "tag": "{{TrivialBlock.StepTag}}", "specName": "{{TrivialBlock.StepTag}}", "type": "Int" },
          { "tag": "{{TrivialBlock.LimitTag}}", "specName": "{{TrivialBlock.LimitTag}}", "type": "Int" }
        ],
        "resultSources": [
          { "tag": "{{TrivialBlock.CountTag}}", "specName": "{{TrivialBlock.CountTag}}", "type": "Int",
            "inertRest": { "value": "0", "basis": "network 1 holds the count at 0 while the start command is off" } },
          { "tag": "{{TrivialBlock.DoneTag}}", "specName": "{{TrivialBlock.DoneTag}}", "type": "Int",
            "inertRest": { "value": "0", "basis": "network 1 holds the done flag at 0 while the start command is off" } },
          { "tag": "{{LimitEcho}}", "specName": "{{LimitEcho}}", "type": "Int",
            "inertRest": { "value": "10", "basis": "the inert phase writes the next test's vector before it observes, so the setpoint is already in place" } }
        ]
      }]
    }
    """;

    private static LoopResult Run(string settlingSignals, TrivialBlockDefect defect, int unchangedForScans = 3) =>
        LoopRun.Execute(
            LoopCli.Compose(
                SubmissionDocument.Read(DerivedFixture.WithDerivation(
                    Submission(settlingSignals, unchangedForScans), DerivedFixture.ArtifactPath, Binding)),
                BindingDocument.Read(Binding),
                TrivialBlock.Generate(ProgramBase, blockNumber: 901, defect),

                // A DERIVED fixture, so gate 0c attributes the map/deployment/edges instead of refusing
                // them as hand-authored. The reader serves the artifact those records name; 0c re-hashes
                // it, and one it cannot read is NOT CHECKED rather than a pass.
                readFile: DerivedFixture.ReaderFor(Binding)),
            new SimulatedGateway(Harness.Map.MirrorGeometry.ForCpu1214C(256, MirrorBase)));

    private static ResultPackage Package(string settlingSignals, TrivialBlockDefect defect, int unchangedForScans = 3)
    {
        var result = Run(settlingSignals, defect, unchangedForScans);
        Assert.Equal(LoopOutcome.Ran, result.Outcome);
        return Assert.Single(result.Packages);
    }

    private static string Quoted(params string[] signals) => string.Join(", ", signals.Select(s => $"\"{s}\""));

    // ---------------------------------------------------------------------------------------------
    // The defect itself
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DECLARATION_WHOSE_NAMED_SIGNALS_ARE_STABLE_SETTLES_WHILE_OTHER_REGISTERS_IN_THE_BAND_MOVE()
    {
        // 🔴 *** THE WHOLE POINT, AND THE MEASURED FAILURE. *** The done flag and the setpoint echo are
        // the declared signals and both hold still; the COUNT is in the same band and is still ramping,
        // because this build never stops. Under the whole-band comparison this returned NotSettled and the
        // verdict was Unsettled — on a live wave that shape produced sixteen of sixteen assertions holding
        // and every single vector reported UNSETTLED, against registers the binding itself declares to
        // have no resting value.
        var package = Package(Quoted(LimitEcho, TrivialBlock.DoneTag), TrivialBlockDefect.DoneWhileStillRunning);

        Assert.Equal(SettlingState.Settled, package.Settling.State);
        Assert.NotEqual(ResultVerdict.Unsettled, package.Verdict);

        // *** AND THE PASS STATES ITS OWN SCOPE. *** A settled verdict here is a claim about two named
        // signals and NOT about the band, and a reader who takes it for the second has been misled by the
        // artifact rather than by their own assumption.
        Assert.Contains(LimitEcho, package.Settling.Detail, StringComparison.Ordinal);
        Assert.Contains(TrivialBlock.DoneTag, package.Settling.Detail, StringComparison.Ordinal);
        Assert.Contains("NOT ABOUT SLOT", package.Settling.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_DECLARED_SIGNAL_THAT_MOVES_STILL_FAILS_so_the_narrowing_did_not_disarm_the_check()
    {
        // The converse, and it is what stops the test above from passing against a check that simply
        // stopped comparing. Same defective build, same band — the COUNT is now one of the declared
        // signals, and it is exactly what phase 2's build exists to catch: done at 10, still ramping.
        var package = Package(Quoted(TrivialBlock.CountTag, TrivialBlock.DoneTag), TrivialBlockDefect.DoneWhileStillRunning);

        Assert.Equal(SettlingState.NotSettled, package.Settling.State);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.False(package.ConclusiveAboutTheBlock);
    }

    [Fact]
    public void THE_FAILURE_NAMES_WHICH_REGISTER_MOVED_AND_FROM_WHAT_TO_WHAT()
    {
        // 🔴 *** ATTRIBUTING THIS ON THE LIVE WAVE TOOK A FORENSIC PASS, BECAUSE SETTLING RETURNED A BARE
        // BOOLEAN. *** InertPhase collects every disagreement into a set and names them — "slot 0 R003
        // moved 5 -> 7" — and the two checks compare registers across a scan gap for the same reason.
        // There was never a case for one of them reporting and the other not.
        var package = Package(Quoted(TrivialBlock.CountTag, TrivialBlock.DoneTag), TrivialBlockDefect.DoneWhileStillRunning);

        Assert.Contains("R000", package.Settling.Detail, StringComparison.Ordinal);
        Assert.Contains($"'{TrivialBlock.CountTag}'", package.Settling.Detail, StringComparison.Ordinal);
        Assert.Contains("moved", package.Settling.Detail, StringComparison.Ordinal);
        Assert.Contains("->", package.Settling.Detail, StringComparison.Ordinal);

        // And it reaches the field a reader is actually pointed at. WhatToDoNext for Unsettled is true of
        // every road to that verdict and actionable on none of them without this.
        Assert.Contains("SETTLING:", package.WhatToDoNext, StringComparison.Ordinal);
        Assert.Contains("R000", package.WhatToDoNext, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // Empty is not clean — the two ways a per-signal check could examine nothing
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_SETTLING_SIGNAL_THE_BINDING_DOES_NOT_CARRY_IS_REFUSED_BEFORE_ANYTHING_IS_DEPLOYED()
    {
        // *** THE FAIL-CLOSED HALF. *** Comparing only the names that DID resolve would answer a weaker
        // question than the one declared, silently — and a settling check over no registers at all is
        // satisfied by any program whatsoever. It is refused above the device boundary for the reason
        // SlotJoin is: a cross-document fault found after the deployment costs a whole download.
        var result = Run(Quoted(TrivialBlock.CountTag, "SPEC.NoSuchSignal"), TrivialBlockDefect.None);

        Assert.Equal(LoopOutcome.NotBound, result.Outcome);
        Assert.Empty(result.Packages);
        Assert.Contains("SPEC.NoSuchSignal", result.Detail, StringComparison.Ordinal);
        Assert.Contains("Settling:", result.Detail, StringComparison.Ordinal);
        Assert.Contains("Nothing was generated and nothing was deployed", result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void A_SETTLING_DECLARATION_NAMING_NO_SIGNAL_AT_ALL_IS_NOT_SETTLED()
    {
        // *** THE SECOND ROAD TO ZERO REGISTERS, AND THE ONE THE WHOLE-BAND FORM CONCEALED. *** Under the
        // old comparison an empty signal list still compared the entire band and could return Settled, so
        // a vector that named nothing looked exactly like one that named everything. Per-signal it
        // compares nothing, and a check over nothing is satisfied by anything — so it is NotEstablished
        // with the reason, never Settled.
        var package = Package(settlingSignals: string.Empty, TrivialBlockDefect.None);

        Assert.Equal(SettlingState.NotEstablished, package.Settling.State);
        Assert.Equal(ResultVerdict.Unsettled, package.Verdict);
        Assert.Contains("EMPTY IS NOT CLEAN", package.Settling.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void PROSE_WITH_NO_SCAN_COUNT_IS_STILL_NotEstablished_and_now_says_why()
    {
        // Unchanged behaviour, newly legible: zero remains legal, meaningful and NOT a pass. What is new
        // is that the package says which of the several roads to NotEstablished it took.
        var package = Package(Quoted(TrivialBlock.CountTag), TrivialBlockDefect.None, unchangedForScans: 0);

        Assert.Equal(SettlingState.NotEstablished, package.Settling.State);
        Assert.Contains("settlingUnchangedForScans", package.Settling.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void THE_UNAFFECTED_CASE_a_correct_block_settles_on_the_signal_it_is_asked_about()
    {
        // Asserted as deliberately as the failures. A narrowing that made everything settle would pass
        // every test above except this one.
        var package = Package(Quoted(TrivialBlock.CountTag, TrivialBlock.DoneTag), TrivialBlockDefect.None);

        Assert.Equal(SettlingState.Settled, package.Settling.State);
        Assert.Equal(ResultVerdict.Pass, package.Verdict);
    }
}
