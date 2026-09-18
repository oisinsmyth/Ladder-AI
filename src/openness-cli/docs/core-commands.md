# openness-cli — the core program commands

Per-command usage for the block and program surface, plus the compile-scope findings that govern what a compile actually proves. Those findings lived under `## Exit codes` until 2026-09-17, which is where nobody looked for them.

> Split out of `src/openness-cli/README.md` on 2026-09-17. That file remains the index and the invocation contract.

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

🔴 **`--out` IS A FILE PATH, AND A PRE-EXISTING DIRECTORY THERE IS EXIT 5 — so `mkdir -p` first is exactly wrong.** Measured 2026-08-21: `export … --out <an existing dir>` throws *"Cannot export to the specified location because a directory with the name '…' already exists"*, which surfaces as the **catch-all `UnexpectedError` (5)** and therefore reads like a bug rather than a usage error. The trap is that the paragraph above is true of a pre-existing **file** — that one is deleted for you — so the natural inference is that `--out` is somewhere to put things, and the natural habit of creating the directory first is the one thing that guarantees failure. Point `--out` at the **file you want written**, and let its parent exist. (`export-all` and `library --export-version` are the opposite: those take a **directory**.)

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

🔴 **A `--block` compile's message tree is PROGRAM-WIDE, and its `ERRORS:` line counts the whole program, not the block (2026-08-24).** Measured on a real project: a per-block compile of a block that had just been repaired printed `STATE: Error` and `ERRORS: 7`, while the verdict lines for that same block read *"Block was successfully compiled"* and `CONSISTENT: yes`. **Not one of the seven belonged to the block being compiled.** Two were real errors in an unrelated block in a different folder — not a dependency, not in scope, not touched by the import. Four were the structural parent nodes TIA emits above them (`<device>:`, `Program blocks:`, `<folder>:`, `<the other block>:`), each carrying `Error` state and an **empty description**. The seventh was the `Compiling finished (errors: 2; warnings: 1)` rollup. The compiler's own `ErrorCount` was `2`, and the `NOTE` this table prints — declaring the compiler's aggregates unreliable and preferring the message-tree count — was, in this direction, backwards.

Two consequences, and the first is the one that misleads:

- **On a `--block` or `--type` run, the only per-item verdict is the `[Success] <name>:` line plus `CONSISTENT:`.** `STATE:` and `ERRORS:` describe the compile TIA actually performed, which takes in the item's dependencies and — observed here — blocks that are not dependencies at all. **Do not read `ERRORS: n` on a per-block run as that block's error count.** `sanity-check`'s `INCONSISTENT:` enumeration remains the thing to quote as evidence.

⚠️ **The two halves have different blast radii, and the counting half is everywhere.** The *scope* defect is specific to `--block`/`--type`. The *counting* defect is not: measured the same day on the same project, `sanity-check`'s station device-compile line read `Error (errors=9, warnings=0)` while the compiler's own tail in that very block read `Compiling finished (errors: 2; warnings: 0)` — **six of the nine were empty `[Error]` tree headers**, printed as blank lines. So on `--station`, `compile-all` and `sanity-check` the **scope** is honest, but the **count still inflates**. Quote the enumeration, not the tally.
- **The exit code is unaffected and stays correct**, because it is fail-closed — the larger of the two counts. It cannot read a dirty program as clean. But note what it therefore is: a **program-wide** gate wearing a per-block command's clothes. A genuinely clean block sitting in a program that has an unrelated broken block still exits non-zero.

**Counting is not fixed, deliberately.** Counting only *leaf* messages would drop the four structural nodes and reconcile both observations — this one, and the older case behind the `NOTE` where the compiler reported `WARNINGS: 0` against 156 warning messages. But `CompileMessage` is **flattened at collection**: `CollectMessages` recurses the nested `CompilerResultMessage` tree and keeps every node with no parent/child marker, so the leaf/parent distinction no longer exists by the time the formatter sees it. Restoring it needs a model change plus a live compile to confirm the real tree shape, and the shape above is inferred from pre-order output, not observed. Logged rather than guessed at.

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
   `Success (errors=0, warnings=0)`, and TIA then refused `export --type UDT_Rack` as inconsistent.
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

