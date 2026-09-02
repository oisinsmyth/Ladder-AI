# Fidelity declaration — `MDL-HopperBlockageStimulus-1`

Companion to `model-fidelity-MDL-HopperBlockageStimulus-1.json`. Every entry in that file is
justified here against a network number or an interface member of the model block, not against a
paraphrase of it.

## What this is, and who wrote it

Spec §11 **M3** requires every model to carry a declaration of what it does and does not represent;
**M4** permits a vector to assert a behaviour **only** if the model claims it. Gate 4b refuses a
submission whose fidelity declarer is also a vector author, because then the list that licenses the
assertions is written by the party whose assertions it licenses. **M1** bars the block's own author
for the same reason.

**This is the SECOND declaration of this model, and it is a re-derivation, not an edit.** The block
changed after the first declaration was written, and the first declaration no longer describes it.
Every entry below — including the ones carried over — was re-verified against the current IR and is
cited to a network in it. Nothing was inherited on the strength of already being written.

- **The vector files were not read.** `conformance-vectors*.json`, `conformance-vectors*.md`,
  `assertion-enumeration.yaml`, every wave result (`first-wave-result.json`,
  `wave-result-after-fixes.json`, `hba-wave-result*.json`), the submission document and its `model`
  object, and `agent-tasks/hopper-wave-deploy/` were not opened, grepped or listed at any point, by
  this declarer or by the sub-agent that performed the IR read. No embargoed content was seen.
- **The block author's account of the change was deliberately withheld from this declarer**, and the
  change was derived from the IR and from `git diff` of the `.ir` files alone. The author's commit
  messages were read only as *claims to be checked against the diff*, and where a claim and the diff
  disagree the diff is recorded below.
- **Sources read** (all under `ir/test-project001/`): `FB_HopperBlockageStim.ir` (the subject),
  `UDT_HopperBlockageStim.ir`, `iDB_HopperBlockageStim.ir`, and — for the delivery path only —
  `FC_Inputs.ir`, `FC_Outputs.ir`, `FC_ControlMain.ir`, `DB_Input.ir`, `DB_Controls.ir`, `Main.ir`,
  `OB100.ir`, `FC_HarnessCopyLayer.ir`, `FB_HarnessViolationLatch.ir`, `HarnessMirror.ir`,
  `FB_HopperBlockageMonitor.ir`, `iDB_HopperBlockageMonitor.ir`, `UDT_HopperBlockageIO.ir`.
  Per CLAUDE.md hard rule 8 the IR itself was read by a `lad-reader` sub-agent — a party that is
  neither the block author nor any vector author, and that was held to the same embargo and confirmed
  compliance with it. The judgement in this document is the declarer's.
  `requirements.md` was read **for vocabulary only** — no entry below is justified by the register,
  and where the register's language and the block's behaviour disagree, the block wins, because the
  block is what the model is.

Throughout, `Nn` means network *n* of `FB_HopperBlockageStim.ir` unless another file is named.

## What changed in the block, stated before anything rests on it

Derived from `git diff HEAD` (the change is **uncommitted** in the working tree; `+27 −6`), and
confirmed against `converter digest`, which reports **24 networks** where the first declaration
described 22.

- **Two networks appended: N23 `"Trailing Clear-Down Pending"` and N24 `"Trailing Clear-Down
  Window"`**, plus five new non-retentive statics (`CleardownPending`, `InTrailingCleardown`,
  `TrailingReset`, `TrailingT`, `TrailingTimer`). They add a **second clear-down that runs AFTER the
  harness drops `Start`**.
- **N21 changed in place:** `Test[0] := Running` became `Running OR InTrailingCleardown`, and
  `DB_Controls.FaultReset := Running AND Stim.FaultReset` became
  `TrailingReset OR Running AND Stim.FaultReset`.
- **N22 changed in place:** `RCOIL Test[6] := Running` became `Running OR InTrailingCleardown`.
- **N1–N20 were not renumbered and not edited**, in this change or the one before it. Both changes
  are strict appends plus in-place edits to N21/N22. **A citation to N1–N20 by number is therefore
  still sound**; a citation to N21/N22 has the right number and the wrong rung.
- **No UDT member and no iDB start value changed.** `UDT_HopperBlockageStim.ir` and
  `iDB_HopperBlockageStim.ir` have no uncommitted changes.

Two entries of the first declaration are **falsified** by this change and are corrected below:
`represents` 12 (field restored at the end of a run) and `doesNotRepresent` 17 (`ResetMode = 2`
presents exactly one rising edge "with nothing after it"). One further entry, `doesNotRepresent` 12,
was **stale for an unrelated reason** — it quoted a scan time that has since been retracted.

## 🔴 The load-bearing limitation, stated before anything else

**Read this before reading the lists, because it conditions every entry in them.** Two facts compose,
and together they bound what *any* assertion resting on this model can mean.

1. **The stimulus is not observable — at all, anywhere.** `Stim.Hopper`, `Stim.Run` and
   `Stim.FaultReset` reach **no mirror register**, and `Stim.Hopper`/`Stim.Run` are read by no block
   outside this one. `FC_HarnessCopyLayer` publishes the model's *phase* (`Armed`, `ScenarioDone`) and
   the block-under-test's *response*, and nothing of what was applied. A wave can confirm that the
   model believed it was in the armed part of a scenario, and can see how the monitored block
   responded — **it can never witness that the hopper was actually driven high.** Detail at
   `doesNotRepresent` 12.
2. **There is no committed SimaticML export of this model to check the IR against.**
   `simatic-ml/test-project001/` holds 26 files and none is `FB_HopperBlockageStim`,
   `UDT_HopperBlockageStim` or `iDB_HopperBlockageStim`, so `converter drift-check` has nothing to
   compare. **Drift between the object this document describes and the object in the controller can be
   neither confirmed nor excluded.** Detail under "Declared gaps".

