# Pattern: valve-two-state (equipment-instance kind)

**Status: PROPOSED. Admitted to the library by owner instruction, ahead of the admission criteria
in `docs/07-pattern-library-spec.md` — see "Admission status" below for which criteria are not met
and why it was admitted anyway.**

---

## ⚠ THIS PATTERN IS NOT YET PROVEN

**It has never run on hardware. Not once, not partially, not on a bench.**

Everything recorded under "What has actually been verified" below is desk verification: it parses,
it converts, it imports into a TIA project, it compiles with zero errors and zero warnings, it
round-trips losslessly, and it passes the mechanical convention checks. **None of that is evidence
that it controls a valve correctly.** No timing has been observed. No fault has been provoked. No
operator has used it. No commissioning engineer has argued with it.

It becomes proven when the system it was written for has been **successfully installed and is
running**, and not before. Until that happens, **anyone using this pattern is using unproven code**
and carries the risk of doing so — including the risk that a defect here is now replicated across
every instance they created from it.

This is a departure from the library's own founding rule. `patterns/README.md` opens with "Proven
LAD, extracted and documented from real working logic — never invented speculatively", and this
block is the opposite of that: written fresh, from a specification, and promoted before it ran.
The owner made that call deliberately and knowingly. The declaration exists so that nobody
downstream has to reconstruct it.

**Do not remove or soften this section until the proving event has actually happened**, and when it
does, replace it with what was found — including anything that had to be changed.

---

## Intent

One reusable FB for a **two-state valve**: one energised state that OPENS, one de-energised state
that CLOSES, with the de-energised state as the fail-safe. There is no close command — the output
dropping *is* the close, and the valve fails shut with no energy on it.

It covers the command path (automatic source, hand source, fail-safe), both fault detections, the
two consequence-class condensations, the upstream-enable handshake, an operation counter, a status
code and the alarm word. Per `docs/06-lad-conventions.md` C-106, repeated equipment gets one
standard FB plus one UDT interface, and this is that pair for valves.

**Structure is deliberately `patterns/motor-dol`'s**, member for member where it transfers. A DOL
motor and a two-state valve are the same problem in different clothes — an automatic source, a hand
source, a fail-safe that beats both, a feedback that can only prove non-arrival, a shared fail-to
time, a latched fault surface, an upstream enable, a wear count, a status code and an alarm word.
Where the motor's shape did not transfer, the network that would have carried it says so.

**Source:** written fresh against a specification, not extracted from working logic. Verified in a
scratch TIA project on 2026-08-06.

### Written as a library block, and deliberately over-specified

*(Recorded here on 2026-08-07, moved out of the block's own header comment under C-204 — a comment
is documentation, not the place a design choice is defended.)*

The block is **deliberately over-specified against any single application**: it carries capability a
given plant will never exercise. The reason is asymmetric cost — **stripping a proven capability out
is cheap, and discovering a missing one at commissioning is not.** An instance that needs none of
the consequence-class split, none of the suppression and no operation count pays two Bools, an Int
and a UDInt for them, and nothing else.

Nothing in the block names a plant, a process or a site alarm register. **Everything site-specific
belongs to how an instance is WIRED, never to the block** — which is the same statement as "the
position feedback is a computed Bool", generalised: thresholds, windows, magnitudes, attribution
and alarm-bit allocation all live outside.

## When to use

- Any valve driven by a single digital output where energised means open and de-energised means
  closed, and where de-energised is the safe direction.
- Valves with a real position limit contact **and** valves commanded blind — the same block serves
  both, neutralised per instance by wiring (`PositionValid`) rather than by having two blocks.
- Valves whose position is **inferred** rather than sensed — from a flow, a level, a weight, a
  pressure, a rate of change. The block takes a computed Bool and does not know or care which.

## When *not* to use

- **Modulating or positioning valves.** This block has two states and no analog anything. A
  control valve on a loop needs a different block that does not exist yet.
