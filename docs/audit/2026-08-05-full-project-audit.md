# Audit — 2026-08-05: full project audit (all dimensions D1–D10)

The second audit run under the standing charter (`docs/audit/README.md`), and the first to have a real
predecessor to re-derive. Read-only: this report and its paired fix list
(`2026-08-05-full-project-audit-fixlist.md`) are the entire output — no code, doc, skill or IR file was
changed, and no ladder logic was opened.

## Project state summary — the strong overview

**Ladder-AI is capable and mid-S6, and its records have fallen a full landing behind it.** Every capability
the docs describe exists, and more besides; the entire finding set is *records lagging shipped work*, with
one exception that matters more than the rest — **the newest safety net, the "mechanical floor", can
silently fail to run**. Nothing found is a correctness defect in shipped tooling logic.

- **Active stage:** S5 and S6 both open. S6's exit criterion (ten fresh plain-language generation requests)
  stands at **1 of 10** — correctly, and the large S6-Killer-Plan validation wave deliberately did *not*
  inflate it. S7 remains gated behind S6 by the D-4 ruling. S0 is still formally open, gate sign-off
  pending 26 days.
- **High-water mark since the 2026-07-20 baseline:** the **four-rung structured spec pipeline** (rungs A–D)
  built, adversarially validated twice and exercised across three runs on one plant; the **FI-36/FI-39
  mechanical floor** — five new converter checks plus a `trace` guard-containment hop; the `tagstatus`
  hard-rule-3 fix closing a real anti-laundering hole; and the live-run data-boundary regime. 55 commits,
  145 files, ~53,900 insertions.
- **Test baseline (Method rule 1, all three suites, this machine):** **converter 707 · openness-cli 122 ·
  golden-harness 37 = 866 passed, 0 failed, 0 skipped.** Up from 830 at the prior audit; the +36 on
  converter is the mechanical-floor tooling, none of which landed untested.
- **Health by area:**
  | Area (dimensions) | Read | Note |
  |---|---|---|
  | PC-side code quality (D4) | 🟢 green | 25 purpose-built exception types, 22/24 broad catches filtered, zero real TODO/skip markers, net48 pin intact and justified at point of use; one real exception-discipline gap (F-09) |
  | Tests (D4, Method 1) | 🟢 green | 866 pass, 0 skip; every new command shipped with tests; live-TIA proofs still un-automated |
  | Four-rung pipeline **design** (D8) | 🟢 green | every A→B→C→D handoff matches; abstraction ladder holds; each rung traces to a named autopsy failure |
  | Four-rung pipeline **contracts** (D8) | 🔴 **red** | rung C's documented output format is rejected by its own parser; skills pinned to a stale binary — see F-01/F-02 |
  | Pipeline docs of record (D1/D6/D8) | 🔴 **red** | `docs/15` self-certifies as ground truth and is wrong about all four rungs |
  | Docs accuracy & consistency (D1/D2/D3) | 🟡 amber | status layers lag across `AITODO.md`, `docs/16`, `stage-gates.md`, both READMEs |
  | Governance records (D9) | 🟡 amber | risk register never once reviewed; the pipeline has no ADR; ADR index stale |
  | Compliance / hard rules (D5) | 🟢🟡 green-amber | no violation found; compile gate held under pressure; but hard rule 8 is not evidenceable from the record |
  | Data boundary — retention (D5, Method 7) | 🟢 green | live-run retention boundary **held**; see below |
  | Dead-link integrity (D6) | 🟢 green | C-rule IDs 74/74 clean, all cross-file §-anchors resolve; one live filename defect (F-03) |
  | Git hygiene (D10) | 🟡 amber | clean tree, merge discipline held; CHANGELOG 44 commits behind |
- **Method-rule-7 leak grep — clean, and checked deeper than the mechanical minimum.** 456 check terms were
  derived from the live job folder (identifying name and variants, job code, and every equipment/parameter
  identifier in its specs and design deliverables) and swept across all 591 tracked files **and the full git
  history**. The identifying name has never appeared in any commit. Of 456 terms, three produced hits, two of
  which are generic controls vocabulary predating the job; high-signal equipment identifiers scored zero in
  tree and in history. No commit message carries the job identity. The `.gitignore` posture is intact,
  unanchored, spelling-tolerant, with no negations anywhere in the file, and nothing from the folder is
  tracked. **"Use anything, commit nothing" held.** One residual item (F-53) is definitional, not a leak.
- **Headline open decisions / risks:** the risk register has never been reviewed despite claiming per-gate
  review, while four risks have materialised; the four-rung pipeline runs the project's spec work with no
  ADR; `docs/13` still opens "DRAFT — needs a decision" while carrying the project's largest data decision;
  hard rule 8 cannot be evidenced from telemetry; and the `references/<class>/` library remains the binding
  constraint on rung A's core claim.
- **Fix list:** 53 items — **0 blocker, 6 high, 18 medium, 29 low**. Dispositions: **36 fix**, **13 decide**,
  **4 accept**. See `2026-08-05-full-project-audit-fixlist.md`.

> **Time-sensitive note for the owner.** F-01 and F-02 are not latent. A live engineering job is in progress and
> at the spec-review stage — precisely rung C/D territory. If that work is using the rung skills as written
> and permitted, the mechanical floor is not protecting it. These two items are worth clearing before the
> next rung runs, ahead of everything else on this list.

## Header

- **Requested by:** project owner — a full audit, with work split across parallel agents for wall-clock
  efficiency.
- **Scope:** all ten dimensions **D1–D10**, including the normally-opt-in **D5 compliance** (owner's
  explicit choice). The Method-rule-7 leak grep was run at the **deepest** of the three offered levels, again
  by explicit owner choice: check terms derived by reading the live job folder, rather than the historical
  identifiers alone. Nothing was excluded.
- **Prior-audit baseline:** `2026-07-20-full-project-audit.md` + fix list — **never actioned, marked
  out-of-date 2026-08-05**. Per its own banner, D7 **re-derived** every item against current `master` rather
  than ticking any off. What shipped since is listed in the state summary above.
- **Method:** four parallel read-only tracks (D1/D2/D9 · D3/D4 · D8/D5 · D10/D6) with the coordinator
  holding the test baseline, the live-run leak check, and synthesis. Two inter-track disagreements were
  resolved by the coordinator re-verifying at source (see D3 positives and the D7 table).
- **Disposition:** recommend-only. The audit is read-only; executing the fix list is a separate later step.

## Findings

Grouped by dimension, severity-ranked within each group. Each finding carries an `F-NN` id resolving to the
fix list. Findings describe; the audit fixed nothing.

### D8 — AI-operational surface

- **F-01 (high) — rung C's skill documents an output format that its own contract-parser rejects; proven by
  running the tool.** `.claude/skills/gen-equipment-spec/SKILL.md:160–169` writes relation lines bare
  (`  C1  run output driven while…`), while `RelationArtifactParsers.ParseSpecs`
  (`src/converter/Converter/RelationReconcile/RelationArtifactParsers.cs:13`) requires a bolded bullet
  `^\s*-\s*\*\*([CP]\d+)\*\*`. The parser's own source comment (`:6–9`) records that the divergence was known
  and settled *in the parser's favour* — the skill was never corrected. Empirically, both directions:
  `relation-reconcile` against `gen/PlantAutoControl-bench-rerun/equipment-specs` (which followed the documented
  form) → `parsed 0 relations from 6 file(s)`, EXIT=1; against `rerun2`/`rerun3` (undocumented bullet form) →
  174/174/174 and 168/168/168, EXIT=0.
  **Same defect, quieter second face:** `SignalSweepRunner.CollectClaimed`
  (`src/converter/Converter/SignalSweep/SignalSweepRunner.cs:98`) harvests **backticked** identifiers to build
  the "claimed" set, while the skill's `IO BINDING` example (`:154–158`) and register example (`:194`) leave
  signals unbackticked. An author following the example yields `claimed = 0`, and that path **degrades to a
  warning, not an error** (`:105`) — every signal silently reads as unaccounted. The mechanical floor exists
  to be "checks that survive an agent choosing not to look"; following the written instructions defeats it.
