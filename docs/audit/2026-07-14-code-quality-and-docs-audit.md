# Audit — 2026-07-14: code quality + docs-vs-code accuracy check

**Requested by:** project owner, after the full `PlantAutoControl` round-trip proof and the full-cycle
verification pass across every block in `SampleProject`.
**Scope, chosen by the project owner** (not the full option set offered): (1) docs-vs-code
accuracy — read the doc suite, cross-check specific claims against actual code/tests/git history;
(2) code quality & architecture — review `src/converter/`, `src/openness-cli/`, and the three test
suites. Hard-rule/data-boundary compliance auditing was explicitly **not** requested this round.

**Baseline:** all three suites green before starting — 291 converter, 101 openness-cli, 11
golden-harness. Precedent: `docs/audit/2026-07-11-stage-and-docs-audit.md` covered the doc suite
up to S1 item 7; this audit picks up from there, since a great deal has shipped since (Sub/Div/Le,
TON family, the entire `PlantAutoControl` round-trip across three phases, DB Input/Output support, and
the 47-block full-cycle verification pass).

## Docs-vs-code accuracy findings

### 1. `AITODO.md` was severely stale — actively undermined its own stated purpose

`AITODO.md`'s own header says it's "the recovery point — read it first after any gap, before
trusting your own memory." It described "Current task: Phase 1 — `PlantAutoControl`'s 8 dependency FBs,
in progress," with `MotorFwdRevSystem`/`MotorVSDSystem` shown as blocked and `TomraControlSystem`
"not yet attempted," and said the work was "not yet committed."

In reality: all three of those FBs were fixed and proven compiling clean; Phase 1 (all 8 FBs),
Phase 2 (all 26 DB/tag-table roots), and Phase 3 (`PlantAutoControl` itself, full round-trip proven) are
all complete; and three separate commits (`e2fb301`, `a1f46d3`, `60ac96f`) had landed since this
file was last touched. A session recovering from this file, exactly as its own instructions direct,
would have been given a materially wrong picture of project state.

**Fixed:** rewritten to reflect current reality — "Current task: none in-flight," the three
completed phases moved to "Recently closed," and the two genuinely-open items (the deferred `Main`/
OB1 gap, the `Normalizer` Part-UId-volatility gap) listed explicitly as deliberately deferred, not
in-progress.

### 2. `CHANGELOG.md` was missing an entire day's largest milestone

The newest entry (top of file, dated 2026-07-14) was from the *early* part of today's work
("Phase 1 continued: `FilterUnitSystem`/`AirStarSystem` compile clean..."). Nothing followed it for
`Sub`/`Div`/`Le` support, the `Sanitizer` `BlockName` fix, `MotorFwdRevSystem` being fixed at the
source, Phase 2, Phase 3 (`PlantAutoControl` itself proving the full round trip — arguably this
project's single biggest milestone to date), the DB Input/Output extension, or the 47-block
full-cycle verification pass that found and fixed 5 more real bugs. Three commits' worth of
substantial work had no corresponding entry.

This is the same class of gap the 2026-07-11 audit found ("`CHANGELOG.md` had a Phase A entry but
no matching Phase B entry") — recurring, now at much larger scale.

**Fixed:** three new entries added, matching the existing level of detail, in the correct
chronological position (newest-first, above the entry that was previously at the top).

### 3. `ir/SPEC.md` — multiple stale claims, including a direct internal self-contradiction

- Line 136's own comparison table said "`Le` still unconfirmed" — but `Le` was confirmed real and
  built 2026-07-14 (`FC Scale`), completing the full IEC comparison family. **This directly
  contradicted line 878 later in the same file**, which correctly implied `Gt` was confirmed
  ("`Gt`... confirmed real and built, 2026-07-14") in one place while a different bullet near the
  bottom said "`Le`/`Gt` still unconfirmed real, not built" — two claims about the same construct,
  in the same document, disagreeing with each other.
- `Sub`/`Div` (confirmed real and built 2026-07-13, extending the `Mul`/`Add` arithmetic family)
  had **no documentation anywhere in the 902-line file** — not in the readable-form grammar table,
  not in the "Open items" chronological log. Two separate "Open items" bullets still explicitly
  said arithmetic beyond `Mul`/`Convert`/`Add` "remains out of scope."
- `Swap` had an "Open items" bullet calling it "a new, real, currently-unaddressed gap, not yet
  scoped into any item" — but `Swap` was resolved days earlier (S1 item 25, documented in detail
  elsewhere in the very same file). The bullet was simply never removed once superseded.
- `SECONDARYTYPE`/`INFORMATIVE`/`BAREPARAM` (all three added to the converter today, fixing real
  OB-round-trip and IR-text-fidelity bugs) had no grammar documentation at all.

**Root cause, worth naming explicitly:** this file (like `stage-gates.md`, per the 2026-07-11
audit's own finding #1) has both a "current status" layer (the readable-form table, the top-level
grammar sketch) and a chronological "Open items" narrative log below it. Updates land naturally in
whichever section whoever's working at the time happens to be touching — the *other* section then
silently drifts. This is now a **confirmed recurring pattern across two different files**, not a
one-off. See the process recommendation at the end of this report.

**Fixed:** `Le`'s status corrected and the table's own wording tightened to state the family is
complete; `Sub`/`Div` given a full grammar entry (mirroring the existing `Mul`/`Convert` entry's
own level of detail) plus a live-verification note; both stale "remains out of scope" bullets
corrected; the stale `Swap` bullet rewritten to point at its own real resolution; `SECONDARYTYPE`/
`INFORMATIVE`/`BAREPARAM` added to the file-shape grammar block with grounding notes matching the
document's own established style.

### 4. `--tagtable`/`--tagtables` undocumented in two places, one of them `CLAUDE.md` itself

The tag-table feature (`TAGTABLE`, `list --tagtables`, `export`/`import --tagtable`) is real,
built, tested, and live-verified — confirmed directly in `ArgumentParser.cs`'s own usage string and
in `CHANGELOG.md`'s existing entry for it. It was completely absent from:
- `src/openness-cli/README.md`'s own top-level command-summary table (the file whose own stated job
  is to be the authoritative flag/behavior reference `CLAUDE.md` explicitly points to).
