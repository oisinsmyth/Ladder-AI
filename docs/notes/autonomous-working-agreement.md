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
| **The orchestrator re-dispatched a lane that was already running, then killed the WRONG one of the two** — the survivor was mid-work (*"Now I'll write the submission"*) while the replacement had just started | After a machine crash the orchestrator judged liveness from the agent's **output file being 0 bytes**. ***THAT FILE IS NOT WRITTEN UNTIL THE AGENT FINISHES — a live agent's output sits at 0 bytes indefinitely***, so size and mtime carry **no** liveness information. Measured twice within ten minutes, including on the replacement | *** LIVENESS COMES ONLY FROM THE NOTIFICATION STREAM *** — the "still running, do NOT spawn a duplicate" reminders and the completion/stopped notifications. **Keep a running ledger of dispatched lanes and read it BEFORE dispatching, not after a user points at the duplicate.** When unsure whether a lane is alive, **message it** — a message to a finished agent resumes it harmlessly; a duplicate dispatch does not. And it is the same *empty is not clean* error, made by the orchestrator about its own tooling |
| **A path-scoped commit carried every MODIFIED file and NONE of the five NEW ones** — the recorded history built only because the working tree still had them | `git commit -- <path>` commits **the index** for that path, and a new file is not in the index until it is added. ***The rule above already said "New files need `git add` first" — the ORCHESTRATOR'S BRIEFINGS PARAPHRASED IT AS "commit path-scoped, never `git add -A`" AND DROPPED THAT HALF***, to every lane, all day | `git add` the new files, **then** `git commit -- <paths>`, **then read `git status`**. *A commit that succeeds is not a commit that carried what you meant.* And the meta-fix: **a rule quoted from memory into a brief is a rule half-transmitted** — link or paste it, and put the *check* in the brief, not just the instruction |
| A lane's driver picked up **another lane's in-progress Debug DLLs** and computed its inputs one register off | One agent per *component* does not cover a shared **build output** or a **gitignored scratch dir** — both are outside the rule's reach | A lane that consumes another component's code **extracts it at an explicit commit into its own directory, builds it there, and reports the SHA**. Never reference the live tree or a `bin/` under it |
| **The ORCHESTRATOR's path-scoped commit swept in a lane's uncommitted append** to the same file (`9366e57`, the shared test log) | *** PATH-SCOPING PROTECTS THE COMMITTER, NOT THE OTHER LANE'S UNCOMMITTED WORK IN THAT PATH. *** The rule above is about not committing files you did not touch; this is its blind side — **a file that MANY lanes append to is shared mutable state between them**, and no commit discipline reaches it. Benign here only because an append-only file cannot lose content this way | **Treat a shared append-only artifact as a shared resource: append and commit in one step, never leave an append sitting in the tree.** *Prompt committing is cheaper than coordination.* And when reviewing a swept commit, **check the content survived** — the sweep is not automatically a loss, and reporting it as one is its own false finding |

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

## KEEPING THE PLAN AND THE POSITION IN SEPARATE PLACES

**Owner instruction, 2026-08-14.** Durable plans live in `docs/` — they are committed, reviewed, and
they stop the work deviating. ***BUT A PLAN DOES NOT RECORD WHERE YOU ARE IN IT***, and that has been
the gap: the position lived only in lane reports and in whatever the orchestrator happened to
remember.

> **The `/plan` file holds the LIVE EXECUTION STATE: the phase now in progress, in detail, with its
> exit criterion and its actual blockers. It is updated at every phase and stage transition.**

- **A plan says what we intend; the plan file says what is true right now.** Keep them apart — a
  planning document that is edited to track progress stops being a plan and becomes a log.
- **Write the detail of the CURRENT stage**, not a checklist of stage names. *"Run the vectors"* is a
  heading; *"the loop is a library with no entry point, and `Execute` always deploys"* is the state.
- **Update it on transition, not at the end.** The value is entirely in being able to pick the work
  up cold without reconstructing it from lane reports — which is exactly the situation a crash, a
  compaction or a handover produces, and all three have happened here.
- **Carry the measured facts forward in it** so they are not re-derived. Re-measuring is cheap;
  re-deriving from memory is how a stale number gets quoted as current.

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
      ⚠️ *** AND CHECK THE RIGHT ASSEMBLY. *** Measured the same day: a CLI exe was **older than the
      last commit to its own component** and read stale by timestamp — it was not. The gate names
      lived in the **library** the shim calls, not in the shim. **Checking the wrong assembly
      produces a false "stale binary" just as a comment string does.**
    ➜ **And an optional parameter is an invitation.** The same defect had six call sites that simply
      never passed the type; making the parameter **required** fixed the class, where fixing six
      omissions would have left the seventh to be written next year.
- *** AN OVER-FIRING GATE DECAYS INTO A WARNING — WHICH IS THE RULE ABOVE, RUNNING BACKWARDS. ***
  *A warning is not a gate* says fail closed on the path that reaches production. Its complement,
  measured 2026-08-13: a gate built correctly but asked in the **wrong order** refused submissions
  that **did not depend on it at all** — a model-ordering check tested *"is a model slot present?"*
  before *"is there already a passing result?"*, so a submission carrying **both** was refused for
  want of a gate it never needed. Three existing tests caught it.
    ➜ *** CONSERVATIVE IS NOT THE SAME AS CORRECT. *** A gate that fires on cases outside its scope
      is **noise, and noise gets switched off** — after which the cases it *was* right about go
      through unchecked. The two failure directions are not symmetric in appearance but they end in
      the same place.
    ➜ **So test the unaffected case as deliberately as the refused one.** *"A plan that does not use
      this route is untouched"* matters as much as *"the route without its precondition is refused"*.
      A gate refusing every ordinary submission is removed within a week — and it will be removed by
      someone who is right to.
- *** TWO FLAGS SIDE BY SIDE WHOSE OMISSIONS FAIL IN OPPOSITE DIRECTIONS ARE A TRAP THAT LOOKS LIKE
  SYMMETRY. *** Measured 2026-08-14 on the copy layer's two signal-level properties. Forgetting
  `Transient` produces **no latch**, and gate 5 refuses the `Latched` expectation — *loud, before
  anything is spent*. Forgetting `RearmsEachIndex` produces a **one-shot latch that compiles, deploys
  and reads plausibly** — and the only symptom is the predicted findings quietly absent from a rig
  run. **They sit adjacent, they are declared the same way, and they look like a pair.** Anyone
  reasoning by analogy from the loud one will trust the silent one exactly as far, and be wrong.
    ➜ **The fix is NOT to flip the silent default**, which here would refuse every ordinary transient
      — *the asymmetry is an argument for closing the gap, not for moving it.* Where the gap cannot be
      closed tonight, ***write the asymmetry at the declaration site, where someone tidying the flag
      will meet it*** — not in a report, which is read once by a person who already knew.
    ➜ **Ask it of any new optional flag: if this is omitted, does the system SAY so, or does it
      produce a plausible artifact?** A flag whose omission yields a plausible artifact is not
      optional; it is a defect with a default.
- *** A DECLARATION IS A TRANSFERRED RESPONSIBILITY, NOT A VERIFICATION. *** The stamp trick — carry
  *what it was established against* rather than a bare `bool`, so *"nobody did it"* and *"did it
  against a different version"* come out as distinct facts — has been applied four times here and is
  the right default. **But it has a ceiling worth stating in the code, not only in a report:** a
  component that cannot execute the thing it is asking about can make absence a refusal, demand an
  author and evidence, and detect the version drifting. ***It cannot make a false declaration true.***
    ➜ **Print the limit where a reader of RESULTS meets it**, not only where a reader of the design
      does. A caveat that lives in a lane report has already failed the person it was written for.
