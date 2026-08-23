# Openness quirks log

Working notes on TIA Openness friction (risk R-06). Record here as encountered.

## First-connect approval dialog
Per Portal version/binary, the first Openness connect triggers a manual approval dialog inside TIA Portal. If a connect hangs, check Portal for the dialog.
TODO: paste exact dialog text/screenshot on first connect (S0 exit item).

> **READ THIS BEFORE ACTING ON THE PARAGRAPH ABOVE (2026-08-08).** "If a connect hangs, check Portal
> for the dialog" has now sent three separate investigations hunting a dialog that did not exist. The
> measured cause of a silent connect hang on this machine is an **unapproved (rebuilt) binary**, which
> Openness refuses with no dialog at all. See *"A rebuilt binary is refused SILENTLY"* at the end of
> this file for the mechanism, the registry evidence and the one-command diagnostic.

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

## Which S7-1200? Classic and G2 are different articles (migrated from CLAUDE.md 2026-08-21)

**Check WHICH S7-1200 before relying on a G2-only or classic-only fact.** CLAUDE.md read
"S7-1200 G2 target" until 2026-08-11 and that was wrong for the hardware actually in use.

- The live job and its bench rig are the **CLASSIC 1214C, `6ES7 214-1AG40-0XB0`** — verified against
  the device by order-code read, and corroborated by a successful download, which requires the
  configured CPU to match the physical one.
- **G2 is a different article number** (`6ES7 214-1AH50-0XB0`) with different firmware and
  capabilities, so a claim sourced from G2 documentation may not hold here, and vice versa.

Classic enters phase-out from 2026-11-01, so new work may well target G2 — but that is a per-project
fact to establish, not a default to assume.

## `openness-cli` output must be redirected, never piped (migrated from CLAUDE.md 2026-08-21)

`openness-cli` launches Portal as a **child process inheriting stdout**, so **piping its output
hangs forever** — the pipe outlives the command. Redirect with `>` / `Out-File` on the outer
invocation; do not pipe.

## Known constraints
- User must be in the "Siemens TIA Openness" Windows group (log off/on to take effect).
- Concurrent Portal instances on *different* projects are safe (fixed 2026-07-13 — see the
  dedicated section below); two Openness sessions on the *same* project concurrently is still not
  supported (a genuine single-writer-file constraint, not a policy choice).
- Project open is slow; don't kill and retry.
- `PlcBlock.Export()` can return without producing a file, no exception thrown — observed once
  during the ADR-0001 grounding spike (2026-07-10), first of three sequential exports in one
  process. Immediate retry on the same call succeeded. Cause unconfirmed (Portal-side timing?).
  Mitigation for real `export` subcommand work (S1 item 5): verify the output file actually
  exists after `Export()` returns before treating it as success; retry once before failing.
- **Standalone (non-multi-instance) system-FB instance DBs are invisible to `SW.Blocks` entirely
  — confirmed twice, generalizes beyond one instruction.** A `GlobalVariable`-scope instance of a
  system FB (a name-only `<Instance>` reference to a standalone DB, not a multi-instance `Static`
  member) doesn't appear in `PlcBlockComposition`/`list` under its own name, isn't exportable by
  name (`export --block <name>` reports "No block named ... found" even though the block
  genuinely exists and is referenced live elsewhere), and if recreated via
  `PlcBlockComposition.CreateInstanceDB` the auto-numbering deterministically assigns `DB0` — a
  number that is itself invalid (`"The entered address is not within the valid address range"`
  on compile) and not repairable via any exposed Openness API (no way to directly set a DB's own
  `Number`). First seen with a standalone `TON` (`CycleDelayReset` in `MotorFwdRevSystem`,
  `docs/notes/stage-gates.md` Phase 1) — resolved there only by the project owner converting it
  to a proper multi-instance `Static` member at the source. Confirmed again 2026-07-14 with
  `Modbus_Master`/`Modbus_Comm_Load`'s own `Modbus_Master_DB`/`MB_Master_Comm` (Phase 2 Tier 4,
  same doc) — neither name appears anywhere in `JOB9002`'s full block listing across either PLC
  station, despite being real, live-referenced instance DBs. No Openness-exposed fix found either
  time; both instances left honestly unverified for live compile rather than forced. If this
  recurs a third time, treat it as a confirmed general limitation of standalone system-FB
  instances (not per-instruction), not a one-off.

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

**Superseded, 2026-07-13 — see the dedicated section below.** This auto-switch-by-closing
behavior was safe under the single-operator assumption active at the time (nothing else was ever
running Portal concurrently), but became a real risk once a human could be running Portal
manually, on a different project, at the same time — `OpenProject` would have silently saved and
closed *their* live project to make room for whatever this tool was asked to open next. Replaced
with "never touch a project this tool didn't open — launch a dedicated fresh Portal instance
instead" (still auto-switches, just never by force-closing something unrelated).

## Safe concurrent Portal sessions on different projects

Prompted by the project owner's own need: manual PLC engineering in TIA Portal on one project
while `openness-cli`/Claude Code keeps working on a different one, at the same time. Investigating
surfaced the real gap this fixes, described above — `OpenProject`'s own force-close-whatever's-open
behavior was written for a single-operator world and became unsafe the moment that stopped being
true.

**Fix**: `OpennessGateway.OpenProject` no longer calls `Save()`/`Close()` on any project it didn't
open itself. If the attached process has the target project already open, reuse it (unchanged —
the common "engineer already has it open" convenience). If it has nothing open, open the target
there directly (unchanged). If it has a **different** project open, leave it alone entirely and
launch `new TiaPortal(TiaPortalMode.WithUserInterface)` — a dedicated fresh instance — and open
the target there instead. No new CLI flag; this is strictly safer than the old behavior with no
functional downside for existing solo use.