- **F-02 (high) — every skill's `allowed-tools` points at a Release binary that predates the mechanical floor
  by two weeks.** All 8 converter-using skills hardcode
  `src/converter/Converter/bin/Release/net8.0/converter.exe`, dated **2026-07-20 16:10**; the five
  mechanical-floor commands landed 2026-08-05 and exist only in the Debug build (`2026-08-05 15:11`). Invoked
  today, that binary's usage text ends at `trace` — no `relation-reconcile`, `signal-sweep`, `candidate-scan`
  or `undriven-scan`. The failure mode is the dangerous one: an unknown subcommand **prints usage rather than
  erroring**, so a check that never ran is indistinguishable from a check that found nothing. `bin/` is
  gitignored, so this is local build state, not a committed defect — which is exactly why it will not
  self-announce.
- **F-04 (high, with D1/D6) — `docs/15-generation-pipeline.md` declares itself ground truth and is wrong about
  all four rungs.** `docs/15:3–5`: *"the 'Build order & status' table at the bottom is **the ground truth** for
  what exists vs. what is still design."* Against the filesystem: `:204` marks `gen-pid-analysis` **"Not
  built"** while the skill exists and has run three times; `:56` says it produces `process-topology.md` while
  it produces `equipment-topology.md`; `gen-functional-analysis`, `gen-equipment-spec` and `gen-code-structure`
  appear **nowhere in the file** (verified: zero grep hits); and `:46` still says "Fifteen skills" against 12
  skill directories. `CLAUDE.md:85–98` is current and correct — so the project's two most load-bearing
  instruction documents contradict each other and the one that self-certifies as authoritative is the wrong
  one. This actively misroutes work: `lad-coder.md:57–59` sends the sub-agent to this table to decide "built
  vs perform manually".
- **F-18 (medium, with D5) — the telemetry schema cannot evidence hard rule 8, which is what D5 checks it
  against.** `docs/notes/gen-telemetry.md:16` fixes the row format at
  `date | skill | wall_clock | portal_roundtrips | tokens | outcome | note` — there is **no field recording
  whether the run was dispatched to `lad-coder`**. The charter (`docs/audit/README.md:138–141`) says hard-rule
  compliance is verified "from process artefacts — telemetry, git history, dispatch records"; the dispatch
  board `agent-tasks/README.md` is empty and the 37 telemetry rows carry no dispatch field. Not hypothetical:
  `AITODO.md:41–43` records that in the runs that used them, *"the four rung skills weren't exposed to the
  Skill tool … each agent read the SKILL.md directly instead"*, while
  `gen-code-structure/SKILL.md:25–26` says *"You run inside `lad-coder` … If reached any other way, stop and
  require dispatch."* Whether rung D of rerun3 ran inside `lad-coder` is unverifiable from the record. Two rows
  self-report deviations in free text (`gen/test-project001/telemetry.log:14`), which shows the discipline
  exists — but only as prose the author chose to write.
- **F-21 (medium) — the rung skills assert "completeness by construction" as delivered; CLAUDE.md and AITODO
  record that it is nominal.** `gen-pid-analysis/SKILL.md:39–41` and `gen-equipment-spec/SKILL.md:46–47` state
  the property flatly and without caveat. But all three references open with
  `> **DERIVED FROM AS-BUILT CODE, NOT FROM AN ENGINEERING STANDARD.**`
  (`references/{FilterUnitSystem,optical-sorter,vsd-motor}/reference.md:3`), and `CLAUDE.md:96–98` / `AITODO.md:33–36`
  both record the property as *"nominal, not delivered"*. The SKILL is what the executing agent actually reads
  at rung time. The runs behaved correctly (rerun3 raised it as blocking `Q-A2`) — but that was the run's own
  vigilance, not the skill's instruction.
- **F-22 (medium) — `gen-equipment-spec` is not permitted to run its own mandatory self-check.**
  `gen-equipment-spec/SKILL.md:205–217` makes `converter signal-sweep` a mandatory pre-finish check
  (*"an approximate denominator cannot support a completeness claim"*), but its `allowed-tools` (`:11–14`)
  permit only `tagstatus` and `candidate-scan`. Contrast `gen-code-structure/SKILL.md:11–12`, which uses a
  `converter.exe:*` wildcard and therefore *can* run its own `relation-reconcile` self-check.
- **F-23 (medium) — `CLAUDE.md`'s "interface members … nowhere earlier" contradicts rung C's own sanctioned
  carve-out, and the contradiction has a named cost.** `CLAUDE.md:89–91` states the rule absolutely.
  `gen-equipment-spec/SKILL.md:76–82` establishes the opposite for one field: an unresolved requirement carries
  `CANDIDATES:` which *"may name interface members, tagged `[iface, named-only]`"*, and states the cost of
  omitting it — *"the rung that discovers a `{raw input, aggregated FB output}` choice cannot write it down,
  and the discovery is lost."* An agent applying CLAUDE.md's absolute rule would refuse to write the field rung
  C mandates, losing exactly the discovery the autopsy says was missed.
- **F-13 (medium, with D1) — FI-37 and FI-38 are marked `Raised` while both are implemented in the rung
  skills; FI-09 is marked `Parked` while 18 of its rules ship.** `docs/16:575` FI-37 `Raised` vs
  `gen-pid-analysis/SKILL.md:65`, literally headed `## Explicitness rules (non-negotiable — FI-37)`;
  `docs/16:583` FI-38 `Raised` vs `gen-code-structure/SKILL.md:213–217`, which cites it by name; `docs/16:192`
  FI-09 `Parked` against its own Verdict paragraph and the shipped rule set.
- **F-29 (low) — `gen-telemetry.md` still calls itself unadopted while four skills cite it as binding.**
  *(Prior F-08, re-derived — still valid and materially worse.)* `docs/notes/gen-telemetry.md:3` verbatim
  unchanged: *"A proposed format, not adopted pipeline behavior yet."* Since 2026-07-20 it has gained 6 logs /
  37 rows including all 12 four-rung rows, and four SKILL.md files now mandate it (`pid:112`, `func:81`,
  `equip:226`, `struct:223`).
- **F-36 (low) — `review-conventions` miscites its own `docs/15` skill number, twice.**
  `.claude/skills/review-conventions/SKILL.md:3` and `:21` say "pipeline skill #9"; `docs/15:65` numbers it
  **#11**, and #9 is `gen-block-modify-fix` — whose own SKILL.md correctly claims #9. Two skills claim the same
  number; the rest spot-check correct.
- **F-49 (low, accept) — the safety enforcer guards writes only; it cannot block reads.** The enforcement chain
  was traced live for the first time, past the rule file: hookify is enabled, registers its `PreToolUse` hook
  plugin-side (which is why no `hooks` key appears in project config), and `python3` resolves on this machine —
  the enforcers are live, not dead files. But `config_loader.py:57–72` expands a bare `pattern:` under
  `event: file` to `field: new_text`, so `\bF_[A-Za-z]` matches *content being written* and cannot fire on a
  `Read`. Hard rule 2 forbids F-content being *"read, written, converted, explained, or referenced"*. The read
  half is covered separately and well by `SafetyClassifier`
  (`src/openness-cli/OpennessCli/Openness/SafetyClassifier.cs:12,33,43`), which refuses to export or open
  safety blocks and fails loud on unknown languages. Combined coverage is sound; the residual gap is a safety
  `.ir`/`.xml` already on disk being `Read`. Two mechanical notes on the same enforcers: `rule_engine.py:24`
  compiles every pattern `IGNORECASE`, so `\bF_` also matches `f_` (widens, never narrows); and
  `config_loader.py:210` globs the rule files **relative to CWD**, so a hook process whose CWD is not the repo
  root loads zero rules and silently allows everything — latent, since no worktrees existed at check time.
