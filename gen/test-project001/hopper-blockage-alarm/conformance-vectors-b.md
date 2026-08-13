# Conformance vector set B — hopper-blockage alarm

**Author: `vector-author-b-5.2`. 2026-08-13.** The companion to
`conformance-vectors-b.json`. This is the **second, independent** vector set for this block: another
author has produced one, and this one was written without reading it. Agreement between them is
evidence; divergence is where a reader should look.

> **Revision 2 — against enumeration issue 5 (27 assertions).** Issue 4's 25 vectors are unchanged to
> the byte: issue 5 re-hashed nothing, so no citation in this file dangled. **Two vectors were added,
> VB-HBA-026 and VB-HBA-027**, for the two assertions AR-HBA-13 created after this author's
> decomposition dispute was upheld. **Every vector also now declares the bound it was written against**
> — a gate that did not exist when revision 1 was submitted. Both changes are described in their own
> sections below.

## What was read, and what was not

**Read, in full:**

- `gen/test-project001/hopper-blockage-alarm/requirements.md`
- `gen/test-project001/hopper-blockage-alarm/assertion-enumeration.yaml` (issue 4, then **re-read at
  issue 5** — not cached, because a re-issue is exactly where a cached read goes wrong)
- `docs/notes/test-environment-contract.md`
- `docs/notes/assertion-enumeration.md`
- `.claude/skills/design-for-testability/SKILL.md`

**Read under the skill's Step 0** — *verify the verifier before claiming any gate was checked*, which
sends the reader into `src/harness/` and outranks the skill's own gate table:
`Harness.Gate/SubmissionDocument.cs`, `Harness.Gate/GateCli.cs`, `Harness.Results/SubmissionGate.cs`,
`Harness.Results/Admissibility.cs`, `Harness.Results/ObservabilityCheck.cs`,
`Harness.Results/SubmissionVector.cs`, `Harness.Results/StartupTests.cs`,
`Harness.Wire/WireTiming.cs` (`ObservabilityFloorScans`, `ScanPeriodMs`),
`WaveControl/AssertionIdentity.cs`, and at revision 2 `Harness.Results/BoundsCurrency.cs`. These are
the harness's own checkers and constants — none of them says anything about the block under test.

***AND STEP 0 EARNED ITS KEEP A SECOND TIME.*** Between revision 1 and revision 2 the harness grew a
gate — **3i, bounds currency** — and a submission that re-ran on a remembered gate list would have
reported a green it no longer had. Re-verifying against `src/harness/` is what caught it, and it is
why the wire schema is re-read on every submission rather than trusted from the last one.

**NOT read, deliberately:** the implementation (`ir/`), `simatic-ml/`, `architecture.md`,
`review-findings.md`, `integration.md`, `code-structure.md`, `patterns/`,
`docs/notes/test-environment-build-plan.md`, the other author's `conformance-vectors.json` / `.md`,
and every lane report. **Vectors read off an implementation are a change detector, not a correctness
check.**

## The shape of the set

**27 vectors, one per enumerated assertion: every one of the enumeration's 27 assertion IDs is cited
exactly once, and no vector cites anything else.**

🔴 **AND THE WORD FOR WHAT THIS DOES IS NOT "COVERAGE".** `UNCLASSIFIED` is a **computed set
difference** — `enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪ UNTESTABLE-ON-RIG)` — and nobody
ever writes it. Issue 5 added two assertions, so the residual stood at **27 − 25 = 2 and the coverage
gate was correctly failing.** These two vectors are a necessary condition for it to clear, not a
sufficient one: **it clears when somebody CLASSIFIES all 27**, and for OUT-OF-SCOPE and
UNTESTABLE-ON-RIG that identity is the gate-1 architecture signer, **never a vector author** (§4.3).
**No bucket is assigned in this file.** What is reported here is the citation count and its residue:
**27 cited, 0 uncited.**

| slot | start bool | vectors | family |
|---|---|---|---|
| `SLOT-HBA-RAISE` | `HBA_Start_Raise` | VB-HBA-001…004, **026, 027** | raise, the two outputs, standstill, pause-and-resume, **and the threshold's lower pin** |
| `SLOT-HBA-CLEAR` | `HBA_Start_Clear` | VB-HBA-005…010 | debounce and re-arm, running and stopped |
| `SLOT-HBA-LATCH` | `HBA_Start_Latch` | VB-HBA-011…012 | the latch holds |
| `SLOT-HBA-RESET` | `HBA_Start_Reset` | VB-HBA-013…018 | reset, re-raise, held reset, coincident reset, stopped reset |
| `SLOT-HBA-STARTUP` | `HBA_Start_Startup` | VB-HBA-019…023 | power-up / X-F disruptive boundary |
| `SLOT-HBA-PAIR` | `HBA_Start_Pair` | VB-HBA-024…025 | the two outputs agree, both directions |

