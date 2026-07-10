# ADR-0002 — PC-side tooling languages

- **Status:** Accepted
- **Date:** 2026-07-09

## Context

Openness is a .NET API. Options for driving it: native C#, or Python via pythonnet. Non-Openness tooling (IR handling, extractors, reports) has no such constraint.

## Decision

C# for everything that references `Siemens.Engineering.dll` (openness-cli). Python permitted for pure text-side tooling. The IR and the openness-cli command-line contract are the boundary between the two.

## Options considered

- **All-C#:** one toolchain, but slower iteration on text/reporting tasks.
- **All-Python (pythonnet):** one language, but pythonnet adds a fragile layer exactly where reliability matters most (session handling, COM-ish object lifetimes), and Openness docs/examples are C#.
- **Split (chosen):** native API where reliability matters, scripting speed where it doesn't. Cost: two toolchains — acceptable because the boundary (CLI + IR files) is clean.

## Consequences

openness-cli must expose everything AI/scripts need via CLI (no library coupling). Revisit if the CLI surface starts sprouting dozens of niche flags.
