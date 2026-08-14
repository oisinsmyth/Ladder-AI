# HARNESS BINDING — conformance vector set B to the IR that implements it

> ## 🔴 DO NOT GIVE THIS FILE TO A VECTOR AUTHOR
> It quotes the implementation's interface names throughout. A vector author's value rests entirely
> on not having read the implementation, and this document is a contamination channel for exactly
> that. Brief vector authors with an **allowlist** of inputs; this file is not on it.

**Owner: the coordinator.** Not the vector author, who must not read the IR, and not the block
author, who must not read the vectors. *A vector citing a name the block does not have is a `FAIL`
that blames the block for a naming convention*, so this mapping is the thing that has to exist and
has to be maintained by whoever holds both halves.

Written 2026-08-13 by the rig-enablement lane. Set B is the deliverable (set A is frozen at 25 as
the contaminated comparison artifact and is **not** implemented — an A vector refusing to bind is
expected behaviour, not a defect).

---

## 1. Why any mapping is needed at all

Three independent reasons, none of them avoidable:

1. **`C-001` forbids underscores outside physical IO.** `HBA_Stim` was refused by `converter
   preflight`; the member is **`Stim`**.
   > 🔴 **STRUCK 2026-08-14.** This reason previously continued: *"Same rule already forced
   > `HopperBlockedInhibit` to be `HopperBlockStopReq` on the block under test."* **That was false in
   > two independent ways.** C-001's rule is *underscore-free member names* and **neither name
   > contains an underscore** — both comply, so C-001 cannot have forced anything. And
   > `FB_HopperBlockageMonitor` was authored under S6 request #1 **weeks before the enumeration
   > existed**, so the block's name predates the spec's and the stated causal direction is
   > impossible. **The `HopperBlockStopReq` mapping is not a naming-convention consequence. It is
   > divergence D1** — see §3. *A laundering with a rule number beside it stops being re-read.*
2. **Set B's names carry a unit suffix the IR type makes wrong.** `...Ms` members are `Time`, not
   integer milliseconds — see §4.
3. **One set-B field is two IR members.** `ResetAtMs` carries sentinels (`-1`, `HOLD`,
   `COINCIDENT_WITH_THRESHOLD`) mixed with real times; the IR splits the mode from the time, because
   a magic number in a duration is how a scenario silently becomes a different scenario.

---

## 2. Command members — set B name → IR path

Instance root: `iDB_HopperBlockageStim`.

| set B | IR | type | registers |
|---|---|---|---|
| `HBA_Stim.Profile` | `.Stim.Profile` | `Int` | 1 |
| `HBA_Stim.Precondition` | `.Stim.Precondition` | `Int` | 1 |
| `HBA_Stim.P1Ms` | `.Stim.P1` | `Time` | **2** |
| `HBA_Stim.P2Ms` | `.Stim.P2` | `Time` | **2** |
| `HBA_Stim.P3Ms` | `.Stim.P3` | `Time` | **2** |
| `HBA_Stim.P4Ms` | `.Stim.P4` | `Time` | **2** |
| `HBA_Stim.PreBoundaryHighRunningMs` | `.Stim.PreBoundaryHighRunning` | `Time` | **2** |
| `HBA_Stim.ArmAtMs` | `.Stim.ArmAt` | `Time` | **2** |
| `HBA_Stim.ArmUntilMs` | `.Stim.ArmUntil` | `Time` | **2** |
| `HBA_Stim.EndMs` | `.Stim.EndAt` | `Time` | **2** |
| `HBA_Stim.ResetAtMs` | `.Stim.ResetMode` **and** `.Stim.ResetAt` | `Int` + `Time` | 1 + **2** |
| *(no set B name)* | `.Stim.ModelThreshold` | `Time` | **2** |
| `startBool` (per slot) | `.Stim.Start` | `Bool` | 1 bit |

`EndMs → EndAt` because `End` is a reserved word in the surrounding toolchain and a member named
`End` is not worth the risk.

`ModelThreshold` has **no set-B name and is deliberately not derived from the block.** Profile
`RESET_AT_THE_CROSSING` needs the model to place a reset at what it *believes* the threshold to be;
read from the block under test it would stop being a stimulus decision and become a reading of the
thing being tested. Set in the instance DB at `T#60S`.

## 3. Published members

