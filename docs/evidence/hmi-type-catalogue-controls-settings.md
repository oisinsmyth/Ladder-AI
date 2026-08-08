# HMI Unified type catalogue — Controls, Feature interfaces, RuntimeSettings, UI enums

**Reflection catalogue, type-shape only, NOT live-verified, dated 2026-08-08.** Everything below was
obtained by loading `C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll`
with `Assembly.LoadFrom` and reading `Type`/`PropertyInfo`/`MethodInfo`/`Enum` metadata — no TIA Portal
process was started, no project was opened, and nothing here has been exercised against a running
Openness session. It therefore records **what the API declares**, not what it accepts: a property being
`[get set]` in metadata says nothing about whether `SetAttribute` succeeds, whether the attribute is
create-relevant, or whether `Validate()` will pass. `openness-cli hmi --schema` (which reads
`GetAttributeInfos`/`GetCreationInfos` live) remains the authority on access mode and create-relevance;
this document is the offline map that tells you *what to go and ask `--schema` about*. Companion to
`docs/notes/openness-hmi-api-survey.md`. Where reflection cannot answer a question, this document says
**UNKNOWN** rather than guessing.

Namespace census for `Siemens.Engineering.HmiUnified*` (35 namespaces), for orientation:

```
Siemens.Engineering.HmiUnified                                        2
Siemens.Engineering.HmiUnified.Common                                 9
Siemens.Engineering.HmiUnified.Common.Internal                        2
Siemens.Engineering.HmiUnified.Cpm                                   38
Siemens.Engineering.HmiUnified.HmiAlarm                               12
Siemens.Engineering.HmiUnified.HmiAlarm.HmiAlarmCommon                13
Siemens.Engineering.HmiUnified.HmiAudit                                7
Siemens.Engineering.HmiUnified.HmiConnections                         10
Siemens.Engineering.HmiUnified.HmiLogging                             12
Siemens.Engineering.HmiUnified.HmiLogging.HmiLoggingCommon            13
Siemens.Engineering.HmiUnified.HmiOpcUaAlarm                           4
Siemens.Engineering.HmiUnified.HmiTags                                36
Siemens.Engineering.HmiUnified.Library                                 2
Siemens.Engineering.HmiUnified.LoggingTags                             9
Siemens.Engineering.HmiUnified.RuntimeSettings                        22
Siemens.Engineering.HmiUnified.RuntimeSettings.HmiRuntimeSettingsCommon 7
Siemens.Engineering.HmiUnified.Scripts                                 4
Siemens.Engineering.HmiUnified.TextGraphicList                         8
Siemens.Engineering.HmiUnified.UI                                      1
Siemens.Engineering.HmiUnified.UI.Base                                22
Siemens.Engineering.HmiUnified.UI.Controls                            20
Siemens.Engineering.HmiUnified.UI.Dynamization                        12
Siemens.Engineering.HmiUnified.UI.Dynamization.Flashing                4
Siemens.Engineering.HmiUnified.UI.Dynamization.Script                  6
Siemens.Engineering.HmiUnified.UI.Dynamization.Tag                    17
Siemens.Engineering.HmiUnified.UI.Dynamization.Tag.Internal            2
Siemens.Engineering.HmiUnified.UI.Enum                                97
Siemens.Engineering.HmiUnified.UI.Events                             169
Siemens.Engineering.HmiUnified.UI.Features                            15
Siemens.Engineering.HmiUnified.UI.Parts                              148
Siemens.Engineering.HmiUnified.UI.ScreenGroup                          4
Siemens.Engineering.HmiUnified.UI.Screens                              6
Siemens.Engineering.HmiUnified.UI.Shapes                              40
Siemens.Engineering.HmiUnified.UI.Widgets                             36
```

---

## 1. `Siemens.Engineering.HmiUnified.UI.Controls` — the 10 controls

The namespace holds 20 types: 10 concrete controls and 10 matching `*FactoryFacade` abstract classes.
The facades declare **no public instance members at all** — they are the Openness extension-method
carrier pattern, not something a caller touches. The 10 controls are the real content.

### 1.1 Declared members (reflection dump)

```
=== Siemens.Engineering.HmiUnified.UI.Controls.HmiAlarmControl | base=HmiControlWindowBase | class
  interfaces: IValidator, IEngineeringObject, IEngineeringCompositionOrObject, IEngineeringInstance,
              IInternalObjectAccess, IInternalInstanceAccess, IInternalBaseAccess,
              IEngineeringServiceProvider, IServiceProvider, IEquatable`1,
              IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiBoxFeature, IHmiWindowFeature
  P AcknowledgmentFlashingRate : HmiFlashingRate [get set]
  P ActiveAlarmsViewSetup     : HmiVisibleAlarms [get set]
  P AlarmDefinitionViewSetup  : HmiVisibleAlarms [get set]
  P AlarmSourceType           : HmiAlarmSourceType [get set]
  P AlarmStatisticsView       : HmiDataGridViewPart [get]
  P AlarmView                 : HmiDataGridViewPart [get]
  P AlwaysShowRecent          : Boolean [get set]
  P DefaultSortDirection      : HmiSortDirection [get set]
  P EventHandlers             : HmiAlarmControlEventHandlerComposition [get]
  P Filter                    : String [get set]
  P Parent                    : IEngineeringObject [get]
  P ResetFlashingRate         : HmiFlashingRate [get set]
  P SuppressFlashing          : Boolean [get set]
  P TimeZone                  : Int32 [get set]
  P UseAlarmColors            : Boolean [get set]
  M Equals(Object obj) : Boolean ; GetHashCode() : Int32 ; ToString() : String

=== HmiTrendControl | base=HmiTrendControlBase | class
  interfaces: (as HmiAlarmControl)
  P EventHandlers        : HmiTrendControlEventHandlerComposition [get]
  P Parent               : IEngineeringObject [get]
  P ShowStatisticRulers  : Boolean [get set]
  P TimeZone             : Int32 [get set]
  P TrendAreas           : HmiTrendAreaPartComposition [get]

=== HmiFunctionTrendControl | base=HmiTrendControlBase | class
  interfaces: (as HmiAlarmControl)
  P EventHandlers       : HmiFunctionTrendControlEventHandlerComposition [get]
  P FunctionTrendAreas  : HmiFunctionTrendAreaPartComposition [get]
  P Parent              : IEngineeringObject [get]

=== HmiProcessControl | base=HmiControlWindowBase | class
  interfaces: (as HmiAlarmControl)
  P EditMode                 : HmiEditMode [get set]
  P EventHandlers            : HmiProcessControlEventHandlerComposition [get]
  P Online                   : Boolean [get set]
  P Parent                   : IEngineeringObject [get]
  P ProcessView              : HmiDataGridViewPart [get]
  P TimeStepSmoothingBase    : HmiTimeRangeBase [get set]
  P TimeStepSmoothingFactor  : Int32 [get set]
  P TimeZone                 : Int32 [get set]

=== HmiSystemDiagnosisControl | base=HmiControlWindowBase | class
  interfaces: (as HmiAlarmControl)
  P EventHandlers            : HmiSystemDiagnosisControlEventHandlerComposition [get]
  P MatrixView               : HmiMatrixViewPart [get]
  P Parent                   : IEngineeringObject [get]
  P ShowStatusPath           : Boolean [get set]
  P SystemDiagnosisView      : HmiDataGridViewPart [get]
  P SystemDiagnosisViewType  : HmiSystemDiagnosisViewType [get set]
  P TimeZone                 : Int32 [get set]

=== HmiMediaControl | base=HmiControlWindowBase | class
  interfaces: (as HmiAlarmControl)
  P AutoPlay      : Boolean [get set]
  P EventHandlers : HmiMediaControlEventHandlerComposition [get]
  P Parent        : IEngineeringObject [get]
  P Url           : String [get set]
  P VideoOutput   : HmiVideoOutput [get set]

=== HmiWebControl | base=HmiControlWindowBase | class
  interfaces: (as HmiAlarmControl)
  P EventHandlers : HmiWebControlEventHandlerComposition [get]
  P Parent        : IEngineeringObject [get]
  P Url           : String [get set]

=== HmiFaceplateContainer | base=HmiContainerBase | class
  interfaces: (as HmiAlarmControl) PLUS IHmiRotationFeature
  P Adaption                 : HmiScreenWindowAdaption [get set]
  P EventHandlers            : HmiFaceplateContainerEventHandlerComposition [get]
  P Interface                : HmiFaceplateInterfaceComposition [get]
  P Parent                   : IEngineeringObject [get]
  P RotationAngle            : Int16 [get set]
  P RotationCenterPlacement  : HmiRotationCenterPlacement [get set]
  P RotationCenterX          : Single [get set]
  P RotationCenterY          : Single [get set]

