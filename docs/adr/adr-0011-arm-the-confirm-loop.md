# ADR-0011 — Arm the confirm loop, and fence it to the scratch project

- **Status:** **ACCEPTED AND IMPLEMENTED — 2026-08-13 (`54b032a`).** `tools/confirm-roundtrip.ps1` is
  armed and fenced. The mechanism is recorded under *The fence as built*; four things this ADR left
  under-specified were decided during implementation and are carried under *What the implementation
  had to decide*. **Both items that awaited the owner were CONFIRMED on 2026-08-13** — the fence
  guards **`-Arm` only**, and junctions are **refused rather than half-resolved**. One place where
  the implementation **narrowed** this ADR's literal wording is called out explicitly rather than
  absorbed, and is now confirmed as the intended reading. The script itself is another lane's file
  and is not edited from here. ***NOTHING IN THIS ADR IS NOW AWAITING A DECISION.***
- **Date:** ruled 2026-08-13; implemented 2026-08-13.
- **Relates to:** **ADR-0010** (no IR the AI cannot change — this is the gate that decision hands
  capability widening to) · **ADR-0009** (test-rig write access — same shape of ruling: *the gate is
  the TARGET*) · CLAUDE.md **hard rule 5** (the real project is human-gated) · **hard rule 4** (the
  compile gate) · `converter compare` (the loop's judgement half) · `converter drift-check` (the
  weaker check this is strictly stronger than) · `docs/notes/test-environment-build-plan.md` (where
  the loop's design and its two costs were worked out).

## Decision

**Two halves, and neither works without the other:**

1. ***ARM IT.*** `tools/confirm-roundtrip.ps1` is built and **dry-run only**. It is the strongest
   check this project has, and leaving it disarmed keeps a blind spot the project has already paid
   for. It gets to actually run.
2. ***FENCE IT.*** ***It must REFUSE any project path that is not the scratch project.*** Arming a
   mutating loop without that fence means arming a path into the real project, and this ruling
   declines to do one without the other.

## Context

### Why arm it — it is strictly stronger than what we have, and the gap is measured

The loop runs **export → to-ir → to-xml → import → compile → export** and calls
`converter compare` on the first and last exports.

***It is strictly stronger than `drift-check`, which never leaves the PC: this pair has been THROUGH
TIA, so it also catches what TIA does on import and on compile.*** That is not a theoretical
advantage — **it is the check that would have caught the `MemoryLayout` hole that `drift-check`
called a MATCH**: an IR-authored DB imports Optimized with no error at import, no error at compile and
no drift-check finding, and the block goes silently invisible to classic S7comm behind three green
checks.

**So the blind spot is bought and paid for.** A check that exists, is built, is tested, and is never
run is worth exactly what it costs to maintain and nothing more. And under **ADR-0010** this loop is
the *named gate* for converter capability widening — an unarmed gate means widening decisions revert
to judgement, which ADR-0010 explicitly declines to rely on.

### Why fence it — the loop is the one check in this family that MUTATES

`drift-check`, `compare`, `preflight` and `review` are all read-only and can be run against anything,
constantly. **The confirm loop is not:**

- ***IT WRITES.*** The import mutates the project, and we know for certain it is not a no-op —
  the block layout flips underneath it. So it runs against a **scratch** project, or something must
  be restored afterwards.
- **It costs minutes per block and needs Portal exclusively.** A gate for capability changes and
  pre-promotion, never something an agent runs per edit.

CLAUDE.md hard rule 5 puts the promotion into the real project behind the engineer. **A mutating loop
that can be pointed at the real project is a way around that gate that nobody decided to build** —
and it would be reached by a mistyped path, not by a decision.

## The fence — what "refuses" must mean

Stated as requirements, because the implementation is somebody else's and the failure modes here are
well known to this project:

1. ***ALLOWLIST, NEVER A DENYLIST.*** The permitted scratch project path(s) are named explicitly and
   everything else is refused. A denylist of real projects is wrong twice over: it fails open on a
   project nobody thought to list, and it grows silently stale.
2. ***THE REFUSAL IS AN EXIT CODE, NOT A WARNING.*** "A warning is not a gate" — if a check detects
   something and only warns, it gets skimmed. Fail closed on the path that reaches production.
3. ***REFUSE BEFORE PORTAL IS CONTACTED.*** The precedent is `openness-cli`'s `--yes` gates: without
   the gate satisfied, the plan prints and **Portal is never attached**. A fence evaluated after the
   project is open has already done the thing it was meant to prevent.
4. ***AN UNRESOLVABLE, EMPTY OR AMBIGUOUS PATH IS A REFUSAL, NOT A PASS.*** Empty is not clean
   (FI-44). If the fence cannot positively identify the target as the scratch project, it has not
   verified anything.
5. ***COMPARE THE RESOLVED, CANONICAL PATH*** — not the string as typed. A relative path, a different
   casing, a `..`, a junction or a symlink must not walk around the fence.
6. ***NO OVERRIDE FLAG.*** No `--force`, no `--allow-any-project`, no environment variable. The
   precedent is `download-plan`, which refuses `--yes` and `--force` **by name**. If the real project
   ever legitimately needs this loop, that is a decision, made once, by the owner — not a flag.

**Where the allowlist lives is the implementer's call**, provided it satisfies 1–6 and is visible in
the repo rather than in someone's shell profile.

## The fence as built — 2026-08-13, `54b032a`

| | |
|---|---|
| **Allowlist file** | `tools/confirm-roundtrip.allowlist` — in the repo, not in a shell profile |
| **Entry forms** | absolute paths, **or `repo:`-prefixed** |
| **Scope** | **`-Arm` only** — see decision 1 below |
| **Refusal** | **exit 4** |
| **Position** | first thing after the banner — **before the binary checks, before any directory is created, before stage 1** |
| **Override** | `-IsScratchProject` is **refused by name** (the `download-plan` shape) |

**`repo:`-prefixed entries are the detail worth keeping.** A committed allowlist of absolute paths is
correct on exactly one machine and in exactly one checkout; `repo:` makes the file **portable across
worktrees** instead of silently wrong in the second one. Given that agents on this project routinely
work in `.claude/worktrees/`, an allowlist that only resolved in the main checkout would have failed
closed everywhere else — safe, but useless enough that someone would have edited the fence.

### Why an allowlist, restated as the reason rather than the preference

Requirement 1 gave two abstract arguments. The implementation supplied the concrete one, and it is
much stronger:

> *** THIS MACHINE CARRIES ROUGHLY NINETEEN REAL PRODUCTION `.ap20` PROJECTS IN FOLDERS BESIDE THE
> SCRATCH ONES. ***

A denylist here is not merely weaker — it is **indefensible**. It would have to be complete, and it
would stop being complete **the next time a job folder arrived**, silently, with no event to notice.

### How requirement 3 was proved, which is stronger than the usual

Not "we read the code and the check looks early". The script's **only** route to Portal is
`Start-Process $OpennessCliPath`; the tests hand it a **stub that appends to a sentinel file** and
then assert the sentinel **does not exist** — a direct observation that no process was ever launched.

*** AND THE INSTRUMENT IS CONTROLLED, WHICH IS THE HALF THAT MAKES IT EVIDENCE: one permitted case
asserts the sentinel IS written. *** Without that, "the sentinel is absent" would also be what a
broken stub, a wrong path or a test that never ran looks like — the same *empty is not clean* trap
this project keeps meeting. Disabling the fence turns **9 of 11** red.

## What the implementation had to decide — four things this ADR left under-specified

Recorded with the decision taken, because an ADR that does not say what its gaps were is an ADR
someone will re-open by accident.

**1. Scope: the whole script, or `-Arm` only? — CHOSEN: `-Arm` ONLY. ✅ *** CONFIRMED BY THE OWNER,
2026-08-13. *** The fence guards `-Arm` only; a dry run stays unfenced and available.**
*** THIS NARROWS THIS ADR'S LITERAL WORDING, AND THAT IS FLAGGED RATHER THAN ABSORBED. *** The
Decision section says the script "must REFUSE any project path that is not the scratch project",
unqualified — but every argument offered for it is about the **mutating** half. A dry run writes
nothing, contacts nothing and is exactly the reconnaissance someone needs before arming, so fencing
it buys no safety and removes a use. **Requirement 3 is satisfied *more* strongly this way**, not
less: the unfenced path is the one that provably cannot reach Portal at all. If the owner intended
the broader reading it is a one-line change.

**2. *** A BARE PROJECT NAME IS NOW UNUSABLE WITH `-Arm` — A BREAKING CHANGE THAT FALLS OUT OF THE
RULING RATHER THAN BEING STATED BY IT. *** Requirements 4 and 5 force it: a bare name cannot be
resolved to a canonical path with confidence, so it cannot be matched against an allowlist. But this
ADR never says the armed interface changes, and **the script's own examples used a bare name**.
Recorded here as a consequence of the ruling, so that whoever meets the refusal finds the reason in
the decision record rather than concluding the script broke.

**3. Junctions: requirement 5 names them, and PowerShell 5.1 cannot resolve them. — CHOSEN: DETECT
AND REFUSE. ✅ *** CONFIRMED BY THE OWNER, 2026-08-13 — refused rather than half-resolved. *** Requirement 5 assumed resolution was available; on this
toolchain it is not. Half-resolving would produce a path that is *sometimes* canonical, which is the
worst of the three options — a fence that is correct except when it isn't. Refusing is strictly safe
and preserves the requirement's *intent* (a junction cannot walk around the fence) while abandoning
its *mechanism* (comparing a resolved path). **Consequence, stated because it is a real cost: a
scratch project reached through a junction is refused until its real path is allowlisted.**

**4. A bare name can resolve to a DIRECTORY, and the fence explained itself wrongly.** `-Project
GenProject1` hits the project **folder**, which exists — so an existence check alone passes. *** THE
FENCE REFUSED ANYWAY, WHICH IS FAIL-CLOSED WORKING CORRECTLY, BUT IT PRINTED THE WRONG REASON. ***
That is worth more than a cosmetic note: *** A GUARD THAT EXPLAINS ITSELF WRONGLY IS HOW SOMEONE
CONCLUDES IT IS BROKEN AND GOES LOOKING FOR A WAY AROUND IT *** — the failure mode is social, not
technical, and it ends with the fence disabled by someone who thought they were fixing a bug. Now
requires a **leaf file with a `.apNN` extension** and **names which of three cases it hit**.

## Sequencing — one thing must land first

***THE NORMALIZER / `MemoryLayout` FIX LANDS BEFORE THE LOOP IS RELIED ON.*** Building or trusting the
loop while `compare` is blind to layout gives ***a gate that passes when it should fail, which is
worse than having no gate at all*** — the loop's whole claim is that it catches what `drift-check`
misses, and layout is the worked example of what `drift-check` misses. Arming may proceed in parallel;
*relying on a green* may not.

The same applies to the endpoint-ordering blindness ruled fixable on 2026-08-12 (`Normalizer.Strip`
sorting a `<Wire>`'s own endpoints destroys the only signal carrying port direction). Whoever takes it
must re-read the 2026-07-14 fan-out measurement first — the sort was added on evidence, and the fix
must not discard what that evidence was protecting.

## Options considered

**A. Leave it dry-run only.** Zero risk of touching the real project, zero value. Rejected: the blind
spot it leaves is one the project has already been bitten by, and ADR-0010 has since made this loop a
named gate rather than a nice-to-have.

**B. Arm it unfenced and rely on care.** Rejected. The failure is a mistyped path under time pressure,
and the mitigation would be an agent's attention — which is exactly what a gate exists to replace.

**C. Arm it, fenced to scratch, no override.** **Chosen.** Full value on the target it was designed
for, and the path to the real project is closed by construction rather than by discipline.

**D. Arm it with a confirmation prompt for non-scratch projects.** Rejected: a prompt is a warning
with extra steps, and an unattended run either hangs on it or is given a flag to skip it.

## Consequences

- **ADR-0010's gate becomes real.** Capability widenings can be *proved* faithful rather than argued.
- **The real project stays unreachable by this tool**, which is the same fence shape as ADR-0009 —
  the gate is the **target**, not the kind of operation.
- **The scratch project will accumulate the loop's mutations**, which is what it is for. Restores are
  a scratch-project concern, not a fence concern.
- **Revisit trigger:** if the loop is ever genuinely needed against a project outside the allowlist —
  a live-job verification, say — that is an owner decision that adds a path to the allowlist, with the
  restore point named. It is not a flag, and it is not this ADR being relaxed.

### What the implementation confirmed, extended, and narrowed — 2026-08-13

*** ONE PLACE WAS NARROWED AND ONE MECHANISM WAS SUBSTITUTED; EVERYTHING ELSE WAS CONFIRMED OR
STRENGTHENED. *** Recorded in these three buckets so a later reader can tell which parts of this ADR
are still load-bearing as written.

**Narrowed — ✅ confirmed by the owner 2026-08-13 as the intended reading:**
- **Scope.** The Decision says "any project path", unqualified; the fence applies to **`-Arm` only**
  (decision 1). Consistent with every *reason* this ADR gives, inconsistent with its *words* — and
  the reasons were what was meant. **This ADR's Decision section should be read as scoped to the
  mutating path.**

**Mechanism substituted, effect preserved — ✅ confirmed by the owner 2026-08-13:**
- **Requirement 5** asked for a comparison of the **resolved canonical path** and named junctions as
  something that must not walk around the fence. PowerShell 5.1 cannot resolve a junction, so the
  build **detects and refuses** one instead (decision 3). The requirement's intent holds — a junction
  cannot get past — but its stated mechanism does not exist on this toolchain, and a *legitimate*
  scratch project behind a junction is now refused. **This ADR's requirement 5 should be read as
  "must not walk around the fence", not as "must be resolved".** The owner confirmed the refusal,
  including its cost: **a scratch project behind a junction needs its real path allowlisted.**

**Confirmed and strengthened:**
- **Requirement 1** (allowlist) — the abstract argument was right and the concrete one is far
  stronger: ~19 REAL PRODUCTION `.ap20` PROJECTS sit beside the scratch ones on this machine.
- **Requirement 2** (exit code, not a warning) — exit **4**.
- **Requirement 3** (refuse before Portal) — proved by **direct observation** that no process was
  launched, with a controlled positive case, rather than by inspection.
- **Requirement 4** (unresolvable/empty/ambiguous is a refusal) — held, and decision 4 found a case
  this ADR had not imagined: a bare name resolving to a **directory**, where fail-closed worked but
  the *explanation* was wrong.
- **Requirement 6** (no override flag) — went further than asked: `-IsScratchProject` is refused
  **by name**, so a recorded invocation carrying an override fails loudly rather than silently
  meaning something else.

**Nothing in the implementation contradicted this ADR's intent.** The two deviations above are a
narrowing and a substitution, both argued, both recorded, and one of each is the owner's to confirm.

## Implementation note for whoever edits the script

***`.ps1` FILES IN THIS REPO ARE ASCII-ONLY AND CRLF.*** Measured 2026-08-12: PowerShell 5.1 reads a
BOM-less `.ps1` as **ANSI**, so a UTF-8 em dash decodes to `”` (U+201D) — ***which the parser treats
as a QUOTE DELIMITER***, and the resulting error points at an unrelated line **a hundred lines away**
from the real one. This project's prose uses em dashes constantly, so a script written in the house
voice is a live hazard. Match `tools/openness-approve-*.ps1`, and leave a comment in the file saying
why, so nobody "improves" the punctuation later.

✅ **Honoured in the build (2026-08-13): all three files are verified ASCII-only and CRLF *byte-wise*,
with the reason recorded in their own headers** — so the constraint travels with the files rather than
living only here, which is what stops the next editor reintroducing it. Verified by inspection of the
bytes, not by "it looks fine in the editor" — a UTF-8 em dash looks fine in every editor, which is the
entire problem.
