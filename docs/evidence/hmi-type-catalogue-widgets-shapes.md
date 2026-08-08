# HMI Unified type catalogue — Widgets, Shapes, Base (reflection)

This is a **reflection catalogue** of the WinCC Unified screen-item type tree as it exists in
`C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll` (Openness V20,
assembly version 20.0.0.0). It was produced on **2026-08-08** by loading the assembly and enumerating
`GetTypes()` — **no TIA Portal process was started, attached to, or contacted, and no project was
opened**. Everything below is therefore **type shape only: what the API declares, not what the API
does**. Nothing here is live-verified — a property that reflection reports as `get/set` may still be
rejected at runtime, may be create-irrelevant, or may need a companion property set first; only a live
`hmi --schema` run (which reads `GetAttributeInfos`/`GetCreationInfos` from a real project) or an actual
create can settle that. Where reflection cannot answer a question, this document says **UNKNOWN** rather
than guessing. Scope: the three namespaces `Siemens.Engineering.HmiUnified.UI.Widgets` (18 types),
`...UI.Shapes` (20 types) and `...UI.Base` (11 types) — 49 types, **340 declared public properties and
168 declared public methods = 508 declared members** (the `*FactoryFacade` companions, which are
`abstract sealed` plumbing classes with no authoring surface, are excluded from all counts). Related
prior work: `docs/notes/openness-hmi-api-survey.md` (why there is no screen export to decode) and
`src/openness-cli/README.md` (the `hmi` / `hmi --schema` / `hmi-create-screen` commands).

---

## 1. The inheritance tree

Every UI object in these namespaces descends from a single root, `Siemens.Engineering.HmiUnified.UI.UIBase`
(itself deriving from `System.Object`). `UIBase` is where the *generic Openness* surface lives —
`GetAttribute`/`SetAttribute`/`GetAttributeInfos`/`GetAttributes`/`SetAttributes`, the `Dynamizations`
composition (this is where tag bindings hang), `PropertyEventHandlers`, and `Validate()`. Below `UIBase`
the tree forks immediately into *screens* and *screen items*, and the screen-item branch then forks three
ways: windows, shapes and widgets.

```text
System.Object
└── UI.UIBase                                  Dynamizations, PropertyEventHandlers,
    │                                          Get/SetAttribute(s), GetAttributeInfos, Validate()
    ├── UI.Base.HmiScreenBase                  Name, DisplayName, Delete()
    │   └── UI.Screens.HmiScreen               ScreenItems, Width/Height, BackColor…, ResizeScreen()
    │                                          (outside the 3 scoped namespaces; included for context)
    │
    └── UI.Base.HmiScreenItemBase              ***THE UNIVERSAL SCREEN-ITEM CONTRACT***
        │                                      Name, Enabled, Visible, StyleItemClass, TabIndex,
        │                                      ShowFocusVisual, CurrentQuality (RO), Delete()
        │
        ├── UI.Widgets.HmiTouchArea            (sealed; joins HERE, NOT under SimpleScreenItemBase —
        │                                       re-declares its own Authorization/RequireExplicitUnlock,
        │                                       has Left/Top/Width/Height + BackColor, has NO rotation,
        │                                       NO Opacity, NO ToolTipText)
        │
        ├── UI.Base.HmiWindowBase              Caption, CaptionColor, Icon, WindowFlags,
        │   │                                  Left/Top/Width/Height
        │   ├── UI.Base.HmiContainerBase       ContainedType
        │   │   └── UI.Base.HmiCustomWebControlContainer  (sealed) Interface, Authorization,
        │   │                                              RequireExplicitUnlock, EventHandlers
        │   └── UI.Base.HmiControlWindowBase   BackColor, StatusBar, ToolBar
        │       ├── UI.Base.HmiCompanionBase   SnapToSourceControl
        │       └── UI.Base.HmiTrendControlBase  AreaSpacing, ExtendRulerToAxis, Legend, Online,
        │                                        ShiftAxes, ShowRuler
        │
        └── UI.Base.HmiSimpleScreenItemBase    Authorization, RequireExplicitUnlock,
            │                                  Opacity, ToolTipText (RO handle)
            │
            ├── UI.Base.HmiCustomWidgetContainer  (sealed) Interface, box, rotation, VisualizeQuality
            │
            ├── UI.Shapes.HmiShapeBase         RotationAngle, RotationCenterPlacement,
            │   │                              RotationCenterX/Y   — but NO position/size yet
            │   │
            │   ├── UI.Shapes.HmiCentricShapeBase   CenterX, CenterY      <- NO Left/Top/Width/Height
            │   │   ├── UI.Shapes.HmiCircularShapeBase      Radius
            │   │   │   ├── UI.Shapes.HmiCircle             area-fill set
            │   │   │   │   └── UI.Shapes.HmiCircleSegment  (sealed) StartAngle, AngleRange
            │   │   │   └── UI.Shapes.HmiCircularArc        (sealed) line set + StartAngle, AngleRange
            │   │   └── UI.Shapes.HmiEllipticalShapeBase    RadiusX, RadiusY
            │   │       ├── UI.Shapes.HmiEllipse            area-fill set
            │   │       │   └── UI.Shapes.HmiEllipseSegment (sealed) StartAngle, AngleRange
            │   │       └── UI.Shapes.HmiEllipticalArc      (sealed) line set + StartAngle, AngleRange
            │   │
            │   └── UI.Shapes.HmiSurfaceShapeBase   Left, Top, Width, Height
            │       ├── UI.Shapes.HmiRectangle      (sealed) area-fill set + Corners
            │       ├── UI.Shapes.HmiLine           (sealed) line set + X1, Y1, X2, Y2
            │       ├── UI.Shapes.HmiText           (sealed) Text, Font, ForeColor, alignments
            │       ├── UI.Shapes.HmiGraphicView    (sealed) Graphic, GraphicStretchMode, Padding
            │       └── UI.Shapes.HmiPointBasedShapeBase   Points, JoinType
            │           ├── UI.Shapes.HmiPolygon    (sealed) area-fill set
            │           └── UI.Shapes.HmiPolyline   (sealed) line set
            │
            └── UI.Widgets.HmiWidgetBase       BackColor/AlternateBackColor, BorderColor/
                │                              AlternateBorderColor, BorderWidth,
                │                              Left/Top/Width/Height, rotation, VisualizeQuality
                ├── UI.Widgets.HmiButton       Text, AlternateText, Graphic, AlternateGraphic,
                │   │                          HotKey, Content, Font, ForeColor, Padding
                │   └── UI.Widgets.HmiToggleSwitch   (sealed) IsAlternateState
                ├── UI.Widgets.HmiClock        (sealed) Dial*, ShowHours/Minutes/Seconds, TimeSource
                ├── UI.Widgets.HmiScaleWidgetBase    ProcessValue, OriginValue, Thresholds,
                │   │                                PeakIndicators, ProcessValueIndicator*, scale colors
                │   ├── UI.Widgets.HmiBar      BarMode, StraightScale
                │   │   └── UI.Widgets.HmiSlider     (sealed) Thumb*, ShowValue, ValuePosition,
                │   │                                WriteDuringChange
                │   └── UI.Widgets.HmiGauge    (sealed) CurvedScale
                ├── UI.Widgets.HmiSelectionGroupBase  SelectionItems, SelectorPosition,
                │   │                                 SelectionItemHeight, ProcessValue (RO), Content
                │   ├── UI.Widgets.HmiCheckBoxGroup     (sealed) — EventHandlers only
                │   ├── UI.Widgets.HmiRadioButtonGroup  (sealed) — EventHandlers only
                │   └── UI.Widgets.HmiListBox           (sealed) SelectionMode
                └── UI.Widgets.HmiTextWidgetBase   Font, ForeColor, Padding
                    ├── UI.Widgets.HmiIOField      (sealed) ProcessValue, IOFieldType, OutputFormat,
                    │                              InputBehavior, Thresholds, alignments, TextTrimming
                    ├── UI.Widgets.HmiSymbolicIOField (sealed) ProcessValue, ResourceList, IOFieldType,
                    │                              SelectedIndex (RO), Graphic (RO), Selection*Color
                    └── UI.Widgets.HmiLabel        Text, TextWrapping, TextTrimming, alignments
                        └── UI.Widgets.HmiTextBox  (sealed) ReadOnly
```

Two members of the scoped namespaces are **not screen items at all** and sit outside this tree, both
directly on `System.Object`:

