# Multi-agent operating guide

**Read this before starting a multi-agent session, not after something goes wrong.** One page on
purpose. Every number here was measured on this machine (2026-08-14), not estimated.

---

## 1. The two shared resources, and the one trap in each

### The claims store — `C:\ProgramData\Ladder-AI\claims`

Pass **exactly that root**. It lives outside every worktree deliberately, so it cannot be shadowed.

🔴 ***THE TOOL APPENDS THE PROJECT NAME ITSELF.*** Passing `…\claims\test-project001` yields a store
at `…\claims\test-project001\test-project001` — **a second, empty store that grants every claim.**

**This is the worst failure mode in the system, because it is invisible: two agents making the same
mistake agree with each other perfectly.** Every claim succeeds, nothing is ever refused, and the
registry looks exactly like it is working.

> **Read the `store=` line the tool echoes. Do not trust the argument you passed.**

The same trap catches a per-worktree store: agents work in separate worktrees, so a worktree-local
claims dir is *always empty, grants everything, and looks like success*. There is no default for
`--claims` on purpose.

### Portal — a token, not a component

**One lane holds it at a time.** Two Openness sessions on one project is unsupported. A collision
looks like `Collection was modified` with **every block reporting inconsistent** — alarming, and not
what it appears to be. Concurrent sessions on *different* projects are fine.

Also: **`dotnet test` on `openness-cli.sln` is a rebuild**, and a rebuild needs re-approval. Never
rebuild the binary that in-flight Portal work is running. `converter.sln` is safe at any time.

---

## 2. What concurrency actually buys

Measured on the wave store, agents contending for real:

| agents | behaviour |
|---|---|
| 8 · 16 · 32 · 48 · 128 | **linear, no knee.** 69–122 ms per agent |
| store size to 256 slots | free — 87 / 104 / 101 ms |
| wave depth 60 (240 slots, 7,081 edges) | `status` 194 ms, `submit` 241 ms |

**The ceiling is the caller's own timeout, not a breakdown.** At ~91 ms/agent a 30 s lease timeout
serialises roughly **330 agents**; past that the edge agent gets a **named, retryable refusal**
(exit 1, "BACK OFF AND RETRY") — *the system degrades by refusing clearly, not by falling over.*

**Exit codes:** `0` admitted · `1` refused (retryable — back off) · `2` unusable (nothing was
decided; retrying will not help).

---

## 3. 🔴 `SERIALISED` is a correct result, not a failure

***N SLOTS AGAINST ONE FB INSTANCE IS ONE SLOT.*** Independence means **non-overlapping reachable
state** (D9), computed through each block's call tree — not "different block names".

So four agents all touching `FB_Shared` colour into **four wave sets of one**, achieved concurrency
1, and the report says `*** SERIALISED ***`. **That is the tool working.** It is the single most
likely thing to be misread as a bug.

The inverse is the real danger: a campaign that generates N slots against one block and reports "N
concurrent" is **measuring its own generator**. Always read `ACHIEVED CONCURRENCY` — the largest
wave set — never the slot count.

---

## 4. If the wave store misbehaves

Two failure states. They look nothing alike and **their remedies are opposite**, so identify which
one you have first. Full procedure — including the temp-file recovery that restores a lost store —
is **`src/wave-control/RECOVERY.md`**. Do not improvise from memory; it is one page too.

- **"has been published before … THIS IS NOT AN EMPTY STORE"** — a write died mid-publish. The
  refusal is the tool working. **Recover the newest `wave-slots.state.tmp-*`; delete nothing.**
- **A slot whose agent is gone** — leaked, and it **over-separates**, which is the *safe* direction.
  Concurrency does not degrade. **This is not an emergency — finish the campaign, clean up after.**

🔴 ***IF THE STORE READS ZERO SLOTS AFTER A SUBMISSION HAS BEEN MADE, STOP. DO NOT RE-SUBMIT.***
An absence that is correct exactly once — before anyone has ever submitted — is a bug for the rest
of time. Re-submitting into it is what silently discards everyone else's committed work.

**There is no per-slot removal.** The only remedy is `reset`, and it destroys **every** agent's
submission, not just the dead one. That cost is real; `RECOVERY.md` explains why a `withdraw` verb
would be more dangerous than the gap.

---

## 5. What this system cannot tell you

Do not build a check, a timer or a habit on any of these. They are not gaps to be closed; they are
**facts about what is observable from here**.

- **A hung holder from a busy one.** An exclusive open failing repeatedly says nothing about whether
  the holder is progressing. *No timeout length distinguishes them* — which is why the lease-timeout
  message states what it observed and hands you the judgement.
- **A dead agent from a slow one — except by re-submission.** ***The re-submission IS the liveness
  test:*** a live agent re-submits, a dead one does not. It is obtained by doing the work, not by
  asking a question the system cannot answer.
- **Whether a wave has finished.** There is no completion signal, so nothing ever leaves the store.
  Reset between campaigns.

**And the general rule behind all three:** *deleting an input from a system that reports derived
conclusions does not produce a smaller conclusion — it produces a wrong one.* Removing a live
agent's slot makes the store report `NO CONFLICT EDGES: the slots are disjoint on every computed
relation` — **a positive claim of safety about a hazard it can no longer see.** A silent gap is bad;
a confident false assurance is worse than either.

---

## 6. Before you start — 60-second checklist

1. `--claims C:\ProgramData\Ladder-AI\claims` — **read the echoed `store=` line.**
2. One lane holds Portal. Say who.
3. Wave store: shared path, outside every worktree. `status` it first — **zero slots must mean
   nobody has submitted yet**, and you should know which it is.
4. Claim **before** writing, not after. A log is forensics; only a reservation is prevention.
5. Read `ACHIEVED CONCURRENCY`, not the slot count.
6. No `openness-cli` rebuild while Portal work is in flight.
