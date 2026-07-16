# ADR-0004 — Staged generation pipeline (skills + isolated-context reviewers)

- **Status:** Accepted
- **Date:** 2026-07-16
- **Numbering note:** ADR-0003 is deliberately skipped — `docs/13-data-boundary.md` has reserved
  that number for the data-boundary decision since the doc suite was written; taking it here would
  leave a dangling reference in a doc that is still awaiting its decision.

## Context

S6's first real end-to-end build (GenProject1 / Kestrel Shredder, 2026-07-15) met every hard gate —
compile-clean, zero invented tags, pattern-grounded where patterns existed — and the project owner
judged the resulting ladder functionally right but **overly complex and obtuse**. The build record
(`docs/notes/stage-gates.md`, "S6 first real proof") localizes the causes: architecture-stage
mistakes caught only at end-of-line review by the owner personally (C-126 and C-127 were created
*from* that review), a simplicity bar that existed only in the owner's head rather than as citable
rules, thin pattern coverage forcing conventions-grounded freeform, and a single presentation
carrying an entire subsystem — which `11-review-workflow.md` itself names a rejection reason.

The owner's stated priority order for generated LAD: **function → readability & simplicity →
efficiency**.

## Decision

Restructure S6 generation as a staged pipeline of Claude Code skills plus isolated-context
reviewer agents, specified in `docs/15-generation-pipeline.md`:

- Thirteen skills across Analyse / Design / Build / Check / Entry phases, each producing a small,
  committed, engineer-reviewable artifact; stages hand off **through artifacts on disk, never
  through conversation context**.
- Adversarial checks run as fresh-context subagents with read-only tools, given only the artifact
  and its binding docs — never the author's reasoning (the AI form of the blind-review standard
  this project already applies to pattern admissions).
- Two hard engineer gates: architecture sign-off before coding; the existing final presentation.
- A pipeline-wide tag-status rule (`exists` vs `proposed`) so multi-stage handoff can never launder
  an invented tag into "assumed real" (hard rule 3, applied to the pipeline).
- The priority ordering is codified in `06-lad-conventions.md`'s preamble, each tier mapped to the
  check that enforces it; a dedicated simplicity rule set (planned C-6xx) is mined from a
  GenProject1 retrospective and owner-approved before the simplicity reviewer is built.
- Build order: rules → reviewer skills (validated against GenProject1 as the known-defect corpus)
  → architecture skill → analysis skills → orchestrator. No big-bang build.

CLAUDE.md's existing 5-step generation workflow is **wrapped, not replaced** — it remains the
inner loop of the block-coding/integration stages.

## Options considered

- **Better-prompted monolithic one-shot** (keep single-session generation, add simplicity
  instructions to CLAUDE.md/prompts): cheapest, but attacks the weakest cause. GenProject1's worst
  defects were *structural* decisions reviewed too late, and a monolithic flow has no cheap
  review point before coding; prompt-only quality bars also decay without an adversarial check
  that cites written rules. Rejected.
- **Staged pipeline (chosen):** matches the evidence — each identified cause gets a named
  counter-mechanism (early architecture gate, written rules + blind reviewers, harvest step,
  small per-stage diffs). Cost: more moving parts, risk of bureaucracy — mitigated by the
  scale-down rule (the entry skill selects stages; small requests skip most of the chain) and by
  building skills one at a time in leverage order, each validated before the next.
- **Full multi-agent orchestration from day one** (build all 13 skills + orchestrator now):
  rejected as speculation — stage shapes should be proven on real requests before an orchestrator
  hard-codes them (same reasoning that deferred the S8-era pattern taxonomy to "grow organically").

## Consequences

- `docs/15-generation-pipeline.md` is the normative reference for S6 generation structure;
  CLAUDE.md points to it. `docs/06-lad-conventions.md` carries the quality bar and (after owner
  sign-off) the C-6xx simplicity rules. `docs/notes/genproject1-retrospective.md` is the rule-mining
  vehicle and the standing validation corpus description.
- New skills land in `.claude/skills/` incrementally (`gen-*` pipeline skills, `review-*`
  reviewers, `generate` entry); reviewer agents get read-only tool allowlists.
- Generation-project artifacts live in `gen/<project>/`, committed and diffable.
- The pipeline stays greenfield-scoped: no edits to existing networks until S7 passes its gate.
- Risk accepted: pipeline overhead on small requests — watched via the scale-down rule; if the
  overhead proves real anyway, that's a revision to `docs/15`, not a silent bypass.
