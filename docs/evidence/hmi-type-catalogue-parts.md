# HMI type catalogue — `Siemens.Engineering.HmiUnified.UI.Parts`

**What this file is.** A pure *reflection catalogue* of the `Siemens.Engineering.HmiUnified.UI.Parts`
namespace in TIA Portal V20's `Siemens.Engineering.dll`, produced 2026-08-08 by loading the assembly
with `Reflection.Assembly.LoadFrom` and enumerating declared public members. **It is type-shape only
and NOT live-verified** — nothing here has been exercised against a real Portal project, no screen was
opened, and no `openness-cli` command was run to produce it. Reflection tells you what a member *is
declared as*, never whether calling it succeeds, whether the underlying object accepts a write in a
given context, or whether the engineering system permits a create. The write-path survey
(`docs/notes/openness-hmi-write-api.md` §4) has already demonstrated that Unified writability is
*contextual* — a settable property is not a writable one — so treat every "configurable" below as
"declared settable, live behaviour UNKNOWN". Where the assembly cannot answer a question, this file
says UNKNOWN rather than guessing. This raises `UI.Parts` from L1 (names + bases) to L2
(member-detailed) in the coverage table of `openness-hmi-write-api.md` §4g; it does not touch L3.

## 0. Scope, counts, and how they reconcile

| Measure | Value |
|---|---|
| Types in the namespace, all visibilities | 148 |
| **Public types** | **74** |
| Non-public types | 74 — exactly one `<TypeName>FactoryFacade` static class per public type |
| Declared public properties (74 types) | 382 |
| Declared public non-special methods | 307 |
| — of which are `Equals`/`GetHashCode`/`ToString` overrides | 222 (3 per type, on all 74) |
| **Interesting declared methods** (excl. those overrides) | **85** |
| Property accessors + other special methods | 589 |
| Declared members incl. accessors | 1278 |
| Events | 0 |
| Enums | **0 — every enum used by a Part lives in `UI.Enum`, not here** |
| Abstract types | **0 — every `…PartBase` is concrete-but-unsealed, not abstract** |
| Compositions | 16 |

The "**74 types, 689 members**" figure in `openness-hmi-write-api.md` §4g reconciles exactly as
382 properties + 307 non-special methods. Three quarters of the 307 are the boilerplate `Equals` /
`GetHashCode` / `ToString` triple every type carries, so the real behavioural surface is
**382 properties + 85 methods = 467 members**, and 74 of the 382 properties are the identical
`Parent : IEngineeringObject { get; }`. The namespace is much smaller than its member count suggests.

**Universal shape.** Every non-composition part derives (directly or transitively) from
`Siemens.Engineering.HmiUnified.UI.UIBase` and implements the same interface set:

```
HmiUnified.Common.IValidator, IEngineeringObject, IEngineeringCompositionOrObject,
IEngineeringInstance, IEngineeringServiceProvider, IServiceProvider, IEquatable<object>,
HmiUnified.UI.Features.IHmiIdentifierFeature      (+ 3 Private.IInternal* access interfaces)
```

`UIBase` contributes, to every one of them:

```
PROP Dynamizations         : DynamizationBaseComposition   { get; }
PROP PropertyEventHandlers : PropertyEventHandlerComposition { get; }
PROP Parent                : IEngineeringObject            { get; }
METH object GetAttribute(string), IList GetAttributes(...), IList GetAttributeInfos()
METH void   SetAttribute(string, object), SetAttributes(...)
METH IList  Validate()
METH T      GetService<T>()
```

Two consequences worth stating up front. First, **every part is dynamizable** — a trend's `LineColor`,
a grid column's `Visible`, a font's `Size` all carry a `Dynamizations` composition, so tag-driven
behaviour is not a screen-item-level privilege. Second, **every part is reachable by the untyped
`GetAttribute`/`SetAttribute`/`GetAttributeInfos` route**, which is what `openness-cli hmi --schema`
already uses; the typed properties catalogued below are a convenience layer over that, and where a
typed setter is missing the untyped route may or may not still work (UNKNOWN — untested).

**Compositions are different.** All 16 derive from `System.Object`, not `UIBase`. They implement
`IEngineeringComposition` (+ `IEnumerable<T>`), so they have **no** `Dynamizations`, **no** `Validate()`,
and **no** `SetAttribute`. They do inherit `IEngineeringComposition`'s untyped
`GetCreationInfos()` / `Create(Type, IEnumerable<parameter>)` — see §5 trap 2.

**Feature interfaces.** Beyond the universal `IHmiIdentifierFeature` (which declares **zero** members —
a pure marker, as is `IHmiMeasurementUnitFeature`), a few parts carry extra `UI.Features` interfaces.
These are re-declarations of properties the class already declares, so they add no members, but they
are the machine-readable statement of "this thing is a scale / an axis / has a time range":

```
IHmiScaleFeature        : AutoScaling, LabelColor, ScaleMode, TickColor
IHmiAxisFeature         : AxisColor, DisplayName, Name, Visible
IHmiTimeRangeFeature    : BeginTime, EndTime, PointCount, RangeType, TimeRangeBase, TimeRangeFactor
IHmiIdentifierFeature   : (none — marker)
IHmiMeasurementUnitFeature : (none — marker)
```

---

## 1. The ten families

The 74 types partition cleanly into ten families. Each type belongs to exactly one.

| # | Family | Count | What it is FOR |
|---|---|---|---|
| A | **Roots** | 2 | The base class every part inherits (`HmiScreenPartBase`) plus one orphan (`HmiScreenElementBase`). |
| B | **Data-grid view and columns** | 16 | The generic tabular engine — one view part plus a polymorphic column hierarchy — that backs *every* list-shaped Unified control (alarms, process values, system diagnosis, parameters, trend rulers). |
| C | **Trends, areas, axes and their decorations** | 27 | Everything inside a trend control: the plot areas, the curves, the time/value axes, and the per-axis decorations (help lines, scaling entries, thresholds, rulers, legend, quality colouring, data source). |
| D | **Scales** | 3 | The shared scale abstraction and its two geometries — curved (gauge) and straight (bar/slider); value axes in family C derive from it. |
| E | **Control bars** | 11 | The toolbar / status bar strip on window-style controls, and the element types that sit in it (button, toggle, label, text box, separator). |
| F | **Shared visual attributes** | 5 | Reusable styling handles hung off many different owners: font, padding, corner radii, a text block, and a text-plus-graphic "content" layout. |
| G | **Input behaviour** | 1 | How an input field treats operator entry (clear on activate, hidden input, accept on deactivate). |
| H | **Selection items** | 2 | The individual entries of a list box / radio group / check box group. |
| I | **Interface / binding surfaces** | 4 | Name-value property bags used to pass values into a faceplate instance or a custom (web/widget) control. |
| J | **System-diagnosis matrix** | 3 | The tile-matrix presentation of hardware diagnosis, distinct from the grid presentation in family B. |

