# 18 — The Project Workbench: a block-centric workflow (v2)

**Status (2026-08-24): ADOPTED AND LARGELY BUILT. Six of the ten phases are delivered; FOUR of those
six have run on a controller. The four that are not delivered are each CLOSED rather than pending.**
Phases **2, 3, 6 and 10** are delivered **and have run on a controller**. Phases **1 and 4** are
delivered and **have not** — see the correction below. Phase 4 was **redefined** and Phase 6
**re-scoped** before either was built (see §5 — both kept their numbers, neither kept its original
scope). Phase 5 is **decided and declined as a build**; Phase 7 is **closed, its premise overtaken**;
Phase 8 is **built but never contended**; Phase 9 is **struck**. **Nothing in §5 is now waiting to be
picked up as the next thing** — §5's closing note says what is, and it is not a phase.

🔴 **CORRECTION 2026-08-24 — THIS LINE AND §5's TABLE CLAIMED CONTROLLER EXERCISE FOR TWO PHASES THAT
NEVER HAD IT.** Both read *"delivered, and run on a controller · **1 · 2 · 3 · 4 · 6 · 10**"*.
Checked against the phase bodies in this document:

- **Phase 1** (§5, *Close the derive mechanism*) contains **no mention of a rig, controller, download
  or deployment**. Its five gaps are derive-mechanism work and its strongest evidence is *"the dry run
  over the live job found four defects"* — a dry run, stated as one in its own text.
- **Phase 4** (§5, *the lane, generated*) delivers `SlotFcGenerator`, `StimShellGenerator`, an emitted
  lane manifest and content-matching in `converter diff`. Its heading says **DELIVERED**, not *run*.
  The one controller sentence in its body — the area *"widened 576 → 1024 and proven on the
  controller"* (`99396b9`) — is **Phase 6's run cited inside a Phase 4 argument**, not Phase 4's own.
  It may well be that Phase 4's generated lane manifests fed the Phase 6 rig run; **no document in
  this repo says so, so it is unestablished and the claim is not made here.**

This is the **same direction** as the overclaim struck in `c851ea0` a day earlier, which wrote the
rule: *a document wrongly saying "not yet proven" costs a re-check; one wrongly saying "run live"
costs the check itself.* It was also **self-contradictory inside this file** — §0 claimed six phases
on a controller while §3.2 said the deploy gateway is **BUILT — NEVER RUN**.

🔴 **AND THIS LINE HAD ALREADY UNDERSTATED ITS OWN BODY TWICE, TWO DAYS APART.** It read *"Not
adopted"* until 2026-08-23 with four phases marked delivered inside the same document; it was repaired
that morning to *"the rest is still proposal"* and by that evening Phase 6 had been re-scoped,
delivered and **run on a controller** while the header still called it proposal. A reader who trusts
the header discounts the whole page, including the parts running on hardware. Recorded rather than
quietly overwritten, because the same shape — a status line that stopped tracking its own body — is
what §3.2's table did, what §5's Phase 3 and Phase 4 headings did, and what the change log below did.
**The repair is not a better sentence; it is that closing a phase includes closing its markers, in the
same commit.** ⚠️ **The header has now failed in BOTH directions**, which is the argument in §8 v3.6
for making the stamp mechanical rather than remembered a fourth time.

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
| Deploy: stage → `to-xml` → `import-all` → layout re-assert → `compile-all` → `sanity-check` → `download-probe` | `Harness.Device/OpennessDeviceGateway.cs` | 🔴 **BUILT — NEVER RUN.** This cell read **"run live — 45 objects, CPU `Running`"** until 2026-08-24, and it was the widest version of that claim anywhere: the only one naming the gateway file, adding the `download-probe` step, and saying *run live*. **`Harness.Device` appears ZERO times in `docs/notes/test-log.tsv`.** What actually ran on 2026-08-14 was the constituent binaries, driven separately: `download-probe` loaded the 45 objects at 00:52 (`test-log.tsv:2`), `rig-read` read `Running(8)` at 00:58 (`:3`), and `openness-cli` did the Portal work at 02:40 — `import-all` of **nine** objects, then `compile-all` and `sanity-check` (`:5`), **after** the download rather than before it, which is the reverse of this sequence. **Nothing has ever executed the sequence as one program.** |
| Read back over Modbus / S7, and measure the declared boundary | `Harness.MirrorRead/`, `Harness.RigRead/` | **run live** |
| PC-side interpreter for the generated LAD subset | `Harness.Skeleton/LadInterpreter.cs` | built |
| The loop that joins them | `Harness.Loop/` | ✅ **RUN END TO END FOUR TIMES** (2026-08-18, 08-20, twice on 08-22). 🔴 This cell read **"built, never run"** until 2026-08-23 while §5 Phase 2 of THIS DOCUMENT already recorded the opposite — §5 was corrected and §3.2 was not, so one document held two answers and the wrong one was the one a reader met first. |

