# Is a total-plant conformance run feasible?

**Written 2026-08-20. Read-only analysis of one live job's IR corpus and of `src/harness/`. Nothing
was deployed, imported, compiled or downloaded to produce it.**

*This page describes blocks by KIND only. No site block, tag, equipment or alarm name appears in
it, deliberately — a few points that could not be made without one were left for the conversation
that commissioned it.*

⚠️ **A full campaign cost analysis for this job ALREADY EXISTS**, dated 2026-08-18, in the job's own
(gitignored) folder. **Read that first for the job-specific plan.** This page exists to do three
things it cannot: state the reusable, de-identified findings in the committed knowledge base;
**reconcile two analyses that reached opposite answers on concurrency**; and record that **its
single top recommendation has since been carried out**, which moves its central conclusion.

---

## The answer

**A total-plant run is feasible and is not enormous. It is ~8 sequential deployments and on the order
of 5–13 hours of running. The binding constraint is neither registers nor wall clock — it is that
the slots which reach the coverage cannot run concurrently, and one of them cannot be designed yet.**

Three constraints, in the order they bind:

1. 🔴 **CONCURRENCY IS 1, AND IT IS STRUCTURAL.** This is the finding that matters and it is
   counter-intuitive, because *at block level the plant looks parallel*. Over the 21 candidate
   blocks the conflict graph has density **0.433** and is exactly **10-chromatic** — 2.1 slots per
   wave. But **a real slot is not one block.** A slot must contain every block between the stimulus
   and the observable, and on this plant **every slot that can observe a device command contains the
   same aggregate block**, whose closure is 432 storage locations. So the eight slots that actually
   cover the plant form a **complete graph — 27 of 28 pairs are computed conflicts and the 28th is
   closed by a declared model edge — chromatic number 8, achieved concurrency 1.**
   **Coverage and concurrency are in direct opposition here, and the same block is the reason for
   both.**
2. 🔴 **ONE SLOT FAMILY CANNOT BE DESIGNED YET.** Four retained values that *steer decisions* — three
   of them `Real`, and therefore **refused by the mirror in both directions** — have no seating route.
   They gate **58 of 88 reachable assertions, two thirds of the campaign.** This is a design problem,
   not a writing problem, and *"four more slots like the last one"* would be wrong by the margin that
   matters.
3. **Per-slot artifact cost is high and barely amortises.** ~5 substantial judgement artifacts per
   slot, ~30 still to write, and **117 of ~122 vectors** unwritten.

**What is NOT the constraint:** the register budget (the 256-register partition is a choice; the
addressable ceiling is **3,596** and the current 576 lives in **two lines of one IR block**);
deployment wall clock (**~403 s** per restore + **~88 s** download, so **1.1–2.3 h** for eight);
and the conflict graph *at block level*, which is benign and misleading.

🔴 **The honest ceiling: ~25% of the reachable assertions is a few days' work. The remaining ~66% sits
behind an unsolved design problem, and no schedule should be quoted for it.** That is not six months
of authoring — but it is not a night, and the thing standing in the way is not effort.

> ⚠️ **SUPERSEDED 2026-08-20, AND THE CORRECTION IS LARGE ENOUGH TO CHANGE THE PLAN.** A design review
> of that "unsolved design problem" found the problem **as stated does not survive measurement**:
> **four of the six claims it rested on are wrong, and every one of the four errors made it look
> bigger and less tractable than it is.** In particular —
>
> - **the element-table gap unlocks NONE of the ~66%.** Every observation signal in that tier is
>   Boolean; not one observes a `Real`. Widening the table is worth doing for other reasons and buys
>   **zero** of these;
> - **the circularity is real but not load-bearing** — the carried-over value cannot reach the
>   transitions it appeared to gate, because they share an enable with its only write, one network
>   earlier in the same scan;
> - one value called un-restorable **has a reset**, and the claim traces to a *dead, unreferenced*
>   member whose comment describes a hazard the live logic does not have;
> - the real blocker is a **missing stimulus head plus repeatability**, and the program already
>   contains an unused route to the second — reachable through a harness block that is **already
>   built and staged**.
>
> Best option now: **~55 of the tier for two extra registers**, reusing that staged block, with a
> failure mode that is a visible timeout rather than a wrong answer. **"Do nothing" is no longer the
> safe default — it is beaten.** Detail is job-specific and lives outside this file.
>
> **The transferable lesson is the shape, not the numbers: a problem statement assembled from several
> artifacts inherited every one of their errors, and they pointed the same way.** Nobody re-read the
> ladder until someone was asked to design against it. *Price a blocker by measuring it, not by
> summing what has been written about it.*

