# PlantAutoControl-bench-rerun2 — Code structure (rung D)

Produced by `/gen-code-structure` (rung D of the structured spec pipeline A→B→C→D), 2026-08-05,
running inside the `lad-coder` sub-agent (hard rule 8).
**This rung structures; it does not code.** No `.ir` is written here.

## Provenance

- **Inputs consumed:**
  - `gen/PlantAutoControl-bench-rerun2/equipment-specs/*.md` (rung C) — six instance specs, 174 relations.
  - `gen/PlantAutoControl-bench-rerun2/requirements.md` (rung C derived register) — the REQ trace target.
  - `gen/PlantAutoControl-bench-rerun2/unclaimed-signals.md` (rung C §8b).
  - `patterns/` — `chained-permissive-enable/pattern.md` (**ADMITTED**),
    `motor-dol/pattern.md` (**ADMITTED**), `input-mapping/pattern.md` (**proposed, NOT admitted**),
    `output-mapping/pattern.md` (**proposed, NOT admitted**), `db-inputs`, `db-outputs`.
  - **Library blocks read in full** (real interface is ground truth):
    `ir/PlantAutoControl-bench/FilterUnitSystem.ir` (FB `FilterUnitSystem`, NUMBER 8, networks 1–14),
    `ir/PlantAutoControl-bench/MotorVSDSystem.ir` (FB `MotorVSDSystem`, NUMBER 42, networks 1–16),
    `ir/PlantAutoControl-bench/TomraControlSystem.ir` (FB `TomraControlSystem`, NUMBER 5, networks 1–20),
    plus the six instance DBs.
  - `docs/06-lad-conventions.md` — read fresh this run.
- **Safety:** no F-block, F-runtime or safety-program content read, written or referenced. Hard
  rule 2 not triggered. Hard rule 3 honoured — nothing below invents a tag, address or DB number.
- **Outcome:** **D0, D1 and D2 complete. D3 STOPPED for all six instances.** `architecture.md` is
  emitted in **PARTIAL** form and is **NOT SIGNABLE**.

---

# D0 — Logic shapes, declared before any rendering

Six shapes were considered. **Three accepted, one conditional, two REJECTED.** Each states what it
discharges, the argument, and the precondition class of that argument.
*A shape whose precondition is `unverifiable` is not a valid discharge — the relations it would
have covered are rendered as terms instead. This is applied below without exception.*

## S1 — `chained-permissive-enable` (orchestration rung shape) — **ACCEPTED**

