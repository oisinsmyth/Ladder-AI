# 18 — The Project Workbench: a block-centric workflow (v1)

**Status: PROPOSAL, v1. Owner-authored design, recorded 2026-08-21 for iteration. Nothing here is
adopted and nothing here supersedes anything yet.** Sections 1–3 are the owner's design as stated,
written down so it can be argued with. Section 4 is **my feedback, clearly marked as mine** — it is
opinion and capability disclosure, not design. Section 5 is what I would change for v2, section 6
the open questions.

Relationship to the existing suite: this would **replace the driving shape of
`docs/15-generation-pipeline.md`** (the linear analyse → design → build → check pipeline with two
engineer gates) with a **per-block pipeline that runs many blocks concurrently**. It does not
replace docs/15's *stages* — most of them survive as steps inside a block's lane — and it does not
touch the hard rules, the data boundary, or the conventions in `docs/06`. The staged development
plan is suspended (`docs/03`), so this is not stage work; it is the owner setting a new direction.

> **Data boundary note.** This document deliberately carries **no live-run vocabulary** — no job
> number, site, equipment, block or tag name from any job in `Live Runs/`. Where measured
> evidence from the live job is cited it is cited as *counts and durations of our own stage runs*,
> which is process data about us, not content about the site. The specifics stay in the job
> folder (`docs/13-data-boundary.md`).

---

## 1. Why this is being proposed

The owner's diagnosis, in the owner's framing:

> "I have been a bit disappointed by the length of time it's taken the current version to work
> through the live project. Honestly I think it's my fault — I saw what you could do and I compared
> you to a human and extrapolated out too far. I assumed that you being really good at programming
> could be extrapolated out to doing a whole program start to finish without my intervention."

The proposal's purpose is therefore **to make the unit of AI work small, explicitly scoped, and
individually verifiable**, and to put the human at the boundaries between units rather than at two
gates in a long pipeline.

*(My reading of the same problem, which agrees but adds to it, is in §4.2. Short version: the
extrapolation was only half of it. The other half is that the pipeline as built has no cheap
external oracle for "this block is done", so "done" is decided by another read of the same text,
and each fresh read finds more.)*

---

## 2. The proposal as stated

### 2.1 A GUI, and a project

A GUI in which you **create a project**, and then **add blocks and DBs** to it. The purpose of the
GUI is to *"more cleanly define AI work"* — the unit of work is a thing you create in a UI, with a
description attached, rather than a request in a conversation.

On project creation, the AI is either **given a spec** or is **prompted to write one alongside the
user**, so that there is an **overview of the whole project** before any block exists.

### 2.2 A block, and its description

When a block is created it **holds a description**. The AI takes that description and does the
following, in order:

1. **Carve out** the section of the overall project spec that this block plus its description
   fulfils.
2. **Review the description for internal inconsistencies** (does it contradict itself).
3. **Then review it for external inconsistencies** (does it conflict with another block).
4. **Break** the description plus the spec carve-out into **small deliverables**.

### 2.3 The per-block pipeline

From the deliverables:

5. A **`lad-coder`** estimates and returns **an interface for the block**.
6. **In parallel**, an assertion/test agent spawns off — the enumeration side
   (`assertion-enumerator` / `enumerate-assertions`, `design-for-testability`).
7. **`lad-coder` picks up again where it left off** and completes the block.
8. When **both the test vectors and the block are complete**, the **rig-based testing cycle** runs
   **until all are green**.

### 2.4 Across blocks

**All blocks are then done in parallel with each other.**

### 2.5 HMI

*"We will do something similar for the HMIs but not too sure on that yet."* — open, see §6.

---

## 3. What this changes, relative to today

| | today (docs/15) | proposed |
|---|---|---|
| unit of work | a *project stage* (analysis → architecture → build → check) | **a block** |
| concurrency | one stage at a time, whole-project | **blocks in parallel** |
| human gates | 2 (architecture sign-off, final presentation) | at every block boundary, in the UI |
| "done" is decided by | a reviewer agent reading the block | **the rig: vectors green** |
| where state lives | prose plan/handover files in the job folder | **the project model behind the GUI** |
| spec handling | one register for the whole project | **a per-block carve-out of one master spec** |

