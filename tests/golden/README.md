# Golden round-trip corpus

The enforcement mechanism for losslessness (`docs/08-testing-strategy.md`, Layer 1). C#
(`GoldenHarness.sln`/`GoldenHarness.Tests/`), matching the converter/`openness-cli`
toolchain (ADR-0002).

Per block: `SimaticML → IR → SimaticML' → import → compile → re-export → SimaticML''`,
asserting IR self-stability, `SimaticML'' ≡ SimaticML` after normalization, and clean compile
at every import. `RoundTripRunner.cs` orchestrates each stage by shelling out to the built
`openness-cli.exe`/`converter.exe`; `Normalizer.cs` documents and implements the normalization
rules, narrower than originally anticipated because the IR sidecar (ADR-0001) preserves exact
source UIds on regeneration — what's left to normalize is only what TIA itself regenerates
regardless of input (timestamps like `ModifiedDate`/`CompileDate`).

## Why this directory has no committed fixtures yet

`RoundTripRunner`/`Normalizer` are real, tested code (`NormalizerTests.cs` — synthetic
before/after XML, no live Portal needed). What's missing is a *committed* corpus to run them
against automatically. The live proof so far has run against real blocks in
`JOB9002 - Tom White Waste` (Amber-tier, `docs/13-data-boundary.md`) — those can't be committed
here. The permanent corpus needs a small purpose-built Green project (S0's original "reference
project" deliverable, still outstanding — S1 overall plan item 8). This is a deliberate, not
abandoned, empty state.

New LAD constructs found in the wild are added to the corpus *first* (failing), then fixed,
once it exists. Runs on every converter change.
