# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see Exit-criteria evidence below). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | **ACTIVE (walking skeleton core proven end-to-end, incl. re-export/`Normalizer` equivalence — the earlier "re-export blocked" state was resolved same-session via block-level compile, see detail below)** | — | ADR-0001/`ir/SPEC.md` decided; converter (C#, `src/converter/`), `openness-cli export`/`import`/`compile`/`compile --block`, and golden harness machinery (`tests/golden/`) built and live-verified for Contact/Coil, OR-merge (branches are recursive chains — multi-contact, nested, comparison-as-branch, all live-verified), negated contacts (multi-assignment, slice- and array-addressed), TON/TONR/TOF (both instance scopes), comparisons (Eq/Ge/Lt/Ne), MOVE, WAND (bitwise word AND), CALL (FB/FC block calls, incl. FC calls with no `<Instance>`), SCoil/RCoil (set/reset coils), network/block-level Title, MUL/CONVERT/ADD/SWAP (arithmetic, incl. ENO-chaining), FC/FB parameter-interface modeling (Input/Output/InOut/Constant), `Access Scope="LocalConstant"`, plus GlobalDB/InstanceDB `Static`-section round-trip including one-level structured members. **`FC PlantAutoControl` now converts as a whole block** (`to-ir → to-xml → to-ir` byte-identical) — the first real production block this session to fully round-trip end to end; **all 8 of its dependency FBs** (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`/`AirStar`/`MotorVSDSystem`/`TomraControlSystem`) now do too. The true TIA-cycle proof for `PlantAutoControl` specifically remains open: it depends on external tags/FBs no other TIA project has — see S1 items 16/17 below. Reference project has 7 committed corpus artifacts (5 FCs/DBs + `PerimeterSafetyAlarms` + `TimerSample`/`DB_Timers`). All PC-side suites green: 244 converter, 68 openness-cli, 11 golden-harness tests. See Exit-criteria evidence. |
| S2 — Read and explain | not started | — | |
| S3 — Comment generation | not started | — | |
| S4 — Convention review | not started | — | Blocker cleared early: 06-lad-conventions.md is populated |
| S5 — Data extraction | not started | — | Can run parallel with S3/S4 once S1 done |
| S6 — Generation | not started | — | |
| S7 — Modify existing | not started | — | |
| S8 — Pattern maturation | not started | — | |
| S9 — Sim verification | not started | — | Blocked on R-07 (PLCSIM vs S7-1200 G2) |

## Exit-criteria evidence

Record acceptance-test results here at each gate (10 accurate explanations, 10/10 compiling generations, etc. — see `docs/08-testing-strategy.md`).

