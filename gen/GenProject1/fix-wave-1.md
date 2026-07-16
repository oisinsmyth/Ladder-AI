# GenProject1 — Functional fix wave 1, phase 1 (draft + static verification)

Owner-directed fix wave implementing the four rulings of 2026-07-16 on the GenProject1 corpus IR
(`ir/GenProject1/`). **Phase 1 is Portal-free**: IR drafted, statically verified (synthesis +
preflight + mechanical review delta), committed on a worktree branch, stopped at the sign-off
checkpoint below. **Phase 2** (import → compile → untouched-network invariance → adversarial
reviewers → presentation) is run by the coordinator only after the owner signs §1.

- **Produced:** 2026-07-16, `manual:gen-block-coding` (fix wave against existing blocks — S7-style
  modification discipline: named networks only, readable-text diff proves the rest identical).
- **Rulings implemented:** (1) timing defaults, (2) reverse run times confirmed feedback,
  (3) stop/E-stop bundle, (4) jog/repark spec-true + REQ-043/044 Fitted gating.
- **Explicitly out of scope** (unchanged, not drive-by'd): the settings sub-struct migration
  (values land in their current homes), any imported-FB logic edit (`FB_MotorFwdRevSystem`
  untouched; its iDB start VALUES only — flagged loudly in §1 and §4), the "Output Mapping"
  retitle and all other queued cosmetic findings, `IO.InCycle`/`IO.Cycling` dead-member removals
  (queued interface rework), `simatic-ml/` (phase 2 re-exports own it).
- **Grounding:** `gen/GenProject1/requirements.md` @ `2295c64` (REQ-004/005/006/007/012/013/031/
  039/041/042/043/044/045/047/048/054/055/062/063, Q-02/Q-11/Q-04, NEW-1..NEW-4),
  `docs/notes/review-functional-validation-2026-07-16.md` (findings evidence),
  `docs/06-lad-conventions.md`, `ir/SPEC.md` (statement-kind ordering — every added/moved
  same-network dependency traced, §5).

## 1. Settings table — commissioning-default proposals (THE PHASE GATE)

**These are proposals.** The values are drafted into the IR (start values, C-309) so phase 2 can
import exactly what is signed; nothing is imported until this table carries the owner's
signature. Amend any number here and phase 2 edits the IR to match before import.

| # | Member (home) | Proposed | Rationale (spec citation where one exists) |
|---|---|---|---|
| 1 | `DB_Settings.PressureClearResumeDelay` | **5.0 s** | **Spec:** FuncDesc §"Sequence:" — "pressure … not high level + 5s time delay" (REQ-047). The one live-zero with a spec-stated number (Q-02). |
| 2 | `DB_Settings.DischargeConveyorTimeout` | **10.0 s** | Site practice (no spec number, REQ-004): generous DOL start-confirm window — covers contactor pull-in plus belt spin-up in case `DI10` proves to be a rotation sensor rather than an aux contact; a genuinely failed start still aborts within 10 s, long before the infeed is enabled. |
| 3 | `DB_Settings.PusherEndTravelTimeout` | **60.0 s** | Site practice (no spec number, REQ-054): sized to outlast the worst *tolerated* jam sequence — four confirmed pressure holds (0.5 s confirm + clear + 5 s resume each) plus travel — so REQ-048's five-trip blocked fault, not this timeout, decides a jammed cycle (the timer deliberately runs through holds); still bounds a silently-stuck stroke at one minute. |
| 4 | `DB_Settings.PusherParkedTimeout` | **30.0 s** | Site practice (no spec number, REQ-055): retract travel + margin; the retract stroke is never pressure-held (REQ-051), so no jam budget is needed. Alarm-only by design (no forced transition). |
| 5 | `DB_Settings.PusherPumpRunOnTime` | **5.0 s** | Site practice (no spec number, REQ-031): the run-on exists to carry the hydraulic motor across the push→retract transition — it must exceed the 2.0 s end-travel hold; 5 s also rides through brief pressure holds. Longer interruptions restart the pack safely (solenoids gate on `PowerPackRunningFB`). |
| 6 | `iDB_MotorFwdRevSystem_Shredder.IO.FTTime` | **10.0 s** | Site practice (NEW-1; REQ-007/057/063): fail-to-run/-stop confirm window must exceed the soft starter's ramp-to-feedback time; 10 s covers a typical ≤8 s ramp with margin. **Verify against the real starter's ramp at commissioning.** ⚠ IMPORTED iDB START VALUE — the imported FB's logic is untouched. |
| 7 | `iDB_MotorFwdRevSystem_Shredder.IO.ReverseIgnoreFT` | **12.0 s** | Site practice (NEW-1): the reversal fail-to-run/-stop mask is only effective if it exceeds `ReverseDelay` (8.0 s); 12 s = the full entry/spin-down pause + 4 s feedback-settling grace. Worst-case bounded step-30 dwell on a dead reverse ≈ 8 s pause + 10 s FTTime ≈ 18 s to a latched FTR and abort. ⚠ IMPORTED iDB START VALUE. |
| — | `iDB_….IO.EnableUPSTime` / `IO.ShutdownTime` | *(noted, no value)* | For completeness per the ruling: both are inert as wired — `UPSEnable` is superseded by the sequencer's own step-50 upstream timer (deliberate; seq N11 comment) and `Shutdown` is never commanded. Left unconfigured; configure only if those paths ever arm. |
| — | `DB_Settings.OvercurrentSetpointMedium/High`, `OvercurrentMediumDelay/HighDelay` | *(unconfigured, documented)* | Stay unconfigured **by design**: the overcurrent family is disarmed pending Q-11 (no AI hardware) and Q-04 (machine-type structure and per-type values). Member comments added in `DB_Settings.ir` state this at the member itself. Numbers now would imply protection that does not exist. |

Unchanged and re-verified against spec: `PreStartSounderTime` 10.0, `ShredderReverseRunTime` 6.0
(now buys *confirmed* reverse — §2.2), `InfeedRestartDelay` 5.0, `InfeedToUpstreamEnableDelay`
3.0, `OvercurrentSpinUpAllowance` 3.0, `ReversalRetryPauseTime` 4.0, `ReversalWindowTime` 180.0,
`ReversalCountThreshold` 5, `PressureTripConfirmTime` 0.5, `PressureTripCountThreshold` 5,
`PusherEndTravelHoldTime` 2.0, `PusherJogWarningTime` 1.0, `PusherFitted` TRUE.

### Settings sign-off (gates phase 2 — nothing is imported before this is signed)

- [ ] Timing defaults approved as tabled (or as amended below) — Engineer: ____________ Date: ____________
- Amendments: ____________

## 2. Per-ruling design notes

### 2.1 Ruling 1 — timing values

All five live-zeros carry proposed start values in their **current homes** (`DB_Settings` + the
imported iDB — the sub-struct migration is its own queued request); the four overcurrent members
gained member comments documenting *why* they stay unconfigured. The imported iDB edit is start
values only — two lines, nothing else in that file or the imported FB changed.

### 2.2 Ruling 2 — reverse run times confirmed feedback

Seq N9's `ReverseRunTimer` IN is now `IO.Step = 30 AND IO.ShredderRunRevFB` (new UDT input wired
from `DB_Input.Shredder_Run_Rev_FB`), so the spec's 6 s (REQ-005) counts actual reverse rotation:
step 30 now dwells through the motor FB's 8 s entry pause, reverse starts, feedback confirms, six
confirmed seconds elapse, then 30→40 — the NEW-3 structural conflict (6 s window swallowed by the
8 s pause) is gone without touching the imported FB. **The commanded-but-no-feedback dwell**
(C-122) is deliberately *not* re-timed in the sequencer: the motor FB's own fail-to-run window
(FTTime, masked by ReverseIgnoreFT) latches FTR → `FaultActive` → the *existing* step-30
`MotorFaultActive` exit aborts to idle — one interval, one owner (C-409), the same reliance
step 40 already has, documented in the network comment. Bounded worst case ≈ 18 s (8 s pause +
10 s FTTime) to a latched, annunciated fault.

