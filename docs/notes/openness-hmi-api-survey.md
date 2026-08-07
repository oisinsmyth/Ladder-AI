# TIA Openness — HMI API surface survey (2026-08-07)

Reconnaissance only. **Nothing here changes scope.** `10-non-goals.md` still keeps HMI engineering
("screens, scripts, faceplates") out, and this note does not propose lifting that — it answers
questions that several open entries said had to be answered *before* anyone could scope the work:

- `16-future-ideas.md` **FI-35 §5** — "Unknown, and it changes the deliverable's shape: whether
  Openness can drive the HMI alarm list directly instead of via the UI's spreadsheet
  export/import. **Not investigated.** Should be answered before building." → **Answered below (§6).**
- `notes/hmi-alarm-generation.md` **§5** — "No Openness HMI API was involved, and none was
  investigated", plus its three consequences (no compile gate, bit numbering not in the export,
  the collapsed `Class` column). → **All three revisited below (§6).**
- **FI-18** (PLC/HMI boundary skill) — its stated risk is scope creep into building screens. The
  survey below is what makes that boundary discussable with facts rather than guesses.

## Method and epistemic status

Same technique as `openness-api-surface-v20.md`, and the same caveat applies: **this is type-shape
only.** Static reflection over the installed V20 Openness assembly, cross-read against the
doc-comments file Siemens ships beside it:

```powershell
$asm = [Reflection.Assembly]::LoadFrom("C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll")
try { $types = $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }
```

It tells you what compiles. **It does not prove runtime behaviour** — no HMI device was opened, no
screen was created, nothing was imported. Every "can" below means "the API admits it", not "it was
observed working". `Siemens.Engineering.Hmi.dll` (20 KB, present for V17–V20) is a shim; the real
object model is inside `Siemens.Engineering.dll`.

Two accuracy notes worth carrying forward, both instances of "ground via grep, not visual read":

- Counting `IsPublic` alone **misses nested public types**. Re-run including `IsNestedPublic`; for
  these namespaces the counts happened to be identical, but the filter was wrong before it was right.
- The shipped `Siemens.Engineering.xml` **documents members that do not exist**. It carries
  doc-comments for `Siemens.Engineering.Hmi.Logging.*` (`AcquisitionMode`, `HysteresisMode`,
  `LimitScope`); that namespace has **zero types** in the V20 DLL. Treat the XML as commentary on
  the DLL, never as evidence of the DLL.

## 1. The headline: two disjoint APIs with opposite shapes

There is no single "HMI Openness API". There are two, they share nothing, and — this is the part
that decides everything downstream — **each one has exactly what the other lacks.**

| | **Classic** `Siemens.Engineering.Hmi.*` | **Unified** `Siemens.Engineering.HmiUnified.*` |
|---|---|---|
| Targets | Comfort / Advanced / Professional panels | Unified Comfort Panels, Unified PC RT |
| Public types (V20) | **64** | **487** |
| Root object | `HmiTarget` | `HmiSoftware` |
| Create a screen from scratch | **No** | **`Screens.Create(name)`** |
| Objects *inside* a screen | **Not modelled at all** | **Full typed tree** |
| File export/import of screens | **Yes — SimaticML** | **No** |
| Alarms in the object model | **None whatsoever** | **Yes, creatable** |
| Per-object validation | No | **`Validate()`** |

**Classic is a file pipeline with no object model. Unified is an object model with no file
pipeline.** Everything in §5 follows from that one sentence.

## 2. The strategic signal: classic is frozen

Type counts across the four API versions shipped side-by-side in the V20 install:

```
V17: publicTypes=1702  classicHmi= 64  HmiUnified=406
V18: publicTypes=1895  classicHmi= 64  HmiUnified=450
V19: publicTypes=1982  classicHmi= 64  HmiUnified=463
V20: publicTypes=2182  classicHmi= 64  HmiUnified=487
```

**Classic HMI has not gained or lost a single public type across four major releases.** Not one.
Over the same span the assembly overall grew ~28% and Unified grew ~20%. Whatever Siemens is
investing in Openness, none of it is going to classic HMI.