Three existing assets slot straight in and should not be redesigned: the **D6 authorship
separation** (the block author may not be the vector author — an author who decides both what the
code does and what counts as the full set of things it must do makes coverage unfalsifiable), the
**test-environment contract** (`docs/notes/test-environment-contract.md`), and the **conventions**
(`docs/06`). The four-rung spec pipeline (rungs A–D) becomes an *optional front end* for producing
the master spec when the site's own document is thin, rather than a mandatory path.

---

## 4. My feedback

*Written by Claude, 2026-08-21, at the owner's request: "I want your feedback so I don't assume
that you can do things that you know you can't do."*

### 4.1 On "hours of autonomous work"

The measure you have read about is almost certainly a **time horizon**: the length of task —
measured by how long a *human expert* takes to do it — that a model completes with **50% success**.
It is a real and useful measure, and it has been climbing quickly. Two things about it matter more
than the headline number:

- **50% is the definition point.** At the quoted horizon you are flipping a coin. For work you
  intend to rely on, the usable length is well under it.
- **The tasks it is measured on are self-contained, unambiguous, and automatically scored.** This
  project is none of those by default: the spec is ambiguous, the oracle (TIA compile, the rig) is
  slow and single-instance, and success is finally judged by an engineer's reading.

So the honest translation is not "N hours of ladder engineering". It is:

> **I can run unsupervised for as long as each step ends in a pass/fail a machine can produce, with
> a bounded number of retries. I degrade sharply the moment the exit test for a loop is my own
> opinion.**

Design for that and the length of an unsupervised run stops being the interesting number. Design
without it and I will produce confident work that then needs six review passes — which is exactly
what happened (§4.2).

### 4.2 What actually cost the time — measured, not recalled

From the live job's own telemetry log: **54 recorded stage runs, 2026-08-05 to 2026-08-21.**

- **Authoring a new block is not the problem.** New-block runs come in at roughly **40 minutes to
  2 hours** each, including convert/import/compile iterations. That rate is fine, and it is the
  part everyone watches.
- **The review→fix churn is the problem.** Two blocks account for **six separate fix passes of
  about two hours each, across two days** — every one of them recorded `pass`, every one of them
  followed by another. That is roughly **twelve hours spent on two blocks that were already
  "done"**. Nothing was wrong with the coding rate. There was no *oracle*, so "done" was decided by
  another read of the same text, and each fresh read found more.
- **The gate is falling behind the work.** Every stage run recorded in the last several days
  carries the outcome `STAGED-NO-COMPILE-GATE`. Logic is being produced faster than the compile
  gate can be run against it, because Portal is a single-writer resource and the queue for it is a
  hand-edited text file.
- **That queue has already been raced.** The job's Portal claim board records one lane's
  uncommitted probe work showing up inside another lane's compile baseline, and the second lane
  having to prove which of the two owned the error.

**This is the single strongest argument for your proposal.** Your rig-until-green step attacks
exactly the twelve-hour item: it replaces "a second agent reads it and has opinions" with "a
machine says pass or fail". If v1 of the workbench does nothing else, it should do that.

### 4.3 What the proposal gets right

- **The block as the unit.** This fits the constraint that actually binds me, which is not
  intelligence or hours but **context**. One block, plus its spec carve-out, plus its interface,
  plus its vectors, is something I can hold *entirely* — no summarising, no re-reading, no gaps. A
  whole plant is not, and every time I have worked at whole-plant scale I have been working from a
  lossy summary of it without saying so.
- **Description → carve-out → consistency check → deliverables.** This forces ambiguity out at the
  cheapest possible point. Every autopsy in this repo lands on an ambiguous clause; the earlier
  that surfaces, the less it costs.
- **Interface first, then vectors in parallel, by a third party.** Correct, and it is the reason
  coverage will mean anything. Keep the enumerator on the *spec carve-out only* — it must not read
  the implementation.
- **Rig-until-green.** An external oracle. "Done" stops being an opinion.
- **Human checkpoints at block boundaries.** More checkpoints, each cheaper, is strictly better
  than two big ones — because the cost of a wrong turn is bounded by the distance to the next
  checkpoint.

