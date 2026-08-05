# PlantAutoControl-bench-rerun — Code structure (rung D)

Produced by `/gen-code-structure` (rung D of the structured spec pipeline A→B→C→D), running inside
the `lad-coder` sub-agent (hard rule 8). **This rung structures; it does not code.** No `.ir` was
written.

> ## RUN STATUS — D3 (render) STOPPED. D0, D1, D2 COMPLETE.
>
> The skill's stop condition is: *"a spec carrying an unresolved BLOCKING `Q-nn` that affects logic
> you would render → stop and report; never render a guess for a contested requirement."*
> **All six specs carry unresolved blocking questions that land directly on terms of their own
> automatic-control network.** D3 is therefore not produced for any instance. What each render is
> waiting on is itemised in §D3 below, term by term, so the stop is actionable rather than a refusal.
>
> D0 (shape), D1 (block fit, binding audit, undriven-input audit) and D2 (discharge ledger) are
> complete, because none of them requires guessing a contested term — and the two audits are where
> this run's most consequential findings are, including two **HARD FAIL** conditions that would have
> shipped in a render.

## Provenance

- **Produced:** 2026-08-04.
- **Inputs:** `gen/PlantAutoControl-bench-rerun/equipment-specs/*.md` (six specs, rung C);
  `gen/PlantAutoControl-bench-rerun/requirements.md` (derived register, 166 relations);
  library blocks `ir/PlantAutoControl-bench/FilterUnitSystem.ir`, `MotorVSDSystem.ir`, `TomraControlSystem.ir` read in full
  as interface ground truth, plus the six instance DBs and the six global DBs;
  `docs/06-lad-conventions.md` read fresh.
- **`patterns/`:** does not exist in this repo. No pattern vocabulary was available; block fit was
  done against the library blocks directly. Recorded, not worked around.
- **Safety:** no F-block, F-runtime or safety-program content read or referenced. The E-stop members
  in the given DBs are untouched (hard rule 2).
- **Tags:** nothing invented. Every signal named here was verified `exists` at rung C
  (`converter tagstatus`, 0 proposed). No DB number, address or block number is assigned by this rung.

---

# D0 — Logic shape declarations

Declared **before** any rendering, so that "I see a pattern for this" is a decision rather than an
accident. Each shape states what it discharges, the argument, and the preconditions — with every
precondition classified `verified-in-block` / `verified-cross-block` / `unverifiable`.

### S1 — per-instance combinational permissive chain  (shape only; DISCHARGES NOTHING)

- **Structure:** one network per instance — drive the FB's inputs from plant state, neighbour
  interface members and field IO; call the FB; drive the field outputs from the FB's outputs.
- **What it discharges:** **nothing.** It is the rendering shape, not an argument that any relation
  is already satisfied. Declared explicitly so that no later step can quietly treat "it's
  combinational" as a reason to drop a term.
- **Argument:** every rung-C relation is a current-state test; no relation in the six specs needs to
  know which phase the plant is in.
- **Preconditions:** the stateful behaviour (start latch, pre-start memory, shutdown timing, fault
  latching) lives inside the equipment FBs — **`verified-in-block`**: `FilterUnitSystem.ir` N1, N5, N7,
  N8–N10; `MotorVSDSystem.ir` N1, N5, N8, N10–N12; `TomraControlSystem.ir` N6, N10, N12–N15.

### S2 — whole-block reuse of the given equipment FBs  (ACCEPTED DISCHARGE)

- **What it discharges:** the class requirement set each FB implements internally — see the D2
  ledger's `in-FB` rows (96 of the 166 relations).
- **Argument:** each requirement is implemented by a named network of the FB the instance is an
  instance of; the orchestration layer only drives inputs and reads outputs.
- **Preconditions:** every cited network exists and implements the behaviour claimed —
  **`verified-in-block`**, per-row citations in D2.
- **C-606 whole-reuse exception applied:** all three FBs carry considerably MORE than their specs
  require (hours-run totalising, HMI telemetry words, alarm words, speed reference and reverse
  direction on the VSD, a 3-word alarm map on the sorter). Extra capability in a proven library block
  is a valid fit and is **not** flagged as gold-plating.

### S3 — REJECTED DISCHARGE: "the plant fans-shutdown-ready flag subsumes the neighbour's shutdown-complete"