=== HmiDetailedParameterControl | base=HmiControlWindowBase | class
  interfaces: (as HmiAlarmControl)
  P EditMode               : HmiEditMode [get set]
  P EventHandlers          : HmiDetailedParameterControlEventHandlerComposition [get]
  P Font                   : HmiFontPart [get]
  P ForeColor              : Color [get set]
  P HideDetails            : Boolean [get set]
  P ParameterSetTypeFixed  : String [get set]
  P ParameterView          : HmiDataGridViewPart [get]
  P Parent                 : IEngineeringObject [get]
  P SelectionBackColor     : Color [get set]
  P SelectionForeColor     : Color [get set]
  P TimeZone               : Int32 [get set]

=== HmiTrendCompanion | base=HmiCompanionBase | class
  interfaces: (as HmiAlarmControl)
  P EventHandlers                 : HmiTrendCompanionEventHandlerComposition [get]
  P Parent                        : IEngineeringObject [get]
  P ShowAlways                    : Boolean [get set]
  P SourceTrendControl            : String [get set]
  P TimeZone                      : Int32 [get set]
  P TrendCompanionMode            : HmiTrendCompanionMode [get set]
  P TrendRulerView                : HmiDataGridViewPart [get]
  P TrendStatisticAreaView        : HmiDataGridViewPart [get]
  P TrendStatisticResultView      : HmiDataGridViewPart [get]
  P UseSourceControlBackColor     : Boolean [get set]
  P UseSourceControlTrendColors   : Boolean [get set]

=== *FactoryFacade (10 of them: HmiAlarmControlFactoryFacade, HmiDetailedParameterControlFactoryFacade,
    HmiFaceplateContainerFactoryFacade, HmiFunctionTrendControlFactoryFacade, HmiMediaControlFactoryFacade,
    HmiProcessControlFactoryFacade, HmiSystemDiagnosisControlFactoryFacade, HmiTrendCompanionFactoryFacade,
    HmiTrendControlFactoryFacade, HmiWebControlFactoryFacade) | base=Object | abstract
    NO public instance members.
```

### 1.2 The inherited half — where most control properties actually live

Every control's own declared property list is short because the bulk is inherited. The chain is
`UIBase → HmiScreenItemBase → HmiWindowBase → HmiControlWindowBase → (HmiTrendControlBase | HmiContainerBase | HmiCompanionBase)`.
Reading a control without this dump gives a badly wrong impression of how configurable it is:

```
=== HmiScreenItemBase | base=UIBase
  P CurrentQuality : HmiQuality [get]      <-- read-only, runtime quality
  P Enabled : Boolean [get set]
  P Name : String [get set]
  P ShowFocusVisual : Boolean [get set]
  P StyleItemClass : String [get set]
  P TabIndex : UInt16 [get set]
  P Visible : Boolean [get set]
  M Delete() : Void

=== HmiWindowBase | base=HmiScreenItemBase
  P Caption : HmiTextPart [get]   P CaptionColor : Color [get set]   P Icon : String [get set]
  P Left : Int32 [get set]  P Top : Int32 [get set]
  P Width : UInt32 [get set]  P Height : UInt32 [get set]
  P WindowFlags : HmiWindowFlag [get set]

=== HmiControlWindowBase | base=HmiWindowBase
  P BackColor : Color [get set]
  P StatusBar : HmiStatusBarPart [get]
  P ToolBar   : HmiToolBarPart [get]

=== HmiTrendControlBase | base=HmiControlWindowBase
  P AreaSpacing : UInt16 [get set]   P ExtendRulerToAxis : Boolean [get set]
  P Legend : HmiLegendPart [get]     P Online : Boolean [get set]
  P ShiftAxes : Boolean [get set]    P ShowRuler : Boolean [get set]

=== HmiContainerBase | base=HmiWindowBase
  P ContainedType : String [get set]

=== HmiCompanionBase | base=HmiControlWindowBase
  P SnapToSourceControl : Boolean [get set]

=== HmiSimpleScreenItemBase | base=HmiScreenItemBase   (shapes/widgets branch — NOT controls)
  P Authorization : String [get set]   P Opacity : Single [get set]
  P RequireExplicitUnlock : Boolean [get set]   P ToolTipText : MultilingualText [get]

=== HmiScreenItemBaseComposition   (the creation entry point)
  P Count : Int32 [get]   P IsReadOnly : Boolean [get]   P Item : HmiScreenItemBase [get]
  M Create<T>(String name) : T
  M Create<T>(String name, String containedTypeValue) : T     <-- second overload feeds ContainedType
  M Find(String name) : HmiScreenItemBase   M Contains / IndexOf / GetEnumerator
```

`Create<T>` carries **no generic constraint** (reflection reports an empty constraint list), so the type
argument is unconstrained at compile time and any type-checking is a runtime concern.
`HmiScreen.ScreenItems` is the `HmiScreenItemBaseComposition` the controls are created into.

### 1.3 Child compositions each control owns

```
=== HmiDataGridViewPart | base=HmiScreenPartBase      (the shared "grid" child — 8 of the 10 use it)
  P AllowFilter / AllowSort : Boolean [get set]
  P Columns : HmiDataGridColumnPartBaseComposition [get]      <-- THE column list
  P HeaderSettings : HmiDataGridHeaderSettingsPart [get]   P CellPadding : HmiPaddingPart [get]
  P Font : HmiFontPart [get]
  P BackColor / AlternateBackColor / ForeColor / AlternateForeColor : Color [get set]
  P ColoringMode : HmiGridColoringMode [get set]   P GridSelectionMode : HmiGridSelectionMode [get set]
  P GridLineColor : Color   P GridLineVisibility : HmiSimpleGridLine   P GridLineWidth : Byte
  P RowHeight : Byte   P SelectFullRow : Boolean
  P SelectionBackColor / SelectionBorderColor / SelectionForeColor : Color   P SelectionBorderWidth : Byte
  P HorizontalScrollBarVisibility / VerticalScrollBarVisibility : HmiScrollBarVisibility

=== HmiAlarmColumnPart | base=HmiDataGridColumnPartBase
  P AlarmBlock : HmiAlarmBlock [get set]      <-- which of the 50 alarm blocks this column shows
  P UseAlarmColors : Boolean [get set]
=== HmiDataGridColumnPart | base=HmiDataGridColumnPartBase
  P Key : String [get set]

=== HmiTrendAreaPart | base=HmiTrendAreaPartBase
  P BottomTimeAxes : HmiTimeAxisPartComposition [get]   P TopTimeAxes : HmiTimeAxisPartComposition [get]
  P StatisticRulers : HmiRulerPart [get]                P Trends : HmiTrendPartComposition [get]
=== HmiTrendPart | base=HmiTrendPartBase
  P AggregationMode : HmiAggregationMode [get set]   P TrendMode : HmiTrendMode [get set]

=== HmiFunctionTrendAreaPart | base=HmiTrendAreaPartBase
  P BottomValueAxes / TopValueAxes : HmiXValueAxisPartComposition [get]
  P FunctionTrends : HmiFunctionTrendPartComposition [get]
=== HmiFunctionTrendPart | base=HmiTrendPartBase
  P DataSourceX : HmiDataSourcePart [get]   P TrendMode : HmiTrendMode [get set]
  P BeginTime / EndTime : DateTime [get set]   P PointCount : Int32 [get set]
  P RangeType : HmiTimeRangeType   P TimeRangeBase : HmiTimeRangeBase   P TimeRangeFactor : Int32

=== HmiMatrixViewPart | base=HmiScreenPartBase
  P HardwareDetails : HmiSystemDiagnosisHardwareDetailPartComposition [get]
  P SystemDiagnosisHardwareDetailView : HmiSystemDiagnosisDetailViewPart [get]
  P TileBorderWidth : Byte   P TileHeightMax/Min : UInt16   P TileWidthMax/Min : UInt16

=== HmiFaceplateInterface | base=HmiScreenPartBase
  P PropertyName : String [get]      <-- read-only: the name comes from the faceplate type
  P Value : Object [get set]         <-- the settable half

