# 16 — Future Ideas

🛑 **PROMOTION SUSPENDED 2026-08-17.** The staged development plan is suspended
(`docs/03-development-plan.md`), so **no entry here is promoted into the plan or built while it
stands** — the FI backlog is held. Two things continue unchanged, because this doc's discipline is
what keeps ideas from being lost: **entries may still be raised and recorded**, and **no entry is
silently deleted**. An idea raised during the suspension gets its verdict when the plan resumes;
until then, "Raised" is the correct status, not "Rejected".

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

## Index — coverage repair, 2026-08-23

🔴 **THE SECTION BELOW CALLS ITSELF "THE INDEX", IS DATED 2026-08-05, AND INDEXES ROUGHLY HALF OF
THIS DOCUMENT.** It is the newest thing a reader meets and it stops at FI-39. **Thirty-four entries
are not in it** — `FI-40`…`FI-54` (fifteen, seven of them raised the same day the index was written)
and `FI-61`…`FI-73` plus `FI-76`…`FI-81` (nineteen, essentially all of the live-job work). A reader who trusts
it misses the entire second half of the backlog, including everything learned on real jobs.

⚠️ **This index states POINTERS AND A DENOMINATOR, and deliberately restates NO status.** That is
`FI-40`'s own rule — *retire the hand-restated-status drift class* — and re-copying statuses here is
precisely how the 2026-08-05 section rotted. **Each entry below carries its own authoritative status.
Go and read it.**

**Denominator: 73 entries exist**, numbered `FI-01`…`FI-54`, `FI-61`…`FI-73`, `FI-76`…`FI-81`.

🔴 **AND THE NUMBERING HAS HOLES THAT ARE NOT GAPS IN WORK — `FI-55`…`FI-60`, `FI-74` and `FI-75`
HAVE NO ENTRY IN THIS FILE AT ALL, YET ARE CITED AS SETTLED FACT ELSEWHERE.** Measured by
`git grep`: FI-55 and FI-58 in `docs/notes/compile-error-playbook.md:218`, `:271-274`; FI-56, FI-59
and FI-75 in `docs/notes/test-environment-build-plan.md:930-1034`, `:1106`, `:1143-1146`; FI-74 in
`docs/notes/live-project-readiness.md:51`, `docs/notes/openness-api-survey-plc-online.md:156` and
`docs/notes/test-environment-build-plan.md:480`, `:1669`. **Eight numbers were issued against work
that was done and written up somewhere else.** Left as a finding rather than back-filled here: writing
eight entries from other documents' summaries is doc-to-doc citation, which is the failure mode this
whole page keeps recording. **Whoever did that work owns the entries.**

**FI-40 … FI-54** — the greenfield/live-job wave, 2026-08-05 → 08-08:

| | |
|---|---|
| **FI-40** | a mechanical status check: retire the hand-restated-status drift class *(this section is that class)* |
| **FI-41** | `converter review`: TAGTABLE files pass vacuously, including C-001 and C-005 |
| **FI-42** | four converter/CLI gaps that only appear when you BUILD a data landscape |
| **FI-43** | `openness-cli`: no `delete --type`, no in-place update |
| **FI-44** | *"empty is not clean"*: the mechanical floor exited 0 having examined nothing |
| **FI-45** | the checks could not parse two shapes that are ordinary, not exotic |
| **FI-46** | three residuals from the FI-44/45 wave, found and deliberately not fixed |
| **FI-47** | ` RETAIN` on a UDT member parses clean and is silently dropped crossing to XML |
| **FI-48** | `to-xml` can reorder coils within a network, and coil order is semantic |
| **FI-49** | a member name shared across two types is an unenforced contract |
| **FI-50** | `undriven-scan` could not see a multi-instance — the house interface style |
| **FI-51** | an array subscript could only be expressed on the LAST component of a path |
| **FI-52** | the compile gate could return a false pass, and the warning was in the wrong place |
| **FI-53** | the reference graph did not credit a read taken THROUGH an array element |
| **FI-54** | the HMI capability probe programme |

**FI-61 … FI-73, FI-76 … FI-81** — Openness, converter and harness work, 2026-08-07 → 08-27:

| | |
|---|---|
| **FI-61** | a rebuilt binary is refused by Openness silently, and every message blamed the wrong thing |
| **FI-62** | `sanity-check` enumerated blocks only, so a UDT could be inconsistent behind a green gate |
| **FI-63** | the first instance DB created after a project open got number 0, and a green compile hid it |
| **FI-64** | a PLC data type carrying a named-type member could not be read back at all |
| **FI-65** | an Openness manager: multi-agent Portal access, bulk transfer, an import/export ledger |
| **FI-66** | `sanity-check` reported a count its own documented remedy could not reduce |
| **FI-67** | `cross-check` could not be asked the one question a back-out must ask |
| **FI-68** | a relative `--out` failed with an exception naming a different problem entirely |
| **FI-69** | a statement-order violation in the IR blamed the network header |
| **FI-70** | nothing compared the IR on disk against what is actually in the controller |
| **FI-71** | `to-xml` guessed a member type and only warned about it, for the third time |
| **FI-72** | both converters wrote beside their input, and silently overwrote hand-authored IR |
| **FI-73** | an unknown `--flag` was treated as a FILENAME; a stale Release build is how it surfaced |
| **FI-76** | the map model: derive the block structure from the border, build it in order-waves |
| **FI-77** | a per-block compile reported the whole program's errors, counting empty parent nodes |
| **FI-78** | `diff` can declare an insertion but not a deletion, so an add-and-remove cannot pass |
| **FI-79** | `diff` without `--only` exits 0 unconditionally — examining nothing reads as a pass |
| **FI-80** | 🔴 the Normalizer's Access key is EMPTY for a constant — two constants compare EQUAL |
| **FI-81** | `compile-all` is not convergent: one pass can leave more inconsistent than it cleared |

⚠️ **The entries are NOT in numeric order in this file** — FI-67 precedes FI-66, and FI-71/72/73
precede FI-68/69/70. Read by heading, not by position.

⚠️ **Two dead claims that survive in the superseded sections below, flagged rather than edited into
dated records.** Both 2026-07-20 sections end with a *"Parked / deferred"* line carrying **D-7 — the 6
stale `simatic-ml/test-project001` exports; needs a live-Portal re-export (owner)**. ✅ **D-7 IS
DISCHARGED** — verified by a live `drift-check` in this tree on 2026-08-23 (`0 drifted, 26 match`,
`COMPARED: 26`), all six blocks `MATCH`; see FI-26's own entry and
`tests/golden/GoldenHarness.Tests/ExportDriftDetectorTests.cs:87`. The lines below are left as written
because they are the dated record of 2026-07-20, when they were true.

---

## Implementation status — 2026-08-05

⚠️ **SUPERSEDED AS AN INDEX by "Index — coverage repair, 2026-08-23" above — it stops at FI-39 and is
missing 29 entries.** Kept unedited as the dated record of where the backlog stood on 2026-08-05.

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
  unit tests + 2 golden.
  ✅ **UPDATE 2026-08-23 — D-7 IS DISCHARGED; THIS ENTRY CLAIMED THE OPPOSITE UNTIL TODAY.** It read
  *"the 6 stale blocks are consciously deferred — `deferred-items.md` D-7 (owner, 2026-07-20)"*, which
  had been false for ten days. **All six are `MATCH`**, verified by a live run in this tree rather
  than quoted: `SUMMARY: 0 drifted, 26 match, 17 skipped, 0 export-only, 0 error, 0 pairing-failure ·
  COMPARED: 26 object(s)`. The golden guard's own baseline says so at
  `tests/golden/GoldenHarness.Tests/ExportDriftDetectorTests.cs:87` — *"D-7 IS DISCHARGED, AND WHAT
  REMAINS IS NOT D-7"*. Route: three iDBs left 2026-08-13 (`f2a548a` showed their filed reason had
  never been true), `FB_PusherControl` / `FB_ShredderSequencer` on the live re-export in `f0fb0cb`,
  and `DB_Settings` **survived its own re-export** and was re-filed rather than left under a dead
  reason. Debt paid in `4ff6d80`.
  🔴 **That zero is over 26 of 43, and the detector says so.** 17 objects have no committed export and
  are **skipped, not judged**. A wider third-leg run on 2026-08-23, with the tag tables included
  (`SKIPPED` in the two third-leg legs that preceded it — see the correction below), compared **43**
  and found **5 drifted, 38 match, 0 skipped, 2 export-only** — `DB_PLC`, `DefaultTagTable`,
  `FC_HarnessCopyLayer`, `HarnessMirror`,
  `iDB_HopperBlockageStim`. ⚠️ **The smaller earlier figures were INCOMPLETE, not wrong** — those runs
  compared exactly what they said they compared, over a smaller population, which is what `COMPARED:`
  exists to make visible.
  🔴 **CORRECTION 2026-08-23 — this entry said the tag tables were opt-in "and `SKIPPED` in every
  prior run". That is false.** `drift-check` has **no** `--tagtables` flag (it belongs to
  `export-all`), and its `SKIPPED` means only *"no paired `.xml` in the exports dir"*
  (`src/converter/Converter/DriftCheck/DriftCheckRunner.cs:50-54`). The **2026-08-14 05:10** run
  against the controller **did** have both tag tables in its dump: `docs/notes/test-log.tsv:68`
  records `4 drifted, 38 match, 3 export-only (MotorIOSet, MotorVSDIOSet, Default tag table)` —
  `HarnessMirror` was compared there and **MATCHED**, and `DefaultTagTable` was **mis-paired** by the
  space-in-name defect rather than skipped. `HarnessMirror`'s drift was created **after** that run, by
  `084b778` at 11:50:04 the same day, and has never been deployed. The genuine `SKIPPED` case is
  **exactly the two 2026-08-23 third-leg runs** (`dc8308d`), which compared 41. **The mistake was
  inferring a lesson from a flag's name without checking which command owns the flag.**
  **None of the five is a D-7 block**; they are a separate, later harness debt
  recorded in `docs/notes/deferred-items.md`'s D-7 header, and they do **not** inherit D-7's deferral
  or its owner ruling.
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
  `ValveA : "FB_Valve"` — has no DB of its own and was therefore not an instance at all. Every
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
  1. **Addressing.** A multi-instance is written bare inside its owner (`ValveA.IO.Cmd`) with no
     instance root, unlike an iDB. So lookups use the local form **restricted to the owning block** —
     without that restriction two FBs sharing a static name pool each other's writers and each masks
     the other's gap, which would have turned a blind check into a wrong one.
  2. **Nesting.** Resolution iterates to a fixpoint, because multi-instances nest. A single pass
     roots a nested placement at its declaration site and loses the per-instance resolution the
     command exists for.
- **A judgement recorded rather than buried:** where the owning FB has **no instance DB yet**, the
  placement is reported under a declaration-site path (`FB_Cell/ValveA`). That is a class,
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
  last component". So `DB_Gauge.Bay[0].RawValue` — an **array of structs**, indexed in the
  MIDDLE of the path — had nowhere to live.
- **The two halves disagreed, in the worst possible direction.** The parser **refused** a non-final
  indexed component with a clear `UnsupportedConstructException`. The writer **silently emitted**
  `<Component Name="Bay[0]" />` — a component literally *named* `Bay[0]`, which names no member
  and which TIA rejects on import. A read path that refuses and a write path that corrupts is
  strictly worse than either alone: the loud half never fires on content the quiet half produced.
- **What it cost.** On the driving job the weighing interface is
  `Bay : Array[0..3] of UDT_GaugeBay`, so the input map could not be written. **No vessel had a
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
- 🔴 **THE MECHANISM RECORDED ABOVE WAS WRONG, AND IT IS CORRECTED HERE RATHER THAN REWRITTEN —
  2026-08-13.** *"Device-level `Compile()` reports success without ever clearing it"* reads as a TIA
  quirk. ***IT NEVER CLEARED THE FLAG BECAUSE IT NEVER LOOKED AT THE PROGRAM.*** Openness exposes
  **three** PLC compilers — station (`Device`: hardware **and** program blocks), device item
  (`DeviceItem`: **hardware only**), software (`PlcSoftware`: program blocks only) — and
  `openness-cli compile` reached the **device-item** one, so its message tree was
  `Hardware configuration` and nothing else. ***THAT IS PRECISELY WHY IT REPORTED `Success, errors=0`
  OVER NINETEEN UNCOMPILED BLOCKS: the blocks were not examined and not claimed to be — they were
  ABSENT from the output, which nobody read as absence.*** Measured verbatim on the same project
  minutes apart: the old `compile` printed only *"Hardware was not compiled"* while
  `compile --software` printed *"FC8: Block was successfully compiled."*
  **The finding stands, the fix stands, the severity stands — only the explanation was wrong**, and
  the wrong explanation made this look like an unfixable vendor behaviour rather than a scope bug we
  owned. `compile` now defaults to **station**; the old scope survives by name as `--hardware`.
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
- **The shape.** An `Array[0..3] of "UDT_X"` member is inventoried as ONE leaf (`DB_TuningRet.Bay`),
  because the signal walk does not expand a UDT sitting behind an array. Every real reference,
  though, goes through an element AND a member — `DB_TuningRet.Bay[0].ZeroOffset`. `cross-check`
  looked the declared path up by exact string, found nothing, and reported the member
  **`unused (no writer, no reader)`**.
- **Measured, not theorised.** On the live corpus `DB_TuningRet.Bay` reported dead against **24**
  real readers and `DB_GaugeInterface.Bay` against **16**, while plain scalar siblings *in the same
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

**Verdict / revisit trigger:** Open, but **its Unified half is spent** — the remaining Unified questions are narrow follow-ups (why `set_Text` refuses; whether `RaisedStateTagBitNumber` is contextual; whether the three refused dynamization kinds have another route), not a programme. ~~**The decision now sits with ADR-0007, which has the measurement it was waiting for.** Revisit if ADR-0007 is rejected (the write commands then need a disposition) or if a classic job arrives and unblocks phase 4.~~
✅ **UPDATED 2026-08-23 — ADR-0007 WAS ACCEPTED 2026-08-17** (`docs/adr/adr-0007-hmi-engineering-scope.md:3-5`); HMI engineering left `docs/10-non-goals.md:21-23` the same day. **The rejection branch above is dead** and the write commands need no disposition on that account. **Phase 4 (classic) is not merely unblocked — it has been overtaken:** the accepted programme *targets* Classic Basic, and its keystone read came back positive on 2026-08-17 — a Classic Basic screen exports as SimaticML with full content, 224 KB, root `<Hmi.Screen.Screen>`, layers and groups present in the file (`hmi/wave-1-results.md:21-52`). Three waves have run; the record is `hmi/PLAN.md` and `hmi/wave-1-results.md` … `wave-3-results.md`, the tooling is `src/hmi-cli`, the authoring agent is `hmi-designer`. ➜ **This entry's remaining Unified follow-ups are a different API from the programme** — Classic and Unified share no types — so they stay parked here rather than being read as programme work.
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
  `export --type UDT_Rack` with *"Inconsistent blocks and PLC data types (UDT) cannot be exported."*
  The agent checked the source rather than guessing: `ExportType` has no consistency guard of its
  own, so the refusal came from TIA.
- **Why the compile did not catch it either.** Nothing in that corpus instantiated the type. An
  uninstantiated UDT has nothing to make a device compile fail, so both halves of the gate were
  green simultaneously while the type was inconsistent.
- 🔴 **THAT EXPLANATION IS ALSO FALSIFIED BY THE 2026-08-13 SCOPE FINDING, AND NOBODY HAD NOTICED —
  IT IS RECORDED HERE BECAUSE IT IS THE SAME ERROR AS FI-52's, MADE INDEPENDENTLY.** The
  uninstantiated-type argument is plausible and it was not the reason. ***THE DEVICE COMPILE IN THAT
  RUN WAS HARDWARE-ONLY, SO IT WOULD NOT HAVE CAUGHT THE TYPE HOWEVER MANY BLOCKS INSTANTIATED IT.***
  The `Success (errors=0, warnings=0)` quoted above is a **hardware** verdict that was read as a
  program verdict.
  **Two things worth keeping from this.** First, FI-62's *finding* is untouched — `sanity-check`
  really did enumerate blocks only, and fixing that was right. Second, and more useful: ***TWICE NOW,
  A PLAUSIBLE MECHANISM WAS INVENTED TO EXPLAIN A GREEN THAT WAS ACTUALLY GREEN BECAUSE NOTHING HAD
  BEEN EXAMINED.*** That is the *empty is not clean* failure appearing in the **explanations** rather
  than in the checks — and an explanation nobody can falsify is how a check keeps its blind spot.
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
### FI-63 — the first instance DB created after a project open got number 0, and a green compile hid it

