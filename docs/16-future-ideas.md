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


## Index — all 95 entries

**The denominator is 95 entries, FI-1 … FI-103.** The numbers 55–60, 74 and 75 are DELIBERATE HOLES: that work was written up in other documents, and back-filling entries from another document's summary is the doc-to-doc citation this register exists to record. They are not missing and must not be reused.

Status is **derived mechanically** from each entry's own status bullet, inline status, or Verdict/Resolution. Where no recognised form yields one the row says **UNCLASSIFIED** (27 of 95) — a worklist, not a guess. Reading a verdict out of an entry's prose would be the very failure this page keeps recording.

| id | title | status | in |
|---|---|---|---|
| FI-01 | Pattern testing hook (S9 sim harness) | Parked | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-02 | Main (OB1) round-trip support | Parked | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-03 | Modbus multi-instance form | Parked | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-04 | WAIT/Jump converter support | Rejected (not needed — project owner's call, 2026-07… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-05 | Close chained-permissive-enable's blind-draft gap | Deferred (owner ruling, 2026-07-17 — docs/notes/owne… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-06 | Constructed edge-detection as a fourth pattern kind | Under debate | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-07 | openness-cli cleanup: a Portal-instance janitor | Parked (owner, 2026-07-16) — "trust is user-side" fo… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-08 | Engineer-side approval path for proposed tags | Under debate | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-09 | Convention rules Phase 2: mechanize more of the remaining ~42 | Implemented (partial) — ongoing, no longer Parked (c… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-10 | HMI-importable alarm exports from S5 | Parked | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-11 | Presentation-bundling tool for the final gate | Raised | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-12 | Persistent Portal session for the import–compile inner loop | Raised | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-13 | Static pre-flight gate before any Portal round trip | Accepted (owner-directed build, 2026-07-16 — tooling… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-14 | Compile-error playbook: known TIA errors → proven fixes | Accepted (owner-directed build, 2026-07-16 — docs-si… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-15 | Generated block digests for cheap structural context | Accepted (owner-directed build, 2026-07-16 — tooling… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-16 | Per-stage token and latency telemetry with budgets | Accepted (owner-directed, 2026-07-16 — convention on… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-17 | Explanation sidecars: cached AI block explanations, keyed to IR hash | Implemented (partial) — the helper + convention land… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-18 | HMI Interface creation skill (defines the PLC/HMI boundary) | Raised | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-19 | docs/evidence/ split: raw verbatim transcripts out of docs/notes/ | IMPLEMENTED 2026-07-18. The §3 verbatim blind-run tr… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-20 | Slim stage-gates.md to a status index, move narrative to per-stage evidence docs | IMPLEMENTED 2026-07-18. stage-gates.md is now a 43-l… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-21 | Library-candidate harvest-assist skill (run the reviewers, clean to exceptions) | Raised (2026-07-18; owner idea, explicitly "tomorrow… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-22 | Whole-project cross-check review mode (reference-graph analysis over ProjectIndex) | IMPLEMENTED 2026-07-20 as converter cross-check --pr… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-23 | explain-plc-block structural-fingerprint helper | IMPLEMENTED 2026-07-20 as converter digest --fingerp… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-24 | gen-architecture bookkeeping helpers (provenance + tag-status) | Implemented (partial) — 2026-07-18; the provenance-h… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-25 | review-functional forward-pass verdict tracer (AI binding → scripted decision tree) | IMPLEMENTED (v1) 2026-07-20 as converter trace --bin… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-26 | ir ↔ simatic-ml export-drift check | IMPLEMENTED 2026-07-20 as converter drift-check --pr… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-27 | Static Part flow-order / import-validity check in preflight | IMPLEMENTED 2026-07-20 as the flow-order check in co… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-28 | openness-cli portal-status: read-only Portal-process diagnostic | IMPLEMENTED 2026-07-20 as openness-cli portal-status… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-29 | Reuse-first duplicate-logic finder (digest-backed corpus query) | IMPLEMENTED 2026-07-20 as converter reuse-scan --pro… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-30 | S6 new-block target gap-hunter (REQ × tag-status × as-built cross-join) | IMPLEMENTED 2026-07-20 as converter target-scan --re… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-31 | Telemetry line-format validator (shape only, not authoring) | Parked (2026-07-20) — against gen-telemetry.md's own… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-32 | Replace IR with a restricted real programming language (Python/C# subset) | Under debate | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-33 | Authored per-block interface/object model with typed cross-references | Under debate | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-34 | Programmatic (parameterized) pattern library | Under debate | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-35 | converter alarm-scan + HMI alarm-list generation | Raised | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-36 | Per-instance interlock-completeness trace (review-functional's independent check) | Implemented (partial) 2026-08-05 — FI-36-min shipped… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-37 | Spec interlock explicitness: no ambiguous conjunctions, underspecified interlock = block… | Implemented 2026-08-05 — as skill rules, not tooling… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-38 | Interlock-safety disposition: surface ambiguity, don't silently under-constrain; dropped… | Implemented (partial) 2026-08-05 — as a skill rule, … | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-39 | converter candidate-scan: the mechanical floor under the spec-pipeline checks | Implemented 2026-08-05 — all five checks + FI-36-min… | [fi-01-39](future-ideas/fi-01-39.md) |
| FI-40 | a mechanical status check: retire the hand-restated-status drift class | Raised (2026-08-05, out of the audit fix wave — the … | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-41 | converter review: TAGTABLE files pass vacuously, including on C-001 and C-005 | Raised (2026-08-05, from a greenfield generation run… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-42 | four converter/CLI gaps that only appear when you BUILD a data landscape | Raised (2026-08-05, from a greenfield UDT/DB stage —… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-43 | openness-cli: no delete --type, and no way to update an object in place | Raised (2026-08-05, owner-directed, from a live gree… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-44 | "empty is not clean": the mechanical floor exits 0 when it examined nothing | IMPLEMENTED 2026-08-05 — all three paths closed the … | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-45 | the checks cannot parse two shapes that are ordinary, not exotic | IMPLEMENTED 2026-08-05 — all three items closed the … | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-46 | three residuals from the FI-44/FI-45 wave, found but deliberately not fixed | Raised (2026-08-05). Each was found while fixing som… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-47 |  RETAIN on a UDT member parses clean and is silently dropped crossing to XML | Raised (2026-08-06). Found while verifying a data-st… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-48 | to-xml can reorder coils within a network, and coil order is semantic | Raised (2026-08-07). Found by a coding agent doing a… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-49 | a member name shared across two types is an unenforced contract | Raised (2026-08-07), by the agent that created the s… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-50 | undriven-scan could not see a multi-instance, which is the house interface style | BUILT AND FIXED 2026-08-07, same day it was found. K… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-51 | an array subscript could only be expressed on the LAST component of a path | BUILT AND FIXED 2026-08-07. Found while asking why f… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-52 | the compile gate could return a false pass, and the warning was in the wrong place | BUILT AND FIXED 2026-08-07. Found by an agent that d… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-53 | the reference graph did not credit a read taken THROUGH an array element | BUILT AND FIXED 2026-08-07, hours after FI-51, and f… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-54 | HMI capability probe programme | Implemented (partial) — phase 1 landed 2026-08-07/08… | [fi-40-54](future-ideas/fi-40-54.md) |
| FI-61 | a rebuilt binary is refused by Openness silently, and every message we had blamed the wr… | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-62 | sanity-check enumerated blocks only, so a UDT could be inconsistent behind a green gate | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-63 | the first instance DB created after a project open got number 0, and a green compile hid… | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-64 | a PLC data type carrying a named-type member could not be read back at all | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-65 | an Openness manager: multi-agent Portal access, bulk transfer, and an import/export ledg… | Implemented (partial) 2026-08-07 — component 1 (clai… | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-66 | sanity-check reported a count its own documented remedy could not reduce | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-67 | cross-check could not be asked the one question a back-out must ask | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-68 | a relative --out failed with an exception that named a different problem entirely | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-69 | a statement-order violation in the IR blamed the network header | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-70 | nothing compared the IR on disk against what is actually in the controller | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-71 | to-xml guessed a member type and only warned about it, for the third time | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-72 | both converters wrote beside their input, and silently overwrote hand-authored IR | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-73 | an unknown --flag was treated as a FILENAME, and a stale Release build is how it surface… | UNCLASSIFIED | [fi-61-73](future-ideas/fi-61-73.md) |
| FI-76 | the map model: derive the block structure from the border, and build it in order-waves | Raised 2026-08-17 — design settled with the owner, r… | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-77 | a per-block compile reports the whole program's errors, and the count includes empty par… | Raised 2026-08-24 — measured on a real project, docu… | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-78 | converter diff can declare an insertion but not a deletion, so an add-and-remove change … | Raised 2026-08-25 — measured on a real change, NOT F… | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-79 | converter diff without --only returns exit 0 unconditionally, and exit 0 reads as a pass | Raised 2026-08-25 — confirmed from source, documente… | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-80 | the Normalizer's Access content key is EMPTY for a constant, so two different constants … | ✅ FIXED 2026-08-27, and the false PASS was DEMONSTRA… | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-81 | compile-all is not convergent: one pass can leave more inconsistent than it cleared | Raised 2026-08-27 — measured on a real project, NOT … | [fi-76-81](future-ideas/fi-76-81.md) |
| FI-82 | signal-set reports an IEC timer's own members as unused | Raised | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-83 | SidecarSynthesizer.ScopeFor has no CONSTANT case, so a block CONSTANT is emitted as a va… | Raised | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-84 | the readable IR cannot express an INTERLEAVED pair of instruction kinds | NARROWED 2026-09-02 — the headline above is overstat… | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-85 | converter diff --insert silently keeps only the LAST value when repeated | Raised | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-86 | a wave that RAN writes no result file, because the renderer throws after the run | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-87 | gate 0c's verdict depends on the CALLER'S WORKING DIRECTORY, silently | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-88 | a UDT-typed STATIC member used 251 times reports unused, and the binding scaffold goes b… | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-89 | the loop MEASURES the scan period, reports that it disagrees with the constant, and no g… | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-90 | a submission may carry MANY enumerations and exactly ONE model, so a multi-subject campa… | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-91 | undriven-scan and cross-check still expand only INLINED members, so they now disagree wi… | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-92 | TagTypeRegistry silently drops every RE-EXPORTED FB from its interface index | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-93 | nine tests have been red since the reference corpus was widened, and a red suite is a di… | UNCLASSIFIED | [fi-82-93](future-ideas/fi-82-93.md) |
| FI-94 | claim --allocate hands back a number you already hold, for a DIFFERENT object, and calls… | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-95 | the assertion enumerator cannot produce a large enumeration at all: Write only, one pass… | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-96 | an enumeration that parameterises an observation over a CHANNEL cannot join to a flat bi… | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-97 | the "start echo" is a loopback of the client's own write, so a slot that never ran reads… | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-98 | a wave can come back clean without the scenario ever opening, and nothing in the mirror … | Raised | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-99 | a subject that genuinely has NO bounds can never clear gate 3i, because "positively empt… | IMPLEMENTED 2026-09-03 — same day it was raised. enu… | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-100 | a conflict graph built from reachable-state overstates slot conflict, because a closure … | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-101 | the IR has no boolean FALSE, so "hold this input off" is inexpressible, and one of the t… | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-102 | an instance DB of a block that NESTS other FB instances cannot be created at all: import… | FIXED | [fi-94-103](future-ideas/fi-94-103.md) |
| FI-103 | two silent contract traps: diff --insert is last-wins, and a block-network claim cannot … | UNCLASSIFIED | [fi-94-103](future-ideas/fi-94-103.md) |

---

*Four hand-maintained status snapshots (two “Implementation status”, two “Prioritization”, 142 lines) were removed from this file on 2026-09-17. They restated what the entries themselves say and had drifted from them — the class FI-40 exists to retire. They were accurate when written and remain in git history; nothing was lost, and this line exists so a later reader looks there rather than assuming they never existed.*
