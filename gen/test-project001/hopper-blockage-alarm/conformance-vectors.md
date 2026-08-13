# CONFORMANCE VECTOR SET — hopper-blockage alarm (phase 5.2)

**Author: `vector-author-5.2`. Submission: `conformance-vectors.json`. Written 2026-08-13.**

The submission is the artifact; this file is the reasoning the JSON has no field for — the scenario
behind each vector, the judgement calls, the coverage classification, and the escalations.

- **Denominator:** `assertion-enumeration.yaml` issue 4, stamped — **25 assertions over 8 clauses**,
  produced by `assertion-enumerator`, a third party to both the block's author and to me.
- **Register:** `requirements.md` at 12 agent rulings (`AR-HBA-01…12`), 8 REQ clauses.
- **Vectors:** 25, one per assertion. Every `Basis` cites the clause **and** the content-derived
  assertion ID. No citation uses a display ordinal.

---

## 🔴 0. AUTHOR CONTAMINATION — READ THIS BEFORE READING ANYTHING ELSE

***I READ THE PREDICTED SPEC-VERSUS-BLOCK DIVERGENCES. THIS WAS NOT SANCTIONED AND IT CANNOT BE
UNDONE.***

**What happened.** My brief named five things I must not read (`ir/`, `architecture.md`,
`review-findings.md`, `integration.md`, `simatic-ml/`) and I read none of them. While checking whether a
PLC-side stimulus model exists — a question §2.1 forces on every vector in this set — I read
`docs/notes/test-environment-build-plan.md` lines 160–260. **That section contains the divergence table
predicted in advance**, including one of the block's actual interface names and a quotation from one of
its network comments. It was not on the prohibition list and I did not expect it there.

**When.** After reading the register and the enumeration in full; **before** writing a single vector.

**What I did not read:** any `.ir` file, the architecture, the review findings, the integration note, any
SimaticML, and nothing further from that document once I recognised what it was.

**What limits the damage, stated as fact and not as reassurance:**

1. **The vector set's shape was fixed by the denominator, not by me.** 25 assertions, one vector each.
   There was no discretion about *what* to test.
2. ***EVERY `kills` STRING IS SOURCED FROM THE ENUMERATION'S OWN `split_defect:` FIELD*** — spec-side,
   written by the enumerator, who provably had not read the block. That is a mechanical claim anybody
   can check by diffing my `kills` against the YAML. Where I added to a `split_defect` I said so in the
   string (V-HBA-001-A1's complement note, V-HBA-008-A2's).
3. **I expected on the SPECIFICATION's signal names throughout.** `HopperBlockedInhibit` appears in
   eleven expectations. AR-HBA-01's abstention exists exactly so that a name mismatch is a finding about
   the block, and renaming the spec to match the block is not an available resolution — so it is not one
   I took.

**What is genuinely lost.** The claim *"the vectors were written by an agent that had never seen
anything about the implementation"* is no longer true of this submission, and the milestone's
independence argument is weaker than it was designed to be. **That is the coordinator's call, not
mine.** The clean remedy is a fresh vector author re-deriving the set from the same two artifacts; the
cheap partial remedy is the `kills`-versus-`split_defect` diff above. **I am deliberately not naming
which of my vectors correspond to the divergences** — that would compound the leak, and the run should
find what it finds.

---

## 1. The one structural constraint that shapes every vector: §2.1

***A SUBMITTED VECTOR'S `inputs` ARE WRITTEN ONCE, BEFORE T=0. THERE IS NO STEPPING, AND THE CONTRACT
SAYS THAT IS ARCHITECTURAL RATHER THAN A SCHEMA LIMIT.***

Nineteen of these 25 assertions describe a **sequence** — hopper high, then a clear of a stated
duration, then high again; running, then not running, then running. Under §2.1 a PC-side vector cannot
supply that, and the contract's own answer is that **dynamic stimulus belongs to a model running in the
controller, on the controller's own timebase**.

So this submission declares the model it needs — `M-HBA-STIM-01` — and every vector's `inputs` are that
model's **scenario parameters**, written once, before T=0, exactly as §2.1 requires.

### 1.1 🔴 The model and the map are DECLARED BY ME, WHICH MAKES TWO GATES SELF-REFERENTIAL

**Gate 4 (fidelity) and gate 5 (observability) both reported CHECKED and both passed. Neither result is
worth what it looks like, and the reason is the same for both: the artifact each was compared against is
one I wrote.**

