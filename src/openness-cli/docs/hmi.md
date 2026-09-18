# openness-cli — HMI commands

The read-only HMI walk, the two write probes, the graphics store, and the classic tag-table and text-list document formats. HMI *engineering* remains a non-goal (`docs/10-non-goals.md`); these commands exist to observe and to probe.

> Split out of `src/openness-cli/README.md` on 2026-09-17. That file remains the index and the invocation contract.

## `hmi` — the read-only HMI walk

### `--scripts`: the behaviour is in the handlers (migrated from CLAUDE.md 2026-08-21)

`--scripts` dumps the FULL body of every event handler, not the one-line preview. **Not cosmetic:**
on a real Unified project the behaviour is almost ENTIRELY in these handlers — 274 of them in the
reference project, against ZERO script modules — including how faceplates are actually used, which
is `UI.OpenFaceplateInPopup("<Type>_V_0_0_11", title, {IO:{Tag:"<tag>"}})` from a hot-zone polygon,
**NOT** a container on a screen (the reference project has zero faceplate containers across 49
screens / 1,404 items).

Until this flag existed the preview took the first LINE rather than the first NON-BLANK line, so all
274 reported "there is a script" and displayed nothing — the walker described the skeleton and
omitted the animal, in every read this project had ever done.

**`--schema` is the substitute for a screen XML: WinCC Unified has NO screen export at all**
(verified four ways — API sweep, project folder, compiled runtime, third-party docs;
`docs/notes/openness-hmi-api-survey.md` §8), so there is no document to decode. Instead Openness
self-describes via `GetAttributeInfos`/`GetCreationInfos`, and `--schema` dumps the creatable item
types plus every attribute's access mode and **create-relevance** (`Mandatory`/`Relevant`/`None`) —
which an exported example could never tell you. Classic screens DO export as SimaticML; Unified
screens do not exist as any file.

Every other subcommand is PLC-only *by construction*: each device walk filters
`SoftwareContainer.Software is PlcSoftware`, so an HMI device was previously invisible to this tool
rather than merely unsupported. `hmi` is the one command that matches the other two software types.

**It is strictly read-only.** No `Create`, no `SetAttribute`, no import, no compile — there is no
code path in the walker that writes. HMI *engineering* remains a non-goal (`docs/10-non-goals.md`);
this exists to observe, and it does not open a capability.

The output shape is decided by the API, not by preference — the two HMI families are disjoint object
models with opposite strengths (`docs/notes/openness-hmi-api-survey.md`):

| | Classic (`HmiTarget`) | Unified (`HmiSoftware`) |
|---|---|---|
| Screens | names only | name, number, size, item count |
| Screen items | **impossible** — `Screen` has no `ScreenItems` property at all | full typed tree |
| Dynamizations | n/a | per-property, with tag + PLC tag |

So on a classic device the report says outright that Openness exposes no screen contents. That is
the API's ceiling, not a gap in this walker, and the report states it rather than leaving an empty
list to be misread as "this screen is empty".

Two deliberate cost controls, because a Unified screen item is many slow property reads:

- **Without `--screen`, screens are summarised** (name/size/item count) and no items are read.
  `--screen <name>` reads that screen's items; `--screen *` reads every screen's.
- **`--max-items <n>`** (default 500) caps items read per screen. Truncation is always visible —
  the true `ItemCount` is reported alongside the number actually shown, so a cap can never read as a
  complete listing.

Geometry is read through generic attribute access rather than a cast per concrete item type: there
are ~50 of them across widgets/shapes/controls, they do not share one geometry base, and an item
type a future TIA version adds still reports its position instead of throwing. Every read in the
walker is individually defensive — these are the first calls this project makes into the HMI half of
the API, and one property that throws on one item type must not lose the other 47 screens.

Exits `0` when a project has no HMI device at all: "this project has none" is a legitimate answer to
the question the command asks, not a failure.

### `--schema` — the substitute for a screen XML

**WinCC Unified has no screen export**, so there is no XML or JSON document of a screen to read
anywhere: not in the API, not in the project folder, not in the compiled runtime image (which stores
screens as undocumented binary `.rdf`). Verified four ways in
`docs/notes/openness-hmi-api-survey.md` §8.

What Openness offers instead is self-description, and for authoring it is strictly better than a
sample document:

```
openness-cli hmi <project> --schema            # every screen
openness-cli hmi <project> --schema --screen X # one screen's item types
```

It reports the types `ScreenItems` will accept (`GetCreationInfos`), and for each item type observed,
every attribute (`GetAttributeInfos`) with its **access mode**, **create-relevance**, type and a
sample value — sorted **Mandatory → Relevant → the rest**, which is the order someone writing a
screen needs them in.

`CreateRelevance` (`None | Relevant | Mandatory`) is the reason this beats an exported example: it
states which attributes *must* be supplied at creation. An XML sample only ever shows what one screen
happened to set, never what the next one is required to set.