- *** THREE DEFECTS THAT LEAVE EVERY OUTWARD SIGN OF CORRECTNESS INTACT — ONE FAMILY, ALL FOUND ON
  2026-08-13, NONE FINDABLE BY RE-READING. *** They are worth naming together because the instinct
  each defeats is the same one: *read the thing again and see if it still looks right.*
    • **The narrowing repair** — *drops a claim while it still looks intact.* Title, clause ID,
      cross-references and assertion count all survive; only the content goes.
    • **The self-referential parameter** — *keeps the claim and empties it.* "At its threshold" is
      true of every implementation, including every wrong one.
    • **The wrong pointer** — *documents a case as handled and steers the reader away from the real
      gap.* Worse than a missing pointer, because it satisfies.
    • **The guarantee that licenses the damage** — *a correctness property, correctly stated, that
      makes an unsafe operation look safe, because the property does not cover the thing that actually
      depends on it.* An ID scheme guaranteed IDs are **non-positional** — true and load-bearing — so
      reordering reads as free. It **is** free for IDs, and silently retargets every ordinal citation
      in prose, where the check that rejects ordinals cannot reach. *** THE PROPERTY THAT MAKES THE
      DESIGN SOUND IS WHAT MAKES THE UNSAFE EDIT LOOK SAFE. ***
      ➜ **Ask of any guarantee: what does it NOT cover, and who is relying on the uncovered part?**
      ➜ And the discipline that had actually been protecting us — *append, never insert* — **was
        written down nowhere.** It was followed as a habit, for an unrelated reason. **An invariant
        held by habit is one nobody can be asked to keep.**
    ➜ **What found all three was CROSS-REFERENCING, never reading**: checking a clause against what it
      used to claim, against what supplies its terms, and against the assertions it says it relies on.
      *** CROSS-REFERENCING A DOCUMENT AGAINST ITS OWN DEPENDENCIES IS A DIFFERENT OPERATION FROM
      READING IT, AND ONLY ONE OF THEM FINDS THIS FAMILY. ***
