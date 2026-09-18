# converter — whole-document kinds

DBs, PLC data types, tag tables, the sidecar, and the `to-ir` / `to-xml` file contract — the document shapes rather than the logic inside them.

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

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

