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
openness-cli xref          <project>                                           # cross-reference data — not built yet
```

Plain-text/JSON output, non-zero exit codes on failure — designed to be driven from a shell.
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

Attaches to a running TIA Portal instance if one exists, otherwise launches one. If attach/launch doesn't respond within `--timeout-connect` (default 180s), this is almost always the first-connect approval dialog waiting inside TIA Portal — the CLI says so and exits non-zero rather than retrying. `--timeout-open` (default 1800s) covers the project-open step, which can legitimately take minutes on a large project.

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
3. Compiles **every** PLC device found in the project (not just one — `compile` requires you to
   disambiguate with `--device` if there's more than one PLC; `sanity-check` checks them all).

Exit code 0 only if every block is consistent and every device compiles clean; non-zero
otherwise, with the inconsistent blocks and per-device compile results listed. A device
compiling clean does **not** imply its blocks are all consistent — confirmed for real,
2026-07-10: `docs/notes/openness-quirks.md` has a live example where every device compiled
`Success` while 15 blocks stayed flagged inconsistent. Run this any time something in the
export/import/compile chain is behaving oddly, before assuming it's a converter/CLI bug.
