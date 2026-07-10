# converter

SimaticML ↔ IR, bidirectional, lossless. Built in S1.

```
converter to-ir  <files>    # SimaticML → IR
converter to-xml <files>    # IR → SimaticML
```

Rules (docs/05-architecture.md, 04 §8/§10):

- Unknown elements are hard errors, never warnings or best-effort.
- Volatile attributes (UIDs, ordering, geometry) preserved in the IR sidecar; the normalizer canonicalizes them for diffing — every normalization rule documented with an example.
- Every change reruns the golden round-trip suite (`tests/golden/`).
- Refuses safety (F-) content.