- **`CLAUDE.md`'s own "Commands" section** — the single most authoritative document in this
  project, loaded as instructions in every session. Its own `converter to-ir|to-xml` line was far
  more seriously stale: it still described the converter as **"Contact/Coil-only slice today (S1
  walking skeleton)"** — a description from the very earliest days of S1, understating current
  capability by roughly twenty instruction-level constructs (comparisons, timers, `MOVE`/`WAND`/
  `CALL`/`SWAP`/`SCoil`/`RCoil`, the full arithmetic family, UDTs, tag tables, DB Input/Output).
  `delete`/`create-instance-db` were also missing from the command list entirely.

**Fixed:** both files updated — `CLAUDE.md`'s command block now lists every subcommand (including
`delete`/`create-instance-db`) and describes the converter's actual current scope instead of the
walking-skeleton-era description; `src/openness-cli/README.md`'s summary table now shows
`--tagtable`/`--tagtables` alongside `--type`.

### 5. `docs/13-data-boundary.md`'s "Scope, as approved" list for JOB9002 doesn't reflect actual usage

**Not fixed — flagged for your own call.** The recorded approval scope for the JOB9002 scratch
project lists two items (the A-01/A-02 spikes, and S1 IR/converter design grounding). It has
several later "Clarified" addenda but no entry covering the actual scale of what's since happened:
the entire `PlantAutoControl` round-trip effort — bulk export/sanitize/import of 8 dependency FBs, 26
DB/tag-table roots, and `PlantAutoControl` itself, all real JOB9002 content (sanitized before anything was
committed, per the existing rule, but extensively read/used in scratch along the way). This is a
data-governance record, not a mechanical staleness fix — I didn't want to unilaterally write a new
"approved scope" entry into a compliance-relevant document on your behalf. Worth deciding how (or
whether) to formally record this scope in that doc.

## Code quality & architecture findings

### Positive: exception-type discipline is clean and consistent

Zero uses of a bare `Exception`/`InvalidOperationException` as a *thrown* type anywhere in
`src/converter/` — every one of 229 throw sites uses one of the project's three purpose-built
exception types (`SimaticMlFormatException` for malformed/missing source structure,
`UnsupportedConstructException` for real-but-unconfirmed or deliberately out-of-scope shapes,
`IrFormatException` for IR-text parse errors). `src/openness-cli/` has 14 distinct, well-named,
`sealed` exception types. The two broad `catch (Exception ...)` blocks found (`Program.cs`'s
outermost CLI-entry-point handler; `OpennessGateway.cs`'s empty catch while probing candidate
Portal processes) are both legitimate and clearly justified in their own surrounding comments, not
a "swallow everything" antipattern.

### Positive: zero compiler warnings, no dead-code markers

Both C# projects build with 0 warnings. No `TODO`/`FIXME`/`HACK` markers anywhere in `src/` — this
project tracks open items in `ir/SPEC.md`/`stage-gates.md` narrative prose instead, consistently.

### Recommendation: `PartNode`/`DbMember` are large, deliberately-generic "kitchen sink" records, still growing

