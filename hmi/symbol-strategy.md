# Equipment symbols — why composing from primitives is the wrong road

**2026-08-17.** Written after the silo/pump/pipe/motor exercise produced results the owner judged
not good enough. The pipeline handled them flawlessly — 47 objects, 871 fields, 0 changed on
read-back — and the *artwork* was still poor. This records why, and what the alternative is.

## The owner's assessment, and where I disagree

> *"you are close on parts but I dont think a better prompt or guidence/tooling can help or if it
> could it would be alot of work and time"*

**Agreed on the first half and it is worth being blunt: no prompt fixes this.** Composing equipment
artwork from axis-aligned rectangles, circles and two diagonals is the wrong mechanism, not a
quality setting. **Disagreed on the second half** — the fix is not better composition, and it is
not expensive, because the mechanism already exists and both corpora already use it.

## What real projects do — from the corpora, not from opinion

| corpus | GraphicView | Polygon |
|---|---|---|
| JOB9003 Classic (5 screens) | **25** | — |
| JOB9002 Unified (48 screens) | **41** | **84** |

Every one of the 25 Classic instances carries a picture reference:

```xml
<Hmi.Screen.GraphicView …>
  <AttributeList> … Width 48  Height 48  AutoSizing StretchPicture … </AttributeList>
  <LinkList>
    <Picture TargetID="@OpenLink"><Name>…</Name></Picture>
  </LinkList>
</Hmi.Screen.GraphicView>
```

Median 70 × 49 px — pump-and-motor sized. **Real projects place pictures. They do not compose
equipment from primitives.**

🔴 **And two of the 25 also carry a `Tag` link — the PICTURE ITSELF changes with a tag.** That is
how a pump goes from stopped artwork to running artwork. Primitives could never do this, because
H-102 forbids colouring them to show state.

**The evidence was in a corpus already walked, and the question was never asked.** The symbol
exercise ran to completion without anyone checking how the reference projects draw a symbol.

## What the web world does — same answer, arrived at independently

