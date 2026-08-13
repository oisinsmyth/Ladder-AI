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
| 4 | **fidelity (M4)** | asserted behaviours ⊆ the model's `Represents` | `Admissibility` — set difference | **CHECKED** |
| 5 | **observability** | mode valid; signal in the map; window ≥ §12a derivation 1's floor **at the run-time `comp`**; declared before the generating download | ***`ObservabilityCheck` — A COMPUTATION.*** It was a caller-supplied `bool`; it is now derived from the vector's declared nature+mode, what the MAP provides, and the floor `harness-gate` computes from the wave set | **CHECKED** |
| 6 | **settling exists, and is not the completion flag** | both halves | `Admissibility` | **CHECKED** |
| 6b | *does the condition really imply the value is final?* | — | **nothing, ever** | **JUDGEMENT** |
| 7 | **start bool** | exactly one per slot; bound by NAME; raised a later scan than the inert-establish, against the **observed** counter | `SubmissionGate` for the first two. **The later-scan rule is RUN-TIME** (`InertPhase`) and is reported as such, never claimed at submission | **CHECKED** (submission half) |
| 8 | **blacklist** | add-only against computed disjointness; every entry has a reason | `SubmissionGate`. ***Add-only is a property of the TYPE*** — `BlacklistEntry` carries no negation, so a removal cannot be expressed. **An ABSENT conflict graph is NOT CHECKED**, not a pass | **CHECKED** *(NOT CHECKED without `computedConflicts`)* |
| 8b | *is it over-broad?* | — | density, reported not gated | **JUDGEMENT** |
| **8c** | **multi-writer provenance (X-G)** | every conflict edge records WHY two blocks conflict, on which signal, and whether that signal SHIPS | `ConflictGraph`. ***The teeth are on the ABSENCE of provenance***: a multi-writer FINDING on a deliverable signal is **reported, not refused** (the defect is in the deliverable, and a vector author cannot fix it), while an **unprovenanced graph is NOT CHECKED** — *"0 findings"* and *"nobody recorded why"* are the same empty report | **CHECKED** *(NOT CHECKED without provenance on every edge)* |
| 9 | **liveness** *(post-run)* | stimulus check; counter advanced **by the expected amount**; manifest presence | `StimulusCheck`, at run time. **The separable half is now a submission gate**: a vector whose liveness could never be established is refused *before* a wave is spent | **CHECKED** (preconditions) **+ CHECKED at run time** |
| **10a** | **time compression — assertion ceiling (X-D)** | the run-time `comp` is under every vector's own `T_event / scan_period` ceiling | `TimeCompression`. ***It catches what gate 5 structurally cannot:*** gate 5 exempts LATCHED and STAMPED from the observability floor, correctly — **X-D does not exempt them from the SCAN-PERIOD term.** An event compressed below one scan is not long enough to be latched either, so a latched declaration that clears gate 5 at every factor can still be void at the one being run | **CHECKED** |
| **10b** | **time compression — timer / model / ratio ceilings (X-D)** | at `runtimeCompression` > 1: the block's timer presets (`PT / (k x scan)` — the term **X-D says often binds first**), the model's `comp_stable`, and the ratio-distortion bound on unscaled literals | `TimeCompression.Plan`, from contract **§2.3**'s `blockCompression` + `model.compStable`. **At `comp` = 1 this is a REAL computed pass** (nothing is scaled, so none of the three can bind). **Above 1 with the inputs absent it is NOT CHECKED and fails closed** | **CHECKED** *(NOT CHECKED without `blockCompression`, above comp 1)* |

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
| the conflict graph (both `computedConflicts` and `conflictEdges`) | ***NOT CHECKED*** | a blacklist compared against an absent graph is a blacklist nobody checked |
| provenance on any conflict edge | ***NOT CHECKED*** | *"0 multi-writer findings"* and *"nobody recorded why these conflict"* are the same empty report |
| `blockCompression`, at `runtimeCompression` > 1 | ***NOT CHECKED*** | three of X-D's four ceilings compared against nothing |
| `blockCompression`, at `runtimeCompression` = 1 | **CHECKED — a real pass** | nothing is scaled, so none of the three *can* bind. **Computed from the submission, not assumed** |
| a preset's `source` (`Data`/`Literal`) | ***REFUSED*** | the two answers push OPPOSITE ways — a data preset lowers the timer ceiling, a literal one lowers the ratio-distortion ceiling. No fail-safe guess exists |
| `negligibleFraction` | **NOT DECLARED, never invented** | the spec works an example and never says where "negligible" ends. Above comp 1 this blocks; it is not an exemption |
| `model.compStable`, above comp 1 | ***REFUSED*** | the plan is asking a model to run at a rate nobody declared |

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
`completionValue`, `blacklist`, `compressionFactor`, `assertedBehaviours`, `kills`.

**`MaxDuration` is not a formality** — X-B makes it the per-test timeout and DB-13 needs it for wave
length, so a vector without one can neither be packed nor bounded. Its wall-clock backstop is
**computed** per §12a derivation 4, outlier allowance included; a trimmed allowance is a finding.

***And four of those fields were consumed by the runner before §2 named them at all*** (contract §2.2,
§2.3, added 2026-08-13): `expected`, `completionSignal`, `completionValue` and `kills`. **All four are
REFUSALS when absent, and the completion pair no longer defaults to 1** — the near-universal default is
exactly what made the rare block that signals completion with a state number invisible, returning
`TIMED-OUT` on a healthy block. *A field the runner reads and the contract does not name is a default
nobody chose.*

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
  means anything. Note that `computedConflicts` (bare names) is *legal and unprovenanced* — mixing it
  with `conflictEdges` leaves the packing set complete and 8c NOT CHECKED, which is the honest answer.

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
| ***STALE*** | ***the experiment never ran*** | anything whatsoever about the block |
| **REFUSED** | the vector was inadmissible | a defect in the block |

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
   which the compressed path was permanently NOT CHECKED. *If a submission predates these, it will fail
   the schema gate for a good reason — do not tell the author the tool is wrong.*
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
