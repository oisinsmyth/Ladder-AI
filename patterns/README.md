# Pattern library

Proven, parameterized LAD fragments. Spec: `docs/07-pattern-library-spec.md`. Seeded in S6 (~10 patterns: motor start/stop, valve control, debounce, alarm latch, pulse, sequence step, …).

One folder per pattern:

```
patterns/<name>/
├── pattern.md        # intent, behavior, when (not) to use, sign-off
├── template.ir       # IR fragment with parameter slots
├── params.yaml       # slot definitions: name, type, direction, required/optional, default
├── tests/            # simulation test sequences (S9 format)
└── examples/         # ≥1 real, approved instantiation
```

Admission requires: proven in reviewed working logic, lossless round-trip, compiles with each example, complete doc, human sign-off. Breaking slot changes = new pattern name.
