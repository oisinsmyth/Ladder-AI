# HMI generation — development plan

**Date:** 2026-08-17 · **Status:** plan, not a scope grant (see [Scope gate](#scope-gate))
**Source of every claim here:** `research/hmi-research.txt` (rev 7 FINAL), section R — **except the
target**, which the owner set on 2026-08-17 and which post-dates the research. Where this plan and
that document disagree on *method*, the research document is right and this one has a bug; where
they disagree on *target*, §0 is right and the research predates it.

**Wave 1 is COMPLETE — see [`wave-1-results.md`](wave-1-results.md).**

**Companion documents in this folder:** [`sizing-standard.md`](sizing-standard.md) (physical sizing
in mm, pixels derived per panel) · [`target-differences.md`](target-differences.md) (the living
ledger of family/tier differences).

---

## 0. Target scope — 🔴 **CLASSIC BASIC FIRST.** Decision 5 is answered, and answered against the
## direction this plan was first written in.

> **Settled by the owner, 2026-08-17.** The anchor project is **`JOB9003 - K150 Demo - Scratch Copy`**
> (Amber, read approved, `docs/13-data-boundary.md` 2026-08-17, gitignored). It carries a
> **KTP900 Basic** panel. **The first real application is a Classic Basic 7".** Other HMI families
> come later, expanding out from this anchor.

**This inverts §0 as originally written.** The plan was built for Unified because that is what the
reference project uses; it is now built for **Classic Basic**, which is the *other* fork and, in one
important respect, the harder one. What follows is the corrected scope; the Unified material is
retained below because it is where the programme expands to, not because it is the current target.

### What survives the inversion, and what does not

| | status under Classic Basic |
|---|---|
| **T1 `hmi-flatten`** | ✅ **survives verbatim.** HTML → absolute geometry is back-end-agnostic — it is the one piece common to both forks, and it is the proven one |
| **T2 `hmi-lint`** | ✅ survives; canvas constants change (see below) |
| **T3 `hmi-style-check`** | ✅ survives; palette/type scale must be re-derived for the smaller canvas |
| **T5 `hmi-apply`** (Openness item construction) | ❌ **does not exist on Classic.** There is no `ScreenItems`, no button type, no IO-field type. Replaced by a SimaticML emitter |
| **T6 `hmi-serialise`** (property walk) | ❌ **not needed.** Classic screens *export as files*; the read-back is an export, not a walk |
| **T7 `hmi-compare`** | ✅ survives, and gets **easier** — it becomes a file-vs-file Normalizer comparison, exactly `converter compare`'s shape |
| **P1 (layers), P2 (attribute schema)** | ❌ Unified questions. Superseded by the new keystone below |

### The architecture, restated for Classic

Classic screens move as **SimaticML** — so the pipeline is **the one this repo already built**, and
the LAD analogy stops being an analogy:

```
  screen.html + house.css
      │  T1 FLATTEN      (unchanged, proven)
      ▼
  screen-ir.json
      │  EMIT            screen IR → SimaticML          ← replaces T5
      ▼
  openness-cli import → compile
      │  EXPORT          SimaticML back out             ← replaces T6, and it is a supported operation
      ▼
  converter compare (Normalizer)                        ← T7, near-free
```

The round-trip harness, the Normalizer, `drift-check`'s invariance idea, `diff`'s untouched-network
proof and hard rule 7 all generalise. **A screen IR is a 2-D layout-and-binding language, so
`ir/SPEC.md` needs a second *dialect*, not an extension** — that is the main new build cost, and it
replaces the applier rather than adding to it.

Two things go the other way, and neither is small: Classic has **no per-object validation**, so the
gate is a device compile — and FI-52 records precisely how little that is worth on its own — and
**no alarm API at all**.

### The working assumption — 🔴 **Basic IS Classic until something proves otherwise**

**Owner's ruling, 2026-08-17: treat it as a Classic panel until proven otherwise.** So the default is
that everything the survey establishes for Classic — SimaticML screen export/import, no screen
object model, no alarm API, no per-object validation — **holds for Basic**, and the Basic tier is a
capability subset rather than a different animal.

This is the right default: it lets the emitter, the comparator and the gate all be designed now
instead of waiting on a probe, and the alternative (assume Basic is special, build nothing) buys
nothing.

**What it does not license** is quietly forgetting that it is an assumption. So:

- **K1 still runs first**, and still runs early — it *tests* the assumption rather than gating on it.
- **Anything that turns out not to hold is a `target-differences.md` row**, recorded before it is
  worked around.
- **A Basic-specific failure must fail loudly**, not degrade. The predicted failure mode is an item
  type or property that Comfort has and Basic does not, arriving as a confusing import or compile
  error naming something else — which is why the emitter hard-errors on the unknown rather than
  guessing (§0, floor-not-census).

### 🔴 The first probe, and it is a READ

**Does a Basic panel's screen export as SimaticML, and what is in the file?**

`docs/notes/openness-hmi-api-survey.md` establishes SimaticML export for Classic, but its target line
reads *"Comfort / Advanced / Professional"* — **Basic is absent from it.** Basic panels are the
restricted tier of the Classic family and their Openness support level is recorded nowhere in this
repo. Everything above assumes an export that has never been performed.

Answering it costs one read against the anchor project. **Run it first — but do not block on it.**
Under the working assumption above, design proceeds on the Classic answer; K1 exists to catch the
case where that assumption is wrong, as early and as cheaply as possible. If it comes back negative
the emitter, comparator and gate all need rethinking, and the whole point is to learn that in wave 1
rather than in wave 2.

### Canvas — the number changes, and the stylesheet does not survive the change

Experiment 2 was run at **1280×800**. **KTP700 Basic and KTP900 Basic are 800×480** — ✅ confirmed
in wave 1 from the datasheets *and* from the anchor's own export (`Width 800`, `Height 480`). That is not
a scaling factor, it is a different design problem: a ≥48 px touch target is 6% of a 800 px width and
10% of a 480 px height, so the zone geometry, type scale and information density from experiment 2
**do not transfer** and must be re-derived. Lane A needs to know this on day one.

The anchor and the first application share a resolution, so the anchor is representative rather than
merely adjacent. ✅ **And "do not transfer" is now measured, not argued:** checked against a KTP700
canvas, the best experiment-2 arm puts **60 of its 84 items off-screen** (`wave-1-results.md`).

### ✅ The write question — resolved

Write access on the anchor was granted by the owner on 2026-08-17, with an independent master copy
held for resets (`docs/13-data-boundary.md`). **The retention rule is unchanged:** write access
widens what may be done to the project, never what may be retained about it.

---

### The two-API split, retained — it is what the expansion runs into

`docs/notes/openness-hmi-api-survey.md` §1 is decisive and predates this plan:

> **There is no single "HMI Openness API". There are two, they share nothing, and each one has
> exactly what the other lacks.**

| | **Classic** `Siemens.Engineering.Hmi.*` | **Unified** `Siemens.Engineering.HmiUnified.*` |
|---|---|---|
| Targets | Comfort / Advanced / Professional panels | Unified Comfort Panels, Unified PC RT |
| Public types (V20) | 64 | 487 |
| Create a screen from scratch | **No** | `Screens.Create(name)` |
| Objects *inside* a screen | **Not modelled at all** | full typed tree |
| File export/import of screens | **Yes — SimaticML** | **No** |
| Alarms in the object model | none whatsoever | yes, creatable |

**Classic is a file pipeline with no object model. Unified is an object model with no file
pipeline.** They are not two configurations of one target; they are two programmes.

### Where the expansion goes, and the trap in the word "converter"

The owner's stated direction is Classic Basic first, then out to the other families. The order that
implies, cheapest expansion first:

1. **Classic Basic** (KTP400/700/900/1200 Basic) — the anchor and the first application.
2. **Classic Comfort / Advanced / Professional** — *same API, same SimaticML pipeline.* The
   expansion is a capability question (which item types and properties the richer tier adds), not an
   architecture change. This should be nearly free.
3. **Unified** (Comfort Panels, PC RT) — **a second back-end, not a widening.** Everything from
   `screen-ir.json` downstream is rewritten: object construction instead of file emission, a
   property walk instead of an export, `Validate()` instead of a compile.

> ⚠️ **The word "converter" is exact on Classic and misleading on Unified.** On Classic there is a
> real file to emit and compare against, which is why the LAD pipeline transplants. On Unified there
> is no file at all — T1 would produce a *build plan* and the applier would be a *builder*. Do not
> let the vocabulary from this plan's Classic phase quietly imply a Unified design later.

### 🔴 And the tables above are a floor, not a census

The differences enumerated here came from a type-count survey and one reference project. They are
the ones **someone has already met.** They say nothing about Basic vs Comfort vs Advanced within
Classic, nothing about Unified Basic vs Comfort vs PC RT, nothing about panel-size variants,
firmware or version interactions, and nothing about differences that only appear at runtime.

**Assume every one of those hides something nobody has written down**, and build so that the unknown
ones *surface* rather than silently degrade. That is not optimism about discovery; it is the
standing shape of this repo's gates:

- **The target family is DECLARED, never inferred**, and travels with the artifact.
- **`--expect <family>`** on every Portal-touching command — assert what you believe, exit non-zero
  on mismatch, exactly as `block-layout --expect` does.
- **Unknown item type, attribute or enum value → hard error naming it literally.** Never a skip,
  never a default, never a nearest match (ADR-0010, FI-71).
- **Assume a `MemoryLayout`-class trap exists** — one attribute silently reset on re-import,
  invisible to every check — and design T7's comparison to *notice* rather than hoping.

**Every difference discovered in development or in use gets recorded in
[`target-differences.md`](target-differences.md) before it is worked around.** A workaround applied
and not written down is a difference that will be rediscovered at full cost.

## 1. The shape of the work, and why it is not N lanes wide

The naive read of section R's build order is a chain: conventions → tools → agent → apply →
compare. It is not a chain, but it is also not freely parallel, because of one constraint that
dominates everything else:

> 🔴 **PORTAL IS A TOKEN, NOT A COMPONENT.** One lane holds it at a time. Two Openness sessions on
> one project is unsupported and has already produced `Collection was modified` with every block
> reporting inconsistent.

So the real shape is **three genuinely concurrent lanes that never touch Portal, plus exactly one
Portal lane working a strict queue.** That is the whole plan in one sentence, and it is worth
stating plainly because the tempting mistake — spinning up two agents that both want a project
open — is the one that costs a day and looks like a tooling bug while it does so.

```
  ┌─ LANE A  Conventions ─────────────┐  docs/17-hmi-conventions.md
  │                                   │  no Portal · no code · highest leverage
  ├─ LANE B  Toolchain ───────────────┤  src/hmi-cli/  (T1 flatten, T2 lint, T3 style-check)
  │                                   │  no Portal · promotes proven prototypes
  ├─ LANE C  Sources ─────────────────┤  docs/notes/  (P3, P4, Q9, Template Suite numbers)
  │                                   │  no Portal · web retrieval only
  └─ LANE P  Portal queue ────────────┘  THE SINGLE TOKEN HOLDER, strictly serial:
                                          P1 → P2 → T6 → T8 → T5 → gate → T7 → T9

  ┄┄ LANE D  Agent & integration ┄┄┄┄┄   .claude/agents/hmi-designer.md
                                          needs A + B · GATED ON ADR-0007
```

Four things run at once at the start. That narrows to two by wave 2 and one by wave 3, which is
honest rather than disappointing: by then the remaining work is mostly Portal-bound and Portal is
serial by nature.

**File disjointness** is the other rule the lanes are drawn around, so they can be dispatched as
concurrent worktree tracks without merge conflicts. Ownership is exclusive and listed per lane in
`hmi/lanes/`. The one place it nearly breaks — Lane B and Lane P both wanting `src/hmi-cli/` — is
resolved by sequence rather than by argument: Lane P spends its first wave on probes, which are
documents, and does not touch code until Lane B has landed.

---

## 2. Lanes

Full briefs, each written to be handed to a sub-agent as-is, are in `hmi/lanes/`.

| lane | what | Portal | depends on | blocks | size |
|---|---|---|---|---|---|
| **A** | [House conventions](lanes/lane-a-conventions.md) — `docs/17-hmi-conventions.md`, rules `H-nnn` | no | — | B(T3 IDs), D | S |
| **B** | [Toolchain](lanes/lane-b-toolchain.md) — T1 flatten, T2 lint, T3 style-check, tests | no | A (rule IDs only) | P(T5), D | M |
| **C** | [Sources](lanes/lane-c-sources.md) — Basic panel datasheets, then P3/P4/Q9 | no | — | A + sizing (numbers) | S |
| **P** | [Portal queue](lanes/lane-p-portal.md) — K1, capability walk, then T8/T5/gate/T7/T9 | **yes** | B for the code half | everything downstream | L |
| **D** | [Agent & integration](lanes/lane-d-agent.md) — `hmi-designer`, skills | no | A, B | — | S |

**The coordinator takes Lane P.** It holds the token, it carries the open Basic-tier unknowns, and it
is the lane where a wrong call is expensive — Openness writes are multi-minute and
non-transactional, so a failed command keeps what it already did. The other three are clean
hand-offs.

### The Classic tool set, restated — names carried over from the Unified draft

| | Classic meaning | status |
|---|---|---|
| **T1 flatten** | HTML → `screen-ir.json` | ✅ **proven**, unchanged by the redirect |
| **T2 lint** | geometry + **mm-based** touch targets | ✅ prototype; needs px/mm config |
| **T3 style-check** | house-rule conformance, HSL-scored | ✅ prototype |
| **T5 EMIT** | `screen-ir.json` → **SimaticML** *(was: Openness item construction — that API does not exist on Classic)* | **the main new build** |
| **T6** | **already exists** — `openness-cli export`. Classic screens export as files, so there is no serialiser to write | ✅ free |
| **T7 compare** | Normalizer over two SimaticML files — `converter compare`'s exact shape | S, and easier than the Unified version |
| **T8 preflight** | tags exist, item types supported, all elements mapped | S |
| **T9 unwired-check** | every binding actually bound | S |

---

## 3. Waves

### Wave 1 — four lanes, no dependencies between them

| lane | deliverable | done when |
|---|---|---|
| A | `docs/17-hmi-conventions.md` v1 | every rule has an ID, and is marked **mechanized** or **advisory** |
| B | `src/hmi-cli` T1/T2/T3 + golden tests | reproduces experiment 2's four scorecards from the committed arms |
| C | `docs/notes/hmi-svg-contract.md`, Template Suite numbers | P3 and P4 answered at primary source, or recorded as unretrievable with what was tried |
| P | **K1 keystone (a READ)** → capability walk → **the thin spike** | K1 answered, the Basic item-type/property surface is written down, and **one screen exists in a scratch project** |

**K1 — first, and read-only:** *export an existing screen from the anchor project as SimaticML and
read it.* Does a **Basic** panel export at all, and what does the file contain? Run it first, but
**design does not wait on it** — the working assumption (§0) is that Basic behaves as Classic, and K1
is the cheap early test of that assumption rather than a gate on it. It also hands Lane B a **real
SimaticML screen document** to write the emitter against, which is worth more than the yes/no.

> **The thin spike is a deliberate amendment to R.6's order, added 2026-08-17.** Four item types
> (Rectangle, Text, IOField, Button), one screen, single layer, tag binding, then `hmi-compile`. No
> idempotency polish, no widening, throwaway code. It rides the same Portal session as P1/P2 and
> costs little on top of them.
>
> **Why it moves up:** T5 is the hardest and least-known piece in the programme, and the plan as
> first written did not touch a screen until wave 2. Finding out the applier is harder than expected
> belongs in wave 1, while three other lanes are still running and the schedule can absorb it. The
> research's own §7 staging put the minimum converter at Stage 1 for exactly this reason; this plan
> had inverted it on a scheduling argument, and a scheduling argument should not beat a risk one.

Wave 1 needs **no ADR-0007 decision**. A, B and C touch no project at all; P is a read plus a
scratch-project capability probe, which is exactly the class `openness-cli hmi-create-screen` was
already built and run as, with the non-goal explicitly left standing.

### Wave 2 — the toolchain meets Portal

| lane | deliverable | done when |
|---|---|---|
| A | swap in C's real datasheet numbers; finalise the mechanized set against B's T3 | T3 cites real rule IDs, px/mm figures are `[VERIFIED]` not `[INFERENCE]` |
| B | harden: denominators, fail-closed, mm-based thresholds keyed on panel | every check prints `COMPARED: n`; no hard-coded pixel threshold survives |
| P | **T5 EMIT hardened** → T8 preflight → import → compile gate | the spike's screen is reproducible: re-runnable, idempotent, verified by re-export |

Wave 2 is where this stops being research. It is also the first point at which anything is written
to a project as *deliverable content* rather than as a probe — the ADR-0007 line.

### Wave 3 — provable rather than plausible

| lane | deliverable | done when |
|---|---|---|
| P | T7 compare (Normalizer, file-vs-file), T9 unwired-check | a re-run is provable: the re-export diffs clean against the emitted SimaticML |
| D | `.claude/agents/hmi-designer.md` + skills | `/doctor` clean, and a dispatched screen comes back with evidence |

---

## 4. What each lane must not do

Lifted from R.5 and from the rules this project already holds, because the failure modes are known:

- **No check that only warns.** Anything on the path to delivery fails closed. A warning gets
  skimmed; this repo has the scar tissue to prove it.
- **Every check prints its denominator.** `COMPARED: n items`. Empty is not clean — this project
  has been bitten by that on `drift-check`, `compare`, `reuse-scan`, `undriven-scan` and
  `compile-all`, five times, and there is no reason to think the sixth will be different.
- **No self-review.** A lane does not accept its own output. Reviewer → fixer is fine; the reverse
  is the correlated check `docs/evidence/PlantAutoControl-bench-autopsy.md` exists to prevent.
- **No invented tags.** `converter tagstatus` is the existing anti-laundering gate; T8 makes it
  mandatory before any write.
- **No safety content**, under any framing (hard rule 2).
- **No screenshot diffing as a gate.** With no independent ground truth, an approved baseline is
  generated from the same source as the thing it checks. Pixels are a tripwire; geometry is the gate.
- **Do not iterate against TIA.** Converge in the browser where a render costs about a second, and
  pay the Openness cost once. Every applier command re-runnable and idempotent.

---

## 5. Owner decisions this plan is waiting on

Four, and only the first blocks anything in wave 1.

1. **ADR-0007 — HMI engineering scope.** Still *Proposed*, undecided. Blocks Lane D entirely and
   blocks wave 2's first deliverable screen. Does **not** block waves 1's A/B/C, and does not block
   Lane P's probes. *Recommendation: decide it before wave 2 starts, not during.* Building the
   capability and then discovering the scope answer is how a non-goal gets crossed by drift, which
   is the exact failure `docs/10-non-goals.md` was written to prevent.

2. **Where the toolchain lives.** *Recommendation: a new `src/hmi-cli/` (net8.0).* It cannot live in
   `converter` — T1 shells out to headless Chrome, and the converter's standing invariant is that it
   is a pure in-process file transformer with no external-process access. It could live in
   `openness-cli`, but that is `net48` (Openness constraint) and would drag the Chrome dependency
   into the binary whose every rebuild needs a TIA whitelist approval. Keep them apart.

3. **Grid pitch: 1 px or 8 px.** Experiment 1 found 93% of an 8-px-authored layout lands off the grid
   anyway; experiment 2 at 1 px scored **0 sub-pixel** with no snapper at all. *Recommendation: 1 px,
   plus T2's alignment-cluster lint,* which catches the thing an 8-px grid was really for (edges that
   nearly line up) without the snapper. This decision is what makes T4 either unnecessary or wave-2
   work.

4. **The H3 STOP-red correction.** Both rules arms of experiment 2 independently objected that
   forbidding a red STOP is wrong — plant convention overwhelmingly expects red for a
   safety-critical stop. They are right and Lane A will write the carve-out, but it is
   safety-adjacent convention and should carry an explicit owner sign-off rather than an agent's
   judgement. *Recommendation: accept the carve-out, scoped to STOP/E-STOP controls only.*

5. ~~**Which panel family do the paying jobs actually use?**~~ ✅ **ANSWERED 2026-08-17 by the
   owner: Classic Basic, first application a Basic 7", anchored on `JOB9003 - K150 Demo - Scratch
   Copy` (KTP900 Basic).** It redirected the programme, as predicted — §0 is rewritten around it.
   Retained rather than deleted because the *reasoning* still applies to the expansion order: each
   new family is a capability question or a second back-end, and which one is never obvious from the
   outside.

6. **Is a dedicated Portal instance available for this work?** Not required, but it changes the
   shape of waves 2–3 — see §5a. *Recommendation: take it if offered, and pair it with two or three
   dedicated scratch projects, because the projects are what actually buy the parallelism.*

7. ~~**BLOCKING: write access for the thin spike.**~~ ✅ **GRANTED 2026-08-17 by the owner**, with an
   independent master copy held for resets (`docs/13-data-boundary.md`). The spike has a write target
   and wave 1 is unblocked. Hard rule 5's "work freely against the scratch copy" applies here in
   full. **What did not change: the retention rule.** Write access widens what may be *done to the
   project*, not what may be *retained about it* — nothing verbatim from JOB9003 enters a committed
   doc. Two independent axes; only one moved.
   ⚠️ Disposable is not free: a reset costs the owner an action. Prefer additive work.

---

## 5a. If a dedicated Portal instance is provided

The token constraint is **one session per *project***, not per Portal. Concurrent Portal sessions on
*different* projects are explicitly safe and were hardened as a design in July 2026. So:

**What a dedicated instance buys**

- **Decoupling, which is the main prize.** The HMI programme stops contending with `lad-coder` runs,
  harness work and live jobs for the token. Lane P's queue stops being interleaved with everyone
  else's.
- **The rebuild hazard is contained.** Today, rebuilding `openness-cli` mid-flight wedges whoever
  holds Portal, and the failure looks exactly like a first-connect approval dialog that is not there
  — an hour lost, once, already. On a dedicated instance that risk stays local to this programme.
- **Lane P can split in two, but only with more projects.** P1 (layer probe), P2 (schema dump on the
  Template Suite project) and the thin spike are mutually independent and can each own a scratch
  project. With 2–3 projects, Lane P becomes **P-probe** and **P-apply** running concurrently.

**What it does *not* buy — and this is the honest half**

- **Wave 1 barely moves.** Its critical path is Lane B, the toolchain, which never touches Portal.
  Three of the four wave-1 lanes are unaffected by any amount of Portal capacity.
- **Parallelism is bounded by projects and by RAM, not by instances.** Each concurrent session is a
  separate Portal process with a multi-minute project open. Two is comfortable; beyond that, the
  "second instance won't connect" symptom is real and observed — it correlates with stale process
  pileup, so `openness-cli portal-status` becomes routine hygiene rather than a diagnostic.
- **It does not touch ADR-0007.** A dedicated instance is capacity, not permission.

**Net:** worth taking, mostly for waves 2–3 and for the rebuild containment. It compresses the
Portal-bound tail; it does not compress the front of the programme, because the front is not
Portal-bound.

---

## 6. Risks carried into the build

Not repeated in full — `research/hmi-research.txt` §9 is the register. The four that shape *this
plan* specifically:

- **P1 negative** (layers not settable through Openness) — Lane P wave 1 finds out. Survivable:
  most screens are single-layer, and the fallback is single-layer output plus a hard error on any
  source needing more than one tier. Never a silent flattening.
- **Chrome version drift** changes the flattened integers. Lane B pins the version and treats a rect
  delta as build-breaking — the shape `tests/golden/` already is.
- **Font metrics.** The probe used a fallback font. Siemens Sans must be installed in the flattening
  Chrome or every text rect is wrong. Untested; Lane B's first job after promotion.
- **A memory-layout-class trap.** Assume one attribute is silently reset on re-import and invisible
  to every check. Lane P designs the read-back comparison to *notice* rather than trusting it will
  not happen. It has happened before, on `MemoryLayout`, and cost more than the check would have.

---

## 7. What already exists, so nobody rebuilds it

**Usable on Classic — the current target:**

- **`openness-cli export --screen` / `import`** — the whole Classic screen pipeline.
  🔴 **CORRECTED 2026-08-17 (wave 1): this was NOT true as written.** `export` was PLC-shaped
  (`--block`/`--type`/`--tagtable`) and could not reach a screen at all, so "T6 already ships" was
  wrong and the flag had to be built. It exists now, and Unified is refused BY NAME rather than
  returning an empty file. See `wave-1-results.md`.
- **`converter compare` + the `Normalizer`** — T7 is this, re-aimed at screen documents.
- **The round-trip harness and `drift-check`'s invariance idea** — transplant directly.
- `converter tagstatus` — the anti-laundering gate T8 wraps.
- `hmi/prototypes/` — T1, T2, T3 as working prototype code, proven in experiment 2.
- `research/experiment2/{direct,tooled,rules-only,hybrid}/` — **four screens with known scores**,
  Lane B's regression baseline, already authored.

⚠️ **Unified-only, so NOT available on the current target — do not plan against these:**

- `openness-cli hmi` (screen/item walk, `--schema`, `--scripts`), `hmi-create-screen`,
  `hmi-edit-screen`, `hmi-compile`. All resolve through the Unified object model, which Classic does
  not have. They become relevant again at expansion step 3 (§0), not before.
- 🔴 **Which means the Classic gate is an open question, not an inherited one.** The Unified plan
  leaned on `hmi-compile`; here the gate is a **device compile**, and FI-52 is the standing record of
  how little a device-level compile proves on its own. Settling what actually gates a Classic screen
  is Lane P wave-2 work and should not be assumed solved.
- `hmi/prototypes/` — T1, T2 and T3 as working prototype code, proven in experiment 2
- `research/experiment2/{direct,tooled,rules-only,hybrid}/` — **four screens with known scores.**
  Lane B's regression baseline exists already and did not have to be authored for the purpose.

---

## Scope gate

HMI engineering remains a **non-goal** under `docs/10-non-goals.md` pending **ADR-0007**, which is
*Proposed* and undecided. This plan is a plan; it is not a licence, and nothing in it changes scope.
Waves 1's non-Portal lanes and Lane P's probes are defensible under existing precedent. Everything
from wave 2 onward is exactly the decision ADR-0007 exists to make.

**Do not build this by drift.**
