# Lane P — The Portal queue

**Owns exclusively:** `docs/notes/hmi-probe-*.md`, `docs/notes/hmi-attribute-map.md`,
`src/hmi-cli/Apply/**` and `src/hmi-cli/Readback/**` (from wave 2, after Lane B lands)
**Portal:** **yes — this lane is the token holder** · **Waves:** 1–3 · **Size:** L
**Coordinator takes this lane.**

## The rule that defines the lane

> 🔴 **PORTAL IS A TOKEN, NOT A COMPONENT.** One lane holds it at a time. Two Openness sessions on
> one project is unsupported and has already produced `Collection was modified` with every block
> reporting inconsistent.

Everything requiring Portal is in this one lane, worked as a **strict queue**, precisely so that no
second lane ever wants the token. If a job here blocks, the queue stalls — it does not fan out.

Concurrent Portal sessions on *different* projects are safe and that is a supported design, but do
not use it to parallelise this lane. The scratch project is one project.

## Queue order, and why it is this order

```
  wave 1   Stage 0 → P1 → P2 → P5
  wave 2   T6 → T8 → T5 → hmi-compile gate
  wave 3   T7 → T9
```

### Stage 0 — the project that makes the probes answerable [XS]

Install the **HMI Template Suite**, run its wizard once, walk the result with the existing
`openness-cli hmi`. This is not optional throat-clearing: **`--schema` only reports attributes for
types already used in the project**, so P2 is unanswerable without a project that contains the item
types you care about. Stage 0 produces one, and it settles P1 and P2 for free.

### P1 — the keystone [XS]

**Can Openness *set* an item's layer, and does creation order determine within-layer stacking?**

Method: create two overlapping items, vary creation order, read back. Look for a layer attribute via
`hmi --schema` on a project that *has* layered content.

This decides whether the converter's stacking stage (research §5.3) is a **mapping** or a **hard
error**. Not publicly documented; it has to be measured.

**If P1 is negative** the fallback is single-layer output plus a hard error on any source needing
more than one tier. **Never a silent flattening.** That is the `--allow-blind-types` shape this repo
already uses: refuse, name the reason, offer an explicit escape, never guess. Survivable — most
screens are single-layer.

### P2 — the attribute map [XS]

Dump `hmi --schema` for the 12 item types a real 49-screen project actually uses — Text, Circle,
Rectangle, Button, Line, Polygon, GraphicView, IOField, ScreenWindow, ToggleSwitch, SymbolicIOField,
AlarmControl — and build the **CSS-property → HMI-attribute mapping table** into
`docs/notes/hmi-attribute-map.md`.

All 12 are creatable `[REPO-MEASURED]`. Four covered the entire probe screen.

⚠️ **Do this before the house stylesheet is written against properties that may not exist.**
`box-shadow`, gradients, `border-radius` and transforms may have no target equivalent at all. The
probe stylesheet happened to be flat and radius-free, which was luck as much as judgement.

The rule the table encodes: **a visual property with no target equivalent is a hard error or an
explicit opt-out.** Do not let the source express what the target cannot render.

### P5 — CWC placement [XS]

Run the already-written, **never-run** `HmiCustomWebControlContainer` placement code. A throw is a
result. Low priority; it is the escape hatch for a rectangle that genuinely needs internal layout,
not part of the main path.

---

## Wave 2 — the write path

### T6 `hmi-serialise` [M] — and note it comes *first*

Read-back of a screen via the property walk, canonicalised and stably ordered.

**It is first in wave 2 because it does not need anything to have been written yet.** The reference
project has 49 screens; build the serialiser against those. Doing it before T5 means the applier is
verifiable from its first run instead of retrofitted.

