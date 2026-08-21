# openness-cli

C# CLI — the only component that talks to TIA Portal (via Openness). Built in S0/S1.

## Subcommands (contract per docs/05-architecture.md)

```
openness-cli list          <project> [--tagtables]                            # enumerate blocks (or tag tables, with --tagtables); F-/safety blocks flagged, never opened
openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) [--device <name>] --out <path>   # block/UDT/tag table → SimaticML (refuses safety blocks)
openness-cli export        <project> (--screen <name> | --hmitagtable <name> | --textlist <name>) --out <path>   # CLASSIC HMI content → SimaticML. --tagtable is the PLC one; --hmitagtable is the HMI one — see below
openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...>   # SimaticML → TIA (--type/--tagtable import into the Types/TagTables composition, not Blocks)
openness-cli import        <project> (--screen | --hmitags | --textlists) <files...> [--device <name>]   # CLASSIC HMI content. No --group. 🔴 ImportOptions.Override REPLACES the object, it does not merge, so adding one tag means shipping the whole table — see below
openness-cli import-all    <project> --group <device>/<path> <dirs-or-files...> [--dry-run]   # bulk restore: classifies each file itself, imports tag tables → types → blocks, retries to a fixpoint
openness-cli compile       <project> [--device <name>] [--block <name> | --type <name> | --software | --station | --hardware]   # diagnostics; non-zero exit on error.
                                    # 🔴 **THE DEFAULT SCOPE CHANGED ON 2026-08-13 — A BARE `compile` NOW COMPILES THE PROGRAM.** It used to reach the DeviceItem compilable, which compiles
                                    # *HARDWARE ONLY*: measured, its whole message tree is "Hardware configuration", and it reported Success/errors=0 on a project whose FC8 failed with
                                    # `Tag "DB_Example".DataStore not defined`. The default is now --station (hardware AND program), which is a SUPERSET of the old behaviour, so nothing that
                                    # relied on the hardware half loses it. --software is the program alone; --hardware is the OLD default, still reachable by name. This is the real mechanism
                                    # behind FI-52. Every scope is a DELTA compile; Openness exposes no rebuild-all
openness-cli compile-scopes <project> [--json]                                # READ-ONLY: every object answering GetService<ICompilable>(), which of them are the SAME compiler, and each one's
                                    # attributes/invocations. Compiles nothing. Built because four checks disagreed with a download and the first question was structural: how many compiles are there?
openness-cli compile-all   <project> [--device <name>] [--force]              # compiles every INCONSISTENT type and block (or every one, with --force) in one session — the bulk half of the gate (FI-52)
openness-cli delete        <project> --block <name> [--device <name>] --yes    # deletes a block (refuses safety; --yes required)
openness-cli block-layout  <project> --block <name> [--expect Standard|Optimized]   # READ-ONLY: optimized vs standard block access. Classic S7comm cannot see an OPTIMIZED block at all; the IR path yields Optimized silently — see below
openness-cli block-layout  <project> --block <name> --set Standard|Optimized --yes  # DESTROYS THE BLOCK'S RETAINED DATA on the next download. Sets, saves, re-resolves and READS BACK; a mismatch is exit 15, never a pass
openness-cli download-plan <project> [--device <name>] [--options Software|SoftwareOnlyChanges|Hardware]   # READ-ONLY, DRY-RUN ONLY: what a download WOULD comprise. CANNOT DOWNLOAD — no --yes, no --force, no confirmed form. Granularity is WHOLE-PLC; there is no per-block download — see below
openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName>   # scaffolding: instance DB for an already-existing FB. A FAILED RUN LEAVES THE PROJECT UNCHANGED (2026-08-13): the save happens only after the new DB's number reads back valid, so no "Created ..." line means nothing reached disk — exit 17, nothing to clean up (18 = not saved either, but the open session was left holding it). It used to save in a `finally` and COMMIT the broken block it had just failed to fix — see below
openness-cli sanity-check  <project>                                           # is this project's Openness state OK? see below. Since 2026-08-13 its compile half is the STATION scope
                                    # (hardware + program) and the report NAMES the scope it ran — it used to be the DeviceItem scope, which compiles the hardware only and reported
                                    # `Success (errors=0, warnings=0)` over a program that did not compile. Its verdict now keys on ERRORS, never on State, or the station scope's real warnings
                                    # would mark every healthy project unhealthy
openness-cli portal-status                                                     # read-only Portal-process diagnostic (no project); never attaches/launches/kills — see below
openness-cli hmi           <project> [--screen <name>|*] [--max-items <n>]     # READ-ONLY HMI walk: screens, screen items, per-property dynamizations — see below
openness-cli graphics      <project> [--list] [--inspect <name>] [--export <name> --out <path>] [--import <file>]... [--overwrite]   # the PROJECT-level picture store — see below
openness-cli graphics      <project> --delete <name>... --yes                  # deletes graphics BY LITERAL NAME (no wildcard form exists); unknown name = hard error, nothing deleted — see below
openness-cli hmi-delete-screen <project> --name <name>... --yes                # deletes CLASSIC screens, same contract. `hmi-delete` is Unified-only and cannot see one — see below
openness-cli hmi-delete-tagtable <project> --name <name>... [--device <name>] --yes   # deletes CLASSIC HMI tag tables, same contract again. `hmi-delete --kind TagTables` is Unified-only — see below
openness-cli xref          <project>                                           # cross-reference data — not built yet
```

Plain-text/JSON output, non-zero exit codes on failure — designed to be driven from a shell. The
codes and what each means: the file-wide **`## Exit codes`** table near the end. (Not `### Exit
codes — graphics`, which covers that one command.)

## What's in here

**This file is two documents interleaved**: a command reference, and a log of findings and
incidents recorded against each command — the dated 🔴/⚠️ headings, usually sitting under the
command they were found in. The fence above is the invocation synopsis; the table below says where
each command is *documented*, including the ones the fence does not list.

| command | what you use it for | section |
|---|---|---|
| `list`, `export`, `import` | enumerate; one object out; one ordered same-kind set in | synopsis above, plus `## Usage` |
| `export-all` | every block + type out — the disk-vs-controller check's export half | `export-all` — the disk-vs-controller check's missing half |
| `import-all` | a whole mixed program back, dependency-order retry | `import-all` — putting a whole program back |
| `compile` | per-item or scoped compile. **Default scope is `--station`** | `## Exit codes` → the three-compile-scopes section |
| `compile-all` | the gate's bulk half | `compile-all` — the gate's bulk half |
| `sanity-check` | block **and type** consistency + compile health. Run this first when export/import/compile misbehaves | synopsis above |
| `block-layout` | read or `--set` a block's memory layout. **Destructive** | `block-layout` — optimized vs standard block access |
| `download-plan` | read-only, dry-run only; device-level granularity | `download-plan` — what a download would comprise |
| `download-probe` | the only binary that can transfer a program, allowlist-fenced | the `download-probe` sections under `## Exit codes` |
| `library` | project-library walk; `--export-version`, `--probe-documents` | `library` — the project-library walk |
| `delete`, `create-instance-db`, `portal-status` | delete a block (refuses safety, `--yes`); scaffold an iDB; Portal-process health | synopsis above |
| `hmi` | read-only HMI walk; `--scripts`, `--schema` | `hmi` — the read-only HMI walk |
| `hmi-create-screen` / `hmi-edit-screen` | the two HMI write probes; dynamization, events | `hmi-create-screen` / `hmi-edit-screen` |
| `--map` / `--map-clear` | mapping tables — the second route to flashing | `--map` / `--map-clear` — mapping tables |
| `hmi-compile` | compile the HMI device. **Weaker than `compile` success** | `hmi-compile` — compiling the HMI device |
| `graphics` | project-level picture store | `graphics` — the project-level picture store |
| classic HMI tags / text lists | import-only; `Override` **replaces**, never merges | Classic HMI tags and text lists |
| deletion commands | graphics, classic screens, classic tag tables | Deleting graphics, classic screens and classic tag tables |

**Exit codes that are not failures-as-usual**: `8` = compile errors, `11` = CompileIncomplete,
`12` = ExportIncomplete, `13` = ImportIncomplete, `14` = nothing examined. **Key on errors, never
on state** — the `--station` scope surfaces standing hardware warnings, so a healthy project
legitimately returns `Warning, errors=0`.
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

