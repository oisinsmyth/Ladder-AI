# 01 — Initial Scope

## Purpose

Build a pipeline that lets AI (Claude Code) read, document, review, and eventually write Siemens ladder logic (LAD), with TIA Openness XML export/import as the bridge and a human engineer as the final gate. Nothing reaches a PLC without human review, import into TIA Portal, and a clean compile.

## Context

- Engineering environment: TIA Portal V20 with Openness installed.
- Target hardware: S7-1200 G2 first; architecture must remain portable to other PLC platforms (adding a vendor = writing a converter, not redesigning the pipeline).
- Workplace constraint: **LAD only** on the PLC. No SCL, STL, FBD, or graph languages in deliverables. PC-side tooling may use any language.
- Simulation: PLCSIM Advanced is available (see risk R-07 in `09-risk-register.md` regarding S7-1200 support).

## Hard constraints (non-negotiable)

1. PLC code deliverables are LAD only.
2. Safety logic (F-blocks, F-runtime, anything in the safety program) is permanently excluded from AI reading or writing. See `10-non-goals.md`.
3. All AI-generated or AI-modified logic must import and compile cleanly in TIA Portal before it is presented as done.
4. AI never invents tags. Generation uses real project tags supplied from an export, or explicitly flags a proposed new tag for human creation.
5. A human reviews every change before it enters the TIA project. See `11-review-workflow.md`.
6. No direct download to hardware by any tool in this project. Import into TIA is the last automated step; download is a human act in TIA Portal.

## In scope — initial (Stages 0–1)

- Openness connectivity from a local script: attach to TIA Portal, open a project, enumerate blocks.
- Export of LAD blocks (OB/FB/FC) and supporting objects (tag tables, UDTs, DBs) to SimaticML XML.
- A vendor-neutral intermediate representation (IR) of LAD, text-based and diff-friendly, PLCopen-XML-compatible in structure.
- Converters: SimaticML → IR → SimaticML, proven **lossless** by round-trip diff on a reference project.
- Import of blocks back into TIA and programmatic compile via Openness.

## In scope — later stages (summary; detail in `02-roadmap.md`)

Reading/explaining LAD, comment generation, convention review, data extraction (alarm lists, IO usage, cross-references), generation from plain language using real tags, modification of existing networks, a proven-pattern library, and simulation-based verification.

## Out of scope

See `10-non-goals.md`. Headlines: safety logic, non-LAD PLC languages, direct hardware download, HMI runtime logic (HMI *documentation* like alarm tables is a data-extraction target, not HMI engineering).

## Success criteria for the initial scope

- Round-trip of every LAD block in a representative reference project produces a semantically identical re-import (clean compile, no diff in re-export beyond known-benign attributes).
- The IR for a typical network is readable by a controls engineer without a manual.
- Export→convert→import→compile runs end-to-end from one command.

## Related documents

`02-roadmap.md` (staged expansion — **🛑 suspended 2026-08-17**) · `03-development-plan.md` (**🛑 suspended; canonical notice**) · `04-design-philosophy.md` · `05-architecture.md` · `10-non-goals.md`

**Note on this document:** the scope and success criteria above are **unchanged by the suspension** — what is suspended is the plan for *delivering* them, not the definition of what is in scope. Nothing here has been narrowed or withdrawn.
