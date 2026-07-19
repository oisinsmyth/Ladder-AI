# Parity checklist — `SidecarSynthesizer` gaps (the road to complete synthesis)

Goal + strategy: `docs/notes/synthesis-parity-plan.md` (derive-always north star). This doc is the
per-construct **parity checklist** — each gap = a reference-corpus block that must synthesize-match its
real export in the offline harness (`tests/golden/GoldenHarness.Tests/SynthesisParityRunner.cs`, matrix
at `tests/golden/synthesis-parity-matrix.md`). Close a gap → its block flips green in the matrix → move
it into the test's `KnownGreen` guard. **Complete synthesis = every corpus block green.**

Most gaps surfaced 2026-07-18: some across the gen-block-* validations (the wired-CALL feature itself is
done + TIA-proven, commit `e5bfeab`), the rest by the Stage-0 parity harness run (below). None
hand-patched (hard rule 7). `src/converter/` work — normal software rules, not `lad-coder`.

## Real-block synthesis divergence (2026-07-19) — investigated; the reference corpus is not representative

The derive-always migration audited **every** committed block, not just the 14-block reference corpus, and
found **4 that don't round-trip**: `MotorStarter` (`patterns/motor-dol`) and the `test-project001` FBs
`FB_MotorFwdRevSystem`, `FB_PusherControl`, `FB_ShredderSequencer`. Digging in (2026-07-19) untangled
**three distinct causes** — the audit's single number conflated them:

**1. A stale external answer-key (partly the corpus, exactly as hypothesised).** The audit compared synth
vs the committed `simatic-ml/test-project001/*.xml`. But those `.ir` files carry **later fixes the `.xml`
never re-captured** — e.g. `FB_ShredderSequencer`'s `.ir` has the B-5/REQ-028 (2026-07-17) re-arming-window
fix (`IN := NOT (IO.Step = 60 AND NOT ...) AND ReversalCount > 0`) while its `.xml` still has the pre-fix
`IN := ReversalCount > 0`. So the `.ir` is *ahead of* the `.xml`. **The correct migration oracle is
synth-vs-the-block's-own-stored-sidecar, not synth-vs-external-XML** — comparing to the block's own sidecar
shrank the diffs (PusherControl 543→198, ShredderSequencer 266→102), confirming a chunk was stale-XML noise.
(Follow-on: re-export the `test-project001` corpus so `.xml` catches up to `.ir`, or drop the external
answer-key for it and rely on self-consistency.)

**2. Gap D (array-index local scope) — FIXED 2026-07-19.** `ScopeFor` kept the `[i]` subscript on the first
path component, so `RisingEdgeFlags[3]` mis-scoped to GlobalVariable (TIA: undefined global tag). Now strips
the subscript before the local-name lookup. Covered by `ArrayIndexScopeSynthesisTests`. (MotorStarter 1188→976.)

**3. Real, multiple, remaining synthesis gaps.** Working the own-sidecar oracle down:
- **Timer-`.Q` instance scope — FIXED 2026-07-19** (commit `adbb660`): a same-network timer `.Q` is a
  direct-wire `TimerOutputStep` only for a **GLOBAL-instance** timer (TimerSample); a **LOCAL/FB-instance**
  timer's `.Q` is an ordinary `LocalVariable` Access (FB_ShredderSequencer N11). This **perfected two of
  the four** blocks: `FB_ShredderSequencer` and `FB_PusherControl` now round-trip through readable-only
  exactly (0 diff). `FB_MotorFwdRevSystem` and `MotorStarter` remain.
