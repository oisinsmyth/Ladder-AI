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
    pattern: Converter.Tests
  - field: new_text
    operator: regex_match
    pattern: .
---

🚫 **Blocked: direct edit of raw SimaticML (.xml)**

CLAUDE.md hard rule 7: **edit only IR, never raw SimaticML.** SimaticML is converter
territory — this file looks like exported/generated PLC content (e.g. under `ir/`,
`simatic-ml/`, `patterns/`, or a scratch/live TIA project export), not a converter test
fixture.

- Edit the corresponding `.ir` file instead, then regenerate the XML via the normal
  `converter to-xml` / import pipeline.
- If the converter rejects a construct, that is a converter bug or an unsupported
  construct — report it, don't hand-patch the XML to work around it.
- If this really is PC-side tooling content that isn't a converter test fixture, say so —
  the pattern needs narrowing, not a one-off bypass.

**READING SimaticML IS FINE AND IS NOT WHAT THIS RULE GUARDS.** Hard rule 7 governs
*writing*. Exports are read constantly — the IR discovery workflow, `explain-plc-block`,
drift investigation, characterising an unsupported construct. If you are blocked on a
`Read`, that is a bug in this rule, not a rule to route around.

---

## Rule history — three fixes, 2026-08-12

**1. IT BLOCKED READS. Fixed.** Measured: a `Read` of `simatic-ml/reference/AlarmWords.xml`
was denied. The rule keyed only on `file_path`, which is present on reads as well as writes,
so `event: file` fired on `Read` despite the plugin docs describing that event as
"Edit, Write, MultiEdit". This was the whole complaint, and it was blocking live work.

**The fix is the `new_text` condition.** That field is empty for a `Read` and non-empty for a
write, so `regex_match: .` (at least one character) is true only on writes. Documented
operators only — no reliance on undocumented `tool_matcher` behaviour.

*Known residual, recorded rather than hidden:* a write of a **zero-byte** `.xml` is not
blocked, because an empty `new_text` is indistinguishable from an absent one with the
available operators. A degenerate case, and a far better trade than blocking every read.

**2. Carve-out anchored.** Was `not_contains: Converter` — a loose substring matching any
path containing "Converter" anywhere. Now `Converter.Tests`. The 2026-08-05 audit (F-50)
measured that all 67 legitimately-editable `.xml` files live in `Converter.Tests/Fixtures`,
so this tightens the hole without removing anything real. `src/converter/Converter/**.xml`
(non-test) is now correctly blocked where it previously was not.

**3. Case-sensitivity gap — NARROWED, NOT CLOSED.** `not_contains` is case-sensitive while
`\.xml$` is compiled case-insensitively, so a lowercase `converter.tests/` path would still
be blocked. Not fixed here: the operator set has no negated-regex, and inverting the logic
risks disabling the guard — which is the one failure this rule must never have. The real
directory is capitalised `Converter.Tests`, so the gap is currently theoretical.

**Carve-out watch (from the 2026-08-05 audit, still current).** The exclusion is a
heuristic, not a boundary. Measured at that date: 115 tracked `.xml`, of which 48 fell
outside the carve-out and **0 were files that should be editable** (38 under `simatic-ml/`
— the rule's whole purpose; 4 frozen golden answer keys; 3 sanitized SimaticML; 2 under
`patterns/`; 1 generated). **Escalate to a real fix the first time a legitimately-editable
`.xml` fixture is added outside `Converter.Tests/Fixtures`.**
