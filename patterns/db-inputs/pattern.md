# Pattern: db-inputs (buffer-DB structure kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

The standard member layout for the `Input` buffer DB that `input-mapping` populates —
implementing `06-lad-conventions.md` C-304's buffer-DB requirement from the data side. Defined by
the project owner directly (below) — that description is the canonical shape. Grounded against the
real `Input` DB (DB 15, JOB9002 `station_2/JOB9002_PLC/Map IO`) for comparison and as the real working
example; used directly, real content — same genericization-waiver basis as
`input-mapping`/`output-mapping` (`docs/13-data-boundary.md`, 2026-07-15 entry).

**Kind 3 (buffer-DB structure), not kind 1 or kind 2**: this isn't callable (no `CALL` site, unlike
`motor-dol`) and it isn't ladder logic at all (no rungs, unlike `chained-permissive-enable`/
`input-mapping`/`output-mapping`) — it's a single, per-station, singleton data structure. Reuse for
a new station/project means drafting a *new* DB with this member layout, not calling this one or
copying its exact members. `docs/07-pattern-library-spec.md` §Kind 3 has the general definition;
this is its first instance.

## The shape (member layout)

```
DB Input
  MEMBERS
    Test : Array[0..<N>] of Bool         -- N = number of physical points. [0] = master mapping
                                          -- on/off switch. [1..N], one per physical point, forces
                                          -- that point's buffer value during IO test/sanity check.
    SpareDI : Array[1..<M>] of Bool      -- M = number of spare points. Dead-end sink: spare/unused
                                          -- physical points map here for neatness and continuity,
                                          -- not to a named point.
    <TagName1> : Bool
    <TagName2> : Bool
    ...                                   -- one per physical input, real function names, however
                                           -- many are needed
```

**Fixed, always in this order**: `Test` array first, `SpareDI` array second, named points last —
however many of each a given station needs. This order and the array bounds above are the pattern;
a new instance follows this, not the real instance's own layout below.

## Real instance (`Input.ir`), and where it differs

`Input.ir` is the real, complete, currently-working `Input` DB (DB 15) — kept exactly as exported,
not reshaped to match the shape above (never invent or rearrange real project structure). It
differs from the canonical shape in three ways. These are quirks of this one real historical
instance, kept as found — **not** part of the pattern, and not something a new instance should
copy:

- `Test : Array[0..75] of Bool` — 76 slots for 94 named points, not the 1:1 `[0..94]` the shape
  calls for. The highest index actually wired to a rung in `FC Inputs` is `Test[61]`; 62-75 are
  unused headroom that was never consumed.
- `SpareDI : Array[0..4] of Bool` — zero-based, not the 1-based `[1..M]` the shape calls for.
- `SpareDI` is declared **last** in the member list, after all 94 named points, not second (right
  after `Test`).

These line up with `input-mapping/pattern.md`'s own already-documented issue #2 (this DB's history
has known gaps between intended and actual, not resolved here).

## Examples

`Input.ir` — the complete real DB, 96 members (1 `Test` array + 94 named points + 1 `SpareDI`
array, in the real instance's own order — see above, not the canonical order).

## Admission status

1. Instantiated in reviewed, working logic — yes, `Input` DB 15, populated every scan by
   `input-mapping`'s own admitted rungs.
2. Round-trips losslessly — **confirmed** (`Converter.Tests/PatternExampleTests.cs`,
   `DbInputsBlock_RoundTripsLosslessly`, `DbIrParser.ParseDb`/`DbIrSerializer.Serialize`,
   `dotnet test` green).
3. A newly-drafted instance, following the canonical shape above (`Test`, `SpareDI`, named points,
   in that order, with matching array bounds) rather than the real instance's own order/sizing
   quirks, compiles clean — **not started**. Natural candidate: `GenProject1`'s own IO-assignment
   work once that's underway. Needs the project owner's explicit go-ahead before drafting, same as
   every other pattern.
4. `pattern.md` complete — this document; pending review.
5. Human sign-off — pending, not yet recorded.