---

## 2. Per-type dump

Namespace prefix `Siemens.Engineering.HmiUnified.UI.Parts.` is omitted from every entry; any type name
below without a dot is in that namespace. Types with a dot are elsewhere (`HmiUnified:` =
`Siemens.Engineering.HmiUnified.`, `SE:` = `Siemens.Engineering.`). All 74 types are `abstract=no`;
`sealed` is noted per type. `Equals`/`GetHashCode`/`ToString` are omitted throughout (present on all
74). Every non-composition type also declares `Parent : SE:IEngineeringObject { get; }` — listed once
per type for completeness, but it is never informative.

### Family A — Roots (2)

```
HmiScreenPartBase : HmiUnified:UI.UIBase        unsealed   composition=no
  PROP Parent : SE:IEngineeringObject { get; }
  (declares nothing else — the whole namespace's inherited surface comes from UIBase)

HmiScreenElementBase : HmiUnified:UI.UIBase     SEALED     composition=no
  PROP Parent : SE:IEngineeringObject { get; }
  -- nothing derives from it; no property, method return or parameter anywhere in the
     assembly is typed as it. See §3 and §5 trap 1.
```

### Family B — Data-grid view and columns (16)

```
HmiDataGridViewPart : HmiScreenPartBase         unsealed   composition=no
  PROP AllowFilter                   : Boolean                        { get;set; }
  PROP AllowSort                     : Boolean                        { get;set; }
  PROP AlternateBackColor            : Drawing.Color                  { get;set; }
  PROP AlternateForeColor            : Drawing.Color                  { get;set; }
  PROP BackColor                     : Drawing.Color                  { get;set; }
  PROP CellPadding                   : HmiPaddingPart                 { get; }
  PROP ColoringMode                  : UI.Enum.HmiGridColoringMode    { get;set; }
  PROP Columns                       : HmiDataGridColumnPartBaseComposition { get; }
  PROP Font                          : HmiFontPart                    { get; }
  PROP ForeColor                     : Drawing.Color                  { get;set; }
  PROP GridLineColor                 : Drawing.Color                  { get;set; }
  PROP GridLineVisibility            : UI.Enum.HmiSimpleGridLine      { get;set; }
  PROP GridLineWidth                 : Byte                           { get;set; }
  PROP GridSelectionMode             : UI.Enum.HmiGridSelectionMode   { get;set; }
  PROP HeaderSettings                : HmiDataGridHeaderSettingsPart  { get; }
  PROP HorizontalScrollBarVisibility : UI.Enum.HmiScrollBarVisibility { get;set; }
  PROP RowHeight                     : Byte                           { get;set; }
  PROP SelectFullRow                 : Boolean                        { get;set; }
  PROP SelectionBackColor            : Drawing.Color                  { get;set; }
  PROP SelectionBorderColor          : Drawing.Color                  { get;set; }
  PROP SelectionBorderWidth          : Byte                           { get;set; }
  PROP SelectionForeColor            : Drawing.Color                  { get;set; }
  PROP VerticalScrollBarVisibility   : UI.Enum.HmiScrollBarVisibility { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiSystemDiagnosisDetailViewPart : HmiDataGridViewPart   SEALED   composition=no
  PROP HardwareDetails : HmiSystemDiagnosisHardwareDetailPartComposition { get; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiDataGridHeaderSettingsPart : HmiScreenPartBase        SEALED   composition=no
  PROP AllowColumnReorder        : Boolean                      { get;set; }
  PROP AllowColumnResize         : Boolean                      { get;set; }
  PROP ColumnHeaderType          : UI.Enum.HmiDataGridHeaderType{ get;set; }
  PROP Font                      : HmiFontPart                  { get; }
  PROP HeaderBackColor           : Drawing.Color                { get;set; }
  PROP HeaderForeColor           : Drawing.Color                { get;set; }
  PROP HeaderGridLineColor       : Drawing.Color                { get;set; }
  PROP HeaderSelectionBackColor  : Drawing.Color                { get;set; }
  PROP HeaderSelectionForeColor  : Drawing.Color                { get;set; }
  PROP RowHeaderType             : UI.Enum.HmiDataGridHeaderType{ get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiDataGridColumnHeaderPart : HmiScreenPartBase          SEALED   composition=no
  PROP Content : HmiContentPart        { get; }
  PROP Graphic : String                { get;set; }
  PROP Text    : SE:MultilingualText   { get; }
  PROP Parent  : SE:IEngineeringObject { get; }

HmiDataGridColumnPartBase : HmiScreenPartBase            unsealed composition=no
  PROP AllowSort     : Boolean                     { get;set; }
  PROP BackColor     : Drawing.Color               { get;set; }
  PROP Content       : HmiContentPart              { get; }
  PROP Enabled       : Boolean                     { get;set; }
  PROP ForeColor     : Drawing.Color               { get;set; }
  PROP Header        : HmiDataGridColumnHeaderPart { get; }
  PROP MaximumWidth  : UInt32                      { get;set; }
  PROP MinimumWidth  : UInt32                      { get;set; }
  PROP Name          : String                      { get;set; }
  PROP OutputFormat  : String                      { get;set; }
  PROP SortDirection : UI.Enum.HmiSortDirection    { get;set; }
  PROP SortOrder     : UInt16                      { get;set; }
  PROP Visible       : Boolean                     { get;set; }
  PROP Width         : UInt32                      { get;set; }
  PROP Parent        : SE:IEngineeringObject       { get; }
  METH void Delete()

HmiDataGridColumnPartBaseComposition : Object            SEALED   COMPOSITION
  PROP Count      : Int32                     { get; }
  PROP IsReadOnly : Boolean                   { get; }
  PROP Item[Int32 index] : HmiDataGridColumnPartBase { get; }
  PROP Parent     : SE:IEngineeringObject     { get; }
  METH Boolean  Contains(HmiDataGridColumnPartBase item)
  METH HmiDataGridColumnPartBase Find(String name)
  METH Int32    IndexOf(HmiDataGridColumnPartBase item)
  METH IEnumerator<HmiDataGridColumnPartBase> GetEnumerator()
  -- NO Create.  Find(name) only.

HmiDataGridColumnPart : HmiDataGridColumnPartBase        unsealed composition=no
  PROP Key    : String                { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

-- the nine concrete column types; each adds exactly one enum selector naming which
-- system-defined block the column shows.  None is ever a property's declared type
-- (reached only by downcasting HmiDataGridColumnPartBaseComposition.Item).

HmiAlarmColumnPart : HmiDataGridColumnPartBase           SEALED
  PROP AlarmBlock     : UI.Enum.HmiAlarmBlock { get;set; }
  PROP UseAlarmColors : Boolean               { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiAlarmStatisticColumnPart : HmiDataGridColumnPartBase  SEALED
  PROP AlarmStatisticBlock : UI.Enum.HmiAlarmStatisticBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiProcessColumnPart : HmiDataGridColumnPartBase         SEALED
  PROP DataSource : HmiDataSourcePart { get; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiTrendColumnPart : HmiDataGridColumnPartBase           SEALED
  PROP TrendInfoBlock : UI.Enum.HmiTrendInfoBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiTimeRangeColumnPart : HmiDataGridColumnPartBase       SEALED
   (+ IHmiTimeRangeFeature)
  PROP BeginTime       : DateTime                  { get;set; }
  PROP EndTime         : DateTime                  { get;set; }
  PROP PointCount      : Int32                     { get;set; }
  PROP RangeType       : UI.Enum.HmiTimeRangeType  { get;set; }
  PROP TimeRangeBase   : UI.Enum.HmiTimeRangeBase  { get;set; }
  PROP TimeRangeFactor : Int32                     { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiSystemDiagnosisControlColumnPart : HmiDataGridColumnPartBase  SEALED
  PROP SystemDiagnosisControlBlock : UI.Enum.HmiSystemDiagnosisControlBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiSystemDiagnosisMatrixColumnPart : HmiDataGridColumnPartBase   SEALED
  PROP SystemDiagnosisMatrixBlock : UI.Enum.HmiSystemDiagnosisMatrixBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiDetailedParameterControlColumnPart : HmiDataGridColumnPart    SEALED
  PROP DetailedParameterControlBlock : UI.Enum.HmiDetailedParameterControlBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiOverviewParameterControlColumnPart : HmiDataGridColumnPart    SEALED
  PROP OverviewParameterControlBlock : UI.Enum.HmiOverviewParameterControlBlock { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
```

