# IR format specification

**Status: STUB — owned by Stage S1.** Design against real S7-1200 G2 SimaticML exports, not speculation (ADR-0001).

## Requirements (fixed now)

- Vendor-neutral, PLCopen-XML-compatible in semantics; no Siemens-ism leaks above the converter.
- Text-first, diff-friendly, readable by a controls engineer without a manual.
- One file per block. Suggested extension: `.ir`.
- Lossless round-trip: SimaticML → IR → SimaticML with no semantic diff (golden-tested).
- Round-trip metadata TIA needs but humans don't (UIDs, geometry) lives in a sidecar section, out of the reader's way.
- IR self-stability: parse → re-serialize is byte-identical.
- Unknown source elements are hard errors, never best-effort.

## To define in S1

- Concrete syntax (leaning: custom structured format with explicit network list, instruction graph, tag references — see ADR-0001)
- Network/rung/branch grammar; instruction table (vendor name ↔ neutral name)
- How tag tables, UDTs, and DBs are represented (same format or simpler tabular one)
- Sidecar metadata section format
- Comment/title placement
