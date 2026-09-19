# Purpose-change spec — MotorStarter (DOL) → VSD motor control

**Stage:** `gen-spec-analysis`, performed manually-to-contract (no skill) for the
`gen-block-modify-purpose` blind validation.

> 🔴 **Data tier — this case is NOT Green.** Corrected 2026-09-19. This document previously claimed
> "**Green artifact** — invented names only"; **that claim was false** and is retracted here.
>
> Measured 2026-09-19 against this project's derived sanitization vocabulary: **3 of the 10 tracked
> files under `gen/_validation/MotorVSDSystem-purpose/` carry live (unsanitized) vocabulary — 1 distinct
> needle.** The answer key under `answerkey/` is a **byte-identical copy** of
> `ir/PlantAutoControl-bench/MotorVSDSystem.ir` (md5 `9d8dca41764330b889ad9c2e4b2b3d91`), and that bench
> directory carries live vocabulary in **2 of its 35 tracked files — 14 distinct needles, 2 of them
> DECLARED (T1) terms**. Neither directory appears in the scrub builder's `--green` list, so the
> tooling has never treated this content as Green — only this document did.
>
> **Treat this case as Amber** in the sense of `docs/13-data-boundary.md`: real project logic with
> identifying data, usable *only after sanitization, or with explicit per-project approval*. The
> approval it was produced under is recorded there (2026-07-20, the `gen-block-modify-purpose`
> validation extension), and that entry's **sanitize-to-Green-first handling did not fully take** —
> so the artifacts are covered by an approval, not made Green by one.
>
> **Consequence — this is about copying, not about reading.** These files must **not** be copied,
> promoted or reused outside this repository — into `patterns/`, a committed doc, a test fixture, a
> live-run folder, or anything published — without going through the scrub
> (`tools/build-scrub-rules.py` / `tools/verify-scrub.py`). Nothing is leaking today: the
> publication gate (Gate 3 in `tools/verify-scrub.py`) returns an earned zero over exactly this
> content. The risk this correction addresses is a **false "already sanitised" label**, which is
> what licenses a copy into somewhere the scrub does not run — the shape of AB-1 in
> `docs/notes/data-boundary-audit-backlog.md`.
>
> The live terms themselves are deliberately **not** listed here, per that file's own rule: *a
> record of a leak must not be a copy of it.* Re-derive them with the scrub tooling if needed.
>
> Nothing else in this document changes: the task, the REQs, the interface delta and the blindness
> argument below are unaffected by this correction.

## Task for the generator

Start from the **DOL source** `source/MotorStarter.ir` (a complete direct-online-start motor FB)
and **modify its purpose** into a **variable-speed-drive (VSD) motor FB**. This is a scoped purpose
change, not a rewrite: the large majority of the DOL block's behaviour carries over unchanged; the
VSD **layers a speed-control capability on top** and **swaps the motor-feedback model** from a single
on/off contactor to a bidirectional drive.

Implement the HOW yourself (network decomposition, wiring, exact conditions). This spec states the
required **behaviour and interface**, not the ladder.

### Targets you may read (generator-visible)
- `source/MotorStarter.ir` — the DOL baseline to modify.
- `deps/MotorVSDIOSet.ir` — the **target IO struct type** the VSD block's `IO` member must be typed
  to. Every caller-facing signal lives here.
- `deps/AnalogScale.ir` — a reusable linear-scaling FC (`Input`, `Input_Min`, `Input_Max`,
  `Scaled_Min`, `Scaled_Max` → `Output`) to be `CALL`ed for speed scaling.

---

## Section A — DOL behaviours that CARRY OVER unchanged

The VSD keeps all of the DOL FB's existing behaviour. Modify only where a REQ in Section B/C requires
it; otherwise preserve intent, structure, and the site idioms (constructed edges, HMI Set/Reset
contracts, ms-scaled timers).

- **REQ-C1 — Start/stop arbitration.** Arbitrate hand vs auto start commands into a run-permission,
  gated by inhibit / active-fault / system-healthy, and latch a run command that drops on stop or
  shutdown. *(Carried; note the run-permission additionally blocks while fail-to-run is latched.)*
- **REQ-C2 — Hand-intervention latches.** Maintain the `HandIntervention` / `HandStartSignal`
  Set/Reset latches on the HMI press/release contract.
- **REQ-C3 — Start-signal debounce.** Re-assert the hand start signal via a short constructed timer
  after an inhibit/fault clears.
- **REQ-C4 — Hand-selector edge detection.** Detect rising/falling edges of the hand selector the
  constructed way (stored previous-scan bit), not the built-in edge instruction.
- **REQ-C5 — Prestart hold.** Latch `PreStartMemory` once prestart is done; hold until
  fault/stop/hand-selector edge.
- **REQ-C6 — HMI time scaling.** Convert the HMI-set Real-seconds timing parameters into the ms
  integers the timers consume (shutdown, upstream-enable, fail time). *(Extended by REQ-V4.)*
- **REQ-C7 — Delayed shutdown sequencing.** Set/Reset `StopMotor` around a shutdown timer and
  compute `ShutdownComplete`. *(Feedback source adapted per REQ-V6.)*
- **REQ-C8 — Fail-to-run.** Trip `FTR` when the motor is commanded running but feedback is absent for
  the fail time, latched until reset. *(Arming adapted per REQ-V5; feedback per REQ-V6.)*
- **REQ-C9 — Fail-to-stop.** Trip `FTS` when the motor is commanded stopped but feedback persists for
  the fail time, latched until reset. *(Feedback per REQ-V6.)*
- **REQ-C10 — Fault aggregation.** Aggregate `FaultActive`, latched, cleared by `FaultReset`.
  *(Fault sources adapted per REQ-V8.)*
