# Spec — Modbus TCP client transport (PC SIDE ONLY)

Status: **specification, not built.** Written 2026-08-11.

**Scope: the PC half, and nothing else.** No PLC program content is specified here, no `MB_SERVER`
configuration, no register map, no mirror layout. Those are deferred to
`docs/notes/modbus-plc-side-contract.md`, which is **not to be built until the IR work for Modbus is
settled** — the PLC side will be IR-authored like everything else that runs on a controller, not
hand-built in TIA.

Everything marked MEASURED was measured on this project. Everything else is a decision this spec
makes, and each one is reversible without touching the PLC.

## 1. The consequence of PC-side-first, which shapes the whole design

**The PLC-side contract does not exist yet.** There is no `MB_SERVER`, no mirror region, no agreed
register map. So the client cannot be written against one.

That is a constraint, and it is also a gift: it forces the client to be **entirely data-driven**. The
register map, the type conventions and the addressing base are all *inputs*, not assumptions baked
into code. A client built that way works against whatever the PLC side eventually declares — and
against a third party's device, and against a simulator — with a config change rather than a rebuild.

**So: nothing in this client may hardcode a register number, a word order, or a type layout.**

**Everything here is buildable and testable with no PLC in existence.** That is the point of doing
this half first.

## 2. Why Modbus at all — the measured basis

MEASURED, 2026-08-11: this CPU **refuses classic S7comm variable access CPU-wide** while permitting
connect, PDU negotiation and SZL diagnostics on the same session. A `DBRead`, a 1-byte `DBRead` and an
`MBRead` all failed identically while `GetOrderCode` succeeded. Consistent with "Permit access with
PUT/GET from remote partner" being disabled — the S7-1200 default, and a setting the *device owner*
controls, not us.

`MB_SERVER` is a connection the user program creates, so it is not gated by that setting. Modbus TCP
is also plain TCP/502: routable over the existing VPN, no licence, no driver, no admin rights.

## 3. Where it sits

**A second `ITransport`. Not a second `IS7Client`.**

`IS7Client` models an S7 session — rack/slot, PDU negotiation, SZL records, area reads by S7 area
code. Most of that has no Modbus meaning, and stubbing those members would be lying about capability.
`ITransport` is already the harness's only view of a device, expressed in terms Modbus can honour:
symbolic read, symbolic write, scan counter, declared capabilities.

```
Harness            ITransport, VectorRunner, TestVector      (no device knowledge)
Harness.S7         S7Transport     : ITransport              (Sharp7)
Harness.Modbus     ModbusTransport : ITransport   <-- NEW    (NModbus)
DeviceGuard        the fence — consulted by the transport, not by the runner
```

`ITransport` already states the rule: *"Implementations own the fence. A transport that writes to a
real device must consult the write guard before every write; the runner does not do that for it."*
That obligation carries over unchanged.

## 4. Library

**NModbus** — MIT, actively maintained, zero dependencies on net8.0. Preferred over FluentModbus on
licence and maintenance. It is a package reference: **nothing is installed on Windows** — no driver,
no NDIS filter, no admin. That matters concretely here, because a PLCSIM filter driver recently
blackholed this machine's VPN. Modbus adds no network-stack surface at all.

If NuGet restore is unavailable offline, report it rather than vendoring a binary — that is a
decision for a person.

## 5. The codec layer — pure arithmetic, no socket

Modbus holding registers are **16-bit**. Everything above that is convention, and convention is where
integrations break silently.

Required conversions, each round-trippable and unit-tested with no device:

| Type | Registers | Convention that must be configurable |
|---|---|---|
| `Int` / `Word` | 1 | byte order within the register |
| `DInt` / `DWord` | 2 | **word order** — high word first or last |
| `Real` | 2 | word order, IEEE-754 layout |
| `Bool` | bit in a register | bit position, and packing vs one-flag-per-register |
| `String` | 2 chars/register | S7 `String[n]` header: byte 0 declared max, byte 1 current length |

Two rules:

- **Reuse `S7StringCodec`** for the string header semantics. It already exists, it is already tested,
  and a second statement of the same format is a second thing to get wrong.
- **No silent defaults.** A wrong word order **still decodes** — it does not throw, it produces
  plausible nonsense shaped like data, which the harness would then assert against. An unset
  convention is a configuration error, not an assumption. This is the single most likely way for this
  client to be quietly wrong.

