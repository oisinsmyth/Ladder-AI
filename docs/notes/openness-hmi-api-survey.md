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

## 7. Grounded against a real Unified project (2026-08-07)

The sections above are reflection. This one is measurement. Run against JOB9002's own HMI device under
the read-only extension recorded in `13-data-boundary.md` — **all names below are invented**, per that
entry's output rule; the structure is real.

The device is a **Unified Comfort Panel**, which settles §5's "which architecture is even reachable"
question for this shop the unhelpful way: the classic path in §5.A, the one that maps cleanly onto
this project's existing machinery, **does not apply to this hardware at all.**

Reading it required building the tool first: `openness-cli` was PLC-only by construction — every
device walk filters `Software is PlcSoftware` — so the HMI device was *invisible* to it rather than
unsupported. `openness-cli hmi` (read-only; see `src/openness-cli/README.md`) is that walker, and
because Unified has no screen export it is necessarily a live-object-model reader, i.e. the first cut
of §5.B's serialiser.

**What the device actually contains:**

| | |
|---|---|
| Engineering screens | **48** |
| Screen groups | 5 |
| Screen items, all screens | **1404** (largest single screen: 185; none empty) |
| HMI tags | 233 |
| **Discrete alarms** | **311** |
| Analog alarms / script modules | 0 / 0 |
| Alarm classes | 20 |

> **Correction, and it inverts an earlier conclusion in this note.** The first measured run reported
> **3** screens and this section originally drew "screens are few, alarms are many" from it. That was
> wrong. `HmiSoftware.Screens` is **root-only** — the other 45 screens live inside the 5 screen
> groups, and the first walker never descended into them. A 16× undercount, produced silently: the
> output looked complete and internally consistent, and nothing about it invited suspicion except the
> `screenGroups=5` line sitting next to `screens=3`.
>
> The lesson is the one the PLC side already learned in `WalkBlockGroup`: **a composition hanging off
> the software root is not the whole tree.** Any Openness walker should assume a `.Groups` recursion
> exists until it has checked, and treat a group count next to a suspiciously small item count as the
> tell.

So the real shape is the opposite of the first reading: **48 screens and 1404 screen items** against
311 alarms. This is a substantial HMI, not a three-screen one, and the screen-drawing half is not the
small half. The alarm surface is still large and still the most tractable target (§10), but it does
not dwarf the screens the way one run wrongly suggested.

**311 discrete alarms, enumerable through the API.** That is §6's answer stopping being a claim about
type shapes and becoming a measurement on a real plant.

**A trap that turned out to be the reverse of what I recorded.** The project folder holds ~48
`screens/screen_*.rdf` files. I first read that as the screen count, then — after the walker returned
3 — wrote a confident note saying *do not infer HMI content from the project folder, those are only
compiled runtime artifacts*. **The disk was right and the walker was wrong.** 48 `.rdf` files, 48
engineering screens.

