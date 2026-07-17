# Discussion task — stage-gates.md bloat + gate structure (originally owner-questions D-4)

**Purpose of this doc:** the owner asked (2026-07-17) for a separate agent/conversation to go over
this rather than a written ruling. This is the prep. **Whoever picks this up: don't restructure
`docs/notes/stage-gates.md` or the roadmap gates unilaterally — walk the owner through the points
below and record whatever they decide.** (Note: `docs/notes/owner-questions.md`, the doc this was
originally filed against, has since been cleared and reset — it's a reusable batch-questions doc,
not a permanent log; this file is now the durable record of the open item.)

**Multi-agent note:** this is a conversation task, not code — no Portal contact, not part of
`agent-tasks/README.md`'s Portal queue, safe to run at any time alongside anything else in that
folder. Check `agent-tasks/README.md` for whether this is still listed as open before starting —
someone may have already run this conversation.

## The original question (D-4)

Roadmap exit criterion for S6 (`docs/02-roadmap.md` line 51): *"Ten plain-language requests
produce compiling, human-approved LAD with zero invented tags."* Do the fix waves (bug-fix
iteration work, e.g. `gen/GenProject1/fix-wave-1.md`) count toward that ten, or only fresh
feature/generation requests? Does the owner want a running tally kept somewhere (stage-gates?
AITODO? a dedicated counter)?

Owner's answer so far: "No but we will have to discuss the Stage-Gates Doc its so bloated and may
need to adjust gates. Arrange for a separate Agent to talk to me about this." Read as: the
tally-counting question itself is secondary to a bigger concern about the gate/doc structure —
worth having the gate-structure conversation first, then coming back to the literal counting rule.

## Why `stage-gates.md` is worth discussing

As of 2026-07-17 the file is **~4,300 lines / ~330KB** — a single chronological narrative log
covering every stage-gate proof and session summary from S1 through the current S6 work. It's the
authoritative record other docs point to (AITODO's recovery procedure reads its tail first;
`owner-questions.md`'s own "never silently resolved" rule requires a dated entry here for every
ruling). Both of those consumer patterns only ever need the *tail* or a *specific dated entry* —
nobody reads the file front-to-back in normal use, which is a sign the current single-growing-file
shape may not match how it's actually consulted.

## Points to raise

1. **Format: one growing narrative file, or split/indexed?** Options worth putting in front of the
   owner: (a) keep as-is — single file, accept the size, rely on grep/offset reads; (b) split
   per-stage (`stage-gates/s1.md` … `stage-gates/s6.md`) with a slim top-level index of dated
   entries and pointers; (c) split per-project once multiple real projects exist, keeping S1–S5
   (tooling-stage, project-agnostic) separate from per-project gate history.
2. **Granularity of what earns an entry.** Some entries are major gate sign-offs (a stage's exit
   criterion met); others are session-close housekeeping notes or small dated rulings (like the
   owner-questions batch-pass entries added 2026-07-17). Is every dated ruling from
   `owner-questions.md` actually pulling its weight as a *stage-gates* entry, or would a lighter
   "ruling log" separate from "gate proof" reduce the growth rate without losing the audit trail?
   (Note: `owner-questions.md` itself, once an item is marked resolved with a pointer, already
   carries a compact record — the stage-gates entry may be partially redundant with it.)
3. **Does the S6/S7 gate as currently worded match the owner's actual intended sequencing?**
   Concrete tension worth surfacing directly: `docs/02-roadmap.md` line 56 states S7's *entry*
   criterion as **"S6 done"** — i.e. formally, S7 cannot open until S6's ten-request exit criterion
   is met. But the owner's S7-rush framing (`agent-tasks/discussion-a4-s7-ordering.md`, persistent
   project memory) wants modify-skill work moving *now*, ahead of S6 closing. The standing resolution so far
   (`docs/notes/stage-gates.md`, 2026-07-17 entry) has been "sandbox-scoped until S7's gate
   formally opens" — building/exercising the modify skills now, without calling it a formally-open
   S7. Ask directly: does the roadmap's S6-done gate for S7 need rewording to match how work is
   actually proceeding, or does the sandbox-scoped compromise stay the permanent shape?
4. **The literal D-4 question, once the above is settled:** do fix waves count toward the ten?
   A reasonable middle position to float: fix waves demonstrate value but aren't "plain-language
   generation requests" in the roadmap's sense — worth a separate, parallel tally rather than
   merging the two counts, so S6's exit criterion keeps meaning what it was written to mean.

## Pointers

- `docs/02-roadmap.md` — S6/S7 entry/exit wording (lines ~47–58).
- `docs/notes/stage-gates.md` — the file itself; read its tail for current size/shape, don't read
  it front-to-back (offset/limit or grep).
- `AITODO.md` — "Project stage" section, currently the closest thing to a live S6 status summary.
- `agent-tasks/discussion-a4-s7-ordering.md` (related — the sequencing question this touches on).

## Where to record the outcome

- A dated entry in `docs/notes/stage-gates.md`.
- Delete this file (or mark it done at the top) and remove its row from
  `agent-tasks/README.md`'s task index once the conversation has happened and the outcome is
  recorded — don't let a completed discussion task linger here.
- If the doc gets restructured, a top-of-file note in whatever replaces/supplements
  `stage-gates.md` explaining the new shape, plus updating every doc that currently points at it
  (`AITODO.md`'s recovery procedure, this project's CLAUDE.md if it references the file directly).
- If `docs/02-roadmap.md`'s S6/S7 gate wording changes, that's roadmap-level and may want its own
  dated note or ADR depending on how substantive the change is.