- `UI.Shapes.HmiPoint` — a vertex. `X`, `Y` (both `Int32`, get/set), `Delete()`, and the full
  `GetAttribute`/`SetAttribute` surface. It is an `IEngineeringObject` but **not** a `UIBase`, so it has
  no `Dynamizations` — a polygon vertex cannot be tag-driven at the vertex level.
- `UI.Shapes.HmiPointComposition` and `UI.Base.HmiScreenItemBaseComposition` — the two collections
  (`Count`, `IsReadOnly`, `Item[Int32]`, `Contains`, `IndexOf`, `GetEnumerator`, `Create`).

**Where members come from.** The tree is assembled by *feature mixin interfaces* in
`Siemens.Engineering.HmiUnified.UI.Features`, and reading them is the fastest way to know what a type
has. The load-bearing ones, with the level that supplies them:

| Feature interface | Members | Supplied at |
|---|---|---|
| `IHmiBasicScreenItemFeature` | `Enabled`, `Name`, `StyleItemClass`, `TabIndex`, `Visible` | `HmiScreenItemBase` |
| `IHmiIdentifierFeature` | *(marker — no members)* | `HmiScreenItemBase` |
| `IHmiOperabilityFeature` | `Authorization`, `RequireExplicitUnlock` | `HmiSimpleScreenItemBase` (and re-declared on `HmiTouchArea`, `HmiCustomWebControlContainer`) |
| `IHmiBoxFeature` | `Left`, `Top`, `Width`, `Height` | `HmiWindowBase`, `HmiWidgetBase`, `HmiSurfaceShapeBase`, `HmiTouchArea`, `HmiCustomWidgetContainer` — **never on `HmiScreenItemBase`** |
| `IHmiRotationFeature` | `RotationAngle`, `RotationCenterPlacement`, `RotationCenterX/Y` | `HmiShapeBase`, `HmiWidgetBase`, `HmiCustomWidgetContainer` |
| `IHmiAreaFeature` | `BackColor`, `AlternateBackColor`, `BorderColor`, `AlternateBorderColor`, `BorderWidth`, `BackFillPattern`, `FillDirection`, `FillLevel`, `ShowFillLevel`, `DashType` | `HmiRectangle`, `HmiPolygon`, `HmiCircle(+Segment)`, `HmiEllipse(+Segment)` |
| `IHmiLineFeature` | `LineColor`, `AlternateLineColor`, `LineWidth`, `CapType`, `DashType`, `StartType`, `EndType` | `HmiLine`, `HmiPolyline`, `HmiCircularArc`, `HmiEllipticalArc` |
| `IHmiArcFeature` | `StartAngle`, `AngleRange` | the four segment/arc types |
| `IHmiWindowFeature` | `Caption`, `CaptionColor`, `Icon`, `WindowFlags` | `HmiWindowBase` and below |

Note that `IHmiAreaFeature` and `IHmiLineFeature` are **mutually exclusive in practice**: a shape is
either filled-with-a-border or stroked-as-a-line, never both. `HmiPolygon` is the area version and
`HmiPolyline` the line version of the identical `HmiPointBasedShapeBase` geometry.

---

## 2. The COMMON authoring surface

There are two honest answers here, because the API has two different "commons" and conflating them is
the single easiest way to write a generic builder that crashes on circles.

### 2a. Truly universal — every concrete screen item, no exceptions

These come from `HmiScreenItemBase` (plus `UIBase` above it) and are present on all 18 widgets, all
non-helper shapes, all window/container types, and `HmiTouchArea`:

| Property / member | Type | Access | Notes |
|---|---|---|---|
| `Name` | `String` | get/set | also the key used by `HmiScreenItemBaseComposition.Find(name)` |
| `Enabled` | `Boolean` | get/set | operability, not visibility |
| `Visible` | `Boolean` | get/set | |
| `StyleItemClass` | `String` | get/set | the style hook; string-typed, values UNKNOWN from reflection |
| `TabIndex` | `UInt16` | get/set | **UInt16, not Int32** |
| `ShowFocusVisual` | `Boolean` | get/set | declared on `HmiScreenItemBase`, not exposed by any feature interface |
| `CurrentQuality` | `UI.Enum.HmiQuality` | get **only** | `[Flags]`: None/Bad/Uncertain/Good/UpperLimitViolation/LowerLimitViolation — runtime state, never authored |
| `Parent` | `SE.IEngineeringObject` | get only | |
| `Delete()` | → `void` | | the only removal path; the compositions expose no `Remove` |
| `Dynamizations` | `DynamizationBaseComposition` | get only | *from `UIBase`* — where tag/script/flashing bindings hang |
| `PropertyEventHandlers` | `PropertyEventHandlerComposition` | get only | *from `UIBase`* |
| `GetAttribute` / `SetAttribute` / `GetAttributes` / `SetAttributes` / `GetAttributeInfos` | | | *from `UIBase`* — the string-keyed escape hatch, and the only route to anything the typed API omits |
| `Validate()` | → `IList<HmiValidationResult>` | | *from `UIBase`*; each result carries `PropertyName`, `Errors`, `Warnings` |

**A generic screen builder can rely on exactly this list and nothing more.** In particular there is
**no universal position or size**, no universal tooltip, no universal colour, and no universal
authorization.

### 2b. Near-universal — present on everything *except* the centric shapes

- **`Authorization` (`String`) and `RequireExplicitUnlock` (`Boolean`)** — from `IHmiOperabilityFeature`
  at `HmiSimpleScreenItemBase`. Present on all widgets, all shapes, `HmiCustomWidgetContainer`,
  `HmiTouchArea` (own declaration) and `HmiCustomWebControlContainer` (own declaration). **Absent from
  the plain window family** (`HmiWindowBase`, `HmiContainerBase`, `HmiControlWindowBase`,
  `HmiCompanionBase`, `HmiTrendControlBase`).
- **`Opacity` (`Single`) and `ToolTipText` (`MultilingualText`, get-only handle)** — also from
  `HmiSimpleScreenItemBase`, so present on all widgets and shapes but **absent from `HmiTouchArea`** and
  from the whole window family. Tooltip is therefore *not* part of the universal surface.
- **`Left`, `Top` (`Int32`) and `Width`, `Height` (`UInt32`)** — from `IHmiBoxFeature`. Present on
  **all 18 widgets** and on the `HmiSurfaceShapeBase` half of the shapes (`HmiRectangle`, `HmiLine`,
  `HmiText`, `HmiGraphicView`, `HmiPolygon`, `HmiPolyline`), plus every window/container type.
  **Absent from all nine centric-shape types** — `HmiCentricShapeBase`, `HmiCircularShapeBase`,
  `HmiCircle`, `HmiCircleSegment`, `HmiCircularArc`, `HmiEllipticalShapeBase`, `HmiEllipse`,
  `HmiEllipseSegment`, `HmiEllipticalArc` — which are positioned by `CenterX`/`CenterY` (`Int32`) and
  sized by `Radius` (`UInt32`) or `RadiusX`/`RadiusY` (`UInt32`).
- **Rotation** (`RotationAngle` `Int16`, `RotationCenterPlacement`, `RotationCenterX/Y` `Single`) — on
  every widget and every shape, but **not** on `HmiTouchArea` and **not** on the window family.

So a builder's placement routine needs exactly two branches: *box* (`Left`/`Top`/`Width`/`Height`) and
*centric* (`CenterX`/`CenterY` + radius). Testing `item is IHmiBoxFeature` is the clean discriminator
and is reliable — the interface is on the type, not just the property names.

---

## 3. Per-type distinctive properties

Terse: the properties that make a type *that* type, over and above everything inherited. Full declared
lists are in Appendix A.

### 3a. Widgets (18)

