---
name: review-conventions
description: Conventions review of Siemens LAD blocks (IR form) against doc 06's convention sections — Naming, Structure, Commenting, Data, Instructions, Alarms (C-0xx–C-5xx). Wraps the mechanical `converter review` tool (output embedded verbatim) plus an AI pass over the single-file, cross-block, and judgment rules the tool doesn't check. Use whenever asked for convention compliance, "run the conventions check", "does this follow the site rules?", rule-ID findings against doc 06, or before presenting any generated logic (the docs/15 check stage — pipeline skill #9). NOT the simplicity reviewer — readability findings (C-101/C-126/C-203/C-601–C-607, the one-reading test) belong to /review-simplicity. Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe review:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe review:*)
---

# /review-conventions — the conventions reviewer (C-0xx–C-5xx)

Ladder-AI project. This skill enforces the convention sections of `docs/06-lad-conventions.md` —
Naming, Structure, Commenting, Data, Instructions, Alarms (C-0xx–C-5xx) — alongside its two
siblings: `/review-simplicity` (tier 2 of the priority order, the Simplicity & readability
section) and the functional review (tier 1, requirements). It is docs/15's `review-conventions`
stage (pipeline skill #9): the mechanical `converter review` tool wrapped by an AI pass over
everything the tool can't check. Read `CLAUDE.md` at the repo root first if you haven't — its
hard rules apply (you review LAD only; if anything looks like an F-/safety block, stop and report
it; never modify what you review).

**You are a reviewer, not an editor.** Produce findings; never edit the IR, never "fix while
you're in there." Findings are proposals for the engineer.

## Blindness — check before starting

This review is designed to run **without the author's reasoning** (docs/15 isolation model — the
AI form of blind review). If you wrote or restructured the code under review earlier in this same
session, or the conversation contains the author's design rationale or a prior review report of
the same content, say so at the top of your report: your review is then "informed, not blind,"
which the project treats as weaker evidence (see `docs/evidence/stage-S4.md`'s methodology notes). Recommend a
fresh-session/subagent run when blindness matters — e.g., for a gate decision. Two expected,
non-contaminating caveats: (1) **running `converter review` is not contamination** — it is the
mechanical half of this review by design; embedding its output is the job, not a leak.
(2) doc 06's rule rationales cite historical examples from this project's own corpus — reading
them is required and fine; declare it in your blindness note and re-derive every AI-pass finding
from the IR itself rather than from a rationale's summary. Contamination is the author's
reasoning, or another review's report of the same content — not the tool, not the rulebook.

## The bar you hold

1. **Every finding cites a rule ID with the severity doc 06 assigns it — read fresh this run,
   never from memory.** Several rules were revised 2026-07-16 (C-001's PascalCase member ruling,
   C-109's IO-mapping exception, the C-307/C-308 settings sharpening among them); a stale-memory
   citation is itself a review defect. Quote or tightly paraphrase the clause you relied on.
2. **Generated code answers to a stricter bar than site practice** (doc 06 preamble): err toward
   flagging; "defensible" is not a pass; "the real site block does the same" is never a defense
   for generated content.
3. Label every block `generated` or `imported-real` (check `docs/evidence/stage-S6.md` /
   `docs/notes/test-project001-retrospective.md` if unsure, and say which regime you applied).
   Imported-real findings are **documented context** — calibration and cross-reference risk,
   never demands to fix a block the engineer owns.
4. A pattern that appears to breach a rule (or a rule that appears to outlaw an admitted pattern)
   is a **tension flagged for an owner ruling** — never adjudicated by you, never silently
   resolved either way.
5. Severity and checkability are different axes. Stage-gates' "S4 Phase 1" section records the
   known mismatches (C-113/C-504 are error-severity but genuine judgment; C-112 is error-severity
   but structurally out of this pipeline's reach). Where they diverge, say so per rule rather
   than downgrading the severity or overclaiming the check.

## Inputs

- **Full IR only** (`.ir` files) — never a `converter digest` summary; digests are orientation
  aids and explicitly not review input (CLAUDE.md). The readable content is everything **before**
  the `SIDECAR` line — grep `^SIDECAR` for the split; never reason over sidecar content. If
  handed SimaticML, convert first (`converter to-ir`) into your own scratch area.
- **The whole project export** (`ir/<project>/` — blocks, UDTs, DBs, tag tables) for the
  cross-block rules. Reviewing a single block is legitimate for the inner loop, but then every
  cross-block rule in Group 2 is reported "not checkable at this scope" rather than silently
  passed.
- `docs/06-lad-conventions.md` — the preamble and all six convention sections (Naming,
  Structure, Commenting, Data, Instructions, Alarms), read before your first finding.
- `docs/evidence/stage-S4.md`, "S4 Phase 1" section — the checkability grounding: which rules
  the tool really checks, the CheckedVacuous honesty labels, and the severity-vs-checkability
  mismatches.
- The block's interface UDT files and instance DBs — several rules live there (C-118, C-122,
  C-125, C-307), and RETAIN markings live in the iDB/DB files.

## Step 0 — the mechanical pass (anti-drift contract)

From the repo root, run the S4 tool over every file in scope:

```
./src/converter/Converter/bin/Release/net8.0/converter.exe review <files...> --ignore-errors
```

Invoke it as a single command (no `cd &&` chaining, no pipes); if the Release binary is missing,
report the mechanical pass as blocked and get it built — never substitute memory of a previous
run, and never skip to the AI pass as if the tool had run. **Bash exists in this skill for
exactly this one command.** Quote the binary path and the exact invocation in your report header.

The contract:

- **Embed the tool's findings and per-rule status lines verbatim** — per file: the status lines
  (`checked, clean` / `checked, N finding(s)` / `checked, vacuous …` / `not applicable (…)`),
  every finding, and the `SUMMARY` line. Never re-derive, paraphrase, re-severity, re-locate, or
  filter them. Long is fine; edited is not.
- If you believe the tool is wrong about something, that is a **converter bug report** in your
  report's own words — the verbatim output still stands as what the tool said.
- Rules the tool reports `checked, vacuous` are reported exactly as the tool states them (they
  cannot fire against current IR capability) — not silently upgraded to "verified clean."
- **The NotApplicable-gap rule (self-retiring):** the AI pass may hand-check one of the tool's
  own rules ONLY where the tool's per-rule status line for that file says `not applicable`
  (today: every rule on TYPE/TAGTABLE files, the network-dependent rules on DB files). Each such
  finding is labeled **"hand-checked — tool reports NotApplicable for this content kind."** The
  moment a future tool version reports that rule `checked` for that content kind, the hand-check
  retires automatically — the status lines you just embedded are the authority, not this file.
- Never hand-duplicate a finding the tool already made. The AI pass covers what the tool didn't.

## The AI pass

Every AI finding carries a **bucket label**: **A** (single-file mechanical, tool just doesn't
cover it yet), **B** (cross-block/whole-project), or **C** (judgment). Every bucket-C finding
additionally says **"judgment"** in the finding line. Work through the four groups in order.

### Group 1 — bucket-A sweeps (single-file, tool doesn't cover yet)

**Retire condition, applying to every sweep in this group as if written on each rule:** before
sweeping, check Step 0's per-rule status lines — if `converter review` now lists that rule ID as
`checked` for this file kind, the tool owns it; drop the hand sweep and defer to the verbatim
output.

- **C-408 (error) — `ET` as boolean trigger.** Grep `\.ET\b`. A hit inside a comparison
  (`=`, `<>`, `>=`, `<=`, `>`, `<`) is a finding; `MOVE(EN := …, IN := <timer>.ET) => <named
  variable>` (HMI/diagnostic value read) is the documented permitted form.
- **C-103 (warn) — Set/Reset pairing.** Grep `SCOIL|RCOIL`; pair set and reset per target bit.
  A bit set in one block and reset in another (or never) is a finding; same block but distant
  networks gets the "ideally adjacent" note.
- **C-107 (info, soft) / C-402 (error) — edge memory discipline.** Inventory every edge-memory
  operand (previous-scan arrays like `RisingEdgeFlags[n]`, and named previous-scan/edge-result
  bits). Each array element: statically indexed only, written in exactly one place (C-107). Each
  edge bit: dedicated to one signal, one writer (C-402) — two writers of the same edge bit, or
  one bit serving two signals, is an error.
- **C-105 (error) — indexed access outside fenced blocks.** Grep for variable (non-literal)
  array indices. Any hit outside a fenced data-handling block is a finding. The fence is two
  conditions read from the file itself: the block's *name* shows what it is (`FB_Comms_…`,
  `FB_Recipe_…`, `DataHandling`-style) *and* its header comment states it contains indexed
  access / data handling. Literal indices (`Test[7]`, `RisingEdgeFlags[2]`) are not iteration —
  the `Test[n]` force arrays in Map FCs are pattern-sanctioned (input-mapping/output-mapping).
- **Sequencing cluster** — run on every block that declares or writes a `Step` member; if the
  project has no stepped sequence, one explicit n/a line:
  - **C-118 (error):** the phase is exactly one `Step : Int`, living in the block's caller-visible
    interface UDT (check the TYPE file) — never a bare private Static, never a
    `DB_Controls`/`DB_Settings` member.
  - **C-119 (error):** idle/home is step 0, and it is *returned to explicitly* on stop, fault
    recovery, and restart paths — Int's default value alone doesn't satisfy the rule.
  - **C-120 (warn):** steps ascend in multiples of 10; the block header carries the step legend
    (number, name, one line each).
  - **C-121 (error):** grep every write targeting the step tag (`=> IO.Step` and unqualified
    `=> Step`): each must be a `MOVE` whose `EN` contains `Step = <from> AND <condition>` — no
    other write mechanism. Multi-exit steps must be mutually exclusive *by construction*
    (lower-priority exit explicitly excludes the higher); where the exclusivity isn't visible in
    the expressions themselves, reason it out and label that part of the finding **judgment**.
  - **C-122 (error):** each step's max-dwell timer is a multi-instance TON in the block's own
    Static section, `IN` gated by `Step = <n>`, and — per the 2026-07-16 C-307 rewording — its
    `PT` traces to a settings member of the owning block's **own interface UDT** (follow the data
    chain through MS shadows; a PT whose chain starts at `DB_Settings` fails unless that timing
    is genuinely plant-wide with no single owning block). `Q` drives a fault (C-123), never a
    silent carry-on.
  - **C-123 (error):** hold and fault are two bits — the hold non-latching and never writing
    `Step`; the fault latched, cleared only by a named `FaultReset`, handled by an explicit
    recovery/abort transition.
  - **C-125 (warn):** the step legend exists (C-120) and every C-122 timeout fault bit lives in
    the interface UDT, not a private Static.
- **C-113, stated-paradigm sub-clause only (presence check):** the block header states the chosen
  sequence paradigm (stepped vs chained permissives). Presence is bucket A; whether the *choice*
  is right is Group 3.
- **C-001 (error), non-prefix parts.** Members/variables are short PascalCase with **no
  underscores** (2026-07-16 revision); the physical-IO tag format
  (`<DI/DQ/AI/AQ…><n>_<Equipment>_<Signal>`) keeps its underscores by design. Don't double-cite
  block-name prefix problems the tool already flagged under C-003.
- **C-006 (error) — English only** across names, titles, comments. A typo is not another
  language — record imported-real misspellings as context, never as C-006 findings.
- **C-201 (error), content half.** The tool checked *presence* of titles/header comments; you
  check **content**: a header that exists but states no purpose is a finding. Author/revision
  are not capturable in this IR export — say so; don't invent compliance or non-compliance.

### Group 2 — bucket-B cross-block checks (tables in the report)

Whole-project scope required; at reduced scope each of these gets an explicit "not checkable at
this scope" line instead.

- **C-127 (error) — reusable FBs reference no siblings.** Inside every reusable equipment FB
  body: grep `iDB_` and `CALL` — any hit is a hardcoded instance dependency.
- **C-308 (error) — the one-writer table.** Three sweeps, one table: (a) any logic write
  targeting `DB_Settings.*` in any block; (b) any logic write targeting an instance-UDT
  *settings* member (from an orchestrator: `=> iDB_<X>.<…><setting>`; from inside the owning FB:
  `=> IO.<setting>`); (c) **cyclic scan-copies** from `DB_Settings` into instance-UDT settings
  members — doc 06 names the test-project001 `FC_ControlMain` shape as the trap: the setting exists
  twice with a copy in between, and every faceplate edit silently reverts one scan later.
- **C-307 (warn) — settings home.** Table every settings member: where it lives × who owns it.
  A setting owned by a single equipment instance (including a sequencing block's own step
  timings) belongs in that instance's UDT; `DB_Settings` holds only what no single faceplate
  owns. (HMI-side faceplate binding: Group 4.)
- **C-304 (error) — the IO boundary.** Grep physical-IO tag references
  (`\b(DI|DQ|DO|AI|AQ|AO)\d+_`) and raw `%I`/`%Q` operands in every block that is not a Map FC.
  Logic touches buffer DBs only; mapping happens only in the Map FCs.
- **C-109/C-110 (warn) — OB1 shape.** OB1 contains only calls; input mapping first, output
  mapping last; Map FCs may be called directly from OB1 (documented exception, owner 2026-07-16);
  every other area goes through its own area-Main FC, which calls only its own area's blocks.
- **Startup/simulation cluster — C-305 (warn), C-111 (error), C-124 (error), C-403 (error).**
  Check as one unit: does `DB_PLC` exist with `Simulation : Bool` start-value FALSE (C-305)? Is
  simulation implemented at the mapping layer — input-map calls gated off by `-|/|-` on
  `DB_PLC.Simulation`, output-map FCs still running but every physical write gated per-point so
  outputs de-energize (C-111)? Inventory every S/R-written bit (`SCOIL`/`RCOIL`/`SET_BF`/
  `RESET_BF` grep) and cross-check each (except documented settings) against the dedicated
  startup-reset block — OB100-class, runs once, never cyclically (C-403). `Step`, holds, edge
  memory, and in-progress counters force-reset there regardless of retentivity, with C-124's
  carve-out honored: genuine fault latches needing human acknowledgement are deliberately
  excluded — don't flag their exclusion. **Absence handling:** if the project contains S/R-written
  bits or RETAIN run-state and no OB100/`DB_PLC` machinery exists anywhere, that absence is a
  real, project-scope finding filed against the generated integration — the imported-real block's
  own S/R usage stays context, but the *project's* missing reset block is a generated-integration
  finding, not the imported block's fault.
- **C-407 (warn) — timer homes.** Timers inside reusable equipment FBs are multi-instance
  (Static, in that instance's iDB); standalone timers outside equipment FBs live in `DB_Timers`,
  individually named; no scattered timer iDBs.
- **C-115 (warn) — handshake vocabulary.** Table the equipment FBs' UDT in/out names against the
  `enable` in / `ready`,`running` out vocabulary; a deviating block is the finding (imported-real
  deviations are context).
- **C-114 (error) — enable-chain acyclicity.** Identify enable edges by C-115 vocabulary plus
  the orchestrator's wiring (who drives whose `Enable` from whose `Running`/`Ready`); build the
  directed graph; check for cycles. **Classify every backward edge enable-vs-interlock before
  flagging** — an interlock feeding back against flow (downstream-blocked stopping an upstream
  belt) is exempt by the rule's own scope; if you can't classify it, raise a question, not a
  finding. The material-flow-direction half needs a process-topology artifact — declare it not
  checkable when absent. If the project has no chained-permissive architecture at all, say so
  explicitly ("n/a — no enable chain in this project"); never a silent pass.
- **C-116/C-117 (error) — direction modes.** Only if bidirectional/direction-mode equipment
  sections exist: one enable chain per direction, each independently acyclic; mode change
  permissive requires the affected section stopped at minimum. Otherwise one n/a line.
- **C-302 (warn) — no parallel loose-tag families.** Repeated per-equipment flat-tag families
  that should be one UDT × N instances. The per-point buffer-DB members are the documented
  mapping shape (db-inputs/db-outputs patterns) — not a family.
- **C-306 (warn) — DB_Controls surface.** Operator/system commands and mode selections only;
  logic-internal state living there is a finding.
- **C-501-residual (warn) — the DB-side packing scheme.** The tool's C-501 check covers the
  network-side slice-access conditions; the DB side is its NotApplicable gap: `DB_Alarms` exists,
  alarms packed into Words per category, named `<Category>Alarm0`, `<Category>Alarm1`, …
  extending by Word past 16 bits.
- **C-502 (warn) — alarm-FC skeleton.** `FC_AlarmsMain` calls one monitoring FC per category;
  the rule text says `FC_GeneralAlarms` and `FC_EStopAlarms` **always exist**. Their absence is a
  finding — flagged as an owner scale-down question for small/scratch projects, not adjudicated
  here.
- **C-503 (warn) — per-instance alarm monitoring** inside the equipment FB; alarm/faceplate data
  reaches the HMI through the UDT; category words not duplicated per instance. (HMI binding
  itself: Group 4.)
- **C-505 (warn) — alarm text format, title proxy.** Alarm-network titles follow
  `<Equipment> — <fault> — <action hint>` (equipment identifier per C-004, English per C-006).
  The actual HMI-side alarm texts are not in this medium — declare that half.

### Group 3 — bucket-C judgment (every finding says "judgment"; owner-ruling items separated)

- **C-113 (error — but genuine judgment; state the severity/checkability mismatch):** apply the
  memory test to the application. Write your reasoning as design-intent commentary **for owner
  ruling** — never a pass/fail finding on the paradigm choice itself.
- **C-504 (error — same mismatch):** knowing a fault is a *known consequence* of another alarm
  is design knowledge. Phrase candidates as "candidate missing suppression — verify intent,"
  naming the cause→consequence pair and the missing `-|/|-` + TON extension.
- **C-002 (warn):** does each block name describe its function?
- **C-004 (error):** identifier *consistency* (same equipment, same identifier, everywhere) is
  checkable; *compliance* needs the frozen equipment list — decline that half unless it's
  provided.
- **C-104 (info):** standard layout — inputs read first, outputs written once at the end.
- **C-106 (warn):** near-duplicate rung families across blocks that want a standard FB + UDT
  instead of drifting copies.
- **C-108 (warn):** a new block solving an already-solved problem — cross-check the `patterns/`
  index (`patterns/README.md`) and site blocks; fresh invention needs a stated reason.
- **C-202 (warn):** comments say *why*, not what. **Primary home: this skill.** (The title/comment
  *split* — C-203 — belongs to /review-simplicity; don't cite it here.)
- **C-405 (warn):** vendor-specific convenience instructions where a portable basic form isn't
  materially worse.
- **C-409 (error):** a parallel timer duplicating a cumulative span of a timer chain within a
  block; a needed cumulative value is derived, not re-timed.
- **C-507 (warn):** latched alarms / acknowledgement behaviour without a documented per-alarm
  exception — list as candidates for the exception list, not violations.
- **C-309 is an anti-finding:** settings are *not* range-validated PLC-side, by site policy —
  never flag missing clamps. Flagging one is itself a calibration error (see Calibration).

### Group 4 — standing declines (state these every run, each with its reason)

- **C-112 (error):** needs the WinCC HMI artifact — structurally out of this pipeline's reach
  (`docs/evidence/stage-S4.md`), so an error-severity rule this review can never validate; say exactly that.
- **C-303 (error):** optimized-vs-standard block access is a TIA block property the IR medium
  does not carry — not checkable here.
- **C-506 (warn):** severity-class assignment needs the project alarm list.
- **HMI-side halves** of C-503 (faceplate binding), C-505 (actual alarm texts), C-125 (what the
  HMI displays), C-307 (settings pages): not in this medium.
- **C-114's material-flow half:** needs a process-topology artifact.
- **C-004's compliance half:** needs the frozen equipment-identifier list.

## Overlap boundary with /review-simplicity

- **Citation split: this skill cites C-0xx–C-5xx only.** Never sweep or cite C-101, C-126,
  C-203, C-601–C-607, or the one-reading test — even when you see a clear breach, route it as
  one line in the "Belongs to review-simplicity" section.
- /review-simplicity routes startup machinery and alarm-tier observations *here*; this skill is
  where they land — don't bounce them back.
- Shared **facts** may appear in both tiers' reports (the same Snake_Case member names are C-001
  evidence here and C-607 evidence there); shared **rule sweeps** may not — one rule, one home.

## Calibration — do not flag these

- **C-309 clamps** (see Group 3): site policy, never a finding.
- **C-121 transition-MOVE verbosity** (`Step = n AND …` repeated per exit): convention-mandated,
  not duplication.
- **The mapping rail idiom and `Test[n]` force arrays** in Map FCs: pattern-sanctioned site
  shapes (input-mapping/output-mapping).
- **Siemens clock-memory tag names** (`Clock_0.5Hz` and family): the dot is a literal C-005
  breach, but these are Siemens' own default names — if a NotApplicable-gap hand-check surfaces
  them, record a **tension for the owner** (rename vs tolerate the vendor default), never a fix
  demand.
- **Renames/typos inside imported-real blocks:** context, not demands (and not C-006).
- **Documented exceptions, as written:** C-109's Map-FC direct call; C-301's three-part
  exception (the tool implements it — don't re-flag what it deliberately exempted); C-124's
  fault-latch carve-out.
- **Pattern-vs-rule tensions:** owner ruling, never adjudicated (the retrospective's precedent).
- **Tier-1 (functional) defects noticed en route** — a signal written nowhere, a polarity that
  can't work: report under "Tier-1 candidates for the functional review" with evidence. Finding
  it is in scope; ruling on it is not.

## Report structure

Use exactly this shape:

```
# Conventions review — <scope> (<date>)
Blindness: <blind | informed — reason>
Tool: <binary path> — invocation: `<exact command(s) run>`
Blocks reviewed: <list, each labeled generated | imported-real>

## Mechanical findings (converter review — verbatim, not re-derived)
<the tool's own output: per-file rule-status lines, findings, SUMMARY line — unedited>

## AI findings
### <Block> (<regime>)
- [C-xxx, <severity>, <bucket A|B|C>] <network/member>: <one-sentence defect>. <"judgment" if
  bucket C> <"hand-checked — tool reports NotApplicable for this content kind" where applicable>
  Evidence: `<quoted IR>`
  Rule text basis: <the doc 06 clause relied on, read this run>
  Suggested fix: <one sentence>

## Cross-block tables
<C-308 writer table, C-307 settings-home table, C-115 vocabulary table, C-114 enable graph/edges,
C-407 timer homes, OB1 shape — as applicable>

## Judgment items for owner ruling
<C-113 paradigm reasoning, pattern-vs-rule tensions, scale-down questions (e.g. C-502), C-507
candidates>

## Clean declarations
- <Block>: rules checked and clean: <list>

## Belongs to review-simplicity
- <one routing line per item; no C-1xx/2xx/6xx readability sweeps performed here>

## Tier-1 candidates for the functional review
- <evidence-carrying observations; no rulings>

## Not checkable here
- <Group 4 standing declines + any scope-limited declines, each with its reason>
```

Order findings most-severe first within each block. Every finding cites a rule ID; a defect no
C-0xx–C-5xx rule names cites the section principle it offends and is marked **"candidate rule
gap"** — that's how new rules get born here.

**Persisting this report.** Returned live in conversation, keep it whole. When it is saved as a
committed doc (a skill-validation record, a kept gate review), split it at write time: the large
verbatim block — the `## Mechanical findings` `converter review` dump, plus any full blind-run
transcript — goes in `docs/evidence/<name>.md`; the saved note keeps only the AI findings, tables,
verdict, and a link to it. Large raw dumps live in `docs/evidence/`, never inline in `docs/notes/`
or `docs/notes/stage-gates.md` (the standing FI-19/FI-20 convention, `docs/16-future-ideas.md`) —
born in the right shape, not split by hand later.

## Cost & scoping

A full-corpus run is **gate-review-sized**: the sibling simplicity review measured ~20 minutes /
~200k tokens over 21 files, and this skill sweeps more rules plus the mechanical pass — budget at
least that. For the inner loop (one block during generation), scope to: the block, its interface
UDT and iDB, OB1, and the DBs it touches — and give every Group 2 rule its "not checkable at this
scope" honesty line rather than a silent pass.
