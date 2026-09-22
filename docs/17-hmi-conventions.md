# 17 — HMI conventions

House rules for operator screens, in the shape `06-lad-conventions.md` uses for ladder logic:
numbered, citable, and marked by whether a machine can check them.

**Scope:** WinCC **Classic Basic** panels (KTP series), first application a **Basic 7"**. Rules are
written to generalise to Classic Comfort/Advanced and to Unified, but only the Basic tier is tested.

**Quality bar, in order — function → readability & simplicity → efficiency.** Same as the LAD
preamble, same reason: a screen an operator misreads at 3 a.m. has failed regardless of how it looks
in a review.

## Denominator

**39 rules. 14 CHECKED · 3 STRUCTURALLY ENFORCED · 12 SPECIFIED-BUT-UNCHECKED · 10 ADVISORY.**

*Was 31 (14 · 3 · 9 · 5) before the **H-7xx platform block** was added 2026-08-17. **The CHECKED
count did not move**: H-7xx adds three SPECIFIED-NOT-CHECKED and five ADVISORY, and the checker
constructs a `Finding` for none of them. Counted by hand against the table below and reconciled
with it — not grepped, per the warning three lines down.*

🔴 **This line previously read "26 `[MECHANIZED]`", and that was false.** I wrote *mechanized*
meaning *can be mechanized*; the only honest reading is *is mechanized*. Reconciled programmatically
against the rule IDs the checker actually constructs a `Finding` for — which is the
`ReviewRunner.AllRuleIds` lesson this very section warns about, walked into in the file that warns
about it. Twice now: the rule count itself was also wrong on first draft.

| category | n | rules |
|---|---|---|
| **CHECKED** — the tool emits a finding citing this ID | **14** | H-104, H-105, H-201, H-202, H-203, H-205, H-401, H-403, H-404, H-501, H-502, H-503, H-504, H-505 |
| **STRUCTURALLY ENFORCED** — impossible to violate, so no finding exists | 3 | H-405, H-406, H-407 |
| **SPECIFIED, NOT YET CHECKED** — written, citable in review, not automated | 12 | H-101, H-102, H-103, H-107, H-204, H-301, H-302, H-304, H-402, **H-702, H-703, H-704** |
| **ADVISORY** — a human decides | 10 | H-106, H-303, H-305, H-408, H-506, **H-701, H-705, H-706, H-707, H-708** |

**The authoritative CHECKED set is the checker, never this table.** Regenerate rather than retype:
the reconciliation reads every `new Finding("H-nnn", …)` out of `src/hmi-cli/`. A grep of this
document counts mentions, which is how the number got inflated in the first place.

**"Specified, not yet checked" is a promise, not a resting place.** Each of those nine is a
gap to close, and H-107 (the red-STOP carve-out) and H-403 (≥ 20 mm) are the pair that together form
the physical-safety rule — H-403 is checked, H-107 is not.

## These rules GUIDE. The engineer DIRECTS.

**Owner's ruling, 2026-08-17:** *"I want the colour and design decisions to be overwriteable, these
are to guide the AI but can be overwriten when directed by the user."*

That is right, and it is why this document exists at all: a generator has no taste and needs rules;
an engineer has taste and needs a tool that gets out of the way. But *overridable* is not one thing,
so the rules are in three classes and each behaves differently.

| class | example | override |
|---|---|---|
| **DESIGN** | H-104 no green for running, H-2xx form, H-3xx text, H-6xx branding | ✅ name the rule, that is all |
| **SAFETY** | H-401/H-403 touch targets, H-404 spacing, H-503 overlap, H-107 red STOP, H-605 | ⚠️ allowed, but a **stated reason is required** |
| **CORRECTNESS** | H-501 off-canvas, H-502 sub-pixel, H-505 degenerate | 🔴 **refused** — the screen is *broken*, not *different* |

The distinction is not bureaucratic. Green-for-running is a judgement this project holds and a
site's own standard may legitimately contradict it. A control too small to hit under pressure is
a different kind of claim, and setting it aside should cost one sentence. A rectangle drawn off the
edge of the panel is not a preference at all — there is nothing to prefer.

### How

On the element:

```html
<div data-hmi="Text" data-hmi-override="H-104">RUNNING</div>
<button data-hmi="Button" data-hmi-override="H-401: stylus-operated settings field, per site standard">
```

