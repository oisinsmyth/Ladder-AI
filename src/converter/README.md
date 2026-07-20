# converter

SimaticML ↔ IR, bidirectional, lossless. C# (ADR-0002 revision — moved from the originally
planned Python once `openness-cli` made a .NET toolchain a hard requirement on this machine
anyway). Built in S1, `net8.0` (no `Siemens.Engineering` dependency, so unlike `openness-cli`
it isn't pinned to `net48`).

```
converter to-ir  <file>                                   # SimaticML → IR
converter to-xml <file>                                   # IR → SimaticML
converter to-xml <file> --synthesize                      # IR (no SIDECAR needed) → SimaticML — see "Sidecar synthesis" below
converter sanitize <file> --map <mapping.json> --out <path>  # SimaticML → sanitized SimaticML
converter diff <old.ir> <new.ir> [--only <network> ...] [--json]  # which networks changed, rest provably identical (S7 invariance)
```

`to-ir`/`to-xml`/`sanitize` all auto-detect DB vs code-block content (root element name for XML
input, first line for IR/text input) and route accordingly — no separate flag needed.

## Current scope (walking skeleton)

Deliberately narrow — **Contact/Coil and basic tag references, OR-merge (with recursive-chain
branches) and negated contacts, TON, comparisons (Eq/Ge), MOVE, WAND (bitwise word AND), Not
(standalone boolean inverter), CALL (FB/FC block calls), SCoil/RCoil (set/reset coils), and
network/block-level Title** (S1 items 7–17, through 2026-07-12). No arithmetic
(`Add`/`Sub`/`Mul`/`Div`/`Convert`/etc.) or FC/FB parameter-interface modeling yet. Anything
outside scope is a hard error (`UnsupportedConstructException`), never a silent partial result —
hitting that error on a real block is expected at this stage, not a bug.

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

`converter digest <file.ir> [<file.ir> ...] [--ignore-errors] [--json]`

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
6. **review:C-xxx** — `converter review`'s findings folded in, prefixed by rule.
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

`converter tagstatus <name> [<name> ...] --project <ir-dir> [--json]`

Mechanizes the pipeline's anti-laundering classification (`docs/15-generation-pipeline.md`
"Artifacts"; CLAUDE.md hard rule 3): each name is `EXISTS` (present in the current export) or
`PROPOSED` (a named gap the engineer resolves). Built for `gen-architecture`'s tag-status step,
which otherwise hand-greps the export. Composition only — reuses `ProjectIndex` and the *same*
`AccessNode.FromDottedPath` root extraction `preflight` uses, so the two never disagree.

Each name is resolved by checking the whole name first (catches bare tag-table tags whose own name
contains a dot, e.g. `Clock_0.5Hz`, and DB names) then its root (catches `DB.member` /
`Block.member` references — classification is by root, exactly like `preflight`'s `exists`/`proposed`
line). `--project <ir-dir>` is the current export, scanned non-recursively; unindexable files
surface as `INDEX WARNING`s. **Exit non-zero if any name is `proposed`** — so
`converter tagstatus … --project … && <build>` is a usable "all tags exist" gate.

```
$ converter tagstatus DB_Input.Cycle_Start DI3_SYS_CycleStart MadeUpTag --project ir/test-project001
DB_Input.Cycle_Start -> EXISTS (root: DB_Input)
DI3_SYS_CycleStart -> EXISTS
MadeUpTag -> PROPOSED
SUMMARY: 3 name(s), 1 proposed          # exit 1
```

Note: classification is root-level (does `DB_Input` exist?), not member-level — the same scope
`preflight` checks; member existence within a DB is TIA's own compile-time check.

## `diff` — network-level IR invariance (2026-07-18, S7 entry requirement)

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

`converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--json]`

Detects **silent export drift**: a fix that landed in a committed `ir/<proj>/*.ir` but was never
re-exported, leaving its `simatic-ml/<proj>/<name>.xml` stale (the exact landmine that poisoned a
synthesis-parity audit — a stale `FB_ShredderSequencer.xml` made real synth gaps indistinguishable
from noise). The committed round-trip tests deliberately *tolerate* this via an own-sidecar oracle;
this is the complementary **detector** they don't provide.

For each `ir/<proj>/*.ir`, pairs it with `<exports>/<name>.xml` by basename, rebuilds the SimaticML
in-memory (the *same* code path `to-xml` uses — `BuildXmlFromIrText`, all four `.ir` kinds), and
`Normalizer.AreSemanticallyEquivalent`-compares it to the committed export. Per block: `MATCH`,
`DRIFTED`, `SKIPPED` (no paired `.xml` — an ir-only block), or `ERROR` (the `.ir` couldn't be
converted). Decoupled — no knowledge of which drift is "known/tolerated" (that lives in the
`ExportDriftDetectorTests` golden-test baseline, which excludes the answer-key blocks). **Exit
non-zero if any block DRIFTED.**

```
$ converter drift-check --project ir/test-project001 --exports simatic-ml/test-project001
DRIFTED: DB_Settings  (semantic divergence between .ir and committed export)
DRIFTED: FB_ShredderSequencer  (semantic divergence between .ir and committed export)
...
SKIPPED: FB_HopperBlockageMonitor  (no paired .xml in exports dir)
MATCH: DB_Alarms
...
SUMMARY: 6 drifted, 17 match, 3 skipped, 0 error          # exit 1
```

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