**Composed: every claim in this document is an inference from IR that cannot be shown to describe the
running object, about a stimulus that is never observed.** So a `PASS` against this model means *"the
monitored block behaved as expected, given what we believe was applied to it"* — and the belief is
instrumented nowhere. Per the test-environment contract a declaration describing a drifted object
makes every M4 pass a set-difference against the wrong set, and **the verdict for that is `STALE` —
never `FAIL`, never `REFUSED`**, because neither the block nor the vector author would have done
anything wrong.

**The cheapest repair is (1): mirror the three driven booleans.** They are already `Bool` statics in
an iDB the copy layer writes to every scan, and three spare bits would convert this document's central
weakness from an unfalsifiable inference into an observation. This declarer notes it and does not
propose the edit — that is a `lad-coder` matter and a change to the thing under test.

## The delivery path, established once

The model does not write the monitored block's interface. It writes the **input map's test
members** and the **shared operator-reset member**, and the map and the control wiring carry the
value the rest of the way:

| Model writes (N21/N22) | Input map (`FC_Inputs` N1) | Control wiring (`FC_ControlMain` N8) | Monitored block sees |
|---|---|---|---|
| `DB_Input.Test[0] := Running OR InTrailingCleardown` | global test-enable on every line | — | (enables the whole substitution) |
| `DB_Input.Test[8] := Running AND Stim.Hopper` | `DB_Input.Hopper_Level_High` (line 21) | line 91 | `IO.HopperLevelHigh` |
| `DB_Input.Test[5] := Running AND Stim.Run` | `DB_Input.Shredder_Run_Fwd_FB` (line 18) | line 92 (`Fwd OR Rev`) | `IO.PlantRunning` |
| `RCOIL DB_Input.Test[6] := Running OR InTrailingCleardown` | `DB_Input.Shredder_Run_Rev_FB` (line 19) | line 92 | `IO.PlantRunning` (held clear) |
| `DB_Controls.FaultReset := TrailingReset OR Running AND Stim.FaultReset` | — (not a field input) | line 93 | `IO.FaultReset` |

`Main` calls the model at **network 1**, the input map at network 2 and the control at network 3 —
and `FC_ControlMain` **calls the monitored block itself at its own network 9**, inside that third
call. No other block calls `FB_HopperBlockageStim` (`Main.ir:16` is the only `CALL` site). That
ordering is what makes the commanded state same-scan rather than one scan late.

In the other direction there **is** a scan of latency: `FC_HarnessCopyLayer` runs at `Main` N7,
*after* the model, so a command register written by the client reaches the model on the **following**
scan. Those copies are unconditional every scan, which has a consequence of its own — see
`doesNotRepresent` 20.

## `represents` — entry by entry

1. **Three signals, commanded; a fourth held clear.** N21 writes exactly `Test[0]`, `Test[5]`,
   `Test[8]` and `DB_Controls.FaultReset`; N22 holds `Test[6]` clear. N23 and N24 write only the
   block's own statics. Nothing else in the block writes outside its own instance. Delivery per the
   table above. *(Re-verified; the coil expressions for `Test[0]`, `Test[6]` and `FaultReset` changed
   — see "What changed".)*
2. **Same-scan presentation.** `Main` N1 → N2 → N3, with the monitored block called at
   `FC_ControlMain` N9. Cited as a property of the caller: if the model were ever called from
   elsewhere, or `Main` reordered, this entry lapses. *(Re-verified unchanged.)*
3. **Six timeline shapes.** `Shape` is `Profile / 10` (N7, integer `DIV`); segment ends `T1..T4`
   accumulate `Stim.P1..P4` (N8, recomputed from scratch each scan, so no accumulation drift). The
   per-shape drive is N11/N12 feeding N13/N14:
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

   *(Re-verified unchanged.)*
4. **A clear of any commanded length ≥ one scan.** The clear interval is `[T1,T2)` (shape 3) or
   `[T2,T3)` (shape 5), i.e. exactly `Stim.P2` or `Stim.P3` (N8). Nothing in the block floors those
   values, so a clear deliberately shorter than the monitored block's debounce filter is available as
   a stimulus. The *floor* is the scan (see `doesNotRepresent` 14). *(Re-verified unchanged.)*
5. **Four reset modes.** N16 `ScenarioReset` is `ResetMode = 2` **or** (`= 1` and
   `ScenarioT ∈ [ResetAt, ResetAt + ResetPulseWidth)`) **or** (`= 3` and
   `ScenarioT ∈ [ModelThreshold, ModelThreshold + ResetPulseWidth)`); the two window ends are
   precomputed in N15. `ResetPulseWidth` is `T#500MS` (N4). `ResetMode = 0` matches no term.
   *(Re-verified unchanged. What the modes deliver to the plant is no longer the whole story — see
   entry 13 and `doesNotRepresent` 19.)*
6. **The leading clear-down as driven.** `Offset := ClearDownTime = T#5S` when `PreBoundaryDone` is
   clear (N4, N5); `InCleardown := Running AND RunT < Offset` (N9). Neither N13 nor N14 names
   `InCleardown`, so hopper and run are both low through it. `ClearDownReset := InCleardown AND
   RunT >= T#3S AND RunT < T#4S` (N4, N16) reaches `Stim.FaultReset` via N17 and the plant reset via
   N21. **Stated as drive, not as effect** — see `doesNotRepresent` 13. *(Re-verified unchanged.)*
7. **A run with no clear-down.** N5's second `MOVE` sets `Offset := T#0S` when `PreBoundaryDone` is
   set, so N9's `InCleardown := Running AND RunT < 0` can never be true. `PreBoundaryDone` is the
   block's only `RETAIN` member (`iDB_HopperBlockageStim.ir:30`), so this is the shape a run takes
   after an externally performed restart. *(Re-verified unchanged.)*