**Every override is REPORTED**, honoured or refused:

```
OVERRIDDEN  H-104 on item 3 (Design)
OVERRIDDEN  H-401 on item 5 (Safety) - stylus-operated settings field, per site standard
```

**Never silent.** A suppression nobody can see is indistinguishable from a checker that does not
work — which is the failure this whole document keeps closing elsewhere. The override count is part
of the result, so a screen that passes by disabling half the rules cannot look like a screen that
passes.

### Proven, not asserted

Five cases measured 2026-08-17: a design rule fires and is overridable by ID alone; a safety rule is
**refused without a reason** and honoured with one; a correctness rule is **refused outright** even
with a reason attached. A mechanism nobody has seen refuse is not a mechanism.

## Provenance

Seeded from the eight clauses used in the rev-7 research experiment, which measured that a rules
layer costs nothing and fixes screen appearance entirely, where every arm lacking one produced
green-for-running and accent chrome unprompted. Sizing comes from `hmi/sizing-standard.md`
(datasheet-derived, corroborated against real engineering practice on the anchor project).

**H-7xx (2026-08-17)** comes from a different direction: **an existing panel specification written
for a Classic Comfort target, re-cut for Classic Basic.** The absences it names were found by
working a full screen set against the tier rather than by reading a feature matrix, which is why
each rule is phrased as *what an author must do instead* rather than as *what the tier lacks*.
Corroborated against this repo's own measured Classic Basic facts — `hmi/target-differences.md`
(no screen windows; layers exist and export), `hmi/design-evidence.md` (no composition, zero
scripts across a 48-screen reference corpus) and `hmi/sizing-standard.md` (the px/mm pair).

---

## H-1xx — Colour

- **H-101** `[MECHANIZED]` The screen field is **low-chroma grey**. Never white, never a dark theme.
  Saturation ≤ 0.18 and lightness between 0.05 and 0.97 for any large background area.
- **H-102** `[MECHANIZED]` **Colour is reserved for the abnormal.** A plant running normally is grey.
  If everything is fine and the screen is colourful, the colour carries no information.
- **H-103** `[MECHANIZED]` The only alarm colours are **red** (high / trip) and **amber** (low /
  warning). No third alarm colour, no per-area palettes.
- **H-104** `[MECHANIZED]` **No green for running.** Running is the normal state; by H-102 it is grey.
  This is the single most common violation and every unguided generation produced it.
  ⚠️ **This rule is about STATE, not about command identity.** A *static* green marker on a START
  button is permitted and expected — see **H-108/H-109**. What is forbidden is a green that means
  "this is running", i.e. one that appears, disappears or changes with the plant.
- **H-105** `[MECHANIZED]` **No accent colour in the PROCESS AREA** — no coloured dividers, no
  decorative fills, nothing tinted that is not carrying state.
  🔴 **AMENDED 2026-08-17.** This rule previously read *"no accent colour as chrome — no branded
  title bars"*, which forbade Site branding outright. That was doctrine applied past its
  purpose: colour is rationed because it is an ALARM channel, and the header is not where alarms
  live. Branding in chrome costs the operator nothing. Branding in the process area costs them the
  channel. The rule now says which is which — see **H-6xx**.
- **H-106** `[ADVISORY]` Colour is never the *only* channel. Anything colour distinguishes must also
  differ in text, position or shape — for colour-vision deficiency and for a sun-washed panel.

### 🔴 H-107 — the STOP carve-out

- **H-107** `[MECHANIZED]` **A STOP or E-STOP control is red, and this overrides H-102 and H-103.**

  Recorded with its reasoning because it is a deliberate exception rather than an oversight. The
  research's own rules-driven arms **independently objected**, twice, that forbidding a red STOP was
  wrong: plant convention overwhelmingly expects red for a safety-critical stop, and a grey STOP
  "will feel wrong to anyone trained on red-stop panels". They were right.

  **Scope is exactly STOP / E-STOP controls.** It is not "red is allowed for commands" — that would
  reopen H-102 entirely. Pairs with **H-403** (≥ 20 mm): the physical-safety rule is size *and*
  colour together.

  *Owner sign-off recorded 2026-08-17 — this is safety-adjacent convention, so it is the engineer's
  call and not the tool's.*

### H-108 / H-109 — command accents, and what "accent" actually means