There is **no screen export function** `[REPO-MEASURED, five ways]` — but the consequence once drawn
from that, *no diff, no hash, no round-trip*, was already retracted in the repo's own notes and was
wrong. A serialiser over the property walk recovers all three, and two shipped products prove it is
feasible (Siemens' own Openness-based Excel exporter, SIOS 109792619; and a commercial JSON one).

Two rules inherited from `Normalizer` / `converter compare`, both hard-won:

- **Ignore the volatile fields and say which.** TIA reassigns UIds unprompted. Assume an HMI
  equivalent exists and go find it *before* trusting a diff.
- **Empty is not clean.** Print `COMPARED: n objects` on every run.

### T8 `hmi-preflight` [S]

Before any write: every bound tag exists in `ir/<project>/`, every faceplate type exists, every
element mapped, no duplicate names. `converter tagstatus` already does the tag half and already
exits non-zero on invented names — wrap it, do not reimplement it.

### T5 `hmi-apply` [M] — the hardest job in the programme

IR → Openness write calls, using the existing measured capability: create screen and items, set
attributes with coercion, bind dynamizations, place faceplate containers, set Interface values.

🔴 **There is no transaction and a failed command keeps what it already did.** Therefore:

- **Every command re-runnable and idempotent.** Not aspirationally — a converter that replaces a
  screen must compile in the same breath and survive being run twice.
- **Deletion orphans silently** `[REPO-MEASURED]`. Design for it.
- **Do not iterate against TIA.** Openness writes are multi-minute; a browser render costs about a
  second. Converge in the browser and pay the Openness cost once.

### The gate

`openness-cli hmi-compile`. It checks the faceplate type reference **and** the parameter binding
structurally, by type and version, located screen → item → property — a genuine hard-rule-4
analogue.

**It is blind to geometry and aesthetics.** A green compile is not a pass on its own. T2 and T3 fill
that hole and run alongside, never instead.

⚠️ **No FI-52 backstop exists here.** That mechanism is built on `PlcBlock.IsConsistent` and there is
no HMI equivalent, so `Success` from `hmi-compile` means only that the compiler said so. Treat it
accordingly.

---

## Wave 3

### T7 `hmi-compare` [S]

T6's output vs the intended IR — the `converter compare` analogue. Must ignore volatile fields and
**say which**, and must never exit 0 over zero comparisons.

### T9 `unwired-check` [S]

Every faceplate parameter is actually bound. **The compile catches WRONG, never MISSING — an
entirely unwired parameter compiles clean** `[REPO-MEASURED]`. Buildable now: the read-back reports
`(unset)`.

---

## 🔴 REDIRECTED 2026-08-17 — read this before the queue below

The target is **Classic Basic**, not Unified (`hmi/PLAN.md` §0). The queue in this brief was written
for Unified and **P1, P2 and T6 are now superseded** — they are questions about an object model that
Classic does not have.

**The new first job is K1, and it is a read:** export an existing screen from the anchor project
(`JOB9003 - K150 Demo - Scratch Copy`, KTP900 Basic) as SimaticML and read the file. Does a *Basic*
panel export at all, and what is in it? Nobody has tried. A negative answer invalidates the emitter,
the comparator and the gate together, so nothing downstream starts until it is answered.

Then: a capability walk of what the Basic tier actually supports (item types, properties,
dynamization) — the Classic analogue of P2 — and only then the thin spike.

**The spike needs a WRITE target and does not have one.** The JOB9003 approval is read-only; see
§5 decision 7. Do not write to the anchor on the assumption that a scratch copy is fair game.

Read the sections below for the *shape* of the work — idempotency, denominators, the compile gate's
limits — which all survive the redirect. Read their Unified specifics as superseded.

## Target family — declare it, assert it, and expect surprises

- **`--expect <family>` on every Portal-touching command.** Read the device family back and exit
  non-zero on mismatch, exactly as `block-layout --expect` does. The failure being prevented is
  authoring a good Unified screen against a Classic target and getting a downstream error that names
  something else entirely.
- **The enumerated differences are a floor, not a census.** Basic vs Comfort vs Advanced within
  Classic, Unified Basic vs Comfort vs PC RT, panel-size variants, firmware and version interactions
  — all unrecorded. Expect to meet differences during development and in use.
- **Every one gets written into `hmi/target-differences.md` before it is worked around**, with how it
  was found and at what evidence tier. A workaround applied and not recorded will be rediscovered at
  full cost.

## Standing constraints for this lane

- **ADR-0007.** Wave 1 here is probe work, the same class as `openness-cli hmi-create-screen` — built
  and run as a capability probe with the non-goal explicitly left standing. **Wave 2 is not.** The
  first deliverable screen is the ADR line; do not cross it without the decision.
- **Never rebuild `openness-cli` while Portal work is in flight.** The TIA whitelist is keyed on
  `(Path, FileHash)`; a rebuilt binary needs a fresh approval and, unattended, the attach just hangs
  until the timeout, looking exactly like a wedged Portal. `dotnet test` on `openness-cli.sln` **is**
  a rebuild. `converter.sln` and the new `hmi-cli` are safe.
- **`openness-cli` launches Portal as a child inheriting stdout, so piping its output hangs forever.**
  Redirect with `>` on the outer invocation.
- **Watch for stale Portal processes.** `openness-cli portal-status` is the read-only diagnostic; it
  classifies in-use / self-launched-orphan / stray-empty and never closes anything itself.
- **A memory-layout-class trap should be assumed, not hoped against.** Design the read-back
  comparison to *notice* an attribute silently reset on re-import. That exact failure has happened,
  on `MemoryLayout`, and every check stayed green while it did.

## Done when

- **Wave 1:** P1 has a yes/no; `docs/notes/hmi-attribute-map.md` exists and covers the 12 types
- **Wave 2:** one screen, authored as HTML, exists in a scratch project and compiles clean
- **Wave 3:** a re-run is *provable* — read-back diffs clean against the intended IR, with a stated
  denominator and a stated list of ignored volatile fields