| | what the gate compared against | who wrote it | what that means |
|---|---|---|---|
| **gate 4** | `model.represents` | **me** | a set-difference against my own list. It cannot fail while I keep the two in step |
| **gate 5** | `map.providedFor` | **me** | the skill's own open question #5: *"a submission in which the vector's own author typed it is a declaration checked against itself. Ask which it was."* **It was me.** |

**Why I supplied them anyway rather than omitting them.** Omitting the map makes gate 5 refuse every
expectation with `NothingToCheckAgainst` and takes gates 9 and 10a down with it — twenty-one gates
collapse into one uninformative refusal, and nothing else about the submission gets examined. Supplying
them keeps every other gate meaningful. **The right reading of the two greens is therefore: these are
the REQUIREMENTS this submission places on the model author and on the copy layer, not evidence that
either is satisfied.** §4.3 says the map's declarations must exist *before* the download that generates
the copy layer, so somebody has to write them first; what must not happen is the coordinator reading
this pair of greens as confirmation.

**Owed, and both must be re-run against the real artifacts before any result from this set is quoted:**

- `M-HBA-STIM-01` does not exist. Its required capability set is `model.represents` in the JSON. Its
  `compStable` is undeclared (harmless at `runtimeCompression = 1`, blocking above it).
- Every `Stim_*` input name is a **PROPOSED** model parameter. They are harness-side, not project tags,
  so hard rule 3 is not in play — but the model author owns the final names, and a rename is an edit to
  this submission, never to the register.

### 1.2 The scenario parameters, so the model author has a specification

| parameter | meaning |
|---|---|
| `Stim_PreClearMs` | hold `Hopper_Level_High` false for this long during the **inert phase**, before T=0 |
| `Stim_PreResetPulse` | pulse `FaultReset` during the inert phase, before T=0 |
| `Stim_HopperInitialHigh` | hopper state at T=0 |
| `Stim_ClearAtMs` / `Stim_ClearDurationMs` | one hopper-low episode, at this offset from T=0, for this long. **Duration `0` means "for the remainder of the scenario"** |
| `Stim_RunFbInitial` | both shredder run feedbacks at T=0 |
| `Stim_RunLossAtMs` / `Stim_RunLossDurationMs` | one running-feedback loss, same convention (`0` = to the end) |
| `Stim_ResetAtMs` / `Stim_ResetPulseMs` | one `FaultReset` rising edge at this offset, held for this long |
| `Stim_ResetHeld` | hold `FaultReset` continuously true for the whole scenario |
| `Stim_ResetOnCrossing` | emit the `FaultReset` edge at the model's **own** 60 s of accumulated high-and-running |
| `Stim_PreBoundaryAccrualMs` / `Stim_PreBoundaryLeaveAlarmLatched` | startup and post-boundary vectors only — the state to establish **before** the disruptive boundary |
| `Stim_TotalMs` | scenario length; `StimScenarioDone := 1` at the end |

### 1.3 🔴 PRE-1 — the block carries RETAIN state, so consecutive vectors are not independent

REQ-HBA-007 requires the alarm latch to **survive a power cycle**, and AR-HBA-07 makes the re-raise key
on the accumulator alone. **Both therefore survive the end of a test.** A vector run after one that
raised the alarm starts with the alarm latched and the accumulator past the threshold — and
`V-HBA-001-A3`'s trigger explicitly says *"and no hopper-blockage alarm was previously latched"*.

Left unhandled, **the second vector in the column measures the first one's leftovers.**

Handled here, and declared rather than assumed: every ordinary vector carries
`Stim_PreClearMs = 5000` and `Stim_PreResetPulse = true`, applied in the inert phase before T=0. The
pair is chosen from the specification, not from convenience — AR-HBA-05 says `FaultReset` does **not**
touch the accumulator, so a reset pulse alone would leave it loaded; a **debounced clear** is what
discards it (REQ-HBA-003), and AR-HBA-09 states the debounce filter runs irrespective of
`PlantRunning`, which is what makes a 5 s clear effective in an inert phase with the plant stopped.

**The startup and post-boundary vectors deliberately do the opposite** (`Stim_PreResetPulse = false`,
`Stim_PreBoundaryLeaveAlarmLatched = true`) — for them the retained state *is* the precondition.

---

## 2. Observability — the declaration, and why each mode was chosen

**Floor: 8.6 scans**, computed by the gate from `slotsInWaveSet = 3` and
`resultRegistersPerSlot = 8` at `runtimeCompression = 1`. It is not restated as a constant anywhere in
the submission; every sampled window is 800 scans, ~93x the floor.

