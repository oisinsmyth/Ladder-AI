# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see Exit-criteria evidence below). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | not started | — | |
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
