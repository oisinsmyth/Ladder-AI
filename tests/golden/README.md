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
(`PerimeterSafetyAlarms`, S1 item 7 Phase A — OR-merge and negated contacts) and again the same
day with a 6th/7th artifact (`TimerSample`/`DB_Timers`, S1 item 8 — TON support, built directly
in the reference project by the project owner rather than derived from real production data,
since a TON-only block free of comparisons/Move/RCoil didn't exist in the real grounding
projects):
`ir/reference/{NodeStatusAlarms,CommsProcessData,AlarmWords,EquipmentStatus,
PerimeterSafetyAlarms,TimerSample,DB_Timers}.ir` / matching `simatic-ml/reference/*.xml`.
Structural shapes for the first five were derived from sanitized real production PLC data under
a private approval — no site or site specifics are recorded anywhere in this repo, and the
sanitization mapping (real name -> invented name) is intentionally never committed (`.gitignore`:
`sanitization/`). Every tag path, block/DB name, member name, and comment in the committed files
is invented; only structure (wiring topology, instruction types, slice/array addressing, member
types/retention) reflects something real — DBs are brought in *complete* (all members), not
trimmed to only what the paired FC references. See `docs/13-data-boundary.md`.

**Extended again 2026-07-14 (instruction-coverage corpus growth)** with 7 more artifacts closing
the gap `docs/audit/2026-07-14-code-quality-and-docs-audit.md` flagged: "proven live-TIA
round-trips aren't protected by any permanent regression suite" — roughly 15 of the ~24
instruction-level constructs this converter supports had only ever been proven against real,
uncommittable Amber-tier content, with no permanent committed evidence behind the claim.
`ThresholdAlarms` (comparisons — Eq/Ge/Lt/Ne/Gt/Le, all as infix operators), `SignalConditioning`
(Mul/Convert ENO-chained, Lt-gated Sub/Div, Abs, Swap), `DataHandling` (WAND, Calc, T_SUB/T_CONV,
MOVE_BLK_VARIANT, Move), `BooleanExtras` (standalone Not, SCoil/RCoil), `FBTimers`/`ScaleValue`/
`TimingAndCalls` (TONR/TOF against a hand-numbered standalone-timer DB, CALL to a new,
self-authored callee FC). Unlike the first seven, these are original content composed from
already-proven unit-test fixtures with invented tag names throughout (not derived from a fresh
real export) — see `docs/notes/stage-gates.md`'s own 2026-07-14 "reference corpus growth" entry
for the full per-block story, including two real structural findings from live verification
(`TONR_TIME`'s DB-member shape has no `R` field despite `R` being a real wired port; a bare
`T#5S`-style time literal is rejected on a TONR/TOF `PT` port in this shape — both worked around
rather than guessed past). `WAIT`, `Jump`, and Modbus_Master/Modbus_Comm_Load's own multi-instance
form are deliberately **not** in the corpus — real, separately-documented reasons in
`AITODO.md`'s "Open questions", not oversights.

The full live round-trip (`export -> to-ir -> to-xml -> [sanitize ->] import -> compile ->
re-export`, `Layer 1` assertion 2 via `Normalizer.AreSemanticallyEquivalent`) has been run and
passed against a real TIA project for all seven artifacts, DBs compiled before the FCs that
depend on them (`tests/golden/GoldenHarness.Tests/ReferenceProjectRoundTrip.cs` documents how to
re-run it — needs a live Portal session, not wired into an always-running `[Fact]`, same
reasoning as `RoundTripRunner.RunFull` itself).

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
a real example needs both together); MOVE/comparisons/block calls; a multi-contact OR-merge
branch or a nested OR-merge (real-but-unconfirmed, hard error rather than guessed at). A second
FC block was searched for (12 candidates) without finding one that's both in current LAD scope
and free of Instance-DB dependencies — `docs/notes/stage-gates.md` has the detail; OR-merge
support closed that gap for `PerimeterSafetyAlarms` specifically (now `PerimeterSafetyAlarms`).

## TON round-trip proof, and a second Normalizer finding (2026-07-11)

`FC TimerSample` (purpose-built by the project owner in `SampleProject` — the reference project's
own live TIA copy — specifically to close out TON support) ran the full `export → to-ir → to-xml
→ import → compile → re-export → Normalizer` cycle and passed. Since committed to the corpus
alongside `DB_Timers` (6th/7th artifacts — see "The corpus" above).

Getting there surfaced a real, second UId-volatility finding, parallel to the already-documented
Wire one: **TIA reassigns `Access` element UIds on its own Import()/Compile()/Export() cycle
too** — contrary to the earlier assumption that Part/Access UIds stay stable (only Wire's own UId
was known to be volatile until now). Unlike Wire, an Access is *referenced* elsewhere (every
`IdentCon`), so a plain attribute strip isn't enough: `Normalizer.Strip` now builds a per-network
map from each Access's own UId to a content-derived key (its `Scope`+`Symbol`, or its literal
constant value) and rewrites both the Access element itself and every `IdentCon` pointing at it
to that key before comparing — so two documents compare equal regardless of which arbitrary
number TIA assigned to which Access. Scoped **per `<FlgNet>`** (one map per network), not
flattened across the whole document — UId numbering restarts at the top of every network, so a
document-wide map silently collided real entries across networks in a 3-network block; caught
live by this exact test, a single-network fixture would never have exposed it.