**Nobody hand-writes the copy-layer ladder today — it is generated.** The half of the owner's
directive that sounds hardest ("everything down to the ladder on the PLC to read values into the
Modbus") is the half that is already done.

### 3.3 What is *not* derived — and is where the time goes

`Harness.Gate/SubmissionDocument.cs` carries **a dozen sub-documents** — model, block compression,
deployment, S7 objects, timer presets, enumeration, map, storage, vectors, expectations, conflict
edges, blacklist — and **today the AI authors them**, with **30 gates** checking what was typed
(distinct gate names defined in `Harness.Results/SubmissionGate.cs`, counted 2026-08-23). *This read
**25** until 2026-08-23, which is a different metric: 25 rows **emitted** on one real deliverable on
2026-08-14, when 26 were defined. Not every gate emits on every run.*

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

### 3.6 The solution boundary — ✅ **RULED 2026-08-23: `src/harness` MAY reference `src/converter`**

**Owner ruling, 2026-08-23.** `src/harness` may take a project reference on `src/converter`. This is
recorded here because **this is where a future reader meets the constraint** — the property itself
lives in `src/harness/Directory.Build.props`, whose comment presents dependency-freedom as deliberate
and gives no hint that any part of it has been relaxed.

🔴 **THE RULING COVERS THE CONVERTER ONLY. IT DOES NOT GENERALISE, AND THE ADJACENT PRECEDENT STILL
STANDS.** §5 Phase 1's row 1.3 records a **verified zero project references between `src/harness` and
`src/wave-control`**, settled as **a file contract, not a project reference** — parse
`reachable-state.json` and the conflict-graph document in-harness. **That decision is untouched.**
Two different boundaries, two different answers, and the reason they differ is not arbitrary: the
converter is the **single** IR parser this project has, and referencing it *removes* a parallel
implementation; `wave-control`'s composers are producers whose outputs are already documents, and
referencing them would *add* coupling to buy nothing. **Do not read the converter ruling as a general
licence for the harness to reference whatever it likes.**

**What the ruling unblocks.** Phase 5's stated blocker — *"extend the interpreter to real block IR
means a second IR parser inside a deliberately dependency-free solution"* — was a choice between two
things this project rejects on sight. It is now a choice between one of them and a project reference.
That does not decide Phase 5; it removes the reason Phase 5 was *not executable as written*.

**The cost the ruling accepts, stated up front rather than discovered later:**

- **It couples harness builds to the converter's**, so **FI-73's Release-staleness class gets a new
  home**. FI-73 is the day of converter fixes that reached no agent because only Debug was rebuilt; the
  same shape now becomes available to the harness, which until this ruling could not have it.
  `dotnet build -c Release src/converter/converter.sln` after any converter change was already the
  rule; it now has a second reason.
- **It does NOT touch the TIA Openness `(Path, FileHash)` re-approval cycle.** `Directory.Build.props`'s
  comment names that cycle as a benefit of dependency-freedom, and on that specific point the comment's
  conclusion survives its premise: **the converter has no TIA whitelist and never contacts Portal**, so
  a harness that references it still triggers no approval. Only a `Siemens.Engineering` dependency
  would, and none is being added.
- **It does not change the harness's `net8.0` target**, which the converter shares.

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
| download (device-level) | 34–92 s | 5–9 s | `[M]` recovery downloads 34 s and 92 s; **86 s measured again 2026-08-22** |
| wave run + read-back | ✅ **~45 s per vector** | ✅ **~45 s per vector** | `[M]` **2026-08-22 at comp 4, measured on the controller: 3 vectors, 134 s, 1,676 round trips, ALL THREE Pass and Settled.** Was **~2.7 min per vector** (485 s, 6,185 round trips) uncompressed — **3.6× on wall clock, 3.7× on traffic.** A three-vector block is now **2.25 min**, not 8.1 |
| stage + import | ~40 s | ~15 s | `[M]` **5 s measured 2026-08-22** for a 3-object import — the estimate was ~8× pessimistic |
| compile | ~60 s | ~25 s | `[M]` **24 s measured 2026-08-22** (`compile-all`, whole device) |
| sanity-check | 4 s | <1 s | `[M]` **3 s measured 2026-08-22** |
| **first-pass total** | **≈ 15 min** | **≈ 14 min** | |
| **with one fix iteration** | **≈ 22 min** | **≈ 17 min** | |
| **with three fix iterations** | **≈ 36 min** | **≈ 23 min** | |

> 🔴 **THE FIRST MEASURED LINE BROKE THE BUDGET, AND THAT IS WHAT THE `[E]` MARKS WERE FOR.** One
> estimate replaced by measurement moved the wave from ~1 min to ~2.7 min *per vector* — on a
> three-vector block that is ~8 min against a 20-min target, before authoring. The remaining `[E]`
> lines are not more trustworthy for having survived longer; they are simply unmeasured.
>
> ⚠️ **AND THE CAUSE IS NOT THE HARNESS — IT IS THE VECTORS.** Diagnosed 2026-08-22 (§5, Phase 10):
> polling is 98.7% of the traffic and ~0% of the elapsed time, because the loop merely keeps pace with
> a scenario whose length the vector itself declares. On the dominant index the declared scenario end
> predicted the measured runtime to **0.8%**. So this line does not come down by making the wire
> faster; it comes down by **compressing the plant clock** or by **not spending half an index hedging
> against an unknown warm-up phase.** Both are Phase 10.

### 4.3 The honest verdict on 20 minutes

**Reachable — as a batched average, on a fully-defined spec, at a first-pass-or-one-retry rate.**
**Not reachable** serially per block if the average block needs three rig iterations, and not
reachable at all while the submission is hand-authored.

So the target does not rest on me being faster. **It rests on the iteration count**, and every
lever in §4.1 is aimed there. I would write the 20 minutes into the design as a **budget with named
line items** rather than as a promise — so that when it is missed, the table says which line broke.

### 4.4 Pre-flight, and its honest limit

🔴 **THIS SECTION IS ANNOTATED AS OF 2026-08-23 AND YOU SHOULD READ §5 PHASE 5 BEFORE ACTING ON IT.**
Two things below are wrong, and one of them is the word *"roughly nothing"*.

- **"Extending it" is not an extension.** `LadInterpreter.cs` implements MOVE, ADD and the coil family
  only, resolves addresses in `%M` and nothing else, and has no notion of a DB, a UDT, an FB instance, a
  timer or an interface parameter. A deliverable block uses all of those. Reaching it means an IR parser
  plus LAD execution semantics written a **second** time — inside a solution that is dependency-free on
  purpose. *(⚠️ The "second time" half is now avoidable: the owner ruled 2026-08-23 that `src/harness`
  may reference `src/converter` — §3.6. The **cost** half of the sentence stands; the **impossibility**
  half does not.)*
- **Three of the four catches listed are already covered**, by producers with names: unwired ports and
  disarmed logic by `undriven-scan`, an unreachable step by `cross-check`'s `REACHABILITY:` line. Only
  **a wrong comparison sense** is genuinely uncovered — and an interpreter catches that only *given a
  vector and a plant model to run the block against*, which is not a pre-filter, it is an emulator.

🔴 **AND THE THIRD THING WRONG IS THE FRAME, WHICH THE MEASUREMENT FOUND AND THESE TWO BULLETS DID
NOT: this section places the interpreter BEFORE THE RIG CYCLE, and that is the placement the evidence
does not support.** Of 37 non-Pass rig verdicts, **2 were defects in the block** and both are already
caught by C-410; the other 35 were instrument, declaration, model or deployment faults, which no
reading of the block's IR can see. The 17-row bucket that *does* justify an interpreter sits in the
**design and review loop**, before a harness or a binding exists. Counts, rubric, controls and the
three limits that travel with them:
`docs/notes/preflight-interpreter-classification.md`. The redirect is recorded in §5 Phase 5.
**The last bullet above was closer to right than it knew** — *"not a pre-filter, it is an emulator"*
is the same finding arriving from the other side.

*The paragraph below is retained as written because the argument it makes about the LIMIT is still
exactly right, and is the part worth keeping.*

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

> 🔴 **READ THIS BEFORE THE LIST. THE LIST IS FINISHED — THERE IS NO "NEXT PHASE" TO PICK.**
>
> | | |
> |---|---|
> | ✅ delivered, **and run on a controller** | **2 · 3 · 6 · 10** |
> | ✅ delivered, **never run on a controller** | **1 · 4** |
> | ✅ decided · 🚫 declined as a build | **5** |
> | 🚫 closed, premise overtaken | **7** |
> | ✅ built · ⚠️ never contended (residual is an agent-identity contract question) | **8** |
> | 🚫 struck, its subject deleted | **9** |
>
> ⚠️ **The first two rows were ONE row reading `1 · 2 · 3 · 4 · 6 · 10` until 2026-08-24.** Phase 1's
> body names no rig and closes on a **dry run**; Phase 4's deliverables are generators and `converter
> diff`, and the only controller sentence in it is Phase 6's run quoted inside a Phase 4 argument. §0
> carries the full correction. **Delivered and exercised-on-hardware are different claims and this
> table now separates them.**
>
> **4 and 6 kept their numbers and did NOT keep their scope** — the spine and the element table were
> both argued down before they were built, and what shipped under those numbers is different work.
> Read the heading, not the number.
>
> ➜ **What is actually binding is not in this list. It is in §5z at the foot of this section: the
> third-party enumerator EXISTS and has been run ONCE, on one block in the whole corpus.** A session
> that scrolls this register looking for the next thing to build will find greens and closures and
> conclude the register is the wrong question. **That conclusion is correct — §5z is the answer to
> it, and the answer is dispatchable in a session.** Jump there.

**Priority is assigned on three questions, in this order:** does something else rest on it (a
blocker outranks a big win); how much does it move the 20-minute budget; and what does it cost to
get wrong. **P0** = nothing downstream is trustworthy until it is done. **P1** = a named lever on
the budget. **P2** = real, but it waits.

Status vocabulary: ✅ done · 🔨 specified, not built · ❓ needs a decision before it can be specified ·
🚫 **closed without being built** — struck, or decided-and-declined. **A phase with no marker is a
defect in this list, not a phase in flight.** *(🚫 added 2026-08-23. The vocabulary was never the
problem — every marker below that misreported was precise and stale, not imprecise. What was missing
was a symbol for the outcome three phases actually reached: closed, on evidence, without a build.)*

---

### Phase 1 — Close the derive mechanism · **P0** · ✅ **DONE 2026-08-21**

> **All five closed.** `map` is now recomputed from the binding and compared; `runtimeCompression` is
> settled by a stated rule (uncompressed needs no bounds, a factor above 1 with none is refused); the
> composer outputs are read in-harness through a file contract; artifacts are hashed over **bytes**;
> and an artifact must be the right **kind** and must not be a **report of failure**.
>
> 🔴 **The dry run over the live job found four defects in this work that its own tests did not**, and
> all four were the same class — *a check that is too strict is as damaging as one that is too weak*:
> the map comparison refused an incomplete-but-harmless map (now asymmetric — over-claiming refuses,
> under-claiming reports); the storage check accused 131 correct declarations because the two
> documents use different canonical spellings (now canonicalised, and a total miss reads as *wrong
> closure*, not 131 findings); a **valid, fully-computed conflict graph was refused as WRONG KIND**
> because it uses a third key spelling; and derive hashing over bytes while the gate hashed text would
> have made **every** record read as stale. Each is now a test.

The mechanism shipped in §3.4a attributes but does not compute, and five gaps remain inside it.
They are grouped as one phase deliberately: each on its own leaves the tool half-honest, and the
refusals only become load-bearing once the values are actually recomputed.

| # | gap | resolution | evidence it is feasible |
|---|---|---|---|
| 1.1 | `map` is attributed, not computed | recompute from the binding, **compare**, refuse on mismatch | `MirrorObservability.FromBindings` already exists and is what the loop uses |
| 1.2 | `runtimeCompression` has **no producer artifact anywhere** | compute via `TimeCompression.Plan` from the attributed `blockCompression`; factor 1 with no declared bounds is trivially computed; **> 1 with no bounds is a refusal** | measured on a real job — 4 of 5 fields attributed, this one had nothing on disk |
| 1.3 | the storage and conflict composers are across a solution boundary | **file contract, not a project reference** — parse `reachable-state.json` and the conflict-graph document in-harness | verified: **zero** project references between `src/harness` and `src/wave-control`, and the harness is deliberately dependency-free. ⚠️ **STILL TRUE OF `wave-control` after the 2026-08-23 ruling** — that ruling covers **`src/converter` only** (§3.6); this row is not superseded by it |
| 1.4 | the artifact hash is over text-as-read | hash **bytes**, via an optional byte reader; the text path stays as a documented fallback | current limit is recorded in `DerivationHash` |
| 1.5 | pointing the tool at the **wrong artifact** is possible | **kind validation** — each producer declares what its artifact must parse as, and an artifact that is itself a *"not computed"* report is refused | 🔴 measured on a real job: the conflict-graph artifact **was** a `notComputed` report while the submission carried `conflictEdges` anyway |

> 🔴 **1.5 is the one that turned out to matter most, and it was found by accident.** On a real job
> the artifact that should have produced the conflict edges says, in its own words, that it could not
> compute them — and the submission declares the edges regardless. Attribution alone would happily
> stamp that. **An artifact must be the right KIND, and must not itself be a report of failure.**

---

### Phase 2 — Make the loop ANSWER · **P0** · ✅ **DONE — CONFIRMED ON THE RIG 2026-08-22**

> **The wave ran on the controller and all three vectors settled.**
>
> | vector | slot / index | verdict | settling |
> |---|---|---|---|
> | vector A | 0 / **0** | **Pass** — 5/5 assertions held | **Settled** |
> | vector B | 0 / **1** | **Pass** — 4/4 held | **Settled** |
> | vector C | 0 / **2** | **Pass** — 7/7 held | **Settled** |
>
> **3 of 3 conclusive.** Indices 0 and 1 are the ones that were `Unsettled` *by construction* before
> the fix — only a slot's last index could establish settling. This is the confirmation 2.2 asked for.
>
> **It took the vessel lane to get here.** Every earlier attempt used the valve lane, and the rig runs
> the vessel harness (§ below). The run also exercised the program manifest on hardware: 8 objects,
> stamp `16#9F25F5DA`, matching the device — so this run, unlike the one that started all this, is
> reproducible.

🔴 **THIS PHASE WAS WRITTEN ON A FALSE PREMISE AND THE PREMISE CAME FROM THIS PROJECT'S OWN NOTES.**
It said *"`Harness.Loop` is built and has never run end to end"*, taken from `CLAUDE.md`'s status
table. **The loop had already run end to end twice** — 2026-08-18 and 2026-08-20, the latter reading
`"outcome": "Ran"` with three result packages and **one genuine `Pass`**. The readiness page even
carries the warning *"check the artifacts before relying on any negative claim in it"*, immediately
above the claim that was trusted instead. **A stale negative was quoted onward into a design doc and
a work plan.**

**What was actually broken was sharper.** Of three vectors, two came back `Unsettled` — and *every
assertion held* (1/1, 1/1, 4/4), stimulus `Confirmed` throughout. The block was fine:

> Settling was decided during **result-package building, after the whole wave**, by re-reading the
> device. By then the next index's inert phase had moved the program on, so `LoopRun.Settling` had to
> refuse any index that was not the slot's last — correctly, *given where it was called*.
> **Only the last vector on a slot could ever settle. Two thirds of that wave were unanswerable by
> construction.**

- 2.1 ✅ The dwell now happens **at each index's own close**, inside `WaveRun`, before the caller
  advances. `SettlingEveryIndexTests` asserts three vectors on one slot all settle — and **fails
  against the old code**, verified by temporarily disabling the dwell (3 of its 5 tests went red, and
  the 2 that stayed green were the two that should be unaffected).
- 2.2 ✅ **DONE 2026-08-22** — see the result table above. The history below is kept because the
  route to it is the useful part: three separate blockers, each of which looked like the last one.
  - ✅ **Gate 0c cleared.** The reachable-state closure was regenerated from its producer — scoped to
    the two FBs the submission's storage references, **exit 0, nothing withheld** (the first attempts
    withheld closures by name because the corpus was incomplete, and a partially-withheld report is
    correctly refused). The submission then **derived all five fields** and the gate returned
    **ADMISSIBLE**. This was the consequence Phase 1 predicted, and repairing the artifact was the
    intended remedy — it worked.
  - 🔴 **The wave still cannot run, and the reason is a gap in the artifact trail.** The verifying
    gateway refuses: the device's build stamp does not match the staged one. The stamp is a hash over
    the `--program` set, **two different sets give two different stamps** (confirmed), and **no
    recorded artifact says which set the successful 2026-08-20 run used** — the result package keeps
    the outcome and the manifest is computed but never serialised. *A run that cannot be reproduced
    cannot be re-verified.* The device's build has been unchanged since 2026-08-17, and the block IR
    is unchanged since then too, so nothing is wrong on the rig — the bookkeeping is missing.
  - 🔨 **A redeploy would resolve it** (pre-authorised for this rig, and it would push the same
    unchanged IR) **but was not attempted: Portal was busy** — two active sessions on other projects
    plus two Openness-invisible processes, the stale-pileup state associated with connect failures.
    A download is the one operation that must not be interrupted.
  - ✅ **Fixed for every future run: the result package now records the program manifest** — every
    object hashed, with a per-object content hash, the self-referential exclusions by name, and the
    stamp they produced, all emitted from the *same* derivation that computes the stamp. The
    per-object hash is the load-bearing part: it distinguishes a **different set** from a **changed
    file**, which were the two live explanations for the mismatch and could not be told apart.
    **It does not help retroactively** — the 2026-08-20 run predates it.
  - 🔎 **A bounded search for the missing set was run and failed, and the bound is stated:** eight
    combinations (two bindings × four program sets), computed **offline** via `--generate-only`,
    which constructs no gateway and reads no host. **None reproduced the device's stamp.** One
    binding produced an identical stamp for all four sets, so it is not discriminating. The set is
    not recoverable from the artifacts that exist.
  - 🔨 **Still blocked.** The redeploy remains the only route and Portal was still busy on re-check
    ~20 minutes later — unchanged: two active sessions on other projects, two Openness-invisible
    processes.
- 2.3 ✅ **Partly done, and one measurement contradicts the budget.** From the 2026-08-22 run:
  - **download `[M]` 85.4 s** (23:38:48.33 → 23:40:13.72), inside the 34–92 s already recorded.
  - 🔴 **wave `[M]`: 3 indices, 6,185 round trips.** At the measured RTT that is **~8 minutes for 3
    vectors — roughly 2.7 min per vector**, against §4.2's estimate of *"~1 min"* for the whole wave.
    **The budget line is wrong by about 8×** and §4.2 now says so.
  - ⚠️ **Not attributed to the settling dwell.** The dwell does add a per-index cost, and an earlier
    3-vector wave on the *other* lane cost 1,939 round trips — but that is a different lane with
    different vectors, so the comparison does not carry. *Measured, not explained.*
  - **Still `[E]`: import, compile and sanity.** The deploy script did not time its steps — a gap
    worth closing before the next deploy, since those are three of the budget's lines.

- 2.4 ✅ **The rig's own state, recorded because it changed twice today:** `21D74D35` (pre-existing,
  unreproducible) → `65BB248D` (my valve deploy, which degraded observation) → `357CBC4B` (restore,
  matching the project) → `9F25F5DA` (the vessel harness, and the first build whose inputs are recorded
  in the result package) → `622F3EB7` (**time-compressed at ×4**, and the first build whose stamp covers
  the parameter DB — nine objects, not eight) → `2D379A9E` (W2's phase signals; **rejected on
  measurement, the wave could not start**) → back to **`622F3EB7`**, restored and re-confirmed
  2026-08-22: three vectors, all `Pass`, all `Settled`, 3 of 3 conclusive, 134 s.

