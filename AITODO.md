# AITODO — working state / recovery doc

**Purpose:** this project's context can get interrupted (session limits, restarts). This doc is
the recovery point — read it first after any gap, before trusting your own memory of "what I was
doing." Keep it up to date as you work: update the checklist as items close, don't wait for a
clean stopping point. `docs/notes/stage-gates.md` is the permanent, narrative record of *closed*
work; this doc is the scratchpad for *in-flight* work only. When a task here finishes and is
documented/committed, delete it from this file rather than letting it accumulate.

## Recovery procedure (do this first)

1. `git status` / `git diff --stat` in the repo root — uncommitted changes are the ground truth of
   in-flight work, more reliable than any narrative.
2. Read `docs/notes/stage-gates.md`'s tail (most recent entries) — the authoritative record of
   what's actually *closed*, with dates and evidence.
3. Read this file's "Current task" section below.
4. Cross-check: does the code in the diff match what this doc claims is done? If not, trust the
   code/diff and fix this doc.

## Project stage

**S1 — Lossless round-trip, ACTIVE.** See `docs/notes/stage-gates.md` for full gate history.
Working through the approved `PlantAutoControl` round-trip plan (Phase 0 grounding done, Phase 1
in progress: get `PlantAutoControl`'s 8 dependency FBs importing+compiling standalone in
`SampleProject`). Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md
hard rule, gated by `docs/notes/stage-gates.md`.

## Recently closed (all committed)

- PLC tag table support (`TAGTABLE`) — task #127, `b2fe156`.
- `create-instance-db` command + `MotorStarter` compiles — task #122, `ef2ff1d`. 1st dependency FB.
- Anonymous-struct converter fix + `EquipmentControlSystem` compiles — `9ec648f`. 2nd FB.
- Deep-recursion + IR-text-serializer fixes + `ShredderControlSystem` compiles (bar one tag) —
  `e0fd86a`. 3rd FB.

## Current task: Phase 1 — `PlantAutoControl`'s 8 dependency FBs (task #124, in_progress)

**Status as of 2026-07-14, working at an explicitly usage-limit-conscious pace (project owner's
own instruction).** 5 of 8 FBs proven; 2 blocked on real, well-scoped gaps; 1 not yet attempted.
**Not yet committed** — the work below (`FilterUnitSystem`/`AirStar` success, `Gt` comparison,
bare-parameter member shape, `MotorFwdRevSystem`/`MotorVSDSystem` UDTs) needs a commit before anything else.

| FB | Status |
|---|---|
| `MotorStarter` (`MotorDOL`) | ✅ compiles clean (committed) |
| `EquipmentControlSystem` (`EquipmentControlSystem`) | ✅ compiles clean (committed) |
| `ShredderControlSystem` (`ShredderControlSystem`) | ✅ compiles clean bar `Clock_0.5Hz` (committed; hardware-config tag, deliberately left open) |
| `FilterUnitSystem` (`FilterUnitSystem`) | ✅ compiles clean, first try — self-contained, reuses `TypeDOL`/`MotorIOSet` |
| `AirStarSystem` (`AirStar`) | ✅ compiles clean — needed 5 more tag-table entries + reused `PlantControl.Test` |
| `MotorFwdRevSystem` (`MotorFwdRevSystem`) | ❌ blocked: `CycleDelayReset`, a standalone named `TON` instance (not a user-FB instance) — `create-instance-db --instance-of TON` fails (`"Block 'TON' does not exist"`, only resolves user FBs). Needs a different approach, not investigated further. |
| `MotorVSDSystem` (`MotorVSDSystem`) | ❌ blocked: calls `FC Scale`, which itself uses `Sub` (subtraction) — unsupported arithmetic instruction, comparable scope to the `Add`/`Mul` addition (S1 item 18). Not started. |
| `TomraControlSystem` (`TomraControlSystem`) | ⬜ not yet attempted |

**Two new converter capabilities landed getting `MotorVSDSystem` this far** (both real, both tested,
both needed regardless of the `Sub` blocker — not wasted work):
- **`Gt` (greater-than) comparison** — confirmed real via `FC Scale`, identical shape to the
  already-supported `Eq`/`Ge`/`Lt`/`Ne` family. `FlgNetParser`/`GraphReducer` updated, 4 new tests.
- **A fourth, minimal Input/Output/InOut member shape** — `FC Scale`'s own params have no
  `Remanence` and no `<AttributeList>` at all (previously a hard error). New `DbMember.
  IsBareParameter` flag. 1 new test.
- **272/272 converter tests pass** (up from 267 at the last commit). `openness-cli` unchanged
  (101/101, not re-run this round but no `openness-cli` code touched).

Full story for everything above: `docs/notes/stage-gates.md`, "Phase 1 continued: `FilterUnitSystem`/
`AirStar` compile clean, `Gt` + a fourth Input/Output shape added, two FBs blocked on real gaps".

**Sanitization maps built and kept** (`sanitization/`, gitignored): `FilterUnitSystem`, `AirStar`,
`Control` (real DB, → `PlantControl`), `MotorFwdRevSystem`, `MotorFwdRevIOSet1`, `MotorVSDSystem`, `TypeVSD`.
Reusable directly if/when `MotorFwdRevSystem`/`MotorVSDSystem` are picked back up.

**Scratch state**: `$CLAUDE_JOB_DIR/tmp/phase1_fbs/` holds sanitized/non-identifying artifacts only
— raw `JOB9002` exports (`*.fresh.xml`/`.fresh.ir` for each FB/UDT/DB, `Scale.fresh.xml`) should be
deleted once each item is fully closed out; `.sanitized.*` siblings and the combined
`MinimalTagTable54.raw.*` are safe to keep.

**Next steps, in order**:
1. Commit the current uncommitted work (see `git status` — converter fixes for `Gt`/bare-parameter,
   plus docs).
2. Either resume `MotorFwdRevSystem`/`MotorVSDSystem` (both need genuinely new capability — a
   system-instruction single-instance-DB creation path, and `Sub` arithmetic support respectively)
   or move to `TomraControlSystem` (not yet attempted, unknown scope) — project owner's call given
   remaining budget.
3. Once all 8 FBs (or as many as feasible) are proven: Phase 2 (bulk DB/tag-table closure for
   `PlantAutoControl`'s other ~26 dependency roots), Phase 3 (`PlantAutoControl` itself — its own 20
   call-site instance DBs, re-import, compile, re-export, `Normalizer` equivalence proof — the
   actual Layer 1 assertion this whole effort exists to establish).