| Type | What makes it that widget |
|---|---|
| `HmiWidgetBase` *(base)* | `BackColor`/`AlternateBackColor`, `BorderColor`/`AlternateBorderColor`, `BorderWidth` (`Byte`), box, rotation, `VisualizeQuality` |
| `HmiTextWidgetBase` *(base)* | `Font` (part), `ForeColor`, `Padding` (part) — text chrome only, no text |
| `HmiScaleWidgetBase` *(base)* | `ProcessValue` (`String` = tag ref), `OriginValue` (`Double`), `RelativeToOrigin`, `OutputFormat`, `Thresholds`, `PeakIndicators` `[Flags]`, `ProcessValueIndicatorMode`, `ShowTrendIndicator`, `Title`/`Label` parts |
| `HmiSelectionGroupBase` *(base)* | `SelectionItems` (composition), `SelectorPosition`, `SelectionItemHeight` (`UInt16`), `ProcessValue` (**get-only here**), `Content` part |
| `HmiButton` | `Text` + `AlternateText` (MLT), `Graphic` + `AlternateGraphic` (`String`), `HotKey` (`UInt16`), `Content` part |
| `HmiToggleSwitch` | `IsAlternateState` (`Boolean`) — that is the entire delta over `HmiButton` |
| `HmiLabel` | `Text` (MLT), `TextWrapping`, `TextTrimming`, `HorizontalTextAlignment`, `VerticalTextAlignment` |
| `HmiTextBox` | `ReadOnly` (`Boolean`) — entire delta over `HmiLabel` |
| `HmiIOField` | `ProcessValue` (`String`), `IOFieldType` (Output/InputOutput), `OutputFormat` (`String`), `InputBehavior` (part), `Thresholds` |
| `HmiSymbolicIOField` | `ProcessValue`, `ResourceList` (`String`), `IOFieldType`, `SelectedIndex` (RO), `Graphic` (RO), `SelectionBackColor`/`SelectionForeColor` |
| `HmiBar` | `BarMode` (Segmented/Unicolor/…), `StraightScale` (part) |
| `HmiSlider` | `ThumbBackColor`/`ThumbForeColor`, `ShowValue`, `ValuePosition`, `WriteDuringChange` |
| `HmiGauge` | `CurvedScale` (part) — the entire delta over `HmiScaleWidgetBase` |
| `HmiClock` | `DialMode` (`HmiScaleMode` `[Flags]`), `ShowHours`/`ShowMinutes`/`ShowSeconds`, `TimeSource` (RO), `DialBackColor`/`DialLabelColor`/`DialTickColor`, `Title` |
| `HmiListBox` | `SelectionMode` (NonExclusive/Exclusive) |
| `HmiCheckBoxGroup` | *(nothing but `EventHandlers` — it is `HmiSelectionGroupBase` under a different name/style)* |
| `HmiRadioButtonGroup` | *(nothing but `EventHandlers` — same)* |
| `HmiTouchArea` | `BackColor`, box, own `Authorization`/`RequireExplicitUnlock`; event set is `GestureDetected` only |

### 3b. Shapes (20)

| Type | What makes it that shape |
|---|---|
| `HmiShapeBase` *(base)* | rotation only |
| `HmiSurfaceShapeBase` *(base)* | `Left`, `Top`, `Width`, `Height` |
| `HmiCentricShapeBase` *(base)* | `CenterX`, `CenterY` (`Int32`) |
| `HmiCircularShapeBase` *(base)* | `Radius` (`UInt32`) |
| `HmiEllipticalShapeBase` *(base)* | `RadiusX`, `RadiusY` (`UInt32`) |
| `HmiPointBasedShapeBase` *(base)* | `Points` (`HmiPointComposition`, RO handle), `JoinType` (Round/Bevel/Miter) |
| `HmiRectangle` | area-fill set + `Corners` (`HmiCornersPart`) |
| `HmiLine` | `X1`, `Y1`, `X2`, `Y2` (`Int32`) + line set (`StartType`/`EndType`/`CapType`) |
| `HmiPolyline` | line set, drawn through `Points` |
| `HmiPolygon` | area-fill set, drawn through `Points` |
| `HmiCircle` | area-fill set on a `Radius` |
| `HmiCircleSegment` | `StartAngle`, `AngleRange` (`Int32`) — a filled pie slice |
| `HmiCircularArc` | line set + `StartAngle`, `AngleRange` — a stroked arc |
| `HmiEllipse` | area-fill set on `RadiusX`/`RadiusY` |
| `HmiEllipseSegment` | `StartAngle`, `AngleRange` |
| `HmiEllipticalArc` | line set + `StartAngle`, `AngleRange` |
| `HmiText` | `Text` (MLT, RO handle), `Font` (part), `ForeColor`, `HorizontalTextAlignment`, `VerticalTextAlignment` — **no background/border at all** |
| `HmiGraphicView` | `Graphic` (`String`), `GraphicStretchMode`, `Padding`, plus back-fill set |
| `HmiPoint` *(helper)* | `X`, `Y` (`Int32`, get/set), `Delete()` |
| `HmiPointComposition` *(helper)* | `Count`, `Item[Int32]`, `Create(Int32, Int32)` |

### 3c. Base (11)

| Type | What it adds |
|---|---|
| `HmiScreenItemBase` | the universal contract of §2a |
| `HmiSimpleScreenItemBase` | `Authorization`, `RequireExplicitUnlock`, `Opacity`, `ToolTipText` |
| `HmiScreenBase` | `Name`, `DisplayName` (MLT RO), `Delete()` — the screen, not an item |
| `HmiScreenItemBaseComposition` | `Count`, `Item[Int32]`, `Find(String)`, `Create<T>(String)`, `Create<T>(String, String)` |
| `HmiWindowBase` | `Caption` (part), `CaptionColor`, `Icon` (`String`), `WindowFlags` `[Flags]`, box |
| `HmiContainerBase` | `ContainedType` (`String`) — pairs with the 2-arg `Create<T>` overload |
| `HmiControlWindowBase` | `BackColor`, `StatusBar` (part), `ToolBar` (part) |
| `HmiCompanionBase` | `SnapToSourceControl` (`Boolean`) |
| `HmiTrendControlBase` | `AreaSpacing` (`UInt16`), `ExtendRulerToAxis`, `Legend` (part), `Online`, `ShiftAxes`, `ShowRuler` |
| `HmiCustomWidgetContainer` | `Interface` (`HmiCustomControlInterfaceComposition`), box, rotation, `VisualizeQuality` |
| `HmiCustomWebControlContainer` | `Interface`, own `Authorization`/`RequireExplicitUnlock`, `EventHandlers` |

---

## 4. Point-based shapes, and how points are added

**Exactly two concrete types are point-based**, both `sealed`, both deriving from
`Siemens.Engineering.HmiUnified.UI.Shapes.HmiPointBasedShapeBase`:

- `Siemens.Engineering.HmiUnified.UI.Shapes.HmiPolygon` — the **area** variant (`IHmiAreaFeature`:
  `BackColor`, `BorderColor`, `BorderWidth`, `BackFillPattern`, `FillLevel`, …)
- `Siemens.Engineering.HmiUnified.UI.Shapes.HmiPolyline` — the **line** variant (`IHmiLineFeature`:
  `LineColor`, `LineWidth`, `StartType`, `EndType`, `CapType`, `DashType`)

`HmiPointBasedShapeBase` declares only two members of its own:

```text
Points     Siemens.Engineering.HmiUnified.UI.Shapes.HmiPointComposition   get/---   (READ-ONLY handle)
JoinType   Siemens.Engineering.HmiUnified.UI.Enum.HmiLineJoinType         get/set   (Round=0, Bevel=1, Miter=2)
```

`Points` **cannot be assigned** — there is no setter, and no constructor for `HmiPointComposition`
(zero public constructors on every type in scope). Vertices exist only by asking the composition to make
them. The exact signature a generic builder needs:

```text
Siemens.Engineering.HmiUnified.UI.Shapes.HmiPointComposition

    Count                Int32                                        get/---
    IsReadOnly           Boolean                                      get/---
    Item[Int32]          Siemens.Engineering.HmiUnified.UI.Shapes.HmiPoint   get/---
    Parent               Siemens.Engineering.IEngineeringObject       get/---

    Create(Int32 x, Int32 y)              -> Siemens.Engineering.HmiUnified.UI.Shapes.HmiPoint
    Contains(HmiPoint item)               -> Boolean
    IndexOf(HmiPoint item)                -> Int32
    GetEnumerator()                       -> IEnumerator<HmiPoint>
```

`Create` is **non-generic**, takes two `Int32`s and nothing else — no name, no index, no overload. So:

```csharp
var poly = screen.ScreenItems.Create<HmiPolygon>("Poly_1");
poly.Points.Create(100, 100);
poly.Points.Create(200, 100);
poly.Points.Create(150, 200);      // triangle; order of calls == vertex order
poly.JoinType = HmiLineJoinType.Miter;
```

Consequences a generic builder must live with:

- **Append-only.** There is no `Insert`, no `Add(HmiPoint)`, no `Remove`, and no `Clear`. Vertex order
  is call order. To delete a vertex you call `Delete()` on the `HmiPoint` itself (`HmiPoint` declares
  `Delete()`); to re-order, UNKNOWN — reflection shows no reordering API, so the only visible route is
  delete-and-recreate.
