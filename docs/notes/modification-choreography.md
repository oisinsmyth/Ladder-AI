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
   changed. **Synthesis rule:** an added OR-group (or `NOT` of a compound) must be the **rail-most / first**
   operand of an AND chain — `(X OR Y) AND <rest>`, never `<rest> AND (X OR Y)` (appending a compound is
   un-synthesizable, `UnsupportedSynthesisConstructException`; AND is commutative, so lead with it — the
   conventional LAD shape). Bites most when adding a permissive to an existing chain.
4. **Re-synthesize (the D-6 reality).** A network in an already-exported (sidecar-carrying) block can't take
   an added/changed statement in place (`docs/notes/deferred-items.md` D-6). Use the whole-file
   strip-and-synthesize: strip the file's entire `SIDECAR` section → keep your readable edit → `converter
   to-xml --synthesize` → import → compile → re-export → `to-ir`. This regenerates every network's sidecar
   UIds, which is fine — the invariance check below reads the sidecar-*free* form, so untouched networks
   still prove identical. (A sidecar-less block skips this — just edit + `--synthesize`.)
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