- **F-50 (low, accept) — F-16's predicted `.xml` carve-out failure has materialised structurally, with zero
  harm.** *(Prior F-16, re-derived from scratch.)* Tracked `.xml` grew 67 → **115**; **48** now fall outside the
  `not_contains: Converter` carve-out. But every one of them should not be hand-edited anyway — 38 in
  `simatic-ml/` (rule 7's whole purpose), 4 frozen answer keys (`FrozenAnswerKeyRoundTripTests.cs:8–9`), 3
  sanitized SimaticML, 2 in `patterns/` (which the hook message names explicitly), 1 generated. **Files
  legitimately editable but wrongly blocked: 0.** No real PLC content is wrongly permitted — all 67 permitted
  `.xml` are `Converter.Tests/Fixtures`. New mechanical fact: `not_contains` is case-sensitive
  (`rule_engine.py:173`) while `\.xml$` is IGNORECASE, so a future fixture under a lowercase `converter/` path
  would be blocked.

### D9 — Governance-record currency

- **F-05 (high) — the risk register claims per-gate review and has never been edited since the day it was
  written, across four gate sign-offs and 26 days; four risks have materialised and one assumption's own
  checkpoint was passed without a recorded verification.** *(Supersedes and substantially strengthens prior
  F-04.)*
  `git log -1 --date=short -- docs/09-risk-register.md` → **2026-07-10**, the original suite commit. `docs/09:3`
  states *"Reviewed at every stage gate."* S1, S2, S3 (2026-07-14) and S4 (2026-07-15) were signed off since,
  and S5/S6 opened, with zero edits. Against reality:
  - **R-04** (AI hallucination: invented tags, plausible-but-wrong logic), H/H, still listed prospectively —
    **materialised twice**: the S6-Killer-Plan graded a shipped **REGRESSION** plus 3 DEFECTs, and hard rule
    3's own mechanical gate had a live hole (`tagstatus` resolved only to the DB root, so an invented member
    passed as `EXISTS`) until 2026-08-05. The mitigation cell mentions none of `tagstatus`, `preflight` or the
    mechanical floor.
  - **R-08** (cloud-AI / NDA), mitigation *"`13-data-boundary.md` written and agreed **before** real project
    data flows"* — real data has flowed since 2026-07-10; 2026-08-05 widened access to **Red-confidentiality**
    material. The stated precondition was not met and the register does not say so.
  - **R-11** (solo-project bus factor) — live reality, rated prospectively.
  - **R-06** (Openness friction) — materialised and heavily mitigated (`openness-quirks.md`,
    `concurrent-portal-test-plan.md`, `portal-status`/FI-28); none of it appears in the mitigation cell.
  - **A-02** closes with its own checkpoint — *"not yet tested — worth doing before S3 leans on this for the
    compile gate"*. S3 signed off 2026-07-14; `compile-error-playbook.md` now holds **21** entries derived from
    real TIA compile errors, which is de-facto verification. Never recorded.
  - **A-04** ("Claude Code within IT policy — *verify*") — still unverified while the pipeline runs against
    live engineering jobs.
- **F-07 (medium) — the four-rung spec pipeline was designed, built, validated twice and exercised across three
  runs with no ADR, while ADR-0004 remains the accepted record of "the" generation pipeline.**
  `grep -rn "rung\|four-rung\|equipment-spec\|code-structure" docs/adr/` → zero relevant hits.
  `docs/03-development-plan.md:30`: *"Decisions with lasting consequences get an ADR … even if only a
  paragraph."* This is not merely tooling: it changes **where signals and booleans may enter the artifact
  chain**, adds four artifact formats now parsed as contracts by five converter checks, and runs *alongside*
  rather than replacing `gen-architecture` — a relationship asserted only in CLAUDE.md prose. It also has no FI
  entry; it arrived directly from the autopsy.
- **F-26 (low) — the ADR index omits the two newest accepted ADRs.** *(Prior F-01, re-derived — unchanged.)*
  `docs/00-README.md:25` lists `adr-0000…0004`; `adr-0005-derive-always-sidecar.md` (Accepted 2026-07-18) and
  `adr-0006-fanout-annotation.md` (Accepted 2026-07-19) both exist and are cited across `ir/SPEC.md`,
  `AITODO.md`, `docs/notes/`, `src/converter/README.md`.
- **F-27 (low, with D2) — ADR-0005's status self-contradicts within one file.** *(Prior F-06, re-derived —
  byte-for-byte unchanged, line numbers still exact.)* `docs/notes/synthesis-parity-plan.md:111` and `:141` say
  "Proposed"; `:121` says "Accepted (owner)". Actual: `adr-0005:3` = Accepted (owner, 2026-07-18).

### D5 — Compliance

- **F-16 (medium) — the live-runs boundary is operative and load-bearing, but `docs/13` still opens "DRAFT —
  needs a decision"; the standing ADR-0003 hold is now materially harder to justify.** *(Prior F-17,
  re-derived; premise moved and severity raised low → medium.)* `docs/13:3` still reads *"Status: DRAFT — needs
  a decision … **before real project data flows to any AI**"* and `:24–26` still states the interim rule *"Only
  Green-tier content goes near Claude Code"* — which its own next paragraph already contradicts with two
  standing exceptions. Meanwhile `:240–307` grants, as an established owner decision, full unsanitized working
  access to Red-tier-on-confidentiality material including generation/import/compile against a live-run
  project, and `:317` records a live job opened under it. A document whose header says the decision has not
  been taken now carries the project's single most consequential data decision plus 10 per-project approvals
  and a live-job register. No `adr-0003-*.md` exists; `adr-0004:5` still records the slot as deliberately
  reserved.
- **F-17 (medium) — recorded approval scope: the live-runs regime supersedes the per-project process without
  saying what happens to the approvals that still name real site owners.** *(Prior F-19 scope-adequacy half,
  re-derived.)* The live-runs register (`docs/13:309–315`) is deliberately job-code-only and states that live
  runs *"do not follow"* the older Amber process — but does not say whether the existing named approvals are
  superseded, remain live in parallel, or are frozen. The 2026-07-29 entry (`:230–234`) records the precedent
  that matters: *"every prior approval here was scoped to advancing a stage of **this** project … This one is
  not — it is production engineering output for a real job."* The live-runs section generalises exactly that
  shift without stating the relationship. **No breach was found** — the committed real names remain explicitly
  approved under `docs/13:32–39`. The gap is definitional.
- **F-25 (low) — two committed pipeline artifacts carry unqualified `tagstatus` clean-bills produced by a
  checker now known to have been member-blind.** `gen/PlantAutoControl-bench-rerun/telemetry.log:3` records
  *"tagstatus 0 proposed"* with no caveat; that pass predates `f50753a` (2026-08-05T09:19). `rerun2`'s log
  **caught the defect and said so** — *"converter tagstatus found DEFECTIVE (validates root DB only, reports
  invented members as EXISTS) so all binding verified by grep instead"* — and `rerun3`, post-fix, immediately
  reports *"62 names (2 proposed, 5 member-not-found)"* on the same plant. Consequence is contained: all three
  reruns record `D3 STOPPED` and no `.ir` was written, so nothing reached ladder. The residue is
  records-accuracy: `rerun`'s committed clean-bill is not trustworthy and warns no future reader.
- **F-53 (low) — the live-run governance ledger carries the job code, in the same two lines that say job
  identity lives only in the job folder.** `docs/13-data-boundary.md:317–318` records the job by its code while
  stating *"Job identity, scope and all content live in `Live Runs/<job>/` only."* Surfaced by the Method-7
  sweep as the **only** real-identifier hit in tracked content, introduced by the commit that established the
  boundary. Reads as deliberate and minimal — a governance record that a live run happened at all — but it is
  in literal tension with its own sentence.

### D1 — Stage & goals accuracy

