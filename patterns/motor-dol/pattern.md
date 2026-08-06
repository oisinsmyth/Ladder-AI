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

**The interface type ships with the pattern: `MotorIOSet.ir` / `.xml` (added 2026-08-06).** Under
**C-132** the whole caller-visible interface is that one `STATIC` member, so without the type the
pattern could not be instantiated at all — the pattern shipped from admission (2026-07-15) until now
with its own interface type absent, a gap `patterns/valve-two-state/` exposed by shipping one.
The type's member list is recovered from `MotorStarter.ir`'s own `STATIC` section (where
`IO` carries its members as inline nested declarations), name for name, type for type, in order; it
is not a fresh export of the real `TypeDOL`. Its member comments are written from this block's
fourteen networks and from nothing else — each says who writes the member and who reads it, and
where the ladder shows nothing the comment says so (`Name` has no reader and no writer anywhere in
this block). **No member carries `RETAIN`**, deliberately: a `TYPE` member has no `Remanence`
attribute in the real XML shape, so the token would be silently dropped crossing to XML (FI-47).
Retention is declared where it can be — on the FB static, `IO : "MotorIOSet" RETAIN SETPOINT`, which
`MotorStarter.ir` already does. The table below stays as the readable summary; the type file is the
authoritative member list.

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
14. **Alarm bits** — packs `FTR`/`FTS`/`FaultFB` into `Alarm.%X0`/`%X1`/`%X2`. **Status changed 2026-08-06, and the change went the pattern's way**: C-501 previously required "exactly one alarm bit per network", which this network was recorded here as deviating from; the rule was amended that day to require the opposite — **one network per alarm word** — citing this very network as the proven site shape it had been contradicting. Conditions 1 (one network per word) and 2 (each bit a single named cause) are met as written. **Condition 3 is not: the network carries no bit-map comment** — one line per bit with that bit's C-505 alarm text — which is why `converter review` still reports network 14 under C-301/C-501. That condition postdates the block and the block is proven site code, so **the gap is recorded here for the owner to rule on, not silently fixed**: adding the comment is a comment-only change of the S3-proven lowest-risk class, but it is an edit to proven site content and that call is the owner's.

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

## Post-admission changes

**2026-07-16 — C-126 exception condition + C-201 title (documentation fields only; logic untouched).**
A blind review-simplicity validation run (2026-07-16, `docs/notes/review-simplicity-validation-2026-07-16.md`,
finding 6) flagged a pattern-vs-rule tension: `docs/06-lad-conventions.md` C-126's documented
exception (added 2026-07-16) admits the block-top "HMI Times" batch only if the network comment
states the one-pair-per-timer scheme — which network 6 didn't carry — and network 4 was untitled
(C-201). A generation copying the admitted example verbatim would have breached doc 06. Owner
ruling: update the pattern, not waive the rule. Changes: network 6 gained the scheme comment
(modeled on the generated blocks' own proven text — `FB_PusherControl` N3 / `FB_ShredderSequencer`
N2 — adapted to this block's three name-matched pairs); network 4 gained the title "Hand Selector
Edges And Prestart Timeout". Risk class: titles/comments through the IR layer, the S3-proven
lowest-risk write class (cannot change logic — `docs/notes/stage-gates.md`, S3 proofs, verified
live end-to-end three times including real JOB9002 content). `MotorStarter.xml` regenerated from the
edited IR via `converter to-xml` (diff: exactly the two MultilingualText `<Text>` values; sidecar
and all wiring UIds byte-identical); `dotnet test` green including `MotorDolBlock_RoundTripsLosslessly`;
full `to-xml`/`to-ir` cycle is a fixed point. Live TIA re-import re-verification deliberately
deferred for this change class per the S3 precedent — the admission's compile evidence (criterion 3)
covers the unchanged logic. Note: the committed pattern now intentionally carries richer
documentation than the real source block (`MotorDOL` is itself untitled at N4 and comment-less at
N6 — `sanitization/MotorDOL.map.json` reflects the source), so a future re-sanitization from a
fresh export would need these two fields re-applied. Change re-affirmed under the admission's
sign-off discipline: Oisín, 2026-07-16.

**2026-08-06 — `MotorIOSet.ir` / `.xml` added; `MotorStarter.ir` untouched (added file, not a
refactor).** Building `patterns/valve-two-state/` exposed the gap: under C-132 (added the same day)
the block's entire caller-visible interface is the one `STATIC` member `IO : "MotorIOSet"`, and the
type it names was not in this folder — so the pattern could not be instantiated as shipped. The type
is recovered from `MotorStarter.ir`'s own inline nested declaration (27 members, name/type/order
verified identical), given member comments written from this block's own networks, and shaped after
`patterns/valve-two-state/UDT_Valve.ir`. **No `RETAIN` on any member** (FI-47) — verified 0
`Remanence=` attributes in the generated XML. Verified: `converter preflight` clean (0 findings);
`converter to-xml` then `to-ir` round-trips byte-identical; `dotnet test` green. **Not verified: TIA
import + block compile of the type** — the Portal slot was in use by another project at the time and
this run did not queue for it, so the compile gate on this file is outstanding and the type's
standing rests on `MotorStarter`'s own admission evidence (the identical struct compiles clean with
nine instances in `SampleProject`). Item 14's C-501 status also changed on this date, in the
pattern's favour — see "Behaviour, by network" 14 for the one condition still unmet and the ruling it
awaits.
