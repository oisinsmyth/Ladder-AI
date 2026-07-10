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

## Known constraints
- User must be in the "Siemens TIA Openness" Windows group (log off/on to take effect).
- One Portal instance/session — no parallel Openness sessions.
- Project open is slow; don't kill and retry.
- `PlcBlock.Export()` can return without producing a file, no exception thrown — observed once
  during the ADR-0001 grounding spike (2026-07-11), first of three sequential exports in one
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