- **F-08 (medium, with D2/D6) — `AITODO.md`, whose stated purpose is to be the post-interruption recovery
  point, describes finished-and-merged work as in-flight on a branch that no longer exists.** `AITODO.md:23`
  heads the section *"In flight — S6-Killer-Plan aftermath (2026-08-05, branch `worktree-s6-killer-plan`, NOT
  merged)"*, and `:25` tells the reader to run `git log --oneline master..worktree-s6-killer-plan` — which
  fails outright (`git rev-parse --verify` → `fatal: Needed a single revision`). The work **is** on master via
  `242e5e2`. This is the exact failure mode the 2026-07-14 audit named, and it defeats `AITODO.md`'s own
  recovery step 4. Compounding it, `:109` says *"Current task: none in-flight"* 86 lines after the "In flight"
  heading, dated two weeks earlier; and two "Recently landed" blocks (`:124`, `:152`) — one self-instructing
  *"prune once stale"* — were never pruned, against the file's own opening rule.
- **F-10 (medium) — the single largest S6 work wave since the stage opened has no entry in `stage-gates.md` or
  `docs/evidence/stage-S6.md`, in breach of the append convention those files state.** `stage-gates.md:39–43`
  states the rule; `docs/evidence/stage-S6.md`'s last heading is the 2026-07-20 generation request, with
  nothing after; `stage-gates.md` was last touched 2026-07-20 and has no 2026-08 entry. The wave's record went
  instead to `docs/notes/S6-Killer-Plan.md` plus six `docs/evidence/PlantAutoControl-bench-*.md` and two
  `four-rung-design-validation*.md` files, none linked from the "Full evidence per stage" block. The evidence
  exists and is thorough — the *index into it* does not.
- **F-11 (medium) — `AITODO.md`'s FI round-up lists nine implemented items as "awaiting a decision … still
  open".** *(Prior F-03, re-derived — unchanged and now wider.)* `AITODO.md:289–301` lists FI-17, 22, 23, 26–30
  as open; `docs/16` marks every one IMPLEMENTED 2026-07-20 with the matching subcommands live in `CLAUDE.md`.
  It also asserts FI-09's C-118–125/C-103 family "still open" when those rules ship. FI-25 is absent entirely
  and FI-32–39 appear nowhere in the round-up.
- **F-12 (medium) — `docs/16`'s two status-layer sections (both dated 2026-07-20) omit FI-36–FI-39 and assert a
  "cleared / nothing buildable" state the 2026-08-05 wave contradicts.** `docs/16:48–51` — *"Open, actionable
  next (no external gate): **essentially cleared**"*; `:73` — *"nothing is buildable without an external
  trigger."* Against: FI-39 shipped five converter checks plus `SignalInventory` on 2026-08-05, and its own
  entry (`:593`) records two *named open refinements* buildable now with no external gate. Textbook
  status-layer-vs-narrative drift — the per-entry narrative is current, the index above it is two weeks stale.
- **F-24 (medium) — "8 of ~50 rules" understates the mechanized reviewer, and the prior audit's replacement
  figure was itself wrong.** *(Prior F-02, re-derived — and corrected.)* `docs/notes/stage-gates.md:13` and
  `AITODO.md:62` both say "8 of ~50 rules". The real figure is **18** — `ReviewRunner.cs:11`'s `AllRuleIds`
  dispatches exactly 18 IDs. The 2026-07-20 audit said 24 in three places and its F-02 recommended writing 24
  into both docs; that count was of *mentions* in `Rules.cs`, not dispatches. The six extras (C-101, C-105,
  C-123, C-124, C-307, C-407) appear only in comments and finding-message text, each explicitly labelled a
  judgment clause deliberately not mechanized. **Verified at source by the coordinator** after two tracks
  disagreed. Any F-02 follow-up must use 18.
- **F-32 (low) — `00-README`'s "immediate to-dos" are complete, and one is now actively false.** *(Prior F-15,
  re-derived.)* `docs/00-README.md:33` still says "Verify assumption log items A-01/A-02 during S0"; both are
  **VERIFIED 2026-07-10** (`docs/09:22–23`). To-do #1's parenthetical — that deciding the data boundary
  "doesn't block S0–S6 on the reference project" — is now materially false, since S6 work has run against real
  restricted data.
- **F-44 (low) — S0's gate has been "pending" for 26 days and its own stated precondition was overtaken four
  stages ago.** `stage-gates.md:9` ends *"TODO: formal gate review sign-off before flipping to done/starting
  S1"*, while S1–S4 are all signed off. `:3` discloses the situation, so it is disclosed-but-unreconciled
  rather than hidden — but the charter's D1 asks that open gate sign-offs be *deliberately* open, not stale.
- **F-45 (low) — `docs/02-roadmap.md` has not been touched since 2026-07-10 and does not carry the ruled
  interpretations of its own exit criteria.** Three rulings modify how its criteria are read — the "~10
  patterns" seed target satisfied by two kinds; S6 exit meaning ten *fresh* requests with fix waves tracked
  separately (D-4); S7 entry deliberately kept as "S6 done" — all recorded only in `stage-gates.md`,
  `docs/evidence/stage-S6.md` and `AITODO.md`. The roadmap is the doc a fresh reader consults for what "done"
  means.

### D2 — Doc internal consistency

- **F-34 (low) — `docs/16`'s declared status vocabulary matches roughly half the statuses in use, and two
  entries break the machine-readable `Status:` key.** `:7` declares *"Raised → Under debate → Accepted |
  Rejected | Parked"*; the 39 entries also use `Deferred`, `PILOT LANDED`, `Partially done`, and `IMPLEMENTED`
  (×12). Format defect: FI-36 (`:565`) and FI-39 (`:591`) write `- **Status: … IMPLEMENTED 2026-08-05**` with
  the colon *inside* the bold, so they do not match `- **Status:**` and are invisible to the obvious grep —
  and both then carry a second line `- **Superseded status line:** Raised` that reads as a live status to a
  scanner. This is a repeatedly-audited surface; it should be mechanically scannable.
- **F-35 (low) — `docs/16`'s Lifecycle rule is contradicted by every Accepted entry, each of which states its
  own exception.** `:9` says promotion into the plan *"requires an ADR"*; FI-13 (`:248`), FI-14 (`:256`),
  FI-15 (`:264`) and FI-16 (`:272`) each independently restate an exception the rule does not contain, and
  eleven IMPLEMENTED entries never went through `Accepted` at all. Naming the carve-out once would also give
  F-07 (the missing pipeline ADR) a clean test to apply.
- **F-43 (low) — the glossary and the suite's reading-order table have not tracked the project's growth.**
  `docs/12-glossary.md` (untouched since 2026-07-10) defines LAD, IR, Openness, Pattern, Slot — but not
  *sidecar*, *preflight*, *telemetry*, *rung A–D*, *mechanical floor*, *Green/Amber/Red tiers*, *Live Runs*,
  *`lad-coder`*, *skill*, or *FI item*, several of which carry compliance weight. `docs/00-README.md:7–28`
  lists every `docs/NN-*.md` but omits `docs/notes/` and `docs/evidence/` — the latter now 9,115 lines across
  18 files, where every stage's actual evidence lives.
- **F-46 (low) — `docs/04-design-philosophy.md` §10 already forbade the failure the autopsy found, but is
  scoped to the converter only.** `:41–43`: *"Unknown XML elements, unmapped instructions, ambiguous tags: hard
  errors, not warnings. Silent best-effort conversion is how a debounce timer becomes a latch."* The autopsy's
  root cause is the same principle violated one layer up — an ambiguous `/` silently resolved toward the weaker
  reading by both coder and reviewer. FI-37/FI-38 exist to enforce §10 at the spec layer; the philosophy doc
  does not say so, which makes the new rung rules look ad hoc rather than derived.

### D3 — Docs-vs-code accuracy

