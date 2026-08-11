# rig-write — plan one governed device write, and stop

`rig-write` is the runnable entry point for the ADR-0009 write fence. Until it existed, the fence
(`src/device-guard/`) and the transport (`src/harness/Harness.S7/`) were libraries with no way to
invoke them together, so "would this write be allowed?" could only be answered by writing a program.

**This build cannot write to a device.** It opens no socket, sends no packet and resolves no host. It
decides every gate that can be decided from files, names the ones only the device can answer, and
stops. There is no flag that arms it — see `Arming.cs`: the capability is absent from the assembly,
not switched off. The CLI is handed a client factory that throws and never calls it, which
`RigWriteCliTests` asserts rather than describes.

## The one command

```
rig-write plan --target 10.10.10.10 --allowlist %USERPROFILE%\.ladder\device-allowlist.json
```

Everything else defaults to the rig marker DB, so that plans the proposed first governed write. Add
`--json` for a machine-readable form, `--restore-dir <dir>` to point at restore-point manifests
(default `%USERPROFILE%\.ladder\restore-points`), and `rig-write layout` to print the marker DB's
computed offsets and the arithmetic behind them.

Exit codes: `0` nothing decidable offline refuses this write · `1` a gate or precondition refuses ·
`2` usage · `3` arming was requested and this build has no such capability.

## The nine steps it plans

| # | Step | Decided by |
|---|---|---|
| 1 | Resolve the target in the allowlist | files |
| 2 | Build the identity sources the entry needs | files |
| 3 | Connect | **the device** |
| 4 | Read the device's identity, on this session | **the device** |
| 5 | Capture and verify a restore point covering the bytes | **the device** (a read) + disk |
| 6 | Ask the write fence — `DeviceWriteGuard`, seven gates | files, given 4 and 5 |
| 7 | WRITE | not in this build |
| 8 | Verify by reading the same bytes back | not in this build |
| 9 | Restore the captured bytes and confirm by re-reading | not in this build |

A step the device decides is reported `DEFERRED` — an open question, never a pass. The one exception
is gate 5 of the fence: a dry run cannot read the device, so the identity the allowlist *declares*
stands in for the one it would report, and the plan says so (`GATE 5 ASSUMED`). Every other gate is
decided for real.

The planner does **not** re-implement the fence. Gate order lives in `DeviceWriteGuard` and nowhere
else; a second statement of the same rules would eventually disagree with the first, and a person
would be told the wrong reason a write was refused.

## The restore point

For a data write into a DB, a restore point is **the bytes that were there before** — captured with
`FileRestorePointStore` (`RestorePointScope.ProcessDataOnly`), verified by reading the manifest back
off disk and hash-comparing it, and put back by `Restore`, which confirms itself by re-reading the
device. That store is fully implemented and tested against an in-memory device. What it has never had
is a device: **capture is a device read**, so nothing in this build performs one.

It does not cover a download, an online edit or a hardware-configuration change — a copy of some DB
bytes cannot undo any of those, and a caller declaring `ProgramAndData` is refused permanently rather
than handed something that looks close enough.

**One hole the fence cannot see, and this planner can.** The fence asks "does a verified restore point
exist for this target?", and the store answers by comparing *area names*, because an area is the
vocabulary the fence is scoped on. So a two-byte capture labelled `DB_RigMarker` satisfies gate 7 for
a thirty-four-byte write into that area, and the restore would put back two of the thirty-four bytes
and report success. `rig-write` knows the write's exact extent, so it asks the byte-level question
(`RestorePointManifest.Covers`) and reports a restore point that passes the fence while missing the
bytes as a blocker in its own right.

## What arming would take

Three separate acts, and no two of them together are enough:

1. `writeEligible: true` on the allowlist entry — the owner's decision, and the only gate that is a
   pure judgement rather than a fact about a file, a device or a captured restore point.
2. A live `IS7Client` factory in `Program.cs` (`Sharp7Client`), plus an execute path that captures the
   restore point through `S7RegionAccess` before it writes anything.
3. An identity the device can actually report — an entry declaring a serial no configured source can
   read is refused as a *configuration* fault, and the tempting fix (delete the serial) silently
   downgrades the check from "this exact box" to "a box of this model".
