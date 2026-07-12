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

Deliberately narrow — **Contact/Coil and basic tag references, OR-merge (with recursive-chain
branches) and negated contacts, TON, comparisons (Eq/Ge), MOVE, and WAND (bitwise word AND)**
(S1 items 7–12, through 2026-07-12). No block calls yet. Anything outside scope is a hard error
(`UnsupportedConstructException`), never a silent partial result — hitting that error on a real
block is expected at this stage, not a bug.

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