**0 assertions this author could not write a vector for.** Whether all 27 are *runnable* turns on the
capability requests below, and that is a different question from whether they are *written*.

## The threshold, pinned from both sides — VB-HBA-026 and VB-HBA-027

**These two exist because a dispute this set raised was upheld.** REQ-HBA-002 was *titled* "No alarm
below threshold" while AR-HBA-04 had narrowed its text to the debounced-clear case, so **an
under-scaled preset left all 25 earlier assertions true.** AR-HBA-13 restored the unconditional limb
as `REQ-HBA-002:feecbe` (the alarm) and `REQ-HBA-008:047ac6` (the stop demand).

### Why they are two vectors and not one

The stimulus is identical — `RAISE_UNINTERRUPTED`, the same profile VB-HBA-001 and VB-HBA-002 already
run — which is exactly what makes merging them tempting and wrong.

> ***AN UNDER-SCALED PRESET ASSERTS BOTH OUTPUTS EARLY, AND REQ-HBA-008's TWO TRACKING ASSERTIONS STAY
> TRUE, BECAUSE THE OUTPUTS STILL AGREE. A SIMULTANEITY CLAIM IS SATISFIED BY A SYNCHRONISED ERROR.***

So VB-HBA-024 and VB-HBA-025 are green throughout against a block that raises both outputs at 45 s,
and a single vector watching only the alarm would leave **the operationally worse half** untested: the
inhibit is the output that stops the plant, so an early inhibit stops a plant that is **not** blocked.
AMB-14 does not apply to either — both are single-signal claims — so there is no co-signal to fold in
and no excuse for one vector.

### ***Was the "specified vs the block's own preset" distinction expressible? YES — and at revision 1 it would have been only half-expressible***

Two separate questions, and they got two different answers.

**1. Can the vector be written against the specified value? Yes, mechanically, and it always could
be.** The armed window is computed from **AR-HBA-03's bounds table** — `T#60S`, which at the loaded
scan period of 23.33 ms is 2572 scans — and closes at **58 s = 2486 scans**, two seconds below it. The
block's preset is not read, is not known to this author, and appears nowhere in either vector. A block
whose effective threshold is 45 s asserts inside the armed window and the latch reads `true`.
**Sensitivity, stated rather than implied: this catches any effective threshold shorter than 58 s — an
under-scale of more than 3.3% — and a preset between 58 s and 60 s escapes on the margin.** The 2 s is
scan jitter plus the copy layer running one scan ahead of the block (§9.4).

**2. Can the vector DECLARE that it used the specified value, so that the claim is falsifiable on that
axis? At revision 1, NO — and it is exactly the finding this section was going to carry.** A bare
integer in `windowScans` has no provenance: nothing bound it to the bounds table, and nothing would
have failed if a later author "fixed" a failing vector by sliding the window out to match the block —
**silently converting the assertion back into the tautology the word *specified* exists to prevent,
with every gate still green.**

✅ ***AT REVISION 2 IT IS EXPRESSIBLE, BECAUSE THE HARNESS BUILT THE FIELD: `enumeration.bounds` plus a
per-vector `boundsUsed`, compared by gate 3i.*** That is AMB-19's fix, and the comparison is
**deliberately asymmetric** — bound *names* matched case-insensitively (a wrong name yields a refusal,
so leniency can only turn a refusal into a real comparison) and bound *values* compared ordinally with
case preserved (leniency there would turn a real difference into a pass). It is not a duration parser:
`T#60S` and `T#1M` are reported as different, on the stated ground that *teaching it to equate them
would mean teaching it to equate things.*

**All 27 vectors now declare it, not just the two new ones** — `NotDeclared` fails closed, and a
vector that never records the number it was written against can never be found stale. Gate 3i reports:
*every one of 27 vectors states the bound it was written against, and every one matches the
enumeration's current table.* **A retune of `T#60S` now refuses this submission instead of silently
changing what it tests.**