8. **A pre-boundary conditioning interval.** `InPreBoundary` (N9) requires `Stim.Precondition = 1`,
   `NOT PreBoundaryDone` and `ScenarioT < Stim.PreBoundaryHighRunning`; N13 and N14 both name it, so
   the hopper is high and the plant runs for that duration. N10 `SCOIL PreBoundaryDone :=
   InPreBoundaryWait` is the entire boundary mechanism. What this entry deliberately does **not**
   claim is the hold that follows it — see the contradiction section. *(Re-verified unchanged.)*
9. **The published observation window.** N18: `Stim.Armed := InScenario AND ScenarioT >= Stim.ArmAt
   AND ScenarioT < Stim.ArmUntil`. *(Re-verified unchanged.)*
10. **The published completion flag.** N19 `SCOIL Stim.ScenarioDone` at `ScenarioT >= Stim.EndAt` with
    the phase guards; N1 drops `Running` on it the following scan; N20 `RCOIL Stim.ScenarioDone :=
    NOT Stim.Start AND NOT Running` re-arms only after the harness drops `Start`.
    *(Re-verified unchanged.)*
11. **Untruncatable run.** N1: `Running := (Stim.Start OR Running) AND NOT Stim.ScenarioDone`. The
    harness dropping `Start` mid-run changes nothing: the scenario runs to `EndAt`, the reset pulses
    still fire, injection stays engaged. *(Re-verified unchanged.)*
12. 🔴 **Field restoration, in two disjoint windows — and NOT at the end of a run.** *(CHANGED. The
    first declaration said `Test[0] := Running` with `Running` false ⇒ the field is restored when the
    run ends. That is no longer what the block does, and the correction cuts both ways.)* With
    `Test[0] := Running OR InTrailingCleardown` (N21) and `InTrailingCleardown := CleardownPending
    AND NOT Stim.Start AND NOT Running` (N24), the field is connected in exactly two windows:
    - **From the scan after `ScenarioDone` latches until the harness drops `Start`.** `Running` is
      false and `InTrailingCleardown` is false (it requires `NOT Stim.Start`), so `Test[0]` is **low**
      and `FC_Inputs` takes `Hopper_Level_High` and `Shredder_Run_Fwd_FB` from `DI8_HPR_LevelHigh` and
      `DI5_SHR_RunFwdFB` again — **while the harness is reading the result.** This window is unbounded;
      it lasts as long as the harness holds `Start`.
    - **After the trailing clear-down completes**, when `CleardownPending` is cleared by
      `TrailingTimer.Q` (N23).

    Between those two, injection is re-engaged for the trailing clear-down. What the model represents
    is therefore *"the field is connected outside a run"* — **not** *"the field is connected as soon
    as the scenario ends"*, and not *"injection is held for as long as it matters"*. Whether the first
    window is material depends on what `DI5`/`DI8` read on the rig, which no reading of the IR
    establishes — see "Not decidable from the corpus".
13. 🔴 **A trailing clear-down, after the harness acknowledges.** *(NEW — N23, N24, and the changed
    N21/N22.)* When a run reaches `ScenarioDone`, N23 latches `CleardownPending`. When the harness
    then drops `Start`, N24 runs `TrailingTimer` for `ClearDownTime` (`T#5S`, N4) and drives, for that
    duration: `Test[0]` high (N21), `Test[6]` held clear (N22), `Test[5]` and `Test[8]` low — because
    both are ANDed with `Running`, which is false — and `TrailingReset` true across
    `TrailingT ∈ [T#3S, T#4S)`, which N21 puts onto `DB_Controls.FaultReset`. So the model represents
    **a second commanded interval of hopper-low, plant-stopped, with a 1 s plant-wide reset pulse in
    it, occurring after the result has been read.** `TrailingTimer.Q` then clears `CleardownPending`
    (N23), ending the phase. Stated as drive, not as effect, on the same grounds as entry 6.

## `doesNotRepresent` — entry by entry

These are the specific, foreseeable things a reader could otherwise assume this model covers.

1. **No physics.** N13 and N14 compute the two driven booleans from `Shape` and elapsed time only.
   There is no state variable representing material, level, rate or in-flight mass anywhere in the
   interface (`UDT_HopperBlockageStim.ir`, `iDB_HopperBlockageStim.ir`), and no term couples the run
   signal to the hopper signal. Shape 2 drives a permanently high hopper with a permanently stopped
   plant; nothing objects. *(Re-verified unchanged.)*
2. **Fitted to the specification's cases, not to the equipment (§11 M2).** The block's own `COMMENT`
   states the shape set's provenance outright: *"six shapes cover every scenario the vector set asks
   for, because the scenarios differ in WHICH of the two driven signals drops and WHEN, never in
   anything else."* A model derived from the case list inherits the case list's blind spots. This is
   the M2 failure mode named explicitly so it is visible in every result, not a criticism smuggled in.
   *(Re-verified: the sentence is still in the header, and the header was edited by this very change.)*
3. **No field-side path.** N21 writes `Test[n]`; `FC_Inputs` N1 selects the test member over the
   terminal. `DI5_SHR_RunFwdFB` and `DI8_HPR_LevelHigh` are never exercised while `Test[0]` is high.
   Nothing between the terminal and the buffer — break, short, bounce, hardware input filter, polarity
   inversion — is in the model's path. *(Re-verified unchanged. Note that the map's test branch is
   `OR AlwaysTrue AND Test[n]` and does **not** include `Test[0]`: a set `Test[n]` injects even with
   the test flag low. It does not bite for this model, whose `Test[5]`/`Test[8]` are ANDed with
   `Running`, but the map is not the gate the N21 comment describes.)*
