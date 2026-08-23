# Deferred items

Work the owner has explicitly said "yes, eventually — not now" to, separated out from AITODO
(which is for *in-flight* work) and `docs/16-future-ideas.md` (which is for ideas still *under
debate*). An item here is already decided in principle; only its timing is open. Created
2026-07-17 at the owner's own suggestion (`docs/notes/owner-questions.md` D-5).

Each entry: what it is, why it's deferred, what would bring it back. An item leaves this doc when
the owner picks it back up — move it to AITODO as in-flight work, don't just delete the line.

## D-2 — Settings rework wave

**What:** the sub-struct settings design (`UDT.Set.X`) is fully live-verified and ready. Folds in:
nine `DB_Settings` scan-copy deletions (the `FC_ControlMain` trap, C-308), settings migrated into
per-instance UDTs with iDB start values as commissioning defaults (C-307), the `DB_Settings`
shrink-to-empty question, `InCycle` member removal, a C-605 member-comment pass, and buffer
`Snake_Case` renames (C-001/C-607) — or split into smaller waves, owner's call.

**Why deferred:** owner ruling, 2026-07-17 (`docs/notes/owner-questions.md` D-2) — "Hold-Off for
now."

**Revisit trigger:** none specified; owner's call to restart. Fuller queue detail lived in
`fix-wave-1-reviews.md`, retired in the 2026-07-17 test-project001 declutter; `docs/notes/owner-questions.md`
D-2 remains the ruling record.

## D-6 — Converter can't add a new statement to an already-exported network — RESOLVED 2026-07-18 (ADR-0005)

**Resolved by derive-always (ADR-0005, Accepted).** A synthesizable block no longer *stores* a sidecar
(`to-ir` omits it; `to-xml` re-derives it), so there is no stale sidecar to reconcile when a statement is
added — the edit just re-derives. Both gates passed (offline parity 14/14, live TIA compile 10/10) and
the flip landed (`to-xml` derives by default, `to-ir` omits the sidecar for synthesizable blocks). The
scoped `SidecarSynthesizer` merge sketched below is therefore **no longer needed** — it was the interim
bridge for a world where the sidecar stayed stored. (A block using a still-unsynthesizable construct —
`Limit`/`Wait`/`FillBlockI`/`Modbus*` — keeps a stored sidecar and would still hit D-6 if edited in
place; adding that construct to synthesis, guarded by the parity harness, is the path, not the merge.)

**Original writeup (historical):**