---

## 1. The correction this page exists to make

The 2026-08-18 campaign analysis reasoned as follows: *the campaign is dominated by deployment count,
not test duration; at the fast end of the assumed scan band the tests take ~18 minutes while eight
deployments are unmeasured minutes each; therefore compression is not the blocker.*

**Its number-one recommendation was "measure this job's scan time — the campaign duration is uncertain
by 15× and the measurement is one deployment away."**

🔴 **That has since been done, three independent ways, and the answer landed at the SLOW end of the
assumed band:**

| measurement | value | basis |
|---|---:|---|
| the harness wire-timing constant | **24.931 ms** loaded | 12,027 scans over 299.8 s of continuous polling, from the mirror's own counter, ±0.002 ms |
| the same program, quiet | **23.80 ms** | replicated twice; poll load costs a reproducible **+1.13 ms/scan** |
| longest recorded run | **22.60 ms** | 100,554 scans over 2,272.5 s |
| a deploy-session probe | 24.29 ms | two control reads 3,473 ms apart |

The assumed band was **2–30 ms and the vectors were sized from the 2 ms end.** The measured value is
**~23–25 ms**, so:

- the **15× uncertainty is now ~10%**;
- the campaign's *"tests take 0.3 h"* row is void — the honest row is the ~23 ms one: **3.4 h floor /
  10.0 h expected**, and an independent extrapolation from the measured waves agrees (**3.7 h / 10.7 h**);
- 🔴 **and that REVERSES the compression conclusion.** Against **1.1–2.3 h** of deployment,
  **3.4–10.7 h** of test time is the dominant term, not the minor one. The argument *"compressing a
  0.3-hour campaign to save 13 minutes is not worth it"* rested on the 2 ms figure and no longer
  holds.

⚠️ **But compression is still BLOCKED rather than merely unproven**, and that is unchanged: the model
stability factor is **absent from both completed fidelity declarations by explicit decision**, and
with compression applied and that field absent the plan reads NOT DECLARED and is **not runnable**.
So the correct conclusion is now *"measuring model compression stability has moved up the list"*, not
*"compress everything."*

⚠️ **One caution on re-use of the constant:** scan period is a property of the **program**, not the
controller. It was measured with a narrow slot deployed. A wide multi-block slot adds copy-layer
rungs and will move it. **Re-measure per slot family**; do not treat ~24.9 ms as a rig fact. (A
~2.1 ms figure elsewhere in this repository belongs to a much smaller reference program and was once
quoted as a rig fact — wrong by an order of magnitude.)

---

## 2. What blocks are candidate slots

From the job's IR corpus: **102 `.ir` files, 23 blocks** (21 excluding the two organisation blocks),
0 unparseable. **No safety content of any kind is present** — no F-blocks, no F-runtime group; hard
rule 2 was not engaged. One block raises *alarms about* a safety circuit and lives entirely in the
standard program.

Facts, not verdicts. "Registers if all mirrored" applies the harness element table to the declared
interface and is an upper bound, not a prediction.

| kind | networks | iface members | regs if all mirrored | runtime instances |
|---|---:|---:|---:|---:|
| two-state device driver | 14 | 51 | 65 | 16 |
| motor-starter driver | 18 | 82 | 108 | 10 |
| signal-movement utility | 5 | 16 | 24 | 36 |
| process-equipment aggregate, class A | 59 | 159 | 219 | 4 |
| process-equipment aggregate, class B | 68 | 231 | 314 | 4 |
| cycle state machine | 16 | 26 | 37 | 4 |
| batch sequence | 93 | 302 | 414 | 4 |
| resource arbiter | 15 | 21 | 28 | 2 |
| operator-command decoder | 47 | 209 | 219 | 1 |
| parameter-set lookup | 5 | 25 | 42 | 4 |
| operator-interface publisher | 97 | 24 | 36 | 1 |
| duration/parameter converter | 17 | 57 | 114 | 1 |
| field-input mapper | 14 | 2 | 2 | 1 |
| field-output mapper | 9 | 5 | 8 | 1 |
| plant-scope alarm raiser | 8 | 1 | 1 | 1 |
| safety-circuit alarm raiser (standard program) | 2 | 1 | 1 | 1 |
| process simulation model, class A | 15 | 56 | 91 | 4 |
| process simulation model, class B | 2 | 14 | 18 | 4 |
| control call tree | 43 | 7 | 8 | 1 |
| alarm call tree | 2 | 1 | 1 | 1 |
| simulation call tree | 16 | 0 | 0 | 1 |
| cyclic / startup organisation blocks | 8 / 25 | — | — | 1 each |

