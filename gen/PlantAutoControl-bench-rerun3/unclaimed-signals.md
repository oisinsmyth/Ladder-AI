# PlantAutoControl-bench-rerun3 — Plant-level residual signal sweep (rung C §8(b))

Run **once** for the project. Every member of every global DB in `ir/PlantAutoControl-bench/` is listed
below with either a reason or a blocking `Q-nn`. Judged **by role, not by name pattern** — the
`Bypass*` / `*Enable` / `*Select` families are a prompt for attention, never the test.

- **Produced:** 2026-08-05, `gen-equipment-spec` §8(b).
- **Swept DBs:** `DiscreteInputs` (Input.ir), `DiscreteOutputs` (Output.ir), `HMIControlSignals`
  (HMIControlSignals.ir), `PlantControl` (**Control.ir**), `InterlockData` (**PLC.ir**), `ProcessTimings`
  (Timings.ir).
- **"Claimed"** means claimed by one of the six instance specs in
  `gen/PlantAutoControl-bench-rerun3/equipment-specs/`. **This run specifies 6 of the plant's 20
  machines**, so "claimed by no instance spec" most often means "belongs to a machine outside this
  run's scope" — that is a legitimate reason and is stated as such. The findings that matter are the
  signals that belong to **no machine at all**.

## Summary

| Bucket | Count |
|---|---|
| Claimed by an in-scope instance spec | 32 |
| Disposed with a stated reason (out-of-scope machine, safety boundary, sequencer, spare, or a blocking Q) | 168 |
| **Unaccounted** | **1** — and only because of a tool token-regex gap, see §7 |
| **Total swept (global-DB + tag-table leaves)** | **201** |

Counts are the **machine-computed** ones from
`converter signal-sweep --project ir/PlantAutoControl-bench --specs … --register … --unclaimed …`,
not a hand tally. They count array **leaves** (e.g. `DiscreteInputs.Test[0..75]` is 76 leaves), which
is why they exceed the member counts the narrative sections below use. Of the 168 disposed, **31
carry a blocking `Q-nn`** (§6).

---

## 1. Claimed by an in-scope instance spec (22)

`DiscreteInputs`: `FilterUnit1Flt`, `FilterUnit1Op`*, `FilterUnit1Ready`*, `FilterUnit2Flt`,
`FilterUnit2Op`*, `FilterUnit2Ready`*, `CycloneDustRemOp`, `CycloneDustAutoRunning/Stop`,
`CycloneDustSysOk`, `AirStarDCIsoFB`, `AirStarDCRotSen`, `OSCIsoFB`, `TomraReady`, `TomraRunning`,
`TomraComFlt`
`DiscreteOutputs`: `FilterUnit1Start`, `FilterUnit1Reset`, `FilterUnit2Start`, `FilterUnit2Reset`,
`CycloneDustFilterStart`, `CycloneDustFilterReset`, `TomraRun`
(*) claimed but **unresolved** — in a blocking candidate set (Q-C01 / Q-C02).

Shared plant conditions claimed by all six specs: `PlantControl.Status`,
`PlantControl.PreStartComplete`, `PlantControl.FansShutdownReady` (filter units only),
`HMIControlSignals.SystemReset`, `ProcessTimings.NormalFanStartTime` (filter units only).

## 2. Unclaimed — belongs to a machine outside this run's scope (62)

Reason for every row: the machine is one of the 14 not specified by this run. Named so that a later
run covering that machine can pick the signal up rather than rediscover it.