Schema is a Unified-only concept — classic exposes no screen items, so there is no item schema to
report, and `--schema` says so rather than printing an empty document.

## `hmi-create-screen` — the only HMI command that writes

Everything else under `hmi` is read-only by construction. This one is deliberately a **separate
subcommand** rather than a flag on the walker, so that "walk the HMI" can never become "modify the
HMI" by a mistyped argument.

```
openness-cli hmi-create-screen <project> --name <name> [--width <n>] [--height <n>] [--item <TypeName>]... --yes
```

- **Gated on `--yes`**, like `delete` — the two mutating commands in this tool behave the same way.
  Without it, the command prints what it *would* create and exits `10 = NotConfirmed`. That refusal
  is answered from the arguments alone: **it never contacts Portal**, so a dry run is instant and
  cannot fail on a connect.
- **Creates, never overwrites.** An existing screen of that name is a hard error, not a merge.
- **Refuses an ambiguous device.** More than one Unified HMI is an error rather than a first-match
  guess — writing to the wrong panel is not undone by re-reading.
- **Unified only.** Classic exposes no screen-item model, so there is nothing to create into.
- `--item` takes CLR type names from `hmi --schema`'s own creatable list, repeatable. Item creation
  needs only a name (see `--schema`: nothing is `Mandatory`, only `Name` is `Relevant`), so items are
  created bare and positioned/styled afterwards through attributes.
- **Runs `Validate()` and reports it explicitly**, including "ran, returned no errors and no
  warnings" — because silence and "never ran" would otherwise look identical. Validation errors exit
  `8 = CompileFailed`: a failed gate, not a successful create with commentary. This is the HMI
  analogue of hard rule 4.
- **Saves.** `Project.Save()` is called, and the result says whether it was — an unsaved create lives
  only in the Portal process holding it and vanishes with that process.

**Scope note:** HMI engineering is still a non-goal (`docs/10-non-goals.md`, "revisit only via ADR").
This exists as a capability *probe* — the cheapest way to answer the survey's own open question of
whether `Validate()` does anything — not as an HMI authoring capability.

## `hmi-edit-screen` — modify an existing screen, and attach events

### Dynamization kinds, nested paths, and one forbidden entry type (migrated from CLAUDE.md 2026-08-21)

`hmi-edit-screen <project> --name <name> [--set <Target>.<Attr>=<Value>]... [--event <Target>:<EventType>[=<script>]]... --yes`

