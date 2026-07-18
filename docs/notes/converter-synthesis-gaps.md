# Parity checklist — `SidecarSynthesizer` gaps (the road to complete synthesis)

Goal + strategy: `docs/notes/synthesis-parity-plan.md` (derive-always north star). This doc is the
per-construct **parity checklist** — each gap = a reference-corpus block that must synthesize-match its
real export in the offline harness (`tests/golden/GoldenHarness.Tests/SynthesisParityRunner.cs`, matrix
at `tests/golden/synthesis-parity-matrix.md`). Close a gap → its block flips green in the matrix → move
it into the test's `KnownGreen` guard. **Complete synthesis = every corpus block green.**

Most gaps surfaced 2026-07-18: some across the gen-block-* validations (the wired-CALL feature itself is
done + TIA-proven, commit `e5bfeab`), the rest by the Stage-0 parity harness run (below). None
hand-patched (hard rule 7). `src/converter/` work — normal software rules, not `lad-coder`.

## Stage-0 harness baseline (2026-07-18) — 9/14 corpus blocks reach parity

The offline oracle (export → strip sidecar → `to-xml --synthesize` → `Normalizer` vs the real export)
turned the remembered gap list into data. First run flagged 9 failures; triage showed **4 were an oracle
under-strip** — the `Normalizer` wasn't treating `SW.Blocks.CompileUnit`'s block-scoped `ID` as volatile
(a real export numbers them 3/8, a fresh synthesis mints 1/2; `SynthesizerLiveCheck` proves TIA
reassigns it on import like every other UId). **Fixed** in `Normalizer.cs` (`ElementsWithVolatileId`),
lifting parity to 9/14. The remaining **5 are real synthesis gaps**, below, split into hard synthesize
errors and subtler compare-divergences:

- **Synthesize-blockers** (hard error, block can't be derived at all): SUB/DIV (`SignalConditioning`,
  Gap "arith" below); **WordAnd** (`DataHandling`, **Gap F — new**, not previously listed); TONR
  (`TimingAndCalls`, Gap C).
