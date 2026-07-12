# Changelog

Git doesn't generate this on its own — `git log` gives raw commit history, not a curated record
of what changed and why. This is that record, maintained by hand. Entries are grouped by date,
newest first, one bullet group per commit (or per uncommitted round of work, until committed).

For the detailed story behind any entry — the investigation, the evidence, the live-verification
results — see `docs/notes/stage-gates.md` (stage-gate status) and `docs/notes/openness-quirks.md`
(TIA/Openness findings). This doc is the short index; those are the record.

## 2026-07-12

**S1 item 23: `TOF` (off-delay timer) — built, tested, live-verified; `AirStar` fully round-trips**

- Picked up per the project owner's own choice, asked directly what `TOF` was likely to be —
  answered from prior knowledge before any grounding: an off-delay timer, IEC sibling of
  `TON`/`TONR`. Grounded directly against real `AirStar` (the block that first surfaced it).
  Confirmed structurally identical to `TON` — same `Version`/`Instance`/`time_type` shape, same
  `IN`/`PT`/`ET` ports, no reset port (unlike `TONR`), no `EN`/`ENO`.
- `TimerKind` gains a third variant, `Tof` — needing zero new fields, simpler than `TONR`'s own
  addition. Every touchpoint `TONR` already generalized just needed a third case added.
- 5 new tests, one new fixture. All three suites green: 230 converter (up from 225), 68
  openness-cli, 11 golden-harness.
- **Live-verified — a genuine milestone**: fresh `AirStar` export converted **completely, no
  errors at all**, and round-trips `to-ir → to-xml → to-ir` **byte-identical**, confirmed
  genuinely exercising both `TOF`/`Ne`. `AirStar` is now the **sixth** of `PlantAutoControl`'s own 8
  dependency FBs to fully round-trip (S1 item 19 already got 5 there; this closes `AirStar`,
  the last of the three the S1 item 20 Interface-modeling sweep found). Only `TomraControlSystem`
  (`Swap`) and `MotorVSDSystem` (a `<Call>` missing its `<Instance>`) remain blocked. Full story:
  `docs/notes/stage-gates.md` ("S1 item 23").

**S1 item 22: `Ne` (not-equal comparison) — built, tested, live-verified**

- Picked up per the project owner's own choice, asked directly what `Ne` was likely to be —
  answered from prior evidence already in the repo (an existing `ir/SPEC.md` table row, an unused
  `<>` token already wired up in `IrParser`) before any grounding: the not-equal sibling of
  `Eq`/`Ge`/`Lt`, all three already fully supported.
- Grounded directly against real `AirStar` (the block that first surfaced `Ne`, S1 item 21's own
  live verification) rather than a fresh plan-mode cycle — small, well-precedented, third
  comparison-family addition this session. Confirmed identical shape to `Eq`/`Ge`/`Lt`.
- `FlgNetParser`/`GraphReducer` each gained a fourth comparison case. Everything downstream needed
  **zero changes** — `CompareStep.PartName`/`Expr.Compare.Operator` are both carried verbatim, not
  derived from a hardcoded switch.
- 4 new tests, one new fixture. All three suites green: 225 converter (up from 221), 68
  openness-cli, 11 golden-harness.
- **Live-verified:** the `Ne` error is gone from `AirStar`. Doesn't fully round-trip yet —
  progresses to `TOF` (an off-delay timer, spotted alongside `Ne` during grounding — a new,
  separate, unaddressed gap). Full story: `docs/notes/stage-gates.md` ("S1 item 22").

**S1 item 21: `Access Scope="LocalConstant"` — built, tested, live-verified**

- Picked up per the project owner's own choice — the last of the two real gaps S1 item 20's live
  verification found. Grounded against `MotorVSDSystem`/`AirStar` (4 independent instances): a genuine
  fourth Access shape, `<Access Scope="LocalConstant"><Constant Name="X" /></Access>` — a bare
  reference by name to the block's own declared `Constant`-section member (S1 item 20), no value
  at the reference site at all, neither `AccessNode`'s `<Symbol>` shape nor `ConstantAccessNode`'s
  `<ConstantType>`/`<ConstantValue>` shape.
