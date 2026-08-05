# PlantAutoControl-bench-rerun3 — Code structure (rung D)

Produced by `/gen-code-structure` (rung D of A → B → C → D), inside `lad-coder` (hard rule 8).
**Booleans and interface members enter at this rung** — and, as it turns out, do not get as far as a
render: see D3.

## Provenance

- **Produced:** 2026-08-05, `gen-code-structure`.
- **Inputs:** `gen/PlantAutoControl-bench-rerun3/equipment-specs/*.md` (6 specs, 117 `C-nn` + 51 `P-nn`);
  `gen/PlantAutoControl-bench-rerun3/requirements.md`; `gen/PlantAutoControl-bench-rerun3/unclaimed-signals.md`;
  `patterns/chained-permissive-enable/pattern.md` (ADMITTED), `patterns/input-mapping/pattern.md`,
  `patterns/motor-dol/pattern.md` (read for shape vocabulary); `docs/06-lad-conventions.md` (read
  fresh this run); the full IR of the three library FBs
  (`ir/PlantAutoControl-bench/{FilterUnitSystem,MotorVSDSystem,TomraControlSystem}.ir`) and the six instance DBs.
- **No `.ir` was written.** This rung structures; coding is `gen-block-new`'s job.
- **Outcome: D0, D1 and D2 complete. D3 STOPPED** on this rung's own stop condition. The manifest
  (`architecture.md`) is therefore emitted **PARTIAL and NOT SIGNABLE**.

---

# D0 — Declared logic shapes (declared BEFORE any rendering)

## S1 — `chained-permissive-enable`, one orchestration network per equipment instance

- **What it discharges:** the *structure* of each instance's orchestration network — that the
  instance's interface is wired from plant state, neighbour enables and field signals in one network,
  ending in the FB call, with the physical start output as an optional last coil.
- **The argument:** the pattern is ADMITTED (`patterns/chained-permissive-enable/pattern.md`,
  signed 2026-07-15), is instantiated 18+ times in reviewed working logic **in this same plant**, and
  its documented skeleton maps one-to-one onto the relation kinds rung C produced
  (`RunningFB` / `SystemHealthy` / `InhibitMotor` / `PreStartDone` / `AutoStartSignal` / `Shutdown` /
  `FaultReset` / optional start coil / `CALL`). It also satisfies C-109/C-110 (the orchestrating FC
  reads as a table of contents, one network per equipment) and C-106 (one call per instance, same
  block, same shape).
- **Preconditions, classified:**
  | # | Precondition | Class |
  |---|---|---|
  | S1-a | Each instance's start permissive resolves to a named neighbour's `UPSEnable` or a named plant flag | **verified-cross-block** — rung A relation set + `PlantControl` members (in `ir/PlantAutoControl-bench/Control.ir` - the filenames are swapped in this project, see `unclaimed-signals.md` §7) |
  | S1-b | The instance DB and FB type exist and the FB's interface carries the skeleton's members | **verified-cross-block** — `ir/PlantAutoControl-bench/{FilterUnitSystem,MotorVSDSystem,TomraControlSystem}.ir` INTERFACE, and `tagstatus` on 19 interface members (all EXISTS) |
  | S1-c | The plant run-state encoding (`Status >= 1` = running, `Status = -1` = controlled shutdown) | **UNVERIFIABLE** — computed in the plant sequencer, outside this generation's boundary; the source register flags it as reverse-derived (its Q-01) |
- **Consequence of S1-c, applied not noted:** because one precondition is `unverifiable`, **S1 may
  not discharge any relation whose truth depends on the run-state encoding.** Every such relation
  (`P6`/`P7`/`P8`/`P10` "commanded to start and run automatically while the plant is running", and
  every shutdown-hold) is therefore carried as a **term to be rendered**, not discharged. This is the
  documented regression path — *"the plant flag probably already encodes the neighbour's
  shutdown-complete"* — and it is refused here rather than argued around.

## S2 — `combinational-interlock` (C-113 memory test = **No**)

- **What it discharges:** the absence of any stepped-sequence machinery — no `Step` Int (C-118), no
  step legend (C-120), no transition `MOVE`s (C-121), no dwell timers (C-122) — for all six
  instances.
- **The argument:** C-113's memory test applied to the rung-C relation set: not one of the 168
  relations requires knowing *which phase* the machine is in to know what to do next. Every
  permissive is a function of current plant state, current neighbour enables and current feedback.
  The stateful parts (fault latches, up-to-speed and shutdown timers) live **inside** the equipment
  FBs, not in the orchestration layer.
- **Preconditions:** no in-scope relation is phase-dependent — **verified-in-artifact**: enumerated
  over all 168 rows of the D2 ledger; the two stateful behaviours the source register identifies
  (a feed-conveyor reverse latch, a pre-start one-shot) belong to machines outside this run's scope.

## S3 — whole-block reuse of the three given FB types (C-108 / C-106, C-606 whole-reuse exception)

- **What it discharges:** every class requirement satisfied by mechanism *inside* the reused FB —
  fault latching and reset, the up-to-speed enable timer, the shutdown-complete timer, hours
  totalising, status telemetry.
- **The argument:** each FB's own logic implements its class requirement set (the class references
  were derived from these very FBs). A block carrying more than the spec asks is a valid fit and is
  not flagged (C-606 whole-reuse exception).
- **Preconditions:** the FB actually implements the cited mechanism — **verified-cross-block**, cited
  per ledger row as the FB network number.

## S4 — a shape that is deliberately NOT declared

**Refused: any shape that compacts a filter unit's two shutdown-hold conditions into one term.**
The compaction would need the argument *"the plant `FansShutdownReady` flag already accounts for the
Air-Separator VSD's shutdown-complete"*. That flag is computed in another block, outside this
generation's boundary — the argument is `unverifiable`, and §D2 is explicit that plausibility is not
verification. Both conditions stay as two separate terms (rung C already refused the merge for the
same reason). **Recording the refusal is the point:** an undeclared discharge here is the documented
cause of a real dropped-interlock regression, and a shape that is never written down is never
reviewed.