=== HmiLegendPart : Font [get], ForeColor [get set], Visible [get set]
=== HmiToolBarPart : UseHotKeys [get set]        === HmiStatusBarPart : (no own settable members)
```

### 1.4 What each control is for, and its defining properties

**`HmiAlarmControl`** — the alarm screen. Its `AlarmSourceType` (`ActiveAlarms` / `LoggedAlarms` /
`LoggedAlarmsUpdated` / `AlarmDefinition` / `AlarmStatistics`) is what decides whether the same control
object is a live alarm banner, an alarm history, or an alarm-statistics table; `NotConfigured=0` is the
default, so an un-set control is not a live alarm list. Defining properties: `AlarmSourceType`,
`Filter` (a string — the filter grammar is **UNKNOWN** from reflection, it is not an enum or a
structured type), `ActiveAlarmsViewSetup` / `AlarmDefinitionViewSetup` (both the `[Flags]`
`HmiVisibleAlarms`, gating whether suppressed/shelved/disabled alarms appear), and
`DefaultSortDirection`. Compositions owned: **two** grids — `AlarmView` and `AlarmStatisticsView`, each
an `HmiDataGridViewPart` with its own `Columns` composition of `HmiAlarmColumnPart`, each column
selecting one of the 50 `HmiAlarmBlock` values. Plus the inherited `ToolBar` / `StatusBar` /
`Caption`. This is the high-leverage claim in the task and reflection supports it: an alarm screen is
one `HmiAlarmControl` plus N `HmiAlarmColumnPart` children, and column configuration is a loop over an
enum, not per-column bespoke code.

**`HmiTrendControl`** — time-based trending. Only four own properties; the substance is in
`TrendAreas` (`HmiTrendAreaPartComposition`), and each area owns `TopTimeAxes` / `BottomTimeAxes`
(`HmiTimeAxisPartComposition`), `Trends` (`HmiTrendPartComposition`) and a `StatisticRulers` ruler.
The chart-wide switches (`Online`, `ShowRuler`, `ShiftAxes`, `ExtendRulerToAxis`, `AreaSpacing`,
`Legend`) live on `HmiTrendControlBase`. Defining properties: `TrendAreas`, `Online`, `ShowRuler`,
`ShowStatisticRulers`, `Legend`. A three-pen trend is one control → one area → three `HmiTrendPart`.

**`HmiFunctionTrendControl`** — X/Y trending (value against value, not against time). Same base as
`HmiTrendControl`, and the difference is entirely in the child type: `FunctionTrendAreas` owns
`TopValueAxes`/`BottomValueAxes` (`HmiXValueAxisPartComposition`) instead of time axes, and each
`HmiFunctionTrendPart` carries its own `DataSourceX` plus a complete time-range block
(`RangeType`/`TimeRangeBase`/`TimeRangeFactor`/`BeginTime`/`EndTime`/`PointCount`) which the plain
`HmiTrendPart` does not have. Defining properties: `FunctionTrendAreas`, and per-trend `DataSourceX`,
`TrendMode`, `RangeType`.

**`HmiProcessControl`** — a tabular process-value / parameter view. Defining properties: `EditMode`
(a `[Flags]` of `Update`/`Create`/`Delete` — this is the write-permission gate on the grid),
`Online`, `TimeStepSmoothingBase` + `TimeStepSmoothingFactor` (a `HmiTimeRangeBase` unit and an
integer multiplier), `TimeZone`. Owns one grid: `ProcessView`, whose columns are
`HmiProcessColumnPart` (the type exists in `UI.Parts` and carries the `IHmiIdentifierFeature` marker).

**`HmiSystemDiagnosisControl`** — PLC/hardware diagnostics. It is genuinely two controls in one:
`SystemDiagnosisViewType` switches between `Diagnosis=0` (a grid, `SystemDiagnosisView`, columns from
the 6-value `HmiSystemDiagnosisControlBlock`) and `Matrix=1` (a tile view, `MatrixView`, an
`HmiMatrixViewPart` with tile sizing and its own `HardwareDetails` composition and detail view, columns
from the 25-value `HmiSystemDiagnosisMatrixBlock`). Defining properties: `SystemDiagnosisViewType`,
`SystemDiagnosisView`, `MatrixView`, `ShowStatusPath`.

**`HmiMediaControl`** — video/media playback. Three own settables: `Url` (string), `AutoPlay`,
`VideoOutput` (`Undefined`/`Stretch`/`PreserveAspectFit`/`PreserveAspectCrop`). Owns no child
composition beyond the inherited toolbar/status bar. The only control with playback-state events
(`Paused`/`Playing`/`Stopped`).

**`HmiWebControl`** — an embedded browser. The thinnest control in the set: one own settable, `Url`.
No child composition. Note it is *not* `HmiCustomWebControlContainer` (that one lives in `UI.Base`,
derives from `HmiContainerBase`, and owns an `Interface` composition of `HmiCustomControlInterface` —
that is the custom-web-control extension point, a different thing).

**`HmiFaceplateContainer`** — the instance placement of a reusable faceplate; the reuse mechanism, and
so the one most relevant to generated-screen work. Defining properties: `ContainedType` (inherited from
`HmiContainerBase` — the faceplate type name, and the reason `Create<T>` has a
`Create<T>(name, containedTypeValue)` overload), `Interface` (`HmiFaceplateInterfaceComposition` — the
per-instance parameter bindings, each an `HmiFaceplateInterface` with a read-only `PropertyName` and a
settable `Value : Object`), `Adaption` (`None`/`WindowToScreen`/`ScreenToWindow`), and the rotation
block. **It is the only one of the 10 that implements `IHmiRotationFeature`.** The `Interface`
composition is the child composition that matters: a faceplate instance is a container plus a list of
name/value bindings, structurally the same shape as an FB call with its parameter list.

**`HmiDetailedParameterControl`** — parameter-set (recipe) detail editing. Defining properties:
`ParameterSetTypeFixed` (string — pins the control to one parameter-set type), `EditMode` (same
`[Flags]` gate as `HmiProcessControl`), `HideDetails`, and the selection colours. Owns one grid,
`ParameterView`, columns from `HmiDetailedParameterControlColumnPart` and the 4-value
`HmiDetailedParameterControlBlock`. Note the sibling enum `HmiOverviewParameterControlBlock` and type
`HmiOverviewParameterControlColumnPart` exist for an *overview* parameter control that is **not present
in `UI.Controls`** — where the overview control lives, or whether it is exposed at all, is UNKNOWN.

**`HmiTrendCompanion`** — not a standalone control: a satellite pinned to a trend control, showing that
trend's ruler values, statistic areas or statistic results. Defining properties: `SourceTrendControl`
(a **string** naming the trend control it follows — a by-name reference, not an object reference, so
nothing in the type system stops it dangling), `TrendCompanionMode`
(`Ruler`/`StatisticArea`/`StatisticResult`), `SnapToSourceControl` (inherited from `HmiCompanionBase`),
and the two `UseSourceControl*` colour-inheritance flags. Owns **three** grids —
`TrendRulerView`, `TrendStatisticAreaView`, `TrendStatisticResultView` — one per mode; the mode selects
which is shown. Columns come from `HmiTrendColumnPart` / the 27-value `HmiTrendInfoBlock`.

Two cross-cutting observations. First, **no control implements `IHmiOperabilityFeature`** — that
interface (and therefore `Authorization` / `RequireExplicitUnlock`) is declared on
`HmiSimpleScreenItemBase`, the shapes-and-widgets branch. So per-item operator authorization is
available on a button but not on an alarm control, at least not through this path; whether
authorization on a control is configured some other way is UNKNOWN. `HmiCustomWebControlContainer` is
the exception that proves it — it re-declares `Authorization` and `RequireExplicitUnlock` itself
rather than inheriting them. Second, **eight of the ten expose at least one `HmiDataGridViewPart`**
(alarm ×2, process, diagnosis, parameter, companion ×3), so a single generic "configure a grid and its
columns" routine covers most control configuration; only `HmiMediaControl`, `HmiWebControl` and
`HmiFaceplateContainer` fall outside it, and the two trend controls substitute area/trend compositions
for the grid.

---

## 2. `Siemens.Engineering.HmiUnified.UI.Features` — the 15 `IHmi*Feature` interfaces

### 2.1 Full dump, with implementor counts

```
IHmiArcFeature                (4)  AngleRange:Int32[gs]  StartAngle:Int32[gs]
      -> HmiCircleSegment, HmiCircularArc, HmiEllipseSegment, HmiEllipticalArc

IHmiAreaFeature               (6)  AlternateBackColor:Color[gs] AlternateBorderColor:Color[gs]
      BackColor:Color[gs] BackFillPattern:HmiFillPattern[gs] BorderColor:Color[gs] BorderWidth:Byte[gs]
      DashType:HmiDashType[gs] FillDirection:HmiFillDirection[gs] FillLevel:Byte[gs] ShowFillLevel:Boolean[gs]
      -> HmiCircle, HmiCircleSegment, HmiEllipse, HmiEllipseSegment, HmiPolygon, HmiRectangle

IHmiAxisFeature               (4)  AxisColor:Color[gs] DisplayName:MultilingualText[g]
                                   Name:String[gs] Visible:Boolean[gs]
      -> HmiTimeAxisPart, HmiValueAxisPartBase, HmiXValueAxisPart, HmiYValueAxisPart

