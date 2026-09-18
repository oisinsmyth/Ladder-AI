# `rig-control --run` — the RUN transition, fenced

A **new class of write to a physical controller**: asking an allowlisted bench rig to go to RUN, and
then **proving it did**. Built 2026-08-14 under the owner's explicit approval, for one purpose.

**It refuses today, and that is the correct behaviour.** The live allowlist entry reads
`"writeEligible": false`. See *Live evidence* below.

---

## THE EXACT COMMAND A PERSON WOULD RUN

🔴 **PowerShell — this is the form that was RUN, 2026-08-14, and the one to use.** It reached the
`NotWriteEligible` gate, which is the correct refusal today.

```powershell
& "src\harness\Harness.RigControl\bin\Release\net8.0\rig-control.exe" `
    --run --target 10.10.10.10 `
    --allowlist "$env:USERPROFILE\.ladder\device-allowlist.json" `
    --yes
```

⚠️ **This block previously showed the `cmd` form (`%USERPROFILE%`, `^` continuations) and the owner
pasted it into PowerShell.** `%VAR%` is `cmd`/batch syntax; **PowerShell does not expand it**, so the
tool was handed the literal string, refused at `AllowlistUnusable`, and **never reached the gate we
actually wanted to see.** The fence behaved perfectly and echoing the literal back is what made it
diagnosable in seconds — but a documented command that only works in one shell is a defect in the
document, so the shell is now named and the variable is quoted. The `cmd` form, if you want it:

```bat
rig-control.exe --run --target 10.10.10.10 ^
    --allowlist "%USERPROFILE%\.ladder\device-allowlist.json" --yes
```

**And the tool now says this itself.** A path that contains `%…%` **and does not resolve** gets one
extra line — it still refuses identically, and it **does not expand anything for you**:

```
  gate    : AllowlistUnusable
  verdict : Allowlist file not found: %USERPROFILE%\.ladder\device-allowlist.json
  hint    : that path contains %USERPROFILE%, which is cmd/batch variable syntax. PowerShell does
            NOT expand it, so the literal string above is what this tool was handed — it was not the
            file that was missing, it was the substitution.
            In PowerShell, quote it: "$env:USERPROFILE\.ladder\device-allowlist.json"
            Nothing here expanded it for you, on purpose: a fence that repairs its own input is one
            you cannot tell what it actually read.