**Owner, 2026-08-17:** *"start is always accented with green, stop is always accented with red and
reset is always accented with a light blue. When I say accent I mean that, while the button is a
slightly different shade from the background there is a small symbol or something similar with the
accent colour."*

That definition is the whole reason this is compatible with H-102 and H-104, so it comes first.

- **H-109** `[MECHANIZED]` **An ACCENT is a SMALL MARKER, never a FILL.** The control's body is a
  shade of the grey field — slightly lifted, so it reads as pressable — and the accent appears as a
  **small symbol, bar or dot**, occupying a minor fraction of the control (**≤ 15% of its area**).
  🔴 **A control whose body is the accent colour is a FILL, not an accent, and is forbidden.** The
  difference is not stylistic: a filled green button is a coloured region competing with the alarm
  palette; a small green marker on a grey button is a label.

- **H-108** `[MECHANIZED]` **Command accents are fixed by FUNCTION, not chosen per screen:**

  | command | accent | why it is not a state colour |
  |---|---|---|
  | START / RUN | **green** | marks *which control starts the plant*. It does not mean "running" |
  | STOP / E-STOP | **red** | H-107. Plant convention, and the strongest one there is |
  | RESET / ACKNOWLEDGE | **light blue** | distinct from both, and from every alarm hue |

  These are constant on every screen, in every project, and **never vary with plant state**. An
  operator learns three shapes once.

### Why this does not contradict H-102 or H-104

H-104 forbids **green for running**. That is a rule about **state**: a green region that appears when
a pump runs and disappears when it stops is carrying process information in the alarm channel.

A green marker on a START button is not that. It is **static** — it is there when the plant is
running, stopped, or faulted — so it carries **no state at all**. It identifies the control, the way
the word START does. Two properties make it safe, and both are required:

1. **It never changes.** Anything that changes with the plant is state, and state belongs to the
   alarm palette.
2. **It is small.** A marker is read as a label; a fill is read as a region.

Lose either and it becomes exactly what H-104 forbids. **H-108 accents are permitted precisely
because they are the least informative thing on the screen** — which is what lets the eye stop
reporting them and keeps the alarm channel loud.

### Interaction with brand colour (H-6xx)

Command accents are **reserved** and outrank the brand. A site's blue may theme the chrome and
mark the primary command (H-604), but it **never** replaces the green on START, the red on STOP or
the light blue on RESET — those three are the operator's vocabulary, not the site's.

⚠️ **An operator whose brand colour is a green, a red or a light blue therefore collides with the
command set as well as the alarm set.** H-602 already refuses red and amber; this extends the same
reasoning: a brand accent must be distinguishable from the command accents too, or the operator has
two meanings for one colour.

## H-2xx — Form

- **H-201** `[MECHANIZED]` No gradients.
- **H-202** `[MECHANIZED]` No drop shadows or text shadows.
- **H-203** `[MECHANIZED]` No corner radius. `CornerRadius = 0`, `CornerStyle = Pointed`.
- **H-204** `[MECHANIZED]` No 3-D, bevel or embossed edges. On Classic this means **`EdgeStyle` is
  not `Style3D`** — note the panel default *is* `Style3D`, so this rule fires on unmodified defaults
  and that is intended.
- **H-205** `[MECHANIZED]` **No animation except an unacknowledged alarm.** `Flashing = None`
  everywhere else. Motion on an operator screen is an alarm channel; spending it on decoration
  spends the operator's attention.

## H-3xx — Text

- **H-301** `[MECHANIZED]` Dark text on the grey field.
- **H-302** `[MECHANIZED]` Process values use **tabular (monospaced) numerals**, so a changing value
  does not reflow and the eye can hold position.
- **H-303** `[ADVISORY]` Text is sized for the *viewing distance*, not the screen. A panel read at
  arm's length and one read across an aisle are different problems.
