# Conformance vector set B — reconciled against the independent model

**Reconciled by `vector-reconciler-1`, 2026-08-17.**
Runnable submission: `conformance-vectors-b-reconciled.json`.
Model (**authoritative, untouched**): `model-fidelity-MDL-HopperBlockageStimulus-1.json`, declared by
`model-fidelity-declarer-1`.
What was refused, kept as the record: `conformance-vectors-b-independent-model.json` and
`conformance-vectors-b.json`.

---

## THE COUNT

| | |
|---|---|
| Vectors in | **27** |
| **Survive** | **22** |
| Dropped | **5** |
| Kept **unchanged** | ***0*** |
| Reworded only (no loss of claim) | ***0*** |
| Narrowed | **22 — every survivor** |
| Assertions in the enumeration | 27 |
| **Assertions with no vector after this** | **5 — the whole of REQ-HBA-007** |

*** READ THE "KEPT UNCHANGED: 0" ROW BEFORE THE "22 SURVIVE" ROW. *** Every single vector in this set
carried the asserted behaviour *"shredder running feedback, **forward or reverse**, held for a commanded
duration"*, and the model drives **forward only**. There is no such thing here as a vector that merely
needed different words: all 22 survivors lost a claim, and the same one.

---

## THE DIRECTION, AND WHY NOTHING IN THE MODEL MOVED

M4 says a vector may assert a behaviour **only if the model claims it**. The model is now authored by a
third party who worked from `ir/test-project001/FB_HopperBlockageStim.ir` with no sight of these vectors,
which is the only reason gate 4b passes. `model.represents` was **not** widened, edited or reordered —
adding an entry so a vector would fit is precisely the laundering gate 4b exists to prevent, and it would
have spent the one independent artifact in this submission to buy a green.

**Verified mechanically, and stated at the precision the check actually reached:** the `model` object in
`conformance-vectors-b-reconciled.json` is **equal, key for key, to the `model` object in
`conformance-vectors-b-independent-model.json`** — i.e. this pass changed nothing in it. Against the
**standalone** `model-fidelity-MDL-HopperBlockageStimulus-1.json`, all five *contract* fields (`id`,
`represents`, `doesNotRepresent`, `declaredBy`, `validatedAgainstPlantData`) are equal, so a naive
"byte-identical" claim would have been **false** for the reason in the next paragraph.

> ⚠️ **FINDING, NOT MINE TO FIX: THE JOIN DROPPED FOUR OF THE MODEL'S OWN ANNOTATIONS.** The standalone
> declaration carries `_declaredBy` (what the declarer read and did **not** read), `_validatedAgainstPlantData`
> (why it is `false`), **`_scanTimeBudget`** (*"NOT DECLARED - a gap, stated rather than filled"*, the
> spec S11 M3 gap) and `_notComputable` (marker retentivity across a restart — **the very question the
> five dropped vectors turn on**). None of them is present in either submission copy; they were lost when
> the coordinator joined the model in, before this pass. M3's whole purpose is that *the declaration
> travels with every result so it is never read without its caveats* — and here the caveats did not
> travel. **Not repaired here, because repairing it means editing the `model` object, which this lane is
> forbidden to do.** Raised for the coordinator: re-join the model with its `_`-prefixed fields intact
> (gate 0b excludes them by name, so carrying them costs nothing).

---

## THE MODEL'S `represents` SET, INDEXED

`R`*n* below is `model.represents[n-1]`, 1-based, in file order. Four of the twelve are cited by the
reconciled vectors; they are quoted in full because they are the licences.

- **R1** — *"The commanded state of exactly three signals the monitored block consumes - the hopper
  high-level sensor, the **FORWARD** shredder running feedback, and the shared operator fault reset -
  injected at the input map's test members and at DB_Controls.FaultReset (FB_HopperBlockageStim N21;
  FC_Inputs N1 lines for Test[5]/Test[8]; FC_ControlMain N8 lines 91-93)."*