Attaches to a running TIA Portal instance if one exists, otherwise launches one. If attach/launch doesn't respond within `--timeout-connect` (default 180s), there are two known causes and the CLI no longer guesses between them: **(1) this executable is not approved for Openness** — TIA whitelists callers by `(Path, FileHash)`, so *any* rebuild or a different build directory revokes approval, and the refusal is completely silent (no dialog, no exception, no log). Since FI-61 the CLI hashes itself against the whitelist before attaching and warns when it can detect this. **(2)** the first-connect approval dialog genuinely waiting inside Portal. Note `portal-status` **cannot** rule (2) in or out — it never attaches. See `docs/notes/openness-quirks.md`, *"A rebuilt binary is refused SILENTLY"*. **Both causes are removed by pre-approving the build**: `tools/openness-approve-setup.ps1` once (elevated), after which every build of this project self-approves via the `ApproveForOpenness` post-build target and no dialog is ever raised — see *"Approving a rebuilt binary WITHOUT a human at the machine"* in the same note. `--timeout-open` (default 1800s) covers the project-open step, which can legitimately take minutes on a large project.

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
openness-cli compile <project> [--device <device>] [--block <name> | --type <name> | --software | --station | --hardware] [--json] [common flags]
```

🔴 **BEHAVIOUR CHANGE, 2026-08-13: a bare `compile` now runs the STATION scope (hardware + program). It used to run the DeviceItem scope, which compiles the hardware and nothing else.** This document called that a *"whole-program compile"* from 2026-07-10 until 2026-08-13 and it was wrong — its entire message tree is `Hardware configuration`. Read "🔴 THERE ARE THREE PLC COMPILE SCOPES" below for the measurement.

**What this changes for a caller.** A project whose program does not compile now exits `8` where it used to exit `0`. That is the point, and it is the only intended change: station is a *superset* of the old scope, so the hardware compile still runs and anything that depended on it still gets it. The old scope stays reachable **by name** as `--hardware` — a behaviour obtainable only by asking for nothing is one nobody can reason about. Also expect `STATE: Warning` where you used to see `Success`: the hardware-only compile had nothing to warn about, and the station scope reports the project's real warnings. **The verdict keys on errors, never on state** (unchanged rule), so `errors=0` is still a pass.

Without a scope flag: `--station`. With `--hardware`: wraps `ICompilable.Compile()` found via the PLC's own `DeviceItem` — **the HARDWARE compile, which is what this defaulted to for a year.** With `--block <name>`: compiles that one block via its own `ICompilable` service (`PlcBlock.GetService<ICompilable>()`) — same safety refusal and `--device` disambiguation as `export`. **These are not equivalent for clearing `IsConsistent`** after an `import`: device-level compile reports `Success` but does not clear a freshly-imported block's `IsConsistent` flag; block-level compile does. Confirmed live, 2026-07-10 — full story in `docs/notes/openness-quirks.md`. Structured output either way: `State`/`ErrorCount`/`WarningCount` plus each diagnostic message's `State`/`Description`/`Path`.

**The verdict keys on the ERROR COUNT, never on `State` (2026-08-12).** It used to exit non-zero whenever `State != Success`, and that is wrong for the same reason `compile-all` has keyed on `ErrorCount` since it was written: **a project carrying a pre-existing hardware warning returns a non-`Success` state on a perfectly clean block.** Measured live — a scratch project with a permanent *"Inputs or outputs are used that do not exist in the configured hardware"* warning made **every** per-block compile exit `8` with `errors: 0`, regardless of the block, so a caller branching on the exit code read every clean compile as a failure and would reasonably stop. `State` and the warning count are still **reported** — "compiled with warnings" and "compiled clean" are different facts and the output still says which, adding a `PASSED WITH WARNINGS` line when the state is not `Success`. Only the exit code changed. The count itself is fail-closed: the larger of the compiler's own `ErrorCount` and the number of `Error` messages in its message tree, because those two can disagree and the table already prints a `NOTE` saying the aggregates are unreliable when they do.

**A per-item compile reads the item's consistency flag back, and gates on it (2026-08-12).** A clean compile does **not** imply the block is exportable: measured, a per-block compile reported *"Block was successfully compiled"* with `errors: 0` and the block was **still** flagged `IsConsistent=false`, because a block it referenced did not exist — after which TIA refused to export it (*"Inconsistent blocks and PLC data types (UDT) cannot be exported"*). `sanity-check` caught it; the compile's own output did not. This is the **converse of FI-52**: FI-52 is *a device compile leaves other blocks inconsistent*, this is *a per-block compile leaves its own block inconsistent*. So after compiling, `--block`/`--type` **re-resolves the item from the project and reads `IsConsistent` again** — the same shape `block-layout --set` uses, and for the same reason: an operation that reports success and did not take is indistinguishable from one that did, unless you look afterwards. The result is printed as a `CONSISTENT: yes|NO` line (and a `consistentAfterCompile` field in `--json`, emitted even when null so a consumer can tell "not asked" from "not supported"), and a `NO` — or a read-back that could not be performed at all — exits **11 (`CompileIncomplete`)**, never 0. A block that cannot be exported must not leave a clean-looking exit behind.

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

**A failed `create-instance-db` now leaves the project unchanged (2026-08-13).** It did not before, and
that is worth stating as the guarantee rather than as a bug fix, because it is what a caller may rely
on: if this command does not print a `Created ...` line, **nothing was written to disk** — there is no
half-made block to find, and a retry is safe. `Project.Save()` is the only operation in the command
that reaches disk, and it is now called on exactly one path: after the new DB has been created *and*
its block number has been read back as valid. Every other exit removes the partial block from the open
session and declines to save.

What that replaces, and why it mattered more than an ordinary failure: the previous version wrapped the
whole operation in `finally { SaveProject(); }`. On a live job the sequence ran *create (auto-numbered)
→ DB numbered `0` (FI-63) → the repair sets `db.Number` → **`set_Number` throws** under automatic
numbering → the exception unwinds → **the `finally` saves**.* The command reported a failure it had
already committed, leaving a `DB0` that could neither compile nor export, and three delete-and-retry
cycles did not clear it because each retry recreated it. A repair had turned a recoverable state into a
hard one.

The renumber repair (FI-63 part 2) is therefore **retired, not hardened** — it is measured to throw on
the only project it has ever run against, and "mutate further to rescue a mutation that already went
wrong" is the shape that made the failure destructive. `BlockNumbering.LowestFree`, which chose its
replacement number, is deleted with it. FI-63 **part 1 is kept**: enumerating the composition before
creating is read-only, cannot hurt, and is still the best available hypothesis for why the first
creation after a project open misnumbers.

Two exit codes, and the split is the caller's next move (see the table below): `17` (`ChangeAbandoned`)
means the partial block was removed and there is nothing to clean up; `18` (`RollbackIncomplete`) means
nothing reached disk either, but the block could not be removed from the open session — so if that
session is a person's Portal window, close it **without saving**. The predecessor exception was in no
`ExitCodes.ForException` clause at all, so this condition used to exit `5`, an internal fault.

The sequencing lives in `OpennessCli/Openness/InstanceDbCreation.cs`, behind a seam with no
`Siemens.Engineering` in it, for the reason `BlockNumbering` was split out originally: a rule that can
only be exercised against a live Portal is a rule nobody exercises. `InstanceDbCreationTests` asserts
on the **project** — the block list and the save count — not on the return value or the exit code,
because the exit code was never the defect. It also keeps a transcription of the pre-fix sequence and
runs the same assertions against it, so the guards are demonstrated to detect this defect rather than
merely to pass in its absence.

**Not implemented, and deliberately: the retry.** FI-63's own workaround was to create a throwaway
instance DB and delete it before creating the real one. The rollback above performs the first half
already, so mechanizing it — abandon, then create once more and re-check — is a small step and would
turn most of these failures into successes. It is not taken because it cannot be verified without a
live Portal, and an unverified second write on a path that exists to contain a failed first write is
the same bet that produced the defect. What would settle it: one live run against a scratch project
observing whether the second create numbers correctly.

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
4. Groups those blocks by **(device, block type, number)** and reports every group larger than one —
   **duplicate block numbers** (2026-08-13, see below). The `DUPLICATE NUMBERS:` count is printed
   **always**, including at zero, for the same reason the `TYPES:` line is: a count that appears only
   when it is non-zero cannot be told apart from a check that does not exist.
5. Compiles **every** PLC device found in the project (not just one — `compile` requires you to
   disambiguate with `--device` if there's more than one PLC; `sanity-check` checks them all).

Exit code 0 only if every block is consistent, **every PLC data type is consistent**, **no two blocks
share a number**, and every device compiles clean; non-zero otherwise, with the inconsistent blocks,
the inconsistent types
(each with the `compile --type <name>` line that clears it) and per-device compile results listed. A device
compiling clean does **not** imply its blocks are all consistent — confirmed for real,
2026-07-10: `docs/notes/openness-quirks.md` has a live example where every device compiled
`Success` while 15 blocks stayed flagged inconsistent. Run this any time something in the
export/import/compile chain is behaving oddly, before assuming it's a converter/CLI bug.

#### Duplicate block numbers (2026-08-13)

**`sanity-check` used to report `OVERALL: HEALTHY` on a project containing two blocks with the same
number.** Measured on the device: an artifact named `FC_HarnessCopyLayer` declaring
`<Number>910</Number>` was imported into a project where `FC_MsTick` already held 910. **TIA accepted
it and created two blocks at FC 910.** Every gate in the chain then passed:

| step | result |
|------|--------|
| `import` | exit **0**, printing the file's claim |
| per-block `compile` | exit **0**, *"successfully compiled"*, `CONSISTENT: yes` |
| device compile | `Success, errors=0` |
| `sanity-check` | **`OVERALL: HEALTHY`, `INCONSISTENT: 0`, exit 0** |

Hard rule 4 names `sanity-check` as *the* gate, so this was a hole in the gate itself — the
FI-52/FI-62 family once more, a check reporting a clean result over a question it never asked. With
one extra twist that made it worse than either: `sanity-check` **did** fire once immediately after the
colliding import, but as **`INCONSISTENT: 1`** — the wrong signal, about the wrong property — and **a
single per-block compile erased it**, after which the same check returned `HEALTHY` with the duplicate
still present. *A signal that is only true until you fix something else is not a check.*

The grouping key is **(device, block type, number)**, and all three parts are load-bearing:

- **device**, because block numbers are scoped to a PLC. Two PLCs in one project may each
  legitimately hold an FC 910, and `EnumerateBlocks` walks every device — grouping without it would
  fail a correct project. It is read as the first segment of the block's `Path`, which is the device
  name by construction.
- **block type**, because FC/FB/OB/DB are separate number spaces. An FC 910 and a DB 910 are not a
  collision.
- Groups spanning **different block groups on one device** *are* collisions — one device is one
  number space, and a collision hidden by a folder is the easiest kind to create by accident.

The report names the device, the type, the number, and **every block holding it** — "there is a
duplicate somewhere" is not actionable — plus what does not clear it (nothing this tool runs) and what
does (delete or renumber one of them, then re-import). Safety blocks are included in the scan and
flagged `[SAFETY]`: nothing is read beyond what `list` already prints for one, and excluding them
would put a hole in the check that exists *because* holes in this gate ship defects.

Exit code **19**, deliberately not `9` — see the exit-code table.

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

### `--scripts`: the behaviour is in the handlers (migrated from CLAUDE.md 2026-08-21)

`--scripts` dumps the FULL body of every event handler, not the one-line preview. **Not cosmetic:**
on a real Unified project the behaviour is almost ENTIRELY in these handlers — 274 of them in the
reference project, against ZERO script modules — including how faceplates are actually used, which
is `UI.OpenFaceplateInPopup("<Type>_V_0_0_11", title, {IO:{Tag:"<tag>"}})` from a hot-zone polygon,
**NOT** a container on a screen (the reference project has zero faceplate containers across 49
screens / 1,404 items).

Until this flag existed the preview took the first LINE rather than the first NON-BLANK line, so all
274 reported "there is a script" and displayed nothing — the walker described the skeleton and
omitted the animal, in every read this project had ever done.

**`--schema` is the substitute for a screen XML: WinCC Unified has NO screen export at all**
(verified four ways — API sweep, project folder, compiled runtime, third-party docs;
`docs/notes/openness-hmi-api-survey.md` §8), so there is no document to decode. Instead Openness
self-describes via `GetAttributeInfos`/`GetCreationInfos`, and `--schema` dumps the creatable item
types plus every attribute's access mode and **create-relevance** (`Mandatory`/`Relevant`/`None`) —
which an exported example could never tell you. Classic screens DO export as SimaticML; Unified
screens do not exist as any file.

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

### Dynamization kinds, nested paths, and one forbidden entry type (migrated from CLAUDE.md 2026-08-21)

`hmi-edit-screen <project> --name <name> [--set <Target>.<Attr>=<Value>]... [--event <Target>:<EventType>[=<script>]]... --yes`

`Target` is an item name or the literal `Screen`. Values are coerced from the target's own
`GetAttributeInfos` (a `UInt32` won't take a string) and the applied value is reported WITH its
converted type so a silent coercion is visible. Read-only attributes are refused; unknown targets,
attributes and events are hard errors, never no-ops.

**Events are enum-keyed per item type and touch-first — there is NO `Click`:** `Tapped`,
`ContextTapped`, `KeyDown`, `KeyUp` (+ `Down`/`Up` on buttons); screens use `Loaded`/`Unloaded`;
controls use `Initialized`/`CommandFired`.

**A dynamization kind is gated on the TARGET PROPERTY'S TYPE, not on the kind** (P7, 2026-08-09):
`Tag`/`Script`/`Expression` bind to anything; **`Flashing` only to colour properties**;
**`ResourceList` only to text properties**; `TagParameter` refuses outside a faceplate. A refusal
names no reason, so **vary the target before concluding a kind is unsupported** — this project read
three such refusals as an API limit and was wrong.

`--set` takes nested paths (`Item.Prop.ValueConverter.MappingTable.Entries[0].Flashing`) and `--map`
builds mapping-table entries. **Flashing is NOT limited to colour properties** — that limit belongs
to `FlashingDynamization`; a `TagDynamization` mapping-table entry carries its own `Flashing` flag
and works anywhere a tag binds, including a Boolean (P8).

🔴 **`MappingTableEntrySimple` CRASHES TIA PORTAL** — three isolated occurrences with controls.
Treat it as forbidden; use **`MappingTableEntryRange`**. Bitmask entries come only from
`Create(BitDynamizationType)` and cannot be deleted.

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

## `graphics` — the project-level picture store (2026-08-17)

```
openness-cli graphics <project> [--list]
openness-cli graphics <project> --inspect <name>
openness-cli graphics <project> --export <name> --out <path> [--export-options <name>]
openness-cli graphics <project> --import <file>... [--overwrite]
```

`Project.Graphics` is a `MultiLingualGraphicComposition` and it is **project-scoped, not
device-scoped** — one picture store shared by every HMI device, which is why this command takes no
`--device`. `HmiTarget.GraphicLists` is a different object entirely (a state→picture *mapping*, not
the picture store) and nothing here touches it.

### The typed object is `Name` and nothing else — the document is the only surface

`--inspect` on a real graphic reports **one** attribute (`Name`), **zero** compositions, and a CLR
surface of `Name`, `Parent`, `Delete`, `Export`, `GetAttribute*`/`SetAttribute*`. **The image bytes
are not reachable through the object model at all.** The exported document, by contrast, carries
`DefaultDithering`, `DefaultImageStream`, `DefaultSmoothness` and `Name` — so the SimaticML document
is strictly richer than the API, exactly the relationship SimaticML has to a classic screen.

`MultiLingualGraphicComposition` has **no `Create`** (its members are `Import`, `Find`, `Contains`,
`IndexOf`, `Count`, the indexer and the enumerator). **Import is the only route in.**

### `Export` produces a DOCUMENT plus a sidecar folder holding the real image file

Measured on an existing graphic. `--export X --out X.xml` writes **two** things:

```
X.xml
X files\DefaultImageStream.png      <- a real PNG, 96x96 8-bit RGBA
```

and the document references it by relative path rather than embedding it:

```xml
<Hmi.Globalization.MultiLingualGraphic ID="0">
  <AttributeList>
    <DefaultDithering>false</DefaultDithering>
    <DefaultImageStream external="path">X files\DefaultImageStream.png</DefaultImageStream>
    <DefaultSmoothness>false</DefaultSmoothness>
    <Name>X</Name>
  </AttributeList>
</Hmi.Globalization.MultiLingualGraphic>
```

**A caller that copies only the `.xml` has copied nothing.** The sidecar folder is the picture.

### `Import` wants that document, refuses a bare image, and VALIDATES the payload

Handing it a raw `.png`:

```
Invalid XML encountered while reading Simatic ML file:
Invalid character in the given encoding. Line 1, position 1.
```

Handing it a well-formed document whose sidecar is not a decodable image (prose, or a zero-byte
file):

```
The external file "...\DefaultImageStream.png" is corrupt or invalid.
```

So the store is **not** a blind blob store — it decodes what it is given. And the check is keyed on
the **declared extension**: SVG bytes named `DefaultImageStream.png` are refused as corrupt, while
the same bytes named `.svg` are accepted.

### Nine formats accepted, all round-tripping byte-identical

PNG, BMP, JPG, GIF, ICO, TIFF, WMF, EMF **and SVG** each imported (exit 0), and each exported back
with a **SHA-256-identical** payload. The store preserves the original encoding rather than
normalising to one — the read-back extension follows the source (`.tif` came back as `.tiff`, the
only name change observed).

Because the store round-trips bytes, **a green `--import` is not on its own evidence that the panel
can render the format.** That question belongs to `hmi-compile`, and the compiler does answer it:
a `GraphicView` naming a picture that does not exist fails the HMI compile by name —

```
[Error] ZZ_<screen>: 
[Error] GV_<item>: 
[Error] The graphic for the 'GV_<item>' screen object is invalid.
```

— which is the negative control that makes a clean compile of the same screen mean something.

### Exit codes — `graphics` only

*(Renamed 2026-08-21. This heading was `### Exit codes`, colliding with the file-wide `## Exit
codes` table near the end — the same slug, and the file's own synopsis says "see 'Exit codes' at
the end of this file", which a by-name search resolved to whichever it hit first. The two are not
the same list.)*

`--import` failures surface as `CommandError` (7) carrying the **whole** exception chain verbatim,
because on a capability question the refusal text *is* the result. A `--import` that reports zero
graphics also exits 7 rather than 0: empty is not clean.

## Classic HMI tags and text lists (2026-08-18)

```
openness-cli export <project> --hmitagtable <name> --out <path> [--device <name>]
openness-cli export <project> --textlist    <name> --out <path> [--device <name>]
openness-cli import <project> --hmitags   <files...> [--device <name>]
openness-cli import <project> --textlists  <files...> [--device <name>]
```

`--tagtable` is the **PLC** tag table. `--hmitagtable` is the **classic HMI** one — a different
composition on a different device — and the two are deliberately not merged behind one flag.

### Import is the only route in — again

Reflected on the installed V20 `Siemens.Engineering.dll`:

| type | route in | route out |
|---|---|---|
| `Hmi.Tag.TagTableComposition` | `Import(FileInfo, ImportOptions)`, `CreateFrom(MasterCopy)` | — |
| `Hmi.Tag.TagTable` | — | `Export(FileInfo, ExportOptions)`, `.Tags` |
| `Hmi.Tag.TagComposition` | `Import(FileInfo, ImportOptions)`, `CreateFrom(MasterCopy)` | — |
| `Hmi.TextGraphicList.TextListComposition` | `Import(FileInfo, ImportOptions)` | — |
| `Hmi.TextGraphicList.TextList` | — | `Export(FileInfo, ExportOptions)` |

🔴 **Not one of them has `Create`.** Same shape as `ScreenComposition` and
`MultiLingualGraphicComposition`: **the document is richer than the API.** A classic `TextList`
object exposes `Name`, `Parent`, `Export`, `Delete` and the attribute bag and **nothing about its
entries** — the entries exist for a reader *only* inside the exported document, which is why the
import report prints `members NOT EXPOSED BY THE API` rather than `0`.

Both types live in **`Siemens.Engineering.dll`**, not `Siemens.Engineering.Hmi.dll` — loading the
wrong assembly reports all of them absent.

**Classic only.** The Unified compositions are separate types with a different contract:
`HmiUnified.HmiTags.HmiTagTableComposition` *does* have `Create(string)` (that is what
`hmi-create-tag` uses), and `HmiUnified.TextGraphicList.HmiTextListComposition`'s `Import`/`Export`
take a `DirectoryInfo` + filename rather than a `FileInfo`. A Unified hit is refused **by name**, not
returned as an empty result — the object exists, this route does not reach it.

`--hmitagtable`/`--textlist` are also the only **enumeration** there is: nothing in this CLI lists
classic HMI tag tables, so a name that does not resolve is refused with `PRESENT: <every name>`.
Not decoration — TIA's own default table is called `Default tag table`, spaces included, which is not
a name anyone guesses.

### The tag-table document

`Hmi.Tag.TagTable` → `ObjectList` of `Hmi.Tag.Tag`. A tag's **datatype, connection, address and
acquisition cycle are NOT attributes** — they are `LinkList` entries naming another object:

```xml
<Hmi.Tag.TagTable ID="0">
  <AttributeList>
    <Name>MyTable</Name>
  </AttributeList>
  <ObjectList>
    <Hmi.Tag.Tag ID="1" CompositionName="Tags">
      <AttributeList>
        <AcquisitionTriggerMode>Visible</AcquisitionTriggerMode>
        <AddressAccessMode>Symbolic</AddressAccessMode>
        <Coding>IEEE754Float</Coding>          <!-- Binary for Bool/Int/String -->
        <ConfirmationType>None</ConfirmationType>
        <GmpRelevant>false</GmpRelevant>
        <JobNumber>0</JobNumber>
        <Length>4</Length>                     <!-- bytes: Bool 1, Int 2, Real 4, String 254 -->
        <LinearScaling>false</LinearScaling>
        <LogicalAddress />                     <!-- empty under AddressAccessMode Symbolic -->
        <MandatoryCommenting>false</MandatoryCommenting>
        <Name>MyTag</Name>
        <Persistency>false</Persistency>
        <QualityCode>false</QualityCode>
        <ScalingHmiHigh>100</ScalingHmiHigh>
        <ScalingHmiLow>0</ScalingHmiLow>
        <ScalingPlcHigh>10</ScalingPlcHigh>
        <ScalingPlcLow>0</ScalingPlcLow>
        <StartValue />
        <SubstituteValue />
        <SubstituteValueUsage>None</SubstituteValueUsage>
        <Synchronization>false</Synchronization>
        <UpdateMode>ProjectWide</UpdateMode>
        <UseMultiplexing>false</UseMultiplexing>
      </AttributeList>
      <LinkList>
        <AcquisitionCycle TargetID="@OpenLink"><Name>1 s</Name></AcquisitionCycle>
        <Connection      TargetID="@OpenLink"><Name>HMI_Connection_1</Name></Connection>
        <ControllerTag   TargetID="@OpenLink"><Name>MyDb.MyMember</Name></ControllerTag>
        <DataType        TargetID="@OpenLink"><Name>Real</Name></DataType>
        <HmiDataType     TargetID="@OpenLink"><Name>Real</Name></HmiDataType>
      </LinkList>
      <ObjectList>
        <!-- Comment, DisplayName and TagValue, each a MultilingualText/MultilingualTextItem
             carrying <Culture> and <Text>. Present even when empty. -->
      </ObjectList>
    </Hmi.Tag.Tag>
  </ObjectList>
