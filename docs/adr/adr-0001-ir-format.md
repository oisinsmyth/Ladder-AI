# ADR-0001 — Intermediate Representation format

- **Status:** Proposed (decide during S1, against real SimaticML exports)
- **Date:** 2026-07-09

## Context

The IR is the project's central artifact: AI reads/writes it, humans diff it, converters must round-trip it losslessly, and its semantics must stay PLCopen-compatible so other vendors can plug in later. It must serve three masters: machine losslessness, human readability, and AI legibility.

## Options considered

1. **PLCopen XML (TC6) directly.** Standard, vendor-neutral by definition. But XML diffs poorly, humans won't read it, and TC6 LD coverage of Siemens specifics needs extensions anyway.
2. **Constrained YAML/JSON schema, PLCopen-aligned semantics.** Diffable, parseable everywhere, schema-validatable. Readability is middling for ladder structure (nesting shows topology poorly).
3. **Custom structured text format** (readable network/rung syntax with an explicit contact/coil/branch grammar), with a sidecar section for round-trip metadata (UIDs, geometry). Best human/AI readability and diffs; costs a parser and a spec.

## Leaning

Option 3 for the primary form, with a mechanical projection to/from PLCopen XML as the interchange proof (satisfying the vendor-neutral requirement without making XML the daily medium). Decide only after building against real S7-1200 G2 exports — the exports may reveal constraints that change the answer.

## Consequences

Owning a format means owning its parser, spec (`ir/SPEC.md`), and stability tests forever. The golden suite (08, Layer 1) is the enforcement mechanism.