IHmiBasicScreenFeature        (1)  AlternateBackColor BackColor BackFillPattern BackGraphic:String
      BackGraphicStretchMode:HmiGraphicStretchMode BackgroundFillMode:HmiBackgroundFillMode
      Enabled:Boolean Height:UInt32 Width:UInt32
      HorizontalAlignment:HmiHorizontalAlignment VerticalAlignment:HmiVerticalAlignment   (all [gs])
      -> HmiScreen

IHmiBasicScreenItemFeature   (56)  Enabled:Boolean[gs] Name:String[gs] StyleItemClass:String[gs]
                                   TabIndex:UInt16[gs] Visible:Boolean[gs]
      -> every screen item incl. all 10 controls (full list in §2.2)

IHmiBoxFeature               (44)  Height:UInt32[gs] Left:Int32[gs] Top:Int32[gs] Width:UInt32[gs]
      -> all controls, windows, widgets, and the non-centric shapes

IHmiIdentifierFeature       (114)  *** ZERO MEMBERS — pure marker interface ***
      -> every screen item, every Part, every shape, every widget

IHmiLineFeature               (4)  AlternateLineColor:Color[gs] CapType:HmiCapType[gs]
      DashType:HmiDashType[gs] EndType:HmiLineEndType[gs] LineColor:Color[gs] LineWidth:Byte[gs]
      StartType:HmiLineEndType[gs]
      -> HmiCircularArc, HmiEllipticalArc, HmiLine, HmiPolyline

IHmiMeasurementUnitFeature    (7)  *** ZERO MEMBERS — pure marker interface ***
      -> HmiScalePartBase, HmiCurvedScalePart, HmiStraightScalePart,
         HmiValueAxisPartBase, HmiXValueAxisPart, HmiYValueAxisPart, HmiIOField

IHmiOperabilityFeature       (39)  Authorization:String[gs] RequireExplicitUnlock:Boolean[gs]
      -> HmiCustomWebControlContainer, HmiCustomWidgetContainer, HmiSimpleScreenItemBase
         and its whole subtree (all shapes + all widgets).  NOT the 10 controls.

IHmiRotationFeature          (37)  RotationAngle:Int16[gs] RotationCenterPlacement:HmiRotationCenterPlacement[gs]
                                   RotationCenterX:Single[gs] RotationCenterY:Single[gs]
      -> all shapes, all widgets, HmiCustomWidgetContainer, and HmiFaceplateContainer
         (the ONLY one of the 10 controls)

IHmiScaleFeature              (7)  AutoScaling:Boolean[gs] LabelColor:Color[gs]
                                   ScaleMode:HmiScaleMode[gs] TickColor:Color[gs]
      -> HmiScalePartBase, HmiCurvedScalePart, HmiStraightScalePart, HmiTimeAxisPart,
         HmiValueAxisPartBase, HmiXValueAxisPart, HmiYValueAxisPart

IHmiScreenWindowFeature       (1)  Adaption:HmiScreenWindowAdaption[gs] CurrentZoomFactor:Double[gs]
      HorizontalScrollBarPosition:Int32[gs] HorizontalScrollBarVisibility:HmiScrollBarVisibility[gs]
      InteractiveZooming:Boolean[gs] Screen:String[gs] ScreenName:String[g] ScreenNumber:UInt16[g]
      System:Byte[gs] VerticalScrollBarPosition:Int32[gs] VerticalScrollBarVisibility:HmiScrollBarVisibility[gs]
      -> HmiScreenWindow

IHmiTimeRangeFeature          (2)  BeginTime:DateTime[gs] EndTime:DateTime[gs] PointCount:Int32[gs]
      RangeType:HmiTimeRangeType[gs] TimeRangeBase:HmiTimeRangeBase[gs] TimeRangeFactor:Int32[gs]
      -> HmiTimeAxisPart, HmiTimeRangeColumnPart

IHmiWindowFeature            (17)  Caption:HmiTextPart[g] CaptionColor:Color[gs] Icon:String[gs]
                                   WindowFlags:HmiWindowFlag[gs]
      -> all 10 controls, HmiWindowBase and its bases, HmiScreenWindow, HmiCustomWebControlContainer
```

None of the 15 inherits from another (`GetInterfaces()` on each returns empty); they are a flat set.

### 2.2 The 56 `IHmiBasicScreenItemFeature` implementors

```
HmiScreenItemBase, HmiWindowBase, HmiControlWindowBase, HmiCompanionBase, HmiContainerBase,
HmiCustomWebControlContainer, HmiSimpleScreenItemBase, HmiCustomWidgetContainer, HmiTrendControlBase,
HmiAlarmControl, HmiDetailedParameterControl, HmiFaceplateContainer, HmiFunctionTrendControl,
HmiMediaControl, HmiProcessControl, HmiSystemDiagnosisControl, HmiTrendCompanion, HmiTrendControl,
HmiWebControl, HmiScreenWindow, HmiShapeBase, HmiCentricShapeBase, HmiCircularShapeBase, HmiCircle,
HmiCircleSegment, HmiCircularArc, HmiEllipticalShapeBase, HmiEllipse, HmiEllipseSegment,
HmiEllipticalArc, HmiSurfaceShapeBase, HmiGraphicView, HmiLine, HmiPointBasedShapeBase, HmiPolygon,
HmiPolyline, HmiRectangle, HmiText, HmiWidgetBase, HmiScaleWidgetBase, HmiBar, HmiButton,
HmiSelectionGroupBase, HmiCheckBoxGroup, HmiClock, HmiGauge, HmiTextWidgetBase, HmiIOField, HmiLabel,
HmiListBox, HmiRadioButtonGroup, HmiSlider, HmiSymbolicIOField, HmiTextBox, HmiToggleSwitch, HmiTouchArea
```

The 12 that have `IHmiBasicScreenItemFeature` but **not** `IHmiBoxFeature` are exactly the abstract
roots plus the centric shapes: `HmiScreenItemBase`, `HmiSimpleScreenItemBase`, `HmiShapeBase`,
`HmiCentricShapeBase`, `HmiCircularShapeBase`, `HmiCircle`, `HmiCircleSegment`, `HmiCircularArc`,
`HmiEllipticalShapeBase`, `HmiEllipse`, `HmiEllipseSegment`, `HmiEllipticalArc`. They are positioned
by centre and radius instead:

```
HmiCentricShapeBase    : CenterX:Int32, CenterY:Int32
HmiCircularShapeBase   : Radius:UInt32
HmiEllipticalShapeBase : RadiusX:UInt32, RadiusY:UInt32
HmiShapeBase           : RotationAngle, RotationCenterPlacement, RotationCenterX, RotationCenterY
```

### 2.3 Is this a usable capability-detection mechanism? — honest assessment

**Partly, and the partly matters.** The pattern is real: these are interfaces that group a coherent
property set and are implemented across otherwise unrelated types, so `if (item is IHmiRotationFeature r)`
is a legitimate way to ask "can this be rotated?" without a type switch, and it gives a *correct*
answer — `HmiFaceplateContainer` implements it and `HmiAlarmControl` does not, which is exactly the
distinction a builder would otherwise have to hardcode. The same holds for `IHmiBoxFeature`: the
centric-shape exclusion above is a genuine, non-obvious API fact that the interface expresses for free.
A generic layout pass written against `IHmiBoxFeature` and `IHmiBasicScreenItemFeature` would work
across 44 and 56 types respectively with no per-type knowledge.

But three limitations keep it from being a general capability mechanism:

1. **It is a fixed, incomplete partition, not an extensible capability model.** Fifteen interfaces do
   not cover the property space. There is **no font/text feature** — `HmiText`, `HmiLabel`, `HmiIOField`,
   `HmiDetailedParameterControl` and `HmiDataGridViewPart` all expose a `Font : HmiFontPart`, and
   nothing groups them. There is no colour feature (`BackColor` appears on `IHmiAreaFeature`,
   `IHmiBasicScreenFeature`, and separately on `HmiControlWindowBase` and `HmiDataGridViewPart`, with no
   common interface). There is no "owns a grid" feature despite eight controls doing so, no "owns an
   `Interface` composition" feature despite three types doing so, and no feature for `EditMode`,
   `TimeZone` or `EventHandlers` even though those recur across most controls. So a builder can ask
   "rotatable?" and "box-positioned?" but not "has a font?" or "has columns?" — the questions it most
   needs for a data-driven screen builder.

2. **Two of the fifteen carry zero members.** `IHmiIdentifierFeature` (114 implementors) and
   `IHmiMeasurementUnitFeature` (7) declare no properties and no methods at all — verified via
   `GetMembers().Count == 0`. They are marker interfaces. `IHmiIdentifierFeature` is on essentially
   everything in the UI tree, so as a capability test it is vacuous; whatever it identifies is carried
   through the `IEngineeringObject` attribute layer, not through the interface. Their purpose is
   **UNKNOWN** from reflection.

3. **It says nothing about the thing that actually matters — dynamization.** Binding a property to a
   tag is the entire point of HMI generation, and it happens through the
   `Siemens.Engineering.HmiUnified.UI.Dynamization*` namespaces (39 types) operating on
   `IEngineeringObject` attribute names, not through any feature interface. No feature answers "can
   this property be bound to a tag?". The `--schema` path (`GetAttributeInfos` / `GetCreationInfos`) is
   the only mechanism that does, and it is per-attribute and live-only.

**Verdict:** worth using as a *shortcut* for the four or five questions it happens to answer — geometry,
rotation, window chrome, identity/visibility, operator authorization — and it is strictly better than
hardcoding those. It is not a substitute for the live `GetAttributeInfos` schema, because the schema
answers per-attribute access and create-relevance for *every* property, which the interfaces answer for
about a third of them and never with create-relevance. A generic builder should treat the feature
interfaces as a compile-time convenience layer over a subset, and `--schema` as the source of truth.

---

## 3. `Siemens.Engineering.HmiUnified.RuntimeSettings` (+ `.HmiRuntimeSettingsCommon`)

29 types total, of which 11 are `*FactoryFacade` abstracts with no members — so **18 substantive types**,
carrying **194 declared public instance members** (85 properties, 109 methods). Entry point:

```
Siemens.Engineering.HmiUnified.HmiSoftware.RuntimeSettings : HmiRuntimeSetting [get]
```

`HmiSoftware.RuntimeSettings` is the sole exposure of `HmiRuntimeSetting` anywhere in the assembly, and
it is **get-only** — the settings object is reached, never replaced.

**None of this is written by this project.** Device-level runtime configuration is out of scope
(docs/10 non-goal), and `openness-cli hmi-create-screen` is a screen-level write probe only. This
section is a read-only map of what *could* be configured, recorded so the question does not have to be
re-asked.

### 3.1 The root object

```
=== HmiRuntimeSetting
  P AutoLogOffURL                          : String   [get set]
  P BitSelection                           : Boolean  [get set]
  P BitSelectionStrategyForResourceLists   : BitNumberEvaluationType   [get set]
  P BitSelectionStrategyForTagDynamization : BitNumberEvaluationType   [get set]
  P EnableLanguageCompatibleFontFamilies   : Boolean  [get set]
  P GeneralESIGCommentsStrategy            : GeneralESIGCommentsStrategy [get set]
  P GMPEnabled                             : Boolean  [get set]
  P ScreenResolution                       : ScreenResolution [get set]      <-- SETTABLE
  P StartScreen                            : String   [get set]              <-- SETTABLE
  P LanguageAndFonts        : HmiLanguageAndFontAssociation [get]            <-- READ-ONLY collection
  P HmiReportingSettings                   : HmiReportingSettings [get set]
  P HmiUnifiedTagSettings                  : HmiUnifiedTagSettings [get set]
  P HmiUpssRuntimeSettings                 : HmiUpssRuntimeSettings [get set]
  P MaxLoginRuntimeSettings                : HmiMaxLoginRuntimeSettings [get set]
  P OpcUaServerRuntimeSettings             : HmiOpcUaServerRuntimeSettings [get set]
  P ProcessDiagnosticsRuntimeSettings      : HmiProcessDiagnosticsRuntimeSettings [get set]
  P RuntimeResourceSettings                : HmiRuntimeResourceSettings [get set]
  P TelemetryRuntimeSettings               : HmiTelemetryRuntimeSettings [get set]
  P Parent                                 : IEngineeringObject [get]
  M GetAttribute / GetAttributeInfos / GetAttributes x2 / SetAttribute / SetAttributes x2
  M Validate() : IList`1                                                     <-- has a validator
```

