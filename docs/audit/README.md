# Audit requirements — Ladder-AI

The standing charter for periodic audits of this project. An audit is a **deliberate, owner-requested
health check**, run occasionally (not on every change), that verifies the project's documented picture
of itself against what the code, tests, and git history actually say — and, when requested, reviews code
quality and compliance. Each audit produces one dated report in this folder.

This document formalises **what an audit may cover, how it is run, and what it must produce**. It is
derived from the two audits that predate it, whose criteria it generalises:

- [`2026-07-11-stage-and-docs-audit.md`](2026-07-11-stage-and-docs-audit.md) — full doc-suite read for
  stage/goals accuracy and internal consistency.
- [`2026-07-14-code-quality-and-docs-audit.md`](2026-07-14-code-quality-and-docs-audit.md) — docs-vs-code
  accuracy plus a code-quality & architecture review.

## How an audit is scoped

**The owner picks the scope from the dimension menu below; the auditor does not silently expand it.**
This is how the 2026-07-14 audit ran — the owner chose two of the offered dimensions and explicitly
excluded hard-rule/data-boundary compliance that round. State the chosen dimensions at the top of the
report, and name anything offered-but-excluded so the exclusion is on the record, not an oversight.

Each report also states its **baseline**: which prior audit it picks up from (so the doc suite is covered
continuously, not re-read from zero each time) and what had shipped since.

## Audit dimensions (the criteria)

An audit covers one or more of these. Each names what it checks and the concrete failure modes the prior
audits actually found, so a future auditor knows what "done" looks like.

### D1 — Stage & goals accuracy
Does the project's own record of *what stage it is in and what is / isn't in scope now* match reality?
- Cross-check `docs/notes/stage-gates.md`, `docs/02-roadmap.md`, `AITODO.md`, and the evidence docs
  (`docs/evidence/stage-S*.md`) against the code and tests actually present.
- Confirm stage entry/exit criteria are evidenced where claimed, and that gate sign-offs that are still
  open are marked as *deliberately* open, not stale.