### Family C — Trends, areas, axes and decorations (27)

```
HmiTrendAreaPartBase : HmiScreenPartBase        unsealed  composition=no
  PROP BackColor            : Drawing.Color              { get;set; }
  PROP GridLines            : UI.Enum.HmiGridLine        { get;set; }
  PROP LeftValueAxes        : HmiYValueAxisPartComposition { get; }
  PROP MajorGridLinesColor  : Drawing.Color              { get;set; }
  PROP MinorGridLinesColor  : Drawing.Color              { get;set; }
  PROP Name                 : String                     { get;set; }
  PROP RightValueAxes       : HmiYValueAxisPartComposition { get; }
  PROP Ruler                : HmiRulerPart               { get; }
  PROP SizeFactor           : UInt16                     { get;set; }
  PROP Visible              : Boolean                    { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiTrendAreaPart : HmiTrendAreaPartBase         SEALED
  PROP BottomTimeAxes  : HmiTimeAxisPartComposition { get; }
  PROP StatisticRulers : HmiRulerPart               { get; }   <-- plural name, SINGULAR type
  PROP TopTimeAxes     : HmiTimeAxisPartComposition { get; }
  PROP Trends          : HmiTrendPartComposition    { get; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiTrendAreaPartComposition : Object            SEALED    COMPOSITION
  PROP Count / IsReadOnly / Item[Int32] : HmiTrendAreaPart / Parent
  METH HmiTrendAreaPart Create(String name)
  METH HmiTrendAreaPart Find(String name)
  METH Boolean Contains(HmiTrendAreaPart) / Int32 IndexOf(HmiTrendAreaPart) / GetEnumerator()

HmiFunctionTrendAreaPart : HmiTrendAreaPartBase SEALED
  PROP BottomValueAxes : HmiXValueAxisPartComposition   { get; }
  PROP FunctionTrends  : HmiFunctionTrendPartComposition{ get; }
  PROP TopValueAxes    : HmiXValueAxisPartComposition   { get; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiFunctionTrendAreaPartComposition : Object    SEALED    COMPOSITION
  METH HmiFunctionTrendAreaPart Create(String name)
  METH HmiFunctionTrendAreaPart Find(String name)
  (+ Count / IsReadOnly / Item[Int32] / Parent / Contains / IndexOf / GetEnumerator)

HmiTrendPartBase : HmiScreenPartBase            unsealed  composition=no
  PROP AlternateBackColor        : Drawing.Color            { get;set; }
  PROP BackColor                 : Drawing.Color            { get;set; }
  PROP BackFillPattern           : UI.Enum.HmiFillPattern   { get;set; }
  PROP DashType                  : UI.Enum.HmiDashType      { get;set; }
  PROP DataSourceY               : HmiDataSourcePart        { get; }
  PROP DisplayName               : SE:MultilingualText      { get; }
  PROP LineColor                 : Drawing.Color            { get;set; }
  PROP LineWidth                 : Byte                     { get;set; }
  PROP MarkerColor               : Drawing.Color            { get;set; }
  PROP MarkerDimension           : UInt32                   { get;set; }
  PROP MarkerGraphic             : String                   { get;set; }
  PROP MarkerType                : UI.Enum.HmiMarkerType    { get;set; }
  PROP QualityVisualization      : HmiQualityPart           { get; }
  PROP ShowLoggedDataImmediately : Boolean                  { get;set; }
  PROP Thresholds                : HmiThresholdPartComposition { get; }
  PROP Visible                   : Boolean                  { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiTrendPart : HmiTrendPartBase                 SEALED
  PROP AggregationMode : UI.Enum.HmiAggregationMode { get;set; }
  PROP TrendMode       : UI.Enum.HmiTrendMode       { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiTrendPartComposition : Object                SEALED    COMPOSITION
  METH HmiTrendPart Create()          <-- no name parameter
  (+ Count / IsReadOnly / Item[Int32] / Parent / Contains / IndexOf / GetEnumerator; NO Find)

HmiFunctionTrendPart : HmiTrendPartBase         SEALED
  PROP BeginTime       : DateTime                 { get;set; }
  PROP DataSourceX     : HmiDataSourcePart        { get; }
  PROP EndTime         : DateTime                 { get;set; }
  PROP PointCount      : Int32                    { get;set; }
  PROP RangeType       : UI.Enum.HmiTimeRangeType { get;set; }
  PROP TimeRangeBase   : UI.Enum.HmiTimeRangeBase { get;set; }
  PROP TimeRangeFactor : Int32                    { get;set; }
  PROP TrendMode       : UI.Enum.HmiTrendMode     { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiFunctionTrendPartComposition : Object        SEALED    COMPOSITION
  METH HmiFunctionTrendPart Create()  (NO Find)

HmiTimeAxisPart : HmiScreenPartBase             SEALED
   (+ IHmiAxisFeature, IHmiScaleFeature, IHmiTimeRangeFeature)
  PROP AlwaysShowRecent : Boolean                 { get;set; }
  PROP AutoScaling      : Boolean                 { get;set; }
  PROP AxisColor        : Drawing.Color           { get;set; }
  PROP BeginTime        : DateTime                { get;set; }
  PROP DisplayName      : SE:MultilingualText     { get; }
  PROP EndTime          : DateTime                { get;set; }
  PROP LabelColor       : Drawing.Color           { get;set; }
  PROP LabelFont        : HmiFontPart             { get; }
  PROP Name             : String                  { get;set; }
  PROP OutputFormat     : String                  { get;set; }
  PROP PointCount       : Int32                   { get;set; }
  PROP RangeType        : UI.Enum.HmiTimeRangeType{ get;set; }
  PROP ScaleMode        : UI.Enum.HmiScaleMode    { get;set; }
  PROP TickColor        : Drawing.Color           { get;set; }
  PROP TimeRangeBase    : UI.Enum.HmiTimeRangeBase{ get;set; }
  PROP TimeRangeFactor  : Int32                   { get;set; }
  PROP Visible          : Boolean                 { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()
  -- NOTE: derives from HmiScreenPartBase, NOT from HmiScalePartBase/HmiValueAxisPartBase,
     and therefore re-declares AutoScaling/LabelColor/ScaleMode/TickColor by hand.

HmiTimeAxisPartComposition : Object             SEALED    COMPOSITION
  METH HmiTimeAxisPart Create(String name) / Find(String name)

HmiValueAxisPartBase : HmiScalePartBase         unsealed  composition=no
   (+ IHmiMeasurementUnitFeature, IHmiScaleFeature, IHmiAxisFeature)
  PROP ApplyScalingEntries     : Boolean                        { get;set; }
  PROP AutoRange               : Boolean                        { get;set; }
  PROP AxisColor               : Drawing.Color                  { get;set; }
  PROP DisplayName             : SE:MultilingualText            { get; }
  PROP HelpLines               : HmiHelpLinePartComposition     { get; }
  PROP Name                    : String                         { get;set; }
  PROP ScalingEntries          : HmiScalingEntryPartComposition { get; }
  PROP ShowScalingDisplayNames : Boolean                        { get;set; }
  PROP Visible                 : Boolean                        { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiXValueAxisPart : HmiValueAxisPartBase        SEALED
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()
  -- adds NOTHING; X vs Y is a type-identity distinction only

HmiXValueAxisPartComposition : Object           SEALED    COMPOSITION
  METH HmiXValueAxisPart Create(String name) / Find(String name)

HmiYValueAxisPart : HmiValueAxisPartBase        SEALED
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiYValueAxisPartComposition : Object           SEALED    COMPOSITION
  METH HmiYValueAxisPart Create(String name) / Find(String name)

HmiHelpLinePart : HmiScreenPartBase             SEALED
  PROP Value   : Double  { get;set; }
  PROP Visible : Boolean { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiHelpLinePartComposition : Object             SEALED    COMPOSITION
  METH HmiHelpLinePart Create()   (NO Find — help lines are unnamed)

HmiScalingEntryPart : HmiScreenPartBase         SEALED
  PROP BeginValue       : Double              { get;set; }
  PROP BeginValueTarget : Double              { get;set; }
  PROP DisplayName      : SE:MultilingualText { get; }
  PROP EndValue         : Double              { get;set; }
  PROP EndValueTarget   : Double              { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiScalingEntryPartComposition : Object         SEALED    COMPOSITION
  METH HmiScalingEntryPart Create()   (NO Find)

HmiThresholdPart : HmiScreenPartBase            SEALED
  PROP Color         : Drawing.Color             { get;set; }
  PROP DisplayName   : SE:MultilingualText       { get; }
  PROP Name          : String                    { get;set; }
  PROP ThresholdMode : UI.Enum.HmiThresholdMode  { get;set; }
  PROP Value         : Double                    { get; }     <-- READ-ONLY. See §5 trap 4.
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiThresholdPartComposition : Object            SEALED    COMPOSITION
  METH HmiThresholdPart Find(String name)
  -- NO Create, although HmiThresholdPart HAS Delete(). See §5 trap 3.

HmiRulerPart : HmiScreenPartBase                SEALED
  PROP Color : Drawing.Color { get;set; }
  PROP Width : UInt32        { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiLegendPart : HmiScreenPartBase               SEALED
  PROP Font     : HmiFontPart    { get; }
  PROP ForeColor: Drawing.Color  { get;set; }
  PROP Visible  : Boolean        { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiQualityPart : HmiScreenPartBase              SEALED
  PROP BadColor       : Drawing.Color { get;set; }
  PROP UncertainColor : Drawing.Color { get;set; }
  PROP Visible        : Boolean       { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiDataSourcePart : HmiScreenPartBase           SEALED
  PROP Source           : String  { get;set; }   <-- the tag binding, as a bare STRING
  PROP VisualizeQuality : Boolean { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
```

