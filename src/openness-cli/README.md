# openness-cli

C# CLI — the only component that talks to TIA Portal (via Openness). Built in S0/S1.

## Subcommands (contract per docs/05-architecture.md)

```
openness-cli list          <project>                                           # enumerate blocks; F-/safety blocks flagged, never opened
openness-cli export        <project> --block <name> [--device <name>] --out <path>   # block → SimaticML (refuses safety blocks)
openness-cli import        <project> --group <device>/<path> <files...>        # SimaticML → TIA
openness-cli compile       <project> [--device <name>]                         # diagnostics; non-zero exit on error
openness-cli sanity-check  <project>                                           # is this project's Openness state OK? see below
openness-cli xref          <project>                                           # cross-reference data — not built yet
```

Plain-text/JSON output, non-zero exit codes on failure — designed to be driven from a shell.
`export`/`import`/`compile` live-verified end-to-end against real project data, 2026-07-11 —
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

Attaches to a running TIA Portal instance if one exists, otherwise launches one (never both — single-session rule). If attach/launch doesn't respond within `--timeout-connect` (default 180s), this is almost always the first-connect approval dialog waiting inside TIA Portal — the CLI says so and exits non-zero rather than retrying. `--timeout-open` (default 1800s) covers the project-open step, which can legitimately take minutes on a large project.

```
openness-cli export <project> --block <name> [--device <device>] --out <path> [common flags]
```

Refuses (before calling `Export()` at all) if the block classifies as safety. `--device` disambiguates when the same block name/number exists under more than one device (common — this project's own scratch copy has two linked PLC stations). Deletes a pre-existing file at `--out` itself rather than surfacing Siemens's "file already exists" exception; retries once if `Export()` returns without producing a file (a real, observed quirk — `docs/notes/openness-quirks.md`).

```
openness-cli import <project> --group <device>/<path> <file> [<file> ...] [common flags]
```

`--group` matches the `Path` column `list` prints (e.g. `S7-1200 G2 station_2/JOB9002_PLC/Alarms`) — Import is a method on a specific block group, so there's no generic "wherever it goes" to infer. Same safety check applied to imported content as defense-in-depth.

```
openness-cli compile <project> [--device <device>] [--json] [common flags]
```

Wraps `ICompilable.Compile()` (found via the PLC's own `DeviceItem`, not `PlcSoftware`). Structured output: `State`/`ErrorCount`/`WarningCount` plus each diagnostic message's `State`/`Description`/`Path`. Non-zero exit when `State != Success`.

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
2026-07-11: `docs/notes/openness-quirks.md` has a live example where every device compiled
`Success` while 15 blocks stayed flagged inconsistent. Run this any time something in the
export/import/compile chain is behaving oddly, before assuming it's a converter/CLI bug.