| set B | IR | note |
|---|---|---|
| `HBA_Stim.Armed` | `.Stim.Armed` | arms the copy layer's observation latches |
| `HBA_Scenario_Done` | `.Stim.ScenarioDone` | latched |
| `HopperBlockedAlarm` | `iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm` | `Bool` |
| `HopperBlockedInhibit` | `iDB_HopperBlockageMonitor.IO.HopperBlockStopReq` | `Bool` — 🔴 **DIVERGENCE D1, NOT A RENAME.** See below |
| `HBA_Violation_AlarmFellWithoutReset` | `iDB_HarnessViolationLatch.AlarmFellWithoutReset` | `Bool`, published `HX_HBA_R004` @ `%M1063.0` (reg 31) |
| `HBA_Violation_AlarmLowUnderHeldReset` | `iDB_HarnessViolationLatch.AlarmLowUnderHeldReset` | `Bool`, published `HX_HBA_R005` @ `%M1065.0` (reg 32) |
| `HBA_Violation_InhibitLowWhileAlarmHigh` | `iDB_HarnessViolationLatch.InhibitLowWhileAlarmHigh` | `Bool`, published `HX_HBA_R006` @ `%M1067.0` (reg 33) |
| `HBA_Violation_InhibitHighWhileAlarmLow` | `iDB_HarnessViolationLatch.InhibitHighWhileAlarmLow` | `Bool`, published `HX_HBA_R007` @ `%M1069.0` (reg 34) |

> ### 🔴 D1 — THE BLOCK DOES NOT IMPLEMENT `HopperBlockedInhibit`
>
> **Re-labelled 2026-08-14 on the coordinator's ruling.** This row previously read *"the rename"*,
> which made a predicted divergence read as a naming convenience and put it beyond the run's reach.
> What it actually records:
>
> *** THE BLOCK DOES NOT IMPLEMENT `HopperBlockedInhibit`. IT IMPLEMENTS `HopperBlockStopReq`. THIS
> IS FINDING D1, AND IT IS REPORTED STATICALLY — NOT BY THIS RUN. *** The run proceeds against the
> real signal **so that the BEHAVIOURAL assertions can still be evaluated**; the naming divergence is
> not theirs to catch and never was.
>
> **Established, not assumed** (`lad-coder` read of `ir/test-project001/`, 2026-08-14): the exact
> string `HopperBlockedInhibit` occurs **nowhere in the corpus** — not in any of the 36 files, in
> code or in comments. `HopperBlockStopReq` is a member of `UDT_HopperBlockageIO`, carried on the FB
> as the STATIC `IO`. The word "inhibit" survives only in prose — network 6's title *"Stop / Inhibit
> Demand"* and the UDT's own member comment — **which is precisely why a name check must read
> member names and never comments.**
>
> ⚠️ **Anyone reading a green D6 must read this box first.** D6 predicts conformance to the tracking
> invariant over `HopperBlockedAlarm` and this signal. That prediction is about **behaviour** and it
> survives — but only while D1 is reported by the independent static check. Without it, D1 hides
> inside D6: a relational assertion is structurally blind to any error its operands share, and a
> misbound operand is exactly such an error.

> ### ✅ THE FOUR VIOLATION LATCHES EXIST — this table said they did not, and it was STALE
>
> **Corrected 2026-08-14.** The row previously read *"**do not exist** — copy-layer instrumentation,
> not yet built"*, which would have refused four vectors against four registers nobody backed.
> **Established by a `lad-coder` read, not assumed from the likelihood:** `FB_HarnessViolationLatch`
> (FB 9003) exists and declares all four as `Bool` STATICs; `FC_HarnessCopyLayer` network 7 copies
> each to the `%M` addresses above; `iDB_HarnessViolationLatch` instantiates it; `Main` network 6
> calls it **before** the copy layer in network 7. The addresses match `wire-prediction.md` §1
> registers 31–34 exactly.
>
> **No other block writes any of the four, and the copy layer is their only reader** — established
> by a whole-corpus search, which is what makes this an absence rather than a failure to find.

## 4. Value encodings

**`Precondition`** — `CLEARDOWN` = `0`, `CLEARDOWN_BEFORE_BOUNDARY` = `1`.

**`ResetAtMs` splits into `ResetMode` + `ResetAt`:**

| set B value | `ResetMode` | `ResetAt` |
|---|---|---|
| `-1` | 0 (never) | ignored |
| a number, e.g. `90000` | 1 (one pulse) | that duration |
| `HOLD` | 2 (held true, no edge inside the scenario) | ignored |
| `COINCIDENT_WITH_THRESHOLD` | 3 (one pulse at `ModelThreshold`) | ignored |

**`Profile` — the tens digit IS the timeline shape**, which is what makes the decode one `DIV`
rather than a 22-way comparison, and makes a new profile a numbering decision rather than a code
change. **Extend by numbering into an existing decade.**

