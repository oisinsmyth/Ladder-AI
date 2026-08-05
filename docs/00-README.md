# Ladder-AI — Pre-Design Document Suite

AI-assisted Siemens LAD engineering via TIA Openness. Generated 2026-07-09; these files are intended to live in the repo as `docs/` (ADRs in `docs/adr/`), with `CLAUDE.md` at repo root.

## Reading order

| File | What it is |
|------|-----------|
| `01-scope.md` | Purpose, hard constraints, initial scope, success criteria |
| `02-roadmap.md` | Staged expansion S0–S9 mapped to the 11 project goals, with entry/exit criteria |
| `03-development-plan.md` | How the work gets done: setup, tooling, milestones, cadence |
| `04-design-philosophy.md` | The 10 principles that settle design arguments |
| `05-architecture.md` | Pipeline, components, repo layout, portability strategy |
| `06-lad-conventions.md` | LAD style guide — **populated**; remaining gaps tracked in its own "To fill in" section |
| `07-pattern-library-spec.md` | What a pattern is and how one earns library admission |
| `08-testing-strategy.md` | Golden round-trip, compile gate, simulation layers |
| `09-risk-register.md` | Risks R-01…R-12 + assumption log |
| `10-non-goals.md` | Permanent exclusions, not-nows, anti-goals |
| `11-review-workflow.md` | The human review checklist — what "reviewed" means |
| `12-glossary.md` | Project vocabulary |
| `13-data-boundary.md` | What data may reach the AI — **draft, needs a decision (R-08)** |
| `14-s2-explanation-checklist.md` | S2's explanation-quality checklist — **populated**, derived from real trial comparisons |
| `15-generation-pipeline.md` | S6 generation pipeline — stages, skills, artifacts, gates (ADR-0004) |
| `16-future-ideas.md` | Candidate ideas under debate — merits/costs, verdicts, ADR-gated promotion into the plan |
| `adr-0000…0006` | Decision records (`docs/adr/`): `0000` template; `0001` IR format (Accepted); `0002` tooling languages (Accepted, revised 2026-07-10 — converter Python→C#); `0004` generation pipeline (Accepted); `0005` derive-always sidecar (Accepted owner, 2026-07-18); `0006` contact fan-out annotation `split`/`recv` (Accepted owner, 2026-07-19). **`0003` is reserved** by `13` for the data-boundary decision and is still unwritten |
| `notes/` | Working notes — not design documents and not a stage record: environment quirks (`openness-quirks.md`, `openness-api-surface-v20.md`), playbooks (`compile-error-playbook.md`), live gate/plan trackers (`stage-gates.md`, `synthesis-parity-plan.md`, `converter-synthesis-gaps.md`), conventions still finding their shape (`gen-telemetry.md`), and the open-question/deferral queues (`owner-questions.md`, `deferred-items.md`). Read `stage-gates.md` first — it says which roadmap stage is active |
| `evidence/` | Per-stage evidence (`stage-S0.md`…`stage-S6.md`) plus the blind-run transcripts and gradings the reviewer/generation skills were validated against, and the autopsies behind design changes (e.g. `PlantAutoControl-bench-autopsy.md`, the correlated-check finding that produced the four-rung spec pipeline). The long-form proof behind claims the other docs state in one line |
| `audit/` | Dated **project**-audit records (not ladder review — that's the review skills' job) — stage/goals, doc-consistency, docs-vs-code, tooling quality, dead-links, compliance, AI-operational health, governance-record & git hygiene. Run periodically, not on every change. **Read-only** — each audit emits a findings report + an actionable fix list, and changes nothing else. `audit/README.md` is the standing charter: dimensions D1–D10, method, naming, and output contract |
| `CLAUDE.md` | The distilled operating manual Claude Code reads every session |
| `CHANGELOG.md` (repo root) | Hand-maintained, dated record of what changed and why — Git doesn't generate this on its own |

## Your immediate to-dos

1. Decide the data boundary (`13`) with whoever owns that call (doesn't block S0–S6 on the reference project).
2. Verify assumption log items A-01/A-02 during S0.
