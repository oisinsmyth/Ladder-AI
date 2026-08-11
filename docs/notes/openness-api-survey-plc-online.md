# Openness API survey — PLC, online, download, and the rest of the surface

**Date:** 2026-08-10. **Method:** reflection over the installed
`C:\Program Files\Siemens\Automation\Portal V20\PublicAPI\V20\Siemens.Engineering.dll`
(2,182 exported types). No Portal process was attached, no project opened, no binary
rebuilt — assembly loading only, so this is safe to repeat at any time, including while
Portal work is in flight. Companion to `openness-hmi-api-survey.md`, same technique.

Written because a survey done for one purpose turned up a lot that was not relevant to
that purpose, and rediscovering it later is pure waste.

---

## 1. Why the survey was run — and the one-line answer

The question was whether Openness could carry a **live data stream** for a test harness
(drive inputs, read outputs, compare against expected).

**Openness is a complete deployment API and a zero-capability data API.**

- **Download / online / upload: fully present.** `DownloadProvider.Download(...)` with
  `DownloadOptions = None | Hardware | Software | SoftwareOnlyChanges`;
  `OnlineProvider.GoOnline()/GoOffline()/State`; `StationUploadProvider.StationUpload()`
  (returns an `UploadResult`, which *carries* an `UploadedStation` — it does not return a
  `Device` directly); **`ConfigurationPcInterface.GetAccessibleDevices()`** for network
  browse (*corrected 2026-08-11 — this note first attributed it to
  `ConnectionConfiguration`*); and **70** `Download.Configurations` types answering the
  dialogs TIA would otherwise raise.
- **Live values: absent.** Verified by exhaustive search across all 2,182 types — there
  is no method anywhere that reads or writes a running PLC value.
  `PlcWatchTableEntry` exposes `Address`, `DisplayFormat`, `ModifyIntention`,
  `ModifyTrigger`, `ModifyValue`, `MonitorTrigger`, `Name`; `PlcForceTableEntry` the same
  plus `ForceIntention` / `ForceValue`. Those are the **table definition**. There is no
  `MonitorValue`, no current value, no `Read()`, no `Write()`, no subscribe, and no way
  to start monitoring or apply a force. You can create, import and export watch/force
  *tables*; making them do anything needs the GUI.
- **No RUN/STOP control.** The only `Start`/`Stop`/`Reset` methods in the whole assembly
  are `PlcMasterSecretConfigurator.Reset` and the download *configurations*
  `StartModules` / `StopModules` — answers to a download dialog, not standalone commands.
  Operating mode changes only as a side effect of a download.
- **No standalone memory reset** (`InitializeMemory` exists only as a download answer),
  and **no trace access at all in V20** — see §3.

So any live-data path must come from outside Openness: S7 protocol (snap7/Sharp7),
OPC UA, the CPU web server, or the PLCSIM Simulation Runtime API.

---

## 2. THE POINT OF THIS NOTE — what was found and is NOT currently relevant

Recorded so it is not rediscovered. Namespace type-counts as measured.
Nothing below has been tried; presence in the API is not evidence that it works, is
licensed, or applies to an S7-1200.

### Likely to matter to this project later

| Namespace | Types | Why it could matter here |
|---|---:|---|
| `VersionControl` | 20 | TIA's **Version Control Interface**. This project hand-rolls a git-based IR round trip; VCI is Siemens' own project↔workspace export. Potentially a large overlap with the export/import pipeline — worth understanding before extending ours further. |
| `Library` / `Library.Types` / `Library.MasterCopies` / `Library.Compare` | 18 / 28 / 12 / 8 | Global libraries, **versioned** library types, master copies. Directly relevant to `patterns/` and the library-filling effort — a pattern could be a versioned library type rather than an IR file. |
| `CrossReference` | 11 | Native cross-reference model. We derive our own reference graph from IR (`converter cross-check`, `trace`). Openness's may be authoritative where ours is derived. |
| `Compare` | 4 | Native project comparison. We built `converter diff` for invariance checking. |
| `FingerprintData` | 4 | Native fingerprinting. We built `ir-hash` and `digest --fingerprint`. |
| `Multiuser` | 18 | Multiuser Server. We built a claims/reservation registry because concurrent work on one project is unsafe; this is Siemens' answer to the same problem. (Multiuser Server V17 is installed on this PC.) |
| `SW.Alarm` / `SW.Alarm.TextLists` / `SW.Supervision` | 12 / 7 / 8 | Alarm and supervision (ProDiag) management, including text lists. Relevant to any project with a large alarm layer and a bit-map. |
| `Cax` | 6 | AutomationML import/export — a possible route for IO lists and hardware config arriving from outside as structured data rather than a spreadsheet. |

### Adjacent to the data-stream question, but configuration only

| Item | Types | Note |
|---|---:|---|
| `SW.OpcUa` | 9 | Configure the CPU's **OPC UA server** — server interfaces, node sets. If OPC UA becomes the data path, its *configuration* is automatable even though its *data* is not. |
| `HW.Features.WebserverUserDefinedPages`, `WebDBGenerateOptions`, `SystemWebPagesFeature`, `WebserverUserManagement`, `SimpleWebserverUserManagement` | — | CPU **web server**, including user-defined pages backed by a DB. A possible read path on real hardware, configurable from here. |
| `HW.Features.SysLogConfigurationManager`, `SysLogServerConfiguration` | — | CPU **syslog to an external server** — a push-based diagnostic channel rather than polled. |
| `Security` / `Umac` / `HW.Features.PlcAccessLevelProvider`, `PlcAccessControlConfigurationProvider`, `CertificateManagementConfiguration`, `PlcMasterSecretConfigurator`, `OpcUaUserManagement` | 15 / 32 / — | Protection levels, user management, certificates, master secret. This is where "permit access with PUT/GET" and OPC UA access rights live — needed the moment an external client is enabled. |

