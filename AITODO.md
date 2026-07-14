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

- **Reference corpus growth: 7 new blocks, 2026-07-14.** Project owner's own ask, after reviewing
  what's left before S1 is "done" in spirit: the committed `ir/reference/`/`simatic-ml/reference/`
  corpus only exercised ~5 of the ~24 instruction-level constructs this converter supports, with
  everything else proven only against uncommittable real JOB9002 content (today's own audit finding).
  Added `ThresholdAlarms` (comparisons), `SignalConditioning`/`DataHandling` (arithmetic/box
  family), `BooleanExtras` (standalone Not — first-ever live-TIA verification of it, SCoil/RCoil),
  `FBTimers`/`ScaleValue`/`TimingAndCalls` (TONR/TOF/CALL) — corpus now 14 blocks, ~20 constructs
  covered. Two pre-existing stale `.ir` files found and fixed along the way (sidecar text format
  predating S1 item 11), plus a real `RunAll` bug (TIA's own `IsConsistent` cascade corrupting a
  naive per-block batch verification — fixed with a proper three-phase `RunAllSettled`). All 14
  reference-project blocks verified together in one pass for the first time. Full story:
  `tests/golden/README.md` ("Reference corpus growth"), `docs/notes/stage-gates.md` (same
  section name). `WAIT`/`Jump` stay excluded (see "Open questions" below, unchanged);
  Modbus_Master/Modbus_Comm_Load's multi-instance form was considered and deliberately not
  attempted — see "Deliberately deferred" below, this one's a closed engineering call, not an
  open question for the project owner.
- **Instruction-coverage sweep + Phase 2 tiers 1–3, 5, and part of 6 (`Abs`/`LIMIT`/`T_SUB`/
  `T_CONV`/`Calc`/`MOVE_BLK_VARIANT`/`FillBlockI`), 2026-07-14.** Grounded all 36 remaining `JOB9002`
  blocks (both PLC stations), found 11 real currently-unsupported instructions, ranked into a
  6-tier plan. 7 instructions built, tested (45 new tests, 345 total), and live-verified —
  imported/compiled clean (0 errors) in `SampleProject`. Several real bugs found only by live TIA
  verification along the way (a misread `LIMIT` `DisabledENO` attribute; a hand-built fixture
  reusing one `Access` UId across two wires; a `FillBlockI` fixture using a plain scalar where the
  real destination is array-indexed) — see `docs/notes/stage-gates.md` for the full story. `WAIT`
  (Tier 6) built and tested but not live-verified — hit a distinct, still-open blocker (see
  "Open questions" below). `Jump` (Tier 6) investigated but deliberately not built, pending a
  design decision.
- **Tier 4 (`Modbus_Master`/`Modbus_Comm_Load`), 2026-07-14.** Built across all 7 files, 15 new
  tests (359 total, all green). Live verification: import clean; compile narrowed 6 errors -> 2,
  every fixed one confirming the converter's own modeling correct (fixture scope/type gaps, not
  converter bugs). The final 2 trace to a confirmed general Openness limitation — standalone
  system-FB instance DBs (`Modbus_Master_DB`/`MB_Master_Comm`) invisible to `SW.Blocks` entirely,
  same class as the earlier `CycleDelayReset` finding — not a converter bug, no Openness-exposed
  fix found. Left honestly unverified for full live compile, same standard as `WAIT`. Full story:
  `docs/notes/stage-gates.md` ("Tier 4 built and tested..."), general limitation writeup:
  `docs/notes/openness-quirks.md` ("Known constraints").
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

## Current task: none — Phase 2 tiers built, reference corpus grown; two open questions await the project owner

Tiers 1–6 are all built and tested (359 converter tests, all green). Tiers 1, 2, 3, 5
(`MOVE_BLK_VARIANT`), and `FillBlockI` (Tier 6) are fully live-verified. Tier 4
(`Modbus_Master`/`Modbus_Comm_Load`) and `WAIT` (Tier 6) are built/tested but live-verification
hit real, honestly-documented blockers outside the converter's own control (see "Open questions"
below). `Jump` (Tier 6) was deliberately not built, pending a design decision. The committed
reference-project corpus has since been grown from 7 to 14 blocks (2026-07-14, "Recently closed"
above) specifically to give the ~20 already-working constructs permanent, committed regression
coverage — independent of whether `WAIT`/`Jump`/Modbus's remaining gaps ever close. Nothing is
currently in-flight — the two items below need the project owner's input before any further
action.

## Open questions (need the project owner's input, not further unilateral work)

