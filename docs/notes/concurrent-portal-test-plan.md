# Test plan — concurrent TIA Portal sessions, stability audit

**Why this exists:** the "safe concurrent Portal sessions" feature (`4841bf5`, 2026-07-13) required
an immediate follow-up pileup fix (`8042648`, 2026-07-14) the same day it was built, and the
"second Portal instance sometimes won't connect at all" symptom
(`docs/notes/openness-quirks.md`) has now recurred **twice** in one session, with two different
resolutions (patient retry once; killing 5 stale processes once). That pattern is reason enough to
stop trusting "it probably just needs a retry" and actually characterize what's going on, before
building anything further on top of this code path. This plan was requested explicitly by the
project owner after closing all Portal instances to get a clean baseline.

**How to use this doc:** work through phases in order. Each test has a precondition, an action
(marked **[CLAUDE]** or **[USER]** for who does it), an expected result, and a pass/fail line to
fill in. Stop and diagnose on any fail — don't push forward past a failed test assuming it's
unrelated. Check `tasklist` (`Siemens.Automation.Portal.exe`) before and after every test; record
the count. Never kill a Portal process without saying which PID(s) and getting explicit
confirmation first — same discipline as the rest of this project.

**Baseline confirmed, 2026-07-14: 0 Portal processes running** (project owner closed all
instances via taskbar immediately before this plan was written).

---

## Phase 0 — single-instance sanity (no concurrency at all)

Establishes that the basic connect/attach/open path still works cleanly on its own, before any
concurrency is introduced. If anything in Phase 0 is flaky, that's a more fundamental problem than
the concurrent-session feature itself.

- **T0.1 — cold open from zero processes.** [CLAUDE] `openness-cli list` against `SampleProject`'s
  full `.ap20` path, from a confirmed-zero process count. Expected: exactly one new
  `Siemens.Automation.Portal.exe` process appears, project opens, all ~10 blocks + `MotorIOSet`
  type-bearing state listed, no errors. Record: time taken, process count after (expect 1).
  **Pass/fail:** ___

- **T0.2 — immediate reuse, same process.** [CLAUDE] Run a second command (`sanity-check`) against
  the same project immediately after T0.1, while that process is still up. Expected: attaches to
  the *existing* process (no new one spawned), completes quickly. Record process count after
  (expect still 1). **Pass/fail:** ___

- **T0.3 — reuse by project name, not path.** [CLAUDE] Run `list SampleProject` (bare name, not the
  full path) — exercises `FindAlreadyOpenProject`'s `Name`-based match rather than the
  `PathsMatch` path-based one. Expected: same reuse, no new process. **Pass/fail:** ___