### Family D — Scales (3)

```
HmiScalePartBase : HmiScreenPartBase            unsealed  composition=no
   (+ IHmiMeasurementUnitFeature, IHmiScaleFeature)
  PROP AutoScaling      : Boolean                 { get;set; }
  PROP BeginValue       : Double                  { get;set; }
  PROP DivisionCount    : Int32                   { get;set; }
  PROP EndValue         : Double                  { get;set; }
  PROP LabelColor       : Drawing.Color           { get;set; }
  PROP LabelFont        : HmiFontPart             { get; }
  PROP OutputFormat     : String                  { get;set; }
  PROP ScaleMode        : UI.Enum.HmiScaleMode    { get;set; }
  PROP ScalingType      : UI.Enum.HmiScalingType  { get;set; }
  PROP SubDivisionCount : Int32                   { get;set; }
  PROP TickColor        : Drawing.Color           { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiCurvedScalePart : HmiScalePartBase           SEALED
  PROP AngleRange : Int32 { get;set; }
  PROP StartAngle : Int32 { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiStraightScalePart : HmiScalePartBase         SEALED
  PROP Orientation : UI.Enum.HmiOrientation { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
```

### Family E — Control bars (11)

```
HmiControlBarPartBase : HmiScreenPartBase       unsealed  composition=no
  PROP BackColor    : Drawing.Color                        { get;set; }
  PROP Elements     : HmiControlBarElementPartBaseComposition { get; }
  PROP Enabled      : Boolean                              { get;set; }
  PROP Font         : HmiFontPart                          { get; }
  PROP Padding      : HmiPaddingPart                       { get; }
  PROP ShowToolTips : Boolean                              { get;set; }
  PROP Visible      : Boolean                              { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiToolBarPart : HmiControlBarPartBase          SEALED
  PROP UseHotKeys : Boolean { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiStatusBarPart : HmiControlBarPartBase        SEALED
  PROP Parent : SE:IEngineeringObject { get; }
  -- adds nothing

HmiControlBarElementPartBaseComposition : Object SEALED   COMPOSITION
  PROP Count / IsReadOnly / Item[Int32] : HmiControlBarElementPartBase / Parent
  METH Boolean Contains(...) / Int32 IndexOf(...) / GetEnumerator()
  -- NO Create AND NO Find.  The most closed composition in the namespace. §5 trap 3.

HmiControlBarElementPartBase : HmiScreenPartBase unsealed composition=no
  PROP Authorization : String              { get;set; }
  PROP CustomID      : Int32               { get;set; }
  PROP Enabled       : Boolean             { get;set; }
  PROP ForeColor     : Drawing.Color       { get;set; }
  PROP Height        : UInt32              { get;set; }
  PROP MaximumHeight : UInt32              { get;set; }
  PROP MaximumWidth  : UInt32              { get;set; }
  PROP MinimumHeight : UInt32              { get;set; }
  PROP MinimumWidth  : UInt32              { get;set; }
  PROP Padding       : HmiPaddingPart      { get; }
  PROP ToolTipText   : SE:MultilingualText { get; }
  PROP Visible       : Boolean             { get;set; }
  PROP Width         : UInt32              { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  -- no Delete()

HmiControlBarDisplayPart : HmiControlBarElementPartBase   unsealed
  PROP Content : HmiContentPart      { get; }
  PROP Graphic : String              { get;set; }
  PROP Text    : SE:MultilingualText { get; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiControlBarButtonPart : HmiControlBarDisplayPart        unsealed
  PROP AlternateBackColor   : Drawing.Color { get;set; }
  PROP AlternateBorderColor : Drawing.Color { get;set; }
  PROP BackColor            : Drawing.Color { get;set; }
  PROP BorderColor          : Drawing.Color { get;set; }
  PROP BorderWidth          : Byte          { get;set; }
  PROP HotKey               : UInt16        { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiControlBarToggleSwitchPart : HmiControlBarButtonPart   SEALED
  PROP AlternateGraphic : String              { get;set; }
  PROP AlternateText    : SE:MultilingualText { get; }
  PROP HotKey           : UInt16              { get;set; }   <-- re-declared, shadows base
  PROP IsAlternateState : Boolean             { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiControlBarLabelPart : HmiControlBarElementPartBase     unsealed
  PROP HorizontalTextAlignment : UI.Enum.HmiHorizontalAlignment { get;set; }
  PROP Text                    : SE:MultilingualText           { get; }
  PROP VerticalTextAlignment   : UI.Enum.HmiVerticalAlignment  { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiControlBarTextBoxPart : HmiControlBarLabelPart         SEALED
  PROP AlternateBorderColor : Drawing.Color { get;set; }
  PROP BackColor            : Drawing.Color { get;set; }
  PROP BorderColor          : Drawing.Color { get;set; }
  PROP BorderWidth          : Byte          { get;set; }
  PROP ReadOnly             : Boolean       { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiControlBarSeparatorPart : HmiControlBarElementPartBase SEALED
  PROP Parent : SE:IEngineeringObject { get; }
  -- adds nothing
```

