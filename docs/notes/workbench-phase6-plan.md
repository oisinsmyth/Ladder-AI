# Workbench Phase 6 — the area, derived

**STATUS 2026-08-23: DELIVERED — Y0, Y1, Y2, Y3.** `3026dbe` · `8bf2716` · `ea362c6` · `40dc0d9` ·
`466185a` · `66a0ab8`, plus `777fac0` and `182b3f9`, which were not planned items but are the same
subject: the reservation guard was **inert on three of the four paths that build a map**, and the rule
had two derivations. Converter **1,569 → 1,643** · harness **2,501 → 2,625** · openness-cli **871** ·
golden **194 of 194** · 0 warnings · no existing test edited.

> ⚠️ **THOSE FIVE FIGURES ARE THIS PHASE'S MEASUREMENT AT `7dbac3c`. THEY ARE HISTORY, NOT A BASELINE —
> DO NOT QUOTE THEM AS CURRENT.** Marked 2026-08-24 after a correction pass did exactly that: it took
> golden **194 of 194** from this line, attributed it correctly, hedged it, and labelled it *as of
> `7dbac3c`* — and it was still wrong, because `c53858e` had since added twelve tests. **A
> correctly-attributed stale count is still stale, and the attribution is what stops the reader
> checking.** Measured at HEAD on 2026-08-24: converter **1,643** · harness **2,724** · openness-cli
> **871** · golden **206** · budget script **23**. ➜ **The durable form of a count is a fresh
> measurement, not a better pointer** (`docs/18` §5z).

**What the phase actually bought:** the mirror no longer takes anybody's word for where it lives.
`served-area` reads the served width off the block that serves it — **both homes, or neither**;
`neighbours` derives who else is in the area **and who declares them**; the build stamp says **n of m**
and names what it skipped. All three print a denominator on every run including the zero case, and all
three state in their own output what they cannot see.

🔴 **Two limits that survive the phase and must not be forgotten.** Every one of these reads the
**staged corpus, never the CPU** — the committed block says 37 while the rig runs 1024, and this
toolchain passes that pair by construction. And the **foreign-occupant fixture is invented**: all 26
real `%M` claims in the reference area are the mirror's own, so a green suite is not evidence the
derivation reproduces the measured 256–323 / 53-tag collision.

~~⚠️ **No wave has run against a controller with any of this attached.** The demonstrations are
generate-only. That is a rig event and it has not happened.~~

✅ **IT HAPPENED THE SAME DAY — `ce2163b`.** Both lanes **3 of 3 PASS**, build stamp
`16#B85BE93C` → `16#95D8731D` **read back off the controller** and matching both result packages, area
exactly 1024 pinned from both sides, **stamp coverage 15 of 15, no gaps**
(`docs/notes/total-plant-run-feasibility.md:235-251`). 🔴 **And the value was not the green:** setting
the run up found that `Main` calls the virtual panel's FC and **no lane declared it**, so **every build
stamp before this one hashed a program short of an object the controller runs** — Y3's own stated
residual, occupied on the first real run. Reachability 6 of 6 → **7 of 7**; neighbour corpus 0 tag
tables → **1 tag table, 7 blocks**.

⚠️ **Two limits above survive the rig run unchanged and must travel with any quote of it:** every check
here still reads the **staged corpus, never the CPU**, and the **foreign-occupant fixture is still
invented**.

✅ **The element table is now re-labelled *"on demand, not a phase"* in `docs/18` §5**, as this plan
recommended under *The element table as written — NOT this phase* (this cited `:97` until 2026-08-24,
a line number stale in the commit that wrote it — see the correction under *Blocked / needs the
owner*). ⚠️ **That recommendation sat unapplied for the whole of the phase that replaced
it**, which is how `docs/18` §5 still read 🔨 *"Element-table widening · P2"* on the evening of the day
Phase 6 shipped and ran. **A recommendation to re-label is not applied by making it.**

---

**Planned 2026-08-23, offline, while Phase 5's redeploy was blocked.** Every claim below was verified
by reading the code or the artifact cited, not by quoting another document.

**Recommendation up front: `docs/18` §5's own Phase 6 (the element table) is NOT the right Phase 6,
and neither is the interpreter.** Build the thing that makes the mirror stop taking somebody's word
for where it lives and what is serving it.

**Size, honestly: smaller than Phase 4 or 5.** Four items — one 30 minutes of hygiene, one medium,
one medium-small, one small. **No rig, no Portal, no Openness re-approval, nothing committed that
needs a data-boundary permission.** The single biggest thing the workbench needs next — the third
conformance lane — is ~~**blocked on an owner ruling (B5)**~~, and that is said plainly here rather
than papered over with scope.