- Modeled as an `AccessNode` with a one-element `ComponentPath` — `DottedPath`/`FromDottedPath`
  needed zero changes. `FlgNetParser.ParseAccess`/`FlgNetWriter`'s `AccessNode`-writing loop both
  gained a scope-conditional branch. `GraphReducer.cs` needed zero changes (UId-lookup-based
  dispatch, not scope-based).
- 5 new tests, one new fixture. All three suites green: 221 converter (up from 216), 68
  openness-cli, 11 golden-harness.
- **Live-verified:** the `LocalConstant` error is gone from both `MotorVSDSystem` and `AirStar`. Neither
  fully round-trips yet — each hits a different, new, unrelated gap (`MotorVSDSystem`: a `<Call>`
  missing its own `<Instance>`; `AirStar`: `Ne`, not-equal, an unsupported comparison Part Name —
  the first confirmed-real sibling of `Eq`/`Ge`/`Lt`). Neither addressed by this item. Full story:
  `docs/notes/stage-gates.md` ("S1 item 21").

**Follow-up: `Mul`'s own `SrcType` fixed — correction to already-committed S1 item 18 code**

- Picked up immediately after S1 item 20's own live verification found it (`FB AirStar`'s own
  `Mul` carries an ordinary `<TemplateValue Name="SrcType">` instead of the self-closing
  `<AutomaticTyped />` shape S1 item 18 confirmed universal from `MotorDOL`/`EquipmentControlSystem`).
- `FlgNetParser.ParseMulFixedShape` now accepts either shape, hard-erroring only if both or
  neither is present. No new `PartNode` field needed — the existing `AutomaticSrcType`/`SrcType`
  fields already coexist generically. `MulStatementSidecar` gained a nullable `SrcType` field
  (sidecar-only). `FlgNetWriter` needed zero changes.
- 5 new tests, one new fixture. All three suites green: 216 converter (up from 211), 68
  openness-cli, 11 golden-harness.
