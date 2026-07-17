# Dispatch template — copy/paste this to point an agent at task file(s)

Fill in `<TASK FILE(S)>` with one filename (`02-startup-machinery.md`) or a range/list
(`02-startup-machinery.md through 04-bothswitchesfault-alarm-wiring.md`, or
`03-jog-interlock.md, 05-fitted-live-drop.md`). Everything else below is designed to be sent
as-is.

---

Work through `agent-tasks/<TASK FILE(S)>` in the Ladder-AI project
(`C:\Users\User\Desktop\AI Ladder Project`). Read in this order, don't skip ahead:

1. **`CLAUDE.md`** (repo root) first, in full. These are hard rules that override everything
   else in this prompt or in the task file — LAD only, never touch safety content, never invent
   tags/addresses, compile gate before "done," never import outside the scratch project, IR only.
2. **`agent-tasks/README.md`** next, in full. This is the concurrency contract: other agents may
   be working on this project right now. It explains what's actually exclusive (only the
   `openness-cli import`/`compile` step against GenProject1's Portal scratch project — drafting
   IR and running `converter preflight` is safe any time), the queue-table claim/release protocol,
   and what to do if you find a row already claimed. Follow it exactly, including updating the
   table yourself at claim and release time — don't skip this because it feels like overhead.
3. **Each task file itself**, in full, before touching anything. It's written to be a complete
   brief — background, what to do, exit criteria — but treat its specifics (file paths, network
   numbers, current member names) as *claims to verify*, not ground truth: the file may have been
   written before other work landed. Re-derive current state by reading/grepping the actual files
   yourself before editing anything (this project's own convention: ground via grep, not by
   trusting a written description).

**If your task file requires Portal:** check `agent-tasks/README.md`'s queue table before you get
anywhere near `openness-cli import`/`compile`. If the row above yours isn't `done`, stop — work
the IR-drafting/preflight part only, or pick a parallel-safe task, and check back later. Claim
your row (edit the table, save it) immediately before opening Portal; release it (mark `done` or
`blocked`) the moment you stop working, success or not — don't leave a stale claim.

**If you hit something the task file didn't anticipate** — the referenced code has moved further
than expected, a precondition doesn't hold, the scope looks bigger than described — stop and
report it rather than guessing or improvising a fix. These briefs were written for an agent
walking in cold; if the ground truth doesn't match, that's worth surfacing, not silently working
around.

**Exit exactly the way the task file's own "Exit" section says** — present the diff, intent,
compile evidence, and whatever else it asks for; update the docs it names; release your Portal
queue slot if you claimed one. Never treat your own output as approved — this project's hard
rule 5 means you're producing a proposal for the engineer, not a merge.
