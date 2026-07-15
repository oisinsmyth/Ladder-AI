# Pattern: chained-permissive-enable (repeated rung-shape kind)

**Status: ADMITTED**, with one criterion accepted on partial evidence rather than fully met —
Oisín, 2026-07-15: accepted criterion 3's current evidence (drafted-with-prior-knowledge, not
blind — see "The newly-drafted instance, honestly accounted" below) and chose to move on rather
than chase a genuinely blind instance now. That gap stays recorded, not erased by admission.

## Intent

The standard shape for computing one piece of equipment's `AutoStartSignal`/`Shutdown` pair inside a
plant-wide sequencing FC, implementing the chained-permissive enable rule (`06-lad-conventions.md`
C-114/C-115/C-116): permission flows in one consistent direction along the material-flow chain — an
equipment instance enables only once the instance upstream of it has proven itself running (its own
`UPSEnable`), never the other way round. Source: `PlantAutoControl` (JOB9002, `station_2/JOB9002_PLC/Control`),
20 networks, each one equipment instance — grounded fresh this session; real, already-compiling
sanitized copy available as `PlantAutoControl` in `SampleProject`.

**Why this is documented as a rung shape, not factored into a call** (`06-lad-conventions.md`
C-109/C-110): `PlantAutoControl`/`PlantAutoControl` is the area-Main FC — it's meant to read top to
bottom like a table of contents, one network per equipment. Breaking each network out into its own
callable FC would hide the sequencing structure the convention deliberately keeps visible in one
place.

## The shape (fixed vs. varies)

Every instance follows this skeleton (real example below is `PlantAutoControl` network 8,
"Overband Magnet Automatic Control", calling `MotorStarterInst8`):

```
COIL <Inst>.IO.RunningFB    := <raw running feedback, see variation below>
COIL <Inst>.IO.SystemHealthy := TRUE                                  [usually fixed]
COIL <Inst>.IO.InhibitMotor  := <raw isolator/inhibit input>
COIL <Inst>.IO.PreStartDone  := PlantControl.PreStartComplete         [fixed]
COIL <Inst>.IO.AutoStartSignal := (<own-readiness>) AND <enable-source>
COIL <Inst>.IO.Shutdown       := NOT (<own-readiness>) AND NOT <Inst>.IO.HandIntervention
                                  AND PlantControl.Status = -1         [fixed shape]
COIL <Inst>.IO.FaultReset     := HMIControlSignals.SystemReset        [fixed]
COIL DiscreteOutputs.<X>Start := <Inst>.IO.Run                        [optional]
CALL <EquipmentFB>(<Inst>, EN := TRUE)
```

**Real annotated example** (network 8):

```
NETWORK 8 "Overband Magnet Automatic Control"
  COIL MotorStarterInst8.IO.RunningFB := DiscreteInputs.OverbandMagRunning
  COIL MotorStarterInst8.IO.SystemHealthy := TRUE
  COIL MotorStarterInst8.IO.InhibitMotor := DiscreteInputs.OverbandMagIsoFB
  COIL MotorStarterInst8.IO.PreStartDone := PlantControl.PreStartComplete
  COIL MotorStarterInst8.IO.AutoStartSignal := (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT MotorVSDInst1.IO.ShutdownComplete) AND MotorStarterInst3.IO.UPSEnable
  COIL MotorStarterInst8.IO.Shutdown := NOT (PlantControl.Status >= 1 OR PlantControl.Status = -1 AND NOT MotorVSDInst1.IO.ShutdownComplete) AND NOT MotorStarterInst8.IO.HandIntervention AND PlantControl.Status = -1
  COIL MotorStarterInst8.IO.FaultReset := HMIControlSignals.SystemReset
  COIL DiscreteOutputs.OverbandMagStart := MotorStarterInst8.IO.Run
  CALL MotorStarter(MotorStarterInst8, EN := TRUE)
```

**What's genuinely fixed**: `SystemHealthy := TRUE` (unless this equipment has a real health
check), `PreStartDone`, the `Shutdown` formula's overall shape (negated readiness, gated by hand
intervention and plant-stopping status), `FaultReset`.

**What varies, with real examples of each**:
- **`RunningFB` complexity** — network 8's is a single raw input; network 7 ("Metal Collection
  Conveyor") is `(RawRunning AND RotationSensor) OR (RawRunning AND SensorBypass)` — equipment with
  a rotation sensor and an HMI bypass needs the richer form.
- **The enable source in `AutoStartSignal`** — usually the upstream equipment's own `UPSEnable`
  (network 8 uses `MotorStarterInst3.IO.UPSEnable` — the Metal Collection Conveyor immediately
  upstream in material flow). Sometimes it's a plant-wide manual enable instead: network 7 uses
  `PlantControl.MagEnable`, not another equipment's `UPSEnable` — equipment gated by an operator
  flag rather than sitting mid-chain uses this form instead.
