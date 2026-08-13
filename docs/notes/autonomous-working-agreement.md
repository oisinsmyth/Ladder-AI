# AUTONOMOUS WORKING AGREEMENT

Written 2026-08-12, after a day where the work stopped repeatedly on **procedure rather than
design**. The spec was finished; the design questions were answered; almost every interruption
was about merges, permissions, worktrees and git semantics.

**This file is the contract for working unattended.** Read it at the start of any autonomous run.

---

## THE RULE THAT MATTERS MOST

> **Being blocked on one lane is not being blocked. Work another lane and batch the question.**

Stopping is a *last* resort, not a first one. On the day this was written there were three items
recorded in the build plan as *"blocked on nothing"* — and they sat untouched while a merge was
awaited. That is the failure this document exists to prevent.

**When something blocks:**
1. Note it.
2. **Move to a different lane immediately** — lanes are per component: converter, `openness-cli`,
   harness, Portal, IR authoring.
3. Accumulate blockers and raise them **together**, once, with what each costs.
4. Only stop entirely when *every* lane is blocked.

---

## PRE-AUTHORISED — do these, do not ask

- **Commit to master**, single-purpose, reasoning in the message. `git log` is the review surface.
- **Rebuild the converter** any time. No TIA whitelist, safe mid-Portal-work.
- **Build/test `openness-cli` in Debug.** Release only when no Portal work is in flight.
- **Import, compile, `sanity-check`, export** against the **scratch** project.
- **Download to the bench rig.** Outputs cannot actuate (ADR-0009).
- **Create tags, blocks and DBs in the scratch project.** Record each as proposed-then-created;
  never silently invent. *(Hard rule 3's mechanism is propose → engineer approves; standing
  approval given 2026-08-12 for scratch.)*
- **Converter capability changes**, gated on the **confirm loop**, not on asking.
- **Fix a defect found in passing** if it is in a lane already open and has a regression test.
- **Spawn agents**, one per component, never two in the same component.

## STOP FOR THESE — and only these

- **Anything touching safety** (hard rule 2). Stop and report, always.
- **The real project**, as opposed to scratch. The promotion gate is the engineer's.
- **A design decision that changes the spec** — not an implementation choice inside it.
- **A measurement that contradicts something already built on** — e.g. A1 coming back "it tears".
- **Irreversible or unmeasured-destructive acts.** 1.7's delegate throw is the live example: G4
  is unmeasured and the failure mode is a half-loaded CPU.
- **Granting my own permissions, or disabling a guard on my own writes.** Supply the mechanism,
  let the owner run it.

---

## PRECONDITION CHECK — run this ONCE at session start, not on discovery

Every stop on 2026-08-12 that was not a design question was a precondition discovered by tripping
over it. **Check the whole toolchain before starting**, in one pass:

```
1. git:      branch, ahead/behind, working tree clean
2. binaries: converter Release CURRENT (behaviourally, not by timestamp);
             openness-cli Release approved and NOT about to be rebuilt
3. perms:    the commands this run will need are actually allowed - try one cheap
             read-only invocation of each family rather than assuming
4. Portal:   portal-status; is a human session holding the project
5. rig:      run state, if the run will touch it
```

*** REPORT ALL FAILURES TOGETHER, ONCE. *** One message listing five things costs the owner two
minutes; five messages over an hour costs them the afternoon.

---

## THE FAILURE MODES THAT ACTUALLY HAPPENED, AND THEIR FIXES