- **H-304** `[MECHANIZED]` Every value carries its **engineering unit**. A bare number is a defect.
- **H-305** `[ADVISORY]` Labels say what the operator calls the thing, not what the tag is called.
- **H-306** `[CHECKED]` `[CORRECTNESS]` 🔴 **A text box must be tall enough for the text in it.**
  Minimum height is `1.35 × FontSize` for a Text or Button and **`1.92 × FontSize` for an IOField** —
  a field carries margins, a border and a focus rectangle that a plain label does not.

  **The floors are MEASURED, not derived from font metrics.** Height ÷ FontSize across 66
  text-bearing objects in a real TIA export:

  | | n | min | median | max |
  |---|---|---|---|---|
  | TextField | 37 | **1.35** (23/17) | 1.54 | 1.54 |
  | IOField | 23 | **1.92** | 1.92 | 2.00 |
  | Button | 6 | 2.67 | 6.67 | 6.67 |

  The floor is the corpus **minimum**, not its median: below it is a size no real screen uses, which
  is a defensible line — above it is a density judgement, and that belongs to the author.

  **Found by the owner looking at built screens**, not by any check: *"the textboxes have been sized
  too small compared to the text they hold and has resulted in cut-off text at the bottom."* It was
  systematic — 36 of 62 items on one screen, 70 of 84 on another — and every geometry rule passed,
  because the boxes were a legal size, on the canvas and not overlapping **while the glyphs were
  clipped**. Nothing in H-5xx measures a box against its contents.

  **CORRECTNESS, so it cannot be overridden.** Clipped text is broken on any panel, to anyone's
  taste — the same class as off-canvas.

  ⚠️ **It bites density, and that is usually the right trade.** Raising an IOField from 24 px to 33
  px at font 17 costs a row per band. On the screens where it first fired, the owner's other
  complaint was that they were *too crowded* — so the two findings had one fix, and losing rows was
  the cure rather than the cost. Where a band genuinely cannot lose one, a smaller font in the same
  box is the fallback; prefer losing the row to making an operator squint.

## H-4xx — Physical sizing

Full derivation, per-panel pixel tables and the measured corroboration: **`hmi/sizing-standard.md`**.

- **H-401** `[MECHANIZED]` Every interactive object is **≥ 9 mm on its minor axis**, measured on the
  **smallest panel in the target family**.
- **H-402** `[MECHANIZED]` Commands default to **12 mm**; primary or frequent actions to **15 mm**.
- **H-403** `[MECHANIZED]` Safety-critical controls (STOP / E-STOP) are **≥ 20 mm**. See H-107.
- **H-404** `[MECHANIZED]` Interactive objects are separated by **≥ 3 mm**, **5 mm** by default.
- **H-405** `[MECHANIZED]` **Thresholds are declared in millimetres and converted with the target
  panel's px/mm. A hard-coded pixel threshold is a defect whatever its value.** This is the rule that
  keeps H-401…H-404 honest across panels — 48 px is 9 mm on exactly one panel this project touches.
- **H-406** `[MECHANIZED]` **px/mm is a PAIR, not a scalar** — Basic panels do not have square
  pixels, and the vertical figure is the tighter one. The minor-axis check uses the vertical value.
- **H-407** `[MECHANIZED]` The screen artifact **declares the panel it was sized for**. An artifact
  that does not is a refusal, not a default: the anchor and the target share a resolution but differ
  ~28% physically, so an assumed panel is a quarter-scale error that passes every pixel check.
- **H-408** `[ADVISORY]` Gloved operation moves every band up one. A site fact, not a design choice —
  establish it before laying out.

## H-6xx — Site branding

Site owners supply a logo and expect their identity on the panel. That is a real requirement, not a
concession, and it is compatible with everything above **provided colour keeps meaning what it
means**. These rules generalise a practice the owner has used for years: take the site's
accent colour — from their logo, or their website — and apply it as a theme.

The whole scheme rests on one distinction:

> **CHROME** identifies the plant and the screen: header, footer, title bar, navigation.
> **PROCESS AREA** shows what the plant is doing.
> Brand colour lives in chrome. Colour in the process area still means abnormal, and nothing else.

- **H-601** `[MECHANIZED]` **Brand colour is permitted in CHROME ONLY** — header, footer, title bar,
  navigation rail, and the selected state of navigation. The process area stays grey per H-102.
  Chrome and process must be declared (`data-hmi-zone="chrome|process"`), because a checker cannot
  infer which is which from geometry and must not guess.

- **H-602** `[MECHANIZED]` **The brand colour must be hue-separated from the alarm bands** — at
  least **40°** from red (0°) and from amber (35°). An operator whose colour is red or orange cannot
  have it as an accent: an operator glancing at a red header learns nothing, but an operator who has
  learned to ignore a red header has been trained to ignore red. In that case the colour is used
  **desaturated in chrome only, never as an accent**, and the site owner is told why.

