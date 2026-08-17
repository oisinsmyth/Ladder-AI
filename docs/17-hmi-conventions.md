# 17 — HMI conventions

House rules for operator screens, in the shape `06-lad-conventions.md` uses for ladder logic:
numbered, citable, and marked by whether a machine can check them.

**Scope:** WinCC **Classic Basic** panels (KTP series), first application a **Basic 7"**. Rules are
written to generalise to Classic Comfort/Advanced and to Unified, but only the Basic tier is tested.

**Quality bar, in order — function → readability & simplicity → efficiency.** Same as the LAD
preamble, same reason: a screen an operator misreads at 3 a.m. has failed regardless of how it looks
in a review.

## Denominator

**31 rules. 14 CHECKED · 3 STRUCTURALLY ENFORCED · 9 SPECIFIED-BUT-UNCHECKED · 5 ADVISORY.**

🔴 **This line previously read "26 `[MECHANIZED]`", and that was false.** I wrote *mechanized*
meaning *can be mechanized*; the only honest reading is *is mechanized*. Reconciled programmatically
against the rule IDs the checker actually constructs a `Finding` for — which is the
`ReviewRunner.AllRuleIds` lesson this very section warns about, walked into in the file that warns
about it. Twice now: the rule count itself was also wrong on first draft.

| category | n | rules |
|---|---|---|
| **CHECKED** — the tool emits a finding citing this ID | **14** | H-104, H-105, H-201, H-202, H-203, H-205, H-401, H-403, H-404, H-501, H-502, H-503, H-504, H-505 |
| **STRUCTURALLY ENFORCED** — impossible to violate, so no finding exists | 3 | H-405, H-406, H-407 |
| **SPECIFIED, NOT YET CHECKED** — written, citable in review, not automated | 9 | H-101, H-102, H-103, H-107, H-204, H-301, H-302, H-304, H-402 |
| **ADVISORY** — a human decides | 5 | H-106, H-303, H-305, H-408, H-506 |

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
- **H-105** `[MECHANIZED]` **No accent colour in the PROCESS AREA** — no coloured dividers, no
  decorative fills, nothing tinted that is not carrying state.
  🔴 **AMENDED 2026-08-17.** This rule previously read *"no accent colour as chrome — no branded
  title bars"*, which forbade site branding outright. That was doctrine applied past its
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
  least **40°** from red (0°) and from amber (35°). A site whose colour is red or orange cannot
  have it as an accent: an operator glancing at a red header learns nothing, but an operator who has
  learned to ignore a red header has been trained to ignore red. In that case the colour is used
  **desaturated in chrome only, never as an accent**, and the site is told why.

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
  the process area.** It identifies; it does not decorate. If the site supplies only a logo and
  no stated colour, sample the accent from it rather than the dominant colour — a logo's largest
  area is usually its background.

- **H-607** `[ADVISORY]` **One brand colour, not a palette.** Site owners often have several. Taking
  two turns a theme into decoration and starts competing with the alarm palette for meaning.

### ✅ What the panel actually stores — measured, not assumed

The owner noted that a colour is sometimes *"not exactly useable, but I put it into the hmi on tia
and it corrects it into the closest available."* That would matter a great deal here: a brand colour
silently snapped to a neighbour is a promise to a site that the panel quietly breaks.

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

## What no checker covers, and it is deliberate

The two rules that matter most — **is this the right information** and **would an operator act
correctly on it at 3 a.m.** — are not in this document, because they cannot be mechanized and
pretending otherwise inflates the denominator. They belong to the reviewing engineer.

A green run of every rule above means the screen breaks no house convention. **It does not mean the
screen is good.**
