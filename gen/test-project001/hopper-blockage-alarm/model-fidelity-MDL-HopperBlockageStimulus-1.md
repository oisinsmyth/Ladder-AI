# Fidelity declaration — `MDL-HopperBlockageStimulus-1`

Companion to `model-fidelity-MDL-HopperBlockageStimulus-1.json`. Every entry in that file is
justified here against a network number or an interface member of the model block, not against a
paraphrase of it.

## What this is, and who wrote it

Spec §11 **M3** requires every model to carry a declaration of what it does and does not represent;
**M4** permits a vector to assert a behaviour **only** if the model claims it. Gate 4b refuses a
submission whose fidelity declarer is also a vector author, because then the list that licenses the
assertions is written by the party whose assertions it licenses.

- `declaredBy`: **`model-fidelity-declarer-1`** — an agent independent of every vector author.
- **The vector files were not read.** `conformance-vectors-b.json`, `conformance-vectors-b.md`,
  `conformance-vectors.json` and `conformance-vectors.md` were not opened, grepped or otherwise
  inspected at any point. Neither was the submission's existing `model` object. The lists below were
  derived from the block and cannot have been shaped to fit an assertion.
- **Sources read** (all under `ir/test-project001/`): `FB_HopperBlockageStim.ir` (the subject),
  `UDT_HopperBlockageStim.ir`, `iDB_HopperBlockageStim.ir`, and — for the delivery path only —
  `FC_Inputs.ir`, `FC_ControlMain.ir`, `DB_Input.ir`, `Main.ir`, `OB100.ir`, `FC_HarnessCopyLayer.ir`,
  `FB_HarnessViolationLatch.ir`, `HarnessMirror.ir`, `FB_HopperBlockageMonitor.ir`.
  `gen/test-project001/hopper-blockage-alarm/requirements.md` was read **for vocabulary only** — no
  entry below is justified by the register, and where the register's language and the block's
  behaviour disagree, the block wins, because the block is what the model is.

Throughout, `Nn` means network *n* of `FB_HopperBlockageStim.ir` unless another file is named.

## The delivery path, established once

The model does not write the monitored block's interface. It writes the **input map's test
members**, and the map and the control wiring carry the value the rest of the way:

| Model writes (N21/N22) | Input map (`FC_Inputs` N1) | Control wiring (`FC_ControlMain` N8) | Monitored block sees |
|---|---|---|---|
| `DB_Input.Test[0] := Running` | global test-enable on every line | — | (enables the whole substitution) |
| `DB_Input.Test[8] := Running AND Stim.Hopper` | `DB_Input.Hopper_Level_High` (line 21) | line 91 | `IO.HopperLevelHigh` |
| `DB_Input.Test[5] := Running AND Stim.Run` | `DB_Input.Shredder_Run_Fwd_FB` (line 18) | line 92 (`Fwd OR Rev`) | `IO.PlantRunning` |
| `RCOIL DB_Input.Test[6] := Running` | `DB_Input.Shredder_Run_Rev_FB` (line 19) | line 92 | `IO.PlantRunning` (held clear) |
| `DB_Controls.FaultReset := Running AND Stim.FaultReset` | — (not a field input) | line 93 | `IO.FaultReset` |

`Main` calls the model at **network 1**, the input map at network 2 and the control at network 3, and
no other block calls `FB_HopperBlockageStim` (grep over `ir/test-project001/`: the only `CALL` site is
`Main.ir:16`). That ordering is what makes the commanded state same-scan rather than one scan late.

## `represents` — entry by entry

1. **Three signals, commanded.** N21 writes exactly `Test[0]`, `Test[5]`, `Test[8]` and
   `DB_Controls.FaultReset`; N22 clears `Test[6]`. Nothing else in the block writes outside its own
   instance. Delivery per the table above.
2. **Same-scan presentation.** `Main` N1 → N2 → N3. Cited as a property of the caller: if the model
   were ever called from elsewhere, or `Main` reordered, this entry lapses.
