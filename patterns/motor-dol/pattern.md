# Pattern: motor-dol (equipment-instance kind)

**Status: ADMITTED.** Reviewed and signed off — Oisín, 2026-07-15. All 5 admission criteria met.

## Intent

A complete, reusable FB for one direct-online-started (DOL) motor: arbitrates hand/auto start
commands, enforces interlocks and health checks, detects fail-to-run/fail-to-stop faults, sequences
a delayed shutdown, totalises operating hours, reports an HMI status code, and packs its own fault
bits into an alarm word. This is the whole of what "a motor" means at this site (`06-lad-conventions.md`
C-106: repeated equipment gets one standard FB + UDT interface, called once per instance) — not a
narrowed "start/stop only" slice. Source: `MotorDOL` (JOB9002, `station_2/JOB9002_PLC/Motors`), grounded
fresh from a live export this session, reviewed against 8 real instantiations already compiled clean
in `SampleProject` (as `MotorStarter`/`MotorStarterInst1..9`, sanitized).

## When to use

- Any DOL-started motor: conveyors, fans, agitators, pumps — anything started/stopped directly across
  the line, no speed control.

## When *not* to use

- **VSD-driven motors** — a separate FB already exists at this site (`MotorVSDSystem`; real, already
  proven in `SampleProject` as `MotorVSDSystem`) with its own rotation-sensing and drive-comms
  concerns this pattern doesn't cover.
- **Forward/reverse motors** — also a separate FB (`MotorFwdRevSystem`), not this one.
- Equipment needing genuinely different fault/shutdown semantics (e.g. no meaningful "hours run"
  concept) should still start from this shape, but expect real changes, not just re-tagging.

## Interface

Everything a caller reads or writes lives on the block's own `IO` struct (typed `TypeDOL` in the
real project; `MotorIOSet` in the sanitized/committed form) — confirmed against every real call site
in `PlantAutoControl`, not assumed from the interface declaration alone. Internal state (edge-memory
arrays, every timer instance, `PreStartMemory`, `HandPosEdge`/`HandNegEdge`) is never touched from
outside and needs no caller wiring.

**Caller writes (real-world inputs):**

| Member | Type | Meaning |
|---|---|---|
| `InHand` | Bool | Hand/auto selector position (from HMI or a physical selector) |
| `AutoStartSignal` | Bool | This instance's own computed run-permission (see `chained-permissive-enable` — this is the pattern this member's own value comes from) |
| `HandStartSignal` | Bool | Momentary hand start command, edge-latched by the block itself |
| `RunningFB` | Bool | Raw running feedback (rotation sensor, contactor aux, etc.) |
| `FaultFB` | Bool | Raw external fault feedback, if the equipment has one beyond fail-to-run/fail-to-stop |
| `SystemHealthy` | Bool | Upstream health/permissive gate |
| `InhibitMotor` | Bool | Hard inhibit (isolator open, E-Stop zone, etc.) |
| `FaultReset` | Bool | Momentary reset command |
| `HandIntervention` | Bool | Set/reset by the block itself on hand button press/release — **caller does not write this directly**, listed here because it's read back by callers computing `Shutdown` for *other* equipment in the chain |
| `FTTime`, `EnableUPSTime`, `ShutdownTime` | Real | Commissioning-set timing parameters (seconds, HMI-editable), converted internally to milliseconds |

