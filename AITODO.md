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
Confirmed closed so far within S1: walking skeleton (Contact/Coil), OR-merge (branches are
recursive chains — multi-Contact, nested, comparison-as-branch, all live-verified) + negated
contacts, Instance DB + structured members, DB round-trip, TON/TONR/TOF (all instance scopes,
live-proven), comparisons (Eq/Ge/Lt/Ne), MOVE (fan-out taps + telescoping dedup), WAND (bitwise
word AND; closes out the AND-merge open item as **confirmed non-existent**), `Not` — standalone
boolean inverter, `CALL` — FB/FC block calls (incl. FC calls with no `<Instance>`), `SCoil`/
`RCoil` — set/reset coils, network/block-level `Title`, `MUL`/`CONVERT`/`ADD`/`SWAP` (arithmetic
incl. ENO-chaining), FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`,
committed `500dc68`), the `Mul`/`SrcType` fix (committed `eaf93b0`), `Access
Scope="LocalConstant"` (committed `55b5cb2`), `Ne` (not-equal comparison, committed `70fbfbc`),
`TOF` (off-delay timer, committed `3f66880`), `CALL` without `<Instance>` (a real FC call,
committed `32e5228`), `SWAP` (byte-swap box instruction, committed `2c61b0b`). `FC PlantAutoControl`
now converts as a whole block (`to-ir → to-xml → to-ir` byte-identical) — first real site
block this session to fully round-trip; **all 8 of its dependency FBs** now do too — **no known
remaining gaps in `PlantAutoControl`'s dependency FBs.** `openness-cli` gained block deletion,
confirmed `import`-overwrite behavior, and an Openness API surface survey (committed `b20cb37`).
Full story for each closed item: `docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task: `openness-cli` — safe concurrent Portal sessions on different projects — implementation + live verification DONE, NOT YET COMMITTED

**Status as of 2026-07-13: fully implemented, tested (73/73 openness-cli tests green; converter/
golden-harness unaffected at 244/11), live-verified for real (two genuinely independent TIA
Portal processes running concurrently, not simulated), and documented (`CLAUDE.md`,
`src/openness-cli/README.md`, `docs/notes/openness-quirks.md`, `docs/notes/stage-gates.md`,
`CHANGELOG.md` all updated this pass). Waiting on explicit "commit this" from the project owner
before committing** — per this session's established discipline, never auto-commit.

**Picked up per the project owner's own explicit ask**, immediately after the delete/update/
survey item: they need to do their own manual PLC engineering in TIA Portal tomorrow, on a
**different** project than whatever `openness-cli` is working on, and want work here to continue
concurrently. CLAUDE.md's own environment notes previously said "Only one Portal instance/session
assumption: don't launch parallel Openness sessions" — investigating turned that from a policy
note into a real, fixable code issue. Went through a full formal plan given the stakes
(foundational connection code every subcommand depends on).

**The real gap found, independent of tomorrow's specific need**: `OpennessGateway.OpenProject()`
used to call `Save()` + `Close()` on **whatever project was already open** in the attached Portal
process before opening its own target (built 2026-07-10 — safe at the time, since nothing else
was ever running Portal concurrently with this tool). The moment a human runs Portal manually, on
a different project, at the same time, this tool could attach to *their* process and silently
close *their* live project to make room for its own — a real risk to a live engineering session.

**Fix**: `OpenProject()` no longer force-closes anything it didn't open itself. Target already
open in the attached process → reuse (unchanged, the common solo-operator convenience). Nothing
open there → open directly (unchanged). A **different** project open → leave it alone entirely,
launch a dedicated `new TiaPortal(TiaPortalMode.WithUserInterface)`, open the target there
instead. No new CLI flag — derived structurally from what's actually open where, not from
guessing whose process is whose. `CloseAnyOtherOpenProject` (the old force-close helper) deleted
as dead code, its only caller gone.

**Live-verified, 2026-07-13 — the real thing, not simulated**: launched TIA Portal directly
(`Siemens.Automation.Portal.exe`, bypassing Openness entirely) with `SampleProject` open, to
genuinely mimic an independent human session (deliberately not opened via `openness-cli` itself,
since its own exit-time `Dispose()` behavior was one of the ambiguities this fix depends on). Left
it running, then ran `openness-cli list` against `JOB9002` (a different project) while that session
stayed up. Result: `JOB9002` listed successfully (176 blocks) via its own freshly-launched Portal
instance — `tasklist` confirmed three new process IDs, the original three (`SampleProject`'s
session) untouched throughout. Re-queried `SampleProject` immediately after: identical 10-block
content, instant reconnect (proving it was never closed — a closed-then-reopened project wouldn't
reconnect instantly). Six Portal processes coexisted with zero interference. Cleaned up all
test-launched processes via `taskkill`; `git status --short` confirmed only expected code files
changed. All three suites green: 73 openness-cli, 244 converter, 11 golden-harness.

**Residual, non-fixable caveat, documented rather than built around**: `Connect()`'s very first
`Attach()` call, if it reaches a human's manually-launched process before `OpenProject()`
discovers it's occupied, can still trigger TIA's own one-time first-connect approval dialog on
their screen — `Attach()` alone never opens/closes/saves anything, so no data risk, purely a
one-time visual interruption; not avoidable with the current Openness API surface (no way to
inspect what's open in a process without attaching first). Didn't occur in this live test (V20
already trusted machine-wide from earlier sessions) — worth confirming stays true tomorrow.

**Still correctly unsupported**: two Openness sessions holding the exact *same* project open at
once — a genuine TIA-side single-writer-file constraint, not something this tool could relax.

**Final verification before presenting for commit — already done this pass**: all three test
suites re-run and confirmed green, `git status --short` confirmed only expected files changed (no
real restricted data touched at any point — the live test used `SampleProject`, Green-tier, and
`JOB9002` read-only via `list`). Ready to present for explicit commit approval.
