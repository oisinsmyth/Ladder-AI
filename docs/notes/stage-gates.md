# Stage gates

**Active stages: S5 — Data extraction and S6 — Generation** (see the table below for full per-stage status, and `docs/02-roadmap.md` for entry/exit criteria). *(S0 remains formally open — exit criteria met, gate sign-off pending, see the S0 row — but work has long since moved to S5/S6.)*

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE — exit criteria met, gate review pending** | — | Entry criteria met: TIA V20 + Openness installed. Done: repo skeleton; openness-cli `list` with safety filter (built + live-verified, incl. cold-open); Windows "Siemens TIA Openness" group membership confirmed manually via cmd by project owner (2026-07-10); A-01 and A-02 verified (2026-07-10, see `docs/evidence/stage-S0.md`). Project in use: **JOB9002 - Tom White Waste (scratch copy)**, replacing JOB9003 - K150 (no longer in use) — private engineering project, Amber-tier, explicit per-project approval recorded in `docs/13-data-boundary.md`; incomplete against `06-lad-conventions.md` but sufficient for verification. TODO: formal gate review sign-off before flipping to done/starting S1 |
| S1 — Lossless round-trip | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | ADR-0001/`ir/SPEC.md` decided; converter + `openness-cli` + golden harness support ~24 instruction-level constructs (full list: `FlgNetParser.SupportedPartNames` plus `CALL`), all live-verified against real or reference-project data at least once. **`FC PlantAutoControl` round-trips through the true TIA cycle, completely** (2026-07-14 — export → sanitize → import → block-level compile clean → re-export → `Normalizer.AreSemanticallyEquivalent` = true) against its full real dependency closure (8 dependency FBs, 26 DB/tag-table roots) — the actual Layer 1 assertion this stage exists to prove, at production scale. **Reference-project corpus grown from 7 to 14 committed artifacts** (2026-07-14, "Reference corpus growth" in `docs/evidence/stage-S1.md`) specifically to close the gap between that production-scale proof and the committed regression suite: the corpus now exercises ~20 of the ~24 supported constructs (up from ~5), not just Contact/Coil/OR-merge/TON. Three smaller flagged gaps (data-boundary doc staleness, Sanitizer `ExternalAccessible`, `Normalizer` Part-identity) also closed the same day. **Signed off with two real items deliberately still open, carried forward rather than blocking the gate**: `WAIT` (missing library dependency in `SampleProject`, needs the project owner's input) and `Jump` (genuine cross-network control flow, needs a real IR-format design decision before any code gets written) — neither is part of the committed reference corpus, so neither affects the literal exit criterion; both tracked in `AITODO.md`. All PC-side suites green at sign-off: 366 converter, 101 openness-cli, 14 golden-harness (offline) + all 14 reference-project blocks verified live together in one `RunAll` pass. See `docs/evidence/stage-S1.md`. |
| S2 — Read and explain | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | Deliverables per `02-roadmap.md`: 4 real JOB9002 blocks explained in conversation (`PerimeterSafetyAlarms`, `MotorDOL`, `PlantAutoControl`, `MotorFwdRevSystem`; not committed anywhere, per `13-data-boundary.md`'s S2-kickoff entry) — 51+ networks sampled, all confirmed accurate by the project owner (2026-07-14), well past the 10-network exit bar. Explanation-quality checklist built and committed (`14-s2-explanation-checklist.md`), derived empirically from re-explaining the same real block across five subagents at varying context levels and verifying every claim against source, not invented solo (full methodology in `docs/evidence/stage-S2.md`; "S2: explanation-quality checklist built from direct comparison"). One real error did occur and was caught during that verification pass (a wrong field-uniformity count in the first `PlantAutoControl` pass) — corrected before the project owner's own sign-off; checklist item `E-01` exists specifically because of it. **Signed off with `WAIT`/`Jump` explicitly closed as not needed** (project owner's own call, 2026-07-14) — carried in S1's sign-off as open questions needing input, now resolved rather than deferred (detail in `AITODO.md`'s "Deliberately deferred" section). Modbus's own live-compile gap (same S1 finding) is untouched by this decision and stays open, unrelated to S2/S3. |
| S3 — Comment generation | **DONE — gate reviewed and signed off by the project owner** | 2026-07-14 | Entry criteria met: S2 done, and `docs/11-review-workflow.md` explicitly agreed by the project owner (2026-07-14). Exit criterion met three times over, at increasing scale: `TimerSample` (Green-tier, title only, both block and network level), `PerimeterSafetyAlarms` (Green-tier, title + comment, both levels), and `PlantAutoControl` (real JOB9002 content — data-boundary approval explicitly extended first — title + comment, block level and all 20 networks, including replacing the original engineer's own titles). All three live-verified end-to-end (edit IR → `to-xml` → `import` → clear the known `IsConsistent` refusal via `compile` → re-export → confirm the new content is genuinely present → confirm nothing structural changed via `Normalizer`) and reviewed/approved before committing. Two real converter gaps closed along the way (embedded-newline guard in `IrSerializer`/`IrParser`; the previously-untested "edit an existing title" scenario, now covered by 4 new tests) plus one real pre-existing corpus bug found and fixed (`TimerSample.ir`'s stale sidecar format) — swept the other 13 committed reference-corpus files afterward to confirm it wasn't a wider gap; it wasn't. Full detail: `docs/evidence/stage-S3.md`. |
| S4 — Convention review | **DONE — gate reviewed and signed off by the project owner** | 2026-07-15 | Phase 1 (8 of ~50 rules: C-003/C-005/C-201/C-301+C-501/C-406, plus C-102/C-401/C-404 labeled vacuous) built and tested (46 new tests, 412/412 suite-wide). Live pilot against all 14 reference-corpus files matched every predicted finding exactly. Real-content validation against JOB9002 (`FC StatusAlarms`, station_1 — never previously touched by this project) confirmed the same finding pattern holds outside the reference corpus. **Signed off on a "shown, then confirmed" match, not a genuinely blind one** — flagged explicitly before revealing the tool's output, and again before closing the gate; project owner's own informed call to accept it anyway rather than run a stricter blind pass first. **The 8 is the sign-off figure and stays that way; the FI-09 waves have since taken `converter review` to 18 mechanized C-IDs** (`ReviewRunner.AllRuleIds`, verified 2026-08-05 — see `docs/16` FI-09 for what remains, which is AI-by-design). Full detail: `docs/evidence/stage-S4.md`. |
| S5 — Data extraction | **ACTIVE** | — | Entry criteria met: S1 done (S4 not required — roadmap explicitly allows running in parallel with S3/S4). Opened 2026-07-15, in parallel with S4 (still open at Phase 1, not blocking). No work started yet — detailed plan being built, same process as S3/S4. |
| S6 — Generation | **ACTIVE** | 2026-07-15 | Entry criteria met: S3 done (already true); seed pattern library exists (`docs/07-pattern-library-spec.md`, two kinds — equipment-instance FB/FC via `CALL`, repeated rung-shape documented from a real example — no new IR mechanism, redesigned from an abandoned `template.ir`/`<SlotName>` approach). Seeded with `patterns/motor-dol/` (ADMITTED, all 5 criteria met) and `patterns/chained-permissive-enable/` (ADMITTED, criterion 3 accepted on partial evidence — drafted-with-prior-knowledge, not blind, gap recorded not erased). Project owner's own call: two well-proven kinds satisfies the roadmap's "~10 patterns" as an approximate target, not a literal count; the rest grows organically once S6 is running. **Exit criterion open: 1 of 10 fresh plain-language requests** (D-4 — fix waves and validation fixtures don't count; live tally in `AITODO.md`). **2026-08-05 — S6-Killer-Plan answer-key validation wave** (the largest since the stage opened): graded MATCH 14 / IMPROVEMENT 1 / DEFECT 3 / REGRESSION 1 / SPEC-GAP 3, autopsy → the four-rung spec pipeline + the FI-36/FI-39 mechanical floor; deliberately **not** an S6-exit request. Full detail: `docs/evidence/stage-S6.md` (2026-07-16 → 2026-08-05). |
| S7 — Modify existing | not started | — | |
| S8 — Pattern maturation | not started | — | |
| S9 — Sim verification | not started | — | Blocked on R-07 (PLCSIM vs S7-1200 G2) |

## How this file works

This is the **status index** — the stage table above is the whole point of it: check
which stage is active and whether a capability has passed its gate, in one cheap read.
Detailed narrative, acceptance-test evidence, and dated history live in `docs/evidence/`,
one file per stage (split out of this file 2026-07-18 per `docs/16-future-ideas.md`
FI-20; this file was ~4,300 lines before the split). Open a stage's evidence doc only
when you actually need its history.

## Full evidence per stage

- **S0 — Foundation:** `docs/evidence/stage-S0.md`
- **S1 — Lossless round-trip:** `docs/evidence/stage-S1.md`
- **S2 — Read and explain:** `docs/evidence/stage-S2.md`
- **S3 — Comment generation:** `docs/evidence/stage-S3.md`
- **S4 — Convention review:** `docs/evidence/stage-S4.md`
- **S6 — Generation:** `docs/evidence/stage-S6.md` (also holds the S6/S7 sequencing entries and the 2026-07-17 sub-agent process-rule decision)
- **S5 / S7 / S8 / S9:** no evidence doc yet — not started, or no work recorded.

**Cross-stage evidence (not per-stage — validation fixtures and pipeline-design records):** the
S6-Killer-Plan answer-key validation (2026-08-05) is summarised in `docs/evidence/stage-S6.md` and
recorded in full across: `docs/notes/S6-Killer-Plan.md` (the plan + status block),
`docs/evidence/PlantAutoControl-bench-derivation-notes.md`, `…-grading.md`, `…-autopsy.md` (the
correlated-check root cause), `…-review-conventions.md`, `…-review-functional.md`,
`…-review-simplicity.md`, `docs/evidence/PlantAutoControl-answerkey/` (the sealed key), and the two
adversarial design rounds `docs/evidence/four-rung-design-validation.md` / `…-round2.md`.

**HMI capability probe (FI-54), 2026-08-07 → 2026-08-09 — RUN AND CLOSED, and it is NOT a stage or a
capability.** TIA Openness's HMI surface walked live against JOB9002's scratch copy, so that ADR-0007
would rest on measurement. `docs/10-non-goals.md`'s "not now" line for HMI engineering **still
stands** — nothing here may be used as HMI engineering capability, and `openness-cli hmi-create-screen`
/ `hmi-edit-screen` / `hmi-new` / `hmi-delete` / `hmi-set` remain **probe commands pending
ADR-0007's disposition**. Record: `docs/notes/hmi-capability-probe-plan.md` (programme state, all six
phases), `docs/notes/openness-hmi-write-api.md` (findings), `docs/notes/openness-hmi-api-survey.md`
(the read survey), `docs/evidence/hmi-capability-probes.md` (transcripts, redacted per `docs/13`),
`docs/adr/adr-0007-hmi-engineering-scope.md` (**Proposed — awaiting the project owner's decision**).
Headline results: the create/modify/bind/script/compile chain works end to end; **deletion orphans
silently**; **alarm text cannot be written at all**; 21 of 56 item types refuse without saying why;
and the device compile is a genuine reference-integrity gate. A seventh phase (P7, 2026-08-09)
**retracted one of those headlines**: dynamization kinds are gated on the target property's type, so
**5 of 6 create** (`Flashing` on colour properties, `ResourceList` on text) — the earlier "3 of 6"
was a confounded probe, not an API limit.

**Appending history going forward:** add dated entries to the relevant
`docs/evidence/stage-SN.md`, and update this file's status table in place. Keep large
verbatim transcripts (blind-run reports, full tool output) in the evidence docs — never
inline here (FI-19/FI-20). When a not-yet-started stage opens, create its evidence doc
and add its link above. **A record that isn't per-stage** — a validation fixture, a
pipeline-design validation round — still gets both: a pointer in the cross-stage block
above, and a dated summary entry in the evidence doc of the stage it was run under.
Evidence that exists only as loose files nobody links to is how the 2026-08-05 audit
found the largest S6 wave unrecorded here.