| Family | Signals | Machine |
|---|---|---|
| `IFC*` | `IFCIsoFB`, `IFCRotSen`, `HMIControlSignals.BypassIFCRotSen` | Incline Feed Conveyor (`MotorVSDInst2`) |
| `HFLC*` | `HFLCRunning`, `HFLCIsoFB`, `HFLCRotSen`, `BypassHFLCRotSen`, `HFLCStart` | Link Conveyor (`MotorStarterInst4`) |
| `AirStarFC*` | `AirStarFCRunning`, `AirStarFCIsoFB`, `AirStarFCRotSen`, `AirStarFCStart` | air-separator feed conveyor |
| `AirStar*` | `AirStarRunning` | Air-Separator VSD (`AirStarInst1`) |
| `FMCC*` | `FMCCRunning`, `FMCCIsoFB`, `FMCCRotSen`, `BypassFMCCRotSen`, `FMCCStart` | Metal Collection Conveyor (`MotorStarterInst3`) |
| `MotorStarterInst81*` | `OverbandMagRunning`, `OverbandMagIsoFB`, `OverbandMagStart` | Overband Magnet (`MotorStarterInst8`) |
| `MotorStarterInst5*` | `OSDrumSepRunning`, `OSDrumSepIsoFB`, `OSDrumSepRotSen`, `OSDrumSepStart` | Drum Separator (`MotorStarterInst5`) |
| `OSEC*` | `OSECRunning`, `OSECIsoFB`, `OSECRotSen`, `BypassOSECRotSen`, `OSECStart` | Ejected Material Conveyor (`MotorStarterInst6`) |
| `OSRC*` | `OSRCRunning`, `OSRCIsoFB`, `OSRCRotSen`, `BypassOSRCRotSen`, `OSRCStart` | Residual Material Conveyor (`MotorStarterInst7`) |
| `DiskSpreader*` | `MotorStarterInst1/2Running`, `MotorStarterInst1/2IsoFB`, `MotorStarterInst1/2Start` | Disc Spreaders 1/2 |
| `SFC*` | `SFCRunningFwd`, `SFCRunningRev`, `SFCIsoFB`, `SFCRotSen`, `BypassSFCRotSen`, `SFCRunFwd`, `SFCRunRev`, `PlantControl.SFCStockWhenShredFlt`, `InterlockData.ShredderFeedLost` | Feed Conveyor (`MotorFwdRevInst1`) |
| `SDC*` | `SDCRuning` *(sic — one `n`)*, `SDCIsoFB`, `SDCRotSen`, `BypassSDCRotSen`, `SDCStart` | Discharge Conveyor (`MotorStarterInst9`) |
| `ECS*` | `ECSReady`, `ECSHandMode`, `ECSAutoMode`, `ECSSettingMode`, `ECSSumFlt`, `ECSRunning`, `ECSFCRotorFlt`, `ECSFltRotorRevs`, `ECSFCConveyorFlt`, `ECSFltConveyotRevs`, `ECSInterlockingFeeder`, `ECSMatChargeEnabled`, `ECSAutoStart`, `ECSReset` | Equipment Control System (`ECSControlInst1`) |
| `JOB9001*` | `JOB9001Interlock`, `JOB9001ShutdownComplete`, `JOB9001LifeBitLost`, `JOB9001Status`, `JOB9001ReadyToStop` | third-party plant handshake (REQ-014 machines) |
| shredder | `ShredderBypassActivated` | Shredder (`ShredderControlInst1`) |
| enables | `PlantControl.MagEnable`, `DrumsEnable`, `MagShutdownReady`, `DrumShutdownReady` | magnet / drum sub-systems |

**Recorded for a later run, not for this one:** `DiscreteInputs.OSCRotSen` is *in* scope and is
**unclaimed** — that finding lives in `MotorVSDInst3.md` as blocking **Q-C07**, not here.

## 3. Unclaimed — safety-circuit boundary (36) — HARD RULE 2

`DiscreteInputs`: `EStopActivated`, `PanelEStopActivated`, `EStopFB1` … `EStopFB25`,
`ShredderEStopActivated`, `ECSEStopFB`
`DiscreteOutputs`: `EStopActive`, `EStopReset`
`PlantControl`: `ShredderEStopFB`, `EstopResetStart`
`InterlockData`: `OldPlantEStopZone1`, `OldPlantEStopZone2`, `OldPlantEStopZone3`