- **Live re-verified against the real `AirStar` export**: the `Mul`-specific error is gone — the
  block now progresses to the same `Access Scope="LocalConstant"` gap `MotorVSDSystem` also hits
  (unrelated, still open). Full story: `docs/notes/stage-gates.md` ("Follow-up... `Mul`'s own
  `SrcType` fixed").

**S1 item 20: FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`) — built,
tested, live-verified; closes the last real gap from the original 8-FB dependency sweep**

- Picked up per the project owner's own request. Zero real Input/Output/InOut/Constant member XML
  had ever been captured anywhere before this item — the strongest Phase 0 mandate of any item
  this session. Grounded against `TomraControlSystem` (Input/Output) and `MotorVSDSystem`/`AirStar`
  (Constant, two independent instances).
- `Input`/`Output`: same shape as `Static`'s own full member shape, but missing the `SetPoint`
  BooleanAttribute `Static` always carries — `DbInterfaceMembers.ParseMember`/`WriteMember`
  gained a `requireSetPoint`/`includeSetPoint` parameter rather than a parallel type.
- `Constant`: a genuinely distinct third shape (no `AttributeList`, required `StartValue`) — new
  `ParseConstantMember`/`WriteConstantMember`.
- Deliberately a block-level concept only, per ADR-0001 — `CALL`'s own call-site wiring untouched.
- Found and fixed a related bug in the same path: `Return`'s boilerplate was written
  unconditionally for every block; real FBs never have a `Return` section at all — now emitted
  only for non-FB blocks.
- Also corrected `ir/SPEC.md`'s own stale `INTERFACE` grammar sketch, which had never listed
  `STATIC` despite it being the one section actually implemented since S1 item 7 Phase B.
- 10 new/changed converter tests, one hard-error test repurposed into a positive test (its old
  fixture was synthetic, never sourced from a real export). All three suites green: 211 converter
  (up from 207), 68 openness-cli, 11 golden-harness.
- **Live-verified:** whole-block `to-ir` on all 3 previously-blocked FBs confirms the Interface
  gap is genuinely closed for all three — none hit an Interface-section error anymore. None fully
  round-trips yet: each hits a different, new, unrelated gap (`TomraControlSystem`: `Swap`; `MotorVSDSystem`:
  `Access Scope="LocalConstant"`; `AirStar`: a **real correction needed to already-committed S1
  item 18 code** — `Mul`'s own `SrcType` isn't always the self-closing `AutomaticTyped` shape,
  contradicting what S1 item 18 confirmed universal). Flagged as new open items, not fixed
  speculatively. Full story: `docs/notes/stage-gates.md` ("S1 item 20").

**S1 item 19: `TONR`/`Add`/`Lt` — built, tested, live-verified; closes every instruction-level
gap in `PlantAutoControl`'s 5 previously-blocked dependency FBs**

- Picked up per the project owner's choice (via `AskUserQuestion`) over the FC/FB
  parameter-interface gap. Phase 0 grounding (`MotorDOL`/`FilterUnitSystem`) found `TONR`'s shape
  identical to `TON` plus one genuine new port, `R` (reset) — tag-fed, no chain, same shape as
  `PT`. `TimerKind` (`Ton`/`Tonr`) enum added, mirroring `CoilKind`.
- **Scope expanded mid-grounding, confirmed via `AskUserQuestion`**: the same real networks also
  needed `Add` (identical shape to `Mul`) and `Lt` (a third comparison operator alongside
  `Eq`/`Ge`) to fully round-trip. Checked directly against the real wiring that `Add`'s own `en`
  (fed by `Lt`'s `out`) is an ordinary condition, not ENO-chained — no speculative new branch
  added to the `Mul`/`Convert` ENO-chain check.
- `MulKind` (`Multiply`/`Add`) enum added, same pattern as `TimerKind`/`CoilKind`. `Lt` needed no
  new model shape — `ChainStepSidecar.CompareStep` already carries `PartName` generically.
- 16 new converter tests, three fixtures genericized from the real grounded shapes. All three
  suites green: 207 converter (up from 191), 68 openness-cli, 11 golden-harness.
- **Live-verified:** whole-block `to-ir` on all 5 previously-`TONR`-blocked dependency FBs
  (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) all fully converted —
  confirmed genuinely exercising the new code via grep counts, not a lucky no-op.
  `MotorDOL`/`FilterUnitSystem` additionally round-trip `to-ir → to-xml → to-ir` byte-identical. All 5
  of `PlantAutoControl`'s dependency FBs previously blocked by `Mul`/`Convert`/`TONR` now fully
  round-trip as whole blocks — the remaining 3 (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) are blocked
  only by the separate, already-known FC/FB parameter-interface gap. Full story:
  `docs/notes/stage-gates.md` ("S1 item 19").

**S1 item 18: `MUL`/`CONVERT` (arithmetic) — built, tested, live-verified**

- Picked up per the project owner's sequencing after Title ("scope arithmetic support next"),
  planned formally in plan mode. Phase 0 grounding (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`)
  found a real surprise contradicting the plan's own inherited Move/WAND-based assumption:
  despite `DisabledENO="true"` on both `Mul` and `Convert`, real networks chain `Mul`'s own `eno`
  directly into the following `Convert`'s own `en` — a genuine control-flow dependency, not a
  boolean condition. Flagged to the project owner before implementing; approved design: "Go with
  that design."
- New `EnSource` discriminated union (`Condition` | `PrecedingEno`) rather than folding the chain
  into `Expr` — there's no tag to reference "the preceding instruction's own success" by. New
  reserved readable-form sentinel `EN := ENO`, mirroring the existing `TRUE` sentinel precedent.
- `Mul`'s own type is `<AutomaticTyped Name="SrcType" />` — self-closing, no value (TIA infers the
  type from the connected operands), modeled as `PartNode.AutomaticSrcType: bool`. `Convert`
  carries an ordinary `SrcType`/`DestType` `TemplateValue` pair, converting *between* two types.
