# review-simplicity skill — blind-run report (verbatim), 2026-07-16

> Verbatim §3 of `docs/notes/review-simplicity-validation-2026-07-16.md`, split out
> 2026-07-18 per `docs/16-future-ideas.md` FI-19. This is the unedited blind-run
> agent output. Kept verbatim on purpose: the drift check needs byte-identical
> output and the "blind executor, non-blind examiner" method needs the report
> unedited so a skeptic can check the grading against the agent's own words. The
> §1/§2 analysis and verdict stay in the validation note.

---

## 3. Blind run report (verbatim)

The full report as returned by the fresh-context agent, unedited:

---

# Simplicity review — ir/test-project001/ complete corpus, 21 files (2026-07-16)

**Blindness:** blind — I did not author or restructure any of this code, and this session contains no author reasoning. Two unavoidable caveats, declared for honesty: (1) doc 06's own rule rationales (C-601, C-607) cite examples drawn from this corpus's history, and the stage-gates section I was directed to for regime labels contains a one-paragraph summary of earlier finding categories; (2) I did **not** open `docs/notes/test-project001-retrospective.md`. Every finding below was independently re-derived from the IR text with quoted evidence, not copied from those summaries.

**Blocks reviewed:**
- `Main` (OB1) — generated
- `FC_Inputs`, `FC_Outputs`, `FC_ControlMain`, `FC_AlarmsMain` — generated
- `FB_PusherControl` + `UDT_PusherIO` + `iDB_PusherControl` — generated
- `FB_ShredderSequencer` + `UDT_ShredderSequencerIO` + `iDB_ShredderSequencer` — generated
- `DB_Input`, `DB_Output`, `DB_Controls`, `DB_Settings`, `DB_Alarms`, `DB_AnalogInput`, `DefaultTagTable` — generated
- `FB_MotorFwdRevSystem` + `MotorFwdRevIOSet` + `iDB_MotorFwdRevSystem_Shredder` — **imported-real** (regime confirmed in stage-gates: "real, imported unmodified"; findings on these are documented context, not fix demands)

Generated blocks were held to the stricter bar (doc 06 preamble); imported-real findings are calibration/cross-reference context.

---

## Findings

### FC_ControlMain (generated)

- **[C-604, error]** Network 3 "MotorFwdRevSystem Wiring": four `AlwaysTrue`-derived constants wired into block inputs with no comment and no named source — the reader cannot tell design ("this plant has no hand station") from debt ("hand mode not wired yet").
  Evidence:
  ```
  COIL iDB_MotorFwdRevSystem_Shredder.IO.InHand := NOT AlwaysTrue
  COIL iDB_MotorFwdRevSystem_Shredder.IO.RecentStart := AlwaysTrue
  COIL iDB_MotorFwdRevSystem_Shredder.IO.InhibitMotor := NOT AlwaysTrue
  COIL iDB_MotorFwdRevSystem_Shredder.IO.HandReverse := NOT AlwaysTrue
  ```
  Why it fails one reading: none of the four is (a) the mapping-rail idiom, (b) a commented known-gap placeholder, or (c) a named `Fitted`-style source — and the same block does (c) correctly two networks later (`IO.Fitted := DB_Settings.PusherFitted`), so the reader is left guessing which of the four are permanent. Two aggravators found by grep: `RecentStart := AlwaysTrue` is a second writer fighting the FB's own management of that bit (FB network 4 both comments the HMI set-bit contract and writes `IO.RecentStart := IO.RecentStart AND NOT ...` — the constant re-set makes that logic decorative); `HandReverse` is wired into a member **no FB network ever reads** (declaration only, `FB_MotorFwdRevSystem.ir` line 43). Every bare constant here also erodes the loud-TODO value of the `NOT AlwaysTrue` idiom that FB_ShredderSequencer's genuine placeholder depends on — C-604's stated rationale.
  Suggested fix: source each from a named, commented setting (`ShredderHandStationFitted`-style, per the `PusherFitted` precedent) or add a per-line comment stating why the input is permanently constant, and delete the `HandReverse`/`RecentStart` wiring outright if the members are unused/self-managed.