**`runtimeCompression = 1`, deliberately.** X-D's rule is `comp_min`, never `comp_max`; nothing here
needs compressing, no wave has ever run compressed, and `model.compStable` is undeclared. At comp 1
gate 10b is a **real computed pass** rather than an assumption. The cost is wave time: the column is
about 25 minutes of scenario. **If the coordinator wants this compressed, `blockCompression` must be
supplied and each preset's `source` (`Data` or `Literal`) decided — and that is a question for the
block's author, not for me: the register gives the values (`T#60S`, `T#2S`) but not how they are
held.**

### 2.1 Mode by mode

- **LATCHED wherever the claim is "did it happen (or not) at all"** — 16 of 25 vectors. Preferred for
  the reason the skill gives: a latch cannot fall in a poll gap, and a small but real fraction of gaps
  are enormous.
- **SAMPLED only where a latch is structurally unable to answer** — 9 vectors, all of them post-reset
  states. Once the alarm has been raised earlier in the same scenario the latch is already set, so
  *"false when next observed and stays false"* cannot be read from it. All nine cite `When` assertions,
  so **F-3 is not engaged**; all nine declare 800-scan windows over states that hold for 19–25 s.
- **STAMPED: not used anywhere, deliberately.** §9.4's off-by-one is unresolved and the skill says not
  to admit an assertion that turns on it. Nothing here needs to.

### 2.2 🔴 FMT-1 — the map declares a MODE, never what the latch CAPTURES

Four vectors (`004-A1`, `005-A4`, `006-A1`, `008-A1`) need an observation of the form ***"the signal
went LOW after having been high"*** — a fall-latch. That is the only thing that can answer a
*never-de-asserts* claim, because F-3 forbids sampling a `Never` and a rise-latch is already set.

They declare it as `nature: Transient, mode: Latched, expected: "false"` on the signal itself.
**Mechanically the gate accepts it, and it should not be read as having checked it**: `map.providedFor`
carries `signal → [modes]` and has no way to say whether the copy layer's latch on
`HopperBlockedAlarm` captures the rise or the fall. **A rise-latch supplied where a fall-latch was meant
returns `true` where the vector expects `false`, and the result is a FAIL that blames the block for the
copy layer.** Same shape as AMB-14, one axis over.

***It is a schema question — a polarity field, or two named observables — and it must be settled before
the copy layer is generated, because the declaration is frozen for the wave set.*** Raised to the
coordinator, not resolved here.

### 2.3 🔴 OBS-1 — one direction of REQ-HBA-008 A2 is genuinely unobservable, and I did not paper over it

The enumeration's named isolating defect for `REQ-HBA-008:741315` is a **set-only inhibit latch**: it
follows the alarm up and never comes down. Catching it requires observing the inhibit **after** an
alarm-clearing reset — and at that point in the scenario:

- a rise-latch on the inhibit is already set, so LATCHED cannot answer;
- the assertion is a `Never`, so **F-3 refuses SAMPLED**;
- a two-signal simultaneity is not a thing any of the three modes expresses.

**So `V-HBA-008-A2` is written against a different, also-credible mutant** — an inhibit driven from a
condition broader than the alarm — using the plant-stopped scenario in which the alarm is never
asserted at all. Its `kills` says so in as many words. ***The set-only-latch defect is caught by
`V-HBA-005-A2` instead***, which is a `When` assertion and may therefore sample.

I am recording this rather than quietly relying on the overlap, because *"assertion cited, vector
green"* and *"assertion tested"* are the same report otherwise — §5's inflation route 5, arriving by
observability rather than by carelessness. **Per §9.1 this is an ESCALATION, not a task:** the obvious
fix is a new copy-layer observable, and whether one may be created is open with the owner.

---

## 3. The vector set

Slot / index / assertion / mode. Full scenarios are in the JSON's `inputs`; the timings below are the
reasoning.

### `SLOT-HBA` — 20 ordinary vectors, start bool `HbaSlotStart`

