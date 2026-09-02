using Harness.Map;

namespace PusherSlotGen;

/// <summary>
/// The DECLARED half of the pusher conformance slot. Everything here is a claim a person makes;
/// every line of IR downstream of it is emitted by Harness.Map, never typed.
/// </summary>
public static class Spec
{
    public const string HeadName = "FB_PusherStim";
    public const string UdtName = "UDT_PusherStim";
    public const string InstanceDb = "iDB_PusherStim";
    public const int HeadNumber = 9001;   // claim block-number FB9001
    public const int InstanceNumber = 9001; // claim block-number DB9001

    public static StimHeadSpec Head => new(
        HeadName: HeadName,
        // NOT the block under test's own FaultReset member: FC_ControlMain NETWORK 5 copies
        // DB_Controls.FaultReset into iDB_PusherControl.IO.FaultReset every scan, AFTER this head
        // runs, so a write there is destroyed before the block reads it. And NOT DB_Controls.FaultReset
        // either: FB_HopperBlockageStim already writes that line unconditionally, so a second
        // unconditional writer would silently disable the deployed HBA slot. The head therefore
        // publishes a REQUEST and one authored rung arbitrates. See the hand-back.
        UutReset: "Stim.ResetRequest",
        Watchdog: "T#10M",
        // 8 s: longer than BOTH standing settings that decide when this block is genuinely at rest —
        // DB_Settings.PressureClearResumeDelay = 5.0 s (how long HighPressure must stay clear before
        // PressureHold releases) and DB_Settings.PusherPumpRunOnTime = 5.0 s (how long RunPowerPack
        // holds after the last demand). A dwell shorter than either reads a settling block as a dirty one.
        Dwell: "T#8S",
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
        Causes: new[]
        {
            "iDB_PusherControl.IO.Blocked",
            "iDB_PusherControl.IO.EndTravelTimeoutFault",
            "iDB_PusherControl.IO.ParkedTimeoutFault",
            "iDB_PusherControl.IO.BothSwitchesFault",
            "iDB_PusherControl.PressureHold",
            "iDB_PusherControl.IO.Cycling",
        },
        CycleEdges: Array.Empty<string>(),
        OutcomeBits: new[]
        {
            "Stim.ArrivedDirty", "Stim.InertAtStart", "Stim.RecoverFailed",
            "Stim.InertAtEnd", "Stim.ScenarioDone",
        });

    private static StimUdtMember Edge(string signal, string what) => new(
        signal + "Initial", "Bool", null,
        $"In (harness): the level {what} holds from the start of the scenario until Edge1.");

    private static StimUdtMember At(string signal, int n, string what) => new(
        signal + "Edge" + n, "Time", null,
        n == 1
            ? $"In (harness): when {what} leaves its initial level, measured from the start of the scenario. A time at or past EndAt means it never does."
            : $"In (harness): when {what} returns to its initial level, measured from the start of the scenario. A time at or past EndAt means it never does.");

