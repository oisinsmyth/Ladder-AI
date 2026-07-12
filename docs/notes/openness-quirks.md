# Openness quirks log

Working notes on TIA Openness friction (risk R-06). Record here as encountered.

## First-connect approval dialog
Per Portal version/binary, the first Openness connect triggers a manual approval dialog inside TIA Portal. If a connect hangs, check Portal for the dialog.
TODO: paste exact dialog text/screenshot on first connect (S0 exit item).

2026-07-10: first live `openness-cli list` run (attach to an already-running Portal V20 with
project "JOB9003 - K150" already open) completed immediately — no approval dialog appeared, no
`ConnectTimeoutException`. Either this machine had already approved Openness for this Portal
binary before this session, or attaching to an already-open UI session doesn't trigger the
same prompt a fresh launch would. Dialog text is still uncaptured — this item stays open until
seen on a clean approval state.

## Project paths (local machine reference)
Recorded 2026-07-11 at the project owner's explicit instruction (`docs/13-data-boundary.md`'s
redaction rule covers tag/comment/structural *content* pulled from JOB9002, not the scratch copy's
own local filesystem path — the project name itself is already committed unredacted elsewhere in
this doc and in `docs/notes/stage-gates.md`, so the path adds no new identifying information).
Needed because `openness-cli`'s cold-open path (`<project>` as a `.apNN` file, not an
already-open project name) requires an absolute path — a relative one throws
`"The argument 'path' cannot be a relative path."`

- JOB9002 scratch copy: `C:\Users\User\Desktop\AI Ladder Project\JOB9002 - Tom White Waste - Scratch
  Copy\JOB9002 - Tom White Waste\JOB9002 - Tom White Waste_V20\JOB9002 - Tom White Waste_V20.ap20`

## Known constraints
- User must be in the "Siemens TIA Openness" Windows group (log off/on to take effect).
- One Portal instance/session — no parallel Openness sessions.
- Project open is slow; don't kill and retry.
- `PlcBlock.Export()` can return without producing a file, no exception thrown — observed once
  during the ADR-0001 grounding spike (2026-07-10), first of three sequential exports in one
  process. Immediate retry on the same call succeeded. Cause unconfirmed (Portal-side timing?).
  Mitigation for real `export` subcommand work (S1 item 5): verify the output file actually
  exists after `Export()` returns before treating it as success; retry once before failing.

## .NET target framework — net48 required, not net8.0-windows
Confirmed empirically while building `openness-cli` (2026-07-10): a `net8.0-windows` console app referencing `Siemens.Engineering.dll` by path builds cleanly, but fails at runtime the moment any API is called (e.g. `TiaPortal.GetProcesses()`):

```
System.IO.FileLoadException: Could not load file or assembly 'Siemens.Engineering.Contract, ...'. Method does not exist.
 ---> System.MissingMethodException: Method not found: 'System.Reflection.Assembly System.Reflection.Assembly.Load(Byte[], Byte[], System.Security.SecurityContextSource)'.
   at Siemens.Engineering.Private.LocationProvider.AssemblyLoad(String assemblyPath)
```

`Siemens.Engineering.dll` internally calls a .NET-Framework-only `Assembly.Load` overload that has no .NET (Core) 5+ equivalent — this is inside Siemens's own code, not fixable from consuming code. The same reference, rebuilt targeting `net48`, works immediately (`TiaPortal.GetProcesses()` returns cleanly). Requires the .NET Framework 4.8 Developer Pack (`winget install Microsoft.DotNet.Framework.DeveloperPack_4`) in addition to any modern .NET SDK for building — the SDK version and the project's TFM are independent.

