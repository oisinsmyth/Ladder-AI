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

**S1 — Lossless round-trip, ACTIVE.** See `docs/notes/stage-gates.md` for full gate history. All
8 of `PlantAutoControl`'s dependency FBs are IR-complete (`to-ir → to-xml → to-ir` byte-identical) — no
known remaining converter-level gaps. `openness-cli` gained block deletion, confirmed
`import`-overwrite behavior, an Openness API surface survey (`b20cb37`), and safe concurrent
Portal sessions on different projects (`4841bf5`). Full story for each closed item:
`docs/notes/stage-gates.md`.

Do not perform S2+ capabilities (explain/comment/generate/modify) — CLAUDE.md hard rule, gated by
`docs/notes/stage-gates.md`.

## Current task 1: `openness-cli` — fix Portal-instance-pileup bug — DONE, NOT YET COMMITTED

**Status as of 2026-07-14: fully implemented, tested (73/73 openness-cli tests green; converter/
golden-harness unaffected at 244/11), live-verified against the real messy state it caused, and
documented (`docs/notes/openness-quirks.md`, `docs/notes/stage-gates.md`, `CHANGELOG.md` all
updated). Waiting on explicit "commit this" before committing.**

Found while attempting the full-cycle test below (task 2): the concurrent-session fix
(`4841bf5`, 2026-07-13) made `OpenProject()` always launch a fresh Portal instance when the
attached process was occupied by something else — safe, but a Bash-tool timeout killing a
mid-import CLI process (twice) left Portal stuck, and each retry's `Connect()` only checked one
arbitrary process before launching yet another. Process count grew 3 → 4 → 5.

Fixed in two rounds, both live-verified against the real pileup (not simulated): `OpenProject()`
now does two full passes across every running process — exact already-open match anywhere wins
first, only then is an empty process considered usable, only then does it fall back to a fresh
instance. A separate bug (forward-slash vs. backslash path comparison spuriously missing an
already-open project — caught live when the project owner noticed and asked about an unexpected
extra Portal window) was also found and fixed via `Path.GetFullPath()` canonicalization.

Cleaned up 2 confirmed-idle stray Portal processes with the project owner's explicit go-ahead
(taskkill on Portal processes correctly requires explicit confirmation — Claude Code's own safety
classifier flagged an earlier unverified kill attempt during this same investigation). Full story:
`docs/notes/openness-quirks.md` ("Follow-up, 2026-07-14"), `docs/notes/stage-gates.md`.

**Practical lesson, recorded for future invocations**: `--timeout-connect`/`--timeout-open` are
sequential, not parallel — their *sum* must stay comfortably under whatever outer timeout wraps
the call (e.g. the Bash tool's own 600s cap), or the outer kill fires first and risks leaving
Portal stuck server-side, defeating the point of a graceful internal timeout.

## Current task 2: full import+compile cycle test for `PlantAutoControl`'s dependency FBs — PAUSED, needs a decision

**Status as of 2026-07-14: paused mid-`MotorDOL`, blocked on a new, genuine finding — not a bug,
a real project-structure gap.**

Picked up per the project owner's own explicit ask, now that all 8 dependency FBs are
IR-complete: attempt a real `export → sanitize → import → compile` cycle for each into
`SampleProject`, to see whether any (especially the smaller/self-contained ones) can actually
compile there without `JOB9002`'s full tag table/FB library — previously only assumed blocked by
analogy to `PlantAutoControl` itself, never directly tested.

**Data-boundary check, done properly this time**: before importing real `JOB9002`-derived content
into `SampleProject` (a *persistent, git-tracked* project — different from transient scratch-temp
use), checked `docs/13-data-boundary.md`'s own "Per-project approvals" section — the recorded
JOB9002 approval doesn't cover this scope. Flagged to the project owner rather than proceeding
silently; they asked for sanitization first rather than approving raw import.

**`MotorDOL` sanitized and round-tripped cleanly**: confirmed it has zero external tag references
(fully self-contained FB, only touches its own interface members) — the best-case candidate.
Built `sanitization/MotorDOL.map.json` (reusing established names from
`sanitization/reference-project.map.json` where they already existed — `MotorDOL`→`MotorStarter`,
`TypeDOL`→`MotorIOSet`, etc. — inventing/identity-mapping the rest, since nothing in the block is
actually identifying). `converter sanitize` succeeded with zero missing-mapping errors on
the first real attempt; the sanitized XML round-trips through `to-ir`/`to-xml` cleanly.

**Import blocked by a genuine new finding**: `Data type "MotorIOSet" is unknown.` `MotorDOL`
declares its own `Static` section using a custom UDT (`TypeDOL`, sanitized to `MotorIOSet`) —
this UDT is a separate PLC data type, not part of the FB's own exported XML, and doesn't exist in
`SampleProject`. Even a *fully self-contained* FB (no external tag/DB/FB references at all) still
has this one external dependency. `openness-cli`/the converter have **no UDT export/import
support at all** — never built, never in scope so far (S1's own converter work has been
Contact/Coil/DB-member-level, not PLC-type-level).

**Options for the project owner to decide, not yet chosen**:
1. Scope UDT export/import as new work (a real new capability, not a quick fix — would need
   `openness-cli` support for the `SW.Types.PlcType` composition, entirely unbuilt, plus
   converter-side handling).
2. Try a different dependency FB that might not use any custom UDTs (unconfirmed which, if any,
   of the remaining 7 avoid this — would need the same grounding check `MotorDOL` just got).
3. Accept this as a known limitation and stop the full-cycle experiment here, having learned the
   real reason none of `PlantAutoControl`'s dependency FBs can currently compile standalone in
   `SampleProject` (not just missing tags — missing types too).

**Scratch state**: `sanitization/MotorDOL.map.json` is a real, reusable artifact (gitignored, not
committed) — kept. `$CLAUDE_JOB_DIR/tmp/autocontrol_fullcycle/` has the exported/sanitized/IR
files for `MotorDOL` — real `JOB9002`-derived content, must be deleted once this item is fully
resolved one way or another, not left lying around past the end of this working session.
