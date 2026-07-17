# GenProject1 — Requirements Register

The numbered functional-requirements register for GenProject1 (the Kestrel Shredder Systems K150
demo panel build). Produced by `docs/15-generation-pipeline.md`'s `gen-spec-analysis` stage
(performed manually — the skill does not exist yet). This is the input artifact for the
`review-functional` check stage and the eventual S9 sim tests; REQ IDs below are stable forever.

## Provenance

- **Produced:** 2026-07-16, `manual:gen-spec-analysis` — **retroactive**: the GenProject1 corpus
  (`ir/GenProject1/`, built 2026-07-15) predates this register. See the method note below for what
  that means and how contamination was avoided.
- **Primary source:** the supplied functional description (`FuncDesc.docx` — local-only,
  gitignored per `docs/13-data-boundary.md`'s 2026-07-15 GenProject1 entry). Identity anchors so a
  later run can prove it read the same document: sha256 prefix `91a1184d00a5af02`, file date
  2026-03-19, 22,071 bytes. Text extracted in-memory (unzip + de-tag); never written to disk.
- **Supporting source:** the supplied specification sheet (`SpecSheet.xlsx` — local-only,
  gitignored, same entry; file date 2026-01-12). Used for its project-specific option rows, the
  "typical panel sequence" narrative, and equipment/IO vocabulary. Where the two sources disagree,
  the functional description (project-specific, newer) is primary and the disagreement is an open
  question, never silently resolved.
- **Genericization:** every company, model-line, and identifying name in this file is an invented
  name from the project's local (gitignored, never-committed) sanitization map, per the
  data-boundary entry above. This file was scanned against every real-name key in that map before
  commit: zero hits. No real identifying name, job number, part number, or model code appears here.
- **Tag-status verification corpus:** `ir/GenProject1/` as of commit `19b2022` (the last commit
  touching it at write time). Every `exists` mark below was grep-verified against that corpus when
  this register was written, per docs/15's anti-laundering rule.
- **Method note (retroactive-run honesty):** register content — the requirement texts, classes,
  and open questions — is derived ONLY from the two source documents and the recorded owner
  rulings cited per-REQ. The corpus was consulted solely for tag-status marks (which tags/DB
  members/annunciation bits exist) and for the verbatim text of the two recorded owner rulings
  that live as corpus comments (cited at REQ-069 and in the inventory). No REQ was written by
  enumerating corpus features and back-filling requirement text — the reverse (corpus→register)
  pass belongs to `review-functional`, and it stays meaningful only if this register is
  source-derived. Declared plainly: the authoring session had read the corpus; the register's
  defense against circularity is this sourcing discipline plus the per-REQ citations, each of
  which points at source text, not at code.

## Format

This is the defining instance of the `requirements.md` format (docs/15 build-order step 4). The
contract migrates into the `gen-spec-analysis` skill when that is built (build-order step 6) —
until then, this section is the format's home. **Coordination handoff:** docs/15's build-order
table row 4 ("`review-functional` + `requirements.md` format definition") flips to built with
this artifact; that edit belongs to the pipeline doc's owner, not this artifact.

- **IDs:** `REQ-nnn`, assigned in source order, **stable forever** — S9 sim tests trace to them.
  Never renumbered, never reused. A withdrawn requirement keeps its ID and gains
  `Status: withdrawn` (with the dated reason); it never disappears.
- **Classes** (one per REQ): `control` (sequencing/interlocks/drives), `alarm`
  (fault-annunciation causes — texts/severities belong to the alarm-design stage), `HMI`
  (operator commands/indications), `mode` (auto/manual/off/hand, selection semantics), `timing`
  (named delays/setpoints as the ask itself), `out-of-scope` (hardwired/safety material recorded
  so nobody expects PLC logic for it). Sequence steps with stated durations are `control` with
  the number captured in the text; the functional review checks the number chain on any REQ whose
  text states one.
- **Sources:** every REQ cites its source. FuncDesc is cited by section heading + leading words
  (`FuncDesc §"<heading>" — "<leading words>"`) because Word auto-numbering does not survive text
  extraction. SpecSheet is cited by section label + leading words (cell coordinates are not
  preserved by the extraction). Owner rulings are cited with their date and, where applicable,
  where the ruling is recorded.
- **WHAT, never HOW:** the register records what was asked, never how it was or will be built —
  no block/network content, no logic shapes. Tag-status marks are the one sanctioned contact with
  the corpus.
- **Tag status:** every tag or DB member named in this register is marked `exists`
  (grep-verified in `ir/GenProject1/` at write time) or `proposed` (a named gap; the engineer
  creates tags — CLAUDE.md hard rule 3). Nothing may be coded against a `proposed` tag.
- **Open questions:** `Q-nn`, in their own section. Any pipeline stage may append; none may
  silently resolve one — resolution is a recorded owner answer, noted at the question.

## Requirements

### Start sequence

### REQ-001 — Start command
- **Text:** The plant start sequence is commanded by pressing the start pushbutton.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Press Start button"
- **Notes:** `DI3_SYS_CycleStart` exists ("Cycle start pushbutton (local/remote)"). SpecSheet's
  typical sequence says "Press and hold Start Button" — momentary vs hold resolved as momentary,
  see Q-06.

### REQ-002 — Downstream confirmed before start
- **Text:** The start sequence proceeds only with downstream confirmed running / available.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Confirm downstream Running / Available"
- **Notes:** `DI15_SYS_DownstreamRunning` exists ("Downstream equipment running").

### REQ-003 — Pre-start siren
- **Text:** The pre-start siren sounds for 10 s before any equipment starts.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Sound pre-start siren (10s)"
- **Notes:** `DQ10_SYS_PreStartSounder` exists. Setting `DB_Settings.PreStartSounderTime` exists,
  start value 10.0 (matches).

### REQ-004 — Discharge conveyor first, confirmed
- **Text:** The discharge conveyor starts first and its running feedback is confirmed before the
  sequence continues.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Start discharge conveyor & confirm running"
- **Notes:** `DQ5_DIS_Run`, `DI10_DIS_Running` exist. Setting `DB_Settings.DischargeConveyorTimeout`
  exists but is unconfigured (no start value) — Q-02. The confirm-window duration is not stated in
  either source.

### REQ-005 — Shredder reverse run at start
- **Text:** The shredder starts in reverse and runs for 6 s.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Start shredder reverse, run for 6s"
- **Notes:** `DQ2_SHR_RunRev`, `DI6_SHR_RunRevFB` exist. Setting
  `DB_Settings.ShredderReverseRunTime` exists, start value 6.0 (matches).

### REQ-006 — Spin-down pause after reverse
- **Text:** The shredder stops after the reverse run and pauses for 8 s (motor spin-down) before
  the forward start.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Stop shredder, pause for 8s"; SpecSheet §"Typical
  panel sequence" — "Pauses for pre-set time to allow for motor spin down"
- **Notes:** No `DB_Settings` member exists for this pause. A per-instance member `ReverseDelay`
  with start value 8.0 exists on the shredder motor instance (grep-verified) — name/value match
  suggests that is its tunable home (C-307 per-instance scope), but confirming the chain is the
  functional review's job, not this register's. Q-09.

### REQ-007 — Shredder forward start
- **Text:** The shredder starts in the forward direction after the spin-down pause.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Start shredder forward"
- **Notes:** `DQ1_SHR_RunFwd`, `DI5_SHR_RunFwdFB` exist.

### REQ-008 — Hopper-not-high gate before infeed
- **Text:** The sequence confirms the shredder hopper level is not high before starting the infeed.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Confirm shredder hopper level is not high level"
- **Notes:** `DI8_HPR_LevelHigh` exists ("Hopper high level sensor").

### REQ-009 — Delay after hopper-clear confirm
- **Text:** A 5 s delay follows the hopper-not-high confirmation before the infeed conveyor starts.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Time delay 5s"
- **Notes:** Setting `DB_Settings.InfeedRestartDelay` exists, start value 5.0 (matches). The same
  5 s appears in the hopper-recovery flow (REQ-015) — one number in the source, presumed one
  setting.

### REQ-010 — Infeed conveyor start
- **Text:** The infeed conveyor starts.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "Start infeed conveyor"
- **Notes:** `DQ4_IFC_Run` exists; running feedback `DI9_IFC_Running` exists.

### REQ-011 — Upstream enable after infeed
- **Text:** Upstream is enabled 3 s after the infeed conveyor starts.
- **Class:** control
- **Source:** FuncDesc §"Start sequence" — "3s delay" / "Enable Upstream"
- **Notes:** `DQ9_SYS_EnableUpstream` exists ("volt-free"). Setting
  `DB_Settings.InfeedToUpstreamEnableDelay` exists, start value 3.0 (matches). SpecSheet's typical
  sequence says "After 6 secs Up-stream signal activated" and treats the upstream signal as the
  infeed/loading light itself — Q-07.

### Stop sequence

### REQ-012 — Stop pushbutton stops all
- **Text:** Pressing the stop pushbutton stops all equipment at the same time.
- **Class:** control
- **Source:** FuncDesc §"Stop Sequence" — "(push button or loss of downstream available) / Stop
  all at same time"
- **Notes:** `DI4_SYS_CycleStop` exists; its tag comment records the field device as a
  normally-closed pushbutton ("Cycle stop pushbutton (NC)") — the register records the field fact;
  polarity handling is implementation. SpecSheet §"Shut-down" corroborates ("Press Stop Button /
  Complete System comes to stop").

### REQ-013 — Downstream loss stops all
- **Text:** Loss of downstream available stops all equipment at the same time.
- **Class:** control
- **Source:** FuncDesc §"Stop Sequence" — "(push button or loss of downstream available)"
- **Notes:** `DI15_SYS_DownstreamRunning` exists. SpecSheet's typical "Downstream Signal Blockage"
  narrative differs (upstream-signal stop, optional full shutdown, auto/manual restart per
  set-up) — FuncDesc primary; restart-on-recovery behavior is Q-08.

### Shredder hopper high level control

### REQ-014 — Hopper high stops infeed only
- **Text:** When the shredder hopper is at high level, the infeed conveyor stops. The shredder
  itself keeps running.
- **Class:** control
- **Source:** FuncDesc §"Shredder Hopper High Level Control" — "If shredder hopper is high level.
  Stop infeed conveyor"; also FuncDesc §"Sequence:" (pusher) — "When hopper is high level, stop
  infeed conveyor and perform 1 cycle of the pusher"
- **Notes:** Shredder-keeps-running is corroborated by SpecSheet §"Hopper Level Sensors" —
  "Shredder continues to run until blockage is cleared". `DI8_HPR_LevelHigh` exists.

### REQ-015 — Infeed restart on hopper clear
- **Text:** After the hopper is no longer at high level, plus a 5 s delay, the infeed conveyor
  restarts.
- **Class:** control
- **Source:** FuncDesc §"Shredder Hopper High Level Control" — "Wait until hopper is not high
  level + Delay 5s / Start infeed conveyor"
- **Notes:** Same 5 s as REQ-009; `DB_Settings.InfeedRestartDelay` exists = 5.0.

### REQ-016 — Upstream re-enable after infeed restart
- **Text:** Upstream is re-enabled 3 s after the infeed restarts.
- **Class:** control
- **Source:** FuncDesc §"Shredder Hopper High Level Control" — "3s delay / Enable Upstream"
- **Notes:** Same setting as REQ-011 (`InfeedToUpstreamEnableDelay` = 3.0).

### Shredder overcurrent control

### REQ-017 — Overcurrent event definition
- **Text:** An overcurrent event is triggered when the motor current feedback is in excess of the
  setpoint for a time period. Overcurrent is distinct from overload.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "An overcurrent event is triggered when
  the current feedback is in excess of the setpoint for a time period"; "Note: Overcurrent is
  different than overload"
- **Notes:** Buffer member `DB_AnalogInput.ShredderMotorCurrent` exists; **no physical analog
  input channel exists in the as-built export** (the buffer DB's own header records the owner's
  genuine-analog-current decision and the missing AI hardware) — Q-11. Overload annunciation is
  REQ-059/Q-10.

### REQ-018 — Machine-type selection in engineering
- **Text:** An engineering selection chooses the machine type: K75, K100, or K150.
- **Class:** mode
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "In engineering add a selection for K75,
  K100 & K150 shredders" (model codes genericized)
- **Notes:** **proposed** — no machine-type selection tag or DB member exists in the corpus
  (grep-verified). Q-04. SpecSheet's changeover row (REQ-067) names only two types — also Q-04.

### REQ-019 — Overcurrent setpoints per machine type
- **Text:** Overcurrent setpoints exist per machine type (one set for each of the three types).
- **Class:** timing
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "Have overcurrent setpoints for each
  machine type"
- **Notes:** `DB_Settings.OvercurrentSetpointMedium` / `OvercurrentSetpointHigh` exist but as a
  **single pair**, both unconfigured (no start values); no per-machine structure exists —
  **proposed** (per-machine sets), Q-02/Q-04.

### REQ-020 — Overcurrent on either motor
- **Text:** An overcurrent can occur on either motor and must be detected on both.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "An overcurrent can occur on either motor"
- **Notes:** **proposed** — the as-built panel instruments one motor system: one current buffer
  member, one combined fault input (`DI7_SYS_MotorFault`), one fwd/rev feedback pair. Per-motor
  detection has no tags. Q-05.

### REQ-021 — Medium overcurrent level
- **Text:** Medium-level overcurrent: current a little above the setpoint, with a longer delay
  before triggering a reversal.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "Medium level – Overcurrent a little
  above the setpoint, longer delay before triggering a reversal"
- **Notes:** Setting `DB_Settings.OvercurrentMediumDelay` exists, unconfigured (no spec number
  either) — Q-02.

### REQ-022 — High overcurrent level
- **Text:** High-level overcurrent: current considerably above the setpoint, with a shorter delay
  before triggering a reversal.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "High Level – Overcurrent considerably
  above setpoint shorter delay before triggering a reversal"
- **Notes:** Setting `DB_Settings.OvercurrentHighDelay` exists, unconfigured (no spec number
  either) — Q-02.

### REQ-023 — Overcurrent stops the shredder
- **Text:** When the shredder is running forward and an overcurrent event occurs, the shredder
  stops.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "the shredder should stop"
- **Notes:** —

### REQ-024 — Overcurrent stops the infeed
- **Text:** On the same overcurrent event, the infeed stops.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "the infeed should stop"
- **Notes:** —

### REQ-025 — Overcurrent parks the pusher
- **Text:** On the same overcurrent event, the pusher returns to parked.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "the pusher should return to parked"
- **Notes:** —

### REQ-026 — Post-overcurrent pause
- **Text:** After the overcurrent stop, pause for 4 s.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "Pause for 4s"
- **Notes:** Setting `DB_Settings.ReversalRetryPauseTime` exists, start value 4.0 (matches).

### REQ-027 — Auto-resume via reverse run
- **Text:** After the pause, the startup sequence resumes from the reverse-run step (REQ-005) —
  i.e. the shredder reverses to clear the jam, then restarts forward through the normal sequence.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "Resume the shredder startup sequence
  from point 5 above" (the source's own numbering; its point 5 is the reverse-run step)
- **Notes:** SpecSheet §"Reversals & Stoppages" corroborates the reverse-to-unblock reading.

### REQ-028 — Reversal-count trip
- **Text:** If 5 overcurrent events occur within 3 minutes, the system stops and an alarm is
  raised.
- **Class:** control
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "If 5 overcurrent events occur in 3
  minutes, stop the system and alarm"
- **Notes:** Settings `DB_Settings.ReversalCountThreshold` (= 5) and `ReversalWindowTime`
  (= 180.0) exist (match). The annunciation is REQ-058. SpecSheet corroborates ("If shredder
  reverses 5 times within 3 minutes Trip fault occurs"). **Window semantics RESOLVED (owner
  ruling, 2026-07-17 — owner-questions B-5):** re-arming, not fixed-from-first. The count only
  clears after the plant runs **clean for the full 180 s with no new reversal** — every reversal
  restarts the 180 s clock; the count keeps accumulating across any run of reversals each less
  than 180 s apart, however long that run lasts in total. `ReversalWindowTime` stays the tunable
  span (adjustable, per C-307). Fix wave 1's implementation used the wrong semantics (one fixed
  180 s window from the first reversal only) — a genuine functional defect. **Fixed and compiled
  clean 2026-07-17** (`agent-tasks/06-reversal-window-rearm-fix.md`) — see
  `gen/GenProject1/fix-wave-1-reviews.md` B-5 for the built shape.

### REQ-029 — Spin-up overcurrent suppression
- **Text:** Overcurrent is ignored for approximately 2–3 s at shredder startup so the motor can
  get up to speed.
- **Class:** timing
- **Source:** FuncDesc §"Shredder Overcurrent Control" — "Ignore overcurrent on startup for
  approx. 2-3s to allow shredder to get up to speed"
- **Notes:** Setting `DB_Settings.OvercurrentSpinUpAllowance` exists, start value 3.0 (within the
  stated range).

### Shredder pusher

### REQ-030 — Power pack before and during movement
- **Text:** Any movement of the pusher requires the hydraulic power pack to be running prior to
  and during operation of the extend or retract solenoids.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "Any movement of the pusher requires the hydraulic
  power pack to be running prior to and during operation"
- **Notes:** `DQ3_PSH_RunPowerPack`, `DI11_PSH_PowerPackRunning` exist.

### REQ-031 — Power pack run-on
- **Text:** The power pack has a run-on so the hydraulic motor does not stop when transitioning
  from push to retract.
- **Class:** timing
- **Source:** FuncDesc §"Shredder Pusher" — "Have a run on so the Hyd motor doesn't stop when
  transitioning from push to retract"
- **Notes:** Setting `DB_Settings.PusherPumpRunOnTime` exists, unconfigured (no spec number
  either) — Q-02.

### REQ-032 — Pusher cycle definition
- **Text:** One pusher cycle: start from parked, transition to the end-travel limit, hold a short
  pause of approximately 2 s, then retract to parked.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "One pusher cycle is starting from the parked
  position, transition to the end limit position. Hold a short pause of approx. 2 sec and then
  retract to the parked position"
- **Notes:** `DI12_PSH_HomeLimit`, `DI13_PSH_FullTravelLimit`, `DQ6_PSH_Extend`,
  `DQ7_PSH_Retract` exist. Setting `DB_Settings.PusherEndTravelHoldTime` exists, start value 2.0
  (matches).

### REQ-033 — Three pusher modes
- **Text:** The pusher has three modes of operation: off, automatic, manual.
- **Class:** mode
- **Source:** FuncDesc §"Shredder Pusher" — "3 modes of operation – off, automatic, manual"
- **Notes:** `DB_Controls.PusherMode : Int` exists. The separately-requested hand/jog control
  (REQ-038) is not named as a fourth mode in the source — Q-15.

### REQ-034 — Auto mode: cycle on hopper high
- **Text:** In automatic mode, a pusher cycle is triggered by hopper high level.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "In Auto a pusher cycle is triggered by hopper high
  level"; also FuncDesc §"Sequence:" — "When hopper is high level, stop infeed conveyor and
  perform 1 cycle of the pusher"
- **Notes:** `DI8_HPR_LevelHigh` exists.

### REQ-035 — Manual mode: HMI cycle button
- **Text:** In manual mode, the operator can trigger a pusher cycle with a manual cycle button on
  the HMI.
- **Class:** HMI
- **Source:** FuncDesc §"Shredder Pusher" — "In Manual, the operator can press a manual cycle
  button on the HMI"
- **Notes:** `DB_Controls.PusherManualCycleCmd` exists.

### REQ-036 — Manual mode: remote pushbutton
- **Text:** In manual mode, a pusher cycle can also be triggered by a remote-mounted pushbutton
  via a digital input.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "or a remote mounted push button via Digital input"
- **Notes:** **proposed** — no digital input tag for a remote pusher cycle button exists. The
  panel's `DI16_PSH_LocalRemote` selector exists but is a selector, not a cycle button, and no
  source text defines it — Q-03.

### REQ-037 — Off mode: retract to home
- **Text:** When the pusher mode is off, the pusher retracts to the home (parked) position.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "When off, retract the pusher to home position"
- **Notes:** —

### REQ-038 — Hand control: jog per direction
- **Text:** Hand control of the pusher exists for testing, with a jog button for each direction.
- **Class:** mode
- **Source:** FuncDesc §"Shredder Pusher" — "New request from site is to have hand control of
  the pusher for testing. In hand, have a jog button for each direction"
- **Notes:** `DB_Controls.PusherJogExtendCmd`, `DB_Controls.PusherJogRetractCmd` exist. Relation
  to the three modes (REQ-033) is Q-15.

### REQ-039 — Jog is hold-to-move
- **Text:** The operator must hold the jog button to move the pusher; on release it stops where
  it is.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "The operator should have to hold the button to
  transition the pusher, on release it stops where it is"
- **Notes:** —

### REQ-040 — Jog independent of shredder
- **Text:** Hand jogging can be done with or without the shredder running.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "This can be done with or without the shredder
  running"
- **Notes:** —

### REQ-041 — Jog pre-start warning
- **Text:** The pre-start siren sounds for 1 s before jog movement begins and continues to sound
  while the pusher is transitioning.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "Sound the pre-start for 1s before moving and
  continue to sound while transitioning"
- **Notes:** `DQ10_SYS_PreStartSounder` exists. Setting `DB_Settings.PusherJogWarningTime`
  exists, start value 1.0 (matches).

### REQ-042 — Engineering pusher enable/disable
- **Text:** An engineering control enables or disables the pusher entirely (for test machines
  with no pusher installed).
- **Class:** mode
- **Source:** FuncDesc §"Shredder Pusher" — "In engineering have a push button to enable or
  disable pusher"
- **Notes:** `DB_Settings.PusherFitted : Bool` exists, start value TRUE.

### REQ-043 — Disabled pusher: no outputs, no feedback supervision
- **Text:** With the pusher disabled, the system never attempts to run the power pack or drive
  the solenoids, and does not look for pusher switch feedback.
- **Class:** control
- **Source:** FuncDesc §"Shredder Pusher" — "Disabling it will ensure that if a pusher is not
  installed on the test machine that it will not attempt to run the power pack or look for switch
  feedback"
- **Notes:** —

### REQ-044 — Disabled pusher: no pusher faults
- **Text:** With the pusher disabled, no pusher faults are raised or visible on the screen.
- **Class:** alarm
- **Source:** FuncDesc §"Shredder Pusher" — "Ie no pusher faults should be visible on the screen"
- **Notes:** —

### Pusher sequence

### REQ-045 — Park after pre-start
- **Text:** When the pre-start (siren) phase completes, if the pusher is not at the parked
  position, it transitions to parked.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "When pre-start is complete, if pusher is not at parked
  position, transition to parked"
- **Notes:** `DI12_PSH_HomeLimit` exists.

### REQ-046 — High-pressure hold while pushing
- **Text:** If the pusher high-pressure switch activates for more than 0.5 s while pushing, stop
  pushing and hold position.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "If pusher high pressure switch activates for >0.5s while
  pushing: / Stop pushing and hold position"
- **Notes:** `DI14_PSH_HighPressure` exists. Setting `DB_Settings.PressureTripConfirmTime`
  exists, start value 0.5 (matches).

### REQ-047 — Resume push after pressure clears
- **Text:** When the pressure is no longer high, plus a 5 s delay, pushing restarts from the
  existing position and the cycle continues.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "When pressure is not high level + 5s time delay, restart
  pushing from existing position and continue sequence"
- **Notes:** Setting `DB_Settings.PressureClearResumeDelay` exists but is **unconfigured despite
  the source stating 5 s** — Q-02 (the one unconfigured member with a spec-stated number).

### REQ-048 — Pressure-trip count: return and fault
- **Text:** If high pressure activates 5 times on the same cycle, the pusher returns to parked
  and a pusher-blocked fault is raised.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "If high pressure activates 5 times on the same cycle,
  return to parked and activate a pusher blocked fault"
- **Notes:** Setting `DB_Settings.PressureTripCountThreshold` exists, start value 5 (matches).
  Annunciation is REQ-053.

### REQ-049 — Blocked fault spares the shredder
- **Text:** The pusher-blocked fault does not stop the shredder.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "Do not stop the shredder"
- **Notes:** —

### REQ-050 — Blocked lockout until reset
- **Text:** After a pusher-blocked fault, the pusher is locked out from activating until the
  fault has been reset.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "Lock out the pusher from activating until the fault has
  been reset"
- **Notes:** `DB_Controls.FaultReset` exists (operator reset command).

### REQ-051 — Pressure ignored on retract
- **Text:** The pressure switch is ignored on the retract stroke.
- **Class:** control
- **Source:** FuncDesc §"Sequence:" — "Ignore pressure switch on the retract stroke"
- **Notes:** —

### Pusher faults

### REQ-052 — Fault: both pusher switches
- **Text:** Both pusher limit switches on at the same time is a pusher fault, annunciated on the
  display.
- **Class:** alarm
- **Source:** FuncDesc §"Pusher Faults:" — "Both pusher switches on at the same time"; also
  §"Faults to include..." — same wording
- **Notes:** An annunciation bit for this exists in the corpus alarm word (grep-verified). Alarm
  text/severity design belongs to the alarm-design stage.

### REQ-053 — Fault: pusher blocked
- **Text:** Too many pusher high pressures in one cycle (pusher blocked) is a fault, annunciated
  on the display.
- **Class:** alarm
- **Source:** FuncDesc §"Pusher Faults:" — "Too many pusher high pressures in one cycle (pusher
  blocked)"; also §"Faults to include..." — same wording