- **Deterministic, measured three times on a live job.** `create-instance-db` gave the **first** DB
  created after a project open the number **0**, which is not a valid block number; later creations in
  the same session numbered correctly.
- **FI-52's family again, which is what makes it serious rather than untidy.** A **whole-device compile
  reports `Success` over an invalid-numbered block**; only the per-block compile says
  `has an invalid number 0`. So the default gate passes and the defect ships. The workaround that
  unblocked the job was to create a throwaway DB, then the real one, then delete the throwaway.
- **Fixed in two independent halves, deliberately.** *Part 1, the hypothesis:* what a throwaway does
  incidentally is force the block composition to be enumerated, so the composition is now enumerated
  before the create — if the auto-numberer needs the existing numbers materialised first, that is the
  whole fix and the throwaway was that fix by accident. *Part 2, which does not depend on part 1 being
  right:* read the number back, repair it with the lowest free number, and throw if the repair does not
  take. Part 1 is a guess about somebody else's allocator; part 2 holds either way.
- **Lowest-free rather than highest-plus-one**, because this runs only to repair an invalid result and
  should slot into a gap the project already has instead of growing the numbering space each time.
- **Verdict.** Built, 244 openness-cli tests (+5) covering the number choice. **Not live-verified** —
  the gateway half needs a Portal, and a rebuild revokes the binary's Openness approval until a person
  re-approves it (FI-61). The hypothesis in part 1 is explicitly unconfirmed.

### FI-64 — a PLC data type carrying a named-type member could not be read back at all

- **The same expansion FI-56 fixed, on the third parse path.** TIA expands a member whose type is a
  named UDT — including an array of one — into a nested `<Sections>` on re-export. FI-56 taught
  `ParseBareMember` to collapse that back to the type reference the IR already names.
  **`ParseTypeMember` was left refusing it outright**, so a PLC data type carrying such a member
  hard-errored on `to-ir`: *"member 'Claim' has nested structured content (`<Sections>`)"*.
- **Why it mattered more than a parse error.** The re-export round trip is the only check on this
  project that has caught defects **every other gate passed** — three separate times on one job: a
  stale `.xml` imported with every gate green; a UDT member present in the type but missing from a
  block's inline interface expansion, preflight clean over it; and a comment-only edit left out of a
  `to-xml` list. **A type the converter cannot read back loses that check entirely**, degrading to
  reading raw XML by hand — which is exactly what an agent had to do to prove one member existed.
- **Fixed as the rule, not a third special case.** The standing preference on this project is one
  general fix over stacked special cases, and FI-56 had already established the rule: an expansion of
  a **named** type is redundant because the IR names the type. Applied to `ParseTypeMember` rather
  than re-derived.
- **The distinction that had to hold.** An **anonymous** structured member's `<Sections>` carries its
  only definition, and collapsing it would silently discard real members — strictly worse than
  refusing to parse. Anonymous nesting arrives on this path as direct `<Member>` children with
  Datatype `Struct`, so refusing `<Sections>` for anything that is not a named-type reference leaves
  that case exactly as it was.
- **One existing test had to be repointed, and that is worth recording.**
  `Parse_MemberWithNestedSections_HardErrors` asserted the old behaviour using
  `Datatype="SomeOtherType"` — a *named* reference. It encoded the rule being fixed rather than
  catching a mistake in the fix, so it now guards the anonymous-`Struct` half that must still refuse.
- **Verified against the file that caused it**, not just in unit tests: `to-ir` on the real
  `UDT_SlotQueue` export now exits 0, collapses `Claim : Array[1..8] of "UDT_SlotTicket"` to
  its reference, and reads back all nine members including the one previously provable only by hand.
- **Verdict.** Built and verified. 865 converter tests (+6), 39 golden.

### FI-65 — an Openness manager: multi-agent Portal access, bulk transfer, and an import/export ledger
- **Status:** **Implemented (partial) 2026-08-07 — component 1 (claims) shipped; components 2–5
  (workspaces, lease, integration, ledger) open.** `converter claim` / `converter claims`
  (`src/converter/Converter/Claims/`, `src/converter/README.md`): six kinds across the two semantics,
  corpus-validated against `ProjectIndex` (extended additively with `(Kind, Number)` and network slots),
  `SignalInventory` and `ProjectUsageGraph`; `--claims` required with no default; 46 tests, 822 total.
  **Two things changed in the build vs the design below.** (1) `--suggest` was **dropped for
  `--allocate`**, which takes the lowest free value atomically — a non-binding suggestion is the exact
  race the tool exists to remove. (2) Acquisition writes to a temp file and `File.Move`s it into the
  slot rather than opening the slot with `CreateNew`: the parallel-acquisition test caught the winner
  holding its new file open while writing, so a loser could not read who had beaten it. That defect was
  found by the test, not by review — the argument for having written it.
  Scope held deliberately: no Portal, no `openness-cli`, no `.ir` touched. **Adoption is not part of
  it** — nothing yet requires an agent to claim before writing, and wiring claims into the coding
  skills is what turns the registry from available into binding.
  **Build and test record: `docs/evidence/fi-50-claims-build.md`** — what changed, the unit /
  cross-process / real-corpus / regression evidence, the **standing testing requirements** for future
  changes (§4), and the explicit not-tested list (§5: non-local filesystems, multi-machine, clock
  skew, crash-mid-acquire, adoption).
- **Prior status:** Under debate (2026-08-07) — raised by the owner as one idea; the analysis below argues it
  is **four** ideas with four different verdicts, and that the headline framing ("multi-agent access",
  "streamline", "bulk") names benefits the substrate cannot deliver *as stated*. Owner clarified the same
  day that the target is specifically **multiple agents on the same project / TIA file**; §FI-65a is the
  decomposition that makes that reachable and §FI-65b the resulting build. Nothing here is accepted.
- **Raised:** 2026-08-07 · **Source:** conversation — "a multi-agent access idea for TIA … streamline AI
  access … bulk imports and exports … maybe Openness won't support it, but maybe an Openness manager that
  can queue the agent access and keeps a log of exported and imported blocks."

**The seed, restated.** A process (daemon, broker, or serialising CLI layer) sits between N agents and
TIA Portal: it queues their access so they cannot collide, offers bulk import/export instead of
one-object-per-invocation, and maintains a ledger of what was imported and exported.

**The model in one paragraph** (agreed with the owner 2026-08-07 after a readback; read this before the
objections, which are the reasoning that produced it). *TIA is single-access. Agents work on IR in their
own workspaces. Before an agent commits to a shared resource — block number, alarm bit, network slot, DB
member — it takes a **claim** in the repo, so a second agent is refused rather than colliding. For round
trips it uses its own copy of the project, in parallel with everyone else. A serialized layer guards only
the **canonical** project, at integration, where the work merges and one authoritative compile runs. The
log is for attribution after the fact — useful, but it is not what keeps agents apart.* One line:
**claims keep agents off each other; the queue only protects the canonical project; the log just tells
you who did what.**

**Objection 1 — a queue does not buy multi-agent access to one project; it buys multi-agent waiting.**
The single-writer constraint is **TIA's, not ours**: T3.1 in `docs/notes/concurrent-portal-test-plan.md`
confirmed live that a second open of the same project is refused by Portal itself. Throughput against one
project is therefore exactly 1 no matter what is built in front of it. A queue over a single-writer
resource is a mutex with a log — real value (ordering, no interleaved failures, observability), but not
the value the word "multi-agent access" implies. Any entry that keeps the original framing will
oversell itself to its own future reader.

**Objection 2 — the concurrency that *is* available already exists, and the cheap design is copies, not
a queue.** Concurrent sessions on *different* projects are proven safe and reliable: T2.1–T2.3 (human on
one project, CLI on another, unsaved edits intact) and T4.3 (5/5 fresh concurrent launches, no hangs).
So the design that actually yields parallel agents is **one scratch project copy per agent** — a TIA
project is a copyable directory, and agents already work in file-disjoint worktrees
(`feedback_value_leverage_parallel_tracks`). The queue is the design you need only if you have decided
not to copy the project. The hard part of the copy design is not tooling, it is **merge-back**: which
agent's diff enters the real project, in what order, reviewed by whom — an engineering-review question
(hard rule 5), not a scheduler.

**Objection 3 — this optimises a cost the project's own telemetry says is not the cost.** Committed
`gen/*/telemetry.log` rows run **10–75 minutes per stage**, dominated by AI reasoning and by *blocking
questions*. A cold project open is 20–30s; a warm attach to an already-open instance is ~1s (T0.2 —
`openness-cli` never closes what it did not open, so the expensive open is already amortised across
invocations by process reuse). Bulk-exporting 40 blocks therefore does not save 40 project opens; it
saves ~39 attaches. **FI-12 is already parked on exactly this gate** — "(b) should not be built on an
assumed bottleneck", pending FI-16 measurement — and FI-16's format is stage-grained, so it will never
produce the per-call latency FI-12 is waiting for. Two consequences: this idea inherits FI-12's gate
rather than escaping it, and *someone should fix the measurement* (a `--timing` line on `openness-cli`)
before anyone argues from assumed Portal cost again.

**Objection 4 — the recorded pain is reliability and hygiene, not throughput.** The one stage in the
telemetry that Portal actually *blocked* failed like this: "COMPILE GATE BLOCKED: Portal connect timed
out 3min, 2 stale Portal processes present (first-connect approval dialog needs human)". Not contention
— **cruft plus a human-gated dialog**. T4.3's conclusion says the same thing: connection reliability
degrades with process pileup, not with concurrency. So the highest-value component of the seed is the
one it barely mentions — a broker that **refuses to start in a dirty environment**, owns the
launch/reuse decision, and reports `held by <agent> for <n>s` or `needs human: approval dialog` instead
of hanging for three minutes. FI-28 (`portal-status`) is its read-only half, built; FI-07 (janitor) is
its parked write half. This idea is largely **FI-07 + FI-28 + a lease**, and should be read as their
continuation rather than as a new thing.

**Objection 5 — a daemon is the single most dangerous component available here.** The project's
documented failure mode is stale Portal processes; a long-lived daemon is a stale-process factory with a
heartbeat (FI-12(b) records this). Worse, it multiplies a live-found hazard: **any mutating command
calls `Project.Save()`, which persists everything dirty in that project** — the T4.1 target had to be
changed mid-audit because a compile would have force-saved a human's unsaved edit. Today's
per-invocation, short-lived isolation is a *safety property*, not merely an inefficiency: agent B cannot
persist agent A's half-finished work because agent B's process does not share A's session. A shared
long-lived session removes that. Any daemon design must state what happens to a dirty project when the
holder dies.

**Objection 6 — bulk mutation weakens the compile gate and has no clean undo.** Hard rule 4 wants compile
evidence *for the thing that changed*; a 12-block import plus one device compile yields a diagnostic pile
that then has to be attributed, which is the attribution problem the per-block loop exists to avoid.
Import is also **order-dependent** (UDT-first, or `Data type "<name>" is unknown` — live-verified
2026-07-14), so a bulk import is a topologically-ordered operation, not a `for` loop. And rollback is
missing: FI-43 records there is no `delete --type` and no in-place update, so a bad bulk type import is
not cleanly undoable. **Export is the safe half** — read-only, no ordering, no attribution problem — and
should be separated from import rather than shipped as one "bulk" feature.

**Objection 7 — the ledger is the weakest element, and a better answer already exists.** This project has
repeatedly recorded that derived and hand-maintained records rot (ADR-0005; the `TimerSample` stale
sidecar; FI-15's staleness warning; FI-40's "hand-restated status drift class"). A log of what was
imported and exported is a *claim about history* that nothing verifies. `converter drift-check` (FI-26)
already answers the question the ledger is a proxy for — whether `ir/` and `simatic-ml/` actually agree —
by Normalizer comparison rather than by testimony. **Prefer derived truth to recorded history.** The
residue a ledger could legitimately own is what drift-check cannot derive: which agent did what, when,
with what exit code — an attribution trail, not a source of truth about content. It must never become an
input a stage trusts instead of looking.

**Objection 8 — a repo-level ledger is a live-run retention leak.** `docs/13-data-boundary.md`'s live-run
rule is **retention, not access**: nothing from a job folder is ever committed, and block/equipment names
are exactly what a ledger records. A manager-owned log directory in the repo will be committed by
someone. Non-negotiable if a ledger is ever built: per-project, gitignored by default, and a live-run
job's ledger lives inside that job folder — never a global one.

**Objection 9 — non-goal adjacency.** `10-non-goals.md` lists "Multi-user / team deployment — single
engineering PC first" as a *not-now, ADR-only* item. N agents on one PC is not multi-user, but a manager
daemon with a queue, leases, and a persistent log is the first sixty percent of one. The entry must say
single-PC, single-human, N-agents explicitly, or the not-now drifts by construction rather than by
decision.

**Objection 10 — the ceiling is RAM, and it is about 3×.** One logical Portal instance is ~2 processes
and roughly 1.5 GB (measured in the audit: +497 MB on a project open; 1.2 → 1.58 GB on another). Three or
four concurrent instances is the practical ceiling on an engineering PC. The maximum benefit of *any*
multi-agent Portal design is therefore ~3×, and only for agents on different projects — worth sizing the
ambition to before choosing an architecture.

**Objection 11 — the human cannot be queued, and is the likeliest lock-holder.** An engineer can open the
project in Portal by hand at any moment (and does). A queue that does not include them is *advisory*,
and an advisory lock over a resource another actor can take is a lie that eventually gets believed. The
manager must therefore **detect** reality (portal-status, exact-project match) rather than schedule it.

**Unanswered design questions** — these decide whether this is small or huge, and none has an obvious
default: is the lock per-project or per-Portal-process? What is the unit of queueing — a command, or a
*transaction* (import→compile→export must not lose the lock between steps, which implies a session
concept, which is FI-12(b) again)? What happens when a lease-holder's agent dies (lease + TTL, or a
project locked forever)? Does the queue survive reboot? Is a queued agent's wait bounded, and what does
it do on timeout?

**Also worth checking before building anything:** whether two `lad-coder` agents have *ever* actually
needed Portal at the same time, or whether today's dispatch pattern already serialises them by
construction. If it is hypothetical, this is FI-12's mistake at a larger scale.

**Merits (what survives the above).** A per-project **advisory lease with an environment precondition**
is genuinely valuable and small: it converts the recorded failure (3-minute hang, dirty process list,
human-gated dialog) into a fast, legible refusal, and it is the missing piece that makes parallel agent
tracks safe to attempt at all. Bulk **export** is cheap and independently useful. Per-agent scratch
copies are the only route to real parallelism, and rest on an already-proven capability.

#### FI-65a — the same-project decomposition (owner clarification, 2026-08-07)

The owner's target is explicit: **multiple agents working on the same project / the same TIA file.**
Objections 1–2 above stand as written about the `.ap20`, but they answer the wrong question if read as
"so it cannot be done". The resolution is a decomposition, and it is the load-bearing idea of this entry.

**The `.ap20` is not the source of truth — `ir/<project>/` is.** Hard rule 7 already says so (edit only
IR); the TIA project is a *compile target and export source*, not the master copy. So "the same project"
splits into three resources with three different concurrency properties:

| Resource | Concurrency | Today |
|---|---|---|
| `ir/<project>/`, `gen/<project>/`, `patterns/` — the actual project content | **Multi-writer capable**; agents already work in file-disjoint git worktrees | uncoordinated |
| The `.ap20` an agent compiles against | **Single-writer, but replicable** — 5.6 MB (`GenProject1`, measured) | one shared scratch project |
| The **canonical** project the engineer reviews into | **Single-writer, not replicable** | same shared project |

So agents *can* work the same project concurrently: each takes a private disposable clone of the
`.ap20`, works the shared IR under coordination, and converges through one serialized integration. That
is git's own model — distributed copies, serialized merge — and it is the only shape the substrate
permits. What cannot exist is two concurrent writers on one `.ap20`; nothing in this build pretends
otherwise.

**The hazards that actually break same-project multi-agent work are repo-side, and no Portal queue can
see any of them.** Grounded in this repo:

1. **Block numbers.** `NUMBER 50` is a line in the IR — numbers are author-allocated. Two agents each
   scan `ir/` for the next free FB, both pick 51. Import matches by *name*, so this does not
   necessarily fail loudly; it can land as two blocks claiming one number.
2. **Alarm bits.** The committed telemetry records `ShredderAlarm0.%X9 <- HopperBlockedAlarm, X9 verified
   free`. Two agents verifying "X9 free" concurrently both take it. Nothing detects it until someone
   reads the alarm list — a wrong-alarm-text defect that reaches site.
3. **Append slots in shared blocks.** The same run appended `FC_ControlMain` NW8+NW9. Two agents both
   append "NW8". Each passes its own `converter diff --only` invariance check; the conflict exists only
   between them.
4. **Shared DB members** (`DB_Settings`/`DB_Controls`) and **proposed tags/addresses** — same class.
5. **`Project.Save()`** — only if agents ever share one open project, which this build removes.

**Five of those six are logical conflicts in repo state, invisible to any lock on the Portal side.** That
inverts the seed's emphasis: the manager's core is a **claims registry over shared logical resources**;
the Portal lease is a small supporting part. It is also the same philosophy as FI-39/FI-44 — a mechanical
floor that survives an agent choosing not to look — applied to coordination instead of review.

#### FI-65b — the proposed build (five components, no daemon)

Everything below is files plus short-lived processes. Nothing long-lived, so FI-12(b)'s stale-process
risk is not incurred.

**1. Claims registry — `converter claim` / `converter claims`.** The component that actually delivers
same-project multi-agent work; build first, and it is testable entirely offline.
- Storage: **one file per claim** under `gen/<project>/claims/`, not one table — a directory of disjoint
  files is conflict-free under concurrent writers by construction, where a shared table is a merge
  conflict per claim.
- Claimable kinds: `block-number` (FB/FC/DB), `alarm-bit` (`<word>.%Xn`), `db-member`, `block-network`
  (an append slot in a shared block), `tag`, and `block-edit` (exclusive write on an existing block).
- `claim --suggest --kind block-number --type FB` must allocate against **`ir/` *and* outstanding
  claims** — allocating against the corpus alone is exactly the race in hazard 1.
- `claims --check` verifies every claim still holds and no *unclaimed* conflict exists. Per FI-44:
  zero claims examined must **not** exit 0 vacuously.
- Placement is an open call: it is pure repo-file transformation, so `converter` fits the
  no-external-process invariant — but coordination state is arguably neither converter nor CLI. Decide
  before building, not during.

**2. Per-agent workspaces — `openness-cli workspace create|list|sync|destroy`.** A filesystem clone of
the project directory per agent (5.6 MB — disk cost is a rounding error), registered and gitignored.
Every agent's import/compile targets its own clone. Openness exposes no `SaveAs` in the recorded API
surface, so this is a directory copy of a closed project, not an API call.

**3. Canonical lease — `openness-cli lease`.** With clones in place the lease covers only the canonical
project, so contention approaches zero. Holder + PID + TTL, acquired per *transaction*, `--wait`,
TTL-reclaim for dead holders, and a `portal-status` precondition. It must **detect** a human holding the
project rather than pretend to schedule them (objection 11).

**4. Integration — the serialized merge.** The step that makes clones safe, composed almost entirely of
tools that already exist: acquire lease → `claims --check` → `converter diff` per changed block (proves
each agent touched only what it claimed) → **`cross-check` + `preflight` on the union** → ordered import
(UDT-first) → compile canonical once → export → `drift-check` → release. Step four is the one that earns
the whole design: two blocks that each compiled clean in isolation can still conflict (C-308 multi-writer,
dead wiring, IO boundary), and only a union check sees it.

**5. Status and ledger.** `manager status` (leases, workspaces, outstanding claims, stale agents) plus an
attribution-only ledger — agent, time, exit code — **per-project and gitignored** (objection 8).
`drift-check` remains the authority on content; the ledger never becomes an input a stage trusts.

**Ranked build order:** 1 (claims) → 2 (workspaces) → 3 (lease) → 4 (integration) → 5 (status/ledger).
Bulk export stays worth doing independently; bulk *import* and any queue-proper stay behind FI-12's
measurement gate and FI-43's missing undo.

**Benefits, stated precisely.**
- **Real parallel compile on one project** — not a faster queue: N agents compile simultaneously because
  they are not sharing a writer. Ceiling ~3 concurrent, set by RAM (objection 10), not by the design.
- **Failure isolation, probably the largest practical win and not a speed argument at all.** Today one
  agent leaving the shared scratch project inconsistent (`IsConsistent` cascades; FI-42's 14 stranded
  superseded types) breaks every other agent's compile gate. A poisoned 5.6 MB clone is deleted, not
  debugged.
- **The `Save()` cross-contamination hazard is removed** for agents outright — no shared session.
- **Silent logical collisions become loud, and become loud *before* the work** — a refused claim costs
  seconds; the same collision found at merge costs a rebuilt block, and found on site costs a callout.
- **A union compile gate that matches what hard rule 4 actually wants** before the engineer sees it.
- **Legible failure** — the recorded 3-minute hang becomes `needs human: approval dialog, PID <n>`.
- **Attribution** — when a review finds a defect in a multi-agent build, the ledger says which agent
  produced which block under which claims. Currently unreconstructable.
- **It makes the existing parallel-track workflow mechanically safe.** File-disjointness is enforced
  today by the coordinator's judgment; claims make it a check.

**Costs this build genuinely carries.**
- Integration becomes a new serialized bottleneck, and the hard work migrates into it. If integration
  costs 30 minutes, three agents saving 20 minutes each have bought nothing.
- Claims cover only the kinds someone enumerated; an unclaimed resource class is an undetected collision.
- **Clone staleness, and a hard-rule-4 question that needs the owner's ruling, not an assumption:** an
  agent's clone diverges from canonical while it works, so its per-agent compile is evidence against an
  older project. The coherent reading is that the per-agent compile is a *filter* (like `preflight`) and
  the **integration compile is the gate** — but hard rule 4 is written per-change, and re-reading it this
  way is a rule interpretation the owner must sign off before anything is built on it.
- Someone must run integration; if that is the orchestrator, the orchestrator is the serialization point.

#### FI-65c — three corrections that the design turns on, and one live alternative

Recorded because each is a reading a future reader will arrive at independently, and two of them are the
seed's own natural shape. From the owner's readback, 2026-08-07.

**Correction 1 — a log cannot stop agents stepping on each other; only a claim can.** A log records what
already happened; preventing a collision needs a reservation taken *before* the work. "Agent B, FB51 is
taken, use 52" has to be answerable at the moment B is about to choose, not reconstructable afterwards.
Same information, opposite direction in time, and only one direction is useful for the stated problem: a
log tells you who broke it, a claim stops it breaking. This is why FI-65b leads with the claims registry
and demotes the ledger to attribution — it is not a preference about tooling, it is the difference
between prevention and forensics.

**Correction 2 — the collisions happen upstream of the layer, so a layer in front of TIA is structurally
in the wrong place to prevent them.** All four real clash sources — same block number (`NUMBER` is a line
in the IR), same alarm bit, same append slot in a shared block, same DB member — are decided *while
writing IR*, potentially hours before any round trip, and **none of them ever passes through the TIA
boundary**. A queue at the Portal door never observes them, however well built. Coordination has to live
where the work happens: in the repo, over IR. This is the single most load-bearing correction in the
entry — it is what moves the manager's centre of gravity off the Portal side entirely.

