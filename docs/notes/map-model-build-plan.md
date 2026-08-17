# MAP MODEL — BUILD PLAN

**Written 2026-08-17. NOT STARTED. NOT AUTHORISED.** This is a costed plan held for later
execution, not queued work.

**Design source of truth: `docs/adr/adr-0012-map-model-and-order-waves.md`.** This document does not
restate the model — read the ADR first. It answers one question: **what does building this cost, in
what order, and what breaks on the way?**

Analysis entry: `docs/16-future-ideas.md` **FI-76**.

---

## 🛑 THREE GATES BEFORE ANY OF THIS STARTS

1. **The staged development plan is SUSPENDED** (`docs/03-development-plan.md`, 2026-08-17). Building
   this *is* pipeline development. It needs the owner to unsuspend or to carve it out explicitly.
   Neither has happened. `agent-tasks/` dispatch is suspended too, which is why this plan lives here
   and not on the board.
2. 🔴 **ADR-0012's open item 1 — the mixed-order block — must be closed first.** A block consuming
   state from an order-1 block *and* an order-3 block: order 2 by "lowest", order 4 by `1 + max`, or a
   violation? **Item 2 below cannot be written without the answer**, and it is cheap to decide now and
   expensive to discover at wave 3.
3. **Do the half-day retrospective first (see *Cheapest first slice*).** It can kill the whole spend.

---

## THE NUMBERS THIS PLAN RESTS ON

All measured 2026-08-17 against the repo, not estimated:

| Fact | Value | Where |
|---|---|---|
| Total logged pipeline time | **1,637 min** across 20 stage types / 45 runs | `gen/*/telemetry.log` |
| What the cartographer replaces (C + D + `gen-architecture`) | **477 min — 29%** | same |
| `gen-architecture` alone | **202 min — largest single stage in the log** | same |
| Parallelisable fraction under the map model | **35% conservative / 54% generous** | ADR-0012 |
| Expected saving, 4 effective agents | **26–40% wall clock** (~54% ceiling) | ADR-0012 |
| Portal round-trips, all logged coding runs | **16 total, ~1.6 per block** | `gen/*/telemetry.log` |
| `src/wave-control/` | **15,560 prod + 8,240 test lines, never run with two agents contending** | measured |
| Claims registry | **2,711 lines, 6 kinds** (`block-number`, `alarm-bit`, `db-member`, `block-network`, `tag`, `block-edit`) | measured |
| Repo references to the 3 replaced skills | **205 mentions / ~70 files** | measured |
| Repo references to their 3 artifacts | **183** | measured |

**Sample caveat that must travel with the economics:** one plant across three reruns plus
test-project001 and the validation corpora — not independent samples. Wall clock is confounded by
Portal state (`gen-telemetry.md` says so itself). Several PlantAutoControl runs ended `blocked`, so their
minutes include stopping to raise questions. The design-to-coding ratio is ~3:1 in favour of design,
which is too wide to be noise.

---

## THE WORK — 9 ITEMS, ~10–13 AGENT-DAYS, ~6–8 CALENDAR DAYS SEQUENCED

### Item 1 — map artifact format + parser · ~600 prod / ~450 test · 1 d · confidence HIGH

**Consumes:** rung A `equipment-topology.md`, rung B `plant-behaviours.md`, the IO table.
**Produces:** the map artifact — territories (name, kind, anchor, order, state), border crossings
(direction, type, member), and enough to feed items 2 and 3.

**It must be a parseable format from day one.** Prose that gets formalised later is how the mechanical
floor is lost — see item 3.

**Basis:** comparable to `RelationReconcile` (485 prod / 503 test).
**Verified by:** round-trip parse of a hand-authored map; malformed input is a hard error, never a
silent zero-reading (`FI-44`, empty is not clean).

### Item 2 — `converter map-check` · ~650 prod / ~500 test · 1–1.5 d · confidence MED-HIGH

The tiling checker. Four questions:

- **Gaps / overlaps** — is the map tiled? Overlap = C-308 as a structural invariant.
- **Order** — `1 + max(order of anything whose interface you must know to write yourself)`. Order 1 =
  externally anchored (hardware *or* class library — two anchors, the horseshoe).
- **Cycles** — **reported as a violation, never condensed.** A cycle means coordination logic landed
  inside an equipment block (C-127 under a new name). Do not make this graceful.
- **Crossing discipline** — every border crossing is an interface member, no global-DB reach-through.

