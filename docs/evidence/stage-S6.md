# Stage S6 — Generation (incl. S6/S7 sequencing + process-rule decisions) — full evidence record

> Narrative and acceptance-test evidence for stage S6, split out of `docs/notes/stage-gates.md` on 2026-07-18 per `docs/16-future-ideas.md` FI-20.
> `stage-gates.md` remains the live status index; this file is the append-only
> detail record. Content below is verbatim as it stood in `stage-gates.md` at
> commit d8572c2. Append new S6 entries here, not to the index.

---

## S6 unlock: redesigned around FB/FC + CALL, two seed patterns admitted (2026-07-15)

S6 (generation from plain language) was named the project owner's own stated priority. Its entry
needs S3 done (already true) plus a seed pattern library per `docs/07-pattern-library-spec.md`. A
prior agent session had already started building this — a `docs/13-data-boundary.md` approval for
JOB9002 pattern extraction, tasks #171–180 (one per the roadmap's ~10 example patterns) — before being
stopped (confusion about parallel sessions, not a rejection of the work).

**First design, built then abandoned.** Initial work built a `template.ir` file format with a
reserved-sentinel `<SlotName>` syntax for unfilled parameter slots — a genuine open IR-format
question (nothing in `ir/SPEC.md` could represent this before), resolved via the project owner's
own explicit choice after being presented three options, verified collision-free against
`IrParser.cs`'s comparison-operator grammar by direct code reading, and proven end-to-end with a
synthetic example that round-tripped and compiled clean in `SampleProject`. All of that held up
technically — the design itself was the problem, not the execution. The project owner pushed back:
this project already has a complete mechanism for parameterized reusable logic
(`CallStatement`/`CallArgument`, proven since S1, plus TIA's own compiler doing full type-checking
on every `CALL` argument for free), and the new mechanism never reconciled with
`06-lad-conventions.md` C-106, which already mandates FB+UDT for repeated equipment as the site's
own pre-existing convention.

**Redesign: two kinds of pattern, no new IR mechanism.** Working through it together surfaced a
real distinction the original spec had collapsed into one shape:
1. **Equipment-instance** — a whole, proven, callable FB/FC (e.g. `MotorDOL`, all 14 networks —
   start/stop, fault detection, hours-totaliser, telemetry, alarm bits — not narrowed to a "start/
   stop" slice, which would have been arbitrary micro-decomposition of what's already one coherent
   unit at this site). Reuse = ordinary `CALL`. Nothing new to build — C-106 as already practiced.
2. **Repeated rung-shape** — the same logical shape repeated many times within one sequencing/
   mapping FC (e.g. `PlantAutoControl`'s per-equipment networks), kept inline rather than factored into
   calls because C-109/C-110 want an area-Main FC to read top-to-bottom like a table of contents.
   No template mechanism — a real, documented, sanitized example is what the AI drafts a new
   instance from by analogy, checked by the ordinary compile gate.

Explicitly **not a closed taxonomy** — plan review caught that "constructed edge-detection" (one of
the original 10 names) is a real third kind (a cross-block micro-idiom using shared `aEdgeMem[]`
storage, never factored into a call or concentrated in one FC), not built this round, not forced
into either of the two seeded kinds.

**Reverted cleanly**: the `<SlotName>`/`template.ir` sections in `docs/07-pattern-library-spec.md`,
the pointer paragraph added to `ir/SPEC.md` (replaced with a short note on the design question and
how it was actually resolved, keeping the historical record rather than deleting it), and
`docs/13-data-boundary.md`'s mechanism wording (the underlying JOB9002-extraction approval itself was
unchanged, just the description of what gets built from it).

**Two seed patterns, both ADMITTED 2026-07-15:**

- **`patterns/motor-dol/`** — all 5 admission criteria fully met. `MotorDOL`/`MotorStarter`, 8 real
  instances. Round-trips confirmed (block, instance DB, and calling network all covered by
  `Converter.Tests/PatternExampleTests.cs`). Compiles confirmed: discovered along the way that
  `SampleProject` already had `MotorStarter` + 9 real instance DBs + `PlantAutoControl` (the
  sanitized `PlantAutoControl`) sitting from earlier S1 work, all showing `IsConsistent = false` despite
  a clean device-level compile — the exact known quirk `docs/notes/openness-quirks.md` already
  documents (device compile doesn't clear `IsConsistent` after `Import()`; block-level compile
  does). Cleared by compiling all 21 affected blocks individually in dependency order; whole project
  now compiles clean, 0 errors, 0 warnings. `examples/` uses a real `CALL` site pulled directly from
  `PlantAutoControl` (network 8, "Overband Magnet") rather than a constructed one.
- **`patterns/chained-permissive-enable/`** — ADMITTED with criterion 3 (a newly-drafted instance)
  accepted on partial evidence rather than fully met, recorded honestly rather than quietly
  inflated. `examples/` has three real excerpts covering the documented variation (clean baseline,
  richer `RunningFB` + plant-wide-flag enable source, optional pre-start latch), plus a fourth,
  newly-drafted instance (`MotorStarterInst5`, "Drum Separator," a genuine chain-head with no
  equipment-to-equipment dependency) explicitly labeled as composed *with* prior knowledge of the
  real answer, not blind — the pattern's own documentation had already been read in full while
  building it, so drafting "fresh" from the same content wasn't a fair test. A genuinely blind
  attempt was tried first, using JOB9002's separate, untouched station_1 `PlantAutoControl` — one candidate
  network was partially read before a line-by-line stop could catch it, and a second turned out to
  be VSD-driven with an edge-detected `RCOIL` `FaultReset`, a real structural variant this pattern
  doesn't document yet. Project owner's own call: accept the current evidence and move on rather
  than chase a genuinely blind instance now — recorded in `pattern.md` as an outstanding gap, not
  resolved.