Also fixed alongside it: `<Parts>` order (like `<Wires>` order, already normalized) isn't
semantically meaningful either — TIA doesn't preserve `BlockSourceWriter`'s own Access/Part
ordering on re-export. Both fixes are covered by the existing `NormalizerTests.cs` suite pattern
(11 tests, all still passing) plus this live round-trip itself.

## Reference corpus growth: 7 new blocks, a real `RunAll` bug, two pre-existing staleness bugs (2026-07-14)

Full story of the 2026-07-14 corpus extension mentioned in "The corpus" above.

**Two pre-existing `.ir` files were stale, not caused by this pass but found while placing new
content alongside them**: `NodeStatusAlarms.ir`/`PerimeterSafetyAlarms.ir` still used a sidecar
text format that predates the scope-suffix-on-`access`-lines and nested-`OrStep`-branch changes
(S1 item 11, 2026-07-11/12) — the current `IrParser` can no longer parse them. The committed
`simatic-ml/reference/*.xml` for both was confirmed, via `Normalizer.AreSemanticallyEquivalent`
against a fresh live export, to have no semantic drift at all — only the `.ir` text's own encoding
was stale. Regenerated both from a fresh export and confirmed self-stable (a full parse → build →
write → re-parse → re-serialize cycle reproduces the committed text byte-for-byte) before
replacing them.

**New content methodology**: every new block reuses an already-proven `Converter.Tests/Fixtures/*.xml`
unit fixture (scope swapped to `LocalVariable`, tags renamed to something thematically coherent)
rather than inventing new wiring shapes — the instructions were already grounded against real
data when those fixtures were built; this pass only needed to prove they compile as *permanent,
committed* content, not re-derive their shape. Two genuine exceptions, both deliberately avoiding
a shape the fixture itself used real but this pass had independent reason to distrust:

- **The richer "telescoping" `Move` shape (two taps sharing chain positions with a terminal Coil,
  `MoveTelescopingChain.xml`) was tried first and rejected by live TIA import** — `"The elements
  must be sorted according to the current flow"`, the same class of error the S1 item 26
  `FlgNetBuilder` Part-ordering bug produced, but confirmed **not** the same bug: `FlgNetBuilder`'s
  own general `parts.Sort((a, b) => a.UId.CompareTo(b.UId))` fix (added for that earlier bug) was
  already in effect and didn't help here. Left as a real, still-open question about non-rail
  multi-consumer wire-endpoint ordering — not forced through by guessing. `SignalConditioning`/
  `DataHandling` use the simpler, already-safe plain-`Contact`-tap `Move` shape instead.
- Standalone `Not` (`NotFedByContact.xml`) also taps a shared, non-rail wire the same way — given
  the `Move` finding above, `BooleanExtras` uses a plain linear chain (`Contact -> Not -> Contact
  -> Coil`) instead, sidestepping the same open question. This is also the **first-ever live-TIA
  verification of standalone `Not` at all** — `ir/SPEC.md` had explicitly flagged it as unverified
  (every real instance found always paired it with an unbuilt `CALL`, so no real network could
  isolate it before now).