</Hmi.Tag.TagTable>
```

⚠️ **`DataType` and `HmiDataType` are two different links and they can DISAGREE.** Measured: a tag
whose PLC-side `DataType` is `String` carries an HMI-side `HmiDataType` of `WString`. A generator
that writes one value into both is wrong for that case.

`TargetID="@OpenLink"` means *resolve this by name at import time*. So a generated tag table must
name a connection, an acquisition cycle and a controller tag **that already exist** — none of them is
created by this document.

### The text-list document

`Hmi.TextGraphicList.TextList` → `ObjectList` of `Hmi.TextGraphicList.TextListEntry`. The
value→text mapping is `From`/`To` (a **range**, equal for a single value) plus a `MultilingualText`:

```xml
<Hmi.TextGraphicList.TextList ID="0">
  <AttributeList>
    <ListRange>Decimal</ListRange>            <!-- Decimal | Bit | BitNumber -->
    <Name>MyList</Name>
  </AttributeList>
  <ObjectList>
    <MultilingualText ID="1" CompositionName="Comment"> ... </MultilingualText>
    <Hmi.TextGraphicList.TextListEntry ID="3" CompositionName="Entries">
      <AttributeList>
        <DefaultEntry>false</DefaultEntry>
        <EntryType>SingleValue</EntryType>
        <From>0</From>
        <To>0</To>
      </AttributeList>
      <ObjectList>
        <MultilingualText ID="4" CompositionName="Text">
          <ObjectList>
            <MultilingualTextItem ID="5" CompositionName="Items">
              <AttributeList>
                <Culture>en-US</Culture>
                <Text>&lt;body&gt;&lt;p&gt;the displayed text&lt;/p&gt;&lt;/body&gt;</Text>
              </AttributeList>
            </MultilingualTextItem>
          </ObjectList>
        </MultilingualText>
      </ObjectList>
    </Hmi.TextGraphicList.TextListEntry>
  </ObjectList>
</Hmi.TextGraphicList.TextList>
```

The entry text is **escaped HTML**, not plain text: `<body><p>…</p></body>`.

### 🔴 `ImportOptions.Override` REPLACES the object. It does not merge.

Measured 2026-08-18 against a real project, with **disjoint** contents so the answer cannot be read
two ways — a merge and a replace give different counts, not the same count by coincidence:

| step | sent | tag tables | tags in device | read back from the project |
|---|---|---|---|---|
| create | table `P`, one tag `A` | 3 → **4** | 67 → **68** | — |
| override | table `P`, one tag `B` | 4 → **4** | 68 → **68** | **1 tag, and it is `B`** |

`A` is gone. The same experiment on a text list: a list holding entry `0` was overridden by a
document holding only entry `1`, and the read-back holds **one** entry, `From/To = 1`.

**Consequence: adding one tag means shipping the whole table.** There is no additive path — read
the existing table out with `export --hmitagtable`, edit the document, send all of it back.

Both documents round-trip **identical modulo object IDs** (TIA reassigns `ID=` on import, exactly as
it does for `Part`/`Wire`/`Access` UIds in LAD): export → import → export produced a byte-identical
body once IDs were normalised, for the tag table and for the text list alike.

### Reporting: counts read back from the project, never from `Import()`

Both imports print the denominator on both sides:

```
DEVICE:  <device>/<hmi runtime>
KIND:    HMI tag table
FILES:   1
           C:\...\MyTable.xml

HMI tag tables in device   BEFORE 3  ->  AFTER 4   (+1)
tags in device        BEFORE 67  ->  AFTER 68   (+1)

RETURNED BY Import(): 1
  PRESENT MyTable
PRESENT AFTER a re-read: 4
    ...
```

The **member** count is the line that distinguishes replace from merge; the container count cannot,
because an override leaves it unchanged. For a text list the member line reads
`NOT EXPOSED BY THE API` — a `0` there would be a false claim about the project instead of a true one
about the API.

**Empty is not clean, in two ways, and both exit 13** (`ImportIncomplete`): `Import()` returning
nothing, and `Import()` naming an object that a re-read does not find. A count comparison alone is
*not* used as the gate — re-importing an existing table legitimately leaves every count unchanged,
and that is a real success.

### A wrong-kind document is refused, and the refusal names both sides

Handing a text-list document to `--hmitags` (exit 7, nothing written):

```
HMI tag table import failed for 'MyList.xml':
  Siemens.Engineering.EngineeringTargetInvocationException: Error when calling method 'Import'
  of type 'Siemens.Engineering.Hmi.Tag.TagTableComposition'.
  ---> ... : Import action was invoked on navigator 'TagTables' which is out of context for the
  Simatic ML file containing 'Siemens.Engineering.Hmi.TextGraphicList.TextList' root object.
```

The **whole** chain is carried verbatim for the graphics path's reason, and it is not decoration
here: the outer message says only that `Import` threw. The inner one is the entire answer.

### What is NOT built

- **No `import-all` equivalent.** These are separate flags, not a mixed-kind bulk restore.
- **Tag tables CAN be deleted** — `hmi-delete-tagtable`, added 2026-08-20; see *Deleting graphics,
  classic screens and classic tag tables* below. **Text lists still cannot:** `TextList.Delete()`
  exists and nothing here calls it.
- **Individual tags are not wired.** `TagComposition.Import` takes a single-tag document into ONE
  named table; only the table-level route is exposed, because that is the one the export produces.
- **The Unified refusal is unit-tested, not measured** — the project used for the live run carries
  no Unified device.

## Deleting graphics, classic screens and classic tag tables (2026-08-17, tag tables 2026-08-20)

```
openness-cli graphics            <project> --delete <name>... --yes
openness-cli hmi-delete-screen   <project> --name <name>... [--device <name>] --yes
openness-cli hmi-delete-tagtable <project> --name <name>... [--device <name>] --yes
```

`hmi-delete-screen` is separate from `hmi-delete` for the same reason `hmi-compile` is separate from
`compile`: `hmi-delete` is the metamodel command over `HmiSoftware` (Unified) compositions and
**cannot see a classic screen at all**. `hmi-delete-tagtable` is separate for exactly that reason
too — `hmi-delete --kind TagTables` resolves through `FindSingleUnifiedSoftware`, so it is blind to a
classic device's `TagFolder`.

Everything below applies to all three. Three notes specific to the tag-table verb:

- The walk over `HmiTarget.TagFolder` is **recursive** (`TagFolder.Folders` is a
  `TagUserFolderComposition`), matching `export --hmitagtable`. A flat walk would report "not found"
  for a table plainly visible in the project tree.
- A table on a **Unified** device is refused **by name** (`HmiClassicOnlyObjectException`), not
  flattened into "not found": the object exists, this route does not reach it, and those are two
  different corrections.
- **A deleted classic tag table cannot be re-created through the API.** `TagTableComposition` has no
  `Create` — a SimaticML import is the only route back, so `export --hmitagtable` first if the
  content might be wanted again.

### 🔴 There is no wildcard, and that is deliberate

`--delete` / `--name` take **literal names only**, repeated once per object. There is no `--prefix`,
`--pattern`, `--glob` or `--all`, and a name containing `*` is carried through as a literal that
simply fails to resolve. A pattern evaluated at delete time is one typo away from taking real
content with it, and the caller can always enumerate first and pass the names it meant. A test
asserts the absence of every pattern-style flag, so adding one later has to delete a statement that
it does not exist.

### Every name resolves BEFORE anything is deleted

A batch containing one unresolvable name deletes **nothing** — measured against a real project:

```
$ ... --delete ZzProbePng --delete ZzDoesNotExistAnywhere --yes
No graphic named 'ZzDoesNotExistAnywhere' in Project.Graphics. Present: <the 31 that are>
exit 7      graphics before: 31      graphics after: 31
```

A half-applied delete is worse than none, and an unknown name is the likeliest mistake a caller
makes. The message names what **is** present, so a typo is correctable without a second command. A
name that does not exist is a **hard error, never a silent no-op**.

### The confirm fence, and the read-back

`--yes` is required. Without it the plan lists **every** name and Portal is **never contacted**
(exit 10) — measured at ~0.1 s per run, where a real attach costs at least 0.7 s. A unit test drives
the real entry point with a counting gateway and asserts `OpenProjectCalls == 0`, because a refusal
that still opened the project would satisfy any test that only checked the exit code.

After the save, the composition is **re-read** and any survivor raises
`DeleteDidNotTakeEffectException`. That is classified `UnexpectedError` (5), **not** `CommandError`:
nothing the caller typed can fix a delete that reported success and did not happen. It is the same
shape as `block-layout --set`'s silent no-op, which is exit 15 rather than a success with a note for
exactly this reason. A run that deletes zero objects never exits 0.

### Order: referencing objects first

**Delete screens before the graphics they reference.** A `GraphicView` left pointing at a deleted
picture fails the HMI compile with `The graphic for the '<item>' screen object is invalid` — the
same error this file documents as the graphics negative control.

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

**The verdict keys on ERRORS, never on `State` (2026-08-12)** — the same rule as `compile`, and
literally the same code path: both call `Program.EffectiveErrorCount`, the fail-closed
`max(ErrorCount, Error messages in the message tree)`. `compile` earned that rule the expensive way (a
project-wide hardware warning made every clean per-block compile exit 8 with `errors: 0`);
`hmi-compile` carried the identical `State != Success` defect and was fixed by ruling rather than by
being bitten. **Warnings are not swallowed**: the state and the warning count still print, and a
non-`Success` state with zero errors prints a `PASSED WITH WARNINGS:` line saying so explicitly. Only
the exit code changed.

### `hmi-compile` success is WEAKER than `compile` success, and the API is why

The two commands take the same flags and print the same `CompileResult` table. **They do not carry the
same guarantee, and the difference cannot be closed from this side.** Read this before treating a
green `hmi-compile` the way you would treat a green per-block `compile`:

| | `compile --block/--type` | `hmi-compile` |
|---|---|---|
| Errors reported by the compiler | gated (exit 8) | gated (exit 8) |
| Item still inconsistent afterwards | gated (exit 11) — read back from the item itself | **no such check exists** |
| What a `0` means | the compiler reported no errors **and** the item re-read as consistent, so TIA will export it | the compiler reported no errors. That is all |

`compile`'s second row is built on `PlcBlock.IsConsistent`, and **there is no HMI equivalent to read.**
Measured against the V20 API surface (2026-08-12, `PublicAPI/V20/Siemens.Engineering.xml` +
`Siemens.Engineering.Hmi.xml`): `IsConsistent` exists on exactly **four** types — `PlcBlock`,
`PlcType`, `PlcForceTable`, `PlcWatchTable`, all `Siemens.Engineering.SW.*` — and on **nothing** in
`Siemens.Engineering.Hmi.*` (Classic) or `Siemens.Engineering.HmiUnified.*`. The nearest neighbour on
the Unified side is `HmiValidationResult` (Errors/Warnings), which is the `Validate()` surface already
measured shallow — it accepts a zero-width screen. So there is no per-screen or per-device flag to
cross-examine a green result with, and this tool cannot manufacture one.

Consequently `hmi-compile` **does not report a consistency field it cannot fill**: no `CONSISTENT:`
line, and `consistentAfterCompile` is `null` in `--json` — the same value a whole-device PLC compile
carries, meaning *the question was not asked*. An always-null field printed as though a check had run
would read to a consumer as "checked, nothing wrong", which would be false. Exit codes are `0` /
`8 = CompileFailed` only; there is no `11 = CompileIncomplete` analogue **because there is nothing to
detect it with**, not because the case cannot arise.

### Attaching under Portal pileup

`Connect` prefers a running Portal that already has the requested project open, reading
`TiaPortalProcess.ProjectPath` **without attaching** (free, and touches nothing). It previously took
`GetProcesses()[0]` unconditionally, which is fine with one Portal and harmful with several:
observed live 2026-08-07, two runs against the same project succeeded and a third hung for a full
15-minute connect timeout with five Portal processes running — the "second instance sometimes won't
connect under process pileup" symptom CLAUDE.md records. The hint narrows *which* process is
attached, never *whether* one is, and falls back to the old behaviour when absent or unmatched.

## `export-all` — the disk-vs-controller check's missing half (2026-08-10, FI-70)

### Safety is named, not omitted (migrated from CLAUDE.md 2026-08-21)

`export-all <project> --out <dir> [--device <name>] [--tagtables] [--json]`

Every block + PLC data type to one dir, so that
`converter drift-check --exports <dir> --complete` can compare **the IR ON DISK against WHAT IS
ACTUALLY IN THE CONTROLLER** — the comparison neither `drift-check` alone nor a re-export proof
could make.

**Safety content is REFUSED AND NAMED, never silently omitted** — an absent file reads downstream as
"not in the controller", which would turn a refusal into a false finding. A basename collision is
refused too.

**Exit 12 = ExportIncomplete:** everything attempted worked, the dump is not whole.

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

## `import-all` — putting a whole program back (2026-08-11)

### Why the ordering is a fixpoint, not a sort (migrated from CLAUDE.md 2026-08-21)

`import` takes files as ONE kind, in the order given, and stops at the first failure — right for the
2–3 files a change touches, useless for putting a whole program back. **A program is a MIXED set in
a DEPENDENCY ORDER NOT DERIVABLE FROM FILENAMES**, and getting it wrong yields
`Data type "X" is unknown` on a file that was fine and would have imported ten seconds later.

So:

- **Kind is READ FROM each file's SimaticML root element** — no `--type`/`--tagtable`, no three
  invocations.
- Order is tag tables → types → blocks (iDBs last).
- Then it **RETRIES failures until a pass imports nothing new.** Any workable order converges, so
  the sort is an optimisation and **the fixpoint is the correctness argument.**
- Unreadable, unclassifiable, or duplicate-basename files are **REJECTIONS WITH REASONS, never
  skips.**
- Saves ONCE at the end (`Save()` per file × retries would dominate).
- `--dry-run` prints the plan without contacting Portal.

**Exit 13 = ImportIncomplete:** the project does NOT contain everything supplied — including a file
never attempted. **NOT a compile gate and doesn't pretend to be:** everything imported is flagged
inconsistent, so `sanity-check` still follows (hard rule 4).

### 🔴 IT COULD NOT READ A SINGLE FILE TIA PRODUCED, FROM THE DAY IT WAS WRITTEN UNTIL 2026-08-13

Measured, against a complete restore point of the scratch project:

```
export-all   ->  126 exported, 0 refused, 0 failed, COMPLETE
import-all   ->  0 file(s) would be imported, 126 rejected     (exit 13)
```

**Every restore point taken that week was unusable, and nobody found out — because the restore path
is the one thing you only exercise when something has already gone wrong.**

**The mechanism was a deny-list where a search belonged.** `ReadRootElement` walked elements,
skipped the two names it knew — `Document` and `Engineering` — and returned whatever came next. A
real TIA export carries a third header element:

```xml
<Document>
  <Engineering version="V20" />
  <DocumentInfo>…</DocumentInfo>      <!-- never on the skip list -->
  <SW.Blocks.FC ID="0">…
