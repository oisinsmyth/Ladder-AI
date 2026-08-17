# Design evidence from real projects

**Gathered 2026-08-17** from two corpora, read-only, on the owner's instruction. Structural and
geometric facts only — no screen names, tag names, label text or equipment names, per
`docs/13-data-boundary.md` (2026-08-17).

| corpus | family | scale |
|---|---|---|
| **JOB9002** | Unified | 48 screens, 1,404 items read, 233 tags, 311 discrete alarms, **0 scripts** |
| **JOB9003** | Classic Basic 800×480 | 5 screens, 176 items with geometry |

## 🔴 How to read this, and it is the owner's own rule

> *"The design in those projects are probably not all good or a perfect example, but they are
> directionally correct. So just because its in a project doesn't make it right."*

So everything below is sorted into three bins, and the third is the important one:

- **DELIBERATE** — repeated consistently enough that it is clearly a decision.
- **OBSERVED** — present, but frequency alone cannot distinguish a convention from a habit that
  was copy-pasted. Reported, not promoted to a rule.
- **CONTRADICTS A FLOOR** — where the corpus and an established principle disagree. The principle
  wins; the disagreement is recorded rather than resolved silently.

---

## 1. The finding that reframes everything: screens are COMPOSED, not drawn

**48 screens is not 48 screens.** It is **14 full-size screens and 34 sub-screens**, assembled at
runtime through `ScreenWindow` items.

The proof is that window sizes match available screen sizes *exactly*:

| ScreenWindow size | matching screens in the project |
|---|---|
| 1280 × 110 | 4 |
| 1280 × 615 | 8 |
| 640 × 570 | 3 |
| **300 × 430** | **18** |

And the full-size geometry decomposes cleanly:

```
1280 × 800   full panel
   110       header band        (a screen, embedded)
   615       content area       (a screen, embedded)
    75       remainder / footer
```

`110 + 615 + 75 = 800`. That is not a coincidence — it is a layout system, where a screen is a
frame that hosts interchangeable content, and 18 interchangeable dialogs share one 300 × 430 slot.

**Classification: DELIBERATE.** Nobody produces an exact size match across four distinct window
sizes and eighteen same-sized dialogs by accident.

**Why it matters to us:** our generated screens are monolithic — every item drawn onto one canvas.
This corpus says a competent project of any size is **a frame plus a library of panes**. That is a
structural convention `docs/17` says nothing about, and it is probably the single biggest gap
between what we generate and what an engineer would build.

### 🔴 CONFIRMED UNIFIED-ONLY — and this is a hard constraint on the current target

**Owner, 2026-08-17:** *"the classic can't have subscreens. So while I would like you to use them at
a later point in a unified panel, they can't be used in a basic panel."*

So §1 describes a technique that is **unavailable on the KTP700 target**, and the Classic item
census agrees — no window type appears anywhere in it. The finding is retained because it is where
the programme is going, not where it is.

**The consequence for Basic is bigger than it looks, and runs the wrong way.** If screens cannot
compose, then **every Basic screen is self-contained** and must carry its own chrome — title bar,
navigation, status line — out of its own 800 × 480. Where Unified pays for a header once and embeds
it into fourteen screens, Basic pays for it fourteen times, out of a canvas that is already about a
third of the area.

That tightens an already tight budget: the usable field after chrome was measured at 800 × 380, and
none of that is recoverable through composition. It also means **repetition across screens is
unavoidable on Basic**, so consistency has to be enforced by the generator rather than by reuse —
there is no shared object to get right once.

⚠️ **The one Classic mechanism that might substitute is the TEMPLATE** — a single shared layer behind
every screen. It is far weaker than arbitrary embedding (one layer, not a library of panes) but it
is the only shared-chrome device Classic has. **Untested by us**, and note the alarm view on the
anchor sits on the screen rather than the template, so the template's actual carrying capacity is
unknown.

## 2. Density

| | items per screen |
|---|---|
| JOB9002, all 48 screens | min 1, **median 19**, max 185 |
| JOB9002, full-size screens only | 485 items over 10 screens, ~48 each |
| JOB9003 Classic, 5 screens | 6, 6, 11, 61, 97 |

**Classification: OBSERVED.** The spread is enormous and both corpora contain both near-empty and
very dense screens. What it does establish is a **sanity range**: our generated screens carry 10–17
items, which sits comfortably inside normal practice rather than looking sparse. The 185-item and
97-item screens are the outliers, not our 12.

## 3. The item vocabulary is small and text-dominated

| JOB9002 (Unified) | n | | JOB9003 (Classic) | n |
|---|---|---|---|---|
| Text | 421 | | Rectangle | 42 |
| Circle | 301 | | TextField | 37 |
| Rectangle | 254 | | Circle | 30 |
| Button | 147 | | GraphicView | 25 |
| Line | 94 | | IOField | 23 |
| Polygon | 84 | | Line | 14 |
| GraphicView | 41 | | Button | 6 |
| IOField | 38 | | Switch | 3 |

