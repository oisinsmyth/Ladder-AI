# Target-family differences — a living ledger

**Status: OPEN AND EXPECTED TO GROW. This is not a specification and it will never be complete.**

`hmi/PLAN.md` §0 enumerates the Classic/Unified split from
`docs/notes/openness-hmi-api-survey.md`. That enumeration is **a floor, not a census.** It was
derived from a type-count survey and one reference project, and it says nothing at all about:

- Basic vs Comfort vs Advanced vs Professional within Classic
- Unified Basic Panels vs Unified Comfort Panels vs Unified PC RT
- panel-size variants within a family (MTP700 / MTP1000 / MTP1200 …)
- firmware and TIA version interactions
- per-property differences that only appear at runtime rather than at engineering time

**Assume every one of those hides a difference nobody has written down.** This project's most
expensive failures have all been a green result that examined nothing, and "the target family is
close enough" is exactly that shape.

---

## The rule this ledger exists to enforce

> **A difference we have not met yet must produce a NAMED REFUSAL, never a guess and never a silent
> no-op.**

That is not a new principle here. It is:

- **ADR-0010** — an unsupported construct is a scope item, not a resting place. It hard-errors.
- **FI-71 / `--allow-blind-types`** — refuse, name the reason, offer an explicit escape, never guess.
- **`block-layout --expect`** — assert the state you believe you are in and exit non-zero on
  mismatch, rather than discovering it later.
- **"Empty is not clean"** — a check that examined nothing must never report a pass.

## Five mechanisms that make the unknown surface instead of silently degrading

1. **The target family is DECLARED, never inferred.** Every tool in the chain takes the family
   explicitly and records it in its output. `screen-ir.json` carries the family it was authored for.
   A tool handed an artifact for a different family refuses; it does not adapt.

2. **`--expect <family>` on every Portal-touching command.** Read the device's actual family back and
   exit non-zero on mismatch. The failure mode being prevented is authoring a perfectly good Unified
   screen against a Classic target and getting a confusing downstream error that names something
   else entirely.

3. **Unknown item type, unknown attribute, unknown enum value → hard error naming it.** Not a skip,
   not a default, not a nearest match. The error text carries the literal name so it can be searched
   for and added here.

4. **Assume a `MemoryLayout`-class trap exists and design the read-back to notice.** There is at
   least one attribute, somewhere, that is silently reset on re-import and invisible to every check.
   That exact thing happened on the PLC side: every gate stayed green while the block reverted, and
   it bit on the *second* import. Do not hope; make T7's comparison capable of seeing it.

5. **Every entry below records HOW it was found.** A difference found by reading a manual and one
   found by a failed import are different evidence tiers, and the second is worth more.

## What to do when a new difference appears

1. **Stop and record it here** before working around it. A workaround applied and not written down
   is a difference that will be rediscovered at full cost.
2. **Name it in the refusing tool's error text** so the next occurrence self-identifies.
3. **Decide explicitly** whether it is a scope item (widen the tool) or a boundary (refuse
   permanently, documented). ADR-0010's rule: it does not get to be a resting place.
4. **Note whether it invalidates anything already measured.** The Classic/Unified split invalidated
   nothing here only because nothing had been built yet; a later one will not be so cheap.

---

---

## 🔵 SCOPE OF EVERY ALARM-VIEW FINDING BELOW: **CLASSIC CONFIRMED · UNIFIED TBC**

**Owner's ruling, 2026-08-17.** Everything established about the alarm view — that it does not
export, that seven probes found no alternative aperture, that the master copy reproduces it — was
measured **on a Classic Basic panel (KTP900) and nowhere else.**

**Do not carry any of it to Unified.** The two families share no types, and Unified has the *opposite*
shape: a full typed screen-item tree (`HmiAlarmControl` is a creatable item there) and no file export
at all. It is entirely plausible that Unified can author an alarm view the moment the tooling reaches
it — the reason Classic cannot is the absence of an object model, and Unified does not have that
absence.

**Status by family:**

| family | alarm view: author? | alarm view: reproduce? | evidence |
|---|---|---|---|
| **Classic Basic** | ❌ **NO — confirmed** | ✅ **YES — confirmed** (master copy) | measured, this project, 2026-08-17 |
| Classic Comfort/Advanced/Pro | *assumed as Basic* | *assumed as Basic* | ⏸️ untested — same API, so likely, but see row 16 |
| **Unified** | ⏸️ **TBC** | ⏸️ TBC | **nothing measured. `HmiAlarmControl` is a creatable type there, so the Classic answer probably does NOT transfer** |

### 🔴 A retracted inference, kept because the failure mode is the point

Row 18 originally asked whether TIA resolves an imported screen's template link — reasoning that
since `Root screen`'s export contains no alarm element, the banner must live on `Template_1`.

**The owner corrected it: the alarm view is on the screen directly. The template does not have it.**