**Disposition: stop at the boundary.** These are E-stop / safety-circuit feedback and command
signals. This run does **not** read, specify, explain or reference their internals; no requirement
above binds any of them; no rung below may claim them. The source register records the same position
(REQ-022 / B-32: the E-stop function is hardwired and other-block material, and the E-stop members
present in these DBs are deliberately untouched).
**Reported, per hard rule 2:** safety-related signals are present *inside ordinary non-safety global
DBs* in this project. That is a fact an engineer should know about the data landscape; it is flagged
rather than swept, and nothing here is elaborated further.

## 4. Unclaimed — plant-sequencer / other-block function (17)

Reason: rung B recorded these functions as belonging to other blocks (B-33) — the master
start/stop sequencer, the run-state computation, the pre-start timer, the anti-condensation
sequence's own timing, and the third-party handshake's own logic.

`HMIControlSignals`: `SystemStart`, `SystemStop`, `RunPreStart`
`DiscreteOutputs`: `RunPreStart`, `PlantRunning`
`PlantControl`: `PositiveEdgeArray`, `NegitiveEdgeArray`, `LastCompleteStartUp`, `AutoPreStart`
`InterlockData`: `SystemDateTime`, `NotFirstScan`, `Error`, `PreStartReqFlt`, `JOB9002ReadyToStop`
`ProcessTimings`: `PreStartRunTime`, `PreStartTimeout`, `AntiCondensationTime`

## 5. Unclaimed — spare / unused (2)

`DiscreteInputs.SpareDI` (`Array[0..4] of Bool`) — declared spare.
`ProcessTimings.DrumsDelay` (0.0), `MagnetsDelay` (5.0), `GeneralDelay` (10.0) — sub-system start
delays for out-of-scope machines; counted in §2's reason, listed here for completeness.

---

## 6. BLOCKING — implies a control function, owned by nobody (31)

Each of these implies a control function, is claimed by **no** instance spec in this run, and is not
attributable to a named out-of-scope machine. Per §8, that makes it blocking regardless of its name.

### Q-C20 (BLOCKING) — plant health and protection inputs have no owner
`DiscreteInputs.ControlHealthy`, `DiscreteOutputs.ControlHealthy`, `DiscreteInputs.SurgeProtection`

Every one of the six in-scope specs carries a class requirement of the form *"does not start unless
it is reported system-healthy"* (C4 in five specs) that **no signal satisfies**, because rung B
records this layer as asserting health true for every machine [B-32]. Meanwhile the plant has a
health input, a health output and a surge-protection input that **nothing in this specification
claims**. Either those signals feed the health function that six requirements are waiting for, or
plant health is genuinely computed elsewhere and these three are that other block's. The two
readings are not reconciled here.
*Why it is blocking rather than a note:* a start permissive that is present in the requirement set
and satisfied by a hardcoded truth is the most invisible kind of missing interlock — it reads as
implemented in every review.

### Q-C21 (BLOCKING) — an unclaimed drive fault-reset output
`DiscreteOutputs.VSDFaulrReset` *(sic — the member is spelled `Faulr`)*

A fault-reset **output** aimed at drives. Both in-scope VSD specs bind their fault reset to the
plant-wide operator reset input (C16) and neither claims this output. It may belong to
`MotorVSDInst1`, to `MotorVSDInst3`, to the out-of-scope incline-feed VSD, or to all three. A shared
fault-reset output claimed by nobody is one of the named residual shapes this sweep exists to catch.
**Not claimed unilaterally by either VSD spec** — that would be the guess this rung forbids.

### Q-C22 (BLOCKING) — an entire unspecified feature: anti-condensation
`HMIControlSignals.RunAntiConNxtStart`, `SkipAntiConNxtStart`, `AntiConEnabled`,
`PlantControl.AntiConRunRequired`, `ProcessTimings.AntiCondensationTime` (5.0)

