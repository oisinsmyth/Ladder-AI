# Accepted deviation from `docs/10-non-goals.md` #4(b) — 2026-08-21

**Written BEFORE the device write it covers, deliberately.** A deviation recorded afterwards is a
justification; recorded beforehand it is a decision. This one is the owner's, taken in session.

## What the rule requires

`docs/10-non-goals.md` #4(b), and `CLAUDE.md` hard rule 5's closing sentence:

> No AI writes to a device or to a project without a **verified restore point captured first** — the
> prior content recorded, and provably restorable. If the restore point cannot be captured, **the
> write does not happen**. … the hard case is retentive data and DB actual values, which a download
> can silently reinitialise: **a restore point that captures only blocks is not a restore point.**

## Why it cannot be satisfied — this is a tooling gap, not a permissions question

Established by reading the code, not inferred:

- `src/harness/Harness.S7/RestorePoints.cs` defines `RestorePointScope.ProgramAndData` — the kind a
  download needs — and documents it as *"**Nothing in this repository produces one yet**."*
- `FileRestorePointStore` *only ever* produces `ProcessDataOnly`, and **permanently refuses** a
  caller declaring it needs `ProgramAndData`, with the refusal text citing this very rule: *"Per
  10-non-goals.md #4(b) a partial restore point is not a restore point."*
- **Nothing on the download path even asks.** `src/harness/Harness.Device/` and
  `src/openness-cli/DownloadProbe/` contain no reference to a restore-point store;
  `DeviceWriteGuard`, where the precondition is enforced, is referenced by six other components and
  **not** by the download path.
- The store has never been used on this machine: `~/.ladder/` holds `claims`, `lib`,
  `stage-harness-s7` and `device-allowlist.json`, and **no `restore-points` directory exists**.

So there is no command that captures a download-grade restore point, and no check that would stop a
download for lacking one.

## What is being done instead, and what it does not cover

The de-facto practice — a **project-level** baseline:

```
openness-cli export-all <project> --tagtables --out <baseline>
# restore = delete-all -> import-all -> compile-all -> sanity-check
# verify  = converter compare <baseline> <after>
```

This is proven: `tools/download-probe.allowlist` records 36 blocks deleted and 45 restored in three
fixpoint passes, 45/45 Normalizer-equivalent, on the rig project itself.

**It is exactly the thing #4(b) says is not a restore point.** It captures the offline project —
blocks — and not retentive data or DB actual values on the controller. The last real download to
this rig answered `DataBlockReinitialization -> StopPlcAndReinitialize` and `StopModules -> StopAll`,
and the live job recorded the consequence in its own notes: *"Retentive data reset by the STOP/START
the download took."*

## What is accepted, by whom

**Owner decision, 2026-08-21, in session:** proceed, accepting that retentive and DB actual values
on the bench rig will be reinitialised by the download, with this deviation recorded.

Standing authorisation already covers the act itself — `docs/notes/owner-decisions-2026-08-14.txt`
Q-12: *"You are free to download overwrite, anything you need to do in the test environment and the
current rig"*, taken as standing authorisation for the deploy and for redeploys, and
`docs/notes/autonomous-working-agreement.md` lists *"Download to the bench rig"* under
**PRE-AUTHORISED — do these, do not ask**.

What that authorisation does **not** do is satisfy #4(b), which is why this page exists.

## Why the residual risk is small here, and where it would not be

The bench rig is **standalone with no I/O wired and outputs isolated** — `~/.ladder/device-allowlist.json`
records `"outputsIsolated": true`, asserted by the owner 2026-08-11, and the label reads *"CPU 1214C,
standalone, no I/O wired"*. Reinitialised retentive data on a rig driving nothing is a nuisance, not
a hazard.

**This reasoning does not transfer.** On a device in service the same gap is the difference between
a recoverable mistake and an unrecoverable one, and #4(b) would forbid the write outright — as would
`docs/10` #3 and ADR-0009, which put a device in service out of scope entirely.

## What would close it

A `ProgramAndData` restore-point store: capture retentive areas and DB actual values off the
controller before the write, verify by read-back rather than by assuming, and wire the precondition
into the download path so a missing restore point refuses the download instead of being unasked.
`FileRestorePointStore`'s own comment already anticipates it — *"When a download path is built it
will need its own store."*

Worth its own plan. It is the one gap in this toolchain where the letter of a hard rule cannot be
met by any available command.
