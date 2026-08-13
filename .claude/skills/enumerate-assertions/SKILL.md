---
name: enumerate-assertions
description: 'Produce or check the spec-derived assertion enumeration — the coverage denominator — by decomposing requirement clauses into independently falsifiable assertions in the canonical WHEN/THEN or NEVER form, hashing stable IDs, and computing the UNCLASSIFIED residual. Use when asked to enumerate assertions, decompose a requirement or clause, build or audit the coverage denominator, work out what a clause actually asserts, resolve a decomposition dispute raised by a vector author, or re-decompose after a requirement changed. Also use before quoting any coverage figure — a percentage without all four bucket counts and the oldest deferral age is not an available output. Runs inside the assertion-enumerator agent, which is a THIRD party to the block author and the vector author. Ladder-AI project.'
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
---

# /enumerate-assertions — building the denominator

Ladder-AI project. The definition is `docs/notes/assertion-enumeration.md`; §7 of
`docs/notes/PC-Client-Modbus-Spec-Draft-final.txt` settles where the unit sits; the contract that
cites your output is `docs/notes/test-environment-contract.md` §3. **Read the definition this run.**
It is a DRAFT and it has open items that change what you should do.

> ***THE DENOMINATOR COMES FROM THE SPECIFICATION, NEVER FROM THE TEST SUITE.*** Any unit defined by
> what somebody wrote a vector for is self-referential: you cannot be missing an assertion nobody
> wrote, so coverage is always 100% and the gate is theatre.

**You run as a third party** (the `assertion-enumerator` agent) — not the block's author, not the
vector's author. If you are neither, say so in the report and name who you are. If you *are* one of
them, stop: the enumeration you would produce cannot be evidence about the coverage of your own work.

---

## Step 0 — verify the verifier, and verify the precondition

**Two things before any decomposition.**

**0a. Does the checker exist, and what does it read?** Do not trust this file's account of the code —
it goes stale fast, and it already has: `src/harness/Harness.Gate/GateCli.cs`
(`harness-gate check <submission.json>`) appeared *after* the 5.1 skill was written and reported no
runnable gate at all. So `Grep` for it, read `SubmissionDocument`, and record what you find. Today's
reading, to be re-checked and not believed:

| what | where | state |
|---|---|---|
| the gate | `Harness.Gate/GateCli.cs`, `Harness.Results/SubmissionGate.cs` | **runnable** |
| what it reads as the enumeration | `EnumerationDocument { Clauses: string[], Assertions: string[] }` | **two flat string lists — no form, no signal** |
| assertion form | `AssertionEnumeration.Of(..., forms, ...)` exists; `VectorDocument.AssertionForm` still declared by the vector | **library ready, WIRE FORMAT NOT** — `EnumerationDocument` has no `forms`, so the cross-check cannot fire from the CLI |
| enumerator identity | `AssertionEnumeration.Enumerator` + `SubmissionGate.EnumeratorIndependence` | **gate EXISTS** *(added 2026-08-13, after this skill first said it did not)* — but `EnumerationDocument` has no `enumerator` field and `GateCli` calls `Of(...)` with two arguments, **so from the CLI it always reports `NotChecked`** |

**0b. Are the clause IDs stable?** `REQ-014` is an identifier; *"§3.2, fourth paragraph"* is a
position. If the register addresses clauses positionally, **stop and report "not enumerable"** — the
assertion IDs would inherit exactly the instability the ID scheme exists to remove. *(Checked
2026-08-13: `gen/<project>/requirements.md` uses `### REQ-nnn — <title>`, which satisfies this.)*

---

## Step 1 — decompose

> ***AN ASSERTION IS THE SMALLEST STATEMENT ABOUT A BLOCK'S OBSERVABLE BEHAVIOUR THAT CAN BE FALSIFIED
> ON ITS OWN.*** Formally: `A` is an assertion of clause `C` if there is a possible implementation
> defect that makes `A` false **while leaving every other assertion of `C` true**.

Note what that test is about: **defects, not grammar.** It cuts both ways — *"opens when X and closes
when Y"* is too coarse (a wrong contact on the open rung falsifies half of it), and *"the coil
energises"* plus *"the coil stays energised"* is too fine (no defect separates them).