```

so it returned `"DocumentInfo"`, which classifies as nothing, and every file was rejected by name
with a reason. *(Grepping the source for `DocumentInfo` finds nothing, which is the point: the code
never named the element it was tripping over.)*

**Single-file `import` was unaffected all week and that is what disguised it** — it never
classifies, because the caller states the kind with `--type`/`--tagtable`. So the same files went in
one at a time, all day, and the failure looked like bad files rather than a bad reader.

**Why the tests passed: the fixtures were the problem.** They hand-wrote
`<Document><Engineering/><SW.Blocks.FB/></Document>` — a document TIA does not produce. The fixture
agreed with the code about a shape neither had checked against reality, so the pair was
self-consistent and wrong. *A test whose input was written to match the implementation is not a test
of anything.*

**The fix is a LOCATE, not a longer skip list** — `ReadObjectElement` returns the first element
whose `LocalName` starts with `SW.`. A deny-list is only ever as complete as the documents someone
happened to look at; searching for what you want cannot be broken by a header element nobody has
seen yet. This is also exactly what `src/converter` has always done
(`BlockSourceParser`: `Descendants().FirstOrDefault(e => e.Name.LocalName.StartsWith("SW.Blocks."))`),
and the converter consumes these same exports without trouble — one rule, and the one already proven
against real files. Document order at any depth is safe because the outer object always precedes its
own nested `SW.*` children (a real FC export has `SW.Blocks.FC` at line 54 and
`SW.Blocks.CompileUnit` at line 95); `LocalName` rather than `Name` so a namespace prefix cannot hide
it. A file with no `SW.*` object is still a rejection, and the message now **names the top-level
elements it did see** — "no SW.* element" over a 3 MB file is not diagnosable on its own.

**Proof, end to end, with real output:** `export-all` → 126 files → `import-all --dry-run` over that
exact directory → **126 planned, 0 rejected**. `ImportAllRealExportTests` holds it, reading the
committed `simatic-ml/test-project001/` corpus — 23 genuine de-identified TIA exports, every one
carrying `DocumentInfo`. **Negative-tested:** reinstating the deny-list turns 3 of those tests red,
while all 24 pre-existing hand-fixture tests stay green — which is the measurement that the old
fixtures could never have caught this.

**Same class elsewhere: checked, and clean.** `src/converter`'s `BlockSourceParser`, `DbSourceParser`
and `Program`'s kind detection all locate by name (`Descendants(…StartsWith("SW."))`); `converter
compare` gates on `Root.DescendantsAndSelf().Any(e => LocalName.StartsWith("SW."))`; `drift-check`
reads through the same parsers and is exercised against the committed real-export corpus. The one
positional read in the converter — `CompareRunner`'s `before.Elements().FirstOrDefault()` — operates
on a `<Wire>`'s children to find its producer end, where position *is* the schema. **`import-all` was
the only consumer of these documents that guessed.**

`openness-cli import-all <project> --group <device>/<path> <dirs-or-files...> [--json] [--dry-run]`

`import` is built for the two or three files a change touches: it takes them as **one** kind
(blocks, or `--type`, or `--tagtable`), in the order given, and stops at the first failure. A whole
program is none of those things. It is a mixed set — tag tables, UDTs that contain other UDTs, FBs,
the instance DBs of those FBs — in a **dependency order that is not derivable from the filenames**,
and getting that order wrong does not produce a diagnosable error. It produces `Data type "X" is
unknown` on a file that was perfectly good and would have imported ten seconds later.

So this command decides what it can from the files themselves and brute-forces the rest:

- **Classification is read from the file, not declared on the command line.** The SimaticML root
  element (`SW.Blocks.*` / `SW.Types.*` / `SW.Tags.*`) says which composition a file belongs to, so
  one invocation handles a mixed directory instead of three invocations in an order you had to know.
- **Ordering is an optimisation; the fixpoint is the correctness argument.** Tag tables, then types,
  then blocks (a block may use a UDT; a UDT never uses a block), and instance DBs last within the
  block phase. Then it retries every failure until a pass imports nothing new. **Any order that can
  work converges**, because a pass that imports at least one file unblocks strictly more than the
  last — which is why no dependency graph is computed here, and why being wrong about the sort costs
  a pass rather than a restore.
- **Nothing goes missing quietly.** A file that cannot be read, cannot be classified, or shares a
  basename with another file in the same run is a **rejection with a reason**, never a skip. The
  failure mode of a restore is a project that comes back *looking* whole: it opens, it lists, and a
  device compile can pass on it (FI-52).
- **Safety content stops the command.** A `SafetyContentRefusedException` is never retried and never
  demoted to a line at the bottom of a mostly-successful summary (hard rule 2).
- **One save, at the end, in a `finally`.** `Project.Save()` is seconds on a real project; saving per
  file — times the retry count — would dominate the run. The `finally` keeps the property the
  per-call save had: a mid-run abort still keeps whatever went in.

`--dry-run` prints the classification and the order it would use and **never contacts Portal** —
same reasoning as the unconfirmed HMI writes: answering from the arguments alone must not pay for,
or fail on, a connect.

**This is not a compile gate and does not pretend to be one.** Every block that goes in through
`Import()` is flagged inconsistent; clearing that is `sanity-check`'s job (hard rule 4). Exit **0**
only when every supplied file is in the project, **13** (`ImportIncomplete`) otherwise — including
when the shortfall is a file that was never *attempted*.

```
$ openness-cli import-all "C:\proj\P.ap20" --group "S7-1200 station_1/PLC_1" C:\ir
GROUP: S7-1200 station_1/PLC_1
RETRIED:  MotorIOSet  (imported on pass 2)
SUMMARY: 101 imported, 0 failed, 0 rejected, in 4 pass(es)
COMPLETE: every file supplied is now in the project. This is NOT a compile gate — every
          imported block is flagged inconsistent until compiled. Run:
          openness-cli sanity-check <project>
