# PlantAutoControl-bench-rerun2 — Plant-level residual signal sweep (rung C §8b)

Produced by `/gen-equipment-spec`, run once for this project, 2026-08-05.

**What this is:** the reverse pass. The binding pass asks *"what signal satisfies this
requirement?"*; this pass asks *"what requirement claims this signal?"* — over every member of every
global DB in `ir/PlantAutoControl-bench/`, whether or not it belongs to a machine in scope. Every signal
is **bound**, or **out-of-scope-instance**, or **UNCLAIMED with a reason or a blocking `Q-nn`**.

**Judged by role, never by name pattern.** `Bypass*` / `*Enable` / `*Override` prompted attention
but were not the test; several findings below (`HandFansShutdown`, `PartInHand`, `Test[…]`) match no
pattern and are blocking anyway, and several that match a pattern (`GeneralEnable`, `MagEnable`) are
merely out-of-scope.

**Scope caveat, stated up front:** only six of the plant's twenty machines are specified in this
run, so *most* signals here are legitimately unclaimed — they belong to the other fourteen. Those
are marked `out-of-scope-instance` and are **not** findings. The findings are the **ownerless**
ones: signals no machine in the plant would claim, or that a specified machine should have claimed
and did not.

**Verification note:** all membership checks were done by grep against the DB files.
`converter tagstatus` was run but **cannot** be used for this pass — it validates only the root DB
name and reports any invented member of a real DB as `EXISTS` (demonstrated:
`HMIControlSignals.TotallyInventedMember -> EXISTS`). See the run report.

---

## Findings — ownerless, control-implying, BLOCKING

These are the output of this pass. Each is a signal that implies a control function and that **no
requirement in any source, and no instance spec in this run, claims**.

### Q-C09 — `DiscreteInputs.OSCRotSen` (+ the bypass that is missing)

A belt motion sensor for the Sorter Conveyor VSD (`MotorVSDInst3`), a machine in scope. **No source
requirement mentions it.** Rung A recorded "no motion sensor on this machine" as a candidate
minus-delta (Q-A14) precisely because the source's list of motion-confirmed conveyors omits it — and
the sensor is physically there.

Worse, the operator bypass that `[B-37]` requires for **every** motion-confirmed conveyor does not
exist for this one: `HMIControlSignals.BypassOSCRotSen` is **absent** from the DB (grep: 0 hits)
while the other eight conveyors each have theirs (`BypassHFLCRotSen`, `BypassFMCCRotSen`,
`BypassOSECRotSen`, `BypassOSRCRotSen`, `BypassSFCRotSen`, `BypassSDCRotSen`, `BypassIFCRotSen`,
`BypassAirStarDCRotSen`). It is a **proposed** tag and is not invented here (hard rule 3).

Two readings, both live: the sensor is used and its bypass was forgotten; or the sensor is spare and
the specification is right. **BLOCKING.**

### Q-C10 — the Optical Sorter's entire data-link word interface is absent

The source names `Tag_45`…`Tag_54` (8 in / 2 out process-data words) as the sorter's comms buffer.
**None of those tags exists anywhere in `ir/PlantAutoControl-bench/`, and no tag table is exported at
all.** Six class requirements of a machine in scope (`TomraControlInst1` C5, C6, C7, C13, C14, C18)
depend on that link. All **proposed**. **BLOCKING.**

### Q-C15 — the global override feature set: eight flags and seven values, ownerless