This is the single most decision-relevant fact in the survey, and it cuts both ways: the frozen
surface is also a *stable* surface — a classic-HMI capability would not be chasing a moving API,
which is the opposite of the situation on the Unified side.

## 3. Classic HMI (`Siemens.Engineering.Hmi.*`) — 64 types

Namespace breakdown: `Screen` 31, `RuntimeScripting` 10, `Tag` 10, `TextGraphicList` 4,
`Communication` 2, `Cycle` 2, `Faceplate` 2, `Globalization` 2, root 1.

`HmiTarget` exposes `ScreenFolder`, `ScreenPopupFolder`, `ScreenTemplateFolder`,
`ScreenSlideinFolder`, `ScreenGlobalElements`, `ScreenOverview`, `TagFolder`, `TextLists`,
`GraphicLists`, `VBScriptFolder`, `Connections`, `Cycles`.

**The decisive shape — `Screen` has no contents.** Its entire public surface is:

```
Screen : Object
  Name, Parent
  GetAttribute/SetAttribute/GetAttributes/SetAttributes/GetAttributeInfos
  GetService<T>()
  Export(FileInfo, ExportOptions)      -- doc-comment: "Simatic ML export of a screen"
  Delete()
```

There is **no `ScreenItems` property, no button type, no I/O field type, no shape type** — the
`Siemens.Engineering.Hmi.Screen` namespace's 31 types are screens, popups, templates, slide-ins,
their folders and compositions, plus `SlideinType`/`VisibilityModes` enums and library-type
wrappers. Nothing that lives *on* a screen is modelled.

And `ScreenComposition` has **no `Create(name)`**:

```
ScreenComposition
  CreateFrom(ScreenLibraryTypeVersion) : Screen
  CreateFrom(MasterCopy)               : Screen
  Import(FileInfo, ImportOptions)      : IList
  Find(name)
```

So a new classic screen arrives one of exactly three ways: **imported from SimaticML**, copied from
a **library type**, or copied from a **master copy**. `SetAttribute` still reaches screen-level
properties (size, colour, number) on an existing screen, so "tweak a property" is possible without
a file — but "author the content" is not.

Export/import coverage is otherwise broad and uniform (`Export(FileInfo, ExportOptions)` on the
object, `Import(FileInfo, ImportOptions)` on the composition): screens, popups, templates,
slide-ins, screen global elements, screen overview, tags, tag tables, text lists, graphic lists,
VB scripts, connections, cycles, multilingual graphics.

**Classic has no alarm API at all.** There is no alarm type anywhere under
`Siemens.Engineering.Hmi.*` — discrete, analog, class, or log. (`Siemens.Engineering.SW.Alarm` is
PLC-side ProDiag/supervision, a different thing entirely.)

## 4. Unified (`Siemens.Engineering.HmiUnified.*`) — 487 types

`HmiSoftware` is the root and carries 20 compositions: `Screens`, `ScreenGroups`, `Tags`,
`TagTables`, `TagTableGroups`, `SystemTags`, `DiscreteAlarms`, `AnalogAlarms`, `AlarmClasses`,
`AlarmLogs`, `DataLogs`, `AuditTrails`, `HmiAlarmAuditClass`, `Connections`, `Scripts`,
`HmiTextLists`, `HmiSystemTextLists`, `OpcUaAlarmTypes`, `PlantObjectTags`, `RuntimeSettings`.

**Screens are constructed, not imported:**

```
HmiScreenComposition.Create(name) : HmiScreen
HmiScreen.ScreenItems : HmiScreenItemBaseComposition
    Create<T>(string name)                            -- generic: T is the widget/shape/control type
    Create<T>(string name, string containedTypeValue)
```

`HmiScreen` itself carries `Width`/`Height`/`BackColor`/`BackGraphic`/`ScreenNumber`/alignment/
`EventHandlers`, plus `ResizeScreen()`.

**The object catalogue** (V20): 18 widgets, 20 shapes, 10 controls, 74 "parts", 15 feature
interfaces, 97 enums, 85 event-handler types.

- **Widgets** — `HmiButton`, `HmiIOField`, `HmiSymbolicIOField`, `HmiLabel`, `HmiTextBox`,
  `HmiBar`, `HmiGauge`, `HmiSlider`, `HmiClock`, `HmiCheckBoxGroup`, `HmiRadioButtonGroup`,
  `HmiListBox`, `HmiToggleSwitch`, `HmiTouchArea` (+ 4 bases).
