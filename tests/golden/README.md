# Golden round-trip corpus

The enforcement mechanism for losslessness (`docs/08-testing-strategy.md`, Layer 1). C#
(`GoldenHarness.sln`/`GoldenHarness.Tests/`), matching the converter/`openness-cli`
toolchain (ADR-0002).

Per block: `SimaticML → IR → SimaticML' → import → compile → re-export → SimaticML''`,
asserting IR self-stability, `SimaticML'' ≡ SimaticML` after normalization, and clean compile
at every import. `RoundTripRunner.cs` orchestrates each stage by shelling out to the built
`openness-cli.exe`/`converter.exe`; `Normalizer.cs` documents and implements the normalization
rules. Narrower than originally feared for most UIds — the IR sidecar (ADR-0001) preserves exact
source `Part`/`Access` UIds through our own `to-ir`/`to-xml` regeneration — but **not** for
`Wire` UId specifically: confirmed real, 2026-07-10, that TIA reassigns every wire's own UId on
its own `Import()`/compile cycle regardless of input, so that one is genuinely volatile (a wire's
identity is its endpoint set, not its own UId) and normalized accordingly, alongside timestamps
like `ModifiedDate`/`CompileDate`.

## The corpus

Seeded 2026-07-10 with one FC and three DBs, extended 2026-07-11 with a second FC
(`PerimeterSafetyAlarms`, S1 item 7 Phase A — OR-merge and negated contacts):
`ir/reference/{NodeStatusAlarms,CommsProcessData,AlarmWords,EquipmentStatus,
PerimeterSafetyAlarms}.ir` / matching `simatic-ml/reference/*.xml`. Structural shapes were
derived from sanitized real production PLC data under a private approval — no site or site
specifics are recorded anywhere in this repo, and the sanitization mapping (real name -> invented
name) is intentionally never committed (`.gitignore`: `sanitization/`). Every tag path, block/DB
name, member name, and comment in the committed files is invented; only structure (wiring
topology, instruction types, slice/array addressing, member types/retention) reflects something
real — DBs are brought in *complete* (all members), not trimmed to only what the paired FC
references. See `docs/13-data-boundary.md`.

The full live round-trip (`export -> to-ir -> to-xml -> [sanitize ->] import -> compile ->
re-export`, `Layer 1` assertion 2 via `Normalizer.AreSemanticallyEquivalent`) has been run and
passed against a real TIA project for all five artifacts, DBs compiled before the FCs that depend
on them (`tests/golden/GoldenHarness.Tests/ReferenceProjectRoundTrip.cs` documents how to re-run
it — needs a live Portal session, not wired into an always-running `[Fact]`, same reasoning as
`RoundTripRunner.RunFull` itself).

Real gaps found and fixed along the way (not glossed over), FC pass:

- **`Wire` UId is reassigned by TIA on every import/compile cycle** — not preserved the way
  Part/Access UId is (the original assumption, now disproven by real data). A wire's identity is
  its endpoint set, not its own UId. `Wire` order within `Wires` isn't meaningful either (TIA
  relocates the shared rail wire). Both handled in `Normalizer.cs`.
- **`BlockSourceParser` now hard-errors** rather than silently drops real content it doesn't
  model yet: a non-empty `Title` (distinct from `Comment`), or a non-boilerplate `Interface`
  (real FC/FB parameters) — design philosophy #10, applied retroactively once real data exposed
  the gap.
- **`DocumentInfo` and several block-configuration elements** (`AutoNumber`, `HeaderAuthor`/
  `HeaderFamily`/`HeaderName`, `IsIECCheckEnabled`, `MemoryLayout`, `SetENOAutomatically`,
  `UDABlockProperties`, `UDAEnableTagReadback`) are TIA-assigned scaffolding/defaults on
  `Import()`, not written by `BlockSourceWriter` and not modeled by this converter slice —
  confirmed benign, added to `Normalizer`'s strip list.

DB pass (`src/converter/README.md` has the full detail):

- **`Member`/`AttributeList`/`StartValue` inherit the `Interface` XML namespace** from the
  ancestor `<Sections xmlns="...">` rather than redeclaring it — looking them up as unnamespaced
  silently returned null on both parse and write.
- **A blanket `"Interface"` strip in `Normalizer` — correct for code blocks, wrong for DBs.**
  Would have made every DB round-trip trivially pass without ever comparing member content, the
  exact silent-false-positive this harness exists to prevent. Fixed with a structural test (a
  DB's `Interface` always has a `Static` Section; a code block's never does), guarded by two new
  permanent `NormalizerTests`.
- Five more DB-specific block-config elements (`DBAccessibleFromOPCUA`, `IsOnlyStoredInLoadMemory`,
  `IsRetainMemResEnabled`, `IsWriteProtectedInAS`, `MemoryReserve`) added to the strip list, same
  reasoning as the FC set above.

FC pass, OR-merge/negation (S1 item 7 Phase A, 2026-07-11):

- **`Part Name="O"` (OR-merge) and `<Negated Name="operand" />` (a normally-closed contact) are
  real** — confirmed against `PerimeterSafetyAlarms`/`GeneralAlarms`. Neither needed a new `Normalizer`
  rule: both round-trip UId-for-UId (Part UId, Wire UId, `Cardinality` value all identical
  pre-import vs. post-compile re-export in the live proof below) — the raw diff between them is
  only `DocumentInfo`/whitespace, already normalized.

New LAD constructs found in the wild get added to the corpus *first* (failing), then fixed. Still
out of scope: `SW.Blocks.InstanceDB` and any UDT-typed/structured member (deferred as one unit —
a real example needs both together); TON/MOVE/comparisons/block calls; a multi-contact OR-merge
branch or a nested OR-merge (real-but-unconfirmed, hard error rather than guessed at). A second
FC block was searched for (12 candidates) without finding one that's both in current LAD scope
and free of Instance-DB dependencies — `docs/notes/stage-gates.md` has the detail; OR-merge
support closed that gap for `PerimeterSafetyAlarms` specifically (now `PerimeterSafetyAlarms`).
