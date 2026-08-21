# Agent tasks — dispatch board (template)

One file per task, written so you can point an agent at a single file and it has everything it
needs — no other context required. **`DISPATCH-TEMPLATE.md`** is a ready-to-paste prompt for doing
exactly that. This folder is the live dispatch board for *queued* work; it is not a narrative log
(that's `docs/notes/stage-gates.md`) and not the in-flight scratchpad for whoever's actively
working (that's `AITODO.md`). A task file moves here once its ruling/design decision is settled
and it's ready to hand to an agent; when done, its outcome gets folded back into the permanent docs
and the task file itself gets deleted — don't let finished tasks accumulate here.

🛑 **DISPATCH SUSPENDED 2026-08-17** along with the staged development plan
(`docs/03-development-plan.md`) — **and the board was empty when it happened, so nothing is stranded
here.** Do not queue pipeline-development tasks while the suspension stands. This does not fence off
agent dispatch as such: `lad-coder` dispatch under hard rule 8 is unaffected, and live-job work is
dispatched the same way it always was.

**Status: empty.** The queue this folder was built for (nine tasks against the `test-project001`
scratch project, 2026-07-17) is fully closed and its task files removed — the pattern below is
what's worth keeping, not the specific tasks. Populate it again next time a batch of queued,
potentially-concurrent work needs dispatching.

**Every task file should assume multiple agents may be working on this project at the same time.**
Before touching anything, run `git status` and `git diff --stat` — don't trust that the repo is in
the state a task doc describes; another agent's uncommitted or committed work may have changed it
since the doc was written. If you find edits you don't recognize, stop and figure out whose they
are (check recent commits, check this README's queue table) before overwriting anything. This
turned out not to be theoretical: two concurrent sessions genuinely collided on this project twice
— once on a Portal-queue claim (worked as designed: the second session recognized the first's
in-progress claim and picked a different task), and once on an *architecture decision* (a gate-1
interface-change sign-off got asked of the owner twice in two concurrent sessions and got two
different answers, discovered only when the results were reconciled — see "Lessons learned" below).

## The hand-back contract: `evidence.json`

**A task that touched IR hands back `agent-tasks/<id>/evidence.json` alongside its prose report.**
It carries the raw `--json` of `preflight`, `diff --only` and a compile gate, each with its exit
code, plus a `converter ir-hash` for every file touched. Schema and field-by-field notes are in
`.claude/agents/lad-coder.md`.

The dispatcher verifies it with one command, which recomputes every hash itself:

```
python tools/check-agent-evidence.py agent-tasks/<id>/evidence.json
```

**Why this exists.** Hard rule 8 requires the dispatcher to verify the sub-agent's *actual* diff
and compile evidence rather than its summary. Before this, that meant re-reading the work — the
single largest source of the recheck-each-other cost, and the thing the 2026-08-21 context cut was
about. An agent can write anything in a summary; it cannot write an `ir-hash` that survives being
recomputed from the file on disk.

**What a green from it does and does not license.** It says the gates ran, they passed, and the
files are what the agent said they were. It says **nothing** about whether the logic is right —
that is the reviewer's job and the engineer's, and an automated opinion there would be exactly the
correlated check this project exists to avoid. Read it as "verified, now review", never as
"reviewed".

The evidence file is a hand-back artifact, not a permanent record: it goes when the task file goes.

## The shared-resource pattern: a Portal-backed scratch project

Any TIA Portal scratch project is a genuine single-writer resource — CLAUDE.md's environment notes:
two Openness/TIA Portal sessions on the *same* project concurrently is unsupported. Every task that
ends in "import + compile" against a shared scratch project contends for that one resource;
different projects are safe concurrently.

**What's actually exclusive:** only the `openness-cli import` + `openness-cli compile` step.
Drafting IR changes, and running `converter preflight` (fully static, no Portal contact), is safe
to do **at any time, in parallel**, by any number of agents — including while another agent holds
the Portal slot. Get your IR written and preflight-clean first; only queue for Portal once you're
actually ready to import.

### Portal queue protocol

1. Before running `openness-cli import`/`compile` against the shared project, open this README and
   check the queue table.
2. If any row shows `in-progress`, **do not open Portal.** Either wait, keep working your own
   task's IR-drafting/preflight phase, or work a `parallel-safe` task instead, and check back
   later.
3. If the row immediately above yours (lower **Order** number) is not yet `done`, wait for it —
   the order should reflect a rough dependency/risk sequence, not an arbitrary queue number.
4. When clear to proceed: edit the table, set your row's **Status** to `in-progress` and
   **Claimed by** to something identifying you (session id, task description — anything that lets
   a human tell agents apart; a generic label like "main session" isn't enough, see lessons below),
   save/commit that edit, *then* open Portal.
5. When your import+compile finishes (success, failure, or you're stopping for any reason), set
   **Status** to `done` or `blocked` (with a one-line reason) and clear **Claimed by**. This is
   what unblocks the next row — don't leave it `in-progress` after you've stopped working.
6. If you go to claim a row and find it's already `in-progress`, someone beat you to the edit —
   don't proceed, don't overwrite their claim. Tell the user.

This is a cooperative convention enforced by agents reading and editing a shared file, not a real
lock — it only works if every agent actually follows it. The user is the backstop: if Portal
sessions ever seem to be colliding, check `tasklist` for stray `Siemens.Automation.Portal.exe`
processes (CLAUDE.md's existing guidance) and check this table for a stale `in-progress` row.

### Queue

*(empty — no batch currently dispatched)*

| Order | Task | Status | Claimed by | Portal? |
|---|---|---|---|---|
| 1 | S6 req#1 hopper-blockage-alarm — alarm-live integration (CALL in FC_ControlMain + annunciate in FC_AlarmsMain) | done | — | yes |
| 2 | c603-n15 — C-603 fix, FB_ShredderSequencer NETWORK 15: import + compile gate against GenProject1 (test-project001) | done | — | yes |
| 3 | c603-n14 — C-603 fix wave 2, FB_ShredderSequencer NETWORKs 14+15: import + compile gate against GenProject1 (test-project001) | done | — | yes |
<!-- Row 3: import + compile gate PASSED 2026-08-21 16:40. sanity-check OVERALL HEALTHY, BLOCKS 36 INCONSISTENT 0, TYPES 7 INCONSISTENT 0, station-scope device compile Warning errors=0 warnings=1 (keyed on errors). Round-trip proof added this run: re-export from TIA -> to-ir -> diff against the edited IR = 15/15 networks identical, so TIA altered nothing on import. block-layout read BEFORE the import: Optimized; --set Standard deliberately NOT run (unrequested change, and this FB's interface member is RETAIN SETPOINT); gated --expect Optimized exit 0 after. Portal released. No download - the rig's deployed program now diverges from the project. simatic-ml/ deliberately not re-exported (ExportDriftDetectorTests pins this block). Not committed. -->
<!-- FINDING (second observation, same as row 2's): sanity-check run 1 exited 9 naming 5 inconsistent blocks and printed "a device compile does NOT clear these, and re-running sanity-check will not either." The five per-block compiles that followed EACH reported "No block was compiled. All blocks are up-to-date." with consistentAfterCompile=true, and sanity-check run 2 then reported INCONSISTENT: 0. So whatever cleared them, it was not those per-block compiles - the printed guidance line is now falsified twice by observation, on consecutive runs. Mechanism still unestablished; not asserting one. -->
<!-- Portal-process note: 4 Siemens.Automation.Portal processes at claim time - 2 in-use (one holding GenProject1, launched by an earlier openness-cli run; one holding a DIFFERENT project, TestEnviroment, i.e. safe concurrently) and 2 OS-ONLY invisible to Openness. Every connect this run was prompt; nothing killed. -->
<!-- openness-cli export UX note: `export --out <dir>` fails with exit 5 if the output directory ALREADY EXISTS ("Cannot export to the specified location because a directory with the name ... already exists"), and it writes the export as a FILE at that path, not a directory containing one. `mkdir -p` before exporting is exactly wrong. -->

<!-- Row 2: import + compile gate PASSED 2026-08-21. sanity-check OVERALL HEALTHY, BLOCKS 36 INCONSISTENT 0, TYPES 7 INCONSISTENT 0, station-scope device compile Warning errors=0 warnings=1 (the healthy reading, keyed on errors). compile-all --force compiled 43 objects, 0 withErrors, 0 stillInconsistent. block-layout: block was Optimized BEFORE the import, so --set Standard was deliberately NOT run (it would introduce an unrequested change and destroys retained data on next download; this FB is RETAIN SETPOINT) - gated --expect Optimized exit 0 instead. Portal released. No download. simatic-ml/ deliberately not re-exported. Not committed. -->
<!-- Portal-process note: the dispatch premise was five TIA Portal processes running with a ~15-minute connect-timeout risk. Measured at claim time: ZERO Siemens.Automation.Portal.exe processes. The seven Siemens processes present were background services (Help viewer, ETW tracing, telemetry connector, HMI runtime, SRM RDP utilities, TiaAdminNotifier), none of which holds the Openness token. Every connect in this run was prompt; no hang, no retry, nothing killed. -->
<!-- FINDING for whoever owns openness-cli: sanity-check run 1 exited 9 naming 4 inconsistent blocks and printed "a device compile does NOT clear these, and re-running sanity-check will not either." compile-all immediately after examined NOTHING (exit 14, compiled 0, entries []). sanity-check run 2 then reported INCONSISTENT: 0. The mechanism is unestablished and deliberately not asserted, but that printed guidance line is falsified by observation. -->

**Board is empty again — both rows above are closed. Delete rows and the `c603-n15` task folder once their outcomes are folded into the permanent docs, per this file's own "don't let finished tasks accumulate here."**
<!-- Row 1: build + C-605 + F1/F2/S1 fix + alarm-live integration all DONE 2026-07-19/20. Integration: FC_ControlMain NW8/9 (wiring+CALL), FC_AlarmsMain NW10 (%X9 alarm); invariance diff --only exit 0 both; FC3+FC4 block compile 0 errors. Alarm live in scan. Stop demand unwired (owner scope, documented later step). Not committed (coordinator handles). -->

**Parallel-safe (no Portal, no queue position — work anytime, alongside anything above):**

| Task | Status | Notes |
|---|---|---|

## Other shared resources, briefly

- **This README's queue table** — edited by every Portal-bound task at claim/release time; two
  agents editing it at the exact same moment could race. Low probability at human-paced dispatch;
  if you hit a merge conflict here, resolve by re-reading the current state, not by force-pushing
  your version.
- **Whatever permanent docs the tasks fold results into** (rule docs, requirements registers,
  stage-gates-style narrative logs) — if two Portal-queue tasks finish close together, their edits
  should still land fine if they touch different sections, but re-read the file immediately before
  editing it, don't assume it still matches what your task doc quoted.
- **Working tree / uncommitted changes** — this project does not mandate git worktrees for this
  kind of work, so by default every agent shares the same checkout. Commit your own change (or at
  minimum keep `git status` clean of anything but your own files) before considering a task done,
  so the next agent isn't looking at your half-finished edit.

## Lessons learned (kept from the first real run, 2026-07-17)

- **A generic "Claimed by" label defeats the whole point of claiming.** Two of the claims in the
  first run both said "main session, 2026-07-17" — indistinguishable from each other. The
  resulting collision wasn't caught by the protocol; it was caught by chance when re-reading git
  log. Use something that actually disambiguates (a real session/agent id, not a role description).
- **The Portal-queue protocol held up under real concurrency.** A second session ran into a queue
  row already claimed and correctly worked a different task instead of forcing through — the
  mechanism worked exactly as designed for the resource it was built to protect.
- **The protocol didn't cover the resource that actually caused the real conflict.** Portal access
  was never double-claimed; an *architecture sign-off* was. Two sessions independently ran the same
  gate-1 mini-manifest for the same interface change and got two different answers from the owner,
  because nothing forced either session to check whether that specific decision was already
  in flight elsewhere. If this pattern gets reused, decisions that need a one-time human sign-off
  need the same claim/release discipline as Portal access does — not just code-touching work.
- **A proven workaround can outlive the reason you needed it.** A converter limitation (adding a
  statement to a network that already has real sidecar data from a prior export) looked like a hard
  blocker on paper; a whole-file sidecar-strip-and-`--synthesize` cycle worked every time it was
  tried, at the cost of a larger diff (full sidecar regeneration) per use. Worth deciding
  separately whether that's good enough permanently or the underlying gap should get a real fix —
  don't let "there's a workaround" quietly become "there's no gap."
- **Killing a stale worktree's process before removing it is sometimes necessary, and that's fine
  to do once confirmed.** A worktree directory that won't delete ("device or resource busy") on
  Windows usually means a live process still has it open, not filesystem corruption — check with
  `Get-CimInstance Win32_Process` before assuming you need to force anything.