**Observations rather than verdicts:**

- **The three call trees declare in their own headers that they contain no control decision.** There
  is nothing in them to assert about. One of them reaches **1,218** storage locations — *testing it
  is testing half the program.*
- **The two simulation models are behaviour** and are genuine subjects, but 🔴 **a model may never
  share a wave with a slot that USES it** — a red would be ambiguous between block and model. That
  edge is **not computable**: the coupling lives in a common caller and the closure walk goes
  downward only. It exists **only if an author declares it**, and an undeclared campaign gets a
  *confident, clean, wrong* pairing. **Every submission must carry its model declarations.**
- **The driver and utilities are CLASS-scoped** — one purpose-built instance validates the class.
  **The aggregates and sequences are not**; how far one instance's result generalises is
  **not established**.
- 🔴 **75 interface members across the two largest blocks have no row in the element table and are
  REFUSALS** — `Real` ×43, `DInt` ×19, `Word` ×8, and a few others. The table has exactly three rows:
  Bool→1 register, Int→1, Time→2. **Every continuous process quantity on this plant — weight, rate,
  target, offset — is invisible to a result package** until a conversion instrument is agreed. That
  is an open gate-owner question and it caps what any slot can observe.

---

## 3. What the register budget actually allows

### 3a. The allocation rule, from the code

`Harness.Map/MapAllocator.cs`:

```
total = 2 (version) + 2 (scan) + ceil(K/16) (start bools) + ceil(K/16) (start echo)
      + K x Vmax + K x Rmax
```

🔴 **`Vmax`/`Rmax` are the MAXIMUM over the wave set, not each slot's own width** — slots are
fixed-size by design, so **one wide slot pads every slot in the wave**: a two-slot wave costs *twice
the larger*, never the sum. This is why "53 + 164 = 217, so they fit in 256" is wrong; the real
figure is **322**.

### 3b. The ceilings, and which are hard

| ceiling | value | source | hard? |
|---|---:|---|---|
| result region **per slot** | **125 registers** | FC03 max read; a design-time refusal, explicitly "not something to split at runtime" | 🔴 **HARD** |
| start-bool commit | 123 ⇒ K ≤ 1,968 slots | FC16 max write; the commit must be one transaction | never binds |
| addressable registers | **3,596** at the current base | `(8192 − base) / 2`; 8,192 bytes of bit memory on this CPU class | soft |
| current declared area | **1024** — register 1023 reads, **1024 and 1025 refused with exception 2, measured on the controller 2026-08-23** | the area-pointer width | **a choice** |
| conformance partition | **704** | where the panel band starts | **a choice** |

✅ **THE AREA WAS WIDENED 576 → 1024 AND PROVEN ON THE CONTROLLER, 2026-08-23.** Pinned from both
sides exactly as 576 was: 1022 and 1023 answer, 1024 and 1025 are refused by the server with a Modbus
exception — a refusal, not a silence. **576 is no longer "the largest width ever proven here."**

Three things that were open before the probe and are now measured:

- **No cap.** Nothing in `MB_SERVER` or the CPU refused 1024. The step was 1.78× past anything
  previously run, and the worry was not that it would fail but that it would **silently serve fewer** —
  which is why the probe reads the edge from both directions rather than trusting the download.
- **Scan time is unchanged**: 24.65 ms/scan at 1024 against 24.72 ms/scan at 576, measured minutes
  apart on the same program. `MB_SERVER` services one request per call, so area width does not reach
  the scan — now measured rather than reasoned.
- **The panel is live at its new base**: registers 1022–1023 are band-D panel registers and they answer.

🔴 **AND THE REASON THE WIDENING WAS NEEDED IS WORTH MORE THAN THE NUMBER.** The mirror and the panel
had been sharing registers 256–323 in the deployed program — 53 tags colliding **bit-for-bit**,
including the panel's master enable and a safety-healthy substitution, both overwritten every scan by
the copy layer. The panel's own comment said it was inert until enabled; that was false on the running
controller, and safe only because the rig cannot actuate. **Nothing compared the two**: the mirror is
bounded against the declared area and proved disjoint from *itself*. `Harness.Map/ReservedRegion.cs`
now closes that, but only for neighbours somebody declares.

