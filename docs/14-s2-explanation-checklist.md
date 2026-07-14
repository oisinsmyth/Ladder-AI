# 14 — S2 Explanation Quality Checklist

**Status: POPULATED — first version, derived empirically from directly comparing real trial
explanations against each other and against source (2026-07-14), not invented in the abstract.
Revise as more sampled explanations surface new failure patterns.**

## Purpose

Per `02-roadmap.md`, S2's exit criterion is 10 sampled network/block explanations judged accurate
by the engineer, with no hallucinated tags or behavior (`04-design-philosophy.md` #7, "Never
invent reality"). This is the checklist that judgment runs against — for the engineer reviewing an
explanation, and for Claude Code self-checking one before presenting it.

This is not `11-review-workflow.md`'s checklist. That one governs S6/S7 AI-generated or -modified
logic going into TIA — safety of a change. S2 never modifies anything; this list judges accuracy
of description only.

## How this was derived

Built by producing multiple independent explanations of the same real block — varying how much
project/process context each attempt was given, from a bare task description up to full staging
context plus explicit methodology guidance — then verifying every claim in every attempt against
the actual source. Every item below traces to a real, observed failure or success in that
comparison, not a hypothetical.

## Checklist

- **E-01** — Every "uniform / always / never" claim about a repeated field or pattern across
  multiple instances is checked against *every* instance, not a sample. The single most common
  failure observed: a confident count or uniformity claim, plausible-sounding, wrong on direct
  recount — it happened twice, independently, in the same comparison. Treat any such claim as
  unverified until checked exhaustively.
- **E-02** — Every claim about what a shared field/tag means is grounded by reading the logic that
  actually defines or consumes it (a called block, a dependency FB), with that source cited — not
  inferred from the name alone. A naming-based guess never checked against source stays a guess,
  not a finding.
- **E-03** — Anomalies that look like they could be conversion/tooling artifacts (odd characters,
  typos, unusual constructs) are confirmed against the raw export before being reported as real.
- **E-04** — Every instance of a repeated template/pattern is individually checked for
  self-consistency, not described from 2-3 representative examples and generalized. This is where
  copy-paste drift hides — invisible to a description built from samples, found every time a run
  actually checked all instances.
- **E-05** — Inference is flagged at the point it's made, not only in a closing disclaimer, and
  distinguished by kind: read fact / grounded inference (cross-referenced against real source) /
  background or domain inference (general knowledge, not from this project's data).
- **E-06** — The data-boundary approval covering this data's use is checked and cited before real
  tag/equipment names appear in the explanation (`13-data-boundary.md`).
- **E-07** — Nothing is asserted that isn't directly visible in what was actually read. A real gap
  (a caller not in this export, a dependency not opened) is stated as a gap, not silently filled
  with a plausible guess.
- **E-08** — A flagged anomaly's downstream consequences are traced where practical, not just the
  anomaly itself — which specific other logic depends on it, and what breaks if it's wrong.

## Applying it

Not every item fires on every explanation — a small, non-templated block may have nothing for
E-01/E-04 to check. This is a checklist to run through, not a score: an explanation that skips a
firing item (e.g., a real repeated-pattern block where every instance obviously wasn't checked) is
not S2-exit-ready regardless of how well-written it reads.
