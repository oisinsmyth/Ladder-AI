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

#### ⚠️ The code guard catches ONE shape. The rest is convention — recorded 2026-08-24

Stated so nobody reads "there's a guard" as "the mistake is unreachable."

`ClaimStore.RejectDoubledRoot` (`src/converter/Converter/Claims/ClaimStore.cs:62-83`, called from the
**constructor** at `:31`, so every verb inherits it) throws when the root's **last segment equals this
project's slug**. That is the measured SELF-1 failure — `…\claims\test-project001` — and it is now
unreachable. ***It is the only shape refused.***

🔴 **The per-worktree fork the routing rule actually warns about is an ordinary path, and it is
accepted silently.** The suite says so in its own words:
`Converter.Tests/ClaimStoreShadowingTests.cs:151`,
`TwoDifferentRoots_StillForkTheRegistry_WhichIsWhyTheDoubledFormIsRefused` — two roots, one resource,
`Assert.True(a.Ok); Assert.True(b.Ok); // BOTH granted — no process can see the other's store`. **No
process can detect this from inside**: each store is well-formed and legitimately empty.

What stands between you and that, and none of it is a gate:

| defence | what it actually is |
|---|---|
| `--claims` is **required** (or `LADDER_CLAIMS_DIR`), no default | `Program.cs:441-444`. Stops *forgetting*, not *mis-pointing* |
| the echoed `store=` line | `ClaimsOutputFormatter.cs:18` says it plainly: *"**AN ECHOED VALUE NOBODY READS IS NOT A SAFEGUARD** — this line is not claimed as one"* |
| the hookify rule `claims-store-root` | `.claude/hookify.claims-store-root.local.md`. Its own body: *"**This rule is not load-bearing on its own. Hookify fails open**"* — a missing `python3`, an import error or a wrong cwd disables every rule with no indication |
| `tools/check-agent-evidence.py`'s claims gate | **The one that is a mechanism, from 2026-08-24.** It resolves `C:\ProgramData\Ladder-AI\claims` as a CONSTANT — `LADDER_CLAIMS_DIR` does not move it — and REFUSES any run whose environment sets that variable elsewhere, because those reservations were taken where nobody else can see them. Its only override is `--claims-root` plus a sentinel file its own test suite plants, and a run using it can never print the plain `VERIFIED` banner. Before that date it read the same env var `converter claim` reads, so one export moved the writer and the reader together and a private store produced a clean green |

**So: one narrow code guard, one gate at hand-back, plus convention.** *Not a vulnerability, and no
further fix is proposed here* — the point is that the checklist item below is still a habit for
everything the hand-back gate does not see.

> ✅ **A FOURTH DEFENCE ARRIVED WITH ADOPTION AND THIS TABLE PREDATES IT — added 2026-08-24.**
> `tools/check-agent-evidence.py`'s claims gate hard-codes the shared root
> (`C:\ProgramData\Ladder-AI\claims`, overridable only by `LADDER_CLAIMS_DIR`) rather than taking it
> from the evidence file — *"A PER-WORKTREE ROOT MAKES THIS WHOLE GATE A NO-OP … which is the reason
> the default is the real shared path rather than anything relative to this checkout"* — and it
> **derives the project from the `.ir` file paths rather than reading a declared name**, because
> *"a declared project name is one more field an agent could point at a store where its claims happen
> to live."* It also names the vacuity explicitly: **a store answering with zero claims is a failure,
> with the message
> saying to check the root is the SHARED one and not a per-worktree path.** ➜ **It is a real gate and it
> is on the DISPATCHER's side, not the agent's**, so it catches the forked store only on runs the
> dispatcher actually verifies. **It does not make the fork undetectable-from-inside any less true**;
> it means a forked run now fails the evidence check afterwards instead of passing quietly.

#### ⚠️ The store buckets by the LAST PATH SEGMENT, so a project directory named `ir` collides

`ClaimStore.SlugOf` (`ClaimStore.cs:94`) keys the bucket on the project directory's own name (`ir/test-project001` →
`test-project001`). **Pass a path that ends `/ir` and the bucket is called `ir`.** The live store has
one: as of 2026-08-24 it holds **14 claims from 5 distinct agents**, all from a single job whose IR
directory was passed as the project. ***Any two projects whose IR directory is named `ir` share that
bucket.***

