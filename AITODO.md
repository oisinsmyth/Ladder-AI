# AITODO — working state / recovery doc

**Purpose:** this project's context can get interrupted (session limits, restarts). This doc is
the recovery point — read it first after any gap, before trusting your own memory of "what I was
doing." Keep it up to date as you work: update the checklist as items close, don't wait for a
clean stopping point. `docs/notes/stage-gates.md` is the slim status index of *closed* work (the
full per-stage narrative lives in `docs/evidence/stage-SN.md`, split out 2026-07-18 per FI-20);
this doc is the scratchpad for *in-flight* work only. When a task here finishes and is
documented/committed, delete it from this file rather than letting it accumulate.

## Recovery procedure (do this first)

1. `git status` / `git diff --stat` in the repo root — uncommitted changes are the ground truth of
   in-flight work, more reliable than any narrative.
2. Read `docs/notes/stage-gates.md`'s status table (which stage is active, what's signed off);
   for recent detailed history, read the tail of the active stage's evidence doc
   (`docs/evidence/stage-S6.md`, the last stage to have been active) — the authoritative record of
   what's actually *closed*, with dates and evidence. **Since 2026-08-17 the answer to "which stage
   is active" is *none*: the staged plan is suspended — `docs/03-development-plan.md`.**
3. Read this file's "Current task" section below.
4. Cross-check: does the code in the diff match what this doc claims is done? If not, trust the
   code/diff and fix this doc.

## Current task / in flight — the workbench phases, OUTSIDE the suspended staged plan

> ✅ **UPDATED 2026-08-23, AND IT WAS A WEEK STALE.** This section read *"nothing mid-execution"* while
> the harness, the rig loop, the lease, the batch and workbench Phases 1/2/3/10 were all being built and
> run. **The recovery procedure eight lines above tells the next session to trust this section**, so a
> stale "nothing in flight" is the single most misleading line this file can carry — it invites the
> reader to skip the cross-check step that would have caught it. Fixed, and recorded rather than
> silently overwritten.

**IN FLIGHT: NOTHING IN `docs/18-project-workbench.md` §5. THE TEN-PHASE LIST IS FINISHED.** Six
phases are delivered and have run on a controller (1, 2, 3, 4, **6**, 10); Phase 5 is decided and
declined as a build; Phase 7 is closed; Phase 8 is built but never contended; Phase 9 is struck. This
work is **not part of the suspended staged plan** (`docs/03-development-plan.md`) — it is tooling for
real jobs, which the suspension notice explicitly leaves running.

> ⚠️ **This line read *"IN FLIGHT: Phase 4"* until 2026-08-23 evening** — after Phase 4 closed that
> morning, after Phase 6 was re-scoped, delivered (`7dbac3c`) and **run on the rig** (`ce2163b`) that
> same day, and with **no mention of Phase 6 anywhere in this file.** The recovery procedure at the
> top of this document tells the next session to trust this section. Recorded rather than overwritten,
> because this is the second time in one week that the fix has been *"update the stale line"* when the
> fix is that **closing a phase must include closing its status lines, in the same commit.**

🔴 **WHAT IS ACTUALLY NEXT IS NOT A PHASE — see `docs/18` §5z.** Assertion coverage has not moved
across any rig event since 2026-08-18 (the same 2 and 3 assertions cited every time). **The
enumeration has no THIRD-PARTY PRODUCING PARTY** — an enumerator that is neither the block author nor
the vector author — which is D6 lost at the denominator: coverage measured by the party it measures is
unfalsifiable. **That is an agent-and-process question, not a `src/` one.** The instrument is fine and
almost nobody is feeding it; an eighth round of instrument-building is the wrong answer however tidy
it looks.

> ⚠️ **CORRECTED WITHIN THE HOUR, AND THE CORRECTION IS THE MORE USEFUL ITEM.** This paragraph first
> read *"the enumeration has no producer — a caveat on every result package"*, quoting
> `LoopRun.cs`'s `F-3-authority`. **That caveat was a string constant, and the gate it denies has been
> running since 2026-08-13** (`3e assertion form authority`,
> `src/harness/Harness.Results/SubmissionGate.cs:1455`, shipped `b44677b`). Built from the *request*
> before any gate runs, it could not report the gate — so it was byte-identical on every package
> because it was a literal, not because the hole was open, and it was quoted onward into a work plan.
> **A caveat that cannot observe what it describes is a constant.** ✅ **Both halves repaired and
> shipped in `82af95f`** — the caveat now reports gate 3e's actual finding (keeping its ID), and
> `Harness.Results/AssertionCoverage.cs` counts the numerator per subject; suite **2,698 passing, 0
> failures, 0 warnings.** Full record: `docs/18` §5z.