- **Notes:** Annunciation bit exists (grep-verified). Cause behavior is REQ-048.

### REQ-054 — Fault: end-travel timeout
- **Text:** The pusher taking too long to reach the end-travel switch is a fault, annunciated on
  the display.
- **Class:** alarm
- **Source:** FuncDesc §"Pusher Faults:" — "Pusher taking too long to reach parked or end
  switch"; also §"Faults to include..." — "Pusher taking too long to reach end switch"
- **Notes:** Annunciation bit exists (grep-verified). Setting `DB_Settings.PusherEndTravelTimeout`
  exists, unconfigured — Q-02.

### REQ-055 — Fault: parked timeout
- **Text:** The pusher taking too long to reach the parked switch is a fault.
- **Class:** alarm
- **Source:** FuncDesc §"Pusher Faults:" — "Pusher taking too long to reach parked or end switch"
- **Notes:** The display-faults list (§"Faults to include...") names only the end switch; the
  Pusher Faults list names both — recorded as stated, both faults required. Annunciation bit
  exists (grep-verified). Setting `DB_Settings.PusherParkedTimeout` exists, unconfigured — Q-02.

### Faults on the text display

### REQ-056 — Fault: motor tripped input
- **Text:** Motor fault (tripped input) is annunciated on the text display.
- **Class:** alarm
- **Source:** FuncDesc §"Faults to include and add on the text display" — "Motor Fault – Tripped
  Input"