- **F-14 (medium) — both the converter README and `CLAUDE.md` understate the converter's construct coverage,
  and the README contradicts itself.** `src/converter/README.md:19–27` is headed *"Current scope (walking
  skeleton)"* and states *"**No arithmetic (`Add`/`Sub`/`Mul`/`Div`/`Convert`/etc.) or FC/FB
  parameter-interface modeling yet**"* — contradicted **in the same file** by `:552` (`MUL/CONVERT —
  arithmetic`), `:639` (`TONR / ADD / Lt`) and `:702` (`FC/FB parameter-interface modeling`). Ground truth is
  `FlgNetParser.cs:17`, a **33-name** supported-part set. Separately, `CLAUDE.md:37–39` omits five supported,
  tested constructs — **`LIMIT`, `WAIT`, `FillBlockI`, `Modbus_Master`, `Modbus_Comm_Load`** — each with a
  dedicated committed test file, while asserting *"Anything else outside this slice is a correct hard error,
  not a bug."* Mitigating: CLAUDE.md's pointer to `docs/evidence/stage-S1.md` resolves to complete information.
  This is the 2026-07-14 #4 failure class with the offender swapped — CLAUDE.md was fixed then, the README was
  not.
- **F-15 (medium) — `CLAUDE.md` claims a CI that does not exist.** `CLAUDE.md:21`: *"`ExportDriftDetectorTests`
  guards it **in CI** against a known-drift baseline."* There is no `.github/`, no `.gitlab-ci.yml`, no
  `azure-pipelines.yml`, no `.circleci`. The test is real but runs only in a local `dotnet test`.
- **F-30 (low) — `--no-sidecar` is real but absent from the README whose stated job is the flag reference.**
  Implemented at `src/converter/Converter/Program.cs:135,143,173` with the verified-omit path at `:1300–1338`;
  zero hits in `src/converter/README.md` and in `CLAUDE.md`. It *is* documented in ADR-0005, `ir/SPEC.md`,
  `AITODO.md` and the tool's own usage banner. Precisely the `--tagtable` class the 2026-07-14 audit caught.
- **F-31 (low) — `extract/README.md` documents a test suite that doesn't exist.** *(Prior F-14, re-derived —
  unchanged.)* `extract/README.md:7` — *"Run: `pytest tests/` for unit tests"*; `ls extract/` → `README.md`
  only, unmodified since 2026-07-09. Worth recording alongside: the two *other* places making this claim get it
  right (`CLAUDE.md:58` "once `extract/` (S5) exists"; `docs/08:38` "reserved … once it exists"). Only the
  README is present-tense.
- **F-39 (low) — residual flag-surface gaps.** `candidate-scan --instance`/`--phrase` absent from
  `CLAUDE.md:53`'s synopsis; `openness-cli portal-status` accepts `--timeout-connect`/`--timeout-open`
  (`ArgumentParser.cs:242,249`) which cannot affect a command that never attaches; `list`/`compile`/
  `sanity-check` accept `--json` but `CLAUDE.md:29/32/35` omit it; and `src/openness-cli/README.md` has no
  exit-code table despite 11 distinct codes (`Program.cs:244–257`). The READMEs are correct in each case, which
  is why this is low.
- **F-42 (low) — `tests/golden/README.md`'s corpus description is behind the harness.** It documents 7 + 7 = 14
  reference artefacts and calls them "the full committed corpus"; actual is 15 `.ir` / 15 `.xml`, and the 15th
  (`HandAuthorSplitsMerges`) gets zero mentions there. Separately, `grep -c "test-project001\|PlantAutoControl"` →
  **0**, yet `CommittedBlocksRoundTripTests.cs:20–24` and `ExportDriftDetectorTests.cs:27–30` both cover the
  `test-project001` pair. The README presents `ir/reference` as the whole corpus; the harness covers two
  projects.

### D4 — Code quality & architecture (PC-side tooling only)

- **F-09 (medium) — 19 domain errors throw `InvalidOperationException`, so the top-level handler reports the
  commonest user mistake as an internal error with the wrong exit code.** `OpennessGateway.cs` has 34
  `throw new InvalidOperationException`; 15 are legitimate precondition guards, but **19 are user-facing domain
  errors** — `No device item found under '<groupPath>'` (`:1181,1234,1285`), `'<path>' is not a PLC software
  container` (`:1187,1240,1291`), `Block/Type/Tag table group '<x>' not found` (`:1194,1247,1298`),
  `Empty --group path` (`:1156,1209,1260`), and 7 more. `OpennessCli/Program.cs:83` catches only four
  purpose-built types → exit **7** (`CommandError`); everything else falls to `:88`'s catch-all → exit **5**
  (`UnexpectedError`), printing `failed: InvalidOperationException: …`. So a mis-typed `--group` — the mistake
  `CLAUDE.md:110` explicitly warns is easy to make — exits 5, indistinguishable from an internal fault. A
  `DeviceNotFoundException` already exists and is thrown **two lines above** in the same method
  (`:1160,1215,1266`): the pattern is established and simply not carried through the resolver. **This
  disqualifies a prior-audit clean bill** ("zero bare `InvalidOperationException` used as domain errors") which
  was already false on 2026-07-20 — `src/openness-cli/` has had zero commits since.
- **F-19 (medium) — `ir/PlantAutoControl-bench/` (34 tracked `.ir` files) has zero round-trip and zero drift
  coverage, and the exclusion is stated nowhere.** No matching `simatic-ml/PlantAutoControl-bench/` exists;
  `CommittedBlocksRoundTripTests.cs:20–24` and `ExportDriftDetectorTests.cs:27,30` hard-code exactly two
  `(ir, simatic-ml)` pairs. So the **largest single IR corpus in the repo** — the one the entire four-rung
  pipeline was validated against across three runs — sits outside every automated tooling check. Whether that
  is intentional (a synthetic bench never taken through TIA) is recorded in neither `tests/golden/README.md`,
  `CLAUDE.md`'s "What you work on" list, nor either test. A future reader cannot distinguish "deliberately out
  of corpus" from "forgotten". *(Filename/`git ls-files` inventory only — no `.ir` opened.)*
- **F-20 (medium) — the live-TIA regression gap is unchanged, and one half of it fails silently.** *(Prior
  F-05, re-derived.)* `SynthesizerLiveCheck.cs:32` and `ReferenceProjectRoundTrip.cs:33` are still
  `public static class` with no `[Fact]` — their own doc comments say so and tell you to add one by hand. The 3
  IR-only `test-project001` artefacts still have no `.xml` (26 `.ir` vs 23 `.xml`), and
  `CommittedBlocksRoundTripTests.cs:57` gates on `File.Exists` and **yields nothing** rather than failing — a
  silent exclusion, not a reported skip. `ir/reference` is still 15/15 with no growth since 2026-07-20.
- **F-37 (low) — no compiler-warning enforcement anywhere.** `grep -rn "TreatWarningsAsErrors\|NoWarn\|
  WarningLevel\|EnableNETAnalyzers\|AnalysisMode\|#pragma warning"` across all `src`/`tests` `.cs`, `.csproj`
  and `.props` → **zero hits**; neither `Directory.Build.props` sets a warning policy. With no CI, the
  charter's "target zero warnings" is both unenforced and unmeasured.
- **F-38 (low) — build-config duplication unchanged; `net48` constraint intact.** *(Prior F-10, re-derived —
  still valid verbatim.)* The four test pins (`coverlet.collector` 6.0.0, `Microsoft.NET.Test.Sdk` 17.8.0,
  `xunit`/`xunit.runner.visualstudio` 2.5.3) are duplicated across all three test `.csproj`;
  `GoldenHarness.Tests.csproj:4–7` still pins `net8.0` inline with no `Directory.Build.props` under
  `tests/golden/`; no central package management; no CI. *Positive within it:* the runtime-critical constraint
  is intact and correctly sited — `src/openness-cli/Directory.Build.props:4` `net48` with the justification at
  `:10–13`, and `src/converter/Directory.Build.props:4` `net8.0` (correct — no Siemens dependency).
  `Siemens.Engineering.dll` is referenced by `HintPath` from `$(TiaOpennessDir)` with **no `Version` and no
  `SpecificVersion`**, so a Portal upgrade silently rebinds with no build-time signal — inherent to an
  unversioned local SDK dependency, not a defect, but live evidence for R-02/R-12.