> ⚠️ **A dependency worth naming: Phase 1 now gates Phase 2's own verification.** Nothing is wrong
> with either, but the ordering means no live wave can run until the job's artifacts support the
> fields its submissions declare. That is the intended effect of the gate and the real cost of it,
> and both belong in the same sentence.

⚠️ **One misreading recorded so nobody re-derives it:** the low `framesRead` counts (1, 12, 14) in
those packages are **not** a slow poll. `ObservationSeries` retains **distinct frames — frames where
the value CHANGED** — not poll rounds, so a stable signal legitimately yields one. `PollsObserved` is
the denominator.

---

### Phase 3 — The gate queue · **P1** · ✅ **DELIVERED AND RUN ON THE RIG 2026-08-22**

> **Built, and executed against Portal and a controller.** `converter lease` is a real lock, raced by
> two processes (`9b4a863`, `e7b2af9`); `harness-batch` plans and runs N lanes off one deployment
> (`2cc474b`, `c583aa0`). **First live end-to-end run: one lane, leases → generate → convert →
> import-all → compile-all → sanity-check → download → wave, 111 s, 3 of 3 vectors PASS**, device
> stamp moved to match the staged build (`ed27465`). **Then two lanes off one deployment** — the
> second lane cost about 18 s on top of the first, *"the whole thesis of batching, and it is now a
> measurement rather than an argument"* (`d289a27`).
>
> ⚠️ **This heading carried 🔨 *"specified, not built"* until 2026-08-23**, while §0 of this same
> document listed Phase 3 as delivered. Four defects were found by that first live run and none was
> findable otherwise (`ed27465`) — a status marker that says a delivered phase is unbuilt discourages
> exactly the reading that would have found them.

Tool-owned, batching, replacing the hand-edited claim board — which had **already been raced
once**. Download granularity is device-level (34–92 s measured), so batching is the whole reason
§4.2's batched column beats the serial one.

---

### Phase 4 — ~~The workbench spine~~ → **the lane, generated** · ✅ **DELIVERED 2026-08-23** · *redefined the same day*

> **`802327f`..`dd274fe`** — W1 four silent omissions (`802327f`) · W2 `converter diff` matches
> networks on content (`2c5eab9`) · W3 `SlotFcGenerator` + the emitted lane manifest (`d3d5ab1`) ·
> W4 `StimShellGenerator` · W5 reachability parity against the converter (`f126ffc`) · W6 the stale
> status lines (`dd274fe`).
>
> ⚠️ **This heading read *"in progress"* until 2026-08-23** — the same shape W6 was itself opened to
> repair: the status was written when the work started and nothing made closing it part of closing
> the work.

🔴 **THE SPINE AS WRITTEN BELOW IS NOT BEING BUILT, AND 4.4 IS STRUCK RATHER THAN DEFERRED.** The
argument, in this document's own terms:

- **4.4 violates §3.1** — *"a field that can be derived must never be authored; a hand-typed derivable
  field is only an opportunity to disagree with reality."* A `writes:` list is exactly that, and the
  producers already exist: `cross-check`'s writer graph and `reachable-state`, whose whole point is
  that *every consumer already existed and nothing computed the sets*. Adding an authored one
  re-opens the hole FI-65 #1 and D9 both closed. **Struck.**
- **4.2 is already unit-tested at N=8** (`BatchPlannerTests.Eight_lanes_that_fit_are_batched`) and
  unused at N=3. What bounds the wavefront is the mirror's declared area and the number of lanes
  that EXIST — not a dependency graph nobody has drawn. ⚠️ **This line said "576-register" until
  2026-08-23.** The area was widened **576 → 1024 and proven on the controller** that day, pinned
  from both sides exactly as 576 had been: 1022 and 1023 answer, **1024 and 1025 are refused by the
  server with a Modbus exception** — a refusal, not a silence (`99396b9`;
  `docs/notes/total-plant-run-feasibility.md:204-219`). The argument is unaffected — it is about what
  *kind* of thing bounds the wavefront — which is exactly why the number sat here unnoticed.
- **4.1 has no consumer** left once 4.2 and 4.4 go.
- **4.3 (carve-out as a partition with a residual) is genuinely valuable and genuinely missing** —
  and belongs with its producer in the assertion pipeline (`enumerate-assertions`, `signal-sweep`),
  not in a new file format. **Re-homed, not dropped.**
- And none of it moves the budget in §5's own table: the batched deploy-and-wave path is ~2–4 min per
  lane, so the remaining ~12 of the 14-min total is all `[E]`, on the AUTHORING side.

**What Phase 4 is instead:** *a lane is a thing the tool makes.* A conformance lane needs three
blocks; only the copy layer was generated. The slot FC and the 18-network stimulus shell are now
generated too (`Harness.Map/SlotFcGenerator.cs`, `Harness.Map/StimShellGenerator.cs`), a lane's
program set is **emitted as a manifest rather than typed**, and `converter diff` no longer reads an
insertion as *"n changed, 1 added"*. That last one is a precondition, not a bonus: a regenerable
shell renumbers networks, and until 2026-08-23 that made every re-render unprovable.

**Why this and not the spine:** it is the reason *"three lanes has never been run"*. A third lane
directory exists and carries only an injection binding, because a third **conformance** lane cost a
hand-built shell plus a hand-built slot FC. Three lanes is an OUTCOME of this phase, not an input to it.

---

### Phase 5 — ~~Pre-flight interpreter~~ → **the interpreter, REDIRECTED** · **P1** · ✅ **DECIDED 2026-08-23 · 🚫 DECLINED AS A BUILD**

> ⚠️ **This heading carried NO STATUS MARKER AT ALL until 2026-08-23**, in a section whose own
> vocabulary line is three paragraphs above it. An unmarked phase in a marked list reads as in-flight,
> which is the one thing Phase 5 is not.
>
> **What is decided:** the measurement the phase demanded was taken (`docs/notes/preflight-interpreter-classification.md`),
> it inverted the expected answer, and the redirect below is the ruling — **the interpreter belongs in
> the design loop, not in front of the rig.** **What is declined:** building it. Nothing is scheduled,
> and the phase is not waiting on Portal, a rig or an owner answer.
>
> ➜ **Forwarding address — the next interpreter action is NOT a build.** It is the cheap disproof this
> phase's own method demands: **attempt two or three `converter review` rule specifications over the 17
> B rows.** 8 of the 10 bucket-A rows already went that way (*found once expensively → mechanised as a
> rule*), and if the B rows go the same way the instrument is never needed. That attempt is a session,
> not a phase, and it **needs a new per-item data-boundary permission**, because a rule specification
> derived from B rows is written in the shape of a defect. Reverse this decision by producing those
> rule specifications, not by re-reading the note.

🔴 **THE PHASE IS NOT STRUCK. ITS STATED PURPOSE IS.** The measurement this phase demanded before any
build has now been taken — full record, rubric, controls and limits in
`docs/notes/preflight-interpreter-classification.md`. **93 rows classified of 115 recorded: A = 10
(11%), B = 17 (18%), C = 0, D = 56 (60%), U = 10 (11%).** Bucket B — *only an interpreter would catch
this* — is **not** small, so the "then it is not worth a second IR parser" branch does not fire.

**But the evidence splits by WHERE the interpreter is placed, and it splits hard.**

- **As a pre-filter before a rig cycle — what §4.4 and the struck plan below describe — the evidence
  is WEAK.** Of the rig corpus's **37 non-Pass verdicts** (59 verdicts across 27 result packages; the
  22 Passes are the remainder, so 37 is the whole non-Pass set, not a sample), **only 2 were defects in
  the block under test — and both are already caught by `converter review` C-410**
  (`docs/06-lad-conventions.md:515`). The other 35 were instrument, declaration, model or deployment
  problems. An interpreter sitting in front of the rig filters almost nothing, **because almost nothing
  that fails on the rig is the block.**
- **As an instrument in the DESIGN AND REVIEW loop — evaluating a block against the specification's
  assertions before any harness, binding, slot or download exists — the evidence is STRONG.** **17 of
  the 56 block-defect rows**, today caught only by fresh-context adversarial review: expensive,
  correlated with the coder that produced the block, human, and demonstrably incomplete.

**So the redirect is: build it for the design loop, not for the rig queue.** Its output there is one
verdict per assertion, not the 601 facts `cross-check` emits on this corpus — which is the second
reason the design loop is the right home.

🔴 **Three limits that travel with the number, and must not be dropped when it is quoted:**

- **C = 0 is STRUCTURAL, not evidential.** The delivered plant program has never been executed, so the
  register *cannot* contain a "only the controller would have caught this" row. **Some B rows will turn
  out to be C rows**, so 17 is an upper bound on what an interpreter catches.
- **Every B row needs a vector, and vector supply is the measured bottleneck** — 2 of 96 and 3 of 96
  assertions covered on the two blocks that have run a wave. **An interpreter with no vectors catches
  nothing.** Funding this without funding the enumeration→vector path buys an instrument nothing feeds.
- **The load-bearing element is a JUDGEMENT, and it is recorded as one.** 8 of the 10 bucket-A rows
  follow the cheap proven pattern *found once expensively → mechanised as a converter or review rule*.
  The assessment is that the 17 B rows do **not** cluster into two or three such rules — **an
  assessment, not a fact.** If they do, write the rules and do not build the interpreter. Reverse this
  by producing the rule specifications, not by re-reading the note.

**The solution-boundary blocker is CLEARED (owner ruling, 2026-08-23) — see §3.6.** `src/harness` may
take a project reference on `src/converter`, so *"a second IR parser inside the harness"* is no longer
the only route and is no longer the plan.

The original plan, struck as a pre-filter and retained for its limit clause: ~~🔨 Extend
`Harness.Skeleton/LadInterpreter.cs` to the block-under-test's IR subset (§4.4) as a seconds-long pass
before the rig cycle.~~ **Its limit is not negotiable and is restated wherever it is offered:** not a
CPU model, green there is never evidence about a 1214C, and it is a pre-filter — never a substitute for
the rig. That clause survives the redirect unchanged, and matters *more* in the design loop, where
there is no rig immediately downstream to contradict a wrong green.

---

### Phase 6 — ~~Element-table widening~~ → **the area, DERIVED** · **P1** · ✅ **DELIVERED AND RUN ON A CONTROLLER 2026-08-23** · *re-scoped before it was built*

> ⚠️ **THIS HEADING READ 🔨 *"Element-table widening · P2"* UNTIL 2026-08-23 — on the evening of the
> day the phase it names was re-scoped, built and run on hardware.** It was the worst line in the
> document: the only marker still describing work that had been argued down *and* replaced *and*
> proven on a controller, in a list a new session reads to choose what to do next.
>
> **Delivered.** Y0–Y3 plus two unplanned items of the same subject — the reservation guard was inert
> on three of the four paths that build a map, and the rule it enforces had two derivations:
> `8bf2716` · `ea362c6` · `40dc0d9` · `466185a` · `66a0ab8` · `777fac0` · `182b3f9`, closed in
> `7dbac3c`. Follow-ups the same day: `818ba02` (the slack floor becomes mechanical) · `c53858e` ·
> `6937df0` · `0d0ebac` (the derived width meets the controller; the third leg becomes a step) ·
> `dc8308d`.
>
> **On the rig** (`ce2163b`; measurements at `docs/notes/total-plant-run-feasibility.md:235-251`):
> both lanes **3 of 3 PASS**, build stamp **`16#B85BE93C` → `16#95D8731D`** read back off the
> controller and matching both result packages, the area still exactly **1024** pinned from both
> sides, **stamp coverage 15 of 15, no gaps**.
>
> 🔴 **THE VALUE OF THAT RUN WAS NOT THE GREEN.** Setting it up found that `Main` calls the virtual
> panel's FC and **no lane declared it**, so the deployed program contained a block the tooling did
> not know about and **every build stamp before this one hashed a program short of an object the
> controller runs** — Y3's own stated residual (*"an object executing on the device that appears in no
> corpus"*), occupied on the very first real run. Reachability 6 of 6 → **7 of 7**; neighbour corpus
> 0 tag tables → **1 tag table, 7 blocks**.