- **Notes:** `DI7_SYS_MotorFault` exists ("Motor fault (soft start fault relay)"); an
  annunciation bit exists (grep-verified). See REQ-059/Q-10 for the overload relationship.

### REQ-057 — Fault: motor failed to run, identified
- **Text:** Motor failed to run is annunciated, identifying which motor.
- **Class:** alarm
- **Source:** FuncDesc §"Faults to include..." — "Motor failed to run – Identify which motor"
- **Notes:** A failed-to-run annunciation bit exists (grep-verified). "Identify which motor" is
  **proposed** — the as-built panel instruments one motor system (Q-05). System consequence of
  this fault is REQ-063.

### REQ-058 — Fault: shredder blocked
- **Text:** Too many shredder reversals (shredder blocked) is annunciated on the text display.
- **Class:** alarm
- **Source:** FuncDesc §"Faults to include..." — "Too many shredder reversals – Shredder blocked"
- **Notes:** Annunciation bit exists (grep-verified). Cause behavior is REQ-028.

### REQ-059 — Fault: overload
- **Text:** Overload faults are annunciated on the text display. (Overload is distinct from
  overcurrent — REQ-017.)
- **Class:** alarm
- **Source:** FuncDesc §"Faults to include..." — "Overload faults"; §"Shredder Overcurrent
  Control" — "Note: Overcurrent is different than overload"
