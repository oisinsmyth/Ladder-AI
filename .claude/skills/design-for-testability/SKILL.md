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

`CLAUDE.md` is already in your context — do not re-read it. Hard rules apply: this skill **never edits a block** and never
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

> 🔴 ***AND THE RULE THAT MAKES A DIVERGENCE HARMLESS EITHER WAY: AN UNKNOWN FIELD IS A REFUSAL NAMING
> IT, NEVER SILENTLY IGNORED*** (contract §2.4).
>
> **A silently-ignored field is worse than a rejected one, because it reads as ACCEPTED.** An author who
> writes a field the contract specifies and the runner has not yet built gets a green, believes it was
> checked, and has submitted a document with a hole in exactly the place they were most careful.
>
> **This is not hypothetical.** Something has been stale in one direction or the other *every single time
> these two artifacts have been compared* — the gate table short by three rows, then by four; a field
> specified with three keys and built with one. **Expect it, look for it, and report which artifact is
> behind.** The refusal is what makes staleness announce itself instead of accumulating.

---

## Step 1 — the gates, and what actually checks each one

Contract §10's surface, in the order a submission meets it, with the **verifier reality on
2026-08-13**. `Admissibility.Check` is `src/harness/Harness.Results/Admissibility.cs`.

| # | gate | what the contract requires | verifier today | outcome |
|---|---|---|---|---|
| **0b** | **unknown fields** | every field in the document is one the schema reads | `SubmissionGate`. ***A field the schema does not know is a REFUSAL NAMING IT, never a silent drop*** — and if nobody supplied the unknown-field set at all, that is **NOT CHECKED**, because a dropped field reads as an accepted one | **CHECKED** *(NOT CHECKED if the reader supplied no extension data)* |
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
| **8s** | **signal storage join** — contract **§2.7** | every signal a vector expects on is joined: either in `map.storage` (controller storage) or declared `map.harnessOnly` | `SignalStorageMap`. **Two statuses, deliberately: an ambiguity or a contradiction was COMPARED and found wrong (refusal); an absent join was never compared (NOT CHECKED)** — different repairs. ***Nothing matches a signal by the shape of its name***, and the pass names how many resolved each way | **CHECKED** *(NOT CHECKED without the join)* |
| 8 | **blacklist** | add-only against computed disjointness; every entry has a reason | `SubmissionGate`. ***Add-only is a property of the TYPE*** — `BlacklistEntry` carries no negation, so a removal cannot be expressed. **An ABSENT conflict graph is NOT CHECKED**, not a pass; ***a `conflictEdges` that is present and NULL is REFUSED BY NAME*** rather than normalised | **CHECKED** *(NOT CHECKED without `conflictEdges`, which needs `map.storage` — gate 8s)* |
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
The two are different facts and the gate distinguishes them.

**The table is contract §2.4, and it is the single authoritative version.** It carries every row this
section used to restate and about a dozen more, and **no gate may invent a treatment that is not in
it.** Read it per run rather than from memory: it is ahead of anything remembered — the
`boundsUsed: {}` ruling of 2026-08-17 (§2.5) landed after this section was written, and a remembered
row would now be wrong. §2.4's complement matters as much as the table: ***an UNKNOWN field is a
refusal naming it, never silently ignored*** — an author is most careful exactly where the hole is.

The four treatments are different facts: **REFUSED** (fix the vector) · ***NOT CHECKED*** (fix
another artifact — never a pass) · **NOT DECLARED** (a bound nobody computed, ***which is not a
ceiling of infinity***) · **reported-but-not-gating** (computed to be unable to bind, printed anyway
so an absent line never reads as a check that passed).

Two of §2.4's rows carry an instruction to *you* rather than a rule about the document, so they are
repeated here: **a `specName` that genuinely equals its `tag` must SAY so** rather than be left
absent — as a default it matches by accident where the names coincide and misses everywhere else;
and **the gate takes the NAME, not the FACT, for `latchedBy` — verify yourself that the named block
is deployed and latches this signal.**

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
**Contract §3**, which carries the full rule plus the autopsy it came from, the §7-denominator trap,
the enumeration-document requirements and the checkable/not-checkable split.

