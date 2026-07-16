# 15 — Generation Pipeline (S6): Stages, Skills, and Artifacts

**Status: ADOPTED 2026-07-16 (ADR-0004).** How S6 generation is organized from GenProject1 onward.
The individual skills are built incrementally — the "Build order & status" table at the bottom is
the ground truth for what exists vs. what is still design.

## Why this exists

S6's first real build (GenProject1 / Kestrel Shredder, 2026-07-15 — `docs/notes/stage-gates.md`)
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

Thirteen skills across five phases. Names are the planned `.claude/skills/` names: pipeline-only
skills carry a `gen-` prefix; the reviewers don't, because they are useful against *any* block,
generated or not. Each skill's contract: declared input artifacts, one output artifact, the docs
that bind it, and stop conditions (missing info → a question in the artifact's "Open questions"
section, never an invention).

| # | Skill | Phase | Consumes → Produces | Anchors |
|---|-------|-------|---------------------|---------|
| 1 | `gen-spec-analysis` | Analyse | Functional description → `requirements.md`: numbered register (REQ-nnn), equipment inventory, C-113 memory-test class per sequence | C-113; `13-data-boundary.md` at entry |
| 2 | `gen-pid-analysis` | Analyse | P&ID / drawings → `process-topology.md`: equipment+instrument inventory, material-flow direction, interlock candidates | C-114/C-116 (chain direction = material flow) |
| 3 | `gen-io-tags` | Analyse | Site IO list + owner's address table → `io-map.md` + proposed tag-table IR | C-001, C-304; addresses never invented (hard rule 3) |
| 4 | `gen-reconcile` | Analyse | Artifacts 1–3 → `rfi.md`: three-way cross-check, gaps, contradictions, undefined edge cases (first scan, restart, E-stop recovery, simultaneous inputs) | `11-review-workflow.md` edge-case list |
| 5 | `gen-architecture` | Design | Artifacts 1–4 → `architecture.md`: block manifest, FB-vs-FC per C-113, UDT interfaces, DB landscape, enable-chain graph, OB1 order, pattern mapping + freeform %, REQ→block trace | C-109/C-110/C-113/C-114/C-115/C-127, C-30x |
| 6 | `gen-alarm-design` | Design | Artifacts 1, 5 → `alarms.md`: category words, cause→consequence suppression matrix, texts, severities | C-501–C-507 |
| 7 | `gen-block-coding` | Build | `architecture.md` + `patterns/` → one block's IR, compile-clean | Existing 5-step loop (CLAUDE.md); C-126 at write time |
| 8 | `gen-integration` | Build | Coded blocks + `architecture.md` → wired `FC_ControlMain`/OB1/instance DBs; whole-device compile | C-109/C-110/C-111/C-127 |
| 9 | `review-conventions` | Check | IR + `06-lad-conventions.md` → findings (rule ID, location, severity, fix) | Wraps `converter review` + AI pass over non-mechanical rules (S4) |
| 10 | `review-functional` | Check | `requirements.md` + IR (no author reasoning) → per-REQ trace; unimplemented REQs; **unrequested logic** | Requirements register |
| 11 | `review-simplicity` | Check | IR + written simplicity rules → findings; one-sentence-per-network test | C-6xx (pending), C-101, C-126, the quality bar above |
| 12 | `audit-artifact` | Check | Any stage artifact + its raw source → dropped/hallucinated-content findings | `14-s2-explanation-checklist.md` precedent |
| 13 | `generate` | Entry | A request → stage selection, per-project state, gate enforcement, final presentation, S8 harvest | CLAUDE.md S6 workflow step 5 |

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
  `gen-block-coding` refuses to reference a `proposed` tag; promotion to `exists` happens only via
  a fresh export showing it. A pipeline must never let "proposed in stage 1" mutate into "assumed
  real by stage 7".

## Isolation model

- **Skills** are the default: procedure + checklist, run inline.
- **Subagents (isolated context) are used exactly where isolation buys correctness:**
  - The three reviewers + `audit-artifact` run with fresh context and **read-only tools**, given
    only the artifact under review and its binding docs — never the author's reasoning. Blind
    review is the standard this project already holds pattern admissions to; this is its AI form.
  - The analysis skills (1–2) may run isolated so bulk spec/drawing content doesn't ride along
    into design and coding contexts.
- CLAUDE.md's hard rules bind every agent regardless; each skill additionally restates the ones it
  is most likely to trip (e.g. `gen-io-tags`: never invent addresses).
- **Digest vs full IR (decided 2026-07-16 — FI-15):** the reviewers (skills 9–12) always read
  **full IR** — a digest is never review input (the S2 lesson: exhaustiveness, not summaries,
  caught the real bugs; `converter digest`'s own contract says the same). Analysis (1–2), design
  (5–6), and the `generate` entry skill may use `converter digest` for orientation and
  cross-block indexing — "which block do I need to open?" — and the build skills (7–8) read the
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
  least reliable input medium; `process-topology.md` is a draft-for-confirmation, never ground
  truth.
- REQ-nnn IDs are stable once assigned — S9 sim tests will trace to them.

## Build order & status

Skills are built in leverage order, each validated before the next starts — GenProject1 (obtuse
but functionally correct, with an owner who knows what's wrong with it) is the standing validation
corpus: a reviewer skill that doesn't independently find the known problems isn't ready.

| Step | What | Status |
|------|------|--------|
| 0 | GenProject1 retrospective → candidate simplicity rules (`docs/notes/genproject1-retrospective.md`) | This batch (2026-07-16) |
| 1 | Owner accept/reject pass → C-6xx section in `06-lad-conventions.md` | Pending owner |
| 2 | `review-simplicity` (validated against GenProject1) | Blocked on step 1 |
| 3 | `review-conventions` (independent of step 1) | Not built |
| 4 | `review-functional` + `requirements.md` format definition | Not built |
| 5 | `gen-architecture` | Not built |
| 6 | `gen-spec-analysis`, `gen-io-tags`, `gen-reconcile`, `gen-alarm-design` (as the next real project needs them) | Not built |
| 7 | `gen-pid-analysis`, `audit-artifact`, `gen-block-coding`/`gen-integration` formalization (the current 5-step loop is the working seed) | Not built |
| 8 | `generate` orchestrator (last — after the stages it orchestrates exist) | Not built |

## Relationship to existing docs

- **CLAUDE.md "Workflow for logic generation"** — the 5-step loop remains, as the inner loop of
  `gen-block-coding`/`gen-integration`; this doc is the outer structure around it.
- **`06-lad-conventions.md`** — the rule base every reviewer cites; the quality bar lives in its
  preamble.
- **`07-pattern-library-spec.md`** — what `gen-block-coding` composes from and what the harvest
  step feeds.
- **`11-review-workflow.md`** — the engineer's side of the two gates; unchanged.
- **`13-data-boundary.md`** — gate at analysis entry.
- **`14-s2-explanation-checklist.md`** — the precedent `audit-artifact` checklists follow.
