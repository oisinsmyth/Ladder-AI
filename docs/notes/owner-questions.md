# Open questions for the owner

**Scope: PROJECT-WIDE questions only** (owner ruling, 2026-08-06). Tooling, conventions, the
roadmap, the data-boundary regime itself — anything about how the tool should work. **Questions
about a live engineering job do not belong here and must never be written here**, because this file is
committed to git and a live run's questions are written in that job's own vocabulary (tags,
equipment, alarm IDs, process detail). Each live run keeps its own open-questions doc *inside its
job folder*, which is gitignored — see `docs/13-data-boundary.md`'s "Live runs" section. A question
that is genuinely both gets split: the general half here in generic terms, the job-specific half in
the job folder. Policy questions *about* the live-run regime are project-wide and do belong here —
the test is whether answering it requires naming anything from a site's plant.

**Purpose.** This doc exists to consolidate a *large batch* of accumulated questions into one
priority-ordered list when there are too many for ad hoc handling — it is not a permanently
populated running log. When opened for a new batch, it holds everything currently waiting on the
owner's word, grouped and prioritized (direction/functional/rule-book/project-scope/register/
tooling, or whatever grouping fits that batch), each item citing where the evidence lives. An
item leaves the list only by a recorded answer — a pointer to where it actually landed (a doc
update, the requirements register, a dated `stage-gates.md` entry) — never silently. Once every
item in a batch is closed, deferred to `docs/notes/deferred-items.md`, or routed to its own
dedicated conversation/briefing doc, the batch is cleared out and the doc goes back to empty until
enough questions accumulate to justify opening it again. **Actionable, agent-dispatchable work
that comes out of a resolved batch** (a ruling that means code needs writing, or a discussion that
needs a dedicated conversation) gets its own task file in `agent-tasks/` — see that folder's
`README.md` for the format and the concurrency/Portal-queue rules. This doc is where questions get
answered; `agent-tasks/` is where the resulting work gets dispatched.

