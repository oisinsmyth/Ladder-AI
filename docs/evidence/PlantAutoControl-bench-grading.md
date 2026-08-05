# PlantAutoControl answer-key grading (S6-Killer-Plan, Phase 3)

**Grader** dispatch. Compares the blind-generated `gen/PlantAutoControl-bench/PlantAutoControl.ir`
(Candidate output, 20 networks) against the sealed answer key
`docs/evidence/PlantAutoControl-answerkey/PlantAutoControl.ir` (real `PlantAutoControl`, sanitized), REQ by
REQ and behavior by behavior, per `docs/notes/S6-Killer-Plan.md`. The answer key is a **functional
oracle, never a style oracle** — it is site code held to a lower bar (does not meet C-001). A
generated divergence toward cleaner/safer/more-compliant that loses no required function is an
IMPROVEMENT; the answer key "wins" a divergence only by REGRESSION (generated dropped a
load-bearing behavior).

Grading is behavioral (independent trace of each REQ through both blocks), not byte-diff. The two
blocks share the same invented-name namespace, so tag-level comparison is meaningful. `converter
diff` is not usable as a verdict here (the answer key encodes shared sub-expressions via
`split/recv` wire-sharing while the generated block writes flat expressions, so a mechanical diff
reports every network changed); the analysis below is a manual boolean trace, which the plan
requires.

## Integrity weighting (stated, applied to interpretation)

Per the presentation bundle: gate-1 sign-off was answer-key-biased, and blindness was **weaker on
pattern-shaped networks** because `patterns/chained-permissive-enable` was generalized *from* this
target and embeds real example networks **2, 7, 8, 12**. So a MATCH on a pattern-shaped network
proves little. The **informative** verdicts are on the freeform / decision networks the pattern did
not hand over: **1, 3, 4, 5, 6, 10, 11, 17, 19**. Every DEFECT, the REGRESSION, the IMPROVEMENT,
and all three owner-ruling divergences below fall in that informative bucket — which is the real
signal: on pattern-shaped machines the pipeline reproduced the answer key; on the freeform/decision
networks it diverged, sometimes toward a genuine gap and once toward a genuine fix.

---

## 1. Per-REQ verdict table