- **R2** — same-scan presentation to the monitored block. *(cited by nothing)*
- **R3** — *"Six timeline shapes selected by the TENS digit of Profile (N7 DIV by 10), with segment
  boundaries accumulated from the commanded P1..P4 (N8): shape 1 hopper high and plant running throughout;
  shape 2 hopper high with the plant never running; shape 3 plant running throughout with the hopper low
  for [T1,T2); shape 4 hopper high throughout with the plant stopped for [T1,T2); shape 5 plant stopped
  for [T1,T4) with the hopper low for [T2,T3), i.e. the clear falls wholly inside the stop; shape 6
  neither signal driven, hopper low and plant stopped (N11, N12, N13, N14)."*
- **R4** — *"A hopper clear of any commanded duration of one CPU scan or longer - including one
  deliberately SHORTER than the monitored block's clear-debounce filter time - as a stimulus, by choosing
  P2 (shape 3) or P3 (shape 5) (N8, N11)."*
- **R5** — *"A commanded operator reset in four modes (N16, N17): never asserted inside the scenario; one
  pulse of ResetPulseWidth = T#500MS at ScenarioT = ResetAt; continuously true for the whole scenario
  phase; one pulse of the same width at the model's own ModelThreshold value."*
- **R6** — the fixed T#5S pre-scenario clear-down. *(cited by nothing — see "what I did not add")*
- **R7** — a run that begins with no clear-down when `PreBoundaryDone` is already set. *(cited by nothing)*
- **R8** — the pre-boundary conditioning interval ending in one retentive Bool. *(cited by nothing)*
- **R9** — the published `Stim.Armed` observation window. *(cited by nothing — see "what I did not add")*
- **R10** — the published latched `Stim.ScenarioDone`. *(cited by nothing)*
- **R11** — a run that cannot be truncated by the harness dropping Start. *(cited by nothing)*
- **R12** — restoration of the field at the end of a run. *(cited by nothing)*

---

## (A) THE VOCABULARY ARTEFACT — the transcription table

Gate 4 compares `assertedBehaviours` against `represents` as **exact ordinal strings**
(`Admissibility.cs`: `assertedBehaviours.Where(b => !fidelity.Represents.Contains(b))`). An independent
declarer naturally used its own words, so vectors refused even where the behaviour genuinely is
represented. Each row below is the transcription and the entry that licenses it.

| original asserted behaviour | vectors | → | disposition | why that entry covers it |
|---|---|---|---|---|
| hopper-level-high discrete sensor held true for a commanded duration | 22 | **R1 + R3** | REWORDED | R1 names the hopper high-level sensor as one of the three signals whose commanded state the model represents; R3 supplies *"for a commanded duration"* — segment boundaries accumulated from the commanded P1..P4. **Neither entry alone carries both halves.** |
| shredder running feedback, **forward or reverse**, held for a commanded duration | 22 | **R1 + R3** | ***NARROWED*** | See (B)1. |
| hopper-level-high discrete sensor driven false for a commanded duration, longer than the clear-debounce filter time | 9 | **R4** | REWORDED | R4 represents a clear of *any* commanded duration ≥ one CPU scan, which contains "longer than the filter time". |
| hopper-level-high **sensor chatter** driven false for a commanded duration shorter than the clear-debounce filter time | 3 | **R4** | ***NARROWED*** | R4 represents a **commanded short clear**, explicitly *"including one deliberately SHORTER than the monitored block's clear-debounce filter time"*. But the model disclaims the field-side signal path outright — *"No wire break, stuck-at, contact bounce, DI hardware input filtering or terminal polarity is represented"* — so **"chatter" over-claimed a physical phenomenon.** The commanded durations (1.0 s / 1.5 s / 1.0 s) all clear R4's one-scan floor (~2.1 ms) and fall under the 2 s filter, so the *assertion* is still exercised; only the word went. |
| loss and return of shredder running feedback part-way through a persistence window | 4 | **R1 + R3** | ***NARROWED*** | R3's shape 4 (*"hopper high throughout with the plant stopped for [T1,T2)"*) and shape 5 are exactly that loss and return, at commanded boundaries. Forward only, per R1 — the same narrowing as (B)1. |
| operator fault-reset as a single rising edge at a commanded time | 5 | **R5** | REWORDED | R5 mode 2: *"one pulse of ResetPulseWidth = T#500MS at ScenarioT = ResetAt"*. |
| operator fault-reset held continuously true **from before T=0** | 1 | **R5** | ***NARROWED*** | See (B)3. |
| operator fault-reset rising edge placed in the scan in which **the model's own** persistence threshold elapses | 1 | **R5** | REWORDED | R5 mode 4: *"one pulse of the same width at the model's own ModelThreshold value"*. The vector already said *"the model's own"*, so it made no claim about the **block's** threshold — which is what `doesNotRepresent` requires. |
| CPU stop and restart across an already-scheduled disruptive download boundary, with retentive memory preserved | 5 | — | ***DROPPED*** | See (B)2. |

