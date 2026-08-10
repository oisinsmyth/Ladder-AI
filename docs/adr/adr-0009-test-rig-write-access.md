# ADR-0009 — Write access to test-rig devices, process data only

- **Status:** **Proposed — 2026-08-10. Not accepted, and not accept-able by anyone but the owner.**
  This document asks to **reopen a permanent exclusion**. `10-non-goals.md` #3 says the write
  direction is "permanently excluded"; ADR-0008 §Context says the question "would require reopening a
  *permanent* exclusion, which this project has said it does not do", and its revisit triggers name
  write appetite as explicitly **not** a trigger. None of that is oversight — it was written
  deliberately, five hours before this. **This ADR exists because the premise it rested on turned out
  to be false, not because the appetite changed.** If the owner holds the line, option 3 records
  exactly what that costs and the answer is coherent.
- **Date:** 2026-08-10
- **Relates to:** `10-non-goals.md` **Permanent #3** (the exclusion this asks to narrow) ·
  **ADR-0008** (read-only live-device access — this is the "separate and heavier question" it
  deferred) · CLAUDE.md **hard rule 2** (safety — untouched, and tightened here) · **hard rule 5**
  (never bypass review — untouched) · `src/device-guard/` (the read fence, which is **not** sufficient
  as-is) · `13-data-boundary.md` · the trigger: the conformance test harness.

## Context

### What needs the capability

A conformance test harness for generated ladder logic. Its purpose is to catch the defect class no
existing gate catches: the compile gate proves the program *builds*, the reviewers prove it is
*readable and internally consistent*, and neither can tell you it does what the specification says.
Three of the worst defects found on the current job were scan-order faults across blocks, and **not
one was found by a compile, a preflight, a round-trip or a sanity-check** — all were found by a human
reading scan order.

A harness that can **observe** a device but not **stimulate** it can only regression-test: it detects
that behaviour changed, never that behaviour is wrong. Conformance against a specification requires
driving inputs and comparing outputs to values derived independently from the spec. **Stimulus is not
a convenience in that design; it is the half that makes it a correctness check rather than a change
detector.**

### Why the simulator is not the answer here, which is the new fact

ADR-0008 could reasonably assume that anything needing writes could be done in simulation. For the
CPU family this project actually targets, that assumption does not hold. Measured and sourced
2026-08-10:

- A **classic S7-1200 can only be a PLCSIM *Standard* instance**, and Standard instances are confined
  to **Softbus**. No routable IP is ever bound, so **no external client can reach a simulated classic
  1200 at all** — not S7 protocol, not OPC UA, not the web server.
- **PLCSIM Advanced — the API-driven product — does not simulate S7-1200 in any version**, including
  V8.0 (11/2025). S7-1200 **G2** gained support only at V8.0 / TIA V21. The classic 1214C is named
  unsupported throughout.
- **Virtual time scaling is unavailable for Standard instances**, so hours-long process phases cannot
  be compressed.
- **Counting/HSC, PID and motion are not simulated** for classic S7-1200 (they *are* for G2 — the two
  products differ exactly here).
- PLCSIM's own documentation states **scan cycle time and the timing of actions are not
  representative** of firmware, with no bound given.
- **Unsupported instructions silently return OK** — the worst possible failure mode for a test
  oracle, because assertions pass while nothing happened.
- Scan Control (pause / run N scans) *does* exist for Standard instances — correcting a belief held
  earlier in the day — but it is **GUI-only**, with no API, script or CLI.

The net: the one environment where a harness could run against this CPU faithfully is **real
hardware**, and that is precisely the environment the write ban closes. That inversion is the new
information. It did not exist when ADR-0008 was written.

### What is not on the table

**Program and configuration writes stay permanently excluded, and this ADR does not touch them.**
Download to a PLC, online edit, tag force — the three things `10-non-goals.md` #3 names — remain
banned in every environment, on every device, permanently. Nothing below creates a route to any of
them, and acceptance must not be read as softening them.

**Hard rule 2 is untouched and gets stricter.** No write may target safety-tagged content; the client
must refuse and *name* it, never silently skip it — the same fail-closed posture the export path
takes.

