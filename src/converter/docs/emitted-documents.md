# converter — emitted documents

Machine-readable artifacts the harness gates consume. All five share one framing: **derived, never declared.** A document that was asserted rather than computed is not evidence.

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

## `conflict-graph` — the submission-scoped emission the harness gate consumes (2026-08-14)

```
converter conflict-graph --project <ir-dir> (--submission <file> | --signals <file>) [--json] [--allow-unresolved]
```

**Not `cross-check` with a filter.** `cross-check` emits whole-project **fact tables keyed on a
storage path**; `Harness.Results.SubmissionGate` gates 8/8c consume **edges between blocks** carrying
a provenance and a signal class. Those are not the same shape — an instruction to bridge them was
withdrawn as wrong, and the lane that received it correctly supplied nothing rather than reshaping
one into the other. The emitted `conflictEdges` matches `ConflictEdgeDocument` field for field
(`blockA`/`blockB`/`provenance`/`signal`/`class`), **read off the consumer** rather than written and
handed over for the harness to accept.

### *** An absent graph and an empty one are different documents ***

`conflictEdges: []` is the **positive claim that the graph ran and found nothing**; a missing key is
`NOT CHECKED` and gates. So when the graph did not run the key is **omitted** — not `[]`, and **not
`null`**, because a null would let a lenient deserializer round it to the empty list and *restore the
false claim one layer down*. Three states withhold it, each printing its reason:

| state | why the key is withheld |
|---|---|
| **partial corpus** (any project file unparseable) | an unread file can hold the second writer that makes a signal a conflict |
| **no signals supplied** | nothing was scoped and nothing was examined (FI-44) |
| **signals that did not resolve** to exactly one storage path | an edge list over a scope nobody looked at is empty for a reason that has nothing to do with conflicts |

`--allow-unresolved` is the named escape for the third, never the default, and the gap is still
reported in full beside the edges it does emit.

### Only `MultiWriter`, and that is a refusal rather than an omission

`CallGraph` **is** derivable — the graph records every CALL — but a call edge is about no signal, so
it could only carry an `Unstated` class, and the consumer's `ProvenanceComplete` is **all-or-nothing**:
*** one such edge would turn gate 8c to `NOT CHECKED` for the entire submission. *** A helpful-looking
extra edge would silently disable the report it was added beside. `computedConflicts` is never
emitted for the same reason (a bare name carries `Unstated` provenance); gate 8's packing set derives
from the edges.

### Signal class, derived and never declared

From the same reserved 9000–9999 band `converter review`'s harness scope uses, read off each **writing
block's own number**. Any **plant** writer ⇒ `Deliverable` (the multi-writer ships); **all harness** ⇒
`HarnessInstrumentation`; anything **unclassifiable** ⇒ `Unstated`, which fails the gate closed. Exit
**3** names that last case, because at the gate it appears as a flat `NOT CHECKED` for the whole
submission with nothing naming the cause — and the operator who can fix it is the one running this.

### Ambiguity is refused, not guessed

A signal name matching **more than one distinct storage** is `AMBIGUOUS` with both candidates named —
*picking a candidate is exactly the aliasing above, wearing a different hat.* Instance aliases of one
storage are collapsed first, so a determinate signal is never falsely refused (which is what
`instanceAliases` buys). Measured: `IO.Step` is refused, naming `FB_PusherControl.IO.Step` and
`FB_ShredderSequencer.IO.Step`.

**Exit codes**: 0 computed (an empty list is the *earned* claim) · 1 usage · 2 **NOT COMPUTED, key
withheld** · 3 emitted but an edge is unprovenanced.

Every edge is derived from the **corrected** storage grouping (`StorageGroups`, one producer for both
consumers), so the fictitious cross-block multi-writers above are **structurally incapable** of
becoming edges: a block-local storage has all its writers in one block, and one block is not a
conflict.

### 🔴 `--submission` reads `map.storage` — the declared join, which it did not until 2026-08-17

***A SUBMISSION SPEAKS THE SPECIFICATION'S VOCABULARY BY DESIGN*** (D8: agents cite tag names, never
registers), and this tool fed those names straight into a resolver expecting **storage paths**.
**Measured on a real submission: 70 of 70 unresolved, and the graph correctly refused** — over a
question it had never actually asked. ***The field carrying the join already existed in the same
document and was read by nobody.*** Fifth instance in this codebase of one seam: a slot id, a
vector-target prefix, an observable vocabulary, a completion signal and `ResultRegisterOf`'s tag-vs-
cited-name have each failed the same way, and *this tool was built after the seam was diagnosed.*