- **Valves with two independent solenoids** (energise-to-open *and* energise-to-close). The
  fail-safe reasoning here rests on there being exactly one output and on de-energising being safe;
  neither holds for a double-acting valve without a defined de-energised state.
- **Motors and drives** — `patterns/motor-dol` for a DOL starter.
- **Where the operator interface sends momentary button presses.** `HandOpenSignal` is specified as
  a MAINTAINED level and this block carries none of the latching the motor pattern needs for
  pushbuttons. That machinery would have to be added.

## Interface

Per **C-132**, the entire caller-visible interface is one `STATIC` member, `IO : "UDT_Valve" RETAIN
SETPOINT`, and the block declares **no `INPUT`, `OUTPUT` or `IN_OUT` parameters at all**. The
instance DB *is* the interface: the caller writes into it, the HMI binds to it, and retention is
declarable because the FB static is the only place it can be stated (a UDT member carries no
`Remanence` attribute in the real XML shape — FI-47).

### The caller's adjacency obligation — this is part of the pattern, not advice

C-132's cost is real: **the call site shows nothing.** `CALL FB_Valve(iDB_Valve_Inlet, EN := TRUE)`
does not reveal what was wired, where a parameter interface would. The mitigation is mandatory and
it falls on the caller, not on this block:

> **Every write to an instance's struct sits immediately adjacent to that instance's `CALL`, never
> scattered.** A reviewer who cannot see the writes beside the call should treat that as a finding.

`examples/calling-network.ir` is that shape written out. Read it as the argument list the call site
does not have.

**Caller writes:**

| Member | Type | Meaning |
|---|---|---|
| `AutoOpenSignal` | `Bool` | Open command from the automatic source. The default path. |
| `HandOpenSignal` | `Bool` | Open command from the operator side. **Maintained, not momentary.** |
| `InHand` | `Bool` | Operator has the valve in hand. Selects the hand source *instead of* auto — the two replace each other, never combine. |
| `PositionFB` | `Bool` | **The valve is proven open.** Real contact or caller's inference; the block cannot tell. |
| `PositionValid` | `Bool` | **C-131's cannot-tell state.** The statement above is meaningful right now. Gates whether either fault may arm. |
| `FBSenseInvert` | `Bool` | The position source proves CLOSED rather than OPEN. |
| `FaultFB` | `Bool` | External fault this block cannot detect itself (actuator thermal, driver fault, valve-island diagnostic). |
| `SystemHealthy` | `Bool` | Permissive that must be present to drive the valve at all. |
| `InhibitValve` | `Bool` | External inhibit — process interlock, trip, shared-resource refusal, **or any interlock that reads a sibling valve**. |
| `FaultReset` | `Bool` | From the one plant-wide reset. Clears the latched causes. |
| `FTTime` | `Real` | **Seconds.** Fail-to time, shared by both directions. Must not be zero. |
| `EnableUPSTime` | `Real` | **Seconds.** How long proven-open before `UPSEnable`. |
| `FailCloseCritical` | `Bool` | This instance's fail-to-close is the CRITICAL class rather than the ERROR class. |
| `SuppFTO` / `SuppFTC` | `Bool` | Alarm-bit suppressors. Reach the alarm bit only. |
| `SuppCause` | `Int` | Suppressor address, encoding entirely the application's. The block never touches it. |

**Caller reads:**

| Member | Type | Meaning |
|---|---|---|
| `OpenValve` | `Bool` | **The commanded state.** The only member the output mapping should read. |
| `UPSEnable` | `Bool` | C-115 handshake out — proven open, downstream may run. |
| `FaultActive` | `Bool` | **The one bit control logic reads.** Pure OR of the three latched causes. |
| `CriticalActive` / `ErrorActive` | `Bool` | The same causes condensed by consequence class. |
| `FTO` / `FTC` / `ExtFault` | `Bool` | The individual latched causes, if something genuinely needs one. |
| `OverrideActive` | `Bool` | Standing: this valve is in hand. Not a fault. |
| `Alarm` | `Word` | Bits 0–3, written in one network. **Bits 4–15 are the application's.** |
| `Telemetry` | `Int` | −1 faulted, 0 closed, 1 commanded open and travelling, 2 proven open. |
| `CycleCount` | `UDInt` | Proven-open operations, debounced. A wear figure. |

