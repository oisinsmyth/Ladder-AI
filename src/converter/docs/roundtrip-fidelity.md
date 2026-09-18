# converter — round-trip fidelity and the silent losses

The defect class this project cares about most: a change the round trip *preserves*, so the round-trip check cannot see it.

> *A round-trip check is blind to any error the round trip preserves.*

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

## `MemoryLayout` — optimized vs standard block access, carried through the round trip (2026-08-12)

**The measured defect.** A real standard-access global DB was round-tripped
`export → to-ir → to-xml`:

```
original export : <MemoryLayout>Standard</MemoryLayout>
regenerated xml : NO MemoryLayout element at all
drift-check     : *** MATCH ***
```

Re-importing that regenerated XML states **no opinion** about layout, so TIA applies the S7-1200
default — `Optimized` — and the DB becomes **invisible to classic S7comm** (not an error: the block
is simply absent, and it fails at the first *data* read). **Every check in the pipeline stayed
green**: `drift-check` was structurally blind because `Normalizer` had `MemoryLayout` on its
volatile-element list, import did not error, compile did not error. The first symptom was a runtime
Modbus status code. Background on the Openness side, including the re-import revert this pairs with:
`src/openness-cli/README.md`, `block-layout`.

**What it is.** The `<MemoryLayout>` element inside a block's own `<AttributeList>`, immediately
before `<Name>` in every real export. **Not DB-only** — it is backed by `PlcBlock.MemoryLayout` and
is present on every FC, FB and OB in the committed `simatic-ml/` corpus, so both `DbSourceParser`/
`DbSourceWriter` and `BlockSourceParser`/`BlockSourceWriter` carry it. The value set is closed and
confirmed (`Standard`, `Optimized` — the whole of `Siemens.Engineering.SW.Blocks.MemoryLayout`);
anything else is a hard error on both the XML and the IR side rather than a value passed blindly to
TIA. IR grammar: a `MEMORYLAYOUT <value>` line — `ir/SPEC.md`, `BLOCK` and `DB` file shapes.

**Absent in IR emits nothing, and that rule is not optional.** Every `.ir` in the repo predates this
and carries no layout; emitting a default for those would silently restate the layout of every DB in
the corpus — a broader silent corruption than the one being fixed. So: present in the IR, emit it;
absent, emit nothing, exactly as before. The defect closes because an `.ir` that *came from* an
export now carries the attribute.

**Order of the change: emit first, un-ignore second — landed in one commit.** `Normalizer` no longer
ignores the attribute, so a layout **difference** is now reported instead of silently matched. It
compares as an *optional assertion*: a difference between two documents that **both** declare a
layout is real; a document declaring none is stating no opinion and is not held to the other's
value. That second half is what keeps the change from turning the corpus red for a benign reason —
**measured**: comparing strictly instead reports 30 committed blocks as drifted (19 in
`test-project001`, 11 in `reference`) against a known-drift baseline of 6, because their `.ir`
predates the emit side while their exports carry `Optimized`. The comparison sharpens by itself as
blocks are re-derived from their exports.

**It cannot be derived — checked, not assumed.** The alternative considered was *computing* a
standard layout from member order and types (bools packing within a byte, a partly-used byte not
reused) rather than storing a flag. The evidence removes the question: **SimaticML carries no
per-member byte/bit offsets at all.** A real `WithDefaults` export of a standard-access DB has zero
occurrences of an offset or address, and across the 900 `<Member>` elements in the committed corpus
the only attributes that exist are `Name`, `Datatype`, `Remanence`, `Accessibility`, `Version`,
`Informative`. So there is no offset channel for TIA to infer a layout from on import, and none for
a derivation to be verified against — the CPU memory layout is computed by TIA and never serialized.
This element is the only carrier the file format has. A derivation would still be worth building for
a PC-side S7comm harness, which needs real absolute addresses; that belongs to the harness, needs
the even-byte alignment rule for `WORD` and larger, and must be grounded against offsets TIA
actually displays.

18 converter tests (`Converter.Tests/MemoryLayoutTests.cs`), fixture
`Fixtures/GlobalDbStandardMemoryLayout.xml` — the grounding export's exact XML shape with invented
DB/member names. Headline test: take the real `Standard` export, `to-ir`, `to-xml`, assert the
regenerated XML still says `Standard`. All 908 converter tests pass (up from 890); the offline
golden-harness suites (39, including `ExportDriftDetectorTests`) stay green.

## Fixed-shape instruction registry, unconnected ports, and two one-way-IR fixes (2026-08-12)

### `MB_SERVER` 5.3 — registered, and proven end to end (migrated from CLAUDE.md 2026-08-21)

