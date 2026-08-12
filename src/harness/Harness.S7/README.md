# Harness.S7 — the transport, and the place the fence gets consulted

`Harness` (the runner) and `DeviceGuard` (the fence) do not know each other exists. That is
deliberate: neither drags the other's dependencies, and both are testable alone. It also means there
has to be exactly one place where the two meet, and this is it.

```
Harness              DeviceGuard
   |  ITransport         |  DeviceWriteGuard / DeviceAccessGuard
   +---------+-----------+
             |
        Harness.S7            <- this project
             |  IS7Client
          Sharp7  ->  classic S7comm  ->  S7-1200
```

## What is here

| Piece | What it is for |
|---|---|
| `IS7Client` | Everything the transport may do to a device. Sharp7's own surface *minus* `PlcStop`, `Download`, `Delete`, `PlcCopyRamToRom` — a capability that is not on the interface cannot be reached from the harness at all. |
| `Sharp7Client` | The real adapter. The only file that cannot be unit-tested, so it does nothing but marshal. |
| `S7TagMap` | Symbolic name → DB/offset/type. Classic S7comm has no symbolic access, so the binding lives here, with duplicate/overlap/alignment checks. |
| `S7Values` | Big-endian encode/decode, invariant-culture formatting, range checks that refuse rather than truncate. |
| `S7RunState` / `S7RunStateReading` | Whether the CPU answered RUN — the fact Modbus cannot supply and Openness does not expose. `Running` / `NotRunning` / `Unknown`, and no `Stopped`: see below. |
| `IDeviceIdentitySource` | Order code today; a rig marker DB and (untried) the CPU's SZL serial. Composed, contradictions refused. |
| `S7Transport` | `ITransport` with the write fence behind it. Identity is re-read on **every** connection. |
| `FileRestorePointStore` | The first `IRestorePointStore` that can answer yes — and the conditions under which it must still say no. |

## Three things worth knowing before changing any of it

**Capabilities are derived, never declared.** `Write` appears because the transport was built with a
fence; `ScanCounter` appears because a scan-counter tag is in the map. `EventScanStamps` and
`LatchedTransients` cannot be turned on at all, because no program under test provides them. A false
capability flag does not produce a wrong answer — it produces a **green** one, by converting a vector
the runner would have refused to evaluate into one it evaluates against data that cannot carry the
answer.

**Identity is per-connection.** On 2026-08-11 a remote tunnel injected `10.10.10.0/24` at metric 0
and shadowed a local segment carrying the same range. Nothing in the routing table announced it, and
it can happen mid-session. A transport that identifies its device once and then silently reconnects
has verified nothing, so every reconnect re-runs the identity sources and the guard always sees the
identity of the session the bytes will travel on.

**The order code identifies a model, not a unit.** Every `6ES7 214-1AG40-0XB0` on earth reports the
same string. It catches "a different *kind* of box answered"; it cannot catch "a different box of the
same kind answered". `MarkerDbIdentitySource` is what closes that, and its DB number, offset and
encoding are still unsettled — hence no defaults.

## The run-state read, and why its decoder must not be tightened

"The CPU went to STOP" and "the link dropped" are the same observation over Modbus, and Openness
exposes no operating mode, so `IS7Client.ReadRunState` is the only source of that fact here. It is a
status request and cannot change a mode; `PlcStop`, `PlcHotStart` and `PlcColdStart` stay off the
interface, so the harness cannot reach them.

**Measured on the rig 2026-08-12, in both states.** RUN answers PDU byte `0x08` and Sharp7 reports 8.
STOP answers **`0x03`**, which is none of Sharp7's three named constants, so it reaches the caller as
4 through Sharp7's **catch-all** arm — `S7CpuStatusStop` is never actually returned by this device.
Confirmed live against the stopped CPU on the same day: `status ok, state NotRunning (4), 78 ms`.

Two consequences the code carries in comments and the tests pin:

- **Do not tighten the decoder** to accept only `{0, 4, 8}`, and do not tighten Sharp7's. On this rig
  either change reports a stopped CPU as not-stopped, which is the failure the read exists to prevent.
- **There is no `Stopped` state, by design.** `Running` is exact (only the pass-through value reaches
  it); everything else is `NotRunning`, which covers STOP, STARTUP, HOLD and any byte nobody has seen.
  A read that did not complete is `Unknown` and never `NotRunning` — "the CPU did not answer RUN" and
  "the CPU could not be asked" are different facts, and telling them apart is the entire point.

## Two conditions in TIA that this code cannot check

Both fail at the first **read**, not at the connect, which makes them confusing the first time:

- the DB must have **"Optimized block access" turned OFF** — an optimized DB has no stable byte
  offsets and is not reachable over classic S7comm at all;
- the CPU's protection settings must **permit PUT/GET communication from a remote partner**.

## Restore points

`DeviceWriteGuard`'s seventh gate asks whether a verified restore point exists, and until now only
`NoRestorePoints` existed, which answers no to everything. `FileRestorePointStore` can answer yes —
and says no when the manifest was never read back after writing, when it does not **cover the areas
this run declared it will write**, when it has been edited, when it is stale, and when the caller
needs a kind of restore point this store cannot produce.

That last one is the important one. This store captures **DB byte ranges only**. It cannot capture
program blocks, and it cannot capture the retentive-data and actual-value reinitialisation that a
**download** performs — the hard case `10-non-goals.md` #4(b) names outright. So it only ever
produces `RestorePointScope.ProcessDataOnly`, and a run declaring `ProgramAndData` is refused
permanently rather than handed something close enough. When a download path is built it will need its
own store, and that refusal is what makes it impossible to forget.

Restore is one operation and its success is **confirmed by re-reading the device**, never assumed. A
region the program rewrites every scan therefore reports as unrestorable, which is the truth.

## Sharp7 — an open decision, not a settled one

Sharp7 v1.1.82 (single file, pure C#, no native dependency, MIT) is **not committed to this
repository**. The working copy lives outside it at `%USERPROFILE%\.ladder\lib\Sharp7.dll`; the
`Sharp7Path` MSBuild property points there by default and can be overridden. Without the DLL the
adapter is excluded from the build and everything else — the transport, the fence wiring, all tests —
still compiles and passes.

How it *should* be referenced needs a decision:

- **vendor the DLL** under a `lib/` folder — reproducible and offline, but an undiffable binary in
  git, and the repo has no precedent for one;
- **a NuGet `PackageReference`** — auditable, versioned, and it slots into the existing central
  package management, but it makes a build need the network;
- **vendor the source** — it is one `.cs` file under MIT, so it can be a file in this project:
  diffable and steppable, at the cost of owning a fork.

## Build & test

```
dotnet test src/harness/harness.sln              # 221 tests, no PLC, no network, no rig
dotnet build -c Release src/harness/harness.sln
dotnet build src/harness/harness.sln -p:Sharp7Path=C:\some\other\Sharp7.dll
```

net8.0 throughout, no `Siemens.Engineering` — so a rebuild here never triggers the TIA Openness
`(Path, FileHash)` re-approval that an `openness-cli` rebuild does.

## Not built here, on purpose

No CLI, no live connection, no rig test. The rig at its allowlist entry is `writeEligible: false` by
design as of 2026-08-11, so the honest transport against it today is `S7Transport.ReadOnly`, and a
stimulus vector run through it reports `NotObservable` — loudly, and not as a pass.
