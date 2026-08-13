# Spec — Modbus TCP client transport (PC SIDE ONLY)

Status: **specification, not built.** Written 2026-08-11, revised the same day after review.

**Scope: the PC half.** No PLC program content is specified here. That is deferred to
`docs/notes/modbus-plc-side-contract.md`, which waits on the IR work — the PLC side will be
IR-authored, not hand-built in TIA.

Everything marked MEASURED was measured on this project.

## 0. Rulings from review (2026-08-11)

These were open questions in the first draft. They are now decided, and the reasoning is kept so a
later reader can tell a decision from a default.

1. **The register map is authored PC-SIDE and is authoritative. The PLC works off it.** Not the
   reverse, and not hand-maintained on both sides. One source of truth, on the side that can be
   changed without a download.
2. **Identity comes from the PLC.** The idea of authenticating over S7 (firmware-provided order code)
   while doing data over Modbus is dropped — that route has failed before and cannot be depended on.
   Identity is therefore program-published, with the weakening recorded rather than assumed away.
3. **Build order is not precious.** Development runs with a fast feedback loop; writes need not be
   quarantined to the very end. The write path is built against the fake server early so the fence is
   exercised throughout; only device-facing arming stays last.
4. **The transport must batch.** A per-tag round trip is not viable at the measured latency — see §5.
5. **Independent verification is an acceptance criterion, not a later nicety** — see §10.

## 1. The consequence of PC-side-first

The PLC-side contract does not exist yet, so the client cannot be written against one. That forces the
client to be **data-driven**: the register map, the type conventions and the addressing base are
inputs, never baked into code.

Combined with ruling 1, this makes the PC-side map the **contract** the PLC side must later implement,
and the thing an eventual conformance check compares the PLC's IR against.

**Nothing in this client may hardcode a register number, a word order, or a type layout.**
Every step below is buildable and testable with no PLC in existence.

## 2. Why Modbus — the measured basis

MEASURED, 2026-08-11: this CPU **refuses classic S7comm variable access CPU-wide** while permitting
connect, PDU negotiation and SZL diagnostics on the same session. `DBRead`, a 1-byte `DBRead` and
`MBRead` all failed identically while `GetOrderCode` succeeded. Consistent with "Permit access with
PUT/GET from remote partner" being disabled — the S7-1200 default, controlled by the device owner,
not by us.

`MB_SERVER` is a connection the user program creates, so it is not gated by that setting. Modbus TCP
is plain TCP/502: routable over the existing VPN, no licence, no driver, no admin rights.

## 3. Where it sits

**A second `ITransport`. Not a second `IS7Client`** — most of `IS7Client` (rack/slot, PDU
negotiation, SZL, S7 area codes) has no Modbus meaning, and stubbing those members would be lying
about capability.

```
Harness            ITransport, VectorRunner, TestVector      (no device knowledge)
Harness.S7         S7Transport     : ITransport              (Sharp7)
Harness.Modbus     ModbusTransport : ITransport   <-- NEW    (NModbus)
DeviceGuard        the fence — consulted by the transport, not by the runner
```

`ITransport` already states the rule: *"Implementations own the fence. A transport that writes to a
real device must consult the write guard before every write."* That carries over unchanged.

## 4. Library

**NModbus** — MIT, maintained, zero dependencies on net8.0. A package reference: **nothing is
installed on Windows**, no driver, no NDIS filter, no admin. That matters here — a PLCSIM filter
driver recently blackholed this machine's VPN, and Modbus adds no network-stack surface.

## 5. Batching is mandatory, not an optimisation

MEASURED ON THE RIG, 2026-08-12 — **and this supersedes the 79–108 ms figure this section carried
until 2026-08-13, which was the wrong SHAPE rather than the wrong size**: it sat between the median
and the p90 and understated the tail by ~60%.

| | measured [M] |
|---|---|
| round trip, median | **71–78 ms** |
| round trip, p90 | 89–115 ms |
| round trip, **p99** | **136–173 ms** ← every budget and timeout keys on this |
| round trip, worst in 2,000 samples | **2,216 ms** (0.05%) |
| marginal cost per register | **indistinguishable from zero** — 1, 4, 8 and 16 registers cost the same within noise |