⚠️ **THE TRANSCRIPTION COARSENS, AND THAT IS A PROPERTY OF THE GATE, NOT A CHOICE MADE HERE.** R1 is a
*single* entry covering all three commanded signals, so once transcribed, *"the hopper was held high"* and
*"the running feedback was held"* collapse onto the **same string**. Gate 4 after reconciliation therefore
checks a claim at the model's granularity, which is coarser than the one the vectors originally made. The
original per-behaviour detail survives in each vector's `_assertedBehavioursProvenance.mapping` and in the
table above — nowhere else. **A green on gate 4 from this file is weaker evidence than a green on gate 4
would have been from a set whose vocabulary matched the model's granularity.**

---

## (B) THE THREE GENUINE CONTRADICTIONS — no rewording reaches these

### (B)1 — REVERSE RUNNING FEEDBACK IS FORCE-CLEARED BY THE MODEL. **All 22 survivors narrowed.**

The model represents *"the **FORWARD** shredder running feedback"* (R1) and nothing else.
`doesNotRepresent` is explicit:

> *"The REVERSE running feedback, or the plant-running condition as an OR of two directions. The model
> drives the forward feedback only and force-clears the reverse feedback's test member for the whole run
> (N22), while the monitored block's PlantRunning is Fwd OR Rev (FC_ControlMain line 92)."*

Every vector asserted *"forward or reverse"*. Every one is narrowed to forward. **The vectors were always
going to drive forward only** — their own `_scenario` prose already names `Shredder_Run_Fwd_FB` — so what
this repairs is an over-broad *claim*, not the stimulus. That makes the narrowing correct and the coverage
loss real at the same time; see the coverage section.

**VB-HBA-003 is the sharpest case and is called out separately.** Its cited assertion
`REQ-HBA-001:0772fa` names both feedbacks in its own trigger — *"while **neither** Shredder_Run_Fwd_FB
**nor** Shredder_Run_Rev_FB is true"*. The model does deliver both-false (forward driven false by shape 2,
reverse force-cleared), so the vector will run and mean something. But the reverse-false half arrives as a
**force-clear the model explicitly declines to represent**, so it is asserted here only through R3's
*"shape 2 hopper high with the plant never running"*. The `nor Shredder_Run_Rev_FB` limb is exercised
**incidentally, not modelled.** Recorded rather than resolved.

### (B)2 — THE PRE-BOUNDARY "WAIT FOR THE CPU RESTART" DOES NOT EXIST. **5 vectors dropped.**

`doesNotRepresent` contradicts this twice, and the second entry is a measurement off the IR:

> *"A CPU stop, a CPU restart, or a power cycle. The block cannot command, cause, detect or confirm one."*