- 14 new converter tests, two fixtures genericized from the real grounded shapes. All three
  suites green: 191 converter (up from 177), 68 openness-cli, 11 golden-harness.
- **Live-verified:** both the ENO-chained case (`MotorDOL`) and the standalone rail-fed case
  (`ShredderControlSystem`) reduce and round-trip correctly. Whole-block `to-ir` sweep of all 5
  previously-`Mul`/`Convert`-blocked dependency FBs confirmed none hit that error anymore — all
  five now hit `TONR` instead, confirming it as real (previously only a name noticed during an
  earlier sweep). Arithmetic beyond `Mul`/`Convert` stays out of scope; nothing observed needing
  it. Full story: `docs/notes/stage-gates.md` ("S1 item 18").

**S1 items 16/17: network- and block-level `Title` — `PlantAutoControl` now fully round-trips**

- Picked up immediately after `SCoil`/`RCoil` surfaced the gap. Found a real design correction
  needed first, not just a new field: the IR's own `NETWORK <n> "<title>"` line was actually
  sourced from the network's `Comment` field, not `Title` — invisible until now since every real
  network grounded this session had empty Comment; `PlantAutoControl` has the opposite (Title
  populated, Comment empty everywhere). Confirmed the fix with the project owner first
  (`AskUserQuestion`, not assumed): `NETWORK`'s own label now carries `Title`; `Comment` gets its
  own new, separate `COMMENT "..."` line.
- `SimaticMl.CompileUnitSource`/`BlockSource` and `Ir.IrNetwork`/`IrBlock` all gained `Title`/
  `Comment` fields as appropriate. `DbSourceWriter.WriteComment` generalized to
  `WriteMultilingualText(text, compositionName, ...)` once a second real `CompositionName`
  confirmed the shape is shared. New optional `TITLE "..."`/`COMMENT "..."` IR lines at both
  block and network level, surviving an `[empty]`-marked network too.
- Block-level Title was unexpected: initially assumed always-empty (network-level grounding only
  found it populated), confirmed real while grounding `PlantAutoControl`'s own 8 dependency FBs —
  `MotorVSDSystem`/`AirStar` both carry a real, identical block-level Title ("VSD Motor").
- Sanitization: both Titles are identifying free text (equipment/process names) — same
  hard-error-if-unmapped treatment as Comment, via two new `SanitizationMap` dictionaries
  (`NetworkTitles`, `Titles`).
- 13 new converter tests. All three suites green: 177 converter (up from 164), 68 openness-cli,
  11 golden-harness.
- **Live-verified, same session:** `converter to-ir` on a fresh whole-block `PlantAutoControl` export
  now succeeds completely — the first real production block this whole session to fully convert as
  a whole block — and `to-ir → to-xml → to-ir` round-trips byte-identical.
- **Attempted the true TIA cycle, per the project owner's explicit instruction to route through
  `SampleProject` rather than `JOB9002`.** Import succeeded (TIA accepted the regenerated XML into
  a completely unrelated project); compile failed with 502 errors, entirely missing tags (323
  distinct paths) and missing FB library blocks (8 dependency FBs) — `SampleProject` has neither.
  Not a converter defect — a block doesn't carry its project dependencies with it. Left the
  imported-but-uncompiled block in `SampleProject` for manual cleanup, as agreed.
- Grounded the 8 dependency FBs directly afterward: 0 of 8 convert cleanly today — 5 blocked by
  arithmetic (`Mul`/`Convert`), the rest by the same already-known FC/FB parameter-interface gap.
  Project owner's decision: scope arithmetic support next. Full story: `docs/notes/stage-gates.md`
  ("S1 items 16/17").

**S1 item 15: `SCoil`/`RCoil` (set/reset coils) — built, tested, surfaced a new whole-block gap**

- Picked up per the project owner's own sequencing after `CALL`: 3 of each real in
  `FC PlantAutoControl`, also seen alongside TON in `FB MotorDOL`'s own earlier grounding. Grounded
  against two independent real instances of each before any code — both completely bare
  (`<Part Name="SCoil"/"RCoil" UId="N" />`), exact same `in`/`operand` wire shape as a plain
  `Coil`, never a producer. Structurally identical to `Coil` in every respect.
