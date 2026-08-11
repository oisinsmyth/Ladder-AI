# ADR-0009 — Write access to test-rig devices

- **Status:** **Accepted in principle — 2026-08-11.** All four decision questions answered by the
  owner (see §Decisions taken). **The enacting edit is outstanding:** `10-non-goals.md` #3 still reads
  as a blanket permanent write ban and must be re-scoped before this is more than a proposal —
  proposed replacement text is in §The enacting edit. Build scope beyond that is open.
- **Date:** drafted 2026-08-10; decided 2026-08-11.

## The decision in one table

| Target | Program & configuration writes<br>(download, online edit, force, HW config) | Process-data writes<br>(values the running program reads from outside) |
|---|---|---|
| **Test rig** — allowlisted **and** physically isolated | **Permitted** | **Permitted** |
| **Live plant / system in service** | **Not permitted** | **Not permitted** *(see the one open item)* |

**The gate is the TARGET, not the class of write.** On a qualifying rig the tooling may do anything an
engineer at a bench may do. On a device in service it writes nothing at all, of any kind — the
engineer makes the change in TIA Portal and the tooling assists around them (§Live plant).

- **Relates to:** `10-non-goals.md` **#3** (the exclusion this re-scopes) · **ADR-0008** (read-only
  live-device access — this is the "separate and heavier question" it deferred) · CLAUDE.md **hard
  rule 2** (safety — untouched, tightened here) · **hard rule 4** (compile gate) · **hard rule 5**
  (never bypass review — untouched) · `src/device-guard/` (the read fence, extended here) ·
  `13-data-boundary.md`.

## Context

### What needs the capability — three distinct uses

1. **The development loop.** The IR coder writes a change and needs to *run* it: download the block to
   a rig, exercise it in isolation, read the result, iterate. This needs **program writes**
   (a download) as well as data writes, and it is the reason the data-class distinction could not
   survive — a coding loop that cannot deploy code is not a coding loop.
2. **End-of-development plant testing.** The conformance harness: large volumes of reads and writes
   against the interface blocks, driving a whole plant model through its sequences. This is
   process-data-heavy and high-volume.
3. **A change to a system already in service.** Sometimes something is missed from the specification
   and the program must change on a running plant. **The engineer does that work.** The value this
   project adds there is analysis, proposal and read-back — not writing. Guard rails exist for exactly
   this case, so that "the tooling can write to rigs" never quietly becomes "the tooling can write to
   the plant."

### Why simulation is not a substitute here

The obvious objection — do all of this in PLCSIM — does not hold for the CPU family this project
targets. Measured and sourced 2026-08-10:

- A **classic S7-1200 can only be a PLCSIM *Standard* instance**, and Standard instances are confined
  to **Softbus**: no routable IP is bound, so **no external client can reach a simulated classic 1200
  at all** — not S7 protocol, not OPC UA, not the web server.
- **PLCSIM Advanced — the API-driven product — does not simulate S7-1200 in any version**, including
  V8.0 (11/2025). S7-1200 **G2** gained support only at V8.0 / TIA V21.
- **Virtual time scaling is unavailable for Standard instances**, so hours-long process phases cannot
  be compressed.
- **Counting/HSC, PID and motion are not simulated** for classic S7-1200 (they *are* for G2 — the two
  products differ exactly here).
- Siemens' own documentation states **scan cycle time and the timing of actions are not
  representative** of firmware, with no bound given.
- **Unsupported instructions silently return OK** — the worst possible failure for a test oracle,
  because assertions pass while nothing happened.
- Scan Control (pause, run N scans) *does* exist for Standard instances and is fully deterministic,
  but it is **GUI-only** — no API, script or CLI, so it cannot drive an automated suite.

So the only environment where this program can be executed faithfully and automatically is real
hardware. A rig is real hardware.

### Where the line moved, and why

This ADR was first drafted with the gate on the **class of write**: process data permitted, program
and configuration permanently banned everywhere. That draft recorded its own weakness — a command DB
is process data too, and it commands equipment, so *"the fence cannot rest on the data class alone —
it has to rest on the target."*

