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

**Then, and only then, release the sub-agent's claims:**

```
converter claims --project ir/<project>/ --claims C:\ProgramData\Ladder-AI\claims --release --agent "<the agent id in the evidence>" --all
```

🔴 **THE ORDER IS THE POINT, AND GETTING IT WRONG INVERTED THE GATE ONCE ALREADY.** The claims half of
the check joins the evidence to claims the registry is *still holding*: it is what proves the block was
reserved before it was written. Until 2026-08-24 all three `gen-block-*` skills ended by telling the
sub-agent to release everything, while verification has always been the dispatcher's job and happens
*after* hand-back — so by the time the check ran the store was empty, and it reported `claims confirmed
in store: 0` with a message accusing the reader of a misconfigured store root. It reded on every run
that followed the skills exactly, which is how a team learns to reach for `--no-verify`. The sub-agent
now hands back HOLDING its claims and this step releases them.

**A dispatch that dies between hand-back and this step leaves a live claim.** Nothing auto-releases,
deliberately — `ClaimStore` reports stale claims and never clears them, because deleting another
agent's coordination state on a guess is how work gets lost. The backstop is
`converter claims --project ir/<project>/ --claims C:\ProgramData\Ladder-AI\claims --check`, which lists
anything older than 24 h under `stale`. **Run it before dispatching a batch**: a stale claim from a
dead dispatch does not corrupt anything, but it will refuse the next agent that wants the same block,
and the refusal names the holder so you can see it was a session that no longer exists. (Measured
2026-08-24: the shared store was holding fourteen unreleased claims, the oldest a week old. Nothing
had ever looked.)

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
| 4 | c204-sweep - C-204 comment-only sweep, FB_ShredderSequencer NETWORKs 2/3/4/9/11/14/15 + block comment: import + compile gate against GenProject1 (test-project001) | done | — | yes |
| 5 | c204-members - C-204 sweep completion, FB_ShredderSequencer INTERFACE MEMBER comments (lines 12/43/103): import + compile gate against GenProject1 (test-project001) | done | — | yes |
| 6 | harness-gen-gate — hard rule 4 compile gate on the newly GENERATED harness artifacts (abf7145/d61652c). Target project is **TestEnviroment**, NOT GenProject1 | done | — | yes (TestEnviroment only) |
| 7 | hba-reset-edge — AR-HBA-05/07 conformance repair, FB_HopperBlockageMonitor NETWORKs 3/4/5 + 2 new statics + iDB mirror: import + compile gate against GenProject1 (test-project001) | done | — | yes |
| 8 | hba-inhibit-rename — AR-HBA-01 interface-member rename `HopperBlockStopReq` -> `HopperBlockedInhibit`, UDT + FB + iDB + FC_ControlMain + 2 regenerated harness objects: import + compile gate against GenProject1 (test-project001) | done | — | yes |
| 9 | hba-rig-prep — rig preparation for the post-AR-HBA-01 conformance wave: baseline, import/compile gate, redeploy, stamp confirmation, submission re-derive. WAVE NOT RUN. | done | — | yes |
| 10 | harness-compile-gate — hard rule 4 compile gate on nine harness objects committed at eba7033 (2 UDTs, 2 stimulus FBs, FC9002 arbiter, 2 iDBs, FB_HopperBlockageStim N21/22, Main). IMPORT + COMPILE ONLY, no download, no rig, no wave. | done | — | yes |
| 11 | deploy-three-slot — deploy the compile-gated three-slot foundation to the rig: stamp-basis decision, copy-layer regeneration, import + compile gate, redeploy, wire probes, submission re-derive. NO WAVE. | done | — | yes |
<!-- Row 11: DEPLOYED AND READY, WAVE NOT RUN (withheld by the dispatch), 2026-09-02. Ran in TWO PARTS: blocked first on a refused claim, then completed after the coordinator released it. COMPILE GATE PASSED: BLOCKS: 47 INCONSISTENT: 0 DUPLICATE NUMBERS: 0 / TYPES: 10 INCONSISTENT: 0, station scope Warning errors=0 warnings=1 (keyed on ERRORS, never STATE). Pre-import baseline taken and HEALTHY (47/0, 10/0), so the run-1 cascade (FC_HarnessCopyLayer + Main, the ELEVENTH observation of that shape) is ESTABLISHED as caused by this import. Round trip hasAnyChange=false, 8/8 networks identical. STAMP BASIS: EIGHT objects, ruled after a five-set measurement whose CONTROL reproduced the deployed 16#79B22CE9 exactly off the pre-repair baseline IR - 4obj-asdeployed 79B22CE9 / 4obj-repaired 70FD6FB9 / +arbiter 6C1B2E3C / +2heads AE0DB967 / +Main 0617A96E. converter cross-check settled it: all five hopper stimulus lines (Test[0],[5],[6],[8], DB_Controls.FaultReset) are written by the arbiter AND by at least one new head, and on all five the LAST writer before FC_Inputs reads is FB_ShredderSequencerStim. Main added because its own comment makes the total call order the soundness argument for that four-writer chain. DEPLOYED 16#0617A96E: download-probe 67 objects loaded by name (was 60; +7 is the foundation), stamp confirmed on the wire by BOTH protocols, SCAN COUNTER FELL 18,306,338 -> 4,996. Mirror confirmed EXACTLY 1024 wide from both sides (r1024/r1025 refused with Modbus exception 2). Note 37 vs 1024: 37 is the MAP ALLOCATION, 1024 the SERVED WINDOW - not in tension. Submission re-derived AFTER the download (derive exit 0, 5 fields, deployment computed against THIS download's 67-object manifest) to C:/Users/User/AppData/Local/Temp/hba-derive/submission-v9-threeslot-derived.json; submission gate 31 gates 0 refused 0 could-not-run and RECOMPUTES 16#0617A96E = the wire. *** ACCEPTED GAP, RULED BY THE COORDINATOR AND MUST BE REVISITED NOT INHERITED: FC_Inputs and FC_ControlMain pass the same fail-open test and are in NEITHER basis; the justification is likelihood (plant code frozen, harness under active edit), not structure, and it expires the moment any lane takes a block-edit claim on either. *** SECOND FINDING: the submission's deployment.deliverableObjects names 60 objects while the download loaded 67; harness-gate derive named the 7 by name and classed it 'incomplete, not wrong' - NOT edited by me, it is the submission author's field. Socket FREE (two independent Modbus connects), no harness process, no socket to the rig, Portal unchanged and PID 2220 never touched. evidence.json in agent-tasks/deploy-three-slot/ - checker exit 1, PROBLEMS 2, both the pre-existing C-103 preflight AND its own control run on the untouched committed block; claims 1 of 1 confirmed and joined, 1 of 1 edited blocks covered, 32 gates. CLAIM STILL HELD at hand-back (block-edit FC_HarnessCopyLayer) - release by kind+value, NOT --all. Not committed. -->
<!-- Row 11, FIRST PART: BLOCKED ON A CLAIM, and NOTHING WAS DEPLOYED. `converter claim --kind block-edit --value FC_HarnessCopyLayer` was REFUSED (exit 1, HeldByAnother: ee055079-.../lad-coder-widen, held since 2026-09-01T23:56:59Z). The dispatch's standing instruction on a refusal is to stop rather than work around it, so the copy layer was never regenerated into the corpus, nothing was imported or compiled, Portal was never opened for a write, and NO QUEUE ROW WAS EVER CLAIMED - this row is `blocked`, not `done`, and it never held Portal. THE UNBLOCK IS THE DISPATCHER'S STEP AND HAS AN EXACT PRECEDENT: row 9 -> modbus-widen-1024 hit this same refusal against lad-coder-rigprep, the coordinator released THAT ONE CLAIM by kind+value (never --all), and the lane re-acquired and finished. Beware --all here: lad-coder-widen also holds block-edit FB_Comms_ModbusServer, which this task does not need. WHAT DID GET DONE, all read-only: the STAMP BASIS was MEASURED, not argued - five harness-run --generate-only runs over five --program sets, with a CONTROL run reproducing the deployed 16#79B22CE9 exactly off the pre-repair FB_HopperBlockageStim baseline, which is what makes the other four numbers trustworthy. 4 obj as-deployed 16#79B22CE9 / 4 obj repaired stim 16#70FD6FB9 / +arbiter 16#6C1B2E3C / +2 heads 16#AE0DB967 / +Main 16#0617A96E. Map hash identical on all five (a9566946...), so --program feeds the stamp ONLY; the emitted copy layer differs between sets in EXACTLY ONE LINE and HarnessMirror.ir is byte-identical across all five and to the committed one. RECOMMENDATION: eight objects, 16#0617A96E - the arbiter and BOTH new heads belong, and so does Main. `converter cross-check` shows all five of the hopper slot's stimulus lines (Test[0], [5], [6], [8], DB_Controls.FaultReset) are written by the arbiter AND by at least one new head, and on all five the LAST writer before FC_Inputs reads is FB_ShredderSequencerStim - not the hopper head. Main's call order is the whole soundness argument for that four-writer chain, by its own block comment. Open residual NOT closed: FC_Inputs and FC_ControlMain pass the same test and are in neither basis. Rig left untouched at 16#79B22CE9, mirror confirmed 1024 wide on both sides, socket FREE (two independent Modbus connects), tunnel UP. evidence.json in agent-tasks/deploy-three-slot/ - checker exit 1, PROBLEMS 4, 6 gates DEFERRED with reasons, 0 claims held. Not committed. -->
<!-- Row 10: GATE PASSED 2026-09-02. Nine objects, all committed at eba7033, none of which had ever been through TIA. BEFORE (established, taken before the import): BLOCKS 42 INCONSISTENT 0, TYPES 8 INCONSISTENT 0. AFTER, run 2: BLOCKS 47 INCONSISTENT 0, TYPES 10 INCONSISTENT 0, station Warning errors=0 warnings=1 (keyed on errors). All nine per-item compiles [Success] with consistentAfterCompile=true. THE RISKY CONSTRUCT SURVIVED: FC_HarnessStimArbiter's 23 instances of `COIL DB_Input.Test[n] := (NOT) AlwaysTrue` - the negated-system-memory-bit-to-coil shape onto a DB ARRAY ELEMENT, never previously tried in this corpus - compiled clean, and so did the same shape in FB_HopperBlockageStim N22. Imported the arbiter ALONE FIRST so that answer could not be confounded. ROUND TRIP: all five code blocks came back with hasAnyChange=false (4/21/26/24/15 networks, 90 total, zero changed, interface and comments included); both UDTs and iDB_ShredderSequencerStim byte-identical modulo CRLF. NO IR WAS WRITTEN and no claim was taken - the evidence declares the three AUTHORING lanes' live reservations under their true holder ids. *** DO NOT RUN THE USUAL RELEASE STEP ON THIS EVIDENCE: the claims belong to lad-coder-pusherslot / -shredderslot / -arbitration, not to the gate lane. *** evidence.json in agent-tasks/harness-compile-gate/ - checker exit 0, PROBLEMS 0, 31 gates, 8 claims joined. No download, no rig, no wave. Not committed. -->
<!-- ONE THING TIA CHANGED ON THE WAY IN, and it is the only one: iDB_PusherStim.ir is committed as an empty-MEMBERS shell (`INSTANCEOF FB_PusherStim` and nothing under MEMBERS), and TIA populated the full 96-line member image from the FB on import - the round-trip diff is 96 added lines and ZERO removed. Its sibling iDB_ShredderSequencerStim.ir is committed FULLY POPULATED and round-tripped identical. Two sibling objects from two lanes in two different shapes; both legal, both compiled clean, and the shell form is simply resolved by TIA. Worth a ruling on which shape the corpus wants, since only one of them survives a re-export unchanged. -->
<!-- TENTH OBSERVATION of the post-import clearing: sanity-check run 1 exited 9 naming 2 dependents (FB_HarnessViolationLatch, FC_HarnessCopyLayer); both per-block compiles reported "No block was compiled. All blocks are up-to-date." with errors=0 and consistentAfterCompile=true; run 2 reported INCONSISTENT: 0. Same shape as rows 2-6, 8 and 9. This run carries a BEFORE-baseline, so the two are established as caused by this import. Mechanism still not asserted - and note the test named at row 5 (one sanity-check run with NO per-block compile after it) has still never been run. -->
<!-- CLAIM REGISTRY GAP, second instance: UDT_ShredderSequencerStim is in the corpus under NO claim of any kind, while its lane reserved FB9004 and DB9004 and the pusher lane did reserve UDT_PusherStim. check-agent-evidence.py names it and correctly does not gate it (a TYPE has no NUMBER line). Same door as row 8's tag-table finding: an object kind the gate can only report on. -->
<!-- Row 9: RIG READY, WAVE NOT RUN (withheld by the dispatch). Compile gate PASSED 2026-09-01: BLOCKS 42 INCONSISTENT 0, TYPES 8 INCONSISTENT 0, station Warning errors=0 warnings=1 (keyed on errors). ONE object imported, not seven: drift-check --complete against a fresh 52-object export showed all seven AR-HBA-01 objects ALREADY MATCH in the project (row 8 imported them), so the only real delta was the copy layer's build-stamp constant. STAMP: the committed ir/ copy layer carries 16#B8BCB7CB but a regeneration at HEAD emits 16#0444D18E - measured, and NOT the 16#2819B312 row 8 recorded for the same control, which is unexplained and not asserted either way. Deployed 16#0444D18E, confirmed on the wire twice (S7 marker read AND Modbus FC03) with the scan counter falling 17,004,851 -> 20,090. Submission re-derived AFTER the redeploy: harness-gate derive exit 0 (5 fields, deployment compared against THIS download's 60-object manifest), submission gate AdmissibleSubjectToJudgement, 31 gates, 0 refused. Two derivation artifacts (rs.json, conflict-graph.json) still named the OLD member and were REGENERATED rather than re-cited. Socket free (two independent Modbus connects), no harness-run process alive. evidence.json in agent-tasks/hba-rig-prep/ - checker exit 1 naming three lines, all explained: preflight's pre-existing C-103 (with a control run on the as-built returning it identically), that control run itself, and drift-check's 3 pre-existing drifted objects. CLAIMS STILL HELD at hand-back. Not committed. -->
<!-- NINTH OBSERVATION of the post-import clearing: sanity-check run 1 exited 9 naming 2 dependents; both per-block compiles reported "No block was compiled. All blocks are up-to-date." with errors=0 and consistentAfterCompile=true; run 2 reported INCONSISTENT: 0. Same shape as rows 2-6 and 8. This run DOES carry the before-baseline rows 7/8 lacked - sanity-check --json before the import was healthy=true, 0 inconsistent blocks - so the two inconsistencies are established as caused by this import rather than pre-existing. Mechanism still not asserted. -->
<!-- MEASURED, for whoever owns the harness: the build stamp is INDEPENDENT OF THE SUBMISSION. Two --generate-only runs over the same --program set and binding, with submission-v4-derived and submission-v5-derived, both emitted 16#0444D18E. It is a function of the program set, the map/binding and the retentive-M default. That is what makes it safe for the wave to recompute at launch against a device loaded earlier. -->
<!-- TRAP, cost ~20 minutes: `converter conflict-graph --project <dir> --submission <file>` exits 2 (NOT DERIVED) on this lane's submission because 10 of 16 SUBMISSION SIGNAL NAMES have no declared join - a submission signal is the spec's name, not a storage path. The form that works, and the one the previous lane used, is `--signals <file>` with one STORAGE PATH per line; 21 of 21 resolved, edges [] earned. -->
<!-- NOT A FINDING, recorded so it is not "found" again: reading gen/.../conformance-vectors-b*.json with Python's default Windows codec (cp1252) renders their raw UTF-8 em dashes as mojibake and makes them look corrupted next to an ensure_ascii-escaped submission. Read them as UTF-8; the vectors array is byte-equal to the previous submission's. -->