### 3.2 Sub-objects

```
=== HmiLanguageAndFont      (item of the LanguageAndFonts collection)
  P DefaultFont       : String  [get set]     <-- settable
  P Enable            : Boolean [get set]     <-- settable
  P EnableForLogging  : Boolean [get set]     <-- settable
  P FixedFont1..4     : String  [get]         <-- READ-ONLY
  P Language          : String  [get]         <-- READ-ONLY
  P Order             : Int16   [get]         <-- READ-ONLY
  M GetAttribute/GetAttributeInfos/GetAttributes/SetAttribute/SetAttributes   (NO Validate())

=== HmiLanguageAndFontAssociation    (collection; Count/IsReadOnly/Item all [get];
                                      Contains/IndexOf/GetEnumerator — NO Create, NO Add, NO Remove)

=== HmiMaxLoginRuntimeSettings
  P EnableLockAfterNumberOfAttempts : Boolean [get set]   P MaxLoginErrors : UInt32 [get set]
  M ... Validate() : IList`1

=== HmiOpcUaServerRuntimeSettings   (22 properties, ALL [get set] except Parent)
  ActAsOPCServer, AllowAlarmOperations, EnableAlarmConditions, EnableGuestAuthentication,
  EnableLocalDiscoveryServer, EnableUsernameAndPasswordAuthentication, EndpointUrlPortNumber:Int32,
  MaxMonitoredItemPerSubscriptionCount:Int32, MaxSessionCount:Int32, MaxSessionTimeout:Int32,
  MinPublishingInterval:Int32, and 12 security-policy booleans:
    OpcUaServerSecurityNone,
    OpcUaServerSecurity128RsaModeSigned / ...SignedAndEncrypted,
    OpcUaServerSecurity256ModeSigned / ...SignedAndEncrypted,
    OpcUaServerSecurity256ShaModeSigned / ...SignedAndEncrypted,
    OpcUaServerSecurityAes128Sha256RsaOaepSigned / ...SignedAndEncrypted,
    OpcUaServerSecurityAes256Sha256RsaPssSigned / ...SignedAndEncrypted
  M ... Validate() : IList`1

=== HmiProcessDiagnosticsRuntimeSettings
  P CriteriaAnalysisAbsoluteAddress : Boolean [get set]
  P CriteriaAnalysisAll             : ExtendTextWith [get set]
  P CriteriaAnalysisComment         : Boolean [get set]
  P CriteriaAnalysisExtendText      : CriteriaAnalysisExtendedText [get set]
  P CriteriaAnalysisSymbol          : Boolean [get set]
  P CriteriaAnalysisValue           : Boolean [get set]
  P EnableProcessDiagnostics        : Boolean [get set]      (no Validate())

=== HmiReportingSettings
  P IsReportingEnabled : Boolean [get set]
  P ReportingDatabaseStorage : StorageLocation [get set]  P ReportingDatabaseStoragePath : String [get set]
  P ReportingMainStorage     : StorageLocation [get set]  P ReportingMainStoragePath     : String [get set]

=== HmiRuntimeResourceSettings
  P EnableHighResolutionGraphicsOptimization : Boolean [get set]

=== HmiTelemetryRuntimeSettings
  P TelemetryActive : Boolean [get set]   P TelemetryStorageFolder : String [get set]
  P TelemetryStorageMedia : StorageLocation [get set]

=== HmiUnifiedTagSettings
  P IgnoreInitialQCNotifications : Boolean [get set]  P IgnoreTimestampNotifications : Boolean [get set]

=== HmiUpssRuntimeSettings
  P GlobalScopePersistencyAuthorization : String [get set]
  P PersistencyStrategy : GeneralPersistencyStrategy [get set]
```

### 3.3 Settable vs read-only — the complete picture

Reflection reports read-only properties in the whole tree as:

```
HmiLanguageAndFont              readonly = Parent, FixedFont1, FixedFont2, FixedFont3, FixedFont4,
                                           Language, Order
HmiLanguageAndFontAssociation   readonly = Parent, Count, IsReadOnly, Item
HmiRuntimeSetting               readonly = Parent, LanguageAndFonts
HmiMaxLoginRuntimeSettings      readonly = Parent
HmiOpcUaServerRuntimeSettings   readonly = Parent
HmiProcessDiagnosticsRuntimeSettings  readonly = Parent
HmiReportingSettings            readonly = Parent
HmiRuntimeResourceSettings      readonly = Parent
HmiTelemetryRuntimeSettings     readonly = Parent
HmiUnifiedTagSettings           readonly = Parent
HmiUpssRuntimeSettings          readonly = Parent
```

In other words: **everything is settable except `Parent`, the `LanguageAndFonts` collection handle, and
the seven read-only members of `HmiLanguageAndFont`.** Caveat that must not be lost — this is
*metadata* settability. Openness routinely declares a CLR setter on an attribute that the live
`GetAttributeInfos` reports as read-only or not create-relevant. Treat this table as "not obviously
blocked", not as "writable".

On the four specifically asked about:

- **`StartScreen`** — `String`, `[get set]`, directly on `HmiRuntimeSetting`. A screen *name*, not an
  object reference, so like `HmiTrendCompanion.SourceTrendControl` there is nothing in the type system
  preventing a dangling value; whether `Validate()` catches an unknown screen name is UNKNOWN.
