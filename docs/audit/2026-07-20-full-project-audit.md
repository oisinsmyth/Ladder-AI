# Audit — 2026-07-20: full project audit (all dimensions, from zero)

> ## ⚠️ STATUS: OUT OF DATE — NOT IMPLEMENTED (marked 2026-08-05)
>
> **The findings below were true on 2026-07-20 and are retained as the historical record of that audit.
> They should not be treated as the current state of the project, and the fix list was never executed.**
>
> - **Fix list: unimplemented.** None of the 19 items (F-01–F-19) were actioned. The audit was read-only
>   by charter and the owner chose recommend-only; no follow-up execution pass was run.
> - **Findings: stale.** The project changed substantially in the ~2 weeks after this audit — enough that
>   re-verification, not selective reuse, is the right move. Concretely, since 2026-07-20 master gained:
>   the **four-rung structured spec pipeline** (`gen-pid-analysis`, `gen-functional-analysis`,
>   `gen-equipment-spec`, `gen-code-structure`) — so the D8 skill inventory and `docs/15` build-order
>   findings no longer describe reality; the **FI-36/FI-39 "mechanical floor"** tooling (candidate-scan,
>   undriven-scan, relation-reconcile, signal-sweep, trace guard-containment); an **FI renumbering**
>   (FI-32..35 → FI-36..39) that invalidates the FI-status finding F-03; a **`tagstatus` hard-rule-3 fix**
>   (DB-member resolution); and a **data-boundary change** ("live-run data boundary — full
>   Red-confidentiality access, zero retention", `9971dbd`) that directly supersedes the premises of
>   F-17 and F-19.
> - **Do not action this fix list as-is.** Individual items may still be valid (several are simple doc
>   corrections), but each needs re-checking against current `master` before being executed.
> - **Next step:** run a fresh full audit under the charter when wanted. Its D7 (prior-findings
>   follow-through) should treat this list as *unimplemented-and-superseded* — i.e. re-derive rather than
>   tick off.
>
> *This banner is an archival status annotation. The audit's findings, evidence, and counts below are
> unaltered from the original — per the charter, a landed report is never rewritten.*

The first audit run under the standing charter (`docs/audit/README.md`). Read-only: this report and its
paired fix list (`2026-07-20-full-project-audit-fixlist.md`) are the entire output — nothing else in the
repo was changed, and no ladder logic was opened.

## Project state summary — the strong overview

**Ladder-AI is healthy and mid-S6.** The tooling is solid, every test passes, and the defects found are
almost all *documentation/governance drift* — records lagging behind work that actually shipped — not
broken code. Nothing found is a blocker.

- **Active stage:** S6 (generation-pipeline validation). All three coding skills (`gen-block-new`,
  `gen-block-modify-fix`, `gen-block-modify-purpose`) and the S7 invariance tool (`converter diff`) are
  built and validated; the S7 gate is deliberately held until S6's exit criterion (ten fresh plain-language
  generation requests) closes. S5 library-filling runs in parallel (FI-21).
- **High-water mark since project start:** full `PlantAutoControl` live round-trip proven; ~20 instruction
  constructs supported; 24 mechanized convention rules; the FI housekeeping backlog cleared (FI-26–30
  implemented 2026-07-20); the audit charter itself just formalised.
- **Test baseline (Method rule 1, all three suites, this machine):** **converter 671 · openness-cli 122 ·
  golden-harness 37 = 830 passed, 0 failed, 0 skipped.** All suites build and run here (net48 openness-cli
  included — `Siemens.Engineering.dll` present).