Contract §2.7's shape, read exactly as `Harness.Gate.MapDocument` reads it — **object form only**,
because accepting a shape the gate ignores would let an author write a map that this tool honours and
the gate does not:

```
map
  storage      signal -> { owner?, path }   -- WHERE it lives. Two keys: an emitted string is not a schema
  harnessOnly  [ signal ]                   -- a POSITIVE claim: occupies NO PLC storage
```

**Every resolution says WHICH JOIN carried it**, on its own line and in the `JOINS:` breakdown —
*a resolution that cannot be explained is what made this invisible.*

| join | means |
|---|---|
| `DeclaredStorage` | `map.storage` named the (owner, path) and it matched project storage. **The only join the contract endorses** |
| `DeclaredHarnessOnly` | declared to occupy no PLC storage. No edge is possible and **that is a computed fact** — it does NOT count against the scope |
| `ProjectPathMatch` | the document declares **no map at all** (or the name came from `--signals`), so the cited name was matched against the project's own paths. Weaker, includes a leaf match, and says so every time |
| `NotDeclared` | the document declares a map and this signal is in neither half. **Unresolved, and no name-shape fallback is tried** |
| `ContradictoryDeclaration` | in both halves, declared twice differently, or a malformed entry. **Refused — and `--allow-unresolved` does not reach it**, because that flag accepts names nobody looked at, not a document that answers one question twice |

***THE REFUSAL IS NOT WEAKENED, WHICH IS THE POINT.*** Once a map is declared, a signal absent from it
stays `Unresolved` **even when its name would have matched something** — a map with one hole in it is
repaired by filling the hole. And a submission that declares no map behaves exactly as before, so the
gate does not fire outside its scope.

### 🔴 A reference is not a location — which placement a name reaches (2026-08-17, second pass)

First real use of the declared join produced two more cases, **both resolver gaps rather than bad
declarations**:

| symptom | cause | ruling |
|---|---|---|
| a **fully-qualified instance path** came back `AMBIGUOUS` over every instance of its FB | the first fix walked a **transitive closure** over *"these two spellings name one storage"* — **and that relation is not transitive.** `FB_X\|IO.Cmd` names the same storage as `iDB_A.IO.Cmd` *and* as `iDB_B.IO.Cmd`; `iDB_A.IO.Cmd` is **not** `iDB_B.IO.Cmd` | **gap.** The bound was right and the pooling should never have happened — *a declaration that names the placement has already answered the question* |
| a **nested multi-instance path** (`<outerInstance>.<innerStatic>.<member>`) resolved to nothing | an FB placed as a STATIC of another FB has real per-instance state and **no DB of its own**. `ProjectUsageGraph` has resolved these to a fixpoint since **FI-50**, nested ones included, and the resolver never asked | **gap.** Same lesson `undriven-scan` learned in FI-50, one tool later |

Both close with one change of model: **the equivalence closure became a CONTAINMENT relation.**

> **A group is not a location. It is a REFERENCE at some level of qualification, and what it COVERS is
> the answer.** A *global* reference covers itself. A *block-local* reference — an FB addressing its own
> member as a bare path — covers **one location per PLACEMENT of that FB**, instance DBs and
> multi-instances alike, because the FB's write executes once per placement and lands in each one's own
> memory. An FB with **no** placement covers a single declaration-site location (FI-44: a block written
> before its caller still has storage).

Resolution then works in locations: **one** location resolves, with the writers of every reference
reaching it unioned; **several** are refused naming each. *Sibling placements can no longer pool, and
that is now a property of the model rather than a bound bolted on after it.*

**The refusals are kept, and one is sharper.** An **owner-qualified** declaration names a member of the
*class*, so on a multi-placement FB it names N locations and is still `AMBIGUOUS` — now naming every
placement, with the repair in the message. A declaration naming an instance that does not exist, or a
real instance plus an unknown member, stays `UNRESOLVED` and is **never rounded to a sibling that
does**.

**So a per-instance test CAN resolve a shared member name** — the qualifier selects — provided the
declaration is the fully-qualified placement path. That is the whole of what changed for a
multi-instance plant.

The reported path is now the **location**, with the reaching references named beside it on every
resolution: *the location is computed and the references are the code, so a reader can check the
answer instead of taking it.*

### The writer set is UNIONED across every spelling of one storage

An FB writing its own `IO.Alarm` and a caller writing `iDB_X.IO.Alarm` are **one location under two
spellings**, arriving as two groups because each is keyed on how it was written. Resolving to one of
them reported **only that one's writers** — so a member the FB drives internally and a caller also
drives read as *single-writer*, and the conflict was invisible. The equivalence class is now walked
transitively and the writers unioned, with the pooling stated in the reason line.