The owner took that to its conclusion and **replaced the data-class gate with a target gate**. That is
the better structure, for a reason worth stating: the data-class line was **unstable**. It had to be
re-litigated for every new kind of write (is a setpoint data? is a mode selector a command?), and each
answer was a judgement call in a document that should not need judgement calls at the point of use.
The target line is a fact about a box: either it is on the allowlist with its outputs disconnected, or
it is not. **A fence you can check is worth more than a fence you have to reason about.**

### What is not touched

**Hard rule 2 (safety) stands, and is tightened.** No write may target safety-tagged content on any
device, rig included. The client must refuse and *name* it, never silently skip — the same fail-closed
posture the export path takes. A rig being isolated does not make its safety content writable.

**Hard rule 5 (never bypass review) stands.** A rig is not the project. Nothing here creates a route
for AI output to enter a real TIA project without human review, and applying a change to a *project*
remains a human act. Note also that a coder deploying its own block to a rig and observing it pass is
**a build step, not a review** — it is evidence about behaviour, not a second opinion, and it does not
discharge the reviewer→fixer separation this project relies on.

**Hard rule 4 (compile gate) stands** and is unaffected: a block still compiles clean before it goes
anywhere, rig included.

## The two environments

### Test rig — everything, behind the fence

A qualifying rig is one that is **on the write allowlist** *and* **physically incapable of actuating**
(§The fence, items 1 and 3). On such a device the tooling may download, online-edit, force, write
configuration, and read and write any declared data area. This is deliberately the full set: it is
what an engineer at a bench already does, and withholding half of it would only push the work back to
a human without making anything safer, because the fence — not the verb list — is what contains the
risk.

### Live plant — no automated writes, and here is what *is* available

The tooling writes **nothing** to a device in service. That is not the same as being no help, and the
distinction matters because this is the case the engineer most often actually faces:

- **Read-only diagnostics off the live device** — already permitted by ADR-0008 and already built
  (`src/device-guard/` plus the read-only fetch client). Logs, trace artifacts, exported diagnostics.
- **Full analysis on what comes back**, on local files, under the ordinary rules.
- **Proposing the change** — IR diff, intent statement, compile evidence, reviewer findings, exactly
  as the normal pipeline produces.
- **Proving the change on a rig first**, which is the whole point of having one.
- **Reading back afterwards** to confirm the effect.

The engineer applies the change in TIA Portal. **This is measured, not assumed:** ADR-0008 records a
real debugging session run this way and concluded *"the human-download boundary was **not an
impediment** (one operator action per iteration)"*. The loop works; the human is one step in it.

## The fence

`src/device-guard/` is built, fail-closed, exact-match, no default path, 30 tests. It is a good read
fence and needs extending, because the failure modes are not comparable: the read fence's worst case
is reading a log it should not have; the write fence's worst case is a device changing state.

1. **Write-eligibility is a separate grant.** Read-listed must never imply write-listed. A distinct
   field and a distinct deliberate opt-in per device, so the existing read list cannot silently
   acquire write rights.
2. **Structural separation, not a runtime check.** Write verbs live where they cannot resolve a
   read-only entry at all — ADR-0007's lesson that a gate you can bypass is not a gate.
3. **Physical isolation — MANDATORY (decided 2026-08-11).** A write-listed device's **outputs must be
   physically incapable of actuating**: field wiring disconnected, or interposing relays unpowered.
   Recorded in the allowlist entry as an assertion by a named human, the way ADR-0008 made "this is a
   rig" durable. Verified at listing time and **re-verified whenever a rig is re-purposed** — exactly
   the moment it will otherwise be forgotten. **A device that cannot be asserted physically isolated
   is not write-listable, full stop.** With the data-class gate gone this is no longer the strongest
   fence among several; it is the one whose failure mode is not a configuration file, and the whole
   scheme rests on it.
