# Extractors

**Not built yet — a placeholder for S5.** This directory holds only this README; nothing here exists
in code, and no roadmap stage has opened that would create it (`docs/02-roadmap.md`,
`docs/notes/stage-gates.md`). Everything below is the intended shape, stated in future tense on
purpose.

Python: IR → structured data (CSV/XLSX). To be built in S5.

Targets: alarm lists (feeds HMI alarm tables per C-501/C-505/C-506), IO usage (feeds IO checklists), cross-references (verified against TIA's own xref data).

Tests will run as `pytest tests/` once the extractors exist — the project's current test command is
`dotnet test` (PC-side C# only). Output templates will live alongside the scripts.