4. **Forward feedback only.** N22 holds the reverse feedback's test member clear for the whole run
   **and for the trailing clear-down**, while `FC_ControlMain:92` builds `PlantRunning` as
   `Fwd OR Rev`. The model can therefore say nothing about reverse-direction running or about the OR
   itself. *(Re-verified; the rung now also covers `InTrailingCleardown`.)*
5. **The other nineteen inputs are suppressed, not modelled.** `Test[0]` is a **single global**
   test-enable — every line of `FC_Inputs` N1/N2 reads `NOT DB_Input.Test[0] AND DIn... OR
   DB_Input.Test[n]`, and there are **22 such rungs**, consuming `Test[1]..Test[22]`. The model drives
   **three** of them. The other **nineteen** are written by nothing in the corpus, so they hold their
   `Bool` default and each mapped member is forced **false** for the duration: `Control_Healthy`,
   `SpareDI[1]`, `Cycle_Start`, `Cycle_Stop`, `Motor_Fault`, `Infeed_Conv_Running`,
   `Discharge_Conv_Running`, `Pusher_PowerPack_Running`, `Pusher_Home_Limit`,
   `Pusher_Full_Travel_Limit`, `Pusher_High_Pressure`, `Downstream_Running`, `Pusher_Local_Remote`,
   `SpareDI[2..7]`. That is a real effect on the rest of the program and it is not a modelled plant
   state. Two consequences worth naming rather than leaving to be discovered:
   - **`Cycle_Stop` inverts.** The map negates the terminal (`NOT DI4_SYS_CycleStop`) but does **not**
     negate the test branch, so in test mode `Cycle_Stop` reads false — *"not commanding stop"* — which
     is the opposite of the fail-safe reading of an unwired NC button.
   - **The suppression reaches a terminal.** `DB_Output.In_Cycle` (`FC_ControlMain:83`) is an OR of
     five of these forced members, and drives `DQ8_SYS_InCycle`.
