# HARNESS BINDING — the Hx corpus slots

> ## 🔴 DO NOT GIVE THIS FILE, OR `harness-binding.json`, TO A VECTOR AUTHOR
> Both quote the implementation's interface names throughout. A vector author's value rests entirely
> on not having read the implementation, and these are contamination channels for exactly that.
> Brief vector authors with an **allowlist** of inputs; neither of these is on it.

**The machine-readable artifact is `harness-binding.json`** — that is the generator's input. This file
is its prose half: what the binding claims, what it deliberately does not claim, and the four things
in it that somebody has to decide before a wave runs.

**Ownership, stated because it is irregular.** Binding documents are the coordinator's
(`gen/test-project001/hopper-blockage-alarm/harness-binding.md` says so, and the reason is that only
the coordinator holds both the spec and the IR). This one was written by the **block author**, on the
coordinator's instruction, because for this corpus the two halves are the same person: I wrote the
blocks and I wrote the register they are specified in. **That is itself a finding — see §3.**

Written 2026-08-14 against contract §2.7 / §2.8 and `Harness.Gate/BindingDocument.cs`.

---

## 1. What it binds

Four slots, `HXE` / `HXD` / `HXS` / `HXL`, one per corpus block, **11 commanded signals and 9
observed, 20 in total** — counted from the JSON by parsing it, not from memory. *(An earlier
telemetry line and an earlier heading in this file both said 22. That figure was a slip, carried from
a grep that also counted the four IEC timer sub-members and the timer instance itself; it is
corrected here and in the log rather than left as a number quoted as measured.)*
Every path is a member of the block's own instance DB — that is the isolation the corpus exists to
provide, and it is why these four slots can share a wave.

Register widths are **not** restated here. `Time` occupies two registers and `SlotBinding`'s running
sum is the only correct source for every offset in this system; a second copy in prose is a second
number able to disagree with it.

## 2. 🔴 THE BINDING IS ADDITIVE. GENERATING FROM THIS FILE ALONE UNWIRES THE HOPPER BLOCK

`CopyLayerGenerator.Generate` emits **the whole copy layer** from the binding list it is given. So
these four slots must be **concatenated with the existing `HBA` slot's binding**, never substituted
for it. Generating `FC_HarnessCopyLayer` from this file alone produces a copy layer with no `HBA`
slot: the hopper stimulus model stops being commanded and its results stop being published, with no
error anywhere, because a copy layer that mirrors four slots correctly is a perfectly valid copy
layer.

The `HBA` slot's binding is **deliberately not reproduced here.** It would have to be reconstructed by
reading the generated `FC_HarnessCopyLayer.ir` — and *a fixture authored by reading our own output is
not a fixture, it is a mirror.* Whoever holds the `HBA` binding supplies it.

## 3. `specName` — every pair coincides, and that is the corpus's one blind spot

Contract §2.8 requires `specName` beside `tag`, and is explicit that **absent does not mean "same as
tag"** — that identity is the assumption being removed. So every signal states its `specName`
explicitly, and in this corpus **every one of them is the same string as its `tag`.**

That is honest, and it is also the thing to know about this corpus:

> *** THE Hx CORPUS CANNOT EXERCISE §2.8's TRANSLATION AT ALL. *** §2.8 was written because *one
> signal in seventeen resolved, and it resolved because its two names happen to be the same string.*
> **Every** signal here is that case. A campaign that validates the `specName` path against this
> corpus validates it against the one shape that cannot fail.

The cause is structural, not an oversight: I am both the spec author and the block author, so the
spec's vocabulary *is* the block's vocabulary. **Recommendation, not a decision I made:** if the
campaign wants §2.8 exercised, one block should be given a deliberately divergent spec name — a
rename inside `gen/test-project001/hx-corpus/requirements.md` costs nothing **today** and will cost
every citation into the enumeration once one exists. `FB_HxIntStep` is the cheapest place (three
clauses, no timing). **I did not do it: renaming signals in a requirements register is a change to
the specification, and that is an escalation rather than an implementation choice.**

## 4. The instrumentation mode — `Generated` for all 20 signals, stated rather than left unstated

**No signal in this binding carries `latchedBy`.** That is a positive claim, not an omission: no
deployed block instruments any Hx signal, so the mode is **read off the shape the copy layer emits**
— contract §2.8's `modeSource: Generated`, the row that is *"CHECKED — a computation, not a claim."*

🔴 **And the trap this corpus sets, which is worth stating loudly because it looks like the opposite:**

