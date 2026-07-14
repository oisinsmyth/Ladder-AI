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
- `src/openness-cli/` (C#), `src/converter/` (C#, ADR-0002), `extract/` (Python, from S5), `tests/golden/` (round-trip harness) — PC-side tooling you may develop freely; normal software rules apply, hard rules above apply only to PLC logic.
- `docs/` — the design suite. When in doubt: `04-design-philosophy.md` for principles, `02-roadmap.md` for what's in scope *now*, `10-non-goals.md` for what never is.

## Commands

```
openness-cli list          <project>                                                     # enumerate blocks; F-/safety blocks flagged, never opened; --tagtables lists tag tables instead
openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) [--device <name>] --out <path>
openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...>
openness-cli compile       <project> [--device <name>] [--block <name> | --type <name>]   # non-zero exit on error
openness-cli delete        <project> --block <name> [--device <name>] --yes               # deletes a block (refuses safety; --yes required)
openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName>   # scaffolding: instance DB for an already-existing FB
openness-cli sanity-check  <project>                                       # block-consistency + compile health — run this first if export/import/compile misbehave
converter to-ir|to-xml <file>       # LAD: Contact/Coil/OR-merge/negation, comparisons (Eq/Ge/Lt/Ne/Gt/Le), TON/TONR/TOF, MOVE, WAND, CALL, SWAP, SCoil/RCoil,
                                    # MUL/CONVERT/ADD/SUB/DIV; DBs/UDTs/tag tables. Auto-detects block vs DB vs UDT vs tag-table content. Anything else outside
                                    # this slice is a correct hard error, not a bug — see docs/notes/stage-gates.md for exactly what's covered.
converter sanitize <file> --map <mapping.json> --out <path>   # real-project data → invented names, for scratch/live-verification use (docs/13-data-boundary.md)
dotnet test                         # PC-side tests (openness-cli, converter, tests/golden); pytest tests/ once extract/ (S5) exists
```

(Exact flags/behavior: `src/openness-cli/README.md`, `src/converter/README.md`. Long operations: TIA project open is slow — be patient, don't kill and retry.)

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
- Concurrent Portal sessions on *different* projects are safe (fixed 2026-07-13, hardened 2026-07-14): `openness-cli` never closes a project it didn't open itself — if the Portal process it attaches to already has an unrelated project open, it launches its own dedicated Portal instance instead of touching that one. It also never repurposes a human's own freshly-launched, still-empty Portal window (fixed 2026-07-14) — an empty process is only ever reused if `openness-cli` positively confirms it launched that exact process itself, in this or an earlier interrupted run (`LaunchedInstanceRegistry`); anything it has no record of creating is left alone and a dedicated fresh instance is launched instead. A human can run Portal manually — with or without a project already open — while `openness-cli` works a different one, at the same time, with no risk to either session. One residual caveat: the very first attach to an already-running process may still trigger the first-connect approval dialog above, even if that process turns out to be someone else's session — a one-time visual interruption only, no data risk (`Attach()` alone never opens/closes/saves anything). Two Openness sessions on the *same* project concurrently is still unsupported — that's a genuine single-writer-file constraint, not a policy choice. A dedicated stability audit (2026-07-14, `docs/notes/concurrent-portal-test-plan.md`) confirmed the concurrent-session design itself is not a source of instability — the "second instance sometimes won't connect" symptom correlates with stale Portal-process pileup, not concurrency; periodically check `tasklist` for `Siemens.Automation.Portal.exe` and close idle instances if they accumulate.
- PC-side code referencing `Siemens.Engineering.dll` must target `net48` — modern .NET (e.g. `net8.0-windows`) builds fine but fails at runtime. See `docs/notes/openness-quirks.md`.
- Block-consistency (`IsConsistent`) issues on the TIA side are real and can make `export`/`compile` behave confusingly without it being a tooling bug — run `openness-cli sanity-check <project>` before assuming otherwise.

## Data boundary

Only Green-tier content (tooling, docs, the reference project) by default. Amber-tier (real project data) is usable only under an explicit per-project approval recorded in `docs/13-data-boundary.md`'s "Per-project approvals" section — check its stated scope before use, and don't extend it yourself. If you find identifying data outside a recorded approval, flag it before proceeding.