- **Notes:** The panel's single `DI7_SYS_MotorFault` input is commented as the soft-start fault
  relay; whether a separate overload annunciation is distinguishable from REQ-056's tripped-input
  fault on this hardware is Q-10.

### SpecSheet-sourced requirements

### REQ-060 — Reversal counter display
- **Text:** A reversal counter display is visible inside the panel and on the outside of the
  panel, with a flashing motor fault light.
- **Class:** HMI
- **Source:** SpecSheet §"Reversals & Stoppages" — "A reversal counter display should be visible
  inside the panel and on the outside of the panel with flashing motor fault light"
- **Notes:** A reversal-count value exists in the corpus (sequencer interface member
  `ReversalCount`, grep-verified) for display binding. The outside-panel display and flashing
  fault lamp are panel hardware — no lamp output tag exists (**proposed** if PLC-driven) — Q-13.

### REQ-061 — E-stop stops everything (hardwired)
- **Text:** In an E-stop situation, everything stops. This is the hardwired safety circuit's
  function.
- **Class:** out-of-scope
- **Source:** SpecSheet §"E/Stop situation:" — "Everything stops"; SpecSheet safety-circuit
  conditions rows
- **Notes:** Hard rule 2 / hardwired safety circuit, outside PLC logic scope — recorded so no
  stage expects PLC logic for the stop function itself. The PLC-visible fact is the healthy
  feedback `DI1_SYS_ControlHealthy` (exists). Safety-circuit internals are not elaborated here by
  policy.