```

## `compile-all` — the gate's bulk half (2026-08-11)

### Empty is not clean, and errors are retried within a run (migrated from CLAUDE.md 2026-08-21)

**An item that compiled WITH ERRORS is still flagged CONSISTENT, and errors do not survive the
process** — so the run right after a failed one finds an EMPTY work set, compiles nothing, and is
the run most likely to be believed. It reports **`NOTHING EXAMINED` and exits 14** rather than
claiming a pass.

**`--force` compiles EVERY type and block** instead of only the inconsistent ones — the
re-verification path after a restore, and the only way to re-examine an item whose errors have been
forgotten.

**Errors are retried within a run.** `Block "X" that is accessed has not been compiled` is an
ORDERING artefact that clears on a re-run, and a loop watching only consistency stopped one pass
short and reported 15 non-failures on the first live restore.

Reports ERRORS and STILL-INCONSISTENT as **SEPARATE counts** (compiled-and-wrong vs never-examined;
the second is the one that looks like a pass), and keys its verdict on **ErrorCount, NEVER on
State** — a project with pre-existing hardware warnings returns non-Success on a clean block.

Exit **8** = errors, **11** = something left inconsistent, **14** = nothing examined.

`openness-cli compile-all <project> [--device <name>] [--json] [--force]`

FI-52 established that a whole-device compile **does not clear** the `IsConsistent=false` flag a
freshly-imported block carries. The consequence nobody had to face until a whole program was
restored at once: the only thing that clears it is a per-block or per-type compile, and after a bulk
import that is ninety-odd of them — which, one CLI invocation each, is ninety-odd Portal attaches.
This does them in one session, types first, retrying while progress is being made (compiling one
item can clear another; the order that happens in is not worth deriving when a second pass settles
it).

**Errors and inconsistency are counted separately, and both are reported.** They are different
failures — *compiled and wrong* versus *never examined* — and collapsing them into one count would
let the second hide inside the first. The second is the one that matters most, because a block the
gate did not examine is indistinguishable from one that passed.

**The verdict keys on `ErrorCount`, never on `State`.** A project carrying pre-existing hardware
warnings returns a non-`Success` state on a perfectly clean block, so a state-based verdict would
call every block on such a project a failure.

**An item that compiled WITH ERRORS is still flagged CONSISTENT.** Errors do not survive the
process; consistency does. So the invocation immediately after a failed one finds an empty work set,
compiles nothing, and — before this was fixed — reported *"every item compiled without errors"*. That
is the moment the report is most likely to be believed and least entitled to be. It now says
`NOTHING EXAMINED` and exits **14** (`NothingExamined`), on the same principle as the converter's
"compared against nothing" exit (FI-44): **empty is not clean**.

`--force` is the answer when you need the question actually asked: it compiles **every** type and
block rather than only the inconsistent ones. That is the re-verification path after a restore,
and the only way to re-examine an item whose errors have already been forgotten.

Exit **8** if any item compiled with errors, **11** if any remain inconsistent, **14** if nothing was
examined, **0** only when items were examined and none of the above is true.

## `block-layout` — optimized vs standard block access (2026-08-11)

### The read-back gate, and why the setting is NOT durable (migrated from CLAUDE.md 2026-08-21)

Read form: `block-layout <project> --block <name> [--expect Standard|Optimized] [--device <name>] [--json]`
Write form: `... --set Standard|Optimized ... --yes`

Why it matters: **CLASSIC S7comm CANNOT SEE AN OPTIMIZED BLOCK AT ALL** — not an error, the block is
simply ABSENT, and it fails at the first DATA read rather than at connect. A PC-side harness reading
a DB over Sharp7 needs that DB to be STANDARD. The S7-1200 default is Optimized and **the IR path
silently yields Optimized**: `MemoryLayout` is absent from `ir/SPEC.md`, never written by
`DbSourceWriter`, never read by `DbSourceParser`, and on `Normalizer`'s ignore list — so an
IR-authored DB imports optimized with NO error at import, NO error at compile and NO drift-check
finding.

🔴 **DESTRUCTIVE: changing a block's layout DESTROYS ITS RETAINED DATA on the next download** — no
migration, no warning at download, and nothing visibly missing afterwards. Aimed at ONE new
purpose-built block, never at one in service. `--yes` required; without it the plan prints and
Portal is NEVER contacted (exit 10).

The write form sets, saves, **RE-RESOLVES the block and READS THE LAYOUT BACK**. A read-back that
does not match the request is **exit 15, never a success with a note** — a silent no-op is the whole
failure mode — and there is deliberately no flag that skips the check.

🔴 **NOT DURABLE, MEASURED 2026-08-11: A RE-IMPORT OF THE BLOCK REVERTS IT TO `Optimized`.**
Confirmed by TIA exports either side of one import, and the revert happens **AT IMPORT**, observed
BEFORE any compile (compile is not implicated). Mechanism: the exported `.xml` carries NO
`MemoryLayout` element at all, so the import states no opinion and TIA applies the S7-1200 default.
`Normalizer` ignores the attribute, so **`drift-check` is structurally BLIND to a change in either
direction** — the block goes silently back to invisible on the wire while every check stays green.
It bites on the SECOND import.

➜ **RE-ASSERT `--set Standard --yes` AFTER EVERY IMPORT OF THE BLOCK, then gate with
`--expect Standard`. REQUIRED, not precautionary** — `--expect` only tells you it broke; `--set` is
what repairs it.

Do NOT "fix" this by un-ignoring `MemoryLayout` in `Normalizer`: converter output never emits it, so
that breaks every export-vs-output comparison wholesale. It only becomes correct once the converter
can EMIT the attribute.

```
openness-cli block-layout <project> --block <name> [--device <name>] [--expect Standard|Optimized] [--json]
openness-cli block-layout <project> --block <name> --set Standard|Optimized [--device <name>] [--json] --yes
```

**Why this exists.** A PC-side test harness reads a PLC over classic S7comm. **Classic S7comm cannot
see an OPTIMIZED block at all** — the block is not reported as an error, it is simply absent, and the
failure surfaces at the first *data* read rather than at connect. On an S7-1200 the TIA default is
`Optimized`.

Nothing else in this toolchain can see the attribute. `MemoryLayout` is absent from the IR grammar
(`ir/SPEC.md`), is never written by the converter's `DbSourceWriter` and never read by its
`DbSourceParser`, and `Normalizer` has it on its ignore list. So a DB authored in IR and imported
comes out **optimized, silently**: no error at import, no error at compile, no `drift-check` finding.
Measured 2026-08-11 — a genuine TIA export of a DB created that way reads
`<MemoryLayout>Optimized</MemoryLayout>`.

Backed by `Siemens.Engineering.SW.Blocks.PlcBlock.MemoryLayout`, a read/write property of enum type
`Siemens.Engineering.SW.Blocks.MemoryLayout` with members `Standard` and `Optimized` — confirmed by
reflecting on the installed V20 assembly (`CanRead=True CanWrite=True`) and in its own
`Siemens.Engineering.xml` ("Determines if a block access is optimized or not"). This is the first
thing in this CLI that writes a property on a `PlcBlock` at all; every other setter here is on the
HMI path.

**Read (the default) is read-only.** It reads one property and does not save. A block reported as
`Optimized` also gets the S7comm note, because that is the consequence, not the value.

**`--expect` makes the read a gate.** Same read, but a mismatch exits **15** instead of merely
printing. It is there to be run after an import — see the durability hazard below.

**`--set` is destructive, and says so.** Changing an existing block's memory layout **destroys its
retained data on the next download**. There is no migration and no warning at download time, and
unlike a deleted block there is nothing visibly missing afterwards — the loss shows up as a
retentive value that did not survive a restart. The warning is printed in both the dry run and the
confirmed run. `--yes` is required; without it the plan is printed and **Portal is never contacted**
(exit **10**), decided from the arguments alone before `Connect`, exactly like `delete` and
`hmi-create-screen`.

**A set is verified by reading it back.** After setting the property the project is saved, the block
is **re-resolved from the project** (not re-read off the same object, which a cached value could
answer), and the layout read again. A read-back that does not match the request exits **15** and is
reported as `NOT APPLIED` — never a success with a note, because a silent no-op is the exact failure
this command exists to catch. The check compares against the *invocation's own* requested value
rather than the value the result reports having been asked for, so it fails closed even if those ever
disagree. **There is deliberately no flag that skips it.**

Safety blocks are refused before the property is touched, like every other subcommand (exit 6).

### MEASURED: a re-import REVERTS the layout to `Optimized`

**Re-importing the same block reverts it to `Optimized`.** Measured against a real project on
2026-08-11, confirmed by genuine TIA exports either side of one import, and **the revert happens at
IMPORT — observed before any compile ran.** Compile is not implicated.

The mechanism is plain once seen: the exported `.xml` contains **no `MemoryLayout` element at all**,
so the import states no opinion and TIA applies the S7-1200 default, which is `Optimized`.

Nothing downstream notices:

- the converter emits no `MemoryLayout`, so an import carries **no opinion** about layout;
- `Normalizer` ignores the attribute — under a comment reading *"Block-level configuration TIA
  assigns sensible defaults for on Import() regardless of source content"* — so **`drift-check`
  cannot detect a layout change in either direction**;
- so the block goes silently back to being invisible to any classic-S7comm reader while every check
  in the pipeline stays green. As predicted, it bites on the *second* import, not the first.

**Re-assert after every import of the block. This is required, not precautionary:**

```
openness-cli block-layout <project> --block DB_Whatever --set Standard --yes
openness-cli block-layout <project> --block DB_Whatever --expect Standard
```

The `--expect` call is the gate; the `--set` is what actually repairs it. Running only `--expect`
tells you the block is broken without fixing it.

Do **not** "fix" this by un-ignoring `MemoryLayout` in `Normalizer`: converter output never emits the
attribute, so un-ignoring it would make every real-export-vs-converter-output comparison differ and
break the drift check wholesale. That change only becomes correct once the converter can *emit* the
attribute, which is separate work.

Related, and worth reading before reasoning about a block's layout from any file on disk:
`docs/notes/block-memory-layout-and-the-export-trap.md`. Its headline is that **converter `to-xml`
output is not a TIA export** — it renders five block-level attributes and `MemoryLayout` is not one of
them, so grepping converter output for it finds nothing, and *nothing is evidence about the converter,
not about the block*. An absent attribute is "unanswered", never "false".

## `library` — the project-library walk (migrated from CLAUDE.md 2026-08-21)

This command had no section here; what follows was its only documentation.

### Walk

`library <project> [--master-copies] [--json]`

READ-ONLY. Every type with its CLR class, consistency status, `GetSupportedExportFormats()` and
versions.

### `--export-version` — a second, format-free export

`library <project> --export-version <TypeName> [--version <v>] --out <dir>`

Calls `LibraryTypeVersion.Export` — distinct from type-level `ExportAsDocuments`. **PLC types emit a
`ContentObject` with the full definition; EVERY plain `LibraryType` — faceplates AND images — emits
none.**

So HMI library content is **not withheld, it is simply NOT IN the library object**: it lives in the
project's binary `.rdf` store (`docs/notes/hmi-rdf-store.md`), which is also why Unified has no
screen export — there is no document because the project never keeps one.

**Exits 7 if nothing was written.**

### `--probe-documents` — BUILT, NEVER RUN

`library <project> --probe-documents <TypeName> --out <dir>`

Invokes EVERY `Export*` overload including `ExportAsDocuments` **when the format list is EMPTY**,
reporting each binding and outcome. That emptiness has been treated as a gate since P10 and never
TESTED as one — **an empty ADVERTISEMENT is not a REFUSAL.** A throw here is a RESULT, not a bug.

## `download-plan` — what a download would comprise, and why it cannot perform one (2026-08-11)

### Granularity, the provider acquisition path, and what the connection is NOT (migrated from CLAUDE.md 2026-08-21)

READ-ONLY, DRY-RUN ONLY. No `--yes`, no `--force` (both refused BY NAME), no confirmed form; nothing
in the binary reaches `DownloadProvider.Download`, and a test walks the compiled IL of EVERY method
in the assembly asserting so (negative-tested by retargeting it at `ICompilable.Compile`, which it
correctly caught).

**GRANULARITY IS DEVICE-LEVEL AND THAT IS THE POINT.** `Download()`'s three overloads each take a
connection, two callbacks and a `DownloadOptions` — **NO block, group, selection or exclusion** — so
"download my one new DB" IS NOT A THING THIS API DOES; the smallest real unit is THE WHOLE PLC
SOFTWARE. `SoftwareOnlyChanges` means "the parts TIA finds different from the controller", decided
by TIA at download time, **NOT** "the blocks you edited" — which is why the default is the
unambiguous `Software`.

**Reports the PROVIDER ACQUISITION PATH.** `DownloadProvider`'s only ctor is internal and it
implements `IEngineeringService`, so **`IEngineeringServiceProvider.GetService<DownloadProvider>()`
is the ONLY route there is.** Which object answers is unstated by the API, so it asks outward from
the software-bearing item and reports EVERY object tried with its `GetServiceInfos()`.

**Reports the CONNECTION from `DownloadProvider.Configuration`**
(Modes→PcInterfaces→TargetInterfaces→Addresses) — a **PROJECT-MODEL read, never a connect.**
`GetAccessibleDevices()` (a live scan) and `ApplyConfiguration()` are **never called**, so these are
CONFIGURED addresses that say nothing about what answers.

REFUSES safety (exit 6): device-level granularity means no download of an F-capable PLC excludes its
safety program, so planning one is planning to write it. **Exit 16 = no provider obtainable** — the
plan ran and describes nothing (empty is not clean). Says NOTHING about whether a download would be
PERMITTED — that is the write fence's question (ADR-0009).

```
openness-cli download-plan <project> [--device <name>] [--options Software|SoftwareOnlyChanges|Hardware] [--json]
```

**This command plans and cannot download.** There is no `--yes`, no `--force` and no confirmed form;
`--yes` and `--force` are *refused by name* rather than ignored, because someone who types one has
concluded this command can be talked into it. No argument, environment variable or build
configuration in this binary reaches `DownloadProvider.Download` — the one method that would,
`IOpennessGateway.PerformDownload`, has a body consisting entirely of a `throw`, and a unit test
walks the compiled IL of **every method in the shipped assembly** asserting that no call to a
`Download` member on a `Siemens.Engineering.Download` type exists anywhere in it. That test was
negative-tested by temporarily retargeting it at `ICompilable.Compile` and confirming it *fails*,
naming `OpennessGateway.RunCompile` — a scanner that has never been shown to detect anything is not
a check.

### Granularity is DEVICE-LEVEL. There is no per-block download.

The single most important thing this command exists to say. `DownloadProvider.Download` has three
overloads and every one of them takes a connection, a pair of configuration callbacks and a
`DownloadOptions` value. **None takes a block, a group, a selection or an exclusion.** So "download
my one new DB" is not something this API does — the smallest real unit is the whole PLC software.

`SoftwareOnlyChanges` does **not** mean "the blocks you edited". It means the parts TIA finds
different from what is in the controller, decided by TIA at download time, neither chosen by nor
visible to this tool beforehand. Because that value is the one most easily misread as a per-block
selector, the **default is `Software`** — the unambiguous whole-software value. The misconception has
to be typed in; it is not inherited by leaving a flag off. Siemens's own enum also has `None`, which
transfers nothing; this command refuses it, since a confident plan for a transfer of nothing is the
most dangerous output it could produce — it reads as reassurance.

The granularity statement prints on **every** run, in table and JSON alike, for every option value.
It is not conditional, because the belief it corrects is what a reader arrives with rather than
something a particular flag triggers. In JSON it is a *field* (`granularity`, alongside
`canDownload: false`), not only prose: a consumer keying on a missing property could not tell "this
build refuses" from "this build predates the flag".

### Where the `DownloadProvider` comes from

`Siemens.Engineering.Download.DownloadProvider` has exactly one constructor and it is `internal`, and
the type implements `IEngineeringService`. `IEngineeringServiceProvider.GetService<T>()` is
constrained `where T : class, IEngineeringService`. Those two facts together mean
**`GetService<DownloadProvider>()` is the only way a client can ever hold one** — it is not returned
by any method, and it cannot be constructed. (Confirmed by reflecting on the installed V20 assembly;
`RHDownloadProvider`, for redundant systems, is the same shape.)

What the API does *not* state is which object answers. So this walks **outward from the device item
carrying the `PlcSoftware`**, asking each ancestor, and reports every object it asked with what each
one advertises via `GetServiceInfos()` — the same shape `CompileHmiTarget` uses, which exists
precisely because the analogous assumption for `ICompilable` ("it will be on the software-bearing
item") was measured wrong and returned null. The objects that *refused* are reported too: "the CPU
answered and the station did not" is the finding, and a report showing only the winner would have to
be re-derived by hand next time.

If no provider is obtainable anywhere in the tree, the command exits **16**, not 0. The report then
answers none of the questions it was asked, and a green exit would present that silence as a clean
bill of health (FI-44 — empty is not clean).

### The connection is readable WITHOUT connecting

`DownloadProvider.Configuration` is a `Siemens.Engineering.Connection.ConnectionConfiguration` — an
ordinary project-model object, walked with plain property reads:

```
ConnectionConfiguration  (IsConfigured, EnableLegacyCommunication)
  └── Modes            ConfigurationMode          (Name)
       └── PcInterfaces   ConfigurationPcInterface  (Name, Number, Addresses, Subnets)
            └── TargetInterfaces  ConfigurationTargetInterface  (Name, Addresses)
                 └── Addresses    ConfigurationAddress  (Name, Address)   ← the device-side address