**Classification: DELIBERATE.** Twelve distinct types across 1,404 items, and the top four account
for the large majority. **Text is the most common item in both corpora.** Our emitter supports
Rectangle, Text, Button, Line and Circle — which covers the top five of both lists.

**The notable absence is `Circle`: 301 in Unified, 30 in Classic, at a median of 11 × 11 px.** Those
are status lamps, and they are the second most common item in the Unified corpus. We support the
type but have never used one, because nothing in `docs/17` suggests indicators as a primitive.

## 4. Recurring sizes

**Classification: DELIBERATE** where a mode dominates, **OBSERVED** otherwise.

| JOB9002 type | median W × H | modal size | count at mode |
|---|---|---|---|
| Button | 100 × 60 | **60 × 60** | 54 of 147 |
| Rectangle | 100 × 50 | **100 × 50** | 73 / 52 |
| IOField | 160 × 40 | 160 × 40 | 38 / 21 |
| ToggleSwitch | 98 × 38 | 98 × 38 | 8 of 8 |
| GraphicView | 90 × 90 | 90 × 90 | 19 |
| Text | 129 × 30 | height **16** | 83 |

Most common item **heights** on full-size screens: **15** (×86), **50** (×71), **10** (×68),
49 (×40), 75 (×21).

So there is a working vertical rhythm — roughly **15 px for a text line, 50 px for a row, 10 px for a
rule** — rather than an arbitrary spread. That is the closest thing to a grid the corpus offers; the
horizontal edges do *not* show a comparable spike pattern, so there is **no evidence of a column
grid**, only of consistent row heights.

## 5. 🔴 Touch targets — where the corpus and the floor disagree, systematically

JOB9003, Classic Basic, converted at the KTP900's **4.30 px/mm vertical**:

| type | n | height range | in mm | vs the 9 mm floor |
|---|---|---|---|---|
| **Button** | 6 | 40–100 px | **9.3 – 23.3 mm** | ✅ 0 of 6 violate |
| IOField | 23 | 25–32 px | **5.8 – 7.4 mm** | 🔴 **23 of 23 violate** |
| Switch | 3 | 20–22 px | **4.7 – 5.1 mm** | 🔴 3 of 3 violate |
| SymbolicIOField | 1 | 25 px | 5.8 mm | 🔴 1 of 1 violates |

**27 of 33 interactive items are below the floor — and every single button clears it.**

That pattern is too clean to be carelessness. The house convention evidently distinguishes
**command controls**, which are sized generously for the finger, from **data-entry and setting
fields**, which are sized for reading and only occasionally touched.

Unified shows the same shape at its own scale: buttons median 60 px, IOFields 40 px, toggle
switches 38 px — entry fields consistently around two-thirds the height of a button.

**So the disagreement may be a gap in OUR rule rather than an error in theirs.** `H-401` states one
9 mm floor for everything interactive and does not distinguish a start button from a setpoint field.

🔴 **This is the owner's call and is NOT being resolved here.** Two options, and the difference
matters because it is safety-adjacent:

- **(a)** Split `H-401` — command controls ≥ 9 mm (corpus agrees, 6/6), entry fields get a lower
  documented floor. This ratifies observed practice.
- **(b)** Keep the single floor and record that both corpora violate it for entry fields. This
  treats the practice as a shortcut that our screens will not copy.

**Do not let this be settled by the corpus alone.** It is exactly the case the "directionally
correct, not authoritative" rule was stated for.

## 6. Zero scripts

JOB9002 has **0 script modules** across 48 screens, against 311 discrete alarms and 233 tags. The
behaviour lives in tag bindings and alarm configuration, not in code.

**Classification: DELIBERATE**, and reassuring — it means a screen generator that emits geometry and
bindings, and no scripting, is not missing a layer that real projects depend on.

---

## What this suggests changing in `docs/17`

Proposed, not adopted — each needs the owner's agreement:

1. ~~**A composition rule.**~~ **DEFERRED TO UNIFIED — confirmed unavailable on Classic Basic (§1).**
   What replaces it for the current target is the opposite instruction: every Basic screen is
   self-contained, so chrome is repeated and consistency must be enforced by the generator rather
   than by reuse. Investigate the template as the one available shared layer.
2. **A vertical rhythm.** ~15 px text line, ~50 px row, ~10 px rule, scaled per panel. There is
   evidence for row heights and **none for a column grid** — so do not invent one.
3. **Status indicators as a first-class primitive.** The second most common item in the Unified
   corpus, and absent from our vocabulary and our rules.
4. **A density sanity range**, so a generated screen can be flagged as implausibly sparse.
5. **Resolve the touch-target split** (§5) — the one item here that is safety-adjacent.

## What this evidence cannot tell us

Frequency cannot distinguish a convention from a habit copy-pasted 49 times. Where a pattern appears
consistently I have called it deliberate; where it merely appears, I have said so. **Neither corpus
was reviewed by anyone for quality** — they are working projects, and the touch-target finding is
direct evidence that being shipped is not the same as being right.
