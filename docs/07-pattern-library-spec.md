# 07 — Pattern Library Specification

Generated LAD is composed from proven patterns, not invented rung by rung (`04-design-philosophy.md` §6). This spec defines what a pattern is and what it takes for one to enter the library.

## What a pattern is

A parameterized LAD fragment (one or more networks, or a whole FB) that implements one well-understood function: motor start/stop with interlocks, valve with feedback monitoring, signal debounce, alarm latch with acknowledge, pulse generator, sequence step, analog scaling, etc.

## Pattern anatomy

Each pattern is a folder under `patterns/<name>/`:

```
patterns/motor-start-stop/
├── pattern.md        # human doc: intent, behavior, when (not) to use, timing diagram if relevant
├── template.ir       # IR fragment with parameter slots
├── params.yaml       # slot definitions: name, data type, direction, required/optional, default
├── tests/            # simulation test sequences proving the behavior (S9 format)
└── examples/         # at least one real, approved instantiation
```

## Parameter slots

- Every external reference in the template is a slot — no hardcoded tags.
- Slots declare data type and direction (in/out/inout/static) so instantiation can be type-checked against real project tags *before* the compile gate ever sees it.
- Optional slots (e.g. a second interlock) define what the template does when omitted.

## Admission criteria (all required)

1. Instantiated at least once in reviewed, working logic — patterns are *proven*, then librarized; never speculative.
2. Round-trips losslessly (golden test on the template).
3. Compiles when instantiated with each example.
4. `pattern.md` complete: a colleague could use the pattern from the doc alone.
5. Human sign-off recorded in `pattern.md` (name, date).

## Composition rules

- Patterns compose by wiring one pattern's outputs to another's input slots; the AI proposes the wiring, the type check validates it.
- A generation request that can't be satisfied ≥80% from patterns should surface that fact; freeform rungs are opt-in per request and flagged in the review diff.

## Versioning

Patterns are versioned by git; breaking slot changes require a new pattern name (`motor-start-stop-v2`), because existing instantiations reference the old contract.
