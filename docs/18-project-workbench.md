# 18 — The Project Workbench: a block-centric workflow (v2)

**Status: PROPOSAL, v2. Owner-authored design; recorded and iterated 2026-08-21. Not adopted.**

**What changed from v1:** v1 recorded the owner's design and listed eight problems with it. v2
applies those eight, and adds the owner's two new directives — **a ~20-minute per-block budget
including testing**, and **mechanising the test stage so the AI authors the test vector and nothing
else**. The capability assessment that was §4 of v1 is compressed into §6; the measured evidence
that motivated all of it is kept in §1.2 because every number in §5's budget leans on it.

Relationship to the suite: this replaces the *driving shape* of `docs/15-generation-pipeline.md`
(linear analyse → design → build → check, two engineer gates) with a **per-block lane, many lanes
concurrent**. Most of docs/15's stages survive as steps inside a lane. Hard rules, the data
boundary and `docs/06` conventions are untouched. The staged plan is suspended (`docs/03`), so this
is direction-setting, not stage work.

> **Data boundary.** No live-run vocabulary anywhere in this document — no job number, site,
> equipment, block or tag name. Durations and run counts cited are *our own process data*. Example
> signal names in §4 are invented.

---

## 1. The problem this is solving

### 1.1 The owner's diagnosis

> "I have been a bit disappointed by the length of time it's taken the current version to work
> through the live project… I assumed that you being really good at programming could be
> extrapolated out to doing a whole program start to finish without my intervention."

and, in v2:

> "What I have noticed is the testing nearly takes longer than the design. I really need to
> mechanise this testing stage, where the input from the AI defines the test vector only, and
> everything from the harness down to the ladder on the PLC to read values into the Modbus is all
> determined programmatically."

### 1.2 What the measured record says

From the live job's telemetry: **54 recorded stage runs, 2026-08-05 to 2026-08-21.**

- Authoring a new block: **40 min – 2 h**. Not the problem.
- **Two blocks absorbed six separate ~2-hour fix passes across two days**, each recorded `pass`,
  each followed by another. ~12 hours on two blocks already called done — because "done" was
  decided by *another read of the same text*, with no external oracle.
- Every stage run in the last several days is stamped `STAGED-NO-COMPILE-GATE`: logic is produced
  faster than the gate can run.
- The Portal claim board — a hand-edited text file — has already been raced.

**The conclusion that drives v2: the cost is not in producing logic. It is in (a) deciding it is
finished and (b) the paperwork around proving it.** Both are mechanisable, and §4 argues most of
the machinery already exists.

---

## 2. The design (v2)

### 2.1 Project and master spec

Create a project. It is **given a spec, or one is written alongside the user**. One master spec per
project, and it is the only source of requirement text.

**State lives in files in the repo; the GUI is a viewer over them.** `project.yaml` plus one
`blocks/<name>.md` per block, each carrying an explicit state field. Agents read and write those
files directly. *(v1 §4.4.7 — if the state lives only behind a GUI's own database, the agents
either cannot read it or must go through an API that has to be built first, and the whole point is
that they read and write it.)*

### 2.2 The block record

A block is created with a **shaped description**, not a free-text box *(v1 §4.4.8 — a free-text box
is a new source of the same ambiguity every autopsy in this repo ends at)*:

```yaml
name:        FB_Example
purpose:     one sentence
owns:        what this block is authoritative for
never:       what it must not do
depends-on:  [ other blocks, DBs ]        # -> the DAG, v1 §4.4.2
writes:      [ signal paths ]             # -> conflict detection, v1 §4.4.4
done-when:   the observable condition
```

`writes:` makes **external-conflict detection a set intersection** rather than a judgement — cheap
enough to run on every edit. `depends-on:` makes the parallel wave computable.

### 2.3 The per-block lane

1. **Ambiguity hand-back.** First action on the description: list what is ambiguous, and stop.
   Nothing else runs until it is resolved. Cheapest possible point to catch it.
