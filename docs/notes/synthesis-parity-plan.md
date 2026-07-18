# Complete synthesis + the parity harness — plan (2026-07-18)

**Goal.** Make `SidecarSynthesizer` cover every construct the *read* side covers, so a sidecar can
always be **derived** from readable IR rather than **stored**. The end-state (north star) is
**derive-always**: the readable IR is the single source of truth, `to-xml` derives the sidecar by
default, and the stored `SIDECAR` sections — and with them the whole staleness/D-6 problem class — go
away.

Owner-approved 2026-07-18 (this session). PC-side converter/test work — normal software rules, not
`lad-coder`.

## Why this is the right direction (the reframing that started it)

The sidecar is **not needed to read the logic** (ADR-0001) — it's machine-owned round-trip bookkeeping
(UIds, wire topology, scopes, some type attrs). `--synthesize` already *derives* a valid, importable,
TIA-compilable sidecar from readable IR alone (proven live, gen-block-new). So the sidecar stores
**nothing semantically load-bearing that isn't recoverable** from `{readable IR} + {surrounding
DBs/UDTs/callees} + {complete synthesis rules}`. Storing it is an optimization + a fidelity choice, not
a necessity.

Which means **D-6, sidecar-staleness, and the scoped merge all exist only because we store a derivable
artifact** — they are cache-invalidation problems, and the cache is redundant. Close the synthesis
coverage gap and we can stop storing it: the problem class dissolves rather than getting worked around.

The only thing keeping the sidecar mandatory *today* is that synthesis coverage < read coverage (a real
as-built FB uses TONR, array-index members, typed comparisons/converts that synthesis can't yet mint —
see `converter-synthesis-gaps.md`). This plan closes exactly that gap, measured against a self-checking
oracle.

## The oracle (the measurement — owner's idea)

The real export **is** the answer key. For any block:

```
export block             → real.xml         (real TIA UIds)
converter to-ir real.xml → real.ir          (readable + REAL sidecar)
strip the SIDECAR section → readable.ir      (logic only)
converter to-xml --synthesize readable.ir → synth.xml   (DERIVED sidecar, minted UIds)
Normalizer.AreSemanticallyEquivalent(real.xml, synth.xml)   → PASS / FAIL
```

This is "compare the derived sidecar to the exported sidecar," done at the SimaticML level (sidecar↔XML
is deterministic via `FlgNetBuilder`, so XML-equivalence *is* sidecar-equivalence up to the UId renaming
TIA does anyway). It reuses the trusted `Normalizer` that already encodes TIA's UId-volatility model.

- **Offline + CI-runnable.** The reference corpus ships both sides already: `ir/reference/<b>.ir`
  (readable + sidecar) and `simatic-ml/reference/<b>.xml` (real export). No Portal needed — the answer
  key is committed.
- **Type-sensitive.** `Normalizer` preserves non-volatile attributes, so `SrcType`/`DestType`/param
  types **are** compared — a Gap B/E mistype fails the offline harness, not only a live compile.
- **Live compile backstop.** `SynthesizerLiveCheck` already does derive→import→compile for one probe;
  extending it corpus-wide is the ground-truth check for anything the offline oracle can't see.

## Stage 0 — the parity harness (the measuring instrument, built first)

`tests/golden/GoldenHarness.Tests/SynthesisParityRunner.cs` (+ a `[Fact]`): for each of the 14 reference
blocks, strip the sidecar from `ir/reference/<b>.ir`, run `converter to-xml --synthesize`, compare to
`simatic-ml/reference/<b>.xml` via `Normalizer`. Emit a per-block PASS/FAIL matrix; on a synthesis
failure, the synthesizer's own per-network *populated-unsupported* message
(`SidecarSynthesizer.cs` `UnsupportedSynthesisConstructException`) self-identifies the blocker.

Output: a **per-block × per-construct PASS/FAIL matrix** — the empirical gap catalogue (replacing the
remembered A–E list) and a permanent regression suite. *"Complete synthesis" = every corpus block green.*
As each gap closes, its block flips green and becomes a hard assertion (known-red blocks tracked
explicitly so CI stays honest without going red on not-yet-built coverage).

**Stage 0 DONE (2026-07-18) — baseline 9/14 green.** Harness built + run. First pass flagged 9 failures;
triage found **4 were an oracle under-strip** (`Normalizer` wasn't treating `SW.Blocks.CompileUnit`'s
block-scoped `ID` as volatile — fixed, no regression to the 14 existing `NormalizerTests`), lifting the
baseline to 9/14. The remaining **5 are real synthesis gaps** (`converter-synthesis-gaps.md`): SUB/DIV,
**WordAnd (Gap F, new)**, TONR (blockers); **timer-instance-scope (Gap G, new)** and **standalone-Not
(Gap H, new)** (compare-divergences). The 9 green are locked into the test's `KnownGreen` guard.

## Stage 1 — close the gaps (ordered by leverage)

Enumerated from the construct dispatch (`src/converter/Converter/Ir/SidecarSynthesizer.cs`):

| Gap | Missing | Where | Effort |
|---|---|---|---|
| **D** | array-index local member mis-scoped `GlobalVariable` | `ScopeFor` — strip `[i]` before local lookup | tiny |
| **C** | TONR/TOF (TON-only) | `BuildTimerSidecar` — add reset/off ports | moderate |
| **B+E** | CONVERT hardcoded Real→DInt; compare `SrcType` magnitude-only | new `TagTypeRegistry` (sibling of `CalleeInterfaceRegistry`) | larger (symbol table) |
| **arith** | SUB/DIV (MUL/ADD-only) | `BuildMulSidecar` family | small |
| **CALL** | InOut params (Input/Output only) | `BuildCallArguments` | moderate |
| **A** | UDT-param inline-nesting whitespace | interface serialization, or adopt bare-type-ref permanently | small |

Each fix lands with its corpus block flipping green in the Stage-0 matrix — the harness *is* the test.
**B+E is the keystone** (the recurring "no symbol table" theme; the callee registry is done, this is the
tag/member-type sibling).

## Stage 2 — extend the corpus to what the reference project lacks

The 14 Green blocks already exercise TONR/TOF (`FBTimers`/`ScaleValue`), comparisons (`ThresholdAlarms`),
arithmetic (`SignalConditioning`/`DataHandling`), CALL (`TimingAndCalls`). What they don't (array-index
locals, Word→Int convert, Real tag-vs-tag compare, UDT-param inline, SUB/DIV, InOut) needs coverage —
**prefer new Green reference blocks** (committable, no sanitize); fall back to **sanitized JOB9002 blocks**
(under the 2026-07-18 generation-validation scope, `docs/13`) for anything only a real as-built FB shows.

## Stage 3 — flip to derive-always (the payoff)

Once the parity matrix is fully green **and** the live compile backstop passes corpus-wide, record the
ADR: readable IR is canonical; `to-xml` derives the sidecar by default; stored `SIDECAR` sections are
deprecated. At that point **D-6 and the scoped merge become moot** — editing a network just re-derives.

**Sequencing.** Keep stored sidecars until parity is *proven per construct* (don't drop the safety net
early). The **D-6 scoped merge stays as the interim bridge** so real-block modifies work *now*
(`converter-synthesis-gaps.md` — keep unchanged networks' real sidecars, synthesize only changed ones),
and retires itself when derive-always lands. Same converter investment, aimed at *eliminating* the
sidecar rather than *maintaining* it.

## Definition of done

- Offline parity harness green over the full corpus (type-sensitive).
- `SynthesizerLiveCheck` extended corpus-wide passes (import + compile).
- Derive-always ADR recorded; `converter-synthesis-gaps.md` reframed into a parity checklist (each
  construct = a corpus block that must synthesize-match its export).

## Tracking

`converter-synthesis-gaps.md` (the gap detail, being reframed as the parity checklist); `AITODO.md`.
The scoped merge algorithm sketch lives in this session's transcript / `deferred-items.md` D-6.
