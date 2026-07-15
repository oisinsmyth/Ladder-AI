# Pattern: db-outputs (buffer-DB structure kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

The standard member layout for the `Output` buffer DB that `output-mapping` populates — the output
side of `db-inputs`, same canonical shape, same source of truth (the project owner's own
description, see `db-inputs/pattern.md`). Grounded against the real `Output` DB (DB 16, JOB9002
`station_2/JOB9002_PLC/Map IO`) for comparison and as the real working example; used directly, real
content — same basis as `db-inputs`.

**Same kind-3 (buffer-DB structure) classification as `db-inputs`** — not re-explained in full
here; see `db-inputs/pattern.md` and `docs/07-pattern-library-spec.md` §Kind 3.

## The shape (member layout)

Same shape as `db-inputs`, applied to outputs:

```
DB Output
  MEMBERS
    Test : Array[0..<N>] of Bool         -- N = number of physical points. [0] = master mapping
                                          -- on/off switch. [1..N], one per physical point, forces
                                          -- that point's buffer value during IO test/sanity check.
    SpareDQ : Array[1..<M>] of Bool      -- M = number of spare points. Dead-end sink: spare/unused
                                          -- physical points map here for neatness and continuity,
                                          -- not to a named point.
    <TagName1> : Bool
    <TagName2> : Bool
    ...                                   -- one per physical output, real function names, however
                                           -- many are needed
```

**Fixed, always in this order**: `Test` array first, `SpareDQ` array second, named points last.
This order and the array bounds above are the pattern; a new instance follows this, not the real
instance's own layout below.

## Real instance (`Output.ir`), and where it differs

`Output.ir` is the real, complete, currently-working `Output` DB (DB 16) — kept exactly as
exported. It differs from the canonical shape in one way:

- **No `SpareDQ` member at all.** The real DB has no spare-output points to sink, so the array was
  never added. `Test : Array[0..26] of Bool` otherwise matches the shape exactly — 27 slots, index 0
  the master switch, 1-26 mapping 1:1 onto the 26 named points, no unused headroom (unlike
  `db-inputs`'s own real instance).

## Examples

`Output.ir` — the complete real DB, 27 members (1 `Test` array + 26 named points; no `SpareDQ`).

## Admission status

1. Instantiated in reviewed, working logic — yes, `Output` DB 16, populated every scan by
   `output-mapping`'s own admitted rungs.
2. Round-trips losslessly — **confirmed** (`Converter.Tests/PatternExampleTests.cs`,
   `DbOutputsBlock_RoundTripsLosslessly`, `dotnet test` green).
3. A newly-drafted instance, following the canonical shape above (`Test` then named points, plus a
   `SpareDQ` array if the target station has spare outputs), compiles clean — **not started**.
   Needs the project owner's explicit go-ahead before drafting, same as every other pattern.
4. `pattern.md` complete — this document; pending review.
5. Human sign-off — pending, not yet recorded.