| Flag (`HMIControlSignals`) | Value (`ProcessTimings`) | Overrides |
|---|---|---|
| `GlobalFTTimeOverwrite` | `GlobalFTTimes` = 0.0 | every machine's fail-to-run / fail-to-stop time |
| `GlobalUPSEnableTimeOverwrite` | `GlobalUPEnableTime` = 0.0 | every machine's up-to-speed enable time |
| `GlobalShutdownCompleteTimeOverwrite` | `GlobalShutdownCompleteTime` = 0.0 | every machine's shutdown time |
| `GlobalVSDStartUpTimeOverwrite` | `GlobalVSDStartUpTime` = 0.0 | both in-scope VSDs' ramp allowance |
| `GlobalVSDAutoSpeedOverwrite` | `GlobalVSDAutoSpeed` = 0.0 | both in-scope VSDs' automatic speed |
| `GlobalVSDHandSpeedOverwrite` | `GlobalVSDHandSpeed` = 0.0 | both in-scope VSDs' hand speed |
| `GlobalFwdRevDOLRevDelayTimeOverwrite` | `GlobalFwdRevDOLRevDelayTime` = 0.0 | out-of-scope (fwd/rev DOL) |
| `GlobalFwdRevDOLFTRIgnoreTimeOverwrite` | `GlobalFwdRevDOLFTRIgnoreTime` = 0.0 | out-of-scope (fwd/rev DOL) |

**No source mentions any of it.** Six of the eight reach machines in this run's scope and override
settings this rung has just recorded from the instance boundary. Every override value is `0.0`, so
if the flag is set the setting becomes zero — which for a fault time or an enable time is not a
benign default. **BLOCKING**, and it is also half of Q-C14 (the VSD speed question): the plant-wide
override is one of the two competing mechanisms for a setpoint no source states.

### Q-C16 — `HMIControlSignals.HandFansShutdown`

An operator command whose name places it squarely on the filter/fan units — three of the six
machines in scope. No requirement in any source claims it. It implies an operator-initiated fan
shutdown path parallel to the plant's controlled-shutdown cascade (the `FansShutdownReady` condition
of P2 on all three filter specs). **BLOCKING** — a second shutdown path for a machine class is not a
detail.

### Q-C17 — `DiscreteOutputs.VSDFaulrReset`

A physical VSD fault-reset output (name as spelled in the DB). Both in-scope VSDs are candidates and
no source mentions it. The specs bind `HMIControlSignals.SystemReset` as the reset for every
machine; this is a **second, physical** reset path for the VSD class, exactly parallel to the filter
units' `FilterUnit1/2Reset` echo which **is** specified (P8 on those specs). Its absence from the
sources is therefore an asymmetry, not an omission by design. **BLOCKING.**

### Q-C18 — test / simulation data wired into production control

- `PlantControl.Test : Array[0..10] of Bool` — the **only** plant-level candidate for the optical
  sorter's byte-order selection (C19, a real class requirement). A production selector reading from
  an array named `Test`.
- `DiscreteInputs.Test : Array[0..75] of Bool` and `DiscreteOutputs.Test : Array[0..26] of Bool` —
  test arrays overlaying the field IO image. The `optical-sorter` class reference records (its
  anomaly A-1) that a plant test/simulation array **overwrites** one of that machine's status bits,
  so this is not hypothetical for a machine in scope.
- `InterlockData.Simulation : Bool` — a simulation flag living in production interlock data.

**BLOCKING.** Whether these are commissioning aids that must be removed, or a deliberate simulation
facility that must be specified, changes what the plant does in production.

### Q-C25 — `HMIControlSignals.GlobalSetAllToAuto`

A plant-wide mode command that would place every machine — including all six in scope — into
automatic. No source mentions it, and the specs' C1 (auto/hand selection) has no signal at all
(Q-C04), so this is a candidate for the one mode mechanism the specs could not bind. **BLOCKING.**

### Q-C24 — the anti-condensation feature set

`HMIControlSignals.AntiConEnabled`, `RunAntiConNxtStart`, `SkipAntiConNxtStart`;
`PlantControl.AntiConRunRequired`; `ProcessTimings.AntiCondensationTime` = 5.0.

A complete, coherent five-signal feature with an operator enable, a per-start run/skip pair, a plant
request flag and a time — and **no requirement anywhere in any source**. It plausibly scopes to
fans/filters (three of the six in scope), but "plausibly" is not a scope. **BLOCKING** — an entire
unspecified feature set is the largest single gap this sweep found.