- **`ScreenResolution`** — the `ScreenResolution` enum, `[get set]`, 32 fixed values (§3.4). It is a
  *pick from a list*, not a width/height pair, so a resolution not in the enum cannot be expressed here.
- **`LanguageAndFonts`** — `[get]` only, and the `HmiLanguageAndFontAssociation` collection has
  **no `Create`, `Add` or `Remove`** (only `Contains`/`IndexOf`/`GetEnumerator`/`Item`/`Count`). The
  language *set* is therefore fixed from this API's point of view and presumably follows the project
  languages; what is settable is per-language `Enable`, `EnableForLogging` and `DefaultFont`.
- **Default language** — **there is no default-language property.** An assembly-wide sweep of
  `HmiUnified*` for any property whose name contains `Lang` returns exactly three hits:
  `HmiLanguageAndFont.Language` (read-only), `HmiRuntimeSetting.EnableLanguageCompatibleFontFamilies`
  (a font-fallback boolean), and `HmiRuntimeSetting.LanguageAndFonts` (the read-only collection).
  `HmiLanguageAndFont.Order : Int16 [get]` is the closest thing to a language priority and it is
  **read-only**. Whether runtime default language is set elsewhere — project-level editing languages,
  a `SetAttribute`-only attribute with no CLR property, or not through Openness at all — is **UNKNOWN**
  from reflection; `--schema`-style `GetAttributeInfos` on a live `HmiRuntimeSetting` would settle it.

`Validate()` exists on only four of the eighteen types: `HmiRuntimeSetting`,
`HmiMaxLoginRuntimeSettings`, `HmiOpcUaServerRuntimeSettings`, and (from §1) the UI item types. The
other settings sub-objects have no validator of their own, so validation presumably rolls up to the
root — UNKNOWN whether the root's `Validate()` descends into them.

### 3.4 `HmiRuntimeSettingsCommon` enums (7)

```
BitNumberEvaluationType (Int32): ExactMatch=0, LeastSignificantBit=1
CriteriaAnalysisExtendedText (Int32): None=0, EventText=1, InfoText=2, AdditionalText1=3,
   AdditionalText2=4, AdditionalText3=5, AdditionalText4=6, AdditionalText5=7, AdditionalText6=8,
   AdditionalText7=9, AdditionalText8=10, AdditionalText9=11
ExtendTextWith (Int32): TheFirstFaultyOperand=0, AllFaultyOperand=1
GeneralESIGCommentsStrategy (Int32): BothComments=0, SignatureComment=1, OperationComment=2
GeneralPersistencyStrategy (Int32): Discard=0, UserScope=1
StorageLocation (Int32): Default=0, None=1, Off=2, Local=3, SDX51=4, USBX61=5, USBX62=6,
   Internal=7, ProjectFolder=8
ScreenResolution (Int32):
   SR_1980X1280=0,  SR_1920X1200=1,  SR_1920X1080=2,  SR_1680X1050=3,  SR_1600X1200=4,
   SR_1440X900=5,   SR_1366X768=6,   SR_1280X1024=7,  SR_1280X800=8,   SR_1200X1920=9,
   SR_1200X1600=10, SR_1080X1980=11, SR_1080X1920=12, SR_1050X1680=13, SR_1024X1280=14,
   SR_1024X768=15,  SR_900X1440=16,  SR_800X1280=17,  SR_800X600=18,   SR_800X480=19,
   SR_768X1366=20,  SR_768X1024=21,  SR_640X480=22,   SR_600X800=23,   SR_480X800=24,
   SR_480X640=25,   SR_480X272=26,   SR_320X240=27,   SR_272X480=28,   SR_240X320=29,
   SR_240X80=30,    SR_3840x2160=31
```

---

## 4. `Siemens.Engineering.HmiUnified.UI.Enum` — all 97 enums, all values

All 97 have underlying type `Int32`. Eight carry `[Flags]`.

### 4.1 Full dump (alphabetical)