The half that does the work: a clause alone is *admissible and nearly worthless*. **The assertion
says what would be observed if the block were correct, and that is the decorrelating half** — and it
must be an **ID cited from the enumeration**, never written. An author who *writes* an assertion
rather than *citing* one has re-created the correlated check with extra steps.

### Gate 3i — bounds currency, ***the hole made entirely out of correct decisions***
**Contract §2.5 and its five subsections** — the mismatch-is-`STALE`-never-`FAIL` ruling, why it
outranks both its neighbours, why the comparison is asymmetric on purpose, and the four states of
which only one is a pass.

The shape, so you recognise it: **no hashed assertion text contains a numeric bound**, and retuning
the bounds table therefore **re-hashes nothing** — so a retune changes the truth conditions of every
assertion referring to the table while moving zero IDs. A vector written against the old bound goes
on passing, because staleness keys on assertion **IDs** and not on bound **values**.

🔴 **Read §2.5 rather than remembering this section**: the `boundsUsed: {}` ruling of 2026-08-17 — an
empty bounds object is a **claim** that is verified, not a silence that is accepted — postdates
everything this skill used to say here.

### Gate 4 — fidelity (M4)
Set-difference: every asserted behaviour must be in the model's `Represents`. **A model that claims
nothing, or that claims and disclaims the same behaviour, is unusable — a declaration that says two
things says nothing.**

⚠️ **M4 has no section of its own in the contract, and `§4` is NOT it — §4 is observability.** The
set-difference rule lives in §1's element table and §10's row; the drift half is **§2.9**.

> 🔴 ***THE DECLARATION DESCRIBES THE MODEL AS ITS IR STATES IT.*** If the deployed object has
> **drifted**, the declaration describes a *different object from the one that will run*, and every
> M4 pass is a set-difference against the wrong set. **Not admissible; the verdict is `STALE`, never
> `FAIL` and never `REFUSED`** (contract §2.9).

### Gate 5 — observability, ***the one with teeth***
**Read the floor from §12a derivation 1** — which is in `docs/notes/PC-Client-Modbus-Spec-Draft-final.txt`,
not in the contract. **Do not carry the number here or in your report**: it has moved twice, and a
restated constant will one day refuse the wrong vectors with great confidence.

The rules are **contract §4.1–§4.4**: the physical floor (a one-scan event is unobservable at any
polling rate; a same-scan coincidence is unobservable by sampling at all), the three modes and why
you ***PREFER LATCHED*** (it is the only mode immune to the tail — a sampled assertion landing in a
long poll gap is a silent wrong answer, not an error), what must be declared per expectation, and why
**scan counts are meaningless without the `comp` they were stated at**.

**The mode is DERIVED, and that is contract §2.8** — including why `latchedBy` names a **block**
rather than asserting a mode, why declaring `Latched` does not make a signal latched, and why a block
that latches **its own output** is a value under test rather than instrumentation. Two instructions
out of that section are yours to act on rather than merely know:

- 🔴 **The gate takes the NAME, not the FACT.** It does not verify the named block is deployed or
  that it latches this signal. **Verify that yourself.**
- ⚠️ If the sealing or holding **is the behaviour being asserted**, declare those expectations
  **`Sampled`** and leave `latchedBy` absent.

**If a vector is refused here, STOP AND ESCALATE — do not fix it by editing the block.** The obvious
repair (add a status output so the behaviour is visible) collides with D13/§2.1, and whether an author
may change a block's interface purely to make it testable is **open with the owner** (contract §9.1).
Adding one is not yours to decide.

### Gate 6 — settling
**Contract §5**, plus **§5.1**'s ruling (keep the caller-supplied model, with `NotEstablished` as the
third value) and its open gap, neither of which this skill used to carry.

***A completion flag is NOT a settling signal.*** A defective build raised `Done` early and went on
ramping, so a faster-than-floor poll reads mid-ramp and returns a **confidently wrong verdict** — the
worst outcome available in this system. If the settling condition names only the block's done-signal,
it is refused: **the block's own opinion of its progress is a claim under test**, not evidence about
the observation.