4. **A declared write surface, per device.** The surface may name **anything the campaign requires** —
   any data area, and the program and configuration verbs — up to everything on that rig. What it may
   not do is *default* to "any address on any listed device". The declaration is the difference
   between **broad** and **unbounded**: written down per rig, reviewable, and anything outside it
   refused, the same "empty grants nothing" posture as the allowlist. A client that writes anywhere on
   any rig by default is a device shell; a client that writes everywhere on *one declared rig* because
   the tests need it is a test harness.
5. **Program and configuration verbs are gated identically, not separately.** *(Rewritten 2026-08-11 —
   the original text forbade them outright.)* Download, online edit, force and hardware-configuration
   writes resolve through the **same** allowlist and the **same** isolation assertion as a data write.
   There is deliberately no second, lighter path for them: the dangerous verbs must not be the ones
   with the thinner gate. Note that **Openness cannot write live values at all** — verified across all
   2,182 exported types in the V20 assembly — so a download would go through Openness's
   `DownloadProvider` while data writes go over S7 protocol or OPC UA. Two transports, one fence.
6. **Forces get an extra obligation.** A force is the one write that **persists beyond the run** — it
   survives the session, can survive a download, and on an S7-1200 applies to peripheral I/O only.
   Every force must be recorded and **cleared at end of run, with the clear verified**; a rig must not
   be left carrying a force from a suite that crashed. This is the write equivalent of a stale claim.
7. **An audit trail.** Every write recorded with target, verb, area, value and the run that issued it.
   A harness generates thousands; the log is what makes "what did it do to the rig" answerable.
8. **The I/O mapping layer is inhibited for the duration of a data test** *(owner requirement)*. The
   functions copying raw physical addresses into the interface layer, and the interface layer back out
   to physical outputs, are gated off in **both** directions. Two reasons, which fail differently:
   - **Validity.** If input mapping keeps running it overwrites every injected value on the next scan.
     Without this, injection does not merely leak — *it does not work at all*, and the failure is
     silent: the test drives nothing while the program carries on reading the field.
   - **Isolation.** With output mapping gated, nothing the program commands reaches a terminal — a
     software layer beneath the physical one in item 3. Deliberately redundant: **item 3 survives a
     software bug; item 8 survives a rig someone quietly re-wired.**

   **Verified, never assumed.** The harness reads back positive confirmation the inhibit is engaged and
   **refuses to write without it**, re-checked during a run and not only at the start, since the flag
   lives in memory this capability may write. An inhibit that is merely requested is not an inhibit.

   **This constrains the program under test, not the tooling.** Logic that reads `%I` and writes `%Q`
   directly mid-sequence **cannot be data-tested under this ADR at all** — a real limit on
   availability, and a design pressure toward the I/O-layer indirection a per-vessel simulation mode
   needs anyway.
9. **Empty grants nothing**, inherited from device-guard and FI-44's "empty is not clean".

None of this is safety-grade. A rig is chosen precisely so a mistake is survivable; the fence's job is
to make the routine case impossible to get wrong and to leave a trail when overridden.

## Options considered

1. **Target-gated — everything on a qualifying rig, nothing on a device in service.** *(Chosen.)*
   Serves all three uses in §Context. The fence is checkable rather than arguable. Costs: re-scopes a
   permanent exclusion; makes the allowlist safety-adjacent; commits to a write client, its credential
   story and its audit trail.
2. **Class-gated — process data anywhere qualifying, program/config nowhere.** The original draft.
   Rejected because it cannot serve the development loop at all (no download, no block test) and
   because the class line is unstable in use.
3. **Reject — hold the blanket ban.** Coherent and cheap. The repo builds no write path; conformance
   testing against real hardware does not exist; what remains is read-only observation, the GUI-paced
   PLCSIM loop, and the existing gates. Regression testing survives; correctness testing does not.
4. **Defer — build read-only observation now, revisit on evidence.** Costs a manual loop; buys
   evidence before re-scoping anything.

## Consequences

**The accountability property is given up for rigs and kept for plant.** `10-non-goals.md` #3 says
*"applying any change to a live device remains a human act in TIA Portal."* On a rig that is now
false by design — an agent will change device state without a person in the loop. On a device in
service it remains true and is the load-bearing half of this decision. The re-scope must make that
split explicit rather than leaving one sentence covering both.