3. **Six timeline shapes.** `Shape` is `Profile / 10` (N7, integer `DIV`); segment ends `T1..T4`
   accumulate `Stim.P1..P4` (N8). The per-shape drive is N11/N12 feeding N13/N14:
   - **1** — `HopperHeldHigh` (N11) and `RunHeldOn` (N12) both true ⇒ hopper high and plant running
     for the whole scenario.
   - **2** — `HopperHeldHigh` true; no run term names shape 2 ⇒ hopper high, plant **never** running.
   - **3** — `HopperClearedMidRun := (ScenarioT < T1 OR ScenarioT >= T2) AND Shape = 3` (N11) with
     `RunHeldOn` ⇒ plant running throughout, hopper low across `[T1,T2)`.
   - **4** — `HopperHeldHigh` with `RunStoppedMidRun := (ScenarioT < T1 OR ScenarioT >= T2) AND Shape = 4`
     (N12) ⇒ hopper high throughout, plant stopped across `[T1,T2)`.
   - **5** — `HopperClearedInStop := (ScenarioT < T2 OR ScenarioT >= T3) AND Shape = 5` (N11) with
     `RunStoppedRoundClear := (ScenarioT < T1 OR ScenarioT >= T4) AND Shape = 5` (N12) ⇒ plant stopped
     across `[T1,T4)`, hopper low across `[T2,T3)` — the clear falls wholly inside the stop.
   - **6** — named by no term in N11 or N12, so N13 and N14 both stay false ⇒ hopper low, plant
     stopped for the whole scenario.
4. **A clear of any commanded length ≥ one scan.** The clear interval is `[T1,T2)` (shape 3) or
   `[T2,T3)` (shape 5), i.e. exactly `Stim.P2` or `Stim.P3` (N8). Nothing in the block floors those
   values, so a clear deliberately shorter than the monitored block's debounce filter is available as
   a stimulus. The *floor* is the scan (see `doesNotRepresent` on sub-scan timing).
5. **Four reset modes.** N16 `ScenarioReset` is `ResetMode = 2` **or** (`= 1` and
   `ScenarioT ∈ [ResetAt, ResetAt + ResetPulseWidth)`) **or** (`= 3` and
   `ScenarioT ∈ [ModelThreshold, ModelThreshold + ResetPulseWidth)`); the two window ends are
   precomputed in N15. `ResetPulseWidth` is `T#500MS` (N4). `ResetMode = 0` matches no term.
6. **The clear-down as driven.** `Offset := ClearDownTime = T#5S` when `PreBoundaryDone` is clear
   (N4, N5); `InCleardown := Running AND RunT < Offset` (N9). Neither N13 nor N14 names `InCleardown`,
   so hopper and run are both low through it. `ClearDownReset := InCleardown AND RunT >= T#3S AND
   RunT < T#4S` (N4, N16) reaches `Stim.FaultReset` via N17. **Stated as drive, not as effect** — see
   the `doesNotRepresent` entry on the block's actual starting state.
7. **A run with no clear-down.** N5's second `MOVE` sets `Offset := T#0S` when `PreBoundaryDone` is
   set, so N9's `InCleardown := Running AND RunT < 0` can never be true. `PreBoundaryDone` is the
   block's only `RETAIN` member (`iDB_HopperBlockageStim.ir:30`), so this is the shape a run takes
   after an externally performed restart.
8. **A pre-boundary conditioning interval.** `InPreBoundary` (N9) requires `Stim.Precondition = 1`,
   `NOT PreBoundaryDone` and `ScenarioT < Stim.PreBoundaryHighRunning`; N13 and N14 both name it, so
   the hopper is high and the plant runs for that duration. N10 `SCOIL PreBoundaryDone :=
   InPreBoundaryWait` is the entire boundary mechanism. What this entry deliberately does **not**
   claim is the hold that follows it — see the contradiction section.
9. **The published observation window.** N18: `Stim.Armed := InScenario AND ScenarioT >= Stim.ArmAt
   AND ScenarioT < Stim.ArmUntil`.
10. **The published completion flag.** N19 `SCOIL Stim.ScenarioDone` at `ScenarioT >= Stim.EndAt` with
    the phase guards; N1 drops `Running` on it; N20 `RCOIL Stim.ScenarioDone := NOT Stim.Start AND NOT
    Running` re-arms only after the harness drops `Start`.
11. **Untruncatable run.** N1: `Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone`.
12. **Field restored after the run.** `Test[0] := Running` (N21) with `Running` false ⇒ every line of
    `FC_Inputs` falls back to its `DIn_` terminal.

## `doesNotRepresent` — entry by entry

These are the specific, foreseeable things a reader could otherwise assume this model covers.