> 🔴 **B5 IS ANSWERED — THE LANE IS NOT BLOCKED ON THE OWNER.** See the correction under *Blocked /
> needs the owner* below; canonical record `docs/notes/owner-questions.md`. **This pointer was added
> 2026-08-24** because the correction block sat 359 lines further down and the file's own framing
> paragraph — the first thing a reader meets — still manufactured the blocker. The body below is left
> as written, per that block's stated policy; **what is not acceptable is the dead claim being the
> first one read.** Two more sites carry it: the table row under *Why not these* and the last bullet
> under *Risks*, both marked in place.

---

## Context

Phase 4 made a lane something the tool makes. Phase 5 measured the interpreter instead of building
it, and the measurement inverted the expected answer. What the rig work of 2026-08-23 then exposed is
a different hole, and it is the one with a **live, measured, bit-for-bit defect behind it**:

The mirror and a hand-authored virtual panel shared registers 256–323 on the running controller —
53 tags colliding, including the panel's master enable
(`src/harness/Harness.Map/ReservedRegion.cs:7-12`, `docs/notes/total-plant-run-feasibility.md:221-227`).
Nothing caught it, and the reason is written where it belongs: the allocator bounds the mirror
against the declared area, and `RegisterMap` proves the mirror disjoint *from itself*.
**Both ran. Both passed. Both examined something real that was not the thing at risk**
(`ReservedRegion.cs:14-21`) — the closed-check class CLAUDE.md now names.

`ReservedRegion` closes it, and its author wrote down what it does not do, in the binding where a
reader meets it:

> *"Until something DERIVES the neighbour list from the deployed program, this is a place to put the
> knowledge rather than a way to obtain it."* — `src/harness/Harness.Gate/BindingDocument.cs:63-66`

**Verified by reading: no committed artifact declares a single `reservedRegions` entry.** Repo-wide
grep over `*.json` / `*.md` returns zero. The guard is built, wired
(`Harness.Batch/BatchPlanner.cs:205-212`), unit-tested (37 attributes in `ReservedRegionTests.cs`,
7 in `ReservedRegionWiringTests.cs`) and has **never had a real neighbour to see.**

The adjacent field is worse, because it is a derivable field somebody *did* type, with a note
admitting it:

```
gen/test-project001/hopper-blockage-alarm/harness-binding.json:26
"_declaredRegistersNote": "37 is READ FROM the comms block, not chosen here:
 ir/test-project001/FB_Comms_ModbusServer.ir declares MB_HOLD_REG := P#M1000.0 WORD 37..."
```

That is §3.1's forbidden shape stated in the artifact's own words — *"a hand-typed derivable field is
only an opportunity to disagree with reality"* (`docs/18:140-142`). It is the argument that struck
4.4 in Phase 4, one document along. And the number is now 1024 on the rig while that file says 37:
**one measured constant, two homes** — M-18's shape.

---

## The central question, answered

### The element table as written — NOT this phase

`docs/18:634-638` and §3.5 already argue it down, and the argument holds:

- An unsupported type is **inexpressible, not mishandled**: `MirrorValueType` has exactly four members
  (`Harness.Map/CopyLayer.cs:18-38` — `Unstated`, `Bool`, `Int`, `Time`). The limitation surfaces as
  an unparseable binding. **That is the safe failure**, and it is the type system doing the work.
- Nothing is blocked on it. No lane, no gate, no run.
- **It would guess at an unanswered owner question.** `docs/18:902-903` Q2 — *"Which types must be
  observable for real blocks?"* — is open. Building `Real`/`DInt`/`Word` now answers the cheap half by
  fiat and leaves the expensive half (UDT members, array elements) where it is.
- Its own cost estimate disqualifies it as a *phase*: one enum member, one element-table row, one copy
  shape. **An hour's work the day a real block needs one** — arriving with that block's actual type in
  hand instead of three speculative ones.

➜ **Recommend re-labelling it in §5 as "on demand, not a phase"** rather than leaving it numbered as
the next thing.

*What Phase-6-as-written cannot possibly see:* whether the types it adds are the types a real block
needs. It has no consumer to be wrong about.

### The interpreter — NOT this phase, and its own note says why

C2 removed the *impossibility*, not the *cost*. Three arguments, all from
`docs/notes/preflight-interpreter-classification.md`:

1. **Vector supply is the binding constraint** — 2 of 96 and 3 of 96 assertions covered on the two
   blocks that have run a wave (`:141-146`). *"An interpreter with no vectors catches nothing."*
2. **The load-bearing element is an untested judgement** — that the 17 B rows do not collapse into two
   or three converter rules the way 8 of 10 A rows did (`:165-173`). The note names what would settle
   it and says it has not been attempted: *"Anyone reversing this recommendation should reverse it by
   producing two or three rule specifications... not by re-reading this page."* Building the
   instrument before the cheap disproof attempt is the opposite order from Phase 5's own method.