- Smallest diff of any S1 item this session: `GraphReducer.ReduceOneChain`/
  `FlgNetBuilder.BuildOneChain` reused verbatim for all three kinds. Only addition: a new
  `CoilAssignment.Kind` field (`Assign`/`Set`/`Reset`), mirroring `CompareStep`'s own
  `PartName`↔`Operator` split. `CoilAssignmentSidecar` needed no new field — `BuildOneChain`
  already takes the model alongside its sidecar, so `Kind` is derived directly.
- Readable-form keywords `SCOIL`/`RCOIL` chosen to match every other keyword's own
  mirror-the-source-Part-Name convention (`WAND` is the one exception, for a collision that
  doesn't apply here).
- 6 new converter tests, one fixture interleaving all three kinds on a shared rail — passed on
  first run. All three suites green: 164 converter (up from 158), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated the real network already grounded for `CALL` (it also
  has the block's own `SCoil`/`RCoil` pair) — reduces and round-trips completely.
- **Then attempted a whole-block round-trip of `PlantAutoControl`**, since this should have closed its
  last instruction-level gap. Hit a different, already-known wall instead: every one of its 20
  real networks carries a non-empty `Title` (distinct from `Comment`), which `BlockSourceParser`
  already hard-errors on (documented earlier, `tests/golden/README.md` — not a new discovery,
  just newly encountered on this specific real block). `PlantAutoControl` has no remaining
  instruction-level gap but still doesn't round-trip as a whole block. Full story:
  `docs/notes/stage-gates.md` ("S1 item 15").

**S1 item 14: `CALL` (FB/FC block calls) — built, tested, live-verified in-memory with `Not`**

- Picked up per the project owner's own decision at the close of S1 item 13: build block calls
  next, since `Not` and `<Call>` co-occur in every one of `FC PlantAutoControl`'s 20 real networks.
  Planned with plan-mode rigor first, including a Phase 0 grounding pass before any design was
  finalized (no `<Call>` XML had actually been inspected before this item).
- Grounding found `<Call>` genuinely isn't a `<Part Name="Call">` — it's its own sibling element
  under `<Parts>` (`<Call UId="N"><CallInfo Name="..." BlockType="FB"><Instance/><Parameter/>...
  </CallInfo></Call>`). `FlgNetParser`/`FlgNetWriter` adapt this into/from an ordinary
  `PartNode(Name="Call")` so the rest of the pipeline never needs a parallel type.
- Also found: arguments are sparse, not a full interface snapshot — 19 of 20 real calls have
  zero wired parameters at all (not present-but-empty, simply absent); the one wired example has
  10 (8 Input, 2 Output). Instance reuses the exact same shape as TON's own `<Instance>`,
  confirmed identical — the reuse `ir/SPEC.md`'s TON section had already anticipated.
- Design mirrors Move/WAND (`en` via the same `TraceChain` fan-out mechanism, all 20 real
  instances directly rail-fed) crossed with TON's own Instance reference (factored
  `ParseInstanceReference` out of `ParseTon` for reuse). Readable-form syntax:
  `CALL BlockName(Instance, EN := <expr>, Param := <expr>, ... => OutParam, ...)` — `EN` always
  shown explicitly, matching MOVE/WAND's own convention.
- 14 new converter tests, two fixtures genericized from the two real shapes found — passed on
  first run. All three suites green: 158 converter (up from 144), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated `FC PlantAutoControl`'s richest real network (`Not`
  wrapping an OR-of-comparisons, 8 independent Contact→Coil rungs, and the fully-wired
  10-parameter call, all sharing one rail wire) — reduces and round-trips completely through the
  in-memory pipeline, the first real network combining `Not` and `Call` together to do so. Still
  not verified through the true TIA `import → compile → re-export → Normalizer` cycle:
  `openness-cli import` has no per-network granularity, and `PlantAutoControl` as a whole still has
  `SCoil`/`RCoil` elsewhere. Per the project owner's own sequencing, `SCoil`/`RCoil` is next.
  Full story: `docs/notes/stage-gates.md` ("S1 item 14").

