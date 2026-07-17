# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see Exit-criteria evidence below). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | ADR-0001/`ir/SPEC.md` decided; converter + `openness-cli` + golden harness support ~24 instruction-level constructs (full list: `FlgNetParser.SupportedPartNames` plus `CALL`), all live-verified against real or reference-project data at least once. **`FC PlantAutoControl` round-trips through the true TIA cycle, completely** (2026-07-14 — export → sanitize → import → block-level compile clean → re-export → `Normalizer.AreSemanticallyEquivalent` = true) against its full real dependency closure (8 dependency FBs, 26 DB/tag-table roots) — the actual Layer 1 assertion this stage exists to prove, at production scale. **Reference-project corpus grown from 7 to 14 committed artifacts** (2026-07-14, "Reference corpus growth" below) specifically to close the gap between that production-scale proof and the committed regression suite: the corpus now exercises ~20 of the ~24 supported constructs (up from ~5), not just Contact/Coil/OR-merge/TON. Three smaller flagged gaps (data-boundary doc staleness, Sanitizer `ExternalAccessible`, `Normalizer` Part-identity) also closed the same day. **Signed off with two real items deliberately still open, carried forward rather than blocking the gate**: `WAIT` (missing library dependency in `SampleProject`, needs the project owner's input) and `Jump` (genuine cross-network control flow, needs a real IR-format design decision before any code gets written) — neither is part of the committed reference corpus, so neither affects the literal exit criterion; both tracked in `AITODO.md`. All PC-side suites green at sign-off: 366 converter, 101 openness-cli, 14 golden-harness (offline) + all 14 reference-project blocks verified live together in one `RunAll` pass. See Exit-criteria evidence. |
| S2 — Read and explain | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | Deliverables per `02-roadmap.md`: 4 real JOB9002 blocks explained in conversation (`PerimeterSafetyAlarms`, `MotorDOL`, `PlantAutoControl`, `MotorFwdRevSystem`; not committed anywhere, per `13-data-boundary.md`'s S2-kickoff entry) — 51+ networks sampled, all confirmed accurate by the project owner (2026-07-14), well past the 10-network exit bar. Explanation-quality checklist built and committed (`14-s2-explanation-checklist.md`), derived empirically from re-explaining the same real block across five subagents at varying context levels and verifying every claim against source, not invented solo (full methodology below, "S2: explanation-quality checklist built from direct comparison"). One real error did occur and was caught during that verification pass (a wrong field-uniformity count in the first `PlantAutoControl` pass) — corrected before the project owner's own sign-off; checklist item `E-01` exists specifically because of it. **Signed off with `WAIT`/`Jump` explicitly closed as not needed** (project owner's own call, 2026-07-14) — carried in S1's sign-off as open questions needing input, now resolved rather than deferred (detail in `AITODO.md`'s "Deliberately deferred" section). Modbus's own live-compile gap (same S1 finding) is untouched by this decision and stays open, unrelated to S2/S3. |
| S3 — Comment generation | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | Entry criteria met: S2 done, and `docs/11-review-workflow.md` explicitly agreed by the project owner (2026-07-14). Exit criterion met three times over, at increasing scale: `TimerSample` (Green-tier, title only, both block and network level), `PerimeterSafetyAlarms` (Green-tier, title + comment, both levels), and `PlantAutoControl` (real JOB9002 content — data-boundary approval explicitly extended first — title + comment, block level and all 20 networks, including replacing the original engineer's own titles). All three live-verified end-to-end (edit IR → `to-xml` → `import` → clear the known `IsConsistent` refusal via `compile` → re-export → confirm the new content is genuinely present → confirm nothing structural changed via `Normalizer`) and reviewed/approved before committing. Two real converter gaps closed along the way (embedded-newline guard in `IrSerializer`/`IrParser`; the previously-untested "edit an existing title" scenario, now covered by 4 new tests) plus one real pre-existing corpus bug found and fixed (`TimerSample.ir`'s stale sidecar format) — swept the other 13 committed reference-corpus files afterward to confirm it wasn't a wider gap; it wasn't. Full detail: "S3 first/second/third proof" sections below. |
| S4 — Convention review | **DONE — gate reviewed and signed off by the project owner** | 2026-07-15 | Phase 1 (8 of ~50 rules: C-003/C-005/C-201/C-301+C-501/C-406, plus C-102/C-401/C-404 labeled vacuous) built and tested (46 new tests, 412/412 suite-wide). Live pilot against all 14 reference-corpus files matched every predicted finding exactly. Real-content validation against JOB9002 (`FC StatusAlarms`, station_1 — never previously touched by this project) confirmed the same finding pattern holds outside the reference corpus. **Signed off on a "shown, then confirmed" match, not a genuinely blind one** — flagged explicitly before revealing the tool's output, and again before closing the gate; project owner's own informed call to accept it anyway rather than run a stricter blind pass first. Full detail: "S4 Phase 1" and "S4: real-content validation" sections below. |
| S5 — Data extraction | **ACTIVE** | — | Entry criteria met: S1 done (S4 not required — roadmap explicitly allows running in parallel with S3/S4). Opened 2026-07-15, in parallel with S4 (still open at Phase 1, not blocking). No work started yet — detailed plan being built, same process as S3/S4. |
| S6 — Generation | **ACTIVE** | 2026-07-15 | Entry criteria met: S3 done (already true); seed pattern library exists (`docs/07-pattern-library-spec.md`, two kinds — equipment-instance FB/FC via `CALL`, repeated rung-shape documented from a real example — no new IR mechanism, redesigned from an abandoned `template.ir`/`<SlotName>` approach). Seeded with `patterns/motor-dol/` (ADMITTED, all 5 criteria met) and `patterns/chained-permissive-enable/` (ADMITTED, criterion 3 accepted on partial evidence — drafted-with-prior-knowledge, not blind, gap recorded not erased). Project owner's own call: two well-proven kinds satisfies the roadmap's "~10 patterns" as an approximate target, not a literal count; the rest grows organically once S6 is running. Full detail: "S6 unlock" section below. |
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

### S1 item 26 (UDT / PLC data type support), 2026-07-14

Picked up per the project owner's own explicit ask ("let's work on UDTs next"), directly motivated
by a real gap the paused `MotorDOL` full-cycle test found: even a fully self-contained FB (zero
external tag/DB/FB references) still depends on its own declared UDT (`TypeDOL`) — neither
`openness-cli` nor the converter had any PLC-data-type support at all before this item.

**Phase 0 grounding hit a real, initially-alarming obstacle**: launching a second concurrent Portal
instance to export `TypeDOL` (one `SampleProject` instance was already idly running) repeatedly
failed to connect at all — four patient retries, up to a full 9-minute timeout, all failed
identically with no second process ever appearing (`docs/notes/openness-quirks.md`'s own "A second
concurrent Portal instance sometimes won't connect at all" section has the detail). Respected the
existing discipline around not killing Portal processes without explicit, specific, per-instance
confirmation — rather than force a diagnostic overnight, the item paused with implementation-ready
`openness-cli` plumbing done but zero converter code written against an unconfirmed shape.

**Resolved on retry, not by a fix**: the next session turn, a fresh `export --type TypeDOL --device
JOB9002_PLC` attempt succeeded immediately — a new Portal instance launched, just needed more
patience than the earlier attempts' timeout budgets allowed. The blocker was transient, not a real
concurrency limit; `openness-quirks.md`'s own "unresolved" framing for that section is now stale
and should be read alongside this entry, not as the final word.

**Confirmed real shape**, root element `SW.Types.PlcStruct`, structurally closer to `PlcBlock` than
first assumed but with genuine, confirmed differences (full detail: `src/converter/README.md`'s new
"UDT / PLC data type support" section): no `Number`, no `ProgrammingLanguage` (so no safety
classification concept exists for a UDT at all — confirmed by its absence from the reflected
property list, not assumed); the one Interface section is named `"None"`, not `"Static"`; each
`<Member>` carries no `Remanence`/`Accessibility` attribute (unlike a DB/FB Static member, which
always has both) but the same four `BooleanAttribute`s; a new safety-adjacent
`<IsFailsafeCompliant>` element, hard-errored on if ever anything but `"false"` (CLAUDE.md hard
rule 2 extended to a construct that has no other safety-classification surface); `ObjectList`
carries both Comment and Title `MultilingualText` (a DB's own only carries Comment).

**Design**: reused `DbMember`/`DbMemberLineFormat` directly rather than building a parallel member
model — the four `BooleanAttribute`s and `StartValue` shape match a DB/FB Static member's own
exactly, confirmed by grounding rather than assumed from the surface similarity. New
`PlcTypeSource`/`PlcTypeSourceParser`/`PlcTypeSourceWriter`/`TypeIr.cs` mirror
`DbSource`/`DbSourceParser`/`DbSourceWriter`/`DbIr.cs` closely, simpler in the same places a UDT is
simpler than a DB (no `NUMBER`/`INSTANCEOF` lines). `Sanitizer.ApplyToType` reuses the existing
`Names`/`Tags` map tables unchanged. `openness-cli` gained `ExportType`/`ImportTypes`/`CompileType`
on `OpennessGateway` (using the real `PlcType`/`PlcTypeComposition`/`PlcTypeGroup` API, confirmed
via `Siemens.Engineering.xml` doc comments to mirror `PlcBlock`/etc. almost exactly) and `--type` as
a mutually-exclusive alternative to `--block` on `export`/`import`/`compile`. `list`/`delete --type`
deliberately deferred — not needed for this item's own goal.

10 new converter tests (`PlcTypeTests.cs`, fixture genericized from the real `TypeDOL` shape down to
6 representative members spanning every real datatype seen), 6 new `openness-cli` argument-parser
tests. All three suites green: **254 converter tests** (up from 244), **79 openness-cli tests** (up
from 73), 11 golden-harness.

#### Live verification against real data, 2026-07-14 — the original blocker is resolved; a new, separate one is found

Exported the real `TypeDOL` from `JOB9002_PLC` (`--device` needed — `TypeDOL` exists identically on
both stations), round-tripped `to-ir`/`to-xml` byte-structurally identical (only the already-
accepted DocumentInfo/whitespace/synthetic-MultilingualText-ID differences every other block/DB
round-trip already has). Sanitized (`sanitization/TypeDOL.map.json`, reusing the already-
established `TypeDOL`→`MotorIOSet` name from `sanitization/reference-project.map.json`), imported
cleanly into `SampleProject`, compiled cleanly via the new `compile --type` (the already-documented
`IsConsistent` quirk applies here too — cleared in one call, same as blocks). All real exported
content deleted from scratch temp immediately after use.

**Re-imported the already-sanitized `MotorDOL.sanitized.xml` (paused, waiting since the earlier
full-cycle work) — the original `Data type "MotorIOSet" is unknown` error is gone.** UDT support
directly and completely resolves the gap it was built for.

**A genuinely new, separate finding surfaced immediately after**: importing `MotorDOL` itself now
fails with a *different* error — `"The elements must be sorted according to the current flow"` at
`Part UId=49` (an `RCoil`, network 3, "Start Signal After Inhibit/Fault"). Confirmed this is
unrelated to UDT support or sanitization (sanitization only renames identifiers, never reorders XML
elements) — a real `FlgNetWriter` Part-ordering gap, TIA's own `Import()` validator apparently
expecting Parts/Wires in some data-flow-topological order this converter doesn't currently
guarantee. Per hard rule 7, not hand-patched — flagged as a new, distinct open item rather than
fixed silently inside this one. Confirmed the failed import rolled back cleanly (`list` showed
`SampleProject`'s original 10 blocks, unchanged) and that `MotorIOSet` itself remained present and
compilable throughout.

**Bottom line:** UDT/PLC-data-type support is built, tested, and live-verified end to end — the
exact blocker that motivated it (`MotorDOL`'s own `TypeDOL` dependency) is closed. The broader goal
this session's "full import+compile cycle test" work has been chasing (`MotorDOL` compiling
standalone in `SampleProject`) is not yet reached — the UDT gap that was blocking it is gone, but a
new, separate, unaddressed Part-ordering bug now stands in its place. Worth raising with the project
owner as its own item once this one is committed.

### `openness-cli`: concurrent-Portal stability audit — real bugs found and fixed; feature itself cleared — 2026-07-14

Committed as `557d35c` since S1 item 26 above; this entry covers unrelated work afterward. Project
owner raised a direct concern: the "second Portal instance sometimes won't connect at all" symptom
(`docs/notes/openness-quirks.md`) had now recurred twice in one session with two different
resolutions — was the concurrent-session feature itself (`4841bf5`/`8042648`, 2026-07-13/14)
unstable? Rather than take another anecdotal data point, a full rigorous test plan was designed and
walked through together, live, phase by phase — `docs/notes/concurrent-portal-test-plan.md` has the
complete record (every test's exact PIDs, timings, pass/fail).

**Six phases, real bugs found and fixed along the way, not just observed**:

1. **Phase 0** (single-instance sanity) — 4/4 pass. Two corrections to assumed baselines: one
   logical Portal instance normally presents as ~2 OS processes, not 1; `Save()` (the fix from S1
   item 26's own commit) only writes to disk when the project is actually dirty.
2. **Phase 1 — real bug found and fixed**: `OpenProject()`'s "an empty process is fair game" rule
   didn't distinguish a human's own freshly-launched, still-empty Portal window from anything else
   — it would silently open its own target into a human's window, unannounced. Reproduced live with
   the project owner watching (their own empty window visibly changed to show `SampleProject`).
   **Fixed**: `Connect()` now records whether it had to launch a brand-new instance
   (`_connectLaunchedFreshInstance`); only that exact instance is ever treated as fair game when
   empty. Retested live after the fix: a fresh empty window was confirmed untouched, a separate
   instance launched instead.
3. **Phase 2** (genuine two-party concurrency, different projects, for the first time with an
   actual second human rather than one operator simulating both sides) — 4/4 pass. No interference
   either direction; a real unsaved edit (`AlarmMain` Network 1, `JOB9002`) survived every concurrent
   CLI operation untouched.
4. **Phase 3** (same-project concurrency) — 1/1 pass. TIA's own single-writer lock refused cleanly,
   no hang, no corruption.
5. **Phase 4 — the actual instability question**. Killing the client process genuinely mid-launch
   (reproduced via an atomic launch+find-PID+kill script, confirmed by an empty output file) does
   **not** leave Portal itself stuck — a follow-up call always succeeded cleanly. The decisive test:
   **5 fresh-instance launches in a row, from a clean baseline, alternating target projects — 5/5
   succeeded, no hangs, ~20-28s each.** Every real hang this session ever hit happened with multiple
   stale processes already piled up; from a clean process list, concurrent access was completely
   reliable every time. One real, separate cost was found here too (see below).
6. **Phase 5** (test-coverage gap) — `PathsMatch` (the exact code the 2026-07-14 slash-direction bug
   lived in) had zero unit test coverage. Added 6 tests, `internal`-scoped with a new
   `InternalsVisibleTo`. `FindAlreadyOpenProject` stays live-verified only (real COM-backed types,
   no fake available).

**A second real gap, found during Phase 4 and closed the same day rather than left as an accepted
cost**: a client killed mid-launch used to leave a permanently orphaned Portal process behind —
the Phase 1 fix's own restriction (never trust "empty" unless positively identified as this tool's
own) meant nothing would ever reuse or clean it up either. Closing this required a fresh, full
re-reflection on `TiaPortalProcess` — the original 2026-07-10 API survey
(`docs/notes/openness-api-surface-v20.md`) had recorded only `Attach()`/`Dispose()` on that type,
missing `Id` (the real OS process ID) entirely, and had `TiaPortal.GetCurrentProcess()`'s own return
type wrong too (`TiaPortal`, not the actual `TiaPortalProcess`) — both corrected. Built
`LaunchedInstanceRegistry` (`src/openness-cli/OpennessCli/Openness/LaunchedInstanceRegistry.cs`): a
small JSON file persisting which OS process IDs this tool has itself launched, across separate
invocations, marked at launch and unmarked on success. `OpenProject()`'s search gained a new pass
between "exact match" and "launch yet another fresh instance": a discovered empty process is reused
only if the registry positively confirms this tool marked it itself — never guessed, so the Phase 1
fix isn't reopened. Stale marks (process fully exited) are pruned automatically on read. 4 new unit
tests (`LaunchedInstanceRegistryTests.cs`, pure file/PID logic).

**Live verification found a genuinely interesting complication along the way, not a flaw in the
fix**: killing the client process doesn't always abort an already-issued, in-flight Portal launch —
the underlying OS-level launch (and even a subsequent `Projects.Open()`, if that line had already
been reached) can complete asynchronously regardless of whether the .NET client that started it is
still alive. This made externally timing a kill to land in the exact narrow "marked but not yet
opened" window impractical (sub-second precision needed, tool-call latency exceeds it) — several
attempts either killed too early (before any process existed, no side effect) or too late (the
async completion had already succeeded on its own). **Verified the fix by direct construction
instead**: launched a genuinely empty Portal instance directly (bypassing Openness), marked its
real PID via the registry API (simulating the exact state a real kill-race would produce), then ran
`list` against an unopened project and confirmed: the marked process's memory jumped (project
opened into it, +497MB), no redundant instance was launched, and the registry correctly cleared
afterward. This directly proves the recognize → reuse → unmark cycle works end to end.

**Verdict: the concurrent-session feature itself is not the cause of instability.** Every scenario
deliberately constructed to stress it behaved correctly and predictably. The "sometimes won't
connect at all" symptom correlates with Portal-process accumulation, not with concurrency — both
real occurrences this session happened with stale processes already piled up; a clean process list
was reliable every single time it was tested. Two real, previously-unknown bugs were found and
fixed in the course of proving this (the human-window case, the orphan-accumulation case) — genuine
gaps closed, not evidence the broader design was unsound. **89/89 openness-cli tests pass** (up
from 79 at the start of this work). Committed as `2829ec1`.

### S1 item 26 continued: `FlgNetBuilder` Part-ordering fully fixed — `MotorDOL` now imports into `SampleProject` — 2026-07-14

Picked back up per the project owner's own explicit instruction, resuming exactly from
`RESUME-FlgNetBuilder-Timer-Ordering.md` (deleted now that this is folded in here). Two real bugs
in `FlgNetBuilder`, both root-caused by comparing against a fresh, untouched `MotorDOL` export from
`JOB9002` (re-exported live specifically for this — the earlier raw copy had been deleted per
data-boundary cleanup):

1. **`OrStep`'s own Part was emitted before its branches** (already fixed and confirmed correct
   before the Portal-stability audit interrupted this work) — `BuildStep`'s `OrStep` case now adds
   the `O` Part *after* building its branches, mirroring `NotStep`'s own existing children-then-self
   order. Verified: regenerated Parts order changed from `O(44), O(41), Contact39, Contact40, ...`
   to the correct `Contact39, Contact40, O(41), ...` — upstream before downstream.
2. **The deeper root cause: Timer builds were phase-hoisted, not inline with the production that
   needs them.** `FlgNetBuilder.Build()` used to build *all* Timers in one global phase before *any*
   Assignment/Move/etc. — real TIA export order is instead **fully contiguous per rung**
   (`Contact34-37, Coil38` then `Contact39-46, O(41), O(44), TON(47), RCoil(49)`, back to back), not
   grouped by construct type. Since `RCoil(49)`'s own rung depends on `TON(47)` (built in the
   Timers phase) while `Coil(38)`'s completely unrelated rung sat physically between them (from the
   Assignments phase), TIA's own `Import()` validator rejected the document even though every
   dependency was still technically declared before its dependent — the first network this session
   ever hit with two independent rungs where one uses a `TimerOutputStep`.

**Fix**: Timers are no longer pre-built. A new `EnsureTimerBuilt` helper builds a Timer's own Part
(and upstream IN-chain) the first time any production's `BuildStep` reaches it via a
`TimerOutputStep` — contiguous with whichever production triggers it, matching real TIA order
exactly. A Timer never referenced via `TimerOutputStep` (e.g. `FC ControlDelays`' own `Q` read via
an ordinary `Access`) still falls through to a catch-all pass at the end of `Build()`. Required
threading a new `timersByTonPartUId` lookup through `BuildTimer`/`BuildMove`/`BuildWordAnd`/
`BuildCall`/`BuildEnSource`/`BuildMul`/`BuildConvert`/`BuildSwap`/`BuildOneChain`/`BuildStep` (every
method that could recursively reach a `TimerOutputStep`) — mechanical once the design was settled.
All 254 converter tests still pass (no test asserts Part order, only endpoint sets/counts — this
refactor changes ordering only, not topology).

**Live-verified, 2026-07-14**: regenerated `MotorDOL`'s sanitized XML fresh from the pristine
original export (not from the earlier, still-buggy regeneration, to avoid compounding artifacts).
Confirmed Parts order in the affected network now reads `Contact34, 35, 36, 37, Coil38, Contact39,
40, O(41), Contact42, 43, O(44), Contact45, 46, TON(47), RCoil(49)` — an **exact match** with the
real TIA-original export's own order. Retried the import into `SampleProject`:
**succeeded** — `MotorStarter` (sanitized `MotorDOL`) is now FB2 in `SampleProject`, the blocker
this whole sub-investigation existed to resolve.

**A new, separate, expected finding surfaced on compile, not a regression**: `compile --block
MotorStarter` fails with `"Network 3: Missing instance DB"` / `"Network 8: Missing instance DB"` —
`MotorDOL`'s own `LocalVariable`-scoped timer instances (multi-instance, storage living in the
calling FB/DB's own instance data) have nowhere to resolve to, since it was imported standalone
with no instance DB and no calling FC. This is the same category of gap already documented for
`PlantAutoControl` itself (S1 items 16/17: testing an isolated block without its full calling/dependency
context in `SampleProject` surfaces exactly this kind of missing-context error) — not a converter
bug, not investigated further here.

**Bottom line**: the `FlgNetWriter`/`FlgNetBuilder` Part-ordering gap that blocked `MotorDOL`'s own
import is fully fixed and live-verified. The broader "does a dependency FB compile standalone in
`SampleProject`" question (S1 items 16/17's own original question, revisited via UDT support and
now this) still isn't answered for `MotorDOL` — it's blocked on a *different*, well-understood kind
of gap (missing instance DB / calling context), not a converter defect.

### S1 item 26 continued again: full converter round-trip proof for `MotorStarter`, and a real gap found in `Normalizer` itself — 2026-07-14

Project owner asked for the actual Layer 1 assertion this whole project's testing strategy is built
on (`docs/08-testing-strategy.md`): `export → to-ir → to-xml → import&compile → export → compare`.
`MotorStarter` couldn't do the live half of this — it's currently `INCONSISTENT` in `SampleProject`
(the "missing instance DB" finding just above), and `Export()` refuses any inconsistent block
outright, blocking *both* the first export and the final re-export the same way (re-importing the
regenerated XML would leave it inconsistent again, for the identical reason) — "skip compile" alone
doesn't route around this, since `Export()` itself enforces consistency, not the test procedure.
Confirmed with the project owner via `AskUserQuestion` rather than guessing which path to take;
they chose the pure converter round trip (`to-ir → to-xml → compare`, no live TIA involved),
leaving the instance-DB gap for another day.

**IR-level round trip: byte-identical**, as expected (`to-ir → to-xml → to-ir`, matching this
project's own established pattern for every prior block).

**XML-level comparison via `Normalizer.AreSemanticallyEquivalent`: initially reported `false`** —
investigated rather than dismissed, since the IR round trip's own success made a real XML-level
difference implausible. Isolated the exact cause with a throwaway test (`ZZRoundTripCheck.cs`,
deleted after use) dumping both sides' own `Normalizer.Strip()` output: **100% of the diff was
endpoint order within individual `<Wire>` elements** — the same two electrically-identical wires
listing their `<IdentCon>`/`<NameCon>` children in a different order, nothing else. Confirmed by
filtering the diff for anything *other* than wire-endpoint elements: zero lines.

**Real gap found and fixed, in `tests/golden`'s own `Normalizer.cs`, not the converter.**
`Normalizer` already treats Wire-vs-Wire order (within `<Wires>`) and Access UId numbering as
non-semantic — TIA relocates/renumbers freely, only the topology matters, confirmed real
2026-07-10/11 — but had never extended that same principle one level deeper, to a single wire's
*own* endpoint list. This had simply never been exercised before: every prior `Normalizer`
comparison ran against a real live TIA re-export (single genuine copy each time), never a
converter-only round trip regenerated twice and diffed against itself, on a network with genuine
multi-endpoint wire fan-out (`MotorStarter`'s own Move-tap/OR-merge shapes). Fixed: `Wire` added
to the existing order-independent-child-sort branch, same mechanism already used for `Wires`/
`Parts`. Re-ran: **`AreSemanticallyEquivalent` now returns `true`.** Confirmed the fix doesn't mask
real differences — the existing `AreSemanticallyEquivalent_DifferingWireEndpoint_ReturnsFalse` test
(genuinely different endpoint *sets*, not just order) still correctly returns `false`. All 11
(now 12, then back to 11 once the throwaway test was removed) `GoldenHarness.Tests` pass.

**Bottom line**: `MotorStarter`'s converter-level round trip is now proven lossless end to end,
including the wire-fan-out-heavy content the earlier `MotorDOL`/S1 item 10 grounding first
introduced — this is the first time that content has been checked with the *correct* comparison
tool, not just eyeballed. The live TIA half of the original request (import → compile → re-export)
remains blocked on the separate, already-documented missing-instance-DB gap, not on anything found
in this pass.

### A real `Sanitizer` bug, found by the project owner directly inspecting the sanitized output — 2026-07-14

Project owner noticed, just by reading the sanitized `MotorStarter` XML: timer names were renamed
at their **Interface declaration** (`MotorStarter.GeneralDelayTimer1` → `MotorStarter.GeneralDelayTimer`,
`MotorStarter.FaultTripTimer2` → `MotorStarter.FaultTripTimer`) but the **same timers' own instance
references in the network body** (`<Instance Scope="LocalVariable"><Component Name="GeneralDelayTimer1" />
</Instance>`, used by each `TON` Part) were left completely unsanitized — still the real names.

**Root cause**: `Sanitizer.SanitizeNetwork` only ever sanitized `network.AccessNodes` (ordinary
`<Access>`-based tag references) — it never walked `network.Parts` at all, so a `PartNode`'s own
`Instance` field (an `AccessNode` with the exact same Scope+ComponentPath shape, used by every
`TON`/`TONR`/`TOF` timer instance and every `CALL`'s own FB instance — confirmed by `PartNode`'s
own doc comment) was never reached. This had been invisible until now: every previously-renamed
tag in this map (`RisingEdgeFlags1`→`RisingEdgeFlags`, confirmed correctly applied everywhere,
network body included) happened to be an ordinary Access reference, never a Part's own Instance —
and every other renamed-vs-identity tag in the map's own `Tags` table couldn't reveal the gap
either, since an identity mapping (`"X": "X"`) can't distinguish "sanitized" from "never touched."

**Fixed**: `SanitizeNetwork` now also sanitizes each `Part.Instance` (when present) via the same
tag-lookup helper already used for `AccessNodes`, extracted into a shared `SanitizeAccessNode`
method. Verified as a genuine regression, not just a plausible-sounding theory: a new fixture
(`SanitizeSourceWithTimerInstance.xml`, a `TON` with a `LocalVariable` instance) and test
(`Apply_TimerInstanceReference_IsSanitized`) were confirmed to **fail** against the pre-fix code
(`git stash`-verified) and pass with the fix. **255/255 converter tests pass** (up from 254).

**Bottom line**: a real, previously-undetected sanitization gap — genuinely important given this
tool's whole purpose is guaranteeing nothing unmapped/unsanitized reaches committed or
cross-project output. Found by the project owner's own direct inspection, not by any existing
test or process. `SampleProject`'s own currently-imported `MotorStarter` block was built from the
pre-fix sanitized XML, so it still carries the real `GeneralDelayTimer1`/`FaultTripTimer2` instance names in its
network body until re-imported from a freshly-sanitized copy — not yet redone as of this entry
(the block isn't compiling standalone yet regardless, per the missing-instance-DB gap above, so
this doesn't block anything currently in progress, but is worth closing out before treating
`MotorStarter` as a finished, trustworthy artifact).

### PLC tag table support (`TAGTABLE`), deliberately minimal, live-verified end to end — 2026-07-14

Picked up mid-way through Phase 0.1 grounding for the approved `PlantAutoControl` round-trip plan
(confirming its exact dependency list, per S1 items 16/17's own reopened question). Re-ran the
sanitization-map "collect everything missing" trick (`empty.map.json`, near-empty map) against a
fresh `PlantAutoControl` export: 343 distinct tag paths across 36 distinct top-level roots. Cross-checked
each root against `openness-cli list --json` for `JOB9002`: 26 are genuine DBs (confirmed `"type":
"DB"`), but 10 (`Tag_45`-`Tag_54`) are **absent from the block enumeration entirely**. Their real
XML shape confirmed the reason: a bare single-component `<Access Scope="GlobalVariable"><Symbol>
<Component Name="Tag_45" /></Symbol></Access>` — no `Table.Tag` dotted structure, unlike any DB
member reference. A genuinely new object type: `PlcSoftware.TagTableGroup` had been in the
reflected API survey since S1's early Openness work, marked "unused so far," and never touched.

**An initial hypothesis was wrong and corrected immediately**: `Control`/`Input`/`Output`/`PLC`/
`Timings`/`HMIControlSignals` were first guessed to be tag tables too (generic-sounding names) —
checked live via `list --json` and confirmed all 6 are actually `"type": "DB"`. Only `Tag_45`-`54`
turned out to be real tag-table entries. Corrected course rather than building unneeded generality
for names that were DBs all along — worth recording as a reminder that "looks like a tag table"
isn't grounds enough on its own.

**Grounded live** before writing any parser code (CLAUDE.md hard rule 3 discipline, same as every
other construct in this project): confirmed the real Openness API surface (`PlcTagTable`, `PlcTag`,
`PlcTagTableComposition`, `PlcTagTableGroup` — mirrors `PlcType`/`PlcTypeGroup` almost exactly, root
reachable via `PlcSoftware.TagTableGroup`), then live-exported "Default tag table"
(`station_2/JOB9002_PLC`, 900+ tags) via a newly built `list --tagtables`/`export --tagtable` on
`openness-cli` — **succeeded on the first attempt**. Confirmed the real XML shape against this
export (see `src/converter/README.md`, "PLC tag table support", for the full shape details) —
notably simpler than either a DB or a UDT: no nesting, no `BooleanAttribute`-wrapped attributes, no
table-level Comment/Title. `PlcTag.IsSafety` exists as a reflected C# property but **never appears
in the exported XML at all** (confirmed by grep across the full 900+-tag export) — nothing to
hard-error on for this construct at the XML level.

**Scope decision** (project owner's own explicit call, via `AskUserQuestion`): the real table has
900+ tags but `PlantAutoControl` needs only 10 — build a **minimal synthetic tag table**, not recreate
the full real one. Built the converter-side pipeline (`PlcTagTableModel.cs`,
`PlcTagTableSourceParser.cs`/`PlcTagTableSourceWriter.cs`, `Ir/TagTableIr.cs`,
`Sanitizer.ApplyToTagTable`) and the `openness-cli` side (`EnumerateTagTables`/`ExportTagTable`/
`ImportTagTables`, `list --tagtables`, `export`/`import --tagtable`), each mirroring the existing
`--type`/UDT precedent closely. 7 new converter tests, 7 new `openness-cli` tests — all green on
first run. Explicitly scoped **minimal, not a general tag-table framework** per the project owner's
own instruction to track every intentional gap in a doc rather than build silently — full, dated
list in `src/converter/README.md`'s own "PLC tag table support" section and `ir/SPEC.md`'s "Tag
tables, UDTs, DBs" section (no `PlcConstant`/`PlcSystemConstant`/`PlcUserConstant`, no tag-table
folder/grouping beyond enumeration, `DataTypeName` never sanitized, no `delete`/`compile
--tagtable`, whole-table-only Openness import/export).

**Live-verified, both directions.** `export --tagtable` (above) already proved the export half.
For import: rather than hand-sanitize the full 900+-tag table (disproportionate to what's actually
needed, and Openness only supports whole-table import/export — no partial-extraction path),
converted the real export to IR (readable text) and filtered it down to just the `Tag_45`-`Tag_54`
lines — editing IR text, not raw SimaticML, per CLAUDE.md hard rule 7 — then converted that back to
a minimal 10-tag XML. `import --tagtable` against `SampleProject`'s root tag-table group (device
path `S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0`, resolved the same way `--group` already works
for blocks/types) **succeeded on the first attempt**; `list --tagtables` confirmed "Default tag
table" (10 tags) now present in `SampleProject`. Both halves of the round trip are live-verified
against real TIA Portal, not just unit-tested.

Tag names/addresses used for this live verification (`Tag_45`-`Tag_54`, `%IW64`-`%IW78`/
`%QW64`-`%QW66`) are the real, unmodified `JOB9002` values, not run through `Sanitizer` first — judged
non-identifying (Siemens auto-generated placeholder-style names, no site business content,
same "structural" category `LogicalAddress` itself already sits in), not a data-boundary exception.
Flagged explicitly in `src/converter/README.md`'s own gaps list rather than left silent.

**Bottom line**: a genuinely new Openness/converter object type, grounded, built minimal-by-design,
and live-verified in both directions on the first attempt each time. `SampleProject` now carries
"Default tag table" as real, deliberate Phase 2 groundwork toward `PlantAutoControl`'s own full
dependency closure — not scratch left behind. All three suites green: 262 converter (up from 255),
96 `openness-cli` (up from 94), 11 golden-harness. Committed (`b2fe156`).

### Phase 0.2: `CreateInstanceDB` grounded live — `MotorStarter` now compiles, not just imports — 2026-07-14

Next step of the approved `PlantAutoControl` round-trip plan: ground `PlcBlockComposition.
CreateInstanceDB` before assuming it's the right tool for the "Missing instance DB" gap
(`MotorDOL`/`MotorStarter`'s own `LocalVariable`-scoped `TON` timers are multi-instance — their
storage lives in whichever DB backs a *call* to `MotorStarter`, which doesn't exist since it was
imported standalone with no calling FC). The prior reflected-API survey
(`docs/notes/openness-api-surface-v20.md`) had flagged `CreateFB`/`CreateInstanceDB` as "squarely
S6+ (logic generation) territory" — the plan explicitly called this framing out for re-examination:
creating an instance DB for an *already-existing* FB writes no logic and invents no tag/address/DB
number (CLAUDE.md hard rule 3) — it's project-structure scaffolding, the same category as importing
a real dependency DB, not S6+ generation.

**Confirmed real signature** via `Siemens.Engineering.xml` (TIA Portal V20 `PublicAPI`,
`/PublicAPI/V20/Siemens.Engineering.xml`): `PlcBlockComposition.CreateInstanceDB(string name, bool
isAutoNumbered, int number, string instanceOfName) -> InstanceDB`. Used with `isAutoNumbered:
true` — the DB number itself is always TIA's own choice, never a literal supplied here, so nothing
about calling this invents a DB number.

**Built**: `OpennessGateway.CreateInstanceDb(groupPath, dbName, instanceOfName)` (wraps `FindGroup`
+ `group.Blocks.CreateInstanceDB(...)`, mirrors `ImportBlocks`'s own `SaveProject()`-in-`finally`
discipline), a new `create-instance-db <project> --group <path> --name <name> --instance-of
<FBName>` subcommand (`ArgumentParser`/`Program.cs`, mirroring `delete`'s own parsing shape). 5 new
argument-parser tests — 101/101 `openness-cli` tests (up from 96).

**Live-verified against real data, 2026-07-14.** `create-instance-db SampleProject --group
"S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0" --name MotorStarter_Instance --instance-of
MotorStarter` succeeded on the first attempt (`list` confirmed the new DB present, `"type": "DB"`,
initially `"consistent": false` — expected, clears on compile, not a bug). `compile --block
MotorStarter`: **`STATE: Success, ERRORS: 0, WARNINGS: 0`** — the "Missing instance DB" errors on
Networks 3/8 are gone. A subsequent whole-project `compile SampleProject` (no `--block`) was also
`STATE: Success, ERRORS: 0`.

**Bottom line**: `CreateInstanceDB` is confirmed the right tool, grounded and live-verified in one
pass. This closes the "Tier 1" gap from this session's own "what's left in `PlantAutoControl`" breakdown
— `MotorStarter` (sanitized `MotorDOL`) is now the **first of `PlantAutoControl`'s 8 dependency FBs
proven to round-trip *and compile*** in a target project, not just import cleanly. Directly unblocks
Phase 1 (the same technique applies to each of the other 7 dependency FBs) and informs Phase 3
(`PlantAutoControl`'s own 20 call-site instances will need the same treatment, pending Phase 0.3's
grounding of exactly how `PlantAutoControl` itself represents those call sites).

### Phase 1: `FB EquipmentControlSystem` — a real data-loss bug found and fixed; second dependency FB now compiles clean — 2026-07-14

First of the remaining 7 dependency FBs (Phase 1). Exported fresh from `JOB9002`, sanitized (new
`EquipmentControlSystem.map.json` — this FB shares the exact same title/comment wording and member-naming
convention as `MotorDOL`, right down to identical network titles like `"DOL Motor Start / Stop"`
and identical comment text, strongly suggesting both were built from the same site template;
reused `MotorDOL.map.json`'s own established conventions directly rather than re-deriving them).

**A real converter bug surfaced on first import attempt**: `compile --block
EquipmentControlSystem` failed with 133 errors — `"Interface: A structure without components is
not allowed"` and dozens of `"Tag #Inputs.InHand not defined"`. Root-caused to `EquipmentControlSystem`'s own
`Inputs`/`Outputs : Struct` members — a **third, previously-unseen structured-member shape**
(plain anonymous struct, no UDT type name), genuinely different from both shapes `ir/SPEC.md`
already documented: nested `<Member>` elements are direct children of the owning member (no
`<Sections><Section Name="None">` wrapper), each with its own full `<AttributeList>` — the exact
shape `TYPE`'s own members already use, not the bare `Name`/`Datatype`-only shape a UDT-typed/
system-function-block-instance member's nested members have.

**This was a real bug, not just an unconfirmed shape refused loudly**: `DbInterfaceMembers.
ParseMember` only checked for a `<Sections>` wrapper to detect structured content — with none
present, `Datatype="Struct"` silently fell through to the plain-scalar path and the nested
`<Member>` children were discarded entirely. No error, no warning — `to-ir` reported success with
`Inputs : Struct RETAIN` and zero nested lines. A real, silent data-loss bug, caught only because
the resulting block was actually imported and compiled against real TIA Portal, not by any
pre-existing unit test — the same category of finding as the `Sanitizer` Instance-reference gap
found earlier this session, and further evidence for this project's own "ground every claim
against real TIA behavior, don't trust a clean `to-ir` alone" discipline.

**Fixed**: nested members for this shape are now detected by direct `<Member>` children (not by
`<Sections>` presence) and parsed via `DbInterfaceMembers.ParseTypeMember`/`WriteTypeMember` —
reused directly rather than duplicated, since the shape is identical to a PLC data type's own
member shape. 3 new tests (`Parse_AnonymousStructMember_ReadsNestedMembersWithFullAttributeList`,
`RoundTrip_AnonymousStructMember_ParseWriteParse_IsStable`,
`IrRoundTrip_AnonymousStructMember_SerializeParse_IsStable`), genericized fixture
(`GlobalDbWithAnonymousStructMember.xml`). **265/265 converter tests pass** (up from 262).

**Live-verified, 2026-07-14.** Re-sanitized and re-imported `EquipmentControlSystem` (as
`EquipmentControlSystem`) into `SampleProject`. Confirmed the sanitized XML now carries all 88
nested `<Member>` elements (previously 0 for `Inputs`/`Outputs`). `compile --block
EquipmentControlSystem`: **`STATE: Success, ERRORS: 0, WARNINGS: 0`**, on the first attempt after
the fix — notably, no `create-instance-db` step was needed this time (unlike `MotorStarter`), even
though `EquipmentControlSystem` has the same kind of multi-instance `TON_TIME` timers referenced by `.Q` in
network bodies; not investigated further since the result is a clean pass, not a blocker. A
subsequent whole-project `compile SampleProject` was also `STATE: Success, ERRORS: 0`, and `list`
confirmed both `MotorStarter` and `EquipmentControlSystem` as `"consistent": true`.

**Bottom line**: second of `PlantAutoControl`'s 8 dependency FBs now proven to round-trip *and*
compile. A genuinely new, previously-unseen structured-member shape confirmed real and fixed
before it could silently corrupt any future FB sharing this same "anonymous struct" convention —
plausible this recurs across some of the remaining 6 FBs, given how closely `EquipmentControlSystem` mirrors
`MotorDOL`'s own template.

### Phase 1: `FB ShredderControlSystem` — deeper structured-member bug, external DB/tag-table closure, one hardware-config gap flagged and deliberately not chased — 2026-07-14

Second of the remaining 7 dependency FBs. Unlike `MotorDOL`/`EquipmentControlSystem`, `ShredderControlSystem` pulls
in a much larger external footprint — surfaced by the same empty-sanitization-map trick used
throughout this project: a reference into the real `Control` DB (`ShredderEStopFB`/`Test[7]`) and
**44 separate PLC tag-table entries** (`Tag_1`-`Tag_44`, distinct from the 10 already built for
`PlantAutoControl` itself). Confirmed with the project owner (`AskUserQuestion`) before building this
out live, since it meant pulling a slice of Phase 2's own scope forward for one FB.

**Built the extra dependency closure**: re-exported the real "Default tag table" from `JOB9002`,
combined `Tag_1`-`Tag_54` into one contiguous minimal synthetic table (superseding the earlier
10-tag one — a strict superset, nothing lost) via the same "filter the real export's own IR text"
technique as before. Exported the real `Control` DB (small, ~17 members, whole-DB sanitized rather
than a subset) — **`Control` → `PlantControl`, a real naming mistake caught by the auto-mode
classifier**: the first sanitization map drafted renamed nothing at all (not even the DB's own
name), which the project's own established discipline and `docs/13-data-boundary.md`'s 2026-07-11
clarification both require (nested/generic field names may stay identity, but a DB's own top-level
name always needs an invented replacement) — corrected immediately, no real name reached any
import.

**A second real converter bug, deeper than the first**: `ShredderControlSystem`'s own `ComsOutByte501`
(one of several `Inputs`/`Outputs`-sibling anonymous `Struct` members, same shape as `EquipmentControlSystem`'s
own `Inputs`/`Outputs`) turned out to itself nest **four further `Struct`-typed members**
(`ComsOutByte1`-`ComsOutByte4`), one of which nests a **third** level again — a genuinely
deeply-recursive real shape, not just the one-level case `EquipmentControlSystem` needed. The `ParseTypeMember`/
`WriteTypeMember` reuse from the `EquipmentControlSystem` fix only handled one level; extended both to recurse
to arbitrary depth (a member's own direct `<Member>` children are parsed/written via the same
method, as long as each intermediate level's `Datatype` is `"Struct"`). `Sanitizer.SanitizeMember`'s
own nested-member handling was shallow the same way — fixed via a new recursive
`SanitizeNestedMember` helper so a deeply-nested member's own `StartValue` is sanitized at any
depth, not just one level in.

**A third, independent bug found investigating the first**: the IR *text* format (`to-ir`/`to-xml`)
has its own **separate, independently broken** two-level-only member serialization —
`IrSerializer.cs` (block-level STATIC sections) had a full duplicate of the same fixed-depth logic
`DbIrSerializer.cs` had, and neither was the file first fixed for `EquipmentControlSystem`. Consolidated both
into shared `DbMemberLineFormat.SerializeMemberRecursive`/`ParseMemberRecursive` helpers, used by
`DbIrSerializer`, `DbIrParser`, `IrSerializer`, and `IrParser` alike — one recursive implementation,
not four independently-drifting copies. New tests at both the DB level (`DbConverterTests.cs`,
extended `GlobalDbWithAnonymousStructMember.xml` with a doubly-nested member) and the block level
(`BlockInterfaceTests.cs`, extended `FbWithStaticAndTemp.xml` with a `ComsByte`/`SubByte` pair) —
the block-level test exists specifically because `DbConverterTests` alone would never have caught
`IrSerializer`'s own separate copy of the bug. **267/267 converter tests pass** (up from 265).

**Live-verified, iteratively, 2026-07-14.** Imported the combined 54-tag table, `PlantControl`,
and `ShredderControlSystem` together. First compile: the "structure without components" errors
were gone (deep-nesting fix confirmed working), but two new, real, independent gaps surfaced:
1. `"Block \"PlantControl\" that is accessed has not been compiled"` — resolved by compiling
   `PlantControl` itself first (`compile --block PlantControl`: `STATE: Warning` but 0 errors —
   the warning is the same generic "hardware I/O" one seen elsewhere, not a real problem).
2. `"Tag \"Clock_0\".\"5Hz\" not defined"` — investigated and found to be a **genuine PLC tag**,
   not a Static member (`GlobalVariable`-scoped `Clock_0.5Hz`, address `%M0.7`), one of a
   Siemens-standard "Clock memory byte" set (`Clock_10Hz`/`Clock_5Hz`/.../`Clock_0.5Hz`/
   `Clock_Byte`, all in the real "Default tag table" at `%M0.0`-`%M0.7`/`%MB0`) — added to the
   minimal tag table and re-imported.

**One gap deliberately left open, flagged to the project owner rather than chased further**: even
with the tag present in the tag table (confirmed via re-export — `<Name>Clock_0.5Hz</Name>`,
`<LogicalAddress>%M0.7</LogicalAddress>`, exactly right), `compile --block
ShredderControlSystem` still reports `"Tag \"Clock_0\".\"5Hz\" not defined"` — including after a
clean whole-project `compile SampleProject` (`STATE: Success, ERRORS: 0`, an already-documented
quirk: device-level compile doesn't validate as deeply as block-level, `docs/notes/
openness-quirks.md`) and a bare retry. Root cause, not chased further per the project owner's own
call: `Clock_0.5Hz` isn't an ordinary tag someone typed into a table — it's the auto-generated tag
of the CPU's own "Clock memory byte" **hardware configuration feature**. A plain `PlcTag` imported
with the same name/address doesn't carry whatever internal registration TIA's compiler expects for
the real system-clock tag; the actual fix would be enabling "Clock memory byte" on `SampleProject`'s
own CPU device — a device hardware-configuration change, a capability category this project's
tooling has never touched and CLAUDE.md hard rule 6 explicitly says not to add without being asked.
Presented to the project owner via `AskUserQuestion`; chosen: stop here, accept
`ShredderControlSystem` as done except for this one tag, move on.

**Bottom line**: `ShredderControlSystem` imports and compiles cleanly except for one flagged,
out-of-scope hardware-configuration dependency — everything else (its own logic, the `Control`/
`PlantControl` DB reference, all 54 needed tags) is proven. Two more real, previously-unseen
converter bugs found and fixed (deep anonymous-struct recursion; the IR-text format's own separate,
independently-broken duplicate of the same bug) — both fixed at the shared-helper level so they
can't recur independently again. Third of `PlantAutoControl`'s 8 dependency FBs attempted this session,
and the first to surface a genuine, deliberately-deferred hardware-configuration boundary rather
than a pure data/converter gap.

### Phase 1 continued: `FilterUnitSystem`/`AirStar` compile clean, `Gt` + a fourth Input/Output shape added, two FBs blocked on real gaps — 2026-07-14

Continued Phase 1 at an explicitly usage-limit-conscious pace, per the project owner's own
instruction. `FilterUnitSystem` (as `FilterUnitSystem`) compiled clean on the first attempt — fully
self-contained, reusing the already-imported `TypeDOL`/`MotorIOSet` UDT, no new converter gap.

`AirStar` (as `AirStarSystem`) needed 5 more tag-table entries (`AirStarWord0IN`/`2IN`/`0OUT`/
`2OUT`, `FirstScan` — all real `GlobalVariable`-scoped tags from the "Default tag table", added to
the combined minimal table) and reused the already-imported `PlantControl.Test` reference (no new
DB needed) — compiled clean once those were added. First real block-level `TITLE` seen since
`MotorVSDSystem` ("VSD Motor") — `Sanitizer`'s existing `Titles` map field (distinct from
`NetworkTitles`) handled it with no code change needed.

**Two more real converter gaps found and fixed, both small and mechanical**, grounding `MotorVSDSystem`'s
own dependency closure (`FC Scale`, a small project utility FC wrapping a scale/rescale
calculation):
- **`Gt` (greater-than) comparison** — confirmed real, identical shape to the already-supported
  `Eq`/`Ge`/`Lt`/`Ne` family (same `SrcType` TemplateValue, same `pre`/`in1`/`in2`/`out` ports).
  Added to `FlgNetParser.SupportedPartNames`/`SupportedComparisonPartNames` and `GraphReducer`'s
  `OutPortFor`/`ComparisonOperator`/comparison-chain check. 4 new tests
  (`Parse_GtFeedsCoil_ProducesGtPartWithSrcType` and siblings, mirroring the existing `Ne` tests
  exactly), new fixture `GtFeedsCoil.xml`.
- **A fourth, genuinely minimal Input/Output/InOut member shape** — `FC Scale`'s own `Input`/
  `Output` params have **no `Remanence` attribute at all** (not merely an unrecognized value) and
  **no `<AttributeList>`** — just `Name`/`Datatype`[/`Accessibility="Public"`]. Previously a hard
  error (`"has unrecognized Remanence ''"` — a missing attribute reads back as `null`, printed as
  an empty string via string interpolation). Genuinely distinct from every shape already modeled:
  not `TomraControlSystem`'s ordinary Input/Output shape (has both `Remanence` and `AttributeList`, just
  missing `SetPoint`), not `ParseBareMember`'s shape (forbids `Accessibility`), not
  `ParseConstantMember`'s shape (requires a `StartValue`). Fixed via a new `DbMember.IsBareParameter`
  flag — needed only so the writer can regenerate the same bare shape on write, since
  `Retain`/`SetPoint` alone can't distinguish "genuinely bare" from "an ordinary member that
  happens to have both false". 1 new test (fixture `FcWithBareParameterMembers.xml`).
  **272/272 converter tests pass** (up from 267).

**Two FBs blocked on real, clearly-scoped, deliberately-deferred gaps — flagged rather than chased,
per the project owner's own explicit usage-limit-conscious pace:**

- **`MotorFwdRevSystem`** (as `MotorFwdRevSystem`): needed a new UDT (`MotorFwdRevIOSet1` →
  `MotorFwdRevIOSet`, built and imported successfully, same pattern as `TypeDOL`). Compile still
  fails: Network 1 ("Reverse Pause and Control") uses `CycleDelayReset`, a **standalone named `TON`
  instance** (`GlobalVariable`-scoped, `instancepath = CycleDelayReset` in the IR) — genuinely
  distinct from the FB's own multi-instance Static timers (which `create-instance-db
  --instance-of MotorFwdRevSystem` already resolved fine, `DB3`). Tried `create-instance-db --name
  CycleDelayReset --instance-of TON` — fails: `"Block 'TON' does not exist at the object with UID
  ''"` — `PlcBlockComposition.Create` only resolves user-created FBs by name, not built-in system
  instructions (TIA normally creates this kind of instance DB automatically when a "Single
  Instance" `TON` box is placed in the UI, not via this API). Not the same case as `PlantAutoControl`'s
  own named instance DBs either (those instantiate real user FBs like `MotorDOL`, not raw system
  timers) — genuinely new, needs either a different Openness API or a different approach entirely.
- **`MotorVSDSystem`** (as `MotorVSDSystem`): needed a new UDT (`TypeVSD` → `MotorVSDIOSet`, built and
  imported successfully) and calls `FC Scale` (grounded above, both real gaps it surfaced now
  fixed). Compile still fails: `Scale`'s own internal logic also uses `Sub` (subtraction) — a
  genuinely unsupported arithmetic instruction, not yet built (`Add`/`Mul` support was itself a
  non-trivial addition, S1 item 18 — `Sub` would need the same scope: `SrcType` handling, ENO-chain
  support). Bigger than what remaining session budget allowed; flagged rather than started.
  `create-instance-db --instance-of MotorVSDSystem` was already confirmed working for this FB's own
  multi-instance timers (`DB4`) — that half of the compile is not the blocker.

**Bottom line**: 6 of `PlantAutoControl`'s 8 dependency FBs now proven to round-trip *and* compile
(`MotorStarter`, `EquipmentControlSystem`, `ShredderControlSystem` bar one hardware-config tag,
`FilterUnitSystem`, `AirStarSystem`) — plus 2 new UDTs (`MotorFwdRevIOSet1`, `TypeVSD`) and 2 new
converter capabilities (`Gt`, bare-parameter members) landed along the way. 2 FBs
(`MotorFwdRevSystem`/`MotorVSDSystem`) remain blocked on real, well-understood, narrowly-scoped gaps —
documented precisely enough here to resume directly without re-deriving anything. Sanitization maps
for every attempted FB (`FilterUnitSystem`/`AirStar`/`MotorFwdRevSystem`/`MotorFwdRevIOSet1`/`MotorVSDSystem`/
`TypeVSD`) are built and kept in `sanitization/` (gitignored) for that resume.

### Phase 1 continued: `Sub`/`Div`/`Le` added, `TomraControlSystem` + `MotorVSDSystem`'s own `Scale` dependency both compile clean — 7 of 8 dependency FBs now proven, 2026-07-13

Closed `MotorVSDSystem`'s blocker from above (`Sub`, unsupported arithmetic) and `TomraControlSystem`
(not yet attempted). Grounded `Sub`/`Div` directly against `FC Scale`'s own real export before
writing any code: both are structurally distinct from `Mul`/`Add` (S1 item 18) in a way not
previously modeled — **no `<TemplateValue Name="Card">` element at all**, ever (always binary,
`in1`/`in2` only, no chaining), whereas `Mul`/`Add` always carry one (`Card="2"` in every real
instance). Modeled by making `PartNode.Cardinality` an `int?`, left `null` for `Sub`/`Div`
specifically (defaulted to `2` only inside `GraphReducer`'s own reduction step), and never
regenerating a `Card` element for these two kinds on write. Also confirmed real: a `Sub`'s own
`eno` chaining into a following `Div`'s own `en` (`GraphReducer.ResolveEnSource`'s producer check
extended to recognize `Sub`/`Div` — deliberately **not** `Add`, which stays unconfirmed as a
producer, matching this project's existing grounding discipline). `Le` (less-or-equal) followed
immediately, same shape as `Gt`/`Eq`/`Ge`/`Lt`/`Ne` — completes the full IEC comparison family,
none left unconfirmed. A real `FlgNetWriter` bug was caught by a round-trip test failure along the
way: the `DisabledENO="true"`-writing condition listed `Mul`/`Add`/`Convert`/`Swap` but not
`Sub`/`Div`. 283/283 converter tests pass (up from 272).

With `Sub`/`Div`/`Le` landed, `FC Scale` (sanitized as `AnalogScale`) and `TomraControlSystem` (as
`TomraControlSystem`) both compiled clean standalone — `TomraControlSystem` is the 6th of 8
dependency FBs proven, `AnalogScale` isn't itself one of the 8 but is `MotorVSDSystem`'s own dependency.

**`MotorVSDSystem` itself then hit a second, completely separate real bug** — after `AnalogScale`
compiled clean on its own, `MotorVSDSystem`'s own compile still failed with the exact same two
errors as before Scale was fixed: `"The referenced block \"Scale\" no longer exists"` and
`"Tag #FaultTripTimer2.Q not defined"`. Root cause, found by inspecting `MotorVSDSystem.sanitized.xml` directly:
`Sanitizer.SanitizeNetwork` renamed a Call Part's own `Instance` (the callee's instance-DB
reference) but never its `BlockName` (the callee's own name) — a completely separate field on the
same `PartNode`, exactly the same shape of gap as the earlier "Part.Instance never sanitized" bug
(S1 item 7 Phase A/B era) but for a different field. The sanitized `MotorVSDSystem` block still
said `<CallInfo Name="Scale">` even though `Scale` was independently sanitized/imported as
`AnalogScale` — compiled clean in isolation, but its caller's own reference never got updated to
match. Fixed by renaming `BlockName` via `map.Names` in `Sanitizer.SanitizeNetwork`, same treatment
as `Instance`. Two new tests (`Apply_CallBlockName_IsSanitizedViaNamesMap`,
`Apply_CallBlockName_MissingMapping_HardErrors`), new fixture `SanitizeSourceWithCall.xml`.

The `FaultTripTimer2.Q` half of the error was a separate, non-converter bug: `MotorVSDSystem.map.json` itself
was internally inconsistent — it renamed the static member `FaultTripTimer2` → `FaultTripTimer` but kept
an identity mapping for the bare network-body reference `FaultTripTimer2.Q` (should have followed the
same rename to `FaultTripTimer.Q`). Both map entries fixed by hand; every other timer in the same
map was checked and found consistent (base name unchanged in each other case, so the identity
mapping was correct there).

With both fixes applied, re-sanitized and re-imported `MotorVSDSystem` → `MotorVSDSystem` compiled clean
(`STATE: Warning, ERRORS: 0`, the same expected hardware-config warning every other FB shows). **7
of `PlantAutoControl`'s 8 dependency FBs now proven.** Only `MotorFwdRevSystem`'s `CycleDelayReset`
(standalone named `TON` instance, no Openness API path found yet) remains blocked.

### Phase 1 closed: `MotorFwdRevSystem` fixed at the source, all 8 dependency FBs compile clean — 2026-07-13

The `CycleDelayReset` blocker (a standalone single-instance `TON`, invisible to the whole
`SW.Blocks` object model — no `create-instance-db` path, not listed, not exportable by name; see
above) was confirmed exhausted from the Openness side. The project owner then fixed the actual
root cause directly in `JOB9002`: converted `CycleDelayReset` from a standalone `GlobalVariable`-scope
`TON` into a proper multi-instance `Static`-section timer within `MotorFwdRevSystem` itself — the same
shape every other working timer in this FB already uses. Re-exporting confirmed the fix: `TON`
`UId=85`'s own `Instance` is now `Scope="LocalVariable"`, naming an ordinary top-level `TON_TIME`
Static member, structurally identical to `InHandReqPreStart`/`HrTotaliserTimer`.

One small map gap surfaced and fixed: `MotorFwdRevSystem.map.json` was missing the owner-prefixed
`MotorFwdRevSystem.CycleDelayReset` → `MotorFwdRevSystem.CycleDelayReset` Tags entry (present for
every sibling timer, missed for this one since it didn't exist as a normal Static member until the
fix). Re-sanitized, re-imported, compiled: **`MotorFwdRevSystem` — `STATE: Warning, ERRORS: 0`.**

**All 8 of `PlantAutoControl`'s dependency FBs now compile clean.** Phase 1 is complete.

### Phase 2: `PlantAutoControl`'s own tag/DB dependency closure — 2026-07-14

Built the exact, current dependency list via `Sanitizer.Apply` against an empty map (collects every
missing entry in one pass, per its own design) rather than trusting the earlier "~323 paths / 36
roots" estimate: **523 missing entries, 36 distinct roots (26 real DB/tag-table roots + 10 bare
`Tag_45`-`Tag_54` default-tag-table entries), 343 unique tag paths.** Cross-referencing the 26
DB/tag-table roots against `PlantAutoControl`'s own real device (`--device JOB9002_PLC`) confirmed all of
them concrete, exportable blocks: **20 are Instance DBs of the 8 already-proven dependency FBs**
(9× `MotorDOL`, 3× `MotorVSDSystem`, 3× `FilterUnitSystem`, 1× each `AirStar`/`EquipmentControlSystem`/`ShredderControlSystem`/
`TomraControlSystem`/`MotorFwdRevSystem` — exactly `PlantAutoControl`'s own 20 call sites, fully accounted for),
**6 are ordinary GlobalDBs/tag tables** (`Control`, `HMIControlSignals`, `Input`, `Output`, `PLC`,
`Timings`).

Bulk-exported all 26 from `JOB9002` (one hit the known `IsConsistent`-blocks-export quirk —
`MotorFwdRevInst1` — cleared with a block-level compile in `JOB9002` itself, same established fix). Built
sanitization maps for all 26 programmatically (a PowerShell generator reusing each of the 8 FBs'
own already-established top-level-field → invented-suffix tables, re-keyed per real instance name)
rather than hand-authoring hundreds of near-duplicate entries — 19 of 20 instance DBs sanitized
clean on the first pass.

**One genuinely new converter gap found: `TomraControlInst1` has a non-empty `Input`/`Output`
Interface section.** Every Instance DB grounded before this one happened to have only `Static`
content; `TomraControlSystem` itself has real Input/Output formal parameters (`inputWord0`-`7`,
`OutputWord0`-`1` — S1 item 20 already grounded these on the *block* side, but never on the
*Instance DB* side). `DbSourceParser` hard-errored exactly as designed rather than silently
dropping them. Fixed properly, not worked around: extended `DbSource` with
`InputMembers`/`OutputMembers`/`InOutMembers` (mirroring `BlockSource`'s own fields exactly),
`DbSourceParser`/`DbSourceWriter` to parse/write them (reusing
`DbInterfaceMembers.ParseMember`/`WriteMember` with `requireSetPoint: false`, same as
`BlockSourceParser`'s own Input/Output/InOut handling), `Sanitizer.ApplyToDb` to sanitize them, and
`DbIrSerializer`/`DbIrParser` (`INPUT`/`OUTPUT`/`INOUT` IR sections, same null-vs-empty convention
as `BlockSource`'s own) — closing the loop so `to-ir`/`to-xml` doesn't silently drop this content
either. New fixture + 4 new tests (parse, round-trip, IR round-trip, sanitize). All 26 DBs then
sanitized clean; 287/287 converter tests pass (up from 283).

Batch-imported all 26 sanitized DBs into `SampleProject`, then block-level compiled all 26 —
**every one: `STATE: Warning, ERRORS: 0`** (only the same expected hardware-config warning every
other block shows). `PlantAutoControl`'s complete tag/DB dependency closure is now proven.

### Phase 3: `PlantAutoControl` itself — full round trip proven, 2026-07-14

Built `PlantAutoControl`'s own sanitization map (343 Tags entries, 35 Names entries covering itself + 8
FB names + 26 DB names, 20 NetworkTitles) programmatically, driving it directly off the same
missing-entries list Phase 2 used as its own source of truth — guarantees complete coverage rather
than risking a hand-built map missing something. Every one of `PlantAutoControl`'s 20 real call-site
instance DB names and 8 FB call-site names reuses the exact invented names already established in
Phase 1/2's own per-block maps, so `Sanitizer`'s `BlockName`/`Instance` rename (the same fix that
unblocked `MotorVSDSystem` earlier) resolves every one of `PlantAutoControl`'s own 20 `<Call>` sites
correctly. Sanitize succeeded with zero missing-entry warnings on the first attempt.

**Import → block-level compile → `STATE: Warning, ERRORS: 0` on the first attempt** — no new gaps,
confirming Phases 1/2's dependency closure was genuinely complete. Re-exported and ran
`Normalizer.AreSemanticallyEquivalent` (a throwaway test, deleted immediately after use, same
discipline as every prior live verification) against the sanitized-then-imported XML vs. the fresh
re-export: **true.**

**This is the actual Layer 1 assertion (`docs/08-testing-strategy.md`) this entire multi-session
effort existed to prove, now demonstrated end-to-end for `PlantAutoControl` itself** — the real site
master-control block, its complete real dependency closure (8 FBs + 26 DBs/tag-tables), imported
into an independent TIA project, compiling clean, and round-tripping losslessly. All three PC-side
suites green throughout: 287 converter, 101 openness-cli, 11 golden-harness tests.

**`MotorFwdRevSystem`'s blocker was a real gap in the source data, not the tooling — confirmed
exhausted, then resolved by the project owner directly, same day.** Before hearing back from the
project owner, the Openness-side investigation was pushed to its actual limit: `create-instance-db
--instance-of TON`/`TON_TIME` both refuse (`PlcBlockComposition.Create` only resolves user-created
FBs by name); reflection on `PlcBlockComposition` shows no alternate overload accepting a system
block reference; and the real standalone instance in `JOB9002` (`FC ControlDelays`'s own
`GeneralEnableDelay`) doesn't even appear in `list`'s output or resolve via `export --block` by
name — a standalone single-instance system-function-block instance (as opposed to an FB's own
multi-instance Static-section timer, which works fine everywhere else) is invisible to the whole
`SW.Blocks` object model, not merely uncreatable. Confirmed via a fresh `list`/`export` against
`JOB9002` that this data-boundary-safe finding holds (no committed content changed). Also confirmed,
via a fresh `PlantAutoControl` export, that `MotorFwdRevSystem` genuinely is one of `PlantAutoControl`'s own 20
call sites (not an optional/skippable dependency) — ruling out simply deferring it.

**Resolved same day: the project owner fixed the root cause directly in `JOB9002`** — the real
`CycleDelayReset` was a standalone single-instance `TON` at `GlobalVariable` scope (exactly the
invisible-to-Openness shape above); converting it to a proper multi-instance `Static`-section timer
in the FB itself (the same working pattern as `MotorDOL`'s own timers) sidesteps the gap entirely,
since multi-instance Static timers already round-trip and compile cleanly. Re-exporting after the
fix confirmed `CycleDelayReset` now declares as an ordinary top-level `TON_TIME` Static member with
a `LocalVariable`-scope `Instance` — structurally identical to every other working timer in this FB
(`InHandReqPreStart`, `HrTotaliserTimer`, etc.). One small map gap surfaced and fixed along the way:
`MotorFwdRevSystem.map.json` was missing the `MotorFwdRevSystem.CycleDelayReset` →
`MotorFwdRevSystem.CycleDelayReset` owner-prefixed Tags entry (present for every sibling timer,
missed for this one since it didn't exist as a normal Static member until the fix). Re-sanitized,
re-imported, compiled: **`MotorFwdRevSystem` — `STATE: Warning, ERRORS: 0`.**

**All 8 of `PlantAutoControl`'s dependency FBs now compile clean.** Phase 1 is complete.

### Full-cycle verification pass: every block in `SampleProject`, one by one — 2026-07-14

Project owner's own explicit ask: run the *complete* export → `to-ir` → `to-xml` → import →
compile → re-export cycle (not just sanitize+import, the path every phase above actually used)
against **every block already sitting in `SampleProject`** — 49 blocks, the full accumulated
history of this project's own work plus the pre-existing reference/`TimerSample` corpus — one at a
time, fixing whatever real issues surfaced. This is a materially stronger test than anything run
before it: it's the first time the converter's own `to-ir`/`to-xml` round trip (not just
`sanitize`, which operates on the parsed XML model directly, bypassing the IR text layer entirely)
was exercised against this much real, structurally-varied content in one pass.

**One pre-existing broken artifact found and removed, not fixed:** `MotorStarter_Instance` (DB0),
leftover scaffolding from the very first `create-instance-db` spike, was stuck with an invalid DB
number (`0`) — not repairable via any exposed Openness API (no way to directly set a DB's own
`Number`), and fully superseded by the real `MotorStarterInst1`-`9` DBs from Phase 2. Deleted with
the project owner's explicit confirmation.

**Five real, previously-unknown converter bugs found and fixed**, each caught only because this
pass ran the full cycle against real, already-imported content rather than synthetic fixtures:

1. **`Sanitizer.SanitizeAccessNode` corrupted any real tag whose own name contains a literal `.`**
   (`FB ShredderControlSystem`'s own reference to the genuine Siemens system tag `Clock_0.5Hz` — real
   XML: one `<Component Name="Clock_0.5Hz" />`, not two). `sanitizedPath.Split('.')` assumed every
   `.` in the invented value is a component-boundary separator, splitting an identity-mapped
   `Clock_0.5Hz` into two bogus components (`Clock_0`, `5Hz`) — TIA then reported the tag as
   undefined. Fixed by splitting into exactly `access.ComponentPath.Count` pieces (the real,
   trusted component count) instead of an unlimited split.
2. **The exact same class of bug, independently, in `AccessNode.FromDottedPath`** — the IR text
   round-trip path (no `Sanitizer` involved at all) hit the identical `Clock_0.5Hz` corruption via
   its own naive `path.Split('.')`. Since a flat IR-text tag string carries no side-channel
   component count the way `Sanitizer` had, fixed by recognizing Siemens's own fixed set of 8
   "Clock memory byte" system tag names (`Clock_10Hz` … `Clock_0.1Hz`) as atomic before the general
   splitting fallback — grounded directly against the real export, not a general escaping scheme
   guessed at for a case that's only ever been seen once.
3. **A whole class of "wrong document order" bugs in `FlgNetBuilder.Build`**, found via two
   distinct real networks: `FB MotorStarter`'s "HMI Times" network interleaves three independent
   `Mul → Convert` chains (`Mul, Convert, Mul, Convert, Mul, Convert`) but came out grouped by kind
   (each production kind has its own dedicated build loop); `FB AirStarSystem`'s own network has
   four independent Coil-assignment chains laid out with their contacts spatially interleaved in
   the real ladder diagram, which also came out grouped by chain instead. TIA's own Import()
   validator rejects both ("The elements must be sorted according to the current flow"). Rather
   than special-case each pair of kinds as its own gap (the first attempt, a Mul/Convert-specific
   merge-sort, was superseded once the second, structurally-different case showed the problem was
   general), fixed with one general mechanism: sort the fully-built `parts` list by `UId` — a
   confirmed-real, trustworthy proxy for true document position (same reasoning `EnsureTimerBuilt`
   already relied on) — regardless of which loop built each Part.
4. **The same general fix, extended to wire endpoints**: `FB AirStarSystem`'s own rail wire, shared
   by more than two consumers, hit a second, related TIA validation ("The parts in the parts list
   and the connections in the power rail ... with more than two I/Os must be located in the same
   sequence") — a wire's own endpoint list also needs real document order, not just its Parts.
   Endpoints were appended in whichever order each production's own loop happened to reach the
   shared wire. Fixed by sorting each wire's own endpoint list by `UId` too (a `null` UId —
   Powerrail itself — sorting first, the natural "source before consumers" position).
5. **`IsBareParameter` (S1 item 20's `FC Scale`/`AnalogScale` fix) was never carried by the IR text
   format** — `DbMemberLineFormat` (shared by a DB's own `MEMBERS` section and a block's own
   `STATIC`) had no marker for it at all, so a bare parameter crossing the `to-ir`/`to-xml`
   boundary silently reverted to the ordinary member shape, and TIA's own Import() then refused the
   resulting (wrong) `Remanence` attribute outright. Fixed with a new ` BAREPARAM` line-format
   marker, peeled/appended in the same fixed-suffix-order convention as `VERSION`/`RETAIN`/
   `SETPOINT`.

**`OB1 Main` — two more real, OB-specific gaps found and fixed, one left open:**
- **`SecondaryType`** (`<SecondaryType>ProgramCycle</SecondaryType>`, required by Openness's own
  `Create()` for an OB specifically — "The argument 'SecondaryType' is missing" otherwise) was
  never modeled at all (parsing silently ignored it, matching how parsing has always been more
  tolerant than writing). Added as a nullable field threaded through `BlockSource`/`IrBlock`/the IR
  text format (a new optional `SECONDARYTYPE <value>` line) end to end.
- **`Informative`/`InformativeComment`**: an OB's own system-defined Input parameters
  (`Initial_Call`/`Remanence`) carry `Informative="true"` plus a
  `<Comment><MultiLanguageText Lang="en-US">...</MultiLanguageText></Comment>` child — TIA's own
  Import() requires this specifically for OB system parameters ("OB system parameters must be
  informative"). Added as two new fields on the existing bare-parameter shape (`IsBareParameter`),
  with matching IR-text (`INFORMATIVE "text"`) and XML support.
- **Left open, recommended to defer**: after both fixes, `Main` hit a third, narrower issue —
  "Section 'Output' is not valid for this block" (an OB's own required Interface section set
  apparently excludes `Output`/`InOut` entirely when unused, unlike FC/FB where a present-but-empty
  section is always valid). `Main` (OB1) is TIA's own auto-generated system placeholder block, not
  restricted content, and OB support was never a stated project goal (S1's own scope has always been
  FC/FB/DB/UDT/tag-table) — each fix so far has revealed another narrow, OB-specific quirk with no
  sign the tail is short. Recommended as a deliberately out-of-scope, deferred item rather than
  continuing to chase it.

**A genuinely new TIA behavior confirmed, exposing a real gap in the verification tooling itself
(not the converter):** running the actual `Normalizer.AreSemanticallyEquivalent` check (not just
"compiles with 0 errors") across all 47 successfully-round-tripped blocks found 40 pass outright;
the other 7 (`AirStarSystem`, `AnalogScale`, `EquipmentControlSystem`, `FilterUnitSystem`,
`MotorFwdRevSystem`, `MotorStarter`, `MotorVSDSystem`) report "not semantically equivalent" —
but spot-checking one (`MotorStarter`) directly confirmed the *only* difference is that TIA
reassigned every Part's own `UId` on import/compile (155 Parts, identical count and kind
distribution before and after — Contact/Coil/O/Mul/Convert/etc. all match exactly). This is the
same class of already-known-and-accepted behavior `Normalizer.cs` already handles for `Wire` and
`Access` UId ("TIA relocates/renumbers freely, only the topology matters") — just never previously
observed to apply to `Part` UId too, likely because no prior comparison exercised a block whose
own content had gone through this many import/compile cycles in one session (a block with `Part`
UId that happens to already match what TIA would assign on its own — confirmed for
`PlantAutoControl`, zero Part UId drift — never triggers the reassignment at all). **Deliberately
not fixed now**: unlike `Access` (content-addressable by its own `Symbol`/tag path, safely
collapsible to a stable key), a bare `Contact`/`Coil` Part has no distinguishing content of its own
— many identical-looking Parts coexist in one network — so a correct fix needs real graph-based
identity matching (matching Parts by their own wiring relationships, not content), not a simple
content-key map the way `Access` got. A real, well-scoped follow-on for `tests/golden/Normalizer`,
not a converter correctness issue — every one of these 7 blocks compiles with `ERRORS: 0`.

**Bottom line: 47 of 48 blocks in `SampleProject` (every block except `Main`/OB1, deferred as
out-of-scope) now round-trip through the complete, true cycle — export → convert → import →
compile clean → re-export — including `PlantAutoControl` (`PlantAutoControl` itself) with a byte-level
`Normalizer` equivalence pass, not just a compile-clean check.** 5 real converter bugs found and
fixed along the way, all with regression tests. 291 converter tests (up from 283), 101
openness-cli, 11 golden-harness — all green.

### Instruction-coverage sweep of the full `JOB9002` inventory, and Phase 2 tiers 1–3: `Abs`/`LIMIT`/`T_SUB`/`T_CONV`/`Calc` built, 2026-07-14

(Not to be confused with the `PlantAutoControl` round-trip plan's own "Phase 1/2/3" above — this is a
separate, later plan, the project owner's own explicit follow-on request after the full-cycle
verification pass: "examine what instructions we are missing... lay out what's needed for phase
2.")

**Grounding pass**: exported every remaining block across *both* `JOB9002` PLC stations (`JOB9002_PLC`
and `JOB9001_PLC` — confirmed the same approved site per `docs/13-data-boundary.md`, not a second
site) not already swept by the `PlantAutoControl` dependency work — 36 blocks, one export per
block per the project owner's own explicit instruction ("run it one block at a time"), the
`IsConsistent` quirk hit repeatedly and cleared the same way as every prior session (block-level
`compile`). Tallying every `<Part Name="...">` across all ~49 blocks now grounded found 11
real, currently-unsupported instructions: `MOVE_BLK_VARIANT`, `LIMIT`, `Calc`, `T_SUB`, `T_CONV`,
`WAIT`, `Modbus_Master`, `Modbus_Comm_Load`, `Jump`, `FillBlockI`, `Abs` — none guessed, all with a
real `UId` in a real block. Ranked by confidence/effort (near-identical-to-existing-code first,
genuinely novel categories last) into a 6-tier plan; the project owner asked to start on it
overnight.

**Tier 1 (`Abs`/`LIMIT`) and Tier 3 (`Calc`) built and live-verified** — see `ir/SPEC.md`'s own
Network-body history for the full per-instruction shape/grammar detail. Headline findings:
- `PartNode.TonVersion` renamed to `Version` — `LIMIT` (and, looking ahead, `Modbus_Master`/
  `Modbus_Comm_Load`) needed the same bare `Version="N.N"` attribute TON already carried, so the
  field was generalized rather than growing a parallel `LimitVersion` field, the same "don't stack
  special cases" call already made once this session for the Part/wire-endpoint sort.
- **`LIMIT`'s `DisabledENO` was misread from the raw export** — the parser was first built
  requiring its *absence* (a clean `dotnet test` pass the whole time, since the hand-built fixture
  was wrong in exactly the same way). Only surfaced when the live TIA import rejected the
  regenerated XML outright: "ENO cannot be deactivated for the 'LIMIT' instruction." Fixed by
  re-reading the raw export directly rather than trusting the earlier transcription — the real
  shape does carry `DisabledENO="true"`, same as `Convert`/`Swap`/`Abs`/`Mul`/`Sub`/`Div`.
- A **`Calc` sanitize attempt on the real `VSDSim`** hit a separate, pre-existing Sanitizer gap
  unrelated to tonight's instruction work: a Static member (`SpeedCalcArray`) with
  `ExternalAccessible="False"` — already a deliberate, tested hard-error case
  (`GlobalDbWithNonDefaultAttribute.xml`), now genuinely grounded as real for the first time.
  **Not fixed tonight** — flagged as its own well-scoped follow-on (would need a new `DbMember`
  field, `DbInterfaceMembers` parse/write changes, and a new IR-text marker, the same shape as the
  existing `IsBareParameter`/`Informative` additions) rather than scope-creeping into it mid-plan.
  Live verification instead used a synthetic FC composed from the already-proven `Abs`/`LIMIT`/
  `Calc` fixture networks (avoiding the unrelated gap entirely) — imported into `SampleProject`,
  compiled clean (0 errors), re-exported, confirmed byte-identical readable IR text before and
  after the full cycle. Deleted from `SampleProject` afterward (synthetic scaffolding, not
  reference content worth keeping there).

**Tier 2 (`T_SUB`/`T_CONV`) built and live-verified.** Time-arithmetic variants of `Sub`/`Convert`
(`FB VibratorCycle`, a real `T_SUB -> T_CONV -> Convert` ENO chain) — extended `ResolveEnSource`'s
existing Mul/Convert/Sub/Div allowlist to include both as valid ENO-chain sources. **A second real
finding, also only caught live**: the hand-built fixture reused one `<Access>` UId (`StartTime`)
across two different `<Wire>` elements (one feeding `T_SUB.IN1`, one feeding `T_CONV.IN`) — TIA's
`Import()` rejected it, "the connection ... is used multiple times at the cables." Direct
inspection of the real export confirmed why: TIA itself never dedupes Access-by-tag-name within a
network — `VibratorCycleTimer.PT`/`.ET`, `Remainder`, `UDintTime`, and `time` are each declared
*twice*, once per wire reference, with two different UIds. Fixed by declaring a second `Access`
element for the fixture's own second reference, matching the real shape. Same live-verification
approach as Tier 1/3 (synthetic composed FC, imported/compiled clean/re-exported/byte-identical,
then deleted from `SampleProject`).

**40 new converter tests** across `AbsTests.cs`/`LimitTests.cs`/`TSubTConvTests.cs`/`CalcTests.cs`
— 323 converter tests total, all green.

**Tier 5 (`MOVE_BLK_VARIANT`) also attempted the same night, with a different outcome.**
Re-grounding it properly (per the `T_SUB`/`T_CONV` lesson just above — read the *full* wire list,
don't trust a `head_limit`-truncated `grep`) found the earlier "only `en` wired" conclusion was
simply wrong: all 4 real instances (`FC MoveData`/`FC VSDDataSequence`) are fully wired and,
unlike `Modbus_Master`/`Modbus_Comm_Load`, genuinely simple — four plain-tag inputs, no chains, no
`OpenCon`-optional ports. Built and unit-tested the same way as tiers 1–3 (8 new tests,
`MoveBlkVariantTests.cs`, 331 converter tests total) — the first instruction this converter
reduces with **two** destination writes (`Ret_Val`/`DEST`) instead of one, needing genuinely new
(if simple) infrastructure. **Live TIA verification did not complete**: two consecutive
`openness-cli import` attempts against a synthetic composed FC both hit the first-connect Portal
timeout ("check Portal, accept the dialog if it's there") — with the project owner asleep, there
was no way to check for or accept that dialog. The 6 `Siemens.Automation.Portal.exe` processes
present were the *same* 6 PIDs seen at the very start of the night, unchanged through dozens of
successful operations in between, so they don't look like the actual cause — more likely ordinary
first-connect flakiness. Retrying blindly a third time (or killing processes without being able to
confirm none held real unsaved work) was judged the wrong call rather than a shortcut worth taking
— **`MOVE_BLK_VARIANT` is unit-tested only, explicitly not presented as live-verified or closed**
until an `import`/`compile` actually runs clean. Cleaned up (temp verification `.cs` file deleted)
rather than left half-finished.

**Tier 6 also attempted the same night — `WAIT`/`FillBlockI` built, `Jump` deliberately not.**
Re-grounded all three precisely (`grep`, not visual re-reading, throughout). `WAIT` (`FC
VSDDataSequence`) and `FillBlockI` (`FC ModbusComs`) both turned out genuinely simple — mirror-image
Part shapes (`WAIT`: bare `Version`, no `DisabledENO`; `FillBlockI`: `DisabledENO="true"`, no
`Version`) — built the same way as every tier above, 13 more tests (344 converter tests total).
`WAIT` is the first production modeled with no destination tag at all (a pure delay, not a value
producer); `FillBlockI` is Move-shaped plus a `count` input. **Live verification is blocked** — by
the time these were ready, TIA Portal had stopped responding to *any* command at all, including
the lightest possible one (`sanity-check`, no scratch-project write involved) — confirming this
isn't specific to importing these two instructions, a genuine external outage with no one awake to
check for a stuck approval dialog. Left honestly unverified rather than retried blindly (each retry
risks piling up more stale Portal processes, the tool's own explicit concern).

`Jump` turned out to be the biggest finding of the night, and was **deliberately not built**:
grounding it (`FB VSDUpdateComs`) found its `label` port references a brand new `Access
Scope="Label"` node shape, and the actual jump target is a **network-level `<Labels>
<LabelDeclaration>` element** — a sibling of `<Parts>`/`<Wires>` — confirmed real in a *different*
`CompileUnit` than the `Jump` Part itself. `JMP` is genuine cross-network control flow, something
no production this converter has ever needed to model (every one so far is an independent,
network-local statement). This is a real IR-format design question, not a routine build — flagged
for the project owner's own call rather than decided unilaterally. Full detail in `ir/SPEC.md`.

**Not yet started**: Tier 4 (`Modbus_Master`/`Modbus_Comm_Load` — re-grounded precisely, see
`AITODO.md`; needs three new pieces of general infrastructure, not just port grounding — a
chain-fed `REQ` operand, multi-output-tag productions, and an open/unconnected operand variant).
See `AITODO.md` for current state, including both `MOVE_BLK_VARIANT`'s and `WAIT`/`FillBlockI`'s
still-pending live verification, and the `Jump` design question awaiting the project owner.

### Tier 5/6 live verification completed the next morning — `MOVE_BLK_VARIANT`/`FillBlockI` closed, `WAIT` hit a real distinct blocker, 2026-07-14

TIA Portal had recovered on its own by the time the project owner returned (confirmed via the same
`sanity-check` probe that found it down the night before) — the outage was transient, not caused by
anything in this project.

**`MOVE_BLK_VARIANT` and `FillBlockI` both now fully live-verified**: a synthetic FC composed from
both fixture networks imported and compiled clean (0 errors) in `SampleProject`, byte-identical
readable IR text before and after the full cycle. Getting there found one more real fixture gap,
the same "only live TIA catches it" pattern as the whole night: `FillBlockI`'s real `out`
destination (`CommsProcessData.NodeFaultCount[3]`, grounded originally in `FC ModbusComs`) is
**array-indexed**, not a plain scalar tag — the synthetic verification fixture had used a plain
scalar, which TIA's compiler correctly rejected ("Elements of a structure or of an ARRAY can only
be filled if all the elements have the same elementary data type"). No converter code needed to
change — the general array-index `AccessNode` support already existed (confirmed real since the
original `CommsProcessData.Node_Error` grounding, well before tonight) — only `FillBlockIFedByRail.xml`
needed correcting to match the real shape faithfully.

**`WAIT` hit a genuinely different, still-open blocker**: TIA's own `Import()` rejects it —
"An instruction with the name 'WAIT' cannot be found" — specifically in `SampleProject`, even
though the regenerated XML faithfully reproduces the real `JOB9002` shape (confirmed not a converter
bug: `MOVE_BLK_VARIANT`/`FillBlockI` imported into the very same target project cleanly in the same
session). The likely explanation is a library/technology-object dependency present in `JOB9002`'s own
project configuration but not in `SampleProject`'s — not yet confirmed, not guessed at further.
Flagged for the project owner rather than experimented on blindly (adding libraries to a project is
a more consequential change than anything else attempted tonight).

**345 converter tests, all green.** `AITODO.md` updated to reflect current state.

### Tier 4 built and tested, live verification blocked by a confirmed general Openness limitation — 2026-07-14

Full converter support for `Modbus_Master`/`Modbus_Comm_Load` built across all 7 files
(`FlgNetParser`/`FlgNetWriter`/`SimaticMl.Model`/`Ir.Model`/`GraphReducer`/`Ir.IrSerializer`/
`Ir.IrParser`/`FlgNetBuilder`) — the three new pieces of general infrastructure Tier 4's grounding
called for: `TraceChain` reused as-is for `Modbus_Master`'s chain-fed `REQ` port (a genuine
`Contact -> Contact -> REQ` chain, not a plain tag), a new `BuildChainIntoPort` helper on the build
side; `ResolveOptionalOutputPort` generalized to `ResolveOptionalOpenPort` (TON's own `ET`-specific
naming dropped once `Modbus_Comm_Load`'s three deliberately-unconnected ports — `FLOW_CTRL`/
`RTS_ON_DLY`/`RTS_OFF_DLY` — confirmed the shape wasn't TON-specific); multi-output-tag productions
extended to four outputs (`Modbus_Master`: `DONE`/`BUSY`/`ERROR`/`STATUS`) and three (`Modbus_Comm_Load`:
`DONE`/`ERROR`/`STATUS`), same pattern `MOVE_BLK_VARIANT` established. 15 new tests, **359 converter
tests total, all green.**

**Live verification: import clean, compile progressively narrowed from 6 errors to 2, both
remaining errors traced to a confirmed Openness limitation, not the converter.** A synthetic FC
composed from both fixture networks imported into `SampleProject` cleanly on the first attempt.
Initial compile found 6 errors, each independently explainable:

1. `Tag "GateBit"/"TriggerBit" not defined` — verification-fixture-only `GlobalVariable` ->
   `LocalVariable` fix, same pattern as `Abs`/`LIMIT`/`FillBlockI`'s own `GateBit` issue. Committed
   fixture (`ModbusMasterFedByRail.xml`) left untouched — it faithfully reflects the real shape.
2. **A genuinely new, real finding**: `Modbus_Comm_Load`'s `PORT` formal parameter is a specific
   Siemens system datatype called `PORT`, not a generic integer (`"The data type UDInt of the
   actual parameter does not match the data type PORT of the formal parameter"`). Fixture harness's
   `PortNumber` member retyped `UDInt` -> `PORT`; no converter code changed — the converter already
   treats operand types opaquely, this was purely a verification-harness gap.
3. `Missing instance DB` (both networks — `Modbus_Master_DB` and, via `Modbus_Comm_Load`'s own
   `MB_DB` parameter, `Modbus_Master_DB` again) — expected, `SampleProject` had never seen either
   instance before.

Fixing 1–2 and creating both instance DBs via `create-instance-db` cleared 4 of the 6 errors
outright (every "tag not defined"/"type mismatch" gone) — direct confirmation the converter's own
port/wire/parameter modeling is correct. The remaining 2 (`"The entered address is not within the
valid address range"`, one per network) trace to the exact same **standalone system-FB
instance-DB limitation already documented for `CycleDelayReset`** (`docs/notes/openness-quirks.md`
"Known constraints"): `create-instance-db` deterministically assigned `Modbus_Master_DB` number
`DB0` (confirmed deterministic — deleted and recreated, got `DB0` again), which is itself invalid
and has no exposed API to repair. The established fallback (export the real DB from `JOB9002` with
its own valid pre-assigned number, the same technique that fixed `MotorStarter_Instance`) doesn't
apply here either: neither `Modbus_Master_DB` nor `MB_Master_Comm` appears anywhere in `JOB9002`'s
full block listing, across either PLC station — confirmed not just mis-numbered but genuinely
invisible to `SW.Blocks`, exactly like `CycleDelayReset` was. Both fix paths exhausted; this
generalizes the earlier single-instruction finding into a confirmed limitation of standalone
system-FB instances broadly, not something specific to `TON`.

**Left honestly unverified for full live compile, same standard as `WAIT`.** Not a converter bug —
every error traceable to the converter's own output was found and fixed; what remains is entirely
an Openness/TIA infrastructure gap outside this tool's reach (no UI-automation path, and the
source-side fix that ultimately resolved `CycleDelayReset` — restructuring the real instance in
`JOB9002` itself — is the project owner's call, not mine to attempt). Scratch state cleaned up fully:
both broken instance DBs and the synthetic `Phase2Tier4Check` block deleted from `SampleProject`,
temp harness (`TempPhase2Tier4Check.cs`) deleted, device recompiled clean (`errors=0`) after
cleanup. `sanity-check` still shows 4 blocks `IsConsistent = false`
(`NodeStatusAlarms`/`MotorFwdRevSystem_Instance`/`MotorVSDSystem_Instance`/`MotorVSDSystem`) —
pre-existing from earlier Phase 1/2 work, untouched by Tier 4, device still compiles clean; not
chased further here.

**Tier 4's converter implementation is complete and as-verified as Openness currently allows.**

### Reference corpus growth: 7 new blocks close the "no permanent regression suite" gap — 2026-07-14

Project owner's own explicit ask, after reviewing what's actually left before S1 is "done" in
spirit, not just by the roadmap's literal exit criterion ("golden-file suite green for the whole
reference project" — technically already true for the original 7 blocks, but those only exercise
~5 of the ~24 instruction-level constructs this converter now supports; everything else was only
ever proven against real, uncommittable JOB9002 content, per today's own audit). Added 7 new
committed `ir/reference/`/`simatic-ml/reference/` blocks: `ThresholdAlarms` (comparisons),
`SignalConditioning`/`DataHandling` (arithmetic/box family), `BooleanExtras` (standalone Not,
SCoil/RCoil), `FBTimers`/`ScaleValue`/`TimingAndCalls` (TONR/TOF/CALL). Full per-block story,
including two real structural findings (`TONR_TIME`'s DB-member shape has no `R` field; a bare
`T#5S`-style time literal is rejected on a TONR/TOF `PT` port) and a real `RunAll` bug fixed along
the way (TIA's own `IsConsistent` cascade corrupting a naive per-block batch run — fixed with a
proper three-phase `RunAllSettled`): `tests/golden/README.md`'s own "Reference corpus growth"
section has the complete detail, kept there rather than duplicated here since that's where this
corpus's own story already lives.

**Deliberately excluded from this pass, real reasons not oversights**: `WAIT` (missing library
dependency, already flagged) and `Jump` (no design decision yet) stay out for the same reasons as
always. Modbus_Master/Modbus_Comm_Load's *standalone* form was already excluded (Tier 4, above);
its *multi-instance* form was considered and deliberately not attempted — would have stacked two
independently-unproven assumptions (whether Modbus_Master/Modbus_Comm_Load support multi-instance
at all, never grounded against real data; whether a hand-authored DB with an FB-typed member,
untested anywhere in this project, can stand in for a proper instance DB) for one FC's worth of
optional coverage, disproportionate given everything else closed cleanly.

**Also found and fixed, unrelated to any single new block**: two pre-existing committed `.ir`
files (`NodeStatusAlarms.ir`/`PerimeterSafetyAlarms.ir`) used a sidecar text format that predates
the S1 item 11 scope-suffix/nested-`OrStep`-branch changes and could no longer be parsed by the
current `IrParser` — confirmed via `Normalizer.AreSemanticallyEquivalent` that the committed XML
had no semantic drift, only the `.ir` encoding was stale; regenerated both from a fresh export.

**359 converter tests** (unchanged — this pass composed already-tested unit fixtures into
reference-project content, not new unit-level coverage), **101 openness-cli**, **11
golden-harness** offline, plus all 14 reference-project blocks — the complete committed corpus,
old and new — now verified together in one `RunAll` pass for the first time: import, compile (0
errors), re-export, `Normalizer`-equivalent, every one.

### Three smaller flagged gaps closed: data-boundary doc, Sanitizer `ExternalAccessible`, Normalizer Part-identity — 2026-07-14

Project owner's own explicit ask, after reviewing what was left once the reference-corpus growth
above closed. Three independent items, `WAIT`/`Jump` deliberately still excluded (unchanged).

**`docs/13-data-boundary.md`'s JOB9002 approval-scope record backfilled.** The recorded scope only
listed the original A-01/A-02 spikes and early S1 IR/converter design work — it had drifted well
behind the much larger volume of Amber-tier access actually done since (the full instruction-
coverage grounding sweeps, the `PlantAutoControl` plan's own bulk dependency-closure export/sanitize/
import), even though that work was itself separately confirmed with the project owner at the time
it happened. Added a new dated sub-entry recording the real scope, same genericization rule
applied.

**Sanitizer `ExternalAccessible=False` gap, closed — and a second real finding surfaced by live
verification.** `DbInterfaceMembers.RequireDefaultBooleanAttributes` used to hard-error whenever
any of `ExternalAccessible`/`ExternalVisible`/`ExternalWritable` was false — confirmed real
2026-07-14 (`FB VSDSim`'s own `SpeedCalcArray`, `ExternalAccessible=false`, found during the
instruction-coverage sweep). Fixed generally, not just for the one confirmed attribute: `DbMember`
gained three new fields (default `true`), captured and regenerated verbatim exactly like
`SetPoint` rather than hard-refused; a new IR-text marker per attribute (`EXTERNALACCESSIBLE=FALSE`
etc., shown only when false — deliberately *not* matching `TAGTABLE`'s own shown-when-true
`ACCESSIBLE`/`VISIBLE`/`WRITABLE` convention, since that one was never tested against a real false
tag and copying it here would put the marker on nearly every member line in every committed DB).

Live verification (composing a throwaway DB, not part of the reference corpus) found a real,
previously-unknown constraint neither the original gap report nor the fix's own first attempt
anticipated: **`ExternalAccessible=false` requires `ExternalVisible`/`ExternalWritable` to also be
false** — `ExternalAccessible=false` alone with the other two left `true` was tried first and
TIA's own `Import()` rejected it ("The attribute 'ExternalVisible' cannot be set"). Fixed the
committed fixture to match (all three false) and separately live-verified that
`ExternalWritable=false` *alone* (the existing `GlobalDbWithNonDefaultAttribute.xml` fixture's own
shape, a common "externally readable but not writable" pattern) genuinely is independently valid
— confirmed clean, 0 errors, in isolation. So `ExternalAccessible` gates the other two; they don't
gate each other or it. 7 new/updated converter tests (`GlobalDbWithExternalAccessibleFalse.xml`,
a newly-invented fixture — the real member name from `FB VSDSim` stays out of committed content,
same discipline as every other fixture in this corpus). **366 converter tests, all green.**

**`tests/golden/Normalizer` Part-UId volatility gap, closed with real graph-based identity
matching, not a simple content-key map.** Unlike `Access` (identified by its own `Scope`+`Symbol`
content, so a flat UId→content-key map was enough), a bare `Part` (`Contact`/`Coil`/`TON`/etc.)
has no distinguishing content of its own — two `Contact`s in the same network can be byte-identical
XML except for `UId`. Built `Normalizer.BuildPartContentKeyMap`: iterative structural refinement
(Weisfeiler-Leman-style color refinement) — start each Part's own signature from its content minus
every UId in its subtree (an `Instance` child's own UId is excluded defensively, since it isn't
independently confirmed volatile or stable either way), then repeatedly fold in every wired
neighbor's *current* signature (resolving through already-stable `Access` content-keys,
`Powerrail`, `OpenCon`, or another Part's own in-progress signature) until the whole set stops
changing. Real bug found live partway through: naively carrying each round's full descriptive
string forward as the next round's input embeds the entire previous signature as a substring, so
signature length grows multiplicatively and overflowed `Int32` (`ArgumentOutOfRangeException` in
`string.Join`) on `MotorStarter` — real color refinement hashes each round down to a compact,
fixed-size digest before reuse, not a literal running concatenation; fixed with a SHA256 hash per
round. 3 new `NormalizerTests` (identical topology under full renumbering; the real test — two
same-kind Parts with *swapped* relative numbering between documents, proving identity comes from
wiring topology, not document-order heuristics; a negative case confirming genuinely different
wiring still correctly reports non-equivalent, guarding against over-normalizing into a false
positive). **Live-verified against all 7 real blocks originally confirmed to hit this gap**
(`AirStarSystem`/`AnalogScale`/`EquipmentControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`/
`MotorStarter`/`MotorVSDSystem`) — every one now compares semantically equivalent; the full
14-block reference corpus `RunAll` still passes cleanly too (no regression). 14 golden-harness
tests total, all green.

## S2: explanation-quality checklist built from direct comparison, not invented solo

Per the roadmap, S2's second deliverable (the explanation-quality checklist) doesn't exist until
built — the project owner's own instruction was to ground it in real trial explanations first,
compared against each other, rather than draft it from first principles. Three full explanations
of real JOB9002 blocks (`PerimeterSafetyAlarms`, `MotorDOL`, `PlantAutoControl`) were produced and reviewed in
conversation and confirmed accurate by the project owner — content not committed anywhere, per
`13-data-boundary.md`'s 2026-07-14 S2-kickoff entry.

`PlantAutoControl` was then re-explained by five independent subagents, each given a different amount
of project/process context — from a bare task description up to full staging context plus explicit
methodology guidance — specifically to isolate what actually drives explanation quality versus
what just costs more tokens. Findings that shaped the checklist: cost didn't track context amount
monotonically (the bare-minimum condition wasn't the cheapest — it paid its own "discovery tax"
finding basic tooling info that direct orientation skips). The clearest, most reproducible quality
lever was an explicit instruction to check every instance of a repeated pattern exhaustively rather
than describe it from samples — this reliably surfaced real cross-instance bugs that
sampling-based descriptions missed, replicated across multiple independent runs, and is now
checklist item E-04. Data-boundary awareness (whether the agent checked if it was even allowed to
use this data this way) tracked whether it was told to read `CLAUDE.md` at all, not any deeper
context — a governance-awareness gap, not a technical-quality one, and is now E-06. A "uniform
across every instance" claim turned out wrong on direct recount in two independent runs (including
one produced with a purpose-built briefing document) — the single most common failure observed,
now E-01.

A one-off attempt to package the resulting methodology as an installable Claude Code skill
(`.claude/skills/explain-plc-block/`) confirmed the file alone isn't sufficient for a subagent to
invoke it as a real skill in this harness — a newly-added project-local `SKILL.md` didn't appear in
a fresh subagent's own available-skills list. It works today only as a plain reference doc a task
explicitly points at; genuine skill-invocation needs further investigation before relying on it,
not assumed working.

Checklist committed as `docs/14-s2-explanation-checklist.md` (indexed in `00-README.md`).
`AITODO.md` updated to reflect the three original S2-kickoff questions as resolved, with a running
count toward the 10-sample exit criterion.

## S2 gate review: signed off, S3 opened (2026-07-14)

Four real JOB9002 blocks explained in conversation this session — `PerimeterSafetyAlarms`, `MotorDOL`,
`PlantAutoControl`, `MotorFwdRevSystem` — covering 51+ networks combined, all explicitly confirmed
accurate by the project owner. Combined with the checklist itself (methodology above), that clears
the roadmap's exit bar (10 sampled networks judged accurate, no hallucinated tags/behavior) several
times over.

The project owner separately confirmed `WAIT` and `Jump` — carried forward from S1's own sign-off
as open questions needing input — are not needed. Closed rather than deferred; see `AITODO.md`'s
"Deliberately deferred" section for the technical detail preserved in case either becomes relevant
again. Modbus's live-compile gap, the third item from that same S1 carry-forward, is unaffected by
this and stays open.

S3's own entry criterion needed one thing beyond S2 itself: `docs/11-review-workflow.md` explicitly
*agreed*, not just drafted or existing. Summarized for the project owner in conversation and agreed
as-is — no changes requested, the doc stands exactly as originally drafted.

S3 (comment generation) is now open. No work started yet.

All three smaller flagged gaps from the earlier "what's left before S1" review are now closed.

## S3 first proof: a real title written through the IR layer, end to end (2026-07-14)

Planned properly before touching anything (two Explore agents + a Plan-agent review, each finding
verified directly rather than trusted secondhand): both network- and block-level Title/Comment were
already fully supported by the write path (`IrParser`/`IrSerializer`/`BlockSourceWriter` → real
SimaticML `<MultilingualText>` emission), but two real gaps existed — no guard against embedded
newlines in Title/Comment text, and no test anywhere for the actual "edit an existing title,
verify the new value is what gets written" scenario (every prior test was parse, fresh
construction, or unchanged-round-trip). Fixed both first: `IrSerializer.EscapeString` now rejects
embedded `\n`/`\r` with a clear error (`docs/04-design-philosophy.md`'s "fail loudly and early");
4 new tests added to `NetworkTitleCommentTests.cs` (network- and block-level edit round-trips, plus
the newline-guard itself). Also fixed a stale comment in `Normalizer.IsVolatile` (`tests/golden/`)
that justified skipping Title content on "always empty" grounds — an assumption S1 items 16/17
already disproved; the skip behavior itself was still correct, just for a different reason than
stated. 485 tests green (370 converter + 14 golden + 101 openness-cli) before touching TIA.

Research also surfaced that the exact live-TIA mechanics needed (export, edit a Title, re-import
with `Override`, clear the known `IsConsistent` refusal via `compile`, re-export, confirm the new
value landed) had already been proven live the day before, on this same block, verifying
`openness-cli`'s own import-overwrite behavior — narrowing what this proof actually needed to
verify to one specific, previously-untested link: editing through the *IR layer* itself.

Target, confirmed with the project owner ahead of time: `TimerSample` (`ir/reference/`) — the one
reference-corpus block with zero real-site lineage (built directly in TIA, not sanitized from
production logic), lowest possible risk for a first write-path proof.

**Hit a real, pre-existing bug immediately, unrelated to the edit itself**: `to-xml` on the
committed `TimerSample.ir` — original file, before any edit — threw `Malformed sidecar constant
line`. Isolated properly (reran the plain `git show HEAD:...` original before assuming anything):
confirmed the same failure on the untouched file, so not a regression from this session's own
Phase 0 changes. Root cause: the exact same "stale sidecar format" class of bug already found and
fixed for `NodeStatusAlarms`/`PerimeterSafetyAlarms` earlier this session — `TimerSample.ir` simply
wasn't swept up in that pass. The sidecar's `constant` line grammar gained a mandatory type suffix
and timer sidecars gained a `kind` field at some point after this file was last generated; the
committed text predates both. Fixed the same proven way: exported `TimerSample` fresh from
`SampleProject`, regenerated the `.ir`, confirmed zero semantic drift against the committed XML
(`Normalizer.AreSemanticallyEquivalent` = true, checked directly) before applying the title edit to
that current-format version instead of the stale one.

From there: title written into the IR for the block's 3 non-empty networks (network 4 is genuinely
empty, no Parts — left untitled, nothing to describe) → `to-xml` → `import` (`Override`) → the
expected `IsConsistent` refusal → cleared via `compile --block TimerSample`, 0 errors (one
unrelated device-level hardware-config warning, the same benign class already documented) →
re-exported → confirmed the new titles are what's actually in the re-exported IR, not just what
was written locally → `Normalizer` confirmed the logic itself is untouched, only Title differs →
full 14-block `RunAll` still passes.

**Presented for review — the project owner caught a real, valid gap before approving**: the
proof only wrote *network*-level titles; the block itself (`FC TimerSample`) was left with no
title at all, despite block-level Title being the same already-proven write path. Not a nitpick —
directly relevant to what S3 is actually supposed to produce. Fixed properly rather than deferred:
added `TITLE "Chained On-Delay Timer Sequence"` to the block header, reran the full live cycle a
second time (import → clear `IsConsistent` → compile clean → re-export → confirm both block- and
network-level titles present → `Normalizer` confirms logic still untouched → full `RunAll` still
passes, all 14 blocks). Committed corpus pair (`ir/reference/TimerSample.ir`,
`simatic-ml/reference/TimerSample.xml`) reflects the block-titled version, not the intermediate
network-only one.

S3's own exit criterion — an undocumented block gets useful comments end-to-end, generated,
imported, compiled, human-approved — is met by this one block. Richer, non-synthetic candidates
(`PerimeterSafetyAlarms`, `NodeStatusAlarms`) are the natural next targets, not part of this proof.

## S3 second proof: `PerimeterSafetyAlarms` — title *and* comment, both levels (2026-07-14)

Explicit instruction this round: title (short) and comment (longer, explains *why*) together, on
whichever block, both block- and network-level this time — the project owner's own direct response
to the block-title gap caught in the first proof. Picked `PerimeterSafetyAlarms` over
`NodeStatusAlarms` deliberately: richer logic (an `OR`-merge of 3 negated safety-zone contacts, not
just a flat bit-mapping) means there's genuine "why" to write a comment about, not just "what" —
matching `06-lad-conventions.md` C-202 ("comments say why, not what").

Learned from the first proof's own stale-sidecar surprise: sanity-checked `to-xml` on the committed
file *before* editing anything, this time — clean, so no repeat of the `TimerSample` detour.

Grounded the actual comment content directly from the real rungs, not from memory of explaining
this same block earlier in S2: bits 0-4 (`AlarmWord1.%X0`-`%X4`) all negate their source
(`SafetyZone1-3`/`SafetyGate1`) — consistent only with those tags reading true-when-intact, so a
break reads false and needs `NOT` to alarm. Bits 5-7 (`PullCord1`/`FireDamper1`/`FireDamper2`) carry
no negation at all — the opposite field-device convention, already true-when-triggered. Bit 0 is a
separate summary coil (`OR` of the three zone bits), not a re-read of an already-computed value —
existing purely so the HMI can show one "perimeter breached" indicator without decoding three bits
individually. Wrote both a short block-level `TITLE` ("Perimeter Safety Alarms") + higher-level
block `COMMENT` (what the block is for), and a network-level `TITLE` + the detailed polarity/summary
`COMMENT` above (the specific why). Network 2 (genuinely empty, no Parts) got neither — nothing to
document.

**Verification needed a different check than the first proof, and this was worked out properly, not
glossed over**: `Normalizer.IsVolatile` deliberately treats Title as ignorable but *not* Comment —
real comment content is exactly the kind of difference that check exists to catch. A plain
`AreSemanticallyEquivalent` call would have correctly returned false here, which isn't a failure,
just not the right tool for "did anything besides my intended documentation change." Instead:
stripped both the original and re-exported XML via `Normalizer.Strip`, then additionally blanked
the `Comment` `MultilingualTextItem` text in both stripped trees, and confirmed the two are then
byte-identical — proving the *only* difference anywhere in the document is the new Title/Comment
text, nothing structural. Full live cycle otherwise identical to the first proof: import
(`Override`) → cleared the expected `IsConsistent` refusal via `compile`, 0 errors → re-exported →
confirmed all four new strings are what's actually in the re-exported IR → full 14-block `RunAll`
still passes. Presented for review (title, comment, and the reasoning behind the comment, so it
could actually be checked against the real rungs) before committing.

## S3 third proof: real JOB9002 content, `PlantAutoControl` (2026-07-14)

First two proofs used the Green-tier reference corpus deliberately, precisely to avoid this
question until it needed answering: the recorded JOB9002 data-boundary approval covered A-01/A-02
spikes, S1 grounding, and S2 (read-only) explanation work, but never S3 write activity. Asked
before touching anything, per `CLAUDE.md`'s own "check the recorded scope, don't extend it
yourself, flag before proceeding" instruction, rather than assuming either way. Extended and
recorded as its own dated entry in `13-data-boundary.md` once confirmed.

Target: `PlantAutoControl`, the same block already extensively read and explained during S2. Genuinely
different shape of gap than the reference-corpus proofs: every one of its 20 networks already
carried a real title from the original engineer, so the first-round gap was the block itself,
which had neither a title nor a comment. Grounded fresh (a new export, not carried forward from
memory) before writing anything, matching the same discipline as the S2 explanation work. Compiled
clean on the first attempt — 0 errors, 0 warnings, a fully-configured real device, unlike the
synthetic scratch project's own benign hardware-config warning seen on the first two proofs.

Presented for review; the project owner then asked for a full redo covering all 20 networks, not
just the block, including replacing the existing titles rather than only adding comments alongside
them — a materially bigger, more consequential change than anything attempted previously (real
production content, an original engineer's own prior work, 20 networks instead of one). Clarified
the exact scope explicitly before touching anything — keep-vs-replace the existing titles,
comprehensive-vs-selective network coverage — rather than assuming either reading of an ambiguous
instruction, given the stakes.

Rewrote all 20 network titles and added a comment to each, grounded in the same verified
understanding built during S2's own explanation of this block, re-confirmed against the fresh
export rather than trusted from memory. Found and fixed two genuine spelling errors already flagged
during that S2 explanation, and otherwise converged close to the original engineer's own naming
wherever it was already accurate — authored fresh rather than either a blind rewrite or a rubber
stamp. Verification scaled up the same Comment-aware structural check used on the second proof:
stripped both the true pre-edit baseline and the fully-redone re-export, blanked all Comment text
in both, confirmed the results are byte-identical — across all 20 networks, the only differences
anywhere are the intended new Title/Comment text. Compiled clean, 0 errors, 0 warnings, on both the
block-only and full-redo rounds.

Per the data-boundary entry's own scope: the real content itself (specific tag paths, exact
comment wording) stays in conversation and inside JOB9002's own gitignored project, not reproduced
here.

## S4 Phase 1: mechanical rule-checking built and pilot-proven (2026-07-15)

Built exactly the 8-rule Phase 1 slice scoped in the approved plan: `converter review <file>
[<file> ...] [--ignore-errors] [--json]`, five discriminating rules (C-003 naming prefix, C-005
charset, C-201 title/comment presence, C-301+C-501 absolute-addressing with all three documented
exceptions, C-406 timer-kind checked in both declaration and usage form) plus three rules built and
explicitly labeled `CheckedVacuous` rather than `Checked` (C-102/C-401/C-404 — no jump/counter/
built-in-edge construct exists anywhere in the current IR model, confirmed against
`FlgNetParser`'s own whitelist, so these can structurally never fire; reported honestly as "cannot
fire against current IR capability" rather than implying real verification happened).
`--ignore-errors` (added mid-session, on request) makes a batch review record a per-file
`FileError` and continue rather than aborting the whole run on the first unparseable file — the
abort-on-first-error default (matching `to-ir`/`to-xml`) is unchanged without it. 46 new unit tests
(true-positive + true-negative per rule, including the exact `DataHandling.ir`-shaped C-301
exception case); full converter suite green throughout (412/412).

**Live pilot** against all 14 committed `ir/reference/*.ir` files reproduced every finding expected
from planning, exactly — arithmetic double-checked against the tool's own summary line (29
findings, 14 error/15 warn): 13/14 blocks missing their naming prefix (only `DB_Timers` already
compliant); the 5 DB-kind blocks missing a header comment, plus `TimerSample` for a genuinely
different reason (a real, previously-undetected gap — S3 gave it a block-level *Title*, never a
*Comment*, and C-201 checks the latter; a distinction this project has otherwise been careful
about); `NodeStatusAlarms` (15-bit alarm word, untitled — C-301+C-501 both fire) and
`PerimeterSafetyAlarms` (8-bit alarm word, *has* title/comment from S3's own work, still fails
C-501's literal "exactly one bit" condition — expected, not a reversal of that sign-off, a
different axis than what S3 evaluated); `FBTimers`/`TimingAndCalls`'s C-406 declaration/usage
split, one finding on each side. Confirmed `DataHandling.ir`'s own C-301 exemption directly against
its real content (not just the synthetic unit test written for it): it has three genuine `.%X`
slice-access writes (`StatusWord.%X0/.%X3/.%X7`) that would otherwise all be flagged, correctly
exempted because its header comment says "Data-handling reference examples" — the exact scoping
bug the plan-agent review caught before any code was written, now proven against real corpus
content.

**Exit criterion explicitly NOT yet met.** `02-roadmap.md`'s own S4 exit is "review of the reference
project matches the engineer's own review on a sample; false-positive rate acceptable" — that needs
an actual blind comparison against the project owner's independent judgment on the same content,
which hasn't happened (the pilot above used findings already narrated during planning, acknowledged
upfront as not a clean blind test). Full report presented for review 2026-07-15; project owner's
read: good enough to pause active work here and move to S5, explicitly *not* the same as calling S4
done — S4 stays **ACTIVE**, Phase 1 complete and pilot-proven, formal exit validation deferred
rather than skipped silently. Two of the pilot's own findings (the packed alarm words in
`NodeStatusAlarms`/`PerimeterSafetyAlarms`) are open questions about whether the *rule* or the
*practice* is right, not resolved either way — carried forward, not force-closed.

**Durable checkability findings** (owed from the plan's own "don't let this decay into
conversation-only narrative" discipline — both prior project audits, 2026-07-11 and 2026-07-14,
flagged exactly that failure mode as this project's most-repeated documentation bug):

- Severity/checkability mismatches: **C-113** and **C-504** are *error*-severity rules that are
  genuinely Bucket C (human design-intent judgment — "was this the right paradigm," "is this really
  a known consequence of that alarm") — no code-property check can validate them even in principle,
  only whether a stated *decision* exists (C-113's own "the chosen paradigm is stated in the header
  comment" clause is separately Bucket-A checkable, but that's a materially weaker claim than "the
  right paradigm was chosen"). **C-112** is *error*-severity and needs a WinCC HMI artifact this
  pipeline never ingests at all — structurally out of reach, not just unbuilt.
- **C-301's exception scope is three-part, not one**: alarm words (C-501's own conditions), comms
  mapping, and data-handling blocks (C-105). A pre-implementation review caught a draft that only
  modeled the first and would have false-positived on `DataHandling.ir`'s legitimate usage — all
  three now share one `IsSelfIdentifiedExemptBlock` keyword-match mechanism (`Rules.cs`),
  deliberately loose and Phase-1-appropriate, not a full C-105 compliance check.
- **C-406 needs checking in two structurally different places**: the *declaration* form
  (`DbMember.Datatype == "TONR_TIME"/"TOF_TIME"`, what `FBTimers.ir` has — zero networks, so a
  usage-only check would have silently missed it) and the *usage* form (`TimerBinding.Kind` inside
  a network's own `Timers` list, what `TimingAndCalls.ir` has instead). Both built and both fired
  correctly in the pilot.
- New, found while writing this section rather than during original planning: **C-408** ("`ET`
  never compared against a constant to produce a boolean trigger") looks Bucket-A checkable after
  all — a single-block scan for any `Expr.Compare` with one operand's `TagRef.Path` ending `.ET` —
  worth a look for Phase 2, not built this round.
- Rough re-estimate of the full ~50-rule set, done fresh against the actual rule text and the
  now-built `TagReferences`/`Rules` infrastructure (supersedes the looser live-conversation estimate
  from initial planning — deliberately not reconciled line-by-line against it, since that estimate's
  own per-rule reasoning was never itself recorded anywhere durable): beyond the 8 rules built here,
  candidates that look genuinely Bucket-A/single-block-checkable without new infrastructure include
  C-103 (Set/Reset pairing within a block), C-107 (edge-memory array one-write-per-element
  discipline), C-110 (OB1 input/output mapping call order), and C-408 above. A similar-sized group
  needs cross-block or whole-project context that doesn't exist yet in this tooling (C-114's own
  enable-chain cycle check is explicitly called out as mechanically checkable *in the rule's own
  text*, once that cross-block graph exists; C-308's "`DB_Settings` never written from logic" is
  fully mechanical in principle but needs scanning every block in a project, not one file at a
  time). A firm remainder (e.g. C-002, C-004, C-101, C-108, C-113, C-117, C-202, C-309, C-405,
  C-409, C-504, C-507) stays genuine human judgment regardless of tooling investment. Not
  re-verified to the same rigor as the 8 rules actually built and tested this round — a starting
  point for scoping a future Phase 2, not a committed spec.

## S4: real-content validation against JOB9002, `StatusAlarms` (2026-07-15)

Data-boundary approval extended first (own dated entry above) to cover `converter review` against
real JOB9002 content — a third, distinct kind of activity from S2's read/explain and S3's
write/comment-generation. Picked `FC StatusAlarms` (station_1, `JOB9001_PLC`) specifically because
that whole station had never come up anywhere in this project before (every prior touch — S1's
round-trip proof, S2's four explanations, S3's `PlantAutoControl` proof — used station_2, `JOB9002_PLC`),
so review of it is unambiguously fresh, and because it's alarm-category, the same family as
`PerimeterSafetyAlarms` (already explained during S2) — a good stress test for the C-301/C-501 exception
logic specifically.

Exported, converted to IR, and reviewed cleanly — 8 error/4 warn findings: missing `FC_` naming
prefix, no header comment, all networks with real logic left untitled, and three networks each
packing multiple alarm bits into one network (C-301+C-501 both firing) rather than satisfying the
"exactly one bit per network, network titled" exception — the same shape already seen in
`NodeStatusAlarms`/`PerimeterSafetyAlarms` during the reference-corpus pilot.

**Methodology note, stated honestly rather than glossed over:** this was not the blind comparison
S4's own exit criterion calls for. The plan was to hold the tool's output back until the project
owner had independently reviewed the same block and reported their own findings first; instead,
asked to see the tool's output immediately, with their own independent judgment to follow (flagged
this distinction explicitly before revealing, so it was a known tradeoff, not a silent one).
Verdict: **"That's what I got to, mark that as a pass."** — real signal that the findings hold up
against the project owner's own judgment on genuine, previously-untouched real-project content, but
a "shown first, then confirmed" match is a weaker result than an independent match would have been
(the standard confound: agreeing with an answer already seen is easier than reaching it
independently) — recorded as such, not inflated into "the blind test passed." Whether this is
sufficient to call S4's exit criterion met, or whether a genuinely blind pass is still wanted before
that claim, was left to the project owner rather than decided here.

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

## S6 first real proof: Kestrel Shredder System build (2026-07-15)

The first genuine end-to-end exercise of the S6 workflow (`CLAUDE.md`'s own "Workflow for logic
generation") against a real, plain-language functional description (genericized from a real
supplied spec — `docs/13-data-boundary.md` covers the sanitization), not a synthetic exercise.
Built out `GenProject1` from 5 blocks to a complete subsystem: `DB_Settings`/`DB_Controls`/
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

The project owner's verdict on the Kestrel build (previous section): functionally right — "it has
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

**GenProject1 became a committed corpus (commit `b14be52`):** `.gitignore` anchored to
`/GenProject1/` (the live TIA folder stays ignored; the extracted content no longer is), then all
17 blocks + 3 UDTs (`UDT_PusherIO`, `UDT_ShredderSequencerIO`, `MotorFwdRevIOSet` — enumerated by
grepping the exports, since `list` can't enumerate types) + the default tag table exported to
`simatic-ml/GenProject1/` and converted to `ir/GenProject1/` (21/21 clean `to-ir`, including OB1
`Main` — the deferred OB quirks are on the write path, not read). Four blocks + one UDT hit the
known `IsConsistent` export refusal ("Inconsistent blocks and PLC data types (UDT) cannot be
exported") and cleared via the documented block-level-compile-then-retry (all compiled 0 errors;
the only diagnostic was a device-level warning about IO points absent from the configured
hardware — expected for a sandbox project, recorded not hidden). Data boundary verified rather
than assumed: the corpus was scanned against `sanitization/Kestrel Shredder Systems.map.json`'s real-name
keys (case-insensitive; word-boundary for the short model codes) — zero hits, so docs/13's
"nothing identifying appears in GenProject1" claim now has a checked basis. This corpus is the
durable S6 output and the standing validation corpus for reviewer skills.

**Retrospective delivered (`docs/notes/genproject1-retrospective.md`) — owner pass PENDING:**
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

The project owner reviewed `docs/notes/genproject1-retrospective.md` in full and answered every
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
  never writes it *and orchestrating FCs never scan-copy into it*, naming the concrete GenProject1
  trap found during the read: `FC_ControlMain` MOVEs `DB_Settings.PusherX` over the pusher's UDT
  settings members every scan, so any faceplate edit would silently revert one scan later (the
  setting existed in two homes with a cyclic copy between them). C-122 reworded to match.
  GenProject1's own settings rework is a queued S6 request, not silently done.
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
  block attributes (empty/0.1 in GenProject1) that the IR currently drops entirely — queued
  converter work item to carry them as IR header lines, which is the only path to C-201's
  author/revision ever becoming mechanically checkable. Struct-inside-standalone-UDT round-trip
  needs one proof before the grouped-UDT `Set` sub-struct design can be committed to (anonymous
  Struct members are proven for FB statics via `EquipmentControlSystem`, not yet for `SW.Types.PlcStruct`).

Next per the docs/15 build order: `review-simplicity`, validated against the GenProject1 corpus
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
got only the skill file, the binding docs, and `ir/GenProject1/` (21 files), explicitly barred
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
Kestrel build demonstrably wrote (stage-gates' own S6 record cites OvercurrentTripped's member
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
iteration — the same established mode as the Kestrel build's own C-126/C-127 restructuring of
already-imported blocks (GenProject1 is S6's scratch; S7's gate concerns real-project
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
+ two-phase-validated.** `gen/GenProject1/requirements.md` — the first pipeline artifact ever —
69 REQs + 15 open questions from the genericized Kestrel spec (`manual:gen-spec-analysis`;
contamination rule enforced: REQ text only from sources, corpus touched for tag-status greps
only; both commits gated by the real-name scan — 0 hits/14 keys, counts-only evidence). First
telemetry rows live in `gen/GenProject1/telemetry.log`. Validation phase 1 ran against the
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
on the functional-findings wave → a GenProject1 fix request (the settings zeros + structural
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

## S6: fix wave 1 complete - drafted, signed, imported, compiled, invariance-proven, triple-reviewed (2026-07-17)

The full pipeline discipline applied to our own fixes, end to end. Phase 1 (worktree agent,
Portal-free): all four owner rulings drafted into IR with static verification (synthesize +
preflight zero-new-findings), the commissioning-defaults table produced with per-value rationale,
two design tensions surfaced and ruled (park-at-next-pre-start stands; jog must survive downstream
absence - implemented as the two-tier PusherModeForceOff / PusherCycleInhibitCmd split), settings
table signed as proposed (Oisin, 2026-07-16, recorded in gen/GenProject1/fix-wave-1.md). Phase 2
first attempt correctly stopped at the Portal boundary (unapproved fresh worktree binary hit the
first-connect dialog; classifier denials respected, state fully staged and documented); the
coordinator ran the staged sequence with the session's approved binary. One roundtrip lost to a
new playbook class - "Element cannot be found / check the consistency of the type used" (UDT
imports need compile --type before dependent iDBs; FBs before iDBs on interface changes) - now a
playbook entry with live proof. Corrected order: 9 imports, every block compile 0 errors, whole-
device compile Success 0/0, and all nine re-exports READABLE-IDENTICAL to the signed drafts
(invariance proven, not asserted). Merged at 238de48.

Check stage: three fresh-context blind reviewers against the merged corpus. Functional verdict
flipped from 28/18/6/11/4-contradicted to 45 implemented / 7 partial / 6 unimplemented /
10 disarmed / 0 contradicted - every ruled fix implemented with field-boundary evidence, all 14
spec numbers verified into the right PTs, anti-laundering clean. Mechanical conventions baseline
improved 17 to 16 (FC_ControlMain header cleared; both generated FBs + FC now tool-clean). The
reviewers earned their keep against our own code: new tier-1 candidates (simultaneous-jog dual
solenoids; power-cycle auto-resume now TRACED not hypothesized; BothSwitchesFault acts on
nothing; Fitted-drop mid-cycle strands Step), a reintroduced C-601 near-match in the new pusher
launch logic, a genuine C-604 rule-wording tension (commented-constant branch vs reservation
clause), a new C-117 error-class finding (direction changes lack not-running feedback terms),
and a converter reporter bug (C-301 count vs printed findings). Full actionable record:
gen/GenProject1/fix-wave-1-reviews.md; owner-rulings queue updated in AITODO.

Meta-lesson, recorded deliberately: the check stage found real defects in code written BY the
pipeline's own coordinator under the pipeline's own rules, one day after the rules were written.
That is the system working - the stricter-bar principle has teeth precisely because the reviewers
do not care who wrote the code.

## S6/S7 direction reset + session close: owner-questions gate, S7-first path proposed (2026-07-17)

**The owner called a course correction, and it stands as the session's most important output.**
Points raised, in the owner's own framing: (1) the pipeline "got ahead of itself" - three coding
waves ran manual-to-contract while gen-block-coding sat unbuilt at build-order step 7; (2)
gen-architecture was intended as a REUSE-FIRST carving algorithm - identify whole library blocks,
then pattern-composed blocks, then modified library blocks, then new/freeform, in that order,
handing the compiled list to the programming skill(s) - where the built skill derives blocks from
REQs and maps patterns afterwards; (3) programming should be two or three skills: New Block
creation / Modify block for new purpose / Modify block for fix; (4) the library is PURPOSELY thin
- organic growth via S8 harvest, never a library campaign; and (5) **S7 is the rush** - safe
modification is the capability that takes production load off the owner, freeing time to develop
this app (the project's bootstrapping loop, now stated explicitly).

**Coordinator's concession, recorded:** the criticism is correct. Manual-to-contract was written
for the exception and became the norm; the deferred-coding-skill priority was never revisited as
coding volume grew; and the fix wave's own defects (the reintroduced C-601 near-match, the C-604
tension) are exactly what a coding skill's write-time checklist would have prevented rather than
reviewers catching after. The reuse-first point is structurally right because decomposition
boundaries ARE reuse decisions - a carving blind to the library can make reuse impossible in ways
no after-the-fact mapping repairs - with one composition rule preserved: REQs stay the
correctness spine (each tier candidate held against the REQ set it claims; the almost-fits trap
guarded by traceability). The 3-skill split maps cleanly: gen-block-new (S6), and the modify pair
as S7's own deliverable shape (roadmap S7 verbatim: targeted edits + invariance + before/after
diff), sandbox-scoped until S7's gate formally opens.

**Everything now gates on `docs/notes/owner-questions.md`** (created this session at the owner's
request; priority-ordered A-F): A-1..A-4 direction rulings (carving model; skill split; bounding
manual coding; the S7-first ordering - converter diff tooling, modify pair vs the B-docket, D-4
then S6's ten requests, A-1 in passing), the B-docket (six tier-1 defect candidates from fix wave
1's check stage), C rule-book rulings, D project-scope decisions, E register questions, F tooling.
AITODO pruned to a lean recovery doc pointing there; superseded queues deleted per its own
discipline.

**Session-close housekeeping:** all six agent worktrees removed and branches deleted (every one
verified fully merged; master is the only branch); working tree clean; suites green (converter
491/491, openness-cli 101/101, golden 14/14). Persistent agent memory updated so the next session
starts with the owner's priorities even with this conversation cleared: the stricter-bar
principle, the S7-rush/thin-library economics, the build-order-drift lesson, and the environment
facts (classifier wants visible owner approval for site-data/admitted-content agent launches;
fresh worktree openness-cli binaries hit TIA's first-connect approval dialog - use the session's
approved binary for Portal work).

## S6: owner-questions batch pass - rulings applied, three items deliberately held (2026-07-17)

The owner answered `docs/notes/owner-questions.md` in full; this entry is the dated ruling record
the doc's own "never silently" rule requires. Every clear answer was written into its home
document rather than left narrative-only:

- **A-1/A-2/A-3 (direction) applied.** `gen-architecture`'s Method rewritten for reuse-first
  carving (library-whole -> pattern-composed -> library-modified -> freeform, matched against
  cached per-run library/pattern summaries, extra capability on a whole-reuse item explicitly not
  a finding). `docs/15-generation-pipeline.md` now carries the 3-skill Build-phase split
  (`gen-block-new`/`gen-block-modify-purpose`/`gen-block-modify-fix` + a shared
  modification-choreography reference), pulled forward to build-order step 6, and A-3's
  manual-coding bound (per-case owner waiver required once `gen-block-new` exists). **A-4 (the
  S7-first ordering) was explicitly NOT ruled** - the owner wants a fuller discussion before
  committing to the internal sequencing; it stays open.
- **B-docket verdicts recorded**, not yet coded - `gen/GenProject1/fix-wave-1-reviews.md`'s new
  "Owner verdicts on the B-docket" section covers B-1 through B-6. B-2's ruling withdraws the
  demo-panel OB100/`DB_PLC` startup-machinery waiver. B-5 surfaced a mismatch between the question
  asked and the answer given - recorded honestly as partially answered, not forced to a verdict.
- **C rulings applied to `docs/06-lad-conventions.md`:** C-8's `<DI/DO/AI/AO>` typo fixed to
  `<DI/DQ/AI/AQ>`; new C-007 (vendor-default-name exception); C-502 gets a confirming note (no
  project-scale-down, per C-6); new **C-128** (no automatic restart after E-Stop or a power
  event, except a documented exception - closes D-1/Q-01/B-2 together); F-5's four candidate
  rules adopted as **C-608-C-611** (comment-vs-rung contradiction, unexplained redundant terms,
  undocumented dead signals, C-601-named-bit scope - the last one's precise origin wasn't located
  in this session's source material, flagged for confirmation). **C-1 was not resolved** - the
  owner's proposed placeholder-naming convention needs a clarifying round-trip.
- **D applied/recorded:** D-1 -> C-128. D-3 -> `requirements.md` Q-11 resolved (real AI hardware
  exists generally; this prototype panel stays bool-signal-only by choice, not by hardware gap).
  **D-2 (settings rework) deliberately deferred** - owner's own call, documented as a live AITODO
  entry rather than actioned. **D-4 (S6 exit tally, stage-gates size) explicitly not ruled** - the
  owner wants a separate agent conversation about this doc's own bloat before deciding gate
  structure; flagged, not resolved by this pass.
- **E register resolved in `gen/GenProject1/requirements.md`:** Q-01 (new C-128), Q-08 (manual
  restart always required), Q-10 (overcurrent and overload stay distinct, not merged), Q-11 (see
  D-3), Q-12 (partial - `HrsRun` confirmed as the hour-clock's home, zeroable-reset behavior still
  open). Q-03 and Q-05 got owner context without closing (an HMI-boundary skill idea filed as
  `docs/16-future-ideas.md` FI-18; a motor-starter-FB hint noted for the reuse-first pass).
- **F applied:** F-1 (converter reporter bug) confirmed real by the owner, ticketed. F-5 -> the
  C-608-C-611 rules above.
- **Held open, by the owner's own words, not this pass's choice:** A-4, D-2 (deferred), D-4
  (routed to a separate conversation), C-1/C-2/C-3/C-4/C-5/C-7/C-10, D-5, F-2/F-3/F-4, and register
  Q-02/Q-04/Q-06/Q-07/Q-09/Q-13/Q-14/Q-15 - each is a case where the owner asked for further
  explanation/context before ruling, or explicitly asked that the topic wait. `owner-questions.md`
  itself was updated to mark resolved items with a pointer here and keep the rest listed as open.

## S6: owner-questions round 2 - remaining rule-book items ruled, two briefing docs prepared (2026-07-17)

Same day, second pass: the clarification-needed items from round 1 came back answered.

- **C-1 confirmed** - the `placeholder_<Var Name>` constant convention resolves C-604's
  wording conflict; written in. **C-2** - an interlock already inside a called FB satisfies
  C-117, no caller-side duplication needed; closes the sequencer's direction-change finding.
  **C-3** - a named bit that's a genuine equivalent of `Step = <from>` may be reused, satisfying
  C-121 (grep-confirm before treating pusher N7 as closed). **C-4** - `FaultReset -> step 0` is
  itself the recovery transition C-123 requires, even with no safer intermediate step; queues a
  verify-and-fix for `ParkedTimeoutFault`. **C-5** - C-115's handshake vocabulary applies to
  stepped-sequence FBs too, always, even if a given integration leaves it unwired; queues an
  interface-extension item (needs `gen-block-modify-purpose`, not a fix). **C-7** - resolved the
  actual confusion: PLC fault-latch/`FaultReset` (C-123) and HMI alarm acknowledgment (C-507) are
  different mechanisms; GenProject1's latched X1-X8 bits were never a C-507 exception case, no
  change needed. **C-10** - tentatively accepted as context (owner: "I dont know this is all
  vibes"), not turned into a rule. **F-2** - the `FaultFB` cross-block split is the FB's intended
  contract (deliberate external set, per-instance isolation); documented as a named C-103
  exception pattern. **F-3/F-4** - "all AI code need commented" generalizes C-605 to
  `DB_Settings` members too (F-4 folds in); F-3's literal empty-end-state sub-question stayed
  unanswered, still open. All written into `docs/06-lad-conventions.md` (C-103/C-115/C-117/
  C-121/C-123/C-507/C-604/C-605) and `gen/GenProject1/fix-wave-1-reviews.md`'s "Round 2" section.
- **Register resolved:** Q-04 partial (three types confirmed, FuncDesc primary - setpoint
  numbers still needed; the owner separately noted this kind of source disagreement should be
  flagged for a ruling as soon as found, not carried as a long-open question). Q-07 (3s upstream
  delay confirmed), Q-13 (reversal display is HMI-side, fault lamp is PLC-driven - now proposed),
  Q-14 (`FaultReset` is the only precondition, no separate control-on step), Q-15 (Hand is always
  an overlay on Manual, not a fourth mode) all closed in `gen/GenProject1/requirements.md`.
- **B-5 still open, and honestly so.** The owner's round-2 answer confirmed REQ-027's
  reverse-then-continue step design (already correct as built) but addressed a different question
  than the one asked - the REQ-028 counting-window semantics (does the 3-minute window re-arm on
  every reversal, or run once fixed from the first?) remain unresolved. Re-asked with a concrete
  worked numeric example rather than guessed at.
- **D-5 now resolved as deferred** (round 1 had it as needing clarification); at the owner's own
  suggestion, a new `docs/notes/deferred-items.md` was created to hold genuinely-deferred (not
  merely under-debate) work, and D-2 moved there alongside it.
- **A-4 and D-4 briefing docs prepared** (`docs/notes/a4-s7-ordering-briefing.md`,
  `docs/notes/d4-stage-gates-review-briefing.md`) per the owner's request - context, what's
  already landed, and concrete decision points for whichever agent/conversation the owner directs
  there. Neither A-4 nor D-4 is ruled; the docs are prep, not a ruling by proxy. Notably, D-4's
  briefing surfaces that `docs/02-roadmap.md`'s S7 entry criterion ("S6 done") is in tension with
  the owner's S7-rush framing - flagged for that conversation, not resolved here.
- **Still genuinely open after round 2:** A-4, D-2 (deferred), D-4, B-5 (re-asked), F-3's
  empty-end-state sub-question, and register Q-02/Q-06/Q-09. `owner-questions.md` updated
  throughout with round-2 pointers.

## S6: owner-questions round 3 - B-5 resolved as a real defect, not a comment fix (2026-07-17)

Third pass, same day. **B-5 resolved:** the REQ-028 reversal-count window is re-arming - the
count clears only after 180s with no new reversal, every reversal restarts the clock - the
opposite of fix wave 1's actual fixed-window-from-first-reversal implementation. This upgrades
B-5 from "comment mismatch" to a genuine functional defect, queued in the B-docket
(`gen/GenProject1/fix-wave-1-reviews.md`, `requirements.md` REQ-028). **F-3 resolved:**
`DB_Settings` is allowed to end up empty after the settings rework - not forced to keep content.
**Q-04 fully resolved:** per-type overcurrent setpoint numbers deferred (owner doesn't have them
yet) - moved to `docs/notes/deferred-items.md`, affected `DB_Settings` members get an explicit
`0` placeholder start value per the C-604 convention in the meantime.

Every item raised in `docs/notes/owner-questions.md` is now either closed (with a pointer) or
deliberately deferred/routed to a separate conversation - A-4, D-2, D-4, D-5 (deferred, tracked
in `docs/notes/deferred-items.md`), F-3(b) (unprioritized tooling backlog), and register
Q-02/Q-06/Q-09 (simply unanswered) are what remains.

## S6/S7: agent-tasks dispatch board opened; B-2 (startup machinery) closed, first proven OB write-path round trip (2026-07-17)

`agent-tasks/` created as the live dispatch board for the B-docket/C-item work the owner-questions
batch surfaced - one file per task, a Portal-queue gating protocol for GenProject1's single-writer
scratch project, explicit multi-agent-awareness in every task doc. Two agents worked it
concurrently this session (as designed): one on task 08 (`ParkedTimeoutFault`, C-4), one on task
02 (`02-startup-machinery.md`, B-2/D-1/Q-01/C-128).

**Task 02 closed - `DB_PLC` + `OB100` built, imported, compiled clean.** `DB_PLC.Simulation`
(C-305) plus `OB100` (`SECONDARYTYPE Startup`) forcing `Step` to idle on both stepped sequencers
and clearing the S/R-driven/transient state named in C-124/C-403 (fault latches - `FaultActive`/
`FTR`/`FTS` and the pusher/sequencer fault bits - deliberately excluded per C-124's carve-out).
Whole-device compile 0 errors/0 warnings; both new blocks individually consistent (the known
"Inconsistent blocks... cannot be exported" playbook entry applied and cleared exactly as
documented); re-export readable-identical to the signed IR. One real content bug caught by the
compile gate itself, not by review: `StartTimer` in the imported `FB_MotorFwdRevSystem` turned out
to be a `TEMP` (scan-scoped, no persistence, not externally addressable), not a `Static` - a
misread from static grep alone; the block-level compile error ("Tag ... StartTimer not defined")
caught it immediately, removed from OB100's reset set. **New grounding for the project itself:**
AITODO's "OB1 write-path never proven" note was specifically about `Main`'s own template quirks -
narrowed now that a hand-authored, non-`Main` OB has round-tripped clean; `converter to-xml
--synthesize` handles a sidecar-less OB correctly, and TIA's own `Import()` silently corrects an
OB's system-input parameter set to the real one for its `SecondaryType` (caught via re-export
diff: my authored `Initial_Call`/`Remanence` - copied from OB1 - got replaced with OB100's actual
`LostRetentive`/`LostRTC`). C-111 full simulation-mode gating deliberately scoped out of this pass
(the traced B-2 defect only needed the restart-safety half); flagged as a follow-up.

**Task 08 found a real converter gap (D-6), not yet resolved.** Modifying a network that already
has real (non-synthesized) sidecar data from a prior export hits `IrFormatException: Network N:
IR has X move(s) but the sidecar records Y` - affects every remaining queued task except 02 (all
of them touch existing, already-exported networks). Full writeup: `docs/notes/deferred-items.md`
D-6; concrete case: `gen/GenProject1/fix-wave-1-reviews.md` C-4 / `agent-tasks/08-
parkedtimeoutfault-recovery.md`. Per hard rule 7, this is a converter gap to report, not something
to hand-patch around - tasks 03/04/05/06/07/09 are blocked on it, flagged in `agent-tasks/README.md`.

## S6/S7: agent-tasks Portal queue fully closed (2026-07-17)

All 8 queued tasks (02-09) closed same day, two sessions working the queue concurrently exactly as
designed - no Portal collisions, no clobbered work. D-6 turned out not to be a real blocker in
practice: the whole-file strip-and-`--synthesize` workaround (proven by task 03, reused by 06/07/09)
handled every case, at the cost of a larger diff each time (full sidecar regeneration). B-1 through
B-6 (the full defect docket) and C-4/C-5 are now built and compiled clean against GenProject1 -
full detail in `gen/GenProject1/fix-wave-1-reviews.md`'s per-item entries and the `agent-tasks/`
commit history. Task 09 (C-115 handshake vocabulary, C-5) needed and got a real gate-1 mini-manifest
sign-off before coding, per docs/15's hard gate for interface changes - the queue's only task that
actually exercised that gate.

**`FC_AlarmsMain`'s standing inconsistency also closed out** (was flagged pre-existing/not-mine
throughout the queue work, `list`/`sanity-check` showing it `INCONSISTENT` from the very first
Portal touch this session, unrelated to any block this session edited): a plain block-level
`compile --block FC_AlarmsMain` cleared it in one call, the same known playbook pattern used
throughout the queue - not a content defect, never was. `sanity-check` now reports
`OVERALL: HEALTHY, INCONSISTENT: 0` for the whole device.
