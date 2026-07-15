# 07 — Pattern Library Specification

Generated LAD is composed from proven patterns, not invented rung by rung (`04-design-philosophy.md` §6). This spec defines what a pattern is and what it takes for one to enter the library.

## What a pattern is

A proven, reusable piece of LAD, extracted and documented from real working logic — never invented speculatively (`04-design-philosophy.md` §6/§7). **Revised 2026-07-15**: an earlier design represented this as a standalone `template.ir` file with reserved-sentinel "parameter slots," instantiated by text substitution. Abandoned — it duplicated parameterization/type-checking this project's IR model and TIA's own compiler already provide via ordinary FB/FC interfaces and `CALL`, and it never reconciled with `06-lad-conventions.md` C-106 (repeated equipment already uses FB+UDT, by site convention, predating this project). No new IR syntax, file format, or tooling is used below.

Two kinds of pattern have been identified so far — **this is not a closed taxonomy**; a pattern that doesn't fit either shape (e.g. a cross-block micro-idiom like constructed edge-detection, C-402/C-404, which recurs in many unrelated blocks via shared storage rather than living in one FB or one sequencing FC) just needs its own shape defined when it's actually built, not forced into one of these two.

### Kind 1: Equipment-instance pattern

A whole, proven, callable FB/FC — e.g. `MotorDOL`. Reuse is an ordinary `CALL` with real arguments; TIA's own compiler type-checks every argument at compile time, for free. This is C-106 as already practiced at this site, not a new mechanism. Includes the *entire* block, not a slice of it — the pattern is whatever coherent unit of behavior the site already treats as one equipment FB (e.g. `MotorDOL`'s fault detection, hours-totalizer, and telemetry are part of what "a motor" means here, not separable extras).

```
patterns/<name>/
├── pattern.md   # intent, full real behavior, when (not) to use, sign-off
├── <name>.ir    # the block itself: ordinary IR, ordinary tags, no template syntax
└── examples/    # ≥1 real CALL site (instance DB + calling network), sanitized
```

Admission (all required):
1. Instantiated at least once in reviewed, working logic — proven, not speculative.
2. Round-trips losslessly — the block itself, ordinary round-trip discipline (`Normalizer.AreSemanticallyEquivalent`), nothing template-specific.
3. Compiles when instantiated — the `examples/` `CALL` site imports and compiles clean.
4. `pattern.md` complete: a colleague could use the pattern from the doc alone.
5. Human sign-off recorded in `pattern.md` (name, date).

### Kind 2: Repeated rung-shape pattern

The same logical rung shape repeated many times *within* one sequencing/mapping FC (e.g. `PlantAutoControl`'s per-equipment networks), deliberately kept inline rather than factored into calls — C-109/C-110 want an area-Main FC to read top-to-bottom like a table of contents, which breaking it into many small calls would fight. No template mechanism: a real, documented, sanitized example is what the AI drafts a new instance from by analogy during generation, checked by the ordinary compile gate and human review — the same way any other generated rung is checked, no separate type-checker needed.

```
patterns/<name>/
├── pattern.md   # the shape: what's fixed, what varies, real annotated example,
│                # which sequencing/mapping FC it's drawn from, sign-off
└── examples/    # 1+ real network excerpts, sanitized (IrSerializer.SerializeNetworkOnly /
                 # IrParser.ParseNetworkOnly — no full block/sidecar needed for a standalone excerpt)
```

Admission (all required):
1. Instantiated at least once in reviewed, working logic — proven, not speculative.
2. The excerpt itself round-trips losslessly.
3. **At least one newly-drafted instance** — covering a real, structurally distinct case not already in `examples/` (e.g. a chain-head vs. a mid-chain equipment position), using a specific real not-yet-represented equipment context rather than synthetic tags — compiles clean, and the reviewer confirms it wasn't produced by editing a duplicate of an existing excerpt. (A plain "compiles clean" test on a copy-paste-rename would prove nothing about whether `pattern.md` itself was sufficient — this is the deliberately harder bar.)
4. `pattern.md` complete: a colleague could use the pattern from the doc alone.
5. Human sign-off recorded in `pattern.md` (name, date).

## Composition rules

- Kind-1 patterns compose via ordinary `CALL` wiring — ports connect the same way any two real blocks already do.
- Kind-2 patterns compose by the AI drafting a new instance of the documented shape with real tags, reviewed the same as any other generated rung.
- A generation request that can't be satisfied ≥80% from existing patterns should surface that fact; freeform rungs are opt-in per request and flagged in the review diff.

## Versioning

Patterns are versioned by git. For kind 1, a breaking interface change needs a new pattern name (or a new FB `Version`, mirroring how this project's own IR model already tracks block/timer versions) — existing call sites reference the old contract. For kind 2, a breaking change to "what's fixed" in the documented shape needs a new pattern name for the same reason.