**Instruction names are keyed on `(NAME, VERSION)`, and an unknown version is REFUSED.** TIA emits
three different spellings of the Modbus family; version is exactly what changes a port list, and the
port list is what the converter SUPPLIES, so accepting an unknown version means applying the WRONG
TEMPLATE — it converts, imports, and misbehaves on the controller. New instructions of this shape
are a TABLE ENTRY in `SimaticMl/FixedShapeInstructions.cs`, not five files edited in lockstep.

✅ **`MB_SERVER` 5.3 is REGISTERED and the IR path works** — `MbServer53` was added to
`FixedShapeInstructions.cs` on 2026-08-12.

Both the legacy `Modbus_*` entries and the real `MB_MASTER` / `MB_COMM_LOAD` names are kept
deliberately; migrating the legacy ones onto the registry is ruled **NOT NOW**
(`docs/notes/deferred-items.md` D-8). Revisit only on a real export that CONTRADICTS the retained
names — tidiness is not a trigger.

***And the `Array[..] of Struct` never applied to us at all:*** it belongs to the Siemens **sample**
block used to characterise the port list, not to anything we author. `MB_HOLD_REG` is an **area
pointer over marker memory** (`P#M1000.0 WORD n`), not a reference to an `Array[..] of Int` static,
so the structured interface simply never arises. Two statics suffice: an `MB_SERVER` instance and a
`TCON_IP_v4`.

Proven in `GenProject1` 2026-08-13 — imported, compiled (`errors=0`), and confirmed from **TIA's own
re-export** (`Part Name="MB_SERVER" Version="5.3"`, the pointer, `LocalPort 503`), never from an
exit code. The converter half was verified behaviourally too: the Release binary takes a real
`MB_SERVER` IR block to SimaticML, exit 0, part emitted.

🔴 **A converter limitation is NEVER by itself a reason a block cannot reach a project.** The
converter's registry governs **IR → SimaticML only**. `openness-cli export` / `import` produce and
consume SimaticML **straight from TIA and never touch the converter**. A stale note claiming
`MB_SERVER` was deliberately unregistered was once read as "it cannot be moved into a project", and
that stopped a rig deployment a step early. `MB_SERVER` is a `<Part>`, measured: an InOut port wires
as an ORDINARY SYMBOLIC `<Access>` in normal input order, so the `<Call>` / `Section="InOut"`
whitelist is IRRELEVANT to it.

**Unconnected ports, concretely:** `REQ := OPEN` for a deliberately unwired input, `DONE => OPEN`
for an output — versus **no argument at all** when the port is absent from `<Wires>` entirely.
`OPEN` is a reserved bare word (precedent: `TRUE` / `ENO`); a tag genuinely named `OPEN` on such a
port is a hard error naming the collision, never a silent mangle.

Four defects, found together on ONE real exported block — a genuine TIA V20 export of a live
S7-1200 (classic 1214C) Modbus TCP FC — each of which alone stopped it round-tripping. The fixture
`Converter.Tests/Fixtures/FixedShapeModbusTcpBlock.xml` **is** that export, structurally element
for element (415 lines in both), with four identifier lines replaced by invented ones per the data
boundary.

**1. The supported instruction spellings were the wrong ones.** `FlgNetParser.SupportedPartNames`
carried `Modbus_Master`/`Modbus_Comm_Load`; TIA emits `MB_MASTER` Version="2.2" and `MB_COMM_LOAD`
Version="2.1" — and, on a sibling FB, `MB_SERVER` Version="5.3". *Not one whitelist entry was a
name TIA actually produces here.* Rather than hand-write a third and fourth production, this added
**`Converter/SimaticMl/FixedShapeInstructions.cs`**: a `(Part Name, Version) → ordered port list
with direction` registry, driving one reducer (`GraphReducer.ReduceFixedShape`), one builder
(`FlgNetBuilder.BuildFixedShape`), one serializer block and one parser block. Adding the next
instruction of this shape is a table entry.

The registry exists because **section and datatype do not exist at the call site** — the network
XML states only a port NAME on `<NameCon>`. Wire order distinguishes read from write; it cannot
distinguish an input from an InOut, and says nothing about a port wired only to an `<OpenCon>`.
**Version participates in matching and an unknown version is refused** (2.1 vs 2.2 vs 5.3 across
three real families): applying one version's port template to another version's wiring would
convert, import, and misbehave on the controller. The older `Modbus_*` pair is **kept, not
replaced** — see `FixedShapeInstructions`' own doc comment for the live-import evidence that its
names are real, and why neither family aliases the other.