---

# D1 — Block fit, grouping, binding audit, undriven-input audit

## Block fit and instance grouping (derived, not assumed)

| Class | Library FB | Block NUMBER | Instances | Instance DB source |
|---|---|---|---|---|
| FilterUnitSystem | `FilterUnitSystem` | 8 | `FilterUnitInst2`, `FilterUnitInst3`, `FilterUnitInst1` | `FilterUnitInst2.ir`, `FilterUnitInst3.ir`, `FilterUnitInst1.ir` |
| vsd-motor | `MotorVSDSystem` | 42 | `MotorVSDInst1`, `MotorVSDInst3` | `MotorVSDInst1.ir`, `MotorVSDInst3.ir` |
| optical-sorter | `TomraControlSystem` | 5 | `TomraControlInst1` | `TomraControlInst1.ir` |

**The grouping is derived from rung A's per-instance expansion, as intended.** `FilterUnitInst2` and
`FilterUnitInst3` were recorded at rung A as source-identical in every column — that recorded fact,
not a resemblance noticed here, is what licenses giving them the same FB and the same network shape
with different signal families. `FilterUnitInst1` shares the FB but **not** the shape: it has no
neighbour start permissive and its fault condition comes from a system-OK signal rather than a fault
input, so its network differs by construction and that difference is traceable to two recorded
deltas rather than to drift (C-106/C-607).

**Coverage:** all 117 `C-nn` relations map onto a class requirement the chosen FB implements or
exposes; no relation requires a capability none of the three FBs has. No new block is proposed
(C-108). Extra FB capability beyond the specs (speed control on `MotorVSDInst1`, the sorter's alarm
words) is accepted, not flagged (C-606 whole-reuse exception).

**Settings (C-308 discipline, applied):** every setting rung C listed is instance-owned, and every
one is left at its **instance-DB default**. The orchestration layer will **not** cyclically copy
`ProcessTimings.Global*` values onto them — that is the documented C-308 scan-copy trap, and it is
live here because a whole global-overwrite mechanism exists for exactly these members
(`unclaimed-signals.md` Q-C23). The mechanism is unspecified, so nothing is wired to it.

**Divergent instance defaults, recorded because the specs say "not stated":**

| Setting | FilterUnitInst1 | FilterUnitInst2 | FilterUnitInst3 | MotorVSDInst1 | MotorVSDInst3 | TomraControlInst1 |
|---|---|---|---|---|---|---|
| `FTTime` (fail-to-run/stop) | 60.0 | 2.0 | 3.0 | 3.0 | 5.0 | 3.0 |
| `EnableUPSTime` (up-to-speed) | 10.0 | 10.0 | 10.0 | 2.0 | 2.0 | 2.0 |
| `ShutdownTime` | 5.0 | 5.0 | 5.0 | — | — | 2.0 |
| `StartUpTime` (ramp) | — | — | — | 8.0 | 5.0 | — |
| `HandSpeedInput` | — | — | — | 30.0 | 20.0 | — |

A 30× spread on the same setting across three instances of one class (60 s / 2 s / 3 s), against
sources that state **no value at all** for any of them, is not a detail — it is the shape of
commissioning values that were tuned and never written down. Left untouched (C-308), reported
(Q-C15, sharpened).

## Binding audit (mandatory) — rung-C bindings checked against the FB interfaces

Five findings. Every one is a `rebind` or a dead-interface cross-check; none is applied silently, and
none is applied at all, because D3 is stopped.

**BA-1 — `MotorVSDInst1` C11 (motion sensor → running-forward confirmation): rebind the interface
target.**
Rung C bound the *signal* correctly (`DiscreteInputs.AirStarDCRotSen`, on REQ-012's explicit
statement). But the FB exposes **two** plausible targets:
- `IO.RotationSensor` — `undriven-scan` classifies it **DEAD-INTERFACE on both VSD instances**: no
  writer, *and the FB never reads it*. Wiring the sensor here would be a silent no-op — the exact
  documented shape (a real VSD FB declaring a rotation-sensor member that nothing wires and nothing
  reads).
- `IO.RunningFwdFB` — read by the FB's run-confirmation, fail-to-run/fail-to-stop and
  shutdown-complete logic (class-reference provenance: networks 8, 9, 13).
**Preferred: `IO.RunningFwdFB`** — the stronger/broader guard, and the only one the FB acts on.
Recorded as a `rebind`, not applied (D3 stopped).

**BA-2 — `MotorVSDInst3` C18 / Q-C07: dead interface confirms the gap.**
`IO.RotationSensor` is DEAD-INTERFACE on this instance too. Crossed against the spec: `C18` records
that a physical motion sensor exists for this machine with no stated use. The tool shows the
capability is neither wired nor read. **`dead-interface` does not auto-fail — but here a `C-nn`
relation did require the capability, which is exactly how the tool turns a fact into a finding.**