- **Compare-divergences** (synthesizes, but the derived graph differs from the real export):
  **timer-instance-scope** (`TimerSample`, **Gap G — new**: a single-instance TON in a *global* DB is
  synthesized `Scope="LocalVariable"` instead of `GlobalVariable`, plus a divergent `.Q`-read shape);
  **standalone-Not** (`BooleanExtras`, **Gap H — new**: a real `Part Name="Not"` invert-RLO element is
  synthesized as a `Contact` with `<Negated>` — logically close, structurally different; likely a
  readable-form expressiveness question — `NOT x` doesn't distinguish the two).

## Stage-1 reality check (2026-07-18) — the corpus blocks are dense; "small wins" are mostly gated

Starting Stage 1 revealed the reference corpus blocks are **multi-construct** (built to exercise the
*read* side comprehensively), so a single gap rarely isolates one block, and each remaining gap has a
hidden dependency:

- **Only `TimerSample` isolates a single small gap** (timer-scope). Gap G is now fixed, but Gap G2
  (dual-encoding) still holds it red.
- **`SignalConditioning` — GREEN 2026-07-18.** Closed the whole box family in one bundle: SUB/DIV kinds,
  **Mul-to-Mul ENO chaining** (`DIV(EN := ENO)` chains from the preceding SUB), and registry-typed
  **ABS/SWAP**. First parity gain of Stage 1 (9→10/14).
- **`DataHandling`** needs WAND **+ Calc + T_SUB + T_CONV + MOVE_BLK_VARIANT** (five builders).
- **The box family is gated behind the type symbol table.** WAND/ABS/SWAP each carry a **required
  `SrcType`** (`Word`/`Real`/`Word`) the read side pulls from source; to reach *parity* synthesis must
  emit the same type → the **`TagTypeRegistry` (Gap B/E) is the real keystone**, not a late item. Only
  SUB/DIV is type-independent (`SrcType=null`, TIA auto-infers).
- **Two gaps are dual-encoding / SPEC questions** (G2 timer-`.Q`, H standalone-Not): the readable IR
  admits two valid encodings and synthesis picks the other one from this particular export.

**Revised leverage order:** (1) the **`TagTypeRegistry`** — unlocks WAND/ABS/SWAP/Convert/Compare typing
in one stroke; (2) **Mul-to-Mul ENO chaining** + SUB/DIV; then the remaining box builders (ABS/SWAP/WAND,
Calc/T_SUB/T_CONV/MOVE_BLK_VARIANT); (3) resolve the **dual-encoding SPEC questions** (G2, H). A
complementary **Stage-2** move — add small *single-construct* Green reference blocks — would let each
incremental builder flip a block green instead of waiting for a whole dense block's constructs.

## THE CRITICAL ONE — the D-6 scoped merge is now the S7-on-real-blocks blocker (genval3, 2026-07-18)

The genval3 blind DOL→VSD `gen-block-modify-purpose` run proved the skill's *authoring + invariance
discipline* works (the modified block was authored; `converter diff --only` proved the N1–N5 skeleton
identical; the wired CALL synthesized clean) — but its **compile gate could not be reached**, because the
D-6 *whole-file* strip-and-synthesize requires the **entire** block to be within the synthesizable subset,
and a **realistic as-built equipment FB is not** (it uses TONR, array-index members, Real tag-vs-tag
comparisons — Gaps C/D/E below). Crucially, two of those hit **unchanged / STAY** networks, so the
whole-file path can't even reproduce a network the change didn't touch.

**Consequence:** the whole-file-strip workaround is fine for a block *entirely within the synthesizable
subset* (a generated block, test-project001) but **fails on a real as-built block** — i.e. exactly S7's
whole point. **The D-6 scoped `SidecarSynthesizer` merge** (`deferred-items.md` D-6 — keep every unchanged
network's *real* sidecar UIds as-is, mint fresh collision-safe UIds only for the changed/added statements)
sidesteps this entirely: unchanged networks keep their real TONR/array sidecars, so only the changed
networks must be synthesizable. **This is now the highest-priority converter build — it's the enabler for
modifying real blocks at all.** (Gaps C/D/E still matter for when a *changed* network uses those constructs;
the scoped merge only saves the *unchanged* ones.)

## Gap C — synthesis is TON-only (no TONR/TOF) — DONE 2026-07-18 (TimingAndCalls green)

**Fixed.** `BuildTimerSidecar` now accepts all three `TimerKind`s and passes `timer.Kind` through; a TONR
additionally builds its `R` reset operand (TON/TOF have none). The read side and `FlgNetBuilder` already
render each kind, so this was just building the right sidecar. Covered by `TimerScopeSynthesisTests`
(TONR carries kind + reset; TOF carries kind, no reset). Also unblocks the genval1/genval2 REQ-032-class
retentive-timer requests directly.

**Also fixed a Normalizer oracle gap it surfaced.** TimingAndCalls synthesized correctly but failed
*compare* on pure UId-value differences: the Normalizer didn't treat `<Call>` (a producer, like a
`<Part>`), `<Instance>`, or `<OpenCon>` UIds as volatile. Extended the volatility model (`<Call>` now
WL-refined alongside `<Part>`; `<Instance>`/`<OpenCon>` plain-stripped) — the same completion as the
CompileUnit-ID fix. This had been silently affecting **wired-CALL** parity too (the wired-CALL feature was
only ever validated by live TIA import, never the offline oracle, until now). No regression to the 14
`NormalizerTests`.

## Gap D — array-index local members mis-scoped `GlobalVariable`

**Symptom.** A local STATIC member accessed by array index (`RisingEdgeFlags[3]`) synthesizes with
`Scope="GlobalVariable"` → TIA `Tag "RisingEdgeFlags"[3] not defined`; the sibling scalar (`HandPosEdge`)
correctly gets `LocalVariable`. **Cause:** `SidecarSynthesizer.ScopeFor` splits the tag path on `.` and
checks the first component against the local-name set, but doesn't strip the `[i]` subscript, so
`RisingEdgeFlags[3]`'s first component isn't recognised as a local member. **Fix:** strip a trailing
`[…]` subscript before the local-name lookup in `ScopeFor` (small, self-contained). Add a fixture test.

## Gap E — tag-vs-tag comparison `SrcType` defaults to `Int` (mis-types Real)

**Symptom.** A comparison between two tags with no literal (`SpeedPerc < MinSpd`, both Real) synthesizes as
`SrcType="Int"` → Real-actual vs Int-formal compile error. `InferCompareSrcType` (the 2026-07-18 UDInt fix)
only infers from a *literal* operand's magnitude; a tag-vs-tag comparison has no literal, so it falls back
to `Int`. **Fix:** the same **`TagTypeRegistry`** Gap B wants — resolve a tag operand's type from the
DB/UDT member type; a tag-vs-tag comparison then takes the operand type. (So B, D-adjacent, and E converge
on the tag-type symbol table.)

**Through-line worth noting first.** Synthesis is steadily accreting a *symbol table*: the wired-CALL
feature added a **callee-interface registry** (block param types); the UDInt fix added magnitude-based
literal/comparison typing. Gap B below wants a **tag/member-type registry** (DB/UDT member types) — the
same "look types up from the other files in the batch / `--project`" pattern. Consider building B as a
sibling of `CalleeInterfaceRegistry` (a `TagTypeRegistry`) rather than a one-off, so comparison SrcType
(currently a magnitude heuristic) can eventually use it too.

## Gap A — UDT-typed CALL/interface param inline-nesting mints bad member names (small)

**Symptom.** An INPUT/OUTPUT parameter typed as a **UDT with inline nested members** synthesizes member
names with **leading whitespace** in the XML (`Name="  Ready"`), which TIA rejects. The STATIC
anonymous-struct path and the ordinary DB-member path both parse/emit clean — only the *block-interface
UDT-typed param* inline-nesting path is affected.

**Workaround in use.** Declare a UDT-typed param as a **bare type reference** (`Name : "UDT_X"`, no
inline member expansion) — TIA expands it from the UDT definition. This synthesizes and compiles clean;
it's what the genval2 build ended up doing.

**Fix (investigate first).** Trace the block-interface member serialization for a UDT-typed param with
nested members (likely `BlockSourceWriter`/`DbMemberLineFormat` interaction on the INPUT/OUTPUT path).
Either (a) suppress inline expansion for a UDT-typed param and always emit the bare type ref (matches
the workaround, and is what a real export does), or (b) fix the indentation so nested member `Name`s
don't carry leading whitespace. (a) is likely simpler and more correct. Add a fixture test (a UDT-typed
INPUT param, nested members) asserting clean member names; live-verify via re-import.