> *"A HELD pre-boundary wait for that restart - CONTRADICTS THE BLOCK'S OWN COMMENTS. InPreBoundaryWait
> requires NOT PreBoundaryDone (N9) and N10 sets PreBoundaryDone from InPreBoundaryWait, so the wait state
> is true for EXACTLY ONE SCAN (~2 ms) and can never be waited in."*

**Dropped, not narrowed.** Narrowing these to R8 (the pre-boundary conditioning interval and its retentive
Bool) would leave a vector that no longer tests its cited REQ-HBA-007 assertion at all — a claim dropped
while the title, the clause ID and the assertion count all still look intact. That is the *narrowing
repair* this project has already named as a defect class, and it is worse than a visible drop.

*Independently:* every one of these is `SLOT-HBA-STARTUP` and the runner already refuses STARTUP-class
vectors `NotSchedulable`, so gate 4 was never the only thing standing in their way. **That is a second
reason, not the reason** — the fidelity contradiction stands on its own.

| dropped | slot | clause | assertion | what it was for |
|---|---|---|---|---|
| **VB-HBA-019** | SLOT-HBA-STARTUP | REQ-HBA-007 | `REQ-HBA-007:4197e0` | accumulator must start fresh after power-up (kills a RETENTIVE accumulator) |
| **VB-HBA-020** | SLOT-HBA-STARTUP | REQ-HBA-007 | `REQ-HBA-007:7edf6c` | a latched alarm must survive the power cycle |
| **VB-HBA-021** | SLOT-HBA-STARTUP | REQ-HBA-007 | `REQ-HBA-007:578675` | the inhibit output must survive it too |
| **VB-HBA-022** | SLOT-HBA-STARTUP | REQ-HBA-007 | `REQ-HBA-007:9855c5` | AR-HBA-10's accepted gap, first half — reset after a power cycle clears a still-blocked hopper |
| **VB-HBA-023** | SLOT-HBA-STARTUP | REQ-HBA-007 | `REQ-HBA-007:34dc51` | AR-HBA-10's accepted gap, second half — the half that makes the gap **bounded** |

### (B)3 — "RESET HELD FROM BEFORE T=0" IS NOT PRODUCIBLE. **VB-HBA-016 narrowed, and its prose corrected.**

The model states the number of edges outright:

> *"ResetMode 2 likewise presents exactly ONE rising edge of the commanded reset - the reset is false from
> 4 s to 5 s of the run and true from the first scan of the scenario - not zero edges."*

VB-HBA-016's `_scenario` said *"FaultReset driven TRUE from before T=0"* and *"There is no rising edge
during the test"*. Both are false against the model. Narrowed to R5 mode 3, *"continuously true for the
whole scenario phase"*, and **the `_scenario` text has been corrected in the JSON** (the original is
preserved verbatim in `_scenarioBeforeReconciliation`) — leaving a false statement beside a corrected
declaration is how a stale claim outlives its retraction.

**The assertion survives the narrowing, and here is the argument, which is judgement and not a
measurement:** the single edge lands at the first scan of the scenario, with the accumulator at zero and
nothing latched, so it can clear nothing. The reset is then held true through the raise at 60 s. The NEVER
assertion — *"the hopper-blockage alarm is held de-asserted by a continuously-true FaultReset while the
accumulated persistence time is at or past the threshold"* — is still exercised, and the defect the vector
exists to kill (a LEVEL-sensitive reset) still dies. **What is weakened:** an implementation that resets
on the edge *and* holds down on the level would now be entered with one harmless edge it did not have
before. This sits under gate 3c / 6b, which are judgement gates and verify nothing.

---

## PER-VECTOR DISPOSITION — all 27

`represents cited` is the transcribed `assertedBehaviours`, by R-index.

