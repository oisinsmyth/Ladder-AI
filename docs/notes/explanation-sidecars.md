# Explanation sidecars (FI-17 pilot)

Cache one AI block explanation per IR *version*, keyed to a readable-IR hash, reused across sessions
for **orientation only** — "which block do I open / what does this roughly do" — and thrown away the
moment the block's logic changes. Origin and rationale: `docs/16-future-ideas.md` FI-17. This note is
the storage convention plus the use-boundary; the `converter ir-hash` subcommand is the key/validate
helper it depends on.

This is a **pilot by convention, not a pipeline.** Per the FI-17 verdict, cache the explanations you
already produce during normal work (the `explain-plc-block` skill, S2) before building any heavier
tooling. The `ir-hash` helper exists so those cached explanations can be keyed and invalidated
correctly; nothing here mandates generating sidecars ahead of need.

## Storage

An explanation sidecar lives next to the block's `.ir` file:

```
ir/<project>/FB_ShredderSequencer.ir
ir/<project>/FB_ShredderSequencer.explain.md   <- the sidecar
```

Filename: `<Block>.explain.md` (the block file's base name + `.explain.md`). It is a plain Markdown
explanation for a controls engineer, with one required header field:

```markdown
---
derived-from: 108729C97FC94CDBF0CEC5AF8902888104E4966A598A9AA585E09E2F28D3B6AD
---

<the plain-language explanation>
```

`derived-from` is the exact `converter ir-hash <block>.ir` output for the block at the moment the
explanation was written. It is the version stamp the sidecar is bound to.

### Why this hash is the right key

`converter ir-hash` hashes `IrSerializer.SerializeBlockReadable(block)` — the readable-only form, no
SIDECAR section. So the hash:

- **invalidates on any change to meaning** — logic, interface, titles, or comments;
- is **immune to SIDECAR / UId churn** — re-deriving a sidecar (ADR-0005) or TIA reassigning
  Wire/Access/Part UIds does not change it, so an explanation isn't needlessly discarded when only
  round-trip bookkeeping moved.

Do **not** key on `converter digest --fingerprint` — that abstracts tag names away, so a tag rename
(a real change in what the block does) would not invalidate the cache. Wrong key for FI-17.

## Use-boundary — the load-bearing rules

1. **Orientation only, never authoritative.** A sidecar answers "which block do I open / what does it
   roughly do." It is a *claim about* the block, not ground truth. Any decision that turns on what the
   block actually does reads the actual IR.

2. **NEVER a review input.** Reviewers (`review-conventions` / `review-functional` /
   `review-simplicity`) and any gate read the full IR — never the sidecar. This is the S2
   exhaustiveness lesson: an explanation describes the template, but findings live in the one instance
   that quietly breaks it, which a summary hides. Feeding a cached explanation into a review would
   launder a claim in as if it were the block.

3. **Hash-on-read invalidation — never trust a stored copy.** Every consumer, before using a sidecar,
   recomputes `converter ir-hash` for the current block and compares it to the sidecar's
   `derived-from`. On mismatch the sidecar is **stale**: discard it (and regenerate if still wanted),
   never read its body. Never assume a stored sidecar is current because it exists.

This mirrors ADR-0005's derive-always / hash-on-read discipline: a derivable artifact is re-checked
against its source at read time rather than trusted because it was written down. The `TimerSample`
stale-sidecar bug is the cautionary precedent — a stale cached artifact poisons every consumer that
trusts it instead of re-checking. Cached *AI judgment* is worse than a stale mechanical sidecar,
because its errors look plausible rather than mechanically detectable (FI-17 "Costs / risks"), which
is exactly why rules 1 and 2 hold even when the hash matches.

## Consumer recipe

```
# before using ir/<project>/<Block>.explain.md:
converter ir-hash ir/<project>/<Block>.ir
# compare the hash to the sidecar's `derived-from`:
#   match    -> the explanation is current; use it for ORIENTATION only (rules 1-2 still apply)
#   mismatch -> stale; discard, do not read the body, regenerate if wanted
```

## Cross-references

- `docs/16-future-ideas.md` FI-17 — origin, merits, costs/risks, pilot verdict.
- `docs/adr/adr-0005-derive-always-sidecar.md` — derive-always / hash-on-read discipline this mirrors.
- `.claude/skills/explain-plc-block/SKILL.md` — the S2 skill that produces the explanation body
  (Output section notes the optional caching path).