## Gap B — CONVERT/box typing needs a tag-type symbol table — REGISTRY BUILT 2026-07-18

**Status.** The keystone `TagTypeRegistry` (`Converter/Ir/TagTypeRegistry.cs`) is **built + unit-tested**
(sibling of `CalleeInterfaceRegistry`: resolves a dotted path → datatype from batch/`--project`
DB/UDT/tag-table `.ir` files — nested/UDT members, array-element typing, tag-table tags). It's **wired
into `BuildConvertSidecar`** (`SrcType` from the IN tag, `DestType` from the dest tag; falls back to the
old Real→DInt default only when a type is unknown — strictly better, never worse) and built in
`Program.cs` alongside the callee registry. **ABS/SWAP — DONE 2026-07-18** (SignalConditioning green): both resolve `SrcType` from the operand type
(hard-error if unresolvable — no safe default). The registry was extended to also index the **block's
own interface members** (`WithLocalMembers`, layered on in `SynthesizeBlock`), since these operands are
TEMP/STATIC members, not DB members. **Remaining consumers (queued):** WAND (Gap F) and comparison
`SrcType` (Gap E, replace the magnitude heuristic). Original design detail below (kept for the
operand-resolution reasoning):

### Original framing — Word→Int CONVERT is mis-typed (bigger; needs a tag-type symbol table)

**Symptom.** `SidecarSynthesizer.BuildConvertSidecar` hardcodes `SrcType=Real` / `DestType=DInt` (the
real Real-seconds→DInt-ms HMI idiom it was grounded on). A **Word→Int** telemetry convert (genval2
REQ-002: `ActualSpeed`/`MotorAmps1/2` from the inbound comms words) synthesizes but is mis-typed and
would fail compile. So REQ-002's engineering-unit conversion is currently **deferred** (raw words are
buffered and available; only the typed convert is missing).

**Why it's bigger.** A CONVERT's `SrcType`/`DestType` are the **operand tag/member types**, not
inferable from magnitude (unlike the UDInt literal case). It needs a **tag/member-type symbol table**
over the batch's DBs/UDTs/tag-tables.

**Fix (design).**
1. Build a `TagTypeRegistry` (sibling of `CalleeInterfaceRegistry`, `Ir/`): from the batch's DB/UDT/
   tag-table `.ir` files (+ `--project`), map a tag/member dotted path → its datatype (reuse the DB/UDT
   member models + `AccessNode.FromDottedPath` root/member resolution the `ProjectIndex` already does).
