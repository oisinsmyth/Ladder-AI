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
a private approval — no organisation or site specifics are recorded anywhere in this repo, and the
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
compile, to be real *restricted* content from `JOB9002` (sanitized elsewhere as `AnalogScale`), not a
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
  equals a documented baseline. A *new* name appearing there means a block drifted unexpectedly;
  investigate rather than extend the baseline. **The baseline stopped being a flat `string[]` on
  2026-08-13** — see "A drift baseline has to say *why*" below.

The live-cycle checks (`ReferenceProjectRoundTrip`, `SynthesizerLiveCheck`) remain manual-run `static`
classes needing a Portal session, not `[Fact]`s — same convention as `RoundTripRunner.RunFull`.

## What the Normalizer discards, and why two suites now compare raw (2026-08-13)

This harness is one of only two checks in the toolchain with an **outside authority** in its loop — it
compares against real TIA exports, which the converter did not produce (`docs/notes/autonomous-working-agreement.md`,
*"a proof is only as strong as the most independent authority in its loop"*). But almost all of it reaches
that authority **through `Normalizer.AreSemanticallyEquivalent`**, which is converter code with an ignore
list — so anything on that list is outside the harness's reach no matter how good the answer key is. That
is the same shape as the wire-endpoint bug, where the converter and the Normalizer were blind in exactly
the same way and cancelled. `WireEndpointDirectionTests` was the first raw, Normalizer-free guard;
these are the second and third.

- **`BlockInterfaceFidelityTests`** — a block's `<Interface>` compared raw against its real export,
  committed `.ir` → `to-xml` (the `drift-check` direction). `Normalizer.IsVolatile` drops any
  `<Interface>` with no `<Section Name="Static">`, justified by a premise that has since expired
  ("a code block's Interface is known-boilerplate by the time it reaches here"). Two classes of block
  now arrive with real content there: **an FC with parameters** (measured: retyping `ScaleValue`'s
  `ScaleFactor` `Real` → `Int` and comparing the derived XML against the real export returns
  `VERDICT: EQUIVALENT`) and **a UDT**, whose interface is `<Section Name="None">` — and a UDT is
  *nothing but* its interface, so the comparison is left with a name and a block number.
  *Found by this suite:* `UDT_PusherIO` and `UDT_ShredderSequencerIO` each declare a member
  (`AutoStartSignal : Bool`) that is **absent from their committed exports**, and `drift-check` has
  been printing `MATCH` for both — so `ExportDriftDetectorTests` was green over real drift that its
  own baseline never listed. Both are now named in `KnownInterfaceDrift`, asserted in both directions
  (an unlisted block must match; a listed one must still differ).
