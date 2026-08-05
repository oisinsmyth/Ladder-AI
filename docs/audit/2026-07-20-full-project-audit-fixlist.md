# Fix list — 2026-07-20 full project audit

> ## ⚠️ STATUS: UNIMPLEMENTED — OUT OF DATE (marked 2026-08-05)
>
> **None of the 19 items below were ever executed, and the project has since changed enough that this list
> should not be worked through as-is.** It is retained as the record of what the 2026-07-20 audit
> recommended.
>
> Since that audit, master gained the four-rung spec pipeline (rungs A–D), the FI-36/FI-39 mechanical-floor
> tooling, an FI renumbering (FI-32..35 → FI-36..39), a `tagstatus` hard-rule-3 fix, and a data-boundary
> change (full Red-confidentiality access, zero retention) — which between them invalidate or move the
> premises of several rows, notably **F-03** (FI status), **F-17/F-19** (data boundary), and the D8-derived
> rows **F-07/F-16** (skill inventory).
>
> Some rows are still likely valid — mostly the small doc corrections (F-01, F-06, F-08, F-14, F-15) and the
> git/build hygiene items (F-10, F-12) — but **re-verify each against current `master` before acting**.
> Preferred path: run a fresh audit and let it re-derive the list. See the status banner on the report for
> the full picture.

Actionable companion to `2026-07-20-full-project-audit.md`. Ranked worst-first. **The audit applied none of
these** (read-only, owner chose recommend-only) — each row is a turnkey recommendation for a later,
separate step. Disposition: **fix** = mechanical, safe, no decision needed · **decide** = needs an owner
call · **accept** = acknowledged / deferred, listed for the record.

**Totals:** 19 items — 0 blocker · 6 medium · 13 low · (9 fix · 8 decide · 2 accept).