3. **B = 17 is an upper bound** — C = 0 is structural (`:81-92`), so some B rows are C rows in a B coat.

🔴 **An unrecorded cost of the C2 ruling, found by reading:**
`src/converter/Converter/Converter.csproj` is `OutputType=Exe`, `AssemblyName=converter`, and it
references `WaveControl`. **There is no library project to reference.** A clean
`src/harness → src/converter` reference therefore means either splitting a `Converter.Core` library
out — a converter build-layout change while every skill invokes `bin/Release/converter` — or
referencing an Exe and pulling `WaveControl` in behind it. **That belongs in `docs/18` §3.6 whatever
Phase 6 turns out to be.**

🔴 **CORRECTED 2026-08-23, and it cuts against the objection above.** *"Referencing an Exe"* was
written as though it were the awkward option. It is not: **`DeviceGuard.csproj` is itself
`OutputType=Exe`** (`AssemblyName=device-guard`, net8.0) and six harness projects already reference
it. Referencing an Exe is **established practice in this harness**, so that half of the objection is
weaker than stated. **The real cost is different and is stated nowhere else:** `WaveControl` is
netstandard2.0 and references nothing, so the *build* cost is near zero — but the reference would pull
`WaveControl` into the harness's build graph **transitively**, making `docs/18` §5 row 1.3's *"verified
zero project references between `src/harness` and `src/wave-control`"* false **by a route nobody
chose.** That is the sharp edge, and it is a contract question rather than a build one.

### Vector supply — real, and mostly not mechanisable

It is the measured bottleneck and it was considered as the phase. It is not, because §3.1 puts the
stimulus, the expected value and the settling intent in the irreducibly-authored column — and
deriving the expected value from the block makes every test pass (`docs/18:141-148`). What remains
mechanisable is a coverage ledger with four bucket counts and an oldest-deferral age, which the
`enumerate-assertions` skill **already claims as its output**, with `AssertionEnumerationSet.cs` and
`EnumerationStamper.cs` built underneath it. **Funding "vector supply" as a tooling phase would be
funding authoring throughput with a wrapper.** Recorded so it is not re-proposed as tooling; it is a
staffing and ordering question.

### The third conformance lane — the right next RIG event, and ~~blocked~~ **not blocked on the owner**

Phase 4 exists to make it cheap and it has never happened; **two is not N.** ~~Blocked on **B5 — which
block.**~~ 🔴 **B5 is answered — `FB_SiloSequence`** (correction under *Blocked / needs the owner*;
canonical record `docs/notes/owner-questions.md`). Two decisions of the lane's own stand ahead of it
instead. It also needs Portal and a deployment. **Phase 6 is deliberately the offline half that should
land before the mirror grows again**, because growing from one lane to two is precisely what walked
the mirror into the panel, a third lane is the next such growth, and **band D ends exactly at 1023
with zero slack above the panel** (704 clears four lanes at padded width ≤174 and no more).

### A producer for reserved regions — YES, and this is the phase

Judged higher-value than the guard **by the person who built the guard, in the guard's own docstring.**
Derived over authored; a computed refusal rather than a prose warning; a measured live defect behind
it; entirely offline; and the growth event that would re-trigger the original defect is the next thing
anyone wants to do.

---

## Scope

| # | item | size |
|---|---|---|
| **Y0** | A committed conflict marker, a ruled question still listed open, `portal-close` documented nowhere, three stale status lines | 30 min |
| **Y1** | **The neighbour list is DERIVED from the deployed program** — `%M` occupancy computed, compared against any declared list, refused on conflict, never silently absent | medium (the phase) |
| **Y2** | **`declaredRegisters` is read from the block that serves it** — both homes of the number compared | medium-small |
| **Y3** | **The build stamp states its own coverage** — hashed *n* of *m*, every unhashed object named | small |

**Order: Y0 → Y2 → Y1 → Y3.** Y0 first for Phase 4's reason: cheap, and it changes what the next
reader is told. **Y2 before Y1** because Y2 has a real committed fixture and Y1's must be invented —
Y2 proves the corpus-reading plumbing against a true artifact before Y1 leans on it.

---

## Y0 — four documents that are wrong right now

**(a) ✅ DONE 2026-08-23 (`a398763`).** A committed bare `<<<<<<< HEAD` at
`docs/notes/live-project-readiness.md:7`, introduced by merge `4f9c428`, in the page CLAUDE.md makes
the mandatory first read. Removed; nothing had been dropped; `git grep` confirms it was the only
marker in the tracked tree. ➜ **The grep is worth having as a mechanical check** — noted for the hook
backlog, not built.

