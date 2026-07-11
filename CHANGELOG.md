# Changelog

Git doesn't generate this on its own — `git log` gives raw commit history, not a curated record
of what changed and why. This is that record, maintained by hand. Entries are grouped by date,
newest first, one bullet group per commit (or per uncommitted round of work, until committed).

For the detailed story behind any entry — the investigation, the evidence, the live-verification
results — see `docs/notes/stage-gates.md` (stage-gate status) and `docs/notes/openness-quirks.md`
(TIA/Openness findings). This doc is the short index; those are the record.

## 2026-07-11

**S1 item 9: comparisons (Eq/Ge)**

- Added `Part Name="Eq"`/`"Ge"` support, grounded against a real export (`FC ControlDelays`)
  before building anything. Real finding: a comparison behaves like a `Contact` (a pass-through
  chain position with its own rail-facing/continuation port, `pre`), not a terminal leaf like an
  OR-merge or TON — `GraphReducer.TraceChain`'s upstream dispatch now varies both the "out"- and
  "in"-equivalent port names by part kind (previously only the "out" side varied, since TON).
- New top-level `Access Scope="LiteralConstant"` (a comparison's literal operand, e.g. `1`) —
  always carries a `<ConstantType>`, the mirror image of TON's `TypedConstant` (never has one).
  `ConstantAccessNode`/`SidecarConstantEntry` gained a nullable `ConstantType` field;
  `Expr.TimeLiteral` generalized to `Expr.Literal` (both literal kinds render identically in text).
- Scope decision made during implementation: `ControlDelays`' own network composes two
  comparisons via a second OR-merge, each fed by a first OR-merge rather than Powerrail directly.
  Rather than build a larger, riskier OR-merge-branches-as-recursive-chains generalization this
  session, confirmed the *existing* OR-merge branch/fan-out checks already safely refuse this
  shape without new code — verified with a dedicated test against the real shape, not assumed.
  Same deferred status as the already-known multi-contact-OR-branch/nested-OR-merge cases.
- 11 new converter tests, 1 repurposed (`LiteralConstant` was the old "unrecognized scope"
  example — now real, moved to a scope name that's still genuinely unsupported). 94 converter /
  68 openness-cli / 11 golden-harness tests all pass. Not yet live-round-trip-proven — `Mul`/
  `Convert` still needed for `ControlDelays` as a whole, and a live re-check was blocked by TIA
  Portal session state (not retried, per this project's "don't kill and retry" discipline).

**S1 item 8, closed out: TON live round trip, direct-`Q`-wiring support, a second Normalizer fix**

- Project owner built `FC TimerSample`/`DB_Timers` directly in the reference project
  specifically to close out TON — a TON-only block, free of the comparisons/Move/RCoil that
  blocked every prior grounding example from a full live round trip. Confirmed a second real
  `Instance Scope="GlobalVariable"` shape along the way: a two-component path
  (`DB_Timers.SampleTimerN`), needing no code change.
- Built `ChainStepSidecar.TimerOutputStep`: a TON's `Q` wired *directly* into a downstream `Coil`
  (no ordinary Access in between) — the one shape left unmodeled after `FB MotorDOL`'s only
  example fed an out-of-scope `RCoil`. Always chain-terminal, like an OR-merge, except it never
  touches Powerrail — `RailWireUId` is now nullable (`none` in the IR sidecar) for this case.
- Live round trip passed: `export → to-ir → to-xml → import → compile → re-export →
  Normalizer.AreSemanticallyEquivalent` — **true**, first full live proof for TON (3 networks,
  including chained timers — one network's `IN` reads back two other TONs' `Q`, exercised for
  free). Surfaced and fixed a real, second golden-harness gap: TIA reassigns `Access` element
  UIds on its own import/compile cycle too (parallel to the already-known Wire UId volatility) —
  `Normalizer` now resolves Access/IdentCon references by content, scoped per network (a
  document-wide map would silently collide entries across networks, since UId numbering restarts
  per network — caught live by this exact 3-network test).
- Net +3 converter tests versus the entry below (83 total: repurposed an obsolete hard-error test
  into a positive one, added 4 new). `tests/golden`'s `NormalizerTests` unchanged (11, all still
  pass) plus the live proof itself.
- Committed `TimerSample`/`DB_Timers` to the reference corpus (6th/7th artifacts) — re-verified
  against the exact committed files before committing, not assumed from the earlier scratchpad run.

**S1 item 8: TON support, both instance scopes**

- Added `Part Name="TON"` support, grounded against two real exports at the user's request to
  confirm both instance scopes before building anything: `FB MotorDOL` (`Instance
  Scope="LocalVariable"`, multi-instance in the calling FB's own iDB) and `FC ControlDelays`
  (`Instance Scope="GlobalVariable"`, a standalone instance DB named directly). Both reuse the
  existing `AccessNode` model rather than a new type, so a future FC/FB call's own instance
  argument can reuse it too.
- IR design, agreed with the user before coding: no bound name (`ir/SPEC.md`'s original `timer :=
  TON(...)` sketch) — TIA has no timer-name concept to draw from, so a TON statement is
  `TON(<instance path>, IN := <expr>, PT := <expr>)`, and later reads of its output reuse the
  instance's own dotted path as a plain tag reference (e.g. `GeneralEnableDelay.Q`) — not a new
  IR construct, since that's exactly how the source itself reads a standalone TON's output back.
- New `Access Scope="TypedConstant"` for a literal-fed `PT` (`T#100MS`); `Q`/`ET` confirmed valid
  both entirely unwired and `OpenCon`-wired — a `Q`/`ET` wired directly to a downstream part is a
  real shape (`FB MotorDOL`, feeding an out-of-scope `RCoil`) but not modeled, since it can't be
  proven end to end regardless of how much is built for it.
- Fixed a real, pre-existing bug found while grounding PT-as-tag: `FlgNetBuilder` rebuilt every
  ordinary tag Access as hardcoded `GlobalVariable`, ignoring the real source scope — harmless
  until now (every tag seen before was GlobalVariable), now fixed to carry the real scope.
- 12 new converter tests, 1 obsolete one removed; 80 converter / 68 openness-cli / 11
  golden-harness tests all pass. Not yet live-round-trip-proven at the block level — both
  grounding blocks also use comparisons/Move, a separate unbuilt capability, and `to-ir` requires
  a whole block in scope at once.

**S1 item 7 Phase A: OR-merge + negated contacts, live-proven in the reference project**

- Added `Expr.Not` to the IR (`NOT <tag>` notation) and generalized `GraphReducer`'s backward
  trace to recognize `Part Name="O"` (OR-merge) as a valid rail-facing chain position — grounded
  against two fresh real exports (`PerimeterSafetyAlarms`: 3-way OR of negated contacts; `GeneralAlarms`:
  33-way OR of plain contacts), both confirming every OR-merge branch is exactly one Contact
  (optionally negated via `<Negated Name="operand" />`) fed directly by the shared rail wire.
  Rebuilt `CoilAssignmentSidecar`'s flat contact/wire lists as a recursive `ChainStepSidecar`
  (`ContactStep`/`OrStep`) to represent a chain position that fans out. A multi-contact OR branch
  or a nested OR-merge — both real but unconfirmed — hard-error rather than guess.
- Live proof: `PerimeterSafetyAlarms` (8 independent rungs — the 3-way negated OR plus 7 more
  single-contact rungs, 4 negated) taken through `sanitize → to-ir → to-xml → import →
  block-compile → re-export → Normalizer.AreSemanticallyEquivalent` — **true**, the first real
  confirmation that OR-merge/negated-contact SimaticML output actually imports and compiles in
  TIA. Committed as `PerimeterSafetyAlarms`, the reference project's second FC block — every tag
  it needed was already in the sanitization map from the earlier 12-block search that found this
  gap in the first place.
- 10 new converter tests (45 total), all three suites still green.

**S1 item 7 Phase B: Instance DB + one-level structured members**

- Added `SW.Blocks.InstanceDB` support (`InstanceOfName`/`InstanceOfType`, the latter regenerated
  as a constant rather than carried as an IR field) and one-level structured-member round-trip
  for both Global and Instance DBs — UDT-typed (e.g. `"TypeDOL"`) and system-function-block
  instance-typed (e.g. `TON_TIME`, `PT`/`ET`/`IN`/`Q`) members are inlined directly in the IR
  rather than referenced by name, a deliberate call (`ir/SPEC.md` "Structured members") made
  because there's no UDT/`PlcType` export capability yet to safely derive a canonical
  reference-by-name shape from. Grounded against a real `ConveyorMotor1` instance DB (`FB MotorDOL`).
- New source files `DbMemberLineFormat.cs`/`DbInterfaceMembers.cs`; new fixtures for
  Instance DBs with structured members, UDT-typed and doubly-nested members (hard-error case),
  non-`None` nested sections (hard-error case), and non-empty FB interfaces; new
  `BlockInterfaceTests.cs` plus 263 new lines in `DbConverterTests.cs`.
- Doubly-nested structured members and non-`None` nested sections remain hard errors
  (real-but-unconfirmed shapes).
- Verified 2026-07-11 (during a docs audit that flagged this phase as undocumented): 68 converter
  tests (up from 45), 68 openness-cli tests, 11 golden-harness tests, all green.

## 2026-07-10

**Seed the reference project: sanitizer, DB round-trip support, auto project-switching**

- Added `converter sanitize` — a new subcommand that renames every identifying value in a
  parsed block/DB via a hand-authored, never-committed mapping file (`docs/13-data-boundary.md`),
  hard-erroring on anything left unmapped. Reuses the existing parse/write pipeline; the
  transform is just a rename pass on the parsed model.
- Seeded S1's purpose-built Green reference project (`ir/reference/`, `simatic-ml/reference/`) —
  didn't exist before this; everything proven so far ran against `JOB9002`, explicitly barred from
  becoming the committed corpus. One FC (`NodeStatusAlarms`) plus three complete DBs
  (`CommsProcessData`/11 members, `AlarmWords`/4, `EquipmentStatus`/47) are now committed, each
  proven through a real `export → sanitize → to-ir → to-xml → import → compile → re-export`
  cycle ending in `Normalizer.AreSemanticallyEquivalent` — the actual Layer 1 losslessness
  assertion, not a manual spot-check — returning true.
- Built real DB round-trip support (`DbSourceParser`/`DbSourceWriter`/DB IR format) — the two
  DBs the FC depends on were hand-made placeholders until now. Found and fixed three real bugs
  along the way: `Member`/`AttributeList`/`StartValue` XML-namespace inheritance silently
  dropping every attribute on parse and write; a structural-section scan that wrongly recursed
  into a structured member's own nested content; and a blanket `Normalizer` strip rule that
  would have made every DB round-trip trivially pass without comparing real member content —
  correct for code blocks, wrong for DBs, now a structural check instead of a name match.
- Fixed a second real bug independent of DB work: `openness-cli compile`'s diagnostic messages
  were silently incomplete (a nested Siemens API message tree only ever read one level deep) —
  every past compile failure this session showed a bare error count with no explanation.
- Added automatic TIA project-switching to `openness-cli` — every session up to this point
  required manually closing whichever project was open before touching a different one; now it
  saves and closes automatically (`Project.Close()`/`Save()`, confirmed to leave Portal itself
  running).
- Searched 12 real blocks for a second FC to bring into the reference project; none fit without
  either an unsupported instruction (OR-merge, comparisons) or a cascade of new Instance-DB
  dependencies — a real finding about this codebase's converter-scope gaps, not a dead end.

**Fix converter data-loss bugs found via live TIA testing; add block-level compile support** (`d3cb494`)

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