AI website builders **do not generate icons. They reference a library.** Tools like v0, Bolt,
Lovable and Cursor build on shadcn/ui, which made **Lucide** (1,600+ SVGs) its default icon set, so
that is what they emit. The visible symptom is sameness — *"open ten AI-built landing pages and they
all use the same icons, including the same little X in the corner of every dialog."*
[[Manish Tamang](https://manishtamang.com/blog/icons-for-vibecoded-sites) ·
[Lucide](https://lucide.dev/)]

### The research says our failure is a known model limitation, not carelessness

From the 2025 vector-graphics literature:

- *"Semantically ambiguous and tokenized representations within LLMs may result in **hallucinations
  in vector primitive predictions**."*
- 🔴 *"LLM training typically lacks modeling and understanding of the **rendering sequence of vector
  paths, which can lead to occlusion between output vector primitives**."*
- Existing methods are *"limited to generating **monochrome icons of over-simplified structures**."*

[[LLM4SVG, CVPR 2025](https://arxiv.org/abs/2412.11102) ·
[VGBench](https://arxiv.org/pdf/2407.10972) · [OmniSVG](https://omnisvg.github.io/)]

**That middle finding is exactly what bit this project twice today.** The brand demo's accent bars
painted *behind* the buttons, and the symbol screen needed layering discipline applied by hand,
declaration by declaration. Occlusion from an unmodelled rendering sequence is a named, documented
limitation of LLM vector generation — not a lapse to be prompted away.

And "monochrome icons of over-simplified structures" is a fair description of the silo.

## 🟢 The asymmetry that makes this EASIER for us than for web design

The library approach has one real cost in web design: **everything looks the same.**

**In HMI that is not a cost. It is the objective.** Industrial symbols are standardised — an
operator should see the same pump on every plant, every panel, every vendor. Sameness is what
"learned once" means. So we get the industry's proven answer without paying its known price, and
Siemens ships a symbol library already.

## ✅ ANSWERED 2026-08-17 — the blocking question, and one premise overturned

Two probes reported: one measured the Openness path against the anchor project, one read Siemens'
primary documentation. **Step 1 below is closed, and step 2 is unblocked.**

**Yes — a picture can be got into a Classic project and referenced from a `GraphicView`.** One screen
with nine picture references imported clean and `hmi-compile` returned **exit 0, ERRORS: 0**, with all
nine links intact on read-back. A **negative control** — one view naming an absent picture — gave
`exit=8, ERRORS: 6, "The graphic for the … screen object is invalid."` Without that control the clean
compile would have proved nothing. Nine formats round-trip **SHA-256 byte-identical**.

Two structural facts worth carrying:

- **Export is a document PLUS a sidecar folder** (`X.xml` + `X files\DefaultImageStream.png`,
  referenced `external="path"`). **A caller that copies only the `.xml` has copied nothing.**
- The typed object exposes only `Name` — **0 compositions** — so image bytes are unreachable through
  the object model, and there is **no `Create`**: import is the only route in. Same shape as the
  Classic screen pipeline, where the document is strictly richer than the API.

### 🔴 The premise that was wrong: vector buys nothing on Basic

The obvious reason to want a vector library — one master scaling across the 4″/7″/9″/12″ range — **does
not exist here.** Siemens, Basic-scoped, verbatim:

> *"All file formats are downloaded to the devices and saved as bitmap (`*.png`/`*.jpg`). Due to the
> conversion, vector formats pixelate, resulting in visible artifacts in different resolutions. It is
> therefore recommended to use several graphics that are customized to the target resolution."*

So SVG is **accepted on import and never reaches the panel as SVG**. Rasterising to a PNG per symbol
per size is not a compromise forced by a failed SVG route — **it is what the vendor recommends.**
Vector-vs-raster is therefore not a library selection criterion; **licence and coverage are.**

*Note how the two probes closed each other's gaps.* The Openness probe found GIF/ICO/TIFF compiling
clean, was not in Siemens' commonly-quoted format list, and **refused to bank it** — flagging that
`hmi-compile` might be validating decodability rather than panel support. The documentation probe then
found the real Basic-scoped list, which **includes** `ico`, `gif` and `tif`: the widely-repeated
BMP/JPEG/PNG/WMF/EMF list is simply incomplete, and the gate was right. An independent measurement and
a primary source agreeing is worth more than either alone.

### The practical trap: transparency

Basic Panels have **no "Transparent" property**, and WinCC replaces a transparent background with the
graphic object's background colour, *"linked firmly with the graphic"*. **Render each PNG onto the
actual screen background colour** rather than shipping alpha and hoping. Consequence to keep in view:
a symbol is bound to the background it was rendered for, so a house-palette change invalidates the
symbol set. Recorded as row 26 of `target-differences.md`.

### The toolchain is already on this machine

**Inkscape 1.4.3** at `C:\Program Files\Inkscape\bin\inkscape.com` (not on PATH), verified by running
it: `--export-type=png --export-width=64 --export-height=64` produced a correct 64×64 RGBA PNG,
headless. Headless Chrome — already the T1 FLATTEN stage — can rasterise too, so symbol rendering can
live in the existing flow rather than adding a dependency. *(`convert` on PATH is Windows' FAT→NTFS
tool, **not** ImageMagick — a false positive worth knowing.)* **Do not bother with EMF/WMF:**
Inkscape's EMF export drops transparency, gradients and text, and the panel rasterises it anyway.

## The recommendation

1. ~~**Probe the graphic-library import path first.**~~ ✅ **Done — it works.** See above.
2. **If yes — emit `GraphicView` + `Picture` link.** Small work: an AttributeList and a one-line
   LinkList. The AI places, sizes and binds; the artwork is drawn once or taken from the Siemens
   library. Same "hand-author once, reuse mechanically" pattern already accepted for the alarm
   view — but unlike that one, not blocked.
3. **Then pursue tag-driven pictures**, since the corpus proves the mechanism carries state.
4. **Polygon is worth adding, but separately.** 84 instances in Unified, and it is what makes a
   *filled* cone possible. It is a better primitive, not a different category — it would improve
   the silo without changing the verdict on composing artwork.

## Which library — recommendation: buy nothing yet

**The cheapest option has never been looked at.** The **WinCC graphics folder ships with TIA and is
already installed**; its contents for a Basic panel have never been inspected, because that needs
Portal. That is the highest-value open item and it should be answered **before any purchase**.

⚠️ Note the distinction that is commonly conflated: the **"Symbol library" OBJECT is not available on
Basic Panels** (Comfort/Panel/RT Advanced/RT Pro only) — but the **WinCC graphics FOLDER** is. Two
different things with different device scope.

If the shipped set proves thin, the ranked answer:

| candidate | licence for our use | verdict |
|---|---|---|
| **WinCC graphics folder** (ships with TIA) | **not stated anywhere** — a grep of all 346 topics for `licen\|copyright\|redistribut\|royalty` found nothing relevant | **evaluate first** — free, installed, unexamined |
| **Symbol Factory Universal**, $695 perpetual, 5,000+ symbols | *"no RUNTIME royalty fees for RUNTIME applications created using images from this image library"*; may not hand the site the library itself | **recommended purchase** — placing rasterised symbols into a private project is the intended use |
| **Classic HMI Template Suite** (Siemens entry 91174767 **V2.1**) | *"Sharing the application examples with third parties… is permitted only in combination with your own products"* — permits delivery | **adopt for CHROME** — explicitly supports WinCC Basic and the KTP900, ships at 800×480. **Not equipment symbols.** Note the *current* Template Suite at that entry is Unified-only; the Basic-capable one is the older V2.1 |
| **Wikimedia ISO 10628-2** | CC BY-SA 4.0 — **copyleft** | reference set only; ShareAlike on a commercial deliverable is a real question I am not qualified to resolve |
| **Ecava IntegraXor**, $99 | *"deliver your project as a whole, but you must not redistribute or resell any SVG file as it is"* | workable fallback; coverage is thin |
| **hmilibrary.com**, ~19 €/mo | **the best written terms of any candidate** | ❌ **not primary — see below** |
| **AggreGate** / **Opto 22** | none stated / none found | ❌ disqualified on licence silence |

### 🔴 Why the best licence is still refused

`hmilibrary.com` was **independently re-confirmed unreliable by fresh evidence**, not merely inherited
from the prior record. Its page presents *"the canonical palette"*, claiming *"Every modern ISA-101
screen lives within this palette"* — and the seven hex values are **Google Material Design** (Red 700,
Orange 700, Yellow 700, Blue 700, Green 800, Grey 500, Grey 700). Its "256×128 or 512×256 standard"
turns out to be **its own product's export spec presented as a standard.**

**A licence is worth what the licensor's title to the artwork is worth**, and a vendor that fabricates
a standards palette gives no basis to assume that title. No company name, registration or VAT number
appears anywhere on the site. The free tier is 12 symbols, so the claimed 900+ is unverifiable without
paying.

⚠️ **One clause to clear in writing before buying Symbol Factory:** the EULA frames permitted use
around *"internal manufacturing facility consumption"*, while the marketing page describes exactly our
case (an integrator building screens for a specific site). Those are not obviously the same scope.
**Get it in writing; do not resolve it by preferring the marketing sentence.**

## A reusable capability found along the way

`docs.tia.siemens.cloud` runs **Fluid Topics** and exposes an **unauthenticated REST API** returning
topic HTML — no browser, no JS wall, no PDF size cap:

```
GET /api/khub/maps
GET /api/khub/maps/<mapId>/toc
GET /api/khub/maps/<mapId>/topics/<contentId>/content
```

Every topic carries a **machine-readable device gate** — the condition TIA itself filters on, which is
far better evidence than a URL slug or a page title:

```
<meta name="Siemens.TIAHelp.HelpScope"
      content="$CLIENT='WINCC' and ($DEVICE='BASIC_PANEL_RUNTIME' or ...)">
```

This is how the Basic-vs-Comfort question got a real answer instead of a plausible one, and it should
be reused for any future "does Basic support X" question. It also removes the blocker that stopped the
Openness manual being read (the PDF exceeds WebFetch's 10 MB cap).

## What is still open

1. **Whether the panel RENDERS any of this at runtime** — only compile-level acceptance is measured.
   Needs a download.
2. **What the WinCC graphics folder actually contains** for a Basic panel — needs Portal. Highest value.
3. **TIA V15–V19** — the docs portal carries only V20/V21. The verbatim list is proven for those two.
   The common *"SVG from TIA V14+"* claim remains **unsourced**; every repetition traces to a site this
   repo has caught publishing wrong data.
4. **Tag-driven pictures** — the corpus's two tag-linked `GraphicView`s were not probed. Basic allows
   100 graphics lists × 30 entries and 500 graphic objects, so there is headroom.
5. **Import size cap for `png` and `svg`** — absent from Siemens' own table.

## What NOT to do

- **Do not iterate on primitive composition.** Two independent lines of evidence — what real
  projects do, and what the research says LLMs cannot yet do — point the same way.
- **Do not build a drawing system.** The expensive thing is teaching a model to draw good vector
  artwork; nobody in either corpus does that either.
