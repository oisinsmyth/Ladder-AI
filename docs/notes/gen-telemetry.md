# Generation-run telemetry convention (FI-16)

A proposed format, not adopted pipeline behavior yet: the instrumentation itself happens inside
pipeline runs (`docs/15-generation-pipeline.md`), so adoption lands with that work stream. This doc
exists so measurement can start with the very next generation project instead of being designed
mid-run. Purpose (per FI-16, `16-future-ideas.md`): ground the decisions this project keeps
deferring to measurement — whether FI-12's Portal-session work is worth its risk, which pipeline
stages deserve subagent isolation, where FI-15 digests actually pay.

## Format

One append-only file per generation project: `gen/<project>/telemetry.log`. One line per skill or
stage run, appended when the run ends, pipe-separated:

```
date | skill | wall_clock | portal_roundtrips | tokens | outcome | note
```

- **date** — ISO date+time the run ended (local), e.g. `2026-07-16T14:32`.
- **skill** — the pipeline skill/stage name (`gen-block-coding`, `review-simplicity`, …), or
  `manual:<stage>` when a not-yet-built stage is performed by hand to the same contract.
- **wall_clock** — whole run, start to finish, in minutes (`m` suffix). Interpret with care: this
  is confounded by Portal state (stale-process pileup, first-connect dialogs — see
  `openness-quirks.md`); the note field is where that context goes.
- **portal_roundtrips** — count of import+compile iterations the run needed (`0` for stages that
  never touch Portal). This is the FI-12 number: it sizes what a batch mode or persistent session
  could save, and (multiplied by observed per-roundtrip cost) what FI-13's pre-flight gate saves
  by preventing iterations outright.
- **tokens** — approximate tokens consumed, where the harness exposes a figure for the run; `-`
  when unknown. An estimate marked `~` beats a blank; never invent precision.
- **outcome** — one word: `clean` (deliverable produced, no open findings), `findings` (produced,
  findings attached), `blocked` (stopped on a stop condition / open question), `abandoned`.
- **note** — short free text: what dominated the time, anything that confounds the numbers.

Example:

```
2026-07-16T14:32 | gen-block-coding | 41m | 6 | ~180k | clean | 4 of 6 iterations were "tag not defined" — pre-flight-preventable
2026-07-16T15:10 | review-simplicity | 12m | 0 | ~35k | findings | 3 findings, C-601/C-126
```

## Discipline

- **A log, not a dashboard.** No tooling until the log itself proves too slow to read — the
  anti-goal is instrumentation polish outrunning a two-project sample size (FI-16's own cost note).
- Append at run end, even for abandoned runs — the failures are the most informative rows.
- Never backfill from memory; a missing row beats an invented one.
- Revisit after the first full pipeline project: do the columns answer the FI-12/FI-13/FI-15
  questions? Adjust then, not speculatively.