**The error was using a blind instrument as evidence of absence.** The export shows no alarm element
on `Root screen` — but this very ledger records that *no* export ever shows an alarm element. The
silence was a property of the instrument, not of the screen. Having established the exporter cannot
see alarm views, I then read its failure to mention one as information.

Worth keeping visible because the shape recurs: **a measurement that something is missing is only
evidence if the instrument could have found it.** The same trap is why `drift-check`, `compare` and
`compile-all` all had to learn to print denominators.

## Ledger

Evidence tiers: `[MEASURED]` observed live · `[VENDOR-DOC]` primary source · `[SEARCH]` search-summary
· `[INFERENCE]` reasoned.

| # | difference | families | how found | tier | consequence |
|---|---|---|---|---|---|
| 1 | Two disjoint Openness APIs sharing no types | Classic vs Unified | type-count survey across V17–V20 | `[MEASURED]` | forces two architectures; see PLAN §0 |
| 2 | Classic models nothing *inside* a screen — no `ScreenItems`, no button/IO-field/shape type | Classic | API survey | `[MEASURED]` | Classic cannot be built by API; must go via SimaticML file |
| 3 | Unified has no screen export in any form | Unified | confirmed five ways | `[MEASURED]` | no diff/hash/round-trip without T6 serialiser |
| 4 | Classic screens export/import as SimaticML | Classic | doc-comment "Simatic ML export of a screen" | `[MEASURED]` | Classic fork reuses this repo's existing pipeline shape |
| 5 | Classic has no alarm API whatsoever | Classic | API survey | `[MEASURED]` | alarm generation is Unified-only |
| 6 | Classic has no per-object validation | Classic | API survey | `[MEASURED]` | gate is a device compile, which FI-52 shows is weak alone |
| 7 | `Validate()` accepts a zero-width screen | Unified | live probe | `[MEASURED]` | validation is shallow; not a substitute for T2/T3 |
| 8 | 🔴 Basic panels absent from the Classic target line; Openness/SimaticML support unrecorded | **Classic Basic** | absence noted 2026-08-17 | `[INFERENCE]` | **THE KEYSTONE. K1 answers it — PLAN §3 wave 1** |
| 9 | CWCs "not recommended" on Unified Basic Panels; no `fetch`/`XHR` on Unified Comfort Panels | Unified tiers | vendor docs | `[VENDOR-DOC]` | escape-hatch route is tier-dependent |
| 10 | Anchor project carries a **KTP900 Basic** | Classic Basic | string read from the anchor's own project files, 2026-08-17 | `[MEASURED]` | fixes the anchor family; see PLAN §0 |
| 11 | KTP700 / KTP900 Basic both **800×480** | Classic Basic | ✅ datasheets + the anchor's own export (`Width 800`, `Height 480`) | `[VERIFIED]` | anchor is resolution-representative of the target; experiment 2's 1280×800 stylesheet does not transfer |
| 12 | 🔴 **Basic panels do NOT have square pixels** — KTP700 1.794 physical vs 1.667 pixel aspect; KTP900 1.773 | Classic Basic | datasheet, 2026-08-17 | `[VERIFIED]` | **px/mm is a PAIR, not a scalar.** Contradicts a `[INFERENCE]` this project had already written down, reasoned from the Unified MTP700 which *is* square |
| 13 | ✅ **A Classic Basic screen DOES export as SimaticML, with full content** — items, geometry, colours, layers, groups | Classic Basic | K1, live against the anchor | `[MEASURED]` | **row 8 closed positively.** The architecture stands |
| 14 | 🔴 **Classic's SimaticML carries what its OBJECT MODEL does not** — `ScreenLayer`, `Group`, and every item type with absolute `Left/Top/Width/Height` | Classic | K1 | `[MEASURED]` | the API survey's "Classic models nothing inside a screen" is true **of the object model only**, and reads as a capability limit it is not |
| 15 | `SoftKey` items carry **no geometry** (physical bezel keys, not screen objects) | Classic Basic | K1 | `[MEASURED]` | an item type the emitter must handle as position-less, and the linter must not flag as zero-size |
| 16 | ⏸️ **KTP700 vs KTP900 Basic: assumed to differ ONLY in physical size** | Classic Basic | assumption, owner-deferred 2026-08-17 | `[INFERENCE]` | **OPEN AND DEFERRED BY DECISION.** See below |
| 17 | ✅ **A bad enum value is caught AT IMPORT**, naming value, attribute, element type, SimaticML ID and line/position | Classic Basic | fault injection M5, live | `[MEASURED]` | import is a real gate, not a passthrough |
| 18 | ~~Does TIA resolve an imported screen's `<Template>` link?~~ **QUESTION WITHDRAWN** — it rested on a false premise | Classic Basic | owner correction, 2026-08-17 | `[RETRACTED]` | the alarm view is **on the screen directly**; the template does not carry it. The template link is preserved by the round trip (still true, still measured) but it was never the banner's source |
| 19 | Screen export/import round trip is **element-for-element faithful** (12 types, 219 KB, template link preserved) | Classic Basic | round trip, live | `[MEASURED]` | the architecture's core assumption, verified |
| 21 | 🔴🔴 **THE ALARM VIEW DOES NOT EXPORT AT ALL.** A screen containing only an alarm view exports with NO object representing it | Classic Basic | owner's A/B screens, live differential | `[MEASURED]` | **round-tripping such a screen DESTROYS the control.** See "Row 21" below |
| 23 | ✅ **The IMPORT path preserves 24-bit colour EXACTLY — no quantisation** | Classic Basic | 20-swatch round trip incl. values chosen to move under RGB565 | `[MEASURED]` | the emitted colour is what the project stores; whatever "closest available" correction TIA does happens in its own colour PICKER or at download/display, none of which this path touches |
| 22 | 🔴 **Classic CANNOT have sub-screens.** Unified composes screens via `ScreenWindow`; Classic has no window item type at all | Classic vs Unified | owner, 2026-08-17, corroborated by the item census | `[MEASURED]` + owner | a Basic screen is SELF-CONTAINED: all chrome repeats on every screen, out of the same 800×480. No composition, so consistency must come from the generator |
| 20 | 🔴 **Degenerate geometry (zero width) passes BOTH Classic compile and Unified `Validate()`** | both families | fault injection M2 + prior Unified probe | `[MEASURED]` | shared blind spot — this is the hole `hmi-cli` H-501/502/505 exist to fill |
| 24 | 🔴🔴 **EVERY graphic format is RASTERISED TO BITMAP (`*.png`/`*.jpg`) AT DOWNLOAD.** *"Due to the conversion, vector formats pixelate… It is therefore recommended to use several graphics that are customized to the target resolution."* | Classic Basic | Siemens *TIA Portal Information System*, Basic-scoped topic, V20 + V21 | `[VENDOR-DOC]` | **vector buys NOTHING on Basic.** No scaling across 4″/7″/9″/12″. The deliverable is a PNG per symbol per size, which is Siemens' own recommendation — not a fallback from a failed SVG route |
| 25 | **Basic accepts TEN formats — `bmp ico emf wmf gif tif png svg jpeg jpg`** — wider than the web-repeated BMP/JPEG/PNG/WMF/EMF list | Classic Basic | verbatim Siemens topic, corroborated 3× in the same book; independently corroborated by live import | `[VENDOR-DOC]` + `[MEASURED]` | resolves the probe's own doubt: GIF/ICO/TIF compiling clean was **correct behaviour**, not a hole in the gate |
| 26 | 🔴 **No "Transparent" property on Basic Panels.** A transparent background is replaced by the graphic object's background colour, *"linked firmly with the graphic"* | Classic Basic | verbatim Siemens, Basic-scoped | `[VENDOR-DOC]` | **render each PNG onto the ACTUAL screen background colour**; do not ship alpha and hope. A symbol is bound to the background it was rendered for — so a house-palette change invalidates the symbol set |
| 27 | **The "Symbol library" OBJECT is not available on Basic** (Comfort/Panel/RT Advanced/RT Pro only). The **WinCC graphics FOLDER** *is* — two different things, commonly conflated | Classic Basic | Siemens topic device scope | `[VENDOR-DOC]` | the shipped graphics folder is the free, already-installed option and has never been inspected — highest-value open item before any purchase |
| 28 | **Import size caps by format:** `bmp tif emf wmf` ≤ 4 MB · `jpg jpeg ico gif` ≤ 1 MB. **`png` and `svg` are ABSENT from Siemens' own table** | Classic Basic | Siemens topic | `[VENDOR-DOC]` | read the absence as a documentation gap, **not** as "unlimited" |
| 29 | **KTP700 / KTP900 are 800×480 at 16-bit colour (65,536)** | Classic Basic | *Basic Panels 2nd Gen Operating Instructions*, 05/2021, A5E33293231-AD | `[VENDOR-DOC]` | **completes row 23.** The import path preserves 24-bit exactly *and* the panel displays 16-bit — so the owner's observed "TIA corrects it to the closest available" is a DISPLAY/PICKER quantisation, exactly where row 23 predicted it, not a loss in the file path |
| 30 | 🔴🔴 **A SCREEN-NUMBER COLLISION CRASHES THE PORTAL PROCESS.** Importing a screen whose number is already held by a DIFFERENT screen does not fail validation — Portal dies, reporting only `Access to a disposed object of type 'Siemens.Engineering.Project'` | Classic (Comfort measured) | live import, control run in BOTH directions on a real project | `[MEASURED]` | **`Root screen` holds number 1**, so the first screen anyone numbers naturally collides. Third member of the Line-endpoint / Circle-radius family: TIA validates none of them and dies instead of refusing. Guard now in `import --screen`, refusing before Portal is contacted |
| 31 | 🔴 **Openness does NOT expose a classic screen's NUMBER.** Neither `Number` nor `ScreenNumber` reads, and `GetAttributeInfos` offers no attribute containing "Number" | Classic | attribute reads + self-description sweep, live | `[MEASURED]` | consistent with `hmi --json` reporting `screenNumber: null` for every classic screen. **So row 30's guard cannot use the object model** — it reads the number from an EXPORT instead. THE DOCUMENT IS RICHER THAN THE API, for the third time (screens, alarm view, now numbers) |
| 32 | 🔴 **AN EVENT CANNOT BE CREATED ON A CLASSIC SCREEN THROUGH AN IMPORT:** `'Create' is not supported by type 'Siemens.Engineering.Hmi.Event.EventComposition'` | Classic | live import of a single button carrying one ActivateScreen event | `[MEASURED]` | **so NAVIGATION cannot be generated at all** — it is a hand-off item, exactly like the alarm view. ⚠️ Note the trap: **TIA EXPORTS events perfectly well** (the emitted structure was harvested from a real export carrying four), so a round trip reads as though it should work. **This is the alarm view's shape a SECOND time, which answers row 21's closing question — it is a CLASS, not an alarm-specific quirk** |
| 33 | **`FieldLength` is the FormatPattern's LENGTH, not its digit count** — corpus: pattern `99999.999`, FieldLength `9` (length 9, digits 8) | Classic | corpus read + emitter regression test | `[MEASURED]` | two attributes that must agree, same family as Circle's radius. Found while hunting row 30 and **NOT its cause** — recorded separately so it is not credited with that fix |
| 34 | 🔴🔴 **`hmi-compile` RUNS INCREMENTALLY BY DEFAULT AND CAN EXAMINE NO SCREEN AT ALL.** A run that reports a handful of hardware errors and "not one names a screen" may have looked at nothing | Classic | live, on a full "Compile all" that TIA switched to itself | `[MEASURED]` | **this retracts a claim recorded earlier the same day** — *"a dangling tag imports clean AND compiles clean, nothing downstream catches it"*. A FULL compile catches it **per field**, naming the screen and the object: 239 errors, all `The process tag is invalid. The tag does not exist.` So `hmi-compile` IS a real gate on tag existence. **The compiler states its own mode** (`Compilation mode was switched from "Compile changes" to "Compile all"`) — read that line before believing a green, exactly as `compile-all` already teaches for PLC blocks |

