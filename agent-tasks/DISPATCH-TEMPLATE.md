# Dispatch template — copy/paste this to point an agent at task file(s)

Fill in `<TASK FILE(S)>` with one filename (`02-startup-machinery.md`) or a range/list
(`02-startup-machinery.md through 04-bothswitchesfault-alarm-wiring.md`, or
`03-jog-interlock.md, 05-fitted-live-drop.md`). Everything else below is designed to be sent
as-is.

**Pick the model before you dispatch — this is the dispatcher's call, not the agent's.** Judgement
work keeps the inherited model: `lad-coder`, `hmi-designer`, `assertion-enumerator`, and anything
reviewing. Mechanical work — grep sweeps, file discovery, doc inventories, "which files mention X" —
runs on a cheaper, faster model, passed per dispatch. It returns sooner as well as cheaper, and the
telemetry says reasoning time is the bottleneck (`docs/notes/orientation-cost-roadmap.md`), so this
is one of the few levers that moves the wall clock. **No agent file pins a model**, deliberately:
the three above are all judgement work, and a pinned model there is only a way to get it wrong.
Caveat: if a requested model is unavailable to the account, the **parent** model runs instead — the
failure direction is expensive, not cheap, so check what came back rather than assuming.

---

Work through `agent-tasks/<TASK FILE(S)>` in the Ladder-AI project
(`C:\Users\User\Desktop\AI Ladder Project`). Read in this order, don't skip ahead:

1. **`CLAUDE.md` is already in your context — do not read it again.** It was injected with this
   prompt. Its hard rules override everything else here and in the task file — LAD only, never
   touch safety content, never invent tags/addresses, compile gate before "done," never import
   outside the scratch project, IR only — and they bind you whether or not you re-read them.
   Opening it costs ~19.6 KB of context for a file you were handed free. *(This instruction used
   to say "read it first, in full". That was the double-load the 2026-08-21 context cut removed
   from `lad-coder.md` in `c078fab`, and it survived here for a while afterwards, contradicting
   the agent file.)*
2. **`agent-tasks/README.md`** first, in full. This is the concurrency contract: other agents may
   be working on this project right now. It explains what's actually exclusive (only the
   `openness-cli import`/`compile` step against the shared Portal scratch project — drafting
   IR and running `converter preflight` is safe any time), the queue-table claim/release protocol,
   and what to do if you find a row already claimed. Follow it exactly, including updating the
   table yourself at claim and release time — don't skip this because it feels like overhead.
3. **Each task file itself**, in full, before touching anything. It's written to be a complete
   brief — background, what to do, exit criteria — but treat its specifics (file paths, network
   numbers, current member names) as *claims to verify*, not ground truth: the file may have been
   written before other work landed. Re-derive current state by reading/grepping the actual files
   yourself before editing anything (this project's own convention: ground via grep, not by
   trusting a written description).

**Start here (optional — fill in or delete):** `<FILES THE DISPATCHER ALREADY KNOWS ARE RELEVANT>`.
These are a **starting point, not a substitute** for item 3 above: they save you the discovery pass,
they do not license you to skip verifying anything. If the list turns out to be wrong or
incomplete, that is expected — grep is still the ground truth, and say so in your report.

**Work in as few round-trips as you can.** Every tool call is a round-trip, and on a task with
dozens of them that dominates the wall clock. Independent commands go in **one** call rather than a
sequence; multi-step logic goes in a **script file** you run once rather than a chain of calls that
each wait on the last. This is about latency, not tokens — prefer one longer call over three short
ones even when the total output is larger. (Two exceptions: keep a command that needs its
predecessor's output separate, and don't chain past a step whose failure should stop the rest —
note that `grep` exits 1 on zero matches, so `grep … && next` silently skips `next` on a legitimate
empty result.)

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
queue slot if you claimed one. **If the task touched IR, that includes writing
`agent-tasks/<id>/evidence.json`** per the hand-back contract in `agent-tasks/README.md` — the
dispatcher verifies it with `python tools/check-agent-evidence.py`, which recomputes every
`ir-hash` itself, so hand-copied hashes fail. Never treat your own output as approved — this
project's hard rule 5 means you're producing a proposal for the engineer, not a merge.
