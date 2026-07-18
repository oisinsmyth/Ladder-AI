---
name: gen-block-modify-fix
description: The Build-stage skill (docs/15 pipeline skill #9, the S7 targeted-fix path) that fixes ONE named defect in an EXISTING block, touching only the named network(s) and proving every other network is provably unchanged in IR. Use whenever asked to fix, correct, or repair a defect in an existing ladder block — "fix network N", "the block does X wrong", "correct the fault-reset polarity", a named review finding or a fix-wave/B-docket item, "restore REQ-nnn in <block>". This is a SCOPED fix, not a rewrite: it changes the least it can and leaves the rest byte-identical (verified by `converter diff --only`). NOT for writing a NEW block (that's gen-block-new) and NOT for a purpose/interface change or feature addition (that's gen-block-modify-purpose — different intent, different invariance profile). Runs inside the lad-coder sub-agent (CLAUDE.md hard rule 8). Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
  - Write
  - Edit
  - Bash(./src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(src/converter/Converter/bin/Release/net8.0/converter.exe:*)
  - Bash(./src/openness-cli/OpennessCli/bin/Release/net48/openness-cli.exe:*)
  - Bash(src/openness-cli/OpennessCli/bin/Release/net48/openness-cli.exe:*)
---

# /gen-block-modify-fix — the targeted fixer (one named defect → a scoped, invariance-proven fix)

Ladder-AI project. This is docs/15's `gen-block-modify-fix` stage (pipeline skill #9) and the concrete
S7 capability: take **one named defect** in an **existing** block and produce a **fixed block whose only
changed networks are the ones the defect names** — every other network provably identical in IR. It is
the highest-risk capability the project builds (changing working logic), so its whole discipline is
*minimal, scoped, and proven*. Read `CLAUDE.md` in full first — hard rules bind you, and CLAUDE.md's
"Workflow for modifying existing logic (Stage S7+)" is the contract this skill mechanizes.

**You run inside `lad-coder`** (hard rule 8). If reached otherwise, stop — a human-facing agent must
dispatch this to `lad-coder`.

**You are a targeted fixer — not a coder from scratch, not a re-architect, not a reviewer.**

- **Change the least you can.** Fix the named defect in the named network(s); touch nothing else. The
  discipline that makes modifying working logic safe is that the diff is *small and provable* — a fix
  that "improves" untouched networks along the way is a scope breach, not a bonus.
- **A purpose/interface change is not yours.** If the "fix" actually needs a new interface member, a new
  network, or a behavior the register doesn't already ask for, that's `gen-block-modify-purpose` (or a
  gap for the engineer) — stop and route it, don't grow the fix into a redesign.
- **A requirement-vs-convention tension is a stop-and-route, not a guess.** If fixing the REQ as stated
  would breach a convention (or vice versa), flag it for an owner ruling with both sides — don't silently
  pick one. (Real example: a "retentive timer" REQ vs C-406's TONR ban.)
- **Never invent tags/addresses** (hard rule 3), **safety content → stop** (hard rule 2), **never
  self-review** (the fresh-context Check stage does that), **never import into the real project** (scratch
  only, hard rule 5).

## Inputs

- **The fix-request — REQUIRED.** A named defect: the target **block**, the target **network(s)**, what's
  wrong, and the **REQ it restores** (or the convention rule it satisfies). A fix-wave item, a review
  finding (rule ID + location), a B-docket entry, or a `gen/_validation/*/fix-requests.md` task all
  qualify. **Stop condition:** no named defect + target → stop and ask for one; never go hunting for
  things to fix (that's a review, not a fix).
- **The as-built block's IR** — read the **full IR** of the target block; read the target network(s)
  closely and enough of the rest to be sure your change is self-contained (no shared temp/edge-bit a fix
  would disturb elsewhere).
- **The requirements register** (`gen/<project>/requirements.md` or the corpus spec) — for the REQ the
  fix restores; a fix that compiles but doesn't actually satisfy the REQ is a miss.
- **`docs/06-lad-conventions.md`** (read fresh) — the touched networks still answer to conventions (C-126
  grouping, titles/comments, the stricter generated-code bar); **`docs/notes/compile-error-playbook.md`**.

## Method — the modification choreography

**Scope first, edit narrowly, prove the rest unchanged, then compile.** (docs/15 notes this choreography
is shared with `gen-block-modify-purpose`; documented inline here until that skill is built.)

1. **Scope.** From the fix-request, fix the exact network number(s) named. Confirm the defect by reading
   those networks. Everything else in the block is off-limits.
2. **Snapshot the as-built.** Keep the original IR (pre-edit) — you need it for the invariance diff.
3. **Edit only the target network(s)'** readable IR to fix the defect. Keep the fix minimal and
   convention-clean; re-title/comment only the network(s) you changed if the change warrants it.
   **Synthesis rule when adding a permissive:** an added OR-group (or `NOT` of a compound) must be the
   **rail-most / first** operand of an AND chain — appending `AND (X OR Y)` to the *end* of a chain is
   un-synthesizable (`UnsupportedSynthesisConstructException`). Write `(X OR Y) AND <rest>`, not
   `<rest> AND (X OR Y)` — AND is commutative, and a leading branch is the conventional LAD shape anyway.
4. **Re-synthesize (the D-6 reality).** A network in an **already-exported (sidecar-carrying)** block
   can't take an added/changed statement in place — the converter has no scoped-merge path yet
   (`docs/notes/deferred-items.md` D-6). Use the proven **whole-file strip-and-synthesize** workaround:
   strip the file's entire `SIDECAR` section → keep your readable edit → `converter to-xml --synthesize`
   the whole file → import → compile → re-export → `to-ir` to restore real sidecars. This regenerates
   every network's sidecar UIds, which is fine — the invariance check below reads the sidecar-*free*
   form, so untouched networks still prove identical. (A sidecar-less block skips this — just edit +
   `--synthesize`.)
5. **Invariance gate — the hard gate that defines this skill.** Run
   `converter diff <as-built.ir> <fixed.ir> --only <target networks>` (paths first; `--only` takes
   space- or comma-separated numbers, or repeated `--only`, e.g. `--only 1 2` / `--only 1,2`). It **must
   exit 0**: every network *outside* the named set is provably identical in readable IR, and the named
   ones are the only changes. If it reports a change you didn't intend, you touched something you
   shouldn't have — undo it or stop. This is CLAUDE.md's "untouched-network invariance check", mechanized.
   `diff` handles sidecar-carrying *and* sidecar-less inputs (compares the readable form either way).
   **A UDT / `TYPE` change has no networks** — `diff` is block-only, so there's no invariance concept for
   it; verify a UDT edit by member-text inspection + the compile gate instead.
6. **Compile gate** (hard rule 4): `converter preflight` (zero findings) → import to the **scratch**
   project → `openness-cli compile` clean. Playbook first on any failure. Claim the `agent-tasks/README.md`
   Portal queue before import/compile. **An FB with multi-instance timers (a common fix target) compiles
   only after its instance DB exists** — `openness-cli create-instance-db` first if there isn't one.

## Exit

Hand back (per `lad-coder`'s "what you hand back" contract — your summary is not proof):

- **The `converter diff` before/after** of the changed network(s) — plus the **`diff --only` invariance
  result** (exit 0) proving the rest is untouched. This pairing *is* the S7 deliverable.
- A **one-paragraph intent**: which defect, which REQ/rule it restores, why the change is correct.
- **preflight** (zero findings) + **compile** evidence (State, error/warning counts).

Append one telemetry line to `gen/<project>/telemetry.log` (`gen-block-modify-fix`; include blocked/routed
runs) **when the project has one** — a validation corpus (`gen/_validation/*`) has no telemetry log, so
note the run in your report instead of creating one. Then **stop** — the fresh-context Check stage
(re-review of the fix + no-regression) and the final gate (`docs/11-review-workflow.md`) belong to others.
Never treat your own fix as reviewed or approved.

## Calibration

- **Minimal and scoped beats clever.** The measure of a good fix here is a small, provable diff that
  restores the REQ — not elegance. If you find yourself rewriting a network, ask whether this is really a
  modify-*purpose* job.
- **A fix must actually fix.** Re-check the changed network against the REQ it targets; compiling clean is
  necessary, not sufficient (the compile gate never proves behavior).
- **Don't gold-plate the neighbourhood.** Other findings you notice while in the block are *routed* to a
  review/their own fix-request, not fixed drive-by — that would break the invariance the skill exists to
  provide.
- **Telemetry always**, including a run that stopped to route a tension for an owner ruling — that's a
  useful row, not a failure.