```

**No socket is opened to produce any of it.** The one member on that tree that *would* touch the
network — `ConfigurationPcInterface.GetAccessibleDevices()`, "Delivers a list of accessible devices",
a live scan — is never called anywhere in this codebase. Neither is
`ConnectionConfiguration.ApplyConfiguration(...)`, which mutates the project's connection
configuration: a plan does not get to change what it is planning.

So the report describes **the route the project has configured, and says nothing about whether
anything is at the far end of it.** A configured address that answers and one that does not look
identical here, and mistaking the first for the second is the only wrong conclusion this section can
produce — which is why it leads with `READ FROM THE PROJECT, NOT FROM THE NETWORK` rather than
closing with it.

### Safety content is refused at the plan (exit 6)

Not the export path's refusal copied across. Nobody names a safety block here, and the plan is
refused anyway — because granularity is device-level, so on an F-capable PLC **there is no download
that excludes the safety program**. There is no option, overload or flag for it. Planning a download
of such a device *is* planning to write safety content (hard rule 2), and the refusal has to happen
while it is still a plan.

### What it also reports

Blocks the project flags `IsConsistent=false` are listed as **unverified content a download would
carry**, with a pointer to `compile-all`. This command gates nothing — it is a plan, not a gate — but
a report that omitted them would be describing the transfer of content no compile has ever examined
(hard rule 4, FI-52).

### What it deliberately does NOT do

- It does **not** ask whether a download would be *permitted*. That is the write fence's question
  (`src/device-guard/`, ADR-0009) and this command never asks it — see the note below.
- It does **not** answer the download's configuration callbacks. `docs/notes/openness-api-survey-plc-online.md` §4
  records the rule for whenever a real `download` is built: an unanswered configuration that would
  prevent the download throws `EngineeringTargetInvocationException`, `DataBlockReinitialization` is
  the destructive one, and the command must **fail closed** — refuse a download that raised a
  configuration it was not explicitly told how to answer, rather than choosing a default.

### The fence is not wired in, and cannot be as things stand

`DeviceWriteGuard` (ADR-0009) is the seven-gate write fence, and it lives in `src/device-guard/`,
which targets **net8.0**. `openness-cli` must target **net48** — `Siemens.Engineering.dll` calls a
.NET-Framework-only `Assembly.Load` overload and fails at runtime on modern .NET. So openness-cli
**cannot reference `DeviceGuard` as it stands**, and no version of this command could have called it.
That is a reason this command plans rather than gates, not an omission from it: a plan needs no
authorization, and adding a fence call that could not compile would have been the worst of the
options. Resolving it is design work recorded with the branch, not something this command decides.

## Exit codes

Every code this CLI can return (`OpennessCli/Program.cs`, `ExitCodes`). Anything driving it from a
shell should branch on these rather than on stderr text.

| Code | Name | Meaning |
|------|------|---------|
| 0 | `Success` | The command did what it was asked. For `compile`, means **zero errors** — *not* `State == Success`, which a project-wide hardware warning makes non-`Success` on a clean block (see `compile` above) — and, for a `--block`/`--type` run, that the item read back **consistent** afterwards. For `sanity-check`, every block consistent and every device compiling clean. `portal-status` always exits 0 — it is informational, never a gate |
| 1 | `UsageError` | Argument parsing failed before anything was touched: unknown subcommand, missing/duplicated flag value, an unexpected positional, mutually-exclusive flags together |
| 2 | `EnvironmentError` | The TIA install couldn't be resolved (`TiaInstallNotFoundException`) — checked up front, before any Portal contact, so this never means a half-done operation. Fix `--tia-install` / `TIA_OPENNESS_PATH` |
| 3 | `ConnectTimeout` | Attach/launch didn't respond within `--timeout-connect` (default 180s). **First suspect the binary, not Portal**: an unapproved (rebuilt, or run from a different directory) executable is refused *silently* — the pre-attach whitelist check warns when it detects this. Otherwise the first-connect approval dialog. Confirm by running a known-approved build against the same project; it connects in under a minute |
| 4 | `ProjectOpenTimeout` | The project-open step exceeded `--timeout-open` (default 1800s). A large project legitimately takes minutes; raise the timeout before assuming a hang |
| 5 | `UnexpectedError` | Catch-all for any exception not classified below. Prints the full inner-exception chain. Treat as "a bug or an unmodelled Openness failure", not as user error — but see the caveat below |
| 6 | `SafetyRefused` | `SafetyContentRefusedException` — the command touched safety-classified content and was refused (hard rule 2). Not retryable, by design |
| 7 | `CommandError` | A recognised domain failure with a clear user-facing cause: `BlockNotFoundException`, `AmbiguousBlockException` (name/number under more than one device — pass `--device`), `DeviceNotFoundException`, `ExportProducedNoFileException`, `BlockMemoryLayoutUnavailableException` (the block resolved but exposes no access mode — name a DB or an FB) |
| 8 | `CompileFailed` | `compile`, `compile-all` or `hmi-compile` ran to completion and reported **at least one error** — the larger of its own `ErrorCount` and the `Error` messages in its message tree, whichever is bigger (one shared `Program.EffectiveErrorCount`, so all three judge identically). **Warnings alone never earn this**, nor does a non-`Success` `State` on its own (2026-08-12): a pre-existing hardware warning returns a non-`Success` state on a clean block, and keying on it made every per-block compile on such a project exit 8 with `errors: 0`. `hmi-compile` carried the same `State`-keyed defect and was fixed with it, on the ruling that a known defect left because it has not bitten yet is how it bites later. The diagnostics are on stdout (`--json` for structured form). Also earned by `hmi-create-screen`/`hmi-edit-screen` on a `Validate()` error |
| 9 | `SanityCheckFailed` | `sanity-check` ran to completion and the project is not healthy — at least one inconsistent block or type, or at least one device compile reporting **errors**. Both lists are printed. **Two things changed on 2026-08-13.** The compile it runs is now the **station** scope (hardware *and* program) rather than the DeviceItem scope, which compiled the hardware only and printed `Success (errors=0, warnings=0)` over a program containing a hard compile error — the report now names the scope on every line and prints each `[Error]` beneath it. And the health verdict now keys on the **effective error count**, not on `CompileState.Success`: the station scope surfaces a real project's standing warnings (an OB40 with no trigger, IO absent from the configured hardware), so a `State`-keyed verdict would mark every healthy project unhealthy forever. `errors=0` with a non-`Success` state is a **pass**, and the output says so |
| 10 | `NotConfirmed` | A destructive command printed what it *would* do and `--yes` was absent, so **nothing was changed and Portal was never contacted** — the refusal is decided from the arguments alone, before `Connect`. Earned by `delete`, `block-layout --set`, and the HMI writers |
| 11 | `CompileIncomplete` | A compile reported **no errors** and left something it should have verified unverified. Two ways to earn it, the same fact from either end: **(a)** a **whole-scope** `compile` (station, software or hardware) over blocks that remain flagged `IsConsistent=false` — it did not compile them, and the unverified ones are listed on stderr (FI-52). The backstop runs after all three scopes, and it is *not* made redundant by the station default: a compile can report no errors and still leave an item unexamined; **(b)** a **per-block/per-type** compile whose own item reads back `IsConsistent=false` afterwards, or whose read-back could not be performed at all (2026-08-12 — the converse of FI-52; TIA will refuse to *export* that item, and nothing in the compile output used to say so). Distinct from `CompileFailed`: nothing reported an error, the gate simply did not prove what it appears to have proved. One code for both because the caller's response is identical — this is not the hard-rule-4 gate passing. **`hmi-compile` can never return this**, and that is not reassurance: `IsConsistent` is PLC-only across the whole V20 API, so the HMI path has nothing to detect the condition with (see `hmi-compile` above) |
| 12 | `ExportIncomplete` | `export-all` exported everything it attempted, but the directory is **not** the whole project — something was refused (safety content, or a basename collision). Nothing went wrong; the dump is simply not whole, and comparing against it with `drift-check --complete` would produce findings about the dump that read as findings about the controller (FI-70). Same shape as 11 |
| 13 | `ImportIncomplete` | `import-all` ran, but the project does **not** now contain everything handed to it — a file that never resolved its dependencies, or one never attempted (unreadable, unclassifiable, duplicate basename). Its own code because a project missing a block looks exactly like one that is not: it opens, it lists, and a device compile can pass on it. Same shape as 11 and 12 |
| 14 | `NothingExamined` | The command ran, nothing went wrong, and it examined **nothing** — so its silence says nothing about the project. `compile-all` earns this when no item is flagged inconsistent. It is not a success because of how the gap arises: an item that compiled *with errors* is still flagged *consistent*, and errors do not survive the process, so the run right after a failed one is the one that examines nothing and looks cleanest. `--force` compiles everything |
| 15 | `LayoutMismatch` | `block-layout`: the block's memory layout is **not** the one asked for — either a `--set` whose read-back after saving disagrees with the request, or a `--expect` assertion that does not hold. Its own code because nothing was named wrongly and re-running with a different argument does not fix it; and never a success-with-a-note, because this failure is invisible everywhere else — an optimized block is not an error to a classic-S7comm reader, it is simply absent, and no compile, `drift-check` or `sanity-check` can see the attribute at all |
| 16 | `DownloadPlanIncomplete` | `download-plan` ran, nothing went wrong, and it could **not obtain a `DownloadProvider`** from any object in the device's tree — so the report answers none of the questions the command exists to answer, and its calm appearance is not evidence about anything. Same family as 11–14: not a failure (nothing threw, no argument was wrong, re-running changes nothing) and emphatically not a success. Says nothing about whether a download would be *permitted* — that is the write fence's question, which this command never asks |
| 17 | `ChangeAbandoned` | A write command failed and **the project is unchanged** — nothing was saved, and the partial mutation was removed from the open session. Earned by `create-instance-db` when the new instance DB comes back with an invalid block number (FI-63) or its number cannot be read back at all. Its own code because nothing was named wrongly (so not `7`) and because it is a modelled outcome with a known recovery, not an internal fault (so not `5`). The half a caller reads off it: **there is nothing to clean up, and a retry is safe.** Until 2026-08-13 this same condition exited `5` having already *saved* the broken block, so the exit code and the project disagreed about whether anything had happened |
| 18 | `RollbackIncomplete` | The `17` failure with its cleanup half missing: nothing was saved, so **nothing reached disk**, but the partial mutation could not be removed from the in-memory project model either. A separate code because the caller's response differs — which is this table's rule for when to split one (cf. `11`, which does not). On `17` a retry is immediately safe; on `18` the open Portal session holds a block that exists nowhere on disk, so if that session belongs to a person rather than to this process, close it **without saving** — advice that would be actively wrong on a `17` |
| 19 | `DuplicateBlockNumber` | The project contains **two or more blocks holding the same number** on one device (2026-08-13). Earned by `sanity-check`, and by `import`/`import-all` when the project holds a collision after the files went in. **Not `9`, and that separation is the whole point:** `9` means "something is inconsistent or a device failed to compile", and the measured project was *perfectly consistent and compiled clean* while holding two blocks at FC 910 — folding this into `9` would put a real defect behind a code whose documented remedy (compile the listed blocks) is exactly what **erased the only signal there was**. Not `7` either: nothing was named wrongly and re-running with a different argument does not fix it. **It outranks every other non-zero verdict here** (`9`, `13`) — those either clear themselves on the next pass or announce themselves again, and a duplicate does neither; both reports are printed in full regardless, so the ranking hides nothing. On the import path it does **not** mean the import failed: the files went in and were saved, and the message says so |

### 🔴 THERE ARE THREE PLC COMPILE SCOPES, AND `compile` HAS ALWAYS USED THE ONE THAT COMPILES *HARDWARE* (2026-08-13)

**Measured, on the JOB9004 scratch project, with `openness-cli compile-scopes`.** Openness exposes
`ICompilable` on five objects, and the three PLC ones are three *different* compilers — not one
compiler reached three ways:

| scope | how it is reached | what its message tree actually contains |
|---|---|---|
| **station** | `Device.GetService<ICompilable>()` | `Hardware configuration` **and** `Program blocks` — both halves |
| **device item** | `DeviceItem.GetService<ICompilable>()` | `Hardware configuration` **only** |
| **software** | `PlcSoftware.GetService<ICompilable>()` | `Program blocks` **only** |

`CompileDeviceItem` asks the **device item** first and falls back to `PlcSoftware` only when the
device item has none. On an S7-1200 the device item always has one, so **that fallback is dead code
and the software-scope compile had never once been invoked from this repository.** Verbatim, from a
plain `openness-cli compile` on a project with an uncompiled block sitting in it:

```
STATE: Success   ERRORS: 0  WARNINGS: 0
[Information] PLC_1:
[Information]   Hardware configuration:
[Information]     Hardware was not compiled. The configuration is up-to-date.
[Success]     Compiling finished (errors: 0; warnings: 0)
```

Nothing about program blocks. Not "up to date" — *absent*. And immediately afterwards,
`compile --software` on the same project, unchanged:

```
[Success] Program blocks:
[Success]   FC_ModbusTCP_Sample (FC8): Block was successfully compiled.
```

**This is the real mechanism behind FI-52.** That finding was recorded as *"device-level compile
does not clear the inconsistent flag"*, which is true and reads like a TIA quirk. It is not a quirk:
the device-level compile **does not look at the program at all**, so of course it clears nothing and
of course it can report `Success, errors=0` over nineteen uncompiled blocks. The same correction
applies to `sanity-check`, whose `Device compiles:` line runs this same hardware-only compile — its
value is entirely in the `INCONSISTENT:` enumeration, and its compile half was never a program check.

Added, therefore: `compile --software`, `compile --station`, `compile --hardware` (the old default,
kept reachable by name), and the read-only `compile-scopes` survey that produced the table above.

**The owner ruled on 2026-08-13: `--station` is the gate.** So:

- **A bare `compile` now runs the station scope.** Every caller in this repository — the `lad-coder`
  agent, the `gen-block-new` skill, docs 03/05/08/11/15, hard rule 4 — invokes a bare `compile`
  *intending* a program check, and not one of them wanted the hardware-only compile it was getting.
  Changing the default fixes them all at once; leaving it would have required editing every caller,
  including the ones a silent miss hurts most.
- **`sanity-check` compiles at the station scope too, and now says which scope it ran.** Its
  `Device compiles:` line has never been a program check. On the broken program it printed
  `Success (errors=0, warnings=0)` while FC8 did not compile; it now prints
  `scope=station (hardware + program)` and every `[Error]` message underneath.
- **`SanityCheckResult.IsHealthy` had to change with it.** It keyed on `CompileState.Success`, which
  was survivable only because the hardware-only compile had nothing to warn about. The station scope
  reports the project's genuine warnings (an OB40 with no trigger; IO absent from the configured
  hardware), so on `State` this check would have called a perfectly good project unhealthy on every
  run, for reasons no action of ours can clear. It now keys on the **effective error count** — the
  larger of the compiler's own aggregate and the `Error` messages in its tree — the same fail-closed
  rule `compile` and `compile-all` already use.

### There is no "rebuild all" through Openness, and that is measured (2026-08-13)

`ICompilable.Compile()` takes **no arguments**. `compile-scopes` also dumps each compiler object's
`GetAttributeInfos()` and `GetInvocationInfos()`, because a name-based attribute or a parameterised
invocation are the only two other places a knob could hide. On every one of the five compilers:

```
attributes : (none — measured, not assumed)
invocations: Compile()
```

So **every Openness compile is a delta compile** — an unchanged program reports *"No block was
compiled. All blocks are up-to-date."* TIA's GUI plainly distinguishes `Compile → Software` from
`Compile → Software (rebuild all)`, and **only the second exists in the GUI.** When a download fails
with *"An internal consistency error has occurred. Please compile the program in this CPU again"*,
the recovery that is known to work is the GUI rebuild-all, and **no `openness-cli` invocation is
known to substitute for it.** `compile-all --force` is not it either: it forces *which items* are
visited, not *how* each one is compiled.

### The download runs a compile that none of ours ran, and its exception does not name the block

Measured 2026-08-13: `compile --block`, the device compile, `compile-all --force` and `sanity-check`
**all reported clean** while a device download failed. TIA's own Info → Compile tab said:

```
An internal consistency error has occurred. Please compile the program in this CPU again.
The following blocks could not be compiled: FC_ModbusTCP_Sample [FC8];
```

The Openness exception for that same failure carries, in total:

```
Siemens.Engineering.EngineeringTargetInvocationException
  Error when calling method 'Download' of type '...DownloadProvider'.
  An error has occured during download: 'Software compiling completed with error.'
  openness detail messages (1): (the same sentence again)
