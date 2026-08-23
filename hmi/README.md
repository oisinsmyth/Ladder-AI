# `hmi/` — HMI generation from a spec

Generating an operator screen from a specification, for WinCC Unified on TIA Portal V20.

✅ **HMI ENGINEERING IS ACTIVE WORK. ADR-0007 WAS ACCEPTED 2026-08-17**
(`docs/adr/adr-0007-hmi-engineering-scope.md:3-5`) and HMI engineering **left**
`docs/10-non-goals.md`'s "Not now" list the same day (`docs/10-non-goals.md:21-23`), at full
screen-authoring size. ⚠️ **This line read *"remains a non-goal … pending ADR-0007, which is Proposed
and undecided"* until 2026-08-23 — six days after the decision, and three delivered waves later.**
Corrected rather than overwritten, because the first thing a reader of this folder met was a scope
statement that had been false for a week.

**What is still true, and is a different claim:** the folder's *contents* below are planning and
prototypes, and the section-by-section marks say which parts have shipped. A decided scope is not the
same as a finished capability — see **Status** at the foot of this page.

---

## Start here

| file | what |
|---|---|
| **[PLAN.md](PLAN.md)** | the development plan — target scope, lanes, waves, owner decisions, risks |
| [sizing-standard.md](sizing-standard.md) | physical sizing in **mm**, pixels derived per panel — buttons, touch targets, gaps |
| [target-differences.md](target-differences.md) | living ledger of Classic/Unified/panel-tier differences — **open, expected to grow** |
| [lanes/](lanes/) | one dispatchable brief per lane, written to hand to a sub-agent as-is |
| [prototypes/](prototypes/) | working prototype code for T1/T2/T3, proven in experiment 2 |

🔴 **Target: CLASSIC BASIC.** Settled by the owner 2026-08-17 — the first real application is a
**Classic Basic 7"**, anchored on `JOB9003 - K150 Demo - Scratch Copy` (a **KTP900 Basic**; Amber,
read-approved, gitignored). Expansion goes Classic Basic → Classic Comfort/Advanced/Professional
(same API, a capability question) → Unified (a second back-end, not a widening).

Classic and Unified are two disjoint Openness APIs sharing no types — **Classic is a file pipeline
with no screen object model, Unified an object model with no file pipeline.** Because Classic screens
move as SimaticML, the pipeline below is **the one this repo already built for ladder logic**, which
is the good news in the redirect. `PLAN.md` §0 has the full consequences.

✅ **BOTH OF THE "NOT YET TRUE" ITEMS THIS PARAGRAPH USED TO CARRY WERE SETTLED ON 2026-08-17 AND THE
PARAGRAPH DID NOT NOTICE UNTIL 2026-08-23.** It read: *"whether a Basic panel exports as SimaticML at
all (nobody has tried — it is the keystone, and it is a read), and whether the programme has a write
target (the JOB9003 approval is read-only)."* Both are now answered, and this line is the reason a
current reader could still be told the keystone was untried a week after it was tried:

- **K1 — does a Classic Basic screen export as SimaticML? ✅ YES**, and bigger than the question asked.
  One screen → **224 KB**, root `<Hmi.Screen.Screen>`, 5 screens walked on the anchor, two read-only
  runs both exit 0. **`<Hmi.Screen.ScreenLayer>` and `<Hmi.Screen.Group>` exist in the file**, and
  every item carries absolute `Left`/`Top`/`Width`/`Height` — exactly T1's output shape.
  `wave-1-results.md:21-52`. 🔴 **The finding that came with it:** the survey's *"Classic models
  nothing inside a screen"* is true of the **object model** and false of the **file**.
- **A write target exists.** Approved the same day and recorded in `docs/13-data-boundary.md:375` and
  `:399`; three waves have since imported, compiled and exported against the scratch copy.

