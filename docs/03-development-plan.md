# 03 — Development Plan

## Approach

Solo engineer + Claude Code, working stage by stage per `02-roadmap.md`. Each stage is a vertical slice: build the minimum tooling, prove it on the reference project, write the tests, hold the stage-gate review, then move on. No stage is skipped because a later one looks more exciting — the boring stages (round-trip, tests) are what make the exciting ones safe.

## Environment setup (S0 checklist)

1. TIA Portal V20 with Openness installed (done — added post-install via Modify).
2. Windows user added to the **"Siemens TIA Openness"** group; log off/on to take effect.
3. First Openness connect triggers an access prompt inside TIA Portal — accept manually once per Portal version/binary. Document the exact dialog in `docs/notes/`.
4. .NET SDK matching Openness API requirements; reference `Siemens.Engineering.dll` from the TIA V20 installation by path (do not copy Siemens DLLs into the repo).
5. Claude Code installed on the engineering PC; this repo is its working directory; `CLAUDE.md` at repo root. Always launch Claude Code from inside the repo (CLAUDE.md is discovered by walking up from the working directory).
6. Create `docs/notes/stage-gates.md` with S0 marked as the active stage — `CLAUDE.md` references it and the stage check doesn't work without it.
7. Reference TIA project created and exported as the golden corpus.
8. Verify assumption log items A-01 and A-02 (`09-risk-register.md`) during first connect/export attempts; record results in `docs/notes/`.

## Tooling decisions

- **C# for everything that touches Openness** (export, import, compile, enumeration). Openness is a .NET API; C# is the native, best-documented path. Ship as a CLI (`openness-cli`) with plain-text/JSON output so Claude Code can drive it. See ADR-0002.
- **Python allowed for text-side tooling** (IR manipulation, extraction to CSV/XLSX, reports) where it's faster to build. The IR is the contract between the two worlds.
- **Git for everything:** IR files, patterns, docs, tooling. SimaticML exports are committed too (they're the evidence), but the IR is the artifact humans diff. Normalize SimaticML (strip volatile IDs) before commit so diffs are meaningful — see `08-testing-strategy.md`.
- **AutoHotkey macros** stay for UI actions Openness can't do; each remaining macro is a candidate for replacement as Openness coverage grows.

## Ways of working with Claude Code

- Claude Code operates on the repo (IR, patterns, docs, PC-side code) and drives `openness-cli` via shell. It never gets a path that bypasses the compile gate.
- Every AI change lands as a diff the engineer reviews before it is imported to TIA (`11-review-workflow.md`).
- Decisions with lasting consequences get an ADR (`docs/adr/`), even if only a paragraph.

## Milestones

| # | Milestone | Stage | Done when |
|---|-----------|-------|-----------|
| M1 | Openness hello-world | S0 | One command lists all blocks in the reference project, F-blocks flagged and never opened |
| M2 | Lossless round-trip | S1 | Golden-file suite green for the whole reference project |
| M3 | First explanation | S2 | 10 networks explained accurately |
| M4 | First AI write | S3 | AI comments imported + compiled + approved |
| M5 | Convention reviewer | S4 | Findings match engineer review on a sample |
| M6 | Extractors live | S5 | Alarm/IO lists verified against TIA cross-reference |
| M7 | First generated logic | S6 | 10/10 requests compile with real tags |
| M8 | First safe modification | S7 | 10/10 edits with zero out-of-target changes |
| M9 | Simulated verification | S9 | Generated logic ships with a passing sim test |

## Cadence

No calendar commitments — this runs alongside production work. Each stage gets a short kickoff note (what/why/exit criteria) and a stage-gate review against exit criteria before the next stage starts. Open items live in the repo, not in your head.

## Verification discipline

Per `08-testing-strategy.md`: golden-file round-trip tests run on every converter change; the compile gate runs on every AI write; stage exit criteria are checked explicitly, never assumed.
