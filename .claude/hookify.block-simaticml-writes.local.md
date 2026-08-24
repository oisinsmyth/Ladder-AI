---
name: block-simaticml-writes
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
  - field: content
    operator: regex_match
    pattern: .
---

🚫 **Blocked: writing a whole raw SimaticML (.xml) file**

CLAUDE.md hard rule 7: **edit only IR, never raw SimaticML.** This is the `Write`-side
companion to `block-simaticml-edits`, which covers `Edit`/`MultiEdit`. Same rule, same
reasons — see that file for the full guidance and the carve-out history.

- Edit the corresponding `.ir` file instead, then regenerate via `converter to-xml`.
- If the converter rejects a construct, that is a converter bug or an unsupported
  construct — report it, don't hand-author the XML to work around it.

**READING SimaticML IS FINE.** Hard rule 7 governs *writing*. If you are blocked on a
`Read`, that is a bug in the rule, not a rule to route around.

---

## Why this file exists — measured 2026-08-25

**`Edit` of a `.xml` was blocked and `Write` of the same `.xml` was not.** Found by an
agent running an ADR-0011 confirm loop, which reported it rather than exploiting it, and
then confirmed directly:

| tool | path | rules loaded | result |
|---|---|---|---|
| `Write` | `scratch/guard-probe/hookcheck.xml` | 4, cwd = repo root | **allowed** |
| `Edit` | the same file, immediately after | 4, cwd = repo root | **denied** |

**The cause is the field name, not the path or the cwd.** `block-simaticml-edits` gates on
`field: new_text`, which is the payload field for `Edit`/`MultiEdit`. A `Write` carries its
payload in `content`, so `new_text` is absent, the condition fails, and — because conditions
are ANDed — the whole rule declines to fire. The `new_text` condition was added deliberately
in 2026-08-12 to stop the rule blocking `Read`s, and that fix was correct; this is its
unnoticed cost.

**An alternative explanation was ruled out, not assumed away.** The sibling safety rule
records that discovery globs `.claude/hookify.*.local.md` *relative to the current working
directory*, so a hook process whose CWD is not the repo root loads no rules at all — and the
agent had been working inside `scratch/`. That would have been the more serious fault. The
table above was therefore run deliberately from the repo root with all four rule files
present, which separates the two: the `Write` was allowed **while the guard was fully
loaded**.

**This rule is a separate file on purpose.** The operator set has no OR, so the condition
could not simply be added to the existing rule without weakening it. An additive sibling can
only ever block more, never less — and the one failure these rules must never have is
silently not firing.

## 🔴 The same gap almost certainly applies to the safety guard

`hookify.block-safety-fblock-content.local.md` uses a bare `pattern:`, which its own scope
note records is expanded to `field: new_text`. **By the identical mechanism, a whole-file
`Write` of F-block content would not be blocked** — and that is hard rule 2, not hard rule 7.

**Stated as a structural inference, NOT as a measurement.** It was not tested, and it must
not be: testing it means writing F-block content, which is precisely what hard rule 2
forbids. The inference is strong — same event, same field, same tool asymmetry, measured on
an identical rule shape — but it is an inference, and the fix belongs to the owner because
altering a safety control is not a call the pipeline makes for itself.

Note the read-side half of hard rule 2 is unaffected and still holds: `SafetyClassifier`
refuses to export or open safety blocks at all, and fails loud on an unrecognised language.

## Known residuals, recorded rather than hidden

- **A zero-byte `.xml` write is still not blocked**, for the same reason the sibling rule
  documents: an empty payload is indistinguishable from an absent one with the available
  operators.
- **A file written by a subprocess is not blocked and cannot be**, because it is not a file
  *tool* call at all. The confirm-loop agent that found this gap generated its candidates
  with a Python script after the `Edit` was denied. **These rules are a guard against
  drifting into hand-patching, not a sandbox against a determined process.** Anyone reading
  a green here should know which of the two they have.
- **`not_contains` is case-sensitive** while `\.xml$` compiles case-insensitively — the same
  narrowed-not-closed gap the sibling rule records, and theoretical for the same reason.

## Open for the owner — NOT decided here

An ADR-0011 confirm loop is the one sanctioned reason to hand-author SimaticML: ADR-0010
makes an unsupported construct a scope item, and the converter's own refusal message says to
*"widen the converter against a real TIA import/compile first."* **These rules have no
carve-out for that**, so the sanctioned workflow is blocked by the same guard that blocks the
abuse it targets. A narrow exclusion for a designated probe directory under `scratch/` is
defensible — but it *loosens* a control, so it is the owner's call, not the pipeline's, and
it is not taken here.