### Gate 7 — start bool
**Contract §6** (exactly one per slot, bound by **name** and never by bit position, raised in one
transaction on a later scan than the inert-establish, its rising edge recorded as T=0 rather than
inferred) and **§6.1** (`startCondition: null` is admissible, and it is a claim).

What to act on from §6.1's cost list: a null-start slot gets no per-slot T=0 and no per-slot evidence
the code ran, so **do not admit a `Stamped` assertion or any timing claim on one**, and a `NEVER`
passing on such a slot cannot be told from "the block never ran". A pass there says *these assertions
held somewhere in this wave*, never *after this slot started*.

### Gate 8 — blacklist
**Contract §7.** Add-only over computed disjointness (D9); every entry carries a reason; the failure
mode is defensive over-blacklisting and nobody noticing *because it still works*. Note for the
author: the blacklist names **blocks** while admission colours **slots**, so naming a block excludes
every slot testing it.

### Gates 8 and 8c — ***first check whether they COULD have been fed at all***
Both consume `conflictEdges`, and a conflict graph is a statement about **storage**: two blocks
conflict because they write *the same location*. **A submission names signals; nothing joined them to
storage** — measured at **16 of 17 signals resolving to no PLC storage path**, so these two gates were
not merely unsupplied, they were *inexpressible*.

**Contract §2.7** is the join and carries the rules: `map.storage` and `map.harnessOnly`, `owner` and
`path` as separate keys, the three states, and why an ambiguous resolution is refused naming every
candidate. Two things there are instructions rather than rules:

- ***A BARE LEAF NAME IS NOT A STORAGE REFERENCE.*** Matching by name shape re-introduces the aliasing
  defect that manufactured fictional multi-writers — two of the four cross-block multi-writer findings
  this project ever recorded were fiction produced that way. **Never resolve by name shape, and do not
  accept a submission that asks you to.**
- ⚠️ The join needs to know **which name it is keying on**: `map.storage` says where a **tag** lives,
  the specification cites a **spec name**. **A submission carrying no `specName` cannot be fixed by
  declaring storage — check §2.8 first.**

### Gate 8c — multi-writer provenance (X-G)
Two blocks that both write one coil are a conflict, so the packer puts them in different tensors and
**both tests pass** — ***the scheduler has silently repaired a defect that will ship.*** So the graph
must carry, per edge, **why** the two conflict, **on which signal**, and **whether that signal is part
of the deliverable**.

**Contract §2.7**'s refusal-semantics subsections carry the rest, with §10's row: `[]` is the
**earned** claim that the graph ran and found nothing while a `null` lets a lenient deserializer
restore that claim falsely; the signal class is **derived from the writing blocks, never declared**;
and the provenance test is ***all-or-nothing***, so one unprovenanced edge turns 8c to NOT CHECKED for
the entire submission. 🔴 `computedConflicts` is therefore **never emitted** — *say so, or somebody
"fixes" the omission.*

- **A finding is REPORTED, NOT REFUSED**, and that is a decision: the defect is in the *deliverable*,
  not in the submission, and refusing here would make a vector author answerable for a program defect
  they cannot fix. **Whether it should instead be a hard refusal is an open owner question** — flag
  it, do not settle it. *(It is in tension with the standing "a warning is not a gate" rule; the
  reading taken is that this warning is about a program the vector author cannot repair.)*

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

**Contract §4.5** carries the invariant and its consequences: the harness touches harness-generated
objects only, so the block under test stays `Optimized` and a deliverable pays nothing; the `%MW`
mirror is not a data block and has no layout to revert, while a harness *data block* is guaranteed by
**re-assertion**; the layout **reverts at every import, silently**, so `--set Standard` after each
import and `--expect Standard` as the gate — ***`--expect` only tells you it broke; `--set` repairs
it***; and ***never accept `drift-check` as the gate for this***, because the `Normalizer` ignores the
attribute and reports MATCH in both directions.

