# Open questions for the owner

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

**Last cleared:** 2026-07-17. The 2026-07-17 batch (~50 items, sections A–F, three rounds of
owner answers) is fully resolved. Where things landed:
- Closed items: `docs/06-lad-conventions.md` (naming/structure/data/alarm/simplicity rules, new
  C-115/C-117/C-121/C-123/C-128/C-507/C-604/C-605/C-608–C-611), `gen/GenProject1/requirements.md`
  (register resolutions), `gen/GenProject1/fix-wave-1-reviews.md` (B-docket owner verdicts),
  `docs/15-generation-pipeline.md` (skill split, build order), `.claude/skills/gen-architecture/
  SKILL.md` (reuse-first carving), `docs/16-future-ideas.md` FI-18.
- Deferred items (decided in principle, timing only): `docs/notes/deferred-items.md`.
- Discussion-pending items, briefing docs prepared for the owner to route to an agent when ready:
  `agent-tasks/discussion-a4-s7-ordering.md`, `agent-tasks/discussion-d4-stage-gates-review.md`.
  The B-docket/C-item implementation work from this batch also lives in `agent-tasks/` now, as a
  Portal-gated queue (`agent-tasks/README.md`) — see that folder for anything code-shaped.
- Still-unanswered items with no clarification blocking them: register questions Q-02/Q-06/Q-09
  stay tracked in `gen/GenProject1/requirements.md`'s own Open Questions section (their canonical
  home); the C-003 tooling-enforcement backlog item stays in `AITODO.md`'s small/tooling queue.
- Full dated history of every ruling in this batch: `docs/notes/stage-gates.md`'s "owner-questions
  batch pass" / "round 2" / "round 3" entries (2026-07-17).

---

*(No open batch right now.)*