- **Coordinates are `Int32`** on `HmiPoint.X`/`Y` — so negative vertex coordinates are representable,
  unlike `Width`/`Height` which are `UInt32`.
- **`HmiPoint` is not a `UIBase`.** It derives straight from `System.Object` and implements
  `IEngineeringObject` only. It has `GetAttribute`/`SetAttribute`/`GetAttributeInfos`, but **no
  `Dynamizations`** — a vertex cannot be tag-dynamized, and no `Validate()` of its own.
- **Whether points are absolute or relative to the shape's `Left`/`Top` is UNKNOWN from reflection.**
  `HmiPolygon`/`HmiPolyline` inherit `Left`/`Top`/`Width`/`Height` from `HmiSurfaceShapeBase` *and*
  carry `Points`; which is derived from which, and whether writing `Left` translates the vertices or
  merely re-frames them, cannot be determined without a live create. A builder should assume nothing
  and verify this first.
- **Minimum vertex count is UNKNOWN.** Reflection shows no constraint; `Validate()` is the only stated
  gate and it runs live.

---

## 5. Traps

**Get-only properties that look like data but are handles.** A large fraction of the "interesting"
properties are `get`-only object references, not values you assign. Assigning is a compile error in C#
and there is no obvious second route in the typed API — you mutate the returned object, or fall back to
`SetAttribute`. The full set in scope:

- **`MultilingualText` handles** — `HmiButton.Text`, `HmiButton.AlternateText`, `HmiLabel.Text`,
  `HmiText.Text`, `HmiSymbolicIOField.Text`, `HmiSimpleScreenItemBase.ToolTipText`,
  `HmiScreenBase.DisplayName`. `MultilingualText` itself exposes only `Items`
  (`MultilingualTextItemComposition`) plus the attribute API — **you cannot write `label.Text = "Pump 1"`**.
  Text is per-language items. This is the single most likely first surprise in a screen builder.
- **`*Part` handles** — `Font` (`HmiFontPart`), `Padding` (`HmiPaddingPart`), `Content`
  (`HmiContentPart`), `Corners` (`HmiCornersPart`), `Caption`/`Title`/`Label` (`HmiTextPart`),
  `StraightScale`, `CurvedScale`, `InputBehavior`, `StatusBar`, `ToolBar`, `Legend`. All get-only.
- **Compositions** — `Points`, `Thresholds`, `SelectionItems`, `Interface`, `EventHandlers`,
  `Dynamizations`, `PropertyEventHandlers`, `ScreenItems`. All get-only.

**`ProcessValue` is a `String`, and it is a tag *reference*, not a value.** On `HmiIOField`,
`HmiSymbolicIOField` and `HmiScaleWidgetBase` it is `String` get/set; on `HmiSelectionGroupBase` the
same-named property is **get-only**. Same name, different contract, one level apart in the tree.

**Signed/unsigned split on geometry.** `Left`/`Top` are `Int32` (negatives legal — items can hang off
the screen edge) but `Width`/`Height` are `UInt32`. `CenterX`/`CenterY` are `Int32`; `Radius`,
`RadiusX`, `RadiusY` are `UInt32`. `HmiPoint.X`/`Y` are `Int32`. In C# every literal size needs a `u`
suffix or a cast, and any arithmetic that could go negative on a `UInt32` wraps rather than throwing.

**Small integer types that are not `Int32`.** `TabIndex` `UInt16`; `HmiButton.HotKey` `UInt16`;
`SelectionItemHeight` `UInt16`; `HmiTrendControlBase.AreaSpacing` `UInt16`; `RotationAngle` **`Int16`**;
`BorderWidth`, `LineWidth`, `FillLevel` all **`Byte`** (so a border wider than 255 is unrepresentable,
and `FillLevel` being a `Byte` strongly suggests a 0–100 percentage, though the range is UNKNOWN from
reflection); `Opacity`, `RotationCenterX`, `RotationCenterY` are `Single`; `OriginValue` is `Double`.
`ScreenNumber` on `HmiScreen` is `UInt16`.

**Angle units are UNKNOWN.** `StartAngle`/`AngleRange` are `Int32` and `RotationAngle` is `Int16`;
reflection gives no unit. Degrees is the plausible reading but is not evidence.

**Nothing in scope is `abstract`, and nothing has a public constructor.** All 49 types report
`abstract=False` — including every `*Base` type — so a builder **cannot use `Type.IsAbstract` to filter
out the non-instantiable base classes**. And all 49 report zero public constructors, so `new` is never
the creation route. Creation is exclusively
`HmiScreenItemBaseComposition.Create<T>(string name)`; the second overload
`Create<T>(string name, string containedTypeValue)` pairs with `HmiContainerBase.ContainedType`.
Critically, **`Create<T>` carries no generic constraint at all** — reflection reports an empty
constraint set on `T` — so the compiler will happily accept `Create<HmiWidgetBase>` or even a wholly
unrelated type; rejection happens at runtime. The authoritative list of what `ScreenItems` will actually
accept is `GetCreationInfos`, i.e. `openness-cli hmi --schema`, not this catalogue.

**Type tests are ambiguous because behaviour classes are inherited, not composed.**
`HmiCircleSegment : HmiCircle`, `HmiEllipseSegment : HmiEllipse`, `HmiSlider : HmiBar`,
`HmiToggleSwitch : HmiButton`, `HmiTextBox : HmiLabel`, `HmiCompanionBase : HmiControlWindowBase`. A
walker that dispatches on `item is HmiCircle` will also match every `HmiCircleSegment`; `is HmiButton`
also matches every toggle switch. Dispatch on `GetType()` equality, or order the type tests
most-derived-first.

**Two text renderers with different families.** `HmiText` is a *shape* (no `BackColor`, no
`BorderColor`, no `TabIndex`-relevant chrome beyond the universal set) while `HmiLabel` is a *widget*
(has `BackColor`/`BorderColor`/`BorderWidth` and text wrapping). Picking the wrong one gives a
correct-looking item that cannot be styled the way the caller expected.

**`DashType` is declared twice.** It appears in both `IHmiAreaFeature` and `IHmiLineFeature`, so it is
re-declared on the area shapes and the line shapes independently. Harmless, but a member-counting pass
will see it more than once.

**`[Flags]` enums that read like scalars.** `HmiQuality`, `HmiWindowFlag`, `HmiPeakIndicator`,
`HmiScaleMode` (used as `HmiClock.DialMode`) are all `[Flags]`. `DialMode = Labels` and
`DialMode = Labels | Ticks` are both legal; a builder that treats these as single-choice will silently
under-configure. Values are in Appendix A.

**Colours are `System.Drawing.Color`**, so any consumer needs a `System.Drawing` reference — worth
noting alongside the existing `net48` constraint recorded in `docs/notes/openness-quirks.md`.

**`Validate()` is on `UIBase`, i.e. on the *item*, not only on the screen.** Every screen item can be
validated individually and returns `IList<HmiValidationResult>` where each result carries
`PropertyName`, `Errors` and `Warnings` — so a per-item validation gate is available, not just a
whole-screen one.

---

## Appendix A — raw reflection dump

Generated 2026-08-08 from `Siemens.Engineering.dll` v20.0.0.0. Declared members only (inherited members
are shown once, at the type that declares them — that is what makes the tree in §1 readable). `Equals`,
`GetHashCode` and `ToString` are omitted from every type since all 49 declare all three. Interface lists
are filtered to the `UI.Features.*` mixins. `*FactoryFacade` types are excluded throughout.