- **H-603** `[MECHANIZED]` **Text on the brand colour meets a 4.5:1 contrast ratio.** A brand colour
  is chosen for a logo on white, not for legibility behind 15 px type at arm's length on a sunlit
  panel. Derive the chrome text colour from the brand colour; never assume white works.

- **H-604** `[MECHANIZED]` **Brand accent may indicate PRIMACY, never STATE.** It may mark which
  command is the primary one on a screen — **at most one per screen** — and may never encode
  running, stopped, faulted, selected-equipment or any plant condition. Primacy is a property of the
  screen's design; state belongs to the alarm palette.

- **H-605** `[MECHANIZED]` **A STOP or E-STOP control is never brand-coloured.** H-107 owns that
  control and it is red. This holds even where the site's colour is a red — the two would be
  indistinguishable, which is the whole objection.

- **H-606** `[ADVISORY]` **The logo is a graphic, placed in chrome, sized so it never competes with
  the process area.** It identifies; it does not decorate. If the site owner supplies only a logo and
  no stated colour, sample the accent from it rather than the dominant colour — a logo's largest
  area is usually its background.

- **H-607** `[ADVISORY]` **One brand colour, not a palette.** Site owners often have several. Taking
  two turns a theme into decoration and starts competing with the alarm palette for meaning.

### ✅ What the panel actually stores — measured, not assumed

The owner noted that a colour is sometimes *"not exactly useable, but I put it into the hmi on tia
and it corrects it into the closest available."* That would matter a great deal here: a brand colour
silently snapped to a neighbour is a promise to a site owner that the panel quietly breaks.

**Measured 2026-08-17 and it does not happen on this path.** Twenty swatches were emitted, imported
and re-exported, including values chosen specifically to move if the panel used the usual 16-bit
RGB565 format — `1,1,1`, `3,5,7`, `100,101,102`, `250,251,253`. **All twenty came back byte-identical.**

So **TIA stores the exact 24-bit colour**, and the generator may state any colour it likes without
modelling a palette.

⚠️ **This does NOT mean the operator sees that colour.** The test covers import and re-export only.
Three places downstream remain unmeasured and any of them could be where the owner's observation
comes from:

- TIA's own **colour picker**, which may snap a hand-entered value to a palette — note the import
  path bypasses the picker entirely, so **the generator may preserve a brand colour more faithfully
  than typing it in does**
- the **download** to the panel
- the **panel hardware's** own colour depth

**So state the emitted colour as exact in the project, and treat display fidelity as unverified.**
Confirming it needs a photograph of a real panel, which no amount of file comparison can substitute
for.

### Why this is safe, stated plainly

The alarm channel is untouched: red and amber keep their meaning in the process area, where an
operator looks when something is wrong. The brand colour appears in a fixed, predictable band that
carries no state and never changes — which is precisely why the eye stops reporting it, and exactly
what you want from chrome.

**The failure mode this guards against** is the brand colour drifting inward: first the header, then
a divider, then a "highlight" on a running pump. At that point the operator has two colour systems
to interpret and the alarm palette has lost its monopoly on meaning. H-601 and H-604 are the fence.

## H-5xx — Geometry

- **H-501** `[MECHANIZED]` Nothing extends beyond the screen bounds.
- **H-502** `[MECHANIZED]` All positions and sizes are integers.
- **H-503** `[MECHANIZED]` No two interactive objects overlap.
- **H-504** `[MECHANIZED]` No alignment near-miss: an edge within 3 px of a shared alignment line
  shared by ≥ 2 objects is either on it or deliberately off it.
- **H-505** `[MECHANIZED]` No zero-size or degenerate objects. *(Exception: item types that carry no
  geometry at all — `SoftKey` on Classic maps to a physical bezel key. Absent geometry is not
  zero geometry.)*
- **H-506** `[ADVISORY]` No large dead band. Unused area is acceptable; a screen that is mostly
  unused area is usually a screen that should be two screens or one smaller one.

---

## H-7xx — Platform capability

**Added 2026-08-17**, from re-platforming an existing panel specification written for a **Classic
Comfort** target onto **Classic Basic**. Everything below is a property of the tier, not of any
project.