### Q-C31 — plant health inputs that exist while health is asserted true

`DiscreteInputs.ControlHealthy`, `DiscreteInputs.SurgeProtection`, `DiscreteOutputs.ControlHealthy`.

`[B-34]` records that this layer asserts every machine system-healthy **true**, defeating the
class-level health permissive (FU-04 / VSD-04) on five of the six in-scope machines. Meanwhile a
real plant health input exists and is claimed by nothing. The two facts together are the finding:
the permissive is not merely defeated, it is defeated **while a signal that could feed it sits
unused**. **BLOCKING** (this is the IO half of Q-C05 / Q-B12).

### Q-C37 — ownerless interlock-data members implying control

`InterlockData.SystemInterlock` (start value **true**), `PartInHand`, `PlantPartialRunning`,
`UpStreamEnable`, `Error`, `PreStartReqFlt`, `NotFirstScan`, `JOB9001LifeBitLost`, `JOB9001Status`,
`JOB9001ReadyToStop`, `JOB9002ReadyToStop`.

`SystemInterlock` defaulting to **true** is a plant-wide interlock that is satisfied by default;
`PartInHand` is the only in-hand-shaped signal in the whole export and is the sole non-interface
candidate for C1's hand/auto source on all six specs (Q-C04); `UpStreamEnable` is enable-shaped at
plant level. None is claimed by any source requirement. **BLOCKING** for `SystemInterlock`,
`PartInHand` and `UpStreamEnable`; the `JOB9001*`/`JOB9002*` members are third-party-plant handshake
signals belonging to out-of-scope machines (the incline feed and link conveyors) and are recorded as
`out-of-scope-instance` rather than as findings.

---

## Excluded by hard rule 2 — safety

`DiscreteInputs.EStopActivated`, `PanelEStopActivated`, `EStopFB1`…`EStopFB25`,
`ShredderEStopActivated`, `ShredderBypassActivated`; `DiscreteOutputs.EStopActive`, `EStopReset`;
`PlantControl.ShredderEStopFB`, `EstopResetStart`; `InterlockData.OldPlantEStopZone1/2/3`;
`DiscreteInputs.ECSEStopFB`.

**Not swept, not bound, not elaborated.** These are E-stop / safety-circuit feedback members. The
source register records the safety function as hardwired and outside this layer (`[B-33]`), and this
run neither reads nor specifies their behaviour. They are listed **by name only** so that the sweep
is provably complete rather than silently short — no safety content is analysed, and none of the six
specs references any of them. *No F-block, F-runtime group or safety program was encountered at any
point in this run; hard rule 2 was not otherwise triggered.*

---

## Full disposition table

`bound` = claimed by a spec in this run · `out-of-scope-instance` = belongs to one of the fourteen
machines not specified here · `UNCLAIMED` = a finding above · `safety` = excluded by hard rule 2.

### `DiscreteInputs` (DB 15)

