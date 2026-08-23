# 15 — Generation Pipeline (S6): Stages, Skills, and Artifacts

**Status: ADOPTED 2026-07-16 (ADR-0004).** How S6 generation is organized from test-project001 onward.
The individual skills are built incrementally — the "Build order & status" table at the bottom is
the ground truth for what exists vs. what is still design.

🛑 **BUILD-OUT SUSPENDED 2026-08-17 along with the staged plan** (`docs/03-development-plan.md`). The
**pipeline itself is not suspended and ADR-0004 still stands**: the skills that exist are built,
validated and in use, and the stage contract they follow is unchanged — a live job still runs through
them under `CLAUDE.md`'s hard rules. What stops is *building the rest*: the unbuilt rows in the "Build
order & status" table stay unbuilt, and a missing skill is still no excuse to do the stage inline
(hard rule 8 — `lad-coder` performs it manually to the same contract, exactly as before).

## Why this exists

S6's first real build (test-project001 / Kestrel Shredder, 2026-07-15 — `docs/notes/stage-gates.md`)
was functionally correct and compile-clean, and the project owner judged it overly complex and
obtuse. The record shows why, and none of it was a compile-gate failure:

1. **Structural defects were caught at end-of-line review, by the owner, not by process.** C-126
   (grouping by instruction kind) and C-127 (hardcoded instance refs in an FB) were both born from
   that review, then fixed by restructuring finished blocks — design-stage mistakes, caught at
   code-stage prices.
2. **The simplicity bar was unwritten.** Before the review, no rule forbade a block-wide "Timers"
   network. A checker — human or AI — can only catch what a rule names.
3. **Thin pattern coverage forced conventions-grounded freeform.** Two admitted pattern kinds
   couldn't cover a stepped sequencer, pusher control, and alarm packing; freeform is where
   obtuseness breeds.
4. **One presentation carried a whole subsystem** (4 DBs, 3 FBs, 2 FCs, OB1, UDTs) —
   `11-review-workflow.md` calls that a rejection reason by itself.

The response: split generation into stages, each producing a small reviewable artifact, each
checked by an adversarial pass that cites written rules, with the engineer's sign-off at the two
points where mistakes are cheapest to kill.

## The 20% freeform threshold — what it is FOR (migrated from CLAUDE.md 2026-08-21)

If more than ~20% of a request needs freeform (non-pattern) rungs, say so and get explicit go-ahead
before writing them.

**What the threshold is FOR (owner clarification, 2026-08-07):** it is a rough prompt to check you
have not missed an already-solved problem *because a pattern's name did not match what you were
looking for*. **It is not a cap on freeform.** Some projects genuinely need more, and that is fine;
early-stage work on a thin library needs a lot.

So the go-ahead is normally granted — **what is not optional is doing the check before asking.**

## The quality bar

Generated LAD is prioritized, in order:

1. **Function** — it does what the requirement says. Enforced by the compile gate (never
   sufficient alone), the functional review (REQ-by-REQ traceability), and eventually S9 sim tests.
2. **Readability & simplicity** — the site mantra (`06-lad-conventions.md`): an electrician with a
   multimeter and a spanner can look at a rung and get a rough idea of what's going on. Enforced by
   the simplicity review against written rules.
3. **Efficiency** — only on a *measured* problem (scan-time, memory), never speculatively. On an
   S7-1200 running plant logic this is nearly never the constraint. A reviewer may cite this
   ordering to recommend an optimization be deleted.

A finding of "correct but harder to read than it needs to be" is a real finding, not a style nit.

## Stages and skills

