# Stage S2 — Read and explain — full evidence record

> Narrative and acceptance-test evidence for stage S2, split out of `docs/notes/stage-gates.md` on 2026-07-18 per `docs/16-future-ideas.md` FI-20.
> `stage-gates.md` remains the live status index; this file is the append-only
> detail record. Content below is verbatim as it stood in `stage-gates.md` at
> commit d8572c2. Append new S2 entries here, not to the index.

---

## S2: explanation-quality checklist built from direct comparison, not invented solo

Per the roadmap, S2's second deliverable (the explanation-quality checklist) doesn't exist until
built — the project owner's own instruction was to ground it in real trial explanations first,
compared against each other, rather than draft it from first principles. Three full explanations
of real JOB9002 blocks (`PerimeterSafetyAlarms`, `MotorDOL`, `PlantAutoControl`) were produced and reviewed in
conversation and confirmed accurate by the project owner — content not committed anywhere, per
`13-data-boundary.md`'s 2026-07-14 S2-kickoff entry.

`PlantAutoControl` was then re-explained by five independent subagents, each given a different amount
of project/process context — from a bare task description up to full staging context plus explicit
methodology guidance — specifically to isolate what actually drives explanation quality versus
what just costs more tokens. Findings that shaped the checklist: cost didn't track context amount
monotonically (the bare-minimum condition wasn't the cheapest — it paid its own "discovery tax"
finding basic tooling info that direct orientation skips). The clearest, most reproducible quality
lever was an explicit instruction to check every instance of a repeated pattern exhaustively rather
than describe it from samples — this reliably surfaced real cross-instance bugs that
sampling-based descriptions missed, replicated across multiple independent runs, and is now
checklist item E-04. Data-boundary awareness (whether the agent checked if it was even allowed to
use this data this way) tracked whether it was told to read `CLAUDE.md` at all, not any deeper
context — a governance-awareness gap, not a technical-quality one, and is now E-06. A "uniform
across every instance" claim turned out wrong on direct recount in two independent runs (including
one produced with a purpose-built briefing document) — the single most common failure observed,
now E-01.

A one-off attempt to package the resulting methodology as an installable Claude Code skill
(`.claude/skills/explain-plc-block/`) confirmed the file alone isn't sufficient for a subagent to
invoke it as a real skill in this harness — a newly-added project-local `SKILL.md` didn't appear in
a fresh subagent's own available-skills list. It works today only as a plain reference doc a task
explicitly points at; genuine skill-invocation needs further investigation before relying on it,
not assumed working.

Checklist committed as `docs/14-s2-explanation-checklist.md` (indexed in `00-README.md`).
`AITODO.md` updated to reflect the three original S2-kickoff questions as resolved, with a running
count toward the 10-sample exit criterion.

## S2 gate review: signed off, S3 opened (2026-07-14)

Four real JOB9002 blocks explained in conversation this session — `PerimeterSafetyAlarms`, `MotorDOL`,
`PlantAutoControl`, `MotorFwdRevSystem` — covering 51+ networks combined, all explicitly confirmed
accurate by the project owner. Combined with the checklist itself (methodology above), that clears
the roadmap's exit bar (10 sampled networks judged accurate, no hallucinated tags/behavior) several
times over.

The project owner separately confirmed `WAIT` and `Jump` — carried forward from S1's own sign-off
as open questions needing input — are not needed. Closed rather than deferred; see `AITODO.md`'s
"Deliberately deferred" section for the technical detail preserved in case either becomes relevant
again. Modbus's live-compile gap, the third item from that same S1 carry-forward, is unaffected by
this and stays open.

S3's own entry criterion needed one thing beyond S2 itself: `docs/11-review-workflow.md` explicitly
*agreed*, not just drafted or existing. Summarized for the project owner in conversation and agreed
as-is — no changes requested, the doc stands exactly as originally drafted.

S3 (comment generation) is now open. No work started yet.

All three smaller flagged gaps from the earlier "what's left before S1" review are now closed.