| 35 | 🔴🔴 **AN HMI TAG DOCUMENT CRASHES THE PORTAL PROCESS IN THREE SEPARATE WAYS.** Same aftermath as row 30 — `Access to a disposed object of type 'Siemens.Engineering.Project'`, naming nothing. **(a)** `HmiDataType` naming a type that is not an HMI-side type; **(b)** `DataType` disagreeing with the PLC member's own declared type; **(c)** a `ControllerTag` naming a BIT path (`<DB>.<Member>.%X0`) with `DataType Bool` | Classic Basic | 17 single-tag imports, each varying ONE field against a held-constant document, live | `[MEASURED]` | **fourth member of the Line-endpoint / Circle-radius / screen-number family.** Every one is schema-valid and enum-valid; TIA validates none of them and dies. A generator must derive `DataType` from the controller's own declaration, never from the tag's name or from the screen's format string |
| 36 | ✅ **OMITTING the `HmiDataType` link is accepted, and TIA DERIVES it — `Byte`→`USInt`, `Word`→`UInt`, everything else identity** | Classic Basic | omit-probe + read-back of 167 tags | `[MEASURED]` | **this is the fix for row 35(a) and it is better than getting the name right**: the PLC-side and HMI-side type vocabularies are different sets, `Word`/`Byte`/`UShort` are all fatal as `HmiDataType`, and the safe move is to state the PLC type and say nothing about the HMI one. Corroborates the README's measured `String`/`WString` pair — the two links are *expected* to disagree |
| 37 | 🔴 **A classic HMI process tag HAS NO BIT FORM.** `Hmi.Tag.Tag`'s attribute set carries no bit number, and address access is `Symbolic` against an optimized DB, so there is no absolute route either | Classic Basic | attribute census + row 35(c) | `[MEASURED]` | **a screen that binds one bit of a published word cannot be satisfied by a tag.** It needs either a Bool member published by the PLC, or the parent word bound with a per-bit dynamization — and this tier's emitter can create neither. Plan the interface DB with Bools where the panel needs Bools |
| 38 | 🔴 **AN `AcquisitionCycle` CAN RESOLVE AT IMPORT AND STILL FAIL THE COMPILE.** `250 ms` and `125 ms` import clean, read back correctly, and then produce `Invalid acquisition cycle for tag <x>`; `100 ms`, `500 ms` and `1 s` compile clean | Classic Basic | live, four import+compile pairs | `[MEASURED]` | **`TargetID="@OpenLink"` resolving is NOT evidence the link is valid** — the cycle object exists in the project and is still not permitted here. The accepted set looks like multiples of 100 ms, but that is `[INFERENCE]` from five points. **Always compile after a cycle change**; a read-back agrees with the document either way |
| 39 | ✅ **An INTERNAL (non-PLC) tag is expressed by OMITTING the `Connection` and `ControllerTag` links** — with or without an `AcquisitionCycle`, both import clean and read back with both links absent | Classic Basic | two single-tag probes + read-back | `[MEASURED]` | the panel-internal tags a screen needs for selection/entry fields are generable, so they do not have to be a hand-off item |
| 40 | ⚠️ **Text-list entry text is sent ESCAPED and comes back UNESCAPED.** The document carries `&lt;body&gt;&lt;p&gt;…`; the export carries `<body><p>…` as real child elements | Classic Basic | five lists, sent vs read back | `[MEASURED]` | **a naive text comparison reports every entry as empty and every list as broken.** Unescape before comparing — this cost a false "the import dropped all the text" reading on a run that was in fact perfect |

