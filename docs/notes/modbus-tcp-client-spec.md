# Spec — Modbus TCP client transport for the harness

Status: **specification, not built.** Written 2026-08-11. Everything marked MEASURED was measured on
this project; everything marked MUST BE MEASURED is a known gotcha that has to be settled against a
real device before the client is trusted.

## 1. What this is, and what it is not

A **Modbus TCP client (master)** inside the PC-side test harness, talking to an S7-1200 running
`MB_SERVER`. It gives the harness read and write access to PLC data over a routable, licence-free
protocol.

**In scope:** connect, read and write holding registers, decode/encode S7 data types, map symbolic
tag names onto register addresses, and honour the existing device fence.

**Out of scope, explicitly:**

- **It is not a Modbus server.** The PC is the client; the PLC is the server.
- **It does not do project operations.** No download, compile, import, export — those stay with
  Openness/`openness-cli`. This solves runtime data access, not the toolchain.
- **It does not create the PLC-side interface.** `MB_SERVER`, its connection configuration and the
  mirror region are PLC program content. The harness *consumes* a contract it does not author.
- **It writes no coils.** See §7.

## 2. Why Modbus, in one paragraph

MEASURED, 2026-08-11: this CPU **refuses classic S7comm variable access CPU-wide** while permitting
connect, PDU negotiation and SZL diagnostics on the same session — a `DBRead`, a 1-byte `DBRead` and
an `MBRead` all fail identically while `GetOrderCode` succeeds. Consistent with "Permit access with
PUT/GET from remote partner" being disabled, which is the S7-1200 default and a setting the device
owner controls. `MB_SERVER` is a connection the **user program** creates, so it is not gated by that
setting — and on a site's production PLC, being independent of someone else's security posture is
worth more than any per-protocol convenience. Modbus TCP is also plain TCP/502: routable over the
existing VPN, no driver, no licence, no admin rights.

## 3. Where it sits

**A second `ITransport`, NOT a second `IS7Client`.**

`IS7Client` models an S7 session — rack/slot, PDU negotiation, SZL records, area reads by S7 area
code. Most of that has no Modbus meaning, and an implementation that stubbed those members would be
lying about what it can do. `ITransport` is already the harness's only view of a device and is
expressed in exactly the terms Modbus can honour: symbolic read, symbolic write, scan counter,
declared capabilities.

```
Harness            ITransport, VectorRunner, TestVector      (no device knowledge)
Harness.S7         S7Transport  : ITransport                 (Sharp7)
Harness.Modbus     ModbusTransport : ITransport   <-- NEW    (NModbus)
DeviceGuard        the fence — consulted by the transport, not by the runner
```

`ITransport` already states the rule: *"Implementations own the fence. A transport that writes to a
real device must consult the write guard before every write; the runner does not do that for it."*
The Modbus transport inherits that obligation unchanged.

## 4. The PLC-side contract this assumes

The harness requires the program to expose a **mirror region** reachable through `MB_HOLD_REG`.

**Prefer `%MW` over a DB.** Siemens permits `MB_HOLD_REG` to reference *"a standard DB area or a
memory area"*. A DB must be **standard (non-optimized)** access — and MEASURED today: a block's
memory layout **silently reverts to Optimized on every re-import**, with `converter drift-check`
structurally blind to it in both directions. **M memory has no "optimized" concept at all**, so a
`%MW` mirror removes that entire failure mode. The 1214C has 8 KB of `%M`.

The mirror should carry, at minimum:

| Purpose | Why the harness needs it |
|---|---|
| Format / layout version | Read first; refuse the whole mirror on an unexpected value, because every other offset is justified only by that number |
| Rig identity marker | The unit-level identifier the write fence verifies (see §6) |
| Order number | Cross-check: catches "right marker, wrong model of box" |
| Free-running scan counter | Required for `TransportCapabilities.ScanCounter`; without it "wait N scans" is a wall-clock guess, not a measurement |
| Command / status words | Driving and observing the program |

Two of those are not optional if the harness is to do its job honestly: **the scan counter** (the
runner refuses vectors whose observability the transport cannot support) and **the identity marker**
(the fence has no other unit-level identifier under Modbus — see §6).

## 5. Register map and type codecs — the part that goes wrong

Modbus holding registers are **16-bit**. Everything else is a layering convention, and conventions
are where integrations break. All of the following **MUST BE MEASURED** against the real device
before the client is trusted; none may be assumed from documentation alone.

- **Register base.** On the wire, holding registers are 0-based; the `4xxxx` convention is a 1-based
  display form. NModbus takes a 0-based `ushort`. Where `MB_SERVER` maps the start of `MB_HOLD_REG`
  must be established, not assumed.
- **Mirror offset mapping.** Which `%MW` word corresponds to holding register *N*.
- **32-bit word order.** `DInt` and `Real` span two registers. Whether the high word comes first is
  the single most common source of silently-wrong values, and a wrong choice still decodes — it just
  yields nonsense that looks like data.
- **Byte order within a register.** Modbus is big-endian per register; confirm S7 agrees.
- **Bools.** Bit position within a register, and whether the program packs them or uses one register
  per flag. **Do not reach for coils to solve this** (§7).
