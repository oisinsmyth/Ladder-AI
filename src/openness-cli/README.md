# openness-cli

C# CLI — the only component that talks to TIA Portal (via Openness). Built in S0/S1.

## Subcommands (contract per docs/05-architecture.md)

```
openness-cli list          <project> [--tagtables]                            # enumerate blocks (or tag tables, with --tagtables); F-/safety blocks flagged, never opened
openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) [--device <name>] --out <path>   # block/UDT/tag table → SimaticML (refuses safety blocks)
openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...>   # SimaticML → TIA (--type/--tagtable import into the Types/TagTables composition, not Blocks)
openness-cli compile       <project> [--device <name>] [--block <name> | --type <name>]   # diagnostics; non-zero exit on error
openness-cli delete        <project> --block <name> [--device <name>] --yes    # deletes a block (refuses safety; --yes required)
openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName>   # scaffolding: instance DB for an already-existing FB
openness-cli sanity-check  <project>                                           # is this project's Openness state OK? see below
openness-cli portal-status                                                     # read-only Portal-process diagnostic (no project); never attaches/launches/kills — see below
openness-cli hmi           <project> [--screen <name>|*] [--max-items <n>]     # READ-ONLY HMI walk: screens, screen items, per-property dynamizations — see below
openness-cli xref          <project>                                           # cross-reference data — not built yet
```

Plain-text/JSON output, non-zero exit codes on failure — designed to be driven from a shell. The
eleven codes and what each means: "Exit codes" at the end of this file.
`export`/`import`/`compile` live-verified end-to-end against real project data, 2026-07-10 —
see `docs/notes/stage-gates.md` S1.

## Setup notes

- Reference `Siemens.Engineering.dll` from the TIA V20 install by path — never copy Siemens DLLs into the repo.
- Safety filter is present from the first read-only command (Goal 11, never retrofitted).
- **Target framework: `net48`, not `net8.0-windows`.** Confirmed empirically (see `docs/notes/openness-quirks.md`): `Siemens.Engineering.dll` calls a .NET-Framework-only `Assembly.Load` overload internally that doesn't exist on .NET (Core) 5+, so it fails at runtime under `net8.0-windows` even though it builds cleanly. Building requires the .NET Framework 4.8 Developer Pack in addition to a .NET SDK capable of building `net48` (any recent SDK works — the SDK version and the TFM are independent).
- `Siemens.Engineering.dll`'s location is resolved via `$(TiaOpennessDir)` in `Directory.Build.props`: `TIA_OPENNESS_PATH` env var, else the standard `Portal V20\PublicAPI\V20` install path. Override at build time with `TIA_OPENNESS_PATH` if TIA is installed elsewhere. The CLI's own `--tia-install`/`TIA_OPENNESS_PATH` resolution (`TiaInstallLocator`) mirrors this at runtime, for a clear error message if the environment is misconfigured.
- Solution: `dotnet build` / `dotnet test` from this directory (`openness-cli.sln`).
- Confirmed `Siemens.Engineering` object-model shape (from reflecting on the installed DLL, useful when building `export`/`import`/`compile`/`xref`): `docs/notes/openness-api-surface-v20.md`.

## Usage

```
openness-cli list <project> [--json] [--tia-install <dir>] [--timeout-connect <seconds>] [--timeout-open <seconds>]
```

`<project>` is either the **name** of a project already open in the attached TIA Portal instance, or a **path** to a `.apNN` file to open fresh — the already-open case is checked first, so a project you already have open in Portal is never re-opened.

Attaches to a running TIA Portal instance if one exists, otherwise launches one. If attach/launch doesn't respond within `--timeout-connect` (default 180s), there are two known causes and the CLI no longer guesses between them: **(1) this executable is not approved for Openness** — TIA whitelists callers by `(Path, FileHash)`, so *any* rebuild or a different build directory revokes approval, and the refusal is completely silent (no dialog, no exception, no log). Since FI-61 the CLI hashes itself against the whitelist before attaching and warns when it can detect this. **(2)** the first-connect approval dialog genuinely waiting inside Portal. Note `portal-status` **cannot** rule (2) in or out — it never attaches. See `docs/notes/openness-quirks.md`, *"A rebuilt binary is refused SILENTLY"*. `--timeout-open` (default 1800s) covers the project-open step, which can legitimately take minutes on a large project.