**2. An unconnected INPUT port could not be reduced.** `<OpenCon>` on an *output* already worked
(TON's `ET`, `Modbus_Comm_Load`'s `FLOW_CTRL`/`RTS_ON_DLY`/`RTS_OFF_DLY`); an unconnected input was
`NonReducibleNetworkException: REQ wire 36 for UId=22 has no IdentCon source`. The registry path
handles both directions uniformly at no extra cost, and makes the state **visible in the readable
IR** (`PORT := OPEN`, `DONE => OPEN`) rather than sidecar-only — a port an AI cannot see is a port
an AI cannot write back. Three states are distinguished: wired, `OPEN` (wired to `<OpenCon>`), and
absent from `<Wires>` entirely (no argument at all). An `OPEN` port names no tag, so it contributes
nothing to `tagstatus`/`preflight`; a tag genuinely named `OPEN` on such a port is a hard error.

**3. `MOVE_BLK_VARIANT` was IR the AI could read and never write back.** `to-ir` emitted
`constant P#DB99.DBX0.0 BYTE 2 = 21 Any`; `to-xml` on the converter's own output threw
`Malformed sidecar constant line` — the sidecar value was matched as `\S+` and a classic S7 area
pointer contains spaces. Fixed generally (greedy value against the anchored ` = <uid> <type>`
tail), not `Any`-specifically: `String`/`WString`/`DT`/`DTL`/`Pointer` values can all carry spaces,
and a value containing ` = ` survives too. Backward compatible with every sidecar in the repo.
Alongside it, `P#`-prefixed text now parses as a **literal** rather than an `Expr.TagRef` (as `T#`
already did), so an area pointer stops being reported as a proposed tag.

**4. A comparison's literal was typed by magnitude, not by the comparison's own type.** FI-55 fixed
the `SrcType` half and left the literal half, so `UInt` registers compared to constants emitted
nine `Int`/`DInt` literals against `SrcType="UInt"` boxes — the same mismatch TIA rejects. `SrcType`
is now resolved first and passed to both operands as `constantTypeOverride`, the rule
`MUL`/`ADD`/`CALC` operands already followed. **The committed corpus contains zero `UInt`/`Word`
comparisons**, which is why nothing had hit it; the guards are parameterised over
`Word`/`USInt`/`UDInt`/`SInt`/`LInt` too. FI-54's duration-literal carve-out is untouched.

`MB_SERVER` 5.3 is deliberately **not** in the registry yet: its port list is characterised, but the
block carrying it also needs `Array[…] of Struct` and doubly-nested structured interface members
that this converter does not model, so a template added now could not be exercised end to end.
*(Closed out the same day — see the next section.)*

**Confirm loop, offline half**: `to-ir → to-xml → converter compare` against the original export —
`EQUIVALENT` for each of the three networks individually and for the whole block; the IR text is
byte-identical across a second round trip. 968 converter tests pass (up from 929); the golden
harness stays green at 39. No Portal, no import, no compile, no download — that half is the
owner's, with a person present.

## MB_SERVER made expressible — and the silent losses found on the way (2026-08-12)

Owner ruling: **no IR the AI cannot change.** A block the converter cannot express is a block the AI
can never modify, so "author the `MB_SERVER` call by hand in TIA" was withdrawn. Grounded on a
genuine TIA V20 export of an S7-1200 (classic 1214C) Modbus TCP FB, 55,783 bytes, preserved
byte-exact and converted only in copies.

Five gaps were scoped. Two more turned up while closing them, **both silent**:

1. **`Array[1..10] of Struct` with inline nested members** — hard error. The direct-nested-`<Member>`
   gate accepted only the literal `Struct`. An array of an anonymous struct nests identically because
   it *is* the same struct, dimensioned. Widened to `Struct` **or** `Array[…] of Struct`, and to
   nothing else.
2. **Doubly-nested structured members** — hard error, and on the critical path: `CONNECT` must point
   at a `TCON_IP_v4`, which nests an `IP_V4`, which nests an `Array[1..4] of Byte`. A `<Sections>` at
   the nested position now has three dispositions — collapse a *quoted* named type (FI-56,
   unchanged), still refuse an anonymous `Struct` (its expansion is its only definition), and
   **recurse into and keep** an unquoted system structured type. Keeping matters: `IP_V4`'s expansion
   holds the remote IP address, so collapsing would trade one silent loss for another.
3. **The `MB_SERVER` 5.3 Part template** — registered, now that the block carrying it round-trips.
   Ports read off the export's own `<Wires>`. `MB_HOLD_REG`/`CONNECT` are genuinely InOut and are
   `Input` in the template, which is correct rather than a compromise: they are wired as ordinary
   symbolic `<Access>` operands in normal input order, and the whole document contains **zero**
   `<Parameter Section=…>` elements.
