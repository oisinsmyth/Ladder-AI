# Lane A — House conventions

**Owns exclusively:** `docs/17-hmi-conventions.md` (new file, nothing else)
**Portal:** never · **Wave:** 1 · **Size:** S · **Blocks:** Lane B's T3 rule IDs, Lane D
**Depends on:** nothing. Lane C supplies better numbers later; that is a patch, not a blocker.

## Why this lane is first

Experiment 2 measured it: **the rules layer is free and it is decisive.** The arm with rules and no
render loop cost the same as the arm with neither (77.0k vs 77.5k tokens, 2.7 min each) and fixed
appearance entirely — 0 greens, 0 accents, 0 shadows, grey field OK. Every arm that *lacked* it
produced green-for-running and accent chrome **unprompted, every time**. This document is the
highest-leverage artifact in the whole programme and it costs days, not weeks.

## 🔴 REDIRECTED 2026-08-17 — the canvas changed, and it changes the rules

Target is **Classic Basic**, first application a **Basic 7"**; anchor is a **KTP900 Basic**
(`hmi/PLAN.md` §0).

**Experiment 2 ran at 1280×800. A Basic 7"/9" is believed to be 800×480** — unconfirmed, see
`hmi/target-differences.md` row 11, confirm it off the anchor before building on it.

That is not a scale factor, it is a different design problem. A ≥48 px touch target is **6% of the
width and 10% of the height** at 800×480, against 4%/6% at 1280×800. Zone geometry, type scale and
information density from experiment 2 **do not transfer** — the *rules* survive, the *numbers* do
not. Assume roughly a third of the usable area and design the information hierarchy for it rather
than shrinking a bigger layout.

Two more consequences worth checking early rather than discovering in Lane B:

- **Classic Basic is the restricted tier.** Its supported object set, colour depth, font set and
  dynamization options are all narrower than Comfort's and are recorded nowhere in this repo. A rule
  that assumes a property exists may be unenforceable on the actual target. Lane P's capability walk
  answers this; mark anything dependent on it `[PENDING CAPABILITY WALK]`.
- **The Template Suite is a Unified product.** Its palette and grid are still a better starting point
  than invention, but they are not authority for this target and its zone geometry is sized for a
  much larger canvas. Harvest the *palette*; re-derive the *geometry*.

## Deliverable

`docs/17-hmi-conventions.md`, modelled on `docs/06-lad-conventions.md`: numbered rules with stable
IDs that a checker cites and a finding references.

- **Rule IDs `H-nnn`.** Group them the way the LAD conventions group C-IDs.
- **Every rule is marked `[MECHANIZED]` or `[ADVISORY]`.** This is not decoration. `converter review`
  has 18 mechanized C-IDs of which 3 are vacuous against current capability, and the authoritative
  count lives in `ReviewRunner.AllRuleIds` rather than in prose — because a grep for `C-nnn` counts
  mentions and yields 24. Do not create that ambiguity here. If T3 can score it, say so; if it needs
  a human, say that instead. **A rule that claims to be mechanized and is not is worse than an
  advisory one**, because it inflates the denominator.
- **State the coverage denominator in the document itself**: "n rules, of which m are mechanized."

## Seed content — the eight clauses experiment 2 used

These are already measured as sufficient to fix appearance. Start here, do not re-derive:

1. Low-chroma grey field. Never white, never dark-mode.
2. Colour is reserved for the abnormal. A normal running plant is grey.
3. Red and amber only as alarm colours.
4. No gradients, no shadows, no corner radius, no 3-D.
5. No animation except an unacknowledged alarm.
6. Dark text on the grey field; tabular numerals for process values.
7. ~~Touch targets ≥ 48 px on the minor axis.~~ 🔴 **SUPERSEDED — see
   [`hmi/sizing-standard.md`](../sizing-standard.md), and adopt its draft H-S1…H-S6 wholesale.**
   48 px is 9 mm on *one* panel and this project will touch several. Thresholds are declared in
   **millimetres** and converted with the target panel's px/mm; a hard-coded pixel threshold is a
   defect whatever its value. The same 48 px button is 9.1 mm on the 7" target and 11.7 mm on the 9"
   anchor — identical pixels, 29% different physical size, and no mechanical check sees it.
8. No accent colour as chrome.

## 🔴 The correction that is already owed

**H3 needs an explicit STOP carve-out.** Both rules arms of experiment 2 independently objected that
forbidding a red STOP is wrong: plant convention overwhelmingly expects red for a safety-critical
stop, and a grey STOP "will feel wrong to anyone trained on red-stop panels". They are right.

Write the carve-out **scoped to STOP / E-STOP controls only** — not "red is allowed for commands",
which would reopen the whole rule. Flag it in the hand-back as needing owner sign-off: it is
safety-adjacent convention, and per `hmi/PLAN.md` §5 decision 4 the owner takes that call, not you.

Record *why* the carve-out exists in the document. It is the argument for the guide being a
reviewable document engineers can correct rather than a prompt buried in an agent — first contact
with real work produced a correction, before a single screen shipped.

## Numbers

Palette hex values, grid pitch, zone geometry (title bar / process area / alarm banner heights) and
the type scale should come from the free **SIMATIC HMI Template Suite**, not from invention. Lane C
is harvesting them.

**Do not block on that.** Write the rules now using experiment 2's own values, marked
`[PROVISIONAL — pending Lane C]`. Swap them in wave 2. A rule with a provisional number is useful;
a missing rule is not.

## Done when

- Every rule has an ID and a `[MECHANIZED]`/`[ADVISORY]` marking
- The STOP carve-out is written, with its reasoning, and flagged for sign-off
- The document states its own mechanized/total denominator
- Provisional numbers are marked as such and listed in one place for wave-2 replacement

## Hand back

The document, the count of rules by category, the list of provisional numbers awaiting Lane C, and
an explicit statement of anything in the eight seed clauses you could **not** express as a checkable
rule — that last one is Lane B's problem to hear about early, not late.
