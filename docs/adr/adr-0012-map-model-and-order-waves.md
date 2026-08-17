# ADR-0012 — The map model: derive the block structure from the border, and build it in order-waves

- **Status:** **PROPOSED — design settled with the owner 2026-08-17; BUILD NOT SCHEDULED.** The staged
  development plan is suspended (`docs/03-development-plan.md`) and FI promotion with it, so this ADR
  records a settled *design*, not an authorised *build*. Nothing here changes the pipeline until the
  owner schedules it. One decision is deliberately left open — *the mixed-order block*, under
  **Open items** — and it must be closed before a wave scheduler is written.
- **Date:** 2026-08-17.
- **Relates to:** **ADR-0004** (the generation pipeline this replaces three stages of) ·
  **ADR-0010** (no IR the AI cannot change — same instinct, applied to structure) ·
  `docs/15-generation-pipeline.md` (rungs A–D; C and D are superseded here) ·
  `docs/evidence/PlantAutoControl-bench-autopsy.md` (the correlated-check failure this is built around) ·
  `docs/06-lad-conventions.md` C-127 / C-304 / C-308 (three rules that become one property here) ·
  `src/wave-control/`, `converter claim`/`claims` (the multi-agent machinery this would finally use) ·
  `docs/16-future-ideas.md` **FI-76** (the analysis entry) ·
  **`docs/notes/map-model-build-plan.md`** (what building it would cost, in what order — held for
  later execution, not queued work).

## Decision

**Model a PLC program as a map.** The border is the set of edge interfaces. Territories are carved
inside it. What is left over is not glue — it is a territory whose boundary is *derived* rather than
designed. Blocks are then built in **order-waves**, outward from the border, with the per-block work
parallelised across agents and the gate batched at each wave boundary.

Concretely, this replaces rung C, rung D and `gen-architecture` with **two roles** — a **scout**
before rung A and a **cartographer** after rung B — and adds a wave-based build loop after it.

## Context

### The instinct: derived beats declared

This project has been right every time it has replaced a declaration with a computation.
`converter reachable-state` computes slot disjointness by set intersection instead of accepting the
submitting agent's `--reaches`. `signal-sweep` computes an exact denominator instead of a
self-reported approximation. `drift-check` prints `COMPARED: <n>` because an invariance claim over an
empty remainder proves nothing.

Block interfaces are the largest remaining thing this project *declares*. `gen-architecture` designs
them by judgement; rung D admits interface members by reasoning. The map model makes most of that a
**set difference**: fix the border, place the territories whose shape you already know, and the
boundary of what remains is determined by what surrounds it.

### Why start at the border

The border is the only part of the program you are **forbidden to invent** (hard rule 3 — no invented
tags, addresses, hardware). Carving outward from it is "never invent reality" applied to the *shape*
of the program rather than to its contents. Every other starting point starts somewhere softer.

### The property that makes it more than a metaphor

Several rules discovered separately, at different times, for different reasons, are one property of a
tiled map — **no gaps, no overlaps**:

| Existing rule | Under the map |
|---|---|
| C-308 multi-writer | two territories overlap |
| C-304 IO boundary | only border territories touch the border |
| C-127 hardcoded instance refs in an FB | a territory reaching into another's interior instead of crossing at a named border |
| dead wiring | a border segment with nothing on the far side |
| hard rule 2 (safety) | a neighbouring country with a closed frontier — shared border, no entry |

A model that reproduces independently-found rules is usually the right model. The standing test of
whether it is a *discovery* rather than a restatement is whether it predicts a rule we do **not**
already have; that is an open question, deliberately not answered here.

## The model

### Definitions

- **Territory** — a thing with **state that survives the scan**. This is the load-bearing definition:
  it is mechanical and checkable, and it maps onto FB-with-instance-DB. Stateless computation is not
  a territory; it belongs to whoever calls it.
- **Border** — the edge interfaces. **IO, comms to devices, and control/state from the HMI**, and
  the list is deliberately open — retained memory is a border in *time* rather than space, and
  **plant mode is treated as a border**, not a territory (see *Cycles* below).
- **Border crossing** — **always an interface member. Never a global-DB reach-through.** This is the
  rule that makes the map *have* borders rather than being regions on a shared blackboard.
- **Territories may not overlap.** With this accepted, C-308 stops being a review finding and becomes
  a structural invariant checkable on the artifact, before any ladder exists.