**The `ATTACHED` column (2026-08-23)** says whether anyone currently holds an Openness session on each
process, read from `TiaPortalProcess.AttachedSessions` — also without attaching. It prints **three**
states, not two, and the difference is load-bearing: a **holder** (`1: pid 19536 openness-cli.exe` — the
attaching client's pid and exe, so you can go and look at it), a measured **`none`**, and
**`(not visible to Openness)`** for a process that is not in `GetProcesses()` at all and therefore was
never asked. `--json` carries `attachedSessionCount` (`null` = not read, never "zero") and
`attachedSessionHolders`. What was measured to establish all this is in
`docs/notes/openness-api-surface-v20.md`; it is what lets `portal-close` refuse to sweep an empty Portal
somebody is working in.

Output includes a short human note inferring the likely cause from the counts (one stray reads as a
probable first-connect-dialog wait or human window; several strays match the pileup symptom). This
is the read-only, safe subset of the parked FI-07 janitor — **killing stays out of scope *for this
command*** (FI-07 needs a `--yes`/dry-run pattern and a safe "idle" definition so a mid-compile
Portal is never touched; there is no is-compiling flag on the Openness side, so the tool cannot
tell). ⚠️ **This paragraph read "killing stays out of scope" without qualification until 2026-08-23**,
by which date the owner had ruled and `portal-close` existed (`f9abeb1`) — the killing half now has
its own command, with its own `--yes` gate and its own section below. `portal-status` itself is
unchanged and still never kills anything. Because a
stray is frequently a legitimate human window, this is **not a gate**: it is purely informational
and **always exits 0**, in both table and `--json` form.

The classification and formatting are pure and unit-tested (`PortalStatusTests`); the
`GetProcesses()` enumeration itself is integration-only (needs a live Portal).

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

`--tagtables` is opt-in. **The rationale once given for that — "a tag table has no `.ir` counterpart
in the shape the corpus uses, so including it by default would manufacture `EXPORT-ONLY` findings
that mean nothing" — is false for this project's own reference corpus, and was corrected 2026-08-23.**
`ir/test-project001` carries **both** tag tables as `.ir`, and both paired cleanly on the declared
name, including the cross-name pair `DefaultTagTable.ir` ↔ `Default tag table.xml`. The pairing
failure the old wording predicted did not occur.

The default is left as it is — opt-in is still defensible for a corpus that genuinely lacks
tag-table IR — but **know what it costs where the IR exists.** Two consecutive `drift-check` legs
against `test-project001` **on 2026-08-23** reported `3 drifted / 38 match` over 41 objects with both
tag tables `SKIPPED`; the next run, with `--tagtables`, compared 43 and reported `5 drifted` — the
two extra being the register map (`HarnessMirror`) and `DefaultTagTable`. Neither earlier leg was
wrong — each correctly reported what it compared, which is what the `COMPARED:` line is for — but
the denominator was smaller than the reader assumed. **Pass `--tagtables` whenever the corpus has
tag-table IR to compare against.** (`docs/notes/test-environment-contract.md` §2.9.)

> 🔴 **CORRECTION 2026-08-23.** This paragraph read *"the **first** run passing `--tagtables` reported
> `5 drifted`, and the two it had been hiding…"*. **Both halves are false.** The first run passing
> `--tagtables` was **2026-08-14 05:10**, and it reported **4 drifted / 38 match / 3 export-only**
> (`docs/notes/test-log.tsv:68`) — `HarnessMirror` was compared there and **MATCHED**; the drift in it
> was created later the same day by `084b778` (11:50:04) and has never been deployed. `DefaultTagTable`
> was in that dump too but was **mis-paired** by the space-in-name defect, so it went uncompared —
> mis-paired, not skipped for want of an export. **The mistake was inferring a lesson from a flag's
> name without checking which command owns the flag:** `--tagtables` belongs to `export-all`;
> **`drift-check` has no such flag**, and its `SKIPPED` means only *"no paired `.xml` in the exports
> dir"* (`src/converter/Converter/DriftCheck/DriftCheckRunner.cs:50-54`). The `SKIPPED`-both-times
> finding is real, and it is **exactly two runs wide** — the two 2026-08-23 third-leg legs above.

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

