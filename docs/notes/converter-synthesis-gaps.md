# Queued plan — two `--synthesize` gaps found in the genval2 (ShredderControlSystem) build

Both surfaced 2026-07-18 while building the reusable ShredderControlSystem subsystem (the wired-CALL feature
itself is done + TIA-proven — commit `e5bfeab`). Neither was hand-patched (hard rule 7); both are
recorded here with a workaround so nothing is blocked, queued for a later PC-side session. These are
`src/converter/` work — normal software rules, not `lad-coder`.

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
