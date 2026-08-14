---
name: design-for-testability
description: 'Phase 5.1 design-for-testability gate — checks a test vector or wave submission against docs/notes/test-environment-contract.md before it is submitted: vector format, Basis citing clause AND assertion, the observability declaration, settling, start-bool binding, blacklist, authorship (D6) and model fidelity (M4). Use whenever asked to write, review or submit a conformance test vector, to check whether a block is testable or observable, "is this vector admissible", "why was my vector refused", or before any wave submission. Also use when reading a result package — it says what PASS, FAIL, TIMED-OUT, UNSETTLED, STALE and REFUSED each do and do not license. Ladder-AI project.'
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Bash(dotnet run --project src/harness/Harness.Gate -- check *)
  - Bash(src/harness/Harness.Gate/bin/*/net8.0/harness-gate check *)
---

# /design-for-testability — the phase 5.1 admissibility gate

Ladder-AI project. The contract is `docs/notes/test-environment-contract.md`; this skill is its
enforcement half. **Read the contract this run — never from memory.** It is a DRAFT and it carries
pointers rather than constants on purpose, so a remembered figure is a wrong figure.

Read `CLAUDE.md` first if you have not. Hard rules apply: this skill **never edits a block** and never
writes observability code into one (D13/§2.1 put instrumentation in the copy layer). It produces an
admissibility report.

---

## The one property that makes this skill worth having

> ***A GATE NOBODY CAN RUN IS NOT A GATE, AND SAYING IT PASSED IS WORSE THAN SAYING NOTHING.***

Three outcomes per gate, and **they must never be collapsed into two**:

| outcome | means | counts as |
|---|---|---|
| **CHECKED** | a verifier ran against this submission's actual content | a result |
| **JUDGEMENT** | no verifier can exist for this; an independent reader decided | a result, **labelled** |
| ***NOT CHECKED*** | ***no verifier exists yet, or the input it needs is absent*** | ***NOT A RESULT — the submission is NOT ADMISSIBLE*** |

**NOT CHECKED fails closed.** There is no flag that relaxes it, deliberately: §10's precedent is phase
2's `AddressesExamined`, a rule that was correct while nothing established it had run, and *a gate with
a skip flag is a gate that will be skipped at 2 a.m.*

**Empty is not clean** (FI-44). Zero vectors, zero assertions, an empty assertion enumeration, a
blacklist compared against an absent graph, a fidelity declaration that claims nothing — each is
**NOTHING EXAMINED and a refusal**, never a pass.

*The same defect was live in `converter review` until 2026-08-13: eighteen rules reported "not
applicable" against a tag table and the run exited 0, so an unreviewed file was indistinguishable from
a clean one. Do not re-create it here.*

---

## Step 0 — ***verify the verifier, before claiming any gate was checked***

**The table in Step 1 is a claim about code, established on 2026-08-13. It will go stale.** Another
lane is building the harness right now. So do not trust it: for every gate you are about to call
CHECKED, confirm the verifier still exists and is reachable —

- `Grep` for the type or method the table names, under `src/harness/`;
- confirm there is a **command you can actually run**. A C# method is not a check an author can
  perform. If the only way to reach it is to write a program, the gate is **NOT CHECKED**.

If you find a verifier the table does not list, use it, and say the table is out of date. If a listed
one has gone, say that too. **Never report a gate as checked because this file said a tool existed.**

---

## Step 1 — the gates, and what actually checks each one

Contract §10's surface, in the order a submission meets it, with the **verifier reality on
2026-08-13**. `Admissibility.Check` is `src/harness/Harness.Results/Admissibility.cs`.

| # | gate | what the contract requires | verifier today | outcome |
|---|---|---|---|---|
| 1 | **schema** | every §2 field present and typed; `MaxDuration` non-empty | `SubmissionGate` — **`harness-gate check`** | **CHECKED** |
| 2 | **authorship (D6)** | vector author ≠ block author | `AgentIdentity` — **normalised**, so a case/whitespace variant no longer passes it | **CHECKED** |
| 3 | **basis — clause** | resolves to written text | `Admissibility` against `AssertionEnumeration`, populated from the submission's `enumeration` block | **CHECKED** |
| 3b | **basis — assertion** | ID drawn from the spec-derived enumeration, never free text | same. An empty enumeration is a **refusal**. ⚠️ **The tool emits 3 and 3b as ONE line** — `3 basis — clause AND assertion` — so do not go looking for a `3b` in its output and do not report one as missing | **CHECKED** |
| 3c | *is the assertion a faithful reading of the clause?* | — | **nothing, ever** | **JUDGEMENT** |
| **3d** | **enumerator independence** | the enumeration's author ≠ the block's author and ≠ any vector's author — otherwise D6 is lost **at the denominator** and citing into it buys nothing | `AgentIdentity`, **normalised** — measured: `"AGENT-B "` against a vector author `agent-b` is **REFUSED**, so a case or trailing-space variant does not slip past | **CHECKED** *(NOT CHECKED without `enumeration.enumerator`)* |
| **3e** | **assertion form authority** | the vector's declared `assertionForm` must match the form the **enumeration** records for the assertion it cites | `AssertionEnumeration` — the enumeration is the authority, so the vector can no longer declare its own form and take the permissive path | **CHECKED** *(NOT CHECKED without `enumeration.forms`)* |
| **3f** | **citation shape** | the citation is the content-derived ID (`<clause>:<six lowercase hex>`), **never the display ordinal** | `AssertionId`. An ordinal is **POSITIONAL** — inserting one assertion above it silently makes the citation name a different one | **CHECKED** |
| **3g** | **assertion IDs recompute** | every ID re-hashes from the `normalisedTexts` it claims to come from | `AssertionId.Compute`. ***This recomputation is the ONLY reason the stamper is permitted to have no independence from the block or vector author*** — without it a hand-written or altered hex string is indistinguishable from a computed one, **and the omission is exactly what a compromised stamper would emit** | **CHECKED** *(NOT CHECKED without `enumeration.normalisedTexts`)* |
| **3h** | **required observations (AMB-14)** | every signal the cited assertion depends on appears in this vector's `Expectations` | `AssertionEnumeration.RequiredObservationsOf` — set difference. **A relational assertion names more than one**: expect on one output, never observe the other, and the relation is untested while everything reports green | **CHECKED** *(NOT CHECKED without `enumeration.requiredObservations`)* |
| **3i** | **bounds currency (AMB-19)** — contract **§2.5** | each vector's `boundsUsed` value matches `enumeration.bounds` | `BoundsCurrencyCheck`. ***A mismatch REFUSES and is reported as `STALE`, NEVER `FAIL`***; a vector declaring no bound is **NOT CHECKED and keeps that status beside a stale sibling** | **CHECKED** *(NOT CHECKED without `boundsUsed` or `enumeration.bounds`)* |
| 4 | **fidelity (M4)** | asserted behaviours ⊆ the model's `Represents` | `Admissibility` — set difference | **CHECKED** |
| 5 | **observability** | mode valid; signal in the map; window ≥ §12a derivation 1's floor **at the run-time `comp`**; declared before the generating download | ***`ObservabilityCheck` — A COMPUTATION.*** It was a caller-supplied `bool`; it is now derived from the vector's declared nature+mode, what the MAP provides, and the floor `harness-gate` computes from the wave set | **CHECKED** |
| 6 | **settling exists, and is not the completion flag** | both halves | `Admissibility` | **CHECKED** |
| 6b | *does the condition really imply the value is final?* | — | **nothing, ever** | **JUDGEMENT** |
| 7 | **start bool** | exactly one per slot; bound by NAME; raised a later scan than the inert-establish, against the **observed** counter | `SubmissionGate` for the first two. **The later-scan rule is RUN-TIME** (`InertPhase`) and is reported as such, never claimed at submission | **CHECKED** (submission half) |
| 8 | **blacklist** | add-only against computed disjointness; every entry has a reason | `SubmissionGate`. ***Add-only is a property of the TYPE*** — `BlacklistEntry` carries no negation, so a removal cannot be expressed. **An ABSENT conflict graph is NOT CHECKED**, not a pass | **CHECKED** *(NOT CHECKED without `conflictEdges`, which needs `map.storage` — see below)* |
| 8b | *is it over-broad?* | — | density, reported not gated | **JUDGEMENT** |
| **8c** | **multi-writer provenance (X-G)** | every conflict edge records WHY two blocks conflict, on which signal, and whether that signal SHIPS | `ConflictGraph`. ***The teeth are on the ABSENCE of provenance***: a multi-writer FINDING on a deliverable signal is **reported, not refused** (the defect is in the deliverable, and a vector author cannot fix it), while an **unprovenanced graph is NOT CHECKED** — *"0 findings"* and *"nobody recorded why"* are the same empty report | **CHECKED** *(NOT CHECKED without provenance on every edge)* |
| 9 | **liveness** *(post-run)* | stimulus check; counter advanced **by the expected amount**; manifest presence | `StimulusCheck`, at run time. **The separable half is now a submission gate**: a vector whose liveness could never be established is refused *before* a wave is spent | **CHECKED** (preconditions) **+ CHECKED at run time** |
| **10a** | **time compression — assertion ceiling (X-D)** | the run-time `comp` is under every vector's own `T_event / scan_period` ceiling | `TimeCompression`. ***It catches what gate 5 structurally cannot:*** gate 5 exempts LATCHED and STAMPED from the observability floor, correctly — **X-D does not exempt them from the SCAN-PERIOD term.** An event compressed below one scan is not long enough to be latched either, so a latched declaration that clears gate 5 at every factor can still be void at the one being run | **CHECKED** |
| **10b** | **time compression — timer / model / ratio ceilings (X-D)** | at `runtimeCompression` > 1: the block's timer presets (`PT / (k x scan)` — the term **X-D says often binds first**), the model's `comp_stable`, and the ratio-distortion bound on unscaled literals | `TimeCompression.Plan`, from contract **§2.3**'s `blockCompression` + `model.compStable`. **At `comp` = 1 this is a REAL computed pass** (nothing is scaled, so none of the three can bind). **Above 1 with the inputs absent it is NOT CHECKED and fails closed** | **CHECKED** *(NOT CHECKED without `blockCompression`, above comp 1)* |
| **11** | **memory layout (§4.5)** | every `(area, dbNumber)` a classic-S7comm path can reach names a **harness-generated** object, declares `Standard` (or `NotApplicable`), and its `layoutSetAfterImport` equals the current `importStamp` | `SubmissionGate.MemoryLayout` against the deployment declaration **and the tag map's reach** — so a row naming an object that SHIPS is ***the invariant broken, not a finding***. An unstated layout is refused: absence means "no opinion" and **TIA resolves no opinion to Optimized** | **CHECKED** *(NOT CHECKED without `deployment`, or without an `importStamp` beside a non-empty `s7Objects`)* |

### The command

```
dotnet run --project src/harness/Harness.Gate -- check <submission.json>
```

`exit 0` = ADMISSIBLE-SUBJECT-TO-JUDGEMENT · `exit 1` = NOT ADMISSIBLE · `exit 2` = NOTHING EXAMINED.
***There is deliberately no plain ADMISSIBLE.*** Run it, and paste its per-gate output into your report
rather than restating it — the report's job is the judgement calls and the escalations, which the tool
cannot make.

**`exit 2` is its own code and never 0.** An empty submission, an unreadable document or a mode nothing
implements is *nothing examined*, and a gate that exits 0 on those is the purest form of the failure
this skill exists to prevent.

### An absent field is decided per case, and never defaulted

***"THE FIELD WAS ABSENT" MUST NOT QUIETLY BECOME "THE CHECK PASSED" — NOR AUTOMATICALLY "REFUSED".***
The two are different facts and the gate distinguishes them. Measured on the built exe:

| what is absent | outcome | why that one |
|---|---|---|
| `enumeration.enumerator` | ***NOT CHECKED*** | a property of the **enumeration**, which a resubmission of the *vector* cannot fix. Reporting it refused would send an author to edit the wrong artifact |
| `enumeration.forms` | ***NOT CHECKED*** | same — the flat projection simply carries no forms to be the authority |
| a vector's `assertionForm` (i.e. `Unstated`) | ***REFUSED*** | the vector had one field to fill and left it |
| a form for the **cited** assertion, or `Unstated` on either side | ***REFUSED*** | the comparison cannot be made, and an uncomparable form is not a passing one |
| an expectation's `expected` (the predicate) | ***REFUSED*** | measured: a null predicate became the literal `<no predicate>`, was compared against the observed value, and produced a **FAIL** — *a vector that never said what right looks like told its author the block was wrong* |
| `completionValue` / `completionSignal` | ***REFUSED*** | the same shape one field over: an unstated value yields **`TIMED-OUT`** on a healthy block. **Contract §2.2 removed the default of 1** — a near-universal default is what makes the rare `Step = 90` block invisible |
| `kills` | ***REFUSED*** | §10 requires mutation and this is the only mechanism there is |
| any `inputs` value or `completionValue` **outside its element's range** | ***REFUSED BY NAME, never a modulo*** | §2.6. The refusal prints *what it would have become*: `75000` arrives as **`9464`** — a plausible dwell nobody questions — and every boundary keyed on it fires early, returning a confident `FAIL` against a correct block. **Measured: 81 duration values in the deliverable set exceed 65 535 ms** |
| an `inputs` value present but **empty** | ***REFUSED*** | ***zero is a value a block could legitimately be driven with***, so supplying one invents the stimulus |
| a signal the vector says **nothing about** | **not checked — legitimate** | an undriven input is the *inert declaration's* business. **Absent is not present-and-empty** |
| a mirrored signal's **element type** | ***REFUSED*** | the table's zero value is `Unstated`; nothing can be range-checked against a type with no width |
| a **completion signal wider than one register** | ***REFUSED as INEXPRESSIBLE*** | comparing it tests the **high half** and reports `TIMED-OUT` forever on a block that finished — and ***a spurious `TIMED-OUT` is worse than a spurious `FAIL`, because it is believed*** |
| the conflict graph (both `computedConflicts` and `conflictEdges`) | ***NOT CHECKED*** | a blacklist compared against an absent graph is a blacklist nobody checked |
| provenance on any conflict edge | ***NOT CHECKED*** | *"0 multi-writer findings"* and *"nobody recorded why these conflict"* are the same empty report. **All-or-nothing: ONE unprovenanced edge disables the report for the whole submission** |
| `conflictEdges` present as **`null`** | ***REFUSED*** | §2.7. **Not the same as omitted** — a lenient deserializer restores the false *"ran and found nothing"* claim one layer down |
| a signal in neither `map.storage` nor `map.harnessOnly` | ***NOT CHECKED*** | §2.7, and ***measured at 16 of 17 signals*** in the deliverable submission. `providedFor` says HOW a signal is watched, never WHERE it is |
| a signal in **both** | ***REFUSED***, naming it | it cannot both occupy storage and occupy none |
| a `storage` entry's `owner` | **a POSITIVE claim, not an omission** | the path is **global** — DB member, PLC tag, `iDB_…`, physical address — and already unique |
| a declared join resolving to **more than one** storage | ***REFUSED, naming EVERY candidate*** | ***never resolved to one***; picking a candidate is the aliasing that manufactured fictional multi-writers |
| a conflict edge's **signal class** | ***NOT CHECKED*** (`Unstated`) | **derived from the writing blocks, never declared.** There is no field for it, and an unclassifiable signal fails closed |
| a mirrored signal's `specName` | ***REFUSED*** | §2.8. ***Never "same as `tag`"*** — that defaulting rule **is** the assumption being removed, and in the format it would look like a decision somebody made |
| a signal's `modes`, with `modeSource: Generated` | **CHECKED — DERIVED, a real pass** | read off the shape the copy layer generates. ***A declared mode is a caller assertion, forgotten exactly when it matters*** |
| a signal's `modeSource` | ***NOT CHECKED*** | zero value `Unstated`, failing closed: the mode's authority is the question, so a blank cannot be the trustworthy answer |
| `instrumentedBy`, with `modeSource: HandAuthored` | ***REFUSED*** | an unnamed hand-authored instrument is a bare assertion **wearing a provenance field** — worse than no field, because it looks checked |
| a declared mode the copy layer **contradicts** | ***REFUSED, naming BOTH*** | never a silent preference either way |
| `blockCompression`, at `runtimeCompression` > 1 | ***NOT CHECKED*** | three of X-D's four ceilings compared against nothing |
| `blockCompression`, at `runtimeCompression` = 1 | **CHECKED — a real pass** | nothing is scaled, so none of the three *can* bind. **Computed from the submission, not assumed** |
| a preset's `source` (`Data`/`Literal`) | ***REFUSED*** | the two answers push OPPOSITE ways — a data preset lowers the timer ceiling, a literal one lowers the ratio-distortion ceiling. No fail-safe guess exists |
| `negligibleFraction` | **NOT DECLARED, never invented** | the spec works an example and never says where "negligible" ends. Above comp 1 this blocks; it is not an exemption |
| `model.compStable`, above comp 1 | ***REFUSED*** | the plan is asking a model to run at a rate nobody declared |
| `deployment` (§4.5's layout record) | ***NOT CHECKED*** | a property of the **download**, not of a vector. Absent is **not** the same as `s7Objects: []`, which is a positive claim that no S7comm path reaches a data block |
| an `s7Objects` row's `layout`, or `layoutSetAfterImport` | ***REFUSED*** | absence means "no opinion" and **TIA resolves no opinion to `Optimized`**, which is invisible on the wire. A stale stamp means it reverted at the last import |
| a vector's `boundsUsed` (§2.5) | ***NOT CHECKED*** | ***this is the AMB-19 hole itself, not a formality*** — a vector that records no bound **cannot be found stale by anything**, so it survives a retune with every gate green. **It keeps this status even beside a provably stale sibling**: *"we compared and refused"* must not hide *"and these we could not compare at all"* |
| `enumeration.bounds` | ***NOT CHECKED*** | an absent table is not an agreeing one |
| a declared bound that **differs** from the table | **REFUSED, reported `STALE`** | ***never `FAIL`.*** The block may be correct and the vector predates a retune |
| a declared bound the table does **not contain** | **REFUSED, reported `Unknown`** | a disagreement about which bounds *exist*; its repair precedes any question about a value, so it outranks `Stale` |
| `enumeration.normalisedTexts` | ***NOT CHECKED*** | the stamper's output is taken on trust — **and the omission is exactly what a compromised stamper would emit** |
| `enumeration.requiredObservations` | ***NOT CHECKED*** | a relational assertion's second signal goes unobserved and the relation is untested while everything reports green |

**Contract §2.4 is the single authoritative version of this table** — it covers the document fields this
one omits, and no gate may invent a treatment that is not in it. The four treatments are different
facts: **REFUSED** (fix the vector) · ***NOT CHECKED*** (fix another artifact — never a pass) ·
**NOT DECLARED** (a bound nobody computed, ***which is not a ceiling of infinity***) ·
**reported-but-not-gating** (computed to be unable to bind, and printed anyway so an absent line never
reads as a check that passed).

> 🔴 ***AND THIS IS WHY `AssertionForm.Unstated` IS THE ZERO VALUE.*** It used to be `When = 0`, so an
> omitted field was silently handed **the permissive form** — the exact hole 3e exists to close,
> reappearing one layer up in the wire format. Making the default *unusable* means ***a dropped form
> fails the same comparison as a wrong one***, which is the only arrangement where "the field was
> absent" cannot quietly become "the field said the convenient thing". Verified both ways on the exe:
> a vector declaring **no** form and a vector declaring the **wrong** form are both `REFUSED` at 3e.
>
> **So if you tell an author to declare a form, tell them what happens when they do not.** It is not
> a lint warning; the submission does not proceed.

*The same shape is worth recognising anywhere a default is chosen: `default(T)` on an enum is whatever
sits at zero, so putting a permissive value there hands it to everyone who omitted the field.*

### What this means for a report you write today

***No submission can be certified admissible from a command line right now.*** Say exactly that. A
report that reads "admissible" today is decoration, and the contract's whole thesis is that a
mechanical floor must survive an agent choosing not to look.

The useful output is the **per-gate list naming the missing verifier** — it is the harness lane's build
list, and it keeps an author from writing a green they did not earn. When gate 5 or 8 gains a real
command, this table changes and the report stops being all-NOT-CHECKED for a real reason.

---

## Step 2 — the manual pass, gate by gate

Run this even where the gate is NOT CHECKED. A recorded judgement is worth having; it is simply not a
verification, and you must not label it as one.

### Gate 1 — schema
Every field of contract §2 present, in the wire spelling the runner reads: `id`, `slot`, `index`,
`author`, `clause`, `assertion`, `assertionForm`, `inputs`, `startBool`,
`expectations[{signal, nature, mode, windowScans, expected}]`, `settlingCondition` +
`settlingSignals` (**per vector, not per expectation**), `maxDurationScans`, `completionSignal`,
`completionValue`, `blacklist`, `compressionFactor`, `assertedBehaviours`, `kills`, `boundsUsed`.

**`MaxDuration` is not a formality** — X-B makes it the per-test timeout and DB-13 needs it for wave
length, so a vector without one can neither be packed nor bounded. Its wall-clock backstop is
**computed** per §12a derivation 4, outlier allowance included; a trimmed allowance is a finding.

***And four of those fields were consumed by the runner before §2 named them at all*** (contract §2.2,
§2.3, added 2026-08-13): `expected`, `completionSignal`, `completionValue` and `kills`. **All four are
REFUSALS when absent, and the completion pair no longer defaults to 1** — the near-universal default is
exactly what made the rare block that signals completion with a state number invisible, returning
`TIMED-OUT` on a healthy block. *A field the runner reads and the contract does not name is a default
nobody chose.*

⚠️ ***DO NOT CHECK `completionValue` AGAINST A RANGE — CHECK IT AGAINST ITS ELEMENT*** (contract §2.6).
An earlier version of this gate said `0..65535`, **and that was wrong twice**: the range was *unsigned*
where a single-register element is **signed** (ceiling 32 767, not 65 535), and it assumed **one**
register, which made a 32-bit completion signal *inexpressible rather than mis-expressed*. **The framing
was the mistake** — a completion signal is *just another mirrored element*, and a range written here
would be a second, private notion of width able to disagree with the mirror's. **One notion of width,
one place.** The same rule governs every `inputs` value.

### Gate 2 — authorship (D6)
Vector author ≠ block author. **A match is a refusal, not a warning** — this is the correlated check
the whole pipeline exists to prevent. If either side is unrecorded, that is *also* a refusal:
**unknown is not independent.**

### Gate 3 — `Basis`, both halves
A clause alone is *admissible and nearly worthless*. The clause says where the requirement came from;
**the assertion says what would be observed if the block were correct, and that is the decorrelating
half** — two readings of one clause produce two *visibly different* assertions instead of two greens.
The assertion must be an **ID cited from the spec-derived enumeration**. An author who *writes* an
assertion rather than *citing* one has re-created the correlated check with extra steps.

### Gate 3i — bounds currency, ***the hole made entirely out of correct decisions***
Contract **§2.5**. Two rulings, each right on its own: **no hashed assertion text contains a numeric
bound** (which is what makes a re-issue cost zero re-hashes), and **retuning the bounds table therefore
re-hashes nothing.** Together:

> *** A RETUNE CHANGES THE TRUTH CONDITIONS OF EVERY ASSERTION REFERRING TO THE TABLE WHILE MOVING ZERO
> IDs *** — 22 of 27 on the hopper enumeration. A vector written against `T#60S` goes on passing after
> the bound becomes `T#90S`: nothing dangles, nothing recomputes wrong, and no `Stale` fires, because
> staleness keys on assertion **IDs** and not on bound **values**.

- **`boundsUsed` is REQUIRED for admissibility.** `{ "persistence_threshold": "T#60S" }` on the vector,
  compared against `enumeration.bounds`. **Carry the bare value** — the enumeration's YAML wraps the
  number in provenance prose; a mis-transcription then shows up as a loud STALE naming both strings.
- 🔴 ***A MISMATCH IS `STALE` AND NEVER `FAIL`.*** A `FAIL` says the block disagreed with the
  specification; here the block may be perfectly correct and the vector predates a retune nobody told it
  about. The refusal ends ***"Do NOT edit the block on the strength of this finding"*** and a test
  asserts that sentence — **it is load-bearing text.** Same defect as the missing predicate, one field
  over: a fact nobody supplied, surfacing as a verdict about the block.
- **It outranks both neighbours in the result package.** Before **admissibility**, because `Refused`
  reads as *the author broke a rule* and this author broke none. Before **content**, because ***a
  retuned bound is `Stale` even when an assertion disagreed*** — a disagreement measured against the
  wrong number is not evidence.
- **A vector stating no bound is NOT CHECKED, and keeps that status beside a provably stale sibling.**
  Both refuse, so nothing is admitted either way; only the report differs — and *"we compared and
  refused"* must not hide *"and these we could not compare at all."*
- **`Unknown` (a bound the table does not contain) outranks `Stale`**: a broken reference has to be
  repaired before any question about its value can be asked.
- ***The comparison is ASYMMETRIC on purpose — say so, or somebody will "fix" it.*** The bound's **name**
  is case-insensitive because a wrong name yields a refusal, so laxity there **fails closed**. The
  bound's **value** is ordinal and case-preserving because laxity there would turn a real difference into
  a ***pass***. And it is deliberately **not a duration parser**: `T#60S` and `T#1M` are the same
  interval and it reports them as different — *teaching it to equate them is how a comparator starts
  passing.* The cost of the strictness is a human reading two values; the cost of the leniency is a
  silent green.

### Gate 4 — fidelity (M4)
Set-difference: every asserted behaviour must be in the model's `Represents`. A model that claims
nothing, or that claims and disclaims the same behaviour, is **unusable** — a declaration that says two
things says nothing.

### Gate 5 — observability, ***the one with teeth***
Read the floor from **§12a derivation 1**. Do not carry the number here or in your report: it has moved
twice, and a restated constant will one day refuse the wrong vectors with great confidence.

- **A one-scan event is unobservable at any polling rate** — a poll IS one round trip; there is no rate
  to turn up. **A same-scan coincidence is unobservable by sampling at all.**
- ***PREFER LATCHED.*** Not merely cheaper: it is the only mode immune to the tail. A small but real
  fraction of poll gaps are enormous, and a sampled assertion landing in one is **a silent wrong
  answer, not an error**. A latch cannot fall in a gap.
- **Scan counts are meaningless without the `comp` they were stated at.** A behaviour occupying 20
  scans at `comp = 1` occupies 2 at `comp = 10` — *crossing the floor with nobody editing the vector.*
  The vector declares its `comp`; the floor is re-checked at the `comp` actually used.
- **The declaration must exist before the download that generates the copy layer**, and is frozen for
  the wave set. This is the single most common way an author is surprised.

> 🔴 ***DECLARING `Latched` DOES NOT MAKE A SIGNAL LATCHED — AND THIS GATE HAS FAILED IN BOTH DIRECTIONS
> AT ONCE.*** Measured on one live run: **17 `Latched` expectations across 11 of 27 vectors.** **13
> refusals were CORRECT** — the copy layer emits a plain **coil** for those signals, and a coil is not a
> latch. ***4 were FALSE REFUSALS***: those signals genuinely **are** latched on the device by a
> deployed hand-authored block, and there was **no mode field to say so** while the code hard-coded
> every signal to `Sampled`. *A real, deployed latch was structurally undeclarable.*
>
> **So the mode is DERIVED from what the copy layer generates, wherever it can be** (contract §2.8) — a
> declared mode is a caller assertion and *those are forgotten exactly when they matter*. A hand-authored
> instrument is statable, but **only with its provenance**: `modeSource: HandAuthored` plus
> `instrumentedBy` naming the block that performs it. ***`HandAuthored` with no `instrumentedBy` is a
> refusal*** — a bare assertion wearing a provenance field looks checked, which is worse than no field.
> **A declared mode the copy layer contradicts is refused naming BOTH**, never silently resolved either
> way.

> 🔴 ***AND CHECK WHICH NAME YOU ARE READING.*** The harness assumed **the specification's signal name
> IS the block's tag name**. One run, three mechanical paths broken by that one assumption: gate 5's 17
> refusals, a static interface check reporting a signal missing, and the conflict graph resolving **1 of
> 17** signals — *the one being `HopperBlockedAlarm`, **the only signal whose two names coincide***.
> A mirrored signal carries **both**: `tag` (on the controller) and `specName` (in the specification).
> ***An absent `specName` is a REFUSAL and never "same as tag"*** — that defaulting rule is the
> assumption being removed. *The translation had only ever lived in a prose markdown table, which is why
> a predicted finding could be laundered in it: prose is what no gate reads.*

**If a vector is refused here, STOP AND ESCALATE — do not fix it by editing the block.** The obvious
repair (add a status output so the behaviour is visible) collides with D13/§2.1, and whether an author
may change a block's interface purely to make it testable is **open with the owner** (contract §9.1).
Adding one is not yours to decide.

### Gate 6 — settling
***A completion flag is NOT a settling signal.*** Phase 2's defective build **raised `Done` at 10 and
went on ramping to 15**, so a faster-than-floor poll reads mid-ramp and returns a **confidently wrong
verdict** — the harness observes a value, believes it, compares it, and is wrong. That is the worst
outcome available in this system. If the settling condition names only the block's done-signal, it is
refused. The block's own opinion of its progress is *a claim under test*, not evidence about the
observation.

### Gate 7 — start bool
Exactly one per slot. ***Bound by NAME, never by bit position*** — the bit order within the start-bool
register is `[I]`, not `[M]`, and the simulator and `BitAddressOf` agree *from the same premise*, so
their agreement is worth nothing. Raised in one transaction with every other slot's start bool (X-A,
the commit); raised on a **later scan** than the inert-establish (D33/D37), enforced against the
**observed** counter, never assumed. Its rising edge is T=0, and T=0 is recorded rather than inferred.

### Gate 8 — blacklist
***May only ever ADD exclusions, never remove them.*** Computed disjointness (D9) is the floor; an
agent must never be able to declare itself compatible with something the graph says it conflicts with.
Every entry carries a **reason** — the failure mode is defensive over-blacklisting, concurrency
collapsing toward serial, and nobody noticing *because it still works*. Note for the author: the
blacklist names **blocks**, but admission colours **slots**, so naming a block excludes every slot
testing it — usually what was meant, occasionally much wider.

### Gates 8 and 8c — ***first check whether they COULD have been fed at all***
Both consume `conflictEdges`, and a conflict graph is a statement about **storage**: two blocks conflict
because they write *the same location*. **A submission names signals; nothing joined them to storage.**

> *** MEASURED: 16 OF 17 SIGNALS IN THE DELIVERABLE SUBMISSION RESOLVE TO NO PLC STORAGE PATH. *** They
> are harness-side logical names. `map.providedFor` carries **observability modes** — it says HOW a
> signal can be watched and never WHERE it is — so **these two gates were not merely unsupplied, they
> were inexpressible.** Contract **§2.7** is the join: `map.storage` (signal → `{owner?, path}`) and
> `map.harnessOnly` (signals that occupy no PLC storage, as a positive claim).

- ***A BARE LEAF NAME IS NOT A STORAGE REFERENCE.*** Matching a signal to storage by the shape of its
  name — "find the path that ends with this" — re-introduces **the aliasing defect that manufactured
  fictional multi-writers** (`IO.Step` in two UDTs; a `Time` temp declared separately in three FBs), at
  the gate boundary instead of inside the analysis. **Two of the four cross-block multi-writer findings
  this project ever recorded were fiction produced that way.** Never resolve by name shape, and do not
  accept a submission that asks you to.
- **`owner` and `path` are separate keys, not one dotted string** — *an emitted string is not a schema*,
  and a consumer handed `A.B.C` cannot tell an owning block from a DB without parsing.
- ***An ambiguous resolution is refused NAMING EVERY CANDIDATE***, never resolved to one. Instance
  aliases of one storage are collapsed first, so a refusal means genuinely different storage.
- **A signal in neither map is NOT CHECKED; in both, REFUSED.** `harnessOnly` is a claim the author
  makes, and it is what turns *"this may be a mirror-only signal, or the name may be wrong"* — two
  entirely different repairs behind one silence — into a fact.
- ⚠️ ***AND THE JOIN NEEDS TO KNOW WHICH NAME IT IS KEYING ON.*** `map.storage` says where a **tag**
  lives; the specification cites a **spec name**. The one signal that resolved out of seventeen resolved
  because those two strings happen to be identical (§2.8). **A submission carrying no `specName` cannot
  be fixed by declaring storage** — check §2.8 first.

### Gate 8c — multi-writer provenance (X-G)
Two blocks that both write one coil are a conflict, so the packer puts them in different tensors and
**both tests pass** — ***the scheduler has silently repaired a defect that will ship.*** So the graph
must carry, per edge, **why** the two conflict, **on which signal**, and **whether that signal is part
of the deliverable**.

- **A finding is REPORTED, NOT REFUSED**, and that is a decision: the defect is in the *deliverable*,
  not in the submission, and refusing here would make a vector author answerable for a program defect
  they cannot fix. **Whether it should instead be a hard refusal is an open owner question** — flag it,
  do not settle it. *(It is in tension with the standing "a warning is not a gate" rule; the reading
  taken is that this warning is about a program the vector author cannot repair.)*
- ***The teeth are on the ABSENCE of provenance.*** An unprovenanced graph is **NOT CHECKED**: a bare
  block list and a fully-analysed clean graph produce the identical empty report, and only one of them
  means anything. `computedConflicts` (bare names) is *legal and unprovenanced* — mixing it with
  `conflictEdges` leaves the packing set complete and 8c NOT CHECKED, which is the honest answer.
- 🔴 **Which is why `computedConflicts` is now NEVER EMITTED, deliberately** — say so, or somebody
  "fixes" the omission. The provenance test is ***all-or-nothing***, so ***one unprovenanced edge turns
  8c to NOT CHECKED for the ENTIRE submission***: a helpful-looking extra edge — a call-graph coupling,
  which is about **no signal** and so can carry no signal class — would **silently disable the
  multi-writer report it was added beside.** Call-graph and shared-model coupling belong in the
  **author's blacklist**, which is add-only for exactly this reason.
- **The signal CLASS is derived from the writing blocks, never declared.** There is no field for it. An
  unclassifiable signal carries `Unstated` through to the gate — *the refusal is carried across, not
  resolved into a guess*.
- ***`conflictEdges` is OMITTED when the graph did not run — not `[]`, and NOT `null`.*** `[]` is the
  **earned** claim that the graph ran over a whole corpus and found nothing; **a `null` lets a lenient
  deserializer restore that false claim one layer down.** A partial corpus makes it concrete: an
  unparsed file may hold the second writer that makes a signal a conflict.

### Gates 10a and 10b — time compression (X-D)
***A scan count and the `comp` it was stated at are ONE FACT.*** The hazard runs in both directions and
the same arithmetic causes both: as `comp` **rises** an observation window crosses the observability
floor and **a missed assertion is reported as a PASS**; as `comp` **falls** the wall-clock backstop
under-sizes and a healthy test **`TIMED-OUT`s, and is believed**.

- **10a is the vectors' own ceiling and a submission can always answer it.** It catches the case gate 5
  structurally cannot: ***LATCHED is exempt from the observability floor, never from the scan-period
  term.*** An event compressed below one scan of real time is not long enough to be latched either, so
  a latched declaration that clears gate 5 at every factor can still be void at the one being run.
- **10b belongs to the BLOCK and the MODEL**, and until contract §2.3 there was no way to state it:
  timer presets, `model.compStable`, and the ratio-distortion threshold on unscaled literals. **At
  `runtimeCompression` = 1 it is a real computed pass; above 1 with the inputs absent it is NOT
  CHECKED.**
- ***USE `comp_min`, NOT `comp_max`.*** The ceiling is a limit, never a target — compression is a
  fidelity risk and the remainder is margin. There is deliberately no member of the plan that
  recommends the ceiling.
- **Read every figure from §12a derivation 5, never from memory.** The measured timer floor is higher
  than X-D assumed, which pulls a 500 ms preset's ceiling from 10x down to roughly 4.3x — *every
  marginal case moved toward REFUSE, so a remembered number is a permissive number here.*
- 🔴 **Known hole, do not paper over it:** 10b's model bound keys on `comp_min` in the built code and on
  `runtimeCompression` in the contract. **They differ**, and a wave at `runtimeCompression = 8` whose
  `comp_min` is 1 passes today with **no `comp_stable` declared at all**. If a submission runs above
  `comp_min`, treat the model ceiling as **NOT CHECKED by hand** whatever the tool printed.

### Gate 11 — memory layout, ***the property that decides whether there is anything to observe at all***
Gate 5 asks whether a window clears a floor. **This is prior to that and it is binary.**

> ***AN S7-1200 BLOCK DEFAULTS TO `Optimized`, AND AN OPTIMIZED BLOCK IS INVISIBLE TO CLASSIC S7comm —
> NOT AN ERROR. THE BLOCK IS SIMPLY ABSENT, AND IT FAILS AT THE FIRST *DATA* READ RATHER THAN AT
> CONNECT.***

**The invariant (contract §4.5, the coordinator's ruling, overturnable at one line):** *the harness
never touches a deliverable block's data directly — it touches harness-generated objects only.* That is
the isolation the copy layer already provides, so **the block under test stays `Optimized`, the platform
default, and a deliverable pays nothing.**

- **Two mechanisms, and conflating them is the mistake to avoid.** The `%MW` mirror — the wave's whole
  surface, **start bools included** — is **not a data block at all**, so it has no layout to revert;
  its guarantee is the **address**. A harness *data block* is guaranteed by **re-assertion**. Somebody
  hunting for `--expect Standard` on the mirror will not find one, and that is not a missing check.
- ***THE LAYOUT REVERTS AT EVERY IMPORT, SILENTLY*** (the exported `.xml` carries no `MemoryLayout`
  element, so the import states no opinion). So: `block-layout --set Standard --yes` after **each**
  import, then `--expect Standard` as the gate. ***`--expect` only tells you it broke; `--set` is what
  repairs it.***
- ***NEVER ACCEPT `drift-check` AS THE GATE FOR THIS.*** The `Normalizer` ignores the attribute, so it
  reports MATCH in **both** directions. A green from a blind check is worse than no check.
- 🔴 **True today, enforced by nothing.** Audited 2026-08-13: the wave path is Modbus-into-`%M`
  throughout and cannot break the invariant; the rig marker DB is a harness object declared `Standard`.
  **But `S7Transport` resolves tags through a HAND-WRITTEN JSON tag map and will address any DB**, and
  the write fence is scoped on an **area name from that same map** — so it checks that the caller's
  claimed area matches the tag's, never what *kind of object* it is. **If a submission's transport is
  that tag map, ask which DBs it names before believing the invariant holds for it.**

---

## Step 3 — mutation, not example

> ***A TEST THAT CANNOT FAIL HAS PROVEN NOTHING.***

Phase 2 found one of its own tests could not fail — a 3-slot map whose start bools fit in one register
made a per-register loop and a single transaction indistinguishable. **Only mutation found it.**

So for every vector, require and record:

1. **The wrong implementation it kills.** `Harness/TestVector.cs` already carries a `Kills` field for
   exactly this: *"a vector that no credible wrong implementation would fail only measures uptime."*
   Contract §2's format omits it — flag that (see the guess list) and require it anyway.
2. **Evidence the vector goes red against that mutant**, not merely green against the real block. A
   vector only ever run against a correct implementation is in the same state as a guard that has never
   been executed.
3. For any check *this skill* asks you to trust: the same question. If you cannot describe the input
   that would make it fail, you have not checked anything.

---

## Step 4 — how to read a result, ***including what it does not mean***

| verdict | means | does **not** mean |
|---|---|---|
| **PASS** | these assertions held | that the block is correct — only *these* assertions, under *this* model, at *this* fidelity |
| **FAIL** | an assertion was observed and disagreed | that the vector is right. ***Fix the block against the SPECIFICATION, not against the vector*** |
| **TIMED-OUT** | the condition never occurred within `MaxDuration` | the wrong thing happened. Kept distinct because an agent told only FAIL goes and fixes the wrong thing |
| **UNSETTLED** | the value never met its settling condition | that the value was wrong — **nothing was legitimately read at all** |
| ***STALE*** | ***the experiment never ran*** — **or the vector's premise expired** (§2.5) | anything whatsoever about the block |
| **REFUSED** | the vector was inadmissible | a defect in the block |

> ⚠️ ***A SPURIOUS `TIMED-OUT` IS WORSE THAN A SPURIOUS `FAIL`, BECAUSE IT IS BELIEVED.*** A `FAIL`
> invites an argument with the specification and someone goes and looks; a `TIMED-OUT` reads as *the
> condition simply never occurred* and **closes the question**. That asymmetry is why a value that
> cannot fit its mirror element is **refused** rather than compared (§2.6) — and why a 32-bit completion
> signal is refused as *inexpressible* rather than compared against its high half.

### ***`Stale` has TWO ROADS, and they send you to different halves of the system***

| road | it is a … | what to do |
|---|---|---|
| frozen mirror / no stimulus confirmed | ***RIG*** problem | the experiment never ran. Nothing here is evidence about anything |
| an out-of-date `boundsUsed` (AMB-19) | ***VECTOR*** problem | the experiment ran fine and **measured the wrong number**. Re-read the vector against the current bounds table and re-submit |

**In neither case do you edit the block** — and telling somebody *"the experiment never ran"* when it
was the premise that expired sends them to the wrong place entirely. **Read which road the result
names.**

### `Stale` is the one that will fool you

> ***"EVERY REGISTER AGREES" IS ALSO WHAT A MIRROR NO WRITE EVER REACHED LOOKS LIKE.***

A frozen mirror is *perfectly self-consistent*: it passes every coherence check and every
did-the-values-agree test, because nothing ever changed them. So **liveness must be established
independently of content** — the stimulus check, a counter that advanced **by the expected amount**
(not merely "moved"), and manifest presence, which separates *wrong* from *never loaded*.

***READ THE VERDICT, NOT THE EXIT CODE.*** And: **an absence of disagreement is not a result.**

A green never licenses more than it says. It does not generalise past the model's fidelity
declaration; it does not survive its validity stamp (program version + map version, DB-2); and it does
not mean the co-running slice was benign — only that nothing detected interference.

---

## Step 5 — the report

```
# Vector admissibility — <submission> (<date>)

## Verdict
NOT ADMISSIBLE / ADMISSIBLE-SUBJECT-TO-JUDGEMENT   <- never "admissible" while any gate is NOT CHECKED

## Gate results
<one line per gate: CHECKED (by <verifier, with the command run>) | JUDGEMENT (<who decided, on what>)
 | NOT CHECKED (<which verifier is missing>)>

## NOT CHECKED - what is missing
<the build list: gate, what the contract requires, what would have to exist to check it>

## Judgement calls, stated as such
<assertion faithfulness, settling sufficiency, blacklist breadth - each with the reasoning>

## Mutation
<the wrong implementation each vector kills, and whether it has been shown to go red against it>

## Escalations
<contract sections 9.1-9.6 and anything the contract left open that this submission depends on>
```

**Verify Step 0 was actually done** and say so in the report: which verifiers you confirmed present,
and by what search. A report that lists a verifier it did not look for is the failure this skill is
built to prevent, committed by the skill itself.

---

## What I had to guess — ***raise these, do not silently resolve them***

The contract is a **draft**, and these are the places it did not determine the answer. If a submission
turns on one, escalate rather than picking a reading.

1. ***Two incompatible vector models exist and the contract matches neither.***
   `src/harness/Harness/TestVector.cs` has `Basis` as a **plain string**, an `Observability` enum of
   `{PersistentState, Transient, Coincidence}`, and `Steps`/`WaitScans` — no `Slot`, `Index`, `Author`,
   `StartBool`, `MaxDuration`, `Blacklist` or `CompressionFactor`. Contract §2 specifies all of those
   and `Basis{Clause,Assertion}`. `Harness.Results/Admissibility.cs` uses the **contract's** `Basis`.
   *This skill enforces the contract and treats `TestVector` as the legacy shape* — **assumed, not
   stated anywhere.**
2. ***The observability vocabulary is two different axes.*** The contract's modes are
   `{Latched, Sampled, Stamped}` — *what instrumentation is applied*. The shipped enum is
   `{PersistentState, Transient, Coincidence}` — *what the signal is like*. Nothing maps them. The
   natural reading is Transient⇒Latched required, Coincidence⇒Stamped required, PersistentState⇒Sampled
   permitted, **but the contract never says so and no code uses the mode names at all.**
3. ✅ **RESOLVED 2026-08-13 — `kills` is now in contract §2 (§2.2), REQUIRED, and absent is a refusal.**
   So are `expected`, `completionSignal` and `completionValue`, all four of which the runner consumed
   while §2 named none of them. **Contract §2.3 adds `blockCompression` + `model.compStable`**, without
   which the compressed path was permanently NOT CHECKED. **§2.5 adds `boundsUsed`, required for
   admissibility.** *If a submission predates any of these it will be refused for a good reason — do not
   tell the author the tool is wrong.* ⚠️ **Expect this**: a vector set that names its bound in **prose
   only** carries no `boundsUsed` and is **NOT CHECKED at 3i**. The repair is to declare the field, not
   to argue that the number was written down somewhere.
4. **"Agent identity" is undefined.** D6 turns on vector author ≠ block author, and the code compares
   two strings with `StringComparison.Ordinal`. What makes two agents different — session, model,
   worktree? **Ordinal equality on an unspecified string is a gate that is passed by typing a different
   string.**
5. **The "map's observability declarations" (§4.3) — half resolved.** The submission's `map.providedFor`
   is now that list, and gate 5 checks against it. **What is still open is who fills it in:** it is
   supposed to be built by the coordinator *from the copy layer it generated*, and a submission in which
   the vector's own author typed it is a declaration checked against itself. Ask which it was.
6. **The spec-derived assertion enumeration — half resolved.** The submission's `enumeration` block
   populates it, and `docs/notes/assertion-enumeration.md` defines the decomposition. **The remaining
   hole is the same one:** nothing binds that block to an enumeration produced by a third party, beyond
   the `enumerator` identity string gate 3d compares. **The flat projection (clauses + assertions, no
   `forms`) is legal and costs two gates** — 3d and 3e go NOT CHECKED against it.
7. **"reject" vs "refuse" in §10's on-failure column** are used differently — *refuse* is emphasised for
   authorship and observability — but `RefusalReason` treats every gate identically. Treated here as one
   outcome (not admissible); if they are meant to differ, §10 should say how.
8. **Gate order vs reporting every failure.** §10 lists gates "in the order a submission meets it",
   implying short-circuit; `Admissibility.Check` deliberately reports **every** failure. This skill
   follows the code — one refusal at a time costs an author N round trips.
9. **§9.1 is open with the owner** and this skill depends on it: an author refused at gate 5 has no
   route back to testable. Until it is ruled, gate 5's refusal is an **escalation**, not a task.
10. **§9.4's stamped off-by-one is unresolved.** The copy layer runs *before* the block, so a stamp
    difference of 1 or 2 sits inside an unspecified off-by-one. **Do not admit an assertion that turns
    on one** until §9.4 says whether a stamp is the scan the copy layer observed it or the scan the
    block did it.
11. **§4.5's memory-layout invariant is the COORDINATOR'S ruling, not the owner's**, and it is
    overturnable at one line. It costs a deliverable nothing (the block under test stays `Optimized`),
    which is why it was taken rather than escalated. **Gate 11 now enforces it** against the tag map's
    reach — a declared object that SHIPS is refused as *the invariant broken, not a finding*. **What is
    still unenforced is the tag map itself**: `S7TagMap` will address any DB, and `RigWriteCli --db` /
    `Harness.RigRead --db` are free-form. If a submission reaches a deliverable block over classic
    S7comm, that is not a vector to fix: **stop and escalate.**
