# 08 — Testing & Validation Strategy

Three layers: converter correctness (golden round-trip), output validity (compile gate), and behavioral correctness (simulation). Each layer catches what the one before can't.

## Layer 1 — Golden-file round-trip (converter correctness)

The reference project is the corpus. For every block:

```
SimaticML (exported) → IR → SimaticML' → import to TIA → compile → re-export → SimaticML''
```

Assertions:

1. `IR` parses and re-serializes byte-identically (IR self-stability).
2. `SimaticML''` ≡ `SimaticML` after normalization (semantic losslessness).
3. Compile succeeds at every import.

**Normalization:** TIA regenerates volatile attributes (UIDs, some ordering, geometry). The normalizer strips/canonicalizes these; every normalization rule is documented with an example, because each one is a claim that a difference is benign — and a wrong claim here is a silent logic change.

Runs on every converter change. New LAD constructs found in real projects get added to the reference project *first*, failing, then fixed.

## Layer 2 — Compile gate (output validity)

Every AI write path ends with `openness-cli import` + `compile` against a scratch copy of the project. Non-zero exit = the result loops back to the AI with the compiler diagnostics; the human only ever sees compiling output (Goal 7). The gate itself is tested: deliberately broken IR must fail it.

## Layer 3 — Simulation (behavioral correctness, S9)

Test sequences are declarative: initial state, per-step stimulus (tag writes), expected observations (tag reads) after N scans. Stored next to the thing they test (`patterns/*/tests/`, or alongside generated blocks). Harness executes them against PLCSIM and reports pass/fail.

Open item (risk R-07): PLCSIM Advanced has historically targeted S7-1500 — confirm S7-1200 G2 support in the installed version. Fallbacks: TIA-integrated PLCSIM, or maintain an S7-1500 shadow project purely for logic-level testing (the LAD is the same; hardware config differs).

## Cross-cutting

- **AI capability checks per stage:** each roadmap stage's exit criteria are effectively acceptance tests (10 accurate explanations, 10/10 compiling generations, 10/10 clean modifications). Track results in `docs/notes/stage-gates.md`.
- **Regression on modification (S7):** after a targeted edit, every *untouched* network must be IR-identical. This is an automated check, not a review step.
- **Safety filter test:** a reference project containing an F-block must cause export to refuse — tested like any other behavior.
- **PC-side code:** normal unit tests — xUnit for C# (`openness-cli`, converter, per ADR-0002's revision); pytest reserved for Python tooling once it exists (`extract/`, S5). The converter is the highest-value target.
