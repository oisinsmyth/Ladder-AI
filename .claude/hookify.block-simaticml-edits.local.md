---
name: block-simaticml-edits
enabled: true
event: file
action: block
conditions:
  - field: file_path
    operator: regex_match
    pattern: \.xml$
  - field: file_path
    operator: not_contains
    pattern: Converter
---

🚫 **Blocked: direct edit of raw SimaticML (.xml)**

CLAUDE.md hard rule 7: **edit only IR, never raw SimaticML.** SimaticML is converter
territory — this file looks like exported/generated PLC content (e.g. under `ir/`,
`simatic-ml/`, `patterns/`, or a scratch/live TIA project export), not a PC-side test
fixture.

- Edit the corresponding `.ir` file instead, then regenerate the XML via the normal
  `converter to-xml` / import pipeline.
- If the converter rejects a construct, that is a converter bug or an unsupported
  construct — report it, don't hand-patch the XML to work around it.
- If this really is PC-side tooling content that isn't a converter test fixture (this
  rule excludes anything with "Converter" in the path), say so — the pattern needs
  narrowing, not a one-off bypass.

**Carve-out watch — re-derived 2026-08-05 (audit F-50).** The exclusion keys on the literal
substring `Converter` in the path, which is a heuristic, not a boundary. Measured at that date:
115 tracked `.xml`, of which 48 fall outside the carve-out and **0 of them are files that should
be editable** (38 under `simatic-ml/` — the rule's whole purpose; 4 frozen golden answer keys,
which must not be hand-edited either; 3 sanitized SimaticML; 2 under `patterns/`; 1 generated).
All 67 permitted files are `Converter.Tests/Fixtures`, so no real PLC content is wrongly editable.
The prediction that a fixture would eventually land outside a `Converter` path has come true
structurally without doing harm. **Escalate to a real fix — anchor the carve-out on
`Converter\.Tests[\\/]Fixtures` — the first time a legitimately-editable `.xml` fixture is added
outside that directory.** Note also that `not_contains` is case-sensitive while `\.xml$` is
compiled case-insensitively, so a lowercase `converter/` path would be blocked.