**Why these are house rules and not a datasheet.** A Comfort specification transfers to Basic
*looking* correct — same resolution, same tags, same alarm design — and fails on four structural
absences that no import error and no compile reports. **Each rule here is one of those absences
turned into something an author can be held to.**

> ⚠️ **The failure mode this block exists to prevent: a specification that was RIGHT on the tier it
> was written for, carried across unchanged, and wrong in ways that look like design choices.**

### The four absences

| | Absent on Classic Basic | Consequence |
|---|---|---|
| **Composition** | no screen-window item type — screens cannot embed screens | every screen carries its own chrome — **H-705** |
| **Faceplates** | no parameterised reusable object | a repeated object is a **copy** — **H-704** |
| **Popup screens** | a popup is a **layer**, shown by a condition | it belongs to one screen — **H-701 · H-702 · H-703** |
| **Scripting** | the panel displays and writes tags | it computes nothing — **H-706** |

### The rules

- **H-701** `[ADVISORY]` **A popup is a LAYER on one screen, and is never global.** There is no
  screen window to host a panel-wide dialog, so a popup can appear only on screens whose layers
  carry it. **Any alert that must reach an operator who is looking at a different screen is carried
  by CHROME — an announcement that names its subject and navigates — never by the popup.**
  ⚠️ **Replicating the layer onto every screen is the alternative and it is usually wrong**: it
  multiplies the object by the screen count, and **two conditions true at once means two layers
  drawn over each other with no ordering**, because there is no dialog manager to queue them.

- **H-702** `[MECHANIZABLE, NOT YET CHECKED]` **Every object on a popup layer carries the SAME
  visibility condition, driven by ONE control tag.** The popup is only as hidden as its
  least-hidden object. **An object with no condition, or a different one, is a fragment of a dialog
  floating over a live screen** — and it reads as a rendering glitch rather than a logic error,
  which is why nobody finds it by looking. **Check by counting: objects on the layer against objects
  carrying the condition.**

- **H-703** `[MECHANIZABLE, NOT YET CHECKED]` 🔴 **A SHOWN LAYER DOES NOT DISABLE WHAT IS BENEATH
  IT. Modality has to be built, and it takes both halves:** an **opaque backing rectangle** covering
  the whole content area, **and** a **disable condition on the controls underneath**, true while the
  popup condition is true.
  ⚠️ **The backing rectangle alone is not enough** — a covered control may still take the touch,
  and that is precisely the case where a press does something the operator cannot see. **Verify by
  pressing through a shown layer; it is not provable by inspection.**

- **H-704** `[MECHANIZABLE, NOT YET CHECKED]` **There are no faceplates: a repeated object is a
  COPY.** Author **one canonical group** per equipment class, copy it, and **make the per-instance
  difference nothing but the tag path.** Any difference in geometry, colour, elements or logic is a
  divergence **that will not be found by looking**, because nobody compares a dozen small objects
  across four screens.
  ⚠️ **Where instances genuinely differ in content, make a SECOND canonical group — never one group
  carrying an element that is unbound on most copies.** The unbound element is the one somebody
  eventually binds.
  **Two checks that exist only because there is no faceplate:** count the copies and confirm each
  binds a *different* instance — **two copies bound to the same source is the failure that hides** —
  and diff every copy against the canonical group.

- **H-705** `[ADVISORY]` **Every screen is self-contained and carries its own chrome.** Where a
  composing platform pays for a header once, this one pays per screen. **Consistency is therefore an
  authoring discipline, not a structural guarantee, and it is the first thing to drift.**
  ⚠️ **The template — a single shared layer behind every screen — is the only shared-chrome device
  the tier has. It is far weaker than composition and its carrying capacity for BOUND and
  INTERACTIVE content is unproven here: test it with one bound value and one button before
  committing chrome to it.**

