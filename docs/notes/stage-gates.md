# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see Exit-criteria evidence below). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | **ACTIVE (walking skeleton: export→to-ir→to-xml→import→compile verified live; re-export blocked by a pre-existing project state issue, not this session's code)** | — | ADR-0001/`ir/SPEC.md` decided; converter (C#, `src/converter/`), `openness-cli export`/`import`/`compile`, and golden harness machinery (`tests/golden/`) built and live-verified for a Contact/Coil-only, multi-assignment, slice-addressed slice. See Exit-criteria evidence. |
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

Real findings from the live proof against `JOB9002` blocks (2026-07-11), each a correctness gap
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

#### Result, 2026-07-11 — did the walking skeleton pass its own test? Partially.

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

**Precisely characterized, 2026-07-11, by the new `openness-cli sanity-check <project>`**
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