```

**The block name is not in the exception.** `download-probe` already walks the detail-message list
and prints it verbatim, so this is not a reporting gap — the API genuinely does not carry it. There
is also **no `DownloadResult` on the throw path**, so there is no result object to interrogate.

Nor is it on disk. Checked: the project's own `Logs/` folder (empty), every
`%APPDATA%\Siemens\Automation\Portal V20\OnlineService\*.log` (all **0 bytes**), the Openness
telemetry under `%PROGRAMDATA%\Siemens\Automation\TelemetryConnector` (logs API **call names** only —
`objectName`/`methodName`, no results), and TIA's ETW diagnostic trace
`%PROGRAMDATA%\Siemens\ETWEventCollector\TIA_ADiag_*.etl` (8 MB of UTF-16 internal engineering
events; it contains the block's *name* from the search indexer, and **zero** occurrences of
`successfully compiled`, `Compiling finished`, `could not be compiled`, `errors:` or `warnings:`).

**So the route to the block-level detail is not to decode the failure — it is to run the compile
that finds it before the download does.** The software/station scopes report per-block messages
(`FC_ModbusTCP_Sample (FC8): Block was successfully compiled.`), which is precisely the granularity
the exception lacks.

### 🔴 `download-probe`'s fence is an ALLOWLIST, not a file-name suffix (2026-08-13)

**The guard used to accept any project whose file name ended `" scratch.ap20"`. It was wrong in both
directions**, and neither half was fixable by renaming anything:

* **It blocked the legitimate case.** `GenProject1.ap20` (the S6 sandbox, deliberately not renamed —
  CLAUDE.md "Codename note") and `SampleProject.ap20` could never be the target of a download,
  however deliberately somebody chose one.
* **It did not stop the dangerous case.** A file-name suffix is a *convention*, and anything can be
  renamed into one. This machine carries about **nineteen real production `.ap20` projects** beside the
  scratch ones; a fence any of them could satisfy by rename was not protecting them.

*The suffix was not arbitrary* — the scratch **copies of live jobs** on this machine are named that
way, so it was fitted to the projects the tool was being pointed at, and it deliberately avoided
naming any of their paths in the repository. That constraint is real and the replacement still
honours it (below).

**The replacement is ADR-0011's pattern**, because its properties were argued for and measured there:
allowlist never denylist; entries absolute or **`repo:`-prefixed** so a committed file is portable
across worktrees; the check runs **before anything else**, so Portal is never contacted on a refusal;
junctions **detected and refused**, not half-resolved; `..`, casing and 8.3 short names cannot walk
around it; **and no override — no flag, no environment variable, no argument names a different
allowlist.** Refusal is **exit 3 (`RefusedByPath`)**, unchanged.

**Two allowlist files, both fixed paths, both read:**

| file | for |
|---|---|
| `tools/download-probe.allowlist` | projects whose paths may be committed. Tracked, reviewable in `git log`. |
| `%ProgramData%\Ladder-AI\download-probe.allowlist` | a project whose **path may not be committed** — a copy of a live engineering job (CLAUDE.md "Live runs": use anything, commit nothing). |

**Why the second one lives OUTSIDE the working tree rather than being a gitignored companion.** A
gitignored file is protected by a *pattern*, and a pattern protects the file somebody thought of — on
2026-08-13 an agent's `.claude/settings.local.json.bak` sat untracked but **unignored**, one
`git add -A` from committing a site path. A path outside the tree cannot be published by an
ignore-rule gap, a `git add -f`, or any tool that walks the repo. It is the same file with the same
rules, in the one place where a mistake cannot leak it.

**Every "empty is not clean" case is a refusal, never a pass:** no allowlist file, an allowlist of
comments only, a malformed entry, an unresolvable path, a junction, a project that is a directory, a
file that is not `.apNN`. And a **permitted** run prints which entry vouched for it, into the log —
a fence that only speaks when it refuses leaves a successful run unable to say what permitted it.

**Proved the way ADR-0011's fence is proved, not by inspection:** the session delegate — the probe's
only route to Portal — is a stub that **appends to a sentinel file**, and the refusal test asserts the
sentinel does not exist *and* that no log file was created. The instrument is controlled: a permitted
case asserts the sentinel **is** written. Disconnecting the fence turns **4 tests red**, the sentinel
test among them.

### `download-probe --json` carries the LOAD MANIFEST as data (2026-08-13)

**The manifest is the only positive evidence that a download carried anything** — `DownloadResult.State`
was `Success` on a live run that transferred nothing — so it is what the harness's `Loaded` verdict
keys on, and the most load-bearing value this binary produces. It was reaching its consumer **by
scraping**: the deployment gateway is a separate process (net8.0; it must never link
`Siemens.Engineering` or every harness build joins the `(Path, FileHash)` approval cycle), so it
re-parsed the *rendered log* embedded in the JSON, and anything the renderer dropped was invisible to
it.

`download-probe` now references **`Ladder.Download`** (netstandard2.0, no Siemens reference, no
network, no Portal) and calls `DownloadResultAdapter` on the live message tree — **this is the live
path that library's own documentation names, and until now nothing used it.** The result is emitted
under `--json` as `loadManifest`, whose keys are `DownloadFeedback`'s own property names, camel-cased:
`loadedObjects`, `loadedObjectCount`, `duplicateLoadedObjects`, `nonObjectLoadSubjects`,
`transferredItemCount`, `verdict`, `verdictReason`, `runStateDisclosed`, `runStateTransitions`,
`finalRunStateEvent`, `upToDateSignalPresent`, `unrecognisedMessages`, `anomalies`.

* **`source` says which authority produced it** — `DownloadResultAdapter` is first-hand, off the live
  objects; `ProbeLogReader` re-derives from a rendering and is forensics only. A consumer that cannot
  tell them apart cannot tell a first-hand answer from a second-hand one.
* **`available: false` with every list and count `null`** when no `DownloadResult` existed (an abort, a
  throw, a `--to-folder` run). *Empty is not clean:* an empty array would say "TIA loaded nothing",
  which is the opposite of "nobody looked". Mutating those nulls to empty lists turns a test red.
* **Additive.** The embedded `log` array and the `transferVerdict` / `softwareLoaded` / `logFile` keys
  the gateway reads today are untouched, so nothing breaks on the day this lands.
* **Tested against recorded live-rig downloads**, not fixtures authored by reading our own output: the
  99-object full load, the 1-object differential, the up-to-date run and the aborted run, read from
  `src/download-feedback/DownloadFeedback.Tests/Fixtures/`. A missing fixture is a failure, never a
  skip. Removing the `loadManifest` key turns **7 tests red**.

### 🔴 TWO IMPLEMENTATIONS OF THE TRANSFER RULE — measured, then collapsed to one (2026-08-14)

`download-probe` grew its own transfer classifier (`TransferVerdicts`, commit `79f1596`) and
`Ladder.Download`'s `DownloadFeedbackParser` landed **later the same day** (`ccaae5a`) written to
replace it — its remarks name the two things it excludes by construction, and **both were live in the
probe's copy**. The duplicate was simply left behind when `download-probe` gained its reference to the
library for `DownloadResultAdapter`.

**Established before fixing, because "they will diverge" is a prediction and not a measurement:**

| | |
|---|---|
| **Seven recorded downloads** (six `download-feedback` fixtures + the live deployment's stdout) | **AGREE — every one.** |
| **Two constructed shapes the corpus does not contain** | **DISAGREE, in opposite directions.** |

*** THE AGREEMENT WAS A PROPERTY OF THE CORPUS, NOT OF THE RULES. *** No recorded run has a result
that carries messages while naming nothing loaded — and that is exactly where they part:

* **A result naming nothing loaded.** The probe answered **"YES — THE SOFTWARE WAS LOADED"**, inferred
  from the *absence* of an up-to-date phrase. The library answers `Undetermined` from the manifest.
  This is the dangerous direction, and it is the precise defect `DownloadFeedbackParser` exists to
  make impossible.
* **A reworded up-to-date sentence.** The probe matched three *loose substrings* and concluded
  `NothingTransferred`; the library declines to conclude **and reports the message as unrecognised**,
  which is how that parser is designed to go out of date rather than silently wrong.

**The verdict is now `Ladder.Download`'s and is not recomputed here.** `TransferVerdicts.FromFeedback`
maps its three-valued verdict 1:1 and total, so this type cannot hold an opinion the library does not;
what stays is the **presentation** the library does not produce — the headline and the verbatim
evidence block for the log a person reads. `ProbeSession` builds the feedback **once** and uses it for
both the verdict and the manifest, so they can never be two readings of one download.

**`TransferVerdictParityTests` is kept after the answer**, because it is what proves the derivation did
not quietly reintroduce a second opinion: reintroducing the absence-inference turns **4 red**.

> 🔴 **Two existing tests asserted the old rule, and one of them asserted the defect.**
> `ASuccessWithoutUpToDate_IsReportedAsTRANSFERRED` *required* `SoftwareLoaded = true` for a result
> naming nothing loaded. **While it stood, the correct behaviour was a failing build.** It is renamed
> and inverted rather than deleted, so the change of mind is visible in the file. The other encoded
> the wide-substring matching; its *argument* ("a false 'transferred' costs a wrong conclusion") is
> preserved exactly where it matters — an unrecognised wording can never now produce a false
> "transferred", it produces the conservative `Undetermined` **and** surfaces the message.

### 🔴 A FOLDER RUN REPORTED THAT THE SOFTWARE WAS LOADED (found and fixed 2026-08-13)

**Found by the first live rehearsal of the deployment path** — `GenProject1`, `--to-folder`, no wire,
no CPU stop. The premise in the code was wrong:

> *"an abort, a throw **and a folder run** produce no `DownloadResult` at all."*

*** A FOLDER RUN PRODUCES ONE, AND ITS MESSAGE TREE NAMES OBJECTS AS LOADED. *** On the rehearsal:
**27 objects**, 3 non-object items, exit 0. So the report contradicted itself three ways in one
document:

| field | said |
|---|---|
| `transferVerdict` | `Undetermined` — correct, but only because of a post-hoc override |
| `loadManifest.verdict` | **`Transferred`** |
| top-level `verdict` | **"YES — THE SOFTWARE WAS LOADED"** |

The middle two are exactly a gateway's `Loaded` conditions. **A consumer handed that report computes
`Loaded = true` for a run that contacted no controller.**

**Two separate faults, and both are fixed at the source:**

1. **The verdict was decided by reading message text.** `TransferVerdicts.Classify` searches for
   up-to-date phrases and concludes "loaded" from their absence — it **cannot** tell a folder run from
   a device run, because the two produce the same words. Whether anything reached a controller is a
   property of **which overload was called**, so it is now decided from the destination
   (`ProbeSession.ClassifyTransfer`) and the message tree is reported as what it is.
2. **The old correction was applied too late.** `DownloadToFolder` reassigned `Transfer` *after*
   `ReportResult` had already rendered the verdict **sentence** from the un-overridden value — which is
   why the object and the sentence beside it disagreed. **A post-hoc correction only fixes the copy it
   reaches.** The override is gone; the destination is passed *in*, and the log section, the verdict
   string and the JSON are all rendered from one verdict.

**`ReportResult` and `ClassifyTransfer` take the destination as a REQUIRED parameter.** A default
would be a guess about whether a controller was contacted, and a caller that forgets must fail to
compile rather than fall back to "controller".

**What a folder run now reports:** `available: false` (there is no manifest of what reached a
*controller*), `target: "folder"`, `describesDeviceTransfer: false`, `verdict: Undetermined`, every
device-facing field `null` — and **`resultPresent: true`**, which is what keeps a folder run
distinguishable from an abort now that both are `available: false`. The image facts are **kept in
full** under `image` (`objects`, `objectCount`, `folder`, and `parserVerdict`, labelled as the
parser's word about the *image*). Discarding them would trade one dishonesty for another.

> **Why not `available: true` with the device fields nulled?** More honest-looking, and **measured to
> be worse**: the existing consumer treats a missing `loadedObjects` array as "no first-class
> manifest" and **falls back to scraping the embedded log** — which names the same 27 objects. The
> tidy shape would have re-created the false positive one layer down.

**🔴 And the guard for this existed and could not fire.** The consumer's `available is not true`
branch **names "a folder download" in its own message**, and was proved to work by a **hand-authored
`available: false` fixture** — a fixture asserting the very premise this run falsified. Written,
tested around, never executed. The regression tests therefore run on
`OpennessCli.Tests/Fixtures/folder-run-stdout-20260813.json`, **the recorded stdout of that real
folder run**: its message tree is the input, its conclusions are what is under test, and a control
test asserts the recording really does satisfy all three `Loaded` conditions — so "the new output is
honest" is measured against a recording that demonstrably was not.

Mutation-tested both routes: ignoring the destination in the classifier turns **2 red**; removing the
folder branch from the manifest builder turns **4 red**.

### `download-probe --to-folder` — the download's own compile, without the download

`DownloadProvider` has a second overload, `Download(DirectoryInfo, DownloadConfigurationDelegate)`,
which writes hardware and software to a **folder**. No connection, nothing on the wire, no CPU
stopped, no rig involved. Measured to raise the **same `ConsistentBlocksDownload` configuration** the
device download raises, and to return the same shape of `DownloadResult` — so it reaches the
download's own compile path while touching no controller.

**CORRECTION, same day.** The paragraph that stood here said `--to-folder` was *"not a reproducer"*.
That was written from a healthy project, where the folder run and a device download both succeeded —
and it generalised from an absence. On a **genuinely broken** program (controlled reproducer below)
the folder download fails **identically to the device download**:

```
==== *** FOLDER DOWNLOAD THREW *** ====
Siemens.Engineering.EngineeringTargetInvocationException
  An error has occured during download: 'Software compiling completed with error.'
  openness detail messages (1): (the same sentence again)