| shape | code | profile |
|---|---|---|
| 1 hold high + running | 10 / 11 / 12 / 13 / 14 / 16 / 17 | `RAISE_UNINTERRUPTED` / `RESET_CONDITION_HOLDS_RUNNING` / `RESET_HELD_ON` / `RESET_AT_THE_CROSSING` / `STARTUP_NO_ALARM_LATCHED` / `STARTUP_RESET_ON_BLOCKED_HOPPER` / `STARTUP_RESET_THEN_REARM` |
| 2 high at standstill | 20 | `HIGH_AT_STANDSTILL` |
| 3 clear mid-run | 30 / 31 / 32 / 33 / 34 / 35 / 36 / 37 / 38 | `DEBOUNCED_CLEAR_BELOW_THRESHOLD` / `CHATTER_BELOW_FILTER` / `REARM_RUNNING` / `CHATTER_NEAR_THRESHOLD` / `LATCH_HOLDS` / `HOPPER_CLEARS_WHILE_LATCHED` / `RESET_CONDITION_CLEARED` / `PAIR_ALARM_HIGH` / `PAIR_ALARM_LOW` |
| 4 stop mid-run | 40 / 41 | `PAUSE_AND_RESUME` / `STOP_THEN_RESET` |
| 5 stop with a clear inside it | 50 / 51 | `DISCARD_BEATS_FREEZE` / `FREEZE_BELOW_FILTER` |
| 6 idle | 60 | `STARTUP_ALARM_LATCHED` |

Segment boundaries: `T1 = P1`, `T2 = T1+P2`, `T3 = T2+P3`, `T4 = T3+P4`.
Hopper high when: shape 1/2/4 always; shape 3 `t<T1 or t>=T2`; shape 5 `t<T2 or t>=T3`; shape 6 never.
Plant running when: shape 1/3 always; shape 4 `t<T1 or t>=T2`; shape 5 `t<T1 or t>=T4`; shape 2/6 never.

---

## 5. 🔴 WIDTH — NOTHING HERE IS ONE REGISTER PER VALUE

**Ten of the thirteen command members are `Time`, which is 32-bit and spans TWO registers.** The
mirror's element type is therefore not `Int`, and any geometry, allocator or copy layer that assumes
16-bit elements is wrong for this block. This is the same finding as the `Bool` result-register
defect and it is **one idea, not two**: the mirror carries `Bool`, `Int` and 32-bit elements.

**And every 32-bit member inherits the uncalibrated word-order transform.**

> ### The failure is LOUD here, and that is worth knowing before someone spends a day on it
> A word-swapped `Time` in the **command** direction is not a subtle timing error. Every duration in
> set B lies between 1 000 ms and 120 000 ms; swapping the words multiplies the low half by 65 536,
> so `P1 = 75 000 ms` (`16#000124F8`) arrives as `16#24F80001` — about **seven days**. The scenario
> then never reaches `P1`, `ScenarioDone` never latches, and the run **times out** rather than
> returning a wrong answer.
>
> *** THE SAME IS NOT TRUE IN THE OBSERVATION DIRECTION. *** The free-running scan counter is a
> `DInt` whose high word changes slowly, so a swapped read is a plausible-looking number, and *that*
> is where a word-order error hides. Calibrate against the counter, not against a duration.

---

## 6. 🔴 SIX SLOTS CANNOT RUN AS ONE WAVE — THEY ARE STRICTLY SERIAL

Set B declares `slotsInWaveSet: 6` with six start bools (`HBA_Start_Raise`, `_Clear`, `_Latch`,
`_Reset`, `_Startup`, `_Pair`). **The project cannot honour that concurrently, and the reason is
structural rather than a shortage of instances:**

- there is **one** `iDB_HopperBlockageMonitor`, so all six slots test the same block state;
- all six drive the **same** `DB_Input.Test[]` members and the **same** `DB_Controls.FaultReset`;
- `DB_Input.Test` is a single shared array — there is no per-slot copy of the input buffer.

So the six slots are **mutually conflicting by computed disjointness (D9)**, and the wave set is six
waves of one slot, not one wave of six. **The `_waveGeometry` note's figures were declared
conservatively by an author who could not see this**, and the honest replacement is `slotsInWaveSet:
1`. Anything derived from 6 — the section 12a floor in particular — should be recomputed.

Making them genuinely concurrent would need six monitor instances, six stimulus instances and six
input buffers. That is a project-shape decision, not a harness setting.