```

***THE PRINTED FORM WAS COPIED OUT OF THAT OUTPUT AND RUN*** — because *an error message's advice is
a claim*, and an untested instruction is a guess offered to somebody already in trouble. It expanded
to `C:\Users\<user>\.ladder\device-allowlist.json` and the fence advanced to `NotWriteEligible`.
It fires **only** when the path does not resolve: `%` is legal in a Windows filename, and a directory
genuinely called `%TEMP%-not-a-variable` is left alone (there is a test that builds one on the real
filesystem).

Without `--yes` the plan prints, **no transport is constructed**, and it exits `10`. Run it that way
first; that is what the flag is for.

**As of 2026-08-14 this command exits `1` at the `NotWriteEligible` gate.** To change that, a
*person* edits `writeEligible` in the allowlist. Nothing in this binary does, and no flag stands in
for it.

---

## 🔴 IS A RUN TRANSITION EVEN REACHABLE ON THIS CPU? THREE ANSWERS, AND ONLY ONE IS MEASURED

| route | verdict | basis |
|---|---|---|
| **Openness, inside a download** (`download-probe --disruptive`) | ✅ **REACHABLE — MEASURED** | `StopModules+StartModules answered, exit 0` (2026-08-14); `rig-read` then read `Running (8)` |
| **Classic S7comm PI service** (`PlcHotStart`), i.e. *this tool* | ⚠️ **NOT ESTABLISHED, IN EITHER DIRECTION — NEVER SENT** | see below |
| **Modbus TCP** (the data path) | 🔴 **STRUCTURALLY IMPOSSIBLE** | `MB_SERVER` is a *program block*; in STOP the program does not execute, so nothing answers on `:503`. The transport that would report the problem is the one the problem switches off |

### 🔴 RETRACTION, 2026-08-14 — this section used to say "predicted refused". It was wrong.

**What it argued:** `PlcHotStart` is job-class; job-class variable access is refused CPU-wide on this
rig with `0x00040000`; therefore the run request sits on the refused side of that split.

**Why it was wrong, and it is the more instructive half:** the second premise was a fact I **read from
a 2026-08-12 record and never re-took**. ***PUT/GET WAS ENABLED ON THIS CPU IN THE MEANTIME.***
Re-measured read-only on 2026-08-14, independently by two parties with `rig-read`:

```
DBRead(db=38, start=0, size=1) -> rc=12582912 (0x00C00000): CPU: Item not available  [93 ms]
MBRead(0, 1)                   -> ok  [79 ms]      <- job-class variable access, ANSWERED
GetOrderCode (SZL)             -> ok  [108 ms]
CPU run state                  -> Running (8)
```

`MBRead` **is** job-class variable access and it succeeds, at a full round trip. `DBRead(38)` fails
with `0x00C00000` — *item* not available, a **block-specific** code meaning DB38 is optimized or
absent — **not** the CPU-wide negative acknowledgement `0x00040000`. So the split the prediction rested
on no longer exists.

> ***A RECORDED FACT ABOUT A DEVICE HAS A DATE, AND A CONFIGURATION CHANGE DOES NOT ANNOUNCE ITSELF TO
> THE DOCUMENT THAT RECORDED IT.*** The failure was not the inference — the inference was sound on its
> premises. It was **citing a measurement instead of re-taking one**, on a claim that was load-bearing
> for the whole ruling, when re-taking it cost one read-only command.

**This does NOT make the route "expected to work."** It moves it to a *different kind of unknown*.
Three things stand between "PUT/GET is on" and "this telegram is answered", and none has been tested:

1. PUT/GET governs the **variable** services (Read/Write Var, `0x04`/`0x05`); `0x28` is a **different
   function** that merely shares the job class.
2. Whether an S7-1200 implements the classic `P_PROGRAM` PI service **at all** is unknown here — TIA's
   own mode control for a 1200 does not go over classic S7comm.
3. A CPU may gate mode control on its **protection level**, independently of PUT/GET.

***AN UNTESTED TELEGRAM IS UNTESTED.*** The only way to settle it is to send it, which requires
`writeEligible: true`. When that happens, the elapsed-time discriminator below still applies.

### What the telegram is

Read out of Sharp7 1.1.82's own `S7_HOT_START` telegram on 2026-08-14:

```
S7_HOT_START = 03 00 00 25 02 F0 80 32 01 00 00 0C 00 00 14 00 00 28 ... 50 5F 50 52 4F 47 52 41 4D
                                          ^^^^^                   ^^          "P_PROGRAM"
                                          32 01 = S7comm JOB      0x28 = PI service