## Row 16 — why deferring the 7" project is safe, and what it costs

The owner asked whether acquiring a real **KTP700** project can wait. **Yes, and the reason it is safe
is structural rather than optimistic:**

- **Panel geometry is a DATASHEET fact, not a project fact.** The whole experiment-2 arm set was
  already checked against a KTP700 canvas without any KTP700 project existing — `--panel KTP700` is a
  flag, and H-405/406/407 make every threshold a per-panel conversion. Nothing needs rebuilding when
  the real panel arrives.
- **The two panels share a resolution (800×480) and a tier.** A layout transfers pixel-for-pixel;
  only the millimetre reading changes, and that is already computed for both.

**What it costs, stated so it is not forgotten:** if the KTP700 and KTP900 differ in *supported item
types or attributes*, we learn it late — at delivery rather than in development. That is judged
low-probability (same product family, same tier, same resolution, same WinCC Basic runtime) but it is
**not zero**, and "same tier" is exactly the kind of reasonable inference row 12 has already caught
being wrong once.

**Cheap mitigation, no 7" project required:** the emitter hard-errors on unknown item types and
attributes anyway (§0 floor-not-census), so a capability difference surfaces as a *named refusal* at
emit or import rather than as a screen that silently misbehaves. That is what makes the deferral
tolerable — not the low probability, but the fact that being wrong fails loudly.

