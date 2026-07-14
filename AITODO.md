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

- **Instruction-coverage sweep + Phase 2 tiers 1–3 (`Abs`/`LIMIT`/`T_SUB`/`T_CONV`/`Calc`),
  2026-07-14.** Grounded all 36 remaining `JOB9002` blocks (both PLC stations), found 11 real
  currently-unsupported instructions, ranked into a 6-tier plan. Tiers 1–3 (5 instructions) built,
  tested (40 new tests, 323 total), and live-verified via a synthetic composed FC imported/compiled
  clean in `SampleProject`. Two real bugs found only by live TIA verification (a misread `LIMIT`
  `DisabledENO` attribute; a hand-built fixture reusing one `Access` UId across two wires, which
  TIA's own export never does) — see `docs/notes/stage-gates.md` for the full story.
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
- **Tier 5**: re-ground `MOVE_BLK_VARIANT` — every sample seen so far (`MoveData`,
  `VSDDataSequence`) only had `en` wired; the real array/count port shape is still unknown. Find a
  fully-wired example before designing anything.
- **Tier 6**: `Jump` (+ implied `Label` — needs its own jump-target shape found), `FillBlockI`,
  `WAIT` — novel categories, no existing analog, each needs its own small grounding spike. Lowest
  priority.

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