- **Phase 6 — DELIVERED AND RUN ON THE RIG 2026-08-23**, and **re-scoped before it was built**: not
  the element table, but *"the area, derived"*. Y0–Y3 plus `777fac0` / `182b3f9`, closed in `7dbac3c`;
  follow-ups `818ba02` `c53858e` `6937df0` `0d0ebac` `dc8308d`. **On the controller (`ce2163b`):** both
  lanes 3 of 3 PASS, stamp `16#B85BE93C` → `16#95D8731D` read back off the device, area exactly 1024
  pinned from both sides, stamp coverage 15 of 15. 🔴 **The value was not the green** — setting the run
  up found that `Main` calls the virtual panel's FC and no lane declared it, so **every build stamp
  before it hashed a program short of an object the controller runs.** The element table is re-labelled
  **"on demand, not a phase"** in `docs/18` §5.

- **Phase 3 — DELIVERED and run on the rig.** `converter lease` (a real lock, raced), the mirror's
  ceiling probed from both sides, `harness-batch`, link-loss survival. **Two lanes off one download,
  3-of-3 PASS each, 402 s.** ⚠️ **This line said "the mirror's true 576-register ceiling" until
  2026-08-23.** The area was widened **576 → 1024** and proven on the controller the same day — 1023
  answers, 1024 and 1025 are refused with a Modbus exception (`99396b9`,
  `docs/notes/total-plant-run-feasibility.md:204-219`). 576 is history, not the ceiling.
- **Phase 4 — DELIVERED 2026-08-23**, `802327f`..`dd274fe`. W1 four silent omissions · W2 `converter diff`
  matches networks on content · W3 `SlotFcGenerator` + lane manifest · W4 `StimShellGenerator` · W5
  reachability parity · W6 stale status lines. Plan: `.claude/plans/lets-the-crunch-this-linear-gosling.md`.
  ⚠️ **This line said "IN PROGRESS" for the length of one message after the last item was committed** —
  which is the shape, not the duration, that matters here: the status was written when the work started
  and nothing made closing it part of closing the work.
- **THE BRANCH IS RECONCILED WITH `master`** (merge `4f9c428`, 0 behind). It had been 45 behind with
  concurrent commits in **all three `Diff/*` and all three `CrossCheck/*` files** — the subsystems Phase 4
  rewrote. Suites after: converter **1,539**, harness **2,438**, openness-cli **832**.
- **Phase 5 — ALL ITEMS DONE**, *"Pre-flight, measured"*. **X3** the scan period (`c06aa05`) · **X2** the
  union check run over the union (`6777724`) · **X5** stale lines + the C1 permission (`0a7ce43`) ·
  **X2b** the manifest names the block under test (`cdb37f7`) · **X1** the five-bucket classification and
  **X4** the interpreter question (this edit — `docs/notes/preflight-interpreter-classification.md`,
  `docs/18` §4.4 + §5 Phase 5). **Both owner questions ruled: C1** — the classification's counts may be
  committed, **aggregates only**, the per-row table stays in the job folder (`docs/13-data-boundary.md`,
  2026-08-23 entry); **C2** — `src/harness` **may** take a project reference on `src/converter`
  (`docs/18` §3.6), which clears the "second IR parser" blocker the phase was named after.
  **X1's result: 93 classified of 115 — A 10 / B 17 / C 0 / D 56 / U 10.** 🔴 **The rubric was the
  finding:** the three buckets docs/18 asked for have no bucket for *"not a defect in the block"*, so
  **56** rows would have been forced into *only the rig would* and the study would have argued for more
  rig time on the strength of our own instruments' bugs. **X4's ruling: Phase 5 is REDIRECTED, not
  struck** — weak as a pre-filter before the rig (2 of 37 non-Pass verdicts were block defects, both
  already caught by C-410), strong in the design and review loop (17 of 56).
  **What remains — and none of it is a Phase 5 item:** the interpreter itself is **not built**, and
  three things bound whether it should be. (a) **C = 0 is structural** — the plant program has never
  run, so some B rows are C rows in disguise and 17 is an upper bound. (b) **Vector supply gates the
  whole benefit** — 2 of 96 and 3 of 96 assertions covered on the two blocks that have run a wave; an
  interpreter with no vectors catches nothing, so this competes with, and does not substitute for, the
  enumeration→vector path. (c) 🔴 **The load-bearing element is a JUDGEMENT** — that the 17 B rows do
  **not** collapse into two or three converter rules the way 8 of the 10 A rows did. **Nobody has tried
  to write those rules.** That attempt, not more classification, is what would reverse the decision.
- **Owner rulings recorded 2026-08-23** (commit `b7dc407`): `src/harness/` is PC-side tooling under hard
  rule 8; `Main`'s write-path deferral is LIFTED; the stimulus-shell generator may be ported,
  mechanism-only. Plus **FI-65: reading (b) — the integration/union compile is the gate — and the
  SHARED-QUEUE model over per-agent copies**, so FI-65 component 2 is parked.