**TWO BATCHES ARE BELOW** (2026-08-23 workbench, then the 2026-08-05 audit's 13 `decide` items).
Read both before concluding nothing is waiting on you.

🔴 **THAT LINE IS NOW FALSE, AND IT FAILED THE SAME WAY THE THIRD TIME. UPDATED 2026-08-24.** It read:
*"AS OF 2026-08-23 THE WORKBENCH BATCH HAS NOTHING BLOCKING IN IT. B4 and B5 are both answered and both
are recorded below with their answers; Q2 is open and blocks nothing. The 13 audit items below are the
ones actually waiting on you."* **Every clause of that is still true of B4, B5 and Q2 — and two new
decisions arrived on 2026-08-24 that nobody wrote down here** (see **D1** and **D2** below). ***Third
firing of the process finding at the head of the workbench batch, and the third distinct direction:
first the questions were missing, then an answer was, now a heading that was accurate the day it was
written outlived the state it described.*** ➜ **D1 blocks a `gen/` edit that is being deliberately
withheld pending your word, so the board is not clear.** The 13 audit items below are still waiting on
you as well.

**Last cleared:** 2026-07-17. The 2026-07-17 batch (~50 items, sections A–F, three rounds of
owner answers) is fully resolved. Where things landed:
- Closed items: `docs/06-lad-conventions.md` (naming/structure/data/alarm/simplicity rules, new
  C-115/C-117/C-121/C-123/C-128/C-507/C-604/C-605/C-608–C-611), `gen/test-project001/requirements.md`
  (register resolutions), fix-wave-1-reviews.md (B-docket owner verdicts — file retired in the
  2026-07-17 test-project001 declutter once its rulings were folded into doc 06 and the
  `stage-gates.md` history below),
  `docs/15-generation-pipeline.md` (skill split, build order), `.claude/skills/gen-architecture/
  SKILL.md` (reuse-first carving), `docs/16-future-ideas.md` FI-18.
- Deferred items (decided in principle, timing only): `docs/notes/deferred-items.md`.
- Discussion items A-4 (S7 ordering) and D-4 (stage-gates/gate structure): both **resolved
  2026-07-18** and their briefing docs deleted from `agent-tasks/` — outcomes recorded in
  `docs/evidence/stage-S6.md` ("Coding-skill build order ruled — A-4" and "Stage-gates structure +
  S6/S7 gate + fix-wave tally ruled — D-4").
  The B-docket/C-item implementation work from this batch also lives in `agent-tasks/` now, as a
  Portal-gated queue (`agent-tasks/README.md`) — see that folder for anything code-shaped.
- Still-unanswered items with no clarification blocking them: register questions Q-02/Q-06/Q-09
  stay tracked in `gen/test-project001/requirements.md`'s own Open Questions section (their canonical
  home); the C-003 tooling-enforcement backlog item stays in `AITODO.md`'s small/tooling queue.
- Full dated history of every ruling in this batch: `docs/evidence/stage-S6.md`'s "owner-questions
  batch pass" / "round 2" / "round 3" entries (2026-07-17).

---

# OPEN BATCH — the workbench questions (2026-08-23)

🔴 **A PROCESS FINDING FIRST, because it is worth more than either question below.** Until
2026-08-23 this file — *the file that exists to hold open owner questions* — **contained neither of
the two questions that were actually open.** Both lived in `AITODO.md`'s in-flight section only, and
one of them (B4) had already been **answered and built** while still listed there as open. A session
looking for open owner questions looks here first, finds a batch cleared in July, and concludes there
are none.

⚠️ **AND IT RECURRED THE SAME DAY, IN THE OPPOSITE DIRECTION.** Hours after this batch was opened to
hold the missing *questions*, it was holding a missing *answer*: B5 was answered, this file's heading
still read *"The one that is open"*, and the only entry under it was that question. **The failure is
not "questions go missing" — it is that nothing makes updating this file part of asking or answering
anything.** Both directions cost the same thing: a session plans against a blocker that is not there,
or against a clear board that is not clear.

⚠️ **LABEL COLLISION — THERE ARE NOW THREE `B5`s ACROSS TWO VISIBILITY DOMAINS. DO NOT ADD A FOURTH,
AND DO NOT MERGE ANY TWO.**

| label | where | what |
|---|---|---|
| **`B-5`** (hyphenated) | the 2026-08-05 audit batch, below | F-46 — extend design-philosophy §10 to the spec layer |
| **`B5`** (unhyphenated) | here, `AITODO.md`, `docs/notes/workbench-phase6-plan.md` | which block becomes the third conformance lane — **the entry immediately below** |
| **`B5`** | the live job's own gitignored open-questions doc | an unrelated job question that happens to reuse the letter |

`B-4` / `B4` collide the same way (S0's gate; the stray-Portal ruling). **Neither pair answers the
other.** Anything new written here should use a label that is not `B<n>`.

## 🔴 The two that ARE blocking — gate 5c, added 2026-08-24

**Context, once, for both.** `57432c6` / `915b6e8` built gate **`5c map authority`**: the observability
map a conformance submission is judged against is derived from the coordinator's binding document, and
that document now carries a `declaredBy`. 5c compares it against the block author and against every
vector author. **Unrecorded reads NOT CHECKED, never a pass — and one NOT CHECKED makes a submission
inadmissible.** Both committed bindings currently record nothing, so both read NOT CHECKED today.
**The lane deliberately did not fill the field in, because the value is the thing in question and
inventing one is the hard-rule-3 shape.** *(Labels are `D1`/`D2` and not `B<n>` — see the collision
table above.)*

- 🔴 **D1 · Filling `declaredBy` truthfully on the hx-corpus binding REFUSES that wave. Does it stay
  admissible?**
  `gen/test-project001/hx-corpus/harness-binding.md`'s own header says it: *"This one was written by the
  **block author**, on the coordinator's instruction, because for this corpus the two halves are the
  same person: I wrote the blocks and I wrote the register they are specified in. **That is itself a
  finding.**"* **So the honest value for `declaredBy` is the block author, and 5c refuses on it.**
  ***That is the gate working, not the gate misfiring*** — a correlation the prose has admitted in
  writing since the day the file was written, and never once mechanically enforced until now.
  **What is being asked:** does that wave stay admissible (an explicit, recorded exception with an
  expiry), or does the corpus get a third-party binding author before it runs again? **Nothing is
  written into `gen/` until you rule**, because the alternatives are inventing an identity or leaving
  the field blank, and blank is the NOT CHECKED that already makes it inadmissible.
  *The hopper binding is the milder case of the same question:* its prose names an **owner as a role**
  and no machine identity, so there is no truthful string to write there either.

- 🔴 **D2 · A multi-coordinator batch cannot be attributed at all. Plural `declaredBy`?**
  `BatchPlanner` merges lane bindings. Identical declarers across lanes collapse losslessly and keep 5c
  a live verdict on the ordinary single-coordinator batch; **different declarers — or one silent lane —
  leave it null, and the batch reads NOT CHECKED.** Two alternatives were **ruled out on evidence, not
  taste**, and are recorded so they are not re-proposed: the batcher stamping its own name **fails
  open** (where a lane's coordinator is also that lane's block author, 5c compares batcher against block
  author, finds a difference, and passes a real conflict); a **joined string is worse than nothing**,
  because the comparison would match neither party — a conflict rendered invisible by formatting.
  **The correct answer is a set.** That needs a plural wire field visible to gate 0b and every reader,
  plus a plural `MapAuthor` — **a schema change with a blast radius**, and **no multi-coordinator batch
  is known to have occurred yet.**
  **What is being asked:** build the plural field now, or leave the case reading NOT CHECKED until a
  real multi-coordinator batch exists? Until you rule it stays NOT CHECKED rather than guessed at.

## The one that was open — ✅ **ANSWERED, and the lane is NOT blocked on you**

- ✅ **B5 (workbench) · Which block becomes the third conformance lane? — ANSWERED: `FB_SiloSequence`.**
  🔴 **This entry read *"Blocked on this and nothing else"* until 2026-08-23, after the answer was
  given.** So did `AITODO.md:88` and `docs/notes/workbench-phase6-plan.md:150`, `:367`, `:395`. **Three
  tracked files told a session the third lane was waiting on one sentence from the owner, and it was
  not.**
  🔴 **The process finding above recurred IN THE OPPOSITE DIRECTION, inside the same batch.** Last time
  the *questions* were missing from the file that exists to hold them; this time the **answer** was.
  A file that records questions and not answers is a file that manufactures blockers.

  **Where the lane actually stands: not blocked on the owner — blocked behind two decisions of its
  own.** Both were taken up in the live job's work and **both are recorded only in that job's
  gitignored folder**, so their substance is not written here (`docs/13-data-boundary.md`, "Live
  runs"). **The SHAPE, which is what a session planning against this file needs:**

  1. **A scope decision about how deep the lane observes, and it must be taken BEFORE the lane is
     built** — it changes the interface, the drive surface and the register bill, none of which is
     cheap to revise afterwards. The cheap option's claims rest on the harness asserting values it
     does not itself compute; the thorough option's register bill **overruns the 125-register FC03
     read limit outright**, which is a design-time refusal rather than a slow path.
  2. **Part of the intended assertion set for this lane has no implementing logic to test**, so a
     planned sub-slot cannot be built at all and the campaign's reachable coverage ceiling is
     materially lower than the figure on record. ➜ **The right output is a GAP REPORT, not a slot** —
     specified-and-unbuilt behaviour is worth more to the site than more Passes, and it costs one
     document rather than one deployment. ⚠️ **It must be confirmed by someone other than the finding's
     author** (D6), and it is **M-21 recurring**: *"cannot be reached"* recorded where *"there is
     nothing to reach"* is true.

  ➜ **The specifics of both — counts, the behaviour involved, the register arithmetic — are in the job
  folder and stay there.** This entry exists so that a session reading the knowledge base stops
  planning against "blocked on the owner", not so that it can plan the lane from here.

  ⚠️ **What is still true and unchanged:** two lanes have run — off one deployment, on the rig
  (`d289a27`) — **two is not N**, and every claim the batching design makes is a claim about N. Phase 4
  built `SlotFcGenerator` and `StimShellGenerator` (`d3d5ab1`) specifically to make a third lane cheap.
  A third lane is still the right next **rig** event, and it still needs Portal, a deployment and rig
  time. It is now gated on the two decisions above rather than on a name.

## The one that is answered, recorded here so the file holds the answer too

- ✅ **B4 · May `openness-cli` ever close a stray Portal?** — **RULED 2026-08-23: *"save where you can,
  then close."*** Quoted verbatim where the implementing code lives,
  `src/openness-cli/OpennessCli/Openness/PortalClosePlanner.cs:62-63`; built the same day as
  `openness-cli portal-close` (`f9abeb1`).
  **The ruling is MORE permissive than the assumption it replaced.** The prior working assumption was
  that a process which cannot be saved must not be closed; the ruling carves that case out with *"where
  you can"* instead — an Openness-invisible Portal cannot be asked to save and is closable anyway, and
  that branch is reported by name (`TerminateUnsaveable`) rather than folded into a rolled-up "closed"
  count. What the ruling *forbids* is the converse: a save that was possible and **failed** stops the
  terminate, and the process is left running (`PortalCloseExecution.cs:167-173`).
  ⚠️ **Its `--yes` path has never been run against a live Portal** — the planning half is pure and
  unit-tested, and the **plan form has now run live twice** (2026-08-23, exit 10 both times, nothing
  terminated). The terminating half still has no live evidence. Documented in
  `src/openness-cli/README.md` (*"`portal-close` — closing a stray Portal"*) and CLAUDE.md's index.
  ✅ **CLOSED 2026-08-23 — the attached-session hole this entry warned about is fixed.** It read: *"the
  planner cannot consult `AttachedSessions`, so an agent attached to an empty Portal is
  indistinguishable from abandoned pileup."* `AttachedSessions` was probed and **it distinguishes them
  decisively, across processes** — a sweeper that is not the holder can see the holder's pid. An empty
  Portal with a live session is now `Leave` unless named by `--pid`, the same protection an in-use
  Portal already had, and the refusal names the holding pid.
  🔴 **The residual, which is narrower but real:** an `OpennessInvisible` process is not in
  `GetProcesses()` at all, so it can never report attachment and **is swept exactly as blindly as
  before**, protected only by the 5-minute age floor. Treating "unreadable" as "attached" would make
  every uninterrogable process permanently unsweepable, so it was deliberately not done.

## Not blocking, but it decides a scope

- **Q2 · Element-table width** (`docs/18-project-workbench.md` §7, *"Q2 — Element-table width"*).
  Which mirror element types must be observable for real blocks? `MirrorValueType` has exactly four
  members today — `Unstated`, `Bool`, `Int`, `Time`
  (`src/harness/Harness.Map/CopyLayer.cs:18-52`) — so an unsupported
  type is **inexpressible rather than mishandled**: it surfaces as an unparseable binding, which is the
  safe failure. `Real` and `DInt` look unavoidable; UDT members and array elements are a much bigger
  question.
  → **Needed:** nothing is blocked. Answering it converts "widen on demand, the day a real block needs
  a type, with that block's actual type in hand" into a scoped item with a decided type list. Left
  unanswered, the element table should **not** be built speculatively — building it now answers the
  cheap half by fiat and leaves the expensive half where it is.

---

# OPEN BATCH — 2026-08-05 audit: the 13 `decide` items

Opened 2026-08-05 by the audit fix wave. Source: `docs/audit/2026-08-05-full-project-audit.md` and its
fix list. The audit found 53 items; the 36 mechanical `fix` items and 4 `accept` notes were executed
without you. **These 13 need your word** — an auditor must not resolve governance, scope or
data-boundary questions unilaterally, so none of them were touched.

Each item cites where its evidence lives. Answer in any order; nothing here blocks anything else.

**Claim discipline (this folder's own lesson, 2026-07-17):** a one-time sign-off needs the same
claim/release care as a Portal slot — two concurrent sessions once asked you the *same* gate-1
question and got two *different* answers. So: this batch is the single place these 13 get asked. If a
session wants one of these decided, it reads the answer from here rather than re-asking you.

## A — Governance (highest value; A-1 is the cheapest big win on the list)

- **A-1 · F-05 · The risk register has never been reviewed.** `docs/09-risk-register.md:3` says
  "Reviewed at every stage gate"; `git log` shows **one commit, 2026-07-10** — zero edits across four
  gate sign-offs and 26 days. Four risks have materialised while still rated prospectively: **R-04**
  (AI hallucination — a graded REGRESSION shipped, and hard rule 3's own gate had a live hole until
  2026-08-05), **R-06** (Openness friction — heavily mitigated since, none of it recorded), **R-08**
  (cloud-AI/NDA — its mitigation says the boundary would be agreed *before* real data flows; data has
  flowed since 2026-07-10 and now includes Red-confidentiality material), **R-11** (bus factor).
  Assumption **A-02**'s own checkpoint ("test before S3 leans on this") was passed without a record,
  though `compile-error-playbook.md`'s 21 real entries are de-facto verification. **A-04** is still
  unverified while the pipeline runs on real jobs.
  → **Needed:** one review sitting. Mark what has materialised, re-rate or re-word, record a review
  date, and close or restate A-02/A-04. *An auditor cannot re-rate your risks.*

- **A-2 · F-07 · The four-rung spec pipeline has no ADR.** It was designed, built, validated twice and
  exercised on three runs; `docs/03-development-plan.md:30` says decisions with lasting consequences
  get an ADR "even if only a paragraph". It changes **where signals and booleans may enter the
  artifact chain** and adds four formats now parsed as contracts by five converter checks — and it
  runs *alongside* `gen-architecture`, a relationship asserted only in CLAUDE.md prose. ADR-0004 is
  still the accepted record of "the" generation pipeline.
  → **Needed:** may I write `adr-0007-structured-spec-pipeline.md`, and at what status? If you judge
  the design still provisional, **Proposed** is the correct record — the absence of one is not.

- **A-3 · F-16 · `docs/13-data-boundary.md` still opens "DRAFT — needs a decision".** Its header says
  a decision is needed *"before real project data flows to any AI"*, and `:24–26` still states the
  interim rule "Only Green-tier content goes near Claude Code" — while `:240–307` grants, as a settled
  decision, full unsanitized working access to Red-confidentiality material, and a live job runs under
  it. No `adr-0003-*.md` exists; `adr-0004:5` still records the slot as deliberately reserved.
  → **Needed:** either take the decision and record ADR-0003, or re-affirm the reservation with a
  dated note. Either way the header should describe the doc as it now is.

- **A-4 · F-17 · What happens to the older per-project approvals?** The live-runs regime says live runs
  "do not follow" the older Amber process, but not whether the existing named approvals are
  superseded, still live in parallel, or frozen. The 2026-07-29 entry recorded the precedent that
  matters — *"every prior approval here was scoped to advancing a stage of **this** project … This one
  is not — it is production engineering output for a real job"* — and the live-runs section
  generalises exactly that shift without saying so. **No breach was found**; the gap is definitional.
  → **Needed:** which regime governs new work on the previously-approved projects, and are the
  real names already committed in Green-tier content re-affirmed as-is or slated for genericization?

- **A-5 · F-53 · May the governance ledger name the job code?** `docs/13:317–318` records the live job
  by its code, in the same two lines that say *"Job identity, scope and all content live in the job
  folder only."* This was the **only** real-identifier hit in tracked content across a 456-term sweep
  of every tracked file and the full git history — everything else is clean, so this is a wording
  question, not a leak.
  → **Needed:** is a bare job code permitted in the ledger (and nothing else — no site, plant,
  equipment or tag), or should the register move to codenames as `test-project001` did?

## B — Process & convention (small, but each closes a recurring finding)

- **B-1 · F-18 · Hard rule 8 cannot be evidenced.** `docs/notes/gen-telemetry.md:16`'s row format has
  **no field recording whether a run was dispatched to `lad-coder`**, the dispatch board is empty, and
  `AITODO.md:41–43` records that agents read the rung SKILL.md files directly rather than via the Skill
  tool. So the charter's method for auditing the project's central process control cannot be applied.
  → **Needed:** add an `agent` field (`lad-coder` / `inline` / `manual`)? Or should rule-8 compliance
  be evidenced some other way — self-attestation in telemetry may not be worth much.

- **B-2 · F-47 · Are audit artefacts deliberately not changelogged?** Zero CHANGELOG entries for any of
  three audits or the charter, consistently, across five weeks. The practice looks intentional; it has
  just never been written down, so every audit re-derives it as a finding.
  → **Needed:** declare it a convention in `docs/audit/README.md` (recommended — closes it
  permanently), or start logging them.

- **B-3 · F-35 · The ADR lifecycle rule is contradicted by every entry that follows it.**
  `docs/16:9` says promotion "requires an ADR"; FI-13/14/15/16 each independently state an exception
  the rule does not contain, and 11 IMPLEMENTED entries never went through `Accepted` at all.
  → **Needed:** confirm the carve-out — ADR required for roadmap/scope or cross-cutting design
  changes; tooling-, docs- and convention-only builds owner-directed and recorded in the entry.

- **B-4 · F-44 · S0's gate.** Still "ACTIVE — gate review pending" after 26 days, and its own TODO says
  sign-off must happen *"before … starting S1"* — while S1–S4 are all signed off. The situation is
  disclosed at the top of the file, so this is unreconciled rather than hidden.
  → **Needed:** sign it off retroactively on the 2026-07-10 evidence, or keep it open and drop the
  now-false precondition clause.

- **B-5 · F-46 · Extend design-philosophy §10 to the spec layer?** §10 already says ambiguity is a hard
  error, not a warning — *"silent best-effort conversion is how a debounce timer becomes a latch"* —
  but is scoped to the converter. The autopsy's root cause is the same principle violated one layer up.
  FI-37/38 enforce it at the spec layer; the philosophy doc doesn't say so, which makes the rung rules
  read as ad hoc rather than derived.
  → **Needed:** may I add one sentence? *Editing a founding principles doc is your call even when
  purely additive.*

## C — Corpus & scope (C-1 gates the other two)

- **C-1 · F-19 · Is `ir/PlantAutoControl-bench/` in the committed corpus or not?** 34 tracked `.ir` files —
  the largest IR corpus in the repo, and the one the entire four-rung pipeline was validated against —
  with **no** matching exports and no entry in either round-trip or drift test. Nothing anywhere states
  whether that is deliberate.
  → **Needed:** if it is a spec-pipeline fixture never taken through TIA, this is a one-paragraph doc
  fix. If those blocks were ever imported/compiled, it wants exports committed and both tests extended
  — which needs Portal and a `lad-coder` dispatch.

- **C-2 · F-20 · Grow the committed corpus to close the live-TIA gap?** The two live-cycle proofs are
  still un-automated `static` classes never run by `dotnet test`; `ir/reference` has not grown since
  2026-07-20. *(The cheap half — making the silent exclusion loud — is being done now without you.)*
  → **Needed:** invest in genericized blocks covering the uncovered construct families, or accept the
  gap and keep the live proofs as point-in-time narrative?

- **C-3 · F-48 · One re-export hand-off.** `simatic-ml/test-project001/DB_Settings.xml:288` carries a
  rename residue pointing at a directory that no longer exists. Hard rule 7 forbids hand-patching the
  XML, so the fix is a source-side correction plus re-export — a `lad-coder` dispatch that also
  touches the `ExportDriftDetectorTests` known-drift baseline.
  → **Needed:** worth a Portal cycle, or accept it as a frozen-export residue and record that so the
  next audit stops re-deriving it?

---

**Pre-resolved, so you are not asked:** F-37 (warning enforcement) was contingent on the real warning
count — measured 2026-08-05: the Release builds report **0 warnings**, so it became a plain fix.
