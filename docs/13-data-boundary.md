# 13 — Data & IP Boundary

**Status: DRAFT — needs a decision (yours, possibly your employer's) before real project data flows to any AI. Ties to risk R-08.**

## The question

Claude Code sends repo content and command output to Anthropic's API. Exported PLC projects can contain identifying names, machine know-how, network details, and NDA-covered material. What is allowed to leave the engineering PC?

## Proposed tiers

| Tier | Content | AI access |
|------|---------|-----------|
| Green | This doc suite, tooling code, IR spec, pattern library, the purpose-built reference project | Yes |
| Amber | Real project logic with identifying data (company names in comments, IP addresses, site names) | Only after sanitization, or with explicit per-project approval |
| Red — confidentiality | Anything a contract, NDA or commercial sensitivity would otherwise keep off a shared repo | Never in committed repo content. **Full working access inside `Live Runs/`** (see "Live runs" below) — the restriction there is on *retention*, not on access |
| Red — safety | Safety program content: F-blocks, F-runtime groups, the safety program | **Never, everywhere, no exception** — `CLAUDE.md` hard rule 2, also enforced in tooling. `Live Runs/` does not change this |

> 🔴 **A DIRECTORY IS GREEN IF AND ONLY IF IT IS LISTED IN `tools/green-claims.txt`. Prose carries
> no authority — including prose in this document.** Added 2026-09-19 as the standing answer to
> `AB-2`, where four directories were asserted Green in sentences (three of them in this file's own
> per-project approvals) while **not one had ever been on the scrub builder's `--green` list**.
> A machine-readable register and a prose claim coexisted, disagreeing, for two months because
> nothing compared them.
>
> `tools/check-green-claims.py` now does, on every CI run: each declared directory must be covered
> by that list **and** its tracked content must carry no declared or inferred vocabulary. Writing
> "this is Green-tier" in a document declares nothing and is checked by nothing — the word is
> load-bearing in ~55 innocent sentences here ("the suite stays green"), which is why the register
> and not the word is the enumerator.
>
> **A claim about an UNTRACKED directory cannot be made this way and should not be made at all** —
> there is no content at `HEAD` to check. The reference TIA project folder is the live case: the
> *exports* from it (`ir/reference`, `simatic-ml/reference`) are registered and checkable; the
> folder itself is gitignored and is Green by the tier row above, not by measurement.

## Decisions needed (record as ADR-0003 when made)

1. Does workplace IT/management policy permit cloud AI on project data at all? In what form (commercial terms, data retention)?
2. Is a sanitization pass required for Amber (strip/alias restricted identifiers, IPs, site names in comments and tag names)? If so it becomes a converter feature.
3. Per-project: do any active NDAs categorically exclude their projects?

## Interim rule (until decided)

Only Green-tier content goes near Claude Code. The reference project is purpose-built and contains nothing identifying — all development through S6 can proceed on Green data alone, so this decision does not block early stages.

Two standing exceptions to that rule, both recorded below: the per-project Amber approvals, and — from 2026-08-04 — `Live Runs/`, which is governed by its own section and is *not* an approval-per-job process.

## Per-project approvals (Amber, pending ADR-0003)

- **2026-07-10 — "JOB9002 - Tom White Waste" (scratch copy).** Private engineering project, Amber-tier
  (identifying name; not sanitized). Explicit per-project approval given by the
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
  - **2026-08-17 — extended to the HMI, READ-ONLY, for design-convention learning.** Project
    owner's own explicit instruction in conversation: *"The JOB9002 project has a Unified panel, run
    the walk on it, see what you can learn."* Every prior extension of this entry was PLC-side, so
    the HMI is new scope and is recorded rather than assumed covered.
    - **Scope:** read-only walk of the Unified HMI device (`openness-cli hmi`) — screens, screen
      items, their geometry, and per-property dynamizations. No write of any kind: no
      `hmi-create-screen`, no `hmi-edit-screen`, no import, no compile.
    - **Purpose:** `docs/17-hmi-conventions.md` is a constraint set with no layout or composition
      rules at all, so generated screens are rule-compliant and unguided. Real projects are the
      only available evidence for what a competent screen actually looks like.
    - **What may be RETAINED, and it is a narrow slice:** *structural and geometric facts only* —
      zone heights, grid pitch, item-size distributions, item counts per screen, spacing, how items
      cluster. These are not identifying and are the whole point of the exercise.
      **NOT retained, in any committed doc:** screen names, tag names, item names, alarm or label
      text, equipment names, or any layout reproduced closely enough to identify the plant. The
      rule is the same one this entry has always carried — structure may inform an invented
      artifact; values never leave.
    - 🔴 **Owner's methodological caveat, recorded because it governs how the findings are used:**
      *"The design in those projects are probably not all good or a perfect example, but they are
      directionally correct. So just because its in a project doesn't make it right."* Observed
      practice is therefore **evidence about direction, not authority**. Where the corpus and an
      established ergonomic principle disagree, the principle wins and the disagreement is recorded.
      There is already a measured instance: the JOB9003 anchor's switches are 5.1 mm on the minor
      axis, against a 9 mm floor.

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
    here that actually identifies the site — not assumed exempt by this entry.
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
  - 🔴 **CORRECTION 2026-09-19 — the two 2026-07-20 entries above claim an OUTCOME they did not
    achieve. The approvals stand; the "every committed artifact is Green" sentences do not.**
    Both entries mandate *sanitize-to-Green-first* and then assert the result: "Every downstream
    artifact stays Green and committable" and "every committed artifact (`ir/PlantAutoControl-bench/`,
    the sealed answer key under `docs/evidence/`, the derived register
    `gen/PlantAutoControl-bench/requirements.md`) is Green". Measured against this project's derived
    de-identification vocabulary, over **tracked content at `HEAD`** — which is what "committed"
    means — **all four named directories carry live vocabulary**:

    | directory | tracked files carrying it | distinct hunted needles | of which DECLARED (T1) |
    |---|---|---|---|
    | `gen/_validation/MotorVSDSystem-purpose` | 3 of 10 | 1 | 0 |
    | `ir/PlantAutoControl-bench` | 2 of 35 | 14 | 2 |
    | `gen/PlantAutoControl-bench` | 6 of 8 | 3 | 1 |
    | `docs/evidence` | 13 of 24 | 36 | 4 |

    **What is and is not being said.** The *approvals* were correctly scoped and are not withdrawn:
    they authorized an Amber read to export and sanitize, and that is what happened. What is
    corrected is the claim about the **output**. Read this as a candidate list, not a verdict —
    `T2 INFERRED` gates on whole-token presence and a map key can legitimately be a conventional
    word, so triage is still required (AB-1's rule). **The `T1 DECLARED` column is the hard part**:
    the owner wrote those strings down as identifiers, and they are present in committed files that
    three sentences in this document call Green.

    **Nothing is leaking, and that is exactly why this is worth writing down.** The publication gate
    (`tools/verify-scrub.py`) returns `T1 0 / T2 0` over the published artifact, because the scrub
    rewrites this content on the way out. The hazard is not the data — it is the **label**. A false
    *already sanitised* mark is what licenses a copy into somewhere the scrub does not run, which is
    the shape of **AB-1** and the reason **M-5** exists.

    **Why it was not caught at the time is stated in the entry itself**: the PlantAutoControl scope names
    its own enforcement as "a map-key leakage grep before commit". That is a per-lane instrument, and
    AB-1's finding is that a per-lane check catches the lane's own work while only a repo-wide sweep
    catches the repo. The instrument was the wrong shape, not carelessly applied.

    The terms are deliberately **not** listed: a record of a leak must not be a copy of it. Re-derive
    with the scrub tooling. Tracked as **AB-2** in `docs/notes/data-boundary-audit-backlog.md`; the
    durable fix is **M-22** in `docs/notes/mechanisation-backlog.md` — a Green claim should be
    checkable by a tool, not asserted in prose.
  - **2026-08-07 — extended to the HMI device, read-only, for survey grounding.** Project owner's own
    explicit instruction, this session ("open up JOB9002 use the hmi in that for examples"), following
    `docs/notes/openness-hmi-api-survey.md`. Recorded because **every prior scope in this entry is
    PLC-side** — blocks, DBs, UDTs, tag tables, patterns — and none of them reaches an HMI device, so
    this is genuinely new ground rather than a re-reading of an existing extension. The entry's own
    "per-project, not blanket — re-confirm before using this project for anything beyond what's
    listed below" rule is what required it to be asked and recorded rather than assumed.
    - **Scope, as approved:** read-only examination of the project's HMI device via
      `openness-cli hmi` — enumerate screens, screen items, their geometry and their per-property
      dynamizations, to ground the survey's claims in a real project instead of reflection alone.
      **Read-only in the strict sense**: no screen or tag is created, no property is set, nothing is
      imported, nothing is compiled, and the walker has no code path that writes.
    - **Output rule — unchanged from the 2026-07-14/07-15 read-only extensions, which this mirrors:**
      real screen, tag and equipment names may appear in conversation and in local scratch files
      outside the repo; **nothing verbatim reaches a committed doc.** Where the survey needs an
      example it gets an invented one carrying the same structure, on the same basis as the rest of
      this entry. Structural findings (which item types exist, how a dynamization is shaped, what the
      API does and does not expose) are not identifying and may be recorded directly.
    - **Relevant finding, recorded here because it changes what the approval is worth:** JOB9002's panel
      is a **Unified** device, not classic. Unified has no screen export, so there is no file
      artifact to sanitize the way `converter sanitize` handles SimaticML — the only way to observe a
      screen is to read the live object model. That makes the output rule above the *only* control on
      this material, rather than a second one behind a sanitiser.
    - **Not authorized by this entry:** any write to JOB9002's HMI (screens, tags, alarms, scripts),
      use of its HMI content as generation input, committing any real HMI name or comment text, or
      HMI work on any other project. `10-non-goals.md`'s exclusion of HMI *engineering* is untouched
      — this is reconnaissance, and it does not open a capability.
    - **Superseded in one respect, 2026-08-07 (same session): a WRITE test was authorized.** Project
      owner's own explicit instruction — "now try creating a test screen in the scratch copy" —
      immediately after the read-only survey established that Unified screens can only be authored
      through the object model. This lifts the "no write to JOB9002's HMI" clause above **for a
      throwaway test artifact only**, and nothing else in that clause moves.
      - **Scope:** create one test screen, with an invented name and a small number of static items,
        in **JOB9002's own scratch copy** (never a real project file), to prove the
        `Screens.Create` → `ScreenItems.Create<T>` → `Validate()` → `Save()` path works end to end.
        Writing into that scratch copy is not itself new ground — the 2026-07-14 S3 extension above
        already authorized importing generated content back into it.
      - **Deliberately still excluded:** any modification of an *existing* screen, tag, alarm or
        script; any binding to real HMI/PLC tags; and any write to the real (non-scratch) project.
        The test screen references nothing that already exists.
      - **Extended 2026-08-07, same session: MODIFY + EVENTS, on the test artifact only.** Project
        owner's instruction — "now modify this screen (action 1) and add events to objects
        (action 2)". Note what this does and does not move: the exclusion above was written against
        *pre-existing* screens, and the thing being modified here is **the throwaway this session
        created minutes earlier**, which nothing depends on. Editing `ZZ_AI_TestScreen` and editing
        one of the 48 real screens are materially different acts, and only the first is authorized.
        - Covered: setting attributes on that screen and its own items, and attaching event handlers
          (with or without script bodies) to them.
        - **Still excluded, unchanged:** touching any of the 48 pre-existing screens, any real HMI
          tag/alarm/script, any dynamization bound to a real tag, and the non-scratch project. The
          event scripts written are self-contained trace calls that reference no project data.
        - The tooling built for this (`hmi-edit-screen`) is general-purpose and *could* edit a real
          screen; the restriction is procedural and recorded here, not enforced in code.
      - **Extended again 2026-08-08: DYNAMIZATIONS, against invented tags only.** Owner instruction
        ("now try the dynamizations"). Binding a screen property to a tag is the PLC↔HMI coupling and
        the largest untested capability. Scope:
        - **A new tag table and tag, both with invented `ZZ_AI_*` names**, created in the scratch
          copy purely as a bind target. No existing tag table is modified and no real tag is read,
          bound or altered.
        - **Dynamizations on the throwaway test screen only**, bound to that invented tag — and, as a
          deliberate experiment, to a tag name that does not exist, to establish whether anything
          (Validate, or the device compile) catches a dangling reference. That question is the one
          §4d of the write-api note says nothing currently answers.
        - **Still excluded:** binding to any real HMI or PLC tag, and everything previously excluded.
          The clause "any dynamization bound to a real tag" stands unchanged — this extension covers
          bindings to tags this session itself invented, which is a different thing.
      - **Extended 2026-08-08 to the whole CAPABILITY PROBE PROGRAMME, including DELETION.** Owner
        instruction: work through the measured gaps autonomously ("This is autonomus work so try not
        involve me"), against the plan approved the same day. This is deliberately a **single**
        extension covering the programme rather than one per object kind — the previous pattern of
        asking per escalation is incompatible with an instruction to proceed without involvement, so
        the scope is stated once, in full, here.
        - **Covered:** creating, modifying and **deleting** HMI objects of any kind — screens, screen
          items, event handlers, dynamizations, tags, tag tables, screen groups, alarms, alarm
          classes, connections, data/alarm logs, logging tags, plant views — **provided every object
          involved carries an invented `ZZ_AI_*` name and was created by this programme**, in
          **JOB9002's scratch copy only**.
        - **DELETION is the significant new grant** and the reason this entry exists: no previous
          entry authorised deleting anything, and 0 of 184 deletable types had ever been touched.
          Deletion applies **only to this programme's own `ZZ_AI_*` artifacts** — never to any of the
          48 pre-existing screens, the 233 real tags, the 311 real alarms, or any real tag table,
          connection or log.
        - **Read-only, explicitly:** device-level `RuntimeSettings` (start screen, resolution,
          languages). These are observed and mapped, never written — changing a device's start screen
          is a real change to a real project, not a probe.
        - **Still excluded, unchanged:** any write to a real (non-scratch) project; any create,
          modify or delete touching a pre-existing object; binding to a real HMI or PLC tag;
          committing any real HMI name or comment text; and HMI work on any other project.
        - **Cleanup is part of the scope, not an afterthought:** the programme ends by deleting its
          own artifacts and proving a clean device compile with none of them present.
        - **`10-non-goals.md` is NOT amended.** This stays a capability probe. Making HMI engineering
          a goal needs ADR-0007, which this programme *drafts as Proposed* for the owner to accept or
          reject — it does not self-accept, and the probe tooling remains general-purpose with the
          restriction procedural and recorded here rather than enforced in code. That is
          worth stating plainly rather than implying the tool is safe by construction.
      - **Standing intent:** the artifact is disposable and removable on request. Its name is
        invented, so no real HMI name is created or committed either.
      - **Executed 2026-08-07.** One screen created in the scratch copy with three default items,
        `Validate()` clean, saved, and confirmed by read-back (49 screens where there were 48).
        Details in `notes/openness-hmi-api-survey.md` §10.
      - **Artifacts left in the scratch copy (2026-08-08), all invented names, all disposable:**
        `ZZ_AI_TestScreen` (3 items, a `Tapped` handler with a self-contained script, a `Loaded`
        handler, two tag bindings), the tag `ZZ_AI_TestTag`, and its table `ZZ_AI_TestTags`. These
        are everything this session wrote to any project. The device **compiles clean** with them
        present (`STATE: Success`, 0 errors; the 156 warnings are pre-existing, from the project's
        own screens). Delete them in TIA Portal whenever convenient — nothing depends on them.
      - **`10-non-goals.md` is NOT amended by this.** HMI engineering remains a non-goal there,
        "revisit only via ADR". This is a capability *probe* answering the survey's own cheapest
        open question (does `Validate()` do anything?), not the opening of an HMI capability — that
        would need the ADR the non-goals doc asks for.

- **2026-07-10 — reference project (`ir/reference/`, `simatic-ml/reference/`) seeded from
  sanitized data, under a separate, private approval not detailed here.** The committed content's
  structural shapes (wiring topology, instruction types, slice/array addressing) originate from
  real production PLC data; every tag path, block name, and comment is invented — nothing
  organisation- or site-identifying is recorded in this repo. The mapping from real to invented
  values is intentionally not committed anywhere (`.gitignore`: `sanitization/`) and isn't
  reconstructable from what's here. This entry exists so the corpus's Green-tier claim
  ("purpose-built, contains nothing identifying") has a recorded basis rather than none — see
  `tests/golden/README.md` for what was actually done to it.

- **2026-07-15 — test-project001's own functional design informed by a real production spec+functional
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

- **2026-07-29 — "JOB9003 - K150 Demo" TIA project data, Amber, approved read-only and task-scoped.**
  Explicit per-project approval given by the project owner in conversation, and deliberately narrow:
  it **supersedes only the last clause** of the 2026-07-15 entry above ("not reopening JOB9003 as an
  active data source") **for this one task**, and does not otherwise widen that entry — JOB9003 is not
  a general Amber source the way JOB9002 is. Offered to the owner as scoped-vs-broad; scoped was chosen.
  - **Scope, as approved:** read-only extraction of alarm definitions from the live project's own
    blocks (`FC_AlarmsMain`, `FB_MotorDOL`, `FB_MotorFwdRevDOL`, plus `DB_Alarms` and the instance
    DBs needed to resolve per-equipment wiring), for the purpose of generating HMI discrete-alarm
    rows in the owner's local `HMIAlarms.xlsx`. Export/convert/read only — no import, no compile
    against the live project, no modification of any block.
  - **Output rule:** real tag names, equipment names, alarm wording, and network titles may appear
    in conversation and in `HMIAlarms.xlsx` (local, gitignored — see below). **Nothing verbatim from
    this project goes into any committed repo doc** — same rule as the JOB9002 entry's 2026-07-14/07-15
    read-only extensions, which this mirrors deliberately. The raw exports live in `scratch/`
    (gitignored) and are not committed.
  - **Noted at approval time, and the owner's call to make:** every prior approval here was scoped to
    advancing a stage of *this* project (S0 spikes, S1 grounding, S2 explanation, S4 review, S6
    generation-validation). This one is not — it is production engineering output for a real job,
    using the knowledge base rather than building it. Recorded explicitly so that difference is
    visible in the record rather than folded silently into a boundary approval.
  - **Not authorized by this entry:** any write to the JOB9003 project, any use of its logic as
    generation input or as a committed example, and any further JOB9003 work beyond the alarm
    extraction above — re-confirm with the owner first, per the same "per-project, not blanket"
    rule the JOB9002 entry states.

- **2026-08-17 — "JOB9003 - K150 Demo - Scratch Copy", Amber, approved READ access, anchor project for
  the HMI programme.** Explicit per-project approval given by the project owner in conversation:
  *"add it to .gitignore, mark it as an amber project with read access allowed... use it as your
  anchor initially and we will expand to the other hmi types from there."* A scratch copy of the real
  JOB9003 project, placed at the repo root by the owner. **Gitignored** — `.gitignore` names it
  explicitly rather than relying on the `j[0-9][0-9][0-9][0-9]*` backstop, which matches it only via
  Windows' case-insensitivity.
  - **Scope, as approved:** **read** access to the project, as the anchor for the `hmi/` development
    programme. This is a genuine widening of the 2026-07-29 entry, which was scoped to one alarm
    extraction and said "no further JOB9003 work" — that clause is superseded for HMI-programme work
    by this entry, and only for it.
  - **What it anchors, and why it matters:** the project carries a **KTP900 Basic** panel — a
    *Classic Basic* device. The owner states the first real application is a **Classic Basic 7"**.
    That settles the open target-family question in `hmi/PLAN.md` §5 decision 5, and settles it
    against the direction the plan was originally written in (Unified). Classic is a SimaticML file
    pipeline with no screen object model, so the HMI programme's architecture changes accordingly —
    see `hmi/PLAN.md` §0.
  - **Output rule, unchanged from the JOB9002 and 2026-07-29 JOB9003 entries:** real tag names,
    equipment names, screen names, alarm wording and comment text may appear in conversation and in
    gitignored local artifacts. **Nothing verbatim from this project goes into any committed repo
    doc.** Structural findings — SimaticML element shapes, which item types a Basic panel supports,
    attribute names, API behaviour — are *not* identifying and may be documented directly;
    that distinction is what makes this project usable as an anchor at all. Specific values get
    invented replacements.
  - **Extended to WRITE the same day, 2026-08-17, by the project owner:** *"I give you write
    permissions, dont worry if you overwrite or delete anything in that project I have a separate
    copy that I can use to reset the scratch version with."* So: **import, screen creation and
    deletion, compile and modification are all approved on this scratch copy**, and it is explicitly
    disposable — the owner holds an independent master. This unblocks the HMI plan's wave-1 thin
    spike, which had no write target. Hard rule 5's "work freely against the scratch copy" now
    applies here in full.
    - **What does NOT change:** this is a *scratch copy*, not the real JOB9003 project — the real one
      stays untouched. The **output rule above is unaffected**: real names may exist inside this
      project and in gitignored local artifacts, and **nothing verbatim from it enters a committed
      doc.** Write access widens what may be *done to the project*; it does not widen what may be
      *retained about it.* Those are independent axes and this entry deliberately moves only one.
    - **Still not authorized:** any write to the *real* JOB9003 project, use of JOB9003 logic as
      generation input, or JOB9003 content as a committed example.
    - ⚠️ **Disposable is not the same as free.** A restore costs the owner an action, and a
      destructive step taken casually is still a step somebody has to undo. Prefer additive work;
      take a note of what a reset would cost before a bulk delete.

## Live runs (`Live Runs/`) — full working access, zero retention

**Established 2026-08-04 by the project owner.** From this point the tooling is used on real
live jobs worked end-to-end, not only on pipeline-development data. Those jobs live under
`Live Runs/<job>/` and are governed by this section rather than by the per-project approval
process above — no separate approval entry is needed per job; placing a job in `Live Runs/` **is**
the approval.

**Access — all of it, every tier of confidentiality (widened 2026-08-05).** Whatever the owner puts
in `Live Runs/` — control specs, functional descriptions, the live TIA project, exports, I/O
schedules, correspondence — the AI may read, reason over and work against **in full, unsanitized**.
Real identifying names, site names, tag names, equipment names and comment text are all usable in
conversation and in local working files inside `Live Runs/`. There is no read-only restriction:
generation, import and compile against a live-run project are in scope when the owner asks for them,
subject to the hard rules in `CLAUDE.md` (which are unchanged — in particular hard rule 5, engineer
review before anything enters the real project).

This access is **not limited to Green and Amber material**. Content that would elsewhere be
Red-tier on confidentiality grounds — contract- or NDA-restricted, commercially sensitive, whatever
would normally never go near a shared repo — is in scope inside `Live Runs/` on the same terms as
everything else. The owner placing a job there is the decision that this material may be worked on;
the AI does not tier-triage it, ask for a per-file approval, or hold back from using a document
because it looks sensitive. **The entire boundary for live-run data is retention, not access:** it
may be used freely and must never be committed or written into the knowledge base (below). The one
carve-out is safety, which is a different axis entirely — see "Safety is unaffected".

**Retention — the whole point of this section. Two prohibitions, both absolute:**

1. **Nothing from a live run is ever committed.** `Live Runs/` is gitignored — unanchored, so a
   live-run folder at any depth is covered, and spelling variants (`LiveRuns/`, `Live-Runs/`,
   `live_runs/`) are ignored too. That must stay that way; never narrow those patterns and never
   add a negation (`!`) that re-exposes anything under them. Do not commit its contents, do not
   copy its contents to a committed path, do not `git add -f` any of it, and do not quote it
   verbatim in a commit message, PR body or changelog entry. This applies with full force to the
   Red-tier material the access rule above now admits: broad access is only safe because retention
   is zero, so the two halves are load-bearing on each other.
2. **Nothing from a live run is ever written into the knowledge base.** No committed doc, ADR,
   note, skill, pattern, convention rule, test fixture, `CLAUDE.md` edit or Claude Code memory
   file may carry live-run content — including tag names, block names, equipment names, comment
   text, alarm wording, site names, or a paraphrase specific enough to identify any of
   them. This bites hardest where it is least obvious: a "lesson learned" phrased in the job's own
   vocabulary is still live-run content leaking into the repo.

**How a lesson gets out.** Live runs will produce genuinely valuable learnings — converter gaps,
convention rules, new patterns, spec-review heuristics. Those are not lost, but they are recorded
**only after** one of:

- **Sanitization.** The learning is restated in invented/generic vocabulary carrying no
  site-specific identifier, on the same basis as the 2026-07-10 reference-project and 2026-07-15
  Kestrel Shredder Systems entries: real structure may inform an invented artifact; real values never
  appear. Where a name mapping is used it goes in `sanitization/` (gitignored), never committed.
- **Explicit owner permission.** The owner may nominate a specific, defined item — one lesson, one
  data shape, one example — and either approve it verbatim or direct that it be sanitized. The
  permission is per-item and per-instance, not a standing grant for the job, and the item it
  covers is named when it is given. Record the permission in this section when it happens.

### How leaks actually happen (recorded 2026-08-05, from four real ones in one day)

The first live run produced four leaks into tracked files inside a single working day, every one
caught by a mechanical grep before it was committed, and none of them by anyone being careless
about confidentiality. They are recorded because the *shape* of them is not what the rule above
leads you to expect.

**None of the four was an identifying name, an alarm text, or a block of copied content.** All four
were fragments that felt generic:

  - an example in a command's own documentation, using the job's equipment token
  - a unit-test fixture named after one of the job's tag tables
  - a code comment illustrating an error message with the job's equipment noun
  - a convention rule whose worked example used the job's word for one of its operations

**THE TEST IS NOT "DOES THIS SOUND GENERIC". IT IS "DID THIS STRING COME FROM THE JOB FOLDER".**
Three of the four read as perfectly ordinary industrial vocabulary in isolation. That is exactly
why they got written: a term that has been in front of you all day stops looking like it belongs to
anyone. The word had a source, and the source was the job.

**Where they occur is predictable, and it is not where you would guard.** Nobody pastes a spec into
a commit. What leaks is the *illustrative* material — examples, fixtures, comments, error strings —
because that is the writing where you reach for a concrete case, and the concrete case you have to
hand is the one you have been working on. Prose about the job is easy to notice and rare. An
example is neither.

**So: grep before every commit that touches a tracked file, during a live run.** Not when it feels
warranted — every time. It costs one command. All four of these were found by the grep and not by
the author re-reading their own work, including one written by the reviewing engineer who had
already written the boundary rules. Re-reading finds prose; it does not find a fixture name.

**A wide grep will also produce false positives, and that is fine.** The same sweep flagged
`drum separator` across a dozen committed files — a waste-processing machine in an unrelated
corpus, two weeks older than the live run. Confirming a hit is innocent costs a `git log`; assuming
one is innocent is how a real one survives. Prefer the noisy pattern.

**Not automatic.** The AI does not decide on its own that something is generic enough to keep. If a
live run surfaces something worth capturing, raise it with the owner and ask; the default while
unanswered is that it stays inside `Live Runs/` and is not written down anywhere else. Working
notes for a live run belong in that job's own folder, where they are gitignored along with it.

**Safety is unaffected.** This section grants confidentiality access, not safety access — the two
Red rows in the table above are separate axes and only the confidentiality one is opened here.
`CLAUDE.md` hard rule 2 stands in full: F-blocks, F-runtime groups and the safety program are never
read, written, converted, explained or referenced, in a live run exactly as anywhere else, however
freely the rest of the same project may be worked on. If the owner wants that changed it is a
deliberate separate decision (hard-rule change + ADR), never something inferred from "full access to
all data provided".

### Live runs opened

Job codes only. The site name, equipment scope and any other job detail stay in the job's
own gitignored folder and are deliberately not restated here — prohibition 2 applies to this
register like it applies to everything else. Note the difference from the per-project approval
entries above, which do name their projects: those predate this section and were recorded under
the older Amber process; live runs do not follow it.

- **2026-08-04 — job `JOB9004`.** First live run; owner is reviewing each step. Job identity, scope
  and all content live in `Live Runs/JOB9004/` only. Governed entirely by this section.

### Promotions out of a live run

The one route by which anything leaves a job folder. Every promotion is recorded here, in job-code
terms only, so that "did anything ever leave, and on whose word" is answerable without opening a job
folder. **A promotion is never the agent's call and never mine** — this section exists because the
rule it records is the easiest one in this document to talk yourself past, the content being
generic-looking and the benefit obvious.

- **2026-08-06 — `patterns/valve-two-state/`, from job `JOB9004`.** A generic two-state valve FB and
  its interface UDT. **Route: explicit per-item owner permission**, not sanitization-with-mapping —
  the owner named the two items and instructed their promotion. Copy, not move: the job keeps its
  own versions, which retain the job-specific design history the library copies must not.
  *Audited before commit, by the dispatching agent rather than the writing one*: zero restricted,
  process, equipment, tag or alarm-ID content in any promoted file. The FB was already free of job
  content and was promoted unchanged; the UDT's comments carried job provenance (a deleted
  allocation paragraph, a bit-history narrative, a document reference, a job-specific count and one
  job word for a hazard) and were rewritten to keep the engineering and drop the record. Two
  residual audit hits were checked and cleared: a date, which cites a convention change in this
  repo, and one generic noun inside a sentence listing what the block does *not* name.
  **A security check fired on this promotion** and was assessed rather than waved through: it
  reported that no owner permission was recorded *in the sub-agent's own transcript*, which was
  true — the permission was given in the main conversation and relayed. The finding was correct
  about what it could see and its underlying rule is the right one; this register entry is the
  durable record whose absence it was really objecting to.

- **2026-08-23 — the stimulus-head shell generator, from job `JOB9004`. Permission granted for the
  MECHANISM ONLY, and the owner's words were "keep it general".** A renderer in that job folder
  turns a declared head spec into the fixed phase-shell networks of a stimulus FB. What is
  permitted out is the **generator**: the shell's structure, its required refusals, and the shape
  of a head spec. What stays in the job folder is **every input and every piece of evidence** —
  the two existing head specs, the blocks they render, and the byte-for-byte verification that
  measured the port. Those are job content and this permission does not reach them.
  **The consequence is stated up front rather than discovered later:** the committed test corpus
  is therefore an *invented* head, so a green committed suite is **not** evidence that the
  generator reproduces a real one. That evidence exists only in the job folder and is re-run
  there. A reader who forgets this will over-trust the suite, which is why the denominator is
  written into the plan, the tests and the commit rather than left to be inferred.
  🔴 **The failure mode this permission is most exposed to is not a copied block — it is
  vocabulary.** A phase name, a cause-list member, an outcome bit or a watchdog comment carried
  across as an "example" is a leak in exactly the shape the four recorded ones took: it arrives
  inside something that feels like rigour. The generator must name nothing it did not invent.

- **2026-08-23 — the pre-flight classification's COUNTS, from job `JOB9004`. Permission granted for
  AGGREGATES ONLY.** Workbench Phase 5 asks a question that decides a phase: *would a PC-side
  interpreter over the block-under-test's IR have caught the failures we actually had?* Answering it
  means classifying two live-run corpora — the rig runs' result packages, and the job's numbered
  problem register.
  **What may be committed:** the **bucket counts**, the **names of the checks** a bucket-A row cites
  (`preflight`, `undriven-scan`, C-410 and so on — repo vocabulary, not the job's), and the
  **denominators** each count is out of.
  **What stays in the job folder:** the per-defect row table. Every row's subject is rendered as an
  **opaque id** wherever it is quoted outward, so a count can be checked against its denominator
  without the subject travelling with it.
  🔴 **The trap here is different from the generator's, and sharper: a bucket-B row has to say what
  an interpreter would have NEEDED to catch it** — a vector, a plant model, timer semantics — and
  that sentence is where a real signal name will try to get in, because naming it is the most
  natural way to be precise. **Describe the SHAPE of what was needed, never the signal.** A
  classification is a summary of a defect, and a summary specific enough to be useful is often
  specific enough to identify.
  ⚠️ **And the aggregate itself is a claim about the job's engineering, not just about our tooling.**
  *"N defects in this plant's blocks would only have been caught on the rig"* is a sentence about a
  site equipment. It stays inside the tooling question it was asked for — whether to build an
  interpreter — and is not repeated as a statement about the job.
