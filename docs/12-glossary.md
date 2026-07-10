# 12 — Glossary

| Term | Meaning in this project |
|------|------------------------|
| LAD | Ladder logic, the only permitted PLC language for deliverables |
| TIA Portal | Siemens engineering environment (V20 here) |
| Openness | TIA Portal's .NET automation API — our only programmatic door into TIA |
| SimaticML | Siemens XML format produced/consumed by Openness block export/import; wire format only, never hand-edited |
| PLCopen XML | Vendor-neutral IEC 61131-3 exchange standard (TC6); compatibility target for the IR's semantics |
| IR | Intermediate Representation — this project's vendor-neutral, diff-friendly text form of LAD; the medium AI and humans share |
| openness-cli | The C# CLI wrapping Openness (list/export/import/compile/xref); the only component that touches TIA |
| Converter | The lossless SimaticML ↔ IR translator |
| Golden test | Round-trip test against committed reference exports; any diff after normalization is a failure |
| Normalization | Canonicalizing volatile SimaticML attributes (UIDs, ordering, geometry) so diffs show only semantic change |
| Compile gate | Mandatory import+compile in TIA before any AI output counts as done (Goal 7) |
| Pattern | A proven, parameterized LAD fragment in the library (`07-pattern-library-spec.md`) |
| Slot | A pattern parameter bound to a real project tag at instantiation |
| Freeform | AI-invented rungs not composed from patterns; opt-in, reviewed hardest |
| Reference project | The TIA project used as test corpus; grows as new constructs appear |
| Stage gate | Review of a roadmap stage's exit criteria before the next stage starts |
| OB / FB / FC / DB / iDB | Organization Block / Function Block / Function / Data Block / instance DB |
| UDT | User-defined data type (PLC data type) |
| F-block / F-runtime | Fail-safe (safety) logic — permanently excluded from the pipeline |
| PLCSIM / PLCSIM Advanced | Siemens PLC simulators; S9 test target (see risk R-07 for the 1200-support question) |
| S7-1200 G2 | Second-generation S7-1200 CPU family, primary hardware target, TIA V20+ |
| ADR | Architecture Decision Record (`docs/adr/`) |
| CLAUDE.md | Repo-root instruction file Claude Code reads at session start — the distilled operating manual |