## How C-131 governs both detections

**C-131: a feedback proves NON-arrival. It never proves arrival.** Both detections here fire on
**positive evidence of the wrong state** and neither fires on the absence of evidence of the right
one:

- `FTO` — commanded open, position source trusted, and it says **not open**, for longer than `FTTime`.
- `FTC` — commanded shut, position source trusted, and it says **still open**, for longer than `FTTime`.

`FTC` proves the valve did *not* close. **It never proves that it did.** A quiet feedback is
absence of evidence, not evidence of absence: the actuator could have stalled between limits, the
seat could be holding on debris, the sensor could have failed low. A valve nobody can currently
judge therefore leaves the fault **quiet**, not **clear** — and telling those two apart at the panel
is the entire job of `SuppCause`.

**`PositionValid` is C-131's third state — cannot-tell — carried as one bit.** An inferred feedback
has a state a real contact does not: the process variable may be untrustworthy, or moving for a
legitimate reason, or driven by something other than this valve. That state **gates whether a fault
may arm** and is **never read as a position**. Reading cannot-tell as not-arrived alarms the plant
on every legitimate operation; reading it as arrived is the silent failure C-131 exists to prevent.

On a valve with no position source of any kind, drive `PositionValid` permanently `FALSE`. Both
detections are then neutralised by wiring rather than by absence, and that is the honest answer for
a valve nobody can judge — not a gap to be filled.

Both detections are **armed from a command the program itself issued**, which is what C-131 asks
for: the valve was *told* to change state, so a window opens in which its effect must appear.
That is certain knowledge, where arming from an inference would be one more thing that can be wrong.

## Behaviour, by network

1. **Valve Open / Close** — source selection and fail-safe, in one coil.
2. **HMI Times** — the two `Real` seconds settings converted once to `DInt` milliseconds (C-126's
   documented exception; pairing is by index, not adjacency).
3. **Position Feedback, Normalised To Prove Open** — the sense question answered in exactly one place.
4. **Fail To Open** — C-131, with the seal-in on the cause at its own detection network (C-508).
5. **Fail To Close** — the mirror, and the more important of the two.
6. **External Fault Latch** — `FaultFB` latched into its own cause bit.
7. **Faults** — `FaultActive`, pure OR, no seal-in of its own.
8. **Critical Class Consequence** — `FTC` where `FailCloseCritical`, and nothing else, ever.
9. **Error Class Consequence** — the exact complement.
10. **Valve In Hand** — standing indication, feeds nothing.
11. **Valve Proven Open, Enable Upstream Component** — C-115 handshake, requires positive proof.
12. **Valve Operation Counter** — explicit edge (C-404), no counter instruction (C-401), fixed debounce.
13. **Valve Status Telemetry** — overriding moves, last enabled wins.
14. **Valve Alarm Word** — **one network for the whole word** (C-501), bit map and alarm texts in
    the network comment, every bit a direct unlatched read of an already-latched cause.

## Deliberate departures from `motor-dol`, and why

These are the places a reader who knows the motor pattern will look for something and not find it.
Each was ruled, not overlooked.