### S0
- [x] One command lists all reference-project blocks, F-blocks flagged and never opened — result: `openness-cli list "JOB9003 - K150"` (2026-07-10) attached to the running Portal session, reused the already-open project, and listed 7 blocks (1 OB, 3 DB, 3 FC) across nested groups ("Map IO", "Alarms") with correct type/number/language/path. **Caveat:** this project contains no F-blocks, so live output has nothing flagged — the flagging behavior itself is proven by reflection (all 7 `F_`-prefixed `ProgrammingLanguage` values, `docs/notes/openness-api-surface-v20.md`) and by unit tests (`SafetyClassifierTests`), not by a live F-block. Still open: run against a project that actually contains one, whenever one is available.
- [x] First-connect approval dialog documented in `docs/notes/` — result: no dialog appeared on this run (attach to an already-running, already-approved Portal instance). Documented in `docs/notes/openness-quirks.md`; the exact dialog text/screenshot is still uncaptured and stays open pending a fresh-approval-state test.
- [x] Cold-open (project not already loaded in Portal) — result: `openness-cli list "<path>\JOB9002 - Tom White Waste_V20.ap20"` (2026-07-10), project not previously open, exercised `Projects.Open(FileInfo)` live for the first time (previously reflection-verified only). Succeeded, exit code 0: listed ~150 blocks across two linked PLC stations (`station_1/JOB9001_PLC`, `station_2/JOB9002_PLC`) and every block group (Motors, Map IO, Control, Coms, Alarms, HMI, Simulation). One SCL block present (`LSNTP_Server`); no F-blocks in this project either — safety-flagging still only reflection/unit-test-verified, not live.
- [x] A-01 verified (LAD export/import for S7-1200 G2 incl. comments) — result: verified 2026-07-10 against `JOB9002 - Tom White Waste` (scratch copy, `FC PlantAutoControl`). Export → SimaticML with network titles/comments intact → re-import via `ImportOptions.Override` succeeded cleanly (1 block returned). Full detail: `docs/09-risk-register.md` A-01.
- [x] A-02 verified (programmatic compile gives usable diagnostics) — result: verified 2026-07-10, same spike, immediately after the A-01 re-import. `ICompilable.Compile()` (obtained from the PLC's `DeviceItem`) returned `State=Success, Errors=0, Warnings=0` with structured per-message diagnostics (state/description/path). Deliberately-broken-input case not yet tried. Full detail: `docs/09-risk-register.md` A-02.

**All four S0 exit-criteria items are now evidenced.** Status below reflects that; converting to "done" and starting S1 is a deliberate gate-review step per `docs/03-development-plan.md`, not automatic — held open pending that review.

### S1 (walking skeleton — Contact/Coil-only slice, overall plan items 4–6)

Real findings from the live proof against `JOB9002` blocks (2026-07-10), each a correctness gap
found and fixed, not guessed at — full detail in `src/converter/README.md`:

- **Multiple independent rungs per network are real** — a 16-independent-rung alarm-bit network
  (`PerimeterSafetyAlarms`). `IrNetwork`/`NetworkSidecar` redesigned around a list of coil assignments
  rather than assuming one coil per network.
- **The rail wire is commonly shared across many chains** (one wire, many endpoints) rather than
  one wire per chain — tracked separately (`CoilAssignmentSidecar.RailWireUId`) so it isn't
  mistaken for fan-out that would break series-chain reducibility.
- **Empty networks are real** (`<NetworkSource />`, no content — a trailing placeholder network
  in the same block) — represented as zero coil assignments, not a hard error.
- **Slice access (bit-within-word) addressing is real** — `06-lad-conventions.md` C-501's
  documented alarm convention, confirmed as `SliceAccessModifier="x15"` in the source XML.
  Initially silently collapsed 16 distinct alarm bits into one tag name in the IR (a real bug,
  caught by inspecting `to-ir` output before proceeding, not by a test that happened to check
  for it) — fixed, now preserved as `.%X15` in the IR tag path.
- The root block element needs its own `ID` attribute (`<SW.Blocks.FC ID="0">`, distinct from
  its `CompileUnit` children's IDs), every `MultilingualText`/`MultilingualTextItem` (comments)
  needs its own unique `ID`, and the block's `AttributeList` needs a `<Namespace />` element
  even when empty — all three found one `Import()` attempt at a time (each error was specific
  enough to fix immediately: "missing ID for the 'SW.Blocks.FC' element", then same for
  `MultilingualTextItem`, then "missing 'Namespace' identifier attribute").
- `PlcBlock.Export()` refuses to export an "inconsistent" block (needs recompile in TIA) with a
  clear error.

#### Result, 2026-07-10 — did the walking skeleton pass its own test? Partially.

Layer 1's actual assertion (`docs/08-testing-strategy.md`) is
`SimaticML → IR → SimaticML' → import → compile → re-export → SimaticML''`, with
`SimaticML'' ≡ SimaticML` (via `Normalizer`) as the thing that proves losslessness. That's what
"pass" means here — everything before it is necessary but not sufficient.

**Ran clean — 5 of 6 stages, against a real block (`FC PerimeterSafetyAlarms`, `JOB9002 - Tom White Waste`,
station_2):**
1. `export` — succeeded.
2. `converter to-ir` — succeeded, correct IR (multi-assignment, slice-addressed).
3. `converter to-xml` — succeeded.
4. `import` — succeeded, block returned with no errors.
5. `compile` — `State=Success, Errors=0, Warnings=0`, on two independent runs.

**Did not run — stage 6, the re-export, and therefore the `Normalizer` equivalence check that
was the actual point.** The second `export` call (needed to produce `SimaticML''` to compare
against the original) was refused: `"Inconsistent blocks and PLC data types (UDT) cannot be
exported."` No pass/fail exists for the one assertion that proves losslessness — the test
didn't complete, not "completed and failed."

**Why, and why it's not this session's bug:** reproduced the identical refusal on `ControlMain`,
a block untouched by any work this session, and it matches an identical error hit hours earlier
(before any import activity at all) exporting `PlantAutoControl` in the same station. So the
`station_2` PLC software's inconsistency predates this session's import, isn't caused by it, and
a clean project-wide compile (checked twice) doesn't clear it either.

**Precisely characterized, 2026-07-10, by the new `openness-cli sanity-check <project>`**
(built specifically to answer this — reads every block's `IsConsistent` flag directly, no export
attempt needed, plus compiles every PLC device found): 180 blocks total, **15 inconsistent**,
all under `station_2/JOB9002_PLC`, clustered in `Map IO/Simulation` (simulation-mode blocks) and
`Control`/`Alarms` (`ControlMain`, `PlantAutoControl`, `AlarmsMain`, `PerimeterSafetyAlarms`) — while **both**
device compiles report `Success, Errors=0, Warnings=0` at the same time. Confirms conclusively
this is a real TIA/Openness block-consistency state issue in the scratch project, not a timing
artifact or something a second compile fixes. Full detail and the clustering hypothesis:
`docs/notes/openness-quirks.md`. Outside what `openness-cli`/the converter can fix from here —
needs the project owner to check directly in TIA Portal.

**Bottom line:** the converter/CLI produce output TIA's `Import()` accepts and that compiles
cleanly — real evidence the design works. The specific claim "this round-trips losslessly"
stays unproven pending either the project's consistency state getting resolved, or a re-run
against a block/project that isn't affected by it.

#### Cold-open re-test + second full attempt, 2026-07-10

Project owner saved and closed the scratch project and TIA Portal entirely, then asked for a
fresh re-run. `sanity-check` against a fresh Portal launch reproduced byte-identical results (180
blocks, same 15 inconsistent, both device compiles `Success`) — rules out session-state/caching
as the explanation. Full detail: `docs/notes/openness-quirks.md`.

Chose a different, previously-unaffected block (`NodeStatusAlarms`, `station_2/JOB9002_PLC/Alarms`) to
attempt a clean full round-trip. Hit a new, real converter bug on `converter to-xml`: the same
tag path can be referenced by two distinct `<Access>` XML elements (different UIds) in one
network, which crashed `FlgNetBuilder`'s `TagPath`-keyed rebuild lookup. Fixed by threading the
exact source Access UId positionally through `CoilAssignmentSidecar` instead of re-deriving it by
tag path at rebuild time — no XML was hand-patched (hard rule 7); full detail:
`src/converter/README.md`. All 17 converter tests still pass.

With the fix, `export → to-ir → to-xml → import → compile` ran clean end-to-end on `NodeStatusAlarms`
(import returned the block with no errors; compile reported `Success, Errors=0, Warnings=0`).
Stage 6 (re-export for the `Normalizer` equivalence check) still didn't run: `sanity-check`
immediately after showed `NodeStatusAlarms` newly flagged inconsistent (16 total now, the original 15
plus this one), and a second compile didn't clear it.

**Root cause found, same session:** project owner opened the newly-inconsistent `NodeStatusAlarms` in
the TIA UI to inspect it directly and spotted the actual defect — `"CommsProcessData".Node_Error` is
a `BOOL` array, and three separate array elements (`[1]`, `[2]`, `[3]`) feed three separate alarm
bits (`ModbusAlarm.%X0/1/2`). The converter's `AccessNode` had no concept of array-subscript
addressing, so all three `Node_Error[n]` references collapsed to the identical, ambiguous
`CommsProcessData.Node_Error` in the IR — this is also what caused the earlier `FlgNetBuilder`
crash (three genuinely different Access elements sharing one indistinguishable tag path). Ground
truth for the fix was pulled from an untouched sibling block (`station_1/JOB9001_PLC/Alrams/
NodeStatusAlarms`, never imported into) rather than guessed: `<Component Name="Node_Error"
AccessModifier="Array"><Access Scope="LiteralConstant"><Constant><ConstantType>DInt</ConstantType>
<ConstantValue>n</ConstantValue></Constant></Access></Component>`. Fixed by adding
`AccessNode.ArrayIndex`, parsed/written explicitly, IR notation `Node_Error[n]`. Verified against
that same untouched block: `to-ir` now shows 15 distinct `Node_Error[0..14]` tag paths instead of
one collapsed path, and `to-ir → to-xml` regenerates the exact `AccessModifier="Array"` + nested
`Constant` structure with the correct per-element index. Full detail: `src/converter/README.md`.

**Follow-up test, same session — the array-index fix was real, but it wasn't the reason blocks go
inconsistent.** `station_2/NodeStatusAlarms`'s copy in the scratch project was already contaminated
by the pre-fix import (and the pristine original had been deleted in scratchpad cleanup), so
re-testing it wouldn't prove anything. Instead ran the full chain against the untouched
`station_1/JOB9001_PLC/Alrams/NodeStatusAlarms`: `export → to-ir → to-xml` (regenerated XML inspected
directly and confirmed byte-structurally correct — same `AccessModifier="Array"` shape, correct
per-element index, as the original) → `import` (succeeded) → `compile` (`Success, Errors=0,
Warnings=0`). `sanity-check` immediately after still showed `NodeStatusAlarms` newly inconsistent
(plus its caller `AlarmMain`, same clustering pattern as station_2) — on content now verified
correct, not just "compiled so probably fine." A second compile didn't clear it either; `export`
kept refusing.

**Conclusion:** `IsConsistent = false` after import is not explained by content defects — it
reproduces on genuinely byte-correct, round-tripped content just as it did on the earlier
array-index-lossy content. It looks like an inherent side effect of `Import()` in this
project/TIA version (importing a block flags it and its caller(s) inconsistent; device-level
`Compile()` never clears it), separate from converter correctness. Full detail:
`docs/notes/openness-quirks.md`. Whether the original 15 pre-existing inconsistent blocks were
introduced the same way (an earlier round of Import() before this session) is now a plausible
explanation, still unconfirmed — needs the project owner to check in the TIA UI.

**Resolved, same session: block-level compile clears it; device-level `compile` doesn't.** Project
owner opened `station_1/NodeStatusAlarms` in the TIA UI, ran block-level "Compile (only changes)", then
compiled the whole PLC. `sanity-check` immediately after: `NodeStatusAlarms` and `AlarmMain`
(station_1) both cleared — back to the original 16 (station_2's pre-existing set, plus the still-
contaminated `station_2/NodeStatusAlarms`). Root cause: `ICompilable.Compile()` on a `DeviceItem` —
the only compile granularity Openness's object model exposes, and what `openness-cli
compile`/`sanity-check` use — is **not equivalent** to TIA's own block-level compile for clearing
`IsConsistent` after `Import()`. A real Openness/TIA behavior, not a converter or `openness-cli`
bug. Full detail: `docs/notes/openness-quirks.md`.

**Practical implication (hard rule 4):** a device-level `compile` after `import` isn't sufficient
proof a generated/modified block is clean — the compile gate needs a block-level compile too,
which today only exists via the TIA UI. Worth adding block-level compile to `openness-cli` if this
keeps mattering past S1.

**Stage 6 reached, same session, immediately after the block-level compile.** With
`station_1/NodeStatusAlarms` cleared, `export` was retried — succeeded (previously refused every
time). Inspected the re-exported XML directly: 15/15 `AccessModifier="Array"` array-index
Components and 15/15 `SliceAccessModifier` slice-access Components present, matching the original
exactly. This is the walking skeleton's actual target assertion
(`SimaticML → IR → SimaticML' → import → compile → re-export → SimaticML''`, `SimaticML'' ≡
SimaticML`), reached end-to-end for the first time this project, on real production LAD data
(multi-assignment, array-indexed, slice-addressed). A formal `Normalizer`-based automated diff
(vs. this manual field-count check) is still open — `tests/golden/`'s `Normalizer` exists but
hasn't been run against this pair.

**Gap closed, same session: `openness-cli compile --block <name>` added, no TIA UI step needed.**
Reflecting on the installed V20 DLL showed `PlcBlock` implements `IEngineeringServiceProvider`
just like `DeviceItem`/`PlcSoftware` — untested territory, so verified live with a throwaway spike
before building anything: `plcBlock.GetService<ICompilable>()` returns non-null, and calling
`.Compile()` on `station_2/NodeStatusAlarms` (still inconsistent) flipped `IsConsistent` to `true`
immediately, no UI interaction. Built `IOpennessGateway.CompileBlock` on top of this and wired it
to a new `--block` flag on `compile`; same safety refusal and `--device` disambiguation as
`export`. All 68 openness-cli tests pass.

Live-verified against the full remaining inconsistent list: `openness-cli compile --block
PerimeterSafetyAlarms` cleared it — one of the *original* 15 pre-existing blocks, predating this session
entirely, proving the original mystery and this session's self-inflicted cases share the exact
same mechanism. Ran `--block` compile across every remaining inconsistent block (the
`Simulation`/`DOLSim`/`VSDSim` cluster, `ControlMain`, `PlantAutoControl`, `AlarmsMain`) — one real
finding along the way: `ControlMain` failed with a genuine `State=Error` the first time (it calls
`PlantAutoControl`, compiled after it in this run), then succeeded cleanly once retried after
`PlantAutoControl` was clean — compile order matters for callers. Final `sanity-check`:
**`OVERALL: HEALTHY`, 0 of 180 blocks inconsistent.** Full detail: `docs/notes/openness-quirks.md`,
`src/openness-cli/README.md`.

**Updated bottom line:** the converter/CLI's output is accepted by `Import()`, is byte-structurally
correct against real array-indexed data, compiles clean at both device and block level, and
round-trips through a real `export` with the array-index and slice-access structures intact. S1's
core technical claim — lossless SimaticML↔IR round-trip for this converter slice — now has live,
end-to-end evidence. The compile-gate gap (hard rule 4) that required a human TIA UI step is
closed: `openness-cli compile --block <name>` does it programmatically, and using it end to end
brought the entire scratch project (both stations, 180 blocks) to fully healthy for the first time
this session.

### S1 item 1 (reference project) — Phase 1 complete, 2026-07-10

Seeded the purpose-built Green reference project (`ir/reference/`, `simatic-ml/reference/`) —
S1's stated exit criteria runs against "the reference project," which didn't exist before this;
everything proven above ran against `JOB9002`, explicitly excluded from becoming the committed
corpus. One block (`NodeStatusAlarms`, structurally derived and sanitized from a real one —
`docs/13-data-boundary.md` has the recorded, non-identifying basis; `tests/golden/README.md`
has the technical detail) is now committed, and the full live round-trip
(`export → to-ir → to-xml → import → compile → re-export`) ran against a real, separate TIA
project, ending with `Normalizer.AreSemanticallyEquivalent` — the actual Layer 1 assertion 2
(`08-testing-strategy.md`), not the manual field-count check used earlier this session —
returning **true** for the first time.

Getting there surfaced three more real, previously-unknown gaps, each found by the process
working exactly as designed ("new constructs found in real projects... failing, then fixed"):

1. **A block referencing another block's data needs that block compiled first, in the target
   project too** — the imported FC referenced two DBs that didn't exist in the brand-new
   reference project at all (only in the source project). Not a bug; expected given hard rule 3.
   Required two small placeholder DBs (matching real declared types — `Array[0..14] of Bool`,
   `Word` — confirmed with the project owner rather than guessed) plus compiling them before the
   FC that depends on them, same dependency-order lesson as `ControlMain`/`PlantAutoControl` above.
2. **`openness-cli compile`'s diagnostic messages were silently incomplete.** `CompilerResultMessage`
   has a nested `Messages` tree (confirmed by reflecting on the DLL) that `OpennessGateway` only
   ever read one level of — every past compile failure this session showed a bare error count with
   an empty description. Fixed by recursing; immediately paid for itself by revealing the real
   cause of item 1 above ("Block ... that is accessed has not been compiled") instead of an opaque
   `errors: 30`.
3. **`Wire` UId is not preserved through a real TIA import/compile cycle** — contrary to the
   original sidecar design assumption. TIA reassigns every wire's own UId on its own (the shared
   rail wire moved from 126 to a fresh 81, everything else shifted), even though the *set* of
   connections was byte-identical. `Normalizer.cs` now treats wire identity as its endpoint set,
   not its UId, and wire order as non-significant — both confirmed real, not guessed. Two smaller,
   related gaps fixed alongside it: `BlockSourceParser` was silently dropping a `Title` field
   (distinct from `Comment`) and would've silently dropped real FC/FB parameters (`Interface`) —
   both now hard-error if ever non-empty/non-boilerplate, rather than vanish quietly.

Full detail on all of these: `tests/golden/README.md`, `docs/notes/openness-quirks.md`.

**Not yet done** (deliberately out of scope for Phase 1, per the approved plan): only one block;
no DB/UDT round-trip (the two placeholder DBs are hand-created scaffolding, not converter output);
no TON/MOVE/comparisons/calls/branches; no automated CI wiring (the real round-trip needs a live
Portal session, same as `RoundTripRunner.RunFull` always required). Growing the corpus is
follow-on work using the now-proven pattern.

### S1 item 1 (reference project) — Phase 2: real DB round-trip, 2026-07-10

Closed the biggest Phase 1 gap: DB/UDT round-trip was placeholder scaffolding, not converter
output. Built `DbSourceParser`/`DbSourceWriter`/`DbIrSerializer`/`DbIrParser` for
`SW.Blocks.GlobalDB` (`Static` section, scalar and `Array[m..n] of <scalar>` members —
`src/converter/README.md` has the full scope), grounded against three real DBs
(`CommsProcessData`/11 members, `Alarms`/4, `Input`/47 — all fully brought in, not just the
members the paired FC references, per your call). `ir/SPEC.md`'s DB sketch had two things wrong,
corrected: DB kind is the source's root element name, not a field; retention is per-member, not
per-DB. `converter sanitize`/`to-ir`/`to-xml` now auto-detect DB vs code-block content.

Three real bugs found and fixed, same discipline as Phase 1 — caught by testing against real
data, not invented:

1. **`Member`/`AttributeList`/`StartValue` inherit the `Interface` XML namespace** from the
   ancestor `<Sections xmlns="...">` rather than redeclaring it — looking them up as unnamespaced
   elements silently returned null for every one on both parse and write, making every
   `BooleanAttribute` read back as "absent." Caught immediately (first real DB tried).
2. **A structural-section scan used `.Descendants()` instead of direct children**, so a
   structured member's own nested `<Sections>` (inside its own `<Member>`, e.g. a timer instance)
   was wrongly picked up as a top-level DB section, producing a less specific hard-error than the
   dedicated structured-member check was designed to give. Caught by this converter's own test
   suite, not live data.
3. **A blanket `"Interface"` strip in `Normalizer` — correct for code blocks (confirmed
   boilerplate by a parser-level guard), wrong for DBs.** Applied unconditionally, it would have
   made every DB round-trip trivially pass without ever comparing member content — the exact
   silent-false-positive the golden harness exists to prevent. Fixed with a structural test (a
   DB's `Interface` always has a `Static` Section, a code block's never does), not a blanket name
   match. Two new permanent `NormalizerTests` guard this distinction specifically.

Explicitly deferred as one unit (an instance DB example (`ConveyorMotor1`) needs both together, so a
converter that opens an instance DB and immediately hard-errors on its first member isn't worth
half-building): `SW.Blocks.InstanceDB`, and any member with nested `<Sections>` (UDT-typed or
system-function-block-instance-typed, e.g. a `TON_TIME` timer).

**Second FC block: searched, not found this pass.** Tried `PerimeterSafetyAlarms`/`GeneralAlarms`
(OR-merge instructions) and `StatusAlarms` (a `LiteralConstant` access, likely a comparison) —
all in the `Alarms` group station_1 also has, all outside current LAD scope. `ShutdownControl`
itself converts cleanly but every single dependency is a structured member on an Instance DB
(`<Instance>.IO.ShutdownComplete`) — the exact deferred case above. Checked 12 candidates total
without finding a clean fit; most of this project's Control-group logic depends on Instance DBs,
most of its Alarms-group logic uses OR-merges. A real, useful finding about this codebase's
converter-scope gaps, not a dead end — the next FC block waits on either OR-merge support or
instance-DB support, whichever comes first.

Live pipeline: sanitized all three DBs (dependency-order compile, DBs before `NodeStatusAlarms`,
which was re-verified still compiles against the real — not placeholder — DBs), full round-trip
against the real reference project, `Normalizer.AreSemanticallyEquivalent` **true** for all
three. Committed: `ir/reference/{CommsProcessData,AlarmWords,EquipmentStatus}.ir` +
matching `simatic-ml/reference/*.xml`. All 114 tests pass across the three suites (35 converter,
68 openness-cli, 11 golden harness).

### S1 item 7 Phase A (OR-merge + negated contacts), 2026-07-11

Closed the OR-merge half of the Phase 2 gap (`PerimeterSafetyAlarms`/`GeneralAlarms` above). Grounded
against two fresh real exports before writing any code: `PerimeterSafetyAlarms` (3-way OR of negated
contacts) and `GeneralAlarms` (33-way OR of plain contacts) — both confirmed the same shape,
`Part Name="O"` with a `TemplateValue Name="Card" Type="Cardinality">N</TemplateValue>` child,
each of its `N` `inK` ports fed by exactly one Contact (optionally negated via a
`<Negated Name="operand" />` child), every branch fed directly by the shared rail wire — no
branch confirmed real is itself a multi-contact chain.

Design: added `Expr.Not` to the IR (`NOT <tag>` notation, same style as `AND`/`OR`).
`GraphReducer`'s backward trace now recognizes `Part Name="O"` as a valid rail-facing node (always
terminates the trace — an OR-merge's branches resolve straight to Powerrail in every case seen, so
nothing has been observed *behind* one) and resolves each branch independently, hard-erroring on
a multi-contact branch or a nested OR-merge (both real-but-unconfirmed shapes, refused rather than
guessed at). The sidecar's flat `ContactUIds`/`WireUIds` lists couldn't represent a chain
position that fans out, so `CoilAssignmentSidecar` was rebuilt around a recursive
`ChainStepSidecar` (`ContactStep` | `OrStep`, the latter holding a list of `ContactStep`
branches) — each step now owns its own outgoing-wire UId directly rather than the old
interleaved flat-list bookkeeping.

Live proof: `PerimeterSafetyAlarms` — fully in scope once OR-merge + negation landed (8 independent rungs
in one network: the 3-way negated OR, four more individually-negated single-contact rungs, three
plain single-contact rungs, all bit-sliced into one alarm word; a second, empty network). Every
tag it references was already in `sanitization/reference-project.map.json` from the Phase 2
search. `export → sanitize → to-ir → to-xml → import → block-compile → re-export →
Normalizer.AreSemanticallyEquivalent` — **true**, first real confirmation that OR-merge and
negated-contact SimaticML output actually imports and compiles in TIA, not just self-consistent
in the converter's own model. Committed as `PerimeterSafetyAlarms` (`ir/reference/`,
`simatic-ml/reference/`) — the fourth reference-project FC block (the `NodeStatusAlarms` name
carries no OR-merge/negation content, so this is genuinely new coverage, not a duplicate).

All 45 converter tests (10 new: OR-merge reduce/round-trip/serialize, negated-contact
reduce/round-trip/serialize, IR self-stability with both, two hard-error fixtures for the
explicitly-deferred shapes) and 11 golden-harness tests pass.

### S1 item 7 Phase B (Instance DB + structured members), 2026-07-11

Closed the deferred half of the Phase 2/Phase A gap (Instance DBs and one-level structured
members, `ConveyorMotor1`/`FB MotorDOL`). `SW.Blocks.InstanceDB` carries `InstanceOfName`/
`InstanceOfType` — `InstanceOfType` isn't an IR field since every real instance DB seen has
`Type="FB"` (writer regenerates the constant; parser hard-errors if a source ever disagrees).
Structured members (UDT-typed, e.g. `"TypeDOL"`; or system-function-block instance-typed, e.g.
`TON_TIME` with a `Version` attribute) are **inlined** in the IR one level deep, rather than
referencing a separately-defined `UDT <Name>` by name — project owner's call, 2026-07-11,
recorded in `ir/SPEC.md`'s "Structured members" section: reference-by-name is the better
long-term shape but isn't safely buildable yet (`openness-cli` has no `PlcType`/UDT export
capability, so a "canonical" UDT shape would have to be guessed at rather than grounded); inline
needs nothing new since the source DB XML already contains the full nested shape at the
declaration site. Grounded against a real `ConveyorMotor1` instance DB (an instance of `FB MotorDOL`:
a `"TypeDOL"`-typed member with 29 scalar sub-members, plus `TON_TIME`/etc. timer members with
`PT`/`ET`/`IN`/`Q`). Doubly-nested structured members, and any nested `Section` other than
`"None"`, remain hard errors (real-but-unconfirmed shapes, refused rather than guessed).

New source files `DbMemberLineFormat.cs`/`DbInterfaceMembers.cs`; new fixtures covering
Instance DBs with structured members, UDT-typed and doubly-nested members, non-`None` nested
sections, and non-empty FB `Input`/`Static`+`Temp` interfaces; a new `BlockInterfaceTests.cs` and
263 new lines in `DbConverterTests.cs`. Full detail: `ir/SPEC.md` ("Structured members"),
`src/converter/README.md` ("DB support").

**Verified 2026-07-11 (this audit pass, run to resolve a doc inconsistency — see
`CHANGELOG.md`):** all three PC-side suites green — 68 converter tests (up from 45; the +23 is
this phase's DB/interface/fixture coverage), 68 openness-cli tests (unaffected by this phase,
confirmed still green), 11 golden-harness tests. Phase B is functionally complete, not merely
documented as such.

Still deferred: reference-by-name for structured members (needs real UDT/`PlcType` export,
`ir/SPEC.md`'s explicit "not now" — revisit only as its own deliberate phase); MOVE/comparisons
landed separately (items 9/10 below), block calls as first-class LAD instructions still not built.
(A multi-contact OR-merge branch and a nested OR-merge were deferred at this point — both closed
out later, S1 item 11, 2026-07-11/12.)

### S1 item 8 (TON support, both instance scopes), 2026-07-11

Grounded against two real exports, at the user's specific request to confirm both a
`GlobalVariable` and a `LocalVariable` TON instance before building anything (the site convention
C-407 distinction between multi-instance timers-in-an-equipment-FB and standalone timers-in-
`DB_Timers`): `FB MotorDOL` (`Instance Scope="LocalVariable"`, its own `GeneralDelayTimer1` — a
`TON_TIME`-typed structured member, the same shape Phase B already parses/writes on the DB side)
and `FC ControlDelays` (`Instance Scope="GlobalVariable"`, a single `<Component>` naming its own
dedicated instance DB directly, `GeneralEnableDelay` — not a two-component `DB_Timers.Member`
path, resolving that open question). Both scopes are modeled by reusing `AccessNode` rather than
inventing a new type, since the `<Instance>` element carries exactly the same scope+path shape as
an ordinary `<Access>` — deliberately, so a future FC/FB call's instance argument can reuse it too.

IR design decision, made with the user before writing code (a deviation from `ir/SPEC.md`'s
original `timer := TON(...)` sketch): **no bound IR-level name.** TIA has no "timer name" concept
beyond the instance reference itself, so inventing one would be a synthetic identifier this
project avoids everywhere else. A TON statement is `TON(<instance path>, IN := <expr>,
PT := <expr>)`; later references to its output reuse the instance's own dotted path as a plain
tag reference (e.g. `GeneralEnableDelay.Q`) — not a new IR construct, since the only grounded way
to read a TON's output back is via an *ordinary* Access elsewhere in the source (confirmed real,
`FC ControlDelays`), which looks exactly like this in the source XML too.

Real wrinkle found during grounding, changed a design assumption: `FC ControlDelays`' TON has
**no wire at all** on its own `Q` port — not even `OpenCon`, simply absent from `<Wires>`.
`GraphReducer`'s `ResolveOptionalOutputPort` treats an entirely absent port the same as an
`OpenCon`-wired one (both valid); only a `Q`/`ET` wired to a real consumer is refused. That
refusal is deliberate, not a gap: the only real example of direct `Q`-to-consumer wiring
(`FB MotorDOL`) feeds an `RCoil`, itself out of scope, so it can't be proven end to end regardless
— `ChainStepSidecar`'s doc comment in `Model.cs` records this as a considered, not missed, cut.

Also new: `Access Scope="TypedConstant"` for a TON's `PT` fed by a literal (`T#100MS`, `FB
MotorDOL`) — distinct from the array-index `LiteralConstant` scope (no `ConstantType` child).
`PT` fed by an ordinary `LocalVariable` tag is also confirmed real (`FC ControlDelays`'
`GeneralDelayMS`, an FC-local parameter).

**Real, pre-existing bug found and fixed along the way, unrelated to TON specifically but exposed
by grounding PT-as-tag:** `FlgNetBuilder` rebuilt every ordinary tag Access as hardcoded
`Scope="GlobalVariable"`, regardless of the real source scope — harmless until now (every plain
tag seen in every prior phase happened to be GlobalVariable) but exactly the kind of silent scope
drift (R-05) this project's discipline exists to prevent. `SidecarAccessEntry` now carries the
real scope per entry.

12 new converter tests (fixtures built directly from both real exports — `WithTon.xml`,
`WithTonLocalInstance.xml`, `WithTonAndQReadBack.xml` — plus two hard-error fixtures for the
unsupported-instance-scope and direct-Q-wiring cases); one obsolete test removed (a prior
"TON is unsupported" hard-error test, now testing behavior that's deliberately changed). All
80 converter tests, 68 openness-cli tests, 11 golden-harness tests pass.

**Not yet live-round-trip-proven at the block level** (at the time the above was written). Both
grounding blocks (`MotorDOL`, `ControlDelays`) also use `Eq`/`Ge`/`Mul`/`Convert`
(comparisons/Move) elsewhere in the same block — a separate, unbuilt converter capability — and
`to-ir` requires every network in a block to be in scope (no partial-block conversion), so
neither converts as a whole. Closed the same day — see below.

### S1 item 8, closed out: `FC TimerSample`, live round trip + a second Normalizer finding, 2026-07-11

Project owner built `FC TimerSample` and `DB_Timers` directly in the reference project
(`SampleProject`) specifically to close out what the unit tests above couldn't reach — a
TON-only block, free of comparisons/Move/RCoil. Grounding it surfaced two more real gaps, closed
the same session:

1. **`Instance Scope="GlobalVariable"` with a *two*-component path**
   (`DB_Timers.SampleTimerN` — a named member inside a shared standalone-timer DB), not just the
   single-component case grounded earlier (`FC ControlDelays`, `GeneralEnableDelay`). Needed no
   code change — `AccessNode`'s component-path parsing was never length-restricted.
2. **`Q` wired *directly* into a plain `Coil`** (no ordinary Access in between) — the one shape
   deliberately left unmodeled after `FB MotorDOL`'s only example turned out to feed an
   out-of-scope `RCoil`. Built `ChainStepSidecar.TimerOutputStep`: `GraphReducer`'s shared
   `TraceChain` now recognizes an upstream `TON` via its `Q` port as a valid, chain-terminal leaf
   (parallel to how an OR-merge terminates a chain) — except it never touches Powerrail, so
   `CoilAssignmentSidecar`/`TimerBindingSidecar`'s `RailWireUId` became nullable (`none` in the
   IR sidecar text when a chain terminates this way). `FlgNetBuilder` skips rail-wiring entirely
   for a null `RailWireUId`. The IR text itself needed no new grammar — `GeneralDelayTimer1.Q` reads as
   an ordinary tag reference whether the source wired it directly or via a separate Access.

`TimerSample` itself needed simplifying first: its original Input/Output/Return parameters hit
`BlockSourceParser`'s existing (unrelated) hard error for real block parameters — full FC/FB
interface modeling is a separate, deferred piece tied to general block-call support. Project
owner replaced them with direct references to two new tag DBs (`TempControlBools`,
`TempControlDInt`) in TIA, keeping the session scoped to TON specifically.

**Live round trip:** `export → to-ir → to-xml → import → compile --block → re-export →
Normalizer.AreSemanticallyEquivalent` — **true**, first full live proof for TON (3 networks; the
third's `IN` reads back *two* other TONs' `Q` outputs via ordinary Access — chained timers,
exercised for free, not deliberately sought). Surfaced and fixed a real, second Normalizer gap:
TIA reassigns `Access` element UIds on its own Import()/Compile()/Export() cycle too (previously
only Wire's own UId was known to be volatile) — `tests/golden/README.md` has the full story,
including a real bug caught in the fix itself (a document-wide UId→content map collided entries
across networks, since UId numbering restarts per network — caught by this exact 3-network test,
a single-network fixture would never have exposed it).

12 new converter tests became 4 more after this (reduce/round-trip/serialize for the direct-`Q`
case, plus repurposing an obsolete hard-error test into a positive one) — 83 converter tests, 68
openness-cli tests, 11 golden-harness tests (12 during the live investigation, back to 11 once
the throwaway verification test was removed) all pass.

**Committed to the reference corpus, same session:** `TimerSample`/`DB_Timers` — the reference
project's 6th/7th artifacts (`ir/reference/{TimerSample,DB_Timers}.ir`,
`simatic-ml/reference/{TimerSample,DB_Timers}.xml`). Re-verified end to end against the exact
committed files (fresh export → to-ir → to-xml → import → compile → re-export →
`Normalizer.AreSemanticallyEquivalent`) — **true** for both — before committing, not assumed from
the earlier scratchpad run.

### S1 item 9 (comparisons, Eq/Ge), 2026-07-11

Next converter capability after TON, per the user's choice among four candidates (comparisons,
MOVE, RCoil/SCoil, block calls) — picked as the lowest-design-risk option since the IR syntax was
already sketched in this doc's readable-form table. Grounded against a real export before
building anything, `FC ControlDelays`: only `Eq`/`Ge` directly observed (`Ne`/`Le`/`Gt`/`Lt`
unconfirmed, same status as AND-merge).

Real finding that changed the original framing: a comparison isn't "just another leaf kind like
TON" — it behaves like a **Contact**, a pass-through chain position, not a terminal. Its own
rail-facing/continuation port is `pre` (not `in`, genuinely different). This required generalizing
`GraphReducer.TraceChain`'s dispatch on **both** sides (the upstream "out"-equivalent port, already
varied per part kind since TON, and — new — the continuation "in"-equivalent port, previously
hardcoded). A second real finding: `Access Scope="LiteralConstant"` used at the *top level*
(a comparison's literal operand, e.g. `1`) — always carries a `<ConstantType>`, the exact
opposite of TON's `TypedConstant` (never has one). `ConstantAccessNode`/`SidecarConstantEntry`
gained a nullable `ConstantType` field; `Expr.TimeLiteral` generalized to `Expr.Literal` (covers
both, no reader-relevant difference in rendered text).

**Scope decision, made during implementation, not pre-approved in the plan:** `ControlDelays`'
own real network combines two comparisons (`Ge`/`Eq`) via a *second* OR-merge (`O(41)`), each
comparison itself fed by a *first* OR-merge (`O(38)`) rather than Powerrail directly — a
comparison-composes-with-OR-merge shape. Tracing this by hand (not guessing) showed the
**existing, unmodified** OR-merge branch check (`branchPart.Name != "Contact"`) and wire fan-out
check already safely refuse it — so rather than build a larger OR-merge-branches-as-recursive-
chains generalization (real complexity, but riskier to get right under the same session's time
budget), this phase ships Eq/Ge as an ordinary chain position only, leaving the OR-composition
case exactly as safely refused as it already was. Verified with a dedicated test using a fixture
built from the real `O(41)` shape (genericized per `docs/13-data-boundary.md`), not just assumed.
Same deferred status as the already-known multi-contact-OR-branch/nested-OR-merge cases — a
legitimate future phase, not silently dropped.

11 new converter tests (`ComparisonTests.cs`: rail-facing and mid-chain reduce/round-trip/
serialize, the OR-merge-composition hard-error, an unconfirmed-Part-Name hard-error); one existing
test repurposed (`LiteralConstant` was previously the "unrecognized scope" example — now a real,
recognized scope, so the test's fixture moved to a scope name that's still genuinely unsupported).
94 converter tests, 68 openness-cli tests, 11 golden-harness tests all pass.

### S1 item 9 continued: live re-verification, 2026-07-11

The three stale `Siemens.Automation.Portal` processes that blocked the first attempt were
terminated (project owner's explicit go-ahead — nothing unsaved was open in any of them),
confirmed clean via `Get-Process`, then `openness-cli` launched a fresh single instance
successfully. Interesting footnote: a fresh single-session launch also settles at 3
`Siemens.Automation.Portal` processes on this machine — apparently normal for how V20 starts
(launcher + main + a helper, or similar), not itself a symptom of a stale/broken state; the
original attach timeout was something else (transient, unconfirmed).

Fresh `export` of `FC ControlDelays` succeeded immediately. `converter to-ir` against the whole
block reproduced exactly the predicted result: `UnsupportedConstructException: Unsupported
instruction 'Mul' (UId=36)` — the *first* construct hit, because `Mul`/`Convert` live entirely in
Network 1 (`CompileUnit ID="3"`), which comes first in file order and is completely unrelated to
Eq/Ge (confirmed by grepping the fresh export: `Mul`/`Convert` only ever appear in `ID="3"`;
`Eq`/`Ge` only in `ID="8"`/`"D"`/`"12"`). Since `to-ir` throws on the first unsupported construct
across the whole block, Networks 2–4 (all the Eq/Ge content) never actually got exercised by that
run — a real, honest caveat, not glossed over.

**Isolated Network 2 (`CompileUnit ID="8"`) directly** — extracted its raw `<FlgNet>` content
from the fresh export into a standalone file (never committed) and ran it through
`FlgNetParser.Parse` → `GraphReducer.Reduce` → `FlgNetBuilder.Build` → `FlgNetWriter.Write` →
re-parse directly (a temporary, throwaway test, deleted immediately after). Result — precise and
better than expected:

- `Eq(32) → Contact(33) → TON(34).IN` reduced **successfully**, no error — confirms the
  rail-facing-comparison-feeding-a-Contact-feeding-a-TON shape works against genuinely live,
  unmodified real data, not just the genericized fixture.
- The Coil's own chain (which needs `O(41)` — the OR-merge combining `Ge(39)`/`Eq(40)`) threw
  exactly the predicted, already-tested error: `NonReducibleNetworkException: OR-merge UId=41
  branch 1 is a 'Ge', not a Contact — multi-element/non-contact OR branches are outside this
  slice.` — from the stack trace, this fired inside `ReduceOneChain` (the Coil's chain), *after*
  `ReduceTimer` (the TON's `IN` chain) had already succeeded — live proof that the scope decision
  (comparisons as an ordinary chain position; OR-merge composition deliberately refused by the
  existing, unmodified check) is exactly right, on the exact real network that motivated it.

**Bottom line:** comparisons are now proven against genuinely live data for the part that's in
scope, with the deliberately-deferred part confirmed to fail exactly as designed, not by
accident. A full whole-block `import → compile → re-export → Normalizer` round-trip for
`ControlDelays` itself still isn't reachable (Network 1's `Mul`/`Convert`, unrelated to
comparisons) — closing that out would need either MOVE/comparisons-adjacent arithmetic support
landing too, or a purpose-built reference-project block (matching the `TimerSample` precedent)
that avoids Network 1's content entirely.

### S1 item 10 (MOVE support), 2026-07-11

Grounded against a real export, `FB MotorDOL`'s "HMI Motor Status Telemetry" network: a cascade
of `Contact -> Move` taps writing a status code, no `Coil` at all in that real network.

Real finding that changed the original framing: a Move is neither a boolean chain position (like
Eq/Ge) nor a self-contained production like TON — it's a side effect *tapped off* a chain
position's own output via genuine wire fan-out. The same wire that feeds the next chain position
also feeds the Move's `en`, so a wire can carry three endpoints (producer, Move.en tap,
next-position.in) instead of the usual two. This required a core `GraphReducer.TraceChain`
redesign: the fan-out check changed from "exactly one other endpoint on a wire, else throw" to
"find the single endpoint whose (Part, Port) is a genuine producer, ignore every other endpoint
as an uninspected consumer" — every prior capability's fan-out check assumed exactly two
endpoints per wire, which a Move's tap genuinely breaks.

Second consequence, in `FlgNetBuilder`: multiple Moves' own `en` chains commonly telescope
through the same upstream Contacts a Coil's (or another Move's) chain already walked. The
reducer deliberately re-derives the same `ChainStepSidecar` data once per production that traces
through a shared Contact (correct — each production's own sidecar needs a complete, self-
contained picture) rather than trying to share state across productions. Naively rebuilding from
that duplicated sidecar data the old way (each chain-building call emitting its own `WireNode`
directly) would emit duplicate `<Part>`/`<Wire>` elements. Rebuilt `FlgNetBuilder` around a
shared, de-duplicating endpoint accumulator (`AddPart`/`AddEndpoint`, keyed by UId) — the old
rail-only `railEndpointsByWireUId` special case generalized into the same mechanism used for
every wire.

`DisabledENO="true"` and the `Card=1` `TemplateValue` are fixed in every real instance seen —
hard-validated on parse (`ParseMoveFixedShape`) and unconditionally regenerated on write, never
carried as `PartNode` data (same "don't store a confirmed constant" reasoning as TON's
`InstanceOfType`). A different `Card` value would presumably be a `MOVE_BLK_VARIANT`-style
multi-element copy — real but unconfirmed, refused rather than guessed at.

Readable-form syntax: `MOVE(EN := <expr>, IN := <expr>) => <dest>` — `EN` reduces via the same
`TraceChain` mechanism as a Coil's condition/a TON's `IN`; `IN` is a tag-or-literal operand (same
resolver as TON's `PT`); `<dest>` is always a bare tag (`out1` always wires straight to an
ordinary Access in every real instance seen).

12 new converter tests (`MoveTests.cs`): parse validation (including two hard-error fixtures for
the DisabledENO/Cardinality checks), reduce/round-trip/serialize for the simple no-fan-out case,
and — the load-bearing proof — a fixture built directly from the real telescoping/fan-out shape
(genericized per `docs/13-data-boundary.md`: `Contact1 -> [Move tap, Contact2 continue] ->
Contact2 -> [Move tap, Coil continue] -> Coil`) proving both halves of the redesign at once: the
reducer's deliberate per-production duplication, and the builder's correct de-duplication on
rebuild (exactly 5 Parts, 10 Wires — no duplicates — with both genuinely fanned-out wires ending
up with all 3 real endpoints each, not 2). **This test passed on its first run** — the redesign
holds up against the exact real-world shape it was built for, not just the simple case. All 106
converter tests pass (up from 94); `openness-cli`/golden-harness suites untouched by this work.

### S1 item 10 continued: live verification against real data, 2026-07-11

TIA Portal was already running (3 processes — confirmed normal for this machine per the item 9
footnote above, not a stale-session symptom). Fresh `export` of `FB MotorDOL`
(`station_2/JOB9002_PLC/Motors`) succeeded immediately. Isolated `CompileUnit ID="3F"` (the "HMI
Motor Status Telemetry" network itself — 12 parts, 24 wires, 11 access nodes, 5 constants) into a
standalone file, same technique as item 9's Network 2 isolation, then ran it directly through
`FlgNetParser.Parse → GraphReducer.Reduce → FlgNetBuilder.Build → FlgNetWriter.Write` → re-parse
via a temporary, throwaway test (deleted immediately after use, real data never committed).

**The real topology is richer than any fixture built for this phase:** `Contact37 →
[Move38 tap, Contact39 continue] → Contact39 → [Move40 tap, Contact41 continue] → Contact41 →
[Move42 tap, Contact43.in, Contact44.in]` — Contact41's own outgoing wire fans out to **three**
consumers (Move42's tap plus two Contact continuations), not two. `Contact43`/`Contact44` feed an
OR-merge (`O(45)`) whose own output feeds a fifth Move (`Move46`); a fully separate, independently
rail-fed `Contact47` feeds a sixth production, `Move48`.

Result, precise and better than expected:

- **Parsing succeeded immediately** on the whole real network, including the genuine 3-consumer
  fan-out at Contact41's wire — confirms `FlgNetParser`/the wire model handle arbitrary fan-out
  width, not just the 2-consumer case every fixture happens to test.
- **`Reduce()` failed**, but predictably and for a reason unrelated to MOVE:
  `NonReducibleNetworkException: OR-merge UId=45 branch 1 contact UId=43 is not fed directly from
  Powerrail — multi-contact OR branches are outside this slice.` `Contact43`/`Contact44` are
  downstream of `Contact41`, not directly rail-fed — the *existing*, unmodified, already-deferred
  multi-contact-OR-branch check (same class of gap already known from comparisons' `O(41)` case)
  refused it correctly. **Not a new gap and not a MOVE-specific bug** — the same real network would
  hit this identical error if `O(45)` fed a Coil or a TON instead of a Move.
- **Confirmed via Part document order (`Reduce()` processes Moves in source order: 38, 40, 42, 46,
  48, throwing on the first failure) that `Move38`, `Move40`, and `Move42` all reduced
  successfully before the throw** — including `Move42`, whose own `en` taps directly off the
  genuine 3-way fan-out wire. This is real, live, positive proof of the core mechanism (fan-out
  tap identification, telescoping chain re-derivation) against unmodified production data, not
  just the genericized `MoveTelescopingChain.xml` fixture.
- **A fully clean, real, self-contained sub-network was isolated and round-tripped end to end
  successfully:** `Contact47 → Move48` (both `Access`/`Wire` UIds byte-identical to the real
  export, with exactly one necessary trim — the shared rail wire, UId 49, reduced from its real
  2-consumer form, `Contact37` + `Contact47`, to just the `Contact47` endpoint, since `Contact37`
  isn't part of this isolated subset; the same "rail wire is commonly shared" pattern this project
  already tracks separately, not a fabrication). `Parse → Reduce → Build → Write → Reparse`
  produced a wire-for-wire, part-for-part match against the original — genuine positive
  full-pipeline proof.

**Bottom line, matching item 9's own pattern exactly:** MOVE is now proven against genuinely live
data for the part that's in scope (parsing, fan-out producer-identification, telescoping
re-derivation, and a full clean round-trip on an isolated real sub-network), with the
deliberately-deferred part (multi-contact OR-merge branches) confirmed to fail exactly as
designed, on real data, for a pre-existing reason unrelated to this phase's work. A full
whole-block `import → compile → re-export → Normalizer` round-trip for the "HMI Motor Status
Telemetry" network itself still isn't reachable (needs the OR-merge composition gap closed, same
blocker as comparisons' `ControlDelays` case) — closing that out is a future, shared phase across
both capabilities, not MOVE-specific follow-up.

### S1 item 11 (OR-merge branches generalized to recursive chains), 2026-07-11/12

Closes the exact gap items 9 and 10 both left open. `GraphReducer.ResolveOrMerge` originally
required every OR-merge branch to be a single `Contact` fed directly by Powerrail — real data hit
this limitation twice, both already precisely characterized: `FC ControlDelays`' `O(41)` combines
two comparisons (`Ge`/`Eq`), each fed by a further OR-merge (`O(38)`) rather than Powerrail
directly; `FB MotorDOL`'s `O(45)` is fed by a shared, non-rail-fed Contact (`Contact41`'s own
fan-out). Planned via `/plan` and approved before any code was written (plan file:
`quirky-gathering-ripple.md`).

**Design decision confirmed with the project owner before writing code, via AskUserQuestion:**
once an OR-merge branch can be a compound expression rather than a bare tag, the IR text needs
real operator precedence — `AND` binds tighter than `OR` (standard convention), parentheses only
where precedence alone would misparse — not blanket-parenthesizing every compound branch.

**The fix: an OR-merge branch is resolved via the exact same `TraceChain` mechanism already used
for a Coil's condition, a TON's `IN`, or a Move's `en` — recursion, not a new algorithm.**
`ResolveOrMerge` shrank from a hand-rolled single-hop branch walker (three separate hard-error
checks: branch must be a Contact, must be fed directly from Powerrail, all branches must share
one rail wire) to a thin loop calling `TraceChain` once per `inK` port. A branch that's itself a
multi-Contact chain, a comparison, or a nested OR-merge all just work, handled by `TraceChain`'s
own existing per-part-kind dispatch — no bespoke case added for any of them.

Two structural consequences, both foreseen in the plan and confirmed correct by testing:
- `ChainStepSidecar.OrStep.Branches` changed from a flat `ContactStep` list to a new `OrBranch`
  (`Steps` + nullable `RailWireUId`) — the same `(Steps, RailWireUId)` shape every other
  production already carries, since a branch is now a genuine first-class mini-chain.
- The outer chain's own `RailWireUId` is `null` whenever it terminates at an `OrStep` — once
  branches can diverge (one rail-fed, another terminating at a TON's `Q`), there's no single
  shared value left to bubble up. Mirrors the existing `TimerOutputStep` precedent exactly. The
  common real case (branches genuinely sharing one rail wire, confirmed real 2026-07-10) still
  works with zero special-casing — two branches independently reporting the same wire UId simply
  merge via the existing endpoint-accumulator dedup.

`FlgNetBuilder` needed no new accumulation mechanism at all — the MOVE-era shared, de-duplicating
`AddPart`/`AddEndpoint` accumulator already handles "the same upstream Contact touched by
multiple productions," so an OR-branch sharing a prefix with another branch, a Move tap, or an
unrelated chain elsewhere in the network all dedupe identically. `BuildStep`'s `OrStep` case now
builds each branch's own steps the same way any top-level chain builds its own (recursing into
`BuildStep` itself), terminating at that branch's own `inK` port.

**`IrParser`'s old `ParseExpr` was a real, if previously-unexercised, bug — not just an
enhancement.** It naively checked `.Contains(" AND ")` before `.Contains(" OR ")` regardless of
which one actually had lower precedence, so it never actually handled mixed AND+OR correctly; it
just never got exercised by anything more complex than a flat AND-chain or an OR of single leaves
until an OR-merge branch could be compound. Rewritten as a real precedence-climbing recursive
descent: `ParseOrExpr → ParseAndExpr → ParseUnaryExpr (NOT) → ParsePrimaryExpr` (parenthesized
group, comparison, or leaf). `IrSerializer.SerializeExpr` became precedence-aware to match:
`Parenthesize(expr, needsParens)` wraps an `Or` appearing as an `And`'s or `Not`'s own operand,
nothing else.

Three fixtures already existed as hard-error tests for exactly these shapes (built during earlier
OR-merge/comparisons work, specifically as forward-looking coverage for real-but-unconfirmed
shapes) — repurposed into positive tests rather than replaced, the same "obsolete hard-error test
becomes a positive one" pattern already used twice this project (TON's direct-`Q`-wiring,
`LiteralConstant` scope): `NestedOrMerge.xml`, `OrMergeMultiContactBranch.xml`,
`OrMergeOfComparisons.xml`. One genuine test-authoring bug was caught converting these: an
existing sidecar assertion (`Reduce_ThreeWayOrMerge_SidecarRecordsSharedRailAndBranches`) still
expected the OLD outer `RailWireUId` value instead of the new expected `null` — caught immediately
by the test failing, fixed as a test bug, not a code bug.

One new fixture was needed — none of the three existing ones covered `FB MotorDOL`'s specific real
shape (a single upstream Contact's outgoing wire genuinely fanning out to become the shared prefix
of two different OR-merge branches): `OrMergeSharedPrefixBranches.xml`, built directly from that
real topology at a minimal scale (no Move tap needed to exercise it) — **passed on its first
run**, confirming OR-branch recursion and the MOVE-era dedup/fan-out mechanism compose correctly
together, not just each in isolation. 14 new standalone grammar tests (`IrExprGrammarTests.cs`)
cover mixed AND/OR/NOT/parens precedence independent of the reducer, including a deeply-nested
mixed-precedence-with-parens case (`A AND (B OR C) OR D`) — all passed on first run too.

All three suites green: 130 converter tests (up from 106), 68 openness-cli tests, 11
golden-harness tests (`openness-cli`/golden-harness untouched by this diff, re-confirmed still
green rather than assumed).

#### Live verification against real data, 2026-07-12

TIA Portal was already running (3 processes, the now-familiar normal baseline for this machine).
First `export` attempt for `FC ControlDelays` timed out on attach (3-minute limit) exactly like
one earlier session's first attempt — reproducing the same "transient, unconfirmed" pattern
already on record; a plain manual retry (no process killing, no state changes) succeeded
immediately. `FB MotorDOL` exported cleanly on the first try.

Isolated `FC ControlDelays`' `CompileUnit ID="8"` (the network containing `O(41)`/`O(38)`, 10
parts, 21 wires) and `FB MotorDOL`'s `CompileUnit ID="3F"` (the "HMI Motor Status Telemetry"
network, 12 parts, 24 wires — same network as item 10's own grounding, re-extracted fresh rather
than reused, confirmed byte-identical to the prior session's characterization) into standalone
files, same technique as items 9/10's own live-re-verification. Ran each through `FlgNetParser →
GraphReducer → FlgNetBuilder → FlgNetWriter → re-parse` directly via a temporary, throwaway test
(deleted immediately after use, real data never committed).

**Both real networks now reduce and round-trip completely — the exact result this whole item
exists to produce.** `ControlDelays`' `Network 8` revealed a real shape richer than anything
built from a genericized fixture: the Coil's condition is
`(GeneralEnableDelay.Q OR PlantControl.GeneralEnable) AND PlantControl.Status >= 1 OR
(GeneralEnableDelay.Q OR PlantControl.GeneralEnable) AND PlantControl.Status = -1` — an `Or` of two `And`s,
each itself containing a nested `Or`, confirming the precedence grammar renders deeply-nested,
genuinely real mixed AND/OR correctly with exactly the right parens (only around the nested `Or`
inside each `And`, none elsewhere) — not just the smaller hand-built grammar-test cases. `IN`
reduces to `PlantControl.Status = 1 AND PlantControl.MagEnable`, matching the TON support already proven in
item 8. `FB MotorDOL`'s telemetry network now reduces **all five** `Move` statements (previously
only 3 of 5 got past the `Reduce()` call before the OR-merge blocked the rest) — `Move46`'s own
condition (the one gated by `O(45)`) came back as
`NOT IO.FaultActive AND IO.Run AND IO.RunningFB AND IO.UPSEnable OR NOT IO.FaultActive AND
IO.Run AND IO.RunningFB AND IO.InHand`, the OR-of-two-AND-chains shape predicted from the wire
topology, rendered correctly with no parens needed (AND already binds tighter). Both networks:
every original wire's endpoint set matched a rebuilt wire exactly (part/wire counts identical,
`Parse → Reduce → Build → Write → Reparse` produced a structural match); `ControlDelays`'
deeply-nested IR text additionally round-tripped byte-identically through `Serialize → Parse →
Serialize`, proving grammar stability on the richest real expression seen yet, not just the
reducer's own sidecar fidelity.

**Bottom line:** this closes the loose end left open by both item 9 (`ControlDelays`) and item 10
(`MotorDOL`) — both real networks that were blocked at exactly this OR-merge limitation now fully
reduce and round-trip against genuinely live, unmodified production data. A full whole-block
`import → compile → re-export → Normalizer` round-trip for `ControlDelays` itself still isn't
reachable (Network 1's unrelated `Mul`/`Convert`, a separate deferred capability) — but the
specific gap this item targeted is closed on both real blocks that motivated it.

### S1 item 12 (WAND — bitwise word AND, correcting the AND-merge premise), 2026-07-12

Project owner picked "AND-merge" as the next S1 item, explicitly. Every prior mention in the docs
(`ir/SPEC.md`'s readable-form table, this doc's own item 9 entry) flagged it as unconfirmed — only
`O`/OR had ever been directly observed. CLAUDE.md hard rule 3/project discipline: never guess an
XML shape, so grounding came first, before any code.

**Systematic search: 28 real LAD blocks (Control/Alarms/Coms/Simulation/Motor groups, a broad
representative sweep of the project — not exhaustive across all ~180 blocks) exported and grepped
for a real AND-merge shape. Found none.** No boolean parallel-branch AND-merge (an `O`-sibling)
exists anywhere in the sweep. This is architecturally expected in hindsight, not a dead end:
boolean AND in ladder logic is always expressed as plain series Contacts — unlike OR, which
genuinely needs an explicit merge Part because parallel paths converging require a defined merge
point in the wire graph, AND never does. `ir/SPEC.md`'s original `"A"` table row was a speculative
sketch, never confirmed, and this search is reasonably strong evidence it doesn't correspond to
any real SimaticML construct in this codebase.

**What the sweep did find real:** `Part Name="And"` (the full word, not `"A"`) — in `FB
VSDUpdateComs`, `CompileUnit "17"`: `Word AND 16#89 -> ControlWord`. Structurally this is a
**bitwise/word-level box instruction**, not a boolean chain position at all — `DisabledENO="true"`,
`en`/`in1`/`in2`/`out` ports, `Card`+`SrcType` `TemplateValue`s, closest in shape to `Move`. The
sweep also surfaced (not requested, noted for later, not investigated further this pass): `Ne`/
`Le`/`Lt` comparisons real (`Gt` still unseen), a standalone `Not` Part (distinct from
`<Negated Name="operand" />` on a Contact), `TOF` (timer off-delay), `RCoil`/`SCoil`,
`Add`/`Sub`/`Mul`/`Div`/`Abs`/`Swap`/`Calc`/`Convert` (arithmetic family), `MOVE_BLK_VARIANT`,
`Modbus_Comm_Load`/`Modbus_Master`, `FillBlockI`, `LIMIT`, `Jump`, `WAIT`.

**Reported to the project owner before building anything** — this corrected the premise of the
task itself, not just an implementation detail, per the project's "check before design deviation"
discipline. Project owner chose: build the real bitwise `And` instead of the speculated boolean
merge.

**Design, mirroring `Move` closely (structurally the closest existing pattern) crossed with
`O`'s own `Cardinality`-driven shape:** `en` is reduced via the exact same `TraceChain`
fan-out-tap mechanism as Move's own `en` (confirmed real — the And's `en` shares a rail wire with
three sibling Contacts elsewhere in the network, wire37 in the real export: `Powerrail` +
3 Contact `in` endpoints + the And's own `en`, a 5-endpoint wire). Inputs are `Cardinality`-driven
(`in1`..`inCard`, `Card="2"` in the one real example — `ResolveTagOrLiteralOperand` looped per
port, mirroring how `ResolveOrMerge` already loops over an OR-merge's branches) rather than a
fixed pair of fields, since only one real cardinality value has been seen — not enough to treat as
a universal constant the way Move's own `Card="1"` was after multiple confirming instances.
`SrcType` (`Word`) mirrors a comparison's own `SrcType` field exactly, including the same
"sidecar-only, not shown in the readable IR text" precedent. `PartNode` needed zero new fields —
`Cardinality` (from `O`) and `SrcType` (from `Eq`/`Ge`) were already there, just never
co-occurring on one Part before; the parser's `ParseCardinality`/`ParseSrcType` (renamed from
`ParseOrCardinality`/`ParseComparisonSrcType`) needed a real fix to look their own
`<TemplateValue>` up by its `Name` attribute rather than assuming it's the only one present, since
`And` carries both together on the same Part.

**Naming: `WAND`, not `AND`.** Deliberately avoiding a collision with the existing boolean `AND`
infix operator — a converter-owned vendor-neutral name mapping, the same precedent as
`MOVE_BLK_VARIANT` → `MOVE`. Readable-form syntax:
`WAND(EN := <expr>, IN1 := <expr>, IN2 := <expr>, ...) => <dest>`.

**A real, previously-unexercised parser gap surfaced by this grounding:** Siemens' own
`<base>#<value>` numeric-literal notation (`16#89`) wasn't recognized by `IrParser.ParseLeaf`'s
shape-based literal detection (only `T#`-prefixed time literals and bare integers were, from
TON's PT and comparisons' operands respectively) — without a fix, parsing the serialized text back
would have silently misclassified `16#89` as a `TagRef` rather than a `Literal` (harmless for pure
text-round-trip-stability specifically, since both render identically, but a real, misleading
Expr-tree defect for anything else that might inspect it). Fixed by recognizing the base-number
shape generically (not hardcoded to base 16), matching the existing "recognize a literal by shape,
safe since a real tag path is never purely numeric and never contains `#`" discipline.

8 new converter tests (`WordAndTests.cs`), fixture (`WordAndFedByContacts.xml`) built directly
from the real `VSDUpdateComs` shape (genericized per `docs/13-data-boundary.md`, same UIds and
structure) — **passed on first run**. All three suites green: 138 converter tests (up from 130),
68 openness-cli, 11 golden-harness (`openness-cli`/golden-harness untouched by this diff,
re-confirmed still green).

#### Live verification against real data, 2026-07-12

TIA Portal already running (3 processes, the familiar normal baseline). Fresh `export` of `FB
VSDUpdateComs` succeeded on the first try. Isolated `CompileUnit "17"` (7 parts, 13 wires — same
network already grounded above, re-extracted fresh rather than reused, confirmed byte-identical
structure) into a standalone file, same technique as every prior live verification this session.
Ran it through `FlgNetParser → GraphReducer → FlgNetBuilder → FlgNetWriter → re-parse`, plus a
text-grammar round-trip (`Serialize → Parse → Serialize`), via a temporary, throwaway test
(deleted immediately after use, real data never committed).

**The real network reduces and round-trips completely.** Three independent Contact→Coil rungs
(`Word.%X0 := IO.Run`, `Word.%X3 := IO.SystemHealthy`, `Word.%X7 := IO.FaultReset`) plus
`WAND(EN := TRUE, IN1 := Word, IN2 := 16#89) => ControlWord` — the And's own `en` condition
correctly reduces to the "wired directly to rail, always on" sentinel (`TRUE`) since it shares the
rail wire directly, no Contact of its own in between, exactly matching the genericized fixture's
own shape. Every original wire's endpoint set matched a rebuilt wire exactly; the IR text
round-tripped byte-identically, including the `16#89` literal now correctly recognized as a
literal (not a tag) on the way back in.

**Bottom line:** this closes S1 item 12 with what's actually real, not what was speculated. The
boolean AND-merge open item is resolved as **confirmed non-existent** in this codebase (not
"still unconfirmed") — a definitive answer, not a deferral. The real `WAND` instruction is built,
tested, and live-proven end to end.

### S1 item 13 (`Not` — standalone boolean inverter), 2026-07-12

Project owner picked up `Not` next, one of the constructs the item-12 sweep surfaced but didn't
investigate (`Ne`/`Le`/`Lt` still deferred). Immediately after, project owner asked whether the
converter as built could handle `FC PlantAutoControl` — a real, complex orchestrator block. It cannot
yet, but investigating exactly how far it gets is what grounded this item: `PlantAutoControl`'s very
first network hits `Not` first.

**Grounded twice, independently, before writing any code:** two different real instances in
`PlantAutoControl` (different networks, different UIds) — same bare shape both times, a `Part
Name="Not"` with only `in`/`out` ports, no operand/Access, no TemplateValue of any kind. Genuinely
different from a Contact's own `<Negated Name="operand" />` (which negates a *tag read*): `Not`
inverts whatever boolean value arrives on `in`, as a standalone chain position.

**Design: no new top-level production, purely a new chain-position kind.** Unlike TON/Move/WAND
(each its own `Reduce()` loop target with a dedicated `IrNetwork`/`NetworkSidecar` list), `Not` is
discovered only incidentally when some other production's own `TraceChain` walk hits one — a
Coil's condition, a TON's `IN`, an OR-merge branch, whatever happens to trace through it. Resolved
via the same "chain-terminal via recursive `TraceChain`" pattern established by OR-merge branches
(S1 item 11): a fully self-contained recursive call on the `Not`'s own `in` port produces its own
`(Expr, Steps, RailWireUId?)` triple, wrapped in `Expr.Not` and inserted as one step, then the
outer loop `break`s — mirrors `OrStep` exactly. `ChainStepSidecar.NotStep` mirrors `OrBranch`'s
nested `(Steps, RailWireUId)` shape. Zero new IR-text grammar needed — `NOT <expr>`/`Expr.Not`
already existed from S1 items 7/11 and already had correct precedence handling.

**Both real instances tap a shared wire via genuine fan-out**: an upstream Contact's output feeds
both a separately-continuing chain and the `Not` — the same fan-out-tap mechanism already proven
for Move (S1 item 10), reused here feeding back into a boolean chain rather than terminating in a
side-effect write. `PartNode` needed zero new fields; `FlgNetParser`/`FlgNetWriter` needed zero new
code for `Not` at all — it falls through to the existing generic bare-part handling on both parse
and write.

6 new converter tests (`NotTests.cs`), fixture (`NotFedByContact.xml`) built directly from the real
`PlantAutoControl` shape (genericized per `docs/13-data-boundary.md`) — **passed on first run**. All
three suites green: 144 converter tests (up from 138), 68 openness-cli, 11 golden-harness
(`openness-cli`/golden-harness untouched by this diff, re-confirmed still green).

#### The "gold standard" course-correction

Mid-session, the project owner interjected explicitly: "Remember the gold standard is a lossless
full cycle." This was a necessary correction — every "live-verified against real data" claim in
this doc for S1 items 10 (MOVE), 11 (OR-merge), and 12 (WAND) has, in fact, only ever exercised the
**in-memory C# pipeline** (`FlgNetParser.Parse → GraphReducer.Reduce → FlgNetBuilder.Build →
FlgNetWriter.Write → re-parse`, plus a text-grammar round-trip) against real exported XML via
throwaway tests — never the actual TIA `import → compile → re-export →
Normalizer.AreSemanticallyEquivalent` cycle that is this project's true bar (only `FC TimerSample`,
S1 item 8's live-round-trip section above, has ever reached that full cycle for LAD content). This
distinction wasn't previously called out sharply enough in this doc's own "Live-verified against
real data" sections, which read more definitively than the underlying proof actually supports. It
does not invalidate items 10–12's findings (the in-memory pipeline is real, useful evidence — wire
graphs matched exactly, text round-tripped byte-identically), but it is a materially lower bar than
"lossless full cycle," and this doc should stop implying otherwise.

**Investigated whether `Not` specifically could reach the true full-cycle proof.** Checked the
reference project's existing blocks (`OB Main`, `FC NodeStatusAlarms`, `FC PerimeterSafetyAlarms`,
`FC TimerSample`, plus their DBs) for a viable extension target — none suitable without either
overwriting committed reference content or requiring new-block authorship, which, per the
`TimerSample` precedent (the project owner built that block directly in TIA specifically to close
TON's own live-proof gap), is the project owner's own TIA-UI action, not something to do
unilaterally. Then swept all 20 `CompileUnit`s of a fresh `PlantAutoControl` export for `Not`/`Call`
co-occurrence per network: **every single network pairs `Not` with a `<Call>` block-call
element** (CompileUnit IDs 3, 8, D, 12, 17, 1C, 21, 26, 2B, 30, 35, 3A, 3F, 44, 49, 4E, 53, 58, 5D,
62 — all 20, `Not=1 Call=1` each). No real network in this block can currently be isolated to
prove `Not` alone through the full TIA cycle.

Reported this honestly to the project owner rather than settling for the lesser proof silently.
Project owner's decision: **build block calls next**, since they co-occur with `Not` in every real
`PlantAutoControl` network and would unlock a genuine full-cycle proof for both together, rather than
requesting a purpose-built reference block (the `TimerSample` path) or accepting the current proof
level as sufficient for now.

**Bottom line:** `Not` is built, tested, and round-trips byte-identically through the in-memory
pipeline against a real-shaped fixture — but is **not yet live-verified against the true TIA
gold standard**, and this is recorded explicitly as open, not glossed over. Closing it is now tied
to the next capability (block calls), which is expected to be substantially bigger than `Not` was
(FB instance handling, multi-instance vs. global instance DB references, call-site argument
binding) and will get its own grounding/planning pass before any code.

### S1 item 14 (`CALL` — FB/FC block calls), 2026-07-12

Picked up per the project owner's own explicit decision at the close of S1 item 13: build block
calls next, since `Not` and `<Call>` co-occur in every one of `FC PlantAutoControl`'s 20 real
networks, and closing this gap unlocks a genuine full-cycle proof for both together. Planned with
proper plan-mode rigor first (matching the project's own discipline for "substantially bigger"
capabilities), including a mandatory Phase 0 grounding step before any design was finalized —
per CLAUDE.md hard rule 3, no `<Call>` XML had actually been inspected before this item; only its
*existence* and call targets were known from the earlier Part-Name inventory sweep.

**Phase 0 grounding (fresh `PlantAutoControl` export, scratch temp, deleted after use):**

```xml
<Call UId="58">
  <CallInfo Name="MotorVSDSystem" BlockType="FB">
    <Instance Scope="GlobalVariable" UId="59">
      <Component Name="MotorVSDInst2" />
    </Instance>
  </CallInfo>
</Call>
```

- **`<Call>` genuinely isn't a `<Part Name="Call">`** — it's its own sibling element under
  `<Parts>`. This was the plan's own first open question, and the actual biggest deviation from
  every prior construct's shape (all of which were `<Part Name="...">` variants).
- **`<CallInfo Name="<callee>" BlockType="FB">`** — both attributes present and uniform on all 20
  real instances (`BlockType="FB"` every time; no FC call observed in this block, remains a
  real-but-unconfirmed gap, same treatment as `Gt`/`TOF` elsewhere).
- **`<Instance Scope="GlobalVariable" UId="N"><Component Name="..." /></Instance>`** — identical
  shape to TON's own `<Instance>` (confirmed, not assumed — this is the reuse `ir/SPEC.md`'s TON
  section deliberately anticipated). All 20 real instances are `GlobalVariable` scope (standalone
  instance DBs); no `LocalVariable`/multi-instance call observed in this block specifically —
  low-risk by analogy to TON's own dual-scope proof, not separately grounded.
- **Parameters are sparse, not a full interface snapshot — the single most surprising finding.**
  19 of the 20 real calls have **zero** `<Parameter>` children and zero parameter wires at all —
  just `Instance` + a rail-fed `en`. Only one (in `CompileUnit "35"`) has any: 10
  `<Parameter Name="..." Section="Input"|"Output" Type="Word" />` elements (8 Input, 2 Output),
  each with a matching wire — Input via `<IdentCon/><NameCon Name="paramName"/>` (same shape as
  every other operand wire in this codebase), Output via `<NameCon Name="paramName"/><IdentCon/>`
  (same order as Move's own `out1` destination wire).
- **`en` gating**: every one of the 20 real calls has its `en` wired directly onto the network's
  single shared rail wire — none Contact-gated in this block. The existing `TraceChain` "always-on
  TRUE sentinel" mechanism (proven for WAND) already handles this without any new code.
- **No `eno` port wired anywhere, and `<Call>` carries no `DisabledENO` (or any) attribute** —
  genuinely simpler than Move/WAND in this one respect: there's no fixed shape to validate at all,
  just nothing there.
- **Best real fixture candidate found: `CompileUnit "35"` ("Tomra Auto Control").** Its only Part
  kinds are `Contact`/`Coil`/`Ge`/`Eq`/`O`/`Not`/`Call` — every one already supported by the
  converter once `Call` landed. This became the live-verification target below.

**Design, confirmed against grounding (matched precedent closely, one real deviation):**
`Call` reduces as its own top-level production (like TON/Move/WAND, not like `Not`) —
`GraphReducer.ReduceCall` mirrors `ReduceMove`/`ReduceWordAnd` closely: `en` via the same
`TraceChain` mechanism, `Instance` required (factored `FlgNetParser.ParseInstanceReference` out
of `ParseTon` once a second real caller confirmed the shape is genuinely shared), and arguments
resolved by iterating the source's own sparse, ordered `<Parameter>` list — an Input via the same
`ResolveTagOrLiteralOperand` used everywhere else, an Output via `ResolveOperand` with the
source's own Parameter Name as the port (not a fixed port like Move's `out1`). New model types:
`CallArgument`/`CallStatement` (IR-facing) and `CallArgumentSidecar`/`CallStatementSidecar`
(round-trip-facing) — `BlockName` and each argument's `ParamName` are carried on *both* sides
(not just derived by index), since `FlgNetBuilder` works entirely off the sidecar, never
cross-referencing the model, matching this codebase's existing discipline. `FlgNetParser`/
`FlgNetWriter` needed a genuinely new mechanism (not just a new `Name` value): the top-level
`<Parts>` loop now branches on element name (`Part` vs `Call`), and `FlgNetWriter` emits/expects
the sibling `<Call>`/`<CallInfo>` shape specifically for `PartNode(Name="Call")`. Readable-form
syntax:
`CALL <BlockName>(<InstancePath>, EN := <expr>, Param1 := <expr>, ..., OutParam => <tag>, ...)`
— `EN` shown explicitly (a deliberate choice matching MOVE/WAND's own convention for full
losslessness, even though every real instance seen is trivially `TRUE`; `ir/SPEC.md`'s original
`CALL` sketch predates this convention and didn't show it — corrected there too).

14 new converter tests (`CallTests.cs`), two fixtures genericized from the two real shapes found
(`CallBareFedByRail.xml` for the common unparameterized case, `CallWithParametersFedByRail.xml`
scaled down from the one real 10-parameter instance) — **passed on first run**. All three suites
green: 158 converter tests (up from 144), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

TIA Portal already running. Fresh `export` of `FC PlantAutoControl` succeeded (first attempt hit the
first-connect/attach timeout — not the approval-dialog case, just a slow wake, succeeded on
retry with a longer `--timeout-connect`). Isolated `CompileUnit "35"` ("Tomra Auto Control") into
a standalone file, same technique as every prior live verification this session, via a
throwaway test (deleted immediately after use, real data never committed).

**The real network reduces and round-trips completely** — the first real network combining `Not`
and `Call` together to do so. Structurally, this one network alone exercises: 8 independent
Contact→Coil rungs; an OR-merge of two comparisons (one gated by a negated contact) feeding a
9th Coil; that same OR-merge wrapped in `Not`, combined with a further negated contact and a
comparison, feeding a 10th Coil; and the fully-wired 10-parameter FB call (8 Input + 2 Output,
all Word) sharing the network's rail wire with everything else. Every original wire's endpoint
set matched a rebuilt wire exactly; the IR text round-tripped byte-identically end to end
(`Serialize → Parse → Serialize`). Real tag/DB/equipment names are not reproduced here per
`docs/13-data-boundary.md`'s genericization rule — described structurally only.

**Not yet verified through the true TIA `import → compile → re-export → Normalizer` cycle.**
`RoundTripRunner.RunFull` (the harness's own real-project orchestration, `tests/golden/
GoldenHarness.Tests/RoundTripRunner.cs`) operates at whole-block granularity —
`openness-cli import` has no per-network import — and `PlantAutoControl` as a whole still has
`SCoil`/`RCoil` elsewhere (3 each), so a whole-block run would still fail at those, unrelated to
`Call`. A fourth option beyond the three considered for `Not`'s own gap (build `SCoil`/`RCoil`;
request a purpose-built reference block; accept the current proof level) was identified but not
attempted: splice this network's own regenerated XML back into the real, otherwise-untouched rest
of the `PlantAutoControl` export and re-import the whole file. Technically feasible (the scratch copy
is precisely the sandboxed area this kind of import experimentation is meant for), but genuinely
writes regenerated content back into a real production block's actual TIA-side state, even if
scoped to one network — left for the project owner's own explicit call rather than assumed
authorization, consistent with this session's discipline of never taking a more invasive action
than what's been explicitly asked for.

**Bottom line:** `Call` is built, tested, and live-round-trips in-memory against real data,
including the richest real network found combining it with `Not`. The true TIA-cycle gold
standard for this specific network remains open — recorded explicitly, not glossed over. Per the
project owner's own sequencing, `SCoil`/`RCoil` is next; closing that out is the most natural
path to finally reaching the true cycle for a whole real block, `PlantAutoControl` included.

### S1 item 15 (`SCoil`/`RCoil` — set/reset coils), 2026-07-12

Picked up per the project owner's own explicit sequencing at the close of S1 item 14: 3 of each
real in `FC PlantAutoControl`, also seen alongside TON in `FB MotorDOL`'s own earlier grounding (S1
item 8 — the one real "TON's `Q` wired directly into a downstream part" example that couldn't be
modeled at the time fed an `RCoil`, itself out of scope then). Lighter-weight than `CALL` —
grounded directly rather than through a full plan-mode cycle, since the shape turned out to be
the simplest of any construct built this session.

**Grounded first (two independent real instances of each, different `CompileUnit`s), before any
code, per CLAUDE.md hard rule 3:**

```xml
<Part Name="SCoil" UId="80" />
...
<Part Name="RCoil" UId="83" />
```

Both completely bare — no attributes or children beyond `Name`/`UId`. Their own wires (scoped
precisely to one `CompileUnit`, not just grepped loosely across the whole file, to avoid
cross-network UId collisions): `<NameCon UId="79" Name="out" /><NameCon UId="80" Name="in" />`
(condition feeds `in`) and `<IdentCon UId="47" /><NameCon UId="80" Name="operand" />` (target tag
via `operand`) for the `SCoil`; the exact same shape for the `RCoil`. No `out` port on either in
either grounded instance — never a producer, exactly like a plain `Coil`. **Structurally
identical to `Coil` in every respect this converter models** — the only difference is the Part
Name itself and its runtime semantics (which the IR doesn't compute).

**Design, directly from grounding (no design ambiguity beyond one free naming choice):**
`GraphReducer.ReduceOneChain`/`FlgNetBuilder.BuildOneChain` are reused verbatim for all three
kinds — genuinely the smallest diff of any item this session, since there's no new chain-position
kind, no new top-level production, and no new wire shape to handle at all. The only addition is a
new `CoilAssignment.Kind` field (`Assign`/`Set`/`Reset`, IR-facing), set from the source Part Name
in a new `GraphReducer.CoilKindFor` helper and read back in a new `FlgNetBuilder.CoilPartNameFor`
helper — mirroring `ChainStepSidecar.CompareStep`'s own `PartName`↔`Operator` split (one XML-facing
concept, one IR-facing). `CoilAssignmentSidecar` gained **no new field**: `FlgNetBuilder.BuildOneChain`
already takes the model `CoilAssignment` alongside its own sidecar (for a pre-existing leaf-count
cross-check no other production's `Build*` method needs), so `Kind` is derived from the model
directly rather than duplicated — the one place in this codebase where the "sidecar must be fully
self-sufficient" rule (established for `CALL`'s own `BlockName`) doesn't apply, because this
particular `Build*` method was never sidecar-only to begin with.

**One free design choice, resolved by precedent rather than asked about:** readable-form keyword
naming. Every IR keyword built so far mirrors its own source Part Name (`COIL`/`TON`/`MOVE`/
`CALL`) except `WAND`, which deliberately diverges from `AND` to avoid a real naming collision
with the boolean infix operator — a collision that doesn't apply here. Chose `SCOIL`/`RCOIL`
(not `SET`/`RESET`) on this basis, consistent with the dominant convention rather than the
IEC-idiomatic alternative.

6 new converter tests (`SCoilRCoilTests.cs`), one fixture interleaving `Coil`/`SCoil`/`RCoil` on
a shared rail wire (including a realistic touch grounded elsewhere in this project: the `SCoil`
and `RCoil` targeting the *same* tag path via two separate `<Access>` UIds, a real, previously-
confirmed pattern) — **passed on first run**. All three suites green: 164 converter tests (up
from 158), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

TIA Portal already running (a fresh `export` hit the same slow-wake attach timeout seen earlier
this session — not the approval-dialog case, succeeded on retry with a longer
`--timeout-connect`, same as `CALL`'s own live verification). Isolated the same real `CompileUnit`
already grounded for `CALL` (it also contains the block's own `SCoil`/`RCoil` pair) via a
throwaway test, deleted immediately after use.

**The real network reduces and round-trips completely**, correctly distinguishing `Set`/`Reset`
kinds from the network's own plain `Coil` assignments and its `Call`.

**Then attempted the natural next step: a whole-block round-trip of `PlantAutoControl` itself**, since
`SCoil`/`RCoil` was believed to be its last remaining *instruction-level* gap. `converter to-ir`
on a fresh whole-block export immediately hit a different wall:

```
UnsupportedConstructException: Network (CompileUnit ID=3) has a non-empty Title
("...") - this converter doesn't model Title text yet, only Comment.
```

**This is a real, but already-known and already-documented gap** — `tests/golden/README.md`'s
own "Real gaps found and fixed along the way" list already records it: `BlockSourceParser`
hard-errors on "a non-empty `Title` (distinct from `Comment`)... design philosophy #10, applied
retroactively once real data exposed the gap." It just hadn't previously blocked a *real site
block's own whole-block round-trip* specifically — every network in `PlantAutoControl` (confirmed
across all 20, not just the first one hit) carries a real per-network title, an entirely
different kind of gap from anything instruction-level this session has been closing. Not
addressed by this item — flagged honestly, not glossed over, and not something SCoil/RCoil's own
scope should have stretched to cover.

**Bottom line:** `SCoil`/`RCoil` is built, tested, and live-round-trips in-memory against real
data — the smallest, cleanest diff of any S1 item this session, reusing existing machinery
wholesale rather than adding anything new. `PlantAutoControl` has no remaining *instruction-level* gap,
but still doesn't round-trip as a whole block, now blocked by network `Title` modeling instead —
recorded as the next real gap, whichever direction the project owner wants to take it.

### S1 items 16/17 (network- and block-level `Title`) — `PlantAutoControl` fully round-trips, 2026-07-12

Picked up immediately after `SCoil`/`RCoil` surfaced the gap: `converter to-ir` on a whole-block
`PlantAutoControl` export hard-errored on the very first network's non-empty `Title`.

**A real design correction was needed first, not just a new field.** Before writing any code,
inspection of `Program.cs`'s existing wiring found that the IR's own `NETWORK <n> "<title>"`
line — despite `ir/SPEC.md`'s original sketch already calling it "title" — was actually sourced
from the network's `Comment` field (`compileUnit.Comment ?? string.Empty`), not `Title`. This had
never surfaced before: every real network grounded this entire session (`ControlDelays`,
`MotorDOL`, `VSDUpdateComs`, and every `PlantAutoControl` network before this point) had an *empty*
Comment, so the mismatch was invisible. `PlantAutoControl` broke that pattern — real, populated Title
text on every network, Comment empty everywhere — the exact opposite emphasis, which is what
finally exposed the gap.

**Flagged to the project owner before changing anything** (per this project's own "check before
design deviation" discipline) — two options presented via `AskUserQuestion`: (a) leave the
existing Comment-as-title wiring untouched and add a new, separate `TITLE` line for the real
field (zero regression risk, but permanently confusing naming), or (b) repurpose the `NETWORK`
line to actually carry `Title` (matching its own name and how engineers actually use it in the
TIA LAD editor) and give `Comment` its own new, separate `COMMENT` line. **Chose (b)** — the
recommended option, since Comment has never been populated in any real data seen, while Title
demonstrably is what's actually used.

**S1 item 16 (network-level), design and implementation:**

- `SimaticMl.CompileUnitSource` gained a `Title` field, read via the exact same
  `MultilingualTextHelper.ReadMultilingualText` helper Comment already used — no new XML-reading
  code, just a new caller of an existing, already-generic method (`RequireEmptyTitle`'s own hard
  error was simply removed for the network-level call site).
  `MultilingualTextHelper`/`BlockSourceWriter`'s own `DbSourceWriter.WriteComment` was generalized
  to `WriteMultilingualText(text, compositionName, ref nextAuxId)` once a second real
  `CompositionName` ("Title") confirmed the shape is genuinely shared, not Comment-specific —
  `WriteComment` itself kept as a one-line wrapper for every existing call site.
- `Ir.IrNetwork` gained a `Comment` field (mirroring `Title`, which already existed as the
  `NETWORK` line's own label). `IrSerializer.SerializeNetwork`/`IrParser.ParseNetwork` gained a
  new, optional `COMMENT "..."` line, deliberately placed so it survives an `[empty]`-marked
  network too (mirrors Title's own pre-existing behavior — both fields live on a source
  `ObjectList` sibling of `NetworkSource`, independent of whether `NetworkSource` itself has
  content — confirmed architecturally, not just assumed, by tracing through the existing
  `IsEmpty`-then-return code path).
- `Program.cs`: `ConvertToIr` now reduces using `compileUnit.Title` for the network's own label,
  threading `compileUnit.Comment` through afterward via a `with` expression (kept
  `GraphReducer.Reduce`'s own signature untouched — Comment isn't consumed by reduction at all,
  just carried through to the output). `ConvertToXml` threads both lists into
  `BlockSourceWriter.Write`'s now-5-argument signature.
- Sanitization: Title is genuinely identifying free text in real data (equipment/process names),
  so it gets the exact same hard-error-if-unmapped treatment as Comment — a new
  `SanitizationMap.NetworkTitles` dictionary, `Sanitizer.Apply` sanitizing `unit.Title` via the
  same (already fully generic despite its name) `SanitizeComment` helper Comment uses.

7 new converter tests (`NetworkTitleCommentTests.cs`) plus one existing shared fixture
(`SanitizeSource.xml`) extended with a real-shaped network Title — passed on first run.

**S1 item 17 (block-level), grounded while checking `PlantAutoControl`'s own dependency FBs:**
`MotorVSDSystem`/`AirStar` (2 of the 8 real FBs `PlantAutoControl` calls) both carry a real, populated
**block**-level Title — "VSD Motor", identical on both, a shared/templated title across that FB
family. This was genuinely unexpected: S1 item 16's own grounding found only network-level Title
populated, and `RequireEmptyTitle` had never seen a real counter-example at block level before.
Same treatment as item 16: `SimaticMl.BlockSource`/`Ir.IrBlock` gained a `Title` field; a new
`TITLE "..."` line at the top of the IR file (alongside the pre-existing block-level `COMMENT`
line — genuinely a *new* line here, unlike the network case, since the `BLOCK` line's own quoted
text is the block's real Name, not available to repurpose the way a `NETWORK` line's label was);
a new `SanitizationMap.Titles` dictionary, same hard-error-if-unmapped treatment. DB-level Title
remains unconfirmed real, still hard-errored via the now-narrower `RequireEmptyTitle`.

6 more converter tests (repurposing what was originally a hard-error test — `Parse_
BlockLevelNonEmptyTitle_...` — into a positive one, same pattern already used for TON's
direct-Q-wiring and OR-merge's nested-branch cases, plus new Sanitizer coverage). All three
suites green: **177 converter tests** (up from 164), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

**Network-level, whole-block:** fresh `PlantAutoControl` export (first attempt hit the same
slow-Portal-wake attach timeout seen repeatedly this session — not the approval-dialog case,
succeeded on retry with a longer `--timeout-connect`). `converter to-ir` on the entire 20-network
block **succeeded completely** — the first real production block this whole session to fully
convert as a whole block, not just an isolated network. `to-ir → to-xml → to-ir` on the result
produced a byte-identical `.ir` file both times (`diff` confirmed), proving IR self-stability at
full-block scale.

**Block-level:** isolated `MotorVSDSystem`/`AirStar` directly — both now progress past the block-Title
check to a different, already-known gap (a non-empty `Interface` section `Constant` — the same
deferred parameter-interface item `TomraControlSystem` hit via its own `Input` section during the
earlier FB dependency sweep, not a new capability).

#### Attempting the true TIA cycle: cross-project import, per the project owner's explicit instruction

The project owner explicitly directed this *not* be attempted against `JOB9002` itself (to keep the
scratch copy clean) — instead: export from `JOB9002`, import into `SampleProject`, run the full
cycle there. Before executing, flagged that `openness-cli` has no delete/remove-block command, so
whatever lands in `SampleProject` can't be cleaned up programmatically afterward — project owner
confirmed proceeding anyway, manual cleanup accepted.

**Import succeeded** — TIA's own `Import()` accepted the regenerated XML as structurally valid
into a completely unrelated project (real evidence of XML correctness beyond just this
converter's own read-back). **Compile failed: 502 errors**, entirely "tag not defined" (of 323
distinct tag/DB-member paths `PlantAutoControl` references, 462 total references across its 20
networks, ~36 distinct top-level tag-table/DB roots) or "referenced block no longer exists" (its
8 dependency FBs, 20 call instances total) — `SampleProject` has neither `PlantAutoControl`'s own tag
table nor its FB library. **Not a converter defect** — a block doesn't carry its project
dependencies with it; this is inherent to relocating a block cross-project, not a round-trip
fidelity issue. The imported-but-uncompiled `PlantAutoControl` block was left in `SampleProject` for
the project owner's own manual cleanup, as agreed.

**Grounded the 8 dependency FBs directly afterward** (project owner's own follow-up ask): **0 of
8 convert cleanly today.** Three distinct, differently-sized gaps:
- `Mul`/`Convert` (arithmetic) — blocks `MotorDOL`, `EquipmentControlSystem`, `FilterUnitSystem`, `MotorFwdRevSystem`
  (4, all on `Mul`) and `ShredderControlSystem` (1, on `Convert`). 5 of 8 total.
- Block-level Title — blocked `MotorVSDSystem`/`AirStar` before this item; now cleared, both progress
  to the `Interface` gap below instead.
- FC/FB parameter `Interface` sections (`Input` on `TomraControlSystem`; `Constant` on `MotorVSDSystem`/
  `AirStar`) — the same already-known, deferred "full parameter-interface modeling" item, not
  newly scoped by this finding.

Project owner's decision: scope arithmetic support next (the larger of the two remaining real
gaps by block count).

**Bottom line:** `Title` (both levels) is built, tested, and live-verified against real data —
`PlantAutoControl` itself now fully round-trips as a whole block through the in-memory/CLI pipeline,
a first for this session. The true TIA-cycle gold standard for `PlantAutoControl` specifically remains
open, but is now understood precisely: it's blocked by project-external dependencies (tag table +
FB library), not by anything this converter itself gets wrong. Reaching it would mean replicating
a meaningful slice of `JOB9002`'s own tag/block library into `SampleProject` (sanitized) — assessed
as disproportionate to this item's own goal and not attempted; arithmetic support (needed for 5
of the 8 dependency FBs regardless) is the next, more proportionate step.

### S1 item 18 (`MUL`/`CONVERT` — arithmetic, incl. ENO-chaining), 2026-07-12

Picked up per the project owner's own explicit sequencing at the close of S1 items 16/17 ("Let's
do block-level Title now, then scope arithmetic support" → "Commit this, then scope arithmetic
support. Plan mode please.") — the larger of the two remaining real gaps blocking `PlantAutoControl`'s
8 dependency FBs (5 of 8 on `Mul`/`Convert`, vs. 1 of 8 on the already-known, separately-deferred
FC/FB parameter-interface gap). Planned formally in plan mode per explicit request
(`C:\Users\User\.claude\plans\quirky-gathering-ripple.md`).

**Phase 0 grounding, mandatory before design per CLAUDE.md hard rule 3.** Fresh exports of
`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem` (scratch temp, deleted after use) — the plan itself
explicitly refused to finalize a design until the real XML shape was seen, since only Part Names
were confirmed by the earlier fast sweep, not actual shapes:

```xml
<Part Name="Mul" UId="36" DisabledENO="true">
  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
  <AutomaticTyped Name="SrcType" />
</Part>
<Part Name="Convert" UId="37" DisabledENO="true">
  <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
  <TemplateValue Name="DestType" Type="Type">DInt</TemplateValue>
</Part>
```

- `Mul`: `DisabledENO="true"`, `Card="2"` (`in1`/`in2`/`out`, same Cardinality-driven shape as
  WAND). Its own type is `<AutomaticTyped Name="SrcType" />` — self-closing, no value at all (TIA
  infers the type from the connected operands). Genuinely different from every other typed
  instruction built this session, which always carries an explicit `TemplateValue Type="Type">X<`.
- `Convert`: `DisabledENO="true"`, an ordinary `SrcType`/`DestType` `TemplateValue` pair —
  converts *between* two types, unlike anything else built so far.
- **The real surprise, confirmed in two independent instances (`MotorDOL`, `EquipmentControlSystem`)**:
  despite `DisabledENO="true"` on both — matching the Move/WAND precedent that `eno` is never
  wired — real networks have three `Mul`→`Convert` pairs where `Mul`'s own `eno` output wires
  directly into the following `Convert`'s own `en` input: a genuine control-flow chain ("only run
  `Convert` if `Mul` succeeded"), fed by a *preceding box instruction's own output port* rather
  than independently rail-fed or contact-gated like every other `en`/`IN` seen this session. This
  directly contradicted the plan's own inherited Move/WAND-based assumption.
- **Not universal**: `ShredderControlSystem` has a standalone `Convert` (`SrcType`/`DestType` both
  `Int`) with a plain, independently rail-fed `en` — the ordinary case is real too, fully covered
  by the existing `TraceChain` mechanism with zero changes.
- No other arithmetic-family instruction (`Add`/`Sub`/`Div`/`Abs`/`Swap`/`Calc`) observed
  co-occurring in any of the three grounded networks — scope stayed `Mul`/`Convert` only.

**Flagged to the project owner before implementing anything** (per this project's "ask before
design deviation" discipline — the plan's own design section explicitly could not be finalized
until this ground truth was in hand) — presented the finding and a recommended design. Project
owner: **"Go with that design."**

**Confirmed design:**

- New `EnSource` discriminated union (`Ir/Model.cs`) — `Condition(Expr Value)` for the ordinary
  boolean-condition case (everything built before this item), `PrecedingEno` for the newly-found
  chained case. Deliberately *not* folded into `Expr` — there's no tag `Mul` could be referenced
  by (no `Instance` element, unlike `TON`/`CALL`), so "the preceding instruction's own success"
  isn't a boolean-tag-condition concept at all.
- New readable-form reserved sentinel `EN := ENO`, mirroring the existing `TRUE` sentinel
  precedent (a reserved value in the `EN` slot, not a generic expression) — meaning "gated by the
  immediately preceding statement's own ENO." Statement declaration order keeps a chained pair
  textually adjacent for readability, but the sidecar always carries the exact source Part UId
  and wire UId being chained from — parsing never actually depends on adjacency for correctness.
- `GraphReducer.ResolveEnSource` — new shared resolver called by both `ReduceMul`/`ReduceConvert`:
  checks whether a chain step's own `en` wire's only non-self endpoint is `NameCon(<uid>, "eno")`
  on a `Mul`/`Convert` Part; if so, records `EnSource.PrecedingEno` (with
  `EnSourceSidecar.PrecedingEnoSidecar` carrying the source Part UId + wire UId) without calling
  `TraceChain` at all; otherwise falls back to the existing `TraceChain` mechanism unchanged,
  wrapped in `EnSource.Condition`.
- `FlgNetBuilder.BuildEnSource` — the write-side mirror: `ConditionSidecar` reuses the existing
  steps+rail chain-building machinery verbatim; `PrecedingEnoSidecar` directly adds both endpoints
  of the `eno`→`en` wire (`NameCon(precedingPartUId, "eno")` / `NameCon(partUId, "en")`).
- `PartNode` gained `AutomaticSrcType: bool` (shape-only flag, nothing to carry — `Mul`'s own
  type) and `DestType: string?` (reusing the existing `SrcType` field for `Convert`'s source
  half).
- `Card` validated fixed at 2 for `Mul` — the only value observed in either grounded instance;
  hard-errors on anything else, same "don't guess an unconfirmed cardinality" discipline as WAND.
- Readable form: `MUL(EN := <expr-or-ENO>, IN1 := <expr>, IN2 := <expr>) => <dest>` /
  `CONVERT(EN := <expr-or-ENO>, IN := <expr>) => <dest>`. Keywords mirror source Part Names,
  matching the dominant convention (`WAND` remains the one deliberate exception, for a naming
  collision that doesn't apply here).

14 new converter tests (`Converter.Tests/MulConvertTests.cs`), two fixtures genericized from the
real grounded shapes: `ConvertStandaloneFedByRail.xml` (from `ShredderControlSystem` — `Move`+`Convert`
sharing a rail wire, `Convert` reading its input via a separate `Access` UId from the one `Move`
wrote, preserving a real "same tag via two different Access UIds" pattern already seen
elsewhere) and `MulConvertEnoChainedPair.xml` (from `MotorDOL`/`EquipmentControlSystem` — `Mul`→`Convert` with
the `eno`→`en` wire carrying no `Powerrail`, exactly 2 endpoints). All passed on first run. All
three suites green: **191 converter tests** (up from 177), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Isolated both real cases directly: the ENO-chained pair (`MotorDOL`) and the standalone rail-fed
`Convert` (`ShredderControlSystem`) — both reduce and round-trip completely, the sidecar correctly
preserving the exact `eno`→`en` wire (no `Powerrail`, no rail) in the chained case and the
ordinary rail/chain-step shape in the standalone case.

**Whole-block `to-ir` sweep of all 5 previously-blocked dependency FBs**
(`MotorDOL`/`EquipmentControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`/`ShredderControlSystem`) — none hit a
`Mul`/`Convert` error anymore. All five now hit `TONR` instead (a retentive TON variant, distinct
from `TON` and out of scope for any item so far) — confirming `TONR` as **real**, not merely a
name noticed during an earlier sweep, and updating its own status in `ir/SPEC.md`'s Open Items
section accordingly (previously listed under the S1 item 15/`SCoil`/`RCoil` bullet as
"unconfirmed against any real data").

Getting these 5 FBs to actually *compile* in `SampleProject` remains blocked by the same
missing-tag-table/FB-library issue already documented for `PlantAutoControl` itself (S1 items 16/17,
"not attempted, disproportionate to scope") — not attempted again here for the same reason.

**Bottom line:** `Mul`/`Convert` is built, tested, and live-verified against real data, including
a genuine, previously-unknown ENO-chaining control-flow pattern that required a mid-implementation
design check-in rather than being forced into the plan's own original (Move/WAND-based) shape.
Arithmetic beyond `Mul`/`Convert` remains out of scope — nothing observed needing it. The
whole-block sweep confirms `TONR` as the next real gap for these 5 FBs specifically, alongside the
already-known FC/FB parameter-interface gap for the remaining 3.

### S1 item 19 (`TONR` — retentive on-delay timer — plus `Add`/`Lt`), 2026-07-12

Picked up per the project owner's own explicit choice, via `AskUserQuestion`, between the two live
candidates the S1 item 18 sweep left open: `TONR` (blocks 5 of 8 dependency FBs) over the FC/FB
parameter-interface gap (blocks the remaining 3). Planned formally in plan mode
(`C:\Users\User\.claude\plans\quirky-gathering-ripple.md`).

**Phase 0 grounding, mandatory before design per CLAUDE.md hard rule 3.** Fresh exports of
`MotorDOL`/`FilterUnitSystem` (scratch temp, deleted after use) — the two networks turned out
byte-for-byte identical in shape (same UIds, same wiring), a copy-pasted/templated pattern giving
two independent confirmations for free:

```xml
<Part Name="TONR" Version="1.0" UId="40">
  <Instance Scope="LocalVariable" UId="41">
    <Component Name="HrTotaliserTimer" />
  </Instance>
  <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
</Part>
```
```xml
<Wire UId="55"><NameCon UId="39" Name="out" /><NameCon UId="40" Name="IN" /></Wire>
<Wire UId="56"><IdentCon UId="23" /><NameCon UId="40" Name="R" /></Wire>
<Wire UId="57"><IdentCon UId="24" /><NameCon UId="40" Name="PT" /></Wire>
<Wire UId="58"><NameCon UId="40" Name="ET" /><OpenCon UId="50" /></Wire>
```

- `TONR`'s own `Version`/`Instance`/`time_type` shape is identical to `TON`'s — no `EN`/`ENO` on
  either. The one genuine new element is port `R` (reset), fed directly by a plain tag `IdentCon`
  in both instances — no chain, exactly the same shape as `PT`.
- `PT`: both a tag-fed example (`MotorDOL`) and a literal example (`FilterUnitSystem`'s own sibling
  network, `T#1H`) confirmed real. `ET`: `OpenCon` (unconnected) in both, same as `TON`'s own
  precedent. `Q`: read back via an ordinary 2-component `Access` in both — no live example of `Q`
  wired directly into a downstream part for `TONR` specifically (the existing `TimerOutputStep`
  mechanism is already generic and should work unchanged if this occurs, but isn't live-proven for
  `TONR` by this grounding pass).
- DB side: `<Member Name="HrTotaliserTimer" Datatype="TONR_TIME" Version="1.0"
  Remanence="Retain" .../>` confirmed exactly matching the shape `ir/SPEC.md` had already
  anticipated as a generically-handled `TON_TIME` sibling — zero new DB-side work needed.

**Real, material scope finding (a full `Part Name="..."` sweep of both grounded files, not
assumed either way)**: `Add` and `Lt` appear alongside `TONR` in these same real networks —
neither was in `FlgNetParser.SupportedPartNames`. `TONR` alone would not have gotten either FB to
fully round-trip. **Flagged to the project owner via `AskUserQuestion` before implementing
anything; approved: bundle `Add`/`Lt` into this item** rather than deferring them (per this
project's "ask before design deviation" discipline — the same pattern used for S1 item 18's own
mid-grounding surprise).

- `Add`: `<Part Name="Add" UId="45" DisabledENO="true"><TemplateValue Name="Card"
  Type="Cardinality">2</TemplateValue><AutomaticTyped Name="SrcType" /></Part>` — identical shape
  to `Mul`. **Checked directly against the real wiring before writing any code**: `Add`'s own `en`
  in the grounded network is fed by `Lt`'s own `out` port, not an `eno` — confirmed to be the
  ordinary `TraceChain`-resolved `Condition` case, not the `Mul`/`Convert` ENO-chain shape. This
  simplified the design: `ResolveEnSource`'s existing `precedingPart.Name is "Mul" or "Convert"`
  whitelist was deliberately left untouched, no speculative `"Add"` branch added without live
  evidence.
- `Lt`: `<Part Name="Lt" UId="44"><TemplateValue Name="SrcType" Type="Type">UDInt</TemplateValue></Part>`,
  wired via `pre`/`in1`/`in2` — structurally identical to `Eq`/`Ge`. Needed zero new model shape —
  `ChainStepSidecar.CompareStep` already carries `PartName` generically.

**Design**: `TimerKind` (`Ton`/`Tonr`) and `MulKind` (`Multiply`/`Add`) enums, mirroring the
`CoilKind` (`Assign`/`Set`/`Reset`) precedent exactly. Both duplicated onto their own sidecar
records (not left model-only) — confirmed necessary by reading `FlgNetBuilder.cs` in full before
touching it: `BuildTimer`/`BuildMul` are sidecar-only and never take the model alongside (unlike
`BuildOneChain`, which does, and is the one place `CoilAssignment.Kind` is *not* duplicated onto
its own sidecar). Readable form: `TONR(<instance path>, IN := <expr>, PT := <expr>, R := <expr>)`
— same as `TON` plus the confirmed-real `R` argument, parsed via a variable-arity
split-on-top-level-commas approach (mirroring WAND/CALL/MUL's own precedent) since `TON`/`TONR`
now genuinely differ in argument count, rather than a single fixed-arity regex. `ADD(EN :=
<expr-or-ENO>, IN1 := <expr>, IN2 := <expr>) => <dest>` mirrors `MUL` exactly, one shared
parse/serialize path distinguished by keyword. `Lt` needed no new syntax — comparisons are already
plain infix `Expr.Compare`, just gaining a new `<` operator symbol alongside `=`/`>=`.

16 new converter tests (`Converter.Tests/TonrTests.cs`), three fixtures genericized from the real
grounded shapes (`WithTonr.xml`, `LtFeedsCoil.xml`, `AddFedByComparison.xml` — real tag names like
`HrTotaliserTimer` were not reused verbatim in committed fixtures; invented generic names used
instead, per `docs/13-data-boundary.md`). All passed on first run after one assertion-shape fix
(a Contact upstream of the comparison in both new fixtures ANDs with it, same as the existing
`GeMidChainFeedsCoil.xml` precedent — initial test assertions wrongly expected the bare compare).
All three suites green: **207 converter tests** (up from 191), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Exported all 5 previously-`TONR`-blocked dependency FBs fresh
(`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) and ran `converter to-ir`
on each. **All 5 fully converted** — confirmed each genuinely exercises the new code (not a lucky
no-op) via a grep count: exactly one `TONR(`, one `  ADD(`, and one `<` comparison operator per
block. `MotorDOL`/`FilterUnitSystem` (the two directly grounded) were additionally carried through a
full `to-ir → to-xml → to-ir` cycle — **byte-identical both times**, proving real round-trip
correctness, not just a one-way conversion. All real exported data deleted from scratch temp
immediately after use, confirmed via `git status --short`.

**Bottom line:** `TONR`/`Add`/`Lt` are built, tested, and live-verified against real data — a
scope check-in mid-grounding (bundling `Add`/`Lt` in) was needed and explicitly approved, same
discipline as every prior item's own design deviations. **All 5 of `PlantAutoControl`'s dependency FBs
previously blocked by `Mul`/`Convert`/`TONR` now fully round-trip as whole blocks** — combined
with S1 item 18, this closes every real *instruction-level* gap the original 8-FB grounding sweep
found. The remaining 3 (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) are blocked only by the separate,
already-known, larger FC/FB parameter-interface modeling item — not addressed by either S1 item 18
or 19, and not attempted here.

### S1 item 20 (FC/FB parameter-interface modeling — `Input`/`Output`/`InOut`/`Constant`), 2026-07-12

Picked up per the project owner's own explicit request ("let's approach the interface side of the
FC/FBs") — the last real gap left from the original 8-FB `PlantAutoControl` dependency sweep.
`TomraControlSystem` (non-empty `Input`), `MotorVSDSystem`/`AirStar` (non-empty `Constant`) all hard-error in
`BlockSourceParser.ParseInterface`'s `default:` case. Planned formally in plan mode.

**Phase 0 grounding carried an unusually strong mandate**: unlike every prior item this session,
a full-codebase search confirmed **zero real `Input`/`Output`/`InOut`/`Constant` member XML had
ever been captured anywhere** — not in `ir/SPEC.md`, not in `docs/notes/stage-gates.md`, not in
any fixture. The one existing fixture touching this (`FbWithNonEmptyInput.xml`) was a hand-built
synthetic shape built only to exercise the hard-error path, explicitly not sourced from a real
export.

**This is a block-level concept, not a call-site one — already decided, not revisited.** ADR-0001
(Decision item 2) chose "reference only... no inline parameter-interface snapshot" for `CALL`
sites: "the callee's own IR file is the single source of truth for its interface." So this item
extends `BlockSource`/`IrBlock` alongside `StaticMembers`/`TempMembers` (S1 item 7 Phase B);
`CallStatement`/`CallArgument`/`CallParameterNode` (S1 item 14) are untouched.

Fresh exports of `TomraControlSystem`/`MotorVSDSystem`/`AirStar` (scratch temp, deleted after use):

```xml
<Section Name="Input">
  <Member Name="inputWord0" Datatype="Word" Remanence="NonRetain" Accessibility="Public">
    <AttributeList>
      <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
      <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
      <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
    </AttributeList>
  </Member>
</Section>
```
```xml
<Section Name="Constant">
  <Member Name="PosSpeedError" Datatype="Int" Accessibility="Public">
    <StartValue>50</StartValue>
  </Member>
</Section>
```

- `Input`/`Output`: real, populated on `TomraControlSystem` (8 `Input`, 2 `Output`, all `Word`-typed, no
  nested members). Same attributes as `Static`'s own full shape — **but genuinely missing the
  `SetPoint` `BooleanAttribute`** `Static` members always carry, confirmed by directly comparing
  both shapes in the same file (`Static` members there have 4 `BooleanAttribute`s including
  `SetPoint`; `Input`/`Output` members have only 3). `DbInterfaceMembers.ParseMember`/
  `WriteMember` could not be reused verbatim as a result — gained a `requireSetPoint`/
  `includeSetPoint` parameter (default `true`, so `Static`'s own already-proven behavior is
  unchanged) rather than a parallel type or rewrite.
- `Constant`: real, populated on both `MotorVSDSystem` and `AirStar` (2 independent instances,
  identical shape) — genuinely distinct from both `ParseMember` (requires an `AttributeList`,
  absent here entirely) and `ParseBareMember` (rejects the `Accessibility` attribute as
  unexpected): `Name`/`Datatype`/`Accessibility="Public"` plus a required `StartValue`, no
  `AttributeList`/`Remanence` at all. New `ParseConstantMember`/`WriteConstantMember`.
- `InOut`: present as an empty, self-closing `<Section Name="InOut" />` in all 3 grounded FBs —
  still zero real populated examples anywhere (neither declared nor call-site-wired).
- `Return`: absent entirely (not even an empty element) on all 3 grounded FBs — confirmed
  FC-specific, consistent with IEC/Siemens semantics (FBs return via output parameters).
- **A related, adjacent bug found and fixed in the same path**: `BlockSourceWriter.WriteInterface`
  previously emitted the standard `Ret_Val` boilerplate *unconditionally* for every block,
  regardless of `Kind`. This asymmetry had never actually been exercised against a real FB's own
  populated Interface-section content before this item — grounding confirms it's wrong for every
  FB (which never has a `Return` section at all). Fixed: emitted only for non-FB blocks, directly
  in the path of what this item was already extending.

**Design**: `BlockSource`/`IrBlock` gain `InputMembers`/`OutputMembers`/`ConstantMembers`
(nullable, mirroring `StaticMembers`' own null-means-absent-section convention) and `InOutMembers`
(never null, mirroring `TempMembers`' own convention — no real example of `InOut` being entirely
absent has been seen). Readable form extends the existing `INTERFACE` section (`STATIC`/`TEMP`
machinery, S1 item 7 Phase B) with `INPUT`/`OUTPUT`/`INOUT`/`CONSTANT` subsections, same
`DbMemberLineFormat` line grammar, same null-vs-present-but-empty distinction preserved in the
text (a present-but-empty section still shows its own header with zero member lines).
Sanitization: `Input`/`Output`/`InOut`/`Constant` member names are the same freely block-owner-
chosen category as `Static`/`Temp`'s own — not given the structural exemption, same
`SanitizeMember` helper.

**Also corrected the same file's own stale grammar sketch** (`ir/SPEC.md`'s `INTERFACE` block) —
it had never listed `STATIC` at all despite `STATIC` being the one section actually implemented
since S1 item 7 Phase B, an early draft never reconciled against the real implementation.

10 new/changed converter tests (`BlockInterfaceTests.cs`) — 5 new (`Parse`/`RoundTrip`/`IrRoundTrip`
for both the `Input`/`Output` and `Constant` shapes), plus `Parse_FbWithNonEmptyInput_HardErrors`
repurposed into a positive test (`Parse_FbWithInputOutput_ReadsBothAsMemberLists`) — same
"repurpose an obsolete hard-error test once the real shape is known" pattern already used for
TON's direct-Q-wiring and block-level Title. The old fixture's own synthetic content was corrected
to the real grounded shape at the same time. New fixture `FbWithConstant.xml`. All three suites
green: **211 converter tests** (up from 207), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Exported all 3 previously-Interface-blocked FBs fresh (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) and
ran `converter to-ir` on each. **The Interface gap is genuinely closed for all three** — none hit
an Interface-section error anymore. None fully round-trips as a whole block yet, though: each now
progresses to one further, different, previously-unknown gap:

- `TomraControlSystem` hits `Swap` (`Part Name="Swap"`, presumably a word byte-swap instruction) — not
  yet supported, not grounded at the XML-shape level.
- `MotorVSDSystem` hits `Access Scope="LocalConstant"` — a new, unconfirmed Access scope, distinct from
  the four already handled (`GlobalVariable`/`LocalVariable`/`TypedConstant`/`LiteralConstant`).
- `AirStar` hits a **real, confirmed correction needed to already-committed S1 item 18 code**:
  `Mul UId=43` carries an ordinary `<TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>`
  instead of the self-closing `<AutomaticTyped Name="SrcType" />` shape S1 item 18 confirmed
  universal from `MotorDOL`/`EquipmentControlSystem`. `FlgNetParser` hard-errors on this shape today
  (`<Part Name="Mul"> is missing its <AutomaticTyped> element`) rather than silently accepting
  it — flagged as a new open item, not fixed speculatively; needs its own grounding pass (is
  `AutomaticTyped` vs. explicit `SrcType` a real choice TIA exposes, or does it correlate with
  something else about how the operands are wired?) before `Mul`'s own model changes.

All real exported data deleted from scratch temp immediately after use, confirmed via
`git status --short`.

**Bottom line:** `Input`/`Output`/`InOut`/`Constant` are built, tested, and live-verified against
real data — the Interface gap that blocked `TomraControlSystem`/`MotorVSDSystem`/`AirStar` since the original
8-FB dependency sweep is genuinely closed. None of the three fully round-trips as a whole block
yet, but each now fails for a *different*, newly-discovered, unrelated reason — consistent with
this session's own honest-reporting discipline (report what's actually true, not what would sound
like a bigger win). The `Mul`/`SrcType` finding is the most consequential of the three: a real
counter-example to already-shipped, already-committed code (S1 items 18/19), not a new capability
gap — worth the project owner's attention before any further arithmetic work.

### Follow-up, same day, 2026-07-12: `Mul`'s own `SrcType` fixed

Picked up immediately per the project owner's own explicit instruction ("let's fix the Mul/SrcType
issue") after S1 item 20's own live verification surfaced it. A correction to already-committed
code (S1 item 18), not a new capability — already grounded (one confirmed real instance,
`AirStar`'s own `Mul UId=43`, captured directly during S1 item 20's live verification and already
recorded in this file's own S1 item 20 section above), so implemented directly without a fresh
formal plan-mode cycle.

**Design**: `FlgNetParser.ParseMulFixedShape` now checks for either shape — the original
self-closing `<AutomaticTyped Name="SrcType" />` (`MotorDOL`/`EquipmentControlSystem`, S1 item 18's own
grounding) or an ordinary `<TemplateValue Name="SrcType" Type="Type">X</TemplateValue>` (`AirStar`,
confirmed real) — hard-erroring only if a real instance ever carries both or neither (never
silently guessing which is "the real one"). No new `PartNode` field needed: the *existing*
`AutomaticSrcType: bool`/`SrcType: string?` fields already coexist generically on that record (used
independently by other Part kinds already), so this was a pure parser/reducer/builder extension,
not a model redesign. `MulStatementSidecar` gained a nullable `SrcType` field (sidecar-only,
mirroring `Convert`'s own precedent — the readable IR text is unaffected either way, since `Mul`'s
own type was never shown there). `FlgNetWriter` needed **zero changes** — its existing
`AutomaticSrcType`/`SrcType` writing branches were already generic enough (mutually exclusive,
correct relative ordering) to regenerate either shape correctly once the right data reaches them.

5 new tests (`Converter.Tests/MulConvertTests.cs`) — parse/reduce/round-trip/full-block for the
new explicit-`SrcType` shape, plus a new negative test (`Parse_MulWithBothAutomaticTypedAndSrcType_
ThrowsUnsupportedConstruct`). One new fixture, `MulWithExplicitSrcType.xml` (a standalone
rail-fed `Mul`, genericized from `AirStar`'s real shape — exact real wiring wasn't captured during
the brief live-verification grep that first found this, so the fixture's own wiring mirrors the
already-proven standalone-rail-fed pattern, not a claim about `AirStar`'s own exact wire UIds).
All three suites green: **216 converter tests** (up from 211), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Exported `AirStar` fresh and ran `converter to-ir` — **the `Mul`-specific error is gone**,
confirming the fix works against the real block it was found on. The block now progresses to a
different, already-known, unrelated gap: `Access Scope="LocalConstant"`, the same one `MotorVSDSystem`
(the same FB family, both titled "VSD Motor") already hits — not addressed by this fix. Real
exported data deleted from scratch temp immediately after use, confirmed via `git status --short`.

**Bottom line:** the `Mul`/`SrcType` counter-example found during S1 item 20's own live
verification is now fixed and live-proven against the real block that surfaced it. Two further,
unrelated open items remain from that same live-verification pass — `Swap` (blocks `TomraControlSystem`)
and `Access Scope="LocalConstant"` (blocks `MotorVSDSystem`/`AirStar` both) — neither addressed here.

### S1 item 21 (`Access Scope="LocalConstant"`), 2026-07-12

Picked up per the project owner's own explicit choice — the last of the two real gaps S1 item 20's
own live verification found (`Swap`, blocking only `TomraControlSystem`, stays deferred; `LocalConstant`
blocks both `MotorVSDSystem` and `AirStar`). Planned formally in plan mode.

**Phase 0 grounding, mandatory per CLAUDE.md hard rule 3** — confirmed by full-text search of
`ir/SPEC.md`/`docs/notes/stage-gates.md` that every prior `LocalConstant` mention was purely "the
scope string was seen and blocked the whole-block round-trip," never the actual XML shape. Fresh
exports of `MotorVSDSystem`/`AirStar` (scratch temp, deleted after use) found the shape identical across
4 independent instances (`MotorVSDSystem`: `MinSpd` ×2; `AirStar`: `PulseTimerMS` ×2):

```xml
<Access Scope="LocalConstant" UId="22">
  <Constant Name="MinSpd" />
</Access>
```

- **A genuine fourth Access shape** — neither `AccessNode`'s own `<Symbol>`/`<Component>` shape
  (`GlobalVariable`/`LocalVariable`) nor `ConstantAccessNode`'s `<ConstantType>`/`<ConstantValue>`
  shape (`TypedConstant`/`LiteralConstant`). A bare, self-closing reference by name — **no value
  at all present at the reference site**, genuinely unlike every scope already modeled.
  `MinSpd`/`PulseTimerMS` are exactly the real member names S1 item 20's own grounding already
  confirmed as populated `Constant`-section members on these same two blocks — this is how a
  network reads back a reference to the block's own declared Interface `Constant` member, not a
  literal value inlined at the wiring level.
- **Always single-component** (no multi-part path seen in any instance) and **always at a
  `ResolveTagOrLiteralOperand`-style operand position** — TON `PT` in `AirStar` (feeding a
  `Part Name="TON" UId="30"`'s own `PT` port), comparison `in1`/`in2` and `Move`'s own `in` in
  `MotorVSDSystem`. Never seen at a plain Contact/Coil `operand` position —
  `GraphReducer.ResolveOperand` has no constant-lookup fallback at all, so this mattered for the
  design fork even though it didn't end up forcing the outcome either way.

**Design fork, resolved by grounding, not guessed at beforehand**: neither of the two originally-
anticipated routes (extend `AccessNode`'s `SupportedAccessScopes` allowlist verbatim, or add a
third `ConstantAccessNode` variant) fit the real shape without *some* change. Chosen: model as an
`AccessNode` with a **one-element `ComponentPath`**, reusing `DottedPath`/`FromDottedPath`
completely unmodified — confirmed by inspection that a single-component path already round-trips
through both with zero changes to `AccessNode` itself (`string.Join('.', ["MinSpd"])` = `"MinSpd"`;
`"MinSpd".Split('.')` = `["MinSpd"]`). This means the IR's own tag-ref text (e.g. `PulseTimerMS`)
reads identically to the member's own declared name in that same block's `INTERFACE`/`CONSTANT`
section (S1 item 20) — a real, meaningful correlation for a reader, not just a convenient encoding.
The one unavoidable cost: `FlgNetParser.ParseAccess`/`FlgNetWriter`'s `AccessNode`-writing loop
both needed a scope-conditional branch (`LocalConstant` skips the `<Symbol>`/`<Component>` shape
entirely in favor of `<Constant Name="..." />`) — small, confined to those two functions.
`GraphReducer.cs` needed **zero changes**: `ResolveTagOrLiteralOperand` dispatches by UId lookup,
not by scope, and a `LocalConstant`-scoped `AccessNode` flows into the same `accessByUId`
dictionary as every other scope via the existing, unmodified top-level `Parts` dispatch (no new
`if`/`else if` branch needed there either — only `ParseAccess`'s own internals changed).

5 new tests (`Converter.Tests/TonTests.cs` — a TON `PT` fed by `LocalConstant`, matching
`AirStar`'s own real structural position, plus a negative test for unexpected `<Constant>`
content), one new fixture (`WithTonPtFedByLocalConstant.xml`). All passed on first run. All three
suites green: **221 converter tests** (up from 216), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Exported `MotorVSDSystem`/`AirStar` fresh and ran `converter to-ir` on each. **The `LocalConstant` error
is gone from both** — confirmed genuinely fixed, not a lucky coincidence. Neither fully round-trips
as a whole block yet, though: each now hits a different, new, unrelated gap —

- `MotorVSDSystem` hits `<Call UId="52">` missing its own `<Instance>` element (a genuine
  `SimaticMlFormatException`, not yet grounded — every `<Call>` seen so far, S1 item 14, has
  carried one).
- `AirStar` hits `Ne` (not-equal) — an unsupported comparison Part Name, the first of the IEC
  family's own `Ne`/`Le`/`Gt` siblings of `Eq`/`Ge`/`Lt` confirmed real (the others remain
  unconfirmed, per `ir/SPEC.md`'s own comparison-family note).

All real exported data deleted from scratch temp immediately after use, confirmed via
`git status --short`.

**Bottom line:** `Access Scope="LocalConstant"` is built, tested, and live-verified against real
data — the last of the two real gaps S1 item 20's live verification surfaced is now closed. Neither
`MotorVSDSystem` nor `AirStar` fully round-trips as a whole block yet, consistent with this session's own
honest-reporting discipline (closing `LocalConstant` was never guaranteed to be either block's only
remaining gap, and it wasn't) — each now blocked by a separate, unrelated, newly-found item
(`<Call>` missing `<Instance>`; `Ne`), neither addressed here.

### S1 item 22 (`Ne` — not-equal comparison), 2026-07-12

Picked up per the project owner's own choice, immediately after asking what `Ne` was likely to be.
Answered directly before any grounding, from prior evidence already in this repo: `ir/SPEC.md`'s
own readable-form table already had a row noting `Ne`/`Le`/`Lt` as "confirmed real Part Names...
not yet built" (from an earlier 28-block sweep during the AND-merge investigation), and
`IrParser`'s own `ComparisonTokens` array already carried the `<>` token, unused, waiting for
exactly this — the not-equal sibling of `Eq`(`=`)/`Ge`(`>=`)/`Lt`(`<`), all three already fully
supported (`Eq`/`Ge` original build 2026-07-11; `Lt` S1 item 19).

**Grounded directly against real data** (real `FB AirStar`, the same block `Ne` was first spotted
blocking during S1 item 21's own live verification) rather than a fresh formal plan-mode cycle —
given how small and well-precedented "add a fourth comparison operator" now is (the third time
this session), CLAUDE.md hard rule 3's own grounding requirement was still honored, just without
the heavier plan-mode ceremony:

```xml
<Part Name="Ne" UId="54">
  <TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>
</Part>
```

Identical shape to `Eq`/`Ge`/`Lt` — same `SrcType` `TemplateValue`, same `pre`/`in1`/`in2`/`out`
ports, confirmed by tracing the real wires within this specific network (`pre`→upstream Contact
chain, `in1`/`in2`→operands, `out`→downstream Coil). Also noted in passing while grounding (not
chased): a `Part Name="TOF"` (off-delay timer) instruction nearby — a new, separate, unaddressed
gap.

**Design**: `FlgNetParser.SupportedPartNames`/`SupportedComparisonPartNames` and `GraphReducer`'s
`OutPortFor`/`ComparisonOperator`/`TraceChain` upstream-dispatch each gained a fourth case.
Everything downstream of reduction needed **zero changes** — `ChainStepSidecar.CompareStep.
PartName` and `Expr.Compare.Operator` are both carried verbatim rather than derived from a
hardcoded switch, so `FlgNetBuilder`/`FlgNetWriter`/`IrSerializer`/`IrParser` already handle any
confirmed comparison Part Name generically (the same design that made `Lt`'s own S1 item 19
addition small is what made this one even smaller). Repurposed the one existing test that used
`"Ne"` as its own placeholder for an unconfirmed Part Name — swapped to `"Le"` (still genuinely
unconfirmed), preserving the test's own point (an ungrounded IEC-family name stays refused) rather
than deleting it outright.

4 new tests (`Converter.Tests/ComparisonTests.cs`), one new fixture (`NeFeedsCoil.xml`,
genericized from the real `AirStar` shape, mirroring the existing `Lt`/`Ge` fixture pattern). All
passed on first run. All three suites green: **225 converter tests** (up from 221), 68
openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12

Exported `AirStar` fresh and ran `converter to-ir` — **the `Ne` error is gone**, confirming the fix
against the real block it was found on. Doesn't fully round-trip as a whole block yet: progresses
to `TOF` (spotted alongside `Ne` during this item's own grounding pass — a new, separate,
unaddressed gap, not chased here). Real exported data deleted from scratch temp immediately after
use, confirmed via `git status --short`.

**Bottom line:** `Ne` is built, tested, and live-verified against real data — the third and
smallest comparison-family addition this session, with zero changes needed anywhere downstream of
`GraphReducer`'s own reduction dispatch. `AirStar` still doesn't fully round-trip (now blocked by
`TOF`, not addressed here); `MotorVSDSystem`'s own remaining `<Call>`-missing-`<Instance>` gap is also
still open, unrelated to this item.

### S1 item 23 (`TOF` — off-delay timer), 2026-07-12 — `AirStar` fully round-trips

Picked up per the project owner's own explicit choice, asked directly what `TOF` was likely to be
— answered before any grounding: an off-delay timer, IEC sibling of `TON`/`TONR`, both already
fully supported. Grounded directly against real `AirStar` (the same block `TOF` was first spotted
blocking, S1 item 22's own live verification) rather than a fresh formal plan-mode cycle — small,
well-precedented (third `TimerKind` variant, after `TON`'s own original build and `TONR`'s S1 item
19 addition). One retry needed on export (`TIA Portal did not respond to attach/launch within 5
minute(s)` — the same recurring slow-Portal-wake pattern seen throughout this session, not a real
error; succeeded immediately on retry with a longer `--timeout-connect`).

```xml
<Part Name="TOF" Version="1.0" UId="55">
  <Instance Scope="LocalVariable" UId="56">
    <Component Name="PulseProgramTimer" />
  </Instance>
  <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
</Part>
```

**Structurally identical to `TON`** — same `Version`/`Instance`/`time_type` shape. Wire tracing
within this specific network confirmed `IN`/`PT`/`ET` ports exactly matching `TON`'s own (`IN` fed
by an upstream `Ne`'s own `out`; `PT` fed by a plain tag `IdentCon`; `ET` unconnected, `OpenCon`).
**No reset port** — unlike `TONR`, confirmed by checking for a `NameCon ... Name="R"` reference to
this Part and finding none. No `EN`/`ENO`. No live example of `TOF`'s own `Q` being consumed
anywhere in the grounded network (same "unconfirmed, not needed for this network to reduce" status
`TON`'s own `ET`-consumed case already has). The only real difference from `TON` is semantic
(off-delay vs on-delay timing behavior), which this converter doesn't compute — same "IR doesn't
compute runtime semantics" reasoning already established for `CoilKind`/`TimerKind`'s own `Tonr`
variant.

**Design**: `TimerKind` gains a third variant, `Tof` — needing **zero new fields**, genuinely
simpler than `TONR`'s own addition (which needed a real `Reset` field for its confirmed `R` port).
Every touchpoint `TONR` already generalized just needed a third case added, no new mechanism
anywhere: `FlgNetParser.SupportedPartNames`/the `Parse` dispatch (`TON`/`TONR`/`TOF` share one
branch, `ParseTon` already takes `partName` generically), `GraphReducer`'s `tonParts` collection
filter/`TimerKindFor`/`OutPortFor`/`TraceChain` upstream dispatch, `FlgNetBuilder.
TimerPartNameFor`, `IrSerializer`'s `TimerKeywordFor`/`TimerSidecarKind`, `IrParser`'s
readable-form loop condition/regex alternation/sidecar `kind` parsing (plus one small extra
guard: a 4th, `R`-shaped argument on a `TOF`/`TON` line is now explicitly rejected, since only
`TONR` may carry one). `ReduceTimer`'s own reset-resolution branch is already gated specifically
on `Kind == TimerKind.Tonr`, so `TOF` naturally skips it with no extra logic needed there.

5 new tests (`Converter.Tests/TonTests.cs`, mirroring the existing `WithTon.xml` coverage exactly
— parse/reduce/round-trip/serialize/full-block), one new fixture (`WithTof.xml`, genericized,
matching `WithTon.xml`'s own structure with `TOF` substituted for `TON`). All passed on first run.
All three suites green: **230 converter tests** (up from 225), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12 — a genuine milestone

Exported `AirStar` fresh and ran `converter to-ir` — **succeeded completely, no further errors at
all.** Carried through a full `to-ir → to-xml → to-ir` cycle: **byte-identical**, confirmed via
`diff`. Confirmed this genuinely exercises today's own work, not a lucky no-op: exactly 1 `TOF(`
occurrence and 2 `<>` (`Ne`) occurrences in the converted `.ir` text, via direct grep.

**`AirStar` is now the sixth of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to
end** — S1 item 19 already got `MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/
`MotorFwdRevSystem` there via its own whole-block sweep; this item closes `AirStar` — the entire
chain of gaps this session's own live-verification work progressively found for this specific
block: S1 item 20's `Constant` Interface section → S1 item 21's `LocalConstant` → S1 item 22's
`Ne` → this item's `TOF`. All real exported data deleted from scratch temp immediately after use,
confirmed via `git status --short`.

**Bottom line:** `TOF` is built, tested, and live-verified — the smallest of the three `TimerKind`
variants (no new fields at all) and the capstone of a four-item chain that started with S1 item
20's own Interface modeling work. Only **2 of `PlantAutoControl`'s 8 dependency FBs remain blocked**:
`TomraControlSystem` (`Swap`) and `MotorVSDSystem` (a `<Call>` missing its own `<Instance>`) — both still
open, unrelated to this item, not addressed here.

### S1 item 24 (`CALL` without `<Instance>` — a real FC call), 2026-07-12 — `MotorVSDSystem` fully round-trips

Picked up per the project owner's own explicit choice, to close `MotorVSDSystem`'s own hard error:
`SimaticMlFormatException: <Call UId="52"> is missing its <Instance> element.` Every `<Call>`
grounded so far (S1 item 14, 20 real instances in `FC PlantAutoControl`) called an FB and carried an
`<Instance>` — this was a genuinely new shape question, not just another "add a variant" pattern.
Formally planned (`EnterPlanMode`/`ExitPlanMode`, given the scope: changing already-shipped
`CallStatement`/`CallStatementSidecar` record fields from non-nullable to nullable). Phase 0
grounding was mandatory before any code, per the plan's own explicit gate.

**Phase 0 finding, grounded against real `MotorVSDSystem` (deleted from scratch temp after use) — the
working hypothesis confirmed exactly:**

```xml
<Call UId="52">
  <CallInfo Name="Scale" BlockType="FC">
    <Parameter Name="Input" Section="Input" Type="Real" />
    <Parameter Name="Input_Min" Section="Input" Type="Real" />
    <Parameter Name="Input_Max" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Min" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Max" Section="Input" Type="Real" />
    <Parameter Name="Output" Section="Output" Type="Real" />
  </CallInfo>
</Call>
```

`BlockType="FC"`, confirmed — a call to Siemens' own standard-library `Scale` function (stateless,
no instance DB needed by design). **No `<Instance>` element at all** — not present-but-empty, not
a different shape, genuinely absent: `<CallInfo>` goes straight from its opening tag into its
`<Parameter>` children. Parameters (5 Input + 1 Output, all `Real`) fit the existing `Input`/
`Output` allowlist unchanged, no new `Section`/`Type` handling needed. `en` is rail-fed, same as
all 20 of `PlantAutoControl`'s own real Calls. Only one `<Call>` in `MotorVSDSystem` — no mixed FB/FC scenario
exercised by this specific real instance, though the design still supports both generically (a
network could in principle mix them).

**Design**: `SimaticMl.Model.PartNode.Instance` was already `AccessNode?` — no change needed at
that layer. The actual required changes: `Ir.Model.CallStatement.InstancePath` (`string` →
`string?`) and `CallStatementSidecar`'s `InstanceUId`/`InstanceScope`/`InstanceComponentPath`
(all three → nullable, as one "all-null-together-or-all-non-null-together" group — not a
discriminated union, since there's no second "kind" to distinguish, just presence/absence).
`FlgNetParser.ParseCall` now checks `callInfo.Element(Ns + "Instance")` presence directly rather
than gating on `BlockType` — checked by direct presence rather than assumed from `BlockType`,
since nothing rules out a real counter-example either way (an FC that does carry an Instance, or
an FB that doesn't) even though the two are expected to correlate. `FlgNetWriter.WriteCall` mirrors
this symmetrically on the way out, wrapping `<Instance>`-writing in a null check.
`GraphReducer.ReduceCall`'s own doc comment claim ("Instance is required... every Call carries
one") was corrected; the `call.Instance ?? throw` line became a plain nullable assignment, and the
final `CallStatement`/`CallStatementSidecar` construction passes `instance?.UId` etc. through.
`FlgNetBuilder.BuildCall`'s own unconditional `new AccessNode(sidecar.InstanceUId, ...)`
construction became conditional on `sidecar.InstanceUId is int instanceUId` (this was a genuine,
expected compile error in the interim state between making the sidecar fields nullable and fixing
this call site — caught and fixed as part of the same task, not treated as optional polish).

**Readable-form grammar**: the instance argument is omitted entirely when absent —
`CALL Scale(EN := TRUE, Input := ..., ...)` instead of `CALL Scale(<instance>, EN := TRUE, ...)`
— favored over an empty-string sentinel (which would've been a first in this codebase and harder
to read). Disambiguation isn't actually ambiguous: `EN := ` is a reserved prefix no real instance
path could ever collide with (instance paths are bare dotted tag-shaped text), so the parser
simply checks whether the *first* split argument itself starts with `EN := ` (no instance) vs.
requiring the *second* to (instance present) — mirroring how `EnSource`'s own `ENO` sentinel and
`TimerBinding`'s own optional 4th `R :=` argument already extend this project's variable-arity,
self-disambiguating grammar style. The sidecar's own `instanceuid =`/`instancescope =`/
`instancepath =` lines are omitted together when absent, mirroring the timer sidecar's own
optional `reset` lines (S1 item 19).

6 new tests (`Converter.Tests/CallTests.cs`, mirroring the existing with-Instance coverage exactly
for the no-Instance case — parse/reduce/sidecar/round-trip/serialize/full-block), one new fixture
(`CallFcNoInstanceFedByRail.xml`, genericized down from the real 5 Input + 1 Output `Real` shape to
2 Input + 1 Output, same structure, mirroring `CallWithParametersFedByRail`'s own genericization
precedent). All passed on first run. All three suites green: **236 converter tests** (up from
230), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-12 — `MotorVSDSystem` fully round-trips

Exported `MotorVSDSystem` fresh from the real `JOB9002_PLC` device and ran `converter to-ir` — **succeeded
completely, no errors at all.** Carried through a full `to-ir → to-xml → to-ir` cycle:
**byte-identical**, confirmed via `diff` between the first-pass `.ir` and the round-tripped `.ir`.
Confirmed this genuinely exercises today's own work, not a lucky no-op: grepped the converted `.ir`
text directly for the `Scale` call —
`CALL Scale(EN := TRUE, Input := IO.SpeedPerc, Input_Min := 0.0, Input_Max := 100.0, Scaled_Min :=
0.0, Scaled_Max := IO.MaxRPM, Output => IO.SpeedOutput)` — no instance argument present, exactly
the confirmed shape. All real exported data deleted from scratch temp immediately after use,
confirmed via `git status --short`.

**`MotorVSDSystem` is now the seventh of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to
end.** Only `TomraControlSystem` (`Swap`) remains blocked — the last gap standing between here and all 8
dependency FBs round-tripping, the natural next candidate once raised with the project owner.

**Bottom line:** a real, previously-unconfirmed `<Call>` shape (an FC call with no `<Instance>`)
is built, tested, and live-verified. Required touching more already-shipped code than the last
several items (nullability changes across `Ir.Model`, `GraphReducer`, `FlgNetBuilder`,
`IrSerializer`, `IrParser`), but no design surprises versus the pre-grounding research — the
Phase 0 finding matched the working hypothesis exactly.

### S1 item 25 (`SWAP` — byte-swap box instruction), 2026-07-12/13 — `TomraControlSystem` fully round-trips, all 8 `PlantAutoControl` dependency FBs closed

Picked up per the project owner's own explicit choice: "let's ground Swap for TomraControlSystem." This
was the last remaining gap in `PlantAutoControl`'s own 8 dependency FBs, after S1 item 24 closed
`MotorVSDSystem`. Grounded first, deliberately scoped narrower than recent items ("ground" only, not
"build") — the project owner wanted to review the real shape before committing to a design.

**Grounding, real `TomraControlSystem` (deleted from scratch temp after use) — 2 independent instances,
identical shape:**

```xml
<Part Name="Swap" UId="34" DisabledENO="true">
  <TemplateValue Name="SrcType" Type="Type">Word</TemplateValue>
</Part>
```

Full network context (isolated via `CompileUnit ID="62"`): `Contact(33) -> Swap(34) -> tag`, en
gated by the Contact's own `out` (same `TraceChain` mechanism as every other en-gated production).
`in` is a plain tag `IdentCon`, `out` writes to a plain tag `IdentCon` — no chaining beyond that in
either direction, no ENO wired. `DisabledENO="true"`, one `SrcType` TemplateValue (`Word`, both
instances) — **no `DestType`**. Structurally this is `Convert` minus `DestType`: a byte-swap
doesn't change the value's type, so there's nothing to declare a destination type for.

**Design fork surfaced and resolved via `AskUserQuestion` before any code**: model `Swap` as its
own standalone `SwapStatement`/`SwapStatementSidecar` record (mirroring `ConvertStatement` minus
`DestType`), or fold it into the existing `ConvertStatement` with a nullable `DestType`. Project
owner chose the standalone type — keeps each source Part Name mapped to its own IR construct
(same precedent as `MulKind`/`TimerKind` staying separate variants), rather than conflating two
distinct source Part Names behind one construct that needs a null-check to know which one it
represents. Project owner then explicitly confirmed proceeding straight to implementation in the
same pass, rather than stopping after grounding+design as the initial request's own narrower
scope might have implied.

**Implementation mirrors `Convert`'s own exactly, minus the `DestType` field/line/group, at every
touchpoint**: `Ir.Model` (`SwapStatement(EnSource En, Expr In, string DestTag)`,
`SwapStatementSidecar` with `SrcType` but no `DestType`; `IrNetwork`/`NetworkSidecar` gain a
`Swaps` list); `FlgNetParser` (`SupportedPartNames` gains `"Swap"`; `ParseSwapFixedShape` validates
`DisabledENO="true"` + `SrcType`, identical to `ParseConvertFixedShape` minus the `DestType`
block); `FlgNetWriter` (`DisabledENO` gate list gains `"Swap"` — the existing `SrcType`-writing
block was already generic, no change needed there); `GraphReducer.ReduceSwap` (`en`/`in`/`out`
resolve via the exact same `ResolveEnSource`/`ResolveTagOrLiteralOperand`/`ResolveOperand` calls as
`ReduceConvert`); `FlgNetBuilder.BuildSwap`; readable-form grammar `SWAP(EN := <expr-or-ENO>,
IN := <expr>) => <dest>` in both `IrSerializer`/`IrParser` (including the sidecar's own
`swapuid =`/`srctype =` lines, minus `desttype =`).

8 new tests (`Converter.Tests/SwapTests.cs`, mirroring `MulConvertTests.cs`'s own Convert coverage
— parse/reduce/sidecar/round-trip/serialize/full-block, plus two negative tests for a missing
`SrcType`/`DisabledENO`), one new fixture (`SwapFedByRail.xml`, genericized from the real
`TomraControlSystem` topology — Contact-gated, not simplified to rail-fed-only, matching the real shape
exactly). One test-assertion mistake self-corrected during the first `dotnet test` run: a
single-Contact-gated `en` reduces to a bare `Expr.TagRef`, not `Expr.And` wrapping one operand
(matching `MoveTests.cs`'s own precedent for a single-Contact `en` — only multi-Contact/OR-merge
chains wrap in `Expr.And`) — fixed the assertion, not the code. All three suites green: **244
converter tests** (up from 236), 68 openness-cli, 11 golden-harness.

#### Live verification against real data, 2026-07-13 — all 8 `PlantAutoControl` dependency FBs now round-trip

Exported `TomraControlSystem` fresh from the real `JOB9002_PLC` device and ran `converter to-ir` —
**succeeded completely, no errors at all.** Carried through a full `to-ir → to-xml → to-ir` cycle
(using distinct scratch filenames throughout to avoid a same-name-overwrite mistake made and
caught on the first attempt): **byte-identical**, confirmed via `diff`. Confirmed this genuinely
exercises today's own work, not a lucky no-op: grepped the converted `.ir` text directly for both
real `Swap` occurrences — `SWAP(EN := PlantControl.Test[5], IN := ControlWord0) => OutputWord0` and
`SWAP(EN := PlantControl.Test[5], IN := ControlWord1) => OutputWord1`. All real exported data deleted
from scratch temp immediately after use, confirmed via `git status --short`.

**`TomraControlSystem` is now the eighth and final of `PlantAutoControl`'s own 8 dependency FBs to fully
round-trip end to end.** Combined with S1 items 18–24's own progressive closures
(`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem` since S1 item 19,
`AirStar` since S1 item 23, `MotorVSDSystem` since S1 item 24), **all 8 of `PlantAutoControl`'s dependency
FBs are now fully instruction-level round-trippable.** The true TIA-cycle proof for `PlantAutoControl`
itself remains separately open (S1 items 16/17's own finding: it depends on external tags/FBs no
other TIA project has) — unrelated to this item, not addressed here.

**Bottom line:** the last remaining real-data gap blocking any of `PlantAutoControl`'s own dependency
FBs is closed. `Swap` turned out to be the smallest possible design fork of this session's several
recent items — structurally a strict subset of `Convert`, resolved with one clarifying question
rather than a full formal plan-mode cycle, since the only real ambiguity was "new type vs. extend
an existing one," not the XML shape itself (which grounding settled immediately, unambiguously).

### `openness-cli`: block deletion, import-overwrite confirmation, API surface survey — 2026-07-13

Picked up per the project owner's own explicit ask, after all 8 of `PlantAutoControl`'s dependency FBs
closed: `openness-cli` had no way to delete a block (flagged as a real gap when `PlantAutoControl` was
left uncompiled in `SampleProject` after the earlier cross-project import test, S1 items 16/17 —
"no delete/remove-block command... manual cleanup accepted"), whether re-importing to a
pre-existing block name actually overwrites it needed live confirmation, and the project owner
wanted a broader survey of unused Openness API surface. Went through a full formal plan
(`EnterPlanMode`/`ExitPlanMode`) given the scope (new gateway method, new CLI subcommand, a new
irreversible-action safety pattern for this codebase).

**Research**: read the installed V20 `Siemens.Engineering.xml` doc-comments file directly
(alongside the DLL already used for the original 2026-07-10 reflection pass) — it carries real
prose descriptions, not just type shapes. Confirmed `PlcBlock.Delete()` exists ("Deletes this
instance.", no arguments) and `ImportOptions.Override` is documented "Override existing" (`None`
= "Throw if exists"). Broader survey (scoped to `SW.Blocks`, where this project's own surface
already lives) turned up several other unused members — full list in
`docs/notes/openness-api-surface-v20.md`'s own new "SW.Blocks survey" section: `SWImportOptions`
(relevant-looking but doesn't fix the cross-project compile blocker), `Find`/`Create` (direct
lookup/group creation, unused), `CreateFB`/`CreateInstanceDB`/`CreateFrom` (S6+ scope, not
buildable now), `GetAttribute`/`SetAttribute` (generic block metadata, unused).

**Design decision, resolved via `AskUserQuestion` in plan mode**: delete requires an explicit
`--yes` flag before actually deleting — without it, the command resolves the block and prints
what it *would* delete, exits a new `ExitCodes.NotConfirmed (10)`, touches nothing. This is a new
safety pattern for this codebase (every other subcommand acts immediately) — delete is the first
genuinely irreversible operation `openness-cli` exposes, and this tool has no undo.

**Implementation**: `IOpennessGateway.DeleteBlock(blockName, deviceFilter, confirm)` mirrors
`ExportBlock`/`CompileBlock` exactly for resolution (`FindMatchingBlocks`, `--device`
disambiguation, `SafetyContentRefusedException` for safety-classified blocks — hard rule 2
extends naturally to deletion). New `delete` subcommand end to end (`ArgumentParser`/`Program.cs`,
mirroring `export`'s own parsing minus `--out`, plus `--yes`). 7 new argument-parser tests
(mirroring `ExportImportCompileArgumentParserTests.cs`'s own `export` coverage) — `DeleteBlock`
itself isn't unit-testable, same reason no `OpennessGateway` method is (Siemens.Engineering's
COM-backed types can't be faked); verified live only.

#### Live verification against real data, 2026-07-13 — both against `SampleProject` only, never `JOB9002`

**Import-overwrite ("update") confirmation** — fully reversible, Green-tier only (used the
already-committed reference-project corpus, `TimerSample`, not `JOB9002` content): exported
`TimerSample` fresh, made one trivial edit (a network `Title` → `"OVERWRITE TEST MARKER"`),
re-imported with `Override` — succeeded, `list` confirmed still exactly one `TimerSample` block
(not duplicated). Re-export initially refused (`"Inconsistent blocks... cannot be exported"` —
the already-documented `IsConsistent` quirk, `docs/notes/openness-quirks.md`); a block-level
`compile --block TimerSample` cleared it in one call, exactly as that doc's own root-cause finding
predicts. Re-exported: **the test marker was present** — confirmed genuine in-place overwrite, not
a silent no-op. Restored the original unmodified content the same way (`Override` again, compile,
re-export), diffed against the very first export: **byte-identical except the `<Created>`
timestamp** — `SampleProject` left in its exact original state.

**Delete** — real cleanup of already-known, already-agreed-to-be-removed cruft: the `PlantAutoControl`
block left uncompiled in `SampleProject` since the S1 items 16/17 cross-project import test.
`list` confirmed it was still there. `delete SampleProject --block PlantAutoControl` (no `--yes`) —
correctly resolved and printed a dry-run preview, exit code 10, `PlantAutoControl` still present on a
follow-up `list`. The confirmed delete itself was initially blocked by Claude Code's own auto-mode
safety classifier ("Irreversible Deletion" — the plan had named `PlantAutoControl` as the target and
the project owner had approved that plan, but the classifier's own bar requires the user to name
the resource directly, not just approve a plan that names it) — stopped and asked directly rather
than working around it; project owner confirmed explicitly ("Yes delete PlantAutoControl from the
sample project"), then `delete SampleProject --block PlantAutoControl --yes` succeeded (exit 0),
confirmed gone via a final `list` — `SampleProject` back to a clean 10-block state.

All three suites green throughout: 73 openness-cli (up from 68), 244 converter, 11 golden-harness.
`git status --short` after each live pass confirmed only the expected code/test files changed —
no data-boundary concerns either way (`SampleProject` is this project's own Green-tier reference
project, not `JOB9002`).

**Bottom line:** `openness-cli` can now delete blocks (with a real confirmation gate, live-proven
against genuine cleanup work), `import`'s own overwrite behavior is now live-confirmed rather than
just trusted from Siemens's own docs, and the broader API survey found several other real
capabilities worth knowing about even though none were built this pass. A genuinely new kind of
finding for this session: an AI-safety-tooling gate (Claude Code's own classifier), not a TIA/
Openness one, caught an under-specified irreversible action and required direct human
confirmation — exactly the kind of check this project's own review discipline already expects.

### `openness-cli`: safe concurrent Portal sessions on different projects — 2026-07-13

Picked up per the project owner's own explicit ask, immediately after the delete/update/survey
item: they need to do their own manual PLC engineering in TIA Portal tomorrow, on a **different**
project than whatever this tool is working on, and wanted work here to continue at the same time.
CLAUDE.md's own environment notes previously said "Only one Portal instance/session assumption:
don't launch parallel Openness sessions" — investigating turned that from a policy note into a
real, fixable code issue.

**The real gap, independent of tomorrow's specific need**: `OpennessGateway.Connect()` attaches to
whichever TIA Portal process it finds first (or launches one if none are running).
`OpenProject()` then checked whether the *target* project was already open in that attached
process; if not, it called `Save()` + `Close()` on **whatever else was open** before opening the
target (built 2026-07-10, "`openness-cli` now switches projects automatically" — safe at the time,
since nothing else was ever running Portal concurrently). The moment a human runs Portal manually,
for a different project, at the same time, this tool could attach to *their* process and silently
save-and-close *their* live project to make room for whatever it was asked to open next — a real
risk to a live engineering session, not a hypothetical one.

**Fix, went through a full formal plan given the stakes (foundational, safety-relevant code every
subcommand depends on)**: `OpenProject()` no longer force-closes anything it didn't open itself.
Target already open in the attached process → reuse (unchanged). Nothing open there → open
directly (unchanged). A **different** project open → leave it alone entirely, launch a dedicated
`new TiaPortal(TiaPortalMode.WithUserInterface)`, open the target there instead. No new CLI flag —
the decision is derived from what's actually open where, not from guessing whose process is
whose, so it's a strict safety improvement with no downside for existing solo use.
`CloseAnyOtherOpenProject` (the old force-close helper) deleted as dead code.

**Live-verified, 2026-07-13 — the real thing, not a simulation**: launched TIA Portal directly
(`Siemens.Automation.Portal.exe`, bypassing Openness entirely) with `SampleProject` open, to
genuinely simulate an independent human session rather than one opened via `openness-cli` itself
(whose own exit-time `Dispose()` behavior was exactly one of the ambiguities this fix depends on).
Left it running, then ran `openness-cli list` against `JOB9002` (a different project) while that
session stayed up. `JOB9002` listed successfully (176 blocks across both stations) via its own
freshly-launched Portal instance — confirmed via `tasklist`: three new process IDs appeared, the
original three (`SampleProject`'s session) were untouched throughout. Re-queried `SampleProject`
immediately after: identical 10-block content, instant reconnect (proving it was never closed,
not just "still running by luck" — a closed-then-reopened project wouldn't reconnect instantly).
Six TIA Portal processes coexisted with zero interference between them. Cleaned up all
test-launched processes via `taskkill` afterward; `git status --short` confirmed only the expected
code files changed. All three suites green throughout: 73 openness-cli, 244 converter, 11
golden-harness.

**Residual, non-fixable caveat, documented rather than built around**: `Connect()`'s very first
`Attach()` call, if it reaches a human's manually-launched process before `OpenProject()`
discovers it's occupied, can still trigger TIA's own one-time first-connect approval dialog on
their screen. `Attach()` alone never opens/closes/saves anything, so there's no data risk — purely
a one-time visual interruption, and the Openness API offers no way to inspect what's open in a
running process without attaching to it first. Didn't occur in this live test (this V20 install
was already trusted machine-wide from earlier sessions) — worth confirming that stays true
tomorrow, but not something further code changes could avoid.

**Still correctly unsupported**: two Openness sessions holding the exact *same* project open at
once — a genuine TIA-side single-writer-file constraint, not a design choice this tool could
relax.

**Docs**: `CLAUDE.md`'s own environment note rewritten to describe the new behavior and its
residual caveat; `docs/notes/openness-quirks.md` gets a dedicated new section plus a "superseded"
note on the 2026-07-10 entry this replaces; `src/openness-cli/README.md` gets a usage-level note.

**Bottom line:** the project owner can now run TIA Portal manually on their own project tomorrow
while this tool keeps working a separate one, with no risk to either session — verified for real,
not just reasoned through. This also closes a genuine, previously-undetected safety gap in
already-shipped code that had simply never been exercised before (nothing had ever run Portal
concurrently with this tool until now).

### `openness-cli`: fixing a real instance-pileup bug in the fix above — 2026-07-14

The fix above traded "force-close whatever's open" for "always launch a fresh instance if
occupied" — correct for the concurrent-session case, but it caused a real Portal-instance pileup
during actual use: a Bash-tool-level timeout killed a mid-import `openness-cli.exe` process
(twice), likely leaving Portal itself stuck server-side, and each retry's `Connect()` only ever
inspected one arbitrary process, found it unusable, and launched yet another rather than checking
whether any *other* already-running process was fine. Process count grew 3 → 4 → 5.

Fixed properly in two rounds, both live-verified against the genuinely messy state this produced
(not a clean simulation): `OpenProject()` now does two full passes across every running process —
first for an exact already-open match anywhere, only then considering an empty process fair game
to open into, only falling back to a fresh instance if neither exists. A separate bug found along
the way (a forward-slash vs. backslash path-comparison mismatch causing a spurious extra Portal
launch) was also fixed. Full story, including the exact TIA lock error that exposed the deeper
bug: `docs/notes/openness-quirks.md` ("Follow-up, 2026-07-14"). All 73 openness-cli tests +
converter/golden-harness suites green throughout. Cleaned up confirmed-idle stray processes with
the project owner's explicit go-ahead before committing.

**Bottom line:** the concurrent-session safety property (never force-close a project this tool
didn't open) is preserved; the resource-pileup side effect that safety property introduced is
fixed by preferring reuse of any already-usable running process over always creating a new one.