1. **No physics.** N13 and N14 compute the two driven booleans from `Shape` and elapsed time only.
   There is no state variable representing material, level, rate or in-flight mass anywhere in the
   interface (`UDT_HopperBlockageStim.ir`, `iDB_HopperBlockageStim.ir`), and no term couples the run
   signal to the hopper signal. Shape 2 drives a permanently high hopper with a permanently stopped
   plant; nothing objects.
2. **Fitted to the specification's cases, not to the equipment (§11 M2).** The block's own `COMMENT`
   states the shape set's provenance outright: *"six shapes cover every scenario the vector set asks
   for, because the scenarios differ in WHICH of the two driven signals drops and WHEN, never in
   anything else."* A model derived from the case list inherits the case list's blind spots. This is
   the M2 failure mode named explicitly so it is visible in every result, not a criticism smuggled in.
3. **No field-side path.** N21 writes `Test[n]`; `FC_Inputs` N1 selects the test member over the
   terminal. `DI5_SHR_RunFwdFB` and `DI8_HPR_LevelHigh` are never exercised. Nothing between the
   terminal and the buffer — break, short, bounce, hardware input filter, polarity inversion — is in
   the model's path.
4. **Forward feedback only.** N22 `RCOIL DB_Input.Test[6] := Running` holds the reverse feedback clear
   for the whole run, while `FC_ControlMain:92` builds `PlantRunning` as `Fwd OR Rev`. The model can
   therefore say nothing about reverse-direction running or about the OR itself.
5. **The other nineteen inputs are suppressed, not modelled.** `Test[0]` is a **single global**
   test-enable — every line of `FC_Inputs` N1/N2 reads `NOT DB_Input.Test[0] AND DIn... OR
   DB_Input.Test[n]`. A repo-wide grep for `Test[` over `ir/test-project001/` returns exactly three
   files: `FC_Inputs.ir` (reads `DB_Input.Test`), `FC_Outputs.ir` (a *different* array,
   `DB_Output.Test`), and `FB_HopperBlockageStim.ir` (writes `Test[0]`, `[5]`, `[6]`, `[8]`). So
   `DB_Input.Test[1..4]`, `[7]` and `[9..22]` are **never written by anything** and hold their `Bool`
   default. While a run is under way, `Control_Healthy`, `Cycle_Start`, `Cycle_Stop`, `Motor_Fault`,
   the conveyor running feedbacks, all four pusher limits/pressure, `Downstream_Running` and
   `Pusher_Local_Remote` all read **false regardless of the field**. That is a real effect on the rest
   of the program and it is not a modelled plant state.
6. **The reset is the shared plant reset.** N21 writes `DB_Controls.FaultReset`. `FC_ControlMain`
   fans that one member out to the sequencer (line 23), the motor system (line 39), the pusher
   (line 59), the monitored block (line 93) **and** the physical output `DQ11_SHR_FaultReset`
   (line 84). It is a plain coil re-evaluated every scan of the run, so it also overwrites any
   externally written operator reset while a run is in progress. A vector's "operator reset" is
   therefore a **plant-wide** reset, not a monitor-private one.
7. **No CPU stop, restart or power cycle.** Nothing in the block commands one, and nothing detects
   one. `PreBoundaryDone` (N10) is set *before* any restart and records only that the pre-boundary
   phase completed; there is no member that reads back "a restart happened". `OB100.ir` does not
   touch this model or its retained flag, which is correct for the design but also means there is no
   startup evidence to read.
8. 🔴 **No held pre-boundary wait — this CONTRADICTS the block's own comments.** Detailed below.
9. **Not the block's threshold.** `Stim.ModelThreshold` is a stimulus constant, held as the instance
   start value `T#60S` (`iDB_HopperBlockageStim.ir:23`) and read only by N15/N16. No network of this
   block reads `iDB_HopperBlockageMonitor`. This is a deliberate and correct design choice (a reset
   placed by reading the thing under test would be a correlated check), but it means `ResetMode = 3`
   places its pulse at the **model's belief**, and coincidence with the block's actual crossing is an
   assumption, not a mechanism.
10. **No observation, expectation or judgement.** Confirmed by absence: `iDB_HopperBlockageMonitor`
    appears nowhere in `FB_HopperBlockageStim.ir`. `Armed` and `ScenarioDone` are published for
    others to use; the model compares nothing. (Judgement lives in `FB_HarnessViolationLatch` and
    `FC_HarnessCopyLayer`, which are *not* this model.)
