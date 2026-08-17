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

## The recommendation

1. **Probe the graphic-library import path first.** Can a picture be got into a Classic project's
   graphic library through Openness, and referenced by name from a `GraphicView`? Everything below
   depends on that one answer, and nothing else should be built until it is known.
2. **If yes — emit `GraphicView` + `Picture` link.** Small work: an AttributeList and a one-line
   LinkList. The AI places, sizes and binds; the artwork is drawn once or taken from the Siemens
   library. Same "hand-author once, reuse mechanically" pattern already accepted for the alarm
   view — but unlike that one, not blocked.
3. **Then pursue tag-driven pictures**, since the corpus proves the mechanism carries state.
4. **Polygon is worth adding, but separately.** 84 instances in Unified, and it is what makes a
   *filled* cone possible. It is a better primitive, not a different category — it would improve
   the silo without changing the verdict on composing artwork.

## What NOT to do

- **Do not iterate on primitive composition.** Two independent lines of evidence — what real
  projects do, and what the research says LLMs cannot yet do — point the same way.
- **Do not build a drawing system.** The expensive thing is teaching a model to draw good vector
  artwork; nobody in either corpus does that either.
