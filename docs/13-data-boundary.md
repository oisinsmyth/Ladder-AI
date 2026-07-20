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
  - **2026-07-14, S2 kickoff — extended to explanation work.** Project owner's own explicit
    instruction, mid-session: read and explain real JOB9002 blocks directly (not the sanitized
    reference-corpus equivalents), since sanitization strips exactly the specific real-world
    context — real tag names, comments, titles — that makes an explanation useful, which is the
    whole point of S2. Explanations happen in conversation, not committed to any file — the
    existing genericization rule above (invented names in *committed* content) still applies in
    full if any example from this work ever gets written into a doc.
  - **2026-07-14, S3 — extended to write/comment-generation activity.** Project owner's own
    explicit instruction, after two proofs against the Green-tier reference corpus: try S3's
    title/comment-generation capability against real JOB9002 content specifically (starting with
    `PlantAutoControl`). Scope: writing a block-level (and, where genuinely useful, network-level)
    title/comment into real JOB9002 blocks' own IR — using real tag names, matching how a real
    engineer would document real content — and importing the result back into JOB9002's own scratch
    copy, the same live-verification pipeline already proven against the reference corpus. JOB9002's
    own scratch copy is gitignored and never committed, so the real content this produces never
    leaves the local project file. The existing genericization rule above still applies in full to
    anything written *about* this work in `docs/notes/stage-gates.md`/`CHANGELOG.md` or any other
    committed doc — real tag names are fine to use in the real project's own IR/comment content
    itself, not in prose describing it here.
  - **2026-07-15 — extended to pattern-library extraction (S6 prerequisite work).** Project
    owner's own explicit choice, made mid-interview when asked which project should source the S6
    seed pattern library (`docs/07-pattern-library-spec.md`): use JOB9002. The spec's own admission
    criterion 1 requires each pattern be "instantiated at least once in reviewed, working logic" —
    JOB9002 already has strong candidates (e.g. `MotorDOL`, deeply grounded during S2). Scope:
    reading real JOB9002 blocks to identify and document proven patterns, of two kinds — whole
    equipment-instance FB/FCs (e.g. `MotorDOL`) reused via ordinary `CALL`, and repeated rung-shapes
    within one sequencing/mapping FC (e.g. `PlantAutoControl`'s per-equipment networks) documented as a
    real annotated example. (Revised 2026-07-15 from an earlier "slot-parameterized template"
    mechanism, abandoned in favor of reusing this project's existing FB/FC + `CALL` support — see
    `docs/07-pattern-library-spec.md`; the approval itself, its scope, and the genericization rule
    below are unchanged by that revision.) The existing genericization rule above is the operative
    constraint and applies in full: no real JOB9002 tag/DB/equipment name is ever committed into
    `patterns/` — any committed content (a pattern's own `.ir`, a `CALL`-site example, a rung-shape
    excerpt) is produced via `converter sanitize` (the same mechanism already used for the reference
    corpus), never copied verbatim.
  - **2026-07-15 — genericization rule waived for `input-mapping`/`output-mapping` specifically.**
    Project owner's own explicit call, made when these two patterns were being built: "do you
    really need to sanitize to make a pattern from it?" — challenging the default assumption that
    `converter sanitize` is always required before JOB9002 content commits to `patterns/`. Agreed:
    the genericization rule above exists to strip identifying data, but `FC Inputs`/`FC Outputs`
    content has none to strip — `DI1`-`DI94`/`DQ1`-`DQ26` are bare sequential point numbers, and
    the function names (`HFLCRunning`, `AirStarFCRunning`, etc.) are the same generic
    controls-engineering vocabulary already committed verbatim elsewhere (`motor-dol`,
    `chained-permissive-enable`'s own equipment names). Scope: `patterns/input-mapping/` and
    `patterns/output-mapping/` only — both committed with real JOB9002 tag/DB names, no
    `converter sanitize` pass run. Not a blanket waiver of the genericization rule; any other
    pattern (including the `DB_Inputs`/`DB_Outputs`-equivalent buffer-DB pattern started
    2026-07-15) is still evaluated on its own content against the same test — is there anything
    here that actually identifies the site/site — not assumed exempt by this entry.
  - **2026-07-15 — extended to mechanical review activity (S4).** Project owner's own explicit
    instruction: finish S4 properly by satisfying its own stated exit criterion (a blind
    comparison between the project owner's independent review and `converter review`'s own
    findings; `docs/02-roadmap.md`). The committed Green-tier reference corpus is fully spent for
    this purpose — every one of its 14 files was narrated during S4 planning and then shown in
    full in the live pilot report, so no blind comparison is possible against it anymore. Scope:
    running `converter review` (read-only findings-report generation — no writes, no import back
    into JOB9002, lower-risk than S3's own write-activity extension) against one or more real JOB9002
    blocks not previously touched by any work in this project, specifically to compare the tool's
    findings against the project owner's own independent read of the same content. Specific
    findings, tag names, and exact wording from this stay in conversation, not committed to any
    file, mirroring S2's own read-only-activity pattern — the existing genericization rule above
    still applies in full to anything written *about* this work in `docs/notes/stage-gates.md`/
    `CHANGELOG.md` or any other committed doc.
  - **2026-07-18 — extended to S6 generation-validation (sanitize-first, Green).** Project owner's
    own explicit choice (this session), after the test-project001 survey found no clean tier-(a) /
    REQ-grounded new-block target: use a **medium-complexity JOB9002 equipment-control FB as a
    ground-truth answer key** for validating the `gen-block-new` skill (derive a spec from the real
    block, regenerate it blind, compare to the original). **Handling: sanitize-to-Green-first** —
    the chosen FB and its tag/DB dependency closure are run through `converter sanitize` (same
    mechanism/maps as the reference corpus) into invented names **before** any generation work, so
    every downstream artifact (derived spec, regenerated block, comparison) is Green and
    committable; the only Amber access is the read needed to export+sanitize the block (Portal
    `list`/`export`, then `sanitize`). **Method: blind isolation, enforced at the filesystem** — the
    sanitized answer-key block is quarantined in a location the generating agent is never pointed at
    and never told exists; the spec is derived by a context that may see the block; a separate,
    fresh context runs `gen-architecture`/`gen-block-new` seeing **only** the derived requirements
    spec + a deps-only context dir (dependency DBs/UDTs/tags, **not** the block), never the original
    IR/XML; a final pass compares the regenerated block to the answer key. The genericization rule
    above is satisfied by the sanitize pass — no real JOB9002 name reaches any committed artifact. The
    resulting Green fixture (answer key + deps + spec + regenerated block) may become a reusable
    coding-skill validation corpus.
  - **2026-07-20 — further extended to `gen-block-modify-purpose` validation (same sanitize-first,
    Green, blind-isolation handling).** Project owner's own explicit choice, to validate
    `gen-block-modify-purpose` via the **MotorDOL→MotorVSDSystem purpose change** — now **complete** (blind
    MotorVSDSystem, `f588fcf`; imports + compiles 0 errors, 2026-07-20): a real JOB9002
    **`MotorVSDSystem`** FB (and its DOL counterpart + dependency closure) is used as the ground-truth **answer
    key** — sanitized to Green *before* any generation work, quarantined, with the blind purpose change
    generated by a fresh context that sees only the DOL source + derived spec + gate-1 manifest, never the
    answer key. Identical mechanism/maps to the 2026-07-18 `gen-block-new` scope above (`sanitization/
    MotorVSDSystem.map.json` already exists); the only Amber access is the read to export+sanitize. Every
    downstream artifact stays Green and committable. This extends the S6 generation-validation scope from
    the `gen-block-new` skill to the `gen-block-modify-purpose` skill; it does **not** authorize production
    S7 modification of any real production block (that stays gated behind "S6 done").
  - **2026-07-20 — further extended to the S6-Killer-Plan `PlantAutoControl` answer-key validation (same
    sanitize-first, Green, blind-isolation handling).** Project owner's own explicit choice, this
    session: implement the S6-Killer-Plan (`docs/notes/S6-Killer-Plan.md`) — derive an intent-level
    requirements register from the real **`PlantAutoControl`** orchestration block **plus its full
    dependency closure** (the 8 equipment FBs and their DB/UDT/tag roots), regenerate it blind through
    the pipeline, and grade against the original as an answer key. This **extends the 2026-07-18
    single-FB `gen-block-new` scope up to `PlantAutoControl` + closure** — an orchestration block, larger
    than a single equipment FB. Handling is unchanged and mandatory: **sanitize-to-Green first** —
    export the closure from JOB9002, `converter sanitize` with the existing `sanitization/*.map.json`
    maps (`PlantAutoControl.map.json` et al., already present from the 2026-07-11–14 round-trip work) into
    invented names **before** any spec work; the only Amber access is the export read; every committed
    artifact (`ir/PlantAutoControl-bench/`, the sealed answer key under `docs/evidence/`, the derived
    register `gen/PlantAutoControl-bench/requirements.md`) is Green. Same genericization rule — no real
    JOB9002 name reaches any committed file, enforced by a map-key leakage grep before commit. Does
    **not** authorize production S7 modification of any real production block.

- **2026-07-10 — reference project (`ir/reference/`, `simatic-ml/reference/`) seeded from
  sanitized data, under a separate, private approval not detailed here.** The committed content's
  structural shapes (wiring topology, instruction types, slice/array addressing) originate from
  real production PLC data; every tag path, block name, and comment is invented — nothing
  site- or site-identifying is recorded in this repo. The mapping from real to invented
  values is intentionally not committed anywhere (`.gitignore`: `sanitization/`) and isn't
  reconstructable from what's here. This entry exists so the corpus's Green-tier claim
  ("purpose-built, contains nothing identifying") has a recorded basis rather than none — see
  `tests/golden/README.md` for what was actually done to it.

- **2026-07-15 — test-project001's own functional design informed by a real supplied spec+functional
  description ("Kestrel Shredder Systems"/JOB9003-K150 demo panel — `SpecSheet.xlsx`, `FuncDesc.docx`),
  genericized rather than approved as Amber.** Project owner's own explicit choice, offered
  directly against approving it the way JOB9002 was: keep test-project001 Green throughout rather than
  bring in a second Amber-tier real project. Both source files stay local-only
  (`.gitignore`: `SpecSheet.xlsx`, `FuncDesc.docx`) — real company name, panel part number, and
  model-line codes never appear in test-project001 itself or anything committed; only the invented
  names in `sanitization/Kestrel Shredder Systems.map.json` (also gitignored, same reasoning as every other
  sanitization map) do. Same mechanism as the 2026-07-10 reference-project entry above (real
  structure informs an invented artifact, mapping never committed) — noted here as a second,
  independent instance rather than assumed covered by that one, since it's a different real
  source. JOB9003 itself is the same project named "no longer in use" in `docs/notes/stage-gates.md`'s
  S0 entry (superseded by JOB9002 as this project's own reference project) — this is new material
  from it (a spec/functional-description pair, not TIA project data), used only as real-world
  input to an invented design, not reopening JOB9003 as an active data source.