- **`MemoryLayoutFidelityTests`** — `<MemoryLayout>` compared raw, real export → `to-ir` → `to-xml`.
  The Normalizer compares this element only when *both* documents declare one, which is right for a
  corpus that predates the emit side and means, re-measured 2026-08-23, that **1 of the 58 committed
  `.ir` declares a layout while 28 of 41 exports do** (it was 0 of 26 / 27 of 38 when this was written)
  — so all but one `AreSemanticallyEquivalent` call reachable from this suite still runs with the
  assertion opted out. `NoCommittedIr_DeclaresALayout_...` records that as a number so re-deriving the
  corpus (which would arm the Normalizer's own comparison) cannot pass unnoticed — and it worked: the
  count moved to 1 when `FB_Comms_ModbusServer` was authored declaring `MEMORYLAYOUT Optimized`
  (`763cdf6`), a direction the comment had not predicted.

Both were negative-tested against a converter built from `HEAD` with the defect surgically reintroduced.
Dropping the two `BlockMemoryLayout.AppendIfPresent` calls — the exact historical hole, where the export
carries no layout, the import states no opinion and TIA applies the S7-1200 default — produces **29
failures, every one of them in `MemoryLayoutFidelityTests`**: the other 46 tests stay green over it.

**Residual, and it needs a Portal session, not more test code:** every committed export declares
`Optimized` and none declares `Standard`, so the corpus alone cannot tell "carries TIA's value" from
"always writes Optimized". `MemoryLayoutValue_IsCarriedThrough_NotHardcoded` closes that by mutating one
real export's single layout value to `Standard` and round-tripping it — the structure and position stay
TIA's and only that value is synthetic, but it is not a substitute for a real `Standard` export in the
corpus (`openness-cli block-layout --set Standard --yes`, then export).

## The SECOND opt-out, and its counter (2026-08-23)

`Normalizer.AreSemanticallyEquivalent` gained a second cross-document opt-out beside
`compareMemoryLayout` on 2026-08-23: `DerivedInterfacePlan.For(...)` (converter `2f7abba`), four rules
covering TIA-side expansions the converter does not author — R1 a typed member's `<Sections>`, R2 its
system-maintained `<AttributeList>`, R3 an instance DB's regenerated member list, R4 Real-literal
re-rendering. It arrived with **no counter anywhere in this harness**, and its only tests were
`Converter.Tests/DerivedInterfaceExpansionTests.cs` — *the same lane whose blind spot the rules create*,
which is the exact failure this whole section exists over.

**`DerivedInterfaceBlindSideTests`** is the census. For every committed `.ir`↔export pair it builds the
plan the gate builds, strips the export twice (once with `DerivedInterfacePlan.Nothing`, once with the
plan) and counts what vanished — the EFFECT, not the plan's private intent. One counter per dropping
rule, **counted in SITES not blocks**: a layout is a per-object boolean, but an expansion opt-out is a
per-site quantity, and a rule widened from "typed members" to "all members" would fire many more times
inside the *same* blocks without moving a block count.

**R4 deliberately gets no counter.** It is a value-preserving canonicalisation, not an ignore — `0.10`
and `0.1` are the same number, `0.1` and `0.2` stay different — so its blind side is empty by
construction and a count would measure activity rather than exposure. It gets the *demonstration* of
that property against a real TIA export instead.

🔴 **All three counters read ZERO, and that is a finding about the CORPUS, not a clean bill of health.**
Everything the rules were built for is unreachable from here: `FB_Comms_ModbusServer.ir` declares
`MB_SERVER 5.3` and `TCON_IP_v4 1.0` bare — the exact R1/R2 shape — and **has no committed export**;
every *paired* instance DB declares its members in full, so R3's precondition is never met, while the
member-less ones (`iDB_Hx*`) have no export either. The whole measurable blind side sits on blocks named
in `CommittedBlocksRoundTripTests.KnownMissingExports`. Because a zero from "nothing fires" and a zero
from "the probe is broken" look identical in a run log, each rule is handed a real export with the one
thing it keys on removed, and the census must both see the drop and file it under the right rule
(`TheCensus_SeesADrop_WhenTheRuleIsGivenOneToPlan`) — the zeros are only worth reading because of it.

## The ignore-list audit (2026-08-13) — MemoryLayout was the first of fifteen

Two of the Normalizer's ignores were measured on 2026-08-13 and both were holes, so the rest were
measured too. `Normalizer.VolatileElementNames` has **22 entries**; the result, against all 38 committed
exports:

| verdict | count | entries |
|---|---|---|
| **hole** — present in real exports, dropped by the converter, ignored by the comparator | **14** | `DBAccessibleFromOPCUA`, `IsOnlyStoredInLoadMemory`, `IsWriteProtectedInAS`, `MemoryReserve`, `IsRetainMemResEnabled`, `SetENOAutomatically`, `IsIECCheckEnabled` (semantic); `AutoNumber`, `HeaderAuthor`, `HeaderFamily`, `HeaderName`, `HeaderVersion`, `UDABlockProperties`, `UDAEnableTagReadback` (metadata) |
| **load-bearing**, verified | 1 | `DocumentInfo` — a whole-export envelope, absent from single-block output by construction |
| **unmeasurable against this corpus** | 7 | `CreationDate`, `ModifiedDate`, `CompileDate`, `CodeModifiedDate`, `InterfaceModifiedDate`, `StructureModified`, `ParameterModified` — **zero occurrences in any committed export** |

**The converter emits exactly one of the 22 — `MemoryLayout` — and only since 2026-08-12, i.e. after it
bit.** For the other 21 no writer in `src/converter/Converter/SimaticMl/` produces the element at all.
So the 14 holes are all the same shape as `MemoryLayout`: *we emit nothing, TIA supplies its default on
import, the two therefore agree, and the ignore list is what makes the agreement look like a match.*
They have not bitten only because every value in the corpus is uniform (`IsIECCheckEnabled=false`,
`DBAccessibleFromOPCUA=true`, `MemoryReserve=100`, …) — which is exactly how quiet `MemoryLayout` was
until someone set a non-default value and watched it revert.

`BlockPropertyFidelityTests` compares every block-level `<AttributeList>` property raw across
export → `to-ir` → `to-xml`, with those 14 named and their cost recorded, so a **15th cannot join them
unnoticed** and each one closing is detected. It also asserts the two other directions — a property
surviving with a *changed* value, and a property we *invent* that TIA never stated — and keeps a positive
control (`Name`/`Namespace`/`Number`/`ProgrammingLanguage` do survive, value-identical, as does
`IsFailsafeCompliant`, which is on no ignore list).

Nothing was un-ignored: a Normalizer that ignores too little is as broken as one that ignores too much,
and the wire-endpoint fix had to preserve what its sort was protecting.

**What would settle the 7 unmeasurable entries, with zero new test code:** they are date/modified stamps
that TIA plausibly regenerates on *re-export after a compile*, and **the committed corpus contains only
first exports.** Committing one block's post-import/compile re-export beside its original settles all
seven at once — which is precisely what `ReferenceProjectRoundTrip` / `RoundTripRunner.RunFull` already
produce during a live cycle; they just discard it. Until then these are **unmeasured, not clean**, and
the Normalizer's own comment says as much ("informed by the PlcBlock properties reflected on in
`docs/notes/openness-api-surface-v20.md`, but **unverified against a real re-export**").

Two ignores outside `VolatileElementNames` were also measured. The `MultilingualText`
`CompositionName="Title"` skip is **latent, not MemoryLayout-shaped**: the converter *does* emit titles
and network titles round-trip with identical content, so the skip currently hides only element ordering
and a DB's own (empty) block-level Title — a real but low-stakes gap, and not the "we emit nothing, TIA
defaults it" pattern. `ElementsWithVolatileId` (Wire/Access/Part/Call/Instance/OpenCon/CompileUnit) and
the `Wires`/`Parts` ordering rules are **load-bearing on recorded live evidence** — TIA reassigns those
UIds unprompted — and the one place order *is* meaningful, a wire's first endpoint, is already exempt and
guarded raw by `WireEndpointDirectionTests`.

## A drift baseline has to say *why* (2026-08-13)

`ExportDriftDetectorTests`' baseline used to be a flat `string[]` of block names. *** THAT FLATNESS IS
WHY TWO REAL UDT DRIFTS SAT INVISIBLE. *** A list that cannot record why a name is on it cannot tell
*"we looked at this and decided to live with it"* apart from *"this turned up and nobody has ruled on
it"* — and once the second kind is written in the same shape as the first, it stops being a question and
becomes furniture. Entries now carry a `DriftDisposition`:

| disposition | meaning | gates? |
|---|---|---|
| `Deferred` | looked at, consciously accepted; clears on a re-export (the D-7 six) | no |
| `Unruled` | a real divergence **nobody has ruled on** — a question, not a tolerance | **YES** |
| `IncompleteExport` | the committed **export** is not a faithful TIA artifact, so the answer key is wrong and our output is right (`NodeStatusAlarms`) | no |
| `RepairInProgress` | an owner ruling **exists** and the repair is **not finished** | **YES** |

There are **four**, and this table listed three until 2026-08-23 — `Unruled` was added on the same day
the table was written and the table never caught up.

`RepairInProgress` is the state the flat list could not express, and it is a hard gate because a
decided-but-unfinished repair is exactly what quietly becomes a permanent baseline entry. `Unruled` is
a hard gate for the mirrored reason: an undecided thing written in the shape of a decided one is the
whole failure the enum exists to prevent. Both are checked by **`NoDriftEntry_IsAnOpenQuestion`** — the
old name `NoKnownDrift_IsAnUnfinishedRepair` was retired when `Unruled` joined, and stood in this file
pointing at nothing for ten days. Its passing branch is demonstrated, not assumed — re-filing the
entries as `Deferred` turns the suite green, so the gate is not red by construction.

🔴 **And a gate nobody clears is a gate that costs.** The one `Unruled` entry, `DB_Settings`, was
answered by a repair in a different lane **eight minutes after it was written** (`984e5af`, which
deliberately left `tests/golden/` alone: *"the golden lane owns the baseline and delists the entry"*).
The golden lane did not come back, so the suite stood red for ten days over a closed question. Delisted
2026-08-23; the record of how is in the baseline where the entry used to be.

The same distinction was added to `CommittedBlocksRoundTripTests.KnownIncompleteAnswerKeys` and
`SynthesisParityTests.KnownIncompleteAnswerKey`: a block that fails parity because **its export is
incomplete** is not a synthesis gap, and filing it as one (by simply leaving it out of `KnownGreen`)
puts it in a bucket nobody revisits. Both are asserted in *both* directions — an entry that starts
passing turns the suite red and tells you to promote it back to a hard guard.

**What triggered all of this**: converter `ae62f76` made an `<Interface>` compared rather than
discarded, and the drifted set immediately grew by three. Until that fix `drift-check` reported `MATCH`
on **any** UDT no matter what had changed, because a UDT's interface *is* its whole definition and the
Normalizer discarded any interface with no `Static` section.

### `IncompleteExport` has to be earned, not cited (2026-08-13)

`IncompleteExport` says *the answer key is wrong, not us*. **That is a claim about the corpus, not about
the converter, and it is what every failing comparison feels like from the inside** — so it is the one
disposition most likely to be reached for later as an excuse, with `NodeStatusAlarms` cited as precedent.
It is a **bar**, not a precedent, and two of its four conditions are mechanised
(`EveryIncompleteExportClaim_MeetsTheEvidenceBar`, which covers **all three** filing sites so it cannot be
dodged by filing in the easiest one):

1. **Mechanised** — the committed export must be independently identifiable as *not* a faithful TIA
   artifact. The marker this corpus has is a **missing `<DocumentInfo>` envelope**: every genuine V20
   export carries one, and the four 2026-07-10 seed files were committed with TIA's scaffolding trimmed.
   That is a property of the *file*, checkable without any opinion about our output.
2. **Mechanised** — a **genuine peer export of the same root kind** must exist in the corpus, or "TIA
   would have carried this element" has nothing behind it. (`ScaleValue.xml` is what proves a real FC
   export carries an `<Interface>`.)