- **REQ-C11 — Upstream enable.** Assert `UPSEnable` after the motor has run for the enable-upstream
  delay, feeding the next equipment's start permission. *(Feedback per REQ-V6.)*
- **REQ-C12 — Hours-run totaliser.** Totalise running hours into `HrsRun`.
- **REQ-C13 — HMI telemetry.** Drive the `Telemetry` status code (stopped / starting / running /
  running-with-upstream-enabled / fault). *(Feedback per REQ-V6.)*
- **REQ-C14 — Alarm word.** Pack `FTR`, `FTS`, and the third fault source into the `Alarm` word bits.
  *(Third source adapted per REQ-V8.)*

## Section B — VSD behaviours ADDED over the DOL

- **REQ-V1 — Speed-source selection.** Select the active speed setpoint (`SpeedPerc`, a 0–100 %
  value) from the two speed inputs by hand/auto mode: use `AutoSpeedInput` in auto, `HandSpeedInput`
  in hand.
- **REQ-V2 — Minimum-speed clamp.** Floor each incoming speed input at a configured minimum before it
  is used, so the drive is never commanded below its minimum runnable speed.
- **REQ-V3 — Speed scaling to engineering units.** Scale the selected `SpeedPerc` (0–100 %) to the
  engineering speed output `SpeedOutput` over the range 0 … `MaxRPM`, by `CALL`ing `AnalogScale`.
- **REQ-V4 — Startup / speed-settle grace window.** Provide a `StartUpTime` parameter (HMI-set Real
  seconds, scaled to ms alongside the REQ-C6 times). A startup timer must complete — measured only
  once the motor is running **and** the commanded speed has settled (setpoint stable) with no recent
  direction change — before fail-to-run monitoring is considered valid.
- **REQ-V5 — Fail-to-run gated by the startup window.** Arm REQ-C8 fail-to-run detection off the
  REQ-V4 startup-window completion rather than directly off the run command, so drive ramp-up and
  commanded-speed changes do not cause nuisance fail-to-run trips.
- **REQ-V6 — Bidirectional feedback model.** The drive exposes a forward and a reverse run command
  (`Run`, `RunRev`) and **direction-specific** running feedback (`RunningFwdFB`, `RunningRevFB`),
  replacing the DOL's single `RunningFB`. Every behaviour that consumed `RunningFB` (shutdown-complete,
  fail-to-run, fail-to-stop, upstream-enable, hours-run, telemetry) must instead derive its
  "running / not-running" condition from the direction-appropriate feedback.
- **REQ-V7 — Reverse-command edge re-arm.** Track edges of the reverse run command and use them to
  re-arm the REQ-V4 startup window (a direction change restarts the settle measurement).
- **REQ-V8 — Drive-fault integration.** The aggregate fault (REQ-C10) and the alarm word (REQ-C14)
  take the drive's own fault signal `VSDError` in place of the DOL's generic external `FaultFB`
  (which no longer exists on the interface). `VSDError` is a drive-provided input, not computed here.

## Section C — Interface delta (what the gate-1 manifest must change)

Re-type the `IO` static member from `MotorIOSet` to **`MotorVSDIOSet`** (`deps/MotorVSDIOSet.ir`).
Relative to the DOL's `MotorIOSet`:

**Removed** (DOL had; VSD interface does not — dependent logic must be rewired per REQ-V6/V8):
`RunningFB`, `FaultFB`.

**Added** (speed / direction / drive-status members):
`RunRev`, `RunningFwdFB`, `RunningRevFB`, `VSDError`, `HandSpeedInput`, `AutoSpeedInput`,
`SpeedPerc`, `SpeedOutput`, `MaxRPM`, `StartUpTime` — **driven or consumed by the REQs above**; plus
`VSDWarning`, `SpeedFB`, `SpeedReached`, `AmpsFB`, `OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`,
`VSDReady`, `VSDOpEnable`, `VSDOverSpeed`, `VSDUnderSpeed`, `RotationSensor` — **present on the drive
interface but NOT required to be driven/consumed by this FB** (they are HMI/external drive signals;
see "Non-requirements" below).

Internal (private) state the VSD needs beyond the DOL's — a startup timer, a stored last-speed value,
reverse-direction edge-memory bits, and a `StartUpTime`-ms value — is the generator's own choice of
declaration; it is not part of the caller-facing interface and is not specified here.

## Non-requirements (do NOT implement — avoids gold-plating)

The target `MotorVSDIOSet` carries a broad drive-status vocabulary (`VSDReady`, `VSDOpEnable`,
`OverTempFault`, `ThermalOLFault`, `TorqueLimitOk`, `AmpsFB`, `SpeedFB`, `SpeedReached`,
`VSDOverSpeed`, `VSDUnderSpeed`, `RotationSensor`, `VSDWarning`). This FB's logic does **not** compute
a speed-error/over-under-speed comparison and does **not** gate its run permission on those members.
Do not invent handling for them beyond what REQ-V1…V8 state.

## Blindness note — what is deliberately kept behavioural (not transcribed)

To keep the test blind, this spec states required behaviour/interface only. The following were
grounded from the answer key but **intentionally not transcribed**, and are the generator's to derive:
- the network decomposition, count, ordering, and titles;
- the exact Boolean permissive expressions (REQ-C1/REQ-V6 conditions);
- the exact speed-selection move cascade and clamp mechanics (REQ-V1/V2);
- the exact `AnalogScale` `CALL` argument wiring and the scaling-range constants (REQ-V3);
- the exact startup-timer enable condition and speed-settle/direction-change re-arm logic
  (REQ-V4/V7);
- the constructed-edge array indices and Set/Reset structure.

The scaling **range** (0–100 % → 0…`MaxRPM`), the **members** involved, and the **behavioural
intent** of each REQ are given; the ladder that realises them is not.
