# `hmi/` — HMI generation from a spec

Generating an operator screen from a specification, for WinCC Unified on TIA Portal V20.

**This folder is planning and prototypes. It is not a scope grant.** HMI engineering remains a
non-goal under `docs/10-non-goals.md` pending **ADR-0007**, which is *Proposed* and undecided.

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

⚠️ **Two things are not yet true and are marked as such throughout:** whether a *Basic* panel exports
as SimaticML at all (nobody has tried — it is the keystone, and it is a read), and whether the
programme has a write target (the JOB9003 approval is read-only).

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

Nothing in this folder has been built yet. The prototypes work but are harness-shaped. Wave 1 of the
plan needs no ADR-0007 decision; everything from wave 2 does.
