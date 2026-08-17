# Lane B — Flatten & check toolchain

**Owns exclusively:** `src/hmi-cli/**` (new solution), its tests
**Portal:** never · **Wave:** 1 · **Size:** M · **Blocks:** Lane P's applier, Lane D
**Depends on:** Lane A for rule IDs only (T3 can be built against placeholder IDs and rewired)

## What is being built

Three tools, all of which already exist as **working prototypes in `hmi/prototypes/`** and were run
live in experiment 2. This lane promotes them; it does not invent them.

| tool | from | what it does |
|---|---|---|
| **T1 `hmi-flatten`** | `flatten-probe.html` (embedded script) | headless Chrome → `screen-ir.json`: absolute integer rects, element type, stacking tier, per-item properties, tag bindings, faceplate type+version |
| **T2 `hmi-lint`** | `scorecard.py` (geometry half) | sub-pixel positions, off-canvas, text-leaf overlap, alignment near-miss, largest dead band, touch targets under minimum |
| **T3 `hmi-style-check`** | `scorecard.py` (HSL half) | every computed background/text/border colour → HSL, then green-for-normal, accent hues, gradients, shadows, radius, animation, grey-field range |

T1 was proven twice: 55 items with 4 faceplate refs and 14 bindings at 1920×1080, and across four
independent screens at 1280×800. T3 scored all four arms of experiment 2 identically and **produced
the result that decided the recommendation.** The prototypes are harness-shaped, not
production-shaped — that is the work.

## Where it lives

**A new `src/hmi-cli/`, targeting net8.0.** Reasons, so this is not relitigated:

- It **cannot** live in `converter`. T1 shells out to headless Chrome, and the converter's standing
  invariant is that it is a pure in-process file transformer — env-touching belongs in
  `openness-cli` or in scripts. That invariant was deliberately preserved once already (FI-24).
- It **should not** live in `openness-cli`. That is net48 by Openness constraint, and every rebuild
  of it needs a TIA whitelist approval keyed on `(Path, FileHash)`. Do not drag a Chrome dependency
  into that binary.

## The regression baseline already exists

`research/experiment2/{direct,tooled,rules-only,hybrid}/` — **four complete screens with four known
scorecards**, authored independently, scored by the prototype. Wire them as golden tests. This is an
unusually good position to start from and it was not created for the purpose; use it.

Expected values are in `research/hmi-research.txt` §3.10. The hybrid arm should score 0 sub-pixel,
0 off-canvas, 0 overlap, grey field OK, 0 greens, 0 accents, 0 shadows.

## Non-negotiables

- **Fail closed.** These are on the path to delivery. A check that only warns gets skimmed.
- **Print the denominator on every run.** `COMPARED: n items`, `SCANNED: n elements`. An empty run
  must never exit 0. This project has been bitten by that five times on five different tools.
- **Emit rule IDs** from Lane A's `docs/17-hmi-conventions.md`. Placeholder IDs are fine in wave 1;
  rewire in wave 2.
- **🔴 Touch-target checking is in MILLIMETRES, with px/mm as config keyed on panel model.** See
  [`hmi/sizing-standard.md`](../sizing-standard.md). Report findings in mm with px in parentheses —
  `7.4 mm (39 px) — below the 9 mm floor` — because a bare pixel count is unactionable when the
  reader does not know the panel. **A screen IR that declares no panel is a REFUSAL, not a default:**
  the anchor and the target share a resolution but differ 29% in physical size, so silently assuming
  a panel is how a quarter-scale error passes every check.
- **The target family is DECLARED, never inferred.** `screen-ir.json` carries the family it was
  authored for (`Unified` today). A tool handed an artifact for another family **refuses**; it does
  not adapt. Classic and Unified are two disjoint APIs and T1 is the only piece common to both —
  everything downstream forks, so the fork must be visible in the data rather than assumed by the
  reader. See `hmi/PLAN.md` §0 and `hmi/target-differences.md`.
- **Unknown item type, attribute or enum value → hard error naming it literally.** Never a skip,
  never a default, never a nearest match. The enumerated family differences are a floor, not a
  census; the ones nobody has met yet must surface as named refusals rather than as silent
  degradation. Record each one in `hmi/target-differences.md` **before** working around it.
- **Pin the Chrome version** and record it. Version drift changes the flattened integers, which
  makes it build-breaking, which makes it a golden test — the shape `tests/golden/` already is.

## 🔴 The limitation that must survive into the docs

Measured directly in experiment 2, and it is the single most important fact about these tools:

> **T2 missed a visible text collision** (glyph overflow that does not intersect boxes) **and the
> render missed 2 off-canvas elements across 8 cycles. Neither subsumes the other.**

T2 also **false-positives on deliberate overlap** — it scored 7 on an arm where the overlap was
intentional. Do not tune that away; it is a true positive about an ambiguous situation.

Write both facts into the tool's own `--help` and README. The whole method depends on somebody
running the render *as well*, and a tool that reads as authoritative invites skipping it.

## First job after promotion

**Install Siemens Sans in the flattening Chrome and re-measure.** The probe used a fallback font. If
the metrics differ, every text rect produced so far is wrong and the golden baselines need
re-taking before anything is built on them. Untested; find out now rather than in wave 2.

## Deferred to wave 2, conditionally

**T4 `hmi-snap`** — quantise the IR to a grid. Needed *only* if the owner adopts a coarse grid
(`hmi/PLAN.md` §5 decision 3). At 1 px the hybrid arm scored 0 sub-pixel with no snapper at all, so
the recommendation is that T4 is never built. Do not build it speculatively.

## Done when

- T1/T2/T3 run as real tools with tests, from a documented entry point
- All four experiment-2 arms reproduce their known scorecards
- Every tool prints a denominator and fails closed
- The Siemens Sans question is answered either way
- The "neither subsumes the other" limitation is documented where a user will see it