### REQ-062 — No auto-restart after E-stop recovery
- **Text:** After E-stop recovery (all E-stops reset), the plant does not restart by itself — a
  fresh start command is required, upon which the start-up procedure runs.
- **Class:** control
- **Source:** SpecSheet §"E/Stop situation:" — "Reset all e-stops / Press start / Start-up
  procedure occurs"
- **Notes:** `DI1_SYS_ControlHealthy`, `DI3_SYS_CycleStart` exist.

### REQ-063 — Motor failed to run stops the system
- **Text:** If any motor fails to run after a preset time, the fault sequence occurs and the
  shredder system stops completely.
- **Class:** control
- **Source:** SpecSheet §"Additional functions / Motor Failed To Run Signal:" — "If any motor
  fails to run after pre-set time, fault sequence occurs / Shredder system stops completely"
- **Notes:** Annunciation is REQ-057. The confirm windows are the unconfigured timeout settings
  (Q-02).

### REQ-064 — HMI shows selected machine type
- **Text:** The HMI shows which machine type is selected (K75/K100/K150).
- **Class:** HMI
- **Source:** SpecSheet project option row — "This should show which machine is selected
  (K75/K100/K150)..." (model codes genericized)
- **Notes:** **proposed** — depends on REQ-018's selection existing; no tags (Q-04).

### REQ-065 — Overall hour clock
- **Text:** The HMI has an overall hour clock (machine running-hours total).
- **Class:** HMI
- **Source:** SpecSheet project option row — "...have a over all hour clock..."
- **Notes:** A running-hours value exists in the corpus (motor system interface member `HrsRun`,
  grep-verified) as a display-binding candidate.

### REQ-066 — Zeroable hour clock
- **Text:** The HMI also has a zeroable clock for different applications (resettable job/trial
  hours).
- **Class:** HMI
- **Source:** SpecSheet project option row — "...but also a zeroable clock for different
  applications"
- **Notes:** **proposed** — no zeroable/resettable hours value or reset command exists in the
  corpus (grep-verified) — Q-12.

### REQ-067 — Changeover selection
- **Text:** The control panel has a switchable changeover to run K75 or K100 machines.
- **Class:** mode
- **Source:** SpecSheet project option row — "Control panel should have switchable change over to
  run K75, K100" (model codes genericized)
- **Notes:** **proposed** — same gap as REQ-018; the two sources disagree on two vs three types
  (Q-04).