<!-- Row 8: import + compile gate PASSED 2026-09-01. Seven objects (UDT, FB, iDB, FC_ControlMain, FB_HarnessViolationLatch + the two REGENERATED harness objects). sanity-check run 2 OVERALL HEALTHY, BLOCKS 42 INCONSISTENT 0, TYPES 8 INCONSISTENT 0, station Warning errors=0 warnings=1 (keyed on errors). diff --only exit 0 on all four code blocks, remainders 5/8/5/5, --allow-header declared on the FB only. Round trip: 6/6, 9/9, 6/6, 8/8 networks identical; UDT, iDB and tag table byte-identical. drift-check 0 drifted / 26 compared after refreshing the four committed exports. HAND-WRITTEN HARNESS IR: 0 lines - both generated objects came from `harness-run --generate-only`, fixed point measured both directions. MAP HASH UNCHANGED (no signal name is an input); BUILD STAMP 16#33434A68 -> 16#B8BCB7CB, and it was ALREADY stale at HEAD (a control regeneration from git HEAD emits 16#2819B312). evidence.json in agent-tasks/hba-inhibit-rename/ - checker exit 1, naming ONE line only: preflight FC_HarnessCopyLayer exit 1 on a C-103 finding proven pre-existing by a control run on the as-built. CLAIMS STILL HELD at hand-back. No download, no wave. Not committed. -->
<!-- EIGHTH OBSERVATION of the post-import clearing, and it CONTRADICTS row 7's distinguishing hypothesis. Row 7 argued its per-block compiles did real work because the import changed an INTERFACE. This import also changed an interface (a member rename), and all nine per-block compiles reported "No block was compiled. All blocks are up-to-date." with errors=0 - the rows 2-6 shape. So "the interface changed" does not distinguish the two cases. Mechanism still not asserted. -->
<!-- CLAIM REGISTRY GAP, measured this run: `converter claim --kind block-edit --value HarnessMirror` is REFUSED, exit 1, result NotInCorpus - "a tag is addressable but is not an editable object, and is deliberately not accepted here". No claim kind reserves a TAG TABLE, so a tag-table edit runs under no reservation, and check-agent-evidence.py does not gate it either (no NUMBER line). Two doors open on the same object. -->
<!-- MEASURED: `converter to-xml` REFUSES without `--project <ir-dir>` on any file whose members resolve through another object - exit 1, "refusing to emit XML with unresolved member types". 5 of 7 files here needed it. The choreography's step-4 pre-check says to read to-xml's exit code as the definitive answer on synthesizability; a bare invocation returns exit 1 for a completely unrelated reason and reads exactly like an unsupported construct. -->