### The canonical form is the enforcement mechanism

```
WHEN <trigger> THEN <observable response> [ WITHIN <bound> | FOR <duration> ]
NEVER <forbidden observable state>
```

***ANYTHING THAT WILL NOT FIT ONE OF THESE TWO SHAPES IS AN UNDECOMPOSED CLAUSE, NOT A SPECIAL
CASE.*** That is what converts most of the judgement into a check: one `WHEN`, one `THEN`. If you find
yourself wanting "and also", or a second trigger, or a parenthetical exception — that is R1 or R2
telling you to split. Reject the text and decompose again rather than widening the template.

`NEVER` is for interlocks and prohibitions with no natural trigger (*"shall not run with the guard
open"*), independently falsifiable in the same sense. **Not for safety-instrumented functions** —
those are not enumerated here at all (hard rule 2).

### The five rules

| | rule | the trap it closes |
|---|---|---|
| **R1** | **Split on trigger.** | The **negative case** is its own assertion — *"and shall not open otherwise"* is the one most often lost. |
| **R2** | **Split on response.** | Two responses to one trigger are two assertions: a defect can produce one and not the other. |
| **R3** | ***Do not split on qualifiers.*** | Timing bounds, tolerances, units, hysteresis, persistence are **attributes of a response**. *"Opens within 2 s when X"* is **one** assertion; a timing defect is a failure **of it**, reported observed-versus-expected. |
| **R4** | **Split on instance** — the unit is `(assertion, instance)`. | The enumeration itself is per **class**; instances multiply it at citation time. |
| **R5** | ***Never split on implementation.*** | No rung, operator, address, block or internal tag. If a proposed split can only be *described* in terms of **how** the thing is built, it is a peek at the implementation and it re-correlates the check. |

**Where the rules leave a choice, the tie-breaker is a procedure, not a taste:** ***name the defect
that breaks this half and not the other.*** If you cannot name one, it is one assertion. Write the
defect down — it is the evidence for the split, and the next enumerator can check your work against
it instead of re-arguing from scratch.

### R5 is also your contamination detector

You worked from the specification. **The tell that someone did not is implementation vocabulary in the
assertion text.** Before emitting, scan your own assertions for rung numbers, operators, addresses,
block names and internal tags, and for anything you could only have known by reading the code. A hit
is not a style nit — it means the denominator was set by someone who had seen the answer.

---

## Step 2 — IDs

```
REQ-014:3f9a1c                 an assertion of clause REQ-014
REQ-014:3f9a1c@Feeder_02       the coverage unit, qualified by instance (R4)
```

`3f9a1c` = first 6 lowercase hex of `SHA-256(normalised assertion text)`, scoped to the clause.
**Normalisation, exactly:** trim; collapse internal whitespace runs to a single space; strip one
trailing `.` or `;`; **preserve case and everything else**; hash the UTF-8 bytes. *(Case is preserved
deliberately — folding it risks merging two distinct signal names.)*

> ***THE ID DEPENDS ONLY ON (CLAUSE IDENTITY, ASSERTION CONTENT). NOTHING POSITIONAL.***

- Inserting a clause above shifts nothing; inserting an assertion into a clause shifts no sibling.
- ***EDITING AN ASSERTION'S TEXT CHANGES ITS ID, DELIBERATELY.*** The old ID dangles, prior citations
  are flagged **stale**, and you record a `supersedes:` link. **Do not "helpfully" preserve an ID
  across a rewording** — a citation that silently survives a rewording of what it cites was written
  against words nobody re-read, and that is the failure the scheme exists to prevent.
- Two assertions in one clause that normalise identically are a **duplicate — an error**, not a
  collision to disambiguate.
- **The display ordinal (`REQ-014.A2`) is display only.** A `Basis` citation in ordinal form is
  rejected by shape, precisely because it is the readable one and would otherwise be the one people
  type.

---

## Step 3 — emit the artifact

Write the **full enumeration** as the source of truth, and derive the flat projection the gate reads
today. Both, because the gate's `EnumerationDocument` is two string lists and would otherwise silently
discard the form and the signal — the two fields that close real gaps (see Step 5).

```yaml
# enumeration source of truth
enumerator: <agent identity>          # recorded even though nothing compares it yet - see Step 5
register: gen/<project>/requirements.md
clauses:
  REQ-014:
    text: "<the clause text, verbatim>"
    assertions:
      - id: REQ-014:3f9a1c
        ordinal: A1                   # DISPLAY ONLY, never cited
        form: When                    # When | Never
        text: "WHEN <trigger> THEN <response> WITHIN <bound>"
        response_signal: <tag>        # what a citing vector must expect on
        split_defect: "<the defect that breaks this and not its siblings>"
        supersedes: <old id or absent>
```

```json
// the projection the gate consumes today (SubmissionDocument.Enumeration)
{ "clauses": ["REQ-014"], "assertions": ["REQ-014:3f9a1c"] }
```

**Never write `UNCLASSIFIED`.** It is a set difference and that is what makes the zero enforceable:

```
UNCLASSIFIED = enumerated − (COVERED ∪ OUT-OF-SCOPE ∪ DEFERRED ∪ UNTESTABLE-ON-RIG)
```

Forgetting an assertion does not produce a missing tick — **it produces a non-zero residual and a
failed gate.** Same shape as phase 2's `AddressesExamined` becoming a separate denominator.

***AND EMPTY IS NOT CLEAN.*** An enumeration parsing zero assertions is *nothing examined* and fails.
A clause with no assertions is an error in the decomposition, not a clause with nothing to say.

**You do not assign OUT-OF-SCOPE or UNTESTABLE-ON-RIG.** Those are the gate-1 signer's, never the
block author's and never the vector author's — they are the two an interested party reaches for under
pressure. DEFERRED carries an **owner and a date**; count *and age* are reported every wave, because
ageing deferrals are the signal, not the classification.

---

## Step 4 — the dispute channel, and why raising must be the easy path

> ***A CITATION THE VECTOR AUTHOR CANNOT MAKE IS A DECOMPOSITION DISPUTE. IT IS RAISED AS AN EVENT AND
> NEVER RESOLVED BY WRITING PROSE INTO `Basis`.***

The design **does not depend on two enumerators agreeing** — it depends on disagreement being visible.
So make raising it cheap and writing prose expensive:

- A vector author who needs an assertion that is not there files the dispute with **the clause ID, the
  reading they need, and the defect that would falsify it** — that is the whole cost, and it is the
  same three things you would have written anyway.
- **You do not add the assertion because a vector wanted it.** Re-decompose the clause from the
  specification. If the re-decomposition genuinely yields it, that is a **new UNCLASSIFIED assertion
  and its own reported event** — the gate fails until somebody rules on it. It is explicitly *not*
  absorbed as a classification change.
- **Report the assertions-per-clause ratio.** An enumeration far off the corpus norm is visible
  without anyone auditing it — in **both** directions, since splitting the easy clauses finely and
  leaving the hard one whole is the same attack wearing a different hat. *(No corpus norm exists yet.
  Report the ratio; do not quote a threshold that was never measured.)*

**Where a dispute is recorded and who rules on it is open** (definition §7 item 2). Until it is ruled,
raise it to whoever signed off the architecture at gate 1, by analogy with §4.3, and say that is what
you did.

---

## Step 5 — what actually checks what

Three outcomes, never collapsed to two: **CHECKED** (a verifier ran) · **JUDGEMENT** (labelled as
such) · ***NOT CHECKED*** (no verifier — fails closed, not a pass).

| check | verifier | state |
|---|---|---|
| assertion matches a canonical form | shape | **CHECKED** by you; `AssertionForm` exists in the gate |
| ID recomputes from the normalised text | rehash and compare — **a hand-edited ID is caught** | **CHECKED** by you |
| IDs unique per clause; identical normalisations are a duplicate | set | **CHECKED** by you |
| citation names an ID in the enumeration | `SubmissionGate` | **CHECKED** — runnable |
| citation is not in display-ordinal form | shape | **CHECKED** by you |
| cited assertion's response signal appears in the vector's `Expectations` | set-difference | ***NOT CHECKED*** — the gate's enumeration is flat strings with no signal |
| **declared `AssertionForm` matches the enumerated form** | `AssertionEnumeration` accepts `forms` | ***NOT CHECKED FROM THE CLI*** — `EnumerationDocument` carries no `forms`, so the vector's own declaration still stands unopposed and citing a `NEVER` while declaring `When` takes the permissive path |
| every assertion in exactly one bucket; `UNCLASSIFIED` = 0 | set difference | ***NOT CHECKED*** — no classification artifact exists |
| enumeration non-empty | *empty is not clean* | **CHECKED** — the gate refuses an empty enumeration |
| bucket assigner ≠ block author ≠ vector author | recorded identity | ***NOT CHECKED*** |
| **enumerator ≠ block author ≠ vector author** | `SubmissionGate.EnumeratorIndependence` — **and it refuses an unrecorded identity rather than passing it** | **gate CHECKED, but ***NOT REACHABLE FROM THE CLI***: no `enumerator` field on the wire, so it always reports `NotChecked`. **Record `enumerator:` anyway** — the day the field lands, every artifact that carried it is already gated |
| R5: no implementation vocabulary in assertion text | keyword scan | **CHECKED** by you, against what you can see |
| decomposition count differs from last time → event | compare | ***NOT CHECKED*** — no stored prior enumeration |

**Judgement, stated plainly rather than dressed up:** whether the decomposition is faithful and at the
right grain (the irreducible one); whether a bucket assignment is honest; whether a vector genuinely
*exercises* what it cites (only the signal-mention half could ever be mechanical); whether an
assertion is a correct reading of the clause at all.

> ***A COVERAGE FIGURE RESTING ON AN UNENUMERABLE DENOMINATOR MUST SAY SO RATHER THAN PRINT A
> PERCENTAGE***, and ***a percentage is not an available output without all four bucket counts and the
> oldest deferral's age.*** The gate proves someone **looked**, never that they looked **well**.

---

## Step 6 — the report

```
# Assertion enumeration — <register> (<date>)

## Independence
Enumerator: <identity>. Block author: <identity or UNKNOWN>. Vector author: <identity or UNKNOWN>.
Files read: <every path>.            <- the R5 contamination declaration
Distinct from both? <yes / no / CANNOT ESTABLISH - and that is not a pass>

## Enumeration
<per clause: the assertions, their IDs, form, response signal, and the split defect for each>

## Ratios
assertions per clause: <n> (min/max/outliers). No corpus norm exists yet - reported, not judged.

## Not enumerable
<clauses with unstable IDs or that would not decompose - named, never silently skipped>

## Disputes raised / re-decompositions
<clause, what changed, the new UNCLASSIFIED assertions, who must rule>

## Checked / Judgement / NOT CHECKED
<per Step 5, with the verifier named and the command run>
```

---

## What I had to guess — raise these, do not resolve them quietly

1. **The gate's enumeration is two flat string lists.** `EnumerationDocument { Clauses, Assertions }`
   carries no form, no response signal, no supersedes link. So the artifact above is richer than
   anything consumes, and I chose to emit both it and the projection. **Whether the gate should learn
   to read the richer form is the fix for two of the NOT CHECKED rows** and is not my call.
2. ***Nothing records the enumerator's identity, so independence is isolated but not enforced.***
   `SubmissionGate.Check` takes a block author only. One field on the submission plus one comparison
   would close it — see the lane report.
3. **`AssertionForm` is declared by the vector, not looked up.** That is F-3's hole: cite a `NEVER`,
   declare `When`, take the permissive path. Closing it needs the form to live in the enumeration.
4. **STARTUP: fifth bucket or routing tag?** The definition proposes a scheduling attribute so
   "exactly one bucket" survives; §7 reads as a fifth bucket. Unconfirmed. Treated as an attribute.
5. **The dispute channel has no home and no ruler.** Routed to the gate-1 signer by analogy with
   §4.3, and said so — but that is an analogy, not a ruling.
6. **No prior enumeration is stored anywhere**, so "a decomposition that yields a different count is
   reported as its own event" has nothing to compare against. The first stored enumeration makes it
   real; until then the check cannot run.
7. **The assertions-per-clause norm is unmeasured.** Report the ratio, never a threshold.
8. **`(assertion, instance)` qualification happens "at citation time"** — but nothing in the vector
   document carries an instance-qualified assertion ID. Where the `@Instance` suffix is written, and
   whether the gate parses it, is unstated.
