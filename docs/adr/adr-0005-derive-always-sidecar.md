# ADR-0005 — Derive-always: the sidecar is derived, not stored

- **Status:** Accepted (owner, 2026-07-18)
- **Date:** 2026-07-18
- **Refines:** ADR-0001 item 5 (the sidecar). Does not supersede it — the two-layer readable/sidecar
  concept stands; this changes the sidecar from a *stored* artifact to a *derived* one for the covered
  construct set.

## Context

ADR-0001 gave the IR two layers: a readable body plus a machine-owned **sidecar** (UIds, wire identity,
scopes, some type attributes, culture data). The sidecar is stored alongside the readable form in every
`.ir` file, and `to-xml` uses it to regenerate the exact SimaticML. Per ADR-0001 the sidecar is "never
needed to understand the logic" — it's round-trip bookkeeping, not meaning.

Storing a *derivable* artifact creates a cache-invalidation problem class: editing a network invalidates
its stored sidecar. Concretely this is **D-6** (`docs/notes/deferred-items.md` — a statement can't be
added to a sidecar-carrying network in place; the workaround is a whole-file strip-and-synthesize), the
recurring **sidecar-staleness** bugs (the `TimerSample` stale-sidecar finding), and the **scoped-merge**
friction that modifying *real* as-built blocks (S7) otherwise needs.

The 2026-07-18 synthesis-parity work removed the reason to store it. `SidecarSynthesizer` now reaches
parity with the read side across **every construct the reference corpus exercises** — contacts / coils /
OR-merge / negated contacts and standalone `Not`, comparisons, TON/TONR/TOF (incl. same-network `.Q`
direct-wire), MOVE, the box family (MUL/ADD/SUB/DIV, CONVERT, ABS, SWAP, WAND, CALC, T_SUB, T_CONV,
MOVE_BLK_VARIANT), and wired CALL — with types resolved from the surrounding DBs/UDTs/callees via the
`TagTypeRegistry`. The offline parity harness (`tests/golden/GoldenHarness.Tests/SynthesisParityRunner`,
matrix at `tests/golden/synthesis-parity-matrix.md`) confirms **all 14 reference blocks** derive
(`to-xml --synthesize`) a sidecar the golden `Normalizer` proves semantically equivalent to the real TIA
export. So, for the covered construct set, storing the sidecar is now an optimization and a byte-fidelity
choice — not a necessity.

## Decision

Adopt **derive-always**: the readable IR is the single source of truth; `to-xml` derives the sidecar by
default; stored `SIDECAR` sections are deprecated. Editing a network just re-derives — **D-6 and the
scoped merge become moot.** The flip is **gated** (on a live compile backstop) and **bounded** (to the
synthesizable construct set), and rolls out in phases (below), so this ADR is the direction and the
guardrails, not an immediate deletion.

## Options considered

1. **Keep storing the sidecar (status quo) + build the D-6 scoped merge.** The scoped merge splices each
   unchanged network's *real* sidecar and mints fresh UIds only for changed statements. Genuinely useful
   as an *interim bridge* for modifying real blocks today. Rejected as the end-state: it maintains the
   redundant cache and its whole problem class rather than removing it.
2. **Derive-always, deprecate stored sidecars (chosen).** Eliminates the problem class. Cost: synthesis
   coverage must keep pace with read coverage, and type resolution needs project context at conversion
   time (both bounded and guardrailed below).
3. **Hybrid — derive by default, keep an optional stored sidecar as a debug aid.** `to-ir` could still
   emit a sidecar for inspection while `to-xml` ignores it and derives. Kept as the fallback/opt-in
   inside the chosen option, not as the canonical path.

## Consequences

**Easier / removed:** no sidecar staleness; D-6 and the scoped merge retired; `.ir` files carry readable
logic only; the readable form is unambiguously canonical (which was always ADR-0001's intent — AI
legibility first).

**Harder / commits us to:**

- **Synthesis must track reads.** A block can drop its sidecar only once every construct it uses is
  synthesizable. The still-unsynthesizable set (`Limits`, `Waits`, `FillBlockI`, `Modbus*`, and any
  non-reducible fallback network per ADR-0001) keeps its sidecar until added. The **parity harness is the
  guardrail**: every corpus block must stay green, and a new construct lands together with its parity
  block. This is a real, ongoing obligation.
