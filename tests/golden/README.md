# Golden round-trip corpus

Committed reference-project exports; the enforcement mechanism for losslessness (docs/08-testing-strategy.md, Layer 1).

Per block: `SimaticML → IR → SimaticML' → import → compile → re-export → SimaticML''`, asserting IR self-stability, `SimaticML'' ≡ SimaticML` after normalization, and clean compile at every import.

New LAD constructs found in the wild are added to the reference project *first* (failing), then fixed. Runs on every converter change.
