# 16 — Future Ideas

Candidate ideas under debate: analysed here for merits and costs until they earn a verdict. This doc fills the gap between the suite's other homes for scope — it holds what is *not yet decided*. Committed work lives in `02-roadmap.md`; excluded work lives in `10-non-goals.md`; in-flight work lives in `AITODO.md` (ephemeral by design). An idea is never silently deleted from here: every entry ends with a recorded verdict or an explicit revisit trigger.

## Lifecycle

Statuses: **Raised → Under debate → Accepted | Rejected | Parked**.

- **Accepted** — promotion into the plan requires an ADR (`docs/adr/`), consistent with `10-non-goals.md`'s "revisit only via ADR" rule, followed by the roadmap/scope edit. The entry stays here with a pointer to the ADR; the ADR is the decision record, this entry is the analysis that led to it.
- **Rejected** — verdict and reason recorded in the entry. If the rejection is a "never", the item also moves to `10-non-goals.md` (with the ADR, per that doc's own rule); a plain "not needed" stays here.
- **Parked** — neither pursued nor dead. Must carry an explicit **revisit trigger** (an event, not a date): what would have to become true for the debate to reopen.

IDs are `FI-xx`, citable the same way as `R-xx` (risks), `C-xxx` (conventions), and `E-xx` (explanation checklist). Numbers are never reused.

## Entry template

```
### FI-xx — Title
- **Status:** Raised | Under debate | Accepted (ADR-NNNN) | Rejected | Parked
- **Raised:** YYYY-MM-DD · **Source:** where it came from
**Merits:** what it buys.
**Costs / risks:** what it costs, what could go wrong.
**Dependencies:** stages, tooling, or decisions it waits on.
**Verdict / revisit trigger:** the outcome, or what reopens the debate.
```

## Implementation status — 2026-07-20

A large build wave landed on 2026-07-20 (a value/leverage survey of this doc → three tiers, built with
parallel subagents). Current state (each entry below carries its own authoritative status; this is the index):

- **Implemented (tooling shipped):** FI-13 `preflight`, FI-14 compile-error playbook, FI-15 `digest`,
  FI-16 telemetry, FI-19/FI-20 doc splits (all earlier) — plus the **2026-07-20 wave**: FI-24 `tagstatus`
  (tag-status half; gen-architecture adopts it), FI-29 `reuse-scan`, FI-30 `target-scan`, FI-26
  `drift-check`, FI-27 `preflight` flow-order check, FI-28 `openness-cli portal-status`, FI-22
  `cross-check`, FI-23 `digest --fingerprint`, **FI-25 `trace`** (forward-pass tracer, v1), **FI-22
  aliasing follow-up** (dead-wiring now covers interface-UDT members). Skills wired to the new tools
  (explain-plc-block, review-conventions, review-functional [incl. `trace`], gen-architecture).
- **Partial:** FI-09 — C-408/C-001, **C-103/C-121 inline**, and **C-118** (cross-file UDT, 2026-07-20) are
  mechanical `converter review` checks; the audit re-scoped the rest (C-107/C-402 subsumed by FI-22's writer
  table; C-125 blocked on un-mechanized C-122; C-119/120/122 need AI sequencer-identification first). FI-25 —
  the disarmed (write-condition) and timing (s→ms) hops are documented v2. FI-24 — provenance-header wrapper
  still open.
- **Open, actionable next (no external gate):** FI-17 explanation sidecars (pilot); FI-25 v2 hops (disarmed
  needs the graph to carry write conditions; timing needs new dataflow analysis); FI-09 C-119/120/122 (need a
  sequencer-identification seam).
- **Gated (waiting on a stage/event/measurement):** FI-08 (first real S6 tag-proposal loop), FI-11
  (`generate` orchestrator), FI-12 (FI-16 measurement), FI-18 (owner scoping), FI-06 (S8 grounded example),
  FI-10 (S5), FI-02 (real OB content), FI-01 (S9), FI-03 (grounded example).
- **Parked / deferred:** FI-05 (blind target — `deferred-items.md` D-5), FI-07 (owner-parked; FI-28 is its
  read-only half), FI-31 (against telemetry discipline). Related: `deferred-items.md` **D-7** — the 6 stale
  `simatic-ml/test-project001` exports FI-26 surfaced (needs a live-Portal re-export).

## Prioritization snapshot — 2026-07-16

**Superseded by "Implementation status — 2026-07-20" above and by each entry's own status — kept as the
original ranking record.** A point-in-time ranking, not a living order: it reflects the entries and project state as of this date and is not maintained as verdicts land. Method: each idea was ranked twice (ease of implementation; logical implementation order given dependencies and leverage), score = sum of the two positions, lowest first, ties broken by logic position. An idea hard-gated by another FI is nested under its gate instead of holding its own slot; external gates (stages/events) are noted inline. FI-04 excluded (Rejected).

1. **FI-16** telemetry — 1+1 = **2**
   - ⛓ **FI-12(a)** batch import/compile — 7+6 = **13**: build only once FI-16's data shows project-open cost dominates
     - ⛓ **FI-12(b)** Portal daemon — 15+13 = **28**: only if the bottleneck survives batch mode
2. **FI-14** compile-error playbook — 2+2 = **4**
3. **FI-13** static pre-flight gate — 4+4 = **8**
4. **FI-07** Portal-instance janitor — 5+5 = **10**
5. **FI-05** blind-draft gap — 9+3 = **12**
6. **FI-15** block digests — 6+8 = **14**
   - ⛓ **FI-17** explanation sidecars — 3+9 = **12**: scores better than its gate, but building the analysis cache before FI-15 settles the derived-file hygiene rules would invent them twice
7. **FI-09** convention rules Phase 2 — 12+7 = **19** · external gate: `review-conventions` in real use
8. **FI-08** proposed-tags approval path — 10+10 = **20** · external gate: first real S6 tag-proposal loop
9. **FI-11** presentation bundler — 8+12 = **20** · external gate: `generate` orchestrator design
10. **FI-06** edge-detection pattern kind — 11+11 = **22** · external gate: grounded example (S8 harvest)
11. **FI-10** HMI alarm exports — 13+14 = **27** · external gate: S5 extractors verified
12. **FI-02** OB1 round-trip — 14+16 = **30** · external gate: real production OB content
13. **FI-01** pattern testing hook — 16+15 = **31** · external gate: S9 opens (R-07)
14. **FI-03** Modbus multi-instance — 17+17 = **34** · external gate: grounded example

**Parallel-safe subset (same date).** With the S6 pipeline build-out active (test-project001 retrospective, C-6xx rules, reviewer skills — `15-generation-pipeline.md` build order), the items implementable *now* without touching that work stream or its live resources: **FI-14** (new notes file, curation only), **FI-16** (format/convention definition; instrumentation itself happens in pipeline runs and lands with them), **FI-13** and **FI-15** (purely static converter tooling, verifiable against the committed `ir/reference/` corpus with no Portal session — additive code, low merge risk). Excluded despite good scores: **FI-07** and **FI-12(a)** (meaningful verification manipulates live Portal processes/projects — the single shared resource), **FI-05** (works the same patterns and validation corpus the S6 stream is actively using).

## Ideas

### FI-01 — Pattern testing hook (S9 sim harness)
- **Status:** Parked
- **Raised:** 2026-07-16 · **Source:** `AITODO.md` "Carried forward"; the S6 FB/FC pattern redesign dropped the `tests/` folder from `docs/07-pattern-library-spec.md`'s pattern anatomy entirely.
**Merits:** Patterns are the trust anchor of generation (`04-design-philosophy.md` §6); a per-pattern simulated test would let admission criteria include behavioural evidence, not just round-trip and compile evidence, and would give S9 ready-made subjects.
**Costs / risks:** Meaningless until a simulator exists — S9's entry is blocked on the PLCSIM story (R-07, S7-1200 G2 support unconfirmed). Designing a testing hook now would be speculation against an unresolved simulation substrate.
**Dependencies:** S9 entry criteria (S6/S7 in use, R-07 resolved).
**Verdict / revisit trigger:** Revisit when S9 actually opens — decide then whether patterns want a testing hook, not before.

### FI-02 — `Main` (OB1) round-trip support
- **Status:** Parked
- **Raised:** 2026-07-16 · **Source:** `AITODO.md` "Deliberately deferred"; partial OB support already exists (`SecondaryType`, `Informative`/`InformativeComment` fixed; `Output`-section validity still open).
**Merits:** Would close the one block in `SampleProject` that doesn't survive the full cycle, making round-trip coverage literally total.
**Costs / risks:** Each fix so far has revealed another narrow OB-specific Interface-section quirk — an open-ended tail of edge cases. `Main` is TIA's own auto-generated template block, not restricted content, and OB support was never a stated project goal; the payoff is completeness, not capability.
**Dependencies:** None technical — purely a cost/benefit call.
**Verdict / revisit trigger:** Deferred per the project owner's own call. Revisit if real production OB content (not TIA's template) ever needs to pass through the pipeline.