- **Health by area:**
  | Area (dimensions) | Read | Note |
  |---|---|---|
  | PC-side code quality (D4) | 🟢 green | exception discipline clean, 0 real TODO/skip, net48 pin intact |
  | Tests (D4, Method 1) | 🟢 green | 830 pass, 0 skip; but live-TIA proofs remain un-automated |
  | AI-operational layer (D8) | 🟢🟡 green-amber | build-order table matches skills, hooks coherent; minor telemetry-convention slips |
  | Docs accuracy & consistency (D1/D2/D3) | 🟡 amber | several "current status" claims lag reality (rule counts, FI status, to-dos) |
  | Governance records (D9) | 🟡 amber | ADR index stale; risk register not reflecting materialised risks |
  | Git hygiene (D10) | 🟡 amber | 2 orphan branches; audit milestone unlogged |
  | Compliance / data boundary (D5) | 🟡 amber | data-boundary decision still DRAFT; approved Amber data committed in the clear |
  | Dead-link integrity (D6) | 🟢 green | one dangling path in an export artefact; everything else resolves |
- **Headline open decisions / risks (all `decide`, none new-but-unknown):** the data-boundary decision
  (ADR-0003 / `docs/13` still DRAFT) while approved real-restricted data flows to a cloud AI; a risk-register
  review that hasn't marked now-materialised risks; whether to grow the committed corpus to close the
  live-TIA regression gap; whether `PartNode`/`DbMember` (now 14 fields each) warrant a restructure.
- **Fix list:** 19 items — 0 blocker, 6 medium, 13 low. Dispositions: **9 fix** (mechanical, safe),
  **8 decide** (owner call), **2 accept** (acknowledged/deferred). See
  `2026-07-20-full-project-audit-fixlist.md`.

## Header

- **Requested by:** project owner — "a fully comprehensive audit starting from zero, covering all dimensions."
- **Scope:** all ten dimensions D1–D10, including the normally-opt-in **D5 compliance**. Nothing excluded.
- **Prior-audit baseline:** none — genesis full read of the whole project. The two prior audits
  (2026-07-11, 2026-07-14) were used only as **D7 inputs**, not as a coverage baseline.