- **Fan-out / shared-contact — SPLIT grammar built 2026-07-19 (clean cases derivable; cross-depth residual).**
  Hand-authored split/merge pairs (`HandAuthorSplitsMerges`, exported from SampleProject) proved fan-out is
  a **pure drawing choice**: split and split-free networks have byte-identical readable but different
  sidecars, so it can't be derived — it's recorded via a new per-network **`SPLIT`** marker (owner-authorised
  grammar change). `to-ir` marks a network when a part is shared across statements; synthesis, on a SPLIT
  network, shares the maximal common leading sub-expressions (contacts + compound OR-merges) via a
  prefix-signature cache, gated on the marker so non-split networks stay duplicated (resolving the earlier
  over-share). All 10 networks of the fixture round-trip byte-exact (parity 15/15). **Residual:** *cross-depth*
  fan-out — a contact shared between a top-level position and one nested inside a NOT/OR (real edge-detect
  patterns; MotorStarter 113→104 contacts, not yet 94) — isn't reproduced; only top-level prefix sharing is.
  The SPLIT marker is still emitted for those blocks; the harder sharing is the next refinement.

  ### Original framing (superseded — kept for the investigation record)
  The remaining two are dominated by **contact fan-out**: TIA
  shares one contact's output across multiple consumers where synthesis rebuilds each Expr-tree occurrence
  separately (MotorStarter N13: five MOVEs to `IO.Telemetry` share the cascading `NF/Run/RunningFB`
  contacts — sidecar shows contact `uid 37` reused across moves). Logically identical, structurally
  different. **Investigated 2026-07-19; NOT yet cracked.** The sharing IS deterministic but the exact rule
  is intricate — attempts to reproduce it (top-level common-prefix sharing; then dest-scoped) each
  **over-shared** and broke the two perfected blocks (PerimeterSafetyAlarms keeps repeated same-tag
  contacts separate when they sit in different structural roles; FB_ShredderSequencer has *same-dest,
  same-leading-prefix* statements TIA still does NOT share, so "dest-scoped cascade" is necessary but not
  sufficient — likely also gated on a *strict prefix cascade* / adjacency, still unconfirmed). Reverted;
  the two blocks keep their sidecars (safe). **Cracking this needs systematic reverse-engineering**: a
  tool that extracts, per network across the whole real corpus, exactly which contacts are shared, and
  correlates with statement structure to derive the precise rule before implementing — approximations make
  it worse.