**BA-3 — `TomraControlInst1` C6 and `MotorVSDInst3` P1 (the sorter's ready state): a dead candidate
and two live ones.**
`HardWireSignals.Ready` is **DEAD-INTERFACE** — the FB declares a hardwired ready member and never
reads it. So:
- binding the neighbour's ready permissive to `TomraControlInst1.HardWireSignals.Ready` would bind to
  a member nothing consumes;
- `DiscreteInputs.TomraReady` is the live raw signal;
- `TomraControlInst1.Outputs.UPSEnable` is the **broader** guard — the FB computes it from ready
  *and* confirmed-running-for-the-enable-time, and withdraws it immediately on loss of running
  (class reference OS-09, network 16).
**Recommendation to whoever answers Q-C12: `Outputs.UPSEnable` is the stronger guard.** Stated as a
recommendation, **not applied** — Q-C12 is a blocking rung-C question, and this rung does not resolve
a contested requirement even when its own binding-audit rule has a preference.

**BA-4 — `MotorVSDInst3` P2 (the sorter's not-faulted state): three candidates, materially different
widths.**
- `DiscreteInputs.TomraComFlt` — raw, comms failure only.
- `Outputs.OSComFault` — the FB's link-aware comms-fault output (network 2).
- `Outputs.FaultActive` — the FB's aggregated fault: 14 machine status bits ORed with loss of the
  data link (network 15).
Binding the narrowest where the widest was meant silently deletes most of a protective permissive.
**Recommendation: `Outputs.FaultActive`, the broader guard (FI-38: on a protective term, prefer the
stronger/fail-safe guard).** Again stated, not applied — Q-C12 blocks.

**BA-5 — `TomraControlInst1` C18: dead interface confirms an unimplementable requirement.**
`Inputs.ProgramSelection` and `Inputs.Belt1OnDelay` are both DEAD-INTERFACE. The spec already carried
this as an interface promise the as-built does not implement (Q-C19, from class anomaly A-3); the
tool independently confirms neither is wired nor read.

## Undriven-input audit — COMPUTED, per instance

```
converter undriven-scan --project ir/PlantAutoControl-bench --fb FilterUnitSystem          → exit 1
   204 member/instance pairs · 45 undriven · 0 disarmed · 72 defaulted · 87 dead-interface
converter undriven-scan --project ir/PlantAutoControl-bench --fb MotorVSDSystem \
                        --instance MotorVSDInst1 --instance MotorVSDInst3             → exit 1
   188 member/instance pairs · 36 undriven · 0 disarmed · 64 defaulted · 88 dead-interface
converter undriven-scan --project ir/PlantAutoControl-bench --fb TomraControlSystem        → exit 1
    89 member/instance pairs · 38 undriven · 0 disarmed · 13 defaulted · 38 dead-interface
```

**Read this correctly:** the orchestration block that would drive these instances is exactly what
this pipeline has not yet produced, so *every* caller-driven input is undriven by construction. The
audit's value here is therefore **not** "these inputs are undriven" — it is **what value each input
takes if its term is omitted**, which is the failure mode of a render that stops half-done.

**The finding that matters — which omissions fail OPEN:**

| Interface input | Default if not driven | Fails… | Spec relation at stake |
|---|---|---|---|
| `IO.SystemHealthy` | **TRUE** | **OPEN** | C4 on `FilterUnitInst1/2/3`, `MotorVSDInst1/3` — five relations, all satisfied by a hardcoded truth (Q-C20) |
| `IO.InhibitMotor` | **FALSE** (= not inhibited) | **OPEN** | C2 on `FilterUnitInst1/2/3`, `TomraControlInst1` — the four machines with **no isolation signal at all** (Q-C03) |
| `IO.ShutdownComplete` | **TRUE** | **OPEN** (for the machine waiting on it) | every shutdown-hold relation: a neighbour is released by default |
| `IO.PreStartDone` | FALSE | safe (blocks start) | C6 |
| `IO.AutoStartSignal` | FALSE | safe (blocks start) | every start permissive |
| `IO.FaultReset` | FALSE | safe | C13 |
| `IO.RunningFB` / `RunningFwdFB` | FALSE | safe-ish (drives fail-to-run) | C14 / C11 |

**Q-C03 and Q-C20 are therefore worse than "a missing binding".** In both cases the class requirement
exists, no signal exists to satisfy it, and the interface default leaves the permissive **open**. A
reviewer reading the finished network would see a machine whose spec says *"does not start while
isolated"* and *"does not start unless healthy"*, and no term implementing either — with nothing in
the ladder to make the absence visible. That is the invisible-missing-interlock shape, computed
rather than argued.

**`dead-interface` cross-check (not an auto-fail):** 213 dead-interface pairs across the three FBs.
Nearly all are timer sub-members and internal edge arrays — legitimate. Four are crossed against spec
relations and become findings: `IO.RotationSensor` ×2 (BA-1, BA-2), `HardWireSignals.Ready` (BA-3),
`Inputs.ProgramSelection`/`Belt1OnDelay` (BA-5).

**`--hints` was not used** — heuristic, opt-in, and never part of a finding.

---

# D2 — Discharge ledger

**Every one of the 168 spec relations appears exactly once.**

| Count | Value |
|---|---|
| `C-nn` + `P-nn` ids in `equipment-specs/` | **168** |
| Rows in this ledger | **168** |
| **Set difference, both directions** | **empty** |

## Disposition rules, declared before the table

1. **If the relation requires any term in the orchestration layer**, its disposition is that term's:
   `rendered` (none — D3 stopped) or **`render-BLOCKED`**.
2. **`in-FB`** only where the relation needs **no** orchestration term — mechanism wholly inside the
   reused FB. Evidence cites the FB network.
3. **`discharged`** by a declared D0 shape, with an evidence citation and a precondition class.
   **No relation is discharged in this run** — S1's run-state precondition is `unverifiable`, S2
   discharges the *absence* of machinery rather than any relation, and S3's discharges are recorded
   as `in-FB` (the more specific disposition) rather than double-counted.
4. **`rebind`** where D1's binding audit found a stronger/broader interface target than rung C's.
5. **`out-of-scope-obligation`** where the relation's counterpart term lives outside this run —
   another machine's network, the alarms artifact, or the HMI command surface. The owed obligation is
   named in every case.
6. **`render-BLOCKED` sub-kinds**, distinguished because they are not the same problem:
   - `[contested]` — the term itself is contested by a blocking `Q-nn`;
   - `[network-stop]` — the term is fully determined, but its network cannot be rendered because
     other terms in the same network are contested. A network is rendered as a unit; rendering the
     determined half would produce a network that looks complete and silently omits a permissive.

Per the contract, `render-BLOCKED` and `out-of-scope-obligation` **count toward the set-difference,
are never discharges, and carry no precondition class**.

## Ledger — `FilterUnitInst2` (Dust Filter Unit 1)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `IO.Run` (instance member `FilterUnitInst2.IO.Run`) — auto/hand selection, `FilterUnitSystem` N1; hand inputs owed to the HMI surface (undriven) | verified-cross-block |
| C2 | render-BLOCKED `[contested]` | **Q-C03** — no isolation signal; `IO.InhibitMotor` default FALSE **fails OPEN** | — |
| C3 | in-FB | FB writes `IO.FaultActive` (instance member `FilterUnitInst2.IO.FaultActive`) — fault gate in the run permissive, N1 | verified-cross-block |
| C4 | render-BLOCKED `[contested]` | **Q-C20** — no health signal; `IO.SystemHealthy` default TRUE **fails OPEN** | — |
| C5 | render-BLOCKED `[contested]` | **Q-C01** — `FilterUnit1Ready` vs `FilterUnit1Op` | — |
| C6 | render-BLOCKED `[network-stop]` | term determined: `PreStartDone := PlantControl.PreStartComplete` | — |
| C7 | in-FB | FB writes `IO.UPSEnable` (instance member `FilterUnitInst2.IO.UPSEnable`) — enable timer, N11 | verified-cross-block |
| C8 | in-FB | FB writes `IO.StopMotor` (instance member `FilterUnitInst2.IO.StopMotor`) — shutdown timer + complete, N7 | verified-cross-block |
| C9 | in-FB | FB writes `IO.ShutdownComplete` (instance member `FilterUnitInst2.IO.ShutdownComplete`) — fault/hand escape terms on shutdown-complete, N7 | verified-cross-block |
| C10 | in-FB | FB writes `IO.FTR` (instance member `FilterUnitInst2.IO.FTR`) — fail-to-run trip timer, N8 | verified-cross-block |
| C11 | in-FB | FB writes `IO.FTS` (instance member `FilterUnitInst2.IO.FTS`) — fail-to-stop timer, N9 | verified-cross-block |
| C12 | render-BLOCKED `[network-stop]` | term determined: `FaultFB := DiscreteInputs.FilterUnit1Flt` (set-outside/clear-inside, C-103 documented exception); latch in-FB N10 | — |
| C13 | render-BLOCKED `[network-stop]` | terms determined: `FaultReset := HMIControlSignals.SystemReset`; `DiscreteOutputs.FilterUnit1Reset := ` same | — |
| C14 | render-BLOCKED `[contested]` | **Q-C01** | — |
| C15 | in-FB | FB writes `IO.HrsRun` (instance member `FilterUnitInst2.IO.HrsRun`) — hours totaliser, N12 | verified-cross-block |
| C16 | in-FB | FB writes `IO.Telemetry` (instance member `FilterUnitInst2.IO.Telemetry`) — telemetry, N13 | verified-cross-block |
| C17 | out-of-scope-obligation | alarms artifact; FB produces `IO.Alarm` (N14), the alarm layer consumes it | — |
| C18 | render-BLOCKED `[network-stop]` | term determined: `DiscreteOutputs.FilterUnit1Start := IO.Run` | — |
| P1 | render-BLOCKED `[network-stop]` | term determined: `MotorVSDInst3.IO.UPSEnable` in the start permissive | — |
| P2 | render-BLOCKED `[contested]` | **Q-C09** — combination with P3 unsettled; S4 refused the compaction | — |
| P3 | render-BLOCKED `[contested]` | **Q-C09** | — |
| P4 | out-of-scope-obligation | owed: `AirStarInst1`'s start permissive must read this unit's `UPSEnable` (that network is outside this run) | — |
| P5 | out-of-scope-obligation | owed: `AirStarInst1`'s alternative start path, same network | — |
| P6 | render-BLOCKED `[contested]` | run-state term; S1-c **unverifiable**, so it must be a term, and Q-C09 contests the shutdown half | — |
| P7 | render-BLOCKED `[network-stop]` | term determined: `NOT FilterUnitInst2.IO.HandIntervention` in the shutdown expression | — |

## Ledger — `FilterUnitInst3` (Dust Filter Unit 2)

Structurally identical to `FilterUnitInst2`; only the signal family differs (`FilterUnit2*`) and the
binding question is **Q-C02** rather than Q-C01.

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `IO.Run` (instance member `FilterUnitInst3.IO.Run`) — N1 | verified-cross-block |
| C2 | render-BLOCKED `[contested]` | **Q-C03** — fails OPEN | — |
| C3 | in-FB | FB writes `IO.FaultActive` (instance member `FilterUnitInst3.IO.FaultActive`) — N1 | verified-cross-block |
| C4 | render-BLOCKED `[contested]` | **Q-C20** — fails OPEN | — |
| C5 | render-BLOCKED `[contested]` | **Q-C02** | — |
| C6 | render-BLOCKED `[network-stop]` | `PreStartDone := PlantControl.PreStartComplete` | — |
| C7 | in-FB | FB writes `IO.UPSEnable` (instance member `FilterUnitInst3.IO.UPSEnable`) — N11 | verified-cross-block |
| C8 | in-FB | FB writes `IO.StopMotor` (instance member `FilterUnitInst3.IO.StopMotor`) — N7 | verified-cross-block |
| C9 | in-FB | FB writes `IO.ShutdownComplete` (instance member `FilterUnitInst3.IO.ShutdownComplete`) — N7 | verified-cross-block |
| C10 | in-FB | FB writes `IO.FTR` (instance member `FilterUnitInst3.IO.FTR`) — N8 | verified-cross-block |
| C11 | in-FB | FB writes `IO.FTS` (instance member `FilterUnitInst3.IO.FTS`) — N9 | verified-cross-block |
| C12 | render-BLOCKED `[network-stop]` | `FaultFB := DiscreteInputs.FilterUnit2Flt` | — |
| C13 | render-BLOCKED `[network-stop]` | `FaultReset` / `DiscreteOutputs.FilterUnit2Reset` | — |
| C14 | render-BLOCKED `[contested]` | **Q-C02** | — |
| C15 | in-FB | FB writes `IO.HrsRun` (instance member `FilterUnitInst3.IO.HrsRun`) — N12 | verified-cross-block |
| C16 | in-FB | FB writes `IO.Telemetry` (instance member `FilterUnitInst3.IO.Telemetry`) — N13 | verified-cross-block |
| C17 | out-of-scope-obligation | alarms artifact | — |
| C18 | render-BLOCKED `[network-stop]` | `DiscreteOutputs.FilterUnit2Start := IO.Run` | — |
| P1 | render-BLOCKED `[network-stop]` | `MotorVSDInst3.IO.UPSEnable` | — |
| P2 | render-BLOCKED `[contested]` | **Q-C09** | — |
| P3 | render-BLOCKED `[contested]` | **Q-C09** | — |
| P4 | out-of-scope-obligation | owed: `AirStarInst1` start permissive | — |
| P5 | out-of-scope-obligation | owed: `AirStarInst1` alternative path | — |
| P6 | render-BLOCKED `[contested]` | S1-c unverifiable + Q-C09 | — |
| P7 | render-BLOCKED `[network-stop]` | `NOT FilterUnitInst3.IO.HandIntervention` | — |

## Ledger — `FilterUnitInst1` (Cyclone Filter Unit)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `IO.Run` (instance member `FilterUnitInst1.IO.Run`) — N1 | verified-cross-block |
| C2 | render-BLOCKED `[contested]` | **Q-C03** — fails OPEN | — |
| C3 | in-FB | FB writes `IO.FaultActive` (instance member `FilterUnitInst1.IO.FaultActive`) — N1 | verified-cross-block |
| C4 | render-BLOCKED `[contested]` | **Q-C20** — fails OPEN | — |
| C5 | render-BLOCKED `[network-stop]` | term determined: remote-operational from `DiscreteInputs.CycloneDustRemOp` | — |
| C6 | render-BLOCKED `[network-stop]` | `PreStartDone := PlantControl.PreStartComplete` | — |
| C7 | in-FB | FB writes `IO.UPSEnable` (instance member `FilterUnitInst1.IO.UPSEnable`) — N11 — **but the enable it produces has no consumer** (Q-A7) | verified-cross-block |
| C8 | in-FB | FB writes `IO.StopMotor` (instance member `FilterUnitInst1.IO.StopMotor`) — N7 | verified-cross-block |
| C9 | in-FB | FB writes `IO.ShutdownComplete` (instance member `FilterUnitInst1.IO.ShutdownComplete`) — N7 | verified-cross-block |
| C10 | in-FB | FB writes `IO.FTR` (instance member `FilterUnitInst1.IO.FTR`) — N8 | verified-cross-block |
| C11 | in-FB | FB writes `IO.FTS` (instance member `FilterUnitInst1.IO.FTS`) — N9 | verified-cross-block |
| C12 | render-BLOCKED `[network-stop]` | term determined: fault condition from `DiscreteInputs.CycloneDustSysOk` in its healthy sense | — |
| C13 | render-BLOCKED `[network-stop]` | `FaultReset` / `DiscreteOutputs.CycloneDustFilterReset` | — |
| C14 | render-BLOCKED `[contested]` | **Q-C05** — what `CycloneDustAutoRunning/Stop` asserts is unresolved | — |
| C15 | in-FB | FB writes `IO.HrsRun` (instance member `FilterUnitInst1.IO.HrsRun`) — N12 | verified-cross-block |
| C16 | in-FB | FB writes `IO.Telemetry` (instance member `FilterUnitInst1.IO.Telemetry`) — N13 | verified-cross-block |
| C17 | out-of-scope-obligation | alarms artifact | — |
| C18 | render-BLOCKED `[network-stop]` | `DiscreteOutputs.CycloneDustFilterStart := IO.Run` | — |
| P1 | render-BLOCKED `[contested]` | no neighbour term; the start rests on the run state alone — S1-c **unverifiable** | — |
| P2 | render-BLOCKED `[contested]` | **Q-C09** | — |
| P3 | render-BLOCKED `[contested]` | **Q-C09** | — |
| P4 | out-of-scope-obligation | owed: **nothing** — no machine consumes this enable. The obligation is recorded as unowed, which is Q-A7's whole point | — |
| P5 | render-BLOCKED `[contested]` | S1-c unverifiable + Q-C09 | — |
| P6 | render-BLOCKED `[network-stop]` | `NOT FilterUnitInst1.IO.HandIntervention` | — |

## Ledger — `MotorVSDInst1` (Discharge Conveyor VSD)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `IO.Run` (instance member `MotorVSDInst1.IO.Run`) — N1 | verified-cross-block |
| C2 | render-BLOCKED `[network-stop]` | term determined: `InhibitMotor := DiscreteInputs.AirStarDCIsoFB` | — |
| C3 | in-FB | FB writes `IO.FaultActive` (instance member `MotorVSDInst1.IO.FaultActive`) — N1 (fault + fail-to-run gate) | verified-cross-block |
| C4 | render-BLOCKED `[contested]` | **Q-C20** — fails OPEN | — |
| C5 | render-BLOCKED `[network-stop]` | `PreStartDone := PlantControl.PreStartComplete` | — |
| C6 | in-FB | FB writes `IO.SpeedOutput` (instance member `MotorVSDInst1.IO.SpeedOutput`) — speed scaling, N6 | verified-cross-block |
| C7 | render-BLOCKED `[contested]` | **Q-C16** — no reverse output, no reverse feedback | — |
| C8 | in-FB | FB writes `IO.UPSEnable` (instance member `MotorVSDInst1.IO.UPSEnable`) — enable timer, N13 | verified-cross-block |
| C9 | in-FB | FB writes `IO.StopMotor` (instance member `MotorVSDInst1.IO.StopMotor`) — shutdown, N8 | verified-cross-block |
| C10 | in-FB | FB writes `IO.ShutdownComplete` (instance member `MotorVSDInst1.IO.ShutdownComplete`) — fault escape on shutdown-complete, N8 | verified-cross-block |
| C11 | **rebind** | **BA-1** — signal `AirStarDCRotSen` correct; interface target must be `IO.RunningFwdFB`, **not** `IO.RotationSensor` (DEAD-INTERFACE). Also blocked from rendering by the network stop | — |
| C12 | in-FB | FB writes `IO.FTR` (instance member `MotorVSDInst1.IO.FTR`) — start-up allowance + re-arm on direction change, N9 | verified-cross-block |
| C13 | in-FB | FB writes `IO.FTR` (instance member `MotorVSDInst1.IO.FTR`) — fail-to-run, N10 | verified-cross-block |
| C14 | in-FB | FB writes `IO.FTS` (instance member `MotorVSDInst1.IO.FTS`) — fail-to-stop, N11 | verified-cross-block |
| C15 | in-FB | FB writes `IO.FaultActive` (instance member `MotorVSDInst1.IO.FaultActive`) — drive error → latched fault, N12 (input interface-carried) | verified-cross-block |
| C16 | render-BLOCKED `[network-stop]` | `FaultReset := HMIControlSignals.SystemReset`; the unclaimed `VSDFaulrReset` output is Q-C21 | — |
| C17 | out-of-scope-obligation | owed to the HMI faceplate; drive condition members are interface-only, never consumed in the FB | — |
| C18 | **rebind** | same finding as C11, stated as the instance-delta relation | — |
| C19 | in-FB | FB writes `IO.HrsRun` (instance member `MotorVSDInst1.IO.HrsRun`) — hours totaliser, N14 | verified-cross-block |
| C20 | in-FB | FB writes `IO.Telemetry` (instance member `MotorVSDInst1.IO.Telemetry`) — telemetry, N15 | verified-cross-block |
| C21 | out-of-scope-obligation | alarms artifact (N16) | — |
| C22 | render-BLOCKED `[contested]` | **Q-C08** — no discrete start output and no documented drive-interface path | — |
| P1 | render-BLOCKED `[network-stop]` | `MotorStarterInst8.IO.UPSEnable` | — |
| P2 | render-BLOCKED `[network-stop]` | `ECSControlInst1.IO.UPSEnable` | — |
| P3 | render-BLOCKED `[contested]` | shutdown hold on `AirStarInst1.IO.ShutdownComplete`; S1-c **unverifiable** | — |
| P4 | out-of-scope-obligation | owed: `AirStarInst1`'s two start paths must both read this machine's `UPSEnable` | — |
| P5 | out-of-scope-obligation | owed: the Overband Magnet's network holds on this machine's `ShutdownComplete` | — |
| P6 | out-of-scope-obligation | owed: the ECS network holds on this machine's `ShutdownComplete` | — |
| P7 | render-BLOCKED `[contested]` | run-state term; S1-c **unverifiable** | — |
| P8 | render-BLOCKED `[network-stop]` | `NOT MotorVSDInst1.IO.HandIntervention` | — |
| P9 | render-BLOCKED `[contested]` | **Q-C11** — whether `PlantControl.GeneralEnable` gates this machine is disputed between rung A and rung B | — |

## Ledger — `MotorVSDInst3` (Sorter Conveyor VSD)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `IO.Run` (instance member `MotorVSDInst3.IO.Run`) — N1 | verified-cross-block |
| C2 | render-BLOCKED `[network-stop]` | `InhibitMotor := DiscreteInputs.OSCIsoFB` | — |
| C3 | in-FB | FB writes `IO.FaultActive` (instance member `MotorVSDInst3.IO.FaultActive`) — N1 | verified-cross-block |
| C4 | render-BLOCKED `[contested]` | **Q-C20** — fails OPEN | — |
| C5 | render-BLOCKED `[network-stop]` | `PreStartDone := PlantControl.PreStartComplete` | — |
| C6 | in-FB | FB writes `IO.SpeedOutput` (instance member `MotorVSDInst3.IO.SpeedOutput`) — N6 | verified-cross-block |
| C7 | render-BLOCKED `[contested]` | **Q-C16** | — |
| C8 | in-FB | FB writes `IO.UPSEnable` (instance member `MotorVSDInst3.IO.UPSEnable`) — N13 — enable consumed by three machines | verified-cross-block |
| C9 | in-FB | FB writes `IO.StopMotor` (instance member `MotorVSDInst3.IO.StopMotor`) — N8 | verified-cross-block |
| C10 | in-FB | FB writes `IO.ShutdownComplete` (instance member `MotorVSDInst3.IO.ShutdownComplete`) — N8 | verified-cross-block |
| C11 | render-BLOCKED `[contested]` | **Q-C06** — no bindable running-confirmation source exists | — |
| C12 | in-FB | FB writes `IO.FTR` (instance member `MotorVSDInst3.IO.FTR`) — N9 | verified-cross-block |
| C13 | in-FB | FB writes `IO.FTR` (instance member `MotorVSDInst3.IO.FTR`) — N10 | verified-cross-block |
| C14 | in-FB | FB writes `IO.FTS` (instance member `MotorVSDInst3.IO.FTS`) — N11 | verified-cross-block |
| C15 | in-FB | FB writes `IO.FaultActive` (instance member `MotorVSDInst3.IO.FaultActive`) — N12 | verified-cross-block |
| C16 | render-BLOCKED `[network-stop]` | `FaultReset := HMIControlSignals.SystemReset`; Q-C21 on the unclaimed output | — |
| C17 | out-of-scope-obligation | HMI faceplate | — |
| C18 | render-BLOCKED `[contested]` | **Q-C07** + **BA-2** — sensor exists, DEAD-INTERFACE, no bypass exists | — |
| C19 | in-FB | FB writes `IO.HrsRun` (instance member `MotorVSDInst3.IO.HrsRun`) — N14 | verified-cross-block |
| C20 | in-FB | FB writes `IO.Telemetry` (instance member `MotorVSDInst3.IO.Telemetry`) — N15 | verified-cross-block |
| C21 | out-of-scope-obligation | alarms artifact (N16) | — |
| C22 | render-BLOCKED `[network-stop]` | commanded through the drive interface; no discrete output to render | — |
| P1 | render-BLOCKED `[contested]` | **Q-C12** + **BA-3** (`HardWireSignals.Ready` dead; `Outputs.UPSEnable` recommended) | — |
| P2 | render-BLOCKED `[contested]` | **Q-C12** + **BA-4** (`Outputs.FaultActive` recommended as the broader guard) | — |
| P3 | render-BLOCKED `[network-stop]` | `MotorStarterInst6.IO.UPSEnable` | — |
| P4 | render-BLOCKED `[network-stop]` | `MotorStarterInst7.IO.UPSEnable` | — |
| P5 | render-BLOCKED `[contested]` | hold on `ECSControlInst1.IO.ShutdownComplete`; S1-c **unverifiable** | — |
| P6 | out-of-scope-obligation | owed: the sorter's own network holds on this machine's `ShutdownComplete` (that network is in scope but stopped — see `TomraControlInst1` P3) | — |
| P7 | out-of-scope-obligation | owed: `FilterUnitInst2` P1 consumes this enable (in scope, also stopped) | — |
| P8 | out-of-scope-obligation | owed: `FilterUnitInst3` P1 consumes this enable | — |
| P9 | out-of-scope-obligation | owed: the ECS network consumes this enable (out of scope) | — |
| P10 | render-BLOCKED `[contested]` | run-state term; S1-c **unverifiable** | — |
| P11 | render-BLOCKED `[contested]` | **Q-C10** — whose hand-intervention state gates the shutdown suppression | — |
| P12 | out-of-scope-obligation | owed: nothing to render — records that the reciprocal hold is absent by design or by defect (rung A asymmetry delta) | — |

## Ledger — `TomraControlInst1` (Optical Sorter)

| relation | disposition | evidence | precondition class |
|---|---|---|---|
| C1 | in-FB | FB writes `HardWireSignals.Run` (instance member `TomraControlInst1.HardWireSignals.Run`) — N6/N7 (note class anomaly A-2 on the hand path — recorded, not specified) | verified-cross-block |
| C2 | render-BLOCKED `[contested]` | **Q-C03** — no isolation signal; `Inputs.InhibitMotor` default FALSE **fails OPEN** | — |
| C3 | in-FB | FB writes `Outputs.FaultActive` (instance member `TomraControlInst1.Outputs.FaultActive`) — N6 | verified-cross-block |
| C4 | render-BLOCKED `[network-stop]` | `PreStartDone := PlantControl.PreStartComplete` | — |
| C5 | render-BLOCKED `[contested]` | **Q-C14** — the link half has no tags; `DiscreteOutputs.TomraRun := HardWireSignals.Run` is determined | — |
| C6 | render-BLOCKED `[contested]` | **BA-3** — `HardWireSignals.Ready` is DEAD-INTERFACE; the hardwired ready is read by nothing | — |
| C7 | render-BLOCKED `[contested]` | **Q-C14** — the liveness bit lives in untagged words | — |
| C8 | in-FB | FB writes `Outputs.OSRunning` (instance member `TomraControlInst1.Outputs.OSRunning`) — N2/N4 running and comms-fault fallback | verified-cross-block |
| C9 | in-FB | FB writes `Outputs.UPSEnable` (instance member `TomraControlInst1.Outputs.UPSEnable`) — N16 enable, gated on the sorter's ready status bit | verified-cross-block |
| C10 | in-FB | FB writes `Outputs.ShutdownComplete` (instance member `TomraControlInst1.Outputs.ShutdownComplete`) — N12 | verified-cross-block |
| C11 | in-FB | FB writes `Outputs.FTR` (instance member `TomraControlInst1.Outputs.FTR`) — N13 | verified-cross-block |
| C12 | in-FB | FB writes `Outputs.FTS` (instance member `TomraControlInst1.Outputs.FTS`) — N14 | verified-cross-block |
| C13 | render-BLOCKED `[contested]` | **Q-C14** — the machine's own fault bits arrive in untagged words | — |
| C14 | render-BLOCKED `[contested]` | **Q-C14** — `FaultReset := HMIControlSignals.SystemReset` is determined; the echo over the link is not | — |
| C15 | in-FB | FB writes `Outputs.HrsRun` (instance member `TomraControlInst1.Outputs.HrsRun`) — N17 | verified-cross-block |
| C16 | in-FB | FB writes `Outputs.Telemetry` (instance member `TomraControlInst1.Outputs.Telemetry`) — N18 | verified-cross-block |
| C17 | out-of-scope-obligation | alarms artifact (N19) | — |
| C18 | render-BLOCKED `[contested]` | **Q-C19** + **BA-5** — `ProgramSelection` / `Belt1OnDelay` DEAD-INTERFACE; an unimplementable interface promise | — |
| C19 | render-BLOCKED `[contested]` | **Q-C13** — a production selector on `PlantControl.Test[5]`, a test/simulation array | — |
| P1 | render-BLOCKED `[network-stop]` | `MotorStarterInst6.IO.UPSEnable` | — |
| P2 | render-BLOCKED `[network-stop]` | `MotorStarterInst7.IO.UPSEnable` | — |
| P3 | render-BLOCKED `[contested]` | hold on `MotorVSDInst3.Outputs/IO.ShutdownComplete`; S1-c **unverifiable** | — |
| P4 | out-of-scope-obligation | owed: the Ejected Material Conveyor's network holds on this machine's `ShutdownComplete` | — |
| P5 | out-of-scope-obligation | owed: the Residual Material Conveyor's network, same | — |
| P6 | out-of-scope-obligation | owed: `MotorVSDInst3` P1 consumes this machine's ready state (in scope, stopped; **Q-C12**) | — |
| P7 | out-of-scope-obligation | owed: `MotorVSDInst3` P2 consumes this machine's fault state (**Q-C12**) | — |
| P8 | render-BLOCKED `[contested]` | run-state term; S1-c **unverifiable** | — |
| P9 | render-BLOCKED `[contested]` | **Q-C10** | — |
| P10 | out-of-scope-obligation | owed: nothing to render — records that a fault here does not release P4/P5 (Q-A5) | — |

## Ledger disposition counts

| Disposition | Count |
|---|---|
| `rendered` | **0** |
| `in-FB` | **60** |
| `discharged` | **0** |
| `rebind` | **2** |
| `render-BLOCKED` — `[contested]` | **47** |
| `render-BLOCKED` — `[network-stop]` | **33** |
| `out-of-scope-obligation` | **26** |
| **Total** | **168** |

`render-BLOCKED` total: **80**. Relations with **no** blocking obstruction of any kind
(`in-FB` + `out-of-scope-obligation` + `rebind`): **88**.

**Counts and set-difference are machine-checked, not tallied by eye.** The relation-id column was
extracted from this file and from `equipment-specs/*.md` and compared: 168 = 168, difference empty in
both directions. The disposition counts above come from the same extraction.

---

# D3 — Render

## **STOPPED.** No render was produced, for any of the six instances.

This rung's stop condition: *a spec carrying an unresolved BLOCKING `Q-nn` that affects logic you
would render → stop and report; never render a guess for a contested requirement.* **All six specs
carry such questions.** Per instance, the questions that block its network:

| Instance | Blocking questions that contest terms in its network |
|---|---|
| `FilterUnitInst2` | Q-C01 (running / remote-operational), Q-C03 (isolation), Q-C04 (fan shutdown), Q-C09 (shutdown-hold combination), Q-C20 (health) |
| `FilterUnitInst3` | Q-C02, Q-C03, Q-C04, Q-C09, Q-C20 |
| `FilterUnitInst1` | Q-C03, Q-C04, Q-C05 (running signal sense), Q-C09, Q-C20 |
| `MotorVSDInst1` | Q-C08 (command path), Q-C11 (general enable), Q-C16 (reverse), Q-C20, Q-C21 |
| `MotorVSDInst3` | Q-C06 (running confirmation), Q-C07 (motion sensor), Q-C10 (hand flag), Q-C12 (neighbour permissive), Q-C16, Q-C20 |
| `TomraControlInst1` | Q-C03, Q-C10, Q-C12, Q-C13 (test-array selector), Q-C14 (untagged data words), Q-C19 |

**Why no partial render, given that 33 terms are fully determined.** A `chained-permissive-enable`
network is rendered as a unit — one network per instance, per S1 and C-109/C-110. A network
containing the determined terms and silently missing the contested ones would read, to any reviewer
and to `gen-block-new` downstream, as a **complete** network. The undriven audit above makes the
consequence concrete rather than theoretical: omitting the `InhibitMotor` term leaves the isolation
permissive **open** at its interface default, and omitting the `SystemHealthy` term leaves the health
permissive **open** — neither absence is visible in the ladder. Emitting that would be the exact
failure this rung's stop condition exists to prevent, dressed as progress.

The determined terms are **not** lost: all 33 are recorded in the D2 ledger's evidence column, with
the term written out. When the blocking questions are answered, D3 resumes from the ledger rather
than from scratch.

**No `.ir` was written. No tag, address or DB number was invented. No blocking question was resolved
to keep moving.**

---

# Open questions carried into the gate

All questions below are carried unresolved from rungs A, B and C, plus the two `rebind`
recommendations this rung produced. Nothing new is blocking that was not already blocking, with one
exception: **BA-3 promotes `HardWireSignals.Ready` from a candidate to a dead member**, which the
engineer answering Q-C12 needs.

- **Blocking, rung C:** Q-C01, Q-C02, Q-C03, Q-C04, Q-C05, Q-C06, Q-C07, Q-C08, Q-C09, Q-C10,
  Q-C11, Q-C12, Q-C13, Q-C14, Q-C20, Q-C21, Q-C22, Q-C23, Q-C24, Q-C25, Q-C26.
- **Blocking, rung B:** Q-B1 (circular behaviour source).
- **Blocking, rung A:** Q-A2 (class references are not engineering standards), Q-A3 (single-instance
  class).
- **Non-blocking, carried:** Q-A4, Q-A5, Q-A6, Q-A7, Q-C15, Q-C16*, Q-C17, Q-C18, Q-C19.
  (*Q-C16 is recorded blocking on both VSD specs' C7; listed here only because rung C's own summary
  placed it in both lists — treat it as **blocking**.)
- **From this rung:** BA-1 (rebind, `MotorVSDInst1` C11/C18 → `IO.RunningFwdFB`); BA-3/BA-4
  (recommendations for whoever answers Q-C12, deliberately not applied).

---

# Gate 1 — engineer sign-off before the Build coding stage

**STATE: NOT SIGNABLE.**

`docs/11-review-workflow.md`'s gate 1 requires a signed architecture before `gen-block-new` may
begin. This structure cannot be signed, because:

1. **D3 produced no render.** There is nothing for the coder to build.
2. **21 blocking rung-C questions plus 3 blocking A/B questions are open**, of which 5 land on
   safety-adjacent process protection (isolation permissives with no signal; health permissives
   satisfied by a hardcoded truth; a protective permissive whose fault source could be any of three
   signals of different widths).
3. **Two of the six machines have a requirement with no bindable signal at all** — `MotorVSDInst3`'s
   running confirmation (Q-C06) and `TomraControlInst1`'s data-link words (Q-C14, PROPOSED tags).

```
Gate 1 — architecture sign-off
  Engineer:        ____________________     Date: __________
  Status:          NOT SIGNABLE — D3 stopped, 24 blocking questions open
  Blocking set:    Q-A2, Q-A3, Q-B1, Q-C01…Q-C14, Q-C16, Q-C20…Q-C26
  Next step:       answer the blocking set (or record explicit acceptances), then re-run D3 from
                   the D2 ledger — the 88 non-blocked relations and 33 determined terms are already
                   recorded and do not need re-deriving.
```