**Bounded**: an FB with **two** instance DBs has an internal write landing in both, so a member reached
through more than one instance is `AMBIGUOUS` naming them — pooling there would invent a conflict
between blocks that never share a location.

### ⚠️ The second measured failure mode, and what it cost

Supplying a slot's storage tags **directly** still resolved **0 of 68**, because the corpus references
those members only **from inside the owning block** — never through the instance path the harness uses.
That is now repaired by an **identity** join (the instance DB's own declared members say which
`iDB.<suffix>` names the FB's member), not by a name shape. Verified on the committed corpus:
`iDB_HxBoolEcho.EchoResponse` read `UNRESOLVED` before and `RESOLVED -> FB_HxBoolEcho.EchoResponse`
after.

### One join site, and a walk that fails when a second appears

`SignalStorageResolver` is the only type in the assembly that turns a cited name into a location.
***A SHARED HELPER IS NECESSARY AND NOT SUFFICIENT*** — `Harness.Map.MirroredSignal.JoinKey` carries
the comment *"one definition, used by every path that joins the two documents"* and **the observe path
was not one of them**. So `JoinSiteWalkTests` walks the compiled assembly and goes red when a type
outside a declared allowlist reaches `StorageGroup`, with a denominator (bodies examined > 0) and a
live negative control (the predicate is a parameter, retargeted at a type used everywhere). It is a
fence around one gate: a join built directly on `ProjectUsageGraph.Usages`, or written in another
assembly, is outside it.

**48 tests** (15 `ConflictGraphTests` + 19 `ConflictGraphDeclaredJoinTests` + 10
`ConflictGraphInstanceScopeTests` + 4 `JoinSiteWalkTests`). **Mutation-tested 2026-08-17 — every figure
below was RUN, not predicted.** Baselines differ because the suite grew during the work: unmarked rows
were measured at 1,377, † at 1,379, ‡ at 1,389 (the placement pass).

| mutation | red |
|---|---|
| ignore the declared join (always take the project-path match) | **9** |
| fall back to a name match for a signal the map does not name | 1 |
| drop the instance-alias arm from the project-path match | 1 |
| drop the instance-alias arm from the declared match | 1 |
| keep one spelling's writers instead of the union | **3** |
| let `--allow-unresolved` cover a contradictory declaration | **3** |
| treat a malformed `map` entry as a skip rather than a rejection | 1 |
| pool a member reached through two instance DBs instead of refusing | 1 |
| label a project-path match as `DeclaredStorage` | **3** |
| emit `conflictEdges` unconditionally | **3** |
| never declare ambiguity | 1 |
| count only "missing from a map that exists", so a no-map submission is told nothing † | 1 |
| **add a second name-to-storage join site to the assembly** | **1** — `JoinSiteWalkTests` |
| ‡ a qualified instance path stops selecting and fans back out to every placement | **6** |
| ‡ multi-instances dropped from the placement index (instance DBs only) | 2 |
| ‡ a block-local reference covers only ONE placement | **7** |
| ‡ the name match loses its location arm | 1 |
| ‡ an owner-qualified declaration stops expanding to placements | 3 |
| ‡ nothing may ever resolve to one location (the resolution path itself) | **22** |

**The over-fire converse, run as deliberately as the refusals**: a new type in the assembly that
touches no storage grouping leaves all 1,377 green — *a guard that fires on ordinary code is noise,
and noise gets switched off.* A submission declaring no map still resolves by project path (2 red if
that path is removed). And an operator `--signals` name that resolves to nothing is **not** reported
as a missing declaration — *an unresolved path there is a wrong path, and sending its user to write a
`map` would be the wrong repair.*

**The strongest converse available, and it is on real data.** The placement pass was re-run against the
submission that produced the original finding, and each signal's outcome compared with the previous
run's: **0 signals lost** — nothing that resolved before stopped resolving — **10 newly resolved**, and
all **58** already-resolved signals changed only in the path *reported*, from the FB-internal reference
to the placement's location. That equivalence was **checked mechanically, not by eye**: for all 58, the
previously-reported path is still one of the *reaching references* named in the new reason.

⚠️ **What none of this establishes.** The mutation figures and every fixture are from the converter's
own tests on the **committed** corpus. The `0 of 68` figure in the section above is quoted from the
report that raised it, not re-taken. And **an empty edge list on that submission was checked rather
than assumed**: the corpus holds 140 genuine cross-block multi-writers, and **none of them is at a
location the submission declares** — they are all on deployed instances while the submission's scope is
a dedicated test instance. *That is why the empty list is earned; without that check it would be an
empty list over a scope that had just been narrowed.*

## `served-area` — the Modbus window, derived from the block that serves it (2026-08-23, workbench Y2)

```
converter served-area --project <ir-dir-or-file>... [--json]
```

**The defect.** A harness binding's `declaredRegisters` was **authored by hand, per lane, and
required**. It flows into `MirrorGeometry.ForCpu1214C`, into `MapAllocator`, into
`RegisterMap.MapHash`'s canonical form (`declared=`) and therefore into the build stamp — so the
stamp *does* hash a declared width. **The gap is narrower than it first looks, and that is what makes
it worth closing:** what the stamp is blind to is not the number, it is **whether the number is true
of the program**. `gen/test-project001/hopper-blockage-alarm/harness-binding.json` carries a
`_declaredRegistersNote` saying in its own words that 37 was *read from* the comms block — by a
person, once.

**The truth lives in the program, in two places that can silently disagree.**

```
ir/test-project001/FB_Comms_ModbusServer.ir:31
  MB_SERVER(MbServer, EN := TRUE, ..., MB_HOLD_REG := P#M1000.0 WORD 37, ...)
ir/test-project001/FB_Comms_ModbusServer.ir:39   (SIDECAR)
  constant P#M1000.0 WORD 37 = 22 Any
```

`to-xml` rebuilds the operand **from the sidecar**, so a readable line that drifted is invisible to
every other check and surfaces only when the controller serves a different area than the map was
allocated against. Both are read here, joined by the UId the fixed-shape port cites, and a
disagreement is a refusal **naming both lines**.

**Every uncertainty refuses rather than approximates**, because the direction of error is not
symmetric: a width derived NARROW costs a refusal on a map that would have fitted, while a width
derived WIDE lets a map that overflows the real Modbus window allocate cleanly and fail on the wire
as a device fault. Refused by name: a unit that is not `WORD`, an area outside marker memory, a bit
offset inside the byte, a missing sidecar, a file that would not parse, and a corpus with a **second**
`MB_SERVER` call — guessing which one serves the mirror would invent the answer.

**Exit codes.** `0` derived · `1` refused · `2` **NOT DERIVED**, and 2 is never a pass.

**The denominator, printed on every run** — derived, refused or not:

```
served area: base 1000, 37 register(s), derived from ir/test-project001\FB_Comms_ModbusServer.ir:31
                                              + sidecar ir/test-project001\FB_Comms_ModbusServer.ir:39
NOT DERIVED — 10 block(s) in 15 file(s) scanned, no MB_SERVER call
```

`--project` is **repeatable** and takes a directory or a single `.ir` file, because a batch's corpus
is the union of several lanes' program paths and `harness-batch plan` cannot materialise that union
before it needs the answer.

**Consumer.** `harness-batch plan --converter <exe>` and `harness-batch run --yes --converter <exe>`
run this over the lanes' program paths and refuse a `declaredRegisters` / `baseByte` the program does
not serve, naming both numbers and both sources. Absent `--converter` the plan still runs and states
that the width was **DECLARED, not derived**.

🔴 **What it cannot possibly see: whether the block it read is the block on the controller.** It
reads the program corpus, never the CPU. The mirror's widening to 1024 registers was established by
probing the device from both sides and nothing here substitutes for that. A corpus stale with respect
to the rig derives a confident, agreed, *wrong* number and is indistinguishable from a fresh one. The
claim bought is strictly smaller and the tool prints it on every run: **a binding can no longer
disagree with the program that was staged.**

## `neighbours` — the neighbour list, derived from the program (2026-08-23, workbench Y1)

```
converter neighbours --project <ir-dir-or-file>... --base <%M byte> (--registers <n> | --bytes <n>) [--json]
```

**The defect, measured live on a running controller.** A generated mirror and a hand-authored virtual
panel both claimed registers 256–323 of one `%M` area — **53 tags overwritten bit for bit every
scan**, among them the panel's master enable. Nothing caught it, and the reason is not "a check was
missing": the allocator bounds the mirror against the *declared* area and `RegisterMap` proves the
mirror's regions disjoint *from each other*. **Both ran. Both passed. Both examined something real
that was not the thing at risk.** `ReservedRegion` closed the hole and its author wrote the limit into
the binding document where a reader meets it — *"Until something DERIVES the neighbour list from the
deployed program, this is a place to put the knowledge rather than a way to obtain it."*
(`src/harness/Harness.Gate/BindingDocument.cs:63-66`). **This verb obtains it.**

**What it returns.** Every `%M` claim overlapping the given area, from two sources:

- **tag-table entries** with an absolute `%M` address — `%M1009.0`, `%MB…`, `%MW…`, `%MD…`;
- **every `P#M…` area-pointer literal** in any object's body.

Each claim carries **the object that declares it** as its owner label, plus `file:line`. That is not
decoration: *"a refusal that cannot say WHOSE space was hit sends the reader looking in the wrong
place"* (`ReservedRegion.cs:31-35`) — to the mirror, which is the one place the problem is not.

**A NEW VERB, deliberately not an extension of `cross-check`.** On the real corpus `cross-check`
emits **351 multi-writer facts and 250 dead-member facts**, and
`docs/notes/preflight-interpreter-classification.md` §6 is an argument about signal-to-noise. A
refusal-critical fact does not go in that stream.

**Bytes, not registers.** Spans are emitted as `%M` byte addresses and the consumer converts them
through its own `MirrorGeometry.ReservingBytes` (already derived, already rounding outward). Doing
that arithmetic twice would give the two halves of one check two chances to disagree.

**The area's own declaration is separated from its occupants**, derived from the span and never from a
name: a claim covering the area *exactly* is the window stating itself — the `MB_SERVER`
`MB_HOLD_REG` pointer — and returning it as a neighbour would make every map refuse against its own
area. A claim that is only *part* of the given area is a genuine claim and is reported as one.

**Exit codes.** `0` derived (a `[]` list under `derived: true` is the **earned zero**) · `1` refused ·
`2` **NOT DERIVED**, and 2 is never a pass.

**The denominator and the exclusions, printed on every run** — derived, refused or not:

```
neighbours: 26 region(s) derived from 2 tag table(s) + 18 block(s) + 23 other object(s) in 43 file(s); 0 file(s) unparseable; area %M1000..%M1073 (base 1000, 37 register(s))
excluded: 3 %M claim(s) below base 1000, 0 at or above %M1074; 0 area pointer(s) outside marker memory; 1 declaration(s) of the area itself. 0 claim(s) bounded by their address alone (a data type this verb does not know).

NOT DERIVED — 1 tag table(s) + 0 block(s) + 0 other object(s) in 2 file(s) scanned; 1 of 2 file(s) would not parse, so the corpus is PARTIAL: …\FB_Broken.ir: would not parse (…); area …
NOT DERIVED — 0 tag table(s) + 0 block(s) + 0 other object(s) in 0 file(s) scanned; NOTHING WAS EXAMINED, so '0 neighbours' would be a claim about nothing rather than about this area; area …
```

**Three outcomes that must never render alike, and do not:** a real corpus with no occupant (exit 0,
the zero line printed with what it read), a corpus that was empty (exit 2), and a corpus one of whose
files would not parse (exit 2, **naming the file**). An unread file can declare the very claim the
list is meant to contain, so **no list is emitted rather than a short one** — the shape
`Reachability.cs`'s silent `continue` already cost this project once.

**Every uncertainty refuses rather than approximates.** A `%M` address form whose width cannot be read
(`%MX1512`), an area-pointer unit that converts to no width, a `P#` this scan cannot read at all, and
a tag whose declared type disagrees with its address width (`Real @ %MW1512`) are all **refusals
naming the object** — a claim that is seen and not measured, dropped quietly, is what turns a short
occupancy list into one that looks complete. The one exception is sound rather than lenient: an
unboundable claim whose *start* is at or above the area top cannot reach the area, because occupancy
runs upward, so it is excluded and counted.

**Extraction is over the file's text, after a successful parse has gated it — a choice, not a
shortcut.** `IrNetwork` carries about twenty statement kinds and grows; a structural walk that forgot
one would drop a real claim and report a short list as clean. The parse still runs and still gates;
what it does not do is decide which shapes are worth looking inside. Quoted text (titles, block and
network comments) is stripped first, because a pointer *described in prose* is not a pointer and an
over-broad derivation refuses maps that are fine.

`--project` is **repeatable** and takes a directory or a single `.ir` file, for the same reason
`served-area`'s is: a batch's corpus is the union of several lanes' program paths.

**The `P#` syntax reader is shared** with `served-area` (`Converter/Ir/AreaPointer.cs`, extracted
here). Syntax there, policy in each caller — the two legitimately disagree about what is acceptable,
and two hand-rolled readers of one notation is how two checks come to disagree about what a program
says.

🔴 **What it cannot possibly see.** Printed on every run, including the derived one:

1. **An occupant that reaches `%M` without DECLARING it** — an indirect access, a runtime-computed
   pointer, an offset arrived at by arithmetic. The derivation is over **declarations in the IR, not
   over execution**, and no amount of scanning changes that.
2. Anything outside the corpus it was handed.
3. Whether that corpus is the program on the controller. It reads files, never the CPU.

A zero here means *"nothing in the corpus I read declared a claim"*, **never** *"the area is free"*.

⚠️ **The committed suite's positive fixture for a FOREIGN occupant is invented, and a green suite is
not evidence that the derivation reproduces the real collision.** `ir/test-project001` has 26 `%M`
tags inside the served area and every one of them belongs to `HarnessMirror` — the mirror's own. The
real evidence is a re-run against the live corpus, in the job folder, never committed: it must
reproduce the 256–323 band and its 53 tags. Fewer, or a different band, stops the claim. (This also
corrects the Y1 plan's prerequisite row, which read the reference project as having no `%M` tag in the
area on the strength of `DefaultTagTable.ir` alone; `HarnessMirror.ir` is a second tag table.)

## `signal-set` — a block's signal set as one machine-readable document (2026-08-27)

```
converter signal-set --project <ir-dir> --block <Name>
                     [--origin interface|external|any] [--type <T>]
                     [--direction read|written|both|unused|any] [--json]
```

**What it is for.** The harness binding document is hand-typed — **297 lines for a single slot of 20
signals** — and roughly half of every entry is a restatement of what the block's own IR already says:
the member's type, whether it is RETAIN, its start value, whether the block reads it or writes it, and
who else touches it. Nothing emitted that half, so it was transcribed, and a transcription is the one
step in this pipeline with no mechanical check behind it. This emits it.

**It is not a check and states no verdict.** It never says a signal is bindable, safe to force, or
missing. Facts only.

### What the set contains

Two halves, counted separately (one total would hide which half is empty):

| half | what | `origin` |
|---|---|---|
| `interface` | every leaf of the block's OWN declared interface | `interface` |
| `referenced` | every signal the block references and does not declare | `globaldb` · `tagtable` · `instancedb` · `undeclared` |

`undeclared` is a path the block references that **nothing the inventory walked declares** — a raw
address, or a declaration in a file this export does not contain. It is **emitted and labelled, never
dropped**: a generator handed a silently-shortened signal set produces a binding that looks complete,
and the missing entries are exactly the ones nothing else will mention. Its `type` is `null`, because
nothing states one and naming one anyway would be an invented fact.

### 🔴 `direction` is relative to the BLOCK; `writers`/`readers` are project-wide

That is the distinction a binding is built on: a signal the block **reads** is one the harness must
**stimulate**; one it **writes** is one the harness **observes**. Getting them the wrong way round
produces a binding that drives an output and watches an input. The site lists are project-wide on
purpose — a harness needs to know who is *already* driving the signal it is about to contend with.

`direction` is **COMPUTED from the usage graph**, never read off the interface section: under C-132
the caller-facing members live under STATIC inside interface-UDT structs while INPUT/OUTPUT carry
unrelated data-link words, so a section filter answers the wrong question on the real corpus. This is
the same computation `candidate-scan`'s `MemberRole` performs, over the same join
(`SignalInventory` × `ProjectUsageGraph`) — **deliberately reused rather than re-derived**, so the two
tools cannot give different answers to "does this block write this member". `cross-check` and
`undriven-scan` contradicting each other over one corpus is what made a 60%-false check findable, and
the cost of that lesson is not worth paying twice.

Two joins it inherits from that repair, both load-bearing:

- **An interface member is addressed two ways.** Bare and local from inside the block (`IO.Step`),
  absolute on the placement from everywhere else (`iDB_Rack_RackA.IO.Step`). Both are resolved; the
  **bare form is restricted to the subject block**, because `_usages` is keyed verbatim and three FBs
  each declaring their own `IO.Step` land on one key — the false-multi-writer defect of 2026-08-14.
- **The placement is the FLOOR on the ancestor walk.** `CALL FB(iDB, …)` records a write at the bare
  instance path, an ancestor of every member in it; admitting it marks the whole interface written.
  That same reference is also excluded from the referenced half outright — a CALL naming its own state
  store is a placement, not a signal.

### The denominator, and the exit codes

Every run states `N file(s) scanned` and the two half-counts, so a consumer can tell **"no signals"**
from **"nothing was examined"** — in the text output, in `--json` (`counts`, `scope`,
`examinedNothing`), and in the exit code.

| exit | meaning |
|---|---|
| 0 | a complete signal set was derived |
| 1 | **PARTIAL** — the set is not a complete statement of the block's signals. **Two causes, reported separately** (below). Also the usage/argument refusal code, per the house convention |
| 2 | **NOTHING EXAMINED** — `--block` names no block in the corpus, or the origin/type/direction filters left zero rows |

**Exit 2 is never a pass.** An emitter is not exempt from "empty is not clean": an empty document is
what a block with no signals produces *and* what a mis-typed `--block` produces, and the consumer
downstream is a generator that will happily emit a binding for zero signals. `candidate-scan --fb <a
name in no block>` scanned all 43 files and exited 0 for a week (FI-44). The gate here keys on the
**row count** as well as on the scope enum, per `undriven-scan`'s third shape — so a shape nobody has
enumerated yet still cannot report a pass.

**Exit 1 on a partial corpus GATES rather than warns**, unlike every sibling, which prints its
inventory warnings and exits 0. That is right for a check a human reads; this document's reader is a
generator that never sees a warning line, and a detection that only warns on the path to production
gets skimmed.

#### 🔴 PARTIAL has TWO causes and they need different actions (FI-88, 2026-09-03)

| cause | what it means | what to do |
|---|---|---|
| **warning** | a FILE could not be read, so members may be absent and referenced paths may be misclassified `undeclared` | fix the corpus so it parses |
| **opaque member** | a MEMBER'S TYPE could not be opened, so **that member's leaves are missing from a set that otherwise reads complete** — the row is listed, what is underneath it is not | supply the missing type in `--project` |

The second is FI-88's own shape. On a real program a UDT-typed `STATIC` — one block's entire
caller-visible interface, referenced **251 times** — came back as a single row reading
`direction = unused, writers = [], readers = []`, at `partial: false` and exit 0. Six of eight
function blocks were excluded from a conformance harness on that output. A member whose type cannot
be opened is now that gate.

- `--json` carries `opaqueMembers: [{path, datatype, reason}]` beside `partial`, so a generator
  reading `partial: true` can say **which** member it may not trust rather than distrusting the whole
  document (which in practice means trusting it).
- The `reason` names the **search root**, the **file count**, and that the scan is
  **TOP-DIRECTORY-ONLY with no recursion** — so "the type is one directory down" is distinguishable
  from "the type does not exist".
- **The opaque set is collected from the UNFILTERED entries**, before `--origin`/`--type`/
  `--direction` apply. An opaque member is exactly the row a `--type` filter drops — its type is the
  thing that did not resolve — so a filter must narrow what is REPORTED and never what is GATED on.
- A member's type is resolved through the project's `TYPE` files, so a UDT-typed member with **no
  inlined body** (what a generation pipeline emits; a TIA re-export inlines everything) expands to
  its real leaves. A `TYPE ` file that is present but unparseable now raises a warning instead of
  being skipped silently — "type absent" and "type present and broken" used to produce byte-identical
  output.
- **Not everything unopened is opaque.** A multi-instance (the datatype names a block in this
  corpus), an IEC timer/counter, any declaration carrying a `VERSION` (an instruction or library
  instance, whose definition lives in TIA's libraries and can never be an `.ir` file), and an
  `Array[…] of` (one aggregate leaf — expanding it would lose the element index) all terminate as a
  single leaf without gating.

### What it deliberately does not do

- **No filtering of instruction state.** An IEC timer's `.IN`/`.PT`/`.ET`/`.Q` appear as ordinary
  interface leaves, because the inventory is declaration-only and deciding they are "not really
  signals" would be a judgement the document is not entitled to make. Filter with `--type` if you
  want them out.
- **No name-resemblance matching**, no suggested bindings, no "this looks like that". `undriven-scan`
  offers a labelled heuristic hint for a human; this emits data for a generator, where a heuristic
  would be laundered into a fact.

## `harness-binding` — the derivable half of a binding document, emitted (2026-08-27)

```
converter harness-binding --project <ir-dir> --stimulus <FBName> --slot <id>
                          [--observe <FBName>]... [--scope <tag-prefix>]...
                          [--instance <iDB>] [--emit <file.json>] [--json]
```

`signal-set` states a block's signals. **This turns that statement into the document the harness
actually loads** — `Harness.Gate.BindingDocument`'s wire format — and stops at the exact line where
the corpus stops knowing. Without `--emit` it only reports; with it, it writes the scaffold.

### What it derives, measured against the committed 297-line artifact

Run against `ir/test-project001` for the `HBA` slot and compared to
`gen/test-project001/hopper-blockage-alarm/harness-binding.json`:

| field | result |
|---|---|
| `tag` | **20 of 20**, exact |
| `type` (+ register width: Bool 1, Int 1, Time 2) | **20 of 20**, exact |
| driven vs observed | **0 misclassifications** over the 20 shared rows |
| `latchedBy` | **all 8 result sources exact** — 5 named, 3 correctly left absent |
| `baseByte` / `declaredRegisters` | `1000` / `37`, from `served-area` |

**Recall is 20 of 20; precision is 20 of 27.** It offers 7 rows the deliverable does not want (2 driven
— `Stim.Start`, which is that slot's `startCondition`, and `Stim.ModelThreshold`, which the prose
deliberately leaves unnamed — and 5 observed). That asymmetry is chosen: deleting a row a human did not
want is safe, and silently lacking one they did is not. Both properties are locked by
`Harness.Results.Tests/ScaffoldedBindingParityTests.cs`, which runs this binary and parses the result
with the harness's own reader — the only thing connecting the two schemas.

### 🔴 The partition is NOT `direction` alone

Two rules, and the real corpus needs both:

- **`both` is never a vector target.** The block writes the member itself, so a harness write contends
  with its own coil — and the `writers` list is the evidence that it would. It is offered as an
  observation instead, because reading is non-destructive.
- **A `read` member of an `--observe` block is driven by the STIMULUS MODEL, not by the harness.**
  Driving it would have the harness testing its own arithmetic.

Both are **excluded with a reason and counted**, never dropped: a shortened document reads exactly
like a shorter block. Roles are declared by the caller and never inferred — a corpus cannot say which
of its FBs is the stimulus head, and guessing decides from nothing which signals may be *driven*.

### 🔴 `latchedBy` is derivable, and the deliverable records a human deriving it by hand

Its own note reads *"the latch provenance was in the IR all along … a whole-corpus search finds no
other writer."* That search is two conditions, both required: a **SET or RESET coil** among the
writers, and **every writer in one block**. A plain coil is not a latch however it is sealed —
`IO.HopperBlockedAlarm` seals itself in its own rung and the deliverable correctly claims no latch for
it. Absent is itself the claim *"this binding claims no latch"*, so it is never written speculatively,
and the evidence rides along in `_latchEvidence` so a reader can check it without re-deriving.

### What it will not write, ever

`specName` · `encoding` · `inertRest` · `startCondition`. Each is a claim about the **plant** or about
a **specification**, and a generator that supplies one has invented the fact this pipeline exists to
keep out. `startCondition` is the trap: **absent parses as null, and null is the positive claim "this
slot has no start gate"** — so neither answer is emitted.

They become named holes under **`unresolvedHoles`**, which is deliberately **not** underscore-prefixed:
gate 0b splits a binding's unmapped keys on the leading underscore, counts `_note` as an annotation and
**refuses** everything else. So an unfinished scaffold is *unrunnable*, not merely commented — a
warning is not a gate. Each hole names what is missing **and who resolves it**.

`baseByte`/`declaredRegisters` are written **only when `served-area` derived them**; NOT DERIVED is
carried as an absence, never as a number. `BatchPlanner` still compares this derived width against any
authored one and refuses on disagreement rather than substituting — that property is untouched.

### Exit codes

| exit | meaning |
|---|---|
| 0 | a scaffold was derived (and written, if `--emit`) |
| 1 | **REFUSED** — no document is written at all; or PARTIAL corpus; or a usage error |
| 2 | **NOTHING EXAMINED** — a named block is not in the corpus, or `--scope` admitted nothing |

Refusals name what is missing and who resolves it: an unknown block, a block with **no** placement (the
engineer creates the instance — hard rule 3 forbids inventing one), a block with **two** (the caller
picks with `--instance`; which unit is on the rig is a fact about the plant), and a type the mirror has
no element for — refused by name rather than approximated, because a hard-coded `Int` once reached a
controller and TIA answered `Data type Bool is not permitted here` after a full import.

**One refusal refuses the whole scaffold** (`CopyLayerResult`'s rule): a partially-emitted binding is
the shape that looks finished.

### Where it stops

The order of the two arrays **is** the register order, and the scaffold's ordinal-by-tag order is a
decision it makes, not a fact it read — so it is named as a hole. It also does not derive `serves`,
`servesRunInOrder`, `boundarySpanning`, `transient`, `rearmsEachIndex` or `armedBy`: those are claims
carried by the **vector submission**, not by the corpus, and joining the two is a separate seam.

