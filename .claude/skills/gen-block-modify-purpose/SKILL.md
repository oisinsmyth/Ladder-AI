---
name: gen-block-modify-purpose
description: 'The Build-stage skill (docs/15 pipeline skill #8, the S7 purpose-change path) that changes what an EXISTING block DOES — per a gate-1-signed architecture decision — by adding/changing interface members and adding/changing/removing networks, while proving every UNTOUCHED network is provably unchanged in IR. Use whenever asked to repurpose or extend an existing block''s function — "change the DOL block into a VSD", "make this motor block variable-speed", "add reversing to this starter", "repurpose <block> to also do X", or a tier-(c) modify-candidate item from an architecture manifest. This is a SCOPED purpose change (touch only what the manifest names; the rest stays byte-identical, verified by `converter diff --only`), not a rewrite. NOT for repairing a scoped defect against an existing REQ (that''s gen-block-modify-fix) and NOT for writing a brand-new block (that''s gen-block-new). Runs inside the lad-coder sub-agent (CLAUDE.md hard rule 8). Ladder-AI project.'
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

# /gen-block-modify-purpose — the authorized re-purposer (change what a block does, still scoped and proven)

Ladder-AI project. This is docs/15's `gen-block-modify-purpose` stage (pipeline skill #8): implement a
**gate-1-signed architecture decision that changes an existing block's purpose** — new/changed interface
members, new/changed/removed networks — **touching only what the manifest names and proving the rest
identical in IR**. It is highest-risk (changing working logic at a larger scope than a fix), so its whole
discipline is *authorized, scoped, and proven*. Read `CLAUDE.md` in full first — hard rules bind you, and
CLAUDE.md's "Workflow for modifying existing logic (Stage S7+)" is the contract this mechanizes.

**You run inside `lad-coder`** (hard rule 8). If reached otherwise, stop — a human-facing agent must
dispatch this to `lad-coder`.

**You are an authorized re-purposer — not a coder from scratch, not a defect fixer, not a reviewer.**

- **The manifest is the authorization and the boundary.** A purpose change is a gate-1 decision
  (`gen-architecture`, tier-(c)): what changes (interface members, which networks) is decided and signed
  *before* you touch the block. Implement exactly that. Changing more than the manifest names — even to
  "finish the thought" — is a scope breach; changing less leaves the purpose half-done.
- **Still scoped, even though it's bigger than a fix.** The value of doing this as a *modification* rather
  than a rewrite is that the untouched networks (the shared skeleton the block keeps) **prove identical**.
  If the manifest's change touches nearly everything, that's a signal it's really a **new block** —
  `gen-block-new` — not a purpose change; say so.
- **A defect you notice is not yours to fix here.** Route it to `gen-block-modify-fix` / a review; don't
  fold an unrelated repair into the purpose change (it breaks the invariance the skill provides).
- **Never invent tags/addresses** (hard rule 3), **safety content → stop** (hard rule 2), **never
  self-review**, **never import into the real project** (scratch only, hard rule 5).

## Inputs

- **The tier-(c) architecture item — gate-1 SIGNED. REQUIRED.** The manifest item that authorizes the
  purpose change: the target **block**, the **interface members** to add/change, the **networks** to
  add/change/remove, and the **REQ(s)** the new purpose satisfies (with C-115 handshake conformance for any
  new interface). A purpose change **needs gate 1** — it changes the block's function, unlike a fix.
  **Stop conditions:** no signed tier-(c) item → stop and say gate 1 (`gen-architecture`) must sign it
  first; if the change is really a scoped defect repair → route to `gen-block-modify-fix`; if it touches
  nearly the whole block → route to `gen-block-new`.
- **The as-built block's IR** — full IR; know which networks the manifest keeps (they must prove invariant)
  vs changes.
- **The requirements register** — the new/changed REQs the purpose delivers; **`docs/06`** (read fresh —
  esp. C-115 handshake vocabulary for the new interface, C-118/C-125 if the new purpose is a sequence,
  C-126, the stricter bar); **`docs/notes/compile-error-playbook.md`**.

## Method — follow the shared modification choreography

