# Ladder-AI

AI-assisted Siemens LAD engineering via TIA Openness. Claude Code reads/writes a vendor-neutral IR; `openness-cli` is the only TIA touchpoint; a human reviews everything.

- Start here: `docs/00-README.md` (reading order for the design suite)
- Claude Code operating manual: `CLAUDE.md`
- Active stage: `docs/notes/stage-gates.md`

## Layout

```
CLAUDE.md                  Claude Code session instructions
docs/                      design suite (01–13, adr/, notes/)
src/openness-cli/          C# — the only Openness/TIA touchpoint
src/converter/             SimaticML ↔ IR (lossless, golden-tested)
ir/                        SPEC.md + exported blocks as IR, one file per block
simatic-ml/                raw XML exports (normalized), evidence trail
patterns/                  pattern library, one folder per pattern
extract/                   Python extractors (alarms, IO, xref) + templates
tests/golden/              reference project round-trip corpus
tools/                     AHK leftovers, one-off scripts
```