**Durable artifacts**: `patterns/_templates/` (two `pattern.md` templates, one per kind, plus a
README on the pre-writing checklist — re-ground fresh, sanitize, verify round-trip, don't fill
"Admission status" with optimistic guesses) — built specifically so the *next* pattern doesn't need
to reverse-engineer structure from the two existing examples. `Converter.Tests/PatternExampleTests.cs`
gives `patterns/` committed content the same standing round-trip regression guarantee `ir/reference/`
already has, extensible with new `[InlineData]`/`[Fact]` entries per new pattern rather than a
separate mechanism each time.

## S6 first real proof: test-project001 build (2026-07-15)

The first genuine end-to-end exercise of the S6 workflow (`CLAUDE.md`'s own "Workflow for logic
generation") against a real, plain-language functional description (genericized from a real
supplied spec — `docs/13-data-boundary.md` covers the sanitization), not a synthetic exercise.
Built out `test-project001` from 5 blocks to a complete subsystem: `DB_Settings`/`DB_Controls`/
`DB_Alarms`/`DB_AnalogInput`, `FB_PusherControl` + `UDT_PusherIO` + instance DB (new, C-118–C-125
stepped sequence), `FB_MotorFwdRevSystem` (real, imported unmodified) + instance DB, wired via
CALL-site corrections rather than FB edits (see below), `FB_ShredderSequencer` + `UDT_
ShredderSequencerIO` + instance DB (new stepped sequence, plant-level), `FC_ControlMain` (new
orchestration layer), `FC_AlarmsMain` (9 networks, C-501-literal alarm packing), `OB1 Main` wired
per C-109/C-110. Every block compiles clean individually and as a whole device (0 errors, 0
warnings) — full presentation (IR diff, intent, compile evidence) delivered per the S6 workflow's
own step 5.