| # | vector | assertion | scenario (times from T=0) | expects |
|---|---|---|---|---|
| 0 | `V-HBA-001-A1` | `001:ab68c1` | high + running, 75 s | alarm latch **true** |
| 1 | `V-HBA-001-A2` | `001:8ea7f6` | same | inhibit latch **true** |
| 2 | `V-HBA-001-A3` | `001:0772fa` | high, **both run feedbacks false**, 75 s | alarm latch **false** |
| 3 | `V-HBA-001-A4` | `001:2bdfa7` | high throughout; running lost 30→50 s | alarm latch **true** (60 s cumulative reached at 80 s) |
| 4 | `V-HBA-002-A1` | `002:efe6b5` | high 0→40, clear 40→45 (≥2 s), high 45→75 | alarm latch **false** (correct: 30 s; carrying defect: 70 s) |
| 5 | `V-HBA-002-A2` | `002:bfda9d` | high 0→30, **1 s** glitch, high 31→66 | alarm latch **true** (65 s cumulative) |
| 6 | `V-HBA-003-A1` | `003:607fdf` | high 0→55, clear 55→58, high 58→108 | alarm latch **false** — scenario ends 50 s into the second episode, so only a carried accumulator can have alarmed |
| 7 | `V-HBA-003-A2` | `003:652dd6` | high 0→55, **1 s** glitch, high 56→66 | alarm latch **true** |
| 8 | `V-HBA-003-A3` | `003:aa1d87` | high; stop 40→46; clear 41→44 **while stopped**; run 46→80 | alarm latch **false** — AR-HBA-06's worked case, 40 s discarded |
| 9 | `V-HBA-003-A4` | `003:09e3a8` | high; stop 55→60; **1 s** glitch at 56 while stopped; run 60→70 | alarm latch **true** — 55 s frozen, not discarded (AR-HBA-09) |
| 10 | `V-HBA-004-A1` | `004:14c9b6` | high + running 90 s, no reset | alarm latch **true** (positive control) **+ fall-latch false** |
| 11 | `V-HBA-005-A1` | `005:0463ce` | high→65, clear 65→end, reset edge 75 | alarm **sampled false**, 800 scans |
| 12 | `V-HBA-005-A2` | `005:7e39bc` | same | inhibit **sampled false**, 800 scans |
| 13 | `V-HBA-005-A3` | `005:179eb7` | high + running, reset edge 70 | alarm **sampled true** — a zeroing reset reads false for 60 s |
| 14 | `V-HBA-005-A4` | `005:6e1e8d` | high + running 90 s, **`FaultReset` held true throughout** | alarm latch **true** + fall-latch false |
| 15 | `V-HBA-005-A5` | `005:34fd97` | high + running; reset edge **at the model's own threshold crossing** | alarm **sampled true** |
| 16 | `V-HBA-005-A6` | `005:e7e0e0` | high; running lost at 65 to the end; reset edge 75 | alarm **sampled true** — the stop→acknowledge sequence |
| 17 | `V-HBA-006-A1` | `006:469954` | high→65, then low to the end, **no reset** | alarm **sampled true** + fall-latch false |
| 18 | `V-HBA-008-A1` | `008:d97781` | high + running 90 s | alarm latch true, inhibit latch true, **inhibit fall-latch false** |
| 19 | `V-HBA-008-A2` | `008:741315` | high, plant stopped, 90 s | alarm latch **false**, inhibit latch **false** |

### `SLOT-HBA-STARTUP` — 3 vectors, start bool `HbaStartupStart`

X-F: startup tests ride the disruptive boundary's own CPU restart, are **not** packed into ordinary
tensors, and **every expectation is LATCHED** — the harness is disconnected across the download, so
first-scan evidence is read from the latch after reconnect.

| # | vector | assertion | pre-boundary state | post-restart scenario | expects |
|---|---|---|---|---|---|
| 0 | `V-HBA-007-A1` | `007:4197e0` | 50 s accrued, **no** alarm | high + running 40 s | alarm latch **false** |
| 1 | `V-HBA-007-A2` | `007:7edf6c` | alarm **latched** | hopper low, plant stopped, 20 s | alarm latch **true** |
| 2 | `V-HBA-007-A3` | `007:578675` | alarm **latched** | same | inhibit latch **true** |

**A2 and A3 hold the hopper low and the plant stopped on purpose**: a non-retentive block must not be
able to re-earn the alarm during the observation and pass by accident.

### `SLOT-HBA-POSTBOUNDARY` — 2 vectors, start bool `HbaPostBoundaryStart`

| # | vector | assertion | scenario | expects |
|---|---|---|---|---|
| 0 | `V-HBA-007-A4` | `007:9855c5` | alarm latched pre-boundary; after restart hopper high + running; reset edge at 10 s; ends at 40 s | alarm **sampled false** (window lies wholly inside 10→70 s) |
| 1 | `V-HBA-007-A5` | `007:34dc51` | same, run to 95 s | alarm **sampled true** (window lies wholly after the 70 s deadline) |

