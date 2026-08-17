# Wave 3 — results

**Executed 2026-08-17.** Plan: [`PLAN.md`](PLAN.md) §3. Everything below was run, not designed.

---

## Delivered

| | what | evidence |
|---|---|---|
| **T7 `hmi-cli compare`** | file-vs-file SimaticML comparison, the `converter compare` idea re-aimed at screens | 13 objects, 0 differences on the real round trip; same-file-twice refused with exit 2 |
| **Coherence gate** | validates the emitted document **before** it can reach TIA | 8 tests, including the exact Portal-crashing document reproduced |
| **Test project** | `src/hmi-cli/HmiCli.Tests` | **23 passing** |
| **`hmi-designer` agent** | `.claude/agents/hmi-designer.md` | frontmatter parses, 456 chars retained, quoted |
| **Denominator honesty** | `docs/17` rule count reconciled against the checker | 26 claimed → **14 checked**, three-way split |

---

## The coherence gate — a gate nobody has seen fire is not a gate

Wave 2 ended with a lesson rather than a tool: **TIA's import is excellent at catching malformed
documents and useless at catching incoherent ones.** A schema-valid, enum-valid document describing
an object whose parts contradict each other does not get rejected — it **crashes the Portal
process**, reporting only `Access to a disposed object of type 'Siemens.Engineering.Project'`.

`hmi-cli emit` now refuses to write such a document. The tests are built around **reproducing the
exact document that crashed Portal**:

```
Left=40 Top=400 Width=400 Height=1   StartLeft=0 StartTop=0 EndLeft=400 EndTop=1
```

with a positive control (the same line written correctly passes) and a reversed-endpoint case (a
line drawn right-to-left is still coherent). Also covered: out-of-bounds, negative size, malformed
XML, and **a document with no items at all**, which is reported rather than passed.

`emit` now prints `COHERENCE: clean over 13 item(s)` on success — the denominator, on every run.

---

## 🔴 Two documents were wrong, and a test found both

### The sizing table was wrong in six cells

`sizing-standard.md` was computed **by hand with rounding**; the checker uses **`Ceiling`**. For a
*minimum* threshold, ceiling is the only correct choice — **50 px at 5.59 px/mm is 8.95 mm**, which
is below a 9 mm floor. Rounding down produces a threshold that does not enforce the rule it names.

Found by a unit test asserting the tool's arithmetic, not by re-reading the table.

**The fix is structural, not numeric:** `hmi-cli panels --bands` now generates the table, and the
document is a transcript of it. A table a human retypes is a table that drifts from the checker
enforcing it.

### The conventions denominator was inflated by nearly half

`docs/17` claimed **26 mechanized**. Reconciled programmatically against every
`new Finding("H-nnn", …)` in `src/hmi-cli/`:

| category | n |
|---|---|
| **CHECKED** — the tool emits a finding citing this ID | **14** |
| STRUCTURALLY ENFORCED — impossible to violate, so no finding exists | 3 |
| SPECIFIED, NOT YET CHECKED | 9 |
| ADVISORY | 5 |

I had written *mechanized* meaning *can be*; the only honest reading is *is*. **That is the
`ReviewRunner.AllRuleIds` trap, walked into inside the very file that warns about it** — and it is
the second counting error in that document, after the rule total itself.

Worth stating plainly: **both of these were found by machinery, not by reading.** Re-reading a table
does not catch a table that is internally consistent and externally wrong.

---

## The agent

`.claude/agents/hmi-designer.md` encodes the measured method — house rules, render, **both** checks
every time, iterate, hand back evidence — and the prohibitions, including *do not invoke
`artifact-design`*, *do not review your own output*, and *do not re-import a screen you did not
author*.

The frontmatter was validated by parsing it, because **both failure modes are silent**: an unquoted
`": "` de-registers the agent entirely (this is how `lad-coder` once vanished), and an unquoted
`" #"` truncates the description while everything still appears to work. Result: parses, `name` and
`tools` correct, **456 characters retained**.

⚠️ `/doctor` is the only surface that reports these in the harness itself, and it has not been run —
the parse check above is a substitute, not a replacement.

---

## State of the toolchain

```
hmi-cli  flatten · lint · style-check · check · emit · compare · panels [--bands]
         23 tests passing
openness-cli  export --screen · import --screen · hmi-clone-screen · hmi --inspect · hmi-compile
```

Proven end to end on a real screen: **check (0 errors) → emit (13 items, coherence clean) → import
(4.8 s) → compile (0 errors) → export → compare (13 objects, 0 differences)**.

## Still open

1. **T8 preflight** — tag existence, faceplate existence, duplicate names. Not built.
2. **T9 unwired-check** — not built, and lower value on Classic than it was on Unified.
3. **The 9 specified-but-unchecked rules**, notably **H-107** (red STOP): H-403's size half is
   checked, H-107's colour half is not, and together they are the physical-safety rule.
4. **The regeneration guard** the placeholder workflow depends on — stamp a content hash, refuse to
   regenerate a hand-finished screen. Without it the placeholder quietly eats manual work.
5. **`/doctor` has not been run** against the new agent.
6. **The agent has never been dispatched.** It is written and unproven.
7. **Scratch project** holds `ZZ_Minimal`, `ZZ_TText`, `ZZ_TButton`, `ZZ_DemoHome`, `Screen_3`,
   `Screen_4`, and a `Screen_1` master copy.