**Hard rule 5 is untouched.** This creates no route for AI output to enter a real project. A rig is
not the project; anything the harness demonstrates still goes to a human reviewer, and applying a
change to a *project* remains a human act in TIA Portal.

## The distinction this ADR rests on — stated with its own weakness

There is a real difference between two things that "writing to a device" currently conflates:

- **Program / configuration writes** change *what the controller is*: its logic, its hardware
  configuration, its forced I/O state. A forced tag is not process data — it is an override of the
  program's own reasoning, and it persists beyond whoever set it.
- **Process-data writes** put a value where the running program is *designed* to receive one from
  outside. The clearest case in the current job: the specification has the PLC **read** a per-silo
  weighing interface DB and **never write it**; the comms client owns that DB. A harness writing that
  DB is impersonating a device the architecture already expects to be there. That is a different act
  from forcing a tag, not a euphemism for it.

**The weakness, stated plainly because it is the thing that could go wrong.** The distinction is not
self-limiting. A *command* DB is also process data, and it commands equipment. On a bench with the
outputs disconnected nothing moves; on a plant, things move. So **the fence cannot rest on the data
class alone — it has to rest on the target.** Any version of this decision that gates only on "is it
process data" is unsafe and should be rejected.

## The fence — what a write fence needs that the read fence does not

`src/device-guard/` is built, fail-closed, exact-match, no default path, 30 tests. It is a good read
fence and it is **not sufficient here unchanged**, for one reason: the failure modes are not
comparable. The read fence's worst case is *we read a log we should not have read*. The write fence's
worst case is *we moved an output on a live plant*. Same allowlist, categorically different
consequence.

What a write path needs on top:

1. **Write-eligibility is a separate grant.** Being read-listed must never imply write-listed. A
   distinct field and a distinct deliberate opt-in per device, so the existing list cannot silently
   acquire write rights.
2. **Structural separation, not a runtime check.** The write verbs live where they cannot resolve a
   read-only entry at all — ADR-0007's lesson that a gate you can bypass is not a gate, and
   ADR-0008's own "read-only by construction" reasoning applied in the other direction.
3. **A physical precondition, because it is the only gate a software bug cannot cross.** Listing a
   device for writes requires its **outputs to be physically incapable of actuating** — field wiring
   disconnected, or interposing relays unpowered. Recorded in the allowlist entry as an assertion by a
   named human, the way ADR-0008 made "this is a rig" durable. Every software fence here is a config
   file; this one is not.
4. **A bounded write surface, declared per device.** Named areas only — never "any address". A client
   that can write anywhere on the rig is a device shell, which is the thing #3 exists to prevent.
5. **No program/config linkage at all**, structurally: no `DownloadProvider`, no force verbs, nothing
   that could reach hardware configuration. (Note that Openness itself cannot write live values at
   all — verified across all 2,182 exported types — so this path cannot be built on Openness even by
   accident; it would be S7 protocol or OPC UA.)
6. **An audit trail**: every write recorded with target, area, value and the run that issued it. A
   test harness generates thousands of writes; the log is what makes "what did it do to the rig"
   answerable afterwards.
7. **Empty grants nothing**, inherited from device-guard and from FI-44's "empty is not clean".

None of this is safety-grade, and a rig is chosen precisely so a mistake is survivable. The fence's
job is to make the routine case impossible to get wrong and to leave a trail when overridden.

## Options considered

1. **Accept, narrow — process-data writes only, to allowlisted *and* physically-isolated test rigs, a
   declared bounded surface per device, never program/config.** Unblocks the conformance harness on
   the only environment where it can run faithfully. Costs: reopens a permanent exclusion; makes the
   allowlist safety-adjacent; commits to building and maintaining a write client and its credential
   story. *Recommended if the harness is to be built at all — see the conflict-of-interest note below.*
2. **Accept, broad — general write access to rigs, any area, forcing included.** Rejected. That is a
   device shell; it discards the only distinction that makes option 1 arguable, and it is exactly what
   #3 was written to prevent.
3. **Reject — hold the permanent exclusion.** Coherent, and cheap to state. Consequences: this repo
   does not build the stimulus half, so automated conformance testing against the real CPU does not
   exist. What remains is the **read-only** observation half (permitted today under ADR-0008), the
   GUI-driven PLCSIM loop — Scan Control plus SIM tables with Consistent Modify, which *is* fully
   deterministic but human-paced and unautomatable — and the existing gates. Regression testing
   survives; conformance testing does not. If the answer is "a human drives the stimulus", say so
   explicitly, because that is a real and defensible position and it should not be re-litigated.