### Family F — Shared visual attributes (5)

```
HmiFontPart : HmiScreenPartBase                 SEALED
  PROP Italic    : Boolean                  { get;set; }
  PROP Name      : UI.Enum.HmiFontName      { get;set; }   <-- an ENUM, not a string
  PROP Size      : Single                   { get;set; }
  PROP StrikeOut : Boolean                  { get;set; }
  PROP Underline : Boolean                  { get;set; }
  PROP Weight    : UI.Enum.HmiFontWeight    { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  -- NOTE: UI.Enum also declares HmiFontStrikeOut, unused by this type (StrikeOut is Boolean).

HmiPaddingPart : HmiScreenPartBase              SEALED
  PROP Bottom : Int32 { get;set; }
  PROP Left   : Int32 { get;set; }
  PROP Right  : Int32 { get;set; }
  PROP Top    : Int32 { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiCornersPart : HmiScreenPartBase              SEALED
  PROP BottomLeftRadius  : UInt32 { get;set; }
  PROP BottomRightRadius : UInt32 { get;set; }
  PROP TopLeftRadius     : UInt32 { get;set; }
  PROP TopRightRadius    : UInt32 { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiTextPart : HmiScreenPartBase                 SEALED
  PROP Font      : HmiFontPart        { get; }
  PROP ForeColor : Drawing.Color      { get;set; }
  PROP Text      : SE:MultilingualText{ get; }
  PROP Visible   : Boolean            { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiContentPart : HmiScreenPartBase              SEALED
  PROP ContentMode           : UI.Enum.HmiContentMode         { get;set; }
  PROP GraphicStretchMode    : UI.Enum.HmiGraphicStretchMode  { get;set; }
  PROP HorizontalTextAlignment : UI.Enum.HmiHorizontalAlignment{ get;set; }
  PROP Spacing               : UInt32                         { get;set; }
  PROP SplitRatio            : Double                         { get;set; }
  PROP TextPosition          : UI.Enum.HmiTextPosition        { get;set; }
  PROP TextTrimming          : UI.Enum.HmiTextTrimming        { get;set; }
  PROP VerticalTextAlignment : UI.Enum.HmiVerticalAlignment   { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  -- the text-vs-graphic layout policy of a button/column/cell; holds no text itself
```

### Family G — Input behaviour (1)

```
HmiInputBehaviorPart : HmiScreenPartBase        SEALED
  PROP AcceptOnDeactivated : Boolean { get;set; }
  PROP ClearOnActivate     : Boolean { get;set; }
  PROP HiddenInput         : Boolean { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
```

### Family H — Selection items (2)

```
HmiSelectionItemPart : HmiScreenPartBase        SEALED
  PROP Graphic    : String              { get;set; }
  PROP IsSelected : Boolean             { get;set; }
  PROP Text       : SE:MultilingualText { get; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiSelectionItemPartComposition : Object        SEALED    COMPOSITION
  METH HmiSelectionItemPart Create()   (NO Find — selection items are unnamed)
  (+ Count / IsReadOnly / Item[Int32] / Parent / Contains / IndexOf / GetEnumerator)
```

### Family I — Interface / binding surfaces (4)

```
HmiFaceplateInterface : HmiScreenPartBase       SEALED
  PROP PropertyName : String { get; }        <-- read-only key
  PROP Value        : Object { get;set; }    <-- weakly typed
  PROP Parent : SE:IEngineeringObject { get; }
  -- no Delete()

HmiFaceplateInterfaceComposition : Object       SEALED    COMPOSITION
  METH HmiFaceplateInterface Find(String propertyName)
  -- NO Create, NO CanCreate.  The faceplate's parameter set is fixed by its type.

HmiCustomControlInterface : HmiScreenPartBase   SEALED
  PROP Properties   : HmiCustomControlInterfaceComposition { get; }  <-- NESTED, recursive
  PROP PropertyName : String { get; }
  PROP Value        : Object { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiCustomControlInterfaceComposition : Object   SEALED    COMPOSITION
  METH Boolean CanCreate()                  <-- the ONLY CanCreate/CanDelete pair in the namespace
  METH Boolean CanDelete()
  METH HmiCustomControlInterface Create()
  METH HmiCustomControlInterface Find(String propertyName)
  (+ Count / IsReadOnly / Item[Int32] / Parent / Contains / IndexOf / GetEnumerator)
```