```
HmiAggregationMode: None=0, TimeAverageStepped=1, MinMax=2
HmiAlarmBlock: Undefined=0, ID=1, Name=2, Class=3, Priority=4, Group=5, Origin=6, Area=7, Comments=8,
  Information=9, LoopInAlarm=10, EventText=11, AlarmText1=12, AlarmText2=13, AlarmText3=14,
  AlarmText4=15, AlarmText5=16, AlarmText6=17, AlarmText7=18, AlarmText8=19, AlarmText9=20,
  AlarmState=21, ModificationTime=22, RaiseTime=23, AcknowledgeTime=24, ClearTime=25, ResetTime=26,
  SuppresionsState=27, EscalationLevel=28, Context=29, Duration=30, AcknowledgmentState=31, Value=32,
  ValueQuality=33, ValueLimit=34, HostName=35, UserName=36, ProcessValue1=37, ProcessValue2=38,
  ProcessValue3=39, ProcessValue4=40, ProcessValue5=41, ProcessValue6=42, ProcessValue7=43,
  ProcessValue8=44, ProcessValue9=45, ProcessValue10=46, ClassSymbol=47, StateText=48, GroupID=49
HmiAlarmControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002
HmiAlarmSourceType: NotConfigured=0, ActiveAlarms=1, LoggedAlarms=2, LoggedAlarmsUpdated=3,
  AlarmDefinition=4, AlarmStatistics=5
HmiAlarmStatisticBlock: Undefined=0, AverageRaisedRaised=1, AverageRaisedCleared=2,
  AverageRaisedAcknowledged=3, AverageRaisedReset=4, Frequency=5, SumRaisedRaised=6, SumRaisedCleared=7,
  SumRaisedAcknowledged=8, SumRaisedReset=9, ID=10, Name=11, Class=12, Priority=13, Group=14, Origin=15,
  Area=16, Comments=17, Information=18, LoopInAlarm=19, EventText=20, AlarmText1=21, AlarmText2=22,
  AlarmText3=23, AlarmText4=24, AlarmText5=25, AlarmText6=26, AlarmText7=27, AlarmText8=28,
  AlarmText9=29, AlarmState=30, ModificationTime=31, RaiseTime=32, AcknowledgeTime=33, ClearTime=34,
  ResetTime=35, SuppressionState=36, EscalationLevel=37, Context=38, Duration=39,
  AcknowledgmentState=40, Value=41, ValueQuality=42, ValueLimit=43, TagName=44, Computer=45, User=46,
  ProcessValue1=47, ProcessValue2=48, ProcessValue3=49, ProcessValue4=50, ProcessValue5=51,
  ProcessValue6=52, ProcessValue7=53, ProcessValue8=54, ProcessValue9=55, ProcessValue10=56,
  ClassSymbol=57, StateText=58
HmiBackgroundFillMode: Window=0, Screen=1
HmiBarEventType: None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006, KeyUp=1007,
  ContextTapped=1011
HmiBarMode: Segmented=0, Unicolor=1, SegmentedStatic=2, UnicolorStatic=3
HmiButtonEventType: None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006, KeyUp=1007,
  Down=1008, Up=1010, ContextTapped=1011
HmiButtonStyleItemClass: HmiButton=0, HmiRoundButton=1
HmiCapType: Square=0, Round=1, Flat=2
HmiCheckBoxGroupEventType: None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006,
  KeyUp=1007, ContextTapped=1011
HmiCircleEventType: None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006, KeyUp=1007,
  ContextTapped=1011
HmiCircleSegmentEventType: (same 7 as HmiCircleEventType)
HmiCircularArcEventType:   (same 7 as HmiCircleEventType)
HmiClockEventType:         (same 7 as HmiCircleEventType)
HmiComboBoxEventType:      (same 7 as HmiCircleEventType)
HmiContentMode: GraphicOrText=0, GraphicAndText=1, Text=2, Graphic=3
HmiDashType: Solid=0, Dash=1, Dot=2, DashDot=3, DashDotDot=4
HmiDataGridHeaderType: None=0, Index=1, Content=2
HmiDetailedParameterControlBlock: None=0, ParameterSetElementName=1, ParameterSetValue=2,
  ParameterSetElementUnit=3
HmiDetailedParameterControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001,
  CommandFired=2002
HmiDotNetControlContainerEventType: None=0, Activated=1000, Deactivated=1001
[Flags] HmiEditMode: None=0, Update=1, Create=2, Delete=4
HmiEllipseEventType:        (same 7 as HmiCircleEventType)
HmiEllipseSegmentEventType: (same 7 as HmiCircleEventType)
HmiEllipticalArcEventType:  (same 7 as HmiCircleEventType)
HmiFaceplateContainerEventType: None=0, Activated=1000, Deactivated=1001
HmiFillDirection: BottomToTop=0, TopToBottom=1, LeftToRight=2, RightToLeft=3
HmiFillPattern: Solid=0, Transparent=1, BackwardDiagonal=2, Cross=3, DiagonalCross=4, ForwardDiagonal=5,
  Horizontal=6, Vertical=7, GradientHorizontal=8, GradientVertical=9, GradientForwardDiagonal=10,
  GradientBackwardDiagonal=11, GradientHorizontalTricolor=12, GradientVerticalTricolor=13,
  GradientForwardDiagonalTricolor=14, GradientBackwardDiagonalTricolor=15
HmiFlashingRate: Slow=0, Medium=1, Fast=2, None=3
HmiFontName: Arial=0, TimesNewRoman=1, SimSun=2, SiemensSans=3
HmiFontStrikeOut: None=0, Single=1
HmiFontWeight: Light=0, Normal=1, SemiBold=2, Bold=3, None=4
HmiFunctionTrendControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001,
  CommandFired=2002
HmiGaugeEventType:       (same 7 as HmiCircleEventType)
HmiGraphicStretchMode: None=0, Fill=1, Uniform=2, UniformToFill=3, Tiled=4
HmiGraphicViewEventType: (same 7 as HmiCircleEventType)
HmiGridColoringMode: None=0, Columns=1, Rows=2
[Flags] HmiGridLine: None=0, VerticalMajor=1, HorizontalMajor=2, VerticalMinor=4, HorizontalMinor=8
HmiGridSelectionMode: None=0, Single=1, Multi=2
HmiHorizontalAlignment: Left=0, Center=1, Right=2, Stretch=3
HmiIOFieldEventType: (same 7 as HmiCircleEventType)
HmiIOFieldType: Output=0, InputOutput=1
HmiLineEndType: Line=0, EmptyArrow=1, Arrow=2, ReversedArrow=3, EmptyCircle=4, Circle=5
HmiLineEventType:    (same 7 as HmiCircleEventType)
HmiLineJoinType: Round=0, Bevel=1, Miter=2
HmiListBoxEventType: (same 7 as HmiCircleEventType)
HmiMarkerType: None=0, Point=1, Square=2, Circle=3, Graphic=4
HmiMediaControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002,
  Paused=4711, Playing=4712, Stopped=4713
HmiOrientation: Horizontal=0, Vertical=1
HmiOverviewParameterControlBlock: None=0, ParameterSetID=1, LastUser=2, LastAccess=3,
  ParameterSetElementOdd=4, ParameterSetElementEven=5
[Flags] HmiPeakIndicator: None=0, Low=1, High=2
HmiPolygonEventType:  (same 7 as HmiCircleEventType)
HmiPolylineEventType: (same 7 as HmiCircleEventType)
HmiProcessControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002
HmiProcessIndicatorMode: Bar=0, Indicator=1, DetailedIndicator=2, BarWithDetailedIndicator=3
[Flags] HmiQuality: None=0, Bad=1, Uncertain=2, Good=4, UpperLimitViolation=8, LowerLimitViolation=16
HmiRadioButtonGroupEventType: (same 7 as HmiCircleEventType)
HmiRectangleEventType:        (same 7 as HmiCircleEventType)
HmiRotationCenterPlacement: AbsoluteFromCenter=0, NormedFromCenter=1, AbsoluteToContainer=2
[Flags] HmiScaleMode: None=0, Labels=1, Ticks=2
HmiScalingType: Linear=0, Logarithmic=1, NegativeLogarithmic=2, Tangent=3, Quadratic=4, Cubic=5
HmiScreenEventType: None=0, Tapped=1003, ContextTapped=1011, Loaded=1018, Unloaded=1019
HmiScreenWindowAdaption: None=0, WindowToScreen=1, ScreenToWindow=2
HmiScreenWindowEventType: None=0, Activated=1000, Deactivated=1001
HmiScrollBarVisibility: Automatic=0, Visible=1, Collapsed=2
HmiSelectionMode: NonExclusive=0, Exclusive=1
[Flags] HmiSimpleGridLine: None=0, Vertical=1, Horizontal=2
HmiSimplePosition: LeftOrTop=0, RightOrBottom=1
HmiSliderEventType: (same 7 as HmiCircleEventType)
HmiSortDirection: None=0, Ascending=1, Descending=2
HmiSymbolicIOFieldEventType: (same 7 as HmiCircleEventType)
HmiSystemDiagnosisControlBlock: Undefined=0, Number=1, DateTime=2, EventMessage=3, EventType=4,
  EventState=5
HmiSystemDiagnosisControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001,
  CommandFired=2002
HmiSystemDiagnosisMatrixBlock: Undefined=0, Status=1, Name=2, OperatingState=3, Rack=4, Slot=5,
  OrderNumber=6, Address=7, PlantDesignation=8, LocationIdentifier=9, Subsystem=10, Station=11,
  Subslot=12, SubAddress=13, SoftwareVersion=14, Installation=15, AdditionaInformation=16,
  ErrorDescription=17, ManufacturerID=18, HardwareVersion=19, ProfileID=20, SpecificProfileData=21,
  IandMDataVersion=22, SerialNumber=23, RevisionCounter=24
HmiSystemDiagnosisViewType: Diagnosis=0, Matrix=1
HmiTextBoxEventType: (same 7 as HmiCircleEventType)
HmiTextEventType:    (same 7 as HmiCircleEventType)
HmiTextPosition: Left=0, Right=1, Top=2, Bottom=3, Behind=4, InFront=5
HmiTextTrimming: None=0, CharacterEllipsis=1
HmiTextWrapping: NoWrap=0, WordWrap=1
HmiThresholdMode: Undefined=0, Upper=1, Lower=2, Normal=3, Minimum=4, Maximum=5
HmiTimeRangeBase: Undefined=0, Millisecond=1, Second=2, Minute=3, Hour=4, Day=5, Month=6, Year=7
HmiTimeRangeType: TimeRange=0, FromBeginToEnd=1, PointCount=2
HmiToggleSwitchEventType: None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006,
  KeyUp=1007, Down=1008, Up=1010, ContextTapped=1011, StateChanged=1025
HmiTouchAreaEventType: None=0, GestureDetected=1012
HmiTrendCompanionEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002
HmiTrendCompanionMode: Ruler=0, StatisticArea=1, StatisticResult=2
HmiTrendControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002
HmiTrendInfoBlock: None=0, Name=1, Index=2, Label=3, Show=4, TagNameY=5, TagNameX=6, YValue=7,
  XValueOrTimestamp=8, YValueLowerLimit=9, TimestampLowerLimit=10, YValueUpperLimit=11,
  TimestampUpperLimit=12, Minimum=13, MinimumTimestamp=14, Maximum=15, MaximumTimestamp=16, Average=17,
  StandardDeviation=18, Integral=19, WeightedAverageValue=20, Duration=21, NumberOfValues=22,
  AreaName=23, AreaNameLL=24, AreaNameHL=25, Sum=26
HmiTrendMode: Points=0, Interpolated=1, Stepped=2, Bar=3, Value=4
HmiVerticalAlignment: Top=0, Center=1, Bottom=2, Stretch=3
HmiVideoOutput: Undefined=0, Stretch=1, PreserveAspectFit=2, PreserveAspectCrop=3
[Flags] HmiVisibleAlarms: None=0, UnSuppressed=1, Disabled=2, SuppressedByDesign=4, Shelved=8
HmiWebControlEventType: None=0, Activated=1000, Deactivated=1001, Initialized=2001, CommandFired=2002
[Flags] HmiWindowFlag: None=0, ShowCaption=1, ShowBorder=2, AlwaysOnTop=4, CanSize=8, CanMove=16,
  CanMaximize=32, CanClose=64, AlwaysInParent=128
```

The eleven enums marked "(same 7 as `HmiCircleEventType`)" are verbatim identical member/value sets —
`None=0, Activated=1000, Deactivated=1001, Tapped=1003, KeyDown=1006, KeyUp=1007, ContextTapped=1011` —
expanded here only to keep the dump readable; the reflection run printed each in full.

### 4.2 Grouping — what the 97 configure

