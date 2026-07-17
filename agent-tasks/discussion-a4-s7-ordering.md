# Discussion task — the S7-first ordering (originally owner-questions A-4)

**Purpose of this doc:** the owner asked (2026-07-17) for a doc another agent/conversation can use
when they're ready to go over this — they want "a greater back and forth" than a single written
answer supports. This is that briefing. **Whoever picks this up: don't rule on this yourself —
walk the owner through the decision points below and record whatever they decide back into
`docs/15-generation-pipeline.md`'s build-order table, plus a dated entry in
`docs/notes/stage-gates.md`.** (Note: `docs/notes/owner-questions.md`, the doc this was originally
filed against, has since been cleared out and reset — it's a reusable batch-questions doc, not a
permanent log; this file is now the durable record of the open item, not that one.)

**Multi-agent note:** this is a conversation task, not code — no Portal contact, not part of
`agent-tasks/README.md`'s Portal queue, safe to run at any time alongside anything else in that
folder. Check `agent-tasks/README.md` for whether this is still listed as open before starting
someone may have already run this conversation.

## Where things stand (as of 2026-07-17)

Already ruled and applied — **not** part of this discussion, just context:
- **A-1** — `gen-architecture`'s carving model rewritten for reuse-first (whole library block →
  pattern-composed → modified library block → freeform), matched against cached library/pattern
  summaries. Done: `.claude/skills/gen-architecture/SKILL.md`.
- **A-2** — programming split into three Build-phase skills (`gen-block-new`,
  `gen-block-modify-purpose`, `gen-block-modify-fix`) plus a shared modification-choreography
  reference, pulled forward to build-order step 6 (ahead of the remaining analysis skills). Done:
  `docs/15-generation-pipeline.md`.
- **A-3** — once `gen-block-new` exists, manual coding needs a per-case owner waiver. Done:
  `docs/15-generation-pipeline.md` Boundaries section.

**Still unbuilt:** all three coding skills. The current 5-step manual loop (CLAUDE.md) is the
working seed, still legal to use until `gen-block-new` exists (A-3's bound).

**What's NOT decided:** the *internal* sequencing of build-order step 6 — which of the three
skills gets built first, whether tooling (`converter diff`) comes before or alongside them, and
how this interacts with closing out S6's exit criterion. That's A-4.

## The owner's original proposed critical path (raised 2026-07-17, not yet ruled)

(a) `converter diff` tooling now, in parallel — S7's entry requirement, formalizes the invariance
proofs already being done by hand.
(b) The modify-skill pair (`gen-block-modify-purpose`, `gen-block-modify-fix`) built and exercised
against the B-docket (six owner-ruled tier-1 defects, `gen/GenProject1/fix-wave-1-reviews.md`).
(c) D-4 ruling (S6 exit-criterion tally — see the separate D-4 briefing,
`agent-tasks/discussion-d4-stage-gates-review.md`), then close S6's ten requests via small deliberate
asks — brings `gen-block-new` up; each approval feeds S8 harvest.
(d) A-1's carving-model rewrite, done in passing (now already done — see above).

Owner's context for *why* this ordering, stated 2026-07-15/17 and carried in persistent memory:
the pattern library is **purposely thin** — it grows organically via S8 harvest, never a library
build-out campaign — and **S7 is "the rush"**: safe, contract-bound modification is the capability
that takes production/bug-fix load off the owner directly, which is the whole point of building
this tool. New-block generation (S6, `gen-block-new`) matters, but modification is the more urgent
unlock.

## Decision points to walk through

1. **Does `gen-block-new` need to exist before the modify pair gets real practice?** Tension: A-3
   says manual coding needs a waiver once `gen-block-new` exists — but the B-docket (the intended
   validation corpus for the modify skills) is *fix* work, not new-block work. If the modify pair
   builds first (per the owner's original ordering), does the B-docket get fixed manually in the
   meantime (under an A-3 waiver, since no coding skill exists yet at all), or does everything wait
   for whichever skill builds first?
2. **Build order among the three:** `gen-block-modify-fix` (small, scoped, the B-docket's own
   shape) vs `gen-block-modify-purpose` (bigger, interface-changing) vs `gen-block-new` (S6's own
   deliverable, roadmap-named). Cheapest-first argues for `gen-block-modify-fix`; roadmap-fidelity
   argues for `gen-block-new`.
3. **`converter diff` timing:** build now stand-alone (low risk, no dependency), or fold into
   whichever modify skill needs it first as a byproduct?
4. **Dependency on D-4:** does closing S6's ten-request exit criterion actually block starting S7
   work, or can S7 (modify skills) develop in parallel with S6 still open? The roadmap's exact
   S6→S7 gate wording is worth re-reading together (`docs/02-roadmap.md`).
5. **Horizon item, not urgent:** S7 on private engineering projects (vs. GenProject1's sandbox) needs a
   `docs/13-data-boundary.md` approval extension — a new activity class. Worth flagging even if
   not resolved this session.

## Where to record the outcome

- `docs/15-generation-pipeline.md`'s build-order table (step 6 row) — once sequencing is decided.
- A dated entry in `docs/notes/stage-gates.md`.
- Delete this file (or mark it done at the top) and remove its row from
  `agent-tasks/README.md`'s task index once the conversation has happened and the outcome is
  recorded above — don't let a completed discussion task linger here.
