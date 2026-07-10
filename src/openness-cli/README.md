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
- Init: `dotnet new console` here; match the .NET version Openness V20 requires.
- Tests: `dotnet test` (xUnit).