- **F-40 (low) — `candidate-scan --instance` is accepted and displayed but never narrows anything.**
  `CandidateScanRunner.Run` takes `string? instance` (`:22`) whose only use is `:51`, passing it into the
  report for the header and JSON; it never reaches `CollectIoCandidates` (`:55`) or `CollectFbCandidates`
  (`:75`). Given the sibling command's headline claim is *"PER-INSTANCE interface drive states… per-instance
  is the point"*, a reader can reasonably assume `--instance` narrows here too. The design reason (a candidate
  set is a property of the FB class, not an instance) is sound but unstated.
- **F-41 (low) — two consistency nits in the new relation-reconcile parsers.**
  `RelationArtifactParsers.cs:174` compiles a `Regex` inline inside a per-line loop while every other pattern
  in the file is a `static readonly … RegexOptions.Compiled` at the top (`:13–37`). And `:37`
  `StoppedMarker = new(@"STOPPED")` is a bare substring match over the whole ledger file (`:213`), used at
  `RelationReconcileRunner.cs:32–34` to choose between *"a stopped rung is a legitimate state, not a drift"*
  and *"no STOPPED marker either. Verify this is intended."* The uppercase word appearing anywhere in unrelated
  prose flips a "verify this" warning into a reassuring one. Case-sensitivity limits the risk and **neither
  branch gates**, which is why this is low.