- **`WAIT` — built and unit-tested, but hit a genuinely different live blocker, still open.**
  TIA's own `Import()` rejects it — "An instruction with the name 'WAIT' cannot be found" — in
  `SampleProject` specifically, even though the regenerated XML faithfully matches the real
  `JOB9002` shape (confirmed not a converter bug: `MOVE_BLK_VARIANT`/`FillBlockI` imported into the
  same target project cleanly in the same session). Likely a library/technology-object dependency
  present in `JOB9002`'s own project but not in `SampleProject`'s — **not yet identified, needs the
  project owner's input** rather than guessed at further (adding a library/technology object to a
  project is a more consequential change than anything else done this whole plan). **Ask**: do
  you know what `WAIT` depends on / whether `SampleProject` should have it added, or would you
  rather verify `WAIT` against `JOB9002`'s own scratch copy instead (would need to extend the data
  boundary approval's scope — see `docs/13-data-boundary.md` — to cover writing new synthetic
  content into `JOB9002`, not just reading from it, which hasn't been explicitly approved yet)?
- **`Jump` was investigated and deliberately NOT built — needs the project owner's own design
  call, not a routine build.** Grounding it found `JMP` is genuine **cross-network control
  flow**: its `label` port references a new `Access Scope="Label"` node shape, and the actual
  jump target is a network-level `<Labels><LabelDeclaration></Labels>` element (a sibling of
  `<Parts>`/`<Wires>`) confirmed real in a *different* `CompileUnit` (network) than the `Jump`
  Part itself. No existing production models anything beyond its own single network — this
  needs a real IR-format decision (how should the readable form represent "network N can
  transfer control to network M"?) before any parser/reducer code gets written. Full grounding
  detail in `ir/SPEC.md`'s own `Jump` entry. **Ask the project owner how they'd like this
  represented before starting** — this is exactly the kind of design question, not
  implementation detail, that shouldn't be decided unilaterally.
- **`Modbus_Master`/`Modbus_Comm_Load` (Tier 4) — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct (every "tag not defined"/type-mismatch error cleared
  during live verification). What's left unverified is TIA accepting a standalone instance DB for
  either instruction in `SampleProject`: `create-instance-db` deterministically assigns an invalid
  `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor `MB_Master_Comm` is exportable
  from `JOB9002` (both invisible to `SW.Blocks` entirely — same class of limitation as `WAIT`'s own
  neighbor finding, `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). No
  Openness-exposed fix found. **Ask**: same question as `WAIT` in spirit — is this worth a
  source-side fix in `JOB9002` (the only path that resolved `CycleDelayReset`), or is
  Openness-API-only live verification for standalone system-FB instances simply out of reach and
  worth accepting as a documented, permanent limitation?

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
- A Sanitizer gap, newly confirmed real 2026-07-14: a Static member with
  `ExternalAccessible="False"` (`FB VSDSim`'s own `SpeedCalcArray`) hard-errors — already a
  deliberate, tested case (`GlobalDbWithNonDefaultAttribute.xml`), just never previously grounded
  as real. Would need a new `DbMember` field, `DbInterfaceMembers` parse/write changes, and a new
  IR-text marker (same shape as the existing `IsBareParameter`/`Informative` additions) — flagged
  rather than fixed mid-Phase-2-plan, since it's unrelated to LAD instruction coverage.
- **Modbus_Master/Modbus_Comm_Load's multi-instance form, considered during reference-corpus
  growth (2026-07-14) and deliberately not attempted** — unlike `WAIT`/`Jump`, this one's a closed
  engineering call, not something needing the project owner's input. Would have stacked two
  independently-unproven assumptions (whether these two instructions support multi-instance at
  all — never seen real, only the standalone `GlobalVariable`-scope form was grounded in `JOB9002`;
  whether a hand-authored DB with an FB-typed member, untested anywhere in this project, can stand
  in for a proper instance DB) for one FC's worth of optional coverage, disproportionate given
  everything else in that pass closed cleanly. Revisit only if a real grounded example of
  Modbus multi-instance usage ever turns up.

**Possible next work** (no explicit instruction yet — ask before starting):
- Build the `Normalizer` Part-identity fix above.
- Fix the `ExternalAccessible=False` Sanitizer gap above.
- S2 (read and explain) — the next roadmap stage once S1's gate review is formally signed off.

**Sanitization maps built and kept** (`sanitization/`, gitignored): all 8 dependency FBs' own maps,
plus `PlantAutoControl.map.json` itself and per-instance maps for all 26 DB/tag-table roots. Reusable
directly for any follow-on work against the same real project.