4. **Defer — build the read-only observation half now, revisit stimulus on evidence.** Mirrors
   ADR-0008's own option 4 and this project's "build the skill on the second manual run" rule. Costs a
   manual stimulus loop for now; buys evidence about whether it is a real tax before reopening
   anything permanent. **Genuinely attractive**, because the observation half is permitted today, is
   independently useful, and its design does not depend on how this question resolves.

## Consequences

**Accepting (option 1) retires a property that is not primarily about safety.**
`10-non-goals.md` #3 says *"applying any change to a live device remains a human act in TIA Portal."*
That is an **accountability** property: for every change to a device, a person did it. A harness that
writes stimulus removes the human from that loop for the class of writes it makes. On a rig with
disconnected outputs the physical stakes are near zero — but the property being given up should be
named as what it is, not smuggled through as a safety argument that the physical fence answers.

**"Permanent" loses some of its force, and that cost is real and not local.** A project that reopens
a permanent exclusion on good evidence has, from then on, permanent exclusions that a reader must
check rather than rely on. Two mitigations, both required if this is accepted:
- **Amend `10-non-goals.md` #3 explicitly** rather than leaving it standing and contradicted. A
  governing document that is silently overridden by an ADR is worse than either.
- **Restate the program/config half as permanent** in the same edit, so what remains excluded is
  narrower but not weaker.

**The allowlist becomes safety-adjacent.** "How did this address get on the write list" needs a real
answer, a review discipline stronger than the read list's, and the physical-isolation assertion has to
be re-checked when a rig is re-purposed — which is exactly when it will be forgotten.

**Credentials and the data boundary.** Writes need auth to the device, so credential scope and the
"never committed" rule apply as they do for reads. Anything read back from a rig mirroring a real job
falls under `Live Runs/` retention: use freely, commit nothing.

**Rejecting (option 3) costs the conformance harness on hardware** and leaves only a GUI-paced loop on
a simulator that, for this CPU, is documented as timing-unrepresentative, silently OK-ing unsupported
instructions, and unable to simulate counting at all. The regression half survives; the correctness
half does not.

**Deferring (option 4) costs nothing that is not already being paid**, and the observation half is
buildable today either way.

### Conflict of interest, recorded

The recommendation for option 1 comes from the same agent that designed the harness this ADR would
unblock. That is precisely the correlated-check pattern this project's `PlantAutoControl-bench` autopsy
exists to name. **This document should not be accepted on its own recommendation.** The evidence in
§Context is verifiable independently (Siemens documentation and the installed assemblies); the
judgement that it justifies reopening a permanent exclusion is not mine to make, and would benefit
from a reader who has no stake in the harness being built.

## What must be decided

1. **Is the permanent exclusion reopened at all?** Owner only. Options 3 and 4 are both complete
   answers.
2. If yes — **is the program/config half restated as permanent** in the same `10-non-goals.md` edit?
   *(Recommended: yes, explicitly.)*
3. **Is physical output isolation mandatory or advisory** for a write-listed device? *(Recommended:
   mandatory — it is the only fence that is not a config file.)*
4. **Does the bounded surface include command DBs, or only sensor/interface DBs?** These are not
   equivalent: writing a weighing interface DB impersonates an instrument, while writing a command DB
   issues operator commands. A harness needs both to test a sequence end to end, and the second is
   materially more dangerous if ever pointed at a live device. *No recommendation — this is the
   sharpest question in the document.*

## Revisit triggers

If **rejected or deferred**: revisit if the manual stimulus loop proves a recurring tax across several
test cycles; if a second, different write need appears that is not the harness; or if the target CPU
changes to one the simulator supports properly (an S7-1200 **G2** or an S7-1500 would remove most of
§Context's argument, since PLCSIM Advanced and its API cover both — at which point the honest answer
is to test there and leave #3 whole).

If **accepted**: revisit if a write is ever issued to a device that turned out not to be isolated, or
if the write surface starts growing past the declared areas — either is evidence the fence is being
managed rather than enforced.