3. **Author's, stated in the `Reason`** — the difference must be **localised and enumerated** with
   `converter compare`, never "VERDICT: DIFFERS". `NodeStatusAlarms` was *one* difference and its Reason
   says which.
4. **Author's, stated in the `Reason`** — every difference must be an **addition of TIA's own defaults**:
   an element the trimmed export lacks and a genuine one carries. *** IF ANY DIFFERENCE IS A CONTENT
   CHANGE — A MEMBER, A VALUE, A WIRE, A TYPE — IT IS NEVER `IncompleteExport`, *** however sure you are
   that we are right. That is drift, and the two UDTs are what happens when you rule on one without first
   asking why it is there.

The vacuity guard matters as much as the bar: if *no* export carried a `<DocumentInfo>`, "missing
`<DocumentInfo>`" would distinguish nothing and the check would wave everything through, so that is
asserted too.

### A known-problem list must not be a `[Theory]` (2026-08-13)

Four guards in this suite are staleness checks over a **known-problem list** — `KnownMissingExports`,
`KnownInterfaceDrift`, `KnownDroppedProperties`, and the uncovered-block check. As `[Theory]` +
`MemberData` **every one of them fails with `No data found` the moment its list empties** — which is the
state the work is driving towards. *** A TEST THAT CANNOT EXPRESS SUCCESS PUNISHES ITS OWN FIX. *** Two
of the four did exactly that when `c49f5e9` committed the last missing exports. All four are `[Fact]`s
now: vacuously green on an empty list, and the "empty is not clean" concern is moved to where it belongs
— the **corpus enumerations**, which stay `[Theory]` precisely because an empty corpus really is broken.

The distinction to apply when adding a guard: *is this list a population, or a list of things wrong with
the population?* A population must never be empty. A problem list should be trying to become empty.

### `Deferred` is a claim with an expiry date

`Deferred` says *our `.ir` is ahead of the corpus and we will re-export later* — a claim about the
**reference data**. It has now been disproven twice, both times by a re-export that did not clear the
entry:

- the three instance DBs sat as *"interface cascade from its FB"* when the real cause was `converter
  to-xml` omitting the empty `<Section Name="InOut"/>` that every real TIA instance-DB export declares.
  **Our defect, filed as a property of the answer key — which makes it a closed question.** No re-export
  could ever have cleared it, which is precisely what the disposition promised would happen. (Converter
  lane, `f2a548a`.)
- `DB_Settings` sat as *"D-7 deferred re-export"* straight through the live re-export in `f0fb0cb`.

So: **if a block is re-exported and still drifts, the `Deferred` claim is dead and the entry must be
re-filed** — it is not a `Deferred` any more, whatever its Reason says. That is what `Unruled` is for.
