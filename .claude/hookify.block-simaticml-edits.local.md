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