**What:** `converter to-xml`/`preflight` have no supported path for "add one new statement to a
network that already carries real sidecar data from a prior TIA export." Hit concretely during
the 2026-07-17 fix-wave/agent-tasks build-out: adding one `MOVE` to
`FB_PusherControl.ir`'s Network 10 (which already has a real sidecar for its existing `TON`/
`COIL`/`MOVE`) fails with `IrFormatException: Network 10: IR has 2 move(s) but the sidecar
records 1`. The only existing new-content path is `converter to-xml --synthesize`
(`docs/15-generation-pipeline.md`, "Sidecar synthesis"), which mints a whole fresh sidecar for a
genuinely new, sidecar-less network — and `IrParser.ParseBlockWithoutSidecar` deliberately
hard-errors if a real `SIDECAR` section is already present, so it can't be pointed at a
part-real/part-new network either. The sidecar is machine-owned (`ir/SPEC.md` "Sidecar" section)
— hand-authoring the missing entry is explicitly against that rule (CLAUDE.md hard rule 7: report
converter gaps, don't hand-patch).

**Why deferred:** owner ruling, 2026-07-17 — asked to log this as a blocker rather than build the
converter fix immediately (a `SidecarSynthesizer`-style scoped merge — keep every existing Part/
Wire/Access UId as-is, mint fresh collision-safe UIds only for the newly-added statements — was
the proposed shape, not attempted).

**Revisit trigger:** this is a general converter limitation, not tied to any one task — it will
resurface any time new logic needs to be added to an already-exported network. The 2026-07-17
fix-wave/agent-tasks queue hit it repeatedly across several tasks (all since closed) and confirmed
the whole-file-strip workaround below handles every case tried. Concrete-case detail (the C-4
entry) lived in `fix-wave-1-reviews.md`, retired in the test-project001 declutter; the drafted IR
itself is in `ir/test-project001/FB_PusherControl.ir` Network 10.

**Update (task 03, 2026-07-17): the whole-file workaround still works and is not itself blocked.**
Task 03 hit the identical wall the naive way at first (touch one network, leave the rest of the
file's real sidecar in place) but the established fix-wave-1 edit workflow — strip the file's
*entire* `SIDECAR` section, edit the readable IR, `to-xml --synthesize` the whole file, import,
compile, re-export, `to-ir` to restore real sidecars for every network — went through clean (0
errors) on the exact file this entry describes, *including* task 08's still-uncompiled Network 10
`MOVE` (task 03 had to rebase its own Network 11 change onto task 08's already-committed draft).
Re-exported readable IR diffed byte-identical against pre-edit HEAD everywhere except task 03's
own Network 11 — task 08's Network 10 addition survived the round-trip untouched and now compiles.
So this doesn't block anything that's willing to do the whole-file strip (mildly more disruptive —
temporarily loses every other network's real sidecar until the post-compile re-export, and any
other in-flight uncommitted edit to the same file has to be reconciled first) — task 08's own
C-4/queue entries haven't been touched by this update; that's task 08's call to close out.

**Update (task 05, 2026-07-17): confirmed clear, and an even simpler case than task 03's.** Task
05's fix (`FB_PusherControl.ir` `NETWORK 14`, a standalone `Step <> 0`-keyed force-to-idle
transition) never touches any existing network's own statements at all, so it doesn't even need
task 03's "rebase onto in-flight sidecar-stripped drafts" step — the whole-file strip +
`to-xml --synthesize` ran clean on the first try, verified directly (not just `preflight`). Not yet
imported/compiled (still waiting on its own queue slot), but D-6 is not what's blocking it.

## D-7 — Stale `simatic-ml/test-project001` exports (ir↔simatic-ml drift)

**What:** `converter drift-check` (FI-26, built 2026-07-20) surfaced **6** test-project001 blocks whose
committed `simatic-ml/*.xml` has drifted from the fixed `.ir`: `FB_ShredderSequencer`, `FB_PusherControl`,
`DB_Settings`, `iDB_MotorFwdRevSystem_Shredder`, `iDB_PusherControl`, `iDB_ShredderSequencer`. The cause is
the B-5/REQ-028 re-arming fix and its interface cascade (into the instance DBs and `DB_Settings`) landing in
the `.ir` but never being re-exported. Closing it = refreshing those `.xml` from **real TIA output** (the
`simatic-ml/` corpus is reviewer-skill validation data, deliberately *not* a converter-regenerable cache),
which is a live-Portal round trip.

**Why deferred:** owner ruling, 2026-07-20 — chose to defer rather than open a Portal session now. The drift
is already consciously tolerated by the own-sidecar round-trip oracle (`FrozenAnswerKeyRoundTripTests`), and
`ExportDriftDetectorTests`' known-drift baseline pins exactly these 6 so the build stays green until they're
cleared (a 7th drifting block, or any of these ceasing to drift, turns the baseline test red — the nudge to
revisit).

**Revisit trigger:** next live-Portal session on GenProject1. **Open question to settle then:** does the live
TIA project already carry the re-arming fix (→ a plain `openness-cli export` refresh) or not (→ import the
fixed `.ir` → compile gate → export)? When cleared, re-run `drift-check` to confirm MATCH and prune the 6
from `ExportDriftDetectorTests`' baseline. `docs/16-future-ideas.md` FI-26 is the tool record.

## D-8 — Migrating the legacy `Modbus_*` names onto the `(name, version)` instruction registry

**Decided 2026-08-13: NOT NOW — and this is a decision with reasoning, not a TODO nobody got to.**
The `(name, version)` registry (`SimaticMl/FixedShapeInstructions.cs`) is the right structure and is
where new instructions of that shape go. The question here is only whether the two *legacy* entries
already in the whitelist are rewritten onto it. They are not.

**What:** TIA emits **three different spellings** of the Modbus family, and the whitelist originally
matched **none of the real ones**:

| | |
|---|---|
| whitelist carried | `Modbus_Master`, `Modbus_Comm_Load` — **taken from HAND-AUTHORED FIXTURES** |
| a real FC export emits | `MB_MASTER` **2.2**, `MB_COMM_LOAD` **2.1** |
| a real FB export emits | `MB_SERVER` **5.3** |

Both families are now deliberately **kept**. Migrating would mean retiring the legacy names in favour
of the measured ones alone.

**Why not now — three reasons, in order of weight:**

1. **It would discard evidence.** `docs/evidence/stage-S1.md` records a live TIA `Import()`
   **resolving** the V5.0/6.0 `Modbus_*` names — failing later, on instance DBs, rather than with the
   *"instruction cannot be found"* TIA raised for `WAIT` in that same session. Those names are not a
   fixture artefact that happens to be wrong; **TIA resolved them.** The fixtures were evidence about
   the *shape*, never about the *name* — but the import log is evidence about the name.
2. **It would change readable IR text on committed files** for no behavioural gain, touching the
   corpus that `drift-check` and the golden harness are pinned against.
3. **There is no measured gain.** Nothing is currently mis-converting because both spellings are
   accepted. Keeping both costs two table entries.

**And the thing the registry actually protects is unaffected either way:** an **unknown version is
refused**, because version is exactly what changes a port list, and the port list is what the
converter *supplies* — accepting an unknown version means applying the wrong template, which converts,
imports, and then misbehaves on the controller.

✅ **CORRECTED 2026-08-23. The parenthesis that stood here said `MB_SERVER` 5.3 was *"characterised and
deliberately not registered"*, and it had been wrong since 2026-08-12** — `MbServer53` is in
`SimaticMl/FixedShapeInstructions.cs:142`, added on exactly the condition the old note set for it.
CLAUDE.md was corrected on 2026-08-13 **and flagged at the time that the stale version had already cost
a wrong plan** (it was read as *"MB_SERVER cannot be moved into a project"*, which stopped a rig
deployment a step early); this copy was not corrected with it, so the retracted claim stayed readable
here for ten days. ***And the stated reason never applied to us at all:*** the `Array[..] of Struct`
belongs to the Siemens **sample** block used to characterise the port list, not to anything we author —
`MB_HOLD_REG` is an **area pointer over marker memory** (`P#M1000.0 WORD n`), so the structured
interface simply never arises. Proven end to end in `GenProject1` on 2026-08-13: imported, compiled
`errors=0`, and confirmed from **TIA's own re-export** (`Part Name="MB_SERVER" Version="5.3"`).

🔴 **The lesson is the one this file keeps recording: a correction applied in one place and not the
other is worse than no correction, because the two copies now disagree and nothing says which is
current.** When you retract a claim, grep for it.

**Revisit trigger — one, and it is narrow: a real export that CONTRADICTS the retained names.** An
export emitting `Modbus_Master`/`Modbus_Comm_Load` with a port list that differs from what the
whitelist supplies, or evidence that TIA no longer resolves them, makes the legacy entries actively
wrong rather than merely redundant. Tidiness is **not** a trigger — the entries are cheap and the
evidence they carry is not.

## A8 / G2 — What a structural DB change does to retentive data

**What:** whether restructuring a DB preserves or resets its **retentive** members. It feeds DB-1's
change-class table in the test-harness design, and therefore the **routing** of a change into the
RUN-class or STOP-class download queue — so it is not cosmetic.

**Why deferred:** owner ruling, restated 2026-08-13. **It is deferrable because the conservative
route is already the default and costs little:** an unknown answer routes the change the careful way,
and R4 already records that `DataBlockReinitialization` resets **all** data including retain [R], so
the pessimistic assumption is both available and correct-if-unlucky. Nothing is blocked on the
answer; only an optimisation is.

**Revisit trigger:** when retain pressure makes the optimisation worth having — §16.13 measures
retain as the tightest budget on the rig by a factor of twelve, so a harness that consumes most of
the remaining retain makes this answer matter considerably more than it does today. That is the
signal, not the calendar.

## Q-04 (test-project001) — Per-type overcurrent setpoint numbers

**What:** REQ-019 needs an overcurrent setpoint pair (`OvercurrentSetpointMedium`,
`OvercurrentSetpointHigh`, and by extension the per-type delays `OvercurrentMediumDelay`/
`OvercurrentHighDelay`, REQ-021/022) for each of the three machine types (K75/K100/K150, Q-04's
type-count question — resolved). No numbers exist yet.

**Why deferred:** owner ruling, 2026-07-17 (`gen/test-project001/requirements.md` Q-04) — "I dont
have those at the min, leave 0 add to deferred." In the meantime the relevant `DB_Settings`
members get an explicit `0` start value as a deliberate placeholder (C-604 convention) rather
than staying unset.

**Revisit trigger:** owner has the real setpoint numbers (from commissioning data, motor
nameplate/drive data, or site input). Also blocked on the overcurrent family's disarmed
status generally (Q-11 — real AI hardware not on this prototype panel).

## D-5 — `chained-permissive-enable` pattern's blind-draft admission gap

**What:** pattern admission criterion 3 (a genuinely blind drafted instance, proving the pattern
generalizes) was accepted on partial evidence — the drafting session had prior knowledge of the
real answer, not blind. Full analysis: `docs/16-future-ideas.md` FI-05.

**Why deferred:** owner ruling, 2026-07-17 (`docs/notes/owner-questions.md` D-5) — "Record as
deferred." Blind test material is scarce (a real, untouched target within the recorded
data-boundary approval is needed).

**Revisit trigger:** none specified; owner's call, or a suitable blind target turning up.
`docs/16-future-ideas.md` FI-05 stays the analysis record — update its status there too if this
is ever picked back up or closed.

## D-9 — Push CLAUDE.md's Routing rules down into the skills that own them

**What:** the `## Routing rules` section in `CLAUDE.md` holds operational rules that were
extracted from the historical narration during the 2026-08-21 context cut - e.g.
"`gen-block-modify-fix` must never pass `--allow-header`", "re-assert `block-layout --set
Standard` after every import", the `--claims` store-root rule. Each of these is owned by a
specific skill or command. The alternative shape is to move each rule into the skill that
governs it and drop the resident section.

**Why deferred:** owner decision, 2026-08-21. A rule that lives only in a skill is invisible to
any agent not running that skill, and several of these bite agents who are not running one -
the `--claims` and Portal-token rules in particular apply to whoever is orchestrating. Resident
was chosen for this pass; the cost is roughly 2 KB of context on every dispatch.

**Revisit trigger:** the routing set growing past ~10 lines, or a specific rule being shown to
be genuinely skill-local (i.e. no agent outside that skill can reach the situation it guards).

**Record for the cut this came from:** `CLAUDE.md` was 96,655 bytes at parent commit `a1eca27`
and 20,277 bytes after. The Commands-block content it shed was migrated into
`src/converter/README.md` and `src/openness-cli/README.md`; what was checked is recorded in
`docs/notes/claude-md-migration-inventory.md` (working file - delete once the cut has settled).

## D-10 — `lad-coder` reading raw SimaticML, and the IR gap that sent it there

**What:** two coupled fixes. They are recorded as one item deliberately, because doing the first
without the second makes the agent's job impossible rather than safer.

*(a) The boundary.* `lad-coder` must not read raw SimaticML. Owner's ruling, 2026-08-21, on
reviewing an instrumented `explain-plc-block` run: *"XML is for the converter only, except in
weird unexpected situations where there may be a problem with the xml, but still that is not for
the lad-coder to deal with."* So even the exception case - a suspect or malformed export - is a
converter concern to be reported upward, not something the ladder agent opens the XML to settle.

The run in question (`ir/test-project001/FB_ShredderSequencer.ir`, read-only) cross-read
`simatic-ml/test-project001/FB_ShredderSequencer.xml` and reasoned from its `Parts`/`Wires` -
specifically the `Mul(71).eno -> Convert(72).en` chaining - to establish execution order.

**The agent broke no stated rule.** That is the finding, not an excuse. Hard rule 7 is written
entirely around *writes*: "Edit only IR, never raw SimaticML ... don't hand-patch XML." Reading
is not prohibited by its letter. `.claude/agents/lad-coder.md` does not mention SimaticML at all.
The prohibition on reads is real but currently unwritten, so it needs stating in both places.

*(b) The reason it went there.* The agent read the XML because **the IR does not tell it execution
order**. IR groups statements within a network by instruction kind, and nothing in `ir/SPEC.md`'s
reachable path, the resident `CLAUDE.md`, or the `explain-plc-block` skill says that kind-order is
not execution order. Read literally in IR order, the block's Network 2 appears to run ten `MUL`s
into one shared TEMP before any `CONVERT` consumes it - i.e. all ten timer presets taking the last
product. The agent was one step from reporting that as a headline defect; it is false, and the
export's wiring is what disproved it. It caught itself only because an unrelated block comment
happened to mention kind-ordering.

This is ADR-0010 territory (*no IR that the AI cannot change*): a semantic the AI must reason
about is legible only in the sidecar/export, not in the readable IR.

**Ordering constraint:** fix (b) at or before (a). Closing the XML boundary on its own removes the
only route the agent had to a fact it needs, and the next run ships the false defect instead of
catching it.

**Fix shape:**
- Hard rule 7 restated to cover reads, not just edits, with the escalate-don't-investigate path for
  a suspect export named explicitly.
- The same boundary stated in `.claude/agents/lad-coder.md`, which is currently silent on it.
- Execution order made legible without leaving IR. Minimum viable: an explicit warning in the
  `explain-plc-block` skill's Method that IR statement order is kind-order, never execution order.
  Better: `ir/SPEC.md` states it where a reader will hit it. Best, and the actual ADR-0010 answer:
  the IR renders enable-chain order so the question does not arise.

**Why deferred:** recorded as a future fix at the owner's direction, 2026-08-21, rather than
patched inline during the CLAUDE.md context-cut validation.

**Revisit trigger:** picked up as its own change; or immediately, if any `lad-coder` report is seen
citing SimaticML as evidence again - that is this item recurring, not a new finding.

**Provenance:** surfaced by the second instrumented `explain-plc-block` re-run against the cut
`CLAUDE.md` (`docs/notes/context-cut-handoff.md`). That run also confirmed subagents receive the
post-cut file (19,593 bytes, `## Routing rules` present) and found no regression attributable to
the cut itself.
