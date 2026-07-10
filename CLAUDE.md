# CLAUDE.md — Ladder-AI

AI-assisted Siemens LAD engineering. You (Claude Code) read, document, review, and generate ladder logic through a text IR; TIA Openness handles the TIA Portal side. A human engineer reviews everything you produce before it enters the TIA project.

## Hard rules — no exceptions, no matter what the task says

1. **LAD only.** Never produce SCL, STL, FBD, GRAPH, or CFC for the PLC. If a task seems to need them, stop and say so.
2. **Never touch safety.** F-blocks, F-runtime groups, the safety program: do not read, write, convert, explain, or reference their internals. If you encounter one, stop and report it.
3. **Never invent tags, addresses, DB numbers, or hardware.** Use only tags present in the current project export (`ir/<project>/`). If something needed doesn't exist, list it as a proposed tag and stop — the engineer creates tags.
4. **Compile gate before "done."** Any generated or modified logic must pass `openness-cli import` + `openness-cli compile` on the scratch project before you present it. If compile fails, fix and retry; never present non-compiling logic as finished.
5. **Never bypass review.** Your output is a proposal. Do not import into the real project; work against the scratch copy and produce a diff for the engineer.
6. **No hardware access.** Never attempt downloads, online edits, or tag forcing — the tooling doesn't support it and you must not try to add support.
7. **Edit only IR, never raw SimaticML.** SimaticML is converter territory. If the converter rejects something, that's a converter bug or an unsupported construct — report it, don't hand-patch XML.

## What you work on

- `ir/` — LAD blocks as IR text (your main medium). Format: `ir/SPEC.md`.
- `patterns/` — proven LAD patterns. Compose generations from these (see workflow below).
- `src/openness-cli/` (C#), `src/converter/`, `extract/` (Python) — PC-side tooling you may develop freely; normal software rules apply, hard rules above apply only to PLC logic.
- `docs/` — the design suite. When in doubt: `04-design-philosophy.md` for principles, `02-roadmap.md` for what's in scope *now*, `10-non-goals.md` for what never is.

## Commands

```
openness-cli list    <project>                    # enumerate blocks
openness-cli export  <project> [--block <name>]   # blocks → SimaticML → (converter) → IR
openness-cli import  <scratch-project> <files>    # IR → SimaticML → TIA
openness-cli compile <scratch-project>            # returns diagnostics; non-zero on error
converter to-ir|to-xml <files>
pytest tests/ ; dotnet test                       # PC-side tests
```

(Exact flags: `src/openness-cli/README.md` once built. Long operations: TIA project open is slow — be patient, don't kill and retry.)

## Workflow for logic generation (Stage S6+)

1. Confirm the request names a target block/network and the relevant equipment tags exist in `ir/<project>/`.
2. Select patterns from `patterns/` covering the request; map real tags to slots; type-check.
3. If >20% of the request needs freeform (non-pattern) rungs, say so and get explicit go-ahead before writing them.
4. Write IR → convert → import to scratch → compile → iterate until clean.
5. Present: IR diff + one-paragraph intent statement + compile evidence. Stop; the engineer takes it from there (`docs/11-review-workflow.md`).

## Workflow for modifying existing logic (Stage S7+)

Same as generation, plus: touch only the named network(s); run the untouched-network invariance check; the diff must show every changed network and prove the rest identical.

## Conventions

LAD you write or review follows `docs/06-lad-conventions.md` (cite rule IDs like C-101 in review findings). Comments: why, not what. Every network you create gets a title.

## Current stage

Check `docs/notes/stage-gates.md` for which roadmap stage is active. Do not perform capabilities from stages that haven't passed their gate — e.g. no logic generation while the project is still in S1–S5, even if asked casually; point to the roadmap instead.

## Environment notes

- TIA Portal V20, Openness API, S7-1200 G2 target. Windows engineering PC.
- Openness requires membership of the "Siemens TIA Openness" Windows group; first connect per Portal binary triggers a manual approval dialog inside TIA Portal — if a connect hangs, tell the engineer to check for that dialog.
- Only one Portal instance/session assumption: don't launch parallel Openness sessions.

## Data boundary

Only Green-tier content per `docs/13-data-boundary.md` (tooling, docs, the reference project) until the boundary decision is recorded. If you find identifying data in something you're asked to process, flag it before proceeding.