### REQ-068 — Shredder state indications
- **Text:** The HMI indicates shredder state: jammed, running in reverse, running forwards.
- **Class:** HMI
- **Source:** SpecSheet §"HMI Screen Layout Example" — "Shredder Jammed / Shredder Running in
  Reverse / Shredder Running Forwards"
- **Notes:** Source is an example layout — binding set to be confirmed at the HMI design stage.
  State values exist in the corpus (running feedbacks `DI5`/`DI6` via buffers; a blocked fault
  member — grep-verified).

### Owner rulings

### REQ-069 — In-cycle lamp
- **Text:** The machine in-cycle lamp is on whenever any piece of equipment is running — taken
  from field running feedbacks (shredder forward/reverse, discharge conveyor, infeed conveyor,
  pusher power pack), not from PLC run commands, so the lamp reflects what the machine is
  actually doing.
- **Class:** HMI
- **Source:** Owner ruling, 2026-07-16 (recorded in the corpus at `FC_ControlMain` network 7's
  comment). Neither source document defines the in-cycle lamp's semantics; the panel provides the
  output.
- **Notes:** `DQ8_SYS_InCycle` exists ("Machine in-cycle status"). The five feedback inputs exist:
  `DI5_SHR_RunFwdFB`, `DI6_SHR_RunRevFB`, `DI10_DIS_Running`, `DI9_IFC_Running`,
  `DI11_PSH_PowerPackRunning`.

## Sequence classification (C-113 memory test)

Per docs/06 C-113, each sequence in the ask is classified by the memory test — *does the logic
need to remember what phase it's in to know what to do next?* — with the paradigm that follows.
This is a classification of the requirement, not a design.

| Sequence | Memory test | Paradigm |
|---|---|---|
| Shredder plant cycle (REQ-001…016, 023–028, 062–063) | **Yes** — the same inputs mean different actions by phase: hopper-not-high means "advance toward infeed start" only after forward start; reverse-running is a normal phase at start (REQ-005) but a recovery action mid-run (REQ-027); "resume from the reverse-run step" (REQ-027) is only expressible by remembering position in the sequence | Explicit stepped sequence (stateful → FB by construction, C-118) |
| Pusher cycle (REQ-032, 045–051) | **Yes** — extend/hold/retract phases; a mid-push pressure hold must resume "from existing position" (REQ-047), and the pressure switch means different things by stroke (REQ-051: ignored on retract) | Explicit stepped sequence (stateful → FB, C-118) |
| IO mapping & fault annunciation (REQ-052…059) | **No** — every annunciation and mapping output is expressible from current signals | Combinational (chained conditions in FCs; C-114 vocabulary) |
| In-cycle lamp (REQ-069) | **No** — a pure OR of current running feedbacks, by the ruling's own wording | Combinational |

## Equipment inventory