### 3.1 🔴 SCH-1 — two scheduling facts the submission format cannot express

1. **`SLOT-HBA-POSTBOUNDARY` is not a startup class and must not be scheduled as one.** Its evidence is
   live, after reconnect, following a reset edge 10 s into RUN — so `StartupTests` would refuse both
   vectors with `EvidenceNotLatched`, correctly and for the wrong reason. What they need is not
   first-scan latching but *"run in the wave immediately following a disruptive boundary"*, **and no
   field says that.**
2. **Five vectors require pre-boundary state** (`Stim_PreBoundaryAccrualMs`,
   `Stim_PreBoundaryLeaveAlarmLatched`). A submission has no way to say *"this vector depends on the
   state a previous wave left"*, and I have expressed it as model parameters because that was the only
   place it would go.

**And the precondition under all five is an open question I cannot answer: does the disruptive boundary
— a full download — PRESERVE RETAIN memory?** REQ-HBA-007 is about a **power cycle**; the boundary is a
download-driven stop/restart. If a download reinitialises retentive data, then `007:7edf6c`,
`007:578675`, `007:9855c5` and `007:34dc51` are **not covered by these vectors** and route to the
gate-1 signer. *(`007:4197e0` survives either way — it asserts the accumulator does **not** persist.)*

---

## 4. Coverage classification

***UNCLASSIFIED IS COMPUTED, NEVER WRITTEN:*** `enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪
UNTESTABLE-ON-RIG)`.

| bucket | count | assigner |
|---|---|---|
| **COVERED** | **25** | `vector-author-5.2` — a vector exists and cites it. Permitted: §4.3 restricts only the two escape hatches |
| **OUT-OF-SCOPE** | **0** | — |
| **DEFERRED** | **0** | — |
| **UNTESTABLE-ON-RIG** | **0** | — |
| ***UNCLASSIFIED*** | ***0*** | computed |

**Oldest deferral age: n/a — there are no deferrals.** *(§5 route 6: the four counts and this figure are
quoted together because a bare percentage is not an available output.)*

**Every one of the 25 is COVERED and none is bucketed, so no escape-hatch assignment was made and §4.3's
interested-party check has nothing to catch.** That is the honest state — and it is also the fragile
one, for two reasons stated rather than left to be discovered:

1. ***COVERED MEANS "A VECTOR CITES IT", NOT "IT WILL BE TESTED".*** Gate 3h confirms each vector
   observes every signal its assertion depends on; that it genuinely exercises the assertion is
   judgement (§6), and OBS-1 above is one place where I say plainly that it does not fully.
2. **Five of the 25 are conditional on SCH-1's open question.** If the boundary cannot establish their
   preconditions, `007:7edf6c` / `007:578675` / `007:9855c5` / `007:34dc51` lose their coverage and
   **UNCLASSIFIED becomes 4 until the gate-1 signer buckets them.** I cannot pre-empt that: those two
   buckets are not mine to assign, and neither is DEFERRED, which needs an owner and a date.

### 4.1 No decomposition dispute is raised — and that is a checked result, not a silence

I looked for a response the specification demands and the enumeration does not carry, clause by clause,
including the four places the enumeration itself explains an absence (`why_no_per_case_inhibit_siblings`,
`not_split_further` on `001.A4`, the two `overlap_with_REQ_HBA_008` notes). **In every case the
enumeration's reasoning holds and the response is carried somewhere.** Specifically checked and found
already covered: the inhibit's behaviour at re-raise (`008:d97781`), the inhibit's return inside
AR-HBA-10's bounded window (`008:d97781` again), the clear-debounce filter running irrespective of
`PlantRunning` (isolated by `003:aa1d87`), and the post-power-cycle clear for the **inhibit**
(`005:7e39bc`, which the enumeration deliberately made route-agnostic).

**Nothing was resolved by writing prose into a `Basis`.** The three items I do raise — FMT-1, OBS-1,
SCH-1 — are limits in the **submission format and the copy layer's vocabulary**, not in the
decomposition, so they route to the coordinator and not to the enumerator.

---

## 5. The three carried flags

### 5.1 AMB-14 (open) — and the enumeration's note on it is now STALE

AMB-14 says a simultaneity claim has two signals while `ResponseSignal` is one string, and that
`also_requires_observation_of:` is *"what the wire format cannot carry"*.