🔴 **True today, enforced by nothing** (§4.5's audit). The one thing to *do* rather than know:
**`S7Transport` resolves tags through a hand-written JSON tag map and will address any DB**, and the
write fence is scoped on an area name from that same map — so it checks that the caller's claimed area
matches the tag's, never what *kind of object* it is. **If a submission's transport is that tag map,
ask which DBs it names before believing the invariant holds for it.**

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

**The verdict table is contract §8.1**, with its two "does not mean" columns — plus **§8.1a**, which
this skill never carried: ***the harness's record of its own actions is an input to the verdict.***

The one row that is an instruction: on a **FAIL**, ***fix the block against the SPECIFICATION, not
against the vector.*** And the three verdicts that are not failures — `TIMED-OUT`, `UNSETTLED`,
`STALE` — are kept distinct precisely because an agent told only `FAIL` goes and fixes the wrong
thing.

> ⚠️ ***A SPURIOUS `TIMED-OUT` IS WORSE THAN A SPURIOUS `FAIL`, BECAUSE IT IS BELIEVED.*** A `FAIL`
> invites an argument with the specification and someone goes and looks; a `TIMED-OUT` reads as *the
> condition simply never occurred* and **closes the question**. That asymmetry is why a value that
> cannot fit its mirror element is **refused** rather than compared (§2.6) — and why a 32-bit
> completion signal is refused as *inexpressible* rather than compared against its high half.

### ***`Stale` has THREE ROADS, and they send you to different halves of the system***

**Contract §8.1** — the paragraph beginning ***"`STALE` HAS THREE ROADS"***, not §2.5's "Two roads"
heading, which predates the drift road and covers only two of them. A rig problem (the experiment
never ran), a **vector** problem (an out-of-date `boundsUsed` — it ran fine and measured the wrong
number), and a **project** problem (a drifted model or block, §2.9 — it ran fine against an object
that is not the one the fidelity declaration describes).

**In none of the three do you edit the block** — and telling somebody *"the experiment never ran"*
when it was the premise that expired, or the project that moved, sends them to the wrong place
entirely. **Read which road the result names.**

> 🔴 ***AND CHECK THE PROJECT BEFORE THE WAVE, NOT AFTER.*** `drift-check --complete` against a fresh
> controller dump — **and read its SCOPE line**, because a green summary without it only means
> *everything paired matched*. Measured on the first whole-project run: **4 DRIFTED, one of them the
> stimulus model the live vector set depends on**, and **3 EXPORT-ONLY** — objects in the controller
> with no `.ir` at all, which make the corpus partial and are ***named, never counted***.

### `Stale` is the one that will fool you

**Contract §8.2** (why it is hard to make unmistakable, including the addendum on proving freshness
by a counter at every derived layer) and **§8.3** (what a green never licenses).

> ***"EVERY REGISTER AGREES" IS ALSO WHAT A MIRROR NO WRITE EVER REACHED LOOKS LIKE.***

A frozen mirror is *perfectly self-consistent*: it passes every coherence check, because nothing ever
changed the values. So **liveness must be established independently of content**.

***READ THE VERDICT, NOT THE EXIT CODE.*** And: **an absence of disagreement is not a result.**

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
4. **"Agent identity" — ✅ DEFINED, and the D6 comparison is NORMALISED**
   (`AgentIdentity.SameAs`: `Trim()` + `OrdinalIgnoreCase`). *A different agent means a different
   context instance: isolation is the mechanism, and the identity string is a label for it, not a
   proof of it.* 🔴 **A normalised string is still a string somebody types** — the gate establishes
   non-collision of *names*, not independence of *parties*. Graded ceiling L0–L4 and the
   claims-registry asymmetry: **`docs/notes/test-environment-contract.md` §1.1.**
5. **The "map's observability declarations" (§4.3) — half resolved.** The submission's `map.providedFor`
   is now that list, and gate 5 checks against it. **What is still open is who fills it in:** it is
   supposed to be built by the coordinator *from the copy layer it generated*, and a submission in which
   the vector's own author typed it is a declaration checked against itself. Ask which it was.
6. **The spec-derived assertion enumeration — half resolved.** The submission's `enumeration` block
   populates it, and `docs/notes/assertion-enumeration.md` defines the decomposition. **The remaining
   hole is the same one:** nothing binds that block to an enumeration produced by a third party, beyond
   the `enumerator` identity string gate 3d compares. *(Bounded, not closed — contract §1.1.)*
   **The flat projection (clauses + assertions, no
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
