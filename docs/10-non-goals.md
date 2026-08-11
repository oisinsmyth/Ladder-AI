# 10 — Non-Goals & Hard Exclusions

Explicit list of what this project will not do. "Permanent" items are never revisited; "not now" items may become goals later by conscious decision (with an ADR), never by drift. Ideas still under debate — not yet decided enough to be a "not now" — live in `16-future-ideas.md`; a future idea rejected as a "never" lands here, via ADR.

## Permanent exclusions

1. **Safety logic.** No AI reading, writing, converting, or explaining of F-blocks, F-runtime groups, safety signatures, or anything in the safety program. Enforced in tooling at export time, not by convention. Safety engineering remains an entirely human, formally-assessed activity.
2. **Non-LAD PLC languages.** No SCL, STL, FBD, GRAPH, or CFC deliverables — workplace constraint. (Reading a stray SCL block to *explain* it is also out: the pipeline treats non-LAD blocks as opaque.)
3. **Writing to hardware in service.** No tool in this repo writes to a device that is in service — no download, no online edit, no tag force, no configuration write, and no process-data write. Applying any change to a live device remains a human act in TIA Portal, and **the tooling does not connect to a device in service at all** — read-only online access is itself scoped to allowlisted *test rigs* by ADR-0008, not to production. On a device in service the tooling's role is therefore offline: analysing artefacts the engineer exports and hands over, and proposing the change. **Writing to an allowlisted test rig whose outputs are physically incapable of actuating is permitted, and is governed by ADR-0009** — that is the only exception, and it is a statement about the *target*, never about the kind of write: on a qualifying rig every write class is available (download, online edit, force, configuration, process data), and on a device in service none is. *(Re-scoped 2026-08-11; this item was a blanket permanent write ban until then. **Read-only** online access was separately narrowed out of it on 2026-08-10 and remains governed by ADR-0008.)*
4. **Unreviewed promotion, and change with no way back.** *(Re-scoped 2026-08-11. This item used to read "**Autonomous operation.** No AI change enters the TIA project without a human review step. There is no 'auto-apply' mode, ever." Autonomy inside a development loop is now explicitly wanted — the goal is a loop as close to ordinary software development as the substrate allows. What is excluded is the two properties that made autonomy dangerous, not the autonomy.)*

    **(a) Promotion without review.** An AI may iterate freely **inside** its own environment — editing IR, importing to a scratch project, compiling, and where a rig is available, downloading and testing on it — as often as it likes, without asking. What it may not do is **promote** work across an environment boundary unreviewed. Scratch → the real TIA project is a human review of the diff; the real project → a device in service is a human act (#3). The review moved from *every change* to *every promotion*; it did not go away — in shape it is the same bargain ordinary software makes, where work proceeds unsupervised until it asks to be merged.

    **What the environments actually are — the analogy invites the wrong picture, so state the mechanism.** There is **no git branch per change**. The separation is by **copy and by hardware**: a scratch TIA project is a directory copy of the real one, and a rig is a physically different device from the one in service. Git covers *tracked* content — the pipeline project's IR, the docs, the tooling — but **`Live Runs/` is gitignored**, so on a live job the "branch" is a copied directory and the restore point required by (b) is a deliberate backup, not a commit. Know that before relying on it: **for live-run content nothing reverts by itself**, and the discipline that makes (b) true there is manual.

    **(b) A write with no way back.** No AI writes to a device or to a project without a **verified restore point captured first** — the prior content recorded, and provably restorable. If the restore point cannot be captured, **the write does not happen**. Restore is a single operation, and its success is confirmed by re-reading the target, never assumed. Note that the hard case is retentive data and DB actual values, which a download can silently reinitialise: a restore point that captures only blocks is not a restore point.

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