***THE WIRE FORMAT NOW CARRIES IT.*** `EnumerationDocument.RequiredObservations` is a
`signal-list` per assertion, and gate **3h** reports:

> `[CHECKED] 3h required observations (AMB-14) — every citation observes EVERY signal its assertion
> depends on, across 25 cited vector(s).`

**Affected vectors: `V-HBA-008-A1` and `V-HBA-008-A2`**, the only citations of relational assertions.
Both declare expectations on **both** `HopperBlockedInhibit` and `HopperBlockedAlarm`, so the
mechanical half of AMB-14 is closed for this submission — I supplied
`requiredObservations` by merging the YAML's `response_signal:` and `also_requires_observation_of:`,
which is the merge the field was built for.

**The judgement half is not closed, and OBS-1 is exactly it:** observing both signals is not the same as
testing the relation between them. AMB-14's option 1 has been implemented; its residual risk stands.

### 5.2 Q-HBA-04 — the priced owner reversal, and which of my vectors it moves

Q-HBA-04's parenthetical (*"a full hopper at standstill does not alarm"*) is an **owner** answer that
outranks AR-HBA-07. The enumeration prices the reversal at **1 re-hash, 1 inversion, 1 knock-on**.
Mapped onto this submission:

| enumeration's exposure | my vector | what happens if the owner asserts the strong reading |
|---|---|---|
| `001.A3` re-hashes | **`V-HBA-001-A3`** | its citation goes **STALE**. The vector needs re-issuing against the new ID; the scenario itself is unchanged |
| `005.A6` inverted | **`V-HBA-005-A6`** | **the vector is wrong, not stale.** Its expectation flips from `true` to `false`, or the vector is withdrawn with the assertion |
| `004.A1` knock-on | **`V-HBA-004-A1`** | a run-state exception would be needed; the latched-alarm-must-not-drop claim this vector tests is what the objection is about |

**`V-HBA-008-A2` uses the plant-stopped scenario but is NOT exposed** — its assertions are relational
(*the two outputs agree*), so they survive an inversion of what the alarm does at standstill. The
enumeration checked this; I re-checked it against the scenario I actually wrote.

### 5.3 NEW-HBA-05 (open) — `007.A4` is the assertion that fails first

The enumeration names `REQ-HBA-007:9855c5` as the assertion that fails first if NEW-HBA-05 is ever
settled in a way that makes the accumulated time **retentive**. Its vector is **`V-HBA-007-A4`**, and
its `kills` string is that exact mutant.

***SO A RED FROM `V-HBA-007-A4` IS AMBIGUOUS BY CONSTRUCTION AND MUST NOT BE READ AS A BLOCK DEFECT
WITHOUT CHECKING NEW-HBA-05 FIRST.*** If the accumulator is retentive by design, the failure is
AR-HBA-10's dependency firing — a spec event — and AR-HBA-10 must be revisited rather than the block
changed.

---

## 6. Gate result — `harness-gate check`, run 2026-08-13

```
dotnet run --project src/harness/Harness.Gate -- check gen/test-project001/hopper-blockage-alarm/conformance-vectors.json
```

**`VERDICT: NOT ADMISSIBLE` · exit 1 · 25 vectors examined, 21 gates run.**

**Every gate that could run, ran and PASSED.** The verdict is `NOT ADMISSIBLE` because three gates are
`NOT CHECKED`, and NOT CHECKED fails closed with no flag that relaxes it.