**Live-verified, 2026-07-13**: launched TIA Portal directly (`Siemens.Automation.Portal.exe`, not
through Openness) with `SampleProject` open, simulating a human's independent manual session, and
left it running. Ran `openness-cli list` against `JOB9002` (a different project) while that session
stayed up. Result: `JOB9002` listed successfully via its own newly-launched Portal instance
(confirmed via `tasklist` — three new process IDs appeared, the original three from the manual
session were untouched throughout). Re-queried `SampleProject` immediately after: same 10 blocks,
identical content, instant reconnect (proving it had never been closed, not just "still running
by luck"). Six TIA Portal processes coexisted with zero interference. Cleaned up all
test-launched processes afterward via `taskkill`.

**Residual, non-fixable caveat**: `Connect()`'s very first `Attach()` call, if it happens to reach
the human's manually-launched process before `OpenProject` discovers it's occupied, can still
trigger TIA's own one-time first-connect approval dialog on their screen — `Attach()` alone never
opens/closes/saves anything, so there's no data risk, purely a one-time visual interruption. Not
avoidable with the current Openness API surface — there's no way to inspect what's open in a
running process without attaching to it first. In this live test, no such dialog was needed
(this exact V20 install had already been trusted machine-wide from earlier sessions) — worth
confirming this stays true tomorrow, but not something to build around.

**Still unsupported, and correctly so**: two Openness sessions holding the *same* project open at
once. That's a genuine TIA-side single-writer-file constraint (`Projects.Open()` throws "Another
project is already open" within one session, and a second local instance opening the same
`.apXX` file directly would risk file-lock/corruption issues), not a design choice this tool could
relax.

### Follow-up, 2026-07-14 — the fix above caused a real instance pileup, now fixed properly

