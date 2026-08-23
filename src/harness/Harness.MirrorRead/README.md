> **This project also owns `IMirrorFeedSource` / `FileMirrorFeedSource`** — the sibling of
> `IRegisterSource` / `ModbusRegisterSource` that reads **a wave's published feed instead of a socket**,
> so `harness-mirror-view --follow` can show a run live without taking its connection. `MB_SERVER` accepts
> ONE connection per instance: with a viewer attached, a conformance wave reported **0 of 22 vectors
> attempted**.
>
> **It is a separate port rather than an `IRegisterSource` implementation, deliberately.** That interface
> answers `ushort[] Read(start, count)` — values, or an exception. A feed read has **four outcomes that must
> stay apart** (*no publisher ever wrote here* / *a publish did not complete* / *unreadable* / *ok*) and it
> carries **per-register provenance**: which registers a real read covered, and *when each one was read*.
> Forcing it through `IRegisterSource` would collapse three non-device conditions into one thrown exception
> that `RegisterRead.Perform` would then report as a **transport failure** — a device diagnosis for
> conditions in which no device was involved. Same seam SHAPE, deliberately not the same TYPE.
>
> The format is `Harness.Wire.MirrorFeed`; the writer is `Harness.Wire.MirrorFeedPublisher`. Full
> description in `src/harness/Harness.MirrorView/README.md`.

# `harness-mirror-read` — READ-ONLY Modbus holding-register reader

Reads holding registers off the rig's `MB_SERVER` mirror and reports **how wide the area actually
is**, measured from both sides.

## Why it exists

`Harness.Wire` has had a working Modbus transport (`NModbusTransport`) and a map-aware client
(`MirrorClient`) since phase 2. **No executable exposed a read.** The only harness binaries are
`harness-gate`, `harness-run`, `rig-read`, `rig-write` and `rig-control`, and every rig observation
therefore went over **S7**.

That matters more than it sounds. `rig-read --marker` addresses `%M` directly and **never consults
`MB_HOLD_REG`** — `MB_SERVER` is not in that path at all. So it can confirm that bytes exist in
marker memory, and read them correctly, while saying **nothing whatsoever** about whether a Modbus
client can see them. When the question is *"does the area pointer's declared width reach the wire?"*,
the S7 path is the one transport that structurally cannot answer it.

## What it measures, and why it takes two probes

| probe | what it establishes |
|---|---|
| the whole declared area, `0..n-1`, in `ceil(n/125)` FC03(s) | a Modbus client can see the area the IR declares |
| single-register probes **inside** the edge | the server answers single reads at all — **the control** |
| a single-register probe at `n`, the first **undeclared** register | the area is **not wider** than declared |

🔴 **The whole-area read is PAGED, and the passing verdict was unreachable until it was.** FC03 carries
at most 125 registers. This step issued ONE request whatever the width, which worked only while the
live run declared 37 — at the rig's real 576, and then 1024, the request was rejected by the client's
own bounds check before a packet left, the run recorded a finding, and **`exit 0` could not be reached
on any real area.** Two runs on 2026-08-23 produced a perfect boundary measurement under
`RESULT: 1 finding(s). This run does NOT pass.`

**Paged rather than declared not-applicable above 125, and the alternative was seriously considered.**
Skipping the step above the protocol limit would leave the boundary sweep as the only thing that reads
anything, and the sweep touches five registers at the edge — registers `4..1020` would be read by
nothing, so a hole in the middle of the mirror would pass every check this tool has. An exemption that
removes the only coverage of 99.5% of the area is the same closed check by a different door.

⚠️ **`NModbusTransport`'s refusal to split still stands, and is about a different span.** Its words are
*"splitting it here would hide a map that was derived wrong; the map refuses this at derivation time"* —
both halves are about a span `MapAllocator` **derived**, where a >125 read means the map cannot be
served and splitting would paper over a design-time defect. Neither premise holds here: this span is
`--declared-registers`, read off `MB_HOLD_REG` by the operator, so there is no derivation to be wrong;
and the split is one layer **above** the transport, counted and named in the output. The transport keeps
refusing. **The boundary probe stays unpaged**, deliberately and with a test pinning it: paging the read
whose refusal *is* the measurement would mask it.