- **Derived territory** — the logic not yet written, whose interface is implied by what surrounds it.
  Named deliberately: calling it "empty space" invites treating it as glue, and glue is what
  test-project001 was called obtuse for.

The map may be N-dimensional. The dimensionality is incidental — the interface and the map are
defined as needed.

### Order

**Order 1 = anything whose definition comes from outside the program.** Two anchors of equal rank:

- the **hardware** gives you `FC_Inputs` / `FC_Outputs`;
- the **class library** (`references/<class>/`) gives you `FB_Motor`.

Neither is derived from the other, which is why both are order 1 and why they **meet in the middle** —
the *horseshoe*. A motor FB wired to raw IO (against conventions) is still order 1; a motor FB reading
`DB_Inputs` written by `FC_Inputs` is *also* order 1, interfacing with a peer.

**Order N+1 = 1 + the highest order of anything whose interface you must know in order to write
yourself.**

Two consequences that are easy to get wrong:

1. **Commands flowing down from a higher order are not build dependencies.** You write an order-2
   block by exposing a command input; who eventually drives it is irrelevant to writing and testing
   it. That is what an interface is for. So the naive software-dependency intuition — layer by
   longest path over every arrow — is wrong here, and *lowest* is right for the horseshoe case.
2. **The horseshoe join is a wave-boundary artifact, not a block author's output.** `FC_Inputs` and
   `FB_Motor` are peers with no dependency between them, so the wiring between them is not designed by
   either side. It is generated from the map at the gate — which is also what keeps the shared
   boundary objects out of the agents' hands (see *Conflicts*).

### Cycles are violations, not cases

A plant cycle — conveyor A will not run unless B is running; B stops if A faults — does **not** appear
in the block graph. The motors are order 1 and know nothing about each other; the interlock is an
order-2 block that reads both states and writes both permissives. `A → L → B` and `B → L → A`, no loop.

Generalised: **coupling between same-order blocks is always mediated by the layer above.** The
layering does not merely describe the program, it *prevents* cycles. Therefore a genuine cycle in the
block graph is a **violation** — coordination logic ended up inside an equipment block, which is C-127
with a new name — and the model must report it rather than handle it. An earlier draft of this
decision proposed SCC condensation to absorb cycles gracefully; that was rejected, because handling
the defect gracefully would have quietly licensed it.

**Plant mode is the case that looks like a cycle and is not.** Nearly every order-1 block reads the
mode while mode sits conceptually at the top. It is classed as a **border** — an ambient input, the
same class as HMI — or one enormous cycle swallows the program.

## The two roles

The map is drawn by a single derivation. That is the point: **one reader, N implementers.**

This is the structural answer to the failure this project exists because of. The PlantAutoControl-bench
autopsy found that an AI reviewer reading the same register as the AI coder is a **correlated** check,
so both failed together on an ambiguous `/`. N coder agents each reading the spec are correlated in
exactly the same way — and they do not disagree randomly, **they agree wrongly**. Reconciliation after
the fact catches divergent readings, which were the survivable class, and is structurally blind to
convergent ones, because agreement reads as confirmation.

So interpretation happens **once**, and the parallel agents implement contracts rather than read
specs.

### Scout — before rung A

Reads the source material once. Produces **routing**: what is in which document, the equipment
inventory, which `references/<class>/` entries apply, what each rung needs to open. Stops every
downstream agent re-reading a 200-page spec.

**It names no blocks and no signals.** Rung A bans signals and interfaces; rung B additionally bans
block names. A scout suggesting blocks upstream of A would inject the answer into the process analysis
and destroy the abstraction ladder the rungs were built to enforce.

Its most valuable output is probably not routing at all but an **ambiguity register**: every clause
that can be read two ways, found once and escalated to the engineer before any agent touches it.

> 🔴 **The scout must FIND ambiguity, never RESOLVE it.** The moment it resolves, it becomes the
> single upstream reader and every downstream agent inherits its misreadings **systemically** rather
> than by sampling — the autopsy failure with better ergonomics. Same artifact, opposite consequence.
> Resolutions come from the engineer.

### Cartographer — after rung B

Draws the map: territories, borders, orders. Names blocks. Emits one **contract per territory**.
This replaces rung C, rung D and `gen-architecture`.

Order cannot be computed until the map exists, so order is derived from the **carve** — which is
derived from spec + IO + rung A + rung B — not from spec text directly.

## The build loop

Waves proceed outward by order. Within a wave:

1. **Parallel, per territory, one agent each:** the per-instance half of the old rungs C and D (the
   block's own spec and boolean render), the IR, and the test vectors.
2. **Batched at the wave boundary, serial:** `import-all` → **layout re-assert** → `compile-all` →
   `sanity-check` → deploy → run the wave's vectors. Plus the project-level half of C and D — the
   set-difference reconciliation, the discharge ledger, the cross-instance sweeps — and the horseshoe
   joins.
3. **Then the next order.**

Two resources force the batch and neither is negotiable:

- **Portal is a token.** One lane at a time on a project. Agents author IR with no Portal at all.
- **The rig is a token too.** Deployment is device-level — the smallest unit Openness will transfer is
  the whole PLC software — and there is one rig. **Vector authoring parallelises; vector execution
  does not.**

This still satisfies hard rule 4: the gate moves to the wave boundary, it is not skipped.

**Testability descends with order** — order-1 blocks touch the border, so the rig can drive them
directly; higher orders need their subordinates real or simulated. Wave order therefore front-loads
the highest-confidence testing, which is a second, independent argument for building outward.

## Conflicts: prevented, not accepted

An earlier draft accepted inter-agent conflict in the hope it would save time. **Rejected.** Conflict
is prevented mechanically where it can be, and the one class that cannot be prevented is designed out.

| Class | Treatment |
|---|---|
| Block numbers, alarm bits | **Prevented** — `converter claim --allocate`, atomic. Proven: A took FB9020, B was refused on the same value with A's purpose named, B took FB9021 |
| Territory overlap | **Prevented** — `reachable-state` closures intersected at admission, *before* dispatch |
| Shared boundary objects (`DB_Inputs`, alarm words, tag tables) | **Designed out** — generated from the map at the gate; **a coder agent never edits them.** This is where conflict would otherwise concentrate, since every order-1 agent touches them and agents work in separate worktrees |
| Semantic gap-filling | **Cannot be detected.** See below |

> 🔴 **The class that does not resolve cleanly.** Two agents given an underspecified contract fill the
> gap differently, both compile, both pass their own vectors, and **nothing collides** — it is not a
> conflict, it is two self-consistent readings. `cross-check` catches structural divergence
> (multi-writer, dead wiring, IO boundary); it cannot catch this. The mitigation is
> `docs/15`'s existing norm promoted to a **hard stop**: *a wave agent that finds a gap in its
> contract stops and asks — it never fills it.* Under parallelism a violation of that rule is both far
> more expensive and far harder to see than it is today.

**A new claim kind is needed:** `territory`. The registry has block-number, alarm-bit, db-member,
block-network, tag and block-edit; territory is the natural unit under this model and subsumes most of
them.

## The economics, measured

Taken from `gen/*/telemetry.log` — 20 stage types, 45 logged runs, **1,637 minutes**. Recorded here
because the estimate moved by a factor of three under scrutiny and the reasoning should be auditable.

| Stage group | Minutes | Share |
|---|---|---|
| Analysis + design, old pipeline (spec-analysis, A, B, C, D, architecture) | 742 | 45% |
| — of which **C + D + `gen-architecture`** (what the cartographer replaces) | **477** | **29%** |
| Per-block coding | 410 | 25% |
| Review | 290 | 18% |
| Gate / integration / compile | 120 | 7% |
| Other | 75 | 5% |

`gen-architecture` alone is **202 minutes, the single largest stage in the log.**

Splitting C and D by what is genuinely per-instance (~60%) versus project-wide (~40% — the derived
register, set-difference, discharge ledger, reverse sweeps), the parallelisable set under this model is
**575 min (35%)** conservatively, or **880 min (54%)** counting per-block AI review and vector
authoring. Amdahl at four effective agents gives **26%–40% wall-clock saving**; the ceiling with
unlimited agents is ~54%.

**Portal round-trips across every logged coding run total 16 — about 1.6 per block.** Preflight has
already squeezed that, so batching the compile is right but worth about an hour across a project, not
a multiplier. It was at one point argued to be the largest win; it is not.

**Caveats, stated because the sample is thin:** one plant across three reruns plus test-project001 and
the validation corpora — not independent samples; wall clock is confounded by Portal state, as
`gen-telemetry.md` says itself; several PlantAutoControl runs ended `blocked`, so their minutes include
stopping to raise questions. The design-to-coding ratio is roughly 3:1 in favour of design, which is
too wide to be noise.

### The dial that decides where in that range you land

**Contract thickness trades conflict safety against parallel fraction, directly.** Semantic divergence
stops being a risk only when contracts are complete — and completing them means the cartographer does
the interpretive work for all N territories, serially, on the critical path. Every piece of
specification moved into the cartographer to prevent divergence is a piece removed from the parallel
fraction.

Thin contracts: agents interpret, high parallelism, correlated misreadings return. Thick contracts:
near-zero divergence, and `gen-architecture`'s 202 minutes *grows* rather than shrinks as it absorbs
per-instance work back.

**Start thick and thin deliberately** where a wave shows the agents did not need the detail — rather
than start thin and discover divergence in a block that already compiled.

## Options considered

- **Keep rungs C and D, add the map alongside** (the first proposal in this discussion). Rejected by
  the owner: C, D and `gen-architecture` already split the "carve into blocks and fix the interfaces"
  job three ways, and one model doing it once is better than three artifacts doing thirds of it.
- **Accept inter-agent conflict and reconcile after each wave.** Rejected on the autopsy argument
  above — it defends against the cheap failure and not the expensive one, while *feeling* more
  validated than a single agent would.
- **Scout before A that suggests blocks.** Rejected: breaks the abstraction ladder A and B exist to
  enforce. Split into scout (routing, pre-A) and cartographer (blocks, post-B).
- **SCC condensation for cycles.** Rejected: solves a problem the model is built not to have, and
  would license the defect it should report.
- **Longest-path layering.** Rejected: imports software-dependency intuition where downward command
  flow is not a build dependency.

## Consequences

**Easier.** Interfaces are derived rather than argued, which removes rework — and the S6 record is
explicit that structural defects were *"design-stage mistakes, caught at code-stage prices."* Block
structure becomes checkable as a tiling property before any ladder exists. The multi-agent machinery
already built (`src/wave-control/`, the claims registry) finally has a use that needs it. Testing is
front-loaded onto the wave where the rig can drive inputs directly.

**Harder.** The cartographer is on the critical path for everything, including the parallel work —
agents cannot implement contracts nobody has written. It is the new bottleneck and the thing most
likely to eat the win. Review load arrives per wave, i.e. a whole layer at once, which collides with
`docs/11-review-workflow.md`'s rule that a presentation carrying a whole subsystem is a rejection
reason by itself; wave 1 may be tolerable because order-1 blocks are homogeneous (review the class
once), but higher waves are bespoke.

**Commits us to.** A parseable map artifact from day one, because replacing rungs C and D removes the
inputs two mechanical-floor checks are built on.

> ⚠️ **Which checks, measured rather than quoted — `docs/15` overstates this and the overstatement
> would have inflated the estimate.** That doc says *"`relation-reconcile`, `signal-sweep`,
> `candidate-scan` and `undriven-scan` parse these files"*. Only the **first two** do, both through
> the shared `RelationArtifactParsers` (241 lines, `ParseSpecs`/`ParseLedger`/`ParseRegister`/
> `ParseRender`). **`candidate-scan` and `undriven-scan` take `--project <ir-dir> --fb <FBName>` and
> read IR only** — they are unaffected by anything in this ADR. Verified 2026-08-17 by reading the
> runners, not the doc.

**Standing honesty.** `src/wave-control/` is **built and has never run with two agents contending**,
and `Harness.Loop` has never run a wave end to end. Every conflict prediction in this ADR is
prediction.

## Open items

1. 🔴 **The mixed-order block — must be closed before a wave scheduler is written.** A block consuming
   state from an order-1 block *and* from an order-3 block: "lowest" makes it order 2, but it cannot be
   built in wave 2 because the order-3 interface does not exist yet; the `1 + max` rule makes it order
   4. Both answers are workable and it is the owner's call — but the scheduler needs to know which,
   and it is obvious now and expensive at wave 3.
2. **Does the model predict a rule we do not already have?** If it only re-derives C-127/C-304/C-308,
   it is a good restatement. If it predicts one, it is a discovery. Worth looking for deliberately.
3. **Where `relation-reconcile` and `signal-sweep` re-point** once C and D no longer emit their
   artifacts — the map artifact has to carry, in some form, the instance/relation sets and the signal
   claims those two checks currently read out of the spec files and the ledger.

## Revisit trigger

The first wave run. Wave 1 should be small — three or four order-1 blocks, two agents — and its
deliverable is **evidence the wave machinery works**, not the blocks; at full width the harness and the
ladder are debugged simultaneously and failures are not attributable. The number to instrument is the
**cartographer's wall clock**, logged to `telemetry.log` in the existing format, against the 477
minutes it replaces.