- **F-51 (low, accept) — `PartNode` and `DbMember` have stopped growing; recommend downgrading the watch.**
  *(Prior F-11, re-derived — recommendation reversed.)* Both are at **14** fields, **identical** to the
  2026-07-20 counts: zero growth across 16 days and 55 commits that landed four new subcommands, a shared
  `SignalInventory` primitive and a `tagstatus` semantics change. The escalation premise ("the kitchen-sink
  trend has continued") is now false. A discriminated-union refactor of `PartNode` would touch
  `FlgNetParser.cs`, `FlgNetBuilder.cs` (1,264 lines), `GraphReducer.cs` (2,068), `Normalizer.cs` and
  `SidecarSynthesizer.cs` (1,272) — ~6,000 lines of the most load-bearing code in the repo, with no CI to catch
  a regression. Cost/benefit has moved against it.

### D6 — Cross-reference / dead-link integrity

- **F-03 (high) — two built, live skills consume an artefact filename that nothing in the repo produces, and
  the failure is silent.** `.claude/skills/gen-architecture/SKILL.md:71` — *"`gen/<project>/process-topology.md`
  … **consumed if present**"* — and `:205`; `.claude/skills/review-conventions/SKILL.md:243` and `:306` —
  *"C-114's material-flow half: needs a process-topology artifact"*; plus `docs/15:56` and `:178`. Repo-wide,
  `process-topology` returns 10 hits and **not one is a file**. The producer emits `equipment-topology.md`
  (`gen-pid-analysis/SKILL.md:86`), confirmed on disk across all three runs. Because the consumer says
  "consumed if present", it will record the artefact as absent and fall through to its weaker path **on every
  run, forever**, with no error and no stop condition; `review-conventions`' C-114 material-flow half will
  permanently self-declare not-applicable. The project's own round-2 validation already caught this
  (`docs/evidence/four-rung-design-validation-round2.md:409,490,615`) and left it open.
- **F-33 (low) — the 2026-08-05 supersede banners cite the mechanical floor by its pre-renumbering IDs, in the
  same sentence that announces the renumbering.** `docs/audit/2026-07-20-full-project-audit.md:14` and
  `-fixlist.md:9` say *"the **FI-32/FI-35** 'mechanical floor' tooling"*, then `:16`/`:10` say *"an FI
  renumbering (FI-32..35 → FI-36..39)"*. Under current `docs/16`, FI-32 is *"Replace IR with a restricted real
  programming language"* and FI-35 is *"`converter alarm-scan` + HMI alarm-list generation"*; the mechanical
  floor is FI-36 and FI-39. Written *after* the renumbering commit. The charter's never-rewrite rule protects
  findings, evidence and counts — the banner is explicitly an archival annotation and the thing the next
  auditor uses to judge reusability, so correcting it is in bounds; but it touches an audit file and should be
  a conscious act.
- **F-48 (low, decide/hand-off) — a dangling `gen/GenProject1/` path frozen inside a committed SimaticML
  export.** *(Prior F-09, re-derived — unchanged.)* `simatic-ml/test-project001/DB_Settings.xml:288` references
  `gen/GenProject1/fix-wave-1.md`; the directory is now `gen/test-project001/`. It is the only tracked
  non-`.md` occurrence outside changelog/audit prose. Per hard rule 7 this must **not** be hand-patched — the
  fix is a source-side correction plus re-export, i.e. a `lad-coder` hand-off, which also touches the
  `ExportDriftDetectorTests` known-drift baseline.
- **F-52 (low, accept) — pre-rename `GenProject1` paths persist in historical `CHANGELOG.md` entries.**
  `CHANGELOG.md#2026-08-05` (cited as `:325`, `:483` before the 2026-09-17 table of contents shifted line
  numbers), plus `gen/test-project001/telemetry.log:4` (append-only by convention) and one
  correct live-Portal-project reference. The paths were accurate when written and both files are point-in-time
  records. Logged so the next audit does not re-derive it.

### D10 — Repository & git hygiene

- **F-06 (high) — `CHANGELOG.md` has zero entries for the entire period since the last audit: 44 commits, 145
  files, 53,910 insertions, including five new user-facing subcommands, four skills, a hard-rule-3 correctness
  fix and a data-boundary governance change.** Newest heading is `## 2026-07-20`; last commit touching the file
  is `9516d20`. Zero-hit greps for `candidate-scan`, `undriven-scan`, `relation-reconcile`, `signal-sweep`,
  `guard-containment`, `four-rung`, `gen-equipment-spec`, `gen-code-structure`, `Live runs`, `S6-Killer`. New
  source trees added and unrecorded: `src/converter/Converter/{CandidateScan,RelationReconcile,SignalInventory,
  SignalSweep,UndrivenScan}/` plus five new test files. Test count drifted 671 → 707 with no entry. **The
  failure is CHANGELOG-specific, not general doc neglect** — in the same range `CLAUDE.md` (+25),
  `src/converter/README.md` (+195/−16), `docs/16` (+92) and `AITODO.md` (+27) were all updated. Eleven distinct
  milestones are missing; the most consequential is `f50753a`, a **user-visible behaviour change** (`tagstatus`
  used to pass invented DB members). This is the **third consecutive audit** to find this class.
- **F-28 (low) — the two orphan branches are still present, and the prior audit's stated premise was wrong,
  which makes deletion provably safe.** *(Prior F-12, re-derived — premise corrected.)* The 2026-07-20 audit
  said they were "not in `master`'s line". In fact all four independent tests agree they are fully merged:
  `git log master.._master_sync` and `..._master_sync2` both empty, `git cherry` both empty,
  `git merge-base --is-ancestor` exit 0 for both `ba21079` and `9324685`, and `git branch --merged master`
  lists both. Nothing unique rides on them; a safe `git branch -d` (not `-D`) will succeed precisely because
  they are merged.
- **F-47 (low) — audit artefacts are still not changelogged, and the precedent is now three-for-three while the
  convention has never been written down.** *(Prior F-13, re-derived to a clearer answer.)* `grep -i audit
  CHANGELOG.md` → 4 incidental prose hits; no entry exists for any of the three audits nor for the charter
  itself (7 commits). Five audit files are tracked. The consistency across three audits over five weeks makes
  the exclusion de-facto intentional — what is missing is the *record* of it as a convention.

## Positives / clean-bill notes

Recorded per the charter — a dimension checked with no issue found is a finding.

**The four-rung pipeline's design audits clean.** All four SKILL.md files were read in full and every handoff
traced: A→B (`equipment-topology.md`), A/B→C, C→D (`equipment-specs/<Instance>.md`), D→Build — all names match.
The abstraction ladder holds with one documented carve-out (F-23): A bans signals/booleans/interfaces, B bans
those plus block names, C admits signals and bans booleans/interface members with an argued polarity carve-out,
D admits booleans and interface members. Each rung's stop conditions and Q-escalation rules are internally
consistent, and the autopsy's three root causes map cleanly onto split-and-retain + blocking Q (A), the computed
candidate set (C), and the discharge ledger's HARD FAIL (D). **The defects found in it are format-contract and
record-currency issues, not design issues.**

**Three of the four parser contracts match their skill exactly**, verified regex by regex: the D2 ledger
(heading, header cell, ids, em-dash normalisation), the derived register, the D3 render — including the
deliberate `ABSENT`-vs-trivially-agrees distinction, confirmed live on two runs. Only `--specs` diverges (F-01).

**The mechanical floor demonstrably works on the artifacts it was built for.** Run read-only against the current
build: `relation-reconcile` on rerun2 (174/174/174) and rerun3 (168/168/168), 0 differences, EXIT=0;
`signal-sweep` on rerun3 (201 swept / 32 claimed / 168 disposed / **1 unaccounted**) versus rerun2 (**54
unaccounted**). The rerun2→rerun3 improvement is exactly what the tooling was added to produce, and the residual
1 matches the known open item. Every parser **refuses to report a clean result from an unparsed leg**.

**Hard-rule compliance from the process trail — clean; no violation found.** HR1: no `.scl/.stl/.awl/.graph/.cfc`
tracked. HR2: `git grep -lE "\bF_[A-Za-z]"` → 4 files, **all** meta (the hook rule, an API-surface note, two
`openness-cli` test files) — zero safety content. HR3: rerun3 raised proposed/member-not-found as blocking rather
than coding against them. **HR4 — the strongest single piece of compliance evidence in the corpus:**
`gen/test-project001/telemetry.log:14` records a run **blocked at the compile gate and refusing to report done**;
`:15` records it passing on resumption. The gate held under pressure. HR5: reviews ran in fresh context with
repeated "not committed" / "report-only". HR6: no download/online/force API surface. HR7: no hand-patched
SimaticML.

**Hard-rule and command-surface coherence — clean.** Every command the hard rules and Commands block reference
exists: 16/16 converter subcommands dispatched in `Program.cs`, 8/8 `openness-cli` verbs in
`ArgumentParser.cs:142–149`. `lad-coder.md`'s contract matches hard rule 8 and `docs/15`, including its
no-skill-yet clause.

**Convention rule-ID parity — clean.** All 18 dispatched C-IDs exist in `docs/06-lad-conventions.md`, verified
individually. No phantom IDs, no drifted meanings. The six additional IDs in `Rules.cs` are each explicitly
labelled at the point of use as judgment clauses deliberately not mechanized — e.g. `Rules.cs:845`, which tells
the reader inside the finding message itself that only step-0 presence is checked. That is exactly the charter's
requirement that documented-but-unimplemented rules be marked as such, done well.

**C-rule and reference integrity — clean.** 74 distinct C-IDs defined in `docs/06`; 74 cited across all 591
tracked files; set difference **empty** — zero dangling rule citations anywhere. All 20 cross-file `§`-anchor
citations resolve. ADR-0000..0006 all resolve, with ADR-0003's absence documented as deliberate in three
independent places. All 12 skill directories exist and every `.claude/…` path citation resolves; the eight
cited-but-absent skill names are all correctly documented as unbuilt stages.

**The FI-32..35 → FI-36..39 renumbering is clean.** Every `FI-3[2345]` citation in a live doc refers to master's
meaning; the design-study file was renamed with it; stale old-number references survive only in immutable commit
messages and the two archival banners (F-33). Given the charter names FI churn as a live dead-link risk, this is
a genuinely good result.

**Exception and code discipline — strong (excluding F-09).** Zero `throw new Exception`, zero
`ApplicationException`; **25 purpose-built exception types**, and the three new commands each added their own
rather than reaching for a generic type. 24 broad-catch sites, **22 type-filtered**; the two unfiltered are
justified inline (`OpennessGateway.cs:154`; the CLI's last-resort handler, which prints the full inner-exception
chain and returns a distinct code). Zero `TODO`/`FIXME`/`HACK` markers, zero skipped or ignored tests. No new
file exceeds 400 lines — all growth since the prior audit landed in new, well-factored directories rather than
accreting onto the large existing files.

**The new FI-36..39 code is the best-documented code in the repo, and its design judgement is sound.** Each
module states the defect it exists to catch *and what it deliberately refuses to do*:
`CandidateScanRunner.cs:37–40` keys the exit code off the **unfiltered** set because *"filtering a candidate set
by name resemblance is precisely the reasoning that produced the swapped-pairing defect… narrowing on it would
launder the same bias behind a computed-looking number"*; `UndrivenScanRunner.cs:120–122` keeps heuristic hints
out of the exit condition; `SignalSweepRunner.cs:81–82` calls its own containment test "deliberately coarse" and
names the case it must not pretend to detect. `SignalInventory` is a well-judged shared primitive, and its claim
that the three project walkers can never disagree about corpus contents was verified at all three sites.

**`tagstatus` documentation is accurate, including the new fix** — the four states, the gate semantics,
`--roots-only`, and a dated History paragraph naming the old behaviour and why it was wrong. This is the model
the other README sections should follow.

**Enforcement hooks are genuinely live**, traced past the rule files for the first time (see F-49):
`SafetyClassifier` remains coherent with the `\bF_` pattern and retains its fail-loud on unrecognised languages;
the `.xml` carve-out currently blocks nothing that should be editable.

**Git hygiene — clean apart from the changelog.** Working tree completely clean, `--untracked-files=all` empty —
no finished-but-uncommitted work, the single most recurring D10 failure mode, absent this round. No stale
worktrees. **Subagent merge discipline held**: every parallel track since 2026-07-20 landed via a merge commit
and the branches were deleted after — prior F-18 did not recur. Local-only posture intact with no drift toward a
remote. `.gitignore` sound, every entry justified by a comment, and the blind-isolation quarantine holds.

**The project is honest about the pipeline's maturity where it matters most.** `CLAUDE.md`, `AITODO.md` and
`docs/notes/S6-Killer-Plan.md` all carry the one-plant and references-nominal caveats prominently and without
softening; `AITODO.md` names two of its own tool weaknesses; `docs/16:604` self-corrects an earlier overstatement.
**No doc overstates the pipeline's maturity.** The only honesty gap is inside the skills themselves (F-21).

**Deletion residue is self-documenting.** Every reference to a removed file explains its own absence at the
citation site — `network-3-no-simulation-override.ir` ("was removed after…"), `fix-wave-1-reviews.md` ("retired
in the 2026-07-17 declutter"), `template.ir` ("Abandoned"). This is precisely the rename-only-pass failure mode
`CLAUDE.md` warns about, and it is being avoided.

**The S6 tally is consistent across all three places that state it** (`CLAUDE.md`, `AITODO.md:80`, `:222`), and
both `AITODO.md:47` and `S6-Killer-Plan.md:32` independently confirm the validation wave does **not** count
toward it. Given how easily a large wave could have quietly inflated the tally, this is the discipline working.

**`docs/10-non-goals.md` holds** — no scope drift crosses a permanent exclusion, and `docs/01-scope.md`'s six
hard constraints map 1:1 onto CLAUDE.md's hard rules 1–6 with no divergence in substance.
`docs/notes/owner-questions.md` is correctly empty.

## Prior-findings status (D7)

The 2026-07-20 fix list was **never actioned** and is marked superseded; per its banner and the charter, every
item was **re-derived** against current `master` rather than ticked off. Of 19 items, **14 are still valid, 2
were overtaken by events, 2 have reversed recommendations, and 1 had its premise corrected**. Two prior
*positives* were also found to be wrong.

| Prior | Status | Evidence / where it went |
|---|---|---|
| F-01 ADR index omits 0005/0006 | **still valid, unchanged** | → F-26 |
| F-02 "8 of ~50 rules" | **still valid — but the prior fix figure was wrong** | Real count **18** (`ReviewRunner.cs:11`), not 24; the prior audit counted mentions. Its recommended action would have written a wrong number. → F-24 |
| F-03 AITODO FI round-up | **still valid, unchanged, now wider** | The banner's claim that the renumbering invalidated this is **wrong** — the renumbering touched FI-32..35, not the FI-17/22/23/26–30 items F-03 named. → F-11 |
| F-04 risk register | **still valid — materially strengthened** | `git log` proves zero edits since creation across four gate sign-offs. Adds R-04 and R-06 and A-02's passed checkpoint. → F-05 |
| F-05 live-TIA regression gap | **still valid — unchanged and now wider** | No corpus growth; silent exclusion confirmed; new: 34-file `PlantAutoControl-bench` uncovered. → F-20, F-19 |
| F-06 ADR-0005 self-contradiction | **still valid, byte-for-byte unchanged** | → F-27 |
| F-07 telemetry `manual:` row | **still valid, both halves** | Folded into F-04 — the same build-order table is wrong about four other stages |
| F-08 telemetry "not adopted yet" | **still valid, materially worse** | +6 logs / 37 rows; four skills now cite it as binding. → F-29 |
| F-09 dangling `gen/GenProject1/` | **still valid, unchanged** | → F-48 |
| F-10 build-config duplication / no CI | **still valid, verbatim** | Newly consequential: `CLAUDE.md:21` now *asserts* CI exists. → F-38, F-15 |
| F-11 `PartNode`/`DbMember` growth | **still open, but premise now false — recommendation reversed** | Both flat at 14/14 across 16 days and 55 commits. → F-51, **accept** |
| F-12 orphan branches | **still valid — prior premise was wrong** | Both *are* in master's line; four tests agree. Deletion provably lossless. → F-28 |
| F-13 audit charter unchangelogged | **still open — now answerable** | Three-for-three across five weeks; the convention just isn't written. → F-47 |
| F-14 `extract/README.md` pytest | **still valid, unchanged** | → F-31 |
| F-15 `00-README` to-dos | **still valid — and one is now actively false** | → F-32 |
| F-16 `.xml` carve-out fragility | **overtaken in part — structure arrived, harm did not** | 48 files now outside the carve-out; 0 legitimately-editable wrongly blocked. → F-50, **accept** |
| F-17 `docs/13` DRAFT | **still valid — banner was wrong, gap widened** | The live-run change did not supersede the premise; it made it worse. Severity raised low → medium. → F-16 |
| F-18 direct-to-master subagent commit | **overtaken by events — did not recur** | Every track since landed via a merge commit. Closed. |
| F-19 leak grep | **re-derived: no breach; a new definitional gap** | Committed real names remain explicitly approved. The new question is the two regimes' relationship. → F-17, F-53 |
| *Prior positive:* "zero bare `InvalidOperationException` as domain errors" | **incorrect — was already false on 2026-07-20** | 19 in `OpennessGateway.cs`; zero commits to `src/openness-cli/` since. → F-09 |
| *Prior positive:* "`docs/15` build-order table matches the filesystem exactly" | **inverted** | Now wrong about all four rungs. → F-04 |

## Hand-offs (out of audit lane)

Items needing a block opened, explained or reviewed — dispatched to `lad-coder` / the review skills, not audit
findings:

1. **Whether the 3 Hopper artefacts and the 34 `ir/PlantAutoControl-bench/` blocks can be exported** — answering F-19
   and part of F-20 needs an import/compile/export cycle against a scratch project. The answer determines
   whether F-19 is a one-paragraph doc fix or a corpus-growth project; worth asking the owner before any fix
   session.
2. **Whether rerun3's 5 `member-not-found` results represent invented members**, and whether
   `gen/PlantAutoControl-bench/PlantAutoControl.ir` — the one rerun-era artifact that did produce ladder, predating
   the fix — contains a member laundered by the old root-only `tagstatus`. Re-running `tagstatus`/`preflight`
   with the fixed binary answers it.
3. **Whether `gen/PlantAutoControl-bench-rerun3/`'s committed artifacts conform to the tightened rung contracts**
   made after that run exposed the gaps.
4. **The FI-38 "gate rule" half** — confirming that *"a functional 'partial' that drops a stated interlock is a
   blocking fail"* is actually enforced needs a real run's output read against the skills. Verify before
   rewording its status under F-13.
5. **F-48's re-export**, which also touches the `ExportDriftDetectorTests` known-drift baseline.

## Outcome + process recommendations

**The project's capability is in good shape and its records are not.** Every capability the docs describe
exists; the tooling is well-built, well-tested and — in the newest code — unusually well-reasoned about its own
limits. All 53 findings are documentation, governance or contract drift. No incorrect claim about what the
project *can do* was found; the failure is uniformly in the other direction.

**One cluster is different in kind and should be treated first.** F-01, F-02 and F-03 are not records drift —
they are three independent ways the **mechanical floor can fail to fire while appearing to pass**: a skill whose
documented output format its own parser rejects, a permitted binary that predates the checks and prints usage
instead of erroring, and a consumed artefact filename that nothing produces so the consumer silently takes its
weaker path. The floor was built specifically because *"an AI reviewer reading the same register as the AI coder
is a correlated check"*. Each of these restores exactly that correlation, because in each case the thing that
should independently object simply doesn't run. With a live engineering job at spec-review stage right now, these
three are the time-sensitive items on the list.

**The dominant failure mode has escalated from line granularity to merge granularity.** The prior audit named
status-layer-vs-narrative-log drift. This round shows the same thing one level up: the 2026-08-05 merge landed
~25 commits of real capability and updated `CLAUDE.md`, `docs/16`'s entry bodies and `S6-Killer-Plan.md` — but
did not touch `docs/15`, `stage-gates.md`, `docs/evidence/stage-S6.md`, `docs/16`'s status sections,
`AITODO.md`'s merge state, or `CHANGELOG.md`. Eleven findings trace to that single omission.

**The prior audit's process recommendation is now empirically vindicated, and should be acted on rather than
repeated.** Fourteen of nineteen prior items re-derived as still valid; the hand-maintained status layers in
`AITODO.md`, `docs/16`, `stage-gates.md` and the two READMEs have now failed at two consecutive audits. A
mechanical check would retire the whole class — even something as small as a test asserting that every
`IMPLEMENTED` FI in `docs/16` has its subcommand present in `CLAUDE.md`'s Commands block and absent from
`AITODO.md`'s open list. Two structural additions would do most of the work:

1. **Make status derive from one authoritative place, or assert it mechanically.** The `Rules.cs` count is the
   worked example of why: three documents stated it, two stated it wrong, and the last audit's correction was
   *also* wrong because it counted the easy thing (mentions) rather than the real thing (dispatches). A
   four-line test over `AllRuleIds.Length` would have made all three impossible.
2. **Fold "CHANGELOG entry" into the commit-time checklist that already reliably catches `CLAUDE.md` + README +
   `docs/16`.** The pattern `docs: … — README/CLAUDE.md/skill/FI-doc/CHANGELOG` appears in commits on
   2026-07-20 and then never again. The habit existed and lapsed; it does not need inventing, only restoring.

**A charter note for the owner.** The charter says an audit "writes **only** its own two output files", and also
says to add a row to the Record of audits when one lands. Those instructions conflict. This round wrote the two
files and added the Record row, treating the row as bookkeeping about the audit rather than a change to audited
content — but the charter should say which it means.

**Test baseline re-statement (read-only run, nothing changed):** converter 707 · openness-cli 122 ·
golden-harness 37 = **866 passed, 0 failed, 0 skipped**.