`ITransport.Read` is per-tag, so a naive implementation costs one round trip per tag. Re-derived on
the measured figures (`PC-Client-Modbus-Spec-Draft-final.txt` §12a derivation 6):

- a 50-tag assertion sweep, one round trip each: **3.9 s** typical, **8.7 s at the p99** — plus a
  50/2000 = 2.5% chance that any given sweep eats a 2,216 ms outlier;
- the same 50 tags inside **one** FC03 (≤125 registers): **one** round trip, 78 ms typical / 173 ms
  at the p99 — a **50x** reduction;
- a scan-counter poll: one round trip, 78 ms typical / 173 ms at the p99.

**Therefore `ModbusTransport` reads in blocks and serves tags from a snapshot.** One transaction
fetches a contiguous register range; individual tag reads decode out of that buffer.

**The zero marginal register cost makes this stronger than it was, and changes how the map should be
laid out**: a wide read costs what a narrow one costs, so the map is laid out for the *widest legal
read*, not the smallest sufficient one. Under the old assumption a narrow range was a saving; it is
not, and treating it as one only buys extra round trips — the sole thing that does cost.

> ⚠️ **The zero-cost figure is measured to 16 registers and INFERRED to 125.** A 125-register FC03
> response is 259 bytes against 41 for a 16-register one — one TCP segment either way, so nothing in
> the path changes shape. Good inference, still an inference; a sweep to 125 costs one run of the
> existing `timing` client and would retire it. Do not quote it as measured beyond 16.

**Per-request timeout floor: 3,000 ms** — derived, not chosen. One round trip in 2,000 took 2,216 ms
[M]; a timeout below that converts a measured, routine tail event into a spurious transport failure
at ~1 in 2,000 requests. A generous timeout costs nothing (it only elapses on a request that has
already failed) and a tight one costs an intermittent, unattributable red. **And a retry after a
timeout must not be indistinguishable from a lost request** — the original may still land, which on
a write duplicates a vector and on the start-bool commit is a second T=0.

Two consequences that must be explicit, not incidental:

- **A snapshot has an age.** `Read` no longer means "now". The transport must expose when the current
  snapshot was taken and refresh on a defined policy, so a caller cannot silently assert on stale
  data.
- **Coincidence assertions need one snapshot.** Two values claimed to be from the same scan must come
  from the *same* transaction, or the assertion is meaningless. The snapshot boundary is what makes
  that honest.

The register map (§6) is what makes blocking possible: contiguity is a property of the map, so the
map should be laid out with block reads in mind.

## 6. The register map — PC-side, authoritative, external

Per ruling 1, the map is the contract. It is data, not code:

- tag name → register address, type, and (for Bools) bit position;
- addressing base declared explicitly — on the wire holding registers are 0-based, while `4xxxx` is a
  1-based display convention and NModbus takes a 0-based `ushort`;
- block/range declarations, so the transport knows what it may fetch in one transaction;
- **load-time validation**: overlapping ranges, unknown types, a Bool with no bit position, a tag
  outside any declared block, and a range exceeding the Modbus per-transaction limit are all errors
  at load, naming the offending tag.

`S7TagMap` is the precedent for shape.

## 7. Fence integration and identity

Reads are authorised by `DeviceAccessGuard`, writes by `DeviceWriteGuard`, as with the S7 transport.
The transport consults the guard; the runner does not know the guard exists.

Per ruling 2, **identity over Modbus is whatever the program publishes** — there is no SZL and no
device-identity service the user program does not implement. So:

- `IdentitySourcePlan` must not assume an order-code source exists on a Modbus transport; that
  assumption is S7-specific;
- wherever identity is documented, say plainly that a Modbus-only check verifies **what the program
  claims**, not what the silicon reports.

## 8. Error handling — apply what today taught

Today's failed read was valuable **only because the error was diagnosed rather than reported**. Four
causes produced an identical symptom; the discriminator was the exact error code plus a second probe.