⚠️ **The mirror's extent is a function of lane count (~165 registers/lane), so 704 is not permanent
headroom** — it clears four lanes of padded width up to 174 and no more. **Band D ends exactly at
1023: there is zero slack above the panel.**

**The declared width lives in exactly two lines of one IR block** — the area-pointer argument and the
sidecar constant backing it — and **nothing in `src/harness/` hardcodes it**. Widening is a two-line
IR edit, a re-import, a re-compile and a re-download, plus relocating the panel band (its address
table is generated). **A mismatch between those two lines is silent, so both must be read back**, and
the width is a map-hash input so the build stamp moves.

> 🔴 **That last clause was FALSE when written, and was made true on 2026-08-22.** `declared` was not
> in `RegisterMap.MapHash`'s canonical form at all — the line carried `mem=`, `retain=` and `base=` and
> nothing else — so widening or narrowing `MB_HOLD_REG` changed what the wire could reach while **every
> build stamp stayed identical**. A client and a controller could disagree about the size of the window
> and still match, which is the one thing the version register exists to prevent. It is now hashed, and
> `DeclaredAreaCeilingTests` pins it: two maps differing only in the declared width hash differently.
> The pinned-stamp test moved with it, `0x055BE3AE → 0x58613924`, re-derived outside the assembly rather
> than copied out of the failure message.
>
> **Also corrected:** *"nothing in `src/harness/` hardcodes it"* is still true, but it was being read as
> *"nothing checks it"*, which was the real gap — `MapAllocator` compared a map only against **bit
> memory** (3,596 registers at base `%M1000`) and never against the declared 576. A batch of four lanes
> would have overflowed it and shown up as reads refused by the server partway through the band, i.e.
> as a device fault. That is now a refusal at derivation time naming both numbers.
>
> ⚠️ **And the widening is not the two-line edit this paragraph describes, on the PC side.** Both
> `harness-mirror-view` and `harness-mirror-read` issued **one unchunked `FC03(0, declared)`** and
> neither file referenced the 125-register protocol limit; at 576 that read is 4.6× the ceiling and
> comes back refused. They now page. The boundary probe stays deliberately unpaged — paging it would
> split the read whose refusal is the measurement.

### 3c. What fits

Measured bills: **6 control + 24 vector + 23 result = 53** (driver slot); **6 + 63 + 95 = 164**
(aggregate slot). The eight campaign slots come in at **~35 to ~118 result registers** — **all fit
125, most with `R = 1`.** The tightest has ~10 registers of headroom and has a named fallback (split
its one wide assertion into its own slot).

**So the register budget is NOT binding — provided the mirror is widened once.** A single-slot wave
never exceeds ~180 registers, which fits today. The 256-register partition only bites if slots are
co-run, and per §4 they cannot be.

### 3d. 🔴 But the ceiling IS what forces the split

A single slot covering all the cross-block assertions in one equipment train would have to publish
**≈ 355 registers** against a ceiling of **125**. That is a design-time refusal with no runtime
split offered. **The eight-slot decomposition is not a preference — it is what the FC03 read limit
forces**, and each slot then publishes a behaviour-scoped *selection* rather than whole interfaces.

Two escapes exist and neither is free: **pack two values per register** (the allocator explicitly
refuses to, on the reasoning that registers are free and round trips are not), or **narrow further
and add slots** — which adds deployments, the term that already dominates concurrency.

---

## 4. The conflict graph — and why two analyses disagreed

### 4a. At block level, computed fresh

`converter reachable-state` over the whole corpus. Provenance from the tool's own derivation block:
**102 files, 23 blocks, 0 unparseable, 23 of 23 computed, 0 withheld, 3,463 alias rewrites, no
warnings.**

| scoping | blocks | pairs | conflicts | density | max clique | **waves** |
|---|---:|---:|---:|---:|---:|---:|
| all 21 non-organisation blocks | 21 | 210 | 91 | 0.433 | 10 | **10** |
| minus the 3 pure call trees | 18 | 153 | 52 | 0.340 | 7 | **7** |
| minus call trees and simulation models | 16 | 120 | 48 | 0.400 | 7 | **7** |