| REQ | Verdict | One-line rationale | Evidence |
|---|---|---|---|
| REQ-001 | MATCH | All 20 equipment PlantAutoControl networks present, each `CALL`s its FB in material-train order | both nets 1–20 |
| REQ-002 | MATCH | `Status >= 1` running-state start path present in every machine | e.g. gen net1 L21 / AK net1 L18 |
| REQ-003 | **REGRESSION** *(owner-contingent)* | Cascaded-shutdown neighbour-`ShutdownComplete` hold term dropped on the 3 filter units (nets 3/4/19); intact on the other 17 | gen L48/64/249 vs AK L47/62/250 |
| REQ-004 | MATCH | Downstream-ready `UPSEnable`/sub-system-enable start permissives identical on all machines | verified nets 1,5,6,8,9,10,14,15,16,18 |
| REQ-005 | MATCH | `PreStartDone := PlantControl.PreStartComplete` on every machine | all nets |
| REQ-006 | **IMPROVEMENT** *(confirm Q-07)* | Nets 10/11 gate auto-shutdown on the machine's **own** `HandIntervention`; the answer key uses a *different* machine's flag (Q-07 quirk). MATCH elsewhere | gen L144/155 vs AK L141/153 |
| REQ-007 | MATCH | `FaultReset := HMIControlSignals.SystemReset` on every machine | all nets |
| REQ-008 | MATCH | Every `DiscreteOutputs.*Start/RunFwd/RunRev/TomraRun := *.Run` mapping present | nets 2,3,4,7,8,9,11,12,13,14,15,16,17,19,20 |
| REQ-009 | MATCH | Field running feedbacks confirmed on all machines that have one (filter running-source pairing graded under REQ-019; nets 1/5/10 add extra running feedbacks — reverse pass) | — |
| REQ-010 | MATCH | `InhibitMotor := *IsoFB` on every isolated machine | nets 1,2,6,7,8,10,12,13,14,15,16,17,20 |
| REQ-011 | **DEFECT** *(partial, net17-rev)* | Feed-conveyor **reverse** running feedback drops the rotation-sensor+bypass motion confirmation the answer key applies; all forward belt conveyors MATCH | gen L223 vs AK L221 |
| REQ-012 | **DEFECT** | Discharge-VSD rotation-sensor bypass clause **not implemented**; `BypassAirStarDCRotSen` unreferenced, `RotationSensor` coil + S/R running latch dropped | gen L83 vs AK L79–82 |
| REQ-013 | MATCH | `MagEnable` / `DrumsEnable` / `GeneralEnable` sub-system gates present and correct | nets 7,12,13,20,17 |
| REQ-014 | **SPEC-GAP / OWNER RULING** | Link conveyor MATCH (JOB9001 factored, equivalent); incline feed **restructures** the JOB9001 hold — different signals, register text vs table tension, Q-03 inferred | gen L21–22 vs AK L18–19 |
| REQ-015 | MATCH | Auto-pre-start rising-edge one-shot preserved (`PositiveEdgeArray[4]` prev-scan latch, S/R on `AutoPreStart`); trigger simplified to `AutoStartSignal` (embeds JOB9001) — note | gen L36–38 vs AK L31–33 |
| REQ-016 | **OWNER RULING** — rec. REGRESSION | Feed-conveyor reverse changed from answer key's **latched** state (gated on running & not-in-hand) to a **stateless combinational** `Reverse`; core fwd/rev *direction* intent MATCH, the "only change direction while running & not under hand" gating dropped | gen L227 vs AK L224–225 |
| REQ-017 | **DEFECT** + SPEC-GAP | Net10 narrows the "not faulted" sorter interlock from FB `Outputs.FaultActive` to raw `TomraComFlt` (weaker); net11 sorter data words dropped (Q-06 SPEC-GAP) | gen L143/158 vs AK L140/155 |
| REQ-018 | MATCH | All ECS mode/fault/status inputs + auto-start/reset outputs mapped (same 11-signal set) | gen net9 vs AK net9 |
| REQ-019 | **OWNER RULING** — rec. DEFECT + SPEC-GAP | Dust-filter `RemoteOp`/`RunningFB` feedback pairing **swapped** vs answer key (nets 3/4); cyclone running feedback un-wireable given-boundary defect (net19, NEW-01, SPEC-GAP) | gen L43–44/58–59/245+ vs AK L40/44/55/59/246 |
| REQ-020 | MATCH | `MOVE NormalFanStartTime => EnableUPSTime` on nets 3/4/19, identical to answer key (incl. the C-308 scan-copy the answer key also has) | gen L53/68/254 vs AK L50/65/253 |
| REQ-021 | MATCH | Air-separator alternative start permissive (both OR paths) identical | gen L76 vs AK L72 |
| REQ-022 | MATCH | `SystemHealthy := TRUE` on every machine; no safety/E-stop signal touched | all nets |

---

## 2. Per-verdict evidence (every non-MATCH, both sides quoted)

### REQ-003 — REGRESSION (owner-contingent): FilterUnitSystem cascade hold term dropped (nets 3, 4, 19)