**What a pass does NOT establish, printed on every multi-page run.** `n` transactions are `n` separate
moments on a live mirror — the scan counter moves between them — so the words are a **reassembly, not a
snapshot**. Two registers from different pages never provably held their values at the same instant.
The step measures **reachability**, which is per-register and survives that; it licenses nothing about
coherence. Also unchanged by paging: a refused page is located to the page, not to the register — the
single-register sweep is what localises an edge, and it only sweeps the declared edge, so a hole in the
middle is detected (the page fails) but **not localised**.

**Reading the new registers successfully proves the area is *at least* wide enough. It is equally
consistent with a server exposing far more than intended.** The register one past the declared end
being *refused* is what closes it from the other side, and only the pair pins the width. The tool
refuses (exit 2, before any socket) any `--boundary-from`/`--boundary-to` pair that does not straddle
the declared edge, because a sweep with no inside control cannot tell a real boundary from a server
refusing everything.

**A silence is not a refusal.** A Modbus *exception response* means the server received the request
and refused it — that is the measurement. A timeout means nothing was measured. The two are separate
outcomes (`RefusedByServer` / `TransportFailed`) and no verdict rests on the second.

## Read-only, and how that is checked

The banner claims *"this binary contains no write path"*. That is a claim about the **compiled
assembly**, and the only thing entitled to make it is a walk over the compiled assembly.

`MirrorReadStructureTests` walks **every method body** in `harness-mirror-read.dll` and asserts none
of them names a write member of `Harness.Wire`, `Harness.Map` or `NModbus`, with:

- a **denominator** (`BodiesExamined > 0`) — `Hits.Count == 0` is true of *nothing found* and of
  *nothing looked at*;
- a **live positive control** — `PlantedWriteControl` in the test assembly really does call
  `WriteHoldingRegisters`, and the same walk with the same predicate must find it;
- a **resolution control in the target module** — the walk must find the `ReadHoldingRegisters` that
  `ModbusRegisterSource` certainly makes, or a module whose tokens resolve to nothing would report a
  clean sweep.

This replaces a `const bool` with an `Assert.False` on it. Measured 2026-08-14 on `Harness.RigWrite`:
planting a class that constructed a live socket client left **all 202 tests green**.

**What the claim does NOT cover, stated because it is real.** `Harness.Wire` *does* contain a write —
the harness proper must write vectors and raise start bools — and this binary references that
assembly. The property checked is that nothing here **names** it, not that the capability has been
deleted from the process. `TheResidualIsReal_…` asserts the write is still in `Harness.Wire`, so if it
ever goes away this file's description stops being true **loudly**, rather than decaying in a comment.

## The fence

`DeviceAccessGuard` is consulted **before any socket**, and the ordering is observable rather than
asserted: `IRegisterSourceFactory` is the only route to a connection, and every refusal test asserts
`Opens == 0` — the observable consequence, not the exit code, because a disconnected gate can produce
the right exit code by accident.

⚠️ **`DeviceAccessGuard`, never `DeviceWriteGuard`.** The live allowlist entry reads
`"writeEligible": false`, which is correct and **must not block a read**. A test pins that directly:
a fence "hardened" to consult the write guard would refuse every real run of this tool.

**No allowlist is a refusal, never a pass.** There is no default path.

## Usage