**S1 item 13: `Not` (standalone boolean inverter) — built, tested, gold-standard gap documented**

- Investigated whether the converter as built could handle `FC PlantAutoControl` (a real, complex
  orchestrator block); its very first network hits `Not` first. Grounded twice, independently,
  against two different real instances in that block before writing any code — identical bare
  shape both times (`in`/`out` only, no operand/Access, no TemplateValue).
- Design: no new top-level production — `Not` is purely a new chain-position kind, discovered
  only when some other production's own `TraceChain` walk hits one. Resolved via the same
  "chain-terminal via recursive `TraceChain`" pattern OR-merge branches established (S1 item 11):
  a fully self-contained recursive call on `Not`'s own `in`, wrapped in the already-existing
  `Expr.Not`. `ChainStepSidecar.NotStep` mirrors `OrBranch`'s `(Steps, RailWireUId)` shape. Zero
  new IR-text grammar needed; `PartNode`/`FlgNetParser`/`FlgNetWriter` needed zero new fields or
  code — falls through to existing generic bare-part handling both ways.
- Both real instances tap a shared wire via genuine fan-out (same mechanism proven for Move's
  `en` tap, S1 item 10), here feeding back into a boolean chain instead of a side-effect write.
- 6 new converter tests, fixture built directly from the real `PlantAutoControl` shape (genericized) —
  passed on first run. All three suites green: 144 converter (up from 138), 68 openness-cli, 11
  golden-harness.