6. 🔴 **The reset is the shared plant reset, and the model writes it UNCONDITIONALLY, always.** N21's
   fourth rung is a plain coil with no enable: `DB_Controls.FaultReset := TrailingReset OR Running AND
   Stim.FaultReset` is evaluated **every scan, in every state, including idle**, where it writes
   false. A repo-wide grep finds **no other writer of `DB_Controls.FaultReset` anywhere in the PLC
   program** — because it is the HMI surface (`DB_Controls.ir`: *"Operator/HMI command surface
   (C-306). Written by HMI, consumed by equipment FBs and the sequencer."*). The model runs at `Main`
   N1; all five consumers are in `FC_ControlMain` at `Main` N3:
   `iDB_ShredderSequencer.IO.FaultReset` (line 23), `iDB_MotorFwdRevSystem_Shredder.IO.FaultReset`
   (line 39), `iDB_PusherControl.IO.FaultReset` (line 59), `DB_Output.Motor_Fault_Reset` (line 84) and
   `iDB_HopperBlockageMonitor.IO.FaultReset` (line 93). So:
   - An operator/HMI/Modbus/watch-table write to `DB_Controls.FaultReset` is **destroyed before any
     consumer reads it**, for as long as this block is in the cyclic program — not merely "while a run
     is in progress", which is what the first declaration said and is an understatement.
   - Every commanded reset pulse also resets the shredder sequencer, the motor FB and the pusher FB.
   - `DB_Output.Motor_Fault_Reset` reaches `DQ11_SHR_FaultReset` (`FC_Outputs:25`), so **every reset
     pulse energises a physical relay output.**

   A vector's "operator reset" is therefore a **plant-wide** reset that also lands on a terminal, not
   a monitor-private one. **The block's own header denies this in as many words — see entry 22, which
   is the reason this entry cannot be left to be discovered by reading the block.**
7. **No CPU stop, restart or power cycle.** Nothing in the block commands one, and nothing detects
   one. `PreBoundaryDone` (N10) is set *before* any restart and records only that the pre-boundary
   phase completed; there is no member that reads back "a restart happened". `OB100.ir` names neither
   this model's iDB nor the monitor's nor `DB_Input.Test[]` nor `DB_Controls.FaultReset`, which is
   correct for the design but also means there is no startup evidence to read.
   *(Re-verified unchanged.)*
8. 🔴 **No held pre-boundary wait — this CONTRADICTS the block's own comments.** Detailed below.
   *(Re-verified unchanged: N5, N6, N9, N10 and N20 are all untouched by the change.)*
9. 🔴 **No boundary-aware suppression of the trailing clear-down — this CONTRADICTS N23's own
   comment.** *(NEW.)* Detailed below, as the second limb of the contradiction section.
10. **Not the block's threshold.** `Stim.ModelThreshold` is a stimulus constant, held as the instance
    start value `T#60S` (`iDB_HopperBlockageStim.ir:23`) and read only by N15/N16. No network of this
    block reads `iDB_HopperBlockageMonitor`. This is a deliberate and correct design choice (a reset
    placed by reading the thing under test would be a correlated check), but it means `ResetMode = 3`
    places its pulse at the **model's belief**. That belief currently agrees with
    `iDB_HopperBlockageMonitor.IO.BlockedTimeThreshold = T#60S` — **by coincidence of two
    independently hand-set start values, with no check anywhere in the corpus.** Retune the monitor
    and `ResetMode = 3` fires at the wrong instant, silently. *(Re-verified, and the monitor's actual
    value is now named.)*
11. **No observation, expectation or judgement.** Confirmed by absence: `iDB_HopperBlockageMonitor`
    appears nowhere in `FB_HopperBlockageStim.ir`. `Armed` and `ScenarioDone` are published for
    others to use; the model compares nothing. (Judgement lives in `FB_HarnessViolationLatch` and
    `FC_HarnessCopyLayer`, which are *not* this model.) *(Re-verified unchanged.)*
12. 🔴 **The commanded plant is NOT observable at the boundary.** *(NEW.)* `FC_HarnessCopyLayer`
    N7/N8 publishes to `HarnessMirror`: the monitored block's `HopperBlockedAlarm` (`HX_HBA_R000`)
    and `HopperBlockStopReq` (`HX_HBA_R001`), the model's `Armed` (`R002`) and `ScenarioDone`
    (`R003`), four violation latches (`R004`–`R007`) and two phase-armed response latches
    (`L008`/`L009`). **`Stim.Hopper`, `Stim.Run` and `Stim.FaultReset` are mirrored nowhere** — and
    `Stim.Hopper`/`Stim.Run` are read by no block outside this one at all. A harness cannot confirm
    from the boundary that the hopper was actually driven high, only that the model believed it was in
    the armed part of a scenario. **The stimulus is unwitnessed; only its phase and the block-under-
    test's response are witnessed.** A related timing fact: `ScenarioDone` is cleared by N20 in the
    same scan `Start` falls, and `L008`/`L009` are reset by the copy layer on `NOT HX_HBA_Start` in
    that same scan — so `R003`, `L008` and `L009` are readable **only while `Start` is high**.
13. **Not the block's starting state.** `represents` 6 and 13 are drive statements. Whether 5 s of
    low hopper plus a 1 s reset pulse actually leaves the monitored block unalarmed with a zeroed
    accumulator depends on the **block's** debounce preset and latch semantics
    (`FB_HopperBlockageMonitor` N1, N3, N5), which this model neither sets nor checks. This now
    applies to **both** clear-downs, the leading one and the trailing one, and neither is verified.
    "The run starts unalarmed" is not a model-licensed precondition. Note also that the coupling N4's
    comment relies on — `T#5S` being longer than the monitor's `ClearDebounceTime` (`T#2S`) — is
    **unenforced**: nothing links the two numbers, and raising `ClearDebounceTime` above `T#3S` would
    put the reset pulse before the filter expires, silently. *(Re-verified and extended.)*
14. **Nothing below one scan, and one scan on THIS program is ~2.26 ms.** N2 runs a single
    `TON(RunTimer, IN := Running, PT := T#10M)`; N3 copies `RunTimer.ET` into `RunT`; every phase and
    shape term in N6/N9/N11/N12/N16/N18/N19 is a comparison against that once-per-scan value.
    Transitions land on scan boundaries and two commanded transitions closer together than one scan
    collapse into one. The narrowest commandable reset is `T#500MS` (N4) in the scenario and 1 s in
    either clear-down (N4) — so a scenario reset pulse is **~221 scans** wide and a clear-down pulse
    ~442.

    🔴 **THE FIGURE, WITH ITS PROVENANCE, BECAUSE THE PROVENANCE IS THE WHOLE POINT.** The program
    deployed on this rig — **`GenProject1`, stamp `16#41E5DA59`** — measures **~2.26 ms/scan**,
    measured directly off the rig as scan-counter deltas against wall clock, three times:
    **2.2629 ms** (79.906 s / 35,312 scans), **2.2733 ms**, and **2.2041 ms** (fastest observed).
    **This is a property of THAT program and it does not transfer**, which is exactly the error this
    entry has now been wrong about twice:
    - The first declaration quoted **~2.1 ms**, sourced to CLAUDE.md. That figure belongs to a *bare
      harness program* deployed 2026-08-14 and not on the rig since — so it was **approximately right
      by accident and wrong by provenance.**
    - This declarer then replaced it with **23.80 / 24.931 ms** from
      `src/harness/Harness.Wire/WireTiming.cs`. That is a faithful record of the **JOB9004 plant +
      harness** program, which is **not what is on this rig** — so it was **wrong by an order of
      magnitude, in the opposite direction.** Corrected here.

    `docs/notes/live-project-readiness.md` states the rule that both errors broke: *"Every row in this
    table is a property of the PROGRAM, not of the controller."* **That table currently carries no row
    for `GenProject1` as deployed** — the three figures above are recorded here and are not yet in it.
    Do not quote any of them without the program they were measured against.

    **What this does and does not move.** It makes the sub-scan floor ~11× *finer* than this declarer
    previously wrote, so this limitation is weaker, not stronger — but it is still real, and every
    conclusion elsewhere in this document survives it unchanged. The one derived bound that consumes
    the scan is the timer floor, `EffectiveTimerFloor = max(500 ms, k × scan)` with `k ≈ 5`
    (`TimeCompression.cs`): at 2.26 ms the scan term is ~11 ms, so **the absolute 500 ms floor binds**
    — as it also did at 24.9 ms (~125 ms). That conclusion is insensitive to which figure is right,
    and `k ≈ 5` is X-D's own number and has never been measured.
15. **Nothing past ten minutes.** `RunTimer`'s `PT` is the literal `T#10M` (N2) — not an interface
    member, so the harness cannot change it. A `TON`'s `ET` stops advancing at `PT`, so `RunT` (N3)
    saturates and with it `ScenarioT` (N6) and every comparison downstream. A run whose clear-down plus
    `EndAt` exceeds ten minutes never satisfies N19, never publishes `ScenarioDone`, and never ends —
    leaving `Test[0]` high indefinitely. `RunTimer.Q`, which is the one signal that would detect this,
    is **read nowhere in the block**. *(Re-verified unchanged.)*
16. **No vector validation.** N7 is a bare `DIV` by 10. A `Profile` of 0 (the instance default —
    `iDB_HopperBlockageStim.ir` leaves every command member unwritten except `ModelThreshold`) or 70
    yields a `Shape` that matches no term in N11/N12, so the model drives hopper low and plant stopped
    and publishes `ScenarioDone` as soon as `ScenarioT >= EndAt`. An unset or out-of-range command is
    a **silent idle run**, indistinguishable in the driven signals from a deliberate shape 6. The
    defaults compound: `ArmAt = ArmUntil = T#0S` means `Armed` (N18) is **never true**, so a defaulted
    run completes having observed nothing, and reports success in doing so. *(Re-verified and
    extended.)*
17. **One clear and one stop per run.** Each of N11's and N12's shape terms is the complement of a
    single interval. There is no construct in the block that repeats a segment, so a second high
    episode, a second clear, or a stop/start pair repeated within one scenario is not representable.
    *(Re-verified unchanged.)*
18. **The window cannot outlast the scenario.** N18 ANDs `Armed` with `InScenario`, and `InScenario`
    (N9) requires `ScenarioT < Stim.EndAt`. An `ArmUntil` beyond `EndAt` is silently truncated; an
    `ArmAt` before the scenario begins does not open the window early, because `InScenario` is false
    during the clear-down and the pre-boundary phase. *(Re-verified unchanged.)*
19. 🔴 **A reset-free run does not exist, and `ResetMode = 2` no longer presents a single edge.**
    *(CHANGED.)* N17 is `Stim.FaultReset := ClearDownReset OR ScenarioReset AND InScenario`, and
    `ClearDownReset` (N16) does not consult `ResetMode` at all, so a `ResetMode = 0` run still asserts
    the shared reset from 3 s to 4 s of the run. The first declaration then said `ResetMode = 2`
    presents *"exactly **one** rising edge, at `ScenarioT = 0`, with nothing after it."* **The
    trailing clear-down falsifies the second half.** For `ResetMode = 2` the plant reset
    (`DB_Controls.FaultReset`, N21) now goes: true 3–4 s (leading clear-down), false 4–5 s, true from
    the scenario's first scan to `ScenarioDone`, **false through the whole unbounded window in which
    the harness still holds `Start`**, then — once `Start` falls — **true again for 3–4 s into the
    trailing clear-down**. That is **two** rising edges, the second of them after the result was read,
    and it is delivered plant-wide (entry 6). Note also that `Stim.FaultReset` — which
    `FB_HarnessViolationLatch` watches (N1–N3) — does **not** carry the trailing pulse: N17 is unchanged
    and only N21 ORs in `TrailingReset`. **The violation instrument and the monitored block are
    therefore watching two different reset signals**, identical during a run and divergent after it.
20. **The command set is live, not latched at the start of a run.** *(NEW.)* `FC_HarnessCopyLayer`
    N3–N5 copies the vector registers into the model's iDB with **unconditional `MOVE`s every scan**.
    A client that rewrites `EndAt`, `P1..P4`, `ArmAt`, `ResetMode` or `Profile` mid-run changes the
    running scenario, with one scan of latency and no record that it happened. The model represents a
    scenario *as commanded at each scan*, not *as commanded at T=0*.
21. **The trailing clear-down is not guaranteed to run.** *(NEW.)* `InTrailingCleardown` (N24)
    requires `NOT Stim.Start`. If the harness re-raises `Start` inside the phase, `InTrailingCleardown`
    goes false immediately, the `TrailingTimer` resets, and `CleardownPending` **stays latched** —
    only `TrailingTimer.Q` clears it (N23). So the phase can run for a partial duration, or **zero
    times**, and `CleardownPending` then persists into the next run and is re-set at its end. N23's
    comment says the phase *"runs once per completed run"*; it can run zero times. The next run's
    *leading* clear-down covers the hygiene, so the outcome is benign — but "every completed run is
    followed by a full 5 s clear-down" is not a claim this model supports.
22. 🔴 **THE BLOCK'S OWN HEADER CONTAINS A FALSE SENTENCE ABOUT ITS PLANT WRITES, AND IT IS THE
    SENTENCE A READER WOULD RELY ON INSTEAD OF CHECKING.** *(NEW, and stated as its own entry rather
    than a parenthetical because of what it does to a reader.)* The block `COMMENT` says the model
    **"writes no plant tag"**. It writes `DB_Controls.FaultReset` — the plant's **only** operator-reset
    surface, the HMI command surface named as such in `DB_Controls.ir` — **unconditionally, every scan,
    in every state** (N21, a plain coil with no enable), as the **sole PLC writer**, reaching five
    consumers including the monitored block and the physical relay output `DQ11_SHR_FaultReset`. Every
    clause of the header sentence is false. **That sentence survived the very edit that produced the
    current version of this block** — the edit that added a *second* write of the same tag (N23/N24 →
    `TrailingReset` → N21).

    This is recorded as a fidelity entry, not filed as a code defect, because of its effect on
    evidence: **an undocumented behaviour prompts a reader to go and check; a false header sentence
    answers the question so the reader does not.** Anyone auditing this model's blast radius by
    reading it — the M5 review the spec requires, precisely because a wrong model silently corrupts
    every vector that uses it — is told in the block's own voice that the blast radius is zero. **No
    assertion, review finding or result may rest on that sentence, and this declaration contradicts it
    outright.** Correcting the comment is a `lad-coder` matter and is not done here; the declaration's
    job is to make sure nothing downstream believes it in the meantime.

## The contradiction, in full

The block now contains **two** places where a comment names a mechanism that the logic does not
implement. The first was in the previous declaration and is unchanged. The second arrived with the
trailing clear-down.

### First: the pre-boundary wait

**Claim under examination** — the block `COMMENT` and the `InPreBoundaryWait` member comment both say
the model *"is holding still, waiting for the CPU to be stopped and restarted"*, with the plant held
stopped so the monitored block's window *"does not drift while it waits"*.

**What the logic does.**

- N9: `InPreBoundaryWait := Running AND NOT InCleardown AND NOT PreBoundaryDone AND
  Stim.Precondition = 1 AND ScenarioT >= Stim.PreBoundaryHighRunning`.
- N10: `SCOIL PreBoundaryDone := InPreBoundaryWait`.

The state that is supposed to wait sets, in the same scan, the very flag its own guard forbids. On
the next scan `InPreBoundaryWait` is false. **The wait is one scan wide** — **~2.26 ms** on the
program deployed on this rig (`doesNotRepresent` 14) — and cannot be waited in.

**What happens on that next scan.**

1. N5 flips `Offset` from `ClearDownTime` (`T#5S`) to `T#0S`, because `PreBoundaryDone` is now set.
2. N6 recomputes `ScenarioT := RunT - Offset`, so `ScenarioT` **jumps forward by 5 s** in one scan.
3. N9's `InPreBoundary`/`InPreBoundaryWait` are both false (`NOT PreBoundaryDone` fails), so N19's
   phase guards are open. Two cases follow:
   - **`EndAt <= PreBoundaryHighRunning + 5 s`** (the ordinary case — `EndAt` is a scenario length,
     `PreBoundaryHighRunning` is sized to spend a 60 s persistence window): N19 latches
     `Stim.ScenarioDone`, N1 drops `Running`, and **N20's `RCOIL PreBoundaryDone :=
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

**A related ambiguity, recorded rather than resolved.** Even with no restart at all, the transition
`InPreBoundaryWait → InScenario` does **not** re-zero `ScenarioT`, so a `Precondition = 1` scenario
begins at `ScenarioT ≈ PreBoundaryHighRunning`. The UDT defines its times as *"durations measured from
the start of the scenario, which is the instant the clear-down finishes"*, under which reading the
behaviour is self-consistent and `P1`, `ArmAt`, `ResetAt` and `EndAt` all **include** the pre-boundary
duration. A reader who took "start of the scenario" to mean "after the pre-boundary phase" would be
off by `PreBoundaryHighRunning` on every one of them. The model's own vocabulary supports the first
reading; **this declaration does not license the second.**

### Second: the trailing clear-down's "protection for the startup scenarios"

**Claim under examination** — N23's comment says of its second term:

> *"…and that second term is the whole protection for the startup scenarios: across an externally
> performed CPU stop and restart the run has not completed, ScenarioDone is false and the retained
> PreBoundaryDone is set, so nothing is owed and nothing is cleared."*

**What the logic does.**

- N20 (four networks earlier, same scan): `RCOIL PreBoundaryDone := Stim.ScenarioDone`.
- N23: `SCOIL CleardownPending := Stim.ScenarioDone AND NOT PreBoundaryDone`.

In the scan N19 latches `ScenarioDone`, N20 **clears `PreBoundaryDone`**, and N23 then evaluates
`NOT PreBoundaryDone` as **true, unconditionally**. It cannot recover afterwards either:
`PreBoundaryDone` is set only by N10 from `InPreBoundaryWait`, which requires `Running`, which N1
holds false while `ScenarioDone` is latched. **The term named as "the whole protection" is provably
dead — it can never be false when the rung is evaluated.**

**What is therefore claimed and not claimed.** The *behaviour* the comment describes is nevertheless
delivered, by the comment's own other clause: across an externally performed stop/restart
`ScenarioDone` is false, so `CleardownPending` is never set and no trailing clear-down runs. **This
declaration claims the behaviour and does not claim the mechanism.** A vector that asserts *"a
restart leaves the latched alarm untouched"* rests on `ScenarioDone` alone; a vector or a review
finding that cites `NOT PreBoundaryDone` as the reason is citing a term that contributes nothing, and
any change that made `ScenarioDone` survive a restart would remove the protection with no rung
appearing to change.

## `validatedAgainstPlantData`: `false`

Answered honestly, and the absence is recorded because §11 M5 says it must be. There is no plant data
for this model to be validated against: no level trace, no blockage event log, no sensor-chatter
statistics, no commissioning record — nothing of the kind exists anywhere in this repository for the
hopper, and `test-project001` is a de-identified sandbox project whose deployment target is a bench
rig with outputs physically incapable of actuating. The model's timeline shapes come from the
specification's case list (see `doesNotRepresent` 2), and its two driven signals are commanded
booleans with no physical referent (entry 1). `false` is not a caveat here — it is the whole epistemic
status of the model. **Re-declared as `false`, and nothing in the current block moves it.**

## Declared gaps

### No scan-time budget, and no `compStable` (§11 M3, M6)

M3 requires a **declared scan-time budget** as part of the fidelity declaration, and the submission
contract's `model` object carries a `compStable` field (`docs/notes/test-environment-contract.md`
§2.3). **Neither is declared here, and both are stated as gaps rather than filled with an estimate.**