**What Phase 6 became:** *the mirror no longer takes anybody's word for where it lives.* `served-area`
reads the served width off the block that serves it — both homes or neither; `neighbours` derives who
else is in the area **and who declares them**; the build stamp says **n of m** and names what it
skipped. All three print a denominator on every run including the zero case, and all three state in
their own output what they cannot see.

🔴 **Two limits that survive the phase and must not be dropped when it is quoted:** every check here
reads the **staged corpus, never the CPU** — the committed block says 37 while the rig runs 1024, and
this toolchain passes that pair by construction — and the **foreign-occupant fixture is invented**,
since all 26 real `%M` claims in the reference area are the mirror's own.

**The element table — ON DEMAND, NOT A PHASE.** *(Re-label applied 2026-08-23; it was recommended when
Phase 6 was re-scoped and then left unapplied for the length of the phase it was replaced by, which is
how the stale marker above survived.)* `Real`, `DInt`, `Word` are **one enum member, one element-table
row, one copy shape** each. `MirrorValueType` has exactly four members —
`src/harness/Harness.Map/CopyLayer.cs:18-52` — so an unsupported type is **inexpressible rather than
mishandled**: it surfaces as an unparseable binding, which is the safe failure, and the type system is
doing the work (§3.5). It is **not numbered work and not deferred work**: it is an hour, the day a
real block needs a type, arriving with that block's actual type in hand. Building it speculatively
would answer open owner question **Q2** (§7) by fiat on its cheap half and leave the expensive half —
UDT members, array elements — exactly where it is. **It has no consumer to be wrong about.**

---

### Phase 7 — ~~The HMI oracle~~ · ✅ **CLOSED 2026-08-23 — Q1 IS ANSWERED AND THE ANSWER IS BUILT**

> **This phase never had a size, a deliverable or a line in §4.2's budget table** — the only phase in
> §5 with none of the three — and its premise was overtaken **six days before it was written.**
>
> **Q1 asked what the HMI oracle is, and listed read-back comparison after import first. That is the
> one that exists.** `hmi-cli compare` — file-vs-file SimaticML comparison, `converter compare`
> re-aimed at screens — ran on a real round trip: **13 objects, 0 differences**, with same-file-twice
> refused at exit 2 (`hmi/wave-3-results.md:11`, `:102`; the full path is *author → flatten → check →
> emit → import 4.8 s → compile 0 errors → export → compare*). Q1's runner-up, the coherence check,
> was **built and graded**: TIA's import catches malformed documents and not incoherent ones, and the
> gate that closes that hole is *"a gate nobody has seen fire is not a gate"* — it is tested against
> the exact document that crashed the Portal process. Q1's third candidate, a person looking at a
> screenshot, is what the geometry read-back replaces.
>
> **And the non-goal the phase was written under is gone.** `docs/adr/adr-0007-hmi-engineering-scope.md:3-5`
> — **ACCEPTED 2026-08-17** by the owner; HMI engineering left `docs/10-non-goals.md`'s "Not now" list
> and became active work with a plan of its own (`hmi/PLAN.md`), and three waves have run.

➜ **Forwarding address: §5 does not own HMI and should stop holding a phase-shaped hole for it.** The
programme is `hmi/PLAN.md`, its record is `hmi/wave-1-results.md` … `wave-3-results.md`, and the agent
that owns screen content is `hmi-designer`. §7's Q1 below is answered and marked so.

⚠️ **CLOSING THIS PHASE DOES NOT CLOSE THE `hmi/` PROGRAMME, AND THE DIFFERENCE MATTERS.** Phase 7 was
one question about an oracle. The programme is screen authoring for **Classic Basic**, and its own
"not built" list is long and current: T8 preflight, T9 unwired-check, **9 specified-but-unchecked
convention rules** — including **H-107**, the colour half of a physical-safety rule whose size half
*is* checked — the regeneration guard the placeholder workflow depends on, and an `hmi-designer` agent
that is **written and never dispatched** (`hmi/wave-3-results.md:105-114`). Doc 17's own denominator
was reconciled in the same wave: **26 rules claimed, 14 actually checked.**

🔴 **AND A CORRECTION IN THE SAME DIRECTION AS EVERYTHING ELSE IN THIS PASS.** Until 2026-08-23
`hmi/README.md` flagged the programme's **keystone read** — *does a Basic panel export as SimaticML at
all* — as **untried**, and this phase was closed against that belief. **It was tried on 2026-08-17 and
came back positive**: one screen → 224 KB of SimaticML, root `<Hmi.Screen.Screen>`, layers and groups
present in the file, absolute geometry on every item (`hmi/wave-1-results.md:21-52`). The same
paragraph called the programme's write target open; write access was granted the same day
(`docs/13-data-boundary.md:375`, `:399`). **Two settled facts sat marked "not yet true" for six days
in the README a reader meets first** — the identical failure this document keeps recording about
itself, in a folder it does not own. Both corrected there.

---

### Phase 8 — Multi-agent contention · **P2** · ✅ **BUILT** · ⚠️ **NEVER CONTENDED BY TWO AGENTS**

> ⚠️ **This heading carried 🔨 — *"specified, not built"* — above a sentence that says the parts "are
> built".** One line contradicting the next, in the vocabulary the section defines. The marker was
> wrong; the sentence was right.

The claims registry (`src/converter/Converter/Claims/` — `ClaimStore`, `ClaimValidator`,
`ReservedBand`), wave-set admission (`src/wave-control/WaveControl/AdmissionController.cs`), slot
colouring and the escalation ladder (`EscalationLadder.cs`) are all built. **What has never happened
is two agents actually contending.**

⚠️ **And one clause of the original entry is overtaken: the lease *was* raced.** `converter lease` is a
real lock and **two processes ran the race** — `9b4a863` (the lock and the IL walk that decodes rather
than guesses), `e7b2af9` (*"a CLI, a human gate, and a race that two processes actually run"*). That
is Phase 3's delivery, not Phase 8's, and it narrows what is left here rather than closing it: the
lock is exercised, the **multi-agent admission and escalation path above it** is not.

🔴 **The residual is narrower than "run it with two agents", and it is a contract question, not a
build.** `harness-batch run` self-supplies its own pid and is honest about doing so, because it spans
the lease (`Harness.Batch/BatchCli.cs`, the `holderPid` default and the comment above it — at HEAD
`:438-451`); the gap bites the **agent-across-shells** case. But *what makes two agents different* has
no definition anywhere in this repo: D6 turns on vector author ≠ block author, and the code compares
two strings with `StringComparison.Ordinal`. The question is written down at
`.claude/skills/design-for-testability/SKILL.md` §"agent identity is undefined" — ***"Ordinal equality
on an unspecified string is a gate that is passed by typing a different string"*** — and until it has
an answer, "two agents contending" has no definition to test against. **A contention run is worth
scheduling anyway; what it cannot produce is evidence that two *agents* contended rather than two
processes.**

⚠️ **CORRECTED 2026-08-24, TWICE IN ONE SENTENCE.**
- **The citation was `BatchCli.cs:188-196`, and those lines are the `--neighbours derive needs
  --converter` refusal** — a different guard entirely. `BatchCli.cs` has not changed since `dc8308d`,
  so this is not drift. The sentence, wrong citation and all, was **lifted from
  `docs/notes/workbench-phase6-plan.md`** (a bare `BatchCli.cs:188-196`) and its filename promoted to
  a full path on the way in. **The standard for this document is "never another document," and this is
  that standard failing in the exact predicted way: a doc-to-doc citation wearing a source line's
  clothes.** The source note is corrected too, so the wrong line is no longer available to copy.