- **Strings.** Two characters per register, plus the S7 `String[n]` header semantics already
  implemented in `S7StringCodec` — byte 0 declared max, byte 1 current length. Reuse that codec
  rather than writing a second statement of the format.

**Design rule:** the codec layer is pure byte/word arithmetic over a buffer and must be unit-tested
with **no device and no socket**, exactly as `S7StringCodec` is. Word-order and base choices are
configuration with **no silent default** — an unset value is an error, not an assumption.

## 6. Identity and the fence — a real weakening to handle honestly

Reads are authorised by `DeviceAccessGuard`; writes by `DeviceWriteGuard`'s gates. The Modbus
transport consults them exactly as the S7 transport does.

**But identity is weaker under Modbus, and the design must not paper over it.** Under S7,
`OrderCodeIdentitySource` reads the order code from an SZL record — firmware-provided, and it works
even on this CPU where variable access is refused. **Modbus has no equivalent.** There is no SZL, no
device-identity service the program does not implement. So under Modbus:

- every identifier is **published by the user program**, therefore only as trustworthy as the program
  and the download that placed it;
- `IdentitySourcePlan` currently *always* includes the order-code source. That assumption is
  S7-specific and must not be silently carried across;
- the mirror should publish the **order number as well as the marker**, so the two can disagree —
  that is what catches a program copied onto a different box.

State this plainly wherever the fence is documented: a Modbus-only identity check verifies **what the
program claims**, not what the silicon reports.

## 7. Safety property: holding registers only

Siemens' `MB_SERVER` address model routes function codes **03/06/16** to `MB_HOLD_REG` and
**01/02/04/05/15** to the process image — so FC05/FC15 can write `%Q` directly.

In practice this is a weak hazard: a PLC scan overwrites any output the program actually drives, so
an external coil write is a one-scan transient rather than control, and the usual holding-register
pattern never issues those codes at all.

**Nonetheless: this client will not implement coil writes.** Not as a policy to be observed, but as
absent capability — `WriteSingleCoil` and `WriteMultipleCoils` are simply not called anywhere in the
implementation, and a test asserts that. It costs nothing (the harness has no use for them) and it
makes "our tooling cannot write `%Q`" a property of the binary rather than a promise.

## 8. Error handling — apply what today taught

Today's failed read was valuable **only because the error was diagnosed rather than reported**. Four
different causes produced an identical symptom, and the discriminator was the exact error code plus a
second probe against a different area.

Requirements:

- Surface the **exact** Modbus exception code and function code, never a flattened "read failed".
- Distinguish, and say which: **TCP-level failure** (no route, refused, timeout — the tunnel or
  firewall), **Modbus exception response** (the server answered and objected — e.g. `0x02` illegal
  data address means the register is outside `MB_HOLD_REG`), and **a malformed/short reply**.
- On failure, report the request that produced it — register, count, function — so the failure is
  reproducible with a third-party client.
- A timeout must state what it was waiting for and for how long.

## 9. Library

**NModbus** — MIT, actively maintained, zero dependencies on net8.0. Preferred over FluentModbus on
licence and maintenance. It is a package reference: **nothing is installed on Windows**, no driver, no
NDIS filter, no admin. That matters here specifically — a PLCSIM filter driver recently blackholed
this machine's VPN, and Modbus adds no network-stack surface at all.

## 10. Testing

**The client must be fully buildable and testable with no PLC and no `MB_SERVER` in existence.**

1. **Codec tests** — pure arithmetic, no socket. Round-trip every type; pin word order explicitly.
2. **Transport tests against a fake Modbus server** (in-process, or NModbus' own server) — covering
   read, write, exception responses, timeout, and reconnect.
3. **Fence tests** — a write is refused when the guard refuses, and the refusal names the failing
   gate. A read-only configuration refuses writes by capability.
4. **No-coil test** — assert the implementation never calls the coil write functions.
5. **Cross-check with a third-party client.** Before trusting our numbers, confirm the same registers
   with `modpoll`/`mbpoll`. Today produced two cases where our tooling and the device disagreed; an
   independent dumb client is the cheapest way to tell which is wrong.

## 11. Build order

1. Codecs + tag map + register-address model — no I/O, fully unit-tested.
2. `ModbusTransport : ITransport` against a fake server.
3. Fence integration (read guard, write guard, identity from published registers).
4. A `rig-modbus` read-only CLI mirroring `rig-read`, so the first live contact is a read.
5. Writes — last, fenced, and off by default.

Steps 1–3 need no device, no `MB_SERVER`, and no decisions from anyone else. Start there.

## 12. Open questions, to settle against a worked example

The owner has offered worked examples from a previous job. These are what they should settle, and
until then the client must treat each as configuration with no default:

- Where `MB_SERVER` maps the base of `MB_HOLD_REG`, and the register ↔ `%MW` offset relation.
- 32-bit word order for `DInt` and `Real`.
- How Bools are packed.
- Whether `MB_HOLD_REG` against `%MW` behaves identically to a standard DB.
- How many `MB_SERVER` instances are needed for N concurrent clients, and whether the harness should
  hold one long-lived connection or reconnect per operation.
