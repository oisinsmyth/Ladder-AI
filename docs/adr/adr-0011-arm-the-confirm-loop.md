# ADR-0011 — Arm the confirm loop, and fence it to the scratch project

- **Status:** **ACCEPTED — ruled 2026-08-13. NOT YET IMPLEMENTED.** The ruling is recorded here; the
  enacting change to `tools/confirm-roundtrip.ps1` is **tooling, not docs**, and is deliberately not
  made by this ADR. Nothing in the script has been touched. Update this status line when the fence
  lands, and say which mechanism was chosen.
- **Date:** 2026-08-13.
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

## Implementation note for whoever edits the script

***`.ps1` FILES IN THIS REPO ARE ASCII-ONLY AND CRLF.*** Measured 2026-08-12: PowerShell 5.1 reads a
BOM-less `.ps1` as **ANSI**, so a UTF-8 em dash decodes to `”` (U+201D) — ***which the parser treats
as a QUOTE DELIMITER***, and the resulting error points at an unrelated line **a hundred lines away**
from the real one. This project's prose uses em dashes constantly, so a script written in the house
voice is a live hazard. Match `tools/openness-approve-*.ps1`, and leave a comment in the file saying
why, so nobody "improves" the punctuation later.
