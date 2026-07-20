# Audit requirements — Ladder-AI

The standing charter for periodic audits of this project. An audit is a **deliberate, owner-requested
health check**, run occasionally (not on every change), that gives a strong overview of the project's
state — verifying its documented picture of itself against what the code, tests, and git history actually
say, and, when requested, reviewing tooling quality, process, and compliance. It is **read-only**: it
changes nothing in the repo, and produces two dated files in this folder — a findings report and an
actionable fix list.

This document formalises **what an audit may cover, how it is run, and what it must produce**. It is
derived from the two audits that predate it, whose criteria it generalises:

- [`2026-07-11-stage-and-docs-audit.md`](2026-07-11-stage-and-docs-audit.md) — full doc-suite read for
  stage/goals accuracy and internal consistency.
- [`2026-07-14-code-quality-and-docs-audit.md`](2026-07-14-code-quality-and-docs-audit.md) — docs-vs-code
  accuracy plus a code-quality & architecture review.

## Scope boundary — this is a *project* audit, not a ladder-code audit

**An audit examines the health of the project: its docs, its PC-side tooling, its process, and the
AI-operational layer that produces ladder logic. It does not review ladder logic itself.**

LAD/IR content is reviewed **continuously during development** by the dedicated review skills —
`review-functional`, `review-conventions`, `review-simplicity` — running in fresh adversarial context per
`docs/11-review-workflow.md` and the `docs/15` check stage. That is where ladder correctness, convention
compliance, and simplicity are judged. An audit never re-does that work and **never opens a LAD/IR block
to read or judge it**.

The one carve-out — the "good reason" to touch the corpus at all: an audit may cite **mechanical,
tooling-derived facts *about* the corpus** as evidence for a project-level claim — e.g. whether a tag
exists in the export (`converter tagstatus`), whether blocks compiled (`openness-cli compile`), or a
`converter diff`/`digest`/`drift-check` count. That is reading the *tooling's output*, not reading or
reasoning about ladder logic. The moment a finding would require a block to be **read, explained, or
semantically reviewed**, it is out of the audit's lane: dispatch it to `lad-coder` / the review skills
(hard rule 8), and record it as a hand-off, not an audit finding.

## The audit is read-only

**An audit never modifies the repository — no edits, no fixes, no commits.** It reads, verifies, and
reports. This is a change from the two founding audits, which fixed mechanical issues inline; going forward
the auditor and the fixer are kept separate, so the thing that *judges* the project is never also the thing
that *changes* it (auditor independence — and it means an audit can be run at any time without disturbing
in-flight work).

Concretely: the only things an audit runs are read/observe actions — reading files, `grep`, `git log`,
`dotnet test`, and the read-only tooling commands (`converter tagstatus`/`diff`/`digest`/`drift-check`,
`openness-cli list`/`compile`/`sanity-check`/`portal-status`). It writes **only** its own two output files
under `docs/audit/` (the report and the fix list). It does not touch the code, docs, or IR it is auditing.

Every issue the audit finds becomes an entry on the **fix list** (its own file — see the output contract),
with a precise recommended action. Actually applying those fixes is a **separate, later step** — done by
the owner or a follow-up working session against the fix list — explicitly outside the audit itself.

## How an audit is scoped

**The owner picks the scope from the dimension menu below; the auditor does not silently expand it.**
This is how the 2026-07-14 audit ran — the owner chose two of the offered dimensions and explicitly
excluded compliance auditing that round. State the chosen dimensions at the top of the report, and name
anything offered-but-excluded so the exclusion is on the record, not an oversight.

Each report also states its **prior-audit baseline**: which earlier audit it picks up from (so the doc
suite is covered continuously, not re-read from zero each time) and what had shipped since. ("Baseline" is
also used in one other, distinct sense — the *test baseline*, the recorded green test counts at audit time;
see Method rule 1. The two are unrelated.)

## Audit dimensions (the criteria)

An audit covers one or more of these. Each names what it checks and the concrete failure modes the prior
audits actually found, so a future auditor knows what "done" looks like. None of them involve reading
ladder logic (see the scope boundary above).

### D1 — Stage & goals accuracy
Does the project's own record of *what stage it is in and what is / isn't in scope now* match reality?
- Cross-check `docs/notes/stage-gates.md`, `docs/02-roadmap.md`, `AITODO.md`, and the evidence docs
  (`docs/evidence/stage-S*.md`) against the code and tests actually present.
