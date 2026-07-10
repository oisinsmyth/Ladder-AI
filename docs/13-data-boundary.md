# 13 — Data & IP Boundary

**Status: DRAFT — needs a decision (yours, possibly your employer's) before real project data flows to any AI. Ties to risk R-08.**

## The question

Claude Code sends repo content and command output to Anthropic's API. Exported PLC projects can contain identifying names, machine know-how, network details, and NDA-covered material. What is allowed to leave the engineering PC?

## Proposed tiers

| Tier | Content | AI access |
|------|---------|-----------|
| Green | This doc suite, tooling code, IR spec, pattern library, the purpose-built reference project | Yes |
| Amber | Real project logic with identifying data (identifying names in comments, IP addresses, site names) | Only after sanitization, or with explicit per-project approval |
| Red | Safety program content; anything a site contract forbids sharing | Never — also enforced in tooling for safety content |

## Decisions needed (record as ADR-0003 when made)

1. Does workplace IT/management policy permit cloud AI on project data at all? In what form (commercial terms, data retention)?
2. Is a sanitization pass required for Amber (strip/alias restricted identifiers, IPs, site names in comments and tag names)? If so it becomes a converter feature.
3. Per-project: do any active NDAs categorically exclude their projects?

## Interim rule (until decided)

Only Green-tier content goes near Claude Code. The reference project is purpose-built and contains nothing identifying — all development through S6 can proceed on Green data alone, so this decision does not block early stages.

## Per-project approvals (Amber, pending ADR-0003)

- **2026-07-10 — "JOB9002 - Tom White Waste" (scratch copy).** Private engineering project, Amber-tier
  (identifying identifying name; not sanitized). Explicit per-project approval given by the
  project owner to use it for A-01/A-02 Openness verification spikes (`docs/notes/stage-gates.md`
  S0). No sanitization pass applied. Scoped to this verification use — not a blanket approval
  for future work; re-confirm before using this project for anything beyond the A-01/A-02 spikes.
  The project contains two linked PLC stations, `station_1/JOB9001_PLC` and `station_2/JOB9002_PLC`
  (`PLCToPLCComs`/`LSNTP_Server` between them) — confirmed by the project owner to be the same
  site (two phases/PLCs of Tom White Waste), so both are covered by this approval, not just the
  station matching the project's own name.