**One residue, and it is small:** the enumeration's `bounds:` table carries provenance prose beside
the value (`T#60S  (Q-HBA-01, owner) — the SPECIFIED value, per AR-HBA-13`) and the field wants the
bare `T#60S`, so a human extraction stands between the two. That is the same trust `normalisedTexts`
already rests on, and it fails in the safe direction: a mis-transcription surfaces as a loud STALE
naming both strings, never as a silent pass.

### The other side of the pin

**An over-scaled preset was exactly as invisible as an under-scaled one**, and it is caught by a
vector that was already there: **VB-HBA-001**'s scenario ends at 75 s and its 65–75 s sample expects
`true`, so any effective threshold longer than 75 s reads `false` and fails. That number came from the
register's table too. So the boundary is now pinned from below by VB-HBA-026/027 and from above by
VB-HBA-001/002 — **and no number in any of the four came from the block.**

### What F-3 costs VB-HBA-027, declared rather than glossed

VB-HBA-026 cites a `WHEN`, so it carries **two** expectations: a latch over 1–58 s expecting `false`
*and* a sample over 65–75 s expecting `true`. The second is a **positive control** and is not
decoration — without it, a block whose alarm output is dead passes the prohibition trivially and the
vector measures uptime.

VB-HBA-027 cites a `NEVER`, so **F-3 refuses sampling for the whole vector** and a second latch on the
same signal would need a second arming window the copy layer does not provide. **So in isolation
VB-HBA-027 is satisfied vacuously by a dead inhibit output.** It is non-vacuous *as a set* —
VB-HBA-002 proves that output does assert at the threshold, VB-HBA-024 proves it holds while the alarm
is up. **A reader who takes VB-HBA-027's green on its own has read it wrongly**, and that is a real
cost of F-3 rather than a defect in it.

## Why almost everything here is SAMPLED, when the skill says prefer LATCHED

This is the design decision the whole set turns on, and it deserves the argument in full.

**The block's alarm latch is RETAIN and is cleared only by a `FaultReset` edge** (REQ-HBA-004,
REQ-HBA-007). So *every* scenario has to begin by driving the block into a known clear state — a
**clear-down**: hopper low, plant stopped, a reset pulse. And a clear-down takes seconds.

**A vector cannot supply it before T=0.** Contract §2.1 rules that a vector's `Inputs` are written
**once**, before T=0, while nothing is running, and D33/D37 put exactly one scan between the inert
establish and the commit. One scan is not a clear-down. So the clear-down must be a **phase of the
in-PLC stimulus model, after T=0** — which puts a legitimately-asserted alarm *inside the test
window*.

***AND THAT DESTROYS A FREE-RUNNING LATCH IN BOTH DIRECTIONS.*** A copy-layer latch cleared at test
start refills from the clear-down, so it reads `true` on every vector regardless of the block: useless
for a positive claim, and actively false for a negative one.

Two ways out, and this set uses both:

1. **For `WHEN` assertions — sample late.** Each scenario is built so the assertion's claim is decided
   by the time the observation window opens, and the window is the last 10–20 s. **A late sample is
   decisive here for a reason peculiar to this block: nothing but a `FaultReset` edge can clear the
   alarm, so a negative read at the end also rules out a raise earlier in the test.** The windows are
   429–858 scans against a floor of **8.6 scans** — 50× to 100× the floor, and about 4.5× the largest
   poll gap ever measured (2,216 ms ≈ 95 scans). The tail this project rightly fears cannot reach
   them.
   🔴 **The dependency is declared, not hidden:** those negative vectors lean on REQ-HBA-004's latch
   discipline holding. A block that *both* accrues wrongly *and* fails to latch could pass one of
   them. VB-HBA-011 is what makes that combination visible, and a FAIL there invalidates the negative
   reads elsewhere.
2. **For `NEVER` assertions — latch the FORBIDDEN STATE, armed by the model's phase flag.** F-3
   refuses a sampled `NEVER` outright, and the refusal applies to *every* expectation in the vector,
   not just the interesting one. So the four `NEVER` vectors (VB-HBA-011, 016, 024, 025) are latched
   throughout, and each carries a copy-layer latch of the conjunction the assertion forbids, armed
   after the clear-down.

**Latching a forbidden state IS what the `NEVER` form means**, and it is the only instrument immune to
the tail. It is also the only honest answer to AMB-14: two independent latches say each signal went
true at some point; only the conjunction latch says they **agreed throughout**.

### The capability requests this set depends on

All three are **copy-layer instrumentation (D13)**. **None is a change to the block under test, so
contract §9.1 is not engaged** — nothing here asks `lad-coder` to expose anything.