Signals from the as-built panel export (`DefaultTagTable`), operator commands (`DB_Controls`),
and settings (`DB_Settings`), plus named gaps. Status: `exists` = grep-verified in
`ir/GenProject1/` at write time; `proposed` = named gap, engineer creates tags (hard rule 3).
The pusher-component terminology note in the functional description ("terminology in drawings may
be slightly different") is carried here: drawing names may differ slightly from these tag names.

### Physical IO

| Equipment | Signal (dir) | Tag | Status |
|---|---|---|---|
| System | Control circuit healthy (in) | `DI1_SYS_ControlHealthy` | exists |
| System | Cycle start pushbutton (in) | `DI3_SYS_CycleStart` | exists |
| System | Cycle stop pushbutton, NC field device (in) | `DI4_SYS_CycleStop` | exists (tag comment: "(NC)") |
| System | Motor fault, soft-start fault relay (in) | `DI7_SYS_MotorFault` | exists |
| System | Downstream running/available (in) | `DI15_SYS_DownstreamRunning` | exists |
| System | In-cycle lamp (out) | `DQ8_SYS_InCycle` | exists |
| System | Enable upstream, volt-free (out) | `DQ9_SYS_EnableUpstream` | exists |
| System | Pre-start sounder/beacon (out) | `DQ10_SYS_PreStartSounder` | exists |
| System | Motor fault reset to soft start (out) | `DQ11_SHR_FaultReset` | exists |
| Shredder | Run forward command (out) | `DQ1_SHR_RunFwd` | exists |
| Shredder | Run reverse command (out) | `DQ2_SHR_RunRev` | exists |
| Shredder | Running forward feedback (in) | `DI5_SHR_RunFwdFB` | exists |
| Shredder | Running reverse feedback (in) | `DI6_SHR_RunRevFB` | exists |
| Shredder | Motor current feedback (analog in) | `DB_AnalogInput.ShredderMotorCurrent` | exists (buffer member only — **no physical AI channel**, Q-11) |
| Hopper | High level sensor (in) | `DI8_HPR_LevelHigh` | exists |
| Infeed conveyor | Run command (out) | `DQ4_IFC_Run` | exists |
| Infeed conveyor | Running feedback (in) | `DI9_IFC_Running` | exists |
| Discharge conveyor | Run command (out) | `DQ5_DIS_Run` | exists |
| Discharge conveyor | Running feedback (in) | `DI10_DIS_Running` | exists |
| Pusher | Power pack run command (out) | `DQ3_PSH_RunPowerPack` | exists |
| Pusher | Power pack running feedback (in) | `DI11_PSH_PowerPackRunning` | exists |
| Pusher | Parked/home limit switch (in) | `DI12_PSH_HomeLimit` | exists |
| Pusher | End travel limit switch (in) | `DI13_PSH_FullTravelLimit` | exists |
| Pusher | High pressure switch (in) | `DI14_PSH_HighPressure` | exists |
| Pusher | Extend solenoid (out) | `DQ6_PSH_Extend` | exists |
| Pusher | Retract solenoid (out) | `DQ7_PSH_Retract` | exists |
| Pusher | Local/remote selector (in) | `DI16_PSH_LocalRemote` | exists (semantics undefined in sources — Q-03) |
| Pusher | Remote manual-cycle pushbutton (in) | — | **proposed** (REQ-036, Q-03) |
| System | Machine-type selection (eng.) | — | **proposed** (REQ-018/067, Q-04) |
| System | Second-motor instrumentation (current/fault/feedback) | — | **proposed** (REQ-020/057, Q-05) |
| System | Motor-fault flashing lamp (out) | — | **proposed** if PLC-driven (REQ-060, Q-13) |
| HMI | Zeroable hour clock value + reset | — | **proposed** (REQ-066, Q-12) |
| Spares | 7 spare DIs (`DI2`, `DI17`–`DI22`), 7 spare DQs (`DQ12`–`DQ18`) | per tag table | exists (spare) |
| — | Legacy residue: `Tag_1`–`Tag_54`, comms words, at unrelated addresses | per tag table | exists (no function — sandbox residue, not equipment) |
| — | CPU system bits `AlwaysTrue`, `FirstScan`, `Clock_0.5Hz` | per tag table | exists |

### Operator commands (`DB_Controls`, HMI-written)

| Member | Role (per REQ) | Status |
|---|---|---|
| `PusherMode : Int` | REQ-033 mode selection | exists |
| `PusherManualCycleCmd : Bool` | REQ-035 manual cycle | exists |
| `PusherJogExtendCmd : Bool` | REQ-038 jog extend | exists |
| `PusherJogRetractCmd : Bool` | REQ-038 jog retract | exists |
| `FaultReset : Bool` | REQ-050 (and general fault reset) | exists |

### Settings (`DB_Settings`; start values = commissioning defaults, C-309)

| Member | Start value | Spec-stated number | REQ |
|---|---|---|---|
| `PreStartSounderTime` | 10.0 | 10 s | REQ-003 |
| `ShredderReverseRunTime` | 6.0 | 6 s | REQ-005 |
| `InfeedRestartDelay` | 5.0 | 5 s | REQ-009/015 |
| `InfeedToUpstreamEnableDelay` | 3.0 | 3 s (Q-07) | REQ-011/016 |
| `OvercurrentSetpointMedium` | **unconfigured** | none stated | REQ-019 |
| `OvercurrentSetpointHigh` | **unconfigured** | none stated | REQ-019 |
| `OvercurrentMediumDelay` | **unconfigured** | none stated | REQ-021 |
| `OvercurrentHighDelay` | **unconfigured** | none stated | REQ-022 |
| `OvercurrentSpinUpAllowance` | 3.0 | approx. 2–3 s | REQ-029 |
| `ReversalRetryPauseTime` | 4.0 | 4 s | REQ-026 |
| `ReversalWindowTime` | 180.0 | 3 minutes | REQ-028 |
| `ReversalCountThreshold` | 5 | 5 events | REQ-028 |
| `PressureTripConfirmTime` | 0.5 | 0.5 s | REQ-046 |
| `PressureClearResumeDelay` | **unconfigured** | **5 s stated** | REQ-047 |
| `PressureTripCountThreshold` | 5 | 5 trips | REQ-048 |
| `PusherEndTravelTimeout` | **unconfigured** | none stated | REQ-054 |
| `PusherParkedTimeout` | **unconfigured** | none stated | REQ-055 |
| `PusherEndTravelHoldTime` | 2.0 | approx. 2 s | REQ-032 |
| `PusherPumpRunOnTime` | **unconfigured** | none stated | REQ-031 |
| `PusherJogWarningTime` | 1.0 | 1 s | REQ-041 |
| `PusherFitted : Bool` | TRUE | — | REQ-042 |
| `DischargeConveyorTimeout` | **unconfigured** | none stated | REQ-004 |

(The 8 s spin-down pause of REQ-006 has no `DB_Settings` member; see REQ-006 notes and Q-09.)

## Open questions

Never silently resolved; resolution is a recorded owner answer noted at the question.

- **Q-01 — Restart/first-scan behavior. RESOLVED (owner ruling, 2026-07-17 —
  `docs/notes/owner-questions.md` D-1/B-2).** No auto-restart, ever: "Equipment should never
  restart after an E-Stop! (Except specifically documented exceptions)." Generalized into doc 06
  **C-128** (new rule) — a PLC power cycle is treated the same as an E-Stop for this purpose. A
  fresh, explicit start command always re-runs the **full** start-up sequence from the top; no
  mid-sequence resume. This makes the demo-panel OB100/`DB_PLC` omission (previously waived) a
  real gap, not a style choice — B-2's traced auto-resume is the concrete evidence. **Machinery
  built 2026-07-17** (`agent-tasks/02-startup-machinery.md`): `DB_PLC.Simulation` + `OB100` (force
  Step to idle on both stepped sequencers, clear the transient/S/R-driven state named in C-124/
  C-403, force `Simulation` off) imported and compiled clean against the GenProject1 scratch
  project (whole-device 0 errors/0 warnings; both blocks individually consistent; re-export
  readable-identical to the signed IR). See `gen/GenProject1/fix-wave-1-reviews.md` B-2 for the
  scope note (C-111 full simulation-mode gating deliberately not included in this pass).
- **Q-02 — Unconfigured settings.** Nine `DB_Settings` members have no start value (see the
  settings table): the four overcurrent setpoints/delays, `PressureClearResumeDelay` (the only
  one with a spec-stated number — 5 s, REQ-047), `PusherEndTravelTimeout`, `PusherParkedTimeout`,
  `PusherPumpRunOnTime`, `DischargeConveyorTimeout`. Owner numbers needed; commissioning defaults
  are C-309's factory-reset surface, so "unset" is a real gap, not a style note.
- **Q-03 — Local/remote selector semantics. Still open** — owner's 2026-07-17 response didn't
  settle the selector's own semantics; instead it raised a process idea worth tracking separately:
  "These are HMI features, Perhaps we should do a HMI Interface creation skill so we can fully
  define the PLC boundary which the system will squarely work in." Logged as a future-idea
  candidate (`docs/16-future-ideas.md`). The original question — is the selector meant to choose
  between the HMI button (local) and a remote button (remote), and which input is the remote
  button wired to? — still needs an owner answer.
- **Q-04 — Machine-type selection structure and values. RESOLVED (owner ruling, 2026-07-17).**
  "FuncDesc is [primary], this disparity should have been flagged at an earlier stage." Type
  count: **three types (K75/K100/K150)** per REQ-018/FuncDesc, consistent with this register's
  own stated source-precedence rule (FuncDesc over SpecSheet on disagreement) — REQ-067's
  two-type changeover row is the SpecSheet disagreement, formally overridden. Selection tags: no
  selection tags exist yet — stays **proposed** (engineer creates, hard rule 3). Per-type
  overcurrent setpoint numbers: **deferred** (round 3, 2026-07-17 — "I dont have those at the min,
  leave 0 add to deferred"). Moved to `docs/notes/deferred-items.md`; `OvercurrentSetpointMedium`/
  `OvercurrentSetpointHigh` (REQ-019, currently unconfigured, no start value) get an explicit `0`
  start value as a deliberate placeholder (per the new C-604 placeholder convention) rather than
  staying unset, until real numbers land.
- **Q-05 — Motor count / per-motor identification. Still open** — owner's 2026-07-17 response was
  a design hint, not a headcount answer: "This isn't a full question, going off limited context,
  it may be a good idea to use motor starter type FBs for each motor." Noted for
  `gen-architecture`'s reuse-first pass (owner-questions A-1) if/when this is built — a
  motor-starter FB per motor is the likely tier-(a)/(b) shape. Still unresolved: does the demo
  machine have two motors to instrument, or do REQ-020/057 scale down for the demo?
- **Q-06 — Start button action. RESOLVED (owner ruling, 2026-07-17): "Ok momentary, this the
  default in most systems."** FuncDesc's reading (momentary press) stands over SpecSheet's
  "press and hold" typical-sequence narrative — REQ-001 confirmed accordingly. `DI3_SYS_CycleStart`
  is read as a momentary pushbutton, not a maintained contact; the start sequence latches on its
  own once triggered (no separate "held" behavior to implement).
- **Q-07 — Upstream enable delay. RESOLVED (owner ruling, 2026-07-17): "Confirm."** 3 s after
  infeed start, as a separate `DQ9_SYS_EnableUpstream` output — FuncDesc's reading stands over
  SpecSheet's conflated 6 s/loading-light narrative. `DB_Settings.InfeedToUpstreamEnableDelay` =
  3.0 is correct as-is (REQ-011/016).
- **Q-08 — Downstream recovery behavior. RESOLVED (owner ruling, 2026-07-17).** "Yes always
  always always require a manual restart unless their is a documented exception." Not
  configurable/automatic — manual restart only, consistent with the new C-128 rule and Q-01's
  resolution.
- **Q-09 — Home of the 8 s spin-down pause. RESOLVED by verification (2026-07-17) — no owner
  input needed, this was answerable from existing evidence.** Grep-confirmed:
  `MotorFwdRevIOSet.ReverseDelay : Real` (the motor FB's own interface UDT, genuinely
  per-instance) is actually consumed inside `FB_MotorFwdRevSystem` — feeds the standard
  HMI-time-conversion pair (`Time`/`PauseTimeMS`) that drives the FB's own `ReversalPauseTimer` —
  and `iDB_MotorFwdRevSystem_Shredder`'s own start value is `8.0`, an exact match to REQ-006's
  stated 8 s. This is precisely C-307's per-instance-setting shape (owned by the one instance that
  uses it, commissioning default as the iDB start value) — no `DB_Settings` member is needed or
  would be architecturally correct here. No fix required; the existing home was already right.
- **Q-10 — Overload annunciation. RESOLVED (owner ruling, 2026-07-17).** "No, these are
  different. Their is Software overload and their is hardware overload, the trip is both hardware
  overcurrent or overload, while the software is in the effort of catching the overload before
  hardware trip so no manual intervention is needed, reduces strain on motor, etc." Reading: the
  single `DI7_SYS_MotorFault` hardware input (REQ-056) is the last-resort trip and covers hardware
  overcurrent *or* hardware overload together — it cannot be split on this hardware, and that's
  accepted. The PLC-side software overcurrent chain (REQ-017–029) is a genuinely separate,
  distinct protective function — not a duplicate/mergeable alarm — that exists specifically to
  catch a developing overload *before* it reaches the hardware trip. REQ-059's overload alarm
  stays its own annunciation, distinct from REQ-056. Do not merge REQ-056/059 into one alarm bit.
- **Q-11 — Analog current feedback hardware. RESOLVED (owner ruling, 2026-07-17 — ties to
  owner-questions D-3).** "Their is real AI hardware, this was a proto type run want to confirm
  with just bool signals." Reading: real AI hardware exists (for production panels); this
  GenProject1 build is a prototype run, and the owner confirms proceeding with bool-signal-only
  overcurrent detection for it — the overcurrent family (REQ-017…022) stays disarmed pending an
  actual AI-hardware build, not because hardware doesn't exist in general, but because this
  prototype's own panel doesn't carry the AI channel. No tags to propose from this ruling alone.
- **Q-12 — Zeroable hour clock. Partially resolved (owner ruling, 2026-07-17).** "Already
  implemented via Fwd/Rev Block, the clock is shredder specific." Read as answering REQ-065 (the
  overall hour clock) — its home is the motor instance's own `HrsRun` member, confirmed. Still
  open: REQ-066 specifically asked about a *zeroable* clock for different applications (resettable
  job/trial hours) — `HrsRun` has no reset command in the corpus (grep-verified at register write
  time). Whether `HrsRun` itself should gain a reset, or a second value is wanted, needs a
  follow-up owner confirmation.
