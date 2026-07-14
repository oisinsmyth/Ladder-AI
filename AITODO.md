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

- **Instruction-coverage sweep + Phase 2 tiers 1–3, 5, and part of 6 (`Abs`/`LIMIT`/`T_SUB`/
  `T_CONV`/`Calc`/`MOVE_BLK_VARIANT`/`FillBlockI`), 2026-07-14.** Grounded all 36 remaining `JOB9002`
  blocks (both PLC stations), found 11 real currently-unsupported instructions, ranked into a
  6-tier plan. 7 instructions built, tested (45 new tests, 345 total), and live-verified —
  imported/compiled clean (0 errors) in `SampleProject`. Several real bugs found only by live TIA
  verification along the way (a misread `LIMIT` `DisabledENO` attribute; a hand-built fixture
  reusing one `Access` UId across two wires; a `FillBlockI` fixture using a plain scalar where the
  real destination is array-indexed) — see `docs/notes/stage-gates.md` for the full story. `WAIT`
  (Tier 6) built and tested but not live-verified — hit a distinct, still-open blocker (see
  "Current task" below). Tier 4 re-grounded but not built; `Jump` (Tier 6) investigated but
  deliberately not built, pending a design decision.
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

## Current task: Phase 2 instruction-coverage plan, tiers 4–6 not yet started

Tiers 1–3 (`Abs`/`LIMIT`/`T_SUB`/`T_CONV`/`Calc`) closed and committed 2026-07-14 (see "Recently
closed" above). Working overnight, autonomously, per the project owner's own explicit request
("work through as much as you can without me... follow the plan") — continue in tier order:

- **Tier 4**: `Modbus_Master`/`Modbus_Comm_Load` (`FC ModbusComs`) — Instance-DB-backed like
  `TON`/`Call` (reuse that machinery: `ParseInstanceReference`/`AccessNode`). **Fully re-grounded
  2026-07-14 night** (every attribute checked directly via `grep`, not visual reading, per the
  `LIMIT` lesson above) — genuinely more infrastructure than Tiers 1–3 combined, which is why this
  was deliberately left for a fresh session rather than rushed at the tail of a long one:
  - `Modbus_Master` (`Version="6.0"`, no `DisabledENO`): `en` rail-fed; **`REQ` is fed by a full
    Contact chain's own `out`, not a plain IdentCon tag** — needs `TraceChain` (already generic
    over port name, same mechanism `en`/a Coil's own condition already use), not
    `ResolveTagOrLiteralOperand`. `MB_ADDR`/`MODE`/`DATA_ADDR`/`DATA_LEN`/`DATA_PTR` are ordinary
    plain-tag inputs. **`DONE`/`BUSY`/`ERROR`/`STATUS` are four separate output tags** — no
    existing production writes more than one destination; needs a new statement/sidecar shape,
    not a reuse of the single-`DestTag` pattern every instruction so far has used.
  - `Modbus_Comm_Load` (`Version="5.0"`, no `DisabledENO`): `en` rail-fed (fan-out shared with
    several Contacts); `REQ`/`PORT`/`BAUD`/`PARITY`/`RESP_TO`/`MB_DB` are ordinary plain-tag
    inputs; `DONE`/`ERROR`/`STATUS` are three output tags (same multi-output need as
    `Modbus_Master`). **`FLOW_CTRL`/`RTS_ON_DLY`/`RTS_OFF_DLY` are wired to `<OpenCon>`** —
    confirmed-real "deliberately left unconnected" ports, a genuinely new operand shape no
    existing instruction has needed (`OperandSidecar` only has `TagOperand`/`LiteralOperand`
    today — would need a third `OpenOperand` variant, plus a readable-IR-text sentinel, e.g. an
    `OPEN` keyword parallel to `EnSource`'s own `ENO` sentinel).
  - Net: needs three new pieces of general infrastructure (chain-fed non-`en` operand resolution
    — mechanically ready, `TraceChain` already supports it; multi-output-tag productions; an
    open/unconnected operand variant) before either instruction can be built, not just "port
    grounding" as tonight's plan first estimated. Worth tackling as its own dedicated session.
- **Tier 5** (`MOVE_BLK_VARIANT`): **closed 2026-07-14.** Built, unit-tested (8 tests), and now
  live-verified — imported/compiled clean (0 errors) in `SampleProject`, byte-identical round-trip.
- **Tier 6**: `FillBlockI` **closed 2026-07-14** — built, tested, and live-verified alongside
  `MOVE_BLK_VARIANT`. Along the way found the real `out` destination is array-indexed
  (`CommsProcessData.NodeFaultCount[3]`, not a plain scalar) — no converter code changed (the
  general array-index support already existed), only `FillBlockIFedByRail.xml` was corrected to
  match. 345 converter tests total, all green.
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

**Possible next work** (no explicit instruction yet — ask before starting):
- Build the `Normalizer` Part-identity fix above.
- Fix the `ExternalAccessible=False` Sanitizer gap above.
- Grow the committed reference-project corpus to actually exercise the ~15 instruction-level
  constructs (`Mul`/`Convert`/`Sub`/`Div`/comparisons beyond `Eq`/`Ge`/`CALL`/`SWAP`/`SCoil`/
  `RCoil`/`WAND`/`Not`/`TONR`/`TOF`) that currently have no permanent, committed, live-TIA-verified
  regression coverage — see `docs/audit/2026-07-14-code-quality-and-docs-audit.md` for the full
  finding.
- S2 (read and explain) — the next roadmap stage once S1's gate review is formally signed off.

**Sanitization maps built and kept** (`sanitization/`, gitignored): all 8 dependency FBs' own maps,
plus `PlantAutoControl.map.json` itself and per-instance maps for all 26 DB/tag-table roots. Reusable
directly for any follow-on work against the same real project.