| Members | Disposition |
|---|---|
| `FilterUnit1Ready`, `FilterUnit1Op`, `FilterUnit1Flt` | bound — `FilterUnitInst2` (Ready/Op unresolved, Q-C01) |
| `FilterUnit2Ready`, `FilterUnit2Op`, `FilterUnit2Flt` | bound — `FilterUnitInst3` (Ready/Op unresolved, Q-C02) |
| `CycloneDustRemOp`, `CycloneDustSysOk`, `CycloneDustAutoRunning/Stop` | bound — `FilterUnitInst1` (the third unresolved, Q-C06) |
| `AirStarDCRotSen`, `AirStarDCIsoFB` | bound — `MotorVSDInst1` |
| `OSCIsoFB` | bound — `MotorVSDInst3` |
| **`OSCRotSen`** | **UNCLAIMED — Q-C09** |
| `TomraReady`, `TomraRunning`, `TomraComFlt` | bound — `TomraControlInst1` (and `TomraReady`/`TomraComFlt` read by `MotorVSDInst3`) |
| `HFLCRunning`, `AirStarFCRunning`, `FMCCRunning`, `OverbandMagRunning`, `OSDrumSepRunning`, `OSECRunning`, `OSRCRunning`, `DiskSpreader1Running`, `DiskSpreader2Running`, `SFCRunningFwd`, `SFCRunningRev`, `SDCRuning`, `AirStarRunning` | out-of-scope-instance |
| `IFCIsoFB`, `HFLCIsoFB`, `AirStarFCIsoFB`, `FMCCIsoFB`, `OverbandMagIsoFB`, `OSDrumSepIsoFB`, `OSECIsoFB`, `OSRCIsoFB`, `DiskSpreader1IsoFB`, `DiskSpreader2IsoFB`, `SFCIsoFB`, `SDCIsoFB` | out-of-scope-instance |
| `AirStarFCRotSen`, `HFLCRotSen`, `FMCCRotSen`, `OSDrumSepRotSen`, `OSECRotSen`, `OSRCRotSen`, `SFCRotSen`, `SDCRotSen`, `IFCRotSen` | out-of-scope-instance |
| `ECSReady`, `ECSHandMode`, `ECSAutoMode`, `ECSSettingMode`, `ECSSumFlt`, `ECSRunning`, `ECSFCRotorFlt`, `ECSFltRotorRevs`, `ECSFCConveyorFlt`, `ECSFltConveyotRevs`, `ECSInterlockingFeeder`, `ECSMatChargeEnabled` | out-of-scope-instance (`ECSControlInst1`) |
| **`ControlHealthy`, `SurgeProtection`** | **UNCLAIMED — Q-C31** |
| **`Test : Array[0..75]`** | **UNCLAIMED — Q-C18** |
| `SpareDI : Array[0..4]` | UNCLAIMED — spare, non-control-implying by role; no finding |
| E-stop / shredder-E-stop / bypass members | safety — hard rule 2 |

### `DiscreteOutputs` (DB 16)

| Members | Disposition |
|---|---|
| `FilterUnit1Start`, `FilterUnit1Reset` | bound — `FilterUnitInst2` |
| `FilterUnit2Start`, `FilterUnit2Reset` | bound — `FilterUnitInst3` |
| `CycloneDustFilterStart`, `CycloneDustFilterReset` | bound — `FilterUnitInst1` |
| `TomraRun` | bound — `TomraControlInst1` |
| `HFLCStart`, `AirStarFCStart`, `FMCCStart`, `OverbandMagStart`, `OSDrumSepStart`, `OSECStart`, `OSRCStart`, `DiskSpreader1Start`, `DiskSpreader2Start`, `SFCRunFwd`, `SFCRunRev`, `SDCStart`, `ECSAutoStart`, `ECSReset` | out-of-scope-instance |
| `RunPreStart` | out-of-scope — the pre-start sequence is another block (`[B-06]`) |
| `PlantRunning` | out-of-scope — plant-level indication owned by the sequencer (`[B-04]`) |
| **`VSDFaulrReset`** | **UNCLAIMED — Q-C17** |
| **`ControlHealthy`** | **UNCLAIMED — Q-C31** |
| **`Test : Array[0..26]`** | **UNCLAIMED — Q-C18** |
| `EStopActive`, `EStopReset` | safety — hard rule 2 |

### `HMIControlSignals` (DB 38)

| Members | Disposition |
|---|---|
| `SystemReset` | bound — all six specs |
| `BypassAirStarDCRotSen` | bound — `MotorVSDInst1` |
| `BypassHFLCRotSen`, `BypassFMCCRotSen`, `BypassOSECRotSen`, `BypassOSRCRotSen`, `BypassSFCRotSen`, `BypassSDCRotSen`, `BypassIFCRotSen` | out-of-scope-instance |
| *(`BypassOSCRotSen` — does not exist)* | **proposed / missing — Q-C09** |
| `SystemStart`, `SystemStop`, `RunPreStart` | out-of-scope — plant sequencer / pre-start block |
| **`HandFansShutdown`** | **UNCLAIMED — Q-C16** |
| **`AntiConEnabled`, `RunAntiConNxtStart`, `SkipAntiConNxtStart`** | **UNCLAIMED — Q-C24** |
| **the eight `Global*Overwrite` flags** | **UNCLAIMED — Q-C15** |
| **`GlobalSetAllToAuto`** | **UNCLAIMED — Q-C25** |

