# device-fetch — read-only device file fetch (ADR-0008)

The read-only fetch client. It pulls a named resource off an **approved test rig** over HTTPS, gated
by `device-guard`. It is the counterpart to the safeguard: the guard decides *which* device may be
touched; this client does the touching — read-only, one GET at a time.

## Guarantees by construction

- **Guard first.** Every fetch calls `DeviceAccessGuard.Check(target)` before any network I/O. A
  target the allowlist refuses is never contacted (there is a test proving the HTTP getter is not
  even invoked on a refused target).
- **Read-only.** The only verb is `get`, and the only network method in the assembly is HTTP GET.
  There is no Post/Put/Delete/upload anywhere — writing to hardware stays permanently banned
  (`10-non-goals.md` #3).
- **No credential guessing.** A protected resource returns `AUTH REQUIRED` (401/403) rather than a
  retry or a guess. Credentials are supplied by the caller: a `--cookie` from an established session,
  or `--user` with a password read **only** from the environment variable named by `--password-env`.
  Nothing is hardcoded or logged.
- **Safety refusal (coarse).** A resource whose path or content looks like safety-program material is
  refused (hard rule 2). This is a conservative name/marker check over HTTP, not the export-time
  safety enforcement — it errs toward refusal.
- **Self-signed TLS tolerated**, scoped to this tool: test rigs use their own CA, so machine-store
  chain validation would always fail. Built on .NET 8's HttpClient, which negotiates the HMI's modern
  TLS (the Windows PowerShell / .NET Framework stack could not).

## Usage

```
device-fetch get --target <addr> --path </resource> --out <file>
                 [--allowlist <path>] [--cookie <session>]
                 [--user <name> --password-env <ENVVAR>] [--json]
```

Allowlist path comes from `--allowlist` or `LADDER_DEVICE_ALLOWLIST` (same as `device-guard`). Write
output to a path **outside the repo** — fetched device content is never committed (retention rule).

Verified live against a WinCC Unified HMI: pulled `/device/WebRH/ca.crt` (unauthenticated) end to end
through the guarded chain.

### Caveat: Git Bash mangles `/`-leading paths

Under MSYS/Git Bash, an argument like `--path /device/WebRH/ca.crt` is rewritten to a Windows path
(`C:/Program Files/Git/device/...`). Invoke from **PowerShell**, or prefix the Git Bash command with
`MSYS_NO_PATHCONV=1`. This is a shell artifact, not a tool bug — the tool builds the correct URL from
the argument it actually receives.

## Build & test

```
dotnet test src/device-guard/device-guard.sln
dotnet build -c Release src/device-guard/device-guard.sln
```
