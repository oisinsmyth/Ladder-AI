# Queued plan — `SidecarSynthesizer` gaps (found across the genval2/genval3 builds)

All surfaced 2026-07-18 across the gen-block-* validations (the wired-CALL feature itself is done +
TIA-proven — commit `e5bfeab`). None hand-patched (hard rule 7); all recorded here with their workaround
(or lack of one) so the finding isn't lost, queued for a later PC-side session. `src/converter/` work —
normal software rules, not `lad-coder`.

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

## Gap C — synthesis is TON-only (no TONR/TOF)

**Symptom.** `to-xml --synthesize` hard-errors on a `TONR`/`TOF` ("TON is the only timer instruction sidecar
synthesis supports … found 'Tonr'"). Real equipment FBs use TONR (retentive hours totalisers) routinely.
Blocks the whole file under the D-6 workaround even when the TONR network is unchanged. **Fix:** extend
`BuildTimerSidecar` to mint TONR (its `R` reset port) and TOF, mirroring the read side's `TimerKind`
handling. (Also unblocks the genval1/genval2 REQ-032-class retentive-timer requests directly.)

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

## Gap B — Word→Int CONVERT is mis-typed (bigger; needs a tag-type symbol table)

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

## Where this is tracked
`AITODO.md` "Recently landed" → the wired-CALL bullet's two-follow-ups line points here. Update both
when either gap lands.
