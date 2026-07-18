# AITODO — working state / recovery doc

**Purpose:** this project's context can get interrupted (session limits, restarts). This doc is
the recovery point — read it first after any gap, before trusting your own memory of "what I was
doing." Keep it up to date as you work: update the checklist as items close, don't wait for a
clean stopping point. `docs/notes/stage-gates.md` is the slim status index of *closed* work (the
full per-stage narrative lives in `docs/evidence/stage-SN.md`, split out 2026-07-18 per FI-20);
this doc is the scratchpad for *in-flight* work only. When a task here finishes and is
documented/committed, delete it from this file rather than letting it accumulate.

## Recovery procedure (do this first)

1. `git status` / `git diff --stat` in the repo root — uncommitted changes are the ground truth of
   in-flight work, more reliable than any narrative.
2. Read `docs/notes/stage-gates.md`'s status table (which stage is active, what's signed off);
   for recent detailed history, read the tail of the active stage's evidence doc
   (`docs/evidence/stage-S6.md` while S6 is active) — the authoritative record of what's actually
   *closed*, with dates and evidence.
3. Read this file's "Current task" section below.
4. Cross-check: does the code in the diff match what this doc claims is done? If not, trust the
   code/diff and fix this doc.

## Project stage

**S3 — Comment generation, DONE — gate reviewed and signed off by the project owner, 2026-07-14.**
Full history in `docs/notes/stage-gates.md`'s status table and `docs/evidence/stage-S3.md`'s "S3
first/second/third proof" sections: exit criterion met three times over (`TimerSample`, `PerimeterSafetyAlarms` — Green-tier
reference corpus; `PlantAutoControl` — real JOB9002 content at full 20-network scale), the JOB9002
data-boundary approval explicitly extended to cover write activity first, two real converter gaps
closed (embedded-newline guard; the previously-untested "edit an existing title" scenario), one
real pre-existing corpus bug found and fixed (`TimerSample.ir`'s stale sidecar format), and the
other 13 committed reference-corpus files swept afterward to confirm that bug wasn't a wider gap.

**S4 — Convention review, DONE — gate reviewed and signed off by the project owner, 2026-07-15.**
Phase 1 (8 of ~50 rules) built, tested, live-piloted against the reference corpus, validated
against real untouched JOB9002 content. Full history: `docs/evidence/stage-S4.md` ("S4 Phase 1"
and "S4: real-content validation" sections).

**S5 — Structured data extraction, ACTIVE**, opened 2026-07-15, no work started — deprioritized
below S6/S7 per the owner (2026-07-15 "a workable S6 is the main goal"; 2026-07-17 "S7 is the
rush").

**S6 — Generation from plain language, ACTIVE**, opened 2026-07-15; the staged generation
pipeline (docs/15, ADR-0004) was adopted 2026-07-16 and largely built through 2026-07-17: all
four reviewer-adjacent skills exist and are blind-validated (`review-simplicity`,
`review-conventions`, `review-functional`, `gen-architecture`, alongside `explain-plc-block`);
the first pipeline artifacts live in `gen/test-project001/` (69-REQ requirements register,
architecture baseline, telemetry); test-project001's functional verdict stands at
45 implemented / 7 partial / 6 unimplemented / 10 disarmed / **0 contradicted** after fix wave 1
(2026-07-17, device compile 0/0, invariance proven, triple-reviewed). Exit criterion **not yet
closed**: counting rule ruled 2026-07-18 (D-4) — S6 exit = **ten fresh plain-language generation
requests**; fix waves are tracked *separately* and don't count toward the ten. **S6 generation-request
tally so far: 0 of 10** (fix waves excluded — keep this count here as the live tally). Full history:
`docs/evidence/stage-S6.md` entries 2026-07-16 → 2026-07-18.

## Current task: none — nothing in-flight

**Recently landed (2026-07-18 session — prune once stale; full record in git + the pointers named):**
- **Wired-argument CALL synthesis built + LIVE-TIA-PROVEN (`e5bfeab`, CHANGELOG).** `to-xml
  --synthesize` now mints wired Input/Output CALLs (types from the callee `.ir` via
  `CalleeInterfaceRegistry`, ADR-0001), unblocking reusable formal-parameter FBs. Proven end-to-end:
  the **genval2 ShredderControlSystem subsystem** (2nd blind validation, harder target) built as a 10-block
  reusable/C-304/C-127-clean subsystem and **compiled clean in TIA** (0 errors) — the wired CALL to
  `FB_ShredderControl` resolved and compiled. The AI produced a *better architecture* than the real
  single-instance source block, and the owner's "extend the converter" call paid off.
  - **Two converter follow-ups from that build — SCOPED PLAN queued:**
    `docs/notes/converter-synthesis-gaps.md`. (A) UDT-typed-param inline-nesting bug (small —
    bare-type-ref fix); (B) Word→Int CONVERT typing (bigger — a `TagTypeRegistry`, sibling of the
    callee-interface registry). Both PC-side; workarounds in use so nothing is blocked.
  - **genval2 Stage C DONE** (`docs/evidence/stage-S6.md`): strong — 31/35 REQs, compile-clean,
    architecturally better than the source, **avoided the OQ-3 bug** (a text-verifiable double-fault in
    the original), honored OQ-4. Three real findings to fix (a natural `gen-block-modify-fix` corpus):
    REQ-030 missing Hand-intervention gate (both reviewers), C-610 four undriven outbound bits, REQ-032
    non-retentive timer (REQ-vs-C-406 tension). Owner-ruling tensions: C-115, C-501 pack, C-403 OB100.
  - **Meta: the folded calibration lesson works** — C1 (review skills) honored OQ-4; C2 (manual
    comparison, no skill) repeated the guarded execution-order-from-text error re the *original*. → **FI
    candidate: a blind-comparison / answer-key-audit skill** carrying the calibration discipline.
    (C2's "the original is buggy on OQ-4/hours/TONR" claims are unverified — the genval1 N6 lesson.)
- **`gen-block-new` VALIDATED end-to-end (blind, vs a real block).** Sanitized JOB9002 `FilterUnitSystem` →
  `FilterUnitSystem` as a ground-truth answer key; blind pipeline (spec → gate-1 manifest → code →
  reviewers+compare) produced a compile-clean, functionally-correct block **cleaner than the site
  original** (fixed a latent rollover bug). Both reviewer-raised "defects" dissolved on verification:
  N10 = a spec-derivation error (REQ-020 mis-worded; code correct), N6 = a reviewer false positive
  (execution-order-from-IR-text; synthesizer interleaves ENO-chained pairs — artifact-proven correct).
  Full record: `docs/evidence/stage-S6.md` ("gen-block-new first validation"). **Does NOT count toward
  S6's ten** (validation fixture). Fixture lives in the session scratchpad (`genval-work/` +
  `genval-answerkey/`), reproducible from the sanitization maps.
- **Converter UDInt bug fixed** (`SidecarSynthesizer`, commit `46abd8f`) — found by that validation.
- **`gen-block-new` skill authored (docs/15 #7).** `.claude/skills/gen-block-new/SKILL.md` — the
  Build-stage coder: one gate-1-signed manifest item → one new compile-clean block's IR, reuse-first
  (CALL/patterns), C-126 grouping at write time, tag-status + compile gates, no self-review (Check
  stage runs fresh). Scope = code block + own interface + instance-DB scaffolding; shared DB/UDT + tag
  tables out of scope (named-gap stop). **Not yet validated** — next step is a real tier-(a)
  `motor-dol` instance against test-project001 (dispatched to `lad-coder`), which also becomes
  **S6 generation-request #1 of 10**. docs/15 step-6 status updated.
- **A-4 coding-skill build order ruled + `converter diff` built.** Owner ruled (2026-07-18):
  `gen-block-new` first (roadmap-fidelity; "S7 is the rush" retired), then `gen-block-modify-fix`,
  then `gen-block-modify-purpose`; `converter diff` built now in parallel. Recorded in `docs/15`
  step-6 row and `docs/evidence/stage-S6.md`. **`converter diff` is now built** (S7 invariance
  tool: per-network before/after, `--only` assertion → exit 1 on any out-of-scope change;
  sidecar-free readable-form compare, no separate normalizer needed). `src/converter/Converter/Diff/`,
  8 tests, README + CLAUDE.md updated; full converter suite 519 green; CLI smoke-tested against
  `ir/reference/PerimeterSafetyAlarms.ir`.
- **GenProject1 → test-project001 codename purge complete.** Docs/IR/mirror renamed; `simatic-ml/test-project001/`
  refreshed to the full 23 files by live-exporting `DB_PLC` + `OB100`. Live TIA folder keeps its on-disk
  name `GenProject1/` (CLAUDE.md codename note).
- **Hard rule 8 + `.claude/agents/lad-coder.md`.** All LAD/IR work (write/edit/review/explain `.ir`,
  the compile loop, `patterns/`) now goes through the `lad-coder` sub-agent; the main agent orchestrates
  and verifies, never authors. Recorded in `docs/evidence/stage-S6.md`.
- **FI-20 done:** `stage-gates.md` is now a 43-line status index; per-stage narrative lives in
  `docs/evidence/stage-S0.md`…`stage-S6.md`.
- **FI-19 done:** the three validation docs' verbatim §3 transcripts moved to `docs/evidence/…-blind-run-…md`;
  the split convention is baked into the review skills and stated canonically in `docs/15` ("Artifacts").
- Reference sweep clean afterward (128 files, zero dangling file refs).

## Outstanding works

**Priority steer (2026-07-18):** the earlier "S7 is the rush" framing is retired. **S5** is bumped up
for a proper look; **library-filling** is acknowledged as heavy work that may get a dedicated
harvest-assist skill (**FI-21**, `docs/16`) — but that's tomorrow's work, not now. Order below reflects
that steer.

**S5 — Structured data extraction (bumped up 2026-07-18):** ACTIVE, no work started — owner wants a
proper look / detailed plan here (entry criteria long met, S1 done; was parked below S6/S7, no longer).

**S6/S7 — generation & modification pipeline:**
- **S6 exit criterion not closed** — needs ten fresh plain-language generation requests (D-4:
  fix waves don't count; tally kept in the Project stage / S6 section above, currently 0 of 10).
- **S7-vs-S6 sequencing — fully ruled 2026-07-18 (A-4 + D-4).** Build order: `gen-block-new` first,
  then `gen-block-modify-fix`, then `gen-block-modify-purpose`; `converter diff` built in parallel
  (done — see Recently landed). Gate wording kept as-is (D-4): roadmap S7 entry stays "S6 done", so
  the path is build `gen-block-new` → close S6's ten → open S7. Both discussion briefings (a4, d4)
  resolved and deleted from `agent-tasks/`; full record in `docs/evidence/stage-S6.md`.
- **Coding skills:** `gen-block-new` **authored + VALIDATED 2026-07-18** (two blind runs — see Recently
  landed / `docs/evidence/stage-S6.md`). **`gen-block-modify-fix` authored 2026-07-18**, pending
  validation against the genval2 fix corpus (`gen/_validation/shredder/fix-requests.md`: FR-1 REQ-030,
  FR-2 C-610 blind-fixable; FR-3 REQ-032 needs-ruling). Its invariance gate = `converter diff --only`.
  Still unbuilt: `gen-block-modify-purpose` (`docs/15` build-order step 6, last of the three).
  **Tooling to mechanize modify-fix (from the plan):** `converter diff --only` (built); the D-6 scoped
  `SidecarSynthesizer` merge (recommended next — removes the whole-file-strip friction); FI-22
  whole-project cross-check (post-fix cross-block safety).
- **Follow-ups from the gen-block-new validation (queued, not yet done):**
  - Fold the **reviewer-calibration rule** into `review-functional`/`review-simplicity`: never infer
    TIA execution order from IR source-text order for ENO-chained MUL/ADD→CONVERT pairs (shared TEMP is
    safe by construction — the N6 false-positive lesson).
  - Fold **`gen-block-new` SKILL.md gaps**: `--synthesize` path + IR statement-kind ordering; the
    synthesizable subset (TON-only, MUL/ADD/CONVERT, magnitude-typed); Portal mechanics (absolute
    `.ap20`, background for slow opens); manifest-vs-C-003 naming resolution.
  - **Fixture housekeeping (owner's call):** whether to commit the Green `FilterUnitSystem` fixture as a
    reusable coding-skill validation corpus (and where), vs. leave it reproducible-from-maps only.
  - Optional fixture polish (tier-2, not defects): C-605 member comments, C-602 N1 mega-expression,
    C-505 alarm titles, C-601 `RunConfirmed`, and the N5/C-606 gold-plating (justify or remove).
- **Library-filling is real, heavy work** — no longer treated as purely passive S8 harvest. A
  harvest-assist skill is proposed (**FI-21**) to lower the per-pattern cost; see it for the design
  constraints (candidate-prep, not auto-admission).

**Deferred — decided in principle, owner's call on timing (`docs/notes/deferred-items.md`):**
- D-2 settings rework wave; D-6 converter can't add a statement to an already-exported network
  (whole-file-strip workaround proven; a scoped `SidecarSynthesizer` is the real fix); Q-04 per-type
  overcurrent setpoint numbers (owner has the real numbers); D-5 `chained-permissive-enable` blind-draft
  gap (needs a genuine blind target).

**Open register questions — need owner input (`gen/test-project001/requirements.md` Open Questions):**
- `Q-03` local/remote selector semantics (HMI-boundary — see FI-18), `Q-05` motor count / per-motor id,
  `Q-12` residual (zeroable-clock reset behaviour). Q-01/02/04/06–11/13–15 are resolved.

**Tooling backlog — non-blocking, PC-side (normal dev, not `lad-coder`):**
- `F-3(b)` = C-003's `iDB_<FBName>_<Instance>` sub-clause unenforced in `converter review`. Capture
  `HeaderAuthor`/`HeaderVersion`/`HeaderFamily` as IR header lines (would make C-201's author/revision
  mechanically checkable — still unbuilt). (`F-1` reporter count bug — fixed 2026-07-18, CHANGELOG.)
- **`converter review` now reviews sidecar-less blocks** — found + FIXED 2026-07-18 (CHANGELOG):
  review used to error "Expected a 'SIDECAR' section" on a `.ir` with no `SIDECAR` (e.g. `OB100.ir`),
  leaving that block invisible to convention review. `ReviewRunner.ReviewFile` now branches on the
  consolidated `IrParser.HasSidecarSection` like preflight does; the whole corpus reviews.

**S1 carryover — needs owner:** `Modbus_Master`/`Modbus_Comm_Load` live-compile blocked by a confirmed
general Openness limitation — source-side `JOB9002` fix vs. accept as a documented permanent limitation
(full detail in the "Open question carried over from S1" section below).

**Ideas awaiting a decision when there's room (`docs/16-future-ideas.md`, still open):** FI-06
(constructed edge-detection pattern), FI-08 (engineer-side proposed-tag approval path), FI-11
(presentation bundling for the final gate), FI-12 (persistent Portal session for the inner loop),
FI-17 (explanation sidecars), FI-18 (HMI-interface skill — would give Q-03/13/14/15-class questions a
home). **From the 2026-07-18 skill/tooling audit:** FI-22 (whole-project cross-check review mode over
`ProjectIndex` — highest-leverage net-new), FI-23 (`explain-plc-block` structural-fingerprint helper),
FI-24 (`gen-architecture` helpers — **`tagstatus` built 2026-07-18**, CHANGELOG; provenance wrapper
still open); FI-09 (**C-408 + C-001 mechanized 2026-07-18** — `.ET`-in-comparison, and
member/variable PascalCase across DB/iDB/UDT/block-vars; C-121/C-118–125/C-103/C-107/C-402 still
open). FI-13/14/15/16/19/20 done; FI-04 rejected; FI-01/02/03/07/10 parked.

`docs/07-pattern-library-spec.md` no longer mentions a `tests/`/S9 hook — revisit if S9 ever opens.

## Open question carried over from S1 (still needs the project owner's input)

- **`Modbus_Master`/`Modbus_Comm_Load` — built and unit-tested, live compile blocked by a
  confirmed general Openness limitation, not a converter bug.** Both instructions' own port/wire/
  parameter modeling is verified correct. What's left unverified is TIA accepting a standalone
  instance DB for either instruction in `SampleProject`: `create-instance-db` deterministically
  assigns an invalid `DB0` for `Modbus_Master_DB`, and neither `Modbus_Master_DB` nor
  `MB_Master_Comm` is exportable from `JOB9002` (invisible to `SW.Blocks` — same class as
  `CycleDelayReset`; full detail in `docs/notes/openness-quirks.md`). **Ask**: source-side fix in
  `JOB9002` (the only path that resolved `CycleDelayReset`), or accept as a documented permanent
  limitation?

**Deliberately deferred, not a bug to chase:**
- `Main` (OB1) full round-trip (write path) — TIA's own template block; OB support never a goal;
  owner's call. (Read path works: OB1 exports/converts fine, proven 2026-07-16.) **Narrowed
  2026-07-17:** this is now specifically about `Main`'s own template quirks, not "OB write support"
  generally — a hand-authored, non-`Main` OB (`OB100`, `SECONDARYTYPE Startup`) round-tripped clean
  end-to-end (startup machinery task, 2026-07-17: `converter to-xml --synthesize`,
  `openness-cli import`/`compile`, re-export readable-identical). `Main` itself stays deferred.
- Modbus multi-instance form — revisit only on a real grounded example.
- `WAIT`/`Jump` — closed as not needed (owner, 2026-07-14); grounding preserved in `ir/SPEC.md`.

## Sanitization maps built and kept

`sanitization/` (gitignored): all 8 dependency FBs' own maps, plus `PlantAutoControl.map.json` and
per-instance maps for all 26 DB/tag-table roots; `Kestrel Shredder Systems.map.json` (extended 2026-07-16
with two abbreviation pairs found during register drafting). Reusable directly for follow-on work
against the same real sources.