1. `Sampled` **and** `Latched` on `HopperBlockedAlarm` and `HopperBlockedInhibit`.
2. ***THE LATCHES MUST BE ARMED BY THE STIMULUS MODEL'S PHASE FLAG*** (`HBA_Stim.Armed`), not
   free-running from T=0. Without arming, see the argument above — every latched observation in this
   set is worthless.
3. **Four violation latches**, each latching a state computed in the copy layer:
   `HBA_Violation_AlarmFellWithoutReset`, `HBA_Violation_AlarmLowUnderHeldReset`,
   `HBA_Violation_InhibitLowWhileAlarmHigh`, `HBA_Violation_InhibitHighWhileAlarmLow`.

**And the honest label on the map:** `map.providedFor` in the JSON was **written by this author**. §4.3
says the map is the coordinator's, built from the copy layer it generated, and the declaration must
exist *before* the download that generates it. No copy layer exists for this block, so what is in the
file is the **request**, and gate 5 checked against it is checked against this author's own claim. It
becomes a check when the coordinator replaces it.

The same label belongs on `model` and on `assertedBehaviours`: **the stimulus model does not exist**.
Contract §2.1 rules that dynamic stimulus belongs to a model running in the controller, so every
multi-phase scenario here needs one, and gate 4's set-difference is currently a declaration compared
with itself.

## The gate run

`dotnet run --project src/harness/Harness.Gate -- check gen/test-project001/hopper-blockage-alarm/conformance-vectors-b.json`

**`VERDICT: NOT ADMISSIBLE` — exit 1. 27 vectors examined, 22 gates run.** That is the correct
outcome and it was not avoidable by anything a vector author could write:

| outcome | gates |
|---|---|
| **CHECKED and passed** (16) | 1 schema · 2 authorship (D6) · 3 basis · 3d enumerator independence · 3e form authority · 3f citation shape · 3g IDs recompute · 3h required observations (AMB-14) · **3i bounds currency (AMB-19)** · 4 fidelity · 5 observability · 6 settling · 7 start bool · 9 liveness preconditions · 10a assertion ceiling · 10b timer/model/ratio ceilings |
| **JUDGEMENT** (3) | 3c faithful reading · 6b settling sufficiency · 8b blacklist breadth (density **54 entries across 27 vectors** — see below) |
| ***NOT CHECKED*** (3) | **8 blacklist** and **8c multi-writer provenance** — both need D9's computed conflict graph, i.e. `converter cross-check --project ir/test-project001`, and this author is fenced from `ir/`. **11 memory layout** — needs a `deployment` declaration and a tag map, both properties of a DOWNLOAD that has not happened. |

***THE THREE NOT-CHECKED GATES WERE NOT FAKED, AND THE TEMPTATION WAS REAL AND SPECIFIC.*** Writing
`"computedConflicts": []` would have turned gates 8 and 8c green by typing seven characters — because
an empty array is read as *"the graph ran and found no conflicts"*, a claim nobody here is entitled to
make. Likewise `"s7Objects": []` is the positive claim that no classic-S7comm path reaches a data
block, and this submission cannot make it either. **Absent and empty are different facts and the gate
keeps them apart; this file does too.**

**Two refusals were hit and fixed across the two revisions, and both are reported rather than hidden.**
*(a)* Revision 1's first run exited **2, NOTHING EXAMINED** — a documentation note placed inside
`enumeration.requiredObservations` broke the `Dictionary<string, List<string>>` deserialisation, and
the CLI correctly reported the whole document unreadable rather than skipping the field. The note was
moved beside the map, not deleted. *(b)* Revision 2's first run reported **3i NOT CHECKED** on all 27
vectors — the gate did not exist when revision 1 was written, and neither `enumeration.bounds` nor any
`boundsUsed` was present. **Supplying them was not a workaround: it is the declaration the enumerator
asked for**, and it turned a NOT CHECKED into a CHECKED by adding a fact, not by relaxing anything.

### Blacklist density: 50 entries, and it is a real judgement call

Every vector excludes `FB_ShredderSequencer` and `FB_PusherControl`. The reason is specific and comes
from the register's own reuse-first section: **the stimulus model drives `Hopper_Level_High`, the
shredder running feedbacks and `DB_Controls.FaultReset`, and all three are consumed elsewhere in the
corpus** — `FaultReset` is the *shared* operator reset already routed to the sequencer and pusher
faults, so a pulse on it clears their latches too, and VB-HBA-016 holds it true for 90 seconds.