**The failure direction is over-refusal, not double-grant** — which is the safe direction: the second
project's agents are refused against the first project's reservations rather than granted alongside
them. There **is** a detector — `ClaimsRunner.cs:165-170` warns when a claim's recorded `project`
differs from the one being checked — but ***it is a warning and it does not gate***:
`ClaimsModel.cs:87`, `HasFindings => Conflicts.Count > 0`, and a project mismatch lands in
`Warnings`, not `Conflicts`. **"A warning is not a gate"** — this project's own phrase, and this is
another instance of it.
➜ **There is no argument that fixes this from the caller's side, which is why it is recorded rather
than routed around.** `--project` must be the **IR directory** — `ClaimCorpus.Build` scans it
(`ClaimValidator.cs:32-35`), so passing the project root instead would key the bucket correctly and
then validate against an **empty corpus**, which is the worse failure. **Operationally: read the
bucket name off the echoed `store=` line, and where it is generic, read the `project` line inside the
claims before believing the store is about your project.** *No fix is proposed here.*

> 🔴 **THE CONSEQUENCE CHANGED ON 2026-08-24 AND NOBODY RE-READ THIS SECTION AGAINST IT. THE
> ASSESSMENT ABOVE — *"the failure direction is over-refusal, not double-grant"* — NO LONGER HOLDS ON
> ITS OWN.**
>
> Everything above was written while **claiming was optional**. `7de3ac0` made it **binding**: the three
> `gen-block-*` skills now claim before writing, and every one of them passes `--project ir/<project>/`
> — a spelling that ends in the **project's own directory name**, so `ClaimStore.SlugOf` keys the bucket
> `<project>` and never `ir`. `SlugOf` trims the trailing separator first (`ClaimStore.cs`), so the
> trailing slash changes nothing. **The verifier keys it a third way** — `check-agent-evidence.py`
> takes `os.path.dirname` of each `.ir` file it was given — so **the skill's `--project` and the
> verifier's derived project agree only when they share a last path segment.** Three producers of one
> bucket name, none of them reading the other two.
>
> **Measured at the shared root, 2026-08-24, by listing it — not quoted:** `C:\ProgramData\Ladder-AI\claims`
> holds two buckets. **`ir` holds 14 claims, 12 of them `block-number` in the 9000–9999 harness reserve
> band. `test-project001` holds ZERO.**
>
> ➜ ***So the store's only populated bucket is one the mandated spelling can never reach, and the
> bucket the mandated spelling does reach is empty.*** Every reservation ever taken under a spelling
> ending `/ir` is invisible to every run that follows the skills, and vice versa. With `--value <n>` such
> a claim is granted although the number is held; with `claim --allocate --type FB`, which the skills
> offer as *"the lowest free one"*, **the tool computes "lowest free" against an empty bucket** and can
> hand back a number already reserved. **That is a double-grant, and it is the direction this section
> previously ruled out** — the ruling was correct for two *different* projects sharing one bucket, and
> it does not cover **one project reachable by two spellings**, which is what adoption introduced. The
> detector is still only a `Warning` and still does not gate (`ClaimsModel.cs`,
> `HasFindings => Conflicts.Count > 0`).
>
> ⚠️ **Unestablished, deliberately: whether the 12 numbers in `ir` and the next mandated run are the same
> project.** Settling it means reading the `project` line inside those claim files, and they belong to a
> live job. **The mechanism above does not depend on the answer** — two spellings key two buckets either
> way — but the *severity* does, so nobody should quote this as a realised collision.
>
> ⚠️ **This blocks adoption rather than merely complicating it, and it is why no fix is proposed here
> either.** Any repair is a choice between changing the key, changing the spelling every skill passes,
> or migrating the existing bucket — each of which invalidates live reservations held by agents that are
> not asking, and the third is a write to a store this document exists to say is shared. **What is
> recorded is that the consequence is now different from the paragraph above it, so that nobody reads
> "the safe direction" and stops.**

🔴 ***AND A BOARD IS ONLY A TOKEN IF EVERY LANE WRITES TO IT.*** Twice in one day (2026-08-18) a lane
did the work on a single-writer resource **without claiming it**. Both times it came out clean — and
**the lane that reported it was right to call that luck rather than a licence.**

Nothing refuses an unclaimed write. The board cannot detect a lane that skipped it, so the only
symptom of the habit slipping is a collision that may simply not happen that day. **A near miss is
therefore the cheapest evidence you will ever get that the protocol has stopped being followed —
spend it, rather than filing it as a success.** *A coordination protocol that one party may opt out
of whenever it looks unnecessary is not a protocol; it is an intention.*

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

1. `--claims C:\ProgramData\Ladder-AI\claims` — **read the echoed `store=` line**, and check the
   bucket name on the end of it. A generic one (`…\claims\ir`) is shared by every project whose IR
   directory has that name.
2. One lane holds Portal. Say who.
3. Wave store: shared path, outside every worktree. `status` it first — **zero slots must mean
   nobody has submitted yet**, and you should know which it is.
4. Claim **before** writing, not after. A log is forensics; only a reservation is prevention.
5. Read `ACHIEVED CONCURRENCY`, not the slot count.
6. No `openness-cli` rebuild while Portal work is in flight.