### FI-03 — Modbus multi-instance form
- **Status:** Parked
- **Raised:** 2026-07-16 · **Source:** `AITODO.md` "Deliberately deferred"; considered during reference-corpus growth and deliberately not attempted.
**Merits:** Would extend `Modbus_Master`/`Modbus_Comm_Load` coverage (both already built and unit-tested in single-instance form) to the multi-instance shape, for one FC's worth of optional corpus coverage.
**Costs / risks:** Would have stacked two independently-unproven assumptions at once — exactly the kind of ungrounded construction this project's discipline exists to prevent. No real example exists to ground against; the standalone-instance-DB live-verification gap (a confirmed general Openness limitation, `docs/notes/openness-quirks.md`) compounds the uncertainty.
**Dependencies:** A real, grounded example.
**Verdict / revisit trigger:** Revisit only if a real grounded example of Modbus multi-instance usage ever turns up (a closed engineering call, carried verbatim from `AITODO.md`).

### FI-04 — `WAIT`/`Jump` converter support
- **Status:** Rejected (not needed — project owner's call, 2026-07-14)
- **Raised:** 2026-07-16 · **Source:** carried from S1's sign-off as open questions; closed at the S2 gate. Full grounding preserved in `ir/SPEC.md`'s own entries for each.
**Merits:** Would close the last two instructions from the full `JOB9002` inventory sweep that were investigated but never shipped.
**Costs / risks:** `WAIT` hit "instruction cannot be found" against `SampleProject` during live verification (likely a missing library/technology-object dependency, never identified). `Jump` needs a real IR-format design decision — its `label` port references a new `Access Scope="Label"` node shape, and the jump target is a network-level `<Labels>` element in a *different* `CompileUnit` than the `Jump` Part: genuine cross-network control flow, which nothing in the current IR models.
**Dependencies:** For `Jump`, an IR design decision (ADR-worthy); for `WAIT`, identifying the missing `SampleProject` dependency.
**Verdict / revisit trigger:** Not needed — resolved rather than deferred. The grounding in `ir/SPEC.md` was deliberately kept in case either becomes relevant again; a real production block using either instruction reopens this.

### FI-05 — Close `chained-permissive-enable`'s blind-draft gap
- **Status:** Deferred (owner ruling, 2026-07-17 — `docs/notes/owner-questions.md` D-5; tracked
  in `docs/notes/deferred-items.md`). Analysis below stands; timing is the owner's call.
- **Raised:** 2026-07-16 · **Source:** `AITODO.md` "Carried forward"; S6 unlock — pattern admission criterion 3 accepted on partial evidence (drafted with prior knowledge of the real answer, not blind), recorded as an open gap rather than silently closed.
**Merits:** A genuinely blind drafted instance is the missing proof that the pattern generalizes; worth having before leaning on this pattern heavily in S6 generation. The earlier blind attempt against JOB9002's station_1 `PlantAutoControl` also surfaced a structurally different case (VSD, edge-detected fault-reset) the pattern doesn't document yet — closing the gap likely improves the pattern itself.
**Costs / risks:** Needs a suitable blind target: real, untouched, and within the recorded data-boundary approval. Low tooling cost; the cost is finding honest test material.
**Dependencies:** None — S6 is open; this was explicitly judged "worth closing before leaning on this pattern heavily, not before S6 can start."
**Verdict / revisit trigger:** Nearest-term entry in this doc. Live tracking stays in `AITODO.md`; this entry records the analysis and will take the verdict when the gap is closed or consciously waived.

### FI-06 — Constructed edge-detection as a fourth pattern kind
- **Status:** Under debate
- **Raised:** 2026-07-16 · **Source:** `docs/07-pattern-library-spec.md` names it explicitly as the example of a shape outside the current three kinds (a cross-block micro-idiom recurring via shared storage, C-402/C-404); FI-05's blind attempt also hit an edge-detected fault-reset case the current patterns can't express.
**Merits:** It is the already-identified next gap in the pattern taxonomy, and `15-generation-pipeline.md` names thin pattern coverage as test-project001's cause 3 — every kind added shrinks the freeform surface. The FI-05 gap and this one likely close together.
**Costs / risks:** Needs a real grounded example to document from, per pattern-spec discipline; a cross-block idiom needs its own shape defined (the spec says don't force it into an existing kind), which is design work, not just documentation.
**Dependencies:** A grounded example; possibly the S8 harvest step surfacing one from a real build.
**Verdict / revisit trigger:** Open — a natural candidate for the first S8 harvest that touches edge-detected logic.

### FI-07 — `openness-cli cleanup`: a Portal-instance janitor
- **Status:** Parked (owner, 2026-07-16) — "trust is user-side" for now (the manual tasklist
  mitigation stands); the project is at prototype stage, and further CLI/Portal churn could
  invalidate careful janitor testing while fresh test subjects are scarce. **Revisit trigger:**
  post-prototype — when the Portal-session tooling stabilizes (e.g. FI-12's fate is decided) and
  real generation work is producing enough Portal sessions to test against.
- **Raised:** 2026-07-16 · **Source:** CLAUDE.md environment notes — stale Portal-process pileup is the confirmed correlate of "second instance won't connect" (2026-07-14 stability audit), with a manual `tasklist`-and-close mitigation.
**Merits:** `LaunchedInstanceRegistry` already knows which processes the tool launched, so a command that lists/closes only its own idle instances automates the standing mitigation without ever touching a human's window.
**Costs / risks:** Process-killing is the CLI's second irreversible operation; needs the same `--yes` dry-run/confirm pattern as `delete`. "Idle" needs a safe definition — a Portal mid-compile must never be killed.
**Dependencies:** None — the registry and the safety pattern both exist.
**Verdict / revisit trigger:** Open.

### FI-08 — Engineer-side approval path for proposed tags
- **Status:** Under debate
- **Raised:** 2026-07-16 · **Source:** hard rule 3 plus `15-generation-pipeline.md`'s tag-status rule (`exists` | `proposed`); `gen-io-tags` already emits a proposed tag-table IR.
**Merits:** The pipeline produces `proposed` tags but the promotion path — engineer reviews, creates, re-exports — is entirely manual. A defined handoff (the proposed tag-table IR as a reviewable, importable artifact the *engineer* imports after approval) would shorten the most human-bound step of the S6 loop.
**Costs / risks:** Must not blur rule 3's bright line: the engineer's explicit import action has to remain the only path from `proposed` to real. Any convenience that makes the import automatic launders the invention.
**Dependencies:** Real S6 use showing where the friction actually is; `gen-io-tags` (pipeline build-order step 6).
**Verdict / revisit trigger:** Debate properly once a real generation project has been through the tag-proposal loop at least once.

### FI-09 — Convention rules Phase 2: mechanize more of the remaining ~42
- **Status:** Parked
- **Raised:** 2026-07-16 · **Source:** S4 Phase 1 shipped 8 of ~50 rules as mechanical checks; the rough re-estimate of the rest is in `docs/evidence/stage-S4.md`. `review-conventions` (pipeline skill 9) wraps `converter review` plus an AI pass over the non-mechanical rules. Specificity below added 2026-07-18 from a read-only skill/tooling audit (preserved in git history, commit `eede95d`, then folded here).
**Merits:** Each mechanized rule is a deterministic, zero-hallucination check forever, and directly shrinks the AI-judgment surface `review-conventions` has to carry. Each landed rule *auto-retires the matching hand-sweep* via `review-conventions`' self-retiring NotApplicable clause — so growing the tool shrinks the skill with no skill edit per rule.
**Which rules pay off (2026-07-18 audit).** `review-conventions` spells out exact mechanical recipes that are pure structural checks on the already-parsed IR model — the highest-leverage genuinely-mechanizable: **C-121** (a Step-write must be a `MOVE` whose `EN` carries `Step = <from> AND …`); **C-118/119/120/122/125** (sequencing structure — one `Int` Step in the interface UDT, step 0 present and explicitly returned to, ascend-by-10, dwell-timer shape); **C-408** (`.ET` inside a comparison — trivial over `Ir/TagReferences.cs` / `Expr.Compare`); **C-103** (SCOIL/RCOIL set-vs-reset pairing — `CoilAssignment.Kind` already carries Assign/Set/Reset); **C-107/C-402** (within-block edge-memory single-writer discipline); **C-001** non-prefix (PascalCase / no-underscore on members and variables). Each follows the existing one-method-per-rule pattern in `Rules.cs` + a `ReviewRulesTests.cs` fixture. `AITODO.md`'s F-3(b) (C-003's `iDB_<FBName>_<Instance>` sub-clause) is one such gap already flagged.
**Boundary — do not over-mechanize (same audit).** All bucket-C judgment rules (C-113 paradigm choice, C-504 suppression intent, C-002/C-004 semantic naming, the one-reading test), blindness/regime disposition, owner-ruling routing, and the report split stay AI. Scripting these manufactures false confidence — worse than the hand-work.
**Costs / risks:** The re-estimate found genuine severity/checkability mismatches — some rules are not mechanical at any reasonable cost; diminishing returns are real.
**Dependencies:** `review-conventions` in real use (pipeline build-order step 3).
**Verdict / revisit trigger:** Revisit when `review-conventions` usage shows which rules actually fire often enough to be worth mechanizing. **Rules landed 2026-07-18:** **C-408** (`.ET` inside any comparison, `Rules.CheckC408EtComparison`, on the new `TagReferences.AllExpressions` seam) and **C-001** (member/variable PascalCase/underscore-free, `Rules.CheckC001MemberNames` — DB incl. iDBs, UDTs via a now-checked TYPE path, and block interface variables; tag tables exempt). Both are deterministic `converter review` checks now; CHANGELOG has detail. **Rules landed 2026-07-20:**
**C-103** (Set/Reset pairing, Warn — `Rules.CheckC103SetResetPairing`; candidate-worded, the
external-set/internal-reset reusable-FB exception is cross-block/judgment) and **C-121 inline form**
(Step-transition = MOVE whose EN carries `Step = <from>`, Error — `Rules.CheckC121StepTransition`;
the named-equivalent-bit form stays AI). **A 2026-07-20 audit refined the rest:** C-107/C-402
(edge-memory single-writer) is subsumed by FI-22's C-308 multi-writer table (built — don't duplicate
in `Rules.cs`); C-118/C-125 need **cross-file UDT resolution** (FI-22's ProjectIndex direction, not the
per-file `Rules.cs` seam); C-119/C-120/C-122 are structurally mechanizable only once an upstream AI
judgment (C-113) identifies the block as a stepped sequencer — all deferred with dependencies recorded. **C-118 landed 2026-07-20** — the
first cross-file review rule: `converter review --project <ir-dir>` builds a `TagTypeRegistry` UDT/DB index
and `Rules.CheckC118StepInterfaceUdt` verifies the phase is exactly one `Step:Int` in the block's interface
UDT (flags bare-Static / `DB_Controls`/`DB_Settings` placement / non-Int; NotApplicable without `--project`).
C-125 stays deferred (blocked on the un-mechanized C-122).

### FI-10 — HMI-importable alarm exports from S5
- **Status:** Parked
- **Raised:** 2026-07-16 · **Source:** S5's roadmap entry — extractors "feed existing manual work (HMI alarm tables, O&M manuals, IO checklists)".
**Merits:** One step past CSV/XLSX: emitting a WinCC-importable alarm file turns a documentation byproduct into direct labor savings on existing manual work.
**Costs / risks:** Sits at the edge of the HMI "not now" in `10-non-goals.md` — the line to hold is data export, not HMI engineering. S5 hasn't started; committing now would be premature.
**Dependencies:** S5's basic extractors proven (its exit criterion).
**Verdict / revisit trigger:** Revisit when S5's alarm extractor exists and is verified.

### FI-11 — Presentation-bundling tool for the final gate
- **Status:** Raised
- **Raised:** 2026-07-16 · **Source:** the S6 presentation format (IR diff + intent + compile evidence + reviewer findings) is assembled by hand each time; `15-generation-pipeline.md`'s `generate` skill (13) now owns final presentation.
**Merits:** A small tool that bundles the presentation into one reviewable, archivable artifact gives the engineer of record a uniform audit trail.
**Costs / risks:** Largely absorbed by pipeline skill 13 — the residual idea is only the packaging tool that skill would call. Easy to build too early, before real reviews show what the engineer actually wants in the bundle.
**Dependencies:** The `generate` orchestrator's design (pipeline build-order step 8 — deliberately last).
**Verdict / revisit trigger:** Fold into skill 13's build when it happens; reject as a separate tool if the skill's plain-markdown presentation turns out to be enough.

### FI-12 — Persistent Portal session for the import–compile inner loop
- **Status:** Raised
- **Raised:** 2026-07-16 · **Source:** CLAUDE.md — "TIA project open is slow — be patient"; every `openness-cli` invocation currently pays project-open cost, and `gen-block-coding`'s contract is iterate-until-clean, i.e. many round trips per block.
**Merits:** The dominant latency in the generation inner loop is Portal/project open, not compile. Two designs, same goal: (a) a batch subcommand (import N files, compile, report — one open per iteration set; `RunAllSettled`'s three-phase structure in `tests/golden` is the proven shape), or (b) a long-lived daemon that keeps the scratch project open across iterations. (a) is cheap and proven; (b) is the bigger win and the bigger risk.
**Costs / risks:** (b) collides with everything the 2026-07-13/14 concurrency work made safe: a crashed daemon is exactly the stale-process pileup that causes "won't connect"; single-writer-per-project must hold; crash recovery and idle shutdown need real design. `LaunchedInstanceRegistry` and FI-07 mitigate but don't erase it.
**Dependencies:** Real S6 use measuring where the time actually goes (see FI-16) — (b) should not be built on an assumed bottleneck.
**Verdict / revisit trigger:** Consider (a) as soon as a real generation project shows repeated multi-block iterations; (b) only if measurement shows project-open still dominates after (a).

### FI-13 — Static pre-flight gate before any Portal round trip
- **Status:** Accepted (owner-directed build, 2026-07-16 — tooling-side, no roadmap change, so the ADR gate doesn't apply). Implemented as `converter preflight` (`src/converter/README.md`). **Adopted 2026-07-16 (same day, Tier-1 adoption batch):** mandatory before every import in the generation inner loop — CLAUDE.md workflow step 4 + `docs/15` "Inner-loop tooling"; zero-findings bar. The remaining step is closed.
- **Raised:** 2026-07-16 · **Source:** the project's own record — recurring live-verification error classes (tag not defined, type mismatch, missing instance DB, `IsConsistent` refusals) each cost a slow Portal round trip to discover.
**Merits:** A bundled pre-import check — converter round-trip (`to-xml` succeeds), `converter review`, tag-existence grep against the current export (the pipeline's tag-status rule already mandates the grep), instance-DB presence for every CALL — would catch the common failure classes in milliseconds instead of a Portal cycle. Raises the odds the *first* import compiles clean, which compounds with FI-12: fewer round trips, each cheaper.
**Costs / risks:** Must never be presented as the compile gate (hard rule 4) — it is a filter in front of it, not a substitute. Risk of duplicating TIA's compiler badly; scope must stay at the known, recurring error classes.
**Dependencies:** None — every component check already exists; this is composition plus a CLI entry point.
**Verdict / revisit trigger:** Open — cheap enough to pilot during the next real generation build.

### FI-14 — Compile-error playbook: known TIA errors → proven fixes
- **Status:** Accepted (owner-directed build, 2026-07-16 — docs-side, no ADR needed). Implemented as `docs/notes/compile-error-playbook.md` (19 seeded entries); grows as encountered. **Adopted 2026-07-16:** the inner loop's first lookup on any compile failure (CLAUDE.md step 4 + `docs/15`), entries treated as grounded hypotheses to verify.
- **Raised:** 2026-07-16 · **Source:** `docs/notes/stage-gates.md` and `openness-quirks.md` already contain dozens of hard-won error→fix pairs (PORT needs the system datatype, `Clock_0.5Hz` registration, instance-DB gaps, `IsConsistent` cascades…), but they're narrative — each new session re-derives the diagnosis.
**Merits:** A structured lookup (error signature → confirmed cause → proven fix → source reference) would make `gen-block-coding`'s iterate-until-clean loop converge in fewer iterations and fewer tokens, and stop re-spending investigation effort on already-solved errors.
**Costs / risks:** Curation cost, and a staleness risk: a playbook entry wrong for a new context is worse than no entry — every entry must cite its grounding, and the loop must treat entries as hypotheses to verify, not answers to trust blindly.
**Dependencies:** None — the raw material exists; format and harvest discipline are the work.
**Verdict / revisit trigger:** Open — could start as a section in `openness-quirks.md` and only become tooling if lookup by hand proves too slow.

### FI-15 — Generated block digests for cheap structural context
- **Status:** Accepted (owner-directed build, 2026-07-16 — tooling-side, no ADR needed). Implemented as `converter digest` (`src/converter/README.md`). **Adopted 2026-07-16:** per-stage policy decided in `docs/15`'s isolation model — reviewers always full IR; analysis/design/entry stages may digest for orientation; build stages read full IR of what they touch.
- **Raised:** 2026-07-16 · **Source:** the S2 multi-agent experiment (`docs/notes/stage-gates.md`) — explanation quality didn't track context volume; pipeline stages and reviewers often need a block's *shape* (interface, call graph, network titles, tag roots), not its full IR.
**Merits:** A `converter digest <file>` emitting a compact per-block summary would let analysis/design stages and reviewers load structure at a fraction of the tokens, reserving full IR reads for the blocks actually under edit or review. Directly serves the pipeline's isolation model (artifact-only contexts).
**Costs / risks:** Staleness is the killer — the `TimerSample` stale-sidecar bug is this project's own proof that derived files rot. Digests must be generated on demand or verified against the IR's hash at read time, never hand-maintained. Also a subtle quality risk: a reviewer that *should* read every rung (the S2 lesson: exhaustiveness, not summaries, caught the real bugs) must not be handed a digest instead.
**Dependencies:** None technical; the pipeline's per-skill contracts should state digest-vs-full-IR explicitly per stage.
**Verdict / revisit trigger:** Open — decide per pipeline stage, not globally; the reviewers likely stay on full IR by design.

### FI-16 — Per-stage token and latency telemetry with budgets
- **Status:** Accepted (owner-directed, 2026-07-16 — convention only, no ADR needed). Format defined in `docs/notes/gen-telemetry.md`. **Adopted 2026-07-16:** `docs/15` and CLAUDE.md now mandate one line per stage run (including `manual:<stage>` and abandoned runs) in `gen/<project>/telemetry.log`; first rows land with the next real generation run — no backfill.
- **Raised:** 2026-07-16 · **Source:** the S2 experiment measured token cost against explanation quality once, by hand, and it changed the design (exhaustiveness over context volume). The pipeline now has thirteen skills whose isolation choices are currently judgment calls.
**Merits:** Recording tokens and wall-clock per skill run (a line per run in the `gen/<project>/` artifacts or a simple log) would ground decisions this doc keeps deferring to measurement: whether FI-12(b) is worth its risk, which stages deserve subagent isolation, where FI-15 digests actually pay. Budgets follow once baselines exist.
**Costs / risks:** Low, but only if it stays a log, not a dashboard — the anti-goal is instrumentation polish that outruns the two-project sample size. Latency data is confounded by Portal state (see the pileup findings); record enough context to interpret it.
**Dependencies:** Real pipeline runs to measure — starts paying with the very next generation project.
**Verdict / revisit trigger:** Open — cheapest of this batch; a candidate to adopt informally (manual notes per run) before building anything.

### FI-17 — Explanation sidecars: cached AI block explanations, keyed to IR hash
- **Status:** Raised
- **Raised:** 2026-07-16 · **Source:** conversation — the analysis-cache counterpart to FI-15's mechanical digests. Precedent on both sides: ADR-0004's pipeline artifacts are exactly "one expensive AI pass, written down, consumed downstream" (with `audit-artifact` as the safety net), and S2's explanation capability already produces the content.
**Merits:** One exhaustive AI read per block *version*, cached beside the IR with the hash it was derived from, reused across sessions and stages for orientation — "which block do I need to open?" — instead of re-reading full IR every time. Saves tokens and latency on the most repeated read in the project.
**Costs / risks:** Cached AI judgment that is subtly wrong poisons every consumer that trusts it instead of looking — plausible-looking errors, unlike a mechanical digest's detectable ones. Hard constraints: invalidated by IR hash on any change (the `TimerSample` stale-sidecar bug is the mild mechanical precedent); orientation use only, **never review input** — reviewers read full IR per the S2 exhaustiveness lesson; an explanation is a claim about the block, not ground truth.
**Dependencies:** None technical — the S2 explanation skill exists; needs only a storage convention (sidecar with derived-from hash) and the use-boundary written down.
**Verdict / revisit trigger:** Open — pilot by caching the explanations already produced during normal work before building any tooling for it.

### FI-18 — HMI Interface creation skill (defines the PLC/HMI boundary)
- **Status:** Raised
- **Raised:** 2026-07-17 · **Source:** `docs/notes/owner-questions.md` Q-03 — the owner's response to the `DI16_PSH_LocalRemote` selector-semantics question didn't settle the selector, but proposed a process fix instead: "These are HMI features, Perhaps we should do a HMI Interface creation skill so we can fully define the PLC boundary which the system will squarely work in."
**Merits:** Several open test-project001 questions (Q-03 selector semantics, Q-13 reversal display/flashing lamp, Q-14 "control on" step, Q-15 hand/jog mode overlay, REQ-060/064/068's HMI bindings) are really HMI-design questions bleeding into the PLC-side register because there is no dedicated place to resolve them. A skill (or even just a defined artifact, `hmi-interface.md`) that pins down exactly what the PLC exposes — tags, value ranges, mode legends — and what's purely HMI-side would give `gen-architecture` and the register a clean boundary to design against instead of guessing at operator-facing intent.
**Costs / risks:** Scope creep risk — this project's hard rules keep HMI engineering itself out of scope (`10-non-goals.md`'s "not now" line, per FI-10's note); the skill would need to stop at *defining the boundary/contract*, never at building HMI screens. Also competes for build-order slots against the S6/S7 skills already queued (owner-questions A-2/A-4).
**Dependencies:** None technical. Best timed against a project that actually has open HMI-boundary questions blocking it — test-project001 already does (Q-03/13/14/15).
**Verdict / revisit trigger:** Open — revisit once the S6/S7 coding-skill priority (owner-questions A-4) is settled and there's room to scope this; in the meantime Q-03 etc. stay open, answered by the owner directly rather than waiting on this skill.

### FI-19 — `docs/evidence/` split: raw verbatim transcripts out of `docs/notes/`
- **Status:** IMPLEMENTED 2026-07-18. The §3 verbatim blind-run transcripts moved to `docs/evidence/review-conventions-blind-run-2026-07-16.md` and `review-functional-blind-run-2026-07-16.md`; the two notes dropped from 1,119→175 and 972→129 lines, now just §1/§2 (comparison + verdict) plus a stub linking to the evidence file. Split by script (verbatim body untouched; line counts reconciled exactly). `review-simplicity-validation` (285→85 lines) was split too for pattern-consistency — its §3 (207 lines) → `docs/evidence/review-simplicity-blind-run-2026-07-16.md`. All three validation docs now follow the same shape (note = §1/§2 + link; transcript in `docs/evidence/`). Standing convention now adopted: large verbatim transcripts (blind-run reports, full tool dumps) live in `docs/evidence/`, never inline in notes or `stage-gates.md` (see FI-20). **Baked into the three review skills** (`review-conventions`/`review-functional`/`review-simplicity`, "Persisting this report" in each Report-structure section, 2026-07-18): a persisted review/validation record is split at write time — verbatim block to `docs/evidence/`, lean note + link — so future records are born in this shape instead of being split by hand. The canonical statement of the convention now lives in `docs/15-generation-pipeline.md` ("Artifacts"); the skills point there, and these FI entries remain the origin/rationale record.
- **Raised:** 2026-07-18 · **Source:** conversation — a size review of `docs/notes/review-conventions-validation-2026-07-16.md` (1,120 lines) and `review-functional-validation-2026-07-16.md` (973 lines) found the bulk of each is a verbatim §3 blind-run transcript (full mechanical `converter review` output, or a full 69-REQ trace), sitting under a short §1/§2 analysis-and-verdict that's the part anyone actually reads day to day.
**Merits:** The verbatim-ness is load-bearing — the drift check needs byte-identical raw tool output, and "blind executor, non-blind examiner" needs the report unedited so a skeptic can check the grading against the agent's own words — so it can't just be trimmed. But it doesn't need to live inline either. Splitting §3 out to `docs/evidence/<name>.md` and leaving the note as analysis-plus-link would cut both files to roughly a tenth their size with no loss of traceability, and gives future validation/gate work (and possibly future `stage-gates.md` entries, see FI-20) a standing place to put large raw dumps instead of embedding them.
**Costs / risks:** Low — mechanical split, no content loss if the link is exact (commit hash or line range). Slight risk of the split becoming stale if a note is edited without checking its evidence file still matches; worth a one-line convention (evidence files are also append-only/immutable once linked, never silently edited).
**Dependencies:** None technical.
**Verdict / revisit trigger:** Open — apply retroactively to the two 2026-07-16 validation docs first as the pilot; adopt as a standing convention for any future doc that wants to embed a large verbatim transcript as proof.

### FI-20 — Slim `stage-gates.md` to a status index, move narrative to per-stage evidence docs
- **Status:** IMPLEMENTED 2026-07-18. `stage-gates.md` is now a 43-line status index (title + stage table + evidence links); the full narrative moved to `docs/evidence/stage-S0.md`…`stage-S6.md` (one per stage with recorded work; S5/S7/S8/S9 get theirs when they open). Split done by script (no content pulled into context; every one of the original 4,295 lines reconciled). Cross-references updated in the agent-followed files (CLAUDE.md, AITODO.md, `lad-coder`, the review skills) and the core rulebook/ADR; remaining references route correctly through the index's evidence-links block and were left as index-routed (not broken — `stage-gates.md` still exists). **FI-19 (splitting the two validation docs' verbatim transcripts) was NOT done here** — still open, and the `docs/evidence/` folder now exists as its landing spot.
- **Raised:** 2026-07-18 · **Source:** conversation — `stage-gates.md` is 341KB / 4,295 lines (~80–90K tokens by rough estimate), and the cost is already documented as real, not hypothetical: the `review-conventions` blind validation run needed it only for regime labels but read ~500 lines beyond the minimum section, and that note's own §2 recommends fencing future gate-grade runs to just the S4 section. First pass at a fix (chapter-splitting the file by closed stage) was rejected on further owner questioning: it still bundles two things with completely different read patterns — **status** ("which stage are we in, is X allowed" — CLAUDE.md's actual stated use, needed constantly, should be a five-second read) and **narrative** ("the full story of how a stage's gate was met, with quotes" — needed rarely, opt-in). Chapter-splitting keeps both together per stage, so the status question still costs however big the *current* stage's chapter has grown to — and a single open stage can itself balloon to thousands of lines, which is exactly what already happened with S6/S7 before this session's trim.
**Merits:** Split along the status/narrative axis instead of the time axis: `stage-gates.md` shrinks to a stage table + one-line status per stage + links (tens of lines, always cheap, exactly what CLAUDE.md's "check which stage is active" needs and nothing more); the narrative moves out to `docs/evidence/stage-<N>.md`, one per roadmap stage (S1, S2, ... S6, S7...), as verbose as it needs to be since it's read only when someone specifically wants that stage's history. Same principle as FI-19 (index vs. evidence), applied to the file's own organizing structure rather than only to embedded verbatim transcripts — worth doing together, and FI-19's transcript-splitting naturally lands inside each stage's evidence doc rather than needing its own separate location.
**Costs / risks:** A real one-time migration: every existing "S4 Phase 1" / "S6: owner-questions round 2" style entry needs sorting into its stage's evidence doc, and every live cross-reference from other docs into a stage-gates section needs updating to point at the new location (same dangling-reference risk CLAUDE.md's Environment notes already flag from the 2026-07-17 codename cleanup) — mechanical but not small, given the file's current size. Ongoing: needs a clear, boring rule for what's "status" (goes in the index, one line, updated in place) vs. "narrative" (goes in the stage's evidence doc, dated, append-only) so entries don't drift back into the index over time.
**Dependencies:** None technical. Do alongside FI-19, not before it — deciding what counts as "evidence" for the split is the same judgment call both ideas need.
**Verdict / revisit trigger:** Open — owner's call on timing; not urgent while the file is still mostly read via its tail, but worth doing before it roughly doubles again (S6/S7 in full swing), and cheaper to do sooner than after more entries accumulate to re-sort.

### FI-21 — Library-candidate harvest-assist skill (run the reviewers, clean to exceptions)
- **Status:** Raised (2026-07-18; owner idea, explicitly "tomorrow's work" — feedback captured here, not yet built).
- **Raised:** 2026-07-18 · **Source:** conversation — filling the pattern library is real, heavy legwork, and the owner is now steering S5 + library-coverage up and softening the earlier "S7 is the rush / library fills via S8 harvest only" framing. Idea: a skill that takes an existing example (a real block/excerpt), runs the three review skills against it, and iterates until only *exceptions* remain — the residue being the pattern's documented deviations.
**Merits:** Attacks the named cost (library-filling is expensive) by reusing already-validated assets (the three reviewers) instead of new review logic. The stopping point is elegant: "only exceptions left" is exactly `pattern.md`'s hardest section to write — the documented deviations / known quirks — so the loop's terminal output seeds it directly. Iterating *site* code (held to a lower bar) up to the *generated-code* reviewer bar is a legitimate way to lift a real example to library quality.
**Costs / risks — design constraints, not blockers:**
1. **Don't let "iterate to clean" erode "proven."** `docs/07`'s #1 admission rule is "instantiated in reviewed, working logic — proven, not speculative." A loop that edits the example until reviewers are happy yields a reviewer-pleasing *synthetic*, not a proven artifact. Reframe the skill as **candidate-preparation / harvest-assist**: keep the proven original as the proven artifact; emit a *candidate shape + a findings ledger + a diff*, and stop at the human admission gate (criterion 3's blind-draft proof and criterion 5's sign-off still happen after). It prepares a candidate; it does not admit one.
2. **No exception-laundering.** An "iterate to zero findings" loop's cheapest move is to relabel a stubborn finding an "exception." Classify findings into {auto-fixable-mechanical / proposed-exception / needs-ruling}; apply only the first class automatically; exceptions are *proposed*, owner-signed at admission (matches CLAUDE.md's "a consciously-accepted finding needs the engineer's explicit OK recorded").
3. **Stop at rule-vs-practice tensions, don't "fix" them.** The reviewers already surface these (e.g. C-501 packed-alarm-word) as owner-ruling items; the loop must flag-and-stop, not split a network that was right as-is. Needs a max-iteration / no-progress guard too — these tensions can oscillate (a fix re-raises another finding).
4. **Function is the weak link.** `review-functional` needs a requirements register; a harvested existing block has none. So the loop is really conventions+simplicity-clean with function *asserted*, not checked — say so in the output. Function still needs the register or the engineer.
**Mechanics:** reads/reviews/edits IR → runs **inside `lad-coder`** (hard rule 8); each review pass follows the persist-to-`docs/evidence/` convention (`docs/15` "Artifacts"). It is an **S8 accelerator** by nature; building it early is fine as tooling but it shouldn't jump the S6-exit / coding-skill queue unless the owner explicitly bumps library-coverage up (which this session's steer begins to).
**Dependencies:** the three review skills (built); `docs/07-pattern-library-spec.md`'s admission criteria (the gate it feeds, never replaces).
**Verdict / revisit trigger:** Open — owner's "tomorrow's work." Start from the candidate-prep reframe above, not the literal "iterate to clean the example" seed.

### FI-22 — Whole-project cross-check review mode (reference-graph analysis over `ProjectIndex`)
- **Status:** IMPLEMENTED 2026-07-20 as `converter cross-check --project <ir-dir>`
  (`src/converter/Converter/CrossCheck/`, `src/converter/README.md`). Substrate:
  `TagReferences.AllDirectedUsages` (a direction-tagged reader/writer extractor, sibling of
  `AllTagPaths`) + `CrossCheck/ProjectUsageGraph` (whole-export reader/writer index — **the shared
  substrate FI-25 reuses**). Emits four fact tables, verbatim facts the reviewer reasons over (never
  verdicts): multi-writer paths (C-308, with Set/Reset kind), dead global-DB members
  (review-functional Pass-2 dead-wiring, both directions), physical-IO references (C-304), per-block
  sibling references (C-127). Exit 0 (facts dump). Dead-wiring scoped to global-DB members
  (unambiguous addressing); **iDB/interface-UDT member aliasing (instance→FB correlation) was the
  one documented follow-up — **DONE 2026-07-20**: `ProjectUsageGraph` gained additive
  `InstanceToFb`/`InstanceMemberPaths` and the dead-member pass now pools writers/readers across the
  FB-internal bare form and every `iDB.<suffix>` alias, so interface-UDT members are covered
  (`[interface]` scope), no false positives on the real corpus (e.g. `FB_ShredderSequencer.IO.InCycle`,
  comment-confirmed "superseded, no longer wired"). The dead-wiring table is now complete. **Wired into the review skills 2026-07-20:** `review-conventions` Group 2
  (embeds the C-308/C-304/C-127 facts) and `review-functional` Pass 2 (the dead-member table for the
  buffer-DB / DB_Settings bullets; the interface-UDT-member bullet stays hand-checked pending the
  aliasing follow-up). Real-corpus piloted (surfaces `DB_Input.Pusher_Local_Remote` dead input, unused
  overcurrent setpoints). 5 tests.
- **Raised:** 2026-07-18 · **Source:** a read-only skill/tooling audit (preserved in git history, commit `eede95d`, then folded here). `converter review` is per-file, so the review skills do project-wide reference-graph analysis by hand — but the infrastructure already exists: `Preflight/PreflightRunner.cs` builds a `ProjectIndex` (`Preflight/ProjectIndex.cs`) over the whole export (verified 2026-07-18: `ProjectIndex.Build` / `ResolvesAsTagRoot`).
**Merits:** A `converter review --project ir/<project>/` (or a new `cross-check` subcommand) reusing that index could emit — as verbatim tool output the reviewer reasons over, same status as today's `converter review` dump, **not** a new judgment source:
- **review-conventions Group-2 cross-block tables** — C-308 one-writer table (every write destination targeting `DB_Settings.*` or an instance-UDT settings member); C-115 handshake-vocabulary table; C-127 reusable-FB sibling-reference check; C-304 IO-boundary scan (raw `%I`/`%Q` outside Map FCs). These are exactly the tables the skill's report already builds by hand.
- **review-functional Pass-2 dead-wiring** — every interface-UDT / buffer-DB member checked for readers AND writers in both directions (consumed-but-never-written / written-but-never-consumed). A whole-project reader/writer set-difference — the *in-cycle-lamp bug class* the functional reviewer exists to catch, and it is mechanical. REQ *tracing* stays AI (needs register semantics); the dead-wiring detection should not be hand-grep.
**Costs / risks:** Emit reference-graph *facts* the AI reasons over, never adjudicated verdicts (same discipline as the mechanical `converter review` dump). Keep scope to structure, not judgment.
**Dependencies:** `ProjectIndex` (built, used by `preflight`); the per-file rule methods (`Rules.cs`). Complements FI-09 (per-file rules) but is a distinct capability (cross-file).
**Verdict / revisit trigger:** Open — worth it once the reviewers are in steady real use; the highest-leverage net-new item from the audit.

### FI-23 — `explain-plc-block` structural-fingerprint helper
- **Status:** IMPLEMENTED 2026-07-20 as `converter digest --fingerprint`
  (`src/converter/Converter/Digest/NetworkSignature.cs`, `src/converter/README.md`). Adds a
  per-network normalized structural signature (SHA-256 of a canonical string that walks the statement
  lists in fixed order and canonicalizes each statement's `Expr` trees — `TagRef`→`TAG`, sorted
  And/Or operands, ordered Compare with operator — so two structurally-identical networks with
  different tags collapse to one hash; **literal values are KEPT** so a template copy that changed a
  constant is surfaced). Default `digest` output unchanged; signature shown per network only with
  `--fingerprint` (always in `--json`). Stays digest's orientation-only posture (explanation aid, not
  review input — reviewers read full IR). Piloted on `FB_ShredderSequencer` (15 distinct signatures;
  separates two step-networks a count-summary can't). 7 tests.
- **Raised:** 2026-07-18 · **Source:** the audit. `explain-plc-block`'s core method is "describe the repeating template once, then verify *every* instance — copy-paste drift is where the real findings are," done by hand across N networks today.
**Merits:** A `--fingerprint` mode emitting a normalized structural signature per network (extending `Digest/DigestBuilder.cs`, which already computes per-network statement summaries — verified 2026-07-18) would surface the outlier instance directly instead of hand-comparison, serving the skill's stated main failure mode.
**Costs / risks:** Keep it **distinct from `digest`**, which is policy-banned *as review input* (`docs/15` isolation model). This is an orientation aid for *explanation*, which is allowed — it must not become a review shortcut.
**Dependencies:** `DigestBuilder` (built).
**Verdict / revisit trigger:** Open — net-new; lower priority than FI-22 but cheap (extends existing digest machinery).

### FI-24 — `gen-architecture` bookkeeping helpers (provenance + tag-status)
- **Status:** Partially done (2026-07-18). The **tag-status half is built** — `converter tagstatus <name…> --project <ir-dir> [--json]` (`src/converter/Converter/TagStatus/`, `src/converter/README.md`): classifies each name `EXISTS`/`PROPOSED` against the export via the same `ProjectIndex` + `AccessNode.FromDottedPath` primitive `preflight` uses, exit 1 if any proposed (a usable "all tags exist" gate). The **provenance-header wrapper is still open** (below). Originally raised 2026-07-18 from the skill/tooling audit — net-new, small tooling.
- **Raised:** 2026-07-18 · **Source:** the audit. Two rote, error-prone hand-steps in `gen-architecture`:
**Merits:**
- **Provenance header** currently needs `git log -1 --format=%h -- <path>` per input; a tiny wrapper emitting the whole provenance block removes a hand-repeated, easy-to-fumble step.
- **Tag-status classification** ("every named tag `exists`/`proposed`") is exactly `ProjectIndex.ResolvesAsTagRoot`; a `converter tagstatus <names…> --project ir/<project>/` mode would let the designer mechanically classify a proposed-tag list instead of hand-grepping — the same primitive `preflight` already uses, and direct support for the anti-laundering rule.
**Costs / risks:** Low — both are small, deterministic, and reuse existing primitives.
**Dependencies:** `ProjectIndex` (built).
**Verdict / revisit trigger:** Open — small quick wins; `tagstatus` is the more useful (anti-laundering support).
**Adoption gap CLOSED (2026-07-20).** `gen-architecture/SKILL.md` now calls `converter tagstatus`
in its tag-status pass (Inputs "Tag status, always", Method step 7, section-9 output, and the
mini-manifest), with a `tagstatus:*` Bash allowance added alongside the existing `digest`/`review`
pairs and the prose "exactly two read-only commands" updated to three. One behavioural difference
from `gen-block-new` preserved: a `PROPOSED` result is an *expected, recorded* section-9 outcome
(a named gap the engineer resolves), not a run-stopping gate — the design stage designs against
gaps, it doesn't write logic. **This closes FI-24's tag-status half; the provenance-header wrapper
(above) remains open.**

### FI-25 — `review-functional` forward-pass verdict tracer (AI binding → scripted decision tree)
- **Status:** IMPLEMENTED (v1) 2026-07-20 as `converter trace --binding <bindings.json> --project
  <ir-dir>` (`src/converter/Converter/Trace/`, `src/converter/README.md`), wired into `review-functional`
  Pass 1. Layer A (AI reviewer) emits a per-REQ binding (out_tag / iface_member / number-constraint —
  its own evidence-carrying anchors, NOT architecture.md's table); Layer B walks it over FI-22's
  `ProjectUsageGraph` (read-only) + DB start values and returns per-hop **candidate** verdicts (facts,
  never an adjudicated pass). **v1 hops:** output-path (out_tag written? → unimplemented), interface-chain
  (iface_member written? → broken-chain / in-cycle-lamp class), number-constraint (DB start value vs spec
  → ok/contradicted/partial). **Deferred v2:** the **disarmed** hop needs the graph to carry each write's
  condition Expr (a `UsageSite` extension; the AlwaysTrue test is the empty-`And` "TRUE" sentinel); the
  **timing** ×1000 s→ms MUL/CONVERT-chain hop is net-new dataflow analysis. Binding is a snake_case JSON
  DTO. Real-corpus smoke passed (REQ-004 discharge conveyor). 4 tests.
- **Raised:** 2026-07-18 · **Source:** conversation follow-up — "is there a way to mechanize the functional review more, like a scripted decision tree?" The answer decomposes the review into two layers with a clean seam, and only one layer is a decision tree.
**Merits:** The per-REQ verdict genuinely *is* the same decision tree every run — but only *below* the one step that is irreducibly semantic. Split it:
- **Layer A (stays AI — the semantic anchor):** a per-REQ *trace binding* mapping the natural-language REQ to concrete IR anchors — expected output tag, expected interface source member, any number/polarity constraint. Evidence-carrying, and the reviewer owns it (it must **not** just trust `architecture.md` §7's REQ→block table, or the review stops being blind). No decision tree can produce this from register text — that is the whole reason an AI reviewer exists.
- **Layer B (a real scripted decision tree — a candidate `converter trace --binding <file> --project ir/<project>/`):** given a binding, walk the field-to-field chain over the parsed IR and emit verdict + quoted evidence per hop. Concretely: `out_tag` written by a map FC? no → **unimplemented (no output path)**. `iface_member` written anywhere? no → **unimplemented (broken chain — the in-cycle-lamp class)**. Any writer on the `member→out` path gated by `NOT AlwaysTrue` and not a map rail? yes → **disarmed**. Number constraint present and DB start value ≠ spec? → **contradicted**; no start value? → **partial** (cite `Q-nn`). Timing REQ and the ×1000 s→ms `MUL`/`CONVERT` chain reaches *this* timer's `PT`? no → **contradicted/partial** (the pair-crossing trap). Else → **implemented**, with the AI still confirming the member semantically matches the REQ.
  Steps above are pure structure on the reader/writer graph — the same `ProjectIndex` reader/writer index FI-22 builds, driven *forward from a binding* instead of only reverse. (Pass-2 dead-wiring — the reverse direction — is already FI-22 and needs no binding at all; build that first.)
**Costs / risks:** A wrong binding yields a false `implemented` — the tree removes *tracing* mistakes (a wiring hop a tired hand-grep skipped), **not** *binding* mistakes; the AI still owns the binding and the final semantic confirm. Some verdicts resist the tree: polarity "can't-work" contradictions (a stop button held permanently asserted) can be *flagged as candidates* but not ruled (NC-vs-NO intent is judgment); `not-statically-checkable` is judgment by definition. Emit facts + candidate classifications, never an adjudicated pass — same discipline as the mechanical `converter review` dump.
**Dependencies:** FI-22's `ProjectIndex` reader/writer graph (the shared substrate); a small structured binding format the reviewer emits. Complements FI-22 (reverse pass) as its forward-pass counterpart.
**Verdict / revisit trigger:** Open — the maximal *honest* mechanization of the functional review: AI binds once, script traces deterministically. Worth piloting once FI-22's reader/writer index exists, since Layer B reuses it.

### FI-26 — `ir` ↔ `simatic-ml` export-drift check
- **Status:** IMPLEMENTED 2026-07-20 as `converter drift-check --project <ir-dir> --exports
  <simatic-ml-dir>` (`src/converter/Converter/DriftCheck/`, `src/converter/README.md`) plus the
  `ExportDriftDetectorTests` golden guard (`tests/golden/GoldenHarness.Tests/`). Pairs each committed
  `.ir` with its `.xml` by basename, rebuilds the SimaticML in-memory via the shared
  `BuildXmlFromIrText` (the same path `to-xml` uses — factored out of `ConvertToXml` so there's one
  code path), and `Normalizer`-compares: MATCH / DRIFTED / SKIPPED (unpaired) / ERROR. Exit non-zero
  on any drift. The golden test asserts the drift set equals a documented KNOWN-drift baseline (the 6
  test-project001 blocks stale from the B-5/REQ-028 re-arming fix never being re-exported —
  `FB_ShredderSequencer`, `FB_PusherControl`, `DB_Settings`, and 3 iDBs; `reference` is clean) → green
  now, red the moment an in-sync block silently drifts. Clearing a baseline entry means re-exporting
  that block from TIA (a live-Portal step, the owner's call — the detector makes the drift visible). 3
  unit tests + 2 golden. **The 6 stale blocks are consciously deferred — `docs/notes/deferred-items.md`
  D-7 (owner, 2026-07-20).**
- **Raised:** 2026-07-20 · **Source:** the 2026-07-20 housekeeping scan. The committed `simatic-ml/<project>/*.xml` exports can silently drift from their `ir/<project>/*.ir` after a fix that is never re-exported — hit this session: `FB_ShredderSequencer.ir` carried the B-5/REQ-028 re-arming fix while its `.xml` still had the pre-fix logic, which poisoned the synthesis-parity audit (diffs that looked like real synthesis gaps were partly stale-XML noise, e.g. `PusherControl 543→198`). The team worked around it by switching the oracle from synth-vs-external-XML to synth-vs-own-sidecar (`CommittedBlocksRoundTripTests`/`FrozenAnswerKeyRoundTripTests`) — i.e. *tolerated* the drift rather than detecting it.
**Merits:** A `converter` check (or a golden test) that Normalizer-compares each committed `ir/<proj>/<block>.ir` against its paired `simatic-ml/<proj>/<block>.xml` and **fails on semantic divergence** turns silent drift into a red build instead of an invisible landmine that amplifies into every downstream audit. All machinery already exists — `Normalizer` (`Converter.SimaticMl`), `to-xml`, both parsers.
**Costs / risks:** The committed round-trip tests deliberately *tolerate* the drift (own-sidecar oracle) — this is the complementary *detector*, not a replacement. Must pair files correctly and skip unpaired ones (some `ir/` blocks have no `simatic-ml/` counterpart and vice-versa) so it never false-fails.
**Dependencies:** `Normalizer`, `to-xml` (both built).
**Verdict / revisit trigger:** Open — cheap and high-value; the clearest win from the scan.

### FI-27 — Static Part flow-order / import-validity check in `preflight`
- **Status:** IMPLEMENTED 2026-07-20 as the `flow-order` check in `converter preflight`
  (`Preflight/FlowOrderCheck.cs`, wired into `PreflightRunner`'s per-network synthesis loop). The
  DFS-from-rail rule was exposed as `FlgNetWriter.FlowOrderedPartUIds` (one source of truth; the
  writer keeps using it), and the check validates each synthesized network's serialized `<Part>`/
  `<Call>` order against it — catching offline (the Normalizer masks raw Part order from every
  equivalence oracle) a writer regression that would otherwise only surface as a live-import
  rejection. Pure/parameterized helper so it's testable both ways (the writer always applies the
  rule, so the positive case is reached via an injected order). Paired with a
  `compile-error-playbook.md` "must be sorted according to the current flow" entry under Import stage.
  4 unit tests. Scope held to the flow-order rule (not TIA's whole import validator), per this FI.
- **Raised:** 2026-07-20 · **Source:** the scan. Whether a synthesized block's `<Parts>` are in TIA's required wire-graph flow order was only discoverable by a *live* TIA import — the MotorVSDSystem purpose-change import was rejected ("the elements must be sorted according to the current flow… element with UId 56"), costing a compile-gate cycle. Root cause: the `Normalizer` sorts `<Parts>` before comparing, so the parity harness, the own-sidecar oracle, and `NoSidecarEquivalenceTests` all **structurally mask** this whole class; only a raw-order test catches it. The specific bug is fixed (`7694fdf`, guarded by `FlgNetWriterPartOrderTests`), but the *general* blind spot remains — any future flow-order regression is invisible offline.
**Merits:** A `preflight` check validating synthesized `<Parts>` raw order against the DFS-from-rail rule `7694fdf` now implements catches this class **before** a Portal round trip instead of on import rejection (which is atomic — the block never lands, so the compile gate can't even run). Pair it with a `compile-error-playbook.md` entry for the "must be sorted according to the current flow" message (there is none today).
**Costs / risks:** Only meaningful for synthesized / derive-always blocks; scope to the flow-order rule, don't reimplement TIA's whole import validator.
**Dependencies:** `FlgNetWriter`'s flow-order pass (built, `7694fdf`); `preflight` (FI-13, built).
**Verdict / revisit trigger:** Open — the Normalizer hides this from every equivalence oracle, so a dedicated offline guard is the only cheap way to catch regressions.

### FI-28 — `openness-cli portal-status`: read-only Portal-process diagnostic
- **Status:** IMPLEMENTED 2026-07-20 as `openness-cli portal-status` (read-only; classifies
  `TiaPortal.GetProcesses()` output vs `LaunchedInstanceRegistry` into in-use / self-launched-orphan /
  stray-empty, with a pileup-vs-first-connect-dialog note; reads `ProjectPath`/`Id` without `Attach()`;
  never attaches/launches/opens/kills; always exits 0). Split for testability — a Siemens-free
  `PortalProcessInfo` POCO + a pure `PortalStatusClassifier` (21 unit tests, no COM) behind a thin
  gateway enumerator. Killing stays FI-07 (Parked); this is the safe read-only half. Built in parallel
  with FI-26/27 (`src/openness-cli/`).
- **Raised:** 2026-07-20 · **Source:** the scan. Stale `Siemens.Automation.Portal.exe` pileup is diagnosed by hand via `tasklist` + human judgment on which to close. It blocked/delayed work in ≥3 runs this session (the hopper Stage-3 compile gate was blocked on a 3-min connect timeout with 2 stale processes; fix-wave-2 lost roundtrips). `sanity-check` checks *project* health; nothing reports Portal *process* health.
**Merits:** A read-only `openness-cli portal-status` enumerating Portal processes and cross-referencing the CLI's `LaunchedInstanceRegistry` to separate self-launched orphans (self-healing) from strays — reporting the likely first-connect-dialog vs pileup cause — gives the human a precise picture without the CLI killing anything. Read-only sidesteps the permission-classifier problem that denied the kill remedies this session (fix-wave-2 telemetry).
**Costs / risks:** Read-only only — *killing* stays FI-07 (Parked; needs the `--yes`/dry-run pattern and a safe "idle" definition so a mid-compile Portal is never touched). This is deliberately the safe subset of FI-07, buildable now without FI-07's irreversibility risk.
**Dependencies:** `LaunchedInstanceRegistry` (built).
**Verdict / revisit trigger:** Open — distinct from FI-07 (diagnosis, not action); the cheap, safe half, worth doing first.

### FI-29 — Reuse-first duplicate-logic finder (digest-backed corpus query)
- **Status:** IMPLEMENTED 2026-07-20 as `converter reuse-scan --project <ir-dir> [--tag <tag>…]
  [--kind <kind>…]` (`src/converter/Converter/ReuseScan/`, `src/converter/README.md`). A
  digest-backed query over the whole export: `--tag` matches a block's tag roots, `--kind` matches a
  network's statement kind, ANDed across groups (`--tag T --kind timer` = blocks referencing `T` with
  a timer network). Composition on `DigestBuilder` only; surfaces candidate blocks, never rules
  "duplicate" (digest's orientation-only policy). Exit non-zero if any candidate found (the
  reuse-first alarm). Smoke-tested against `ir/test-project001/`: `--kind timer` surfaces
  `FB_ShredderSequencer` network 8 "Step 20 (DischargeStart) – Timer, Timeout Fault" — exactly the
  discharge-timeout logic the `discharge-conv-monitor` request duplicated. 5 unit tests.
- **Raised:** 2026-07-20 · **Source:** the scan. The reuse-first carving pass (`gen-architecture` / `gen-spec-analysis`) is a manual grep over the corpus. An entire spec-analysis cycle was wasted this session: the `discharge-conv-monitor` request (#1/10, ~20m, 10 REQs, 6 RFIs) was analysed then **abandoned** because it duplicated the discharge-timeout fault already in `FB_ShredderSequencer` — caught by the owner, not tooling; the hopper pivot then needed the same manual "does this already exist?" grep.
**Merits:** A `digest`-backed query — "which blocks reference tag T / implement a timeout/fault on T" — over the whole `ir/<proj>/` corpus surfaces the candidate blocks a human/AI must check, deterministically. `digest` already extracts CALL sites, tag roots, and per-network statement kinds; this is an index + query on top.
**Costs / risks:** Won't fully judge *semantic* duplication (that stays human/AI) — it surfaces candidates, never rules "already exists." Keep it an orientation aid, consistent with digest's policy (never review input, `docs/15` isolation model).
**Dependencies:** `DigestBuilder` / `ProjectIndex` (built).
**Verdict / revisit trigger:** Open — one full analysis cycle abandoned this session for exactly this; the reuse grep recurs every request. Related to FI-30 (both corpus cross-checks).

### FI-30 — S6 new-block target gap-hunter (REQ × tag-status × as-built cross-join)
- **Status:** IMPLEMENTED 2026-07-20 as `converter target-scan --requirements <register.md>
  --project <ir-dir>` (`src/converter/Converter/TargetScan/`, `src/converter/README.md`).
  Cross-joins the register against a fresh `ProjectIndex` classification and the corpus digest,
  bucketing each REQ: **DISQUALIFIED** (mechanical, precise — `hmi-only`/`out-of-scope`/
  `proposed-tag-blocked`/`q-open`, each with the specific blocking names/questions), **LIKELY-
  IMPLEMENTED** (a deliberately-separate *heuristic* bucket — mechanically clean but the REQ's
  exists-tags already appear in an as-built block, shown with the overlapping roots so the human
  judges strength), **CANDIDATE** (mechanically clean + no inline hint), **WITHDRAWN**. The mechanical
  layer is precise; the "already implemented" signal stays soft/semantic and never becomes a
  mechanical verdict — the boundary the project's mechanization discipline requires. Exit non-zero if
  zero candidates (the fast "no clean target" signal). Register parsing is a focused reader of the
  register's own strict `## Format` contract, not general markdown. Smoke-tested on the real 69-REQ
  test-project001 register: 15 mechanically disqualified + 35 likely-implemented pre-cleared (with
  evidence), 19 to confirm — matching the FI narrative (proposed-blocked / Q-disarmed / HMI-only /
  already-built). 5 unit tests.
- **Raised:** 2026-07-20 · **Source:** the scan. Finding a clean new-block-only landing spot is manual cross-referencing of unimplemented REQs × tag status × as-built inline coverage. Two consecutive `gen-architecture` runs (~55m combined this session) were spent almost entirely on this ("honest survey found NO clean REQ-grounded new-block-only target — all proposed-tag-blocked or Q-11-disarmed or HMI-only"). With the reuse-first bar rejecting obvious asks, targets are scarce (test-project001 is fully implemented).
**Merits:** A script/subcommand cross-joining `requirements.md` REQs against `tagstatus` (proposed-blocked?) and `digest` (already implemented inline? HMI-only?) emits a candidate-target table with each disqualification reason pre-computed — turning a manual survey into a filtered list a human confirms.
**Costs / risks:** Needs `requirements.md` in a parseable-enough shape (REQ IDs are already structured); reuse the `tagstatus` and `digest` primitives, don't rebuild them.
**Dependencies:** `tagstatus` (built, FI-24), `digest` / `ProjectIndex` (built).
**Verdict / revisit trigger:** Open — high per-occurrence cost (~55m/build) and it recurs at the top of every S6 request; timely given the S6-exit tally still needs 9 more requests.

### FI-31 — Telemetry line-format validator (shape only, not authoring)
- **Status:** Parked (2026-07-20) — against `gen-telemetry.md`'s own stated discipline; captured for completeness, not recommended now. **Revisit trigger:** malformed telemetry rows actually start appearing in practice.
- **Raised:** 2026-07-20 · **Source:** the scan. Every stage hand-writes a 7-field pipe-separated telemetry line. `gen-telemetry.md` explicitly defers tooling ("A log, not a dashboard… No tooling until the log itself proves too slow to read") — a deliberate anti-goal, so any authoring/dashboard tool would violate stated discipline.
**Merits:** If anything, a cheap non-intrusive *validator* only — check the 7-field shape, ISO date, and the `outcome` enum (`clean|findings|blocked|abandoned`) on an appended row. Not an author, not a dashboard.
**Costs / risks:** Even a validator edges against the "no tooling until the log proves too slow/error-prone" rule; premature at a two-project sample size.
**Dependencies:** None.
**Verdict / revisit trigger:** Parked — noted so it isn't re-proposed cold; revisit only if malformed rows actually appear.
