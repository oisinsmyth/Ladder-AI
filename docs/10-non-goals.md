# 10 — Non-Goals & Hard Exclusions

Explicit list of what this project will not do. "Permanent" items are never revisited; "not now" items may become goals later by conscious decision (with an ADR), never by drift. Ideas still under debate — not yet decided enough to be a "not now" — live in `16-future-ideas.md`; a future idea rejected as a "never" lands here, via ADR.

## Permanent exclusions

1. **Safety logic.** No AI reading, writing, converting, or explaining of F-blocks, F-runtime groups, safety signatures, or anything in the safety program. Enforced in tooling at export time, not by convention. Safety engineering remains an entirely human, formally-assessed activity.
2. **Non-LAD PLC languages.** No SCL, STL, FBD, GRAPH, or CFC deliverables — workplace constraint. (Reading a stray SCL block to *explain* it is also out: the pipeline treats non-LAD blocks as opaque.)
3. **Direct download to hardware.** No tool in this repo ever downloads to a PLC, online-edits, or forces tags. The pipeline ends at "imported and compiled in the TIA project"; going online is a human act in TIA Portal.
4. **Autonomous operation.** No AI change enters the TIA project without a human review step. There is no "auto-apply" mode, ever.

## Not now (revisit only via ADR)

- Other PLC vendors (Rockwell, Beckhoff, Codesys) — designed-for but not built until Siemens pipeline is proven.
- HMI engineering (screens, scripts, faceplates). HMI *documentation* (alarm tables) is in scope as data extraction.
- Hardware configuration / network configuration via Openness.
- Multi-user / team deployment — single engineering PC first.
- Version-comparison against PLC online state.
- Training/fine-tuning models on project data.

## Anti-goals (things that would look like progress but aren't)

- A generator that produces impressive freeform LAD without pattern grounding — explicitly worse than a smaller, pattern-based one (`04-design-philosophy.md` §6).
- Skipping golden tests to demo generation sooner (§5, §8).
- IR "improvements" that make it nicer for AI but unreadable for the engineer — the IR serves both or it fails (§3).