| vector | slot | clause | assertion | disposition | represents cited | narrowings beyond the universal forward-only one |
|---|---|---|---|---|---|---|
| VB-HBA-001 | RAISE | REQ-HBA-001 | `ab68c1` | narrowed | R1, R3 | — |
| VB-HBA-002 | RAISE | REQ-HBA-001 | `8ea7f6` | narrowed | R1, R3 | — |
| VB-HBA-003 | RAISE | REQ-HBA-001 | `0772fa` | narrowed | R1, R3 | ⚠️ its **assertion** names both feedbacks; reverse-false arrives as an unrepresented force-clear (B)1 |
| VB-HBA-004 | RAISE | REQ-HBA-001 | `2bdfa7` | narrowed | R1, R3 | — |
| VB-HBA-005 | CLEAR | REQ-HBA-002 | `efe6b5` | narrowed | R1, R3, R4 | — |
| VB-HBA-006 | CLEAR | REQ-HBA-002 | `bfda9d` | narrowed | R1, R3, R4 | "chatter" → commanded 1.0 s clear |
| VB-HBA-007 | CLEAR | REQ-HBA-003 | `607fdf` | narrowed | R1, R3, R4 | — |
| VB-HBA-008 | CLEAR | REQ-HBA-003 | `652dd6` | narrowed | R1, R3, R4 | "chatter" → commanded 1.5 s clear |
| VB-HBA-009 | CLEAR | REQ-HBA-003 | `aa1d87` | narrowed | R1, R3, R4 | — |
| VB-HBA-010 | CLEAR | REQ-HBA-003 | `09e3a8` | narrowed | R1, R3, R4 | "chatter" → commanded 1.0 s clear |
| VB-HBA-011 | LATCH | REQ-HBA-004 | `14c9b6` | narrowed | R1, R3, R4 | — |
| VB-HBA-012 | LATCH | REQ-HBA-006 | `469954` | narrowed | R1, R3, R4 | — |
| VB-HBA-013 | RESET | REQ-HBA-005 | `0463ce` | narrowed | R1, R3, R4, R5 | — |
| VB-HBA-014 | RESET | REQ-HBA-005 | `7e39bc` | narrowed | R1, R3, R4, R5 | — |
| VB-HBA-015 | RESET | REQ-HBA-005 | `179eb7` | narrowed | R1, R3, R5 | — |
| VB-HBA-016 | RESET | REQ-HBA-005 | `6e1e8d` | narrowed | R1, R3, R5 | **"from before T=0" dropped; `_scenario` corrected** (B)3 |
| VB-HBA-017 | RESET | REQ-HBA-005 | `34fd97` | narrowed | R1, R3, R5 | — |
| VB-HBA-018 | RESET | REQ-HBA-005 | `e7e0e0` | narrowed | R1, R3, R5 | — |
| **VB-HBA-019** | STARTUP | REQ-HBA-007 | `4197e0` | ***DROPPED*** | — | (B)2 |
| **VB-HBA-020** | STARTUP | REQ-HBA-007 | `7edf6c` | ***DROPPED*** | — | (B)2 |
| **VB-HBA-021** | STARTUP | REQ-HBA-007 | `578675` | ***DROPPED*** | — | (B)2 |
| **VB-HBA-022** | STARTUP | REQ-HBA-007 | `9855c5` | ***DROPPED*** | — | (B)2 |
| **VB-HBA-023** | STARTUP | REQ-HBA-007 | `34dc51` | ***DROPPED*** | — | (B)2 |
| VB-HBA-024 | PAIR | REQ-HBA-008 | `d97781` | narrowed | R1, R3, R4 | — |
| VB-HBA-025 | PAIR | REQ-HBA-008 | `741315` | narrowed | R1, R3, R4, R5 | — |
| VB-HBA-026 | RAISE | REQ-HBA-002 | `feecbe` | narrowed | R1, R3 | — |
| VB-HBA-027 | RAISE | REQ-HBA-008 | `047ac6` | narrowed | R1, R3 | — |