The answer key holds each filter running during controlled shutdown until **either** the global
fans-ready flag **or the specific dust-generating neighbour** has completed shutdown. The generated
block keeps only the global flag, dropping the neighbour link — exactly the cascade link REQ-003
specifies and the register inventory documents ("Holds through shutdown until: Fans-shutdown-ready /
Air-separator VSD shut down" for the dust filters; "/ Shredder shut down" for the cyclone).

Dust filter 1 (net 3):
- ANSWER KEY (L47): `COIL FilterUnitInst2.IO.AutoStartSignal := ((PlantControl.Status >= 1) OR (PlantControl.Status = -1) AND NOT PlantControl.FansShutdownReady OR (PlantControl.Status = -1) AND NOT AirStarInst1.Outputs.ShutdownComplete) AND MotorVSDInst3.IO.UPSEnable`
- GENERATED (L48): `COIL FilterUnitInst2.IO.AutoStartSignal := (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT PlantControl.FansShutdownReady) AND MotorVSDInst3.IO.UPSEnable`

Dust filter 2 (net 4): identical drop — AK L62 has `... OR (PlantControl.Status = -1) AND NOT AirStarInst1.Outputs.ShutdownComplete`; gen L64 omits it.

Cyclone (net 19):
- ANSWER KEY (L250): `COIL FilterUnitInst1.IO.AutoStartSignal := ((PlantControl.Status >= 1) OR (PlantControl.Status = -1) AND NOT PlantControl.FansShutdownReady OR (PlantControl.Status = -1) AND NOT ShredderControlInst1.Outputs.ShutdownComplete)`
- GENERATED (L249): `COIL FilterUnitInst1.IO.AutoStartSignal := PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT PlantControl.FansShutdownReady`

The same drop propagates into each unit's `Shutdown` coil (gen L49/64/250 negate the shorter
expression; AK L48/63/251 negate the full one).

**Concrete reachable failing condition:** controlled shutdown (`Status = -1`), `FansShutdownReady`
asserted while the air separator (dust filters) or shredder (cyclone) has **not** yet signalled
`ShutdownComplete`. The answer key keeps the dust filtration running; the generated block stops it,
leaving the still-running dust generator unfiltered. **Load-bearing** unless `FansShutdownReady` is
proven to already encode the neighbour completion (unknown — computed in another block, out of
scope). Because the register explicitly specified both terms and redundancy cannot be proven here,
the conservative verdict is REGRESSION; **owner-arbitration flagged** (see §4-A): if
`FansShutdownReady` provably subsumes the neighbour `ShutdownComplete`, this degrades to IMPROVEMENT
(redundant belt-and-suspenders removed).

### REQ-006 — IMPROVEMENT (nets 10, 11): own-machine hand suppression vs answer key's cross-machine flag

REQ-006 requires each machine's auto-shutdown to be suppressed under **its own** hand intervention.
The answer key references a *different* machine's `HandIntervention` in two networks (register Q-07,
logged as a probable copy/paste defect). The generated block uses each machine's own flag.

Sorter Conveyor VSD (net 10):
- ANSWER KEY (L141): `... AND NOT TomraControlInst1.Inputs.HandIntervention AND PlantControl.Status = -1` — the Optical Sorter's hand flag inside the **Sorter Conveyor VSD's** shutdown.
- GENERATED (L144): `... AND NOT MotorVSDInst3.IO.HandIntervention AND PlantControl.Status = -1` — its own.

Optical Sorter (net 11):
- ANSWER KEY (L153): `... AND NOT MotorStarterInst6.IO.HandIntervention AND PlantControl.Status = -1` — the Ejected Material Conveyor's hand flag inside the **Optical Sorter's** shutdown.
- GENERATED (L155): `... AND NOT TomraControlInst1.Inputs.HandIntervention AND PlantControl.Status = -1` — its own.