- **Mid-session course-correction from the project owner:** "the gold standard is a lossless full
  cycle" — flagged that every prior "live-verified" claim (items 10–12) was only ever the
  in-memory pipeline, never the true TIA `import → compile → re-export → Normalizer` cycle.
  Investigated whether `Not` could reach that bar: no viable reference-project block to extend
  without either overwriting committed content or requiring new-block authorship (the project
  owner's own TIA-UI action, per the `TimerSample` precedent); a full sweep of all 20
  `PlantAutoControl` networks found **every one pairs `Not` with a `<Call>`** (block calls, not yet
  built) — no real network can be isolated to prove `Not` alone through the full cycle yet.
  Reported honestly rather than settling silently; project owner chose to build block calls next
  to close this out for both constructs together. Full story: `docs/notes/stage-gates.md`
  ("S1 item 13").

**S1 item 12: WAND (bitwise word AND) — corrects the AND-merge premise, live-verified**

- Project owner picked "AND-merge" as the next S1 item. Grounded first, per hard rule 3: searched
  28 real LAD blocks specifically for a boolean parallel-branch AND-merge (an `O`-sibling) —
  **found none anywhere.** Architecturally expected in hindsight: boolean AND in ladder logic is
  always plain series Contacts, never needing an explicit merge Part the way OR genuinely does.
- What the sweep found real instead: `Part Name="And"` (`FB VSDUpdateComs`,
  `Word AND 16#89 -> ControlWord`) — an entirely different thing, a bitwise/word-level box
  instruction, not a boolean chain position. Reported to the project owner before building
  anything (corrects the task's own premise, not just an implementation detail) — chose to build
  the real instruction instead of the speculated one.
- Design mirrors `Move` closely (same `TraceChain` fan-out-tap mechanism for `en`) crossed with
  `O`'s own `Cardinality`-driven shape (here driving input count, `IN1`..`INn`, not branch count)
  and a comparison's own `SrcType`. `PartNode` needed zero new fields — `Cardinality`/`SrcType`
  already existed, just never co-occurring on one Part before; `ParseCardinality`/`ParseSrcType`
  (renamed from `ParseOrCardinality`/`ParseComparisonSrcType`) needed a real fix to look their
  `TemplateValue` up by `Name` rather than assuming it's the only one present.
- IR keyword is `WAND`, not `AND` — deliberately avoids colliding with the boolean `AND` infix
  operator, a converter-owned vendor-neutral name mapping (same precedent as `MOVE_BLK_VARIANT` →
  `MOVE`).
- Real, previously-unexercised parser gap found along the way: Siemens' `<base>#<value>` numeric
  literal notation (`16#89`) wasn't recognized by `ParseLeaf`'s shape-based literal detection —
  fixed generically (not hardcoded to base 16).
- 8 new converter tests, fixture built directly from the real `VSDUpdateComs` shape (genericized)
  — passed on first run. All three suites green: 138 converter (up from 130), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated `FB VSDUpdateComs`'s real network — reduces and
  round-trips completely, including the `16#89` literal round-tripping cleanly through the text
  grammar. Full story: `docs/notes/stage-gates.md` ("S1 item 12").

**S1 item 11: OR-merge branches generalized to recursive chains, live-verified**

- Closed the gap items 9 and 10 both left open: `GraphReducer.ResolveOrMerge` required every
  branch to be a single Contact fed directly by Powerrail — real data hit this twice
  (`ControlDelays`' `O(41)` combining two comparisons fed by a further OR-merge; `MotorDOL`'s
  `O(45)` fed by a shared, non-rail-fed Contact). Planned via `/plan`, approved before any code.
- Design decision confirmed with the project owner first (`AskUserQuestion`): once a branch can
  be a compound expression, the IR text needs real operator precedence — `AND` binds tighter than
  `OR`, parens only where precedence alone would misparse.
- The fix: a branch is resolved via the exact same `TraceChain` mechanism as a Coil's condition/a
  TON's `IN`/a Move's `en` — recursion, not a new algorithm. `ResolveOrMerge` shrank from a
  hand-rolled single-hop walker (three separate hard-error checks) to a thin per-branch loop.
  `ChainStepSidecar.OrStep.Branches` changed from a flat `ContactStep` list to `OrBranch`
  (Steps + nullable RailWireUId, mirroring every other production's own chain shape); the outer
  chain's own `RailWireUId` is now `null` whenever it terminates at an `OrStep` (mirrors the
  existing `TimerOutputStep` precedent). `FlgNetBuilder` needed no new accumulation mechanism —
  the MOVE-era shared, de-duplicating endpoint accumulator already handles it.
- `IrParser.ParseExpr` was a real, previously-unexercised bug (naive `.Contains(" AND ")` checked
  before `.Contains(" OR ")` regardless of actual precedence) — rewritten as a proper
  precedence-climbing recursive descent parser; `IrSerializer.SerializeExpr` became
  precedence-aware to match.
- Repurposed 3 existing hard-error fixtures/tests into positive ones (`NestedOrMerge.xml`,
  `OrMergeMultiContactBranch.xml`, `OrMergeOfComparisons.xml`) rather than inventing new test
  data — same pattern already used twice this project. One new fixture
  (`OrMergeSharedPrefixBranches.xml`, the one real shape none of the three already covered) —
  **passed on first run**. 14 new standalone grammar tests, also all passing on first run,
  including a deeply-nested mixed-precedence case. All three suites green: 130 converter tests
  (up from 106), 68 openness-cli, 11 golden-harness.
- **Live-verified, same session:** isolated both real networks that were previously blocked
  exactly at this OR-merge limitation (`ControlDelays`' `CompileUnit "8"`, `MotorDOL`'s "HMI Motor
  Status Telemetry" network) via fresh exports and a throwaway test (real data deleted after use).
  **Both now reduce and round-trip completely.** `ControlDelays`' Coil condition turned out
  deeply nested (`Or` of two `And`s, each containing a nested `Or`) — confirms the precedence
  grammar renders genuinely real, richer-than-any-fixture expressions correctly. `MotorDOL`'s
  telemetry network now reduces all 5 Moves (previously only 3 of 5 got past `Reduce()` before
  the OR-merge blocked the rest). Full story: `docs/notes/stage-gates.md` ("S1 item 11" + its live
  verification section).

## 2026-07-11

**S1 item 10: MOVE support, live-verified (uncommitted)**

- Added `Part Name="Move"` support, grounded against a real export (`FB MotorDOL`'s "HMI Motor
  Status Telemetry" network) before building anything. Real finding: a Move is neither a boolean
  chain position (like Eq/Ge) nor a self-contained production like TON — it's a side effect
  *tapped off* a chain position's own output via genuine wire fan-out (the same wire feeds both
  the Move's `en` and the chain's real continuation).
- Core `GraphReducer.TraceChain` redesign: the fan-out check changed from "exactly one other
  endpoint on a wire, else throw" to "find the single genuine producer endpoint, ignore every
  other endpoint" — needed because every prior capability assumed exactly two endpoints per wire,
  which a Move's tap genuinely breaks.
- `FlgNetBuilder` rebuilt around a shared, de-duplicating endpoint accumulator (`AddPart`/
  `AddEndpoint`, keyed by UId) — needed because multiple Moves' own `en` chains telescope through
  the same upstream Contacts a Coil's (or another Move's) chain already walked, and the reducer
  deliberately re-derives duplicate chain-step data per production (correct, not a bug); only the
  builder's de-duplication prevents that from becoming duplicate XML on rebuild.
- Readable-form syntax: `MOVE(EN := <expr>, IN := <expr>) => <dest>`.
- 12 new converter tests (`MoveTests.cs`), including a fixture built from the real telescoping/
  fan-out shape (genericized) — **passed on its first run**, proving the redesign against the
  exact shape it was built for. 106 converter tests pass (up from 94); `openness-cli`/
  golden-harness suites unaffected, not re-run this pass.
- **Live-verified, same session:** isolated the real "HMI Motor Status Telemetry" network from a
  fresh `FB MotorDOL` export (throwaway test, real data deleted after use). The real topology is
  richer than any fixture — one Contact's outgoing wire fans out to **three** consumers, not two
  — and parsing/fan-out-producer-identification handled it correctly. `Reduce()` failed on the
  whole network, but for a pre-existing, already-deferred reason unrelated to MOVE (a
  multi-contact OR-merge branch not fed directly from Powerrail — same class of gap already known
  from comparisons); confirmed via Part document order that 3 of the network's 5 real Moves
  (including one tapping the genuine 3-way fan-out) reduced successfully before the throw. A
  fully self-contained real sub-network (`Contact47 → Move48`, one necessary rail-wire trim,
  otherwise byte-identical to the export) round-tripped cleanly end to end.
- Docs updated: `ir/SPEC.md` (readable-form entry), `src/converter/README.md` (new section, plus
  fixed a stale scope line that hadn't been updated since before TON/comparisons landed),
  `docs/notes/stage-gates.md` (S1 item 10 + live-verification sections, refreshed summary table
  row).

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
  68 openness-cli / 11 golden-harness tests all pass.

**S1 item 9 continued: live re-verification against real data**

- Cleared 3 stale TIA Portal processes (project owner's go-ahead) and confirmed a fresh single
  instance launches fine — settling at 3 processes again turned out to be normal for this
  machine, not itself the earlier problem.
- Fresh `export`/`to-ir` of `FC ControlDelays` reproduced the predicted result exactly: hard-errors
  on `Mul` in Network 1 (unrelated to Eq/Ge, which live entirely in Networks 2–4) before ever
  reaching the comparison content — a real, honest caveat (Networks 2–4 weren't exercised by that
  run) rather than declaring victory early.
- Isolated Network 2 directly (`FlgNetParser` → `GraphReducer` → `FlgNetBuilder` → `FlgNetWriter`,
  a throwaway test against the real extracted XML, deleted after) and got a precise, better-than-
  expected result: `Eq(32) → Contact(33) → TON(34).IN` reduced successfully against genuinely
  live, unmodified data; the Coil's own chain (needing `O(41)`, the OR-merge of two comparisons)
  threw exactly the predicted, already-tested error, confirming the OR-merge-composition scope
  decision is correct on the exact real network that motivated it.

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