PowerShell (this repo's shell — `%USERPROFILE%` does not expand here):

```powershell
dotnet run --project src\harness\Harness.MirrorRead -c Release -- `
  --address 10.10.10.10 --port 503 --unit 1 `
  --declared-registers 37 `
  --allowlist "$env:USERPROFILE\.ladder\device-allowlist.json"
```

`--declared-registers` is **required and deliberately not defaulted**: it is the claim under test and
it comes from the IR (`MB_HOLD_REG ... WORD n` → registers `0..n-1`). A default would let a run
conclude, plausibly, against a width nobody stated.

| flag | default |
|---|---|
| `--address` | **required** |
| `--declared-registers` | **required** |
| `--allowlist` | `LADDER_DEVICE_ALLOWLIST`, else refused |
| `--port` | `503` (this rig; `502` is refused there) |
| `--unit` | `1` |
| `--boundary-from` | `n-3` |
| `--boundary-to` | `n+1` |
| `--interval-ms` | `3000` — the gap between the two control reads |
| `--scan-retry` | `0` — extra whole measurements, **and only on exit 8** |
| `--scan-retry-interval-ms` | `5000` |
| `--out <path>` | none — write the JSON report there |
| `--json` | off — report on stdout, prose transcript on stderr |

## The JSON report (`--out`, `--json`)

Until 2026-08-23 the only structured output of this tool was its **exit code**: the width, the
denominator, the probe outcomes and every finding reached a reader as console prose and reached a
*caller* not at all. That was survivable while a person typed the width in. It stopped being
survivable when `harness-batch` began feeding it a **derived** width (`BatchStepKind.MirrorWidth`) —
the verdict is then evidence about a deployment, and evidence that lives in scrollback is evidence
nobody can cite.

```jsonc
{
  "target": { "address": "10.10.10.10", "port": 503, "unit": 1 },
  "declaredRegisters": 576, "lastDeclaredRegister": 575,
  "exit": 0, "exitName": "Ok",
  "widthVerdict": "ExactlyAsDeclared",   // | NarrowerThanDeclared | WiderThanDeclared | NotEstablished
  "measured": true,                      // the run opened a session and took readings
  "attempts": 1,                         // >1 only ever means the scan counter had not started yet
  "examined": { "registersReadWhole": 576, "pages": 5, "boundaryProbes": 5,
                "boundaryFrom": 573, "boundaryTo": 577 },
  "probes":  [ { "register": 575, "declared": true,  "outcome": "Ok",              "exceptionCode": null },
               { "register": 576, "declared": false, "outcome": "RefusedByServer", "exceptionCode": 2 } ],
  "control": { "first":  { "ok": true, "buildStamp": "16#95D8731D", "scanCounter": 41221 },
               "second": { "ok": true, "buildStamp": "16#95D8731D", "scanCounter": 41341 },
               "measuredIntervalMs": 3012, "scanAdvance": 120 },
  "findings": [],
  "notSeen":  [ "REACHABILITY, NOT CORRECTNESS: …", "NOT WHETHER THE CLAIM IS THE RIGHT CLAIM: …", … ]
}
```

**Three properties of the shape, each there for one reason.**

- 🔴 **A run that measured nothing still writes a report**, with `"measured": false`, `"widthVerdict":
  "NotEstablished"` and **zeroes in `examined`**. A consumer that found no file could not tell a
  refusal from a run nobody started, and those are already the two states hardest to tell apart. The
  only exception is a refusal raised *before* there is a target to describe (no `--address`), which
  says so on stderr.
- **`examined` is present on every run, including the zero case** — the same rule as `drift-check`'s
  `COMPARED:` line. Every other number in the document is a conclusion; this is the one that says how
  much was looked at.
- **`notSeen` travels with the verdict.** A caveat kept in this README is a caveat the artifact does
  not have.

`--json` puts the document on **stdout** and the prose transcript on **stderr** — neither is
discarded, because the transcript is what a person reads when a verdict is surprising.

## The one retry, and the one code it covers

A download **stops the CPU**, so a run placed straight after one meets a scan counter that has not
started again yet: **exit 8**, which is a liveness fact and not a width verdict, and which clears by
itself. `--scan-retry` takes the whole measurement again, up to *n* extra times, and **reports the
attempt count** — the same shape as the wave's inert-retry, which replaced a blind 15 s wait that was
measured to be too short. One transient, one policy.

🔴 **Nothing else is retried.** A narrower or wider area is a **conclusion**, and re-rolling a
conclusion until it changes turns a check into a random number generator. Default `0`: a tool run by
hand should report a stall on its first reading, not after a minute of quietly asking again.

## Exit codes

| code | meaning |
|---|---|
| 0 | the area is **exactly** the declared width, measured from both sides |
| 1 | the device fence refused the target — **no socket opened** |
| 2 | usage: the arguments do not describe a measurement — nothing contacted |
| 3 | connect failed |
| 4 | **not established** — a probe that had to answer did not. The run measured nothing and says so |
| 5 | fence fault — a fault in the fence, not a verdict about the device |
| 6 | **NARROWER than declared** — the widening did not reach the wire |
| 7 | wider than declared — the server exposes more than the IR says |
| 8 | the scan counter did not advance between the two control reads |