**(b) `AITODO.md` lists a ruled question as open.** *"Still open for the owner: may `openness-cli` ever
close a stray Portal (B4)"* — B4 was ruled and the command built the same day
(`PortalClosePlanner.cs:60-63` quotes the ruling; `f9abeb1`). The recovery procedure eight lines above
tells the next session to trust that section.

**(c) 🔴 `portal-close` is documented in NO markdown file in the repository.**
`grep -rn "portal-close" --include=*.md .` returns nothing. Absent from `src/openness-cli/README.md`'s
command table and from CLAUDE.md's index. **A command that terminates OS processes — including, by
`--pid`, one with a project open — is reachable from no documented surface**, on the day it has never
been run.

**(d) Three stale status lines in `docs/18` itself**, the same shape as the two it already records:
`:540-544` Phase 3 still marked 🔨 *"specified, not built"* while `:4` says it is delivered;
`:548` Phase 4 *"in progress"* while the change log at `:934` says **DELIVERED**;
`:560` *"the mirror's 576-register area"*, superseded.

**(e) `src/harness/Directory.Build.props`'s comment still presents dependency-freedom as unqualified.**
`docs/18:255-258` predicted exactly this and it is still unamended. **Add the C2 ruling and the
Exe-not-library finding.**

**Also recommended here:** write **B5** and B4's closure into `docs/notes/owner-questions.md`. That
file says *"Last cleared: 2026-07-17"* and holds a 2026-08-05 batch; **the file that exists to hold
open owner questions contains neither of the two that are actually open.** A process finding, not just
a missing line.

**Verification.** Each replacement cites a commit, a source line or a result package — **never another
document.** Doc-to-doc citation is how three of Phase 4's five got there.

*What Y0 cannot see:* a status line stale in a file nobody grepped. The countermeasure is mechanical
and small — the conflict-marker grep — not attentional.

---

## Y2 — `declaredRegisters` is read from the block that serves it

**Defect.** `BatchPlanner.cs:179-183`: an **authored** number, required per lane. It flows into
`MirrorGeometry.ForCpu1214C`, into `MapAllocator`, and into `RegisterMap.MapHash`'s canonical form
(`RegisterMap.cs:461`, `declared=`), and therefore into the build stamp (`BuildStamp.cs:105`).
**So the stamp does hash a declared width** — the gap is narrower than it first looks, and the
correction matters: what the stamp is blind to is not the number, it is **whether the number is true
of the controller.**

The truth lives in the program, in **two places that can silently disagree**:

```
ir/test-project001/FB_Comms_ModbusServer.ir:31
  MB_SERVER(MbServer, EN := TRUE, ..., MB_HOLD_REG := P#M1000.0 WORD 37, ...)
ir/test-project001/FB_Comms_ModbusServer.ir:39   (SIDECAR)
  constant P#M1000.0 WORD 37 = 22 Any
```

`total-plant-run-feasibility.md:233-237` names it: *"A mismatch between those two lines is silent, so
both must be read back."* Nothing reads either. The converter already parses `P#` as a literal
(`Converter/Ir/IrParser.cs:1311-1322`, `:2498-2504`), so the fact is one parse away.

> **Change.** A producer reads the program corpus and returns the **served area**: base byte and
> register count, from the `MB_SERVER` call's `MB_HOLD_REG` area pointer **and** from the sidecar
> constant backing it, **refusing when the two disagree, naming both lines.** `BatchPlanner` compares
> it against the lane's authored `declaredRegisters` and **refuses a mismatch naming both numbers and
> both sources** — the derive-and-compare shape `docs/18:216-218` already calls *"strictly stronger
> than attribute"*. The authored field becomes optional-and-checked rather than required-and-trusted.

**Verification.** Against the committed reference project: derives `base 1000, 37 registers` — *a real
artifact, not a fixture written for the test*. Desynced copy (readable 37, sidecar 40) ⇒ refused
naming both lines. Binding declaring 576 against a corpus serving 37 ⇒ refused naming both numbers.
**Negative controls, run:** no comms block ⇒ `NOT DERIVED`, and the plan says the width was **declared,
not derived** — it must not silently fall back to trusting the authored value; **two** `MB_SERVER`
calls ⇒ refused as ambiguous, because guessing which serves the mirror invents the answer.
**Denominator, every run:** *"served area: base N, W register(s), derived from `<file>:<line>` +
sidecar"* or *"NOT DERIVED — n block(s) scanned, no MB_SERVER call"*.

