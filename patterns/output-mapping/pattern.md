# Pattern: output-mapping (repeated rung-shape kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

The standard shape for mapping the `Output` buffer DB out to one physical discrete output, with a
per-point IO test override — implementing `06-lad-conventions.md` C-304, the output side of the
same buffer-DB requirement `input-mapping` implements for inputs. Source: `FC Outputs` ("Output
Map", JOB9002 `station_2/JOB9002_PLC/Map IO`), 26 discrete output points across 2 networks — the
complete FC, grounded fresh this session. Used directly, real content — every tag is already
generic (`DQ1`...`DQ26` are bare sequential point numbers; the function names reuse the same
vocabulary already committed elsewhere).

**Why documented as a rung shape, not factored into a call** — same reasoning as `input-mapping`
and `chained-permissive-enable`: one flat, auditable FC (C-110), not a set of equipment instances.

**Same correction as `input-mapping`'s own pattern.md applies here too**: this FC's `Test[]` is IO
testing and a sanity check, not simulation — a different, unrelated mechanism from `06-lad-
conventions.md` C-111's actual simulation mode. There is no discrepancy with C-111; an earlier pass
here mislabeled the mechanism as "simulation," which has been corrected. Not re-explained in full;
see `input-mapping/pattern.md`.

**Genuinely simpler than `input-mapping`, confirmed complete, not a partial sample**: all 26 real
points (both networks — the entire FC) use the identical full-mux, non-negated shape, always with
the IO test override present. No negated polarity, no spare/array-indexed tag, and — unlike
`input-mapping` — no instance of the missing-override bug either. Stated as a confirmed finding over
the whole FC, not an extrapolation from a sample.

**Spare/array-indexed tag, borrowed from `input-mapping`**: no real spare output point exists in
the grounded content, but the concept is a proven site convention (`input-mapping`'s real
`DiscreteInputs.SpareDI[0]`/`SpareDI[3]`), and per the project owner it carries over to outputs. Documented
here as an expected variant for when a real spare-output point is grounded or needed — not
fabricated as an example; no `Output.SpareDQ[...]`-style tag appears anywhere in this pattern's
`examples/` unless and until it's confirmed to actually exist in the real project (`CLAUDE.md`:
never invent tags).

## The shape (fixed vs. varies)

```
COIL DQ<n> := AlwaysTrue AND Output.<TagName> AND NOT DiscreteOutputs.Test[0]
              OR AlwaysTrue AND DiscreteOutputs.Test[<unique n>]
```

**What's genuinely fixed, confirmed across the entire real FC**: the entire shape — every one of
the 26 real points follows this exact structure, no observed variation in polarity or form, no
observed omission of the IO test override.

**What's expected to vary, not yet observed in real content**: a spare/array-indexed tag, by
analogy with `input-mapping`'s own proven `SpareDI[...]` convention (see above) — plausible for
outputs too, not yet grounded here.

## Examples

`examples/network-1-baseline.ir` — all 10 points from the FC's first network. Network 2 (the
remaining 16 points) shows no new variation over network 1 and isn't separately included.

## Admission status

1. Instantiated in reviewed, working logic — yes, `FC Outputs`, 26 real points, the complete FC
   (10 shown in `examples/`).
2. The excerpt round-trips losslessly — **confirmed** (`Converter.Tests/PatternExampleTests.cs`,
   `NetworkOnlyExample_RoundTripsLosslessly`, `dotnet test` green).
3. A newly-drafted instance, covering a structurally distinct case, compiles clean — **not
   started**. Since no real shape variation exists anywhere in the (now fully-grounded) real FC, a
   structurally distinct instance would have to use the borrowed spare-tag variant above — but only
   once a real spare-output tag is confirmed to exist (not fabricated). Needs the project owner's
   explicit go-ahead before drafting, same as every other pattern.
4. `pattern.md` complete — this document; pending review.
5. Human sign-off — pending, not yet recorded.