| what happened | why | fix |
|---|---|---|
| Four merge requests in one session | Converter changes could not reach the permitted binary from a worktree | One tree, one branch. `bgIsolation: none` + permissions merged |
| Exited the worktree, lost 16 permission rules and all edit ability | Acted before checking preconditions | The precondition check above |
| Waited on a merge while three unblocked items sat idle | Treated one blocked lane as everything blocked | The rule at the top |
| A commit swept another agent's staged files, twice | `git add` + bare `git commit` commits the **whole index**, which is shared | `git commit -- <paths>`. New files need `git add` first |
| Asked for rulings on `hmi-compile`, `Normalizer`, legacy migration | Reversible, low-stakes, inside the spec | Decide, record the reasoning, report after |
| **Two lanes ran Openness against the SAME project at once** — one saw `Collection was modified` then `EngineeringObjectDisposedException` inside a *read-only* plan, with `sanity-check` then reporting **all 84 blocks and 33 types inconsistent**; the other had **Portal exit mid-run, `PROCESSES: 0`** | *One agent per component* does not cover **Portal**, which is not a component — it is a **single-writer external resource**, and CLAUDE.md already says two Openness sessions on one project is unsupported. The orchestrator dispatched a second Portal job while the rig lane held it | *** PORTAL IS A TOKEN, NOT A COMPONENT. ONE LANE HOLDS IT AT A TIME. *** Before dispatching Portal work, check no other lane has it — and if a lane's job merely *might* attach, it counts |
| A mutation test's `git checkout` restore **silently reverted uncommitted work in the same file** — removing the very seam the test needed, between two runs of the same mutation | `git checkout <file>` restores the file, not the mutation; anything else uncommitted in it goes too | **Grep for your own change before trusting the next mutation result.** Commit the seam first, or mutate a file you are not also editing |
| A lane's driver picked up **another lane's in-progress Debug DLLs** and computed its inputs one register off | One agent per *component* does not cover a shared **build output** or a **gitignored scratch dir** — both are outside the rule's reach | A lane that consumes another component's code **extracts it at an explicit commit into its own directory, builds it there, and reports the SHA**. Never reference the live tree or a `bin/` under it |

---

## HOW TO PROMPT FOR AN AUTONOMOUS RUN

What worked, condensed — put this in the opening message:

> Work through `docs/notes/test-environment-build-plan.md` autonomously per
> `docs/notes/autonomous-working-agreement.md`. Run the precondition check first and report
> **all** blockers at once. Do not stop for procedure — if a lane blocks, switch lanes. Report
> when a phase completes, when a design decision arises, or when a measurement contradicts the
> spec. Record findings in the build plan as you go; do not report each one.

**What made things worse, and is worth avoiding:**
- Adding scope to a running agent mid-task. Let it finish, then dispatch the next.
- Two agents in one component. The index and the build output are shared.
- Asking me to "continue" without clearing a known blocker — I will re-hit it.

---

## THE STANDARD THAT DOES NOT RELAX

Autonomy is about **who is asked**, never about **how carefully the work is done**. Unattended
running does not license:

- **An absence of errors read as a positive result.** Three silent-loss defects were found on
  2026-08-12 — `MemoryLayout`, a dropped `Version`, 182 discarded subelements — *** every one of
  them sitting behind a green check. *** Verify content, not exit codes.
- **A check that shares its subject's blind spot.** The converter emitted wire endpoints in the
  wrong order and the Normalizer was blind in exactly the same way, so *** the two cancelled and
  every check passed. ***
- *** A PROOF IS ONLY AS STRONG AS THE MOST INDEPENDENT AUTHORITY IN ITS LOOP. *** A round-trip
  check is structurally blind to any error the round trip **preserves**. Measured 2026-08-13: the
  converter typed every hex literal `Int` regardless of its destination, so every 32-bit build stamp
  failed to import — and `to-xml` → `to-ir --no-sidecar` passed **byte-identically**, because both
  halves of that loop are converter code. *** ALL 1,069 TESTS WERE GREEN BEFORE THE FIX AND AFTER IT.
  NOT ONE OF THEM COULD TELL THE WRONG TYPE FROM THE RIGHT ONE. ***
    ➜ **Ask of any proof: who in this loop could disagree with us?** If the answer is "nobody", it
      demonstrates self-consistency and nothing else. The checks here that have an outside authority
      are `compare` + `confirm-roundtrip.ps1` (it has been **through TIA**) and the golden harness
      against a real export; the converter round trip, `diff` and `ir-hash` have none.
    ➜ *** AND THE CHEAPEST VERSION CHECK THERE IS: COMPARE A WORDING IN THE SOURCE AGAINST THE
      WORDING THE BINARY ACTUALLY PRINTS. *** Measured 2026-08-13, third instance of a stale binary
      in one day: the source said `object element`, the running binary printed `root element`, and
      that established the mismatch **in under a minute** — where a timestamp comparison is only
      circumstantial and a behavioural test is expensive. **Do it first whenever a capability seems
      missing**, before concluding the feature was never built.
      ⚠️ *** THE STRING MUST BE ONE THE BINARY ACTUALLY EMITS — NOT ONE IN A COMMENT. *** Measured
      the same day: two of three strings picked from a fresh commit sat in **comments**, so they
      read ABSENT from the binary and would have been reported as a stale build. *** A FALSE
      "STALE BINARY" IS THE SAME CLASS OF ERROR AS A FALSE GREEN. *** Pick an interpolated
      literal from an output path, and confirm it is one before trusting its absence.
    ➜ **And an optional parameter is an invitation.** The same defect had six call sites that simply
      never passed the type; making the parameter **required** fixed the class, where fixing six
      omissions would have left the seventh to be written next year.