- **Source:** `patterns/chained-permissive-enable/pattern.md`, **ADMITTED** 2026-07-15 (criterion 3
  accepted on partial evidence — the pattern's own recorded gap; noted, not treated as a blocker).
- **Structure:** one network per equipment instance in the orchestrating FC, writing the instance's
  `RunningFB` / `SystemHealthy` / `InhibitMotor` / `PreStartDone` / `AutoStartSignal` / `Shutdown` /
  `FaultReset` interface members, then the physical output coil, then `CALL <FB>(<Inst>)`.
- **What it discharges: NOTHING.**
  This is declared deliberately and is not an oversight. S1 is a **rendering** shape — it fixes the
  *form* of the terms, not the claim that any relation may be omitted. Every plant interlock still
  becomes an explicit term inside the shape's `AutoStartSignal` / `Shutdown` expressions.
  *Declaring a zero-discharge shape is the point of D0: without the declaration, "the pattern covers
  it" is exactly the unexamined assumption that turns a shape into a silent discharge.*
- **Precondition class:** n/a (nothing discharged).
- **Conventions check:** satisfies C-113's memory test as classified by the source register
  (combinational — no phase memory in this layer), and C-114's one-direction enable rule, with
  `ShutdownComplete` cross-references exempt as process interlocks (C-114 scope note). One network
  per instance satisfies C-101/C-106.

## S2 — `equipment-FB-encapsulation` (whole-reuse of the class FB) — **ACCEPTED**

- **Structure:** each instance is one call to its class FB with its existing instance DB; the FB's
  own networks implement the class requirement set.
- **What it discharges:** the class relations `C-nn` whose mechanism is **wholly internal** to the
  FB and needs no caller-written wire. These take the `in-FB` disposition in D2 with the FB network
  cited.
- **Argument:** the FB is site-proven, already instantiated, and its networks were read in full this
  run; a block carrying more than the spec requires is a valid fit (C-606 whole-reuse exception), so
  extra capability is accepted, not flagged.
- **Precondition class: `verified-in-block`** — every `in-FB` row in D2 cites the specific network
  of the specific FB file, read this run. No row rests on "the FB probably does this".
- **Limit, stated:** encapsulation discharges the *mechanism* only. Any relation needing a caller
  wire is **not** discharged by S2 — it is a term, and therefore blocked wherever D3 is stopped.

## S3 — `plant-flag-subsumes-neighbour-shutdown` — **REJECTED**

- **The tempting discharge:** the three filter units each hold through shutdown on **both** the
  plant's `PlantControl.FansShutdownReady` **and** a named neighbour's `ShutdownComplete`
  (`AirStarInst1` for the dust units, `ShredderControlInst1` for the cyclone). The shape would argue
  that the plant flag already encodes the neighbour's shutdown state, so the neighbour term is
  redundant and one term suffices.
- **Precondition:** that `PlantControl.FansShutdownReady` is computed to include those neighbours'
  shutdown-complete states.
- **Precondition class: `unverifiable`.** `FansShutdownReady` is written in the plant sequencer —
  a block outside this run's boundary and **not present in `ir/PlantAutoControl-bench/`** at all. No
  artifact in scope establishes what it contains.
- **VERDICT: REJECTED.** Plausibility is not verification. **Relations P2 and P3 on all three filter
  units stay as two separate terms** (and, in this run, as six separate blocked rows), exactly as
  rung C recorded them. *This is the shape whose acceptance is the documented cause of a real
  dropped-interlock regression; it is declared here so the rejection is on the record rather than
  the acceptance being invisible.*

## S4 — `hardwired-health-constant` — **REJECTED**

- **The tempting discharge:** five of the six instances inherit a "does not start unless reported
  system-healthy" permissive (`FU-04` / `VSD-04`), and rung B records that this layer asserts the
  healthy input **true** for every machine (`B-34`). The shape would argue that health is enforced
  by the hardwired safety circuit, so driving the input true discharges the relation.
- **Precondition:** that the hardwired circuit genuinely inhibits each drive independently of the
  PLC.
- **Precondition class: `unverifiable`** — and *doubly* so: the safety circuit is not an artifact in
  scope, and hard rule 2 forbids reasoning about its internals at all. A precondition this rung is
  **forbidden** to verify can never support a discharge.
- **VERDICT: REJECTED.** `C4` on `FilterUnitInst1/2/3`, `MotorVSDInst1`, `MotorVSDInst3` is
  **render-BLOCKED under Q-C05**, not discharged. The §8b sweep additionally found a real plant
  health input (`DiscreteInputs.ControlHealthy`) claimed by nothing — so the constant would be
  defeating a permissive *while a candidate signal sits unused*.
- **Convention note:** even if the owner rules the constant correct, C-604 forbids a bare
  `AlwaysTrue`-derived constant: it would need a named, commented `placeholder_<Var>` constant
  stating *why*, never `NOT AlwaysTrue` (which is reserved for genuine to-be-built gaps).

## S5 — `io-buffer-boundary` — **ACCEPTED (as an obligation, not a discharge)**

- **Structure:** C-304 — control logic touches only buffer DBs; physical points are mapped in the
  `Map` FCs (C-109's mapping exception, C-110's ordering).
- **What it discharges:** nothing in this run. Every signal rung C bound is already a buffer-DB
  member (`DiscreteInputs.*` / `DiscreteOutputs.*`), so the boundary holds **by construction**. The
  physical-point mapping itself is an **`out-of-scope-obligation`** owed to the map layer.
- **Precondition class: `verified-cross-block`** — `ir/PlantAutoControl-bench/Input.ir` (DB
  `DiscreteInputs`, NUMBER 15) and `Output.ir` (DB `DiscreteOutputs`, NUMBER 16) are the buffer DBs,
  and every bound member was grep-verified in them this run.
- **Constraint recorded:** `patterns/input-mapping` and `patterns/output-mapping` are **proposed, not
  admitted** (criterion 3 not started, human sign-off pending, and `input-mapping` carries two
  recorded real data-quality defects). **They may not be used to author new map rungs in this run**
  without the owner's explicit go-ahead, which is not on record.

## S6 — `motion-sensor-running-confirmation` — **CONDITIONAL, currently BLOCKED**

- **The shape:** `chained-permissive-enable`'s documented richer `RunningFB` variant —
  `(RawRunning AND RotationSensor) OR (RawRunning AND SensorBypass)` (pattern §"What varies",
  network 7).
- **Intended use:** `MotorVSDInst1` (C17/REQ-098), whose spec says the motion sensor **is** its
  running-forward confirmation, operator-bypassable.
- **Blocker:** the documented variant needs a **raw running feedback** to AND against, and the §8
  sweep established that **no running-feedback signal exists for this machine at all**
  (Q-C08/Q-C29). The pattern's shape therefore cannot be instantiated as written, and the two
  candidate readings of the bypass behaviour (fall back to the drive's report / no confirmation at
  all) differ in exactly the failure the sensor exists to catch.
- **Precondition class: `unverifiable` until Q-C08 is answered.** Not used. Relation render-BLOCKED.
- **FI-34 note:** were this forced, the stronger/fail-safe reading (bypass does **not** synthesise a
  running confirmation) would be preferred and recorded. It is **not** being forced here — the
  relation is left blocked.

---

# D1 — Block fit (reuse-first), grouping, and the two audits

## D1.1 — Coverage: class → library block

Grouping **derived** from rung A's per-instance expansion, not assumed:

| Class (rung A) | Library FB | Instances in this run | Instance DBs | New blocks needed |
|---|---|---|---|---|
| `FilterUnitSystem` | `FilterUnitSystem` (NUMBER 8) | 3 | `FilterUnitInst2` (37), `FilterUnitInst3` (40), `FilterUnitInst1` (23) | **none** |
| `vsd-motor` | `MotorVSDSystem` (NUMBER 42) | 2 | `MotorVSDInst1` (8), `MotorVSDInst3` (22) | **none** |
| `optical-sorter` | `TomraControlSystem` (NUMBER 5) | 1 | `TomraControlInst1` (26) | **none** |

Reuse-first is satisfied outright: every class has an existing, instantiated FB and every instance
already has its DB. **No new FB, no new instance DB, no new DB number** is proposed by this run
(hard rule 3). The only new content this pipeline would produce is orchestration networks in a
calling FC.

### Coverage table — `FilterUnitSystem` C-relations against `FilterUnitSystem`

| C | Satisfied | Evidence |
|---|---|---|
| C1 auto/hand arbitration | in FB, **caller must wire `InHand`/`HandStartSignal`** | N1, N2 |
| C2 inhibit | in FB, caller must wire `InhibitMotor` | N1 |
| C3 fault permissive | **in FB** | N1 (`NOT IO.FaultActive`), N10 |
| C4 system-healthy permissive | in FB, caller must wire `SystemHealthy` | N1 |
| C5 remote-mode permissive | in FB — **but `RemoteOp` is a STATIC SETPOINT, not an interface input** | N1, N14 |
| C6 pre-start | in FB, caller must wire `PreStartDone` | N1, N5 |
| C7 enable after up-to-speed time | **in FB** | N11, N6 |
| C8 shutdown + shutdown-complete | **in FB**, caller must wire `Shutdown` | N7 |
| C9 immediate shutdown-complete on fault/hand | **in FB** | N7 OR-terms |
| C10 fail-to-run | **in FB** | N8 |
| C11 fail-to-stop | **in FB** | N9 |
| C12 unit fault → latched fault | in FB, caller must wire `FaultFB` | N10 (`RCOIL IO.FaultFB := NOT IO.FaultReset` — the C-103 documented set-outside/reset-inside contract) |
| C13 fault reset | in FB, caller must wire `FaultReset` | N8, N9, N10 |
| C14 running state from running feedback | in FB, caller must wire `RunningFB` | N7, N8, N9, N11 |
| C15 hours run | **in FB** | N12 (uses `TONR` — a known C-406 deviation carried by the existing block) |
| C16 status telemetry | **in FB** | N13 |
| C17 alarm word | **in FB** | N14 |

### Coverage table — `vsd-motor` C-relations against `MotorVSDSystem`

| C | Satisfied | Evidence |
|---|---|---|
| C1 auto/hand arbitration | in FB, caller wires `InHand`/`HandStartSignal` | N1, N2 |
| C2 inhibit | in FB, caller wires `InhibitMotor` | N1 |
| C3 fault + standing-FTR permissive | **in FB** | N1 (`NOT IO.FTR AND NOT IO.FaultActive`) |
| C4 system-healthy | in FB, caller wires `SystemHealthy` | N1 |
| C5 pre-start | in FB, caller wires `PreStartDone` | N1, N5 |
| C6 speed reference | **in FB** — min-speed floor is `CONSTANT MinSpd = 20.0`; scaling via `CALL AnalogScale` against `IO.MaxRPM` | N6 |
| C7 reverse + fwd/rev running confirmation | in FB, caller wires `RunningFwdFB`/`RunningRevFB`/`RunRev` | N1, N8, N13 |
| C8 enable after up-to-speed time | **in FB** | N13 |
| C9 shutdown + shutdown-complete | **in FB**, caller wires `Shutdown` | N8 |
| C10 immediate shutdown-complete on fault | **in FB** | N8 OR-term |
| C11 start-up allowance, re-armed on direction change | **in FB** | N9 (`StartUpTimer`, `RevPosEgde`/`RevNegEge`) |
| C12 fail-to-run after the allowance | **in FB** | N10 (gated by `StartUpTimer.Q`) |
| C13 fail-to-stop | **NOT SATISFIED — see finding D-F1** | N11 |
| C14 drive error → latched fault | in FB, caller wires `VSDError` | N12 |
| C15 fault reset | in FB, caller wires `FaultReset` | N10, N11, N12 |
| C16 drive condition to operator | interface-only; **not consumed inside the FB**, caller must wire 11 members | interface (`VSDReady`…`SpeedReached`) |
| C17 motion-sensor input | **declared but never read inside the FB** — see finding D-F2 | interface (`IO.RotationSensor`) |
| C18 hours run | **in FB** | N14 (`TONR`, same C-406 deviation) |
| C19 status telemetry | **in FB** | N15 |
| C20 alarm word | **in FB** | N16 |

### Coverage table — `optical-sorter` C-relations against `TomraControlSystem`

| C | Satisfied | Evidence |
|---|---|---|
| C1 auto/hand arbitration | in FB, caller wires `Inputs.InHand`/`HandStartSignal` | N6, N7 |
| C2 inhibit | in FB, caller wires `Inputs.InhibitMotor` | N6 |
| C3 fault + standing-FTR permissive | **in FB** | N6 |
| C4 pre-start | in FB, caller wires `Inputs.PreStartDone` | N6, N10 |
| C5 dual command (link + hardwired) | **in FB** — `ControlWord0.%X0` and `HardWireSignals.Run` driven together | N6 |
| C6 dual condition read | in FB, caller wires `HardWireSignals.Ready/Running/ComError` **and the 8 link words** | N1, N2, N4 |
| C7 link liveness watchdog | **in FB** — `LifeBitTimer`, **PT = `T#5S`** (this answers Q-B11: `verified-in-block`, N5) | N5 |
| C8 link/hardwire agreement + dead-link fallback | **in FB** | N2, N4 |
| C9 enable after up-to-speed time, withdrawn on loss of running | **in FB** | N16 |
| C10 shutdown + shutdown-complete (no fault escape) | **in FB**, caller wires `Inputs.Shutdown` | N12 |
| C11 fail-to-run | **NOT SATISFIED — see finding D-F3** | N13 |
| C12 fail-to-stop | **in FB** | N14 |
| C13 machine faults + link loss → latched fault | **in FB** (14 status bits OR `LostLifeBit`) | N15 |
| C14 fault reset + echo over link | in FB, caller wires `Inputs.FaultReset` | N3, N13–N15 |
| C15 hours run | **in FB** | N17 |
| C16 status telemetry | **in FB** | N18 |
| C17 wide alarm set | **in FB** | N19 |
| C18 operator program / belt delays | **NOT SATISFIED — see finding D-F4** | interface only |
| C19 byte-order selection | **in FB** — selector is `PlantControl.Test[5]` | N20 |

## D1.2 — Settings disposition (C-307/C-308)

Every setting rung C recorded is an **instance-DB SETPOINT member** owned by engineering or HMI, and
is therefore **left at its instance default**. **No orchestration rung may cyclically copy a value
onto any of them** — that is the C-308 scan-copy trap, and it is a live risk here because the §8b
sweep found an unclaimed plant-wide override feature set (`HMIControlSignals.Global*Overwrite` +
`ProcessTimings.Global*`, Q-C15) whose obvious naive implementation is exactly such a copy. Recorded
as a standing constraint on whoever specifies that feature.

**Finding D-F5 (C-308 breach inside the reused block).** `MotorVSDSystem` N6 contains
`MOVE(EN := NOT IO.InHand, IN := IO.AutoSpeedInput) => IO.HandSpeedInput` — while the drive is in
auto, the FB overwrites the operator's hand speed setpoint every scan with the automatic setpoint.
`HandSpeedInput` is an HMI-owned `SETPOINT` member (C-307's per-instance scope). This is the
scan-copy trap C-308 names, occurring **inside** the library block rather than in an orchestrating
FC, so no amount of care in the calling layer avoids it. Affects `MotorVSDInst1` (hand setpoint
30.0) and `MotorVSDInst3` (20.0) — both values are silently unreachable in service. **Reported, not
fixed:** this rung structures and does not code, and the block is existing site content.

## D1.3 — Binding audit (mandatory)

Every rung-C binding checked against the FB interface now that it is ground truth.

### `rebind-1` — `MotorVSDInst3` P2 / REQ-133, "sorter is free of faults"

| Candidate | What the FB does with it |
|---|---|
| `DiscreteInputs.TomraComFlt` (rung C's binding) | Enters `TomraControlSystem` as `HardWireSignals.ComError` and is used **only** in N2, to compute `Outputs.OSComFault` — the *communications* fault. It says nothing about the sorter's own machine faults. |
| `TomraControlInst1.Outputs.FaultActive` `[iface]` | N15 — latched OR of `FTR`, `FTS`, `LostLifeBit` and **fourteen** of the sorter's own reported status-bit faults. Strictly broader: it covers everything the comms input covers **plus** the machine's own fault set. |
| `TomraControlInst1.Outputs.OSComFault` `[iface]` | N2 — link-aware comms fault (link-healthy: status bit AND hardwired; link-dead: hardwired alone). Broader than the raw input, narrower than `FaultActive`. |

**Decision: rebind to `TomraControlInst1.Outputs.FaultActive`** — the **stronger/broader guard**
(D1's own rule, and FI-34's protective-term preference: this is a start permissive on a conveyor
feeding a sorter). With rung C's raw-input binding, a sorter with a standing *machine* fault and a
healthy comms link would leave the permissive satisfied and let the feed conveyor start into it.
**Q-C34 is closed by this audit** — it is closed by the mechanism Q-C34 itself deferred to (rung C
could not see the interface), not by a guess to keep moving. Ledger disposition: `rebind`.

### `rebind-2` — `FilterUnitInst1` C3/C4, the cyclone's system-OK report

| Candidate | What the FB does with it |
|---|---|
| `IO.FaultFB` (rung C's C3 binding, fed by `NOT DiscreteInputs.CycloneDustSysOk`) | N10 — raises `IO.FaultActive`, **latched until `FaultReset`**. N1 then blocks `TryRunMotor`. A dropped system-OK therefore stops the unit and *keeps* it stopped until an operator resets. |
| `IO.SystemHealthy` (the Q-C27 alternative) | N1 only — a live permissive. A dropped system-OK would stop the unit and let it restart the instant the signal returned, with no operator involvement. |

**Decision: rung C's binding CONFIRMED** (`IO.FaultFB`) — the latched path is the
stronger/fail-safe guard (FI-34), and C-128 forbids automatic restart after a stop. Recorded as a
`rebind` row because the audit was performed and the alternative was live, not because the binding
changed. **Q-C27 is narrowed, not closed:** the audit settles which member the *system-OK* signal
should drive; it does not produce a source for `IO.SystemHealthy`, which stays blocked under Q-C05.

### `rebind-3` — the filter units' `RemoteOp`: an audit finding, NOT a rebind

Rung C offered `FilterUnitInst*.RemoteOp` `[iface, named-only]` as a C5 candidate. **The interface
refutes it as a wiring target:** `RemoteOp` is declared `RemoteOp : Bool SETPOINT` in the FB's own
STATIC section — an engineering setpoint, **not a member the caller writes per scan**. Writing a
field signal onto it every scan would be a C-308 breach identical in kind to D-F5.

**Consequence:** the field's remote-operational reports (`DiscreteInputs.FilterUnit1Op` /
`FilterUnit2Op` / `CycloneDustRemOp`) have **no interface member to land on**. `MotorIOSet` offers
exactly one running-shaped input (`IO.RunningFB`) and no remote-mode input at all. **Q-C01 / Q-C02
therefore cannot be resolved by choosing between the two signals — one of the two has nowhere to
go.** This is a stronger statement than rung C could make and it makes the question worse, not
better. Recorded; both relations stay render-BLOCKED.

## D1.4 — Undriven-input audit

Every caller-writable interface member of each chosen FB, marked **driven** (citing the D3 term it
would carry) or **defaulted** (with the reason). *An undriven input whose name matches an unclaimed
IO signal is a **HARD FAIL**.*

### `FilterUnitSystem` — per instance (`FilterUnitInst2` / `InstInst3` / `FilterUnitInst1`)

| Member | Status |
|---|---|
| `IO.AutoStartSignal` | driven — S1's permissive expression (P1/P4/P5) — **term blocked, D3 stopped** |
| `IO.Shutdown` | driven — S1's shutdown expression (P2/P3/P6) — **term blocked** |
| `IO.PreStartDone` | driven — `PlantControl.PreStartComplete` (C6/P5) — term blocked by the instance stop only |
| `IO.FaultReset` | driven — `HMIControlSignals.SystemReset` (C13/P7) — term blocked by the instance stop only |
| `IO.RunningFB` | driven — `FilterUnit1Op`/`FilterUnit2Op`/`CycloneDustAutoRunning/Stop`, **all contested** (Q-C01/C02/C06) |
| `IO.FaultFB` | driven — `FilterUnit1Flt` / `FilterUnit2Flt` / `NOT CycloneDustSysOk` (C12, rebind-2) |
| `IO.SystemHealthy` | **UNDRIVEN — HARD FAIL.** Would be a constant true (S4, rejected), while `DiscreteInputs.ControlHealthy` — a plant health input matching this member's role — sits **unclaimed** in the §8b sweep. Q-C05 / Q-C31. |
| `IO.InhibitMotor` | **UNDRIVEN.** No isolator signal exists for any filter unit (Q-C03). Not the literal name-match HARD FAIL (no unclaimed filter isolator exists to match), but a required protective input with no source. |
| `IO.InHand` | **UNDRIVEN — HARD FAIL.** No per-machine hand/auto signal exists (Q-C04), while `InterlockData.PartInHand` sits **unclaimed** and matches this member's role and name. |
| `IO.HandStartSignal` | **UNDRIVEN.** HMI-set by contract (FB N2/N3 comments: *"Must Have Set Bit … On Hand Control Buttons Press/Release On HMI"*). No HMI tag exists in the export. |
| `IO.HandIntervention` | **UNDRIVEN.** HMI-set by the same contract; the FB only ever *resets* it (N2). Read back by other instances' shutdown terms — so an unset member silently changes **other** machines' behaviour. |
| `IO.RecentStart` | **UNDRIVEN — blocking functional gap (finding D-F6).** N1's run latch is `TryRunMotor AND PreStartMemory AND IO.RecentStart`; HMI-set by contract, and no HMI tag exists. `FilterUnitInst3` and `FilterUnitInst1` carry start value **FALSE**, so on this boundary **neither unit can ever latch its run command**. Not the literal name-match HARD FAIL, but worse in effect than one. |
| `IO.FTTime`, `EnableUPSTime`, `ShutdownTime` | defaulted — engineering SETPOINTs at instance-DB start values (D1.2); never scan-copied |
| `RemoteOp` | defaulted — SETPOINT, **not caller-writable** (rebind-3); its value is uninitialised in all three instance DBs, and N1 requires it true for `TryRunMotor` |

### `MotorVSDSystem` — per instance (`MotorVSDInst1` / `MotorVSDInst3`)

| Member | Status |
|---|---|
| `IO.AutoStartSignal`, `IO.Shutdown` | driven — S1 expressions — **terms blocked** |
| `IO.PreStartDone`, `IO.FaultReset` | driven — plant members; blocked by the instance stop only |
| `IO.InhibitMotor` | driven — `DiscreteInputs.AirStarDCIsoFB` / `OSCIsoFB` (C2/P9/P14) |
| **`IO.RotationSensor`** | **UNDRIVEN — HARD FAIL, both instances.** The member is declared on the interface and, verified across N1–N16, **never read anywhere inside the FB**. For `MotorVSDInst3` the unclaimed `DiscreteInputs.OSCRotSen` matches it by name and role (Q-C09) — the documented hard-fail case, reproduced exactly. For `MotorVSDInst1` the sensor is claimed at the caller (C17) but the member itself stays undriven and unread — a C-610 dead interface signal with no known-gap comment. |
| `IO.RunningFwdFB` | **UNDRIVEN.** `MotorVSDInst1`: contested (Q-C08 — the motion-sensor expression, with no fallback). `MotorVSDInst3`: **no candidate signal exists at all** (Q-C33). |
| `IO.RunningRevFB` | **UNDRIVEN.** No reverse feedback exists for either machine (Q-C29/Q-C33). Note N8's shutdown-complete needs **both** feedbacks clear and N11's fail-to-stop needs **both** set. |
| `IO.VSDError` | **UNDRIVEN.** No drive-error signal in the exported IO (Q-C30). C14 unreachable. |
| `IO.VSDReady`, `VSDOpEnable`, `VSDWarning`, `VSDOverSpeed`, `VSDUnderSpeed`, `OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`, `AmpsFB`, `SpeedFB`, `SpeedReached` | **UNDRIVEN (11 members).** No signals exist (Q-C30). Not read inside the FB either — pure operator-facing pass-through, so all eleven are C-610 dead signals as things stand. **Three of them default TRUE in both instance DBs** (`VSDWarning`, `OverTempFault`, `ThermalOLFault`) — fault-shaped members defaulting to the *asserted* state. |
| `IO.RunRev` | **UNDRIVEN.** No reverse operation specified (Q-A12); N9 reads it for the direction-change re-arm. |
| `IO.InHand`, `HandStartSignal`, `HandIntervention`, `RecentStart` | **UNDRIVEN** — same four contract members and the same consequences as the filter units (Q-C04, D-F6). `MotorVSDInst1`/`Inst3` both start with `RecentStart = FALSE`. |
| `IO.AutoSpeedInput`, `HandSpeedInput` | defaulted SETPOINTs — **but see D-F5**: the FB itself overwrites `HandSpeedInput` every scan while in auto |
| `IO.FTTime`, `EnableUPSTime`, `ShutdownTime`, `StartUpTime`, `MaxRPM` | defaulted — engineering SETPOINTs at instance-DB start values |

### `TomraControlSystem` — `TomraControlInst1`

| Member | Status |
|---|---|
| `inputWord0…7` (FB **INPUT** parameters) | **UNDRIVEN — the tags do not exist.** The source names `Tag_45`…`Tag_54`; grep over all of `ir/PlantAutoControl-bench/` returns **zero** hits and no tag table is exported. **The FB cannot be called at all** without its eight input words (Q-C10). |
| `OutputWord0…1` (FB **OUTPUT** parameters) | **UNDRIVEN — same** (Q-C10). |
| `HardWireSignals.Ready`, `.Running`, `.ComError` | driven — `TomraReady` / `TomraRunning` / `TomraComFlt` (C6) |
| `HardWireSignals.Run` | **must NOT be driven** — written *by* the FB (N6) and read back by the caller into `DiscreteOutputs.TomraRun` (C5/P9). Recorded so a coder does not wire it as an input. |
| `Inputs.AutoStartSignal`, `Inputs.Shutdown` | driven — S1 expressions — **terms blocked** |
| `Inputs.PreStartDone`, `Inputs.FaultReset` | driven — plant members; blocked by the instance stop only |
| `Inputs.InhibitMotor` | **UNDRIVEN.** No isolator signal exists for this machine (Q-C11). |
| `Inputs.InHand`, `HandStartSignal`, `HandIntervention`, `RecentStart` | **UNDRIVEN** — as above (Q-C04, D-F6). |
| `Inputs.ProgramSelection`, `Inputs.Belt1OnDelay` | **UNDRIVEN and never read inside the FB** — C-610 dead signals with no known-gap comment (finding D-F4). |
| `Inputs.Belt1OffDelay` | defaulted; scaled in N11 to `Belt1OffDelayMS`, which is itself never consumed — also dead. |
| `Inputs.FTTime`, `EnableUPSTime`, `ShutdownTime` | defaulted — engineering SETPOINTs |

## D1.5 — Findings raised at this rung

| # | Finding | Evidence |
|---|---|---|
| **D-F1** | **`MotorVSDSystem` fail-to-stop cannot trip on a single-direction drive.** N11's timer input is `NOT IO.Run AND NOT IO.RunRev AND IO.RunningFwdFB AND IO.RunningRevFB` — it requires **both** running feedbacks true simultaneously. A motor running forward while not commanded never raises fail-to-stop. C13/REQ-094/REQ-124 is **not** satisfied in-FB. | `MotorVSDSystem.ir` N11 |
| **D-F2** | **`IO.RotationSensor` is declared and never read** anywhere in `MotorVSDSystem` (N1–N16 checked). Any instance relying on it relies entirely on the calling layer. HARD FAIL for `MotorVSDInst3` (unclaimed `OSCRotSen` matches). | `MotorVSDSystem.ir` interface + N1–N16 |
| **D-F3** | **`TomraControlSystem` fail-to-run can never trip.** N13's timer input is `ControlWord0.%X0 AND ControlWord0.%X1 AND NOT Outputs.OSRunning`, and **`ControlWord0.%X1` is never written anywhere in the block** (N3 writes `%X7`, N6 writes `%X0`; N20 only moves the word out). The condition is permanently false. C11/REQ-156 is **not** satisfied in-FB. | `TomraControlSystem.ir` N3, N6, N13, N20 |
| **D-F4** | **`Inputs.ProgramSelection` and `Inputs.Belt1OnDelay` are declared and never used**, and `Belt1OnDelayMS` is never computed while `Belt1OffDelayMS` is; `ControlWord1` is transmitted (N20) but never written. C18/REQ-163 is an interface promise only. Grounds the class reference's anomaly A-3 as fact. | `TomraControlSystem.ir` N11, N20 |
| **D-F5** | **C-308 scan-copy inside `MotorVSDSystem`** — N6 overwrites the HMI-owned `IO.HandSpeedInput` with `IO.AutoSpeedInput` every scan while in auto. | `MotorVSDSystem.ir` N6 |
| **D-F6** | **`RecentStart` is required by every run latch and driven by nothing.** All three FBs gate `Run` on it and document it as HMI-set; no HMI tag for it exists in the export. Four of the six instances start with it FALSE. | `FilterUnitSystem.ir` N1/N3, `MotorVSDSystem.ir` N1/N3, `TomraControlSystem.ir` N6/N8 |
| **D-F7** | **`TomraControlSystem` reads a plant global DB directly from inside a reusable equipment FB** — `PlantControl.Test[7]` overwrites `StatusWord0.%X2` (N2, after that same bit has been used to compute the comms fault) and `PlantControl.Test[5]` selects the output byte order (N20). This is production behaviour driven by a **test array**, and it couples a reusable FB to one specific plant DB (against C-127's intent and C-304's boundary). Grounds anomaly A-1 and answers *what* Q-C18's selector is, without answering whether it should be. | `TomraControlSystem.ir` N2, N20 |
| **D-F8** | **Q-B11 answered as `verified-in-block`:** the sorter's data-link liveness watchdog is **5 s** (`LifeBitTimer`, `PT := T#5S`). Recorded as fact, not as a resolution of any contested relation. | `TomraControlSystem.ir` N5 |

---

# D2 — Discharge ledger

**Every one of the 174 spec relations appears exactly once.** Dispositions used:
`in-FB` (satisfied inside the reused block — precondition class `verified-in-block`, network cited) ·
`rebind` (D1.3 binding audit) ·
`render-BLOCKED` (would be a term, but D3 is stopped — the contesting `Q-nn` is cited; where the
relation itself is uncontested and only the **instance-level** stop prevents it, the evidence column
says `instance-stop`) ·
`out-of-scope-obligation` (the counterpart lives outside this run's six instances).

**`discharged` is used ZERO times.** Both candidate discharges (S3, S4) were rejected on
`unverifiable` preconditions, and S1 discharges nothing by declaration. **No relation in this run is
covered by an argument rather than by a term or a cited FB network.**

## Set-difference

| | Count |
|---|---|
| `C-nn`/`P-nn` ids in `gen/PlantAutoControl-bench-rerun2/equipment-specs/` | **174** |
| Rows in this ledger | **174** |
| Relations with no ledger row (**must be 0**) | **0** |
| Ledger rows with no relation (**must be 0**) | **0** |

Per instance: `FilterUnitInst2` 27 · `FilterUnitInst3` 27 · `FilterUnitInst1` 27 ·
`MotorVSDInst1` 30 · `MotorVSDInst3` 34 · `TomraControlInst1` 29. Sum **174**.

## Disposition counts

Counted mechanically over the ledger tables below, not asserted:

| Disposition | Count |
|---|---|
| `in-FB` | **54** |
| `render-BLOCKED` | **113** |
| `rebind` | **2** |
| `out-of-scope-obligation` | **5** |
| `discharged` | **0** |
| **Total** | **174** |

Breakdown of the 113 `render-BLOCKED` rows, also counted mechanically:

| Evidence shape | Count |
|---|---|
| cites a contesting `Q-nn` (8 of these also note the instance stop) | **59** |
| `instance-stop` only — the relation itself is uncontested | **51** |
| blocked by a rung-D finding with no `Q-nn` yet (D-F1 ×2, D-F3 ×1) | **3** |

The distinction matters to whoever answers the questions: the 51 `instance-stop`-only rows are not
themselves in dispute and unblock automatically once the instance's cited questions are answered.

## Ledger — `FilterUnitInst2` (Dust Filter Unit 1)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 — no hand/auto source; `IO.InHand`/`HandStartSignal` undriven | — |
| C2 | render-BLOCKED | Q-C03 — no isolator signal exists | — |
| C3 | in-FB | `FilterUnitSystem.ir` N1 (`NOT IO.FaultActive` in `TryRunMotor`), N10 | verified-in-block |
| C4 | render-BLOCKED | Q-C05 / Q-C31 — S4 rejected; `SystemHealthy` HARD FAIL | — |
| C5 | render-BLOCKED | Q-C01 + rebind-3 (`RemoteOp` is a SETPOINT, not a wiring target) | — |
| C6 | render-BLOCKED | instance-stop; wire `IO.PreStartDone := PlantControl.PreStartComplete` uncontested | — |
| C7 | in-FB | N11 (`EnableUpstreamTimer`), N6 (time scaling) | verified-in-block |
| C8 | in-FB | N7 (`ShutdownTimer`, `IO.ShutdownComplete`) | verified-in-block |
| C9 | in-FB | N7 OR-terms (`IO.FaultActive`; in-hand-without-start) | verified-in-block |
| C10 | in-FB | N8 (`FaultTripTimer`) | verified-in-block |
| C11 | in-FB | N9 (`FTSTimer`) | verified-in-block |
| C12 | render-BLOCKED | instance-stop; wire `IO.FaultFB := DiscreteInputs.FilterUnit1Flt` uncontested (C-103 set-outside/reset-inside) | — |
| C13 | render-BLOCKED | instance-stop; wire `IO.FaultReset := HMIControlSignals.SystemReset` uncontested | — |
| C14 | render-BLOCKED | Q-C01 — `…Op`/`…Ready` competing pair; only one interface slot exists | — |
| C15 | in-FB | N12 | verified-in-block |
| C16 | in-FB | N13 | verified-in-block |
| C17 | in-FB | N14 (alarms — outside the control spec's own scope) | verified-in-block |
| P1 | render-BLOCKED | instance-stop; term `MotorVSDInst3.IO.UPSEnable` | — |
| P2 | render-BLOCKED | Q-C12 — **S3 REJECTED**, term retained separately | — |
| P3 | render-BLOCKED | Q-C12 — **S3 REJECTED**, term retained separately | — |
| P4 | render-BLOCKED | instance-stop; Q-B02 on the `Status` encoding | — |
| P5 | render-BLOCKED | instance-stop | — |
| P6 | render-BLOCKED | Q-C04 — `IO.HandIntervention` undriven | — |
| P7 | render-BLOCKED | instance-stop | — |
| P8 | render-BLOCKED | instance-stop; wire `DiscreteOutputs.FilterUnit1Reset` uncontested | — |
| P9 | out-of-scope-obligation | `AirStarInst1`'s own rung consumes `FilterUnitInst2.IO.UPSEnable`; that machine is not specified in this run | — |
| P10 | render-BLOCKED | instance-stop; wire `DiscreteOutputs.FilterUnit1Start := IO.Run`; physical point owed to the map layer (S5) | — |

## Ledger — `FilterUnitInst3` (Dust Filter Unit 2)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 | — |
| C2 | render-BLOCKED | Q-C03 | — |
| C3 | in-FB | N1, N10 | verified-in-block |
| C4 | render-BLOCKED | Q-C05 / Q-C31 — S4 rejected | — |
| C5 | render-BLOCKED | Q-C02 + rebind-3 | — |
| C6 | render-BLOCKED | instance-stop | — |
| C7 | in-FB | N11, N6 | verified-in-block |
| C8 | in-FB | N7 | verified-in-block |
| C9 | in-FB | N7 OR-terms | verified-in-block |
| C10 | in-FB | N8 | verified-in-block |
| C11 | in-FB | N9 | verified-in-block |
| C12 | render-BLOCKED | instance-stop; wire from `FilterUnit2Flt` | — |
| C13 | render-BLOCKED | instance-stop | — |
| C14 | render-BLOCKED | Q-C02 | — |
| C15 | in-FB | N12 | verified-in-block |
| C16 | in-FB | N13 | verified-in-block |
| C17 | in-FB | N14 | verified-in-block |
| P1 | render-BLOCKED | instance-stop | — |
| P2 | render-BLOCKED | Q-C12 — S3 REJECTED | — |
| P3 | render-BLOCKED | Q-C12 — S3 REJECTED | — |
| P4 | render-BLOCKED | instance-stop; Q-B02 | — |
| P5 | render-BLOCKED | instance-stop | — |
| P6 | render-BLOCKED | Q-C04 | — |
| P7 | render-BLOCKED | instance-stop | — |
| P8 | render-BLOCKED | instance-stop | — |
| P9 | out-of-scope-obligation | `AirStarInst1` not specified in this run | — |
| P10 | render-BLOCKED | instance-stop; map-layer obligation (S5) | — |

## Ledger — `FilterUnitInst1` (Cyclone Filter Unit)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 | — |
| C2 | render-BLOCKED | Q-C03 | — |
| C3 | **rebind** | rebind-2 — `NOT CycloneDustSysOk` → `IO.FaultFB` (latched, N10) preferred over `IO.SystemHealthy` (live permissive, N1); FI-34 stronger guard, C-128 no auto-restart | verified-in-block |
| C4 | render-BLOCKED | Q-C05 / Q-C27 — narrowed by rebind-2, still no health source | — |
| C5 | render-BLOCKED | Q-C27 + rebind-3 — `CycloneDustRemOp` has no interface member to land on | — |
| C6 | render-BLOCKED | instance-stop | — |
| C7 | in-FB | N11, N6 — *enable computed but serves nothing (Q-C20)* | verified-in-block |
| C8 | in-FB | N7 | verified-in-block |
| C9 | in-FB | N7 OR-terms | verified-in-block |
| C10 | in-FB | N8 | verified-in-block |
| C11 | in-FB | N9 | verified-in-block |
| C12 | render-BLOCKED | instance-stop; satisfied only through rebind-2's inverted system-OK path | — |
| C13 | render-BLOCKED | instance-stop | — |
| C14 | render-BLOCKED | Q-C06 — `CycloneDustAutoRunning/Stop`: one signal, two asserted meanings, inverse of each other | — |
| C15 | in-FB | N12 | verified-in-block |
| C16 | in-FB | N13 | verified-in-block |
| C17 | in-FB | N14 | verified-in-block |
| P1 | render-BLOCKED | Q-C19 — no start permissive established | — |
| P2 | render-BLOCKED | Q-C12 — S3 REJECTED | — |
| P3 | render-BLOCKED | Q-C12 — S3 REJECTED | — |
| P4 | render-BLOCKED | instance-stop; Q-B02 | — |
| P5 | render-BLOCKED | instance-stop | — |
| P6 | render-BLOCKED | Q-C04 | — |
| P7 | render-BLOCKED | instance-stop | — |
| P8 | render-BLOCKED | instance-stop | — |
| P9 | render-BLOCKED | Q-C20 — served machine not established; **not** an out-of-scope obligation, because no machine has been identified to owe it to | — |
| P10 | render-BLOCKED | instance-stop; map-layer obligation (S5) | — |

## Ledger — `MotorVSDInst1` (Discharge Conveyor VSD)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 | — |
| C2 | render-BLOCKED | instance-stop; wire `IO.InhibitMotor := DiscreteInputs.AirStarDCIsoFB` uncontested | — |
| C3 | in-FB | `MotorVSDSystem.ir` N1 (`NOT IO.FTR AND NOT IO.FaultActive`) | verified-in-block |
| C4 | render-BLOCKED | Q-C05 / Q-C31 — S4 rejected | — |
| C5 | render-BLOCKED | instance-stop | — |
| C6 | render-BLOCKED | Q-C14 — instance SETPOINT vs plant-wide override, two mechanisms; **min speed 20.0 is verified-in-block** (N6 `CONSTANT MinSpd`) | — |
| C7 | render-BLOCKED | Q-C29 — no reverse feedback; Q-C08 for the forward path | — |
| C8 | in-FB | N13 (`EnableUpstreamTimer`) | verified-in-block |
| C9 | in-FB | N8 | verified-in-block |
| C10 | in-FB | N8 OR-term (`IO.FaultActive`) | verified-in-block |
| C11 | in-FB | N9 (`StartUpTimer`, direction-change re-arm) | verified-in-block |
| C12 | in-FB | N10 (gated by `StartUpTimer.Q`) | verified-in-block |
| C13 | render-BLOCKED | **D-F1** — N11 requires both running feedbacks simultaneously; not satisfied in-FB | — |
| C14 | render-BLOCKED | Q-C30 — `IO.VSDError` has no signal | — |
| C15 | render-BLOCKED | instance-stop; Q-C17 (`VSDFaulrReset` is an unclaimed second reset path) | — |
| C16 | render-BLOCKED | Q-C30 — 11 undriven condition members | — |
| C17 | render-BLOCKED | Q-C08 — **S6 not instantiable**; `IO.RotationSensor` undriven and unread (**D-F2**) | — |
| C18 | in-FB | N14 | verified-in-block |
| C19 | in-FB | N15 | verified-in-block |
| C20 | in-FB | N16 | verified-in-block |
| P1 | render-BLOCKED | instance-stop; term `MotorStarterInst8.IO.UPSEnable` | — |
| P2 | render-BLOCKED | instance-stop; term `ECSControlInst1`'s enable | — |
| P3 | render-BLOCKED | instance-stop; term `NOT AirStarInst1.…ShutdownComplete` | — |
| P4 | render-BLOCKED | instance-stop; Q-B02 | — |
| P5 | render-BLOCKED | instance-stop | — |
| P6 | render-BLOCKED | Q-C04 | — |
| P7 | render-BLOCKED | instance-stop | — |
| P8 | out-of-scope-obligation | `AirStarInst1` consumes this machine's `UPSEnable` on both its start paths; not specified in this run | — |
| P9 | render-BLOCKED | instance-stop; same wire as C2 seen from the plant side | — |
| P10 | render-BLOCKED | instance-stop; no discrete start output exists — command travels through the drive interface | — |

## Ledger — `MotorVSDInst3` (Sorter Conveyor VSD)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 | — |
| C2 | render-BLOCKED | instance-stop; wire `IO.InhibitMotor := DiscreteInputs.OSCIsoFB` uncontested | — |
| C3 | in-FB | N1 | verified-in-block |
| C4 | render-BLOCKED | Q-C05 / Q-C31 — S4 rejected | — |
| C5 | render-BLOCKED | instance-stop | — |
| C6 | render-BLOCKED | Q-C14 | — |
| C7 | render-BLOCKED | Q-C33 — no running feedback of any kind exists | — |
| C8 | in-FB | N13 | verified-in-block |
| C9 | in-FB | N8 | verified-in-block |
| C10 | in-FB | N8 OR-term | verified-in-block |
| C11 | in-FB | N9 | verified-in-block |
| C12 | in-FB | N10 | verified-in-block |
| C13 | render-BLOCKED | **D-F1** | — |
| C14 | render-BLOCKED | Q-C30 | — |
| C15 | render-BLOCKED | instance-stop; Q-C17 | — |
| C16 | render-BLOCKED | Q-C30 | — |
| C17 | render-BLOCKED | **Q-C09 — HARD FAIL (D-F2):** `IO.RotationSensor` undriven while `DiscreteInputs.OSCRotSen` is unclaimed, and `HMIControlSignals.BypassOSCRotSen` does not exist | — |
| C18 | in-FB | N14 | verified-in-block |
| C19 | in-FB | N15 | verified-in-block |
| C20 | in-FB | N16 | verified-in-block |
| P1 | render-BLOCKED | instance-stop; term `DiscreteInputs.TomraReady` | — |
| P2 | **rebind** | rebind-1 — `DiscreteInputs.TomraComFlt` → `TomraControlInst1.Outputs.FaultActive` (N15, 14 machine faults + link loss); stronger/broader guard; **Q-C34 closed** | verified-in-block |
| P3 | render-BLOCKED | Q-C35 — whether the sorter's *enable* is additionally required is not established | — |
| P4 | render-BLOCKED | instance-stop; term `MotorStarterInst6`'s enable | — |
| P5 | render-BLOCKED | instance-stop; term `MotorStarterInst7`'s enable | — |
| P6 | render-BLOCKED | instance-stop; term `NOT ECSControlInst1.…ShutdownComplete` | — |
| P7 | render-BLOCKED | instance-stop; Q-B02 | — |
| P8 | render-BLOCKED | instance-stop | — |
| P9 | render-BLOCKED | **Q-C13** — whose hand intervention gates this machine's shutdown is contested; mirrored on `TomraControlInst1` P6 | — |
| P10 | render-BLOCKED | instance-stop | — |
| P11 | render-BLOCKED | instance-stop — the term lives in `FilterUnitInst2` P1 and `FilterUnitInst3` P1, both stopped; **in scope, so not an out-of-scope obligation** | — |
| P12 | out-of-scope-obligation | `ECSControlInst1` consumes this machine's `UPSEnable`; not specified in this run | — |
| P13 | render-BLOCKED | instance-stop; no discrete start output — drive-interface command | — |
| P14 | render-BLOCKED | instance-stop; same wire as C2 seen from the plant side | — |

## Ledger — `TomraControlInst1` (Optical Sorter)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | render-BLOCKED | Q-C04 (+ Q-A17: the class's own N7 makes a hand start reachable only while faulted) | — |
| C2 | render-BLOCKED | Q-C11 — no isolator signal | — |
| C3 | in-FB | `TomraControlSystem.ir` N6 | verified-in-block |
| C4 | render-BLOCKED | instance-stop | — |
| C5 | render-BLOCKED | **Q-C10** — the link half has no tags; the FB cannot be called | — |
| C6 | render-BLOCKED | **Q-C10** — `inputWord0…7` undriven | — |
| C7 | in-FB | N5 (`LifeBitTimer`, `PT := T#5S` — **D-F8**) | verified-in-block |
| C8 | in-FB | N2, N4 (link/hardwire agreement and dead-link fallback) | verified-in-block |
| C9 | in-FB | N16 (enable ANDed with `Outputs.OSRunning` — withdrawn immediately on loss of running) | verified-in-block |
| C10 | in-FB | N12 (no fault/hand escape — the recorded class difference) | verified-in-block |
| C11 | render-BLOCKED | **D-F3** — `ControlWord0.%X1` never written, so N13's fail-to-run can never trip | — |
| C12 | in-FB | N14 | verified-in-block |
| C13 | render-BLOCKED | **Q-C10** — N15 aggregates status bits sourced from link words that do not exist | — |
| C14 | render-BLOCKED | **Q-C10** — the link echo (N3 → `ControlWord0.%X7`) cannot reach the machine | — |
| C15 | in-FB | N17 | verified-in-block |
| C16 | in-FB | N18 | verified-in-block |
| C17 | in-FB | N19 (alarms — outside the control spec's own scope) | verified-in-block |
| C18 | render-BLOCKED | **Q-C36 / D-F4** — no operator signals; members declared and never used | — |
| C19 | in-FB | N20 — selector is `PlantControl.Test[5]` (**D-F7**; Q-C18 contests whether it *should* be) | verified-in-block |
| P1 | render-BLOCKED | instance-stop; term `MotorStarterInst6`'s enable | — |
| P2 | render-BLOCKED | instance-stop; term `MotorStarterInst7`'s enable | — |
| P3 | render-BLOCKED | instance-stop; term `NOT MotorVSDInst3.IO.ShutdownComplete` | — |
| P4 | render-BLOCKED | instance-stop; Q-B02 | — |
| P5 | render-BLOCKED | instance-stop | — |
| P6 | render-BLOCKED | **Q-C13** — mirrored on `MotorVSDInst3` P9 | — |
| P7 | render-BLOCKED | instance-stop | — |
| P8 | render-BLOCKED | instance-stop — the term lives in `MotorVSDInst3` P1/P2, stopped; in scope | — |
| P9 | render-BLOCKED | instance-stop; wire `DiscreteOutputs.TomraRun := HardWireSignals.Run` | — |
| P10 | out-of-scope-obligation | the effect lands on `MotorStarterInst6`/`MotorStarterInst7`, which hold through shutdown on this machine's `ShutdownComplete` and are not specified in this run (Q-A18/Q-B06) | — |

---

# D3 — Per-instance render

## **STOPPED — all six instances. No render produced.**

This rung's own stop condition: *"a spec carrying an unresolved BLOCKING `Q-nn` that affects logic
you would render → stop and report; never render a guess for a contested requirement."*

Every one of the six specs carries blocking questions that reach the **core** of what its rung would
render — the auto-start permissive, the shutdown expression, the running confirmation, or the ability
to call the FB at all. Per instance:

| Instance | Stopped by | What could not be written |
|---|---|---|
| `FilterUnitInst2` | Q-C04, Q-C03, Q-C05, Q-C01, Q-C12 | `IO.InHand`, `IO.InhibitMotor`, `IO.SystemHealthy`, `IO.RunningFB`, and the `Shutdown` expression's two hold terms |
| `FilterUnitInst3` | Q-C04, Q-C03, Q-C05, Q-C02, Q-C12 | same five |
| `FilterUnitInst1` | Q-C04, Q-C03, Q-C05, Q-C06, Q-C12, Q-C19, Q-C20 | the above **plus** the entire `AutoStartSignal` permissive (no start permissive established) |
| `MotorVSDInst1` | Q-C04, Q-C05, Q-C08, Q-C14, Q-C29, Q-C30 | `IO.RunningFwdFB` (the machine's only running confirmation), the speed setpoint source, `IO.VSDError` |
| `MotorVSDInst3` | Q-C04, Q-C05, Q-C09, Q-C13, Q-C14, Q-C30, Q-C33, Q-C35 | `IO.RunningFwdFB` (no signal at all), `IO.RotationSensor` (HARD FAIL), the `Shutdown` hand term, the sorter-enable permissive |
| `TomraControlInst1` | **Q-C10**, Q-C04, Q-C11, Q-C13 | **the `CALL` itself** — eight `inputWord` parameters and two `OutputWord` parameters have no tags |

**`TomraControlInst1` is a hard structural stop, not a judgement call:** `TomraControlSystem` takes
its eight process-data words as FB **INPUT** parameters. Without `Tag_45`…`Tag_54` there is no legal
call site, so no amount of answering the other questions would let this instance be rendered.

**Nothing was rendered partially, and no term was written on a preferred reading.** Where FI-34
would have applied (a forced choice on a protective term), the choice was **not** forced — the
relation was left blocked and the fail-safe preference was recorded as the intended answer *if* the
owner rules it must proceed:

| Contested protective term | Fail-safe reading recorded (NOT applied) |
|---|---|
| Q-C08 — running confirmation while the motion-sensor bypass is active | the bypass does **not** synthesise a running confirmation |
| Q-C12 — how the two shutdown-hold conditions combine | hold until **both** (the longer hold; a fan kept running is safer than material left in a duct) |
| Q-C13 — whose hand intervention suppresses whose shutdown | **its own** (the per-machine rule of B-19), the narrower suppression |
| Q-C05 — system-healthy source | a real signal, never a constant true |

---

# Open questions at rung D

**Closed at this rung, by the sanctioned mechanism:**

- **Q-C34** — closed by `rebind-1`. Rung C explicitly deferred the decision to D1's binding audit,
  which has the FB interface as ground truth; the broader guard (`Outputs.FaultActive`) is chosen
  and argued.
- **Q-B11** — answered as a fact: the sorter's link watchdog is **5 s** (`verified-in-block`).
- **Q-C27** — **narrowed** by `rebind-2` (which member the system-OK signal drives), **not closed**
  (no source for `IO.SystemHealthy` exists).

**Raised at this rung (all blocking for the Build stage):**

- **Q-D01** — `MotorVSDSystem` fail-to-stop requires both running feedbacks simultaneously (D-F1).
  Is that intended, or a defect in the library block? It makes C13/REQ-094/REQ-124 unreachable on
  single-direction drives, which is both in-scope VSDs.
- **Q-D02** — `TomraControlSystem` fail-to-run is gated on `ControlWord0.%X1`, which nothing writes
  (D-F3). C11/REQ-156 is unreachable. Is `%X1` meant to be written by the caller, by another block,
  or is the term a defect?
- **Q-D03** — `RecentStart` is required by every run latch in all three FBs and driven by nothing in
  the export (D-F6). Where does it come from? Four of the six instances start FALSE and cannot run.
- **Q-D04** — `MotorVSDSystem` N6 overwrites the HMI-owned hand speed setpoint every scan (D-F5,
  C-308). Fix in the library block, or accept and document?
- **Q-D05** — `TomraControlSystem` reads `PlantControl.Test[5]`/`Test[7]` from inside a reusable
  equipment FB (D-F7). Both a C-127/C-304 boundary question and a production-behaviour-from-a-test-
  array question.
- **Q-D06** — three fault-shaped VSD interface members (`VSDWarning`, `OverTempFault`,
  `ThermalOLFault`) default **TRUE** in both instance DBs while being undriven. Intended, or a
  commissioning artefact?

**Carried and still blocking:** all 30 rung-C blocking questions except Q-C34; plus Q-A01, Q-A02,
Q-A03, Q-A04, Q-A05, Q-A06, Q-A10, Q-A11, Q-A13, Q-A16, Q-B01, Q-B07, Q-B09, Q-B12, Q-B13, Q-B17,
Q-B18.

---

# Gate 1 — engineer sign-off

**STATUS: NOT SIGNABLE.**

Gate 1 authorises the Build coding stage. It cannot be signed on this artifact because:

1. **D3 produced no render for any of the six instances** — there is nothing for `gen-block-new` to
   code.
2. **35 blocking questions stand** (30 from rung C, less Q-C34 closed here, plus 6 raised at D3's
   findings — Q-D01…Q-D06), 17 more carried from rungs A and B. Q-D07 (no startup-reset block) is
   raised in `architecture.md` §3, bringing the rung-D total to 7 and the standing total to **36**.
3. **Three HARD FAILs** stand in the undriven-input audit (`IO.RotationSensor` on `MotorVSDInst3`;
   `IO.SystemHealthy` and `IO.InHand` on every instance).
4. **Two structural blockers** are not questions of interpretation at all: the optical sorter's FB
   cannot be called without tags that do not exist, and four of the six instances cannot latch a run
   command as their given boundary stands.

**Signed:** — *(unsigned)*
**Date:** — *(none)*

*This rung reached a well-formed stop. The ledger is complete (174/174, set-difference 0 both ways),
no discharge rests on an unverifiable precondition, and no contested relation was rendered on a
guess.*
