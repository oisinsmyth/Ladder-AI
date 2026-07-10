# 05 — Architecture & Project Structure

## Pipeline

```
TIA Portal V20 (project)
      │  ▲
      │  │  TIA Openness API (C# openness-cli)
      ▼  │
SimaticML XML  (vendor wire format — never hand-edited)
      │  ▲
      │  │  converter (SimaticML ↔ IR), lossless, golden-tested
      ▼  │
IR — vendor-neutral text representation of LAD
      │  ▲
      │  │  Claude Code: read / explain / comment / review / extract / generate / modify
      ▼  │
Human review (diff) ──► import via openness-cli ──► compile gate ──► TIA project
                                                        │
                                              (S9) sim harness / PLCSIM
```

Human download to hardware happens in TIA Portal, outside every tool in this repo.

## Components

- **openness-cli (C#)** — the only component that talks to TIA. Subcommands: `list`, `export`, `import`, `compile`, `xref`. Plain-text/JSON output, non-zero exit codes on failure, so Claude Code can drive it from the shell. Refuses any block identified as safety (F-) — see principle 9.
- **converter** — SimaticML ↔ IR, bidirectional, lossless. Unknown elements are hard errors (principle 10). Round-trip metadata that TIA needs but humans don't (UIDs, geometry) is preserved in a sidecar section of the IR, out of the reader's way.
- **IR (intermediate representation)** — vendor-neutral, PLCopen-XML-compatible in semantics, text-first and diff-friendly in form. One file per block. Exact format defined in `ir/SPEC.md` (owned by Stage S1; a likely shape is a structured text format with an explicit network list, instruction graph, and tag references — design it against real exports, not speculation).
- **pattern library** — proven LAD fragments with parameter slots, metadata, and tests (`07-pattern-library-spec.md`).
- **extractors (Python)** — IR → alarm lists, IO usage, cross-references (CSV/XLSX).
- **sim harness (S9)** — drives PLCSIM to run AI-generated test sequences.
- **Claude Code + CLAUDE.md + skills** — the AI layer. All PLC knowledge it needs at session start lives in `CLAUDE.md` and the docs it points to.

## Repository layout

```
ladder-ai/
├── CLAUDE.md                  # Claude Code session instructions
├── docs/                      # this document suite (01–13, adr/, notes/)
├── src/
│   ├── openness-cli/          # C# — the only Openness/TIA touchpoint
│   └── converter/             # SimaticML ↔ IR
├── ir/
│   ├── SPEC.md                # IR format definition
│   └── <project>/             # exported blocks as IR, one file per block
├── simatic-ml/<project>/      # raw XML exports (normalized), evidence trail
├── patterns/                  # pattern library, one folder per pattern
├── extract/                   # extractor scripts + output templates
├── tests/
│   ├── golden/                # reference project round-trip corpus
│   └── ...
└── tools/                     # AHK leftovers, one-off scripts
```

## Data flow rules

1. TIA is only ever touched through `openness-cli`.
2. AI reads and writes IR (and docs/patterns/code) — never raw SimaticML, never the TIA project directly.
3. Every write path ends at the compile gate before human presentation.
4. Safety blocks are filtered out at export, the earliest possible point.

## Portability strategy

The IR and everything above it never references Siemens-specific concepts without an abstraction (e.g. instruction names map through an instruction table). Adding Rockwell/Beckhoff/Codesys later = new converter + instruction table entries. The acceptance test for "vendor-neutral enough": could this IR file describe the same logic exported from a different vendor's tool? If a Siemens-ism leaks above the converter, that's a bug.

## Design questions resolved in S1

- IR concrete syntax: custom structured text — readable expression form for reducible
  series/parallel logic, explicit node/wire fallback per-network otherwise. Decided in
  ADR-0001, spec'd in `ir/SPEC.md`.
- Tag tables/UDTs/DBs: a simpler tabular sub-format, not the network-graph form — they carry
  typed members with no wiring, structurally distinct from code blocks. `ir/SPEC.md`.
- Layout/geometry preservation: deliberately left open, pending an empirical answer from the
  golden-file harness (S1 item 6) on whether TIA's own re-layout-on-import is sufficient.
