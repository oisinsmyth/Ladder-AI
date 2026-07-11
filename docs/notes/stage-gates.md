# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see Exit-criteria evidence below). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | **ACTIVE (walking skeleton core proven end-to-end, incl. re-export/`Normalizer` equivalence — the earlier "re-export blocked" state was resolved same-session via block-level compile, see detail below)** | — | ADR-0001/`ir/SPEC.md` decided; converter (C#, `src/converter/`), `openness-cli export`/`import`/`compile`/`compile --block`, and golden harness machinery (`tests/golden/`) built and live-verified for Contact/Coil, OR-merge, negated contacts (multi-assignment, slice- and array-addressed), plus GlobalDB/InstanceDB `Static`-section round-trip including one-level structured members. Reference project has 5 committed corpus artifacts (4 FCs/DBs + `PerimeterSafetyAlarms`). All PC-side suites green: 68 converter, 68 openness-cli, 11 golden-harness tests. See Exit-criteria evidence. |
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
`ir/SPEC.md`'s explicit "not now" — revisit only as its own deliberate phase); MOVE/comparisons/
block calls as first-class LAD instructions (TON landed separately, see below; still Contact/
Coil/OR-merge/TON scope on the network-body side otherwise); a multi-contact OR-merge branch or
a nested OR-merge (real-but-unconfirmed, still hard errors).

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

**Not yet live-round-trip-proven.** `ControlDelays` as a whole still needs `Mul`/`Convert`
elsewhere in the same block regardless; a live re-verification attempt was blocked by TIA Portal
session state (three Portal processes running, attach/launch timed out at 3 minutes) — not
retried per this project's "don't kill and retry" discipline (`docs/notes/openness-quirks.md`).
Both grounded shapes are proven by unit tests built directly from the real export. A purpose-built
reference-project block (matching the `TimerSample` precedent that closed out TON's own live-proof
gap) would close this out the same way.