**Rows 10–11 were added on the first day the target was known. Row 11 is exactly the kind of
plausible-sounding number this ledger exists to stop being quoted as fact — it is marked
`[INFERENCE]` and it stays that way until something measures it.**

## Row 8 is now the critical path, not a footnote

When this ledger was written, row 8 was an open question at the bottom of a list. One owner decision
later it is **the single fact the whole architecture rests on**, and nothing about the underlying
uncertainty changed — only its position did.

That is the argument for the ledger. A difference nobody has met is not ranked by how important it
will turn out to be, because that is not knowable when it is recorded. **Record them flat, and let
the plan reach in and promote one when it needs to.**

---

## 🔴🔴 Row 21 — the export is INCOMPLETE, and nothing in the file says so

**The owner's differential settled this in two screens**, which no amount of reading the API survey
would have: `Screen_1` (alarm view, nothing else) against `Screen_2` (genuinely empty). The entire
difference between them:

```xml
<Hmi.Screen.ScreenLayer ID="3" CompositionName="Layers">
  <AttributeList><Index>0</Index><Name /><VisibleES>true</VisibleES></AttributeList>
</Hmi.Screen.ScreenLayer>
```

**That is all.** No alarm-view object, no attributes, no placeholder, no warning, exit 0. The
control is simply *not in the document.*

### Why this is worse than a missing feature

**It is silent data loss on a round trip.** Export a real screen carrying an alarm view, re-import
it, and the alarm view is gone — with a clean export, a clean import and a clean compile. This is
the `MemoryLayout` shape exactly: every gate green, the artifact quietly wrong.

### 🔴 And it invalidates a claim this project made earlier the same day

`wave-1-results.md` reported the round trip as **"element-for-element faithful"**, from an
export → import → re-export comparison. That comparison is **self-consistent but not complete**:
both sides were produced by the same exporter, so both are blind to the same omissions.

> **A round trip cannot detect content that neither side carries.** The claim was not false about
> what it measured; it was over-read as being about fidelity in general. Corrected here.

### Detection — partial, and the honest limits