**🔴 BLOCKED ON GATE 2** — the order rule is the mixed-order decision.
**Reuses, does not reimplement:** `reachable-state`'s closure machinery (348 prod / 451 test) for
overlap detection.
**Basis:** `reachable-state` + `cross-check` (206).
**Verified by:** a corpus with a known planted overlap, a known cycle, and a known gap — each caught
by name. Assert the clean case reports its **denominator** (how many territories compared), not just
a pass.

### Item 3 — re-point `relation-reconcile` + `signal-sweep` · ~250 changed / ~200 test · 0.5 d · HIGH

⚠️ **Exactly two checks, not four.** `docs/15` says *"`relation-reconcile`, `signal-sweep`,
`candidate-scan` and `undriven-scan` parse these files"*. **Only the first two do**, both through the
shared `RelationArtifactParsers` (241 lines — `ParseSpecs`/`ParseLedger`/`ParseRegister`/`ParseRender`).
**`candidate-scan` and `undriven-scan` take `--project <ir-dir> --fb <FBName>` and read IR only** —
unaffected by any of this. Verified 2026-08-17 by reading the runners.

**Fix `docs/15`'s line regardless of whether this plan ever runs** — it is wrong today and it inflates
every estimate anyone makes from it.

**The seam is `RelationArtifactParsers`.** The map artifact must carry, in some form, the
instance/relation sets and the signal claims those two currently read from the spec files and ledger.

### Item 4 — `territory` claim kind · ~200 lines · 0.5 d · HIGH

Add to the claims registry. Territory is the natural unit under this model and subsumes most existing
kinds. Pattern-following work against 2,711 existing lines.

**Test it the way the registry was proved:** agent A acquires, agent B is **refused on the same value
with A's purpose named**, agent B then acquires a different one (so the fence isn't simply refusing
everything).

🔴 **`--claims` must be `C:\ProgramData\Ladder-AI\claims` — the ROOT, not the project folder.** The
tool appends the project name itself; passing the project folder yields a second empty store that
grants every claim, and two agents making that mistake agree with each other perfectly. **Read the
`store=` line the tool echoes; do not trust the argument.**

### Item 5 — cartographer skill + validation · ~400–500 lines · 2 d · confidence MEDIUM

Replaces `gen-equipment-spec` (282), `gen-code-structure` (235) and `gen-architecture` (299).
Runs **after rung B**. Draws the map, names blocks, emits **one contract per territory**.

**Validation dominates the cost, not authoring** — the four-rung build took two adversarial rounds
(`docs/evidence/four-rung-design-validation{,-round2}.md`). Hold this to the same bar.

🔴 **Start with THICK contracts.** Contract thickness trades conflict safety against parallel fraction
directly: complete contracts are what stop semantic divergence, and completing them moves interpretive
work onto the serial critical path. Thin deliberately where a wave shows the agents did not need the
detail — never start thin and discover divergence in a block that already compiled.

**This is the item most likely to eat the win.** Instrument its wall clock in the existing
`telemetry.log` format against the 477 minutes it replaces. That number is the whole point.

### Item 6 — scout skill + validation · ~230 lines · 1 d · MED-HIGH

Runs **before rung A**. Reads the source once. Produces routing (what is in which document, equipment
inventory, applicable `references/<class>/` entries) and an **ambiguity register**.

🔴 **It names no blocks and no signals.** Rung A bans signals and interfaces; rung B additionally bans
block names. A scout naming blocks upstream of A destroys the abstraction ladder the rungs exist to
enforce.

🔴 **It FINDS ambiguity and never RESOLVES it.** A resolving scout becomes the single upstream reader
and every downstream agent inherits its misreadings systemically — the PlantAutoControl-bench autopsy
failure with better ergonomics. Resolutions come from the engineer.

### Item 7 — wave driver + first two-agent run · 2–3 d · 🔴 confidence LOW — WIDEST ITEM

`src/wave-control/` already holds **15,560 prod lines** — wave sets, slot colouring, admission, model
ordering, escalation — and **has never run with two agents contending.** `Harness.Loop` has never run
a wave end to end.

So this is either a day of glue or a rewrite, and **nobody knows which.**

**Do the half-day probe before costing the rest of this item:** two agents, the existing claims
registry and wave-set admission, a toy map. That single experiment converts the widest number in this
plan into a real one.