The honest lesson is narrower than either version: the runtime image is a *plausible* cross-check on
the engineering content, not an authority on it (it also contains faceplate instances and system
artifacts, so the correspondence is not guaranteed to be exact). What it is genuinely good for is
**smelling out an under-reporting reader** — a large disagreement between the file count and the API
count means one of them is wrong, and that is worth resolving rather than explaining away. I
explained it away. The `.rdf` files themselves remain an undocumented binary format that should not
be parsed (hard rule 7's reasoning applies).

**Item types observed** — rectangle, text, line, IO field, graphic view, button, screen window, alarm
control. Layout is absolutely positioned (`@x,y w×h`), no layout engine.

**Composition pattern**, which is a real design worth borrowing: one *frame* screen sized to the
panel carries a header strip (rectangles/lines/texts) plus a `HmiScreenWindow`; content screens are
sized to the window, not the panel, and are loaded into it — the window's `Screen` property is itself
dynamized by an Expression. The entire alarm UI is a **single** `HmiAlarmControl` filling one screen.
So 311 alarms cost exactly one screen object, and the screen/alarm asymmetry above is by design.

**Dynamizations observed:** kind `Tag` and kind `Expression`, on properties `ProcessValue`, `Visible`,
`Graphic` and `Screen`. So a Unified screen's PLC coupling really is per-property and really is
readable — the mechanism §4 describes from reflection is the mechanism in use.

### The finding that corrects the knowledge base

`TagDynamization` exposes `Tag` and `PlcTag` **separately, and on this project they differ.** Two
distinct HMI tags bound to a PLC tag of the *same* name on two different PLC stations:

```
HMI tag "UnitA_TripAlarm"   ->  PLC tag "TripAlarm0"
HMI tag "UnitB_TripAlarm0"  ->  PLC tag "TripAlarm0"
```

The HMI tag names are disambiguated by station because one HMI faces two PLCs; the PLC tag names are
not, because each is unambiguous *within its own PLC*. Neither name is derivable from the other.

**This falsifies a stated assumption in `notes/hmi-alarm-generation.md` §5.2**, which recorded that
the live alarm job took trigger tags from PLC member names "under a stated assumption that HMI tags
match", and flagged that assumption as the engineer's to check. On a real project of this shape it is
**wrong**, and wrong in the most dangerous way — silently, and only for the tags where two stations
collide. An alarm generator that assumes name equality would produce rows that import cleanly and
point at the wrong station's fault bit.

The constructive half: the mapping is not guesswork, it is **readable**. `PlcTag` alongside `Tag` is
exactly the join an alarm tool needs, and on Unified it can be extracted rather than assumed.

### Walker gaps, stated rather than hidden

- **Events are not read.** Buttons came back with no dynamizations because a button's behaviour lives
  in `EventHandlers`, which is a different composition (`UI.Events`, 85 types) that this walker does
  not touch. "No dynamizations" on a button therefore means *not looked at*, not *not bound* — the
  one place this tool's output could currently mislead.
- Faceplate *instances* are reported as ordinary items; faceplate *types* are library content and are
  not walked.
- Alarms/tags are counted, not enumerated. Counting proved the surface exists; listing them is the
  obvious next step and was out of scope for a survey.

## 8. "Decode the XML" — there isn't one, and what replaces it

Asked directly (2026-08-07, owner) to decode the HMI screen XML, with screen creation and design as
the eventual goal. **For WinCC Unified there is no screen XML to decode.** That is not a gap in the
search; it is the shape of the product. Checked four independent ways, all agreeing:

1. **The API.** Sweeping every `Import`/`Export` method across the Unified namespaces returns only
   tags, script modules and text lists. `IChromDataExchangeExport` is implemented by exactly one
   type, `HmiScriptModule`. There is no `HmiScreen.Export` and no `HmiScreenComposition.Import`.
2. **The project folder.** Every `.xml` file in a real project is a GSD device description, a
   conversion log, or `DownloadTask.xml` — none is a screen.
3. **The compiled runtime image.** Screens there are `.rdf`, an undocumented Siemens binary
   serialisation. The two large `.zip`s next to them are **not** screens either: they are web-control
   bundles (Angular/JS assets for custom controls). Reading `.rdf` would be reverse-engineering an
   internal format — the HMI equivalent of hand-patching SimaticML, which hard rule 7 exists to
   forbid, and it would bind us to a format Siemens can change without notice.
4. **Independently.** Third-party tooling documents the same limitation in its own release notes:
   WinCC Unified is not supported for export by the Openness API.

**Classic is the opposite** and worth restating here, because it is where the intuition comes from:
classic screens *do* move as SimaticML, and Siemens' own doc-comment on `Screen.Export` says exactly
that. So "the HMI screen XML" is a real thing — on hardware this project's own reference job does not
use.

### What replaces it is better than XML

Openness is **self-describing**, and the metamodel answers questions a sample document cannot:

| API | Question it answers |
|---|---|
| `GetAttributeInfos()` | every attribute of an object: `Name`, `AccessMode`, `SupportedTypes`, and **`CreateRelevance`** |
| `GetCompositionInfos()` | the child collections an object owns (`ScreenItems`, `Dynamizations`, `EventHandlers`) |
| `GetCreationInfos(composition)` | **which types that composition will actually accept**, with their constructor parameters |

`CreateRelevance` is the one that matters most for authoring: it is an enum of
`None | Relevant | Mandatory`, so the API states outright **which attributes must be supplied at
creation time**. No exported example can tell you that — an XML file shows you what one screen
happened to set, never what the next screen is required to set. `GetCreationInfos` is similarly
decisive: it is the API declaring its own contract rather than us inferring a whitelist from samples.

`openness-cli hmi --schema` extracts exactly this: the creatable screen-item types, and for each item
type observed, every attribute with access mode, create-relevance, type and a sample value, sorted
**Mandatory → Relevant → the rest** — authoring order.

### Measured schema (JOB9002, 2026-08-07) — what the metamodel actually says

`openness-cli hmi --schema` run against the real device. Four results, all load-bearing for a builder:

**1. 56 creatable screen-item types.** `GetCreationInfos("ScreenItems")` returns 56 — the widgets,
shapes and controls of §4, plus the abstract-looking bases (`HmiShapeBase`, `HmiControlWindowBase`,
`HmiCentricShapeBase`…). The API declares this itself; no whitelist has to be inferred from samples.

**2. Creation is almost unconstrained — and this is the headline for a builder.** Across every type
inspected, **nothing is `Mandatory` and the only `Relevant` attribute is `Name`.** Everything else —
geometry, colours, `ProcessValue`, alignment — is `None`, i.e. set *after* construction like any
other property. So the builder shape is simply:

```
ScreenItems.Create<HmiIOField>("<name>")   ->  set attributes  ->  Dynamizations.Create<TagDynamization>("<property>")
```

There is no constructor-argument puzzle to solve, which is the single biggest unknown a missing
export format would otherwise have left open.

**3. Bound properties read empty, and that is the tell.** On a real dynamized `HmiIOField`,
`ProcessValue` is a `String` with **no value at all** — because the binding lives in the item's
`Dynamizations` composition, not in the property. A reader that only walked attributes would
conclude the field displays nothing. Value and binding are separate surfaces and both must be read.

**4. Sub-parts are `Read`-only handles, not assignables.** `Font`, `Padding`, `InputBehavior`,
`ToolTipText` report `[Read/None]` and return part objects (`HmiFontPart`, `HmiPaddingPart`, …). You
configure them by reaching *into* the returned object, never by assigning a new one. Each item also
owns four compositions — `Dynamizations`, `EventHandlers`, `PropertyEventHandlers`, `Thresholds`.

A representative type is 24–37 attributes (`HmiScreen` itself is 14), so the whole authoring surface
for a screen is a few hundred well-typed properties — large, but enumerable and self-documenting.

### What this means for screen creation

Reframe the goal. A screen-creation capability here is **not** a converter emitting a document;
there is no document. It is a builder driving the object model — `Screens.Create` →
`ScreenItems.Create<T>` → set attributes → `Dynamizations.Create<TagDynamization>("PropertyName")` —
against a schema the API hands you. Consequences, all of them already visible in §5.B:

- **The IR/converter pattern does not transfer.** There is no SimaticML-shaped middle format to own.
- **The review discipline does not transfer either, yet.** No export means no diff, no `ir-hash`, no
  golden round-trip. A canonical serialiser has to be written before any of this project's existing
  review machinery can apply to a screen — which is why it is the long pole, not the generation.
- **The verification story is better, if `Validate()` holds up.** Per-object, per-property errors and
  warnings, checkable before anything is committed — still untested (§9).

## 9. Unverified — do not treat as proven

- **`Validate()`'s DEPTH is still unproven** — see §10. It has now been invoked and it works, but it
  was asked about a screen with nothing wrong with it, and it said nothing. That is consistent with a
  thorough checker and equally consistent with a shallow one.
- **Nothing has been *modified*.** The write path is proven for *creation* only. Editing an existing
  screen, re-binding a dynamization, or deleting a screen have never been exercised, and creation
  being easy says little about them — creation touches nothing anyone else depends on.
- **No dynamization has been created**, only read. `Dynamizations.Create<TagDynamization>` remains
  reflection-only, and it is the step that would actually bind a screen to the PLC.
- **The compile path is inferred, not observed.** `ICompilable` exists (`Compile() : CompilerResult`)
  and `HmiTarget` implements `IEngineeringServiceProvider`, so `HmiTarget.GetService<ICompilable>()`
  *compiles*. Whether it returns non-null is untested. Note that **`HmiSoftware` (Unified) does not
  implement `IEngineeringServiceProvider` at all** — it has no `GetService<T>()` — so a Unified
  compile must be reached via the device/device-item, not via the software object. Worth confirming
  before anyone designs a gate around it.
- **`Create<T>` constraints are unknown.** The generic `ScreenItems.Create<T>(name)` presumably
  rejects some `T`, and `Create<T>(name, containedTypeValue)` exists for container types. Which
  types are legal where is not derivable from reflection.
- **One project is not a population.** §7 measured a single device. The counts, the
  frame/content-window composition, and the tag-naming mismatch are all facts *about that project*.
  The tag/PlcTag mismatch generalises the least comfortably — it arises because one panel faces two
  PLCs, which is a common but not universal arrangement. Treat §7 as one grounded data point, in the
  same spirit the four-rung spec pipeline is held to "one plant, three runs".
- **The screen-item schema is drawn only from types this project happens to use.** `--schema`
  describes 13 item types because those are the ones on these 48 screens; the device reports **56**
  creatable types. The other 43 have not been inspected, and nothing here should be read as a
  complete catalogue of what a Unified screen can contain.
- **V21 exists** and was not examined. Given classic's four-release freeze, the useful question for
  V21 is only ever about Unified.

## 10. The write path, proven end to end (2026-08-07)

`openness-cli hmi-create-screen` run against JOB9002's scratch copy under the write extension recorded
in `13-data-boundary.md`. **It worked**, and a fresh process read the result back:

```
CREATED screen 'ZZ_AI_TestScreen' on <device>
  size: 1280x615
  items created: 3   (HmiRectangle, HmiText, HmiButton)
  Validate(): ran, returned no errors and no warnings
  project saved: yes
```

Read-back in a separate invocation: **49 screens where there were 48**, the new one carrying its
three items — `HmiRectangle` at 96×48, `HmiText` and `HmiButton` at 160×40, all at `@0,0`.

**What this establishes, and it is the whole §4/§5.B construction story:**

1. **`Screens.Create(name)` works**, and the created screen accepts `Width`/`Height` immediately.
2. **`ScreenItems.Create<T>(name)` really does take only a name.** The schema said nothing is
   `Mandatory` and only `Name` is `Relevant` (§8); creation confirms it. Items arrive with sensible
   per-type defaults (a rectangle 96×48, a text and a button 160×40) rather than 0×0 — so the
   builder does not have to know a type's geometry to produce a valid object.
3. **`Save()` persists a Unified screen**, verified by re-reading in a new process rather than by
   trusting the return value.
4. **`Validate()` is real, callable, and does not throw** — the first time this project has ever
   invoked it.

**What it does NOT establish, and the distinction matters.** `Validate()` returned *no errors and no
warnings* — on a screen that had **nothing wrong with it**. Three default-positioned items on a
correctly-sized screen is the easiest possible case. A clean result there is consistent with a
thorough validator and equally consistent with one that checks almost nothing. **§5.B's "better
verification story" therefore remains a hypothesis, not a measured fact.** The way to settle it is to
create something deliberately invalid — an item bound to a non-existent tag, a screen sized past the
panel — and see whether `Validate()` objects. Until that is run, do not cite `Validate()` as a gate.

Two smaller observations worth keeping:

- **Creation is cheap and non-destructive**, which is exactly why it is a poor guide to the rest of
  the write surface. Modifying an existing screen and deleting one are the operations with real
  consequences, and neither has been touched.
- **The `--yes` gate refusing without contacting Portal** turned out to matter in practice, not just
  in principle: during the wedge (`openness-quirks.md`) every Portal-touching command failed, while
  the dry run kept answering in 0.4 s. A confirmation step that needs a working session is a
  confirmation step that stops working exactly when things are going wrong.

## 11. If this is ever picked up

In rough order of cost, and none of it authorised by this note:

1. ~~Confirm which panel family real jobs actually use.~~ **Answered (§7): Unified.** Which means
   §5.A — the architecture that reuses this project's existing SimaticML machinery — is the one that
   does *not* apply, and §5.B's serialiser is on the critical path rather than being an alternative
   to it.
2. ~~Call `Validate()` once.~~ **Done (§11) — and it answered less than hoped.** Create/Save are
   proven; `Validate()` runs but was only ever shown a valid screen. **The replacement task: make
   something deliberately invalid and see whether it objects** — an item bound to a non-existent
   tag, or a screen sized past the panel. That is now the highest-value single run, because §5.B's
   entire "better verification" argument rests on the answer.
3. **Enumerate the alarms**, not just count them. 311 of them with a readable `Tag`/`PlcTag` join is
   FI-35's use case sitting in reach, and it needs no screen work at all.
4. Read `EventHandlers` — the walker's one genuinely misleading gap (§7).
5. For Unified generation proper: the serialiser is the long pole, not the generation. Scope it
   first, not last.
