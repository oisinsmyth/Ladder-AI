# 15 — Future Ideas

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
- **Status:** Under debate
- **Raised:** 2026-07-16 · **Source:** `AITODO.md` "Carried forward"; S6 unlock — pattern admission criterion 3 accepted on partial evidence (drafted with prior knowledge of the real answer, not blind), recorded as an open gap rather than silently closed.
**Merits:** A genuinely blind drafted instance is the missing proof that the pattern generalizes; worth having before leaning on this pattern heavily in S6 generation. The earlier blind attempt against JOB9002's station_1 `PlantAutoControl` also surfaced a structurally different case (VSD, edge-detected fault-reset) the pattern doesn't document yet — closing the gap likely improves the pattern itself.
**Costs / risks:** Needs a suitable blind target: real, untouched, and within the recorded data-boundary approval. Low tooling cost; the cost is finding honest test material.
**Dependencies:** None — S6 is open; this was explicitly judged "worth closing before leaning on this pattern heavily, not before S6 can start."
**Verdict / revisit trigger:** Nearest-term entry in this doc. Live tracking stays in `AITODO.md`; this entry records the analysis and will take the verdict when the gap is closed or consciously waived.