Nothing else changed: stimulus `inputs`, `expectations`, `startBool`, `settlingCondition`,
`maxDurationScans`, `blacklist`, `compressionFactor`, `kills`, the citation, the enumeration, the map and
`conflictEdges` are all untouched. Vector authorship stays `vector-author-b-5.2` on every vector, because
they wrote them; `_reconciledBy: vector-reconciler-1` records what this pass changed.

---

## ⛔ THE COVERAGE COST — this is the number that must not be buried

### 1. REQ-HBA-007 has **no coverage at all**. 5 of 27 assertions are uncovered.

The enumeration and the vector set were 1:1 — 27 assertions, 27 vectors, each assertion cited exactly
once. Dropping five vectors therefore uncovers five assertions **exactly**, and they are the whole of one
clause:

| assertion | text | now covered by |
|---|---|---|
| `REQ-HBA-007:4197e0` | *WHEN the PLC powers up with no hopper-blockage alarm latched THEN a hopper-blockage alarm requires a full persistence threshold of high-and-running time accumulated after power-up* | ***nothing*** |
| `REQ-HBA-007:7edf6c` | *WHEN the PLC powers up with the hopper-blockage alarm latched from before the power cycle THEN the hopper-blockage alarm is asserted after power-up* | ***nothing*** |
| `REQ-HBA-007:578675` | *WHEN the PLC powers up with the hopper-blockage alarm latched from before the power cycle THEN the inhibit/stop demand output is asserted after power-up* | ***nothing*** |
| `REQ-HBA-007:9855c5` | *WHEN a rising edge of FaultReset occurs after a power cycle while the hopper is still blocked THEN the hopper-blockage alarm is false when next observed* | ***nothing*** |
| `REQ-HBA-007:34dc51` | *WHEN a rising edge of FaultReset after a power cycle clears the hopper-blockage alarm while the hopper remains blocked and the plant runs THEN the hopper-blockage alarm is asserted again WITHIN one further persistence threshold of accumulated high-and-running time* | ***nothing*** |

**Coverage of the enumeration falls from 27/27 to 22/27 (81.5%).** The defect class this loses is named
in the dropped vectors' own `kills` fields: **a retentive accumulator surviving a power cycle**, and **a
latched alarm failing to survive one**. Neither is reachable by any surviving vector, and neither is
reachable by *any* vector this stimulus model can drive. Closing it needs a stimulus surface that can
outlive a CPU restart, which `FB_HopperBlockageStim` is not — this is a **capability request**, not a
vector-authoring task.

### 2. The **reverse** limb of `PlantRunning` is untested in either direction.

`FC_ControlMain` line 92 computes `PlantRunning := Fwd OR Rev`. All 22 survivors reach the block through
`Shredder_Run_Fwd_FB` alone, because the model force-clears the reverse test member for the whole run.
***An implementation that ignored `Shredder_Run_Rev_FB` entirely would pass all 22 vectors.*** No
assertion is formally uncovered by this — the assertions say *"the plant has run"* — but the coverage is
narrower than the assertion text reads, and `REQ-HBA-001:0772fa` names the reverse feedback in its own
trigger. Closing it needs the model to drive the reverse test member, i.e. a change to
`FB_HopperBlockageStim` N22.

### 3. Chatter as a **field** phenomenon is untested.

VB-HBA-006, 008 and 010 exercise a commanded short clear at the input map's test member. Their assertions
are phrased on what `Hopper_Level_High` *reads*, so the assertions **are** covered; contact bounce, wire
break and DI input filtering are not, and the model says so. Recorded so nobody reads a green on these
three as evidence about a real sensor.

---

## WHAT I DELIBERATELY DID NOT DO