- **Disposition:** recommend-only (owner's choice) — the audit is read-only; fixes are a separate later step.

## Findings

Grouped by dimension, severity-ranked within each. Each finding carries an `F-NN` id resolving to the fix
list. Findings describe; the audit fixed nothing.

### D1 — Stage & goals accuracy

- **F-02 (medium) — "8 of ~50 rules" understates mechanized review coverage by 3×.**
  `docs/notes/stage-gates.md:13` (S4 row) and `AITODO.md:35` both describe the mechanical reviewer as
  "8 of ~50 rules". `src/converter/Converter/Review/Rules.cs` now implements **24 distinct C-IDs**
  (C-001/003/005/101/102/103/105/118/119/120/121/122/123/124/125/201/301/307/401/404/406/407/408/501) —
  the FI-09 wave landed after S4 sign-off. `AITODO.md` is internally inconsistent about this: line 269
  acknowledges the FI-09 additions while line 35 still says 8.
- **F-03 (medium) — AITODO FI round-up contradicts `docs/16` on same-day-implemented items.**
  `AITODO.md:262–274` lists FI-17/22/23 and FI-26–30 as "awaiting a decision / still open", but
  `docs/16-future-ideas.md` ("Implementation status — 2026-07-20") and each entry mark all of them
  **IMPLEMENTED 2026-07-20**, with matching `converter` subcommands live in `CLAUDE.md:46–51`. (Cross-file,
  so also a D2 instance.)
- **F-15 (low) — `00-README` "immediate to-dos" already complete.** `docs/00-README.md:33` still says
  "Verify assumption log items A-01/A-02 during S0"; both are marked **VERIFIED 2026-07-10** in
  `docs/09-risk-register.md:22–23`.

### D2 — Doc internal consistency

- **F-06 (low) — ADR-0005 status self-contradicts within one file.**
  `docs/notes/synthesis-parity-plan.md` calls ADR-0005 "**Proposed**" at lines 111 and 141, but line 121
  in the same file says "**ADR-0005 Accepted (owner)**". The actual ADR status is Accepted
  (`docs/adr/adr-0005-derive-always-sidecar.md:3`). (Also a D9 currency finding.)
- *Positive:* the historic `ir/SPEC.md` TONR self-contradiction risk is **reconciled** — items 15/18/19
  mark TONR resolved; the lone "deferred" mention is a point-in-time narrative sentence, not a live status
  claim. The status-layer-vs-narrative-log shape persists across `docs/16`, `ir/SPEC.md`, `AITODO.md`,
  `stage-gates.md` (the charter's recurring risk), and produced F-02/F-03/F-06 this round.

### D3 — Docs-vs-code accuracy

- *Positive — convention rule-ID parity is clean.* All 24 C-IDs implemented in `Rules.cs` exist in
  `docs/06-lad-conventions.md`; no phantom or mismatched rule IDs.
- **F-14 (low) — `extract/` documents a test suite that doesn't exist.** `extract/README.md` states
  "Run: `pytest tests/`", but `extract/` contains only that README (S5 Python extractor not yet built) —
  reads as present-tense capability for a placeholder.

### D4 — Code quality & architecture (PC-side tooling only)

- *Positives (clean bill):* zero bare `throw new Exception`/`InvalidOperationException` as domain errors
  (21 purpose-built `sealed` exception types; 19/20 broad `catch` blocks type-filtered, the one unfiltered
  catch justified inline at `OpennessGateway.cs:154`); zero real `TODO`/`FIXME`/`HACK` markers; zero
  skipped/ignored tests; the runtime-critical **net48 pin is intact and documented at its point of use**
  (`src/openness-cli/Directory.Build.props:4,9–13`); a clean 15-block matched `ir/reference` ↔
  `simatic-ml/reference` pair.
- **F-05 (medium) — the live-TIA regression gap persists (carry-forward from 2026-07-14).** The two
  live-cycle proofs (`SynthesizerLiveCheck`, `ReferenceProjectRoundTrip`) are un-automated `static`
  classes never exercised by the baseline run; the arithmetic/convert construct family and TOF/TONR have
  no named committed golden block (proven only via unit fixtures + point-in-time live narrative); and 3
  IR-only `test-project001` blocks (the `HopperBlockageMonitor` family) have no matching `.xml`, so
  `CommittedBlocksRoundTripTests` silently excludes them from the round-trip theory.
- **F-10 (low) — build config has no single source of truth, no CI.** The four test-package pins
  (`Microsoft.NET.Test.Sdk` 17.8.0, `xunit`/`xunit.runner.visualstudio` 2.5.3, `coverlet` 6.0.0) are
  duplicated verbatim across all three test `.csproj`; the golden harness pins `net8.0` inline with no
  shared `Directory.Build.props`; there is no CI (`.github/` absent). A version/framework drift would be
  silent.
- **F-11 (low, watch → now actionable) — `PartNode` and `DbMember` both at 14 fields.** `PartNode`
  (`SimaticMl/Model.cs:136–150`) grew 13→14 (added `Equation`); `DbMember` (`SimaticMl/DbModel.cs:101–115`)
  grew 10→14 since 2026-07-14 — the "kitchen-sink" trend the prior audit flagged as watch-this has
  continued in both records (D7 carry-forward).

### D5 — Compliance (in scope this round)

- **F-19 (medium, decide) — Method-rule-7 leak grep: real-restricted identifiers committed in Green-tier
  content.** `git grep "Tom White"` → 10 tracked files (evidence docs, ADR-0001, risk register, both
  READMEs, quirks); `git grep "JOB9002"` → 50+ tracked files. Per `docs/13-data-boundary.md` this is an
  **approved, deliberate** presence (the JOB9002 approval commits the real name), so it is **not an
  accidental leak** — but the charter requires flagging it, and whether the recorded approval *scope*
  actually covers everything that happened remains the standing owner call (see F-04, D7).
- *Positive — hard-rule adherence reads clean from process artefacts.* The generation telemetry shows the
  discipline the rules require: `manual:` stage prefixes, preflight/compile gates run, repeated "no IR
  edits" / "not committed" / "report-only", scratch-only work. No hard-rule violation surfaced.
- **F-17 (low, decide) — the data-boundary decision is still open.** `docs/13-data-boundary.md` remains
  **DRAFT** ("needs a decision… before real project data flows to any AI"), with ADR-0003 reserved for it —
  while the cloud pipeline is in active use against approved Amber data. Confirm the reservation is still a
  deliberate hold, not an aging forgotten decision.

### D6 — Cross-reference / dead-link integrity

- *Positive:* no live citations to the deleted `a4`/`d4`/`fix-wave-1-reviews.md` briefings (every mention
  correctly says "retired"); no references into the non-existent `docs/evidence/stage-S{5,7,8,9}.md`.
- **F-09 (low, decide/hand-off) — dangling `gen/GenProject1/fix-wave-1.md` path in an export artefact.**
  `simatic-ml/test-project001/DB_Settings.xml:288` carries a comment referencing `gen/GenProject1/fix-wave-1.md`;
  the directory is now `gen/test-project001/`, a rename residue. Reported as a mechanical grep fact (not a
  ladder judgement). Note: this lives in raw SimaticML — per hard rule 7 it must **not** be hand-patched; the
  correct fix is a re-export, so this is a hand-off, not a mechanical edit. (`gen/test-project001/telemetry.log:4`
  carries the same stale path but is append-only by convention — immutable, accept.)

### D7 — Prior-findings follow-through

See the status table below. Both founding audits' inline fixes still hold (spot-checked: `CLAUDE.md`'s
Commands block still lists the full subcommand set and the ~20-construct converter description — the
2026-07-14 #4 fix is in place).

| Prior item (2026-07-14 unless noted) | Status | Evidence |
|---|---|---|
| #5 JOB9002 approval-scope record | **substantially addressed** (adequacy = `decide`, F-04/F-19) | backfilled `docs/13:41–56` + addenda through 2026-07-20 |
| `Normalizer` Part-UId-volatility gap | **partially resolved** | specific volatility bugs fixed (`ElementsWithVolatileId`, Call-producers, CompileUnit ID — `converter-synthesis-gaps.md:142–144`, `NormalizerTests`); residual Parts flow-order blind spot open, tracked as an FI (`docs/16:454–458`) |
| `PartNode`/`DbMember` restructure decision | **still open** (F-11) | now 14 fields each (was 13/10) |
| Grow committed reference corpus (live-TIA gap) | **partially addressed** (F-05) | `ir/reference` grew 7→15 blocks, but still skews alarms/timers/comms; arithmetic/convert + TOF/TONR uncovered |
| 2026-07-11 #1/#2 inline fixes | **hold** | `stage-gates.md` S1 row + Phase B section present |

### D8 — AI-operational surface

- *Positives:* the `docs/15` build-order table (`:194–206`) matches the filesystem exactly — the 7 built
  pipeline skills exist, the 8 "Not built" stages have no skill dir; the `lad-coder` contract is coherent
  with hard rule 8; **both hookify enforcers verified coherent** — the safety `\bF_` pattern matches
  `SafetyClassifier.cs:12` (`SafetyPrefix = "F_"`), and the SimaticML `\.xml$` block's `Converter`
  carve-out leaves all 67 current fixtures correctly editable and no stray `.xml` wrongly blocked.
- **F-07 (low) — a telemetry row breaks the `manual:` convention / contradicts the build table.**
  `gen/test-project001/telemetry.log:23` logs a `gen-integration` run **without** the `manual:` prefix that
  `docs/notes/gen-telemetry.md` mandates for not-yet-built stages — while `docs/15:204` marks
  `gen-integration` "Not built". Every other manual row on that log uses `manual:` correctly, making line 23
  the outlier. Either the run mislabelled an unbuilt stage or the build table is stale.
- **F-08 (low) — telemetry convention doc says "not adopted yet" while in active use.**
  `docs/notes/gen-telemetry.md:1–3` opens "A proposed format, not adopted pipeline behavior yet", yet two
  telemetry logs are actively maintained through 2026-07-20 (`gen/test-project001/`,
  `gen/_validation/MotorVSDSystem-purpose/`).
- **F-16 (low, accept) — the `.xml` hook carve-out is a fragile substring heuristic.** The rule-7 enforcer
  keys its carve-out on the literal path substring `Converter`; a future `.xml` fixture added outside a
  path containing that exact string (e.g. under `src/openness-cli/…` or a lowercase `fixtures/`) would be
  silently blocked. Coherence-risk, not a current defect.

### D9 — Governance-record currency

- **F-01 (medium) — the ADR index omits the two newest accepted ADRs.** `docs/00-README.md:25` lists the
  ADR set as "`adr-0000…0004`", but `adr-0005-derive-always-sidecar.md` (**Accepted 2026-07-18**) and
  `adr-0006-fanout-annotation.md` (**Accepted 2026-07-19**) both exist and are cited across the repo. The
  suite index is behind the accepted-decision backbone.
- **F-04 (medium, decide) — the risk register doesn't reflect materialised risks.**
  `docs/09-risk-register.md:3` claims "Reviewed at every stage gate", but the table shows original L/I
  ratings with no per-gate review trail; **R-08** (cloud-AI data policy) and **R-11** (solo-project bus
  factor) are live realities still listed prospectively, and assumption **A-04** ("Claude Code within IT
  policy… *verify — ties to R-08*") is unverified while the cloud pipeline runs. Needs an owner risk review.
- **F-06** (ADR-0005 "Proposed" in `synthesis-parity-plan.md`) — see D2.
- ADR-0003 reserved slot: confirmed still deliberate (`adr-0004:5–7`, `docs/13:17`) — folded into F-17.

### D10 — Repository & git hygiene

- *Positive:* working tree clean; no git remote (deliberate local-only posture — not flagged).
- **F-12 (low) — two orphan branches.** `_master_sync` (`ba21079` "Claim Portal queue slot 3") and
  `_master_sync2` (`9324685` "Claim Portal queue slot 4") are leftovers from a parallel-dispatch queue
  experiment, not in `master`'s line. Candidates for deletion after confirming nothing unique rides on them.
- **F-13 (low, decide) — the audit-charter milestone has no CHANGELOG entry.** `CHANGELOG.md` has no entry
  for the charter work (the 4 "audit" hits all reference *other* audits) — the recurring missing-milestone
  class both prior audits flagged. Nuance: the charter commits sit on the unmerged worktree branch, so the
  entry may simply be pending merge; and audit artefacts have never been changelogged, which may be an
  intentional meta-exclusion. An owner call on the convention.
- **F-18 (low, accept) — historical direct-to-master subagent commit.** `c6744c2`
  ("…Track 2; subagent committed to master") is a self-recorded instance of the workflow smell the charter
  names; it is already in `master`'s ancestry — nothing to fix retroactively, noted for process awareness.

## Outcome + process recommendations

The project is in good shape: **healthy tooling, a fully green 830-test baseline, and no correctness
defects.** The audit surfaced 19 items, none a blocker — overwhelmingly *records lagging shipped work*
(stale status lines, an out-of-date ADR index, an overdue risk review). The single most consequential
cluster is **governance currency** (F-01, F-04, F-17, F-19): the ADR index, risk register, and
data-boundary decision have drifted behind a project that is now actively running a cloud pipeline against
approved real-restricted data — worth an owner governance pass independent of the mechanical doc fixes.

Process recommendation, reinforcing both prior audits: the **status-layer-vs-narrative-log drift** remains
this project's dominant failure mode (F-02, F-03, F-06 all instances). The charter's whole-file-reconcile
rule holds; the highest-leverage structural mitigation would be to make the *implemented* status of an FI /
rule / ADR derive from one authoritative place (or a mechanical check) rather than being re-stated by hand
in `stage-gates.md`, `AITODO.md`, `docs/16`, and the ADR index independently.

**Test baseline re-statement (read-only run, nothing changed):** converter 671 · openness-cli 122 ·
golden-harness 37 = **830 passed, 0 failed, 0 skipped**.