<!-- Row 7: import + compile gate PASSED 2026-09-01. FaultReset made edge-triggered (new FaultResetEdge/FaultResetMem statics, NW5) and removed from the accumulator entirely (NW3 MOVE enable, NW3 ADD enable, NW4 TON IN), per AR-HBA-05/AR-HBA-07. Routed to gen-block-modify-purpose, NOT the fix skill, because the repair needs an interface member — the standing routing rule. NO GATE-1 TIER-(c) MANIFEST ITEM EXISTS for this delta (architecture.md predates AR-HBA-05); proceeded on the dispatch's explicit naming of the delta and boundary and flagged it for engineer ratification. diff --only 3 4 5 --allow-header exit 0, INVARIANCE OK, UNCHANGED REMAINDER 3 network(s) (NW1/2/6). preflight 0 findings, and CLEAN on the as-built first. review --project exit 0, UNCHECKED 0. sanity-check run 2 HEALTHY, BLOCKS 42 INCONSISTENT 0, TYPES 8 INCONSISTENT 0, station device compile Warning errors=0 warnings=1 (keyed on errors). Round trip re-export -> to-ir -> diff = 6/6 networks identical; only header delta is an empty CONSTANT section TIA emits. block-layout Optimized before and after, --expect Optimized exit 0, --set deliberately NOT run. iDB mirrored and separately claimed. evidence.json in agent-tasks/hba-reset-edge/ — checker exit 0, 15 gates, 2 claims joined, 0 problems. CLAIMS STILL HELD at hand-back, per the dispatcher-releases protocol above. No download, no wave. Not committed. -->
<!-- COUNTER-OBSERVATION to rows 2-6's unexplained clearing: sanity-check run 1 exited 9 naming 4 dependents (FC_ControlMain, FC_AlarmsMain, FB_HarnessViolationLatch, FC_HarnessCopyLayer) and the four per-block compiles that followed did REAL work — none reported "No block was compiled. All blocks are up-to-date." Run 2 then reported INCONSISTENT: 0. The distinguishing feature of this run is that the import changed an INTERFACE, so the dependents genuinely needed recompiling. Offered as a distinguishing case, not a cause. -->
<!-- NOTE for whoever owns the cascade: FB_HarnessViolationLatch was among the four. No BEFORE sanity-check was taken this run, so it is NOT established that all four were consistent before this import — three are direct callers/readers of the changed FB or iDB and are explained by the cascade; that one is not obviously explained by it. -->