| case | detectable? | how |
|---|---|---|
| screen with ONLY unexportable content | ✅ yes | a `ScreenLayer` exists with **no `Hmi.Screen.*` item siblings** — the layer is the tell. A truly empty screen has no layer at all |
| screen with a button **and** an alarm view | ❌ **no** | the layer and the button both export; the alarm view's absence is invisible |

**So the export alone cannot certify its own completeness.** The one instrument that might is the
**device compile**, which walks the screen tree and names objects (`Root screen: Button_7: …`) — if
it names an object absent from the export, that is a completeness signal. Untested; wave 2.

### The operating rule this forces, and it costs us nothing

> **NEVER RE-IMPORT A SCREEN THIS TOOLING DID NOT AUTHOR.**

Emit new screens; never round-trip an engineer's existing one. The generation use case does not need
to — and this makes the hazard unreachable rather than merely detected. Any future need to *edit* an
existing screen re-opens it and must be treated as a scope item under ADR-0010, not worked around.

### Challenged, re-tested, and it survived — with much better evidence

The owner pushed back: *"I struggle to believe it does not export... confirm there isn't something
that could do it."* Right to ask — the original conclusion rested on **one** export call with **one**
option value. Three things were done to break it:

**1. `ExportOptions` is a THREE-valued enum and only one had ever been used.**
`None` | `WithDefaults` | `WithReadOnly`. All three now run against `Screen_1`:

| option | bytes | objects emitted |
|---|---|---|
| `None` | 2,818 | Screen, ScreenLayer, MultilingualText, MultilingualTextItem |
| `WithDefaults` | 2,953 | *identical set* |
| `WithReadOnly` | 2,826 | *identical set* |

**With a positive control proving the flag is live:** `ActiveLayer`, `GridColor`, `Visible` and
`Width` appear only under `WithDefaults`. The parameter demonstrably changes the document — it
governs **attributes**, never **objects**. So the test is valid rather than a silent no-op.

**2. Reflection over `Siemens.Engineering.dll` — `Screen` has no second route.** Its *entire* public
surface:

```
properties: Name, Parent
methods:    Delete, Export(FileInfo, ExportOptions), GetAttribute(s), GetAttributeInfos,
            SetAttribute(s), GetService, + object overrides
```

**One `Export`. No item collection, no child accessor, nothing else to call.** There is no second
way through this object because there is nothing else on it.

**3. Other export surfaces exist but hold different content.** `HmiTarget` also exposes
`ScreenGlobalElements`, `ScreenOverview`, `ScreenTemplateFolder`, `ScreenPopupFolder`,
`ScreenSlideinFolder` — each with its own `Export`. None of them contains an object placed on
`Screen_1`; they are separate containers, not alternative views of the same screen.

**Conclusion stands, now on far stronger footing:** for a control placed on a classic screen,
`Screen.Export(FileInfo, ExportOptions)` is the only route that exists, and it does not carry the
alarm view under any available option.

### The compile-as-oracle idea — tried, inconclusive, and worth retrying

The owner also asked whether a compile could serve as the export. It cannot *produce* an editable
document, but it is the natural **completeness oracle**: the compiler walks the screen tree and names
objects. Run now, it said nothing about `Screen_1` — **because with errors present the message tree
carries only error branches**, and `Screen_1` is clean.

⚠️ And even on a clean project it is a **weak** oracle: the baseline run named `Button_7` only
because that button *had a warning*. An object that compiles silently is never mentioned. So it can
CONFIRM an omission, never rule one out.

**Retry condition:** delete the `ZZTest_*` mutants, recompile, and check whether the alarm view is
named. Worth doing, but do not treat silence as absolution.

### One rival explanation not yet ruled out from this side

If the project had **unsaved** changes when the export ran, the exporter may have read a model
without the control. Evidence against: `Screen_2` (blank) has **no** `ScreenLayer` while `Screen_1`
has one, and TIA materialises layer 0 when a screen gains content — so content *was* visible to the
exporter at read time. Strong, but it is inference; one word from the owner closes it.

### Hammered — seven independent probes, one lead left standing

Owner's instruction: *"hammer this problem with different things."* Every route the API offers was
tried against `Screen_1` (alarm view, nothing else), after the owner cleared the error screens.

