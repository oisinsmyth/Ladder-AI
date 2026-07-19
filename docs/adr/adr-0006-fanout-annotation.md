# ADR-0006 — Contact fan-out is recorded per-node in the readable IR (`split`/`recv`)

- **Status:** Accepted (owner, 2026-07-19)
- **Date:** 2026-07-19
- **Refines:** ADR-0001 (the IR grammar — adds a node-level annotation). **Enabled-by / required-by
  ADR-0005** (derive-always: because the sidecar is no longer stored, anything it encoded that is *not*
  present in the readable logic must now be carried by the readable itself — fan-out is exactly such a
  thing). **Supersedes** the interim per-network `SPLIT` grammar (introduced 2026-07-19, recorded in
  `docs/notes/converter-synthesis-gaps.md`, never its own ADR).

## Context

A LAD network is a **DAG**, not a tree: one part's output can fan out to several consumers ("contact
fan-out" / a "split"). The readable IR is tree-shaped (nested `Expr`), so fan-out is the precise point
where the drawing diverges from the logic.

Fan-out is a **pure drawing choice, not derivable from the logic.** The hand-authored `HandAuthorSplitsMerges`
pairs proved it directly: a split network and its split-free equivalent have **byte-identical readable
logic but different sidecars**. Under ADR-0005 (derive-always) the sidecar is derived from the readable,
so a bit that isn't in the readable cannot be reconstructed — the readable must carry it.

The first attempt carried it coarsely: a per-network **`SPLIT`** flag plus a synthesis heuristic that
shares the maximal common *leading* sub-expressions across a split network's statements. That handled the
clean cascades (`HandAuthorSplitsMerges` 10/10 networks byte-exact, parity 15/15) but left a residual.

**The 2026-07-19 depth-aware experiment settled that the residual is not a heuristic-tuning problem.**
Threading the sharing cache into OR-branches / NOT-operands ("share maximally within a split network")
**closed** `MotorStarter` N13 and the `NotFedByContact` fixture — but **over-shared** N4 and N7. N13 and
N4 have the *identical logical shape* (a contact appearing top-level and inside an OR in the same network),
and TIA drew them **oppositely**: N13 fans the shared contact into the OR; N4 draws a separate contact. A
single heuristic is therefore wrong half the time. The distinguishing bit genuinely is not in the logic —
it must be recorded explicitly, **per node**.

The fan-out extraction (`tmp/fanout.py`, run over the stored sidecars) also showed the phenomenon is
**four distinct shapes**, and that a per-network flag is structurally too coarse for two of them:

| Shape | Real example | Why the `SPLIT` flag can't express it |
|---|---|---|
| Cross-statement | N13 `NF·Run·RF` cascade → 5 MOVEs; N12 `Q` → 2 coils + contact | (handled by top-level prefix sharing) |
| Cross-depth | N13 move 3's OR taps the cascade; `NotFedByContact` | needs per-node gating (N4 same shape, must *not* share) |
| **Intra-statement** | N1 `IO.TryRunMotor` shared between two OR-branches of the *single* coil `IO.Run` | `DetectSplit` only sees *cross-statement* reuse — N1 isn't even flagged |
| **Non-contact receive** | N12 `RisingEdgeFlags` → `Lt.pre` + `Eq.pre`; N13 → `Move.en` | the tap feeds a compare/move/coil port, not a contact |

(N3 looked like a fifth residual but was a *different* bug — a latch-coil timer-Q direct-wire — fixed
2026-07-19, unrelated to fan-out. See `converter-synthesis-gaps.md`.)

## Decision

Record contact fan-out **explicitly, per node, in the readable IR**, and **remove the synthesis heuristic
and the per-network `SPLIT` flag entirely.**

- **`{split N}`** marks the *master* occurrence of a shared node — the one place its part is drawn. `N` is
  an **ordinal label scoped to the network** (`1`, `2`, …).
- **`{recv N}`** marks every *other* position that is physically that same shared node (its output wire
  taps in), rather than a fresh part.
- The marker attaches to a sub-expression node **at any depth** and the receiver may feed **any consumer
  port** (`contact.in`, `coil.in`, `move.en`, `compare.pre`). The **full logic chain stays visible** — the
  marker only records *which occurrence is physically shared*, it never hides an operand. Example (N1):

  ```
  COIL IO.Run := (IO.TryRunMotor{split 1} AND PreStartMemory AND IO.RecentStart
               OR IO.TryRunMotor{recv 1} AND IO.Run) AND NOT IO.StopMotor AND NOT IO.Shutdown
  ```

  A reader still sees `IO.TryRunMotor AND IO.Run`; `{recv 1}` only says that second `IO.TryRunMotor` is
  the same physical contact as the first, not a new one.

Three sub-decisions, resolved by the owner (2026-07-19):