**Caller reads (this instance's own outputs):**

| Member | Type | Meaning |
|---|---|---|
| `Run` | Bool | Commanded run state — drives the physical output |
| `UPSEnable` | Bool | "This equipment has been running long enough" — feeds the *next* equipment's own `AutoStartSignal` in the enable chain |
| `ShutdownComplete` | Bool | Feeds neighbouring equipment's own shutdown-sequencing logic |
| `FTR`, `FTS`, `FaultActive` | Bool | Fail-to-run / fail-to-stop / aggregate fault |
| `HrsRun` | UDInt | Running-hours totaliser |
| `Telemetry` | Int | HMI status code: `0` stopped, `1` starting, `2` running, `3` running+upstream-enabled, `-1` fault |
| `Alarm` | Word | `%X0`=FTR, `%X1`=FTS, `%X2`=FaultFB |

## Behaviour, by network

1. **Start/stop** — `TryRunMotor` = auto-or-hand-selected start command, gated by inhibit/fault/health; `Run` latches while `TryRunMotor` and pre-start conditions hold, drops on `StopMotor`/`Shutdown`.
2. **Hand intervention** — `HandIntervention`/`HandStartSignal` are Set/Reset latches, explicitly requiring the HMI to *set* them on button press and the block to *reset* them (real source comment: "Must Have Set Bit... On Hand Control Buttons Press/Release On HMI" — a real HMI-wiring contract, not just internal logic).
3. **Start-signal debounce** — a 100ms constructed `TON` (`GeneralDelayTimer`) reasserts `HandStartSignal` after an inhibit/fault clears.
4. **Hand-selector edge detection** — rising/falling edge on `InHand`, built the constructed way (compare against a stored previous-scan bit, C-404), not the built-in edge instruction.
5. **Pre-start hold** — `PreStartMemory` latches once pre-start is done, holds until fault/stop/a hand-selector edge.
6. **HMI time scaling** — three `MUL`+`CONVERT` pairs turn the three Real (seconds) timing parameters into millisecond `DInt`s the timers actually use.
7. **Shutdown sequencing** — `StopMotor` Set/Reset around a shutdown timer; `ShutdownComplete` also considers a stuck-in-hand-without-start case.
8–9. **Fail-to-run / fail-to-stop** — each a `TON` comparing commanded state against feedback for `FTTimeMS`, latched until `FaultReset`.
10. **Fault aggregation** — `FaultActive` is FTR OR FTS OR FaultFB, latched, cleared by `FaultReset`.
11. **Upstream enable** — `UPSEnable` delays by `EnableUPSTimeMS` after `Run AND RunningFB` — the actual enable-chain link consumed by the next equipment.
12. **Hours totaliser** — **known deviation, not silently cleaned up**: uses `TONR` directly, which `06-lad-conventions.md` C-406 bans (TON-only; the convention doc's own action item calls for "a site-standard, cross-platform retentive-timer FB" to replace exactly this). Documented as real, existing, compiling content — not proposed as the template to copy into new equipment.
13. **HMI telemetry** — a cascade of gated `MOVE`s setting `Telemetry` to the status codes above.
14. **Alarm bits** — packs `FTR`/`FTS`/`FaultFB` into `Alarm.%X0`/`%X1`/`%X2`. **Also a known deviation**: 3 bits in one network doesn't satisfy C-501's "exactly one bit per network" exception to C-301 — same shape already flagged during S4's own convention-review pilot on `NodeStatusAlarms`/`PerimeterSafetyAlarms`. Real, compiling, existing content; not the template to copy.

## Examples

`examples/` holds a real, sanitized `CALL` site — everything needed to see the pattern instantiated,
not just described:

- `MotorStarterInst8.ir`/`.xml` — the real instance DB (`Name = 'Overband Magnet Unit'`), exported
  directly from `SampleProject` (already Green-tier, no further sanitization needed).
- `calling-network.ir` — the real calling network (`PlantAutoControl` network 8, "Overband Magnet
  Automatic Control"), touching nearly every documented interface member (all the caller-writes,
  plus `Run` read back into a physical output) and ending in the actual `CALL MotorStarter(MotorStarterInst8, EN := TRUE)`.

## Admission status

1. Instantiated in reviewed, working logic — yes, `MotorDOL`/`MotorStarter`, 8 real instances.
2. Round-trips losslessly — **confirmed**, both the block and the `examples/` instance DB
   (`Converter.Tests/PatternExampleTests.cs`: `MotorDolBlock_RoundTripsLosslessly`,
   `MotorDolInstanceDb_RoundTripsLosslessly`, `NetworkOnlyExample_RoundTripsLosslessly` for the
   calling network — `dotnet test` green, 418/418), on top of `MotorDOL`'s own earlier inclusion in
   S1's `PlantAutoControl`-dependency-closure round-trip proof.
3. Compiles when instantiated — **confirmed**: `MotorStarter` + `MotorStarterInst1..9` all compile
   clean (0 errors) in `SampleProject` (block-level compile, clearing the known
   `IsConsistent`-after-import quirk — `docs/notes/openness-quirks.md`), and the real `CALL` site in
   `examples/` is exactly `PlantAutoControl` network 8, itself already compiled clean as part of the
   same whole-project proof.
4. `pattern.md` complete — reviewed and accepted, Oisín, 2026-07-15.
5. Human sign-off — **Oisín, 2026-07-15.**
