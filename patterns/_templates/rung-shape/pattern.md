# Pattern: [pattern-name] (repeated rung-shape kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

[What this shape computes, in one or two sentences. Cite the specific `06-lad-conventions.md`
rule(s) it implements (e.g. C-114/C-115/C-116 for an enable chain). Name the source FC and where it
was grounded from: `[FcName]` ([project], `[device/path]`), [N] networks, each one [equipment/unit],
grounded fresh on [date].]

**Why documented as a rung shape, not factored into a call** — cite `06-lad-conventions.md`
C-109/C-110 (or whichever rule keeps this inline) and say specifically why factoring it out would
fight that convention here, not just assert it generically.

## The shape (fixed vs. varies)

[Skeleton with placeholder names, matching the real instruction keywords used, e.g.:]

```
COIL <Inst>.[Member] := [fixed or placeholder expression]
...
CALL <EquipmentFB>(<Inst>, EN := TRUE)
```

**Real annotated example** (`examples/[filename].ir`, network [N], "[real title]"):

```
[paste the real network's own readable IR text verbatim — this must match the committed examples/
file exactly, not a paraphrase]
```

**What's genuinely fixed**: [list].

**What varies, with real examples of each** — not hypothetical variation, cite the specific real
network(s) in `examples/` that actually show each case:
- **[Variation 1]** — [network X shows this, network Y shows the alternative].

## Examples

`examples/` holds [N] real, sanitized excerpts as standalone network-only `.ir` files
(`IrSerializer.SerializeNetworkOnly`'s own format — no `BLOCK`/`SIDECAR` wrapper), covering the
range of real variation above, not just the single cleanest case:

- `[filename].ir` — [what this one specifically demonstrates].

## Admission status

1. Instantiated in reviewed, working logic — [yes/no + evidence, how many real instances].
2. The excerpts round-trip losslessly — [confirmed how — extend
   `Converter.Tests/PatternExampleTests.cs` with new cases rather than a separate mechanism — or
   pending].
3. **A newly-drafted instance, covering a structurally distinct real case not already represented,
   compiles clean** — not the naive "compiles clean on a copy-paste-rename" test, which proves
   nothing about whether `pattern.md` itself is sufficient. **Requires the project owner's explicit
   go-ahead before drafting anything** if this would be the first AI-authored new logic in this
   project — check `docs/notes/stage-gates.md` before assuming that's already been crossed.
4. `pattern.md` complete — [pending review / reviewed by whom, when].
5. Human sign-off — [pending, or name + date].