- **[C-607, warn — cross-cites C-308 (error)]** Network 5 "PusherControl Wiring": eight cyclic `MOVE`s copy `DB_Settings.Pusher*`/`Pressure*` members into `iDB_PusherControl.IO` settings members every scan — the same setting exists in two homes with a copy in between.
  Evidence: `MOVE(EN := TRUE, IN := DB_Settings.PusherEndTravelTimeout) => iDB_PusherControl.IO.EndTravelTimeout` (and 7 siblings, lines 57–64)
  Why it fails one reading: the reader cannot tell which copy is authoritative — and per C-308's extension this shape silently reverts any faceplate edit of the UDT member one scan later. Doc 06 names this exact FC as the trap example. Neither this FC nor FB_PusherControl's header states a reason for the double-home policy.
  Suggested fix: one home per C-307 — keep the settings in `iDB_PusherControl.IO` (HMI-written, start values as commissioning defaults), delete the eight MOVEs and the eight `DB_Settings.Pusher*` duplicates.

- **[preamble one-reading, info]** Network 1 "Sequencer Input Wiring": `COIL iDB_ShredderSequencer.IO.PusherJogPreStartSounder := iDB_PusherControl.IO.JogPreStartSounder` reads a pusher output before `FB_PusherControl` runs this scan (network 6) — a deliberate-looking one-scan lag with no comment.
  Why it fails one reading: the lag is invisible unless the reader reconstructs call order; the project's own house style for exactly this situation is FB_ShredderSequencer network 11's comment ("one scan behind ... harmless for a multi-second delay").
  Suggested fix: one comment line on network 1 noting the intentional one-scan lag on the sounder feedback edge.

- **[C-203/one-reading, info]** Network 7 is titled "Output Mapping" — the same phrase OB1 network 4 uses for the real output-mapping layer (`FC_Outputs`, "Output Map"), but this network does a different job (FB outputs → buffer DB).
  Why it fails one reading: a reader told "output mapping is the last call in OB1" (C-110) meets a second, mid-scan "Output Mapping" and must reconcile the collision.
  Suggested fix: retitle, e.g. "Output Buffer Wiring".

### FB_ShredderSequencer (generated)

