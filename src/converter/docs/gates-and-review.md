# converter — gates and review

What proves a block is safe to import and conforms to the conventions: the confirm loop (`preflight` → `diff` → `compare` → `drift-check`) and the `review` incident record.

**EMPTY IS NOT CLEAN.** Exit 1 = found something; exit 2 = EXAMINED NOTHING. Exit 2 is never a pass. When you read a green, read what it says it *compared*.

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

## `digest` — compact structural summary (2026-07-16, FI-15)

`converter digest <file.ir> [<file.ir> ...] [--ignore-errors] [--json] [--fingerprint]`

A deterministic, mechanically-derived orientation summary of `.ir` content — "what shape is this
block?" at a fraction of the tokens of the full IR, per FI-15 (`docs/16-future-ideas.md`). Derived
fresh on every run, never stored, so it cannot go stale. **Not** an explanation and **not** review
input — a reviewer that should read every rung still reads the full IR (the S2 exhaustiveness
lesson); this is for finding *which* file to open.

- Blocks: kind/name/number/title, interface sections (`Name : Datatype`, nested-member counts),
  CALL sites grouped by callee with distinct instance paths, per-network title + statement counts
  (`coil:2, timer:1` — statement-level only; contacts/comparisons live inside condition
  expressions and are deliberately not counted), and deduplicated global tag roots (first path
  component via `AccessNode.FromDottedPath`, so `Clock_0.5Hz`-style literal-dot names stay
  atomic).
- DBs (`GlobalDB`/`InstanceDB` + `INSTANCEOF`), UDTs, and tag tables (name : type @ address) get
  the corresponding member/tag listing.
- Same batch contract as `review`: fails on the first bad file unless `--ignore-errors` records
  it and continues; `--json` for machine use; non-zero exit only for file errors (a digest has no
  findings). Works on both exported IR (with `SIDECAR`) and freshly hand-authored, sidecar-less
  IR — dispatched by the section's presence, mirroring `ParseBlock`/`ParseBlockWithoutSidecar`'s
  own contract.
- Piloted against all 14 `ir/reference/*.ir`, a pattern example DB, and `test-project001`'s
  `FC_ControlMain` (calls/instances/network map/DB roots all correct at a glance).

### `--fingerprint` — per-network structural signatures (2026-07-20, FI-23)

Adds a `SIG: <hash>` line under each network: a normalized structural signature (12 hex chars,
SHA-256 of a canonical string). Where the statement counts above are *shape-blind* (`coil:2,
timer:1` says nothing about the rung shapes), the signature captures statement **kinds + order**
plus the canonicalized structure of every condition/operand `Expr` tree — so **N copy-pasted
networks collapse to one hash and the drifted outlier stands out**. Serves the explain-plc-block
skill's "describe the template once, verify EVERY instance" method (done by hand today).

- **Tag-name-independent**: every tag ref / bare dest / instance path abstracts to `TAG`, so two
  networks with identical shape but different wiring share a signature.
- **Literal values are KEPT** (`LIT:<value>`): a network that copied a template but changed a
  constant (`Step = 10` vs `Step = 20`) gets a *different* signature — that's exactly the
  copy-paste drift worth surfacing. (`CalcStatement.Equation` is excluded — free-text that embeds
  operand names, same reason `TagReferences` skips it.)
- **Canonical**: `And`/`Or` operands are sorted (order-independent); `Compare` stays ordered
  (`>=`/`<=` aren't symmetric); statement order within the network matters (it's part of the
  shape).
- The signature is **always present in `--json`** (a machine consumer grouping by shape wants it
  unconditionally); the flag only gates the text `SIG:` line. Default text output (no flag) is
  byte-identical to before FI-23. Same "derived fresh, orientation-only, never review input"
  posture as the rest of `digest` — a fingerprint is an explanation aid, not a policy change.
- Pilot: `FB_ShredderSequencer` (15 networks, several with repeating step-transition shapes) — all
  15 signatures distinct; notably nets 9 and 13 share the *count* summary `timer:1, move:3` but get
  different signatures because their guard structure and step literals genuinely differ. The
  fingerprint separates hand-differentiated steps that the count view can't tell apart.

## `preflight` — static checks before any Portal round trip (2026-07-16, FI-13)

`converter preflight <file.ir> [<file.ir> ...] --project <ir-dir> [--json]`

A static filter in front of the compile gate, per FI-13 (`docs/16-future-ideas.md`) — catches the
*known, recurring* import/compile error classes in milliseconds instead of a slow Portal cycle.
**Explicitly not the compile gate** (hard rule 4): passing pre-flight proves nothing about TIA
acceptance; the text output carries that disclaimer permanently. Composition only — no new
analysis: real parsers, real writers, `review`'s own rules, `TagReferences` +
`AccessNode.FromDottedPath` for resolution.

Checks, per file:

1. **parse** — the target parses at all (a parse failure is a finding, not a batch abort).
2. **convert** — blocks build through `FlgNetBuilder` (sidecar'd) or `SidecarSynthesizer`
   (sidecar-less; "not synthesizable" is a finding worth knowing before trying
   `to-xml --synthesize`); DB/UDT/tag-table content passes its writer.
3. **tag** — every global tag root (via `TagReferences`, roots via `FromDottedPath`) must resolve
   to a local declaration, a project DB, a tag-table entry, or another file *in the same batch*
   (a new block plus its new DB pre-flight together, the way they'd be imported together). One
   finding per unresolved root. This is the pipeline's "`exists`, verified by grep" rule
   (`docs/15-generation-pipeline.md`), mechanized.
4. **call** — every CALL's callee resolves to a block in the project or batch.
5. **instanceof** — an instance DB's `INSTANCEOF` target resolves to a block.
6. **review:C-xxx** — `converter review`'s findings folded in, prefixed by rule. Since 2026-08-13
   this passes the `TagTypeRegistry` preflight had already built (it requires `--project`, so one
   always existed), which is what makes the cross-file rules C-118/C-122/C-125 run here at all —
   they had recorded themselves unrunnable on every preflight before that. A rule left **unjudged**
   is folded in as a `[NOT CHECKED]` finding too: preflight's value is that passing it means
   something, and "nobody implemented that rule" must not be one of the ways it passes.
7. **flow-order** (FI-27) — each synthesized network's instruction `<Parts>` must serialize in TIA's
   DFS-from-rail wire-graph order, or a live import rejects it ("the elements must be sorted according
   to the current flow"). The `Normalizer` sorts `<Parts>` before comparing, so *no* equivalence
   oracle sees raw Part order — this validates the emitted order against the single-source-of-truth
   rule (`FlgNetWriter.FlowOrderedPartUIds`), catching a writer regression offline on any real block.

`--project <ir-dir>` is the current export (`ir/<project>/`), scanned non-recursively; files that
fail to index are surfaced as `INDEX WARNING`s. Exit non-zero on any finding. Deliberately *not*
checked in v1: UDT existence for `Datatype` strings (verbatim strings like quoted UDT names /
`Array[…] of X` would need their own parser — scope stays at the known error classes).

Piloted: all 21 `ir/test-project001/*.ir` against their own export — zero tag/call/instanceof/convert
findings, 17 genuine review findings; `ir/reference` blocks reproduce the S4 pilot's known
findings (C-003 naming, the `NodeStatusAlarms` C-301/C-501 alarm-word pair, C-406 TONR/TOF) with
zero false unresolved-tag findings.

### The denominator, and why there are five of them (2026-08-23)

**Narrow is not dirty** — the inverse of the EMPTY IS NOT CLEAN principle the mechanical floor is
built on, and the same defect `reuse-scan` was fixed for (above). Until 2026-08-23 preflight printed
a numerator with no denominator, so the answer silently depended on `--project` and a reader could
not tell. Measured, same file, same instant:

```
$ converter preflight ir/test-project001/FB_PusherControl.ir --project ir/test-project001
SUMMARY: 1 file(s), 2 finding(s), 0 reported but not gating (harness-scope)

$ converter preflight ir/test-project001/FB_PusherControl.ir --project ir/reference
SUMMARY: 1 file(s), 5 finding(s), 0 reported but not gating (harness-scope)
```

43 `.ir` files against 15, and **neither number appeared anywhere**. `1 file(s)` is the BATCH SIZE —
the numerator's subject, not its index — so it stays 1 whatever the corpus is. The three extra
findings are worded *"does not resolve to any block in the project or batch"* / *"is neither a
UDT-typed interface member nor a DB known to the --project index"*, and were indistinguishable from
genuine unresolved names.

Now, appended after `SUMMARY:` and before the `PRE-FLIGHT ONLY:` footer, on every run:

```
CORPUS: 43 project file(s) + 1 batch file(s)  (project=ir/test-project001)
RESOLVED AGAINST: 18 block name(s) [call, instanceof]; 145 tag/DB root name(s) [tag root]; 23 DB/UDT body(ies) [member path, literal-fit]; 18 callee interface(s) [convert: wired CALL]; 43 file(s) classified [review:harness-scope]
```

against the narrow scope, the same file:

```
CORPUS: 15 project file(s) + 1 batch file(s)  (project=ir/reference)
RESOLVED AGAINST: 11 block name(s) [call, instanceof]; 5 tag/DB root name(s) [tag root]; 5 DB/UDT body(ies) [member path, literal-fit]; 11 callee interface(s) [convert: wired CALL]; 16 file(s) classified [review:harness-scope]
```

🔴 **FIVE counts because there are FOUR corpus walks, and the number must match the finding it sits
under.** `PreflightRunner` independently enumerates `--project` four times — `ProjectIndex.Build`,
`Program.BuildCalleeRegistry`, `Program.BuildTagTypeRegistry`, `HarnessScope.Build` — and they index
different things. *"Does not resolve to any BLOCK"* is falsifiable against the **block-name count**
and nothing else; the member-path findings (`member path '…' does not resolve`) **never touch
`ProjectIndex` at all** — they walk `TagTypeRegistry` via `MemberPathResolver`. A single "43 files"
number under either would be a category slip, so each name-set states its own size and is tagged
with the finding class it decides. `ProjectIndex` had answered only the boolean
(`IndexedAnything`, FI-44) and its four name-sets were private with no accessors.

**The counts are merged, project ∪ batch** — that is what the resolution actually consulted — and
the `CORPUS:` line carries the split. Paths ride beside the counts on `drift-check`'s
`COMPARED: … (project=…)` precedent, because the whole defect is that the answer depends on
`--project`. `HarnessCorpusFileCount` is deduplicated and path-canonicalised by `HarnessScope.Build`,
so it is legitimately 43 (not 44) when the batch file already lives in the project dir.

The zeroes print, for the reason the harness-scope clause beside them already does: an absent count
cannot distinguish "the corpus held none" from "the walk never ran". An **empty** `--project` — a
directory that exists (the CLI refuses a missing one) but holds no `.ir` — additionally prints

```
PROJECT CORPUS EMPTY: --project contributed 0 .ir file(s), so every name above was resolved against the batch alone — neither a finding nor a CLEAN verdict here is evidence about the project.
```

**No exit-2 case, and the asymmetry with `reuse-scan` is deliberate.** There, exit 0 *licenses*
"nothing to reuse, write a new block", so a scan that examined nothing had to stop being a pass.
Here a narrow or empty corpus makes preflight **noisier, not quieter**: its false answer is a false
accusation delivered on exit 1, and no exit code repairs a false accusation — only the denominator
printed under it does. A self-contained batch (a new block plus its new DB, pre-flighted before
either exists in the project) is a legitimate run and must not be failed for it. The existing
`SUMMARY:` line is likewise untouched: `1 file(s) of 43` would assert that one of 43 project files
was pre-flighted, which is the category slip this change removes, and skills grep that line.

`--json` gained its **first summary object of any kind** — a `corpus` sibling of `files` and
`indexWarnings` carrying the same nine values. Before this a JSON consumer counted array elements
and could reach no denominator at all.

**What the denominator still cannot tell you.** It is a count of *names indexed*, not of *names
relevant*: 145 tag roots does not mean the one your block needs is among them, and a corpus of the
right size can still be the wrong project. It says nothing about **recursion** — `--project` is
scanned top-directory-only, so a subdirectory full of `.ir` contributes 0 and reads exactly like an
empty export. It cannot distinguish a **stale** export from a current one (`drift-check` is that
question). It does not cover the review pass's own corpus reach beyond the `TagTypeRegistry` /
`HarnessScope` counts shown. And the per-class counts are *set sizes, not per-finding provenance*:
they tell you how large the haystack was, never which of the four walks a particular
`review:C-xxx` finding consulted.

## `tagstatus` — classify tag names exists/proposed (2026-07-18, FI-24)

`converter tagstatus <name> [<name> ...] --project <ir-dir> [--json] [--roots-only]`

Mechanizes the pipeline's anti-laundering classification (`docs/15-generation-pipeline.md`
"Artifacts"; CLAUDE.md hard rule 3). Built for `gen-architecture`'s tag-status step and for the
Build stage's run-stopping gate, both of which otherwise hand-grep the export. Composition only —
root resolution reuses `ProjectIndex` and the *same* `AccessNode.FromDottedPath` extraction
`preflight` uses; **member** resolution walks the `TagTypeRegistry` corpus (the cross-file DB/UDT
index behind the C-118 review rule), so "does `DB.member` exist" reads the same indexed facts as
"what type is `DB.member`" and the two cannot drift.

Five states, because a root can resolve while its member namespace is genuinely unknowable, and
because a subscript outside an array's declared bounds is a different fault from an invented member:

| Status | Meaning | Gate |
|---|---|---|
| `EXISTS` | the whole dotted path resolves (or a bare name resolves as a tag/DB) | pass |
| `PROPOSED` | the **root** does not resolve — a named gap the engineer creates | **fail** |
| `MEMBER-NOT-FOUND` | root resolves, members **are** enumerable, this member is absent | **fail** |
| `MEMBER-UNCHECKED` | root resolves, member namespace not enumerable (unexported UDT, or an instance-DB stub from `create-instance-db` with no member tree) | pass, reported |
| `INDEX-OUT-OF-RANGE` | every component is a real member, but a written subscript falls outside the declared bounds (`Vessel[7]` of `Array[0..3] of "UDT_Vessel"`) | **fail** |

Each name is resolved by checking the whole name first (catches bare tag-table tags whose own name
contains a dot, e.g. `Clock_0.5Hz`, and DB names), then its root, then the member path.
**Array-of-UDT members are walked through** (2026-08-05, FI-45 item 1): a subscript is stripped, the
member resolved, and the walk continues into the element type's own members, recursively
(`DB.Vessel[0].Sensor[1].Reading`). The **unindexed** form (`DB.Vessel.Reading`) resolves identically
and deliberately — it is a legitimate *type-level* question ("does every element carry this
member?"), the form a spec or binding table uses, and answering `MEMBER-NOT-FOUND` there would call
something real invented. Bounds are only ever checked against a subscript actually written, and only
when both the index and the declared bounds are integer literals — a symbolic index (`Vessel[#i]`),
`Array[*]`, or a dimension-count mismatch is left alone rather than guessed at.
`--project <ir-dir>` is the current export, scanned non-recursively; unindexable files surface as
`INDEX WARNING`s. **Exit non-zero if any name is `PROPOSED`, `MEMBER-NOT-FOUND` or
`INDEX-OUT-OF-RANGE`** — so
`converter tagstatus … --project … && <build>` is a usable "all tags exist" gate.
`--roots-only` restores root-level-only classification for the Design stage, which classifies at
root level because it designs *against* gaps rather than coding against them.

```
$ converter tagstatus DB_Input.Cycle_Start DI3_SYS_CycleStart MadeUpTag DB_Input.Invented --project ir/test-project001
DB_Input.Cycle_Start -> EXISTS (root: DB_Input)
DI3_SYS_CycleStart -> EXISTS
MadeUpTag -> PROPOSED
DB_Input.Invented -> MEMBER-NOT-FOUND (root: DB_Input)
SUMMARY: 4 name(s), 1 proposed, 1 member-not-found, 0 member-unchecked, 0 index-out-of-range   # exit 1
```

A blocking entry that can say something more precise than its status carries a short detail after an
em dash (`… -> INDEX-OUT-OF-RANGE (root: DB_Params) — Vessel[7] is outside Array[0..3] of
"UDT_Vessel"`); `--json` carries the same string as `detail`.

**History (2026-08-05):** classification used to stop at the root, so `DB_Input.Invented` reported
`EXISTS` — the gate protecting hard rule 3 blessed invented DB members, and `gen-block-new` gates its
run on that result. Found by a pipeline run that independently grep-verified every member; the
member check closes it, and `MEMBER-UNCHECKED` keeps the fix from manufacturing false gaps in the
other direction.

**History (2026-08-05, FI-45 item 1):** the member walk then turned out to stop dead at an array
subscript — `DB.Vessel[0].MaxNet` and its unindexed form both reported `MEMBER-NOT-FOUND` while a
named-UDT member resolved fine. An array of UDT is the ordinary way to express N identical vessels,
so on such a project **every** per-instance binding read as invented: hard rule 3's gate firing at
correct code, which is exactly how a gate gets trained out of use. The walk now crosses arrays at any
depth, and the bounds check it needed anyway became a finding nobody previously got
(`INDEX-OUT-OF-RANGE`). Same pass fixed `TagTypeRegistry.Resolve`, which was blind to the same shape,
so an operand inside an array of UDT now types correctly for `to-xml --synthesize` too.

## `diff` — network-level IR invariance (2026-07-18, S7 entry requirement)

### What gates, and what the verdict states (migrated from CLAUDE.md 2026-08-21)

`converter diff <old.ir> <new.ir> [--only <network>...] [--allow-header] [--json]`

🔴 **WITHOUT `--only`, `diff` IS A REPORT AND CANNOT GATE. EXIT 0 IS UNCONDITIONAL AND PROVES
NOTHING (2026-08-25).** Both halves of the verdict are guarded on `--only` having been supplied:

```csharp
public IReadOnlyList<NetworkDiff> InvarianceViolations =>
    AllowedNetworks.Count == 0 ? Array.Empty<NetworkDiff>() : …   // no --only ⇒ empty, always

public bool HasUnclaimedHeaderChange =>
    AllowedNetworks.Count > 0 && !HeaderChangeAllowed && …        // no --only ⇒ false, always

public bool HasInvarianceViolation => InvarianceViolations.Count > 0 || HasUnclaimedHeaderChange;
```

So a bare `converter diff <old> <new>` returns **0 with any number of changed, added or removed
networks**, and returns 0 through an undeclared **interface retype** and a **block name mismatch** —
the two things the header rules below exist to catch. This is by design (the report form is useful),
but the exit code does not say so, and **`diff` is the tool agents are told to quote as invariance
evidence.** An agent reporting *"`converter diff` exit 0"* without `--only` has reported nothing at
all.

**This is `EMPTY IS NOT CLEAN` in the one place the convention does not apply itself:** everywhere
else on the mechanical floor, examining nothing is **exit 2**, which is never a pass. Here examining
nothing is **exit 0**, which reads as one. Raised as **FI-79**; not changed here, because the exit
code is a contract other callers key on.

**When you need the gate, pass `--only`.** When a change genuinely touches every network, say so and
prove it another way — do not reach for the bare form because it goes green.

⚠️ **And do not read a `diff` exit code through a pipe.** Measured the same day: a run piped to
`tail` reported the exit status of `tail`, not of `diff`, and a real gate result was nearly filed as
a tool defect on the strength of it. Redirect with `>` and read the file. (The standing "never pipe"
rule was written for `openness-cli` and Portal; the reason differs here, but the discipline is the
same.)

`--only` asks one question: *did anything change outside the named networks that could alter what
the PLC does?*

- **A HEADER change gates** — exit 1. An **interface** member answers yes (a `Bool` → `Int` retype
  with no network touched compiles, imports, and misbehaves on the controller). A **rename or
  renumber** answers yes on identity grounds: TIA's import matches by NAME, so a rename creates a
  DUPLICATE block rather than updating one.
- **A block COMMENT cannot** — comment-only ⇒ **exit 0**, with `HEADER COMMENT CHANGED (does not
  gate)` on its own line. Non-gating is not invisible: that line is where a stale comment gets
  repaired, and equally where a correct one gets silently discarded.
- **NOR CAN AN INTERFACE MEMBER'S COMMENT** (2026-08-21). The interface used to be compared as one
  serialized blob, so member comment text sat inside `interfaceChanged` and repairing one was
  indistinguishable from a retype. Measured: deleting the seven characters `(C-115)` from a member
  comment produced `INVARIANCE VIOLATION: an INTERFACE member changed`, exit 1, while the identical
  edit to the BLOCK comment exited 0 — the same defect as the 2026-08-14 narrowing, one level down,
  and it left a C-204 sweep with no route to the breaches living in member comments.
- **The carve-out is ONE field.** `Retain`, `SetPoint`, datatype, start value, the `External*` flags,
  nesting and the member NAME all still gate. The comparison is done twice — once with member
  comments blanked (that is `interfaceChanged`), once whole — and `interfaceCommentChanged` is set
  only when the structure compared **equal** and the text did not. The two are mutually exclusive by
  construction, so a retype can never be reported as a comment edit.
- **A comment edit is never cover for a behaviour-bearing one** — both together still exit 1,
  naming the INTERFACE, not the comment.
- The JSON carries `commentChanged`, `interfaceChanged` and `interfaceCommentChanged` as separate
  fields. A consumer gating on `interfaceChanged` alone is unaffected: the new field is a strictly
  narrower signal, never a reclassification of something that used to gate.
- The human-readable form prints `HEADER changed: interface` (or `: comment`, `: interface-comment`)
  naming which half moved, so the verdict and the exit code can be read against each other. The
  non-gating line NAMES its subject — `the block comment`, `one or more INTERFACE member comments`,
  or both — because a reader told the wrong subject inspects the wrong text.

`--allow-header` is the named escape (FI-71's shape), and it means ROUTE, not DECLARE:

- `gen-block-modify-purpose` **passes it** and declares the interface delta in its hand-back.
- `gen-block-modify-fix` **must NEVER pass it** — a fix needing an interface member *is* a purpose
  change, so the answer is to route to the purpose skill, not to declare.

**The verdict states its own denominator:** `UNCHANGED REMAINDER: <n> network(s) proven identical
outside --only`. Where every network is inside the `--only` set the remainder is EMPTY and
`INVARIANCE OK` proves nothing — it says `NOTHING WAS PROVEN` instead. Same shape as
`drift-check`'s `COMPARED: <n>`: an invariance claim over an empty remainder is another
empty-is-not-clean. A clean verdict states what it EXAMINED (`…, header unchanged`), not merely
that it passed.

`converter diff <old.ir> <new.ir> [--only <network> ...] [--json]`

Mechanizes the S7 "untouched-network invariance check" (roadmap `docs/02-roadmap.md` line 57;
CLAUDE.md "Workflow for modifying existing logic"): given the before/after IR of **one block**, it
reports which networks changed and — with `--only` — proves the rest are identical. Built now, in
parallel with the coding skills, per the 2026-07-18 A-4 ruling (`docs/evidence/stage-S6.md`).

**How "identical" is defined (and why no separate normalizer is needed):** semantic equality is
equality of the sidecar-free *readable* form (`IrSerializer.SerializeNetworkOnly`), which
`IrSelfStabilityTests` already proves byte-stable. Every volatile UId (Wire/Access/Part/
CompileUnit) lives only in the `SIDECAR` section that form omits, so the UId churn a TIA re-export
produces normalizes out for free. Block-level Title/Comment/interface changes are surfaced
separately (the interface via its own sidecar-free canonical slice; block `RootUId` is deliberately
not compared, being a volatile block ID).

Either input may carry a real `SIDECAR` (a TIA export — the S7 shape: before = exported block, after =
the same block edited then reconverted) **or be sidecar-less** (a freshly-authored / validation-corpus
block); the comparison is sidecar-free either way. `--only` accepts space- or comma-separated numbers,
or repeated flags (`--only 1 2` / `--only 1,2` / `--only 1 --only 2`).

🔴 **NETWORKS ARE MATCHED ON CONTENT FIRST, THEN ON NUMBER — AND MATCHING ONLY ON NUMBER WAS A
CLOSED CHECK (2026-08-23).** The old matcher paired networks purely by `Number`, with a comment
calling wholesale renumbering *"a known, documented limitation"*. That limitation is not exotic:
**insert one network at 11 in a 20-network block and 11..20 all shift**, so the report read
`10 changed, 1 added` and `--only 11` **exited 1 naming nine networks nobody had touched** — from
the gate whose entire job is *"prove the rest is identical"*. It was answering a question about
numbers while claiming to answer one about content, which is why it looked healthy: never empty,
never silent. Now content that appears **exactly once on each side** is paired regardless of number,
and the residue falls back to number matching as before.

**`Moved` is its own kind and it GATES.** LAD executes in network order, so a rung that runs later
than it used to changes what the PLC does. The fix is that a move is *named* — with the number it
came from — not that it is forgiven. A pure reorder is `2 moved` and exit 1.

⚠️ **The ambiguity rule is the safety argument.** Only content unique on **both** sides anchors. Two
networks with identical bodies — which real ladder has — anchor to nothing and fall back to number,
because pairing them would be a guess, and a content anchor that mis-paired duplicates would be a
*new* false green inside a gate whose job is proving identity.

⚠️ **Known, asserted limitation: a network that is BOTH edited AND moved reports as a REMOVE plus an
ADD**, not as one `Changed`. Nothing can pair it — the content differs so the anchor declines, the
number differs so the fallback declines — and inventing a pairing would be a guess about which
network the author meant. The gate is unharmed (it fires, and names both numbers); it is just less
tidy than one row.

**`--insert <n>` is the named escape** (`--allow-header`'s shape, FI-71's): it declares *"networks
were inserted and/or removed at position n"*, and the resulting shift does not gate. **It is CHECKED,
never believed** — every moved network must satisfy `MovedFrom >= n` and `newNumber - MovedFrom ==
(added - removed)`, and the shift must be non-zero. A declaration that does not match buys nothing,
because a flag that silences whatever it is pointed at is an off switch, not an escape. It is
**refused with exit 2 when `--only` is absent** (nothing was judged: without `--only` there is no
gate for it to modify, and a flag that can sit harmlessly in a script becomes a default nobody
notices). `gen-block-modify-purpose` may pass it and must declare the insertion in its hand-back;
**`gen-block-modify-fix` must never** — a fix that needs a new network is a purpose change, so the
answer is to route, not to declare.

**Exit code:** with `--only`, exit 1 if any network *outside* the declared set changed/appeared/
disappeared/moved — the invariance assertion the S7 skill gates on. Without `--only`, it's an
informational report (exit 0); a malformed IR file or a missing path exits 1; `--insert` without
`--only` exits 2.

```
$ converter diff before.ir after.ir --only 1
SUMMARY: 2 network(s): 1 changed, 0 added, 0 removed, 0 moved, 1 identical

CHANGED network 1 "Perimeter Safety Alarm Bit Mapping"
  - ... old readable form ...
  + ... new readable form ...

INVARIANCE OK: all changes confined to --only {1}          # exit 0
UNCHANGED REMAINDER: 1 network(s) proven identical outside --only
```

An insertion, declared:

```
$ converter diff before.ir after.ir --only 11 --insert 11
SUMMARY: 21 network(s): 0 changed, 1 added, 0 removed, 10 moved, 10 identical

ADDED network 11 "Call The Slot FC"
  + ... new readable form ...

MOVED network 12 "Index Latch" — was network 11, content unchanged. LAD executes in network
order, so this alters what the PLC does.
... (nine more)

INVARIANCE OK: all changes confined to --only {11}, header unchanged        # exit 0
DECLARED INSERTION at network 11 (--insert): 10 network(s) shifted by 1 and do not gate.
UNCHANGED REMAINDER: 10 network(s) proven identical outside --only
```

## `drift-check` — ir↔simatic-ml export-drift detector (2026-07-20, FI-26)

### Scope, the denominator, and pairing (migrated from CLAUDE.md 2026-08-21)

`converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]`

`--complete` (FI-70) declares the exports dir the WHOLE picture — a fresh controller dump, not a
possibly-lagging committed corpus — so an absence FAILS in either direction: a missing `.xml`
(block not in the controller) or an `.xml` with no `.ir` (block in the controller that no `.ir`
describes). Without it, a green SUMMARY only means "everything paired matched"; the `SCOPE:` line
says which question was answered.

**`COMPARED: <n> object(s) put through the Normalizer` is printed on EVERY run.** Every other
number in the summary is a reason a comparison did NOT happen; that one is the denominator.

**Empty is not clean — three measured routes to a green over zero comparisons, all now exit 1 and
print `NOTHING COMPARED - this is not a pass`:**

1. Both dirs exist but are EMPTY → previously `0 drifted, 0 match`, exit 0.
2. EVERY `.ir` unparseable (a zero-byte file, a truncated write) → previously `1 error`, exit 0,
   because `DriftStatus.Error` never gated. `--complete` covered a missing FILE and said nothing
   about a comparison that could not RUN — the same absence one level in.
3. Either path pointed ONE LEVEL ABOVE the files — both walks are `TopDirectoryOnly`. The likeliest
   real mistake of the three.

**Pairing is by the object's DECLARED NAME, not its filename.** TIA's own name for the default tag
table contains spaces (`Default tag table`) while the `.ir` filename does not
(`DefaultTagTable.ir`), and both documents declare the real name in their own content. Pairing on
the filename made one object fail to pair and then counted it twice — `EXPORT-ONLY: Default tag
table` **plus** `SKIPPED: DefaultTagTable` — a spurious finding and a silently skipped comparison
from one naming mismatch. Identity is now read from each side's own content (`BLOCK/DB/TYPE/
TAGTABLE <name>` and the outermost `SW.*` object's `AttributeList/Name`), whitespace-normalised,
with basename as fallback. Two files claiming one identity is **`PAIRING-FAILURE`** — a THIRD
outcome, not an absence in either direction, and it always gates.

**A `MATCH` here is silent about `MemoryLayout`, and says so on every run.** `compare` refuses
(exit 2, NOT COMPARED) the very pair this reports as a MATCH. Do not "fix" this by un-ignoring the
attribute in `Normalizer` — converter output never emits it, so that breaks every
export-vs-output comparison wholesale until the converter can EMIT it.

`converter drift-check --project <ir-dir> --exports <simatic-ml-dir> [--complete] [--json]`

Detects **silent export drift**: a fix that landed in a committed `ir/<proj>/*.ir` but was never
re-exported, leaving its `simatic-ml/<proj>/<name>.xml` stale (the exact landmine that poisoned a
synthesis-parity audit — a stale `FB_ShredderSequencer.xml` made real synth gaps indistinguishable
from noise). The committed round-trip tests deliberately *tolerate* this via an own-sidecar oracle;
this is the complementary **detector** they don't provide.

For each `ir/<proj>/*.ir`, pairs it with `<exports>/<name>.xml` by basename, rebuilds the SimaticML
in-memory (the *same* code path `to-xml` uses — `BuildXmlFromIrText`, all four `.ir` kinds), and
`Normalizer.AreSemanticallyEquivalent`-compares it to the committed export. Per block: `MATCH`,
`DRIFTED`, `SKIPPED` (no paired `.xml` — an ir-only block), `EXPORT-ONLY` (an `.xml` with no `.ir`),
or `ERROR` (the `.ir` couldn't be converted). Decoupled — no knowledge of which drift is
"known/tolerated" (that lives in the `ExportDriftDetectorTests` golden-test baseline, which excludes
the answer-key blocks). **Exit non-zero if any block DRIFTED.**

### `--complete` — when an ABSENCE is a finding (2026-08-10, FI-70)

The exports directory means two different things and the tool cannot tell them apart from the
inside. As a **committed corpus** it may legitimately lag the `.ir`, so an unpaired file is ordinary.
As a **fresh dump of the controller** an unpaired `.ir` means *this block is not in the controller*
and an unpaired `.xml` means *this block is in the controller and no `.ir` describes it*. `--complete`
is the caller's declaration that the directory is the whole picture; it never changes what is
compared, only whether an absence fails.

Two things changed here beyond the flag, both because a green result was carrying a claim it hadn't
earned:

- **`EXPORT-ONLY` is new, and is always reported.** The runner enumerates `.ir` files, so a block
  that exists only in the controller — added by hand in TIA, or left behind by a rename — could not
  appear in the report under *any* status. Silence about that half was the actual defect; `--complete`
  only decides whether it fails.
- **A `SCOPE:` line now states which question was answered**, so a clean `SUMMARY` can't be read as
  "disk and controller agree" when nothing established that.

This is the comparison half of the disk-vs-controller check. The **export half belongs in
`openness-cli`, not here** — the converter is a pure in-process file transformer that never touches
the environment, and that invariant was held deliberately (FI-24). The intended recipe is
`openness-cli` dumping every block and type to a directory, then this command with `--complete`.

```
$ converter drift-check --project ir/test-project001 --exports simatic-ml/test-project001
DRIFTED: DB_Settings  (semantic divergence between .ir and committed export)
DRIFTED: FB_ShredderSequencer  (semantic divergence between .ir and committed export)
...
EXPORT-ONLY: FB_AddedInPortal  (no paired .ir in project dir)
SKIPPED: FB_HopperBlockageMonitor  (no paired .xml in exports dir)
MATCH: DB_Alarms
...
SUMMARY: 6 drifted, 17 match, 3 skipped, 1 export-only, 0 error          # exit 1
SCOPE: comparison only. 4 file(s) had no counterpart and were NOT judged — pass --complete when the exports
       dir is a full dump (e.g. straight from the controller) and an absence should fail.
```

Two caveats measured on a real corpus rather than anticipated: compare **normalised, never bytes**
(a `--no-sidecar` disk copy differs from its export by hundreds of lines and is not drift), and line
endings vary per file, so neither side may be assumed CRLF or LF.

### 🔴 `PROVENANCE:` — whose documents were on the other side (2026-08-18)

**The fourth way this tool could report a clean run having proved nothing — and the only one that
shows a FULL denominator while doing it.** The three closed under *"empty is not clean"* all surface
as `COMPARED: 0`. This one surfaces as `COMPARED: 101`, a hundred green `MATCH` lines, and an
answer about nothing:

> `to-xml` **writes beside its input by default** (FI-72). Run it over an `ir/` directory and it
> silently replaces every real export sitting there. From then on `drift-check --exports ir/` is
> **the converter compared against its own output** — same binary, same input, same output. `MATCH`
> is a tautology, not a measurement.

Measured on a live job: **0 of 101** files in the directory being passed as `--exports` carried
TIA's `<DocumentInfo>` block, and `0 drifted / 101 match` had been recorded **four times** in the
job's own records as evidence the corpus was in sync with the controller. It was evidence of
nothing. What later appeared as *"52 drifted"* was two real converter **fixes** landing
(`1edf376` wire-endpoint direction, `f2a548a` the instance-DB `InOut` section) while the stale
output did not move with them — a **tightening**, not a regression; see the closing note in the
`compare` section.

`<DocumentInfo>` is written by TIA's exporter on every Openness export and by **nothing else** —
the converter's own writers never emit it, and the Normalizer already ignores it (it has to; real
exports carry it and converter output does not, and the two are compared for equivalence every
day). So it is an exact discriminator, and it is now counted and printed on every run:

```
COMPARED: 101 object(s) put through the Normalizer  (project=…  exports=…)
PROVENANCE: 0 of 101 export(s) compared carry TIA's <DocumentInfo>; 101 do NOT and could be
            converter output (`to-xml` writes BESIDE ITS INPUT by default)
NO TIA EXPORT WAS COMPARED — this is not a pass. …                        # exit 1
```

**The gate is NONE, not ALL, and that is deliberate.** A real corpus legitimately carries the odd
document without provenance — `simatic-ml/reference/` has four out of fifteen — and failing those
would be the gate firing outside its own question. Zero is a different claim: *not one document on
the other side of the comparison came from the controller.* A partial count is **reported and does
not gate**, so a directory drifting towards converter output is visible long before it becomes
total. `--json` carries `tiaExportCount`, `nonTiaExportCount`, `comparedNothingFromTia`, and
`fromTia` per entry.

*Consequence for fixtures:* a test standing in for a TIA export now has to declare itself one
(`TiaExportFixture.SaveAsTiaExport`). Every drift-check fixture in the suite previously built its
"export" side by calling a converter writer and saving it — precisely the state being gated.

## `compare` — the confirm loop's judgement half (2026-08-12)

### Exit codes, the layout premise, and the fenced loop script (migrated from CLAUDE.md 2026-08-21)

`converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]`

Normalizer-compares two SimaticML exports and reports WHAT differs — the path and both values —
not merely THAT something did. **Strictly stronger than `drift-check`**, which never leaves the PC:
this pair has been THROUGH TIA, so it also catches what TIA does on import and compile.

**The `MemoryLayout` premise is enforced, not assumed.** The Normalizer holds neither side to the
other's layout when one is silent — right for the committed corpus, wrong here, because two TIA
exports both declare one. A SILENT SIDE means an input is not what the loop assumes and the
comparison is quietly weaker than it looks: that is **exit 2 with the reason**, never a warning.
`--allow-silent-layout` is the named escape (FI-71's shape).

Exit **0** equivalent / **1** differs / **2 NOT COMPARED**. Exit 2 covers a missing or unparseable
file, XML carrying no `SW.*` object, the same path twice, or a walk that localizes nothing while
the Normalizer says they differ. **Empty is not clean: none of those may exit 0.**

**`tools/confirm-roundtrip.ps1` is armed and fenced (ADR-0011).** It runs
export → to-ir → to-xml → import → compile → export and calls `compare` on the first and last.
It MUTATES, so:

- **`-Arm` REFUSES any project not named in `tools/confirm-roundtrip.allowlist`** — exit 4, checked
  before the binary checks and before any directory is created, so Portal is never contacted on a
  refusal.
- Entries are absolute or **`repo:`-prefixed** so the committed file is portable across worktrees.
- **A bare project name does not work with `-Arm`** — it cannot be canonicalised, and it resolves
  to the project FOLDER.
- **Junctions are DETECTED AND REFUSED**, not half-resolved (PS 5.1 cannot resolve them), so a
  scratch project behind a junction needs its real path allowlisted.
- `-IsScratchProject` is refused BY NAME (`download-plan`'s shape). A dry run is unfenced.
- Allowlist not denylist: this machine carries ~19 real production `.ap20` projects beside the
  scratch ones, and a denylist would have to be complete and would stop being complete the next
  time a job folder arrived.

`.ps1` here is **ASCII-ONLY + CRLF**: PS 5.1 reads a BOM-less script as ANSI, a UTF-8 em dash
decodes to a quote delimiter, and the parse error points a hundred lines away from the real one.

`converter compare <first.xml> <second.xml> [--json] [--max-differences <n>] [--allow-silent-layout]`

The owner's **invariance principle**, stated as a loop
(`docs/notes/test-environment-build-plan.md`):

```
export ──► to-ir ──► to-xml ──► import ──► compile ──► export
   └──────────────── compare THESE TWO ───────────────────┘
```

**Strictly stronger than `drift-check`.** `drift-check` never leaves the PC, so it can only ask
whether the converter is self-consistent; this pair has been **through TIA**, so it also catches
what TIA does on import and on compile. It is the check that would have caught the `MemoryLayout`
hole `drift-check` called a MATCH: original export `Standard`, converter output silent, TIA applies
its S7-1200 default, re-export `Optimized` — first ≠ last, caught.

**The orchestration is not the work; the comparison is.** Built naively it misses the same hole:
a byte-compare fires on every reassigned Part/Wire/Access UId (TIA reassigns them unprompted) and is
unusable as a gate, while a normalizer that ignores too much is exactly how the hole survived. So
this walks the **normalized** trees — the same ones `Normalizer.AreSemanticallyEquivalent` hands to
`XNode.DeepEquals` — and reports **what** differs, not merely **that** something does. The path is a
path in the normalized document, so it names the element TIA changed:

```
$ converter compare 01-first-export.xml 06-second-export.xml
FIRST : 01-first-export.xml
SECOND: 06-second-export.xml
MEMORYLAYOUT: Standard -> Optimized  (compared — both documents declare one)
VALUE-DIFFERS  : /Document/SW.Blocks.GlobalDB/AttributeList/MemoryLayout
    first : Standard
    second: Optimized
VERDICT: DIFFERS — 1 difference(s).                                        # exit 1
```

A bare same/different verdict would send the operator to a manual XML diff through the very UId
churn the Normalizer exists to absorb, which is the state this command replaces.

**Exit 0 equivalent / 1 differs / 2 NOT COMPARED.** The third is the point (FI-44, "empty is not
clean"): a file missing or unparseable, a document carrying no `SW.*` object at all, the same path
passed twice, or a walk that localizes nothing while the Normalizer says the documents differ —
each answers the question with *nothing*, and none of them may wear the face of a pass.

### The MemoryLayout premise, enforced rather than assumed

The Normalizer compares `MemoryLayout` as an **optional assertion**: both documents must declare one
for a difference to be held against them. That weakness is deliberate and belongs to the *other*
caller — every committed `.ir` predates the emit side, so a strict compare would report ~30 blocks
drifted for the benign reason that the IR states no layout.

In the confirm loop it cannot legitimately arise: **both inputs are TIA exports, and a TIA export
always declares a layout**, so the loop gets full strictness for free. But "for free" is a premise,
and a premise nobody checks is how this class of defect keeps recurring. So a one-sided declaration
is **exit 2 with the reason**, not a warning — the same fail-closed shape as `to-xml`'s FI-71
refusal, with `--allow-silent-layout` as the named escape for deliberately comparing converter
output against an export.

### Direction awareness: a reversed wire says so (2026-08-12)

A `<Wire>`'s **first** endpoint is its **producer** and the rest are its **consumers**; nothing else
in a SimaticML document encodes direction (106 `(part, port)` pairs across 34 real exports, **zero**
appearing in both slots). The Normalizer therefore pins endpoint 0 and sorts only the tail, so a
reversal **survives** to this walk — but it did not survive *legibly*. The Normalizer also sorts
`<Wires>` **by** each wire's rendered content, and a reversal changes that content, so the flipped
wire moves to a different index and the positional pairing compares two **different** wires:

```
# BEFORE — one flipped CALL-output wire, measured
ATTR-DIFFERS   : …/Wires/Wire[2]/IdentCon/@UId       first: …ScaleMax…      second: …ScaledValue…
ATTR-DIFFERS   : …/Wires/Wire[2]/NameCon/@Name       first: ScaleFactor     second: ScaledResult
ATTR-DIFFERS   : …/Wires/Wire[3]/NameCon/@Name       first: ScaledResult    second: ScaleFactor
ATTR-DIFFERS   : …/Wires/Wire[3]/IdentCon/@UId       first: …ScaledValue…   second: …ScaleMax…
VERDICT: DIFFERS — 4 difference(s).
```

Every line of that is true and an operator can act on it — but it reads like a **rewiring**, which is
a materially different defect to go hunting for than a **direction reversal**. Now:

```
# AFTER — the same flip
WIRE-DIRECTION : …/FlgNet/Wires/Wire[3]
    the SAME endpoints with the producer and consumer roles REVERSED. A wire's FIRST
    endpoint is its producer; nothing else in the document encodes direction.
    first : port 'ScaledResult' drives operand 'ScaledValue'
    second: operand 'ScaledValue' drives port 'ScaledResult'
VERDICT: DIFFERS — 1 difference(s).
```

The endpoint descriptions deliberately **omit the normalized `UId`** — for an `IdentCon` it is the
Access content key (a whole embedded `<Symbol>` element) and for a `NameCon` it is a topology hash.
Neither reads as anything, and printing them is what made the old output unreadable.

**The classification is all-or-nothing, and that is the safety argument.** It fires only when the two
`<Wires>` containers hold the same wires **as endpoint sets** and differ solely in which endpoint is
first — given equal endpoint multisets a surviving order difference can only be at endpoint 0, since
the tail is already sorted, so *same set, different order* **is** *different producer*. That is
asserted per pair rather than assumed: a pair that differs while its producers match abandons the
classification for the whole container. Any other edit — an endpoint changed, a wire added or
removed, an attribute retyped — fails the multiset test and falls straight through to the
per-attribute walk, **unchanged**. *** Detection is never weakened to improve the message: an
unexplained real difference beats a confidently mislabelled one. *** The known cost, asserted as a
test rather than left as a surprise: a reversal arriving **alongside** another edit in the same
network still reports as per-attribute noise.

Nine tests, negative-tested by disabling the interception (the five direction tests redden; the
fallback tests stay green, which is the point of having both).

### It cannot orchestrate the loop, and that is FI-24

The loop needs Portal; the converter is a pure in-process file transformer and never shells out or
touches the environment. So the sequence lives in **`tools/confirm-roundtrip.ps1`** and only the
judgement lives here. Same split as `drift-check --complete` + `openness-cli export-all`.

## `ir-hash` — stable readable-IR content hash (2026-07-20, FI-17)

`converter ir-hash <file.ir> [<file.ir> ...] [--json]`

Emits `SHA-256(SerializeBlockReadable(block))` per block — a stable content key over the *readable* logic
only (interface + networks), so it invalidates on any logic/interface/comment change but is **immune to
SIDECAR/UId churn** (a re-export doesn't change it). The key for **FI-17 explanation sidecars**
(`docs/notes/explanation-sidecars.md`): a cached explanation stamps `derived-from: <ir-hash>`, and every
consumer recomputes the hash and discards the cache on mismatch (hash-on-read, the ADR-0005 discipline).
Deliberately **not** `digest --fingerprint` (that abstracts tag names → a rename wouldn't invalidate). Exit
1 on a missing/unparseable/non-block file.

## `review` — the tag table was never reviewed, and the report said "not applicable" (2026-08-13)

`converter review` run against a **tag table** exited 0 while all 18 mechanized rules reported
`not applicable — TAGTABLE rule support not implemented in Phase 1`. The file was **effectively
unreviewed** and the exit code and summary were indistinguishable from a clean review — this
project's recurring failure class (an absence of findings read as a positive result), live in the
tooling. A tag table is **where tag names live**, which makes it the file kind C-001 applies to
*most*, not least.

**The three-way split the old wording collapsed.** "Not applicable" was doing three jobs at once,
and only one of them was true:

| | means | zero findings is | reported as |
|---|---|---|---|
| the rule has a subject here and found nothing | a result | **meaningful** | `checked, clean` |
| the rule has no subject in this content kind | a result | meaningful | `not applicable (<this rule's own reason>)` |
| the rule has a subject and nobody judged it | **not a result** | **proves nothing** | `NOT CHECKED - no result, not a pass` → **exit 2** |

**Fail-closed, not a warning.** A `Skipped` (NOT CHECKED) status now **gates**: `converter review`
exits **2 = REVIEW INCOMPLETE**, ahead of exit 1 (error findings), and names what went unjudged on
stderr. This is deliberate — the offending line *had already been printed on every tag-table review
since Phase 1*, in a run that exited 0, and it was read as clean every time; a warning that gets
skimmed is how the defect survived. `--allow-unchecked` is the named escape (FI-71's shape), never
the default. The report always carries an `UNCHECKED: n rule(s) not judged` line **including when n
is 0** — an absent line would itself be ambiguous. `preflight` folds unjudged rules in as findings
for the same reason.

**What is now checked on a tag table** (3 rules), each with a violating *and* a conforming fixture:

- **C-001** — two layers, and every tag lands in exactly one, so no tag is silently unexamined.
  *Physical-IO* (address in the `%I…`/`%Q…` process image): the `<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>`
  format, checked as a **field split, not a prefix match** — the equipment token sits in the *middle*
  (`DQ3_PSH_RunPowerPack`). Because the ADDRESS is right there beside the name, two cross-checks a
  name alone could never give come free: the direction letter must agree with I-vs-Q, and the D/A
  letter with bit-vs-word width. A width that cannot be established (`%I5`: no size letter, no bit
  offset) is **not guessed at** — only the direction is checked there.
  *Everything else* (`%M` flags): C-001's variables layer, short PascalCase, underscore-free.
- **C-005** — charset over every tag name **and the table's own name**. The `LogicalAddress` is
  deliberately not checked (`%I0.0`'s dot is addressing syntax, not a chosen name — the same
  exclusion `CheckPathCharset` already makes for `%Xn` slice components).
- **C-406** — the declaration form, against a tag's own `DataTypeName`. It may well never fire, but
  `DataTypeName` is a free string in the IR model, so a `TOF_TIME` here is **representable** and
  therefore worth looking for. `Checked`, not `CheckedVacuous`: "cannot appear" is not a claim this
  codebase can make about a free-text field.

**C-007's vendor-default exception is reported, not silently applied.** `Clock_0.5Hz` (a dot) and
`Default tag table` (a space) are tolerated per C-007 — as **Info** findings naming C-007, so a
reader is told they were tolerated and why, and the severity keeps them out of the exit gate. The
exception is a narrow enumerated set (the clock/system memory bits), and TIA's auto-generated
`Tag_1` placeholder is **deliberately not in it** — an unnamed tag at a physical input is exactly
what a naming review should catch.

**Two more instances of the same defect, found in passing and fixed with it:**

- **TYPE files** carried the identical blanket stamp (`TYPE rule support: only C-001 … in Phase 1`)
  over 17 rules. Four were implementable against a UDT all along: **C-003** names `UDT_` in the same
  breath as `FB_`/`FC_`/`DB_`, **C-005**'s charset applies to a member name wherever it lives,
  **C-201**'s header-comment half applies to any content kind carrying its own `Comment` (the
  reasoning already written into `CheckC201HeaderComment` and already applied to DBs), and
  **C-406**'s declaration form reads members.
- **`preflight` never passed the reviewer the `TagTypeRegistry` it had already built** — it requires
  `--project`, so the index was always available — which meant **C-118/C-122/C-125 recorded
  themselves unrunnable on every preflight this tool has ever done**. One call site away from the
  tag-table hole. Now passed through, so those three actually run in preflight.

**The `--project` non-run is now split by whether the rule had a subject.** C-118/C-122/C-125 are
cross-file (FI-09). A block that references no `Step` register has no stepped sequence to place —
genuinely `not applicable`, with or without an index, and it does not gate. A block that *does* use
one, reviewed without `--project`, had a subject and was not judged: that is `NOT CHECKED` and it
gates. Measured on the real `FB_ShredderSequencer`: previously `SUMMARY: 0 finding(s)`, exit 0, with
three of its most relevant rules silently never run; now exit 2 naming all three, and exit 0 with
`checked, clean` once `--project` is supplied.

**Corpus impact, stated rather than discovered later.** `ir/test-project001/DefaultTagTable.ir` now
reports **58 C-001 errors** — every one a real violation (TIA's `Tag_1..Tag_54` placeholders at
physical addresses, and four `AirStarWord*` comms words at `%IW`/`%QW`). The 38 hand-authored
`DI…`/`DQ…` tags in the same file are silent, which is the evidence the rule discriminates rather
than flagging everything. Three of four project UDTs gain a C-201 (no header comment) and one a
C-003 (no `UDT_` prefix); the most recently authored, `UDT_HopperBlockageIO`, is clean.

59 new tests (`ReviewTagTableRulesTests`, `ReviewOutcomeTests`, plus the runner's own); 1069
converter tests green, up from 1010. Every implemented rule has a fixture that **violates** it as
well as one that conforms, and the gate is tested in both directions — a gate only ever exercised
against input that should trip it has not been tested either.

## `review` — harness scope: the reviewer knows a harness object when it sees one (2026-08-13)

A generated test-harness copy layer drew **24 × C-001 plus C-201 on every run**. The owner ruled
that doc 06's naming conventions govern PLC program content **authored for the plant**, and a
harness-generated object is neither plant nor hand-authored — so those findings were correct
against the letter of the rule and **wrong about their subject**.

*** THE REQUIREMENT WAS NEVER "SUPPRESS 21 FINDINGS", AND BOTH FAILURE DIRECTIONS ARE LIVE. ***
Twenty standing findings a run is how a reviewer learns to skim — *an over-firing gate decays into
a warning* — and a **silent exemption is indistinguishable from a correct pass**, which is this
repository's most-repeated defect. So nothing is dropped: the findings are **reported**, in a
counted, labelled, non-gating bucket, beside the derivation that classified the object.

### Derived, never declared

*** THERE IS NO `--harness` FLAG AND NO IR FIELD. *** A caller assertion is forgotten exactly when
it matters, and *a declaration is a transferred responsibility, not a verification*.

| content | derivation | if it cannot be decided |
|---|---|---|
| FC / FB / DB | **number inside the reserved band 9000–9999**, independently per number space (declared `docs/notes/test-environment-build-plan.md`) — structural, in the file's own IR, and not a name | — |
| **OB** | **excluded**: an OB's number is fixed by its **event class**, so the band cannot read one in *either* direction (OB80 is a harness object the band would call plant) | `Unclassified` — findings gate |
| **tag** | ⚠️ see below — **per tag**, from the blocks that reference it | `Unclassified` — findings gate |
| UDT | none exists: no number, and no referrer relation of this kind | `Unclassified` — findings gate |

`Unclassified` behaves **exactly** like `Plant` for gating and differs only in what the report
*says*. That is the point: *"this is plant content"* and *"no derivable property could tell me"* are
different facts, and collapsing them is how a classifier that **silently stopped running** would
look identical to one that examined everything and found no harness objects.

### ⚠️ The tag table was the hard case, and the honest answer is not to read its name

A tag table **has no number**, so its only table-level property is its **NAME** — and a name is
exactly what anything can be renamed into. *** SO THE TABLE'S NAME IS NEVER READ, AND NEITHER IS AN
`HX_` PREFIX. *** Classification is **per tag**, from the one derived property available: the set of
blocks that reference the tag, each of which is classified by the number band.

- **Harness** only when the tag has **at least one referrer** and **every** referrer is a harness
  block.
- **Any plant referrer ⇒ Plant.** One plant reader or writer makes a tag plant content.
- **No referrer at all, an unclassifiable referrer, or any unparseable corpus file ⇒ Unclassified.**
  The last is the **partial-corpus fence**: an unread file could hold the plant reference that
  changes the answer, so a partial corpus **refuses rather than mis-classifies** — *a comparison
  that could not be made must not be reported as one that came out negative*.
- A block's **own interface member names** are excluded from its references, so a harness block's
  input `Start` cannot vouch for an identically-named plant tag.

Laundering a plant tag therefore means **moving every reference to it into blocks numbered
9000–9999**, which breaks the plant program — where a rename costs nothing. Measured on the real
corpus: renaming the harness table to `PlantProcessTags` left it `HARNESS-GENERATED` (24/24), and
renaming the plant `DefaultTagTable` to `HarnessMirror` exempted **nothing** (0 harness / 41 plant /
60 unclassified, all 60 findings gating). Adding **one plant block that reads the same `HX_` tags**
collapsed the harness table to `0 harness / 24 plant` on the spot.

The corpus is the review batch **plus `--project`** when supplied — so reviewing a generated tag
table together with the copy layer that drives it is enough, and `--project` widens it to the whole
export, which is the stronger question because it can see a plant reference the batch omitted. The
corpus that answered is printed with the verdict.

### Scope, and what still gates

**C-001 and C-201 only.** `C-103` stays a finding on the harness copy layer — it is *behaviour, not
naming*, and it was recorded rather than silenced. A rule outside the scoped set is untouched no
matter what the verdict says, so a classifier gone wrong cannot silence anything else.

### The report says so, on every file

- `SCOPE:` — verdict **and its basis**, on **every file including plant ones**. This is what makes a
  broken derivation visible: a scope that only announces itself when it exempts something has an
  **invisible failure mode**.
- `HARNESS-SCOPE (reported, NOT gating - n finding(s))` — the findings themselves, printed.
- `HARNESS-SCOPE: n finding(s) … across m harness-generated object(s); k object(s) could not be
  classified` — the run total, **always printed including the zeros**: *a count of zero is a
  different fact from an absent section*.
- A new `RuleCheckStatus.CheckedHarnessScope` reads `checked, n gating finding(s) - <n> further
  finding(s) reported under HARNESS-SCOPE and NOT gated`. `FindingCount` keeps its single meaning
  everywhere — **the findings that gate** — so the status line never disagrees with the list printed
  beneath it.
- `preflight` folds them in under `review:harness-scope` with `Gates: false`. **`PreflightFinding.Gates`
  defaults to `true`**, so a future check is gating unless someone said otherwise — a non-gating
  default would install *"a warning is not a gate"* at the type level.

**Fail-closed by construction.** The scope parameter's default is `HarnessScope.Empty`, under which
blocks still classify from their own number but **no tag can be classified at all** — so a caller
that forgets to build one gets the full pre-change finding set, never a bypass.

**Mutation-tested in four directions**, on the committed fix: band never matches (9 red, including
every harness case), **band always matches — the dangerous direction — (11 red**, including the
laundering test and the plant-block did-not-run test), tags classified by `HX_` name prefix (5 red,
including the laundering test), and scope crept to include C-103 (1 red). 25 new tests
(`HarnessScopeTests`); 1145 → 1170 converter tests green.

*** THE DID-NOT-RUN TEST IS THE LOAD-BEARING ONE: *** `PlantBlock_SameDefects_StillDrawsItsC201FindingThroughTheSamePath`
reviews the *same block with the same defect and one number changed*. Without it, a classifier that
called everything harness would leave every other assertion in the file green.

## 🔴 `review` — C-410, the self-restarting timer: a silent block-killer nobody was checking for (2026-08-18)

A live job lost most of a day to a timer written `TON(X, IN := NOT X.Q, PT := …)`. On real hardware
it **fires once and then does not re-arm** — or re-arms only after an enormous, irregular delay.
Established by controlled experiment on the device, not inferred: the same program held a timer with
an ordinary Bool `IN` keeping perfect time beside a self-referential one that did not, and breaking
the self-reference through a plain Bool repaired it, measurably. **Six instances in one corpus, found
because a person happened to grep** — one of them a simulation layer's master clock, whose failure
mode was *every simulated rate multiplied by zero while every health bit stayed good.*

*That* is the argument for mechanizing it. The defect is silent, fatal to the block it sits in, and
**structurally detectable** — which is exactly what the mechanical floor is for: checks that survive
an agent choosing not to look. There were 18 mechanized rules and none of them covered it.

**C-410 (error), registered in `ReviewRunner.AllRuleIds` (now 19).** For every `TimerBinding` in a
network, it collects the tag references inside that timer's own `IN` and asks whether any reads that
same instance's own **output** — `<instance>.Q` (the measured case) or `<instance>.ET` (the same loop
through the other output port; C-408 has its own, separate quarrel with ET comparisons). Two
severities of the defect, both `Error`, distinguished by literal text in the finding:

| | shape | behaviour | reported |
|---|---|---|---|
| **total** | `IN := NOT X.Q` — no external term at all | never re-arms | `SELF-RESTART (TOTAL)` |
| **partial** | `IN := ArmBit AND NOT X.Q` | first cycle after each disarm→arm works; later cycles inside one armed period do not | `SELF-RESTART (PARTIAL)` |

Both gate. A partial still ships a block that silently stops timing after its first cycle, and *a
finding that only warns is the class this project has already recorded as getting skimmed* — so the
severity says "this gates" and the text says which of the two it is.

*** SCOPE IS DIRECT, ON PURPOSE, BECAUSE THE INDIRECT FORM IS THE FIX. *** The repair is to write the
timer's `Q` to a named Bool and gate the `IN` on that Bool. In the repaired corpus that coil sits in
the **same network** as the timer it feeds, so even a network-order-sensitive "the intermediate is
written no later than the timer" variant would flag the repair — and the hardware behaviour is known
only empirically (route it through a Bool and it works), which is nowhere near enough to guess which
indirect paths are still broken. **A rule that flags the fix is worse than no rule.** The rule's own
name states its coverage: *the `IN` reads its own instance.*

*** `.IN` IS NOT AN OUTPUT, AND THE FIRST DRAFT SAID IT WAS. *** Run over the committed corpora, that
draft reported three timers as deriving their `IN` "from its own output (`X.IN`)". They read the
timer's own **input image** as a latch's self-holding term (`Trigger OR Self.IN AND NOT Self.Q`) —
a different construction, and the finding's own sentence was false of it. *A finding that
misdescribes what it found is how a real rule gets switched off*, so the port filter is part of the
rule: `Q`/`ET` count, `IN`/`PT` do not. The self-holding one-shot still fires **on its `.Q` alone**,
which is the term measured to misbehave.

**Mutation-tested in five directions, on the shipped code.** Port test never matches → **10 red**, every
positive, negatives green. Port filter removed (any member of the instance counts) → **2 red**, exactly
the two `.IN` tests — the regression above, now guarded. Instance identity dropped at the call site
(any `Root.Q` reads as self) → **3 red**, including *another timer's Q is chaining, not self-reference*.
Severity collapsed to always-total → **2 red**, both partial tests, so the split is tested and not
decorative. Converse (semantics-preserving reorder of the two port comparisons) → **all green**, so the
suite is not red by coincidence. 17 new tests; 1389 → 1405 converter tests green.

**Corpus impact, stated rather than discovered later.** `ir/reference`: clean. `ir/test-project001`
and `ir/PlantAutoControl-bench`: **three PARTIAL findings between them**, all the same shape — a
self-holding one-shot whose drop-out term is its own `Q`. None is a `TOTAL`. Nothing in these corpora
is flagged for the repaired `Q → named Bool → IN` form, which is the property the rule had to have
before it could be run anywhere.

**`ReviewRunner.AllRuleIds` is now public and consumed by the tests.** It was private and referenced
by nothing, while `ReviewRunnerTests` carried a hand-copied duplicate that had **drifted to 12 of the
18** — so the "every rule gets exactly one status on every content kind" invariant was being asserted
against a stale subset, and a rule could be registered, never wired into the DB/TYPE/TAGTABLE
branches, and still pass.

## 🔴 `review` — C-603, and the exemption that had to be per-subject (2026-08-21)

A C-603 breach sat in `FB_ShredderSequencer` for weeks. `converter review` returned `0 finding(s)`
on the block every time it was run, because **C-603 was not one of the 19 mechanized rules**. It was
found by a human-directed read and cost two fix dispatches and two review dispatches. *A rule the
runner can see costs an exit code; a rule it cannot costs a review round.*

**C-603 (warn), registered in `ReviewRunner.AllRuleIds` (now 20)** — doc 06: *"Step membership is
enumerated, not ranged."* An ordered-range predicate over the C-118 phase register (`>=`, `<=`, `>`,
`<`, and the two-sided spans built from them) is a finding; `Step = n` and `Step <> n` never are —
neither is ordered, so neither can absorb the step C-120 exists to let a later revision insert.
"A step register" is **not** re-derived here: it is `HasStepLeaf`, the same leaf-named-`Step` test
C-118/C-121/C-122 already key off, so the rule cannot see a different sequence than its neighbours.

*** THE WHOLE DESIGN IS IN THE EXEMPTION, AND THE OBVIOUS EXEMPTION IS WRONG. *** Doc 06 allows a
range "where 'every future step inserted in this span belongs here too' is the stated intent
(comment)". Judging whether a paragraph of English states that intent is taste, and this runner does
not do taste. Judging whether the network *has* a comment is worthless — and measurably so:
`FB_ShredderSequencer` network 14 before the fix had **one comment covering three ranged coils**,
stated the range intent for exactly one of them (`PusherParkCmd`, still legitimately a range today),
and carried two unstated ranges beside it. **A has-a-comment exemption passes all three.**

So the mechanized test is per **subject**: *the network comment must name the write target whose
guarding condition carries the range*, matched on a word boundary against the target's leaf name
(`IO.PusherParkCmd` → `PusherParkCmd`) or its full dotted path. A comment that never mentions the
coil cannot have stated an intent for it; a comment that does is where a reader would go to find it.
The check is deliberately **one-sided** — it can prove the intent was *not* stated, never that it
was, and the finding text says what the reviewer still has to confirm. Same deferral shape as
C-121's "a named bit may be a genuine equivalent of `Step = <from>`".

- **A hyphen counts as part of the word.** No S7 identifier has one (C-005), so `reverse-run` is
  English compounding, not the coil `Run`. Wrong direction to be wrong in: an exemption granted on
  ordinary prose is worse than a finding raised on `PusherParkCmd-driven`.
- **One exception to the name anchor: a range guarding a write to the Step register itself.** `Step`
  is not distinctive prose in a sequencer — every network comment in the block contains the word, so
  anchoring there would auto-exempt every transition. Those are always findings; the repair is
  C-601's (name the condition to a bit, and the bit's name becomes the anchor) or enumeration, which
  is what C-121's `Step = <from>` transitions want anyway.

**EMPTY IS NOT CLEAN, expressed in the return type.** `CheckC603StepMembershipEnumerated` returns a
`C603Result`, not the plain `IEnumerable<Finding>` every other rule returns, because it has two
answers. `Findings` are the ranges it judged. `UnattributedRanges` are ranges it **found** somewhere
that is not a write's guarding condition (a `CALL` input argument, a `MOVE`'s `IN`), where it cannot
identify the subject to ask the comment about — so it did not judge them. `ReviewRunner` reads both:
a non-empty second list records C-603 **Skipped**, which is **exit 2, REVIEW INCOMPLETE**, the same
fail-closed treatment C-118/C-122/C-125 get when their subject exists and goes unjudged. A caller
reading only `Findings` would have seen an empty list and reported a pass — the return type is what
makes that impossible to write by accident. A block with no Step register at all is `NotApplicable`
with its own reason, never `Checked`-and-zero.

**Corpus impact, stated rather than discovered later.** Every `.ir` under `ir/` and `patterns/`:
C-603 reports `NotApplicable` or `Checked` with **zero** findings. No new exit-2 anywhere. The one
retained range in the corpus — `FB_ShredderSequencer`'s `PusherParkCmd`, `IO.Step >= 20 AND IO.Step
<= 40` — passes on its network comment naming it. Reconstructed from commit `27bc689` (the pre-fix
state), the same rule raises 3 findings and still passes `PusherParkCmd`, which is the discriminating
test (`ReviewC603Tests`, 17 tests, both directions on every claim).

⚠️ **C-603 is `warn`, and `ReviewOutcome.ExitCode` only gates on `Error` findings** — so a C-603
finding prints and counts but exits 0, exactly like C-120's and C-601-family severities generally.
The rule now *appears in the report*, which is the round it saves; making it *gate* is a severity
decision in doc 06, not a converter one.