<!-- Row 6: GATE PASSED 2026-08-27. Five generated artifacts (slot FC 9100, cyclic OB 200, three instance DBs 9101/9102/9103) imported and compiled into TestEnviroment. BEFORE: BLOCKS 77 INCONSISTENT 0, TYPES 42 INCONSISTENT 0. AFTER: BLOCKS 82 INCONSISTENT 0, TYPES 42 INCONSISTENT 0. Round trip EQUIVALENT on all five (converter compare), diff --only INVARIANCE OK on both code blocks. Project then returned to 77/0 + 42/0 by deleting the five - the generated OB starts a second MB_SERVER on the rig's own port and connection ID. TWO GENERATOR FINDINGS, both reported not fixed: (1) CyclicObGenerator cannot emit a block COMMENT, so no binding can produce a C-201-compliant OB - preflight exit 1; (2) LoopCli parses no commsFb/stimUdt, so two of the six generated artifact kinds have NO production caller at all and a binding declaring them is reported as AUTHORED. evidence.json in agent-tasks/harness-gen-gate/ - checker exit 1, naming (1) only. -->
<!-- FIFTH OBSERVATION of the unexplained clearing: sanity-check run 1 exited 9 naming ONE dependent (Main); the per-block compile of Main reported "No block was compiled. All blocks are up-to-date." with errors=0 and consistentAfterCompile=true; run 2 reported INCONSISTENT: 0. Same shape as rows 2-5. Mechanism still not asserted. -->
<!-- MEASURED, worth keeping: `openness-cli export --out` with a path whose directory segment is fine but whose FILE name failed to expand wrote the export to a FILE named after the unexpanded token and still exited 0 with a "Exported 'X' -> <path>" line. The tool echoes the path it wrote; read that line rather than trusting the exit code plus an expected filename. -->
<!-- Portal-process note: another lane held Portal on a DIFFERENT project throughout. Every connect in this run was prompt; nothing was killed, nothing was rebuilt. -->
<!-- 125-object export-all --tagtables baseline was taken first (COMPLETE, exit 0) and all 125 converted with `converter to-ir` cleanly - 125 ok, 0 fail. That is a whole-workbench-project converter read with no UnsupportedConstructException anywhere. -->