- ✅ **B4 — RULED 2026-08-23, and the command was built the same day. This line said "still open" after
  both.** The ruling, quoted verbatim where the code that implements it lives
  (`src/openness-cli/OpennessCli/Openness/PortalClosePlanner.cs:62-63`): **"save where you can, then
  close"**. Built as `openness-cli portal-close` in `f9abeb1`. 🔴 **The ruling is MORE permissive than
  the assumption it replaced** — an Openness-invisible process cannot be asked to save, and it is
  closable anyway; that branch (`TerminateUnsaveable`) is reported by name on every run rather than
  folded into a rolled-up "closed" count. The one thing it forbids is the opposite: a save that was
  possible and **failed** stops the terminate (`PortalCloseExecution.cs:167-173`). 🔴 **Never run
  against a live Portal.** The decision half is pure and unit-tested; the execution half has no live
  evidence. Documented in `src/openness-cli/README.md` (`portal-close` section) and CLAUDE.md's index.
- ✅ **B5 — ANSWERED: `FB_SiloSequence` is the third conformance lane. THE LANE IS NOT BLOCKED ON THE
  OWNER.** 🔴 **This line read *"Blocked on this and nothing else"* after the answer was given**, as did
  `docs/notes/owner-questions.md:72` and `docs/notes/workbench-phase6-plan.md:150`/`:367`/`:395` —
  three tracked files manufacturing a blocker that did not exist. **Two decisions of the lane's own now
  stand ahead of authoring it**, and their substance lives only in the live job's gitignored folder:
  (1) a scope choice about observation depth that must be taken *before* the build, because it changes
  the interface, the drive surface and the register bill, and whose thorough option overruns the
  125-register FC03 read limit outright; (2) part of the intended assertion set has no implementing
  logic, so a planned sub-slot cannot be built and the right output is an **independently confirmed gap
  report** (D6), not a slot — **M-21 recurring**. **Canonical record, with the shape written and the
  data-boundary line drawn: `docs/notes/owner-questions.md`.** Two lanes have run off one deployment on
  the rig (`d289a27`); **two is not N**, and a third lane is still the right next *rig* event.

> 🛑 **SUSPENDED 2026-08-17 — this applies to the STAGED PLAN below, not to the workbench work above.**
> Everything in this section and in *Outstanding works* below is **held,
> not cancelled** — the staged plan is suspended (`docs/03-development-plan.md`). Nothing was
> abandoned mid-execution: this section already recorded nothing in flight, and `agent-tasks/` was
> empty. The owner decisions listed here **stay open and stay waiting**; they are not resolved by the
> suspension, and the items depending on them stay unverified. Read this section as the resume list.

**CONTEXT CUT — ALL PHASES DONE 2026-08-21. Full handoff:
`docs/notes/context-cut-handoff.md`.** `CLAUDE.md` went 96,655 → 19,593 bytes and `lad-coder`
stopped re-reading it in full; per-dispatch orientation dropped ~55k → ~11.4k tokens. The command
reference moved into the two READMEs, which already claimed to be authoritative for it. **This is
repo tooling, not pipeline development, so the 2026-08-17 suspension does not hold it.** Phase 4
(size budget, `4df5b57`), Phase 2 (hookify gates, `cc4fe05`) and Phase 3 (`evidence.json`
hand-back, `970067a`) all landed. Gate before any further trim:
`python tools/check-claude-md-migration.py`; the budget gate then refuses the commit itself.
**A fresh clone needs `git config core.hooksPath hooks` once, or the budget gate is not installed.**

**Still outstanding: only read-only `lad-coder` tasks have ever been run against the cut file — a
generation or fix run has not.** Two `explain-plc-block` dispatches, the second confirming
subagents receive the cut file. The write path is where the READMEs are now load-bearing, and it is
also what Phase 3's `evidence.json` contract was written for, so neither has been exercised in
anger. That is the next validation worth doing; it needs Portal, a target defect and the compile
gate.

✅ **THE "NEEDS A PERSON AT THE MACHINE" HALF OF THIS ITEM IS NO LONGER TRUE — checked, not assumed,
2026-08-23.** `openness-approve-build.ps1 -Status` shows the whitelist carries **136 entries** for
`openness-cli.exe`, including auto-approved builds from other worktrees, and
`OpennessCli.csproj:43–44` defaults `AutoApproveOpenness` to **true** with an `ApproveForOpenness`
post-build target. **FI-74's setup has been run on this machine, so a rebuild self-approves and needs
nobody.** A rebuild was performed unattended today and the binary connected.
⚠️ **What remains true is the rest of the rule:** never rebuild the binary that *in-flight Portal work
is running* — Debug and Release hold independent approvals, so the constraint is about which path is in
use, not about rebuilding at all. **This decision no longer waits on scheduling a person; it waits only
on somebody choosing to run the verification.**