Five signals, an operator enable, a per-start run/skip command pair, a plant-level "required" flag
and a configured time — a complete, coherent feature set for running equipment to keep it dry. **No
requirement anywhere in rungs A, B or C mentions anti-condensation.** It plainly runs *equipment*,
which means it would run the equipment specified by this run. Whether it belongs to this control
layer or to another block is not stated by any source.
*(`ProcessTimings.AntiCondensationTime` is listed in §4 as a sequencer timing and here as part of
this feature — deliberately, because which it is is exactly the question.)*

### Q-C23 (BLOCKING) — a global settings-override mechanism over in-scope machines
`HMIControlSignals.GlobalFTTimeOverwrite`, `GlobalUPSEnableTimeOverwrite`,
`GlobalShutdownCompleteTimeOverwrite`, `GlobalVSDStartUpTimeOverwrite`, `GlobalVSDAutoSpeedOverwrite`,
`GlobalVSDHandSpeedOverwrite`, `GlobalFwdRevDOLRevDelayTimeOverwrite`,
`GlobalFwdRevDOLFTRIgnoreTimeOverwrite`
`ProcessTimings.GlobalFTTimes`, `GlobalUPEnableTime`, `GlobalShutdownCompleteTime`,
`GlobalVSDStartUpTime`, `GlobalVSDAutoSpeed`, `GlobalVSDHandSpeed`,
`GlobalFwdRevDOLRevDelayTime`, `GlobalFwdRevDOLFTRIgnoreTime`

Sixteen signals forming one mechanism: eight operator flags, each paired with a plant-wide value,
that overwrite exactly the per-machine settings this run's six specs list as **"not stated"**
(fail-to-run time, up-to-speed enable time, shutdown-complete time, VSD start-up time, VSD auto and
hand speeds). No source describes it. **This makes rung B's Q-B6 sharper, not softer:** the settings
have no stated values *and* there is an unspecified mechanism for overwriting them globally at
runtime. All eight values currently sit at 0.0, which is itself a question — a global overwrite of
0.0 applied to a fail-to-run time is not a neutral default.

### Q-C24 (BLOCKING) — a global mode command over every machine
`HMIControlSignals.GlobalSetAllToAuto`

An operator command that puts **all** equipment into automatic — that is, it acts on the hand/auto
mode of every machine specified by this run. Rung B recorded per-machine hand behaviour (B-13, B-14)
and nothing plant-wide. Unclaimed.

### Q-C25 (BLOCKING) — plant-wide interlock / mode flags with no stated function
`InterlockData.SystemInterlock` (start value **TRUE**), `InterlockData.UpStreamEnable`,
`InterlockData.PartInHand`, `InterlockData.PlantPartialRunning`, `InterlockData.Simulation`,
`InterlockData.LinkOut3rdParty` (start value **TRUE**)

Six flags in the interlock DB, each of which plainly implies a control function, none claimed by any
requirement in this run:
- `SystemInterlock` defaults TRUE — a plant-wide interlock that is permissive-by-default is a
  standing question on every start permissive above.
- `UpStreamEnable` names the exact concept the whole start cascade is built on (B-09) but is a
  single plant-level flag, not a per-machine one.
- `PartInHand` and `PlantPartialRunning` imply plant modes in which *some* equipment runs — directly
  relevant to six machines whose specs assume a single plant run state (B-03).
- `Simulation` implies a plant-wide simulation mode. Combined with §7 below, that is a live concern.
- `LinkOut3rdParty` is referenced by rung B (B-12) as part of the air separator's alternative start
  path, but the air separator is out of this run's scope, so no spec here claims it. Recorded rather
  than orphaned.

### Q-C13 / Q-C26 (BLOCKING) — production functions carried on test/simulation arrays
`PlantControl.Test` (`Array[0..10] of Bool`), `DiscreteInputs.Test` (`Array[0..75] of Bool`),
`DiscreteOutputs.Test` (`Array[0..26] of Bool`)

Three test/simulation arrays sized to shadow the real IO (76 inputs, 27 outputs). Two independent
reasons this is blocking rather than housekeeping:
1. **A production function is already bound to one of them.** The optical sorter's data byte-order
   selection is carried on `PlantControl.Test[5]` [ref OS-19] — recorded as **Q-C13** in
   `TomraControlInst1.md`. A production selector living in a test array is a documented residual
   shape.