That is coupling through plant-model state, which §7 says is exactly what a blacklist is for. **But
without the computed graph I cannot tell whether these two are already computed conflicts** — in which
case the entries are redundant (harmless, since the blacklist can only add). Naming a *block* excludes
*every slot testing it*, which here is the whole set. **If the coordinator's graph already carries
them, drop all 50 entries; nothing else changes.**

## The decomposition dispute — ✅ RAISED AT REVISION 1, UPHELD AS AR-HBA-13, CLOSED AT REVISION 2

> ***NO ASSERTION IN THE ENUMERATION FORBIDS THE ALARM RAISING **EARLY** ON AN UNINTERRUPTED FIRST
> EPISODE.***

**Outcome: the enumeration re-issued at 27** — REQ-HBA-002's limb 1 restored alongside AR-HBA-04's
limb 2, both live, **with zero re-hashes**, so no citation in this file dangled. The two new
assertions are covered by VB-HBA-026 and VB-HBA-027 above. The record of the original dispute is kept
below unchanged, because *how* it was found is the part worth keeping: **what found it was somebody
trying to WRITE A TEST for the behaviour the title promised**, and no consistency pass could have —
the text was internally consistent, it was simply missing a claim.

REQ-HBA-001's assertions all say what happens **at or after** the threshold. REQ-HBA-002 is titled
*"No alarm below threshold"* — but after AR-HBA-04's rewording its **text** conditions the whole claim
on a **debounced clear** having intervened. REQ-HBA-003's four assertions are all about what happens
**after** a clear. So the plain sub-threshold case — *hopper high, plant running, 30 s of a 60 s
threshold, no clear at any point* — **is asserted by nothing.**

**Raised as an event, per `assertion-enumeration.md` §2.3, and NOT written into any `Basis`.** It is
for the enumerator to rule on, not this author.

- **Severity: medium, and it is partly covered by accident.** VB-HBA-005 and VB-HBA-008 both catch a
  *badly* short timer, because their scenarios put 50 s and 55 s of high-and-running before the clear.
  What survives is a timer short by a *little* — say 45 s of a 60 s threshold — which no vector in
  this set can fail, because no assertion says it is wrong.
- **The isolating defect exists**, which is §1's own test for a missing assertion: a block whose
  persistence preset is under-scaled (`T#45S`, or a `T#60S` compared against the wrong time base)
  raises early on a genuinely-blocked-but-not-yet-blocked-long-enough hopper, and every one of the 25
  assertions stays true.
- **What I would expect it to look like:** `WHEN Hopper_Level_High has been continuously true and the
  plant has run continuously for less than the persistence threshold time THEN the hopper-blockage
  alarm is not asserted` — R1's negative case of `001.A1`, which R1 says is *"the one most often
  lost"*. Offered as a description of the gap, not as a citable assertion; a vector author who writes
  one has re-created the correlated check.

## Underspecified — stated as gaps, not filled in

**These are things the register, the contract or the wire format does not determine. None of them was
decided here.**

1. **THE BLOCK HAS NO COMPLETION SIGNAL, AND §2.2 REQUIRES ONE.** Contract §2.2 says `completionSignal`
   is *"the block's own done-signal"*, and a continuous supervisory alarm monitor **has none** — there
   is no state in which it is finished. Every vector here declares `HBA_Scenario_Done` = 1, **the
   stimulus model's** completion flag, not the block's. It is the honest answer (the test is over when
   the scenario has played out) and it is a **deviation from the contract's wording**, reported rather
   than absorbed. The schema gate prints the completion condition on a pass for exactly this reason.
2. **THE SETTLING CONDITION CANNOT BE EVALUATED, IN ANY SUBMISSION.** `SettlingDeclaration` carries
   `UnchangedForScans` — described in the code as *"the one settling form this harness can
   EVALUATE"* — and **`VectorDocument` has no field for it**, so `GateCli.ToVector` constructs every
   settling declaration with `0`. **Every vector in every submission therefore returns
   `SettlingState.NotEstablished`**, which is deliberately not the same as settled. Each vector here
   states its condition as *"unchanged across 43 consecutive scans"* so the number is there to be
   read; **the wire format has nowhere to put it.** This is a schema gap, not a vector defect, and it
   sits squarely on §5.1's recorded gap that `WaveRun` observes completion and not settling.