- **Proposal considered:** for `FilterUnitInst1/2/3`, relations P2 (`PlantControl.FansShutdownReady`)
  and P3 (the neighbour's `.ShutdownComplete`) are both shutdown holds on the same machine. P2 could
  plausibly already encode P3, letting one term stand for both.
- **Argument offered:** plausibility only — a plant-level "fans may now shut down" flag *sounds like*
  it is raised once the machines the fans serve have stopped.
- **Precondition it depends on:** that `PlantControl.FansShutdownReady` is computed from those
  neighbours' shutdown-complete.
- **Precondition class: `unverifiable`.** Checked directly: `FansShutdownReady` is *declared* in
  `ir/PlantAutoControl-bench/Control.ir` (line 14) and **written by no block inside this boundary** — a
  grep for coil/move targets across every `.ir` in `ir/PlantAutoControl-bench/` returns no writer. Its
  computation lives in a block outside this generation's scope, so the claim cannot be checked from
  any artifact in scope.
- **VERDICT: DISCHARGE REJECTED. P2 and P3 render as two separate terms on all three filter units.**
  This is the exact shape of the documented regression the skill warns about; the rejection is
  recorded rather than the conclusion, so the reasoning is auditable. Plausibility is not
  verification.

### S4 — REJECTED DISCHARGE: "a neighbour's enable already implies the plant is running"

- **Proposal considered:** drop P4 (auto-start commanded while the plant run state is running) on any
  instance whose start permissive is a neighbour's `.UPSEnable`, since a neighbour cannot be enabled
  unless the plant is running.
- **Precondition it depends on:** that every neighbour's enable is itself gated by the plant run
  state.
- **Precondition class: `unverifiable`, and circularly so.** A neighbour's `IO.UPSEnable` is computed
  in its FB from `IO.Run AND IO.RunningFB` (`FilterUnitSystem.ir` N11, `MotorVSDSystem.ir` N13,
  `TomraControlSystem.ir` N16 — that much is `verified-in-block`), but `IO.Run` traces back to
  `IO.AutoStartSignal`, which is driven by **the orchestration block this rung is structuring**. The
  argument depends on the artifact under construction.
- **VERDICT: DISCHARGE REJECTED. P4 renders on all six instances.** Independently fatal for
  `FilterUnitInst1`, which has no neighbour permissive at all (A Q-04) and which the argument could
  therefore never have covered.

### S5 — reverse relations render in the neighbour's network  (CONDITIONALLY ACCEPTED)

- **What it discharges:** a `REVERSE` relation (`this machine's enable is a start permissive of X`;
  `X holds through shutdown until this machine completes`) is the same fact as X's own forward
  relation. Rendering it in both networks would double-drive.
- **Argument:** the fact is rendered exactly once, in X's network.
- **Precondition:** X's own spec exists and carries the forward relation.
  - Where X is one of the six in scope → **`verified-cross-block`**, citing the sibling spec and its
    relation id. **Discharge accepted** (4 relations).
  - Where X is outside this run — `AirStarInst1`, `MotorStarterInst8`, `MotorStarterInst6/7`,
    `ECSControlInst1`, `ShredderControlInst1` → **`unverifiable`**: no spec exists for those machines,
    so nothing confirms the fact will be rendered anywhere. **Discharge refused.** Those relations are
    carried as explicit `out-of-scope-obligation` rows (8 relations) — a handoff the engineer must
    close before coding, not a silence.

---

# D1 — Block fit, grouping, and the two mandatory audits

## Block fit (reuse-first)

| class | library block | coverage | instances (grouped from rung A's per-instance expansion) |
|---|---|---|---|
| `FilterUnitSystem` | `FilterUnitSystem` (`ir/PlantAutoControl-bench/FilterUnitSystem.ir`, FB, 14 networks) | full class set; extra capability accepted (C-606) | `FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1` |
| `vsd-motor` | `MotorVSDSystem` (`MotorVSDSystem.ir`, FB, 16 networks) | full class set except VSD-17 (rotation sensor), which the FB declares but never reads — must be wired by orchestration | `MotorVSDInst1`, `MotorVSDInst3` |
| `optical-sorter` | `TomraControlSystem` (`TomraControlSystem.ir`, FB, 20 networks) | full class set; OS-18 declared-but-unimplemented inside the FB | `TomraControlInst1` |

No new FB type is needed. All three FBs and all six instance DBs already exist in
`ir/PlantAutoControl-bench/` — this is pure reuse. The only new block is the orchestration layer that
calls them; it is named `proposed` and **no block number or DB number is assigned by this rung**
(hard rule 3).

**Settings honour rung C's owner field.** Every HMI-owned setting (`FTTime`, `EnableUPSTime`,
`ShutdownTime`, `StartUpTime`, `AutoSpeedInput`, `HandSpeedInput`, `ProgramSelection`) is left at its
instance-DB default and **never cyclically copied onto** — that is the C-308 scan-copy trap, and it
would also make the HMI's own writes invisible. The one engineering-owned exception is the filter
units' fan up-to-speed time, which rung C binds to `ProcessTimings.NormalFanStartTime`; that is a
genuine plant-parameter drive, and it collides with the instance default of the same value (Q-C10).

## Binding audit (mandatory — rung C chose bindings before the FB interface was ground truth)

| relation | rung-C binding | FB-exposed alternative | what the FB does with each | verdict |
|---|---|---|---|---|
| `MotorVSDInst3` P2 — "Optical Sorter free of faults" | `DiscreteInputs.TomraComFlt` (raw comms-fault input) | `TomraControlInst1.Outputs.FaultActive` | `TomraComFlt` reaches only `Outputs.OSComFault` (`TomraControlSystem.ir` N2) — a comms indication. `Outputs.FaultActive` (N15) ORs fail-to-run, fail-to-stop, life-bit loss **and fourteen machine-reported status bits**. | **REBIND to `Outputs.FaultActive`.** Strictly broader guard. The raw input alone would permit the conveyor to keep feeding a sorter reporting a machine fault over a healthy link. Q-C17 downgraded from BLOCKING to a confirmation. |
| `MotorVSDInst3` P1 — "Optical Sorter reports itself ready" | `DiscreteInputs.TomraReady` | `TomraControlInst1.Outputs.UPSEnable` | `TomraReady` is a hardwired input read into `HardWireSignals.Ready`. `Outputs.UPSEnable` (N16) requires the sorter's comms ready bit **and** confirmed running **and** the up-to-speed timer, and is withdrawn immediately on loss of running. | **REBIND to `Outputs.UPSEnable`**, and **keep `TomraReady` as a second term** — rung A recorded the readiness gate as an *additional* delta, so collapsing the two would be an undeclared discharge. Two terms, not one. |
| all "start only while X is enabled" relations | rung A process language ("X is enabled") | `<XInstance>.IO.UPSEnable` | each FB computes `UPSEnable` from confirmed running plus its up-to-speed timer (`FilterUnitSystem.ir` N11, `MotorVSDSystem.ir` N13, `TomraControlSystem.ir` N16) | **CONFIRMED** — the enable member is the intended binding for every permissive of this shape. No rebind needed. |
| all "hold until X reports shutdown complete" relations | rung A process language | `<XInstance>.IO.ShutdownComplete` | computed in `FilterUnitSystem.ir` N7 / `MotorVSDSystem.ir` N8 / `TomraControlSystem.ir` N12 | **CONFIRMED.** Note the sorter's has no fault-escape term (its class delta D6), so a faulted `TomraControlInst1` never releases `MotorVSDInst3`. Carried, not resolved. |
| `FilterUnitInst2/3` C5 / C14 — remote mode vs running | candidate set `{*Ready, *Op}`, unresolved | `RemoteOp` and `IO.RunningFB` | the FB confirms two **distinct** roles exist and are used differently (`RemoteOp` gates `TryRunMotor` in N1; `RunningFB` drives shutdown-complete, fail-to-run, fail-to-stop and the enable timer in N7–N11) | **NO REBIND POSSIBLE.** The interface proves the roles are distinct but cannot say which field signal feeds which. **Q-C01 stays BLOCKING** — and the interface makes the consequence worse, not better: a swap misfeeds four separate mechanisms. |

## Undriven-input audit (mandatory — an undriven input matching an unclaimed IO signal is a HARD FAIL)

### `FilterUnitSystem` — inputs the orchestration must drive

| member | status | note |
|---|---|---|
| `IO.AutoStartSignal` | driven | the permissive chain (P1, P4, C6) |
| `IO.Shutdown` | driven | the shutdown chain (P2, P3, P5) |
| `IO.PreStartDone` | driven | `PlantControl.PreStartComplete` [C6] |
| `IO.FaultReset` | driven | `HMIControlSignals.SystemReset` [C13] |
| `IO.FaultFB` | driven | `DiscreteInputs.FilterUnitNFlt` / absence of `CycloneDustSysOk` [C12] |
| `IO.EnableUPSTime` | driven | `ProcessTimings.NormalFanStartTime` [C7] |
| `IO.RunningFB` | **BLOCKED** | candidate set unresolved — Q-C01 (dust filters), Q-C08 (cyclone polarity) |
| `RemoteOp` | **BLOCKED** | candidate set unresolved — Q-C01 / Q-C07 |
| `IO.InhibitMotor` | **undriven — no signal exists** | Q-C02. No `FilterUnitSystem*IsoFB` in the IO table. No unclaimed IO signal matches, so not a hard fail — but it leaves a class permissive permanently satisfied. |
| `IO.SystemHealthy` | **UNDRIVEN — HARD FAIL** | `DiscreteInputs.ControlHealthy` exists in the IO table and is **unclaimed** (rung C Q-C03). Driving this input from a constant while a plant-health input sits unreferenced is exactly the audit's hard-fail condition. Affects `FilterUnitInst1/2/3` and both VSD instances. Rung B (B-33) says assert it true; the IO table says otherwise. **Not resolved — must be answered before any render.** |
| `IO.InHand`, `IO.HandStartSignal`, `IO.HandIntervention`, `IO.RecentStart` | **BLOCKED** | no auto/hand selection exists anywhere in the IO table — Q-C09. `HMIControlSignals.GlobalSetAllToAuto` is unclaimed. |
| `IO.FTTime`, `IO.ShutdownTime`, `IO.Name` | defaulted | HMI/engineering-owned instance defaults; deliberately not driven (C-308) |

### `MotorVSDSystem` — inputs the orchestration must drive

| member | status | note |
|---|---|---|
| `IO.AutoStartSignal`, `IO.Shutdown`, `IO.PreStartDone`, `IO.FaultReset` | driven | as above |
| `IO.InhibitMotor` | driven | `DiscreteInputs.AirStarDCIsoFB` / `OSCIsoFB` [C2] |
| `IO.RotationSensor` | **UNDRIVEN — HARD FAIL** | The FB declares `IO.RotationSensor : Bool` and **never reads it** (confirmed across all 16 networks of `MotorVSDSystem.ir`). Meanwhile `DiscreteInputs.AirStarDCRotSen` + `HMIControlSignals.BypassAirStarDCRotSen` and `DiscreteInputs.OSCRotSen` sit in the IO table — the latter completely unclaimed (Q-C16). This is the audit's named case, reproduced exactly. For `MotorVSDInst1` the sensor is the machine's **only** running confirmation (Q-C13), so leaving it undriven is not a cosmetic miss; it removes the running feedback entirely. |
| `IO.RunningFwdFB`, `IO.RunningRevFB` | **BLOCKED** | no running feedback exists in the IO table for either instance — Q-C13, Q-C16 |
| `IO.SystemHealthy` | **UNDRIVEN — HARD FAIL** | as `FilterUnitSystem` above — Q-C03 |
| `IO.VSDError`, `VSDReady`, `VSDOpEnable`, `VSDWarning`, `VSDOverSpeed`, `VSDUnderSpeed`, `OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`, `AmpsFB`, `SpeedFB`, `SpeedReached` | defaulted | no discrete IO exists for any of them; they arrive over the drive's own communication interface, which is outside this boundary. No unclaimed IO signal matches any of them, so not a hard fail. Recorded so the gap is visible. |
| `IO.RunRev` | defaulted | neither instance has a recorded reverse use, and no reverse output exists (deltas D4/D6) |
| `IO.AutoSpeedInput`, `HandSpeedInput`, `MaxRPM`, `StartUpTime`, `FTTime`, `EnableUPSTime`, `ShutdownTime` | defaulted | HMI/engineering-owned instance defaults (C-308) |

### `TomraControlSystem` — inputs the orchestration must drive

| member | status | note |
|---|---|---|
| `HardWireSignals.Ready` / `.Running` / `.ComError` | driven | `DiscreteInputs.TomraReady` / `TomraRunning` / `TomraComFlt` [C6] |
| `Inputs.AutoStartSignal`, `Inputs.Shutdown`, `Inputs.PreStartDone`, `Inputs.FaultReset` | driven | as above |
| `inputWord0..7` (FB INPUT) | **BLOCKED** | the process-data link is not in the IO table and its scope is undecided — Q-C19. Four class requirements (C5, C7, C13, C14) hang off it. |
| `Inputs.InhibitMotor` | **undriven — no signal exists** | Q-C18; no `TomraIsoFB` in the IO table |
| `Inputs.InHand`, `HandStartSignal`, `HandIntervention`, `RecentStart` | **BLOCKED** | Q-C09 |
| `Inputs.ProgramSelection`, `Belt1OnDelay`, `Belt1OffDelay` | defaulted | declared on the interface and never used inside the FB (class anomaly A-3, Q-C21) |
| `PlantControl.Test[5]`, `Test[7]` | **read directly by the FB itself** | `TomraControlSystem.ir` N2 and N20. The FB reaches out of its own interface into a plant DB array named `Test` — one selecting live byte order, one overwriting a comms-fault status bit that the same network has just read. Not an orchestration input at all, and Q-C20 stands. |

---

# D2 — Discharge ledger

**Every one of the 166 relations is accounted for exactly once.**

| | count |
|---|---|
| distinct `C-nn`/`P-nn` ids in `equipment-specs/` | **166** |
| ledger rows below | **166** |
| set-difference (specs minus ledger) | **0** |
| set-difference (ledger minus specs) | **0** |

**Disposition key.** `in-FB` satisfied inside the reused block · `render` becomes an explicit D3 term
(specified, withheld — D3 stopped) · `render-BLOCKED` would be a term, but a blocking `Q-nn` contests
it · `discharged` covered by a declared D0 shape · `out-of-scope-obligation` a reverse relation whose
counterpart machine has no spec (discharge refused, S5) · `rebind` binding changed by the D1 audit.
Precondition class per the skill: `verified-in-block` / `verified-cross-block` / `unverifiable` / `n/a`.

### FilterUnitInst2 — Dust Filter Unit 1

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `FilterUnitSystem.ir` N1–N3 (start/stop + hand logic); driver blocked by Q-C09 | verified-in-block |
| C2 | render-BLOCKED | `IO.InhibitMotor`, no isolator signal exists — Q-C02 | n/a |
| C3 | in-FB | `FilterUnitSystem.ir` N1 (`NOT IO.FaultActive`) | verified-in-block |
| C4 | render-BLOCKED | `IO.SystemHealthy` — Q-C03, **HARD FAIL** if left undriven | n/a |
| C5 | render-BLOCKED | `RemoteOp` — candidate set Q-C01 | n/a |
| C6 | in-FB + render(driver) | `FilterUnitSystem.ir` N1, N5; driver `PlantControl.PreStartComplete` | verified-in-block |
| C7 | in-FB + render(driver) | `FilterUnitSystem.ir` N11; driver `ProcessTimings.NormalFanStartTime` | verified-in-block |
| C8 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7; `IO.RunningFB` — Q-C01 | verified-in-block |
| C9 | in-FB | `FilterUnitSystem.ir` N7 (fault / hand escape terms) | verified-in-block |
| C10 | in-FB | `FilterUnitSystem.ir` N8 | verified-in-block |
| C11 | in-FB | `FilterUnitSystem.ir` N9 | verified-in-block |
| C12 | in-FB + render(driver) | `FilterUnitSystem.ir` N10; driver `DiscreteInputs.FilterUnit1Flt` | verified-in-block |
| C13 | in-FB + render(driver) | `FilterUnitSystem.ir` N8–N10; driver `HMIControlSignals.SystemReset` | verified-in-block |
| C14 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7–N11; `IO.RunningFB` — Q-C01 | verified-in-block |
| C15 | in-FB | `FilterUnitSystem.ir` N12 | verified-in-block |
| C16 | in-FB | `FilterUnitSystem.ir` N13 | verified-in-block |
| C17 | in-FB | `FilterUnitSystem.ir` N14 — alarms out of scope at rung C, satisfied anyway | verified-in-block |
| P1 | render | `MotorVSDInst3.IO.UPSEnable` into `IO.AutoStartSignal` | n/a |
| P2 | render | `PlantControl.FansShutdownReady` — **S3 discharge REJECTED** | unverifiable (S3) |
| P3 | render | `NOT AirStarInst1.IO.ShutdownComplete` | n/a |
| P4 | render | `PlantControl.Status` — **S4 discharge REJECTED** | unverifiable (S4) |
| P5 | render | `NOT IO.HandIntervention` in the shutdown term | n/a |
| P6 | render | `DiscreteOutputs.FilterUnit1Start := IO.Run` | n/a |
| P7 | render | `DiscreteOutputs.FilterUnit1Reset := HMIControlSignals.SystemReset` | n/a |
| P8 | out-of-scope-obligation | reverse into `AirStarInst1`, which has no spec — S5 discharge refused | unverifiable (S5) |

### FilterUnitInst3 — Dust Filter Unit 2

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `FilterUnitSystem.ir` N1–N3; driver blocked Q-C09 | verified-in-block |
| C2 | render-BLOCKED | Q-C02 | n/a |
| C3 | in-FB | `FilterUnitSystem.ir` N1 | verified-in-block |
| C4 | render-BLOCKED | Q-C03, **HARD FAIL** | n/a |
| C5 | render-BLOCKED | Q-C01 | n/a |
| C6 | in-FB + render(driver) | `FilterUnitSystem.ir` N1, N5 | verified-in-block |
| C7 | in-FB + render(driver) | `FilterUnitSystem.ir` N11 | verified-in-block |
| C8 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7; Q-C01 | verified-in-block |
| C9 | in-FB | `FilterUnitSystem.ir` N7 | verified-in-block |
| C10 | in-FB | `FilterUnitSystem.ir` N8 | verified-in-block |
| C11 | in-FB | `FilterUnitSystem.ir` N9 | verified-in-block |
| C12 | in-FB + render(driver) | `FilterUnitSystem.ir` N10; `DiscreteInputs.FilterUnit2Flt` | verified-in-block |
| C13 | in-FB + render(driver) | `FilterUnitSystem.ir` N8–N10 | verified-in-block |
| C14 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7–N11; Q-C01 | verified-in-block |
| C15 | in-FB | `FilterUnitSystem.ir` N12 | verified-in-block |
| C16 | in-FB | `FilterUnitSystem.ir` N13 | verified-in-block |
| C17 | in-FB | `FilterUnitSystem.ir` N14 | verified-in-block |
| P1 | render | `MotorVSDInst3.IO.UPSEnable` | n/a |
| P2 | render | **S3 REJECTED** | unverifiable (S3) |
| P3 | render | `NOT AirStarInst1.IO.ShutdownComplete` | n/a |
| P4 | render | **S4 REJECTED** | unverifiable (S4) |
| P5 | render | `NOT IO.HandIntervention` | n/a |
| P6 | render | `DiscreteOutputs.FilterUnit2Start` | n/a |
| P7 | render | `DiscreteOutputs.FilterUnit2Reset` | n/a |
| P8 | out-of-scope-obligation | reverse into `AirStarInst1` — S5 refused | unverifiable (S5) |

### FilterUnitInst1 — Cyclone Filter Unit

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `FilterUnitSystem.ir` N1–N3; driver blocked Q-C09 | verified-in-block |
| C2 | render-BLOCKED | Q-C02 | n/a |
| C3 | in-FB | `FilterUnitSystem.ir` N1 | verified-in-block |
| C4 | render-BLOCKED | Q-C03, **HARD FAIL**; additionally confusable with `CycloneDustSysOk` | n/a |
| C5 | render | `RemoteOp := DiscreteInputs.CycloneDustRemOp` (basis recorded, Q-C07) | n/a |
| C6 | in-FB + render(driver) | `FilterUnitSystem.ir` N1, N5 | verified-in-block |
| C7 | in-FB + render(driver) | `FilterUnitSystem.ir` N11 | verified-in-block |
| C8 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7; polarity Q-C08 | verified-in-block |
| C9 | in-FB | `FilterUnitSystem.ir` N7 | verified-in-block |
| C10 | in-FB | `FilterUnitSystem.ir` N8; setting outlier 60 s — Q-C12 | verified-in-block |
| C11 | in-FB | `FilterUnitSystem.ir` N9 | verified-in-block |
| C12 | in-FB + render(driver) | `FilterUnitSystem.ir` N10; `IO.FaultFB` from absence of `CycloneDustSysOk` | verified-in-block |
| C13 | in-FB + render(driver) | `FilterUnitSystem.ir` N8–N10 | verified-in-block |
| C14 | in-FB + driver-BLOCKED | `FilterUnitSystem.ir` N7–N11; polarity Q-C08 | verified-in-block |
| C15 | in-FB | `FilterUnitSystem.ir` N12 | verified-in-block |
| C16 | in-FB | `FilterUnitSystem.ir` N13 | verified-in-block |
| C17 | in-FB | `FilterUnitSystem.ir` N14 | verified-in-block |
| P1 | render-BLOCKED | no start permissive specified — A Q-04. **Deliberately not rendered as "always permitted"**; that is the permissive reading the pipeline forbids adopting silently. | n/a |
| P2 | render | **S3 REJECTED** | unverifiable (S3) |
| P3 | render | `NOT ShredderControlInst1.IO.ShutdownComplete` | n/a |
| P4 | render | **S4 REJECTED** — and S4 could never have covered this instance, which has no neighbour permissive | unverifiable (S4) |
| P5 | render | `NOT IO.HandIntervention` | n/a |
| P6 | render | `DiscreteOutputs.CycloneDustFilterStart` | n/a |
| P7 | render | `DiscreteOutputs.CycloneDustFilterReset` | n/a |
| P8 | render-BLOCKED | reverse into the Shredder unresolved — A Q-05 | n/a |

### MotorVSDInst1 — Discharge Conveyor VSD

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `MotorVSDSystem.ir` N1–N3; driver blocked Q-C09 | verified-in-block |
| C2 | in-FB + render(driver) | `MotorVSDSystem.ir` N1; `DiscreteInputs.AirStarDCIsoFB` | verified-in-block |
| C3 | in-FB | `MotorVSDSystem.ir` N1 (`NOT IO.FTR AND NOT IO.FaultActive`) | verified-in-block |
| C4 | render-BLOCKED | Q-C03, **HARD FAIL** | n/a |
| C5 | in-FB + render(driver) | `MotorVSDSystem.ir` N1, N5 | verified-in-block |
| C6 | in-FB | `MotorVSDSystem.ir` N6 (incl. `CALL AnalogScale`, min-speed clamp) | verified-in-block |
| C7 | in-FB + driver-BLOCKED | `MotorVSDSystem.ir` N8, N13; no running feedback exists — Q-C13 | verified-in-block |
| C8 | in-FB | `MotorVSDSystem.ir` N13 | verified-in-block |
| C9 | in-FB + driver-BLOCKED | `MotorVSDSystem.ir` N8; Q-C13 | verified-in-block |
| C10 | in-FB | `MotorVSDSystem.ir` N8 (`OR IO.FaultActive`) | verified-in-block |
| C11 | in-FB | `MotorVSDSystem.ir` N9 (`StartUpTimer`, direction re-arm) | verified-in-block |
| C12 | in-FB | `MotorVSDSystem.ir` N10 | verified-in-block |
| C13 | in-FB | `MotorVSDSystem.ir` N11 | verified-in-block |
| C14 | in-FB, driver absent | `MotorVSDSystem.ir` N12; `IO.VSDError` has no discrete IO source | verified-in-block |
| C15 | in-FB + render(driver) | `MotorVSDSystem.ir` N10–N12; `HMIControlSignals.SystemReset` — second candidate Q-C14 | verified-in-block |
| C16 | interface-only, driver absent | declared in `MotorVSDSystem.ir` INTERFACE, not consumed; no discrete IO source | verified-in-block |
| C17 | render-BLOCKED | `IO.RotationSensor` never read inside the FB — must be wired here. **HARD FAIL if left undriven.** Q-C13 | n/a |
| C18 | in-FB | `MotorVSDSystem.ir` N14 | verified-in-block |
| C19 | in-FB | `MotorVSDSystem.ir` N15 | verified-in-block |
| C20 | in-FB | `MotorVSDSystem.ir` N16 | verified-in-block |
| P1 | render | `MotorStarterInst8.IO.UPSEnable` | n/a |
| P2 | render | `ECSControlInst1.IO.UPSEnable` | n/a |
| P3 | render | `NOT AirStarInst1.IO.ShutdownComplete` | n/a |
| P4 | render | **S4 REJECTED** | unverifiable (S4) |
| P5 | render | `NOT IO.HandIntervention` | n/a |
| P6 | render-BLOCKED | `HMIControlSignals.BypassAirStarDCRotSen` — no fallback exists for the bypassed case, Q-C13 | n/a |
| P7 | render | no discrete start output; run delivered over the drive interface | n/a |
| P8 | out-of-scope-obligation | reverse into `AirStarInst1` — S5 refused | unverifiable (S5) |
| P9 | out-of-scope-obligation | reverse into `MotorStarterInst8` — S5 refused | unverifiable (S5) |
| P10 | out-of-scope-obligation | reverse into `ECSControlInst1` — S5 refused | unverifiable (S5) |
| P11 | render-BLOCKED | reverse into the Shredder unresolved — A Q-05 | n/a |

### MotorVSDInst3 — Sorter Conveyor VSD

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `MotorVSDSystem.ir` N1–N3; driver blocked Q-C09 | verified-in-block |
| C2 | in-FB + render(driver) | `MotorVSDSystem.ir` N1; `DiscreteInputs.OSCIsoFB` | verified-in-block |
| C3 | in-FB | `MotorVSDSystem.ir` N1 | verified-in-block |
| C4 | render-BLOCKED | Q-C03, **HARD FAIL** | n/a |
| C5 | in-FB + render(driver) | `MotorVSDSystem.ir` N1, N5 | verified-in-block |
| C6 | in-FB | `MotorVSDSystem.ir` N6 | verified-in-block |
| C7 | in-FB + driver-BLOCKED | `MotorVSDSystem.ir` N8, N13; no running feedback exists — Q-C16 | verified-in-block |
| C8 | in-FB | `MotorVSDSystem.ir` N13 | verified-in-block |
| C9 | in-FB + driver-BLOCKED | `MotorVSDSystem.ir` N8; Q-C16 | verified-in-block |
| C10 | in-FB | `MotorVSDSystem.ir` N8 | verified-in-block |
| C11 | in-FB | `MotorVSDSystem.ir` N9 | verified-in-block |
| C12 | in-FB | `MotorVSDSystem.ir` N10 | verified-in-block |
| C13 | in-FB | `MotorVSDSystem.ir` N11 | verified-in-block |
| C14 | in-FB, driver absent | `MotorVSDSystem.ir` N12; no discrete IO source | verified-in-block |
| C15 | in-FB + render(driver) | `MotorVSDSystem.ir` N10–N12; Q-C14 | verified-in-block |
| C16 | interface-only, driver absent | as `MotorVSDInst1` | verified-in-block |
| C17 | render-BLOCKED | `DiscreteInputs.OSCRotSen` unclaimed, no bypass and no running feedback beside it — Q-C16. **HARD FAIL if left undriven.** | n/a |
| C18 | in-FB | `MotorVSDSystem.ir` N14 | verified-in-block |
| C19 | in-FB | `MotorVSDSystem.ir` N15 | verified-in-block |
| C20 | in-FB | `MotorVSDSystem.ir` N16 | verified-in-block |
| P1 | rebind → render | `TomraControlInst1.Outputs.UPSEnable` **and** `DiscreteInputs.TomraReady` — two terms (D1 audit) | verified-in-block |
| P2 | rebind → render | `NOT TomraControlInst1.Outputs.FaultActive` (broader guard; `TomraControlSystem.ir` N15) | verified-in-block |
| P3 | render | `MotorStarterInst6.IO.UPSEnable` | n/a |
| P4 | render | `MotorStarterInst7.IO.UPSEnable` | n/a |
| P5 | render | `NOT ECSControlInst1.IO.ShutdownComplete` | n/a |
| P6 | render | **S4 REJECTED** | unverifiable (S4) |
| P7 | render-BLOCKED | hand-intervention referent contested — A Q-06 / B Q-B8 | n/a |
| P8 | render | no discrete start output; run over the drive interface | n/a |
| P9 | discharged (S5) | rendered in `FilterUnitInst2` P1 | verified-cross-block |
| P10 | discharged (S5) | rendered in `FilterUnitInst3` P1 | verified-cross-block |
| P11 | out-of-scope-obligation | reverse into `ECSControlInst1` — S5 refused | unverifiable (S5) |
| P12 | discharged (S5) | rendered in `TomraControlInst1` P3 | verified-cross-block |

### TomraControlInst1 — Optical Sorter

| id | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | `TomraControlSystem.ir` N6–N8; driver blocked Q-C09 | verified-in-block |
| C2 | render-BLOCKED | `Inputs.InhibitMotor`, no isolator signal exists — Q-C18 | n/a |
| C3 | in-FB | `TomraControlSystem.ir` N6 | verified-in-block |
| C4 | in-FB + render(driver) | `TomraControlSystem.ir` N6, N10 | verified-in-block |
| C5 | in-FB + driver-BLOCKED | `TomraControlSystem.ir` N6 drives both `ControlWord0.%X0` and `HardWireSignals.Run`; the link half is Q-C19 | verified-in-block |
| C6 | in-FB + render(driver) | `TomraControlSystem.ir` N1, N2, N4; drivers `TomraReady`/`TomraRunning`/`TomraComFlt`; `inputWord0..7` BLOCKED Q-C19 | verified-in-block |
| C7 | in-FB + driver-BLOCKED | `TomraControlSystem.ir` N5 (life bit, `T#5S`); Q-C19 | verified-in-block |
| C8 | in-FB | `TomraControlSystem.ir` N2, N4 (link/hardwired fallback) | verified-in-block |
| C9 | in-FB | `TomraControlSystem.ir` N16 | verified-in-block |
| C10 | in-FB | `TomraControlSystem.ir` N12 — note: no fault escape term (class delta D6) | verified-in-block |
| C11 | in-FB | `TomraControlSystem.ir` N13 | verified-in-block |
| C12 | in-FB | `TomraControlSystem.ir` N14 | verified-in-block |
| C13 | in-FB + driver-BLOCKED | `TomraControlSystem.ir` N15; the machine fault set rides on the link — Q-C19 | verified-in-block |
| C14 | in-FB + render(driver) | `TomraControlSystem.ir` N3; `HMIControlSignals.SystemReset`; outbound path Q-C19 | verified-in-block |
| C15 | in-FB | `TomraControlSystem.ir` N17 | verified-in-block |
| C16 | in-FB | `TomraControlSystem.ir` N18 | verified-in-block |
| C17 | in-FB | `TomraControlSystem.ir` N19 (three alarm words) | verified-in-block |
| C18 | in-FB-DECLARED-UNUSED | declared in INTERFACE, never read in any of the 20 networks — Q-C21 | verified-in-block |
| C19 | in-FB, driver contested | `TomraControlSystem.ir` N20 selects `MOVE` vs `SWAP` on `PlantControl.Test[5]` — read by the FB itself, not by orchestration. Q-C20 | verified-in-block |
| P1 | render | `MotorStarterInst6.IO.UPSEnable` | n/a |
| P2 | render | `MotorStarterInst7.IO.UPSEnable` | n/a |
| P3 | render | `NOT MotorVSDInst3.IO.ShutdownComplete` | n/a |
| P4 | render | **S4 REJECTED** | unverifiable (S4) |
| P5 | render-BLOCKED | hand-intervention referent contested — A Q-06 / B Q-B8 | n/a |
| P6 | render | `DiscreteOutputs.TomraRun := HardWireSignals.Run` | n/a |
| P7 | discharged (S5) | rendered in `MotorVSDInst3` P1/P2 (with the Q-C17 rebind) | verified-cross-block |
| P8 | out-of-scope-obligation | reverse into `MotorStarterInst6` — S5 refused | unverifiable (S5) |
| P9 | out-of-scope-obligation | reverse into `MotorStarterInst7` — S5 refused | unverifiable (S5) |

### Ledger totals

| disposition | count |
|---|---|
| `in-FB` (incl. `+ render(driver)` / `+ driver-BLOCKED` variants) | 93 |
| `in-FB-DECLARED-UNUSED` | 1 |
| `interface-only, driver absent` | 2 |
| `render` (specified, withheld — D3 stopped) | 37 |
| `render-BLOCKED` | 19 |
| `rebind → render` | 2 |
| `discharged` (S5, verified-cross-block) | 4 |
| `out-of-scope-obligation` (S5 refused, unverifiable) | 8 |
| **total** | **166** |

(Counts machine-verified against this file's own ledger rows, not asserted.)

Discharges recorded: **1 accepted class** (S2, whole-block reuse, 96 relations satisfied inside the
reused FBs, all `verified-in-block`), **1 conditionally accepted** (S5, 4 relations
`verified-cross-block`, 8 refused as `unverifiable`), **2 rejected** (S3 and S4, both
`unverifiable`, both forcing terms to be rendered rather than dropped).
**No relation is discharged on an `unverifiable` precondition.**

---

# D3 — Render

## NOT PRODUCED. The stop condition fired for all six instances.

No boolean render is emitted, because in every instance at least one term of the automatic-control
network is contested by an unresolved blocking question, and the skill forbids rendering a guess for
a contested requirement. Rendering only the uncontested half would be worse than stopping: the
resulting network would look complete while silently omitting protective terms — the failure mode
this pipeline exists to prevent.

What each render is waiting on:

| instance | blocked terms | waiting on |
|---|---|---|
| `FilterUnitInst2` | `IO.RunningFB`, `RemoteOp`, `IO.SystemHealthy`, `IO.InhibitMotor`, hand inputs | Q-C01, Q-C03 (HARD FAIL), Q-C02, Q-C09 |
| `FilterUnitInst3` | same | Q-C01, Q-C03 (HARD FAIL), Q-C02, Q-C09 |
| `FilterUnitInst1` | `IO.RunningFB` polarity, `IO.SystemHealthy`, `IO.InhibitMotor`, the entire start permissive, hand inputs | Q-C08, Q-C03 (HARD FAIL), Q-C02, A Q-04, Q-C09 |
| `MotorVSDInst1` | `IO.RotationSensor`, `IO.RunningFwdFB`, bypass behaviour, `IO.SystemHealthy`, hand inputs | Q-C13 (HARD FAIL), Q-C03 (HARD FAIL), Q-C14, Q-C09 |
| `MotorVSDInst3` | `IO.RotationSensor`, `IO.RunningFwdFB`, the shutdown hand referent, `IO.SystemHealthy`, hand inputs | Q-C16 (HARD FAIL), A Q-06, Q-C03 (HARD FAIL), Q-C14, Q-C09 |
| `TomraControlInst1` | `inputWord0..7`, `Inputs.InhibitMotor`, the shutdown hand referent, byte-order selector, hand inputs | Q-C19, Q-C18, A Q-06, Q-C20, Q-C09 |

**What is already settled and will render unchanged once the questions are answered** is not lost —
it is the `render` and `rebind → render` rows of the D2 ledger, each naming its exact interface
member and signal. Answering the questions turns those rows into terms mechanically; nothing above
needs redoing.

**Two HARD FAIL conditions must be cleared before any render, not merely answered:**

1. **`IO.SystemHealthy` undriven while `DiscreteInputs.ControlHealthy` sits unclaimed** — five of the
   six instances. Rung B's source says assert health true; the IO table offers a plant-health input
   nobody uses. Whichever is right, the current state is the audit's named hard-fail shape.
2. **`IO.RotationSensor` undriven while rotation-sensor IO sits unclaimed** — both VSD instances, and
   for `MotorVSDInst1` the sensor is the machine's only running confirmation. This is the audit's
   documented case reproduced exactly; it is also the single relation in this run where a silent miss
   would leave a conveyor with no running feedback at all.

---

# Open questions at rung D

- **Q-D1 — BLOCKING. `patterns/` does not exist.** The skill names `patterns/` as half the reuse
  vocabulary. There is no such directory in this repo. Block fit was done directly against the three
  library FBs, which is sufficient here because the fit is whole-block reuse — but no pattern-level
  structure (permissive chain, shutdown chain, output mapping) could be selected from a proven
  library, so the D0 shapes were declared from first principles and are unvalidated.

- **Q-D2 — BLOCKING. The orchestration block itself is unnamed and unnumbered.** Rung D is required
  to produce a manifest, and a manifest needs a block identity, but assigning a block number or DB
  number would be inventing one (hard rule 3). The engineer must name the block and allocate its
  number before `gen-block-new` can run.

- **Q-D3 — `TomraControlSystem` reaches outside its own interface.** `TomraControlSystem.ir` N2 and N20
  read `PlantControl.Test[7]` and `Test[5]` directly, and N2 *writes* `StatusWord0.%X2` — the same
  bit its own comms-fault output has just read, so that output uses last scan's value. This is inside
  a given library block, not something this run may change, but it means the sorter's comms-fault
  behaviour is not fully determined by its interface. Recorded for the engineer; carried with Q-C20.

- **Q-D4 — the sorter's shutdown-complete has no fault escape.** `TomraControlSystem.ir` N12 computes
  `ShutdownComplete` from the shutdown timer and stopped-running only, unlike both sibling classes
  which also release on fault. `MotorVSDInst3` holds its shutdown on this member (its P5 chain), so a
  faulted sorter blocks the shutdown cascade at that point indefinitely. This is as-built library
  behaviour and out of this run's scope to change — flagged because the cascade this run is
  structuring depends on it.

- **Carried unresolved into this rung:** every blocking question from rungs A, B and C —
  A Q-01, Q-02, Q-03, Q-04, Q-05, Q-06, Q-08; B Q-B1, Q-B5, Q-B8, Q-B10;
  C Q-C01, Q-C02, Q-C03, Q-C04, Q-C05, Q-C06, Q-C08, Q-C09, Q-C12, Q-C13, Q-C14, Q-C16, Q-C18,
  Q-C19, Q-C20. **Q-C17 was downgraded to a confirmation** by the D1 binding audit's sanctioned
  rebind to the broader guard.

---

# Gate 1 — engineer sign-off

**NOT SIGNABLE IN THIS STATE.** Gate 1 is the architecture sign-off that must precede any coding
(`docs/11-review-workflow.md`, `docs/15-generation-pipeline.md`). It cannot be offered here: D3 does
not exist, two hard-fail conditions are open, and 28 blocking questions from four rungs are
unanswered.

```
Structure reviewed by: ______________________   date: __________

  [ ] the 28 blocking questions listed above are answered, and rung C's specs updated
  [ ] the two HARD FAIL conditions (IO.SystemHealthy, IO.RotationSensor) are cleared
  [ ] D3 has been produced against the updated specs
  [ ] the D2 ledger still set-differences to empty after those updates
  [ ] the orchestration block is named and its number allocated (Q-D2)

Gate 1 signed: ______________________            date: __________
```
