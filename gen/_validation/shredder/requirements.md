# Requirements — `ShredderControlSystem` (comms-driven shredder equipment-control FB)

**Artifact type:** behavioral requirements register (`gen-spec-analysis` contract, docs/15 skill #1).
**Produced by:** `lad-coder`, MANUAL run to the `gen-spec-analysis` contract (skill not yet built — docs/15 build-order step 7). This is a `manual:gen-spec-analysis` run.
**Date:** 2026-07-18
**Derived from:** the behavioral function of one variable-program industrial **shredder** drive controlled over a serial/Modbus comms link (inbound status bytes/words + outbound command bytes), plus its plant-level and raw-I/O interface (dependency context in `../deps/`: `PlantControl` DB, `PlantIOTags` tag table). Written to state *what* the block must do at the requirements level — sufficient to build from without prescribing rung structure.
**Scope:** one Function Block controlling one comms-connected shredder unit. Non-safety (the shredder's own E-stop is handled elsewhere; this block only reads an E-stop **feedback** word and republishes it — REQ-006). LAD only.

## Why this block (genval2 target rationale — harder than the genval1 fixture)
Unlike the genval1 fixture (`FilterUnitSystem`, which was ~whole-block `motor-dol` pattern reuse),
this block is **not mostly covered by the pattern library**. A reuse-first `gen-architecture` carve
would find:
- **`patterns/input-mapping`** composes N-of-the-comms-in networks (raw tags → internal inbound image), **`patterns/output-mapping`** composes the comms-out networks (internal outbound image → raw tags): ~7 of 29 networks, tier-(b) compose.
- **`patterns/motor-dol` does NOT apply as tier-(a)/(b) reuse.** This FB does **not** `CALL` a `MotorDOL` FB; it re-implements DOL-style start/permissive/prestart/fail-timer/shutdown/hours/telemetry logic **inline and comms-adapted** — the run command is an outbound comms bit (`StartAuto`/`StopAuto`), and "running confirmed" is **decoded from inbound comms bits**, not a wired `RunningFB`. `motor-dol` is at most a *structural reference* for those networks, each modified and interwoven with comms/watchdog logic — freeform/compose, not whole-block reuse.
- **No pattern covers**: the serial life-bit **watchdog** (heartbeat out + loss-of-life detection), the **program/recipe selection** encode, the **word telemetry** (Modbus word → engineering Int), the **13-bit alarm word** pack, and the E-stop-feedback republish.
- **Expected split:** ~24% pattern-compose (input/output-mapping), ~76% freeform. Contrast genval1 (~93% tier-(a) whole-block reuse). This is the freeform-heavy, multi-pattern-composed case docs/15 most cares about.

## Open questions
- **OQ-1:** The HMI-facing command bits (`Inputs.HandIntervention`, `Inputs.HandStartSignal`, `Inputs.RecentStart`) are momentary bits SET by the HMI on button press/release and RESET by this block (REQ-021..025). Who owns the HMI screens is out of scope for this block.
- **OQ-2:** Absolute engineering ranges/defaults for the operator time setpoints (`Inputs.FTTime`, `ShutdownTime`, `EnableUPSTime`, seconds, Real) are not specified by the source — only role and units. Treat defaults as `proposed`.
- **OQ-3 (source anomaly, not a requirement):** In the source block, program-selection bits **4/5/6** are written into the *inbound* comms image (`ComsInByte501.ProgramP4/5/6`) rather than the *outbound* program byte (`ComsOutByte501.Program4/5/6`) that bits 1/2/3 use, while N28 nonetheless writes all six `ComsOutByte501.Program1..6` out to raw tags. This reads as a copy-paste defect in the source. **REQ-009 states the intended behavior** (all six selections drive the six outbound program bits); a faithful regeneration should implement the intent, and any divergence from the source here is a source defect, not a regeneration error.
- **OQ-4 (source anomaly, not a requirement):** The time-conversion network (REQ-016) computes all three ×1000 products through a single shared `TEMP` real before converting to three separate MS presets. The **requirement** is three independent conversions (each setpoint → its own MS preset); the shared temporary is an implementation quirk of the source, not a behavioral requirement, and the generator should give each conversion its own dataflow.
- **OQ-5:** `PlantControl.Test[7]` (a bit of a plant-wide `Test : Array[0..10] of Bool`) is folded into the fault summary (REQ-031). Its plant-level meaning (a maintenance/test fault-inject bit) is external to this block; the block only reads it. Provided in `../deps/PlantControl.ir`.

## Interface contract (requirements level)

This FB carries **no formal INPUT/OUTPUT/IN-OUT parameters** — its entire signal interface is **per-instance STATIC data** (nested `Struct` members), read/written by the plant orchestrator, the HMI, and the comms driver. The generator declares this interface itself (there is **no external UDT** for it — contrast genval1's `MotorIOSet`). The names below are the required member vocabulary; the generator MUST provide equivalent members (it may organize the structs as here).

**`Inputs : Struct` (RETAIN) — HMI/orchestrator command & setpoints:**
`InHand`, `AutoStartSignal`, `HandStartSignal`, `HandIntervention`, `RecentStart`, `PreStartDone`, `InhibitMotor`, `FaultReset`, `Shutdown` (all Bool); `ProgramSelection : Int` (recipe 1..6); `FTTime`, `EnableUPSTime`, `ShutdownTime : Real` (seconds).

**`Outputs : Struct` — status/telemetry to HMI/orchestrator:**
`TryRun`, `Run`, `Stop`, `ShredderRunning`, `UPSEnable`, `ShutdownComplete`, `FaultActive`, `FTR`, `FTS` (Bool); `HrsRun : UDInt`; `Telemetry : Int`; `Alarm : Word`; `Name : String`.

**Inbound comms image (decoded from the shredder over the link) — internal STATIC:**
- `ComsInByte500`: `Ready`, `RotorOn`, `RotorStandby`, `ReleaseFeeding`, `ProgramP1`, `ProgramP2`.
- `ComsInByte501`: `ProgramP3..P6`, `FlashBit`, `LifeBitMirror`, `LocalMode`, `MaintenanceOn`.
- `ComsInByte502`: `BypassActive`, `CuttingUnitRemote`, `TouchPanelShredder`, `TouchPanelMainCabnet`, `RadioModeOn`, `Drive1Ready`, `Drive2Ready`.
- `ComsInByte503`: `OilCoolerRunning`, `AutoModeOn`, `ServiceModeOn`, `Selector1Local`, `Selector1Remote`.
- `ComsInWord508`, `ComsInWord528`, `ComsInWord532 : Word` (raw analog words) → converted to `ActualSpeed`, `MotorAmps1`, `MotorAmps2 : Int`.

**Outbound comms image (command to the shredder over the link) — internal STATIC:**
- `ComsOutByte500`: `LifeBit`, `ResetOverload`, `StopAuto`, `HoldAuto`, `StartAuto`.
- `ComsOutByte501`: `Program1..Program6`.
- `ComsOutByte502`: `Release4Op`, `StartHorn`, `RadioControlPermit`.

**Internal working storage (generator-chosen; named here only where a REQ needs it):** the three MS presets (`ShutdownTimeMS`/`EnableUPSTimeMS`/`FTTimeMS : DInt`); the fail/shutdown/upstream/prestart/debounce/life timers; a retentive hours timer + its reset; edge-flag arrays; `PreStartMemory`, `LostLifeBit`, `HandPosEdge`, `HandNegEdge`.

**External references (grounded in `../deps/`, all `exists`):**
- `PlantIOTags` (tag table): `Tag_1..Tag_26` (inbound status bits, `%I500.x`..`%I503.x`), `Tag_41..Tag_44` (inbound words, `%IW508/528/532/650`), `Tag_27..Tag_40` (outbound command bits, `%Q500.x`..`%Q502.x`), and `Clock_0.5Hz` (`%M0.7`, 0.5 Hz system clock bit).
- `PlantControl` (DB): `ShredderEStopFB : Word` (block **writes** it — REQ-006), `Test : Array[0..10] of Bool` (block **reads** `[7]` — REQ-031).

## Equipment inventory
| Item | Count | Role |
|---|---|---|
| Comms-connected shredder drive | 1 | The controlled equipment. Started/stopped via outbound comms command bits (`StartAuto`/`StopAuto`); running state **decoded** from inbound comms status bits (REQ-008). |
| Downstream/upstream feeding equipment (consumer of `Outputs.UPSEnable`) | 1 (external) | Released once the shredder is confirmed running and feeding (REQ-030). |
| Serial/Modbus link to the shredder controller | 1 | Carries the inbound status image (bytes 500-503, words 508/528/532) and outbound command image (bytes 500-502), plus a life-bit heartbeat both ways (REQ-007). |

## Control paradigm (C-113 memory test)
**Memory test = NO explicit phase memory.** This is continuous, permissive-based single-equipment
control layered over a comms interface — not a phased/stepped sequence. The state that persists is
limited to conventional bistable holds: a run seal-in (`StartAuto`), latched faults
(`FTR`/`FTS`/`FaultActive`/`LostLifeBit`), a prestart-complete memory, and a hours totaliser. There
is **no step register**. Paradigm: **continuous comms-driven equipment control** (C-114 family),
**not** an explicit stepped sequence (C-113).

---

## Requirements

### Comms image — inbound decode
- **REQ-001** The block maintains an internal **inbound comms image** of the shredder's status bytes 500-503, copying each raw input tag bit to the correspondingly-named image bit every scan (raw `Tag_1..Tag_26` → the `ComsInByte500..503` members named in the interface contract). This is the input-mapping stage; downstream logic reads only the image, never the raw tags.
- **REQ-002** Inbound status **words** 508/528/532 (raw `Tag_41`/`Tag_42`/`Tag_43`, Word) are copied into the image and converted (Word→Int) to `ActualSpeed`, `MotorAmps1`, and `MotorAmps2` respectively — HMI telemetry values.

### Comms image — outbound encode
- **REQ-003** The block maintains an internal **outbound comms image** (`ComsOutByte500..502`) and writes each of its bits to the corresponding raw output tag every scan (`ComsOutByte500..502` members → raw `Tag_27..Tag_40`). This is the output-mapping stage; only this stage touches the raw output tags.
- **REQ-004** `Outputs.Run` mirrors the outbound run command `ComsOutByte500.StartAuto` (the single "is the drive commanded to run" status for the orchestrator/HMI).

### E-stop feedback republish
- **REQ-005** Reserved — see REQ-006 (kept distinct so numbering stays stable).
- **REQ-006** The block copies the raw inbound E-stop-feedback word (`Tag_44`, `%IW650`) into `PlantControl.ShredderEStopFB` (Word) each scan, republishing the shredder's E-stop feedback for plant-level consumers. This is a straight word copy; the block performs no safety evaluation on it.

### Overload reset
- **REQ-007a** The outbound overload-reset command bit (`ComsOutByte500.ResetOverload`) follows the operator fault-reset input (`Inputs.FaultReset`) — pressing reset commands the shredder to clear its overload.

### Life-bit watchdog (serial heartbeat)
- **REQ-007** The block drives its outbound life bit (`ComsOutByte500.LifeBit`) from the 0.5 Hz system clock (`Clock_0.5Hz`) — a toggling heartbeat sent to the shredder.
- **REQ-008a** The block monitors the inbound life-bit mirror (`ComsInByte501.LifeBitMirror`). A **loss-of-life** condition is declared when the mirror fails to reflect the heartbeat: a watchdog timer (preset **5 s**) runs while the inbound mirror and the outbound life bit are both low, and its expiry **latches** `LostLifeBit`. `LostLifeBit` holds until `Inputs.FaultReset`. `LostLifeBit` is a fault contributor (REQ-031).

### Running confirmation (decoded, not wired)
- **REQ-008** "Shredder running confirmed" (`Outputs.ShredderRunning`) is **decoded from the inbound comms image**: TRUE when the drive reports ready-and-rotor-on (`ComsInByte500.Ready AND ComsInByte500.RotorOn`) **or** it is releasing feed (`ComsInByte500.ReleaseFeeding`). This decoded signal is used everywhere downstream in place of a wired running-feedback contact.

### Program / recipe selection
- **REQ-009** The operator recipe selector `Inputs.ProgramSelection` (Int 1..6) is encoded to six mutually-exclusive outbound program bits: `ComsOutByte501.Program1` = (`ProgramSelection` = 1), …, `ComsOutByte501.Program6` = (`ProgramSelection` = 6). Exactly the selected program's bit is set; all are written out per REQ-003. (See OQ-3 for a source anomaly on bits 4-6; implement the intent stated here.)

### Modes & run request
- **REQ-010** The equipment operates in **Auto** (`Inputs.InHand`=FALSE) or **Hand** (`Inputs.InHand`=TRUE). In Auto the run request follows `Inputs.AutoStartSignal`. In Hand the run request follows `Inputs.HandStartSignal` **only when `Inputs.HandIntervention` is active**; in Hand without intervention active, the request continues to follow `Inputs.AutoStartSignal`.
- **REQ-011** `Outputs.TryRun` asserts when the selected-source run request (REQ-010) holds **and** all run permissives hold: not inhibited (`NOT Inputs.InhibitMotor`), no fail-to-run latched (`NOT Outputs.FTR`), and no active fault (`NOT Outputs.FaultActive`). `TryRun` is the "request to run", distinct from the confirmed run command.

### Start / stop command to the drive (run seal-in)
- **REQ-012** The outbound run command `ComsOutByte500.StartAuto` engages when a valid `TryRun` coincides with prestart-complete (`PreStartMemory`, REQ-019) and a recent-start acknowledgement (`Inputs.RecentStart`, REQ-018), then **seals in** (holds on its own contact while `TryRun` persists). It drops immediately on `Outputs.Stop` or `Inputs.Shutdown`.
- **REQ-013** The outbound stop command `ComsOutByte500.StopAuto` is the exact logical inverse of `StartAuto` (mutually exclusive — the drive is commanded either to run or to stop each scan).

### Hand-intervention handling
- **REQ-014** `Inputs.HandIntervention` and `Inputs.HandStartSignal` are momentary bits SET by the HMI on button press/release; this block RESETS them under the conditions below (self-clearing from the logic side).
- **REQ-015** `Inputs.HandIntervention` is reset whenever Hand mode is not selected (`NOT Inputs.InHand`).
- **REQ-016b** `Inputs.HandStartSignal` is reset whenever any holds: not in Hand mode, hand-intervention not active, a fault is active, or the motor is inhibited.
- **REQ-017** After a hand start is accepted, `Inputs.HandStartSignal` is additionally reset following a fixed **100 ms** debounce (measured from the accepted-start condition), so a single press yields a single start.

### Recent-start acknowledgement
- **REQ-018** `Inputs.RecentStart` is a momentary start-acknowledge bit SET by the HMI on an Auto/Hand start; the block **holds** it only while the drive is not yet commanded running (`NOT ComsOutByte500.StartAuto`), prestart is complete (`PreStartMemory`), and no fault is active; it clears otherwise. It gates the initial engagement of `StartAuto` (REQ-012).

### Prestart hold
- **REQ-019** The block maintains a prestart-complete memory (`PreStartMemory`): it becomes/stays TRUE when `Inputs.PreStartDone` is present, and otherwise **holds** until any of — the drive is commanded running (`ComsOutByte500.StartAuto`), an in-Hand prestart-request timeout of **30 s** elapses, or a Hand-mode transition (either edge of `Inputs.InHand`) occurs. The generator provides its own edge detection (rising/falling on `InHand`) and the 30 s timer.

### Time setpoint conversion
- **REQ-016** The three operator time setpoints (`Inputs.ShutdownTime`, `EnableUPSTime`, `FTTime`), entered in **seconds as Real**, are each converted to a **millisecond DInt** timer preset (×1000, Real→DInt): `ShutdownTimeMS`, `EnableUPSTimeMS`, `FTTimeMS`. These feed the shutdown, upstream-enable, and fail timers (REQ-027, REQ-030, REQ-028/029). (Three independent conversions — see OQ-4.)

### Shutdown
- **REQ-025** A shutdown is requested by `Inputs.Shutdown`. On the **rising edge** of `Inputs.Shutdown` the block SETs `Outputs.Stop` (latched stop).
- **REQ-026** `Outputs.Stop`, once set, is RESET when shutdown is complete (`Outputs.ShutdownComplete`) and the shutdown request has been released (`NOT Inputs.Shutdown`).
- **REQ-027** `Outputs.ShutdownComplete` asserts when any of: the shutdown timer (preset `ShutdownTimeMS`) has elapsed while the drive is no longer confirmed running (`NOT Outputs.ShredderRunning`); it was already complete and the drive is not commanded running (`NOT ComsOutByte500.StartAuto`); a Hand-mode stop condition applies (in Hand with intervention and no hand-start); or a fault is active. The shutdown timer runs while `Outputs.Stop` is asserted.

### Fault detection & handling
- **REQ-028** **Fail-to-run (`Outputs.FTR`):** if the drive is commanded running (`ComsOutByte500.StartAuto`) but running confirmation (`Outputs.ShredderRunning`) is not decoded within the fail time (preset `FTTimeMS`), `FTR` latches until `Inputs.FaultReset`.
- **REQ-029** **Fail-to-stop (`Outputs.FTS`):** if the drive is not commanded running (`NOT ComsOutByte500.StartAuto`) but running confirmation persists beyond the same fail time, `FTS` latches until `Inputs.FaultReset`.
- **REQ-031** **Fault summary (`Outputs.FaultActive`):** the latched OR of `Outputs.FTR`, `Outputs.FTS`, `LostLifeBit` (watchdog, REQ-008a), and the external plant test/fault-inject bit `PlantControl.Test[7]`; cleared only by `Inputs.FaultReset`. `FaultActive` is the signal that blocks running (REQ-011) and forces the faulted telemetry/alarm states.

### Upstream enable
- **REQ-030** `Outputs.UPSEnable` (release to the feeding equipment) asserts after the shredder is **confirmed running and feeding** — commanded running (`ComsOutByte500.StartAuto`) AND running confirmed (`Outputs.ShredderRunning`) AND releasing feed (`ComsInByte500.ReleaseFeeding`), not blocked by a Hand-intervention condition — has been stable for the upstream-enable delay (preset `EnableUPSTimeMS`); it then follows that delayed-on condition while running remains confirmed.

### Running-hours totaliser
- **REQ-032** The block accumulates shredder **running hours** into `Outputs.HrsRun` (UDInt), advancing only while the drive is commanded running (`ComsOutByte500.StartAuto`) AND running is confirmed (`Outputs.ShredderRunning`), via a retentive 1-hour timer: each completed running-hour increments `HrsRun` by 1.
- **REQ-033** The totaliser is roll-over safe: at the UDInt maximum (`4294967295`) the next completed hour resets `HrsRun` to 0 instead of incrementing.

### Telemetry (HMI status code)
- **REQ-034** `Outputs.Telemetry` (Int) is a priority-encoded equipment status code (highest applicable wins):
  - `-1` = faulted (`Outputs.FaultActive`)
  - `0` = healthy, not commanded running
  - `1` = healthy, run commanded (`ComsOutByte500.StartAuto`) but not yet confirmed
  - `2` = run commanded AND running confirmed (`Outputs.ShredderRunning`)
  - `3` = running confirmed AND upstream enabled (`Outputs.UPSEnable`), **or** running confirmed in Hand mode

### Alarms (bit-packed word)
- **REQ-035** `Outputs.Alarm` (Word) is bit-packed for HMI/alarm consumption with fixed assignments:
  - bit 0 = `Outputs.FTR`
  - bit 1 = `Outputs.FTS`
  - bit 2 = drive-not-ready (`NOT ComsInByte500.Ready`)
  - bit 3 = shredder in local mode (`ComsInByte501.LocalMode`)
  - bit 4 = maintenance mode on (`ComsInByte501.MaintenanceOn`)
  - bit 5 = bypass active (`ComsInByte502.BypassActive`)
  - bit 6 = radio mode on (`ComsInByte502.RadioModeOn`)
  - bit 7 = not in auto mode (`NOT ComsInByte503.AutoModeOn`)
  - bit 8 = service mode on (`ComsInByte503.ServiceModeOn`)
  - bit 9 = selector in local (`ComsInByte503.Selector1Local`)
  - bit 10 = selector not in remote (`NOT ComsInByte503.Selector1Remote`)
  - bit 11 = commanded running but not releasing feed (`ComsOutByte500.StartAuto AND NOT ComsInByte500.ReleaseFeeding`)
  - bit 12 = loss of life bit (`LostLifeBit`)

### Non-behavioral / structural expectations
- **REQ-036** Every network carries a title; comments state *why*, not *what* (site convention). The block header states the chosen control paradigm (continuous comms-driven equipment control; C-113 = no phase memory).
- **REQ-037** The block references only its own declared interface members plus the grounded external names in `../deps/` (`PlantIOTags` tags, `PlantControl` members) — no invented tags, DB numbers, or hardware addresses (hard rule 3).

---

## Tag-status ledger (anti-laundering, docs/15)

`converter tagstatus ... --project ../deps/` (run 2026-07-18): **9 external names checked, 0 proposed** — `Tag_1`, `Tag_26`, `Tag_27`, `Tag_40`, `Tag_41`, `Tag_44`, `Clock_0.5Hz`, `PlantControl.ShredderEStopFB`, `PlantControl.Test` all **EXISTS** against the deps context.

| Name(s) | Status | Grounding |
|---|---|---|
| `Tag_1..Tag_44` (raw inbound/outbound I/O) | **exists** | `../deps/PlantIOTags.ir` (real `%I`/`%Q`/`%IW` addresses, preserved from export; default/generic names). |
| `Clock_0.5Hz` | **exists** | `../deps/PlantIOTags.ir` (`%M0.7`, system clock memory bit). |
| `PlantControl.ShredderEStopFB`, `PlantControl.Test` | **exists** | `../deps/PlantControl.ir` (DB `PlantControl`). |
| `Inputs.*`, `Outputs.*`, `ComsIn*`, `ComsOut*`, `ActualSpeed`, `MotorAmps*`, timers, edges, memories | **exists (by declaration)** | Block-internal interface/STATIC the FB declares itself — not external tags. |

No name in this register refers to any un-grounded external DB, tag, or hardware address. Every
external reference resolves against the supplied `../deps/` context; all internal signals are
declared by the block. Nothing is coded against a `proposed` tag.