- *** ASSERT AN ABSENCE, SO THAT ITS FUTURE PRESENCE FORCES THE REAL CHECK. *** Measured 2026-08-13,
  and it is the neatest way found here of stopping a derived value quietly becoming a corroborated one.
  A mirror's `Time` tag form could not be corroborated against the committed TIA export corpus, because
  **the corpus contains no `Time` tag at all** — so the form is *derived* (from the width and another
  member's address form), not confirmed. Rather than note that in prose, the lane **asserted the
  absence in a test**: the day a real `Time` export appears, the assertion fails and demands the
  comparison that was never possible.
    ➜ **A caveat in a report decays; a caveat that is a red test cannot.** When you must derive
      something because no authority exists yet, *pin the non-existence of the authority.*
    ➜ Same lane, same shape, worth pairing: **the converter is an authority on grammar, not on types**
      — measured, because it converted `MOVE(IN := <Bool>)` at exit 0 while TIA refused it. **Knowing
      exactly what a green from a given tool does NOT cover is what makes the green usable.**
- *** DO NOT FIT A CHECK TO THE IMPLEMENTATION IT HAPPENS TO BE CHECKING. *** Measured 2026-08-13,
  and it is the correlated-check rule at its narrowest and hardest to notice. A violation latch needed
  **two** terms — a live reset *and* a held "reset since the raise" record — because the live term
  alone misreads a fall *caused by* the pulse, and the held term alone misreads a fall *after* it.
  *** THE ONE-TERM FORM WOULD HAVE PASSED, BECAUSE THIS PARTICULAR BLOCK HAPPENS TO SATISFY IT *** —
  and the lane wrote both anyway, on the grounds that **the block's internal timing is not the
  harness's to assume.**
    ➜ **A check simplified until it still passes against the current implementation has silently
      become a change detector.** It will keep passing when the implementation changes in a way the
      requirement forbids.
    ➜ Same lane, same instinct: an inequality between two outputs was built as **two latches, not one**
      — *one could not say which way round it went* — and commented with **what the pair does not
      constrain**, since both outputs moving together at the wrong moment satisfies it.
    ➜ And: **clear evidence on the harness DROPPING its start bool, never on the edge.** *A latch
      cleared on the edge is cleared again by a harness restart mid-run, taking the evidence with it.*
- *** RECONCILE AGAINST THE ARTIFACT, NEVER THE REPORT ABOUT IT — ESPECIALLY WHEN THE RESULT LOOKS
  LIKE THE FAILURE YOU WERE HUNTING. *** Measured 2026-08-14 by a lane that nearly filed a false
  finding **twice in one session**: `$?` after a pipe read the *last* command's status rather than the
  tool's, and a bad `sort` made a correct run look like exactly the headline failure it was testing
  for. Reading the store on disk settled both. The orchestrator made the identical `$?` mistake the
  same day, on the same kind of check.
    ➜ *** A MEASUREMENT THAT MATCHES YOUR HYPOTHESIS IS THE ONE TO RE-TAKE. *** The moment a result
      confirms the defect you went looking for is the moment the instrument stops being questioned.
    ➜ **Prefer the artifact to any summary of it** — the store, the export, the emitted bytes — and
      when a count is the finding, **count it in the thing itself.** A total agrees with a wrong
      derivation as readily as with a right one; **per-item evidence does not.** *(The strongest form
      seen: sixteen agents each seeing a distinct count 1…16 — serialisation demonstrated rather than
      inferred from a total of sixteen.)*
    ➜ *** NOR AGAINST YOUR OWN MODEL OF WHAT THE ARTIFACT SHOULD CONTAIN. *** The sharpest form, and
      the one that survives the other two: a release script derived `ALLOC-i → FC(399+i)` from
      **dispatch order**, so half the releases silently did nothing — *allocation order is not
      dispatch order.* It trusted no agent's report; it trusted **its own prediction of a value it
      could have simply read.** Third and fourth instance in two sessions. **If the artifact can be
      read, reading it is never the expensive option.**
    ➜ **And it applies to a TASKING, not only to a result.** Measured 2026-08-14: the orchestrator
      briefed a lane from a stale picture, and the lane **checked each item against the source before
      executing it** — finding most of the work already landed and redoing none. *An instruction is a
      report about the world too.*
- *** WHEN A LOCK IS A HANDLE, DELETING THE FILE IT LIVES IN IS NOT CLEANUP — IT IS A SECOND, WEAKER
  LOCK WITH A DIFFERENT FAILURE MODE. *** Measured 2026-08-14, and it is the campaign's best find
  because ***it was invisible at 8, 16 and 32 concurrent agents and crashed at 48.*** `Dispose`
  deleted the lease file; on Windows a **delete-pending** file answers `CreateFile` with
  **ACCESS_DENIED**, not the `IOException` a sharing violation raises — so it walked straight past an
  `IOException`-only retry. The exclusive handle was already the lock; **the delete bought nothing and
  opened the race.**
    ➜ *** A DEFECT'S CONCURRENCY FLOOR SITS WHERE NOBODY LOOKED. *** Every level anyone had run was
      clean. **Pick the number that feels sufficient, then go well past it** — and when a sweep is
      clean end to end, that is evidence about the range, never about the mechanism.
    ➜ **Retry on the exception the OS actually raises, and separate the persistent case.** Contention
      raises a sharing violation; an access denial that never clears is a *permissions* problem, and
      retrying it into a timeout reports it as contention.
    ➜ **A crash is loud without being NAMED.** A harness cannot tell an unhandled exception from a
      refusal, so a top-level catch that names it is not decoration — it is what makes the difference
      reportable.
- *** A TEST CAN PIN THE DEFECT AS THE CONTRACT — AND THEN THE NEXT PERSON TO FIX IT GOES RED AND
  CONCLUDES THEY BROKE SOMETHING. *** Found 2026-08-14: `drift-check`'s `Run_UnpairedIrDoesNotFail_…`
  asserted `HasDrift == false` **with a comment naming the condition outright** — *"Nothing is COMPARED
  here at all — and without `--complete` none of it fails."* ***THE BUG WAS NOT UNDETECTED; IT WAS
  DOCUMENTED, ASSERTED, AND DEFENDED.*** That is how a defect acquires tenure.
    ➜ **When a fix turns an existing test red, read the test's INTENT before assuming the fix is
      wrong** — and when the test guarded a real property (here: a committed corpus may legitimately
      lag), **keep the property and re-test it against a run that actually compares something.**
- *** AN EDIT THAT SUPERSEDES A SECTION MUST DELETE THE SUPERSEDED ONE — AND WHEN A DOCUMENT
  CONTRADICTS ITSELF, THE READER OBEYS THE MOST ACTIONABLE HALF. *** The orchestrator rewrote the
  readiness page's headline to *"the binary is current, nothing owed"* on 2026-08-14 and **left the
  whole obsolete section below it**, still saying *"Run this first thing"* and *"UNTIL YOU DO, TWO
  DIAGNOSTICS ARE LYING."* ***THE STALE HALF WAS THE ONE WITH A COMMAND IN IT*** — so a morning reader
  would have rebuilt an already-current binary and gone on distrusting two diagnostics that had just
  started telling the truth. **Caught by a lane, not by its author.**
    ➜ **After editing a status claim, grep the document for the claim you just reversed.** *A stale
      instruction outranks a fresh statement, because one of them tells the reader what to do.*
    ➜ **And do not leave a placeholder in a delivered document** — a literal `05:5x` shipped in the
      same edit. *If the exact value does not matter, do not print one.*
- *** THE CRLF RULE IS NOT UNIFORM, AND `.gitattributes` IS THE AUTHORITY — NOT THE GENERAL RULE. ***
  The orchestrator did this on 2026-08-14, having spent the night telling lanes to verify endings with
  `file`: it converted a tool-written `.json` fixture to CRLF **because the repo convention is CRLF**,
  without checking that `.gitattributes` **exempts tool-written text formats and forces LF on purpose**
  — *"so fresh checkouts and worktrees match the converter's own output"*, owner-approved 2026-07-16.
  ***HAD THAT FILE BEEN ONE OF THE BYTE-COMPARED FIXTURES, APPLYING THE HOUSE STYLE WOULD HAVE BROKEN
  THE TESTS THE EXEMPTION EXISTS FOR.*** It was harmless only because `.gitattributes` overrode it.
    ➜ **Check `.gitattributes` before normalising anything.** *A convention applied where it was
      deliberately suspended is indistinguishable from not knowing the convention.*
    ➜ **And a second-order slip from the same minutes: `git show --stat | tail -3` read the COMMIT
      MESSAGE, not the file list**, because a long body pushes the stat out of the window — briefly
      suggesting the wrong file had been committed. ***A truncated view of a verification is not a
      verification;*** the follow-up that settled it counted the actual keys in the committed blob.
- *** A WHOLE-CORPUS SWEEP REACHES DEFECTS NO TARGETED PROBE CAN, BECAUSE THE INPUT YOU WOULD HAVE TO
  GUESS IS ALREADY SITTING IN THE REPOSITORY. *** The strongest instance, 2026-08-14: validating a new
  `preflight` gate over **all 92 committed `.ir` files** produced **154 findings, every one false** —
  and the cause was **older than the change and in a different tool.** `tagstatus` — *the
  anti-laundering tool, the one enforcing hard rule 3* — was returning `MEMBER-NOT-FOUND` on **C-501
  alarm-bit slices**, the one construct doc 06 documents an exception for, which the converter has
  handled since 2026-07-10. **A night of adversarial probes had not reached it.**
    ➜ *** A GATE THAT ACCUSES CORRECT WORK OF THE MOST SERIOUS OFFENCE IN THE PROJECT IS ONE THAT GETS
      DISBELIEVED *** — and the day it is right, nobody looks. **Validate a new gate against the real
      corpus before believing it, and report the FILE COUNT you swept.**
    ➜ **And prefer three verdicts to two**: the fix distinguishes *invented member* from
      `IndexOutOfRange` from **accepted-unchecked** (slice widths it cannot know). ***Declining to
      judge is the correct verdict when you cannot; inventing one is what caused this.***
- *** THE TRANSPORT THAT WOULD REPORT THE PROBLEM IS SOMETIMES THE ONE THE PROBLEM SWITCHES OFF. ***
  Found 2026-08-14 while asking whether a CPU could be started over Modbus: **`MB_SERVER` is a PROGRAM
  BLOCK, and in STOP the program does not execute** — so nothing answers on `:503` precisely when a
  stopped CPU is the thing you need to observe and fix. ***A diagnostic path that shares a dependency
  with the fault it diagnoses is not a diagnostic path.***
    ➜ **Ask of any recovery route: does it survive the state it exists to recover from?** Here the
      answer put the capability on a different transport, and made the honest limit printable — *this
      binary can start that CPU and cannot stop it.*
- *** A PREDICTION INHERITS THE AGE OF THE FACT IT RESTS ON. *** Same day, two agents measured the same
  device and disagreed: one reported job-class S7 access *"refused CPU-wide"*, the other measured
  `MBRead(0,1) -> ok` **with a different error code entirely**. The likely reconciliation is that
  **the owner enabled PUT/GET part-way through the project**, so both were right about different days
  — and an inference built on the older reading (*"a run request would be refused"*) had quietly lost
  its basis. ➜ **When quoting a measurement to support a prediction, say WHEN IT WAS TAKEN and WHETHER
  YOU TOOK IT.** *A recorded fact and a fresh one are different evidence, and only one of them ages.*
- *** A FAIL-CLOSED GATE MEETING AN UNANTICIPATED LEGITIMATE CASE IS THE EXPECTED COST OF FAILING
  CLOSED — AND FAR CHEAPER THAN THE CONVERSE. *** First real contact with `diff --only`'s new
  fail-closed header rule, 2026-08-14, hours after it landed: a `lad-coder` run repaired two **stale
  block comments** alongside a legitimate scoped edit, and the gate refused with
  `HEADER changed: comment`. ***The agent declined to reach for `--allow-header`, correctly*** — that
  flag belongs to the purpose-change route, and reaching for it means *route, not declare*.
    ➜ **The defect was in the TAXONOMY, not the gate.** `--only` asks *"could anything outside the
      named networks change what the PLC does?"* An **interface** change answers yes; ***a comment
      cannot.*** The verdict conflated them — **while the tool's own JSON already separated them**
      (`commentChanged: true`, `interfaceChanged: false`). *The information was there; the verdict
      threw it away.*
    ➜ ***DO NOT FIX THIS BY WIDENING IT TO "THE HEADER NO LONGER GATES."*** That is one careless
      generalisation away, and it is the direction that costs something.
    ➜ **And the case had no clean route at all**: a documentation-only header repair is not a purpose
      change and the fix path cannot express it — **while leaving a false comment is not a neutral
      option either.** *When a gate has no legitimate path for a legitimate act, that is a finding
      about the routes, not about the actor.*
- *** A GATE LANDED WITHOUT ITS CALLERS IS A BROKEN PIPELINE. *** Same day: `diff --only` was made to
  fail closed on unclaimed header changes, and **the calling skills were updated in the same commit** —
  `modify-purpose` may pass the new `--allow-header` **and must then quote the interface delta**;
  `modify-fix` must **never** pass it, *because a fix needing an interface member routes, it does not
  declare.* ➜ **An escape flag must cost something**: with it the gate stops arguing, so **the reader
  becomes the only remaining check** and must be told that in the same breath.
- *** A TEST OF A LITERAL IS EVIDENCE ABOUT THE LITERAL AND NOTHING ELSE. *** The starkest instance
  this project has found, 2026-08-14: the harness's **strongest safety claim** — *"the capability is
  absent from the assembly"* — was backed by `Assert.False(Arming.CompiledIn)` where `CompiledIn` is a
  ***`const bool`***. **A class constructing a live socket client was planted in that assembly and all
  202 tests stayed green.** ***A CONSTANT STAYS TRUE EXACTLY AS LONG AS SOMEONE REMEMBERS TO CHANGE
  IT, WHICH IS THE ONE THING A FENCE MUST NEVER DEPEND ON.***
    ➜ **A claim about an ASSEMBLY needs a walk over the assembly** — with a denominator *and* a live
      positive control, which catch different failures and neither subsumes the other.
- *** A CHECK WHOSE EXERCISE REQUIRES EDITING THE CHECK WILL NOT BE EXERCISED. *** Root cause of the
  decayed IL-walk control, same day: the searched member name was a **literal**, so the only way to
  run the negative control was to **hand-edit the test** — *which is exactly how it decayed into a
  comment describing a manual run somebody once did.* **Making it a parameter is what makes the
  control runnable, and therefore what makes it survive.**
    ➜ **When you find a control that rotted, ask what it COST to run.** The rot is usually the price,
      not the diligence.
- *** AN HONEST REPRESENTATION CAN BE RE-CONSUMED AS DATA ONE LAYER DOWN. *** Measured 2026-08-14: a
  gateway deliberately left an unknowable timestamp at `default` — *"rather than filled with a
  plausible-looking value"*, correctly, and said so at the site. **The consumer then compared the
  default as if it were a measurement**, so `0001-01-01 < any real start` fired a *"this cannot be
  true"* alarm on every process of that kind. ***DOING THE RIGHT THING AT LAYER N CREATED THE DEFECT
  AT LAYER N+1*** — nastier than a mistake, because **reviewing the site that "caused" it finds
  nothing wrong there.**
    ➜ *** A TEST HELPER'S DEFAULT IS A SILENT ASSUMPTION ABOUT WHICH INPUTS ARE POSSIBLE. *** This one
      defaulted the field to a real timestamp, so **no fixture ever built the shape production
      emits**, and an entire real class was untestable while every test passed.
    ➜ **And the orchestrator's own diagnosis — *"date-blind comparison"* — was wrong.** The lane
      checked rather than implementing it. ***An orchestrator's guess is a report about the world, not
      the world.***
- *** AN ASSERTION THAT CANNOT FAIL IS DOCUMENTATION WEARING A CHECK'S CLOTHES — AND IT IS WORSE THAN
  A MISSING CHECK, BECAUSE IT OCCUPIES THE SLOT. *** Measured 2026-08-14 inside gate 8: it computes
  `effective = computed ∪ declared` and then asserts `computed ⊆ effective` — ***true for every input,
  by construction.*** Nobody looking at that gate would think a blacklist check was owed.
    ➜ *** WHEN A MUTATION YOU EXPECTED TO GO RED STAYS GREEN, THE FINDING IS USUALLY IN THE ASSERTION,
      NOT IN THE MUTATION. *** That is how this was found — the obvious mutation could not fail.
    ➜ **Labelling beats deleting when the line still communicates intent**: the tautology stays, now
      pinned by a test that says it is unfalsifiable, *so nobody later reads it as evidence.*
- *** AN ARCHITECTURAL-SOUNDING EXPLANATION IS THE MOST DURABLE WAY TO BURY A CHORE. *** Same day: a
  gate was triaged `NOT CHECKED — by design, the submission cannot adjudicate its own author`. True,
  principled, and **wrong**: handed a loadable binding the gate *ran* and returned `REFUSED`.
  ***A GATE THAT RETURNS A VERDICT IS NOT ONE THAT CANNOT RUN.*** The real state was a third thing —
  the coordinator's binding **exists as PROSE** and the flag needs **data**, so ***what was owed was a
  TRANSCRIPTION, not a decision.*** "By design" would have retired a transcription job as a law of
  nature.
    ➜ **Triage by measurement, not by category.** Before filing anything as *by design*, **supply what
      it says it lacks and watch what it does.**
    ➜ *** AN EMPTY CLASSIFICATION IS A SLOT WAITING TO BE MISUSED. *** If a triage category ends up
      with no members, **retire it visibly, with the reason** — otherwise the next merely-inconvenient
      case gets filed under it, and *by design* is unfalsifiable once nobody remembers it was measured
      empty.
- *** DELETING AN INPUT FROM A SYSTEM THAT REPORTS DERIVED CONCLUSIONS DOES NOT PRODUCE A SMALLER
  CONCLUSION — IT PRODUCES A WRONG ONE. *** Measured 2026-08-14 while looking for a *safe and small*
  slot-scoped recovery. With the slot present the store reported two wave sets separated by a conflict
  edge; with it removed it reported ***`NO CONFLICT EDGES: the slots are disjoint on every computed
  relation`*** — **a positive claim of disjointness about a hazard it can no longer see.** Acting on
  it puts two agents on one FB instance: ***the recovery would have caused the incident it was added
  to clean up after.***
    ➜ **Rank them: a silent gap is bad; a confident false assurance is worse than either.** Before
      removing a record, ask **what the system will now ASSERT** in its absence — not merely what it
      will stop knowing.
    ➜ **The liveness lesson beside it: the only liveness fact available was RE-SUBMISSION** — a live
      agent re-submits, a dead one does not. ***Obtained by doing the work, not by asking a question
      the system cannot answer.*** Both alternatives failed for stated reasons: ownership cannot help
      (a dead agent cannot clear its own slot — the whole problem), and a timer must not (*an expiry
      heuristic is a wrong answer delivered on a schedule*).
    ➜ **And record a defect's DIRECTION in the runbook.** This leak **over-separates** — the safe
      direction — so it is *not* an emergency, and saying so stops someone reaching for the
      destructive remedy under time pressure. **A runbook that omits severity causes its own damage.**
- *** A CONTROL THAT IS NOT EXECUTED IS A NOTE ABOUT A CONTROL. *** Found 2026-08-14 on the single
  most load-bearing assertion in the repository — the IL walk certifying that **no method in
  `openness-cli` can reach `DownloadProvider.Download`.** It is genuinely armed *(retargeting the
  predicate at `Compile` fails it)*, **but force its body-reader to return `null` and it passes while
  examining ZERO method bodies**: `offenders.Count == 0` is true of *nothing found* and of *nothing
  looked at*. Its negative control existed only as a **comment recording a manual retarget** — while
  its **sibling walker in the same repo has a live one.**
    ➜ **A structural guarantee needs BOTH: a positive control proving the walk detects, and a COUNT of
      what it examined asserted `> 0`.** Neither alone separates the two zeros.
    ➜ ***AND WHEN A PATTERN ALREADY EXISTS IN THE REPO, THE QUESTION IS WHICH SIBLINGS DID NOT GET
      IT.*** One walker had the control and one did not; nothing flagged the difference.
- *** A GATE HELD IN ONE `if` IS ONLY AS GOOD AS THE TEST THAT ROUTES THROUGH IT. *** Same day: the
  `--yes` gate on `block-layout --set` — the only thing between a missing flag and a write that
  **destroys retained data** — can be disconnected with a one-token mutation and **712 of 712 tests
  stay green.** The test calls the refusal helper **directly**, so it pins the *message* and not the
  *routing*, and the downstream call site never re-checks. **The gate is connected today, measured on
  the shipped binary; nothing would notice if it stopped being.**
    ➜ **Test a fence through the entry point a caller actually uses, and assert the OBSERVABLE
      CONSEQUENCE** — the sentinel that proves Portal was never contacted — **not the exit code**,
      which a disconnected gate can still produce by accident.
- *** A FALSE ALARM IN A DIAGNOSTIC IS THE SAME CLASS AS A FALSE GREEN *** — it teaches its reader to
  discount the one time it is right. Measured 2026-08-14: `portal-status` warned that a process
  *"report[s] an ACQUIRED time EARLIER THAN THEIR OWN START TIME — a value that cannot be true"* about
  a pair that is **in the correct order** (`STARTED 08-13 19:19`, `ACQUIRED 08-14 02:49`), reading as
  a date-blind comparison. **A diagnostic that cries wolf is worse than a silent one**, because it is
  the tool people reach for when something is already wrong.
- *** AN ABSENCE THAT IS CORRECT EXACTLY ONCE IS A BUG FOR THE REST OF TIME. *** The strongest form of
  *empty is not clean* this project has produced, measured 2026-08-14 by killing 27 agents mid-write.
  `File.Replace` is **not atomic against process death**: it leaves a window in which the destination
  **does not exist**. A reader landing there got `FileNotFoundException` and read it as an **empty
  store** — ***correct exactly once, before anyone has ever submitted, and catastrophic every time
  after.*** Eight committed wave sets vanished and **the surviving submission reported success.**
    ➜ **First-run semantics quietly become steady-state semantics**, and the day they diverge nobody
      is watching. **Qualify the absence** — here, a marker written *before* the first publish — so
      that *"never"* and *"lost"* are different facts instead of the same one.
    ➜ *** A REVIEW THAT ESTABLISHES THE WRITER IS ATOMIC SAYS NOTHING ABOUT HOW THE READER INTERPRETS
      THE WRITER'S FAILURE. *** This was found in a path already reviewed for exactly this class: the
      rename had been thought about, the **reader's reading of absence** had not.
    ➜ **And report an unknowable count as `UNKNOWN`, never as `0`** — *`0` says "nothing to lose"
      about the one case where there may have been a lot.*
- *** A GUARD THAT BLOCKS ITS OWN RECOVERY PATH TURNS A RECOVERABLE INCIDENT INTO AN OUTAGE. *** The
  fix above initially refused `reset` as well, because `reset` read the outgoing count before clearing
  — leaving the store recoverable only by hand-editing `ProgramData`. **Found by running the tool, not
  by reading it.** ➜ **Whenever you add a fail-closed check, run the ESCAPE HATCH under it.**
    ➜ *** AND CHECK THE RECOVERY ADVICE YOUR ERROR MESSAGE GIVES, BECAUSE ADVICE IS A CLAIM. *** The
      message said the newest `.tmp-*` is a complete store; **restoring one recovered 17 slots.** An
      untested instruction in an error message is a guess offered to someone in trouble.
- *** A NON-RECURRENCE IS NOT A DEMONSTRATION. *** A second 30-kill round lost nothing — **and never
  entered the race window**, so it is evidence about nothing. Banking it as confirmation is how a
  guard comes to be believed on the strength of runs that never reached it. **Say which of your runs
  actually entered the condition under test**; the rest are background.
- *** A MISSING RESULT IS NOT A PASSING RESULT — AND ONLY ONE OF THEM ANNOUNCES ITSELF. *** Two
  instances in one round, 2026-08-14, **both inside the lane's own test harness**: a probe silently
  failed (Windows Python cannot open a `/c/...` path) so every case read an intact store and exited 0
  — ***a clean sweep measuring nothing, produced by the instrument built to detect exactly that*** —
  and a mutation that did not compile emitted **no result line at all**.
    ➜ **Count your result lines against your case list before reading any of them.** A sweep that
      cannot say how many cases it ran cannot say that they passed.
- *** A TEST THAT PASSES FOR THE WRONG REASON IS INDISTINGUISHABLE FROM ONE THAT PASSES. *** Caught by
  the lane that wrote it, 2026-08-14: a test for *"an unknown key is silently dropped"* used
  `specname`, which **binds** — both readers set `PropertyNameCaseInsensitive`. It was green,
  committable, and **evidence for a proposition it never tested.**
    ➜ **Name the vector before writing the input.** The subject there was *a field that VANISHES*, and
      a field that binds has not vanished. **A near-miss input that happens to work is the most
      dangerous fixture there is**, because nothing downstream can tell it from the intended one.
    ➜ **This is the same family as the unexecuted guard, one level up:** the guard ran, the assertion
      held, and the thing under test was never reached.
- *** A NARROWING NOBODY CAN SEE BECOMES A PLACE TO HIDE. *** When a check is scoped down — a prefix
  excluded, a class skipped, a directory ignored — ***PRINT THE COUNT OF WHAT IT EXCLUDED, ON EVERY
  RUN.*** 2026-08-14: gate `0b` was refusing a deliverable over its own 82 `_`-prefixed annotations;
  the fix excludes them **by name and reports `82 annotation(s) EXCLUDED` every time.** A silent
  exclusion has **no cost to grow**, and the first person to widen it will be solving a real problem.
- *** AN UNREACHABLE REFUSAL IS WORSE THAN NO REFUSAL. *** Measured 2026-08-14: a capability gap was
  implemented as a refusal — correctly — but the flag that triggers it had **no wire representation**,
  so no caller could reach it. **The system looks like it has a guard.** The gap was then met on the
  **rig**, where its symptom is *predicted findings quietly absent.* Third instance of *the domain
  model gained a field and the wire format did not* — **when you add a property whose job is to cause
  a refusal, follow it to the wire before believing the refusal exists.**
- *** A DEFENCE BUILT ON TOP OF A GUARANTEE YOU ALREADY HAVE IS PURE RISK — IT CANNOT ADD SAFETY, AND
  IT CAN SUBTRACT IT. *** The closing half of the 48-agent crash, measured 2026-08-14 by killing the
  holder: `kill -9` on a lease holder yields the next acquirer **exit 0, immediately**. ***THERE IS NO
  STALE-LEASE STATE TO DETECT*** — the lock is the handle, so the OS releases it on process death.
  **The delete that opened the ACCESS_DENIED race was doing a job the operating system already did**,
  and the crash was the entire return on it.
    ➜ **So the fix retires the stale-lease detector as UNNECESSARY, not as deferred** — say which,
      because a "deferred" item gets built later by someone who reads the list and not the reason.
    ➜ **Before hardening anything, ask what already guarantees it.** A redundant guard has no upside
      to weigh against its own defects, and it will be defended on the grounds that it is *safe*.
- *** AN ERROR MESSAGE THAT ASSERTS A CONCLUSION ITS CODE CANNOT REACH IS A FALSE CLAIM SHIPPED IN THE
  PRODUCT. *** Found 2026-08-14 by the lane that wrote it: a lease timeout printed *"a timeout this
  long means the holder is stuck rather than busy"* — **a hung holder and a busy one are
  indistinguishable from there, permanently.** Worse than a wrong comment, because **a human reads it
  at the moment they are deciding what to do, under time pressure**, and it will send them to kill a
  process that was merely slow.
    ➜ **State the facts and hand the judgement over**: how long it waited, that the holder is alive,
      that the two cases cannot be told apart from here. *Declining to add the heuristic is only half
      the fix if the sentence claiming it stays.*
- *** ASK WHICH TRANSPORT ACTUALLY CARRIES THIS. *** Measured 2026-08-14, and it is the **fourth**
  instance of the unexecuted-guard family — but the first found by a question rather than by a
  mutation. A scan-counter wrap **is** absorbed, correctly, with a passing test, in the **S7** read
  path. *** S7 VARIABLE ACCESS IS REFUSED CPU-WIDE ON THIS RIG, SO EVERY DATA READ GOES OVER MODBUS —
  AND THE MODBUS PATH ABSORBS NOTHING. *** Three call sites then fail closed **with the wrong
  diagnosis**, and one prints a negative delta as *"after N scan(s)"* on a completed slot.
    ➜ **A guard is only as reachable as the route its callers actually take.** When two transports
      exist, "it is handled" is a claim about **one of them** — and the tested one is usually the one
      that was easy to test.
    ➜ **The fix is not urgent and the finding is not the wrap** (a `DInt` at 23.33 ms/scan wraps after
      ~580 days). *The finding is that a guard lived where nothing runs*, which is worth more than the
      arithmetic it protects.
- *** WHEN A CONSTANT IS USED CORRECTLY BY CANCELLATION, SAY SO AT THE SITE. *** Same audit: one use of
  a bound-shaped constant was **not a bound** — it was the algebraic *inverse* of a figure computed
  with the same constant, and is right only because the two cancel. The project's rule for choosing
  between constants (durations → p90, bounds → p99) **does not reach it**, and the rule that governs
  it — *use the same constant the figure you are inverting was computed with* — **was written
  nowhere.** ➜ **A value that is correct for a reason no rule states is one somebody will "correct".**
- *** A COMPONENT THAT CAN ONLY BE ASSERTED ABOUT HAS A CLASS OF DEFECT NO ASSERTION REACHES. ***
  Measured 2026-08-14, and it is the strongest argument this project has produced for building a
  driver. `WaveControl` had **no entry point** — 375 unit tests, all green, and the assembly was
  reachable only from its own test project. The first time a CLI wrote a real store and read it back:
  *** THE PARSER TRIMMED EACH LINE, THE LAST FIELD WAS EMPTY FOR EVERY ORDINARY SLOT, AND THE TRIM ATE
  THE TRAILING TAB — SO EVERY STORE WRITTEN BY A NON-MODEL SLOT WAS UNREADABLE. *** Three concurrent
  submissions failed the first time the tool met a real file.
    ➜ **The tests round-tripped through OBJECTS. The defect lived strictly between the file and the
      parser** — a region no assertion about the objects can enter. Same shape as *a guard nothing can
      reach*, one level out: there the check was unreachable, here **the whole serialisation boundary
      was.**
    ➜ **So: if a component has no way to be driven, that absence is a finding in its own right** — not
      a convenience gap. Every green it has is scoped to the half somebody could call.
    ➜ Same round, same component: two conflict-edge kinds **had no producer**, so those edges could
      only ever arrive **caller-supplied** — *** WHICH MADE THE CONCURRENCY RULE ASPIRATIONAL: A
      SUBMITTING AGENT COULD DECLARE ITSELF INDEPENDENT. *** **Ask of any rule: who computes its
      inputs? If the answer is "the party the rule constrains", it is not a rule.**
- *** WHEN AN OBJECT HAS NO DERIVABLE PROPERTY, LOOK AT WHAT REFERS TO IT. *** Measured 2026-08-13. A
  tag table carries **no number**, so classifying it looked like it had to fall back to its **name** —
  a convention anything can be renamed into. I authorised reporting it *unclassified* as the honest
  answer. It was not the best one: *** A TAG TABLE HAS NO NUMBER, BUT A TAG HAS REFERRERS, AND A
  REFERRER HAS A NUMBER. *** Classification moved **per tag**, off the graph, and the table's name
  stopped being an input at all.
    ➜ The property that makes it hold: **laundering now costs what it should.** Renaming a plant table
      to the harness name is a *literal no-op*; to launder a tag you must move **every reference** into
      the reserved band, **which breaks the plant program.** *When a classifier cannot be fooled
      without breaking the thing being classified, it is derived rather than declared.*
    ➜ **So before accepting an honest gap, ask what the object is connected to.** Identity is often
      available one level out even when it is absent on the object itself.
    ➜ And it stated its own limits in the code, not just the report: the corpus is **only as complete
      as what it was given** (printed beside every verdict), **a forged corpus is a different threat
      and is not defended against**, and one object class still has **no answer at all**.
- *** A SCOPE THAT ONLY SPEAKS WHEN IT EXEMPTS SOMETHING HAS AN INVISIBLE FAILURE MODE. *** Same lane:
  the derivation is printed on **every** file, **including the plant ones it did not exempt**. If a
  classifier is silent on the objects it leaves alone, then a classifier that has stopped working and
  one that found nothing to exempt produce **identical output.**
    ➜ Pair it with: **an "unclassified" verdict must gate exactly as a negative one does, and differ
      only in what it SAYS.** *"This is plant"* and *"no property could tell me"* are different facts,
      and collapsing them is how a classifier that stopped running looks like one that found nothing.
- *** A SCOPE THAT SUPPRESSES IS ONE STEP FROM A SCOPE THAT HIDES. *** When exempting a class of object
  from a check, the requirement is never *"stop reporting these"* — it is *** "the checker KNOWS what
  they are and SAYS SO". *** A counted, labelled, non-gating bucket; **a count of zero is a different
  fact from an absent section**; and the exemption must be **derived from a property, never declared
  by a flag** — *a caller assertion is forgotten exactly when it matters.*
    ➜ **Watch for the class that has no derivable property.** A block has a number; a tag table does
      not, so it falls back to a name — **and a name is a convention anything can be renamed into.**
      *An honest "unclassified" beats a rule that turns renaming into a route out of review.*
- *** NOT ALL FAILURES GET INVESTIGATED — PREFER THE ONE THAT INVITES AN ARGUMENT. *** Named
  2026-08-13 and now the stated reason behind several refusals here: *** A `FAIL` INVITES AN ARGUMENT
  AND SOMEBODY LOOKS. A `TIMED-OUT` READS AS "THE CONDITION NEVER OCCURRED" AND CLOSES THE QUESTION. ***
  So a spurious `TIMED-OUT` is worse than a spurious `FAIL`, even though both are wrong and the
  timeout is the more cautious-sounding of the two.
    ➜ **When choosing which way a check fails, ask which verdict a tired reader will act on.** The
      correct-but-ignorable outcome is not safer than the arguable one.
    ➜ Consequence adopted throughout: **refuse rather than mis-compare.** A comparison that cannot be
      made must not be reported as a comparison that came out negative.
- *** A WIDTH ERROR WRAPS; AN ORDER ERROR ANNOUNCES ITSELF. *** Same day, on the same 32-bit path:
  81 duration values in the deliverable set exceed 65 535 ms. A **narrow** mapping does not error —
  *** `75 000 ms` ARRIVES AS `9 464 ms`, EVERY BOUNDARY FIRES EARLY, AND THE RUN RETURNS A CONFIDENT
  WRONG ANSWER. *** A **swapped-word** mapping multiplies by 65 536, the scenario never reaches its
  next boundary, and the run **times out loudly.**
    ➜ **Of two failures in the same field, prefer the loud one and design the quiet one out.** A value
      that does not fit its element must **refuse by name, never modulo.**
    ➜ And it flips a calibration instinct: **calibrate against a slowly-changing counter, not against
      a duration** — *a duration cannot tell a correct transform from a broken one*, because both
      produce a plausible-looking number.
- *** BEFORE BUILDING A SEAM, CHECK WHETHER THE THING ALREADY SHIPS ONE. *** Measured 2026-08-13. A
  test model needed to drive a block's inputs, and the question was framed as *where to insert it so it
  overrides the field read* — a real question, with a real cost, and an owner ruling behind it. Then
  somebody read what actually writes those tags: *** EVERY RUNG ALREADY READ A `Test[]` INJECTION
  ARRAY, WITH `Test[0]` DISCONNECTING EVERY PHYSICAL TERMINAL. *** The model does not override the map;
  **it feeds it** — one position earlier, zero scan latency, no second writer, and the contention
  question *disappears* instead of being answered.
    ➜ *** USING A SEAM A BLOCK SHIPS WITH IS NOT INSTRUMENTATION — IT IS THE BLOCK BEING USED AS
      DESIGNED *** , which is a stronger claim than "leaves every shipped block byte-identical".
    ➜ **The tell was that the ruling optimised a cost rather than removing it.** When a decision is
      *"which of these two prices do we pay"*, spend ten minutes asking whether the price is real.
- *** A DIFF THAT COMPARES BY POSITION CANNOT SEE A SHIFT — AND WILL REPORT ONE AS SIX CHANGES. ***
  Same day: inserting one call at the head of `OB1` made `converter diff` report **six networks
  touched**. That is what comparing by network *number* does to a renumbering, and it is **not evidence
  about logic**. The check that settled it was mechanical and different in kind: the four shipped
  `CALL` statements are **byte-identical and in the same relative order**.
    ➜ **Know what your invariance tool is keyed on before quoting it as proof of invariance.** A
      position-keyed diff is exactly the wrong instrument for a positional change, and it fails
      *loudly* — which is better than the converse, but still not an answer.
- *** A CONTROL THAT DOES NOT CONTROL FOR THE RIGHT THING IS NOT A CONTROL. *** Measured 2026-08-13,
  twice in one day, on the cheapest check this project has. Probing a binary for a string to prove it
  is the fresh build: the first attempt reported every marker **ABSENT**, *including the control*,
  because `strings` does not exist on this machine. The second attempt used a positive control that
  **matched in both ASCII and UTF-16** — so it was landing on a **metadata identifier, not a string
  literal**, and therefore controlled for nothing the probe was actually measuring. Corrected across
  both alignments, the two positive controls landed on **different** alignments — *direct evidence
  that the single-alignment method manufactures absences.*
    ➜ *** A FALSE "STALE BINARY" IS THE SAME CLASS OF ERROR AS A FALSE GREEN *** — it sends someone to
      rebuild and re-approve a binary that was already correct, and on this machine that costs a
      Portal window.
    ➜ **Run the control through the same path as the measurement**, and ask what would make the
      control pass while the measurement is broken. A control that cannot fail is the same defect as a
      test that cannot fail, one level out.
- *** A POST-HOC CORRECTION ONLY FIXES THE COPY IT REACHES. *** Measured 2026-08-13, and it explains a
  contradiction that had looked like two unrelated bugs. A probe corrected its transfer verdict
  **after** the human-readable verdict *sentence* had already been rendered from the un-corrected
  value — so one field read *"NOTHING WAS TRANSFERRED BY CONSTRUCTION"* while the sentence beside it
  read ***"YES — THE SOFTWARE WAS LOADED"***, in the same document.
    ➜ **A value that is fixed up after the fact has as many truths as it has readers.** Push the
      correction to the **input** — the fix here deleted the override, made the destination a
      **required parameter**, and rendered all three fields from one verdict, *so forgetting it fails
      to compile.*
    ➜ **The tell is a report that disagrees with itself.** When two fields derived from one fact
      differ, do not fix the wrong one: **ask which of them was computed later.**
    ➜ Sibling defect, same fix: the verdict had been concluded **from message TEXT** — the absence of
      a phrase — and folder runs and device runs emit *the same words*. **A property of *which call
      was made* cannot be recovered from what the call printed.**
- *** THE TIDY REPRESENTATION RE-CREATED THE FAILURE ONE LAYER DOWN — AND ONLY MEASURING THE CONSUMER
  FOUND IT. *** Same day: asked to model "an image exists, nothing was transferred" as a distinct
  positive state, the lane built it — then **measured what the consumer did with it.** The consumer's
  reader returned false on the shape, **fell through to scraping the log, and named the same 27
  objects.** *** THE HONEST-LOOKING SHAPE RESTORED THE FALSE POSITIVE ONE LAYER DOWN. ***
    ➜ **Choosing a representation is not a matter of taste — run the consumer.** "More honest" is a
      claim about what a reader concludes, and readers have fallbacks.
    ➜ It also overruled *my* stated preference on evidence, which is the outcome to want from a brief:
      **a suggestion is an input, not an instruction.**
- *** A COMPARATOR THAT LEARNS TO EQUATE REPRESENTATIONS STARTS PASSING. *** Measured 2026-08-13, as a
  **deliberate refusal** rather than a defect: a bound comparator treats `T#60S` and `T#1M` as
  **different**, though they are the same interval. It is not a duration parser and must not become
  one — *** TEACHING IT TO EQUATE THEM IS EXACTLY HOW A COMPARATOR STARTS PASSING THINGS. *** The same
  family as *a normalizer that ignores too much is how the `MemoryLayout` hole survived a green*, but
  arrived at from the other end: here the convenience **is** the failure mode.
    ➜ **When you decline a convenience for this reason, write the reason beside it** — otherwise the
      next reader files it as a rough edge and fixes it. Both this and its sibling asymmetry
      (*lax where laxity fails closed, strict where it would fail open*) carry theirs.
    ➜ ⚠️ And a live method correction from the same lane: an audit that enumerated gates by grepping
      `new GateResult("…")` **could not see gates that bind their label to a `const string` first** —
      three did. **Two earlier audits were right by luck of style.** *An enumeration built on one
      syntactic form is a denominator with a blind spot;* enumerate both forms, or count from the
      behaviour instead.
- *** A NOTE THAT PROTECTS SOMETHING MUST BREAK WHEN THE PROTECTION IS BREACHED — MAKE THE REFERENCE
  SELF-FALSIFYING. *** Measured 2026-08-13, and it reversed the obvious fix. A note existed to stop a
  redundant-looking word being deleted from two assertions, because that word is what makes them
  non-vacuous. The tidy answer — *replace the fragile identifier with a stable behaviour phrase* —
  **is actively wrong here**, and the reason generalises:
    ➜ *** THE NOTE'S SUBJECT IS HOW THOSE ASSERTIONS ARE WORDED, NOT WHAT THEY CLAIM — AND A CLAIM IS
      EXACTLY WHAT A REWORDING PRESERVES. *** A behaviour phrase would still point at them after the
      word was deleted, so the note would go on **asserting a protection that no longer exists**: the
      wrong-pointer defect, inside the note written to prevent the tidy.
    ➜ **Identify by the PROPERTY instead:** *"every assertion whose own hashed text contains the word
      X — at present two — …"*. Delete the word and the sentence **stops naming anything and the
      count stops matching**, so it *** BREAKS VISIBLY AT THE MOMENT THE PROTECTION IS BREACHED,
      INSTEAD OF OUTLIVING IT. *** Set-defined rather than count-defined, so anything later acquiring
      the property is covered automatically.
    ➜ General form: **when a reference exists to guard an invariant, ask what happens to the reference
      if the invariant is violated.** If it still resolves, it is a comment. If it dangles, it is a check.
    ➜ And the reason the original identifier was never adequate: **it never encoded the property — it
      just pointed at the things that happened to have it.**
- *** A POSITIONAL REFERENCE IN PROSE GOES STALE SILENTLY, AND OUR OWN ID SCHEME SAYS SO. ***
  §3.2 of the enumeration design rules that an index is never an identifier — *"insert an assertion at
  index 1 and every later index shifts, so a stored reference silently comes to name a different
  assertion"* — and a shape check rejects ordinals in a `Basis`. **But ruling PROSE cited display
  ordinals, and the shape check does not reach prose.** Three such citations were written when a
  clause held two assertions and never re-checked as it grew to six. **The one that mattered was a
  ruling whose entire argument was "assertion X already carries it"** — if the ordinal has moved, the
  justification points at nothing, inside the ruling written to make that assertion testable.
    ➜ **A rule enforced by a mechanical check applies only where the check reaches.** Ask where else
      the same identifier appears — comments, commit messages, prose, briefs — because those are
      exactly the places no shape check runs.
    ➜ **Fix the mechanism, not the three instances.** Name the *behaviour*, which is stable under
      re-decomposition by construction; do not "confirm the ordinals and move on", which leaves a
      defect that recurs on every future round.
- *** WHEN IDENTITY IS CONTENT-DERIVED, REDUNDANCY IS LOAD-BEARING — AND TIDYING IS THE MOST LIKELY
  FUTURE DAMAGE. *** Measured 2026-08-13, as a hazard caught before it fired. One ruling wrote *"the
  **specified** threshold"*; a later ruling made that adjective redundant everywhere. **Deleting it
  would move a hashed sentence, change its ID, and dangle every citation in two independent vector
  sets — to buy nothing.** The edit is small, obviously correct-looking, and reversible only by
  restoring the exact bytes.
    ➜ **In any artifact whose IDs are hashes of its own text, "it reads better" is not free and must
      be PRICED.** State the cost in citations before touching a character.
    ➜ Generalises past hashes: *anywhere a downstream reference is derived from content rather than
      from a stable key, cosmetic edits are breaking changes wearing a harmless costume.*
- *** A SIMULTANEITY CLAIM IS SATISFIED BY A SYNCHRONISED ERROR. *** Measured 2026-08-13. Two
  assertions said the alarm and the inhibit outputs *never disagree* — a genuine invariant, correctly
  decomposed into both directions. **An under-scaled preset asserts BOTH outputs early, and both
  assertions stay true, because the outputs still agree.** The relation held perfectly while the
  behaviour was wrong.
    ➜ *** A RELATIONAL ASSERTION IS STRUCTURALLY BLIND TO ANY ERROR ITS OPERANDS SHARE *** — the same
    shape as *a check that shares its subject's blind spot*, but arrived at from inside a
    correctly-written requirement rather than from a buggy checker. **Nothing about the decomposition
    was wrong; the claim simply does not constrain the thing that moved.**
    ➜ So whenever a clause asserts a relation between signals, **ask separately what pins each
      operand to something outside the pair.** If the answer is nothing, the relation is load-bearing
      for consistency and load-bearing for *nothing else*, and it will read as coverage.
- *** A NARROWING REPAIR: THE EDIT LEAST LIKELY TO BE RE-READ FOR WHAT IT DROPPED. *** Measured
  2026-08-13. A ruling correctly removed a contradiction from a requirement clause — **and silently
  removed a claim along with it.** The clause was titled *"No alarm below threshold"*; the reworded
  text conditioned the claim on a debounced clear, so *** AN UNDER-SCALED PRESET LEFT ALL 25
  ASSERTIONS TRUE *** — the most ordinary commissioning error there is, invisible to the coverage
  gate. **The title survived and the content did not**, which is precisely why four consistency passes
  read it as covered.
    ➜ *** A REWRITE THAT FIXES A CONTRADICTION GETS CHECKED FOR WHETHER THE CONTRADICTION IS GONE,
      NEVER FOR WHAT WENT WITH IT. *** After any narrowing edit, ask what the clause claimed
      **before** and confirm each part still has a home. A heading is not coverage.
    ➜ **It was found by a VECTOR AUTHOR, not by review** — the design says *the vector author is a
      second reader by construction*, and this is that mechanism firing rather than being asserted.
      **An independent consumer of a spec finds what re-reading the spec cannot.**
- *** A REQUIREMENT THAT REFERENCES THE IMPLEMENTATION'S OWN PARAMETER IS VACUOUS. *** The repair
  above only bites because it is written against **the SPECIFIED threshold**, never the block's own
  preset: *** AGAINST ITS OWN PRESET, A `T#45S` BLOCK ASSERTS "AT ITS THRESHOLD" AND PASSES *** — true
  of every implementation, including every wrong one.
    ➜ **Ask of any requirement: could the thing under test satisfy this by defining its own terms?**
      If so it is a tautology in requirement's clothing, and it will read as coverage forever.
- *** A PROHIBITION LIST IS A DENYLIST, AND A DENYLIST HAS TO BE COMPLETE. *** Measured 2026-08-13, on
  the run the whole milestone turns on. The vector author — whose value rests **entirely** on never
  having seen the implementation — was briefed with a list of forbidden artifacts. **It read none of
  them, and was contaminated anyway**, through a document nobody had thought to forbid:
  *** `test-environment-build-plan.md`, THE ORCHESTRATOR'S OWN RUNNING RECORD, WHICH QUOTES THE
  BLOCK'S INTERFACE NAMES AND ONE OF ITS NETWORK COMMENTS. *** The same correction had been made to
  the download fence **that same day**, for the same reason, and was not carried across.
    ➜ *** BRIEF INPUTS AS AN ALLOWLIST: "these four files and nothing else in the repository." ***
      A denylist requires the briefer to have thought of every leak; an allowlist requires only that
      they know what the work needs. **And when the agent wants a fifth file, it stops and asks —
      which is the event you wanted.**
    ➜ **Any document that QUOTES an implementation becomes a contamination channel for everyone who
      must not see it** — including running records, lane reports and status summaries written for an
      entirely different reader. **Mark such documents at the top**, because the person who must not
      read them cannot know from the filename.
    ➜ **And grade the disclosure, not just the leak.** This author disclosed unprompted, put it first,
      and *refused to say which of its vectors corresponded to the divergences* — which kept a fresh
      re-derivation clean. **That is the behaviour to want when a fence fails**, and it is worth more
      than a fence that never gets tested.
    ➜ **What a mechanical check CAN and CANNOT settle here.** Checkable and clean: 1:1 coverage of the
      denominator, no invented citations, spec names used throughout, distinct authorship. **Not
      checkable: whether an individual expectation was derived or nudged.** *When the unfalsifiable
      part is the part that matters, re-derive independently and compare* — the divergence between two
      authors is the only evidence available, and it costs one agent run.
- *** A COMMIT MESSAGE IS A CLAIM, NOT EVIDENCE — INCLUDING OURS. *** Measured 2026-08-13: commit
  `f12bf1a` states *"re-export diffed byte-identical"*. **The re-export never landed.** A stale block
  comment then sat in the controller for a month behind green checks, and the record that would have
  exposed it instead asserted the opposite.
    ➜ **This repo's audit trail is its commit messages**, and they are written in the same breath as
      the work rather than after verifying it. *A claim about an artifact belongs in the message only
      once the artifact has been looked at* — "re-exported" is checkable in one command, and was not
      checked.
    ➜ **Corollary for reading history:** a past message describing a *verification* is the weakest
      evidence in the repository, weaker than the artifact it describes and weaker than the diff.
      When a claim matters, **re-measure — do not cite the commit that says it was measured.**
- *** A TEST THAT COULD NOT EXPRESS SUCCESS, AND SO PUNISHED ITS OWN FIX. *** Measured 2026-08-13:
  four golden-harness guards were staleness checks over **a list of things wrong with the corpus**,
  written as `[Theory]` + `MemberData` — so **every one fails with `No data found` the moment its
  list empties**, which is the state the work exists to reach. Two did exactly that when a commit
  closed the last gap, and the failure looks like a regression rather than an achievement.
    ➜ *** ASK OF EVERY ENUMERATION: IS THIS A POPULATION, OR A LIST OF THINGS WRONG WITH THE
      POPULATION? *** **A population must never be empty** — an empty corpus *is* broken, and that
      is where *empty is not clean* belongs. **A problem list should be trying to become empty**, and
      a guard over one must be vacuously green when it succeeds.
    ➜ **This is the boundary of *empty is not clean*, not an exception to it.** The rule earns its
      keep on *examined-nothing* cases; applied to a defect list it inverts, and punishes exactly the
      work it was meant to protect.
- *** A FIXTURE THAT ASSERTED A NUMBER WHICH WAS A PROPERTY OF THE ALGORITHM, NOT OF THE
  REQUIREMENT. *** Measured 2026-08-13: three wave-set fixtures asserted a **wave count** that had
  been guessed at, and failed — a path graph two-colours correctly, and a blacklist edge between an
  already-isolated pair changes nothing. The fix was not to correct the numbers.
    ➜ *** ASSERT THE INVARIANT, NOT THE COUNT. *** "Do these two slots share a wave set?" is the
      requirement. "Are there three waves?" is a fact about the current colourer — so it **fails on a
      better implementation and passes on one that ignores the blacklist entirely.** A test that is
      wrong in both directions is worse than absent, because it will be *fixed* toward whatever the
      code does.
    ➜ Ask of any numeric expectation: **would this still be the right answer if someone improved the
      algorithm?** If not, the number is the wrong thing to pin.
- *** OUR OWN OUTPUT BECAME THE STANDARD IT WAS BEING JUDGED AGAINST. *** Measured 2026-08-13, and
  it wore **two faces in one defect** — `converter to-xml` omitted the empty `InOut` section that
  every real TIA instance-DB export declares, which cascaded into **26 spurious differences** and
  made *** EVERY INSTANCE DB PERMANENTLY DRIFTED, WITH `drift-check` UNABLE TO SEE THE CONTENT
  UNDERNEATH. ***
    ➜ **A FIXTURE AUTHORED BY READING OUR OWN OUTPUT IS NOT A FIXTURE, IT IS A MIRROR.** Both
      hand-authored instance-DB fixtures declared `Static` alone — *our writer's shape* — so every
      round trip agreed with the writer and **not one of them could have failed.** ➜ *Read a fixture's
      expectations out of the foreign artifact, never out of what we emit.* (Same root as the
      `Modbus_*` names taken from hand-authored fixtures: *the fixtures were evidence about the
      SHAPE, never the NAME.*)
    ➜ *** AND THE DEFECT WAS FILED AS A PROPERTY OF THE ANSWER KEY. *** Both golden baselines
      recorded the three drifted iDBs as *"interface cascade from its FB"* — a deferred re-export,
      i.e. **the reference data's fault**. The real cause sat in a raw test's own entry text as a
      *co-factor of the deferral*. **A defect in our output, written down as an accepted tolerance of
      the corpus, becomes a closed question** — nobody re-opens a difference that already has an
      entry explaining it. It survived a re-export *from TIA* still red and was not re-examined.
      ➜ **When a baseline entry blames the reference data, ask once whether our side produced it.**
        An accepted-tolerance entry is the most durable hiding place a defect has, because it is
        indistinguishable from diligence.
- *** A CONTRADICTION BETWEEN TWO SECTIONS OF OUR OWN METHOD DOCUMENT, FOUND BY *USING* IT RATHER
  THAN BY REVIEWING IT. *** Measured 2026-08-13, on the **first live use** of
  `assertion-enumeration.md`: §1 defines the falsifiability test **existentially per assertion**,
  §2.2 wrote the same test as a **one-sided** tie-break. On the first real clause they gave
  **different denominators — 13 assertions or 14** — and the merged reading left *no assertion
  isolating a whole defect class*, so a vector could have covered it in good faith without ever
  testing it. **Two people had read the document and neither saw it.**
    ➜ **A specification is not validated by being read. It is validated by someone trying to follow
      it and reporting where it stopped being followable.** Prose can be internally inconsistent and
      still read perfectly, because a reader resolves the ambiguity silently and moves on — the
      *user* cannot, because they have to emit one answer.
    ➜ So when a document defines a procedure, **the first execution of it is a test run**, and its
      author should expect findings against the document rather than against the work.
- *** A FENCE THAT LEFT THE AGENT UNABLE TO PRODUCE ITS OWN DELIVERABLE. *** Same day, same lane:
  `assertion-enumerator` is denied `Bash` **deliberately and correctly**, so that it cannot run the
  converter over a block and let the implementation contaminate the spec-side denominator. But an
  assertion ID is a **SHA-256**, and that same fence removes every way to compute one. *** THE AGENT
  THE DESIGN MAKES RESPONSIBLE FOR THE DENOMINATOR CANNOT PRODUCE THE IDENTIFIERS IT IS CITED BY. ***
  It did the right thing — emitted the normalised text and **no** `id:`, rather than fabricating hex
  that `IdDoesNotRecompute` exists to catch — but the artifact is unusable until someone else stamps
  it, and nobody had been assigned that.
    ➜ **When you fence an agent, enumerate its deliverable and check each part is still reachable.**
      The fence is usually right; what is missing is the *hand-off* it implies.
    ➜ **A capability an agent must not have on its INPUTS may still be needed for its OUTPUTS.** Those
      are different questions and a single tool grant answers both at once — which is why the fence
      looked complete.
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
    ➜ *** AND A THIRD SHAPE: THE GUARD IS WIRED IN, BUT NOTHING CAN REACH IT — BECAUSE THE THING IT
      CHECKS NEVER MISBEHAVES. *** Measured 2026-08-13: deleting a wave-colourer's independent
      re-verification left **the entire suite green**, because the colourer never produces a bad
      colouring, so the check had nothing to fire on. It is not decoration and it is not
      disconnected — *it is correct, wired, and unfalsifiable in place.*
      ➜ **Extract it and test it directly** against inputs the producer would never generate. And
        **test the converse in the same breath** — that a *correct* input produces no finding —
        without which every other test would pass against a verifier that reported everything.
      ➜ Distinguish it from the decoration case: ask **"could any input reach this line?"** rather
        than "is this line called?". The second question answers yes and tells you nothing.
    ➜ *** COMMIT THE FIX, THEN MUTATE — NEVER THE OTHER WAY ROUND. *** `git checkout` reverting an
      uncommitted extraction mid-mutation has now bitten twice, and it happens **whenever a mutation
      and a fix live in the same file**. The restore that undoes the mutation undoes the fix with it,
      and the re-run then measures the *old* code while appearing to measure the new.
    ➜ *** AND EXECUTE THE GUARD'S OWN ADVICE. A REFUSAL MESSAGE IS A SECOND ARTIFACT AND IT IS
      UNTESTED. *** Measured the same day: a client's tear-verdict guard was mutation-tested four
      ways and correct every time, and **the recovery it printed was refused by the guard itself** —
      it named the one command the guard blocks in exactly that state. The tests checked the
      *decision*; nobody had read the *sentence* while standing in the situation it describes. Do
      that, out loud, on the device.
- **Inference reported as measurement.** `[M]` means measured here, and a `[M]` that turns out to
  be a hand-authored fixture costs more than the gap it hid.
- **Guessing at hardware, tags or addresses.** Read them, or say they are proposed.
