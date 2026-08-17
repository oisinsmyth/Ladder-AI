# 03 — Development Plan

> # 🛑 SUSPENDED — 2026-08-17
>
> **The staged development plan is suspended by the project owner, for time constraints and real
> application needs.** This notice is the canonical one. Eleven other surfaces carry a **pointer** to
> it rather than their own wording, so there is one place to change when it lifts — `CLAUDE.md`
> (*Current stage*), `02-roadmap.md` (top + its S9 status block), `docs/notes/stage-gates.md` (top +
> every non-DONE table row), `AITODO.md` (*Project stage*, *Current task*, *Outstanding works*,
> recovery step 2), `README.md`, `docs/00-README.md`, `docs/01-scope.md`,
> `docs/15-generation-pipeline.md`, `docs/16-future-ideas.md`, `agent-tasks/README.md`, and the
> `CHANGELOG.md` entry for this date. **`docs/evidence/`, `docs/audit/`, the ADRs and the older
> CHANGELOG entries were deliberately left untouched** — they are dated records of what happened, and
> a suspension does not revise history.
>
> **What is suspended** — the *plan*, meaning the way the work was being sequenced and governed:
>
> - The S0–S9 staged progression in `02-roadmap.md`. No stage opens, closes, or advances.
> - The stage-gate reviews and sign-offs in `docs/notes/stage-gates.md`, including the two still
>   formally open (**S0**'s pending gate sign-off, **S6**'s exit criterion at 1 of 10 fresh requests).
>   These are **frozen where they stand, not failed and not waived** — see the status table.
> - Milestones M1–M9 and the cadence below.
> - The build-order queues that exist only to advance the plan: `docs/16-future-ideas.md`'s FI
>   backlog, `docs/15-generation-pipeline.md`'s remaining skills, `agent-tasks/`.
>
> **What is NOT suspended** — and this is the point of the suspension, not an exception to it:
>
> - **`CLAUDE.md`'s hard rules, in full.** A suspended plan does not relax LAD-only, safety, tag
>   invention, the compile gate, the human promotion gate, IR-not-SimaticML, or `lad-coder` dispatch.
> - **`docs/13-data-boundary.md`, in full**, including the `Live Runs/` retention rule (use anything,
>   commit nothing).
> - **The tooling that is already built and proven** — `openness-cli`, `converter`, the skills, the
>   harness. It stays usable for real jobs; suspending the plan does not withdraw the capability.
> - **Real application work in `Live Runs/`**, which is what the suspension is making room for.
>
> ⚠️ **The reading recorded here, so it can be corrected rather than assumed:** *"real application
> needs"* is taken to mean live delivery continues while pipeline development stops. If the owner
> meant the whole project pauses — live jobs included — this notice is wrong in one direction only
> (the "NOT suspended" list) and should be cut back to the hard rules and the data boundary.
>
> **Status of the work at suspension** is not restated here — `docs/notes/stage-gates.md`'s table and
> `docs/evidence/stage-SN.md` are the record, and they were accurate on this date. Nothing was
> abandoned mid-flight: `agent-tasks/` was empty and `AITODO.md` recorded nothing mid-execution.
>
> **To resume:** delete this block and the pointers listed above; re-read `AITODO.md`'s recovery
> procedure before trusting any narrative in these docs, since the tooling may have moved under live
> work while the plan was suspended.

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
- **C# for the SimaticML↔IR converter too**, despite it never touching Openness — revised in ADR-0002 once `openness-cli` made a .NET toolchain a hard requirement on this machine anyway (it needed the .NET SDK and the .NET Framework 4.8 Developer Pack installed from scratch); a second runtime for the converter wasn't earning its keep. `System.Xml.Linq` is also a strong fit for querying the SimaticML wiring graph.
- **Python reserved for later text-side tooling** — concretely, the S5 extractors (`extract/`: extraction to CSV/XLSX, reports) where its ecosystem (pandas/openpyxl) is a genuine advantage and nothing else forces a second runtime onto the machine. Not needed yet. The IR is the contract between whichever languages sit on either side of it.
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
