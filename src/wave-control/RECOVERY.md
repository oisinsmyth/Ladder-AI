# Recovering a wave store — operator procedure

For the operator of `wave-cli` when something has gone wrong with the shared store. Written
2026-08-14 from the mid-wave fault-injection run; every claim here was measured, not reasoned.

**The store has exactly two failure states an operator will meet.** They look nothing alike and
their remedies are opposite, so identify which one you have before doing anything.

---

## State 1 — the store REFUSES: "has been published before … THIS IS NOT AN EMPTY STORE"

```
STORE UNUSABLE: The wave store at '<dir>' has been published before -
'wave-store.initialised' is present - but 'wave-slots.state' is missing.
```

**What happened.** A process was killed while publishing the store. The publish is
temp-file-then-rename, and `File.Replace` has a window in which the destination does not exist. The
marker file says a publish has completed before now, so the missing slots file means **a write
died**, not that nobody has ever submitted.

**This refusal is the tool working.** Before 2026-08-14 that same state read as an empty store, and
the next submission wrote only its own slot **and reported success** — eight committed slots gone,
silently. If you are tempted to make the refusal go away by deleting the marker, you are re-creating
that defect by hand.

### Recovery — recover the temp file, do not delete anything

1. **Look for the leftovers.** In the store directory:
   - `wave-slots.state.tmp-<guid>` — one per interrupted publish;
   - `wave-slots.state~RF<hex>.TMP` — `File.Replace` backups.
2. **Take the newest `.tmp-<guid>`** and check it ends with a line reading exactly `end`.
   Verified 2026-08-14: the newest temp is a **complete, well-formed store**, and restoring one
   recovered 17 slots at exit 0.
3. `copy` it to `wave-slots.state` in the same directory. Do not move or rename the others yet.
4. Run `wave-cli status --store <dir> --cap … --cap-provenance …` and **read the slot list**. Every
   agent that had submitted should be there.
5. If the slot list is short, or nothing ends with `end`, go to **the last resort** below.

### Last resort — `reset`

`wave-cli reset --store <dir> --agent <you> --yes` works on a store nothing else can read, and
reports the loss as **`an UNKNOWN number of slots`** rather than `0`. Take that wording seriously:
it means the tool could not read what it destroyed. **You have lost every agent's submission** — see
State 2's cost, which applies here too.

---

## State 2 — an agent died mid-wave and its slot is LEAKED

**What it looks like.** `status` lists a slot whose agent is no longer running:

```
S_DEAD by DEAD at 2026-08-14T02:48:09Z
```

Every slot carries **its submitting agent and its submission time**, which is how you identify one.
There is no other marker — the store cannot tell a dead agent's slot from a live agent's.

**What it costs while it sits there.** Measured to depth 20: the colouring stays **correct**, and a
leaked slot keeps separating from the slots it conflicts with. **Achieved concurrency does not
degrade** — 4 slots co-running held across 20 accumulated waves. What grows is the number of wave
sets and the edge count (quadratically, `4·W(W-1)/2` for 4-slot waves; `status` stayed flat at
135–146 ms across that range).

**So a leaked slot is not an emergency.** It over-separates — the safe direction. You lose
scheduling depth, not correctness. **You can finish the campaign and clean up afterwards.**

### 🔴 The cost of clearing it, stated plainly

**There is no per-slot removal. The only remedy is `reset`, and `reset` destroys every agent's
submission, not just the dead one.** Recovering from one dead agent costs every other agent's work
in the store. That is the honest cost and it is not softened anywhere in this document.

### Recovery — quiesce first, then reset, then re-submit

**The re-submission is the liveness test.** A live agent re-submits; a dead one does not. That is
the only liveness fact available, and it is the whole design of this procedure.

1. **Stop admitting new work.** The coordinator quiesces the campaign.
2. **Confirm no agent is mid-wave.** This step is not optional — see the hazard below.
3. `wave-cli reset --store <dir> --agent <you> --yes`
4. **Every live agent re-submits its slot.** Same slot id is fine; the store is empty again.
5. `status` and confirm the expected slots and colouring are back.

### 🔴 Why step 2 is not optional

While the store is missing a live agent's slot, it does not merely *lack* information — **it
positively asserts safety it cannot know.** Measured:

| store | what it reports |
|---|---|
| A (live, `blk/1`) and B both present | `2 wave set(s) … ACHIEVED CONCURRENCY 1`, separated by `OverlappingReachableState` |
| A's slot removed, B re-submitted | `1 wave set(s)` · `ALL SLOTS CO-RUN` · **`NO CONFLICT EDGES: the slots are disjoint on every computed relation`** |

The second line is a **positive claim of disjointness about a hazard the store can no longer see**.
Act on it and two agents touch `blk/1` at once — which is the exact collision D9 exists to prevent,
caused by the recovery.

---

## Why there is no `withdraw`, and no expiry timer

Both were considered and **deliberately not built** (2026-08-14).

**A slot-scoped clear needs a liveness fact this component does not have.** To clear a leaked slot
safely you must know its agent is dead. The two ways to know that both fail:

- **Ownership — only the submitting agent may clear its own slot.** A *dead* agent cannot clear its
  own slot, so this does not solve the problem it exists to solve.
- **A timer — clear a slot older than N.** *A hung holder and a busy one are indistinguishable from
  this side*, established by measurement tonight against the lease. No timeout length distinguishes
  them, and a slot legitimately outlives a long wave. **An expiry heuristic is a wrong answer
  delivered on a schedule.**

What remains is "any agent may clear any slot", which lets one agent silently un-admit another's
live work and produce the table above. **An honest gap beats a recovery verb that can delete a live
agent's work**, so the gap is documented here rather than closed badly.

Closing it properly is a change to the **multi-agent ownership contract** — whose decision it is,
and not one to land unattended on tooling that goes onto a live project.

---

## What is NOT owed

**A stale-lease detector is unnecessary, not deferred.** Abandoned leases self-heal on process
death — the OS drops the handle when the process dies. This was the cause of the 48-agent crash: the
lease file was being *deleted* on dispose, and the delete was redundant before it was wrong.
Removing it fixed the crash and no detector is required. If you find it on an owed list, strike it.