**Functional superset argument:** under the answer key, putting the Sorter Conveyor VSD itself into
hand does **not** suppress its auto-shutdown cascade (the miss REQ-006 warns against), and hand on
the *sorter* wrongly suppresses the *conveyor's* shutdown. The generated block gives exactly the
per-machine behavior REQ-006 states — equivalent-or-safer, no function lost. **Rule/argument:**
REQ-006 (+ the register's own Q-07 finding, a C-127 sibling-reference anomaly). Recommended
IMPROVEMENT, **contingent on the owner confirming Q-07** is a defect and not intentional grouped-hand
behavior (the register left it open).

### REQ-011 — DEFECT (net 17, reverse feedback): motion confirmation dropped on reverse

- ANSWER KEY (L221): `COIL MotorFwdRevInst1.IO.RunningFBRev := DiscreteInputs.SFCRunningRev AND DiscreteInputs.SFCRotSen OR DiscreteInputs.SFCRunningRev AND HMIControlSignals.BypassSFCRotSen`
- GENERATED (L223): `COIL MotorFwdRevInst1.IO.RunningFBRev := DiscreteInputs.SFCRunningRev`

The forward feedback keeps the sensor confirmation on both sides (gen L222 == AK L219). Only the
**reverse** feedback drops it. **Concrete reachable condition:** feed conveyor commanded reverse,
`SFCRunningRev` asserted but the belt physically stalled (`SFCRotSen` false, not bypassed) — the
answer key withholds "running-reverse" confirmation, the generated block asserts it, masking a
stalled belt in reverse. Lower severity than a forward-direction miss and clusters with the net-17
freeform rework (REQ-016); owner may downgrade, but it is a dropped interlock the answer key had.

### REQ-012 — DEFECT (net 6): discharge-VSD rotation-sensor bypass clause unimplemented

REQ-012: running-forward is derived from the rotation sensor "unless that sensor is bypassed, in
which case it is not used."

- ANSWER KEY (L79–82):
  `COIL MotorVSDInst1.IO.RotationSensor := NOT HMIControlSignals.BypassAirStarDCRotSen`
  `SCOIL MotorVSDInst1.IO.RunningFwdFB := NOT HMIControlSignals.BypassAirStarDCRotSen AND DiscreteInputs.AirStarDCRotSen`
  `RCOIL MotorVSDInst1.IO.RunningFwdFB := NOT HMIControlSignals.BypassAirStarDCRotSen AND NOT DiscreteInputs.AirStarDCRotSen`
- GENERATED (L83): `COIL MotorVSDInst1.IO.RunningFwdFB := DiscreteInputs.AirStarDCRotSen`

The generated block wires the sensor **unconditionally**, drops the `RotationSensor` interface coil,
and never references `BypassAirStarDCRotSen` (a real tag — `HMIControlSignals.ir` L… `BypassAirStarDCRotSen`).
**Concrete reachable condition:** operator sets `BypassAirStarDCRotSen` (failed sensor / commissioning)
— the answer key freezes `RunningFwdFB` off the sensor and tells the FB the sensor is bypassed; the
generated block still drives `RunningFwdFB` straight from the (failed) sensor, so a bypassed/failed
rotation sensor forces a false "not running forward." This removes required operator capability — it
cannot be an IMPROVEMENT. This is the bundle's acknowledged "one genuine block-authored functional
gap." Firm DEFECT.

### REQ-014 — SPEC-GAP / OWNER RULING (net 1): incline-feed JOB9001 hold restructured

Link conveyor (net 2) is functionally equivalent (answer key distributes `JOB9001Interlock` across
both `Status` paths; generated factors it out — `(J AND A) OR (J AND B) == J AND (A OR B)`):
- ANSWER KEY (L29): `... := (InterlockData.JOB9001Interlock AND (PlantControl.Status >= 1) OR InterlockData.JOB9001Interlock AND (PlantControl.Status = -1) AND NOT InterlockData.JOB9001ShutdownComplete) AND MotorVSDInst2.IO.UPSEnable`
- GENERATED (L32): `... := (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT InterlockData.JOB9001ShutdownComplete) AND MotorVSDInst2.IO.UPSEnable AND InterlockData.JOB9001Interlock`  → MATCH (cleaner factoring, C-601).

Incline feed (net 1) genuinely diverges — different signals in the shutdown hold:
- ANSWER KEY (L18): `COIL MotorVSDInst2.IO.AutoStartSignal := ((PlantControl.Status >= 1) OR (PlantControl.Status = -1) AND InterlockData.JOB9001Interlock AND NOT MotorStarterInst4.IO.ShutdownComplete) AND AirStarInst1.Outputs.EnableUPS`
- GENERATED (L21): `COIL MotorVSDInst2.IO.AutoStartSignal := (PlantControl.Status >= 1 OR (NOT MotorStarterInst4.IO.ShutdownComplete OR NOT InterlockData.JOB9001ShutdownComplete) AND PlantControl.Status = -1) AND AirStarInst1.Outputs.EnableUPS`

Answer key hold (during `Status = -1`) = `JOB9001Interlock AND NOT MotorStarterInst4.ShutdownComplete`
(interlock present AND link conveyor not yet down). Generated hold = `NOT MotorStarterInst4.ShutdownComplete
OR NOT JOB9001ShutdownComplete` (link not down OR third-party not done) — it introduces
`JOB9001ShutdownComplete` (which the answer key net 1 never uses) and drops the "interlock present"
gate. The register **text** REQ-014 ("keep running until that third-party plant signals its own
shutdown is complete") actually supports the generated reading, while the register **inventory
table** (row 1: "Holds through shutdown until: Link Conveyor shut down; extra gate JOB9001 interlock")
supports the answer key. This is a register text-vs-table tension over **inferred** JOB9001 semantics
(Q-03). Not chargeable to generation as a clean DEFECT → SPEC-GAP; **owner-arbitration flagged**
(§4-B).

### REQ-016 — OWNER RULING, rec. REGRESSION (net 17): reverse latch → combinational

- ANSWER KEY (L224–225): reverse is a **latched** state:
  `RCOIL MotorFwdRevInst1.IO.Reverse := ((PlantControl.Status >= 1 AND NOT MotorFwdRevInst1.IO.InHand AND MotorFwdRevInst1.IO.RunningFBFwd) OR PlantControl.Status >= 1 AND NOT MotorFwdRevInst1.IO.InHand AND MotorFwdRevInst1.IO.RunningFBRev) AND ShredderControlInst1.Outputs.UPSEnable`
  `SCOIL MotorFwdRevInst1.IO.Reverse := (PlantControl.Status >= 1 AND NOT MotorFwdRevInst1.IO.InHand AND MotorFwdRevInst1.IO.RunningFBFwd OR PlantControl.Status >= 1 AND NOT MotorFwdRevInst1.IO.InHand AND MotorFwdRevInst1.IO.RunningFBRev) AND NOT ShredderControlInst1.Outputs.UPSEnable`
- GENERATED (L227): `COIL MotorFwdRevInst1.IO.Reverse := NOT ShredderControlInst1.Outputs.UPSEnable AND PlantControl.GeneralEnable`

The core direction intent (forward when shredder ready-to-receive, reverse when not) is preserved.
But the answer key changes direction **only while the conveyor is actually running (`RunningFBFwd`
OR `RunningFBRev`) and not in hand (`NOT InHand`)** — exactly REQ-016's "the direction only changes
while the conveyor is already running automatically and is not under hand control," and the register
C-113 table classifies this as the block's one **latched** stateful element. The generated block
computes `Reverse` combinationally with **neither** gate. **Concrete reachable condition:** operator
runs the feed conveyor forward in hand; the shredder drops ready — the generated block asserts
`Reverse` and (subject to the FB) can reverse against the operator; the answer key holds the latched
direction because `NOT InHand` blocks the change. Also, stopped + shredder-not-ready presets
`Reverse` in the generated version. Whether the drop is fully load-bearing depends on
`MotorFwdRevSystem` internally honoring `InHand`/pause (the bundle asserts C-117 pause is inside the
FB; unverified, NEW-03 sim candidate). Recommended REGRESSION, **owner-arbitration flagged** (§4-C).
Highest-informative network (declared freeform).

### REQ-017 — DEFECT (net 10) + SPEC-GAP (net 11)

Net 10 "not faulted" interlock narrowed:
- ANSWER KEY (L140): `... AND TomraControlInst1.HardWireSignals.Ready AND NOT TomraControlInst1.Outputs.FaultActive AND MotorStarterInst6.IO.UPSEnable AND MotorStarterInst7.IO.UPSEnable`
- GENERATED (L143): `... AND DiscreteInputs.TomraReady AND MotorStarterInst6.IO.UPSEnable AND MotorStarterInst7.IO.UPSEnable AND NOT DiscreteInputs.TomraComFlt`

The readiness halves are equivalent (`HardWireSignals.Ready := DiscreteInputs.TomraReady`, net 11
L149/gen L150). The fault halves are **not**: the answer key gates on the FB's aggregated
`Outputs.FaultActive`; the generated block gates only on the raw comms fault `TomraComFlt`.
**Concrete reachable condition:** the sorter raises a non-comms fault (`FaultActive` true, `ComFlt`
false) — the generated block still permits the sorter-feed conveyor (`MotorVSDInst3`) to start and
deliver material onto a faulted sorter; the answer key blocks it. This is a weakened, not cleaner,
interlock → DEFECT, contingent only on `FaultActive` aggregating more than `ComFlt` (it is a
distinct FB output, so almost certainly yes). Cross-listed in §4-D for owner downgrade if
`FaultActive ≡ ComFlt`.

Net 11 sorter data words dropped:
- ANSWER KEY (L155): `CALL TomraControlSystem(TomraControlInst1, EN := TRUE, inputWord0 := Tag_45, ... inputWord7 := Tag_52, OutputWord0 => Tag_53, OutputWord1 => Tag_54)`
- GENERATED (L158): `CALL TomraControlSystem(TomraControlInst1, EN := TRUE)`

The register **Q-06** explicitly declares the 8-in/2-out data-word interface ambiguous and possibly
out of scope ("read as a given comms buffer, not a derivable requirement"). Divergence traces to the
register being silent → **SPEC-GAP**, not chargeable to generation. Owner note: if the real
`TomraControlSystem` needs those words to function, a production regeneration would require them —
this is spec/RFI signal, not a pipeline bug.

### REQ-019 — OWNER RULING rec. DEFECT (nets 3, 4) + SPEC-GAP (net 19)

Dust-filter feedback pairing swapped:
- ANSWER KEY (net 3, L40, L44): `COIL FilterUnitInst2.RemoteOp := DiscreteInputs.FilterUnit1Ready` / `COIL FilterUnitInst2.IO.RunningFB := DiscreteInputs.FilterUnit1Op`
- GENERATED (net 3, L43, L44): `COIL FilterUnitInst2.RemoteOp := DiscreteInputs.FilterUnit1Op` / `COIL FilterUnitInst2.IO.RunningFB := DiscreteInputs.FilterUnit1Ready`

(Net 4 identical: AK L55/L59 vs gen L58/L59.) The answer key maps `Op → RunningFB` and `Ready →
RemoteOp`; the generated block maps `Op → RemoteOp` and `Ready → RunningFB`. Both fault mappings
agree (`Flt → FaultFB`). The register named the three feedbacks as "remote-operational, running,
fault" without pinning the pairing, and the tag names are ambiguous ("Op" reads as *operational*
→ the generated `RemoteOp` pairing, **or** as *operating/running* → the answer key's `RunningFB`
pairing). **Functional consequence:** `RunningFB` seeds the fan up-to-speed timing (REQ-020 →
`UPSEnable`). If `Op` genuinely means "running" (answer key), the generated block starts the 10 s
fan timer from *Ready* — before the fan physically runs — enabling downstream early. That argues the
answer key is functionally right → recommended DEFECT; but the register's silence + tag-name
ambiguity is a real SPEC-GAP. **Owner-arbitration flagged** (§4-E).

Cyclone running feedback un-wireable (net 19):
- ANSWER KEY (L246): `COIL FilterUnitInst1.IO.RunningFB := DiscreteInputs.CycloneDustAutoRunning/Stop`
- GENERATED (L244–245, comment + no coil): documents that `DiscreteInputs.CycloneDustAutoRunning/Stop`
  contains a `/` (confirmed literally in `Input.ir`: `CycloneDustAutoRunning/Stop : Bool`), a C-005
  violation in the **given** DB that makes the member un-referenceable in IR.

This is a **given-boundary defect** (NEW-01), not a generation fault → **SPEC-GAP**. The generated
block correctly flagged it rather than inventing a name. Note: the answer key's own IR carries the
same illegal token — it is faithful to a broken source, not a behavior the generated block can
reproduce cleanly.

---

## 3. Reverse pass — answer-key behaviors not driven by a REQ

Hunting for dropped load-bearing behaviors and for unrequested additions:

- **Dropped, load-bearing → caught above:** filter cascade hold terms (REQ-003); discharge-VSD
  `RotationSensor` coil + S/R running latch + bypass (REQ-012); feed-conveyor reverse latch gating
  (REQ-016) and reverse motion confirm (REQ-011); sorter `FaultActive` interlock (REQ-017); sorter
  data words (REQ-017/Q-06).
- **Changed, "fixed" → IMPROVEMENT:** cross-machine `HandIntervention` on nets 10/11 (REQ-006).
- **Answer-key structural idiom not reproduced, but functionally equivalent:** the answer key shares
  the run-state sub-expression between `AutoStartSignal` and `Shutdown` via `split/recv` wire reuse;
  the generated block re-writes the expression in both coils. Boolean-equivalent on all 17
  non-divergent machines (verified). No behavior lost. (On net 1/net 2 the shared expression itself
  differs — graded under REQ-014.)
- **Unrequested additions (gold-plating, benign):** the generated block adds running feedbacks the
  answer key leaves unwired — net 1 `MotorVSDInst2.IO.RunningFwdFB := DiscreteInputs.IFCRotSen`
  (L17), net 5 `AirStarInst1.ComsInputs.MachineRunning := DiscreteInputs.AirStarRunning` (L73), net
  10 `MotorVSDInst3.IO.RunningFwdFB := DiscreteInputs.OSCRotSen` (L139). All three source tags exist
  in `Input.ir` (grep-verified — not invented, hard rule 3 clean). These wire real running signals
  into FB inputs the answer key defaults; plausible and not wrong, but unrequested (REQ-009's source
  networks exclude 1, 5, 10). Minor concern: net 1 adds the rotation-sensor feedback **without** the
  `BypassIFCRotSen` term (that bypass tag exists — `HMIControlSignals.ir` L28), an incomplete addition;
  since the answer key has nothing there, it is not a regression, but it is inconsistent with the
  bypass treatment the block applies elsewhere. Recorded for the owner; not a fail.
- **No missing networks, no extra networks:** both blocks are exactly 20 machine networks; the
  generated block invented no equipment. No safety/F-content in either (hard rule 2 clean).

---

## 4. Owner-arbitration flags (recommended verdict each — not auto-ruled)

- **A. REQ-003 filter cascade hold (nets 3/4/19) — recommend REGRESSION.** Hinges on whether
  `PlantControl.FansShutdownReady` (computed in another block, out of scope) already encodes the
  neighbour `ShutdownComplete`. If it provably does → IMPROVEMENT (redundant term removed). If not →
  REGRESSION (dust filtration can stop while its dust generator still runs). Conservative default:
  REGRESSION.
- **B. REQ-014 incline-feed JOB9001 hold (net 1) — recommend SPEC-GAP.** Register text (REQ-014)
  supports the generated `JOB9001ShutdownComplete` reading; register inventory table + Q-03 support the
  answer key's `JOB9001Interlock AND link-conveyor-complete` reading. Genuinely inferred process
  semantics — the register's own tie is unresolved (Q-03). Owner confirms which JOB9001 semantics are
  real.
- **C. REQ-016 feed-conveyor reverse paradigm (net 17) — recommend REGRESSION.** The answer key gates
  direction change on running & `NOT InHand`; the generated combinational `Reverse` does not. Load-
  bearing **iff** `MotorFwdRevSystem` does not itself suppress `Reverse` under hand/while stopped.
  Resolve by inspecting the FB or by S9 sim (NEW-03). If the FB fully guards it → IMPROVEMENT
  (simpler, delegated). Absent that proof → REGRESSION.
- **D. REQ-017 sorter fault interlock (net 10) — recommend DEFECT.** `Outputs.FaultActive` (answer
  key) vs raw `TomraComFlt` (generated). DEFECT unless `FaultActive ≡ ComFlt` inside
  `TomraControlSystem` (unlikely — distinct aggregated output). Owner confirms the FB's fault
  aggregation.
- **E. REQ-019 dust-filter feedback pairing (nets 3/4) — recommend DEFECT.** `RemoteOp`/`RunningFB`
  swapped vs answer key; register silent on the pairing and tag names ("Op"/"Ready") ambiguous.
  DEFECT if `Op` is the true running signal (fan up-to-speed timing depends on it); SPEC-GAP if the
  register's silence is deemed the cause. Owner confirms the real signal meaning.
- **F. REQ-006 own-hand fix (nets 10/11) — recommend IMPROVEMENT (confirm Q-07).** The generated
  block is correct per REQ-006; the answer key's cross-machine flag is Q-07, logged as "possible
  defect, needs owner ruling." IMPROVEMENT stands unless the owner declares the grouped-hand behavior
  intentional (in which case MATCH-with-note, still not a fail).

---

## 5. Scorecard

Counting the **recommended** verdict for each REQ (owner-contingent items marked):

| Verdict | Count | REQs |
|---|---|---|
| MATCH | 14 | 001, 002, 004, 005, 007, 008, 009, 010, 013, 015, 018, 020, 021, 022 |
| IMPROVEMENT | 1 | 006 *(confirm Q-07)* |
| DEFECT | 3 | 011 *(partial, net17-rev)*, 012 *(firm)*, 017 *(+ SPEC-GAP data words)* |
| REGRESSION | 1 | 003 *(owner-contingent A)* |
| SPEC-GAP / OWNER RULING | 3 | 014, 016 *(rec. REGRESSION)*, 019 *(rec. DEFECT + SPEC-GAP)* |

**Headline: FAIL the "MATCH-or-better on all required behavior" bar.** The generated block is
**not** free of DEFECT/REGRESSION: **1 firm DEFECT (REQ-012 bypass), 2 owner-contingent DEFECTs
(REQ-011, REQ-017), 1 owner-contingent REGRESSION (REQ-003)**, plus 3 owner-ruling divergences
(REQ-014, REQ-016, REQ-019) any of which the owner could rule DEFECT/REGRESSION or MATCH. It earns
**1 confirmed IMPROVEMENT (REQ-006)** — a real, safety-relevant fix of a site quirk.

**What this says about the pipeline (integrity-weighted):** on the pattern-shaped machines (nets 2,
7, 8, 12 and the DOL/starter family) the generated block reproduced the answer key faithfully —
low-information MATCHes. Every gap and the one win landed on the **freeform / decision networks**
(1, 3/4, 6, 10, 11, 17, 19), where pattern support was thin — i.e. the pipeline reproduces
pattern-covered logic well but, on freeform interlock detail, systematically **weakened
interlocks** (dropped the second cascade hold on all three filters; dropped a bypass; narrowed a
fault interlock; de-latched a direction interlock) while also **correcting** one real site defect
(per-machine hand). That is the honest measured signal: composition-from-patterns is strong;
freeform interlock reconstruction is the exposed edge, and it errs toward under-constraining rather
than over-constraining.

Evidence for every verdict is the both-sides quoted IR above (this file), not this summary.