**In all three the maximum clique equals the colouring, so the chromatic number is EXACT.** No
scheduler can beat it and nothing forces it higher. The device-driver block is disjoint from **18 of
the other 20**.

### 4b. At slot level, the answer is the opposite

A slot's closure is the **union of its blocks'**. The eight slots that cover the plant give:

```
WAVE SETS: 8    ACHIEVED CONCURRENCY: 1
```

**27 of 28 pairs are computed conflicts; the 28th is closed by a declared model edge. The graph is
COMPLETE.** Shared-location counts between slot pairs run to **432** (every slot containing the same
aggregate) and **842** (those also containing the sequence).

### 4c. 🔴 The reconciliation, which is the transferable lesson

> **A sparse block graph does not imply parallel slots. Slots are unions, and unions of a sparse
> graph's vertices are dense.** On this plant one aggregate publishes every device command the
> enumeration is about, so **every slot that can observe one contains it** — and containment makes
> every pair conflict, totally.

**This is a general property, not a quirk of one job.** Wherever the enumeration's subject is a
*leaf* behaviour and a single aggregate publishes it, coverage forces containment and containment
forbids concurrency. **The block-level graph is the optimistic reading and it should not be quoted
as a concurrency estimate.**

Two secondary findings, both quantified:

- **29% of the block-level edges (26 of 91) are instance-pooling artifacts** — an owner and a block
  placed inside it as a multi-instance always intersect by construction. Removing every one gives
  8 waves instead of 10. **Per-instance closures would buy two waves out of ten; it is not where the
  leverage is.** The pooling is deliberate and errs safe (it can only *add* an overlap), and it is
  right to keep — but on this plant it is measurably costing parallelism between two equipment
  classes that share no real state.
- **Genuine sibling disjointness IS achievable on this plant** — several block pairs compute to zero
  overlap. It is not achievable for *this* campaign, for the containment reason above.

### 4d. Consequences of `R = 1`

`SlotsPerRead = floor(125 / Rmax)`. At any `Rmax ≥ 63` that is 1. Then:

- **co-running K slots costs K times the wire traffic** of running them one at a time — parallel
  waves save the *download*, nothing else;
- the observability floor is `readsPerPollCycle x 201 ms / 24.931 ms = 8.06 scans per read`, so a
  K-slot wave has a floor of **8.06 x K scans** — **co-running degrades observability linearly.**

Since concurrency is 1 anyway, neither currently bites. **They are the reasons not to reach for
concurrency as a fix.**

---

## 5. The per-slot price

### 5a. The gated deliverable — four files

| required input | slot 1 | slot 2 |
|---|---:|---:|
| vector submission | 128.8 KB, 3 vectors | 128.0 KB, 3 vectors |
| coordinator's binding | 72.6 KB, 17 stimulus + 20 result signals | 139.5 KB, 56 + 83 |
| assertion enumeration (third party) | 107.6 KB, 96 assertions / 16 clauses | 125.2 KB, 47 / 12 |
| model-fidelity declaration (third party) | 16.0 KB, 55 represents / 22 not | 18.4 KB, 122 / 19 |

Plus per-slot **hand-authored ladder** (which must go through the LAD sub-agent): a stimulus FB of
**29 and 36 networks**, its UDT of **26 and 71 members**, a 2-network slot call FC, and a dedicated
instance of the block under test. The transport block and the cyclic-block patch are *structurally*
shared but were **re-authored per slot** with the mirror width changed.

### 5b. Mechanical vs judgement

**Genuinely mechanical:** register allocation and copy-layer generation from the binding; the
enumeration→projection transcription, which cross-checks against the enumeration's own declared
denominator and fails hard on mismatch; behaviour-string selection *by ordinal* out of the fidelity
file, anchored so a retyped string is caught; scan arithmetic from a measured constant.

**Irreducibly judgement:** every assertion; every fidelity line; every scenario, input value,
expectation, settling condition and blacklist entry; every resting-value declaration and its basis
(20 hand-written justifications on the *small* slot); the latch/transient/re-arm flags; and which
signal name means which.

**The scripts are not a library.** ~248 KB of Python across two slots; **one** file was reused across
lanes and then forked five times larger. Several exist only because a hand edit to a generated file
was destroyed by the next rebuild — twice.

🔴 **The one real amortisation is unclaimed.** **17 network titles are identical between the two
stimulus blocks** — the whole index / phase / cleardown / inert-check / re-arm skeleton, which is
contract machinery. That is **59% of the small stimulus block and 47% of the large one, re-authored
rather than shared.** *Extracting it as a template is the highest-leverage cheap item on the board.*

