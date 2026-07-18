# The modification choreography (S7 — shared by the two modify skills)

Both S7 modify skills — `gen-block-modify-fix` (repair a scoped defect) and `gen-block-modify-purpose`
(change a block's purpose per a gate-1 architecture decision) — share this inner loop: **change only the
named network(s)/member(s), prove the rest identical in IR, compile clean.** Each skill's `SKILL.md` adds
its own role framing and what it's allowed to change; this doc is the mechanics both follow. (Split out of
the two skills 2026-07-18 when the second modify skill was built — docs/15's own "documented inline until a
reference is worth splitting out" point.)

## The loop

1. **Scope.** Change only the network number(s) — and, for a purpose change, the interface member(s) — the
   request/manifest names. Confirm by reading them. Everything else in the block is off-limits.
2. **Snapshot the as-built.** Keep the original IR (pre-edit); you need it for the invariance diff.
3. **Edit only the named** readable IR. Keep it minimal and convention-clean; re-title/comment only what you
   changed. Two synthesis rules the synthesizer enforces:
   - **At most one compound (OR-group) per series chain, and it must lead.** Write `(X OR Y) AND <rest>`,
     never `<rest> AND (X OR Y)` (appending a compound is un-synthesizable). And a chain that needs **two**
     parallel OR-conditions — `(A OR B) AND (C OR D)`, an ordinary two-branch ladder shape — can't be one
     synthesized chain: hoist one into a **named helper bit** (`H := A OR B`) and use `H` in the chain.
   - **Statement kind-ordering within a network:** statements parse/emit in kind order (timers → coils →
     moves → arithmetic → calls). Write them in that order or the parser errors.
4. **Re-synthesize (the D-6 reality) — and its hard limit.** A network in an already-exported
   (sidecar-carrying) block can't take an added/changed statement in place (`docs/notes/deferred-items.md`
   D-6). The workaround is the whole-file strip-and-synthesize: strip the file's entire `SIDECAR` → keep
   your readable edit → `converter to-xml --synthesize` → import → compile → re-export → `to-ir`.
   - **PRE-CHECK first — the whole-file path requires the ENTIRE block to be within the synthesizable
     subset.** A realistic as-built equipment FB routinely is **not**: TONR/TOF timers, array-index local
     members, Real tag-vs-tag comparisons all fail synthesis today (`docs/notes/converter-synthesis-gaps.md`),
     and this bites even on **unchanged** networks (the whole-file re-synthesis has to reproduce them too).
     Check `converter preflight`'s `[convert]` line up front; if the block hits any of those, the whole-file
     path **cannot compile** — report it as the converter gap it is (the **D-6 scoped merge**, which keeps
     unchanged networks' real sidecars and synthesizes only the changed ones, is the fix), don't force it.
   - This regenerates every network's sidecar UIds, which is fine for the *invariance check* (it reads the
     sidecar-*free* form, so untouched networks still prove identical) — but the *compile gate* still needs
     every network to synthesize. (A block that is entirely within the synthesizable subset — a generated
     block, or a sidecar-less one — skips all this: just edit + `--synthesize`.)
5. **Invariance gate — the hard gate.** `converter diff <as-built.ir> <modified.ir> --only <changed nets>`
   (paths first) must **exit 0**: every network *outside* the named set is provably identical in readable IR.
   `--only` takes space- or comma-separated numbers or repeated flags (`--only 1 2` / `--only 1,2` /
   `--only 1 --only 2`). `diff` handles sidecar-carrying **and** sidecar-less inputs. A **UDT / `TYPE` change
   has no networks** — `diff` is block-only, so verify a UDT edit by member-text inspection + the compile
   gate instead. A `HEADER changed` line surfaces an interface/title/comment delta (expected for a purpose
   change; not for a fix). If diff reports a change you didn't intend, you touched something you shouldn't —
   undo it or stop. This is CLAUDE.md's "untouched-network invariance check", mechanized.
6. **Compile gate** (hard rule 4): `converter preflight` (zero findings) → import to the **scratch** project
   → `openness-cli compile` clean. An **FB with multi-instance timers compiles only after its instance DB
   exists** — `openness-cli create-instance-db` first if there isn't one. Playbook first on any failure;
   claim the `agent-tasks/README.md` Portal queue before import/compile.

## Exit (both skills)

Hand back: the `converter diff` before/after of the changed network(s) (+ the interface delta for a purpose
change) **plus the `diff --only` invariance result (exit 0)** — that pairing *is* the S7 deliverable — a
one-paragraph intent, and preflight + compile evidence. Append a `gen/<project>/telemetry.log` line when the
project has one (a validation corpus, `gen/_validation/*`, has none — note the run in your report instead of
creating one). Then **stop** — the fresh-context Check stage and the final gate (`docs/11-review-workflow.md`)
belong to others; never self-review.

## What differs between the two skills

- **`gen-block-modify-fix`** — repairs a scoped defect against an *existing* REQ; **no interface change**,
  edits existing network(s) only, **no gate-1** (the fix-request is the authorization). Stop-and-route a
  requirement-vs-convention tension rather than guessing.
- **`gen-block-modify-purpose`** — implements a **gate-1-signed** tier-(c) architecture decision that changes
  the block's *function*; **interface change is in scope** (add/change members per the manifest, C-115
  handshake vocabulary); networks may be **added/removed**, not only edited — added/removed networks land in
  the `--only` changed set, while the untouched skeleton still proves identical (that's the value).