3. **`expected` IS A STRING COMPARED FOR EQUALITY, SO THE CANONICAL FORM'S `WITHIN` CLAUSE CANNOT BE
   EXPRESSED.** `assertion-enumeration.md` §1.1 admits `WHEN … THEN … WITHIN <bound>`, and R3 makes a
   timing bound *an attribute of the response*. There is no range, tolerance or predicate operator in
   the format. `REQ-HBA-007:34dc51` is the assertion that needs one (*"asserted again WITHIN one
   further persistence threshold"*), and VB-HBA-023 expresses the bound as **the arming window of a
   latch** — the bound becomes the window and the predicate becomes `true`. That works, and it works
   only because a latch happens to be available; a sampled `WITHIN` has no equivalent.
4. **THE STARTUP CLASS CANNOT BE DECLARED IN THE DOCUMENT.** `StartupSchedule` is real, and it refuses
   `Sampled` **and** `Stamped` evidence (X-F: the harness is disconnected across the download, D18) and
   refuses a startup vector that also appears in the ordinary wave set. But *"there is no test class
   field on a vector, on purpose"* — the class is expressed by which argument the vector is passed as.
   **So this JSON cannot say that VB-HBA-019…023 are startup vectors.** They are collected in
   `SLOT-HBA-STARTUP`, latched-only, and **the coordinator must pass that slot as `startupVectors` and
   must NOT pack it into an ordinary tensor.** A slot name is a convention, not a gate.
5. 🔴 **WHETHER THE X-F BOUNDARY PRESERVES RETAIN MEMORY IS UNESTABLISHED, AND FOUR VECTORS DEPEND ON
   IT.** X-F's boundary is *a full download that stops and restarts the CPU*. REQ-HBA-007's whole
   content is that the **RETAIN** alarm latch survives a power cycle while the non-retentive
   accumulator does not. If a full download clears retentive memory, then VB-HBA-020, 021, 022 and 023
   cannot ride that boundary at all and need a genuine power cycle — a different and more expensive
   proposition than the one X-F's economy rests on. **I could not settle this from my inputs. Nobody
   should read a green on those four until it is settled.**
6. **PRECONDITIONS AND ORDERING BETWEEN VECTORS ARE NOT EXPRESSIBLE.** VB-HBA-020…023 need an alarm
   latched *before* the boundary; every other vector needs one *not* latched. `index` within a slot is
   *"its position in the slot's column"* and is the only ordering there is — this set uses it, and that
   is a **reading**, not a guarantee that the runner honours it as a dependency.
7. **The `comp` question is left open rather than answered.** `runtimeCompression` is **1**, so the
   whole set spends roughly 40 minutes of plant time. Raising it needs `blockCompression` — the
   block's dwell presets, each declared `Data` or `Literal`, a field with **no fail-safe guess** — and
   the presets are a property of the block, which this author is fenced from. The gate's own note says
   a 500 ms preset caps compression at **4.3×**; this block's shortest preset is the **2 s**
   clear-debounce filter and its longest is the **60 s** threshold, so somebody who can read the block
   should compute this. **An unknown ceiling is not a high one.**
8. **A defect in the enumeration's gate projection, found by using it.** `assertion-enumeration.yaml`'s
   `gate_projection_template` keys `forms:` by **display ordinal** (`REQ-HBA-001.A1: When`) under a
   comment reading *"# id -> form"*. Gate 3e looks the form up **by ID**, so that projection makes
   gate 3e report *"the enumeration carries forms but none for `<id>`"* on all 25 citations — a
   NOT-CHECKED dressed as a supplied field. **Re-keyed by ID in this submission; reported, not fixed
   in the enumeration.** All 25 IDs were independently recomputed from their own normalised text
   before citation (`SHA-256`, first 6 hex, clause-scoped): **25 checked, 0 mismatches**, and gate 3g
   agrees.

## The three flags the enumeration says must not be dropped

### AMB-14 — the relational assertion's second signal

**Touched by VB-HBA-024 (`REQ-HBA-008:d97781`) and VB-HBA-025 (`REQ-HBA-008:741315`)** — the corpus's
only two relational assertions.

✅ ***AND THE FLAG CAN BE CLOSED: THE WIRE FORMAT NOW CARRIES IT.*** The enumeration records AMB-14
as *"a limit in the enumeration format"* whose fix would be for `ResponseSignal` to become a set, and
notes that `also_requires_observation_of:` is something *"the wire format cannot carry"*. **It can.**
`EnumerationDocument.RequiredObservations` is a `Dictionary<string, List<string>>` — a list per
assertion, explicitly built for AMB-14 — and **gate 3h** consumes it and refuses a citation that does
not observe every signal its assertion depends on. This submission supplies it, and gate 3h ran
**CHECKED** across all 25 citations.

**So the residual is a re-issue, not a schema change:** the enumeration should emit
`requiredObservations` (response signal merged with `also_requires_observation_of`) in its
`gate_projection_template`. **The enumerator's own instinct not to fix it by editing assertion text
was right** and is preserved here: no signal name entered a hashed sentence, and this issue re-hashes
nothing.

Both vectors go further than the mechanical check. Gate 3h only proves both signals were *observed* —
mentioning a signal is not testing it. The **conjunction latch** in each vector is what tests the
relation itself.

### The priced cost of an owner reversal on Q-HBA-04

The enumeration prices it at **1 re-hash, 1 inversion, 1 knock-on**. Mapped onto this set:

| enumeration's item | vector | what happens to it |
|---|---|---|
| `001.A3` — **re-hash** (drop *"and no hopper-blockage alarm was previously latched"*) | **VB-HBA-003** | its citation goes **STALE** and must be re-pointed. The scenario, the window and the expected `false` are all unaffected — the trigger loses a clause the clear-down was satisfying anyway. Cheapest of the three. |
| `005.A6` — ***INVERTED, not reworded*** | **VB-HBA-018** | ***THE `expected` VALUE FLIPS FROM `true` TO `false`.*** This is the single place in the submission whose **predicate** turns on an open owner question, and a reversal makes a passing vector into a failing one against an unchanged block. Flagged in the vector's own `_observability` note so it cannot be found by surprise. |
| `004.A1` — **knock-on** (a latched alarm would have to DROP when the plant stops, contradicting REQ-HBA-004) | **VB-HBA-011** | the `NEVER` becomes unstatable in its present form and the violation latch's condition changes. The enumeration calls this an objection *"regardless of any agent ruling"*, and it is the one that would need the owner twice. |

**Total exposure to this set: one stale citation, one inverted predicate, one rewritten violation
latch.** Unchanged from the enumeration's own pricing, which is what I would expect and is worth
saying out loud — a divergence there would have meant one of us had misread the register.

### NEW-HBA-05 — still OPEN, and it decides two vectors

The mechanism question (TON + a non-retentive accumulator, per C-406, versus a single TONR needing a
documented C-406 exception) is unresolved, and **AR-HBA-10 depends on the accumulator staying
non-retentive.**

- **VB-HBA-022** (`REQ-HBA-007:9855c5`) — the enumeration names `007.A4` as *"the assertion that fails
  first if NEW-HBA-05 is ever settled in a way that makes the accumulated time retentive"*. This vector
  is that assertion. **A FAIL here is as likely to be a specification change as a defect and must be
  diagnosed against NEW-HBA-05 before the block is touched.**
- **VB-HBA-019** (`REQ-HBA-007:4197e0`) — asserts the non-retention directly: 40 s accrued before the
  boundary plus 40 s after it must **not** reach the threshold. A retentive accumulator fails it 20 s
  into the armed window.

Neither is resolved here. Both are written against the register **as it stands**, which is the only
defensible reading available to a vector author.

## Mutation — what each vector kills

Every vector carries a `kills` string naming a credible wrong implementation; the schema gate refuses a
vector without one. **None has been shown to go red against its mutant**, because no mutant has been
built and no wave has been run — *a vector only ever run against a correct implementation is in the
same state as a guard that has never been executed.* The strongest claims in the set, by the defect
they isolate:

| defect | caught only by |
|---|---|
| the reset-on-stop accumulator (the one a merged reading of REQ-HBA-001 hid) | VB-HBA-004 |
| accrual not gated on running feedback — a full hopper at standstill earns an alarm | VB-HBA-003 |
| the freeze preserving an accumulator across a stop in which the hopper emptied | VB-HBA-009 |
| the over-correction: any hopper-low during a stop discarding the budget | VB-HBA-010 |
| a level-sensitive `FaultReset` pinning the alarm off — **every pulse-driven vector stays green** | VB-HBA-016 |
| the reset consuming the threshold crossing — a live fault permanently suppressed | VB-HBA-017 |
| the re-raise gated on `PlantRunning` — stop, then acknowledge, clears a live fault | VB-HBA-018 |
| the stale stop demand: inhibit left asserted after the alarm clears | VB-HBA-014 |
| the alarm clearing and never coming back after a post-power-cycle reset | VB-HBA-023 |
| a retentivity mismatch between the two outputs | VB-HBA-021 |
| **an under-scaled preset raising the alarm early** — the most ordinary commissioning error there is | **VB-HBA-026** |
| **the same under-scaled preset asserting the STOP DEMAND early, which no tracking assertion can ever catch** | **VB-HBA-027** |
| an over-scaled preset — as invisible as an under-scaled one under the old reading | VB-HBA-001 (upper pin) |

## Escalations

1. **§9.1 is not engaged by this set, and that was a design goal.** Nothing here asks for a change to
   the block's interface. Everything asked for is copy-layer instrumentation (D13). **If the
   coordinator cannot provide phase-armed latches, the four `NEVER` vectors have no admissible
   instrument at all** — F-3 refuses sampling for them — and *that* would escalate to §9.1. It should
   be answered before the download that generates the copy layer, because the declaration is frozen
   for the wave set.
2. **§9.4 is avoided rather than resolved.** No vector in this set uses `Stamped`, and none asserts on
   a scan difference. VB-HBA-017's coincidence is in the **stimulus**, never in the observation —
   which is exactly what AR-HBA-11 reworded AR-HBA-05 to permit. **But the stimulus coincidence is
   approximate:** the model's 60 s and the block's 60 s are two timers, so the edge lands within a scan
   or two of the crossing. Genuinely establishing the coincident case needs the edge swept across
   crossing−1, crossing, crossing+1 — three positions, which under §2.1 means three vectors and there
   is one assertion to cite. **Raised, not resolved.**
3. **8c is NOT CHECKED, so nothing has been said about multi-writers on this block's outputs.** If the
   graph, when supplied, reports a multi-writer finding on `HopperBlockedAlarm` or
   `HopperBlockedInhibit`, X-G's treatment is *reported, not refused* — **and whether it should instead
   be a hard refusal is an open owner question.** Flagged, not settled.
4. 🔴 **AMB-15 sits directly under VB-HBA-022 and VB-HBA-023, and it is UNSETTLED.** Limb 1's
   precondition is *"no `FaultReset` since the accumulated persistence time was last zero"*, and
   AR-HBA-10's sequence zeroes the accumulator and *then* takes a reset — so on the "zeroing event"
   reading limb 1 does **not** bite inside the post-power-cycle window, and an under-scaled preset is
   **uncatchable there**: `007.A5` is a `WITHIN` bound, an UPPER limit, which a short preset satisfies
   *more* easily. **VB-HBA-026 and VB-HBA-027 do not reach into that window** — their scenario is an
   ordinary first episode, deliberately, because the trigger they cite is ambiguous there and a vector
   should not resolve a clause's ambiguity by choosing a scenario. If AMB-15 is ruled the "zeroing
   event" way, the enumeration needs one further assertion per output (denominator 29) and this set
   needs two more vectors.
5. **AMB-16 is the general form of the dispute I raised, and it is still open.** AR-HBA-13 says limb 1
   means the **specified** threshold; **22 other assertions say "the persistence threshold" and nothing
   says which threshold those mean.** Every vector in this file was written against the specified value
   and now declares it (gate 3i), so **this set has already taken the reading AMB-16 recommends** — but
   it took it as an author's choice, not on a ruling. If the preset reading is intended anywhere, those
   assertions must be reworded and every citation to them dangles.
6. **D6 is satisfied by a string comparison.** `vector-author-b-5.2` ≠ `lad-coder` ≠
   `assertion-enumerator`, normalised. What actually *makes* two agents different is undefined
   anywhere. This set was written from the specification alone, which is the substance the string is
   standing in for.

## How to read a green from this set — and what it will not license

A PASS here says **these 27 assertions held, under a model that does not yet exist, at a fidelity its
author has not yet declared, at comp = 1, against the bounds table as it stood when the submission was
made.** It says nothing about the settling of any observed value (`NotEstablished`, by schema), nothing
about the co-running slice being benign, and — until AMB-15 is ruled — nothing about an under-scaled
preset *inside AR-HBA-10's post-power-cycle window*, which is the one place the early-raise defect may
still survive. **And `STALE` is the verdict to watch for**: every vector here
runs a scenario tens of seconds long, and a mirror nothing ever wrote to is perfectly self-consistent.
Read the verdict, not the exit code.