`PartNode` (`SimaticMl/Model.cs`) now carries 13 fields (`UId`, `Name`, `Negated`, `Cardinality`,
`TonVersion`, `TimeType`, `Instance`, `SrcType`, `BlockName`, `BlockType`, `CallParameters`,
`AutomaticSrcType`, `DestType`); `DbMember` carries 10. Most fields are null/false/default for any
given Part/member kind. This is a **documented, deliberate** choice (the file's own comment:
"every other Part kind is unaffected and the parser/writer already dispatch on Name") rather than
an accident, and it's worked well through ~20 instruction types added incrementally. Not a "fix
this" finding — a "watch this" one: each new construct adds 0-3 more optional fields to one shared
type, and at some point (not yet reached) a discriminated union or per-kind subtype would pay for
its own added ceremony by ruling out invalid field combinations at compile time. Worth a deliberate
look if the field count keeps growing at the current rate, not before.

### Real gap: proven live-TIA round-trips aren't protected by any permanent regression suite

`docs/08-testing-strategy.md`'s own Layer 1 strategy is golden-file round-trip **against the
committed reference project** — but the committed corpus (`ir/reference/`, `simatic-ml/reference/`)
is 7 blocks (`NodeStatusAlarms`, `PerimeterSafetyAlarms`, `CommsProcessData`, `AlarmWords`,
`EquipmentStatus`, `TimerSample`, `DB_Timers`), and none of them exercise `Mul`/`Convert`/`Sub`/
`Div`, comparisons beyond `Eq`/`Ge`, `CALL`, `SWAP`, `SCoil`/`RCoil`, `WAND`, `Not`, `TONR`, or
`TOF` — roughly 15 of the ~20 instruction-level constructs this converter now supports. Every one
of those was proven against real, Amber-tier, JOB9002-derived content instead — content that can
never be committed, so the proof exists only as a point-in-time narrative entry in
`stage-gates.md`. Concretely, from today alone: `TempAutoControlCheck.cs` and
`TempFullCycleNormalizerCheck.cs` — both real, both proved something true and important (`
PlantAutoControl`'s own full-round-trip losslessness; 40 of 47 blocks' `Normalizer` equivalence) — were
both deleted immediately after use, per this project's own established (and correct, given the
data-boundary constraint) discipline. Nothing stops a future change from silently breaking either
property; the only way to find out would be to re-run the whole live cycle again by hand.

`docs/08-testing-strategy.md` already anticipates part of the fix ("New LAD constructs found in
real projects get added to the reference project first, failing, then fixed") — this happens today
at the *unit-fixture* level (every construct has a genericized XML fixture and in-memory
parse/reduce/build/write tests) but not at the *whole-block, live-TIA* level the golden harness
itself is meant to prove. Not something to fix in this pass — flagged as the most valuable
follow-on for `tests/golden/` specifically, and noted in `AITODO.md`'s "possible next work."

### Checked, no issue found

Test/fixture organization (24 test files, largest ~500 lines; 56 fixtures for ~20 supported
constructs — proportionate, no obvious duplication); file sizes across both C# projects (largest is
`GraphReducer.cs`/`IrParser.cs` at ~1,100-1,400 lines each — large but not extreme, and the growth
is expected/incremental given the one-new-instruction-type-per-session pattern, not a red flag on
its own); `docs/04-design-philosophy.md`'s principle numbers (`#7`/`#10`) cited elsewhere in the
repo still match; `patterns/`/`extract/` are correctly still-empty placeholders (S5/S6 haven't
started, per the roadmap's own entry criteria).

## Outcome

Fixed directly (safe, mechanical corrections, no design decisions involved):
`AITODO.md` rewritten; `CHANGELOG.md` gained 3 missing entries; `ir/SPEC.md` had 6 distinct stale/
contradictory claims corrected and 2 new grammar sections added; `CLAUDE.md`'s command reference
refreshed (most consequential single fix, given it's authoritative every session);
`src/openness-cli/README.md`'s command table gained `--tagtable`/`--tagtables`.

Flagged, not fixed (needs your own call): `docs/13-data-boundary.md`'s JOB9002 approval-scope record
(finding 5); the `Normalizer` Part-UId-volatility gap (already flagged in `AITODO.md`); whether/when
to invest in `PartNode`/`DbMember` restructuring; whether/how to grow the committed reference corpus
to close the live-TIA regression-coverage gap.

**Process recommendation:** two different files (`stage-gates.md`, now `ir/SPEC.md`) have shown the
same failure mode — a "current status" summary drifting out of sync with a chronological detail
log below it in the same document. Worth a standing habit: whenever a narrative entry is added that
supersedes an earlier claim elsewhere in the same file, grep that file for the construct name being
resolved and update every mention, not just append the new entry.

All three test suites re-confirmed green after these changes: 291 converter, 101 openness-cli, 11
golden-harness (doc-only changes, no code touched this pass).