- **Scan-time budget.** No measurement of this block in isolation exists. It is one FB, called once
  from `Main` N1, now **24** LAD networks (was 22), **two** `TON`s (was one), 35 statics, no loops and
  no block calls. The only measured figure is whole-program: **~2.26 ms/scan** for `GenProject1`
  (stamp `16#41E5DA59`), the program actually deployed on this rig, which contains this block but does
  not attribute it. Neither of the two figures previously written into this document applies — see
  `doesNotRepresent` 14 for the provenance of all three. **The budget cannot be inferred from the
  whole-program figure**, and the change since the first declaration added a second `TON` and two
  networks, so even a prior per-block measurement would now be stale.
- **`compStable`.** Absent. Per the contract this is *reported and does not gate* at
  `runtimeCompression = 1`, and is a **REFUSAL** at any applied factor above 1×. It is not supplied
  here because no reading of the block establishes at what compression the model still behaves as
  declared, and a number invented for it would be exactly the kind of unfounded input the contract
  refuses rather than defaults. **Any wave that wants to run this model compressed must obtain that
  number by measurement, not from this document.**

### The IR set is not internally consistent, and the model has no export to check against

Three separate facts, each of which limits what this declaration can be taken to describe:

1. **`iDB_HopperBlockageStim.ir` carries 21 of the FB's 35 statics.** Missing: `RunT`,
   `HopperHeldHigh`, `HopperClearedMidRun`, `HopperClearedInStop`, `RunHeldOn`, `RunStoppedMidRun`,
   `RunStoppedRoundClear`, `ClearDownReset`, `ScenarioReset`, `CleardownPending`,
   `InTrailingCleardown`, `TrailingReset`, `TrailingT`, `TrailingTimer`. Nine of those pre-date the
   current change — the iDB has never matched the FB. In TIA the iDB is regenerated from the FB, so
   this is corpus-file staleness rather than a runtime defect, but **the committed IR set does not
   describe one coherent object.**
