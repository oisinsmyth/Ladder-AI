# review-functional skill — blind-run reports (verbatim), 2026-07-16

> Verbatim §3 of `docs/notes/review-functional-validation-2026-07-16.md`, split out
> 2026-07-18 per `docs/16-future-ideas.md` FI-19. This is the unedited blind-run
> agent output (including the raw `converter review` dump). Kept verbatim on
> purpose: the drift check needs byte-identical tool output and the
> "blind executor, non-blind examiner" method needs the reports unedited so a
> skeptic can check the grading against the agent's own words. The §1/§2
> analysis and verdict stay in the validation note.

---

## 3. Blind run reports (verbatim)

Both reports as returned by the fresh-context agents, unedited except that transport escaping of
`<`, `>`, `&` (applied by the Phase-1 result's delivery channel) is restored to the characters
the agent wrote; the Phase-2 text needed no such restoration. Progress-narration lines preceding
each report's own `# Functional review` heading are omitted.

### Phase 1 (pre-fix corpus @ `4903780`)

---

# Functional review — test-project001 full corpus, scratch/prefix-corpus snapshot (2026-07-16)
Blindness: blind — fresh-context run; no authoring involvement with this corpus in this session. Per the skill's expected caveat: I read the register's own notes and doc 06's rule rationales (both cite historical examples from this project); every verdict below is re-derived from the IR itself, not from any note's summary. Regime labels supplied by the invoker.
Register: gen/test-project001/requirements.md @ 2295c64
Corpus: scratch/prefix-corpus/ir/test-project001 @ 4903780
Blocks read (all 21 files, full readable content, sidecars excluded): Main (generated), FC_Inputs (generated), FC_Outputs (generated), FC_ControlMain (generated), FC_AlarmsMain (generated), FB_ShredderSequencer (generated), FB_PusherControl (generated), FB_MotorFwdRevSystem (imported-real), UDT_ShredderSequencerIO (generated), UDT_PusherIO (generated), MotorFwdRevIOSet (imported-real), iDB_ShredderSequencer (generated), iDB_PusherControl (generated), iDB_MotorFwdRevSystem_Shredder (imported-real), DB_Input (generated), DB_Output (generated), DB_Settings (generated), DB_Controls (generated), DB_Alarms (generated), DB_AnalogInput (generated), DefaultTagTable (generated). No F-/safety-block content was encountered anywhere in the corpus.

Verdict totals: 26 implemented, 16 partial, 8 unimplemented, 11 disarmed, 7 contradicted, 1 out-of-scope. No REQ is withdrawn in the register.

## Per-REQ trace

### REQ-001 — Start command: unimplemented
Where: DI3_SYS_CycleStart → FC_Inputs N1 → DB_Input.Cycle_Start → FC_ControlMain N1 → iDB_ShredderSequencer.IO.CycleStart → FB_ShredderSequencer N6.
Evidence: `MOVE(EN := NOT StopCmd AND NOT IO.ShredderBlockedFault AND IO.CycleStart AND IO.DownstreamRunning AND IO.Step = 0, IN := 10) => IO.Step` (seq N6); `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning` (seq N1); `DI4_SYS_CycleStop ... COMMENT "Cycle stop pushbutton (NC)"` (DefaultTagTable).
Notes: The start chain is fully present and correctly shaped, but the `NOT StopCmd` term is permanently FALSE-blocking: the NC stop button reads TRUE unpressed, and no hop inverts it (FC_Inputs N1 maps `DI4_SYS_CycleStop` uninverted; FC_ControlMain N1 wires it uninverted). The start transition can never fire — the behavior can never occur. Root cause is REQ-012's polarity defect. Q-06 (momentary vs hold) carried: corpus implements a momentary level at one transition.

### REQ-002 — Downstream confirmed before start: implemented
Where: DI15_SYS_DownstreamRunning → DB_Input.Downstream_Running → IO.DownstreamRunning → seq N6.
Evidence: `... AND IO.CycleStart AND IO.DownstreamRunning AND IO.Step = 0, IN := 10` (seq N6); belt-and-braces via `StopCmd := ... OR NOT IO.DownstreamRunning` (N1).
Notes: The gate itself is correct; reachability is destroyed by REQ-001/012's defect, reported there.

### REQ-003 — Pre-start siren (10 s): implemented
Where: seq N7 (timer), N15 (output) → FC_ControlMain N7 → DB_Output.PreStart_Sounder → FC_Outputs N1 → DQ10_SYS_PreStartSounder.
Evidence: `TON(PreStartTimer, IN := IO.Step = 10, PT := PreStartSounderTimeMS)` (N7); `COIL IO.PreStartSounder := IO.Step = 10 OR IO.PusherJogPreStartSounder` (N15); `PreStartSounderTime : Real RETAIN = 10.0` (DB_Settings); MUL/CONVERT pair 1↔1 in seq N2 (`MUL(... DB_Settings.PreStartSounderTime, IN2 := 1000.0)` → `CONVERT ... => PreStartSounderTimeMS`, index-matched per the C-126 documented idiom and the network comment stating the scheme).
Notes: Number chain verified end to end; siren step (10) precedes all sequencer-driven equipment (discharge at 20, shredder 30+, infeed 50). One exception outside the start sequence: the pusher auto-park can move equipment with no siren — reported at REQ-045.

### REQ-004 — Discharge conveyor first, confirmed: partial
Where: seq N15 (`RunDischargeConv := Step >= 20`) → FC_ControlMain N7 → DB_Output.Run_Discharge_Conv → FC_Outputs → DQ5_DIS_Run; confirm at seq N8; feedback DI10 → DB_Input.Discharge_Conv_Running → IO.DischargeConvRunning.
Evidence: `MOVE(EN := NOT StopCmd AND NOT DischargeStartTimer.Q AND IO.DischargeConvRunning AND IO.Step = 20, IN := 30) => IO.Step` (N8); `TON(DischargeStartTimer, IN := IO.Step = 20, PT := DischargeConveyorTimeoutMS)`; `DischargeConveyorTimeout : Real RETAIN` (DB_Settings — no start value).
Notes: Chain complete (first-to-start ✓, feedback-confirmed ✓, timeout-abort + latched fault ✓), but the confirm window is unconfigured (Q-02). Real consequence as-configured: PT = 0 ms makes `DischargeStartTimer.Q` fire the entry scan, so step 20 aborts to 0 immediately with `DischargeConveyorTimeoutFault` latched — the start sequence cannot pass step 20 even with REQ-012's polarity fixed.

### REQ-005 — Shredder reverse run at start: contradicted
Where: seq N14 → FC_ControlMain N3 → iDB_MotorFwdRevSystem_Shredder.IO.Reverse → FB_MotorFwdRevSystem N1/N2 → IO.RunRev → FC_ControlMain N7 → DB_Output.Run_Shredder_Rev → DQ2_SHR_RunRev; window at seq N9.
Evidence: `COIL IO.MotorReverseCmd := IO.Step = 30` (seq N14); `SCOIL Pasue := IO.Reverse AND NOT RisingEdgeFlags[0]` (FB N1 — rising edge of Reverse sets an 8 s pause); `COIL IO.RunRev := (...) AND NOT IO.StopMotor AND NOT IO.Shutdown AND NOT Pasue AND IO.Reverse` (FB N2); `TON(ReversalPauseTimer, IN := Pasue, PT := PauseTimeMS)` with `ReverseDelay : Real = 8.0` (iDB) feeding `PauseTimeMS` (FB N7 MUL/CONVERT index 3); `MOVE(EN := NOT StopCmd AND NOT IO.MotorFaultActive AND ReverseRunTimer.Q AND IO.Step = 30, IN := 40) => IO.Step` with `ShredderReverseRunTime : Real RETAIN = 6.0`.
Notes: Entering step 30 raises a rising edge on `IO.Reverse`, which sets `Pasue` for 8 s (ReverseDelay) — and `RunRev` is blocked by `NOT Pasue` for that whole window. The sequencer leaves step 30 on a fixed 6 s timer (no `ShredderRunRevFB` confirmation in the transition), i.e. before the 8 s pause ever releases. Net: DQ2 never energizes; the "reverse run" is a 6 s motor-off dwell. The commanded behavior is materially other than "starts in reverse and runs for 6 s". Statically provable from the two presets (6.0 < 8.0).

### REQ-006 — Spin-down pause after reverse: contradicted
Where: FB_MotorFwdRevSystem N1 (Pasue/ReversalPauseTimer), iDB start value; seq N9/N10.
Evidence: `ReverseDelay : Real = 8.0` (iDB — number present, Q-09's per-instance home confirmed as the wired tunable); `SCOIL Pasue := (IO.Reverse OR NegitiveSignalEdge[0]) AND NOT IO.Reverse` (falling-edge set, the intended post-reverse pause); rising-edge set as quoted at REQ-005.
Notes: The pause runs from step-30 entry (rising edge), concurrent with the would-be reverse run, not after it. Observable gap between the reverse phase's end (t=6 s, step 30→40) and the forward start (t=8 s, when `Pasue` releases and `CycleDelay` re-arms the rung) is 2 s, not 8 s — and there is no "stop after the reverse run" because the reverse run never occurs (REQ-005). The spec'd stop→8 s→forward sequence does not exist as built.

### REQ-007 — Shredder forward start: partial
Where: seq N14 (`MotorAutoStartCmd := Step >= 30 AND Step <= 50`) → FC_ControlMain N3 → IO.AutoStartSignal → FB N2 → IO.RunFwd → FC_ControlMain N7 → DB_Output.Run_Shredder_Fwd → DQ1_SHR_RunFwd; confirm at seq N10 (`IO.ShredderRunFwdFB` → 50).
Evidence: `COIL IO.RunFwd := (IO.TryRunMotor AND PreStartMemory AND IO.RecentStart OR ... OR IO.TryRunMotor AND CycleDelay) AND NOT IO.StopMotor AND NOT IO.Shutdown AND NOT Pasue AND NOT IO.Reverse` (FB N2); `FTTime : Real` (iDB — no start value); `TON(FaultTripTimer, IN := IO.RunFwd AND NOT IO.RunningFBFwd OR IO.RunRev AND NOT IO.RunningFBRev, PT := FTTimeMS)` and `COIL IO.FTR := FaultTripTimer.Q AND NOT ReversalIgnoreFTTimer.IN AND NOT ReversalIgnoreFTTimer.Q OR IO.FTR AND NOT IO.FaultReset` (FB N9).
Notes: Command chain complete and correctly wired. But the instance's failed-to-run window `IO.FTTime` is unconfigured (0.0 s) and `IO.ReverseIgnoreFT` likewise, so `FTR` latches one scan after `RunFwd` asserts unless running feedback arrives within a single scan — physically impossible for a soft-started motor. As commissioned, the forward start trips instantly to fault and step 40→0. Unconfigured per-instance settings on an otherwise-complete chain — NEW question N-1 (these members are outside Q-02's DB_Settings list).

### REQ-008 — Hopper-not-high gate before infeed: implemented
Where: seq N11.
Evidence: `TON(InfeedRestartTimer, IN := IO.Step = 50 AND NOT IO.HopperLevelHigh, PT := InfeedRestartDelayMS)`; `COIL IO.RunInfeedConv := InfeedRunning`.
Notes: Infeed cannot start (or stay running) unless hopper-not-high has held for the delay. Chain to DI8 verified (FC_Inputs N1 → DB_Input.Hopper_Level_High → FC_ControlMain N1).

### REQ-009 — Delay after hopper-clear confirm (5 s): implemented
Where: seq N2 pair 4↔4, N11.
Evidence: `MUL(EN := TRUE, IN1 := DB_Settings.InfeedRestartDelay, IN2 := 1000.0)` → `CONVERT ... => InfeedRestartDelayMS`; `InfeedRestartDelay : Real RETAIN = 5.0`.
Notes: Number matches; same setting serves REQ-015 as the register presumes.

### REQ-010 — Infeed conveyor start: implemented
Where: seq N11 → FC_ControlMain N7 (`DB_Output.Run_Infeed_Conv := iDB_ShredderSequencer.IO.RunInfeedConv`) → FC_Outputs N1 → DQ4_IFC_Run.
Evidence: `COIL InfeedRunning := InfeedRestartTimer.Q`; `COIL DQ4_IFC_Run := AlwaysTrue AND DB_Output.Run_Infeed_Conv AND NOT DB_Output.Test[0] OR ...`.
Notes: Full hop-by-hop chain verified. (Running feedback DI9 is not consumed anywhere — see REQ-063 and reverse pass.)

### REQ-011 — Upstream enable 3 s after infeed: implemented
Where: seq N11 → FC_ControlMain N7 → DB_Output.Enable_Upstream → FC_Outputs → DQ9_SYS_EnableUpstream.
Evidence: `TON(UpstreamEnableTimer, IN := InfeedRunning, PT := InfeedToUpstreamEnableDelayMS)`; `COIL IO.EnableUpstream := UpstreamEnableTimer.Q`; `InfeedToUpstreamEnableDelay : Real RETAIN = 3.0`.
Notes: 3 s per FuncDesc; Q-07 carried (corpus does 3 s with the separate volt-free output, the FuncDesc reading, not SpecSheet's 6 s). Timer keys on the infeed run command, not DI9 feedback — a reasonable reading of "after the infeed conveyor starts". N11's own comment declares the one-scan `InfeedRunning` read-lag; harmless at these presets.

### REQ-012 — Stop pushbutton stops all: contradicted
Where: DI4_SYS_CycleStop → FC_Inputs N1 → DB_Input.Cycle_Stop → FC_ControlMain N1 → IO.CycleStop → seq N1, exits in N7–N13.
Evidence: `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning` (seq N1); `COIL DB_Input.Cycle_Stop := AlwaysTrue AND NOT DB_Input.Test[0] AND DI4_SYS_CycleStop OR ...` (FC_Inputs — no inversion); tag comment `"Cycle stop pushbutton (NC)"`.
Notes: With the NC field device, the input is TRUE unpressed — so `StopCmd` is asserted permanently and *pressing stop releases it*. Exactly the polarity-holds-plant-stopped class. Secondary gap even after a polarity fix: the stop path never reaches the pusher subsystem — FB_PusherControl has no stop/healthy input, and the sequencer forces `PusherModeCmd` to 0 only at step 60 (`MOVE(EN := IO.Step <> 60, IN := IO.OperatorPusherMode) => IO.PusherModeCmd`), so a mid-stroke pusher cycle continues (power pack + solenoids) and new auto cycles can trigger after a stop. "Stops all equipment" is not met for the pusher.

### REQ-013 — Downstream loss stops all: partial
Where: seq N1 + every step's StopCmd exit (N7–N13).
Evidence: `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning`; e.g. `MOVE(EN := StopCmd AND IO.Step = 50, IN := 0) => IO.Step` (N12).
Notes: The downstream-loss term has correct polarity and aborts every step to idle, dropping discharge/infeed/upstream/motor commands within a scan or two. Missing part: the pusher subsystem is outside the stop-all path (same gap as REQ-012). Q-08 carried: corpus does not auto-restart on downstream recovery (a fresh `CycleStart` at step 0 is required) — that is what it does today; the option question stays open.

### REQ-014 — Hopper high stops infeed only: implemented
Where: seq N11.
Evidence: `TON(InfeedRestartTimer, IN := IO.Step = 50 AND NOT IO.HopperLevelHigh, ...)`; step stays 50 (no hopper term in N12's exits) so `RunDischargeConv := IO.Step >= 20` and motor commands are unaffected.
Notes: Infeed drops when the timer's IN falls; shredder keeps running — corroborates the register's SpecSheet note.

### REQ-015 — Infeed restart on hopper clear (+5 s): implemented
Where: seq N11 (same timer as REQ-009).
Evidence: as REQ-008/009; `InfeedRestartDelay ... = 5.0`.
Notes: One number, one setting, as the register presumed.

### REQ-016 — Upstream re-enable after infeed restart (3 s): implemented
Where: seq N11 (UpstreamEnableTimer restarts when `InfeedRunning` returns).
Evidence: as REQ-011.
Notes: Upstream also drops during the hopper-high hold (timer IN falls with InfeedRunning) — consistent behavior, not asked against.

### REQ-017 — Overcurrent event definition: disarmed
Where: seq N12; DB_AnalogInput → FC_ControlMain N1 → IO.ShredderMotorCurrent (dead end).
Evidence: `TON(OvercurrentMediumTimer, IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue, PT := OvercurrentMediumDelayMS)` and the identical `NOT AlwaysTrue` gate on OvercurrentHighTimer (seq N12); N12 comment: `"...see OvercurrentTripped's own comment for the AlwaysTrue placeholder gap this still has."`; `MOVE(EN := TRUE, IN := DB_AnalogInput.ShredderMotorCurrent) => iDB_ShredderSequencer.IO.ShredderMotorCurrent` (FC_ControlMain N1) — and grep shows `IO.ShredderMotorCurrent` is read nowhere in the FB.
Notes: `NOT AlwaysTrue` on the REQ's active path = disarmed by the vocabulary's letter. Additionally the current-vs-setpoint comparison is absent entirely: the placeholder stands where `current > setpoint` should be, `DB_AnalogInput.ShredderMotorCurrent` has no writer (no AI channel — Q-11, per DB_AnalogInput's own header), and both setpoints are read by nothing. "Overcurrent ≠ overload" distinction: see REQ-059/Q-10. The N12 comment names the gap; the member comment it points at ("OvercurrentTripped's own comment") does not exist in this corpus snapshot.

### REQ-018 — Machine-type selection in engineering: unimplemented
Where: nowhere.
Evidence: grep for `K75|K100|K150|MachineType|Machine_Type` across the corpus: zero hits.
Notes: Register marks it proposed; Q-04 carried. No logic references any proposed tag (no laundering).

### REQ-019 — Overcurrent setpoints per machine type: partial
Where: DB_Settings only.
Evidence: `OvercurrentSetpointMedium : Real RETAIN` / `OvercurrentSetpointHigh : Real RETAIN` (no start values); grep shows no reader for either anywhere in the corpus.
Notes: A single pair exists — unconfigured, unconsumed, with no per-machine structure. Missing: per-type sets (proposed, Q-04), values (Q-02), and any consumer at all (the detection that would read them is the REQ-017 placeholder).

### REQ-020 — Overcurrent on either motor: unimplemented
Where: nowhere.
Evidence: single current member (`DB_AnalogInput.ShredderMotorCurrent`), single fault input (`DI7_SYS_MotorFault`), single feedback pair (DI5/DI6) — no second-motor tags exist (tag table read in full).
Notes: Register marks per-motor detection proposed; Q-05 carried.

### REQ-021 — Medium overcurrent level: disarmed
Where: seq N12.
Evidence: `TON(OvercurrentMediumTimer, IN := ... AND NOT AlwaysTrue, PT := OvercurrentMediumDelayMS)`; `OvercurrentMediumDelay : Real RETAIN` (unconfigured).
Notes: Per-level delay timer exists but is placeholder-gated; the "a little above the setpoint" comparison does not exist. Q-02 (delay unconfigured) cross-cited.

### REQ-022 — High overcurrent level: disarmed
Where: seq N12.
Evidence: `TON(OvercurrentHighTimer, IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue, PT := OvercurrentHighDelayMS)`; `OvercurrentHighDelay : Real RETAIN` (unconfigured).
Notes: As REQ-021, for the high level.

### REQ-023 — Overcurrent stops the shredder: disarmed
Where: seq N12 (50→60), N14 (`MotorAutoStartCmd := Step >= 30 AND Step <= 50` drops at 60) → motor FB stop.
Evidence: `MOVE(EN := NOT StopCmd AND NOT IO.MotorFaultActive AND OvercurrentTripped AND IO.Step = 50, IN := 60) => IO.Step`; `COIL OvercurrentTripped := OvercurrentMediumTimer.Q OR OvercurrentHighTimer.Q`.
Notes: The stop path is built, but `OvercurrentTripped` can never assert — both feeding timers carry the `NOT AlwaysTrue` gate. Disarmed is not implemented.

### REQ-024 — Overcurrent stops the infeed: disarmed
Where: seq N11 (at step 60, `InfeedRestartTimer.IN` = FALSE → infeed drops).
Evidence: `TON(InfeedRestartTimer, IN := IO.Step = 50 AND ...)` — step 60 ≠ 50.
Notes: Built; unreachable behind the same placeholder gate.

### REQ-025 — Overcurrent parks the pusher: disarmed
Where: seq N14 → FC_ControlMain N5 (`MOVE ... IO.PusherModeCmd => iDB_PusherControl.IO.Mode`) → FB_PusherControl mode-0 behavior (N6/N7/N8 to step 30, retract).
Evidence: `MOVE(EN := IO.Step = 60, IN := 0) => IO.PusherModeCmd` (seq N14).
Notes: Built (mode forced to Off at step 60 parks the ram per REQ-037 logic); unreachable behind the placeholder gate. Side effect worth knowing: at step 60 the jog is also disabled (mode forced 0).

### REQ-026 — Post-overcurrent pause (4 s): disarmed
Where: seq N13.
Evidence: `TON(OvercurrentAbortPauseTimer, IN := IO.Step = 60, PT := ReversalRetryPauseTimeMS)`; `ReversalRetryPauseTime : Real RETAIN = 4.0` (number matches; MUL/CONVERT pair 9↔9 verified).
Notes: Built and correctly numbered; unreachable (step 60 unreachable).

### REQ-027 — Auto-resume via reverse run: disarmed
Where: seq N13 (60→30).
Evidence: `MOVE(EN := NOT StopCmd AND OvercurrentAbortPauseTimer.Q AND NOT IO.ShredderBlockedFault AND IO.Step = 60, IN := 30) => IO.Step`.
Notes: Built; unreachable. Additional defect if ever armed: every step-30 entry suffers REQ-005's pause interaction, so the "reverse to clear the jam" would never actually rotate the motor.

### REQ-028 — Reversal-count trip (5 in 3 min): disarmed
Where: seq N3/N4/N5, N13; annunciation at REQ-058.
Evidence: `COIL ReversalStepEdge := IO.Step = 60 AND NOT ReversalStepEdgeMem` / `ADD(EN := ReversalStepEdge, IN1 := 1, IN2 := ReversalCount) => ReversalCount` (N4); `TON(ReversalWindowTimer, IN := ReversalCount > 0, PT := ReversalWindowTimeMS)` (N3); `COIL IO.ShredderBlockedFault := ReversalCount >= DB_Settings.ReversalCountThreshold OR IO.ShredderBlockedFault AND NOT IO.FaultReset` (N5); `ReversalWindowTime ... = 180.0`, `ReversalCountThreshold : Int RETAIN = 5`; stop via N13's `...AND IO.ShredderBlockedFault AND IO.Step = 60, IN := 0` and N6's `NOT IO.ShredderBlockedFault` start gate.
Notes: Full chain built, threshold compared against the named member (not a literal), numbers match. Unreachable: `ReversalCount` can only increment on step-60 entries, and step 60 is behind the placeholder gate. Semantics note: the window is a re-arming 180 s window from the first event (count resets when the window elapses or on FaultReset), a defensible reading of "5 in 3 minutes" — the N4 comment points at a "ReversalCount's own comment" that does not exist in this snapshot. S9 candidate for semantics confirmation.

### REQ-029 — Spin-up overcurrent suppression (2–3 s): disarmed
Where: seq N12.
Evidence: `TON(SpinUpTimer, IN := IO.Step = 50, PT := OvercurrentSpinUpAllowanceMS)`; both OC timers require `SpinUpTimer.Q`; `OvercurrentSpinUpAllowance : Real RETAIN = 3.0` (within stated range).
Notes: The suppression structure is correct and armed, but the thing it suppresses can never act (same gate), so the REQ's behavior is moot as built.

### REQ-030 — Power pack before and during movement: implemented
Where: FB_PusherControl N12/N13 → FC_ControlMain N7 → DB_Output (Run_PowerPack, Pusher_Extend, Pusher_Retract) → FC_Outputs → DQ3/DQ6/DQ7.
Evidence: `COIL IO.Extend := ExtendDemand AND IO.PowerPackRunningFB` / `COIL IO.Retract := RetractDemand AND IO.PowerPackRunningFB` (N13); `COIL PumpDemand := ExtendDemand OR RetractDemand` (N12).
Notes: Solenoids are gated on the live running feedback (DI11 chain verified) — prior to and during, both satisfied; feedback loss mid-stroke drops the solenoid immediately.

### REQ-031 — Power pack run-on: partial
Where: FB_PusherControl N13.
Evidence: `TON(PumpRunOnTimer, IN := NOT PumpDemand, PT := PumpRunOnTimeMS)`; `COIL IO.RunPowerPack := PumpDemand OR PumpEverDemanded AND NOT PumpRunOnTimer.Q`; `PusherPumpRunOnTime : Real RETAIN` (unconfigured).
Notes: Correct TON+inversion off-delay construction (with the commented cold-start guard). Unconfigured preset (Q-02) means PT = 0: the run-on is zero-length, and during the Hold step (step 20, ~2 s, where neither demand is active) the pump stops — precisely the push→retract transition the REQ exists to protect. Structure right, number missing.

### REQ-032 — Pusher cycle definition: partial
Where: FB_PusherControl N6 (0→10), N7 (10→20 on FullTravelLimit), N8 (20→30 after hold), N9 (30→0 on HomeLimit).
Evidence: `TON(HoldTimer, IN := IO.Step = 20, PT := EndTravelHoldTimeMS)` with `PusherEndTravelHoldTime : Real RETAIN = 2.0` (matches "approx. 2 s"; MUL/CONVERT pair 3↔3 verified); `MOVE(EN := IO.Step = 10 AND IO.Mode <> 0 AND NOT EndTravelTimer.Q AND NOT IO.Blocked AND IO.FullTravelLimit, IN := 20) => IO.Step` (N7); `MOVE(EN := IO.Step = 30 AND IO.HomeLimit, IN := 0) => IO.Step` (N9).
Notes: Cycle shape and hold number are right. Unconfigured `PusherEndTravelTimeout`/`PusherParkedTimeout` (Q-02) make the cycle impossible as commissioned: at step-10 entry `EndTravelTimer` (PT=0) fires the same scan, so the sequence runs 0→10→30→0 in one scan with both timeout faults latched and no motion. Cross-cites REQ-054/055.

### REQ-033 — Three pusher modes: implemented
Where: DB_Controls.PusherMode → FC_ControlMain N1 (`=> IO.OperatorPusherMode`) → seq N14 (`PusherModeCmd`) → FC_ControlMain N5 (`=> iDB_PusherControl.IO.Mode`) → FB_PusherControl.
Evidence: FB header comment: `"Mode legend: 0=Off (finish a retract already in progress, then park; no new cycle starts) - 1=Auto (hopper-high-level starts a cycle) - 2=Manual (operator button/jog starts a cycle)."`
Notes: Three distinct behaviors verified in logic (N1/N6/N7/N8). Q-15 carried: hand/jog is folded into Manual (mode 2), not a fourth selection — that is what the corpus does today; the WHAT-level ruling stays open.

### REQ-034 — Auto mode, cycle on hopper high: implemented
Where: FB_PusherControl N6.
Evidence: `MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked, IN := 10) => IO.Step`.
Notes: Trigger chain to DI8 verified. Consequence note: as commissioned the triggered cycle instantly aborts (REQ-032/054 defaults), so the hopper is never actually relieved until the timeouts are configured.

### REQ-035 — Manual mode, HMI cycle button: implemented
Where: DB_Controls.PusherManualCycleCmd → FC_ControlMain N5 → IO.ManualCycleCmd → FB N6 (same MOVE as REQ-034).
Evidence: `COIL iDB_PusherControl.IO.ManualCycleCmd := DB_Controls.PusherManualCycleCmd` (FC_ControlMain N5).
Notes: Level-triggered (holding the button at home re-triggers); the source doesn't specify edge semantics.

### REQ-036 — Manual mode, remote pushbutton: unimplemented
Where: nowhere in logic.
Evidence: `COIL DB_Input.Pusher_Local_Remote := ... DI16_PSH_LocalRemote ...` (FC_Inputs N2) — and grep shows `DB_Input.Pusher_Local_Remote` has no reader anywhere.
Notes: The remote cycle button has no digital input (register: proposed; Q-03), and the existing selector input is mapped into the buffer then read by nothing. No logic references a proposed tag (no laundering).

### REQ-037 — Off mode, retract to home: implemented
Where: FB_PusherControl N6 (0→30 when off home), N7 (`Mode = 0` → 30), N8 (`Mode = 0` → 30), N9/N11 (retract to HomeLimit).
Evidence: `MOVE(EN := IO.Step = 10 AND IO.Mode = 0, IN := 30) => IO.Step` (N7); `COIL RetractDemand := IO.Step = 30 OR ...` (N11).
Notes: Off mode parks from every state. (The same ungated mechanism is REQ-043's defect when `Fitted` is FALSE.)

### REQ-038 — Hand control, jog per direction: implemented
Where: DB_Controls.PusherJogExtendCmd/PusherJogRetractCmd → FC_ControlMain N5 → FB N10/N11.
Evidence: `COIL ExtendDemand := IO.Step = 10 AND NOT PressureHold OR IO.Step = 0 AND IO.Mode = 2 AND IO.JogExtendCmd AND JogPreStartTimer.Q` and the Retract twin (N11).
Notes: One button per direction, available at Step 0 in mode 2. Q-15 carried (hand-as-overlay-on-Manual is the corpus's answer; owner ruling open).

### REQ-039 — Jog is hold-to-move: contradicted
Where: FB_PusherControl N11 (hold-to-move) vs N6 (release behavior).
Evidence: `MOVE(EN := NOT ((IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Mode = 2) AND IO.Step = 0 AND NOT IO.HomeLimit, IN := 30) => IO.Step` (N6); N6 comment: `"...Suppressed during an active jog command so jogging isn't fought by an auto-retract."`
Notes: Hold-to-move is correct (demand requires the live button). But "on release it stops where it is" is violated: the instant both jog buttons release with the ram off home, the auto-park MOVE fires and the pusher self-retracts to home (pump + retract solenoid, unbidden). Materially other than the REQ text; the network comment shows it is deliberate — engineer ruling needed (NEW question N-4).

### REQ-040 — Jog independent of shredder: implemented
Where: FB_PusherControl N10/N11.
Evidence: no shredder/sequencer term exists in any pusher network; mode passes through the sequencer unchanged except at step 60 (`MOVE(EN := IO.Step <> 60, IN := IO.OperatorPusherMode) => IO.PusherModeCmd`).
Notes: Jog works with the shredder stopped or running; only the (unreachable) overcurrent-abort step suppresses it.

### REQ-041 — Jog pre-start warning (1 s + during transition): implemented
Where: FB N10 → IO.JogPreStartSounder → FC_ControlMain N1 → seq IO.PusherJogPreStartSounder → seq N15 → DB_Output.PreStart_Sounder → DQ10.
Evidence: `TON(JogPreStartTimer, IN := (IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Step = 0 AND IO.Mode = 2, PT := JogWarningTimeMS)`; demand terms carry `AND JogPreStartTimer.Q` (N11); `COIL IO.PreStartSounder := IO.Step = 10 OR IO.PusherJogPreStartSounder` (seq N15); `PusherJogWarningTime : Real RETAIN = 1.0`.
Notes: 1 s lead verified (movement armed only after Q); sounder continues while buttons are held, i.e. for the whole jog motion. Full chain to the physical sounder verified.

### REQ-042 — Engineering pusher enable/disable: implemented
Where: DB_Settings.PusherFitted → FC_ControlMain N5 → IO.Fitted → FB N1.
Evidence: `COIL IO.Enable := IO.Fitted AND IO.Mode <> 0` (N1); `PusherFitted : Bool RETAIN = TRUE`.
Notes: The selection exists and gates cycle starts (N6's launch MOVE requires `IO.Enable`). Its incompleteness as a disable is REQ-043's finding.

### REQ-043 — Disabled pusher: no outputs, no feedback supervision: contradicted
Where: FB_PusherControl N6 (first MOVE), N11, N13, N9.
Evidence: `MOVE(EN := NOT ((IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Mode = 2) AND IO.Step = 0 AND NOT IO.HomeLimit, IN := 30) => IO.Step` (N6 — no `Fitted`/`Enable` term); `COIL RetractDemand := IO.Step = 30 OR ...` (N11 — unconditional on Fitted); `COIL IO.RunPowerPack := PumpDemand OR ...` (N13); `TON(ParkedTimer, IN := IO.Step = 30, PT := ParkedTimeoutMS)` (N9 — switch-feedback supervision).
Notes: On the exact machine the REQ describes (no pusher installed → `HomeLimit` input FALSE, `PusherFitted` set FALSE), the auto-park transition fires, step locks at 30, `RunPowerPack` drives DQ3 continuously, and the block supervises the (nonexistent) home switch. The system does attempt to run the power pack and does look for switch feedback while disabled — the opposite of the REQ.

### REQ-044 — Disabled pusher: no pusher faults: contradicted
Where: FB_PusherControl N9 → FC_AlarmsMain N7.
Evidence: `COIL IO.ParkedTimeoutFault := ParkedTimer.Q OR IO.ParkedTimeoutFault AND NOT IO.FaultReset` (N9); `COIL DB_Alarms.ShredderAlarm0.%X6 := iDB_PusherControl.IO.ParkedTimeoutFault` (FC_AlarmsMain N7 — no Fitted suppression).
Notes: In the REQ-043 scenario the ParkedTimeoutFault latches (instantly, with the unconfigured PT) and is annunciated on the display while the pusher is disabled. No fault in FC_AlarmsMain N4–N7 is suppressed by `Fitted`.

### REQ-045 — Park after pre-start: partial
Where: FB_PusherControl N6 (auto-park) — no pre-start linkage exists.
Evidence: N6 first MOVE (quoted at REQ-043); UDT_PusherIO/FB interface contains no pre-start-done member (full member list read); seq N14's `MotorPreStartDoneCmd := IO.Step >= 20` is wired only to the motor FB (`FC_ControlMain N3: COIL iDB_MotorFwdRevSystem_Shredder.IO.PreStartDone := iDB_ShredderSequencer.IO.MotorPreStartDoneCmd`).
Notes: A transition-to-parked exists but is not sequenced on pre-start completion — it fires at any idle moment, including with the plant stopped and no siren at all (unwarned ram + pump motion; contrast the 1 s warning the jog got). The REQ's stated "when pre-start is complete" trigger is not implemented; the parked outcome is reached incidentally. NEW question N-4.

### REQ-046 — High-pressure hold while pushing (>0.5 s): implemented
Where: FB_PusherControl N4, N11.
Evidence: `TON(PressureConfirmTimer, IN := IO.Step = 10 AND IO.HighPressure, PT := PressureTripConfirmTimeMS)`; `COIL PressureHold := PressureConfirmTimer.Q OR PressureHold AND NOT PressureClearTimer.Q`; `COIL ExtendDemand := IO.Step = 10 AND NOT PressureHold OR ...`; `PressureTripConfirmTime : Real RETAIN = 0.5` (matches; pair 0↔0 verified).
Notes: Stop-pushing-and-hold verified: extend drops, no retract at step 10, step unchanged (a C-123-style hold, per the N11 comment).

### REQ-047 — Resume push after pressure clears (+5 s): partial
Where: FB_PusherControl N4.
Evidence: `TON(PressureClearTimer, IN := PressureHold AND NOT IO.HighPressure, PT := PressureClearResumeDelayMS)`; `PressureClearResumeDelay : Real RETAIN` (no start value despite the spec-stated 5 s).
Notes: Resume-from-existing-position is correct (step stays 10; ExtendDemand returns when the hold releases). The delay is the register's singled-out unconfigured-with-a-stated-number case (Q-02): as built the resume is immediate (PT=0), not 5 s.

### REQ-048 — Pressure-trip count, return and fault: implemented
Where: FB_PusherControl N4 (count), N5 (fault), N7 (return).
Evidence: `ADD(EN := PressureConfirmEdge, IN1 := 1, IN2 := PressureTripCount) => PressureTripCount` with constructed edge (N4); `COIL IO.Blocked := PressureTripCount >= IO.PressureTripCountThreshold OR IO.Blocked AND NOT IO.FaultReset` (N5); `MOVE(EN := IO.Step = 10 AND IO.Mode <> 0 AND NOT EndTravelTimer.Q AND IO.Blocked, IN := 30) => IO.Step` (N7); `PressureTripCountThreshold : Int RETAIN = 5` (matches, named member not a literal); count resets on the cycle-launch condition (N4's MOVE mirrors N6's launch terms) — per-cycle counting as specified.
Notes: Full chain: 5 confirmed trips in one cycle → Blocked → retract to parked. Annunciation at REQ-053.

### REQ-049 — Blocked fault spares the shredder: implemented
Where: absence verified corpus-wide.
Evidence: grep — `IO.Blocked` is consumed only inside FB_PusherControl (N4/N6/N7) and by `FC_AlarmsMain N5: COIL DB_Alarms.ShredderAlarm0.%X4 := iDB_PusherControl.IO.Blocked`; the sequencer takes no pusher-fault input (UDT_ShredderSequencerIO member list).
Notes: The shredder sequence is structurally unaffected by a pusher-blocked fault.

### REQ-050 — Blocked lockout until reset: implemented
Where: FB_PusherControl N5, N6.
Evidence: launch MOVE requires `AND NOT IO.Blocked` (N6); `IO.Blocked ... AND NOT IO.FaultReset` latch (N5); `DB_Controls.FaultReset` chain via FC_ControlMain N5.
Notes: New cycles are locked out until reset. Note: the jog paths (N11) are not gated on `Blocked` — jogging remains possible while blocked, plausibly intentional for jam-freeing but unstated (NEW question N-5).

### REQ-051 — Pressure ignored on retract: implemented
Where: FB_PusherControl N4, N11.
Evidence: `TON(PressureConfirmTimer, IN := IO.Step = 10 AND IO.HighPressure, ...)` — pressure is sampled only during the push step; `COIL RetractDemand := IO.Step = 30 OR ...` carries no pressure term.
Notes: Retract stroke (step 30) and jog-retract ignore the switch entirely.

### REQ-052 — Fault: both pusher switches: implemented
Where: FB_PusherControl N2 → FC_AlarmsMain N4.
Evidence: `COIL IO.BothSwitchesFault := IO.HomeLimit AND IO.FullTravelLimit OR IO.BothSwitchesFault AND NOT IO.FaultReset`; `COIL DB_Alarms.ShredderAlarm0.%X3 := iDB_PusherControl.IO.BothSwitchesFault`.
Notes: Cause + display annunciation both present. (The fault gates no motion — annunciation-only; the REQ asks only for annunciation. Text/severity are the alarm-design tier's business.)

### REQ-053 — Fault: pusher blocked: implemented
Where: FB_PusherControl N5 → FC_AlarmsMain N5 (X4).
Evidence: quoted at REQ-048/049.
Notes: Cause behavior live (REQ-048), annunciated.

### REQ-054 — Fault: end-travel timeout: partial
Where: FB_PusherControl N7 → FC_AlarmsMain N6 (X5).
Evidence: `TON(EndTravelTimer, IN := IO.Step = 10, PT := EndTravelTimeoutMS)`; `COIL IO.EndTravelTimeoutFault := EndTravelTimer.Q OR ... AND NOT IO.FaultReset`; `COIL DB_Alarms.ShredderAlarm0.%X5 := iDB_PusherControl.IO.EndTravelTimeoutFault`; `PusherEndTravelTimeout : Real RETAIN` (unconfigured).
Notes: Chain complete; window unconfigured (Q-02). As commissioned (PT=0) every commanded stroke faults the entry scan — see REQ-032's one-scan-churn consequence.

### REQ-055 — Fault: parked timeout: partial
Where: FB_PusherControl N9 → FC_AlarmsMain N7 (X6).
Evidence: quoted at REQ-044; `PusherParkedTimeout : Real RETAIN` (unconfigured); N9 comment records the alarm-only design (no forced transition out of Retract).
Notes: Both faults required by the register are present (end + parked). Window unconfigured (Q-02): every retract instantly raises the fault as commissioned; also fires in REQ-043/044's disabled scenario.

### REQ-056 — Fault: motor tripped input: implemented
Where: DI7_SYS_MotorFault → FC_Inputs N1 → DB_Input.Motor_Fault → FC_AlarmsMain N1 (X0).
Evidence: `COIL DB_Alarms.ShredderAlarm0.%X0 := DB_Input.Motor_Fault` under title `"Shredder Motor - Fault/Overload Tripped - Check Soft Starter"`.
Notes: Annunciated. The same bit is also the only overload annunciation — see REQ-059/Q-10.

### REQ-057 — Fault: motor failed to run, identified: partial
Where: FB_MotorFwdRevSystem N9 → FC_AlarmsMain N2 (X1).
Evidence: `COIL DB_Alarms.ShredderAlarm0.%X1 := iDB_MotorFwdRevSystem_Shredder.IO.FTR`; FTR logic quoted at REQ-007.
Notes: Failed-to-run annunciation exists for the shredder; discharge conveyor gets its own identifying bit (X8). "Identify which motor" cannot be met beyond that: one instrumented motor system (Q-05), and the infeed conveyor has no failed-to-run detection at all (its feedback is read by nothing — see REQ-063). Window unconfigured (NEW question N-1).

### REQ-058 — Fault: shredder blocked: disarmed
Where: seq N5 → FC_AlarmsMain N8 (X7).
Evidence: `COIL DB_Alarms.ShredderAlarm0.%X7 := iDB_ShredderSequencer.IO.ShredderBlockedFault`.
Notes: The bit is wired, but its only cause (`ReversalCount` reaching threshold) can never occur while step 60 sits behind the `NOT AlwaysTrue` gate (REQ-028). The annunciation can never fire — disarmed, not implemented.

### REQ-059 — Fault: overload: partial
Where: FC_AlarmsMain N1 (shared X0).
Evidence: network title `"Shredder Motor - Fault/Overload Tripped - Check Soft Starter"` over the single `DB_Input.Motor_Fault` bit.
Notes: No distinct overload annunciation exists or is distinguishable on this hardware (single soft-start fault relay input) — Q-10 carried; the corpus's answer today is one merged trip/overload alarm.

### REQ-060 — Reversal counter display: partial
Where: iDB_ShredderSequencer (`ReversalCount : Int`, bare Static outside the IO UDT).
Evidence: `ReversalCount : Int` (iDB line 50); maintained by seq N4 (quoted at REQ-028).
Notes: A bindable count value exists (though as a bare Static, not an interface-UDT member as the register's note described). It can also never move while REQ-028 is disarmed. The outside-panel display and flashing motor-fault lamp have no output tags (proposed if PLC-driven) — Q-13 carried.

### REQ-061 — E-stop stops everything (hardwired): out-of-scope
Where: confirmed by absence.
Evidence: `DI1_SYS_ControlHealthy` is consumed only as `COIL iDB_MotorFwdRevSystem_Shredder.IO.SystemHealthy := DB_Input.Control_Healthy` (FC_ControlMain N3); no PLC logic implements or references the stop function or any safety internals.
Notes: No PLC logic pretends to own it; hard rule 2 untouched (no F-/safety content anywhere in the corpus).

### REQ-062 — No auto-restart after E-stop recovery: contradicted
Where: seq N12 (step-50 exits), FC_ControlMain N3, FB_MotorFwdRevSystem N2/N6.
Evidence: step 50's only exits are `StopCmd`, `IO.MotorFaultActive`, `OvercurrentTripped` (seq N12 — no healthy/E-stop term; UDT_ShredderSequencerIO has no healthy member); `COIL iDB_MotorFwdRevSystem_Shredder.IO.RecentStart := AlwaysTrue` (FC_ControlMain N3 — the FB's own fresh-start-action memory strapped permanently true); `COIL PreStartMemory := IO.PreStartDone OR ...` (FB N6) with `PreStartDone := IO.MotorPreStartDoneCmd` = `IO.Step >= 20` — still true at step 50.
Notes: During an E-stop, `SystemHealthy` drops and the motor command drops, but the sequencer holds step 50 (nothing exits it), infeed/discharge/upstream stay commanded, and on E-stop reset `TryRunMotor` re-arms and the motor restarts immediately — no fresh start press required. The required behavior (fresh start command, start-up procedure runs) does not exist. Caveat: as commissioned, the FTTime=0 instant-FTR defect (REQ-007) would coincidentally fault the restart — a defect masking a defect, not the REQ's mechanism. Q-14 note: no control-on step exists; corpus's answer today is none.

### REQ-063 — Motor failed to run stops the system: partial
Where: shredder — FB N9/N11 → FC_ControlMain N1 (`IO.MotorFaultActive := iDB_MotorFwdRevSystem_Shredder.IO.FaultActive`) → seq N9/N10/N12 exits to 0; discharge — seq N8 timeout abort + X8.
Evidence: `COIL IO.FaultActive := IO.FTR OR IO.FTS OR IO.FaultFB OR IO.FaultActive AND NOT IO.FaultReset` (FB N11); `MOVE(EN := NOT StopCmd AND IO.MotorFaultActive AND IO.Step = 50, IN := 0) => IO.Step` (seq N12); `COIL DB_Alarms.ShredderAlarm0.%X8 := iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` (FC_AlarmsMain N9).
Notes: "Any motor": shredder covered (window unconfigured — N-1), discharge covered (window unconfigured — Q-02), infeed NOT covered (`DB_Input.Infeed_Conv_Running` written by FC_Inputs and read by nothing — no supervision exists), power pack NOT covered (feedback only gates solenoids; no failed-to-run timeout or fault). Two of four motors unsupervised.

### REQ-064 — HMI shows selected machine type: unimplemented
Where: nowhere.
Evidence: machine-type grep zero hits (REQ-018).
Notes: Depends on REQ-018's proposed selection; Q-04 carried.

### REQ-065 — Overall hour clock: partial
Where: FB_MotorFwdRevSystem N13 → iDB_MotorFwdRevSystem_Shredder.IO.HrsRun (imported-real).
Evidence: `TONR(HrTotaliserTimer, IN := IO.RunFwd AND IO.RunningFBFwd OR IO.RunRev AND IO.RunningFBRev, PT := T#1H, R := HrTotaliserTimerReset)`; `ADD(EN := HrTotaliserTimer.Q AND NOT RisingEdgeFlags[2] AND IO.HrsRun < 4294967295, IN1 := 1, IN2 := IO.HrsRun) => IO.HrsRun`.
Notes: A running-hours value accumulates (whole hours of shredder running) and is HMI-reachable — the register's binding candidate, satisfied by the imported block (traced read-only). The HMI screen itself is outside this corpus; partial on that basis.

### REQ-066 — Zeroable hour clock: unimplemented
Where: nowhere.
Evidence: grep for zeroable/reset-hours members: zero hits; `HrTotaliserTimerReset` is internal hour-pulse plumbing only (self-reset each elapsed hour), not an operator zeroing path.
Notes: Q-12 carried; register marks proposed.

### REQ-067 — Changeover selection: unimplemented
Where: nowhere.
Evidence: as REQ-018 (grep zero).
Notes: Q-04 carried (two vs three types also unresolved).

### REQ-068 — Shredder state indications: partial
Where: values — DB_Input.Shredder_Run_Fwd_FB / Shredder_Run_Rev_FB (written by FC_Inputs N1; fwd also on seq IO), jammed — iDB_ShredderSequencer.IO.ShredderBlockedFault / DB_Alarms X7.
Evidence: `COIL DB_Input.Shredder_Run_Rev_FB := ... DI6_SHR_RunRevFB ...` (FC_Inputs N1); X7 quoted at REQ-058.
Notes: All three state values exist for binding; HMI screens out of corpus (register: binding confirmed at HMI stage). Jammed can never assert while REQ-028 is disarmed; and as built the plant never runs in reverse (REQ-005), so the running-in-reverse indication would never show during an auto sequence.

### REQ-069 — In-cycle lamp: unimplemented
Where: FC_ControlMain N7 → DB_Output.In_Cycle → FC_Outputs N1 → DQ8_SYS_InCycle — fed from a member nothing writes.
Evidence: `COIL DB_Output.In_Cycle := iDB_ShredderSequencer.IO.InCycle` (FC_ControlMain N7); `COIL DQ8_SYS_InCycle := AlwaysTrue AND DB_Output.In_Cycle AND NOT DB_Output.Test[0] OR ...` (FC_Outputs N1); corpus-wide grep for `InCycle`: declarations (UDT/FB/iDB), the two consuming COILs, the tag — and **no writer anywhere**. None of FB_ShredderSequencer's 15 networks writes `IO.InCycle`.
Notes: Consumed-but-never-written — DQ8 is permanently FALSE; the lamp can never light. This is the exact failure class the skill was created for. Beyond the broken hop, the ruling's substance is also unbuilt: the lamp must OR five *field feedbacks*, but the sequencer receives only two of them (`ShredderRunFwdFB`, `DischargeConvRunning`); `Shredder_Run_Rev_FB` goes only to the motor FB, `Pusher_PowerPack_Running` only to the pusher FB, and `Infeed_Conv_Running` is read by nothing at all. Also: the register cites the owner ruling as "recorded in the corpus at FC_ControlMain network 7's comment" — in this snapshot network 7 has only the title `"Output Mapping"` and no comment; the recorded ruling text is absent. Paradigm note: classified combinational (pure OR) in the register's C-113 table; no combinational OR exists anywhere — mismatch by absence.

## Unimplemented / disarmed / contradicted REQs

Contradicted (7):
- REQ-012 — stop polarity reversed against the NC field device: `StopCmd := IO.CycleStop ...` holds the plant permanently stopped; pressing stop releases it. Also: stop-all never reaches the pusher.
- REQ-005 — shredder never runs in reverse: the motor FB's 8 s entry-pause (`ReverseDelay = 8.0`) outlasts the sequencer's 6 s step-30 window; DQ2 can never energize.
- REQ-006 — the 8 s spin-down pause runs concurrently with (not after) the reverse phase; effective post-phase gap is 2 s, and there is no reverse run to spin down from.
- REQ-039 — on jog release the pusher does not stop where it is; the ungated auto-park transition immediately self-retracts it to home.
- REQ-043 — with the pusher disabled and no hardware fitted, the block drives the power pack (step locks at 30, `RunPowerPack` on) and supervises switch feedback.
- REQ-044 — in the same disabled scenario `ParkedTimeoutFault` latches and is annunciated (X6); no alarm is suppressed by `Fitted`.
- REQ-062 — the plant auto-restarts on E-stop recovery: step 50 has no healthy-loss exit, and `RecentStart := AlwaysTrue` straps away the motor FB's fresh-start-action requirement.

Unimplemented (8):
- REQ-001 — start can never fire (permanently blocked by REQ-012's polarity term).
- REQ-018, REQ-064, REQ-067 — no machine-type selection/display/changeover exists anywhere (Q-04).
- REQ-020 — no per-motor overcurrent detection; one instrumented motor system (Q-05).
- REQ-036 — no remote pusher cycle input; the local/remote selector is mapped then read by nothing (Q-03).
- REQ-066 — no zeroable hours value or reset (Q-12).
- REQ-069 — in-cycle lamp: `IO.InCycle` consumed into DQ8 but written nowhere; three of five ruled feedback sources not even wired toward it; the ruling's recorded comment absent from this snapshot.

Disarmed (11): REQ-017, REQ-021, REQ-022, REQ-023, REQ-024, REQ-025, REQ-026, REQ-027, REQ-028, REQ-029, REQ-058 — the entire overcurrent feature group sits behind two `NOT AlwaysTrue` gates on the OvercurrentMedium/High timers (seq N12). The gate is on every one of these REQs' active path (step 60 and everything keyed to it is unreachable). Additionally the current-vs-setpoint comparison itself does not exist: `IO.ShredderMotorCurrent` and both `OvercurrentSetpoint*` members are read by nothing (Q-11). Numbers that do exist along the disarmed chain check out (4.0 pause, 180.0 window, threshold 5, spin-up 3.0).

Partial (16, one line each):
- REQ-004 — discharge confirm window unconfigured (Q-02); PT=0 aborts step 20 on entry: the sequence cannot pass it as commissioned.
- REQ-007 — motor `FTTime` unconfigured (N-1); instant FTR means no real motor start survives.
- REQ-013 — downstream-loss stops sequencer equipment + motor, but not the pusher subsystem.
- REQ-019 — one unconfigured, unconsumed setpoint pair; no per-machine sets (Q-02/Q-04).
- REQ-031 — run-on structure right, preset unconfigured (Q-02): pump stops during the 2 s hold, the exact transition the REQ protects.
- REQ-032 — cycle shape right; unconfigured stroke timeouts (Q-02) collapse a commanded cycle into a one-scan 0→10→30→0 churn with both faults latched.
- REQ-045 — park-to-home exists but is not sequenced on pre-start completion; fires unwarned at any idle moment.
- REQ-047 — resume delay unconfigured despite the spec's 5 s (Q-02's singled-out case); resumes immediately.
- REQ-054, REQ-055 — both timeout faults wired to display, both windows unconfigured (Q-02); they fire instantly as commissioned.
- REQ-057 — FTR annunciated, but "which motor" limited to shredder/discharge; infeed has no detection at all (Q-05).
- REQ-059 — overload not distinguishable; merged into X0 with the tripped-input fault (Q-10).
- REQ-060 — count value exists (bare Static, and frozen while REQ-028 is disarmed); no outside-panel display/lamp tags (Q-13).
- REQ-063 — shredder + discharge supervised (windows unconfigured), infeed and power pack not supervised.
- REQ-065 — HrsRun accumulates (imported-real logic); HMI binding out of corpus.
- REQ-068 — state values exist; jammed/reverse indications can never show under current defects; HMI binding out of corpus.

## Unrequested logic (reverse pass)

- Fail-to-stop (FTS) supervision annunciated as alarm X2 — FC_AlarmsMain N3 (generated; the cause logic is imported-real FB N10) — no REQ covers failed-to-stop. Header justification absent (FC_AlarmsMain has no header comment) [C-606].
- Soft-start fault-reset output drive: `COIL DB_Output.Motor_Fault_Reset := DB_Controls.FaultReset` → DQ11_SHR_FaultReset — FC_ControlMain N7 — generated — no REQ asks the PLC to pulse the soft-start reset (Q-14 adjacent). Header justification absent (FC_ControlMain has no header comment) [C-606].
- Pusher interface member `IO.Cycling` (`COIL IO.Cycling := IO.Step <> 0`, FB_PusherControl N13) — generated — written, consumed by nothing; not covered by the block's header comment [C-606]; also a dead interface member (see next section).
- Imported-real FB_MotorFwdRevSystem features unused by this project (documented context, not fix demands): hand-mode apparatus (`InHand`/`HandStartSignal`/`HandIntervention`/`HandReverse` — all strapped or inert), `Shutdown`/`StopMotor`/`ShutdownComplete`/`ShutdownTime` (never commanded), `UPSEnable`/`EnableUPSTime` (computed, consumed by nothing — correctly bypassed per seq N11's comment), `Telemetry`, per-instance `Alarm` word (FC_AlarmsMain reads the members directly instead), `Name` (never set). `HrsRun` is used (REQ-065).
- Constant wirings in FC_ControlMain N3 (generated): `IO.InHand := NOT AlwaysTrue`, `IO.RecentStart := AlwaysTrue`, `IO.InhibitMotor := NOT AlwaysTrue`, `IO.HandReverse := NOT AlwaysTrue` — none disarms a REQ's active path (three are permissives-off for unrequested hand/inhibit features; the RecentStart strap enables auto start but is load-bearing in REQ-062's contradiction). Network has no comment naming these as design vs debt — route note: C-604 to review-conventions/simplicity.
- Spare rails: SpareDI[1..7]/SpareDQ[1..7] buffer members and spare tag mapping — pattern shape for panel spares; register inventories them as spares. Not gold-plating.
- Tag-table legacy residue (Tag_1..Tag_54, AirStar comms words, Clock_0.5Hz, FirstScan) — no logic references; register records them as sandbox residue. Context only.
- Sequence-paradigm check (register C-113 table): shredder plant cycle → stepped FB with `Step : Int` in the interface UDT (matches, C-118 ✓); pusher cycle → stepped FB (matches); IO mapping & fault annunciation → combinational FCs (matches); in-cycle lamp → classified combinational, but no lamp logic exists at all (mismatch by absence — REQ-069).
- Tag-status check (anti-laundering): no logic references any register-`proposed` tag; every tag referenced by logic exists in DefaultTagTable or the DBs. No pipeline breach.

## Suspected functional defects outside any REQ

- Dead chain, consumed-never-written: `iDB_ShredderSequencer.IO.InCycle` → DQ8 (REQ-069's break; listed here as the corpus's one member feeding a physical output with no writer).
- Dead chain, both directions: `DB_AnalogInput.ShredderMotorCurrent` (no writer — no AI map) → `IO.ShredderMotorCurrent` (no reader). Q-11.
- Written-never-consumed: `DB_Input.Infeed_Conv_Running` (FC_Inputs N1 writes it; nothing reads it) — silently removes infeed supervision (REQ-063) and an in-cycle source (REQ-069).
- Written-never-consumed: `DB_Input.Pusher_Local_Remote` (FC_Inputs N2 writes; nothing reads) — REQ-036/Q-03.
- Dead settings: `DB_Settings.OvercurrentSetpointMedium`/`OvercurrentSetpointHigh` — no reader anywhere.
- Dead interface member: `iDB_PusherControl.IO.Cycling` — written, never read.
- Double writer on `IO.RecentStart`: FC_ControlMain N3 (`:= AlwaysTrue`) and FB_MotorFwdRevSystem N4 (`COIL IO.RecentStart := IO.RecentStart AND NOT IO.RunFwd AND NOT IO.RunRev AND NOT IO.FaultActive`) — the caller's strap wins at every point of use, deliberately defeating the FB's press-to-start semantics (load-bearing in REQ-062).
- Unconfigured per-instance timing members on the motor instance: `FTTime`, `ReverseIgnoreFT`, `EnableUPSTime`, `ShutdownTime` (iDB shows no start values) — `FTTime = 0.0` is the plant-stopping one (instant FTR; REQ-007/063); the others are inert as wired.
- Imported-real latent oddities in FB_MotorFwdRevSystem (documented context; inert while `InHand` is strapped FALSE): N5 writes `HandPosEdge` with two COILs in one network (second overwrites first each scan) while `HandNegEdge` is declared and never written, and N6's hold term contains the duplicated `AND NOT HandPosEdge AND NOT HandPosEdge`; N1's `SCOIL StartTimer` sets a TEMP variable that nothing reads.
- Unwarned motion: the pusher auto-park (FB_PusherControl N6 first MOVE) can start the power pack and move the ram at any idle moment with no pre-start warning of any kind — contrast REQ-003/REQ-041's warned motion everywhere else (ties to REQ-039/043/045; NEW N-4).
- No OB100 / startup normalization exists in the corpus, and the sequencer/pusher IO structs are RETAIN: `Step` survives a power cycle while internals (Pasue, timers, PumpEverDemanded, edge memories) do not — a mid-run power cycle resumes at the retained step with cleared internals (Q-01's territory; see S9). Route notes (other tiers): C-604 uncommented constants (FC_ControlMain N3), C-605 missing interface-member comments corpus-wide, dangling comment references to nonexistent member comments (seq N4/N12), no FC_EStopAlarms/DB_PLC (C-502/C-305) → review-conventions/review-simplicity.

## Open questions (carried + newly raised)

- Q-01 carried — restart/first-scan: corpus today has no OB100 and retentive `Step`; a mid-cycle power loss resumes at the retained step with non-retained internals cleared. Owner statement still needed.
- Q-02 carried — the nine unconfigured DB_Settings members are confirmed in DB_Settings.ir; consequences are worse than "unset": PT=0 makes the discharge confirm (REQ-004) abort the start sequence, the pusher timeouts (REQ-032/054/055) collapse every cycle in one scan, the resume delay (REQ-047) vanishes, and the pump run-on (REQ-031) disappears across the hold gap.
- Q-03 carried — `DI16_PSH_LocalRemote` is mapped into the buffer and read by nothing; no remote cycle button input exists (REQ-036).
- Q-04 carried — no machine-type artifacts anywhere (REQ-018/019/064/067).
- Q-05 carried — one instrumented motor system (REQ-020/057).
- Q-06 carried — corpus implements a momentary start press (single level-gated transition), not press-and-hold.
- Q-07 carried — corpus implements 3 s and the separate volt-free upstream output (FuncDesc reading).
- Q-08 carried — corpus does not auto-restart on downstream recovery; fresh start required from step 0.
- Q-09 carried — the per-instance `ReverseDelay = 8.0` is confirmed as the wired tunable for the pause (`MUL IO.ReverseDelay → PauseTimeMS → ReversalPauseTimer.PT`) — but see NEW N-2 for what that pause actually does.
- Q-10 carried — corpus's answer today: one merged "Fault/Overload Tripped" bit (X0) on the single soft-start relay input.
- Q-11 carried — no AI writer for `ShredderMotorCurrent`; the buffer header records the owner decision; overcurrent detection is placeholder-gated pending hardware.
- Q-12 carried — no zeroable-hours counterpart exists (REQ-066).
- Q-13 carried — no lamp/display output tags for REQ-060's outside-panel items.
- Q-14 carried — no control-on/reset precondition exists before start; the corpus does drive DQ11 from `DB_Controls.FaultReset` continuously (unrequested-logic list).
- Q-15 carried — corpus folds hand/jog into Manual (mode 2) at Step 0; not a fourth mode.
- NEW N-1 — Per-instance motor timing members are unconfigured and outside Q-02's list: `IO.FTTime` (=0.0 ⇒ instant failed-to-run trip; no real motor start can survive — REQ-007/062/063), `IO.ReverseIgnoreFT`, `IO.EnableUPSTime`, `IO.ShutdownTime`. Owner numbers needed (or explicit inert-by-design ruling for the latter two).
- NEW N-2 — Reverse-run design conflict: the motor FB imposes its `ReverseDelay` pause on every Reverse rising edge (even from standstill), while the sequencer's 6 s reverse window expires inside that 8 s pause — reverse never runs (REQ-005/006/027). Which side should change is an engineering ruling.
- NEW N-3 — Should stop (button and downstream loss) stop the pusher subsystem? The stop-all path currently ends at the sequencer; the pusher has no stop/healthy input (REQ-012/013).
- NEW N-4 — Is unwarned automatic pusher motion (auto-park on release/at idle, no siren) acceptable? It contradicts REQ-039's stop-in-place and sidesteps REQ-045's pre-start sequencing; the N6 comment shows it is a deliberate never-park-from-unknown-position policy.
- NEW N-5 — Jog is not locked out by a pusher-blocked fault (REQ-050 gates only cycle starts). Intended for jam-freeing?
- NEW N-6 — The register cites REQ-069's owner ruling as recorded in FC_ControlMain network 7's comment; this corpus snapshot has no comment on that network (and no comment anywhere states the lamp ruling). Where does the ruling text now live?

## Not statically checkable (S9 sim-test candidates, keyed by REQ ID)

- REQ-062: E-stop mid-run → reset → verify no equipment restarts without a fresh start press (once a healthy-loss exit exists); also exercise the feedback-decay vs FTTime race to confirm no spurious FTR/FTS latches during E-stop.
- REQ-005/006/007 (post-fix): with a configured FTTime and real feedback delays, verify reverse actually rotates for 6 s, the 8 s spin-down runs after it, and the forward start survives its feedback lag.
- REQ-028: event-spacing semantics of the re-arming 180 s window (e.g. 5 events straddling a window reset) — confirm the built behavior matches the owner's intent for "5 in 3 minutes".
- REQ-031 (once configured): pump run-on continuity across the step-20 hold gap and across push→retract, including the `PumpEverDemanded` cold-start guard.
- REQ-047 (once configured): hold-release timing under a bouncing pressure switch (confirm/clear timer interplay).
- Q-01/REQ-062-adjacent: power cycle mid-cycle at each step — retained `Step` with cleared internals (Pasue, timers, edge memories, PumpEverDemanded) — characterize actual resume behavior before the owner rules on required behavior.

### Phase 2 (current corpus @ `19b2022`)

---

# Functional review — test-project001 (`ir/test-project001/`, all 21 files) vs `gen/test-project001/requirements.md` (2026-07-16)

Blindness: **blind** — fresh-context session; no authoring context for this corpus and no author rationale in the conversation. Expected caveat, declared per the skill: I read the register's own notes/tag-status marks and doc 06's rule rationales (both cite this corpus's history, e.g. C-308's "FC_ControlMain trap", C-126's HMI-times exception); every verdict below was re-derived from the IR itself, never taken from a note's summary.
Register: gen/test-project001/requirements.md @ 2295c64
Corpus: ir/test-project001/ @ 19b2022
Blocks read: Main (generated), FC_Inputs (generated), FC_Outputs (generated), FC_ControlMain (generated), FC_AlarmsMain (generated), FB_ShredderSequencer (generated), FB_PusherControl (generated), UDT_ShredderSequencerIO (generated), UDT_PusherIO (generated), iDB_ShredderSequencer (generated), iDB_PusherControl (generated), DB_Input (generated), DB_Output (generated), DB_AnalogInput (generated), DB_Controls (generated), DB_Settings (generated), DB_Alarms (generated), DefaultTagTable (generated), FB_MotorFwdRevSystem (imported-real), MotorFwdRevIOSet (imported-real), iDB_MotorFwdRevSystem_Shredder (imported-real). Read-only throughout; `.ir` content read only above each file's `SIDECAR` line. No F-/safety-block content was encountered.

**Systemic reachability note (cited by several verdicts below):** with the committed start values, the plant start sequence cannot pass step 20 — `DB_Settings.DischargeConveyorTimeout` has no start value (= 0.0), so `DischargeStartTimer` (PT = 0 ms) fires the instant step 20 is entered, latching the timeout fault and aborting to idle before the discharge conveyor is even commanded for one scan. Behind that gate sit two further blockers: the imported iDB's `FTTime` (= 0.0, no start value) latches a fail-to-run fault one scan into any motor start, and the pusher's unconfigured `PusherEndTravelTimeout`/`PusherParkedTimeout` (= 0.0) abort/fault every pusher stroke instantly. Verdicts below judge each REQ's own chain; this note is the shared as-committed context (Q-02 + NEW-1).

## Per-REQ trace

### REQ-001 — Start command: implemented
Where: DI3 → FC_Inputs N1 → DB_Input.Cycle_Start → FC_ControlMain N1 → iDB_ShredderSequencer.IO.CycleStart → FB_ShredderSequencer N6.
Evidence: `COIL DB_Input.Cycle_Start := AlwaysTrue AND NOT DB_Input.Test[0] AND DI3_SYS_CycleStart OR AlwaysTrue AND DB_Input.Test[3]`; `COIL iDB_ShredderSequencer.IO.CycleStart := DB_Input.Cycle_Start`; `MOVE(EN := NOT StopCmd AND NOT IO.ShredderBlockedFault AND IO.CycleStart AND IO.DownstreamRunning AND IO.Step = 0, IN := 10) => IO.Step`.
Notes: As-built the press is momentary (checked only at the 0→10 transition) — Q-06 carried, corpus implements the FuncDesc reading.

### REQ-002 — Downstream confirmed before start: implemented
Where: DI15 → FC_Inputs N2 → DB_Input.Downstream_Running → FC_ControlMain N1 → IO.DownstreamRunning → seq N6 (and N1 via StopCmd).
Evidence: `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning`; N6 transition quoted at REQ-001 includes `AND IO.DownstreamRunning`.

### REQ-003 — Pre-start siren: implemented
Where: seq N7 (timer), N15 (output) → FC_ControlMain N7 → DB_Output.PreStart_Sounder → FC_Outputs N1 → DQ10.
Evidence: `TON(PreStartTimer, IN := IO.Step = 10, PT := PreStartSounderTimeMS)`; `PreStartSounderTime : Real RETAIN = 10.0` (matches 10 s); `COIL IO.PreStartSounder := IO.Step = 10 OR IO.PusherJogPreStartSounder`; `COIL DQ10_SYS_PreStartSounder := AlwaysTrue AND DB_Output.PreStart_Sounder AND NOT DB_Output.Test[0] OR AlwaysTrue AND DB_Output.Test[10]`. Number chain: MUL pair 1 → `PreStartSounderTimeMS` → `PreStartTimer.PT` (index-matched, verified). Equipment starts only from step 20 (`COIL IO.RunDischargeConv := IO.Step >= 20`; `COIL IO.MotorPreStartDoneCmd := IO.Step >= 20`).
Notes: Holds within the start sequence. Pusher motion paths outside the start sequence run **unwarned** (auto-repark and auto cycles have no siren; only jog warns) — see Defects D-4 / NEW-4. E-stop-recovery restart also bypasses the siren — see REQ-062.

### REQ-004 — Discharge conveyor first, confirmed: partial
Where: seq N15 (command), N8 (confirm + timeout) → DB_Output.Run_Discharge_Conv → DQ5; DI10 → DB_Input.Discharge_Conv_Running → IO.DischargeConvRunning.
Evidence: `COIL IO.RunDischargeConv := IO.Step >= 20`; `MOVE(EN := NOT StopCmd AND NOT DischargeStartTimer.Q AND IO.DischargeConvRunning AND IO.Step = 20, IN := 30) => IO.Step`; `TON(DischargeStartTimer, IN := IO.Step = 20, PT := DischargeConveyorTimeoutMS)`; `DischargeConveyorTimeout : Real RETAIN` (no start value).
Notes: Chain and confirm gate complete; the confirm-window number is unconfigured (Q-02). Consequence as-committed: PT = 0 ⇒ `DischargeStartTimer.Q` true on step-20 entry ⇒ instant `DischargeConveyorTimeoutFault` + abort to 0 in the same scan (`MOVE(EN := NOT StopCmd AND DischargeStartTimer.Q AND IO.Step = 20, IN := 0)`), before N15 ever asserts the run command — **the start sequence dead-ends here** (systemic note).

### REQ-005 — Shredder reverse run at start: unimplemented (chain broken — the behavior can never occur)
Where: seq N9/N14 → FC_ControlMain N3 → motor IO.Reverse/IO.AutoStartSignal → FB_MotorFwdRevSystem N1/N2 → IO.RunRev → FC_ControlMain N7 → DB_Output.Run_Shredder_Rev → DQ2.
Evidence: `TON(ReverseRunTimer, IN := IO.Step = 30, PT := ShredderReverseRunTimeMS)` with `ShredderReverseRunTime : Real RETAIN = 6.0`; `COIL IO.MotorReverseCmd := IO.Step = 30`; motor N1: `SCOIL Pasue := IO.Reverse AND NOT RisingEdgeFlags[0]` (pause SET on the **rising** edge of Reverse) with `TON(ReversalPauseTimer, IN := Pasue, PT := PauseTimeMS)` and `ReverseDelay : Real = 8.0` (iDB) feeding `PauseTimeMS` (motor N7 MUL/CONVERT pair 4); motor N2: `COIL IO.RunRev := (…) AND NOT IO.StopMotor AND NOT IO.Shutdown AND NOT Pasue AND IO.Reverse`.
Notes: Entering step 30 raises `Reverse`, whose rising edge sets `Pasue` for 8 s; `RunRev` requires `NOT Pasue AND Reverse`, but `Reverse` is true only while Step = 30, which `ReverseRunTimer.Q` exits after 6 s — the 6 s reverse window elapses entirely inside the 8 s hold-off, so the `NOT Pasue AND Reverse` windows **never overlap and the shredder never runs in reverse** — not at start, and not on the REQ-027 retry (same 60→30 path). Structural (survives configuring all Q-02 numbers; with any `ReverseDelay` < window, actual reverse time = window − pause, still not the spec'd 6 s). NEW-3. Headline finding.

### REQ-006 — Spin-down pause after reverse: partial
Where: FB_MotorFwdRevSystem N1/N7 (imported-real, traced read-only), instance value in iDB_MotorFwdRevSystem_Shredder.
Evidence: `TON(ReversalPauseTimer, IN := Pasue, PT := PauseTimeMS)`; `SCOIL Pasue := (IO.Reverse OR NegitiveSignalEdge[0]) AND NOT IO.Reverse` (falling-edge set); `MUL(EN := TRUE, IN1 := IO.ReverseDelay, IN2 := 1000.0) => Time` → `CONVERT(EN := ENO, IN := Time) => PauseTimeMS`; `ReverseDelay : Real = 8.0`.
Notes: The 8 s motor-off pause mechanism exists and is number-correct (Q-09: the per-instance `ReverseDelay` is confirmed as the operative tunable — chain verified to the pause timer's PT). But the REQ's premise "after the reverse run" never occurs (REQ-005): as built the pause runs from step-30 entry and precedes a first-ever **forward** start. The specified stop-after-reverse behavior cannot be observed.

### REQ-007 — Shredder forward start: partial
Where: seq N14 (`MotorAutoStartCmd`), N10 (40→50 on feedback); motor N2 RunFwd → DB_Output.Run_Shredder_Fwd → DQ1; DI5 → IO.ShredderRunFwdFB.
Evidence: `COIL IO.MotorAutoStartCmd := IO.Step >= 30 AND IO.Step <= 50`; `COIL IO.RunFwd := (IO.TryRunMotor AND PreStartMemory AND IO.RecentStart OR … OR IO.TryRunMotor AND CycleDelay) AND NOT IO.StopMotor AND NOT IO.Shutdown AND NOT Pasue AND NOT IO.Reverse`; `MOVE(EN := NOT StopCmd AND NOT IO.MotorFaultActive AND IO.ShredderRunFwdFB AND IO.Step = 40, IN := 50) => IO.Step`; iDB: `FTTime : Real` (no start value) feeding `TON(FaultTripTimer, IN := IO.RunFwd AND NOT IO.RunningFBFwd OR IO.RunRev AND NOT IO.RunningFBRev, PT := FTTimeMS)` and `COIL IO.FTR := FaultTripTimer.Q AND NOT ReversalIgnoreFTTimer.IN AND NOT ReversalIgnoreFTTimer.Q OR IO.FTR AND NOT IO.FaultReset`.
Notes: Command/feedback chain complete and the forward start does follow the 8 s pause. But as-committed `FTTime` = 0.0 and `ReverseIgnoreFT` = 0.0 (both unconfigured in the imported iDB, **not** covered by Q-02) ⇒ the fail-to-run window is zero ⇒ FTR latches one scan after any start command (contactor feedback cannot return within a scan) ⇒ `FaultActive` ⇒ sequencer aborts. NEW-1.

### REQ-008 — Hopper-not-high gate before infeed: implemented
Where: seq N11; DI8 → DB_Input.Hopper_Level_High → IO.HopperLevelHigh.
Evidence: `TON(InfeedRestartTimer, IN := IO.Step = 50 AND NOT IO.HopperLevelHigh, PT := InfeedRestartDelayMS)`.

### REQ-009 — Delay after hopper-clear confirm: implemented
Where: seq N11; DB_Settings.
Evidence: timer quoted at REQ-008; `InfeedRestartDelay : Real RETAIN = 5.0` (matches 5 s); MUL/CONVERT pair 5 → `InfeedRestartDelayMS` → `InfeedRestartTimer.PT` (index-matched, verified). Shared with REQ-015 as the register presumed (one setting).

### REQ-010 — Infeed conveyor start: implemented
Where: seq N11 → FC_ControlMain N7 → DB_Output.Run_Infeed_Conv → FC_Outputs N1 → DQ4.
Evidence: `COIL InfeedRunning := InfeedRestartTimer.Q`; `COIL IO.RunInfeedConv := InfeedRunning`; `COIL DB_Output.Run_Infeed_Conv := iDB_ShredderSequencer.IO.RunInfeedConv`; `COIL DQ4_IFC_Run := AlwaysTrue AND DB_Output.Run_Infeed_Conv AND NOT DB_Output.Test[0] OR AlwaysTrue AND DB_Output.Test[4]`.
Notes: Feedback DI9 is mapped but not supervised anywhere (see REQ-063).

### REQ-011 — Upstream enable after infeed: implemented
Where: seq N11 → DB_Output.Enable_Upstream → DQ9.
Evidence: `TON(UpstreamEnableTimer, IN := InfeedRunning, PT := InfeedToUpstreamEnableDelayMS)`; `COIL IO.EnableUpstream := UpstreamEnableTimer.Q`; `InfeedToUpstreamEnableDelay : Real RETAIN = 3.0` (matches 3 s; Q-07 carried — corpus implements FuncDesc's 3 s with the separate output). Block comment declares the deliberate one-scan lag on `InfeedRunning` — harmless at 3 s.

### REQ-012 — Stop pushbutton stops all: partial
Where: DI4 (NC) → FC_Inputs N1 (negated) → DB_Input.Cycle_Stop → IO.CycleStop → StopCmd → every step's stop exit → outputs drop same scan.
Evidence: `COIL DB_Input.Cycle_Stop := AlwaysTrue AND NOT DB_Input.Test[0] AND NOT DI4_SYS_CycleStop OR AlwaysTrue AND DB_Input.Test[4]` (polarity absorbed at the map — active-high stop; correct, not the historical stuck-stop polarity); `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning`; e.g. `MOVE(EN := StopCmd AND IO.Step = 50, IN := 0) => IO.Step` (equivalent MOVEs at steps 10/20/30/40/60); shredder/conveyors/upstream all derive from Step and drop in the same scan.
Notes: **The stop chain never reaches the pusher.** `UDT_PusherIO` has no stop/cycle input; `PusherModeCmd` passes the operator mode through except at step 60 (`MOVE(EN := IO.Step <> 60, IN := IO.OperatorPusherMode) => IO.PusherModeCmd`). A pusher mid-cycle (power pack + solenoids) continues to completion after a stop press, and manual/auto cycles can start while the plant is stopped. "Stops all equipment at the same time" is not met for the pusher. NEW-2.

### REQ-013 — Downstream loss stops all: partial
Where/Evidence: same `StopCmd` OR-term `NOT IO.DownstreamRunning` (quoted at REQ-012); same step exits.
Notes: Same pusher gap as REQ-012. Q-08 carried: as built, downstream recovery does **not** auto-restart — step 0 requires a fresh `CycleStart`.

### REQ-014 — Hopper high stops infeed only: implemented
Where: seq N11.
Evidence: `TON(InfeedRestartTimer, IN := IO.Step = 50 AND NOT IO.HopperLevelHigh, …)` — hopper high ⇒ IN false ⇒ Q false ⇒ `InfeedRunning`/`RunInfeedConv` false (infeed stops); Step stays 50 and `COIL IO.MotorAutoStartCmd := IO.Step >= 30 AND IO.Step <= 50` keeps the shredder commanded (shredder keeps running).

### REQ-015 — Infeed restart on hopper clear: implemented
Where/Evidence: same timer; hopper clear re-arms IN, 5 s (`InfeedRestartDelay = 5.0`) → restart.

### REQ-016 — Upstream re-enable after infeed restart: implemented
Where/Evidence: `UpstreamEnableTimer` (quoted at REQ-011) resets when `InfeedRunning` drops and re-times 3 s on restart.

### REQ-017 — Overcurrent event definition: disarmed
Where: seq N12; input chain FC_ControlMain N1 → IO.ShredderMotorCurrent.
Evidence: `TON(OvercurrentMediumTimer, IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue, PT := OvercurrentMediumDelayMS)` (identically `OvercurrentHighTimer`); `COIL OvercurrentTripped := OvercurrentMediumTimer.Q OR OvercurrentHighTimer.Q`. The `NOT AlwaysTrue` constant-false gate sits exactly where the current-vs-setpoint comparison belongs.
Notes: More than a gate: the comparison is absent entirely — `IO.ShredderMotorCurrent` is written (`MOVE(EN := TRUE, IN := DB_AnalogInput.ShredderMotorCurrent) => iDB_ShredderSequencer.IO.ShredderMotorCurrent`) but read by **no** network (grep-verified), and `DB_AnalogInput.ShredderMotorCurrent` itself has no writer (no analog map — DB header documents this; Q-11). Setpoints unread (REQ-019). "Distinct from overload" is preserved structurally (separate paths). Disarm is commented at N12 per the placeholder idiom; the referenced "OvercurrentTripped's own comment" does not appear in the IR interface.

### REQ-018 — Machine-type selection in engineering: unimplemented
Where: nowhere.
Evidence: grep `K75|K100|K150|MachineType|Machine_Type` across the corpus → `No matches found`.
Notes: Register marks it proposed (Q-04). Anti-laundering check clean — no logic references an invented selection tag.

### REQ-019 — Overcurrent setpoints per machine type: partial
Where: DB_Settings only.
Evidence: `OvercurrentSetpointMedium : Real RETAIN` / `OvercurrentSetpointHigh : Real RETAIN` (no start values); grep shows these two declaration lines are the **only** occurrences in the corpus — no consumer anywhere.
Notes: One machine-agnostic pair exists of the three per-type sets asked; unconfigured (Q-02); per-type structure proposed (Q-04); and both members are dead configuration (nothing reads them — Pass-2 finding).

### REQ-020 — Overcurrent on either motor: unimplemented
Where: nowhere.
Evidence: single current buffer member (`DB_AnalogInput.ShredderMotorCurrent`), single combined fault input (`DI7_SYS_MotorFault`), one feedback pair (`DI5`/`DI6`) — no second-motor tags exist (register: proposed, Q-05).

### REQ-021 — Medium overcurrent level: disarmed
Where/Evidence: `OvercurrentMediumTimer` quoted at REQ-017 (gated `NOT AlwaysTrue`); `OvercurrentMediumDelay : Real RETAIN` (unconfigured, Q-02); PT chain MUL/CONVERT pair 8 → `OvercurrentMediumDelayMS` (index-matched, verified). No current-above-setpoint term exists.

### REQ-022 — High overcurrent level: disarmed
Where/Evidence: as REQ-021 with `OvercurrentHighTimer` / `OvercurrentHighDelay : Real RETAIN` (unconfigured, Q-02); pair 9 verified.

### REQ-023 — Overcurrent stops the shredder: disarmed
Where: seq N12 → N14.
Evidence: `MOVE(EN := NOT StopCmd AND NOT IO.MotorFaultActive AND OvercurrentTripped AND IO.Step = 50, IN := 60) => IO.Step`; at step 60, `IO.MotorAutoStartCmd := IO.Step >= 30 AND IO.Step <= 50` drops ⇒ motor stops. Response built; trigger `OvercurrentTripped` is constant-false (REQ-017).

### REQ-024 — Overcurrent stops the infeed: disarmed
Where/Evidence: step 60 ⇒ `InfeedRestartTimer` IN (`IO.Step = 50 AND …`) false ⇒ `RunInfeedConv` false. Same dead trigger.

### REQ-025 — Overcurrent parks the pusher: disarmed
Where: seq N14 → FC_ControlMain N5 → pusher Mode.
Evidence: `MOVE(EN := IO.Step = 60, IN := 0) => IO.PusherModeCmd`; `MOVE(EN := TRUE, IN := iDB_ShredderSequencer.IO.PusherModeCmd) => iDB_PusherControl.IO.Mode`; pusher mode-0 exits (`MOVE(EN := IO.Step = 10 AND IO.Mode = 0, IN := 30) => IO.Step`, N8 equivalent) drive retract → parked. Same dead trigger.

### REQ-026 — Post-overcurrent pause: disarmed
Where/Evidence: `TON(OvercurrentAbortPauseTimer, IN := IO.Step = 60, PT := ReversalRetryPauseTimeMS)`; `ReversalRetryPauseTime : Real RETAIN = 4.0` (matches 4 s; pair 10 verified). Same dead trigger.

### REQ-027 — Auto-resume via reverse run: disarmed
Where/Evidence: `MOVE(EN := NOT StopCmd AND OvercurrentAbortPauseTimer.Q AND NOT IO.ShredderBlockedFault AND IO.Step = 60, IN := 30) => IO.Step` — resumes at step 30, matching "resume from the reverse-run step".
Notes: Even if armed, the retry's reverse would never physically run (REQ-005's pause interplay) — the jam-clearing function would not occur.

### REQ-028 — Reversal-count trip: disarmed
Where: seq N3/N4/N5/N6/N13; annunciation at REQ-058.
Evidence: `COIL ReversalStepEdge := IO.Step = 60 AND NOT ReversalStepEdgeMem` + `ADD(EN := ReversalStepEdge, IN1 := 1, IN2 := ReversalCount) => ReversalCount` (constructed edge); `TON(ReversalWindowTimer, IN := ReversalCount > 0, PT := ReversalWindowTimeMS)` + `MOVE(EN := ReversalWindowTimer.Q OR IO.FaultReset, IN := 0) => ReversalCount`; `COIL IO.ShredderBlockedFault := ReversalCount >= DB_Settings.ReversalCountThreshold OR IO.ShredderBlockedFault AND NOT IO.FaultReset`; `ReversalCountThreshold : Int RETAIN = 5`, `ReversalWindowTime : Real RETAIN = 180.0` (both match); threshold compared against the named member, not a literal ✓; stop + lockout: step-60 exit to 0 when faulted and `NOT IO.ShredderBlockedFault` in the N6 start transition.
Notes: Trigger chain (step-60 entries) is dead (REQ-017). Window semantics are a re-arming fixed window from the first event, not a sliding window — 5 events straddling a window reset would not trip; S9 item.

### REQ-029 — Spin-up overcurrent suppression: disarmed
Where/Evidence: `TON(SpinUpTimer, IN := IO.Step = 50, PT := OvercurrentSpinUpAllowanceMS)`; both overcurrent timers gated `AND SpinUpTimer.Q`; `OvercurrentSpinUpAllowance : Real RETAIN = 3.0` (within the stated 2–3 s; pair 7 verified).
Notes: Suppression chain itself complete and number-correct; disarmed with the family it gates.

### REQ-030 — Power pack before and during movement: implemented
Where: pusher N12/N13 → DB_Output.Run_PowerPack/Pusher_Extend/Pusher_Retract → DQ3/DQ6/DQ7; DI11 → IO.PowerPackRunningFB.
Evidence: `COIL PumpDemand := ExtendDemand OR RetractDemand`; `COIL IO.Extend := ExtendDemand AND IO.PowerPackRunningFB`; `COIL IO.Retract := RetractDemand AND IO.PowerPackRunningFB` — solenoids only ever energize with confirmed pump running, and drop if it drops.

### REQ-031 — Power pack run-on: partial
Where: pusher N13.
Evidence: `TON(PumpRunOnTimer, IN := NOT PumpDemand, PT := PumpRunOnTimeMS)`; `COIL IO.RunPowerPack := PumpDemand OR PumpEverDemanded AND NOT PumpRunOnTimer.Q`; `PusherPumpRunOnTime : Real RETAIN` (unconfigured, Q-02 — no spec number either).
Notes: Mechanism complete (TON+inversion off-delay, cold-start guarded by `PumpEverDemanded` per the network comment). As-committed run-on = 0 s, so the pump **does** stop during the 2 s hold between push and retract (`PumpDemand` is false at step 20) — the REQ's stated purpose is defeated until a number is set. Retract still waits for pump feedback, so no unsafe motion.

### REQ-032 — Pusher cycle definition: partial
Where: pusher N6 (launch) → N7 (10, extend) → N8 (20, hold) → N9 (30, retract) → 0.
Evidence: launch `MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked, IN := 10) => IO.Step`; `MOVE(EN := IO.Step = 10 AND IO.Mode <> 0 AND NOT EndTravelTimer.Q AND NOT IO.Blocked AND IO.FullTravelLimit, IN := 20)`; `TON(HoldTimer, IN := IO.Step = 20, PT := EndTravelHoldTimeMS)` with `PusherEndTravelHoldTime : Real RETAIN = 2.0` (matches ~2 s); `MOVE(EN := (HoldTimer.Q OR IO.Mode = 0) AND IO.Step = 20, IN := 30)`; `MOVE(EN := IO.Step = 30 AND IO.HomeLimit, IN := 0)`.
Notes: Cycle logic complete and its own number correct; as-committed every cycle aborts at stroke start because `PusherEndTravelTimeout` = 0 (REQ-054, Q-02) makes `EndTravelTimer.Q` fire on step-10 entry.

### REQ-033 — Three pusher modes: implemented
Where: DB_Controls.PusherMode → FC_ControlMain N1 → seq N14 → FC_ControlMain N5 → pusher IO.Mode; consumed in N1/N6/N7/N8/N10/N11.
Evidence: `MOVE(EN := TRUE, IN := DB_Controls.PusherMode) => iDB_ShredderSequencer.IO.OperatorPusherMode`; header: `Mode legend: 0=Off … 1=Auto (hopper-high-level starts a cycle) - 2=Manual (operator button/jog starts a cycle)`.
Notes: Q-15 carried — as built, hand/jog is an overlay inside Manual (Mode 2 at Step 0), not a fourth mode.

### REQ-034 — Auto mode: cycle on hopper high: implemented
Where/Evidence: launch MOVE quoted at REQ-032, arm `IO.Mode = 1 AND IO.HopperHighLevel`.
Notes: Level-triggered — while the hopper stays high, completed cycles relaunch continuously (source wording "perform 1 cycle" — NEW-6). As-committed cycles abort instantly (REQ-054's zero timeout).

### REQ-035 — Manual mode: HMI cycle button: implemented
Where/Evidence: same launch MOVE, arm `IO.Mode = 2 AND IO.ManualCycleCmd`; `COIL iDB_PusherControl.IO.ManualCycleCmd := DB_Controls.PusherManualCycleCmd` (FC_ControlMain N5).

### REQ-036 — Manual mode: remote pushbutton: unimplemented
Where: nowhere.
Evidence: no remote-cycle DI exists (register: proposed, Q-03); `DB_Input.Pusher_Local_Remote` is written (`COIL DB_Input.Pusher_Local_Remote := … AND DI16_PSH_LocalRemote …`) and read by **nothing** (grep-verified — declaration + map write only).
Notes: Nothing is coded against a proposed tag (anti-laundering clean); the selector is dead wiring (Pass 2).

### REQ-037 — Off mode: retract to home: implemented
Where: pusher N6/N7/N8/N9.
Evidence: `MOVE(EN := NOT ((IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Mode = 2) AND IO.Step = 0 AND NOT IO.HomeLimit, IN := 30) => IO.Step` (idle-not-home → retract, active in Mode 0); `MOVE(EN := IO.Step = 10 AND IO.Mode = 0, IN := 30)`; `MOVE(EN := (HoldTimer.Q OR IO.Mode = 0) AND IO.Step = 20, IN := 30)`; N9 retract → home → 0.

### REQ-038 — Hand control: jog per direction: implemented
Where: DB_Controls.PusherJogExtendCmd/PusherJogRetractCmd → FC_ControlMain N5 → pusher N10/N11.
Evidence: `COIL ExtendDemand := IO.Step = 10 AND NOT PressureHold OR IO.Step = 0 AND IO.Mode = 2 AND IO.JogExtendCmd AND JogPreStartTimer.Q`; `COIL RetractDemand := IO.Step = 30 OR IO.Step = 0 AND IO.Mode = 2 AND IO.JogRetractCmd AND JogPreStartTimer.Q`.
Notes: Available in Manual mode at Step 0 only (Q-15).

### REQ-039 — Jog is hold-to-move: contradicted
Where: pusher N11 (hold-to-move) vs N6 first MOVE (release behavior).
Evidence: demands quoted at REQ-038 are live-conditioned on the held button (hold-to-move itself is correct); but on release, `MOVE(EN := NOT ((IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Mode = 2) AND IO.Step = 0 AND NOT IO.HomeLimit, IN := 30) => IO.Step` fires the same scan (suppression ends with the button), driving Step to 30 ⇒ `RetractDemand := IO.Step = 30` ⇒ full auto-retract to home.
Notes: "On release it stops where it is" is directly violated: release triggers an automatic powered retract to the home limit. The N6 comment shows the auto-repark was deliberately suppressed only *during* the jog, leaving the release case to retract.

### REQ-040 — Jog independent of shredder: implemented
Where/Evidence: jog path (N10/N11 quotes above) references only Mode/Step/limits — no shredder or sequencer state; mode passes through unchanged except step 60 (`MOVE(EN := IO.Step <> 60, IN := IO.OperatorPusherMode) => IO.PusherModeCmd`), and step 60 is unreachable (disarmed).

### REQ-041 — Jog pre-start warning: implemented
Where: pusher N10 → IO.JogPreStartSounder → FC_ControlMain N1 → seq IO.PusherJogPreStartSounder → seq N15 → DB_Output.PreStart_Sounder → DQ10.
Evidence: `TON(JogPreStartTimer, IN := (IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Step = 0 AND IO.Mode = 2, PT := JogWarningTimeMS)` with `PusherJogWarningTime : Real RETAIN = 1.0` (matches 1 s); `COIL IO.JogPreStartSounder := (IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Step = 0 AND IO.Mode = 2`; `COIL iDB_ShredderSequencer.IO.PusherJogPreStartSounder := iDB_PusherControl.IO.JogPreStartSounder`; `COIL IO.PreStartSounder := IO.Step = 10 OR IO.PusherJogPreStartSounder`.
Notes: Sounder follows the held button: 1 s warning before demand arms, continues while transitioning (movement requires the same held button). Full chain to DQ10 verified hop-by-hop.

### REQ-042 — Engineering pusher enable/disable: implemented
Where: DB_Settings.PusherFitted → FC_ControlMain N5 → pusher N1.
Evidence: `PusherFitted : Bool RETAIN = TRUE`; `COIL iDB_PusherControl.IO.Fitted := DB_Settings.PusherFitted`; `COIL IO.Enable := IO.Fitted AND IO.Mode <> 0`.
Notes: The control exists and gates cycle launches. The *completeness* of disablement fails — that is REQ-043/044.

### REQ-043 — Disabled pusher: no outputs, no feedback supervision: contradicted
Where: pusher N6 (first MOVE), N9, N11, N13.
Evidence: the idle-not-home transition `MOVE(EN := NOT ((IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Mode = 2) AND IO.Step = 0 AND NOT IO.HomeLimit, IN := 30) => IO.Step` carries **no `Fitted`/`Enable` term**; `COIL RetractDemand := IO.Step = 30 OR …` (no Fitted term); `COIL IO.RunPowerPack := PumpDemand OR PumpEverDemanded AND NOT PumpRunOnTimer.Q` ⇒ with `Fitted = FALSE` on a machine with no pusher (HomeLimit input absent ⇒ FALSE), the first scan drives Step to 30 and **energizes DQ3_PSH_RunPowerPack permanently**; `TON(ParkedTimer, IN := IO.Step = 30, PT := ParkedTimeoutMS)` keeps supervising the never-coming switch feedback. The jog demand path (`IO.Step = 0 AND IO.Mode = 2 AND IO.JogExtendCmd AND JogPreStartTimer.Q`) also carries no Fitted term.
Notes: Exactly what the REQ forbids: with the pusher disabled the system attempts to run the power pack and looks for switch feedback. Solenoids stay off only because `PowerPackRunningFB` never confirms.

### REQ-044 — Disabled pusher: no pusher faults: contradicted
Where: pusher N9 + FC_AlarmsMain N7.
Evidence: `COIL IO.ParkedTimeoutFault := ParkedTimer.Q OR IO.ParkedTimeoutFault AND NOT IO.FaultReset` latches in the disabled scenario above (immediately, with the unconfigured `PusherParkedTimeout` = 0); `COIL DB_Alarms.ShredderAlarm0.%X6 := iDB_PusherControl.IO.ParkedTimeoutFault` annunciates it — no Fitted gating at either hop.
Notes: A pusher fault is raised and displayed with the pusher disabled.

### REQ-045 — Park after pre-start: partial
Where: pusher N6 first MOVE (quoted at REQ-043).
Evidence: the park action exists as an unconditional idle-not-home repark (comment: "Not-at-home while idle … drives straight to Retract (30) … Suppressed during an active jog command").
Notes: The REQ's trigger — *pre-start completion* — is absent: parking is not sequenced after the siren and is not gated by the plant sequence at all. Effect-wise the pusher is parked by the time pre-start completes, but the movement can occur at any time (power-up, plant idle) with **no pre-start warning** — see Defects D-4 / NEW-4.

### REQ-046 — High-pressure hold while pushing: implemented
Where: pusher N4/N11.
Evidence: `TON(PressureConfirmTimer, IN := IO.Step = 10 AND IO.HighPressure, PT := PressureTripConfirmTimeMS)` with `PressureTripConfirmTime : Real RETAIN = 0.5` (matches 0.5 s; MOVE at FC_ControlMain N5 → ×1000 pair 0 → PT verified); `COIL PressureHold := PressureConfirmTimer.Q OR PressureHold AND NOT PressureClearTimer.Q`; `COIL ExtendDemand := IO.Step = 10 AND NOT PressureHold OR …` — pushing stops, Step stays 10, position held (both solenoids off). A live hold, not a fault (C-123 shape).

### REQ-047 — Resume push after pressure clears: partial
Where: pusher N4/N11.
Evidence: `TON(PressureClearTimer, IN := PressureHold AND NOT IO.HighPressure, PT := PressureClearResumeDelayMS)`; hold release re-arms `ExtendDemand` at the unchanged Step 10 ("from the existing position", cycle continues). `PressureClearResumeDelay : Real RETAIN` — **unconfigured despite the source stating 5 s** (Q-02's flagged member).
Notes: As-committed the resume delay is 0 s where the spec says 5 s.

### REQ-048 — Pressure-trip count: return and fault: implemented
Where: pusher N4/N5/N7.
Evidence: `COIL PressureConfirmEdge := PressureConfirmTimer.Q AND NOT PressureConfirmEdgeMem` + `ADD(EN := PressureConfirmEdge, IN1 := 1, IN2 := PressureTripCount) => PressureTripCount`; per-cycle scope via `MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked OR IO.FaultReset, IN := 0) => PressureTripCount`; `COIL IO.Blocked := PressureTripCount >= IO.PressureTripCountThreshold OR IO.Blocked AND NOT IO.FaultReset` with `PressureTripCountThreshold : Int RETAIN = 5` (matches; compared via the named member ✓); return: `MOVE(EN := IO.Step = 10 AND IO.Mode <> 0 AND NOT EndTravelTimer.Q AND IO.Blocked, IN := 30)`; annunciation `%X4` (REQ-053).

### REQ-049 — Blocked fault spares the shredder: implemented
Where/Evidence: `iDB_PusherControl.IO.Blocked` is consumed only inside FB_PusherControl and by `COIL DB_Alarms.ShredderAlarm0.%X4 := iDB_PusherControl.IO.Blocked` (grep-verified); the sequencer interface has no pusher-fault member — the shredder cannot be affected.

### REQ-050 — Blocked lockout until reset: implemented
Where: pusher N5/N6; DB_Controls.FaultReset → FC_ControlMain N5 → IO.FaultReset.
Evidence: launch MOVE requires `AND NOT IO.Blocked`; `COIL IO.Blocked := … OR IO.Blocked AND NOT IO.FaultReset` (latched until reset); `COIL iDB_PusherControl.IO.FaultReset := DB_Controls.FaultReset`.
Notes: Jog is **not** locked out while Blocked (jog demands carry no `NOT IO.Blocked`) — arguably hand-test freedom, but the REQ says "locked out from activating"; NEW-5.

### REQ-051 — Pressure ignored on retract: implemented
Where/Evidence: the only pressure logic is `TON(PressureConfirmTimer, IN := IO.Step = 10 AND IO.HighPressure, …)` — step 30 (retract) never evaluates the pressure switch.

### REQ-052 — Fault: both pusher switches: implemented
Where: pusher N2 → FC_AlarmsMain N4.
Evidence: `COIL IO.BothSwitchesFault := IO.HomeLimit AND IO.FullTravelLimit OR IO.BothSwitchesFault AND NOT IO.FaultReset`; `COIL DB_Alarms.ShredderAlarm0.%X3 := iDB_PusherControl.IO.BothSwitchesFault` (network title carries the alarm text). Text/severity → alarm-design tier.

### REQ-053 — Fault: pusher blocked: implemented
Where/Evidence: cause per REQ-048; `COIL DB_Alarms.ShredderAlarm0.%X4 := iDB_PusherControl.IO.Blocked`.

### REQ-054 — Fault: end-travel timeout: partial
Where: pusher N7 → FC_AlarmsMain N6.
Evidence: `TON(EndTravelTimer, IN := IO.Step = 10, PT := EndTravelTimeoutMS)`; `COIL IO.EndTravelTimeoutFault := EndTravelTimer.Q OR IO.EndTravelTimeoutFault AND NOT IO.FaultReset`; `COIL DB_Alarms.ShredderAlarm0.%X5 := iDB_PusherControl.IO.EndTravelTimeoutFault`; `PusherEndTravelTimeout : Real RETAIN` (unconfigured, Q-02).
Notes: As-committed PT = 0 ⇒ the fault fires and the stroke aborts to retract on every cycle start. Post-configuration observation: the timer runs through pressure holds (IN is `Step = 10` alone), so held time counts against the travel timeout.

### REQ-055 — Fault: parked timeout: partial
Where: pusher N9 → FC_AlarmsMain N7.
Evidence: `TON(ParkedTimer, IN := IO.Step = 30, PT := ParkedTimeoutMS)`; fault coil + `%X6` quoted at REQ-044; `PusherParkedTimeout : Real RETAIN` (unconfigured, Q-02).
Notes: As-committed fires on every retract stroke. Alarm-only by design (no forced transition) per the network comment — consistent with the REQ (fault, not motion).

### REQ-056 — Fault: motor tripped input: implemented
Where: DI7 → FC_Inputs N1 → DB_Input.Motor_Fault → FC_AlarmsMain N1 (display) and → FC_ControlMain N3 → motor IO.FaultFB → FaultActive (response).
Evidence: `COIL DB_Alarms.ShredderAlarm0.%X0 := DB_Input.Motor_Fault` under title "Shredder Motor - Fault/Overload Tripped - Check Soft Starter"; `COIL IO.FaultActive := IO.FTR OR IO.FTS OR IO.FaultFB OR IO.FaultActive AND NOT IO.FaultReset`.

### REQ-057 — Fault: motor failed to run, identified: partial
Where: motor N9 → FC_AlarmsMain N2; seq N8 → FC_AlarmsMain N9.
Evidence: `COIL DB_Alarms.ShredderAlarm0.%X1 := iDB_MotorFwdRevSystem_Shredder.IO.FTR` ("Shredder Motor - Failed To Start"); `COIL DB_Alarms.ShredderAlarm0.%X8 := iDB_ShredderSequencer.IO.DischargeConveyorTimeoutFault` ("Discharge Conveyor - Timeout Starting") — two motors identified by named bits.
Notes: Infeed conveyor (DI9 mapped, consumed only by the in-cycle lamp) and pusher power pack (DI11 gates solenoids only) have **no** failed-to-run monitoring. Per-motor identification beyond this is proposed (Q-05). Shredder FTR window as-committed is 0 (NEW-1).

### REQ-058 — Fault: shredder blocked: disarmed
Where/Evidence: `COIL DB_Alarms.ShredderAlarm0.%X7 := iDB_ShredderSequencer.IO.ShredderBlockedFault` — annunciation chain complete; the cause (`ReversalCount` from step-60 entries) can never accrue while overcurrent detection is constant-false-gated (REQ-017/028), so the alarm can never fire as built.

### REQ-059 — Fault: overload: partial
Where/Evidence: single input, single bit: `%X0 := DB_Input.Motor_Fault`, title "Fault/Overload Tripped" — overload is annunciated only merged with REQ-056's tripped-input fault; the hardware has one motor-protection input (Q-10 carried: distinguishability needs an owner ruling). Distinctness from overcurrent is structural (separate, disarmed path).

### REQ-060 — Reversal counter display: partial
Where: FB_ShredderSequencer Static.
Evidence: `ReversalCount : Int` — a bare Static in the FB/iDB, HMI-bindable as `iDB_ShredderSequencer.ReversalCount` (note: the register calls it an "interface member"; in the IR it is a Static **outside** the IO UDT — C-125 route note).
Notes: Outside-panel display + flashing motor-fault lamp have no output tags (proposed, Q-13). Counter value can also never move while the reversal chain is disarmed.

### REQ-061 — E-stop stops everything (hardwired): out-of-scope
Where/Evidence: confirmed no PLC logic pretends to own the stop function; the PLC-visible fact is consumed exactly once: `COIL iDB_MotorFwdRevSystem_Shredder.IO.SystemHealthy := DB_Input.Control_Healthy` (from `DI1_SYS_ControlHealthy`, tag comment "Safety/control circuit healthy"). No safety internals traced (hard rule 2).

### REQ-062 — No auto-restart after E-stop recovery: contradicted
Where: sequencer interface (absence); FC_ControlMain N3; motor N2/N4.
Evidence: `UDT_ShredderSequencerIO` has **no** healthy/system-OK member — `DB_Input.Control_Healthy`'s only consumer is the motor FB (grep-verified), so the step machine cannot react to an E-stop: at (say) Step 50 with downstream still available and no stop press, `StopCmd` stays false and Step holds 50; `COIL IO.RunDischargeConv := IO.Step >= 20` and `IO.MotorAutoStartCmd := IO.Step >= 30 AND IO.Step <= 50` stay asserted. Motor side: `COIL IO.TryRunMotor := (…) AND NOT IO.InhibitMotor AND NOT IO.FaultActive AND IO.SystemHealthy` merely de-asserts during the E-stop, and `COIL iDB_MotorFwdRevSystem_Shredder.IO.RecentStart := AlwaysTrue` re-arms the FB's start arm (`IO.TryRunMotor AND PreStartMemory AND IO.RecentStart`) every scan — overriding the FB's own self-clearing `COIL IO.RecentStart := IO.RecentStart AND NOT IO.RunFwd AND NOT IO.RunRev AND NOT IO.FaultActive` and its comment "Must Have Set Bit (#IO.RecentStart) On Auto/Hand Start Buttons Press/Release On HMI".
Notes: On E-stop reset, conveyors re-energize immediately and the shredder restarts with no fresh start command and **no pre-start siren** — the opposite of the REQ (which expects Press start → start-up procedure). Bounded scenario: an E-stop that drops `ControlHealthy` but not `DownstreamRunning`, with no operator stop and no latched motor fault; whether an incidental FTS latch (feedback-vs-healthy relay race) happens to abort first is timing-dependent — S9 item. The same design hole (RETAIN `Step`, no startup OB) produces auto-resume after a PLC power cycle — Defects D-6, Q-01.

### REQ-063 — Motor failed to run stops the system: partial
Where: motor N9/N11 → FC_ControlMain N1 → seq IO.MotorFaultActive → step exits; seq N8 (discharge).
Evidence: `TON(FaultTripTimer, IN := IO.RunFwd AND NOT IO.RunningFBFwd OR IO.RunRev AND NOT IO.RunningFBRev, PT := FTTimeMS)` → FTR → `IO.FaultActive` → `COIL iDB_ShredderSequencer.IO.MotorFaultActive := iDB_MotorFwdRevSystem_Shredder.IO.FaultActive` → `MOVE(EN := NOT StopCmd AND IO.MotorFaultActive AND IO.Step = 50, IN := 0) => IO.Step` (and at 30/40) — shredder failure stops the system ✓; discharge failure stops it via N8's timeout abort ✓.
Notes: Infeed conveyor and pusher power pack failures stop nothing (no supervision — REQ-057 note). The shredder's preset window is the unconfigured iDB `FTTime` = 0 (NEW-1), so as-committed the "preset time" is zero.

### REQ-064 — HMI shows selected machine type: unimplemented
Evidence: grep `K75|K100|K150|MachineType…` → `No matches found`. Depends on REQ-018 (Q-04).

### REQ-065 — Overall hour clock: partial
Where: motor N13 (imported-real, traced read-only).
Evidence: `TONR(HrTotaliserTimer, IN := IO.RunFwd AND IO.RunningFBFwd OR IO.RunRev AND IO.RunningFBRev, PT := T#1H, R := HrTotaliserTimerReset)`; `ADD(EN := HrTotaliserTimer.Q AND NOT RisingEdgeFlags[2] AND IO.HrsRun < 4294967295, IN1 := 1, IN2 := IO.HrsRun) => IO.HrsRun` — a running-hours value accumulates and is HMI-bindable.
Notes: The HMI presentation itself is outside this corpus (HMI design stage). No corpus consumer (expected for a display value).

### REQ-066 — Zeroable hour clock: unimplemented
Evidence: grep `Zeroable|HourClock` → `No matches found`; no resettable hours value or reset command exists (Q-12).

### REQ-067 — Changeover selection: unimplemented
Evidence: same no-match grep as REQ-018/064; no changeover tags (Q-04, two-vs-three-types disagreement carried).

### REQ-068 — Shredder state indications: partial
Where/Evidence: state values exist and are bindable: `MOVE(EN := IO.FaultActive, IN := -1) => IO.Telemetry` (and 0/1/2/3 levels, motor N14); `DB_Input.Shredder_Run_Fwd_FB`/`Shredder_Run_Rev_FB` (mapped); `IO.ShredderBlockedFault` (jammed).
Notes: Binding set confirmed at the HMI design stage per the register; corpus provides the values only.

### REQ-069 — In-cycle lamp: implemented
Where: FC_ControlMain N7 → DB_Output.In_Cycle → FC_Outputs N1 → DQ8.
Evidence: `COIL DB_Output.In_Cycle := DB_Input.Shredder_Run_Fwd_FB OR DB_Input.Shredder_Run_Rev_FB OR DB_Input.Discharge_Conv_Running OR DB_Input.Infeed_Conv_Running OR DB_Input.Pusher_PowerPack_Running` — exactly the five field feedbacks named by the owner ruling, not PLC run commands; `COIL DQ8_SYS_InCycle := AlwaysTrue AND DB_Output.In_Cycle AND NOT DB_Output.Test[0] OR AlwaysTrue AND DB_Output.Test[8]`. All five input hops verified through FC_Inputs. The superseded `IO.InCycle` member is dead but documented (see reverse pass).

**Sequence-paradigm check (C-113/C-118):** matches the register's classification table — FB_ShredderSequencer and FB_PusherControl are explicit stepped sequences in FBs with `Step : Int` inside the caller-visible IO UDT (headers state the paradigm + step legends); IO mapping, alarm annunciation, and the in-cycle lamp are combinational FCs. No mismatch.

**Tag-status check (anti-laundering):** no logic references any register-`proposed` tag (machine-type selection, remote cycle button, second-motor instrumentation, fault lamp, zeroable clock) — grep-verified zero hits. No pipeline breach.

## Unimplemented / disarmed / contradicted REQs

**Contradicted (4):**
- REQ-039 — jog release triggers a full powered auto-retract to home (pusher N6), not "stops where it is".
- REQ-043 — with `Fitted = FALSE` the ungated idle-not-home repark still drives the power pack output and runs switch-feedback supervision.
- REQ-044 — `ParkedTimeoutFault` latches and annunciates (`%X6`) with the pusher disabled.
- REQ-062 — no healthy input into the sequencer + `RecentStart := AlwaysTrue`: after a local E-stop (downstream still available), reset restarts the plant with no start command and no siren.

**Unimplemented (7):**
- REQ-005 — the shredder never runs in reverse: the imported FB's 8 s rising-edge pause fully covers the generated 6 s step-30 window (headline structural defect; also hollows REQ-027's jam-clearing retry).
- REQ-018, REQ-064, REQ-067 — no machine-type selection/changeover/display exists (Q-04).
- REQ-020 — one instrumented motor system only (Q-05).
- REQ-036 — no remote-cycle DI; `DI16` selector mapped but read by nothing (Q-03).
- REQ-066 — no zeroable hours value or reset (Q-12).

**Disarmed (11):** REQ-017, REQ-021, REQ-022, REQ-023, REQ-024, REQ-025, REQ-026, REQ-027, REQ-028, REQ-029, REQ-058 — the entire overcurrent family hangs off `IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue` (seq N12): the placeholder stands where the current-vs-setpoint comparison belongs, `IO.ShredderMotorCurrent` is read by nothing, the setpoints are read by nothing, and `DB_AnalogInput.ShredderMotorCurrent` has no writer (Q-11). Response/count/annunciation chains behind the gate are built and number-correct (4 s ✓, 5 ✓, 180 s ✓, 3 s ✓) but can never act. Disarmed is not implemented.

**Partial (18):** REQ-004 (discharge confirm window unconfigured — start sequence dead-ends at step 20 as-committed, Q-02); REQ-006 (8 s pause exists/correct but its after-reverse premise never occurs); REQ-007 (forward chain complete; iDB `FTTime`=0 latches FTR one scan into any start, NEW-1); REQ-012/REQ-013 (stop never reaches the pusher, NEW-2); REQ-019 (single unconfigured, unread setpoint pair; no per-type sets, Q-02/Q-04); REQ-031 (run-on = 0 s ⇒ pump stops during the hold, Q-02); REQ-032 (cycle logic complete; aborted instantly by REQ-054's zero timeout); REQ-045 (park action exists but unlinked to pre-start completion — unwarned movement, NEW-4); REQ-047 (resume delay unconfigured despite spec-stated 5 s, Q-02); REQ-054/REQ-055 (fault+bit complete; timeouts unconfigured ⇒ fire on every stroke, Q-02); REQ-057 (shredder+discharge identified; infeed/power-pack unsupervised, Q-05); REQ-059 (overload merged into `%X0`, Q-10); REQ-060 (count exists as bare Static; panel display/lamp proposed, Q-13); REQ-063 (system-stop for shredder+discharge only; zero preset as-committed); REQ-065/REQ-068 (values exist; HMI binding out of corpus).

## Unrequested logic (reverse pass)

- Soft-start fault-reset output: `COIL DB_Output.Motor_Fault_Reset := DB_Controls.FaultReset` → `DQ11_SHR_FaultReset` — FC_ControlMain N7 / FC_Outputs N2 — generated — no REQ defines this output (Q-14-adjacent); header justification **absent** [C-606] (FC_ControlMain has no header comment at all).
- Fail-to-stop annunciation: `COIL DB_Alarms.ShredderAlarm0.%X2 := iDB_MotorFwdRevSystem_Shredder.IO.FTS` — FC_AlarmsMain N3 — generated — no REQ asks for a failed-to-stop alarm; header justification **absent** [C-606].
- Pusher idle-not-home auto-repark (beyond REQ-045's pre-start-scoped ask, and active in every mode incl. disabled) — FB_PusherControl N6 — generated — justification present in the **network** comment, not the header [C-606 route note]; functional consequences filed at REQ-039/043/045.
- `IO.Cycling := IO.Step <> 0` — FB_PusherControl N13 — generated — unrequested HMI status member, no corpus consumer; header justification absent [C-606, minor].
- Dead interface member `UDT_ShredderSequencerIO.InCycle` — generated — written nowhere, read nowhere; superseded by the REQ-069 ruling and documented as a deferred removal in FC_ControlMain N7's comment (`justified-unrequested`; engineer decides).
- Imported-real capability carried disabled (documented context, not fix demands): hand mode (`InHand`/`HandStartSignal`/`HandIntervention`/`HandReverse` — `HandReverse` is never even read by any network), soft shutdown (`Shutdown` read but written nowhere; `ShutdownTime` = 0), `UPSEnable` upstream path (superseded by the sequencer's step-50 timer — deliberate per FC_ControlMain N11 comment; feeds only `Telemetry` level 3), `Telemetry`/`Alarm` word/`Name` (HMI surface, no corpus consumer; `Name` never written), `HrsRun` (serves REQ-065). Constant wiring `InHand/InhibitMotor/HandReverse := NOT AlwaysTrue` and `RecentStart := AlwaysTrue` at FC_ControlMain N3 carries **no comment stating why** — C-604 route note to review-conventions; the `RecentStart` case has functional teeth (REQ-062).
- Spare channels: `DB_Input.SpareDI[1..7]` written/never read, `DB_Output.SpareDQ[1..7]` read/never written — spare-by-design per the register inventory; `Test[]` force arrays and the mapping-rail `AlwaysTrue` are the admitted mapping-pattern shape (skipped per calibration). Legacy residue tags (`Tag_1`–`Tag_54`, comms words, `Clock_0.5Hz`, `FirstScan`) are referenced by no logic.

## Suspected functional defects outside any REQ

- **D-1 (chain-killer, filed under REQ-005):** generated 6 s reverse window vs imported 8 s rising-edge `Pasue` — reverse can never run; every "reversal" is a silent 8 s pause then forward start.
- **D-2:** `DB_Settings.OvercurrentSetpointMedium/High` are dead configuration — no reader anywhere (grep: declarations only). Even armed, the overcurrent feature has no comparison to consume them.
- **D-3:** `DB_AnalogInput.ShredderMotorCurrent` → `IO.ShredderMotorCurrent` is a doubly-dead chain: no writer at the buffer (no analog map FC — documented, Q-11) and no reader inside the FB (the consumed-but-never-written class the skill exists to catch; here it is at least header-documented).
- **D-4:** Pusher movement without pre-start warning: power-up/idle auto-repark (N6) and auto/manual cycle launches start the power pack and move the ram with no siren — only the jog path warns (REQ-003's "before any equipment starts" principle; REQ-030/045 context). NEW-4.
- **D-5:** `iDB_MotorFwdRevSystem_Shredder.IO.RecentStart` has two writers — FC_ControlMain N3 (`:= AlwaysTrue`, every scan) and motor FB N4 (self-clearing edge memory) — the FC pin defeats the imported FB's press-to-arm start semantics (its own comment requires an HMI-set bit). Contributes directly to REQ-062.
- **D-6:** No startup OB exists in the corpus (21 files, no OB100), and both sequencing UDTs are `RETAIN` — after a PLC power cycle mid-run, `Step` (e.g. 50) is retained and all run commands re-assert on the first scan: unwarned self-restart. C-124's mandated reset mechanism is absent; required plant behavior is Q-01 (carried, not answered).
- **D-7:** Discharge-timeout fault does not block a restart attempt (`N6` checks only `ShredderBlockedFault`), so with the zero timeout each start press repeats siren→instant-fault; and `%X8` re-latches (fault coil `… OR IO.DischargeConveyorTimeoutFault AND NOT IO.FaultReset`). Cosmetic once Q-02 numbers exist; noted for completeness.
- Route notes (other tiers): settings scan-copied over the pusher's faceplate-reachable UDT members at FC_ControlMain N5 (C-308 — review-conventions); missing block header comments on all generated FCs (C-201); `ReversalCount` outside the IO UDT (C-125); FTR/FTS present in both the motor's own `Alarm` word and `DB_Alarms` (C-503/C-504 — alarm-design tier); uncommented constants at FC_ControlMain N3 (C-604); redundant `AND IO.DownstreamRunning` inside seq N6 (C-601 — review-simplicity).

## Open questions (carried + newly raised)

- Q-01 carried — corpus today: RETAIN `Step`, no startup OB ⇒ power-cycle mid-run auto-resumes (D-6). Owner statement still needed.
- Q-02 carried — the nine unconfigured `DB_Settings` members are live zeros, not inert gaps: `DischargeConveyorTimeout`=0 kills the start sequence at step 20; `PusherEndTravelTimeout`/`PusherParkedTimeout`=0 fault every stroke; `PressureClearResumeDelay`=0 vs spec 5 s; `PusherPumpRunOnTime`=0 stops the pump mid-cycle; the four overcurrent members are moot while disarmed.
- Q-03 carried — `DI16_PSH_LocalRemote` mapped, read by nothing; no remote-cycle DI (REQ-036 unimplemented).
- Q-04 carried — no selection tags; REQ-018/064/067 unimplemented; setpoint pair machine-agnostic.
- Q-05 carried — one motor system; REQ-020 unimplemented, REQ-057 identification limited to shredder + discharge bits.
- Q-06 carried — corpus implements momentary press.
- Q-07 carried — corpus implements 3 s with the separate upstream output (FuncDesc reading).
- Q-08 carried — corpus does not auto-restart on downstream recovery; fresh `CycleStart` required.
- Q-09 carried — per-instance `ReverseDelay` = 8.0 confirmed as the operative tunable (chain verified to `ReversalPauseTimer.PT`) — but see REQ-005: its entry-pause semantics swallow the reverse window entirely.
- Q-10 carried — corpus merges overload into `%X0` "Fault/Overload Tripped".
- Q-11 carried — no AI hardware/map; buffer member unwritten; overcurrent cannot arm.
- Q-12 carried — nothing exists for the zeroable clock.
- Q-13 carried — `ReversalCount` exists (bare Static) for binding; no lamp/outside-display outputs.
- Q-14 carried — no control-on step exists; `DQ11_SHR_FaultReset` is wired directly from `DB_Controls.FaultReset` (unrequested-logic entry).
- Q-15 carried — corpus treats hand/jog as an overlay on Manual (Mode 2, Step 0), not a fourth mode.
- **NEW-1** — imported iDB start values `FTTime` = 0.0 and `ReverseIgnoreFT` = 0.0 (not in Q-02's DB_Settings scope): the fail-to-run/fail-to-stop confirm windows are zero, so any real start latches FTR one scan in. Owner numbers needed (and `EnableUPSTime`/`ShutdownTime` = 0.0 for the dormant paths, for completeness).
- **NEW-2** — should REQ-012/013's "stop all" stop/park the pusher (e.g. force Mode 0 like the step-60 path does)? As built it is exempt.
- **NEW-3** — which block owns the reverse-run duration? The sequencer's 6 s window and the motor's 8 s entry-pause are incompatible by construction; even with numbers reordered, actual reverse time = window − pause.
- **NEW-4** — are unwarned pusher movements (auto-repark at any time; auto/manual cycles without siren) acceptable, or must all pusher motion be pre-start-warned like jog?
- **NEW-5** — should jog be locked out while `Blocked` is latched (REQ-050's "locked out from activating")?
- **NEW-6** — auto mode re-launches cycles continuously while the hopper stays high; the source's "perform 1 cycle" wording may intend a one-shot per high-level event.

## Not statically checkable (S9 sim-test candidates, keyed by REQ ID)

No REQ carried the `not-statically-checkable` verdict — every verdict above was reached by static trace. Sim candidates to *confirm* derived timelines and cover residual races:

- REQ-005/006/007: drive the full start sequence (with configured numbers) and observe DQ2 — the derived result is that reverse never energizes and forward starts at T0+8 s; confirm, and confirm FTR latching with `FTTime` = 0 vs a realistic value.
- REQ-062: E-stop recovery matrix — {downstream held/lost} × {feedback-vs-healthy relay drop order}: confirm auto-restart in the held case and whether the incidental FTS race ever aborts it.
- REQ-028: reversal-window boundary — 5 events straddling a `ReversalWindowTimer` reset must (per the REQ) trip but (per the build) will not.
- REQ-012/013: stop press with the pusher mid-cycle — confirm power pack/solenoids continue to cycle completion.
- REQ-043/044: `PusherFitted` = FALSE with no pusher wired — confirm DQ3 energizes on first scan and `%X6` annunciates.
- REQ-047: pressure hold → clear with the resume delay configured — confirm resume from position and that held time consuming `EndTravelTimer` (REQ-054 note) behaves acceptably.

**Bottom line:** of 69 REQs — 28 implemented, 18 partial, 11 disarmed, 7 unimplemented, 4 contradicted, 1 out-of-scope confirmed. The corpus's wiring discipline is largely sound (buffer chains, number chains, and alarm chains verified hop-by-hop; all timer PT pairings index-correct), but the build as committed cannot start (Q-02/NEW-1 zeros), can never reverse the shredder (REQ-005 structural), leaves the pusher outside the stop command, inverts two disabled-pusher guarantees, and does not enforce the no-auto-restart rule after E-stop or power cycle. One functional miss discredits the pipeline, not just the block — and this review found several, all statically traceable.

