# 13 — Data & IP Boundary

**Status: DRAFT — needs a decision (yours, possibly your employer's) before real project data flows to any AI. Ties to risk R-08.**

## The question

Claude Code sends repo content and command output to Anthropic's API. Exported PLC projects can contain identifying names, machine know-how, network details, and NDA-covered material. What is allowed to leave the engineering PC?

## Proposed tiers

| Tier | Content | AI access |
|------|---------|-----------|
| Green | This doc suite, tooling code, IR spec, pattern library, the purpose-built reference project | Yes |
| Amber | Real project logic with identifying data (identifying names in comments, IP addresses, site names) | Only after sanitization, or with explicit per-project approval |
| Red | Safety program content; anything a site contract forbids sharing | Never — also enforced in tooling for safety content |

## Decisions needed (record as ADR-0003 when made)

1. Does workplace IT/management policy permit cloud AI on project data at all? In what form (commercial terms, data retention)?
2. Is a sanitization pass required for Amber (strip/alias restricted identifiers, IPs, site names in comments and tag names)? If so it becomes a converter feature.
3. Per-project: do any active NDAs categorically exclude their projects?

## Interim rule (until decided)

Only Green-tier content goes near Claude Code. The reference project is purpose-built and contains nothing identifying — all development through S6 can proceed on Green data alone, so this decision does not block early stages.

## Per-project approvals (Amber, pending ADR-0003)

- **2026-07-10 — "JOB9002 - Tom White Waste" (scratch copy).** Private engineering project, Amber-tier
  (identifying identifying name; not sanitized). Explicit per-project approval given by the
  project owner. No sanitization pass applied. Per-project, not blanket — re-confirm before
  using this project for anything beyond what's listed below. The project contains two linked
  PLC stations, `station_1/JOB9001_PLC` and `station_2/JOB9002_PLC` (`PLCToPLCComs`/`LSNTP_Server`
  between them) — confirmed by the project owner to be the same site (two phases/PLCs of Tom
  White Waste), so both are covered by this approval, not just the station matching the
  project's own name.
  - **Scope, as approved:**
    1. A-01/A-02 Openness verification spikes (`docs/notes/stage-gates.md` S0) — done.
    2. **2026-07-10, extended:** S1 IR/converter design work — grounding ADR-0001 and
       `ir/SPEC.md` against real SimaticML structure (`docs/adr/adr-0001-ir-format.md`).
    3. **2026-07-11 through 2026-07-14, extended further (record backfilled 2026-07-14 — this
       work was already done and separately confirmed with the project owner at the time; this
       entry just catches the doc up):** the same S1 IR/converter design-work pattern, continued
       at much larger scale as new instructions and block shapes were needed — full
       instruction-coverage grounding sweeps across every remaining block on *both* PLC stations
       (reading real block content to identify unsupported LAD constructs, cross-referencing
       against `FlgNetParser.SupportedPartNames`; basis for `docs/notes/stage-gates.md`'s whole
       Phase 2 tier sequence and `ir/SPEC.md`'s own per-instruction grounding citations), and the
       `PlantAutoControl` round-trip plan's bulk export/sanitize/import of `PlantAutoControl`'s full real
       dependency closure (8 dependency FBs — `MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/
       `FilterUnitSystem`/`MotorFwdRevSystem`/`AirStar`/`MotorVSDSystem`/`TomraControlSystem` — plus 26 real DB/
       tag-table roots) into `SampleProject`, explicitly confirmed by the project owner ahead of
       that work: "recreate `PlantAutoControl`'s tag/DB dependencies by bulk export+sanitize+import of
       the real DBs/tag tables from `JOB9002`... go all the way to `PlantAutoControl` itself in this
       pass, not stop at the 8 FBs." Same genericization rule applied throughout (sanitized before
       import into `SampleProject`; `sanitization/` maps gitignored, never committed).
  - **Rule for this and any future scope:** example content pulled from JOB9002 into a *committed*
    doc (`ir/SPEC.md`, ADRs, anything under `docs/`) must be genericized — invented tag/instance
    names, never copied verbatim from the real project. Structural findings (XML element shapes,
    schema) are not identifying and may be documented directly; specific values
    (tag names, equipment names, comment text) are not, and get invented replacements.
  - **Clarified 2026-07-11, project owner's call:** the scratch copy's own local filesystem path
    (needed for `openness-cli`'s cold-open `.apNN` argument) is not covered by the genericization
    rule above — the project name itself is already committed unredacted in this doc and in
    `docs/notes/stage-gates.md`, so the path adds no new identifying information. Recorded in
    `docs/notes/openness-quirks.md` for reuse. Tag/comment/equipment *content* is still governed
    by the rule above.
  - **Clarified 2026-07-11, project owner's call:** a UDT/structured-member's own nested field
    names (e.g. a motor-IO UDT's `InHand`/`Running`/`Fault` members, or a timer instance's
    `PT`/`ET`/`IN`/`Q`) are treated as structural, not covered by the genericization rule — they
    are generic controls-engineering vocabulary describing the reusable *type's* shape, not
    site-specific identifying data the way a DB name, top-level member name, or comment is. Only
    the DB/member/FB names that reference the type still get invented replacements.
  - JOB9002 is a design-time aid only — it does not become the committed `tests/golden/` corpus;
    that still needs a purpose-built Green project (`docs/notes/stage-gates.md`, S1 overall plan
    item 8).

- **2026-07-10 — reference project (`ir/reference/`, `simatic-ml/reference/`) seeded from
  sanitized data, under a separate, private approval not detailed here.** The committed content's
  structural shapes (wiring topology, instruction types, slice/array addressing) originate from
  real production PLC data; every tag path, block name, and comment is invented — nothing
  site- or site-identifying is recorded in this repo. The mapping from real to invented
  values is intentionally not committed anywhere (`.gitignore`: `sanitization/`) and isn't
  reconstructable from what's here. This entry exists so the corpus's Green-tier claim
  ("purpose-built, contains nothing identifying") has a recorded basis rather than none — see
  `tests/golden/README.md` for what was actually done to it.