```

So `PlcHotStart` is a **job-class** request (`32 01`) — the same PDU class as Read Var / Write Var,
and a *different* class from the userdata/SZL requests `GetOrderCode` and `PlcGetStatus` ride on. That
is a fact about the **telegram**, and it is the only settled fact here. It was the load-bearing half of
the retracted prediction and it survives the retraction intact; what did not survive was the claim
about how this CPU treats that class.

**If it is ever sent and comes back refused, that is a RESULT, not a bug.** Compare its elapsed time
against the order-code round trip (79–108 ms measured 2026-08-14): a failure costing a full round trip
was **answered** by the CPU; one returning immediately was rejected locally.

### What this tool is actually for — the premise, corrected

The brief said *"`download-probe` requires `--disruptive` and therefore stops the CPU"*. Half true.
`--disruptive` answers **both** `StopModules/StopAll` and `StartModules/StartModule`, and
`NoActionFirstPolicy.DisruptiveAllowances` already calls the latter *"the only route to RUN this tool
has"*. **An ordinary download does not leave the CPU stopped**, measured.

The real gap is named in the download probe's own source: `StartModules` is raised in the POST
delegate, *after* the download has already stopped the modules, so an abort there leaves the CPU
stopped **"with no route to start it from inside that download"**. This is that route, from outside
— plus the plainer case of a CPU a person stopped.

---

## THE FENCE

Every gate fails closed, and the whole chain sits **above** any call to the transport factory, so
*"no device was contacted"* holds **by construction** rather than by inspection.

| # | gate | refusal |
|---|---|---|
| 0 | a target was named | `NoTarget` |
| 1 | an allowlist was **located** — `--allowlist` or `LADDER_DEVICE_ALLOWLIST`, **no default path** | `NoAllowlistConfigured` |
| 2 | it loaded: present, readable, parseable, no null entries | `AllowlistUnusable` |
| 3 | `DeviceAccessGuard`: exact address match, `kind` **exactly** `test-rig` | `NotAnApprovedTestRig` |
| 4 | **`writeEligible`** — a *human authorisation*, not a config value | `NotWriteEligible` |
| 5 | `outputsIsolated` — RUN is what makes outputs live | `OutputsNotIsolated` |
| 6 | `isolationAssertedBy` — an unattributed assertion is not one | `IsolationUnattributed` |
| 7 | an `orderNumber` is declared, so the answering CPU can be checked | `NoDeclaredIdentity` |
| 8 | `--yes` | exit `10`, **nothing constructed** |
| — | *then, and only then, a socket* | |
| 9 | the CPU's order code **matches** what the entry declares | exit `1` |
| 10 | the CPU is not already running (if it is, nothing is requested) | exit `0` |
| 11 | **the state is polled back and verified** | exit `15` |

**There is no override.** No flag, no environment variable and no file supplies a gate's answer or
skips one. `--force`, `--override`, `--write-eligible`, `--skip-readback`, `--no-verify`,
`--assume-run`, `--stop`, `--halt` and `--cold-start` are each refused **by name**, because *"unknown
option"* reads as a typo and sends somebody looking for the right spelling.

**`DeviceWriteGuard` is deliberately not used.** It is shaped around writing an *area* — it demands a
declared `WriteScope`, an area inside it, and a verified restore point — and a mode change writes no
area, so those gates have no operands here; its identity gate also takes an identity that has already
been read, which would put a socket ahead of the fence's verdict. The gates that *do* apply are
restated in `RunTransitionFence` in the same order and with the same fail-closed shape.

### What the fence does NOT cover, printed on every run

The **serial number** is the stronger identifier and this transport cannot read one. Two routes, two
different reasons, and *the second reason was also stated wrongly here until 2026-08-14*:

- **SZL `0x001C`** — recorded as refused at every index on this CPU (2026-08-11). Not re-taken; the
  entry's `useCpuInfoSerial` is `false`, so today's `rig-read` never tried it. **A recorded fact,
  labelled as one.**
- **The marker DB** — `DB38` reads back `0x00C00000`, *item not available*: the block is **optimized
  (invisible to classic S7comm) or absent**. This README previously blamed CPU-wide refusal of S7
  variable access, which is **not the cause and is no longer even true** — `MBRead` succeeds. The
  conclusion survives; ***the mechanism I gave for it did not, and a plausible mechanism attached to a
  correct conclusion is how a wrong reading of a device stays unexamined.***

Refusing on the serial would make the gate **permanently unsatisfiable**, and a gate that
refuses every legitimate run is removed within a week by somebody right to remove it. So it is
excluded **by name, with a count, on every run — including the runs that exclude nothing**:

```
narrowing     : 1 declared identifier(s) EXCLUDED and NOT CHECKED - serialNumber ('...').
narrowing     : 0 declared identifier(s) EXCLUDED - the order code is the whole check.
```

A checker that only speaks when it exempts something cannot be told from one that has stopped
working.

---

## TWO THINGS THIS BINARY WILL NOT DO

**1. It will not stop a CPU.** Nothing asked for a stop, and *a capability built because it is
symmetrical is a capability nobody weighed.* The consequence is real and is printed on every run:

> *** THIS BINARY CAN START THIS CPU AND CANNOT STOP IT. *** There is no `--stop` and no restore
> point for a mode change. If RUN turns out to be the wrong state, a person reverses it in TIA Portal.

That is why there is no restore-point gate: there is nothing to restore, and the honest statement of
the gap belongs where a reader of *results* meets it, not only in a design note.

**2. It will not skip the read-back.** A PI-service request can be acknowledged by a CPU that then
does nothing — *a silent no-op is the whole failure mode* — so an exit `0` on the strength of the
acknowledgement would be a confident false green about the one fact the caller needs. The state is
polled back (a CPU reaching RUN passes through a non-running startup state, so a single immediate
read would fail a transition that was going to succeed), and:

- a read-back that **disagrees** → exit `15`;
- a read-back that **could not be performed** → exit `15` as well. *Empty is not clean:* nothing was
  established about the mode, and "no error seen" is not "running".

There is deliberately no flag that skips it, and asking for one is a named refusal.

`IS7Client` is left **exactly as it was**. Its reduction property — no `PlcStop`, no `PlcHotStart`,
no `Download`, so the conformance harness cannot change a mode however a caller behaves — is
load-bearing and should not be spent to add one verb. The transition lives on its own interface
(`IRunTransitionTransport`), in its own assembly, behind its own fence. It does **not** extend
`IS7Client` either: that would have handed this binary `WriteDataBlock` and `WriteBit` as a side
effect, and *a tool that may start a CPU should not also be able to write its memory.*

---

## EXIT CODES

| code | meaning |
|---|---|
| `0` | the CPU answered RUN on a read **after** the request — or was already running and was left alone |
| `1` | a gate refused. **No mode change attempted** |
| `2` | usage, including every refused-by-name flag |
| `3` | the fence allowed and the session could not be established |
| `4` | the request itself failed, or the device phase threw (named, never a bare crash) |
| `5` | **the fence itself threw** — a decision was not reached. Not a verdict about the device |
| `10` | `--yes` absent. The plan printed and **nothing was contacted** |
| `15` | 🔴 **the state read back does not match the state requested, or could not be read at all** |

---

## HOW IT IS TESTED — AND WHAT THAT IS WORTH

**55 tests, none of which certifies anything with a `const bool`.** That defect was found in this
repository on 2026-08-14: `Assert.False(Arming.CompiledIn)` on a constant, with a live socket client
planted in the assembly and all 202 tests still green.

- **Every test runs through `RigControlCli.Run`**, the entry point a caller actually uses — never
  against a refusal helper. A `--yes` gate elsewhere in this repo can be disconnected by a one-token
  mutation with 712 of 712 tests green, because its test pins the *message* and not the *routing*.
- **Every refusal asserts the observable consequence** — the transport factory was **never called** —
  alongside the exit code, which a disconnected gate can produce by accident. A `NeverTouchedTransport`
  whose every member throws catches the weaker case where a transport is built and then not used.
- **The structural claim is an IL walk** over the shipped assembly, asserting that no method anywhere
  can reach `PlcStop`, `PlcColdStart`, `DBWrite`, `MBWrite`, `WriteArea`, `Delete`, `Download`,
  `PlcCopyRamToRom`, `SetPlcDateTime`, `SetPlcSystemDateTime`, `Sharp7Client.WriteDataBlock`,
  `Sharp7Client.WriteBit` or `System.Net.Sockets` — with a **denominator** (`BodiesExamined > 0`, so
  *found nothing* and *looked at nothing* are different results) and **live positive controls**,
  including one that requires the walk to find `Sharp7.S7Client.PlcHotStart` — without which every
  forbidden-member assertion would be vacuous. The control target is a **parameter**, not a literal,
  because *a check whose exercise requires editing the check will not be exercised.*

### Mutation results — the fence broken in both directions

*A fence that refuses everything passes every test that only checks refusals*, so both directions were
run. Baseline 55/55 green; each mutation applied to the committed tree and reverted.

| # | mutation | result |
|---|---|---|
| M1 | CLI computes the fence verdict and **ignores** it | 🔴 **10 failed** |
| M2 | fence **refuses everything** (`Allow` → `Refuse`) | 🔴 **12 failed** |
| M3 | `--yes` gate disconnected (`confirmed` always true) | 🔴 **1 failed** |
| M4 | read-back verification disconnected (`!after.Running` → never) | 🔴 **1 failed** |
| M5 | `writeEligible` gate removed | 🔴 **2 failed** |
| M6 | a forbidden capability added — `PlcColdStart` + `DBWrite`, in a method **nothing calls** | 🔴 **2 failed**, naming both members |
| M7 | the IL walk's body reader forced to `null` (walk examines **zero** bodies) | 🔴 **17 failed** |

M6 is the mutation that slipped past a whole suite before. M7 is the *control that is not executed*
defect: without the denominator, forcing the walk to examine nothing would have left every structural
assertion green.

### Live evidence, 2026-08-14 — a refusal that provably opened no socket

Same Release binary, same session, three runs. **The rig was not tested against.**

| run | allowlist | target | exit | elapsed | what it printed |
|---|---|---|---|---|---|
| **A** | live | `10.10.10.10` | **1** | 751 ms | `gate: NotWriteEligible` … `REFUSED - no connection attempted`. No `== plan ==`, no `== connect ==` |
| **B** | live, **`--yes`** | `10.10.10.10` | **1** | 214 ms | identical refusal |
| **C** | temp, fully eligible | `192.0.2.1` (RFC 5737 TEST-NET-1) | **3** | 2576 ms | `gate: Allowed` … `Connect(192.0.2.1,0,1) -> rc=3: TCP: Connection Error` |

**C is the positive control and it is what makes A and B mean something.** The same binary, given an
entry that satisfies every gate, *does* build a transport and *does* spend the full 2000 ms connect
timeout in a socket. A and B produce neither the plan section nor a connect line and return in the
time it takes .NET to start — so the refusal is the fence deciding, not an inert binary. `192.0.2.1`
is reserved and unroutable; no real device was contacted by any of the three.

*(A loopback control was rejected: something on this PC is listening on `0.0.0.0:102`, so a connect
to `127.0.0.1` would have reached a real S7 server.)*