- **Project context at `to-xml` time.** Types resolve from the surrounding DBs/UDTs/callees (`--project`);
  a block converted in isolation without them hard-errors on an unresolvable type rather than guessing.
  Acceptable — the generation/modification pipeline always has the project export in hand.
- **Byte-fidelity becomes derived, not stored.** Derived UIds differ from the original export's, but TIA
  reassigns all UIds on import anyway and the Normalizer proves semantic equivalence; `to-xml → to-ir →
  to-xml` stays byte-stable (synthesis is deterministic). What we give up is the stored guarantee that "a
  re-export differs from the original only in known-volatile UIds" — kept instead as a derived property
  the harness checks.

**Rollout (phased):**

1. Offline parity green corpus-wide — **DONE** (14/14, `synthesis-parity-matrix.md`).
2. **Live compile backstop corpus-wide** — **DONE 2026-07-18.** `SynthesizerLiveCheck.RunCorpus` derived,
   imported, and compiled all 10 reference code blocks against the real `SampleProject` in TIA — **10/10
   pass** (`NodeStatusAlarms`, `PerimeterSafetyAlarms`, `TimerSample`, `ThresholdAlarms`,
   `SignalConditioning`, `DataHandling`, `BooleanExtras`, `FBTimers`, `ScaleValue`, `TimingAndCalls`). So
   the derived form is not merely Normalizer-equivalent to the export — TIA itself imports and compiles it
   clean. Both gates (offline 14/14 + live 10/10) are now green; what remains is owner acceptance of this
   ADR, then phases 3–5.
3. Make `to-xml` derive by default for blocks fully within the synthesizable subset; keep the
   stored-sidecar path for the rest and as an explicit opt-in. — **DONE 2026-07-18** (`to-xml`
   auto-detects: derives when no `SIDECAR`, uses it when present; `--synthesize` forces the derive path).
4. Omit stored `SIDECAR` sections for synthesizable blocks; update `ir/SPEC.md` §Sidecar. —
   **CORRECTED 2026-07-19.** First shipped 2026-07-18 as `to-ir` auto-omitting whenever
   `IsSynthesizable` (synthesis succeeds). **That was unsafe** — a real block can *synthesise-but-diverge*
   (found: the `test-project001` FBs + `patterns/motor-dol/MotorStarter`, on gaps the reference corpus
   never exercised — array-index locals, Gap D). "Synthesis succeeds" is necessary but **not** proof the
   derived graph matches, so auto-omitting on it would silently corrupt such a block. Corrected: `to-ir`
   **keeps the sidecar by default**; `--no-sidecar` is the explicit opt-in (guarded — errors if the block
   can't even synthesise — but it does not itself prove equivalence, the caller's responsibility). A
   committed block is stored readable-only only after a **verified migration** proves it equivalent (the
   parity/round-trip harness). See the `CriticalCaveat` below.
5. Retire D-6 / the scoped merge from the roadmap (`deferred-items.md`). — **DONE 2026-07-18** (D-6
   marked resolved by derive-always; the scoped merge is no longer needed).

**CriticalCaveat — "synthesis succeeds" ≠ "safe to omit".** The safety invariant is: never store a block
readable-only unless its derived form is *proven semantically equivalent* to its export. The reference
corpus (14 blocks) is **not representative** — real blocks synthesise-but-diverge. So (a) the committed
migration strips sidecars only from blocks the round-trip harness proves equivalent (14 did; 4 diverged
and kept theirs), guarded permanently by `CommittedBlocksRoundTripTests`; and (b) `to-ir` cannot yet omit
safely-and-automatically because the equivalence check (the golden `Normalizer`) lives in the test project,
not the converter. **Follow-on:** port a semantic-equivalence check into the converter so `to-ir` can
verify `synth ≡ source` and then omit automatically — the only way the policy becomes both safe *and*
one-step for arbitrary real exports. The 4 divergent blocks are tracked as synthesis gaps in
`converter-synthesis-gaps.md`.

**Follow-on (not blocking):** the S7 modification-choreography (`docs/notes/modification-choreography.md`)
still documents the D-6 whole-file strip-and-synthesize workaround; with derive-always, editing a
sidecar-less block just re-derives, so that step simplifies — to be folded into the modify skills when
they're next touched.

**Revisit if:** the unsynthesizable construct set turns out large or common in real projects (then stored
sidecars stay necessary for those blocks), or ADR-0001's non-reducible fallback turns out common (same) —
either would mean derive-always applies to a smaller slice than the reference corpus suggests.
