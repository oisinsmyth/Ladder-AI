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
| 3b | **basis — assertion** | ID drawn from the spec-derived enumeration, never free text | same. An empty enumeration is a **refusal** | **CHECKED** |
| 3c | *is the assertion a faithful reading of the clause?* | — | **nothing, ever** | **JUDGEMENT** |
| 4 | **fidelity (M4)** | asserted behaviours ⊆ the model's `Represents` | `Admissibility` — set difference | **CHECKED** |
| 5 | **observability** | mode valid; signal in the map; window ≥ §12a derivation 1's floor **at the run-time `comp`**; declared before the generating download | ***`ObservabilityCheck` — A COMPUTATION.*** It was a caller-supplied `bool`; it is now derived from the vector's declared nature+mode, what the MAP provides, and the floor `harness-gate` computes from the wave set | **CHECKED** |
| 6 | **settling exists, and is not the completion flag** | both halves | `Admissibility` | **CHECKED** |
| 6b | *does the condition really imply the value is final?* | — | **nothing, ever** | **JUDGEMENT** |
| 7 | **start bool** | exactly one per slot; bound by NAME; raised a later scan than the inert-establish, against the **observed** counter | `SubmissionGate` for the first two. **The later-scan rule is RUN-TIME** (`InertPhase`) and is reported as such, never claimed at submission | **CHECKED** (submission half) |
| 8 | **blacklist** | add-only against computed disjointness; every entry has a reason | `SubmissionGate`. ***Add-only is a property of the TYPE*** — `BlacklistEntry` carries no negation, so a removal cannot be expressed. **An ABSENT conflict graph is NOT CHECKED**, not a pass | **CHECKED** *(NOT CHECKED without `computedConflicts`)* |
| 8b | *is it over-broad?* | — | density, reported not gated | **JUDGEMENT** |
| 9 | **liveness** *(post-run)* | stimulus check; counter advanced **by the expected amount**; manifest presence | `StimulusCheck`, at run time. **The separable half is now a submission gate**: a vector whose liveness could never be established is refused *before* a wave is spent | **CHECKED** (preconditions) **+ CHECKED at run time** |

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
Every field of contract §2 present: `Id`, `Slot`, `Index`, `Author`, `Basis{Clause,Assertion}`,
`Inputs`, `StartBool`, `Expectations[{tag, predicate, Observability, Settling}]`, `MaxDuration`,
`Blacklist`, `CompressionFactor`. **`MaxDuration` is not a formality** — X-B makes it the per-test
timeout and DB-13 needs it for wave length, so a vector without one can neither be packed nor bounded.
Its wall-clock backstop is **computed** per §12a derivation 4, outlier allowance included; a trimmed
allowance is a finding.

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
3. **`Kills` is in the code and absent from §2's format.** §10 requires mutation testing and `Kills` is
   the only mechanism for it that exists. This skill requires it; whether the contract meant to drop it
   is unresolved.
4. **"Agent identity" is undefined.** D6 turns on vector author ≠ block author, and the code compares
   two strings with `StringComparison.Ordinal`. What makes two agents different — session, model,
   worktree? **Ordinal equality on an unspecified string is a gate that is passed by typing a different
   string.**
5. **The "map's observability declarations" (§4.3) has no home.** No artifact in the repo is that. An
   author cannot check their signal is in a list they cannot find.
6. **The spec-derived assertion enumeration has no source.** `AssertionEnumeration` is a type with a
   factory; **nothing populates it from a file.** An author cannot cite into an enumeration they cannot
   read, and §3's whole decorrelating argument rests on citing into it.
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
