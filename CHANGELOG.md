# Changelog

Git doesn't generate this on its own — `git log` gives raw commit history, not a curated record
of what changed and why. This is that record, maintained by hand. Entries are grouped by date,
newest first, one bullet group per commit (or per uncommitted round of work, until committed).

For the detailed story behind any entry — the investigation, the evidence, the live-verification
results — see `docs/notes/stage-gates.md` (stage-gate status) and `docs/notes/openness-quirks.md`
(TIA/Openness findings). This doc is the short index; those are the record.

## 2026-07-10

**Fix converter data-loss bugs found via live TIA testing; add block-level compile support**

- Fixed: array-subscript addressing (`Node_Error[1]`, `[2]`, `[3]`) was silently dropped by the
  converter, collapsing distinct array elements into one ambiguous tag path in the IR — a real
  data-loss bug, caught by the project owner reviewing round-tripped LAD directly in TIA, not by
  a test. This was also the root cause of a `FlgNetBuilder` crash hit earlier in the same testing
  session (two genuinely different `<Access>` elements colliding on one ambiguous tag path).
  Ground truth for the fix was pulled from a real, untouched sibling block rather than guessed.
- Resolved: the project's `IsConsistent = false` after `Import()` mystery. `PlcBlock` exposes its
  own `ICompilable` service — confirmed live, not documented anywhere — distinct from the
  whole-device compile `openness-cli` already used. Device-level compile reports `Success` but
  never clears a block's `IsConsistent` flag after import; block-level compile does. Added
  `openness-cli compile --block <name>`. Used it to clear every remaining inconsistent block in
  the scratch project, including ones that predate this session entirely — same root cause.
  Project now reports `OVERALL: HEALTHY, 0/180 blocks inconsistent` for the first time.
- Stage 6 of the S1 walking skeleton (`SimaticML → IR → SimaticML'`, re-exported and compared
  against the original) reached end-to-end for the first time, on real production LAD data.

**Build S1 walking skeleton: converter, export/import/compile, sanity-check** (`36cbbcd`)

- Added the SimaticML↔IR converter (`src/converter/`, C# per ADR-0002), `openness-cli`
  `export`/`import`/`compile`, and golden-harness machinery (`tests/golden/`) for a
  Contact/Coil-only slice. Live-verified end-to-end against real project data.
- Found and fixed four real LAD patterns along the way (not guessed at): multiple independent
  rungs per network, a shared multi-endpoint rail wire, empty placeholder networks, and
  slice-access (bit-within-word) alarm addressing. Found three real `Import()` requirements the
  same way: a root block ID, unique comment-wrapper IDs, a required `Namespace` element.
- Added `openness-cli sanity-check` after the final re-export was refused with "inconsistent
  block" — diagnoses per-block `IsConsistent` + every device's compile state directly.

**Accept ADR-0001 and write `ir/SPEC.md` v1** (`206233e`)

- Decided the IR's concrete syntax against real S7-1200 G2 SimaticML structure: networks are
  wiring graphs, not flat rungs, so the IR uses a readable-expression form for the reducible
  common case with an explicit node/wire fallback otherwise. Block calls are reference-only;
  stateful instructions carry their instance inline; DBs/UDTs get a tabular sub-format.

**Implement `openness-cli list` and close out S0 exit criteria** (`0dcf0f7`)

- Added the first Openness touchpoint: `list` attaches to (or launches) TIA Portal, opens a
  project by name or path, and enumerates every block with a structural, never-open safety filter
  (`ProgrammingLanguage` `F_`-prefix). Targets `net48`, not `net8.0-windows` (`Siemens.Engineering.dll`
  needs a .NET-Framework-only `Assembly.Load` overload). All four S0 exit-criteria items evidenced.

**Repo Skeleton + Design Doc Suite** (`52731d2`)

- Initial commit: the full pre-design document suite (`docs/`), `CLAUDE.md`, repo skeleton.