- **H-706** `[ADVISORY]` 🔴 **THE PANEL DISPLAYS AND WRITES TAGS. IT DOES NOT COMPUTE.** With no
  scripting, **any derived value belongs in the PLC or does not exist.** That covers more than it
  first appears:

  | wanted | on this tier |
  |---|---|
  | a roll-up, a maximum, any arithmetic across tags | 🔴 **not available** — put it in the PLC |
  | a read/compare/retry protocol, a last-good copy, a reject counter | 🔴 **not available** |
  | a watchdog detecting that a counter has STOPPED | 🔴 **not available** — connection loss is still detected; *"peer alive, publishing stopped"* is not |
  | ordered writes on a press | ✅ an ordered function list on the button |
  | ⚠️ writes guaranteed to land in **separate transport jobs** | ⚠️ **not guaranteed — prove it, or make the receiving logic tolerant** |
  | a periodic increment, e.g. a heartbeat | ✅ a cyclic Scheduler task — ⚠️ **confirm the function exists on the tier** |
  | an unknown enum rendering as its own number | ⚠️ **depends on the text list's out-of-range behaviour — verify** |

  ⚠️ **A derived value that the PLC cannot supply either does not become a panel feature by being
  wanted.** Say so, and display the raw inputs instead — **a number a human can see has stopped
  moving beats a derived indicator that cannot be built.**

- **H-707** `[ADVISORY]` **Where the touch minimums make a control-dense screen impossible, use
  SELECT-then-ACT** — many small **indicators** (not touch targets, so they may be small), one
  **selection**, and **one full-size action pair acting on the selection.**
  🔴 **Never shrink below the H-401 floor to make a layout fit.** A matrix of *n* items with two
  actions each needs `2n` controls at a ≥ 12 mm pitch, and the arithmetic runs out quickly on a
  small panel — **at which point the layout is wrong, not the rule.**
  ✅ **It also buys a deliberate step before an act that moves plant**, which on a dense control
  page is a feature rather than a cost.

- **H-708** `[ADVISORY]` 🔴 **PANEL CAPABILITY FIGURES ARE ESTABLISHED FOR THE TARGET PANEL BEFORE
  DESIGN, AND NEVER CARRIED ACROSS FROM ANOTHER TIER.** At minimum: **the tag limit, the maximum
  discrete alarm count, the number of alarm CLASSES and whether they can be created, and whether
  the alarm history persists across a power cycle.**
  ⚠️ **These are the figures most likely to be inherited silently from a higher-tier specification,
  and two of them can invalidate a design rather than trim it:** a tag limit below the design's
  mandatory total means something must be cut, and **a volatile alarm buffer where an archive was
  assumed can mean a plant with no durable record of anything at all.** ⚠️ **Merging alarm classes
  to fit is an engineering decision about which distinctions an operator loses — not a
  configuration detail.**

### The sizing correction this block came from, worked once

**A pixel constant inherited from another panel is the same class of error as an inherited
capability figure**, and it is the one that passes every mechanical check — see **H-405**.

Converting the mm thresholds with the **7-inch Basic** target's own px/mm pair
(`hmi/sizing-standard.md`: **H 5.19 · V 5.59**, and note **H-406** — the vertical figure binds):

| rule | mm | px H | px **V** ← binding |
|---|---|---|---|
| H-401 minor axis | ≥ 9 | 47 | **51** |
| H-402 command | 12 | 63 | **68** |
| H-402 primary/frequent | 15 | 78 | **84** |
| H-403 safety-critical | ≥ 20 | 104 | **112** |
| H-404 separation min / default | 3 / 5 | 16 / 26 | **17 / 28** |

> ⚠️ **A 40 px touch target — a common constant on larger-format panels — is 7.7 × 7.2 mm here, and
> BELOW THE FLOOR ON BOTH AXES.**

**These pixels are for that panel only and are not constants.** Re-derive for any other target;
that is H-405's whole point, and the table is here as a worked example rather than a lookup.

⚠️ **Consequence for layout, worth stating once:** a chrome band carrying touch targets cannot be
40 px, so **chrome and navigation both grow, and the content area shrinks by roughly 10%** against
a specification written for a larger-format panel. **On this tier none of it is recoverable by
composition (H-705).** ✅ **Bezel `SoftKey` items carry no geometry and cost zero pixels** — moving
navigation onto them returns the whole navigation band, and a bezel key stays reachable while a
popup layer covers the glass, which is exactly when a HOME control is most needed.

---

## What no checker covers, and it is deliberate

The two rules that matter most — **is this the right information** and **would an operator act
correctly on it at 3 a.m.** — are not in this document, because they cannot be mechanized and
pretending otherwise inflates the denominator. They belong to the reviewing engineer.

A green run of every rule above means the screen breaks no house convention. **It does not mean the
screen is good.**