| Group | Count | Members |
|---|---|---|
| **Per-type event catalogues** (`Hmi*EventType`) | 39 | One per screen-item type. Not configuration at all — they name the events the matching `EventHandlers` composition can carry. |
| **"Block" / column-content selectors** (`Hmi*Block`) | 7 | `HmiAlarmBlock` (50 values), `HmiAlarmStatisticBlock` (59), `HmiTrendInfoBlock` (27), `HmiSystemDiagnosisMatrixBlock` (25), `HmiOverviewParameterControlBlock` (6), `HmiSystemDiagnosisControlBlock` (6), `HmiDetailedParameterControlBlock` (4). These are the column vocabularies for §1.3's grids and are the single largest body of content here. |
| **Colour / fill / stroke** | 8 | `HmiFillPattern` (16), `HmiFillDirection`, `HmiDashType`, `HmiCapType`, `HmiLineEndType`, `HmiLineJoinType`, `HmiGridColoringMode`, `HmiBackgroundFillMode` |
| **Alignment / position / orientation** | 6 | `HmiHorizontalAlignment`, `HmiVerticalAlignment`, `HmiTextPosition`, `HmiSimplePosition`, `HmiOrientation`, `HmiRotationCenterPlacement` |
| **Text & font** | 6 | `HmiFontName`, `HmiFontWeight`, `HmiFontStrikeOut`, `HmiTextTrimming`, `HmiTextWrapping`, `HmiContentMode` |
| **Grid / table appearance** | 4 | `HmiGridLine` ⚑, `HmiSimpleGridLine` ⚑, `HmiGridSelectionMode`, `HmiDataGridHeaderType` |
| **Trend & axis** | 8 | `HmiTrendMode`, `HmiAggregationMode`, `HmiScalingType`, `HmiScaleMode` ⚑, `HmiMarkerType`, `HmiThresholdMode`, `HmiTimeRangeBase`, `HmiTimeRangeType` |
| **Alarm** | 3 | `HmiAlarmSourceType`, `HmiVisibleAlarms` ⚑, `HmiFlashingRate` |
| **Scroll / window / screen** | 4 | `HmiScrollBarVisibility`, `HmiWindowFlag` ⚑, `HmiScreenWindowAdaption`, `HmiSystemDiagnosisViewType` |
| **Widget-specific modes** | 7 | `HmiBarMode`, `HmiButtonStyleItemClass`, `HmiIOFieldType`, `HmiSelectionMode`, `HmiProcessIndicatorMode`, `HmiPeakIndicator` ⚑, `HmiTrendCompanionMode` |
| **Quality / data state** | 1 | `HmiQuality` ⚑ |
| **Editing / media / graphics** | 4 | `HmiEditMode` ⚑, `HmiVideoOutput`, `HmiGraphicStretchMode`, `HmiSortDirection` |

⚑ = `[Flags]`. Totals: 39 + 7 + 8 + 6 + 6 + 4 + 8 + 3 + 4 + 7 + 1 + 4 = 97.

### 4.3 Non-contiguous or surprising values — flagged

- **Every `Hmi*EventType` is deliberately non-contiguous** and shares one global ID space: `None=0`, a
  `1000`-block for item lifecycle/input (`Activated=1000`, `Deactivated=1001`, `Tapped=1003`,
  `KeyDown=1006`, `KeyUp=1007`, `Down=1008`, `Up=1010`, `ContextTapped=1011`, `GestureDetected=1012`,
  `Loaded=1018`, `Unloaded=1019`, `StateChanged=1025`) and a `2000`-block for control lifecycle
  (`Initialized=2001`, `CommandFired=2002`). The gaps are conspicuous: **1002, 1004, 1005, 1009,
  1013–1017, 1020–1024 are never used by any public enum**, and `Down=1008` / `Up=1010` skip 1009. That
  strongly implies a larger internal event ID space of which only part is published — do not assume the
  published values are all the values, and never derive an ID by arithmetic.
- **`HmiMediaControlEventType.Paused=4711, Playing=4712, Stopped=4713`.** Wildly out of band relative to
  every other event enum. 4711 is the Kölnisch-Wasser house number, a long-running German placeholder
  number; whatever the reason, these three are the only event values outside the 1000/2000 blocks in
  the whole namespace.
- **`HmiFlashingRate.None=3`** — `None` is last, not zero: `Slow=0, Medium=1, Fast=2, None=3`. Default-
  initialising a field of this type to `0` gives **Slow flashing**, not "no flashing". Same trap in
  **`HmiFontWeight.None=4`** (`Light=0` is the zero value). Both are the reverse of the convention every
  other enum here follows (`None`/`Undefined`/`NotConfigured` = 0).
- **`HmiAlarmBlock.SuppresionsState=27` is misspelled** in the API ("Suppresions", and the plural is
  wrong too). The equivalent member in `HmiAlarmStatisticBlock` is spelled correctly as
  `SuppressionState=36`. Any code or config file naming these must reproduce each typo verbatim.
- **`HmiSystemDiagnosisMatrixBlock.AdditionaInformation=16` is misspelled** ("Additiona").
- **`HmiAlarmBlock` and `HmiAlarmStatisticBlock` are not aligned.** They overlap heavily but use
  different names for the same concepts (`HostName`/`UserName` vs `Computer`/`User`), the statistic
  variant adds ten aggregate blocks at the front (which is why every shared member's number differs by
  9 or more), `HmiAlarmBlock` has `GroupID=49` with no statistic counterpart, and
  `HmiAlarmStatisticBlock` has `TagName=44` with no alarm counterpart. Never map one to the other by
  ordinal.
- **`ScreenResolution.SR_1980X1280=0`** is not a real display resolution — almost certainly a Siemens
  typo for `1920x1200` or `1920x1080`, both of which also exist in the enum as separate members. It is
  the **zero value**, so it is what an unset field would read as. Also note `SR_1080X1980=11` carries
  the same transposed digits. Additionally `SR_3840x2160=31` uses a **lowercase `x`** where all 31
  other members use uppercase `X`, and it is appended at the end, breaking the otherwise
  descending-size ordering — it reads as a later 4K addition.
- **`HmiQuality` is `[Flags]` with `Good=4`,** so `Good` is not the zero value and `None=0` is a
  distinct state from `Bad=1`. It also mixes quality (`Bad`/`Uncertain`/`Good`) with limit violations
  (`UpperLimitViolation=8`, `LowerLimitViolation=16`) in one flag word — a value can be `Good` *and*
  limit-violating simultaneously. `HmiScreenItemBase.CurrentQuality` is read-only.
- **`HmiEditMode` is `[Flags]`** (`Update=1, Create=2, Delete=4`) on `HmiProcessControl` and
  `HmiDetailedParameterControl`. `None=0` therefore means read-only, and it is easy to misread as an
  exclusive mode selector.
- **`HmiButtonStyleItemClass` is an enum whose values are type names** (`HmiButton=0`,
  `HmiRoundButton=1`) while the actual property it appears to serve — `HmiScreenItemBase.StyleItemClass`
  — is typed **`String`**, not this enum. So the enum is a source of valid *names*, not the property's
  type; nothing type-checks the string against it. The complete set of valid `StyleItemClass` strings
  for the other ~55 item types is **UNKNOWN** from reflection.
- **`HmiTextPosition` mixes position with z-order**: `Left/Right/Top/Bottom` then `Behind=4`,
  `InFront=5`.
- **`HmiSimplePosition` is orientation-dependent** (`LeftOrTop=0`, `RightOrBottom=1`) — its meaning
  changes with the owning item's `HmiOrientation`.
- **`HmiScalingType` includes `Tangent=3`, `Quadratic=4`, `Cubic=5`** alongside the expected
  `Linear`/`Logarithmic`/`NegativeLogarithmic` — a wider set of axis scalings than most HMI packages
  expose.
- **`HmiVisibleAlarms.UnSuppressed=1`** has an interior capital `S`; and as a `[Flags]` value, `None=0`
  means *no alarms visible at all*, which is a plausible accidental default.
- **`HmiFontName` has only four members** (`Arial`, `TimesNewRoman`, `SimSun`, `SiemensSans`), yet
  `HmiLanguageAndFont.DefaultFont` and the `FixedFont1..4` members are typed `String`. Same
  enum-vs-string split as `StyleItemClass`; whether arbitrary font names are accepted is **UNKNOWN**.
- **`HmiScreenEventType` has no `Activated`/`Deactivated`** — a screen gets `Loaded=1018` /
  `Unloaded=1019` instead, plus `Tapped`/`ContextTapped`. Screens and screen items do not share an
  event vocabulary.
- **`HmiTouchAreaEventType` has exactly one real event**, `GestureDetected=1012`, and notably lacks
  `Activated`/`Deactivated`.
- **`HmiTrendInfoBlock.AreaNameLL=24` / `AreaNameHL=25`** use unexpanded LL/HL abbreviations next to the
  fully-spelled `YValueLowerLimit`/`YValueUpperLimit` in the same enum.

---

## 5. Scope note

Nothing in this document was executed against TIA Portal. It is offline metadata. Anything here that
would drive a write — attribute names, enum values, settability — must be confirmed against a live
`GetAttributeInfos`/`GetCreationInfos` read (`openness-cli hmi --schema`) before use, because CLR
metadata does not carry Openness access mode or create-relevance. HMI engineering remains a
docs/10 non-goal; RuntimeSettings in particular is device-level and is catalogued here only so the
map exists.