## 6. The register map is a file, not code

Because the PLC contract does not exist yet, the mapping from symbolic tag to register **must be
external data**:

- a map file binding tag name → register address, type, and (for Bools) bit position;
- the addressing base declared explicitly — on the wire holding registers are 0-based, while the
  `4xxxx` form is a 1-based display convention, and NModbus takes a 0-based `ushort`;
- validation at load: overlapping ranges, unknown types, a Bool without a bit position, and a tag
  addressed outside any declared block are all **load-time errors**, not runtime surprises.

`S7TagMap` is the precedent to follow in shape.

## 7. Fence integration

Reads are authorised by `DeviceAccessGuard`, writes by `DeviceWriteGuard`, exactly as the S7
transport does it. The transport consults the guard; the runner does not know the guard exists.

**One thing must not be carried across silently.** `IdentitySourcePlan` currently *always* includes
`OrderCodeIdentitySource`, which reads an SZL record — firmware-provided, and it worked today even on
a CPU refusing all variable access. **Modbus has no equivalent.** There is no SZL and no device
identity service the user program does not implement.

So the PC side must model identity-over-Modbus as **whatever the program publishes** — no more. The
plan builder must not assume an order-code source exists on a Modbus transport, and where identity is
documented it should say plainly that a Modbus-only check verifies *what the program claims*, not
what the silicon reports. What those published fields are is a PLC-side question and is deferred.

## 8. Error handling — apply what today taught

Today's failed read was valuable **only because the error was diagnosed rather than reported**. Four
causes produced an identical symptom; the discriminator was the exact error code plus a second probe
against a different area.

Requirements:

- surface the **exact** Modbus exception code and function code, never a flattened "read failed";
- distinguish, and say which: **TCP-level failure** (no route, refused, timeout — tunnel or
  firewall), **a Modbus exception response** (the server answered and objected — e.g. `0x02` illegal
  data address means the register is outside whatever the server exposes), and **a malformed or short
  reply**;
- report the request that produced the failure — function, register, count — so it is reproducible
  with a third-party client;
- a timeout states what it was waiting for, and for how long.

## 9. No coil writes

The client will not implement coil writes — `WriteSingleCoil` / `WriteMultipleCoils` are simply never
called, asserted by test. It costs nothing (the harness has no use for them) and it keeps a whole
function-code class out of the binary. Why that class is worth excluding is a PLC-side address-model
question and is covered in the other document.

## 10. Testing, all of it deviceless

1. **Codec tests** — pure arithmetic. Round-trip every type; pin word order explicitly in both
   directions so a flip is a test failure rather than a field surprise.
2. **Map-loading tests** — every validation in §6 fails at load with a message naming the offending
   tag.
3. **Transport tests against an in-process fake Modbus server** — read, write, exception responses,
   timeout, reconnect. NModbus can host one; if that proves awkward, a hand-rolled fake is fine.
4. **Fence tests** — a write is refused when the guard refuses, and the refusal names the failing
   gate. A read-only configuration refuses writes by capability, not by check.
5. **No-coil test** — assert the coil write functions are never called.
6. **Third-party cross-check, when a server exists** — confirm the same registers with
   `modpoll`/`mbpoll` before trusting our numbers. Today produced two cases where our tooling and a
   device disagreed; an independent dumb client is the cheapest way to find out which is wrong.

## 11. Build order

1. Codecs — no I/O.
2. Register map model + loader + validation.
3. `ModbusTransport : ITransport` against a fake server.
4. Fence integration.
5. A read-only CLI mirroring `rig-read`, pointed at a fake or third-party server.
6. Writes — last, fenced, off by default.

**Steps 1–5 need no PLC, no `MB_SERVER`, and no decisions from anyone else.** Start at 1 and do not
wait on the PLC side.

## 12. Reference implementations

The owner is sourcing worked Modbus TCP projects. When they arrive they inform §5 and §6 — the
conventions a real integration actually uses. Until then every such convention stays configurable
with no default, which is the correct posture regardless.

## 13. Deferred — see the PLC-side document

`MB_SERVER`, the mirror region and whether it lives in `%MW` or a standard-access DB, what the mirror
publishes, the scan counter, the identity fields, the register base the server exposes, and the
process-image address model. All of it is in `docs/notes/modbus-plc-side-contract.md` and none of it
blocks the work above.