- **Shapes** — rectangle, line, polyline, polygon, circle, ellipse, arcs, segments, text,
  graphic view.
- **Controls** — `HmiAlarmControl`, `HmiTrendControl`, `HmiFunctionTrendControl`,
  `HmiProcessControl`, `HmiSystemDiagnosisControl`, `HmiMediaControl`, `HmiWebControl`,
  `HmiFaceplateContainer`, `HmiDetailedParameterControl`.
- **Parts** — the sub-object layer: fonts, padding, thresholds, axes, legends, rulers, data-grid
  columns, tool/status bars, faceplate and custom-control interfaces.

Properties are **strongly typed, not stringly typed** — e.g. `HmiIOField` exposes `ProcessValue`,
`IOFieldType`, `OutputFormat`, `InputBehavior`, `Thresholds`, `Font`, `ForeColor`, `BorderWidth`,
`Left`/`Top`/`Width`/`Height`, `RotationAngle`, `Authorization`, `TabIndex`, `ToolTipText`
(`MultilingualText`), and inherits `Dynamizations` + `PropertyEventHandlers`.

**Dynamization is the tag-binding layer**, and it is per-property:

```
UIBase.Dynamizations : DynamizationBaseComposition
    Create<T>(string propertyName) : T
DynamizationType = None | Tag | Script | ResourceList | Flashing | Expression | TagParameter
```

- `TagDynamization` — `Tag`, `PlcTag`, `Address`, `ReadOnly`, `UseIndirectAddressing`,
  `ValueConverter`.
- `ValueConverter` — either a `Formula` string or a `MappingTable` whose `ConditionType` is
  `Range | Bitmask | Singlebit | Expression`, with typed entries (`MappingTableEntryRange`
  From/To, `MappingTableEntryBitmask` Condition/Relevant, `MappingTableEntrySimple`).
- `ScriptDynamization` — `ScriptCode`, `GlobalDefinitionAreaScriptCode`, `Async`, and a `Trigger`
  (`Disabled | T100ms | T250ms | T500ms | T1s | T2s | T5s | T10s | CustomCycle | Tags |
  AutomaticTags`).
- `FlashingDynamization` — `Color`/`AlternateColor`, `FlashingCondition`
  (`Never | Always | RangeViolation`), `FlashingRate`.

**Alarms are first-class and creatable** — `HmiDiscreteAlarm` (`TriggerBitAddress`,
`RaisedStateTagBitNumber`, `AcknowledgmentControlTag` + `…BitNumber`, `AcknowledgmentStateTag` +
`…BitNumber`, `TriggerMode`), `HmiAnalogAlarm` (`TriggerAddress`, `Condition`, `ConditionValue`),
`HmiAlarmClass` (`Priority`, `StateMachine`, `RaisedState`, `AcknowledgedState`, `ClearedState`,
`AcknowledgedClearedState`, `Log`). All three compositions expose `Create(name)`.

**The plant object model (`Cpm`, 24 types) is read-only.** `PlantObjectInterface` /
`PlantObjectInterfaceMember` / `PlantView` compositions expose `Find(name)` but **no `Create`**.
Note the shape though: `PlantObjectInterface` carries `PlcTag` and `PlcName` alongside its
`Members` tree — that is structurally the same idea as this project's own PLC-side interface-UDT
house style (C-132/C-118), which is a meaningful convergence if FI-18 ever gets scoped.

**But Unified screens have no export/import.** Sweeping every `Import`/`Export` method in the
Unified namespaces returns only: `HmiTagComposition`, `HmiScriptModule(Composition)`,
`HmiTextListComposition`, `HmiSystemTextListComposition` (all `DirectoryInfo`-based), and
`OpcUaAlarm.Import(string)`. `IChromDataExchangeExport` is implemented by exactly **one** type:
`HmiScriptModule`. **There is no `HmiScreen.Export` and no `HmiScreenComposition.Import` in V20.**