**Safe to run concurrently with a human's own separate Portal session, on a different project** (fixed 2026-07-13, live-verified against `SampleProject` + `JOB9002` open at once). If the Portal process this CLI attaches to already has an *unrelated* project open, it never touches it — it launches its own dedicated Portal instance instead and opens the target project there. Two `openness-cli` invocations (or a human + `openness-cli`) working on **different** projects at the same time is fully supported; two Openness sessions on the **same** project concurrently is not (a genuine single-writer-file constraint on TIA's side, not a policy choice here). One residual, non-fixable caveat: the very first attach to an already-running process can still trigger the first-connect approval dialog above, even if that process turns out to belong to someone else's unrelated session — a one-time visual interruption only, no data risk (attaching alone never opens/closes/saves anything).

```
openness-cli export <project> --block <name> [--device <device>] --out <path> [common flags]
```

Refuses (before calling `Export()` at all) if the block classifies as safety. `--device` disambiguates when the same block name/number exists under more than one device (common — this project's own scratch copy has two linked PLC stations). Deletes a pre-existing file at `--out` itself rather than surfacing Siemens's "file already exists" exception; retries once if `Export()` returns without producing a file (a real, observed quirk — `docs/notes/openness-quirks.md`).

`--type <name>` exports a PLC data type (UDT) instead of a block — mutually exclusive with `--block`. Backed by `PlcType.Export()`, the real `SW.Types.PlcType`/`PlcTypeComposition`/`PlcTypeGroup` API (confirmed via `Siemens.Engineering.xml` doc comments to mirror `PlcBlock`/etc. almost exactly). **No safety refusal for types** — `PlcType` genuinely has no `ProgrammingLanguage` property at all (confirmed by its absence from the full reflected property list), so there's nothing for the safety classifier to check. Same `--device` disambiguation as `--block`.

```
openness-cli import <project> --group <device>/<path> <file> [<file> ...] [common flags]
```

`--group` matches the `Path` column `list` prints (e.g. `S7-1200 G2 station_2/JOB9002_PLC/Alarms`) — Import is a method on a specific block group, so there's no generic "wherever it goes" to infer. Same safety check applied to imported content as defense-in-depth.

**Copy the `Path` column value verbatim — don't infer a shortened form.** A device item's own real name can itself contain spaces and an embedded article number as one single literal string (confirmed real, `test-project001`: `S7-1200 station_1/PLC1 6ES7 214-1AG40-0XB0` is two path segments, not three — `PLC1 6ES7 214-1AG40-0XB0` is the whole device item name). Guessing a trimmed-down segment (e.g. just `PLC1`) fails with `No device item found under '...'`, which doesn't point at the mismatch — it looks like the wrong project structure entirely rather than a too-short path.

**This is also "update a block": importing to a group that already has a same-named block overwrites it in place** (`ImportOptions.Override`, always passed) rather than erroring or duplicating — confirmed live, 2026-07-13, against `SampleProject`'s own `TimerSample`: re-imported a version with one changed field, re-exported, and the change was present with no second block created (`docs/notes/openness-quirks.md` has the full story, including the `IsConsistent` gotcha below). There is no separate "update" subcommand — `import` already is one, once you point `--group` at wherever the existing block lives.

`--type` imports the given file(s) as PLC data types (into the `Types` composition at `--group`'s path) instead of blocks (`Blocks`) — needed first when a block's own `import` would otherwise fail with `Data type "<name>" is unknown`. Live-verified, 2026-07-14: importing a sanitized `TypeDOL` UDT this way into `SampleProject`, then retrying a block import that depended on it, made the "unknown data type" error disappear — `docs/notes/stage-gates.md` ("S1 item 26") has the full story.

```
openness-cli compile <project> [--device <device>] [--block <name>] [--json] [common flags]
```

Without `--block`: wraps `ICompilable.Compile()` found via the PLC's own `DeviceItem` (not `PlcSoftware`) — whole-program compile. With `--block <name>`: compiles that one block via its own `ICompilable` service (`PlcBlock.GetService<ICompilable>()`) — same safety refusal and `--device` disambiguation as `export`. **These are not equivalent for clearing `IsConsistent`** after an `import`: device-level compile reports `Success` but does not clear a freshly-imported block's `IsConsistent` flag; block-level compile does. Confirmed live, 2026-07-10 — full story in `docs/notes/openness-quirks.md`. Structured output either way: `State`/`ErrorCount`/`WarningCount` plus each diagnostic message's `State`/`Description`/`Path`. Non-zero exit when `State != Success`.

A block-level compile can fail with a real error (not just an `IsConsistent` artifact) if something it calls hasn't itself been recompiled yet — seen live on `ControlMain` (calls `PlantAutoControl`): failed first, then succeeded cleanly once `PlantAutoControl` had been compiled. Compile callees before callers, or just retry a caller after its callees are clean.

`--type <name>` compiles a PLC data type instead of a block — mutually exclusive with `--block`, same `--device` disambiguation, no safety refusal (see `export --type` above). Live-verified, 2026-07-14: `compile --type MotorIOSet` cleared the same post-import `IsConsistent` quirk blocks hit, in one call.

```
openness-cli delete <project> --block <name> [--device <device>] --yes [common flags]
```

Deletes a block via `PlcBlock.Delete()`. Same block resolution/`--device` disambiguation and safety refusal as `export`/`compile`. **`--yes` is required to actually delete** — without it, the command resolves the block and prints what it *would* delete, then exits `10` (`NotConfirmed`), touching nothing. This is deliberately stricter than every other subcommand here: it's the only genuinely irreversible operation this CLI exposes (no undo, and no way to recreate a deleted block), confirmed live, 2026-07-13, against `SampleProject`'s own leftover, uncompiled `PlantAutoControl` block (left over from an earlier cross-project import test — this both verified the new command and completed that outstanding manual cleanup).

```
openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName> [common flags]
```

Creates a new instance DB via `PlcBlockComposition.CreateInstanceDB(name, isAutoNumbered: true, 0, instanceOfName)` — confirmed real, 2026-07-14 (`Siemens.Engineering.xml` doc comments, TIA Portal V20 `PublicAPI`). Grounding/scaffolding, not logic generation and not tag/hardware invention (CLAUDE.md hard rule 3): the DB number is always auto-assigned by TIA, never a literal passed in here, and the DB's own content is entirely derived from `instanceOfName`'s existing declaration, nothing authored. Exists for a genuine round-trip gap — an FB imported standalone with no calling context has nowhere for its own multi-instance `Static` members (e.g. `TON_TIME` timers) to resolve their storage, surfacing as `"Missing instance DB"` on compile.

Live-verified, 2026-07-14: created `MotorStarter_Instance` (instance of `MotorStarter`, `SampleProject`'s own cross-project-imported FB2) — `compile --block MotorStarter` went from `"Missing instance DB"` on two networks to `STATE: Success, ERRORS: 0`; a subsequent whole-project `compile` was also `STATE: Success, ERRORS: 0`. First `PlantAutoControl` dependency FB proven to round-trip **and compile** in a target project, not just import cleanly.

```
openness-cli sanity-check <project> [--json] [common flags]
```

A health check for "is TIA/this project's Openness state OK" — built after hitting a real
issue it would have caught immediately: a scratch project where several blocks were flagged
`IsConsistent = false` by TIA, which surfaced only as a confusing `Export()` failure well after
the fact, on a block that had nothing to do with what was being worked on. `sanity-check` finds
this directly and fast:

1. Connects and opens the project (proves Portal/Openness reachable and the project loads).
2. Enumerates every block and reads its `IsConsistent` flag — metadata-only, no export attempt,
   so it's cheap and doesn't risk touching anything (same safety handling as `list`: skipped
   for safety-classified blocks).
3. Enumerates every **PLC data type** and reads its `IsConsistent` flag too (**FI-62**, 2026-08-09).
   A UDT is not a `PlcBlock`, so step 2 never saw one: measured live, `sanity-check` reported
   `OVERALL: HEALTHY`, `BLOCKS: 52  INCONSISTENT: 0` and a device compile of
   `Success (errors=0, warnings=0)`, and TIA then refused `export --type UDT_Drum` as inconsistent.
   The device compile stayed green because **nothing in that corpus instantiated the type** — an
   uninstantiated UDT has nothing to make a compile fail. No safety handling is needed here: a
   `PlcType` carries no `ProgrammingLanguage` at all, so there is nothing for the F-prefix
   classifier to check. The `TYPES:` line is printed **always**, including at zero, so a reader can
   see that types were actually examined rather than silently absent.
4. Compiles **every** PLC device found in the project (not just one — `compile` requires you to
   disambiguate with `--device` if there's more than one PLC; `sanity-check` checks them all).

Exit code 0 only if every block is consistent, **every PLC data type is consistent**, and every
device compiles clean; non-zero otherwise, with the inconsistent blocks, the inconsistent types
(each with the `compile --type <name>` line that clears it) and per-device compile results listed. A device
compiling clean does **not** imply its blocks are all consistent — confirmed for real,
2026-07-10: `docs/notes/openness-quirks.md` has a live example where every device compiled
`Success` while 15 blocks stayed flagged inconsistent. Run this any time something in the
export/import/compile chain is behaving oddly, before assuming it's a converter/CLI bug.

```
openness-cli portal-status [--json] [--tia-install <dir>]
```

A **read-only** diagnostic for Portal *process* health — the complement to `sanity-check`, which
checks *project* health. Where `sanity-check` answers "is this project's Openness state OK",
`portal-status` answers "are there stale/leftover `Siemens.Automation.Portal.exe` processes piling
up" — the confirmed correlate of the "second instance won't connect" symptom (CLAUDE.md environment
notes / the 2026-07-14 stability audit), which today is diagnosed by hand via `tasklist` plus
judgment on which to close.

**It never attaches, launches, opens, closes, or kills anything.** Unlike every other subcommand,
it does *not* go through the connect-first flow: attaching risks the first-connect approval dialog,
and launching a fresh Portal is the exact opposite of a pileup diagnostic. It only calls the static
`TiaPortal.GetProcesses()` and reads each process's own `Id`/`ProjectPath`/`Mode`/`AcquisitionTime`
— all readable **without** `Attach()` (`docs/notes/openness-api-surface-v20.md`). It takes **no
`<project>`**; a positional argument is a usage error.

`--timeout-connect` and `--timeout-open` are **accepted but inert** here. `ParsePortalStatus` parses
them into `PortalStatusOptions` for shape-consistency with every other subcommand, but this command
never connects and never opens a project, so neither value can affect anything — passing one is
silently a no-op rather than an error. Documented rather than removed (2026-08-05, audit F-39):
dropping them is a behaviour change to the argument surface, not a docs fix.

Each running process is cross-referenced against the CLI's own `LaunchedInstanceRegistry` and
classified into one of three buckets:

- **in-use** — has a project open. Never a cleanup candidate.
- **self-launched-orphan** — empty *and* marked in the registry as one this tool launched and never
  finished opening a project into. Self-healing: this tool recognises and reuses/replaces these
  itself on its next run, so they need no action.
- **stray-empty** — empty and *not* marked. Could be a human's own empty window, a Portal still
  waiting on the first-connect approval dialog, or genuine stale pileup — never this tool's to close
  automatically, which is why `portal-status` only reports it.

Output includes a short human note inferring the likely cause from the counts (one stray reads as a
probable first-connect-dialog wait or human window; several strays match the pileup symptom). This
is the read-only, safe subset of the parked FI-07 janitor — **killing stays out of scope** (FI-07
needs a `--yes`/dry-run pattern and a safe "idle" definition so a mid-compile Portal is never
touched; there is no is-compiling flag on the Openness side, so the tool cannot tell). Because a
stray is frequently a legitimate human window, this is **not a gate**: it is purely informational
and **always exits 0**, in both table and `--json` form.

The classification and formatting are pure and unit-tested (`PortalStatusTests`); the
`GetProcesses()` enumeration itself is integration-only (needs a live Portal).

## `hmi` — the read-only HMI walk

Every other subcommand is PLC-only *by construction*: each device walk filters
`SoftwareContainer.Software is PlcSoftware`, so an HMI device was previously invisible to this tool
rather than merely unsupported. `hmi` is the one command that matches the other two software types.

**It is strictly read-only.** No `Create`, no `SetAttribute`, no import, no compile — there is no
code path in the walker that writes. HMI *engineering* remains a non-goal (`docs/10-non-goals.md`);
this exists to observe, and it does not open a capability.

The output shape is decided by the API, not by preference — the two HMI families are disjoint object
models with opposite strengths (`docs/notes/openness-hmi-api-survey.md`):

| | Classic (`HmiTarget`) | Unified (`HmiSoftware`) |
|---|---|---|
| Screens | names only | name, number, size, item count |
| Screen items | **impossible** — `Screen` has no `ScreenItems` property at all | full typed tree |
| Dynamizations | n/a | per-property, with tag + PLC tag |

So on a classic device the report says outright that Openness exposes no screen contents. That is
the API's ceiling, not a gap in this walker, and the report states it rather than leaving an empty
list to be misread as "this screen is empty".

Two deliberate cost controls, because a Unified screen item is many slow property reads:

- **Without `--screen`, screens are summarised** (name/size/item count) and no items are read.
  `--screen <name>` reads that screen's items; `--screen *` reads every screen's.
- **`--max-items <n>`** (default 500) caps items read per screen. Truncation is always visible —
  the true `ItemCount` is reported alongside the number actually shown, so a cap can never read as a
  complete listing.

Geometry is read through generic attribute access rather than a cast per concrete item type: there
are ~50 of them across widgets/shapes/controls, they do not share one geometry base, and an item
type a future TIA version adds still reports its position instead of throwing. Every read in the
walker is individually defensive — these are the first calls this project makes into the HMI half of
the API, and one property that throws on one item type must not lose the other 47 screens.

Exits `0` when a project has no HMI device at all: "this project has none" is a legitimate answer to
the question the command asks, not a failure.

### `--schema` — the substitute for a screen XML

**WinCC Unified has no screen export**, so there is no XML or JSON document of a screen to read
anywhere: not in the API, not in the project folder, not in the compiled runtime image (which stores
screens as undocumented binary `.rdf`). Verified four ways in
`docs/notes/openness-hmi-api-survey.md` §8.

What Openness offers instead is self-description, and for authoring it is strictly better than a
sample document:

```
openness-cli hmi <project> --schema            # every screen
openness-cli hmi <project> --schema --screen X # one screen's item types
```

It reports the types `ScreenItems` will accept (`GetCreationInfos`), and for each item type observed,
every attribute (`GetAttributeInfos`) with its **access mode**, **create-relevance**, type and a
sample value — sorted **Mandatory → Relevant → the rest**, which is the order someone writing a
screen needs them in.

`CreateRelevance` (`None | Relevant | Mandatory`) is the reason this beats an exported example: it
states which attributes *must* be supplied at creation. An XML sample only ever shows what one screen
happened to set, never what the next one is required to set.

Schema is a Unified-only concept — classic exposes no screen items, so there is no item schema to
report, and `--schema` says so rather than printing an empty document.

## `hmi-create-screen` — the only HMI command that writes

Everything else under `hmi` is read-only by construction. This one is deliberately a **separate
subcommand** rather than a flag on the walker, so that "walk the HMI" can never become "modify the
HMI" by a mistyped argument.

```
openness-cli hmi-create-screen <project> --name <name> [--width <n>] [--height <n>] [--item <TypeName>]... --yes
```

- **Gated on `--yes`**, like `delete` — the two mutating commands in this tool behave the same way.
  Without it, the command prints what it *would* create and exits `10 = NotConfirmed`. That refusal
  is answered from the arguments alone: **it never contacts Portal**, so a dry run is instant and
  cannot fail on a connect.
- **Creates, never overwrites.** An existing screen of that name is a hard error, not a merge.
- **Refuses an ambiguous device.** More than one Unified HMI is an error rather than a first-match
  guess — writing to the wrong panel is not undone by re-reading.
- **Unified only.** Classic exposes no screen-item model, so there is nothing to create into.
- `--item` takes CLR type names from `hmi --schema`'s own creatable list, repeatable. Item creation
  needs only a name (see `--schema`: nothing is `Mandatory`, only `Name` is `Relevant`), so items are
  created bare and positioned/styled afterwards through attributes.
- **Runs `Validate()` and reports it explicitly**, including "ran, returned no errors and no
  warnings" — because silence and "never ran" would otherwise look identical. Validation errors exit
  `8 = CompileFailed`: a failed gate, not a successful create with commentary. This is the HMI
  analogue of hard rule 4.
- **Saves.** `Project.Save()` is called, and the result says whether it was — an unsaved create lives
  only in the Portal process holding it and vanishes with that process.

**Scope note:** HMI engineering is still a non-goal (`docs/10-non-goals.md`, "revisit only via ADR").
This exists as a capability *probe* — the cheapest way to answer the survey's own open question of
whether `Validate()` does anything — not as an HMI authoring capability.

## `hmi-edit-screen` — modify an existing screen, and attach events

```
openness-cli hmi-edit-screen <project> --name <screen> [--set <Target>.<Attr>=<Value>]... [--event <Target>:<EventType>[=<script>]]... --yes
```

`Target` is an item name, or the literal `Screen` for the screen itself. Same `--yes` gate as
`hmi-create-screen`, and the same no-Portal dry run — which here prints the **whole change list**, so
the refusal is a reviewable plan rather than a count.

**Attribute values are coerced from the API's own schema**, not guessed: the target's
`GetAttributeInfos()` supplies the declared type, and the value is enum-parsed or
`Convert.ChangeType`-d into it (`Width` is a `UInt32` and will not accept a string). The applied
change is reported with the converted value *and its type*, so a silent coercion is visible. A
read-only attribute is **refused**, with a message pointing out that read-only sub-parts (`Font`,
`Padding`, `InputBehavior`, `ToolTipText`) are configured by reaching into the object they return
rather than by assignment.

**Unknown targets, attributes and event names are hard errors**, never no-ops — a skipped edit and a
successful one look identical in the output otherwise.

### Targets NEST — reaching a property OF a dynamization

`Target` was an item name only until 2026-08-09, which meant a dynamization could be *created* and
not *configured*: `--set Rect_1.BackColor.FlashingRate=Fast` was rejected because `Rect_1.BackColor`
is not an item (`openness-hmi-write-api.md` §4m). A target is now a **path**, walked one segment at a
time:

```
Rect_1.BackColor.FlashingRate=Fast
Rect_1.BackColor.ValueConverter.MappingTable.ConditionType=Range
Rect_1.BackColor.ValueConverter.MappingTable.Entries[0].Flashing=True
```

Each step resolves a **dynamization on that property name first**, then a CLR property of that name;
`[n]` indexes into a composition, which is not itself an engineering object and so cannot be a target
on its own. The split is unchanged — last `.` before the first `=` — so the existing grammar is
untouched and a one-segment target still means an item. A step that does not resolve is a hard error
naming the segment and why (`HmiTargetPathNotResolvableException`), never a silent no-op.

### Explicitly-typed values

A value may carry a CLR type tag: `color:#FF0000`, `color:#80FF0000` (ARGB), `int:`, `uint:`,
`long:`, `ulong:`, `double:`, `bool:`, `str:`. Untagged values are coerced from the target's own
schema exactly as before.

The tag exists because some members are declared `object` and the metamodel then says nothing about
what they want — a mapping-table entry's `Value` is the case that forced it. Colours also need it in
spirit: every HMI colour is a `System.Drawing.Color`, which `Convert.ChangeType` cannot produce from
a string, so a declared-`Color` attribute is parsed rather than converted (`#RRGGBB`, `#AARRGGBB`, or
an HTML colour name).

When an attribute is absent from `GetAttributeInfos()` the write falls back to the CLR property and
**says so** in the result (`[via CLR property, not GetAttributeInfos; read back: …]`). The two are
not interchangeable and reporting them identically would describe intent rather than outcome.

## `--map` / `--map-clear` — mapping tables, the second route to flashing

```
openness-cli hmi-edit-screen <project> --name <screen> [--map-clear <Target>.<Property>]... [--map <Target>.<Property>=<EntrySpec>]... --yes
```

Drives `TagDynamization -> ValueConverter -> MappingTable -> Entries` — the value-to-colour-and-flash
mechanism a real alarm display is built from. It hangs off `TagDynamization`, the one dynamization
kind that is not gated on the target property's type, so it reaches properties where
`FlashingDynamization` refuses.

`<EntrySpec>` is `<EntryType>[;<Attr>=<Value>]...`:

| EntryType | Creates via |
|---|---|
| `Simple` / `Range` / `Bitmask` / `Base` | `Entries.Create<T>()` — **no arguments**, unlike every other composition in this API |
| `bits:SingleBit` / `bits:MultiBit` | the non-generic `Entries.Create(BitDynamizationType)`, which returns an `IList` — a whole SET of bitmask entries in one call |

```
--map "Rect_1.BackColor=Range;From=int:1;To=int:5;Value=color:#FF0000;Flashing=True;FlashingRate=Fast"
```

Every entry created is **read back field by field** and reported with the CLR type stored, because
`Value`/`AlternateValue` are declared `object` and nothing in the metamodel says what they accept —
what came back is the only evidence of what went in.

`--map-clear` deletes every entry on that mapping table and is applied **before** `--map`, so a
command carrying both is re-runnable: Openness has no transaction and the composition has no upsert,
so re-running a bare `--map` would stack duplicates.

The property must already carry a **tag binding** (`--bind`): only a `TagDynamization` has a
`ValueConverter`. Asking for a mapping table on any other kind, or on an unbound property, is a hard
error that names the reason.

`openness-cli hmi --screen <name>` reads mapping tables back — `ConditionType`, the formula flag, and
every entry with its stored types. Unified has no screen export, so a fresh-process read of the live
model is the only independent evidence a write took.

### Events are enum-keyed, and the vocabulary is touch-first

Each concrete item type has its own `EventHandlers` composition whose `Create()` takes *that type's*
event enum — `HmiButtonEventType`, `HmiRectangleEventType`, and so on for ~40 types. There is no
shared base, so both reading and writing bind reflectively rather than through a hand-written switch.

**There is no `Click`.** Interactive items expose `Tapped`, `ContextTapped`, `KeyDown`, `KeyUp`
(buttons and toggle switches add `Down`/`Up`; toggles add `StateChanged`); screens expose `Loaded`,
`Unloaded`, `Tapped`, `ContextTapped`; controls expose `Initialized` and `CommandFired`; a touch area
exposes only `GestureDetected`. An invalid name is rejected with the valid list for that type.

Each handler carries a `Script` (`ScriptCode`, `Async`, and a `SyntaxCheck()` this tool does not yet
call). `--event Target:Type` with no `=script` creates an empty handler, which is legitimate.

`openness-cli hmi --screen <name>` now **reads events back** — closing the walker's one previously
misleading gap, where a button reporting no dynamizations read as "not bound" when it meant "not
looked at".

## `hmi-compile` — compiling the HMI device

```
openness-cli hmi-compile <project> [--device <name>] [--json]
```

Same flags and same `CompileResult` shape as `compile`, so diagnostics render identically. It exists
as a separate subcommand because `compile` resolves its target through PLC-only device discovery and
**cannot see an HMI device at all** — the same blind spot the read walker had.

Its purpose is the open question left by `Validate()` being measurably shallow
(`docs/notes/openness-hmi-write-api.md` §4c): if per-object validation does not gate, does a device
compile? Whatever it reports is the answer, including "nothing".

**It deliberately does NOT carry `compile`'s FI-52 consistency backstop.** That check is built on
`PlcBlock.IsConsistent`, and there is no HMI equivalent — no per-screen consistency flag exists to
cross-examine a green result with. So `hmi-compile` reporting Success means only that the compiler
said so, and nothing in this tool can strengthen that claim. Exit codes are `0` / `8 = CompileFailed`
only; there is no `11 = CompileIncomplete` analogue, because there is nothing to detect it with.

### Attaching under Portal pileup

`Connect` prefers a running Portal that already has the requested project open, reading
`TiaPortalProcess.ProjectPath` **without attaching** (free, and touches nothing). It previously took
`GetProcesses()[0]` unconditionally, which is fine with one Portal and harmful with several:
observed live 2026-08-07, two runs against the same project succeeded and a third hung for a full
15-minute connect timeout with five Portal processes running — the "second instance sometimes won't
connect under process pileup" symptom CLAUDE.md records. The hint narrows *which* process is
attached, never *whether* one is, and falls back to the old behaviour when absent or unmatched.

## `export-all` — the disk-vs-controller check's missing half (2026-08-10, FI-70)

`openness-cli export-all <project> --out <dir> [--device <name>] [--tagtables] [--json]`

`converter drift-check` compares `ir/*.ir` against `simatic-ml/*.xml` and **neither side is the
controller**, so a file edited on disk and never imported — or a block changed in TIA and never
exported — was invisible to every automated check this project has. This produces the thing there
was never anything to compare against: the controller's own copy of **every block and PLC data
type** in one directory, ready for `converter drift-check --project <ir-dir> --exports <dir>
--complete`.

Two properties matter more than the export itself, both because of what the directory is *for*:

- **A refusal is reported, never a silent omission.** Safety content is refused (hard rule 2) and
  **named**. The completeness check reads a missing file as "this block is not in the controller",
  so quietly skipping a safety block would convert a correct refusal into a false finding about the
  controller.
- **One failure does not abort the rest.** A partial dump that names its own holes is useful; one
  that stopped at the first problem tells you nothing about the other ninety blocks.

**Basename collisions are refused rather than resolved.** `drift-check` pairs by basename, so a UDT
and a block sharing a name would both write `<name>.xml` — one silently overwriting the other, after
which the comparison runs against the wrong object and yields a false `MATCH` or a false `DRIFTED`
with nothing to indicate which. A disambiguating suffix would break the basename pairing the recipe
depends on, so the collision is reported and both are left for a human.

`--tagtables` is opt-in: a tag table has no `.ir` counterpart in the shape the corpus uses, so
including it by default would manufacture `EXPORT-ONLY` findings that mean nothing.

Exit **0** only when the directory is the whole project; **12** (`ExportIncomplete`) when everything
attempted succeeded but something was refused; **7** when an export failed.

```
$ openness-cli export-all "C:\proj\P.ap20" --out C:\dump
OUT: C:\dump
REFUSED: FB_EStopChain  (classifies as safety content and is never exported by this pipeline (hard rule 2))
SUMMARY: 64 exported, 1 refused, 0 failed
INCOMPLETE: this directory is NOT the whole project. Do not pass it to drift-check --complete
            as-is — that reads a missing file as 'this block is not in the controller', so the
            1 item(s) above would come back as findings about the controller that are really
            findings about this dump.
```

## Exit codes

Every code this CLI can return (`OpennessCli/Program.cs`, `ExitCodes`). Anything driving it from a
shell should branch on these rather than on stderr text.

| Code | Name | Meaning |
|------|------|---------|
| 0 | `Success` | The command did what it was asked. For `compile`, also means `State == Success`; for `sanity-check`, every block consistent and every device compiling clean. `portal-status` always exits 0 — it is informational, never a gate |
| 1 | `UsageError` | Argument parsing failed before anything was touched: unknown subcommand, missing/duplicated flag value, an unexpected positional, mutually-exclusive flags together |
| 2 | `EnvironmentError` | The TIA install couldn't be resolved (`TiaInstallNotFoundException`) — checked up front, before any Portal contact, so this never means a half-done operation. Fix `--tia-install` / `TIA_OPENNESS_PATH` |
| 3 | `ConnectTimeout` | Attach/launch didn't respond within `--timeout-connect` (default 180s). **First suspect the binary, not Portal**: an unapproved (rebuilt, or run from a different directory) executable is refused *silently* — the pre-attach whitelist check warns when it detects this. Otherwise the first-connect approval dialog. Confirm by running a known-approved build against the same project; it connects in under a minute |
| 4 | `ProjectOpenTimeout` | The project-open step exceeded `--timeout-open` (default 1800s). A large project legitimately takes minutes; raise the timeout before assuming a hang |
| 5 | `UnexpectedError` | Catch-all for any exception not classified below. Prints the full inner-exception chain. Treat as "a bug or an unmodelled Openness failure", not as user error — but see the caveat below |
| 6 | `SafetyRefused` | `SafetyContentRefusedException` — the command touched safety-classified content and was refused (hard rule 2). Not retryable, by design |
| 7 | `CommandError` | A recognised domain failure with a clear user-facing cause: `BlockNotFoundException`, `AmbiguousBlockException` (name/number under more than one device — pass `--device`), `DeviceNotFoundException`, `ExportProducedNoFileException` |
| 8 | `CompileFailed` | `compile` ran to completion but returned `State != Success`. The diagnostics are on stdout (`--json` for structured form); the exit code alone doesn't distinguish errors from warnings-only states |
| 9 | `SanityCheckFailed` | `sanity-check` ran to completion and the project is not healthy — at least one inconsistent block, or at least one device failing to compile. Both lists are printed |
| 10 | `NotConfirmed` | `delete` resolved the block and printed what it *would* delete, but `--yes` was absent. **Nothing was deleted.** The only subcommand with a confirmation gate, because it's the only irreversible one |
| 11 | `CompileIncomplete` | A **whole-device** `compile` returned `Success` with no errors, but blocks remain flagged `IsConsistent=false` — so it did not compile them and proved less than it appears to. The unverified blocks are listed on stderr. Distinct from `CompileFailed`: nothing reported an error, the gate simply did not examine everything (FI-52) |
| 12 | `ExportIncomplete` | `export-all` exported everything it attempted, but the directory is **not** the whole project — something was refused (safety content, or a basename collision). Nothing went wrong; the dump is simply not whole, and comparing against it with `drift-check --complete` would produce findings about the dump that read as findings about the controller (FI-70). Same shape as 11 |

### `compile` is not a whole-program gate on its own (FI-52, 2026-08-07)

A block freshly re-imported through Openness's `Import()` is flagged `IsConsistent=false`, and
**device-level `Compile()` reports `Success` without ever clearing that flag.** Measured live on a
real project: `STATE: Success, ERRORS: 0, WARNINGS: 0` while **19 of 34 blocks were uncompiled**,
one of which failed with **8 errors** the moment it was compiled individually.

The quirk itself was known and written down on 2026-07-10 — in `IOpennessGateway.CompileBlock`'s
doc comment and `docs/notes/openness-quirks.md`. What made it bite anyway is that the top-level
instruction still named a bare `compile` as the gate, so the warning lived somewhere the person
about to walk into it had no reason to read. **A trap recorded in the wrong place is not recorded.**

`compile` now fails closed: after a clean whole-device run it enumerates blocks and, if any remain
inconsistent, prints them and exits `11` instead of `0`. Safety blocks never trip it —
`EnumerateBlocks` reports them consistent by construction rather than reading their flag (hard
rule 2), so this cannot become a back door into safety content.

**The exit code is a backstop, not the plan.** Prefer `sanity-check`, which checks consistency
*and* compiles every device, and whose `INCONSISTENT: 0` line is the thing to quote as evidence.
Per-block `--block`/`--type` compiles in dependency order are the other valid form.

**Known gap (2026-08-05, audit F-09): a mis-typed `--group` exits 5, not 7.** The group-resolution
failures in `OpennessGateway` (`FindGroup`/`FindTypeGroup`/`FindTagTableGroup`) throw plain
`InvalidOperationException`, which falls through the classified `catch` filter into the catch-all —
so the commonest user mistake reports as an internal error. What you actually see is this CLI's own
`No device item found under '<path>'` wrapped in the catch-all's inner-exception chain. The fix is a
dedicated exception type added to the filter; until then, read a 5 from `import`/`create-instance-db`
as "check the `--group` path against `list`'s `Path` column verbatim" first.
