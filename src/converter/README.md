# converter

SimaticML ↔ IR, bidirectional, lossless. C# (ADR-0002 revision — moved from the originally
planned Python once `openness-cli` made a .NET toolchain a hard requirement on this machine
anyway). Built in S1, `net8.0` (no `Siemens.Engineering` dependency, so unlike `openness-cli`
it isn't pinned to `net48`).

```
converter to-ir  <file>                                   # SimaticML → IR
converter to-xml <file>                                   # IR → SimaticML
converter sanitize <file> --map <mapping.json> --out <path>  # SimaticML → sanitized SimaticML
```

`to-ir`/`to-xml`/`sanitize` all auto-detect DB vs code-block content (root element name for XML
input, first line for IR/text input) and route accordingly — no separate flag needed.

## Current scope (walking skeleton)

Deliberately narrow — **Contact/Coil and basic tag references, plus OR-merge and negated
contacts** (S1 item 7 Phase A, 2026-07-11). No TON, MOVE, comparisons, or block calls yet.
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
  network** — surfaced live, 2026-07-10 (`NodeStatusAlarms`): `FlgNetBuilder` rebuilt each operand's
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
  Caught live, 2026-07-10, by the project owner reviewing the round-tripped `NodeStatusAlarms` block
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
- **OR-merge (`Part Name="O"`) and negated contacts (`<Negated Name="operand" />`) are real** —
  grounded 2026-07-11 against `PerimeterSafetyAlarms` (3-way OR of negated contacts) and `GeneralAlarms`
  (33-way OR of plain contacts). An OR-merge is a `Cardinality` `TemplateValue` plus `N` `inK`
  ports, each fed by exactly one Contact (optionally negated) wired directly to the rail — no
  branch confirmed real is itself a multi-contact chain, and no OR-merge confirmed real sits
  behind another element (it always terminates `GraphReducer`'s backward trace, the same way
  Powerrail itself does). IR notation: `NOT <tag>`, `<a> OR <b> OR NOT <c>` etc., same style as
  `AND`. A multi-contact branch or a nested OR-merge is refused (`NonReducibleNetworkException`),
  not guessed at — real-but-unconfirmed shapes.

**Live-verified end-to-end, 2026-07-10 and 2026-07-11**: `export → to-ir → to-xml → import →
compile` against a real 2-network, multi-assignment, slice-addressed block (`PerimeterSafetyAlarms`) —
import returned the block cleanly, compile reported `State=Success, Errors=0, Warnings=0`. The
outer block-export XML wrapper is no longer a guess — three requirements were found and fixed
one live-`Import()`-attempt at a time: the root block element needs its own `ID` attribute
(distinct from its `CompileUnit` children's IDs), every `MultilingualText`/`MultilingualTextItem`
comment wrapper needs a unique `ID` (synthetic IDs starting at 100,000 are used, since the
source values aren't captured during parsing and aren't believed to carry meaning beyond
uniqueness), and the block's `AttributeList` needs a `<Namespace />` element even when empty.
2026-07-11: the same block (now including its OR-merge/negated-contact content, added to the
real project since the first pass) taken all the way through the reference-project pipeline —
`sanitize → to-ir → to-xml → import → block-compile → re-export →
Normalizer.AreSemanticallyEquivalent` — **true**. Committed as `PerimeterSafetyAlarms`.
Full detail: `docs/notes/stage-gates.md` S1, `docs/notes/openness-quirks.md`.

The final re-export/comparison step of that live run didn't complete — blocked by a pre-existing
"inconsistent block" state on that scratch project unrelated to this converter (confirmed by
reproducing it on an untouched block); see `docs/notes/openness-quirks.md`.

Still open (not yet needed by this slice's fixtures, not guessed at): exact source attribute
for negated contacts, exact `Part Name` for an AND-merge, and full instance-DB / structured-member
support (see below — the shape is understood, just deferred). `DocumentInfo` (product/version
provenance in the wrapper) is confirmed present in real full-project exports but not required —
our own writer never emits one and `Import()` has never complained.

## DB support

Deliberately narrow, same discipline as the LAD side — **`Static` section only**, both
`SW.Blocks.GlobalDB` and `SW.Blocks.InstanceDB`, scalar/`Array[m..n] of <scalar>` members plus
structured members one level deep (S1 item 7 Phase B, 2026-07-11). Confirmed against three real
GlobalDB exports, 2026-07-10 (`CommsProcessData`, 11 members; `Alarms`, 4; `Input`, 47 — none had
structured content, so all three round-trip cleanly end to end):

- **DB kind is the root element name** (`SW.Blocks.GlobalDB` vs `SW.Blocks.InstanceDB`), not a
  field — corrects `ir/SPEC.md`'s original sketch, which had it as a `KIND` line.
- **Retention (`Remanence`) is per-member, not per-DB** — also corrects the original sketch.
  `RETAIN` appears on the IR member line, omitted when non-retentive.
- **`StartValue` is captured verbatim**, whatever literal syntax the source uses (`FALSE`, `2.0`,
  `16#0000`, `'text'`, `T#1H`) — never parsed/understood. One real DB (`ConveyorMotor1`, an instance
  DB, out of scope below) had a string start value that was a descriptive equipment name —
  confirms string values are a real sanitization surface, not hypothetical.
- **Every member's `AttributeList` (`ExternalAccessible`/`Visible`/`Writable`, `SetPoint`) was
  identical boilerplate** (`true/true/true/false`) on every scalar/array member across all three
  real DBs — `DbSourceParser` hard-errors if a member ever differs, rather than assume the pattern
  holds universally.
- **`Member`/`AttributeList`/`StartValue` inherit the `Interface` XML namespace** from the
  ancestor `<Sections xmlns="...">` rather than redeclaring it — a real bug caught immediately by
  testing against `CommsProcessData`: looking them up as unnamespaced elements (`Element("StartValue")`)
  silently returned null for every one, making every `BooleanAttribute` read back as "absent."
  Fixed by searching by `LocalName` (parse side) and explicitly namespacing every written element
  (write side), matching the rest of this parser's existing discipline.

**Instance DBs and structured members (S1 item 7 Phase B, 2026-07-11).** `SW.Blocks.InstanceDB`
carries `InstanceOfName`/`InstanceOfType` — `InstanceOfType` isn't an IR field, since every real
instance DB seen has `Type="FB"` (the writer regenerates that constant; the parser hard-errors if
a source ever disagrees). Structured members (UDT-typed, e.g. `"TypeDOL"`; or
system-function-block instance-typed, e.g. `TON_TIME` with a `Version` attribute) **inline** their
nested sub-members directly in the IR, one level deep, never referencing a separately-defined UDT
by name — a deliberate choice (project owner's call, 2026-07-11), not a limitation: the source DB
XML already contains the full nested shape at the point of declaration, so inlining round-trips
with zero new converter capability, where reference-by-name would need real UDT/`PlcType` export
support that doesn't exist yet (`ir/SPEC.md` "Structured members" has the full tradeoff). Nested
members are structurally minimal — only `Name`/`Datatype` and an optional `StartValue`, no
`Remanence`/`Accessibility`/`AttributeList` — confirmed real against `ConveyorMotor1` (an instance of
`FB MotorDOL`: a `"TypeDOL"`-typed member with 29 scalar sub-members, several `TON_TIME`/etc.
timer members with `PT`/`ET`/`IN`/`Q`).

**Still hard error (design philosophy #10):** a doubly-nested structured member (a nested member
that is itself structured); a nested `Section` named anything but `"None"`; any non-`Static`
top-level Interface section with content.

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