11. **Not the block's starting state.** Entry 6 of `represents` is a drive statement. Whether 5 s of
    low hopper plus a 1 s reset pulse actually leaves the monitored block unalarmed with a zeroed
    accumulator depends on the **block's** debounce preset and latch semantics
    (`FB_HopperBlockageMonitor` N1, N3, N5), which this model neither sets nor checks. "The run starts
    unalarmed" is not a model-licensed precondition.
12. **Nothing below one scan.** N2 runs a single `TON(RunTimer, IN := Running, PT := T#10M)`; N3 copies
    `RunTimer.ET` into `RunT`; every phase and shape term in N6/N9/N11/N12/N16/N18/N19 is a comparison
    against that once-per-scan value. Transitions land on scan boundaries and two transitions closer
    than a scan collapse. The narrowest commandable reset is `T#500MS` (N4) in the scenario and 1 s in
    the clear-down (N4). For scale: the deployed harness program measures **~2.1 ms** per OB1 scan
    (CLAUDE.md measured facts, 2026-08-14 — a property of that program, not of the controller).
13. **Nothing past ten minutes.** `RunTimer`'s `PT` is `T#10M` (N2). A `TON`'s `ET` stops advancing at
    `PT`, so `RunT` (N3) saturates and with it `ScenarioT` (N6) and every comparison downstream. A run
    whose clear-down plus `EndAt` exceeds ten minutes never satisfies N19 and never completes.
14. **No vector validation.** N7 is a bare `DIV` by 10. A `Profile` of 0 (the instance default —
    `iDB_HopperBlockageStim.ir` leaves every command member unwritten except `ModelThreshold`) or 70
    yields a `Shape` that matches no term in N11/N12, so the model drives hopper low and plant stopped
    and publishes `ScenarioDone` as soon as `ScenarioT >= EndAt`. An unset or out-of-range command is
    a **silent idle run**, indistinguishable in the driven signals from a deliberate shape 6.
15. **One clear and one stop per run.** Each of N11's and N12's shape terms is the complement of a
    single interval. There is no construct in the block that repeats a segment, so a second high
    episode, a second clear, or a stop/start pair repeated within one scenario is not representable.
16. **The window cannot outlast the scenario.** N18 ANDs `Armed` with `InScenario`, and `InScenario`
    (N9) requires `ScenarioT < Stim.EndAt`. An `ArmUntil` beyond `EndAt` is silently truncated; an
    `ArmAt` before the scenario begins does not open the window early, because `InScenario` is false
    during the clear-down and the pre-boundary phase.
17. **Every clear-down run contains a reset.** N17 `Stim.FaultReset := ClearDownReset OR ScenarioReset
    AND InScenario`. `ClearDownReset` (N16) does not consult `ResetMode` at all, so a `ResetMode = 0`
    run still asserts the shared reset from 3 s to 4 s of the run. And `ResetMode = 2` is not
    "edgeless": the reset is true 3–4 s, **false 4–5 s** (the clear-down tail), then true from the
    scenario's first scan — exactly **one** rising edge, at `ScenarioT = 0`, with nothing after it.
    Whether that satisfies a "held reset presents no edge inside the scenario" reading is a vector
    author's question; the mechanical fact is one edge at the boundary.

## The contradiction, in full

**Claim under examination** — the block `COMMENT` and the `InPreBoundaryWait` member comment both say
the model *"is holding still, waiting for the CPU to be stopped and restarted"*, with the plant held
stopped so the monitored block's window *"does not drift while it waits"*.

**What the logic does.**

- N9: `InPreBoundaryWait := Running AND NOT InCleardown AND NOT PreBoundaryDone AND
  Stim.Precondition = 1 AND ScenarioT >= Stim.PreBoundaryHighRunning`.
- N10: `SCOIL PreBoundaryDone := InPreBoundaryWait`.

The state that is supposed to wait sets, in the same scan, the very flag its own guard forbids. On
the next scan `InPreBoundaryWait` is false. **The wait is one scan wide** — about 2 ms on the deployed
program — and cannot be waited in.

**What happens on that next scan.**