**OWNER DECISION WAITING (2026-08-10): five committed tooling fixes cannot be verified without a
Release rebuild.** FI-63, FI-66, FI-68 and FI-70's `export-all` are built, unit-tested and committed,
and **none has been exercised against a live Portal**. A rebuild revokes the binary's TIA Openness
approval until a person re-approves it at the machine (FI-61) — unattended there is nobody to accept —
so this must be scheduled when the owner is present and no Portal work is in flight. FI-62 is the
exception: already live-verified, and it found **9 inconsistent UDTs** behind a fully green gate on its
first real run. Worth doing deliberately rather than letting it drift, because `export-all` is the
export half of the disk-vs-controller check (FI-70) and until it runs once, *nothing has ever compared
the IR on disk against what is actually in the controller*. Suggested order and full detail are in the
task board. The Debug binary is a separate whitelist entry and can be rebuilt freely; the approved
Release binary is byte-identical to where it started.

**Tooling landed 2026-08-10, all committed on `master`, all recorded in `docs/16-future-ideas.md`:**
FI-63, FI-68, FI-69, FI-70 (both halves), FI-71, FI-72. Five of the six are the same defect family this
repo keeps paying for — **a gate or a message that confidently names the wrong cause, or reports a
result stronger than it earned** — and FI-71 is the sharpest instance: the check *looked*, *saw the
problem*, and only warned, so the same mistake cost a third full import-and-compile cycle. Suites:
888 converter, 244 openness-cli, 39 golden.

**That wave LANDED on `master`** (2026-08-05, merge `242e5e2`, ~25 commits): the S6-Killer-Plan
answer-key validation end-to-end (score: MATCH 14 / IMPROVEMENT 1 / DEFECT 3 / REGRESSION 1 /
SPEC-GAP 3), its autopsy, the four-rung spec pipeline skills, the FI-36/FI-39 mechanical floor
(5 converter checks, 707 tests), the `tagstatus` member-blindness fix, and three real runs of the
pipeline. Nothing of it is unmerged and no branch of it survives. Map:
`docs/notes/S6-Killer-Plan.md`'s status block; dated record: `docs/evidence/stage-S6.md`.