**The loop, once driven:** parallel per territory (per-instance spec + IR + vectors) → batched at the
wave boundary (`import-all` → **layout re-assert** → `compile-all` → `sanity-check` → deploy → run the
wave's vectors, plus the project-level half of C/D and the horseshoe joins) → next order.

**Two single-token resources, neither negotiable:** Portal (one lane per project — agents author IR
with no Portal at all) and **the rig** (deployment is device-level, there is one rig — vector
*authoring* parallelises, vector *execution* does not).

### Item 8 — stop-on-gap hard stop · 0.5 d · HIGH

Promote `docs/15`'s "missing info → an open question, never an invention" to a **hard stop** in
`gen-block-new`, `gen-block-modify-fix`, `gen-block-modify-purpose`.

**Why it changes status under this model:** semantic gap-filling is the one conflict class that
**cannot be detected**. Two agents given an underspecified contract fill the gap differently, both
compile, both pass their own vectors, and **nothing collides** — it is not a conflict, it is two
self-consistent readings. `cross-check` catches structural divergence and cannot catch this.

### Item 9 — docs churn · 1.5–2 d · confidence MEDIUM

**The biggest single line, and the one that gets underestimated.**

- **205 mentions of the three replaced skills across ~70 files**; **183 references to their artifacts**
  (`architecture.md`, `equipment-specs/`, `code-structure.md`).
- `docs/15-generation-pipeline.md` — stage table and the four-rung section rewritten.
- `CLAUDE.md` — workflow sections, command table.
- `docs/02-roadmap.md`, `docs/03-development-plan.md`, `docs/notes/stage-gates.md`.

🔴 **This repo cites by literal file path everywhere, and CLAUDE.md warns that a rename-only bulk pass
will not catch pointers left dangling by deletions. Grep repo-wide after every deletion.**
⚠️ **Tracked text here is CRLF and `sed -i` silently rewrites the whole file to LF while
`git diff --stat` still shows a small count.** Use the Edit tool; if you must use `sed`, verify with
`file <path>` and expect `CRLF line terminators`.

---

## SEQUENCING

**Lanes are file-disjoint and can run concurrently** (the owner's standard multi-item shape):

- **Lane T (tooling):** items 1 → 2 → 3, then 4. Item 2 blocked on gate 2.
- **Lane S (skills):** items 6 → 5. Item 5 consumes item 1's format, so lane S needs item 1 landed.
- **Lane D (docs):** item 9, last — it documents what the other lanes actually built, not what this
  plan predicted.
- **Item 7** after lane T lands items 1–2 and lane S lands item 5. **Its probe runs first and alone.**
- **Item 8** is independent of everything and can go any time.

Sequenced this way, **~6–8 calendar days** against ~10–13 agent-days.

---

## WHAT IS *NOT* TOUCHED

Worth stating because the blast radius is narrower than the proposal sounds. **Nothing in the
proven-against-the-controller column changes.**

Converter core (IR, `to-ir`/`to-xml`, Normalizer) · all of `openness-cli` · the harness deploy /
read-back / mirror path · rungs A and B (they become inputs) · `candidate-scan`, `undriven-scan`,
`cross-check`, `drift-check`, `compare`, `diff` · `reachable-state` (**reused**, not changed) · the
three reviewers · `patterns/` · the hard rules · the data boundary.

---

## CHEAPEST FIRST SLICE — DO THIS BEFORE ANYTHING ELSE

**Draw the map retrospectively over the PlantAutoControl bench corpus. Half a day. Zero code, no Portal,
no agents.** Three runs (`gen/PlantAutoControl-bench-rerun{,2,3}/`) and an answer key already exist.

- Reproduces the block structure that shipped → evidence for the 10-day spend.
- Produces a **better** structure → a much stronger result than the economics alone.
- Does **not** reproduce it → the 10 days are saved, and that is worth half a day to find out.

Pair it with **item 7's two-agent probe (half a day)**. Together, one day converts both of this plan's
soft numbers — does the model work, and is the wave driver real — into measured ones.

---

## OPEN ITEMS CARRIED FROM ADR-0012

1. 🔴 **The mixed-order block** — gate 2 above. Blocks item 2.
2. **Does the model predict a rule we do not already have?** If it only re-derives C-127 / C-304 /
   C-308 it is a good restatement; if it predicts one, it is a discovery. Look deliberately.
3. **Where `relation-reconcile` and `signal-sweep` re-point** — item 3's design question.

## REVISIT TRIGGER

The first wave run. **Wave 1 must be small — three or four order-1 blocks, two agents — and its
deliverable is evidence the wave machinery works, not the blocks.** At full width the harness and the
ladder are debugged simultaneously and failures are not attributable.
