---
name: review-functional
description: Tier-1 functional review of Siemens LAD blocks (IR form) against a requirements register (gen/<project>/requirements.md) — per-REQ trace with verdicts, unimplemented/disarmed/contradicted REQs, and unrequested logic / gold-plating via a reverse pass. Use before presenting any generated logic (the docs/15 check stage), or whenever asked "does this do what the spec says?", "trace the requirements", "is anything missing or extra?", "review this against the spec" — even if they don't say the word "functional". Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
---

# /review-functional — the tier-1 reviewer (function)

Ladder-AI project. This skill enforces tier 1 of the LAD priority order (**function** →
readability & simplicity → efficiency) — `docs/06-lad-conventions.md`, preamble: *it does what the
requirement says*. Read `CLAUDE.md` at the repo root first if you haven't — its hard rules apply
(you review LAD only; if anything looks like an F-/safety block, stop and report it; never modify
what you review).

**You are a reviewer, not an editor.** Produce findings; never edit the IR, never "fix while
you're in there." Findings are proposals for the engineer.

**The compile gate is never sufficient evidence of function.** This review exists because a
compile-clean, presented build shipped an in-cycle lamp that could never light (its interface
member was consumed into the physical output but written nowhere) and a stop button whose polarity
held the plant permanently stopped. Both compiled. Both survived a readability review. Function is
checked by tracing, not by building.

## Blindness — check before starting

This review is designed to run **without the author's reasoning** (docs/15 isolation model — the
AI form of blind review). If you wrote or wired the code under review earlier in this same
session, or the conversation contains the author's design rationale, say so at the top of your
report: your review is then "informed, not blind," which the project treats as weaker evidence.
Recommend a fresh-session/subagent run when blindness matters — e.g., for a gate decision. One
expected, non-contaminating caveat: doc 06's rule rationales and the register's own notes cite
historical examples from this project's corpus — reading them is required and fine; declare it in
your blindness note and re-derive every verdict from the IR itself, never from a note's summary.

## Inputs

- **The requirements register: `gen/<project>/requirements.md` — REQUIRED.** Its REQ-nnn entries
  are the trace targets; its tag-status marks, open questions, and C-113 sequence classifications
  are review inputs. **Stop condition:** if no register exists for the project, STOP and say so —
  `gen-spec-analysis` (docs/15 stage 1) must produce one first. Never review against remembered,
  improvised, or conversation-supplied requirements; a functional review without a committed
  register is not a functional review.
- **The full IR corpus** (`ir/<project>/`, every file — blocks, UDTs, DBs, instance DBs, tag
  table), never digests: docs/15's own rule — a digest is never review input. The readable
  content of an `.ir` file is everything **before** the `SIDECAR` line — grep `^SIDECAR` for the
  split; never reason over sidecar content. If handed SimaticML instead, convert first
  (`converter to-ir`) into your own scratch area.
- `docs/06-lad-conventions.md` — read C-113/C-118 (sequence paradigms) and C-604/C-606 before
  your first finding so you cite current rule text, not memory of it.
- Regime labels for every block: `generated` or `imported-real` — from the invoker, or
  `docs/evidence/stage-S6.md` if unlabeled. Say which regime you applied to each block.

## Method

Two passes, both mandatory, in this order. Pass 1 answers "is everything that was asked for
built?"; Pass 2 answers "is everything that was built asked for?". Skipping Pass 2 is how
gold-plating and dead wiring survive.

### Pass 1 — forward trace, REQ by REQ

For every REQ in the register (including `withdrawn` ones — confirm the logic is gone or note
what remains), trace the behavior **field boundary to field boundary**: input tag → input-map FC
→ input buffer member → wiring FC → FB/FC logic → interface output member → wiring → output
buffer member → output-map FC → output tag. **A REQ implemented inside an FB but broken at any
wiring hop is NOT implemented** — the in-cycle-lamp failure lived at a wiring hop, not in a
block's logic. Grep every hop; never assume a member is wired because its name matches.

Verdict vocabulary — exactly one per REQ:

- **implemented** — the full chain exists and does what the REQ text says.
- **partial** — some of the chain or some of the REQ's clauses exist; state precisely which part
  is missing. An unconfigured setting on an otherwise-complete chain is `partial` — cross-cite
  the register's open question for it.
- **unimplemented** — no logic serves the REQ (or the chain is broken such that the behavior can
  never occur: e.g. a consumed-but-never-written member in the path).
- **disarmed** — logic for the REQ exists but is placeholder-gated so it can never act: grep
  `AlwaysTrue` along the trace path; a `NOT AlwaysTrue` (or equivalent constant) gating the REQ's
  active path means the feature is built but switched off. **Disarmed is NOT implemented** — it
  never gets a pass, however complete the gated logic looks.
- **contradicted** — logic exists and does the opposite or something materially other than the
  REQ text (e.g. a polarity that asserts stop permanently; a start value that disagrees with a
  spec-stated number).
- **not-statically-checkable** — the verdict genuinely needs execution (timing races, retentive
  interactions, field behavior); list it as an S9 sim-test candidate keyed by REQ ID. Use
  sparingly — most sequence logic IS statically traceable.
- **out-of-scope** — the register classes it out-of-scope (hardwired/safety); confirm no PLC
  logic pretends to own it, and never trace into safety internals (hard rule 2).

Every verdict carries block + network numbers and **quoted IR evidence** — grep-grounded, never
from memory. No verdict without a quote.

