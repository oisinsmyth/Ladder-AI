# openness-cli

C# CLI — the only component that talks to TIA Portal (via Openness). Built in S0/S1.

## Subcommands (contract per docs/05-architecture.md)

```
openness-cli list          <project> [--tagtables]                            # enumerate blocks (or tag tables, with --tagtables); F-/safety blocks flagged, never opened
openness-cli export        <project> (--block <name> | --type <name> | --tagtable <name>) [--device <name>] --out <path>   # block/UDT/tag table → SimaticML (refuses safety blocks)
openness-cli export        <project> (--screen <name> | --hmitagtable <name> | --textlist <name>) --out <path>   # CLASSIC HMI content → SimaticML. --tagtable is the PLC one; --hmitagtable is the HMI one — see docs/hmi.md
openness-cli import        <project> --group <device>/<path> [--type | --tagtable] <files...>   # SimaticML → TIA (--type/--tagtable import into the Types/TagTables composition, not Blocks)
openness-cli import        <project> (--screen | --hmitags | --textlists) <files...> [--device <name>]   # CLASSIC HMI content. No --group. 🔴 ImportOptions.Override REPLACES the object, it does not merge, so adding one tag means shipping the whole table — see docs/hmi.md
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
openness-cli block-layout  <project> --block <name> [--expect Standard|Optimized]   # READ-ONLY: optimized vs standard block access. Classic S7comm cannot see an OPTIMIZED block at all; the IR path yields Optimized silently — see docs/core-commands.md
openness-cli block-layout  <project> --block <name> --set Standard|Optimized --yes  # DESTROYS THE BLOCK'S RETAINED DATA on the next download. Sets, saves, re-resolves and READS BACK; a mismatch is exit 15, never a pass
openness-cli download-plan <project> [--device <name>] [--options Software|SoftwareOnlyChanges|Hardware]   # READ-ONLY, DRY-RUN ONLY: what a download WOULD comprise. CANNOT DOWNLOAD — no --yes, no --force, no confirmed form. Granularity is WHOLE-PLC; there is no per-block download — see docs/download-and-transfer.md
openness-cli create-instance-db <project> --group <device>/<path> --name <name> --instance-of <FBName>   # scaffolding: instance DB for an already-existing FB. A FAILED RUN LEAVES THE PROJECT UNCHANGED (2026-08-13): the save happens only after the new DB's number reads back valid, so no "Created ..." line means nothing reached disk — exit 17, nothing to clean up (18 = not saved either, but the open session was left holding it). It used to save in a `finally` and COMMIT the broken block it had just failed to fix — see docs/core-commands.md
openness-cli sanity-check  <project>                                           # is this project's Openness state OK? see docs/core-commands.md. Since 2026-08-13 its compile half is the STATION scope
                                    # (hardware + program) and the report NAMES the scope it ran — it used to be the DeviceItem scope, which compiles the hardware only and reported
                                    # `Success (errors=0, warnings=0)` over a program that did not compile. Its verdict now keys on ERRORS, never on State, or the station scope's real warnings
                                    # would mark every healthy project unhealthy
openness-cli portal-status                                                     # read-only Portal-process diagnostic (no project); never attaches/launches/kills — see docs/core-commands.md
openness-cli portal-close  [--pid <n>]... [--json] --yes                       # 🔴 DESTRUCTIVE: TERMINATES Portal OS processes (no project). Without --yes it prints the plan and exits 10. A sweep takes empty and Openness-invisible processes; a Portal WITH A PROJECT OPEN, or an empty one SOMEBODY IS ATTACHED TO, is reachable only by --pid. NEVER RUN LIVE beyond the plan form — see docs/portal-process.md
openness-cli hmi           <project> [--screen <name>|*] [--max-items <n>]     # READ-ONLY HMI walk: screens, screen items, per-property dynamizations — see docs/hmi.md
openness-cli graphics      <project> [--list] [--inspect <name>] [--export <name> --out <path>] [--import <file>]... [--overwrite]   # the PROJECT-level picture store — see docs/hmi.md
openness-cli graphics      <project> --delete <name>... --yes                  # deletes graphics BY LITERAL NAME (no wildcard form exists); unknown name = hard error, nothing deleted — see docs/hmi.md
openness-cli hmi-delete-screen <project> --name <name>... --yes                # deletes CLASSIC screens, same contract. `hmi-delete` is Unified-only and cannot see one — see docs/hmi.md
openness-cli hmi-delete-tagtable <project> --name <name>... [--device <name>] --yes   # deletes CLASSIC HMI tag tables, same contract again. `hmi-delete --kind TagTables` is Unified-only — see docs/hmi.md
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
| `portal-close` | **`portal-status`'s destructive sibling — it terminates OS processes.** `--yes`-gated, **never run live** | `portal-close` — closing a stray Portal |
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

## The documents

This file is the index and the invocation contract. The detail lives in `docs/`, split out on
2026-09-17 because a 2,398-line README is one nobody finishes.

| document | what you come to it for |
|---|---|
| [`docs/core-commands.md`](docs/core-commands.md) | per-command usage for the block and program surface, plus **the three compile scopes** and what a compile does and does not prove |
| [`docs/hmi.md`](docs/hmi.md) | the read-only HMI walk, the two write probes, the graphics store, and the classic tag-table / text-list document formats |
| [`docs/download-and-transfer.md`](docs/download-and-transfer.md) | 🔴 the only path that can move a program onto a controller — `download-plan`, and the `download-probe` findings |
| [`docs/portal-process.md`](docs/portal-process.md) | 🔴 `portal-close`, kept separate so the one destructive command stays visible |

**`## Exit codes` used to be 584 lines.** 28 of them were the table below; the rest were three
unrelated investigation logs appended under it over time — the compile-scope findings, the
`download-probe` family, and an `hmi-delete-screen` bug filed under exit codes for no reason anyone
recorded. Each now sits with the command it is about.

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
| 7 | `CommandError` | A recognised domain failure with a clear user-facing cause: `BlockNotFoundException`, `AmbiguousBlockException` (name/number under more than one device — pass `--device`), `DeviceNotFoundException`, `ExportProducedNoFileException`, `BlockMemoryLayoutUnavailableException` (the block resolved but exposes no access mode — name a DB or an FB). **`portal-close` also earns it without any exception**: at least one selected process ended in a failure outcome — a save that did not succeed (so it was left running), a terminate that failed, or a pid that no longer names the process the plan was made about |
| 8 | `CompileFailed` | `compile`, `compile-all` or `hmi-compile` ran to completion and reported **at least one error** — the larger of its own `ErrorCount` and the `Error` messages in its message tree, whichever is bigger (one shared `Program.EffectiveErrorCount`, so all three judge identically). **Warnings alone never earn this**, nor does a non-`Success` `State` on its own (2026-08-12): a pre-existing hardware warning returns a non-`Success` state on a clean block, and keying on it made every per-block compile on such a project exit 8 with `errors: 0`. `hmi-compile` carried the same `State`-keyed defect and was fixed with it, on the ruling that a known defect left because it has not bitten yet is how it bites later. The diagnostics are on stdout (`--json` for structured form). Also earned by `hmi-create-screen`/`hmi-edit-screen` on a `Validate()` error |
| 9 | `SanityCheckFailed` | `sanity-check` ran to completion and the project is not healthy — at least one inconsistent block or type, or at least one device compile reporting **errors**. Both lists are printed. **Two things changed on 2026-08-13.** The compile it runs is now the **station** scope (hardware *and* program) rather than the DeviceItem scope, which compiled the hardware only and printed `Success (errors=0, warnings=0)` over a program containing a hard compile error — the report now names the scope on every line and prints each `[Error]` beneath it. And the health verdict now keys on the **effective error count**, not on `CompileState.Success`: the station scope surfaces a real project's standing warnings (an OB40 with no trigger, IO absent from the configured hardware), so a `State`-keyed verdict would mark every healthy project unhealthy forever. `errors=0` with a non-`Success` state is a **pass**, and the output says so |
| 10 | `NotConfirmed` | A destructive command printed what it *would* do and `--yes` was absent, so **nothing was changed and Portal was never contacted** — the refusal is decided from the arguments alone, before `Connect`. Earned by `delete`, `block-layout --set`, and the HMI writers. ⚠️ **`portal-close` earns it too, and the "Portal was never contacted" half is NOT true there** — it prints its plan, which it can only build by reading the running process list first (the same read-only enumeration `portal-status` performs). Nothing was attached to, saved or terminated; that is the whole guarantee, and the command's own stderr says exactly that rather than the stronger sentence |
| 11 | `CompileIncomplete` | A compile reported **no errors** and left something it should have verified unverified. Two ways to earn it, the same fact from either end: **(a)** a **whole-scope** `compile` (station, software or hardware) over blocks that remain flagged `IsConsistent=false` — it did not compile them, and the unverified ones are listed on stderr (FI-52). The backstop runs after all three scopes, and it is *not* made redundant by the station default: a compile can report no errors and still leave an item unexamined; **(b)** a **per-block/per-type** compile whose own item reads back `IsConsistent=false` afterwards, or whose read-back could not be performed at all (2026-08-12 — the converse of FI-52; TIA will refuse to *export* that item, and nothing in the compile output used to say so). Distinct from `CompileFailed`: nothing reported an error, the gate simply did not prove what it appears to have proved. One code for both because the caller's response is identical — this is not the hard-rule-4 gate passing. **`hmi-compile` can never return this**, and that is not reassurance: `IsConsistent` is PLC-only across the whole V20 API, so the HMI path has nothing to detect the condition with (see `hmi-compile` above) |
| 12 | `ExportIncomplete` | `export-all` exported everything it attempted, but the directory is **not** the whole project — something was refused (safety content, or a basename collision). Nothing went wrong; the dump is simply not whole, and comparing against it with `drift-check --complete` would produce findings about the dump that read as findings about the controller (FI-70). Same shape as 11 |
| 13 | `ImportIncomplete` | `import-all` ran, but the project does **not** now contain everything handed to it — a file that never resolved its dependencies, or one never attempted (unreadable, unclassifiable, duplicate basename). Its own code because a project missing a block looks exactly like one that is not: it opens, it lists, and a device compile can pass on it. Same shape as 11 and 12 |
| 14 | `NothingExamined` | The command ran, nothing went wrong, and it examined **nothing** — so its silence says nothing about the project. `compile-all` earns this when no item is flagged inconsistent. It is not a success because of how the gap arises: an item that compiled *with errors* is still flagged *consistent*, and errors do not survive the process, so the run right after a failed one is the one that examines nothing and looks cleanest. `--force` compiles everything |
| 15 | `LayoutMismatch` | `block-layout`: the block's memory layout is **not** the one asked for — either a `--set` whose read-back after saving disagrees with the request, or a `--expect` assertion that does not hold. Its own code because nothing was named wrongly and re-running with a different argument does not fix it; and never a success-with-a-note, because this failure is invisible everywhere else — an optimized block is not an error to a classic-S7comm reader, it is simply absent, and no compile, `drift-check` or `sanity-check` can see the attribute at all |
| 16 | `DownloadPlanIncomplete` | `download-plan` ran, nothing went wrong, and it could **not obtain a `DownloadProvider`** from any object in the device's tree — so the report answers none of the questions the command exists to answer, and its calm appearance is not evidence about anything. Same family as 11–14: not a failure (nothing threw, no argument was wrong, re-running changes nothing) and emphatically not a success. Says nothing about whether a download would be *permitted* — that is the write fence's question, which this command never asks |
| 17 | `ChangeAbandoned` | A write command failed and **the project is unchanged** — nothing was saved, and the partial mutation was removed from the open session. Earned by `create-instance-db` when the new instance DB comes back with an invalid block number (FI-63) or its number cannot be read back at all. Its own code because nothing was named wrongly (so not `7`) and because it is a modelled outcome with a known recovery, not an internal fault (so not `5`). The half a caller reads off it: **there is nothing to clean up, and a retry is safe.** Until 2026-08-13 this same condition exited `5` having already *saved* the broken block, so the exit code and the project disagreed about whether anything had happened |
| 18 | `RollbackIncomplete` | The `17` failure with its cleanup half missing: nothing was saved, so **nothing reached disk**, but the partial mutation could not be removed from the in-memory project model either. A separate code because the caller's response differs — which is this table's rule for when to split one (cf. `11`, which does not). On `17` a retry is immediately safe; on `18` the open Portal session holds a block that exists nowhere on disk, so if that session belongs to a person rather than to this process, close it **without saving** — advice that would be actively wrong on a `17` |
| 19 | `DuplicateBlockNumber` | The project contains **two or more blocks holding the same number** on one device (2026-08-13). Earned by `sanity-check`, and by `import`/`import-all` when the project holds a collision after the files went in. **Not `9`, and that separation is the whole point:** `9` means "something is inconsistent or a device failed to compile", and the measured project was *perfectly consistent and compiled clean* while holding two blocks at FC 910 — folding this into `9` would put a real defect behind a code whose documented remedy (compile the listed blocks) is exactly what **erased the only signal there was**. Not `7` either: nothing was named wrongly and re-running with a different argument does not fix it. **It outranks every other non-zero verdict here** (`9`, `13`) — those either clear themselves on the next pass or announce themselves again, and a duplicate does neither; both reports are printed in full regardless, so the ranking hides nothing. On the import path it does **not** mean the import failed: the files went in and were saved, and the message says so |