2. **There is no committed SimaticML export of this model.** `simatic-ml/test-project001/` contains
   26 files and none of them is `FB_HopperBlockageStim`, `UDT_HopperBlockageStim` or
   `iDB_HopperBlockageStim`. So `converter drift-check` has nothing to compare the IR against, and
   **drift between this declaration's subject and whatever is in the controller can be neither
   confirmed nor excluded.** The contract is explicit about what that costs: a declaration describing
   a drifted object makes every M4 pass a set-difference against the wrong set, and **the verdict is
   `STALE`, never `FAIL` and never `REFUSED`.**
3. **The block author's own commit messages state, in capitals, that the compile gate has never run on
   this block** — the import was refused by the auto-mode classifier and not retried. This declarer
   cannot verify or refute that (it needs Portal, which is out of scope here), and records it as the
   author's claim. If it is true, the two new networks have never been compiled either. A related
   unresolved discrepancy: `FB_HopperBlockageMonitor` states as fact that *"TIA import rejects a bare
   Time literal as a MOVE operand"* and carries a `ZeroTime` member to avoid it, while this model's
   N4/N5/N6 do exactly that (`MOVE(EN := TRUE, IN := T#5S)`). Both cannot be right, and only an import
   settles it.

## Not decidable from the corpus

1. **What `DI5_SHR_RunFwdFB` and `DI8_HPR_LevelHigh` read on the rig.** This decides whether
   `represents` 12's first release window — injection off, field connected, while the harness is
   reading the result — is cosmetic or material. If those terminals are unterminated-low the effect is
   nil; if either can be high, the monitored block resumes accruing on the real field during the read.
   Terminal state is not expressible in IR. **Recorded as unknown, and it is the single fact this
   declarer would most want confirmed against the rig wiring.**
