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
4. **Re-derive the sidecar (ADR-0005 derive-always) — and its one residual limit.** A synthesizable block
   is committed sidecar-*less* — `to-ir` omits the sidecar and `to-xml` re-derives it on the way back to
   XML (ADR-0005, Accepted, which **retired** the old D-6 "can't add a statement to a stored sidecar"
   problem, `docs/notes/deferred-items.md` D-6). So for the common case the loop is just: keep your readable
   edit → `converter to-xml --project <ir-dir> --out <staging>` (derive is the default; `--synthesize`
   forces it) → import → compile →
   re-export → `to-ir`. There is no stored sidecar to strip.
   - **PRE-CHECK — the residual limit is the still-unsynthesizable construct set, not the old gap list.**
     Re-deriving requires the ENTIRE block to be within the synthesizable subset (it re-derives every
     network, unchanged ones included). The gaps that used to block real as-built FBs — TONR/TOF (Gap C),
     array-index local members (Gap D), Real tag-vs-tag comparisons (Gap E) — are all **closed** now and
     synthesize fine; all three test-project001 FBs synthesize byte-exact and are committed readable-only.
     What genuinely remains out of subset is **`Limit`/`Wait`/`FillBlockI`/`Modbus*`**: a block using one of
     those still carries a **stored** sidecar and can't be re-derived, so its compile gate can't be reached.
     Check for that set up front by grepping the IR for `Limit`/`Wait`/`FillBlockI`/`Modbus*` and, if it
     is clean, by running `converter to-xml --project <ir-dir> --out <staging>` and reading the exit code.
     ⚠️ **Match at STATEMENT position, not anywhere in the file.** Measured 2026-08-21: this grep
     hits `NETWORK 10 "Step 40 (WaitForwardRun)"` — a network TITLE, not an instruction — and a block
     with a `Limit` tag or a "Waiting" step name would read as blocked when it is not. **`to-xml`'s exit
     code is the definitive answer**; the grep is only a cheap pre-filter, so read every hit before
     believing it.
     *(Corrected 2026-08-21: this said to check "`converter preflight`'s `[convert]` line". **There is no
     `[convert]` line** - preflight emits `FILE`/`NAME`/`CLEAN`/`SUMMARY` and nothing else, so the
     instruction could not be followed as written.)* If the block hits one, report it as the
     converter gap it is — the fix is **adding that construct to synthesis** (guarded by the parity harness),
     **not** the retired D-6 scoped merge — don't force it.
   - Re-deriving regenerates every network's sidecar UIds, which is fine for the *invariance check* (it reads
     the sidecar-*free* form, so untouched networks still prove identical) — but the *compile gate* still
     needs every network to synthesize.