Hit live while attempting a full import+compile cycle test: two `MotorDOL` import attempts each
timed out (Bash tool's own outer timeout, 6 and 10 minutes respectively — not `openness-cli`'s own
graceful internal timeout, which never got the chance to fire because the *combined*
`--timeout-connect` + `--timeout-open` budget I'd set exceeded the outer Bash timeout, a mistake on
the invoking side, not the tool's). Killing a client process (`openness-cli.exe`) mid-COM-call can
leave the *server* process (Portal, a separate process) stuck waiting on a response that will
never come. `Connect()` only ever inspects a single process (`GetProcesses()[0]`); the original
concurrent-sessions fix's own `OpenProject()` only checked *that one* attached process before
deciding "occupied → launch fresh." Each retry attached to an arbitrary process, found it
unusable, and launched *another* full instance rather than checking whether any of the *other*
already-running processes (including ones from earlier retries) were actually fine. Process count
grew 3 → 4 → 5 across repeated retries, each new instance competing for resources with the others.

**First fix attempt — search all running processes, not just one — surfaced a second, sharper
bug.** Restructured `OpenProject()` to loop over every `TiaPortal.GetProcesses()` result before
falling back to a fresh instance. This reduced pileup but a live retry still failed outright:

```
Unable to open the project under path '...\SampleProject\SampleProject.ap20'.
The project/library ...\SampleProject.ap20 cannot be accessed. It has already been opened by
user User on computer AWCS-VPC10. Note: If the application was not correctly closed, the open
projects and libraries can only be opened again after a 2 minute delay.
```

Root cause: the search treated "this particular process has nothing open" as immediately safe to
open into, without first confirming that *no other* already-running process already held the
target project's own exclusive file lock. If `Connect()`'s primary attached process happened to be
empty while a *different* sibling process genuinely had the target open, the empty one would try
`Projects.Open()` anyway and TIA's own file lock correctly refused it.

**Second, corrected fix — two full passes, not one interleaved check.** Pass 1 searches *every*
running process for the target already open (exact name/path match) before pass 2 is ever allowed
to consider an empty process fair game to open into. Only if neither pass finds anything usable
does it launch a dedicated fresh instance. Each non-matching candidate handle is disposed once the
winner is chosen (same "`Attach()` alone never touches anything, `Dispose()` on an attached handle
just releases the reference" reasoning as before).

**A separate, unrelated bug found and fixed along the way**: `FindAlreadyOpenProject`'s own path
comparison used plain `string.Equals`, which doesn't tolerate forward-slash vs. backslash
differences (`C:/foo/bar.ap20` vs. `C:\foo\bar.ap20`, `Project.Path.FullName`'s own native
format). A forward-slash identifier (from a Bash `pwd -W` invocation) failed to match an
already-open project's own path, spuriously concluding it wasn't open anywhere and triggering an
unnecessary extra Portal launch — confirmed live, caught immediately when the project owner
noticed an unexpected new Portal window and asked about it directly. Fixed via
`Path.GetFullPath()` canonicalization on both sides before comparing.

**Live-verified, 2026-07-14**: from a genuinely messy starting state (3 stray Portal processes
from the earlier pileup, one of which held `SampleProject`'s real file lock), a `list SampleProject`
call — first with the same forward-slash path that triggered the bug, confirming the path fix —
correctly found the process holding the real lock and reused it, with **no new process spawned**
(confirmed via `tasklist` before/after). All 73 openness-cli tests still green throughout both
fix iterations. Cleaned up the 2 now-confirmed-idle stray processes afterward, with the project
owner's explicit go-ahead (killing Portal processes is exactly the kind of action Claude Code's
own safety classifier rightly wants explicit confirmation for, given the earlier concurrent-
session work — see the `openness-cli` block-deletion entry in `docs/notes/stage-gates.md` for the
same pattern on a different action).

**Practical lesson for future invocations**: `openness-cli`'s own `--timeout-connect` and
`--timeout-open` are sequential, not parallel — their *sum* must stay comfortably under whatever
outer timeout wraps the whole call, or the outer kill will fire first and risk leaving Portal
stuck, defeating the whole point of having a graceful internal timeout in the first place.

**TODO, deferred — additional testing for the multi-instance feature**, per the project owner's
own request (2026-07-14). Everything proven so far was live-verified opportunistically, either
via a deliberate simulation (2026-07-13, `SampleProject` + `JOB9002` open at once) or by observing
real failures as they happened during the `MotorDOL` full-cycle work (2026-07-14). Not yet
covered, worth a dedicated pass later:
- A genuine two-*person* concurrent session (the project owner actually running Portal manually
  on their own project tomorrow, per the original ask) — everything so far has been one operator
  (Claude) simulating both sides.
- The two-pass search (`OpenProject`) exercised against 3+ real running processes in more varied
  configurations (e.g. two empty, one occupied by something else, one with the real target) —
  only ever exercised opportunistically against whatever state a prior failure happened to leave.
- The `Attach()`-fails-for-one-candidate skip path (the `try`/`catch` around each candidate in the
  search loop) — reasoned through, never actually exercised against a genuinely dead/unresponsive
  process.
- `PathsMatch`/`FindAlreadyOpenProject` are pure string/path logic, not COM-touching — genuinely
  unit-testable (unlike the rest of `OpennessGateway`), and currently have zero test coverage.
  Lowest-effort, highest-value item on this list — worth doing before the others.

## A second concurrent Portal instance sometimes won't connect at all (2026-07-14, transient — resolved on retry)

**Update, 2026-07-14, next session turn:** a fresh retry (`export --type TypeDOL --device
JOB9002_PLC`, same target project, one `SampleProject` instance still idly running) succeeded
immediately — a new Portal instance launched, just slower than the timeout budgets below allowed
for. Never reproduced again afterward in the same session. Whatever caused the four failures below
was transient, not a hard concurrency limit — the working hypotheses underneath (licensing/seat
limits, resource contention) are now the *less* likely explanations, though still unconfirmed
either way. Leaving the original entry below intact as the real, live symptom observed at the
time — just no longer treat it as an open blocker.

Hit live while grounding UDT support (`export --type TypeDOL` against `JOB9002`, one `SampleProject`
Portal instance already idly running from earlier work). Four patient retries
(`--timeout-connect` 240s, 280s, 320s, 560s — the last one waited a full 9 minutes) all failed
identically at the *connect* stage — `openness-cli`'s own graceful internal timeout fired every
time (confirmed: `ConnectTimeoutException`'s message, not a Bash-tool-level kill), and `tasklist`
confirmed **no second Portal process ever appeared** — the launch attempt itself never got far
enough to spawn a visible process, let alone open a project. A plain `list` against the same
project failed identically, ruling out anything specific to `--type`/`export`.

**Not the same failure mode as the earlier pileup bug** (`docs/notes/openness-quirks.md`'s own
"Follow-up, 2026-07-14" section) — that one produced *extra* processes; this one produces *none*.
Whatever's gating this happens before `TiaPortal`'s own constructor returns anything observable.

**Not resolved this session** — the one obvious next diagnostic step (close the idle
`SampleProject` instance and retry with zero other Portal processes running, to isolate whether
this is specifically about *concurrency* or something else entirely) needs explicit human
confirmation before killing a Portal process, correctly enforced by Claude Code's own safety
classifier even under a general "keep working, I trust your judgment" instruction — a blanket
statement doesn't meet the bar this session already established (explicit, specific,
per-instance confirmation) for an irreversible action, especially with the user not immediately
available to catch a mistake. Left the idle instance running rather than forcing the issue.

**Working hypotheses, none confirmed**: a TIA Portal licensing/seat limit specific to
Openness-launched (`new TiaPortal(...)`) instances (as opposed to a manually double-clicked
`Siemens.Automation.Portal.exe`, which is how the *other* concurrent instance in this same session
was created, and which worked fine); resource contention from the already-running instance
starving a second cold launch; or something specific to this particular machine's state at the
time, unrelated to concurrency at all. Needs the project owner to either watch a retry happen live
(catch a dialog or other visible symptom the automated side can't see) or explicitly authorize
closing the idle instance first to isolate the variable.

## CLOSED, 2026-07-14 — dedicated stability audit, root cause confirmed: process pileup, not the concurrent-session feature

The project owner raised a direct concern after this symptom's second occurrence in one session
(the entry above, plus a near-identical hang hit again later the same day): is the concurrent-Portal
feature itself unstable? Rather than take another anecdotal data point, a full rigorous test plan
was designed and walked through together, live, phase by phase
(`docs/notes/concurrent-portal-test-plan.md` has the complete record — every test's exact PIDs,
timings, and pass/fail, not summarized from memory). Six phases, all closed out:

- **Phase 0** (single-instance sanity, no concurrency) — 4/4 pass.
- **Phase 1** — found a real, previously-untested bug: `OpenProject()`'s "an empty process is fair
  game" rule didn't distinguish a human's own freshly-launched, still-empty Portal window from one
  this tool created itself, so it would silently open its own target into a human's window without
  asking. **Fixed**: `Connect()` now records whether it had to launch a brand-new instance; only
  that exact instance (known, for certain, to be this tool's own, this run) is ever treated as fair
  game when empty. Any other discovered empty process — a human's window, or a leftover orphan from
  an earlier run — is now left alone unconditionally; a dedicated fresh instance is launched instead.
  Retested live after the fix: confirmed a human's empty window was untouched, a separate instance
  launched for the CLI's own target.
- **Phase 2** (genuine two-party concurrency, different projects, for the first time with an actual
  second human rather than one operator simulating both sides) — 4/4 pass. No interference either
  direction; a real unsaved human edit survived every concurrent CLI operation untouched.
- **Phase 3** (same-project concurrency) — 1/1 pass. TIA's own single-writer lock refused the second
  open cleanly, no hang, no corruption — a genuine TIA-side constraint, working exactly as it should.
- **Phase 4** (the actual instability question) — killing the client process genuinely mid-launch
  (not just a generic kill — reproduced via an atomic launch+find-PID+kill script, confirmed by an
  empty output file, i.e. the operation never got to finish) does **not** leave Portal itself stuck:
  a follow-up call succeeded cleanly every time. Then, the decisive test: **5 fresh-instance
  launches in a row, from a clean baseline, alternating target projects, cleaning up between each
  trial — 5/5 succeeded, no hangs, consistent ~20-28s connect times.** Every real hang this session
  ever hit happened with multiple stale/orphaned processes already sitting around; from a clean
  process list, concurrent access was completely reliable across every trial.
- **Phase 5** (test-coverage gap) — `PathsMatch` (the exact code the 2026-07-14 slash-direction bug
  lived in) had zero unit test coverage; added 6 tests, `internal`-scoped with a new
  `InternalsVisibleTo` for the test assembly. `FindAlreadyOpenProject` itself stays live-verified
  only (real COM-backed `Project`/`ProjectComposition` types, no fake available).

**Verdict, stated plainly: the concurrent-session feature is not the cause of instability.**
Every scenario deliberately constructed to stress it — real two-party use, same-project refusal,
a client killed mid-connect, five back-to-back fresh launches — behaved correctly and predictably.

## OPEN, 2026-07-15 — a synthesized (`--synthesize`) FB's own instance DB sticks at "invalid number 0" on block-level compile, even though the whole-device compile is clean

Found building the Kestrel Shredder System's `iDB_PusherControl` (instance of `FB_PusherControl`,
itself imported via `converter to-xml --synthesize` — no real TIA donor XML, this session's own new
capability). `create-instance-db` reports the new DB as `DB0` immediately after creation (expected —
matches the same not-yet-numbered display `iDB_MotorFwdRevSystem_Shredder` also showed before its
own first compile). But `compile --block iDB_PusherControl` fails with `Number: The block
iDB_PusherControl DB has an invalid number 0`, **while a whole-device `compile` (no `--block`)
reports `STATE: Success, ERRORS: 0` in the same project state** — the device-level pass evidently
resolves/tolerates the numbering, the block-level pass does not.

Tried, in order:
1. Block-level recompile after a whole-device compile had already run — same error, unchanged.
2. Delete + recreate the instance DB from scratch (`delete --yes` then `create-instance-db` again)
   — same error, unchanged. Rules out a one-off transient project-state glitch from the delete
   itself; this is reproducible against a fresh instance DB of the same FB.

**Not yet root-caused.** `iDB_MotorFwdRevSystem_Shredder` — created the same way
(`create-instance-db`), same session, same project — has never shown this problem; its own
block-level compile settled to a normal `DB6` on the first try. The one structural difference
between the two FBs is that `FB_MotorFwdRevSystem` was imported unmodified from a real TIA export
(`SampleProject`), while `FB_PusherControl` was synthesized via `--synthesize` — real,
already-compiling content either way (`FB_PusherControl` itself compiles clean, 0 errors, block-
level — this issue is specific to *its instance DB's own* number assignment, not its logic), but
this is the only lead so far, not confirmed. Worth checking, not done yet: whether every other
`--synthesize`-produced FB's own instance DB shows the same symptom, or whether this is specific to
`FB_PusherControl` for some other reason (e.g. its unusually large STATIC section).

**RESOLVED, same day.** Root cause still not confirmed (see above — the `--synthesize`-vs-native
correlation is a lead, not a proof), but a reliable **workaround** is: don't use
`create-instance-db` for a `--synthesize`-produced FB at all — hand-author the instance DB's own
`.ir` file directly (`DB <name> / NUMBER <n> / INSTANCEOF <FBName> / MEMBERS` mirroring the FB's own
STATIC section verbatim — the same shape `create-instance-db` would have scaffolded, just typed out
by hand with an explicit, chosen-non-colliding number instead of TIA's own auto-numbering), then
`converter to-xml` + `openness-cli import` through the exact same path every other DB in this
project already uses. Live-verified: `iDB_PusherControl` (instance of `FB_PusherControl`, the
`--synthesize`-produced FB this bug was found on) — `create-instance-db` reproduced `DB0`/"invalid
number 0" a 4th time (including once as a direct sanity-check re-run at the project owner's own
request); the hand-authored `NUMBER 10` alternative imported as `DB10` and compiled clean (0
errors) on the first try. This is now the standing recommendation for any future `--synthesize`
FB's own instance DB, not just a one-off patch — `create-instance-db` remains correct and preferred
for instance DBs of natively-authored/imported FBs (confirmed working normally for
`iDB_MotorFwdRevSystem_Shredder`, never showed this symptom).
The "sometimes won't connect at all" symptom correlates with **Portal-process accumulation**, not
with concurrency itself: both real occurrences this session happened with several stale processes
already piled up; a clean process list was reliable every single time it was tested today. One real
bug was found and fixed (the empty-window case above) — a genuine gap, now closed, not evidence the
broader design was unsound.

**Second real gap found and also closed the same day, not left as an accepted cost**: a client
killed mid-launch used to leave a permanently orphaned process behind — nothing would ever reuse or
clean it up, symmetric with (and a direct side effect of) never touching a human's window. Closing
this properly required a fresh, full re-reflection on `TiaPortalProcess` — the original 2026-07-10
API survey had recorded only `Attach()`/`Dispose()` on that type, missing `Id` (the real OS process
ID) entirely, which is exactly the identifier needed to tell "an empty process this tool launched
itself, in an earlier interrupted run" from "a human's own empty window" (`docs/notes/
openness-api-surface-v20.md` has the correction). Built `LaunchedInstanceRegistry` — persists which
PIDs this tool has itself launched, across separate invocations, marked at launch and unmarked on
success; `OpenProject()` now reuses a discovered empty process only if the registry positively
confirms it's this tool's own, never guessed. Live-verified the full recognize → reuse → unmark
cycle (a marked, genuinely-empty process's memory jumped when its target opened into it, no
redundant instance was launched, and the mark was correctly cleared afterward). One genuinely
interesting complication surfaced along the way: killing the client process doesn't always abort an
already-issued, in-flight Portal launch — it can complete asynchronously regardless, which is why
externally timing a kill to hit the exact narrow "marked but not yet opened" window proved
impractical and the fix was verified by direct construction instead of a live race. Full story,
including why the live-race approach didn't pan out and how verification was actually done:
`docs/notes/concurrent-portal-test-plan.md`.

**Practical guidance going forward**: periodically check `tasklist` for
`Siemens.Automation.Portal.exe` and close idle instances by hand if any pile up regardless — process
hygiene remains the first line of defense against the pileup state that correlates with connection
hangs, even though orphan accumulation specifically is now self-healing on the next invocation.

## `export --out` must be an absolute path (2026-07-29)

`openness-cli export` with a relative `--out` fails on every block type with:

```
EngineeringTargetInvocationException: Error when calling method 'Export' of type
'Siemens.Engineering.SW.Blocks.FC'. The argument 'path' cannot be a relative path.
```

The rejection comes from Siemens's own `Export()`, not from `openness-cli`, so it surfaces as a
type-qualified engineering exception rather than an argument-validation message — the "relative
path" sentence is the last line and easy to skim past when the exception name suggests something
went wrong inside Portal. Reproduced against `FC`, `FB` and `GlobalDB` in the same run, so it's the
path check, not anything block-specific.

Fix is just to pass an absolute path. Worth knowing because a shell loop over several blocks with a
relative `--out` fails **identically on every block**, which reads like a project/connection problem
rather than a bad argument. Nothing was written and no state changed — the check happens before
`Export()` does anything.

**FIXED IN THE TOOL, 2026-08-10 (FI-68) — kept here because the constraint is still true of the API.**
`openness-cli` now resolves every path argument that reaches Openness as a *file target* to an
absolute path at the argument boundary (`Cli/PathArguments`), so a relative `--out` simply works.

The reason it earned a fix rather than a docs line is what it cost the second time. Hit again on
2026-08-10, the exception read **exactly like** the "Inconsistent blocks and PLC data types (UDT)
cannot be exported" refusal — a real and frequent condition on that job. The only thing that stopped
a long detour hunting an inconsistent block was that the agent happened to run `sanity-check` between
attempts and got `HEALTHY` on both lines each time. A message that confidently names the wrong cause
is this project's most expensive recurring defect class (FI-61, FI-66, FI-69 are the others).

Fixed for the **class**, not the one argument: `export --out`, `export-all --out`, `library --out`,
the `import` file list and `--tia-install` are all resolved by one rule, so an argument added later
inherits it. **The project identifier is deliberately excluded** — it is documented as "either a full
`.apNN` path or a bare project name", and resolving a bare name against the current directory would
silently turn a name into a path that does not exist.

*(Candidate `openness-cli` improvement: resolve `--out` against the working directory itself before
calling `Export()`, so the caller doesn't have to. Not done — recorded here rather than fixed
because it came up mid-job on unrelated work.)*

## Five TIA behaviours found building a DB/UDT set from scratch (2026-08-05)

All five surfaced in one greenfield data-structure stage — the first time this project has
created a full UDT and DB landscape rather than exporting one that already existed. None is a
bug; all five are things the design had assumed otherwise, and each was cheap to absorb at
declaration time and would have been expensive after logic was bound to it.

**1. A DB cannot itself be an array.** A design that writes `MyBlock[x].Member` has to become
`"MyBlock".Item[x].Member` — the array is a *member* of the DB, never the DB. This adds one level
to every published path. It matters most where the path is an issued contract (an HMI tag list, a
comms boundary): reissue the path list before the panel is built, not after.

**2. Start values are per TYPE, not per array element.** Defaults declared on a UDT seed *every*
element that uses it. There is no IR (or TIA source) way to give element 1 a different start value
from element 2. Anything genuinely per-element — a seeded first recipe, a per-vessel physical
constant — is a typed-in value or something a block writes at first run, not a start value.

**3. TIA normalises trailing-zero REAL literals on export.** `0.30` returns as `0.3`, `0.60` as
`0.6`. Numerically identical, but a naive text diff of sent-vs-returned XML reports it as a
change. Compare parsed values, not literals, or the round-trip check cries wolf.

**4. Every imported DB gets `MemoryReserve = 100` bytes, and `Optimized` access, by default.**
Neither is expressible in the IR — `DbSourceWriter` emits no `MemoryLayout`, so TIA's default
wins. If a design wants Standard access (for absolute-offset addressing, or to remove the
download-without-reinitialisation hazard rather than merely detect it), that is a tick in the TIA
properties dialog or converter work — it cannot be carried through this pipeline today.

**5. Openness exposes no memory-usage data at all.** `compile --json` carries none, and there is no
API surface for load/work/retentive memory. A retentive budget therefore cannot be verified
programmatically — the retentive-memory dialog after first download is the only authority. Plan
for a human to read it rather than assuming a check can assert it.

*(Recorded from a live job; the behaviours are platform facts and carry nothing project-specific.)*

## `CONV` rejects `TIME`. `T_CONV` is the instruction that takes it.

**Found 2026-08-07, by a throwaway probe rather than by a failed block.**

Converting a timer's `ET` to an integer is the natural way to accumulate elapsed time, and the
converter emits `CONVERT` for it happily — with a correct-looking `SrcType="Time" DestType="DInt"` —
and it round-trips byte-identical. **TIA refuses it on import:**

```
Error when calling method 'Create' of type 'Siemens.Engineering.SW.Blocks.CompileUnitComposition'.
No import will be performed. The element with UId 19 causes the following error message:
Data type Time is not permitted here.
IMPORT_EXIT=5
```

`T_CONV` (`src_type="Time" dest_type="DInt"`) imports clean and compiles 0/0.

**Why this is worth a note rather than a shrug.** Nothing on our side catches it: the converter
accepts it, `to-xml` produces plausible XML, the round trip is clean, `preflight` passes and
`converter review` passes. **The first thing that says no is the Portal import**, which is the
expensive place to find out — and if it is discovered during a whole-block import, a failure on a
network that was just redesigned reads as a *design* failure rather than a wrong instruction choice.

**The practical lesson is the probe, not the instruction.** A construct never written on a project
before is worth importing as a two-network throwaway *before* it goes into a real block. That cost
one probe cycle here and would have cost a misdiagnosed redesign otherwise.

## `converter to-ir <file>` overwrites `<file>.ir` in place, and emits a sidecar by default
**Confirmed live 2026-08-08, by an agent that destroyed its own work with it and reported it.**

Converting a TIA re-export to compare it against the working IR is an obvious move — and running
`to-ir ir/FB_Drum.xml` writes `ir/FB_Drum.ir`, **overwriting the file you were about to compare it
against.** The agent lost every edit in two blocks this way. Worse, the rewritten files carried
`SIDECAR` sections, which this corpus does not use, so the damage was not a clean revert either.

**Convert an export on a COPY, in a scratch directory, never against `ir/`.** If you must convert in
place, `--no-sidecar` at least matches this corpus's shape — and it verifies the sidecar is
derivable (ADR-0005) as a side effect, which is a useful check in its own right.

The repair is worth recording too, because it is the standard to hold: the agent re-derived the
files, re-applied every edit from a scripted patch asserting exactly one match per anchor (17 of
17), and then separately proved it had not reverted a *concurrent* agent's work on one of the same
files, by diffing against a timestamped baseline and showing 13 networks of that agent's later work
still present. Reporting the accident is the minimum; proving the blast radius is the bar.

## A per-block `compile` exits 8 at `ERRORS: 0` on a project with a standing warning
This project carries an inherited `"Inputs or outputs are used that do not exist in the configured
hardware"` warning on every block, which sets `STATE: Warning`, and `RunCompile` returns
`CompileFailed` for any state that is not `Success`. **Read the `ERRORS:` count, not the exit code**,
for per-block compiles here. The exit code remains meaningful for `sanity-check`, which is the gate.

## A rebuilt binary is refused SILENTLY — the whitelist is keyed on (Path, FileHash) (2026-08-08)

**The mechanism, from the registry rather than from inference.** TIA keeps an allow-list of Openness
callers:

```
HKLM\SOFTWARE\Siemens\Automation\Openness\<version>\Whitelist\<exe name>\Entry (N)
  Path         = C:\...\src\openness-cli\OpennessCli\bin\Debug\net48\openness-cli.exe
  DateModified = 2026/07/10 08:40:17.964
  FileHash     = PY8nw9ndT2J/qsmq1G289KwAEM10YkvRPjcY1j5MlTE=     <- base64 SHA-256
```

**One entry is added per approved build**, so the key accumulates — this machine had **84** entries
for a single Debug path, 3 for the Release path, and 10+ for various worktree builds. Rebuild the
executable and its hash matches none of them. Openness then **refuses the caller and never
responds**: `Attach()` blocks until the caller's own timeout expires. No dialog. No exception. No log
entry. From outside it is indistinguishable from a wedged Portal.

**What it cost, so the shape is recognisable.** A `dotnet test` on the openness-cli solution, run to
check something unrelated, rebuilt the binary *while an agent was mid-run*. Every attach after that
moment hung for its full `--timeout-connect` — 3 min, then 15 min, then more. Roughly an hour went
into "Portal is wedged, someone must clear a dialog". Two things made it hard to see:

- **The CLI's own error text asserted the dialog**, and the reader believed it.
- **`portal-status` was used as evidence that no dialog existed**, which it cannot be: it is the one
  subcommand that never attaches, so it is structurally incapable of observing one. A clean
  `portal-status` says nothing at all about a dialog.

**Proof there was no dialog, and the technique is reusable.** `EnumWindows` over both Portal
processes showed only the two real project windows plus the usual hidden plumbing
(`ThreadSynchronizer`, `SCP Communication`, IME, GDI+) — every main window `visible` **and
`enabled`**, which a modal owner would not be. That is suggestive but not conclusive on a WPF app,
where a dialog can be an in-window overlay with no HWND of its own, so it was settled by capturing
each window with `PrintWindow` and **looking at the pixels**: both Portals idle, no dialog anywhere.
Screenshot-the-window is the check worth reaching for when window enumeration is ambiguous.

**The one-command diagnostic.** Run a build that is already approved against the same project:

```
"...\src\openness-cli\OpennessCli\bin\Release\net48\openness-cli.exe" list "<project>.ap20" --timeout-connect 90
```

It connected and listed blocks in **57 s** while the rebuilt Debug binary was hanging on the identical
project. That single comparison separates "this executable is not approved" from "Portal/project is
unhealthy", and costs one minute.

**Now detected automatically (FI-61).** `openness-cli` hashes itself and checks the whitelist
*before* attaching, printing a warning that names the cause and predicts the hang. Verified live: it
reported `84 earlier build(s) of this exact path are approved, but none of them matches this file's
current hash`. The check is **advisory and must stay advisory** — it warns and proceeds, never
blocks. A false negative (a whitelist layout it does not understand, a registry view it cannot read)
would otherwise refuse every Portal command on a machine where everything works, which is far worse
than the hang it prevents. `ConnectTimeoutException`'s text now lists both causes and no longer
claims to know which one it is.

**Practical rules.**
- **Do not rebuild `openness-cli` while Portal work is in flight.** `dotnet test` on
  `openness-cli.sln` is a rebuild. `converter.sln` is safe — it never touches Portal.
- Approval does **not** carry across build locations, so a worktree build is a different application
  to Openness even from identical source.
- Re-approving a build means adding a whitelist entry under `HKLM`, which is a privileged security
  control and the machine owner's call — not something the tooling should do for itself.

### The same refusal has a SECOND face: it sometimes THROWS instead of hanging (2026-08-09)

Found from the other side, by a branch that hit this wall while the section above was being written
on master. The section above documents the **silent** presentation. There is another, and it looks
like a completely different bug.

An unapproved build was run twice, minutes apart — same binary, same project, nobody clicking:

| Run | Result | Exit |
|---|---|---|
| 1 | `AggregateException … ---> EngineeringSecurityException: Security error.` — fast, loud | **5**, now **2** |
| 2 | attach never completed; the CLI's own 15-minute connect timeout fired | **3** |

Same cause, two presentations, **neither of which names the approval**. The plausible reading is that
it depends on which Portal the client reaches — a process that has already refused this hash rejects
outright, while a freshly-launched one goes quiet — but that is a hypothesis from two data points.

So the diagnosis for a wedged or failed attach is **three-way**: another session's client, an
unapproved build refused silently, or an unapproved build refused loudly. Concretely:
**exit 3 OR exit 5-with-a-security-error on a freshly rebuilt binary both mean "needs approval"**,
and neither should be read as contention or as a broken install.

**Also fixed on that branch, and it complements FI-61 rather than duplicating it.**
`EngineeringSecurityException` was exiting **5 = UnexpectedError** with a full inner-exception dump —
the most predictable consequence of editing this program, reported as an internal fault. It now exits
**2 = EnvironmentError** and prints what to do. It arrives *wrapped* (the connect path runs through
`Task`), so the classifier walks the chain including every `AggregateException` branch; matching only
the top-level type finds nothing.

The two mechanisms cover different halves and **neither subsumes the other**: FI-61's pre-attach
whitelist check cannot see a refusal that only materialises on connect, and the post-hoc message
cannot fire when the attach hangs instead of throwing.

### Never PIPE `openness-cli` output (2026-08-09)

`openness-cli` launches Portal as a **child process that inherits stdout**, so anything that waits for
the stream to close — `| Out-File`, `| Tee-Object`, `$(...)` — **hangs forever even after the command
itself has finished**, because the pipe stays open as long as the Portal child lives. Redirect with
`>` / `Out-File` on the *outer* invocation instead. Cost two harness hangs before it was spotted, and
it presents as yet another "Portal is wedged".

## Approving a rebuilt binary WITHOUT a human at the machine (2026-08-10)

The sections above establish the cost: every rebuild is a caller TIA has never seen, and a caller TIA
has never seen needs a person to accept it. Seconds when someone is sitting there, fatal when nobody
is.

**Accepting that dialog writes a registry entry and nothing else.** So the entry can be written
directly, and then the dialog is never raised at all. Three scripts under `tools/`, in order of
preference:

| Script | What it does | Needs |
|---|---|---|
| `openness-approve-setup.ps1` | **One time.** Grants ONE account read/write on `HKLM\...\Openness\<ver>\Whitelist\openness-cli.exe` — that key only | Elevation, once |
| `openness-approve-build.ps1` | Approves a built exe: computes base64 SHA-256, writes `Path`/`DateModified`/`FileHash` | Nothing, after setup |
| `openness-approve-watch-dialog.ps1` | **Fallback.** Watches Portal via UI Automation and accepts the dialog if one appears | Nothing |

**Builds of `src/openness-cli` now approve themselves.** The `ApproveForOpenness` target in
`OpennessCli.csproj` runs the approve script `AfterTargets="Build"` with `$(TargetPath)`. It **never
fails the build** — before the one-time setup it exits 3 and MSBuild emits `MSB3073` as a *warning*,
which is the same trade as the CLI's own advisory whitelist check: a build broken by a registry
permission is worse than the hang it prevents. Opt out with `-p:AutoApproveOpenness=false` or
`LADDER_AUTO_APPROVE_OPENNESS=0`.

This blunts the sharpest edge of the FI-61 rule. `dotnet test` on `openness-cli.sln` is still a
rebuild, but the rebuild re-approves itself as part of the build, so it no longer silently revokes a
running agent's ability to connect.

**Details that matter if any of it stops working.**

- **`Registry64` explicitly.** A 32-bit host is redirected to `WOW6432Node`, where TIA does not look.
  The entry gets written, the script reports success, and the dialog still appears.
- **`DateModified` is `yyyy'/'MM'/'dd HH:mm:ss.fff` in UTC**, not local time. Found on 2026-08-10 only
  by getting TIA to write an entry for a file this script had already written one for, and reading the
  two side by side:

  ```
  hand-written  2026/08/10 00:49:08.728    <- LastWriteTime, local (UTC+1 here in August)
  TIA itself    2026/08/09 23:49:08.728    <- LastWriteTimeUtc
  ```

  Same instant, same milliseconds, one hour apart. Whether TIA *compares* the field or treats it as
  decoration is still unknown -- but a hand-written entry that disagrees with TIA's own format is a
  variable worth removing. **On a machine at UTC+0 this bug is invisible**, which is worth knowing
  before trusting any of this on a second machine.
- **Entry subkey names are copied from siblings TIA created**, never guessed. `Entry (N)` is what this
  machine has, but that is an observation, not a contract — the script reuses an existing sibling's
  prefix/suffix and takes the next free number.
- **Unknown values are reported, not ignored.** If a real entry ever carries a value the script does
  not write, it warns. That is the one way this could write a dead entry that *looks* approved, and it
  should surface immediately rather than during an unattended run.
- **Approval is per PATH as well as per hash**, so a worktree build still needs its own approval —
  pass `-Exe` with that path, or simply build it there (the post-build target handles it).
- The whitelist accumulates one entry per build (84 for one path here); `-Prune` clears a path's stale
  hashes.

**The security trade, stated rather than buried.** The whitelist is what stops arbitrary programs from
driving TIA Portal. After the one-time grant, anything running as that account can approve that one
executable *name* without prompting — including malware that drops its own `openness-cli.exe` and
approves it. On a single-engineer workstation building its own tool that is a fair trade for
unattended automation; on a shared machine it is not. It is scoped to one key and one exe name, and
`openness-approve-setup.ps1 -Revoke` undoes it. The approval capability is deliberately **not** a
subcommand of `openness-cli.exe`: it stays a separate, auditable act, and the binary that needs
approval cannot be the thing that grants it.

**The watcher is the fallback, and it clicks nothing by default.** A robot that accepts dialogs inside
TIA can accept one you badly wanted to read. It reports what it *would* click until `-Click` is
passed, acts only inside a window whose visible text matches `-TextPattern`, and prints the dialog
text that justified every action. Capture the real wording on your Portal build and language in
observe mode first, then tighten the pattern before enabling clicks.

**What is verified, measured against the live registry on 2026-08-10.**

- **The layout is exactly as assumed.** `20.0\Whitelist\openness-cli.exe` holds **113** entries; every
  one carries `Path`, `DateModified`, `FileHash` and **nothing else**, so there is no unknown value to
  forge. `17.0` and `19.0` exist but hold none. FI-61's recorded hash `PY8nw9ndT2J/...` is still there,
  which cross-checks the note above against the registry itself.
- **Entry naming is `Entry`, then `Entry (1)` ... `Entry (112)`.** The derive-from-siblings logic
  proposed `Entry (113)` -- correct without the shape being hardcoded.
- **Elevation is genuinely required.** An unelevated write returns ACCESS DENIED and exit 3 with the
  pointer to the setup script, so the one-time grant is load-bearing, not ceremony.
- **The elevation guard fires**: run unelevated, `openness-approve-setup.ps1` exits 3, resolves the
  real account name into the command to copy, and touches nothing.
- The post-build target fires with the correct quoted `$(TargetPath)`, skips under the opt-out, and
  warns rather than fails when approval exits 3.

**The privileged half now works, measured 2026-08-10.** The setup was run elevated and reported
`confirmed: Allow SetValue, CreateSubKey, Delete, ReadKey` on all three version keys, read back from
the DACL rather than assumed. The approve script then wrote `Entry (113)` for a worktree binary
**unelevated**, and a re-run was a clean no-op.

### PROVEN 2026-08-10: a hand-written entry IS honoured by a freshly launched Portal

The controlled run, after the UTC fix below:

1. `-Forget` deleted **every** entry naming the target binary, including the one TIA had written
   itself, so no older approval could cover for the test.
2. `openness-approve-build.ps1` wrote a single entry. `-Status` confirmed
   `1 name this exact path; 1 match this file's hash`.
3. `openness-cli list` was pointed at a project **no running Portal had open**, forcing a fresh Portal
   instance that had never seen this binary (PID confirmed, started at 02:27).
4. It **connected and listed the blocks with nobody at the machine, and no dialog was ever raised.**
   `-Status` afterwards still showed exactly one entry -- TIA had not written one of its own, which is
   what rules out "something else granted it".

So the approach works, and the unattended case -- where `openness-cli` launches its own Portal -- is
exactly the case it works in.

**What this does NOT establish.** Two variables changed between the failing attempt and this one: the
`DateModified` format was corrected from local to UTC, *and* the Portal was fresh rather than an hour
older than the entry. Either could explain the earlier failure. Do not write up "a running Portal
caches the whitelist" as fact -- isolating it needs one more run: a deliberately local-time entry
against a fresh Portal.

**Operational note, and it cost two apparently-failed tests.** Both runs produced their complete
output and then **never exited**; the harness eventually killed the wrappers, so both looked like
failures while the log on disk held a full, correct block listing. Suspected the same inherited-handle
trap as the pipe rule below, in its `>` form -- Portal is a child that inherits the redirected stdout,
so the parent can wait long after the command's own work is done. **Judge these runs by their output
file, not by whether the command returned.**

### The failing attempt, for the record (2026-08-10)

Minutes after the first `Entry (113)` was written, an `Openness access (0033:000666)` dialog appeared
naming **that exact worktree binary**, requesting access to Portal **PID 29412**. That entry did not
prevent the prompt — it carried a local-time `DateModified`, and the Portal predated it by an hour.

What is established:

- **The entry is not malformed.** Field-for-field against an entry TIA wrote itself: same three
  values, same `String` kinds (`Path`, `DateModified`, `FileHash`), same `yyyy/MM/dd HH:mm:ss.fff`
  date format. There is no structural difference to blame.
- **The write happened before the request.** `Entry (113)` was written, then `portal-status` was the
  only run of that binary that contacted Portal, and the dialog followed.
- **The Portal in question had been running since 00:04:36**, roughly an hour before the entry existed.

**Both candidate causes are still live**, and the section above shows why: the corrected entry worked
against a fresh Portal, but the local-time format and the Portal's age were fixed in the same step.
The wrong-format explanation is now the more concrete of the two, since the mismatch was measured
directly; "a running Portal caches the whitelist" remains a hypothesis nobody has tested.

One thing the failing attempt did establish: accepting the dialog made TIA write a **duplicate** entry
for a (path, hash) that already had one. TIA did not recognise the hand-written entry as covering that
request — consistent with either cause.

**RETRACTED: "`portal-status` triggers the approval dialog."** That was written here on 2026-08-10 from
sequence alone -- a `portal-status` run was the only thing that had touched Portal before the dialog
appeared, so it got the blame. **Tested directly later the same day and it is false:** a *copy* of
`openness-cli.exe` at a brand-new temp path, with no whitelist entry of any kind, ran `portal-status`
against both long-running Portals, printed their project paths, and **raised no dialog and no
warning**. Enumerating Portal processes evidently does not trip the approval check.

**So what raised that dialog is unexplained.** It named the worktree binary and Portal PID 29412, and
no other run of that binary had touched Portal. Leaving it recorded as unexplained rather than
inventing a second guess -- the first one cost a wrong entry in this file.

### The approval dialog exposes no clickable controls to UI Automation (2026-08-10)

Measured while trying to accept that dialog. It is a **WinForms** window
(`WindowsForms10.Window.8.app.*`), and to UI Automation it publishes **four unnamed `Pane` elements
and not one `Button`** -- so the watcher script matched its text and then found nothing to invoke.
Win32 enumeration shows the truth: three `WindowsForms10.BUTTON.*` child windows, **all with empty
captions**, at relative x=245/323/414 on a 502x298 dialog. The captions exist only as pixels; the
dialog body names them "Yes", "Yes to all", "No" in that order, and the middle button is 86px against
73px for the outer two.

`PrintWindow` does **not** render those child controls, and `SetForegroundWindow` cannot raise the
dialog from a background process, so a screenshot cannot label them either. **Any click-the-dialog
automation must therefore aim by geometry, not by caption or accessibility name** -- which is a real
argument for the registry route over the watcher.

`openness-approve-watch-dialog.ps1` is built on those measurements: Win32 enumeration rather than UI
Automation, buttons identified by left-to-right position cross-checked against "the middle one is the
widest", and a real mouse click at the button's own `GetWindowRect` centre -- no scaling, so it is
DPI-correct and survives the dialog moving. Two refusals are deliberate and both exit **5**: a layout
that is not exactly three buttons with the middle widest, and a target pixel that `WindowFromPoint`
says does not belong to the intended button (the dialog is covered and could not be raised). **A blind
click at a stale coordinate would press whatever is on top, in another application** -- that check is
the difference between automation and a hazard.

**Its click path is UNTESTED against a live dialog.** Verified: it parses, runs, reports correctly
when no dialog is present, and its geometry matches the real dialog measured above. Not verified: an
actual click, because no dialog could be provoked afterwards -- the retraction above removed the cheap
trigger, and a long-running Portal that has already authorised a binary does not ask again. Test it the
next time a dialog appears for real: run it with no `-Click` first and confirm it names the middle
button, then re-run with `-Click`.

**A live finding from that same run, worth repeating as a warning.** The main checkout's Debug binary
was *already* unapproved: **86** entries named its exact path and **none** matched its current hash. A
rebuild had silently revoked it, exactly as FI-61 describes, and nothing in the working tree showed
it. `openness-approve-build.ps1 -Status` is the cheap way to ask.
