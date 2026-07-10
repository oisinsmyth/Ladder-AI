# converter

SimaticML ↔ IR, bidirectional, lossless. C# (ADR-0002 revision — moved from the originally
planned Python once `openness-cli` made a .NET toolchain a hard requirement on this machine
anyway). Built in S1, `net8.0` (no `Siemens.Engineering` dependency, so unlike `openness-cli`
it isn't pinned to `net48`).

```
converter to-ir  <file>    # SimaticML → IR
converter to-xml <file>    # IR → SimaticML
```

## Current scope (walking skeleton)

Deliberately narrow — **Contact/Coil and basic tag references only**. No TON, MOVE,
comparisons, block calls, or OR-merge instructions yet (overall S1 plan item 7 grows this).
Anything outside scope is a hard error (`UnsupportedConstructException`), never a silent
partial result — hitting that error on a real block is expected at this stage, not a bug.

Confirmed against real exports (`JOB9002 - Tom White Waste`, under the data-boundary approval in
`docs/13-data-boundary.md`) and handled explicitly, not guessed at:

- **A network can bundle multiple independent Contact-chain-into-Coil rungs** with no wiring
  between them (e.g. a 16-independent-rung alarm-bit network) — represented as multiple `COIL`
  assignments per `NETWORK` in the IR.
- **The rail connection is commonly one shared wire** powering every chain's first element at
  once (many endpoints), not one wire per chain — this is not fan-out that breaks
  series-purity, and is tracked separately from each chain's own private wires
  (`CoilAssignmentSidecar.RailWireUId`).
- **Empty networks are real** — `<NetworkSource />` with no `FlgNet` content at all (seen as a
  trailing/placeholder network). Represented as a network with zero assignments
  (`NETWORK n "title" [empty]` in the IR), not a hard error, since there is unambiguously
  nothing there to represent.
- **Slice access (bit-within-word) addressing is real** — `06-lad-conventions.md` C-501's
  documented alarm-bit convention (`DB_Alarms.EStopAlarm0.%X3`), seen as
  `SliceAccessModifier="x15"` on an Access's last `<Component>`. Preserved in the IR using the
  same `.%X15` notation the site already uses, not a converter-invented one.
- **The same tag path can appear on two distinct `<Access>` elements (different UIds) within one
  network** — surfaced live, 2026-07-11 (`NodeStatusAlarms`): `FlgNetBuilder` rebuilt each operand's
  source Access UId via a `TagPath`-keyed dictionary, which crashed (`ArgumentException`,
  duplicate key) the first time this shape appeared. Fixed by threading the exact source Access
  UId positionally instead — `CoilAssignmentSidecar.ContactOperandAccessUIds`/
  `CoilOperandAccessUId`, captured once by `GraphReducer` at reduce time — so rebuild never needs
  to look an Access UId up by tag path at all. (This particular collision turned out to have a
  more specific cause — see array-index addressing below — but the fix is correct regardless of
  *why* two Access elements ever share a tag path.)
- **Array-subscript addressing is real, and was being silently dropped — a genuine data-loss bug,
  not just the crash above.** `"CommsProcessData".Node_Error` is a `BOOL` array; three separate
  elements (`Node_Error[1]`, `[2]`, `[3]`) feed three independent alarm bits. The converter had no
  concept of array indexing, so all three collapsed to the identical `CommsProcessData.Node_Error`
  tag path in the IR — indistinguishable to a reviewing engineer, and *also* the direct cause of
  the `FlgNetBuilder` crash above (three genuinely different Access elements, one ambiguous path).
  Caught live, 2026-07-11, by the project owner reviewing the round-tripped `NodeStatusAlarms` block
  directly in TIA and noticing the array index was gone — not by a test. Ground truth for the fix
  was pulled from a real, untouched sibling block (`station_1/JOB9001_PLC/Alrams/NodeStatusAlarms`,
  never imported into) rather than guessed: `<Component Name="Node_Error" AccessModifier="Array">
  <Access Scope="LiteralConstant"><Constant><ConstantType>DInt</ConstantType>
  <ConstantValue>n</ConstantValue></Constant></Access></Component>`. Fixed by adding
  `AccessNode.ArrayIndex` (only a literal-constant `DInt` index is supported — anything else is a
  hard error, not a guess), IR notation `Node_Error[n]`, seen only on the last `Component` (same
  precedent as `SliceAccessModifier`). Verified against the untouched sibling block: `to-ir` now
  shows 15 distinct `Node_Error[0..14]` tag paths, and `to-ir → to-xml` regenerates the exact
  source XML shape with the correct index per element.

**Live-verified end-to-end, 2026-07-11**: `export → to-ir → to-xml → import → compile` against a
real 2-network, multi-assignment, slice-addressed block (`PerimeterSafetyAlarms`) — import returned the
block cleanly, compile reported `State=Success, Errors=0, Warnings=0` on two separate runs. The
outer block-export XML wrapper is no longer a guess — three requirements were found and fixed
one live-`Import()`-attempt at a time: the root block element needs its own `ID` attribute
(distinct from its `CompileUnit` children's IDs), every `MultilingualText`/`MultilingualTextItem`
comment wrapper needs a unique `ID` (synthetic IDs starting at 100,000 are used, since the
source values aren't captured during parsing and aren't believed to carry meaning beyond
uniqueness), and the block's `AttributeList` needs a `<Namespace />` element even when empty.
Full detail: `docs/notes/stage-gates.md` S1, `docs/notes/openness-quirks.md`.

The final re-export/comparison step of that live run didn't complete — blocked by a pre-existing
"inconsistent block" state on that scratch project unrelated to this converter (confirmed by
reproducing it on an untouched block); see `docs/notes/openness-quirks.md`.

Still open (not yet needed by this slice's fixtures, not guessed at): exact source attribute
for negated contacts, exact `Part Name` for an AND-merge, instance-DB representation detail,
and whether `DocumentInfo` (product/version provenance in the wrapper) is actually required by
`Import()` or was just never tested without it.

## Rules (docs/05-architecture.md, 04 §8/§10)

- Unknown elements are hard errors, never warnings or best-effort.
- Volatile attributes (UIDs, ordering, geometry) preserved in the IR sidecar — machine-owned,
  never hand-edited; the golden harness normalizer (`tests/golden/`) canonicalizes what's left
  (things TIA itself regenerates regardless of input) for diffing, each rule documented with an
  example.
- Every change reruns the golden round-trip suite (`tests/golden/`).
- Refuses safety (F-) content — inherited for free at this scope, since `openness-cli export`
  already refuses to export a safety-classified block in the first place.

## Build & test

```
dotnet build   # from this directory (converter.sln)
dotnet test
```
