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

## 3. `specName` — 14 pairs coincide, 6 diverge, and the divergence is deliberate

Contract §2.8 requires `specName` beside `tag`, and is explicit that **absent does not mean "same as
tag"** — that identity is the assumption being removed. Every signal states its `specName`
explicitly. Of the 20:

| | |
|---|---|
| **14 coincide** (`HXE`, `HXD`, `HXL`) | I wrote both the spec and the blocks, so the two vocabularies are one |
| 🔴 **6 diverge** (`HXS`, every signal) | `AddendA`/`AddendB`/`Limit`/`ClearRequest`/`Total`/`LimitExceeded` against `StepInput`/`StepIncrement`/`StepThreshold`/`StepReset`/`StepSum`/`StepOverThreshold` |

**The divergence was introduced on purpose, and it is what makes this corpus able to test §2.8 at
all.** §2.8 was written because *one signal in seventeen resolved, and it resolved because its two
names happen to be the same string.* Before this change **every** signal here was that case, so a
green from the translation path would have meant nothing. Owner-ruled 2026-08-14: this is artificial
material and the specification is ours to set.

Note what was changed and what was not: ***the divergence lives in the REQUIREMENTS REGISTER, and no
block member was renamed — no IR changed and no re-import was needed.*** Renaming the block's members
would have moved both names together and left them equal, which is the one edit that cannot create a
divergence.

**So the corpus now exercises both paths**: three slots where the join is an identity and one where
every signal needs translating. A gate that resolves `HXE` but not `HXS` has found the real defect.

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
✅ **RULED ADMISSIBLE, 2026-08-14.** A `startCondition: null` slot is admissible: these blocks have no
start gate, and *inventing one to satisfy a schema would be fabricating a stimulus.* **No workaround
was implemented and none should be** — in particular, giving each block a `Start` member that no
network reads would produce an echo proving the *copy layer* ran rather than the *block*, which is an
echo that proves the wrong thing and is worse than none.

**Still to do, and it is not mine:** the ruling needs writing into the contract, because gate 7's own
text ("exactly one start bool per slot") and D37 read opposite ways, and a ruling that lives only in
a lane message is one the next reader cannot find. *A caveat in a report decays.*

## 6. Two gaps I did not fill, rather than filling them with a guess

- **`retentiveBytes` is absent.** It is the retentive `%M` extent that the 0.1b non-retentive
  assertion is checked against, and it is a property of the **hardware configuration**, which I have
  not read. A plausible number here would silently void that assertion — the same shape as a defaulted
  build stamp. Whoever knows the rig's retentive window supplies it.
- ~~Contract §2.8 and `BindingDocument.cs` disagree on shape.~~ ***RESOLVED 2026-08-14 (`e663fb0`):
  §2.8 has narrowed to `specName` and `latchedBy` and nothing else — `modes`, `modeSource` and
  `instrumentedBy` are gone.*** This JSON was written to the code and is checked against the narrowed
  text rather than assumed to match it: `tag`, `specName`, `type`, and `latchedBy` absent. **No
  change was needed.** The narrowed contract also states independently the trap in §4 above —
  `latchedBy` names an **instrument**, so naming the block under test would claim the thing being
  tested is the reason the test can see it.

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