**Correction 3 — round trips need not queue, and the alternative is a real choice, not an error.** The
seed's natural shape is *one shared TIA project behind a queue*: every agent's round trip is a request
the layer services in turn. It is simpler than FI-65b, needs one Portal instance rather than N, and
throughput is tolerable because a warm attach is ~1s and a block compile ~2s. **What it trades away is
failure isolation.** One agent leaving the shared project inconsistent (`IsConsistent` cascades;
FI-42's stranded superseded types — both recorded here, not hypothetical) breaks the compile gate for
*every* agent, and someone has to debug it. With per-agent copies that agent deletes 5.6 MB and
re-clones. Against that, each concurrent copy costs a Portal instance (~1.5 GB), so **copies win clearly
below ~3 concurrent agents and are unavailable above it** — at which point the shared-queue model is the
only design left. Both should stay on the table; the agent count decides, and that number is not known
yet.

**Two smaller ones.** (a) What returns from a round trip is **compile diagnostics plus the re-exported
IR** — not diffs. `converter diff` is offline and the agent can run it itself; asking the layer for diffs
puts work at the serialization point that does not need to be there. (b) **"Save" is not a request an
agent makes** — every mutating command already saves implicitly (`Project.Save()`, objection 5). So it
cannot be queued as a distinct operation, and its implicitness is itself an argument against a shared
project: one agent's compile force-persists another agent's half-finished work.

**Dependencies:** FI-28 (built, read-only half), FI-07 (parked write half — this idea is its natural
reopening trigger), FI-12 (shares its measurement gate), FI-43 (no undo for bulk import), FI-26
(supersedes the ledger's content question), `docs/13-data-boundary.md` (ledger retention).
**Verdict / revisit trigger:** ✅ **RULED 2026-08-23 — both open questions answered, and two components
are already built.**

- **Hard rule 4 reads PER-INTEGRATION (reading (b)).** The integration/union compile is **the gate**; a
  per-agent compile is a **filter**, like `preflight`. The reasoning the owner accepted: a clone compile
  examines something real — the clone — that is not the thing it claims, which is exactly the **closed
  check** class CLAUDE.md names. Calling it the gate would license a green obtained over the wrong
  project state. **The cost is explicit: nothing is "done" until integration runs**, which makes
  integration the serialization point, and if integration costs 30 minutes then three agents saving 20
  each have bought nothing.
- **SHARED QUEUE, not per-agent copies — for now**, with extra Portal sessions added later if contention
  actually appears. **So component 2 (per-agent workspaces) is PARKED, not pending.**
- ⚠️ **Two consequences worth writing down before anyone re-opens this.** (1) Under a shared queue there
  is only one project, so a compile is already against canonical and reading (b) is *cheaper* to satisfy
  than it was under clones — its motivation shifts from clone-staleness to the thing that survives either
  model: **two blocks can each compile clean and still conflict, and only a union check sees it.** (2)
  The queue ruling **removes the stated reason `--wait` on the lease was deferred** ("you cannot queue
  behind a person, and nothing contends on the rig"). A queue in which nobody can wait is not a queue. It
  is still not *needed* — nothing contends yet — but it is no longer deferred on principle.

🔴 **AND THIS ENTRY IS PARTLY STALE ABOUT ITS OWN BUILD.** Component 1 (claims) is **built**. Component 3
is **built and raced live** — as **`converter lease`, not `openness-cli lease`** as proposed above, which
also silently settled the "placement is an open call" question in component 1. Component 5's scope is
half-answered by `lease status` + `claims --check`. So the ranked build order 1→2→3→4→5 has already run
1→3, and what remains under debate is component 4 alone.

⚠️ **A third model exists that this entry does not have**, and it has run green twice: `harness-batch` is
neither N agents with N Portals nor N agents queueing at one Portal, but **N lanes merged into ONE
transaction held by one holder** — two lanes off one download for ~18 s of marginal cost. For anything
ending at the rig that sidesteps the copies-vs-queue question entirely. It does not cover general block
authoring, but it does mean the contention this entry was designed against may not be the binding one. Note that component 1 is the same either way — claims are required
under both models, because Correction 2 holds regardless of how round trips are served. Components 1–3
are buildable now and are justified by recorded evidence
(author-allocated `NUMBER`, the `%X9` alarm-bit claim, the stale-process compile block) rather than by an
assumed bottleneck. Bulk *import*, a queue proper and any daemon stay shut until either FI-16 gains
per-call timing or a real run records agents actually contending for the canonical project.

### FI-67 — `cross-check` could not be asked the one question a back-out must ask

- **The gap was structural, not an oversight of scope.** `multiWriters` lists, *by construction*, only
  paths written by **more than one** site. The sole-writer set is precisely its complement and was
  never emitted — so the writer graph the tool already builds could not answer *"which members lose
  their only writer if I delete this feature?"*
- **Why that matters.** Deleting the sole writer of a **retentive** member leaves it frozen at its
  last value with nothing able to clear it. On a live job that included a resource reservation whose
  surviving reader gates every grant: removing the writer with the bit standing would have made a
  shared machine **ungrantable permanently**, curable only by an online write.
- **The finding that forced it.** Three successive review passes each hand-added *one more* residue to
  a back-out list — three, then five, then six. When the set was finally **derived** instead of
  listed, it came out at 20 members in four consequence classes, and it contained **a whole class of
  four that every hand pass had missed**. Same lesson as the hold anchors that took five attempts:
  *an enumerated list is one the next item is not on.*
- **Built.** `SoleWriterFact(Path, Writer, Readers)`, computed off the same graph as its complement.
  **Readers are carried deliberately** — a member whose readers all disappear with the feature is
  inert; one with a surviving reader is live. Both answers come from one graph, and answering only
  the first is what left the earlier passes re-deriving the second by hand.
- **JSON only, on purpose.** Most members have exactly one writer, so this is the largest table in the
  report; printing it in the human view would drown the four fact tables a reader actually scans. It
  exists to be queried.
- **Retention is deliberately not filtered.** This layer does not model it, and guessing would be
  worse than leaving the caller to intersect the set with the declarations. Facts, not verdicts — the
  same contract as every other table here.
- **Verdict.** Built. 868 converter tests (+3).

### FI-66 — `sanity-check` reported a count its own documented remedy could not reduce

- **Re-reading is not a fixpoint, and the output implied it was.** The working assumption on a live job
  was *"run `sanity-check` again and the count drops to 0"*. That is true for a block the **previous
  pass compiled**, and false for one that **nothing** has compiled. Measured: a block sat
  `INCONSISTENT` across **three consecutive reads** while never appearing in any import list.
- **Why an agent cannot escape it unaided.** The device compile does not clear these — that is FI-52's
  whole finding — and the block is not in the import list, so nothing prompts a per-block compile.
  Following the documented loop, you re-read for ever, with no indication of why.
- **Fixed by naming the remedy where the count is**, exactly as the FI-62 types list already does:
  the block list now states that a device compile will not clear them, that re-running will not
  either, and gives the `--block` command that does.
- **Same family as FI-52 and FI-62, and the fourth instance:** a gate whose output implies an action
  that does not work. FI-52 was a compile reporting success over unverified blocks; FI-62 was a scope
  silently excluding types; this is a remedy that does not remedy.
- **Related, not fixed, recorded for whoever picks it up:** the *phantom* first-pass compile error —
  `"Block could not be compiled"` with no reason, clean on an identical second pass with no file
  changed — has now been seen **eight** times, always on a block whose dependency compiled in the same
  round. One agent avoided it entirely by compiling the UDT **before** any block, so nothing compiled
  against a still-flagged type; that ordering is worth testing as the general remedy.
- **Verdict.** Built, 221 openness-cli tests (+1). Not yet live-verified — that needs another
  owner-approved rebuild (FI-61).

### FI-71 — `to-xml` guessed a member type and only warned about it, for the third time

- **FI-57's remedy did not remedy.** Converting a block without `--project` cannot type a comparison
  against another DB's member, so it falls back to a type inferred from the literal — which TIA rejects
  when the real member is unsigned. FI-57 answered it with a warning on stderr and recorded that it had
  "cost two separately". It has now cost a **third** import-and-compile cycle, and the third time **the
  untyped file was the one about to be imported**. A warning competes with the tool's own success line
  on the same stream, and loses.
- **`to-xml` now fails closed, and only `to-xml`.** That is the direction whose output goes into a
  controller, where a guessed member type is a defect waiting on a compile to find it. `to-ir` reads an
  export and cannot produce anything a PLC will execute, so it stays advisory. `--allow-blind-types`
  is the escape hatch for a block that genuinely references roots outside the project.
- **Same family as FI-52/FI-62/FI-66**, with a twist worth naming: there the gate did not look. Here it
  looked, saw the problem, and *asked nicely*. A check whose only consequence is a line of text is a
  check that gets skimmed past.
- **Verdict.** Built, 888 converter tests (+7), and verified end-to-end on a real 89-network block: the
  refusal fires, **no `.xml` is written**, exit 1; with `--project` the same file converts clean.

### FI-72 — both converters wrote beside their input, and silently overwrote hand-authored IR

- **Two agents, independently, in one day.** `converter to-ir X.xml` writes `X.ir` beside its input.
  One agent lost **eight hand-edited `.ir` files** that way (recovered only because `ir-hash` could
  prove the readable content identical); another lost its own review snapshot *mid-review*, which is
  the worse case — the file it was measuring against moved under it.
- **Not fixed by refusing to overwrite.** Overwriting is the normal case and the correct one in the
  export-and-read-back loop, so a refusal would break every routine run and be switched off within a
  day. Fixed by giving the caller **somewhere else to put the output** (`--out <dir>` — what both
  agents actually needed and neither had) and by making the overwrite **visible at the moment it
  happens** rather than silent.
- **Applied to all four write paths**, not just the block one: DB, UDT and tag-table conversions have
  their own write sites, and fixing only the path that was reported would have been exactly the partial
  fix this repo keeps re-learning about.
- **Verdict.** Built, tests as above, verified end-to-end (`--out` redirects and creates the directory;
  a second run reports the overwrite). Default behaviour is unchanged, with a test saying so.

### FI-73 — an unknown `--flag` was treated as a FILENAME, and a stale Release build is how it surfaced

- **Found by a fix wave, on the day `--out` was added.** `converter to-ir x.xml --out dir` against a
  build that predated the flag **converted `x.xml` beside its input** — the exact destructive act FI-72
  had just fixed — and only *then* died with an unhandled `FileNotFoundException` on a file literally
  called `--out`. Reproduced before fixing. **The damage is done before the crash**, and *any* flag typo
  does the same: `--projet` converts every other input blind first.
- **Fixed generally:** a leading `--` is never a path here, so an unrecognised flag is refused with the
  valid list, and the message says what the alternative was (*"rather than treating it as a file name,
  which would convert the other inputs first"*) — otherwise the reader learns nothing from it. The
  convert path moved into `Program.RunConvert` so its argument handling is testable at all.
- **THE SECOND HALF IS THE MORE IMPORTANT ONE: BUILT IS NOT DEPLOYED.** The skills invoke
  `src/converter/Converter/bin/Release/net8.0/converter.exe` (see their `allowed-tools` lines), and a
  day of converter fixes had been built **Debug only** — so FI-69, FI-70, FI-71 and FI-72 were committed,
  tested, documented, and *reaching no agent*. The wave that hit this was running the tool as it existed
  before any of it. Nothing in the repo detects that gap.
- **The rule that follows:** *after changing the converter, rebuild Release.* Unlike `openness-cli` this
  is free — the converter has no TIA whitelist and never touches Portal, so `dotnet build -c Release` on
  `converter.sln` is safe at any time, including while Portal work is in flight.
- **Verdict.** Built, 890 converter tests (+2), and verified on the **Release** binary itself: the
  unknown flag exits 1 with the list, `--out` redirects and writes nothing beside the input, and the
  FI-71 refusal still fires on a real 89-network block.

### FI-68 — a relative `--out` failed with an exception that named a different problem entirely

- **The cost was the misdiagnosis, not the failure.** `openness-cli export --out <relative>` throws
  `EngineeringTargetInvocationException`, whose "relative path" sentence is the last line under a
  type-qualified Siemens exception name. It reads **exactly like** the "Inconsistent blocks and PLC
  data types (UDT) cannot be exported" refusal — a real and frequent condition on the job where it was
  hit. The only thing that stopped a long hunt for an inconsistent block was that the agent happened to
  run `sanity-check` between attempts and got `HEALTHY` on both lines each time.
- **The constraint was already written down, for a different argument.** `docs/notes/openness-quirks.md`
  recorded it under "Project paths" for the cold-open path in July, and nobody connected it to the
  export target. That is the general lesson this repo keeps re-learning: *an enumerated list is one the
  next item is not on.*
- **Fixed for the class at the argument boundary** (`Cli/PathArguments`): `export --out`,
  `export-all --out`, `library --out`, the `import` file list and `--tia-install` are resolved by one
  rule, so an argument added later inherits it rather than needing its own special case.
- **The project identifier is deliberately excluded, and there is a test saying so.** It is documented
  as "either a full `.apNN` path or a **bare project name**" — that is how an already-open Portal
  session is matched — so resolving it would silently turn a name into a path that does not exist.
- **Verdict.** Built, 239 openness-cli tests (+7). Live verification needs a Release rebuild (FI-61),
  but the failure it removes was never subtle to reproduce.

### FI-69 — a statement-order violation in the IR blamed the network header

- **The rule is fine; the diagnostic named the wrong thing.** Within a network, statements are parsed
  as a fixed sequence of per-kind sections (`ir/SPEC.md`, "Statement-kind ordering within one
  network"). A statement written out of that order is consumed by no loop, so the parser concluded
  the network had ended and went looking for the next header: `Expected 'NETWORK <n> "<title>"' at
  line 47`. That names a header which is **perfectly well-formed**, at a line number pointing at the
  first statement it could not place. Nothing in it named the rule, the kind, or the expected order.
  **Cost two import passes on a live job** before the actual rule was recognised.
- **Fixed with one table, not one special case.** `IrParser.StatementSections` drives both the check
  and the message, so every one of the 19 kinds gets the same diagnostic rather than only the pair
  that happened to be hit, and a section loop added without its row trips an arity guard instead of
  silently losing the message. The same table covers the network `COMMENT`'s own position rule, which
  failed the same misleading way — the standing preference here is one general fix over stacked
  special cases.
- **Fourth instance of the family**, alongside FI-61's `ConnectTimeout`, FI-66's non-converging count
  and FI-68's relative-path export: a message that confidently names the wrong cause. Every one cost
  an investigation.
- **The stale claim was in the spec, not just the code.** `ir/SPEC.md` documented the old message as
  expected behaviour and listed only 7 of the 19 kinds. Both corrected, with the old message recorded
  so an older transcript still reads.
- **Verdict.** Built (`a053467`), 878 converter tests (+10), including 102 real `.ir` files parsed
  clean to confirm the guard has no false positives on a real corpus.

### FI-70 — nothing compared the IR on disk against what is actually in the controller

- **The blind spot.** `drift-check` compares `ir/<proj>/*.ir` against `simatic-ml/<proj>/*.xml`.
  **Neither side is the controller.** A file edited on disk and never imported, or a block changed in
  TIA and never exported, is invisible to every automated check this project has. Found when an
  **unrecorded editing pass touched seven files**; two were caught only because a later wave happened
  to import them, and the other five — including a library block used by every valve on that plant —
  had never been imported by any recorded wave.
- **Why it does real damage rather than being untidy.** A live job has twice imported a stale export
  with every gate green, because old valid logic is still valid logic. The next wave then either
  imports a stale version over good work, or exports a version nobody authored.
- **Two directions, and only one of them was even representable.** The runner enumerates `.ir` files,
  so a block existing **only in the controller** — added by hand in TIA, or left behind by a rename —
  could not appear in the report under any status. That half was pure silence.
- **Built (the comparison half).** `EXPORT-ONLY` is a new status, always reported. `--complete` is the
  caller's declaration that the exports directory is the whole picture, which is what makes an
  *absence* a finding rather than an ordinary state; it never changes what is compared. A `SCOPE:`
  line now states which question was answered, so a clean `SUMMARY` cannot be read as "disk and
  controller agree" when nothing established that. Same family as FI-52/FI-62/FI-66 — a gate whose
  green result carried more than it earned.
- **Built (the export half), in `openness-cli` where it belongs.** `export-all` dumps every block and
  PLC data type to one directory. It lives there, not in the converter, because the converter is a
  pure in-process file transformer that never touches the environment (FI-24, held on purpose), so
  the smallest correct shape is a two-command recipe rather than a second comparison implementation.
  Three properties, each for a reason the report has to survive: a **refusal is named, never a silent
  omission** (the completeness check reads a missing file as "not in the controller", so quietly
  skipping a safety block would turn a correct refusal into a false finding about the controller); a
  **basename collision is refused rather than resolved** (a suffix would break the basename pairing
  the recipe depends on, and silent overwriting makes the comparison run against the wrong object);
  and **one failure does not abort the rest**, because a partial dump naming its own holes is worth
  something and one that stops at the first problem is not. New exit code 12 `ExportIncomplete`,
  deliberately the same shape as FI-52's 11 — nothing went wrong, the dump is simply not whole.
  Types needed a new `EnumerateTypes()` on the gateway: they had been walked internally since FI-62
  but were never on the interface, and a bulk export silently omitting every UDT would have been
  exactly the partial-read-as-complete failure this whole item is about.
- **Three caveats, all measured rather than anticipated.** Compare **normalised, never bytes**: two
  files whose raw text differed by 776 and 282 lines were semantically identical, because a
  `--no-sidecar` disk copy legitimately omits the sidecar TIA appends — a byte compare would have
  called both drifted. Line endings vary per file within one corpus. And such a tool must **never
  import or decide a side**: on the case that prompted this, "fixing" the divergence automatically
  would have overwritten a convention-compliant disk copy with a non-compliant one, twice.
- **Verdict.** Both halves built. 881 converter tests (+3) and 232 openness-cli tests (+11);
  the comparison half verified against a real 102-file corpus, where the same directory exits 0
  without `--complete` and 1 with it. **`export-all` is not live-verified** — that needs a Release
  rebuild, which revokes the binary's Openness approval until a person re-approves it at the machine
  (FI-61), so it waits for a moment when no Portal work is in flight.

### FI-76 — the map model: derive the block structure from the border, and build it in order-waves

- **Status:** **Raised 2026-08-17 — design settled with the owner, recorded in ADR-0012, BUILD NOT
  SCHEDULED.** Promotion is suspended along with the staged plan (see this doc's header), so "Raised"
  is the correct status even though the design itself is agreed. The decision record is
  **`docs/adr/adr-0012-map-model-and-order-waves.md`**; this entry is the analysis that led to it. The
  costed build plan — 9 items, ~10–13 agent-days, held for later execution — is
  **`docs/notes/map-model-build-plan.md`**.
- **Raised:** 2026-08-17 · **Source:** owner, in conversation — a proposed development methodology
  ("model the solution like a map of a country, where the borders are edge interfaces").

**The idea.** A PLC program is a map. The **border** is the edge interfaces — IO, comms to devices,
control and state from the HMI, and the list is open. **Territories** (things with state surviving the
scan) are carved inside it: equipment first, whose shape is known a priori from `references/<class>/`.
What remains is a **derived territory** — logic not yet written, whose interface is *implied by what
surrounds it* rather than designed. Blocks are then built in **order-waves** outward from the border.

**Merits.**

- **Interfaces become derived, not declared.** The largest remaining declaration in this pipeline is
  the block interface. Under the map it is a set difference. Same instinct that made `reachable-state`
  compute slot disjointness instead of accepting the submitting agent's word for it.
- **It starts at the only thing hard rule 3 forbids inventing.** Carving outward from the border is
  "never invent reality" applied to the *shape* of the program.
- **Five rules collapse into one property.** C-308 multi-writer = overlapping territories; C-304 =
  only border territories touch the border; C-127 = reaching into another territory's interior; dead
  wiring = a border segment with nothing beyond it; hard rule 2 = a frontier with no crossing. A model
  that reproduces independently-discovered rules is usually right.
- **Cycles become violations rather than cases.** Coupling between same-order blocks is always
  mediated by the layer above (two conveyors couple through an order-2 interlock, not to each other),
  so a cycle in the block graph means coordination logic ended up inside an equipment block — C-127
  under a new name. The layering does not just describe the program, it prevents the defect.
- **Measured economics.** From `gen/*/telemetry.log` (20 stage types, 45 runs, **1,637 min**): the
  three stages this replaces — rung C, rung D and `gen-architecture` — are **477 min, 29%** of all
  logged pipeline time, and `gen-architecture` alone at **202 min is the single largest stage in the
  log**. Folding the per-instance half of C and D into the parallel per-block agents puts the
  parallelisable fraction at **35% conservatively / 54% counting per-block review and vector
  authoring**, for an expected **26–40% wall-clock saving** at four effective agents (~54% ceiling).
- **Testability descends with order,** so building outward front-loads the highest-confidence testing:
  order-1 blocks touch the border and the rig can drive them directly.
- It gives the multi-agent machinery already built (`src/wave-control/`, `converter claim`/`claims`) a
  use that actually requires it.

**Costs / risks.**

- 🔴 **The cartographer becomes the critical path and can eat the entire win.** Contract thickness
  trades conflict safety against parallel fraction *directly*: complete contracts are what stop
  semantic divergence, and completing them moves interpretive work back onto one serial agent.
  Recommendation in the ADR is start thick, thin deliberately.
- 🔴 **Semantic gap-filling is undetectable.** Two agents given an underspecified contract fill the
  gap differently, both compile, both pass their own vectors, and nothing collides — it is not a
  conflict, it is two self-consistent readings. `cross-check` catches structural divergence and cannot
  catch this. Needs `docs/15`'s "never an invention" norm promoted to a hard stop for wave agents.
- **Replacing rungs C and D removes the inputs two mechanical-floor checks are built on** —
  `relation-reconcile` and `signal-sweep`, both through the shared `RelationArtifactParsers`. Argues
  for a parseable map artifact from day one. ⚠️ **`docs/15` says all *four* checks parse these files
  and that is wrong: `candidate-scan` and `undriven-scan` take `--project <ir-dir> --fb <FBName>` and
  read IR only** — verified 2026-08-17 by reading the runners. Correcting that line in `docs/15` is a
  cheap item worth doing whether or not this is ever built.
- **Review load arrives a whole layer at a time,** which is exactly the presentation shape
  `11-review-workflow.md` calls a rejection reason by itself. Tolerable for homogeneous order-1 waves;
  not obviously so higher up.
- **Two single-token resources cap the parallelism:** Portal, and the rig (deployment is device-level,
  there is one rig, so vector *authoring* parallelises and vector *execution* does not).
- **Everything about the multi-agent half is prediction.** `src/wave-control/` is built and has never
  run with two agents contending; `Harness.Loop` has never run a wave end to end.
- A correction worth keeping: batching the compile at wave boundaries was argued to be the largest
  win and **is not** — total Portal round-trips across every logged coding run is **16, ~1.6 per
  block**. Preflight already squeezed it. Worth about an hour across a project.

**Dependencies.** The staged plan resuming (`docs/03-development-plan.md`); a `territory` claim kind
in the claims registry; the four mechanical-floor checks re-pointed; the wave-set admission path in
`src/wave-control/` actually exercised with two agents.

**Verdict / revisit trigger.** Design agreed and recorded (ADR-0012); build deliberately not
scheduled. **Revisit on the first wave run**, which should be small — three or four order-1 blocks,
two agents — with the deliverable being *evidence the wave machinery works* rather than the blocks. The
number to instrument is the **cartographer's wall clock**, logged in the existing telemetry format
against the 477 minutes it replaces. One decision is left open in the ADR and gates any scheduler
work: **the mixed-order block** (a block consuming state from order 1 *and* order 3 — is it order 2 by
"lowest", order 4 by `1 + max`, or a violation?).

### OPEN QUESTION — the converter's tag resolution is CASE-SENSITIVE; TIA's symbol resolution is not

**Raised 2026-08-14 by the converter check-tool hammer. Reported rather than changed, because I could
not establish what correct is without Portal, and a session was holding it.**

Every tag-name comparison in the converter is `StringComparer.Ordinal`. So `db_shared.casetarget` and
`DB_Shared.CaseTarget` are **two different storage paths**, and the three tools agree with each other
on that — which is why nothing has ever flagged it:

```
converter cross-check --project <lab>     # DB_Shared.CaseTarget: written-but-never-consumed;
                                          #   writers: FC_CaseA N1        <- FC_CaseB's write INVISIBLE
                                          # and NO multi-writer reported, though two blocks write it
converter preflight   FC_CaseB.ir --project <lab>   # exit 1: tag root 'db_shared' does not resolve
converter tagstatus   db_shared.casetarget --project <lab>   # PROPOSED (root: db_shared)
```

**Why it is contained today, and why that is not the same as settled.** `preflight` REFUSES the
lower-case reference outright, so a block written that way never reaches the compile gate — the
pipeline fails closed. The exposure is confined to a block that entered the project by another route
(authored in TIA by hand, or imported from an export whose casing differs), where **`cross-check`'s
C-308 multi-writer analysis would be structurally blind to a case-varied second writer** — a false
NEGATIVE on the one check a reviewer reasons over for write conflicts.

**What would settle it, in one measurement:** import two blocks writing the same DB member in
different casing and compile. If TIA resolves them to one symbol, `Ordinal` is wrong for the
usage-graph key (though probably still right for `preflight`'s refusal, which enforces a house style
rather than the compiler's tolerance) and `StorageGroups` needs an `OrdinalIgnoreCase` key. If TIA
refuses, the current behaviour is correct throughout and this entry closes.

**Do NOT "fix" it by making every comparison case-insensitive on the strength of the reasoning
above.** That is a comparator learning to equate more representations, which is how a comparator
starts passing things — and it would silently merge two paths in the usage graph on every project,
for a defect nobody has yet observed on real data.

---

### FI-77 — a per-block compile reports the whole program's errors, and the count includes empty parent nodes

- **Status:** **Raised 2026-08-24 — measured on a real project, documented in
  `src/openness-cli/README.md`, NOT FIXED.** The counting half is deliberately left alone; see
  *Why it is not simply fixed* below.
- **Raised:** 2026-08-24 · **Source:** a build lane keying on the `ERRORS:` line, exactly as the
  compile-gate guidance tells it to, and reading a provably clean block as seven errors.

**What was measured.** `compile --block <name>` on a block that had just been repaired printed:

```
STATE: Error
ERRORS: 7  WARNINGS: 2
NOTE: compiler reported ErrorCount=2, WarningCount=1 - these disagree with the messages
      above and are not reliable; counts shown are from the message tree.
CONSISTENT: yes  (re-read after the compile; TIA will export this item)
...
[Success] <the compiled block>: Block was successfully compiled.
[Error] Compiling finished (errors: 2; warnings: 1)
```

**None of the seven errors belonged to the block being compiled.** Two were real errors in an
unrelated block in a different folder — not a dependency, not in the import set. Four were the
structural parent nodes TIA emits above them (`<device>:`, `Program blocks:`, `<folder>:`,
`<the other block>:`), each carrying `Error` state and an **empty description**. The seventh was the
`Compiling finished` rollup.

**Two distinct defects, and they point opposite ways.**

1. **Scope.** A `--block` compile's message tree is program-wide. The per-block command reports a
   whole-program compile, so `STATE:` and `ERRORS:` are not about the block named on the command
   line. The only per-item verdict is the `[Success] <name>:` line plus `CONSISTENT:`.
2. **Counting.** The tree count inflates by counting non-leaf nodes that carry a state but no
   message. Here the compiler's own `ErrorCount=2` was *right* and the tree count of 7 was wrong —
   the reverse of the case the `NOTE` was written for, where the compiler reported `WARNINGS: 0`
   against 156 warning messages.

🔴 **The two have different blast radii — the counting half is NOT confined to `--block`.** Confirmed
the same day on the same project: `sanity-check`'s station device-compile line read
`Error (errors=9, warnings=0)` while the compiler's own tail *inside that same block* read
`Compiling finished (errors: 2; warnings: 0)` — **six of the nine were empty `[Error]` tree headers**,
rendered as blank lines in the output. The scope defect is specific to `--block`/`--type`; the
counting defect reaches `--station`, `compile-all` and `sanity-check` too. This second sighting is
what makes the leaf-counting fix worth doing rather than merely documenting.

**The exit code is NOT affected and needs no change.** It takes the larger of the two counts, so it
is fail-closed and cannot read a dirty program as clean. What it silently is, though, is a
*program-wide* gate on a per-block command: a genuinely clean block in a program containing one
unrelated broken block still exits non-zero. That is the safe direction, but it is not what the
command appears to promise.

**Why it is not simply fixed.** Counting only *leaf* messages would drop the four structural nodes
and reconcile both observations at once. But `CompileMessage` is **flattened at collection** —
`CollectMessages` recurses the nested `CompilerResultMessage` tree and keeps every node with no
parent/child marker — so by the time `OutputFormatter.CountFromMessages` sees them, leaf and parent
are indistinguishable. Restoring the distinction is a model change (`CompileMessage` gains a leaf
flag set at collection time) plus a live compile to confirm the real tree shape.

🔴 **The tree shape above is inferred from pre-order output, not observed.** The rollup node prints
*last*, which a pre-order walk of a single root would not do, so there are multiple roots and the
nesting is not what the flat listing suggests. **Confirm the shape against a live compile before
writing the fix** — this entry records a measurement and a hypothesis, and is careful to say which
is which.

**Do NOT "fix" this by trusting the compiler's aggregates instead.** That inverts a defect already
measured in the other direction and would make a 156-warning compile report zero.

---

### FI-78 — `converter diff` can declare an insertion but not a deletion, so an add-and-remove change cannot pass

- **Status:** **Raised 2026-08-25 — measured on a real change, NOT FIXED.**
- **Raised:** 2026-08-25 · **Source:** a purpose change that inserted one network and removed
  another, and could not be proven at exit 0 by the tool whose whole job is proving exactly that.

**The gap.** `converter diff` matches networks on **content, not number**, and a `Moved` network
**gates** — correctly, because LAD executes in network order. `--insert <n>` is the checked escape
for an insertion: it declares where a network was added so the following networks' renumbering is
expected rather than suspicious. **There is no counterpart for a removal.**

So a change that both adds and removes a network is unprovable. Measured: an edit inserting one
network at position 2 and removing one at position 37 left 31 networks shifted by +1, every one of
them reported by `diff` itself as *"content unchanged"*, and the strict run exits **1**.

**Why this matters more than it sounds.** The two operations arrive together far more often than
either arrives alone — replacing a mechanism means adding the new network and deleting the old one,
which is precisely the shape of a well-executed refactor. The tool is at its least useful on the
change that most needs it, and its failure mode pushes the author toward the two bad escapes:
widening `--only` until the gate passes, or splitting one coherent change into two commits that each
pass while neither reflects what happened.

**What the author did instead, and it is the right pattern to copy until this is fixed:** report the
strict run at its true exit 1; supply a staged run proving the additive half alone is a clean
insertion; supply a widened run naming how many networks were proven identical; and prove the
*ordering* half mechanically outside the tool — extract the ordered network-title sequence before and
after, drop the one addition and the one removal, and compare. Relative order preserved across every
surviving network is the property LAD semantics actually depend on, and it is scriptable.

**The likely fix** is a `--remove <n>` counterpart to `--insert <n>`, with the same "declared and
checked" contract: the caller states where a network was deleted, and the tool verifies the
renumbering below it is exactly the shift that deletion implies and nothing more. Both flags would
then mean what `--insert` already means — **ROUTE, not DECLARE**: a change needing either is a purpose
change and belongs in the purpose-modify path, never the fix path.

🔴 **Do NOT "fix" this by making `Moved` non-gating, or by having the tool infer deletions.** The
gating is the value: LAD executes in network order and a silent reorder is a behavioural change that
no other check on the mechanical floor would catch. An inferred deletion is the tool guessing at
intent, which is the same class of error as a comparator learning to equate more representations.
The escape must stay **declared by the caller and checked by the tool.**

---

### FI-79 — `converter diff` without `--only` returns exit 0 unconditionally, and exit 0 reads as a pass

- **Status:** **Raised 2026-08-25 — confirmed from source, documented in `src/converter/README.md`,
  NOT changed.** The exit code is a contract other callers key on; see *Why it is not simply changed*.
- **Raised:** 2026-08-25 · **Source:** an agent that ran the bare form, got exit 0, and **doubted it**
  — then established from the code that the number could not have been anything else.

**The finding.** Both halves of `diff`'s verdict are guarded on `--only` having been supplied:

```csharp
public IReadOnlyList<NetworkDiff> InvarianceViolations =>
    AllowedNetworks.Count == 0 ? Array.Empty<NetworkDiff>() : …   // no --only ⇒ empty, always

public bool HasUnclaimedHeaderChange =>
    AllowedNetworks.Count > 0 && !HeaderChangeAllowed && …        // no --only ⇒ false, always

public bool HasInvarianceViolation => InvarianceViolations.Count > 0 || HasUnclaimedHeaderChange;
```

So a bare `converter diff <old> <new>` exits **0** with any number of changed, added or removed
networks, **and** through an undeclared interface retype **and** a block name mismatch — the two
header cases the gating rules exist to catch, one of which (`Bool` → `Int` with no network touched)
compiles, imports, and misbehaves on the controller, and the other of which makes TIA's
name-matching import create a **duplicate block** rather than update one.

**Why it matters more than a missing flag.** `diff` is the tool agents are instructed to quote as
invariance evidence — *"touch only the named network(s), and prove the rest identical with
`converter diff --only`."* The failure mode is not a wrong answer; it is a **confident right-looking
answer to a question that was never asked.** An agent reporting *"`converter diff` exit 0"* without
`--only` has reported nothing, and nothing about the output says so.

🔴 **This is `EMPTY IS NOT CLEAN` in the one place the convention does not apply itself.** Across the
mechanical floor — `candidate-scan`, `undriven-scan`, `reuse-scan`, `relation-reconcile`,
`signal-sweep` — examining nothing is **exit 2**, deliberately, *because a pass must never be
returned by a check that looked at nothing*. Here examining nothing is **exit 0**. The project's own
principle is stated in the README of the tool that breaks it.

**Why it is not simply changed.** Making the bare form exit 2 is the answer that matches the
convention, and it is also a **breaking change to an exit-code contract** — the report form is
legitimately useful and other callers may key on 0. The safe shape is probably: keep 0 for the
explicit report form, and make the *verdict lines themselves* state that no invariance question was
asked, so the output cannot be quoted as a gate even when the exit code is.

**Do NOT "fix" this by making `--only` mandatory.** The report form has real uses — orientation on
an unfamiliar change, and the staged proofs FI-78 describes, where a widened run is quoted precisely
*as* a report alongside the strict run. Removing it would push authors toward worse evidence, not
better.

**A second trap, measured the same day and the reason the first was nearly misfiled.** A `diff` run
piped to `tail` reports **`tail`'s** exit status, not `diff`'s, and a real gate result was almost
recorded as a tool defect on the strength of it. The standing *"never pipe"* rule was written for
`openness-cli` (Portal is launched as a child and the pipe outlives the command); the mechanism here
is different and ordinary, but the discipline is identical — **redirect with `>` and read the file.**

---

### FI-80 — the Normalizer's Access content key is EMPTY for a constant, so two different constants compare equal

- **Status:** ✅ **FIXED 2026-08-27, and the false PASS was DEMONSTRATED before it was fixed.**
  Raised earlier the same day as analyse-only; the owner then asked for the fix **and** for the
  wider case to be measured rather than left as inference. Both done. 1,676 converter tests pass.
  ⚠️ **This does NOT make the observed 940-difference comparison clean** — that noise comes from
  the converter emitting a non-canonical `Scope` for constant references, which is a separate,
  still-open defect. This fix stops two *different* constants being interchangeable; it does not
  change what we emit.

  🔴 **THE WIDER CASE WAS REAL AND BIGGER THAN THE ORIGINAL FINDING.** Measured across **47 real
  exports**: `LiteralConstant` occurs **151 times and carries `<Symbol>` exactly ZERO times**;
  `TypedConstant` 5 times, likewise zero. So the empty-discriminator key was never confined to
  `LocalConstant` — **every literal in every document collapsed to `tag:LiteralConstant:`**, and
  only `TypedConstant` escaped it via its own special case. The entry below flagged this as an
  inference to be measured before acting; it measured true.

  **The harmful direction was demonstrated, not argued.** The regression test takes a real block
  fixture, leaves both constant `<Access>` elements byte-identical, and swaps ONLY the wires
  (`IdentCon`) between them — a genuine change to which constant feeds which pin. Against the
  unfixed `Normalizer` that comparison returns **`Expected: Differs / Actual: Equivalent`**: a
  comparator passing two documents that differ. Changing a literal's *value* instead would have
  been caught by the element comparison and would have proven nothing about the key.

  Fix: `Converter/SimaticMl/Normalizer.cs` — when an `Access` has no `<Symbol>`, discriminate on
  what it actually carries: `<Constant Name=…>` → the name; `<Constant><ConstantType>/<ConstantValue>`
  → type + **canonicalized** value. Test: `Converter.Tests/CompareTests.cs`,
  `TwoConstantsSwappedBetweenOperands_CompareUnequal`.
- **Raised:** 2026-08-27 · **Source:** decomposing a 940-difference re-export comparison, where the
  same defect showed up in its harmless direction (noise) and its mechanism made the harmful
  direction obvious.

**The code.** `Normalizer.AccessContentKey` rewrites each `Access` UId to a content-derived key so
two documents compare equal regardless of the arbitrary numbers TIA assigned. It special-cases
`TypedConstant`, then falls through to:

```csharp
var symbol = access.Elements().FirstOrDefault(e => e.Name.LocalName == "Symbol");
return $"tag:{scope}:{symbol?.ToString(SaveOptions.DisableFormatting)}";
```

**A `LocalConstant` Access has no `<Symbol>`.** It carries `<Constant Name="…"/>`. So `symbol` is
null and the key is literally **`tag:LocalConstant:`** — an empty discriminator. **Every
`LocalConstant` in the document collapses to one key**, and because that key also feeds the
part-identification refinement, two *different* constants become interchangeable to the comparer
**and** to the topology hashing built on top of it.

The method's own comment states the intent it defeats: *"The full `<Symbol>` … so two Access elements
only compare equal when truly identical, not just same top-level path."*

🔴 **Why this is worse than the noise that revealed it.** In the observed case it only inflated a
difference count. The same mechanism, with the operands the other way round, **silently equates two
documents that differ** — a comparator quietly passing something it should have caught. That is the
one failure a comparator must never have, and it is the exact class this repo warns about elsewhere:
*"a comparator learning to equate more representations is how a comparator starts passing things."*

⚠️ **Possibly wider — stated as INFERENCE, not measurement.** The same fall-through takes
`LiteralConstant`, and the `TypedConstant` branch's own comment records that *TIA writes a Real
literal under `Scope="LiteralConstant"`*. If those Access elements likewise carry no `<Symbol>`, then
**every literal in a document collapses to `tag:LiteralConstant:` as well.** Not measured. **Measure
it before acting on it in either direction** — including before assuming the blast radius is small.

**The fix shape is already in the same method.** `TypedConstant` returns
`const:{NumericLiteral.Canonicalize(value)}`. The other constant scopes need an equivalent
discriminator drawn from what they actually carry — the `<Constant>` element's `Name` and/or
canonicalized `ConstantValue` — rather than a null `<Symbol>`. **Canonicalize, do not use raw text:**
that branch exists precisely because a raw literal would put `0.10` vs `0.1` back into the comparison
after `NumericLiteral` removed it.

**Do NOT fix this by falling back to the raw Access UId when the Symbol is missing.** That reinstates
the volatile number the whole content-key mechanism exists to remove, and would turn every ordinary
TIA UId reassignment back into a false *difference* — trading a silent wrong answer for a noisy one
in the other direction.

**A regression test must assert the harmful direction**, not just the noisy one: two documents whose
only difference is *which* constant an Access names must compare **UNEQUAL**.

---

### FI-81 — `compile-all` is not convergent: one pass can leave more inconsistent than it cleared

- **Status:** **Raised 2026-08-27 — measured on a real project, NOT FIXED.**
- **Raised:** 2026-08-27 · **Source:** an import that re-typed a widely-declared UDT, where a single
  `compile-all` reported success-shaped progress and left the project dirty.

**Measured.** After importing a changed type plus two blocks, one `compile-all` reported
`passes: 1`, cleared the 6 primary items — and **in that same pass knocked their 9 instance DBs and
4 caller FCs inconsistent**, finishing `stillInconsistent: 14`, exit 8, **without retrying.** A
second explicit pass, ordering **instance DBs before their callers**, cleared all 14. Visible in the
raw gate files either side: `INCONSISTENT: 14` then `INCONSISTENT: 0`.

**Why it happens.** Compiling a block re-derives the layout of everything that declares it, so
clearing a primary *creates* inconsistency downstream. That is ordinary TIA behaviour. The defect is
that a command named `-all` **does not iterate to a fixpoint** — it makes one pass and reports what
is left, so the caller must know to run it again, in dependency order, to get the result the name
implies.

🔴 **The gate consequence.** `sanity-check` immediately after a single `compile-all` reads the
*intermediate* state, not the settled one. A lane that ran one pass and quoted the gate would be
quoting a number that a second pass changes — and the exit code (8) is the only hint, on a project
where **a clean block already exits 8 on a standing hardware-warning floor** (see `compile` in
`src/openness-cli/README.md`). **Two different reasons for the same exit code, one of which means
"not finished".**

**The fix shape:** iterate until the inconsistent set stops shrinking, bounded by a pass limit, and
report the pass count and the final set — so `stillInconsistent: n` means *n after convergence*, not
*n after one attempt*. Dependency-ordering instance DBs before callers within a pass would cut the
iterations but is not a substitute for iterating.

**Do NOT "fix" this by making the caller responsible for re-running.** That is the current
behaviour, and it already produced a project reported at `INCONSISTENT: 14` by a command asked to
compile everything.

## FI-82 — `signal-set` reports an IEC timer's own members as `unused`

**Raised 2026-08-27.** Status: **Raised.**

Found on the first run of `converter signal-set` (`93ad3ae`) against real material rather than the
reference corpus — which is the point worth recording, because the reference corpus did not surface
it.

A block declaring an IEC timer instance gets that instance's four members — `.IN`, `.PT`, `.ET`,
`.Q` — enumerated as ordinary interface leaves, each with `direction: unused`. They are not unused.
A timer's members are reached through the timer Part, not through ordinary Accesses, so the usage
graph legitimately records no reader or writer for them. `ProjectUsageGraph`'s instance walk
excludes them; `SignalInventory`, which is declaration-only, does not — and `signal-set` joins the
two, so the exclusion is lost.

**Why this is worth an entry rather than a shrug.** `unused` is the one direction a consumer acts
destructively on. It reads as "declared and dead", which invites deletion of a member the program
depends on, and it inflates the unused count on every block carrying a timer — most of them. On the
block this was found on, **4 of the 6 reported `unused` members were timer members**, so the false
positives outnumbered the real findings 2:1. A number that is wrong two-thirds of the time in the
direction of "safe to delete" is worse than no number.

**Not fixed on discovery, deliberately.** Excluding timer members outright would be a judgement the
document is not entitled to make — a harness consumer may legitimately want to observe a `.Q`, and
`signal-set`'s contract is to report what the corpus declares, not to curate it. The likely right
shape is a distinct value (`direction: instance-internal`, or an `originKind` marking the member as
reached through a Part) so a consumer can filter deliberately, rather than silently dropping rows.
Whatever is chosen, **the count must stay honest**: dropping the rows would make `filesScanned` and
the total disagree, which is the denominator discipline every other mechanical-floor command holds.

**Verdict / revisit trigger:** Open. Revisit when a consumer first acts on `unused` — a binding
generator that excludes unused signals, or any proposal to delete a member on this evidence. Until
then the entry is the warning: **`unused` from `signal-set` is not yet a safe input to a decision.**

## FI-83 — `SidecarSynthesizer.ScopeFor` has no CONSTANT case, so a block CONSTANT is emitted as a variable

**Raised 2026-08-27.** Status: **Raised.**

`ScopeFor` decides `LocalVariable` vs `GlobalVariable` and nothing else. A reference to a member of
the block's own **CONSTANT** section is a local name, so it takes the `LocalVariable` branch and is
emitted with a `<Symbol>`. TIA's canonical form is `Scope="LocalConstant"` carrying a
`<Constant>` — so TIA **stores and returns the repaired form**, and a sent-vs-returned `compare`
shows one real difference per such reference.

**Why this is not "harmless because it compiles".** It compiles because **TIA repaired it**, not
because it was right. The 2026-08-27 confirm loop measured the neighbouring case directly: TIA
accepts a mislabelled scope **at import, exit 0**, and refuses only at compile — so "it imported" is
not evidence about scope correctness, and here even the compile passed because TIA had already
rewritten the value. The mechanism that saved this is TIA's tolerance, and tolerance is not a
contract. This is the same reasoning that retracted BD-19: a block compiling clean proved TIA
*tolerated* a form, not that the form was canonical.

**The second cost is to drift detection.** Every re-export of an affected block differs from what
was sent, so genuine drift hides in known noise, and `drift-check` / `compare` on those blocks read
as dirty for a reason that is not a change. This is the same class of harm FI-80 records from the
other side of the round trip: FI-80 is the COMPARER unable to tell two constants apart; this is the
SYNTHESIZER unable to emit one correctly. They are independent and both are open.

**Reaches production only through re-derivation.** A block whose stored sidecar is used is
unaffected; a block whose sidecar is re-derived (ADR-0005 derive-always, or any lane that must drop
a stored sidecar to synthesize new statements) picks it up. So it appears intermittently, on the
blocks most recently edited — the worst possible distribution for noticing it.

**The fix shape:** `ScopeFor` needs to know a name's SECTION, not merely whether the block declares
it. The block's `ConstantMembers` are already in scope at the call site — the same set the local-name
walk builds from — so this is a section-aware lookup rather than new analysis. The writer then needs
the `<Constant>` emit shape for that scope.

**Verdict / revisit trigger:** Open. Revisit before any lane relies on `compare` or `drift-check`
being clean on a re-derived block — which is the next time a sent-vs-returned comparison is used as
a gate rather than as an observation.

## FI-84 — the readable IR cannot express an INTERLEAVED pair of instruction kinds

**Raised 2026-08-27.** Status: **NARROWED 2026-09-02 — the headline above is overstated; read the
correction at the end of this entry before acting on it.** Found while deriving a specification from
an implemented block — i.e. by reading a real network closely, not by exercising the converter.

`ir/SPEC.md` requires each instruction kind to appear as **one contiguous run**, in a fixed relative
order between kinds, and states plainly that listed order **is** execution order: *"it mirrors real
top-to-bottom rung-execution order within the compiled network, so it determines same-scan data
freshness."*

A real network can interleave two kinds. The observed shape is three independent branches off the
power rail, each an arithmetic instruction feeding a conversion — `A→B`, `A→B`, `A→B`, paired by
wire and sharing one TEMP. The grammar cannot render that. It renders all three `A`s, then all three
`B`s.

**Read literally in the order the IR states, that computes the wrong answer** — every conversion
would take the last arithmetic result, and all three outputs would collapse to one value.

**Why this is an ADR-0010 item and not a cosmetic gap.** ADR-0010 is *no IR that the AI cannot
change*, and requires that anything the AI must change lives in the **readable** IR and never only
in the sidecar. Here the pairing that makes the network correct lives **only** in the wires. So:

- The readable IR **misstates the execution order** of a block that is currently correct.
- A reader — human or agent — reasoning from the readable form reaches the wrong conclusion, and the
  block's own comment has to carry a warning to stop them.
- **Any re-render, reorder or regeneration that honours the stated grammar would serialise the pairs
  and silently swap the outputs, with no compile error**, because the result is well-formed. The
  compile gate cannot catch a defect whose only symptom is a wrong value.

**It is not currently broken**, and that is the dangerous part: the correctness sits in a
representation the format does not claim to preserve, so it survives by not being touched.

**What this does NOT establish.** That the wires execute as branch-depth-first was read off the
export and reasoned about, not measured on a controller. The claim above holds either way — if that
reasoning is wrong the block is already broken and the finding is more urgent, not less — but the
mechanism should be confirmed against a running CPU before any fix is designed on it.

**Fix shape, not yet chosen:** either the grammar grows a way to express a branch (so the pairing is
readable and re-renderable), or the converter REFUSES to render a network whose wires interleave
kinds it would have to serialise — a loud failure being far better than a silent reorder. The second
is cheaper and is the house style; the first is what ADR-0010 actually asks for, since a construct
the AI cannot express is a block the AI can never safely regenerate.

### 🔴 CORRECTION, 2026-09-02 — the pairing IS expressible, and IS machine-verified

Recorded rather than rewritten: the entry above stood for six days and was cited to scope other
work, so what it got wrong matters as much as what it got right.

**"The readable IR cannot express interleaved instruction kinds" is FALSE as written.** `ir/SPEC.md`
carries a dedicated section — *"Index-paired `MUL`/`CONVERT` batches (the 'HMI Times' idiom)"* — which
states the disambiguating rule outright: pairing is **by index within each kind's run**, so the *i*-th
`CONVERT`'s `EN := ENO` is gated by the *i*-th `MUL`, **not** by the textually preceding line. That
section landed in `ea70afa` on **2026-07-16 — six weeks BEFORE this finding was raised.** The global
"listed order *is* execution order" sentence quoted above is locally overridden here, and the local
rule governs.

**The pairing is also verified, not merely documented.** Checked on `FB_PusherControl` NETWORK 3, the
concrete case this entry describes:

- The sidecar derived from the committed TIA export wires `mul[i].eno → convert[i].en` for all seven
  branches, each MUL fed independently from the rail — no MUL chains from another.
- That block is stored **sidecar-less**, and `converter to-ir --no-sidecar` refuses unless the derived
  form is proven semantically equivalent to the export. So index-pairing was verified **by the tool at
  store time** for this block, not assumed by a reader.
- `SidecarSynthesizerFidelityTests.TwoIndependentMulConvertChainsInterleaved_SynthesizedSidecar_PreservesLogic`
  is the regression fixture, described in-source as the one that would catch a regression back to
  batching.

**Consequence for readers and vector authors: the concern does not apply.** All seven of that
network's presets are determinable from the readable IR — 500 / 5000 / 60000 / 2000 / 30000 / 1000 /
5000 ms. Withholding timing assertions on this ground would be over-caution against a question that
has been answered. A trap worth naming: `PumpRunOnTime × 1000 = 5000 ms` is index 6, the LAST `MUL`,
so the naive misreading and the truth **coincide for exactly that one preset** — which is what makes
the wrong reading feel confirmed when spot-checked.

**What survives, and is still worth keeping this entry for:**

1. **The disambiguating rule is positional and unenforced at read time.** Nothing in `to-ir` refuses
   or flags a hypothetically crossed wiring; the readable text would look identical.
2. **SPEC's global statement locally misleads** any reader who does not reach the override — which is
   exactly how this finding was raised in the first place, by a careful reader.
3. **Depth-first branch execution remains unmeasured on a controller.** The seven branches share one
   TEMP, so correctness needs each branch to complete before the next MUL overwrites it. Note its
   shape: if that is false the block is **already broken in service**, so no choice of reading
   rescues a vector.
4. 🔴 **THE REVISIT TRIGGER STILL BINDS FOR WRITES.** Reads and vector authoring are clear. Reordering,
   re-rendering or regenerating such a network is NOT — the index convention is precisely what a
   reorder would silently break, with no compile error.

**Verdict / revisit trigger:** Open. 🔴 **Revisit BEFORE any tool regenerates, re-renders or
round-trips a network containing more than one instruction kind** — that is the operation this makes
unsafe, and it is an operation the pipeline performs routinely.

---

## FI-85 — `converter diff --insert` silently keeps only the LAST value when repeated

**Raised 2026-09-02.** Status: **Raised.** Found by a lane proving invariance on a multi-point
insertion into `Main`, not by exercising the converter deliberately.

A revision that inserts networks at three positions is a normal shape: an OB gaining an arbiter at
network 1 and two stimulus heads at 3 and 4. The natural invocation is

```
converter diff --only 1 --only 3 --only 4 --insert 1 --insert 3 --insert 4
```

`--only` accumulates. **`--insert` does not** — it overwrites, so the run reported
`declaredInsertAt: 4`, `movesAreDeclared: false` and **twelve** `invarianceViolations`, every one of
them an artefact of the two undeclared insertions renumbering everything below them.

🔴 **THE DANGEROUS HALF IS THE RENDERING, NOT THE ARITHMETIC.** The text-mode summary for that exact
invocation **reads like a pass.** Only the exit code and the `--json` fields say otherwise. This is
the project's standing "a warning is not a gate" failure shape, and it is worse here than usual
because the tool is being used specifically to PROVE something: an agent reaching for `diff --insert`
has already decided the run is its evidence, so a summary that looks green is exactly the thing it
will paste into an `evidence.json`.

**The workaround used, and it is sound but expensive:** prove invariance as a CHAIN of single-point
insertions, each hop declaring one `--insert`, with every stage emitted by the same generator from
the same call list rather than hand-built, and the final hop's "new" side being the committed
deliverable rather than a staging copy. Three insertions cost three gated runs plus two staging
artifacts. It also leaves OB-shaped files named `Main` lying in a scratch tree, which is its own
small hazard if a `--program` glob ever reaches them.

**Fix shape, not yet chosen:** either `--insert` accumulates like `--only` (the obvious repair, and
what every caller already expects), or a repeated `--insert` is a hard REFUSAL naming the collision —
the house style, and better than silently honouring one of them. Either way the text summary must
not render a violation-bearing run as a pass.

**Verdict / revisit trigger:** Open. Revisit before the next multi-point insertion is gated —
realistically the merged multi-slot `Main`, which is exactly this shape again.

## FI-86 — a wave that RAN writes no result file, because the renderer throws after the run

**Measured 2026-09-02, on a real wave against the bench rig.** The loop connected, the verifying
gateway matched the build stamp on the device, the copy layer deployed, an index executed, the
mirror feed published 8 documents and the rig was released cleanly. Then:

```
Unhandled exception. System.InvalidOperationException: JsonSerializerOptions instance must
specify a TypeInfoResolver setting before being marked as read-only.
   at System.Text.Json.Nodes.JsonValueCustomized`1.WriteTo(...)
   at Harness.Run.LoopCli.Render(LoopResult result) in src/harness/Harness.Run/LoopCli.cs:line 2087
   at Harness.Run.LoopCli.Run(...) in src/harness/Harness.Run/LoopCli.cs:line 399
```

`--out` is written by `Render`, so **the file never appears** and the process exits non-zero on a
run that succeeded. In the observed case the path still held a PREVIOUS run's result — a
`NotAdmissible` one — so the only machine-readable artifact on disk described a wave that never
happened, while the wave that did happen survived solely as console text.

🔴 **This is worse than losing output.** The project's whole discipline is that a result package,
not a transcript, is the evidence; `agent-tasks/*/evidence.json` cite result JSON by path. A reader
finding a stale `NotAdmissible` file next to a green console log has no mechanical way to tell which
describes the run, and the stale file is the one that looks authoritative. It is the same shape as
the `compile-all --json` false-clean: the machine-readable half says something confident and wrong.

**Where it is.** A `JsonValueCustomized<T>` inside the node tree — a `JsonValue.Create(someObject)`
whose type needs a resolver — reaches `ToJsonString` with options that were marked read-only without
one. The fix is either to stop putting a customised value into the tree (build the node from
primitives) or to give the options a `TypeInfoResolver` before they are frozen.

**What it does NOT affect:** the wave itself, the device, the mirror feed, or socket release. All
four were clean. This is purely the report.

**Regression test to write with the fix:** render a `LoopResult` from a run that RAN, over a slot
that exited early, and assert the file is written and parses — the throwing path is only reachable
once real per-index data is in the tree, which is why every earlier `--generate-only` run rendered
fine and this was not caught until a wave ran.

## FI-87 — gate 0c's verdict depends on the CALLER'S WORKING DIRECTORY, silently

**Measured 2026-09-02.** The same submission and the same binding, checked twice, differ only in the
directory the command was run from:

```
run from the directory holding the artifacts   -> VERDICT: ADMISSIBLE-SUBJECT-TO-JUDGEMENT
                                                   28 checked, 0 refused, 0 not checked
run from its parent, paths given as harness-x/  -> VERDICT: NOT ADMISSIBLE
                                                   0c derived fields: NOT CHECKED
```

`harness-gate derive` records each derivable field's artifact as the path it was GIVEN — normally a
bare filename, because it is usually run beside the artifacts. Gate 0c then re-opens that path to
re-hash it, resolving it against the **process working directory** rather than against the
submission's own location. From anywhere else the artifacts "could not be read", and 0c fails closed.

🔴 **Failing closed is right; being invisible is not.** The message says the artifact could not be
read, but not that it was looked for relative to the CWD, so the reader's natural conclusion is that
the provenance is broken or the artifact is missing. It cost a wave cycle here: `harness-run` runs
the same gate internally, so a loop launched from one directory up stops at `NotAdmissible` with
`0 gate(s) refused, 1 could not run` and never names which — the loop's own summary omits the gate
list that `harness-gate check` prints.

**Two candidate fixes, and the second is the honest one:**

1. Resolve a relative artifact path against the **submission file's directory**, which is what every
   caller means. Cheap, and matches how the paths are written today.
2. Have `derive` record an absolute path, or record the base it resolved against, so the submission
   carries its own answer instead of depending on how it is later invoked.

**Also worth fixing beside it:** when the loop stops at `NotAdmissible`, it reports counts
(`0 refused, 1 could not run`) but not WHICH gate. Naming it costs one line and removes the need to
re-run `harness-gate check` by hand to find out.

## FI-88 — a UDT-typed STATIC member used 251 times reports `unused`, and the binding scaffold goes blind

**Measured 2026-09-02, on a real program, while choosing which blocks a conformance harness could
reach.** `converter signal-set` was run over eight function blocks in one project. On five of them a
UDT-typed `STATIC` member — in each case the block's **principal caller-visible interface** — came
back as a single unexpanded row:

```
member = <name>   type = "UDT_<name>"   direction = unused   writers = []   readers = []
```

while the same run expanded that block's TIMER instances and its global-DB references normally.
**`partial: false`, `examinedNothing: false`, exit 0** — a confident, complete answer.

It is wrong. Counting references in executable lines only, with comment and declaration lines
excluded:

| member | references in its own block | reported |
|---|---|---|
| block A's settings type | 3 | **expands correctly** |
| block B's interface type | 56 | `unused`, no writers, no readers |
| block C's interface type | 136 | `unused`, no writers, no readers |
| block D's interface type | **251** | `unused`, no writers, no readers |

🔴 **THIS IS THE BIT-SLICE DEFECT'S FAMILY, AND THE SAME SENTENCE APPLIES.** `signal-set`'s stated
reader is a GENERATOR, which is why that command gates on PARTIAL rather than warning — "a generator
never sees a warning line". **This defect does not produce a partial.** It produces a confident
`unused` for the busiest member in the block, and `harness-binding` scaffolded from it finds no
drivable input and no observable output on the block's whole interface.

**Practical cost, measured:** of eight function blocks in that program, exactly **two** could be
harnessed. The other six were excluded on the strength of this output. That is a testing capability
lost to a tooling defect, and from the outside it is indistinguishable from "those blocks have no
observable interface".

### What was ruled OUT, so the next reader does not re-test it

Four hypotheses were tested against the corpus and each is refuted by a counterexample:

- **Nested UDTs or arrays inside the type** — the smallest non-expanding type is 19 lines with zero
  nested type references and zero array declarations, structurally identical to a 17-line type that
  expands.
- **An inline `COMMENT` on the declaration line** — a non-expanding member declared
  `<name> : "UDT_X" RETAIN SETPOINT`, with no comment, has the byte-identical form to an expanding one.
- **The declaration section** — every member compared, expanding and not, is `STATIC`.
- **Whether the member is used at all** — inverted, in fact: the member used 251 times fails and the
  member used 3 times succeeds.

The hypothesis those four left standing — **whether the `.ir` had been ROUND-TRIPPED THROUGH TIA** —
was not tested at the time, and it is the right one. The member that expanded came from a re-export;
the ones that did not came from the generation pipeline.

### Root cause — ESTABLISHED 2026-09-03, two conditions, only one of them a bug

**(a) Expansion was keyed on INLINED TEXT, and type resolution was never attempted.**
`SignalInventory.CollectLeaves` expanded a member **iff** `DbMember.NestedMembers` was already
populated, which happens only from indented child lines physically present in the block's own `.ir`.
`SignalInventory.AddFile` returned on every `TYPE ` file without parsing it, and `SignalInventory`
never received a `TagTypeRegistry` at all. So a `.ir` round-tripped from a TIA export carries the
inlined body and expands; one an authoring pipeline wrote does not. That is the whole defect, and it
explains every row in the table above — including why the 3-reference member worked and the
251-reference member did not.

**(b) `ProjectUsageGraph.UsagesReaching`'s descendant exclusion IS NOT A BUG AND WAS NOT TOUCHED.**
Once (a) expands `IO : "UDT_X"` into `IO.Cmd`, `IO.Status`, …, those keys match EXACTLY; the struct
root stops being a leaf and nothing is left needing a descendant rule. The exclusion is the guard
against the shape that once turned 20 genuine undriven members into 168 driven ones, and relaxing it
would have traded a false `unused` for a false `driven` — the worse direction.

### Resolution — step 1 shipped 2026-09-03

- **`Converter/Ir/MemberExpansion.cs`** — ONE shared classifier for "how far does this member open",
  replacing three copies of the decision (`SignalInventory.CollectLeaves`,
  `ProjectUsageGraph.CollectLeafPaths`, `InterfaceCheckRunner.Walk`). Decision order, first match
  wins: inlined body → block name (multi-instance) → IEC instance → elementary → `Array[…] of`
  (one aggregate leaf) → **resolve the named type through `TagTypeRegistry` (the fix)** → versioned
  instruction/library instance → **opaque, which gates**.
- **The multi-instance discriminator reads the `BLOCK <KIND> <Name>` header line, never
  `TagTypeRegistry._fbInterfaces`** — that index is built with `ParseBlockWithoutSidecar`, which
  throws on any file carrying a `SIDECAR` section and has the throw swallowed, so every RE-EXPORTED
  FB is silently missing from it. Keying there would make a multi-instance of a round-tripped block
  opaque: a false gate, for exactly the corpora this repair serves. Pinned by a test.
- **`signal-set` now gates on `partial` for TWO causes, reported separately**: a WARNING means a FILE
  could not be read; an OPAQUE MEMBER means a MEMBER'S TYPE could not be opened so its leaves are
  missing from a set that reads complete. The opaque set is collected from the UNFILTERED entries, so
  `--type`/`--direction` cannot suppress the gate, and `opaqueMembers[{path,datatype,reason}]` sits
  beside `partial` in the JSON so a generator can say WHICH member it may not trust. The reason names
  the search root, the file count, and that the scan is top-directory-only.
- **Two branches were added beyond the design** while proving the change a no-op on
  `ir/test-project001`, which declares `InterfaceId : HW_ANY` and `MbServer : MB_SERVER VERSION 5.3`
  with no inlined body: the S7 system identifier types joined the elementary list (they are scalar
  aliases with no members), and a `VERSION`-carrying declaration terminates as an instruction/library
  instance. Without them the gate fires on a healthy committed corpus — and worse, on a type whose
  definition lives in TIA's libraries and can therefore NEVER be supplied, i.e. a gate nobody can
  clear.
- **Invariance:** every UDT-typed interface member in the committed corpus is inlined, so the new
  branch cannot execute against committed data. That is checked, not argued —
  `SignalInventoryTests` sweeps the corpus for opaque leaves with a stated denominator, and
  `InterfaceCheckTests.RealCorpus_…_SoCommittedDataCannotExerciseTheCrossFileDescent` states the
  same limit from the other side.

**Still open, filed separately: FI-91** (`undriven-scan` and `cross-check` carry the identical
`NestedMembers`-only condition and were deliberately left alone) and **FI-92**
(`TagTypeRegistry`'s swallowed sidecar throw).

### Why it matters beyond one project

Two consumers read this graph. `harness-binding` is the one that fails visibly — it emits a scaffold
with nothing in it. `cross-check` reads the same `ProjectUsageGraph`, so a multi-writer or
undriven-signal question asked about one of these members gets the same confident silence, and there
the answer looks like a clean result rather than an empty one.

**Regression test to write with the fix:** a block whose principal interface is a UDT-typed STATIC
member referenced from many networks must report that member's leaves with their real directions and
writer sites — with a control on a UDT-typed member of the same shape that IS already expanding, so
the fix cannot be "expand everything" and cannot regress the working case.

## FI-89 — the loop MEASURES the scan period, reports that it disagrees with the constant, and no gate acts on it

**Measured 2026-09-02.** A wave reported, in its own result file:

```
scanPeriod: { measured: true, millisecondsPerScan: 1.5508, scans: 230671,
              windowSeconds: 357.726, compiledConstantMs: 24.931,
              deltaFraction: -0.9378, disagreesWithConstant: true }
```

**The harness knew the constant was 16x wrong for the program it had just run, said so, and nothing
consumed that.** `SubmissionGate.cs` computes gate 1b's whole band from the constant:

```csharp
var scenarioScans = (int)Math.Ceiling(endMs / WireTiming.ScanPeriodMs);
var allowed = (int)Math.Ceiling(scenarioScans * BackstopMargin) + BackstopFixedOverheadScans;
```

with `WireTiming.ScanPeriodMs` a `const double`. For a 55 s scenario the gate's band is about
[2206, 3809] scans; against the period actually measured on that build the scenario needs ~35,500.
**So gate 1b would REFUSE a correctly-sized backstop and ADMIT one roughly fifteen times too short** —
the spurious-`TIMED-OUT` direction the gate exists to prevent, and the constant's own documentation
names that as the reason it takes the pessimistic figure.

### 🔴 THIS IS NOT AN UNDISCOVERED HAZARD. IT IS A DOCUMENTED ONE COMING DUE, WITH NOTHING TO CATCH IT

`WireTiming.ScanPeriodMs`'s own summary is emphatic and was right:

> *"IT IS A PROPERTY OF THE PROGRAM, NOT OF THE CONTROLLER, AND THAT HAS ALREADY BEEN GOT WRONG
> ONCE... **Re-measure this constant whenever the program under test changes materially**; nothing
> about the CPU fixes it, and no figure taken against a different program may be substituted here."*

The obligation is stated, the failure mode is stated, and the enforcement is a **comment**. The
current value was measured against one program; a materially different one now runs, and the only
thing that noticed was the loop's own telemetry, which no gate reads.

**A second, smaller defect in the same place.** Gate 1b renders the constant to the reader as
*"{scenarioScans} scans at the **measured** {WireTiming.ScanPeriodMs} ms period"*. It is not
measured — it is compiled in. That is precisely the confusion the constant's docs record as having
already happened once ("was quoted as a rig fact"), reproduced by the gate's own message.

### The fix is not "update the constant", and that is the interesting part

Re-pointing the constant re-judges **every submission already admitted against it**. On the day this
was found, one slot's vectors were passing gate 1b with bounds sized to the old value; moving it
would have refused them mid-campaign. So the repair has to be a migration, not an edit:

1. **Carry the period as submission DATA, not as a compiled constant** — derived per program and
   provenanced through `harness-gate derive` like `map` and `deployment`, so gate 0c can tell a
   measured one from a typed one, and a stale one fails closed instead of silently converting.
2. **Have the loop's own `disagreesWithConstant` reach a gate.** Today it is printed and dropped. A
   run whose measured period disagrees with the one its bounds were validated against should say so
   where the verdict is, not only in telemetry.
3. Until either exists, gate 1b's pass means "the backstop is consistent with a figure from another
   program", and the honest place for that sentence is the gate's own output.

---

## FI-90 — a submission may carry MANY enumerations and exactly ONE model, so a multi-subject campaign cannot pass gate 4

**Found 2026-09-02**, building a second conformance slot for a second block in the same campaign.

`SubmissionDocument` supports a multi-subject campaign on the enumeration side and only there:

| field | shape | consequence |
|---|---|---|
| `Enumeration` | one | the single-subject shape, still correct for a single-subject campaign |
| `Enumerations` | **`List<EnumerationDocument>`** | one per subject; `AssertionEnumerationSet` resolves citations and gate 3j reports a denominator **per subject, never summed** |
| `Model` | **`ModelDocument?` — one** | *(no `Models`, and no per-vector model reference anywhere)* |

`SubmissionGate.Fidelity(vectors, fidelity)` takes that single declaration and set-differences
**every** vector's `assertedBehaviours` against **its** `Represents`. So the moment a submission
carries vectors for two subjects, each modelled by its own stimulus block with its own fidelity
declaration, gate 4 refuses every vector of whichever subject did not supply the model — not
because the vectors are wrong, but because there is nowhere to put the second declaration.

### Why this is the same defect the `Enumerations` list was added to fix, one artifact over

`Enumerations`' own doc comment records the reasoning:

> *"Until this existed a submission could hold exactly one, so a campaign with a valve enumeration
> and a vessel enumeration had one option: merge them. After a merge a citation to a clause both
> files declare is answered by whichever entry survived, and the denominator gate 3 reports is the
> union of two denominators and therefore neither."*

Every word of that transfers to the model. A merged `represents` set licenses each subject's
vectors to assert behaviours **the other subject's model** claims, which is exactly the
self-issued-licence failure gate 4b exists to prevent — and it would do so silently, since gate 4's
pass is an exact string set difference with no notion of which model a behaviour came from.

### It is not currently blocking, and that is worth stating precisely

The sound workaround is **one submission per subject**, and it is not merely adequate — it is
better on two counts:

1. `scenarioEndInput` is also one string for the whole submission, resolved per vector by name. Two
   slots have two differently-named end inputs, so a merged submission bounds one slot **by a flat
   ceiling** (`byCeilingOnly`) while the other gets its own per-scenario bound. Split, each slot
   gets the stronger per-scenario check.
2. Coverage is already reported per subject and never summed, so nothing is lost by splitting.

**So the item is not "we are blocked".** It is that the asymmetry is undocumented, and the shape a
reader would reach for first — one campaign, one submission, two subjects — fails at gate 4 with a
message about asserted behaviours that does not mention the real cause. The cheap fix is a named
refusal: if the vectors span more than one subject and only one model is declared, say *that*,
rather than reporting a fidelity excess.

## FI-91 — `undriven-scan` and `cross-check` still expand only INLINED members, so they now disagree with `signal-set` about one corpus

**Split out of FI-88 on 2026-09-03, deliberately not fixed with it.** FI-88's repair taught
`SignalInventory` to open a member whose type is NAMED rather than inlined. Two walks in
`ProjectUsageGraph` carry the identical `NestedMembers`-only condition and were left exactly as they
were:

- `CollectInstanceLeafPaths` — every leaf of every INSTANCE DB;
- `CollectMultiInstanceLeafPaths` — every leaf of every MULTI-INSTANCE static.

Both stop at a member with no inlined body and record it as one leaf.

### What that costs, concretely

On a `.ir` an authoring pipeline wrote (the FI-88 shape — a UDT-typed `STATIC` carrying the block's
whole caller-visible interface, with the type in its own `TYPE` file):

| command | what it reports about that interface |
|---|---|
| `signal-set` | its real leaves, with directions and writer sites (fixed) |
| `undriven-scan` | **the whole interface as ONE member, undriven** — a single false row standing for N real ones |
| `cross-check` | **cannot see the members at all** — no multi-writer, no dead-wiring question can be asked about them |

🔴 **The two halves of one project now answer the same question differently**, which is the exact
failure mode `cross-check` and `undriven-scan` contradicting each other over one corpus made findable
in the first place. It is a SECOND defect with its own blast radius, not a loose end of the first:
`undriven-scan`'s false row is the "a live member reads as dead" direction, and deleting on that
advice is what FI-53 records as having nearly removed a plant's entire weighing path.

**Cost to fix: about six lines**, now that `MemberExpansion.Classify` exists — both methods take the
classifier instead of testing `NestedMembers` themselves, exactly as `SignalInventory.CollectLeaves`
now does. What it needs beyond that is the same invariance proof FI-88 got: those two walks feed
`undriven-scan`, `cross-check` and `trace`, and a change to what counts as a leaf changes every row
they emit. Held out of FI-88's step 1 so the two blast radii could be measured separately.

## FI-92 — `TagTypeRegistry` silently drops every RE-EXPORTED FB from its interface index

**Found while building FI-88's multi-instance discriminator, 2026-09-03.**

`TagTypeRegistry.FromFiles` indexes an FB's interface with:

```csharp
else if (text.StartsWith("BLOCK FB ", StringComparison.Ordinal))
{
    var fb = IrParser.ParseBlockWithoutSidecar(text);   // <- unconditional
    fbInterfaces[fb.Name] = ...
}
```

and `ParseBlockWithoutSidecar` **throws on any file carrying a `SIDECAR` section** — by design, and
loudly, so that real round-trip data cannot be silently discarded in favour of synthesis. The throw
is then caught by the best-effort `catch (IrFormatException)` a few lines below and discarded.

**So the FB index contains exactly the blocks that have NOT been round-tripped through TIA, and
nothing says so.** Every re-exported FB is absent. The registry's own `Resolve` fallback — "an
instance DB's member tree is its FB's interface, and the FB is the source of truth for it", added
2026-08-24 after TIA rejected a block with 12 compile errors over a mis-inferred `SrcType` — is
therefore silently unavailable for precisely the corpora that HAVE been exported.

**The fix is one branch**, and `IrParser.HasSidecarSection` already exists for it — the sibling call
sites in `SignalInventory` and `InterfaceCheckRunner` both use it:

```csharp
var fb = IrParser.HasSidecarSection(text)
    ? IrParser.ParseBlock(text).Block
    : IrParser.ParseBlockWithoutSidecar(text);
```

Not done under FI-88 because FI-88 needed only a BLOCK-NAME set, which it now reads off the
`BLOCK <KIND> <Name>` header line — cheaper, and immune to this whichever way it is resolved. That
choice is pinned by `SignalSetUdtExpansionTests.MultiInstanceOfASidecarCarryingBlock_IsStillNotOpaque`,
which fails the day anyone re-keys the discriminator on `_fbInterfaces`.

---

## FI-93 — nine tests have been red since the reference corpus was widened, and a red suite is a disabled suite

**Measured 2026-09-03**, while establishing a baseline before merging an unrelated converter change.

`dotnet test src/converter/converter.sln` reports **9 failed / 1801 passed**, and the nine have
nothing to do with whatever change is in flight:

```
NeighbourTests      × 4
ServedAreaTests     × 4
ReachableStateTests × 1
```

**One cause, and it is entirely mechanical.** The committed reference project was widened from a
37-register Modbus window to 1024:

```
ir/test-project001/FB_Comms_ModbusServer.ir:31   MB_HOLD_REG := P#M1000.0 WORD 1024
ir/test-project001/FB_Comms_ModbusServer.ir:39   constant P#M1000.0 WORD 1024 = 22 Any
```

while the fixtures still assert the old number:

```
Converter.Tests/ServedAreaTests.cs:147   Assert.Equal(37, report.Registers);
Converter.Tests/ServedAreaTests.cs:164   Assert.Equal("P#M1000.0 WORD 37", report.ReadableText);
```

The tests are **correct about what they check and wrong about the value**; the corpus moved and they
did not move with it.

### Why this is worth an entry rather than a quiet fix

**A suite that is permanently red cannot report a regression.** Every one of these nine is a
*corpus-sweep* test — the kind that exists to notice when committed data drifts away from what the
tools assume. That is precisely the class this repo leans on hardest: `signal-set`'s no-op proof,
`interface-check`'s inlining tripwire and `drift-check`'s whole purpose are all corpus sweeps. Nine
of them have been reporting failure for long enough that the correct baseline is now *"the same nine
that fail anyway"* — which is a sentence that has to be said out loud before every merge, and which
silently widens to ten the first time someone breaks a tenth.

This is the same hazard the repo already recorded as **"a warning is not a gate — if a check detects
it and only warns, it gets skimmed."** A permanently-failing test is weaker than a warning: it has
been pre-skimmed.

### The fix, and the one judgement in it

Mechanically it is a fixture update: 37 → 1024 in the four `ServedAreaTests` expectations, and the
derived counts in `NeighbourTests` and `ReachableStateTests`. **The judgement is whether the widened
corpus is the intended long-term reference**, because these tests double as the record of what the
reference project *is*. Re-pointing them ratifies the widening; leaving them red does not preserve
the old value, it only stops anyone finding out. Decide that, then update in one commit — and do not
land any other change in it, so the diff reads as a ratification and not as a repair.

---

## FI-94 — `claim --allocate` hands back a number you already hold, for a DIFFERENT object, and calls it CLAIMED

**Measured 2026-09-03**, reserving four block numbers for one new harness slot.

A slot needs several numbers at once — a stimulus FB, its instance DB, a second instance DB for the
block under test, and a slot FC. Allocating them is four calls differing only in `--type` and
`--purpose`. The first call of each `--type` allocates correctly. **The second call of the same
`--type` returns the number the first one got:**

```
converter claim … --kind block-number --allocate --type DB --purpose "…Stim - instance DB of …"
  CLAIMED   claimed block-number 'DB9003' …

converter claim … --kind block-number --allocate --type DB --purpose "…UnderTest - the block under test"
  CLAIMED   already claimed by agent '<same>' at 2026-09-03T00:21:27Z - no change
    value   DB9003
```

**Exit 0. The word `CLAIMED`.** Two different objects, one number, and the only thing in the request
that distinguished them — `--purpose` — was not consulted.

### Why this is worse than an ordinary bug

The registry exists to stop exactly this. Its exit contract is *"0 acquired · 1 REFUSED — pick
another and re-claim · 2 NOTHING WAS DECIDED"*, and a caller written to that contract reads 0 as
"this number is mine and it is distinct from the last one I was given". Here 0 means "you already
have one of these", which is a different sentence for a caller holding two objects.

Downstream the collision is silent for a long time: two IR files carry the same `NUMBER`, and the
first thing that notices is TIA, at import, replacing one object with the other **by number**. On
the run that found this, the second bad allocation was worse still — the FC request came back with
**the number of the copy layer that is currently deployed on the rig.**

It survived only because the caller cross-checked the returned numbers against the corpus by hand
before writing anything. Nothing in the tool prompted that check.

### The shape of the fix

The intent behind the current behaviour is almost certainly retry-safety: a re-run of the same
allocation should not burn a second number. That is worth keeping, but the identity of an allocation
is **(agent, kind, type, purpose)** and not (agent, kind, type) — the purpose is the only field that
says *which object* is being reserved.

1. `--allocate` should treat a candidate held by **anybody, including the caller under a different
   purpose**, as taken, and move to the next free number.
2. An exact repeat — same agent, same type, same purpose — keeps today's idempotent behaviour, which
   is the retry case and is genuinely useful.
3. If the tool will not consult the purpose, then returning an existing claim must **not** be
   reported as `CLAIMED` at exit 0. It is a refusal to allocate, and the caller has to be able to see
   the difference without diffing the value against its own records.

Until then the safe pattern, and the one now in use: **verify the band against the corpus, then claim
each number with an explicit `--value`.** Note that an explicit in-band `--value` is *"accepted and
announced as accepted, not verified"*, so the verification is the caller's either way — which is the
honest reading of the whole registry today.

---

## FI-95 — the assertion enumerator cannot produce a large enumeration at all: `Write` only, one pass, no Edit

**Measured 2026-09-03.** Four enumerations were dispatched in parallel against one program. Two
completed. **The two largest subjects both died with `max_output_tokens`**, and both agents' final
words were the same realisation:

> *"Edit is disabled, so the file must be written whole in one call. Rewriting it compactly."*
> *"Edit is unavailable, so the file must be written in one pass. Rewriting it complete and compact."*

Neither survived the rewrite. One left a preamble and **zero clauses**; the other left a file
declaring **56 assertions and containing 24**.

### This is a configuration consequence, not an agent mistake

`.claude/agents/assertion-enumerator.md` grants `Read, Grep, Glob, Write, Skill`. **`Bash` is denied
deliberately and correctly** — the fence that keeps the implementation out of a spec-side denominator
also removes every way to compute a SHA-256, which is why `harness-gate stamp` exists as a separate
step. But `Edit` is absent too, and nothing about the independence argument requires that. The
combination means **the entire artifact must fit in one model response**, and an enumeration's size
scales with the subject's clause count — so the constraint bites hardest exactly where the denominator
matters most.

### Why the failure is quiet in the worst case

Truncation mid-file is loud: the YAML is short and obviously incomplete. **The dangerous outcome is
the near-miss** — a file that parses, looks finished, and overstates itself in `denominator:`.

The good news, and it should be recorded as such: **`harness-gate stamp` catches it.** Exit 1, nothing
written, and the message names both numbers:

> *"the file declares 56 assertion(s) and this reader found 24. One of the two is wrong and neither
> can be assumed, so nothing is stamped."*

So an overstated denominator cannot become a false coverage figure. It makes the file unusable
instead, which is the right failure — but it is caught one artifact downstream of where it was made.

### Fixes, cheapest first

1. **Grant `Edit`.** It breaks no fence: the independence argument is about *what the enumerator may
   read* and *that it cannot compute an ID*, neither of which `Edit` touches. This alone removes the
   ceiling.
2. **Have the skill state a budget and the technique that meets it.** Both failed agents identified
   the same fix unprompted — a legend near the top mapping short keys to the observability and caveat
   paragraphs, cited per assertion instead of repeated. On a re-run with that instruction given up
   front it is a routine job. The skill should say so rather than leaving each agent to discover it
   at the point of failure.
3. **Make partial enumeration a first-class, declarable outcome.** A `clauses_not_yet_enumerated:`
   list with a reason per clause is an honest, usable artifact; a silently-short file is not. Today
   the contract offers no way to say "this denominator is incomplete and here is exactly how", so an
   agent that cannot fit the whole job has no correct move available to it.

---

## FI-96 — an enumeration that parameterises an observation over a CHANNEL cannot join to a flat binding

**Measured 2026-09-03**, wiring a new conformance slot to an enumeration written by a third party.

Gate 3h joins a vector's citation to an observation by **string equality on `specName`**, and a
`specName` must resolve to **exactly one** `resultSources` row — two rows sharing a name makes
`ResultRegisterOf` ambiguous.

Some blocks carry **repeated channels inside a single instance**: four request inputs and four
granted flags on one block, one per asker. An enumerator working from the specification writes the
obligation once and parameterises it, because that is what the specification says:

```
required_observations:
  - "the per-asker granted flag, for this asker"          # cited by 8 assertions
  - "the per-asker request input, for this asker"         # cited by 8
  - "the per-asker granted flag, for all four positions"  # cited by 4
```

The binding, correctly, carries **four separate rows** — one per channel, each with its own tag and
its own mirror register. **One phrase, four rows. The join cannot be made.** Setting the name on all
four is ambiguous; setting it on one silently tests one channel and reads as if it tested the
obligation.

On the block that surfaced this, **seven of ten required observations are channel-parameterised, two
are marked UNNAMED IN THE SPEC by the enumerator, and exactly one is singular and joinable.** A
sibling block's enumerator independently flagged the same axis as its own biggest open question.

### Why this is not the "unnamed member" problem, and why that matters

It is easy to file this under the finding that dominates this program — specs describing behaviour
without naming interface members — and it is **not that**. Here the enumeration is *right*, the
binding is *right*, and they still cannot meet. Renaming members would not fix it; writing the
enumeration four times would work but would inflate the denominator with four near-identical
assertions and destroy the property that makes a spec-derived enumeration worth having.

### What the fix has to preserve

The instance axis already exists in the contract as a **multiplier on coverage units** — an
enumeration declares `declared_instances` and reports `assertions × instances`. So the model knows
the axis is there; only the *join* is flat. Three shapes worth weighing:

1. **A channel-qualified `specName`** — the binding names `<phrase> [W]`, `<phrase> [X]`, … and the
   vector cites the phrase plus the channel it drove. Keeps one row per name, keeps one assertion.
   Needs a qualifier the gate understands rather than a naming convention nobody enforces.
2. **A `serves`-style many-to-one map on observations.** The slot binding already carries `serves`
   for the *slot* id — "the specification slot ids this ONE slot serves" — which is the same problem
   solved one level up. An observation-level equivalent would be a small, precedented addition.
3. **Rule that a channelled obligation is tested on one declared channel**, and make the binding say
   *which*, so a pass reads "asker W was tested" and never "the obligation was tested". Cheapest, and
   the only one of the three that is honest without any code change — but it must be **stated on the
   artifact**, because the difference between those two sentences is the whole value of the gate.

Until one of these exists, a block with repeated channels can bind only its singular observations,
and the coverage it can earn is bounded by that rather than by its specification or its logic.

---

## FI-97 — the "start echo" is a loopback of the client's own write, so a slot that never ran reads as one that ran

**Measured 2026-09-03** on a deployed copy layer, while adding a fourth conformance slot.

The generated copy layer emits, per slot, two adjacent rungs:

```
NETWORK n   "Start bool - slot <S>"   COIL  <model>.Stim.Start := HX_<S>_Start
NETWORK n+1 "Start echo - slot <S>"   SCOIL HX_<S>_Ran        := <model>.Stim.Start
```

**Both are inside the copy layer.** The echo latches from the bit the copy layer wrote one rung
earlier, from the client's own mirror register. Nothing between them is the slot.

So `HX_<S>_Ran` asserts when **the client wrote the start register and the copy layer executed** —
and says nothing whatever about whether the slot's FC ran. A slot FC that is

- never called from the cyclic OB,
- called *after* the copy layer,
- or present, called, and internally doing nothing,

produces a mirror **indistinguishable** from a slot that ran correctly.

### Why the name is the dangerous part

`Ran` is the only signal in the mirror that looks like a liveness detector, and a reader reaches for
it for exactly that. The generated network title — *"Start echo"* — reinforces it. What it actually
carries is *"the copy layer saw your command"*, which is a loopback, and a loopback through the one
component that is guaranteed to be running because it is what publishes the mirror.

**This is not hypothetical.** The call-site obligation on a generated slot FC is documented as having
already cost a wave and three hours when a slot deployed green and never ran — and this echo was
present and reading normally throughout.

### What would actually detect it

The echo has to be latched by something **only the slot can drive**. The cheapest honest version is a
rung inside the slot FC, or inside the stimulus head, that sets a bit the copy layer then publishes —
so the chain is `slot ran → bit set → mirror`, with no path from the client's write to the bit. That
costs one static and one rung per slot and turns a decorative register into the one thing the mirror
currently cannot say.

Failing that, **rename it.** `HX_<S>_CommandSeen` is honest about what it carries, and would stop a
reader treating it as evidence of execution. A register that cannot mean what its name says is worse
than an absent one, because absence prompts a question and a wrong name closes it.

### Blast radius

Every slot in every wave set, on every deployment. No existing result is *wrong* because of it — the
slots in question did run, and their verdicts rest on their own observations — but **no existing
result is evidenced against this failure mode either**, and several reports quote the echo as if it
were.

---

## FI-98 — a wave can come back clean without the scenario ever opening, and nothing in the mirror can tell

**Found 2026-09-03** by a model-fidelity declarer reading a generated stimulus head, and confirmed
against `StimShellGenerator` rather than one job's IR — so this is a property of **every** head the
generator emits, not of one block.

Two independent gaps, same shape, and they compose:

### (a) A degenerate scenario window produces a clean, complete-looking run

The shell derives its scenario clock from the phase boundaries
(`StimShellGenerator.cs:185-186`):

```
MOVE(EN := RunT <  HeadEnd, IN := T#0S)                   => ScenT
SUB (EN := RunT >= HeadEnd, IN1 := RunT, IN2 := HeadEnd)  => ScenT
```

If a vector's commanded scenario end coincides with the head boundary — a zero `EndAt`, or a zero
profile selector — **the scenario interval is empty and `InScenario` is never true**. Every drive is
gated on it, so nothing is presented; every inert-rest register therefore reads exactly as declared;
and the completion bit still latches off the clock. The run reports **ran, settled, inert, complete**.

🔴 **It fails in the direction of looking successful**, which is the direction a gate cannot afford.

### (b) There is no held record that the observation window ever opened

`StimShellGenerator.cs:248` emits the arm term as a **live coil**:

```
COIL Stim.Armed := InScenario AND ScenT >= Stim.ArmAt AND ScenT < Stim.ArmUntil
```

Nothing latches it. So a window placed outside the interval where anything happens returns **false on
every `*Raised` bit** — which is indistinguishable, register for register, from *"the block was
exercised and did nothing"*. On a `NEVER`-form vector that is a **PASS**, and a pass earned by never
looking.

This is the same defect family as **FI-97**, where the start echo latches from the copy layer's own
write and so cannot detect a slot that never ran. Together the mirror carries three things a reader
takes as evidence of execution — the start echo, an inert-clean rest, and a latched completion bit —
and **not one of them distinguishes a healthy run from a run that never happened.**

### The fix is one bit, and it is the same bit in both cases

**Latch the arm window.** A held `ArmedEver`, set from `Stim.Armed` and cleared only by the head
cleardown, costs one static and one rung in the generated shell and makes every `atNoPoint` pass
falsifiable: a green with `ArmedEver` false is not a pass, it is a wave that did not look. The same
latch answers (a), because a window that never opened proves the scenario never opened.

**Then gate on it.** A result package whose arm latch is false should refuse to report `Held` on any
observation gated by that window. Today the loop would report a full green.

### What this does and does not say about existing results

It does **not** invalidate them: the waves run so far drove real stimuli and their `*Raised` bits went
high, which is positive evidence that the windows did open. What it says is that **none of those runs
was evidenced against this failure mode** — the passes are believed on the strength of other rows
being non-trivially true, not because anything checked. A submission whose vectors are all `NEVER`
form would have no such corroboration at all.
