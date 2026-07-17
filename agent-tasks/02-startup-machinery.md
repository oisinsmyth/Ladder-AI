---
task: 02-startup-machinery
source: owner-questions B-2 / D-1 / Q-01 (doc 06 C-128, C-124, C-403, C-305)
portal: yes — GenProject1 scratch project (see agent-tasks/README.md queue before importing)
status: done (2026-07-17) — see gen/GenProject1/fix-wave-1-reviews.md B-2 and requirements.md Q-01 for the outcome
queue-order: 1
---

# Task 02 — Build GenProject1's startup machinery (OB100 + `DB_PLC` + C-111 simulation gating)

**Multi-agent note:** this is **queue position 1** of the Portal-serialized tasks in
`agent-tasks/README.md` — the first to claim the Portal slot, since it's the largest, most
foundational fix and the others don't strictly depend on it but this one closes out the
withdrawn waiver first. Read that README's "Portal queue protocol" before doing anything with
`openness-cli`. You can draft IR and run `converter preflight` at any time without claiming the
queue slot; only claim it right before `import`/`compile`.

## Background — why this exists

GenProject1's demo panel shipped with **no OB100 startup block and no `DB_PLC`**, and `Step`/
`RunFwd`/`RecentStart` are RETAIN. Fix wave 1's check stage traced the consequence: a PLC power
cycle mid-run silently re-commands the shredder motor on the first scans after restart, unwarned
(`gen/GenProject1/fix-wave-1-reviews.md`, B-2). The owner ruled this a confirmed fault (2026-07-17)
and **withdrew the prior demo-panel waiver** for omitting this machinery — it's no longer optional.
This also closed `gen/GenProject1/requirements.md` Q-01 and produced a new site-wide rule,
**doc 06 C-128**: "no automatic restart after a stop or power event... a fresh, explicit start
command from the operator is always required, and it re-runs the full start-up sequence... never
a mid-sequence resume."

Read before starting: `docs/06-lad-conventions.md` C-124, C-128, C-305, C-403, C-111 (the rules
this build must satisfy); `gen/GenProject1/fix-wave-1-reviews.md` B-2 and the "Startup-machinery
waiver withdrawn" note at the end of its owner-verdicts section; `gen/GenProject1/requirements.md`
Q-01.

## What to build

1. **`DB_PLC`** (C-305): holds `Simulation : Bool`, start value `FALSE`. Add whatever other
   PLC-specific system data this project genuinely needs — don't invent scope beyond what C-305
   and C-111 require.
2. **OB100** (startup organization block, TIA calls it automatically on warm restart — no OB1 call
   needed): force-writes `Step` (and any other transient run-state: `Hold`, edge-memory, in-progress
   event counters — C-124) back to idle on every block that has one, **regardless of retentivity**.
   Also force-resets `DB_PLC.Simulation` to `FALSE` (C-305) and clears any S/R-driven bits per
   C-403's dedicated-startup-reset-block requirement. **Scope check before writing this:** grep
   `ir/GenProject1/` for every `Step : Int` (currently `FB_ShredderSequencer`, `FB_PusherControl` —
   confirm this is still the full list, don't assume) and for `RETAIN` members generally; C-124
   explicitly excludes genuine fault latches already `RETAIN` (a fault needing human ack still
   needs it after a power cycle) — don't force-clear those.
3. **C-111 simulation gating** at the mapping layer (`FC_Inputs`/`FC_Outputs` or whatever they're
   currently named — verify): gate input-map calls off via `DB_PLC.Simulation`, add
   `FC_Simulation`, and put a `-|/|-` on `DB_PLC.Simulation` between buffer and raw output on every
   physical-output write in the output map (outputs de-energize while simulating, never freeze at
   last state — C-111's own stated reasoning). **Scope call for the agent:** if full C-111
   simulation support looks like it's expanding this task well beyond "add OB100 + DB_PLC," it's
   fine to build the minimal C-305/C-403/C-124 restart-safety machinery first and flag C-111's
   simulation-mode gating as a follow-up — say so explicitly in the presentation rather than
   silently scoping it down.
4. **`DB_PLC.Simulation`** visible on the HMI per C-112 — note this as a tag the design provides;
   actual HMI work is out of scope for this project (CLAUDE.md non-goals), just don't block the
   tag from existing.

## Workflow (CLAUDE.md hard rules apply throughout)

This is new-block creation (OB100, `DB_PLC`) plus a modification to existing Map FCs (if you do
the C-111 gating) — follow CLAUDE.md's "Workflow for logic generation" 5-step loop for the new
blocks, and "Workflow for modifying existing logic" (named networks only, untouched-network
invariance) for anything you touch in `FC_Inputs`/`FC_Outputs`. Never invent tags (hard rule 3).
Compile gate before done (hard rule 4) — this is where you claim the Portal queue slot. Never
import to the real project — scratch only, produce a diff for the engineer (hard rule 5).

## Exit

Present: IR diff, one-paragraph intent statement, compile evidence, and explicitly confirm the
C-128/Q-01 auto-resume scenario is now closed (state how — e.g. "Step force-writes to 0 in OB100,
verified by re-reading the exported IR post-import"). Update `gen/GenProject1/fix-wave-1-reviews.md`
(mark B-2's startup-machinery item done) and `gen/GenProject1/requirements.md` Q-01 (already marked
resolved — add a note that the machinery is now built, not just ruled). Release the Portal queue
slot in `agent-tasks/README.md` (set this row `done`) whether you succeeded or stopped early.