`Target` is an item name or the literal `Screen`. Values are coerced from the target's own
`GetAttributeInfos` (a `UInt32` won't take a string) and the applied value is reported WITH its
converted type so a silent coercion is visible. Read-only attributes are refused; unknown targets,
attributes and events are hard errors, never no-ops.

**Events are enum-keyed per item type and touch-first — there is NO `Click`:** `Tapped`,
`ContextTapped`, `KeyDown`, `KeyUp` (+ `Down`/`Up` on buttons); screens use `Loaded`/`Unloaded`;
controls use `Initialized`/`CommandFired`.

**A dynamization kind is gated on the TARGET PROPERTY'S TYPE, not on the kind** (P7, 2026-08-09):
`Tag`/`Script`/`Expression` bind to anything; **`Flashing` only to colour properties**;
**`ResourceList` only to text properties**; `TagParameter` refuses outside a faceplate. A refusal
names no reason, so **vary the target before concluding a kind is unsupported** — this project read
three such refusals as an API limit and was wrong.

`--set` takes nested paths (`Item.Prop.ValueConverter.MappingTable.Entries[0].Flashing`) and `--map`
builds mapping-table entries. **Flashing is NOT limited to colour properties** — that limit belongs
to `FlashingDynamization`; a `TagDynamization` mapping-table entry carries its own `Flashing` flag
and works anywhere a tag binds, including a Boolean (P8).

🔴 **`MappingTableEntrySimple` CRASHES TIA PORTAL** — three isolated occurrences with controls.
Treat it as forbidden; use **`MappingTableEntryRange`**. Bitmask entries come only from
`Create(BitDynamizationType)` and cannot be deleted.

```
openness-cli hmi-edit-screen <project> --name <screen> [--set <Target>.<Attr>=<Value>]... [--event <Target>:<EventType>[=<script>]]... --yes
```

`Target` is an item name, or the literal `Screen` for the screen itself. Same `--yes` gate as
`hmi-create-screen`, and the same no-Portal dry run — which here prints the **whole change list**, so
the refusal is a reviewable plan rather than a count.

**Attribute values are coerced from the API's own schema**, not guessed: the target's
`GetAttributeInfos()` supplies the declared type, and the value is enum-parsed or
`Convert.ChangeType`-d into it (`Width` is a `UInt32` and will not accept a string). The applied
change is reported with the converted value *and its type*, so a silent coercion is visible. A
read-only attribute is **refused**, with a message pointing out that read-only sub-parts (`Font`,
`Padding`, `InputBehavior`, `ToolTipText`) are configured by reaching into the object they return
rather than by assignment.

**Unknown targets, attributes and event names are hard errors**, never no-ops — a skipped edit and a
successful one look identical in the output otherwise.

### Targets NEST — reaching a property OF a dynamization

`Target` was an item name only until 2026-08-09, which meant a dynamization could be *created* and
not *configured*: `--set Rect_1.BackColor.FlashingRate=Fast` was rejected because `Rect_1.BackColor`
is not an item (`openness-hmi-write-api.md` §4m). A target is now a **path**, walked one segment at a
time:

```
Rect_1.BackColor.FlashingRate=Fast
Rect_1.BackColor.ValueConverter.MappingTable.ConditionType=Range
Rect_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing=True
```

Each step resolves a **dynamization on that property name first**, then a CLR property of that name;
`[n]` indexes into a composition, which is not itself an engineering object and so cannot be a target
on its own. The split is unchanged — last `.` before the first `=` — so the existing grammar is
untouched and a one-segment target still means an item. A step that does not resolve is a hard error
naming the segment and why (`HmiTargetPathNotResolvableException`), never a silent no-op.

### Explicitly-typed values

A value may carry a CLR type tag: `color:#FF0000`, `color:#80FF0000` (ARGB), `int:`, `uint:`,
`long:`, `ulong:`, `double:`, `bool:`, `str:`. Untagged values are coerced from the target's own
schema exactly as before.

The tag exists because some members are declared `object` and the metamodel then says nothing about
what they want — a mapping-table entry's `Value` is the case that forced it. Colours also need it in
spirit: every HMI colour is a `System.Drawing.Color`, which `Convert.ChangeType` cannot produce from
a string, so a declared-`Color` attribute is parsed rather than converted (`#RRGGBB`, `#AARRGGBB`, or
an HTML colour name).

When an attribute is absent from `GetAttributeInfos()` the write falls back to the CLR property and
**says so** in the result (`[via CLR property, not GetAttributeInfos; read back: …]`). The two are
not interchangeable and reporting them identically would describe intent rather than outcome.

## `--map` / `--map-clear` — mapping tables, the second route to flashing

```
openness-cli hmi-edit-screen <project> --name <screen> [--map-clear <Target>.<Property>]... [--map <Target>.<Property>=<EntrySpec>]... --yes
```

Drives `TagDynamization -> ValueConverter -> MappingTable -> Entries` — the value-to-colour-and-flash
mechanism a real alarm display is built from. It hangs off `TagDynamization`, the one dynamization
kind that is not gated on the target property's type, so it reaches properties where
`FlashingDynamization` refuses.

`<EntrySpec>` is `<EntryType>[;<Attr>=<Value>]...`:

| EntryType | Creates via |
|---|---|
| `Simple` / `Range` / `Bitmask` / `Base` | `Entries.Create<T>()` — **no arguments**, unlike every other composition in this API |
| `bits:SingleBit` / `bits:MultiBit` | the non-generic `Entries.Create(BitDynamizationType)`, which returns an `IList` — a whole SET of bitmask entries in one call |

```
--map "Rect_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True;FlashingRate=Fast"
```

Every entry created is **read back field by field** and reported with the CLR type stored, because
`Value`/`AlternateValue` are declared `object` and nothing in the metamodel says what they accept —
what came back is the only evidence of what went in.

`--map-clear` deletes every entry on that mapping table and is applied **before** `--map`, so a
command carrying both is re-runnable: Openness has no transaction and the composition has no upsert,
so re-running a bare `--map` would stack duplicates.

The property must already carry a **tag binding** (`--bind`): only a `TagDynamization` has a
`ValueConverter`. Asking for a mapping table on any other kind, or on an unbound property, is a hard
error that names the reason.

`openness-cli hmi --screen <name>` reads mapping tables back — `ConditionType`, the formula flag, and
every entry with its stored types. Unified has no screen export, so a fresh-process read of the live
model is the only independent evidence a write took.

### Events are enum-keyed, and the vocabulary is touch-first

Each concrete item type has its own `EventHandlers` composition whose `Create()` takes *that type's*
event enum — `HmiButtonEventType`, `HmiRectangleEventType`, and so on for ~40 types. There is no
shared base, so both reading and writing bind reflectively rather than through a hand-written switch.

**There is no `Click`.** Interactive items expose `Tapped`, `ContextTapped`, `KeyDown`, `KeyUp`
(buttons and toggle switches add `Down`/`Up`; toggles add `StateChanged`); screens expose `Loaded`,
`Unloaded`, `Tapped`, `ContextTapped`; controls expose `Initialized` and `CommandFired`; a touch area
exposes only `GestureDetected`. An invalid name is rejected with the valid list for that type.

Each handler carries a `Script` (`ScriptCode`, `Async`, and a `SyntaxCheck()` this tool does not yet
call). `--event Target:Type` with no `=script` creates an empty handler, which is legitimate.

`openness-cli hmi --screen <name>` now **reads events back** — closing the walker's one previously
misleading gap, where a button reporting no dynamizations read as "not bound" when it meant "not
looked at".

## `graphics` — the project-level picture store (2026-08-17)

```
openness-cli graphics <project> [--list]
openness-cli graphics <project> --inspect <name>
openness-cli graphics <project> --export <name> --out <path> [--export-options <name>]
openness-cli graphics <project> --import <file>... [--overwrite]
```

`Project.Graphics` is a `MultiLingualGraphicComposition` and it is **project-scoped, not
device-scoped** — one picture store shared by every HMI device, which is why this command takes no
`--device`. `HmiTarget.GraphicLists` is a different object entirely (a state→picture *mapping*, not
the picture store) and nothing here touches it.

### The typed object is `Name` and nothing else — the document is the only surface

`--inspect` on a real graphic reports **one** attribute (`Name`), **zero** compositions, and a CLR
surface of `Name`, `Parent`, `Delete`, `Export`, `GetAttribute*`/`SetAttribute*`. **The image bytes
are not reachable through the object model at all.** The exported document, by contrast, carries
`DefaultDithering`, `DefaultImageStream`, `DefaultSmoothness` and `Name` — so the SimaticML document
is strictly richer than the API, exactly the relationship SimaticML has to a classic screen.

`MultiLingualGraphicComposition` has **no `Create`** (its members are `Import`, `Find`, `Contains`,
`IndexOf`, `Count`, the indexer and the enumerator). **Import is the only route in.**

### `Export` produces a DOCUMENT plus a sidecar folder holding the real image file

Measured on an existing graphic. `--export X --out X.xml` writes **two** things:

```
X.xml
X files\DefaultImageStream.png      <- a real PNG, 96x96 8-bit RGBA
```

and the document references it by relative path rather than embedding it:

```xml
<Hmi.Globalization.MultiLingualGraphic ID="0">
  <AttributeList>
    <DefaultDithering>false</DefaultDithering>
    <DefaultImageStream external="path">X files\DefaultImageStream.png</DefaultImageStream>
    <DefaultSmoothness>false</DefaultSmoothness>
    <Name>X</Name>
  </AttributeList>
</Hmi.Globalization.MultiLingualGraphic>
```

**A caller that copies only the `.xml` has copied nothing.** The sidecar folder is the picture.

### `Import` wants that document, refuses a bare image, and VALIDATES the payload

Handing it a raw `.png`:

```
Invalid XML encountered while reading Simatic ML file:
Invalid character in the given encoding. Line 1, position 1.
```

Handing it a well-formed document whose sidecar is not a decodable image (prose, or a zero-byte
file):

```
The external file "...\DefaultImageStream.png" is corrupt or invalid.
```

So the store is **not** a blind blob store — it decodes what it is given. And the check is keyed on
the **declared extension**: SVG bytes named `DefaultImageStream.png` are refused as corrupt, while
the same bytes named `.svg` are accepted.

### Nine formats accepted, all round-tripping byte-identical

PNG, BMP, JPG, GIF, ICO, TIFF, WMF, EMF **and SVG** each imported (exit 0), and each exported back
with a **SHA-256-identical** payload. The store preserves the original encoding rather than
normalising to one — the read-back extension follows the source (`.tif` came back as `.tiff`, the
only name change observed).

Because the store round-trips bytes, **a green `--import` is not on its own evidence that the panel
can render the format.** That question belongs to `hmi-compile`, and the compiler does answer it:
a `GraphicView` naming a picture that does not exist fails the HMI compile by name —

```
[Error] ZZ_<screen>: 
[Error] GV_<item>: 
[Error] The graphic for the 'GV_<item>' screen object is invalid.
```

— which is the negative control that makes a clean compile of the same screen mean something.

### Exit codes — `graphics` only

*(Renamed 2026-08-21. This heading was `### Exit codes`, colliding with the file-wide `## Exit
codes` table near the end — the same slug, and the file's own synopsis says "see 'Exit codes' at
the end of this file", which a by-name search resolved to whichever it hit first. The two are not
the same list.)*

`--import` failures surface as `CommandError` (7) carrying the **whole** exception chain verbatim,
because on a capability question the refusal text *is* the result. A `--import` that reports zero
graphics also exits 7 rather than 0: empty is not clean.

## Classic HMI tags and text lists (2026-08-18)

```
openness-cli export <project> --hmitagtable <name> --out <path> [--device <name>]
openness-cli export <project> --textlist    <name> --out <path> [--device <name>]
openness-cli import <project> --hmitags   <files...> [--device <name>]
openness-cli import <project> --textlists  <files...> [--device <name>]
```

`--tagtable` is the **PLC** tag table. `--hmitagtable` is the **classic HMI** one — a different
composition on a different device — and the two are deliberately not merged behind one flag.

### Import is the only route in — again

Reflected on the installed V20 `Siemens.Engineering.dll`:

| type | route in | route out |
|---|---|---|
| `Hmi.Tag.TagTableComposition` | `Import(FileInfo, ImportOptions)`, `CreateFrom(MasterCopy)` | — |
| `Hmi.Tag.TagTable` | — | `Export(FileInfo, ExportOptions)`, `.Tags` |
| `Hmi.Tag.TagComposition` | `Import(FileInfo, ImportOptions)`, `CreateFrom(MasterCopy)` | — |
| `Hmi.TextGraphicList.TextListComposition` | `Import(FileInfo, ImportOptions)` | — |
| `Hmi.TextGraphicList.TextList` | — | `Export(FileInfo, ExportOptions)` |

🔴 **Not one of them has `Create`.** Same shape as `ScreenComposition` and
`MultiLingualGraphicComposition`: **the document is richer than the API.** A classic `TextList`
object exposes `Name`, `Parent`, `Export`, `Delete` and the attribute bag and **nothing about its
entries** — the entries exist for a reader *only* inside the exported document, which is why the
import report prints `members NOT EXPOSED BY THE API` rather than `0`.

Both types live in **`Siemens.Engineering.dll`**, not `Siemens.Engineering.Hmi.dll` — loading the
wrong assembly reports all of them absent.

**Classic only.** The Unified compositions are separate types with a different contract:
`HmiUnified.HmiTags.HmiTagTableComposition` *does* have `Create(string)` (that is what
`hmi-create-tag` uses), and `HmiUnified.TextGraphicList.HmiTextListComposition`'s `Import`/`Export`
take a `DirectoryInfo` + filename rather than a `FileInfo`. A Unified hit is refused **by name**, not
returned as an empty result — the object exists, this route does not reach it.

`--hmitagtable`/`--textlist` are also the only **enumeration** there is: nothing in this CLI lists
classic HMI tag tables, so a name that does not resolve is refused with `PRESENT: <every name>`.
Not decoration — TIA's own default table is called `Default tag table`, spaces included, which is not
a name anyone guesses.

### The tag-table document

`Hmi.Tag.TagTable` → `ObjectList` of `Hmi.Tag.Tag`. A tag's **datatype, connection, address and
acquisition cycle are NOT attributes** — they are `LinkList` entries naming another object:

```xml
<Hmi.Tag.TagTable ID="0">
  <AttributeList>
    <Name>MyTable</Name>
  </AttributeList>
  <ObjectList>
    <Hmi.Tag.Tag ID="1" CompositionName="Tags">
      <AttributeList>
        <AcquisitionTriggerMode>Visible</AcquisitionTriggerMode>
        <AddressAccessMode>Symbolic</AddressAccessMode>
        <Coding>IEEE754Float</Coding>          <!-- Binary for Bool/Int/String -->
        <ConfirmationType>None</ConfirmationType>
        <GmpRelevant>false</GmpRelevant>
        <JobNumber>0</JobNumber>
        <Length>4</Length>                     <!-- bytes: Bool 1, Int 2, Real 4, String 254 -->
        <LinearScaling>false</LinearScaling>
        <LogicalAddress />                     <!-- empty under AddressAccessMode Symbolic -->
        <MandatoryCommenting>false</MandatoryCommenting>
        <Name>MyTag</Name>
        <Persistency>false</Persistency>
        <QualityCode>false</QualityCode>
        <ScalingHmiHigh>100</ScalingHmiHigh>
        <ScalingHmiLow>0</ScalingHmiLow>
        <ScalingPlcHigh>10</ScalingPlcHigh>
        <ScalingPlcLow>0</ScalingPlcLow>
        <StartValue />
        <SubstituteValue />
        <SubstituteValueUsage>None</SubstituteValueUsage>
        <Synchronization>false</Synchronization>
        <UpdateMode>ProjectWide</UpdateMode>
        <UseMultiplexing>false</UseMultiplexing>
      </AttributeList>
      <LinkList>
        <AcquisitionCycle TargetID="@OpenLink"><Name>1 s</Name></AcquisitionCycle>
        <Connection      TargetID="@OpenLink"><Name>HMI_Connection_1</Name></Connection>
        <ControllerTag   TargetID="@OpenLink"><Name>MyDb.MyMember</Name></ControllerTag>
        <DataType        TargetID="@OpenLink"><Name>Real</Name></DataType>
        <HmiDataType     TargetID="@OpenLink"><Name>Real</Name></HmiDataType>
      </LinkList>
      <ObjectList>
        <!-- Comment, DisplayName and TagValue, each a MultilingualText/MultilingualTextItem
             carrying <Culture> and <Text>. Present even when empty. -->
      </ObjectList>
    </Hmi.Tag.Tag>
  </ObjectList>
</Hmi.Tag.TagTable>
```

⚠️ **`DataType` and `HmiDataType` are two different links and they can DISAGREE.** Measured: a tag
whose PLC-side `DataType` is `String` carries an HMI-side `HmiDataType` of `WString`. A generator
that writes one value into both is wrong for that case.

`TargetID="@OpenLink"` means *resolve this by name at import time*. So a generated tag table must
name a connection, an acquisition cycle and a controller tag **that already exist** — none of them is
created by this document.

### The text-list document

`Hmi.TextGraphicList.TextList` → `ObjectList` of `Hmi.TextGraphicList.TextListEntry`. The
value→text mapping is `From`/`To` (a **range**, equal for a single value) plus a `MultilingualText`:

```xml
<Hmi.TextGraphicList.TextList ID="0">
  <AttributeList>
    <ListRange>Decimal</ListRange>            <!-- Decimal | Bit | BitNumber -->
    <Name>MyList</Name>
  </AttributeList>
  <ObjectList>
    <MultilingualText ID="1" CompositionName="Comment"> ... </MultilingualText>
    <Hmi.TextGraphicList.TextListEntry ID="3" CompositionName="Entries">
      <AttributeList>
        <DefaultEntry>false</DefaultEntry>
        <EntryType>SingleValue</EntryType>
        <From>0</From>
        <To>0</To>
      </AttributeList>
      <ObjectList>
        <MultilingualText ID="4" CompositionName="Text">
          <ObjectList>
            <MultilingualTextItem ID="5" CompositionName="Items">
              <AttributeList>
                <Culture>en-US</Culture>
                <Text>&lt;body&gt;&lt;p&gt;the displayed text&lt;/p&gt;&lt;/body&gt;</Text>
              </AttributeList>
            </MultilingualTextItem>
          </ObjectList>
        </MultilingualText>
      </ObjectList>
    </Hmi.TextGraphicList.TextListEntry>
  </ObjectList>
</Hmi.TextGraphicList.TextList>
```

The entry text is **escaped HTML**, not plain text: `<body><p>…</p></body>`.

### 🔴 `ImportOptions.Override` REPLACES the object. It does not merge.

Measured 2026-08-18 against a real project, with **disjoint** contents so the answer cannot be read
two ways — a merge and a replace give different counts, not the same count by coincidence:

| step | sent | tag tables | tags in device | read back from the project |
|---|---|---|---|---|
| create | table `P`, one tag `A` | 3 → **4** | 67 → **68** | — |
| override | table `P`, one tag `B` | 4 → **4** | 68 → **68** | **1 tag, and it is `B`** |

`A` is gone. The same experiment on a text list: a list holding entry `0` was overridden by a
document holding only entry `1`, and the read-back holds **one** entry, `From/To = 1`.

**Consequence: adding one tag means shipping the whole table.** There is no additive path — read
the existing table out with `export --hmitagtable`, edit the document, send all of it back.

Both documents round-trip **identical modulo object IDs** (TIA reassigns `ID=` on import, exactly as
it does for `Part`/`Wire`/`Access` UIds in LAD): export → import → export produced a byte-identical
body once IDs were normalised, for the tag table and for the text list alike.

### Reporting: counts read back from the project, never from `Import()`

Both imports print the denominator on both sides:

```
DEVICE:  <device>/<hmi runtime>
KIND:    HMI tag table
FILES:   1
           C:\...\MyTable.xml

HMI tag tables in device   BEFORE 3  ->  AFTER 4   (+1)
tags in device        BEFORE 67  ->  AFTER 68   (+1)

RETURNED BY Import(): 1
  PRESENT MyTable
PRESENT AFTER a re-read: 4
    ...
```

The **member** count is the line that distinguishes replace from merge; the container count cannot,
because an override leaves it unchanged. For a text list the member line reads
`NOT EXPOSED BY THE API` — a `0` there would be a false claim about the project instead of a true one
about the API.

**Empty is not clean, in two ways, and both exit 13** (`ImportIncomplete`): `Import()` returning
nothing, and `Import()` naming an object that a re-read does not find. A count comparison alone is
*not* used as the gate — re-importing an existing table legitimately leaves every count unchanged,
and that is a real success.

### A wrong-kind document is refused, and the refusal names both sides

Handing a text-list document to `--hmitags` (exit 7, nothing written):

```
HMI tag table import failed for 'MyList.xml':
  Siemens.Engineering.EngineeringTargetInvocationException: Error when calling method 'Import'
  of type 'Siemens.Engineering.Hmi.Tag.TagTableComposition'.
  ---> ... : Import action was invoked on navigator 'TagTables' which is out of context for the
  Simatic ML file containing 'Siemens.Engineering.Hmi.TextGraphicList.TextList' root object.
```

The **whole** chain is carried verbatim for the graphics path's reason, and it is not decoration
here: the outer message says only that `Import` threw. The inner one is the entire answer.

### What is NOT built

- **No `import-all` equivalent.** These are separate flags, not a mixed-kind bulk restore.
- **Tag tables CAN be deleted** — `hmi-delete-tagtable`, added 2026-08-20; see *Deleting graphics,
  classic screens and classic tag tables* below. **Text lists still cannot:** `TextList.Delete()`
  exists and nothing here calls it.
- **Individual tags are not wired.** `TagComposition.Import` takes a single-tag document into ONE
  named table; only the table-level route is exposed, because that is the one the export produces.
- **The Unified refusal is unit-tested, not measured** — the project used for the live run carries
  no Unified device.

## Deleting graphics, classic screens and classic tag tables (2026-08-17, tag tables 2026-08-20)

```
openness-cli graphics            <project> --delete <name>... --yes
openness-cli hmi-delete-screen   <project> --name <name>... [--device <name>] --yes
openness-cli hmi-delete-tagtable <project> --name <name>... [--device <name>] --yes
```

`hmi-delete-screen` is separate from `hmi-delete` for the same reason `hmi-compile` is separate from
`compile`: `hmi-delete` is the metamodel command over `HmiSoftware` (Unified) compositions and
**cannot see a classic screen at all**. `hmi-delete-tagtable` is separate for exactly that reason
too — `hmi-delete --kind TagTables` resolves through `FindSingleUnifiedSoftware`, so it is blind to a
classic device's `TagFolder`.

Everything below applies to all three. Three notes specific to the tag-table verb:

- The walk over `HmiTarget.TagFolder` is **recursive** (`TagFolder.Folders` is a
  `TagUserFolderComposition`), matching `export --hmitagtable`. A flat walk would report "not found"
  for a table plainly visible in the project tree.
- A table on a **Unified** device is refused **by name** (`HmiClassicOnlyObjectException`), not
  flattened into "not found": the object exists, this route does not reach it, and those are two
  different corrections.
- **A deleted classic tag table cannot be re-created through the API.** `TagTableComposition` has no
  `Create` — a SimaticML import is the only route back, so `export --hmitagtable` first if the
  content might be wanted again.

### 🔴 There is no wildcard, and that is deliberate

`--delete` / `--name` take **literal names only**, repeated once per object. There is no `--prefix`,
`--pattern`, `--glob` or `--all`, and a name containing `*` is carried through as a literal that
simply fails to resolve. A pattern evaluated at delete time is one typo away from taking real
content with it, and the caller can always enumerate first and pass the names it meant. A test
asserts the absence of every pattern-style flag, so adding one later has to delete a statement that
it does not exist.

### Every name resolves BEFORE anything is deleted

A batch containing one unresolvable name deletes **nothing** — measured against a real project:

```
$ ... --delete ZzProbePng --delete ZzDoesNotExistAnywhere --yes
No graphic named 'ZzDoesNotExistAnywhere' in Project.Graphics. Present: <the 31 that are>
exit 7      graphics before: 31      graphics after: 31
```

A half-applied delete is worse than none, and an unknown name is the likeliest mistake a caller
makes. The message names what **is** present, so a typo is correctable without a second command. A
name that does not exist is a **hard error, never a silent no-op**.

### The confirm fence, and the read-back

`--yes` is required. Without it the plan lists **every** name and Portal is **never contacted**
(exit 10) — measured at ~0.1 s per run, where a real attach costs at least 0.7 s. A unit test drives
the real entry point with a counting gateway and asserts `OpenProjectCalls == 0`, because a refusal
that still opened the project would satisfy any test that only checked the exit code.

After the save, the composition is **re-read** and any survivor raises
`DeleteDidNotTakeEffectException`. That is classified `UnexpectedError` (5), **not** `CommandError`:
nothing the caller typed can fix a delete that reported success and did not happen. It is the same
shape as `block-layout --set`'s silent no-op, which is exit 15 rather than a success with a note for
exactly this reason. A run that deletes zero objects never exits 0.

### Order: referencing objects first

**Delete screens before the graphics they reference.** A `GraphicView` left pointing at a deleted
picture fails the HMI compile with `The graphic for the '<item>' screen object is invalid` — the
same error this file documents as the graphics negative control.

## `hmi-compile` — compiling the HMI device

```
openness-cli hmi-compile <project> [--device <name>] [--json]
```

Same flags and same `CompileResult` shape as `compile`, so diagnostics render identically. It exists
as a separate subcommand because `compile` resolves its target through PLC-only device discovery and
**cannot see an HMI device at all** — the same blind spot the read walker had.

Its purpose is the open question left by `Validate()` being measurably shallow
(`docs/notes/openness-hmi-write-api.md` §4c): if per-object validation does not gate, does a device
compile? Whatever it reports is the answer, including "nothing".

**The verdict keys on ERRORS, never on `State` (2026-08-12)** — the same rule as `compile`, and
literally the same code path: both call `Program.EffectiveErrorCount`, the fail-closed
`max(ErrorCount, Error messages in the message tree)`. `compile` earned that rule the expensive way (a
project-wide hardware warning made every clean per-block compile exit 8 with `errors: 0`);
`hmi-compile` carried the identical `State != Success` defect and was fixed by ruling rather than by
being bitten. **Warnings are not swallowed**: the state and the warning count still print, and a
non-`Success` state with zero errors prints a `PASSED WITH WARNINGS:` line saying so explicitly. Only
the exit code changed.

### `hmi-compile` success is WEAKER than `compile` success, and the API is why

The two commands take the same flags and print the same `CompileResult` table. **They do not carry the
same guarantee, and the difference cannot be closed from this side.** Read this before treating a
green `hmi-compile` the way you would treat a green per-block `compile`:

| | `compile --block/--type` | `hmi-compile` |
|---|---|---|
| Errors reported by the compiler | gated (exit 8) | gated (exit 8) |
| Item still inconsistent afterwards | gated (exit 11) — read back from the item itself | **no such check exists** |
| What a `0` means | the compiler reported no errors **and** the item re-read as consistent, so TIA will export it | the compiler reported no errors. That is all |

`compile`'s second row is built on `PlcBlock.IsConsistent`, and **there is no HMI equivalent to read.**
Measured against the V20 API surface (2026-08-12, `PublicAPI/V20/Siemens.Engineering.xml` +
`Siemens.Engineering.Hmi.xml`): `IsConsistent` exists on exactly **four** types — `PlcBlock`,
`PlcType`, `PlcForceTable`, `PlcWatchTable`, all `Siemens.Engineering.SW.*` — and on **nothing** in
`Siemens.Engineering.Hmi.*` (Classic) or `Siemens.Engineering.HmiUnified.*`. The nearest neighbour on
the Unified side is `HmiValidationResult` (Errors/Warnings), which is the `Validate()` surface already
measured shallow — it accepts a zero-width screen. So there is no per-screen or per-device flag to
cross-examine a green result with, and this tool cannot manufacture one.

Consequently `hmi-compile` **does not report a consistency field it cannot fill**: no `CONSISTENT:`
line, and `consistentAfterCompile` is `null` in `--json` — the same value a whole-device PLC compile
carries, meaning *the question was not asked*. An always-null field printed as though a check had run
would read to a consumer as "checked, nothing wrong", which would be false. Exit codes are `0` /
`8 = CompileFailed` only; there is no `11 = CompileIncomplete` analogue **because there is nothing to
detect it with**, not because the case cannot arise.

### Attaching under Portal pileup

`Connect` prefers a running Portal that already has the requested project open, reading
`TiaPortalProcess.ProjectPath` **without attaching** (free, and touches nothing). It previously took
`GetProcesses()[0]` unconditionally, which is fine with one Portal and harmful with several:
observed live 2026-08-07, two runs against the same project succeeded and a third hung for a full
15-minute connect timeout with five Portal processes running — the "second instance sometimes won't
connect under process pileup" symptom CLAUDE.md records. The hint narrows *which* process is
attached, never *whether* one is, and falls back to the old behaviour when absent or unmatched.

### ⚠️ `hmi-delete-screen` with several `--name` flags — UNEXPLAINED, reproduced 2026-08-20

Passing eighteen `--name` values in one invocation failed with

```
No classic HMI screen named '09 Parameters' found in the project.
```

**and the same name, alone, in the very next command, deleted successfully.** Two more single-name
deletes and then a loop of sixteen all succeeded — 18 of 18 removed, one call each.

The argument parser accumulates repeated `--name` correctly (verified by echoing the constructed
argv: 36 arguments, `--name` and value alternating, names intact), and `DeleteScreens` resolves each
name against a freshly-built candidate list per iteration, so neither obvious cause holds.

**The cause is NOT established and is deliberately not guessed at here.** What is measured is the
behaviour and the workaround: **one name per invocation**. It costs a Portal attach each, which is
the only real price.

Worth noting the failure was SAFE — the resolution loop runs to completion before anything is
deleted, so a name that does not resolve aborts the whole call and nothing is removed. The defect
is a refusal that should not have happened, not a deletion that should not have happened.

#### What the NEXT reproduction will say (2026-08-20)

The cause is still not established, and the change made here does not pretend to establish it — it
makes the next occurrence *diagnosable*, which is what was actually missing. `ScreenNotFoundException`
reported **only the name**, so absence, a failed walk, and a string that is not the string it appears
to be all printed identically. It now reports:

- **`PRESENT:`** — every classic screen name in the project, built through the *same* walk that
  failed to find the requested one, so the two cannot disagree about what the project holds. A
  project with no classic screens says so rather than printing an empty list.
- **`NEAR MATCH:`** — any present name that differs from the requested one only by whitespace,
  invisible characters or case, followed by **both names dumped code point by code point**.

That second line targets the one hypothesis reading the code could not eliminate: `--name` is taken
**verbatim** (`TryTakeValue` — no `Trim`, no Unicode normalisation) and matched `Ordinal`. A trailing
space or a non-breaking space in one element of a long generated command line is invisible to an
echo, invisible in the output, and produces exactly the observed signature. **The argv was verified
by echo at the time, and an echo cannot show this.**

Deliberately NOT done: trimming or normalising the name. That would change which screens can be
addressed on the strength of a hypothesis, and a delete is the one irreversible operation this CLI
exposes. If the near-match line fires on the next reproduction, the fix follows from evidence; if it
stays silent, that is evidence too — and the present-set will show whether the screen was there at
all.
