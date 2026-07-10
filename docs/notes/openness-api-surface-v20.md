# TIA Portal Openness V20 — confirmed API surface

Working reference for the `Siemens.Engineering` object model actually used by `openness-cli`.
Siemens's own documentation for these members is thin/scattered, so rather than guess at
shapes from samples and forum posts, this was obtained by reflecting directly on the
installed DLL:

```powershell
$asm = [Reflection.Assembly]::LoadFrom("C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll")
try { $types = $asm.GetTypes() } catch [System.Reflection.ReflectionTypeLoadException] { $types = $_.Exception.Types | Where-Object { $_ -ne $null } }
# then $types | Where-Object {...} | GetProperties() / GetMethods() / [Enum]::GetNames(...) etc.
```

`GetTypes()` throws `ReflectionTypeLoadException` on the full assembly (some internal types
don't load standalone) — catch it and use `.Exception.Types` filtered for non-null entries to
get everything that *did* load, which is enough to cover the public API.

Everything below is **type-shape only**, confirmed by static reflection on 2026-07-10 against
the V20 DLL at the path above. It tells you what compiles. It does not by itself prove runtime
behavior (e.g. whether `GetService<SoftwareContainer>()` returns non-null for every device
item type, or exactly when `Attach()` vs `new TiaPortal()` is needed). The `list` subcommand
built from this has since been run successfully against a real open project ("JOB9003 - K150",
2026-07-10) — see `docs/notes/stage-gates.md` — which confirms the block-enumeration path
(`Device` → `DeviceItem` → `SoftwareContainer` → `PlcSoftware` → `PlcBlockGroup` recursion)
end-to-end for that project's shape. Constructs not present in that project (nested device
racks, HMI targets, technological objects, safety blocks) are still reflection-confirmed only.

## Connecting

```
Siemens.Engineering.TiaPortal
  TiaPortal(TiaPortalMode mode)                    — constructor; only overload
  static IList<TiaPortalProcess> GetProcesses()
  static TiaPortalProcess GetProcess(...)           — 2 overloads, unused so far
  TiaPortal GetCurrentProcess()
  Projects : ProjectComposition                     — IEnumerable<Project>
  T GetService<T>()                                  — generic instance method
  Dispose()

Siemens.Engineering.TiaPortalMode  (enum)
  WithoutUserInterface
  WithUserInterface

Siemens.Engineering.TiaPortalProcess
  Attach() : TiaPortal
  Dispose()
```

`openness-cli`'s `Connect()`: `GetProcesses()` → attach to `[0]` if any exist, else
`new TiaPortal(TiaPortalMode.WithUserInterface)`. Confirmed live: attaching to an
already-running Portal process and finding an already-open project both work.

## Opening a project

```
Siemens.Engineering.ProjectComposition   (Project GetService<TiaPortal>().Projects)
  Open(FileInfo path) : Project
  Open(FileInfo path, UmacDelegate umacDelegate) : Project
  Open(FileInfo path, UmacDelegate umacDelegate, ProjectOpenMode projectOpenMode) : Project

Siemens.Engineering.Project
  Name : string
  Path : FileInfo
  Devices : DeviceComposition             — IEnumerable<Device>
  DeviceGroups, Subnets, PlantViews, ProjectLibrary, ... (unused so far)
```

`openness-cli`'s `OpenProject()` checks `TiaPortal.Projects` for a project whose `Name` or
`Path.FullName` already matches the given identifier before calling `.Open()` — an
already-open project (the common case: engineer has Portal + project open already) is
reused, never reopened. Confirmed live against "JOB9003 - K150".

## Device tree → PLC software

```
Siemens.Engineering.HW.Device
  Name : string
  DeviceItems : DeviceItemComposition      — IEnumerable<DeviceItem>
  T GetService<T>()

Siemens.Engineering.HW.DeviceItem
  Name : string
  DeviceItems : DeviceItemComposition      — nests; walk recursively
  T GetService<T>()

Siemens.Engineering.HW.Features.SoftwareContainer   (obtained via DeviceItem.GetService<T>())
  Software : Software
  OwnedBy : DeviceItem

Siemens.Engineering.HW.Software   (base)
  ├─ Siemens.Engineering.SW.PlcSoftware       ← cast target for PLC device items
  ├─ Siemens.Engineering.Hmi.HmiSoftware
  └─ Siemens.Engineering.Hmi.HmiTarget
```