2. Thread it into `SidecarSynthesizer` alongside the callee registry; in `BuildConvertSidecar`, resolve
   `SrcType` from the IN operand's tag type and `DestType` from the dest tag type. Unknown type → a
   clear hard-error (don't silently keep Real/DInt).
3. Optional consolidation: let comparison `SrcType` (currently `InferCompareSrcType`, magnitude-based)
   prefer a known tag type when the operand is a tag — removes the small-literal-vs-wide-tag caveat.
4. `Program.cs`: build the `TagTypeRegistry` from the same batch/`--project` files as the callee one.

**Verify.** Unit tests (Word→Int, Int→Real, unknown-type hard-error); then the live gate — re-run the
genval2 build so REQ-002's telemetry convert synthesizes + compiles clean, closing that deferral.

## Gap F — WordAnd (WAND) synthesis unsupported (new, harness-surfaced)

**Symptom.** `to-xml --synthesize` hard-errors on a `WordAnd` ("Network 1: sidecar synthesis does not
support: WordAnds") — `DataHandling`, a committed reference block, uses one (masking word). The read
side handles WAND fine; only synthesis lacks it. **Fix:** add a `BuildWordAndSidecar` mirroring the
box-family shape (`BuildMulSidecar`/`BuildConvertSidecar`) — WAND is a two-input EN/ENO box with a
constant or tag mask. Its operand types feed the same `TagTypeRegistry` question as Gap B/E. **Verify:**
`DataHandling` flips green in the parity matrix.

## Gap G — timer-instance-scope mis-inferred for a global single-instance DB — FIXED 2026-07-18

**Symptom.** A single-instance `TON` whose instance lives in a **global** DB (`DB_Timers.SampleTimer0`)
synthesized `Instance Scope="LocalVariable"` where the real export has `Scope="GlobalVariable"`.
**Fix (landed):** `BuildTimerSidecar` now scopes the instance via `ScopeFor(timer.InstancePath,
localNames)` — global DB root → `GlobalVariable`, local STATIC member → `LocalVariable` — instead of the
hardcoded `LocalVariable`. Covered by `Converter.Tests/TimerScopeSynthesisTests`; `FBTimers` (a local
multi-instance FB timer) stays green, confirming the local case is preserved. (Adjacent to Gap D — both
are scope-classification of a dotted path.)

## Gap G2 — same-network timer `.Q` read → direct wire — DONE 2026-07-18 (TimerSample green)

**Built.** `BuildAssignment` (timers now built before assignments) emits a `TimerOutputStep` when a
coil's condition is exactly `<sameNetworkTimerInstancePath>.Q`; a cross-network `.Q` has no matching
same-network timer and falls through to an ordinary Access — exactly how TimerSample N3 renders both.
Covered by `TimerScopeSynthesisTests`. (Mid-chain same-network `.Q` isn't in the corpus and is left for
when a real case appears.) Detail below:

**Resolved as a plain synthesis rule (no SPEC change, no ambiguity).** TimerSample's own sidecar is the
proof: a `.Q` read of a timer defined in the **same network** is a `timeroutput` step (direct wire from
the TON's Q port), while a **cross-network** `.Q` read is an ordinary Access. Network 3 shows both at once
— its TON `IN` reads `SampleTimer0.Q`/`SampleTimer1.Q` (timers from N1/N2, cross-network) as ordinary
Access contacts, and its coil reads `SampleTimer2.Q` (same-network) as a `timeroutput` step; N1/N2 confirm
the same-network case. So the readable IR (`:= SampleTimerN.Q`) needs no change — synthesis just detects
whether the referenced timer instance is one built in this same network and, if so, emits a
`ChainStepSidecar.TimerOutputStep` (referencing that TON's part UId) instead of an ordinary contact+Access.
**Implementation note:** build the network's timers before the assignments/chains that read them, so the
TON part UId is known when the TimerOutputStep is minted. **Buildable now — recommend implementing.**

### Original framing (superseded — kept for the shape detail)
A same-network timer `.Q` read synthesized as an ordinary Access, not a direct wire.

**Symptom.** After Gap G, `TimerSample` *still* diverges: each `COIL outN := SampleTimerN.Q` reads a
timer's `Q` that is defined **in the same network**. The real export wires the TON's `Q` port **directly**
into the downstream coil — a `ChainStepSidecar.TimerOutputStep` as `steps[0]`, no intermediate `<Access>`
(the exact shape `TimerBindingSidecar.RailWireUId`-nullable and `Model.cs`'s own comment describe).
Synthesis instead renders `.Q` as an *ordinary tag Access* (`BuildTimerSidecar`'s doc comment says it
"never needs" the TimerOutputStep shape), producing one extra `Contact`+`Access` per network. Both are
valid TIA and both compile — but they're **two encodings of the same logic**, and the readable IR
(`:= SampleTimerN.Q`) doesn't distinguish them. **Same dual-encoding class as Gap H.** **Fix:** teach
synthesis to detect a condition that is exactly a same-network timer's `.Q` and emit a `TimerOutputStep`
(reference the TON part UId, order the TON before its reader) — structural, no symbol table. **Open
question first:** does TIA *always* export a same-network `.Q` read as a direct wire, or do both forms
occur? If both, the readable IR needs a way to say which (a SPEC question), like Gap H. **Verify:**
`TimerSample` flips green.

## Gap H — standalone `Not` vs negated contact — INVESTIGATED 2026-07-18: GENUINE ambiguity, OWNER DECISION NEEDED

**Both encodings occur in real exports, and the readable `NOT <tag>` conflates them.** Confirmed against
the corpus:
- **PerimeterSafetyAlarms** (passes parity): every `NOT <tag>` — including a lone `COIL := NOT SafetyZone1`
  and `NOT A OR NOT B OR NOT C` — is a **negated contact** (7 negated contacts, 0 standalone-Not steps).
- **BooleanExtras** (red): `COIL RunA := NOT EnableCmd AND FaultLatch` is a **standalone `Not` step** fed by
  a *non-negated* contact.

`to-ir` serialises both as `NOT <tag>`, so the readable form can't tell synthesis which to emit. The two
are **semantically identical for a `NOT <single-tag>` at a chain lead** (both compute `NOT tag`, both
compile) — the difference is purely which LAD element the engineer drew. (`NOT <compound>`, e.g.
`NOT (A OR B)`, is already unambiguously a NotStep and synthesises correctly — the ambiguity is only
`NOT <single-tag>`.) This looks like **author choice** preserved by TIA, not a deterministic rule.

**Three options (owner's call — touches `ir/SPEC.md` or accepts a known non-parity case):**
1. **Canonicalise + document (lean).** Synthesis always emits a negated contact for `NOT <tag>`; accept
   BooleanExtras as a known benign non-parity case. The distinction is semantically null, so this matches
   the readable-form principle ("logic, not drawing choices"). One real consequence: a *derive-always*
   round-trip of an existing standalone-`Not` block would render it as a negated contact — same logic,
   same compile, but a `|NOT|` box becomes a `|/|` contact in the editor (a reviewer would see the diff).
2. **SPEC grammar change.** Give the readable IR a distinct spelling for a standalone `Not` vs a negated
   contact so `to-ir` preserves it and byte-parity holds. Faithful, but adds grammar + reader/writer
   complexity for a semantically-null distinction.
3. **Heuristic rule (unconfirmed).** "`NOT <tag>` leading a *multi-element series* → standalone Not; else
   negated contact." It happens to fit both corpus blocks (Perimeter's NOTs are lone or in OR-branches;
   BooleanExtras' leads a series) and would make both green — but it rests on a **single** positive example
   and may just be coincidence/author-choice, so a block that drew `NOT A AND B` as a negated contact would
   mis-synthesise. Not recommended without more data.

**Recommendation: Option 1** unless editor-level visual fidelity of standalone-`Not` boxes matters.

### Original framing (kept for the shape detail)
Standalone `Not` (invert-RLO) synthesized as a negated contact.

**Symptom.** `BooleanExtras` has a real `Part Name="Not"` (a standalone invert-power-flow element);
synthesis emits a `Contact` with `<Negated Name="operand"/>` instead. Logically adjacent but a different
Part — so it synthesizes yet diverges from the real export. **Root question first (don't just force a
Part):** the readable form `NOT x` doesn't distinguish "standalone Not element" from "negated contact".
Options: (a) teach the readable form / synthesizer to emit a standalone `Not` Part where the source shape
calls for it (needs a way to tell them apart — a distinct IR spelling, or a reduction rule); (b) accept
the two as interchangeable where they're provably equivalent and normalize one to the other in the
oracle. Decide before coding — this is a readable-form expressiveness call, not only a synthesis one, so
it may touch `ir/SPEC.md`. **Verify:** `BooleanExtras` flips green (via whichever resolution).

## Where this is tracked
`AITODO.md` "Recently landed" → the wired-CALL bullet's two-follow-ups line points here; the parity
matrix (`tests/golden/synthesis-parity-matrix.md`) is the live scoreboard. Update both when a gap lands.