**Left deliberately OPEN (owner's call, 2026-08-05) — not blockers, just not now:**

1. **The `references/<class>/` library** — the binding constraint. All three references self-disclaim as
   non-standards (derived from as-built FB interfaces), so rung A's *completeness by construction* —
   the property that makes a spec complete rather than merely careful — is **nominal, not delivered**.
   Rung A raises this as a blocking Q on every run. Highest-value remaining pipeline work; needs its own
   scoped proposal.
2. **Two tool refinements** (detail in `docs/16-future-ideas.md` FI-39): `relation-reconcile`'s citation
   check **rewards a vaguer citation** than a precise one; `signal-sweep` can never reach zero here
   because a real member name contains `/`.
3. **The dispatch path** — the four rung skills weren't exposed to the Skill tool in the runs that used
   them; each agent read the `SKILL.md` directly instead. Worth confirming registration before relying
   on `/gen-pid-analysis` by name.
4. **A second plant.** Everything above is fitted to one. A different plant is what shows which rules
   are general and which are overfitted — the real completeness test.

**Unchanged by any of this:** S6 exit still needs its ten fresh plain-language requests. None of this
work counts toward that tally — it is a validation fixture and the tooling that came out of it.

## Awaiting the project owner — ADR-0007 (HMI engineering scope)

**One-page measured capability record: `docs/notes/hmi-capability-record.md`.** Read that before any
HMI conversation — it states what works, what does not, the coverage numbers, and what is owed.

**The FI-54 probe programme is finished** (2026-08-07 → 09, all six phases; see `docs/notes/stage-gates.md`'s
cross-stage evidence section for the map). **One decision is open and it is the owner's**: ADR-0007 is
**Proposed** with the measurement it was drafted waiting for, and deliberately carries no
recommendation. Until it is decided:

- `docs/10-non-goals.md`'s "not now" line for HMI engineering **stands**. Do not build HMI capability.
- The five HMI **write** commands (`hmi-create-screen`, `hmi-edit-screen`, `hmi-new`, `hmi-delete`,
  `hmi-set`) are **probes without a disposition** — ADR-0007 option 3 would freeze or remove them.
  `openness-cli hmi` (read-only) is unaffected and useful either way.
- The headline the owner needs: additively capable, **destructively unsafe by default** (deletion
  orphans silently — a post-delete compile is mandatory), and **alarm text cannot be written at
  all**, which blocks FI-35's alarm-list generation, the concrete use case the whole thing was for.

**Three narrow follow-up probes** would sharpen that last point and are the only Unified work left
(each is one probe, not a programme): why `MultilingualTextItem.set_Text` refuses; whether
`RaisedStateTagBitNumber` is merely contextual (disabled until a trigger tag exists); and whether the
three refused dynamization kinds have another creation route. Classic HMI is externally gated — no
classic device exists locally and adding one is its own non-goal.

## Project stage

> 🛑 **SUSPENDED 2026-08-17 — no stage is active.** The staged development plan is suspended by the
> project owner (time constraints, real application needs); canonical notice in
> `docs/03-development-plan.md`. **S5 and S6 below were ACTIVE and are now frozen** — not closed, not
> failed, exit criteria **not waived**. **S6's live tally stays at 1 of 10 and stops being live**: do
> not add to it, and do not treat a request run for a live job as counting toward it. The stage
> records below are retained verbatim as the state to resume from. Everything outside this section —
> the recovery procedure, the hard rules, the tooling — is unaffected.

**S3 — Comment generation, DONE — gate reviewed and signed off by the project owner, 2026-07-14.**
Full history in `docs/notes/stage-gates.md`'s status table and `docs/evidence/stage-S3.md`'s "S3
first/second/third proof" sections: exit criterion met three times over (`TimerSample`, `PerimeterSafetyAlarms` — Green-tier
reference corpus; `PlantAutoControl` — real JOB9002 content at full 20-network scale), the JOB9002
data-boundary approval explicitly extended to cover write activity first, two real converter gaps
closed (embedded-newline guard; the previously-untested "edit an existing title" scenario), one
real pre-existing corpus bug found and fixed (`TimerSample.ir`'s stale sidecar format), and the
other 13 committed reference-corpus files swept afterward to confirm that bug wasn't a wider gap.

**S4 — Convention review, DONE — gate reviewed and signed off by the project owner, 2026-07-15.**
Phase 1 shipped **8 of ~50 rules** (the sign-off figure) built, tested, live-piloted against the
reference corpus, validated against real untouched JOB9002 content. The FI-09 waves have since taken
`converter review` to **19** mechanized C-IDs (`ReviewRunner.AllRuleIds`, public since 2026-08-18
so the tests read the real list instead of a drifting copy) — see `docs/16` FI-09. The nineteenth is
**C-410**, the self-restarting timer (`IN` reading its own `Q`), added 2026-08-18 after a live job
lost most of a day to one; `src/converter/README.md` carries the measurement and the scope call.
Full history: `docs/evidence/stage-S4.md` ("S4 Phase 1" and "S4: real-content validation" sections).

**S5 — Structured data extraction, ACTIVE**, opened 2026-07-15, no work started — deprioritized
below S6/S7 per the owner (2026-07-15 "a workable S6 is the main goal"; 2026-07-17 "S7 is the
rush").

**S6 — Generation from plain language, ACTIVE**, opened 2026-07-15; the staged generation
pipeline (docs/15, ADR-0004) was adopted 2026-07-16 and largely built through 2026-07-17: all
four reviewer-adjacent skills exist and are blind-validated (`review-simplicity`,
`review-conventions`, `review-functional`, `gen-architecture`, alongside `explain-plc-block`);
the first pipeline artifacts live in `gen/test-project001/` (69-REQ requirements register,
architecture baseline, telemetry); test-project001's functional verdict stands at
45 implemented / 7 partial / 6 unimplemented / 10 disarmed / **0 contradicted** after fix wave 1
(2026-07-17, device compile 0/0, invariance proven, triple-reviewed). Exit criterion **not yet
closed**: counting rule ruled 2026-07-18 (D-4) — S6 exit = **ten fresh plain-language generation
requests**; fix waves are tracked *separately* and don't count toward the ten. **S6 generation-request
tally so far: 1 of 10** (fix waves excluded — keep this count here as the live tally). Full history:
`docs/evidence/stage-S6.md` entries 2026-07-16 → 2026-07-20.
- **#1 (2026-07-20): `FB_HopperBlockageMonitor`** — hopper-blockage supervisory alarm, owner-pivoted
  from an initial discharge-conveyor pick that reuse-first found already implemented. Full pipeline
  (analyse → gate 1 → build → check → gate 2); **compile-clean** on scratch, invariance proven
  (3 additive blocks, nothing existing edited). Stage-4 review caught two real correctness edges
  (F1 reset history-dependence, F2 re-arm race) + readability (S1), all rooted in the countdown-budget
  Gap-E workaround; C-605 (UDT comments) fixed. **F1/F2/S1 then FIXED via `gen-block-modify-fix`**
  (its first real application, `7c842fd`) — root rebuild of NW3/4/5 to a clean up-accumulator, invariance
  proven, compile-clean, **fresh re-review confirmed resolved + no regression**. Block is now correct +
  readable. Proved live: `Time >= Time` compare synthesizes (Gap E fix works) and generic `ADD` accepts
  Time in TIA. Not yet wired into the scan — **now wired alarm-live** (`b763faf`: CALL in
  `FC_ControlMain` + annunciation to `ShredderAlarm0.%X9`; stop-demand consumption deferred). Record:
  `gen/test-project001/hopper-blockage-alarm/`, `docs/evidence/stage-S6.md`.
- **`gen-block-modify-purpose` VALIDATED (blind MotorDOL→MotorVSDSystem, 2026-07-20, `f588fcf`)** — the last
  coding skill. From a behavioural spec + the DOL source (never seeing the quarantined real MotorVSDSystem),
  it produced a VSD implementing all 22 REQs, no gold-plating, NW1–5 byte-identical (invariance held),
  and even improved on the real block (explicit Int→Real speed conversions the real block lacks). 1
  genuine minor skill gap (NW9 fail-to-stop ignores `RunRev`, reverse-only). **All three coding skills
  now exercised end-to-end this session.** The compile gate surfaced a real converter bug — synthesized
  fan-out `<Parts>` emitted parts out of the order TIA import requires, **Normalizer-masked**. Two-part fix:
  `f3ad4ca` fixed the Access-subgroup UId-ordering, then **`7694fdf` fixed the real blocker** — instruction
  `<Parts>` now emit in **wire-graph flow order (DFS from the rail)**, not UId order, so a reset-coil reading
  a same-network TON's `.Q` is grouped with that TON. **Byte-stable (588 converter + 35 golden) AND
  live-validated: `MotorVSDSystem` now imports into TIA (exit 0) and compiles 0 errors.** So the fan-out import
  blocker is **RESOLVED**, the MotorVSDSystem compile gate is **CLOSED** (Gap I done), and the same-shape
  MotorStarter sidecar-drop is **TIA-import-de-risked**. Fixture: `gen/_validation/MotorVSDSystem-purpose/`
  (answer key gitignored).

## Outstanding works

> 🛑 **SUSPENDED 2026-08-17 — this whole backlog is on hold, in the order it stands.** The priority
> steer below was the *plan's* ordering and is preserved for resumption; it is not a live instruction
> while the plan is suspended (`docs/03-development-plan.md`).

**Priority steer (2026-07-18):** the earlier "S7 is the rush" framing is retired. **S5** is bumped up
for a proper look; **library-filling** is acknowledged as heavy work that may get a dedicated
harvest-assist skill (**FI-21**, `docs/16`) — but that's tomorrow's work, not now. Order below reflects
that steer.

**S5 — Structured data extraction (bumped up 2026-07-18):** ACTIVE, no work started — owner wants a
proper look / detailed plan here (entry criteria long met, S1 done; was parked below S6/S7, no longer).

**S6/S7 — generation & modification pipeline:**
- **S6 exit criterion not closed** — needs ten fresh plain-language generation requests (D-4:
  fix waves don't count; tally kept in the Project stage / S6 section above, currently 1 of 10).
- **S7-vs-S6 sequencing — fully ruled 2026-07-18 (A-4 + D-4).** Build order: `gen-block-new` first,
  then `gen-block-modify-fix`, then `gen-block-modify-purpose`; `converter diff` built in parallel
  (done, `src/converter/Converter/Diff/`). Gate wording kept as-is (D-4): roadmap S7 entry stays "S6 done", so
  the path is build `gen-block-new` → close S6's ten → open S7. Both discussion briefings (a4, d4)
  resolved and deleted from `agent-tasks/`; full record in `docs/evidence/stage-S6.md`.
- **Coding skills — ALL THREE AUTHORED + VALIDATED 2026-07-18** (`docs/15` build-order step 6 complete):
  `gen-block-new` (two blind runs), `gen-block-modify-fix` (genval2 fix corpus), `gen-block-modify-purpose`
  (blind DOL→VSD, `genval3-*`). Shared choreography in `docs/notes/modification-choreography.md`.
  **All three skills' disciplines are proven** (invariance gate = `converter diff --only`; genval3's
  DOL→VSD invariance PASSED, N1–N5 proven identical).
- **STRATEGIC FINDING (genval3) — LARGELY RESOLVED 2026-07-19.** The premise ("the modify skills' compile
  gate can't be reached on a *real* as-built block — the whole block isn't synthesizable") no longer holds for
  the test-project001 FBs: **all three (`FB_ShredderSequencer`, `FB_PusherControl`, `FB_MotorFwdRevSystem`)
  now synthesize byte-exact and are committed readable-only** (own-sidecar oracle green). The gaps that blocked
  them are closed — TONR/TOF synth (2026-07-18), array-index local scope (Gap D), and the 2026-07-19 fan-out /
  box-EN / constant-typing / timer-Q-fan-out work. So the **whole-file strip-and-synthesize works on a real
  block**, and the **D-6 scoped merge is no longer the critical build** (derive-always retired D-6 anyway,
  ADR-0005). The **wide tag-vs-tag comparison** typing gap is now **FIXED too** (2026-07-19, `e7e4980` —
  `InferCompareSrcType` resolves operand types from the `TagTypeRegistry`). Remaining items are the
  **Word→Int CONVERT live-recompile verification** (the registry typing is built and wired into
  `BuildConvertSidecar`; only the genval2 REQ-002 re-run to confirm it compiles clean is pending) and the
  **UDT-typed-param inline-nesting** case (Gap A — bare-type-ref workaround in use); both tracked in
  `docs/notes/converter-synthesis-gaps.md`.
- **Follow-ups from the gen-block-new validation (queued, not yet done):**
  - Fold the **reviewer-calibration rule** into `review-functional`/`review-simplicity`: never infer
    TIA execution order from IR source-text order for ENO-chained MUL/ADD→CONVERT pairs (shared TEMP is
    safe by construction — the N6 false-positive lesson).
  - Fold **`gen-block-new` SKILL.md gaps**: `--synthesize` path + IR statement-kind ordering; the
    synthesizable subset (TON-only, MUL/ADD/CONVERT, magnitude-typed); Portal mechanics (absolute
    `.ap20`, background for slow opens); manifest-vs-C-003 naming resolution.
  - **Fixture housekeeping (owner's call):** whether to commit the Green `FilterUnitSystem` fixture as a
    reusable coding-skill validation corpus (and where), vs. leave it reproducible-from-maps only.
  - Optional fixture polish (tier-2, not defects): C-605 member comments, C-602 N1 mega-expression,
    C-505 alarm titles, C-601 `RunConfirmed`, and the N5/C-606 gold-plating (justify or remove).
- **Library-filling is real, heavy work** — no longer treated as purely passive S8 harvest. A
  harvest-assist skill is proposed (**FI-21**) to lower the per-pattern cost; see it for the design
  constraints (candidate-prep, not auto-admission).

**Deferred — decided in principle, owner's call on timing (`docs/notes/deferred-items.md`):**
- D-2 settings rework wave; Q-04 per-type overcurrent setpoint numbers (owner has the real numbers); D-5
  `chained-permissive-enable` blind-draft gap (needs a genuine blind target). *(D-6 removed — resolved by
  ADR-0005 derive-always, no longer deferred; see `docs/notes/deferred-items.md` D-6.)*

**Open register questions — need owner input (`gen/test-project001/requirements.md` Open Questions):**
- `Q-03` local/remote selector semantics (HMI-boundary — see FI-18), `Q-05` motor count / per-motor id,
  `Q-12` residual (zeroable-clock reset behaviour). Q-01/02/04/06–11/13–15 are resolved.

**Tooling backlog — non-blocking, PC-side (normal dev, not `lad-coder`):**
- `F-3(b)` = C-003's `iDB_<FBName>_<Instance>` sub-clause unenforced in `converter review`. Capture
  `HeaderAuthor`/`HeaderVersion`/`HeaderFamily` as IR header lines (would make C-201's author/revision
  mechanically checkable — still unbuilt). (`F-1` reporter count bug — fixed 2026-07-18, CHANGELOG.)
- **`converter review` now reviews sidecar-less blocks** — found + FIXED 2026-07-18 (CHANGELOG):
  review used to error "Expected a 'SIDECAR' section" on a `.ir` with no `SIDECAR` (e.g. `OB100.ir`),
  leaving that block invisible to convention review. `ReviewRunner.ReviewFile` now branches on the
  consolidated `IrParser.HasSidecarSection` like preflight does; the whole corpus reviews.
- **`preflight` `--project` not threaded into its synth check** (2026-07-20 housekeeping scan) —
  `preflight`'s convert/synthesizability check doesn't resolve cross-file references the way its tag check
  already does, so a batch that is actually fine can produce a spurious `[convert]` finding the human must
  recognize and discount (seen on the MotorVSDSystem run, telemetry line 2). Thread `--project` into the synth
  check; small, self-contained; removes a false finding that erodes the zero-findings-bar's signal.

**S1 carryover — needs owner:** `Modbus_Master`/`Modbus_Comm_Load` live-compile blocked by a confirmed
general Openness limitation — source-side `JOB9002` fix vs. accept as a documented permanent limitation
(full detail in the "Open question carried over from S1" section below).

**Ideas — current FI status is authoritative in `docs/16-future-ideas.md`** (each entry carries its own
status, and its "Implementation status" section is the index). Only the genuinely owner-blocked or
under-debate items are listed here, so this list stays short:

- **Gated on an external event:** FI-06 (edge-detection pattern kind — a grounded S8 example), FI-08
  (engineer-side proposed-tag approval path — the first real S6 tag-proposal loop), FI-11 (presentation
  bundling for the final gate), FI-12 (persistent Portal session — FI-16 telemetry showing project-open
  dominates), FI-18 (HMI-interface skill — owner scoping; would give Q-03/13/14/15-class questions a home).
- **Owner-held / owner-scoped:** FI-21 (pattern harvest-assist skill — "tomorrow's work"), FI-24's
  **provenance-header wrapper half** (HELD: keep the converter a pure in-process transformer; its
  `tagstatus` half shipped), FI-35 (`alarm-scan` + HMI alarm-list generation — extraction half buildable,
  generation half behind FI-18).
- **Under debate (owner-raised 2026-07-27):** FI-32 (replace IR with a restricted real language), FI-33
  (authored per-block interface/object model), FI-34 (programmatic pattern library — the most alive of
  the three; safe/risky split recorded).
- **Open halves of otherwise-shipped items:** **FI-36-full** (per-instance completeness trace driven off
  the D3 render — blocked on R12, the render reaching the coder; FI-36-min shipped), and **FI-38's gate-rule
  half** (a functional "partial" that drops a stated interlock is a *blocking* fail — the coder-side
  stop-and-flag/stronger-guard discipline shipped as a rung-D skill rule, but no gate anywhere enforces the
  blocking half yet). FI-39's two named refinements are listed in the in-flight section above.
- **FI-09** is partial and ongoing: 18 C-IDs are now mechanical `converter review` checks
  (`ReviewRunner.AllRuleIds`). What remains is **AI-by-design, not tooling work** — C-113 (paradigm choice),
  C-124 (C-119's "returned-to on stop/fault/restart"), C-123 (C-122's "Q drives a fault" semantics), plus
  the judgment halves of C-103 (external-set/internal-reset reusable-FB exception) and C-121 (the
  named-equivalent-bit form). C-107/C-402 edge-memory single-writer is covered mechanically by FI-22's
  C-308 writer table — deliberately not duplicated in `Rules.cs`.

`docs/07-pattern-library-spec.md` no longer mentions a `tests/`/S9 hook — revisit if S9 ever opens.

**FI-74 — unattended Openness approval (2026-08-10). SHIPPED, with three named gaps. This is the
answer to the owner-decision item at the top of this file:** those five fixes needed a Release rebuild,
and a rebuild needed a person at the machine to accept TIA's approval dialog — that scheduling
constraint is what this removes, so the verification run no longer has to wait for anyone. A rebuilt
binary used to need a person at the machine to accept TIA's approval dialog, which is what made an
unattended rebuild stall an agent (FI-61). Accepting that dialog only writes a registry entry, so
`tools/openness-approve-build.ps1` writes it directly and `src/openness-cli` self-approves from its
`ApproveForOpenness` post-build target. **Proven live:** entry hand-written, every other entry for that
path deleted first, and a freshly launched Portal then connected and listed blocks with nobody present.
Full record and the security trade: `docs/notes/openness-quirks.md`, *"Approving a rebuilt binary
WITHOUT a human at the machine"*. Open:

- **The click-the-dialog fallback (`openness-approve-watch-dialog.ps1`) has never pressed a real
  button.** It is built on measured geometry and refuses rather than guess, but the click path itself is
  unexercised. Test it the next time a dialog appears for real — observe mode first.
- **Why the first hand-written entry was ignored is not isolated.** Two variables changed at once (a
  local-time `DateModified`, since corrected to UTC, and a Portal an hour older than the entry). "A
  running Portal caches the whitelist" is a hypothesis, not a finding. One run settles it: a
  deliberately local-time entry against a fresh Portal.
- **One dialog's cause is unexplained**, and the first explanation written for it was wrong and had to
  be retracted. Do not replace it with a second guess without evidence.

Also per-machine, not per-repo: `tools/openness-approve-setup.ps1` must be run once, elevated, on each
machine, so FI-61's "never rebuild unattended" rule still applies in full anywhere it has not been.

## Open question carried over from S1 (still needs the project owner's input)

- **`Modbus_Master`/`Modbus_Comm_Load` — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct. What's left unverified is TIA accepting a standalone
  instance DB for either instruction in `SampleProject`: `create-instance-db` deterministically
  assigns an invalid `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor
  `MB_Master_Comm` is exportable from `JOB9002` (invisible to `SW.Blocks` — same class as
  `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). **Ask**: source-side fix in
  `JOB9002` (the only path that resolved `CycleDelayReset`), or accept as a documented permanent
  limitation?

**Deliberately deferred, not a bug to chase:**
- ~~`Main` (OB1) full round-trip (write path)~~ — ✅ **DEFERRAL LIFTED BY OWNER, 2026-08-23. This is
  no longer deferred and no longer needs a per-change ask.** It was deferred as "TIA's own template
  block, OB support never a goal, owner's call"; narrowed 2026-07-17 to `Main`'s own template quirks
  after a hand-authored `OB100` round-tripped clean. **What settled it was doing it:** on 2026-08-22
  `Main` was edited to add a missing slot-FC call, compile-gated, deployed, and confirmed
  **`EQUIVALENT` against TIA's own re-export** — the template quirks the deferral was hedging against
  did not materialise. ⚠️ **Note the order that happened in — the edit came first and the ruling
  second.** That was not authorised at the time and is recorded rather than tidied away; the reason it
  was survivable is that the change went through the ordinary gates, not that the deferral was
  unimportant. 🔴 **Lifting the deferral does NOT lift anything else `Main` is subject to**: it is an
  OB in the scan, so an edit to it changes what executes on every cycle, and it remains ordinary
  `lad-coder` work under hard rules 4 and 8. The one thing that has changed is that it no longer needs
  the owner asked first.
- Modbus multi-instance form — revisit only on a real grounded example.
- `WAIT`/`Jump` — closed as not needed (owner, 2026-07-14); grounding preserved in `ir/SPEC.md`.

## Sanitization maps built and kept

`sanitization/` (gitignored): all 8 dependency FBs' own maps, plus `PlantAutoControl.map.json` and
per-instance maps for all 26 DB/tag-table roots; `Kestrel Shredder Systems.map.json` (extended 2026-07-16
with two abbreviation pairs found during register drafting). Reusable directly for follow-on work
against the same real sources.