- **The open question was cited as M-19**, which is a different item: M-19
  (`docs/notes/mechanisation-backlog.md`, *"The observability gate fences the VECTOR author from the
  map, and nobody fences the BLOCK author"*) is a **small specified code change** — its own *Mechanise*
  line says *"the comparison code exists and is simply not pointed at the second party"* — and it
  blocks nothing about contention. The instruction *"Answer M-19 before scheduling a contention run"*
  was **unfollowable**: a reader obeying it closes a fencing gap and finds the contention question
  untouched. Struck.

---

### Phase 9 — ~~The GUI~~ · 🚫 **STRUCK 2026-08-23 — ITS SUBJECT NO LONGER EXISTS**

> 🔴 **It was specified as *"a viewer over Phase 4's files"*, and Phase 4's files were struck by Phase
> 4 itself, the same day, in the section directly above.** The `project.yaml` + `blocks/<name>.md`
> spine (§2.1, §4.4) is gone: 4.4's `writes:` list violates §3.1, 4.1 lost its consumer with it, and
> what Phase 4 delivered instead was *a lane is a thing the tool makes*. **There are no Phase 4 files
> to view.** This is the exact failure mode this document keeps recording — a status line left
> pointing at a deleted deliverable — arriving one heading later than the strike that caused it.

➜ **Forwarding address, because a struck phase without one is how this happened.** Nothing rests on a
GUI: no lane, no gate, no budget line in §4.2, and §5's priority rule disqualifies it on all three
questions. If a viewer is ever wanted, **its subject is not a file spine** — it is the state that
already exists and is already tool-owned: the gate queue and batch plan (Phase 3), the emitted lane
manifests (`d3d5ab1`), and the result packages. **§4.4.7's rule survives the strike and is the part
worth keeping:** state lives in files the agents read and write directly, and a GUI may only ever be a
viewer over them — never the system of record. That rule is why the spine was proposed and it is also
why the spine was struck; it does not need a phase to stay true.

---

### Phase 10 — Wave time · **P0** · ✅ **DELIVERED ON THE CONTROLLER 2026-08-22**

> **8.1 min → 2.23 min for a three-vector block; 6,185 → 1,676 round trips. 3.6× on wall clock.**
> Measured at comp 4 on the rig, not predicted. ✅ **ALL THREE VERDICTS REPRODUCE THE UNCOMPRESSED RUN
> EXACTLY — `Pass` 5/5, 4/4 and 7/7, all three `Settled`, 3 of 3 conclusive.** That equality is the
> whole claim: compression changed how long the wave took and **not what it measured**.
>
> Also measured, replacing three long-standing `[E]` estimates: **import 5 s** (est. ~40), **compile
> 24 s** (est. ~60), **sanity 3 s** (est. 4). The first two were pessimistic by ~8× and ~2.5×.
>
> 🔴 **The first compressed run FAILED, and the cause was mine rather than the block's.** `Generate`
> re-expressed the scenario coordinates by reassigning its own parameter — a local — while `Execute`
> built the wave from the request it still held. The gate reported 18 coordinates re-expressed and the
> device received all 18 unscaled, so the block ran its windows 4× fast against a full-length scenario:
> index 0 hit its backstop **with every assertion holding**, and nothing in any artifact said the block
> was fine. **Nine tests covered the computation and one the gate; none followed the value to the wire**,
> which is exactly the class this codebase warns about twice elsewhere. Fixed, with a test that reads
> the value out of the device's own memory.

### The original scope



Added 2026-08-22, after Phase 2's first measurement broke the budget: **~2.7 min per vector**, i.e.
**8.1 min for a three-vector block** against a 20-minute target, before any authoring. Numbered last
and prioritised first — priority governs order here, so nothing renumbers.

**The diagnosis, and it is not what it looks like.** The wave is **not wire-bound**. 3,052 poll rounds
× 2 round trips = 6,104 of the 6,185 — 98.7% of the traffic and ~0% of the problem. The loop polls
once per 6.4 scans and keeps pace with the plant. **The elapsed time is the controller's own: the
scenario length is a vector input, and on the dominant index the declared scenario end predicted the
measured runtime to 0.8%.** The wave takes as long as the vectors say it takes. **74% of it was one
index, and half of that index was a hedge** against an unknown warm-up phase — the vector's own note
says so.

| | before | after |
|---|---|---|
| healthy 3-vector wave | 8.1 min, 6,185 round trips | ✅ **2.23 min, 1,676 round trips** |
| wedged 3-vector wave | ~24.7 min | ✅ **~2.5 min** |

⚠️ **3.6×, not the nominal 4×, and the shortfall is understood rather than noise.** Each index carries
~1.4 s of dwells that are **literals inside the stimulus model** and cannot scale, and the inert phases
and poll overhead between indices are real time either way. **W2 (phase alignment) was built and is
NOT deployed** — its mirror signals were left out of this deploy, so none of the above depends on it.

- **W1 · Bound the backstop, stop re-running a wedged slot** — ✅ **DONE 2026-08-22.** A timed-out slot
  was re-armed at every remaining index at full backstop cost; only a failure to establish inert ever
  stopped a wave. And nothing bounded `maxDurationScans` beyond `>= 1`, while `WaveRun` takes the
  *maximum* across the tensor — so one generous number set the deadline for every slot at that index.
  Now: **`TimeoutAbandon`** (default 2 consecutive timeouts, reported through the existing NEVER
  ATTEMPTED path — abandoning is not failing), and **gate 1b**, which bounds every vector against the
  scenario it declares *and* against a flat ceiling for vectors that have no scenario clock at all.
  **Both directions refused** — a backstop *below* the scenario's own end fires on a healthy test.
  Also corrected `RoundTripsPerIndex`, which omitted the per-poll control read and so under-sized the
  backstop in the spurious-TIMED-OUT direction.
- **W2 · Phase-aligned scenario start** — ⚙️ **HARNESS HALF BUILT 2026-08-22; NOT YET EXERCISED.**
  Hold the inert phase until the block's own clock is at a known point, then commit. `PhaseCondition`
  names a result register, a trigger and a threshold; the wait sits after both inert checks and
  immediately before returning, so the commit follows as closely as the link allows. Failing to reach
  the phase inside the poll budget is `PhaseNotReached`, which stops the wave — committing anyway
  would run the test from the very phase the declaration exists to remove, and the result would be
  indistinguishable from a correct one. Declared **per slot**, since the phase is a property of the
  block and the binding is the only place the signal can be checked against what the slot publishes.
  **A trap it surfaced:** a phase signal must be *moving* during inert or there is no phase to
  observe — and the quiescence check refuses a gated register that moves. Those two declarations
  contradict each other, and left alone inert never establishes and blames the block. Now a build-time
  refusal naming both halves. **Still blocked on the mirror carrying the phase signal.**

  🔴 **And a second trap, found by reading the actual block rather than reasoning about it: the window
  whose phase the hedge is about is NOT free-running.** It is arm-gated behind a two-minute settle, so
  its elapsed time reads **0 whenever it is disarmed** — and 0 is exactly what *"the window just
  restarted"* looks like. A `Below` trigger would be satisfied on the first poll every time, reporting
  a fresh window when none is running; a `Decreases` trigger is worse, because the arm→disarm edge
  takes the clock from mid-ramp to 0, which reads as a wrap. **A phase condition may therefore declare
  a guard signal that must be set for a sample to count at all, and while the guard is low samples are
  discarded rather than merely unmatched** — keeping one would let a comparison straddle a disarm. Both
  false positives have tests, and the unguarded case is asserted alongside as the control.

  ⚠️ **Two consequences for the deploy, neither settled here.** (1) The block *does* have a genuinely
  free-running clock — a short averaging window — but it is an **independent** clock: knowing its phase
  says nothing about the gated window's, so it is a different measurement rather than a cheaper proxy.
  (2) Waiting for the gated window to arm can take up to its two-minute settle, and the inert phase's
  poll budget is ~200 rounds ≈ 30 s of wall clock. **Aligning to that window means raising the budget,
  which lengthens how long a genuinely wedged inert phase takes to detect.** That is a trade to make
  deliberately, not a default to slide.
- **W3 · Apply compression** — ⚙️ **HARNESS HALF BUILT 2026-08-22; NOT YET EXERCISED.** The planner was
  always complete — `TimeCompression.Plan` returns `CompMin`/`CompMax` across all four ceilings, gates
  10a/10b check them, `ScanBudget` re-expresses every duration at the factor — and **nothing ever
  changed a preset on the device**, so the factor governed only how long the harness would *wait*.
  `CompressedPresets` now produces the table a deploy applies, and `harness-gate compress` emits it as
  a file carrying the factor it was computed at. A literal is reported and never scaled; a preset that
  would land under the ruled 500 ms floor is **refused, not clamped** (clamping one while its
  neighbours scale changes the *ratio*, which is a different plant, not a faster one); a preset already
  under the floor at 1× is a separate outcome, because lowering the factor cures the first and never
  the second. X-D's ratio-distortion bound is enforced here too, since this table is the only place
  that knows both halves. **The block-side precondition holds** — a read confirmed 19 of 23 presets are
  DB data, not `T#` literals, and the shortest gives the recorded 4.0× ceiling — **but there is no
  runtime write path**, so the values arrive at download time, which is the same deploy W2 needs.

🔴 **Two findings from putting real numbers through W3, both of which change what the deploy is aiming
at:**

- ***THE BINDING CONSTRAINT IS NOT THE 4.0× TIMER CEILING THIS PROJECT HAS BEEN QUOTING.*** With the
  block's 17 gating presets declared, `comp_min` for the target budget comes out at **4** and every
  preset clears the 500 ms floor — the quoted answer. But the block's call tree also contains a
  **500 ms literal**, and X-D's ratio-distortion companion requires the shortest compressed behaviour
  to stay **10× the largest *participating* literal**. Declare that literal as participating and
  **compression is refused outright at any factor above 1**, because the shortest behaviour is a 2 s
  window and 10 × 500 ms = 5 s. Measured both ways through the CLI. **So the real question is not "how
  fast" but "does that literal participate" — a judgement about the block, now forced into the open by
  having to declare it.**
- ✅ **RULED BY THE OWNER 2026-08-22: the short literal does NOT participate**, so the ratio bound does
  not bind and the factor stands at **4**.
- 🔴 **Declaring the wave at 4 then refused on a ceiling nobody had looked at: the stimulus MODEL
  declares no `comp_stable`.** *"The wave runs at comp=4, so the model IS being driven at a factor, and
  nothing establishes that it behaves there. An undeclared stability ceiling is not an infinite one."*
  Correct, and it is the ceiling that had stayed invisible because at comp 1 the whole gate is vacuous.
  **Three independent constraints then converge on ~4 and that is worth noticing rather than assuming:**
  `comp_min` from the budget is **3.98**, the timer floor gives **4.0**, and the stimulus model's tick —
  data, 100 ms — divided by the measured 24.931 ms scan gives **4.01**, i.e. one tick per scan, which is
  the floor of what a scan-driven ticker can do. So 4 is simultaneously the least that fits and the most
  that any of the three allows. **`comp_stable` is a claim about the model and must be declared by
  whoever owns it, not derived here** — the arithmetic above is a proposed basis to ratify, in the same
  sense `k = 5` is ratified rather than measured.
- 🔴 **And the stimulus clock has to scale WITH the block, or nothing gets faster.** The scenario times
  are plant milliseconds played out by the model's own tick; scale the block's presets alone and the
  scenario still occupies the same real time. The tick is a *vector input*, not a block preset, so it
  sits outside the compressed-preset table — and at ×4 it lands on exactly one scan, which is why the
  model ceiling is where it is.
- 🔴 **THE BUILD STAMP DOES NOT COVER THE PARAMETERS, SO COMPRESSING THEM CHANGES THE CONTROLLER
  WITHOUT CHANGING THE STAMP.** Measured on the last wave's own manifest: the stamp is derived over
  **8 objects**, and the parameter DB is not one of them — nor is the block under test itself, which
  appears only as its instance DB. Two result packages describing materially different programs would
  therefore carry the **same** stamp, and the verifying gateway — whose entire job is to refuse a
  program that is not the one described — would not notice. **The deploy must add the parameter DB to
  the program-under-test set**, which changes the stamp and so obliges the copy layer to be regenerated
  and redeployed with it. That is the coherent sequence, not an extra step to trim.
- ⚠️ **Nothing yet ties `runtimeCompression` to evidence that the presets were actually applied.** The
  gate checks the *ceilings* admit the factor; it cannot check the device was changed. A submission
  declaring 4 against a program deployed without the scaled values would shrink the backstop 4× on a
  plant still running at 1× — the exact spurious TIMED-OUT this work exists to avoid, arrived at from
  the other side. The natural close is for `runtimeCompression`'s derivation to cite the compressed
  preset table instead of being settled by rule, but the evidence it should really cite is a read-back
  of the applied values, which needs the deploy. **Open, and named here rather than assumed away.**

✅ **THE ONE THING COMPRESSION DID MOVE, AND HOW IT CLOSED.** On the first compressed run the dominant
index's `becomesAndHolds` expectation on the observed weight went `Pass` 7/7 → **`Inconclusive` 6/7**:
the signal reached its value and then fell back. **The evaluation refused to read that as the block's
fault and named its own remedy** — *"NO ARM WINDOW WAS DECLARED for it, so nothing says whether that
fall-back is inside the phase or is the model's own required return to inert. THIS IS NOT A
DISAGREEMENT AND MUST NOT BE ACTIONED AGAINST THE BLOCK."*

Declaring `armedBy` for that signal restored `Pass` 7/7. **Why it was a real gap rather than a
workaround:** the model's recovery toward inert begins at the window's close, so those frames were
never inside the assertion's phase — at comp 1 they simply fell outside what was polled, and at comp 4
they did not. **The arm window states which frames the assertion is about, and that had been left
implicit in the timing.** Eighteen other signals in the same binding already declared it.

⚠️ **The general lesson, and it will recur:** compression exposes anything whose timing was implicit.
A rate the model runs **per SCAN** does not shrink when plant time is compressed, so it occupies a
larger share of a compressed index. **This is a reason to keep an uncompressed reference run** — the
discrepancy was only visible because there was one to compare against.

🔴 **And the stamp correctly did NOT move.** `armedBy` on a non-transient signal changes how the CLIENT
judges frames and changes nothing that executes, so `BuildStamp` deliberately excludes it — **the
re-run needed no redeploy.** Gate 0c did catch the binding's changed hash and refused until the
submission was re-derived, which is exactly the intended sequence: *"re-derive rather than re-stamp."*

🔴 **A DROPPED LINK DESTROYS A WAVE AND LEAVES NO RECORD AT ALL — found 2026-08-22, twice.** A remote
rig reached over a tunnel produced `SocketException 10060` mid-observation, and the loop exited with an
**unhandled exception**: no result package, no partial distribution, no statement that the link went
away. A run that had already established inert and was polling normally simply vanished.

**The distinction already exists one layer away and was never brought here:** `harness-mirror-read`
separates `RefusedByServer` from `TransportFailed` precisely because *a silence is not a refusal*. The
wave path has no equivalent, so **"the network died" is indistinguishable from "the tool crashed"**, and
neither is distinguishable from a run that never started. On a rig reached over a tunnel — which is how
this one is reached — that is not an edge case.

**What it should do:** end the wave with a real outcome naming the transport, keep the indices already
distributed, and mark the rest NEVER ATTEMPTED with the link as the stated reason. Contained, and it is
the same "empty is not clean" shape as the rest of this phase.

**Investigated and rejected — recorded so they are not re-proposed:**

| candidate | why not |
|---|---|
| merge the control read into the result read | the map is `[control][vector][result]` and one FC03 spans 125 registers; it does not fit at this width, and it would enlarge the **unverified** read from 6 registers to the whole map before the version check fires (DB-6) |
| pace the poll loop | worth **1–2%**. The recorder already discards a frame equal to its predecessor, and the loop already runs at 6.4 scans against an 8.06-scan observability floor |
| run one block's vectors as concurrent slots | **slower, not faster.** Reachable-state closures are computed per FB **type** and the instance root is discarded, so two instances of one block get *identical* closures, conflict, and land in **separate wave sets** — an extra wave. Making them disjoint means reversing a deliberate fail-safe |

---

**Rolled-up ordering, as it actually ran:** 1 → 2 → **10** → 3 → 4 → 5 (decided, not built) → 6.
Phases 1 and 2 were independent and could run in either order; everything from 3 onward assumed 2 had
happened; 10 arrived after 2 and outranked the rest on priority.

---

## 5z — 🔴 THE LIST IS FINISHED. WHAT IS BINDING IS NOT ON IT.

**Read this before picking a phase, because there is no phase left to pick.** Six are delivered, one
is decided-and-declined, one is closed, one is struck, and Phase 8's residual is the **agent-identity
contract question** rather than a build. **A session that comes here looking for "the next phase" and
finds one has misread a marker.**

**Apply §5's own priority rule to what remains and it disqualifies more instrument-building.** The
rule is: *does something else rest on it · how much does it move the 20-minute budget · what does it
cost to get wrong.* Two facts settle it:

1. 🔴 **THE INSTRUMENT IS FINE AND ALMOST NOBODY IS FEEDING IT.** Coverage against the spec-derived
   assertion enumeration stands at **2 and 3 distinct assertions on the two blocks that have run a
   wave** (`docs/notes/preflight-interpreter-classification.md` §7, *"Vector supply is the measured
   bottleneck"* — at HEAD `:144-146`), and it has **not moved
   across any rig event since 2026-08-18** — 08-18, 08-20, the 08-23 batch, and the 08-23 Phase 6 run
   cite the same assertions. What changed over those runs is the *verdict quality on the same
   assertions* (Unsettled / Inconclusive / Stale / NotDeployed → Pass), not how many assertions were
   asserted. Every finding in that window is a harness, deployment or declaration defect; **zero are
   plant defects.** ⚠️ **And the recount found something no register held at the time: two vectors on
   one lane cite the SAME assertion**, so that lane's vector count went 2 → 3 and its coverage did not
   move at all. *(A producer that names it now exists — see "What that leaves" below.)*
   ⚠️ **The two denominators on record disagree by one, and neither is being quietly picked.** The
   note cited above says **96 and 96**; recomputing from the enumeration projections gives **97 and
   96**. **The two are known to measure DIFFERENT ARTIFACTS, and the reconciliation is recorded
   outside this repo** — so this is not a transcription error to be corrected here, and a session that
   silently adopts either figure has picked an artifact without knowing it. The numerators are
   unaffected and so is every conclusion in this section. ➜ **A fraction quoted onward must carry the
   source it came from.**
2. 🔴 **THE THIRD-PARTY ENUMERATOR EXISTS, IS GATED, AND HAS BEEN RUN AGAINST EXACTLY ONE BLOCK.**
   The constraint is not *constitute a party*. It is *the party has been run once.*

   - **The agent, the skill and a worked artifact all exist.** `gen/test-project001/hopper-blockage-alarm/assertion-enumeration.yaml`
     is issue **5** — 13 → 19 → 23 → 25 → **27 assertions**, re-decomposed from the amended register
     rather than from a delta — and its own header states the property in the required words:
     *"Produced from the specification only, before any vector exists, by a third party to the block's
     author and the vector's author."* Its `enumerator:` field reads `assertion-enumerator`.
   - **Code enforces the independence.** `SubmissionGate.EnumeratorIndependence`
     (`src/harness/Harness.Results/SubmissionGate.cs`, wired in the gate list as **`3d enumerator
     independence`**) refuses when the enumerator equals the block author, refuses when it equals any
     vector author, and returns **NOT CHECKED, never a pass**, when unrecorded — *"unknown is not
     independent"*, and an unattributed enumerator on **any** enumeration in the set fails the whole
     gate, because a partially-attributed set is not an attributed one.
   - **It has been exercised for real.** `conformance-vectors-b.json` records
     `"enumerator": "assertion-enumerator"` against `"author": "vector-author-b-5.2"`, and
     `conformance-vectors-b.md`'s gate table lists **3d enumerator independence** among the 16 gates
     *CHECKED and passed*.
   - ➜ **`find gen -name 'assertion-enumeration*'` returns ONE file.** One block in the whole corpus
     has a third-party denominator. **That, and not the absence of a party, is the binding constraint.**

   ⚠️ **THE STRUCTURAL LIMIT THAT SURVIVES, AND IT MUST NOT BE LOST IN THIS CORRECTION.** Gate 3d's
   independence is a **normalised string comparison over an identity nobody has defined.** The
   question is recorded at `.claude/skills/design-for-testability/SKILL.md` §"agent identity is
   undefined": *"D6 turns on vector author ≠ block author, and the code compares two strings with
   `StringComparison.Ordinal`. What makes two agents different — session, model, worktree?* ***Ordinal
   equality on an unspecified string is a gate that is passed by typing a different string.***" The
   same page's enumeration item says the same thing one level down: *"nothing binds that block to an
   enumeration produced by a third party, beyond the `enumerator` identity string gate 3d compares."*
   **So dispatching the enumerator proves that a differently-named party produced the denominator. It
   does not prove a differently-*constituted* one did.** That is a real ceiling on what the
   recommendation below can establish, and it is the same open question Phase 8's contention residual
   turns on — one question, two consumers.

   ⚠️ **THIS ITEM HAS NOW BEEN WRONG TWICE, IN THE SAME DIRECTION, IN TWO DAYS — SEE THE TWO FINDINGS
   BELOW.** It read *"the enumeration has no producer"* on 2026-08-23; that was corrected to *"no
   THIRD-PARTY producer — a missing party, not a missing tool ... no code closes that"* the same
   evening, and **that was false too, against an artifact and a gate both already in the repo.**

### 🔴 The finding that corrected this section, and it is a defect class worth naming once

**A CAVEAT THAT CANNOT OBSERVE WHAT IT DESCRIBES IS A CONSTANT, AND A CONSTANT ON EVERY RESULT
PACKAGE IS AN ASSERTION NOBODY RE-CHECKS.**

- **The check it denied has been running since 2026-08-13.** Gate **`3e assertion form authority`** —
  `SubmissionGate.AssertionFormAuthority` (`src/harness/Harness.Results/SubmissionGate.cs`),
  shipped in **`b44677b`** — resolves each citation to its enumeration, reads
  `enumeration.FormOf(assertionId)`, and refuses **four** ways: no form for that ID (*"a partially-formed
  enumeration is not a permissive one"*), form `Unstated` in the enumeration, form dropped by the
  vector, and **declared ≠ enumerated**. Gate 5 likewise takes the form from the enumeration rather
  than the vector (same file, the `?? v.Form` fallback in the gate-5 body). **It is properly
  conditional:** an enumeration carrying no forms returns
  **NOT CHECKED, never a pass**, and gate 5's `?? v.Form` fallback is *reported* rather than silent —
  so where the projections carry forms the gate has authority, and where they do not it says so.
- **The caveat could not know any of that, by construction.** `LoopRun.Caveats(LoopRequest)` is built
  **from the request, before any gate runs** — and `F-3-authority` was a **string literal** asserting
  *"nothing checks it against the enumeration, because the enumeration still has no producer."*
- 🔴 **THAT IS WHY IT WAS BYTE-IDENTICAL ON EVERY PACKAGE FROM 08-18 TO 08-23 — NOT BECAUSE THE HOLE
  STAYED OPEN, BUT BECAUSE THE TEXT WAS A CONSTANT.** A caveat is *what the reader is told the result
  does not establish*, so this one taught every reader to discount an enforcement that was running. It
  was quoted onward into a work plan and then into the first draft of this section, **where it became
  the stated rationale for a programme of work.**
- **This is the "stated, never narrowed" class eating its own plan** — the same shape as §3.2's status
  cell and §0's header, one layer up: not a stale *status*, but a stale *disclaimer*, which is worse,
  because a disclaimer is read as the honest part.

➜ **The rule this earns: a caveat computed before the thing it describes may only report what is
knowable at that point.** *"Not compared yet"* is true and useful; *"nothing checks this"* is a claim
about the system that the caveat is in no position to make.

### 🔴 The finding that corrected the correction — 2026-08-24, and it is the SAME defect class

**THE PARAGRAPH DIRECTLY ABOVE RETRACTED A CLAIM ABOUT SYSTEM STATE MADE WITHOUT OBSERVING THE SYSTEM.
THE PARAGRAPH DIRECTLY BELOW IT THEN MADE ANOTHER ONE — ONE PARAGRAPH LATER, IN THE SAME EDIT, AGAINST
A GATE IN THE SAME SOURCE FILE IT HAD JUST FINISHED RE-CHECKING.** That is the uncomfortable part and
it is stated plainly rather than absorbed into a tidier sentence.

- **What it claimed:** *"the enumeration has no THIRD-PARTY PRODUCER — a missing party, not a missing
  tool ... No code closes that."*
- **What was on disk when it was written:** a third-party enumeration at issue 5 with 27 assertions
  (`gen/test-project001/hopper-blockage-alarm/assertion-enumeration.yaml`), and
  `SubmissionGate.EnumeratorIndependence` — gate **3d** — refusing block-author and vector-author
  collisions and returning NOT CHECKED when unrecorded. Both older than the sentence. Item 2 above
  carries the detail.
- **How it happened, and it is not a new mechanism:** the previous item's rationale was retired but
  its *conclusion* was kept and re-argued one level up. Retiring a premise and keeping the verdict is
  how a diagnosis survives its own disproof.

- ⚠️ **AND THE SAME EDIT MISCITED THE SOURCES IT SAID IT HAD READ.** The v3.4 entry states *"Verified
  by reading both sources, not taken from the report that raised it"*, and cites
  `SubmissionGate.cs:1455` and `:1603`. **Those numbers were BORN WRONG, not drifted.** They were
  exact at `3926b65`; `82af95f` then inserted ~25 lines above them, moving the two symbols to **1480**
  (`AssertionFormAuthority`) and **1628** (the `?? v.Form` fallback); `4102a48` wrote the citations
  *after* `82af95f` had landed, off a superseded copy. The verification was real — the *gates* are
  exactly as described — but it was performed against a stale read, and the entry's "verified" claim
  is corrected here rather than deleted.

➜ **The rule this earns, and it is a citation-form rule: cite the SYMBOL, and the file. A line number
is a coordinate in a document that moves; a symbol name is the thing itself.** Every line-number
citation into `src/` in this section has now been wrong at least once, and not one of them was wrong
about *what the code does* — the fact survived, only the coordinate rotted. `SubmissionGate.cs` alone
moved twice in two days. **Line numbers are kept only where they are pinned with an "at HEAD", and are
never the sole locator.** Applied throughout this section; not retrofitted to sections this pass did
not touch, which is a known inconsistency and is recorded as one.

➜ 🔴 **AND THE SAME ARGUMENT, ONE STEP FURTHER ON, FOR A COUNT: THE DURABLE FORM OF A NUMBER IS A
FRESH MEASUREMENT, NOT A BETTER POINTER.** This pass proved it against itself. A suite figure here was
sourced to the commit that measured it, hedged, and labelled *as of `7dbac3c`, not as of HEAD* —
textbook attribution — **and it was still wrong, because twelve tests had been added since
(`c53858e`).** A better pointer repairs a coordinate that rotted; it cannot repair a value that moved.
**A correctly-attributed stale figure is still a stale figure, and it is worse than an obviously
unsourced one, because the attribution is what persuades the reader not to check.** So: where a number
can be measured, measure it and stamp the measurement with its date; cite a commit only for a figure
you cannot re-run. Every suite count in this document is now a measurement at HEAD on 2026-08-24 or is
explicitly pinned to the commit it describes as *history*, never as *current*.

### What that leaves

**Both halves are repaired and shipped — `82af95f`, 7 files, 896 insertions, suite 2,698 passing
across 16 assemblies, 0 failures, 0 warnings *as measured at `82af95f`* (up from 2,673 by exactly the
25 new tests). At HEAD the harness suite is **2,724 passing, 16 assemblies, 0 failures, 0 warnings —
measured 2026-08-24, not quoted from a commit message**; `7bf400b` added the 26 and updated no
document.
`F-3-authority` now carries gate 3e's actual finding — closed for this submission, refused with the
gate's detail, or not-compared with the reason — and **keeps its ID**, so a search for F-3 still lands
on it and the good news is visible to the search written to find the bad. A numerator producer now
exists: `Harness.Results/AssertionCoverage.cs`, counting distinct cited assertions over the third
party's enumeration, **per subject and never summed**, with `NotComputed` as the default so an unset
coverage cannot print `0 of 0` and read as a measurement. It names the multiply-cited assertion
together with both vectors that cite it — the fact that went unnoticed for five days.

➜ 🔴 **SO THE NEXT THING IS A DISPATCH, NOT A DELIBERATION: RUN THE ENUMERATOR ON A SECOND BLOCK.**
The party is constituted, the skill is written, the gate that grades its output runs, and there is a
five-issue worked precedent to copy. What has never happened is a **second** run.

> **The action, in full, so no session has to reconstruct it.** Dispatch the **`assertion-enumerator`**
> agent with the **`enumerate-assertions`** skill against a block in `gen/test-project001/` that has no
> `assertion-enumeration.yaml` — from the **specification only**, never the implementation, and never
> the agent that authored the block or its vectors. Grade the output with gate **3d enumerator
> independence** and the enumeration-shaped gates beside it. Copy the shape of
> `hopper-blockage-alarm/assertion-enumeration.yaml`: no `id:` key on issue, IDs recomputed fresh,
> the re-hash set reported, the UNCLASSIFIED residual stated.
>
> **The falsifiable result:** does a second denominator, and vectors written against it, move coverage
> off the frozen 2-and-3? If it does, the constraint was supply and the path is to repeat it. If it
> does not, the constraint is somewhere this section has not found, and *that* is worth more than
> another instrument. **Either outcome is an answer; the present state is not.**

⚠️ **What the dispatch CANNOT establish, stated with it.** Gate 3d compares normalised identity
strings, and *what makes two agents different* is undefined
(`.claude/skills/design-for-testability/SKILL.md` §"agent identity is undefined"). **A gate passed by
typing a different string does not prove independence; it proves non-collision of names.** So a second
enumeration raises coverage and exercises the party — it does not close D6 at the denominator. Closing
that needs the identity contract, which is one question serving two consumers, this and Phase 8's
contention residual, and **it is not a blocker on the dispatch: run it, and record which agent, which
session and which model produced it, so the answer has data to be about.**

⚠️ **The superseded recommendation, kept visible.** This section previously read *"the next thing is
the PRODUCING PARTY ... an agent-and-process question rather than a `src/` one"* and pointed at no
acceptance criterion. It is replaced, not softened: the party is not the missing thing.

⚠️ **DO NOT READ ANY OF THIS AS PROGRESS ON THE MEASUREMENT.** Coverage is unmoved. Five rig runs still
found **zero plant defects**. **C = 0 is still structural.** What changed is that two sentences in the
diagnosis were wrong; every number under them is exactly where it was.

**The figures, each carrying its source, per the rule in item 1 above.** The note at
`docs/notes/preflight-interpreter-classification.md` §7 reads **2 of 96 and 3 of 96**; recomputing from
the enumeration projections gives **2 of 97 and 3 of 96**. **The numerators agree; the denominators
disagree by one, they measure different artifacts, and the reconciliation is held outside this repo.**
➜ Quote the pair with the source attached, or quote **the numerators alone (2 and 3), which are not in
dispute.** ⚠️ **This paragraph is itself a correction:** the line here read *"Coverage is still 2 of 97
and 3 of 96"* with no source — the recomputed denominator silently preferred over the cited one, four
paragraphs after this section made carrying the source a rule in bold. **A rule stated in one paragraph
and broken in the next is the strongest available evidence that stating it is not the mechanism.**

⚠️ **The caveat against that recommendation, stated with it.** **C = 0 is structural**: the delivered
plant program has never been executed, so no defect that only a running controller could expose can
be in any corpus read here. If five rig runs found only harness defects partly because the harness is
only asking five questions, the ratio could look very different at forty. **That is an argument for
feeding the instrument. It is not an argument that the plant is clean, and nothing here should be
read as one.**

**The other live item is a rig event, not a build:** the third conformance lane. **Two is not N**, and
every claim the batching design makes is a claim about N. Its owner question is **answered** — see
`docs/notes/owner-questions.md` — and **two further gates now stand ahead of authoring it**, recorded
there. It is no longer blocked on the owner in the way this document's satellites claimed all week.

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

- ~~**Q1 — HMI.**~~ ✅ **ANSWERED — the first candidate it listed is the one that got built.**
  `hmi-cli compare` is read-back comparison after import: **13 objects, 0 differences on a real round
  trip**, same-file-twice refused at exit 2 (`hmi/wave-3-results.md:11`). A coherence gate refuses an
  incoherent document *before* it can reach TIA, tested against the exact document that crashed the
  Portal process. **And the non-goal the question was asked under is gone** — ADR-0007 **ACCEPTED
  2026-08-17** (`docs/adr/adr-0007-hmi-engineering-scope.md:3-5`). The programme, its plan and its
  waves are `hmi/`; **this document does not own them.** Phase 7 closed on this answer.
  ⚠️ **What is answered is the oracle, not the capability.** The programme's keystone read — *does a
  Basic panel export as SimaticML at all* — was also answered, positively, on 2026-08-17
  (`hmi/wave-1-results.md:21-52`), but its "not built" list is long and current: T8, T9, nine
  unchecked convention rules including H-107's colour half, the regeneration guard, and an agent never
  dispatched (`hmi/wave-3-results.md:105-114`).
- **Q2 — Element-table width. STILL OPEN, AND STILL BLOCKING NOTHING.** Which types must be observable
  for real blocks? `Real` and `DInt` look unavoidable; UDT members and array elements are a bigger
  question (§3.5.2). ➜ **Do not build ahead of the answer.** Widening is **on demand, not a phase**
  (§5 Phase 6): one enum member, one element-table row, one copy shape, the day a real block needs a
  type — with that block's actual type in hand. Answering Q2 converts "on demand" into a scoped item
  with a decided type list; building it first answers the cheap half by fiat.
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

> 🔴 **THIS LOG HAS NOW STOPPED TRACKING ITS OWN DOCUMENT THREE TIMES, AND THE THIRD TIME IS RECORDED
> IN v3.5 BELOW.** The rule it drew each time — *"closing a phase includes closing its markers, in the
> same commit"* — has been stated three times and has held zero times. **A fourth statement is not the
> fix.** ➜ **What would actually work is mechanical and cheap: a repo hook that refuses a commit
> touching `docs/18-project-workbench.md` unless the same commit's diff also adds a line under `## 8.
> Change log` and touches the `**Status (` stamp in §0.** The budget gate at `hooks/` already refuses
> commits on a content rule, so the mechanism exists and is installed
> (`python tools/check-claude-md-migration.py`, and `git config core.hooksPath hooks`). **Not built by
> this pass — proposed, and named here so the next session does not re-derive it.** It is worth noting
> what such a gate would NOT have caught: `c851ea0` was a *correct* one-line fix that simply forgot its
> log entry, which a gate catches — but a gate cannot tell a real entry from `- v3.x — misc`, so it
> buys the reminder and not the content.

- **v3.6 — 2026-08-24. 🔴 THE CORRECTION PASS COMMITTED THE DEFECT CLASS IT WAS CONVENED TO FIX, AND
  FOUR OF ITS CITATIONS WERE BORN WRONG.** An independent audit of the 2026-08-23 pass; every finding
  below was re-verified against the artifact before being acted on.
  **§5z's central recommendation was FALSE and is REPLACED.** It read *"the enumeration has no
  THIRD-PARTY PRODUCER — a missing party, not a missing tool ... No code closes that."* Both halves
  are wrong: `gen/test-project001/hopper-blockage-alarm/assertion-enumeration.yaml` is a third-party
  enumeration at issue 5 (27 assertions) whose header states the property verbatim, and
  `SubmissionGate.EnumeratorIndependence` — gate **3d enumerator independence** — has been refusing
  author collisions and returning NOT CHECKED on an unrecorded enumerator all along, recorded CHECKED
  and passed on `conformance-vectors-b`. **This is a claim about system state made without observing
  the system — the exact class §5z exists to name — recurring ONE PARAGRAPH after the retraction of
  the previous instance, in the same source file as the gate it had just re-checked.** The true
  constraint is narrower and dispatchable: **the party has been run ONCE**, on one block in the whole
  corpus (`find gen -name 'assertion-enumeration*'` → one file). §5z now names the concrete action —
  dispatch `assertion-enumerator` with `enumerate-assertions` against a block with no enumeration,
  grade it with gate 3d, and see whether coverage moves — and names the ceiling on it: gate 3d is an
  **ordinal comparison over an identity nobody has defined**
  (`.claude/skills/design-for-testability/SKILL.md` §"agent identity is undefined").
  **The diagnosis is untouched:** coverage frozen across every rig event since 2026-08-18, C = 0
  structural, every finding in the window a harness/deployment/declaration defect.
  **§0 and §5's table claimed controller exercise for Phases 1 and 4, which never had it** — Phase 1's
  body names no rig and closes on a dry run; Phase 4 ships generators and `converter diff`, and its
  one controller sentence is Phase 6's run quoted inside a Phase 4 argument. Same direction as
  `c851ea0`, and self-contradictory against §3.2's *BUILT — NEVER RUN*. The table now separates
  delivered from exercised-on-hardware. Whether Phase 4's lane manifests fed the Phase 6 run is
  **unestablished** and therefore not claimed.
  **Four line-number citations were born wrong, not drifted.** `SubmissionGate.cs:1455`/`:1603` were
  exact at `3926b65`; `82af95f` moved the symbols to 1480 and 1628; `4102a48` wrote the citations
  afterwards off a superseded copy — inside the entry that says *"verified by reading both sources."*
  And `BatchCli.cs:188-196` (§5 Phase 8) is the `--neighbours derive needs --converter` refusal; the
  real code is the `holderPid` default and its comment, **`:438-451`** at HEAD. `BatchCli.cs` has not changed since
  `dc8308d`, so that one is not drift either: **the sentence was lifted out of
  `docs/notes/workbench-phase6-plan.md` and its bare filename promoted to a full path** — the pass's
  own *"never another document"* standard failing in the predicted way. Source note fixed too.
  ➜ **Citation-form rule adopted in the sections this pass touched: cite the SYMBOL and the file; a
  line number is never the sole locator and carries an "at HEAD" when kept.**
  **Phase 8's residual pointed at the wrong backlog entry.** M-19 is *"the observability gate fences
  the VECTOR author from the map, and nobody fences the BLOCK author"* — a specified code change whose
  own *Mechanise* line says the comparison code exists and is simply not pointed at the second party.
  It blocks nothing about contention, so *"Answer M-19 before scheduling a contention run"* was an
  instruction nobody could follow. Repointed at the agent-identity question and struck.
  **§5z forbade quoting a fraction without its source and then did it** at *"Coverage is still 2 of 97
  and 3 of 96"* — the recomputed denominator silently preferred over the cited note, which says **96
  and 96**, four paragraphs after the rule was set in bold. Both quotations now carry their source.
  **The harness figure was stale in three tracked places** — `7bf400b` took it 2,698 → 2,724 and
  updated no document (`docs/18` ×2, `AITODO.md`). **All five baselines were then MEASURED at HEAD
  rather than re-sourced:** converter **1,643** · harness **2,724**, 16 assemblies, 0 failures, 0
  warnings · openness-cli **871** · golden **206** · budget script **23**, 0 failed — 2026-08-24, exit
  0 throughout. ⚠️ *The openness-cli run rebuilt DEBUG only; Debug and Release hold independent TIA
  approvals, so it says nothing about the Release binaries the agents invoke.*
  🔴 **The reason they were measured and not cited is the pass's second citation finding.** A draft of
  this entry carried golden as **194 of 194**, sourced to `docs/notes/workbench-phase6-plan.md`'s Phase
  6 header, hedged, and explicitly labelled *as of `7dbac3c`, not as of HEAD*. **It was stale anyway —
  `c53858e` had added twelve tests.** A correctly-attributed stale number is still stale, and the
  attribution is precisely what stops the reader checking. ➜ **Recorded as a rule in §5z: the durable
  form of a count is a fresh measurement, not a better pointer.**
- **v3.5 — 2026-08-24. THE ENTRY THAT SHOULD HAVE BEEN WRITTEN BY `c851ea0` AND WAS NOT — WRITTEN
  RETROSPECTIVELY BY v3.6, WHICH IS THE THIRD OCCURRENCE OF THIS EXACT FAILURE.** `c851ea0` rewrote
  §3.2's deploy-gateway status cell from **"run live — 45 objects, CPU `Running`"** to 🔴 **"BUILT —
  NEVER RUN"**: `Harness.Device` appears **zero** times in `docs/notes/test-log.tsv`, and what ran on
  2026-08-14 was the constituent binaries driven separately — `download-probe` at 00:52, `rig-read` at
  00:58, `openness-cli`'s `import-all`/`compile-all`/`sanity-check` at 02:40, *after* the download
  rather than before it, which is the reverse of the gateway's sequence. **Nothing has ever executed
  it as one program.** That is the largest correction in the batch and the only one in the
  over-claiming direction — **and it left no trace in this log, while §0's stamp went on reading
  "2026-08-23, second revision that day" for a day after.** Both fixed in v3.6.
- **v3.4 — 2026-08-23 (later the same evening). 🔴 §5z RESTED ON A FALSE PREMISE, AND THE PREMISE WAS
  A STRING CONSTANT.** v3.3 closed the ten-phase list and named the binding constraint as *"the
  enumeration has no producer"*, quoting the `F-3-authority` caveat that rides on every result package.
  **The check that caveat denies has been running since 2026-08-13.** Gate `3e assertion form
  authority` (`SubmissionGate.AssertionFormAuthority`, shipped `b44677b`) reads the form from the
  enumeration and refuses four ways, and gate 5 does the same via its `?? v.Form` fallback. The caveat
  is built from the **request**, before any gate executes, so it could never report the gate — **it
  was byte-identical on every package because it was a literal, not because the hole was open.**
  ⚠️ **CORRECTED 2026-08-24 — this entry read `SubmissionGate.cs:1455` and `:1603`, and both numbers
  were WRONG WHEN WRITTEN** (see v3.6): exact at `3926b65`, moved to **1480** and **1628** by
  `82af95f`, cited by `4102a48` afterwards off a superseded copy. The claim *"verified by reading both
  sources, not taken from the report that raised it"* is **kept and qualified rather than deleted**:
  the gates behave exactly as described, so the reading happened — but it happened against a stale
  copy, and a verification that cannot notice its source moved is a weaker claim than it sounds.
  ⚠️ **AND THIS ENTRY'S CONCLUSION WAS ITSELF FALSE.** It read *"the missing thing is the third-party
  PRODUCING PARTY, not the count and not the check"* — the party existed, gate 3d enforced it, and one
  block already had a third-party enumeration. **Retracted by v3.6.** What survives is the defect class
  it named: *a caveat that cannot observe what it
  describes is a constant, and a constant on every package is an assertion nobody re-checks.* That is
  the "stated, never narrowed" class one layer up from §3.2 and §0, and worse, because a disclaimer is
  read as the honest part. **Nothing here moves the measurement:** the numerators are still **2 and
  3**; the denominator is **96 and 96** per `docs/notes/preflight-interpreter-classification.md` §7,
  or **97 and 96** recomputed from the enumeration projections — *the pair is quoted with its source
  because they measure different artifacts and the reconciliation is held outside this repo.*
  ⚠️ **This sentence read a bare "2 of 97 and 3 of 96" until 2026-08-24** — one denominator silently
  picked, in the entry that introduced the rule against exactly that. C = 0 is still structural, five
  rig runs still found zero plant defects. **Both halves of the repair shipped in `82af95f`** — the
  caveat now reports gate 3e's actual finding while keeping its ID, and
  `Harness.Results/AssertionCoverage.cs` counts the numerator per subject; suite **2,698 passing
  across 16 assemblies, 0 failures, 0 warnings *as measured at `82af95f`* — history, not current.
  Current is 2,724, measured at HEAD 2026-08-24 (v3.6).**
- **v3.3 — 2026-08-23 (evening).** 🔴 **§5 IS NOW TRUE, AND THE REASON IT WAS NOT IS THAT NOTHING MADE
  CLOSING A MARKER PART OF CLOSING A PHASE.** v3 recorded exactly this against itself — *"a change log
  that stops tracking its own document is §3.2's failure in miniature"* — and **it recurred inside 24
  hours**: this log stopped at v3.1 with no Phase 6 entry while Phase 6 was re-scoped, delivered
  (`7dbac3c`) and **run on a controller** (`ce2163b`) the same day. Corrected in this pass, each
  against a commit, source line or measured run and never against another document:
  **Phase 6** — the worst line in the document, still reading 🔨 *"Element-table widening · P2"* — is
  now *"the area, DERIVED"*, ✅ delivered and run on hardware (stamp `16#B85BE93C` → `16#95D8731D`,
  stamp coverage 15 of 15, both lanes 3 of 3 PASS), and the element table is **re-labelled "on demand,
  not a phase"** as its own replacement plan recommended and nobody applied.
  **Phase 5** had **no marker at all**; it is ✅ decided / 🚫 declined as a build, with the next
  interpreter action named as the cheap disproof rather than a build.
  **Phase 7** is 🚫 **closed** — Track 2 below.
  **Phase 8**'s 🔨 contradicted its own next sentence (*"are built"*); it is ✅ built / never contended,
  and the clause saying the lease had never been raced is overtaken — two processes ran the race in
  Phase 3 (`9b4a863`, `e7b2af9`). Its residual is re-stated as a **contract question (M-19)**, not a
  build.
  **Phase 9** is 🚫 **struck**: it was specified as *"a viewer over Phase 4's files"* and Phase 4 struck
  that spine in the section directly above it — a status line resting on a deleted deliverable, one
  heading from the strike that deleted it. **Every strike in this pass carries a forwarding address**,
  because the absence of one is how Phase 9 got there.
  **The §0 header** understated its own body for the second time in two days and now says so.
  **New: §5z**, because a corrected list is still not a plan — it records that the list is *finished*
  and that the binding constraint is off it: coverage frozen at 2 and 3 assertions across every rig
  event since 08-18, and ~~**the enumeration has no producer**, stated on every result package by
  `src/harness/Harness.Loop/LoopRun.cs:1658-1662`~~ — 🔴 **that clause was FALSE and is corrected in
  v3.4 below; the sentence it trusted was a constant, not a finding.** **Q1 is marked answered; Q2 stays open and is
  marked as blocking nothing.** Phases 1, 2, 3, 4 and 10 were checked and needed nothing; §3.x's
  arguments, §5's priority rule, the ✅/🔨/❓/P0–P2 vocabulary and every measured number and recorded
  defect are deliberately untouched — the failure here was maintenance, not design.
- **v3.2 — 2026-08-23.** *(Number reserved and deliberately left empty: the Phase 6 delivery
  (`7dbac3c`) and its rig run (`ce2163b`) belonged here and were never written. The gap is left
  visible rather than back-filled, because it is the evidence for the finding in v3.3.)*
- **v3.1 — 2026-08-23.** **PHASE 5's DECIDING MEASUREMENT TAKEN, AND THE PHASE REDIRECTED RATHER THAN
  STRUCK.** 93 rows classified of 115 recorded — **A 10 / B 17 / C 0 / D 56 / U 10** — full record in
  `docs/notes/preflight-interpreter-classification.md`. 🔴 **The rubric turned out to be the finding:**
  the three buckets this document commissioned have no bucket for *"not a defect in the block at all"*,
  so all **56** such rows would have been forced into *only the rig would* and the study would have
  argued for more rig time on the strength of our own instruments' bugs. **2 of 37 non-Pass rig
  verdicts were block defects and both are already caught by C-410**, which kills the pre-filter
  placement §4.4 describes; **17 of 56 register rows** justify the interpreter in the **design and
  review loop** instead. Three limits recorded beside the number and not detachable from it: **C = 0 is
  structural** (the plant program has never run, so a C row could not exist); **an interpreter with no
  vectors catches nothing** (2 of 96 and 3 of 96 assertions covered on the two blocks that ran); and
  **whether the 17 mechanise into two or three converter rules is a JUDGEMENT**, which is the
  load-bearing element of the whole recommendation and is labelled as one. Also **§3.6 added**: the
  owner ruled `src/harness` MAY reference `src/converter` — **converter only**, with §5 Phase 1's
  `wave-control` file-contract precedent explicitly not generalised, and the accepted cost (FI-73's
  Release-staleness class gains a new home; the TIA `(Path, FileHash)` cycle is untouched) stated in
  the ruling rather than left to be met later.
- **v3 — 2026-08-23.** **PHASE 4 DELIVERED, AND REDEFINED FROM WHAT §5 ORIGINALLY SAID IT WAS.** The
  "workbench spine" is argued down in this document's own terms and **4.4's `writes:` list is struck**
  (§3.1 forbids a hand-authored derivable field, and `cross-check` / `reachable-state` already compute
  it); 4.3 is re-homed with its producer in the assertion pipeline. What was built instead: **a lane is
  now something the tool makes** — `SlotFcGenerator` and `StimShellGenerator` join the copy layer, a
  lane's program set is an emitted manifest rather than a typed flag, `converter diff` matches networks
  on content, and the batch compares its reachability walk against the converter's.
  **Phase 5 redefined as "Pre-flight, measured"** — the original plan is annotated **not executable as
  written** (§4.4, §5), because extending the harness's regex interpreter to real block IR means a
  second IR parser in a deliberately dependency-free solution.
  🔴 **Three status corrections, and the shape is the finding rather than any one of them:** the header
  said *"Not adopted"* with four phases delivered inside it; §3.2's table said `Harness.Loop` was
  *"built, never run"* while §5 of this same document recorded it running; and **this change log had
  stopped at v2.5 while the header, §3.2, §4.4 and §5 were all materially rewritten** — a change log
  that stops tracking its own document is §3.2's failure in miniature.
- **v1 — 2026-08-21.** Owner's design recorded; eight problems raised; capability assessment.
- **v2 — 2026-08-21.** All eight applied into the design itself. Added the ~20-minute budget (§4,
  measured lines marked) and the mechanised test stage (§3) — authored-vs-derived, `harness derive`,
  and the two places derivation must refuse. Added the finding that the copy-layer ladder is
  **already generated** and the real gap is the hand-authored submission document. Capability
  assessment compressed to §6.
- **v2.5 — 2026-08-22.** **PHASE 2 CLOSED — confirmed on the controller.** Three vectors on one slot,
  all three `Pass`, **all three `Settled`**; indices 0 and 1 are the ones that were unanswerable by
  construction before the fix. Getting there took four discoveries, each of which looked like the
  last blocker: **the rig runs the VESSEL harness** (every earlier attempt used the valve lane — the
  copy layer references 155 slot-prefixed mirror tags for the OTHER lane); **a stamp search that read the wrong number** (a
  first-match grep picked up build stamps quoted in declaration *comments* rather than the tool's own
  `BUILD STAMP` line); **`deployment` was unattributable by construction** (Phase 1 demanded a
  `LoopResult` where every real download emits a `download-probe` log — fixed, with the substring trap
  that `TRANSFERRED` is inside `NOTHINGTRANSFERRED`); and **one missing `map.storage` entry** which
  alone caused *68 of 68 signals unresolved* in the conflict graph. 2.3 measured two lines and **the
  first one broke the budget** — the wave is ~2.7 min per vector, not ~1 min per wave.
- **v2.4 — 2026-08-21.** **Phase 2 rewritten and its fix landed (simulator).** The phase as written was
  false: the loop had already run end to end twice, and one vector had genuinely `Pass`ed — a stale
  `CLAUDE.md` row quoted onward into this doc and a work plan. The real defect was that **settling
  could only be established for the LAST vector on a slot**, because the sample was taken after the
  whole wave; every earlier vector was `Unsettled` by construction while its assertions all held. The
  dwell now happens at each index's own close in `WaveRun`. 2160 tests pass, 0 warnings; the new tests
  were negative-tested by disabling the dwell. **Rig re-run still outstanding**, so the 3-of-3 claim is
  simulator-only.
- **v2.3 — 2026-08-21.** **Phase 1 closed.** All five gaps inside the derive mechanism are built and
  tested (2148 passing, 0 warnings). The deriver now RECOMPUTES rather than only citing, and gate 0c
  counts *recomputed* / *by rule* / *cited* separately so "derived" stops being one word. The live-job
  dry run earned its place: it found **four defects the unit tests did not**, every one an
  over-strict check making a false accusation against valid data — including a fully-computed
  conflict graph refused as the wrong kind. On the live job the map is now genuinely computed and
  matched, and the remaining refusals are real: two artifacts that report their own failure, and one
  precise storage gap.
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