2. **A test array is written into a live signal path.** The class reference for the sorter records
   (anomaly A-1) that a status bit used by the fault logic is overwritten from a plant
   test/simulation array in the same scan.
Who owns these arrays, whether they are live in the running plant, and what disables them, is stated
by no source. **Q-C26** covers the arrays as a whole; the specific sorter binding stays Q-C13.

### Q-C04 (BLOCKING, also recorded on all three FilterUnitSystem specs) — an operator fan shutdown
`HMIControlSignals.HandFansShutdown`

An operator command scoped by its name to the fans — i.e. to the three filter units this run
specifies. No rung-A relation and no rung-B behaviour accounts for it. Recorded both here and in each
FilterUnitSystem spec because it is simultaneously an ownerless plant-level signal and an in-scope
per-instance gap.

### Q-C14 (BLOCKING, recorded on `TomraControlInst1`) — the sorter's data words do not exist
`Tag_45` … `Tag_54` → `converter tagstatus` classifies both probed endpoints **PROPOSED**.

Listed here so the plant-level view shows the sorter's link interface as an unresolved plant gap and
not merely an instance detail. Hard rule 3: proposed, never invented, nothing coded against them.

---

## Cross-check against the six instance specs

Every signal marked *claimed* in §1 appears in the `IO BINDING` or `UNCLAIMED IO` table of the named
spec; every blocking `Q-nn` raised here that lands on an in-scope machine (Q-C04, Q-C13, Q-C14,
Q-C20, Q-C21) is also carried in that machine's `OPEN` section, so a reader of either artifact alone
sees it.

---

## 7. Machine-readable disposition tables (the `signal-sweep` leg)

The narrative sections above are the finding; these tables carry the same content in the
form the `signal-sweep` checker reads - one table per global DB, **every** member listed,
the signal name in the first cell. No row here is a new judgment.

**Filename warning, found while building this section and worth stating loudly:**
`Control.ir` holds **`PlantControl`** and `PLC.ir` holds **`InterlockData`** - the two
filenames read as the opposite of their contents. An earlier draft of this table had them
swapped for exactly that reason, and `signal-sweep` caught it as 32 unaccounted signals.
Never infer a DB from its filename in this project.

### `DiscreteInputs` (Input.ir, 96 members)