### Family J — System-diagnosis matrix (3)

```
HmiMatrixViewPart : HmiScreenPartBase           SEALED
  PROP HardwareDetails : HmiSystemDiagnosisHardwareDetailPartComposition { get; }
  PROP SystemDiagnosisHardwareDetailView : HmiSystemDiagnosisDetailViewPart { get; }
  PROP TileBorderWidth : Byte   { get;set; }
  PROP TileHeightMax   : UInt16 { get;set; }
  PROP TileHeightMin   : UInt16 { get;set; }
  PROP TileWidthMax    : UInt16 { get;set; }
  PROP TileWidthMin    : UInt16 { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }

HmiSystemDiagnosisHardwareDetailPart : HmiScreenPartBase  SEALED
  PROP SystemDiagnosisMatrixBlock : UI.Enum.HmiSystemDiagnosisMatrixBlock { get;set; }
  PROP Visible : Boolean { get;set; }
  PROP Parent : SE:IEngineeringObject { get; }
  METH void Delete()

HmiSystemDiagnosisHardwareDetailPartComposition : Object  SEALED  COMPOSITION
  METH HmiSystemDiagnosisHardwareDetailPart Create()   (NO Find)
```

### Composition summary (all 16, side by side)

```
Composition                                       Create              Find              Can*
-------------------------------------------------------------------------------------------
HmiControlBarElementPartBaseComposition           --                  --                --
HmiDataGridColumnPartBaseComposition              --                  Find(name)        --
HmiFaceplateInterfaceComposition                  --                  Find(propName)    --
HmiThresholdPartComposition                       --                  Find(name)        --
HmiCustomControlInterfaceComposition              Create()            Find(propName)    CanCreate/CanDelete
HmiFunctionTrendPartComposition                   Create()            --                --
HmiHelpLinePartComposition                        Create()            --                --
HmiScalingEntryPartComposition                    Create()            --                --
HmiSelectionItemPartComposition                   Create()            --                --
HmiSystemDiagnosisHardwareDetailPartComposition   Create()            --                --
HmiTrendPartComposition                           Create()            --                --
HmiFunctionTrendAreaPartComposition               Create(name)        Find(name)        --
HmiTimeAxisPartComposition                        Create(name)        Find(name)        --
HmiTrendAreaPartComposition                       Create(name)        Find(name)        --
HmiXValueAxisPartComposition                      Create(name)        Find(name)        --
HmiYValueAxisPartComposition                      Create(name)        Find(name)        --
```

All 16 additionally expose `Count`, `IsReadOnly`, `Item[Int32]`, `Parent`, `Contains`, `IndexOf`,
`GetEnumerator`. **Named creation and lookup travel together**: every composition with `Create(name)`
also has `Find(name)`, and every composition whose items are unnamed has neither.

### Types declaring `Delete()` (14 of 58 non-composition types)

```
HmiCustomControlInterface     HmiDataGridColumnPartBase   HmiFunctionTrendAreaPart
HmiFunctionTrendPart          HmiHelpLinePart             HmiScalingEntryPart
HmiSelectionItemPart          HmiSystemDiagnosisHardwareDetailPart
HmiThresholdPart              HmiTimeAxisPart             HmiTrendAreaPart
HmiTrendPart                  HmiXValueAxisPart           HmiYValueAxisPart
```

The other 44 non-composition parts have no `Delete()` — they are permanent sub-objects of their owner,
not list members.

---

## 3. Reachability — which parts can a screen item actually get to?

Method: every public property in the whole assembly whose declared type is one of the 74 was collected
(150 such properties). **43 of them are declared outside `UI.Parts`; the other 107 are parts pointing
at parts.** No method anywhere in the assembly returns or accepts a Part type — the entire namespace is
navigated by property, never by call.

**The 19 direct entry points.** These are the only Part types a screen item hands you directly:

| Part type reached | From (owner.property) |
|---|---|
| `HmiFontPart` | `Widgets.HmiButton.Font`, `Widgets.HmiTextWidgetBase.Font`, `Widgets.HmiScaleWidgetBase.Font`, `Widgets.HmiSelectionGroupBase.Font`, `Widgets.HmiClock.DialLabelFont`, `Shapes.HmiText.Font`, `Controls.HmiDetailedParameterControl.Font` |
| `HmiPaddingPart` | `Widgets.HmiButton.Padding`, `Widgets.HmiTextWidgetBase.Padding`, `Widgets.HmiSelectionGroupBase.Padding`, `Shapes.HmiGraphicView.Padding` |
| `HmiContentPart` | `Widgets.HmiButton.Content`, `Widgets.HmiSelectionGroupBase.Content`, `Widgets.HmiSymbolicIOField.Content` |
| `HmiTextPart` | `Base.HmiWindowBase.Caption` (also on `Features.IHmiWindowFeature.Caption`), `Widgets.HmiScaleWidgetBase.Label`, `Widgets.HmiScaleWidgetBase.Title`, `Widgets.HmiClock.Title` |
| `HmiCornersPart` | `Shapes.HmiRectangle.Corners` |
| `HmiInputBehaviorPart` | `Widgets.HmiIOField.InputBehavior` |
| `HmiCurvedScalePart` | `Widgets.HmiGauge.CurvedScale` |
| `HmiStraightScalePart` | `Widgets.HmiBar.StraightScale` |
| `HmiThresholdPartComposition` | `Widgets.HmiScaleWidgetBase.Thresholds`, `Widgets.HmiIOField.Thresholds` |
| `HmiSelectionItemPartComposition` | `Widgets.HmiSelectionGroupBase.SelectionItems` |
| `HmiToolBarPart` | `Base.HmiControlWindowBase.ToolBar` |
| `HmiStatusBarPart` | `Base.HmiControlWindowBase.StatusBar` |
| `HmiLegendPart` | `Base.HmiTrendControlBase.Legend` |
| `HmiDataGridViewPart` | `Controls.HmiAlarmControl.AlarmView`, `.AlarmStatisticsView`, `Controls.HmiProcessControl.ProcessView`, `Controls.HmiSystemDiagnosisControl.SystemDiagnosisView`, `Controls.HmiDetailedParameterControl.ParameterView`, `Controls.HmiTrendCompanion.TrendRulerView`, `.TrendStatisticAreaView`, `.TrendStatisticResultView` |
| `HmiMatrixViewPart` | `Controls.HmiSystemDiagnosisControl.MatrixView` |
| `HmiTrendAreaPartComposition` | `Controls.HmiTrendControl.TrendAreas` |
| `HmiFunctionTrendAreaPartComposition` | `Controls.HmiFunctionTrendControl.FunctionTrendAreas` |
| `HmiFaceplateInterfaceComposition` | `Controls.HmiFaceplateContainer.Interface` |
| `HmiCustomControlInterfaceComposition` | `Base.HmiCustomWebControlContainer.Interface`, `Base.HmiCustomWidgetContainer.Interface` |