- Plus **OR-factoring** (move 3): the reducer distributes `prefix AND (X OR Y)` to `(prefix AND X) OR
  (prefix AND Y)`, losing the shared prefix — a `to-ir` (reducer) fix, separate from synthesis.

  ### Depth-aware sharing experiment + N1/N3/N12 diagnosis (2026-07-19)
  **The experiment that settled "annotate vs derive".** Threaded the SPLIT prefix cache *into* OR-branches
  and NOT-operands (`BuildCompoundStep` → `BuildChain(..., prefixCache)`), i.e. "share maximally within a
  split network", and measured MotorStarter per-network contacts vs its own stored sidecar:
  - **N13 telemetry 12→6 (exact)** and the `NotFedByContact` fixture both **closed** — the OR branches
    correctly tapped the `NF·Run·RF` cascade / the top-level↔inside-NOT contact.
  - **N4 9→8 and N7 13→12 — over-shared** (merged a contact TIA drew *separately*). In N13 TIA fanned the
    cascade contacts into the OR; in N4 (`(IO.InHand OR …) AND NOT IO.InHand`) TIA drew the OR's `IO.InHand`
    as its *own* contact. **Same logical shape, opposite drawing choice** → a single heuristic is wrong half
    the time → fan-out at depth is genuinely a free drawing choice, **not derivable — annotation required.**
  - All 562 converter + reference parity (15/15, incl. `HandAuthorSplitsMerges`) stayed green — the rule is
    only wrong where nothing guards it (MotorStarter isn't in an automated parity gate). **Reverted.** The
    threading *mechanism* is correct and reusable; it just needs to be **gated by a per-node annotation**
    (a "master" where the shared node is defined, "receive" where each other consumer taps it) rather than
    applied blindly. The per-network `SPLIT` flag is too coarse: it can't say "share the cascade, *don't*
    share the OR's InHand" within one network.

  **What N1/N3/N12 actually need (real fan-out extracted from the stored sidecar, `tmp/fanout.py`):**
  - **N1 — intra-*statement* fan-out (new class).** Real: `Contact IO.TryRunMotor.out → 2×Contact.in`,
    i.e. `P AND (X OR Y)` drawn with one shared `P`, *within a single coil's OR* (`IO.Run`). `DetectSplit`
    only inspects *cross-statement* UId reuse, so N1 **isn't even marked `SPLIT`**; synth builds `P` twice
    (17 vs 16). The annotation must attach at the **sub-expression node inside one statement**, not per-
    network/per-statement — this is the strongest argument for the user's master/receive-at-the-split-point
    over any coarser marker.
  - **N3 — NOT a split at all; FIXED 2026-07-19.** Zero fan-out in real. The only difference: real wires
    `RCOIL IO.HandStartSignal := GeneralDelayTimer.Q` **directly from the same-network TON's Q** (no
    contact); synth built an ordinary `GeneralDelayTimer.Q` contact (+1 → 11 vs 10). The **signal is coil
    type, not scope.** A corpus scan of every whole-condition timer-Q→coil wire settled it: **R/SCoil →
    direct wire, always** (MotorStarter N3, FB_MotorFwdRevSystem N1×2/N4 — 4/4, all LOCAL-instance); a
    **plain (Assign) Coil → direct only for a GLOBAL-instance timer** (TimerSample), else a contact
    (FB_ShredderSequencer N11, FB_PusherControl N5, MotorStarter N11/N12). This is why gating to global
    alone under-fired: it missed the latch-coil case. **Fix landed:** `Synthesize` keeps a second
    same-network **local**-timer map consulted only for Set/Reset coils; `BuildAssignment` wires a latch
    coil directly from a local timer's Q, a plain coil only from the global map. MotorStarter N3 now 10=10;
    FB_ShredderSequencer stays exact on all 15 networks; FB_MotorFwdRevSystem N4 (RCoil) exact. Guarded by
    `TimerScopeSynthesisTests.Synthesize_LatchCoilFedBySameNetworkLocalTimerQ_WiresDirectly` (RCOIL+SCOIL)
    plus the plain-Coil negative test. 562 converter + 31 golden green.
  - **N12 — multi-target fan-out, incl. non-contact consumers.** SPLIT sharing already reproduces
    `HrTotaliserTimer.Q → 2×Coil.in + Contact.in`. Residual: real also fans `RisingEdgeFlags[2] → Lt.pre +
    Eq.pre` (a contact into *two `Compare` boxes' chain-input ports*), which synth doesn't share (6 vs 4);
    plus a reduction question — readable shows one `= 4294967295` where real has a `Lt`+`Eq` pair.
    **Design consequence: "receive" points are any consumer position — `contact.in`, `coil.in`, `move.en`,
    `compare.pre` — not only contacts.**

  **Net for the annotation design:** (1) attach at the sub-expression node (intra-statement, N1);
  (2) receive markers go on *any* consumer port, not just contacts (N12/N13); (3) pull N3 out of the split
  scope entirely — it's a timer-wiring fix. N1/N3/N12 are three *different* residuals, only two of which are
  fan-out.

  **→ Decided: `docs/adr/adr-0006-fanout-annotation.md` (Accepted 2026-07-19).** Per-node `{split N}`/
  `{recv N}` markers replace the per-network `SPLIT` flag + the prefix-sharing heuristic (annotate-all,
  ordinal labels, retire the flag). This section is the investigation record behind that ADR; the ADR's
  phased rollout supersedes the "cross-depth residual" framing above.

**Consequences / next steps:**
- **The 4 blocks correctly keep their stored sidecars** (safe; guarded by `CommittedBlocksRoundTripTests` +
  `to-ir` keeping the sidecar by default). Not droppable until the gaps close.
- **The 14-block reference corpus under-samples real logic** — complete-corpus-green ≠ complete-synthesis.
  Add these blocks as harder fixtures (against the *own-sidecar* oracle, given cause #1), then isolate and
  fix each remaining gap until they round-trip. Only then can their sidecars drop.
- This remains the top synthesis priority — the gap between "the corpus derives" and "real blocks derive."

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

## THE D-6 scoped merge — SUPERSEDED 2026-07-18 by derive-always (ADR-0005)

**No longer needed.** Complete synthesis (parity 14/14 + live compile 10/10) let the sidecar be *derived*
rather than stored (ADR-0005, Accepted): `to-ir` omits the sidecar for a synthesizable block and `to-xml`
re-derives it, so a network edit just re-derives — D-6 cannot occur, and there are no "unchanged networks'
real sidecars" to preserve. The scoped-merge writeup below is kept as the historical record of the
problem it was meant to solve. (A block using a still-unsynthesizable construct keeps a stored sidecar and
would still hit D-6 if edited in place — the fix there is adding that construct to synthesis, guarded by
the parity harness, not the merge.)

### Historical — the scoped merge as the S7-on-real-blocks blocker (genval3, 2026-07-18)

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

## Gap D — array-index local members mis-scoped `GlobalVariable` — DONE 2026-07-19

**Fixed.** `ScopeFor` now strips a trailing `[…]` subscript before the local-name lookup, so
`RisingEdgeFlags[3]` scopes `LocalVariable`. Covered by `ArrayIndexScopeSynthesisTests`. Original writeup:

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

## Gap F — WordAnd (WAND) + the rest of DataHandling — DONE 2026-07-18 (DataHandling green → 14/14)

**Done — completes the reference corpus.** DataHandling needed five builders, all added together (the
block goes green only when all five synthesise): **WAND** (`BuildWordAndSidecar`, SrcType from the first
tag input, its literal mask typed to match — `16#89` is a Word, not the Int its digits suggest, via a
`constantTypeOverride` on `ResolveOperand`), **CALC** (`BuildCalcSidecar`, Equation verbatim + SrcType),
**T_SUB**/**T_CONV** (`BuildTSubSidecar`/`BuildTConvSidecar`, Version 1.2, T_CONV `EN := ENO` chains from
T_SUB via the same index-pairing as Mul→Convert), **MOVE_BLK_VARIANT** (`BuildMoveBlkVariantSidecar`,
Version 1.2, the first production with two output tags — Ret_Val + Dest). All types resolve through the
`TagTypeRegistry` (block-interface members). Removed from `RequireInScope`. Covered by
`DataHandlingSynthesisTests`. **Every reference block now derives a sidecar matching its real export.**

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

## Gap H — standalone `Not` vs negated contact — DONE 2026-07-18 (owner chose the SPEC change; BooleanExtras green)

**Resolved via the SPEC grammar change (owner's decision).** `Expr.Not` now carries a `Standalone` flag;
the readable grammar spells a standalone `Not` part as `NOT (X)` and a negated contact as `NOT A`
(`ir/SPEC.md` updated). Threaded through parser/serializer/reducer/synthesizer (the reducer sets the flag
from which real Part it read; synthesis routes `Standalone` → NotStep, bare → negated contact). The
committed `ir/reference/BooleanExtras.ir` was regenerated with the new grammar (`NOT (EnableCmd)`).
Covered by `StandaloneNotGrammarTests` (parse/round-trip/synthesise, both forms) + `NotTests` updated.
The investigation record + the three options considered are below.

### Investigation (2026-07-18) — why it was a genuine ambiguity

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

## Box-EN fan-out — RESOLVED 2026-07-19 (ADR-0006 phase 4); one build-order limitation open
The "Hours Run Counter" residual (MotorStarter N12, FB_MotorFwdRevSystem N13) was **not** a compare-structure
reducer bug (the readable is faithful — both the reset `MOVE`/`Eq` and increment `ADD`/`Lt` are correct). It
was **fan-out into a box**: the `ADD` shares the `Q AND NOT RisingEdgeFlags` prefix with the `MOVE`/coils, but
fan-out marking + synthesis excluded the box family. Fixed by extending both to the synthesizable box EN
chains (Mul/Add/Sub/Div, Convert, Swap, Abs, Calc, T_Sub, T_Conv). All four migration blocks now byte-exact.
**Still open — build-order alignment:** synthesis builds boxes *before* wands/calls (and abs before swap),
while the serializer/marking order is `… wands → calls → boxes` (abs after swap). So a fan-out node shared
**box↔wand/call** or **swap↔abs** would hit the registry-miss hard-error rather than reproduce. None occurs in
the corpus (N12/N13 share only coils/moves↔box, consistent in both orders). The robust fix is to reorder
`SidecarSynthesizer.Synthesize`'s box builds after wands/calls (and swap before abs) to match the serializer;
deferred because it changes cross-statement UId order and warrants a live-TIA `SynthesizerLiveCheck` re-verify.

## Where this is tracked
`AITODO.md` "Recently landed" → the wired-CALL bullet's two-follow-ups line points here; the parity
matrix (`tests/golden/synthesis-parity-matrix.md`) is the live scoreboard. Update both when a gap lands.