> `SealHeld` and `SealPriority` **are latches**, and an expectation on either must nevertheless be
> declared `Sampled`, never `Latched`. The mode describes **the instrument**, and the copy layer emits
> a plain coil for both. A block latching its own output is a **value under test**, not
> instrumentation. §2.8's thirteen *correct* gate-5 refusals are precisely this mistake, and this
> corpus is unusually good at inviting it.

The window is generous for all of them: every Hx output persists until its commanded inputs change,
so nothing here sits near the observability floor. **The floor itself is not quoted in this file** —
read it from §12a derivation 1 at the `comp` actually used.

## 5. `startCondition: null` on all four slots — what it claims, and what it costs

`null` is a **claim**, not a blank: *this block has no start gate* (D37). It is true — the Hx blocks
run every scan and respond to whatever the mirror last commanded; none has a `Start` member and none
gates on one. `CopyLayerPlan` records slots in this state as a named list, so it is declared and
visible rather than silent.

**What it costs, precisely:** `CopyLayerGenerator` emits a start bool **and** a start echo only when
`StartCondition is not null` (both are guarded by the same test). So these four slots get
***neither `HX_<slot>_Start` nor `HX_<slot>_Ran`***. Consequences:

- **No per-slot T=0.** Timing for `HXD` is measured from when the client commanded `DwellCommand`,
  not from an observed rising edge on the device.
- **No per-slot start echo**, which is the X-E evidence that *this slot's* code actually ran. Liveness
  for these four rests on the mirror's own `HX_ScanCount` advancing and on the results changing —
  which is weaker, and *"every register agrees" is also what a mirror no write ever reached looks
  like.*
- **Contract gate 7 requires exactly one start bool per slot.** Whether a `startCondition: null` slot
  satisfies gate 7 by way of D37, or is refused by it, ***is not something I can settle from the
  contract text*** — §6 and D37 point opposite ways and I would be guessing.

> **ESCALATION, with the options, because this is a design decision that changes the spec.** Giving
> each block a `Start` member is ~8 small edits, but a start member **no network reads** produces an
> echo that proves the copy layer ran rather than that the block ran — an echo that proves the wrong
> thing, which is worse than none. Making it genuinely gate each block adds a condition to every
> clause in the register and stops the blocks being trivial, which was the brief. **The third option
> is to rule that a null-start slot is admissible and say so in the contract.** I recommend the third
> and have implemented none of them.

## 6. Two gaps I did not fill, rather than filling them with a guess

- **`retentiveBytes` is absent.** It is the retentive `%M` extent that the 0.1b non-retentive
  assertion is checked against, and it is a property of the **hardware configuration**, which I have
  not read. A plausible number here would silently void that assertion — the same shape as a defaulted
  build stamp. Whoever knows the rig's retentive window supplies it.
- **Contract §2.8 and `BindingDocument.cs` do not agree on the shape**, and the JSON is written to the
  **code**, since the code is what will read it. §2.8 specifies `modes`, `modeSource` ∈
  `{Generated, HandAuthored, SelfDeclared, Unstated}` and `instrumentedBy`; the class has `SpecName`
  and a single `LatchedBy` naming a block. They are reconcilable — `latchedBy` absent ⇒ `Generated`,
  `latchedBy: X` ⇒ `HandAuthored` + `instrumentedBy: X` — and the code's shape is arguably the better
  one, because it makes `SelfDeclared` **inexpressible** rather than merely NOT CHECKED. But a
  submission written to §2.8's field names will not deserialize, and **that is a live divergence for
  the lane building the code side.**

## 7. How to check the bindings actually landed

`converter undriven-scan --project ir/test-project001 --fb <FB>` for each of the four blocks. Today
every commanded input reports `UNDRIVEN`, which is the true state — nothing writes them. When the copy
layer is regenerated with these four slots, every one of them becomes `DRIVEN`. **An input still
reporting `UNDRIVEN` after generation is a slot that did not land**, and it is the cheapest check
available.

**The copy-layer IR is not generated in this commit, and could not be.** The only call site of
`CopyLayerGenerator.Generate` is `Harness.Loop/LoopRun.cs`, which deploys — there is no standalone
"emit the copy layer" command — and building `src/harness` while another lane has uncommitted changes
in it is the failure the working agreement names by example (*a lane's driver picked up another
lane's in-progress DLLs and computed its inputs one register off*). Generation is the harness lane's
step; this file is its input.