2. **Carve-out.** The section of the master spec this block fulfils — **as a partition, not an
   extract**. Project-wide, every clause is in exactly one carve-out or on an explicit
   `unallocated` list *(v1 §4.4.3; reuse `signal-sweep`'s denominator-plus-residue pattern)*.
3. **Internal then external consistency** — the second is a `writes:` intersection against every
   other block.
4. **Deliverables.**
5. **Interface**, by `lad-coder`. **Then frozen.** *(v1 §4.4.5 — vectors bind to signals; an
   unfrozen interface invalidates them. Change after freeze is an explicit change-control step.)*
6. **In parallel:** the third-party `assertion-enumerator` decomposes the carve-out into assertions
   — **spec only, never the implementation** — and vectors are authored against the frozen
   interface.
7. **`lad-coder` completes the block.**
8. **Pre-flight** (§4.4), then the **gate queue** (§3), then **rig until green**.

**One rule added to step 8** *(v1 §4.4.6)*: a failing vector may be **fixed in the block**, or
**disputed back to the enumerator** as a wrong assertion. **The block author may never edit a
vector.** Otherwise green is reachable by editing the test.

### 2.4 Waves, the DAG, and the gate queue

Blocks fan out for **authoring**. They **serialize at the gate**, because Portal is one writer per
project and the rig is one program on one CPU *(v1 §4.4.1)*.

- The parallel wavefront is a **DAG level**, computed from `depends-on:` — not "all blocks".
- The gate queue is **owned by the tool**, not a text file, and **batches N blocks into one
  deployment**. §5 shows why this is what makes the 20-minute number reachable.

---

## 3. Mechanising the test stage

**Verdict on the owner's directive: right, and a larger part of it is already built than the
current way of working suggests. The missing piece is smaller than it looks — but it concentrates
the risk in one place, and that place needs a denominator.**

### 3.1 The principle: authored versus derived

> **A field that can be derived must never be authored.** A hand-typed derivable field is only an
> opportunity to disagree with reality, and a gate that checks it is checking the transcription
> rather than the truth.

**Irreducibly authored** — these cannot be derived, and trying would be circular:

| authored | by whom | why it cannot be derived |
|---|---|---|
| the **assertion** | `assertion-enumerator`, from the spec | it *is* the requirement |
| the **stimulus** (given / when) | vector author | a choice about what situation to create |
| the **expected value** (then) | vector author, from the assertion | deriving it from the block makes every test pass |
| the **settling intent** | vector author | "what makes this value final" is a claim about behaviour |

**Everything else is mechanism, and must be derived:** register numbers, `%M` addresses, the
copy-layer ladder, the tag table, bit order, word order, scan budgets, the backstop, compression
factor, timer floors, block numbers, deployment stamps, the map, the observability wiring, the
build stamp.

### 3.2 What already exists (and is the good news)

| the thing | where | state |
|---|---|---|
| **Generates the mirror ladder IR** from an element table — `Bool`→COIL/1 reg, `Int`→MOVE/1 reg, `Time`→MOVE/2 regs — plus the tag table, with `%M` addresses in one auditable place | `Harness.Map/CopyLayerGenerator.cs` | built, deployed, read back |
| Register/address allocation, geometry, bit order, value fit | `Harness.Map/` (`MapAllocator`, `RegisterMap`, `MirrorGeometry`, `MirrorValueFit`) | built |
| Deploy: stage → `to-xml` → `import-all` → layout re-assert → `compile-all` → `sanity-check` → `download-probe` | `Harness.Device/OpennessDeviceGateway.cs` | **run live** — 45 objects, CPU `Running` |
| Read back over Modbus / S7, and measure the declared boundary | `Harness.MirrorRead/`, `Harness.RigRead/` | **run live** |
| PC-side interpreter for the generated LAD subset | `Harness.Skeleton/LadInterpreter.cs` | built |
| The loop that joins them | `Harness.Loop/` | **built, never run** |

**Nobody hand-writes the copy-layer ladder today — it is generated.** The half of the owner's
directive that sounds hardest ("everything down to the ladder on the PLC to read values into the
Modbus") is the half that is already done.

### 3.3 What is *not* derived — and is where the time goes

`Harness.Gate/SubmissionDocument.cs` carries **a dozen sub-documents** — model, block compression,
deployment, S7 objects, timer presets, enumeration, map, storage, vectors, expectations, conflict
edges, blacklist — and **today the AI authors them**, with 25 gates checking what was typed.

That is the "testing takes longer than the design" feeling, and it is worth naming precisely: **it
is not test thinking. It is transcription.** Nearly every one of those fields is already knowable
from an artifact that exists — the block's IR, the generated map, the deployment result, the
measured timing constants in `Harness.Wire/WireTiming.cs`.

### 3.4 The change: `harness derive`

One new step. It reads the artifacts, **builds the submission**, and the gates re-point at the
derivation instead of at the author's typing. What the AI writes per vector collapses to about ten
lines:

```yaml
vector:   V-014
assertion: A-07                       # spec-derived, from the enumerator
given:    { Preset: 2s, StartCmd: false }
when:     StartCmd -> true
then:     Running == true  within Preset
settles:  Running unchanged for 3 scans
```

Everything else — which register `Running` lands in, the `%M` address, the COIL rung that puts it
there, the scan budget, the backstop, the compression factor, the build stamp, the deployment
record — is produced by the tool, from the map it generated itself.

### 3.4a What was BUILT, 2026-08-21 — and the line between it and §3.4

**Shipped:** `harness-gate derive`, plus gate **`0c derived fields`** in `Harness.Results/SubmissionGate.cs`.

> ⚠️ **The deriver ATTRIBUTES; it does not yet COMPUTE.** It records, for every derivable field a
> submission carries, which named producer it came from, out of which artifact, and that artifact's
> SHA-256 — and the gate re-hashes it. It does **not** recalculate the map, the conflict graph or the
> compression ceilings. That distinction is deliberate and is not being blurred: a tool that *claimed*
> to compute them while copying would be the same transcription problem with a longer command line.

Attribution alone still closes the loop, because the four checks are structural: gate 0c refuses a
derivable field carrying no record; only the deriver writes records; the producer must come from a
closed set; and the artifact must still hash to what was recorded. So a derivable field cannot reach
the gate as an unattributed opinion. What remains possible — pointing the tool at the wrong artifact
— is a visible, reviewable act rather than an invisible one.

**Still to build:** computing `map` from the binding and comparing it against the authored one (the
copy-layer generator can already do this, and derive-and-compare is strictly stronger than
attribute); and the reachable-state and compression composers, which live in a different solution
(`src/wave-control`).

### 3.5 Where this must refuse rather than shrug

This is the part I would insist on, because it is this project's signature failure mode wearing a
new hat: **a derived submission is only trustworthy if the derivation has a denominator.** All of
the following are built and tested:

1. **A derivable field with no record is refused, and every such field is named** — not counted.
2. **A stale artifact is refused.** Recorded hash vs the artifact now; a mismatch says *re-derive
   rather than re-stamp*.
3. **An unreadable artifact is NOT CHECKED, never a pass.** "Could not look" is not "it matched".
4. **An unknown producer is refused by name.** Without a closed set, `"producer": "me"` satisfies the
   gate and the author is attesting to their own transcription.
5. **The denominator prints on every run**, including the passing one and the zero case.
6. **`runtimeCompression` cannot be withheld.** It has a default, so dropping the key leaves `1` in
   place — withholding it would not withhold anything, it would forge the quieter claim.

**One planned refusal was dropped, and the reason is a finding.** *"The generator must name an
unsupported type and refuse"* is **redundant**: `MirrorValueType` has exactly four members —
`Unstated`, `Bool`, `Int`, `Time` — so a `Real` or `DInt` signal **cannot be expressed in a binding
at all**. The ceiling is enforced by the type system rather than mishandled at runtime. That makes
widening it a smaller job than §7.2 assumed — one enum member, one element-table row, one copy shape
— and it means the limitation surfaces as an unparseable binding rather than as a wrong mirror.

**The gate-0c-on-zero decision, recorded because it deviates from house style.** Zero derivable
fields present is a **pass**, not the usual empty-is-not-clean refusal. 0c asks whether anything was
hand-authored; with nothing present the honest answer is no. The absence of those fields belongs to
the gates that consume them — omit the deployment and gate 11 cannot run, omit the graph and gate 8
cannot — and a single NOT CHECKED already makes the submission NOT ADMISSIBLE. **Omission is a worse
outcome by a different door, not an escape route**, and that claim is asserted by a test rather than
left as prose.

---

## 4. Getting to ~20 minutes

### 4.1 The three levers, in order of effect

1. **Derive the submission** (§3.4). Removes transcription, which is pure overhead — it buys time
   without any risk being taken.
2. **Batch the gate** (§2.4). The download granularity is **device-level**: the smallest unit
   Openness will transfer is the whole PLC software. Per block that is an unavoidable fixed cost;
   over ten blocks it is a tenth of one.
3. **Pre-flight on the PC** (§4.4). Does not make one cycle faster — makes the *number of cycles*
   smaller, which is the term that actually breaks the budget.

### 4.2 The budget

⚠️ **Measured lines are marked `[M]` with their source; the rest are estimates marked `[E]` and
should be replaced by measurement as soon as one lane has run.** Publishing an unmarked estimate is
how this project got a rig scan time wrong by an order of magnitude, twice.

| line | serial, 1 block/deploy | batched, 10 blocks/deploy | source |
|---|---|---|---|
| ambiguity hand-back + carve-out | 2 min | 2 min | `[E]` |
| interface, then freeze | 2 min | 2 min | `[E]` |
| block IR authoring | 6 min | 6 min | `[E]`, from 40 min–2 h today less spec archaeology |
| vector authoring (~10 lines each) | 2 min | 2 min | `[E]`, after §3.4 |
| PC-side pre-flight | 0.5 min | 0.5 min | `[E]` |
| stage + import | ~40 s | ~15 s | `[M]` `import-all` 182 s / 126 objects |
| compile | ~60 s | ~25 s | `[M]` `compile-all --force` 217 s / 126 |
| download (device-level) | 34–92 s | 5–9 s | `[M]` recovery downloads 34 s and 92 s |
| sanity-check | 4 s | <1 s | `[M]` |
| wave run + read-back | ~1 min | ~1 min | `[M]` scan 24.931 ms, RTT typ 78 ms, comp 4.0× |
| **first-pass total** | **≈ 15 min** | **≈ 14 min** | |
| **with one fix iteration** | **≈ 22 min** | **≈ 17 min** | |
| **with three fix iterations** | **≈ 36 min** | **≈ 23 min** | |

### 4.3 The honest verdict on 20 minutes

**Reachable — as a batched average, on a fully-defined spec, at a first-pass-or-one-retry rate.**
**Not reachable** serially per block if the average block needs three rig iterations, and not
reachable at all while the submission is hand-authored.

So the target does not rest on me being faster. **It rests on the iteration count**, and every
lever in §4.1 is aimed there. I would write the 20 minutes into the design as a **budget with named
line items** rather than as a promise — so that when it is missed, the table says which line broke.

### 4.4 Pre-flight, and its honest limit

`Harness.Skeleton/LadInterpreter.cs` already executes generated IR text on the PC. Extending it to
the block-under-test's IR subset would give a **seconds-long pass before the rig cycle**, catching
the mechanical failures — unwired ports, a wrong comparison sense, an unreachable step — for
roughly nothing.

**Its own documentation states the limit and I will not soften it:** it is not a CPU model. It does
not reproduce scan timing, process-image boundaries, ENO chaining, overflow, optimized-vs-standard
access or retentive behaviour. **Green there is not evidence that the block compiles, or that it
behaves that way on a 1214C.** It is a **pre-filter to make the rig cycle succeed first time —
never a replacement for the rig**, and the moment it is treated as one it becomes the most
dangerous instrument in the system.

---

## 5. The gap register — phases and priorities

**Priority is assigned on three questions, in this order:** does something else rest on it (a
blocker outranks a big win); how much does it move the 20-minute budget; and what does it cost to
get wrong. **P0** = nothing downstream is trustworthy until it is done. **P1** = a named lever on
the budget. **P2** = real, but it waits.

Status vocabulary: ✅ done · 🔨 specified, not built · ❓ needs a decision before it can be specified.

---

### Phase 1 — Close the derive mechanism · **P0** · *specified in full, plan written 2026-08-21*

The mechanism shipped in §3.4a attributes but does not compute, and five gaps remain inside it.
They are grouped as one phase deliberately: each on its own leaves the tool half-honest, and the
refusals only become load-bearing once the values are actually recomputed.

| # | gap | resolution | evidence it is feasible |
|---|---|---|---|
| 1.1 | `map` is attributed, not computed | recompute from the binding, **compare**, refuse on mismatch | `MirrorObservability.FromBindings` already exists and is what the loop uses |
| 1.2 | `runtimeCompression` has **no producer artifact anywhere** | compute via `TimeCompression.Plan` from the attributed `blockCompression`; factor 1 with no declared bounds is trivially computed; **> 1 with no bounds is a refusal** | measured on a real job — 4 of 5 fields attributed, this one had nothing on disk |
| 1.3 | the storage and conflict composers are across a solution boundary | **file contract, not a project reference** — parse `reachable-state.json` and the conflict-graph document in-harness | verified: **zero** project references between `src/harness` and `src/wave-control`, and the harness is deliberately dependency-free |
| 1.4 | the artifact hash is over text-as-read | hash **bytes**, via an optional byte reader; the text path stays as a documented fallback | current limit is recorded in `DerivationHash` |
| 1.5 | pointing the tool at the **wrong artifact** is possible | **kind validation** — each producer declares what its artifact must parse as, and an artifact that is itself a *"not computed"* report is refused | 🔴 measured on a real job: the conflict-graph artifact **was** a `notComputed` report while the submission carried `conflictEdges` anyway |

> 🔴 **1.5 is the one that turned out to matter most, and it was found by accident.** On a real job
> the artifact that should have produced the conflict edges says, in its own words, that it could not
> compute them — and the submission declares the edges regardless. Attribution alone would happily
> stamp that. **An artifact must be the right KIND, and must not itself be a report of failure.**

---

### Phase 2 — Run the loop once · **P0**

🔨 **`Harness.Loop` is built and has never run end to end.** Every number in §4's budget assumes it
works, and the closed-loop conformance path has never taken a wave from author to green.

- 2.1 Run it end to end on a trivial block.
- 2.2 Replace §4.2's `[E]` estimate lines with `[M]` measurements from that run.

**Why P0 rather than P1:** it is not a lever, it is the assumption under every lever. Building
Phase 3 or 5 on top of a loop that has never completed is speculation with a build cost.

---

### Phase 3 — The gate queue · **P1** · *biggest single lever on the budget*

🔨 Tool-owned, batching, replacing the hand-edited claim board — which has **already been raced
once**. Download granularity is device-level (34–92 s measured), so batching is the whole reason
§4.2's batched column beats the serial one.

---

### Phase 4 — The workbench spine · **P1**

Four items that only pay off together; each is cheap and none is useful alone.

- 4.1 **State in files** — `project.yaml`, `blocks/<name>.md`, explicit state field. Everything else
  in this phase reads and writes it.
- 4.2 **The dependency DAG** — `depends-on:` captured at creation; the parallel wave is a DAG level.
- 4.3 **Carve-out as a partition with a residual** — *the one place a silent gap stays invisible
  until commissioning.* Reuse `signal-sweep`'s denominator-plus-residue pattern.
- 4.4 **Per-block `writes:` list** — turns external-conflict detection into a set intersection.

---

### Phase 5 — Pre-flight interpreter · **P1** · *biggest lever on iteration count*

🔨 Extend `Harness.Skeleton/LadInterpreter.cs` to the block-under-test's IR subset (§4.4). The
20-minute target rests on iteration count, and this is the only item that attacks it directly.
**Its limit is not negotiable and is restated wherever it is offered:** not a CPU model, green there
is never evidence about a 1214C, and it is a pre-filter — never a substitute for the rig.

---

### Phase 6 — Element-table widening · **P2** · *smaller than previously thought*

🔨 `Real`, `DInt`, `Word`. Now known to be **one enum member, one element-table row, one copy shape**
each: `MirrorValueType` has four members, so an unsupported type is currently *inexpressible* rather
than mishandled (§3.5). Deferred because nothing is blocked on it until a real block needs one.

---

### Phase 7 — The HMI oracle · **P2** · ❓ *decision before specification*

There is no rig loop for HMI and the device compile is shallow enough to accept a zero-width screen,
so **"until green" cannot mean the same thing.** This cannot be planned until the oracle is chosen —
see §7 Q1. Until then, HMI lanes end at *"compiles and reads back"*, and saying so is the honest
position.

---

### Phase 8 — Multi-agent contention · **P2**

🔨 The claims registry, wave-set admission, slot colouring and escalation ladder are built and
**have never run with two agents actually contending.** Same class of risk as Phase 2, one tier
lower because less rests on it today.

---

### Phase 9 — The GUI · **P2**

🔨 Last, and as a **viewer over Phase 4's files** — never as the system of record (§4.4.7).

---

**Rolled-up ordering:** 1 → 2 → 3 → 4 → 5 → (6, 7, 8, 9 as they become blocking). Phases 1 and 2 are
independent of each other and can run in either order; everything from 3 onward assumes 2 has
happened.

---

## 6. What I can and cannot do

**Can, reliably:** author/convert/import/compile/fix ladder against a written spec; hold one block
and everything about it entirely in mind; run a mechanical loop unsupervised for many hours *when
each iteration ends in a machine-checkable pass/fail*; find a long spec's internal contradictions
faster than a person; be relentlessly consistent about written-down conventions.

**Cannot, reliably — design around these:**

- **Notice that my own evidence is empty.** The most-documented failure in this repo: a check that
  examined nothing and reported green. Mitigation is mechanical, not attentional — **every gate
  prints its denominator**, and an empty one fails. §3.5 is this rule applied to the new deriver.
- **Decide what is true when documents disagree or the spec is silent.** I will pick a reading and
  proceed confidently. Mitigation: the ambiguity hand-back, and stopping being cheap.
- **Know when to stop.** Given "make it good" and no external test, I keep finding things. Six
  two-hour fix passes is what that looks like from outside.
- **Remember anything not written down.** Every session starts from the files.
- **See the plant.** I can tell you a spec contradicts itself. I cannot tell you it is wrong about
  the process, or what is dangerous on your site.
- **Work at whole-project scale in one pass** — a context and error-accumulation limit, which is
  exactly what the block-sized unit fixes.

**On "hours of autonomous work":** the measure is almost certainly a *time horizon* — task length
(by human-expert time) completed at **50% success**. 50% is the definition point, so usable length
is well under it; and those tasks are self-contained, unambiguous and automatically scored, which
this work is not by default. The useful translation: **I run unsupervised as long as each step ends
in a pass/fail a machine can produce. I degrade sharply the moment a loop's exit test is my own
opinion.** That single sentence is why §3 is the most valuable part of this design.

---

## 7. Open questions

- **Q1 — HMI.** The same shape applies, but *"until green" cannot mean the same thing*: there is no
  rig loop, and the HMI device compile is shallow enough to accept a zero-width screen. **What is
  the HMI oracle?** Candidates: read-back comparison after import, a tag-binding completeness
  check, a person looking at a screenshot. Until one exists, HMI lanes end at "compiles and reads
  back", not at "green".
- **Q2 — Element-table width.** Which types must be observable for real blocks? `Real` and `DInt`
  look unavoidable; UDT members and array elements are a bigger question (§3.5.2).
- **Q3 — Batch size.** Ten blocks per deployment makes the budget; it also makes a failure harder
  to attribute. Is a failed batch re-run individually, or bisected?
- **Q4 — Do DBs get a lane?** They have a description and a carve-out but no behaviour to test.
  Suggest: DBs are DAG dependencies with a shape check, not lanes.
- **Q5 — Who signs off the carve-out partition?** It is the one artifact where a silent gap stays
  invisible until commissioning.
- **Q6 — Is the master spec editable after blocks exist?** If yes, an edit must invalidate the
  affected carve-outs and their assertions — a real mechanism, not a note.

---

## 8. Change log

- **v1 — 2026-08-21.** Owner's design recorded; eight problems raised; capability assessment.
- **v2 — 2026-08-21.** All eight applied into the design itself. Added the ~20-minute budget (§4,
  measured lines marked) and the mechanised test stage (§3) — authored-vs-derived, `harness derive`,
  and the two places derivation must refuse. Added the finding that the copy-layer ladder is
  **already generated** and the real gap is the hand-authored submission document. Capability
  assessment compressed to §6.
- **v2.2 — 2026-08-21.** §5 replaced by a **phased, prioritised gap register** (nine phases, P0/P1/P2
  on a stated rule). Phase 1 — the five gaps left inside the derive mechanism — is specified in full
  and has an implementation plan. Two feasibility facts pinned while writing it:
  `MirrorObservability.FromBindings` and `TimeCompression.Plan` make 1.1 and 1.2 genuinely
  computable in-harness, and there are **zero project references** between `src/harness` and
  `src/wave-control`, so 1.3 is a file contract rather than a reference. 🔴 And one finding: on a
  real job the conflict-graph artifact is a **`notComputed` report** while the submission declares
  the edges anyway — which is why 1.5 (artifact kind validation) exists at all.
- **v2.1 — 2026-08-21.** §3.4a and §3.5 rewritten to describe what was actually BUILT rather than
  what was planned: `harness-gate derive` attributes rather than computes, and that line is drawn
  explicitly. Six refusals shipped; one planned refusal dropped as **redundant** — `MirrorValueType`
  has four members, so an unsupported type is inexpressible rather than mishandled, which also makes
  widening the element table smaller than §7.2 assumed. Recorded the gate-0c-on-zero deviation and
  the test that backs it. Measured on the live job: **5 derivable fields per submission, all five
  hand-authored today, and 4 of the 5 have a real producing artifact on disk while
  `runtimeCompression` has none.**
