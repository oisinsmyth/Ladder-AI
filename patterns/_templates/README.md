# Pattern templates

Starting points for a new `patterns/<name>/` entry. Copy the relevant `pattern.md` template's
*content* into your new pattern's own `pattern.md` and fill in every `[...]` placeholder — don't
copy this folder itself (its own name, `equipment-instance`/`rung-shape`, isn't a real pattern
name).

There are two kinds so far, per `docs/07-pattern-library-spec.md` — **not a closed taxonomy**; if
a new pattern genuinely doesn't fit either, define its own shape rather than forcing it into one of
these (see the spec's own note on this — e.g. constructed edge-detection is a real third kind,
cross-block micro-idiom, not built yet).

## Kind 1: equipment-instance (`equipment-instance/pattern.md`)

A whole, proven, callable FB/FC — reused via ordinary `CALL`. Folder holds:
```
patterns/<name>/
├── pattern.md     # from this template
├── <BlockName>.ir # the block itself, ordinary IR, real sanitized names, no template syntax
├── <BlockName>.xml
└── examples/      # ≥1 real CALL site (instance DB + calling network), sanitized
```
Real example: `patterns/motor-dol/`.

## Kind 2: repeated rung-shape (`rung-shape/pattern.md`)

The same rung shape repeated many times within one sequencing/mapping FC. Folder holds:
```
patterns/<name>/
├── pattern.md   # from this template
└── examples/    # 1+ real network excerpts, sanitized, one .ir file per variation worth keeping
                 # (not just the cleanest case — include what varies, not only the ideal instance)
```
Real example: `patterns/chained-permissive-enable/`.

## Before writing anything

1. Re-ground the source block/FC fresh from a live export — don't trust an old summary or an
   earlier session's read of the same content, even your own.
2. Sanitize via `converter sanitize` before anything gets committed — real production tag/DB/equipment
   names never land in `patterns/`. Reuse an existing `sanitization/*.map.json` if one already
   covers this source content.
3. Verify round-trip losslessness against the actual committed file (see
   `Converter.Tests/PatternExampleTests.cs` for the pattern — extend it with new `[InlineData]`/
   `[Fact]` entries for the new pattern rather than starting a separate mechanism).
4. Leave "Admission status" honest and unfilled until each criterion is actually true — a template
   filled with optimistic guesses is worse than an incomplete one flagged as such.