1. **Annotate-all.** *Every* fan-out point is marked; synthesis never guesses. (Not "annotate only the
   non-derivable residual".)
2. **Ordinal labels**, network-scoped.
3. **Retire the `SPLIT` flag** and `DetectSplit` — one explicit mechanism, not a flag plus a heuristic.

**Derivation (`to-ir`) is lossless.** The export *is* a DAG; a fan-out node is a part whose output wires
to more than one consumer (exactly what `fanout.py` extracts). `GraphReducer` already knows each contact's
source part UId, so it assigns one ordinal per shared part, tags the first occurrence in document order
`{split}` and the rest `{recv}`. No guessing — it reads the DAG.

**Synthesis (`to-xml`) is label-driven.** A `{split N}` node builds once and registers its output wire
under `N`; a `{recv N}` reuses that wire (fan-out, no new part/access). This is the same branch-threading
mechanism the experiment proved works — but gated by explicit labels instead of a prefix guess, so it can
neither over-share N4 nor miss N1.

## Options considered

1. **Keep the `SPLIT` flag, improve the heuristic (depth-aware sharing).** Rejected — *proven impossible*:
   N13 and N4 are the same logical shape with opposite real drawings, so no rule over the logic alone is
   correct for both (2026-07-19 experiment; over-shared N4/N7 while fixing N13).
2. **Per-node `split`/`recv`, annotate-all (chosen).** Lossless, version-stable, one mechanism; covers all
   four shapes including intra-statement and non-contact receivers.
3. **Per-node, but annotate only the non-derivable residual** (derive the canonical prefix cases silently,
   mark only the ambiguous ones). Rejected: the readable would become **version-dependent** (what's marked
   changes as synthesis improves) and **ambiguous** (a clean network might still hide fan-out), and it
   keeps two mechanisms. Contradicts "completely derivable".
4. **Hoist shared sub-expressions to named intermediates (`let`-binding).** Reads well but **breaks the
   full-chain-visible readability the owner chose** and the split-vs-split-free textual identity that makes
   the corpus oracle clean. Rejected.

## Consequences

**Easier / removed:**

- Fan-out becomes **fully derivable** — the last thing blocking `MotorStarter` and the `test-project001`
  FBs from dropping their stored sidecars (the ADR-0005 residual for those blocks).
- The synthesis heuristic (`SidecarSynthesizer`'s prefix-signature cache) and `DetectSplit` / the
  `IrNetwork.Split` flag are **deleted** — one explicit mechanism, no over/under-share failure mode.
- Lossless and **version-stable**: `to-ir` reads fan-out from the DAG; `to-xml` consumes labels; neither
  guesses, so the readable output doesn't drift as the converter evolves.

**Harder / commits us to:**

- **A new node-metadata class in the grammar.** `{split N}`/`{recv N}` are *not* tags — `preflight`,
  `tagstatus`, `review`, and `digest` must ignore the marker and ground the underlying tag normally
  (hard rule 3 is unaffected: the operand is still a real tag). One-time toolchain surface.
- **Readable carries a drawing detail inline.** Mitigated by design: the full logic chain is always
  visible; the marker is a small suffix that only asserts physical identity. Annotate-all means a
  fan-out-heavy network (e.g. `FB_MotorFwdRevSystem` N14) shows many markers — accepted as the honest,
  stable cost of "no hidden fan-out".
- **Migration must prove byte-exact.** `HandAuthorSplitsMerges` (today SPLIT-flag + heuristic) re-derives
  with per-node markers and must stay byte-exact; the parity harness (15/15) is the guard.

## Rollout (phased)

1. **Grammar + model.** `ir/SPEC.md`: the `{split N}`/`{recv N}` node-marker syntax and lexing; `Expr`
   carries an optional network-scoped ordinal label; parser + serializer round-trip it. *(SPEC/grammar
   change — owner sign-off gate, per [[feedback_ask_before_design_deviation]].)* — **DRAFT WRITTEN
   2026-07-19** as a clearly-banner-marked "PROPOSED / not yet implemented" subsection of `ir/SPEC.md`
   (readable-form section), plus a cross-reference from the Sidecar section. Covers tokens, placement,
   the single-contact / cascade-junction / non-contact-receiver cases, recv-as-node vs recv-as-origin, an
   EBNF sketch, and the parse/validate constraints. **Awaiting owner sign-off** before the model + parser/
   serializer work begins; nothing in the converter accepts the markers yet.
2. **Derivation.** `GraphReducer` emits `split`/`recv` from shared part UIds (document-order master);
   **remove `DetectSplit` and the `IrNetwork.Split` / network `SPLIT` flag.**
3. **Synthesis.** A per-network label registry replaces the prefix cache; `{recv N}` wires fan-out at any
   depth and to any consumer port. Implement both receiver flavors: **recv-as-node** (the marked operand
   *is* the shared node — N1, N12) and **recv-as-origin** (a branch *begins* at node `N` then continues —
   N13 move 3 tapping the `NF·Run·RF` junction). Delete the prefix-signature cache.
4. **Migrate + validate.** Re-derive `HandAuthorSplitsMerges` markers (stay byte-exact); drive
   `MotorStarter` + the four `test-project001` FBs to **per-network byte-exact** against the `fanout.py` /
   golden `Normalizer` oracle; then drop those blocks' stored sidecars (closing the ADR-0005 residual) and
   guard with the round-trip tests.

**Guardrails:** the fan-out extractor + `Normalizer` as the answer key; the synthesis-parity harness stays
green corpus-wide throughout; per-network byte-exact is the acceptance bar for each migrated block.

## Known adjacent issue (not this ADR to fix)

`MotorStarter` N12's readable shows a single `IO.HrsRun = 4294967295` where the real export has a `Lt`+`Eq`
pair sharing one `RisingEdgeFlags[2]` contact. The **shared contact** is fan-out (this ADR), but the
**one-compare-vs-two-compares** structure is a *reducer* fidelity question orthogonal to fan-out. If N12
doesn't reach byte-exact on the shared contact alone in phase 4, that compare-structure difference is the
reason and is tracked separately in `converter-synthesis-gaps.md`, not resolved here.

## Revisit if

- A fan-out shape appears that `split`/`recv` cannot express (e.g. fan-out of a box's non-boolean output
  in a topology the node model doesn't capture) — then the grammar needs extending, not just relabelling.
- Annotate-all marker density measurably harms AI/human legibility enough to reconsider selective
  annotation (option 3) — revisit only with evidence, given the version-stability cost it trades away.