> Web sources describe "JSON-based screen export/import with all properties and dynamizations" for
> Unified. That is **not** in the V20 Openness API as reflected — it is either a third-party tool
> (e.g. TIA Openness Manager), the TIA UI's own feature, or a later version. Do not plan against it
> without re-reflecting on the actual target install.

## 5. What this means for AI HMI design

Three architectures are available, and the choice is forced by which panel family the job uses —
not by preference.

### A. Classic panels → the LAD pipeline, re-aimed

Because classic screens move as **SimaticML** — the doc-comment says so literally, "Simatic ML
export of a screen" — the pipeline shape is **the one this repo already built**:

```
export (SimaticML) → IR → generate/edit → SimaticML → import → compile
```

Every structural asset transfers: the round-trip harness, the Normalizer, `drift-check`'s
export-vs-source invariance idea, `diff`'s untouched-network proof, the IR-as-the-only-editable-
surface rule (hard rule 7 generalises verbatim). What does **not** transfer is the content model —
a screen IR is a 2-D layout-and-binding language, not a rung language, so `ir/SPEC.md` would need a
second dialect, not an extension.

The real cost is the verification story: classic gives you **no per-object validation**, so the
gate is a device-level compile — and this repo already knows from FI-52 exactly how much a
device-level compile is worth on its own.

### B. Unified panels → direct construction, no converter at all

No XML, no IR, no round trip. The AI emits a structured build plan; a builder walks
`Screens.Create` → `ScreenItems.Create<T>` → set typed properties →
`Dynamizations.Create<TagDynamization>("PropertyName")`.

This is **easier to write and harder to review**, and the asymmetry matters:

- **Better gate.** `UIBase.Validate() : IList<HmiValidationResult>` returns per-property `Errors`
  *and* `Warnings`, on every screen and every screen item. `IHmiScript.SyntaxCheck()` checks script
  dynamizations. This is a genuine pre-commit check with **no PLC-side analogue** — nothing in
  `SW.Blocks` offers per-object validation short of a compile.
- **No diff surface.** With no export, there is no text artifact to diff, hash, or store. This
  repo's entire review model — IR diffs, `ir-hash`, golden round-trips, "prove the untouched
  networks identical" — assumes a serialisable representation. On Unified you would have to
  **build the serialiser yourself** (walk the live model, emit canonical text) before any of the
  existing review discipline could apply. That is a real, non-obvious cost and it should not be
  discovered late.

### C. Alarms and the boundary contract only → FI-18's actual size

The smallest viable step, and the one the repo already has a live use case for. Unified turns it
from a spreadsheet-authoring exercise into an API-backed one; classic leaves it exactly where
`hmi-alarm-generation.md` left it. Details in §6.

**The tension to put to the owner:** classic is the installed base but Siemens has not touched its
API in four releases; Unified is where the whole investment is but requires Unified hardware and
licences. An AI HMI capability built on classic is built on a frozen, file-shaped surface that fits
this repo's existing machinery almost perfectly. One built on Unified is built on a live,
object-shaped surface that fits its *review* machinery hardly at all.

## 6. Direct answers to the open repo questions

**FI-35 §5 — "can Openness drive the HMI alarm list directly?"** — **Answered: it depends entirely
on the panel family, and the answer is absolute in both directions.**

- **Unified: yes.** `HmiSoftware.DiscreteAlarms.Create(name)` with typed
  trigger/acknowledgement/bit-number properties, plus `AlarmClasses.Create(name)`. The spreadsheet
  hop is removable and there is something real to verify against.
- **Classic: no, and not partially.** There is no alarm type in the classic namespace at all. The
  spreadsheet route taken on the live job was **not** a missed opportunity — it was the only route
  available. That is worth recording plainly, because the note left it open as a possible oversight.

**`hmi-alarm-generation.md` §5's three consequences**, revisited:

1. **"There is no compile gate on the HMI side."** — True for classic. **False for Unified**, which
   has `Validate()` per object *and* per property, returning warnings as well as errors. This is
   the strongest single argument for Unified if a generation capability is ever scoped, because it
   is the only version of this that satisfies hard rule 4's *spirit* rather than approximating it.