- **Known failure mode:** a summary-table row describing a blocker the narrative below it already recorded
  as resolved (2026-07-11 finding #1); a "recovery point" doc (`AITODO.md`) describing in-progress work
  that was in fact finished and committed (2026-07-14 finding #1).

### D2 — Doc internal consistency
Do the docs agree with **each other** and with themselves?
- Read the relevant doc suite end to end. Flag any two statements — across files *or within one file* —
  that disagree about the same fact.
- **Known failure mode — the recurring one, always check it:** a file that carries both a "current status"
  layer (a summary table, a grammar sketch) *and* a chronological "open items" / narrative log drifts,
  because an update lands in whichever section the author was touching and the other silently goes stale.
  Confirmed across `stage-gates.md` and `ir/SPEC.md` (2026-07-11 #1, 2026-07-14 #3). `ir/SPEC.md` even
  contradicted itself directly (line 136 vs line 878 on the same construct). **Method:** whenever a claim
  is corrected, grep the whole file for the construct/feature name and reconcile every mention, not just
  the one you landed on.

### D3 — Docs-vs-code accuracy
Are specific, checkable doc claims true against the code/tests/git history right now?
- Pick claims that are cheap to falsify — "feature X is out of scope", "converter is Contact/Coil-only",
  "current task is Y" — and check each against source, the test suites, and `git log`.
- Watch especially: **built-but-undocumented** features (real, tested, live-verified, but absent from the
  reference docs) and **documented-but-superseded** claims left behind after the work moved on.
- **Known failure mode:** `CHANGELOG.md` missing an entire milestone's entries (2026-07-11 #2 in miniature,
  2026-07-14 #2 at scale — flagged as *recurring*); `CLAUDE.md`'s own Commands block describing a
  walking-skeleton-era converter ~20 constructs behind reality; a real feature (`--tagtable`) undocumented
  in both `CLAUDE.md` and the README whose stated job is to be the flag reference (2026-07-14 #4).
  `CLAUDE.md` is the highest-value target here — it loads as instructions every session.

### D4 — Code quality & architecture
Is the PC-side code (`src/converter/`, `src/openness-cli/`, `extract/`, `tests/golden/`) healthy? Normal
software rules apply here — hard rules 1–8 are about PLC logic, not this tooling.
- Exception discipline (purpose-built types, no bare `throw new Exception`, justified broad `catch`es),
  compiler warnings (target zero), dead-code / `TODO`/`FIXME` markers, file-size and test-suite growth.
- Type/design health — e.g. the deliberately-generic "kitchen sink" records (`PartNode`, `DbMember`)
  flagged 2026-07-14 as a *watch-this*, not a fix-now: worth re-checking whether the optional-field count
  has grown enough to justify a discriminated union.
- Test-coverage gaps, especially the **live-TIA regression gap**: constructs proven only against Amber-tier
  content that can never be committed, so the proof survives only as narrative in `stage-gates.md`. Report
  which supported constructs still have no committed golden-corpus coverage.
- Report positives too (the prior audit did) — a clean bill on a dimension is a finding worth recording.

### D5 — Compliance (opt-in; not run unless requested)
Explicitly **excluded** from 2026-07-14 by owner choice; listed here so it is a deliberate pick, never a
default assumption.
- **Hard-rule compliance** — evidence that the 8 hard rules in `CLAUDE.md` held (LAD-only; safety untouched;
  no invented tags/addresses; compile gate before "done"; review not bypassed; no hardware access; IR-not-XML
  edits; all LAD/IR work dispatched to `lad-coder`).
- **Data-boundary compliance** — Amber-tier usage stayed inside a recorded per-project approval in
  `docs/13-data-boundary.md`; no identifying data outside an approval. Note that 2026-07-14 #5
  *flagged* a data-governance scope question here but, correctly, did not resolve it unilaterally — see the
  fixed-vs-flagged rule below.

## Method — standing rules for every audit

1. **Bracket with green tests.** Confirm all three PC-side suites pass *before* starting and *after* any
   change (`dotnet test`: converter, openness-cli, golden-harness; state the counts). A doc-only pass still
   re-confirms them.
2. **Verify empirically; don't trust either side of a contradiction.** When docs disagree with each other or
   with code, run the tests / read the source / check `git log` and let the result decide — as 2026-07-11 #2
   did by running all three suites rather than believing the doc *or* the diff.
3. **Reconcile whole files, not single lines** (the D2 grep rule) — the single most repeated failure mode.
4. **Ground claims against the artefact, not memory** — grep the real export / source at check time.
5. **Stay in lane on PLC content.** An audit reads and reasons about docs, tooling code, tests, and git
   history directly. It does **not** read, write, or explain LAD/IR content itself — if a finding needs a
   block opened or explained, that is dispatched to `lad-coder` (hard rule 8), same as any other work.

## Output contract — what an audit produces

One report named `docs/audit/YYYY-MM-DD-<short-scope>.md`, containing:

- **Header:** who requested it, the chosen scope (dimensions D1–D5), anything offered-but-excluded, and the
  baseline (which prior audit it continues + what shipped since).
- **Findings**, grouped by dimension, each stating what was checked, what was found, and — critically —
  which of the two dispositions it got:
  - **Fixed directly** — only safe, mechanical corrections with no design or governance decision involved
    (stale text, missing changelog entries, self-contradictions). Say exactly what changed and cite the
    commit if one was made.
  - **Flagged, not fixed** — anything requiring an owner decision: design trade-offs, data-governance
    records, scope calls. The auditor surfaces these, never resolves them unilaterally (2026-07-14 #5 is
    the template: a compliance-relevant scope entry left for the owner to write).
- **Positives / clean-bill notes** — dimensions checked with no issue found are recorded, not omitted.
- **Outcome + process recommendations** — a short close-out, plus any standing-habit recommendation the
  round surfaced (e.g. the whole-file reconcile rule, promoted here from 2026-07-14's recommendation).
- **Re-confirm tests green** at the end and state the counts.

## Record of audits

| Date | Scope | Report |
|------|-------|--------|
| 2026-07-11 | D1 stage/goals + D2 doc consistency (full suite → S1 item 7) | [`2026-07-11-stage-and-docs-audit.md`](2026-07-11-stage-and-docs-audit.md) |
| 2026-07-14 | D3 docs-vs-code + D4 code quality/architecture (D5 excluded by owner) | [`2026-07-14-code-quality-and-docs-audit.md`](2026-07-14-code-quality-and-docs-audit.md) |

Add a row here whenever a new audit lands.
