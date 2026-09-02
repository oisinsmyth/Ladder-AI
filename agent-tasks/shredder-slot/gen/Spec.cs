using Harness.Map;

namespace ShredderSlotGen;

/// <summary>
/// The DECLARED half of the shredder-sequencer conformance slot. Everything here is a claim a person
/// makes; every line of IR downstream of it is emitted by Harness.Map, never typed. Adapted from the
/// pusher lane's driver (agent-tasks/pusher-slot/gen) rather than invented a second time.
/// </summary>
public static class Spec
{
    public const string HeadName = "FB_ShredderSequencerStim";
    public const string UdtName = "UDT_ShredderSequencerStim";
    public const string InstanceDb = "iDB_ShredderSequencerStim";
    public const int HeadNumber = 9004;      // claim block-number FB9004
    public const int InstanceNumber = 9004;  // claim block-number DB9004

    public static StimHeadSpec Head => new(
        HeadName: HeadName,

        // NOT iDB_ShredderSequencer.IO.FaultReset: FC_ControlMain.ir:23 copies DB_Controls.FaultReset
        // into it every scan, AFTER this head runs and immediately before the CALL at :29, so a write
        // there is destroyed before the block reads it. And NOT DB_Controls.FaultReset either, which is
        // what this lane's binding first declared: StimShellGenerator S9 emits a PLAIN COIL, and three
        // heads driving one plain coil is last-writer-wins - the very collision class this lane found on
        // DB_Input.Test[]. The head therefore publishes a REQUEST and one authored rung arbitrates with
        // the pass-through idiom. Adopted from the pusher lane, which hit this first.
        UutReset: "Stim.ResetRequest",

        Watchdog: "T#10M",

        // 15 s: longer than every standing constant a cleardown must outlast before this block is
        // genuinely at rest - DB_Settings.DischargeConveyorTimeout = 10.0 s, DB_Settings.PreStartSounderTime
        // = 10.0 s and iDB_MotorFwdRevSystem_Shredder.IO.FTTime = 10.0 s. A dwell shorter than any of them
        // reads a settling block as a dirty one.
        Dwell: "T#15S",

        Phases: new[]
        {
            new StimPhase("InHeadDisarm", "HeadDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
            new StimPhase("InHeadReset", "HeadResetEnd", "ResetDwell", StimPhaseKind.Reset),
            new StimPhase("InHeadVerify", "HeadEnd", "VerifyDwell", StimPhaseKind.Verify),
            new StimPhase("InScenario", "ScenarioEnd", "Stim.EndAt", StimPhaseKind.Scenario),
            new StimPhase("InTailDisarm", "TailDisarmEnd", "DisarmDwell", StimPhaseKind.Disarm),
            new StimPhase("InTailReset", "TailResetEnd", "ResetDwell", StimPhaseKind.Reset),
            new StimPhase("InTailVerify", "TailEnd", "VerifyDwell", StimPhaseKind.Verify),
        },

        // Every latched cause reachable in this loop. The first two are sealed in FB_ShredderSequencer
        // N8 and N5 and live in a RETAIN SETPOINT UDT, so they survive a restart. The third is the
        // motor's, sealed in FB_MotorFwdRevSystem N11, and it is listed because it gates three of the
        // sequencer's step transitions: a head that cleared the sequencer's own faults and left the
        // motor's standing would run every scenario into an immediate abort. FTR and FTS are covered by
        // FaultActive, which N11 makes true whenever either stands.
        Causes: new[]
        {
            "iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault",
            "iDB_ShredderSequencer.IO.ShredderBlockedFault",
            "iDB_MotorFwdRevSystem_Shredder.IO.FaultActive",
        },

        // EMPTY, and said so rather than left to be inferred. FB_ShredderSequencer publishes no one-scan
        // counting edge on its interface; its only internal edge, ReversalStepEdge, counts entries into
        // Step 60, which is unreachable. S14 is omitted and Stim.CycleCounted is never published.
        CycleEdges: Array.Empty<string>(),

        OutcomeBits: new[]
        {
            "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
            "Stim.InertAtEnd", "Stim.ScenarioDone",
        });

    public static StimUdtDeclaration Udt => new(
        new StimUdtNaming(
            UdtName,
            "Command and published state of the shredder-sequencer stimulus head. The harness writes the "
            + "command members before an index starts and reads the published ones while it runs. The head "
            + "presents a commanded plant to FB_ShredderSequencer through the input map's own test-injection "
            + "members, so the sequencer sees equipment that behaves to a timeline rather than to a field. "
            + "THE FEEDBACK MEMBERS ARE CLOSED-LOOP BY DESIGN: the shredder motor block latches a fail-to-run "
            + "ten seconds after it commands a contactor that does not confirm, and that fault aborts three of "
            + "the sequencer's step transitions, so a feedback timeline that ignored what the motor was "
            + "actually commanded would inject aborts no vector asked for. Each modelled contactor answers the "
            + "output the PLC is really driving, after a delay the vector chooses. Every time is a duration "
            + "measured from the start of the scenario, which is the instant the head cleardown finishes, "
            + "never from Start."),
        new[]
        {
            // Comments on the members the shell's own rungs create. The generator writes none, by design.
            new StimUdtMember("Start", null, null,
                "In (harness): the index start bool. Its rising edge begins the head cleardown; its falling edge does not truncate an index, which ends only by reaching TailEnd."),
            new StimUdtMember("ScenarioDone", null, null,
                "Out: the index reached TailEnd. Latched, so a harness that was not polling at that instant still sees that it finished."),
            new StimUdtMember("EndAt", null, null,
                "In (harness): how long the scenario itself lasts. The three head dwells before it and the three tail dwells after it are the head's own and are not included."),
            new StimUdtMember("ResetAt", null, null,
                "In (harness): when the fault-reset pulse is asserted inside the scenario under ResetMode 1."),
            new StimUdtMember("ResetRequest", null, null,
                "Out: this head is asking for the plant fault-reset line. It is a REQUEST and not the line itself, because DB_Controls.FaultReset is shared with the hopper head, the pusher head, the motor block and the pusher block: the head publishes here and one authored rung arbitrates, passing whatever another head wrote through whenever this index is not running."),
            new StimUdtMember("ArrivedDirty", null, null,
                "Out: a latched cause was already standing when this index began, before the head cleardown had done anything. Not a verdict on the block - a verdict on what the previous index left behind."),
            new StimUdtMember("InertAtStart", null, null,
                "Out: the head cleardown succeeded and the block held no latched cause when the scenario began. False means the scenario ran against a block that was not clean, so its result says nothing."),
            new StimUdtMember("RecoverFailed", null, null,
                "Out: a latched cause was still standing after the tail cleardown, so the block could not be returned to rest by a reset - a finding about the block, not about the index."),
            new StimUdtMember("InertAtEnd", null, null,
                "Out: the tail cleardown left the block with no latched cause, so the next index starts clean."),
            new StimUdtMember("Armed", null, null,
                "Out: the observation window is open. Published so the copy layer's observation latches are armed by this head's phase and not from the start of the index - a free-running latch would fill from the cleardown, where the block is legitimately being driven."),
            new StimUdtMember("ArmAt", null, null,
                "In (harness): start of the observation window, measured from the start of the scenario."),
            new StimUdtMember("ArmUntil", null, null,
                "In (harness): end of the observation window."),

            // Widths the emitted rungs fix only as an integer - the generator refuses to choose, and it is
            // right to: an integer literal proves an integer and not its width. Both are Int because this
            // slot's binding mirrors both as MirrorValueType.Int, a 16-bit word at a word address.
            new StimUdtMember("ResetMode", "Int", null,
                "In (harness): 0 never asserts the fault reset inside the scenario; 1 pulses it once at ResetAt; 2 holds it true throughout, so an edge-triggered reset finds no edge to act on."),
            new StimUdtMember("Phase", "Int", null,
                "Out: which index phase is running, positional - 1 head disarm, 2 head reset, 3 head verify, 4 scenario, 5 tail disarm, 6 tail reset, 7 tail verify, 0 between indexes. The codes are this head's own; never decode them against another head's."),

            // The drives and the scenario timeline. The shell references none of these.
            new StimUdtMember("PreStartHold", "Time", null,
                "In (harness): how long the head holds the plant idle-but-healthy at the start of the scenario before it presses the cycle-start button, so a vector has a settled Step 0 to depart from."),
            new StimUdtMember("StartPressWidth", "Time", null,
                "In (harness): how long the cycle-start press is held. Many scans wide, so a press cannot fall between two scans and be missed."),
            new StimUdtMember("DischargeConfirmDelay", "Time", null,
                "In (harness): delay from the sequencer commanding the discharge conveyor to the head confirming it running. Longer than DB_Settings.DischargeConveyorTimeout and Step 20 times out to Idle with its fault latched instead of advancing."),
            new StimUdtMember("RevFeedbackDelay", "Time", null,
                "In (harness): delay from the reverse output command to the head confirming reverse running. It must stay inside the motor block's fail-to-run window or the motor faults and the step aborts for a reason the vector did not ask for."),
            new StimUdtMember("FwdFeedbackDelay", "Time", null,
                "In (harness): delay from the forward output command to the head confirming forward running. Same fail-to-run caveat as the reverse delay."),
            new StimUdtMember("HopperHighAt", "Time", null,
                "In (harness): when the hopper high-level sensor is asserted, from the start of the scenario."),
            new StimUdtMember("HopperClearAt", "Time", null,
                "In (harness): when the hopper reads clear again. The infeed restart and upstream enable delays run from here."),
            new StimUdtMember("StopAt", "Time", null,
                "In (harness): when the commanded stop event fires. WHICH stop it is comes from the three levers below, not from this - so no stop-reason value table has to be invented."),
            new StimUdtMember("StopByButton", "Bool", null,
                "In (harness): the stop event is the operator stop button. The only stop reason that also forces the pusher to mode 0, so it is separable from the other two by observation alone."),
            new StimUdtMember("StopByDownstreamLoss", "Bool", null,
                "In (harness): the stop event is loss of the downstream-running permission. It must inhibit the pusher's cycle without taking hand-jog away."),
            new StimUdtMember("StopByUnhealthy", "Bool", null,
                "In (harness): the stop event is loss of the control-circuit healthy input. It reaches the motor block as well, so a vector using it is asserting about two blocks."),
            new StimUdtMember("WithholdDischargeConfirm", "Bool", null,
                "In (harness): never confirm the discharge conveyor running, whatever the delay says. The direct route to the discharge timeout, with no dependence on comparing two durations."),
            new StimUdtMember("WithholdRevFeedback", "Bool", null,
                "In (harness): never confirm reverse running. It stalls the reverse step AND makes the motor block latch a fail-to-run after FTTime, so the observable outcome is a two-block outcome."),
            new StimUdtMember("WithholdFwdFeedback", "Bool", null,
                "In (harness): never confirm forward running. Same two-block caveat as the reverse lever."),
            new StimUdtMember("InjectMotorFault", "Bool", null,
                "In (harness): assert the field motor-fault input at the stop instant. The only way a vector raises the motor's fault deliberately rather than as a side effect of a withheld feedback."),
        });
}