Pattern: `device.DeviceItems` (recursive) → `item.GetService<SoftwareContainer>()?.Software as
PlcSoftware`. Confirmed live: `S7-1200 station_1/PLC_1` resolved to a `PlcSoftware` correctly.

## Block groups and blocks

```
Siemens.Engineering.SW.PlcSoftware
  BlockGroup : PlcBlockSystemGroup   (: PlcBlockGroup — the root block container)
  TagTableGroup, TypeGroup, WatchAndForceTableGroup, ... (unused so far)

Siemens.Engineering.SW.Blocks.PlcBlockGroup   (abstract base)
  Blocks : PlcBlockComposition             — IEnumerable<PlcBlock>
  Groups : PlcBlockUserGroupComposition    — IEnumerable<PlcBlockUserGroup>
  Name : string
  ├─ PlcBlockSystemGroup   (PlcSoftware.BlockGroup's concrete type)
  └─ PlcBlockUserGroup     (what .Groups yields — same shape, recurse identically)
```

`WalkBlockGroup` recurses `.Blocks` then `.Groups` uniformly since `PlcBlockUserGroup` and
`PlcBlockSystemGroup` are both just `PlcBlockGroup`. Confirmed live: nested groups "Map IO"
and "Alarms" under `PLC_1` were both walked and their blocks (`MapInputs`/`MapOutputs`,
`AlarmsMain`) listed with the correct `Path`.

## PlcBlock — the metadata `list` reads (and only this)

```
Siemens.Engineering.SW.Blocks.PlcBlock   (abstract base)
  Name : string
  Number : int
  ProgrammingLanguage : ProgrammingLanguage
  IsConsistent, IsKnowHowProtected : bool     (not currently read — flagged for export/compile subcommands later)
  HeaderAuthor, HeaderFamily, HeaderName, HeaderVersion, Namespace, MemoryLayout,
  CreationDate, ModifiedDate, CompileDate, CodeModifiedDate, InterfaceModifiedDate,
  StructureModified, ParameterModified   (not read by `list` — no need to touch them for a listing)

  ├─ CodeBlock (abstract)
  │    ├─ OB
  │    ├─ FB
  │    └─ FC
  └─ DataBlock (abstract)
       ├─ GlobalDB
       ├─ InstanceDB
       └─ ArrayDB
```

`ClassifyBlockType` pattern-matches on these CLR types directly (`OB`/`FB`/`FC`/`DataBlock`)
rather than trying to infer type from `ProgrammingLanguage` — the CLR type is unambiguous;
the enum is not (e.g. `SDB`, `CPU_DB` don't map cleanly to OB/FB/FC/DB). Anything that isn't
one of these five throws `UnrecognizedBlockTypeException` rather than being silently dropped.

## ProgrammingLanguage — the whole safety filter

Every member, confirmed via `[System.Enum]::GetNames(...)`:

```
Undef, STL, LAD, FBD, SCL, DB, GRAPH, CPU_DB, CFC, SFC, FBD_IEC, LAD_IEC, SDB,
S7_PDIAG, RSE, FCP, FLD, ProDiag, ProDiag_OB, Motion_DB, CEM     ← non-safety (21)

F_STL, F_LAD, F_FBD, F_DB, F_LAD_LIB, F_FBD_LIB, F_CALL          ← safety (7, all F_-prefixed)
```

`SafetyClassifier` flags by `F_`-prefix rather than an exact-match list against those 7 —
deliberately, so any future `F_*` variant Siemens adds is caught automatically without a code
change, instead of silently falling through to "not safety" (design philosophy #10 / risk
R-10). Anything neither in the 21-name non-safety whitelist nor `F_`-prefixed throws
`UnrecognizedProgrammingLanguageException` — fail loud, never guess on the safety boundary.

Note: `F_CALL` marks a *call site* to a safety block from standard code, not a safety block
body per se — flagging it as safety anyway is the conservative (over-inclusive) choice, on
the same "never under-flag" principle.

## What's still unverified

Reflection proves the shape; the live run against "JOB9003 - K150" proves the happy path for a
project with only non-safety LAD/DB/FC/OB blocks in nested groups. Still open, deferred to
whenever export/import/compile/xref get built or a project with these constructs is
available:
- A project actually containing an F-block (safety classification's happy path is untested
  live — only reflection-confirmed enum membership + unit tests on the classifier function).
- Nested `DeviceItem`s more than one level deep (racks with plugged modules).
- HMI targets / technological objects reachable from the same device tree.
- `PlcBlock.IsConsistent` / `IsKnowHowProtected` and the header/date properties — reflected,
  never read by any subcommand yet.