**`--synthesize` grew from a narrow v1 (plain contact/coil chains only) to a genuinely broad v2**
covering nearly every instruction kind this build needed: `Expr.Compare` (as an ordinary chain
position), `TON`, `MOVE`, `MUL`/`ADD`, `CONVERT` (scoped to the Real-seconds→DInt-milliseconds HMI
idiom), and zero-argument `CALL`. Five real, previously-undiscovered bugs found and fixed along the
way, each with regression coverage: a duplicate-instance-UId collision (Timer/Call sidecars were
double-registering their own instance reference), multi-instance TON `ET`-wiring (needs an
`OpenConnectionSidecar`, unlike a standalone TON), a decimal-literal misparse (`1000.0` read as a
dotted tag path), a `LocalVariable`-vs-`GlobalVariable` scoping bug covering *every* reference in a
synthesized block (root-caused from the project owner's own "you need a `#` prefix for internal
FB/FC variables" hint), and a `--synthesize`-produced FB's own instance DB reliably getting an
invalid `DB0` from `create-instance-db` (worked around by hand-authoring the instance DB `.ir` with
an explicit number — `docs/notes/openness-quirks.md` has the full story). `BlockSourceWriter` also
gained two OB-specific fixes (Output/InOut and Return sections are invalid for an OB, previously
emitted unconditionally) — the first time this project ever wrote *new* content into an OB rather
than round-tripping an empty one.

**A real architectural anti-pattern was caught by the project owner, not by process**: an early
`FB_ShredderSequencer` draft hardcoded references to `iDB_PusherControl`/
`iDB_MotorFwdRevSystem_Shredder` inside its own logic ("its not good practice to have a global iDB
in a FB"). The standard S7 fix (InOut parameters typed to another FB) was investigated and refused
— wired InOut call-arguments are ungrounded/unbuilt in this converter (`CallArgument`/
`CallArgumentSidecar` only model Input/Output), and inventing that shape under time pressure was
rejected in favor of the alternative that was actually built: strip the sequencer down to plain
scalar IO, move every real instance name and `CALL` into a new orchestrating `FC_ControlMain` —
matching the real `PlantAutoControl` FC's own structure in the reference project. Written up as C-127.

**A genuine, still-open gap, deliberately not papered over**: the two overcurrent-level
comparisons (`ShredderMotorCurrent > OvercurrentSetpointMedium/High`) are Real-vs-Real, which
`BuildCompareStep`'s hardcoded `SrcType="Int"` can't handle — rather than invent an unproven
"AutomaticTyped Compare" XML shape with no real grounding anywhere in the project's own corpus
(unlike `Mul`, which *is* confirmed real), both conditions were wired to a permanently-false
`NOT AlwaysTrue` placeholder, flagged loudly (on `OvercurrentTripped`'s own member comment, in the
network comment, and in the presented compile evidence) rather than silently shipped as if it
worked. Needs a grounded typed-Compare capability, or real hardware, before it's functional.

**Two new LAD conventions came directly out of engineer review of the delivered blocks**: C-126
(group by function — a timer, its consumer, and its transitions belong together, not batched by
instruction kind into a block-wide "Timers" network far from what reads it) and C-127 (the
encapsulation rule above). Both `FB_PusherControl` and `FB_ShredderSequencer` were restructured
network-by-network to comply, verified by tracing every cross-network dependency against
`ir/SPEC.md`'s statement-kind ordering (also newly documented this session — see its own "Statement-
kind ordering" section) and by parsing the real re-exported TIA content back to IR text to confirm
every original condition survived byte-for-byte.

## S6 direction adopted: staged generation pipeline + simplicity retrospective (2026-07-16)

The project owner's verdict on the test-project001 build (previous section): functionally right — "it has
worked functionally very well" — but the ladder itself overly complex and obtuse, with an explicit
priority order stated for generated LAD: **function → readability & simplicity → efficiency**. The
owner proposed restructuring S6 generation around skills and isolated-context agents; the analysis
that shaped the adopted design (why the complexity happened, what the pipeline must include) and
the owner's approval both happened in-session, then were executed as three committed units.

**Adopted (ADR-0004, `docs/15-generation-pipeline.md`, commit `ab3efd2`):** a 13-skill staged
pipeline — Analyse (`gen-spec-analysis`, `gen-pid-analysis`, `gen-io-tags`, `gen-reconcile`),
Design (`gen-architecture`, `gen-alarm-design`), Build (`gen-block-coding`, `gen-integration`),
Check (`review-conventions`, `review-functional`, `review-simplicity`, `audit-artifact`), Entry
(`generate`) — handing off through committed artifacts (`gen/<project>/`), never conversation;
reviewers run fresh-context with read-only tools and never see author reasoning (the AI form of
the blind-review standard pattern admissions already use); a pipeline-wide tag-status rule
(`exists`-verified-by-grep vs `proposed`; coding refuses `proposed`) so multi-stage handoff can't
launder hard rule 3; two hard engineer gates (architecture sign-off before coding; the existing
step-5 presentation); a scale-down rule so small requests skip stages but never gates/reviewers.
Skills are built in leverage order (rules → reviewers → architecture → analysis → orchestrator),
each validated before the next — nothing exists yet beyond this design; docs/15's build-order
table is the ground truth. CLAUDE.md's 5-step S6 workflow was wrapped (now the block-coding inner
loop), not replaced. Doc 06's preamble now carries the priority order, each tier mapped to the
check that enforces it. ADR numbering note: 0003 left reserved — `docs/13-data-boundary.md` has
pointed at it for the data-boundary decision since the doc suite was written.

**test-project001 became a committed corpus (commit `b14be52`):** `.gitignore` anchored to
`/test-project001/` (the live TIA folder stays ignored; the extracted content no longer is), then all
17 blocks + 3 UDTs (`UDT_PusherIO`, `UDT_ShredderSequencerIO`, `MotorFwdRevIOSet` — enumerated by
grepping the exports, since `list` can't enumerate types) + the default tag table exported to
`simatic-ml/test-project001/` and converted to `ir/test-project001/` (21/21 clean `to-ir`, including OB1
`Main` — the deferred OB quirks are on the write path, not read). Four blocks + one UDT hit the
known `IsConsistent` export refusal ("Inconsistent blocks and PLC data types (UDT) cannot be
exported") and cleared via the documented block-level-compile-then-retry (all compiled 0 errors;
the only diagnostic was a device-level warning about IO points absent from the configured
hardware — expected for a sandbox project, recorded not hidden). Data boundary verified rather
than assumed: the corpus was scanned against `sanitization/Kestrel Shredder Systems.map.json`'s real-name
keys (case-insensitive; word-boundary for the short model codes) — zero hits, so docs/13's
"nothing identifying appears in test-project001" claim now has a checked basis. This corpus is the
durable S6 output and the standing validation corpus for reviewer skills.

**Retrospective delivered (`docs/notes/test-project001-retrospective.md`) — owner pass PENDING:**
mechanical baseline first (`converter review`, 17 findings, 16E/1W): both generated FBs are
*clean* — every inside-a-block mechanical finding lands on the real, imported
`FB_MotorFwdRevSystem` (TONR, packed alarm word, untitled network, no header) — while the
generated FCs/DBs/iDBs owe C-201 header comments. The headline, stated in the doc itself: the
owner's complexity verdict is about things **no current rule names**. Twelve findings (duplicated
7-term cycle-start condition vs the same build's own named-`StopCmd` fix; 31 bare UDT members
despite member-comments being an owner-requested capability; `NOT AlwaysTrue` meaning both
"deliberate constant" and "known gap" with no visual distinction; step-range predicates that
silently absorb future inserted steps; sibling FBs using two different settings-access policies;
no OB100/`DB_PLC`/C-111 machinery while `Step` and latches sit RETAIN; a dead `Pusher_Local_Remote`
input — grep-verified; buffer members in `Snake_Case` vs the pattern's `PascalCase` vs C-001's
written `camelCase`, which nothing follows) distilled into **7 candidate C-6xx rules** (§4, each
with severity + S4-style checkability bucket + accept/reject checkboxes) and **6 adjudications**
(§5, existing rule-vs-practice tensions: the site's own batch "HMI Times" idiom vs C-126's letter,
settings policy, member-case, C-109 scale-down, startup machinery scope, C-504 applicability).
The real block is documented as the read-only contrast case calibrating the rule set — every
candidate rule catches something in it too — and §8 records what the generated code did *right*,
so accepted rules don't overcorrect. Converter suite green after the day's work (463/463).
Honest status: S6's exit criterion (ten requests) is unchanged and not advanced by any of this —
this is the workflow that future requests run through, not exit progress.

## S6: retrospective owner pass folded — C-6xx live, settings policy resolved by faceplate context (2026-07-16)

The project owner reviewed `docs/notes/test-project001-retrospective.md` in full and answered every
checkbox and adjudication (recorded in the doc itself, §4/§5/§6/§9 + resolution in §10). Headline
outcomes, all now folded into `docs/06-lad-conventions.md`:

- **All seven candidate rules accepted** — C-601/C-603/C-604 as drafted, C-605 bumped to *error*
  by the owner ("this should always be true"), C-602 with a documented within-network fan-out
  exception (confirmed reading: C-601 owns cross-network duplication, the exception owns one
  condition driving several coils in one network), C-606/C-607 with justification text directed to
  the block comment. The owner's repeated "title short / comment detailed" note was generalized,
  with explicit confirmation, into a new **C-203**. Doc 06 gains a "Simplicity & readability"
  section holding the set.
- **The §9 principle codified**: AI-generated code faces harsher scrutiny than a human author's —
  one failed reading discredits the pipeline — so generated LAD must survive a skeptic's *single*
  reading; "the real site block does the same" is never a defense. Written into doc 06's preamble
  (reviewers err toward flagging; "defensible" is not a pass) and saved as standing agent memory.
- **Settings adjudication (5.2) resolved by new context, against the draft proposal**: the site
  uses HMI faceplates for nearly all equipment, and faceplates bind the UDT instance — so
  per-instance settings (including a sequencer's own step timings) belong in the instance UDT,
  and the draft's "plant-singleton may read DB_Settings directly" split was wrong. C-307
  sharpened, C-308 extended to **"a settings member has exactly one writer: the HMI"** — logic
  never writes it *and orchestrating FCs never scan-copy into it*, naming the concrete test-project001
  trap found during the read: `FC_ControlMain` MOVEs `DB_Settings.PusherX` over the pusher's UDT
  settings members every scan, so any faceplate edit would silently revert one scan later (the
  setting existed in two homes with a cyclic copy between them). C-122 reworded to match.
  test-project001's own settings rework is a queued S6 request, not silently done.
- **Other rulings**: C-001 members are PascalCase (practice wins over the never-followed
  camelCase; `Snake_Case` buffer members = legacy, renamed at next touch); C-109 gains the
  IO-mapping direct-call exception (wrappers stay the rule elsewhere; C-304 aligned); C-126 gains
  the HMI-Times batch exception *with* a mandatory pairing comment (the 6.1 tooling question
  resolved as an authoring rule — grammar change and converter-emitted annotations both rejected:
  corpus-wide diff churn / losslessness violation); startup-state machinery recorded as an
  accepted demo-panel omission ("should ideally have, do not need to fix right now") and a
  permanent gen-architecture checklist line; C-504 suppression carried to the future
  alarm-design stage.
- **Grounded along the way**: SimaticML carries `HeaderAuthor`/`HeaderVersion`/`HeaderFamily`
  block attributes (empty/0.1 in test-project001) that the IR currently drops entirely — queued
  converter work item to carry them as IR header lines, which is the only path to C-201's
  author/revision ever becoming mechanically checkable. Struct-inside-standalone-UDT round-trip
  needs one proof before the grouped-UDT `Set` sub-struct design can be committed to (anonymous
  Struct members are proven for FB statics via `EquipmentControlSystem`, not yet for `SW.Types.PlcStruct`).

Next per the docs/15 build order: `review-simplicity`, validated against the test-project001 corpus
under the stricter-bar principle.

## S6: review-simplicity built and blind-validated; UDT member-comment gap grounded (2026-07-16)

**The skill (docs/15 build-order step 2).** `.claude/skills/review-simplicity/SKILL.md` — the
tier-2 reviewer. Authored following the newly-installed `skill-creator` plugin's guidance
(explain-why over bare musts, concrete recipes, exact report template, pushy trigger description):
blindness declaration up front, the one-reading walk as pass 1 (judgment before grep — the
retrospective showed the worst defects had no citable rule until a cold read), per-rule sweep
recipes for C-601–C-607/C-203/C-126, calibration list (mapping rail, C-121 verbosity,
imported-real regime, pattern-vs-rule tensions flagged never adjudicated, tier boundaries), and
the stricter-bar disposition (err toward flagging; functional-looking discoveries reported as
tier-1 candidates, not ruled on).

**Blind validation — the docs/15 reviewer model exercised for real.** A fresh-context subagent
got only the skill file, the binding docs, and `ir/test-project001/` (21 files), explicitly barred
from the retrospective (the expected-findings anchor). Result, preserved verbatim with the
comparison in `docs/notes/review-simplicity-validation-2026-07-16.md`: **every material known
finding independently reproduced** (duplicated cycle-start compound with the near-match diff;
both bare UDTs; all three ranged step predicates; the four bare AlwaysTrue constants; the
settings-access split — sharpened into a three-way table correctly identifying the *real* block
as the C-307-compliant one; Snake_Case buffers; dead Pusher_Local_Remote; the mistitled buffer
network), calibrations honored, format followed. Only miss: the retrospective's own
"noted, not pressed" F-4. **Verdict: validated.** Method caveat recorded: blind executor,
non-blind examiner (the anchor and the skill share an author-session); the S4-style
owner-independent comparison remains available before gate-grade reliance.

**The run went beyond the anchor — including two tier-1 (functional) candidates on the
"functionally working" build:** `IO.InCycle` is consumed into physical output `DQ8_SYS_InCycle`
but written nowhere (in-cycle lamp permanently off); `DI4_SYS_CycleStop` is commented "(NC)" in
the tag table but mapped non-negated (as wired, an NC stop button holds StopCmd permanently true —
either polarity or comment is wrong; the input-mapping pattern's negated variant exists for this).
Plus: `RecentStart := AlwaysTrue` in FC_ControlMain fights the motor FB's own management of that
bit; `HandReverse` wired to a never-read member; `DB_Input.Infeed_Conv_Running` mapped and never
consumed; 58/98 tag-table entries are unreferenced legacy noise; the **admitted motor-dol
pattern's own example breaches C-126's new exception condition** (no scheme comment on its "HMI
Times" network, plus an untitled network) — flagged as a pattern-vs-rule tension for owner
ruling, exactly per the skill's calibration rule; and suspected defects in the imported-real
block (HandPosEdge double-write with HandNegEdge never referenced; a self-annihilating
`Pasue AND … NOT Pasue` term; an hours-counter edge guard that reads its own memory after
same-network update) — labeled context + verify-against-TIA, not fix demands (hard rule 7).

**UDT member comments: two grounded discoveries (both queued as converter work).** (1) A
round-trip proof of a nested-Struct UDT with member comments died immediately and *correctly*:
`UnsupportedConstructException — member comments are only confirmed on an ordinary
Static/Input/Output/DB member (WriteMember), not here` — **TYPE/UDT members cannot carry comments
through the converter at all**, so C-605 (error) is currently unsatisfiable on interface UDTs
via this toolchain, for the flat and sub-struct designs alike. (2) Grep over the fresh exports
finds **zero member-line COMMENT tokens anywhere** — including the Static-member comments the
test-project001 build demonstrably wrote (stage-gates' own S6 record cites OvercurrentTripped's member
comment; the fresh export shows the member bare) — so member-comment **persistence through
import→TIA→re-export is unverified** and possibly broken; the sequencer's two "see member
comment" pointers now dangle. Verify persistence before relying on C-605 at all. Release
converter rebuilt meanwhile (queued item closed): `review` verb confirmed present in the Release
binary.

**Settings sub-struct idea (owner's, this session): partially grounded.** The structure-only
round-trip proof (nested anonymous Struct inside a standalone `SW.Types.PlcStruct`) converts
IR→XML cleanly with comments stripped; the live import/compile/re-export leg was still running
when this entry was written — its result lands in the next entry. The comment gap above applies
to *both* variants (sub-struct and separate Settings UDT) equally.

## S6: owner-directed fixes — InCycle lamp and DI4 stop polarity (2026-07-16)

The two tier-1 candidates from the blind review were ruled **real mistakes** by the owner, with
definitions supplied: the In_Cycle lamp is on **whenever any piece of equipment is running**, and
the DI4 NC polarity is **absorbed at the input map** (not in logic). Fixed as S6 sandbox
iteration — the same established mode as the test-project001 build's own C-126/C-127 restructuring of
already-imported blocks (test-project001 is S6's scratch; S7's gate concerns real-project
modification and stays untouched) — but with S7-style discipline applied anyway, as a live
rehearsal of it:

- **FC_Inputs N1**: `Cycle_Stop` rung now negates `DI4_SYS_CycleStop` per the input-mapping
  pattern's own negated variant (`network-2-negated-and-spare` — the canonical shape, C-304:
  polarity absorbed at the map layer only), with a network comment reconciling the tag's "(NC)"
  documentation with the rung. Buffer stays active-high for every consumer.
- **FC_ControlMain N7**: `DB_Output.In_Cycle` now ORs the five field running feedbacks
  (`Shredder_Run_Fwd_FB`/`Shredder_Run_Rev_FB`/`Discharge_Conv_Running`/`Infeed_Conv_Running`/
  `Pusher_PowerPack_Running`) — feedbacks, not PLC commands, so the lamp reflects what the
  machine is actually doing (stated in the network comment with the owner ruling's date). This
  also gives `Infeed_Conv_Running` its consumer, closing that dead-input finding.
  `iDB_ShredderSequencer.IO.InCycle` is now fully unwired; member removal is deliberately
  deferred to the queued settings-rework request (interface change — minimal-diff discipline,
  `11-review-workflow.md`'s "no drive-by edits"; same reason N7's "Output Mapping" mistitle was
  left for its own pass).
- **Method**: edited readable IR → `to-xml --synthesize` (fresh sidecars) → import → block
  compiles (both `Success`, 0 errors; the standing sandbox hardware-IO warning only) →
  whole-device compile **Success, 0 errors, 0 warnings** → re-export → to-ir.
- **Untouched-network invariance, proven not asserted**: pre-fix committed IR vs post-fix
  re-export, readable parts — exactly two hunks per block (the added comment, the changed rung);
  every other network/title/line byte-identical through the full TIA round-trip. The committed
  git diff is larger only because synthesize+TIA regenerated sidecar UIds (the known volatility);
  the semantic diff is those four hunks.
- Corpus updated in place (`ir/` + `simatic-ml/`) so the committed corpus keeps matching the live
  sandbox.

The owner also confirmed the diagnosis the blind run implied: InCycle "probably should have been
flagged at the functional description stage" — precisely `gen-spec-analysis` + `review-functional`'s
job in docs/15, which moves that pair up the build order.

Environment note: partway through this work every `Siemens.Automation.Portal.exe` instance
disappeared from tasklist (owner closed them after the earlier housekeeping note, presumably) —
the import job simply launched a fresh instance and completed; the still-running SampleProject
UDT-proof job from earlier was likely orphaned by the same closure and will hit its own timeout —
its structure result (IR→XML leg passed) stands, the live leg gets re-run in the next batch.

## S6: Tier-1 adoption — preflight/playbook/digest/telemetry wired into the loop (2026-07-16)

The four already-built FI items from the merged worktree branch (FI-13/14/15/16) are now adopted
pipeline behavior, not just available tooling — docs-only batch, no code changes (one Release
rebuild, since the pre-merge binary predated the new verbs; both commands then verified live
against the corpus before being documented: `preflight FB_PusherControl` → CLEAN exit 0,
`preflight FC_ControlMain` → its known C-201 finding, exit 1, disclaimer line present; `digest`
output sane).

- **FI-13 preflight**: mandatory before every import (CLAUDE.md step 4 + docs/15 "Inner-loop
  tooling"). Bar set at **zero findings** — a consciously-accepted finding needs the engineer's
  explicit recorded OK. Rationale: the stricter-bar principle (generated content imports clean of
  known findings); legacy blocks are unaffected because only content about to be imported gets
  pre-flighted. Framed everywhere as a filter before the compile gate, never a substitute (hard
  rule 4 — the tool prints that disclaimer itself).
- **FI-14 playbook**: first lookup on any compile failure; entries are grounded hypotheses to
  verify; proven new error→fix pairs harvest back.
- **FI-15 digest**: per-stage policy decided in docs/15's isolation model — reviewers always full
  IR (S2 exhaustiveness lesson; the tool's own contract agrees), analysis/design/entry may digest
  for orientation, build stages read full IR of what they touch; digests derived fresh, never
  stored.
- **FI-16 telemetry**: one line per stage run (incl. `manual:<stage>` and abandoned) in
  `gen/<project>/telemetry.log` per `docs/notes/gen-telemetry.md`; no backfill — first rows come
  from the next real generation run. This log is what settles FI-12 with data.

CLAUDE.md's command block also gained `converter review` alongside the two new verbs — it had
never been listed there despite existing since S4; the operating manual now names every converter
verb that exists. FI entries in docs/16 updated with adoption pointers per that doc's lifecycle.
FI-07 (Portal janitor) remains the next new build, pending the owner's go-ahead.

## S6: four-stream parallel batch — reviewer suite complete, corpus corrected, plant-level findings (2026-07-16)

Owner-directed batch: FI-07 parked (owner's call — trust stays user-side at prototype stage;
docs/16 carries the reasoning + revisit trigger), and the parallel-safe work identified and run as
**four plan agents → owner approval → four executor agents in isolated worktrees**, zero Portal
anywhere, shared housekeeping centralized into this consolidation. Transient API errors
interrupted three runs; all resumed from transcript with no work lost. The auto-mode permission
classifier twice paused launches pending visible owner sign-off (site-doc access; admitted-
pattern edits) — the owner's explicit "I approve of these agents" message resolved both, a
correct-shaped check worth remembering when orchestrating.

**Stream A — `review-conventions` (pipeline skill #9): built + blind-validated.** Wraps
`converter review` verbatim (anti-drift contract: the tool's findings are embedded never
re-derived; disagreement = converter bug report — the blind run correctly filed one: C-003's
`iDB_<FBName>_<Instance>` sub-clause is unenforced) + a four-group AI pass (bucket-A grep sweeps
with self-retiring NotApplicable-gap hand-checks, bucket-B cross-block tables, bucket-C judgment
items, standing declines). Validation: fresh-context run over both corpora — **drift check passed
byte-identically (46/46 findings, 315/315 rule-status lines)**, all nine must-finds + both
should-finds found, all four negative controls held. `docs/notes/review-conventions-validation-
2026-07-16.md`. New tier-1 candidates from its run: `FaultFB` COIL-vs-RCOIL conflict, step-40
missing dwell timer, unset motor timing start values.

**Stream B — requirements register + `review-functional` (pipeline skills #1-output + #10): built
+ two-phase-validated.** `gen/test-project001/requirements.md` — the first pipeline artifact ever —
69 REQs + 15 open questions from the genericized supplied spec (`manual:gen-spec-analysis`;
contamination rule enforced: REQ text only from sources, corpus touched for tag-status greps
only; both commits gated by the real-name scan — 0 hits/14 keys, counts-only evidence). First
telemetry rows live in `gen/test-project001/telemetry.log`. Validation phase 1 ran against the
**pre-fix corpus** (19b2022~1): both owner-ruled bugs independently rediscovered blind (In_Cycle
`unimplemented`, DI4 `contradicted` with the exact inversion mechanism) — the arc catches what it
was built to catch. Phase 2 (current corpus): both fixes regression-positive. `docs/notes/
review-functional-validation-2026-07-16.md`.

**The functional review's plant-level verdict (69 REQs: 28 implemented / 18 partial / 11 disarmed
/ 7 unimplemented / 4 contradicted / 1 out-of-scope confirmed), all statically traced — owner
rulings queued in AITODO:** the build as committed **cannot start** (nine unconfigured
`DB_Settings` zeros are live: `DischargeConveyorTimeout`=0 aborts the start sequence at step 20;
the imported iDB's own `FTTime`=0 latches fail-to-run one scan into any motor start — a source
the register's Q-02 didn't cover); the shredder **can never run in reverse** (the generated 6 s
step-30 window sits entirely inside the imported FB's 8 s rising-edge reversal pause — structural,
survives any Q-02 configuration; also hollows the overcurrent retry's jam-clearing); **E-stop
recovery auto-restarts the plant** siren-less (no healthy member in the sequencer UDT +
`RecentStart := AlwaysTrue` strap defeating the imported FB's press-to-arm semantics); the
**pusher is exempt from the stop command** (no stop/healthy input in its UDT — a mid-cycle stroke
completes after a stop press); jog release **auto-retracts** instead of stopping in place; a
**disabled pusher still runs its power pack and raises faults** (`Fitted` gates only cycle
launches). Six new owner questions (NEW-1…NEW-6) recorded in the validation note.

**Stream C — TYPE/UDT member comments + recursive `TypeIr` (converter): built, 11 new tests,
491/491.** `WriteTypeMember` now emits comments via the same helper as the proven `WriteMember`
shape (TODO(live-verify): no `SW.Types.PlcStruct` member-comment example exists in any committed
export — a later Portal task proves or loudly refutes it); two silent-corruption bugs fixed
(`ParseTypeMember` dropped comments; `TypeIr` flattened nested members — **which also proves the
earlier "sub-struct structure converts cleanly" note was a false pass**: output was structurally
wrong, and the orphaned live leg would have caught it); sanitizer extended to member comments;
`ir/SPEC.md` gains the TYPE/STATIC COMMENT grammar + the index-paired MUL/CONVERT subsection
(closing C-126's dangling pointer, `SidecarSynthesizer` claim verified in code).

**Correction to this file's own 2026-07-16 "member comments absent from ALL fresh exports —
persistence unproven, possibly broken" claim: wrong, and withdrawn.** Stream C proved the
committed FB XML exports carried all five member comments per FB the whole time (TIA persisted
them fine), and no converter drop path exists in current code — the committed corpus `.ir` was
stale because the original corpus conversion ran with the then-outdated **Release** binary (the
same stale binary that lacked the `review` verb), which predated member-comment support and
silently ignored the elements. Corpus repaired by regenerating both FB `.ir` files from their
committed XMLs (commit `a11c6c4`; the only delta: 5 COMMENT tokens per FB; mechanical review
count unchanged at 17). This also **resolves the blind simplicity run's "dangling member-comment
pointers" finding: the pointers were valid; the medium was lying.** What remains genuinely open
for the Portal batch is only the UDT-position (`SW.Types.PlcStruct`) shape + persistence.

**Stream D — motor-dol pattern C-126/C-201 fix: complete, after a textbook stop.** The stream's
own pre-edit provenance gate refused to proceed: the committed `MotorStarter.xml` twin was never
`to-xml` output — it was a `converter sanitize` product preserving TIA's document order (proven
by building the converter at the twin's own admission commit: byte-identical emission to today's;
UId multisets identical; a pure document-order permutation). Owner ruling: regenerate the twin as
its own reviewed commit (`05996de`) making `to-xml` the canonical provenance permanently; the
rerun then landed the 2-line edit exactly as predicted (`fb9f583`: N4 titled, N6 scheme comment;
.xml diff exactly the two MultilingualText values; full-cycle fixed point; review delta exactly
one finding cleared; pattern.md "Post-admission changes" records the sign-off).

**Batch-wide environment fix (owner-approved):** fresh worktrees materialized `*.ir`/`*.xml` as
CRLF (system `core.autocrlf=true`), failing all 12 byte-comparing pattern tests in any fresh
worktree and biting master during the Stream D merge. `.gitattributes` now pins both to `eol=lf`
(`f737fb9`); index content was already LF everywhere.

**State after consolidation:** all four reviewer-adjacent skills exist (`explain-plc-block`,
`review-simplicity`, `review-conventions`, `review-functional`); docs/15 build-order steps 0–4
done; suites green (converter 491/491, openness-cli 101/101, golden 14/14). Next: owner rulings
on the functional-findings wave → a test-project001 fix request (the settings zeros + structural
items are S6 sandbox iteration with the now-standing invariance discipline); `gen-architecture`
(build-order step 5); the deferred Portal batch (UDT member-comment live verify + sub-struct
re-run, now meaningful post-TypeIr-fix). The blind-run independence caveat stands batch-wide:
blind executors, non-blind examiners — the S4-style owner-independent comparison remains the
stronger form if wanted before gate-grade reliance.

## S6: UDT member comments live-verified; sub-struct settings design fully grounded (2026-07-16)

The Portal task recorded hours earlier ran same-day against SampleProject: a commented flat UDT
(UDT_CommentProof) and a commented nested-sub-struct UDT (UDT_NestProof2) were authored in IR,
converted, imported (--type), type-compiled, re-exported, and converted back. Both round-tripped
to byte-identical IR with comments intact, flat and nested positions alike. Three things proven at
once: TIA Import() accepts the mirrored SW.Types.PlcStruct member-Comment shape; TIA persists UDT
member comments through a full round trip; Stream C's recursive TypeIr path holds live (the
earlier false-pass is now genuinely passed). Consequences: C-605 (error) is satisfiable end-to-end
on interface UDTs including one level down, and the owner's sub-struct settings variant
(UDT.Set.X) is fully grounded for the coming settings rework - single faceplate binding, one
type per equipment, mechanical one-writer checkability. TODO(live-verify) flags cleared in
DbInterfaceMembers.cs / converter README / ir/SPEC.md. Leftovers documented: the two proof UDTs
stay in SampleProject (no --type delete support; same status as MotorStarter_Instance).

## S6: fix wave 1 - full pipeline discipline exercised end to end on our own fixes (2026-07-17)

A batch of owner-ruled fixes against `test-project001` (drafted, signed off, imported, compiled,
invariance-proven, then run through all three check-stage reviewers) — project-specific detail
condensed out; two things worth keeping. First, a genuine playbook addition: "Element cannot be
found / check the consistency of the type used" on import means an interface dependency wasn't
compiled in the right order — UDT types need `compile --type` before their dependent iDBs, and
FBs before their iDBs, whenever an interface changed; fixed by re-sequencing the import order, not
by touching content. Second, the meta-lesson worth recording deliberately: the check-stage
reviewers found real defects in code written by the same session that had just finished writing
the rules those defects broke — one day old. That's the system working as intended; the
stricter-bar principle (`docs/06-lad-conventions.md` preamble) has teeth precisely because the
reviewers don't care who wrote the code or how recently the rule was adopted.

## S6/S7: pipeline-design correction — reuse-first carving, bounded manual coding (2026-07-17)

**A course correction to the pipeline design itself, the durable part of a long owner-questions
exchange otherwise stripped from this log as project-specific bookkeeping (2026-07-17; the
full per-item ruling history lived here briefly and is now redundant with `docs/06-lad-
conventions.md`, where every rule's own clarified text is the permanent record).**

Two genuine methodology lessons came out of it:

1. **Decomposition order matters because carving IS a reuse decision.** `gen-architecture` had
   drifted into deriving blocks from requirements first and mapping patterns onto them afterward —
   plausible, but a carving blind to the existing library can make reuse structurally impossible in
   ways no after-the-fact mapping repairs. Corrected to reuse-first: identify whole
   already-proven blocks first, then pattern-composed blocks, then modified library blocks, only
   then new/freeform — with REQ traceability kept as the correctness spine throughout, so a
   reuse candidate that only *almost* fits its REQs doesn't get force-fitted.
2. **A capability that isn't built stays theoretical no matter how many times it's needed.** Manual
   coding was written into the workflow for the exception case and quietly became the norm as
   volume grew — the deferred build-order priority for an actual coding skill was never revisited.
   Concrete cost: defects a coding skill's own write-time checklist would have caught (a
   reintroduced near-match duplicate condition, a placeholder-constant rule tension) instead
   reached review. Bounded going forward: once a real coding skill exists, ad hoc manual coding
   needs an explicit per-case waiver, not a default.

Also from this pass: `docs/notes/owner-questions.md` was adopted as a reusable pattern —
consolidate a large batch of accumulated questions into one priority-ordered doc, resolve each
into its permanent home (a rule, a register entry, a deferred-items note), then clear the batch
doc back to empty rather than let it grow forever. `docs/notes/deferred-items.md` was split out the
same way, for decisions already made in principle where only timing is open.

## S6/S7: agent-tasks dispatch board — first proven OB write path, a real converter gap found and worked around (2026-07-17)

The B-docket/C-item work this exchange produced (six tier-1 defects plus several rule
clarifications, all against the `test-project001` scratch project) was dispatched through
`agent-tasks/` — see that folder's own README for the concurrency pattern and what it taught about
multi-agent coordination specifically. Two tool-level findings from actually running it, worth
keeping independent of the project they were found against:

- **First proven OB *write* path.** Every prior OB round-trip proof was read-only (export/convert
  an existing OB); this run hand-authored a new `OB100` (`SECONDARYTYPE Startup`) from scratch,
  `to-xml --synthesize`'d it, and imported it clean. One genuine discovery along the way: TIA's own
  `Import()` silently corrects an OB's system-input parameter set to the real one for its declared
  `SecondaryType` — an authored OB100 that (wrongly) copied OB1's `Initial_Call`/`Remanence` pair
  came back from re-export with the actually-correct `LostRetentive`/`LostRTC` pair instead. Trust
  the re-export over a hand-copied assumption about system parameters.
- **A real converter gap (logged as D-6), and a workaround that held up under repeated use.** The
  converter has no supported path for adding one new statement to a network that already carries
  real sidecar data from a prior TIA export — `IrFormatException: Network N: IR has X move(s) but
  the sidecar records Y`. Per hard rule 7 (report converter gaps, never hand-patch the sidecar),
  the standing workaround is a whole-file cycle: strip the file's entire `SIDECAR` section, edit
  the readable IR, `to-xml --synthesize` the whole file, import, compile, re-export, `to-ir` to
  restore real sidecars for every network. Confirmed working every time it was tried, at the
  consistent cost of a much larger diff per use (full sidecar regeneration, not just the changed
  lines) — worth a real converter fix if this pattern recurs often, since a workaround that always
  works is easy to mistake for "not actually a gap."
- **The compile gate catches what static reading misses.** A reset-block draft assumed a member
  was a `Static` (visible via grep) when it was actually a `TEMP` (scan-scoped, no persistence,
  not externally addressable) — the block-level compile error ("Tag ... not defined") caught it
  immediately, before it could ship as a silent no-op. Grep tells you a name exists; only the
  compiler tells you what kind of thing it actually is.
- **`IsConsistent` staying `true` device-wide doesn't mean every block actually is** — the
  standing playbook pattern (device-level compile reports Success without clearing a stale
  per-block flag; `compile --block <name>` clears it) held for every block touched across this
  whole run, including one flagged inconsistent from the very first Portal touch and never
  actually edited — a pre-existing condition, not a regression, cleared the same way.

## Process rule: LAD/IR work moves to a dedicated sub-agent, no exceptions (2026-07-17)

**Owner decree:** the deliverable is an AI capable of programming ladder logic, not ladder logic
produced by whichever agent the owner happens to be talking to. From this point, the main
conversational agent never reads, writes, reviews, or explains LAD/IR content itself — it plans,
dispatches to the `lad-coder` sub-agent (`.claude/agents/lad-coder.md`), verifies the actual
diff/compile evidence handed back, and presents to the engineer. Recorded as CLAUDE.md hard rule 8.

Scope, settled after pushback and four clarifying questions (all "recommended" options chosen):
skill gaps don't create an exception — `lad-coder` still does skill-less stages manually, to the
same contract, rather than the dispatcher doing it inline; scope is the full set (IR edits, the
compile/import/export loop tied to a change, read-only `review-*`/`explain-plc-block` requests,
and `patterns/` edits); no size exception — a one-line fix goes through the sub-agent the same as
a new block; and a dedicated agent type was worth building rather than re-deriving a fresh prompt
per dispatch each time. PC-side tooling (`openness-cli`, `converter`, `extract/`, `tests/golden`)
is explicitly unaffected — normal software rules, no dispatch required.

This generalizes a pattern already proven useful here: docs/15's adversarial-review-in-fresh-context
design already isolates the review skills from whichever context wrote the logic being reviewed,
specifically so the reviewer isn't biased by having just authored it. This extends the same
isolation to authorship itself, not just review.