**"Permanent" becomes conditional, and the cost is not local.** A project that re-scopes a permanent
exclusion has, from then on, permanent exclusions a reader must check rather than rely on. The
mitigation is to make the new boundary *at least as legible* as the old one — hence the table at the
top of this document and the proposed replacement text below.

**The allowlist is now safety-adjacent.** "How did this address get on the write list" needs a real
answer and a review discipline stronger than the read list's. The isolation assertion is the part that
will rot: rigs get re-wired and re-purposed, and nothing in a config file notices.

**Fence item 8 buys validity and isolation at the cost of a permanent coverage hole, and it should be
written on the tin.** With the mapping layer gated, everything testable sits *above* it — and **the
mapping functions themselves are the one part write-testing can never exercise.** Nor can they be
covered by relaxing the gate, because item 3 makes isolation mandatory: there are no live terminals to
map to. Address decoding, scaling, polarity and channel-to-symbol assignment stay outside every
automated gate this project has, provable only by inspection, by cross-reference against the I/O
schedule, or by a human at a real machine. Acceptable — the layer is thin and mechanical while the
sequence logic above it is neither — but **a suite green on everything except the layer nobody can
test must never be reported as covering the program.**

**Credentials and the data boundary.** Writes need auth to the device; credential scope and the
never-committed rule apply as for reads. Anything read back from a rig mirroring a real job falls
under `Live Runs/` retention: use freely, commit nothing.

**A rig now needs to be a maintained asset.** For the development loop to be useful, a rig has to be
available, loaded with a representative configuration, and trusted. That is an ongoing cost this
decision quietly commits to, separate from the software.

### Conflict of interest, recorded

The original recommendation came from the same agent that designed the harness this unblocks — the
correlated-check pattern the `PlantAutoControl-bench` autopsy exists to name. The scope was set by the
owner rather than adopted from that recommendation, which reduces but does not remove the concern. The
evidence in §Context is independently verifiable; the judgement is the owner's.

## The enacting edit

`10-non-goals.md` #3 currently reads as a blanket permanent ban and now contradicts this ADR. Proposed
replacement, for approval — **not yet applied**:

> **3. Writing to hardware in service.** No tool in this repo writes to a device that is in service —
> no download, no online edit, no tag force, no configuration write, and no process-data write.
> Applying any change to a live device remains a human act in TIA Portal; the tooling's role there is
> read-only diagnostics (ADR-0008), analysis, and proposing the change. **Writing to an allowlisted
> test rig whose outputs are physically incapable of actuating is permitted, and is governed by
> ADR-0009** — that is the only exception, and it is a statement about the *target*, never about the
> kind of write. The pipeline still ends at "imported and compiled in the TIA project" for anything it
> *produces*.

## What must be decided

Questions 1–4 are closed: the exclusion is re-scoped (1); the gate is the target, not the class, with
program/config permitted on rigs and nothing permitted on plant (2); physical isolation is mandatory
(3); no data-class restriction applies (4). **One item remains:**

- **Are process-data writes to a device in service permitted, or not?** The instruction — *"all
  program and config writes available when in a test rig but not on a live plant; process data writes
  are ok too"* — reads most naturally as "process data is also permitted **on the rig**", and the
  table and the proposed replacement text above are written that way, which is the conservative
  reading. If instead process-data writes were meant to be permitted **on a plant in service** — an
  engineer-supervised setpoint or command write — say so and both need changing. Recording it as a
  question rather than guessing, because it is the single line separating the two halves of the table.

## Revisit triggers

Revisit if: a write is ever issued to a device that turned out not to be isolated, or a force is found
left behind after a run — either is evidence the fence is being managed rather than enforced; the
declared surface starts growing past what campaigns actually use; or the target CPU changes to one the
simulator supports properly (an S7-1200 **G2** or an S7-1500), at which point much of §Context's
argument dissolves and testing should move back to simulation, leaving hardware writes unused rather
than merely unexercised.