### 5c. Iteration cost — the real signal

| | slot 1 | slot 2 |
|---|---|---|
| gate rounds | **4** → 1 admission | **5** → **0 admissions** |
| wave attempts on hardware | **6** | **13** |
| conclusive PASSes ever | **2** | **0** |
| scratch on disk | 1,761 files / 85.8 MB | 1,320 files / 51.0 MB |

The refusals are worth reading as a class: a field the schema did not read (*"a silently-ignored
field reads as an accepted one"*); an observability map supplied by the vector author rather than the
coordinator (*"a self-declared map cannot be the deciding voice"*); a blacklist checked against an
absent disjointness graph; vectors recording no bound, so nothing could find them stale; a clause
claimed by two subjects without saying which.

**Every one is a cross-artifact consistency failure, not a content error.** That is why **the count
of gate rounds, not the size of any artifact, is the number to multiply** — and why an eight-slot
campaign should expect ~40 gate rounds, not eight.

⚠️ **Effort in hours or tokens is NOT ESTABLISHED.** Nothing records it.

### 5d. 🔴 The item that is not writing but designing

Four retained values *steer decisions* and have no seating route. Three are `Real`, hence **refused
by the mirror in both directions — neither drivable nor observable**. One steers a state machine
directly, and its enable is itself a sequence state, so seating it means driving the block into the
very state the slot is trying to test.

The one comparable value already solved needed a **five-phase injection head** with a published
"seated" evidence bit. **Four such values, in one slot family, gating 58 of 88 assertions.**

⚠️ **And the failure mode is silent:** an index that ran against a premise carried over from an
earlier index produces a *result*, not an error. Until a seating route exists **those indices are
single-shot per deployment**, which multiplies the deployment count — the term that already dominates.

---

## 6. Wall clock

### 6a. Measured

| quantity | value | provenance |
|---|---:|---|
| **restore of a 126-object program** — export-all 44 s + import-all 182 s + compile-all `--force` 217 s + sanity-check 4 s | **403 s ≈ 6 min 43 s** | a recorded no-change restore |
| download to device | **34 s and 92 s** (difference = whether Portal had to start); ~70–80 s is the device half | probe logs |
| download **to folder** (image only, no device) | 17.8 s | same |
| a 3-vector wave, 1 slot | **1,939 round trips ≈ 2 min 37 s** | the result package's own detail line |
| the widest recorded wave (one index timed out at 39,707 scans) | 7,912 round trips ≈ **15 min 35 s** | same |
| three 3-index waves on the wider slot | 4,261 / 4,397 / 4,561 round trips ≈ **8 min each** | same |
| round trip | 63 / 72 / 106 ms min/med/max; **p99 201**; worst observed 2,216 | wire constants |

⚠️ **A plain `compile-all` immediately after an identical import reported `NOTHING EXAMINED`, exit 14,
in 32 s. Quoting that 32 s as the compile cost would be a four-fold underestimate** — the honest
figure is the `--force` one, 217 s.

### 6b. The round-trip formula, corrected

**`round trips = 2P + 13L`** (P = poll rounds, L = indices), which reproduces **four recorded waves
to the digit** — 555, 890, 1,078 and 1,939. The `2` is `1 + ceil(K/R)`: one read for the control
region, one for the result band. The `13` per index is inert-establish + vector write + commit +
start-echo confirm.

**Polling is 98% of the round trips and costs no wall clock** — it runs *during* the test. Settling
adds ~17. **Per-assertion sampling costs nothing extra**: the whole result band is decoded from one
read, and four assertions on one index report the same poll count.

🔴 **`WireTiming.RoundTripsPerIndex` is WRONG against the built code — it omits the control read and
understates by ~2×.** The design spec's own worked example assumes **2 poll rounds per test** where
live waves ran **254–428**; that derivation is illustrative only. **Neither should be used to budget.**

### 6c. Extrapolation

```
T_test    ≈ sum(maxDurationScans + settling) x scan_period          scan ≈ 23–25 ms MEASURED
T_deploy  ≈ 403 s (restore) + ~88 s (download) + PC staging          per wave set
RoundTrips = (1 + ceil(K/R)) x sum(P) + 13 x indices                 [13/index verified at K=1 only]
```

| | vectors | test wall-clock | deployment (8 wave sets) | **total** |
|---|---:|---:|---:|---:|
| **FLOOR** — 1 vector per assertion | 88 | **3.4–3.7 h** | **1.1–2.3 h** | **≈ 4.8–6.0 h** |
| **EXPECTED** — universally-quantified assertions take a family | ~122 | **10.0–10.7 h** | **1.1–2.3 h** | **≈ 11.8–13.0 h** |

Two independent derivations — one from summed declared durations, one from the measured waves —
agree to within ~10%. ⚠️ The vector count's multiplier for universally-quantified assertions is an
**estimate**; everything else is measured. ⚠️ The test figure is an **upper bound**: a
conditional vector completes early on its completion signal.

**Downloads scale with WAVES, not vectors.** Re-running or changing vectors on an unchanged slot set
needs no download at all — vectors are written over Modbus.

### 6d. Time compression

**Never exercised** — both completed slots ran at compression 1 — and **blocked, not merely
unproven**: the model stability factor is absent from both fidelity declarations by explicit
decision, so above compression 1 the plan reads NOT DECLARED and is not runnable.

The floors, from the code: **500 ms absolute** per timer preset (subsuming the scan-derived
`5 x 24.931 = 124.7 ms`), so a data preset caps compression at `preset / 500 ms` and **the SHORTEST
participating preset binds** — a 2-second preset caps the whole vector at 4×. A companion bound
requires the compressed behaviour to stay **10×** above the largest participating *unscaled literal*.

🔴 **This corpus carries timer literals from 50 ms to 4 hours.** A vector touching both ends cannot be
compressed at all. The mitigation is a vector-authoring discipline — partition vectors so
short-literal and long-dwell assertions never participate together — and **it is untested**.

**Per §1, compression is now worth more than the 2026-08-18 analysis concluded** (tests are the
dominant term, not deployments) — but it is behind a measurement nobody has taken.

---

## 7. What is missing entirely

**Ordered by what a whole-plant run hits first.**

1. 🔴 **A seating route for the four decision-steering retained values** (§5d). Gates 58 of 88
   assertions. **The only item on this list whose method does not exist.**
2. 🔴 **A conversion instrument for continuous quantities.** Three element-table rows means **every
   `Real`/`DInt` is refused**; the plant's own process variables are invisible in every result
   package. Open gate-owner question.
3. 🔴 **A multi-slot wave has never been run against the controller.** It *is* wired end to end — the
   loop request takes a *list* of slots, the CLI builds it from the binding's `slots` array, and the
   arithmetic is `K`-aware — and it is **tested in simulation to a good standard**: a two-slot suite
   against a skeleton rig carries a green pair proving results indistinguishable from solo runs, a
   red pair coupled by one extra rung that is caught, and a check that the two builds differ by
   exactly that rung. **But on the real job every binding declares one slot and every result package
   records `ranAlone: true`.** Since concurrency is 1 anyway, this is **lower priority than it
   looks.**
4. 🔴 **Multi-agent contention has never been exercised with two agents contending.** The claims
   registry has been self-tested. The **serialisation points are Portal (a token, one lane at a
   time), one mirror, one program and one download** — so "parallel agents" can mean *authoring* in
   parallel, never *running* in parallel.
5. **Settling is declared but barely enforced.** Only one settling form is evaluable, bounded at 200
   polls, and **only for a slot's last index** — which is why most packages came back unsettled. The
   contract says so itself: the runner *"observes completion, not settling"*.
6. 🔴 **Inert quiescence is hardcoded to 1 scan** and **nothing in the submission or binding format
   can set it.** Every wave ever run used 1.
7. **No wave carries a start/end timestamp and no expected-duration figure is produced**, so a wave
   taking four times longer than it should completes inside budget and nobody notices. Every duration
   in §6a is a file-mtime inference.
8. **The live viewer cannot show a real slot.** Its direct poll issues a **single FC03 from register
   0** covering the whole declared area, so it works up to **125 registers** — the protocol limit, not
   a viewer choice. It has run live against a 37-register map; a 164-register slot cannot be watched
   live except through the follow-a-feed path. Multi-frame direct polling is the fix and does not
   exist.
9. **Cross-slot coverage rollup has no producer.** Merged figures are deliberately refused as
   *"arithmetically true and professionally meaningless"* — right at the artifact level, but it means
   **"what fraction of the plant is conformance-tested" cannot currently be answered.**
10. **Subject-qualified clause citation.** Clauses claimed by two subjects must say which. This
    already refuses one built slot, and at plant scale shared clauses are **the common case.**
11. **A slot template** (§5b) — 17 identical networks re-authored per slot.
12. **Per-instance conformance has no producer** — closures are per *block* and pool instances on
    purpose, so two instances of one class can never be admitted to the same wave.
13. **No slot teardown/restore path** other than another full deploy.

### 7a. A documentation hazard worth fixing cheaply

🔴 **One measured constant has five live copies, and the wrong ones sit in the two documents most
likely to be read** — the "read this first" readiness page, and the spec section that the code
explicitly defers to as its authority. A dependent conclusion (a compression ceiling ~47×) was drawn
from a wrong copy and written down as fact. The already-recorded fix applies: **one home in code,
every other mention cites rather than restates.**

---

## 8. Coverage — the number that decides whether any of this is worth it

| | measured |
|---|---|
| enumerated denominator | **96 assertions over 16 clauses** (43 class-scope, 53 sequence-scope) |
| **permanently unreachable at ANY slot scope** | **8** (one alarm with no producer at all ×6, one needing a controller restart, one quantified over non-PLC routes) |
| **reachable ceiling** | **88 of 96 = 91.7%** |
| covered today | **2 (2.1%)** |
| reachable in a few days, all prerequisites existing | **~22 of 88 = 25%** |
| behind the unsolved design problem | **58 of 88 = 66%** |

The class-scope assertions the first slot could not reach, by stated cause: **9** need a physical
output the harness instance does not own; **12** live on the aggregate's half of the interface;
**15** bind their observable on-slot but their *trigger* off-slot; **1** has no stimulus; **2** need
more than one instance on the slot. **That cause-group of 15, plus the 53 sequence-scope assertions,
is the containment problem of §4c in numbers: 68 of 96.**

⚠️ **A further seven assertions are blocked on a signature, not on work** — four unspecified
register values, one feedback-delay figure, one ruling on an orphaned alarm, one partial-coverage
ruling. **Minutes of owner time.**

---

## 9. What would change the answer

The 2026-08-18 plan's recommended build order still stands. What this analysis changes:

1. ✅ **Its item 0 — measure the scan time — IS DONE** (§1). Re-derive §3 of that plan against
   **~23–25 ms**, not the 2 ms end of the band, and **re-read its compression conclusion**, which
   rested on the void row.
2. 🔴 **Its critical path is confirmed and unchanged: design the seating route** for the four
   decision-steering retained values. 58 of 88 assertions. **Nothing else on the board is designing
   rather than writing.**
3. **Do NOT reach for concurrency as a fix.** The block-level graph looks parallel and is misleading
   (§4c); the slot graph is complete; and `R = 1` means co-running costs full wire traffic and
   degrades observability linearly. **Concurrency is not available and would not help.**
4. **Widen the mirror once**, to ~1,024 registers, and relocate the panel band. Two IR lines, both
   read back, plus a regenerated address table. It removes the register question permanently.
5. **Extract the 17-network slot skeleton into a template** — the only amortisation two slots have
   demonstrated is real.
6. **Settle the five signatures/rulings** — minutes for 7 assertions.
7. **Then measure model compression stability**, which §1 promotes from "not worth it" to "the next
   real lever on a 10-hour test campaign".

---

## Provenance, and what is not established

**Computed fresh for this page:** the whole block-level conflict analysis in §4a (tool provenance
block quoted), the interface/register inventory in §2, and the allocator arithmetic in §3.
**Read from code:** every ceiling, formula and constant in §3, §4d and §6. **Read from job
artifacts:** §5, §6a, §8, and the slot-level graph in §4b.

⚠️ **The harness sources quoted carry ~2,178 lines of uncommitted working-tree change**, including
the files holding the wire-timing, compression and map constants. Everything is read from the
**working tree**.

**Not established:**

- Whether one instance's result generalises to other instances of a per-instance-configured block.
- Effort per slot in hours or tokens. Nothing records it.
- Any plant-wide coverage figure — it has no producer (§7.9).
- The multiplier for universally-quantified assertions in the vector count (§6c).
- Wall clock for a wide multi-block slot: the scan period will move and has not been re-measured.
- Whether a multi-slot wave runs on hardware. Never attempted.
- **How long the unsolved design problem in §5d takes. No number should be offered for it.**
