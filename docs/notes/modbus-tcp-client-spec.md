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
| round trip, p99 — **narrow sweep, 1–16 registers** | 136–173 ms — **superseded as a budgeting figure, see below** |
| round trip, worst in 2,000 samples | **2,216 ms** (0.05%) |

**FULL-WIDTH RE-MEASUREMENT, 2026-08-13 — 123 registers, four runs.** The p99 above was derived at
one width and was being applied at another:

| | measured [M] |
|---|---|
| median, full width | **75–77 ms** — confirms the 78 ms working figure, unchanged |
| p99, per run | **157 / 201 / 168 / 185 ms** — two of four exceed 173 |
| p99, n-weighted | ~183 ms (central estimate, **not** the constant used) |
| **p99 — used for every BOUND** | **201 ms** — the *worst observed*, deliberately |
| **p90 — used for every DURATION** | **102.79 ms** — pooled, n = 16,200, CI [101.77, 103.93] |
| **`exceed_250`** | **0.352%** (57/16,200) — *"about one round trip in 300"*; CI ±26%, so never quote it to three figures |
| marginal cost per register | **~0.040 ms on writes, no trend on reads** — so a full-width write costs ~4.9 ms more than a single-register one, ~6% of a round trip |

**Why the worst observed and not the middle:** four runs is a thin basis for a tail statistic (SD
19.3 ms, standard error 9.7, so a ~95% upper bound on the mean run-p99 is ≈197 — 201 lands there
anyway), and the cost asymmetry is one-sided — too high costs a little throughput, too low costs a
**missed observation reported as a pass**. A budget on an uncertain tail fails toward the pessimistic
side. **Confidence is low-to-moderate; more runs is the only thing that firms it up.** Full reasoning:
`PC-Client-Modbus-Spec-Draft-final.txt` §12a constants block.

> **Measured, and it comes back against the width hypothesis.** This note used to say the causal claim
> was *not established* and name the experiment that would settle it. That experiment was run —
> interleaved 16 → 64 → 123 registers, never blocked, **16,200 round trips in one session** — and
> **no width effect on the tail is detectable**: pooled p99 is highest at width **16** in the read arm
> (188/164/158) and at width **123** in the write arm (156/152/195), per-run estimates have opposite
> signs (−47.5 / +51.3 ms) with CIs spanning zero, and all six cells fall in 152–195 ms, bracketing
> both 173 and 201.
>
> **Read the limits with the result.** The design is *not* powered for 28 ms at the p99 (smallest
> detectable 83 ms read / 145 ms write — a per-run p99 is itself an extreme-value statistic), so this
> is **not a strong null at the p99**. It rests on the **p90**, which *is* powered (13/21 ms
> detectable) and gives **+0.5 and +3.4 ms** — a 28 ms width effect at p90 is excluded in both arms.
> And one fact needs no power argument: at a *single fixed width* within one session, per-run p99
> ranged **136 → 371 ms** (read w16) and **136 → 562 ms** (write w123).
>
> **So `RTT_p99 = 201` is a SESSION figure, not a width figure.** It stands unchanged — session-level
> variation alone accounts for the 173 → 201 move — but it is **revisited on BREADTH (many sessions,
> different times of day), not on width.** One session cannot answer how bad the tail gets across days.
>
> The body *does* move with width, reproducibly and tinily: median **74.8 → 75.7 ms** from 16 to 123
> registers, the same **+1.0 ms** in both arms.

`ITransport.Read` is per-tag, so a naive implementation costs one round trip per tag. Re-derived on
the measured figures (`PC-Client-Modbus-Spec-Draft-final.txt` §12a derivation 6):

- a 50-tag assertion sweep, one round trip each: **5.1 s at `RTT_p90`** (10.1 s at the p99, the
  pessimistic case) — plus **~0.18 expected round trips over 250 ms per sweep, about one sweep in
  six**;
- the same 50 tags inside **one** FC03 (≤125 registers): **one** round trip, **102.79 ms at
  `RTT_p90`** (201 ms at the p99) — a **50x** reduction;
- a scan-counter poll: one round trip, **102.79 ms at `RTT_p90`** (201 ms at the p99).

> 🔴 **These are DURATION-shaped figures, which is why they key on `RTT_p90`. Do not carry that
> substitution into a timeout.** §12a's F-5 kind rule: **bound-shaped figures — timeouts, backstops,
> caps, admission limits — keep `RTT_p99`, and `RTT_max` remains the outlier term.** By definition
> **10% of round trips exceed the p90**, so a timeout set there would fire on one request in ten.

**Therefore `ModbusTransport` reads in blocks and serves tags from a snapshot.** One transaction
fetches a contiguous register range; individual tag reads decode out of that buffer.

**The near-zero marginal register cost makes this stronger than it was, and changes how the map should
be laid out**: the map is laid out for the *widest legal read*, not the smallest sufficient one. The
trade is now measured rather than asserted —

| | cost |
|---|---|
| widening a read across the full range | **~1.0 ms** (interleaved, better controlled) — **~4.9 ms** (across-session, conservative) |
| one additional round trip | **102.79 ms** at `RTT_p90` |

— so **batching wins by at least 21x**, and by ~100x on the better-controlled figure. The
conservative number is the one quoted, so nothing turns on which is right. Under the old assumption a narrow range was a saving; it is worth hundredths of a millisecond
per register, against 78–201 ms for every extra exchange it forces.

> ✅ **The earlier `[I]` is retired — measured at 123 registers, and it held.** This note used to warn
> that the zero-cost figure was inferred beyond 16 registers. It is now measured, in both the body and
> the tail (see the width experiment above), and **the tail caveat this note briefly carried is
> withdrawn: no width effect on the tail is detectable, so a tail-keyed, width-maximising budget needs
> no special check. Nothing argues for capping read width on timing grounds.**

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

### 6a. Slot reads — a split read must be *unexpressible*, not merely rejected

**X-A's rule, as amended 2026-08-13 (F-1, adopted): a read never *splits* a slot.** It may cover
several **whole** slots — that is the point of the change, and it is worth up to 6x fewer read round
trips. What it may never do is return *part* of a slot, because polling runs *during* a test and a
partial read can catch that slot's publish half-done, yielding a coherent-looking result nobody ever
published.

**The enforcement is the shape of the call, not a check on its arguments:**

```
read(first_slot, slot_count)          // slot units. There is no register offset to supply.
    start  = mirror_base + first_slot * slot_size
    length = slot_count  * slot_size            // <= 125, checked at map-derivation time
```

> ⚠️ **Do NOT implement this as a validator over a register-range read.** A `read(start, length)`
> guarded by `assert(start % slot_size == 0)` meets the letter of the rule and throws away its
> mechanism — the unsafe call still exists, still compiles, and is one refactor from being reached.
> **`MirrorClient` is the precedent**: an out-of-region write there is *unaddressable*, not caught.
> A caller wanting half a slot must have nothing to type.

**Consequence for the map, and it reverses earlier guidance:** slots-per-read is `R = floor(125 /
slot_size)`, so **padding a slot is no longer free** — it is paid in round trips, and in O11's
concurrency cap, which is proportional to `R`. Size a slot to what its contents need. (Note this is a
*different* object from the tag-block reads in §5, where "widest legal read" remains right: a read
should still fill its 125 registers — with **whole slots**.)

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