- **`ShutdownComplete` cross-references are not the enable chain** — network 8's own readiness term
  references `MotorVSDInst1.IO.ShutdownComplete` (a *different* instance than the one feeding its
  `UPSEnable`). This is legitimate and not a circular-enable violation: C-114 explicitly exempts
  "process/safety interlocks" from the one-direction rule ("downstream-blocked stopping an upstream
  belt is an interlock, not an enable") — a `ShutdownComplete` reference is exactly that kind of
  interlock, not part of the enable chain proper, and may reference either direction.
- **The optional pre-start latch** — network 2 ("Link Conveyor") additionally has an `SCOIL`/`RCOIL`
  pair on `PlantControl.AutoPreStart` with an edge-detected condition; networks 7/8 don't. Present
  only for equipment that needs to kick off a plant-wide pre-start sequence itself, not every
  instance.
- **Final output coil** — present when the equipment has a direct physical start output
  (`DiscreteOutputs.<X>Start`); absent for equipment types without one (e.g. network 5, `AirStarSystem`,
  has no such line).

## Examples

`examples/` holds all three real, sanitized excerpts referenced above as standalone network-only
`.ir` files (no `BLOCK`/`SIDECAR` wrapper — `IrSerializer.SerializeNetworkOnly`'s own format),
covering the range of real variation, not just the single cleanest case:

- `network-8-overband-magnet.ir` — the clean baseline (single raw `RunningFB`, upstream-equipment
  `UPSEnable` as the enable source, no optional extras).
- `network-7-metal-collection-conveyor.ir` — richer `RunningFB` (rotation sensor + bypass), and a
  plant-wide flag (`PlantControl.MagEnable`) as the enable source instead of an upstream `UPSEnable`.
- `network-2-link-conveyor.ir` — the optional pre-start-latch extension (`SCOIL`/`RCOIL` on
  `PlantControl.AutoPreStart`).
- `network-12-drum-separator-chainhead.ir` — the newly-drafted instance for admission criterion 3
  (see below) — a genuine chain-head: `AutoStartSignal` references no other equipment's `UPSEnable`/
  `ShutdownComplete` at all, gated purely by `PlantControl.Status`/`PlantControl.DrumsEnable`.

## The newly-drafted instance, honestly accounted

`network-12-drum-separator-chainhead.ir` was composed for `MotorStarterInst5` ("Drum Separator"),
following this pattern's own documented skeleton. **Labeled explicitly, per the project owner's own
call: this was composed with prior knowledge of the real answer, not blind** — `PlantAutoControl`'s
own network 12 had already been read in full while building this documentation, so drafting "fresh"
from it isn't a test of independent composition the way the admission criterion originally
envisioned.

A genuinely blind attempt was tried first, using JOB9002's *other*, entirely separate `PlantAutoControl`
FC on station_1 (`JOB9001_PLC`) — untouched all session. Reading it one line at a time to avoid the
enable-chain formula itself surfaced two real findings before either candidate panned out: one
partial exposure (a first candidate's `AutoStartSignal` line was reached before the read could stop
in time), and a second candidate ("Transfer Conveyor") turning out to be VSD-driven with an
edge-detected `RCOIL`-based `FaultReset` — a real structural variant this pattern doesn't document
at all yet, so it wouldn't have been a fair test of the *current* documentation regardless. Both
findings are worth keeping in mind for a future, genuinely blind pass, or for extending this pattern
to cover station_1's own conventions — not chased further this round.

Given that, this artifact's actual evidentiary value is narrower than originally intended: it shows
the documented shape *accurately describes* the real, working structure (every fixed element in
"The shape" section above matches; the chain-head variant is exactly what "what varies" predicted),
which is real signal, but it is **not** proof that `pattern.md` alone is sufficient for independent
drafting — that remains open, and should be the bar for the *next* new instance added to this
pattern, using content nobody involved has already read.

## Admission status

1. Instantiated in reviewed, working logic — yes, 18+ real instances across `PlantAutoControl`.
2. The excerpts round-trip losslessly — **confirmed** (`Converter.Tests/PatternExampleTests.cs`,
   `NetworkOnlyExample_RoundTripsLosslessly`, all four `examples/` files, `dotnet test` green,
   419/419).
3. **A newly-drafted instance... compiles clean** — **partially satisfied, honestly**: the project
   owner gave explicit go-ahead to draft (this project's first act of AI-authored new control
   logic), and `network-12-drum-separator-chainhead.ir` round-trips and — being byte-identical to
   `PlantAutoControl`'s own real network 12 — compiles clean by the same evidence already cited for
   that whole-project proof. But per "The newly-drafted instance, honestly accounted" above, this
   was drafted *with* prior knowledge of the real answer, explicitly not blind — it demonstrates the
   documentation accurately describes the real structure, not that the documentation alone is
   sufficient for independent drafting. **A genuinely blind instance is still owed** before this
   criterion should be considered fully met in the spirit the plan intended — accepted as
   outstanding rather than resolved, Oisín, 2026-07-15 ("accept the current evidence for now, move
   on").
4. `pattern.md` complete — reviewed and accepted, Oisín, 2026-07-15.
5. Human sign-off — **Oisín, 2026-07-15**, on the basis above (criterion 3 accepted with a recorded
   gap, not fully met).