4. 🔴 **The multi-instance member writer dropped `Version` — silently.** It converted without error
   and came back as `<Member Name="…" Datatype="MB_SERVER" Accessibility="Public" />`: a
   **versionless** instance declaration, from a source stating `Version="5.3"`. Cause: a
   multi-instance was *read* as a "bare parameter", a shape with nowhere to put a `Version` or an
   `AttributeList`. It is not one — it is an ordinary Static member that merely never carries
   `Remanence`, and that single difference now lives on the write side (`WriteMember`'s
   `omitRemanence`). The `TON_TIME` member beside it kept its `Version="1.0"` only because it carries
   `Remanence` and never took that path.
5. 🔴 **`<Subelement>` array start values were dropped — silently, and this is the finding of the
   day.** The element name appeared **nowhere in `src/converter`**: not in code, not in tests, not in
   docs. No parse path read it, no write path emitted it, and no check objected, because members had
   no unknown-child guard at all. **The block reached `exit 0` with 182 start values gone** — its
   entire per-node configuration table: IP addresses, node numbers, register addresses, lengths, and
   the remote IP inside `RemoteAddress.ADDR`. *Converted without error* is not *converted correctly*.
   New IR grammar `[<index path>] = <value>` (`ir/SPEC.md`), supported on all three member shapes,
   plus a permanent unknown-child refusal so the next one is an error rather than an omission.
6. **Parameter-section members lost their `AttributeList`** — FI-59's remedy was wider than its
   evidence. The rejection it fixed named `Remanence` and only `Remanence`; the bare shape it reached
   for also drops the `AttributeList`, which nothing asked for. TIA's own export carries
   `Remanence` **and** a 3-attribute `AttributeList` on an FB Input parameter (and `FB TomraControlSystem`
   independently showed the same shape in 2026-07). So the *shape* now comes from the member's own
   `IsBareParameter` and `Remanence` is suppressed **by section** — which is FI-59's actual fix, and
   still protects hand-authored IR that never learned `BAREPARAM`.
7. **A multi-line network comment was a permanent hard error.** The format defined no `\n` escape, so
   the serializer refused one — correct about the risk (the document is split on `\n` before quoted
   strings are parsed) but leaving no way to represent a real comment. The export has one: three
   lines of engineer's notes. `to-ir` rejected the **whole block** over its documentation. `\n`/`\r`
   now escape; the value still occupies one physical line. The five copy-pasted `EscapeString`
   implementations and two of its inverse were collapsed into one `IrStringEscape` first, so the two
   halves cannot drift apart.