The mechanics — scope → snapshot the as-built → edit only the named network(s)/member(s) → D-6 whole-file
re-synthesize → the `converter diff --only` **invariance gate** → compile gate — live in
**`docs/notes/modification-choreography.md`** (shared with `gen-block-modify-fix`; it carries the tooling
notes: the `--only` forms, sidecar-less diffing, the UDT/`TYPE` fallback, the FB-with-timers instance-DB
rule, and the compound-operand-must-lead synthesis rule). Follow it. **For a purpose change specifically:**

- **Interface change is in scope** (unlike a fix): add/change exactly the interface members the manifest
  names — the new function's inputs/outputs/settings (e.g. a VSD's speed reference, ramp, control word,
  speed feedback) — following **C-115 handshake vocabulary** (take member names from the site pattern, not
  doc 06's illustrative ones). `converter diff`'s **`HEADER changed`** line surfaces the interface delta;
  that's expected here. The as-built's other members stay untouched.
  🔴 **So this skill's invariance run needs `--allow-header` (2026-08-14) — and that flag is a DECLARATION,
  not a formality.** Until then an unclaimed header change printed `HEADER changed: interface` and then
  `INVARIANCE OK: all changes confined to --only {N}`, two lines apart, and **exited 0** — so retyping a
  member `Bool` → `Int` with no network touched passed the gate whose whole job is *"prove the rest is
  identical"*, and that is precisely the change that compiles, imports and misbehaves on the controller.
  An unclaimed header change (or a block rename) is now an **invariance violation, exit 1**.
  `gen-block-modify-fix` must **never** pass it: a fix that needs an interface member is a purpose change
  and routes here instead.
- **Networks may be added and removed**, not only edited — per the manifest (add the new function's control
  networks; retire networks the old purpose made and the new one doesn't need, e.g. a DOL run coil replaced
  by VSD speed control). Added/removed networks appear in `diff` as `Added`/`Removed` and belong in the
  `--only` changed set; **the untouched skeleton networks (start/stop, permissives, faults, hours, alarms)
  still prove identical** — that invariance is the whole point.
- **The new networks + interface answer to conventions and must deliver the new REQ(s)** — C-115/C-126, the
  stricter bar; a change that compiles but doesn't actually realize the new purpose is a miss.

## Exit

Hand back (per `lad-coder`'s contract — your summary is not proof):

- **The `converter diff`** showing the changed/added/removed networks **and the interface delta**, plus the
  **`diff --only … --allow-header` invariance result (exit 0)** proving the kept skeleton is untouched —
  that pairing is the S7 deliverable. **Quote the interface delta explicitly in the hand-back**: with
  `--allow-header` the gate stops arguing about it, so the reader is the only remaining check on whether
  the members that changed are the ones the manifest named.
- A **one-paragraph intent**: what purpose change, which REQ(s) it delivers, why it's correct.
- **preflight** (zero findings) + **compile** evidence: `sanity-check`'s `INCONSISTENT: 0` on **both**
  the `BLOCKS:` and `TYPES:` lines, plus the **error count**. *(Corrected 2026-08-13: this read
  "State, error/warning counts". **`State` is not a verdict** — on a project carrying a permanent
  hardware warning it is non-Success on a perfectly clean block, which is why `compile` stopped
  keying on it on 2026-08-12. Key on errors; report warnings without gating on them. And a bare
  whole-device compile is not the gate at all — hard rule 4 / FI-52.)*

Append a `gen/<project>/telemetry.log` line (`gen-block-modify-purpose`) when the project has one (a
validation corpus has none — note the run in your report instead). Then **stop** — the fresh-context Check
stage and final gate (`docs/11-review-workflow.md`) belong to others. Never treat your own change as
reviewed or approved.

## Calibration

- **Authorized and scoped beats complete.** Implement the manifest's change and prove the rest invariant —
  don't improve the block beyond what gate 1 signed.
- **If it's really a rewrite, it's `gen-block-new`; if it's really a defect, it's `gen-block-modify-fix`.**
  Route rather than stretch this skill past its shape.
- **The new interface follows the site's real vocabulary** (C-115) — a VSD's members match the site's VSD
  FB, not invented names; grep before you name.
- **Telemetry always**, including a run that stopped to route (wrong-skill, or an unsigned manifest).