- **T0.4 — a mutating op persists (Bug #3 regression check).** [CLAUDE] `compile --type
  MotorIOSet`. Expected: `STATE: Success`, and — this is the part to actually verify, not assume —
  the `.ap20` file's own last-modified timestamp changes (confirms `Save()` really ran). Record the
  file's `Last Write Time` before and after. **Pass/fail:** ___

---

## Phase 1 — deliberate race: does the CLI ever "steal" a human's own freshly-opened, still-empty
Portal window?

**This is a real, previously-untested edge case, not a hypothetical.** `OpenProject`'s own
two-pass search (pass 2: "if a running process has *nothing* open, it's fair game") does not
distinguish between "an empty process this tool is free to use" and "a human's own Portal window
that they just launched and are about to open something into themselves." If a human launches
Portal and hasn't clicked "open project" yet, and the CLI runs at that exact moment, today's logic
would happily use *that* window to open its own target into — which the human would then see as a
surprising "wait, why is a different project open in my window?" This has never been deliberately
tested; every past concurrent-session test opened the human's project *before* running the CLI.

- **T1.1 — race against an empty human window.** [USER] Launch TIA Portal manually (double-click
  the exe, or however you'd normally start it), but **do not open any project** — leave it at the
  start screen. [CLAUDE] While that empty window is up, run `list` against `SampleProject`.
  Expected (per today's actual code): the CLI's search finds your empty process and opens
  `SampleProject` **into it** — meaning your own Portal window would visibly change to show
  `SampleProject` instead of the blank start screen you left it at. **[USER] confirm what you
  actually see.** If this happens, it's arguably a bug (or at least a surprising behavior worth a
  design decision) — flag it rather than treat "worked as coded" as automatically "worked
  correctly."

  **RESULT, 2026-07-14: REPRODUCED, exactly as predicted.** User manually launched a fresh,
  empty Portal instance (1 process, PID 26068 — notably *one* process for a manual double-click
  launch, vs. the ~2-process pattern seen for an Openness-launched instance in Phase 0, a real
  difference worth keeping in mind). `list SampleProject` ran while it sat empty; afterward, PID
  26068's own memory usage jumped substantially (~1.2GB → ~1.58GB) and exactly one new helper
  process appeared (27116) — matching the "one logical instance ≈ 2 processes" pattern, not a
  separate second instance. User confirmed directly: their own manually-opened Portal window now
  shows `SampleProject` open, not the empty start screen they left it at. **The CLI used the
  user's own window for its own purposes, without asking, without any visible warning.**
  **Pass/fail: FAIL** (confirms the real, previously-untested edge case the plan called out) —
  needs a design decision (see discussion in chat) before this is closed out.

  **Fix applied, 2026-07-14** (`src/openness-cli/OpennessCli/Openness/OpennessGateway.cs`):
  `Connect()` now records whether it had to launch a brand new instance (`_connectLaunchedFreshInstance`) —
  the one case where "this process is empty" can be trusted, because this run created it. `OpenProject()`'s
  old second pass ("any discovered empty process is fair game") was removed entirely; a discovered
  pre-existing empty process is never reused again, only ever a fresh dedicated instance. Built,
  79/79 openness-cli tests still pass (this logic has zero unit-test coverage either way — verified
  live only, confirmed before making the change).

  **RETEST, 2026-07-14: PASS.** User closed the repurposed window, opened a fresh empty one (PID
  28768, confirmed via `tasklist`). `list SampleProject` was run against it — first attempt hit a
  genuine first-connect approval dialog on the user's screen (confirmed by the user directly, not
  assumed), cleared it, retried: succeeded in 29s, and this time PID 28768's memory usage was
  unchanged (noise-level, ~1MB) and **two new processes** appeared instead (matching the "one
  logical instance ≈ 2 processes" pattern) — a separate, dedicated instance was launched for
  `SampleProject`. User confirmed directly on screen: their own empty window was untouched, still
  at the start screen. **Fix confirmed working, not just reasoned through.**

  **Known, accepted tradeoff going forward**: repeated invocations that never find an exact
  already-open match now always launch a fresh dedicated instance rather than opportunistically
  reusing some leftover empty one — meaning more Portal processes can accumulate over a long
  session of repeated CLI calls (like this very testing session) than under the old behavior. This
  is a deliberate cost of never risking a human's own window; worth keeping an eye on in Phase 4's
  pileup-focused tests.

---

## Phase 2 — genuine two-party concurrency, different projects (the feature as originally designed)

The original 2026-07-13 verification simulated both sides as one operator. This phase is the first
time it's tested with an actual human on one side and the CLI on the other, live, together.

- **T2.1 — human opens a different project first, then CLI runs.** [USER] Launch Portal manually,
  open some project that is **not** `SampleProject` (the `JOB9002` scratch copy is fine, or any other
  project you have handy) — a real project open, not just the start screen. [CLAUDE] Once you
  confirm it's open, run `list SampleProject`. Expected: a **second, independent** Portal process
  launches for `SampleProject`; your own window is completely untouched (still showing your
  project, unsaved state if any intact). Record process count (expect 2 total).

  **RESULT, 2026-07-14: PASS.** User had `JOB9002` scratch copy open (2 processes, PID 28768 +
  helper 22752). `list SampleProject` ran successfully in 29s; PID 28768/22752's memory was
  unchanged (untouched), and 2 new processes appeared (29020, 18480) for a separate, dedicated
  `SampleProject` instance — matching the "1 logical instance ≈ 2 processes" pattern seen
  throughout this plan. User confirmed directly on screen: `JOB9002` window unaffected, no dialogs,
  no changes. **Pass/fail: PASS.**

- **T2.2 — CLI mutates its own project; human's project stays untouched.** [CLAUDE] With both
  instances still up from T2.1, run `compile --type MotorIOSet` against `SampleProject`. [USER]
  Watch your own Portal window throughout — confirm nothing happens on your screen (no dialog, no
  forced save, no reload).

  **RESULT, 2026-07-14: PASS.** Compile succeeded (2s, `STATE: Success`). Memory on the `JOB9002`
  side (PIDs 28768/22752) barely moved (+10MB, noise-level); the increase was entirely on the
  `SampleProject` side (29020/18480, +142MB, consistent with the compile). User confirmed directly:
  no change on the `JOB9002` window. **Pass/fail: PASS.**

- **T2.3 — human makes a real edit concurrently; confirm total isolation.** [USER] In your own
  still-open project, open a block, make some trivial change (don't save yet). [CLAUDE] Run
  another `list`/`compile` against `SampleProject` at the same time. [USER] Confirm your unsaved
  edit is still sitting there, untouched, after.

  **RESULT, 2026-07-14: PASS.** User added an unsaved comment ("Test Comment") to `AlarmMain`
  Network 1 in `JOB9002`. `list SampleProject` ran in ~1s (reused the existing instance, no new
  process). User confirmed directly: the unsaved comment was still there, untouched. **Pass/fail:
  PASS.**

- **T2.4 — first-connect dialog check.** Across T2.1–T2.3, note whether TIA's first-connect
  approval dialog ever appeared on either window. Expected: no (this machine's Portal binary has
  been trusted since earlier sessions), but worth confirming it stays true.

  **Observed:** no dialog during T2.1–T2.3 (all ran in 1–29s, no hang). One *did* appear earlier,
  during Phase 1's retest — but that was a genuinely fresh `Attach()` to a brand-new empty window,
  a different situation from T2.1–T2.3 which all reused already-known processes. Consistent with
  this being a first-connect-per-process-instance thing, not a per-command thing.

---

## Phase 3 — same-project concurrency (expected to correctly refuse, not corrupt)

- **T3.1 — human tries to open the exact project the CLI already has open.** [CLAUDE] Run `list`
  against `SampleProject` first (establishes the CLI's own session). [USER] While that's still up,
  manually try to open `SampleProject.ap20` yourself in a separate Portal launch. Expected: TIA
  itself refuses cleanly ("already opened by user... on computer...") — this is a genuine TIA-side
  single-writer constraint, not something the CLI works around. Confirm: no hang, no crash, no
  partial/corrupted state on either side.

  **RESULT, 2026-07-14: PASS.** User attempted to manually open the same `SampleProject.ap20` the
  CLI's own instance already had open — TIA refused the open cleanly. No hang, no crash reported.
  **Pass/fail: PASS.**

---

## Phase 4 — reproducing the actual instability (pileup + "won't connect at all")

This is the phase that directly targets what you're worried about. Goal: turn "sometimes it just
hangs" into either a reliable repro or a reasonably confident "it's fine now, here's why."

- **T4.1 — killed mid-operation client, does Portal get left stuck?** [CLAUDE] Start a `compile`
  and kill the `openness-cli.exe` process mid-flight (Ctrl+C or `taskkill` on the *client* process
  only, not Portal). [CLAUDE] Immediately after, run another `list` against the same project.
  Expected: the two-pass search either reuses the now-possibly-stuck Portal process cleanly, or (if
  genuinely wedged) times out gracefully rather than piling up a redundant instance. Record process
  count before/after this whole sequence.

  **Target changed from `JOB9002` to `SampleProject`, 2026-07-14**: `JOB9002` had an unsaved human edit
  ("Test Comment", `AlarmMain` Network 1, added for T2.3) sitting in it at this point, and the
  `Save()` fix means any mutating command that attaches to an already-open project now persists
  *everything* dirty in it — running a kill-mid-compile test against `JOB9002` risked silently
  force-saving that unsaved edit as a side effect. Flagged to the project owner directly rather
  than proceeding quietly; they chose `SampleProject` instead. **This is itself a real finding
  about the `Save()` fix worth carrying forward**, not just a test-logistics footnote: any
  mutating `openness-cli` command now has the power to force-persist a human's in-progress,
  unsaved work in whatever project it touches.

  **RESULT, 2026-07-14.** Two attempts were needed to actually catch the client mid-flight — the
  gap between separate tool-call invocations exceeded a fast (~1s) reused-process operation, and
  even a fresh-launch attempt (~24s) completed before a *separate* kill command landed. Redone as
  one atomic script (launch in background, sleep 3s, find the client's real OS PID via `tasklist`,
  `taskkill` it — all in one call, no inter-call gap): genuinely killed the client ~3.6s into what
  normally takes ~24-32s, confirmed by an empty output file (no partial result printed at all).

  **Core question — did Portal get left stuck: NO.** A follow-up `list` against the same project
  succeeded cleanly (~22s, no hang, no error). This is the main thing T4.1 exists to check, and it
  passes.

  **Real cost found: the leftover half-launched process is now permanently orphaned.** The retry
  did not reuse the half-launched process from the killed attempt (PID 25324) — correctly, since
  the project was never actually opened into it before the kill (pass 1's exact-match check
  correctly found nothing there) — but because the Phase 1 fix also refuses to treat *any*
  discovered empty process as fair game (not just a human's), the retry launched a **third**,
  fully separate instance instead of reusing it. PID 25324 sits there indefinitely — nothing will
  ever detect or reuse it again, only a human manually closing it will free that memory. This is
  the accepted tradeoff from the Phase 1 fix (`docs/notes/concurrent-portal-test-plan.md` T1.1)
  made concrete: it now also means the tool can't clean up after its *own* mid-flight failures,
  not just that it won't touch a human's window — the two cases are indistinguishable to the code
  as written, on purpose, since telling them apart isn't possible with the current Openness API
  surface (no process-identifying field exposed).

  **Pass/fail: PASS** (the thing this test targets — no hang/stuck-Portal — holds), **with a
  documented, real resource-accumulation cost** worth a decision (see chat discussion).

- **T4.2 — mixed-state search (the actual two-pass logic, exercised properly for once).**
  [USER+CLAUDE] Deliberately get into a state with: one Portal process holding `SampleProject`
  open, one holding a *different* project open, and one completely empty — three distinct
  processes, three distinct states (this exact combination has never been tested; every past test
  only had one non-trivial state at a time). [CLAUDE] Run `list SampleProject` once more. Expected:
  finds the exact process already holding `SampleProject` (pass 1's exact-match check), touches
  neither of the other two, spawns nothing new. **Pass/fail:** ___

- **T4.3 — repeat the "second instance won't connect" scenario, multiple trials.** [CLAUDE] With
  one Portal process already holding `SampleProject` open (idle, nothing else running), run `list`
  against a *different* project (forcing a fresh instance launch) **5 times in a row**, each from a
  clean single-extra-process state (clean up between trials — kill only the just-launched instance,
  confirmed by PID, between each trial). Record: connect time for each trial, and how many of the 5
  succeed within a reasonable timeout (say 90s) vs. hang. This directly quantifies whether the
  "sometimes won't connect at all" symptom is actually reproducible-on-demand or genuinely
  intermittent.

  **RESULTS, 2026-07-14 — 5/5 PASS, no hangs at all**, alternating `Project1`/`Project2` as the
  fresh-launch target, `JOB9002` + `SampleProject` held constant in the background throughout, each
  trial's own newly-spawned processes explicitly confirmed by PID and killed (with the project
  owner's specific go-ahead each time) before the next trial:

  | Trial | Target | Time | Result |
  |-------|----------|------|--------|
  | 1 | Project1 | 20s | PASS |
  | 2 | Project2 | 26s | PASS |
  | 3 | Project1 | 21s | PASS |
  | 4 | Project2 | 28s | PASS |
  | 5 | Project1 | 22s | PASS |

  **Conclusion — this is the single most important finding of this whole plan.** From a genuinely
  clean baseline (exactly 2 known-good logical instances running, nothing stale left over), forcing
  a fresh concurrent Portal launch was **100% reliable** across 5 trials — no hangs, no timeouts,
  consistent ~20-28s connect times throughout. This strongly suggests the "second instance won't
  connect at all" symptom observed twice earlier this session (once resolved by patient retry, once
  by killing 5 accumulated stray processes) correlates with **process pileup/staleness**, not with
  concurrency itself. When the Portal process list is clean, concurrent access is reliable; when
  cruft has accumulated (multiple orphaned/half-launched processes sitting around, as T4.1 itself
  demonstrated the fix can now produce over time), connection reliability appears to degrade. This
  reframes the practical guidance: the real risk isn't "concurrent Portal sessions are unstable,"
  it's "**don't let stray Portal processes accumulate** — clean them up periodically, especially
  after failed/interrupted `openness-cli` runs" — a maintenance discipline, not a code defect in
  the concurrent-session feature itself.

---

## Phase 5 — close the actual test-coverage gap (no live Portal needed, cheapest, highest value)

- **T5.1 — add real unit tests for `PathsMatch`/`FindAlreadyOpenProject`.** These are pure
  string/path logic (no COM/live-Portal dependency, unlike the rest of `OpennessGateway`) and
  currently have **zero** test coverage despite being exactly the code implicated in the
  path-format bug found live on 2026-07-14. [CLAUDE] Write tests covering: identical paths
  different slash direction, relative vs. absolute, name-match vs. path-match, a
  malformed/non-path identifier (shouldn't throw, should just report no match). This is
  implementation work, not a live walkthrough step — can happen independently of the rest of this
  plan.

  **DONE, 2026-07-14.** `FindAlreadyOpenProject` itself still can't be unit tested — it iterates
  `ProjectComposition`/`Project`, real COM-backed Siemens types with no fake/mock available, so it
  stays "verified live only" like the rest of `OpennessGateway`. `PathsMatch` is pure string/path
  logic though, genuinely testable — changed from `private` to `internal` with a new
  `[assembly: InternalsVisibleTo("OpennessCli.Tests")]` (`OpennessCli/AssemblyInfo.cs`), then 6 new
  tests added (`PathsMatchTests.cs`): identical paths, the exact real slash-direction bug case,
  case-insensitivity, different files, a relative-path-resolves-correctly case, and a malformed
  (NUL-containing) identifier confirmed to return `false` rather than throw. **85/85 openness-cli
  tests pass** (up from 79). **Status: DONE.**

---

## After each phase

- `tasklist //FI "IMAGENAME eq Siemens.Automation.Portal.exe"` — record count, compare to
  expectation.
- Note any discrepancy between expected and actual process count immediately, even if the
  functional test itself "passed" — a test that passes despite an unexpected extra process lying
  around is not actually a clean pass.
- Clean up deliberately-spawned test processes between phases only with explicit, specific,
  per-PID confirmation — never a blanket "kill everything."

## Open questions to resolve once this plan is run — ANSWERED, 2026-07-14

1. **Is the "won't connect at all" symptom actually reproducible, or tied to a precondition?**
   Tied to a precondition — 5/5 clean-baseline trials (T4.3) succeeded with no hangs at all. Both
   real occurrences this session happened with multiple stale processes already accumulated.
   Correlates with pileup, not with concurrency itself.
2. **Does Phase 1's "empty window theft" need a design fix?** Yes — fixed and retested (T1.1).
   `OpenProject()` no longer reuses any pre-existing empty process, only ever the exact instance
   `Connect()` itself just launched this run. Accepted cost: orphaned half-launched processes from
   killed clients (T4.1) are no longer cleaned up automatically either — same restriction, applied
   evenly, not just to human windows.
3. **Is the concurrent-session feature itself the cause of instability?** No, based on everything
   tested today. The feature behaves correctly and predictably under every scenario tried
   (different-project concurrency, same-project refusal, killed-mid-flight clients, repeated fresh
   launches). The actual risk is Portal-process accumulation over time — a maintenance/hygiene
   concern, not a defect in the concurrent-session design.

## T4.1's orphan-accumulation cost — CLOSED, 2026-07-14 (same day, not left as a follow-up)

The project owner asked for this closed out properly rather than left as an accepted cost. Real
fix, not a workaround:

**Root cause of why it was previously unfixable**: `OpenProject()`'s two-pass search couldn't tell
"an empty process this tool itself launched in an earlier, interrupted invocation" from "a human's
own freshly-launched empty window" — the 2026-07-10 API survey had recorded only `Attach()`/
`Dispose()` on `TiaPortalProcess`, missing every property on that type. A fresh, full re-reflection
found `TiaPortalProcess.Id` (the real OS process ID) and confirmed `TiaPortal.GetCurrentProcess()`
returns a `TiaPortalProcess` (the original survey had this wrong too, recorded as returning
`TiaPortal`) — both corrected in `docs/notes/openness-api-surface-v20.md`.

**Fix**: new `LaunchedInstanceRegistry` (`src/openness-cli/OpennessCli/Openness/
LaunchedInstanceRegistry.cs`) — a small JSON file persisting which OS process IDs this tool has
itself launched via `new TiaPortal(...)`, across separate invocations (each invocation is its own
short-lived process with no memory of earlier ones). Marked the moment a fresh instance is created
(`Connect()`'s own fresh launch, and `OpenProject()`'s own fallback launch), unmarked the moment
that same invocation successfully opens its target into it. `OpenProject()`'s search gained a new
pass, between "exact match" and "launch yet another fresh instance": an empty discovered process is
now reused **only if** the registry positively confirms this tool marked it itself — never guessed,
never extended to an unmarked process, so the Phase 1 human-window fix isn't reopened. Stale marks
(a marked PID whose process has since fully exited) are pruned automatically on read. 4 new unit
tests (`LaunchedInstanceRegistryTests.cs`, pure file/PID logic, no COM dependency) — **89/89
openness-cli tests pass** (up from 79 at the start of this session's Portal-stability work).

**Real, honest complication found while verifying this live**: killing the client process
(`openness-cli.exe`) does **not** always abort an in-flight `new TiaPortal(...)` call — if the
underlying OS-level Portal launch has already been issued by the time of the kill, it can continue
and complete asynchronously afterward (including calling `Projects.Open()`, if that line had
already been reached), entirely independent of whether the .NET client that started it is still
alive. Confirmed directly: a `Project1` open that appeared to fail (client killed, empty output)
turned out, on a later check, to have completed successfully in the background regardless. This
made externally timing a kill to land in the exact narrow window ("marked, but before this tool's
own `Projects.Open()` call is issued") impractical via bash-level process polling (sub-second
precision needed, tool-call latency exceeds it). **Verified the actual fix a different way
instead**: launched a genuinely empty Portal instance directly (bypassing Openness entirely,
`Siemens.Automation.Portal.exe` double-clicked equivalent), marked its real PID directly via the
registry API (simulating "this tool launched it, in an earlier interrupted run" — the exact state a
real kill-timing race would produce), then ran `list` against an unopened project and confirmed:
the marked empty process's memory jumped (+497MB, project opened into it), no new instance was
launched, and the registry correctly went back to `[]` afterward. **This directly proves the
recognize → reuse → unmark cycle works**, even though reproducing the originating kill-race
externally with precision wasn't practical.

**Bottom line**: the orphan-accumulation cost identified in T4.1 is closed, not merely documented.
A client killed at the right moment can still occasionally get lucky/unlucky with the underlying
async Portal-side completion (a genuine Windows/COM-level behavior outside this tool's control, not
something further code here can prevent) — but any orphan that *does* stay genuinely empty is now
recognized and reused by the next invocation, rather than accumulating forever.

## Other follow-up items surfaced by this audit, not yet acted on

- **`docs/notes/openness-quirks.md`** needs a dated follow-up note tying its existing "second
  Portal instance sometimes won't connect" entry to this whole audit's findings — not yet written
  as of this point in the plan (see chat for the drafted content, being finalized alongside
  `stage-gates.md`).
- **CLAUDE.md's environment note** on concurrent Portal sessions could mention the empty-window
  fix, the orphan-recognition fix, and pileup-hygiene guidance — not yet updated as of this point.
- All of the above, plus this whole plan's results, should be folded into
  `docs/notes/stage-gates.md` as a dated entry once the project owner is ready to commit this
  pass's code changes — not yet committed.