- Confirm stage entry/exit criteria are evidenced where claimed, and that gate sign-offs that are still
  open are marked as *deliberately* open, not stale.
- **FI-backlog accuracy:** recent history is dominated by future-ideas work, and `docs/16-future-ideas.md`
  carries per-item verdicts and "backlog cleared" claims. Confirm each FI item's stated status
  (proposed / accepted / built / cleared) matches the code and `CHANGELOG.md` — a "cleared" item with no
  landing evidence, or a built one still shown as proposed, is the same drift class as a stale roadmap.
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
  "current task is Y" — and check each against source, the test suites, and `git log`. Sample; don't
  exhaust (see the time-box rule under Method).
- Watch especially: **built-but-undocumented** features (real, tested, live-verified, but absent from the
  reference docs) and **documented-but-superseded** claims left behind after the work moved on.
- **Convention rule-ID parity:** `converter review` (`Rules.cs`) implements a subset of `docs/06`'s
  C-rules. Check that rule IDs in code match `docs/06`, and that rules documented but not yet implemented
  are marked as such rather than implied-complete. Conventions are core IP — this slice is worth an
  explicit pass.
- **Known failure mode:** `CHANGELOG.md` missing an entire milestone's entries (2026-07-11 #2 in miniature,
  2026-07-14 #2 at scale — flagged as *recurring*); `CLAUDE.md`'s own Commands block describing a
  walking-skeleton-era converter ~20 constructs behind reality; a real feature (`--tagtable`) undocumented
  in both `CLAUDE.md` and the README whose stated job is to be the flag reference (2026-07-14 #4).
  `CLAUDE.md` is the highest-value target here — it loads as instructions every session.

### D4 — Code quality & architecture (PC-side tooling only)
Is the PC-side code (`src/converter/`, `src/openness-cli/`, `extract/`, `tests/golden/`) healthy? Normal
software rules apply here — hard rules 1–8 are about PLC logic, not this tooling, and this dimension never
touches ladder.
- Exception discipline (purpose-built types, no bare `throw new Exception`, justified broad `catch`es),
  compiler warnings (target zero), dead-code / `TODO`/`FIXME` markers, file-size and test-suite growth.
- Type/design health — e.g. the deliberately-generic "kitchen sink" records (`PartNode`, `DbMember`)
  flagged 2026-07-14 as a *watch-this*, not a fix-now: worth re-checking whether the optional-field count
  has grown enough to justify a discriminated union.
- Test-coverage gaps, especially the **live-TIA regression gap**: constructs proven only against Amber-tier
  content that can never be committed, so the proof survives only as narrative in `stage-gates.md`. Report
  which supported constructs still have no committed golden-corpus coverage. (This reports *coverage of the
  tooling*, derived from test/fixture inventory — it does not read the ladder those tests exercise.)
- **Build-environment reproducibility:** the `net48`-vs-modern-.NET split is load-bearing (modern .NET
  "builds fine but fails at runtime" against `Siemens.Engineering.dll`, per `CLAUDE.md`); that DLL is a
  machine-local, unversioned dependency; package pins live in `Directory.Build.props`. With no CI to catch
  a regression, confirm the target frameworks and pins are intact and the runtime-critical `net48`
  constraint is still honoured where it matters (R-02/R-12 in the risk register flag this).
- Report positives too (the prior audit did) — a clean bill on a dimension is a finding worth recording.

### D5 — Compliance (opt-in; not run unless requested)
Explicitly **excluded** from 2026-07-14 by owner choice; listed here so it is a deliberate pick, never a
default assumption.
- **Hard-rule compliance** — evidence that the 8 hard rules in `CLAUDE.md` held (LAD-only; safety untouched;
  no invented tags/addresses; compile gate before "done"; review not bypassed; no hardware access; IR-not-XML
  edits; all LAD/IR work dispatched to `lad-coder`). This is checked from process artefacts — telemetry,
  git history, dispatch records — not by reading the ladder that resulted.
- **Data-boundary compliance** — Amber-tier usage stayed inside a recorded per-project approval in
  `docs/13-data-boundary.md`; no identifying data outside an approval. Note that 2026-07-14 #5
  *flagged* a data-governance scope question here but, correctly, did not resolve it unilaterally — such
  items become **decide**-disposition entries on the fix list, never actioned by the audit. (The cheap
  mechanical *leak grep* is not gated here — it runs every audit as Method rule 7; D5 is the deeper judgement
  of whether recorded approval scope actually covers what happened.)

### D6 — Cross-reference / dead-link integrity
Do the project's internal pointers still resolve? Distinct from D2: D2 checks whether *claims* agree; this
checks whether *references* are live.
- `CLAUDE.md` warns that this repo "cite[s] each other by literal file path constantly… after deleting or
  renaming any doc/file, grep the old filename repo-wide." A rename-only pass (bulk `sed`) won't catch
  dangling pointers left by deletions.
- Sweep for: dangling `docs/NN-*.md` and `docs/notes/*.md` citations, references to deleted or renamed
  files, stale skill / sub-agent / rule-ID / FI-item / ADR names, and section-anchor citations
  (`… §N`) that no longer exist.
- **Why now:** recent history is heavy with renames and deletions (FI churn, the `test-project001`
  de-identification) — exactly the conditions that break literal-path citations.

### D7 — Prior-findings follow-through
Did the *previous* audit's outcomes actually land? A meta-check the earlier audits couldn't run because
nothing preceded them.
- Walk the prior audit's **fix list** and classify each item as: done (the recommended action was executed) /
  still-open / silently dropped. For the two founding audits, which pre-date the fix-list format and applied
  mechanical fixes inline, walk their **Flagged, not fixed** items the same way and confirm their inline
  **Fixed directly** changes are still in place and weren't quietly reverted.
- Standing carry-forward items to track until closed: `docs/13-data-boundary.md`'s JOB9002 approval-scope
  record (2026-07-14 #5), the `Normalizer` Part-UId-volatility gap, the committed reference-corpus growth
  for live-TIA coverage, and any `PartNode`/`DbMember` restructure decision.

### D8 — AI-operational surface
The deliverable is "an AI capable of programming ladder logic," so the layer that *produces* ladder is
itself a first-class audit target — auditing this layer is not the same as reading ladder.
- **Skill inventory vs docs:** do the skills the docs reference actually exist in `.claude/skills/`, and do
  their descriptions match what they do? Does `docs/15`'s build-order table match the real skill +
  `lad-coder` inventory, including which stages are "built" vs "performed manually to contract"?
- **Sub-agent coherence:** `.claude/agents/lad-coder.md`'s stated contract still matches the workflow in
  `CLAUDE.md` and `docs/15`.
- **Hard-rule coherence:** the 8 hard rules still describe the tooling as it now is (e.g. commands they
  reference exist; the dispatch model in rule 8 matches the actual skill/agent set) — a coherence check on
  the rules' text, separate from D5's compliance-with-them.
- **Enforcement-hook coherence:** the committed hookify rules that *mechanically* enforce the hard rules —
  the safety-F-block-content block (rule 2) and the SimaticML-edit block (rule 7) — still match the rule
  they guard and the code they reference. These back the two most integrity-critical hard rules via regex
  and source assumptions (e.g. a safety-prefix classifier, a converter-path carve-out); if that underlying
  logic changes, the enforcer silently diverges. This extends the text-only "hard-rule coherence" check to
  the enforcers themselves.
- **Telemetry / process artefacts:** the generation-pipeline telemetry convention (`docs/notes/gen-telemetry.md`,
  `gen/<project>/telemetry.log`) is being followed where the pipeline claims to run.

### D9 — Governance-record currency (ADRs, risk register, assumption log)
Are the project's *decision and risk* records still true, and are their self-declared obligations met?
Distinct from D6 (which only checks these files are linkable) and D1 (stage/goals): this checks the
*content* of the governance backbone.
- **ADRs (`docs/adr/`):** each *accepted* ADR still describes the system as built; *reserved* or
  *superseded* slots are resolved or still-deliberately-open (e.g. ADR-0003 is reserved for the
  data-boundary decision — confirm it's still intentionally pending, not forgotten); and the ADR index in
  `docs/00-README.md` lists every ADR that now exists.
- **Risk register + assumption log (`docs/09`):** the register says it is "reviewed at every stage gate" —
  confirm it reflects current reality, that materialised risks (e.g. solo-project bus factor, cloud-AI data
  policy) are marked as such, and that "verify before stage X" assumptions were actually verified when that
  stage was reached rather than silently carried past their checkpoint.

### D10 — Repository & git hygiene
The single most recurring finding across both prior audits — `CHANGELOG.md` missing whole milestones, and
complete-but-uncommitted/undocumented work — is fundamentally a git-workflow smell, and deserves a named
home rather than living only as a D3 footnote.
- **CHANGELOG-vs-`git log` reconciliation:** every substantial commit since the last entry has a matching,
  correctly-placed changelog entry (newest-first), and no committed milestone is missing one.
- **Branch & worktree hygiene:** stale or orphaned branches, and whether the parallel-subagent-worktree
  model has left direct-to-`master` commits or unmerged tracks that should be reconciled. (Respect the
  deliberate local-only / no-remote posture — flag drift, don't propose a remote.)
- **Uncommitted / untracked state:** work that is finished but sitting uncommitted (the exact 2026-07-11 #2
  / 2026-07-14 #1 failure mode), or generated artefacts that should be either committed or gitignored.

## Method — standing rules for every audit

1. **Capture the test baseline.** Run all three PC-side suites once (`dotnet test`: converter, openness-cli,
   golden-harness) and record the pass counts — this is the *test baseline*, a state fact reported in the
   project-state summary and D4. Because the audit changes nothing, there is nothing to re-verify
   afterward; a red suite at this point is itself a finding, not a blocker to fix mid-audit.
2. **Verify empirically; don't trust either side of a contradiction.** When docs disagree with each other or
   with code, run the tests / read the source / check `git log` and let the result decide — as 2026-07-11 #2
   did by running all three suites rather than believing the doc *or* the diff.
3. **Reconcile whole files, not single lines** (the D2 grep rule) — the single most repeated failure mode.
4. **Ground claims against the artefact, not memory** — grep the real export / source at check time.
5. **Sample, don't exhaust, and time-box.** D3/D6 especially can balloon. Prefer a representative sweep of
   cheap-to-falsify claims over an exhaustive line-by-line read; state what was sampled vs covered in full.
6. **Never read ladder to judge it** (the scope boundary). An audit reasons about docs, tooling code, tests,
   git history, and process artefacts directly. If a finding needs a block opened, explained, or reviewed,
   that is a hand-off to `lad-coder` / the review skills, not an audit finding.
7. **Always run the baseline leak grep** — every audit, regardless of scope. Grep tracked files for known
   identifying strings (real names, the live project/asset identifiers the `test-project001`
   de-identification was meant to remove) and flag any hit in Green-tier committed content. It's cheap,
   mechanical, high-consequence, and needs no ladder reading — so it runs by default, not gated behind D5's
   opt-in compliance round. Deeper data-boundary governance (approval-scope adequacy) stays in D5.

## File naming convention

Every audit produces exactly **two files**, sharing one dated stem so they sort together:

```
docs/audit/YYYY-MM-DD-<short-scope>.md            # the report (findings + project-state summary)
docs/audit/YYYY-MM-DD-<short-scope>-fixlist.md    # the actionable fix list
```

Firm rules:

- **`YYYY-MM-DD`** — the ISO calendar date the audit was run, and it must match the date in the report's own
  header and its row in the Record of audits below.
- **`<short-scope>`** — lowercase **kebab-case** (words joined by single hyphens; no spaces, underscores,
  capitals, or non-ASCII), 2–5 words, plainly naming what the round covered, and **ending in `-audit`**.
  Follow the established form: `stage-and-docs-audit`, `code-quality-and-docs-audit`. Describe the scope in
  plain terms rather than by raw dimension codes (write `stage-and-docs-audit`, not `d1-d2-audit`).
- **The fix list is the same stem plus `-fixlist`.** So `2026-08-01-full-project-audit.md` pairs with
  `2026-08-01-full-project-audit-fixlist.md`. If a round somehow finds nothing actionable, still create the
  fix list and state "no actions" — its presence is the proof the audit produced one.
- **Both files are point-in-time records** — a landed report or fix list is never rewritten except to fix a
  typo. New findings, or a re-check later, mean a *new* dated pair, not an edit to an old one. (The fix
  list records the *recommended* actions as of audit time; whether/when they were executed is tracked by
  the next audit's D7, not by editing this one.)
- **Two audits in one day** — give each a distinct `<short-scope>` (the natural case, since same-day audits
  differ in scope); only if scopes are genuinely identical, suffix `-2`, `-3` on the stem.

## Output contract — what an audit produces

The audit is read-only (see above); its entire output is the two files below. Nothing else in the repo is
touched.

### File 1 — the report (`…-<short-scope>.md`)

The findings-of-record. It opens with the project-state snapshot, then the scoped findings. Structure:

- **Project state summary — the strong overview** *(required, first, every audit regardless of scope).*
  A concise at-a-glance description of **the state the project is in at audit time**, so a reader who opens
  only this section understands where the project stands. Cover: the active roadmap stage and what is
  in-flight / blocked / awaiting a gate; the high-water mark of what has shipped (and what shipped since the
  prior-audit baseline); an overall health read across the areas the audit touched (a short per-area
  green/amber/red-style verdict is ideal); the test baseline (the three suites' pass counts); and the
  headline open decisions or risks. Close it with a one-line pointer to the fix list and the count of items
  by severity. This is a *description grounded in what the audit verified* — not an unchecked restatement of
  what the docs claim. Precedent: the 2026-07-11 report's "Stage/goals summary at time of audit" section,
  now made a standing requirement.
- **Header:** who requested it, the chosen scope (dimensions D1–D10), anything offered-but-excluded, and the
  prior-audit baseline (which earlier audit it continues + what shipped since).
- **Findings**, grouped by dimension and **ranked by severity within each group** (most consequential
  first — the prior audits ordered implicitly; make it explicit so triage is obvious). Each finding states
  what was checked and what was found, with evidence (file/line, grep hit, test/command output), and carries
  a **fix-list ID** (`F-01`, `F-02`, …) linking to its action entry. Findings describe; they never claim a
  fix was made — the audit makes none.
- **Positives / clean-bill notes** — dimensions checked with no issue found are recorded, not omitted.
- **Prior-findings status (D7)** — if D7 was in scope, an explicit table of the previous audit's fix-list
  items and flagged/deferred items, each classified done / still-open / silently-dropped.
- **Outcome + process recommendations** — a short close-out, plus any standing-habit recommendation the
  round surfaced (e.g. the whole-file reconcile rule, promoted here from 2026-07-14's recommendation).

### File 2 — the fix list (`…-<short-scope>-fixlist.md`)

The actionable, self-contained work list a follow-up session or the owner executes **after** the audit. It
is not narrative — it is a ranked table of discrete actions, each turnkey enough to hand off without
re-reading the whole report. One row per action:

| Field | Contents |
|-------|----------|
| **ID** | `F-01`, `F-02`, … — matches the finding's ID in the report |
| **Severity** | blocker / high / medium / low — table sorted by this, worst first |
| **Dimension** | which D-dimension it came from (D1–D10) |
| **Problem** | one line: what's wrong |
| **Recommended action** | the precise fix — exact file(s) and what to change — written so it can be applied without rediscovery |
| **Disposition** | **fix** (mechanical, safe, no decision needed — the old "fixed directly", now *recommended* not applied) · **decide** (needs an owner call: design / governance / scope — never resolved unilaterally, 2026-07-14 #5 is the template) · **accept** (acknowledged, no action / deliberately deferred, with the reason) |
| **Effort / risk** | a rough size and any blast-radius note |

If the round finds nothing actionable, the file still exists and says "no actions". Executing these items —
editing code/docs, committing — is a **separate step outside the audit**; the audit only recommends. Every
executed item is then verifiable by the next audit's D7 against this list.

Confirm the **test baseline** counts appear in the state summary; the audit runs the suites once (Method
rule 1) and makes no changes, so there is no "after" run to reconfirm.

## Record of audits

| Date | Scope | Report |
|------|-------|--------|
| 2026-07-11 | D1 stage/goals + D2 doc consistency (full suite → S1 item 7) | [`2026-07-11-stage-and-docs-audit.md`](2026-07-11-stage-and-docs-audit.md) |
| 2026-07-14 | D3 docs-vs-code + D4 code quality/architecture (compliance excluded by owner) | [`2026-07-14-code-quality-and-docs-audit.md`](2026-07-14-code-quality-and-docs-audit.md) |

Add a row here whenever a new audit lands.