### 2.3 Ruling 3 — stop/E-stop bundle

(a) `UDT_ShredderSequencerIO.SystemHealthy` (wired from `DB_Input.Control_Healthy`) joins
StopCmd exactly as ruled: `COIL StopCmd := IO.CycleStop OR NOT IO.DownstreamRunning OR NOT
IO.SystemHealthy` — an E-stop now aborts every step to idle, dropping all sequencer-side
commands and `MotorPreStartDoneCmd`.
(b) Stop reaches the pusher: seq N14's arbitration is now `MOVE(EN := StopCmd OR Step = 60,
IN := 0)` / `MOVE(EN := NOT (StopCmd OR Step = 60), IN := OperatorPusherMode)` — StopCmd forces
mode 0 exactly like the step-60 overcurrent force; a mid-cycle pusher retracts and parks via its
existing Off behavior, within the same scan (sequencer runs before the pusher copy/call in
FC_ControlMain).
(c) **RecentStart arming design** (strap removed): new sequencer output `MotorStartArm :=
IO.Step = 10` — one operator start press = one PreStart phase = one arming. FC_ControlMain N3
wires it as a plain-coil set-then-hand-off: `COIL IO.RecentStart := iDB_ShredderSequencer.IO.
MotorStartArm OR iDB_MotorFwdRevSystem_Shredder.IO.RecentStart`. The imported FB's N4
(`RecentStart := RecentStart AND NOT RunFwd AND NOT RunRev AND NOT FaultActive` — read-only
study) is the **only clearer**: once the motor runs or faults it clears the bit, and the FC coil
then just passes the cleared value through (0 OR 0), never fighting it — the FB's documented
"Set Bit … On … Start Buttons Press" contract honored with plain coils only (no SCOIL, so no new
C-403 startup-reset obligation). After an E-stop or fault, `TryRunMotor` needs `AutoStartSignal`
(steps 30..50) *and* the arm needs a fresh step-10 pass — two independent locks against
auto-restart (REQ-062). The FB's own Pasue/CycleDelay chain still carries the arm across the
reverse→forward direction change and the (disarmed) step-60 retry, as it was designed to.

### 2.4 Ruling 4 — jog/repark spec-true + Fitted gating

The unconditional idle-not-home auto-repark is gone. Repark (0→30) now fires **only** on
(i) the plant pre-start — new seq output `PusherParkCmd := Step >= 20 AND Step <= 40` (dedicated
member per the ruling; held through the machine-start ramp so the warned park reliably fires
even when the discharge confirm is quick; range intent stated per C-603) → new pusher input
`ParkCmd`; and (ii) a live auto/manual cycle request arriving with the ram off home — it reparks
first (warned), then launches if the request is still live at home. Verified: a launched cycle
ends parked by construction (10→20→30→0 at HomeLimit), so launch-from-home needs no extra park
logic. Jog release triggers nothing: the ram stops in place (REQ-039).
**Repark-warning design (NEW-4, no unwarned pusher motion):** the jog warning machinery is
generalized into a pre-motion warning — `TON(JogPreStartTimer, IN := ReparkRequest OR
LaunchRequest OR JogDemand, PT := JogWarningTimeMS)` with the sounder on the same three request
bits; **every** motion start from rest (repark, auto/manual launch, jog) is preceded by
`PusherJogWarningTime` (1.0 s) of sounder, and the step-0 transitions and jog demands act only
on the timer's Q. Jog keeps sounding while moving (REQ-041, unchanged); in-cycle strokes are
continuations of an already-warned launch (warning boundary = step 0). Names kept from the
jog-era (rename queued with the cosmetic pass).
**REQ-043/044, gated at the source:** `Fitted` now gates every demand term (cycle terms
explicitly, jog terms via `JogDemand`), `RunPowerPack` directly (run-on tail included), both
supervision timers' IN (`EndTravelTimer`, `ParkedTimer` — a disabled pusher looks for no switch
feedback), and **all four fault latches** — set *and* hold — (`BothSwitchesFault`, `Blocked`,
`EndTravelTimeoutFault`, `ParkedTimeoutFault`), so `FC_AlarmsMain`'s X3/X4/X5/X6 sources are
already-gated bits (checked: those bits' only sources are these latches) and a disabled pusher
raises nothing and clears anything previously latched. `FC_AlarmsMain` itself is untouched.
C-601 discipline: the shared compounds are named once in the new N1 (`CycleRequest`,
`JogDemand`, `ReparkRequest`, `LaunchRequest`), each with an interface-grade member comment.

## 3. Diffs summary (readable text; sidecar removal excluded — see §4)

| File | Networks touched | Readable delta | Substance |
|---|---|---|---|
| `UDT_ShredderSequencerIO.ir` | — | +4 members | `SystemHealthy`, `ShredderRunRevFB` (in), `MotorStartArm`, `PusherParkCmd` (out) — all with C-605 comments |
| `UDT_PusherIO.ir` | — | +1 member | `ParkCmd` (in) with C-605 comment — required by ruling 4(i): the pusher must receive the sequencer's park request through its own UDT (C-127) |
| `FB_ShredderSequencer.ir` | header, interface, N1, N9, N14 | 20 lines (14 non-comment) | StopCmd + healthy; ReverseRunTimer on confirmed feedback; StopCmd→mode-0 force; MotorStartArm/PusherParkCmd coils. Networks 2–8, 10–13, 15 textually identical |
| `FB_PusherControl.ir` | header, interface, N1, N2, N4(new), N5–N8, N10, N11, N13 | 77 lines (55 non-comment) | Named requests; generalized pre-motion warning; occasion-gated repark; Fitted gating of demands/pack/timers/latches. N3 (HMI times), N9 content, N12 textually identical (N5–N10 renumbered from old 4–9; old N10 absorbed into new N4) |
| `FC_ControlMain.ir` | header(new), N1, N3, N5 | 7 lines (5 non-comment) | +SystemHealthy/+ShredderRunRevFB wiring; RecentStart strap → MotorStartArm OR-hold (+C-604 comment naming the three deliberate `NOT AlwaysTrue` constants); +ParkCmd wiring; C-201 header added (standing finding, in-scope — block being edited). N2/N4/N6/N7 textually identical |
| `DB_Settings.ir` | — | 10 lines | 5 start values (§1), 4 overcurrent member comments, header comment updated to stay accurate |
| `iDB_MotorFwdRevSystem_Shredder.ir` | — | 2 lines | **START VALUES ONLY**: `FTTime = 10.0`, `ReverseIgnoreFT = 12.0` (⚠ imported-real iDB — nothing else touched) |
| `iDB_ShredderSequencer.ir` | — | +4 lines | Mirror of the UDT members (iDBs carry full interface copies) |
| `iDB_PusherControl.ir` | — | +5 lines | Mirror: `ParkCmd` + the four new named-request Statics. **Touch-list note:** this file was not in the stated allow-list but is the same mechanical mirror the list's iDB_ShredderSequencer entry anticipates — without it the instance no longer matches its FB. Flagged, not silent. |

**Sidecars:** the three edited code blocks' `SIDECAR` sections are stripped — that is the
established edit workflow (19b2022 precedent: edited readable IR → `to-xml --synthesize` →
import → compile → re-export → `to-ir` regenerates fresh sidecars). Phase 2's re-export restores
them; the raw-file diff's large deletion counts are this, not logic.

## 4. Verification evidence (static — phase 1 has no Portal contact)

- **Synthesis:** `converter to-xml <file> --synthesize` succeeds for all three edited code
  blocks (`FB_ShredderSequencer`, `FB_PusherControl`, `FC_ControlMain`); plain `to-xml` succeeds
  for both UDTs, `DB_Settings`, and all three iDBs. One synthesizer constraint was hit and fixed
  during drafting: `Fitted AND (X OR Y)` is not a legal ladder chain shape (a branch may only sit
  at the rail) — all five Fitted-gated latches/outputs are written `(X OR Y) AND Fitted`
  (identical semantics, real-ladder shape).
- **Preflight:** `converter preflight <all 9 files> --project ir/GenProject1` → **4 findings,
  all pre-existing** (see accepted list below); parse/convert/tag/call/instanceof all clean —
  every referenced tag root resolves (`DB_Input.Control_Healthy`, `DB_Input.Shredder_Run_Rev_FB`
  both exist; no proposed tag is referenced by any logic).
- **Mechanical review delta:** BEFORE (HEAD, all 9 files): 5 findings — C-201 ×4
  (FC_ControlMain + 3 iDBs), C-406 ×1 (imported TONR). AFTER: **4 findings** — FC_ControlMain's
  C-201 fixed (header added); **zero new findings**. (For the three sidecar-stripped code blocks
  the after-state review runs inside preflight — plain `review` parses exported sidecar'd IR
  only; preflight embeds the same rules for sidecar-less IR.)
- **Accepted pre-existing findings** (bar per the task: zero findings except pre-existing
  C-201s on blocks whose headers aren't being added):
  1. `iDB_MotorFwdRevSystem_Shredder` C-201 (no header comment) — imported-real iDB under a
     strict start-values-only allowance; adding a header exceeds it.
  2. `iDB_MotorFwdRevSystem_Shredder` C-406 (TONR_TIME member) — the imported FB's own retentive
     hours timer; imported-real internals are untouchable (documented context since the
     functional review).
  3. `iDB_ShredderSequencer` C-201 — pre-existing; iDB header comments belong to the queued
     documentation pass, not this ruling set (no drive-bys).
  4. `iDB_PusherControl` C-201 — same as 3.
- **Statement-kind ordering / same-scan freshness** (ir/SPEC.md) — every added or moved
  dependency traced: sequencer N14's MOVEs read N1's StopCmd (cross-network, fresh); FC N3's
  arm coil reads the sequencer call's N2 outputs (fresh) and the motor iDB's own last-call value
  (by design — the FB is the only clearer); pusher N1 requests → N4 warning (fresh) → N5
  count-reset / N7 transitions / N11 demands (all fresh); N3's MS shadows precede the N4 timer
  (fresh — the warning network was deliberately placed after HMI Times). Two deliberate
  one-scan-stale reads are documented in comments with their safety argument: `CycleRequest`
  reads `Blocked` (written later; can only latch mid-stroke where no step-0 request can act) and
  the request bits read `Step` (which only leaves 0 through this scan's own request-gated
  transitions); the jog demand rungs re-check `Step = 0` fresh to close the one theoretical
  both-solenoids scan.

## 5. Design tensions and observed consequences (flagged, not forced)

1. **REQ-037 narrowed by ruling 4:** selecting Off with the ram idle-but-off-home no longer
   self-parks (repark occasions are only pre-start / cycle entry, and Off has no cycle
   requests). Mid-cycle Off still retracts and parks as before. The ram parks at the next plant
   pre-start. If Off-must-park-immediately matters, it needs a further ruling (it would be a
   third repark occasion — and per NEW-4 it would be a *warned* one).
2. **Momentary manual-cycle tap:** a manual cycle press shorter than the 1 s warning no longer
   launches (request released mid-warning = no motion) — the price of NEW-4, consistent with
   jog's release-before-warning behavior. Auto (hopper level) is unaffected (level persists).
3. **Jog while stop is held:** StopCmd (stop button held, downstream absent, or unhealthy)
   forces mode 0, so jog is unavailable for the duration — the ruling's own mechanism. Notably,
   *downstream absent* therefore blocks jogging; if jog-for-testing must work with downstream
   stopped, that needs a ruling amendment.
4. **Auto cycles at plant-idle** (pre-existing, unchanged): once a stop is *released*, mode
   passes through again — auto+hopper-high or a manual press can run warned pusher cycles while
   the plant is idle, exactly as before this wave. Ruling 3(b) is a while-held force, not a
   plant-state interlock.
5. **Stale arm residue:** a start attempt that aborts between PreStart and the motor start
   (e.g. discharge timeout) leaves `RecentStart` armed until the next run/fault clears it —
   harmless (the arm acts only with `AutoStartSignal`, i.e. steps 30–50 of a fresh warned
   start), documented here rather than "fixed" with extra state.
6. **Fitted flipped FALSE mid-stroke:** outputs drop and faults clear immediately; `Step`
   freezes silently until re-enable or a mode exit. The REQ-042 scenario (never-fitted test
   machine) never reaches a stroke; the mid-stroke flip is an engineering action — noted in the
   block header, no extra machinery added.
7. **`iDB_PusherControl.ir` touched** beyond the stated file list — mechanical mirror of the
   allowed UDT/FB edits (see §3 note).

## 6. Phase-2 handoff

**Phase 2 does not start until §1 is signed.** On signature (with any amendments folded in):
import all nine files to the scratch project (`to-xml --synthesize` for the three code blocks),
compile (hard rule 4), run the untouched-network invariance check against this wave's readable
diffs (§3 names every touched network; everything else must prove identical), re-export →
`to-ir` to restore sidecars, then the adversarial reviewers (review-functional expecting
REQ-005/012/013/039/043/044/045/062 to move to implemented and the settings consequences to
clear; review-conventions; review-simplicity), then the docs/11 presentation. S9 sim candidates
carried from the functional review remain open (E-stop feedback-decay race, reversal-window
boundary, run-on continuity).