**Confirm loop, offline half.** `to-ir → to-xml → converter compare` against the preserved original:
**2 differences**, both deliberate and both pre-dating this work — the multi-instance's inline
expanded interface (the *callee's* declaration, discarded by design and re-emitted by TIA) and
`Remanence` on a parameter (which TIA exports but refuses at import). A whole-document element tally
confirms every other `n→0` is a `Normalizer` volatile: subelements **182 → 182**, and the `Member`
`157 → 63` / `Section` `43 → 13` / `Sections` `17 → 8` deltas are accounted for **exactly** by that
one discarded expansion (94 / 30 / 9). `to-ir → to-xml → to-ir` is byte-identical. 996 converter
tests pass (up from 968), golden harness green at 39, and `drift-check` over the committed corpus
reports the identical 6 pre-existing drifts before and after — no corpus regression.

**Also fixed here**: `compare`'s own `MEMORYLAYOUT` line printed *"NOT compared — neither document
declares one"* beside a value one document plainly declared. The skip was right, the stated reason
was false. It now names which side is silent.

## FI-75 — the quoted-UDT collapse was discarding per-use-site start values (2026-08-12)

🔴 **The fourth silent loss found on 2026-08-12, and the one that had been *seen* and left alone.**
Flagged during the `MB_SERVER` gap work and deliberately not touched then, on the belief that the
collapse was load-bearing for the committed corpus's fixed point. **It was not** — see the blast
radius below, which is zero.

**The collapse.** FI-56 (2026-08-08) taught the parser to accept TIA's expansion of a member whose
type is a named UDT — TIA renders `Claim : Array[1..8] of "UDT_SlotTicket"` as a nested
`<Sections>` on re-export, and refusing it had made whole blocks unreadable. It accepted the
expansion by **discarding** it, reasoning that *the IR already names the type, so TIA's rendering of
that type is redundant*. FI-64 (2026-08-09) applied the same rule to the third parse path.

**Why that reasoning is wrong.** *** THE VALUES INSIDE AN EXPANSION BELONG TO THE USE SITE, NOT TO
THE TYPE. *** A UDT declares the members; the referencing DB or FB declares what they *start at*.
Measured in the committed corpus:

| | `FTTime` | `ReverseDelay` | `ReverseIgnoreFT` |
|---|---|---|---|
| `MotorFwdRevIOSet` (the type) | *none* | *none* | *none* |
| `iDB_MotorFwdRevSystem_Shredder` (the use site) | **10.0** | **8.0** | **12.0** |

Three commissioning setpoints — a fail-to-run time and two reversal timings — that exist **nowhere
but the use site**. A collapse discards them and returns **exit 0**. Same class as the 182 dropped
`<Subelement>` values, and as `MemoryLayout`: *converted without error* is not *converted correctly*.

**What exactly was discarded, and in which shapes.** The collapse fired at the two NESTED positions
only — `ParseBareMember` (a quoted named type inside another member's expansion, i.e. doubly nested)
and `ParseTypeMember` (a quoted named-type member of a PLC data type). The **top-level**
`ParseMember` has always *kept* the expansion. So the codebase held two different dispositions for
the identical construct, and the collapse was an **asymmetry, not a principle**. Everything under
the collapsed `<Sections>` went: every nested member, every per-use-site `StartValue`, and every
`<Subelement>`.

**Blast radius: zero blocks, measured three ways.**

1. **Static scan of the corpus** — every quoted-UDT member carrying an expansion, with its nesting
   depth: 6 of 6 sit at **depth 0** (`FB_MotorFwdRevSystem`, `FB_PusherControl`, `FB_ShredderSequencer`
   and their three instance DBs, all the member named `IO`). **Zero** at a nested position, in either
   corpus, and none inside a `SW.Types` document. The collapse never fired on committed content —
   which is exactly why the corpus never lost a start value, and why the fixed point could not have
   depended on it.
2. **`drift-check` before and after** — identical at both baselines, same six names: **6 drifted /
   17 match / 3 skipped** on `test-project001`, **0 drifted / 15 match** on `reference`. (The three
   instance DBs drift for an unrelated pre-existing reason: the IR emits no `InOut` section, so
   `Section[3]` is `InOut` in TIA's export and `Static` in ours.)
3. **Whole-corpus IR re-derivation** — `to-ir` over all 38 committed exports, before and after:
   **0 files changed**, byte for byte.

**The fix.** Recurse and keep, at both nested positions — which is what the top-level path already
does, and what the doubly-nested `MB_SERVER` work built the machinery for. The disposition table at
the bare position drops from three entries to two: a bare `Struct` is **still a hard error** (an
anonymous structured member's `<Sections>` is its only definition, and there is no type name to write
it back out under), and *everything else* — quoted named type or unquoted system type — recurses.
`Array[…] of Struct` is deliberately left on the recursing side, where it has been since the
doubly-nested fix; narrowing it now would turn a shape that round-trips into a new hard error for no
gain.

**The write side needed a matching change, and skipping it would have been worse than the bug.**
`WriteTypeMember` emitted `NestedMembers` as **direct `<Member>` children** — the anonymous-Struct
shape. Keeping a named type's expansion on the read side while writing it in that shape would have
replaced a silent loss with a **silent corruption**. It now chooses the shape from the datatype, on
the same `IsAnonymousStructDatatype` test `WriteMember` has always used: anonymous → direct children,
named type → `<Sections><Section Name="None">` of bare members.

**Measured before and after at CLI level**, on a doubly-nested named type carrying a use-site start
value:

```
# BEFORE — to-ir exits 0
DB RealDbName
  MEMBERS
    Outer : "OuterType" SETPOINT
      Inner : "InnerType"                          <- Leaf : Real = 10.0 is GONE
# round trip: DIFFERS - 1 difference(s), the whole <Sections> ELEMENT-MISSING

# AFTER
    Outer : "OuterType" SETPOINT
      Inner : "InnerType"
        Leaf : Real = 10.0
# round trip: EQUIVALENT
```

11 tests on the read-back path (4 of them written to fail first, and they did), plus the
`DbConverterTests` doubly-nested case **inverted a second time** — it asserted the error before
FI-56, asserted the collapse after it, and now asserts the expansion is kept. 1010 converter tests
green (up from 996), golden harness green at 46.

## A literal is typed by the port it feeds, not by its own magnitude (2026-08-13)

Found on the rig. *** The converter emitted `<ConstantType>Int</ConstantType>` for EVERY hex literal,
regardless of magnitude or of the destination's type *** — so a 32-bit build stamp was 16 bits **by
construction** and every real stamp failed to import, including the worked example `16#A93F2C71`.

**Second instance of one class, so the narrow fix was replaced rather than duplicated.** The 2026-08-12
fix was *"a comparison's literal is typed by the comparison's own type, not by magnitude"*. This is the
same bug one site along. The general rule is therefore:

> **A literal's `ConstantType` is the declared type of the PORT it is written into. Magnitude is only
> the fallback where no declared type exists — and that fallback refuses rather than guesses.**

**Two independent causes, both closed.**

1. **The magnitude path never ran for a base-prefixed literal.** `long.TryParse("16#A93F2C71")` fails,
   so every `16#`/`2#`/`8#` literal fell straight through to the `Int` default — not a bad guess, no
   guess at all. **Widening the parse would not have fixed it:** 2,839,872,113 would then be typed
   `UDInt` into a `DWord` port, which TIA rejects by the same door. A bit string's **width is a
   declaration choice its digits cannot express**, so `InferLiteralConstantType` now returns *null* for
   a base-prefixed literal and the caller **hard-errors** naming the literal and the fix. (Precedent:
   FI-71 refusing to emit XML with unresolved member types.)
2. **Six operand sites never passed the port type at all** — including the CALL-argument site, where
   the callee's own `param.Type` was resolved **on the very next line** and used for the sidecar's
   `Type` while the literal beside it was typed by magnitude. Measured:
   `CALL FB_Reg(Stamp := 16#A93F2C71)` against `Stamp : DWord` emitted `Int`.

**The structural half of the fix, which is the part that stops a third instance:** `ResolveOperand`'s
`constantTypeOverride = null` became a **required** parameter, `portType`. An optional parameter is an
invitation, and six sites accepted it. A new operand site now cannot silently fall back to magnitude —
it must state its port type, or pass `null` and say why. Fixing six omissions would have left the
seventh to be written next year.

Sites and what types them now: CALL arg → the callee's `param.Type`; MOVE → the destination tag's
type; comparison → the compare's `SrcType`; WAND/CALC/MUL/ADD → the box's operation type;
CONVERT/ABS/SWAP/T_SUB/T_CONV → the box's own `SrcType` (these already refuse a non-tag operand, so
they emit nothing different today — they stop being an omission waiting for that guard to relax). The
one site the rule genuinely cannot cover is **MOVE_BLK_VARIANT**, stated rather than defaulted: `SRC`
is a `Variant` with no scalar type, and `COUNT`/`SRC_INDEX`/`DEST_INDEX` carry TIA port types that live
in no registry here and that this project has no grounded export to read off.

**`literal-fit` — the mechanical floor's missing check.** Asked which `converter review` rule should
have caught this, the honest answer is **none**: `docs/06-lad-conventions.md` has no rule about a
literal fitting its destination type, so no review rule failed — there was never one to fail, and
inventing a C-nnn from the tooling side is not this component's call. Pre-flight's stated job *is* the
known, recurring import/compile error classes, and this is one — measured, `MOVE(IN := 70000)` into an
`Int` member was reported **CLEAN** by pre-flight and is rejected by TIA. The new check flags a plain
decimal outside the destination's declared range, and a base-prefixed literal wider than the
destination's **bit width** (`16#A93F2C71` needs 32, `Int` holds 16 — wrong under any reading). It is
deliberately *not* a signed-range test on bit-string literals: whether TIA reinterprets `16#FFFF` as a
two's-complement `Int` is a question with no grounded answer here, and a check that guessed at it would
be one people learn to ignore. Zero findings across the whole 26-file `ir/test-project001` corpus.

### *** THE PROOF THAT SHOULD HAVE CAUGHT IT COULD NOT, AND WHY ***

The phase-2 lane verified its generated IR survives `to-xml` → `to-ir --no-sidecar` **byte-identically**,
and that proof passed — **because the wrong type round-trips faithfully.** All 1069 existing tests were
green on this too. *** A round-trip check is blind to any error the round trip preserves *** — the same
shape as the Normalizer being blind in exactly the way the converter was wrong. Every assertion in
`LiteralDestinationTypeSynthesisTests` is therefore against the **emitted type**, never a round trip.

Audited across the toolchain, and **measured, not assumed**:

| proof | blind to a *consistent* converter error? |
|---|---|
| `to-xml` → `to-ir --no-sidecar` byte-identical (`IrSelfStabilityTests`, `NoSidecarEquivalenceTests`) | **YES.** Both halves are converter code; a shared assumption cancels. This is the proof that passed. |
| `converter diff`, `ir-hash` | **YES**, structurally — `ConstantType` is sidecar, not readable IR. Fine for network invariance; proves nothing about emitted types. |
| `converter drift-check` | **Per file.** Not blind against a genuine TIA export; blind against a corpus entry the converter itself produced. |
| `converter compare` + `tools/confirm-roundtrip.ps1` | **NO — this would have caught it.** Verified live: two exports differing only in `ConstantType` return `VALUE-DIFFERS … first: DWord / second: Int` with the path. TIA's own export is on both ends. |
| golden harness `Normalizer.AreSemanticallyEquivalent` vs a real TIA export | **NO** — `ConstantType` is not on any ignore list (checked, then measured as above). |

The rule worth keeping: **a proof is only as strong as the most independent authority in its loop, and
the converter round trip has none.**

55 new tests (`LiteralDestinationTypeSynthesisTests`, `LiteralFitCheckTests`); **1124 green, up from
1069**. Negative-tested by reverting the two fix points: **11 went red**, and the four "not a blanket
widening" cases stayed green in both states, which is what distinguishes this fix from simply widening
everything.

### ✅ FIXED 2026-09-03 (FI-102) — an instance DB round-tripped from a TIA export could not be re-imported: `Remanence` on a multi-instance

🔴 **THE WORKAROUND BELOW IS RETIRED. Do not follow it.** "Do not import an instance DB" cost nothing
while nothing needed one; two conformance slots then needed a **dedicated** instance DB for the block
under test, which is not something TIA regenerates from the FB, and the workaround had no answer
(`docs/16-future-ideas.md` FI-102). The diagnosis in this section was correct and is kept below,
because it is what located the cause; what replaced the workaround is stated here.

**What replaced it.** `DbSourceWriter.Write` now takes an `InstanceDbTypeResolution` — the corpus an
instance DB's member types resolve against — and omits `Remanence` on a member whose datatype resolves
to a **block name**, exactly as `BlockSourceWriter` already does for a block. The classifier is
**`MemberExpansion.Classify` reused, not a second copy**: `MemberShape.MultiInstance` for a datatype
that resolves to a block, `NamedTypeOpened` for one that resolves to a PLC data type, with block names
read off each `.ir`'s `BLOCK <KIND> <Name>` header rather than from `TagTypeRegistry`'s FB index —
which drops every sidecar-carrying FB, i.e. precisely the file a re-exported corpus is made of (FI-92).
So the `--project` type resolution this section asked for is the plumbing that landed, and none of it
is new analysis.

`to-xml`, `drift-check` and `preflight` each build one from their own batch plus `--project`. A global
DB is never asked — it cannot hold an FB instance. `sanitize` (XML→XML de-identification, no project
argument, not an import path) is deliberately left on the corpus-free default and still re-emits the
attribute; that is the one remaining place this shape can be produced.

🔴 **WITH NO CORPUS THE CONVERSION REFUSES, and that is the deliberate half.** A member whose datatype
resolves to neither a block nor a type — which is what `to-xml` **with no `--project` and no sibling
file** looks like — throws `UnsupportedConstructException`, naming the DB, the member, the type, the
search scope and the missing flag. Both alternatives are worse in the way this repo keeps paying for:
emitting reproduces the defect and surfaces only at import, after convert/preflight/review have all
passed; omitting always would silently change a UDT-typed member's retention on a document that imports
and compiles clean. Same direction, and the same stated reason, as FI-71's `--allow-blind-types` gate.
**One behaviour change to know about:** a hand-authored instance DB with a flat quoted UDT member (no
inlined sub-members) that used to convert with no `--project` now refuses until one is given. Every
instance DB in `ir/test-project001` inlines its structured members, so none is affected.

⏳ **The import is UNVERIFIED here.** The converter cannot import; the emission is the whole of what
changed. Proven offline: the attribute is absent on an FB-typed static and present on a UDT-typed one
in the same DB, and every block and every DB in the committed corpus converts byte-identically with and
without the resolution (`Converter.Tests/InstanceDbMultiInstanceRemanenceTests.cs`, both sweeps carrying
their own denominators).

---

**The original diagnosis, kept. Measured 2026-08-21.** A TIA export of an instance DB, taken through
`to-ir` and back through `to-xml`, was refused at import:

```
iDB_<seq>_<inst>.<member>: The Openness import failed: The attribute 'Remanence' cannot be set.
import-all  SUMMARY: 14 imported, 4 failed, 0 rejected   exit 13
```

The member named is a **multi-instance static** — one whose datatype is a quoted FB name
(`TrendWin : "FB_RollingWindow"`). TIA will not accept `Remanence` on one, and the round trip puts
it there.

**Why `BlockSourceWriter`'s existing guard does not cover it.** That writer already omits `Remanence`
for multi-instances (`WriteMember(..., omitRemanence:)`), deriving the set of multi-instance names
**from the block's own CALL and fixed-shape instruction instances**. An instance DB has **no CALLs**,
so the derivation has nothing to work from and `DbSourceWriter` emits the attribute unguarded.

**Why the obvious fixes are wrong.** A quoted datatype is *not* enough on its own — in the same
section, `IO : "UDT_CellSequence" RETAIN` is a UDT and legitimately carries `Remanence`, while
`TrendWin : "FB_RollingWindow"` is an FB and must not. Telling them apart needs `--project` type
resolution plumbed into the DB writer. **And a name-prefix test (`FB_…`) is not acceptable** — this
repo already rules that out for reachability, for the same reason: anything can be renamed into a
prefix.

**The workaround — RETIRED 2026-09-03, and the sentence that retired it is "why it costs nothing
*today*".** *Do not import an instance DB.* TIA regenerates an
instance DB's contents from its FB, so importing the FB is sufficient — verified on the run that
found this: four instance-DB imports failed, and the new member was nevertheless present in all four
when read back out of the controller, with `compile-all` reporting 33 compiled / 0 errors and
`sanity-check OVERALL: HEALTHY`.

🔴 **The trap was that the corpus looked importable and was not.** `preflight`, `review` and `to-xml`
all passed; only the import refused. `import-all` fails closed (exit 13) and names the member, which is
what stopped it being silent — but a caller reading only "the FB imported" would not learn that the
instance DBs did not. **`preflight` and `to-xml` now both hold the corpus and both refuse**, so the
check that passes and the check that decides no longer disagree — which is the part of this worth
carrying forward.

## 🔴 A dotted path split on `.` — the whole class, not the one splitter (2026-08-27)

`Split('.')` on a tag path was safe for exactly as long as an array subscript could only be an
integer literal: an integer contains no dot. That stopped being true the same day the converter
began accepting a **variable** subscript, because a symbolic index *is* a dotted path.
`DB_Config.Profile[iDB_Unit_A.Cycle.ChosenIndex].Target` cut into
`["…Profile[iDB_Unit_A", "Sequence", "ChosenIndex]"]` — three pieces that name nothing.

`1a6eed0` repaired **one** splitter (`AccessNode.FromDottedPath`) with a private bracket-aware copy.
A live run then hit the others and got two false positives on wiring `tagstatus` confirms exists:

- `preflight` / `tagstatus`: `member path … does not resolve: no member 'Sequence'`
- `review`: `C-005 Name component 'ChosenIndex]' contains characters other than letters/digits/underscore`

**How it was isolated, and why the trigger is certain.** `preflight` on the pre-edit file: exit 0, 0
findings. The identical edit with the subscript written as a literal `[1]`, nothing else changed:
exit 0, 0 findings. So the **dotted index alone** was the trigger, not the edit.

**The fix is one shared primitive, `Converter.TagPath`** — per this repo's standing rule that a
second instance of a bug class earns the general fix rather than another special case (the same
ruling that retired `AccessNode`'s positional special-casing outright). It offers `Split`,
`Split(path, count)` (the bracket-aware generalization of `string.Split('.', count)`, which is what
keeps the `Clock_0.5Hz` system tags intact for the sanitizer), `IndexOfSeparator`,
`LastIndexOfSeparator`, `StripSubscripts` and the two component-level helpers. It lives in the root
`Converter` namespace, dependency-free, so `Review`, `TagStatus`, `CrossCheck` and `Ir` all reach it
by enclosing-namespace lookup — **no analysis layer takes a dependency on the SimaticML
serialization model just to learn where a dot is.** Nesting cannot occur (`IsSymbolicIndex` rejects a
bracket inside an index), so the depth counter is exact; an unbalanced bracket degrades to
whole-string rather than dropping text.

**The defect reached further than the two reported files.** `ProjectUsageGraph.StripSubscripts`
matched `\[\d+\]` under a comment asserting *"an index never contains a dot"* — so a symbolic
subscript **survived the strip**, and `OwnerOf`'s `root.IndexOf('.')` then cut at the dot *inside*
the brackets and produced the root `Buffer[iDB`. `OwnerOf` is the test that separates a real
cross-block conflict from an alias, so a block-local path silently reclassified as global.
`Sanitizer.SanitizeAccessNode`'s `Split('.', count)` merges excess dots into the LAST piece — right
for a literal-dot name, wrong for a mid-path subscript. Both are routed through `TagPath` now, along
with `TagTypeRegistry.Resolve`, the `instancepath` splits on both sides of the IR round trip, and
`Trace`/`SignalSet`/`UndrivenScan`/`Claims`/`InterfaceCheck`'s leaf and root extraction.

**C-005 got stricter, not looser.** A variable subscript is a tag path TIA resolves as one, emitted
as its own nested `<Access>` with one `<Component>` per segment — so its segments are names and
C-005 now judges them **on purpose**. Before the bracket-aware split they were judged *by accident*,
as bogus outer components, which is exactly why a valid one was accused. A literal subscript (`[3]`,
`[0,1]`, `[-1]`) is never charset-checked: those are integers, not names. Measured on a synthetic
fixture: the valid construct gives `preflight` exit 0 / `review` `C-005: checked, clean`; a stray `]`
outside any subscript and a hyphen inside one both still report, the second naming the subscript it
came from.