Fifteen skills across five phases (raised from thirteen, 2026-07-17 — owner-questions A-2: the
Build phase's coding skill split into three), **plus the three additional spec-pipeline rungs added
2026-08-05** (see "The four-rung structured spec pipeline" below) — eighteen in total, twelve of them
built. Names are the planned `.claude/skills/` names:
pipeline-only skills carry a `gen-` prefix; the reviewers don't, because they are useful against
*any* block, generated or not. Each skill's contract: declared input artifacts, one output
artifact, the docs that bind it, and stop conditions (missing info → a question in the artifact's
"Open questions" section, never an invention).

| # | Skill | Phase | Consumes → Produces | Anchors |
|---|-------|-------|---------------------|---------|
| 1 | `gen-spec-analysis` | Analyse | Functional description → `requirements.md`: numbered register (REQ-nnn), equipment inventory, C-113 memory-test class per sequence | C-113; `13-data-boundary.md` at entry |
| 2 | `gen-pid-analysis` | Analyse | P&ID / drawings → `equipment-topology.md`: per-instance topology + interlock relations, process language only | C-114/C-116 (chain direction = material flow) — **BUILT 2026-08-05, and it is rung A of the four-rung pipeline below** |
| 3 | `gen-io-tags` | Analyse | Site IO list + owner's address table → `io-map.md` + proposed tag-table IR | C-001, C-304; addresses never invented (hard rule 3) |
| 4 | `gen-reconcile` | Analyse | Artifacts 1–3 → `rfi.md`: three-way cross-check, gaps, contradictions, undefined edge cases (first scan, restart, E-stop recovery, simultaneous inputs) | `11-review-workflow.md` edge-case list |
| 5 | `gen-architecture` | Design | Artifacts 1–4 → `architecture.md`: block manifest carved reuse-first (owner-questions A-1), UDT interfaces, DB landscape, enable-chain graph, OB1 order, pattern-tier mapping + freeform %, REQ→block trace | C-109/C-110/C-113/C-114/C-115/C-127, C-30x |
| 6 | `gen-alarm-design` | Design | Artifacts 1, 5 → `alarms.md`: category words, cause→consequence suppression matrix, texts, severities | C-501–C-507 |
| 7 | `gen-block-new` | Build | `architecture.md` (tier-(d)/freeform or tier-(a)/(b) items) + `patterns/` → one new block's IR, compile-clean | Existing 5-step loop (CLAUDE.md); C-126 at write time |
| 8 | `gen-block-modify-purpose` | Build | `architecture.md` (tier-(c) item) + as-built block → modified block's IR, compile-clean, untouched-network invariance proof | CLAUDE.md "Workflow for modifying existing logic" (S7 gate); shared modification-choreography reference (§ below) |
| 9 | `gen-block-modify-fix` | Build | A named defect (e.g. owner-questions §B docket) + as-built block → fixed block's IR, compile-clean, untouched-network invariance proof | Same as `gen-block-modify-purpose`; validation corpus is the B-docket |
| 10 | `gen-integration` | Build | Coded blocks + `architecture.md` → wired `FC_ControlMain`/OB1/instance DBs; whole-device compile | C-109/C-110/C-111/C-127 |
| 11 | `review-conventions` | Check | IR + `06-lad-conventions.md` → findings (rule ID, location, severity, fix) | Wraps `converter review` + AI pass over non-mechanical rules (S4) |
| 12 | `review-functional` | Check | `requirements.md` + IR (no author reasoning) → per-REQ trace; unimplemented REQs; **unrequested logic** | Requirements register |
| 13 | `review-simplicity` | Check | IR + written simplicity rules → findings; one-sentence-per-network test | C-6xx (pending), C-101, C-126, the quality bar above |
| 14 | `audit-artifact` | Check | Any stage artifact + its raw source → dropped/hallucinated-content findings | `14-s2-explanation-checklist.md` precedent |
| 15 | `generate` | Entry | A request → stage selection, per-project state, gate enforcement, final presentation, S8 harvest | CLAUDE.md S6 workflow step 5 |

### The four-rung structured spec pipeline (added 2026-08-05)

Four skills that run **alongside `gen-architecture`, not replacing it**, covering the Analyse→Design
span with a stricter artifact chain. They are deliberately *not* numbered into the table above: rung A
is skill #2, and the other three are additions, so numbering them in-sequence would renumber skills
whose own SKILL.md files cite their number. Use the rung letters.

| Rung | Skill | Consumes → Produces | The rule that defines it |
|---|---|---|---|
| **A** | `gen-pid-analysis` (= #2) | P&ID / layout + equipment references → `equipment-topology.md` | **process relations only** — no signals, no IO, no booleans |
| **B** | `gen-functional-analysis` | Supplied functional description → `plant-behaviours.md` | plant intent only, grouped and scoped to equipment |
| **C** | `gen-equipment-spec` | A + B + the IO table → `equipment-specs/<Instance>.md` (+ `requirements.md`) | **signals enter here**; booleans and interface members do not |
| **D** | `gen-code-structure` | C → `code-structure.md` (+ `architecture.md`) | **booleans and interface members enter here, and nowhere earlier** |

**Why they exist.** The S6-Killer-Plan autopsy found the root cause of a shipped REGRESSION: *an AI
reviewer reading the same register as the AI coder is a **correlated** check, so both failed together
on an ambiguous `/`* (`docs/evidence/PlantAutoControl-bench-autopsy.md`). The rungs break that correlation by
forcing each layer of meaning to enter at exactly one place, where it can be checked.

**Their artifact formats are contracts, not conventions.** `converter relation-reconcile`,
`signal-sweep`, `candidate-scan` and `undriven-scan` parse these files — the mechanical floor
(FI-36/FI-39). Read the SKILL before writing one of these artifacts by hand: a malformed spec file
either hard-errors or, worse, silently reads as zero coverage. The shapes are specified in each SKILL,
not here, so there is one authority per contract.

**Maturity — do not read this table as more settled than it is.** Exercised on **one plant across three
runs** (`gen/PlantAutoControl-bench-rerun{,2,3}/`), never yet on a second. And every reference currently in
`references/<class>/` self-declares as derived-from-as-built rather than from an engineering standard,
so rung A's *completeness by construction* is **nominal, not delivered** — the binding open item.

### Building a missing reference: the co-authoring loop (owner ruling, 2026-08-06)

**This is now the standard route for a class with no `references/<class>/` entry.** It exists because
the alternative silently defeats the pipeline: when rung C constructs a class requirement set from
the current job's own documents, the coder and the reviewer end up reading the same source, which is
the **correlated check** the four rungs were built to break (`docs/evidence/PlantAutoControl-bench-autopsy.md`).
Proceeding anyway is not neutral — it removes the guarantee while leaving the artifacts looking
complete.

**The loop.** Whenever an object, a pattern, or — most importantly — an **FB** is identified for which
no class reference exists:

1. **Flag it to the engineer BEFORE generating anything.** Not after a draft, not with a draft
   attached. The point is to reach agreement on what the class *is* before any ladder exists to
   anchor on, because a draft is an anchor whether or not anyone intends it to be.
2. **Work the spec together, in both directions — including specs already approved.** An approved
   spec is not closed to this: the class question ("what must ANY valve do?") is a different question
   from the instance question the approval answered ("what must THIS valve do?"), and the second
   having been signed off does not settle the first.
3. **Generate the ladder and import it** once the spec is agreed.
4. **The engineer edits it directly in TIA Portal** and says what changed.
5. **Repeat 2–4 until both sides are satisfied.** The loop closes on agreement, not on a checklist.
6. **The settled result becomes `references/<class>/reference.md`** — and, unlike every entry that
   preceded it, one that does NOT self-disclaim as derived-from-as-built, because it was authored
   deliberately as a class definition and edited by the engineer in the tool.

**Why the TIA-side edit is the load-bearing step, and not a formality.** It is what makes the check
genuinely independent. The engineer editing real ladder in the real editor is reasoning from the
plant and from site practice — sources the AI does not have and cannot infer from the job documents.
That is precisely the independent second reading `references/` was supposed to supply and currently
does not. A reference written by the AI and merely *approved* by the engineer would be the correlated
check again, one step removed.

**Cost, stated honestly.** This is slower than generating from the job's own documents, and it front-
loads engineer time onto the first instance of each class. The return is that it is paid once per
class and amortised across every future project, and that it converts the library's standing
disclaimer into something real for that class. It is the only route currently proposed that does.

Skills 8–9 (the modify pair) share a **modification-choreography reference** — the common
procedure for scoping a touch to named network(s), proving the untouched-network invariance
check, and presenting the before/after diff (CLAUDE.md "Workflow for modifying existing logic").
Not itself numbered as a skill; split out to `docs/notes/modification-choreography.md` (2026-07-18),
which both modify skills reference.

Checks apply to *artifacts*, not just code: staging concentrates trust in artifacts, so an error in
`requirements.md` is amplified by every stage that consumes it — that is what `audit-artifact`
exists for.

## Artifacts: the handoff contract

- Pipeline artifacts for a generation project live in **`gen/<project>/`** (created on first use),
  committed and diffable like everything else the engineer reviews. Exported IR stays in
  `ir/<project>/` as today. `gen/<project>/telemetry.log` (one line per stage run,
  `docs/notes/gen-telemetry.md`) lives alongside the stage artifacts.
- **Each stage works from artifacts on disk, not from conversation.** An artifact must be complete
  enough for the next stage to proceed from it alone — if it isn't, that's a defect in the
  artifact, found early, exactly like an underspecified engineering document.
- Every artifact carries a short header: date, the inputs it was derived from (file + git hash or
  export date), and an **Open questions** section. `gen-reconcile` consolidates open questions into
  the RFI; any skill may append, none may silently resolve one.
- **Tag status — the anti-laundering rule.** Every tag named in any artifact is marked either
  `exists` (verified present in the current `ir/<project>/` export — verified by grep at write
  time, not memory) or `proposed` (a named gap; the engineer creates tags — hard rule 3).
  `gen-block-new`/`gen-block-modify-purpose`/`gen-block-modify-fix` refuse to reference a
  `proposed` tag; promotion to `exists` happens only via
  a fresh export showing it. A pipeline must never let "proposed in stage 1" mutate into "assumed
  real by stage 7".
- **Verbatim evidence lives in `docs/evidence/`, not inline (the split convention).** A persisted
  reviewer or skill-validation record is split at write time: the large verbatim block — a
  `converter review` mechanical dump, a full blind-run transcript, a long per-REQ trace — goes in
  `docs/evidence/<name>.md`; the record in `docs/notes/` (or a `docs/notes/stage-gates.md` entry)
  keeps only the analysis, verdict, and a link to it. This keeps the constantly-read notes and the
  `stage-gates.md` status index lean while preserving every line of raw evidence verbatim — the
  property that makes it usable for drift checks and blind-review transparency. Born in the right
  shape, not split by hand later. The three reviewer skills (11–13) instruct this in their
  Report-structure sections; origin and rationale: `docs/16-future-ideas.md` FI-19/FI-20.

## Isolation model

- **Skills** are the default: procedure + checklist, run inline.
- **Subagents (isolated context) are used exactly where isolation buys correctness:**
  - The three reviewers + `audit-artifact` run with fresh context and **read-only tools**, given
    only the artifact under review and its binding docs — never the author's reasoning. Blind
    review is the standard this project already holds pattern admissions to; this is its AI form.
  - The analysis skills (1–4) may run isolated so bulk spec/drawing content doesn't ride along
    into design and coding contexts.
- CLAUDE.md's hard rules bind every agent regardless; each skill additionally restates the ones it
  is most likely to trip (e.g. `gen-io-tags`: never invent addresses).
- **Digest vs full IR (decided 2026-07-16 — FI-15):** the reviewers (skills 11–13) always read
  **full IR** — a digest is never review input (the S2 lesson: exhaustiveness, not summaries,
  caught the real bugs; `converter digest`'s own contract says the same). Analysis (1–4), design
  (5–6), and the `generate` entry skill may use `converter digest` for orientation and
  cross-block indexing — "which block do I need to open?" — and the build skills (7–10) read the
  full IR of anything they touch. Digests are derived fresh on every run and never stored, so
  they cannot go stale by construction.

## Gates

Two hard gates, engineer-owned, never skipped:

1. **Architecture sign-off** — before any block is coded, whenever the request creates new blocks
   or interfaces. For a small request this may be a one-paragraph mini-manifest; it still gets a
   yes before coding. This is the cheapest place to kill a C-127-class mistake.
2. **Final presentation** — the existing S6 workflow step 5 (IR diff + intent + compile evidence),
   per `11-review-workflow.md`. Reviewer findings ship with the presentation, including ones the
   coder disagrees with.

**Scale-down rule:** the `generate` entry skill selects which stages a request actually needs. The
full chain is for greenfield; "add one motor to the existing project" needs roughly stages 5 (mini),
7, 8, and the checks. What is never scaled away: the two gates, the compile gate, the tag-status
rule, and the reviewers.

## Inner-loop tooling (adopted 2026-07-16 — FI-13/14/16, `docs/16-future-ideas.md`)

- **`converter preflight` runs before every import** (`<files> --project ir/<project>/`): parse,
  convert, tag/call/instanceof resolution — the tag-status rule's "exists, verified by grep",
  mechanized — plus `review` findings folded in. The bar is **zero findings**; a consciously
  accepted finding needs the engineer's explicit OK recorded (stricter-bar principle: generated
  content imports clean of known findings). Pre-flight is a filter in front of the compile gate,
  never a substitute for it (hard rule 4 — the tool's own output says so on every run).
- **On any compile failure, `docs/notes/compile-error-playbook.md` is the first lookup.** Entries
  are grounded hypotheses to verify against the actual error, never answers to trust blindly; new
  error→fix pairs proven during a run are harvested back into the playbook.
- **Every stage run appends one telemetry line** to `gen/<project>/telemetry.log` per
  `docs/notes/gen-telemetry.md` — including `manual:<stage>` runs and abandoned runs (the most
  informative rows). Never backfilled; a missing row beats an invented one. This log is what
  eventually settles FI-12 (batch/persistent Portal) and per-stage isolation choices with data
  instead of judgment.

## After approval: harvest (S8)

Every approved generation is a pattern-library candidate. The `generate` skill's last step asks:
did this build contain a shape worth admitting (per `07-pattern-library-spec.md`), or a deviation
from an existing pattern worth documenting? Growing pattern coverage is the standing fix for
cause 3 above — proven patterns are the distilled simple forms, and every harvest shrinks the
freeform surface the next build needs.

## Boundaries

- **No edits to existing networks.** The pipeline creates new blocks/networks only. Targeted
  modification (with untouched-network invariance) is S7's gate, not a stage to sneak in early.
- Hard rules 1–7 (CLAUDE.md) apply throughout: LAD only, no safety content, no invented reality,
  compile gate before "done", review before the real project, IR only.
- `13-data-boundary.md` is checked at the analysis skills' entry — that is where restricted material
  (specs, P&IDs, IO lists) enters the pipeline. P&ID reading from PDF drawings is additionally the
  least reliable input medium; `equipment-topology.md` is a draft-for-confirmation, never ground
  truth.
- REQ-nnn IDs are stable once assigned — S9 sim tests will trace to them.
- **Manual coding is bounded** (owner ruling 2026-07-17 — owner-questions A-3): once
  `gen-block-new` exists and is validated, ad hoc manual coding to the CLAUDE.md 5-step contract
  is no longer acceptable except by explicit **per-case owner waiver**, recorded where the waiver
  is granted (the waived request's own artifact or, absent one, `docs/notes/stage-gates.md`).
  Three coding waves ran manual-to-contract before this ruling (InCycle/DI4 fix, fix wave 1
  phases 1–2) — grandfathered, not a precedent for further manual runs.

## Build order & status

Skills are built in leverage order, each validated before the next starts — test-project001 (obtuse
but functionally correct, with an owner who knows what's wrong with it) is the standing validation
corpus: a reviewer skill that doesn't independently find the known problems isn't ready.

| Step | What | Status |
|------|------|--------|
| 0 | test-project001 retrospective → candidate simplicity rules (`docs/notes/test-project001-retrospective.md`) | Done 2026-07-16 |
| 1 | Owner accept/reject pass → C-6xx section in `06-lad-conventions.md` | Done 2026-07-16 (all 7 accepted; + C-203) |
| 2 | `review-simplicity` (validated against test-project001) | **Built + blind-validated** 2026-07-16 (`docs/notes/review-simplicity-validation-2026-07-16.md`) |
| 3 | `review-conventions` (independent of step 1) | **Built + blind-validated** 2026-07-16 (drift check byte-identical; `docs/notes/review-conventions-validation-2026-07-16.md`) |
| 4 | `review-functional` + `requirements.md` format definition | **Built + two-phase-validated** 2026-07-16 (format's defining instance: `gen/test-project001/requirements.md`, 69 REQs; historical-regression phase caught both known bugs blind; `docs/notes/review-functional-validation-2026-07-16.md`) |
| 5 | `gen-architecture` | **Built + validated** 2026-07-16 (independent-reconvergence method; baseline artifact `gen/test-project001/architecture.md`; `docs/notes/gen-architecture-validation-2026-07-16.md`); reuse-first Method rewrite (A-1) landed 2026-07-17 |
| 6 | `gen-block-new`, `gen-block-modify-purpose`, `gen-block-modify-fix` formalization (+ shared modification-choreography reference) — **pulled forward** ahead of the remaining analysis skills (owner ruling 2026-07-17, A-2/A-3: three coding waves ran manual-to-contract while this sat unbuilt at the old step 7) | **`gen-block-new` AUTHORED + VALIDATED 2026-07-18** (`.claude/skills/gen-block-new/SKILL.md`; two blind validations vs real blocks — FilterUnitSystem, ShredderControlSystem — `docs/evidence/stage-S6.md`). **`gen-block-modify-fix` AUTHORED + VALIDATED 2026-07-18** (`.claude/skills/gen-block-modify-fix/SKILL.md`; invariance gate = `converter diff --only`; validated on the genval2 fix corpus `gen/_validation/shredder/` — FR-1/FR-2 fixed compile-clean + invariance-proven, FR-3 correctly stop-and-routed). **`gen-block-modify-purpose` AUTHORED 2026-07-18 + VALIDATED 2026-07-20** (`.claude/skills/gen-block-modify-purpose/SKILL.md`) — the S7 purpose-change path (interface change + add/remove networks in scope); validated via a **blind MotorDOL→MotorVSDSystem purpose change** against a sanitized real answer key (`gen/_validation/MotorVSDSystem-purpose/`, `docs/evidence/stage-S6.md`): all 22 REQs implemented, NW1–5 byte-identical (invariance held), no gold-plating, even improved on the real block; 1 minor skill gap (NW9 reverse fail-to-stop). The compile gate surfaced a real converter part-order bug (Normalizer-masked): synthesized `<Parts>` weren't in TIA's required flow order. **Fixed** (`f3ad4ca` Access-subgroup + `7694fdf` the real fix: instruction parts now emit in wire-graph flow order via a DFS from the rail). **MotorVSDSystem then imports + compiles clean in TIA (0 errors)** — the compile gate is **CLOSED**, so both the compile gate and the fidelity grade confirm the validation. The fix (byte-stable on all real exports + live-validated) also de-risks the MotorStarter sidecar-drop. Gap I in `converter-synthesis-gaps.md` is done. **All three coding skills are now authored AND validated.** The shared modification choreography was split into **`docs/notes/modification-choreography.md`** (both modify skills reference it — the "worth splitting out" point, reached now that two exist). The 5-step manual loop (CLAUDE.md) remains the working seed until each skill is validated. **Internal sequencing ruled 2026-07-18 (A-4):** `gen-block-new` **first** (roadmap-fidelity — it's S6's own deliverable and S6-done is S7's entry gate; the earlier "S7 is the rush" premise that argued modify-first is retired), then `gen-block-modify-fix`, then `gen-block-modify-purpose`. **`converter diff` built now in parallel** as standalone tooling (S7 entry req; PC-side, no dependency) — done 2026-07-18. S7-gate timing ruled 2026-07-18 (D-4): roadmap S7 entry stays "S6 done" (kept as-is), so the path is `gen-block-new` → close S6's ten → open S7; full record in `docs/evidence/stage-S6.md` |
| 7 | `gen-spec-analysis`, `gen-io-tags`, `gen-reconcile`, `gen-alarm-design` (as the next real project needs them) | Not built (`gen-spec-analysis` performed once as a `manual:` run — its output contract is now defined) |
| 8 | The **four-rung structured spec pipeline**: `gen-pid-analysis` (rung A), `gen-functional-analysis` (B), `gen-equipment-spec` (C), `gen-code-structure` (D) — pulled forward out of this step by the S6-Killer-Plan autopsy, which found a correlated-check root cause the existing chain could not fix | **All four BUILT 2026-08-05** (`448251c`, amended `b66c1f8`/`c48057b`/`decd2d5`/`361c0e0`). Adversarially validated twice (`docs/evidence/four-rung-design-validation{,-round2}.md`) and exercised on three runs (`gen/PlantAutoControl-bench-rerun{,2,3}/`). **Caveats, both live:** one plant only, never a second; and `references/<class>/` self-disclaims as non-standards, so rung A's completeness-by-construction is nominal, not delivered |
| 8b | `audit-artifact`, `gen-integration` formalization | Not built |
| 8c | **`gen-data-structures`** — a data-structure stage that does not exist in the numbered skill list at all (§ "Stages and skills" has no entry for it). Authors the UDT and DB landscape from the design artifacts: types, blocks, members, data types, **retentivity**, start values; proves it by import + block-level compile + export-back. It is the stage that turns a retentive-data list and an HMI interface definition into objects a project actually contains. | **Not built, and now RUN MANUALLY FIVE TIMES AND COUNTING** (2026-08-05/06, one live greenfield job: an author pass, a rename + restructure pass, then three successive corrections as owner rulings landed). Per the standing convention that a *second* manual run is the trigger to build the skill rather than do it a third time by hand, this is **overdue, not due** — and the run count is itself evidence about the stage: structures get revised every time a ruling lands upstream, so 8c is re-entered far more often than a stage that runs once. Its gate hazard below is therefore paid repeatedly. Both runs were performed to contract by `lad-coder` under hard rule 8, so the contract is already observed rather than hypothetical — what it consumes, what it proves, and the five TIA behaviours it has to design around (`docs/notes/openness-quirks.md`) |
| 9 | `generate` orchestrator (last — after the stages it orchestrates exist) | Not built |
| 10 | **The test-environment contract skill** (the design-for-testability stage, phase 5.1 of the harness plan) — the contract a block and its vectors must satisfy to be *executed* on the rig rather than only reviewed | **Contract DRAFTED 2026-08-13** (`docs/notes/test-environment-contract.md`, with its coverage denominator in `docs/notes/assertion-enumeration.md`); **skill deliberately not yet written**. This is the point where the check stage stops being three reviewers and gains a *test* — see `docs/02-roadmap.md` S9 |

**Why 8c is listed out of numeric order:** it was never in the pipeline's own stage table. The
fifteen numbered stages go from analysis to blocks to integration and silently assume the data
landscape already exists — true on every project this pipeline had seen, because every one of them
was read out of a project that already had its DBs. The first greenfield job made the gap visible
immediately: nothing can be coded until the types and blocks exist, and no skill owned creating
them.

### 8c's gate is not the compile gate — and this is the stage's defining hazard

**Measured, not predicted (2026-08-06).** On a structure-only pass, a member was silently deleted
from five of six interface types by an editing error. Every static and compile check available
returned clean on the broken input:

| Check | Result on input missing a member from 5 of 6 types |
|---|---|
| `converter preflight` (whole project) | 0 findings, exit 0 |
| `openness-cli compile` (whole device) | **errors 0** — *state not decisive*, see the correction below |
| `openness-cli compile` (each block) | **errors 0** — *state not decisive* |
| `openness-cli sanity-check` | `HEALTHY`, 0 inconsistent |
| **member-by-member re-export diff vs the as-built baseline** | **caught it** — showed `+2/-1` where the change was `+2/-0` |

**Why every gate missed it.** Hard rule 4's compile gate works by making the *consumers* of a
definition object to it. In a structure-only phase there are no consumers yet — no FB has been
written against the type — so a missing member is a definition nobody references, which is exactly
what a compiler is entitled to accept. **The gate is close to vacuous here**, and passing it means
much less than it means at any other stage.

> 🔴 **CORRECTION, 2026-08-13 — THE TWO COMPILE ROWS ABOVE WERE WRONG AS ACCEPTANCE CRITERIA, AND
> AS WRITTEN THEY WOULD NOW FAIL ON A HEALTHY PROJECT.** They read *"`Success`, errors 0,
> warnings 0"*. Two things changed underneath them:
>
> 1. ***`openness-cli compile` now defaults to the STATION scope (hardware + program). It used to
>    compile the HARDWARE ONLY*** — `CompileDeviceItem` reached the device-item compiler, whose
>    message tree is `Hardware configuration` and nothing else. The station scope surfaces the
>    project's standing warnings, so **one pre-existing hardware warning anywhere makes every
>    compile report non-`Success` forever.**
> 2. Measured live after the change: a healthy project reports ***`Warning (errors=0, warnings=2)`***
>    while being entirely healthy. `sanity-check`'s own `IsHealthy` had the same defect and was
>    re-keyed onto the effective **error count**; it now reports `OVERALL: HEALTHY` against that
>    same `Warning` state.
>
> ***SO THE VERDICT KEYS ON `errors 0`, AND THE COMPILE'S STATE IS NOT DECISIVE.*** Warnings are
> still reported and still worth reading — they are simply not the gate.
>
> **Recorded as a correction rather than silently rewritten, because the failure mode matters more
> than the wording: *a gate criterion that fails on a healthy project gets worked around, and then
> it protects nothing.*** The same wording in hard rule 4's `CLAUDE.md` command table was corrected
> the same day.
>
> **What the rows above still demonstrate is untouched** — the member-deletion case is about a
> *vacuous* gate, not a mis-keyed one, and every check listed still missed it.

**The rule this stage carries as a result:** at 8c the real gate is the **sent-vs-returned re-export
diff against the previous as-built export**, and it must be *mechanical and member-by-member*, not a
visual read. Assert the shape of the intended change (`purely additive`, `+N/-0`, baseline order
preserved) rather than merely eyeballing that the new members arrived — the defect here was invisible
in the added members and visible only in the removed count. A quote-balance check over every member
line belongs in the same pass; a dangling comment fragment also survives compile.

**Corollary — the hazard runs the other way too.** Because nothing references these objects yet, a
*wrong* definition is equally silent. This is the same structural reason [FI-47](16-future-ideas.md)
bites at this stage and no other: a `RETAIN` written on a type member parses, compiles, exports and
drift-checks clean, and is discovered by a power cycle on a live plant. 8c is the stage where
"it compiled" is the weakest evidence in the pipeline, and it is also the stage that decides
retentivity — those two facts meeting is what makes the re-export diff non-optional here.

## Relationship to existing docs

- **CLAUDE.md "Workflow for logic generation"** — the 5-step loop remains, as the inner loop of
  `gen-block-new`/`gen-block-modify-purpose`/`gen-block-modify-fix`/`gen-integration`, until A-3's
  bound takes effect; this doc is the outer structure around it.
- **`06-lad-conventions.md`** — the rule base every reviewer cites; the quality bar lives in its
  preamble.
- **`07-pattern-library-spec.md`** — what `gen-block-new` composes from and what the harvest
  step feeds.
- **`11-review-workflow.md`** — the engineer's side of the two gates; unchanged.
- **`13-data-boundary.md`** — gate at analysis entry.
- **`14-s2-explanation-checklist.md`** — the precedent `audit-artifact` checklists follow.