Machine reference (this PC): `Siemens.Engineering.dll` for V20 lives at `C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\` (V17–V19 copies also present alongside it for backward-compat referencing).

## API surface reference
Full confirmed object-model shape (TiaPortal/Project/Device/DeviceItem/PlcSoftware/PlcBlockGroup/PlcBlock/ProgrammingLanguage), obtained by reflecting on the installed DLL: `docs/notes/openness-api-surface-v20.md`.

## "Inconsistent blocks and PLC data types (UDT) cannot be exported"
Hit repeatedly, 2026-07-10, on `station_2` of `JOB9002 - Tom White Waste` (scratch copy):
`PlcBlock.Export()` throws this for a block whose `IsConsistent` is false. First seen exporting
`PlantAutoControl` before any import work that session; seen again 2026-07-10 on `PerimeterSafetyAlarms`
*after* a clean import + two successive project-wide compiles both reporting
`State=Success, Errors=0, Warnings=0` — and confirmed to also block exporting `ControlMain`, a
block untouched by any of this session's work.

**Root cause narrowed by `openness-cli sanity-check` (2026-07-10).** Ran it against the whole
project: 180 blocks total, **15 inconsistent**, all in `station_2/JOB9002_PLC` under two groups —
`Map IO/Simulation` (the simulation-mode blocks: `Simulation`, `DOLSim`, `VSDSim`, and 7
`*DOLSim` data blocks) and `Control`/`Alarms` (`ControlMain`, `PlantAutoControl`, `AlarmsMain`,
`PerimeterSafetyAlarms`). **Both device compiles (`station_1/JOB9001_PLC` and `station_2/JOB9002_PLC`) report
`Success, Errors=0, Warnings=0` at the same time** — conclusively confirming a clean
project-wide compile does not clear these 15 blocks' `IsConsistent` flag; it isn't a timing
issue or something a second compile fixes.

The clustering (simulation-mode blocks + the blocks that reference them, `ControlMain` calls
into `PlantAutoControl`, `AlarmsMain` into `PerimeterSafetyAlarms`) suggests a *scoped* inconsistency — possibly
tied to `06-lad-conventions.md` C-111's simulation-mode wiring specifically — rather than a
whole-project corruption, but that's a hypothesis, not confirmed. Still unresolved and still
outside `openness-cli`'s reach — needs the project owner to check directly in TIA Portal (does
the UI's own compile/consistency view agree with Openness's `IsConsistent`? does a manual
recompile of just these 15 clear it?). `openness-cli export`/`compile` don't attempt to work
around this; they report the real Siemens exception and stop, per design philosophy #10.
`openness-cli sanity-check <project>` is the fast way to re-check this list without needing a
failed export to discover it (metadata-only per block, no export attempt).

**Cold-open re-test, 2026-07-10 (project saved, Portal fully closed, then reopened fresh):**
`sanity-check` reproduced byte-identical results — same 180 blocks, same 15 inconsistent, same
paths, both device compiles still `Success`. Rules out session-state/caching explanations
definitively; the flag survives a save + close + reopen of the whole project.

**A freshly-imported, healthy block reproduces the identical symptom.** Same session, immediately
after: picked `NodeStatusAlarms` (`station_2/JOB9002_PLC/Alarms`) specifically because it was *not* on
the inconsistent list, to get a clean end-to-end round-trip. `export` → `converter to-ir` →
`converter to-xml` → `import` all succeeded (import returned the block with no errors), and the
device compile that followed reported `State=Success, Errors=0, Warnings=0`. `sanity-check`
immediately after showed **16** inconsistent blocks — the same original 15, plus `NodeStatusAlarms`.
A second compile (same device) still reported `Success, Errors=0, Warnings=0` and did **not**
clear it; `export` on `NodeStatusAlarms` still refused with the same "Inconsistent blocks... cannot
be exported" error afterward.

**Update, same session — root cause found, not a mysterious TIA/Openness state after all.** The
project owner opened the newly-inconsistent `NodeStatusAlarms` directly in the TIA UI and spotted a
real defect: three array elements of `"CommsProcessData".Node_Error` (`[1]`, `[2]`, `[3]`), each
feeding a separate alarm bit, had lost their array index in the round-tripped logic — the
converter had no concept of array-subscript addressing and collapsed all three references to the
same ambiguous tag path (`src/converter/README.md` has full detail and the fix). That's
content that's syntactically valid enough to compile clean but not equivalent to the source — a
very plausible, and much less mysterious, explanation for why `NodeStatusAlarms` specifically picked
up `IsConsistent = false` right after import: the block's own content was wrong, not something
about the Import()/Compile() cycle itself.

This narrows what's still open: it does **not** explain the original 15 pre-existing inconsistent
blocks (nothing this session touched them, and they were flagged before any import work happened
at all) — that mystery is unchanged and still needs the project owner to check directly in TIA
(does a manual block-level "Compile" clear them? does the UI's own consistency view agree with
Openness's `IsConsistent`?).

**Follow-up test, same session — content correctness is not the explanation either.** With the
array-index fix in place, ran a full round-trip against `station_1/JOB9001_PLC/Alrams/NodeStatusAlarms`
(untouched, never previously imported into — chosen specifically because `station_2/NodeStatusAlarms`'s
copy in the scratch project was already contaminated by the earlier pre-fix import, and the
pristine original had been deleted in scratchpad cleanup, so it couldn't be used for a clean test):

1. Fresh `export` → `to-ir` → `to-xml` → the regenerated XML was inspected directly and confirmed
   byte-structurally correct — the same `AccessModifier="Array"` + nested `Constant` shape, correct
   per-element index, as the original.
2. `import` into `station_1/JOB9001_PLC/Alrams` — succeeded, block returned with no errors.
3. `compile --device "S7-1200 G2 station_1/JOB9001_PLC"` — `State=Success, Errors=0, Warnings=0`.
4. `sanity-check` immediately after: `NodeStatusAlarms` (station_1) **and** `AlarmMain` (station_1,
   the block that calls it) both newly flagged inconsistent — 18 total now, following the exact
   same caller/callee clustering pattern already seen on station_2 (`ControlMain`→`PlantAutoControl`,
   `AlarmsMain`→`PerimeterSafetyAlarms`).
5. `export` on `NodeStatusAlarms` (station_1) — refused with the same "Inconsistent blocks... cannot
   be exported" error.
6. A second `compile` on the same device — still `Success, Errors=0, Warnings=0`, still didn't
   clear it. `export` refused again afterward.

**This settles the question the array-index fix left open.** The block's content here is
verified byte-structurally correct — not a guess, not "probably fine because it compiled" — and
it *still* goes inconsistent after `Import()`, stays that way through two device-level
`Compile()` calls, and still refuses `Export()`. So `IsConsistent = false` after import is **not**
caused by content defects (the array-index bug was real and worth fixing regardless, but it isn't
why blocks go inconsistent) — it looks like an inherent side effect of `Import()` in this
project/TIA version: importing a block (via Openness, regardless of whether the content changed
at all) flags that block and its caller(s) `IsConsistent = false`, and device-level `Compile()`
never clears it.

**Resolved, same session: block-level compile (TIA UI) clears it; device-level `Compile()`
(Openness) does not.** Project owner opened `station_1/JOB9001_PLC/Alrams/NodeStatusAlarms` in the TIA
UI, right-clicked the block itself and ran "Compile (only changes)", then compiled the whole PLC
as well. `sanity-check` immediately after: both `NodeStatusAlarms` and `AlarmMain` (station_1) are
gone from the inconsistent list — back down to the original 16 (the pre-existing station_2 set
plus the still-contaminated `station_2/NodeStatusAlarms`, untouched by this test).

**Root cause, now well-isolated:** `ICompilable.Compile()` on a `DeviceItem` (what
`openness-cli compile`/`sanity-check` use, and the only compile Openness's object model exposes at
device granularity) is **not equivalent** to TIA's own block-level "Compile (only changes)" when
it comes to clearing a block's `IsConsistent` flag after `Import()` — the device compile reports
`Success` throughout, but doesn't touch whatever internal state the UI's block-level compile does.
This is a real Openness/TIA behavior, not a converter or `openness-cli` bug: our tooling reports
exactly what Siemens's API reports and doesn't paper over it, per design philosophy #10.

**Fully solved, same session: block-level compile is available programmatically too — no UI step
needed.** `PlcBlock` implements `IEngineeringServiceProvider` (confirmed by reflecting on the
installed V20 DLL) the same way `DeviceItem`/`PlcSoftware` do, and the assembly has exactly one
concrete `ICompilable` implementer (`Siemens.Engineering.Compiler.CompileProvider`, internal,
reached only via `GetService<T>()` regardless of what object you call it on) — so nothing in
principle stops calling `plcBlock.GetService<ICompilable>()` directly, it just wasn't something
`openness-cli` had tried. A throwaway spike (`GetService<ICompilable>()` on a `PlcBlock`, then
`.Compile()`) confirmed live: non-null, and calling `Compile()` on `station_2/NodeStatusAlarms` (still
inconsistent, still contaminated from the earlier pre-fix import) flipped its `IsConsistent` from
`False` to `True` immediately — no TIA UI interaction at all.

**Implemented in `openness-cli`:** `compile <project> --block <name> [--device <name>]` — new
`IOpennessGateway.CompileBlock`, backed by `PlcBlock.GetService<ICompilable>()`, same safety
refusal and `--device` disambiguation as `export`. Live-verified against the full remaining list:
`PerimeterSafetyAlarms` (one of the *original* 15, predating this session entirely) cleared via
`openness-cli compile --block PerimeterSafetyAlarms` — proving the original pre-existing inconsistencies
share the exact same mechanism, not separate/unexplained corruption. Ran `--block` compile across
all remaining inconsistent blocks (the `Map IO`/`Simulation` cluster, `ControlMain`,
`PlantAutoControl`, `AlarmsMain`); `sanity-check` afterward: **`OVERALL: HEALTHY`, 0 of 180 blocks
inconsistent.** The entire session-long mystery is resolved: every inconsistent block, old and
new, was cleared by a block-level compile that device-level compile could never trigger.

**One caveat found while doing this:** a block-level compile can fail with a genuine error (not
just an `IsConsistent` artifact) if something it calls hasn't itself been recompiled yet — seen
live on `ControlMain` (calls `PlantAutoControl`): failed with `State=Error, Errors=1` when compiled
before `PlantAutoControl`, then succeeded cleanly (`Success, Errors=0`) once retried after `PlantAutoControl`
had been compiled. Compile order matters for callers; retry a caller after its callees are clean.

**Practical implication for the workflow (hard rule 4, "compile gate before done"):** a device-level
`compile` after `import` is not sufficient proof a generated/modified block is clean — the compile
gate now needs `openness-cli compile --block <name>` (or accept a caller retry if it depends on
something just re-imported), not a TIA UI step.

**Reconfirmed, 2026-07-13, on `SampleProject`'s own `TimerSample`** (live-verifying `import`'s own
overwrite/"update" behavior — `docs/notes/openness-api-surface-v20.md`'s "SW.Blocks survey"): a
re-import via `Override` hit the exact same symptom (`export` refused immediately afterward with
"Inconsistent blocks... cannot be exported") — `openness-cli compile --block TimerSample` cleared
it in one call, exactly as this section's own root-cause finding predicts. Nothing new here, just
independent confirmation the pattern holds outside `JOB9002` too, on this project's own reference
project.

## `openness-cli compile`'s diagnostic messages were silently incomplete

Found 2026-07-10, while debugging a genuine compile failure on the seeded reference project (a
new FC referenced two DBs not yet present in the target project — expected, hard rule 3). The
`--json` output showed `errors: 30` with every message's `Description` empty — useless for
diagnosing anything. Reflecting on the installed DLL showed why:
`Siemens.Engineering.Compiler.CompilerResultMessage` has a nested `Messages` property (a
composition, i.e. a tree, not a flat list) — `OpennessGateway.RunCompile` only ever read the
top-level `result.Messages`, which are often just a rollup ("Compiling finished (errors: N;
warnings: 0)") with the real per-error text nested one or more levels inside `Messages`. Fixed by
recursing (`CollectMessages`) — every past compile failure this session that showed a bare count
was missing this detail. Immediately useful: recursing revealed the real cause of the DB-dependency
failure above ("Block 'X' that is accessed has not been compiled") instead of an opaque error count.

## `Wire` UId is not preserved through a real TIA import/compile cycle

Found 2026-07-10, building the first real `Normalizer.AreSemanticallyEquivalent` check that
compares actual live-round-tripped output byte-for-byte (everything before this used a manual
field-count spot-check, not the real automated equivalence assertion). The original design
assumption (ADR-0001, the IR sidecar) was that preserving every source UId in the sidecar
guarantees exact regeneration — true for our *own* `to-ir`/`to-xml` round-trip, but **not**
true once the file passes through a real `Import()` + compile cycle: TIA reassigns every `Wire`'s
own UId on its own terms, regardless of what was sent. Confirmed concretely: the shared rail wire
(`docs/notes/openness-quirks.md`'s own earlier entry on shared multi-endpoint rail wires) went
from UId 126 (the source's own value, correctly preserved by our sidecar into the regenerated
XML) to a fresh 81 on TIA's re-export — and every other wire's UId shifted too — while the *set*
of connections (which `Access`/`Part` UId each wire references) stayed byte-identical. `Part` and
`Access` UIds, by contrast, were preserved exactly, unchanged.

**Root cause of the confusion, and the fix:** a wire's real identity is the set of things it
connects (Powerrail/IdentCon/NameCon references), not its own UId — that was always the wrong
thing to compare exactly. `Normalizer.cs` now strips `Wire`'s own `UId` attribute (alongside the
existing `MultilingualText`/`MultilingualTextItem` `ID` stripping) and compares the `Wires`
collection by content rather than position (TIA also relocates the shared rail wire earlier in
the list on re-export, not just renumbering it). Two implementation bugs found and fixed on the
way to this: the attribute-name filter was hardcoded to `"ID"`, silently never matching `Wire`'s
`"UId"` attribute at all; and the existing `DifferingWireUId_ReturnsFalse` test encoded the
now-disproven assumption directly — replaced with tests asserting the correct behavior (UId
differences with the *same* endpoints are benign; endpoint differences are still caught).

## `openness-cli` now switches projects automatically

Every session up to 2026-07-10 required manually asking the project owner to close whichever TIA
project was open before touching a different one — Openness only allows one project open at a
time (`Projects.Open()` throws "Another project is already open" otherwise), and this came up
repeatedly switching between `JOB9002` and the reference project. `Project` exposes both `Close()`
and `Save()` (confirmed by reflecting on the installed DLL). `OpennessGateway.OpenProject` now
saves and closes whatever else is open before opening the target, automatically. Confirmed real,
same reflection pass: `Close()` is scoped to the project, not `TiaPortal` — Portal itself stays
running throughout, matching File → Close Project in the UI, not File → Exit. Live-verified both
directions (JOB9002 → reference project → JOB9002) — Portal process confirmed still running
throughout via `tasklist`, no data lost (`Save()` runs first).
