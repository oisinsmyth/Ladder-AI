# device-guard — the test-rig safeguard (ADR-0008)

The fail-closed fence that must exist **before** any live-device read is built. It answers exactly
one question: *is this target device an approved test rig?* If the answer is not a clear yes, it
refuses.

This is the safeguard ADR-0008 requires. Removing hard rule 6 lifted the *prohibition* on hardware
access; it did not create the *safeguard*. This is that safeguard. Until a read-only fetch client is
built and made to consult this gate, nothing in the repo talks to a device — and even then, the write
ban (`10-non-goals.md` #3) is permanent and unaffected.

## What it does — and deliberately does not

- **Does:** load a test-rig allowlist, and refuse any address that is not an *exact* match to an
  entry explicitly marked `"kind": "test-rig"`.
- **Fails closed everywhere:** no allowlist configured, file missing, file unreadable/malformed, file
  present-but-empty, address not listed, address listed but not as a rig → all **refused**. A parse
  error never reads as "no rigs, but fine".
- **Exact match only:** no prefix or subnet matching. `10.10.10.1` never authorizes `10.10.10.15`.
  IPs are canonicalized and hostnames compared case-insensitively; that only ever tightens a match,
  never widens one.
- **Does NOT enforce read-vs-write.** The guard sees an address, not an operation. Read-only-ness is
  the *fetch client's* job — it must expose read/fetch verbs only, with no download/online-edit/force
  path (write to hardware is permanently banned regardless).
- **Does NOT see device contents,** so it cannot enforce hard rule 2 (safety). The fetch client must
  refuse safety-tagged content off a device exactly as the export path does.

## Allowlist

JSON, resolved from `--allowlist <path>` or the `LADDER_DEVICE_ALLOWLIST` environment variable.
**There is no default path** — with neither set, there is no allowlist and every target is refused.
See `allowlist.example.json`. Adding a device asserts it is a non-production test rig; keep the real
allowlist somewhere deliberate and reviewed, not auto-discovered.

```json
{
  "entries": [
    { "address": "10.10.10.15", "label": "Bench HMI", "kind": "test-rig",
      "approvedBy": "you", "approvedDate": "2026-08-10" }
  ]
}
```

## Usage

```
device-guard check <address> [--allowlist <path>] [--json]   # 0 allowed, 1 refused, 2 usage
device-guard list            [--allowlist <path>] [--json]   # show honored rigs (or that there are none)
device-guard where           [--allowlist <path>] [--json]   # show which allowlist path resolved
```

A future fetch client either shells `device-guard check <addr>` and proceeds only on exit 0, or links
this assembly and calls `DeviceAccessGuard.FromPath(path).Check(addr)` — same decision either way.

## Build & test

```
dotnet test src/device-guard/device-guard.sln     # unit tests (pure logic — safe any time)
dotnet build -c Release src/device-guard/device-guard.sln
```

net8.0, no Siemens.Engineering, no network dependency — so a rebuild here never triggers the TIA
Openness `(Path, FileHash)` re-approval that an `openness-cli` rebuild does.
