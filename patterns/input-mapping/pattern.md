# Pattern: input-mapping (repeated rung-shape kind)

**Status:** proposed, pending review and sign-off. Not yet admitted to the library.

## Intent

The standard shape for mapping one physical discrete input into the `Input` buffer DB, with a
per-point IO test override — implementing `06-lad-conventions.md` C-304 ("control logic never
reads physical inputs... directly, all physical IO passes through buffer DBs"). Source: `FC
Inputs` ("Input Map", JOB9002 `station_2/JOB9002_PLC/Map IO`), 94 discrete input points across 6
networks, grounded fresh this session. Used directly, real content — nothing here needed
sanitizing: every tag is already generic (`DI1`...`DI94` are bare sequential point numbers, no more
identifying than a network number; the function names are the same vocabulary already committed in
`motor-dol`/`chained-permissive-enable`).

**Why documented as a rung shape, not factored into a call** — same reasoning as
`chained-permissive-enable`: `FC Inputs` is a Map FC, one line per physical point, meant to be
scanned/audited as a flat list (C-110's own "input mapping is the first call in OB1" framing treats
the whole FC as one auditable unit) — factoring 94 near-identical lines into 94 calls would make
that audit harder, not easier, for no real benefit (there's no meaningful "instance" to a wire
mapping the way there is for a motor).

**Correction to an earlier grounding mistake**: this pattern's `Test[]` mechanism was first
documented here as "simulation." That was wrong. Per the project owner: `Test[]` is IO testing and
a sanity check — a per-point override for wiring/commissioning verification — not `06-lad-
conventions.md` C-111's simulation mode (`DB_PLC.Simulation`, which gates the whole map-FC *call*
off and substitutes `FC_Simulation`). The two mechanisms are unrelated; nothing in `FC Inputs`
contradicts C-111 — there was no real discrepancy, only a mislabeling on first pass. Renamed
throughout to **IO test override** to stop repeating the error.

**Real data-quality issue #1, not silently replicated**: the source data has two `DiscreteInputs.Test[]`
index collisions — `Test[20]` is used by both `OverbandMagIsoFB` and (wrongly) `AirStarFCRotSen`,
both in network 2 (`examples/network-2-negated-and-spare.ir` shows both lines); `Test[21]` is used
by both `OSDrumSepIsoFB` (network 2, also in that example) and `HFLCRotSen` (network 3 — cited by
name here, not preserved as its own example; see issue #2 below for why). Confirmed as a real bug,
not a documented exception — a newly-drafted instance of this pattern must assign a genuinely
unique test index, never copy an existing one.

**Real data-quality issue #2, not documented as a shape variant**: 38 of the 94 real points (all 16
in network 6; 15 of 16 in network 3 — every point but `HFLCRotSen`; 7 of 16 in network 5) omit the
IO test override entirely — bare `COIL Input.<TagName> := AlwaysTrue AND DI<n>`, no `Test[]`
involvement at all. This was originally documented here as a legitimate "without override" variant.
Confirmed with the project owner: it is a mistake in that specific historical implementation, not a
valid form — every physical input should carry the override. Not represented in `examples/`; a
newly-drafted instance must always include it, no exceptions.

## The shape (fixed vs. varies)

```
COIL Input.<TagName> := AlwaysTrue AND NOT DiscreteInputs.Test[0] AND [NOT] DI<n>
                         OR AlwaysTrue AND DiscreteInputs.Test[<unique n>]
```

**What's genuinely fixed, on every legitimate point**: the `AlwaysTrue AND ...` structure on both
OR-branches, `NOT DiscreteInputs.Test[0]` gating the physical branch, `AlwaysTrue AND DiscreteInputs.Test[<n>]` as the
IO-test branch — mandatory, not merely typical (see issue #2 above).

**What varies, with real examples of each**:
- **Polarity** — most points are non-negated (`network-1-baseline.ir`, all 14 points); some are
  negated (`network-2-negated-and-spare.ir`: every `...IsoFB` point uses `NOT DI<n>` — isolator
  feedback reads active-low by field convention here, same negation-polarity pattern already seen
  and documented during S4's `PerimeterSafetyAlarms` pilot).
- **Named vs. spare/array-indexed tag** — most points have a real function name; unused/spare
  points use an array-indexed placeholder instead (`network-2-negated-and-spare.ir`:
  `DiscreteInputs.SpareDI[0]`).

## Examples

`examples/` holds two real network excerpts, both fully compliant (every point carries the IO test
override):

- `network-1-baseline.ir` — the clean baseline, full IO-test-override mux, no negation, no spares.
- `network-2-negated-and-spare.ir` — negated polarity (every `IsoFB` point) and a spare/array-indexed
  tag, in one real, complete network. Also the full `Test[20]` collision (both colliding lines) and
  half the `Test[21]` collision (`OSDrumSepIsoFB`) — see issue #1 above.

A third excerpt (`network-3-no-simulation-override.ir`, covering real network 3) was removed after
review: 15 of its 16 lines are instances of data-quality issue #2 above, not a legitimate variant,
and keeping it would have presented the mistake as part of the pattern.

## Admission status

1. Instantiated in reviewed, working logic — yes, `FC Inputs`, 94 real points.
2. The excerpts round-trip losslessly — **confirmed** (`Converter.Tests/PatternExampleTests.cs`,
   `NetworkOnlyExample_RoundTripsLosslessly`, both `examples/` files, `dotnet test` green).
3. A newly-drafted instance, covering a structurally distinct case, compiles clean — **not
   started**. Same discipline as `chained-permissive-enable`: needs the project owner's explicit
   go-ahead before drafting, not assumed to carry over from a different pattern's own approval.
4. `pattern.md` complete — this document; pending review.
5. Human sign-off — pending, not yet recorded.