| # | probe | result |
|---|---|---|
| 1 | All three `ExportOptions` (`None`/`WithDefaults`/`WithReadOnly`) | identical object set; flag proven live by an attribute delta |
| 2 | DLL reflection for other `Export` overloads on `Screen` | exactly ONE `Export(FileInfo, ExportOptions)` |
| 3 | `GetCompositionInfos()` | 🔴 **does not exist on classic `Screen`** — failed at COMPILE time. Unified has it; this type cannot even be asked |
| 4 | `GetAttributeInfos()` on the live object | returns **`Name` and nothing else** — thinner than the export, which writes 8 attributes |
| 5 | CLR reflection over the live instance | 2 properties (`Name`, `Parent`), 15 methods, **no collection of any kind** |
| 6 | **Compile, then export** (owner's suggestion) | **byte-identical: 2,953 → 2,953**, same four objects |
| 7 | Compile as a completeness oracle | clean compile (0 errors) names `Root screen` — and says **nothing** about `Screen_1` |

**Probe 6 is the owner's own hypothesis and it is cleanly negative.** A blank-screen control was
exported in the same run so any change would have been attributable; it too was byte-identical.

**Probe 7 confirms the oracle is weak, as predicted.** The compiler names an object only when that
object produces a diagnostic — `Root screen` appears because a button there has a missing-text
warning. An object that compiles silently is never mentioned, so this instrument can CONFIRM an
omission but can never rule one out.

**Probes 3, 4 and 5 together are the strongest form of the negative.** The classic `Screen` is not a
container in the object model at all: it has no composition accessor, no item property, and its own
attribute metamodel advertises a single attribute. Its contents exist only inside TIA's project
store, and `Export` is the sole aperture onto them.

### 🔵 The one lead still standing: `IMasterCopySource`

The CLR reflection turned up something the typed surface hides — the interfaces `Screen` implements:

```
IEngineeringObject, IEngineeringCompositionOrObject, IEngineeringInstance,
IMasterCopySource, IInternalObjectAccess, ..., IEngineeringServiceProvider
```

**`IMasterCopySource`.** A screen can become a **master copy** in a library, and
`ScreenComposition.CreateFrom(MasterCopy)` creates a screen from one — a route the survey already
recorded as one of the three ways a classic screen can come into existence. Separately,
`ScreenLibraryTypeVersion.Export` exists as its own export surface.

**What that would and would not buy:**

- ✅ **Reproduction.** If a master copy captures the whole screen, a screen containing an alarm view
  can be duplicated faithfully — which SimaticML cannot do.
- ❌ **Not authoring.** A master copy is an opaque blob, not a document an AI can write. It cannot
  turn HTML into an alarm view.

**Which is exactly the shape the use case needs anyway:** hand-author the banner once, reuse it
mechanically. Untested — this is the next probe, and it needs new CLI capability (library master-copy
create + `CreateFrom`).

### ✅ The master-copy lead PAID OFF — there is another way, and it is not an export

Built `openness-cli hmi-clone-screen` and ran it against `Screen_1`:

```
MASTER COPY: Screen_1      (project library)
NEW SCREEN:  Screen_3      (Screens.CreateFrom(masterCopy))
```

**`Screen_3` exports byte-identically to `Screen_1` — 2,953 bytes — INCLUDING the `ScreenLayer`.**

| screen | bytes | layer | content |
|---|---|---|---|
| `Screen_2` truly blank | 2,725 | **no** | — |
| `Screen_1` alarm view only | 2,953 | **yes** | the alarm view |
| `Screen_3` clone of S1 | **2,953** | **yes** | *inferred: the alarm view* |

**The inference, stated with its limits:** a `ScreenLayer` is emitted only when a screen HAS content —
`Screen_2` proves the negative. `Screen_1`'s only content was the alarm view. So a layer on
`Screen_3` means the clone carried that content across, even though the exporter cannot name it.
✅ **CONFIRMED BY THE OWNER, 2026-08-17: `Screen_3` has the alarm view.** The corroboration above was
correct, and it is now proof rather than inference — the exporter could not be its own witness, so a
human read the screen in TIA. **The master-copy route reproduces content SimaticML cannot represent.**

### And `hmi-create-screen` was tested rather than assumed

Predicted to refuse; it did, cleanly, **exit 7**:

> *"No WinCC Unified HMI device found in this project. Screen creation is Unified-only — classic HMI
> exposes no screen-item model at all, so there is nothing to create into."*

A good refusal: it names the reason and the structural cause rather than failing obscurely.

### The complete picture for alarm views on Classic Basic

| operation | verdict |
|---|---|
| **author** one via the Openness object model | ❌ impossible — classic has no `ScreenItems`, no `Create` |
| **author** one via SimaticML | ❌ impossible — the format does not represent it |
| **reproduce** one a human made, mechanically | ✅ **works** — library master copy → `CreateFrom` |

**Which is a workable architecture, and it is the standard HMI pattern anyway.** Screens the tooling
generates are composed of the primitives that DO export — Button, Text, IOField, Rectangle, Line,
Circle, Switch, GraphicView. Anything needing an alarm view gets it from a human-authored **template
or master copy**, propagated mechanically. Hand-author once; reuse without limit.

**The emitter's rule follows directly:** it may only claim to produce what SimaticML can represent,
and must hard-error on anything else rather than emitting a screen that silently lacks it.

### 🔴 ANSWERED: a screen carrying an alarm view CANNOT be modified through the file path

The owner's question — *"does that mean we can't modify a screen that has an alarm view on it?"* —
was genuinely open: TIA's `ImportOptions.Override` might **replace** the screen from the document or
**merge** into it, and those give opposite answers. Measured rather than reasoned.

**Method.** Clone `Screen_1` again (keeping `Screen_3` intact as evidence) → `Screen_4`, which
carries the alarm view. Export it, change **exactly one thing** — `BackColor` to pure red — re-import
with `Override`, export again.

| screen | bytes | layer | objects |
|---|---|---|---|
| `Screen_2` truly blank | 2,725 | no | Screen, MultilingualText, MultilingualTextItem |
| `Screen_4` **before** re-import | 2,953 | **YES** | + ScreenLayer |
| `Screen_4` **after** re-import | **2,721** | **no** | Screen, MultilingualText, MultilingualTextItem |

**Control passed:** `BackColor` reads back `255, 0, 0`, so the import definitely applied. Without
that, "nothing survived" would have been indistinguishable from "the import silently failed".

**Verdict: `Override` REPLACES. The alarm view was destroyed.** After the round trip `Screen_4` is
structurally identical to a screen that never had anything on it.

⚠️ **And note what the layer did.** The imported document *did* contain a `<ScreenLayer>` element —
it was `Screen_4`'s own export. TIA dropped it anyway, because after the import the layer held
nothing. So the layer is not merely copied through; it tracks whether the screen actually has
content, which is what makes it a usable signal.

### ✅ Which hands us a PRE-FLIGHT GATE, not just a post-mortem

> **A screen whose export contains a `ScreenLayer` but NO `Hmi.Screen.*` item is carrying content the
> exporter cannot represent. Refuse to modify it.**

Checkable before any write, from the export alone, no Portal round trip. It is **not complete** — a
screen holding a button *and* an alarm view exports the button and the layer, and the alarm view
stays invisible — so it is a floor, not a census. But it converts the commonest case from silent
destruction into a named refusal.

### The architecture this settles

Two rules, and the second makes the first mostly moot:

1. **Never re-import a screen this tooling did not author.** Now measured rather than precautionary:
   the round trip provably destroys unexportable content.
2. **Put alarm views (and anything else unexportable) on the TEMPLATE, never on a generated screen.**
   Generated screens then contain only exportable primitives and are freely modifiable; the banner
   comes from the template they reference, and the export carries that reference
   (`<Template TargetID="@OpenLink">`). This is the standard HMI pattern, not a workaround — and it
   removes the hazard structurally rather than relying on a detector that cannot be complete.

### ✅ DECIDED 2026-08-17 — visible placeholder, for now

**Owner's call:** generated screens carry a **visible placeholder rectangle** reading *"Alarm View
goes here"*; the engineer adds the real control by hand. A cleverer route can come later.

**Why this is the right call now.** It is honest — a placeholder that says what is missing beats
silent omission, and everything else on the table costs build time we do not need to spend to ship a
first screen. *(The template alternative was floated and is dead: the alarm view sits on the screen
directly in the anchor project, not on `Template_1`. That recommendation rested on the retracted
inference above.)*

🔴 **The cost, stated plainly, because it is real and it compounds:** the placeholder invites a
manual edit that the next regeneration **destroys** — `Override` replaces, measured. And a
placeholder makes it *harder* to notice: with a rectangle on the screen the export contains items, so
the `layer-but-no-items` pre-flight gate **cannot fire**. The placeholder hides the loss it causes.

That is not an argument against the decision — it is the list of what has to be true for the decision
to stay safe.

### Two guards that make the placeholder workflow safe, and they are not optional

1. **A HAND-OFF CHECKLIST, not just a rectangle.** The rectangle is a reminder on the screen; the
   checklist is the record. The tool emits, per screen, what must be added by hand and where:
   `screen X: alarm view at 12,340 -> 400x200`. A reminder that exists only inside the artifact is
   lost the moment somebody works from the artifact.

2. **A REGENERATION GUARD.** Stamp each generated screen with a content hash. Before regenerating,
   compare what is in the project against what was generated:
   - identical -> regenerate freely
   - **different -> REFUSE**, and say so: the screen has been hand-finished, and overwriting it
     destroys work the tool cannot see.

   This is `converter diff`'s untouched-network invariance idea transplanted, and it is the guard
   that stops this workflow quietly eating the engineer's effort every cycle. **Without it the
   placeholder is a trap; with it, it is a hand-off.**

**Revisit trigger:** the moment screens are regenerated more than once in practice. One-shot
generate-then-hand-finish is a workflow the placeholder suits well; repeated regeneration is the one
it fails, and the failure is invisible.

### The next experiment, which the owner is best placed to run

Is this alarm-specific, or a **class**? The same A/B on a **trend view**, a **recipe view**, a
**user-view** and a plain **date/time field** would say whether "complex ActiveX-style controls do
not serialize" is the real rule. That determines whether the emitter needs a small blacklist or a
whitelist of what it is allowed to believe.