| Departure | Why |
|---|---|
| **No seal-in on the command** | The motor seals `Run` in order to **bypass its prestart and recent-start permissives once running**. With prestart dropped there is nothing left to bypass, so a seal-in here would be decoration that also defeated the inhibit terms it sat outside. |
| **One command coil, not two** | The motor's `TryRunMotor` differs from `Run` only by that seal-in and the shutdown terms. With both gone the two bits would be identical every scan — the duplication C-409 exists to prevent. |
| **No prestart apparatus** | `PreStartDone`, `PreStartMemory`, `InHandReqPreStart` and the hand-selector edge machinery that serves them have no valve meaning. |
| **No controlled shutdown** | `Shutdown`, `StopMotor`, `ShutdownComplete`, `ShutdownTime`. A valve closes by de-energising. |
| **No `HandIntervention` / `RecentStart`** | Both exist because the motor's hand source is a **momentary button press**. This block's is a maintained level. |
| **`ExtFault` as its own cause bit** | The motor latches `FaultFB` in place with a reset coil, which gives that member **two writers** — the caller who raises it and the block that clears it. A separate cause bit leaves it with one, and puts the seal-in at a detection network like every other cause. |
| **No seal-in on `FaultActive`** | The motor seals it. **C-508 was corrected on 2026-08-06** to put the seal on the cause and never on the condensation; the motor pattern predates the correction. |
| **Cycle count, not hours run** | Actuators and seats wear per operation, not per hour. Same rollover-safe shape, plus a small fixed debounce so a chattering position source cannot inflate the count. |
| **`NegativeSignalEdge` spelling** | The motor pattern's own member is misspelled. The apparatus it belongs to was dropped here, so the name does not appear — but if it is ever reintroduced, spell it correctly. Do not "fix" the motor pattern in place. |
| **No `Name` member** | The motor's `Name` is a `String` inside a retained struct instantiated once per valve. On a plant's worth of valves that is a kilobyte-scale retentive cost for a label the panel already holds. |
| **One fail-to preset, not two** | `FTTime` covers both directions. Owner's ruling: two presets are not worth the divergence they invite — they start equal and drift, and the second one is the one nobody re-checks. |

*The three rows above were moved out of the IR's own comments on 2026-08-07 under C-204. The
comments now state the resulting behaviour; the argument for it lives here.*

**One further identity, recorded rather than exploited.** On every instance
`FaultActive` is identical to `CriticalActive OR ErrorActive` — `FTC` lands in exactly one of the
two per instance, and the other two causes are the lesser class everywhere. A reviewer can check it
by reading three networks. It is **not** used to eliminate a network: writing the fact a third way
would give it three homes, and the three would eventually disagree.

## Retention — the consequence that will surprise someone

**Under C-132, `RETAIN` on the interface static makes the ENTIRE interface retentive — not just the
latched fault causes.** Commands, settings, published state, the alarm word, the status code and the
suppressor address are all retained, because a single struct static cannot be half retentive.