**Timing/number checks** (any REQ whose text or register notes state a number — durations,
counts, setpoints): verify (1) the named setting member exists where the register says; (2) the
conversion chain (the site ×1000 seconds→ms MUL/CONVERT idiom) reaches the **right** timer's
`PT` — follow the value, the pair-crossing trap is real; (3) the DB start value matches the
spec-stated number. Mismatched number = **contradicted**. Unconfigured (no start value) =
**partial**, cross-citing the register's open question. Threshold counts compare against the
named threshold member, not a literal.

### Pass 2 — reverse trace (gold-plating and dead structure)

Enumerate what the corpus actually contains and map each item back to a REQ ID:

- **Every network's evident function** (title + logic), every block.
- **Every interface-UDT member** — declared members that no network reads or writes are findings.
- **Every buffer-DB member** — check writers AND readers via grep, both directions:
  **consumed-but-never-written** (the in-cycle-lamp class: an output fed by a member nothing
  drives) and **written-but-never-consumed** (the dead-selector-input class: a mapped field
  signal nothing reads) are both findings, whichever direction is dead.
- **Every `DB_Settings` member's consumer** — a setting nothing reads is dead configuration.
- **Every alarm bit** — each maps to an alarm-class REQ or is unrequested.

Anything with **no REQ** is *unrequested logic*: check the block header comment for a C-606
justification line — present means `justified-unrequested` (still listed in the report; the
engineer decides), absent is a C-606 finding. Do not silently accept plausible extras: the
register records WHAT was asked; "obviously useful" is not a REQ.

**Sequence-paradigm check:** compare each sequencing block's actual shape against the register's
C-113 classification table (stepped where classified stepped, combinational where combinational).
A mismatch cites C-113/C-118 — it is a functional-structure finding, not a style note.

**Tag-status check (anti-laundering, error severity):** any logic referencing a tag the register
marks `proposed` is a **pipeline breach** — docs/15's tag-status rule exists precisely so
"proposed in analysis" can never mutate into "assumed real in code." Report it as an error
regardless of whether the logic works.

## Calibration — the tier boundary

- **Function tier only.** A readability observation gets a one-line route note to
  `review-simplicity`; convention/style to `review-conventions`; alarm texts, severities, and
  suppression design to the alarm-design tier. But **"an alarm exists for the REQ's stated
  cause" IS functional** and is checked here — only its wording/severity/suppression is another
  tier's business.
- **Regimes:** unrequested features in an `imported-real` block are *documented context*, not fix
  demands — real site blocks carry history this project didn't ask for. A REQ may legitimately be
  satisfied by an imported block: trace into it read-only and say so. Generated blocks answer for
  every feature.
- **Pattern-sanctioned shapes are not disarmed:** the mapping-rail `AlwaysTrue` idiom and
  `Test[]` force arrays in map FCs are the admitted input/output-mapping pattern shape — skip
  them. A placeholder `NOT AlwaysTrue` gating a REQ's path is exactly what **disarmed** names —
  the difference is position (rail vs. gate on the REQ's active path).
- **The register's open questions are carried, never answered.** If a trace touches one, cite it
  (`Q-nn`) and report what the corpus does today; the answer belongs to the owner. Newly raised
  questions go in your report's own section, clearly marked as new.
- **Stricter bar for generated code** (doc 06 preamble): err toward flagging; "compiles and looks
  plausible" is not a pass; a defensible-but-untraceable behavior is `partial` or worse, never
  waved through. One functional miss discredits the pipeline, not just the block.

## Report structure

Use exactly this shape:

```
# Functional review — <scope> (<date>)
Blindness: <blind | informed — reason>
Register: gen/<project>/requirements.md @ <git hash | unknown>
Corpus: <path> @ <git hash | unknown>
Blocks read: <list, each labeled generated | imported-real>

## Per-REQ trace
### REQ-nnn — <name>: <verdict>
Where: <block/network(s) — the full chain for chain verdicts>
Evidence: `<quoted IR>` <(one quote per load-bearing hop)>
Notes: <missing parts for partial; Q-nn cross-cites; S9 handoff if not-statically-checkable>

## Unimplemented / disarmed / contradicted REQs
- <REQ-nnn — one-line rollup each; this section is the headline — empty is a statement>

## Unrequested logic (reverse pass)
- <feature> — <block/network> — <generated | imported-real> — header justification
  <present | absent> [C-606]

## Suspected functional defects outside any REQ
- <dead members, broken chains, self-fighting writes — with evidence>

## Open questions (carried + newly raised)
- <Q-nn carried, with what the corpus currently does> / <NEW: …>

## Not statically checkable (S9 sim-test candidates, keyed by REQ ID)
- <REQ-nnn: what a sim test must exercise>
```

Take the register and corpus git hashes from the invoker if provided (`git log -1 --format=%h --
<path>` is the source of truth); write `unknown` rather than guessing. Order the per-REQ trace in
register order — the register's numbering is the report's spine. Every non-`implemented` verdict
must be findable in a rollup section; nothing material lives only in the trace table.

**Persisting this report.** Returned live in conversation, keep it whole. When it is saved as a
committed doc (a skill-validation record, a kept gate review), split it at write time: any large
verbatim block — a full blind-run transcript, or the complete per-REQ trace when it runs long —
goes in `docs/evidence/<name>.md`; the saved note keeps the rollup sections, verdict, and a link.
Large raw dumps live in `docs/evidence/`, never inline in `docs/notes/` or
`docs/notes/stage-gates.md` — the split convention, stated in `docs/15-generation-pipeline.md`
("Artifacts"), origin FI-19/FI-20 in `docs/16-future-ideas.md`. Born in the right shape, not split
by hand later.
