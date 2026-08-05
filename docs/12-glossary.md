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
| Sidecar | The IR's machine-owned layer (UIds, wire identity, scopes, some type attributes) beneath the readable body; **derived** on demand rather than stored, since ADR-0005 |
| Preflight | `converter preflight` — static parse/convert/tag/call/review checks run before any import; a filter *before* the compile gate, never a substitute for it (hard rule 4) |
| Telemetry | One line appended to `gen/<project>/telemetry.log` when a pipeline stage run ends (`notes/gen-telemetry.md`); append-only, never backfilled |
| Spec pipeline / rungs A–D | The four staged spec skills: **A** `gen-pid-analysis` (topology + interlocks) → **B** `gen-functional-analysis` (plant behaviours) → **C** `gen-equipment-spec` (signals enter here) → **D** `gen-code-structure` (booleans and interface members enter here). Runs alongside `gen-architecture`, not instead of it |
| Mechanical floor | The converter checks that fire whether or not an agent chooses to look — `candidate-scan`, `undriven-scan`, `relation-reconcile`, `signal-sweep`, and `trace`'s guard-containment hop (FI-36/FI-39) |
| Green / Amber / Red tier | Data classes in `13-data-boundary.md`. **Green**: this doc suite, tooling, IR spec, patterns, the purpose-built reference project — usable freely. **Amber**: real project logic carrying identifying data — sanitized, or under an explicit per-project approval. **Red**: confidentiality-restricted (never in committed repo content) or safety content (never anywhere, hard rule 2) |
| Live Runs | `Live Runs/` — live engineering jobs worked end-to-end, gitignored. Full unsanitized working access, including confidentiality-Red material; nothing from it is ever committed or enters the knowledge base. The boundary is **retention, not access** (`13-data-boundary.md`) |
| `lad-coder` | The sub-agent that performs *all* LAD/IR work — writing/editing `.ir`, the convert/import/compile loop, the review and explanation reads, `patterns/` edits. Hard rule 8: never done inline, no exception for size |
| Skill | A packaged procedure in `.claude/skills/<name>/SKILL.md` an agent follows for one pipeline stage; the artifact formats a skill defines are contracts the converter parses, not suggestions |
| FI item | A numbered candidate idea in `16-future-ideas.md` (FI-nn) — merits, costs, verdict, status; promotion into the plan is ADR-gated |