Because `HmiScaleWidgetBase`, `HmiTextWidgetBase`, `HmiSelectionGroupBase`, `HmiWindowBase`,
`HmiControlWindowBase` and `HmiTrendControlBase` are base classes, those entry points are inherited by
their concrete descendants: `HmiGauge`/`HmiBar`/`HmiSlider`, `HmiLabel`/`HmiTextBox`/`HmiIOField`/
`HmiSymbolicIOField`, `HmiListBox`/`HmiCheckBoxGroup`/`HmiRadioButtonGroup`, `HmiScreenWindow` and
every `UI.Controls` window control, `HmiTrendControl`/`HmiFunctionTrendControl`.

**Two structural facts about the entry points.** `HmiScreen`, `HmiScreenBase`, `HmiScreenItemBase` and
`HmiSimpleScreenItemBase` expose **no** Part at all — parts hang off *specific* widgets, shapes and
controls, never off the screen or the generic item base. And every one of the 43 owner properties is
`{ get; }` — see §4.

**Everything else is reached by hopping through another part**, up to four hops deep. The deepest live
chain is:

```
HmiTrendControl.TrendAreas → HmiTrendAreaPart → .Trends → HmiTrendPart
   → .Thresholds → HmiThresholdPart
HmiTrendControl.TrendAreas → HmiTrendAreaPart (base) → .LeftValueAxes → HmiYValueAxisPart
   → .ScalingEntries → HmiScalingEntryPart          (or → .HelpLines → HmiHelpLinePart)
```

**23 types are never any property's declared type.** They split three ways:

*(a) Seven reached only as base classes of something reachable* — legitimate, not dead:
`HmiScreenPartBase` (the root), `HmiControlBarPartBase` (→ `ToolBar`/`StatusBar`), `HmiScalePartBase`
(→ curved/straight/value axis), `HmiTrendAreaPartBase` (→ trend/function-trend areas),
`HmiTrendPartBase` (→ trend/function-trend), `HmiValueAxisPartBase` (→ X/Y), `HmiDataGridColumnPart`
(→ the two parameter-control columns).

*(b) Fifteen concrete types reachable only by downcasting a composition's `Item`* — a real usability
cliff. All nine concrete column types (`HmiAlarmColumnPart`, `HmiAlarmStatisticColumnPart`,
`HmiProcessColumnPart`, `HmiTrendColumnPart`, `HmiTimeRangeColumnPart`,
`HmiSystemDiagnosisControlColumnPart`, `HmiSystemDiagnosisMatrixColumnPart`,
`HmiDetailedParameterControlColumnPart`, `HmiOverviewParameterControlColumnPart`) arrive typed as
`HmiDataGridColumnPartBase` and must be cast before their one distinguishing enum is visible. The same
applies to all six control-bar element types (`HmiControlBarDisplayPart`, `HmiControlBarButtonPart`,
`HmiControlBarToggleSwitchPart`, `HmiControlBarLabelPart`, `HmiControlBarTextBoxPart`,
`HmiControlBarSeparatorPart`) behind `HmiControlBarElementPartBase`. Any tool walking a grid or a
toolbar must type-switch, and a type it does not know about degrades silently to the base view.

*(c) One genuinely dead type: `HmiScreenElementBase`.* It is public and sealed, derives from `UIBase`,
declares only `Parent`, and **nothing in the entire assembly derives from it, returns it, or accepts
it**. It cannot be obtained through any typed API path. Best reading is a vestigial or
reserved-for-future type; whether it is obtainable through the untyped `IEngineeringObject` route is
UNKNOWN and would need a live probe.

---

## 4. Read-only handles vs configurable objects

**The general rule, and it holds without a single exception in this namespace:**

> A property whose type is a Part is **always** `{ get; }`. You never assign a part; you navigate into
> it and set its scalars.

All 43 external owner properties and all 107 part-to-part properties are get-only. There is no setter
anywhere that takes a Part, no method that returns one, and no method that accepts one. So the pattern
already recorded for `Font` / `Padding` / `InputBehavior` is not those four types being special — it is
**the** access model for the whole namespace. Configuration is always
`widget.Font.Size = 14f`, never `widget.Font = someFont`; there is no way to share or clone a part
between owners through the typed API.

**Inside a part, the scalars are settable, with four classes of exception:**

1. `Parent : IEngineeringObject { get; }` — on all 74 types. Structural, never interesting.
2. **Every `MultilingualText` property is get-only** — `Text`, `AlternateText`, `ToolTipText`,
   `DisplayName`. Same handle pattern one level down: you reach into the `MultilingualText`'s per-culture
   items rather than assigning a string. Affected: `HmiTextPart.Text`, `HmiControlBarLabelPart.Text`,
   `HmiControlBarDisplayPart.Text`, `HmiControlBarToggleSwitchPart.AlternateText`,
   `HmiControlBarElementPartBase.ToolTipText`, `HmiDataGridColumnHeaderPart.Text`,
   `HmiSelectionItemPart.Text`, `HmiScalingEntryPart.DisplayName`, `HmiThresholdPart.DisplayName`,
   `HmiTimeAxisPart.DisplayName`, `HmiValueAxisPartBase.DisplayName`, `HmiTrendPartBase.DisplayName`.
3. **Identity keys of composition members are get-only where the item is created by a keyed `Create`**:
   `HmiFaceplateInterface.PropertyName`, `HmiCustomControlInterface.PropertyName`. Note the contrast —
   `HmiDataGridColumnPartBase.Name`, `HmiTrendAreaPartBase.Name`, `HmiTimeAxisPart.Name`,
   `HmiValueAxisPartBase.Name` and `HmiThresholdPart.Name` **are** settable, so renaming an item out
   from under the `Find(name)` that located it is possible; whether the composition's index follows the
   rename is UNKNOWN.
4. **One genuine anomaly: `HmiThresholdPart.Value : Double { get; }`** — read-only, while its siblings
   `Color`, `Name` and `ThresholdMode` are all settable, and while the structurally analogous
   `HmiHelpLinePart.Value : Double { get;set; }` and `HmiScalingEntryPart.BeginValue/EndValue` are
   settable. See §5 trap 4.

Composition members `Count`, `IsReadOnly` and `Item[int]` are get-only throughout, as expected.

**What reflection cannot tell you, and you must not infer.** There is no `[Read]`, `[ReadWrite]` or
access-mode custom attribute on any of these members — property-level `GetCustomAttributesData()`
returns empty for every sample checked, and the only type-level attributes are compiler artefacts
(`SecuritySafeCritical`, `DebuggerNonUserCode`, `DefaultMember`). The `Read` / `ReadWrite` access mode
and the `Mandatory` / `Relevant` / `None` create-relevance that `openness-cli hmi --schema` prints come
from the **runtime** `GetAttributeInfos()` / `GetCreationInfos()` calls, not from metadata. So this
catalogue can tell you a C# setter exists; it cannot tell you the engineering system will honour it.
Given §4 of the write-API survey found writability to be contextual, **a `{ get;set; }` here is a
hypothesis, not a permission.**