*What this check cannot possibly see:* **whether the block it read is the block on the controller.** It
reads the corpus, not the CPU. The 1024 widening was proven by probing the device from both sides and
nothing here substitutes for that. Y2 buys only that the binding can no longer disagree with the
program that was staged — a strictly smaller claim, written into the report text so nobody widens it.

---

## Y1 — the neighbour list, derived

**Defect.** The area is shared and the mirror is the only occupant that computes anything. Everything
else is a declaration nobody has ever made.

> **Change, one implementation with two surfaces.**
>
> **In the converter** (it owns the only IR parser this project has; `cross-check` / `reachable-state`
> are the precedent for whole-corpus reference facts): a producer that walks a program corpus and
> returns **every `%M` claim inside a given area** — tag-table entries with absolute `%M` addresses,
> and every `P#M…` area-pointer literal in any block body. Each claim carries **the object that
> declares it** as its owner label, because *"a refusal that cannot say WHOSE space was hit sends the
> reader looking in the wrong place"* (`ReservedRegion.cs:31-35`).
>
> **A new verb, not an extension of `cross-check`.** On the real corpus `cross-check` emits **351
> multi-writer facts and 250 dead-member facts**, and that note's §6 is an argument about
> signal-to-noise. A refusal-critical fact does not go in that stream.
>
> **In the harness**: `BatchPlanner` consumes the document before allocating, converts byte spans to
> registers through the existing `MirrorGeometry.ReservingBytes` (already derived rather than typed,
> and rounds **outward** for the right reason), **excludes the mirror's own tag table by name from
> `CopyLayerNaming`** (the closed-set exclusion `BuildStamp.cs:192-199` already uses — never a
> hardcoded list), and **compares the derived set against any declared `reservedRegions`**: a declared
> region the derivation does not corroborate is reported; a derived region nobody declared is **used
> and named**; a conflict with the mirror is **refused**.
>
> **Route the document; do not take the project reference — this phase.** C2 permits the reference and
> this plan declines it, for a stated reason: the fact needed is naturally a document with provenance,
> which is `docs/18` row 1.3's own test, and **the first consumer of C2 should not also be the change
> that restructures the converter from an Exe into a library.** W5 already put `cross-check --json` in
> the batch's pre-plan path (`f126ffc`), so the mechanism exists. **But W5's fallback shape is wrong
> here and is deliberately not copied:** W5 falls back to a weaker check and says so; a *refusal input*
> that goes missing must **refuse**, with one named escape — `--neighbours declared-only` — which
> prints `NEIGHBOURS: NOT DERIVED` in the plan and in the result package.

**Verification.**

- **Committed, and the denominator is the honest part:** the reference project has **no neighbour** —
  `DefaultTagTable.ir`'s only `%M` tags are `%M0.7`, `%M1.0`, `%M1.2` (lines 58, 63, 64), all below
  base 1000. So the positive fixture must be **invented**, B1-style, and **a green committed suite is
  not evidence that the derivation reproduces the real collision.** That sentence goes in the plan, the
  tests and the commit — the three places Phase 4's W4 put it.
- **Job folder, re-run there, never committed:** the derivation runs against the live corpus and must
  reproduce the **256–323, 53-tag** collision measured on the rig. Fewer, or a different band, stops
  the item.