- *** A PLAUSIBLE MECHANISM INVENTED TO EXPLAIN A GREEN THAT WAS GREEN BECAUSE NOTHING WAS
  EXAMINED. *** This is *empty is not clean* appearing in the **explanations** rather than in the
  checks — and it is how a check keeps its blind spot, because once a green has a reason nobody
  looks again. Three instances found on 2026-08-13, all pre-existing:
    • **FI-52** — "a device compile does not clear the inconsistent flag", read as a TIA quirk.
      It did not clear it **because it never looked at the program.**
    • **FI-62** — a missed UDT attributed to *"nothing in that corpus instantiated the type"*.
      Plausible, and not the reason: **that run's device compile was hardware-only and would not
      have caught the type however many blocks instantiated it.**
    • **`stage-S1.md`** — *"confirms conclusively"* drawn from a device compile. The observation
      stands; the inference does not, and the word was withdrawn.
    ➜ **When you explain a green, ask what the check actually examined before asking why it
      passed.** A reason attached to an unexamined pass is worse than no reason — it closes the
      question.
- *** A GUARD THAT WAS WRITTEN, TESTED AROUND, AND NEVER ACTUALLY EXECUTED. *** This is the most
  reliable failure mode this project has, and on **2026-08-13 it happened three times in one day**:
  `compile`'s exit-code fix sat in source while the Release binary every skill invokes predated it;
  eighteen review rules reported *"not applicable"* on tag tables and the run exited 0; and a
  `timing` mirror-restore added that morning **could never have run at all** — the client's own
  address fence refused its own write and the tool exited before the guard that would have said so.
  In every case the surrounding code was tested and green.
    ➜ **Ask of every guard you write: what would happen if it silently stopped running?** If the
      answer is "everything still passes", the guard is decoration.
    ➜ **Prefer guards that CANNOT be silently disconnected.** The good example from that same day:
      a variable existing solely to feed the check, so removing the check trips
      `CS0219: assigned but never used` under warnings-as-errors — *disconnecting it fails to
      build.* That is a structural property, not a disciplinary one, and it is worth designing for.
    ➜ **Run the guard on the real thing at least once.** Unit tests prove the logic; only the
      device, the binary or the live invocation proves the guard is *reachable*.
    ➜ *** THE CASE A GUARD EXISTS FOR GETS TESTED. THE CASE WHERE THE GUARD ITSELF DID NOT RUN DOES
      NOT. *** Found three times in four lanes on 2026-08-13, most sharply when a lane sent to fix
      *"observability is a caller-supplied bool"* neutered its own null-check and **the suite stayed
      green** — every test passed a report, so *"the check did not run"* had no test at all. **Write
      the did-not-run test explicitly**, and prefer a design where the not-run state is
      unconstructible (`null` fails closed, a required parameter, a separate denominator such as
      `AddressesExamined`) over one where it merely fails.
    ➜ *** AND EXECUTE THE GUARD'S OWN ADVICE. A REFUSAL MESSAGE IS A SECOND ARTIFACT AND IT IS
      UNTESTED. *** Measured the same day: a client's tear-verdict guard was mutation-tested four
      ways and correct every time, and **the recovery it printed was refused by the guard itself** —
      it named the one command the guard blocks in exactly that state. The tests checked the
      *decision*; nobody had read the *sentence* while standing in the situation it describes. Do
      that, out loud, on the device.
- **Inference reported as measurement.** `[M]` means measured here, and a `[M]` that turns out to
  be a hand-authored fixture costs more than the gap it hid.
- **Guessing at hardware, tags or addresses.** Read them, or say they are proposed.