### `PlantControl` (DB 39)

| Members | Disposition |
|---|---|
| `Status` | bound — all six specs (P4) |
| `PreStartComplete` | bound — all six specs (C6/C5/C4 + P5) |
| `FansShutdownReady` | bound — the three filter specs (P2) |
| `GeneralEnable`, `DrumsEnable`, `MagEnable`, `DrumShutdownReady`, `MagShutdownReady` | out-of-scope-instance |
| `AutoPreStart` | out-of-scope — the automatic pre-start request is keyed to the link conveyor (`[B-07]`) |
| `PositiveEdgeArray`, `NegitiveEdgeArray`, `LastCompleteStartUp`, `SFCStockWhenShredFlt` | out-of-scope-instance / sequencer-owned |
| **`AntiConRunRequired`** | **UNCLAIMED — Q-C24** |
| **`Test : Array[0..10]`** | **UNCLAIMED — Q-C18** (and the only candidate for `TomraControlInst1` C19) |
| `ShredderEStopFB`, `EstopResetStart` | safety — hard rule 2 |

### `InterlockData` (DB 2)

| Members | Disposition |
|---|---|
| `JOB9001Interlock`, `JOB9001ShutdownComplete`, `JOB9001LifeBitLost`, `JOB9001Status`, `JOB9001ReadyToStop`, `JOB9002ReadyToStop`, `LinkOut3rdParty`, `ShredderFeedLost`, `SystemDateTime` | out-of-scope-instance (third-party handshake for the incline feed / link conveyors; `LinkOut3rdParty` gates the out-of-scope air-separator's alternative path, `[B-44]`) |
| **`SystemInterlock` (= true), `PartInHand`, `UpStreamEnable`** | **UNCLAIMED — Q-C37** |
| **`Simulation`** | **UNCLAIMED — Q-C18** |
| `PlantPartialRunning`, `Error`, `PreStartReqFlt`, `NotFirstScan` | UNCLAIMED — Q-C37 (lower severity: plant-status-shaped rather than command-shaped, but still ownerless) |
| `OldPlantEStopZone1/2/3` | safety — hard rule 2 |

### `ProcessTimings` (DB 3)

| Members | Disposition |
|---|---|
| `NormalFanStartTime` = 10.0 | bound — the three filter specs (C7) |
| `PreStartRunTime`, `PreStartTimeout` | out-of-scope — pre-start block (`[B-06]`) |
| `DrumsDelay`, `MagnetsDelay`, `GeneralDelay` | out-of-scope-instance |
| **`AntiCondensationTime` = 5.0** | **UNCLAIMED — Q-C24** |
| **the seven `Global*` values** | **UNCLAIMED — Q-C15** |

---

## Sweep result

- Global DBs swept: **6**. Named members swept: **~190** (arrays counted once).
- Bound by the six specs: **26**.
- `out-of-scope-instance`: **~80**.
- Safety, excluded by hard rule 2: **~40** (by name only, not analysed).
- **UNCLAIMED and control-implying: 10 findings, all BLOCKING** — Q-C09, Q-C10, Q-C15, Q-C16,
  Q-C17, Q-C18, Q-C24, Q-C25, Q-C31, Q-C37.
- UNCLAIMED and not control-implying: **1** (`SpareDI`).

**The two findings that would not have been caught by the forward binding pass alone** are Q-C09
(a motion sensor and its missing bypass on a machine whose specification says it has neither) and
Q-C24 (an entire feature set no requirement mentions). Both are invisible from the requirement side
by construction — there is no requirement to bind them to.
