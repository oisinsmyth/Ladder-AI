# ADR-0010 — No IR that the AI cannot change

- **Status:** **ACCEPTED — ruled by the owner 2026-08-12.** Promoted to an ADR on 2026-08-13 from
  `docs/notes/test-environment-build-plan.md`, where it had been recorded with its own note saying it
  *"likely deserves promotion out of this build plan — into an ADR or a CLAUDE.md line — since it
  governs converter scope permanently and well beyond the harness."* This ADR is that promotion. The
  ruling is **unchanged**; nothing here is new decision-making.
- **Date:** ruled 2026-08-12; recorded 2026-08-13.
- **Relates to:** **ADR-0001** (IR format) · **ADR-0005** (derive-always sidecar — the mechanism that
  makes the readable half authoritative) · CLAUDE.md **hard rule 7** (edit only IR, never raw
  SimaticML — the rule that makes this one load-bearing) · `ir/SPEC.md` · `src/converter/`
  (`UnsupportedConstructException`, `FlgNetParser.SupportedPartNames`,
  `SimaticMl/FixedShapeInstructions.cs`) · `tools/confirm-roundtrip.ps1` + `converter compare`
  (the gate this decision hands capability widening to) · **ADR-0011** (arming that gate).

## Decision

> ***A construct the converter cannot read is a block the AI can never modify. That is not a tooling
> inconvenience — it is a hole in the product's core promise, and it is therefore a scope item, not
> an acceptable resting place.***

Two consequences, both binding:

1. **Anything that appears in deliverable logic must be expressible in IR that an AI can read *and*
   write.** Not merely convertible — *changeable*. A construct that round-trips only because its
   details are held somewhere the AI does not author fails this test.
2. **Everything the AI must be able to change lives in the READABLE IR**, not in the machine-owned
   sidecar. The sidecar is not a hiding place for design surface.

## Context

### Why this is not a tooling preference

The project's stated deliverable is **"an AI capable of programming ladder logic"** (CLAUDE.md,
first line). A permanent no-go region in the corpus contradicts that directly: a block containing one
unreadable construct is a block the product cannot touch, forever, no matter how small the requested
change.

**Hard rule 7 is what makes the hole permanent rather than awkward.** The AI edits IR and never raw
SimaticML. So if a construct cannot be expressed in IR, there is no second route — the block is not
"hard to change", it is *unchangeable by this system*. The two rules only work as a pair: hard rule 7
is safe **because** this ADR obliges the IR to keep up.

### What forced the ruling — three findings of the same shape, 2026-08-12

- **Unconnected instruction ports.** A deliberately-unwired port on a Modbus-family instruction had no
  representation in readable IR. The sidecar-only treatment would have round-tripped it perfectly —
  and **an AI reading the IR could not see the port existed, let alone wire it.** The fix is
  `REQ := OPEN` / `DONE => OPEN` in the readable IR, distinct from a port absent from `<Wires>`
  entirely (which produces no argument at all). `OPEN` is a reserved bare word, precedent `TRUE`/`ENO`.
  ***This ADR is the reason that is IR text rather than a sidecar entry.***
- **The silent `Version` loss.** The bare-instance-member writer emitted an `MB_SERVER` multi-instance
  member with **no `Version` and no `<AttributeList>`** — converting without error, importing a
  versionless instance, warning nobody. Recorded in the build plan under this ruling as *"the same
  class as the ANY-pointer defect: a block that converts and cannot be safely written back."*
- **`UnsupportedConstructException` generally.** Previously filed as *"a correct hard error"* and left
  there. The behaviour is still correct — failing loud beats failing silent — but the *resting place*
  is not.

### The instructive error inside the ruling, kept because it shapes how to apply it