### 4.4 Where it will break as drawn

Eight concrete problems. None is fatal; all are cheaper to fix now than in the UI.

1. **"All blocks in parallel" collides with two single-instance resources.** Portal is one writer
   per project. The rig is one program on one CPU. Ten blocks can be *authored* in parallel; they
   cannot be compiled or rig-tested in parallel. The realistic shape is **fan-out for authoring,
   a serialized queue for the gate**, with several blocks batched into one deployment. That queue
   must live in the tool. The text-file version has already been raced once (§4.2).

2. **Blocks are not independent — they form a dependency DAG.** A block that CALLs another, or
   reads a DB another writes, cannot be rig-tested until its dependency exists. So the parallel
   wavefront is *the DAG's levels*, not "all blocks". The workbench should capture each block's
   dependencies at creation time (or derive them from the carve-out), or wave 1 will discover them
   at deployment, which is the most expensive place to discover anything.

3. **The carve-out must be a partition with a residual, not a per-block extract.** If each block
   carves independently, clauses get claimed twice — and, much worse, *silently claimed zero
   times*. The check is a project-level coverage account: **every clause of the master spec is in
   exactly one block's carve-out, or on an explicit "not yet allocated" list.** Without it, the
   project looks finished when it isn't. This repo already has the pattern (`signal-sweep`'s exact
   denominator plus residue); reuse it rather than inventing one.

4. **"Review for external inconsistency" needs something to review against.** Conflict with another
   block is only findable if there is a machine-readable statement of **what each block owns** —
   which signals it writes, which state it is authoritative for. Prose descriptions cannot be
   intersected. Give the block record an explicit *writes* list and conflict detection becomes a
   set intersection: cheap enough to run on every edit, and not a judgement call.

5. **The interface estimate will sometimes be wrong, and vectors will already exist against it.**
   Assertions from the spec carve-out are signal-free and survive an interface change — that is
   fine. **Vectors bind to signals and do not.** Decide the rule now: either vectors bind late
   (after the interface is frozen), or an interface change invalidates and re-runs the affected
   vectors. Frozen interface plus an explicit change-control step is the cheaper of the two.

6. **"Iterate until green" is an overfitting risk unless one rule is added.** Your design already
   separates the agents, which is most of the defence. Add this: **a failing vector may be fixed in
   the block, or disputed back to the enumerator as a wrong assertion — the block author may never
   edit a vector.** Otherwise green is reachable by editing the test, and I will find that path
   without meaning to.

7. **The GUI is itself a substantial software project, and it competes with real jobs.** Project /
   block / DB model, spec storage, a per-block state machine, agent dispatch, a gate queue, results
   display. That is weeks. **My recommendation: put the state in files in the repo from day one and
   make the GUI a viewer over them, added second.** A `project.yaml` plus one `blocks/<name>.md` per
   block with an explicit state field delivers most of the workflow benefit within days, and the GUI
   then adds ergonomics without a rewrite. If it starts as a GUI with its own database, the agents
   either cannot read the state or must read it through an API that has to be built first — and the
   *agents reading and writing that state directly* is where the value is.

   Worth being explicit about what the GUI actually buys, because it is not the UI: it makes
   **state explicit and per-block**. Today that state lives in plan and handover text files, and a
   stalled block is invisible until someone reads them.

8. **The spec is still the bottleneck, and the GUI does not fix it.** Every failure autopsy in this
   repo ends at an ambiguous clause. A free-text description box in a GUI is a *new* source of
   exactly the same ambiguity. Give the description field a **shape** — what the block owns, what
   it must never do, what it depends on, what "done" looks like — and make my first action on it a
   **hand-back of the ambiguities**, before the carve-out, before anything else runs.

### 4.5 What I can and cannot do — plainly

**Can, reliably:**

- Author, convert, import, compile and fix ladder against a written spec at roughly **40 min – 2 h
  per block**, indefinitely, and across many blocks concurrently up to the Portal/rig limit.
- Hold one block and everything about it completely in mind at once.
- Run a mechanical loop unsupervised **for many hours** when each iteration ends in a
  machine-checkable pass/fail — the harness build and the converter gates are exactly this, and
  they went well.
