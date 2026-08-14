---
name: gen-block-modify-fix
description: 'The Build-stage skill (docs/15 pipeline skill #9, the S7 targeted-fix path) that fixes ONE named defect in an EXISTING block, touching only the named network(s) and proving every other network is provably unchanged in IR. Use whenever asked to fix, correct, or repair a defect in an existing ladder block — "fix network N", "the block does X wrong", "correct the fault-reset polarity", a named review finding or a fix-wave/B-docket item, "restore REQ-nnn in <block>". This is a SCOPED fix, not a rewrite: it changes the least it can and leaves the rest byte-identical (verified by `converter diff --only`). NOT for writing a NEW block (that''s gen-block-new) and NOT for a purpose/interface change or feature addition (that''s gen-block-modify-purpose — different intent, different invariance profile). Runs inside the lad-coder sub-agent (CLAUDE.md hard rule 8). Ladder-AI project.'
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

## Method — follow the shared modification choreography

The mechanics — scope → snapshot the as-built → edit only the named network(s) → D-6 whole-file
re-synthesize → the `converter diff --only` **invariance gate** → compile gate — live in
**`docs/notes/modification-choreography.md`** (shared with `gen-block-modify-purpose` so the two skills
can't drift; it also carries the tooling notes: the `--only` space/comma/repeated forms, sidecar-less
diffing, the UDT/`TYPE` no-network-invariance fallback, the FB-with-timers instance-DB rule, and the
compound-operand-must-lead synthesis rule). Follow it. **For a fix specifically:**

- **Scope = the network(s) the fix-request names — a defect repair, not an interface change.** If the
  "fix" actually needs a new interface member or a new/removed network, it's a *purpose* change → stop
  and route to `gen-block-modify-purpose`; don't grow the fix into a redesign.
  🔴 **The `--only` gate now ENFORCES that (2026-08-14): a header change you did not declare is an
  invariance violation, exit 1.** Until then it printed `HEADER changed: interface` and then
  `INVARIANCE OK: all changes confined to --only {N}` and exited 0, so a member retyped `Bool` → `Int`
  passed a gate whose whole job is *"prove the rest is identical"*. **`--allow-header` exists and is
  `gen-block-modify-purpose`'s, not yours** — if you reach for it, you are on the wrong path and the
  answer is to route, not to declare.
  ✅ **BUT REPAIRING A STALE BLOCK COMMENT IS YOURS, AND IT NO LONGER GATES (narrowed 2026-08-14, on the
  gate's first contact with real work).** A fix-wave run widened a Modbus area, repaired two block
  comments that were *already false* — one said the area covered "8 words" when it covered 35 — and hit
  `INVARIANCE VIOLATION`. It rightly refused `--allow-header`, which left a **documentation-only repair
  with no clean path under either modify skill**, while leaving false comments in place was not a
  neutral option either. **`--only` asks one question: did anything change outside the named networks
  that could alter WHAT THE PLC DOES?** An interface member answers yes; a block title or a rename
  answers yes (both are identity, and TIA's import matches by name). ***A block comment cannot.*** So a
  **comment-only** header change now passes, and prints `HEADER COMMENT CHANGED (does not gate)` on its
  own line — **quote that line in your hand-back**: non-gating is not invisible, and it is equally the
  place a correct comment gets silently discarded. **Everything else in the header still gates**, and a
  comment edit is never cover for one: change a member *and* a comment and you get
  `an INTERFACE member changed (the block comment also changed; on its own that would not gate)`, exit 1.
- **The REQ you restore already exists** — re-check the fixed network against it; a fix that compiles but
  doesn't restore the REQ is a miss (the compile gate never proves behavior).
- **A requirement-vs-convention tension is a stop-and-route, not a guess** (e.g. a "retentive timer" REQ vs
  C-406's TONR ban) — flag both sides for an owner ruling rather than silently picking one.

## Exit

Hand back (per `lad-coder`'s "what you hand back" contract — your summary is not proof):

- **The `converter diff` before/after** of the changed network(s) — plus the **`diff --only` invariance
  result** (exit 0) proving the rest is untouched. This pairing *is* the S7 deliverable.
- A **one-paragraph intent**: which defect, which REQ/rule it restores, why the change is correct.
- **preflight** (zero findings) + **compile** evidence: `sanity-check`'s `INCONSISTENT: 0` on **both**
  the `BLOCKS:` and `TYPES:` lines, plus the **error count**. *(Corrected 2026-08-13: this read
  "State, error/warning counts". **`State` is not a verdict** — on a project carrying a permanent
  hardware warning it is non-Success on a perfectly clean block, which is why `compile` stopped
  keying on it on 2026-08-12. Key on errors; report warnings without gating on them. And a bare
  whole-device compile is not the gate at all — hard rule 4 / FI-52.)*

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