⚠️ **What is genuinely still open is narrower, and it is not the keystone:** Basic panels do not have
square pixels (wave 1's one broken assumption, and it was the author's rather than the plan's), and
the residuals listed in each wave's own "not built" section — see **Status** below.

Background, and the evidence for every claim in the plan: **`research/hmi-research.txt`** (rev 7
FINAL). Section R is the recommendation; everything before it is why.

---

## The approach, in short

Author the screen as **HTML + a house stylesheet**, render it in headless Chrome, and read the
resolved geometry back as absolute integer rects. Chrome does the layout; the converter just reads
it off. That flattened form is the compiled artifact; a read-back from TIA is the proof.

```
  screen.html + house.css
      │  T1 FLATTEN    headless Chrome resolves all layout        [PROVEN]
      ▼
  screen-ir.json        absolute integer rects, type, layer/order, props, bindings
      │  T2 LINT        geometry: overlap, off-canvas, alignment  [PROVEN]
      │  T3 STYLE-CHECK house rules, machine-scored via HSL       [PROVEN]
      │  T8 PREFLIGHT   tags exist, faceplates exist, all mapped
      ▼
      │  T5 APPLY       existing openness-cli hmi write commands
      ▼
  screens in TIA  + hmi-compile                                    ← the existing gate
      │  T6 SERIALISE   the property walk, canonicalised
      ▼
  readback.json
      │  T7 COMPARE     against screen-ir.json
      ▼
  VERDICT
```

That is the `.ir` / SimaticML / re-export separation this project already runs on, transplanted.
HTML is the artifact a human reviews; `screen-ir.json` is the compiled form; `readback.json` is the
proof.

## Why this and not a generation tool

Measured, not argued. A credible 1920×1080 operator screen was produced directly, first attempt, no
generator tool and no dependency beyond the Chrome already on the machine. A controlled 2×2
experiment then compared four methods on one spec at 1280×800:

- **the rules layer is free and decisive** — it costs nothing and fixes appearance entirely;
- **the render loop costs ~5.6× wall-clock and fixes geometry only** — but geometry is exactly what
  reasoning about coordinates gets wrong;
- **the general-purpose design skill does not pay** — more tokens than the winning method, and the
  worst scores on overlap and off-canvas.

The leverage is not in a generation tool. It is in the render-and-flatten loop, which costs almost
nothing. Full numbers: `research/hmi-research.txt` §3.10.

## Rejected, with reasons, so they are not re-proposed

- **Prune-and-bind** (instantiate a pre-designed panel, delete what is unwanted) — rejected by the
  owner: produces clunky results. A subtractive generator can only produce arrangements the template
  anticipated, and inherits the template's grouping permanently.
- **Screenshot diffing as the primary gate** — a correlated check. With no independent ground truth,
  the approved baseline is generated from the same source as the thing it checks. Pixels are a
  tripwire; geometry is the gate.
- **The AI-UI-generation tool category** — wrong direction, wrong aesthetics, and cloud-hosted
  against a data boundary.

## Status

⚠️ **This section read *"Nothing in this folder has been built yet … wave 1 needs no ADR-0007
decision; everything from wave 2 does"* until 2026-08-23** — after ADR-0007 was accepted and after
all three waves ran. The gating sentence describes a decision that has been taken.

**Three waves executed 2026-08-17**, each recorded in its own results file, everything run rather
than designed:

| wave | what landed | record |
|---|---|---|
| **1** | K1 answered ✅; the Classic file is richer than the plan assumed | `wave-1-results.md` |
| **2** | a screen authored as HTML is in TIA and provably what was written — `home.html` → flatten → emit → import → export → **compare: 13 objects, 0 differences** | `wave-2-results.md` |
| **3** | `hmi-cli compare` as a tool (13 objects, 0 differences; same-file-twice refused, exit 2) · a **coherence gate** built around reproducing the exact document that crashed Portal · `HmiCli.Tests` **23 passing** · the `hmi-designer` agent · doc 17's rule count reconciled **26 claimed → 14 checked** | `wave-3-results.md` |

**Not built, and each wave says so itself** — T8 preflight, T9 unwired-check, 9 specified-but-unchecked
rules (including **H-107**, whose colour half is the other half of a physical-safety rule), the
regeneration guard the placeholder workflow depends on, and the `hmi-designer` agent has **never been
dispatched**. Read the residual list at the foot of `wave-3-results.md` before assuming a capability.

**Relationship to `docs/18-project-workbench.md`:** that document's §5 **Phase 7 — "The HMI oracle"**
is **closed** as of 2026-08-23, because its open question (§7 Q1) is answered by wave 3's
`hmi-cli compare`. **Closing that phase closes a hole in a document that does not own this programme.
It closes nothing here.** This folder's plan, waves and residuals are the record.