CONFIGURATIONS RAISED: (none)
```

Same exception, same text, **zero configurations raised** — which is exactly what every original
failing run (`bn-07/33/41/42/54`) recorded. So `--to-folder` **is** a faithful stand-in for the
download's compile gate, and it is the only one that needs no controller, no network and no CPU.

### 🔴 THE 1.7 THROWS KILL THE PORTAL PROCESS — and four reporting defects hid it (2026-08-13)

The owner observed at the machine, and the rig lane measured **by PID**: **both throws kill the Portal
process the download was attached to.** PRE killed 16972 of `11228 16972`; POST killed 8256 of
`8256 13912`. *Why* Portal dies is not established and is not guessed at here.

**A count alone could not see it** — 2→1 reads as "an idle instance closed" unless you hold the PIDs.
**And our own tooling silently relaunches**, so every surface we had showed an ordinary run. Four
defects, all of them *reporting* failures, all now fixed and negative-tested:

1. **`launched-instances.json` is `[]` — and that is the design, not a bug.** `MarkLaunched` writes on
   launch, `Unmark` deletes on successful open, so the record is **erased at exactly the moment it
   becomes interesting to a reader**. The registry only ever tracks *empty orphans*, so
   `MarkedByThisTool` is structurally incapable of being true for a process in use.
   **Which direction does it fail in? SAFE.** The reuse guard is
   `!hasProject && IsMarkedAsLaunchedByThisTool(pid)`; an empty registry makes the second conjunct
   false, so an empty process is **never** reused and a fresh one is launched — safe, and wasteful.
   *Nothing consulted it in a way that fails open.* Fixed by a **second, report-only launch history**
   (`launched-history.json`) that is never unmarked and **consulted by no decision** — deliberately
   not by relaxing the guard, which would have edited a safety rule to satisfy a reporting need.
2. **`portal-status` reported `PROCESSES: 1` while the OS showed two.** It enumerated only
   `TiaPortal.GetProcesses()`, so it agreed with the API and disagreed with the machine, silently —
   and **a process Openness cannot see is precisely the case you reach for this command in.** It now
   cross-checks the OS process list and reports the difference as its own class, `OS-ONLY`. An
   OS-only process is never classified as "empty": its null project is an absence of information, not
   a fact.
3. **It dated PID 16972 `ACQUIRED 14:47:51` when that process started `15:38:09`** — fifty minutes
   before its own existence. `TiaPortalProcess.AcquisitionTime` is **not** the process's age and had
   been documented as one. The table now leads with `STARTED` (the OS start time, which is the age
   signal) and flags `ACQUIRED` with `! BEFORE START` when it contradicts the OS. What
   `AcquisitionTime` *does* mean is not established and is not guessed at. **A plausible wrong
   timestamp is worse than a missing one**, because it invites exactly the reasoning-from-sequence
   this project spent the day avoiding.
4. **The guard failed in the binary written to carry it.** A POST run was **armed**, raised **zero**
   POST configurations, **never invoked the delegate**, completed the download — **and exited 0**,
   where the contract says `12`. The cause was a **missing arm**: the completion path tested "did it
   fire" and had no branch for *armed and never fired*. **Can exit 12 fire today? On the folder path
   yes; on the device path it could not — it was unreachable code.** Now decided in one place,
   `ProbeVerdict.Decide`, which is a pure function precisely so a test can interrogate it without a
   rig — the decision previously lived inline in a method needing Portal, a device and a download, so
   the only assertable part was `Classify`, which was already correct. **And the finding it should
   have reported is real: the raised-configuration set depends on `--options`** — forcing
   `--options Software` made the download stop the CPU and the post delegate *was* then reached.

Every one of the four has a **"did not run" partner test**: an ordinary process must not be flagged
OS-only, an ordinary timestamp must not be flagged impossible, a PID we never launched must be false
in both records, and an unarmed run must never earn exit 12. That shape — *a guard whose non-firing
had nothing testing it* — is now the seventh of its kind found across the lanes.

### Experiment 1.7 — the delegate throw: `--throw-from-pre-delegate` / `--throw-from-post-delegate`

**The capability. Firing it at a device is a separate, owner-present act** (the working agreement puts
that on the STOP list; building the mechanism is not on it).

Openness hands the tool two callbacks during a download. It is undocumented what happens if one
**throws** — and that decides whether a callback can refuse a download by failing, which is what the
whole fail-closed posture assumes.

**Two flags, never one, and not combinable.** They are two experiments with very different blast
radii: PRE fires before anything transfers; POST fires *after*, and **`StartModules` is raised in the
POST delegate, after the download has already stopped the modules** — so a throw there can leave the
CPU stopped with no route to start it from inside that download. A single `--throw` firing from
whichever delegate came first would give an unattributable result. `--throw` is itself a usage error,
deliberately, because it is the flag someone would guess.

**Rehearsed on `--to-folder`, and the answer is already in — at zero risk.** The PRE delegate **is**
reached on the folder path. Measured:

```
injected from : PRE delegate, invocation #1
after answering: ConsistentBlocksDownload

*** WHAT OPENNESS DID WITH IT: REPLACEDBYANOTHERFAILURE ***
    Siemens.Engineering.NonRecoverableException came out and the injected exception is
    NOWHERE in its inner chain — the API replaced it rather than carrying it
```

with the surfaced message being, in full: *"Unexpected exception - no exception message available."*

So Openness **neither propagates nor swallows — it replaces.** Two consequences, and they pull in
opposite directions:

- ✅ **A callback CAN stop a download by throwing.** The download did not proceed. Fail-closed by
  failing works.
- 🔴 **The reason is destroyed.** The caller learns nothing about *why* — the same family as the
  download-compile finding above: the API discards the diagnostic and hands back a sentence that
  fits every failure there has ever been. **A tool relying on this must log its own reason before
  throwing**, because nothing downstream will carry it.

**The session survives.** Despite the name `NonRecoverableException`, a subsequent folder download on
the same Portal session completed normally (`state=Success, errors=0`) and `list` reported 84 blocks,
0 inconsistent. The exception is non-recoverable to the *call*, not to the session.

**POST cannot be rehearsed anywhere** — the folder overload is `Download(DirectoryInfo,
DownloadConfigurationDelegate)`, one delegate, the pre one. `--throw-from-post-delegate --to-folder`
is therefore **refused by name** rather than arming something that can never fire and exiting clean
having tested nothing.

**Exit codes:** `11` = the throw fired (a *deliberate* failure, its own code so no script or reader
can mistake it for a real one); `12` = armed and the delegate was never invoked, so nothing was
learned — emphatically not a pass. The injected exception is its own type
(`DeliberateProbeInjectionException`), which nothing in Siemens.Engineering can produce, and the log
says the failure was deliberate in the banner, at the point of the throw, and in the verdict.
Classification is by **object identity** against the thrown instance — never by reading a state or a
message string.

### The controlled reproducer (2026-08-13) — and what each scope said

`DB_Example` is a global DB that `FC_ModbusTCP_Sample` (FC8) reads. Deleting it creates the dangling
reference, which is the defect class FC8 was described as having. Measured, in order, on the scratch
project:

| check | verdict on the broken program |
|---|---|
| `compile` (**old** default, `--hardware`) | `STATE: Success  ERRORS: 0` — **hardware tree only, says nothing** |
| `compile --software` | `[Error] FC_ModbusTCP_Sample (FC8): Network 1: Tag "DB_Example".DataStore not defined.` |
| `compile --station` | same error, plus the hardware tree |
| `compile --block FC_ModbusTCP_Sample` | same error, and `CONSISTENT: NO` |
| `sanity-check` (**old**, device compile) | `ISSUES FOUND` — but its compile line still read `Success (errors=0, warnings=0)` |
| `compile-all --force` | `116 compiled, 1 with errors, 1 still inconsistent` |
| `download-probe --to-folder` | **threw** — `Software compiling completed with error.` |
| device download | **threw** — identical |

**The error text is better than TIA's own download message**: `Network 1: Tag "DB_Example".DataStore
not defined` names the network and the tag, where the GUI's Info → Compile tab says only *"The
following blocks could not be compiled: FC_ModbusTCP_Sample [FC8]"*.

**Recovery was complete through Openness alone** — no GUI, no rebuild-all. Re-importing `DB_Example`
and running **one** `compile --station` compiled the restored DB *and* FC8 in a single call (the
scope resolves dependency order itself, which per-block compiles cannot), after which
`sanity-check` returned `HEALTHY` on both lines and the download succeeded again.

**What this reproducer does NOT establish, stated plainly.** The original FC8 failure had
`compile --block FC_ModbusTCP_Sample` reporting **clean** with `CONSISTENT: yes`. Here the per-block
compile reports the error. So this reproduces *a* defect the hardware-only gate missed — which is
what the ruling rests on — but **not** the specific class where every per-item compile passes and
only the download's compile fails. That class remains unreproduced, and `--station` is not proven
against it. `--to-folder` is, by construction, because it *is* the download's compile.

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

### And a per-block compile is not a gate on its own block either (2026-08-12)

The converse, measured on the same machinery. A per-block compile reported *"Block was successfully
compiled"* with `errors: 0`, and the block was **still** flagged `IsConsistent=false` — a block it
referenced did not exist. TIA then refused to export it: *"Inconsistent blocks and PLC data types
(UDT) cannot be exported."* Only `sanity-check` caught it; the compile that had just declared the
block fine said nothing.

So `--block`/`--type` now **re-resolves the item after compiling and reads `IsConsistent` back**,
prints it as `CONSISTENT: yes|NO`, and exits `11` on a `NO` — the same code the device-level case
earns, because it is the same claim: *no errors were reported, and something went unverified*. The
read-back is not optional and there is no flag that skips it, for the reason `block-layout --set`
gives for its own: the failure mode being caught is an operation that reports success and did not
take, and only looking afterwards tells the two apart.

**Known gap (2026-08-05, audit F-09): a mis-typed `--group` exits 5, not 7.** The group-resolution
failures in `OpennessGateway` (`FindGroup`/`FindTypeGroup`/`FindTagTableGroup`) throw plain
`InvalidOperationException`, which falls through the classified `catch` filter into the catch-all —
so the commonest user mistake reports as an internal error. What you actually see is this CLI's own
`No device item found under '<path>'` wrapped in the catch-all's inner-exception chain. The fix is a
dedicated exception type added to the filter; until then, read a 5 from `import`/`create-instance-db`
as "check the `--group` path against `list`'s `Path` column verbatim" first.

### ⚠️ `hmi-delete-screen` with several `--name` flags — UNEXPLAINED, reproduced 2026-08-20

Passing eighteen `--name` values in one invocation failed with

```
No classic HMI screen named '09 Parameters' found in the project.
```

**and the same name, alone, in the very next command, deleted successfully.** Two more single-name
deletes and then a loop of sixteen all succeeded — 18 of 18 removed, one call each.

The argument parser accumulates repeated `--name` correctly (verified by echoing the constructed
argv: 36 arguments, `--name` and value alternating, names intact), and `DeleteScreens` resolves each
name against a freshly-built candidate list per iteration, so neither obvious cause holds.

**The cause is NOT established and is deliberately not guessed at here.** What is measured is the
behaviour and the workaround: **one name per invocation**. It costs a Portal attach each, which is
the only real price.

Worth noting the failure was SAFE — the resolution loop runs to completion before anything is
deleted, so a name that does not resolve aborts the whole call and nothing is removed. The defect
is a refusal that should not have happened, not a deletion that should not have happened.

#### What the NEXT reproduction will say (2026-08-20)

The cause is still not established, and the change made here does not pretend to establish it — it
makes the next occurrence *diagnosable*, which is what was actually missing. `ScreenNotFoundException`
reported **only the name**, so absence, a failed walk, and a string that is not the string it appears
to be all printed identically. It now reports:

- **`PRESENT:`** — every classic screen name in the project, built through the *same* walk that
  failed to find the requested one, so the two cannot disagree about what the project holds. A
  project with no classic screens says so rather than printing an empty list.
- **`NEAR MATCH:`** — any present name that differs from the requested one only by whitespace,
  invisible characters or case, followed by **both names dumped code point by code point**.

That second line targets the one hypothesis reading the code could not eliminate: `--name` is taken
**verbatim** (`TryTakeValue` — no `Trim`, no Unicode normalisation) and matched `Ordinal`. A trailing
space or a non-breaking space in one element of a long generated command line is invisible to an
echo, invisible in the output, and produces exactly the observed signature. **The argv was verified
by echo at the time, and an echo cannot show this.**

Deliberately NOT done: trimming or normalising the name. That would change which screens can be
addressed on the strength of a hypothesis, and a delete is the one irreversible operation this CLI
exposes. If the near-match line fires on the next reproduction, the fix follows from evidence; if it
stays silent, that is evidence too — and the present-set will show whether the screen was there at
all.
