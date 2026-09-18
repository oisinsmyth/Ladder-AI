# openness-cli — Portal process control

🔴 `portal-close` TERMINATES Portal processes. It is `--yes`-gated and sweeps empty ones by default. **Never run it live.** It is kept in its own document deliberately: burying the one destructive command inside a larger file lowers its visibility, which is the single thing this section cannot afford.

> Split out of `src/openness-cli/README.md` on 2026-09-17. That file remains the index and the invocation contract.

## `portal-close` — closing a stray Portal (2026-08-23)

```
openness-cli portal-close [--pid <n>]... [--json] [--tia-install <dir>] [--timeout-connect <s>] [--timeout-open <s>] --yes
```

🔴 **THIS COMMAND TERMINATES OPERATING-SYSTEM PROCESSES. It is the only thing in this CLI that does.**
It is `portal-status`'s destructive sibling: the same read-only two-source enumeration, and then it
acts on what it read. No `<project>` positional — it targets Portal *processes*, not a project — and a
positional argument is a usage error.

🔴 **IT HAS NEVER BEEN RUN WITH `--yes`.** Built 2026-08-23 (`f9abeb1`), unit-tested offline
(`PortalCloseTests`, `PortalCloseCommandTests`). Its **plan form was run live for the first time on
2026-08-23**, against a five-process machine, while proving the attachment guard below — two runs,
exit 10 both times, nothing attached to, saved or terminated. **Nothing has ever been terminated by
this command.** Everything below other than the attachment measurements is still derived from the
source and from unit tests over hand-built process lists. Treat the first `--yes` run as an
experiment — read the plan output first, and record what happened.

**Owner ruling, 2026-08-23**, quoted verbatim where the code that implements it lives
(`OpennessCli/Openness/PortalClosePlanner.cs:62-63`):

> **"save where you can, then close"**

**Every close is a terminate, and that is not a design choice.** `TiaPortal` exposes no `Close`,
`Exit` or `Quit` — verified against `Siemens.Engineering.dll` V20, whose only teardown member is
`Dispose()`, and `Dispose()` on an `Attach()`ed handle releases *this tool's own reference and nothing
else* (`OpennessGateway.DisposeAllExcept` depends on exactly that). So for any process this tool did
not launch, "close" means killing the OS process, and **the only variable is whether a save happened
first.** An earlier draft of the enum modelled a "graceful close through Openness" for empty
instances; there is no such operation, and it is recorded in
`PortalClosePlanner.cs:19-21` rather than quietly corrected.

### What it does to each process

The planner (`PortalClosePlanner.Plan`) is pure — no Siemens types, no COM — and classifies through
`PortalStatusClassifier`, so the four buckets are `portal-status`'s. One of four `PortalCloseMethod`
values comes out per process:

| method | applies to | what happens |
|---|---|---|
| `Leave` | anything not selected | Nothing is attached to, saved or terminated. The `Reason` says why |
| `SaveThenTerminate` | **in-use** (a project open) **and named by `--pid`** | Attach to that one pid, **`Project.Save()` every open project**, release the handle, then terminate |
| `TerminateEmpty` | **self-launched-orphan**, and **stray-empty** — in both cases only when **nothing is attached to it** (or when named by `--pid`) | Terminate. Openness can see it, nothing is open and nobody is attached, so there is nothing to save |
| `TerminateUnsaveable` | **Openness-invisible**, once past the age floor or when named | 🔴 **Terminate WITHOUT saving.** Openness cannot see the process, so there is nothing to attach to and nothing to ask. Anything unsaved in it is gone |

🔴 **AN UNATTACHED `StrayEmpty` IS TERMINATED BY DEFAULT — a bare `portal-close --yes` takes it, with
no `--pid`.** That bucket is *"an empty instance this tool did not launch"*, and the planner's own
reason text says what that can be: **a human's window, a Portal sitting on the first-connect approval
dialog, or genuine stale pileup**. It still cannot tell those three apart. What it *can* now tell apart,
since 2026-08-23, is whether **anybody is attached to it** — and if somebody is, it is `Leave` unless
named by `--pid` (see the attachment section below). That is the one case where it used to disagree
with `portal-status`, which classifies a stray identically and then does nothing about it precisely
because it *"could be a human's own empty window"*.