The consequence first drawn from this ruling was wrong: it read *"`SupportedCallParameterSections`
gains `InOut` … `MB_SERVER` calls will appear in real blocks."* **`MB_SERVER` is a `<Part>`, not a
`<Call>` — measured** — so that whitelist has nothing to do with it. The ruling stood; only its target
was misidentified. **Apply this ADR to a measured construct, never to an assumed one** — the whole
point is fidelity, and a scope item chosen from an assumption widens the converter in the wrong place.

## Options considered

**A. Leave it as "a correct hard error" (the status quo before the ruling).** Honest, loud, cheap.
Rejected: correct *behaviour* is not the same as an acceptable *end state*, and the accumulated set of
hard errors is exactly the set of blocks the product can never work on. The error tells you the hole
is there; it does not stop the hole growing.

**B. Support the construct in the sidecar only, so it round-trips.** Cheapest way to make the checks
green — and the trap. Round-trip fidelity and AI-changeability are **different properties**, and the
unconnected-port case is the worked example: perfect fidelity, zero changeability. Rejected because it
converts a visible gap into an invisible one, which is this project's most expensive failure mode.

**C. Hand-patch the SimaticML for the awkward cases.** Rejected outright — CLAUDE.md hard rule 7.

**D. Widen the converter as the constructs are met, gated on a proof that the widening is faithful.**
**Chosen.** The obligation is real but bounded (see scope, below) and it has a gate that is not a
judgement call.

## Consequences

### What it makes easier

- **`UnsupportedConstructException` becomes a work item with an owner**, rather than a permanent
  answer. The converter's supported set is now something the design is committed to growing toward
  what real exports contain.
- **Design surface stops leaking into the sidecar.** "Can the AI change it?" is a question with a
  mechanical answer: is it in the readable IR?

### What it makes harder, and the cost accepted

- **Converter scope grows with the corpus**, not with our convenience. Real exports decide what must
  be supported.
- **Every widening needs proof**, which is minutes of Portal time per block rather than a unit test.

### The gate, and it is not a judgement call

***Widening any capability without proving the round trip would replace a loud correct error with
silent infidelity — the same failure class as the `MemoryLayout` hole.*** So a capability widening is
gated on the **confirm loop** — `tools/confirm-roundtrip.ps1` (export → to-ir → to-xml → import →
compile → export) plus `converter compare` on the first and last exports. It is strictly stronger than
`drift-check`, because it has been *through TIA*. See **ADR-0011**, which arms it and fences it.

**The gate must be able to see what it is gating.** A comparator blind to an attribute passes a
widening that loses it — which is how `MemoryLayout` survived. A Normalizer/comparator fix therefore
lands *before* the loop is relied on for a given class of change, not after.

### Scope — what this ADR does NOT say

- **Not "support every TIA construct".** The obligation attaches to what appears in **deliverable
  logic**. A construct nobody's program contains is not a hole in the promise.
- **Not "stop erroring".** The hard error stays. It is the correct behaviour on meeting the gap; what
  changed is that the gap is now tracked as scope.
- **Not a licence to guess a port list.** The `(name, version)` instruction registry refusing an
  unknown version is this ADR working correctly, not against it: an unexercised template is a
  *silent* infidelity, which is precisely what this ADR ranks as worse than a loud refusal.
  (`MB_SERVER` 5.3 is characterised and deliberately **not** registered for exactly that reason.)
- **Safety is untouched.** Hard rule 2 outranks this ADR without exception: F-blocks and the safety
  program are not read, written, converted or explained, and "the AI cannot change it" is the
  *intended* state there.

### Revisit triggers

- **If the confirm loop cannot be armed** (ADR-0011) the gate does not exist, and widening decisions
  fall back to judgement — which this ADR explicitly declines to rely on. That is a reason to fix the
  loop, not to relax the rule.
- **If "deliverable logic" ever needs a construct whose IR form would be unreadable to a human**, the
  readable-IR obligation and the readability conventions (`docs/06-lad-conventions.md`) come into
  tension, and that is an owner question rather than a converter one.