| | gate | outcome |
|---|---|---|
| 1 | schema | **CHECKED** — every §2 field present and typed |
| 2 | authorship (D6) | **CHECKED** — `vector-author-5.2` ≠ `lad-coder`, normalised |
| 3 | basis — clause AND assertion | **CHECKED** — 25 citations into 25 assertions |
| 3c | faithful reading of the clause | **JUDGEMENT** — §4.1 above is my record of it |
| 3d | enumerator independence | **CHECKED** — `assertion-enumerator` is neither author |
| 3e | assertion form authority | **CHECKED** — 21 `When`, 4 `Never`, all matching the enumeration |
| 3f | citation shape | **CHECKED** — no display ordinals |
| 3g | assertion IDs recompute | **CHECKED** — **all 25 recompute from their own normalised text** |
| 3h | required observations (AMB-14) | **CHECKED** — every citation observes every signal it depends on |
| 4 | fidelity (M4) | **CHECKED** — *and see §1.1: compared against a model I declared* |
| 5 | observability | **CHECKED** — floor 8.6 scans at comp 1 — *and see §1.1 and FMT-1* |
| 6 | settling | **CHECKED** — none is the completion flag alone |
| 6b | does settling imply finality | **JUDGEMENT** — §6.1 below |
| 7 | start bool (submission half) | **CHECKED** — one per slot, all bound by name |
| 8 | blacklist | ***NOT CHECKED*** — no conflict graph supplied |
| 8b | over-broad? | **JUDGEMENT** — 75 entries across 25 vectors; §6.2 below |
| 8c | multi-writer provenance (X-G) | ***NOT CHECKED*** — no provenanced graph supplied |
| 9 | liveness preconditions | **CHECKED** |
| 10a | assertion ceiling (X-D) | **CHECKED** — comp 1 is under every ceiling; the sampled vectors compute 92.86x |
| 10b | timer / model / ratio ceilings | **CHECKED — a real computed pass at comp 1** |
| 11 | memory layout (§4.5) | ***NOT CHECKED*** — no `deployment` declared |

### 6.1 The three NOT CHECKED gates — all three belong to another artifact

Per §2.4 these are **fix that artifact**, not **fix the vector**, and I did not fabricate any of them.

- **8 and 8c — the conflict graph.** It comes from `converter cross-check --project ir/test-project001`,
  which reads the IR. ***I am forbidden to read the IR and I did not run it.*** `computedConflicts: []`
  would be the positive claim *"the graph ran and found no conflicts"*, which I cannot make.
  **Owed by the coordinator**, who can run it in one command; with `conflictEdges` carrying X-G
  provenance, both gates close together.
- **11 — `deployment`.** §2.4: a property of the **download**, and *"resubmitting the vector cannot
  supply it"*. It needs the `importStamp` of the import that generated the copy layer, plus every
  `(area, dbNumber)` the S7 tag map can reach. **Owed by the deployment lane.** Note that
  `s7Objects: []` is a *claim* about the deployment, not a default, so I did not write one.

### 6.2 The judgement calls, stated as such

- **3c — faithfulness.** I read all 8 clauses and all 12 rulings before any vector. Each vector's
  scenario is derived from the clause plus the rulings that amend it; §3's table gives the derivation so
  a reviewer can disagree with a specific one. **`003:aa1d87` is the one I would look at hardest** — the
  clause states an internal state transition (*discards the accumulated time*) and the enumeration
  restates it as the alarm's subsequent timing, flagging that restatement as a judgement. My scenario
  inherits it.
- **6b — settling.** Every condition is *"the value has been unchanged for 40 consecutive scans AND the
  stimulus model reports its scenario finished"*. The second half is a **model** signal, not the block's
  done-signal, so phase 2's `Done`-at-10-ramps-to-15 defect is not reachable through it. For latched
  observations the argument is stronger than 40 scans: **once the scenario ends nothing drives the
  block's inputs, so a latch cannot change again.** *Weakness, stated: a block with an internal timer
  still running after the scenario ends could still move a sampled value. 40 scans (~0.9 s) does not
  exclude a 60 s timer.* The nine sampled vectors mitigate this by placing their windows well inside a
  region the specification says is stable — but this is judgement, and it is the weakest one here.
- **8b — blacklist breadth.** Three entries on every vector: `FB_ShredderSequencer`, `FB_PusherControl`,
  `FC_ControlMain`. **Every reason is sourced from the register's own reuse-first section**, which
  records those blocks as the consumers of `Hopper_Level_High` and the routing of `FaultReset` — not
  from the IR, which I did not read. Density is 75/25 = 3.0 per vector; since the blacklist names
  **blocks** while admission colours **slots**, this excludes every slot testing any of the three. I
  believe it is right rather than defensive: this wave set drives two shared DB members that those
  blocks read. **A conflict graph would let it be checked rather than argued** — see 8/8c.

### 6.3 🔴 Step 0 — I verified the verifiers, and the skill's own gate table is out of date

Confirmed present under `src/harness/`, by reading the source: `SubmissionGate.Check` (every gate),
`Admissibility.Check`, `ObservabilityCheck.Evaluate`, `AssertionId`, `AssertionEnumeration`,
`ConflictGraph`, `TimeCompression`, `TagMapReach`/`DeploymentDeclaration`, `WireTiming`,
`StartupTests` — and `harness-gate check` runs and produces per-gate output.

**Two corrections the skill's table owes:**