**This is the motor pattern's own behaviour and it is not new** — `IO : "MotorIOSet" RETAIN
SETPOINT` has always done exactly this. It has simply never been written down.

It matters because **a memory budget built on "we retain a few fault bits per instance" will be
wrong**, and wrong by roughly the whole interface times the instance count. Work it out per
instance from the real member list before committing this shape across a plant's equipment classes.

Nothing is lost by the over-retention itself: the alarm word and the status code are rewritten from
their causes every scan, so a retained copy is a stale value for one scan and never a second
seal-in. The alternative — no `RETAIN` — loses a latched critical fault across a power cycle, which
is the one outcome that actually matters.

## Named gaps — what a caller must supply

These are **not defects**. They are things this block deliberately does not decide, listed so that a
caller supplies them rather than assuming they are handled.

- **Interlocks that read a sibling valve.** A block cannot see its siblings. Anything of the form
  "this valve is refused while *that* one is open" is computed by whoever owns both and arrives
  through `InhibitValve`.
- **Re-drive on resumption.** The block does not restore itself to a required state after a power
  failure or a safety stop. That is a sequencing decision, not a valve decision, and it belongs to
  whatever owns the sequence.
- **Simulation.** The block has no concept of it. If outputs must be inhibited on a simulated
  machine, the caller does that at the output mapping, and drives `PositionValid`/`PositionFB` from
  whatever the simulation provides.
- **Suppressor encoding.** `SuppCause` is moved by nobody and interpreted by nobody inside the
  block. The scheme is the application's.
- **A second position channel.** With one `PositionFB` there is one fail-to-open and one
  fail-to-close detection, and a command/feedback mismatch **is** one of those two. If an
  application must distinguish a *real contact* disagreeing from an *inference* disagreeing, that
  is a second channel and a second detection, and it is not in this block.

## Examples

- `examples/calling-network.ir` — the C-132 adjacency obligation worked out: every write to the
  instance immediately above its own `CALL`, including the `PositionFB`/`PositionValid` pair and the
  output read back out.
- `examples/iDB_Valve_Inlet.ir` / `.xml` — an example instance DB with sensible download-time start
  values. Preflights clean.

## What has actually been verified

Recorded precisely so a reader can tell "unproven" from "unchecked". All of it is desk verification.

| Check | Result |
|---|---|
| `converter preflight`, standalone (outside the job project) | `UDT_Valve` clean; `FB_Valve` clean **except** the two findings below |
| `converter review` — 15 substantive mechanized rules | Clean, **except** the two below |
| `converter to-xml` round-trip | Lossless |
| TIA import, block-level compile | **Success, 0 errors, 0 warnings** |
| TIA whole-device compile | **Success, 0 errors, 0 warnings** |
| `openness-cli sanity-check` | **HEALTHY, 0 inconsistent** |
| Member-by-member sent-vs-returned re-export diff | **55/55 interface rows match** across every section, **14/14 network titles match in order**, merged alarm network returns 4 `Coil` parts intact |
| FI-47 (no `Remanence` on a TYPE member) | 0 violations |
| C-132 shape proven, not assumed | `IO` returns `Remanence=Retain`; `Input`/`Output`/`InOut` return present-and-empty |
| **Hardware** | **NEVER RUN. See the declaration at the top.** |

**The two open findings, both against network 14, are a known checker bug and not a code defect.**
`converter review`'s C-301/C-501 check still enforces C-501's *pre-amendment* wording ("exactly one
bit per network"), while C-501 as amended requires the opposite — one network per alarm *word*. The
checker fires on exactly the shape the current rule mandates, and its own remediation text tells you
to undo it. The convention and the checker are being amended in parallel; **the block's form is
correct and stays.** The three current C-501 conditions were checked by hand: exactly one network
writes the word (four writes, all in network 14); the bit map with alarm texts is in the network
comment; every bit is a named cause, with bits 0 and 1 being `cause AND NOT suppressor`, which is
the two-term form C-504's "where the filter sits" clause *mandates*.

## Admission status

Against `docs/07-pattern-library-spec.md`'s equipment-instance criteria:

1. **Instantiated at least once in reviewed, working logic — NO.** This is the criterion that is not
   met and it is the reason for the declaration at the top. The block has never been instantiated in
   working logic anywhere, because the system it was written for has not been built yet.
2. **Round-trips losslessly — yes.** Member-by-member and network-by-network, evidenced above.
3. **Compiles when instantiated — PARTIALLY.** The FB and its UDT compile clean in a real TIA
   project. `examples/iDB_Valve_Inlet.ir` preflights clean but **has not been imported and compiled
   as a real instance**, and no real `CALL` site has been compiled.
4. **`pattern.md` complete — yes**, this document.
5. **Human sign-off — the owner instructed the promotion on 2026-08-06**, with the not-yet-proven
   declaration as an explicit condition of it.

**Admitted early, deliberately, with criterion 1 unmet.** The reason to promote now rather than
after proving is that the block is about to be instantiated many times, and a pattern that exists
before the copies is worth more than one written after them. The cost is that the risk is real and
is carried by every early user — which is why the declaration is the first thing in this file.

## Post-admission changes

*(none yet — record every change here with its date and reason, per the library's own convention)*