**Two genuinely new real findings, both from `TimingAndCalls`' own live verification**:
`TONR_TIME`'s DB-member nested-member shape doesn't include an `R` field ("Element 'R' cannot be
found") despite `R` being a real, wired port on the `TONR` `Part` itself — matches `TON_TIME`'s
plain `PT`/`ET`/`IN`/`Q` shape exactly. And a bare time literal like `T#5S`/`T#5000MS` is rejected
on a `TONR`/`TOF` `PT` port in this shape (`"The value ... cannot be set for the parameter of the
type 'Time'"`) — worked around by reverting to the already-grounded tag-fed `PT` (matching
`WithTonr.xml`/`WithTof.xml`'s own real shape) rather than chasing the literal format further.

**`FBTimers` (DB30) generalizes the `DB_Timers` precedent** — a hand-numbered, ordinary `GlobalDB`
with `TONR_TIME`/`TOF_TIME`-typed members — deliberately instead of a dedicated FB +
`create-instance-db`: that path is confirmed broken for *any* fresh instance-DB creation in this
project's current state (`docs/notes/openness-quirks.md`'s "standalone system-FB instance DBs"
finding, from the same session's Modbus_Master/Modbus_Comm_Load work) — `create-instance-db`
deterministically assigns an invalid `DB0`, not repairable via any exposed API. Hand-authoring the
DB directly, with an explicit valid number chosen up front, sidesteps the whole problem — TIA
doesn't distinguish "GlobalDB with a system-struct-typed member" from a dedicated `InstanceDB`
for this purpose, and only the latter has the auto-numbering bug.

**`TimingAndCalls`' own `CALL` target changed mid-pass**: the real Siemens "Scale" instruction
referenced in `ir/SPEC.md`'s own `CALL` section (`FB MotorVSDSystem`'s dependency) turned out, on live
compile, to be real *site* content from `JOB9002` (sanitized elsewhere as `AnalogScale`), not a
built-in library instruction — `"The referenced block Scale no longer exists"` in `SampleProject`.
Replaced with `ScaleValue`, a new, small, self-authored callee FC (`Input`/`Output` interface,
`IsBareParameter` on every member — the real shape for FC parameters, confirmed by a live rejection
of the default `Remanence`-attribute shape `DbMember`'s own scalar-member default otherwise
produces).

**A real, general bug in `RunAll` itself, found only by finally running the complete corpus (old +
new, 14 blocks) together in one pass** — apparently never done before this point. Re-*importing*
any block, even byte-identical content, flags every block that references it (a DB's own
dependent FC, an FB a `CALL` targets) as freshly `IsConsistent = false` again, regardless of
dependency order — TIA's own cascade, not a content bug. A naive per-block loop (what `RunAll` did
before) processes this sequentially, so an earlier block's own *import* step can invalidate a
later, dependent block's *baseline export* before that block's own turn even starts — confirmed
live (`NodeStatusAlarms` failing to export because `CommsProcessData`/`AlarmWords`, both earlier
in dependency order, had already been freshly re-imported). Fixed with a proper three-phase
`RunAllSettled` (`RoundTripRunner.cs`): capture every block's baseline export *first*, before
anything is imported; regenerate and re-import everything second; compile and re-export each one
last, so nothing subsequent can touch it again. All 14 reference-project blocks — the full
committed corpus — now verified together in one run: import, compile (0 errors), re-export,
`Normalizer`-equivalent, every one.

Also fixed alongside it: `RunFull`'s own compile-stage check used `openness-cli compile`'s raw
exit code, which is non-zero for *any* non-`Success` state — including the same benign
hardware-config warning every block in this project shows, with 0 actual errors. Stricter than
this project's own established "0 errors is clean, warnings are expected" bar used everywhere
else in `stage-gates.md`. Now parses the JSON body and gates on the real error count.

## Corpus and coverage as it now stands (2026-08-05)

Two corrections to what the dated sections above imply, both found by the 2026-08-05 project audit.

**The reference corpus is 15 `.ir` / 15 `.xml`, not 14.** "All 14 reference-project blocks — the full
committed corpus" above describes *that pass's* `RunAllSettled` run (2026-07-14) and was accurate when
written; it is not a standing count. The 15th is **`HandAuthorSplitsMerges`** (`FC`, hand-authored, added
2026-07-19 for ADR-0006). It exists to pin down one thing the other 14 can't: **contact fan-out is a
drawing choice, not derivable from the logic.** Each of its networks is paired with a split-free
equivalent — byte-identical readable logic, different sidecars — which is exactly why the readable IR
had to grow the per-node `{split N}`/`{recv N}` markers once ADR-0005 made the sidecar derived rather
than stored. It is stored readable-only (regenerated from its export via `to-ir --no-sidecar`), and its
byte-exact re-derivation is the guard on the marker grammar (`SynthesisParityTests`,
`SidecarSynthesizerFanoutTests`, `FanoutMarkerGrammarTests`).

**The automated harness covers two projects, not just `reference`.** Both committed-corpus tests pair
`ir/<project>/` with `simatic-ml/<project>/` for **`reference` and `test-project001`**:

- `CommittedBlocksRoundTripTests` — every *readable-only* `BLOCK` with a matching export must
  synthesize to a semantically equivalent form (the ADR-0005 derive-always safety net). It
  deliberately skips DB/UDT/tag-table `.ir` (no sidecar concept), sidecar-carrying blocks (allowed to
  be non-derivable), and blocks with a committed frozen answer key (guarded by
  `FrozenAnswerKeyRoundTripTests` instead, since their exports have drifted).
- `ExportDriftDetectorTests` — runs `converter drift-check` per project and asserts the drifted set
  equals a documented baseline: empty for `reference`, six known-drift blocks for `test-project001`
  (`DB_Settings`, `FB_PusherControl`, `FB_ShredderSequencer` and their three instance DBs — the
  consciously-deferred D-7 re-export, `docs/notes/deferred-items.md`). A *new* name appearing there
  means a block drifted unexpectedly; investigate rather than extend the baseline.

The live-cycle checks (`ReferenceProjectRoundTrip`, `SynthesizerLiveCheck`) remain manual-run `static`
classes needing a Portal session, not `[Fact]`s — same convention as `RoundTripRunner.RunFull`.