- **Did not touch the model.** Not one `represents` entry added, edited or reordered.
- **Did not add behaviours the vectors never asserted.** Three model behaviours these vectors genuinely
  *depend on* are absent from `assertedBehaviours`, and adding them would be authoring rather than
  transcription — the reconciler is not the vector author. Raised here instead:
  - **R6**, the fixed T#5S pre-scenario clear-down. **Every surviving vector runs one**, and VB-HBA-003
    and VB-HBA-026 explicitly lean on it to establish *"no hopper-blockage alarm was previously latched"*
    and *"no FaultReset since the accumulated persistence time was last zero"*. The model's own caveat
    travels with it: R6 is *"a statement about what the model DRIVES, not about the state the monitored
    block ends up in"*, and `doesNotRepresent` disclaims *"the state the monitored block is actually in
    when a scenario starts"*. **A vector author should add R6 and read that caveat.**
  - **R5 mode 1**, *"never asserted inside the scenario"* — the fifteen vectors with `ResetAtMs: "-1"`
    depend on no reset arriving, and did not say so.
  - **R9**, `Stim.Armed` — every `Latched` expectation in `map.providedFor` is armed by it
    (`map._capabilityRequest` (b)).
- **Did not fabricate a `deployment` declaration.** Gate 11 stays NOT CHECKED; that is the rig lane's.
- **Did not edit `map`.** `map.harnessOnly` still declares `HBA_Start_Startup`, which no surviving vector
  names. Left in place: the claim it makes (that signal occupies no PLC storage) is still true, and the
  map is the vector author's artifact. `map.providedFor`'s six signals are all still in use.
- **Did not overwrite either original.** `conformance-vectors-b.json` and
  `conformance-vectors-b-independent-model.json` both stand as the record of what was refused.

## WHAT I COULD NOT VERIFY

- **Whether each narrowed vector still faithfully reads its clause.** That is gate 3c, a judgement gate,
  *"recorded, never verified"*. The (B)3 argument for VB-HBA-016 in particular is reasoning, not a
  measurement.
- **Anything about `ir/`.** This lane is fenced from it (hard rule 8). Every IR fact quoted above is the
  independent declarer's, cited from its own text — not re-derived here.
- **Anything requiring Portal or the rig.** No `openness-cli` command was run; another lane holds the
  token.
- **The runner's `NotSchedulable` refusal of STARTUP vectors.** Taken from the briefing, not re-measured.
  It is a *second* reason for the drop, and the drop does not rest on it.

---

## GATE VERDICT — `conformance-vectors-b-reconciled.json`, 2026-08-17

```
harness-gate.exe check gen/test-project001/hopper-blockage-alarm/conformance-vectors-b-reconciled.json
```

**`VERDICT: NOT ADMISSIBLE` — 22 vector(s) examined, 25 gate(s) run. Exit 1.**

- **Gate 4 fidelity (M4): `[CHECKED]`** — *"every asserted behaviour is in model
  'MDL-HopperBlockageStimulus-1's Represents set."* **This was the blocker and it is closed.**
- **Gate 4b fidelity authority (M3/M4): `[CHECKED]`** — declared by `model-fidelity-declarer-1`, *"who is
  neither the block's author nor any vector's"*. Still clean; the reconciliation did not disturb it.
- **`REFUSED` gates: zero.** Nothing that was passing before is refusing now.
- **`NOT CHECKED`: 2, both unchanged from the refused run** — gate 5 observability (needs the
  coordinator's binding as data; it was NOT CHECKED before this pass too) and gate 11 memory layout
  (needs a `deployment` declaration from the download; the rig lane's, deliberately not fabricated).
- **`JUDGEMENT`: 3** — 3c basis, 6b settling, 8b blacklist (density now 44 entries across 22 vectors).

*** `NOT ADMISSIBLE` IS THE ONLY VERDICT THIS TOOL EMITS. *** *"There is deliberately no plain
ADMISSIBLE: judgement gates can never be verified."* The change here is that gate 4 moved from `REFUSED`
to `CHECKED`; the remaining distance to a runnable wave is the two NOT CHECKED gates, and neither is
closable by editing a vector.