<!-- Row 5: import + compile gate PASSED 2026-08-21. Three INTERFACE MEMBER comments repaired (IO, PusherModeForceOff, OvercurrentTripped); comment text only, zero logic lines and zero interface-shape change. THE BLOCKER FROM ROW 4 IS CONFIRMED GONE: with the Release converter carrying cd12753, `diff --only 1` gave HEADER changed: interface-comment / INVARIANCE OK / HEADER COMMENT CHANGED (does not gate) / UNCHANGED REMAINDER: 15 network(s), exit 0, interfaceChanged=false, interfaceCommentChanged=true, titleChanged=false, invarianceViolations=[]. --allow-header was NOT passed and was not needed. preflight CLEAN 0 findings; review UNCHECKED: 0, 0 findings. sanity-check run 2 healthy=true, BLOCKS 36 INCONSISTENT 0, TYPES 7 INCONSISTENT 0, station device compile Warning errors=0 warnings=1 (keyed on errors). Round trip re-export -> to-ir -> diff = 15/15 identical with NO header delta, and the three repaired member comments came back byte-identical, so TIA stored the comment text verbatim. block-layout READ before and after: Optimized both times, gated --expect Optimized exit 0 both; --set deliberately NOT run (unrequested, and this FB's IO member is RETAIN SETPOINT). Portal released; claim registry block-edit released. No download. simatic-ml/ deliberately not re-exported (ExportDriftDetectorTests pins this block). evidence.json GREEN (check-agent-evidence exit 0). Not committed. -->
<!-- FOURTH OBSERVATION of the unexplained clearing, and this run narrows it: sanity-check run 1 exited 9 naming 4 dependents; the four per-block compiles that followed EACH reported "No block was compiled. All blocks are up-to-date." with errors=0 and consistentAfterCompile=true; sanity-check run 2 then reported INCONSISTENT: 0. Consistent with row 4's cheap hypothesis (sanity-check run 1 itself performs the station device compile, which clears them, so the per-block compiles that follow have nothing left to do). STILL NOT ASSERTED AS CAUSE - it has not been tested; the test is one sanity-check run with no per-block compile after it. -->
<!-- Note on FB8's own per-block compile, which is NOT part of that pattern: it reported "Block was successfully compiled." (real work), while the four DEPENDENTS reported "No block was compiled." So the freshly-imported block does get compiled by its own --block call; it is only the cascade dependents that come back already up-to-date. -->
<!-- Row 4: import + compile gate PASSED 2026-08-21. C-204 comment-only sweep, ZERO logic lines changed (every -/+ pair in converter diff shows the statements byte-identical). diff --only 2 3 4 9 11 14 15 exit 0, interfaceChanged false, titleChanged false, UNCHANGED REMAINDER 8 network(s) - non-empty, so not the NOTHING WAS PROVEN case; HEADER COMMENT CHANGED (does not gate) printed for the block comment; --allow-header never passed. sanity-check run 2 HEALTHY, BLOCKS 36 INCONSISTENT 0, TYPES 7 INCONSISTENT 0, station device compile Warning errors=0 warnings=1 (keyed on errors). Round trip re-export -> to-ir -> diff = 15/15 networks identical and no header delta, so TIA altered nothing. block-layout read BEFORE deciding: Optimized (third measurement today); --set Standard deliberately NOT run; gated --expect Optimized exit 0 after. Portal released. No download - the rig's deployed program diverges further. simatic-ml/ deliberately not re-exported (ExportDriftDetectorTests pins this block). evidence.json GREEN. Not committed. -->
<!-- BLOCKER ROUTED, NOT WORKED AROUND: three C-204 breaches remain, ALL of them in INTERFACE MEMBER comments (line 12 C-115; line 43 C-601 + 'owner amendment 2026-07-16' + 'Deliberately NOT'; line 103 'rather than guessed at'). A member-comment edit is indistinguishable from a member RETYPE to converter diff: DiffRunner.InterfaceCanonical serializes the whole INTERFACE section including COMMENT text, so it sets interfaceChanged -> BehaviourBearingChange -> exit 1. Measured this run on a scratch copy: deleting '(C-115)' alone from line 12 gave INVARIANCE VIOLATION 'an INTERFACE member changed', exit 1, while the same edit to the BLOCK comment exited 0. This is the SAME taxonomy defect the 2026-08-14 narrowing fixed one level up, still open one level down. -->
<!-- THIRD OBSERVATION of the unexplained clearing: sanity-check run 1 exited 9 naming 4 dependents; the four per-block compiles that followed EACH said 'No block was compiled. All blocks are up-to-date.'; run 2 reported INCONSISTENT 0. Mechanism still not asserted. Note for whoever owns it: sanity-check's own output carries a deviceCompiles[] entry, so run 1 enumerates consistency and THEN compiles - which would explain all three observations and is cheap to test. Not claimed as cause. -->
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