```text
===================================================================
NAMESPACE Siemens.Engineering.HmiUnified.UI.Base   (11 types, facades excluded)
===================================================================

HmiCompanionBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiCompanionBase
  base     : UI.Base.HmiControlWindowBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiWindowFeature
  declared properties:
    Parent                           SE.IEngineeringObject                          get/---
    SnapToSourceControl              Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiContainerBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiContainerBase
  base     : UI.Base.HmiWindowBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiWindowFeature
  declared properties:
    ContainedType                    String                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiControlWindowBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiControlWindowBase
  base     : UI.Base.HmiWindowBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiWindowFeature
  declared properties:
    BackColor                        Drawing.Color                                  get/set
    Parent                           SE.IEngineeringObject                          get/---
    StatusBar                        UI.Parts.HmiStatusBarPart                      get/---
    ToolBar                          UI.Parts.HmiToolBarPart                        get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiCustomWebControlContainer
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiCustomWebControlContainer
  base     : UI.Base.HmiContainerBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiWindowFeature
  declared properties:
    Authorization                    String                                         get/set
    EventHandlers                    UI.Events.HmiCustomWebControlContainerEventHandlerComposition get/---
    Interface                        UI.Parts.HmiCustomControlInterfaceComposition  get/---
    Parent                           SE.IEngineeringObject                          get/---
    RequireExplicitUnlock            Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiCustomWidgetContainer
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiCustomWidgetContainer
  base     : UI.Base.HmiSimpleScreenItemBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiCustomWidgetContainerEventHandlerComposition get/---
    Height                           UInt32                                         get/set
    Interface                        UI.Parts.HmiCustomControlInterfaceComposition  get/---
    Left                             Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
    RotationAngle                    Int16                                          get/set
    RotationCenterPlacement          UI.Enum.HmiRotationCenterPlacement             get/set
    RotationCenterX                  Single                                         get/set
    RotationCenterY                  Single                                         get/set
    Top                              Int32                                          get/set
    VisualizeQuality                 Boolean                                        get/set
    Width                            UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiScreenBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiScreenBase
  base     : UI.UIBase
  abstract=False  sealed=False  public ctors=0
  features : (none)
  declared properties:
    DisplayName                      SE.MultilingualText                            get/---
    Name                             String                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
  declared methods (Equals/GetHashCode/ToString omitted):
    Delete() -> Void

HmiScreenItemBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBase
  base     : UI.UIBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature
  declared properties:
    CurrentQuality                   UI.Enum.HmiQuality                             get/---
    Enabled                          Boolean                                        get/set
    Name                             String                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShowFocusVisual                  Boolean                                        get/set
    StyleItemClass                   String                                         get/set
    TabIndex                         UInt16                                         get/set
    Visible                          Boolean                                        get/set
  declared methods (Equals/GetHashCode/ToString omitted):
    Delete() -> Void

HmiScreenItemBaseComposition
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiScreenItemBaseComposition
  base     : Object
  abstract=False  sealed=True  public ctors=0
  features : (none)
  declared properties:
    Count                            Int32                                          get/---
    IsReadOnly                       Boolean                                        get/---
    Item[Int32]                      UI.Base.HmiScreenItemBase                      get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods (Equals/GetHashCode/ToString omitted):
    Contains(UI.Base.HmiScreenItemBase item) -> Boolean
    Create<T>(String name, String containedTypeValue) -> T
    Create<T>(String name) -> T
    Find(String name) -> UI.Base.HmiScreenItemBase
    GetEnumerator() -> IEnumerator<UI.Base.HmiScreenItemBase>
    IndexOf(UI.Base.HmiScreenItemBase item) -> Int32

HmiSimpleScreenItemBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiSimpleScreenItemBase
  base     : UI.Base.HmiScreenItemBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature
  declared properties:
    Authorization                    String                                         get/set
    Opacity                          Single                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
    RequireExplicitUnlock            Boolean                                        get/set
    ToolTipText                      SE.MultilingualText                            get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiTrendControlBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiTrendControlBase
  base     : UI.Base.HmiControlWindowBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiWindowFeature
  declared properties:
    AreaSpacing                      UInt16                                         get/set
    ExtendRulerToAxis                Boolean                                        get/set
    Legend                           UI.Parts.HmiLegendPart                         get/---
    Online                           Boolean                                        get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShiftAxes                        Boolean                                        get/set
    ShowRuler                        Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiWindowBase
  fullname : Siemens.Engineering.HmiUnified.UI.Base.HmiWindowBase
  base     : UI.Base.HmiScreenItemBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiWindowFeature
  declared properties:
    Caption                          UI.Parts.HmiTextPart                           get/---
    CaptionColor                     Drawing.Color                                  get/set
    Height                           UInt32                                         get/set
    Icon                             String                                         get/set
    Left                             Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
    Top                              Int32                                          get/set
    Width                            UInt32                                         get/set
    WindowFlags                      UI.Enum.HmiWindowFlag                          get/set
  declared methods: (only Equals/GetHashCode/ToString)

===================================================================
NAMESPACE Siemens.Engineering.HmiUnified.UI.Widgets   (18 types, facades excluded)
===================================================================

HmiBar
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiBar
  base     : UI.Widgets.HmiScaleWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    BarMode                          UI.Enum.HmiBarMode                             get/set
    EventHandlers                    UI.Events.HmiBarEventHandlerComposition        get/---
    Parent                           SE.IEngineeringObject                          get/---
    StraightScale                    UI.Parts.HmiStraightScalePart                  get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiButton
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiButton
  base     : UI.Widgets.HmiWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateGraphic                 String                                         get/set
    AlternateText                    SE.MultilingualText                            get/---
    Content                          UI.Parts.HmiContentPart                        get/---
    EventHandlers                    UI.Events.HmiButtonEventHandlerComposition     get/---
    Font                             UI.Parts.HmiFontPart                           get/---
    ForeColor                        Drawing.Color                                  get/set
    Graphic                          String                                         get/set
    HotKey                           UInt16                                         get/set
    Padding                          UI.Parts.HmiPaddingPart                        get/---
    Parent                           SE.IEngineeringObject                          get/---
    Text                             SE.MultilingualText                            get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiCheckBoxGroup
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiCheckBoxGroup
  base     : UI.Widgets.HmiSelectionGroupBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiCheckBoxGroupEventHandlerComposition get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiClock
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiClock
  base     : UI.Widgets.HmiWidgetBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    DialBackColor                    Drawing.Color                                  get/set
    DialLabelColor                   Drawing.Color                                  get/set
    DialLabelFont                    UI.Parts.HmiFontPart                           get/---
    DialMode                         UI.Enum.HmiScaleMode                           get/set
    DialTickColor                    Drawing.Color                                  get/set
    EventHandlers                    UI.Events.HmiClockEventHandlerComposition      get/---
    Parent                           SE.IEngineeringObject                          get/---
    ShowHours                        Boolean                                        get/set
    ShowMinutes                      Boolean                                        get/set
    ShowSeconds                      Boolean                                        get/set
    TimeSource                       String                                         get/---
    Title                            UI.Parts.HmiTextPart                           get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiGauge
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiGauge
  base     : UI.Widgets.HmiScaleWidgetBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    CurvedScale                      UI.Parts.HmiCurvedScalePart                    get/---
    EventHandlers                    UI.Events.HmiGaugeEventHandlerComposition      get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiIOField
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiIOField
  base     : UI.Widgets.HmiTextWidgetBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiMeasurementUnitFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiIOFieldEventHandlerComposition    get/---
    HorizontalTextAlignment          UI.Enum.HmiHorizontalAlignment                 get/set
    InputBehavior                    UI.Parts.HmiInputBehaviorPart                  get/---
    IOFieldType                      UI.Enum.HmiIOFieldType                         get/set
    OutputFormat                     String                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
    ProcessValue                     String                                         get/set
    TextTrimming                     UI.Enum.HmiTextTrimming                        get/set
    Thresholds                       UI.Parts.HmiThresholdPartComposition           get/---
    VerticalTextAlignment            UI.Enum.HmiVerticalAlignment                   get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiLabel
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiLabel
  base     : UI.Widgets.HmiTextWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    HorizontalTextAlignment          UI.Enum.HmiHorizontalAlignment                 get/set
    Parent                           SE.IEngineeringObject                          get/---
    Text                             SE.MultilingualText                            get/---
    TextTrimming                     UI.Enum.HmiTextTrimming                        get/set
    TextWrapping                     UI.Enum.HmiTextWrapping                        get/set
    VerticalTextAlignment            UI.Enum.HmiVerticalAlignment                   get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiListBox
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiListBox
  base     : UI.Widgets.HmiSelectionGroupBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiListBoxEventHandlerComposition    get/---
    Parent                           SE.IEngineeringObject                          get/---
    SelectionMode                    UI.Enum.HmiSelectionMode                       get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiRadioButtonGroup
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiRadioButtonGroup
  base     : UI.Widgets.HmiSelectionGroupBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiRadioButtonGroupEventHandlerComposition get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiScaleWidgetBase
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiScaleWidgetBase
  base     : UI.Widgets.HmiWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Font                             UI.Parts.HmiFontPart                           get/---
    Label                            UI.Parts.HmiTextPart                           get/---
    NormalRangeColor                 Drawing.Color                                  get/set
    OriginValue                      Double                                         get/set
    OutputFormat                     String                                         get/set
    Parent                           SE.IEngineeringObject                          get/---
    PeakIndicators                   UI.Enum.HmiPeakIndicator                       get/set
    ProcessValue                     String                                         get/set
    ProcessValueIndicatorBackColor   Drawing.Color                                  get/set
    ProcessValueIndicatorForeColor   Drawing.Color                                  get/set
    ProcessValueIndicatorMode        UI.Enum.HmiProcessIndicatorMode                get/set
    RelativeToOrigin                 Boolean                                        get/set
    ScaleBackColor                   Drawing.Color                                  get/set
    ScaleForeColor                   Drawing.Color                                  get/set
    ShowTrendIndicator               Boolean                                        get/set
    Thresholds                       UI.Parts.HmiThresholdPartComposition           get/---
    Title                            UI.Parts.HmiTextPart                           get/---
    TrendIndicatorColor              Drawing.Color                                  get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiSelectionGroupBase
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiSelectionGroupBase
  base     : UI.Widgets.HmiWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Content                          UI.Parts.HmiContentPart                        get/---
    Font                             UI.Parts.HmiFontPart                           get/---
    ForeColor                        Drawing.Color                                  get/set
    Padding                          UI.Parts.HmiPaddingPart                        get/---
    Parent                           SE.IEngineeringObject                          get/---
    ProcessValue                     String                                         get/---
    SelectionItemHeight              UInt16                                         get/set
    SelectionItems                   UI.Parts.HmiSelectionItemPartComposition       get/---
    SelectorPosition                 UI.Enum.HmiHorizontalAlignment                 get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiSlider
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiSlider
  base     : UI.Widgets.HmiBar
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiSliderEventHandlerComposition     get/---
    Parent                           SE.IEngineeringObject                          get/---
    ShowValue                        Boolean                                        get/set
    ThumbBackColor                   Drawing.Color                                  get/set
    ThumbForeColor                   Drawing.Color                                  get/set
    ValuePosition                    UI.Enum.HmiSimplePosition                      get/set
    WriteDuringChange                Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiSymbolicIOField
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiSymbolicIOField
  base     : UI.Widgets.HmiTextWidgetBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Content                          UI.Parts.HmiContentPart                        get/---
    EventHandlers                    UI.Events.HmiSymbolicIOFieldEventHandlerComposition get/---
    Graphic                          String                                         get/---
    IOFieldType                      UI.Enum.HmiIOFieldType                         get/set
    Parent                           SE.IEngineeringObject                          get/---
    ProcessValue                     String                                         get/set
    ResourceList                     String                                         get/set
    SelectedIndex                    Int32                                          get/---
    SelectionBackColor               Drawing.Color                                  get/set
    SelectionForeColor               Drawing.Color                                  get/set
    Text                             SE.MultilingualText                            get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiTextBox
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiTextBox
  base     : UI.Widgets.HmiLabel
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiTextBoxEventHandlerComposition    get/---
    Parent                           SE.IEngineeringObject                          get/---
    ReadOnly                         Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiTextWidgetBase
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiTextWidgetBase
  base     : UI.Widgets.HmiWidgetBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Font                             UI.Parts.HmiFontPart                           get/---
    ForeColor                        Drawing.Color                                  get/set
    Padding                          UI.Parts.HmiPaddingPart                        get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiToggleSwitch
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiToggleSwitch
  base     : UI.Widgets.HmiButton
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiToggleSwitchEventHandlerComposition get/---
    IsAlternateState                 Boolean                                        get/set
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiTouchArea
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiTouchArea
  base     : UI.Base.HmiScreenItemBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature
  declared properties:
    Authorization                    String                                         get/set
    BackColor                        Drawing.Color                                  get/set
    EventHandlers                    UI.Events.HmiTouchAreaEventHandlerComposition  get/---
    Height                           UInt32                                         get/set
    Left                             Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
    RequireExplicitUnlock            Boolean                                        get/set
    Top                              Int32                                          get/set
    Width                            UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiWidgetBase
  fullname : Siemens.Engineering.HmiUnified.UI.Widgets.HmiWidgetBase
  base     : UI.Base.HmiSimpleScreenItemBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    Height                           UInt32                                         get/set
    Left                             Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
    RotationAngle                    Int16                                          get/set
    RotationCenterPlacement          UI.Enum.HmiRotationCenterPlacement             get/set
    RotationCenterX                  Single                                         get/set
    RotationCenterY                  Single                                         get/set
    Top                              Int32                                          get/set
    VisualizeQuality                 Boolean                                        get/set
    Width                            UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

===================================================================
NAMESPACE Siemens.Engineering.HmiUnified.UI.Shapes   (20 types, facades excluded)
===================================================================

HmiCentricShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiCentricShapeBase
  base     : UI.Shapes.HmiShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    CenterX                          Int32                                          get/set
    CenterY                          Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiCircle
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiCircle
  base     : UI.Shapes.HmiCircularShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EventHandlers                    UI.Events.HmiCircleEventHandlerComposition     get/---
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShowFillLevel                    Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiCircleSegment
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiCircleSegment
  base     : UI.Shapes.HmiCircle
  abstract=False  sealed=True  public ctors=0
  features : IHmiArcFeature, IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AngleRange                       Int32                                          get/set
    EventHandlers                    UI.Events.HmiCircleSegmentEventHandlerComposition get/---
    Parent                           SE.IEngineeringObject                          get/---
    StartAngle                       Int32                                          get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiCircularArc
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiCircularArc
  base     : UI.Shapes.HmiCircularShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiArcFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiLineFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateLineColor               Drawing.Color                                  get/set
    AngleRange                       Int32                                          get/set
    CapType                          UI.Enum.HmiCapType                             get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EndType                          UI.Enum.HmiLineEndType                         get/set
    EventHandlers                    UI.Events.HmiCircularArcEventHandlerComposition get/---
    LineColor                        Drawing.Color                                  get/set
    LineWidth                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    StartAngle                       Int32                                          get/set
    StartType                        UI.Enum.HmiLineEndType                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiCircularShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiCircularShapeBase
  base     : UI.Shapes.HmiCentricShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Parent                           SE.IEngineeringObject                          get/---
    Radius                           UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiEllipse
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiEllipse
  base     : UI.Shapes.HmiEllipticalShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EventHandlers                    UI.Events.HmiEllipseEventHandlerComposition    get/---
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShowFillLevel                    Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiEllipseSegment
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiEllipseSegment
  base     : UI.Shapes.HmiEllipse
  abstract=False  sealed=True  public ctors=0
  features : IHmiArcFeature, IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AngleRange                       Int32                                          get/set
    EventHandlers                    UI.Events.HmiEllipseSegmentEventHandlerComposition get/---
    Parent                           SE.IEngineeringObject                          get/---
    StartAngle                       Int32                                          get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiEllipticalArc
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiEllipticalArc
  base     : UI.Shapes.HmiEllipticalShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiArcFeature, IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiLineFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateLineColor               Drawing.Color                                  get/set
    AngleRange                       Int32                                          get/set
    CapType                          UI.Enum.HmiCapType                             get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EndType                          UI.Enum.HmiLineEndType                         get/set
    EventHandlers                    UI.Events.HmiEllipticalArcEventHandlerComposition get/---
    LineColor                        Drawing.Color                                  get/set
    LineWidth                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    StartAngle                       Int32                                          get/set
    StartType                        UI.Enum.HmiLineEndType                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiEllipticalShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiEllipticalShapeBase
  base     : UI.Shapes.HmiCentricShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Parent                           SE.IEngineeringObject                          get/---
    RadiusX                          UInt32                                         get/set
    RadiusY                          UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiGraphicView
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiGraphicView
  base     : UI.Shapes.HmiSurfaceShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    EventHandlers                    UI.Events.HmiGraphicViewEventHandlerComposition get/---
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    Graphic                          String                                         get/set
    GraphicStretchMode               UI.Enum.HmiGraphicStretchMode                  get/set
    Padding                          UI.Parts.HmiPaddingPart                        get/---
    Parent                           SE.IEngineeringObject                          get/---
    ShowFillLevel                    Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiLine
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiLine
  base     : UI.Shapes.HmiSurfaceShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiLineFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateLineColor               Drawing.Color                                  get/set
    CapType                          UI.Enum.HmiCapType                             get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EndType                          UI.Enum.HmiLineEndType                         get/set
    EventHandlers                    UI.Events.HmiLineEventHandlerComposition       get/---
    LineColor                        Drawing.Color                                  get/set
    LineWidth                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    StartType                        UI.Enum.HmiLineEndType                         get/set
    X1                               Int32                                          get/set
    X2                               Int32                                          get/set
    Y1                               Int32                                          get/set
    Y2                               Int32                                          get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiPoint
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiPoint
  base     : Object
  abstract=False  sealed=True  public ctors=0
  features : (none)
  declared properties:
    Parent                           SE.IEngineeringObject                          get/---
    X                                Int32                                          get/set
    Y                                Int32                                          get/set
  declared methods (Equals/GetHashCode/ToString omitted):
    Delete() -> Void
    GetAttribute(String name) -> Object
    GetAttributeInfos() -> IList<SE.EngineeringAttributeInfo>
    GetAttributes(IEnumerable<String> names) -> IList<Object>
    GetAttributes(SE.AttributeAccessOptions attributeAccessOptions) -> IReadOnlyList<KeyValuePair<String,Object>>
    GetService<T>() -> T
    SetAttribute(String name, Object value) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes, SE.AttributeDelegate errorHandler) -> Void

HmiPointBasedShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiPointBasedShapeBase
  base     : UI.Shapes.HmiSurfaceShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    JoinType                         UI.Enum.HmiLineJoinType                        get/set
    Parent                           SE.IEngineeringObject                          get/---
    Points                           UI.Shapes.HmiPointComposition                  get/---
  declared methods: (only Equals/GetHashCode/ToString)

HmiPointComposition
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiPointComposition
  base     : Object
  abstract=False  sealed=True  public ctors=0
  features : (none)
  declared properties:
    Count                            Int32                                          get/---
    IsReadOnly                       Boolean                                        get/---
    Item[Int32]                      UI.Shapes.HmiPoint                             get/---
    Parent                           SE.IEngineeringObject                          get/---
  declared methods (Equals/GetHashCode/ToString omitted):
    Contains(UI.Shapes.HmiPoint item) -> Boolean
    Create(Int32 x, Int32 y) -> UI.Shapes.HmiPoint
    GetEnumerator() -> IEnumerator<UI.Shapes.HmiPoint>
    IndexOf(UI.Shapes.HmiPoint item) -> Int32

HmiPolygon
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiPolygon
  base     : UI.Shapes.HmiPointBasedShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EventHandlers                    UI.Events.HmiPolygonEventHandlerComposition    get/---
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShowFillLevel                    Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiPolyline
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiPolyline
  base     : UI.Shapes.HmiPointBasedShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiLineFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateLineColor               Drawing.Color                                  get/set
    CapType                          UI.Enum.HmiCapType                             get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EndType                          UI.Enum.HmiLineEndType                         get/set
    EventHandlers                    UI.Events.HmiPolylineEventHandlerComposition   get/---
    LineColor                        Drawing.Color                                  get/set
    LineWidth                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    StartType                        UI.Enum.HmiLineEndType                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiRectangle
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiRectangle
  base     : UI.Shapes.HmiSurfaceShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiAreaFeature, IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    Corners                          UI.Parts.HmiCornersPart                        get/---
    DashType                         UI.Enum.HmiDashType                            get/set
    EventHandlers                    UI.Events.HmiRectangleEventHandlerComposition  get/---
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    Parent                           SE.IEngineeringObject                          get/---
    ShowFillLevel                    Boolean                                        get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiShapeBase
  base     : UI.Base.HmiSimpleScreenItemBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Parent                           SE.IEngineeringObject                          get/---
    RotationAngle                    Int16                                          get/set
    RotationCenterPlacement          UI.Enum.HmiRotationCenterPlacement             get/set
    RotationCenterX                  Single                                         get/set
    RotationCenterY                  Single                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiSurfaceShapeBase
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiSurfaceShapeBase
  base     : UI.Shapes.HmiShapeBase
  abstract=False  sealed=False  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    Height                           UInt32                                         get/set
    Left                             Int32                                          get/set
    Parent                           SE.IEngineeringObject                          get/---
    Top                              Int32                                          get/set
    Width                            UInt32                                         get/set
  declared methods: (only Equals/GetHashCode/ToString)

HmiText
  fullname : Siemens.Engineering.HmiUnified.UI.Shapes.HmiText
  base     : UI.Shapes.HmiSurfaceShapeBase
  abstract=False  sealed=True  public ctors=0
  features : IHmiBasicScreenItemFeature, IHmiBoxFeature, IHmiIdentifierFeature, IHmiOperabilityFeature, IHmiRotationFeature
  declared properties:
    EventHandlers                    UI.Events.HmiTextEventHandlerComposition       get/---
    Font                             UI.Parts.HmiFontPart                           get/---
    ForeColor                        Drawing.Color                                  get/set
    HorizontalTextAlignment          UI.Enum.HmiHorizontalAlignment                 get/set
    Parent                           SE.IEngineeringObject                          get/---
    Text                             SE.MultilingualText                            get/---
    VerticalTextAlignment            UI.Enum.HmiVerticalAlignment                   get/set
  declared methods: (only Equals/GetHashCode/ToString)

===================================================================
ENUM VALUES for every enum reachable from a declared property above
===================================================================
Siemens.Engineering.HmiUnified.UI.Enum.HmiBarMode
    Segmented                    = 0
    Unicolor                     = 1
    SegmentedStatic              = 2
    UnicolorStatic               = 3

Siemens.Engineering.HmiUnified.UI.Enum.HmiCapType
    Square                       = 0
    Round                        = 1
    Flat                         = 2

Siemens.Engineering.HmiUnified.UI.Enum.HmiDashType
    Solid                        = 0
    Dash                         = 1
    Dot                          = 2
    DashDot                      = 3
    DashDotDot                   = 4

Siemens.Engineering.HmiUnified.UI.Enum.HmiFillDirection
    BottomToTop                  = 0
    TopToBottom                  = 1
    LeftToRight                  = 2
    RightToLeft                  = 3

Siemens.Engineering.HmiUnified.UI.Enum.HmiFillPattern
    Solid                        = 0
    Transparent                  = 1
    BackwardDiagonal             = 2
    Cross                        = 3
    DiagonalCross                = 4
    ForwardDiagonal              = 5
    Horizontal                   = 6
    Vertical                     = 7
    GradientHorizontal           = 8
    GradientVertical             = 9
    GradientForwardDiagonal      = 10
    GradientBackwardDiagonal     = 11
    GradientHorizontalTricolor   = 12
    GradientVerticalTricolor     = 13
    GradientForwardDiagonalTricolor = 14
    GradientBackwardDiagonalTricolor = 15

Siemens.Engineering.HmiUnified.UI.Enum.HmiGraphicStretchMode
    None                         = 0
    Fill                         = 1
    Uniform                      = 2
    UniformToFill                = 3
    Tiled                        = 4

Siemens.Engineering.HmiUnified.UI.Enum.HmiHorizontalAlignment
    Left                         = 0
    Center                       = 1
    Right                        = 2
    Stretch                      = 3

Siemens.Engineering.HmiUnified.UI.Enum.HmiIOFieldType
    Output                       = 0
    InputOutput                  = 1

Siemens.Engineering.HmiUnified.UI.Enum.HmiLineEndType
    Line                         = 0
    EmptyArrow                   = 1
    Arrow                        = 2
    ReversedArrow                = 3
    EmptyCircle                  = 4
    Circle                       = 5

Siemens.Engineering.HmiUnified.UI.Enum.HmiLineJoinType
    Round                        = 0
    Bevel                        = 1
    Miter                        = 2

Siemens.Engineering.HmiUnified.UI.Enum.HmiPeakIndicator  [Flags]
    None                         = 0
    Low                          = 1
    High                         = 2

Siemens.Engineering.HmiUnified.UI.Enum.HmiProcessIndicatorMode
    Bar                          = 0
    Indicator                    = 1
    DetailedIndicator            = 2
    BarWithDetailedIndicator     = 3

Siemens.Engineering.HmiUnified.UI.Enum.HmiQuality  [Flags]
    None                         = 0
    Bad                          = 1
    Uncertain                    = 2
    Good                         = 4
    UpperLimitViolation          = 8
    LowerLimitViolation          = 16

Siemens.Engineering.HmiUnified.UI.Enum.HmiRotationCenterPlacement
    AbsoluteFromCenter           = 0
    NormedFromCenter             = 1
    AbsoluteToContainer          = 2

Siemens.Engineering.HmiUnified.UI.Enum.HmiScaleMode  [Flags]
    None                         = 0
    Labels                       = 1
    Ticks                        = 2

Siemens.Engineering.HmiUnified.UI.Enum.HmiSelectionMode
    NonExclusive                 = 0
    Exclusive                    = 1

Siemens.Engineering.HmiUnified.UI.Enum.HmiSimplePosition
    LeftOrTop                    = 0
    RightOrBottom                = 1

Siemens.Engineering.HmiUnified.UI.Enum.HmiTextTrimming
    None                         = 0
    CharacterEllipsis            = 1

Siemens.Engineering.HmiUnified.UI.Enum.HmiTextWrapping
    NoWrap                       = 0
    WordWrap                     = 1

Siemens.Engineering.HmiUnified.UI.Enum.HmiVerticalAlignment
    Top                          = 0
    Center                       = 1
    Bottom                       = 2
    Stretch                      = 3

Siemens.Engineering.HmiUnified.UI.Enum.HmiWindowFlag  [Flags]
    None                         = 0
    ShowCaption                  = 1
    ShowBorder                   = 2
    AlwaysOnTop                  = 4
    CanSize                      = 8
    CanMove                      = 16
    CanMaximize                  = 32
    CanClose                     = 64
    AlwaysInParent               = 128

===================================================================
INHERITED-BUT-ESSENTIAL: UIBase (root of every UI object)
===================================================================
  base : System.Object
    Dynamizations                    UI.Dynamization.DynamizationBaseComposition    get/---
    Parent                           SE.IEngineeringObject                          get/---
    PropertyEventHandlers            UI.Events.PropertyEventHandlerComposition      get/---
    GetAttribute(String name) -> Object
    GetAttributeInfos() -> IList<SE.EngineeringAttributeInfo>
    GetAttributes(SE.AttributeAccessOptions attributeAccessOptions) -> IReadOnlyList<KeyValuePair<String,Object>>
    GetAttributes(IEnumerable<String> names) -> IList<Object>
    GetService() -> T
    SetAttribute(String name, Object value) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes, SE.AttributeDelegate errorHandler) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes) -> Void
    Validate() -> IList<HmiUnified.Common.HmiValidationResult>

===================================================================
CONTEXT (outside the 3 scoped namespaces, needed to use them)
===================================================================

Siemens.Engineering.HmiUnified.UI.Screens.HmiScreen   base=UI.Base.HmiScreenBase
    AlternateBackColor               Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BackGraphic                      String                                         get/set
    BackGraphicStretchMode           UI.Enum.HmiGraphicStretchMode                  get/set
    BackgroundFillMode               UI.Enum.HmiBackgroundFillMode                  get/set
    Enabled                          Boolean                                        get/set
    EventHandlers                    UI.Events.HmiScreenEventHandlerComposition     get/---
    Height                           UInt32                                         get/set
    HorizontalAlignment              UI.Enum.HmiHorizontalAlignment                 get/set
    Parent                           SE.IEngineeringObject                          get/---
    ScreenItems                      UI.Base.HmiScreenItemBaseComposition           get/---
    ScreenNumber                     UInt16                                         get/set
    VerticalAlignment                UI.Enum.HmiVerticalAlignment                   get/set
    Width                            UInt32                                         get/set
    ResizeScreen() -> Void

Siemens.Engineering.HmiUnified.Common.HmiValidationResult   base=Object
    Errors                           IEnumerable<String>                            get/---
    Parent                           SE.IEngineeringObject                          get/---
    PropertyName                     String                                         get/---
    Warnings                         IEnumerable<String>                            get/---
    GetAttribute(String name) -> Object
    GetAttributeInfos() -> IList<SE.EngineeringAttributeInfo>
    GetAttributes(IEnumerable<String> names) -> IList<Object>
    GetAttributes(SE.AttributeAccessOptions attributeAccessOptions) -> IReadOnlyList<KeyValuePair<String,Object>>
    SetAttribute(String name, Object value) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes, SE.AttributeDelegate errorHandler) -> Void
    SetAttributes(IEnumerable<KeyValuePair<String,Object>> attributes) -> Void

===================================================================
UI.Features interfaces (the mixin contracts the tree is assembled from)
===================================================================
IHmiArcFeature
    AngleRange                       Int32                                          get/set
    StartAngle                       Int32                                          get/set
IHmiAreaFeature
    AlternateBackColor               Drawing.Color                                  get/set
    AlternateBorderColor             Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BorderColor                      Drawing.Color                                  get/set
    BorderWidth                      Byte                                           get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    FillDirection                    UI.Enum.HmiFillDirection                       get/set
    FillLevel                        Byte                                           get/set
    ShowFillLevel                    Boolean                                        get/set
IHmiAxisFeature
    AxisColor                        Drawing.Color                                  get/set
    DisplayName                      SE.MultilingualText                            get/---
    Name                             String                                         get/set
    Visible                          Boolean                                        get/set
IHmiBasicScreenFeature
    AlternateBackColor               Drawing.Color                                  get/set
    BackColor                        Drawing.Color                                  get/set
    BackFillPattern                  UI.Enum.HmiFillPattern                         get/set
    BackGraphic                      String                                         get/set
    BackGraphicStretchMode           UI.Enum.HmiGraphicStretchMode                  get/set
    BackgroundFillMode               UI.Enum.HmiBackgroundFillMode                  get/set
    Enabled                          Boolean                                        get/set
    Height                           UInt32                                         get/set
    HorizontalAlignment              UI.Enum.HmiHorizontalAlignment                 get/set
    VerticalAlignment                UI.Enum.HmiVerticalAlignment                   get/set
    Width                            UInt32                                         get/set
IHmiBasicScreenItemFeature
    Enabled                          Boolean                                        get/set
    Name                             String                                         get/set
    StyleItemClass                   String                                         get/set
    TabIndex                         UInt16                                         get/set
    Visible                          Boolean                                        get/set
IHmiBoxFeature
    Height                           UInt32                                         get/set
    Left                             Int32                                          get/set
    Top                              Int32                                          get/set
    Width                            UInt32                                         get/set
IHmiIdentifierFeature
    (marker interface - no members)
IHmiLineFeature
    AlternateLineColor               Drawing.Color                                  get/set
    CapType                          UI.Enum.HmiCapType                             get/set
    DashType                         UI.Enum.HmiDashType                            get/set
    EndType                          UI.Enum.HmiLineEndType                         get/set
    LineColor                        Drawing.Color                                  get/set
    LineWidth                        Byte                                           get/set
    StartType                        UI.Enum.HmiLineEndType                         get/set
IHmiMeasurementUnitFeature
    (marker interface - no members)
IHmiOperabilityFeature
    Authorization                    String                                         get/set
    RequireExplicitUnlock            Boolean                                        get/set
IHmiRotationFeature
    RotationAngle                    Int16                                          get/set
    RotationCenterPlacement          UI.Enum.HmiRotationCenterPlacement             get/set
    RotationCenterX                  Single                                         get/set
    RotationCenterY                  Single                                         get/set
IHmiScaleFeature
    AutoScaling                      Boolean                                        get/set
    LabelColor                       Drawing.Color                                  get/set
    ScaleMode                        UI.Enum.HmiScaleMode                           get/set
    TickColor                        Drawing.Color                                  get/set
IHmiScreenWindowFeature
    Adaption                         UI.Enum.HmiScreenWindowAdaption                get/set
    CurrentZoomFactor                Double                                         get/set
    HorizontalScrollBarPosition      Int32                                          get/set
    HorizontalScrollBarVisibility    UI.Enum.HmiScrollBarVisibility                 get/set
    InteractiveZooming               Boolean                                        get/set
    Screen                           String                                         get/set
    ScreenName                       String                                         get/---
    ScreenNumber                     UInt16                                         get/---
    System                           Byte                                           get/set
    VerticalScrollBarPosition        Int32                                          get/set
    VerticalScrollBarVisibility      UI.Enum.HmiScrollBarVisibility                 get/set
IHmiTimeRangeFeature
    BeginTime                        DateTime                                       get/set
    EndTime                          DateTime                                       get/set
    PointCount                       Int32                                          get/set
    RangeType                        UI.Enum.HmiTimeRangeType                       get/set
    TimeRangeBase                    UI.Enum.HmiTimeRangeBase                       get/set
    TimeRangeFactor                  Int32                                          get/set
IHmiWindowFeature
    Caption                          UI.Parts.HmiTextPart                           get/---
    CaptionColor                     Drawing.Color                                  get/set
    Icon                             String                                         get/set
    WindowFlags                      UI.Enum.HmiWindowFlag                          get/set
```