🔴 **The dangerous target is the one with a project open, and it is NEVER in the default set.** An
in-use Portal may be a person's session or another agent's; closing it costs someone their afternoon
even when the save succeeds. It is closable, but **only when named by `--pid`**, so that no sweep can
take one by accident.

### The guards, and what each is for

- **`--yes` or nothing happens.** Without it the command prints the full plan (`PORTAL-CLOSE PLAN`,
  one entry per process with class, method and reason), writes to stderr that *"nothing was attached
  to, saved or terminated"*, and exits **10 (`NotConfirmed`)**. Note the wording is deliberately
  weaker than the usual *"Portal was not contacted"*: the process list **was** read, because that read
  is the only way to know what the plan is (`Program.cs:1612-1621`).
- **An empty Portal somebody is ATTACHED to is `Leave`, not `TerminateEmpty`** — named or not at all,
  the same protection an in-use Portal has. Applies to **both** empty buckets. The refusal names the
  holding pid. Measured, both directions; see the attachment section below.
- **A minimum age floor of 5 minutes** (`PortalClosePlanner.DefaultMinimumAgeMinutes`) on
  Openness-invisible processes. *"Still starting"* is a documented cause of invisibility — as is *"has
  just died"* — and killing a Portal three seconds into launching would be this command creating the
  pileup it exists to clear. An invisible process whose **start time cannot be read at all** is left
  alone for the same reason. `--pid` is the only route past the floor.
- **A failed save is a FULL STOP, not a step to push past.** If a save was possible and did not
  succeed, the process is **left running** and reported as `SaveFailedNotTerminated`
  (`PortalCloseExecution.cs:167-173`). Terminating then would destroy exactly the work the ruling
  exists to protect. A timed-out attach counts as a failed save, so it does not terminate either —
  `--timeout-connect` (default 180 s) bounds that attach, and unlike `portal-status` this command
  genuinely uses the value. (`--timeout-open` is accepted and **inert** — nothing here opens a
  project. Same shape-consistency reason `portal-status` carries both, documented rather than
  removed.)
- **The pid is re-checked immediately before the kill.** Pids are reused, and this command by
  definition runs on a machine where Portal processes are dying. `PortalCloseIdentity.Mismatch`
  compares the process's **current name against `OpennessGateway.PortalProcessNames`** (one shared
  list, not a copy) **and its current start time against the one the plan was made about**, with a
  1-second tolerance for clock granularity. It fails **closed** in every direction — an unreadable
  name, a non-Portal name, a start time that disagrees or cannot be read all mean *do not terminate*,
  reported as `IdentityChangedNotTerminated`.
- **A `Leave` can never reach the code that kills.** `Program` builds the `LeftAlone` outcome itself
  and never calls the gateway for one; `PortalCloseSequence.Execute` **throws** on a `Leave` rather
  than no-op'ing, and `IOpennessGateway.ClosePortalProcess` refuses one outright.
- **The save branch attaches to ONE NAMED PID**, never to `GetProcesses()[0]`, and never launches a
  Portal.

### Output

Both forms carry **two** summaries, because they answer different questions and a run where they
disagree is the run worth reading: `SELECTED:` is the planner's denominator (*"2 of 5 Portal
process(es) selected; 1 will be SAVED first; 1 CANNOT be saved …"*, printed on every run including
`NOTHING EXAMINED - no TIA Portal processes are running`), and `RESULT:` is what actually happened,
counted **per outcome kind and never rolled up into one word "closed"**. A run that terminated three
processes one of which could not be saved is not the same event as a run that saved all three, and
that difference is invisible in any total that adds them.