2. **"HMI-side bit numbering within a Word trigger tag is not in the export — it is an HMI
   convention."** — True for classic. **False for Unified**: `HmiDiscreteAlarm.RaisedStateTagBitNumber`
   (and the two acknowledgement bit-number properties) are first-class API fields. The mapping the
   tool was told it must never assert is, on Unified, a value it can *read and write*.
3. **"The workbook's `Class` column collapses two independent axes"** (C-506 severity vs C-507
   acknowledgement). — That collapse is an artifact **of the spreadsheet**, not of the domain.
   `HmiAlarmClass` keeps them separate: `Priority` on one axis; `StateMachine`,
   `AcknowledgedState`, `ClearedState`, `AcknowledgedClearedState` on the other. The E-Stop
   acknowledgement error that bit live is **not expressible** as the same error against this API —
   you cannot blanket-fill one field and silently decide the other. The owner ruling that note asks
   for is still needed, but on Unified it is a mapping decision, not a lossy-compression decision.

**A bonus find, PLC-side, directly relevant to FI-35.** Sweeping every type whose name contains
"Alarm" (the check that confirmed classic has none) turned up `Siemens.Engineering.SW.Alarm` — 12
types that are **not** HMI at all, but PLC alarm *text* handling, including an official spreadsheet
path: `PlcAlarmTextProvider`, `PlcAlarmTextListProvider`, `AlarmClassDataProvider`,
`PlcAlarmTextXlsxExportOption`, `PlcAlarmTextXlsxResult(State)`, `AlarmClassExportImportResult`,
and `SW.Alarm.TextLists.*` (`PlcAlarmUserTextlist`, `PlcAlarmSystemTextlist`,
`PlcAlarmTextlistGroup`, `PlcAlarmTextlistListRange`). Openness has **first-class XLSX export for
PLC alarm texts.** The live job hand-wrote its workbook with `openpyxl` against a supplied
template; whether this API would have produced a better-formed one — or is aimed only at
ProDiag/supervision alarms rather than the C-501 category-word style this project uses — was not
determined and is worth ten minutes before `alarm-scan` is built.

**FI-18** — its stated risk was scope creep from "defining the boundary" into "building screens".
The survey sharpens that: on **classic**, the boundary is the only thing you *could* build (screen
authoring needs a SimaticML dialect nobody has written). On **Unified**, screen-building is
genuinely one API call away from boundary-defining, so the guard rail has to be a deliberate
decision rather than a happy accident of what the API refuses to do.

## 7. Unverified — do not treat as proven

- **Nothing was run against a live HMI device.** No screen created, no alarm created, no import,
  no validation call. Type-shape only.
- **The compile path is inferred, not observed.** `ICompilable` exists (`Compile() : CompilerResult`)
  and `HmiTarget` implements `IEngineeringServiceProvider`, so `HmiTarget.GetService<ICompilable>()`
  *compiles*. Whether it returns non-null is untested. Note that **`HmiSoftware` (Unified) does not
  implement `IEngineeringServiceProvider` at all** — it has no `GetService<T>()` — so a Unified
  compile must be reached via the device/device-item, not via the software object. Worth confirming
  before anyone designs a gate around it.
- **`Create<T>` constraints are unknown.** The generic `ScreenItems.Create<T>(name)` presumably
  rejects some `T`, and `Create<T>(name, containedTypeValue)` exists for container types. Which
  types are legal where is not derivable from reflection.
- **Licensing/hardware.** Unified requires Unified-capable hardware and its own engineering/runtime
  licences. Whether the S7-1200 G2 target here would ever pair with a Unified panel is a
  procurement question, not an API one — and it decides which of §5's architectures is even
  reachable.
- **V21 exists** and was not examined. Given classic's four-release freeze, the useful question for
  V21 is only ever about Unified.

## 8. If this is ever picked up

In rough order of cost, and none of it authorised by this note:

1. Confirm which panel family real jobs actually use. **This decides everything** and costs nothing.
2. Verify the compile/validate path live against one throwaway HMI device — the cheapest way to
   turn §7's inferences into facts.
3. For classic: export one real screen to SimaticML and look at it. That single file answers
   whether a screen IR dialect is a week or a quarter.
4. For Unified: the serialiser in §5.B is the long pole, not the generation. Scope it first, not last.
