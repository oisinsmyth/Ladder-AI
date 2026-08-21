# converter

SimaticML ↔ IR, bidirectional, lossless. C# (ADR-0002 revision — moved from the originally
planned Python once `openness-cli` made a .NET toolchain a hard requirement on this machine
anyway). Built in S1, `net8.0` (no `Siemens.Engineering` dependency, so unlike `openness-cli`
it isn't pinned to `net48`).

```
converter to-ir  <file>                                   # SimaticML → IR (keeps the stored SIDECAR)
converter to-ir  <file> --no-sidecar                      # SimaticML → readable-only IR — omits the SIDECAR, but only after VERIFYING the block is derivable; errors otherwise
converter to-xml <file>                                   # IR → SimaticML
converter to-xml <file> --synthesize                      # IR (no SIDECAR needed) → SimaticML — see "Sidecar synthesis" below
converter sanitize <file> --map <mapping.json> --out <path>  # SimaticML → sanitized SimaticML
converter diff <old.ir> <new.ir> [--only <network> ...] [--json]  # which networks changed, rest provably identical (S7 invariance)
```

`to-ir`/`to-xml`/`sanitize` all auto-detect DB vs code-block content (root element name for XML
input, first line for IR/text input) and route accordingly — no separate flag needed.

## Current scope

*(Corrected 2026-08-05. This section read "walking skeleton" and denied arithmetic and FC/FB
parameter-interface support long after both shipped — contradicted by its own dated sections below.
The authoritative list is code, not prose: `Converter/SimaticMl/FlgNetParser.cs`'s
`SupportedPartNames` set — re-read it before editing this paragraph.)*

**35 supported part names**, each added against a real export rather than guessed at:

`Contact`, `Coil`, `SCoil`, `RCoil`, `O` (OR-merge, recursive-chain branches), `Not` (standalone
boolean inverter), `And` (bitwise word AND — "WAND"); the full IEC comparison family
`Eq`/`Ne`/`Gt`/`Ge`/`Lt`/`Le`; timers `TON`/`TONR`/`TOF`; `Move` and `MOVE_BLK_VARIANT`; arithmetic
`Add`/`Sub`/`Mul`/`Div`; `Convert`, `Calc`, `Abs`, `Swap`, `LIMIT`, `T_SUB`, `T_CONV`, `WAIT`,
`FillBlockI`, `Modbus_Master`, `Modbus_Comm_Load`.

Plus the **fixed-shape registry** names (`FixedShapeInstructions`, 2026-08-12), which
`SupportedPartNames` concatenates rather than restating: `MB_COMM_LOAD` 2.1, `MB_MASTER` 2.2 and
`MB_SERVER` 5.3 — keyed on **(name, version)**, so an unknown version of a known name is refused
rather than templated with the wrong port list. See the dated section below.

Supported alongside that set, not part of it: FB/FC **`CALL`** (a `<Call>` sibling of `<Part>` in
the source XML, normalized internally to `PartNode(Name: "Call")`); negated operands; network- and
block-level `Title`; **FC/FB parameter-interface modeling** (`Input`/`Output`/`InOut`/`Constant`
sections); slice (`.%X15`) and array-index (`[n]`) addressing; per-node fan-out markers
(`{split N}`/`{recv N}`, ADR-0006); and whole-document support for DBs, UDTs and PLC tag tables.

Anything outside that is a hard error (`UnsupportedConstructException`), never a silent partial
result — a correct refusal, not a bug. Per-construct grounding evidence, naming the real block each
was confirmed against, is in `docs/evidence/stage-S1.md`; each construct also has its own dated
section below.

**Conversion scope is not synthesis scope.** `LIMIT`, `WAIT`, `FillBlockI`, `Modbus_Master`,
`Modbus_Comm_Load` and the fixed-shape registry family convert in both directions when a real sidecar is present, but are **not
sidecar-synthesizable** — they hard-error (`UnsupportedSynthesisConstructException`) on the
`--synthesize`/derive-always path, as do InOut `CALL` parameters. See "Sidecar synthesis" below for
the current synthesizable subset.

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
  ports, each fed by a branch. IR notation: `NOT <tag>`, `<a> OR <b> OR NOT <c>` etc., same style
  as `AND`. **Each branch is now resolved as an ordinary chain (S1 item 11, 2026-07-11/12)** —
  see its own section below for the multi-contact-branch/nested-OR-merge/comparison-as-branch
  generalization; this bullet's earlier framing (every branch a single rail-fed Contact) was the
  starting point, not the current state.

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

Still open (not yet needed by this slice's fixtures, not guessed at): exact `Part Name` for an
AND-merge. `DocumentInfo` (product/version provenance in the wrapper) is confirmed present in
real full-project exports but not required — our own writer never emits one and `Import()` has
never complained.

## TON support (S1 item 8, 2026-07-11)

`Part Name="TON"` — grounded against three real exports: `FB MotorDOL` (`Instance
Scope="LocalVariable"`, a multi-instance timer living inside the calling FB's own static
interface — the same `TON_TIME` structured-member shape Phase B built DB-side support for),
`FC ControlDelays` (`Instance Scope="GlobalVariable"`, a standalone instance DB named directly by
a single `<Component>`), and `FC TimerSample` (purpose-built by the project owner in the
reference project specifically to close this out: `Instance Scope="GlobalVariable"` with a
**two**-component path — `DB_Timers.SampleTimerN`, a named member inside a shared standalone-timer
DB — and `Q` wired *directly* into a plain `Coil`). All scopes/shapes are modeled via the existing
`AccessNode` (not a new type) — the `<Instance>` element carries exactly the same data shape
(scope + component path) as an ordinary `<Access>`, just without the `<Symbol>` wrapper; this also
means a future FC/FB call's own instance argument can reuse it.

- `IN` reduces via the same chain-trace mechanism as a Coil's condition (refactored into a
  shared `TraceChain`), terminating at the TON's own `IN` port instead of a Coil's `in`.
- `PT` is a single IdentCon-fed operand — either a tag (`GlobalVariable`/`LocalVariable`,
  confirmed both real) or a literal time constant via a new `Access Scope="TypedConstant"`
  (`<Constant><ConstantValue>T#100MS</ConstantValue></Constant>`, no `<ConstantType>` child —
  distinct from the array-index `LiteralConstant` scope, which always has one).
- `Q`/`ET` are optional output ports. Confirmed real, all handled: entirely absent from `<Wires>`
  (`FC ControlDelays`' `Q`, read back via an *ordinary* Access elsewhere — already-existing
  Contact/Access machinery, no new mechanism needed); wired to `OpenCon` (`FB MotorDOL`'s `ET`);
  and wired *directly* into a downstream part — `FC TimerSample`'s `Q` feeding a plain `Coil`,
  modeled as `ChainStepSidecar.TimerOutputStep` (always chain-terminal, like an OR-merge, except
  it never touches Powerrail — the owning chain's `RailWireUId` is nullable, `none` in this case).
  `ET` wired directly to a consumer has no live example and is still refused.
- **Fixed alongside this:** `FlgNetBuilder` previously rebuilt every ordinary tag Access as
  hardcoded `Scope="GlobalVariable"`, harmless until now (every tag seen so far happened to be
  GlobalVariable) but wrong in general — `SidecarAccessEntry` now carries the real source scope.

**Live-round-trip-proven, 2026-07-11.** `FC TimerSample` (3 networks, including one where the
`IN` reads back two other TONs' `Q` outputs — chained timers, unprompted but exercised for free)
ran the complete `export → to-ir → to-xml → import → compile → re-export → Normalizer` cycle
against the reference project and passed. This also surfaced and fixed a real golden-harness bug
(TIA reassigns `Access` UIds on its own import/compile cycle, same as the already-known Wire UId
volatility) — see `tests/golden/README.md`. All instance scopes/shapes, both PT kinds, and both
`Q`-consumption paths are covered by unit tests built directly from the real exports (fixtures,
not guessed) — see `Converter.Tests/TonTests.cs`.

## Comparisons (S1 item 9, 2026-07-11)

`Part Name="Eq"`/`"Ge"` — grounded against a real export, `FC ControlDelays`. Only these two are
confirmed real; `Ne`/`Le`/`Gt`/`Lt` Part Names are unconfirmed (same status as AND-merge, refused
rather than guessed at).

- **Key finding: a comparison behaves like a `Contact`, not an OR-merge or TON.** It's a
  pass-through chain position — its own rail-facing/continuation port is `pre` (genuinely
  different from a Contact's `in`, not a typo), so after resolving its own two operands the
  backward trace continues from `(comparePartUId, "pre")` exactly as it would from a Contact's
  `in`. `GraphReducer.TraceChain`'s upstream dispatch table now varies **both** the "out"-
  equivalent port (already varied per part kind since TON) and the "in"-equivalent continuation
  port (new — previously hardcoded to `"in"`) by part kind.
- Operands (`in1`/`in2`) are each a tag or a literal — reuses/generalizes the same resolver TON's
  `PT` already used (renamed `ResolveTagOrLiteralOperand`, parameterized by port). The literal
  case is a **new top-level Access scope**, `LiteralConstant` — previously only seen *nested*
  inside an array-index `<Component>`; the top-level use always carries a `<ConstantType>`
  (e.g. `Int`), the exact opposite of `TypedConstant` (TON's `PT` literal, never has one).
  `ConstantAccessNode`/`SidecarConstantEntry` gained a nullable `ConstantType` field rather than a
  second type, mirroring how `AccessNode` already carries its own `Scope` generically.
- `Expr.TimeLiteral` generalized to `Expr.Literal` (covers both TON's time literals and
  comparisons' numeric literals — no reader-relevant difference at the IR-text level, both just
  render their verbatim value). The IR parser recognizes a literal by shape (`T#` prefix, or a
  bare optionally-negative integer — safe since a real tag path is never purely numeric,
  `06-lad-conventions.md` C-005) rather than consulting the sidecar.
- **Deliberately not modeled: a comparison composing with an OR-merge** (as a branch, or feeding
  one). Real — `ControlDelays`' own `O(41)` combines two comparisons (`Ge`/`Eq`) as branches, each
  itself fed by another OR-merge rather than Powerrail directly. The *existing*, unmodified
  OR-merge branch check (`branchPart.Name != "Contact"`) and the *existing* wire fan-out check
  already safely refuse this shape — proven by a dedicated test
  (`ComparisonTests.Reduce_ComparisonAsOrMergeBranch_ThrowsNonReducible`), not just assumed. Same
  deferred status as the already-known multi-contact-OR-branch/nested-OR-merge cases; not scope
  creep to fix, a legitimate future phase.

**Live-verified against real data, 2026-07-11** (after clearing 3 stale TIA Portal processes and
confirming a fresh single instance): isolating the real network directly (`FlgNetParser` →
`GraphReducer` → `FlgNetBuilder` → `FlgNetWriter`, a throwaway test against the live-exported
XML) confirmed `Eq → Contact → TON.IN` reduces successfully against genuinely live, unmodified
data, and the Coil's own chain (needing the OR-merge of two comparisons) threw exactly the
predicted, already-tested error at the time — `docs/notes/stage-gates.md` has the full story.
**Closed out, 2026-07-12 (S1 item 11 below):** the same Coil chain now reduces cleanly too. Both
grounded shapes are additionally proven by unit tests built directly from the real export — see
`Converter.Tests/ComparisonTests.cs`. A whole-block `ControlDelays` round-trip still isn't
reached (`Mul`/`Convert` in an unrelated network, a separate deferred capability).

## MOVE support (S1 item 10, 2026-07-11)

`Part Name="Move"` — grounded against a real export, `FB MotorDOL`'s "HMI Motor Status
Telemetry" network: a cascade of `Contact -> Move` taps writing a status code, no `Coil` at all
in that real network.

- **Key finding: a Move is neither a boolean chain position (unlike Eq/Ge) nor a self-contained
  production like TON — it's a side effect *tapped off* a chain position's own output via genuine
  wire fan-out.** The same wire that feeds the next chain position also feeds the Move's `en`, so
  a wire can carry three endpoints (producer, Move.en tap, next-position.in) instead of the usual
  two. `IN` is a single tag-or-literal operand (reuses `ResolveTagOrLiteralOperand`, same resolver
  as TON's `PT`/a comparison's operands); `out1` writes to a plain tag (reuses `ResolveOperand`
  with a new `port` parameter — the wire shape is identical to an ordinary Contact/Coil operand,
  only the read/write direction differs semantically).
- **Core `GraphReducer.TraceChain` redesign.** The fan-out check changed from "exactly one other
  endpoint on a wire, else throw" to "find the single endpoint whose (Part, Port) is a genuine
  producer (`OutPortFor`), ignore every other endpoint as an uninspected consumer." This is what
  lets a Move's `en` tap coexist on the same wire as the chain's real continuation without
  breaking series-chain reducibility for the chain that owns it.
- **`FlgNetBuilder` rebuilt around a shared, de-duplicating endpoint accumulator.** Multiple
  Moves' own `en` chains commonly telescope through the same upstream Contacts a Coil's (or
  another Move's) chain already walked — `GraphReducer` deliberately re-derives the same
  `ChainStepSidecar` data once per production that traces through a shared Contact (this is
  correct, not a bug: each production's sidecar needs its own complete picture). Naively rebuilding
  from that would emit duplicate `<Part>`/`<Wire>` elements. `AddPart`/`AddEndpoint` now key by
  UId and de-duplicate, and the old rail-only `railEndpointsByWireUId` accumulator generalized
  into the same mechanism used for every wire, not a rail special case.
- `DisabledENO="true"` and the `Card=1` `TemplateValue` are fixed in every real instance seen —
  hard-validated on parse and unconditionally regenerated on write, never carried as `PartNode`
  data (same "don't store a confirmed constant" reasoning as TON's `InstanceOfType`). A different
  `Card` value would presumably be a `MOVE_BLK_VARIANT`-style multi-element copy — real but
  unconfirmed, refused rather than guessed at.
- Readable-form syntax: `MOVE(EN := <expr>, IN := <expr>) => <dest>` (`ir/SPEC.md` has the full
  grammar note).

12 converter tests (`Converter.Tests/MoveTests.cs`), including a fixture
(`MoveTelescopingChain.xml`) built directly from the real telescoping/fan-out shape (genericized
per `docs/13-data-boundary.md`) — proves both that the reducer's duplication-by-design is correct
and that the builder's de-duplication produces exactly the right topology back (no duplicate
Parts/Wires, and the two genuinely fanned-out wires end up with all 3 real endpoints each). All
106 converter tests pass.

**Live-verified against real data, 2026-07-11:** isolating `FB MotorDOL`'s real "HMI Motor Status
Telemetry" network (a throwaway test against a fresh export, deleted after use) showed the real
topology is richer than any fixture — one Contact's outgoing wire genuinely fans out to **three**
consumers, not two — and parsing/fan-out-producer-identification handled it correctly. Full
`Reduce()` on the whole network hit a pre-existing, already-deferred limitation unrelated to MOVE
(a multi-contact OR-merge branch not fed directly from Powerrail, same class of gap as
comparisons' `O(41)` case) — but Part document order confirms 3 of the network's 5 real Moves
(including one tapping the genuine 3-way fan-out) reduced successfully first. A fully
self-contained real sub-network (`Contact47 → Move48`) round-tripped end to end cleanly. Full
story: `docs/notes/stage-gates.md` ("S1 item 10 continued"). **Closed out, 2026-07-12 (S1 item 11
below):** the same real network now reduces all 5 Moves, including the one gated by the OR-merge.

## OR-merge branches generalized to recursive chains (S1 item 11, 2026-07-11/12)

Closes the exact gap items 9 and 10 each left open. `GraphReducer.ResolveOrMerge` originally
required every branch to be a single `Contact` fed directly by Powerrail; real data hit this
twice — `FC ControlDelays`' `O(41)` combines two comparisons, each fed by a further OR-merge
rather than Powerrail; `FB MotorDOL`'s `O(45)` is fed by a shared, non-rail-fed Contact.

- **Each branch (`inK` port) is now resolved via the exact same `TraceChain` mechanism as a
  Coil's condition, a TON's `IN`, or a Move's `en` — recursion, not a new algorithm.** A branch
  that's itself a multi-Contact chain, a comparison, or a nested OR-merge all just work, since
  they're handled by `TraceChain`'s own existing per-part-kind dispatch, not bespoke branch-walk
  logic. `ResolveOrMerge` shrank from a hand-rolled single-hop walker (Contact-only check, "fed
  directly from Powerrail" check, "branches share one rail wire" check) to a thin loop calling
  `TraceChain` once per branch.
- **`ChainStepSidecar.OrStep.Branches` changed from a flat `ContactStep` list to `OrBranch`**
  (`Steps` + nullable `RailWireUId`) — mirrors the `(Steps, RailWireUId)` shape every other
  production (`CoilAssignmentSidecar`/`TimerBindingSidecar`/`MoveStatementSidecar`) already
  carries, since a branch is now genuinely a first-class mini-chain, not a single Contact record.
- **The outer chain's own `RailWireUId` is `null` whenever it terminates at an `OrStep`** — once
  branches can diverge (one rail-fed, another terminating at a TON's `Q`), there's no single
  shared value left to bubble up; each branch now carries its own. Mirrors the existing
  `TimerOutputStep` precedent exactly (a chain terminating at a TON's `Q` was already `null`
  here). The common real case — branches genuinely sharing one rail wire, confirmed real
  2026-07-10 — still works with zero special-casing: two branches independently reporting the
  same wire UId simply merge via the existing endpoint-accumulator dedup.
- **`FlgNetBuilder` needed no new accumulation mechanism** — the MOVE-era shared,
  de-duplicating `AddPart`/`AddEndpoint` accumulator already handles "the same upstream Contact
  touched by multiple productions," so an OR-branch sharing a prefix with another branch, a Move
  tap, or an unrelated chain elsewhere in the network all dedupe identically. `BuildStep`'s
  `OrStep` case now builds each branch's own steps the same way any top-level chain builds its
  own (recursing into `BuildStep` itself), terminating at that branch's own `inK` port.
- **New AND/OR operator precedence grammar** (`ir/SPEC.md` has the full note) — needed once a
  branch's own condition could be a compound expression, not just a bare tag. `AND` binds tighter
  than `OR` (confirmed with the project owner), parens only where precedence alone would
  misparse. `IrParser`'s old `ParseExpr` (a naive `.Contains(" AND ")`/`.Contains(" OR ")`
  substring split — it never actually handled mixed AND+OR correctly, just never got exercised by
  anything more complex until now) became a real precedence-climbing recursive descent
  (`ParseOrExpr → ParseAndExpr → ParseUnaryExpr → ParsePrimaryExpr`, parenthesized-group-aware).

Three fixtures already existed as hard-error tests for exactly these shapes (built during earlier
OR-merge/comparisons work, precisely the project's established "repurpose an obsolete hard-error
test into a positive one" pattern) and were repurposed rather than replaced: `NestedOrMerge.xml`,
`OrMergeMultiContactBranch.xml`, `OrMergeOfComparisons.xml`. One new fixture
(`OrMergeSharedPrefixBranches.xml`) covers the one real shape none of the three already had — a
single upstream Contact's outgoing wire genuinely fanning out to become the shared prefix of two
different OR-merge branches, mirroring `FB MotorDOL`'s real topology at a minimal scale — and
**passed on its first run**. 14 new standalone grammar tests (`IrExprGrammarTests.cs`) cover
mixed `AND`/`OR`/`NOT`/parens precedence independent of the reducer. All 130 converter tests pass
(up from 106); `openness-cli`/golden-harness suites unaffected, confirmed still green (68/11).

**Live-verified against real data, 2026-07-12:** isolated both real networks that motivated this
item — `ControlDelays`' `O(41)`/`O(38)` network and `MotorDOL`'s telemetry network — via fresh
exports and a throwaway test (real data deleted after use). **Both now reduce and round-trip
completely**, where both were previously blocked at exactly this OR-merge limitation.
`ControlDelays`' Coil condition turned out deeply nested — `(GeneralEnableDelay.Q OR
PlantControl.GeneralEnable) AND PlantControl.Status >= 1 OR (GeneralEnableDelay.Q OR PlantControl.GeneralEnable)
AND PlantControl.Status = -1` — an `Or` of two `And`s each containing a nested `Or`, richer than any
fixture and a strong real-data proof of the precedence grammar; it also round-tripped
byte-identically through `Serialize → Parse → Serialize`. `MotorDOL`'s telemetry network now
reduces all 5 `Move` statements. Full story: `docs/notes/stage-gates.md` ("S1 item 11" live
verification section). A whole-block `ControlDelays` round-trip still isn't reached (`Mul`/
`Convert` in an unrelated network, a separate deferred capability) — this item's own scope is
fully closed on both real networks that motivated it.

## WAND — bitwise word AND (S1 item 12, 2026-07-12)

`Part Name="And"` — found while searching 28 real LAD blocks specifically for a boolean
parallel-branch "AND-merge" (this doc's own scope line and `ir/SPEC.md`'s original table row both
carried this as a speculative, unconfirmed shape since the project's earliest sketch). **That
shape doesn't exist anywhere in the sweep** — architecturally expected in hindsight: boolean AND
in ladder logic is always plain series Contacts, never needing an explicit merge Part the way OR
genuinely does (parallel branches converging need a defined merge point; AND never does). What
the sweep found real instead, `Part Name="And"`, is something different: a **bitwise/word-level
box instruction** (`Word AND 16#89 -> ControlWord`, `FB VSDUpdateComs`), not a boolean chain
position at all.

- **Structurally closest to `Move`**: a side effect gated by `en` (the same `TraceChain`
  fan-out-tap mechanism, confirmed real — the And's own `en` shares a rail wire with three
  sibling Contacts elsewhere in the network), crossed with an OR-merge's own
  `Cardinality`-driven multi-operand shape (here driving *input count* — `IN1`..`INn` — not
  branch count; only `Card="2"` observed in the one real example) and a comparison's own
  `SrcType` (`Word` — carried sidecar-only, not shown in the readable IR text, same precedent as
  a comparison's own `SrcType`).
- Readable-form syntax: `WAND(EN := <expr>, IN1 := <expr>, IN2 := <expr>, ...) => <dest>`.
  **`WAND`, not `AND`** — deliberately avoiding a collision with the existing boolean `AND`
  infix operator; a converter-owned vendor-neutral name mapping, same precedent as
  `MOVE_BLK_VARIANT` → `MOVE`.
- `DisabledENO="true"` is fixed in every real instance seen — hard-validated on parse and
  unconditionally regenerated on write, never carried as `PartNode` data, same reasoning as
  Move's own `DisabledENO`. Unlike Move's fixed `Card="1"`, this instruction's `Cardinality` *is*
  carried as data (`PartNode.Cardinality`, already existing — reused, not a new field) since only
  one real value has been observed, not enough to treat as a universal constant.
- **A real, previously-unexercised parser gap surfaced by this grounding**: Siemens' own
  `<base>#<value>` numeric-literal notation (`16#89`) wasn't recognized by `IrParser.ParseLeaf`'s
  shape-based literal detection (only `T#`-prefixed time literals and bare integers were) — fixed
  alongside this work, matched generically by base-number shape rather than hardcoded to base 16.

8 converter tests (`Converter.Tests/WordAndTests.cs`), fixture built directly from the real
`VSDUpdateComs` shape (genericized per `docs/13-data-boundary.md`, same UIds/structure). All 138
converter tests pass (up from 130); `openness-cli`/golden-harness suites unaffected, confirmed
still green (68/11).

**Live-verified against real data, 2026-07-12:** isolated `FB VSDUpdateComs`'s real network
directly (a throwaway test against a fresh export, deleted after use) — reduces and round-trips
completely, including the `16#89` literal round-tripping cleanly through `Serialize → Parse →
Serialize`.

## Not — standalone boolean inverter (S1 item 13, 2026-07-12)

`Part Name="Not"` — found while investigating whether `FC PlantAutoControl` (a real, complex
orchestrator block) round-trips; `Not` blocks its very first network. Grounded against two
independent real instances in that same block (different networks, different UIds, identical
shape) before writing any code.

- **Genuinely different from a Contact's own `<Negated Name="operand" />`**, which negates a *tag
  read*, not a chain position. `Not` is a standalone Part with a single `in`/`out` port pair (same
  port names as `Contact`'s own), no operand/Access lookup at all — it inverts whatever boolean
  value arrives on `in`.
- **No new IR-text grammar needed.** `NOT <expr>`/`Expr.Not` already existed (S1 items 7/11) and
  already rendered/parsed with correct precedence — this construct is entirely about recognizing
  the new `Part Name="Not"` shape and resolving it, not about new syntax.
- **Architecturally simpler than Move/WAND**: `Not` is never its own top-level production (no new
  `IrNetwork`/`NetworkSidecar` list, no new `Reduce()` loop). It's purely a new chain-position
  kind, discovered only when some other production's own backward trace (`TraceChain`) walks into
  one. Resolved via a fully self-contained, recursive `TraceChain` call on the `Not`'s own `in`
  port — exactly like an OR-merge branch's own resolution — wrapping the result in `Expr.Not`.
  `ChainStepSidecar.NotStep` mirrors `OrBranch`'s nested `(Steps, RailWireUId)` shape.
- **Both real instances grounded tap a shared wire via genuine fan-out**: an upstream Contact's
  output feeds both a separately-continuing chain and the `Not` — the same fan-out-tap mechanism
  already proven for Move taps and OR-merge branches, here feeding back into a boolean chain
  instead of terminating in a side-effect write.

6 converter tests (`Converter.Tests/NotTests.cs`), fixture built directly from the real
`PlantAutoControl` shape (genericized per `docs/13-data-boundary.md`). All 144 converter tests pass (up
from 138); `openness-cli`/golden-harness suites unaffected, confirmed still green (68/11).

**Updated 2026-07-12 (S1 item 14):** with `CALL` now built (below), the real network that
motivated closing this gap — `Not` wrapping an OR-of-comparisons alongside a fully-wired
10-parameter FB call — reduces and round-trips completely through the in-memory pipeline. Still
not verified through the true TIA `import → compile → re-export → Normalizer` cycle — see the
`CALL` section below for exactly why and what's next.

## CALL — FB/FC block calls (S1 item 14, 2026-07-12)

Picked up specifically to close the gap `Not` left open: every one of `FC PlantAutoControl`'s 20 real
networks pairs `Not` with a `<Call>`, so `Not` alone could never reach the true TIA-cycle gold
standard. Grounded first (a fresh `PlantAutoControl` export, scratch temp, deleted after use), before
any code.

- **`<Call>` isn't a `<Part Name="Call">`** — genuinely unlike every other construct built so
  far, it's its own sibling element under `<Parts>`:
  `<Call UId="N"><CallInfo Name="<callee>" BlockType="FB"><Instance .../><Parameter .../>...
  </CallInfo></Call>`. `FlgNetParser` adapts this into an ordinary `PartNode(Name="Call")` on the
  way in (and `FlgNetWriter` mirrors it back on the way out) so the rest of the pipeline
  (`GraphReducer`, `FlgNetBuilder`) never needs a parallel type.
- **Reduces as its own top-level production**, like TON/Move/WAND — not like `Not`, which is a
  chain-position discovered incidentally. `en` is reduced via the same `TraceChain` fan-out
  mechanism as Move/WAND's own `en` (confirmed real: all 20 real instances are directly rail-fed,
  reducing to the existing "wired directly to rail" TRUE sentinel — no Contact-gated `en` on a
  Call seen yet, low-risk by analogy).
- **Instance reuses the exact same `AccessNode` shape as TON's own `<Instance>`** — confirmed
  identical (this is the reuse `ir/SPEC.md`'s TON section deliberately anticipated: "a future
  FC/FB call's own instance argument can reuse `AccessNode` rather than needing a redesign").
  `FlgNetParser.ParseInstanceReference` was factored out of `ParseTon` once a second real caller
  confirmed the shape is genuinely shared, not TON-specific.
- **Arguments are sparse, not a full interface snapshot** — the single most surprising real
  finding: 19 of the 20 real `<Call>` instances have **zero** `<Parameter>` children at all (not
  present-but-empty — simply absent, just `Instance` + `en`). Only one real instance has any: 10
  (8 `Section="Input"`, 2 `Section="Output"`, all `Type="Word"`), in source declaration order.
  `GraphReducer.ReduceCall` iterates whatever `<Parameter>` elements are actually present — an
  Input resolves via the same `ResolveTagOrLiteralOperand` used everywhere else; an Output is a
  bare destination tag, same `NameCon`→`IdentCon` wire shape as Move's own `out1`, just named per
  the source's own Parameter Name instead of a fixed port.
- **No `EN`/`ENO` attribute-level fixed shape to validate** — unlike Move/WAND's own
  `DisabledENO="true"`, a Call carries no `DisabledENO` (or any other) attribute at all; `eno`
  itself is never wired in any real instance seen, so nothing is validated or regenerated for it.
- Readable-form syntax:
  `CALL <BlockName>([<InstancePath>, ]EN := <expr>, Param1 := <expr>, ..., OutParam => <tag>, ...)`
  — instance, when present, comes first (no label, same convention as TON's own instance path;
  omitted entirely for FC calls, which carry none — see the dedicated section below), `EN` always
  shown explicitly (matching MOVE/WAND's own convention for full losslessness, even though every
  real instance seen is trivially `TRUE`), remaining arguments exactly whatever the source wired,
  mixed `:=`/`=>` in source order. Matches ADR-0001's "reference only, no inline
  parameter-interface snapshot" decision — realized structurally (the source itself is sparse) as
  well as textually.

14 converter tests (`Converter.Tests/CallTests.cs`), two fixtures genericized from the two real
shapes (`CallBareFedByRail.xml` — the common, unparameterized case; `CallWithParametersFedByRail.xml`
— genericized down from the one real 10-parameter instance). All 158 converter tests pass (up
from 144); `openness-cli`/golden-harness suites unaffected, confirmed still green (68/11).

**Live-verified against real data, 2026-07-12:** isolated `FC PlantAutoControl`'s richest real network
(the one with the wired 10-parameter call) — it also contains `Not` wrapping an OR-of-two-
comparisons (itself gated by a negated contact), 8 independent Contact→Coil rungs, and further
negated-contact/comparison content. **Reduces and round-trips completely through the in-memory
pipeline** — the first real network combining `Not` and `Call` together to do so.

**Updated 2026-07-12 (S1 item 15):** with `SCoil`/`RCoil` now built (below), `PlantAutoControl` no
longer has any *instruction-level* gap. Attempting a whole-block `to-ir` surfaced a genuinely
different, already-known gap instead: every one of its 20 real networks carries a non-empty
`Title` (distinct from `Comment`), which `BlockSourceParser` already hard-errors on (documented
earlier during the reference-project corpus work, `tests/golden/README.md` — not a new
discovery, just newly encountered on this specific real block). See the `SCoil`/`RCoil` section
below for the full story.

## SCoil/RCoil — set/reset coils (S1 item 15, 2026-07-12)

Picked up per the project owner's own explicit sequencing after `CALL` — 3 of each real in
`FC PlantAutoControl`, also seen alongside TON in `FB MotorDOL`'s own earlier grounding (S1 item 8:
the one real "TON's `Q` wired directly into a downstream part" example that couldn't be modeled
at the time fed an `RCoil`, itself out of scope then).

- **Grounded against two independent real instances of each before any code.** Both are
  completely bare — `<Part Name="SCoil" UId="N" />` / `<Part Name="RCoil" UId="N" />`, no
  attributes or children beyond `Name`/`UId` — with the *exact same* `in`/`operand` wire shape as
  a plain `Coil`, and never a producer (no `out` port). Structurally identical to `Coil` in every
  respect this converter cares about.
- **`GraphReducer`/`FlgNetBuilder` reuse `ReduceOneChain`/`BuildOneChain` verbatim** for all three
  kinds — no new reducer/builder method, no new chain-position kind. The only addition is a new
  `CoilAssignment.Kind` field (`Assign`/`Set`/`Reset`), set from the source Part Name in
  `GraphReducer.CoilKindFor` and read back in `FlgNetBuilder.CoilPartNameFor`. `CoilAssignmentSidecar`
  gained **no new field** — `BuildOneChain` already takes the model `CoilAssignment` alongside its
  sidecar (for its pre-existing leaf-count cross-check), so it derives the exact Part Name from
  `Kind` directly rather than duplicating it.
- **The semantic difference is inherent in the keyword, not computed by the IR**: `SCoil` only
  ever sets the target true when the condition is true (leaving it unchanged when false); `RCoil`
  only ever clears it the same way — genuinely different from `Coil`'s own direct assignment, but
  the condition itself is resolved via the identical `TraceChain` mechanism regardless of kind.
- Readable-form keywords `SCOIL`/`RCOIL` mirror their own source Part Names, matching every IR
  keyword built so far except `WAND` (which deliberately diverges from `AND` for a naming
  collision that doesn't apply here). `COIL`/`SCOIL`/`RCOIL` assignments are parsed/serialized in
  one interleaved section, in whatever order they appear — not three separate ones — matching how
  a real network naturally mixes assign/set/reset rungs.

6 converter tests (`Converter.Tests/SCoilRCoilTests.cs`), one fixture interleaving all three
kinds on a shared rail wire, genericized from the real shape. All 164 converter tests pass (up
from 158); `openness-cli`/golden-harness suites unaffected, confirmed still green (68/11).

**Live-verified against real data, 2026-07-12:** isolated the same real network already grounded
for `CALL` (it also contains the block's `SCoil`/`RCoil` pair) — reduces and round-trips
completely, correctly distinguishing `Set`/`Reset` kinds from a plain `Coil` assignment.

**Attempted a whole-block round-trip of `PlantAutoControl`, since this should have closed its last
known instruction-level gap.** `converter to-ir` on a fresh whole-block export immediately hit a
different, already-known gap: every one of `PlantAutoControl`'s 20 real networks carries a non-empty
`Title` (distinct from `Comment`) — `BlockSourceParser` already hard-errors on this shape
(confirmed and documented during the reference-project corpus work, `tests/golden/README.md`,
not a new discovery). So `PlantAutoControl` still doesn't round-trip as a whole block — for a reason
entirely unrelated to any instruction type this converter slice models. **Resolved next, same
session — see the Title section below.**

## Title — network- and block-level (S1 items 16/17, 2026-07-12)

Picked up immediately after `SCoil`/`RCoil` surfaced the gap above. A real design correction, not
just a new field — see `ir/SPEC.md`'s own file-shape section for the full story: the `NETWORK
<n> "<title>"` line's own quoted text was, before this, actually sourced from the network's
`Comment` field, an unnoticed mismatch since every network grounded earlier this session had an
empty Comment. Confirmed with the project owner before changing (`AskUserQuestion`, not assumed):
the `NETWORK` line's label now carries the real `Title`; `Comment` gets its own new, separate,
optional `COMMENT "..."` line, mirroring the pre-existing block-level one.

- **Network-level Title (S1 item 16)**: confirmed real and populated on every one of
  `PlantAutoControl`'s 20 networks (Comment empty everywhere, the opposite emphasis of every network
  grounded before this block). `BlockSourceParser`/`BlockSourceWriter` read/write it the same way
  Comment already was (`MultilingualTextHelper.ReadMultilingualText`/
  `DbSourceWriter.WriteMultilingualText`, generalized from a Comment-only helper once a second
  real `CompositionName` confirmed the shape is genuinely shared). `IrNetwork` gained a `Comment`
  field (mirroring `Title`, which already existed); `CoilAssignmentSidecar` needed no changes —
  synthetic, regenerated `MultilingualText` IDs on write, same precedent as Comment's own.
- **Block-level Title (S1 item 17)**: initially assumed always-empty — `RequireEmptyTitle` had
  never seen a real counter-example before. Confirmed real while grounding the 8 FBs `PlantAutoControl`
  depends on: `MotorVSDSystem` and `AirStar` both carry a real, identical block-level Title ("VSD
  Motor" — a shared, templated title across that FB family). Same treatment as network-level:
  `BlockSource`/`IrBlock` gained a `Title` field, a new `TITLE "..."` line (block-level, alongside
  the pre-existing `COMMENT` line — genuinely new, since the `BLOCK` line's own quoted text is the
  block's real Name, unlike a `NETWORK` line's label which Title could repurpose). DB-level Title
  remains unconfirmed real, still hard-errored.
- Sanitization (S1 items 16/17): both Titles are genuinely identifying free text in real data
  (e.g. equipment/process names) — same hard-error-if-unmapped treatment as Comment, via two new
  `SanitizationMap` dictionaries (`NetworkTitles`, `Titles`).

13 new converter tests across both items, plus regenericized fixtures. All 177 converter tests
pass (up from 164); `openness-cli`/golden-harness suites unaffected, confirmed still green
(68/11).

**Live-verified against real data, 2026-07-12.** Network-level: `converter to-ir` on a fresh
whole-block `PlantAutoControl` export now succeeds completely — **the first real production block this
whole session to fully convert as a whole block** — and `to-ir → to-xml → to-ir` round-trips
byte-identical (IR self-stability at full-block scale, not just per-network). Block-level:
isolated `MotorVSDSystem`/`AirStar` — both now progress past the block-Title check to a different,
already-known gap (a non-empty `Interface` section `Constant` — the same deferred
parameter-interface item `TomraControlSystem` hit via its own `Input` section, not a new capability).

**Then attempted the true TIA cycle, per the project owner's explicit instruction to route
through `SampleProject` rather than modifying `JOB9002`.** Exported `PlantAutoControl` from `JOB9002`,
converted it, and imported the regenerated XML into `SampleProject` — **the import itself
succeeded** (TIA accepted the regenerated XML as structurally valid into a completely unrelated
project). **Compiling it there failed with 502 errors**, every one either a "tag not defined" (of
323 distinct tag/DB-member paths `PlantAutoControl` references) or a "referenced block no longer
exists" (its 8 dependency FBs, called 20 times total) — entirely because `SampleProject` has
neither `PlantAutoControl`'s own tag table nor its FB library. This is an environmental/dependency
limitation, not a converter defect: a block doesn't carry its project context with it. Grounded
those 8 dependency FBs directly afterward: **0 of 8 convert cleanly today**, blocked by arithmetic
(`Mul`/`Convert` — 5 of 8) or the same parameter-interface gap noted above (1 of 8; the other 2,
`MotorVSDSystem`/`AirStar`, are now blocked by that same Interface gap too, having cleared the Title
check). Arithmetic support is scoped as the next capability.

## MUL/CONVERT — arithmetic (S1 item 18, 2026-07-12)

Picked up immediately after Title, per the project owner's own explicit sequencing ("Let's do
block-level Title now, then scope arithmetic support") — the larger of the two remaining real
gaps blocking `PlantAutoControl`'s 8 dependency FBs (5 of 8, vs. 1 of 8 for the already-known,
separately-deferred FC/FB parameter-interface gap). Planned via formal plan mode per explicit
request.

**Phase 0 grounding (mandatory before design, per CLAUDE.md hard rule 3) found a genuine
surprise the plan's own inherited assumption got wrong.** Real exports of `MotorDOL`/
`EquipmentControlSystem`/`ShredderControlSystem` (scratch temp, deleted after use) show:

```xml
<Part Name="Mul" UId="36" DisabledENO="true">
  <TemplateValue Name="Card" Type="Cardinality">2</TemplateValue>
  <AutomaticTyped Name="SrcType" />
</Part>
<Part Name="Convert" UId="37" DisabledENO="true">
  <TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>
  <TemplateValue Name="DestType" Type="Type">DInt</TemplateValue>
</Part>
```

- **`Mul`**: `DisabledENO="true"`, `Card="2"` (two inputs, `in1`/`in2`, `out` — same
  Cardinality-driven shape as WAND). Its own type is `<AutomaticTyped Name="SrcType" />` — a
  self-closing element with no value at all, since TIA infers the type from the connected
  operands rather than declaring it statically. Genuinely different from every other typed
  instruction built this session (WAND/comparisons always carry an explicit
  `TemplateValue Type="Type">X<`). Modeled as `PartNode.AutomaticSrcType: bool` (shape-only,
  nothing to carry) rather than reusing `SrcType`.
- **`Convert`**: `DisabledENO="true"`, an ordinary `SrcType`/`DestType` `TemplateValue` pair
  (e.g. `Real`→`DInt`) — converts *between* two types, unlike anything else built so far.
  `PartNode` gained a new `DestType: string?` field alongside the existing `SrcType`.
- **The real surprise, confirmed in two independent instances (`MotorDOL`, `EquipmentControlSystem`)**:
  despite `DisabledENO="true"` on both — matching the Move/WAND precedent that `eno` is never
  wired — real networks have three `Mul`→`Convert` pairs where **`Mul`'s own `eno` output wires
  directly into the following `Convert`'s own `en` input**: a genuine control-flow chain ("only
  run `Convert` if `Mul` succeeded"), structurally unlike every prior `en`/`IN` in this project
  (always independently rail-fed or contact-gated, never fed by a *preceding box instruction's
  own output port*). This directly contradicted the plan's own inherited assumption — flagged to
  the project owner before implementing anything, per this project's "ask before design
  deviation" discipline, rather than picking a design and hoping it fit.
- **Not universal**: `ShredderControlSystem` has a standalone `Convert` (`SrcType`/`DestType` both
  `Int`) with a plain, independently rail-fed `en` — the ordinary case is real too, fully covered
  by the existing `TraceChain` mechanism with zero changes.
- No other arithmetic-family instruction (`Add`/`Sub`/`Div`/`Abs`/`Swap`/`Calc`) observed
  co-occurring in any of the three grounded networks — scope stayed `Mul`/`Convert` only.

**Confirmed design** (project owner approved: "Go with that design."): ENO-chaining is **not**
folded into `Expr` — there's no tag `Mul` could be referenced by (no `Instance` element, unlike
`TON`/`CALL`). New `EnSource` discriminated union (`Ir/Model.cs`) — `Condition(Expr Value)` for
the ordinary boolean-condition case, `PrecedingEno` for the chained case — with a new reserved
readable-form sentinel `EN := ENO` (mirroring the existing `TRUE` sentinel precedent for a
zero-step rail-fed chain), meaning "gated by the immediately preceding statement's own ENO."
Statement order keeps a chained pair textually adjacent for readability, but the sidecar always
carries the exact source UId being chained from — parsing never actually depends on adjacency for
correctness.

- `MulStatement(EnSource En, IReadOnlyList<Expr> Inputs, string DestTag)` /
  `ConvertStatement(EnSource En, Expr In, string DestTag)` — new top-level productions, same
  shape family as `TON`/`Move`/`WAND`/`CALL`. `Card` validated fixed at 2 for `Mul` (only value
  observed — hard-errors on anything else).
- `GraphReducer.ResolveEnSource` — new shared resolver: checks whether a chain step's own `en`
  wire's only non-self endpoint is `NameCon(<uid>, "eno")` on a `Mul`/`Convert` Part; if so,
  records `PrecedingEno` (source Part UId + wire UId) without calling `TraceChain` at all; else
  falls back to the existing `TraceChain` mechanism unchanged, wrapped in `Condition`.
- `FlgNetBuilder.BuildEnSource` — mirror on the write side: `ConditionSidecar` reuses the
  existing steps+rail chain-building; `PrecedingEnoSidecar` directly adds both endpoints of the
  `eno`→`en` wire.
- Readable form: `MUL(EN := <expr-or-ENO>, IN1 := <expr>, IN2 := <expr>) => <dest>` /
  `CONVERT(EN := <expr-or-ENO>, IN := <expr>) => <dest>`. Keywords mirror source Part Names,
  matching the dominant convention (`WAND` remains the one deliberate exception).

14 new converter tests (`Converter.Tests/MulConvertTests.cs`), two fixtures genericized from the
real grounded shapes (`ConvertStandaloneFedByRail.xml`, `MulConvertEnoChainedPair.xml`). All 191
converter tests pass (up from 177); `openness-cli`/golden-harness suites unaffected, confirmed
still green (68/11).

**Live-verified against real data, 2026-07-12.** Isolated both the ENO-chained case (`MotorDOL`)
and the standalone rail-fed case (`ShredderControlSystem`) — both reduce and round-trip correctly,
sidecar-preserving the exact `eno`→`en` wire in the chained case. **Whole-block `to-ir` sweep of
all 5 previously-blocked dependency FBs** (`MotorDOL`/`EquipmentControlSystem`/`FilterUnitSystem`/
`MotorFwdRevSystem`/`ShredderControlSystem`) confirmed none hit a `Mul`/`Convert` error anymore — they now
hit `TONR` instead (a retentive TON variant, out of scope, now confirmed real rather than
theoretical). Arithmetic beyond `Mul`/`Convert` remains out of scope, per the plan's own explicit
scoping — nothing observed needing it.

## TONR / ADD / Lt (S1 item 19, 2026-07-12)

Picked up per the project owner's own choice (over the FC/FB parameter-interface gap) after S1
item 18's own whole-block sweep confirmed `TONR` (a retentive on-delay timer) as the real, common
blocker across all 5 remaining dependency FBs. Planned formally in plan mode.

**Phase 0 grounding** (`MotorDOL`/`FilterUnitSystem`, two independent instances, byte-identical network
shape):

```xml
<Part Name="TONR" Version="1.0" UId="40">
  <Instance Scope="LocalVariable" UId="41">
    <Component Name="HrTotaliserTimer" />
  </Instance>
  <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
</Part>
```

- **`TONR`**: identical `Version`/`Instance`/`time_type` shape to `TON` — no `EN`/`ENO` on either
  — plus one genuine new port, **`R` (reset)**, fed directly by a plain tag in both instances (no
  chain, same shape as `PT`). Modeled via a new `TimerKind` enum (`Ton`/`Tonr`), mirroring the
  `CoilKind` (`Assign`/`Set`/`Reset`) precedent exactly — duplicated onto the sidecar record too
  (not model-only), since `FlgNetBuilder.BuildTimer` is sidecar-only and never cross-references
  the model (unlike `BuildOneChain`, confirmed by reading `FlgNetBuilder.cs` in full first).
- **Scope expanded mid-grounding, confirmed via `AskUserQuestion` ("Bundle Add + Lt into this
  item")**: a full `Part Name="..."` sweep of both grounded files found `Add` and `Lt` alongside
  the already-expected `TONR` — `TONR` alone would not have gotten either FB to fully round-trip.
  - **`Add`**: `<Part Name="Add" UId="45" DisabledENO="true"><TemplateValue Name="Card"
    Type="Cardinality">2</TemplateValue><AutomaticTyped Name="SrcType" /></Part>` — identical
    shape to `Mul`. Modeled via a new `MulKind` enum (`Multiply`/`Add`) on the same
    `MulStatement`/`MulStatementSidecar` records, same reasoning as `TimerKind`. Its own `en` in
    the grounded network is fed by a comparison's (`Lt`'s) `out` — checked directly against the
    real wiring before writing any code, confirmed to be an ordinary `TraceChain` `Condition`, not
    ENO-chained — so `ResolveEnSource`'s `Mul`/`Convert` whitelist was deliberately left untouched.
  - **`Lt`**: `<Part Name="Lt" UId="44"><TemplateValue Name="SrcType" Type="Type">UDInt</TemplateValue></Part>`,
    wired via `pre`/`in1`/`in2` — structurally identical to `Eq`/`Ge`. Needed **zero new model
    shape** — `ChainStepSidecar.CompareStep` already carries `PartName` generically — purely a
    parser/reducer/writer dictionary extension (`SupportedComparisonPartNames`,
    `ComparisonOperator`, `OutPortFor`, `TraceChain`'s upstream dispatch).
- **Readable form**: `TONR(<instance path>, IN := <expr>, PT := <expr>, R := <expr>)` — same as
  `TON` plus the confirmed-real `R` argument, parsed/serialized via a variable-arity
  split-on-top-level-commas approach (mirroring WAND/CALL/MUL's own precedent) rather than a
  single fixed-arity regex, since `TON`/`TONR` now genuinely differ in argument count. `ADD(EN :=
  <expr-or-ENO>, IN1 := <expr>, IN2 := <expr>) => <dest>` mirrors `MUL` exactly, sharing one
  parse/serialize path distinguished by keyword. `Lt` needs no new syntax — comparisons are
  already plain infix `Expr.Compare`, just gaining a new `<` operator symbol.

16 new converter tests (`Converter.Tests/TonrTests.cs`), three fixtures genericized from the real
grounded shapes (`WithTonr.xml`, `LtFeedsCoil.xml`, `AddFedByComparison.xml` — real tag names like
`HrTotaliserTimer` were not reused verbatim; invented generic names used instead, per the data
boundary). All 207 converter tests pass (up from 191); `openness-cli`/golden-harness suites
unaffected.

**Live-verified against real data, 2026-07-12.** Whole-block `to-ir` on all 5 previously-`TONR`-
blocked dependency FBs (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`)
**all fully converted** — each genuinely exercising `TONR`/`ADD`/`Lt` (one of each per block,
confirmed by grep, not a lucky no-op). `MotorDOL`/`FilterUnitSystem` (the two directly grounded) both
round-trip `to-ir → to-xml → to-ir` byte-identical. All 8 of `PlantAutoControl`'s dependency FBs are now
either fully instruction-level round-trippable (5, this item plus S1 item 18) or blocked only by
the separate, already-known FC/FB parameter-interface gap (3: `TomraControlSystem`/`MotorVSDSystem`/
`AirStar`). Compiling any of these in `SampleProject` remains out of scope, same missing-tag-
table/FB-library reason already documented for `PlantAutoControl` itself.

## FC/FB parameter-interface modeling — Input/Output/InOut/Constant (S1 item 20, 2026-07-12)

Picked up per the project owner's own explicit request ("let's approach the interface side of the
FC/FBs") — the last real gap from the original 8-FB `PlantAutoControl` dependency sweep:
`TomraControlSystem`/`MotorVSDSystem`/`AirStar` all hard-error on a non-empty `Input`/`Constant` Interface
section. Planned formally in plan mode, with an unusually strong Phase 0 mandate: unlike every
prior item this session, **zero real `Input`/`Output`/`InOut`/`Constant` member XML had ever been
captured anywhere** in the codebase before this item — only that the sections existed and blocked
whole-block conversion.

**This is a block-level concept, not a call-site one.** ADR-0001 already decided `CALL` sites are
reference-only (wired arguments, no inline interface snapshot) — "the callee's own IR file is the
single source of truth for its interface." So this item extends `BlockSource`/`IrBlock` alongside
the existing `StaticMembers`/`TempMembers` (S1 item 7 Phase B); `CallStatement`/`CallArgument`/
`CallParameterNode` (S1 item 14) are untouched.

**Phase 0 grounding** (`TomraControlSystem`, `MotorVSDSystem`, `AirStar`):

```xml
<Section Name="Input">
  <Member Name="inputWord0" Datatype="Word" Remanence="NonRetain" Accessibility="Public">
    <AttributeList>
      <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
      <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
      <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
    </AttributeList>
  </Member>
</Section>
```
```xml
<Section Name="Constant">
  <Member Name="PosSpeedError" Datatype="Int" Accessibility="Public">
    <StartValue>50</StartValue>
  </Member>
</Section>
```

- **`Input`/`Output`**: real, populated on `TomraControlSystem` — same attributes as `Static`'s own
  full shape, **but genuinely missing the `SetPoint` `BooleanAttribute`** `Static` members always
  carry (confirmed: `Static` members in the same file have 4 `BooleanAttribute`s including
  `SetPoint`; `Input`/`Output` members have only 3). `DbInterfaceMembers.ParseMember`/
  `WriteMember` gained a `requireSetPoint`/`includeSetPoint` parameter (default `true`,
  `Static`'s own proven behavior unchanged) rather than a parallel type or a rewrite.
- **`Constant`**: real, populated on both `MotorVSDSystem` and `AirStar` (2 independent instances,
  identical shape) — a genuinely distinct **third** member shape: no `AttributeList` at all, no
  `Remanence`, a required `StartValue`. Neither `ParseMember` (requires `AttributeList`) nor
  `ParseBareMember` (rejects the `Accessibility` attribute) fits — new
  `ParseConstantMember`/`WriteConstantMember`.
- **`InOut`**: confirmed real as an always-present, always-empty section in all 3 grounded FBs —
  no populated example seen anywhere (neither declared nor call-site-wired).
- **A related fix found in the same path**: `Return`'s standard `Ret_Val` boilerplate was
  previously written *unconditionally* for every block. Grounding confirmed real FBs never have a
  `Return` section at all (not even an empty element) — only FCs do. `BlockSourceWriter` now only
  emits it for non-FB blocks; this was never actually exercised against a real FB's own
  Interface-section content before this item, so the asymmetry had gone unnoticed.
- **Sanitization**: `Input`/`Output`/`InOut`/`Constant` member names are freely block-owner-chosen
  identifiers, the same category as `Static`/`Temp`'s own — not given the structural exemption,
  sanitized via the same `SanitizeMember` helper.

10 new/changed converter tests (`BlockInterfaceTests.cs`) — 5 new, plus
`Parse_FbWithNonEmptyInput_HardErrors` repurposed into a positive test (its own fixture was a
hand-built synthetic shape, never sourced from a real export — corrected to the real grounded
shape at the same time, same "repurpose once the real shape is known" pattern already used twice
this session). New fixture `FbWithConstant.xml`. All 211 converter tests pass (up from 207).

**Live-verified against real data, 2026-07-12.** Whole-block `to-ir` on all 3 previously-blocked
FBs confirms **the Interface gap is genuinely closed for all three** — none hit an
Interface-section error anymore. None fully round-trips as a whole block yet, though: each now
hits one further, different, previously-unknown gap — `TomraControlSystem` hits `Swap` (unsupported
instruction), `MotorVSDSystem` hits `Access Scope="LocalConstant"` (unconfirmed scope), and `AirStar`
hits a real, confirmed **correction needed to S1 item 18's own `Mul` design**: `Mul`'s own
`SrcType` is not always the self-closing `<AutomaticTyped />` shape — `AirStar`'s own `Mul`
carries an ordinary `<TemplateValue Name="SrcType" Type="Type">Real</TemplateValue>` instead, the
same explicit shape `Convert` already uses. `FlgNetParser` currently hard-errors on this rather
than guessing; flagged as a new open item rather than fixed speculatively — needs its own
grounding pass. None of these three new gaps are addressed by this item.

### Follow-up, same day: `Mul`'s own `SrcType` fixed

Picked up immediately after this item's own live verification surfaced it — a real, confirmed
correction to already-committed S1 item 18 code, not a new capability. `FlgNetParser.
ParseMulFixedShape` now accepts either the original `<AutomaticTyped Name="SrcType" />` shape
(`MotorDOL`/`EquipmentControlSystem`) or an ordinary `<TemplateValue Name="SrcType" Type="Type">X</TemplateValue>`
(`AirStar`, `Mul UId=43`, `SrcType="Real"`) — hard-erroring only if a real instance ever carries
both or neither. No new model field needed: the existing `PartNode.AutomaticSrcType`/`SrcType`
fields already coexist generically. `MulStatementSidecar` gained a nullable `SrcType` field
(sidecar-only, mirroring `Convert`'s own — the readable IR text is unaffected either way).

5 new tests (`MulConvertTests.cs`), one new fixture (`MulWithExplicitSrcType.xml`, a standalone
rail-fed `Mul` genericized from the real `AirStar` shape). All 216 converter tests pass (up from
211). **Live re-verified against the real `AirStar` export**: the `Mul`-specific error is gone —
the block now progresses to the same `Access Scope="LocalConstant"` gap `MotorVSDSystem` also hits
(unrelated, still open, not addressed here).

## Member-level Comment (2026-07-15, Kestrel Shredder System build)

An ordinary Static/Input/Output/DB member's own "Comment" column (as seen in TIA's interface
editor) — distinct from `InformativeComment` above, which is OB-bare-system-parameter-only
machinery. Came from a direct project-owner request while reviewing the Kestrel build
(`FB_PusherControl`): network titles and comments existed, but nothing let a *member itself*
carry a why-comment, which matters for a variable read far from where it's declared.

No real donor example existed anywhere in currently-accessible Green-tier data to ground this
against (checked `FB_MotorFwdRevSystem`, a real untouched vendor FB — none of its own Static
members, including the HMI-tunable ones most likely to want one, carry a Comment). Grounded
instead by reusing XML syntax *already* proven real by the `Informative` shape directly above
(`<Comment><MultiLanguageText Lang="en-US">...</MultiLanguageText></Comment>`, confirmed live on
`OB1 Main`'s system parameters, 2026-07-14) and testing whether it also works on an *ordinary*
member with no `Informative` attribute at all. It does — live TIA import + block compile (0
errors) + export round-trip all confirmed on a real Static member, first attempt.

`DbMember` gained a `Comment` field. `DbInterfaceMembers.WriteMember`/`ParseMember` (the shared
shape a DB's own top-level members and an FB/FC's own Static/Input/Output members all go
through) read/write it; IR text is `<name> : <Type> ... COMMENT "<text>"` (`DbMemberLineFormat`),
appended as the *last* token specifically so it can be peeled off before `StartValue`'s own
leftmost-`" = "` search runs — free-text comment prose routinely contains `=` itself (e.g.
referencing a Step number), which would otherwise corrupt the start-value parse.

**Positions supported, revised 2026-07-16 (was: ordinary `WriteMember` position only).**
`WriteTypeMember` — a PLC data type's own members at any depth, plus an *anonymous-`Struct`*
member's nested fields (block/DB Static and standalone TYPE both route those through the same
method) — now writes/parses `Comment` too, instead of hard-erroring. Motivation was a real
silent-loss pair found while planning the extension: `ParseTypeMember` never read a Member-level
`<Comment>` at all (a commented UDT/anonymous-struct member lost its text crossing `to-ir`, no
error), and the old write-side guard then made the surviving direction asymmetric. One shared
`AddCommentElement` helper now serves `WriteMember` and `WriteTypeMember`, so both emit the
identical proven shape rather than two hand-kept copies.

**TYPE-side shape: LIVE-VERIFIED (2026-07-16, the recorded Portal task executed same-day).**
The Member-level `<Comment>` shape was originally proven only for the ordinary Static-member
position (2026-07-15, plus five Member-level instances in each committed genuine re-export,
`simatic-ml/test-project001/FB_PusherControl.xml`/`FB_ShredderSequencer.xml`); `WriteTypeMember`'s
placement was an informed mirror. The live proof then ran against `SampleProject`: a commented
flat UDT (`UDT_CommentProof`) **and** a commented nested-sub-struct UDT (`UDT_NestProof2`) were
imported (`--type`), type-compiled, re-exported, and converted back — **both round-tripped to
byte-identical IR, comments intact** (2 COMMENT tokens each, flat and nested positions). So the
mirrored shape is accepted by TIA `Import()`, TIA persists UDT member comments through a full
round trip, and the recursive `TypeIr` path holds up live. C-605 is satisfiable end-to-end on
interface UDTs, including one level down (the sub-struct settings design). The two proof UDTs
remain in `SampleProject` (no `--type` delete support; same documented-leftover status as
`MotorStarter_Instance`).

**Still hard-erroring, deliberately:** `WriteBareMember` (Temp members, and a *UDT-typed/
SFB-instance* structured member's own nested fields — a UDT-typed Static's inner fields take
their comments from the TYPE definition itself, per use site would be a contradiction; and the
bare `Name`/`Datatype`/`StartValue` XML shape has never been seen carrying one) and
`WriteConstantMember` (no real Constant member has ever shown one). Same
refuse-rather-than-silently-drop discipline as before; extending either needs its own grounding.

**Adjacent finding (2026-07-16, drop-path chase):** the five member comments in each committed
test-project001 FB re-export are *absent* from the committed `.ir` corpus files — that is **stale
data, not a converter drop**: running the current `to-ir` on the exact committed re-exports
reproduces the committed IR byte-for-byte *except* the member-comment tokens, which come through
correctly. `ir/test-project001/FB_PusherControl.ir`/`FB_ShredderSequencer.ir` need regenerating from
their committed XMLs (coordinator item — outside this change's file boundary).

4 tests from the original 2026-07-15 slice (`BlockInterfaceTests.cs`: IR round-trip plain and
with `=`-containing prose alongside a real `StartValue`, XML round-trip, the still-kept
`WriteBareMember` hard-error guard), plus 11 more 2026-07-16 (`PlcTypeTests.cs`: write-shape
element order, parse drop-regression, XML/IR/full round trips incl. a new
`PlcTypeWithMemberComments.xml` fixture, nested-struct recursion, the flat-parser
indent-mangling regression; `BlockInterfaceTests.cs`: anonymous-struct nested-comment XML round
trip; `SanitizerTests.cs`: see below). 491 converter tests total.

**Sanitizer (same change):** `DbMember.Comment` was the one comment kind that bypassed
sanitization entirely — real member why-prose passed through verbatim. `SanitizeMember`/
`SanitizeNestedMember` now give it the standard `SanitizeComment` treatment, keyed
`Comments["<Owner>.<Member>"]` (nested: `"<Owner>.<Member>.<Nested>"`, the `StartValues` dotted
convention). Contract effect: a commented member requires a map entry and hard-errors listing the
exact missing key otherwise — the established behavior of every other comment kind; zero effect
on comment-free flows.

## `Access Scope="LocalConstant"` (S1 item 21, 2026-07-12)

Picked up per the project owner's own explicit choice — the last of the two real gaps S1 item 20's
own live verification found (`Swap` stays deferred). Planned formally in plan mode.

**Phase 0 grounding** (`MotorVSDSystem`, `AirStar`, 4 independent instances):

```xml
<Access Scope="LocalConstant" UId="22">
  <Constant Name="MinSpd" />
</Access>
```

A genuine fourth Access shape — neither `AccessNode`'s own `<Symbol>`/`<Component>` shape nor
`ConstantAccessNode`'s `<ConstantType>`/`<ConstantValue>` shape. A bare, self-closing reference by
name, **no value at all present at the reference site**. `MinSpd`/`PulseTimerMS` are exactly the
real member names S1 item 20's own grounding already confirmed as populated `Constant`-section
members on these same two blocks — this is how a network reads back a reference to the block's own
declared Interface `Constant` member. Always single-component (never nested/dotted), always at a
`ResolveTagOrLiteralOperand`-style operand position (TON `PT` in `AirStar`; comparison operands and
`Move`'s own `in` in `MotorVSDSystem`) — never a plain Contact/Coil operand (`GraphReducer.ResolveOperand`
has no constant-lookup fallback at all, so this mattered for the design fork, even though it
didn't end up forcing the outcome either way).

**Design**: modeled as an `AccessNode` with a one-element `ComponentPath`, reusing
`DottedPath`/`FromDottedPath` completely unchanged — a single-component path already round-trips
through both with zero modification. The IR's own tag-ref text (e.g. `PulseTimerMS`) then reads
identically to the member's own declared name in that same block's `INTERFACE`/`CONSTANT` section
— a real, meaningful correlation for a reader, not just a convenient encoding choice. The one
unavoidable cost: `FlgNetParser.ParseAccess` and `FlgNetWriter`'s `AccessNode`-writing loop both
needed a scope-conditional branch, since `LocalConstant` skips the `<Symbol>`/`<Component>` shape
entirely in favor of `<Constant Name="..." />`. `GraphReducer.cs` needed **zero changes** —
`ResolveTagOrLiteralOperand` already dispatches by UId lookup, not by scope, and a
`LocalConstant`-scoped `AccessNode` flows into the same `accessByUId` dictionary as every other
scope via the existing, unmodified top-level `Parts` dispatch.

5 new tests (`TonTests.cs` — a TON `PT` fed by `LocalConstant`, matching `AirStar`'s own real
structural position), one new fixture (`WithTonPtFedByLocalConstant.xml`). All 221 converter tests
pass (up from 216).

**Live-verified against real data, 2026-07-12.** Fresh `MotorVSDSystem`/`AirStar` exports both converted
past the `LocalConstant` error completely — confirmed gone from both. Neither fully round-trips as
a whole block yet: each hits a different, new, unrelated gap — `MotorVSDSystem` hits a `<Call>` missing
its own `<Instance>` element; `AirStar` hits `Ne` (not-equal), an unsupported comparison Part Name
(the IEC family's own `Ne`/`Le`/`Gt` siblings of `Eq`/`Ge`/`Lt` — `Ne` is now the first of the
three confirmed real). Neither addressed by this item.

## `Ne` — not-equal comparison (S1 item 22, 2026-07-12)

Picked up per the project owner's own choice, immediately after asking what `Ne` was likely to be
— the IEC comparison family's own not-equal operator, alongside `Eq`(`=`)/`Ge`(`>=`)/`Lt`(`<`),
already fully supported. Well-precedented before any grounding: `ir/SPEC.md`'s own readable-form
table already had a row noting `Ne`/`Le`/`Lt` as "confirmed real Part Names... not yet built" (from
an earlier 28-block sweep), and `IrParser`'s own `ComparisonTokens` array already carried the `<>`
token, unused, waiting for exactly this.

**Grounded directly** (real `FB AirStar`, the same block `Ne` was first spotted blocking during S1
item 21's own live verification) rather than a fresh formal plan-mode cycle, given how small and
well-precedented "add a fourth comparison operator" now is (the third time this session, after
`Eq`/`Ge`'s original build and `Lt`'s own S1 item 19 addition):

```xml
<Part Name="Ne" UId="54">
  <TemplateValue Name="SrcType" Type="Type">Int</TemplateValue>
</Part>
```

Identical shape to `Eq`/`Ge`/`Lt` — same `SrcType` `TemplateValue`, same `pre`/`in1`/`in2`/`out`
ports (confirmed by tracing the real wires: `pre`→Contact chain, `in1`/`in2`→operands, `out`→Coil).

**Design**: `FlgNetParser.SupportedPartNames`/`SupportedComparisonPartNames` and `GraphReducer`'s
`OutPortFor`/`ComparisonOperator`/`TraceChain` upstream-dispatch each gained a fourth case. Every
other layer needed **zero changes** — `ChainStepSidecar.CompareStep.PartName` and
`Expr.Compare.Operator` are both carried verbatim (never derived from a hardcoded switch), so
`FlgNetBuilder`/`FlgNetWriter`/`IrSerializer`/`IrParser` already handle any confirmed comparison
Part Name generically. Repurposed the one existing test that used `"Ne"` as its own placeholder for
an unconfirmed Part Name (now genuinely real) — swapped to `"Le"` (still unconfirmed), preserving
the test's own point rather than deleting it.

4 new tests (`ComparisonTests.cs`), one new fixture (`NeFeedsCoil.xml`, genericized from the real
`AirStar` shape). All 225 converter tests pass (up from 221).

**Live-verified against real data, 2026-07-12.** Fresh `AirStar` export converted past the `Ne`
error completely — confirmed gone. Doesn't fully round-trip as a whole block yet: progresses to
`TOF` (an off-delay timer, spotted alongside `Ne` during this item's own grounding — a new,
separate, unaddressed gap, not chased here).

## `TOF` — off-delay timer (S1 item 23, 2026-07-12) — `AirStar` fully round-trips

Picked up per the project owner's own explicit choice, immediately after asking what `TOF` was
likely to be. Answered directly: an off-delay timer, IEC sibling of `TON`/`TONR`, both already
fully supported. Grounded directly against real `AirStar` (the block `TOF` was first spotted
blocking, S1 item 22's own live verification) rather than a fresh plan-mode cycle:

```xml
<Part Name="TOF" Version="1.0" UId="55">
  <Instance Scope="LocalVariable" UId="56">
    <Component Name="PulseProgramTimer" />
  </Instance>
  <TemplateValue Name="time_type" Type="Type">Time</TemplateValue>
</Part>
```

**Structurally identical to `TON`** — same `Version`/`Instance`/`time_type` shape, same
`IN`/`PT`/`ET` ports (confirmed by tracing the real wires: `IN` fed by an upstream `Ne`'s own
`out`, `PT` fed by a tag, `ET` unconnected — `OpenCon`). **No reset port** (unlike `TONR`), no
`EN`/`ENO`. No live example of `TOF`'s own `Q` being consumed in the one grounded network. The
only real difference from `TON` is semantic (off-delay vs on-delay timing behavior), which this
converter doesn't compute anyway — same "IR doesn't compute runtime semantics" reasoning already
established for `CoilKind`/`TimerKind`'s own `Tonr` variant.

**Design**: `TimerKind` gains a third variant, `Tof` — needing **zero new fields**, unlike `TONR`'s
own addition (which needed a real `Reset` field for its confirmed `R` port). Every touchpoint
`TONR` already generalized (`FlgNetParser.SupportedPartNames`, the `TON`/`TONR`/`TOF` dispatch in
`Parse`, `GraphReducer`'s `tonParts` collection filter/`TimerKindFor`/`OutPortFor`/`TraceChain`
upstream dispatch, `FlgNetBuilder.TimerPartNameFor`, `IrSerializer`'s `TimerKeywordFor`/
`TimerSidecarKind`, `IrParser`'s readable-form loop/regex/sidecar `kind` parsing) just needed a
third case added — no new mechanism anywhere. `ReduceTimer`'s own reset-resolution branch is
already gated specifically on `Kind == TimerKind.Tonr`, so `TOF` (like plain `TON`) naturally
skips it with no extra logic.

5 new tests (`TonTests.cs`, mirroring the existing `WithTon.xml` coverage exactly), one new fixture
(`WithTof.xml`). All 230 converter tests pass (up from 225).

**Live-verified against real data, 2026-07-12 — a genuine milestone.** Fresh `AirStar` export:
**`converter to-ir` succeeded completely, with no further errors at all.** `to-ir → to-xml → to-ir`
round-trips **byte-identical** — confirmed genuinely exercising both `TOF` (1 occurrence) and `Ne`
(2 occurrences) via direct grep, not a lucky no-op. `AirStar` is now the **sixth** of
`PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to end — S1 item 19 already got
`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem` there; this closes
`AirStar` — the entire chain of gaps this session's own live-verification work progressively found
for this specific block, starting from S1 item 20's `Constant` Interface section through S1 items
21 (`LocalConstant`), 22 (`Ne`), and this one. Only `TomraControlSystem` (`Swap`) and `MotorVSDSystem` (a
`<Call>` missing its `<Instance>`) remain of the original 8.

## `CALL` without `<Instance>` — a real FC call (S1 item 24, 2026-07-12) — `MotorVSDSystem` fully round-trips

Picked up per the project owner's own explicit choice, to close `MotorVSDSystem`'s own hard error:
`<Call UId="52"> is missing its <Instance> element.` Every `<Call>` grounded so far (S1 item 14,
20 real instances) called an FB and carried an `<Instance>` — this was a genuinely new shape
question, not just another "add a variant" pattern, so it went through a full formal plan
(`EnterPlanMode`/`ExitPlanMode`) with mandatory Phase 0 grounding before any code.

**Phase 0 finding, grounded against real `MotorVSDSystem` — the working hypothesis (a stateless FC call)
confirmed exactly:**

```xml
<Call UId="52">
  <CallInfo Name="Scale" BlockType="FC">
    <Parameter Name="Input" Section="Input" Type="Real" />
    <Parameter Name="Input_Min" Section="Input" Type="Real" />
    <Parameter Name="Input_Max" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Min" Section="Input" Type="Real" />
    <Parameter Name="Scaled_Max" Section="Input" Type="Real" />
    <Parameter Name="Output" Section="Output" Type="Real" />
  </CallInfo>
</Call>
```

`BlockType="FC"` — a call to Siemens' own standard-library `Scale` function, stateless by design.
**No `<Instance>` element at all** — not present-but-empty, genuinely absent: `<CallInfo>` goes
straight into its `<Parameter>` children. Parameters (5 Input + 1 Output, all `Real`) fit the
existing allowlist unchanged. `en` is rail-fed, same as every other real Call.

- **A bigger change than recent items** — not just adding a fourth case to a switch, but making
  already-shipped, non-nullable fields nullable: `CallStatement.InstancePath` (`string` →
  `string?`) and `CallStatementSidecar`'s `InstanceUId`/`InstanceScope`/`InstanceComponentPath`
  (all three → nullable, as one all-or-nothing group — not a discriminated union, since there's no
  second "kind," just presence/absence). `SimaticMl.Model.PartNode.Instance` was already
  `AccessNode?`, so no change needed at that layer.
- **`FlgNetParser.ParseCall` now checks `<Instance>`'s presence directly** (`callInfo.Element(Ns +
  "Instance") is not null`) rather than gating on `BlockType` — the two are expected to correlate,
  but nothing rules out a real counter-example either way, so this doesn't assume one implies the
  other. `FlgNetWriter.WriteCall` mirrors this symmetrically, omitting `<Instance>` when absent.
- **`GraphReducer.ReduceCall`'s own doc comment** ("Instance is required... every Call carries
  one") was factually corrected; the `call.Instance ?? throw` line became a plain nullable
  assignment, and the sidecar construction passes `instance?.UId` etc. through.
- **`FlgNetBuilder.BuildCall`'s own unconditional `new AccessNode(sidecar.InstanceUId, ...)`**
  became conditional on `sidecar.InstanceUId is int instanceUId` — this was a real, expected
  compile error in the interim state right after the sidecar fields went nullable, caught and
  fixed as part of the same pass.
- **Readable-form grammar**: the instance argument is omitted entirely when absent —
  `CALL Scale(EN := TRUE, Input := ..., ...)` instead of `CALL Scale(<instance>, EN := TRUE, ...)`.
  Disambiguation isn't actually ambiguous: `EN := ` is a reserved prefix no real instance path
  could ever collide with, so the parser checks whether the *first* split argument itself starts
  with `EN := ` (no instance) vs. requiring the *second* to (instance present) — same
  self-disambiguating style as `EnSource`'s own `ENO` sentinel and `TimerBinding`'s optional 4th
  `R :=` argument. The sidecar's `instanceuid =`/`instancescope =`/`instancepath =` lines are
  omitted together when absent, mirroring the timer sidecar's own optional `reset` lines (S1
  item 19).

6 new tests (`CallTests.cs`, mirroring the existing with-Instance coverage exactly for the
no-Instance case), one new fixture (`CallFcNoInstanceFedByRail.xml`, genericized down from the
real 5 Input + 1 Output shape to 2 Input + 1 Output, mirroring `CallWithParametersFedByRail`'s own
genericization precedent). All 236 converter tests pass (up from 230).

**Live-verified against real data, 2026-07-12.** Fresh `MotorVSDSystem` export (from the real `JOB9002_PLC`
device): **`converter to-ir` succeeded completely, no errors at all.** `to-ir → to-xml → to-ir`
round-trips **byte-identical** — confirmed genuinely exercising this item's own work via direct
grep of the converted `.ir` text: `CALL Scale(EN := TRUE, Input := IO.SpeedPerc, Input_Min := 0.0,
Input_Max := 100.0, Scaled_Min := 0.0, Scaled_Max := IO.MaxRPM, Output => IO.SpeedOutput)` — no
instance argument, exactly the confirmed shape. `MotorVSDSystem` is now the **seventh** of
`PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to end. Only `TomraControlSystem` (`Swap`)
remains.

## `SWAP` — byte-swap box instruction (S1 item 25, 2026-07-12) — `TomraControlSystem` fully round-trips, all 8 dependency FBs closed

Picked up per the project owner's own explicit choice, first grounded (Phase 0, no formal plan
mode — small, well-precedented once the design fork was resolved) then built in the same pass, to
close the last remaining gap in `PlantAutoControl`'s own 8 dependency FBs: `FB TomraControlSystem` hard-errors
on `Part Name="Swap"`, unsupported.

**Grounded against real `TomraControlSystem`** (2 independent instances, identical shape):

```xml
<Part Name="Swap" UId="34" DisabledENO="true">
  <TemplateValue Name="SrcType" Type="Type">Word</TemplateValue>
</Part>
```

Ports: `en` (Contact-gated, same `TraceChain` mechanism as every other en-gated production), `in`
(single tag-or-literal operand), `out` (single destination tag). Structurally identical to
`Convert`'s own shape (`en`-gated via `EnSource`, a single tag-or-literal input, one destination
tag via `out`, `DisabledENO="true"`) minus `DestType` — a byte-swap doesn't change the value's
type, so there's nothing to declare a destination type for, only the one `SrcType` TemplateValue
(`Word` in both real instances). Neither instance is ENO-chained; both are independently
Contact-gated.

**Design fork, resolved by the project owner before implementation**: model `Swap` as its own
`SwapStatement`/`SwapStatementSidecar` (mirroring `ConvertStatement` minus `DestType`) versus
folding it into `ConvertStatement` with a nullable `DestType`. Chose the standalone type — keeps
each source Part Name mapped to its own IR construct, same precedent as `MulKind`/`TimerKind`
staying separate variants rather than merging unrelated Part Names into one type under a
discriminating null check.

Every touchpoint mirrors `Convert`'s own exactly, minus the `DestType` field/line/group:
`FlgNetParser.SupportedPartNames`/`ParseSwapFixedShape` (validates `DisabledENO="true"` +
`SrcType`, same as `ParseConvertFixedShape` minus `DestType`); `FlgNetWriter`'s `DisabledENO` gate
list gains `"Swap"` (the existing `SrcType`-writing block is already generic, no change needed);
`GraphReducer.ReduceSwap` (`en`/`in`/`out` resolve via the exact same `ResolveEnSource`/
`ResolveTagOrLiteralOperand`/`ResolveOperand` calls as `ReduceConvert`); `FlgNetBuilder.BuildSwap`;
readable-form syntax `SWAP(EN := <expr-or-ENO>, IN := <expr>) => <dest>`, identical to `CONVERT`'s
own grammar minus the `DestType` group, in both `IrSerializer` and `IrParser` (including the
sidecar's own `swapuid =`/`srctype =` lines, minus `desttype =`).

8 new tests (`SwapTests.cs`, mirroring `MulConvertTests.cs`'s own Convert coverage — parse/reduce/
sidecar/round-trip/serialize/full-block, plus two negative tests for a missing `SrcType`/
`DisabledENO`), one new fixture (`SwapFedByRail.xml`, genericized from the real `TomraControlSystem`
shape — a single Contact-gated `Swap`, matching the real topology exactly rather than a simplified
rail-fed-only shape). All 244 converter tests pass (up from 236).

**Live-verified against real data, 2026-07-12 — closes all 8 of `PlantAutoControl`'s dependency FBs.**
Fresh `TomraControlSystem` export (from the real `JOB9002_PLC` device): **`converter to-ir` succeeded
completely, no errors at all.** `to-ir → to-xml → to-ir` round-trips **byte-identical** — confirmed
genuinely exercising both real `Swap` occurrences via direct grep of the converted `.ir` text:
`SWAP(EN := PlantControl.Test[5], IN := ControlWord0) => OutputWord0` and
`SWAP(EN := PlantControl.Test[5], IN := ControlWord1) => OutputWord1`. `TomraControlSystem` is now the
**eighth and final** of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip end to end — all
8 are now fully instruction-level round-trippable. The true TIA-cycle proof for `PlantAutoControl`
itself remains open (S1 items 16/17's own external-tags/FBs finding), unrelated to this item.

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
support — which now exists (below, S1 item 26) but hasn't been adopted here; still inline, on
purpose, not by default (`ir/SPEC.md` "Structured members" has the full tradeoff). Nested
members are structurally minimal — only `Name`/`Datatype` and an optional `StartValue`, no
`Remanence`/`Accessibility`/`AttributeList` — confirmed real against `ConveyorMotor1` (an instance of
`FB MotorDOL`: a `"TypeDOL"`-typed member with 29 scalar sub-members, several `TON_TIME`/etc.
timer members with `PT`/`ET`/`IN`/`Q`).

**Still hard error (design philosophy #10):** a doubly-nested structured member (a nested member
that is itself structured); a nested `Section` named anything but `"None"`; any non-`Static`
top-level Interface section with content.

## UDT / PLC data type support (S1 item 26, 2026-07-14)

Picked up per the project owner's own explicit ask, prompted by a real gap the `MotorDOL`
full-cycle test found: even a fully self-contained FB (zero external tag/DB/FB references) still
depends on its own declared UDT, and neither `openness-cli` nor the converter had any support for
PLC data types at all. Grounded first (Phase 0, real `TypeDOL` export — `MotorDOL`'s own
dependency) before any parser code, same discipline as every other construct in this project.

**Confirmed real shape**, root element `SW.Types.PlcStruct`:

```xml
<SW.Types.PlcStruct ID="0">
  <AttributeList>
    <Interface><Sections xmlns="...Interface/v5">
      <Section Name="None">
        <Member Name="InHand" Datatype="Bool">
          <AttributeList>
            <BooleanAttribute Name="ExternalAccessible" SystemDefined="true">true</BooleanAttribute>
            <BooleanAttribute Name="ExternalVisible" SystemDefined="true">true</BooleanAttribute>
            <BooleanAttribute Name="ExternalWritable" SystemDefined="true">true</BooleanAttribute>
            <BooleanAttribute Name="SetPoint" SystemDefined="true">false</BooleanAttribute>
          </AttributeList>
        </Member>
        ...
      </Section>
    </Sections></Interface>
    <IsFailsafeCompliant>false</IsFailsafeCompliant>
    <Name>TypeDOL</Name>
    <Namespace />
  </AttributeList>
  <ObjectList>...Comment and Title MultilingualText, both empty...</ObjectList>
</SW.Types.PlcStruct>
```

Genuinely simpler than a DB (no `Number`, no `InstanceOfName`), but with real, confirmed
differences from `DbSourceParser`'s own shape, not assumed from the surface similarity:
- The single Interface section is named `"None"`, not `"Static"` — structurally closer to a
  *structured member's own nested* section than to a block/DB's own top-level multi-section
  Interface.
- Each `<Member>` carries **no `Remanence`/`Accessibility` attribute on the tag itself** — unlike
  a DB/FB Static member, which always has both. Its own `AttributeList`'s four `BooleanAttribute`s
  (`ExternalAccessible`/`ExternalVisible`/`ExternalWritable`/`SetPoint`) match a DB member's own
  exactly, so `DbInterfaceMembers`'s existing `RequireDefaultBooleanAttributes` helper is reused
  directly — only the outer Member-tag handling differs, in new `ParseTypeMember`/`WriteTypeMember`
  methods added alongside the existing `ParseMember`/`WriteMember`.
- **`IsFailsafeCompliant`** — a new, safety-adjacent field. Refused outright (hard error, not a
  silent pass-through) if ever anything other than `"false"` — CLAUDE.md hard rule 2 extends
  naturally: never touch safety/failsafe content, even to read it. Not stored as an IR field
  (only one value ever observed) — regenerated as a validated fixed constant on write, same
  "don't store a confirmed constant" reasoning as `Move`'s own `DisabledENO="true"`.
- `ObjectList` carries **both** Comment and Title `MultilingualText` elements — a DB's own only
  carries Comment; a UDT's own Title element is present (empty), not absent.
- Members reuse `DbMember` directly (`Name`/`Datatype`/`StartValue`/`SetPoint` — `Retain`/
  `Version` default/absent, since no real UDT member has shown either yet). *Updated 2026-07-16:*
  nested anonymous-`Struct` members and member `COMMENT`s are now supported end-to-end — the XML
  side (`ParseTypeMember`/`WriteTypeMember`) had recursed since 2026-07-14, and `TypeIr` now
  recurses too (`SerializeMemberRecursive`/`ParseMemberRecursive`, the same shared helpers a DB's
  own `MEMBERS` section uses) instead of flat-parsing — the flat parser silently dropped
  `NestedMembers` on serialize and mangled a nested line's indent into the member name on parse.
  See "Member-level Comment" below for the TYPE-side comment caveat.

**New files**: `SimaticMl/PlcTypeModel.cs` (`PlcTypeSource`), `PlcTypeSourceParser.cs`/
`PlcTypeSourceWriter.cs` (mirror `DbSourceParser.cs`/`DbSourceWriter.cs` closely), `Ir/TypeIr.cs`
(`TypeIrSerializer`/`TypeIrParser`, mirroring `DbIr.cs` — readable form `TYPE <Name> / ROOTID <id>
/ [COMMENT] / MEMBERS`, reusing `DbMemberLineFormat` directly, no `NUMBER`/`INSTANCEOF` lines).
`Program.cs`'s `ConvertToIr`/`ConvertToXml`/`RunSanitize` gained a third `IsTypeXml` detection
branch alongside the existing `IsDbXml` one. `Sanitizer.ApplyToType` mirrors `ApplyToDb` — same
shared `Names`/`Tags` map tables, so a type's own declaration and a block's own `Datatype="<Type>"`
reference always agree.

**`openness-cli` side**: new `ExportType`/`ImportTypes`/`CompileType` on `OpennessGateway`, using
the real `PlcType`/`PlcTypeComposition`/`PlcTypeGroup` API (confirmed via `Siemens.Engineering.xml`
doc comments — mirrors `PlcBlock`/etc. almost exactly). **No safety refusal for types** — `PlcType`
genuinely has no `ProgrammingLanguage` property at all (confirmed by its absence from the full
reflected property list), so there's nothing for the F-prefix classifier to check.
`export`/`compile` gained `--type <name>` as a mutually-exclusive alternative to `--block`;
`import` gained a `--type` switch selecting which composition (`Types` vs `Blocks`) gets imported
into. `list`/`delete --type` deliberately deferred — not needed for this item's own goal, and
`BlockInfo`'s own Number-less/Language-less fit for a UDT is a real design question worth the
project owner's input rather than guessing overnight.

10 new converter tests (`PlcTypeTests.cs`, fixture genericized from the real `TypeDOL` shape down
to 6 representative members spanning every real datatype seen — `Bool`/`Real`/`UDInt`/`Word`/
`String`), 6 new `openness-cli` argument-parser tests. All three suites green: 254 converter (up
from 244), 79 openness-cli (up from 73), 11 golden-harness.

**Live-verified against real data, 2026-07-14.** Exported `TypeDOL` fresh from `JOB9002`
(`JOB9002_PLC` device — `TypeDOL` exists identically on both stations, `--device` needed to
disambiguate). `to-ir`/`to-xml` round-trip byte-structurally identical (only the already-accepted
DocumentInfo/whitespace/synthetic-MultilingualText-ID differences every other block/DB round-trip
already has). Sanitized (`sanitization/TypeDOL.map.json`, reusing the already-established
`TypeDOL`→`MotorIOSet` name from `sanitization/reference-project.map.json`), imported cleanly into
`SampleProject`, compiled cleanly via the new `compile --type` (`IsConsistent` quirk, same as
blocks — cleared in one call). Re-imported the already-sanitized `MotorDOL.sanitized.xml` (from
the paused full-cycle work): **the `Data type "MotorIOSet" is unknown` error is gone** — UDT
support directly resolves the gap it was built for.

**A genuinely new, separate finding surfaced immediately after**: importing `MotorDOL` itself now
fails with a *different* error — `"The elements must be sorted according to the current flow"` at
`Part UId=49` (an `RCoil`, network 3, `"Start Signal After Inhibit/Fault"`). This is a real
`FlgNetWriter` gap (Parts/Wires aren't emitted in whatever order TIA's own Import() validator
expects) — unrelated to UDT support, previously masked by the UDT blocker this item resolves, not
investigated further here. Full story: `docs/notes/stage-gates.md` ("UDT/PLC data type support").

## PLC tag table support (2026-07-14, follow-on from `PlantAutoControl` dependency grounding)

Surfaced while confirming `PlantAutoControl`'s own exact dependency list (S1 items 16/17's follow-on):
10 of its ~36 referenced top-level roots (`Tag_45`-`Tag_54`) turned out to be genuine PLC tag-table
entries, not DB members — absent from `list`'s own block enumeration entirely, and shaped as a
bare single-component `Access` (`<Component Name="Tag_45" />`, never `Table.Tag`). A construct this
project had never touched before.

**Confirmed real shape**, grounded against the actual "Default tag table" (`station_2/JOB9002_PLC`,
900+ tags), root element `SW.Tags.PlcTagTable` — genuinely simpler than a DB or UDT:

```xml
<SW.Tags.PlcTagTable ID="0">
  <AttributeList>
    <Name>Default tag table</Name>
  </AttributeList>
  <ObjectList>
    <SW.Tags.PlcTag ID="91" CompositionName="Tags">
      <AttributeList>
        <DataTypeName>Word</DataTypeName>
        <ExternalAccessible>true</ExternalAccessible>
        <ExternalVisible>true</ExternalVisible>
        <ExternalWritable>true</ExternalWritable>
        <LogicalAddress>%IW64</LogicalAddress>
        <Name>Tag_45</Name>
      </AttributeList>
      <ObjectList>...Comment MultilingualText only, no Title...</ObjectList>
    </SW.Tags.PlcTag>
  </ObjectList>
</SW.Tags.PlcTagTable>
```

- No `Namespace`, no safety-adjacent field, no table-level Comment/Title at all — simpler than
  `SW.Types.PlcStruct`'s own root.
- Each tag's own `AttributeList` children are **plain elements, not `BooleanAttribute`-wrapped**
  like a DB/UDT member — `DataTypeName`/`ExternalAccessible`/`ExternalVisible`/`ExternalWritable`/
  `LogicalAddress`/`Name`, alphabetical order.
- `ObjectList` carries only a Comment `MultilingualText` — no Title, unlike a UDT.
- `LogicalAddress` treated as structural (never sanitized) — a physical I/O address, not
  identifying business content, same category as a UId.
- A tag's own real "path" as referenced elsewhere is its bare name alone (never
  `TableName.TagName`), so `Sanitizer.SanitizeTag` looks up `map.Tags[tag.Name]` directly, not the
  `"Owner.Member"` dotted convention `SanitizeMember` uses for DB/UDT/block members.
- `PlcTag.IsSafety` exists as a C# property (per `Siemens.Engineering.dll` reflection) but **never
  appears in the exported XML at all** — confirmed by grep across the full 900+-tag real export.
  Nothing to hard-error on for this construct at the XML-parsing level; the F-block refusal already
  covers safety at the block/network level, same reasoning as the UDT section above.

**New files**: `SimaticMl/PlcTagTableModel.cs` (`PlcTagTableSource`/`PlcTagSource`),
`PlcTagTableSourceParser.cs`/`PlcTagTableSourceWriter.cs`, `Ir/TagTableIr.cs`
(`TagTableIrSerializer`/`TagTableIrParser` — readable form `TAGTABLE <Name> / ROOTID <id> / TAGS`,
one line per tag: `<tag> <id> : <DataTypeName> @ <LogicalAddress> [ACCESSIBLE] [VISIBLE]
[WRITABLE] [COMMENT "..."]`, flags shown only when true, same convention as `DbMemberLineFormat`'s
own `SETPOINT`). `Sanitizer.ApplyToTagTable`/`SanitizeTag` sanitize `Name` (table and each tag) and
`Comment` via the same shared `Names`/`Tags`/`Comments` map tables as everything else.
`Program.cs`'s `ConvertToIr`/`ConvertToXml`/`RunSanitize` gained a third `IsTagTableXml` detection
branch alongside `IsDbXml`/`IsTypeXml`. **`openness-cli` side**: new `EnumerateTagTables`/
`ExportTagTable`/`ImportTagTables` on `OpennessGateway`, walking `PlcSoftware.TagTableGroup`
exactly like `.TypeGroup`/`.BlockGroup`. `list --tagtables` enumerates tag tables instead of
blocks; `export`/`import` gained `--tagtable <name>` (export) / `--tagtable` switch (import),
mutually exclusive with `--block`/`--type` on export, `--type` on import.

7 new converter tests (`PlcTagTableTests.cs`, fixture genericized to 3 representative tags —
`Word`/`Bool`, one with a real comment), 7 new `openness-cli` tests (5 argument-parser + 2 for the
`--tagtable` import switch). All suites green: 262 converter, 96 openness-cli.

**Live-verified against real data, 2026-07-14/2026-07-13.** `export --tagtable "Default tag
table" --device JOB9002_PLC` against `JOB9002` succeeded on the first attempt (5934-line real export,
all 10 needed tags confirmed present). Rather than sanitize and recreate the real 900+-tag table
(disproportionate to what `PlantAutoControl` actually needs — see scope decision below), the real export
was converted to IR, filtered down to just the `Tag_45`-`Tag_54` lines (editing readable IR text,
not raw SimaticML — CLAUDE.md hard rule 7), and converted back to a minimal 10-tag XML. `import
--tagtable` against `SampleProject`'s root tag-table group succeeded on the first attempt;
`list --tagtables` confirmed "Default tag table" (10 tags) now present. Both `export --tagtable`
and `import --tagtable` are live-verified round trip, not just unit-tested.

**Deliberately minimal, not a general tag-table framework** (project owner's own call,
2026-07-14) — covers only what `PlantAutoControl`'s own 10 real dependency tags need. Intentional gaps,
tracked here rather than left implicit:

- **No `PlcConstant`/`PlcSystemConstant`/`PlcUserConstant` support** — only plain `PlcTag` entries.
  `PlcTagTable` exposes `SystemConstants`/`UserConstants` compositions on the Openness side (per
  reflection) that are entirely untouched; no real grounding data for either shape yet.
  `PlcTagTableSourceParser` only ever looks at the `Tags` composition group and would silently miss
  a constant if one were present in a future source table — not currently guarded against with a
  hard error, since no real example of a constant-bearing table has been seen to confirm the XML
  shape to error against.
- **No tag-table folder/grouping structure beyond the flat traversal already built for
  enumeration** — `WalkTagTableGroup`/`FindTagTableGroup` recurse through `PlcTagTableUserGroup`
  correctly for `list --tagtables` and `--group` resolution, but nothing else (no `create-group`,
  no move-between-groups).
- **`DataTypeName` is never sanitized** — treated as structural like `LogicalAddress`. Untested
  whether a tag can ever reference a UDT type name the way a DB/UDT member's `Datatype` attribute
  can; all 10 real grounding tags are built-in `Word`. If a identifying tag table ever references a
  UDT by name, that name would currently pass through unsanitized — a real gap, not yet hit.
  `Sanitizer.ApplyToTagTable`'s own test (`Sanitize_RenamesTagTableAndTags`) explicitly asserts
  `DataTypeName`/`LogicalAddress` stay untouched, documenting the current behavior rather than
  hiding it.
- **No `delete --tagtable`** — mirrors the same deliberate deferral already made for `--type` in
  the UDT section above, same reasoning (not needed for this item's own goal).
- **No `compile --tagtable`** — not attempted; a tag table's own consistency isn't a `Compile()`-
  shaped operation the way a block's is (no real Openness precedent found for it), so this wasn't
  built rather than guessed at.
- **Whole-table-only Openness import/export** — there is no way to import or export a subset of a
  real tag table directly; `PlcTagTable.Export()`/`PlcTagTableComposition.Import()` both operate on
  an entire table. This is exactly why the minimal 10-tag table was built by filtering the real
  export's own IR text down to the needed lines, rather than attempting a partial extraction
  through Openness itself.
- **Tag names/`LogicalAddress` values used for live verification are the real, unmodified
  `JOB9002` values** (`Tag_45`-`Tag_54`, `%IW64`-`%IW78`/`%QW64`-`%QW66`) — not run through
  `Sanitizer` before import. These are Siemens auto-generated placeholder-style names carrying no
  identifying content (unlike e.g. `MotorDOL`'s own semantic member names), so this was
  judged equivalent to `LogicalAddress`'s own "structural, not business content" category rather
  than a data-boundary exception — flagged here explicitly rather than left silent.

## Sidecar synthesis — `--synthesize` (2026-07-15, no donor XML needed)

Every other capability in this file reads sidecar data (Wire/Part/Access UIds — the wiring
topology TIA needs to reconstruct valid XML) off a real TIA export. There was no way to produce
one for a genuinely new network that never existed in TIA before — the only workaround, hit for
real generating a brand-new IO-mapping FC into a from-scratch project with zero existing donor
logic, was to hand-clone a real export's sidecar text line-by-line and rename tag strings within
it. Slow, error-prone, and not a real capability — flagged explicitly by the project owner as
needing a proper fix.

**`SidecarSynthesizer.Synthesize(IrNetwork) -> NetworkSidecar`** (`Ir/SidecarSynthesizer.cs`)
mints a fresh, internally self-consistent sidecar directly from a network's own `Expr` tree, via
one recursive walk with a single per-network monotonic UId counter. Safe because TIA reassigns
every Wire/Access/Part UId on its own Import()/Compile()/Export() cycle regardless of what's
written — confirmed independently three times for each of those three element kinds (see
`docs/notes/stage-gates.md`): "TIA relocates/renumbers freely, only the topology matters." The one
invariant the serializer has to get right on its own (nothing here is copying it off a real
document) is the **Parts-list order**, which TIA's `Import()` validator requires to follow signal
flow ("the elements must be sorted according to the current flow"). Ascending UId does **not**
reliably match that for a *synthesized* block — the synthesizer's own UId numbering can separate a
producer from the consumer it feeds by an independent rung (**Gap I**,
`docs/notes/converter-synthesis-gaps.md`; the real MotorVSDSystem import rejection on UId 56). So
`FlgNetWriter` emits instruction Parts in **wire-graph flow order — a DFS from the power rail along
producer→consumer wire edges** (`7694fdf`), grouping each producer with its downstream consumers
before the next rail-rooted rung; Access/Constant UIds keep their own separate ascending-UId sort.
(A real export happens to have UId==flow because TIA numbers along the flow; a synthesized block
does not, which is why the writer can't lean on UId order.)

**Scope, v1 (2026-07-15)**: `Expr.TagRef`/`And`/`Or`/`Not` and `CoilAssignment`
(`COIL`/`SCOIL`/`RCOIL`) only — the plain contact/OR-merge/NOT-merge chain, arbitrary nesting
depth. Everything else hard-errored by name, never guessed at.

**Scope, v2 (2026-07-15, same day — extended for the Kestrel Shredder build)**: adds
`Expr.Compare` (as an ordinary chain position, alongside a bare tag — confirmed real, `FC
ControlDelays`: a comparison behaves like a Contact, not an OR-merge/TON), `TON` (TON only —
TOF/TONR hard-error, matching site convention C-406 as well as being genuinely unimplemented),
`MOVE`, `MUL`/`ADD` (Multiply/Add only — Subtract/Divide hard-error), `CONVERT` (scoped to the
real Real-seconds→DInt-milliseconds HMI idiom this project's `DB_Settings` convention, C-307, is
built on — a differently-typed Convert is a separate, unimplemented case, not guessed at), and
FB/FC `CALL` — **zero-argument** (the STATIC-struct site convention, C-115/C-118) **or with wired
Input/Output arguments** (2026-07-18). A wired call takes each argument's `Type` from the callee's
own `.ir` interface via `CalleeInterfaceRegistry` (ADR-0001: the callee `.ir` is the source of truth;
the readable CALL omits types) — so the callee must be resolvable: include its `.ir` in the same
`to-xml --synthesize` batch, or pass `--project <ir-dir>`. A missing callee/param or a section
mismatch hard-errors; InOut params are not yet supported. Still out of
scope, still a deliberate, named future follow-on: WAND, SWAP, ABS, LIMIT, T_SUB, T_CONV, CALC,
MOVE_BLK_VARIANT, WAIT, FILLBLOCKI, MODBUS_MASTER/MODBUS_COMM_LOAD — hard-errors by name
(`UnsupportedSynthesisConstructException`), same discipline as v1.

**Scope, current (2026-07-20) — supersedes the dated v1/v2 snapshots above for the live subset.** The
synthesizable subset has grown well past the 2026-07-15 snapshots as the parity harness closed gaps
(`docs/notes/converter-synthesis-gaps.md`, CHANGELOG). It now covers: **TON/TONR/TOF** (Gap C),
**MUL/ADD/SUB/DIV** (SignalConditioning green), **ABS/SWAP/WAND/CALC/T_SUB/T_CONV/MOVE_BLK_VARIANT**
(SignalConditioning + DataHandling green → 14/14 corpus), **registry-typed CONVERT** (the `TagTypeRegistry`,
Gap B — no longer scoped to only the Real→DInt idiom), typed **comparisons incl. Real tag-vs-tag** (Gap E),
and a Timer's own `.Q` read **either** via an ordinary Access **or** the same-network `TimerOutputStep`
direct-wire shape (Gap G2). Instruction Parts are emitted in **wire-graph flow order**, not ascending UId
(Gap I, `7694fdf` — see the `SidecarSynthesizer`/`FlgNetWriter` note above). Genuinely still out of subset,
hard-erroring by name (`UnsupportedSynthesisConstructException`): **`LIMIT`, `WAIT`, `FILLBLOCKI`,
`MODBUS_MASTER`/`MODBUS_COMM_LOAD`**, and InOut CALL params. The CLAUDE.md command table is the one-line
current summary.

Two v2-specific design notes, both in `SidecarSynthesizer.cs`'s own doc comments in full: (1) a
Mul/Convert "EN := ENO" chained pair (the real HMI-seconds idiom, confirmed real in `MotorStarter`'s
own "HMI Times" network) is synthesized by *index-pairing* `network.Muls[i]`/`network.Converts[i]`,
not by batching all Muls then all Converts — batching would misattribute which Mul an ENO-chained
Convert belongs to once a network has more than one such pair (real fixture:
`TwoIndependentMulConvertChainsInterleaved.xml`, three in `MotorStarter`'s own real network). (2)
Access/Constant UIds are never subject to the Parts-list flow-order constraint (confirmed by
construction: `FlgNetwork` carries them in their own separate list, and v1's own interleaved
contact-then-access minting already passed live TIA verification), so a Timer's own `Q` read back
elsewhere via an ordinary Access — this synthesizer's only supported way to read a timer's output,
never the `TimerOutputStep` direct-wire shape — needs no `EnsureTimerBuilt`-equivalent inline
bookkeeping the way `FlgNetBuilder`'s own *read* side requires.

Access `Scope` is always synthesized as `GlobalVariable`, except a TON's own multi-instance
reference (`LocalVariable`, per C-407 — a timer inside a reusable equipment FB lives in that FB's
own Static section) and a CALL's own instance (`GlobalVariable`, a standalone instance DB
referenced by name — confirmed real, `FC ControlDelays`' own standalone-timer precedent: "a single
Component naming its own instance DB directly").

Two synthesis policies, both confirmed-legal real shapes, neither enforced as "the" rule by
`GraphReducer` itself (which only ever reads whichever shape a real export happens to already
have): one shared rail-wire UId per network, reused by every chain/branch that terminates at rail;
a fresh Access UId at every syntactic tag reference (no tag-text dedup — two Access elements for
one tag is already a proven-legal, previously-fixed-for real shape, `SCoil`/`RCoil` targeting one
tag via two independent elements).

**CLI**: `converter to-xml <file> --synthesize` — `<file>` is a `BLOCK`/`NETWORK` document with
*no* `SIDECAR` section (same grammar `IrParser.ParseNetworkOnly` already parses for pattern
excerpts, just wrapped in an ordinary block header). Explicit opt-in, not silent auto-detection —
`IrParser.ParseBlockWithoutSidecar` errors loudly if a real `SIDECAR` section is present anyway
(real round-trip data silently discarded in favor of synthesis is confusion, not a feature).
Without the flag, `to-xml` is completely unchanged — same requirement, same errors, every existing
test byte-for-byte unaffected (`IrParser.ParseBlock` itself was only ever extract-method-refactored
to share its header/network-parsing with the new sidecar-less entry point, never rewritten).
`FlgNetBuilder`/`FlgNetWriter`/`BlockSourceWriter`/`GraphReducer` are all completely untouched —
confirmed by full-file audit that `FlgNetBuilder.Build` never computes a UId anywhere, only ever
replays whatever a sidecar already contains, so a synthesized one satisfies it exactly like a real
one would.

## `to-ir` / `to-xml` — output path and unresolved member types (migrated from CLAUDE.md 2026-08-21)

`converter to-ir|to-xml <file> [--out <dir>]`

**FI-72 — `--out <dir>` writes the result THERE instead of BESIDE THE INPUT.** Beside-the-input
remains the default (right for the export-and-read-back loop), but it silently overwrote
hand-authored `.ir` for two agents in one day, so **an overwrite is now REPORTED when it happens.**
Convert a copy in a scratch dir when the `.ir` beside the input is authored rather than generated.

**FI-71 — `to-xml` REFUSES (exit 1, nothing written) when a member type could not be resolved.**
Pass `--project <ir-dir>`, or `--allow-blind-types` if the roots really are external. That guess is
what TIA rejects at compile on an unsigned member, and it had cost three full round trips as a
warning nobody saw.

### `to-ir --no-sidecar` — store readable-only IR (2026-07-19, ADR-0005 follow-on)

The write side of derive-always, and the only way a block legitimately enters the repo without a
stored `SIDECAR` section. `to-ir` **keeps** the stored sidecar by default; `--no-sidecar` is the
explicit opt-in to drop it.

It is not a "trust me" flag — it **verifies** before omitting (`Program.SynthesizeReadableVerified`).
For the block being converted it serializes the readable form, re-parses it, synthesizes a fresh
sidecar exactly as a later `to-xml` would, rebuilds the SimaticML, and `Normalizer`-compares that
against **the very export being converted** (so the comparison is self-consistent and
staleness-immune). Readable-only text is returned only if the two are semantically equivalent.

Two failure modes, both hard errors that leave the block with its sidecar:

- **Not synthesizable** — an unsupported construct or an unresolvable operand type
  (`UnsupportedSynthesisConstructException`, reframed with the `--no-sidecar` context and the
  underlying reason preserved).
- **Synthesizes but diverges** — synthesis succeeds and the derived XML is *not* semantically
  equivalent to the source. This is the case ADR-0005's own `CriticalCaveat` is about, and why the
  earlier auto-omit-on-`IsSynthesizable` behaviour was withdrawn: "synthesis didn't throw" is
  necessary but not sufficient (array-index locals, Gap D). See
  `docs/notes/converter-synthesis-gaps.md`.

`--no-sidecar` is valid **only with `to-ir`** — passing it to `to-xml` is a usage error. Flipping the
*default* to auto-omit-when-equivalent remains a separate, deferred owner decision.

**Tests, three tiers**:
1. `Converter.Tests/SidecarSynthesizerTests.cs` — hand-written IR text per shape (plain AND chain,
   the exact real OR-of-two-ANDs IO-mapping shape, nested OR, standalone NOT, negated leaf, bare
   leaf, `TRUE` sentinel, `SCoil`/`RCoil` sharing one rail, v2: a bare comparison feeding a Coil),
   plus hard-error cases (v2: the "still out of scope" case uses `WAND`, not TON — TON moved into
   scope), plus a direct smoke-feed into unmodified `FlgNetBuilder.Build`.
2. `Converter.Tests/SidecarSynthesizerFidelityTests.cs` — semantic-fidelity round trip reusing
   **existing** fixtures (no new ones needed): reduce a real fixture, discard its real sidecar
   entirely, synthesize a fresh one from the same `IrNetwork`, rebuild → reparse → reduce again,
   assert the re-reduced network reads back identically. v1: 6 real fixtures (including
   `OrMergeSharedPrefixBranches`/nested `OrMergeCoil` shapes). v2 (2026-07-15): 7 more —
   `GtFeedsCoil` (comparison), `WithTon`/`WithTonAndQReadBack` (TON, incl. Q read back via ordinary
   Access), `MoveFedByContact`, `MulConvertEnoChainedPair`/`TwoIndependentMulConvertChainsInterleaved`
   (the ENO-chain index-pairing logic, including the two-independent-pairs case that would catch a
   regression to batching), `CallBareFedByRail`. All pass unchanged — real fixtures already existed
   for every v2 shape, none newly built for this.
3. `tests/golden/GoldenHarness.Tests/SynthesizerLiveCheck.cs` — live-verified 2026-07-15 (v1
   scope): a genuinely new scratch block, built at runtime from `ir/reference/
   PerimeterSafetyAlarms.ir`'s own real Network 1 text (a 3-way OR-merge of negated contacts plus
   five plain single-contact assignments — real tags already part of `SampleProject`'s committed
   corpus), synthesized, imported, and compiled — 0 errors. (First attempt reused
   `patterns/output-mapping`/`patterns/input-mapping` example content instead — a real, useful
   finding, but about that content's own JOB9002-specific tags never having been imported into
   `SampleProject`, not about the synthesizer: 104 "tag not defined" errors, none of them
   wiring/topology errors.) Same "manual/live, not CI" convention as `ReferenceProjectRoundTrip`;
   deletes its own scratch block after. **v2's own Tier 3** is the Kestrel Shredder build itself
   (`test-project001`) that motivated it — every new TON/MOVE/Compare/MUL/CONVERT/CALL network that
   build writes goes through the same real import+compile gate, so a dedicated isolated v2 live
   check was judged redundant with that real work rather than skipped.

## `digest` — compact structural summary (2026-07-16, FI-15)

`converter digest <file.ir> [<file.ir> ...] [--ignore-errors] [--json] [--fingerprint]`

A deterministic, mechanically-derived orientation summary of `.ir` content — "what shape is this
block?" at a fraction of the tokens of the full IR, per FI-15 (`docs/16-future-ideas.md`). Derived
fresh on every run, never stored, so it cannot go stale. **Not** an explanation and **not** review
input — a reviewer that should read every rung still reads the full IR (the S2 exhaustiveness
lesson); this is for finding *which* file to open.

- Blocks: kind/name/number/title, interface sections (`Name : Datatype`, nested-member counts),
  CALL sites grouped by callee with distinct instance paths, per-network title + statement counts
  (`coil:2, timer:1` — statement-level only; contacts/comparisons live inside condition
  expressions and are deliberately not counted), and deduplicated global tag roots (first path
  component via `AccessNode.FromDottedPath`, so `Clock_0.5Hz`-style literal-dot names stay
  atomic).
- DBs (`GlobalDB`/`InstanceDB` + `INSTANCEOF`), UDTs, and tag tables (name : type @ address) get
  the corresponding member/tag listing.
- Same batch contract as `review`: fails on the first bad file unless `--ignore-errors` records
  it and continues; `--json` for machine use; non-zero exit only for file errors (a digest has no
  findings). Works on both exported IR (with `SIDECAR`) and freshly hand-authored, sidecar-less
  IR — dispatched by the section's presence, mirroring `ParseBlock`/`ParseBlockWithoutSidecar`'s
  own contract.
- Piloted against all 14 `ir/reference/*.ir`, a pattern example DB, and `test-project001`'s
  `FC_ControlMain` (calls/instances/network map/DB roots all correct at a glance).

### `--fingerprint` — per-network structural signatures (2026-07-20, FI-23)

Adds a `SIG: <hash>` line under each network: a normalized structural signature (12 hex chars,
SHA-256 of a canonical string). Where the statement counts above are *shape-blind* (`coil:2,
timer:1` says nothing about the rung shapes), the signature captures statement **kinds + order**
plus the canonicalized structure of every condition/operand `Expr` tree — so **N copy-pasted
networks collapse to one hash and the drifted outlier stands out**. Serves the explain-plc-block
skill's "describe the template once, verify EVERY instance" method (done by hand today).

- **Tag-name-independent**: every tag ref / bare dest / instance path abstracts to `TAG`, so two
  networks with identical shape but different wiring share a signature.
- **Literal values are KEPT** (`LIT:<value>`): a network that copied a template but changed a
  constant (`Step = 10` vs `Step = 20`) gets a *different* signature — that's exactly the
  copy-paste drift worth surfacing. (`CalcStatement.Equation` is excluded — free-text that embeds
  operand names, same reason `TagReferences` skips it.)
- **Canonical**: `And`/`Or` operands are sorted (order-independent); `Compare` stays ordered
  (`>=`/`<=` aren't symmetric); statement order within the network matters (it's part of the
  shape).
- The signature is **always present in `--json`** (a machine consumer grouping by shape wants it
  unconditionally); the flag only gates the text `SIG:` line. Default text output (no flag) is
  byte-identical to before FI-23. Same "derived fresh, orientation-only, never review input"
  posture as the rest of `digest` — a fingerprint is an explanation aid, not a policy change.
- Pilot: `FB_ShredderSequencer` (15 networks, several with repeating step-transition shapes) — all
  15 signatures distinct; notably nets 9 and 13 share the *count* summary `timer:1, move:3` but get
  different signatures because their guard structure and step literals genuinely differ. The
  fingerprint separates hand-differentiated steps that the count view can't tell apart.

## `preflight` — static checks before any Portal round trip (2026-07-16, FI-13)

`converter preflight <file.ir> [<file.ir> ...] --project <ir-dir> [--json]`

A static filter in front of the compile gate, per FI-13 (`docs/16-future-ideas.md`) — catches the
*known, recurring* import/compile error classes in milliseconds instead of a slow Portal cycle.
**Explicitly not the compile gate** (hard rule 4): passing pre-flight proves nothing about TIA
acceptance; the text output carries that disclaimer permanently. Composition only — no new
analysis: real parsers, real writers, `review`'s own rules, `TagReferences` +
`AccessNode.FromDottedPath` for resolution.

Checks, per file:

1. **parse** — the target parses at all (a parse failure is a finding, not a batch abort).
2. **convert** — blocks build through `FlgNetBuilder` (sidecar'd) or `SidecarSynthesizer`
   (sidecar-less; "not synthesizable" is a finding worth knowing before trying
   `to-xml --synthesize`); DB/UDT/tag-table content passes its writer.
3. **tag** — every global tag root (via `TagReferences`, roots via `FromDottedPath`) must resolve
   to a local declaration, a project DB, a tag-table entry, or another file *in the same batch*
   (a new block plus its new DB pre-flight together, the way they'd be imported together). One
   finding per unresolved root. This is the pipeline's "`exists`, verified by grep" rule
   (`docs/15-generation-pipeline.md`), mechanized.
4. **call** — every CALL's callee resolves to a block in the project or batch.
5. **instanceof** — an instance DB's `INSTANCEOF` target resolves to a block.
6. **review:C-xxx** — `converter review`'s findings folded in, prefixed by rule. Since 2026-08-13
   this passes the `TagTypeRegistry` preflight had already built (it requires `--project`, so one
   always existed), which is what makes the cross-file rules C-118/C-122/C-125 run here at all —
   they had recorded themselves unrunnable on every preflight before that. A rule left **unjudged**
   is folded in as a `[NOT CHECKED]` finding too: preflight's value is that passing it means
   something, and "nobody implemented that rule" must not be one of the ways it passes.
7. **flow-order** (FI-27) — each synthesized network's instruction `<Parts>` must serialize in TIA's
   DFS-from-rail wire-graph order, or a live import rejects it ("the elements must be sorted according
   to the current flow"). The `Normalizer` sorts `<Parts>` before comparing, so *no* equivalence
   oracle sees raw Part order — this validates the emitted order against the single-source-of-truth
   rule (`FlgNetWriter.FlowOrderedPartUIds`), catching a writer regression offline on any real block.

`--project <ir-dir>` is the current export (`ir/<project>/`), scanned non-recursively; files that
fail to index are surfaced as `INDEX WARNING`s. Exit non-zero on any finding. Deliberately *not*
checked in v1: UDT existence for `Datatype` strings (verbatim strings like quoted UDT names /
`Array[…] of X` would need their own parser — scope stays at the known error classes).

Piloted: all 21 `ir/test-project001/*.ir` against their own export — zero tag/call/instanceof/convert
findings, 17 genuine review findings; `ir/reference` blocks reproduce the S4 pilot's known
findings (C-003 naming, the `NodeStatusAlarms` C-301/C-501 alarm-word pair, C-406 TONR/TOF) with
zero false unresolved-tag findings.

## `tagstatus` — classify tag names exists/proposed (2026-07-18, FI-24)

`converter tagstatus <name> [<name> ...] --project <ir-dir> [--json] [--roots-only]`

Mechanizes the pipeline's anti-laundering classification (`docs/15-generation-pipeline.md`
"Artifacts"; CLAUDE.md hard rule 3). Built for `gen-architecture`'s tag-status step and for the
Build stage's run-stopping gate, both of which otherwise hand-grep the export. Composition only —
root resolution reuses `ProjectIndex` and the *same* `AccessNode.FromDottedPath` extraction
`preflight` uses; **member** resolution walks the `TagTypeRegistry` corpus (the cross-file DB/UDT
index behind the C-118 review rule), so "does `DB.member` exist" reads the same indexed facts as
"what type is `DB.member`" and the two cannot drift.

Five states, because a root can resolve while its member namespace is genuinely unknowable, and
because a subscript outside an array's declared bounds is a different fault from an invented member:

| Status | Meaning | Gate |
|---|---|---|
| `EXISTS` | the whole dotted path resolves (or a bare name resolves as a tag/DB) | pass |
| `PROPOSED` | the **root** does not resolve — a named gap the engineer creates | **fail** |
| `MEMBER-NOT-FOUND` | root resolves, members **are** enumerable, this member is absent | **fail** |
| `MEMBER-UNCHECKED` | root resolves, member namespace not enumerable (unexported UDT, or an instance-DB stub from `create-instance-db` with no member tree) | pass, reported |
| `INDEX-OUT-OF-RANGE` | every component is a real member, but a written subscript falls outside the declared bounds (`Vessel[7]` of `Array[0..3] of "UDT_Vessel"`) | **fail** |

Each name is resolved by checking the whole name first (catches bare tag-table tags whose own name
contains a dot, e.g. `Clock_0.5Hz`, and DB names), then its root, then the member path.
**Array-of-UDT members are walked through** (2026-08-05, FI-45 item 1): a subscript is stripped, the
member resolved, and the walk continues into the element type's own members, recursively
(`DB.Vessel[0].Sensor[1].Reading`). The **unindexed** form (`DB.Vessel.Reading`) resolves identically
and deliberately — it is a legitimate *type-level* question ("does every element carry this
member?"), the form a spec or binding table uses, and answering `MEMBER-NOT-FOUND` there would call
something real invented. Bounds are only ever checked against a subscript actually written, and only
when both the index and the declared bounds are integer literals — a symbolic index (`Vessel[#i]`),
`Array[*]`, or a dimension-count mismatch is left alone rather than guessed at.
`--project <ir-dir>` is the current export, scanned non-recursively; unindexable files surface as
`INDEX WARNING`s. **Exit non-zero if any name is `PROPOSED`, `MEMBER-NOT-FOUND` or
`INDEX-OUT-OF-RANGE`** — so
`converter tagstatus … --project … && <build>` is a usable "all tags exist" gate.
`--roots-only` restores root-level-only classification for the Design stage, which classifies at
root level because it designs *against* gaps rather than coding against them.

```
$ converter tagstatus DB_Input.Cycle_Start DI3_SYS_CycleStart MadeUpTag DB_Input.Invented --project ir/test-project001
DB_Input.Cycle_Start -> EXISTS (root: DB_Input)
DI3_SYS_CycleStart -> EXISTS
MadeUpTag -> PROPOSED
DB_Input.Invented -> MEMBER-NOT-FOUND (root: DB_Input)
SUMMARY: 4 name(s), 1 proposed, 1 member-not-found, 0 member-unchecked, 0 index-out-of-range   # exit 1
```

A blocking entry that can say something more precise than its status carries a short detail after an
em dash (`… -> INDEX-OUT-OF-RANGE (root: DB_Params) — Vessel[7] is outside Array[0..3] of
"UDT_Vessel"`); `--json` carries the same string as `detail`.

**History (2026-08-05):** classification used to stop at the root, so `DB_Input.Invented` reported
`EXISTS` — the gate protecting hard rule 3 blessed invented DB members, and `gen-block-new` gates its
run on that result. Found by a pipeline run that independently grep-verified every member; the
member check closes it, and `MEMBER-UNCHECKED` keeps the fix from manufacturing false gaps in the
other direction.

**History (2026-08-05, FI-45 item 1):** the member walk then turned out to stop dead at an array
subscript — `DB.Vessel[0].MaxNet` and its unindexed form both reported `MEMBER-NOT-FOUND` while a
named-UDT member resolved fine. An array of UDT is the ordinary way to express N identical vessels,
so on such a project **every** per-instance binding read as invented: hard rule 3's gate firing at
correct code, which is exactly how a gate gets trained out of use. The walk now crosses arrays at any
depth, and the bounds check it needed anyway became a finding nobody previously got
(`INDEX-OUT-OF-RANGE`). Same pass fixed `TagTypeRegistry.Resolve`, which was blind to the same shape,
so an operand inside an array of UDT now types correctly for `to-xml --synthesize` too.

## `diff` — network-level IR invariance (2026-07-18, S7 entry requirement)

### What gates, and what the verdict states (migrated from CLAUDE.md 2026-08-21)

`converter diff <old.ir> <new.ir> [--only <network>...] [--allow-header] [--json]`

`--only` asks one question: *did anything change outside the named networks that could alter what
the PLC does?*

- **A HEADER change gates** — exit 1. An **interface** member answers yes (a `Bool` → `Int` retype
  with no network touched compiles, imports, and misbehaves on the controller). A **rename or
  renumber** answers yes on identity grounds: TIA's import matches by NAME, so a rename creates a
  DUPLICATE block rather than updating one.
- **A block COMMENT cannot** — comment-only ⇒ **exit 0**, with `HEADER COMMENT CHANGED (does not
  gate)` on its own line. Non-gating is not invisible: that line is where a stale comment gets
  repaired, and equally where a correct one gets silently discarded.
- **A comment edit is never cover for a behaviour-bearing one** — both together still exit 1,
  naming the INTERFACE, not the comment.
- The JSON carries `commentChanged` and `interfaceChanged` as separate fields.

`--allow-header` is the named escape (FI-71's shape), and it means ROUTE, not DECLARE:

- `gen-block-modify-purpose` **passes it** and declares the interface delta in its hand-back.
- `gen-block-modify-fix` **must NEVER pass it** — a fix needing an interface member *is* a purpose
  change, so the answer is to route to the purpose skill, not to declare.

**The verdict states its own denominator:** `UNCHANGED REMAINDER: <n> network(s) proven identical
outside --only`. Where every network is inside the `--only` set the remainder is EMPTY and
`INVARIANCE OK` proves nothing — it says `NOTHING WAS PROVEN` instead. Same shape as
`drift-check`'s `COMPARED: <n>`: an invariance claim over an empty remainder is another
empty-is-not-clean. A clean verdict states what it EXAMINED (`…, header unchanged`), not merely
that it passed.

`converter diff <old.ir> <new.ir> [--only <network> ...] [--json]`

Mechanizes the S7 "untouched-network invariance check" (roadmap `docs/02-roadmap.md` line 57;
CLAUDE.md "Workflow for modifying existing logic"): given the before/after IR of **one block**, it
reports which networks changed and — with `--only` — proves the rest are identical. Built now, in
parallel with the coding skills, per the 2026-07-18 A-4 ruling (`docs/evidence/stage-S6.md`).

**How "identical" is defined (and why no separate normalizer is needed):** semantic equality is
equality of the sidecar-free *readable* form (`IrSerializer.SerializeNetworkOnly`), which
`IrSelfStabilityTests` already proves byte-stable. Every volatile UId (Wire/Access/Part/
CompileUnit) lives only in the `SIDECAR` section that form omits, so the UId churn a TIA re-export
produces normalizes out for free. Block-level Title/Comment/interface changes are surfaced
separately (the interface via its own sidecar-free canonical slice; block `RootUId` is deliberately
not compared, being a volatile block ID).

Either input may carry a real `SIDECAR` (a TIA export — the S7 shape: before = exported block, after =
the same block edited then reconverted) **or be sidecar-less** (a freshly-authored / validation-corpus
block); the comparison is sidecar-free either way. `--only` accepts space- or comma-separated numbers,
or repeated flags (`--only 1 2` / `--only 1,2` / `--only 1 --only 2`). Networks are matched **by
number** (S7 edits in place; a wholesale renumber would misreport — a stated limitation).

**Exit code:** with `--only`, exit 1 if any network *outside* the declared set changed/appeared/
disappeared — the invariance assertion the S7 skill gates on. Without `--only`, it's an
informational report (exit 0); a malformed IR file or a missing path exits 1.

```
$ converter diff before.ir after.ir --only 1
SUMMARY: 2 network(s): 1 changed, 0 added, 0 removed, 1 identical

CHANGED network 1 "Perimeter Safety Alarm Bit Mapping"
  - ... old readable form ...
  + ... new readable form ...

INVARIANCE OK: all changes confined to --only {1}          # exit 0
```

## `reuse-scan` — reuse-first duplicate-logic finder (2026-07-20, FI-29)

### The denominator, and when the question landed on nothing (migrated from CLAUDE.md 2026-08-21)

**Exit 0 here licenses "nothing to reuse, write a new block"** — so an exit 0 that examined nothing
is a licence issued in error. Until 2026-08-14 a `--tag` naming nothing in the corpus produced
`SUMMARY: 0 block(s) matched`, exit 0, **and no denominator at all**, so a typo'd tag or a wrong
`--project` read exactly like a thorough scan of 43 files. Note the asymmetry it already had:
`--kind` IS validated against a known set and refuses an unknown value by name; `--tag` was
validated against nothing.

Now: **`SUMMARY: … of <n> file(s) scanned` on every run**, an **`ABSENT:`** line naming roots that
appear in no block, and **exit 2 when EVERY queried root is absent** (the question landed on
nothing).

Deliberately keyed on **ALL** roots, not any — a Design-stage query legitimately mixes existing tags
with proposed ones (`gen-architecture` designs AGAINST gaps), and refusing that would be a gate
firing outside its scope.

`--tag` matching is **ROOT-level by design** (digest aggregates tag roots per block), so
`--tag DB_X.Member` matches every block touching `DB_X`; the MATCH line shows the root it actually
matched.

`converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]`

A digest-backed corpus query for the reuse-first carving pass (`gen-architecture` /
`gen-spec-analysis`): "which blocks reference tag T / implement a statement kind (a timeout, a coil,
a call) — anything I might be about to re-build?" Surfaces the **candidate** blocks a human/AI then
checks for semantic duplication; it never rules "already exists" (orientation only, matching
`digest`'s policy — `docs/15` isolation model). Composition on top of `DigestBuilder` — no new IR
traversal; tag roots are extracted with the *same* `AccessNode.FromDottedPath` primitive, so a
literal-dot tag (`Clock_0.5Hz`) is never mis-split.

`--tag` matches at **block level** (the block's tag roots — that's digest's granularity); `--kind`
matches at **network level** (a network whose statement summary contains that kind). Each flag may
repeat; within a group the match is "any", and the two groups are ANDed — so `--tag T --kind timer`
finds blocks that reference `T` *and* have a timer network. `--kind` must be one of the digest
statement labels (`coil timer move wand call arith convert swap abs limit tsub tconv calc moveblk
wait fillblk mb-master mb-commload`); an unknown kind is a hard error listing the valid set. At
least one `--tag` or `--kind` is required. **Exit non-zero if any candidate is found** (the
reuse-first alarm — `reuse-scan … && <build>` stops you to look before building new logic).

```
$ converter reuse-scan --project ir/test-project001/ --kind timer
QUERY: tags=[(none)] kinds=[timer]
MATCH: FB_ShredderSequencer (FB)  ir/test-project001/FB_ShredderSequencer.ir
  network 8 "Step 20 (DischargeStart) - Timer, Timeout Fault, And Transitions" — timer
  ...
SUMMARY: 4 block(s) matched          # exit 1
```

## `target-scan` — S6 new-block target gap-hunter (2026-07-20, FI-30)

`converter target-scan --requirements <register.md> --project <ir-dir> [--json]`

Cross-joins a `requirements.md` register against a **fresh** tag-status classification
(anti-laundering — never trusts the register's own `exists`/`proposed` marks; re-derived via the
same `ProjectIndex` primitive `tagstatus`/`preflight` use) and the as-built corpus, pre-computing
each REQ's *mechanical* disqualifiers so a human confirms a short filtered list instead of surveying
every REQ by hand. Each REQ lands in one bucket:

- **DISQUALIFIED** (mechanical, precise): class `HMI` (`hmi-only`) or `out-of-scope`; any named tag
  classifies `PROPOSED` (`proposed-tag-blocked`, with the specific names); or a linked `Q-nn` is
  `Still open` / `Partially resolved` (`q-open`, with the question + status).
- **LIKELY-IMPLEMENTED** (heuristic — kept deliberately *separate* from the mechanical layer):
  mechanically clean, but the REQ's `exists`-tags already appear in an as-built block's tag roots.
  Shown with the block(s) and the overlapping roots so the human judges the hint's strength. This is
  a soft, semantic signal — never a mechanical verdict; confirm with `reuse-scan` or a read.
- **CANDIDATE**: mechanically clean *and* no as-built block references its tags — a genuine
  new-block-only target the human then confirms.
- **WITHDRAWN**: the register marks the REQ withdrawn (carried for completeness).

The register parser is a focused line/regex reader of the register's own strict `## Format` contract
(`### REQ-nnn — <title>` headers, `- **Class:**` / `- **Notes:**` bullets, `## Open questions` with
`- **Q-nn — … <STATUS>`), not a general markdown parser. Named tags are the back-ticked identifiers
in each REQ's text/notes; classification is **root-level** (like `preflight`) — a bare interface
member may over-report as proposed, which the shown name lets the human sanity-check. **Exit
non-zero if there are zero candidates** — the fast "no clean new-block target here" signal the
manual survey used to reach by hand.

```
$ converter target-scan --requirements gen/test-project001/requirements.md --project ir/test-project001/
REQUIREMENTS: 69 parsed | CANDIDATES: 19
...
DISQUALIFIED (mechanical):
  REQ-020  [control]  Overcurrent on either motor
    reasons: q-open [Q-05 (Still open)]
  REQ-061  [out-of-scope]  E-stop stops everything (hardwired)
    reasons: out-of-scope
...
SUMMARY: 19 candidate(s), 35 likely-implemented, 15 disqualified, 0 withdrawn          # exit 0
```

## `drift-check` — ir↔simatic-ml export-drift detector (2026-07-20, FI-26)

### Scope, the denominator, and pairing (migrated from CLAUDE.md 2026-08-21)

`converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]`

`--complete` (FI-70) declares the exports dir the WHOLE picture — a fresh controller dump, not a
possibly-lagging committed corpus — so an absence FAILS in either direction: a missing `.xml`
(block not in the controller) or an `.xml` with no `.ir` (block in the controller that no `.ir`
describes). Without it, a green SUMMARY only means "everything paired matched"; the `SCOPE:` line
says which question was answered.

**`COMPARED: <n> object(s) put through the Normalizer` is printed on EVERY run.** Every other
number in the summary is a reason a comparison did NOT happen; that one is the denominator.

**Empty is not clean — three measured routes to a green over zero comparisons, all now exit 1 and
print `NOTHING COMPARED - this is not a pass`:**

1. Both dirs exist but are EMPTY → previously `0 drifted, 0 match`, exit 0.
2. EVERY `.ir` unparseable (a zero-byte file, a truncated write) → previously `1 error`, exit 0,
   because `DriftStatus.Error` never gated. `--complete` covered a missing FILE and said nothing
   about a comparison that could not RUN — the same absence one level in.
3. Either path pointed ONE LEVEL ABOVE the files — both walks are `TopDirectoryOnly`. The likeliest
   real mistake of the three.

**Pairing is by the object's DECLARED NAME, not its filename.** TIA's own name for the default tag
table contains spaces (`Default tag table`) while the `.ir` filename does not
(`DefaultTagTable.ir`), and both documents declare the real name in their own content. Pairing on
the filename made one object fail to pair and then counted it twice — `EXPORT-ONLY: Default tag
table` **plus** `SKIPPED: DefaultTagTable` — a spurious finding and a silently skipped comparison
from one naming mismatch. Identity is now read from each side's own content (`BLOCK/DB/TYPE/
TAGTABLE <name>` and the outermost `SW.*` object's `AttributeList/Name`), whitespace-normalised,
with basename as fallback. Two files claiming one identity is **`PAIRING-FAILURE`** — a THIRD
outcome, not an absence in either direction, and it always gates.

**A `MATCH` here is silent about `MemoryLayout`, and says so on every run.** `compare` refuses
(exit 2, NOT COMPARED) the very pair this reports as a MATCH. Do not "fix" this by un-ignoring the
attribute in `Normalizer` — converter output never emits it, so that breaks every
export-vs-output comparison wholesale until the converter can EMIT it.

`converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]`

Detects **silent export drift**: a fix that landed in a committed `ir/<proj>/*.ir` but was never
re-exported, leaving its `simatic-ml/<proj>/<name>.xml` stale (the exact landmine that poisoned a
synthesis-parity audit — a stale `FB_ShredderSequencer.xml` made real synth gaps indistinguishable
from noise). The committed round-trip tests deliberately *tolerate* this via an own-sidecar oracle;
this is the complementary **detector** they don't provide.

For each `ir/<proj>/*.ir`, pairs it with `<exports>/<name>.xml` by basename, rebuilds the SimaticML
in-memory (the *same* code path `to-xml` uses — `BuildXmlFromIrText`, all four `.ir` kinds), and
`Normalizer.AreSemanticallyEquivalent`-compares it to the committed export. Per block: `MATCH`,
`DRIFTED`, `SKIPPED` (no paired `.xml` — an ir-only block), `EXPORT-ONLY` (an `.xml` with no `.ir`),
or `ERROR` (the `.ir` couldn't be converted). Decoupled — no knowledge of which drift is
"known/tolerated" (that lives in the `ExportDriftDetectorTests` golden-test baseline, which excludes
the answer-key blocks). **Exit non-zero if any block DRIFTED.**

### `--complete` — when an ABSENCE is a finding (2026-08-10, FI-70)

The exports directory means two different things and the tool cannot tell them apart from the
inside. As a **committed corpus** it may legitimately lag the `.ir`, so an unpaired file is ordinary.
As a **fresh dump of the controller** an unpaired `.ir` means *this block is not in the controller*
and an unpaired `.xml` means *this block is in the controller and no `.ir` describes it*. `--complete`
is the caller's declaration that the directory is the whole picture; it never changes what is
compared, only whether an absence fails.

Two things changed here beyond the flag, both because a green result was carrying a claim it hadn't
earned:

- **`EXPORT-ONLY` is new, and is always reported.** The runner enumerates `.ir` files, so a block
  that exists only in the controller — added by hand in TIA, or left behind by a rename — could not
  appear in the report under *any* status. Silence about that half was the actual defect; `--complete`
  only decides whether it fails.
- **A `SCOPE:` line now states which question was answered**, so a clean `SUMMARY` can't be read as
  "disk and controller agree" when nothing established that.

This is the comparison half of the disk-vs-controller check. The **export half belongs in
`openness-cli`, not here** — the converter is a pure in-process file transformer that never touches
the environment, and that invariant was held deliberately (FI-24). The intended recipe is
`openness-cli` dumping every block and type to a directory, then this command with `--complete`.

```
$ converter drift-check --project ir/test-project001 --exports simatic-ml/test-project001
DRIFTED: DB_Settings  (semantic divergence between .ir and committed export)
DRIFTED: FB_ShredderSequencer  (semantic divergence between .ir and committed export)
...
EXPORT-ONLY: FB_AddedInPortal  (no paired .ir in project dir)
SKIPPED: FB_HopperBlockageMonitor  (no paired .xml in exports dir)
MATCH: DB_Alarms
...
SUMMARY: 6 drifted, 17 match, 3 skipped, 1 export-only, 0 error          # exit 1
SCOPE: comparison only. 4 file(s) had no counterpart and were NOT judged — pass --complete when the exports
       dir is a full dump (e.g. straight from the controller) and an absence should fail.
```

Two caveats measured on a real corpus rather than anticipated: compare **normalised, never bytes**
(a `--no-sidecar` disk copy differs from its export by hundreds of lines and is not drift), and line
endings vary per file, so neither side may be assumed CRLF or LF.

### 🔴 `PROVENANCE:` — whose documents were on the other side (2026-08-18)

**The fourth way this tool could report a clean run having proved nothing — and the only one that
shows a FULL denominator while doing it.** The three closed under *"empty is not clean"* all surface
as `COMPARED: 0`. This one surfaces as `COMPARED: 101`, a hundred green `MATCH` lines, and an
answer about nothing:

> `to-xml` **writes beside its input by default** (FI-72). Run it over an `ir/` directory and it
> silently replaces every real export sitting there. From then on `drift-check --exports ir/` is
> **the converter compared against its own output** — same binary, same input, same output. `MATCH`
> is a tautology, not a measurement.

Measured on a live job: **0 of 101** files in the directory being passed as `--exports` carried
TIA's `<DocumentInfo>` block, and `0 drifted / 101 match` had been recorded **four times** in the
job's own records as evidence the corpus was in sync with the controller. It was evidence of
nothing. What later appeared as *"52 drifted"* was two real converter **fixes** landing
(`1edf376` wire-endpoint direction, `f2a548a` the instance-DB `InOut` section) while the stale
output did not move with them — a **tightening**, not a regression; see the closing note in the
`compare` section.

`<DocumentInfo>` is written by TIA's exporter on every Openness export and by **nothing else** —
the converter's own writers never emit it, and the Normalizer already ignores it (it has to; real
exports carry it and converter output does not, and the two are compared for equivalence every
day). So it is an exact discriminator, and it is now counted and printed on every run:

```
COMPARED: 101 object(s) put through the Normalizer  (project=…  exports=…)
PROVENANCE: 0 of 101 export(s) compared carry TIA's <DocumentInfo>; 101 do NOT and could be
            converter output (`to-xml` writes BESIDE ITS INPUT by default)
NO TIA EXPORT WAS COMPARED — this is not a pass. …                        # exit 1
```

**The gate is NONE, not ALL, and that is deliberate.** A real corpus legitimately carries the odd
document without provenance — `simatic-ml/reference/` has four out of fifteen — and failing those
would be the gate firing outside its own question. Zero is a different claim: *not one document on
the other side of the comparison came from the controller.* A partial count is **reported and does
not gate**, so a directory drifting towards converter output is visible long before it becomes
total. `--json` carries `tiaExportCount`, `nonTiaExportCount`, `comparedNothingFromTia`, and
`fromTia` per entry.

*Consequence for fixtures:* a test standing in for a TIA export now has to declare itself one
(`TiaExportFixture.SaveAsTiaExport`). Every drift-check fixture in the suite previously built its
"export" side by calling a converter writer and saving it — precisely the state being gated.

## `compare` — the confirm loop's judgement half (2026-08-12)

### Exit codes, the layout premise, and the fenced loop script (migrated from CLAUDE.md 2026-08-21)

`converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]`

Normalizer-compares two SimaticML exports and reports WHAT differs — the path and both values —
not merely THAT something did. **Strictly stronger than `drift-check`**, which never leaves the PC:
this pair has been THROUGH TIA, so it also catches what TIA does on import and compile.

**The `MemoryLayout` premise is enforced, not assumed.** The Normalizer holds neither side to the
other's layout when one is silent — right for the committed corpus, wrong here, because two TIA
exports both declare one. A SILENT SIDE means an input is not what the loop assumes and the
comparison is quietly weaker than it looks: that is **exit 2 with the reason**, never a warning.
`--allow-silent-layout` is the named escape (FI-71's shape).

Exit **0** equivalent / **1** differs / **2 NOT COMPARED**. Exit 2 covers a missing or unparseable
file, XML carrying no `SW.*` object, the same path twice, or a walk that localizes nothing while
the Normalizer says they differ. **Empty is not clean: none of those may exit 0.**

**`tools/confirm-roundtrip.ps1` is armed and fenced (ADR-0011).** It runs
export → to-ir → to-xml → import → compile → export and calls `compare` on the first and last.
It MUTATES, so:

- **`-Arm` REFUSES any project not named in `tools/confirm-roundtrip.allowlist`** — exit 4, checked
  before the binary checks and before any directory is created, so Portal is never contacted on a
  refusal.
- Entries are absolute or **`repo:`-prefixed** so the committed file is portable across worktrees.
- **A bare project name does not work with `-Arm`** — it cannot be canonicalised, and it resolves
  to the project FOLDER.
- **Junctions are DETECTED AND REFUSED**, not half-resolved (PS 5.1 cannot resolve them), so a
  scratch project behind a junction needs its real path allowlisted.
- `-IsScratchProject` is refused BY NAME (`download-plan`'s shape). A dry run is unfenced.
- Allowlist not denylist: this machine carries ~19 real production `.ap20` projects beside the
  scratch ones, and a denylist would have to be complete and would stop being complete the next
  time a job folder arrived.

`.ps1` here is **ASCII-ONLY + CRLF**: PS 5.1 reads a BOM-less script as ANSI, a UTF-8 em dash
decodes to a quote delimiter, and the parse error points a hundred lines away from the real one.

`converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]`

The owner's **invariance principle**, stated as a loop
(`docs/notes/test-environment-build-plan.md`):

```
export ──► to-ir ──► to-xml ──► import ──► compile ──► export
   └──────────────── compare THESE TWO ───────────────────┘
```

**Strictly stronger than `drift-check`.** `drift-check` never leaves the PC, so it can only ask
whether the converter is self-consistent; this pair has been **through TIA**, so it also catches
what TIA does on import and on compile. It is the check that would have caught the `MemoryLayout`
hole `drift-check` called a MATCH: original export `Standard`, converter output silent, TIA applies
its S7-1200 default, re-export `Optimized` — first ≠ last, caught.

**The orchestration is not the work; the comparison is.** Built naively it misses the same hole:
a byte-compare fires on every reassigned Part/Wire/Access UId (TIA reassigns them unprompted) and is
unusable as a gate, while a normalizer that ignores too much is exactly how the hole survived. So
this walks the **normalized** trees — the same ones `Normalizer.AreSemanticallyEquivalent` hands to
`XNode.DeepEquals` — and reports **what** differs, not merely **that** something does. The path is a
path in the normalized document, so it names the element TIA changed:

```
$ converter compare 01-first-export.xml 06-second-export.xml
FIRST : 01-first-export.xml
SECOND: 06-second-export.xml
MEMORYLAYOUT: Standard -> Optimized  (compared — both documents declare one)
VALUE-DIFFERS  : /Document/SW.Blocks.GlobalDB/AttributeList/MemoryLayout
    first : Standard
    second: Optimized
VERDICT: DIFFERS — 1 difference(s).                                        # exit 1
```

A bare same/different verdict would send the operator to a manual XML diff through the very UId
churn the Normalizer exists to absorb, which is the state this command replaces.

**Exit 0 equivalent / 1 differs / 2 NOT COMPARED.** The third is the point (FI-44, "empty is not
clean"): a file missing or unparseable, a document carrying no `SW.*` object at all, the same path
passed twice, or a walk that localizes nothing while the Normalizer says the documents differ —
each answers the question with *nothing*, and none of them may wear the face of a pass.

### The MemoryLayout premise, enforced rather than assumed

The Normalizer compares `MemoryLayout` as an **optional assertion**: both documents must declare one
for a difference to be held against them. That weakness is deliberate and belongs to the *other*
caller — every committed `.ir` predates the emit side, so a strict compare would report ~30 blocks
drifted for the benign reason that the IR states no layout.

In the confirm loop it cannot legitimately arise: **both inputs are TIA exports, and a TIA export
always declares a layout**, so the loop gets full strictness for free. But "for free" is a premise,
and a premise nobody checks is how this class of defect keeps recurring. So a one-sided declaration
is **exit 2 with the reason**, not a warning — the same fail-closed shape as `to-xml`'s FI-71
refusal, with `--allow-silent-layout` as the named escape for deliberately comparing converter
output against an export.

### Direction awareness: a reversed wire says so (2026-08-12)

A `<Wire>`'s **first** endpoint is its **producer** and the rest are its **consumers**; nothing else
in a SimaticML document encodes direction (106 `(part, port)` pairs across 34 real exports, **zero**
appearing in both slots). The Normalizer therefore pins endpoint 0 and sorts only the tail, so a
reversal **survives** to this walk — but it did not survive *legibly*. The Normalizer also sorts
`<Wires>` **by** each wire's rendered content, and a reversal changes that content, so the flipped
wire moves to a different index and the positional pairing compares two **different** wires:

```
# BEFORE — one flipped CALL-output wire, measured
ATTR-DIFFERS   : …/Wires/Wire[2]/IdentCon/@UId       first: …ScaleMax…      second: …ScaledValue…
ATTR-DIFFERS   : …/Wires/Wire[2]/NameCon/@Name       first: ScaleFactor     second: ScaledResult
ATTR-DIFFERS   : …/Wires/Wire[3]/NameCon/@Name       first: ScaledResult    second: ScaleFactor
ATTR-DIFFERS   : …/Wires/Wire[3]/IdentCon/@UId       first: …ScaledValue…   second: …ScaleMax…
VERDICT: DIFFERS — 4 difference(s).
```

Every line of that is true and an operator can act on it — but it reads like a **rewiring**, which is
a materially different defect to go hunting for than a **direction reversal**. Now:

```
# AFTER — the same flip
WIRE-DIRECTION : …/FlgNet/Wires/Wire[3]
    the SAME endpoints with the producer and consumer roles REVERSED. A wire's FIRST
    endpoint is its producer; nothing else in the document encodes direction.
    first : port 'ScaledResult' drives operand 'ScaledValue'
    second: operand 'ScaledValue' drives port 'ScaledResult'
VERDICT: DIFFERS — 1 difference(s).
```

The endpoint descriptions deliberately **omit the normalized `UId`** — for an `IdentCon` it is the
Access content key (a whole embedded `<Symbol>` element) and for a `NameCon` it is a topology hash.
Neither reads as anything, and printing them is what made the old output unreadable.

**The classification is all-or-nothing, and that is the safety argument.** It fires only when the two
`<Wires>` containers hold the same wires **as endpoint sets** and differ solely in which endpoint is
first — given equal endpoint multisets a surviving order difference can only be at endpoint 0, since
the tail is already sorted, so *same set, different order* **is** *different producer*. That is
asserted per pair rather than assumed: a pair that differs while its producers match abandons the
classification for the whole container. Any other edit — an endpoint changed, a wire added or
removed, an attribute retyped — fails the multiset test and falls straight through to the
per-attribute walk, **unchanged**. *** Detection is never weakened to improve the message: an
unexplained real difference beats a confidently mislabelled one. *** The known cost, asserted as a
test rather than left as a surprise: a reversal arriving **alongside** another edit in the same
network still reports as per-attribute noise.

Nine tests, negative-tested by disabling the interception (the five direction tests redden; the
fallback tests stay green, which is the point of having both).

### It cannot orchestrate the loop, and that is FI-24

The loop needs Portal; the converter is a pure in-process file transformer and never shells out or
touches the environment. So the sequence lives in **`tools/confirm-roundtrip.ps1`** and only the
judgement lives here. Same split as `drift-check --complete` + `openness-cli export-all`.

## `cross-check` — whole-project cross-block facts (2026-07-20, FI-22)

### Multi-writer lines count writes from blocks that may never execute (migrated from CLAUDE.md 2026-08-21)

A path written by two blocks, one of which no OB can reach, was reported as a C-308 multi-writer
with **exactly one runtime writer** — and the call graph that settles it was already in the same
report, under SIBLING REFERENCES, unused. Lines now carry
`[NOT REACHABLE from any OB: <blocks> — <n> writing block(s) actually execute]`.

**REPORTED, NEVER SUBTRACTED:** an unreachable block is usually one somebody means to call, and
dropping its write would hide the conflict that appears the moment it is wired up.

Reachability is derived from each block's **KIND** — an OB is called by the operating system and
nothing else is. Never from a name prefix (anything can be renamed into one), and never from
"nothing calls it", which would make every uncalled block its own root, i.e. exactly the state being
detected. **A corpus with NO OB reports `unreachableKnown: false` — that is UNKNOWN, not "all
reachable".**

`converter cross-check --project <ir-dir> [--json]`

`converter review` is per-file; the review skills build cross-block tables by hand. `cross-check`
walks every block's networks across the whole export and emits the reference-graph **facts** those
tables need — as **verbatim facts the reviewer reasons over, never adjudicated verdicts** (same
discipline as the `review` dump). Substrate: `TagReferences.AllDirectedUsages` (a direction-tagged
reader/writer extractor, sibling of `AllTagPaths`) + `CrossCheck/ProjectUsageGraph` (the whole-export
reader/writer index, reused by FI-25). Four fact tables:

- **Multi-writer paths** (C-308): every full path written by >1 site, each writer with its block +
  network + Set/Reset kind — the AI distinguishes a legit S/R pair from conflicting writes.
- **Dead members** (review-functional Pass-2 dead-wiring): each member with no writer
  (consumed-but-never-written / fully unused) or no reader (written-but-never-consumed) — the
  in-cycle-lamp / dead-selector bug classes as a mechanical set-difference. Covers **global-DB
  members** (`[global-db]`, unambiguous full-path) **and FB interface-UDT members** (`[interface]`,
  FI-22 aliasing follow-up): an interface member's writers/readers are pooled across the FB-internal
  bare form and every `iDB.<suffix>` alias (via `DbSource.InstanceOfName`) before the deadness test,
  so a member written inside the FB and read via an iDB is correctly *not* dead. Real corpus surfaces
  e.g. `FB_ShredderSequencer.IO.InCycle` ("superseded, no longer wired") with no false positives.
- **Physical-IO references** (C-304): each `DI/DQ/AI/AQ…`-rooted or raw `%I`/`%Q` reference with its
  block + direction — the AI excludes the Map FCs and flags any other block touching raw IO.
- **Sibling references** (C-127): per block, the blocks it CALLs and any `iDB_*` root it references.

**Exit 0 always** — a facts provider, not a gate. On `ir/test-project001` it surfaces real signals
(e.g. `DB_Input.Pusher_Local_Remote` written-but-never-consumed; the unused overcurrent setpoints).

## `signal-sweep` — project-level residual signal coverage (2026-08-05, FI-39 check 5)

```
converter signal-sweep --project <ir-dir> --specs <equipment-specs-dir>
                       [--register <requirements.md>] [--unclaimed <unclaimed-signals.md>] [--json]
```

Computes the **denominator** (every global-DB leaf and tag-table tag in the corpus) and classifies each
signal `claimed-by-spec` / `disposed` (listed in the residual artifact's per-DB disposition tables) /
`unaccounted`.

**Its value is exactness, not a catch** — it has no oracle, and the design study grades it Medium. What
it converts is an artifact's own self-reported approximations into computed integers: on the fixture,
**201 swept** against the artifact's `~190`, with an exact per-DB breakdown and an exact residue. An
approximate denominator cannot support a completeness claim; an exact one can.

- **Qualification matters:** the disposition tables list **bare leaf names** under a `### \`Db\``
  heading, so each is qualified by its enclosing heading before comparison — without that, every leaf
  silently fails to match and the whole sweep reads as unaccounted.
- **"Mentioned in a spec" is the claimed test, deliberately coarse.** A spec that names a tag without
  binding it is a different problem, and this check must not pretend to detect it.
- **A signal dispositioned only in PROSE reads as unaccounted** (a row like `E-stop members | safety`).
  That is not a false positive so much as a fact about the artifact: prose is not machine-checkable, and
  backticking the member names is the fix. The output says so explicitly.
- **Hard errors, never silent coverage:** an empty corpus, or an `--unclaimed` artifact whose tables
  cannot be found, both fail loudly rather than reporting full coverage or blanket-unaccounted.

**Exit 1** if any swept signal is in neither a spec nor a disposition table. Whether an unaccounted
signal *implies control* is the engineer's call — the tool reports coverage, never a verdict.

## `relation-reconcile` — relation-set reconciliation + probative citations (2026-08-05, FI-39 checks 2+3)

```
converter relation-reconcile --specs <equipment-specs-dir> --ledger <code-structure.md>
                             --register <requirements.md> [--project <ir-dir>] [--json]
```

Reconciles the `(instance, relation-id)` sets across the four relation-bearing artifacts — the specs'
`- **C1**` bullets, the D2 ledger rows, the derived register's `Rel` column, and the D3 render's `[C1]`
term tags — and reports every pairwise difference.

**The key is `(instance, relation-id)`, never the bare id:** two instances' `C1` are different
relations, so a bare union across specs is vacuous and would let one instance's relation satisfy
another's.

What it buys is honest: on a clean artifact set it is a **regression guard, not a catch** — but it
converts three **hand-asserted counts inside the artifact** (`| Rows in this ledger | 174 |`) into
computed ones, which is the "computed rather than asserted" principle this tooling exists for.

- **An `ABSENT` leg is not a reconciling leg.** A stopped D3 reports `ABSENT`; reporting "0 differences"
  for an artifact that does not exist is the silent-green failure the check exists to avoid. An absent
  leg alone does **not** gate.
- **A leg that parses zero rows is a HARD ERROR**, never a clean pass — format drift is the whole risk
  here (five documented divergences between a SKILL written this month and an artifact produced this
  week), so the parsers are strict and say so loudly when the shape is missing.
- **A leg that MATCHED NOTHING exits 2** (FI-44, 2026-08-05). A leg that parsed relations but shares
  **not one key** with any other present leg took part in comparisons that examined nothing. The
  demonstrated cause was the SKILL's own D3 example: it wrote `instance: iDB_MotorDOL_Conv07` while
  every other leg is keyed on the **spec instance**, so following the documentation produced a render
  that could not intersect anything. *Absent* and *matched nothing* are different situations and report
  differently — absence is a parse fact about an artifact that is not there and still never gates;
  matched-nothing is a comparison fact about an artifact that is. Computed on the raw keys, before the
  disposition partition below narrows anything.
- **The ledger is partitioned by disposition** (FI-45, 2026-08-05). Only `rendered` and `rebind` are
  **render-bound**; `in-FB`, `discharged`, `render-BLOCKED`, `render-stopped` and
  `out-of-scope-obligation` all mean *"this relation does not become a D3 term"*. Key identity is
  required only **into** the render, against that subset; the other five report in a `ledger
  dispositions` block as *accounted for*. Before this, one such row exited 1 and `render-BLOCKED` was
  undeclarable in an artifact that had to pass its own self-check. Out of the render nothing is
  filtered: a term tagged with a relation no artifact declares is still a difference. Cells are matched
  as they really occur — `render`, `**rebind**`, `discharged (S5)`, ``render-BLOCKED `[contested]` ``,
  `in-FB + render(driver)` — and an **unrecognized** disposition is treated as render-bound (fail
  closed) and warned about.
- **Citations (check 3):** for each `verified-cross-block` row, every backticked identifier-shaped token
  in the evidence cell is classified — *resolves with N writers* / *writers all disarmed* / *declared
  with no writer in this export* / *does not resolve*. A row where **no** token resolves to a written
  member is the finding. The cell is free prose (file names, network labels, expression fragments), so
  the tool never guesses which token is "the tag" — it reports per token. It also never judges whether
  the guard *entails* the claim; that stays a human duty. Needs `--project`.
- **Denominator, always:** "no writer" means *no writer among the N blocks in this export* — partial
  exports are normal here, so that is a scope fact, not a defect.

**Exit 1** on any non-empty set-difference or citation finding; **exit 2** when a leg was compared
against nothing (that question is unanswerable, not clean). On the fixture: 174/174/174 with
`render ABSENT`, exit 0; delete one ledger row and it names `FilterUnitInst2.C5`, exit 1.

## `undriven-scan` — per-instance interface drive states (2026-08-05, FI-39 check 4)

### What counts as an instance, and exit 2 (migrated from CLAUDE.md 2026-08-21)

Per-instance is the point — `cross-check` pools across instances, so one driven instance masks an
undriven sibling. Exit 1 on undriven/disarmed; dead-interface reports, never gates.

**EXIT 2 = nothing was examined** — `--fb` names no block in the corpus, or names one with no
instances. Under FI-44 both used to exit 0, so a block that had never been written passed the check.

**An INSTANCE means an instance DB *or* a MULTI-INSTANCE** (an FB placed as a STATIC of another FB,
`ValveWater : "FB_Valve"`) — FI-50. It used to mean instance DBs only, so on a corpus following
C-132's single-STATIC-UDT house style *every* block reported "no instances" and the check examined
nothing. IEC timer/counter statics are excluded (a TON's `Q` is written by the instruction, not a
caller); **an owner with no instance DB yet reports its placements under a declaration-site path
`FB_X/Member`.**

```
converter undriven-scan --project <ir-dir> --fb <FBName>
                        [--instance <iDB> ...] [--caller <file.ir> ...] [--hints] [--json]
```

For each **instance** of an FB, which of its interface members actually receive a value. The new value
over `cross-check` is **per-instance resolution**: that command canonicalizes to `(FB, member)` and pools
across every instance — correct for its own question, wrong for this one, because if one instance drives
a member and another does not the pooled view shows the member alive and the gap disappears. A dropped
bypass on one instance of a shared block is exactly that shape.

States, all computed: **`driven`** (an armed writer) · **`disarmed`** (writers exist, all
provably-false-guarded) · **`undriven (default X)`** (no writer, but the instance DB's start value is
then the effective constant) · **`undriven`** · **`dead-interface`** (no writer **and** the FB never
reads it either — inert on both sides).

`--caller <file.ir>` merges a block outside the export into the graph (the `ProjectIndex` batch idiom),
so a freshly generated block can be analysed before import. Consequence: this runs at the check stage,
after IR exists — it reads IR, never a markdown render.

**Exit 1 on `undriven` or `disarmed` only.** `dead-interface` is reported but does **not** gate: a
reusable library block legitimately exposes optional inputs a given instance doesn't use, so gating on it
would emit findings by the hundred and train readers to ignore the output. It is a fact to cross against
a spec that required the capability — which is what makes it useful for the dropped-bypass case, where
`IO.RotationSensor` is declared, unwired, and unread on every instance.

`--hints` opts into a name-token match between an unreferenced project signal and an undriven member
(`RotationSensor` ↔ `…RotSen`). **Off by default and never part of the exit condition** — it is a
labelled heuristic, and on a real corpus it fires often enough to bury the findings it sits beside.

### Multi-instances count as instances (2026-08-07, FI-50)

An **instance** here is an instance DB *or* a **multi-instance** — an FB placed as a `STATIC` member of
another FB (`ValveWater : "FB_Valve"`) rather than given a DB of its own. Both carry per-instance state
and both can be left undriven.

Originally only instance DBs were counted, because instances were read off DB sources carrying an
`InstanceOf`. That is not a corner case under **C-132**, which makes the single-`STATIC`-UDT interface
the house style: a whole corpus can consist of nothing but multi-instances, and this scan would examine
**zero** members of **zero** instances while exiting cleanly. Found on a live job where every FB in the
project reported `no instances` — FI-44's "empty is not clean" failure re-entering through a shape
FI-44 did not consider.

Two details the implementation has to get right, both of which are the reason this is not a one-liner:

- **A multi-instance is addressed by its bare static name from inside its owner** (`ValveWater.IO.Cmd`),
  with no instance root in the text at all — unlike an iDB, which callers address by name. So the usage
  lookup is made on the local form and then **restricted to the owning block**, or two FBs that happen
  to share a static name pool each other's writers and each masks the other's gap.
- **Resolution iterates to a fixpoint**, because multi-instances nest: if `FB_SiloSequence` owns an
  `FB_SiloCycle` and is itself reached through an instance DB, the cycle's real path is
  `iDB_SeqW.Cycle`. A single pass would root it at the declaration site and lose exactly the
  per-instance resolution this command exists for.

Where the owning FB has no instance DB **yet**, the placement is reported under a **declaration-site**
path — `FB_SiloVessel/ValveWater`, with a `/` that can never be mistaken for a member path. That keeps a
block written before its caller judgeable instead of unexaminable; the alternative is reporting nothing,
which is the failure this fix exists to remove.

**IEC timer and counter statics are excluded.** A `TON_TIME`'s `Q` and `ET` are written by the timer
instruction, not by any caller, so "who drives this" is not a meaningful question for them. Left in,
they were the loudest false positive in the output — every dwell in a sequencer carries one.

### 🔴 Two write mechanisms it could not see — 136 of 228 rows were false (2026-08-18)

Found by running this across a **101-file live corpus** and hand-verifying every line. `cross-check`,
reading the **same graph**, got both joins right — two tools contradicting each other over one corpus
is what made it findable, and is the strongest available evidence that this was the scan's defect and
not a corpus quirk. **A tool wrong 60% of the time in one direction cannot be trusted in the other**,
so its output was unusable without hand-verifying every line.

Both repairs went into **`ProjectUsageGraph.UsagesReaching`** — shared, on the graph — rather than
into a second resolver here, because the join is a property of how storage nests and not of any one
check's question.

1. **An ABSOLUTE instance-path write to a MULTI-INSTANCE member was not joined.** The bullet above
   explains that a multi-instance is addressed *bare and local* from inside its owner. It is also
   addressed **absolutely, rooted on the owner's instance DB, from everywhere else** —
   `iDB_SiloVessel_SiloW.ValveDrain.IO.InHand`. Only the local form was ever looked up, so every
   write from an orchestrator, a command decoder or a startup block was invisible. Both forms are now
   resolved and unioned; the **owner restriction stays on the local form only**, and must — a bare
   `ValveDrain.IO.InHand` could belong to any FB declaring a `ValveDrain`, while the absolute form
   names one placement, which is what makes it absolute.

2. **A WHOLE-STRUCT write was not attributed to the struct's members.** `MOVE(…) => Selected` drives
   `Selected.SRID`, `Selected.TargetMC` and every other member under a key that mentions none of
   them, so a verbatim lookup found nothing. Worse than noise on the measured case: those members are
   ones the **FB itself** writes, so they should never have been in the caller-driven scope at all —
   a reader was being told a caller had failed to wire an FB's own outputs.

Measured across the corpus, old binary → new: `FB_RecipeSelect` **40 undriven → 0** (48 rows leave
the scope entirely), `FB_MotorDOL` **58 → 10**, `FB_Valve` **100 → 68**. The remaining findings are
genuine — a per-valve member written for one placement and not its siblings.

#### The floor, and why an ancestor rule without one is worse than the bug

`CALL FB_Drum(iDB_Drum_DrumA, EN := TRUE)` records a **write at the bare instance path** — the CALL
naming its own state store, not a data write of the interface. A naive ancestor rule admits it, and
then every member of every instance reads as `driven`. **Measured live while building this fix:** a
block with 20 genuine undriven members reported **168 driven and exit 0**. So `UsagesReaching` takes
a `notAbove` floor and admits an ancestor only strictly below it; the caller passes the instance
root. A test pins this, and it is the test that matters most here — a fix that makes everything look
driven passes every test that only checks the two joins.

### 🔴 `NOTHING EXAMINED` — the third shape of FI-44 (2026-08-18)

The first two ways this scan could examine nothing and exit 0 were closed by name: an unknown
`--fb`, and an FB nothing instantiates. **This is the way that was not thought of.** The block
EXISTS, it HAS an instance DB, and every one of its interface members is one the FB itself writes —
so the caller-driven scope is empty and nothing is resolved. On the live corpus **two of the three
largest blocks** reported `0 member/instance pair(s), 0 undriven` and **exit 0**, and both were read
as passes.

That is an ordinary state for a block that only publishes. It is not a pass and it is not a finding:
it is a statement that the question does not apply to this block. It now exits **2**, prints
`NOTHING EXAMINED - this is not a pass:` with the reason, and carries `scope` +
`examinedNothing` in `--json` (which previously carried neither, so a consumer reading
`members: []` + `hasFindings: false` could not tell the two apart either).

A fourth shape — `--instance` matching none of the block's instances — is closed with it.

**And the gate now keys on the ROW COUNT as well as on the scope enum.** The enum enumerates the
ways of examining nothing that somebody has already thought of, and the third one arrived three
weeks after the first two were called complete. `Members.Count == 0` is the fact rather than a
catalogue of its causes, so a fifth shape gates on arrival instead of on being noticed.

## `candidate-scan` — compute the candidate set for a requirement (2026-08-05, FI-39 check 1)

### What `--scope` matches, and exit 2 (migrated from CLAUDE.md 2026-08-21)

**`--scope` matches a path prefix OR a C-001 physical-IO equipment token.** `--scope UnitA` reaches
`DQ3_UnitA_...`, whose equipment field is in the MIDDLE and which no prefix could address.

**EXIT 2 = a scope was given and matched NOTHING** — unjudgeable, not clean. Under FI-44 it used to
exit 0, so scoping to a piece of equipment silently cleared a genuinely contested binding.

`--instance` is **PROVENANCE ONLY**: it labels the report and never narrows the set, because a
candidate set is a property of the FB class, not an instance. (Unlike `undriven-scan`, where
per-instance is the whole point.) `--phrase` is advisory only and never narrows.

```
converter candidate-scan --project <ir-dir> --fb <FBName> [--instance <name>]
                         [--scope <path-prefix> ...] [--type <TypeName>]
                         [--direction status|command|any] [--phrase <word> ...] [--json]
```

Given a requirement's target scope and the FB an instance uses, **computes every signal that could
satisfy it** — the IO half from the project's signal inventory, the FB half from that block's own
interface. Makes *"more than one candidate"* a computed fact instead of a judgement call.

Why: two defects shipped because a requirement phrase ("not faulted", "running feedback") admitted more
than one signal and the single reader who resolved it never noticed there was a choice
(`docs/evidence/PlantAutoControl-bench-autopsy.md` §2-C). Nothing computed a candidate set, so nothing could
flag the ambiguity. **This tool reports what is in scope; it never says which one the requirement
means** — that is the engineer's call.

- **Direction is COMPUTED** (does the FB write this member, or read it?), never taken from the
  interface section: on the real corpus a block's reportable status members sit under `STATIC` inside
  interface-UDT structs while `INPUT`/`OUTPUT` carry data-link words, so section-filtering gets the
  wrong answer on exactly the block the narrowed-fault-gate defect concerns.
- **`family`** reports N same-typed in-scope IO signals vs N FB members of matching direction — the
  transposition signature a 1:1 by-name-resemblance assignment silently gets wrong.
- **`--phrase` is advisory only.** Filtering by name resemblance is precisely the reasoning that
  produced the swapped-pairing defect, so the phrase subset is reported but the exit code keys off the
  **unfiltered** size.
- **`--instance` is provenance only** — it labels the report header and the JSON, and never narrows
  the candidate set (which is a property of the FB *class*, not of any one instance). Verified
  against `CandidateScanRunner.Run` 2026-08-05: the value reaches the report record and nothing else;
  neither `CollectIoCandidates` nor `CollectFbCandidates` receives it. Called out because the sibling
  `undriven-scan` is emphatically per-instance, so the same flag name reads as if it filtered here
  too.
- The header states the **denominator** (files scanned) — in a partial export an empty set is a scope
  fact, not a finding.

**Exit 1 when the candidate set size > 1** (the `tagstatus` convention) — the mechanical trigger that
makes an ambiguous binding non-discretionary. On the fixture: `--fb TomraControlSystem --scope
DiscreteInputs.Tomra --type Bool --direction status` surfaces `DiscreteInputs.TomraComFlt` **and**
`Outputs.FaultActive` (the two candidates the defect was about); `--fb FilterUnitSystem --scope
DiscreteInputs.FilterUnit1` surfaces the `Flt`/`Op`/`Ready` family.

Shared primitive: `Converter/SignalInventory/SignalInventory.cs` — a typed signal walk
(`Path, Root, Leaf, Type, IsRetain, Origin`) that keeps what `ProjectUsageGraph.CollectLeafPaths`
discards. Deliberately a **sibling** of that graph, not an extension: `TraceRunner` reads its shape
as-is and its comment declares the verbatim keying deliberate.

## `trace` — forward-pass REQ verdict tracer (2026-07-20, FI-25)

`converter trace --binding <bindings.json> --project <ir-dir> [--json]`

Layer B of the functional review's forward pass. The `review-functional` reviewer emits a per-REQ
**binding** (Layer A — the semantic anchor: REQ → concrete IR anchors); `trace` walks it over FI-22's
`ProjectUsageGraph` (read-only) + DB start values and emits **facts + candidate verdicts per hop —
never an adjudicated pass** (the reviewer confirms each candidate semantically). Binding is a
snake_case JSON list of `{ req, out_tag?, iface_member?, number?: { member, expected } }`.

Hops:
- **output-path**: `out_tag` written anywhere? no → `unimplemented (no output path)`.
- **interface-chain**: `iface_member` written anywhere? no → `broken-chain` (the in-cycle-lamp class).
- **disarmed** (v2, on output-path/interface-chain): every writer gated `NOT AlwaysTrue` (the S7 always-on
  bit) → `disarmed` (built but switched off — NOT implemented). A pure three-valued constant-fold
  (`Trace/DisarmAnalysis.cs`, `AlwaysTrue⇒true`); catches `NOT AlwaysTrue` standalone or ANDed; a bare
  `AlwaysTrue` stays armed. A mixed path (some armed) stays `ok` with the disarmed count noted.
- **number-constraint**: DB member start value vs spec → `ok` / `contradicted` / `partial` (no start value).
- **timing** (v2, `timing: { timer, seconds_member }`): the timer's PT must be the ×1000 ms form of the
  bound seconds member — keyed on the timer's *unique* PT ms-member name corresponding to the seconds
  member (`OvercurrentMediumDelayMS` ↔ `OvercurrentMediumDelay`), since the shared `Time` scratch tag can't
  distinguish which member feeds which timer. Name mismatch → `contradicted`; corresponding + the
  MUL/CONVERT idiom present → `ok`; corresponding but chain absent → `partial`; no such timer →
  `unimplemented`. **Documented limitation:** the specific MUL↔CONVERT `EN:=ENO` wire (sidecar-only) is not
  verified — a sidecar-level refinement.
- **guard-containment** (FI-36-min, 2026-08-05, `guard: { coil, must_contain: [...] }`): every signal the
  spec lists as a condition on `coil` must appear in the guard of **each** write to it. Reported **per
  writing site**, never unioned — a term present in one network and absent in another is exactly the
  multi-instance shape a union would hide. Missing → `missingterm`, naming the site; a coil with no writer
  → `unimplemented` (the output-path fact, *not* "every term missing"); a present term whose writer is
  disarmed is reported present and marked `[disarmed]`. Terms nested in a comparison count.

  This hop is a **set-difference over signal identity, not a re-interpretation** — which is the point:
  a dropped cascade-hold term shipped as a REGRESSION because the coder and the functional reviewer
  resolved the same ambiguous source the same way, so the review confirmed the error instead of catching
  it (`docs/evidence/PlantAutoControl-bench-autopsy.md`). This check cannot be defeated by how anyone reads the
  requirement. Verified on the real graded pair: against the generated block it reports
  `AirStarInst1.Outputs.ShutdownComplete` MISSING at `PlantAutoControl N3` (and the cyclone's at N19);
  against the sealed answer key, the same binding reports `ok`.

**Exit 0 always** — a facts provider. Examples on `ir/test-project001`: a binding for REQ-004 shows
`DQ5_DIS_Run` written by `FC_Outputs` and `DischargeConveyorTimeout`=10.0 matching spec;
`IO.HandReverse` (`:= NOT AlwaysTrue`) → disarmed; `OvercurrentMediumTimer` + `OvercurrentMediumDelay` →
timing ok, but + `OvercurrentHighDelay` (wrong member) → contradicted.

## `ir-hash` — stable readable-IR content hash (2026-07-20, FI-17)

`converter ir-hash <file.ir> [<file.ir> ...] [--json]`

Emits `SHA-256(SerializeBlockReadable(block))` per block — a stable content key over the *readable* logic
only (interface + networks), so it invalidates on any logic/interface/comment change but is **immune to
SIDECAR/UId churn** (a re-export doesn't change it). The key for **FI-17 explanation sidecars**
(`docs/notes/explanation-sidecars.md`): a cached explanation stamps `derived-from: <ir-hash>`, and every
consumer recomputes the hash and discards the cache on mismatch (hash-on-read, the ADR-0005 discipline).
Deliberately **not** `digest --fingerprint` (that abstracts tag names → a rename wouldn't invalidate). Exit
1 on a missing/unparseable/non-block file.

## `claim` / `claims` — reserve a shared resource before writing IR (2026-08-07, FI-65 component 1)

```
converter claim  --project <ir-dir> --claims <dir> --agent <id> --kind <k>
                 (--value <v> | --allocate [--type FB|FC|OB|DB] [--floor <n>] [--in <word|block>])
                 [--purpose <text>] [--json]
converter claims --project <ir-dir> --claims <dir> [--check] [--agent <id>] [--json]
converter claims --project <ir-dir> --claims <dir> --release --agent <id> (--kind <k> --value <v> | --all) [--force]
```

Lets several agents work one project without stepping on each other. The collisions this prevents are
decided **while writing IR** and never reach TIA: two agents each scan the corpus for the next free FB
number and both pick 51; both verify alarm bit `%X9` is free and both take it; both append "network 8"
to the same shared FC. Each agent's own `converter diff --only` invariance check passes — the conflict
exists only between them. See `docs/16-future-ideas.md` FI-65.

**Six kinds, two semantics.** *Allocation* (`block-number`, `alarm-bit`, `db-member`, `block-network`,
`tag`) reserves something not yet used, and is refused if the corpus already uses it. *Exclusive*
(`block-edit`) reserves write access to something that does exist, and is refused only if another agent
holds it.

**`--claims <dir>` is required** (or `LADDER_CLAIMS_DIR`) and there is deliberately **no default**. It
must be a directory shared by every agent on the project: agents run in separate git worktrees, so a
per-worktree claims directory is always empty, grants every claim, and turns the registry into a no-op
that looks like success — FI-44's "empty is not clean" in its purest form.

**Acquisition is atomic** — a claim is written to a temp file and `File.Move`d into its slot, so the
filesystem decides the winner and a claim file is never observed half-written or locked. One file per
claim, never a table: a shared table would be a read-modify-write race between processes and a merge
conflict per claim once committed. Claims are machine state and are never committed.

**`--allocate` takes the lowest free value atomically** and prints what it took. There is no read-only
"suggest" mode on purpose: a non-binding suggestion is the exact race the tool removes — two agents are
both told "FB51 is free", both act on it, and one finds out only after doing the work.

**`--check` gates on conflicts only.** A *fulfilled* allocation claim (the agent wrote the block, so the
resource now exists) is the normal end state and is reported without gating — a check that failed on
success would be ignored on failure. What gates: an exclusive claim on a block that no longer exists,
a malformed value, and the **cross-kind conflict the filesystem cannot see** — agent A holding
`block-edit FC_ControlMain` while agent B holds `block-network FC_ControlMain:8`. Those are two
different values, so both acquisitions legitimately succeed; only the check relates them.

Stale claims are reported, never auto-released — an agent can legitimately hold one across a long
stage, and expiring a claim out from under live work causes the collision the registry exists to stop.
Releasing another agent's claim needs `--force` and says that it was forced.

**Exit codes:** `0` acquired / clean · `1` refused, or `--check` found a conflict · `2` unusable input
(no `--claims`, missing project, unknown kind, empty corpus). The 1-vs-2 split is for the calling
agent: 1 is a real answer ("someone has it, pick another"), 2 means nothing was decided.

**Boundary:** claims prevent only the kinds someone enumerated. The six cover every collision source
this repo has evidence for; a resource class outside them is still an undetected collision.

## `MemoryLayout` — optimized vs standard block access, carried through the round trip (2026-08-12)

**The measured defect.** A real standard-access global DB was round-tripped
`export → to-ir → to-xml`:

```
original export : <MemoryLayout>Standard</MemoryLayout>
regenerated xml : NO MemoryLayout element at all
drift-check     : *** MATCH ***
```

Re-importing that regenerated XML states **no opinion** about layout, so TIA applies the S7-1200
default — `Optimized` — and the DB becomes **invisible to classic S7comm** (not an error: the block
is simply absent, and it fails at the first *data* read). **Every check in the pipeline stayed
green**: `drift-check` was structurally blind because `Normalizer` had `MemoryLayout` on its
volatile-element list, import did not error, compile did not error. The first symptom was a runtime
Modbus status code. Background on the Openness side, including the re-import revert this pairs with:
`src/openness-cli/README.md`, `block-layout`.

**What it is.** The `<MemoryLayout>` element inside a block's own `<AttributeList>`, immediately
before `<Name>` in every real export. **Not DB-only** — it is backed by `PlcBlock.MemoryLayout` and
is present on every FC, FB and OB in the committed `simatic-ml/` corpus, so both `DbSourceParser`/
`DbSourceWriter` and `BlockSourceParser`/`BlockSourceWriter` carry it. The value set is closed and
confirmed (`Standard`, `Optimized` — the whole of `Siemens.Engineering.SW.Blocks.MemoryLayout`);
anything else is a hard error on both the XML and the IR side rather than a value passed blindly to
TIA. IR grammar: a `MEMORYLAYOUT <value>` line — `ir/SPEC.md`, `BLOCK` and `DB` file shapes.

**Absent in IR emits nothing, and that rule is not optional.** Every `.ir` in the repo predates this
and carries no layout; emitting a default for those would silently restate the layout of every DB in
the corpus — a broader silent corruption than the one being fixed. So: present in the IR, emit it;
absent, emit nothing, exactly as before. The defect closes because an `.ir` that *came from* an
export now carries the attribute.

**Order of the change: emit first, un-ignore second — landed in one commit.** `Normalizer` no longer
ignores the attribute, so a layout **difference** is now reported instead of silently matched. It
compares as an *optional assertion*: a difference between two documents that **both** declare a
layout is real; a document declaring none is stating no opinion and is not held to the other's
value. That second half is what keeps the change from turning the corpus red for a benign reason —
**measured**: comparing strictly instead reports 30 committed blocks as drifted (19 in
`test-project001`, 11 in `reference`) against a known-drift baseline of 6, because their `.ir`
predates the emit side while their exports carry `Optimized`. The comparison sharpens by itself as
blocks are re-derived from their exports.

**It cannot be derived — checked, not assumed.** The alternative considered was *computing* a
standard layout from member order and types (bools packing within a byte, a partly-used byte not
reused) rather than storing a flag. The evidence removes the question: **SimaticML carries no
per-member byte/bit offsets at all.** A real `WithDefaults` export of a standard-access DB has zero
occurrences of an offset or address, and across the 900 `<Member>` elements in the committed corpus
the only attributes that exist are `Name`, `Datatype`, `Remanence`, `Accessibility`, `Version`,
`Informative`. So there is no offset channel for TIA to infer a layout from on import, and none for
a derivation to be verified against — the CPU memory layout is computed by TIA and never serialized.
This element is the only carrier the file format has. A derivation would still be worth building for
a PC-side S7comm harness, which needs real absolute addresses; that belongs to the harness, needs
the even-byte alignment rule for `WORD` and larger, and must be grounded against offsets TIA
actually displays.

18 converter tests (`Converter.Tests/MemoryLayoutTests.cs`), fixture
`Fixtures/GlobalDbStandardMemoryLayout.xml` — the grounding export's exact XML shape with invented
DB/member names. Headline test: take the real `Standard` export, `to-ir`, `to-xml`, assert the
regenerated XML still says `Standard`. All 908 converter tests pass (up from 890); the offline
golden-harness suites (39, including `ExportDriftDetectorTests`) stay green.

## Fixed-shape instruction registry, unconnected ports, and two one-way-IR fixes (2026-08-12)

Four defects, found together on ONE real exported block — a genuine TIA V20 export of a live
S7-1200 (classic 1214C) Modbus TCP FC — each of which alone stopped it round-tripping. The fixture
`Converter.Tests/Fixtures/FixedShapeModbusTcpBlock.xml` **is** that export, structurally element
for element (415 lines in both), with four identifier lines replaced by invented ones per the data
boundary.

**1. The supported instruction spellings were the wrong ones.** `FlgNetParser.SupportedPartNames`
carried `Modbus_Master`/`Modbus_Comm_Load`; TIA emits `MB_MASTER` Version="2.2" and `MB_COMM_LOAD`
Version="2.1" — and, on a sibling FB, `MB_SERVER` Version="5.3". *Not one whitelist entry was a
name TIA actually produces here.* Rather than hand-write a third and fourth production, this added
**`Converter/SimaticMl/FixedShapeInstructions.cs`**: a `(Part Name, Version) → ordered port list
with direction` registry, driving one reducer (`GraphReducer.ReduceFixedShape`), one builder
(`FlgNetBuilder.BuildFixedShape`), one serializer block and one parser block. Adding the next
instruction of this shape is a table entry.

The registry exists because **section and datatype do not exist at the call site** — the network
XML states only a port NAME on `<NameCon>`. Wire order distinguishes read from write; it cannot
distinguish an input from an InOut, and says nothing about a port wired only to an `<OpenCon>`.
**Version participates in matching and an unknown version is refused** (2.1 vs 2.2 vs 5.3 across
three real families): applying one version's port template to another version's wiring would
convert, import, and misbehave on the controller. The older `Modbus_*` pair is **kept, not
replaced** — see `FixedShapeInstructions`' own doc comment for the live-import evidence that its
names are real, and why neither family aliases the other.

**2. An unconnected INPUT port could not be reduced.** `<OpenCon>` on an *output* already worked
(TON's `ET`, `Modbus_Comm_Load`'s `FLOW_CTRL`/`RTS_ON_DLY`/`RTS_OFF_DLY`); an unconnected input was
`NonReducibleNetworkException: REQ wire 36 for UId=22 has no IdentCon source`. The registry path
handles both directions uniformly at no extra cost, and makes the state **visible in the readable
IR** (`PORT := OPEN`, `DONE => OPEN`) rather than sidecar-only — a port an AI cannot see is a port
an AI cannot write back. Three states are distinguished: wired, `OPEN` (wired to `<OpenCon>`), and
absent from `<Wires>` entirely (no argument at all). An `OPEN` port names no tag, so it contributes
nothing to `tagstatus`/`preflight`; a tag genuinely named `OPEN` on such a port is a hard error.

**3. `MOVE_BLK_VARIANT` was IR the AI could read and never write back.** `to-ir` emitted
`constant P#DB99.DBX0.0 BYTE 2 = 21 Any`; `to-xml` on the converter's own output threw
`Malformed sidecar constant line` — the sidecar value was matched as `\S+` and a classic S7 area
pointer contains spaces. Fixed generally (greedy value against the anchored ` = <uid> <type>`
tail), not `Any`-specifically: `String`/`WString`/`DT`/`DTL`/`Pointer` values can all carry spaces,
and a value containing ` = ` survives too. Backward compatible with every sidecar in the repo.
Alongside it, `P#`-prefixed text now parses as a **literal** rather than an `Expr.TagRef` (as `T#`
already did), so an area pointer stops being reported as a proposed tag.

**4. A comparison's literal was typed by magnitude, not by the comparison's own type.** FI-55 fixed
the `SrcType` half and left the literal half, so `UInt` registers compared to constants emitted
nine `Int`/`DInt` literals against `SrcType="UInt"` boxes — the same mismatch TIA rejects. `SrcType`
is now resolved first and passed to both operands as `constantTypeOverride`, the rule
`MUL`/`ADD`/`CALC` operands already followed. **The committed corpus contains zero `UInt`/`Word`
comparisons**, which is why nothing had hit it; the guards are parameterised over
`Word`/`USInt`/`UDInt`/`SInt`/`LInt` too. FI-54's duration-literal carve-out is untouched.

`MB_SERVER` 5.3 is deliberately **not** in the registry yet: its port list is characterised, but the
block carrying it also needs `Array[…] of Struct` and doubly-nested structured interface members
that this converter does not model, so a template added now could not be exercised end to end.
*(Closed out the same day — see the next section.)*

**Confirm loop, offline half**: `to-ir → to-xml → converter compare` against the original export —
`EQUIVALENT` for each of the three networks individually and for the whole block; the IR text is
byte-identical across a second round trip. 968 converter tests pass (up from 929); the golden
harness stays green at 39. No Portal, no import, no compile, no download — that half is the
owner's, with a person present.

## MB_SERVER made expressible — and the silent losses found on the way (2026-08-12)

Owner ruling: **no IR the AI cannot change.** A block the converter cannot express is a block the AI
can never modify, so "author the `MB_SERVER` call by hand in TIA" was withdrawn. Grounded on a
genuine TIA V20 export of an S7-1200 (classic 1214C) Modbus TCP FB, 55,783 bytes, preserved
byte-exact and converted only in copies.

Five gaps were scoped. Two more turned up while closing them, **both silent**:

1. **`Array[1..10] of Struct` with inline nested members** — hard error. The direct-nested-`<Member>`
   gate accepted only the literal `Struct`. An array of an anonymous struct nests identically because
   it *is* the same struct, dimensioned. Widened to `Struct` **or** `Array[…] of Struct`, and to
   nothing else.
2. **Doubly-nested structured members** — hard error, and on the critical path: `CONNECT` must point
   at a `TCON_IP_v4`, which nests an `IP_V4`, which nests an `Array[1..4] of Byte`. A `<Sections>` at
   the nested position now has three dispositions — collapse a *quoted* named type (FI-56,
   unchanged), still refuse an anonymous `Struct` (its expansion is its only definition), and
   **recurse into and keep** an unquoted system structured type. Keeping matters: `IP_V4`'s expansion
   holds the remote IP address, so collapsing would trade one silent loss for another.
3. **The `MB_SERVER` 5.3 Part template** — registered, now that the block carrying it round-trips.
   Ports read off the export's own `<Wires>`. `MB_HOLD_REG`/`CONNECT` are genuinely InOut and are
   `Input` in the template, which is correct rather than a compromise: they are wired as ordinary
   symbolic `<Access>` operands in normal input order, and the whole document contains **zero**
   `<Parameter Section=…>` elements.
4. 🔴 **The multi-instance member writer dropped `Version` — silently.** It converted without error
   and came back as `<Member Name="…" Datatype="MB_SERVER" Accessibility="Public" />`: a
   **versionless** instance declaration, from a source stating `Version="5.3"`. Cause: a
   multi-instance was *read* as a "bare parameter", a shape with nowhere to put a `Version` or an
   `AttributeList`. It is not one — it is an ordinary Static member that merely never carries
   `Remanence`, and that single difference now lives on the write side (`WriteMember`'s
   `omitRemanence`). The `TON_TIME` member beside it kept its `Version="1.0"` only because it carries
   `Remanence` and never took that path.
5. 🔴 **`<Subelement>` array start values were dropped — silently, and this is the finding of the
   day.** The element name appeared **nowhere in `src/converter`**: not in code, not in tests, not in
   docs. No parse path read it, no write path emitted it, and no check objected, because members had
   no unknown-child guard at all. **The block reached `exit 0` with 182 start values gone** — its
   entire per-node configuration table: IP addresses, node numbers, register addresses, lengths, and
   the remote IP inside `RemoteAddress.ADDR`. *Converted without error* is not *converted correctly*.
   New IR grammar `[<index path>] = <value>` (`ir/SPEC.md`), supported on all three member shapes,
   plus a permanent unknown-child refusal so the next one is an error rather than an omission.
6. **Parameter-section members lost their `AttributeList`** — FI-59's remedy was wider than its
   evidence. The rejection it fixed named `Remanence` and only `Remanence`; the bare shape it reached
   for also drops the `AttributeList`, which nothing asked for. TIA's own export carries
   `Remanence` **and** a 3-attribute `AttributeList` on an FB Input parameter (and `FB TomraControlSystem`
   independently showed the same shape in 2026-07). So the *shape* now comes from the member's own
   `IsBareParameter` and `Remanence` is suppressed **by section** — which is FI-59's actual fix, and
   still protects hand-authored IR that never learned `BAREPARAM`.
7. **A multi-line network comment was a permanent hard error.** The format defined no `\n` escape, so
   the serializer refused one — correct about the risk (the document is split on `\n` before quoted
   strings are parsed) but leaving no way to represent a real comment. The export has one: three
   lines of engineer's notes. `to-ir` rejected the **whole block** over its documentation. `\n`/`\r`
   now escape; the value still occupies one physical line. The five copy-pasted `EscapeString`
   implementations and two of its inverse were collapsed into one `IrStringEscape` first, so the two
   halves cannot drift apart.

**Confirm loop, offline half.** `to-ir → to-xml → converter compare` against the preserved original:
**2 differences**, both deliberate and both pre-dating this work — the multi-instance's inline
expanded interface (the *callee's* declaration, discarded by design and re-emitted by TIA) and
`Remanence` on a parameter (which TIA exports but refuses at import). A whole-document element tally
confirms every other `n→0` is a `Normalizer` volatile: subelements **182 → 182**, and the `Member`
`157 → 63` / `Section` `43 → 13` / `Sections` `17 → 8` deltas are accounted for **exactly** by that
one discarded expansion (94 / 30 / 9). `to-ir → to-xml → to-ir` is byte-identical. 996 converter
tests pass (up from 968), golden harness green at 39, and `drift-check` over the committed corpus
reports the identical 6 pre-existing drifts before and after — no corpus regression.

**Also fixed here**: `compare`'s own `MEMORYLAYOUT` line printed *"NOT compared — neither document
declares one"* beside a value one document plainly declared. The skip was right, the stated reason
was false. It now names which side is silent.

## FI-75 — the quoted-UDT collapse was discarding per-use-site start values (2026-08-12)

🔴 **The fourth silent loss found on 2026-08-12, and the one that had been *seen* and left alone.**
Flagged during the `MB_SERVER` gap work and deliberately not touched then, on the belief that the
collapse was load-bearing for the committed corpus's fixed point. **It was not** — see the blast
radius below, which is zero.

**The collapse.** FI-56 (2026-08-08) taught the parser to accept TIA's expansion of a member whose
type is a named UDT — TIA renders `Claim : Array[1..8] of "UDT_ResourceClaim"` as a nested
`<Sections>` on re-export, and refusing it had made whole blocks unreadable. It accepted the
expansion by **discarding** it, reasoning that *the IR already names the type, so TIA's rendering of
that type is redundant*. FI-64 (2026-08-09) applied the same rule to the third parse path.

**Why that reasoning is wrong.** *** THE VALUES INSIDE AN EXPANSION BELONG TO THE USE SITE, NOT TO
THE TYPE. *** A UDT declares the members; the referencing DB or FB declares what they *start at*.
Measured in the committed corpus:

| | `FTTime` | `ReverseDelay` | `ReverseIgnoreFT` |
|---|---|---|---|
| `MotorFwdRevIOSet` (the type) | *none* | *none* | *none* |
| `iDB_MotorFwdRevSystem_Shredder` (the use site) | **10.0** | **8.0** | **12.0** |

Three commissioning setpoints — a fail-to-run time and two reversal timings — that exist **nowhere
but the use site**. A collapse discards them and returns **exit 0**. Same class as the 182 dropped
`<Subelement>` values, and as `MemoryLayout`: *converted without error* is not *converted correctly*.

**What exactly was discarded, and in which shapes.** The collapse fired at the two NESTED positions
only — `ParseBareMember` (a quoted named type inside another member's expansion, i.e. doubly nested)
and `ParseTypeMember` (a quoted named-type member of a PLC data type). The **top-level**
`ParseMember` has always *kept* the expansion. So the codebase held two different dispositions for
the identical construct, and the collapse was an **asymmetry, not a principle**. Everything under
the collapsed `<Sections>` went: every nested member, every per-use-site `StartValue`, and every
`<Subelement>`.

**Blast radius: zero blocks, measured three ways.**

1. **Static scan of the corpus** — every quoted-UDT member carrying an expansion, with its nesting
   depth: 6 of 6 sit at **depth 0** (`FB_MotorFwdRevSystem`, `FB_PusherControl`, `FB_ShredderSequencer`
   and their three instance DBs, all the member named `IO`). **Zero** at a nested position, in either
   corpus, and none inside a `SW.Types` document. The collapse never fired on committed content —
   which is exactly why the corpus never lost a start value, and why the fixed point could not have
   depended on it.
2. **`drift-check` before and after** — identical at both baselines, same six names: **6 drifted /
   17 match / 3 skipped** on `test-project001`, **0 drifted / 15 match** on `reference`. (The three
   instance DBs drift for an unrelated pre-existing reason: the IR emits no `InOut` section, so
   `Section[3]` is `InOut` in TIA's export and `Static` in ours.)
3. **Whole-corpus IR re-derivation** — `to-ir` over all 38 committed exports, before and after:
   **0 files changed**, byte for byte.

**The fix.** Recurse and keep, at both nested positions — which is what the top-level path already
does, and what the doubly-nested `MB_SERVER` work built the machinery for. The disposition table at
the bare position drops from three entries to two: a bare `Struct` is **still a hard error** (an
anonymous structured member's `<Sections>` is its only definition, and there is no type name to write
it back out under), and *everything else* — quoted named type or unquoted system type — recurses.
`Array[…] of Struct` is deliberately left on the recursing side, where it has been since the
doubly-nested fix; narrowing it now would turn a shape that round-trips into a new hard error for no
gain.

**The write side needed a matching change, and skipping it would have been worse than the bug.**
`WriteTypeMember` emitted `NestedMembers` as **direct `<Member>` children** — the anonymous-Struct
shape. Keeping a named type's expansion on the read side while writing it in that shape would have
replaced a silent loss with a **silent corruption**. It now chooses the shape from the datatype, on
the same `IsAnonymousStructDatatype` test `WriteMember` has always used: anonymous → direct children,
named type → `<Sections><Section Name="None">` of bare members.

**Measured before and after at CLI level**, on a doubly-nested named type carrying a use-site start
value:

```
# BEFORE — to-ir exits 0
DB RealDbName
  MEMBERS
    Outer : "OuterType" SETPOINT
      Inner : "InnerType"                          <- Leaf : Real = 10.0 is GONE
# round trip: DIFFERS - 1 difference(s), the whole <Sections> ELEMENT-MISSING

# AFTER
    Outer : "OuterType" SETPOINT
      Inner : "InnerType"
        Leaf : Real = 10.0
# round trip: EQUIVALENT
```

11 tests on the read-back path (4 of them written to fail first, and they did), plus the
`DbConverterTests` doubly-nested case **inverted a second time** — it asserted the error before
FI-56, asserted the collapse after it, and now asserts the expansion is kept. 1010 converter tests
green (up from 996), golden harness green at 46.

## A literal is typed by the port it feeds, not by its own magnitude (2026-08-13)

Found on the rig. *** The converter emitted `<ConstantType>Int</ConstantType>` for EVERY hex literal,
regardless of magnitude or of the destination's type *** — so a 32-bit build stamp was 16 bits **by
construction** and every real stamp failed to import, including the worked example `16#A93F2C71`.

**Second instance of one class, so the narrow fix was replaced rather than duplicated.** The 2026-08-12
fix was *"a comparison's literal is typed by the comparison's own type, not by magnitude"*. This is the
same bug one site along. The general rule is therefore:

> **A literal's `ConstantType` is the declared type of the PORT it is written into. Magnitude is only
> the fallback where no declared type exists — and that fallback refuses rather than guesses.**

**Two independent causes, both closed.**

1. **The magnitude path never ran for a base-prefixed literal.** `long.TryParse("16#A93F2C71")` fails,
   so every `16#`/`2#`/`8#` literal fell straight through to the `Int` default — not a bad guess, no
   guess at all. **Widening the parse would not have fixed it:** 2,839,872,113 would then be typed
   `UDInt` into a `DWord` port, which TIA rejects by the same door. A bit string's **width is a
   declaration choice its digits cannot express**, so `InferLiteralConstantType` now returns *null* for
   a base-prefixed literal and the caller **hard-errors** naming the literal and the fix. (Precedent:
   FI-71 refusing to emit XML with unresolved member types.)
2. **Six operand sites never passed the port type at all** — including the CALL-argument site, where
   the callee's own `param.Type` was resolved **on the very next line** and used for the sidecar's
   `Type` while the literal beside it was typed by magnitude. Measured:
   `CALL FB_Reg(Stamp := 16#A93F2C71)` against `Stamp : DWord` emitted `Int`.

**The structural half of the fix, which is the part that stops a third instance:** `ResolveOperand`'s
`constantTypeOverride = null` became a **required** parameter, `portType`. An optional parameter is an
invitation, and six sites accepted it. A new operand site now cannot silently fall back to magnitude —
it must state its port type, or pass `null` and say why. Fixing six omissions would have left the
seventh to be written next year.

Sites and what types them now: CALL arg → the callee's `param.Type`; MOVE → the destination tag's
type; comparison → the compare's `SrcType`; WAND/CALC/MUL/ADD → the box's operation type;
CONVERT/ABS/SWAP/T_SUB/T_CONV → the box's own `SrcType` (these already refuse a non-tag operand, so
they emit nothing different today — they stop being an omission waiting for that guard to relax). The
one site the rule genuinely cannot cover is **MOVE_BLK_VARIANT**, stated rather than defaulted: `SRC`
is a `Variant` with no scalar type, and `COUNT`/`SRC_INDEX`/`DEST_INDEX` carry TIA port types that live
in no registry here and that this project has no grounded export to read off.

**`literal-fit` — the mechanical floor's missing check.** Asked which `converter review` rule should
have caught this, the honest answer is **none**: `docs/06-lad-conventions.md` has no rule about a
literal fitting its destination type, so no review rule failed — there was never one to fail, and
inventing a C-nnn from the tooling side is not this component's call. Pre-flight's stated job *is* the
known, recurring import/compile error classes, and this is one — measured, `MOVE(IN := 70000)` into an
`Int` member was reported **CLEAN** by pre-flight and is rejected by TIA. The new check flags a plain
decimal outside the destination's declared range, and a base-prefixed literal wider than the
destination's **bit width** (`16#A93F2C71` needs 32, `Int` holds 16 — wrong under any reading). It is
deliberately *not* a signed-range test on bit-string literals: whether TIA reinterprets `16#FFFF` as a
two's-complement `Int` is a question with no grounded answer here, and a check that guessed at it would
be one people learn to ignore. Zero findings across the whole 26-file `ir/test-project001` corpus.

### *** THE PROOF THAT SHOULD HAVE CAUGHT IT COULD NOT, AND WHY ***

The phase-2 lane verified its generated IR survives `to-xml` → `to-ir --no-sidecar` **byte-identically**,
and that proof passed — **because the wrong type round-trips faithfully.** All 1069 existing tests were
green on this too. *** A round-trip check is blind to any error the round trip preserves *** — the same
shape as the Normalizer being blind in exactly the way the converter was wrong. Every assertion in
`LiteralDestinationTypeSynthesisTests` is therefore against the **emitted type**, never a round trip.

Audited across the toolchain, and **measured, not assumed**:

| proof | blind to a *consistent* converter error? |
|---|---|
| `to-xml` → `to-ir --no-sidecar` byte-identical (`IrSelfStabilityTests`, `NoSidecarEquivalenceTests`) | **YES.** Both halves are converter code; a shared assumption cancels. This is the proof that passed. |
| `converter diff`, `ir-hash` | **YES**, structurally — `ConstantType` is sidecar, not readable IR. Fine for network invariance; proves nothing about emitted types. |
| `converter drift-check` | **Per file.** Not blind against a genuine TIA export; blind against a corpus entry the converter itself produced. |
| `converter compare` + `tools/confirm-roundtrip.ps1` | **NO — this would have caught it.** Verified live: two exports differing only in `ConstantType` return `VALUE-DIFFERS … first: DWord / second: Int` with the path. TIA's own export is on both ends. |
| golden harness `Normalizer.AreSemanticallyEquivalent` vs a real TIA export | **NO** — `ConstantType` is not on any ignore list (checked, then measured as above). |

The rule worth keeping: **a proof is only as strong as the most independent authority in its loop, and
the converter round trip has none.**

55 new tests (`LiteralDestinationTypeSynthesisTests`, `LiteralFitCheckTests`); **1124 green, up from
1069**. Negative-tested by reverting the two fix points: **11 went red**, and the four "not a blanket
widening" cases stayed green in both states, which is what distinguishes this fix from simply widening
everything.

## `review` — the tag table was never reviewed, and the report said "not applicable" (2026-08-13)

`converter review` run against a **tag table** exited 0 while all 18 mechanized rules reported
`not applicable — TAGTABLE rule support not implemented in Phase 1`. The file was **effectively
unreviewed** and the exit code and summary were indistinguishable from a clean review — this
project's recurring failure class (an absence of findings read as a positive result), live in the
tooling. A tag table is **where tag names live**, which makes it the file kind C-001 applies to
*most*, not least.

**The three-way split the old wording collapsed.** "Not applicable" was doing three jobs at once,
and only one of them was true:

| | means | zero findings is | reported as |
|---|---|---|---|
| the rule has a subject here and found nothing | a result | **meaningful** | `checked, clean` |
| the rule has no subject in this content kind | a result | meaningful | `not applicable (<this rule's own reason>)` |
| the rule has a subject and nobody judged it | **not a result** | **proves nothing** | `NOT CHECKED - no result, not a pass` → **exit 2** |

**Fail-closed, not a warning.** A `Skipped` (NOT CHECKED) status now **gates**: `converter review`
exits **2 = REVIEW INCOMPLETE**, ahead of exit 1 (error findings), and names what went unjudged on
stderr. This is deliberate — the offending line *had already been printed on every tag-table review
since Phase 1*, in a run that exited 0, and it was read as clean every time; a warning that gets
skimmed is how the defect survived. `--allow-unchecked` is the named escape (FI-71's shape), never
the default. The report always carries an `UNCHECKED: n rule(s) not judged` line **including when n
is 0** — an absent line would itself be ambiguous. `preflight` folds unjudged rules in as findings
for the same reason.

**What is now checked on a tag table** (3 rules), each with a violating *and* a conforming fixture:

- **C-001** — two layers, and every tag lands in exactly one, so no tag is silently unexamined.
  *Physical-IO* (address in the `%I…`/`%Q…` process image): the `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>`
  format, checked as a **field split, not a prefix match** — the equipment token sits in the *middle*
  (`DQ3_PSH_RunPowerPack`). Because the ADDRESS is right there beside the name, two cross-checks a
  name alone could never give come free: the direction letter must agree with I-vs-Q, and the D/A
  letter with bit-vs-word width. A width that cannot be established (`%I5`: no size letter, no bit
  offset) is **not guessed at** — only the direction is checked there.
  *Everything else* (`%M` flags): C-001's variables layer, short PascalCase, underscore-free.
- **C-005** — charset over every tag name **and the table's own name**. The `LogicalAddress` is
  deliberately not checked (`%I0.0`'s dot is addressing syntax, not a chosen name — the same
  exclusion `CheckPathCharset` already makes for `%Xn` slice components).
- **C-406** — the declaration form, against a tag's own `DataTypeName`. It may well never fire, but
  `DataTypeName` is a free string in the IR model, so a `TOF_TIME` here is **representable** and
  therefore worth looking for. `Checked`, not `CheckedVacuous`: "cannot appear" is not a claim this
  codebase can make about a free-text field.

**C-007's vendor-default exception is reported, not silently applied.** `Clock_0.5Hz` (a dot) and
`Default tag table` (a space) are tolerated per C-007 — as **Info** findings naming C-007, so a
reader is told they were tolerated and why, and the severity keeps them out of the exit gate. The
exception is a narrow enumerated set (the clock/system memory bits), and TIA's auto-generated
`Tag_1` placeholder is **deliberately not in it** — an unnamed tag at a physical input is exactly
what a naming review should catch.

**Two more instances of the same defect, found in passing and fixed with it:**

- **TYPE files** carried the identical blanket stamp (`TYPE rule support: only C-001 … in Phase 1`)
  over 17 rules. Four were implementable against a UDT all along: **C-003** names `UDT_` in the same
  breath as `FB_`/`FC_`/`DB_`, **C-005**'s charset applies to a member name wherever it lives,
  **C-201**'s header-comment half applies to any content kind carrying its own `Comment` (the
  reasoning already written into `CheckC201HeaderComment` and already applied to DBs), and
  **C-406**'s declaration form reads members.
- **`preflight` never passed the reviewer the `TagTypeRegistry` it had already built** — it requires
  `--project`, so the index was always available — which meant **C-118/C-122/C-125 recorded
  themselves unrunnable on every preflight this tool has ever done**. One call site away from the
  tag-table hole. Now passed through, so those three actually run in preflight.

**The `--project` non-run is now split by whether the rule had a subject.** C-118/C-122/C-125 are
cross-file (FI-09). A block that references no `Step` register has no stepped sequence to place —
genuinely `not applicable`, with or without an index, and it does not gate. A block that *does* use
one, reviewed without `--project`, had a subject and was not judged: that is `NOT CHECKED` and it
gates. Measured on the real `FB_ShredderSequencer`: previously `SUMMARY: 0 finding(s)`, exit 0, with
three of its most relevant rules silently never run; now exit 2 naming all three, and exit 0 with
`checked, clean` once `--project` is supplied.

**Corpus impact, stated rather than discovered later.** `ir/test-project001/DefaultTagTable.ir` now
reports **58 C-001 errors** — every one a real violation (TIA's `Tag_1..Tag_54` placeholders at
physical addresses, and four `AirStarWord*` comms words at `%IW`/`%QW`). The 38 hand-authored
`DI…`/`DQ…` tags in the same file are silent, which is the evidence the rule discriminates rather
than flagging everything. Three of four project UDTs gain a C-201 (no header comment) and one a
C-003 (no `UDT_` prefix); the most recently authored, `UDT_HopperBlockageIO`, is clean.

59 new tests (`ReviewTagTableRulesTests`, `ReviewOutcomeTests`, plus the runner's own); 1069
converter tests green, up from 1010. Every implemented rule has a fixture that **violates** it as
well as one that conforms, and the gate is tested in both directions — a gate only ever exercised
against input that should trip it has not been tested either.

## `review` — harness scope: the reviewer knows a harness object when it sees one (2026-08-13)

A generated test-harness copy layer drew **24 × C-001 plus C-201 on every run**. The owner ruled
that doc 06's naming conventions govern PLC program content **authored for the plant**, and a
harness-generated object is neither plant nor hand-authored — so those findings were correct
against the letter of the rule and **wrong about their subject**.

*** THE REQUIREMENT WAS NEVER "SUPPRESS 21 FINDINGS", AND BOTH FAILURE DIRECTIONS ARE LIVE. ***
Twenty standing findings a run is how a reviewer learns to skim — *an over-firing gate decays into
a warning* — and a **silent exemption is indistinguishable from a correct pass**, which is this
repository's most-repeated defect. So nothing is dropped: the findings are **reported**, in a
counted, labelled, non-gating bucket, beside the derivation that classified the object.

### Derived, never declared

*** THERE IS NO `--harness` FLAG AND NO IR FIELD. *** A caller assertion is forgotten exactly when
it matters, and *a declaration is a transferred responsibility, not a verification*.

| content | derivation | if it cannot be decided |
|---|---|---|
| FC / FB / DB | **number inside the reserved band 9000–9999**, independently per number space (declared `docs/notes/test-environment-build-plan.md`) — structural, in the file's own IR, and not a name | — |
| **OB** | **excluded**: an OB's number is fixed by its **event class**, so the band cannot read one in *either* direction (OB80 is a harness object the band would call plant) | `Unclassified` — findings gate |
| **tag** | ⚠️ see below — **per tag**, from the blocks that reference it | `Unclassified` — findings gate |
| UDT | none exists: no number, and no referrer relation of this kind | `Unclassified` — findings gate |

`Unclassified` behaves **exactly** like `Plant` for gating and differs only in what the report
*says*. That is the point: *"this is plant content"* and *"no derivable property could tell me"* are
different facts, and collapsing them is how a classifier that **silently stopped running** would
look identical to one that examined everything and found no harness objects.

### ⚠️ The tag table was the hard case, and the honest answer is not to read its name

A tag table **has no number**, so its only table-level property is its **NAME** — and a name is
exactly what anything can be renamed into. *** SO THE TABLE'S NAME IS NEVER READ, AND NEITHER IS AN
`HX_` PREFIX. *** Classification is **per tag**, from the one derived property available: the set of
blocks that reference the tag, each of which is classified by the number band.

- **Harness** only when the tag has **at least one referrer** and **every** referrer is a harness
  block.
- **Any plant referrer ⇒ Plant.** One plant reader or writer makes a tag plant content.
- **No referrer at all, an unclassifiable referrer, or any unparseable corpus file ⇒ Unclassified.**
  The last is the **partial-corpus fence**: an unread file could hold the plant reference that
  changes the answer, so a partial corpus **refuses rather than mis-classifies** — *a comparison
  that could not be made must not be reported as one that came out negative*.
- A block's **own interface member names** are excluded from its references, so a harness block's
  input `Start` cannot vouch for an identically-named plant tag.

Laundering a plant tag therefore means **moving every reference to it into blocks numbered
9000–9999**, which breaks the plant program — where a rename costs nothing. Measured on the real
corpus: renaming the harness table to `PlantProcessTags` left it `HARNESS-GENERATED` (24/24), and
renaming the plant `DefaultTagTable` to `HarnessMirror` exempted **nothing** (0 harness / 41 plant /
60 unclassified, all 60 findings gating). Adding **one plant block that reads the same `HX_` tags**
collapsed the harness table to `0 harness / 24 plant` on the spot.

The corpus is the review batch **plus `--project`** when supplied — so reviewing a generated tag
table together with the copy layer that drives it is enough, and `--project` widens it to the whole
export, which is the stronger question because it can see a plant reference the batch omitted. The
corpus that answered is printed with the verdict.

### Scope, and what still gates

**C-001 and C-201 only.** `C-103` stays a finding on the harness copy layer — it is *behaviour, not
naming*, and it was recorded rather than silenced. A rule outside the scoped set is untouched no
matter what the verdict says, so a classifier gone wrong cannot silence anything else.

### The report says so, on every file

- `SCOPE:` — verdict **and its basis**, on **every file including plant ones**. This is what makes a
  broken derivation visible: a scope that only announces itself when it exempts something has an
  **invisible failure mode**.
- `HARNESS-SCOPE (reported, NOT gating - n finding(s))` — the findings themselves, printed.
- `HARNESS-SCOPE: n finding(s) … across m harness-generated object(s); k object(s) could not be
  classified` — the run total, **always printed including the zeros**: *a count of zero is a
  different fact from an absent section*.
- A new `RuleCheckStatus.CheckedHarnessScope` reads `checked, n gating finding(s) - <n> further
  finding(s) reported under HARNESS-SCOPE and NOT gated`. `FindingCount` keeps its single meaning
  everywhere — **the findings that gate** — so the status line never disagrees with the list printed
  beneath it.
- `preflight` folds them in under `review:harness-scope` with `Gates: false`. **`PreflightFinding.Gates`
  defaults to `true`**, so a future check is gating unless someone said otherwise — a non-gating
  default would install *"a warning is not a gate"* at the type level.

**Fail-closed by construction.** The scope parameter's default is `HarnessScope.Empty`, under which
blocks still classify from their own number but **no tag can be classified at all** — so a caller
that forgets to build one gets the full pre-change finding set, never a bypass.

**Mutation-tested in four directions**, on the committed fix: band never matches (9 red, including
every harness case), **band always matches — the dangerous direction — (11 red**, including the
laundering test and the plant-block did-not-run test), tags classified by `HX_` name prefix (5 red,
including the laundering test), and scope crept to include C-103 (1 red). 25 new tests
(`HarnessScopeTests`); 1145 → 1170 converter tests green.

*** THE DID-NOT-RUN TEST IS THE LOAD-BEARING ONE: *** `PlantBlock_SameDefects_StillDrawsItsC201FindingThroughTheSamePath`
reviews the *same block with the same defect and one number changed*. Without it, a classifier that
called everything harness would leave every other assertion in the file green.

## 🔴 `cross-check` — FB-internal paths were unqualified, so it reported multi-writers that do not exist (2026-08-14)

The usage graph keys every path **verbatim**, and an FB addresses its own interface member with **no
root at all** — `IO.Step`, `Time`. So three FBs, each with its own `IO` static of its own UDT type
and its own `Time : Real` temp, all landed on **one key**, and `cross-check` reported them as
**cross-block multi-writers**. They are different members of different instances that share a leaf
name and nothing else.

**Measured on `ir/test-project001`.** Of the four multi-writer paths spanning more than one block:

| path | writers | verdict |
|---|---|---|
| `IO.Step` | FB_PusherControl + FB_ShredderSequencer | ❌ **fictitious** — members of `UDT_PusherIO` and `UDT_ShredderSequencerIO` |
| `Time` | FB_MotorFwdRevSystem + FB_PusherControl + FB_ShredderSequencer | ❌ **fictitious** — a `Real` temp declared separately in each |
| `iDB_MotorFwdRevSystem_Shredder.IO.FaultFB` | FC_ControlMain + OB100 | ✅ real |
| `iDB_MotorFwdRevSystem_Shredder.IO.RecentStart` | FC_ControlMain + OB100 | ✅ real |

After the fix the cross-block set is **exactly the two real ones**. *A false finding is the equal of
a false green here — the first one is what gets a check switched off* — and this was caught only
because a lane refused to feed the output into a submission gate it did not trust.

**The fix keys on whether the block DECLARES the root**, never on whether the path has a dot. *"Is
this bare name the block's own member or a global PLC tag?"* cannot be answered from the name —
`PressureTripCount` (an FB static) and `Start_PB` (a tag-table tag) are both bare single-component
references — and is answered exactly by the block's own declarations, which the IR states outright.
Temps and constants are included: a temp named `Time` **is** the collision.

`multiWriters` and `soleWriters` now regroup by **storage identity** and carry an `owner` field —
the owning block for a block-local path, `null` for a global one. **A consumer must key on `owner`
rather than parse the path: an emitted string is not a schema.**

⚠️ **What it deliberately does NOT do: it does not pool an FB-internal member with the
`iDB.<suffix>` form.** Those are one storage when the FB has one instance, but an FB with **two** has
an internal write landing in **both**, and pooling with either would invent a conflict exactly as the
bug did. The aliases are **reported** on the fact (`instanceAliases`) so a consumer can join them
knowingly. Every FB in `test-project001` has exactly one instance DB — *which is precisely why
designing only for that would be designing for the case that happens to exist.*

**Sibling analyses checked.** `deadMembers`' interface half already restricted the bare form to the
owning FB; `ioBoundary` and `siblingRefs` carry the block on every row. All three are
**byte-identical across the fix** on the committed corpus, so `multiWriters`/`soleWriters` were the
only two affected.

**`soleWriters` was under-reporting**, which is the more dangerous direction: two FBs each writing
their own member once pooled into a two-writer path, so it read as multi-written — *not vulnerable to
a deletion* — when each was its FB's **sole** writer. That did **not** bite on this corpus (no path
moved between the tables) and is recorded as constructed-not-observed; a test builds the case
deliberately.

11 new tests, **every aliasing assertion paired with a genuine cross-block multi-writer that must
still be found** — a fix that silences the false one by silencing everything is the obvious failure
mode. Mutation-tested three ways: qualification disconnected (6 red), everything qualified (6 red,
including two pre-existing tests), and the plausible **name-shape heuristic** (2 red — exactly the
two tests written for it). *The pre-existing suite stayed green through both the defect and the fix
and could not tell them apart.*

## `reachable-state` — D9's producer: slot disjointness COMPUTED, not declared (2026-08-14)

### Consumers, closure direction, and canonicalisation (migrated from CLAUDE.md 2026-08-21)

`converter reachable-state --project <ir-dir> [--block <name>]... [--json]`

Per block: the transitive closure through its CALL tree of every storage location it touches, keyed
on STORAGE IDENTITY, with a corpus-stamped provenance. Feeds
`wave-cli submit --reachable-state <file> --reachable-block <name>`, after which
`SlotConflictDerivation.OverlappingReachableState` makes the edges by set intersection.
`--reaches`/`--reaches-from` survive as the DECLARED path and are **REFUSED in combination** with
the computed one.

- **READS COUNT AS WELL AS WRITES** — two slots cannot share a signal one drives and the other
  observes.
- **CLOSES DOWNWARD ONLY.** Closing upward through callers reaches OB1 from any leaf and would make
  every pair conflict; coupling that exists only in a common caller is the author's add-only
  blacklist.
- **An `iDB.<suffix>` reference is canonicalised onto `<FB>|<suffix>` before intersecting.** Without
  it, a slot testing an FB and one testing its caller read as DISJOINT while driving one location.
  This deliberately POOLS WHERE `QualifiedPath` DOES NOT, because the error points the other way:
  there, pooling invents a multi-writer (a false accusation); here it can only ADD an overlap, i.e.
  separate two slots that might have run together. Computed disjointness is the FLOOR, so more of it
  is the safe direction — and every rewrite is REPORTED, never silent.
- Array subscripts are stripped (`DB_Input.Test[0]` disconnects every terminal for both tests).
- **ABSENT IS NOT EMPTY, AT BOTH LEVELS:** `reachableState: []` is the positive claim "computed,
  reaches nothing"; a closure nobody could compute OMITS the key AND its provenance, so admission
  raises `ReachableStateNotComputed` and refuses rather than admitting the most independent-looking
  slot in the set.

Exit **0** computed / **2** NOT COMPUTED, key withheld / **3** emitted with at least one block
withheld BY NAME.

```
converter reachable-state --project <ir-dir> [--block <name>]... [--json]
```

Per block, the **transitive closure through its CALL tree of every storage location it touches** —
the set `Ladder.Wave.SlotConflictDerivation.OverlappingReachableState` intersects to produce a
slot↔slot conflict edge, and the provenance `Ladder.Wave.WaveSetAdmission` refuses a slot for
lacking. **Every consumer already existed; nothing computed the sets.** They arrived from
`wave-cli submit --reaches`, i.e. from the submitting agent — *ask of any rule: who computes its
inputs? If the answer is "the party the rule constrains", it is not a rule.*

**Reads count as well as writes.** Two slots cannot share a signal one of them drives and the other
observes, whichever way round; the question is *"could these two tests see each other?"*, never
*"would they collide on a write?"*.

### *** The direction of error is chosen, and it is not symmetric ***

An **over-large** closure separates two slots that could have run together: concurrency lost, nothing
unsafe. An **under-large** one produces the positive claim *"the slots are disjoint on every computed
relation"* about a hazard it cannot see, and puts two agents on one FB instance. Where the two are
traded off, **the larger closure wins.** Three consequences:

- **Instance aliases are canonicalised before intersecting.** An FB writes `IO.Step`; its caller
  writes `iDB_X.IO.Step`. One location, two strings — and left alone a slot testing the FB and a slot
  testing its caller read as **disjoint while driving the same storage**. This deliberately **pools
  where `ProjectUsageGraph.QualifiedPath` does not**: there, pooling *invents* a multi-writer (a false
  accusation against correct work); here it can only *add* an overlap. *Computed disjointness is the
  FLOOR* (D22). Every rewrite is reported, never applied silently — and the converse is pinned:
  `FB_PusherControl|IO.Step` and `FB_ShredderSequencer|IO.Step` stay distinct.
- **Array subscripts are stripped.** Two tests driving different elements of one injection array are
  not independent — `DB_Input.Test[0]` disconnects every physical terminal for both.
- **It closes DOWNWARD only.** Closing upward through callers reaches OB1 from any leaf and would make
  every pair of slots in a plant program conflict. Coupling that exists only in a common caller is the
  author's blacklist to state (§2.5 / D22, add-only).

### *** Absent is not empty, at both levels ***

`reachableState: []` is the positive claim *"computed, and it reaches nothing"*. A closure nobody could
compute **omits the key, and its `provenance` with it** — which is what makes admission raise
`ColouringDefect.ReachableStateNotComputed` and refuse, instead of admitting the most
independent-looking slot in the set. An unparseable corpus file withholds the **whole report**; a call
to a block the corpus lacks withholds **that block, by name**, and the rest are still emitted.

Exit **0** computed · **2** NOT COMPUTED, key withheld · **3** emitted with at least one block withheld.

### The proofs, against `ir/test-project001` rather than a fixture

- **Six slots on `FB_HopperBlockageMonitor` → 15 `OverlappingReachableState` edges → SIX WAVE SETS OF
  ONE.** That answer was reached by hand and written into `conformance-vectors-b.json` as a correction
  (`slotsInWaveSet` 6 → 1) **by an author who said outright it was not verified against `ir/`.**
- **The four `FB_Hx*` blocks → NO edges → one wave set of four.** Not optional: *a producer that finds
  conflicts everywhere passes every test that only checks for conflicts.* Each closure is asserted
  non-empty, so neither green can be the empty-set one.

Mutation-tested: disconnecting the alias canonicalisation reds exactly the test written for it (the
corpus proofs stay green — that rule needed its own); emitting a withheld closure as `Computed: true`
reds two.

## `conflict-graph` — the submission-scoped emission the harness gate consumes (2026-08-14)

```
converter conflict-graph --project <ir-dir> (--submission <file> | --signals <file>) [--json] [--allow-unresolved]
```

**Not `cross-check` with a filter.** `cross-check` emits whole-project **fact tables keyed on a
storage path**; `Harness.Results.SubmissionGate` gates 8/8c consume **edges between blocks** carrying
a provenance and a signal class. Those are not the same shape — an instruction to bridge them was
withdrawn as wrong, and the lane that received it correctly supplied nothing rather than reshaping
one into the other. The emitted `conflictEdges` matches `ConflictEdgeDocument` field for field
(`blockA`/`blockB`/`provenance`/`signal`/`class`), **read off the consumer** rather than written and
handed over for the harness to accept.

### *** An absent graph and an empty one are different documents ***

`conflictEdges: []` is the **positive claim that the graph ran and found nothing**; a missing key is
`NOT CHECKED` and gates. So when the graph did not run the key is **omitted** — not `[]`, and **not
`null`**, because a null would let a lenient deserializer round it to the empty list and *restore the
false claim one layer down*. Three states withhold it, each printing its reason:

| state | why the key is withheld |
|---|---|
| **partial corpus** (any project file unparseable) | an unread file can hold the second writer that makes a signal a conflict |
| **no signals supplied** | nothing was scoped and nothing was examined (FI-44) |
| **signals that did not resolve** to exactly one storage path | an edge list over a scope nobody looked at is empty for a reason that has nothing to do with conflicts |

`--allow-unresolved` is the named escape for the third, never the default, and the gap is still
reported in full beside the edges it does emit.

### Only `MultiWriter`, and that is a refusal rather than an omission

`CallGraph` **is** derivable — the graph records every CALL — but a call edge is about no signal, so
it could only carry an `Unstated` class, and the consumer's `ProvenanceComplete` is **all-or-nothing**:
*** one such edge would turn gate 8c to `NOT CHECKED` for the entire submission. *** A helpful-looking
extra edge would silently disable the report it was added beside. `computedConflicts` is never
emitted for the same reason (a bare name carries `Unstated` provenance); gate 8's packing set derives
from the edges.

### Signal class, derived and never declared

From the same reserved 9000–9999 band `converter review`'s harness scope uses, read off each **writing
block's own number**. Any **plant** writer ⇒ `Deliverable` (the multi-writer ships); **all harness** ⇒
`HarnessInstrumentation`; anything **unclassifiable** ⇒ `Unstated`, which fails the gate closed. Exit
**3** names that last case, because at the gate it appears as a flat `NOT CHECKED` for the whole
submission with nothing naming the cause — and the operator who can fix it is the one running this.

### Ambiguity is refused, not guessed

A signal name matching **more than one distinct storage** is `AMBIGUOUS` with both candidates named —
*picking a candidate is exactly the aliasing above, wearing a different hat.* Instance aliases of one
storage are collapsed first, so a determinate signal is never falsely refused (which is what
`instanceAliases` buys). Measured: `IO.Step` is refused, naming `FB_PusherControl.IO.Step` and
`FB_ShredderSequencer.IO.Step`.

**Exit codes**: 0 computed (an empty list is the *earned* claim) · 1 usage · 2 **NOT COMPUTED, key
withheld** · 3 emitted but an edge is unprovenanced.

Every edge is derived from the **corrected** storage grouping (`StorageGroups`, one producer for both
consumers), so the fictitious cross-block multi-writers above are **structurally incapable** of
becoming edges: a block-local storage has all its writers in one block, and one block is not a
conflict.

### 🔴 `--submission` reads `map.storage` — the declared join, which it did not until 2026-08-17

***A SUBMISSION SPEAKS THE SPECIFICATION'S VOCABULARY BY DESIGN*** (D8: agents cite tag names, never
registers), and this tool fed those names straight into a resolver expecting **storage paths**.
**Measured on a real submission: 70 of 70 unresolved, and the graph correctly refused** — over a
question it had never actually asked. ***The field carrying the join already existed in the same
document and was read by nobody.*** Fifth instance in this codebase of one seam: a slot id, a
vector-target prefix, an observable vocabulary, a completion signal and `ResultRegisterOf`'s tag-vs-
cited-name have each failed the same way, and *this tool was built after the seam was diagnosed.*

Contract §2.7's shape, read exactly as `Harness.Gate.MapDocument` reads it — **object form only**,
because accepting a shape the gate ignores would let an author write a map that this tool honours and
the gate does not:

```
map
  storage      signal -> { owner?, path }   -- WHERE it lives. Two keys: an emitted string is not a schema
  harnessOnly  [ signal ]                   -- a POSITIVE claim: occupies NO PLC storage
```

**Every resolution says WHICH JOIN carried it**, on its own line and in the `JOINS:` breakdown —
*a resolution that cannot be explained is what made this invisible.*

| join | means |
|---|---|
| `DeclaredStorage` | `map.storage` named the (owner, path) and it matched project storage. **The only join the contract endorses** |
| `DeclaredHarnessOnly` | declared to occupy no PLC storage. No edge is possible and **that is a computed fact** — it does NOT count against the scope |
| `ProjectPathMatch` | the document declares **no map at all** (or the name came from `--signals`), so the cited name was matched against the project's own paths. Weaker, includes a leaf match, and says so every time |
| `NotDeclared` | the document declares a map and this signal is in neither half. **Unresolved, and no name-shape fallback is tried** |
| `ContradictoryDeclaration` | in both halves, declared twice differently, or a malformed entry. **Refused — and `--allow-unresolved` does not reach it**, because that flag accepts names nobody looked at, not a document that answers one question twice |

***THE REFUSAL IS NOT WEAKENED, WHICH IS THE POINT.*** Once a map is declared, a signal absent from it
stays `Unresolved` **even when its name would have matched something** — a map with one hole in it is
repaired by filling the hole. And a submission that declares no map behaves exactly as before, so the
gate does not fire outside its scope.

### 🔴 A reference is not a location — which placement a name reaches (2026-08-17, second pass)

First real use of the declared join produced two more cases, **both resolver gaps rather than bad
declarations**:

| symptom | cause | ruling |
|---|---|---|
| a **fully-qualified instance path** came back `AMBIGUOUS` over every instance of its FB | the first fix walked a **transitive closure** over *"these two spellings name one storage"* — **and that relation is not transitive.** `FB_X\|IO.Cmd` names the same storage as `iDB_A.IO.Cmd` *and* as `iDB_B.IO.Cmd`; `iDB_A.IO.Cmd` is **not** `iDB_B.IO.Cmd` | **gap.** The bound was right and the pooling should never have happened — *a declaration that names the placement has already answered the question* |
| a **nested multi-instance path** (`<outerInstance>.<innerStatic>.<member>`) resolved to nothing | an FB placed as a STATIC of another FB has real per-instance state and **no DB of its own**. `ProjectUsageGraph` has resolved these to a fixpoint since **FI-50**, nested ones included, and the resolver never asked | **gap.** Same lesson `undriven-scan` learned in FI-50, one tool later |

Both close with one change of model: **the equivalence closure became a CONTAINMENT relation.**

> **A group is not a location. It is a REFERENCE at some level of qualification, and what it COVERS is
> the answer.** A *global* reference covers itself. A *block-local* reference — an FB addressing its own
> member as a bare path — covers **one location per PLACEMENT of that FB**, instance DBs and
> multi-instances alike, because the FB's write executes once per placement and lands in each one's own
> memory. An FB with **no** placement covers a single declaration-site location (FI-44: a block written
> before its caller still has storage).

Resolution then works in locations: **one** location resolves, with the writers of every reference
reaching it unioned; **several** are refused naming each. *Sibling placements can no longer pool, and
that is now a property of the model rather than a bound bolted on after it.*

**The refusals are kept, and one is sharper.** An **owner-qualified** declaration names a member of the
*class*, so on a multi-placement FB it names N locations and is still `AMBIGUOUS` — now naming every
placement, with the repair in the message. A declaration naming an instance that does not exist, or a
real instance plus an unknown member, stays `UNRESOLVED` and is **never rounded to a sibling that
does**.

**So a per-instance test CAN resolve a shared member name** — the qualifier selects — provided the
declaration is the fully-qualified placement path. That is the whole of what changed for a
multi-instance plant.

The reported path is now the **location**, with the reaching references named beside it on every
resolution: *the location is computed and the references are the code, so a reader can check the
answer instead of taking it.*

### The writer set is UNIONED across every spelling of one storage

An FB writing its own `IO.Alarm` and a caller writing `iDB_X.IO.Alarm` are **one location under two
spellings**, arriving as two groups because each is keyed on how it was written. Resolving to one of
them reported **only that one's writers** — so a member the FB drives internally and a caller also
drives read as *single-writer*, and the conflict was invisible. The equivalence class is now walked
transitively and the writers unioned, with the pooling stated in the reason line.

**Bounded**: an FB with **two** instance DBs has an internal write landing in both, so a member reached
through more than one instance is `AMBIGUOUS` naming them — pooling there would invent a conflict
between blocks that never share a location.

### ⚠️ The second measured failure mode, and what it cost

Supplying a slot's storage tags **directly** still resolved **0 of 68**, because the corpus references
those members only **from inside the owning block** — never through the instance path the harness uses.
That is now repaired by an **identity** join (the instance DB's own declared members say which
`iDB.<suffix>` names the FB's member), not by a name shape. Verified on the committed corpus:
`iDB_HxBoolEcho.EchoResponse` read `UNRESOLVED` before and `RESOLVED -> FB_HxBoolEcho.EchoResponse`
after.

### One join site, and a walk that fails when a second appears

`SignalStorageResolver` is the only type in the assembly that turns a cited name into a location.
***A SHARED HELPER IS NECESSARY AND NOT SUFFICIENT*** — `Harness.Map.MirroredSignal.JoinKey` carries
the comment *"one definition, used by every path that joins the two documents"* and **the observe path
was not one of them**. So `JoinSiteWalkTests` walks the compiled assembly and goes red when a type
outside a declared allowlist reaches `StorageGroup`, with a denominator (bodies examined > 0) and a
live negative control (the predicate is a parameter, retargeted at a type used everywhere). It is a
fence around one gate: a join built directly on `ProjectUsageGraph.Usages`, or written in another
assembly, is outside it.

**48 tests** (15 `ConflictGraphTests` + 19 `ConflictGraphDeclaredJoinTests` + 10
`ConflictGraphInstanceScopeTests` + 4 `JoinSiteWalkTests`). **Mutation-tested 2026-08-17 — every figure
below was RUN, not predicted.** Baselines differ because the suite grew during the work: unmarked rows
were measured at 1,377, † at 1,379, ‡ at 1,389 (the placement pass).

| mutation | red |
|---|---|
| ignore the declared join (always take the project-path match) | **9** |
| fall back to a name match for a signal the map does not name | 1 |
| drop the instance-alias arm from the project-path match | 1 |
| drop the instance-alias arm from the declared match | 1 |
| keep one spelling's writers instead of the union | **3** |
| let `--allow-unresolved` cover a contradictory declaration | **3** |
| treat a malformed `map` entry as a skip rather than a rejection | 1 |
| pool a member reached through two instance DBs instead of refusing | 1 |
| label a project-path match as `DeclaredStorage` | **3** |
| emit `conflictEdges` unconditionally | **3** |
| never declare ambiguity | 1 |
| count only "missing from a map that exists", so a no-map submission is told nothing † | 1 |
| **add a second name-to-storage join site to the assembly** | **1** — `JoinSiteWalkTests` |
| ‡ a qualified instance path stops selecting and fans back out to every placement | **6** |
| ‡ multi-instances dropped from the placement index (instance DBs only) | 2 |
| ‡ a block-local reference covers only ONE placement | **7** |
| ‡ the name match loses its location arm | 1 |
| ‡ an owner-qualified declaration stops expanding to placements | 3 |
| ‡ nothing may ever resolve to one location (the resolution path itself) | **22** |

**The over-fire converse, run as deliberately as the refusals**: a new type in the assembly that
touches no storage grouping leaves all 1,377 green — *a guard that fires on ordinary code is noise,
and noise gets switched off.* A submission declaring no map still resolves by project path (2 red if
that path is removed). And an operator `--signals` name that resolves to nothing is **not** reported
as a missing declaration — *an unresolved path there is a wrong path, and sending its user to write a
`map` would be the wrong repair.*

**The strongest converse available, and it is on real data.** The placement pass was re-run against the
submission that produced the original finding, and each signal's outcome compared with the previous
run's: **0 signals lost** — nothing that resolved before stopped resolving — **10 newly resolved**, and
all **58** already-resolved signals changed only in the path *reported*, from the FB-internal reference
to the placement's location. That equivalence was **checked mechanically, not by eye**: for all 58, the
previously-reported path is still one of the *reaching references* named in the new reason.

⚠️ **What none of this establishes.** The mutation figures and every fixture are from the converter's
own tests on the **committed** corpus. The `0 of 68` figure in the section above is quoted from the
report that raised it, not re-taken. And **an empty edge list on that submission was checked rather
than assumed**: the corpus holds 140 genuine cross-block multi-writers, and **none of them is at a
location the submission declares** — they are all on deployed instances while the submission's scope is
a dedicated test instance. *That is why the empty list is earned; without that check it would be an
empty list over a scope that had just been narrowed.*

## 🔴 `claim` — X-J's enforcing half: `--allocate` now knows the reserved band (2026-08-14)

X-J's treatment reads *"a NUMBER RANGE IS RESERVED for harness-generated objects **and the claim tool
refuses allocations inside it**"*. The range existed; **`--allocate` had no knowledge of it and would
hand out 9000–9999 to a deliverable without comment.** This is the missing half.

**The band is READ, never restated.** `Converter.csproj` references `WaveControl` (netstandard2.0,
references nothing) so `HarnessNumberRange.Declared()` — the one place the ruling names, overturnable
at the one line the ruling says — is the only declaration. *Two declarations of one band is how they
diverge*, and this repo had grown a second the previous day: `HarnessScope` carried its own
`9000`/`9999` constants, correct at the time, which would have gone on agreeing with the ruling right
up until somebody overturned it in the place the ruling names and not in the copy. Deleted.

### What is enforced — needing no judgement about who is asking

| | behaviour |
|---|---|
| **plain `--allocate`** | **cannot** return a band number — the band is **removed** from the candidate set, not deprioritised. A floor just below the band **steps over** it and lands on the first number past it. |
| **`--allocate --floor 9000`** (fed by `HarnessNumberRange.AllocationFloor`) | the search is **confined to the band** and stops at its last number. |
| **band exhausted** | `BandExhausted` — its own result, naming the band and its declarer. **Not** `HeldByAnother`, because that invites a retry at a higher floor, and *** a higher floor is exactly what must not happen: it walks out of the reserved range and hands a harness object a deliverable number while every downstream check stays green *** — the measured `FC 910` collision by a different road. |
| **OB** | **excluded structurally**, via the declaration's own `CoversSpace`: no skip, no confinement, no exhaustion rule, no annotation. An OB's number is fixed by its event class and the spec names `OB80` as a harness object; a band applied to OBs emits a false finding on a correct project. |

The skip is written as an **explicit walk**, not an arithmetic shift: a shift has to leave a floor
*below* the band alone until the walk reaches it and leave a floor *above* the band alone entirely,
and `n < First ? n : n + Capacity` displaces the second case into numbers nobody asked for.

### What is NOT enforced, and why a flag would have been worse than the gap

An explicit `--value` inside the band is **accepted and announced**, not refused:

- **Nothing at claim time can derive harness-ness.** A block-number claim is an **allocation** — the
  block does not exist yet, that being the definition, and `ClaimValidator` refuses the claim outright
  if it does. So `HarnessScope`'s number-derived classification has nothing to read. A `--harness`
  switch would close the gap in appearance only: *a caller assertion is forgotten exactly when it
  matters.*
- **Refusing it would break X-J's own interoperation point.** `HarnessNumberRange.ClaimArgumentsFor`
  renders `claim … --value FC9001`, so the harness ledger could no longer record its own allocations —
  the collision the band exists to prevent, reintroduced by the fence.

What is available instead is **attribution**: the outcome says the claim is in-band, quotes the
declaration, and says it was *accepted, not verified*.

### ⚠️ The obvious closure is a tautology — written down so it is not added back

*"Once the block exists, classify it and check the claim against it"* **cannot work**: `HarnessScope`
decides harness-ness **by reading this same band**, so the comparison reduces to `band(n) == band(n)`
and could not fire on any input, in either direction. *** A check that shares its subject's blind spot
is not a check ***, and a guard that is correct, wired in and unfalsifiable in place is one of this
project's named failure modes. Closing it needs an authority that classifies a block by something
**other than its number** — the harness ledger's own record of what it generated would be one — and no
such authority is reachable from the converter today.

### Testing

19 tests, **every refusal paired with a legitimate case that must still succeed** — above all a
harness object taking 9000, and an explicit `--value FC9000`. *A fence that refuses everything passes
every test that only checks refusals.* Mutation-tested four ways: band not skipped (2 red), **band
allocation unconfined so it escapes past 9999** (2 red), band unreachable (6 red), OB carve-out
removed (2 red).

*** TWO OF THOSE TESTS EXIST BECAUSE MUTATION FOUND THEM MISSING. *** The first draft's
"plain allocate never returns a band number" asserted a property of the number *returned*, and on a
small corpus a plain allocate takes `FC1` and never goes near 9000 — so it held with the fence
removed. Worse, **the confinement mutation left the entire suite green**: the exhaustion test used
floor 9000, where a 512-wide walk ends at 9511 and never leaves the band. Both are now asserted over
the **candidate set**, from a floor high enough that the walk must cross or stop.

## `interface-check` — the static comparison D6's green rests on (2026-08-14, NB-30)

### Its exit 1 is a new kind, and two traps (migrated from CLAUDE.md 2026-08-21)

`converter interface-check --project <ir-dir> --block <name> --enumeration <file> [--json]`

Reads the enumeration's own `response_signal:` values as the REQUIRED set — so the requirement has a
PRODUCER rather than a hand-typed list — walks the block's INPUT/OUTPUT/STATIC (TEMP excluded) and
reports each required signal PRESENT or MISSING.

**Its outcome is a new kind: `exit 1` is a FAIL AGAINST THE BLOCK, not a refusal of the
submission.** Every other gate in this system blames the submission; a missing response signal is
the BLOCK's defect. Measured on the deliverable: `INPUT 0, OUTPUT 0, STATIC 25` →
`PRESENT HopperBlockedAlarm`, `MISSING HopperBlockedInhibit`, exit 1.

⚠️ **Design trap:** the block's INPUT and OUTPUT sections are BOTH EMPTY (it uses a single STATIC
interface UDT, C-132 house style), so a naive check that reads only INPUT/OUTPUT reports *every*
signal missing and looks like a catastrophic finding.

⚠️ **Its cross-file UDT descent is UNPROVEN BY ITS OWN TESTS** — every UDT member in this corpus is
INLINED, so that branch has never run; disabling it left all 20 tests green. A test now ASSERTS that
corpus-wide absence, so the first genuinely non-inlined member forces a re-proof instead of silently
exercising untested code.

```
converter interface-check --project <ir-dir> --block <name> (--requires <n1,n2,...> | --requires-file <path>) [--json]
```

*** YOU DO NOT NEED TO DRIVE A BLOCK TO DISCOVER IT LACKS AN OUTPUT THE SPEC NAMES. *** D1 — a
response signal the enumeration names and the block does not provide — rode inside a *relational*
conformance assertion for a week, and a relational assertion is **structurally blind to any error its
two operands share**. A misbound operand is exactly such an error. The comparison that finds it is a
**two-name set difference** over the block's interface, answerable before a scan elapses, and
**nothing in the system made it**.

Measured on the real artifacts, through the Release binary:

```
EXAMINED: 25 interface member name(s) - INPUT 0, OUTPUT 0, INOUT 0, STATIC 25, CONSTANT 0
  PRESENT HopperBlockedAlarm  @ STATIC/IO/HopperBlockedAlarm
  MISSING HopperBlockedInhibit
*** FAIL AGAINST THE BLOCK: 1 of 2 required response signal(s) are absent ***
```

### 🔴 Exit 1 is a FAIL AGAINST THE BLOCK, and that is the point of the subcommand

Every other outcome in this pipeline blames the **submission** — a `REFUSED` means *fix the vector*.
A missing response signal is a fail against the **block**, and the submission is correct. Reusing a
submission-blaming outcome here would send an author to edit the artifact that is right. The finding
also says outright **not to close it by renaming in the block** — that closes the finding and
destroys it as evidence.

`0` all present · `1` **the block does not carry a required signal** · `2` NOT CHECKED.

### The two wrong implementations were predicted before the build, and are pinned by tests

On this corpus's house style (C-132) an FB's `INPUT` and `OUTPUT` sections are **both empty** and the
whole caller interface is one STATIC member of a UDT — the measured line above says so.

1. **Reading INPUT/OUTPUT only** reports *both* signals missing, including the one that exists. *A
   gate that accuses correct work of the most serious offence in the project is one that gets
   disbelieved, and the day it is right nobody looks.*
2. **Matching comments** finds the word *"inhibit"* in the block's own comment and **passes the
   defect**. So: member names only — never a comment, never a network title.

Both are asserted, and both assert their own **premise** first (that the sections really are empty,
that the word really is in the text), so neither test can quietly stop being about anything.

### Where it declines to judge — three verdicts, not two

- A **dotted path** is `NotAMemberName`, not a fail: the same member is reachable by different paths
  from different callers, so a path makes the answer depend on who is asking. The leaf name is named
  in the refusal.
- A **case-only difference is PRESENT** (TIA identifiers are case-insensitive) **with the block's own
  spelling reported** — two artifacts spelling one member differently is how a name drifts.
- A member whose **type could not be opened** withdraws any MISSING into NOT CHECKED, naming it. A
  MISSING is the positive claim that a block does not carry a name, and that is sound only over a
  **complete** member set. PRESENT survives a partial walk; only the negative half is withdrawn —
  the same asymmetry `reachable-state` chooses on a partial corpus.
- **TEMP is excluded by name and COUNTED ON EVERY RUN, including zero.** A temp cannot be observed
  from outside the block. A requirement found *only* in TEMP is MISSING **and says so**, rather than
  reading as absent for no stated reason.

`--requires-file` reads a plain name list **or an assertion-enumeration YAML directly** (its
`response_signal:` values), and **reports which form it read with a count**. That second form is the
one with a producer: a hand-typed required set is the same self-referential check this exists to
break — *ask of any rule: who computes its inputs?*

The report carries the block's **`ir-hash`** as the stamp it was established against, so a consumer
holds the stamp rather than a bool and *"nobody ran it"* stays distinct from *"ran it against a
different version of the block"*.

### Testing — 22 tests, mutated five ways, and the fifth mutation found a missing test

Both whole-corpus sweeps run against `ir/test-project001` with **closed denominators**
(`swept + skipped == blocks`), because *a gate that refuses everything passes every test that only
checks refusals*: 18 blocks, 12 with an interface — every one accepts its own members, every one
reports an impossible name as a fail.

| mutation | result |
|---|---|
| STATIC dropped (wrong implementation 1) | **16 red** |
| always MISSING | **7 red** |
| always PRESENT | **8 red** |
| TEMP included in the walk | **1 red** |
| **cross-file UDT descent disabled** | 🔴 **0 red — every test stayed green** |

*** THE LAST ONE IS THE FINDING. *** Every UDT-typed interface member in the committed corpus carries
its members **inlined** in the block's own IR, so `TryGetUdt` never ran and the branch was a note
about a branch. Two tests were added: one fixture with a non-inlined member that **exercises** it
(the mutation now goes red), and one that **asserts the absence** — every corpus member is inlined
*today*, so the day a real non-inlined one appears, that test fails and demands the resolution be
re-checked against real data instead of a hand-written fixture.

### 🔴 `--subject` — the wrong enumeration was a FALSE ACCUSATION AGAINST A BLOCK (2026-08-18)

```
converter interface-check --project <ir-dir> --block <name> --requires-file <path> [--subject <text>] [--json]
```

`--requires-file` scraped every `response_signal:` value out of **one** enumeration and **never read
that file's own declared `subject:`**, nor compared it to `--block`. Two enumerations now exist
carrying **28 and 15** distinct response signals with an **overlap of 2**, so pointing a block at the
wrong one demands about **26 signals that cannot be present**: ~26 `MISSING`, **exit 1** — which in
this subcommand's contract is a **FAIL AGAINST THE BLOCK**, the one outcome in the whole pipeline
that blames the code rather than the submission.

**What made it credible rather than obviously wrong** is the house-style trap two sections up: on
C-132 an FB's `INPUT` and `OUTPUT` are both empty, so *"nearly every signal missing"* is a shape this
tool's own documentation predicts as a **genuine** output. A wrong-file run and a catastrophic block
defect rendered identically, and the provenance line named **the path only** — which is precisely the
thing that had been mis-typed.

Two repairs, cheapest first:

- **Always, unguarded: the file's own words are reported.** The `REQUIRED FROM:` line now carries the
  document's top-level `subject:` (folded/literal block scalars included, per-assertion `subject:`
  keys deliberately ignored — they are indented, and pooling them would make the document's subject
  depend on which assertion came last), or states outright that *the file declares NO top-level
  `subject:`*. That line is also printed on a **NOT CHECKED** run, which it was not before: the
  outcome most likely to have been caused by the wrong input file was the one that never said which
  file it read.
- **Opt-in gate: `--subject <text>`.** The caller states what the file must be about. Disagreement —
  or a file that declares no subject at all — is **exit 2, NOT CHECKED**, never 1. *A wrong
  enumeration is an unjudgeable INPUT, not a defective block*, the same ruling already applied to an
  unparseable required name, and this subcommand's exit contract already reserves 2 for exactly that.
  `--subject` without `--requires-file` is refused rather than ignored: silently accepting it would
  hand a caller a guard they believe they have.

Agreement is **containment either way, case- and whitespace-insensitive**, and the rule is stated
rather than tuned. A declared subject is a paragraph of prose and an asserted one is a phrase, so
equality would refuse every real file. The coarseness is safe because of the **direction of error**:
disagreement costs a re-run with a better string, and the flag is opt-in, so nothing is forced
through it.

Finally, on a FAIL where a **majority** of required signals are absent, a `NOTE` names the other
thing that produces that shape and states that this run did not establish the file is about this
block. It **does not decide** — a threshold would be a guess, and the house-style trap means
majority-absent really can be genuine. It puts the alternative explanation in front of the reader at
the moment it matters.

Tested at the CLI boundary where the gate lives, with the positive control that matters: **subject
agreement does not suppress a real FAIL**. Mutations: `Agrees` forced true → 3 red; disagreement
returning 1 instead of 2 → red; the top-level parse relaxed to accept an indented per-assertion
`subject:` → red. Semantics-preserving rewrite (containment operands swapped) → green.

## 🔴 `review` — C-410, the self-restarting timer: a silent block-killer nobody was checking for (2026-08-18)

A live job lost most of a day to a timer written `TON(X, IN := NOT X.Q, PT := …)`. On real hardware
it **fires once and then does not re-arm** — or re-arms only after an enormous, irregular delay.
Established by controlled experiment on the device, not inferred: the same program held a timer with
an ordinary Bool `IN` keeping perfect time beside a self-referential one that did not, and breaking
the self-reference through a plain Bool repaired it, measurably. **Six instances in one corpus, found
because a person happened to grep** — one of them a simulation layer's master clock, whose failure
mode was *every simulated rate multiplied by zero while every health bit stayed good.*

*That* is the argument for mechanizing it. The defect is silent, fatal to the block it sits in, and
**structurally detectable** — which is exactly what the mechanical floor is for: checks that survive
an agent choosing not to look. There were 18 mechanized rules and none of them covered it.

**C-410 (error), registered in `ReviewRunner.AllRuleIds` (now 19).** For every `TimerBinding` in a
network, it collects the tag references inside that timer's own `IN` and asks whether any reads that
same instance's own **output** — `<instance>.Q` (the measured case) or `<instance>.ET` (the same loop
through the other output port; C-408 has its own, separate quarrel with ET comparisons). Two
severities of the defect, both `Error`, distinguished by literal text in the finding:

| | shape | behaviour | reported |
|---|---|---|---|
| **total** | `IN := NOT X.Q` — no external term at all | never re-arms | `SELF-RESTART (TOTAL)` |
| **partial** | `IN := ArmBit AND NOT X.Q` | first cycle after each disarm→arm works; later cycles inside one armed period do not | `SELF-RESTART (PARTIAL)` |

Both gate. A partial still ships a block that silently stops timing after its first cycle, and *a
finding that only warns is the class this project has already recorded as getting skimmed* — so the
severity says "this gates" and the text says which of the two it is.

*** SCOPE IS DIRECT, ON PURPOSE, BECAUSE THE INDIRECT FORM IS THE FIX. *** The repair is to write the
timer's `Q` to a named Bool and gate the `IN` on that Bool. In the repaired corpus that coil sits in
the **same network** as the timer it feeds, so even a network-order-sensitive "the intermediate is
written no later than the timer" variant would flag the repair — and the hardware behaviour is known
only empirically (route it through a Bool and it works), which is nowhere near enough to guess which
indirect paths are still broken. **A rule that flags the fix is worse than no rule.** The rule's own
name states its coverage: *the `IN` reads its own instance.*

*** `.IN` IS NOT AN OUTPUT, AND THE FIRST DRAFT SAID IT WAS. *** Run over the committed corpora, that
draft reported three timers as deriving their `IN` "from its own output (`X.IN`)". They read the
timer's own **input image** as a latch's self-holding term (`Trigger OR Self.IN AND NOT Self.Q`) —
a different construction, and the finding's own sentence was false of it. *A finding that
misdescribes what it found is how a real rule gets switched off*, so the port filter is part of the
rule: `Q`/`ET` count, `IN`/`PT` do not. The self-holding one-shot still fires **on its `.Q` alone**,
which is the term measured to misbehave.

**Mutation-tested in five directions, on the shipped code.** Port test never matches → **10 red**, every
positive, negatives green. Port filter removed (any member of the instance counts) → **2 red**, exactly
the two `.IN` tests — the regression above, now guarded. Instance identity dropped at the call site
(any `Root.Q` reads as self) → **3 red**, including *another timer's Q is chaining, not self-reference*.
Severity collapsed to always-total → **2 red**, both partial tests, so the split is tested and not
decorative. Converse (semantics-preserving reorder of the two port comparisons) → **all green**, so the
suite is not red by coincidence. 17 new tests; 1389 → 1405 converter tests green.

**Corpus impact, stated rather than discovered later.** `ir/reference`: clean. `ir/test-project001`
and `ir/PlantAutoControl-bench`: **three PARTIAL findings between them**, all the same shape — a
self-holding one-shot whose drop-out term is its own `Q`. None is a `TOTAL`. Nothing in these corpora
is flagged for the repaired `Q → named Bool → IN` form, which is the property the rule had to have
before it could be run anywhere.

**`ReviewRunner.AllRuleIds` is now public and consumed by the tests.** It was private and referenced
by nothing, while `ReviewRunnerTests` carried a hand-copied duplicate that had **drifted to 12 of the
18** — so the "every rule gets exactly one status on every content kind" invariant was being asserted
against a stale subset, and a rule could be registered, never wired into the DB/TYPE/TAGTABLE
branches, and still pass.

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

### ⚠️ An instance DB round-tripped from a TIA export cannot be re-imported: `Remanence` on a multi-instance

**Measured 2026-08-21.** A TIA export of an instance DB, taken through `to-ir` and back through
`to-xml`, is refused at import:

```
iDB_<seq>_<inst>.<member>: The Openness import failed: The attribute 'Remanence' cannot be set.
import-all  SUMMARY: 14 imported, 4 failed, 0 rejected   exit 13
```

The member named is a **multi-instance static** — one whose datatype is a quoted FB name
(`FillStartWin : "FB_MoveWindow"`). TIA will not accept `Remanence` on one, and the round trip puts
it there.

**Why `BlockSourceWriter`'s existing guard does not cover it.** That writer already omits `Remanence`
for multi-instances (`WriteMember(..., omitRemanence:)`), deriving the set of multi-instance names
**from the block's own CALL and fixed-shape instruction instances**. An instance DB has **no CALLs**,
so the derivation has nothing to work from and `DbSourceWriter` emits the attribute unguarded.

**Why the obvious fixes are wrong.** A quoted datatype is *not* enough on its own — in the same
section, `IO : "UDT_SiloSequence" RETAIN` is a UDT and legitimately carries `Remanence`, while
`FillStartWin : "FB_MoveWindow"` is an FB and must not. Telling them apart needs `--project` type
resolution plumbed into the DB writer. **And a name-prefix test (`FB_…`) is not acceptable** — this
repo already rules that out for reachability, for the same reason: anything can be renamed into a
prefix.

**Workaround, and why it costs nothing today.** *Do not import an instance DB.* TIA regenerates an
instance DB's contents from its FB, so importing the FB is sufficient — verified on the run that
found this: four instance-DB imports failed, and the new member was nevertheless present in all four
when read back out of the controller, with `compile-all` reporting 33 compiled / 0 errors and
`sanity-check OVERALL: HEALTHY`.

🔴 **The trap is that the corpus looks importable and is not.** `preflight`, `review` and `to-xml`
all pass; only the import refuses. `import-all` fails closed (exit 13) and names the member, which is
what stops it being silent — but a caller reading only "the FB imported" would not learn that the
instance DBs did not.