    public static StimUdtDeclaration Udt => new(
        new StimUdtNaming(
            UdtName,
            "Command and published state of the pusher stimulus head. The harness writes the command "
            + "members before an index starts and reads the published ones while it runs. Each of the "
            + "eight commanded plant signals is a two-edge timeline: it holds its Initial level until "
            + "Edge1, takes the opposite level from Edge1 to Edge2, and returns to Initial at Edge2 - so "
            + "one member set expresses hold, step, pulse and gap without the head knowing what any "
            + "scenario is for. Every time is a duration measured from the start of the scenario, which "
            + "is the instant the head cleardown finishes, never from Start. An edge time at or past "
            + "EndAt is how a timeline says the signal never changes."),
        new[]
        {
            // Comments on the members the shell's own rungs create. The generator writes none, by design.
            new StimUdtMember("Start", null, null,
                "In (harness): the index start bool. Its rising edge begins the head cleardown; its falling edge does not truncate an index, which ends only by reaching TailEnd."),
            new StimUdtMember("ScenarioDone", null, null,
                "Out: the index reached TailEnd. Latched, so a harness that was not polling at that instant still sees that it finished."),
            new StimUdtMember("EndAt", null, null,
                "In (harness): how long the scenario itself lasts, measured from the start of the scenario. The head cleardown before it and the tail cleardown after it are the head's own fixed dwells and are not included."),
            new StimUdtMember("ResetAt", null, null,
                "In (harness): when the fault-reset pulse is asserted inside the scenario under ResetMode 1, measured from the start of the scenario."),
            new StimUdtMember("ResetRequest", null, null,
                "Out: this head is asking for the plant fault-reset line. It is a REQUEST and not the line itself, because the hopper head drives the same line: the head publishes here and one authored rung arbitrates, passing the other head's value through whenever this index is not running."),
            new StimUdtMember("ArrivedDirty", null, null,
                "Out: a latched cause was already standing when this index began, before the head cleardown had done anything. Not a verdict on the block - a verdict on what the previous index left behind."),
            new StimUdtMember("InertAtStart", null, null,
                "Out: the head cleardown succeeded and the block held no latched cause when the scenario began. False means the scenario ran against a block that was not clean, so its result says nothing."),
            new StimUdtMember("RecoverFailed", null, null,
                "Out: a latched cause was still standing after the tail cleardown, so the block could not be returned to rest by a reset - which is a finding about the block, not about the index."),
            new StimUdtMember("InertAtEnd", null, null,
                "Out: the tail cleardown left the block with no latched cause, so the next index starts clean."),
            new StimUdtMember("Armed", null, null,
                "Out: the observation window is open. Published so the copy layer's observation latches are armed by this head's phase and not from the start of the index - a free-running latch would fill from the cleardown, where the block is legitimately being driven."),
            new StimUdtMember("ArmAt", null, null,
                "In (harness): start of the observation window, measured from the start of the scenario."),
            new StimUdtMember("ArmUntil", null, null,
                "In (harness): end of the observation window."),

            // Widths the emitted rungs fix only as "an integer" - the generator refuses to choose.
            new StimUdtMember("ResetMode", "Int", null,
                "In (harness): 0 never asserts the fault reset inside the scenario; 1 pulses it once at ResetAt; 2 holds it true throughout, so an edge-triggered reset finds no edge."),
            new StimUdtMember("Phase", "Int", null,
                "Out: which index phase is running, positional - 1 head disarm, 2 head reset, 3 head verify, 4 scenario, 5 tail disarm, 6 tail reset, 7 tail verify, 0 between indexes. The codes are this head's own; never decode them against another head's."),

            // The drives and the scenario timeline. The shell references none of these.
            new StimUdtMember("Mode", "Int", null,
                "In (harness): the operator pusher mode presented for the whole scenario - 0 Off, 1 Auto, 2 Manual. Reaches the block through DB_Controls.PusherMode and the sequencer's PusherModeCmd, so it is honoured only while the sequencer's own PusherModeForceOff is clear."),

            Edge("Hopper", "the hopper high-level sensor"), At("Hopper", 1, "the hopper high-level sensor"), At("Hopper", 2, "the hopper high-level sensor"),
            Edge("ManualCycle", "the operator manual-cycle button"), At("ManualCycle", 1, "the operator manual-cycle button"), At("ManualCycle", 2, "the operator manual-cycle button"),
            Edge("JogExtend", "the operator jog-extend button"), At("JogExtend", 1, "the operator jog-extend button"), At("JogExtend", 2, "the operator jog-extend button"),
            Edge("JogRetract", "the operator jog-retract button"), At("JogRetract", 1, "the operator jog-retract button"), At("JogRetract", 2, "the operator jog-retract button"),
            Edge("Home", "the home limit switch"), At("Home", 1, "the home limit switch"), At("Home", 2, "the home limit switch"),
            Edge("FullTravel", "the full-travel limit switch"), At("FullTravel", 1, "the full-travel limit switch"), At("FullTravel", 2, "the full-travel limit switch"),
            Edge("HighPressure", "the high-pressure switch"), At("HighPressure", 1, "the high-pressure switch"), At("HighPressure", 2, "the high-pressure switch"),
            Edge("PackRunning", "the power-pack running feedback"), At("PackRunning", 1, "the power-pack running feedback"), At("PackRunning", 2, "the power-pack running feedback"),
            Edge("Healthy", "the control-circuit healthy input"), At("Healthy", 1, "the control-circuit healthy input"), At("Healthy", 2, "the control-circuit healthy input"),
            Edge("Downstream", "the downstream-running input"), At("Downstream", 1, "the downstream-running input"), At("Downstream", 2, "the downstream-running input"),
        });
}