1. ***GATE 11 EXISTS.*** The skill says *"NOTHING YET … `SubmissionGate` has no gate 11 and
   `SubmissionDocument` has no `deployment` field. Do not report this as checked because the table lists
   it."* Both now exist: `SubmissionGate.MemoryLayout` performs the set-difference against
   `TagMapReach`, and `SubmissionDocument.Deployment` carries §4.5's object. It reported NOT CHECKED
   here **because my submission declares no `deployment`** — a different fact from *no verifier exists*,
   and the difference is exactly the one the skill's own three-outcome table is built on.
2. **Three gates are absent from the table:** `3f citation shape`, `3g assertion IDs recompute`, and
   `3h required observations (AMB-14)`. All three ran and passed here. 3g is the one worth naming — it
   is what keeps the stamper untrusted, and a report that never mentions it has not established that
   the IDs it cites were computed rather than typed.

---

## 7. Mutation — what each vector kills, and what has NOT been shown

**Every vector carries a `kills`, sourced from the enumeration's own `split_defect:` for the assertion
it cites** (§0.2 above). The gate checks the field is non-empty; that it names a *credible* mutant is
judgement, and the enumerator's independence is what makes it worth something.

***WHAT IS NOT DONE, AND IT IS THE STEP-3 REQUIREMENT: NO VECTOR HAS BEEN SHOWN TO GO RED AGAINST ITS
MUTANT.*** Nothing has run — there is no copy layer, no model, and no deployment. A vector only ever
run against a correct implementation is in the same state as a guard that has never been executed, and
this set has not been run at all. **The first rig session is the first execution, and until then the
mutation claims are arguments, not evidence.**

Three places where the mutation argument is weaker than the rest and I would not want it read as even:

1. **`V-HBA-001-A1`** kills *"never asserts"* but not *"asserts too early"* — a latch cannot express
   *not before 60 s*. The complement is carried by `V-HBA-001-A3`, `V-HBA-002-A1` and `V-HBA-003-A1`,
   which hold the latch at false through scenarios a premature raise would set. **The set kills it; this
   vector does not.**
2. **`V-HBA-008-A2`** — OBS-1. It kills a different mutant from the one the enumeration names.
3. **`V-HBA-005-A5`** — *"coincident"* is the model's own 60 s of accumulation, which may differ from
   the block's by a scan. The observable (*the alarm is true when next observed*, sampled over 800
   scans) is insensitive to that; what a misalignment costs is mutant-hitting precision, not the
   verdict. **This is the closest anything here comes to §9.4, and it deliberately does not turn on a
   stamp.**

---

## 8. Escalations — in priority order

| # | item | who |
|---|---|---|
| **E1** | ***AUTHOR CONTAMINATION (§0).*** The predicted divergences were read before the vectors were written. Decide whether this submission stands, or a fresh author re-derives it | **coordinator / owner** |
| **E2** | The **model** `M-HBA-STIM-01` does not exist. Its required capabilities are `model.represents`; §2.1 makes it the only admissible route to dynamic stimulus | **model author** |
| **E3** | The **map** was written by me (§1.1). Gate 5's green is a declaration checked against itself. §4.3: it must be settled **before** the download that generates the copy layer | **coordinator** |
| **E4** | **FMT-1** — the map cannot say whether a latch captures the rise or the fall. Four vectors need a fall-latch. Schema question, and frozen at the same download boundary | **coordinator** |
| **E5** | **SCH-1** — no field expresses *"run in the wave immediately after a disruptive boundary"*, and **whether that boundary preserves RETAIN memory is unanswered**. Five vectors depend on it | **coordinator / rig** |
| **E6** | **OBS-1 / §9.1** — one direction of `008:741315` is unobservable with the three declared modes. The obvious fix is a new copy-layer observable, which is **open with the owner** | **owner** |
| **E7** | **PRE-1** — the block's RETAIN state crosses test boundaries. Handled by an inert-phase debounced clear + reset pulse; needs the model to implement it, and the runner to honour the inert phase | **model author / coordinator** |
| **E8** | Gates **8/8c** need the conflict graph and **11** needs `deployment`. Neither is a vector-side fix | **coordinator / deployment lane** |
| **E9** | The enumeration's AMB-14 note says the wire format *cannot* carry `also_requires_observation_of:`. **It now can** (§5.1) — worth correcting so the next reader does not re-derive the limitation | **enumerator** |
| **E10** | The skill's gate table is stale on gate 11 and omits 3f/3g/3h (§6.3) | **5.1 lane** |
