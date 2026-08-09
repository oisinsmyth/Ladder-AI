# 16 — Future Ideas

Candidate ideas under debate: analysed here for merits and costs until they earn a verdict. This doc fills the gap between the suite's other homes for scope — it holds what is *not yet decided*. Committed work lives in `02-roadmap.md`; excluded work lives in `10-non-goals.md`; in-flight work lives in `AITODO.md` (ephemeral by design). An idea is never silently deleted from here: every entry ends with a recorded verdict or an explicit revisit trigger.

## Lifecycle

Statuses: **Raised → Under debate → Accepted | Rejected | Parked | Deferred | Implemented | Implemented (partial)**.
The last three are terminal states this doc has been using in practice; they are declared here (2026-08-05)
so the set matches the entries.

- **Accepted** — promotion into the plan requires an ADR (`docs/adr/`), consistent with `10-non-goals.md`'s "revisit only via ADR" rule, followed by the roadmap/scope edit. The entry stays here with a pointer to the ADR; the ADR is the decision record, this entry is the analysis that led to it.
- **Rejected** — verdict and reason recorded in the entry. If the rejection is a "never", the item also moves to `10-non-goals.md` (with the ADR, per that doc's own rule); a plain "not needed" stays here.
- **Parked** — neither pursued nor dead. Must carry an explicit **revisit trigger** (an event, not a date): what would have to become true for the debate to reopen.
- **Deferred** — decided in principle, only the *timing* is the owner's open call; the item is tracked in `docs/notes/deferred-items.md` with a `D-n` id. Distinct from Parked, where nothing is decided.
- **Implemented** — built and in use. The entry keeps its analysis and records what shipped (tool/skill + commit + date); many tooling-, docs- and convention-only builds land here directly from Raised without passing through Accepted. *(That is observed practice, not a declared carve-out to the Accepted bullet's ADR rule — whether it should become one is open, `docs/audit/2026-08-05-full-project-audit-fixlist.md` F-35.)* Entries render it inline as `IMPLEMENTED <date>` — that is the same status.
- **Implemented (partial)** — the useful half shipped, a **named** half is still open; the entry must say which. This subsumes the ad-hoc `PILOT LANDED` and `Partially done` labels earlier entries used.

IDs are `FI-xx`, citable the same way as `R-xx` (risks), `C-xxx` (conventions), and `E-xx` (explanation checklist). Numbers are never reused.

## Entry template

```
### FI-xx — Title
- **Status:** Raised | Under debate | Accepted (ADR-NNNN) | Rejected | Parked | Deferred (D-n) | Implemented <date> | Implemented (partial) — <which half is open>
- **Raised:** YYYY-MM-DD · **Source:** where it came from
**Merits:** what it buys.
**Costs / risks:** what it costs, what could go wrong.
**Dependencies:** stages, tooling, or decisions it waits on.
**Verdict / revisit trigger:** the outcome, or what reopens the debate.
```

## Implementation status — 2026-08-05

**Supersedes the two 2026-07-20 sections below** ("Implementation status — 2026-07-20" and
"Prioritization — remaining work (2026-07-20)"), which are kept as the dated record of where the backlog
stood then. Each entry still carries its own authoritative status; this is the index.

**Milestone: a second build wave shipped a mechanical floor under the spec pipeline (2026-08-05).** It came
out of the S6-Killer-Plan autopsy (`docs/evidence/PlantAutoControl-bench-autopsy.md`) rather than a backlog
survey — the finding that an AI reviewer reading the same register as the AI coder is a **correlated**
check, so prose discipline alone leaves holes an agent walks through by choosing not to look.

- **Implemented this wave:** **FI-39** — all five checks (`candidate-scan`, `undriven-scan`,
  `relation-reconcile`, `signal-sweep`, plus the probative-citation check folded into `relation-reconcile`)
  and **FI-36-min** (`trace`'s guard-containment hop), 707 tests, wired into the rung skills with their
  artifact formats made converter-parsed contracts. **FI-37** and **FI-38** (coder/spec-side halves) —
  implemented as **skill rules, not tooling**, in the four-rung pipeline (`448251c`); see their entries for
  exactly which clauses landed where.
- **Buildable now, no external gate (the current ranked list — short by design):** FI-39's two named
  refinements, both recorded in its entry and deliberately not rushed. (1) `relation-reconcile`'s citation
  check **rewards a vaguer citation** than a precise one — the fix needs instance↔FB path resolution, not a
  patch. (2) `signal-sweep`'s token regex excludes a real member name containing `/`, so its residue can
  never reach zero on this project. Also still buildable: FI-25's low-value sidecar-exact MUL↔CONVERT timing
  refinement (unchanged from 2026-07-20), and FI-35's `alarm-scan` **extraction** half.
- **Open halves of shipped items:** **FI-36-full** (per-instance, driven off the D3 render) — real blocker
  is **R12**, the render reaching the coder, not FI-37. **FI-38's gate-rule half** — "a functional *partial*
  that drops a stated interlock is a **blocking** fail" — is **not enforced anywhere yet** (verified
  2026-08-05: no such gate in the reviewer skills or `docs/15`).
- **Live debates (owner-raised, undecided):** **FI-32** (replace IR with a restricted real language),
  **FI-33** (authored per-block interface/object model), **FI-34** (programmatic pattern library — the most
  alive of the three, with the safe/risky split recorded), **FI-35** (`alarm-scan` + HMI alarm-list
  generation — the HMI half sits behind FI-18 and an unanswered Openness question).
- **Unchanged from 2026-07-20:** the gated set (FI-08, FI-11, FI-12, FI-18, FI-06, FI-10, FI-02, FI-01,
  FI-03), the parked set (FI-05, FI-07, FI-31), FI-21 (harvest-assist skill — owner-scoped, "tomorrow's
  work"), FI-24's HELD provenance-wrapper half, and FI-09's remainder
  — which is AI-by-design, not tooling work (see its entry; `converter review` now dispatches **18** C-IDs).

## Implementation status — 2026-07-20

**Superseded by "Implementation status — 2026-08-05" above and by each entry's own status — kept as the
dated record of the 2026-07-20 build wave. Its "essentially cleared / nothing buildable without an external
trigger" reading no longer holds: FI-36-min and FI-39 shipped on 2026-08-05 and FI-39 records two open
refinements buildable now.**

**Milestone: the mechanical-tooling backlog from this doc is essentially CLEARED (2026-07-20).** A large
build wave (a value/leverage survey → tiers, built in file-disjoint parallel subagent tracks) shipped every
ranked buildable-now item; what's left is AI-by-design, externally gated, or owner-held (see the two
sections below). Current state (each entry carries its own authoritative status; this is the index):

- **Implemented (tooling shipped):** FI-13 `preflight`, FI-14 compile-error playbook, FI-15 `digest`,
  FI-16 telemetry, FI-19/FI-20 doc splits (all earlier) — plus the **2026-07-20 wave**: FI-24 `tagstatus`
  (tag-status half; gen-architecture adopts it), FI-29 `reuse-scan`, FI-30 `target-scan`, FI-26
  `drift-check`, FI-27 `preflight` flow-order check, FI-28 `openness-cli portal-status`, FI-22
  `cross-check`, FI-23 `digest --fingerprint`, **FI-25 `trace`** (forward-pass tracer — **all 5 hops:
  output-path / interface-chain / disarmed / number-constraint / timing**), **FI-22 aliasing follow-up**
  (dead-wiring now covers interface-UDT members), **FI-17** `ir-hash` + explanation-sidecar convention
  (pilot). Skills wired to the new tools (explain-plc-block, review-conventions, review-functional [incl.
  `trace`], gen-architecture).
- **Partial:** FI-09 — C-408/C-001, **C-103/C-121 inline**, and the **C-118–125 sequencer family** (C-118,
  C-119, C-120, C-122, C-125, 2026-07-20) are mechanical `converter review` checks; the remainder is
  **by-design AI** and needs no tooling (C-107/C-402 edge-memory judgment — mechanical half is FI-22's
  writer table; C-119's returned-to; C-122/C-123 Q-drives-a-fault; C-113 paradigm). FI-24 —
  provenance-header wrapper HELD (owner: keep the converter pure — no external-process shell-out).
- **Open, actionable next (no external gate):** **essentially cleared** — the mechanical-tooling backlog is
  done. Only the FI-25 timing hop's *sidecar-exact MUL↔CONVERT wire* refinement remains as a low-value
  deepening (the current hop verifies name/operand correspondence). Everything else is AI-by-design, gated,
  or held.
- **Gated (waiting on a stage/event/measurement):** FI-08 (first real S6 tag-proposal loop), FI-11
  (`generate` orchestrator), FI-12 (FI-16 measurement), FI-18 (owner scoping), FI-06 (S8 grounded example),
  FI-10 (S5), FI-02 (real OB content), FI-01 (S9), FI-03 (grounded example).
- **Parked / deferred:** FI-05 (blind target — `deferred-items.md` D-5), FI-07 (owner-parked; FI-28 is its
  read-only half), FI-31 (against telemetry discipline). Related: `deferred-items.md` **D-7** — the 6 stale
  `simatic-ml/test-project001` exports FI-26 surfaced (needs a live-Portal re-export).

## Prioritization — remaining work (2026-07-20)

**Superseded by "Implementation status — 2026-08-05" above — kept as the dated ranking record. Its
"nothing is buildable without an external trigger" line was true on 2026-07-20 and is not now: the
2026-08-05 wave shipped FI-36-min + FI-39 and left two named FI-39 refinements buildable with no
external gate.**

The living order over what's *left* after the 2026-07-20 build wave (superseded the 2026-07-16 snapshot
below). Ranked by value × leverage ÷ effort; items hard-gated by an external event are listed separately,
unranked, because timing isn't ours to choose.

**Buildable now (ranked):** the mechanical-tooling backlog is **essentially cleared** — FI-09 C-125,
FI-17, and the FI-25 timing hop (this batch) were the last three ranked items, all shipped. What remains:

1. **FI-25 timing hop — sidecar-exact MUL↔CONVERT refinement** (low value). The shipped hop verifies the
   timer's PT is the ×1000 ms form of the bound seconds member by name/operand correspondence; a deepening
   would resolve the exact `EN:=ENO` wire (sidecar `PrecedingEnoSidecar.PrecedingPartUId`) to also catch a
   subtle shared-scratch wire-crossing. Only worth doing if that trap ever actually bites.

Otherwise nothing is buildable without an external trigger. **FI-24** provenance wrapper stays HELD (owner:
keep the converter pure). The FI-09 remainder (C-107/C-402, C-119-returned-to, C-122/C-123 Q-drives-a-fault,
C-113) is **AI-by-design — not tooling work**.

*(Shipped across this session's batches: FI-25 v1 + v2 disarmed + v2 timing, FI-09 C-103/C-118/C-119/C-120/
C-122/C-125, FI-17, FI-22 aliasing — see Implemented above.)*

**Gated — build when the trigger fires (unranked; blocked on an external event):**

- **FI-08** proposed-tags approval path — first real S6 tag-proposal loop.
- **FI-11** presentation bundler — the `generate` orchestrator's design (may fold into it, not be separate).
- **FI-12** persistent Portal session — FI-16 telemetry measuring that project-open still dominates.
- **FI-18** HMI-interface skill — owner scoping room (test-project001 has the open HMI-boundary questions).
- **FI-06** edge-detection pattern kind — a grounded example (S8 harvest).
- **FI-10** HMI alarm exports — S5 extractors proven.
- **FI-02** OB1 round-trip — real production OB content. **FI-01** pattern testing hook — S9 opens (R-07).
  **FI-03** Modbus multi-instance — a grounded example.

**Parked / deferred:** FI-05 (blind target, `deferred-items.md` D-5), FI-07 (owner-parked; FI-28 is its
read-only half), FI-31 (against telemetry discipline). **D-7** (`deferred-items.md`) — the 6 stale
`simatic-ml/test-project001` exports; needs a live-Portal re-export (owner).

## Prioritization snapshot — 2026-07-16

**Superseded by "Prioritization — remaining work (2026-07-20)" and "Implementation status — 2026-07-20"
above, and by each entry's own status — kept as the original ranking record.** A point-in-time ranking, not a living order: it reflects the entries and project state as of this date and is not maintained as verdicts land. Method: each idea was ranked twice (ease of implementation; logical implementation order given dependencies and leverage), score = sum of the two positions, lowest first, ties broken by logic position. An idea hard-gated by another FI is nested under its gate instead of holding its own slot; external gates (stages/events) are noted inline. FI-04 excluded (Rejected).

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
- **Status:** Implemented (partial) — ongoing, no longer Parked (corrected 2026-08-05). `converter review`
  now dispatches **18** C-IDs (`ReviewRunner.AllRuleIds`: C-001/003/005/103/118/119/120/121/122/125/201/301/
  501/406/408, plus C-102/401/404 recorded vacuous) against the 8 at S4 sign-off. The open half is
  **AI-by-design, not tooling work** — C-113 (paradigm choice), C-124 (C-119's returned-to), C-123 (C-122's
  Q-drives-a-fault), and the judgment clauses of C-103 and C-121; C-107/C-402 are covered mechanically by
  FI-22's C-308 writer table and are deliberately not duplicated in `Rules.cs`. Rule-by-rule history below.
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
**C-119/C-120/C-122 landed 2026-07-20** — sequencer structural rules on the same seam: C-119 (idle = step 0
present, Error), C-120 (steps ascend ×10, Warn), C-122 (dwell-timer shape: IN gated `Step=<n>`, Static
instance not `DB_Timers`, PT a UDT settings member — the PT-home part `NotApplicable` without `--project`).
Clean on the real steppers (`FB_ShredderSequencer`/`FB_PusherControl`), no false positives. The AI-deferred
halves stay AI: C-119's "returned-to on stop/fault/restart" (C-124), C-122's "Q drives a fault" (C-123),
and **C-125 landed 2026-07-20** — `Rules.CheckC125TimeoutFaultInInterfaceUdt`: a C-122 dwell-timeout timer's
own fault bit (identified via the safe contrapositive: reads a C-122-subject timer's `.Q`, `CoilTag` leaf
ends `Fault`, self-latch cleared by `NOT …FaultReset`) must live in the interface UDT, not a bare private
Static/DB (warn, candidate; NotApplicable without `--project`). Clean on both real steppers, no `PressureHold`
false positive. **The C-118–125 sequencer rule family is now complete** except the by-design-AI clauses
(C-119 returned-to, C-122/C-123 Q-drives-a-fault semantics, C-113 paradigm).

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
- **Status:** Implemented (partial) — the helper + convention landed 2026-07-20 as a **pilot**; what stays open is the pilot itself (hand-caching during normal work) and any tooling beyond it. The key/invalidate helper `converter ir-hash <file>`
  (`Converter/IrHash/`, `src/converter/README.md`): `SHA-256(SerializeBlockReadable(block))`, a stable
  content hash over the *readable* logic only, immune to SIDECAR/UId churn (deliberately not
  `digest --fingerprint`, which abstracts tags → wouldn't invalidate on a rename). Plus the convention doc
  `docs/notes/explanation-sidecars.md` (storage: `<Block>.explain.md` stamped `derived-from: <ir-hash>`;
  use-boundary: **orientation only, never review input, hash-on-read invalidation** — the ADR-0005 discipline
  applied to an AI artifact, `TimerSample` stale-sidecar the precedent) + an optional `explain-plc-block`
  caching note. Pilot stance per this entry: hand-cache during normal work; the helper keys/validates it.
  5 tests.
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
- **Status:** Implemented (partial) — 2026-07-18; the provenance-header wrapper half is HELD (owner). The **tag-status half is built** — `converter tagstatus <name…> --project <ir-dir> [--json]` (`src/converter/Converter/TagStatus/`, `src/converter/README.md`): classifies each name `EXISTS`/`PROPOSED` against the export via the same `ProjectIndex` + `AccessNode.FromDottedPath` primitive `preflight` uses, exit 1 if any proposed (a usable "all tags exist" gate). **Corrected 2026-08-05 (`f50753a`):** it now resolves to **member** level — it previously stopped at the DB root, so an invented member of a real DB passed this hard-rule-3 gate; `--roots-only` restores the old root-level mode. The **provenance-header wrapper is still open** (below). Originally raised 2026-07-18 from the skill/tooling audit — net-new, small tooling.
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
  → ok/contradicted/partial). **v2 `disarmed` hop DONE 2026-07-20** — the graph now carries each write's
  guard Expr (additive `Guard` on `DirectedTagUsage`/`UsageSite`), and a pure three-valued constant-fold
  (`Trace/DisarmAnalysis.cs`, `AlwaysTrue⇒true`) returns `disarmed` when every writer of a path is gated
  `NOT AlwaysTrue` (built but switched off — the correction: the idiom is `NOT AlwaysTrue`, a negated real
  tag, not `NOT TRUE`). Real corpus: `IO.HandReverse` (`:= NOT AlwaysTrue`) → disarmed. **v2 `timing` hop
  DONE 2026-07-20 — the tracer is now complete (all 5 hops).** `timing: { timer, seconds_member }` verifies
  the timer's PT is the ×1000 ms form of the bound seconds member, keyed on the timer's *unique* PT
  ms-member name (`OvercurrentMediumDelayMS` ↔ `OvercurrentMediumDelay`) — the shared `Time` scratch can't
  distinguish members, so a wrong-member binding is correctly `contradicted`. **Documented limitation:** the
  specific MUL↔CONVERT `EN:=ENO` wire (sidecar-only) isn't verified — a sidecar-level refinement, the one
  remaining timing-hop deepening. Binding is a snake_case JSON DTO. 4 (v1) + 8 (v2 disarmed) + 1 (timing)
  tests.
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

### FI-32 — Replace IR with a restricted real programming language (Python/C# subset)
- **Status:** Under debate
- **Raised:** 2026-07-27 · **Source:** conversation (owner). Motivation: reuse premade language tooling; agents are more fluent in Python/C# (large training-set presence) than in a bespoke notation. Proposed interim test: a Python→IR converter before a full Python/C#→LAD converter.
**Merits:** A mainstream-language authoring surface could, in principle, let the model lean on its language priors and let the project reuse existing parsers/formatters/type-checkers instead of maintaining a bespoke grammar. The underlying real insight — the model reasons better on a familiar, structured, checkable surface — is legitimate and worth keeping visible.
**Costs / risks (the core of the debate):** The cost of IR was never the notation — it is the **semantic mapping to LAD**, which S1 already paid in full (every instruction grounded against real exports and live-verified through the TIA cycle). IR *is* already the "limited language" — tag refs + infix boolean operators + call syntax, deliberately 1:1 and lossless onto LAD's wire-graph semantics. Swapping the syntax does not remove the converter; it enlarges it and breaks properties the project depends on:
1. **Impedance mismatch.** LAD is cyclic dataflow; Python/C# are imperative control-flow (loops, recursion, exceptions, heap, dynamic dispatch). Restricting them hard enough to map onto LAD makes them no longer the real language — so the two claimed benefits (premade tools, training-set fluency) both assume full-language semantics and evaporate. Net new failure class: "valid Python that isn't valid LAD-subset" — the opposite of design-philosophy #10 ("unknown source elements are hard errors, never best-effort").
2. **Loses the read direction.** The pipeline is bidirectional (`TIA → IR` explain/review real site code; `IR → TIA` generate) and IR round-trips **byte-identically and losslessly** (golden-tested). A general language does not round-trip *from* LAD — fan-out drawing choices (`{split}`/`{recv}`), network titles, scan-significant kind-ordering, and sidecar geometry/UIds have no natural home in it; many LAD shapes can't be expressed cleanly and many programs collapse to the same LAD. `explain-plc-block`, the three reviewers, `drift-check`, and `cross-check` all rest on that lossless round-trip.
3. **Human audience reads LAD, not Python.** The review audience is a controls engineer; the whole C-xxx convention set is LAD-shaped and doesn't translate.
4. **SCL already exists.** Siemens ships a native text language (SCL) that compiles to this PLC; the project ruled it out (hard rule 1) for domain reasons (site wants LAD, reviewers read LAD). Generating a real language that compiles *down to* LAD is strictly harder than just using the text language already declined.
5. **The Python→IR interim test doesn't test the risk.** It's easy precisely because IR already sits at the semantics; it says nothing about faithfully, losslessly representing arbitrary real LAD, especially the round-trip-from-TIA direction where it breaks.
Also: the bottleneck has never been notation fluency — it is grounding (real tags, real idioms, site conventions, requirement→rung mapping), which a friendlier syntax does not help and may hurt by inviting imperative constructs with no LAD equivalent.
**Dependencies:** Would invalidate/re-do a large amount of proven S1 converter work; coupled with FI-33 as originally raised. Collides in timing with active S6 (generation-request tally) and library-filling (FI-21).
**Verdict / revisit trigger:** Undecided — kept as an active debate. Analysis above stands as the case against a *replacement* framing; if a real authoring-ergonomics pain is identified, the lighter alternative is targeted IR sugar (or better tooling around IR), not a language swap. Revisit if a concrete ergonomics failure is observed that IR-side tooling can't address.

### FI-33 — Authored per-block interface/object model with typed cross-references
- **Status:** Under debate
- **Raised:** 2026-07-27 · **Source:** conversation (owner), coupled to FI-32. Idea: model each block type as an object with a full network of interfaces the AI authors against, so cross-references between blocks are first-class and cross-checkable.
**Merits:** First-class, typed cross-block interface references would make wiring mistakes (broken chains, interface drift, missing writers) catchable at author time rather than by hand or at compile.
**Costs / risks:** The useful half **already exists as a derived graph**, not an authored model: `ProjectUsageGraph`/`ProjectIndex` (FI-22 `cross-check`, FI-25 `trace`) is a whole-project reader/writer reference graph; the callee `.ir` is already the source of truth for its interface and CALL sites are checked against it; cross-file interface-UDT resolution (C-118–125) already reasons across blocks; the converter already models FB/FC/OB/DB/UDT distinctly. The genuinely new part — a *stored, authored* object/interface model beside the IR — re-introduces exactly the second-source-of-truth this project is most allergic to (the `TimerSample` stale-sidecar bug, `drift-check` FI-26, hash-invalidated digests, and the ADR-0005 *derive-always* discipline all exist to kill stored derived state). An authored interface graph will rot the same way.
**Dependencies:** `ProjectIndex`/`ProjectUsageGraph`, `cross-check` (FI-22), `trace` (FI-25), `preflight` (FI-13) — all built. Coupled to FI-32 as raised.
**Verdict / revisit trigger:** Undecided. The kernel (stronger typed cross-block interface checking) is real but belongs in the *derived* analysis path — extend `cross-check`/`preflight`, don't stand up authored state. Revisit if a specific interface-checking need is identified that the derived graph can't express.

### FI-34 — Programmatic (parameterized) pattern library
- **Status:** Under debate
- **Raised:** 2026-07-27 · **Source:** conversation (owner). Idea: make the library programmatic — encode variations as parameters the generator instantiates, instead of presenting the AI a "copy this template" example.
**Merits:** The strongest of the three raised together. "Copy this template" is a genuinely weak affordance, and library-filling is active work (FI-21). Making pattern instantiation executable and machine-checked is a real improvement to the trust anchor of generation (`04-design-philosophy.md` §6).
**Costs / risks — a sharp line to keep:**
1. **Safe version:** parameters = the *tag-mapping slots* the pattern already has (the workflow's "map real tags to slots; type-check"). Making that binding executable and type-checked is a clean incremental win with no epistemic cost.
2. **Risky version:** parameters = *structural variation* (optional rungs, step counts, with/without edge-detect). Every unseen combination is a new **untested** shape. `docs/07` admission rule #1 is "instantiated in reviewed, working logic — proven, not speculative," and FI-05's blind-draft criterion exists for exactly this. A generator emitting combinations no real instance covers manufactures "reviewer-pleasing synthetics" — the precise failure FI-21's design constraints already warn about. To parameterize *structure* safely you need per-parameterization behavioural evidence, i.e. the pattern testing hook (FI-01), which is Parked behind the S9 simulator (R-07 unresolved).
**Dependencies:** `docs/07-pattern-library-spec.md` admission criteria (the gate it feeds, never replaces); related to FI-01 (testing hook — gates structural parameterization), FI-05 (blind-draft proof), FI-06 (pattern taxonomy), FI-21 (harvest-assist).
**Verdict / revisit trigger:** Undecided — the most alive of the three. Reframe around the safe/risky split: pursue executable, type-checked tag-slot binding now (tooling, no roadmap change); gate structural parameterization on FI-01/S9 so it can't cross the proven-not-speculative line. Revisit as library-filling (FI-21) matures.

### FI-35 — `converter alarm-scan` + HMI alarm-list generation
- **Status:** Raised
- **Raised:** 2026-07-29 · **Source:** conversation — the first use of this knowledge base for *delivery* rather than development: extracting a live project's alarms into an HMI discrete-alarm import spreadsheet, done manually end-to-end. Full grounded write-up: `docs/notes/hmi-alarm-generation.md`.
**Merits:** The extraction half is unusually cheap because C-501's two conditions are load-bearing exactly as intended — one alarm bit per network, and the network title *is* the alarm text. In the live run `FC_AlarmsMain` was N networks of one contact driving one slice-access coil, so trigger tag, bit index and alarm text were all directly readable from IR with no rung analysis. A `converter alarm-scan --project <ir-dir>` emitting the alarm inventory as facts (category words per C-501 **and** per-instance UDT alarm words per C-503) is a small, well-bounded tool of the same shape as `cross-check`/`trace`: derived fresh, facts not verdicts. It also has a natural conformance edge — a network writing two alarm bits, or an alarm bit whose network has no title, is a C-501 violation the tool should hard-error on rather than best-effort (design philosophy #10), which makes it a `review`-adjacent check for free.
**Costs / risks:**
1. **Two surfaces, and the obvious heuristic is wrong.** C-501 category words and C-503 per-instance UDT alarm words are both correct; C-503 says explicitly that category words are *not* duplicated per instance. A tool that flags "instance alarm bits not wired into `DB_Alarms`" as a defect false-positives on every conformant project — this misreading happened live and was caught only by reading C-503. The genuine defect is the inverse (a category bit duplicating what the UDT already carries).
2. **No compile gate exists on the HMI side.** Hard rule 4's "nothing is presented until proven valid" has no analogue: a generated alarm list is unverified against the HMI tag list. Any capability here must state that its output is unverified, or acquire a check — see 4.
3. **The generation half is authoring, not extraction.** C-503 instance alarms have named bits but no authored text, so their alarm text must be *composed* to C-505's format. Those rows are proposals needing review and must not be presented at the same confidence as C-501-extracted rows.
4. **Two boundary questions belong to FI-18, not here:** HMI-side bit numbering within a Word trigger tag (not present in any export — an HMI convention), and how C-506's severity taxonomy and C-507's acknowledgement behaviour map onto the import format's *single* `Class` column. The latter bit live: a blanket non-acknowledging fill matches C-507's default but contradicts C-507's own named E-Stop exception. Per-alarm class needs an owner ruling, not a default.
5. **Unknown, and it changes the deliverable's shape:** whether Openness can drive the HMI alarm list directly instead of via the UI's spreadsheet export/import. Not investigated (the spreadsheet route worked; widening scope mid-job wasn't warranted). Should be answered before building.
**MEASURED 2026-08-09 (HMI capability probe P4, `docs/notes/hmi-capability-probe-plan.md`) — two of the costs above are now settled, one better and one much worse:**
- **Cost 2 is WRONG and can be struck.** There *is* an HMI-side gate: the **device compile**. Across the six probe phases it caught five independent routes to a dangling reference and named the missing field each time — including, for a bare alarm, `Trigger tag: No trigger tag is configured`. Hard rule 4 does have an analogue here after all, so generated alarms need not be presented unverified.
- **CORRECTION 2026-08-09 (P7, write-api §4m):** an earlier version of this block said `Flashing` dynamization refuses, compounding the alarm problem. **That was wrong** — the refusal was the probe binding every dynamization kind to a Boolean property. `Flashing` creates on any **colour** property and `ResourceList` on any **text** property, so **alarm-state DISPLAY is fully expressible** (and there is a second route via `TagDynamization.ValueConverter` → `MappingTable`, whose entries carry `Flashing`/`FlashingRate` per value range — untested but present). The alarm-TEXT block below is unaffected and remains the real constraint.
- **Cost 5 is answered, and the answer BLOCKS the generation half.** Openness *can* drive the Unified alarm list — class, discrete alarm and analog alarm all create, and `Priority`/`StateMachine`/`AlarmClass` all set. But **the alarm text cannot be written at all**: `MultilingualTextItem.set_Text` throws on a fresh alarm. Since the whole point of C-505-format alarm text is the text, **alarm-list generation through Openness is blocked on an unexplained refusal**, not merely awkward. `RaisedStateTagBitNumber` also refuses on a fresh alarm — probably contextual writability (disabled until a trigger tag exists), which if confirmed means the generator has an ordering requirement the schema does not express. Both want one targeted probe before this FI is planned; until then the spreadsheet import/export route remains the only demonstrated path for the text.

**Dependencies:** Extraction half: none technical — `ProjectIndex`/`ProjectUsageGraph` and the IR already carry everything needed. Generation/HMI half: **FI-18** (PLC/HMI boundary) is a genuine prerequisite for the class-mapping and tag-naming contract — and alarm generation is arguably the best concrete case to scope FI-18 against, being small, bounded, and contract-shaped rather than screen-shaped.
**Verdict / revisit trigger:** Open. Natural split: build `alarm-scan` (facts, C-501/C-503 aware, conformance hard errors) independently and now-ish — it stands alone as an explanation/review aid regardless of what happens HMI-side; hold the HMI-list *generation* half behind FI-18 and the Openness-HMI question in 5. Revisit when a second alarm-extraction job comes up, or when FI-18 is scoped.
### FI-36 — Per-instance interlock-completeness trace (review-functional's independent check)
- **Status:** Implemented (partial) 2026-08-05 — **FI-36-min shipped**, the full form is open. FI-36-min is `trace`'s `guard-containment` hop (`708ae21`), made **mandatory** in `review-functional` for every REQ stating a condition (`decd2d5`) — placed there deliberately, since that pass is the one that graded the REGRESSION as MATCH. ~110 LOC on `BindingFile` + `UsageSite.Guard`. The **full** form (per-instance, driven off the D3 render) remains open and its real blocker is R12 (the render reaching the coder), not FI-37.
- **Previous status:** Raised
- **Raised:** 2026-07-20 · **Source:** the S6-Killer-Plan autopsy (`docs/evidence/PlantAutoControl-bench-autopsy.md`). The blind-generated `PlantAutoControl` shipped a REGRESSION (REQ-003: the filter cascade-hold's neighbour-`ShutdownComplete` term dropped on 3 machines) that `review-functional` **explicitly graded MATCH** — it verified the block against the register's own ambiguous `/`-joined condition and read it the same way the coder did.
**Merits:** The functional reviewer, reading the same register as the coder, is a **correlated** check, not an independent one — it catches "unimplemented," never "implemented against a wrong shared reading." A mechanical **per-instance interlock-completeness trace** breaks the correlation: for each machine, take the register/spec's *explicit* condition list (the interlock table's starts-after / holds-until columns) and verify **every listed condition appears in the block's guard** — a set-difference, not a re-interpretation. Deterministic; would have caught REQ-003 regardless of how anyone "reads" the spec. Where a real job has no answer key, this trace is the answer key's stand-in. A companion **strongest-available-guard** check flags using a narrower interlock signal (raw `ComFlt`) when the wired FB exposes a broader one (`FaultActive`) — the REQ-017 miss.
**Costs / risks:** Only as good as the spec's explicitness (needs FI-37's condition list to trace against — the two are complements). Must emit facts (missing condition X on machine Y), not verdicts.
**Dependencies:** FI-22 `cross-check` (reader/writer graph) + FI-25 `trace` (forward REQ→IR binding) — the substrate exists; this drives them off the interlock table. FI-37 (the explicit condition list to trace against).
**CORRECTION (2026-08-05, design study `docs/notes/fi-39-candidate-scan-design.md`):** the FI-37 dependency above is **wrong for the minimal form**. A **FI-36-min** — a `must_contain` constraint on `Trace/BindingFile.cs`'s per-REQ anchors, set-differenced against the guard `Expr` already carried by `ProjectUsageGraph`'s `UsageSite.Guard` (FI-25 v2) — is **buildable today in ~80 LOC** and was verified against the real generated block: it reports `FansShutdownReady` present and `AirStarInst1.Outputs.ShutdownComplete` **MISSING**, i.e. it catches the REQ-003 REGRESSION deterministically. The *full* form's real blocker is **R12** (the D3 render reaching the coder), not FI-37 — the only artifact carrying per-instance conditions over named signals is that render, which is `STOPPED` in the fixture.
**Verdict / revisit trigger:** Open — the highest-leverage autopsy fix; the one thing that would have caught the REGRESSION deterministically. **Build FI-36-min first** — cheapest item in the study, strongest catch.

### FI-37 — Spec interlock explicitness: no ambiguous conjunctions, underspecified interlock = blocking Q
- **Status:** Implemented 2026-08-05 — **as skill rules, not tooling** (`448251c`). Both clauses landed in
  the four-rung pipeline: (1) *no ambiguous conjunctions* is `.claude/skills/gen-pid-analysis/SKILL.md:65`'s
  section, literally headed "Explicitness rules (non-negotiable — FI-37)" — one relation per line on the way
  in as well as out, an ambiguous separator in a SOURCE split into two retained relations or raised, with a
  carve-out forbidding split-and-retain where a side would assert an unestablished signal (hard rule 3);
  (2) the *underspecified detail = blocking `Q-nn`* mechanism is rung C's (`gen-equipment-spec/SKILL.md`
  §2/§3, the `CANDIDATES:` field plus a blocking `Q-nn` decided at D1). The register-format half is enforced
  mechanically only insofar as FI-39's checks parse those artifacts; the explicitness rule itself is prose
  discipline read by the executing agent.
- **Previous status:** Raised
- **Raised:** 2026-07-20 · **Source:** the autopsy. Every gap lived in the register's **ambiguity, silence, or looseness**: REQ-003's dual hold was written `"Fans-shutdown-ready / Air-separator VSD shut down"` (a `/` that both coder and reviewer collapsed to one term); REQ-017 said "not faulted" without pinning the signal; REQ-019's feedback pairing was left to inference.
**Merits:** Removes the ambiguity the coder/reviewer correlation feeds on, at the source. Two `gen-spec-analysis` / `requirements.md`-format rules: (1) **no ambiguous conjunctions** in the interlock table — one explicit condition per cell, or spelled-out AND/OR/NOT, never a bare `/`; (2) an **underspecified interlock detail** (which fault signal? which feedback pairing?) is a **blocking `Q-nn`**, handled like a `proposed` tag — the coder may not silently pick a reading. Directly enables FI-36 (gives it an unambiguous condition list to trace).
**Costs / risks:** More rigour up front in the Examiner/spec stage; the register grows more structured (a feature, not a cost). Risk of over-formalising prose requirements that are genuinely narrative — scope the rule to the *interlock/permissive* table, not all REQ text.
**Dependencies:** None technical; a format-contract + Examiner-discipline edit. Complements the architecture-restructure under separate development (a Block-Spec step whose output contract is boolean-explicit per-interface conditions is the fuller form of this).
**Verdict / revisit trigger:** Open — cheap, and it is where the REGRESSION was born.

### FI-38 — Interlock-safety disposition: surface ambiguity, don't silently under-constrain; dropped guard = blocking
- **Status:** Implemented (partial) 2026-08-05 — **as a skill rule, not tooling** (`448251c`). **Discipline (1)
  landed:** `.claude/skills/gen-code-structure/SKILL.md:213–217` ("Calibration") requires that ambiguity is
  never resolved silently and that a forced choice on **any protective term — interlock, permissive, inhibit
  or fault gate** takes the stronger/fail-safe guard and records it, citing FI-38 by name and carrying the
  same hard-rule-2 scope note this entry uses. **Discipline (2) — the gate rule — is NOT implemented:** a
  functional "partial" that drops a stated interlock is still an ordinary `partial` verdict
  (`review-functional/SKILL.md:117`), not a blocking fail; a repo-wide search on 2026-08-05 found no gate,
  in any skill or in `docs/15`, that blocks presentation on a dropped stated interlock or requires a recorded
  owner waiver first. That half is the open one — it is where the autopsy's shipped defects actually got
  through.
- **Previous status:** Raised
- **Raised:** 2026-07-20 · **Source:** the autopsy's coder-disposition finding. On the *explicit* gaps (REQ-012 bypass, REQ-016 latch) the coder flagged its deviation honestly — those shipped only because a flagged "partial" was non-blocking. On the *ambiguous/underspecified* gaps (REQ-003/011/017/019) the coder **silently resolved every one toward the fewest guards** — the permissive reading of a safety interlock, with no flag.
**Merits:** Two disciplines. (1) **`gen-block-new`:** when an interlock/permissive is ambiguous or underspecified, **stop-and-flag** (like a `proposed` tag) rather than silently pick the weaker reading; prefer the stronger/fail-safe guard when forced to choose on **any protective term — interlock, permissive, inhibit or fault gate** — and record the choice. *(Scope word matters: in this repo "safety" means F-content that must be refused outright per hard rule 2, so this rule is deliberately worded for ordinary **process** protection — which is where every documented miss actually happened.)* (2) **Gate rule (generated-code bar):** a functional **"partial" that drops a stated interlock is a blocking fail**, requiring implementation or an explicitly recorded owner waiver *before* presentation — not a footnote in the bundle. Together these stop the coder's characterised bias (it drops guards, never adds wrong ones) from reaching the engineer silently.
**Costs / risks:** More Build-stage stops (a feature — a stopped run with a named interlock ambiguity is more useful than a silent under-implementation). Needs FI-37's blocking-Q mechanism to route the flags.
**Dependencies:** FI-37 (blocking-Q surface). Pairs with FI-36 (the reviewer catches what the coder failed to flag).
**Verdict / revisit trigger:** Open — the coder-side counterpart to FI-36; the two together close the correlated-failure gap from both ends.

### FI-39 — `converter candidate-scan`: the mechanical floor under the spec-pipeline checks
- **Status:** Implemented 2026-08-05 — **all five checks + FI-36-min** (two named refinements open, below). `trace`'s guard-containment hop (`708ae21`), `candidate-scan` + the shared `SignalInventory` (`6e28f5c`), `undriven-scan` (`febc8bf`), `relation-reconcile` (checks 2+3 folded, `e71b7fa`), `signal-sweep` (`79e8046`); wired into the rung skills (`decd2d5`) and their artifact formats made contracts (`361c0e0`). 707 tests. Each verified against its graded failure on the `PlantAutoControl-bench` fixture, not only in unit tests: guard-containment reports the REQ-003 term MISSING on the generated block and OK on the sealed answer key; `candidate-scan` surfaces both REQ-017 candidates and the REQ-019 family; `undriven-scan` reports REQ-012's `IO.RotationSensor` dead on every instance.
  **Calibrations made against the real corpus, each because the design-as-written would have shipped noise:** `undriven-scan`'s `dead-interface` reports but never gates (gating produced 132 findings on one FB, since a reusable block legitimately exposes unused optional inputs) and its name-join hints are opt-in (they fired on every undriven member and buried the findings); `candidate-scan`'s exit keys on the **IO half** (a total-size trigger fired on every query, so the rule it drives degenerated to "always cite a basis"); `undriven-scan` excludes members the FB *writes* (FB outputs were being reported as undriven from the caller's side — ~2/3 noise, and it nearly caused a misreading).
  **Open refinements, deliberately not rushed:** (1) `relation-reconcile`'s citation check **rewards a vaguer citation** — bare `IO.UPSEnable` passes with "4 writers" while the more precise `FilterUnitInst2.IO.UPSEnable` is rejected as declaration-only, because the pooled FB-local path has writers and the instance-qualified one does not; the fix needs instance↔FB path resolution, not a patch. (2) `signal-sweep` can never reach zero on this project: `DiscreteInputs.CycloneDustAutoRunning/Stop` is a real member whose `/` its token regex excludes.
- **Previous status:** Raised
- **Raised:** 2026-07-20 · **Source:** two adversarial validation rounds against the four-rung spec pipeline (`docs/evidence/four-rung-design-validation.md`, `…-round2.md`). Round 2's verdict: the prose amendments closed both silent-survival paths, but **every remaining check is self-judged** — candidate-set size, "plausibly", "same-shaped", "more broadly", the precondition class are all decided *and recorded* by the same agent. Round 2's RW-3: *"no mechanical floor anywhere; `converter` gained nothing this round."*
**Merits:** Prose discipline has been pushed about as far as it goes — the residual holes are ones an agent walks through by **choosing not to look**. Only computed facts survive that. Five checks, all mechanizable on existing substrate, each tied to a documented real failure:
- **Candidate set (`candidate-scan`)** — given a requirement phrase's target and an instance, compute **every** in-scope signal that could satisfy it: IO-table members plus the chosen FB's exposed status members (`TagReferences` + the interface parse). *Size > 1 becomes a computed fact, not a judgment* — this is what makes rung C §2's blocking-Q trigger real. Catches the REQ-017 narrowed-fault (`FaultActive` vs raw `ComFlt`) and the REQ-019 swapped pairing (two requirements, two same-shaped signals) — the two round-1 SURVIVES.
- **Relation-id set-difference** — parse the `C-nn`/`P-nn` ids out of `equipment-specs/`, the D2 ledger rows, the D3 render terms, and the derived register; assert all four sets reconcile to empty differences. Mechanizes what R2/R11 currently ask an agent to assert about itself; kills the NW-2 lossy-register inversion (a lost relation making the reviewer recommend *deleting* a correct interlock).
- **Probative-citation check** — a `verified-cross-block` precondition must cite a tag that actually **has a writer** in `ir/<project>/`, not merely a declaration. *Demonstrated loophole (round 2, RW-1): `FansShutdownReady`'s only occurrence in the corpus is a bare declaration (`Control.ir:14`), which satisfies "cite the block, file and line" while proving nothing — and that is precisely the false argument that would have licensed the REQ-003 regression.* FI-22's `cross-check` reader/writer graph already computes writer-existence; this is a query on it.
- **Undriven-FB-input audit** — enumerate the chosen FB's interface members, mark driven/defaulted from the render. *A real VSD FB declares `IO.RotationSensor`; a generation left it undriven while its bypass tag sat unreferenced (REQ-012).*
- **Unclaimed-signal sweep, project-level** — every IO/global-DB signal is bound by some instance's spec or listed unclaimed with a reason. Fixes round 2's RW-2 (per-instance scoping missed plant-level `HMIControlSignals.Bypass*` tags) and removes the name-pattern dependence (`Bypass*`/`*Inhibit`) that a differently-named control signal defeats.
**Costs / risks:** Emit **facts, never verdicts** (the standing discipline for `review`/`cross-check`/`trace` output) — "3 candidates for this binding", not "wrong signal chosen". The candidate set needs a defensible scope rule (in-scope = this instance's IO rows + its FB's status members) or it degenerates to noise. Converter stays a pure in-process file transformer (no external-process shell-out — the owner-held invariant).
**Dependencies:** FI-22 `cross-check` (`ProjectUsageGraph`, writer facts), FI-25 `trace` (forward REQ→IR binding), `ProjectIndex`/`DigestBuilder`, and the A–D rung artifacts' formats (the id conventions to parse). Supersedes nothing; it is **the mechanical floor FI-36 needs** — FI-36 defines the completeness trace, this computes the inputs that make it unfakeable.
**Verdict / revisit trigger:** Open — the highest-value remaining build for the spec pipeline, and the only proposal that survives an agent deciding not to look. Sequence it with FI-36. **Design study done 2026-08-05: `docs/notes/fi-39-candidate-scan-design.md`** — all five checks buildable now, ~1800–2200 LOC + 30–40 tests total, recommended order **FI-36-min → `candidate-scan` (+ a new `SignalInventory` primitive, since `ProjectUsageGraph` discards member types) → `undriven-scan` → `relation-reconcile` (checks 2+3 folded) → `signal-sweep`**; items 1–3 read `.ir` only (no skill edit can rot them) and are ~45% of the effort for ~85% of the demonstrated value. **Correction to this entry:** "all five checks have a known-correct expected answer" on the fixture is **overstated** — checks 2 and 3 produce *empty* results there (rerun2 already reconciles 174/174/174; zero `verified-cross-block` rows exist) and check 5 has no oracle; the fixture is a strong regression test for **3 of 5**. Two design constraints the study surfaced: every absence claim must state its **denominator** (`ir/PlantAutoControl-bench/` is a *partial* 34-file export — "no writer" there is a scope fact, not a defect), and a markdown leg parsing **zero rows must hard-error**, never pass clean, or format drift turns these checks into silent all-greens.

### FI-40 — a mechanical status check: retire the hand-restated-status drift class
- **Status:** Raised (2026-08-05, out of the audit fix wave — the audit's own headline process recommendation)
- **Problem.** Three consecutive audits have found the same defect class, and it is now the project's
  measured dominant failure mode: a fact's *status* is re-stated by hand in several independent
  documents, one goes stale, and only an audit notices. The 2026-08-05 audit re-derived **14 of the
  prior round's 19 items as still valid**, and 11 of its own 53 findings trace to a single merge that
  updated `CLAUDE.md` and `docs/16`'s entry bodies but not `docs/15`, `stage-gates.md`,
  `docs/evidence/stage-S6.md`, `docs/16`'s status sections, `AITODO.md` or `CHANGELOG.md`.
- **Why the usual fix does not work.** "Reconcile the whole file" is already a standing Method rule and
  has now failed twice. Worse, the *correction* is not reliable either: the 2026-07-20 audit set out to
  fix a stale rule count and wrote **24**, because it counted `C-nnn` *mentions* in `Rules.cs` rather
  than dispatches in `ReviewRunner.AllRuleIds` — the real figure is **18**. Two audits, three documents,
  and the confident correction was also wrong. A reading discipline cannot fix a measurement that is
  easy to take incorrectly; only executable code reading the authoritative source can.
- **Proposal.** A small test asserting the *derivable* facts these documents claim, so a claim fails in
  a local `dotnet test` the moment it drifts. Candidate assertions, each cheap and unambiguous:
  1. **Rule count** — every doc stating a mechanized-rule count states `AllRuleIds.Length`. One
     assertion would have prevented three wrong numbers across two audits.
  2. **FI status coherence** — every `docs/16` entry marked `Implemented` that names a `converter <cmd>`
     has that subcommand dispatched in `Program.cs`, and does *not* appear in `AITODO.md`'s open list.
     This is F-11 and F-12, mechanically.
  3. **Skill inventory** — every skill named in `docs/15`'s tables exists in `.claude/skills/`, and
     every skill directory appears in `docs/15`. This is F-04 — and the 2026-07-20 clean bill on exactly
     this check, which had inverted within two weeks.
  4. **Artifact-name contract** — every artifact filename a SKILL says it *consumes* is one some SKILL
     *produces*. This is F-03: the `process-topology.md` nothing emitted, which degraded silently on
     every run for two weeks because "consumed if present" never errors.
- **Costs / risks.** Must assert **derivable** facts only — never prose. A check that fires falsely is
  worse than none, because the cure for a noisy test is to delete it. Keep it to identifiers and
  filenames; do not attempt to check meaning. The parsing is markdown-shaped and so itself drift-prone,
  therefore each check must **hard-error on parsing zero rows** rather than passing clean — the same
  rule FI-39's checks already follow, learned the same way.
- **Dependencies.** None. It reads `docs/*.md`, `.claude/skills/` and two C# symbols. Deliberately
  smaller than FI-39: no new subcommand, no new primitive — one test file in an existing suite.
- **Verdict / revisit trigger.** Open, and cheap. The audit's judgement was that this is the single
  highest-leverage item it found, precisely because it does not depend on anyone choosing to look — the
  same property that made the FI-36/FI-39 mechanical floor worth building for the spec pipeline. If a
  fourth audit finds this class again, that is the trigger to stop recommending it and build it.

### FI-41 — `converter review`: TAGTABLE files pass vacuously, including on C-001 and C-005
- **Status:** Raised (2026-08-05, from a greenfield generation run — the first time a tag table has been
  the *first* artifact produced rather than something inherited from an existing project)
- **Problem.** `converter review` on a tag-table `.ir` returns **zero findings**, and every rule reports
  `not applicable (TAGTABLE rule support not implemented)` — **including C-001 and C-005, the two rules
  that actually govern a tag table.** Zero findings reads as "checked and clean"; it means "not checked".
  The output does not distinguish *this rule does not apply to this file kind* from *this rule is not
  implemented for this file kind*, and that is precisely what makes the vacuous pass invisible.
- **Why it matters more than it looks.** C-004 freezes the equipment-identifier list at project start and
  C-001 fixes the physical-IO tag format. Those names then propagate into every instance DB, HMI tag,
  alarm text and cross-reference for the life of the project — renaming is free the day the table is
  written and expensive forever after. It is the highest-leverage naming moment in a job, and it
  currently has no mechanical floor at all. On a greenfield project it is also the *first* artifact, so
  the vacuous pass happens before any other check exists to catch it.
- **Proposal.** Mechanize the tag-table-applicable rules against TAGTABLE files:
  1. **C-001 physical-IO form** — `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>`, including the letters: `DQ`/`AQ`,
     never `DO`/`AO` (the 2026-07-17 owner ruling, owner-questions C-8).
  2. **C-005 charset** — letters, digits and underscore only, first character a letter.
  3. **Duplicate tag name and duplicate address**, within a table and across the tables of one device.
     Both are cheap set operations and both are silent-corruption classes.
  4. **C-004 support** — an agreed equipment-identifier set, so a *second alias for the same equipment*
     becomes a finding rather than a habit that is only noticed at HMI build.
  Also: report unimplemented-for-this-file-kind distinctly from not-applicable, so a vacuous pass is
  visible in the output rather than inferable only by reading the source.
- **Dependencies.** None. Items 1–3 are string and set checks over a file the parser already reads.
- **Verdict / revisit trigger.** Open, and cheap for 1–3. The trigger to build it is the next greenfield
  job, where the tag table is again the first thing written and again the thing everything else inherits.

### FI-42 — four converter/CLI gaps that only appear when you BUILD a data landscape
- **Status:** Raised (2026-08-05, from a greenfield UDT/DB stage — 14 UDTs and 12 DBs authored from
  nothing, rather than exported from a project that already had them)
- **Why they were never seen before.** Every prior run read structures OUT of an existing project.
  Authoring them IN exercises paths the round-trip corpus does not: nested user types, DTL members,
  block-access attributes, and deleting a type you created by mistake. All four below are *import- and
  compile-clean* — they bite on the read-back and management side, which is exactly where a
  round-trip verification lives.
- **The four.**
  1. **`to-ir` refuses a UDT whose member is itself a UDT-typed array.** TIA expands it into
     `<Sections>` and the parser stops: *"has nested structured content (`<Sections>`) — not confirmed
     real for a PLC data type's own member, refused rather than guessed at."* The refusal is the right
     default for an unobserved shape, but it is now observed, and it is ordinary design vocabulary — a
     history buffer inside a per-instance record. Until it parses, **an IR-level re-export diff is
     impossible for any object containing one**, so the round-trip has to be verified as XML instead.
  2. **`to-ir` refuses a nested DTL member carrying `Version="1.0"`.** *"unexpected attribute(s)
     [Version] — only Name/Datatype/StartValue have been observed on this shape."* Same class: a real
     attribute TIA emits, not yet in the observed set. Reproducible on a two-member throwaway DB.
  3. **The IR cannot express block access (Standard vs Optimized) or `MemoryReserve`.**
     `DbSourceWriter` emits no `MemoryLayout`, so every imported DB takes TIA's default (`Optimized`,
     reserve 100). A design that needs Standard — absolute-offset addressing, or removing the
     download-without-reinitialisation hazard rather than detecting it — cannot get there through this
     pipeline at all. See also `docs/notes/openness-quirks.md`.
  4. **`openness-cli delete` has no `--type`.** A UDT imported by mistake cannot be removed by the CLI;
     it needs the TIA UI. Trivial to add and annoying exactly once per mistake.
- **Ranking.** (1) is the one that actually blocks a workflow — it breaks IR-level round-trip
  verification for a common shape. (3) is the one with design consequences. (2) and (4) are small.
- **Verdict.** Open. (1) and (2) are both "extend the observed set and add a fixture"; the refusals are
  correctly conservative and should stay refusals for genuinely unobserved shapes.

### FI-43 — `openness-cli`: no `delete --type`, and no way to update an object in place
- **Status:** Raised (2026-08-05, owner-directed, from a live greenfield job). Supersedes FI-42's
  item 4, which recorded only half of this.
- **Problem, in two halves.**
  1. **`delete` has only `--block`.** A PLC data type imported by mistake cannot be removed by the
     CLI at all — it needs a human in the TIA UI. On a job that authored 14 UDTs and then renamed
     every one of them, that stranded 14 superseded types in the scratch project with no
     programmatic way to clear them. They were inert, but the project no longer matched the IR set,
     and *"go and delete these fourteen by hand"* is not a step an automated stage can own.
  2. **There is no update/overwrite path.** The working pattern today is delete-then-import, which
     for types is now impossible (half 1) and for blocks is a destructive round trip that throws
     away and recreates an object in order to change it. Openness import already has overwrite
     semantics available; the CLI does not surface them.
- **Why it matters more on a greenfield job.** Reading an existing project never creates an object
  you then want to remove or replace. Authoring one does it constantly — a rename pass, a type
  split, a corrected member. The gap is invisible until the tool is used to BUILD rather than to
  READ, which is why it has not surfaced before.
- **Proposal.**
  1. `openness-cli delete <project> --type <name> [--device <name>] --yes` — same safety shape as
     `--block`, same refusal on anything safety-related.
  2. An explicit **overwrite/update** option on `import` (e.g. `--overwrite`), so an existing object
     is replaced in place rather than requiring a delete first. Default stays non-destructive:
     importing over an existing object without the flag should refuse and say so, not silently do
     either thing.
  3. Both should report what they actually did — deleted / replaced / created — because an
     automated stage needs to verify the outcome rather than assume it.
- **Verdict.** Open, and small. (1) is a near-copy of the existing `--block` path. (2) is the one
  with real value: it removes a destructive round trip from the normal edit loop.

### FI-44 — "empty is not clean": the mechanical floor exits 0 when it examined nothing
- **Status:** **IMPLEMENTED 2026-08-05** — all three paths closed the day they were raised, on the
  second-plant pilot of the four-rung pipeline. **Was the highest severity item in this file:** the
  floor exists to be immune to an agent choosing not to look, and these three *rewarded* not
  looking. Each now exits **2** — deliberately distinct from both success and from a real finding,
  because "the plant is wrong" and "the question was wrong" call for different actions.
- **The class.** Three checks return success when they found nothing to check. Not "found nothing
  wrong" — *examined nothing at all*. Reproduced directly:
  1. **`candidate-scan --scope <token>`** → `CANDIDATE SET SIZE: 0`, **EXIT 0**. `--scope` matches a
     path *prefix*, which assumes DB-qualified signals. Under C-001 a physical-IO tag is
     `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>` — the equipment token is the **second** segment, so no
     prefix can express "scoped to this equipment". The scan silently returns nothing and passes.
     Worse: the *only* way to make it exit 1 is to name the disputed signals explicitly — i.e. to
     already know the answer. That inverts the tool's stated purpose ("the size is no longer yours
     to judge").
  2. **`undriven-scan --fb <name>`** → `0 pair(s)`, **EXIT 0**, for an FB *that does not exist*.
     Verified with a deliberately invented name. A block never written passes the drive-state check.
  3. **A D3 render leg whose instance ids match no other leg still parses**, so `relation-reconcile`
     compares it against nothing and is satisfied. The skill's own D3 example makes this concrete —
     it writes the iDB name where the parser keys on the spec filename, so following the
     documentation produces exactly this silent non-comparison.
- **The fix, and it is one principle not three.** **A check that examined nothing must not exit 0.**
  Distinguish *"I examined N things and none were bad"* from *"I examined nothing"*, and give the
  second its own non-zero exit.
  1. `candidate-scan`: an empty candidate set becomes a hard error. Size 0 is not "unambiguous", it
     is *unjudgeable* — either the scope was wrong or there is nothing there, and neither is
     evidence a binding is safe. Independently, add scope matching that can address a C-001
     equipment token (segment-aware, or a distinct `--equipment` flag) so the question can be
     asked at all.
  2. `undriven-scan`: resolve `--fb` against the corpus first; unknown name → hard error. There is
     no legitimate reading of "scan a block that does not exist" that ends in success.
  3. `relation-reconcile`: a leg that contributes zero matching keys is a hard error, not a
     vacuous pass. Fix the skill's example to match the parser in the same edit.
     **Implemented 2026-08-05.** A PRESENT leg that parsed relations but shares **not one key** with
     any other present leg is now reported as *"this leg was compared against nothing"* and exits
     **2** — the same "examined nothing" code as (1) and (2), distinct from a real difference (1).
     The check is computed on the RAW keys, deliberately **before** FI-45's disposition partition
     narrows anything, so the partition cannot become a second silent route to the same pass. The
     **ABSENT** leg keeps its exemption and still never gates: absence is a parse fact about an
     artifact that is not there (a stopped D3) and the tool says so; *matched nothing* is a
     comparison fact about an artifact that is there — different situations, reported differently.
     The skill's D3 example is fixed in the same change (`instance:` is the **spec instance**, the
     key the four legs join on — not the iDB name, which now appears only in the `CALL`), with the
     rule stated in prose beside it. `RelationDispositions.cs` + runner/formatter/model, 25 new
     tests including the iDB-keyed render (fails), the absent leg (still does not gate), and a
     one-key-shared render (a difference, *not* a matched-nothing — the true positive is untouched).
- **Why it went unseen.** All three prior runs of the pipeline stopped before D3, so the render leg
  had never existed and the reconciler had never had four legs to compare. And every prior project
  was read out of an existing plant, where a scope prefix happens to work because signals are
  DB-qualified. The floor was never wrong before — it had never been asked these questions.
- **Verdict.** **CLOSED 2026-08-05.** Each fix was a guard clause, as predicted. Verified against the
  live corpus that raised them: an invented `--fb` and an empty `--scope` both exit 2 where they
  exited 0, and `--scope <equipment>` now returns **7 candidates where it returned 0** — the scope
  fix did not merely stop lying, it made the tool answer the question it was always for, and the 7
  are a genuine ambiguity it now flags.
  Same family as FI-40, and worth restating as the lesson: **a check whose failure mode is silent
  success is worse than no check, because it is believed.** All three had been passing happily for
  three prior runs — they had simply never been asked a question they could not answer.
- **Residual, not closed by this work.** `ParseRender`'s `instance:` regex matches any line
  containing that substring, so prose ("Per instance: `X`") in a stopped-D3 artifact can build a
  *phantom* render leg. It does not fire on the committed corpus, and the new matched-nothing check
  catches the common form (phantom keys are usually disjoint, so exit 2 rather than silence).
  Tightening it to true D3 network headers needs a corpus-measured pattern and is a follow-up.

### FI-45 — the checks cannot parse two shapes that are ordinary, not exotic
- **Status:** **IMPLEMENTED 2026-08-05** — all three items closed the day they were raised, same
  pilot. Coverage gaps rather than safety gaps: these failed loudly, they just failed at *correct
  input*, which trains people to ignore the check and is how a gate dies quietly.
- **1. `tagstatus` cannot resolve a member through an ARRAY OF UDT.** **Implemented 2026-08-05.**
  `DB.Item[0].Member` →
  `MEMBER-NOT-FOUND`, exit 1; the unindexed form fails identically. A *named* UDT member
  (`DB.Word0.Cond`) resolves fine, so the descent machinery exists — it just does not strip an
  array subscript, nor continue resolving into the element type. Any project whose per-instance
  data model is an array of UDT (the normal way to express N identical vessels) has **every**
  per-instance binding read as an invented member: the exact laundering hard rule 3 exists to
  catch, fired at correct code. That trains people to ignore the check, which is how an
  anti-laundering gate dies.
  **Fix as built:** the member walk moved out of a null-check on `TagTypeRegistry.Resolve` into
  `TagStatus/MemberPathResolver.cs`, which answers a four-way question ("does it exist, and if not,
  what is wrong?") rather than a two-way one, while still reading its facts from the same
  `TagTypeRegistry` corpus — one index, only the diagnosis is local. It strips a subscript from any
  segment and continues into the element type, recursively (`DB.Vessel[0].Sensor[1].Reading`). The
  **unindexed** form resolves identically, deliberately: it is a legitimate *type-level* question
  ("does every element carry this member?") — the form a spec, requirements register or binding table
  uses — and `MEMBER-NOT-FOUND` there would call something real invented, which is the whole defect.
  It asserts nothing about *which* element; bounds are checked only against a subscript actually
  written. Out-of-range gets its own status, **`INDEX-OUT-OF-RANGE`** (blocking, exit 1), because the
  member IS real and "member not found" would send the engineer hunting the wrong thing; it speaks
  only when both index and declared bounds are integer literals, so a symbolic index (`[#i]`),
  `Array[*]` or a dimension-count mismatch is left alone rather than guessed at — a bounds check that
  guesses would recreate the crying-wolf failure this entry is about. Two side effects worth naming:
  `TagTypeRegistry.Resolve` was blind to the same shape and is fixed too (so an operand inside an
  array of UDT now types correctly for `to-xml --synthesize` and C-118), and the walk being
  depth-aware means an unknown type three levels down now reports `MEMBER-UNCHECKED` at that depth
  instead of the old unconditional `MEMBER-NOT-FOUND` for the whole path. Verified against the pilot
  data that raised it (indexed, unindexed, out-of-range, invented-member-inside-the-element-type all
  classify correctly); +8 tests, none weakening the true positive.
- **2. `signal-sweep` cannot disposition a PLC tag-table signal at all.** **Implemented 2026-08-05.**
  Dispositions are qualified
  `<heading>.<leaf>`; the inventory stores tag-table tags unqualified, and the parser only leaves a
  token unqualified when it already contains a dot — which a tag name never does. So a tag-table
  signal can never be matched to its disposition row. On a device with no buffer DBs this disables
  the whole disposition half while the DB half works perfectly. Its `by DB` grouping also splits on
  the first dot, so flat tags render as one-row "DBs" with broken alignment.
  **Fix as built:** the inventory keeps the tag's `Path` **bare** — that is how a tag is written in
  IR, and `ProjectUsageGraph`/`candidate-scan`/`undriven-scan` all key on it, so qualifying it there
  would have broken three working checks to fix one. The table name rides alongside as
  `SignalLeaf.Container`, and `signal-sweep` qualifies to `<TableName>.<TagName>` on its own side to
  meet the disposition row. Matching stays **strict equality** — a bare-name match against any
  heading would let a same-named DB member disposition a tag nobody accounted for — and the residual
  risk that strictness carries (a heading spelled unlike its container) is now a stated warning
  rather than a silent miss. Grouping is by container with a `DB`/`tag table` kind column and a
  computed column width; the JSON `byDb`/`db` keys become `byContainer`/`container` + `kind`. The
  heading regex also accepts any backticked heading, so a table really called `Default tag table` is
  dispositionable at all. The disposition-artifact contract is **unchanged** — one `### `<name>``
  heading per container was always the rule; it simply now works when the container is a tag table.
  Verified on the pilot run that raised this: 38 falsely-unaccounted → **0**, and 62 one-row "DBs" →
  3 tag tables. +7 tests.
- **3. `relation-reconcile` cannot tolerate five of its own seven ledger dispositions.**
  **Implemented 2026-08-05.**
  `discharged`, `in-FB`, `render-BLOCKED`, `render-stopped` and `out-of-scope-obligation` all mean
  *"this relation does not become a D3 term"* — yet the reconciler demands the render leg carry an
  identical key set, so any such row exits 1. `render-BLOCKED` is unreachable if the check must
  pass, which makes a documented disposition undeclarable.
  **Fix:** partition the ledger by disposition and require key identity only on the render-bound
  subset; report the rest as accounted-for rather than as differences.
  **Fix as built.** `RelationDispositions.Classify` reads the D2 vocabulary out of the ledger cell and
  answers one question — *does this row owe a D3 term?* Only `rendered` and `rebind` do. Key identity
  is now required only INTO the render (`from.Keys ∩ render-bound`); out of it nothing is filtered, so
  a term tagged with a relation no artifact declares is still the C-606 finding it always was. The
  other five report in a new `ledger dispositions` block as *accounted for — not a D3 term, not a
  difference*, so `render-BLOCKED` is declarable in an artifact that passes its own self-check.
  Measured against the real cells, not the SKILL alone: the corpus writes `render` (not `rendered`),
  `**rebind**`, `discharged (S5)`, ``render-BLOCKED `[contested]` `` and compounds like
  `in-FB + render(driver)` — so classification scans for tokens and a render-bound token anywhere in
  the cell wins. An **unrecognized** disposition is treated as render-bound (fail closed) and warned
  about: the SKILL records runs inventing dispositions, and an invented word must never be the cheap
  way out of the render obligation. On the committed corpus (rerun2/rerun3) the partition is
  informative and the result unchanged: 174/174/174 and 168/168/168, render ABSENT, exit 0.
- **Verdict.** **CLOSED 2026-08-05**, all three. (1) was the one that mattered — it fired on correct
  code in any multi-instance project, which is most of them. It also turned out to run deeper than
  reported: `TagTypeRegistry.Resolve` had the *same* blindness, so an operand inside an array of UDT
  was **typeless for `to-xml --synthesize` and for C-118**, not merely misreported by `tagstatus`.
  Fixed there too, strictly additively.
  Verified on real data rather than fixtures: 639 existing member paths still EXISTS, the 5 known
  true-positive MEMBER-NOT-FOUND names still fail, 91 nested/array paths now resolve — and 3 came
  back INDEX-OUT-OF-RANGE **correctly**, because the probe used `[0]` on arrays whose declared lower
  bound is not 0. A new check catching a real mistake on its first run is the evidence worth having.

### FI-46 — three residuals from the FI-44/FI-45 wave, found but deliberately not fixed
- **Status:** Raised (2026-08-05). Each was found while fixing something else, and each was left
  alone on purpose: fixing a thing you were not sent to fix, in a shared tree, without a corpus
  measurement, is how a clean wave acquires an unreviewed change.
- **1. Bit-slice components read as members.** `DB.SomeWord.%X0` reports MEMBER-NOT-FOUND.
  `AccessNode.FromDottedPath` explicitly models a trailing `.%X0` slice, but the member walk treats
  `%X0` as a member name. Pre-existing — the pre-FI-45 code did the same — and the same
  false-positive family as FI-45 item 1: a real path reading as invented. **Fix:** drop a leading-`%`
  final component before walking. Cheap.
- **2. `converter review` / `preflight` were never checked for the same array blindness.**
  `TagTypeRegistry` is now fixed, so anything resolving through it inherits the fix — but any rule
  doing its *own* member walk still has the defect, and nobody has looked. **Fix:** audit the rules
  for independent member walks; route them through the registry.
- **3. `ParseRender`'s `instance:` regex is too loose** — it matches any line containing that
  substring, so prose in a legitimately-stopped D3 artifact can build a phantom render leg. Does not
  fire on the committed corpus; the new FI-44 matched-nothing check catches the common form. **Fix:**
  tighten to true D3 network headers, which needs a corpus-measured pattern first.
- **Verdict.** Open. (1) is the cheapest and has a live false positive today. (2) is the one with
  unknown extent, which is its own argument for looking.

### FI-47 — ` RETAIN` on a UDT member parses clean and is silently dropped crossing to XML
- **Status:** Raised (2026-08-06). Found while verifying a data-structure stage, not by a test.
- **The defect.** A UDT's member lines share `DbMemberLineFormat`'s grammar with a DB's, so
  `<member> : Bool RETAIN` inside a `TYPE` **parses successfully**. But a real UDT member's XML
  carries no `Remanence` attribute at all, so `WriteTypeMember` has nowhere to put it and the token
  vanishes. `TypeIr.cs`'s own header documents this ("the shared line grammar would still *accept* a
  hand-authored ` RETAIN`, which the XML writer then has nowhere to put") and `ir/SPEC.md` calls it a
  documented edge — so it is **known and deliberate, recorded as prose rather than enforced in code**.
- **Why prose is not enough here.** Retention is the one DB property whose loss is invisible until a
  power cycle: the block compiles, imports, exports and round-trips clean, and the omission surfaces
  as data that did not survive an outage — on site, months later. This is the FI-44 family exactly
  (a check that passes because it examined the wrong thing), with a worse failure mode: FI-44's
  silent cleans were caught by re-running a fixed check, and this one is caught by an outage.
- **Also: `to-ir` round-trips a lie.** Import a UDT whose author wrote `RETAIN`, export it, and the
  IR comes back without it — so `drift-check` and the golden harness both report agreement between
  two files that say different things, because the difference was destroyed on the way in.
- **Fix.** Hard-error on ` RETAIN` (and ` VERSION`) in a `TYPE` member line at parse time, naming the
  member and stating where retention *is* declared — the FB static or DB member that instantiates the
  type. A hard error is right rather than a review finding: there is no valid program in which the
  token means anything, so there is nothing to weigh. Cheap, and the correct behaviour is already
  written down — this is enforcement of an existing documented rule, not a new one.
- **Verdict.** Open, and worth doing before the next data-structure stage. The current mitigation is
  that an author has to know the rule and apply it by hand every time, which is the mitigation FI-44
  was raised to stop relying on.

### FI-48 — `to-xml` can reorder coils within a network, and coil order is semantic
- **Status:** Raised (2026-08-07). Found by a coding agent doing a re-export diff, then hit for real
  by a second agent on the same block.
- **The defect.** A network's `COIL` and `RCOIL` came back from `to-xml` → TIA → `to-ir` in the
  opposite order to the IR that produced them, and to the order the real site export has. The IR
  treats `COIL`/`SCOIL`/`RCOIL` as **one contiguous kind-run** (`ir/SPEC.md`, statement-kind
  ordering), so their relative order inside that run is content, not formatting.
- **Why it matters and is not cosmetic.** Coil order decides same-scan freshness — a coil written
  before another reads its *new* value, after it reads last scan's. This project has now been bitten
  by that four separate times in one week (a never-incrementing hours counter; the valve's cycle
  edge; an ET sample that must precede its timer; and a cast-out condensation that reports a scan
  late). So a transform that silently permutes coils can change behaviour, and the case where it
  happens to be inert is luck rather than safety.
- **Where it is NOT.** `FlgNetWriter` emits parts in **UId order** (`FlgNetWriter.cs:329`), which is
  required — TIA rejects a `<Parts>` whose `<Access>` elements are not UId-ascending, which is what
  `FlgNetWriterPartOrderTests` already guards. The writer is doing the right thing.
- **ISOLATED 2026-08-07, and the first guess in this entry was WRONG.** It originally said the
  suspect was a mixed `COIL`/`RCOIL` run. **It is not** — a block network carrying a mixed
  `SCOIL`/`COIL`/`COIL`/`RCOIL` run round-trips clean and always did. Seven converter-only probes
  pinned the actual trigger. **All three conditions are required:**
  1. the coil is a **Set or Reset** coil (a plain `COIL` in the same position is stable);
  2. its expression is **exactly a timer's `Q`, bare, with no other term** (`RCOIL C := T1.Q AND A`
     is stable; so is a bare non-timer tag);
  3. it is **not already first** in the coil run.
  The effect is **promotion to the front of the coil run**, not a swap of a pair.
  Minimal reproduction, one network:
  ```
  TON(T1, IN := A, PT := T#100MS)
  COIL  B := A AND NOT C
  RCOIL C := T1.Q          <- comes back FIRST
  ```
- **The mechanism, and it explains all three conditions at once.** This is **Gap G2**
  (`SidecarSynthesizer.BuildAssignment`): a coil fed *directly* by a same-network timer's `Q` wires
  **straight from the TON's Q port — no rail, no Access** — because that is how TIA itself exports
  it. And per that method's own logic, a **plain `Assign` coil takes the direct wire only for a
  GLOBAL-instance timer, while a Set/Reset coil takes it for a same-network LOCAL-instance timer
  too** — which is exactly why condition 1 selects S/R coils. So the qualifying coil is **the only
  one in the network that is not on the rail**, and it comes back first because reconstruction finds
  off-rail coils by a different path than the rail walk. Condition 3 is just "it was not already
  where that path puts it".
- **Fix.** Order reconstructed assignments by their part UId — document order as the XML actually
  carries it — rather than by how they are fed. That is the **one general ordering guarantee** the
  standing rule asks for on a second instance of this bug class, not another special case. The
  writer needs no change.
- **Second symptom, probably the same root.** The affected block's TIA re-export is **not
  re-derivable** — `to-ir --no-sidecar` hard-errors (ADR-0005) and `drift-check` reports `DRIFTED`
  even against the export the IR came from. Other blocks derive clean, so it is content-specific.
  **Practical consequence: `drift-check` cannot police that block**, so one of the project's
  mechanical gates is silently absent on it.
- **Fix.** Isolate first — the two symptoms may be one root or two. When fixing, note the standing
  rule that on a *second* instance of a document-order bug class the answer is **one general
  ordering guarantee, not another special case**; this is at least the second.
- **Verdict.** Open. Worth doing before the next block goes into `patterns/`: a library block that
  cannot be round-trip verified ships without one of its gates.

### FI-49 — a member name shared across two types is an unenforced contract
- **Status:** Raised (2026-08-07), by the agent that created the situation and then noticed it.
- **The shape.** Two interface types can be required to carry a member with the same name AND the
  same layout — because one HMI faceplate, one severity routine and one review serve both. On the
  driving job that is a vessel-system alarm word shared by two vessel classes, protected by a rule
  that says its bits must never be renumbered.
- **What is enforced today: nothing.** The requirement lives in a comment on each type. Change one
  and not the other and **it breaks silently** — everything compiles, reviews clean and round-trips,
  because each type is internally consistent. That is the same failure mode as the collision the
  rename was fixing: a name that survives while the meaning underneath it moves.
- **Worth noting the rename was still a strict improvement.** Before it, the identity was invisible
  *and* unenforced; after it, visible and unenforced. This entry is about closing the second half.
- **Fix.** A cross-file check: **where two types declare a member of the same name, their shapes must
  agree** — same datatype, and for a word-typed member the same bit allocation as far as the
  comments declare it. `converter review --project` already does cross-file work (C-118, C-122,
  C-125), so the seam exists. Two honest questions before building it: whether same-name-different-
  shape is ever *legitimate* across unrelated types (if so the check needs an opt-out, and an opt-out
  nobody sets is worse than no check), and whether bit allocation is reliably parseable from comments
  or needs declaring.
- **Verdict.** Open. It is the mechanical floor's own argument applied to a rule currently held by
  discipline — and this project's record is that discipline-held invariants fail quietly, which is
  why FI-44 exists at all.

### FI-50 — `undriven-scan` could not see a multi-instance, which is the house interface style
- **Status:** **BUILT AND FIXED 2026-08-07**, same day it was found. Kept as a record because the
  *shape* of the miss matters more than the fix.
- **The shape.** `undriven-scan` resolved instances by looking for DB sources carrying an
  `InstanceOf`. A **multi-instance** — an FB placed as a `STATIC` member of another FB,
  `ValveWater : "FB_Valve"` — has no DB of its own and was therefore not an instance at all. Every
  FB on the driving corpus reported `no instances`; the scan examined **zero members of zero
  instances** and said so quietly.
- **Why it is not a corner case.** C-132 makes the single-`STATIC`-UDT interface the house style, so
  a project can be built entirely out of multi-instances. The check that exists to catch an undriven
  interface member was structurally blind to **every block in the project**, and the blindness scaled
  with adherence to our own convention. A reviewer doing the sweep by hand found real undriven
  members the tool had reported nothing about.
- **This is FI-44 again, through a door FI-44 did not check.** FI-44 made "examined nothing" exit 2
  instead of 0, which is exactly what saved this: the command was *loud* about finding no instances,
  and that is what got it looked at. The lesson is not that FI-44 was incomplete — it is that
  **"empty is not clean" needs re-asking every time a new shape enters the corpus**, because the set
  of ways to examine nothing grows with the codebase.
- **What the fix had to get right**, both non-obvious:
  1. **Addressing.** A multi-instance is written bare inside its owner (`ValveWater.IO.Cmd`) with no
     instance root, unlike an iDB. So lookups use the local form **restricted to the owning block** —
     without that restriction two FBs sharing a static name pool each other's writers and each masks
     the other's gap, which would have turned a blind check into a wrong one.
  2. **Nesting.** Resolution iterates to a fixpoint, because multi-instances nest. A single pass
     roots a nested placement at its declaration site and loses the per-instance resolution the
     command exists for.
- **A judgement recorded rather than buried:** where the owning FB has **no instance DB yet**, the
  placement is reported under a declaration-site path (`FB_SiloVessel/ValveWater`). That is a class,
  not a placement, and the `/` says so. Reporting it is right — a block written before its caller is
  otherwise unexaminable, and unexaminable is the state this entry exists to abolish.
- **One false positive removed with it.** IEC timer/counter statics are excluded: a `TON_TIME`'s `Q`
  is written by the timer instruction, not a caller, so the question is meaningless for them. Left
  in, they were the loudest noise in the new output — and noise in a check is how a check gets
  ignored.
- **Verdict.** Closed. Four regression tests: per-placement resolution, declaration-site form, the
  timer exclusion, and a placement not being counted as a leaf of its own owner.

### FI-51 — an array subscript could only be expressed on the LAST component of a path
- **Status:** **BUILT AND FIXED 2026-08-07.** Found while asking why four vessels on a live job
  had no weight.
- **The shape.** `AccessNode` carried a single `int? ArrayIndex`, documented as "seen only on the
  last component". So `DB_Weigh.Silo[0].RawValue` — an **array of structs**, indexed in the
  MIDDLE of the path — had nowhere to live.
- **The two halves disagreed, in the worst possible direction.** The parser **refused** a non-final
  indexed component with a clear `UnsupportedConstructException`. The writer **silently emitted**
  `<Component Name="Silo[0]" />` — a component literally *named* `Silo[0]`, which names no member
  and which TIA rejects on import. A read path that refuses and a write path that corrupts is
  strictly worse than either alone: the loud half never fires on content the quiet half produced.
- **What it cost.** On the driving job the weighing interface is
  `Silo : Array[0..3] of UDT_WeighSilo`, so the input map could not be written. **No vessel had a
  weight**, and every weight-derived judgement on the plant — stability, trust, the overfill
  defence, and the inference of valve position that thirteen blind valves depend on — ran on zero.
  The block comment recorded it as a tooling limitation and moved on, which is the right thing to
  do in the moment and exactly how a tooling gap becomes permanent.
- **This was the SECOND defect of this class**, and that decided the fix. The first collapsed
  `Node_Error[1]`/`[2]`/`[3]` into one indistinguishable path — caught by the owner reviewing the
  result in TIA, not by a test. The standing rule is that a second instance earns the **general**
  fix rather than another special case, so the positional modelling was retired outright instead
  of extended to "last component, or the one before it".
- **The fix.** The index is a property of a **component**, not of an access — so it now rides in
  the component itself as `Name[n]`, at any position. `ArrayIndex` is gone; `DottedPath` is a plain
  join with no positional logic left in it to get wrong; parser and writer treat every component
  identically. The bit-slice modifier stays access-level, because a slice genuinely *is* one — it
  addresses a bit within whatever the whole path resolved to.
- **Verified:** 785 converter tests, 39 golden round-trip tests, and `drift-check` against the
  committed export corpus unchanged at its known baseline (`ExportDriftDetectorTests` asserts the
  drifted set equals its baseline, and still passes). Four regression tests: mid-path subscript,
  the trailing case that already worked, subscript-plus-slice composed, and several subscripts in
  one path.
- **The lesson worth keeping.** "Only ever seen on the last component" was an honest observation
  about a corpus, and it hardened into a modelling decision. Refusing the unobserved shape on the
  read side was right. What was wrong was letting the write side produce that same shape without
  refusing — **a converter's two directions must agree about what is inexpressible**, or the
  refusal is not a guard, it is only an inconvenience on one side.

### FI-52 — the compile gate could return a false pass, and the warning was in the wrong place
- **Status:** **BUILT AND FIXED 2026-08-07.** Found by an agent that distrusted its own green result.
- **The shape.** `openness-cli compile <project>` (whole device) returned `STATE: Success,
  ERRORS: 0, WARNINGS: 0`. In fact **19 of 34 blocks had not been compiled at all**, and one of
  them — compiled individually — failed with **8 errors**. A block freshly re-imported through
  Openness's `Import()` carries `IsConsistent=false`, and device-level `Compile()` reports success
  without ever clearing it.
- **Why this one matters more than the others.** Hard rule 4 — *"never present non-compiling logic
  as finished"* — is the project's last line of defence before an engineer sees the work, and its
  instrument could report a clean pass over unexamined blocks. Every "compile gate passed" claim
  made with a bare device compile was weaker than it read.
- **THE REAL DEFECT IS NOT THE QUIRK. IT IS WHERE THE QUIRK WAS WRITTEN DOWN.** The behaviour was
  known and documented on **2026-07-10** — in `IOpennessGateway.CompileBlock`'s own doc comment and
  in `docs/notes/openness-quirks.md` — nearly a month before it bit. It bit anyway because
  `CLAUDE.md`'s hard rule 4 and its Commands table both still named a bare `compile` as the gate,
  with no cross-reference. The knowledge was captured; the instruction that contradicted it was
  not updated. **A trap recorded somewhere the person about to walk into it does not read is not
  recorded.**
- **What caught it.** Not a test and not the rule — an agent that got a suspiciously terse
  seven-line success with no per-block output, and block-compiled something it had *not* touched to
  see whether the result meant anything. That instinct is the actual control here, and it is not
  mechanical, which is why the fix below had to be.
- **The fix.** `compile` now fails closed: after a clean whole-device run it enumerates blocks and,
  if any remain inconsistent, lists them and exits **11 `CompileIncomplete`** rather than 0 —
  deliberately distinct from `8 CompileFailed`, because nothing reported an error; the gate simply
  did not examine everything, and those two call for different actions. Safety blocks cannot trip
  it: `EnumerateBlocks` reports them consistent by construction rather than reading their flag
  (hard rule 2), so this is not a back door into safety content. `CLAUDE.md` hard rule 4, the
  Commands table and the openness-cli README all now name `sanity-check` or per-block compiles as
  the gate.
- **Same family as FI-44 and FI-50, one layer up.** Those were checks that examined nothing and
  exited 0. This is the *compile gate itself* doing it — and unlike them it had no "examined
  nothing" signal at all to notice, because the device compile genuinely did run and genuinely did
  succeed at the thing it was actually doing. **"Empty is not clean" has to be asked of the gates,
  not only of the checks.**
- **Verdict.** Closed on the mechanism. Worth a standing habit: when a documented quirk contradicts
  a top-level instruction, the instruction is the thing to fix — the doc comment has already
  proved it cannot carry the warning alone.

### FI-53 — the reference graph did not credit a read taken THROUGH an array element
- **Status:** **BUILT AND FIXED 2026-08-07**, hours after FI-51, and found by the agent that FI-51
  had just unblocked.
- **The shape.** An `Array[0..3] of "UDT_X"` member is inventoried as ONE leaf (`DB_ParamRet.Silo`),
  because the signal walk does not expand a UDT sitting behind an array. Every real reference,
  though, goes through an element AND a member — `DB_ParamRet.Silo[0].ZeroOffset`. `cross-check`
  looked the declared path up by exact string, found nothing, and reported the member
  **`unused (no writer, no reader)`**.
- **Measured, not theorised.** On the live corpus `DB_ParamRet.Silo` reported dead against **24**
  real readers and `DB_WeighInterface.Silo` against **16**, while plain scalar siblings *in the same
  DB* listed their readers correctly — which is what makes it so easy to believe.
- **This is the more dangerous half of the array problem.** FI-51's failure was a corrupt write that
  TIA would reject. This one is a check quietly telling you a live member is dead, and the natural
  response to "unused" is deletion. Acting on it would have removed the plant's entire weighing
  path — the one FI-51 had just made expressible.
- **Fix.** `ProjectUsageGraph.UsagesCovering(declaredPath)` pools every usage landing on a declared
  member or anywhere inside it, with array subscripts stripped for the comparison. Prefix matching
  respects component boundaries, so a member `Slot` cannot absorb its sibling `SlotCount` — that
  is a regression test, not a hope. Reader/writer lists are now deduplicated, because pooling makes
  repeats ordinary (three members of one element read in one network are three usages and one
  reader).
- **Granularity, stated rather than fudged:** "any element counts". The declared thing is one member
  of one type and the question is whether anything uses it. Whether one particular ELEMENT is unused
  is a different question with a different answer shape, and is not pretended to be answered.
- **The pattern across FI-50, FI-51, FI-52 and this one, all found the same day.** Four checks, four
  different mechanisms, one failure: **each reported a clean result over something it had not
  examined.** A multi-instance that was not an instance; a subscript that could not be written; a
  device compile that skipped 19 blocks; an array member whose readers did not match its name. None
  was caught by a test — three were caught by an agent distrusting a clean result, and one by an
  engineer's question about why a plant had no weight. **That is the control that is actually
  working, and it is not mechanical.** The mechanical floor's job is to make the clean results
  trustworthy enough that distrust is rare; the honest reading of today is that it is not there yet.
- **Verdict.** Closed. 787 converter tests, 39 golden tests.

### FI-54 — HMI capability probe programme
- **Status:** Implemented (partial) — **phase 1 landed 2026-08-07/08** (reconnaissance + the first live
  writes); **phases 2–4 are planned and unstarted** (deletion; alarms + the data-plumbing half; classic).
- **Raised:** 2026-08-08 · **Source:** owner instruction 2026-08-07/08 — first "decode the HMI screen XML", then, when it turned out there is none, "how much of an HMI could be driven programmatically, and where are the walls" — and then walk it rather than reflect it.
**Merits:** Reflection tells you what compiles; only walking tells you what happens. Phase 1 measured the surface (**551 public HMI types, 4549 declared public members**) and then exercised the distinct *mechanisms* of driving a Unified HMI end to end — read the device tree, enumerate screens through their groups, dump the creation/attribute schema, create a screen and items, set attributes with type coercion, attach events and script bodies, create a tag table and tag, bind a property to a tag, validate, compile, save, and read back from a fresh process. Two tools came out of it (`openness-cli hmi` read-only, `hmi-create-screen`/`hmi-edit-screen` as gated probes) and two notes (`docs/notes/openness-hmi-api-survey.md`, `docs/notes/openness-hmi-write-api.md`). The payoff is not the coverage number — it is that **~2% of members walked produced three consequential surprises**, each of which had first been written down confidently from the reflection map and was then measured false: alarm-class "states" are visuals not acknowledgement semantics, the plant model is half writable rather than read-only, and `Validate()` is not a gate. Walking also *added* capability the reading had denied — the device compile catches script errors and dangling tag references by name, which two successive versions of the notes had concluded did not exist. A programme that keeps converting reflection into measurement is the only thing that makes the remaining 98% plannable, and it feeds ADR-0007 facts instead of a guess.
**Costs / risks:**
1. **Deletion is the unexercised half, and it is the unrecoverable one.** **0 of 184** deletable types have ever been touched. Phase 2 needs the `--yes` dry-run/confirm discipline `openness-cli delete` already has, on throwaway `ZZ_AI_*` objects only.
2. **There is no transaction.** A probe that throws part-way can leave a partial object that persists without a `Save()` (a tag did exactly that). Probes must be written re-runnable, and a failure must never be read as "nothing happened".
3. **It runs against a live job's scratch copy** under the `13-data-boundary.md` JOB9002 write extension — so every probe carries the usual Portal-session and retention rules, and every name written into a note is invented.
4. **Probe tooling can be mistaken for capability.** CLAUDE.md already states the guard (`hmi-create-screen` is a capability probe, not an HMI capability); ADR-0007 is where that stops being a comment.
5. **A walk with no decision attached becomes make-work.** Each phase should be scoped to close a *named* unknown, not to raise the coverage percentage: phase 2 = deletion + alarm creation (311 alarms exist and `MultilingualText.Items.Find(language)` for alarm text is entirely unverified — the awkward part); phase 3 = connections, data logs, alarm logs, logging tags, and the other 5 dynamization kinds (`Script`, `Flashing`, `Expression`, `ResourceList`, `TagParameter`); phase 4 = classic, 64 types and zero live contact.
6. **Phase 4 is externally gated** — no classic device exists in the available project, so its whole SimaticML round trip stays untested until a classic job turns up. **And it cannot be unblocked from inside this project:** a filesystem sweep of every local TIA project found exactly one HMI `DeviceType` (the Unified panel), and *adding* a classic device is hardware configuration via Openness, which `10-non-goals.md` lists as its own separate non-goal. So phase 4 waits on a real classic job arriving — it is not a matter of scheduling.
**Dependencies:** **FI-18 and FI-35 both carry unknowns this closes.** FI-18's boundary contract cannot be drawn without knowing what the HMI side can be made to expose — the readable `Tag`/`PlcTag` join, first-class trigger bit numbers, and the three separate alarm-class axes are all phase-1 findings that turn FI-18 from guesswork into a mapping decision. FI-35 §5's "can Openness drive the HMI alarm list directly?" is answered (yes on Unified, absolutely not on classic), but its *generation* half is still untested — **no alarm has ever been created** — which is phase 2's first item. Also **ADR-0007**, which this programme feeds and which decides whether any of it may become capability rather than probe. Phase 4 waits on classic hardware.
**PROGRAMME RUN AND CLOSED, 2026-08-08/09.** Six phases (`docs/notes/hmi-capability-probe-plan.md`), raw transcripts in `docs/evidence/hmi-capability-probes.md`. The Unified phases named above are **all done**; only phase 4 (classic) remains, and it is externally gated as cost 6 says. Live coverage went ~2% → ~3% of members, but the useful movement is that **item types and dynamization kinds are now exhaustively attempted**, so their refusal sets are facts rather than gaps. The bet in Merits paid out again, and again mostly in negatives: **deletion orphans silently**, **alarm text cannot be written at all**, ~~**3 of 6 dynamization kinds refuse** (including `Flashing`)~~ **— RETRACTED by P7, see below —** and **`GetCreationInfos` overstates creatability by 21 of 56**. **P7 (2026-08-09, unplanned, prompted by the owner asking about the refusals) showed the dynamization finding was the PROBE's fault**: kinds are gated on the target property's type, and P3 had bound all three to a Boolean. `Flashing` creates on colour properties, `ResourceList` on text properties — **5 of 6 kinds create**. The bet paid out a third way there: walking corrected the walk. The methodological finding is the durable one — **a refusal is evidence about that call, never about the capability**, because the message never says why; this project has now generalised from one refusal and been wrong twice. Cost 1 is closed (deletion exercised across nine kinds, all cleaned up). Cost 2 held exactly as written — a failed create persisted without a `Save()`. The programme also exposed three defects in the probe *tooling*, all one class: **it reported the intent rather than the outcome** (`--in` silently discarded while claiming success; refusals counted as changes; partial refusal exiting 0). All fixed and guarded.

**Verdict / revisit trigger:** Open, but **its Unified half is spent** — the remaining Unified questions are narrow follow-ups (why `set_Text` refuses; whether `RaisedStateTagBitNumber` is contextual; whether the three refused dynamization kinds have another route), not a programme. **The decision now sits with ADR-0007, which has the measurement it was waiting for.** Revisit if ADR-0007 is rejected (the write commands then need a disposition) or if a classic job arrives and unblocks phase 4.
### FI-61 — a rebuilt binary is refused by Openness silently, and every message we had blamed the wrong thing

- **The bug is not in our code; the bug is that our code could not say so.** TIA whitelists Openness
  callers by `(Path, FileHash)` in `HKLM\SOFTWARE\Siemens\Automation\Openness\<version>\Whitelist\`.
  Rebuild the executable and no entry matches its hash, so Openness refuses it and **never
  responds** — `Attach()` blocks until the caller's own timeout expires, with **no dialog, no
  exception and no log entry**. It is externally indistinguishable from a wedged Portal.
- **What it cost.** A `dotnet test` on `openness-cli.sln`, run to check something unrelated, rebuilt
  the binary while an agent was mid-run. Every attach after that hung for its full timeout — 3 min,
  then 15, then more. About an hour went into "Portal is wedged, a human must clear a dialog".
- **Two pieces of our own tooling actively pointed away from the cause.**
  `ConnectTimeoutException`'s text asserted the approval dialog as the usual cause, and `portal-status`
  was taken as evidence that no dialog existed — which it can never be, since it is the one
  subcommand that deliberately never attaches. **A tool that cannot observe X is not evidence about
  X**, and it read as evidence because its output was clean and confident.
- **How it was settled.** `EnumWindows` across both Portal processes showed every main window visible
  *and enabled*; that is suggestive but not conclusive for a WPF app, where a modal can be an
  in-window overlay with no HWND. So each window was captured with `PrintWindow` and **looked at**.
  Both idle, no dialog. Then the decisive comparison: a known-approved build listed the same project
  in **57 s** while the rebuilt one hung. One command separates the two hypotheses.
- **Built.** `OpennessWhitelist` hashes the running executable and checks it against every Openness
  version key before attaching, warning with the cause and the count of prior approvals. Verified
  live: *"84 earlier build(s) of this exact path are approved, but none of them matches this file's
  current hash."* `ConnectTimeoutException` now lists both causes and no longer claims to know which.
- **Deliberately advisory, never blocking.** A false negative — a whitelist layout it does not
  understand, a registry view it cannot read — would refuse every Portal command on a machine where
  everything works. That is strictly worse than the hang it prevents, so it warns and proceeds.
  Re-approving a build means writing to an `HKLM` security control, which is the machine owner's
  call and deliberately out of scope for the tooling.
- **Same family as FI-44 and FI-52, and the sharpest instance yet.** Those were checks that reported
  a clean result over something they had not examined. This one is a *diagnostic* that reported a
  clean result about something it structurally could not see, and then that clean result was quoted
  back as proof. The lesson worth keeping is narrow and general: **when a check clears a hypothesis,
  confirm the check can observe the thing it is clearing.**
- **Verdict.** Built and verified live. 148 openness-cli tests (+8).

### FI-62 — `sanity-check` enumerated blocks only, so a UDT could be inconsistent behind a green gate

- **Hard rule 4 names `sanity-check` as THE gate, and it had a blind spot.** It walked `PlcBlock`
  and nothing else. A `PlcType` is not a block, so a freshly-imported UDT was never examined.
- **Measured, not theorised.** On a live job it reported `OVERALL: HEALTHY`, `BLOCKS: 52`,
  `INCONSISTENT: 0`, device compile `Success (errors=0, warnings=0)`, exit 0 — and TIA then refused
  `export --type UDT_Drum` with *"Inconsistent blocks and PLC data types (UDT) cannot be exported."*
  The agent checked the source rather than guessing: `ExportType` has no consistency guard of its
  own, so the refusal came from TIA.
- **Why the compile did not catch it either.** Nothing in that corpus instantiated the type. An
  uninstantiated UDT has nothing to make a device compile fail, so both halves of the gate were
  green simultaneously while the type was inconsistent.
- **Built.** `RunSanityCheck` now walks `PlcTypeGroup` alongside blocks, reports a `TYPES:` line
  **always** (including at zero, so a reader can see types were examined rather than silently
  absent), lists any inconsistent type with the `compile --type <name>` line that clears it, and
  counts types toward `IsHealthy`. The existing type walk was reused with a null-means-all filter
  rather than growing a parallel "enumerate all" copy that could drift from the lookup path.
- **Same family as FI-52 and FI-44, and the third instance in four days:** a check reporting a clean
  result over something it never examined. The recurring shape is worth naming — *a gate's scope is
  a claim, and an unstated scope reads as "everything".* `BLOCKS: 52` was on screen the whole time;
  nobody read it as "and zero types".
- **Sequencing note.** Landing this needs a rebuild, and per FI-61 a rebuild revokes the binary's
  TIA Openness approval. The source change is committed; the rebuild is deliberately left for the
  machine owner to approve rather than done mid-delivery.
- **Verdict.** Built, 152 openness-cli tests (+4). Not yet live-verified — it cannot be, until the
  rebuilt binary is approved.
