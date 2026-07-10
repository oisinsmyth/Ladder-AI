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
| `adr-0000…0002` | Decision records: template, IR format (accepted), tooling languages (accepted) |
| `CLAUDE.md` | The distilled operating manual Claude Code reads every session |

## Your immediate to-dos

1. Decide the data boundary (`13`) with whoever owns that call (doesn't block S0–S6 on the reference project).
2. Verify assumption log items A-01/A-02 during S0.