- **Q-13 — Reversal display outside panel + flashing fault lamp. RESOLVED (owner ruling,
  2026-07-17).** "reversal display shown on HMI, fault flashing lamp is PLC Driven." Split
  resolution: the outside-panel reversal-count **display** is HMI-side — no PLC output tag needed,
  bind the existing `ReversalCount` value (already `exists`, REQ-060 notes). The **flashing
  motor-fault lamp** is PLC-driven — promote from "proposed if PLC-driven" to definitely
  **proposed**: a new output tag is needed (equipment inventory's "Motor-fault flashing lamp"
  row updates accordingly).
- **Q-14 — "Control On" step. RESOLVED (owner ruling, 2026-07-17): "FaultReset is all thats
  needed."** No separate control-on/reset precondition before REQ-001's start press — SpecSheet's
  "Control On" step is just the existing `DB_Controls.FaultReset`/`DQ11_SHR_FaultReset` mechanism,
  used when a fault needs clearing, not a new universal precondition gate.
- **Q-15 — Hand control vs mode structure. RESOLVED (owner ruling, 2026-07-17): "Hand = Maunal,
  this is true always."** Hand/jog (REQ-038…041) is not a fourth `PusherMode` value — it is
  available within Manual mode (REQ-033's mode = 2), always. No new mode value needed.
- **Q-16 — Off-mode idle repark (REQ-037 vs REQ-039).** Fix-wave-1 (owner rulings, 2026-07-16)
  narrowed the idle repark to resolve the REQ-037/REQ-039 tension: with the ram idle but off
  home, selecting Off no longer self-parks — repark occasions are only the plant pre-start and
  cycle entry (both pre-warned per NEW-4), and jog release stops in place per REQ-039. Mid-cycle
  Off still retracts and parks (REQ-037's core). **Resolved — owner ruling recorded 2026-07-16
  (fix-wave-1 sign-off): stands as drafted; the ram parks at the next pre-start. An
  Off-must-park-immediately behavior would need a further ruling and would itself be a warned
  repark.**
