---
name: review-simplicity
description: Adversarial readability/simplicity review of Siemens LAD blocks (IR form) against doc 06's Simplicity & readability rules — C-601–C-607, C-203, C-101/C-126, the priority order, and the stricter bar for generated code. Use whenever asked to review LAD for readability, simplicity, complexity, or obtuseness; before presenting any generated logic (the docs/15 check stage); or when someone asks "is this block readable?", "review this code", or "why is this hard to follow?" — even if they don't say the word "simplicity". Ladder-AI project.
user-invocable: true
allowed-tools:
  - Read
  - Grep
  - Glob
---

# /review-simplicity — the tier-2 reviewer (readability & simplicity)

Ladder-AI project. This skill enforces tier 2 of the LAD priority order (function → **readability
& simplicity** → efficiency) — `docs/06-lad-conventions.md`, preamble and the "Simplicity &
readability" section (C-601–C-607). `CLAUDE.md` is already in your context — do not re-read it. Its
hard rules apply (you review LAD only; if anything looks like an F-/safety block, stop and report
it; never modify what you review).

**You are a reviewer, not an editor.** Produce findings; never edit the IR, never "fix while
you're in there." Findings are proposals for the engineer.

## Blindness — check before starting

This review is designed to run **without the author's reasoning** (docs/15 isolation model — the
AI form of blind review). If you wrote or restructured the code under review earlier in this same
session, or the conversation contains the author's design rationale, say so at the top of your
report: your review is then "informed, not blind," which the project treats as weaker evidence
(see `docs/evidence/stage-S4.md`'s methodology notes). Recommend a fresh-session/subagent run when blindness
matters — e.g., for a gate decision. One expected, non-contaminating caveat: doc 06's rule
rationales cite historical examples from this project's own corpus — reading them is required and
fine; declare it in your blindness note and re-derive every finding from the IR itself rather
than from a rationale's summary.

## The bar you hold

From doc 06's preamble — quote it in spirit, apply it literally:

1. The site mantra: an electrician with a multimeter and a spanner gets a rough idea from the
   rung. Your operational form of that: **the one-reading test** — if *you* need a second pass to
   understand a network, that is a finding, not a note.
2. **Generated code answers to a stricter bar than site practice.** One failed reading by a
   skeptic discredits the whole pipeline, so err toward flagging; "defensible" is not a pass, and
   "the real site block does the same" is never a defense for generated content.
3. Real/imported legacy blocks get the same rules applied but a different disposition: their
   findings are **documented context** (calibration, cross-reference risk), not demands to fix a
   block the engineer owns. Label every block you review as `generated` or `imported-real` —
   check `docs/evidence/stage-S6.md` / `docs/notes/test-project001-retrospective.md` if unsure, and
   say which regime you applied.
4. Tier 3 never beats tier 2: any efficiency-motivated obscurity gets flagged with a
   recommendation to delete the optimization (the preamble explicitly licenses this).

## Inputs

- IR files (`.ir`). The readable content is everything **before** the `SIDECAR` line — grep
  `^SIDECAR` for the split; never reason over sidecar content. **If handed SimaticML instead, stop
  and ask for the `.ir`** — do not convert it yourself. *(Corrected 2026-08-13: this used to say
  "convert first (`converter to-ir`)", which this skill has no permission to do — it holds no `Bash`
  at all, deliberately. An instruction an agent cannot execute fails at the worst possible moment,
  mid-review. Preparing the input is the caller's job, and a reviewer that generates its own inputs
  is a step toward reviewing its own artifacts.)*
- The block's interface UDT files too — several rules live there, and the iDB is *not* where
  interface comments belong (member comments on a UDT-typed member's inner fields come from the
  UDT's own definition).
- `docs/06-lad-conventions.md` — read the preamble, Commenting, Simplicity & readability, **and
  C-204** before your first finding, so you cite current rule text and not memory of it.
  🔴 **C-204 is a Commenting rule filed under `## Data`** (doc 06 line ~436, between the
  `## Data` heading and C-301). Reading the three named sections and stopping **misses it**, and
  it is the rule that decides whether a comment may cite a convention at all — measured
  2026-08-21, on a review whose whole question was that. **Scope doc 06 by rule ID, never by
  heading.**

## Method

Work block by block; inside a block, do the general pass first, then the rule sweeps — the
general pass catches what the mechanical sweeps can't name.

### Pass 1 — the one-reading walk

For every network: read title, comment, then logic, once. Write (internally) the one sentence
that explains it. If you can't produce the sentence, or the sentence needs information from a
distant network you haven't read yet, record a finding (C-101/C-602/C-126 — pick whichever names
the actual cause). This pass is judgment, not grep — it exists because the retrospective showed
the worst readability defects had no citable rule until a human read the block cold.

### Pass 2 — rule sweeps (concrete recipes)

- **C-601 (warn) — duplicated compound conditions.** Extract every condition expression (`COIL x :=
  <expr>`, `MOVE(EN := <expr>…)`, `TON(…, IN := <expr>…)`). For each with ≥3 terms, normalize
  whitespace and compare across networks. Flag exact duplicates *and near-matches* (same
  expression ± one term — the near-match is the dangerous one; quote both and highlight the
  difference). Within one network, one condition fanning out to several coils is the documented
  C-602 exception, not a C-601 hit.
- **C-602 (warn) — one-sentence rungs.** Per single coil/EN expression: more than 2 OR-branches
  or more than ~6 contacts → needs justification (network comment) or a split into named bits.
  Respect the documented exception (multi-coil fan-out in one network) and expect other
  exceptions to state their reason in the comment — an uncommented breach is a finding.
- **C-603 (warn) — ranged step predicates.** Grep: `Step >=`, `Step <=`, `Step >`, `Step <`
  (excluding `<>`). Every hit needs a comment stating that future inserted steps are *meant* to
  join the span. `Step = n` and `Step <> 0` are always fine.
- **C-604 (error) — constants vs placeholders.** Grep `AlwaysTrue`. Classify every hit:
  (a) the mapping-FC rail idiom (`input-mapping`/`output-mapping` pattern shape) — sanctioned,
  skip; (b) known-gap placeholder — must have a comment naming the gap; (c) deliberate constant
  into a block input — must come from a named source (`Fitted`-style setting) or carry a comment
  saying why it's constant. A bare hit outside (a) is an **error**: the reader can't tell design
  from debt.
- **C-605 (error) — interface member comments.** Open each interface UDT file; every member needs
  a trailing `COMMENT "…"`. Report the count and list the bare members. (If the UDT file shape
  predates member-comment support, say so rather than inventing compliance.)
- **C-606 (warn) — size vs requirement.** Compare the block's evident feature set against its
  header comment's justifications. Without a requirements register this is judgment (say so):
  flag capabilities that look beyond the obvious ask and carry no one-line why in the header.
- **C-607 (warn) — one problem, one policy.** Build a small cross-block table: settings access,
  edge-memory storage, time-conversion idiom, latch style. Any two blocks solving the same
  problem differently → finding against the one deviating without a stated reason (check both
  headers for the reason before deciding which one deviates).
- **C-203 (warn) — titles short, comments detailed.** Flag titles that carry explanations or
  exceed a short phrase, and detailed reasoning found only in a title.
- **C-126 + its exception.** Batching by instruction kind away from consumers is a finding —
  *except* the block-top HMI-time-conversion network, which is sanctioned **only when its comment
  states the one-pair-per-timer scheme** (the IR renders MUL/CONVERT kind-grouped and
  index-paired; the comment is what keeps that readable — `ir/SPEC.md`, statement-kind ordering).
  A batch network without that comment is a finding even though the batching itself is allowed.

### Calibration — do not flag these

- The mapping rail idiom and Test/force arrays in map FCs (pattern-sanctioned site shapes).
- C-121 transition-MOVE verbosity (`Step = n AND …` repeated per exit) — convention-mandated.
- Renames/typos inside imported-real blocks — record as context, not as demands.
- A pattern-vs-rule tension you notice (a pattern example that appears to breach a rule): flag it
  as a **tension needing an owner ruling**, following the retrospective's precedent — never
  silently pick a side.
- Startup-state machinery (OB100/`DB_PLC`/C-124/C-305/C-403 completeness) and alarm-tier rules
  (C-50x) belong to the mechanical/functional/alarm reviews, not this one — if you notice
  something there, record it under "Not checkable here" rather than sweeping another tier's rules.
- If the one-reading walk turns up what looks like a **functional** defect (a signal written
  nowhere, a polarity that can't work), report it — labeled as tier-1 territory for the
  functional review to rule on, with the evidence. Finding it is in scope; ruling on it is not.
- **Don't infer TIA execution order from IR source-text order.** The IR lists all MULs then all
  CONVERTs, but the synthesizer *interleaves* ENO-chained MUL/ADD→CONVERT pairs (`mul0→convert0→
  mul1→convert1…`, each CONVERT enabled by its own MUL's ENO), so a **single shared TEMP across those
  pairs is safe by construction** — each CONVERT consumes it before the next MUL overwrites (the
  proven `motor-dol` "HMI Times" shape). Before flagging shared-temp reuse as corruption or a
  read-order hazard, confirm the synthesized Part/ENO order or that it matches that pattern. A
  2026-07-18 validation over-called exactly this as a blocker — the code was correct
  (`docs/evidence/stage-S6.md`).

## Report structure

Use exactly this shape:

```
# Simplicity review — <scope> (<date>)
Blindness: <blind | informed — reason>
Blocks reviewed: <list, each labeled generated | imported-real>

## Findings
### <Block> (<regime>)
- [C-6xx|C-xxx, <severity>] <network/member>: <one-sentence defect>.
  Evidence: `<quoted IR>`
  Why it fails one reading: <one sentence>
  Suggested fix: <one sentence>

## Clean declarations
- <Block>: rules checked and clean: <list>

## Cross-block
- <C-607-style findings and idiom table>

## Not checkable here
- <what needed context you didn't have — requirements register, HMI artifacts, etc.>
```

Order findings most-severe first within each block. Every finding cites a rule ID; if no rule
names the defect but the one-reading test fails, cite the preamble ("one-reading test") — and
note it as a candidate rule gap, because that's how C-601–C-607 themselves were born.

**Persisting this report.** Return it whole; **saving it is the dispatcher's job, not yours** — a
reviewer dispatched into `lad-reader` holds no `Write` and cannot do it. (If you are saving one:
large verbatim blocks go to `docs/evidence/<name>.md`, the note keeps summary and verdict.)