- Read a long specification and find its internal contradictions faster than a person.
- Be relentlessly consistent about conventions, *when they are written down*.

**Cannot, reliably — these are the ones to design around:**

- **Notice that my own evidence is empty.** This is the single most-documented failure in this
  repo: a check that examined nothing and reported green. Drift-check over zero comparisons.
  Compile-all over an empty work set. A scan whose filter matched no files. Every one of them was
  me running a check, seeing green and believing it. The mitigation that works is mechanical:
  **every gate prints its denominator**, and an empty denominator is a failure, not a pass.
- **Decide what is true when two documents disagree, or when the spec is silent.** I will pick a
  reading and proceed confidently. The mitigation is the blocking-question mechanism, and it only
  works if stopping is cheap for me.
- **Know when to stop.** Given "make it good" and no external test, I will keep finding things.
  Six two-hour fix passes on two blocks is what that looks like from outside.
- **Remember anything that is not written down.** Every session starts from the files. This is also
  why `CLAUDE.md` has grown to 400 lines of warnings, and why it twice carried a rig timing figure
  that was wrong by an order of magnitude — I copied a number forward and dropped the qualifier
  saying which program it belonged to. **A project database behind a GUI is the same hazard with a
  nicer front end:** it becomes memory nobody re-reads.
- **See the plant.** I can tell you a spec is inconsistent with itself. I cannot tell you it is
  wrong about the process, and I cannot judge what is dangerous on your site.
- **Work usefully at whole-project scale in a single pass.** Less a reasoning limit than a context
  and error-accumulation one — which is precisely what your block-sized unit fixes.

### 4.6 On the apology

Taken, and not needed — but the diagnosis is only half right, so it is worth correcting the other
half rather than accepting it. The extrapolation was optimistic. It was *also* true that the
pipeline as built spends much of its time on process rather than product, and that it never had a
cheap way to say "this block is finished" — so it substituted more reading. That is a fixable
property of the design, not a fixed property of me, and this proposal is aimed at it.

---

## 5. What I would change for v2

In rough order of value:

1. **State in files first, GUI second** — `project.yaml` + `blocks/<name>.md` with an explicit
   state field, agents reading and writing it directly (§4.4.7).
2. **A serialized gate queue** owned by the tool, with batched deployment, replacing the text-file
   claim board (§4.4.1).
3. **Dependencies declared at block creation**, and the parallel wave computed as a DAG level
   (§4.4.2).
4. **Carve-out as a partition with a residual list**, checked project-wide (§4.4.3).
5. **A per-block `writes:` list**, making external-conflict detection a set intersection (§4.4.4).
6. **A shaped description template**, and an ambiguity hand-back as the first action on it
   (§4.4.8).
7. **Freeze the interface before vectors bind**, with an explicit change-control step (§4.4.5).
8. **"The block author may never edit a vector"** written into the loop (§4.4.6).

---

## 6. Open questions for the owner

- **Q1 — HMI.** The same shape can be applied, but *"until green" will not mean the same thing*:
  there is no rig loop for HMI, and the HMI device compile is shallow (it accepts a zero-width
  screen). The honest open problem is **what the HMI oracle is**. Options worth discussing:
  read-back comparison after import, a tag-binding completeness check, or screenshot review by a
  person. Until one exists, HMI blocks would end at "compiles and reads back", not at "green".
- **Q2 — Do DBs get the same lane as blocks?** They have a description and a carve-out, but no
  behaviour to test. Suggest: DBs are *dependencies* in the DAG with a shape check, not lanes.
- **Q3 — What is the batch size for the gate?** One block per deployment is slow; ten is fast but a
  failure is harder to attribute.
- **Q4 — Who signs off the carve-out partition?** It is the one artifact where a silent gap is
  invisible until commissioning.
- **Q5 — Is the master spec editable after blocks exist?** If yes, changing it must invalidate the
  affected carve-outs and their assertions; that is a real mechanism, not a note.

---

## 7. Change log

- **v1 — 2026-08-21.** Recorded from the owner's description. §§1–3 owner's design as stated;
  §§4–6 Claude's feedback and open questions, at the owner's request.