| ID | Sev | Dim | Problem | Recommended action | Disp. | Effort / risk |
|----|-----|-----|---------|--------------------|-------|---------------|
| F-01 | medium | D9 | `docs/00-README.md:25` ADR index says "`adr-0000…0004`"; ADR-0005 (Accepted) & 0006 (Accepted) exist and are cited repo-wide. | Update the ADR index row in `docs/00-README.md:25` to cover 0000–0006 (add derive-always-sidecar + fanout-annotation, both Accepted). | fix | trivial / none |
| F-02 | medium | D1 | "8 of ~50 rules" in `stage-gates.md:13` & `AITODO.md:35` understates the 24 C-IDs now in `Rules.cs`; `AITODO.md:269` already contradicts line 35. | In both files, restate the mechanized-rule count to reflect the FI-09 additions (24 C-IDs), or reframe the "8" as the dated S4-Phase-1 sign-off figure and note FI-09 extended it. Reconcile `AITODO.md` lines 35 & 269. | fix | small / none |
| F-03 | medium | D1/D2 | `AITODO.md:262–274` lists FI-17/22/23/26–30 as open/awaiting-decision; `docs/16` marks all IMPLEMENTED 2026-07-20 (subcommands live in `CLAUDE.md:46–51`). | Move those FI IDs out of AITODO's "awaiting a decision" round-up into a closed/implemented state to match `docs/16`. | fix | small / none |
| F-04 | medium | D9 | Risk register (`docs/09`) claims per-gate review but shows no trail; R-08 & R-11 are materialised yet listed prospectively; A-04 unverified while cloud pipeline is live. | Owner risk review: mark R-08/R-11 materialised (or re-rate), record a review date, and resolve/verify A-04. | decide | owner review / governance |
| F-05 | medium | D4/D7 | Live-TIA proofs (`SynthesizerLiveCheck`, `ReferenceProjectRoundTrip`) are un-automated; arithmetic/convert + TOF/TONR families and 3 IR-only Hopper blocks have no committed golden round-trip coverage. | Decide whether to grow `ir/reference`+`simatic-ml/reference` with genericized blocks covering the uncovered construct families, and/or wire an automated live-cycle gate. Carry-forward from 2026-07-14. | decide | medium / needs live TIA + genericization |
| F-19 | medium | D5 | Method-7 leak grep: "Tom White" in 10 tracked files, "JOB9002" in 50+ — real-restricted identifiers in Green-tier content (approved per `docs/13`, not an accidental leak). | Confirm the recorded `docs/13` approval scope actually covers all committed occurrences; decide if any should be genericized. Pairs with F-04. | decide | owner call / data governance |
| F-06 | low | D9/D2 | `synthesis-parity-plan.md` calls ADR-0005 "Proposed" (lines 111, 141) while line 121 says "Accepted"; actual status is Accepted. | Update lines 111 & 141 to "Accepted (owner, 2026-07-18)"; reconcile with line 121. | fix | trivial / none |
| F-07 | low | D8 | `gen/test-project001/telemetry.log:23` logs `gen-integration` without the mandated `manual:` prefix while `docs/15:204` marks it "Not built". | Reconcile: if `gen-integration` is still unbuilt, the build table is right and the log row should have been `manual:` (telemetry is append-only — note the correction rather than editing the log); if it is effectively built/adopted, update `docs/15:204`. | decide | small / convention call |
| F-08 | low | D8 | `docs/notes/gen-telemetry.md:1–3` says "a proposed format, not adopted yet" while two logs are actively maintained through 2026-07-20. | Update the doc's opening to reflect that the format is in active use (adopted). | fix | trivial / none |
| F-09 | low | D6 | `simatic-ml/test-project001/DB_Settings.xml:288` references `gen/GenProject1/fix-wave-1.md` (dir is now `gen/test-project001/`) — rename residue in raw SimaticML. | Do **not** hand-patch the XML (hard rule 7). Fix at source (regenerate the comment / re-export the block), or accept as a frozen-export residue. | decide | small / hand-off (re-export) |
| F-10 | low | D4 | Test-package pins duplicated across 3 `.csproj`; golden harness pins net8.0 inline with no shared props; no CI. | Centralise the four test-package versions (and optionally the net8.0 pin) into a shared `Directory.Build.props`; separately, decide whether the no-CI posture stays (deliberate for a local solo project). | fix | small / low |
| F-11 | low | D4/D7 | `PartNode` (14 fields, was 13) and `DbMember` (14, was 10) — the kitchen-sink growth the 2026-07-14 watch-item predicted. | Decide whether the optional-field count now justifies a discriminated union / per-kind subtypes, or record another explicit "watch, not yet" with the current counts. | decide | medium if restructured / design call |
| F-12 | low | D10 | Orphan branches `_master_sync` (`ba21079`) & `_master_sync2` (`9324685`) — leftover Portal-queue-slot commits, not in master's line. | Confirm nothing unique rides on them (`git log master..`), then delete both. | fix | trivial / low (verify first) |
| F-13 | low | D10 | No CHANGELOG entry for the audit-charter milestone (recurring missing-milestone class). | Decide the convention: either add a CHANGELOG entry for the charter (likely on/after the worktree branch merges), or record that audit artefacts are intentionally not changelogged. | decide | trivial / convention call |
| F-14 | low | D3 | `extract/README.md` says "Run: `pytest tests/`" but `extract/` is an empty S5 placeholder (no code, no tests). | Reword the README to mark it a not-yet-built placeholder (future-tense), or add the stub test dir when S5 starts. | fix | trivial / none |
| F-15 | low | D1 | `docs/00-README.md:33` "immediate to-dos" still says verify A-01/A-02; both VERIFIED 2026-07-10 (`docs/09:22–23`). | Update or drop the completed to-do in `docs/00-README.md:30–33`. | fix | trivial / none |
| F-16 | low | D8 | Rule-7 `.xml` hook carve-out keys on the literal substring `Converter`; a future fixture outside such a path would be silently blocked. | Note the coherence-risk near the hook / in a test-fixture-placement convention; optionally tighten the carve-out to the exact fixtures dir. Not a current defect. | accept | low / watch |
| F-17 | low | D5/D9 | `docs/13-data-boundary.md` still DRAFT (ADR-0003 reserved) while the cloud pipeline runs against approved Amber data. | Confirm the reservation is a deliberate hold; when ready, take the data-boundary decision and record ADR-0003. | decide | owner decision / standing |
| F-18 | low | D10 | `c6744c2` — a self-recorded subagent commit directly to master (workflow smell), already in ancestry. | Nothing to fix retroactively; keep the parallel-dispatch discipline of not committing subagent work directly to master going forward. | accept | none / process note |

If a later step executes any of these, the **next** audit's D7 verifies them against this list.