| member | disposition |
|---|---|
| `Test` | **BLOCKING Q-C26** - test/simulation array shadowing real IO; one element already carries a production function |
| `HFLCRunning` | out-of-scope machine - Link Conveyor (MotorStarterInst4) |
| `AirStarFCRunning` | out-of-scope machine - air-separator feed conveyor |
| `FMCCRunning` | out-of-scope machine - Metal Collection Conveyor (MotorStarterInst3) |
| `OverbandMagRunning` | out-of-scope machine - Overband Magnet (MotorStarterInst8) |
| `OSDrumSepRunning` | out-of-scope machine - Drum Separator (MotorStarterInst5) |
| `OSECRunning` | out-of-scope machine - Ejected Material Conveyor (MotorStarterInst6) |
| `OSRCRunning` | out-of-scope machine - Residual Material Conveyor (MotorStarterInst7) |
| `DiskSpreader1Running` | out-of-scope machine - Disc Spreaders 1/2 |
| `DiskSpreader2Running` | out-of-scope machine - Disc Spreaders 1/2 |
| `SFCRunningFwd` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SFCRunningRev` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SDCRuning` | out-of-scope machine - Discharge Conveyor (MotorStarterInst9) |
| `TomraReady` | claimed by an in-scope instance spec |
| `TomraRunning` | claimed by an in-scope instance spec |
| `IFCIsoFB` | out-of-scope machine - Incline Feed Conveyor (MotorVSDInst2) |
| `HFLCIsoFB` | out-of-scope machine - Link Conveyor (MotorStarterInst4) |
| `AirStarFCIsoFB` | out-of-scope machine - air-separator feed conveyor |
| `AirStarDCIsoFB` | claimed by an in-scope instance spec |
| `FMCCIsoFB` | out-of-scope machine - Metal Collection Conveyor (MotorStarterInst3) |
| `OverbandMagIsoFB` | out-of-scope machine - Overband Magnet (MotorStarterInst8) |
| `OSDrumSepIsoFB` | out-of-scope machine - Drum Separator (MotorStarterInst5) |
| `OSECIsoFB` | out-of-scope machine - Ejected Material Conveyor (MotorStarterInst6) |
| `OSRCIsoFB` | out-of-scope machine - Residual Material Conveyor (MotorStarterInst7) |
| `DiskSpreader1IsoFB` | out-of-scope machine - Disc Spreaders 1/2 |
| `DiskSpreader2IsoFB` | out-of-scope machine - Disc Spreaders 1/2 |
| `SFCIsoFB` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SDCIsoFB` | out-of-scope machine - Discharge Conveyor (MotorStarterInst9) |
| `OSCIsoFB` | claimed by an in-scope instance spec |
| `TomraComFlt` | claimed by an in-scope instance spec |
| `AirStarFCRotSen` | out-of-scope machine - air-separator feed conveyor |
| `HFLCRotSen` | out-of-scope machine - Link Conveyor (MotorStarterInst4) |
| `FMCCRotSen` | out-of-scope machine - Metal Collection Conveyor (MotorStarterInst3) |
| `OSDrumSepRotSen` | out-of-scope machine - Drum Separator (MotorStarterInst5) |
| `OSECRotSen` | out-of-scope machine - Ejected Material Conveyor (MotorStarterInst6) |
| `OSRCRotSen` | out-of-scope machine - Residual Material Conveyor (MotorStarterInst7) |
| `SFCRotSen` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SDCRotSen` | out-of-scope machine - Discharge Conveyor (MotorStarterInst9) |
| `IFCRotSen` | out-of-scope machine - Incline Feed Conveyor (MotorVSDInst2) |
| `OSCRotSen` | claimed by an in-scope instance spec |
| `AirStarDCRotSen` | claimed by an in-scope instance spec |
| `ShredderBypassActivated` | out-of-scope machine - Shredder (ShredderControlInst1) |
| `ShredderEStopActivated` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `ControlHealthy` | **BLOCKING Q-C20** - plant health / protection, owned by nobody |
| `SurgeProtection` | **BLOCKING Q-C20** - plant health / protection, owned by nobody |
| `EStopActivated` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `PanelEStopActivated` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB1` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB2` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB3` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB4` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB5` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB6` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB7` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB8` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB9` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB10` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB11` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB12` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB13` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB14` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB15` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB16` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB17` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB18` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB19` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB20` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB21` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB22` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB23` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB24` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `EStopFB25` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `AirStarRunning` | out-of-scope machine - Air-Separator VSD (AirStarInst1) |
| `FilterUnit1Flt` | claimed by an in-scope instance spec |
| `FilterUnit1Op` | claimed by an in-scope instance spec |
| `FilterUnit1Ready` | claimed by an in-scope instance spec |
| `FilterUnit2Flt` | claimed by an in-scope instance spec |
| `FilterUnit2Op` | claimed by an in-scope instance spec |
| `FilterUnit2Ready` | claimed by an in-scope instance spec |
| `ECSEStopFB` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `ECSReady` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSHandMode` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSAutoMode` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSSettingMode` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSSumFlt` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSRunning` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSFCRotorFlt` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSFltRotorRevs` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSFCConveyorFlt` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSFltConveyotRevs` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSInterlockingFeeder` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSMatChargeEnabled` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `CycloneDustAutoRunning/Stop` | claimed by an in-scope instance spec |
| `CycloneDustSysOk` | claimed by an in-scope instance spec |
| `CycloneDustRemOp` | claimed by an in-scope instance spec |
| `SpareDI` | declared spare |

### `DiscreteOutputs` (Output.ir, 28 members)

| member | disposition |
|---|---|
| `Test` | **BLOCKING Q-C26** - test/simulation array shadowing real IO; one element already carries a production function |
| `HFLCStart` | out-of-scope machine - Link Conveyor (MotorStarterInst4) |
| `AirStarFCStart` | out-of-scope machine - air-separator feed conveyor |
| `FMCCStart` | out-of-scope machine - Metal Collection Conveyor (MotorStarterInst3) |
| `OverbandMagStart` | out-of-scope machine - Overband Magnet (MotorStarterInst8) |
| `OSDrumSepStart` | out-of-scope machine - Drum Separator (MotorStarterInst5) |
| `OSECStart` | out-of-scope machine - Ejected Material Conveyor (MotorStarterInst6) |
| `OSRCStart` | out-of-scope machine - Residual Material Conveyor (MotorStarterInst7) |
| `DiskSpreader1Start` | out-of-scope machine - Disc Spreaders 1/2 |
| `DiskSpreader2Start` | out-of-scope machine - Disc Spreaders 1/2 |
| `SFCRunFwd` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SFCRunRev` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `SDCStart` | out-of-scope machine - Discharge Conveyor (MotorStarterInst9) |
| `RunPreStart` | plant-sequencer / other-block function (B-33) |
| `PlantRunning` | plant-sequencer / other-block function (B-33) |
| `EStopActive` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `ControlHealthy` | **BLOCKING Q-C20** - plant health / protection, owned by nobody |
| `FilterUnit1Start` | claimed by an in-scope instance spec |
| `FilterUnit1Reset` | claimed by an in-scope instance spec |
| `FilterUnit2Start` | claimed by an in-scope instance spec |
| `FilterUnit2Reset` | claimed by an in-scope instance spec |
| `ECSAutoStart` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `ECSReset` | out-of-scope machine - Equipment Control System (ECSControlInst1) |
| `CycloneDustFilterStart` | claimed by an in-scope instance spec |
| `CycloneDustFilterReset` | claimed by an in-scope instance spec |
| `VSDFaulrReset` | **BLOCKING Q-C21** - drive fault-reset output claimed by no instance |
| `TomraRun` | claimed by an in-scope instance spec |
| `EStopReset` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |

### `HMIControlSignals` (HMIControlSignals.ir, 25 members)

| member | disposition |
|---|---|
| `SystemStart` | plant-sequencer / other-block function (B-33) |
| `SystemStop` | plant-sequencer / other-block function (B-33) |
| `SystemReset` | claimed by an in-scope instance spec |
| `RunPreStart` | plant-sequencer / other-block function (B-33) |
| `RunAntiConNxtStart` | **BLOCKING Q-C22** - anti-condensation, an entire unspecified feature |
| `SkipAntiConNxtStart` | **BLOCKING Q-C22** - anti-condensation, an entire unspecified feature |
| `HandFansShutdown` | **BLOCKING Q-C04** - operator fan shutdown, scoped to the three in-scope filter units |
| `AntiConEnabled` | **BLOCKING Q-C22** - anti-condensation, an entire unspecified feature |
| `GlobalFTTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalUPSEnableTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalShutdownCompleteTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDStartUpTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDAutoSpeedOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDHandSpeedOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalFwdRevDOLRevDelayTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalFwdRevDOLFTRIgnoreTimeOverwrite` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalSetAllToAuto` | **BLOCKING Q-C24** - plant-wide mode command over every machine |
| `BypassHFLCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassFMCCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassOSECRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassOSRCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassSFCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassSDCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassIFCRotSen` | out-of-scope machine - out-of-scope conveyor motion bypass |
| `BypassAirStarDCRotSen` | claimed by an in-scope instance spec |

### `PlantControl` (Control.ir, 17 members)

| member | disposition |
|---|---|
| `Status` | claimed by an in-scope instance spec |
| `PositiveEdgeArray` | plant-sequencer / other-block function (B-33) |
| `NegitiveEdgeArray` | plant-sequencer / other-block function (B-33) |
| `LastCompleteStartUp` | plant-sequencer / other-block function (B-33) |
| `AntiConRunRequired` | **BLOCKING Q-C22** - anti-condensation, an entire unspecified feature |
| `GeneralEnable` | claimed by an in-scope instance spec |
| `DrumsEnable` | out-of-scope machine - drum sub-system |
| `MagEnable` | out-of-scope machine - magnet sub-system |
| `DrumShutdownReady` | out-of-scope sub-system |
| `FansShutdownReady` | claimed by an in-scope instance spec |
| `MagShutdownReady` | out-of-scope machine - magnet sub-system |
| `PreStartComplete` | claimed by an in-scope instance spec |
| `ShredderEStopFB` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `SFCStockWhenShredFlt` | out-of-scope machine - Feed Conveyor (MotorFwdRevInst1) |
| `EstopResetStart` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `AutoPreStart` | plant-sequencer / other-block function (B-33) |
| `Test` | **BLOCKING Q-C26** - test/simulation array shadowing real IO; one element already carries a production function |

### `InterlockData` (PLC.ir, 20 members)

| member | disposition |
|---|---|
| `Simulation` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |
| `SystemDateTime` | plant-sequencer / other-block function (B-33) |
| `JOB9001Interlock` | out-of-scope machine - third-party plant handshake |
| `JOB9001ShutdownComplete` | out-of-scope machine - third-party plant handshake |
| `NotFirstScan` | plant-sequencer / other-block function (B-33) |
| `UpStreamEnable` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |
| `Error` | plant-sequencer / other-block function (B-33) |
| `JOB9001LifeBitLost` | out-of-scope machine - third-party plant handshake |
| `JOB9001Status` | out-of-scope machine - third-party plant handshake |
| `LinkOut3rdParty` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |
| `PreStartReqFlt` | plant-sequencer / other-block function (B-33) |
| `SystemInterlock` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |
| `PartInHand` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |
| `ShredderFeedLost` | out-of-scope machine - Shredder (ShredderControlInst1) |
| `OldPlantEStopZone1` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `OldPlantEStopZone2` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `OldPlantEStopZone3` | **safety-circuit boundary - hard rule 2**; not specified, internals not examined |
| `JOB9001ReadyToStop` | out-of-scope machine - third-party plant handshake |
| `JOB9002ReadyToStop` | out-of-scope machine - third-party plant handshake |
| `PlantPartialRunning` | **BLOCKING Q-C25** - plant-wide interlock/mode flag with no stated function |

### `ProcessTimings` (Timings.ir, 15 members)

| member | disposition |
|---|---|
| `AntiCondensationTime` | **BLOCKING Q-C22** - anti-condensation, an entire unspecified feature |
| `PreStartRunTime` | plant-sequencer / other-block function (B-33) |
| `NormalFanStartTime` | claimed by an in-scope instance spec |
| `DrumsDelay` | out-of-scope machine - drum sub-system |
| `MagnetsDelay` | out-of-scope machine - magnet sub-system |
| `GeneralDelay` | plant-sequencer / other-block function (B-33) |
| `PreStartTimeout` | plant-sequencer / other-block function (B-33) |
| `GlobalFTTimes` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalUPEnableTime` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalShutdownCompleteTime` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDStartUpTime` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDAutoSpeed` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalVSDHandSpeed` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalFwdRevDOLRevDelayTime` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |
| `GlobalFwdRevDOLFTRIgnoreTime` | **BLOCKING Q-C23** - global settings-override mechanism over in-scope settings |

### Known checker gap on one member

`DiscreteInputs.CycloneDustAutoRunning/Stop` contains a `/`, so it is not
identifier-shaped to the sweep tool's token regex and reads as **unaccounted** however it
is written here. It **is** bound - `FilterUnitInst1.md` C14 - and `converter tagstatus`
resolves it as EXISTS. The single residual `unaccounted` signal in the sweep output is
this member, and nothing else.
