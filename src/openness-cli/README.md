# openness-cli

C# CLI — the only component that talks to TIA Portal (via Openness). Built in S0/S1.

## Subcommands (contract per docs/05-architecture.md)

```
openness-cli list    <project>                    # enumerate blocks; F-/safety blocks flagged, never opened
openness-cli export  <project> [--block <name>]   # blocks → SimaticML (refuses safety blocks)
openness-cli import  <scratch-project> <files>    # SimaticML → TIA
openness-cli compile <scratch-project>            # diagnostics; non-zero exit on error
openness-cli xref    <project>                    # cross-reference data
```

Plain-text/JSON output, non-zero exit codes on failure — designed to be driven from a shell.

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
