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
Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Recently closed (all committed)

- **The full `PlantAutoControl` round-trip plan — all three phases done, 2026-07-14.** Phase 1 (all 8
  dependency FBs compile clean), Phase 2 (all 26 real DB/tag-table roots sanitized/imported/
  compiled clean), Phase 3 (`PlantAutoControl` itself imports, compiles with 0 errors, and round-trips
  losslessly — `Normalizer.AreSemanticallyEquivalent` = true). Commits `e2fb301`, `a1f46d3`.
- **Full-cycle verification pass across every block in `SampleProject`, 2026-07-14.** Ran the
  complete export → `to-ir` → `to-xml` → import → compile → re-export cycle against all 49 blocks
  already in the project, one at a time. 5 more real converter bugs found and fixed (Sanitizer/
  `AccessNode` dotted-tag-name corruption, general Part/wire-endpoint document-order sort, IR-text
  `IsBareParameter` loss). 47 of 48 blocks now round-trip completely (`Main`/OB1 deferred — see
  below). Commit `60ac96f`.
- PLC tag table support (`TAGTABLE`) — task #127, `b2fe156`.
- `create-instance-db` command + `MotorStarter` compiles — task #122, `ef2ff1d`. 1st dependency FB.
- Anonymous-struct converter fix + `EquipmentControlSystem` compiles — `9ec648f`. 2nd FB.
- Deep-recursion + IR-text-serializer fixes + `ShredderControlSystem` compiles (bar one tag) —
  `e0fd86a`. 3rd FB.

## Current task: none in-flight

No uncommitted work as of 2026-07-14 (post full-cycle-verification-pass commit `60ac96f`). The
`PlantAutoControl` round-trip plan (`quirky-gathering-ripple.md`) is fully closed.

**Deliberately deferred, not a bug to chase:**
- `Main` (OB1) doesn't round-trip through the full cycle — each fix reveals another narrow
  OB-specific Interface-section quirk (`SecondaryType`/`Informative` fixed; `Output` section
  validity still open). `Main` is TIA's own auto-generated template block, not restricted content,
  and OB support was never a stated project goal. Deferred per the project owner's own call.
- A `tests/golden/Normalizer` gap: TIA can reassign a Part's own `UId` on import/compile (not just
  Wire/Access, as previously documented) — confirmed benign (matching Part count/kind, 0 compile
  errors) on 7 of 47 full-cycle blocks, but `Normalizer.AreSemanticallyEquivalent` reports a false
  "not equivalent" for them. Fixing this properly needs graph-based Part identity matching, not a
  simple content-key map the way `Access` already has — flagged as a real, well-scoped follow-on,
  not attempted yet.

**Possible next work** (no explicit instruction yet — ask before starting):
- Build the `Normalizer` Part-identity fix above.
- Grow the committed reference-project corpus to actually exercise the ~15 instruction-level
  constructs (`Mul`/`Convert`/`Sub`/`Div`/comparisons beyond `Eq`/`Ge`/`CALL`/`SWAP`/`SCoil`/
  `RCoil`/`WAND`/`Not`/`TONR`/`TOF`) that currently have no permanent, committed, live-TIA-verified
  regression coverage — see `docs/audit/2026-07-14-code-quality-and-docs-audit.md` for the full
  finding.
- S2 (read and explain) — the next roadmap stage once S1's gate review is formally signed off.

**Sanitization maps built and kept** (`sanitization/`, gitignored): all 8 dependency FBs' own maps,
plus `PlantAutoControl.map.json` itself and per-instance maps for all 26 DB/tag-table roots. Reusable
directly for any follow-on work against the same real project.