- **Negative controls, run:** no `%M` user in the area ⇒ *"0 neighbours derived over n tag table(s) and
  m block(s)"* — **printed, not empty-clean** (exit 2 is never a pass); an unparseable file ⇒
  `NOT DERIVED` naming the file, **never** a short denominator reported as clean (W5's finding, with
  `Reachability.cs`'s silent `continue` as the worked example); a declared region the derivation
  contradicts ⇒ refused naming both, because two parties disagreeing about what lives in the area is
  not something to average.
- **Denominator, every run including the zero case:** *"neighbours: k region(s) derived from n tag
  table(s) + m block(s); j file(s) unparseable; d declared, c corroborated."*

*What this check cannot possibly see:*
1. **An occupant reaching `%M` without declaring a tag or an area pointer** — indirect or
   pointer-computed access. The derivation is over declarations in the IR, not over execution.
2. **Anything not in the corpus it was given.**
3. **Whether the corpus is the deployed program.** Same limit as Y2, and the reason Y3 exists.

---

## Y3 — the build stamp states its own coverage

**Defect.** `BuildStamp.Derive` hashes whatever `--program` supplies and records a `ProgramManifest` of
what it hashed (`BuildStamp.cs:89-209`), naming its self-referential exclusions. **What it never states
is what it did not hash.** `ProgramManifest.HashedNothing` (`:273`) distinguishes the zero case; nothing
distinguishes the *short* case. The consequence was measured (`docs/18:794-801`): the stamp was derived
over **8 objects**, the parameter DB was not one of them, so *"compressing them changes the controller
without changing the stamp"* — **two result packages describing materially different programs would
carry the same stamp, and the verifying gateway would not notice.**

Phase 4's `LaneManifest` now supplies the missing denominator offline, **derived rather than authored**
(`Harness.Batch/LaneManifest.cs:1-50`), and its docstring already names the failure — *"a mistyped,
stale or short `--program` list produces a stamp describing a program nobody deployed."*

> **Change.** Report **hashed *n* of *m***, where *m* is the staged union corpus and the lane manifests,
> and **name every object present there that it did not hash**, split from the self-referential
> exclusions it already names. Carried into `ResultPackage` beside the manifest.

**Verification.** A 9-object corpus with a `--program` naming 8 ⇒ the ninth is **named** — *written and
observed to fail against the current binary first*. **Negative controls, run:** hashed set equal to the
corpus ⇒ *"hashed 9 of 9"*, printed on the passing case; no program under test ⇒ `HashedNothing`
unchanged and **distinguishable from "hashed 0 of 9"**, which is a different fact and must not render
the same.

*What this check cannot possibly see — and it is the important one:* **objects on the DEVICE that are
in no corpus and no manifest.** The virtual panel is exactly that. Closing it needs Portal, and this
phase does not do it. **So Y3's line must say "of the staged corpus", never "of the program", and the
residual must be stated in the same sentence** — otherwise it becomes a closed check, which is the
failure class this whole phase is about.

---

## Not in scope

| # | why not |
|---|---|
| **The element table** | Inexpressible ≠ mishandled; nothing blocked; would answer open owner question Q2 by fiat. On demand, not a phase. |
| **Building the interpreter** | Value bounded by vector supply (2/96, 3/96); its recommendation rests on an **untried** judgement. **The next interpreter action is the note's own: attempt the two-or-three rule specifications over the 17 B rows.** A session, not a phase — and it needs a **new per-item data-boundary permission**, because a rule specification derived from B rows is written in the shape of a defect. |
| **Vector supply as tooling** | The mechanisable slice is already `enumerate-assertions`'s declared output. Funding it would be a wrapper. |
| **The third conformance lane** | **The right next rig event.** ~~Blocked on **B5**.~~ 🔴 **B5 is answered — not blocked on the owner** (correction below; `docs/notes/owner-questions.md`). Y1/Y2 are the guards that should exist *before* the mirror grows a third time. |
| **`converter lease` — an agent holder** | Real, and **narrower than it looks**: `harness-batch run` self-supplies its own pid and is honest about it, because it spans the lease (`Harness.Batch/BatchCli.cs`, the `holderPid` default and its comment — at HEAD `:438-451`). 🔴 **This cell read `BatchCli.cs:188-196` until 2026-08-24, and those lines are the `--neighbours derive needs --converter` refusal — a different guard.** `BatchCli.cs` has not changed since `dc8308d`, so it was not drift; the wrong line was **copied out of here into `docs/18` §5 Phase 8** with its bare filename promoted to a full path, which is why it is corrected in both places. The gap bites the **agent across shells** case. But *what makes two agents different* is a recorded open contract question — the **agent-identity** one at `.claude/skills/design-for-testability/SKILL.md` §"agent identity is undefined", **not M-19**, which is the block-author/observability-map fencing gap and blocks nothing here (corrected 2026-08-24) — so this is **Phase 8**, not Phase 6. ⚠️ **Cheapest honest fix if wanted now:** an explicit `TtlOnly` liveness mode recorded in the lease file and printed in every reclaim, so a timer-only lease stops *looking* evidence-based. Offered, not planned. |
| **`portal-close` consulting `AttachedSessions`** | ✅ **DONE 2026-08-23 — this exclusion is spent.** It read: *"excluded because probing an unexplored API needs Portal."* Portal came free, it was probed, and the API **distinguishes attached from abandoned decisively and across processes** — a sweeper that is not the holder reads the holder's own pid. An empty Portal with a live session is now `Leave` unless named by `--pid`. Two traps recorded at `openness-api-surface-v20.md:69`: `IsActive` is `False` during a live attachment (it does not mean "attached"), and a hard-killed holder leaves no stale session, so the guard cannot be jammed. 🔴 **Residual:** an `OpennessInvisible` process is not in `GetProcesses()` at all, so it can never report attachment and is swept as blindly as before, protected only by the 5-minute age floor. |
| **Deriving `retentiveBytes`** | M-11 is recorded **NOT MECHANISABLE** — no Openness path reads the PLC-tags retain setting. Do not re-attempt it inside Y1 because it is adjacent. |
| **Splitting `Converter.Core` out** | A build-layout change to buy an in-process call where a document already works. **Record the finding in §3.6; do not act on it here.** |
| **Making `ReservedRegion` a map-hash input** | `MirrorGeometry.cs:76-81` gives the reason and it is right: a reservation changes nothing on the device, and hashing it would move every existing stamp for no fact on the controller. Y1 does not touch the hash. |

---

## Prerequisites — verified by reading

| what | needed by | state |
|---|---|---|
| `converter`, net8.0, Portal-free | Y1, Y2 | ✅ Rebuilding is free and safe; `converter.sln` is not an openness-cli rebuild. **`dotnet build -c Release` after any change (FI-73)** — Y1 and Y2 both touch it. |
| harness solution, no converter reference | Y1–Y3 | ✅ **No project reference from `src/harness` to `src/converter` exists today** (grep over all 35 harness `.csproj` — zero hits). This plan adds none. 🔴 **CORRECTED 2026-08-23: this row first read "dependency-free", and the harness is NOT.** Seven cross-solution project references exist — `src/device-guard` from six projects (`Harness.CmdInject`, `Harness.MirrorRead`, `Harness.MirrorView`, `Harness.Run`, `Harness.S7`, `Harness.Verify`) and `src/download-feedback` from `Harness.Device`. What is verified is the narrower claim, and only that. |
| `MirrorGeometry.ReservingBytes` | Y1 | ✅ Built, tested, rounds outward, refuses a span below base. |
| `ReservedRegion` + binding wiring | Y1 | ✅ Built (`5f6b39b`), wired and unioned across lanes (`e717dfe`). **Zero real declarations exist anywhere** — Y1 is its first real input. |
| `P#` area-pointer parsing | Y2 | ✅ `IrParser.cs:1311-1322`, `:2498-2504`; `ir/SPEC.md:989-996`. |
| A **real** fixture for Y2 | Y2 | ✅ `ir/test-project001/FB_Comms_ModbusServer.ir:31` + `:39`, Green-tier, both homes present. |
| A **real** fixture for Y1 | Y1 | ⚠️ **PARTLY — this row was WRONG, corrected 2026-08-23 by running the thing it described.** It said no committed fixture exists, having read only `DefaultTagTable.ir` (whose three `%M` tags *are* below base 1000, and now serve as a live below-base negative control on real data). But **`ir/test-project001/HarnessMirror.ir` is a SECOND tag table declaring 26 tags across `%M1000..%M1073`** — exactly the area. A real committed positive fixture exists, **for the mirror's OWN occupancy.** 🔴 **What remains invented is a FOREIGN occupant, which is the case that matters** — a green committed suite is still not evidence that the derivation reproduces the real 256–323 / 53-tag collision. B1's obligation stands, narrowed to where it actually applies. |
| `LaneManifest`, `ProgramManifest` | Y3 | ✅ `d3d5ab1`; `BuildStamp.cs:263-274`. |
| **Portal / the rig / Openness re-approval** | — | **None needed by Y0–Y3.** A Debug `dotnet test` on `openness-cli.sln` remains available (Debug and Release hold independent approvals). |
| **Data-boundary permission** | Y1 only | ⚠️ Needed **only if a live-job figure is quoted outward.** The committed suite, commit message and plan may say nothing about a site's `%M` occupancy — a band, an owner label or a tag count is job vocabulary. If a count is wanted in the commit, **ask first**. |

---

## Blocked / needs the owner

> ✅ **CORRECTION 2026-08-23 — B5 IS ANSWERED AND THIS SECTION'S HEADLINE CLAIM IS DEAD.**
> **The answer is `FB_SiloSequence`, and the lane is not blocked on the owner.** Two decisions of the
> lane's own now stand ahead of authoring it, and the canonical tracked record — with the shape stated
> and the live-run data boundary drawn — is **`docs/notes/owner-questions.md`**, not `AITODO.md` and
> not here. This plan is a *delivered* Phase 6 record; its body is left as written, but do not carry
> the blocked claim out of it.
>
> 🔴 **AMENDED 2026-08-24, TWICE, AND BOTH AMENDMENTS ARE THE SAME DEFECT AS THE ONE THIS BLOCK
> CORRECTS.**
> 1. **The block's own line citations were invalidated by the commit that wrote them.** It said *"Three
>    places … here, `:150` and `:367`"*. Those were exact at `7dbac3c`; the *same commit* that added
>    this block (`3926b65`) also inserted **18 lines near the top of this file**, so both numbers were
>    stale the moment they were written. The nearby *"as this plan recommended at `:97`"* moved the same
>    way. ➜ **Every site is now named by its SECTION, and no line number is cited into this file at
>    all.** A heading does not move when a paragraph is inserted above it, and this file is edited by
>    insertion.
> 2. **It under-counted.** *Three places* was **six**, and the sixth was a section HEADING. All of them
>    are now marked in place: the **Size, honestly** paragraph near the top of the file — the first
>    statement a reader meets, and hundreds of lines above this notice — the heading and body of **The
>    third conformance lane — the right next RIG event**, the table row under **Not in scope**, the
>    struck bullet in this section, and the last bullet under **Risks**. **The policy of leaving the
>    body as written is kept for interior prose; it is not kept for the file's framing paragraph or for
>    a heading**, because *"the wrong one is the one a reader meets first"* is the failure this whole
>    correction exists to fix.

- ~~🔴 **B5 — which block becomes the third conformance lane.** Recorded in **`AITODO.md` only.**~~
  ✅ **Answered — see the correction above.**
- **Q2 — element-table width** (`docs/18` §7, *Q2 — Element-table width*; this cited `:902-903` until
  2026-08-24, invalidated by that document's own growth). Answering it converts "on demand" into a scoped
  item with the right type list. Blocks nothing today.
- **Not blocked, offered:** the `TtlOnly` lease-liveness mode; a per-item permission for the
  interpreter rule-specification attempt.

---

## Verification, overall

**Baselines — and the method is stated because the suites were NOT run.** This was a read-only planning
pass, so test *cases* were counted by xUnit attribute, `cases = [Fact] + [InlineData]`:

| solution | attributes | `[Theory]` | **static cases** | last recorded |
|---|---:|---:|---:|---|
| converter | 1,603 | 64 | **1,539** | **1,539** (post-merge `4f9c428`) |
| harness | 2,561 | 81 | **2,480** | 2,438 (before Phase 5's harness commits) |
| openness-cli | 886 | 44 | **842** | 832 (before `portal-close`) |

**The converter figure matches the recorded one exactly, which is the check that the method is sound.**

🔴 **What this measurement cannot possibly see, stated because a count that looks like a run is worse
than no count:** it does not know whether anything **passes** or **compiles**. It under-counts three
files using `MemberData` / `ClassData` whose rows are generated. It cannot see `[Fact(Skip=…)]`.
**It is a floor, not a baseline.** ➜ **Run `dotnet test` on `converter.sln` and `harness.sln` before
starting and quote what you measured.**

- Every behavioural change gets a test **written against the old binary first and observed to fail.**
- Every negative control is **run**, not written: the no-comms-block case, the two-`MB_SERVER` case,
  the desynced-sidecar case, the zero-neighbours case, the unparseable-corpus case, the
  hashed-equals-corpus case, the `HashedNothing`-vs-`0 of 9` case.
- Every check prints its denominator on **every** run including the passing and zero cases.
- For each item, **what the check cannot possibly see** is written into the code, not only the plan.

**The live proof, when it comes.** The first batch plan after this phase, over the real lanes: (1) the
served width is **derived**, printed with its source line, and agrees with the binding — or refuses;
(2) the neighbour list is **derived**, and the panel band appears in it **without anybody typing 704**;
(3) the stamp line reads *n of n* with the named residual. Success is not "it planned" — it is that
**the three numbers came from the program, and the report says which file each came from.**

---

## Risks

- **Y1's strongest evidence cannot be committed.** Same shape as Phase 4's W4, same mitigation: the
  invented-fixture denominator goes in the plan, the tests and the commit.
- **Y2 makes a required field optional.** If the derivation errs in the *permissive* direction —
  deriving a wider area than is served — a map overflowing the real window would allocate cleanly and
  fail on the wire as a device fault, the exact symptom recorded from the other side.
  **Mitigation: the derived value never silently replaces the authored one where both exist;
  disagreement refuses.**
- **Y1 makes a refusal depend on a subprocess.** W5 accepted a converter subprocess in the pre-plan
  path and its stated risk was a silent fallback. This one must **refuse**, with one named escape that
  prints `NOT DERIVED` in two artifacts. If that escape becomes routine the guard is back to
  declared-only and nobody will notice — **so the escape's use is counted in the report, not just
  permitted.**
- **This phase produces no new rig capability and no budget movement.** Afterwards the loop looks
  exactly as it does today. The gain is that the next lane can grow the mirror without a person
  remembering what else lives in the area — worth having *before* the third lane, not after.
  **Said before starting, not after.**
- 🔴 ~~**The most valuable thing in the workbench right now is still blocked on one sentence from the
  owner.**~~ **B5 WAS ANSWERED — see the correction under *Blocked / needs the owner*.** If B5 is
  answered while Y1 is in flight, **stop and run the third lane** — Phase 6 is a guard for a growth
  event, and the growth event outranks the guard's polish. *(The conditional resolved: Y1 shipped and
  the lane is still unauthored, now behind two decisions of its own rather than behind the owner.)*