- **[C-604, error]** Network 12 "Step 50 - Overcurrent Detection, Trip, And Transitions": both overcurrent timers are gated by the `NOT AlwaysTrue` known-gap placeholder; the comment names *that* a gap exists but delegates *what it is* to a member comment that does not exist in any reviewable file.
  Evidence:
  ```
  TON(OvercurrentMediumTimer, IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue, PT := OvercurrentMediumDelayMS)
  TON(OvercurrentHighTimer, IN := IO.Step = 50 AND SpinUpTimer.Q AND NOT AlwaysTrue, PT := OvercurrentHighDelayMS)
  COMMENT "... see OvercurrentTripped's own comment for the AlwaysTrue placeholder gap this still has."
  ```
  Why it fails one reading: the reader must *derive* (not read) that overcurrent detection is nonfunctional and step 60 unreachable; the missing substance is the unbuilt Real-vs-Real compare of `IO.ShredderMotorCurrent` against the setpoints. Grep confirms the whole chain is dead: `IO.ShredderMotorCurrent` is consumed nowhere in the FB, and `DB_Settings.OvercurrentSetpointMedium`/`OvercurrentSetpointHigh` are consumed nowhere in the corpus, with no gap comment on the DB_Settings side (only `DB_AnalogInput`'s header documents the *source* side).
  Suggested fix: state the gap in the network comment itself ("current > OvercurrentSetpointMedium/High compare unbuilt — typed-Compare capability pending; detection disarmed"), and mark the two dead `DB_Settings` members the same way.

- **[preamble one-reading / C-202, warn]** Networks 4 and 12: two "see X's own comment" pointers reference member comments that exist in no file of the corpus (the IR TYPE/DB grammar carries no member-comment field — see UDT findings).
  Evidence: `"...see ReversalCount's own comment for why this is a re-arming window, not a per-cycle count."` (N4); `"...see OvercurrentTripped's own comment..."` (N12)
  Why it fails one reading: the promised justification is unreachable from the artifact under review — a broken documentation pointer forces a search that ends nowhere.
  Suggested fix: inline the referenced explanation into the network comment until the format can carry member comments. (Candidate rule gap: comments must not reference documentation the medium cannot hold.)

- **[C-603, warn]** Networks 14/15: three ranged step predicates, none carrying the required "future inserted steps are meant to join this span" comment.
  Evidence:
  ```
  COIL IO.MotorAutoStartCmd := IO.Step >= 30 AND IO.Step <= 50
  COIL IO.MotorPreStartDoneCmd := IO.Step >= 20
  COIL IO.RunDischargeConv := IO.Step >= 20
  ```
  Why it fails one reading: C-120 invites inserting step 25/35/45 without renumbering; each insertion silently joins all three spans with no visible decision — the reader cannot tell whether that is intended.
  Suggested fix: either enumerate (`Step = 30 OR Step = 40 OR Step = 50`) or add the span-intent comment to networks 14 and 15. (Networks' `Step = 60` / `Step <> 60` pair is fine per the rule.)

- **[preamble one-reading, warn]** `IO.InCycle` is consumed downstream into a physical output but written nowhere.
  Evidence: declared (`UDT_ShredderSequencerIO` line 22), read by `FC_ControlMain` (`COIL DB_Output.In_Cycle := iDB_ShredderSequencer.IO.InCycle`) into `DQ8_SYS_InCycle` ("Machine in-cycle status") — grep finds no COIL/MOVE writing it in any block.
  Why it fails one reading: a reader tracing the in-cycle lamp walks three hops to discover it is permanently false, with no comment anywhere marking it unbuilt — indistinguishable from a wiring mistake. Candidate rule gap: an interface output consumed downstream must be driven somewhere or carry a known-gap comment (C-604's principle without its grep signature).
  Suggested fix: drive it (`IO.InCycle := IO.Step <> 0` matches the pusher's own `Cycling` idiom) or comment the gap at both the member's consumer and here.

- **[C-606, warn — judgment, no requirements register available]** The header enumerates motor/conveyor/upstream duties but not the pusher-mode arbitration (network 14 forces `PusherModeCmd` to 0 in step 60, else passes through `OperatorPusherMode`) or the pusher-sounder merge (network 15) — two caller-visible capabilities discoverable only by reading the body.
  Suggested fix: one header line each ("also arbitrates the pusher's mode — forces Off during overcurrent abort; merges the pusher jog warning into the plant sounder").

- **[preamble one-reading, info]** Network 6: `IO.DownstreamRunning` is logically redundant inside a condition already gated by `NOT StopCmd` (StopCmd := `IO.CycleStop OR NOT IO.DownstreamRunning`, so `NOT StopCmd` already implies it).
  Evidence: `MOVE(EN := NOT StopCmd AND NOT IO.ShredderBlockedFault AND IO.CycleStart AND IO.DownstreamRunning AND IO.Step = 0, IN := 10) => IO.Step`
  Why it fails one reading: the redundant term makes the reader doubt their reading of the named bit ("did I misunderstand StopCmd?") — undermining exactly the documentation value C-601 says the name provides.
  Suggested fix: drop the redundant term, or comment why belt-and-braces is wanted here.

### FB_PusherControl (generated)

- **[C-601, warn — with C-602 secondary]** Networks 4 and 6: the 8-term cycle-start compound is duplicated across two networks, differing only by an `OR IO.FaultReset` tail — the dangerous near-match form.
  Evidence:
  ```
  N4: MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked OR IO.FaultReset, IN := 0) => PressureTripCount
  N6: MOVE(EN := (IO.Mode = 1 AND IO.HopperHighLevel OR IO.Mode = 2 AND IO.ManualCycleCmd) AND IO.Step = 0 AND IO.HomeLimit AND IO.Enable AND NOT IO.Blocked, IN := 10) => IO.Step
  ```
  Difference: N4 carries a trailing `OR IO.FaultReset`; the other 8 terms are identical.
  Why it fails one reading: to confirm "the count resets exactly when a cycle launches" (which N4's comment asserts), the reader must term-by-term diff two distant 8/9-term expressions — a guaranteed second pass, and one edited copy diverging later would be invisible. The same block's sibling (`FB_ShredderSequencer.StopCmd`) and this FB's own `ExtendDemand`/`RetractDemand` show the name-it-once rule done right. Secondary: N4's 9-contact/3-OR expression exceeds C-602's soft guide and the comment justifies the reset *policy*, not the expression size.
  Suggested fix: one named bit (e.g. `CycleStartOk`) written once, read by both networks (`CycleStartOk OR IO.FaultReset` in N4) — resolves both citations.

- **[C-601, info — borderline, engineer's judgment]** Networks 10/11: the 4-term jog-arm condition appears twice verbatim within N10 (TON `IN` and sounder coil — likely one fanned rung, the C-602-exception shape) and in two per-direction near-match variants in N11.
  Evidence: `(IO.JogExtendCmd OR IO.JogRetractCmd) AND IO.Step = 0 AND IO.Mode = 2` (N10, twice) vs `IO.Step = 0 AND IO.Mode = 2 AND IO.JogExtendCmd AND JogPreStartTimer.Q` (N11).
  Why flagged despite mitigation: the networks are adjacent and N11's comment explains the timer relationship, so this is recorded for awareness, not as a demand; a named `JogSelected` bit would still shrink four copies to one write.

### FC_Inputs (generated)

- **[preamble one-reading, warn — function-tier observation; functional review must rule]** Network 1: `DI4_SYS_CycleStop` is documented as a normally-closed pushbutton but mapped non-negated, and the consumer treats true as stop.
  Evidence: tag table: `DI4_SYS_CycleStop ... COMMENT "Cycle stop pushbutton (NC)"`; mapping: `COIL DB_Input.Cycle_Stop := AlwaysTrue AND NOT DB_Input.Test[0] AND DI4_SYS_CycleStop OR ...`; consumer: `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning`.
  Why it fails one reading: an NC button reads true while healthy/un-pressed, which as wired makes StopCmd permanently true (plant can never start) — the tag comment and the rung cannot be reconciled in one pass. The input-mapping pattern documents a negated variant (`NOT DI<n>`) for exactly this active-low case.
  Suggested fix: functional review confirms field polarity; if genuinely NC, map negated per the pattern variant (and title/comment the polarity).

- **[C-606, warn — judgment]** Two buffer inputs are mapped from the field and consumed by nothing, with no comment marking them future/spare: `DB_Input.Pusher_Local_Remote` (from `DI16_PSH_LocalRemote`, "local/remote selector" — a selector that controls nothing) and `DB_Input.Infeed_Conv_Running` (from `DI9_IFC_Running` — the sequencer starts the infeed conveyor but never checks its feedback).
  Evidence: grep over the corpus finds writers only (`FC_Inputs` lines 21, 30), zero readers.
  Why it fails one reading: a reader tracing either signal finds a dead end and cannot tell unbuilt feature from wiring mistake — same principle as C-604, no citable grep signature (candidate rule gap: dead buffer members need a gap comment).
  Suggested fix: consume them, or comment them as mapped-ahead-of-need at the mapping line.

### DefaultTagTable (generated)

- **[C-002-spirit/one-reading, warn]** 58 of 98 entries are function-less legacy names (`Tag_1`…`Tag_54`, `AirStarWord0IN/2IN/0OUT/2OUT`), uncommented, at addresses unrelated to this machine, and referenced by no logic in the corpus (grep-verified).
  Why it fails one reading: more than half the tag table is noise a reader must sift to find the 40 real, well-commented DI/DQ tags — and nothing marks the noise as residue.
  Suggested fix: delete the unused legacy entries from the scratch project, or comment them as sandbox residue pending deletion. (`AlwaysTrue`, `FirstScan`, `Clock_0.5Hz` are legitimate clock-memory bits; `AlwaysTrue` is in live use.)

### UDT_PusherIO (generated)

- **[C-605, error — with a format caveat, per the rule's own instruction]** 30 of 30 members carry no comment (`Step`, `Fitted`, `Enable`, `Mode`, `HopperHighLevel`, `ManualCycleCmd`, `JogExtendCmd`, `JogRetractCmd`, `HomeLimit`, `FullTravelLimit`, `HighPressure`, `PowerPackRunningFB`, `FaultReset`, `EndTravelTimeout`, `ParkedTimeout`, `EndTravelHoldTime`, `PumpRunOnTime`, `JogWarningTime`, `PressureTripConfirmTime`, `PressureClearResumeDelay`, `PressureTripCountThreshold`, `Cycling`, `Blocked`, `EndTravelTimeoutFault`, `ParkedTimeoutFault`, `BothSwitchesFault`, `RunPowerPack`, `Extend`, `Retract`, `JogPreStartSounder`).
  Caveat, stated rather than inventing compliance: the IR `TYPE`/DB member grammar carries **no member-comment field at all** (`ir/SPEC.md` — only TAGTABLE entries have `COMMENT`), so compliance is structurally impossible in the reviewed medium, and I cannot distinguish "absent in TIA" from "present in TIA but undisplayable here". Units/roles are genuinely needed: seven Real members are seconds-valued settings, `Enable` is block-written (not the C-115 input its name suggests), and `Mode`'s 0/1/2 legend lives only in the FB header.
  Suggested fix: add member comments TIA-side and extend the IR TYPE grammar to carry them (tooling prerequisite — see Cross-block tensions).

### UDT_ShredderSequencerIO (generated)

- **[C-605, error — same format caveat]** 23 of 23 members bare; the ones that most need comments: `ShredderMotorCurrent` (dead pending the typed-Compare gap), `InCycle` (never written), `OperatorPusherMode`/`PusherModeCmd` (the 0/1/2 legend lives in the *other* FB's header), `MotorPreStartDoneCmd` (non-obvious handshake to the motor FB).
  Suggested fix: as above.

### FB_MotorFwdRevSystem (imported-real — documented context, not fix demands)

- **[C-126, warn — context]** Network 7 "HMI Times" is the kind-batched MUL/CONVERT block **without** the scheme comment that is the exception's one condition — and the index-pairing is unusually opaque here because pair 4 crosses names.
  Evidence: `MUL(EN := TRUE, IN1 := IO.ReverseDelay, IN2 := 1000.0) => Time` pairs with `CONVERT(EN := ENO, IN := Time) => PauseTimeMS` (position 4 of 5).
  Context value: this is the calibration source of the idiom — both generated FBs correctly added the comment doc 06 now requires.

- **[C-101/C-602, warn — context]** Network 1 "Reverse Pause and Control": 13 statements — three timers, two constructed edge detectors, and interleaved SCOIL/RCOIL set/reset of `Pasue`, `StartTimer`, `CycleDelay`. Multiple readings required; `StartTimer` is a TEMP driven by set/reset coils (undefined between scans — cross-reference risk for anyone copying the shape). Typos `Pasue`/`NegitiveSignalEdge` recorded as context per calibration.

- **[C-602/one-reading, warn — context]** Network 2: `IO.TryRunMotor` is ~10 uncommented contacts; `IO.RunFwd`/`IO.RunRev` are 14-term near-twins differing only in the final `Reverse` polarity, and the `IO.TryRunMotor AND Pasue` disjunct is annihilated by the trailing `AND NOT Pasue` — a dead term the reader trips over.
  Evidence: `COIL IO.RunFwd := (... OR IO.TryRunMotor AND Pasue OR ...) AND NOT IO.StopMotor AND NOT IO.Shutdown AND NOT Pasue AND NOT IO.Reverse`

- **[one-reading, warn — context; recommend verification against the TIA original]** Network 5 (untitled) writes `HandPosEdge` twice — the second assignment overwrites the first in listed order — and network 6 then tests the same bit twice in one expression, while the plausibly-intended twin `HandNegEdge` is declared and never referenced anywhere (grep-verified).
  Evidence: `COIL HandPosEdge := IO.InHand AND NOT RisingEdgeFlags[3]` then `COIL HandPosEdge := (IO.InHand OR NegitiveSignalEdge[2]) AND NOT IO.InHand`; N6: `... AND NOT HandPosEdge AND NOT HandPosEdge`.
  This reads like a rename/miswire; whether it is source-real or a rendering fidelity question, the readable corpus fails one reading here.

- **[one-reading, warn — context; functional review should verify]** Network 13 "Hours Run Counter": the rollover/increment guard reads its own edge memory *after* it is updated in the same network (`COIL RisingEdgeFlags[2] := HrTotaliserTimer.Q` is kind-ordered before the MOVE/ADD that test `NOT RisingEdgeFlags[2]`), which per the IR's documented same-scan freshness rule renders the guard always false. Also the uncommented magic constant `4294967295`. `TONR` use is the known, pattern-documented C-406 deviation — calibrated, no new finding.

- **[C-602/C-601-shape, warn — context]** Network 14 line 4: the telemetry=3 MOVE's EN repeats the full running-condition expression four times (~20 contacts) — the corpus's single worst one-reading failure; the MOVE-cascade idiom itself is the documented site telemetry shape.

- **[C-201/C-203, info — context]** No block title, no header comment, network 5 untitled; network 15 packs 3 alarm bits in one network (known deviation from C-501's one-bit condition, already documented in the motor-dol pattern notes).

### MotorFwdRevIOSet (imported-real — context)

- **[C-605, error — context, same format caveat]** 33 of 33 members bare. Highest cross-reference risk: `RecentStart`/`HandIntervention` (HMI set-bit contracts stated only in network comments), `Reverse` vs `HandReverse` (only one is ever read), `ReverseDelay` (seconds; feeds `PauseTimeMS` cross-name), `Name : String` (unset by anything in this project).

---

## Clean declarations

- **Main (OB1):** clean on all swept rules — C-601/602/603/604/605(n/a)/606/607, C-203, C-101/C-126, one-reading walk (four one-call networks, C-109/C-110 order visible at a glance).
- **FC_Inputs:** beyond the two findings above — rail idiom conforms to the input-mapping pattern shape on all 22 points (unique Test indices 1–22, no missing-override instances, no index collisions); C-601/C-602 (pattern-sanctioned shape), C-603 n/a, C-604(a) sanctioned, C-203 clean.
- **FC_Outputs:** clean on all swept rules — all 18 points exact pattern shape, unique Test indices, spare handling consistent; C-604(a) sanctioned; titles short.
- **FC_AlarmsMain:** clean on all swept rules — one alarm bit per network, network title carries the alarm text per C-501's own condition (not a C-203 breach), every source is a named FB fault bit, no duplicated compounds, no constants.
- **FC_ControlMain:** clean on C-601, C-602 (single-term wiring coils), C-603 (n/a), C-126 (wiring adjacent to its CALL, call order sequencer→motor→pusher keeps command data same-scan fresh).
- **FB_PusherControl:** clean on C-603 (all `Step = n` / `Step <> 0`), C-604 (no hits), C-126 (every timer sits with its fault and transitions; the HMI-times batch carries the required scheme comment), C-203 (short titles, detail in comments — the comment set here is the corpus's best: N6/N7/N9/N11/N13 each pre-answer the reader's "why"), C-607-internal (latch/edge/counter idioms consistent with its sibling).
- **FB_ShredderSequencer:** clean on C-601 (`StopCmd` is the name-it-once rule done right; no ≥3-term duplicates), C-602 (transition MOVEs within guide; C-121 verbosity calibrated out), C-126 (HMI-times comment present; network 11's one-scan-lag comment is exemplary and should be the template for FC_ControlMain's lag), C-203.
- **DB_Controls / DB_Settings / DB_Alarms / DB_AnalogInput:** header comments present and good — DB_AnalogInput's is the corpus's model of C-604-spirit gap documentation. One-writer check clean: grep finds no logic writing DB_Controls or DB_Settings.
- **iDB files (×3):** consistent mirrors of their FB interfaces; nothing independent to flag (and per the skill, the iDB is not where interface comments belong).
- **Imported-real FB, rules clean:** C-603 (no stepped sequence), C-604 (no AlwaysTrue inside the FB).

## Cross-block

**C-607 idiom table** (S = settings access, E = edge memory, T = time conversion, L = latch style, N = buffer naming):

| Idiom | FB_PusherControl (gen) | FB_ShredderSequencer (gen) | FB_MotorFwdRevSystem (real) |
|---|---|---|---|
| S: settings access | own-UDT members ← **scan-copied** from DB_Settings duplicates | **direct `DB_Settings.*` reads inside the FB** (N2 ×10, N5 threshold) | own-UDT members, HMI-written, iDB start value (`ReverseDelay = 8.0`) |
| E: edge memory | named scalar Statics | named scalar Statics | `RisingEdgeFlags[0..3]` / `NegitiveSignalEdge[0..2]` arrays (C-107 style) |
| T: time conversion | block-top MUL×1000→CONVERT, shared TEMP `Time`, **commented** | same, **commented** | same, **uncommented** |
| L: latch style | plain-coil latches throughout | plain-coil latches throughout | SCOIL/RCOIL + plain mix |

- **[C-607, warn]** Settings access: three policies coexist in one project, none stating a reason in its header. Against current C-307/C-122 the imported block's policy is the compliant one; both generated FBs deviate, each differently — the pusher route creates the exists-twice/scan-copy trap (filed under FC_ControlMain), the sequencer route hard-couples a reusable FB to a global DB. Suggested fix: converge both generated FBs on own-UDT settings, HMI-written, per C-307.
- **[C-607/C-001, warn]** Buffer member naming: `DB_Input`/`DB_Output` members are `Snake_Case` (`Control_Healthy`, `Run_Shredder_Fwd`) while every UDT, `DB_Settings`, `DB_Controls`, and the db-inputs pattern's own members are underscore-free PascalCase — two naming policies for the same kind of object, cross-referenced on nearly every wiring line of FC_ControlMain.
- Edge-memory and latch-style differences are generated-vs-imported only; the two generated FBs agree with each other (and the generated latch style is the C-403-preferred plain-coil form) — recorded as context, no finding.
- **[info]** The shared TEMP scratch named `Time` (all three FBs, inherited from the site idiom) shadows the `Time` datatype in lines like `CONVERT(EN := ENO, IN := Time)` — consistent project-wide, so no C-607 breach, but a one-reading stumble and a candidate naming-rule gap.

**Pattern-vs-rule tensions needing an owner ruling (per calibration, no side taken):**
1. The **admitted** motor-dol pattern's `MotorStarter.ir` network 6 "HMI Times" (lines 116–122) carries **no** scheme comment — the pattern's own example fails the one condition C-126's exception sets, so a generation copying the admitted example verbatim would breach doc 06. (The pattern also contains an untitled network 4.)
2. **C-605 (error) vs the IR format:** member comments are mandatory on interface UDTs, but the IR TYPE/DB member grammar has no comment field — the rule is currently unsatisfiable in the medium reviews run on, and FB_ShredderSequencer's two dangling "see member comment" pointers are collateral damage of exactly this gap.

## Not checkable here

- **Requirements register** — C-606 findings are judgment calls as the skill requires me to say; I could not verify which capabilities (pusher-mode arbitration, local/remote selector, infeed feedback) trace to the functional description.
- **HMI artifacts** — whether faceplates bind and display `Cycling`, `Telemetry`, `HrsRun`, `Blocked`, the settings pages (which determines how damaging the FC_ControlMain scan-copy is in practice), and the pusher/motor mode legends operator-side.
- **TIA-side originals** — whether the imported FB's oddities (HandPosEdge double write, dead `AND NOT Pasue` term, N13 edge-guard ordering) are source-real or IR-rendering artifacts; and whether member comments exist in TIA that the IR cannot carry. Recommend a converter-fidelity check by the engineer, not a hand-patch (hard rule 7).
- **Startup machinery** — `IO` structs (including `Step`) are RETAIN with no OB100/`DB_PLC` machinery visible in the corpus (C-124/C-305/C-403 territory — the mechanical/functional reviews own it; noted because RETAIN `Step` surviving a power cycle interacts with FB_PusherControl N6's not-at-home recovery design).
- **Alarm-tier rules** (C-504 consequence suppression, e.g. `Motor_Fault` vs FTR/FTS echoes) — outside this skill's tier-2 scope.
- **Hardware config** — whether the 58 legacy tag-table entries are held by hardware configuration or HMI references invisible to this corpus.

---

**Summary:** 6 error-severity findings on generated content (C-604 ×2, C-605 ×2 with a declared format caveat, plus the two C-605 context errors on the imported UDT counted separately), 12 warn, 5 info; the two generated FBs' network-comment discipline and C-126 compliance are genuinely strong — the dominant defects are duplicated compound conditions (C-601), uncommented constants and dead signal chains (C-604 and its uncited cousins), ranged step predicates (C-603), a three-way settings-access split (C-607/C-308), and a UDT member-comment requirement the IR format cannot yet express. Two pattern-vs-rule tensions are queued for owner ruling. No files were modified.