- Surface the **exact** Modbus exception code and function code, never a flattened "read failed".
- Distinguish, and say which: **TCP-level failure** (no route, refused, timeout), **a Modbus exception
  response** (the server answered and objected — `0x02` illegal data address means the register is
  outside what the server exposes), and **a malformed or short reply**.
- Report the request that produced it — function, register, count — so it is reproducible with a
  third-party client.
- A timeout states what it was waiting for, and for how long.

## 9. No coil writes

Coil writes are never called, asserted by test. The harness has no use for them, and excluding them
keeps a whole function-code class out of the binary. The address-model reasoning is in the PLC-side
document.

## 10. Testing — and why round-trips are not enough

**A round-trip test against a fake server I also wrote cannot catch a wrong convention.** If the codec
believes 32-bit values are high-word-first and the fake stores them high-word-first, the test passes —
and would still pass if the real device were low-word-first. The belief is in both halves, so the test
only proves self-consistency. This is the correlated-check failure this project was built around.

Round-trip tests still earn their place: they catch typos, off-by-ones and buffer bugs. They cannot
catch a misunderstanding. Different bug class, different check.

**Acceptance criteria, not optional steps:**

1. **Known-answer vectors from an independent authority.** For each type, at least one case where the
   expected bytes come from Siemens documentation or a reference project — *never* from our own
   encoder. Cite the source in the test.
2. **A third-party SERVER.** Run the transport against an independently written Modbus server
   (`diagslave` from the modpoll suite, or equivalent) before the codec is trusted. Its conventions
   were decided by someone who never saw our code.
3. **A third-party CLIENT.** Confirm the same registers with `modpoll`/`mbpoll`. Today produced two
   cases where our tooling and a device disagreed; an independent dumb client is the cheapest way to
   find out which is wrong.

Then the ordinary suite: codec round-trips, map-loading validation, transport behaviour against the
fake (read, write, exception responses, timeout, reconnect), fence refusal naming the failing gate,
and the no-coil assertion.

## 11. Build order

1. Codecs — no I/O — with the known-answer vectors from §10.1 written first.
2. Register map model, loader, validation.
3. `ModbusTransport : ITransport` with block reads and snapshot semantics, against a fake server.
4. Fence integration, including the write path (per ruling 3 — built early, exercised throughout).
5. Verification against a third-party server and client (§10.2, §10.3).
6. A read-only CLI mirroring `rig-read`.
7. Device-facing arming — last.

Steps 1–6 need no PLC and no decision from anyone else.

## 12. Open — multi-agent use of one rig

Recorded because it changes requirements, and flagged as **not yet designed**.

The goal is for **several `lad-coder` agents to test their code concurrently during development**.
Constraints already visible:

- **One rig runs one program.** Two agents developing different blocks cannot both have their code
  running unless one program contains both. Any new code requires a download, which stops the CPU and
  disrupts every other agent — so **downloads serialise everything**, and that is likely the dominant
  constraint rather than tag collisions.
- **PLCSIM does not rescue this.** MEASURED: PLCSIM Advanced — the variant with an API a harness could
  drive — lists no S7-1200 CPU type at all, so buying it would not help. Software simulation of this
  target is largely closed off, which pushes back toward arbitrating the one rig.
- **Write collisions have an existing primitive.** `VectorSet.DeclaredAreas` already computes what a
  run will write, *from the vectors themselves* — the comment there notes this is deliberate because
  "hand-maintained scope lists drift wide". Two runs conflict iff their declared areas intersect,
  which is checkable and non-discretionary. The `converter claim` registry (shared, outside any
  worktree) is the natural place to hold a lease.
- **Reads are not safe either.** Agent A's writes change state agent B is observing, so disjoint
  *write* scopes are necessary but not sufficient. Isolation probably has to be structural — each
  agent's block under test getting its own instance and its own mirror region — rather than purely a
  lease.

To be designed separately. It affects the map (per-agent regions), the fence (leases), and the PLC
side (instance-per-agent scaffolding).
