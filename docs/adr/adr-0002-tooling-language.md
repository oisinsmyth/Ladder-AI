# ADR-0002 — PC-side tooling languages

- **Status:** Accepted (revised 2026-07-11 — converter moved from Python to C#; see Revision below)
- **Date:** 2026-07-09

## Context

Openness is a .NET API. Options for driving it: native C#, or Python via pythonnet. Non-Openness tooling (IR handling, extractors, reports) has no such constraint.

## Decision

C# for everything that references `Siemens.Engineering.dll` (`openness-cli`) **and** for the SimaticML↔IR converter (`src/converter/`). Python remains available for tooling that neither touches Openness nor the IR grammar — concretely, the S5 extractors (`extract/`: alarm lists, IO usage → CSV/XLSX, reports) — for whenever that work starts. The IR text format and the CLI command-line contracts (`openness-cli`, `converter`) are the boundary between components regardless of which language sits behind either one.

## Revision (2026-07-11): converter moved from Python to C#

Originally the converter was bucketed into "pure text-side tooling" and defaulted to Python under the general split below. Revisited after `openness-cli` was actually built and the real cost of standing up a .NET toolchain on this machine became concrete evidence rather than a guess: this PC had neither the .NET SDK nor the .NET Framework 4.8 Developer Pack installed, and getting `openness-cli` building required installing both (`docs/notes/openness-quirks.md`). Adding Python 3.12 as a third runtime, on a solo-engineer setup with no CI/ops backing it, is a real recurring cost — not the "faster to build" win the original split assumed, once a .NET toolchain is already a hard requirement for `openness-cli` anyway.

The converter never touches `Siemens.Engineering.dll` — it's pure XML-in/text-out, so the original C#-for-Openness-reliability argument doesn't apply to it either way. What tipped it to C#:

- One `dotnet build && dotnet test` for the whole PC-side toolchain instead of a second one (`pytest`) for just the converter.
- `System.Xml.Linq` (XDocument + LINQ) is a strong, arguably better fit than Python's ElementTree for querying the `Parts`/`Wires` wiring graph found in `ir/SPEC.md`'s grounding work ("find every part wired to X" is a query, not just a parse).
- The converter has no `Siemens.Engineering.dll` dependency, so unlike `openness-cli` it isn't pinned to `net48` — it can target a current .NET version outright.
- Shared model types between `openness-cli` and the converter (if ever useful) are trivial within one solution family, awkward across a process/language boundary.

This is **not** a decision to rule out Python for the project. It's narrowed, not reversed: Python is still the right tool for `extract/` when S5 arrives — pandas/openpyxl for CSV/XLSX report generation is a genuinely good fit that C# doesn't match as cleanly, and that component touches neither Openness nor the IR parser/serializer where toolchain unification actually mattered here.

## Options considered

- **All-C#:** one toolchain, but slower iteration on text/reporting tasks. *(Original framing — see Revision: for the converter specifically, this cost turned out smaller than the cost of a second runtime, given `openness-cli` already forces a .NET toolchain to exist.)*
- **All-Python (pythonnet):** one language, but pythonnet adds a fragile layer exactly where reliability matters most (session handling, COM-ish object lifetimes), and Openness docs/examples are C#. Still rejected for anything touching Openness.
- **Split, converter in Python (original decision):** native API where Openness reliability matters, scripting speed for everything else. Revised — see above.
- **Split, converter in C#, Python reserved for extract/ (current decision):** two toolchains only when each is actually earning its keep — C# where Openness reliability matters *and* where a .NET toolchain already has to exist for other reasons, Python only where its ecosystem (pandas/openpyxl) is a genuine advantage over C# and nothing else forces a second runtime onto the machine anyway.

## Consequences

`openness-cli` and the converter must each expose everything AI/scripts need via their CLI contract (no library coupling between them, even though both are now C# — the process/CLI boundary stays the integration point, not shared assemblies, so either could still be replaced independently). No Python runtime is required on the engineering PC until `extract/` (S5) actually starts. Revisit again if `extract/`'s CSV/XLSX work turns out to have a comparably strong C# path (e.g. ClosedXML) by the time S5 arrives — the case for Python there hasn't been tested empirically the way the converter's C# case now has.