5. **Invariance gate — the hard gate.** `converter diff <as-built.ir> <modified.ir> --only <changed nets>`
   (paths first) must **exit 0**: every network *outside* the named set is provably identical in readable IR.
   `--only` takes space- or comma-separated numbers or repeated flags (`--only 1 2` / `--only 1,2` /
   `--only 1 --only 2`). `diff` handles sidecar-carrying **and** sidecar-less inputs. A **UDT / `TYPE` change
   has no networks** — `diff` is block-only, so verify a UDT edit by member-text inspection + the compile
   gate instead. A `HEADER changed` line surfaces an interface/title/comment delta (expected for a purpose
   change; not for a fix). If diff reports a change you didn't intend, you touched something you shouldn't —
   undo it or stop. This is CLAUDE.md's "untouched-network invariance check", mechanized.
   🔴 **A HEADER CHANGE UNDER `--only` NOW GATES — `--allow-header` IS HOW YOU DECLARE ONE (2026-08-14).**
   Until that date the gate printed `HEADER changed: interface` and then, two lines below,
   **`INVARIANCE OK: all changes confined to --only {N}`, exit 0** — a report disagreeing with itself,
   with the exit code following the wrong half. Measured: an interface member retyped **`Bool` → `Int`**
   with **no network touched** passed; so did a block **renamed and renumbered** (a warning only). Those
   are exactly the changes that compile, import and then misbehave on the controller, and this is the gate
   whose entire job is *"prove the rest is identical"*. `--only` names networks and so cannot express
   header intent — that explained why the **check** was absent and never licensed the **claim**. So:
   unclaimed header change or rename ⇒ **exit 1**; `--allow-header` ⇒ the change is declared and the
   verdict says so (`… plus the header change, declared via --allow-header`). **Purpose changes pass it;
   fixes must not** — a fix that needs an interface member is a purpose change, and the answer is to
   route, not to declare. The clean verdict now also states what it examined (`…, header unchanged`)
   rather than only that it passed.
   ✅ **NARROWED THE SAME DAY, ON THE GATE'S FIRST CONTACT WITH REAL WORK: A COMMENT-ONLY HEADER CHANGE
   DOES NOT GATE.** A fix-wave run widened a Modbus area and repaired two block comments that were
   *already false* (one said the area covered *"8 words"* when it covered 35) — and was refused. It
   rightly declined `--allow-header`, which left a **documentation-only repair with no clean path under
   either modify skill**, while leaving false comments in place was not a neutral option. ***The defect
   was in the TAXONOMY, not the gate:*** `--only` asks one question — *did anything change outside the
   named networks that could alter **what the PLC does**?* An **interface** member answers yes; a
   **title** or a **rename** answers yes (both are identity, and TIA's import matches by name, so a
   rename creates a duplicate block rather than updating one). ***A block comment cannot.*** The report
   already carried `commentChanged` and `interfaceChanged` separately and the verdict threw that away.
   **So: comment-only ⇒ exit 0, with `HEADER COMMENT CHANGED (does not gate)` printed on its own line —
   quote it in the hand-back.** *Non-gating is not invisible: that line is where a stale comment gets
   repaired and equally where a correct one gets silently discarded.* **A comment edit is never cover
   for a behaviour-bearing one** — both together still exit 1, naming the interface, not the comment.
   ⚠️ **AND THE VERDICT NOW STATES ITS OWN DENOMINATOR: `UNCHANGED REMAINDER: <n> network(s) proven
   identical outside --only`.** On a block whose networks are *all* inside the `--only` set the
   remainder is **empty**, and `INVARIANCE OK` proves nothing at all — it says so
   (`NOTHING WAS PROVEN`). Same shape as `drift-check`'s `COMPARED: <n>`: *an invariance claim over an
   empty remainder is another empty-is-not-clean.* **Read that line before quoting an exit 0 as proof.**
6. 🔴 **If you run `converter review` as a cross-check, it needs `--project` too.** Bare, it exits
   **2 = REVIEW INCOMPLETE** with C-118/C-122/C-125 unjudged — but it prints `SUMMARY: … 0 finding(s)`
   **first** and the incomplete line **last**, so a reader who stops at the summary quotes a clean review
   that examined nothing. Measured on a stepper block, 2026-08-21. Fuller account: `review-conventions`'
   Step 0, which is the only place that carried this.
7. **Compile gate** (hard rule 4): `converter preflight <file> --project <ir-dir>` (zero findings) → import to the **scratch** project
   → `openness-cli compile` clean. An **FB with multi-instance timers compiles only after its instance DB
   exists** — `openness-cli create-instance-db` first if there isn't one. Playbook first on any failure;
   claim the `agent-tasks/README.md` Portal queue before import/compile.
8. **The round trip — RECOMMENDED, not required, and the strongest proof this loop can produce.**
   After the compile gate: re-export the block from TIA, `converter to-ir` it, and diff that against the
   IR you edited. `diff --only` proves *you* changed nothing else; the round trip proves **TIA** changed
   nothing either. It costs one read-only export, and it turns "it compiled" into "what is in the project
   is what I wrote". A 2026-08-21 fix run did this unprompted and got 15 of 15 networks identical.
   🔴 **Export to a STAGING dir, never into `simatic-ml/`.** Six blocks are pinned by
   `tests/golden/GoldenHarness.Tests/ExportDriftDetectorTests.cs`, and re-exporting one of those over its
   committed baseline turns that test red. The proof is worth having; it is not worth a red baseline.

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