1. N5 flips `Offset` from `ClearDownTime` (`T#5S`) to `T#0S`, because `PreBoundaryDone` is now set.
2. N6 recomputes `ScenarioT := RunT - Offset`, so `ScenarioT` **jumps forward by 5 s** in one scan.
3. N9's `InPreBoundary`/`InPreBoundaryWait` are both false (`NOT PreBoundaryDone` fails), so N19's
   phase guards are open. Two cases follow:
   - **`EndAt <= PreBoundaryHighRunning + 5 s`** (the ordinary case — `EndAt` is a scenario length,
     `PreBoundaryHighRunning` is sized to spend a 60 s persistence window): N19 latches
     `Stim.ScenarioDone` immediately, N1 drops `Running`, and **N20's `RCOIL PreBoundaryDone :=
     Stim.ScenarioDone` clears the retained flag in the same scan.** The boundary marker is destroyed
     one scan after it was created; a later restart produces a completely fresh run *with* a
     clear-down, whose reset pulse clears the very latched alarm the boundary scenario exists to
     carry across.
   - **`EndAt` larger:** `InScenario` goes true and the model resumes driving the shape from
     `ScenarioT = PreBoundaryHighRunning + 5 s`, i.e. from the middle of a timeline whose `T1..T4`
     are measured from zero. The monitored block is driven by an unintended part of the scenario, and
     any `Armed` window and any commanded reset are placed against that shifted clock.

**Root cause.** The pre-boundary phase and the scenario share one clock (`ScenarioT`, N6, derived from
the free-running `RunTimer`, N2). The block contains **no mechanism to zero its own scenario clock at
the boundary**. Only the CPU restart does that, by clearing the non-retentive `Running` and
`RunTimer`. So `Precondition = 1` is not a self-contained capability: it delivers its intended
scenario only if the CPU stop lands within the single scan after the pre-boundary phase ends, and
delivers either a destroyed boundary marker or a mis-phased scenario otherwise.

**What is therefore claimed and not claimed.** `represents` entry 8 claims only the conditioning
interval and the latching of the retentive flag; `represents` entry 7 claims the no-clear-down run
shape that a restart produces. Neither claims a stable pre-boundary hold, and no vector may assert
one on this model's authority.

## `validatedAgainstPlantData`: `false`

Answered honestly, and the absence is recorded because §11 M5 says it must be. There is no plant data
for this model to be validated against: no level trace, no blockage event log, no sensor-chatter
statistics, no commissioning record — nothing of the kind exists anywhere in this repository for the
hopper, and `test-project001` is a de-identified sandbox project whose deployment target is a bench
rig with outputs physically incapable of actuating. The model's timeline shapes come from the
specification's case list (see `doesNotRepresent` entry 2), and its two driven signals are commanded
booleans with no physical referent (entry 1). `false` is not a caveat here — it is the whole epistemic
status of the model.

## Declared gap: no scan-time budget (§11 M6)

M3 requires a **declared scan-time budget** as part of the fidelity declaration. The `model` object's
field shape has nowhere to put one, and no measurement of this block in isolation exists. Stated as a
gap rather than filled with an estimate: the model is one FB, called once from `Main` N1, 22 LAD
networks, one `TON`, no loops or block calls; the only measured figure is the **whole** deployed
harness program at ~2.1 ms per OB1 scan, which contains this block but does not attribute it.

## Not decidable from the corpus

**Does the harness command mirror survive a CPU stop/restart?** `Stim.Start` is copied from
`HX_HBA_Start` at `%M1009.0`, and the vector registers from `%MW1012` upward, by
`FC_HarnessCopyLayer` N3–N5 every scan. Marker retentivity is a CPU hardware-configuration property
and appears in no `.ir` file. If those markers come back cleared after a restart, a post-restart run
cannot re-latch `Running` until the harness rewrites them; if they survive, it re-latches
immediately. This matters to the `Precondition = 1` path and is recorded as **unknown** rather than
resolved in either direction.

## What a reader should take from this

The model is a competent, well-documented **signal-timeline generator** for three booleans, with a
clean shape/duration parameterisation and an honest separation between commanding and observing. It
is not a plant model in any physical sense, it suppresses nineteen other inputs while it runs, its
"operator reset" is the plant-wide reset, and its pre-boundary/restart path does not do what its
comments say it does. Assertions that rest on the first of those are licensed; assertions that rest
on any of the rest are not.