2. **Whether the harness command mirror survives a CPU stop/restart.** `Stim.Start` is copied from
   `HX_HBA_Start` at `%M1009.0`, and the vector registers from `%MW1012` upward, by
   `FC_HarnessCopyLayer` N3–N5 every scan. Marker retentivity is a CPU hardware-configuration property
   and appears in no `.ir` file. If those markers come back cleared after a restart, a post-restart run
   cannot re-latch `Running` until the harness rewrites them; if they survive, it re-latches
   immediately. This matters to the `Precondition = 1` path and is recorded as **unknown** rather than
   resolved in either direction. *(Re-verified: still not expressible in the IR.)*
3. **Whether `DB_Input.Test[]` and `DB_Output.Test[]` are retentive.** Not declared in the IR. It does
   not affect `Test[0]`, `Test[5]`, `Test[8]` — the model rewrites all three unconditionally at `Main`
   N1 before `FC_Inputs` runs, so a stale value survives zero scans — and `Test[6]` is only ever reset.
   Recorded for completeness.

## What a reader should take from this

The model remains a competent, well-documented **signal-timeline generator** for three booleans, with
a clean shape/duration parameterisation and an honest separation between commanding and observing.
The change since the first declaration is a genuine improvement in test hygiene — it puts the
monitored block back into a known state after a result is read instead of leaving it latched.

It is still not a plant model in any physical sense. It still suppresses nineteen other inputs while
it runs. Its "operator reset" is still the plant-wide reset — and this declaration now says the harder
version of that: **the model is the only PLC writer of `DB_Controls.FaultReset` and writes it
unconditionally every scan, so the operator's own reset is inert for as long as this block is loaded,
and every commanded pulse energises a physical output.** Its pre-boundary/restart path still does not
do what its comments say it does, and the trailing clear-down has now added a second comment that
names a protection its own logic cannot supply.

Three things are new and a reader should weigh them before trusting any assertion:

- **The commanded plant is unwitnessed.** `Stim.Hopper`, `Stim.Run` and `Stim.FaultReset` reach no
  mirror register. Every assertion about this model's stimulus is an assertion about what the IR says
  the model *would* drive, never about what a wave observed it driving.
- **Injection is released in an unbounded window between the scenario ending and the harness dropping
  `Start`** — which is exactly the window in which the result is read.
- **The reset is asserted a second time, plant-wide, after the result is read**, and the violation
  instrument watching `Stim.FaultReset` does not see that pulse while the monitored block does.

Assertions that rest on the timeline the model drives *during a scenario* are licensed. Assertions
that rest on the field being restored the moment a run ends, on the reset being confined to the run,
on `ResetMode = 2` presenting a single edge, on the pre-boundary wait being a wait, on the trailing
clear-down being guaranteed, or on any stimulus value having been observed rather than inferred, are
not.