---

## 5. Traps and surprises

**1. `HmiScreenElementBase` is dead weight.** Public, sealed, one inherited-shadowing `Parent` property,
zero inbound references, zero derived types. It is the only truly unreachable type in the namespace and
should be ignored by any code generator that enumerates `UI.Parts`.

**2. "No typed `Create`" does not prove "cannot create".** Four compositions
(`ControlBarElement`, `DataGridColumn`, `FaceplateInterface`, `Threshold`) offer no typed `Create`, but
all 16 implement `IEngineeringComposition`, which carries the untyped
`GetCreationInfos()` and `Create(Type, IEnumerable<parameter>)`. Whether the underlying engineering
object permits creation through that route is **UNKNOWN from reflection** — `GetCreationInfos()` is a
runtime question and is exactly what `hmi --schema` exists to answer. Do not write "columns cannot be
added" into any capability statement without a live probe; write "no typed Create; untyped route
untested". The one composition that states its answer in metadata is
`HmiCustomControlInterfaceComposition`, the only type here with `CanCreate()`/`CanDelete()`.

**3. Create/Delete asymmetry — you can destroy what you cannot rebuild.**
`HmiDataGridColumnPartBase` and `HmiThresholdPart` both declare `Delete()`, but
`HmiDataGridColumnPartBaseComposition` and `HmiThresholdPartComposition` have **no typed `Create`**.
A tool that deletes a grid column or a threshold has, as far as the typed API goes, no way to put it
back. Conversely `HmiControlBarElementPartBase` has neither `Delete()` nor a creating composition, and
its composition has no `Find` either — toolbars and status bars are effectively **read-and-tune-only**
through the typed surface, addressable solely by integer index (`CustomID` is a settable `Int32`, not a
lookup key any `Find` uses).

**4. `HmiThresholdPart.Value` is read-only.** A threshold you cannot set the value of is close to
useless, so the likely readings are (a) the value comes from `ThresholdMode` plus the axis scaling,
(b) it is settable only through `SetAttribute`, or (c) it is an API defect. **Which one is UNKNOWN** —
this is the single highest-value item to test live in this namespace, because it decides whether trend
and gauge threshold authoring is possible at all.

**5. Tag binding is a bare string with no type.** `HmiDataSourcePart.Source : String { get;set; }` is
how a trend curve, a function-trend X axis and a process-grid column all get their data. There is no
tag object, no validation at the property, and therefore no compile-time protection against a
misspelled or nonexistent tag — the same nonexistent-tag question already open for dynamizations
(`openness-hmi-write-api.md` §3, §5.2). Hard rule 3 discipline (never invent a tag) applies to any HMI
authoring path just as it does to PLC tags.

**6. `HmiTimeAxisPart` is not an axis, structurally.** It derives from `HmiScreenPartBase`, not from
`HmiScalePartBase`/`HmiValueAxisPartBase` like every other axis, and hand-copies `AutoScaling`,
`LabelColor`, `ScaleMode`, `TickColor`, `LabelFont`, `OutputFormat`, `Name`, `DisplayName`, `Visible`.
Any code that walks axes by base type will silently skip time axes. The `IHmiAxisFeature` /
`IHmiScaleFeature` interfaces are the only reliable way to treat all axes uniformly — which is
presumably why the feature interfaces exist.

**7. `HmiXValueAxisPart` and `HmiYValueAxisPart` declare nothing beyond `Delete()`.** They are pure
type-identity markers over an identical `HmiValueAxisPartBase`. Orientation is expressed by *which
composition you found it in* (`LeftValueAxes`/`RightValueAxes` are Y; `TopValueAxes`/`BottomValueAxes`
are X), not by any property. Likewise `HmiStatusBarPart` and `HmiControlBarSeparatorPart` add nothing to
their bases.

**8. Naming traps.** `HmiTrendAreaPart.StatisticRulers` is **plural-named but singular-typed**
(`HmiRulerPart`, not a composition) and sits alongside the inherited singular `Ruler` — two distinct
ruler objects on one area. `HmiFontPart.Name` is an **enum** (`HmiFontName`), not a font-family string,
so fonts are picked from a closed list. `HmiFontPart.StrikeOut` is a `Boolean` even though `UI.Enum`
declares an unused `HmiFontStrikeOut` enum. `HmiCustomControlInterface.Properties` makes the interface
bag **recursive** — a custom-control property can itself contain properties, so any walker over it needs
depth handling; `HmiFaceplateInterface` has no such nesting.

**9. `HmiControlBarToggleSwitchPart.HotKey` shadows `HmiControlBarButtonPart.HotKey`.** Both declare
`HotKey : UInt16 { get;set; }`. Reflection with `DeclaredOnly` shows two; a non-declared-only walk shows
one and may bind to either. Serialisers should read by declaring type, not by name.

**10. Every part carries `Dynamizations`.** Inherited from `UIBase`, so a font size, a grid column's
`Visible`, a threshold's colour and a trend's `LineColor` are all individually tag-bindable. This is far
more surface than "screen items can be dynamized" suggests, and it means a faithful reader of a Unified
screen must walk dynamizations at every level of the part tree, not just at item level. `hmi --screen`
currently reports per-property dynamizations for screen items; whether it descends into parts is a
question for the CLI, not for this file.

**11. The 74 hidden `…FactoryFacade` classes.** Exactly one non-public static facade exists per public
type (74 public + 74 facades = the 148 types in the namespace). Their existence is consistent with
creation being brokered centrally by the engineering framework rather than by public constructors —
none of the 74 public types exposes a usable public constructor path in this catalogue, which is why
`Create` on a composition (or the untyped `IEngineeringObject.Create`) is the only way any of these
objects come into being.

**12. Zero enums and zero events here.** Every enum a Part uses lives in `UI.Enum` (97 enums), and every
event handler lives in `UI.Events`. A generator targeting `UI.Parts` alone will not resolve a single
enum value; the namespaces must be catalogued together.

---

## 6. What would raise this to L3

In priority order, the live probes that would convert the UNKNOWNs above into facts:

1. `HmiThresholdPart.Value` — is it settable via `SetAttribute`? (trap 4)
2. `GetCreationInfos()` on `HmiDataGridColumnPartBaseComposition` and
   `HmiControlBarElementPartBaseComposition` — can columns and toolbar elements be created at all?
   (trap 2/3)
3. `GetAttributeInfos()` on one part of each family — do the declared setters report `ReadWrite`, and
   what create-relevance do they carry? This is the direct check on §4's caveat.
4. Whether `HmiScreenElementBase` is obtainable through any untyped path. (trap 1)
5. Whether `Dynamizations` on a nested part (e.g. a trend's `LineColor`) actually accepts a
   dynamization. (trap 10)

None of these were run for this file — it is reflection only, dated 2026-08-08.