A loud `*** … ***` footer is printed when there is something to be loud about, and the two cases are
kept apart because they send the reader to different places: **terminated without being saved** (work
destroyed, pids listed) versus **could not be saved and was therefore left running** (work survived,
a process is still there). `--json` carries `executed`, `selected`, `summary`, `warnings`,
`anyFailed` and a per-process `outcomes` array.

### Exit codes

**0** when every selected process reached a terminal outcome (including a run that found nothing to
close — see the note below), and **7 (`CommandError`)** when any outcome is a failure:
`SaveFailedNotTerminated`, `TerminateFailed`, or `IdentityChangedNotTerminated`. **10
(`NotConfirmed`)** for the plan-only form. Argument errors are **1** as everywhere else.

**A run that found nothing to close is a SUCCESS, not an empty-is-not-clean case** — deliberately
unlike the compile gate. This command's whole purpose is that there be no stray Portal processes, so
*"there are none"* is the desired end state rather than an unanswered question. The summary still says
outright that nothing was examined.

### It CAN ask who is attached — closed 2026-08-23, and here is what it can still not see

This section used to read *"the known blind spot: it cannot ask who is attached"*. It was a **prose
warning where a mechanism belonged**: the planner's own `StrayEmpty` reason text said the process *"may
be somebody's window and closing it will be noticed"*, and then the default sweep took it anyway. In a
repo that runs many agents concurrently, an agent attached to an empty Portal was **indistinguishable
from abandoned pileup**.

`TiaPortalProcess.AttachedSessions` was **probed live** rather than reasoned about. Full measurements —
every state, verbatim — are in `docs/notes/openness-api-surface-v20.md`; the short version:

- An empty Portal nobody is attached to returns **0 items**.
- While another process holds an `Attach()`ed handle, **a third process** reading the same property gets
  **1 item** naming the holder's own **`ProcessId` and `ProcessPath`**. Cross-process visibility is the
  whole point, and it is measured, not inferred.
- After the holder disposes — or is **hard-killed without disposing** — it is **0** again. No stale
  session outlives a crashed client, so this guard cannot be jammed shut.
- `IsActive` was **`False` throughout a live attachment**. It does not mean "attached"; nothing reads it.

**What changed.** `PortalProcessInfo` gained `AttachedSessionCount` (`int?`) and
`AttachedSessionHolders`; `portal-status` gained an **`ATTACHED`** column; and in the planner, an **empty
Portal with a live session is `Leave` unless named by `--pid`** — the same *named-or-not-at-all*
protection an in-use Portal already had. The reason text names the holding pid, so an operator who
overrides it does so with the pid in front of them.

Both empty buckets are guarded, not just `StrayEmpty`. The registry mark behind `SelfLaunchedOrphan` is
machine-wide, so *"this tool launched it"* does not mean *"nobody is using it"* — another agent's
`openness-cli`, mid-run and not yet into a project, lands in that bucket.

**Measured live, both directions, 2026-08-23** (plan-only form — and this was `portal-close`'s first
live run of any kind):

| state of pid 19388 (empty, self-launched orphan) | plan |
|---|---|
| nothing attached | `SELECTED: 3 of 5` — `TERMINATE (nothing to save)` |
| one `openness-cli` holding a session | `SELECTED: 2 of 5` — `LEAVE ALONE … Held by: pid 17436 openness-cli.exe` |

🔴 **What the guard still cannot see.** An **Openness-invisible** process is not in `GetProcesses()` at
all, so it can never report an attached session — its count is `null`, meaning **not read**, which is
never treated as zero but is also not treated as attached. That bucket is swept **exactly as blindly as
before**, protected only by the 5-minute age floor. Making "unreadable" mean "attached" would render
every uninterrogable process permanently unsweepable, which is the pileup this command exists to clear.
A guard that protects everything protects nothing.

The same `null`-is-not-zero rule binds `--json` consumers: `attachedSessionCount` is absent/null when
the question was not answered, and `0` only when it was.