### Large but probably not applicable here

| Namespace | Types | Note |
|---|---:|---|
| `HW` | 862 | The hardware model itself — by far the largest namespace. |
| `MC.Drives` + `Dcc` + `DccExceptions` + `DFI` + `Enums` + `SecurityObjects` + `MC.DriveConfiguration` | ~123 | Siemens drive configuration (SINAMICS/DCC). Not applicable where the drives are third-party. |
| `SW.TechnologicalObjects` + `.Motion` | 24 | Technology objects. Not used where there is no motion. |
| `AdvancedProtection`, `CustomIdentity`, `Settings`, `HW.HardwareCatalog`, `HW.Systemdiagnostics.Settings`, `HW.Utilities`, `HW.CustomDataTypes` | 1 / 2 / 4 / 2 / 3 / 5 / 3 | Smaller surfaces, noted for completeness. |

### Already in use by `openness-cli`

`SW` (19), `SW.Blocks` (29), `SW.Blocks.Interface` (3), `SW.Types` (14), `SW.Tags` (13),
`Compiler` (5), `HW.Features.SoftwareContainer`, plus the top-level `Siemens.Engineering`
(89). `SW.Units` (11) and `SW.Loader` (2) are present and unused.

---

## 3. OFF LIMITS — found, recorded, not to be used

- **`Siemens.Engineering.Safety` (15) and `Safety.Download.Configurations` (2).**
  Hard rule 2: the safety program is never read, written, converted, explained or
  referenced. Recorded only so that its existence is not mistaken for permission.
  *(Corrected 2026-08-11.* This note first said the only trace hook in Openness sits under
  the safety validation assistant, and reasoned that hard rule 2 therefore put trace
  automation out of reach. **That describes V21, not V20.** In **V20** there is no trace
  item in Openness at all: reflection over the V20 assembly returns one "Trace" match, an
  unrelated `HW.TraceTriggerEdge` attribute enum, and the V20 Openness help package
  contains zero occurrences of "trace". The safety-validation trace page appears only in
  the V21 documentation. The *conclusion* — no general Openness API for starting or
  uploading a trace — holds for both versions, but on V20 it holds by plain absence, and
  hard rule 2 is not what forecloses it.)*
- **`Siemens.Engineering.SW.ExternalSources` (9).** External source import — the route
  by which SCL/STL enters a project. Hard rule 1 is LAD only. Recorded explicitly so it
  is not later "discovered" as a shortcut for something awkward in LAD.

---

## 4. If a `download` command is built

Everything needed is present. Two notes that would otherwise be learned expensively:

1. **An unanswered configuration that *would prevent the download* throws** —
   `EngineeringTargetInvocationException`, scoped by the manual to *"Unhandled
   configuration that can prevent the download"*. *(Corrected 2026-08-11: the original
   "an unanswered configuration blocks or throws … the caller must answer each one" was
   too strong.)* The manual says outright: *"Certain configurations will only contain an
   information, therefore your action will not be required. Also, you can skip the
   configurations that do not prevent the download."* A second type,
   `EngineeringDelegateInvocationException`, covers your own delegate throwing.
2. **One answer is genuinely destructive, and it is not the one whose name suggests it.**
   *(Corrected 2026-08-11: this note previously attributed a retentive-data wipe to
   `InitializeMemory`, which is unsubstantiated.)*
   - **`DataBlockReinitialization`** is the dangerous one, and worse than first recorded.
     Its `StopPlcAndReInitialize` option: *"All data values, **including retain data**,
     will be initialized with their defined start values during loading. **Set the PLC to
     STOP before loading.**"* It takes retentive data with it *and* stops the CPU. Note it
     is a question the download *raises*, not something the type does on its own.
   - **`InitializeMemory`** — the manual's complete description is *"This datatype is used
     to initialize memory."* That is the entirety of it, identical in V16 and V19. No
     mention of retentive data, wiping or reset. Treat anything stronger as unsourced.
   - Beware a **typo in the manual's DataType column**: it prints
     `DataBlockReinitilization` (missing an `a`) while the enum is
     `DataBlockReinitializationSelections`. Match against the assembly, not the table.

   Others in the same class: `OverwriteSystemData`, `ResetModule`, `StopModules`,
   `DowngradeTargetDevice` / `UpgradeTargetDevice`,
   `AcceptDownloadOfUnencryptedSensitiveData`, `SelectiveDeleteDownload`.

   **Therefore: fail closed.** Refuse a download that raised a configuration the command
   was not explicitly told how to answer, rather than choosing a default. Destructive
   answers get individual opt-in flags. This follows the standing rule that a warning
   which only warns gets skimmed.

3. Useful companions to build at the same time: `accessible-devices`
   (`ConfigurationPcInterface.GetAccessibleDevices()` returns Name / Address /
   MACAddress / DeviceSeries — finds a CPU's real address without guessing) and
   `go-online` / `go-offline`.

Remember that adding any of this rebuilds `openness-cli`, which changes the binary hash
and so needs a fresh TIA Openness approval. Since FI-74 that approval no longer needs a
person at the machine — `tools/openness-approve-build.ps1` writes the whitelist entry
directly and every build self-approves through the `ApproveForOpenness` post-build
target. **The setup is per-machine, not per-repo**, so confirm it has actually been run
here before assuming a rebuild is free: `openness-approve-build.ps1 -Status` answers
that read-only, and also says whether the binary you are about to use is approved right
now.
