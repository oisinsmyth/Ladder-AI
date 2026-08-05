# FI-39 `candidate-scan` + FI-36 — grounded design & feasibility study

**Status:** design only. No code written. Written 2026-08-05 against the substrate as it reads today.

**Scope.** FI-39's five mechanical checks (`docs/16-future-ideas.md` L544-555) plus FI-36's
per-instance interlock-completeness trace (L520-526). PC-side C# tooling in `src/converter/` — normal
software rules, not LAD/IR work. Two constraints are load-bearing throughout and are honoured by
every proposal below: the converter stays a **pure in-process file transformer** (no shell-out), and
every output is **facts, never verdicts** — the standing discipline for `review` / `cross-check` /
`trace`.

**Why this exists.** `docs/evidence/four-rung-design-validation-round2.md` RW-3: *"no mechanical floor
anywhere in the amended design… not one of them is computed by a tool, and `converter` gained nothing
in this amendment round."* `docs/evidence/PlantAutoControl-bench-autopsy.md` §3 cause 1: the AI reviewer
reading the same register as the AI coder is a **correlated** check. Both documents converge on the
same remedy — compute something.

---

## 0. Headline findings

Three things this study changes about the FI-39 / FI-36 framing:

1. **FI-36 is not blocked on FI-37.** FI-36's stated dependency is "an unambiguous condition list to
   trace against", assumed to come from FI-37's spec-explicitness discipline. But a condition list in
   the exact shape FI-36 needs already has a home in the tooling: FI-25's `BindingFile`
   (`src/converter/Converter/Trace/BindingFile.cs`) is a per-REQ JSON anchor file whose every field is
   optional. Adding one `guard` constraint to it gives FI-36's set-difference **today**, on existing
   substrate, in roughly 80 lines. This is the smallest and highest-leverage item in the whole study
   and it would have caught REQ-003 deterministically. See §7.

2. **Check 4 must read IR, not the D3 markdown render.** FI-39 frames the undriven-input audit as a
   rung-D activity over `code-structure.md` §D3. In the fixture D3 does not exist (STOPPED, no render
   produced), and building a markdown-boolean-expression parser to read a render that a coder will
   immediately turn into IR is wasted work. Read the generated `.ir` instead — the walk already
   exists (`TagReferences.AllDirectedUsages`). Cost: the check runs at the *check* stage, not at D1.
   That is an honest scope reduction, not a loss — the defect it catches (REQ-012) still gets caught
   before presentation.

3. **The `PlantAutoControl-bench` fixture is a strong regression fixture for checks 1, 4 and FI-36, and a
   weak one for checks 2, 3 and 5.** Checks 2 and 3 produce *empty* results on it (the rerun2
   artifacts already reconcile 174/174, and contain zero `verified-cross-block` rows). Check 5's
   expected output is a residue nobody has computed yet, so there is no known-correct answer to assert
   against — only an exact number replacing the artifact's self-reported `~190`. FI-39's claim that
   "all five checks have a known-correct expected answer" is true for three of them. §8 states each
   expected output concretely.

---

## 1. The substrate, as it actually reads

Everything proposed below is built on primitives that already exist. Cited so the design can be
checked rather than believed.

| Primitive | Path | What it gives |
|---|---|---|
| `ProjectUsageGraph` | `src/converter/Converter/CrossCheck/ProjectUsageGraph.cs` | Per full dotted tag path: `Writers` / `Readers`, each located to `(Block, Network, CoilKind?, Guard)`. Plus `GlobalDbMemberPaths`, `InstanceToFb`, `InstanceMemberPaths`, `CanonicalizeInstancePath`. Built by `Build(projectDir)` over `*.ir`. |
| `TagReferences.AllDirectedUsages` | `src/converter/Converter/Ir/TagReferences.cs` | The per-network read/write walk the graph is built from. |
| `ProjectIndex` | `src/converter/Converter/Preflight/ProjectIndex.cs` | Name index (tags / DBs / types / blocks) + the **batch-merge** idiom: `Build(projectDir, batchPaths)` folds files not yet in the project into the same index. Check 4 reuses this idiom for the generated caller block. |
| `DigestBuilder` | `src/converter/Converter/Digest/DigestBuilder.cs` | Per-file structural summary, tag roots, statement-kind counts, `ignoreErrors` tolerance. |
| `RequirementsParser` | `src/converter/Converter/TargetScan/RequirementsParser.cs` | The house precedent for markdown reading: line/regex parser over a documented strict format, with `BacktickToken` + an `Identifier` regex + a stopword list that already separates tag-shaped tokens from prose. Checks 2, 3 and 5 reuse these regexes directly. |
| `TraceRunner.WriteHop` | `src/converter/Converter/Trace/TraceRunner.cs` L167-195 | "Does this path have a writer anywhere, and are all writers disarmed?" — check 3 is literally this query. |
| `CrossCheckRunner.DeadInterfaceMembers` | `src/converter/Converter/CrossCheck/CrossCheckRunner.cs` L57-108 | Pools writers/readers across the FB-internal bare alias (`IO.Step`) and every `iDB.IO.Step` external alias. Check 4 is a **per-instance** view of this same data. |
| `IrBlock` interface fields | `src/converter/Converter/Ir/Model.cs` L492-512 | `StaticMembers` / `InputMembers` / `OutputMembers` / `InOutMembers` / `TempMembers`, each `IReadOnlyList<DbMember>` with nesting. This is how checks 1 and 4 enumerate an FB's interface without touching an instance DB. |

### The one substrate gap

`ProjectUsageGraph.GlobalDbMemberPaths` records **paths only** — `CollectLeafPaths` discards
`DbMember.Type`, `RETAIN`, and start value. Checks 1 and 5 both need type (to say "same-typed") and
RETAIN (R13's role-based escalation hint). This is the single new shared primitive the build needs:

**New: `Converter/SignalInventory/SignalInventory.cs`** — walks `*.ir` once, yields
`SignalLeaf(Path, Root, Leaf, Type, IsRetain, Origin)` where `Origin ∈ {global-db, tagtable,
fb-interface}`. Same `DbIrParser.ParseDb` / `TagTableIrParser` / `IrParser` prefix dispatch as
`ProjectIndex` and `ProjectUsageGraph`. Do **not** extend `ProjectUsageGraph` — `TraceRunner` reads
its shape as-is and its comment declares the verbatim keying deliberate. A sibling reader is the
lower-risk move.

### An FB's "status members" are not in the OUTPUT section

Grounding fact that changes check 1's design. `ir/PlantAutoControl-bench/TomraControlSystem.ir`:

```
INTERFACE
  INPUT
    inputWord0 : Word          … the data-link words
  OUTPUT
    OutputWord0 : Word
  STATIC
    Inputs : Struct RETAIN
      InHand : Bool
      AutoStartSignal : Bool
      …
    Outputs : Struct
      UPSEnable : Bool
      ShutdownComplete : Bool
      FaultActive : Bool
      …
```

The members a candidate set cares about — `Outputs.FaultActive`, `Inputs.AutoStartSignal` — live
under **STATIC**, inside the site's interface-UDT `Inputs`/`Outputs` structs. The SimaticML
INPUT/OUTPUT sections carry the data-link words instead. So a tool that filters on interface *section*
gets the wrong answer on the exact block the REQ-017 defect concerns.

**Design consequence:** enumerate *all* interface leaves and classify them by a computed fact instead
of by section — namely, **does the FB itself write this member, or read it?** `ProjectUsageGraph`
already knows: `Usages[suffix].Writers.Where(w => w.Block == fbName)`. `Outputs.FaultActive` is
written by `TomraControlSystem.ir` N154; `Inputs.AutoStartSignal` is read by it. That is a *computed*
status-vs-command discriminator, not a naming convention, and it is what keeps check 1's candidate set
defensible rather than noisy.

---

## 2. Check 1 — candidate set (`converter candidate-scan`)

> *"given a requirement's target + an instance, compute every in-scope signal that could satisfy it —
> IO-table members + the chosen FB's exposed status members. Makes 'more than one candidate' a
> computed fact."*

### Feasibility — **buildable now.** The flagship; build it.

Everything it needs exists. The only genuine design problem is the scope rule, and FI-39 already names
the failure mode: *"the candidate set needs a defensible scope rule … or it degenerates to noise."*

### Design

**Reject `--phrase <keywords>` as a filter.** R16's sketch (`round2.md` L607-610) proposes
`--phrase <keywords>`. Keyword-matching tag names is precisely the name-resemblance reasoning that
*produced* the REQ-019 defect (`Op`→running, `Ready`→remote, each "self-evident"). A tool that filters
by name resemblance launders the bias into a computed-looking number. Keep `--phrase` **advisory
only**: report both the unfiltered set size and the phrase-matching subset, and make the exit code key
off the *unfiltered* size. The tool's whole value is refusing to narrow.

**Scope rule (the defensible one):**

- **IO half** — `SignalInventory` leaves whose path matches `--scope <prefix|regex>` (e.g.
  `DiscreteInputs.FilterUnit1`), optionally narrowed by `--type Bool`.
- **FB half** — every interface leaf of `--fb <FBName>`, read from that FB's own `.ir` `IrBlock`,
  classified `status` (the FB writes it) / `command` (the FB reads it) / `unused` (neither) from
  `ProjectUsageGraph`, and filtered by `--direction status|command|any`.

Both halves are enumerations over a stated scope. Neither is a semantic judgment. The output says
"here are N things in scope"; a human or a skill decides which one the requirement means.

**Bonus fact worth including in v1 — the name-family bijection.** Round 2's residual survival path for
failure 4: *"Nothing mechanical detects that N same-typed signals from one instance-scoped name family
were assigned 1:1 by name resemblance."* Emit, per scope, a `family` block: the count of same-typed
in-scope signals and the count of FB members of matching direction. `3 IO signals : 3 FB command
members` is exactly the transposition-risk signature, computed. Cheap — it is a group-by over data
already gathered.

**New types:** `Converter/CandidateScan/{CandidateScanModel,CandidateScanRunner,CandidateScanOutputFormatter}.cs`
plus the shared `Converter/SignalInventory/SignalInventory.cs`.
**Reuses:** `ProjectUsageGraph`, `IrParser`/`IrBlock`, `AccessNode.FromDottedPath` for root splitting
(never hand-split a dotted path — `Clock_0.5Hz` exists).

**CLI:**

```
converter candidate-scan --project <ir-dir> --fb <FBName>
                         [--instance <iDB-or-instance-name>]
                         [--scope <path-prefix>]... [--type <TypeName>]
                         [--direction status|command|any] [--phrase <word>]... [--json]
```

**Exit codes:** `0` when the candidate set is a singleton or empty; **`1` when size > 1** — the
finding-bearing case, matching `tagstatus`'s "exit 1 if any proposed" convention. Non-zero here is the
mechanical trigger that makes rung C §2's blocking `Q-nn` non-discretionary. Usage/IO errors `1` with
a usage line on stderr, as everywhere else.

**Output shape** (text; `--json` mirrors it):

```
candidate-scan  project=ir/PlantAutoControl-bench  fb=TomraControlSystem  instance=TomraControlInst1
scope=DiscreteInputs.Tomra  type=Bool  direction=status

IO candidates (3)
  DiscreteInputs.TomraReady        Bool   read by PlantAutoControl N10
  DiscreteInputs.TomraRunning      Bool   read by PlantAutoControl N10
  DiscreteInputs.TomraComFlt       Bool   read by PlantAutoControl N10

FB status members (9)  — written by TomraControlSystem itself
  Outputs.FaultActive              Bool   written TomraControlSystem N154, N161
  Outputs.ShutdownComplete         Bool   written TomraControlSystem N…
  …

family: 3 same-typed IO signals in scope; 9 FB status members of matching direction
CANDIDATE SET SIZE: 12   (phrase 'fault' matches 2 of them — advisory, not a narrowing)
```

### Effort — **medium**

Two enumerations, one join against the usage graph, one formatter, one CLI handler. `SignalInventory`
is the only genuinely new walk and it is a near-copy of `ProjectUsageGraph.CollectLeafPaths` with the
type retained. Call it ~450-550 LOC including the formatter, plus tests.

**Unit tests would assert:** a two-member scope yields size 2 and exit 1; a one-member scope yields
size 1 and exit 0; an FB whose status members live under STATIC `Outputs :` is enumerated (the
regression that section-filtering would break); `--direction status` excludes members the FB only
reads; a dotted-literal tag (`Clock_0.5Hz`) roots correctly; `--phrase` changes the advisory count but
never the exit code; an unparseable corpus file degrades to a warning, not a crash (the `ignoreErrors`
contract every other scan honours).

### Risk / sharp edges

- **Scope-prefix authoring is the whole check.** A caller who passes `--scope DiscreteInputs.TomraCom`
  gets a size-1 set honestly. The tool computes the set for a *stated* scope; it cannot compute the
  scope. Mitigation: the output echoes the scope verbatim on the first line so an artifact quoting the
  tool also quotes its narrowing, making a narrow scope visible rather than invisible. State this
  limitation in the README — it is the honest boundary.
- **Instance-to-iDB resolution.** `--instance TomraControlInst1` is a *spec-artifact* name; the IR
  knows `TomraControlInst1` / `iDB_*`. Resolve via `ProjectUsageGraph.InstanceToFb` where possible and
  otherwise treat `--instance` as a label echoed into the output, never as a lookup that can silently
  fail. Do not guess a mapping.
- **`Origin=fb-interface` leaves are not signals.** Keep the two halves in separate output sections so
  nobody reads an interface member as bindable IO — rung C's contract forbids writing one into a
  requirement line (the `[iface, named-only]` carve-out).

---

## 3. Check 2 — relation-id set-difference

> *"parse `C-nn`/`P-nn` ids from `equipment-specs/`, the D2 ledger, the D3 render terms and the derived
> register; assert all four reconcile."*

### Feasibility — **buildable now, and cheap — but be clear what it buys**

It buys a **regression guard on a currently-clean artifact**, not a demonstrated catch. Measured on
the fixture this session:

```
equipment-specs/*.md  '- **Cn**' / '- **Pn**' bullets : 27+27+27+30+34+29 = 174
code-structure.md     '| Cn |' / '| Pn |' ledger rows :               174
requirements.md       '| REQ-nnn |' rows              :               174
code-structure.md     D3 render terms                 :  0 — D3 STOPPED, no render produced
```

Three sets already reconcile; the fourth does not exist. So the check's fixture value is "assert 0
differences and 1 absent leg", which is a regression test, not a catch.

That is still worth building, for one reason that matters: **those counts are currently hand-asserted
inside the artifact** (`| Rows in this ledger | **174** |`). Mechanizing a self-reported count is
exactly the "computed rather than asserted" principle RW-3 is about. It is just not the check that
finds the next defect.

### Design

**The ids must be qualified by instance.** A bare union of `C1…C34` across six specs is vacuous —
`FilterUnitInst1`'s `C1` and `TomraControlInst1`'s `C1` are different relations. The comparison key is
**`(instance, relation-id)`**. In each artifact the instance comes from the enclosing structure:

| Artifact | Instance source | Relation-id source |
|---|---|---|
| `equipment-specs/<Instance>.md` | the **filename** | `- **C3** …` / `- **P9** …` bullets |
| `code-structure.md` D2 | `## Ledger — \`FilterUnitInst2\`…` heading | first cell of `\| relation \| disposition \| evidence \| precondition class \|` |
| `requirements.md` | `### \`FilterUnitInst2\` — …` heading | the `Rel` column of the 6-column REQ table |
| `code-structure.md` D3 | `NETWORK "…" instance: iDB_…` | trailing `[C2]` / `[P1,P2]` term tags |

**Format facts the parser must encode (measured, not assumed).** The SKILL contracts and the real
artifacts diverge, and the artifacts win:

- Ids are **`C1`/`P1`** — unhyphenated, unpadded. `C-nn`/`P-nn` appears only as prose naming the *set*.
- The D2 column header is **`relation`**, not `relation-id`; the last is **`precondition class`**
  (space, not hyphen).
- Empty precondition class is an **em-dash `—`**, not blank.
- `rebind` is rendered **bold** (`**rebind**`); the other dispositions are bare.
- `requirements.md` in this fixture is a **6-column table** (`REQ | Rel | Text | Class | Provenance |
  Open`) and *declares its own deviation* from the reference project's heading-block form. A parser
  that only handles `### REQ-nnn — title` (which is what `RequirementsParser` does today) reads this
  register as zero REQs. **Extend `RequirementsParser` with a tabular mode; do not fork it.**
- `code-structure.md` uses **multiple H1s** (`# D0`, `# D1`, `# D2`…). Do not assume one H1 per file.
- D3's absent form is `## **STOPPED — all six instances. No render produced.**` — detect and report
  the leg as `absent`, never as `0 differences`.

**New types:** `Converter/RelationReconcile/{RelationReconcileModel,SpecRelationParser,LedgerParser,RelationReconcileRunner,…OutputFormatter}.cs`.
**Reuses:** `RequirementsParser`'s regex idiom and `BacktickToken`; extended with a tabular REQ mode.

**CLI:**

```
converter relation-reconcile --specs <equipment-specs-dir>
                             --ledger <code-structure.md>
                             --register <requirements.md> [--json]
```

**Exit codes:** `1` on any non-empty set-difference in any direction (`drift-check`'s convention —
a reconciliation tool whose job is to find a mismatch). An `absent` leg (D3 stopped) is reported and
does **not** on its own set exit 1 — a stopped rung is a legitimate state, not a drift.

**Output:** a 4×4 matrix of pairwise differences plus the per-leg counts, then the actual missing
`(instance, id)` pairs listed, capped and sorted.

### Effort — **small**

Three focused readers of a documented format, one set algebra, one formatter. ~350-450 LOC. The
readers are the work; the reconciliation is twenty lines.

**Unit tests would assert:** a relation present in a spec and missing from the ledger is reported in
the right direction with its instance qualifier; two different instances' `C1` do not cancel each
other (the vacuity regression); a `— ` precondition cell parses as empty rather than as a value; a
bold `**rebind**` disposition parses to `rebind`; the tabular register form yields 174 rows and the
heading-block form still yields its rows; a `STOPPED` D3 reports `absent` and exits 0 when the other
three reconcile.

### Risk / sharp edges

- **Format drift is the whole risk**, and it is already demonstrated: five of the documented divergences
  above are between a SKILL.md written this month and an artifact produced this week. Mitigation is the
  `TargetScan` precedent — parse strictly, and when a section header is not found, say so loudly
  (`ledger: no '| relation |' table found under any '## Ledger' heading`) rather than reporting zero
  rows as a clean reconcile. **A silent zero is the failure mode that would make this check worse than
  nothing.** Make "leg parsed 0 rows" a hard error, not a pass.
- **Instance-name spelling must match across three artifacts.** It does in the fixture, but the check
  should report a normalized-name near-miss (`FilterUnitInst2` vs `FilterUnitInst 2`) as its own
  finding class rather than as two missing relations.

---

## 4. Check 3 — probative-citation check

> *"a `verified-cross-block` precondition must cite a tag that actually has a writer, not merely a
> declaration."*

### Feasibility — **buildable now, trivially — but it has no positive fixture case, and it should not be its own subcommand**

The query is already written: `TraceRunner.WriteHop` (L167-195) answers "does this path have a writer
anywhere, and are all writers disarmed?" against `ProjectUsageGraph`. The only new work is extracting
the cited tag from a D2 evidence cell — which is check 2's parser, on the same file, in the same pass.

**Fold check 3 into `relation-reconcile`** as an additional finding class rather than building a
second subcommand that parses `code-structure.md` a second time. One reader, one file, two questions.

### The fixture reality

The rerun2 ledger contains **zero `verified-cross-block` rows** — the artifact says so explicitly
(`**\`discharged\` is used ZERO times.**`); the classes actually present are `verified-in-block` and
`—`. So on this fixture the check reports "0 citations to verify." Its demonstrated case is the
*hypothetical* row round 2 constructed (`round2.md` L131-133), which must become a synthetic test
fixture.

What is real and computable today is the underlying fact the loophole turns on, confirmed by grep this
session:

```
ir/PlantAutoControl-bench/Control.ir:14:    FansShutdownReady : Bool     ← the only occurrence
```

— a bare declaration, no writer anywhere in the corpus, and `gen/PlantAutoControl-bench/PlantAutoControl.ir`
L48/49/63/64/249 **reads** it. Reader-with-no-writer is exactly the fact `cross-check`'s dead-member
pass already emits.

### The false-positive trap — the single biggest risk in this study

`ir/PlantAutoControl-bench/` is a **partial export**: 34 files, the FB library and the DBs, but not the
as-built `PlantAutoControl` FC that presumably writes `FansShutdownReady`. "No writer" therefore means "no
writer *among the blocks exported into this directory*", which is a much weaker statement than "this
flag is never written". If the tool prints "no writer" flatly, it manufactures a false finding on every
partial export — and this project's exports are routinely partial.

**Mandatory output discipline:** every no-writer fact states its denominator —
`no writer among the 34 blocks in ir/PlantAutoControl-bench/ (partial exports are normal; this is a scope
fact, not a defect)`. Same wording discipline `cross-check` uses for dead members. This applies to
FI-36-min and check 4 equally.

**CLI:** none of its own. Findings appear under `relation-reconcile`'s output as a `citations` section;
they contribute to its exit-1 condition.

### Effort — **small (incremental)**

~80-120 LOC on top of check 2, mostly the evidence-cell tokenizer.

**Unit tests would assert:** a `verified-cross-block` row citing a member with a writer passes; one
citing a declaration-only member is reported with its denominator; a row citing a member with writers
that are *all* disarmed (`DisarmAnalysis.IsProvablyFalse`) is reported separately from "no writer" —
built-but-switched-off is a distinct fact, and `WriteHop` already distinguishes them; a `verified-in-block`
or `—` row is skipped, not checked.

### Risk / sharp edges

- **The evidence cell is free prose.** Real cells look like
  `` `FilterUnitSystem.ir` N1 (`NOT IO.FaultActive` in `TryRunMotor`), N10 `` — several backticked tokens, of
  which the file name, a network label and an expression fragment are all *not* the cited tag. Do not
  guess "the tag". Extract **all** backticked identifier-shaped tokens, classify each against
  `SignalInventory` / `ProjectUsageGraph`, and report **per token**: `resolves to a member with 2
  writers` / `resolves to a declared member with no writer` / `does not resolve`. A row where *no*
  token resolves to a written member is the finding. That is a fact per token and a fact per row —
  never a verdict on the argument.
- **Do not attempt to check that the writer's guard *entails* the semantic claim.** R10's stronger
  wording ("quoting its full guard") is a human duty. The tool can print the guard; it cannot judge it.

---

## 5. Check 4 — undriven-FB-input audit

> *"enumerate a chosen FB's interface members, mark driven/defaulted from the render."*

### Feasibility — **buildable now, strong fixture case — and it is ~70% already computed by `cross-check`**

`CrossCheckRunner.DeadInterfaceMembers` already pools writers and readers across the FB-internal bare
alias and every `iDB.` external alias, and emits a `DeadMemberFact` with `DeadMemberScope.InterfaceMember`
when either side is empty. What is genuinely missing is three things:

1. **Per-instance resolution.** `cross-check` deliberately canonicalizes to `(FB, suffix)` and pools
   across *all* iDBs of the FB. That collapse is correct for its own question and **wrong for this
   one**: if `MotorVSDInst3.IO.RotationSensor` is driven and `MotorVSDInst1.IO.RotationSensor` is not,
   the pooled view shows the member as alive and the gap vanishes. REQ-012 is exactly that shape — one
   instance of a shared FB. **This is the real new value in check 4.**
2. **Input-side restriction.** Report on members the FB *reads* (its inputs), where "undriven" means
   something; a status member the FB writes and nobody reads is `cross-check`'s question, not this one.
3. **The unclaimed-signal name join** — R13's decoupled form: *"an undriven input that name- or
   role-matches **any** IO signal in the project — whether or not rung C listed it as unclaimed — is a
   HARD FAIL."* Decoupled is the right call: round 2's RW-2 showed belt and braces failing together
   when the escalation was conditioned on the sweep.

### Design

**Read IR, not the D3 markdown render.** Merge the generated block into the graph the way `ProjectIndex`
merges batch paths — `--caller <file.ir>` (repeatable) parsed and walked alongside `--project`'s
corpus. This is the design decision from §0.2. Consequence: the check runs after the coder has written
IR, at the check stage, not at D1. Accept it.

**Driven / defaulted / undriven, computed:**

- `driven` — at least one writer of `<iDB>.<suffix>` (or of the bare suffix inside the FB itself)
  among corpus + callers.
- `disarmed` — writers exist but all are provably-false-guarded (`DisarmAnalysis.IsProvablyFalse`).
  Distinct from both, and this is the state review-functional treats as not implemented.
- `undriven` — no writer, and the member has a **non-null start value** in the instance DB → report the
  start value as the effective constant. That is `defaulted` stated as a fact, and it is available:
  `TraceRunner.LoadStartValues` already collects it (L237-264).
- `undriven, no default` — no writer, no start value.

**New types:** `Converter/UndrivenScan/{UndrivenScanModel,UndrivenScanRunner,UndrivenScanOutputFormatter}.cs`.
**Reuses:** `ProjectUsageGraph` (including `CanonicalizeInstancePath` and `InstanceMemberPaths`), the
start-value collector (lift `LoadStartValues`/`CollectStartValues` out of `TraceRunner` into a shared
helper rather than copying it), `SignalInventory` for the name join, `DisarmAnalysis`.

**CLI:**

```
converter undriven-scan --project <ir-dir> --fb <FBName>
                        [--instance <iDB>]... [--caller <file.ir>]... [--json]
```

**Exit codes:** `1` if any interface input is `undriven` or `disarmed` for any instance in scope
(`drift-check` convention). `0` when every input of every instance has an armed writer or a stated
default.

**Output:** a matrix — rows are interface members, columns are instances, cells are
`driven N3` / `undriven (default FALSE)` / `disarmed` — plus, per undriven cell, the name-join list of
project signals sharing a leaf token that no caller references.

### Effort — **small-to-medium**

Most of the graph work exists. ~350-450 LOC, and less if the start-value helper is lifted cleanly.

**Unit tests would assert:** an FB with two iDBs where one drives a member and the other does not
reports *per instance*, and the driven one does not mask the undriven one (the regression that makes
this check worth building at all); a member with a writer guarded `NOT AlwaysTrue` reports `disarmed`,
not `driven`; a `--caller` file not present in `--project` still contributes writers (the batch-merge
idiom); an undriven member with a start value reports the value; the name join surfaces a
project signal sharing a leaf token that no caller references, and does not fire on a signal that is
referenced.

### Risk / sharp edges

- **Name-token matching is a heuristic and must be labelled one.** `RotationSensor` ↔ `AirStarDCRotSen`
  matches only under a fuzzy token rule (`RotSen`). Emit it as an explicitly-labelled *hint* section
  with the rule stated, never as part of the exit-1 condition. The exit code keys on `undriven`, which
  is a hard fact; the name join is colour.
- **`Inputs :` vs `IO :` struct naming is not universal.** `TomraControlSystem` uses `Inputs`/`Outputs`;
  `FilterUnitSystem` and `MotorVSDSystem` use `IO`. Do not hard-code a struct name — classify by
  read/write direction from the graph (§1), the same discriminator check 1 uses.
- **A member both read and written by the FB** (an internal latch exposed on the interface) is neither
  cleanly input nor output. Report the direction pair as a fact and leave it out of the exit-1 set.

---

## 6. Check 5 — project-level unclaimed-signal sweep

> *"every IO/global-DB signal bound by some spec or listed unclaimed."*

### Feasibility — **buildable now; highest false-positive surface of the five; medium value**

The denominator is exact and cheap: `SignalInventory` over the corpus enumerates every global-DB leaf
and tag-table tag. The numerator — "claimed by a spec, or listed unclaimed with a reason" — is a
containment test against markdown, and that is where the fragility lives.

The concrete win: `unclaimed-signals.md`'s own `## Sweep result` reports **approximate** counts
(`~190` swept, `~80`, `~40`). Replacing three tildes with three computed integers, and listing the
exact residue, is a real conversion of an assertion into a fact. That is the case for building it.

### Design

**Claimed-set extraction, three sources, one rule.** Reuse `RequirementsParser`'s `BacktickToken` +
`Identifier` regex + `TagStopwords` (already written and tested to separate tag-shaped tokens from
prose — the single best reuse in this study):

1. `equipment-specs/*.md` — every backticked identifier-shaped token, anywhere in the file. Coarse by
   design: "mentioned in a spec" is the honest containment test, and a spec that names a tag without
   binding it is a *different* problem that this check should not pretend to detect.
2. `unclaimed-signals.md` — the per-DB disposition tables, which are the only machine-readable part.
   **Parse only those.** The `### Q-Cnn — <title>` findings are narrative and stay narrative.
3. `requirements.md` — the `Text` and `Provenance` columns' backticked tokens.

**The qualification problem.** `unclaimed-signals.md` lists **bare leaf names** under a per-DB heading:

```
### `DiscreteInputs` (DB 15)

| Members | Disposition |
|---|---|
| **`OSCRotSen`** | **UNCLAIMED — Q-C09** |
| `SpareDI : Array[0..4]` | UNCLAIMED — spare, non-control-implying by role; no finding |
| *(`BypassOSCRotSen` — does not exist)* | **proposed / missing — Q-C09** |
```

So a leaf must be qualified by its enclosing `### \`<DBName>\`` heading to become `DiscreteInputs.OSCRotSen`.
A cell may hold a comma-separated list of a dozen backticked names. A cell may name a signal that
**does not exist** (`*(\`BypassOSCRotSen\` — does not exist)*`) — those must be recognized and excluded
from the claimed set, or the sweep silently "claims" a nonexistent signal. A cell may carry a type
suffix (`SpareDI : Array[0..4]`) — take the first token only.

This heading-scoped, list-splitting, negation-aware read is precisely why this needs a focused reader
rather than a repo-wide grep, and why it is the largest parser of the five.

**Hard rule 2.** The sweep enumerates signals from `ir/<project>/`, which by project policy never
contains F-content. The tool must not acquire any capability to open safety blocks, and its output
should carry the same `safety` disposition vocabulary the artifact uses so an excluded item reads as
excluded rather than as missing. State this in the README entry.

**New types:** `Converter/SignalSweep/{SignalSweepModel,UnclaimedDocParser,SignalSweepRunner,…OutputFormatter}.cs`.
**Reuses:** `SignalInventory`, `RequirementsParser`'s token regexes, `ProjectUsageGraph` (to annotate
each residual with its reader/writer counts — a residual signal that *is* referenced by generated logic
is a different and more interesting fact than one referenced by nothing).

**CLI:**

```
converter signal-sweep --project <ir-dir> --specs <equipment-specs-dir>
                       [--unclaimed <unclaimed-signals.md>] [--register <requirements.md>] [--json]
```

**Exit codes:** `1` if the residue is non-empty (`drift-check` convention). Expect a non-empty residue
to be *normal* on a partial run — so the residue must be presented as a worklist with reader/writer
annotation, not as an error, and the README must say so or the non-zero exit becomes noise people
learn to ignore.

### Effort — **medium**

The `unclaimed-signals.md` reader is the bulk. ~450-600 LOC.

**Unit tests would assert:** a leaf listed bare under `### \`DiscreteInputs\`` is qualified to
`DiscreteInputs.OSCRotSen` and counted claimed; a `*(\`X\` — does not exist)*` cell does **not** claim
`X`; a comma-separated multi-signal cell claims each; `SpareDI : Array[0..4]` claims `SpareDI`; a
signal mentioned in no artifact appears in the residue with its reader/writer counts; the swept
denominator is exact and stable across runs.

### Risk / sharp edges

- **"Mentioned" ≠ "bound."** The containment test cannot distinguish a spec that binds a signal from
  one that merely names it in a caveat. That is a deliberate under-claim — it makes the residue a
  *lower bound* on what is genuinely unswept, which is the safe direction, and it must be stated in
  the output header so nobody reads the residue as complete.
- **The claimed set is a union over prose.** A spec that happens to backtick an unrelated tag in a note
  removes it from the residue. Same direction of error (under-report), same disclosure duty.
- **Array and struct granularity.** `SpareDI : Array[0..4] of Bool` is one leaf to the walker and five
  signals to an engineer. Report the declared form; do not expand.

---

## 7. FI-36 — per-instance interlock-completeness trace

> *"for each machine, take the spec's explicit condition list and verify every listed condition appears
> in the block's guard — a set-difference, not a re-interpretation."*

### Feasibility — **the full form is blocked; a minimal form is buildable now and is the best item in this study**

**Blocked, in its stated form.** FI-36 needs a per-instance list of *conditions over named signals*.
Today no artifact contains one:

- The equipment specs' interlock lines are prose bullets over plant language —
  `- **P3** on controlled shutdown, hold until the Shredder (\`ShredderControlInst1\`) reports its
  shutdown complete \`[A + B-17]\` **(Q-C12)**`. A signal is sometimes backticked, often not, and the
  boolean structure is English.
- `requirements.md` is derived from those and no more explicit.
- **The D3 render is the artifact that would satisfy FI-36 exactly** — explicit boolean over real
  interface members with a relation-id tag per term. In the fixture D3 is `STOPPED`, and NW-3 records
  that the render never reaches the coder anyway.

So FI-36's true dependency is not FI-37's prose discipline — it is **"a D3 render exists, in a form a
parser can read."** That is a more actionable statement of the blocker than the FI entry currently
carries, and it means FI-36-full should be sequenced behind R12 (carry the render to the coder), not
behind FI-37.

### FI-36-min — buildable today, ~80 lines, catches REQ-003

`Trace/BindingFile.cs` is already a per-REQ JSON anchor file with every field optional, consumed by a
runner that walks `ProjectUsageGraph`. Add one constraint:

```json
{ "req": "REQ-003",
  "coil": "FilterUnitInst2.IO.Shutdown",
  "must_contain": ["PlantControl.FansShutdownReady",
                   "AirStarInst1.Outputs.ShutdownComplete"] }
```

New hop `HopKind.GuardContainment`: locate the writer(s) of `coil` in the graph, take each write's
`Guard` `Expr` (already carried on `UsageSite` for FI-25 v2), walk it for `Expr.TagRef` paths, and
**set-difference** `must_contain` against that set. Report present / missing per term, with the writing
site. That is a set-difference over signal identity — no re-interpretation, exactly what the autopsy
asked for.

Run against the fixture, `gen/PlantAutoControl-bench/PlantAutoControl.ir` L49:

```
COIL FilterUnitInst2.IO.Shutdown := NOT (PlantControl.Status >= 1 OR PlantControl.Status = -1
  AND NOT PlantControl.FansShutdownReady) AND NOT FilterUnitInst2.IO.HandIntervention
  AND PlantControl.Status = -1
```

→ `PlantControl.FansShutdownReady` **present**; `AirStarInst1.Outputs.ShutdownComplete` **MISSING**.
The REGRESSION, computed, on the real generated file, regardless of how anyone reads the `/`.

**Reuses:** `BindingFile`, `TraceRunner`, `ProjectUsageGraph.UsageSite.Guard`, `TagReferences`.
**New:** one `GuardConstraint` record, one hop method, one `HopKind` value, formatter cases.

**CLI:** none new — `converter trace --binding <bindings.json> --project <ir-dir>` gains a hop.
**Exit code: stays `0`.** `trace` is a declared facts provider ("facts not verdicts; exit 0") and the
`cross-check`/`trace` contract should not be broken to accommodate a new hop. The missing-term fact is
in the output and in the JSON; a skill that wants to gate on it reads the JSON.

### Effort — **small.** ~80-120 LOC plus tests. The cheapest item here and the only one with a
deterministic catch on the worst documented failure.

**Unit tests would assert:** a guard containing both terms reports both present; a guard missing one
reports it missing and names the writing site; a coil with no writer reports `no output path` (the
existing `WriteHop` verdict), not a false "all missing"; a term reached only through a *disarmed*
writer is reported present-but-disarmed; a coil with two writers reports per-site containment rather
than a union (a term present in one network and absent in another is the multi-instance shape that
matters).

### Do not build: the "strongest-available-guard" companion

FI-36 proposes flagging use of a narrow signal (`ComFlt`) when the FB exposes a broader one
(`FaultActive`). "Broader" is a semantic lattice over signal meaning; the converter cannot compute it
without a hand-authored ontology, and a hand-authored ontology is an artifact somebody must maintain
and can get wrong silently. **Check 1 already delivers the useful half mechanically** — it reports that
the FB exposed 9 status members and the binding used 1 of 12 candidates. That is the fact; the
comparison is the engineer's. Building a lattice on top would be inventing a verdict, which is the one
thing all of this tooling refuses to do.

---

## 8. The fixture — concretely, what each check should output

`gen/PlantAutoControl-bench-rerun2/` (the A–D artifacts), `gen/PlantAutoControl-bench/` (the graded generation),
`ir/PlantAutoControl-bench/` (the corpus), and `docs/evidence/PlantAutoControl-bench-grading.md` (the sealed-key
verdicts) together make a regression fixture. All facts below were grep-verified this session.

| Check | Command against the fixture | Expected output | Grade |
|---|---|---|---|
| **FI-36-min** | `trace` with `coil: FilterUnitInst2.IO.Shutdown`, `must_contain: [PlantControl.FansShutdownReady, AirStarInst1.Outputs.ShutdownComplete]` | present / **MISSING** — the REQ-003 REGRESSION, at `PlantAutoControl` N4 (L49); repeats at N3 (L48), N19 (L249) | **Strong** — deterministic catch of the worst failure |
| **1 candidate-scan** (REQ-017) | `--project ir/PlantAutoControl-bench --fb TomraControlSystem --scope DiscreteInputs.Tomra --type Bool --direction status` | 3 IO (`TomraReady`, `TomraRunning`, `TomraComFlt`) + 9 FB status members incl. `Outputs.FaultActive` → **SIZE 12, exit 1**. The generated block bound 1 of 12 (`TomraComFlt`, L143) | **Strong** — makes C §2's blocking Q non-discretionary |
| **1 candidate-scan** (REQ-019) | `--project ir/PlantAutoControl-bench --fb FilterUnitSystem --scope DiscreteInputs.FilterUnit1 --type Bool` | `FilterUnit1Flt`, `FilterUnit1Op`, `FilterUnit1Ready` (Input.ir L78-80) → **SIZE 3, exit 1**, plus `family: 3 same-typed IO : N FB command members` — the transposition signature | **Strong** — the swap had zero mechanical detector before |
| **4 undriven-scan** | `--project ir/PlantAutoControl-bench --fb MotorVSDSystem --caller gen/PlantAutoControl-bench/PlantAutoControl.ir` | `MotorVSDInst1.IO.RotationSensor` (`MotorVSDSystem.ir:56`) **UNDRIVEN** — zero references in the caller (grep-confirmed: no hit for `RotationSensor` in `PlantAutoControl.ir`); name-join hint surfaces `DiscreteInputs.AirStarDCRotSen` and `HMIControlSignals.BypassAirStarDCRotSen`, both unreferenced by the caller. **exit 1** = REQ-012 | **Strong** |
| **3 probative citation** | `relation-reconcile --ledger gen/PlantAutoControl-bench-rerun2/code-structure.md --project ir/PlantAutoControl-bench` | **`0 verified-cross-block rows to check`** — the fixture has none. Positive case must be the synthetic RW-1 row (`Control.ir:14`), which resolves to a declared member with **no writer among the 34 blocks in ir/PlantAutoControl-bench/** | **Weak** — synthetic only |
| **2 relation-reconcile** | `--specs …/equipment-specs --ledger …/code-structure.md --register …/requirements.md` | specs **174**, ledger **174**, register **174**, D3 **absent (STOPPED)** → 0 differences, exit 0. Turns three hand-asserted `**174**`s into computed ones | **Weak** — regression guard, no catch |
| **5 signal-sweep** | `--project ir/PlantAutoControl-bench --specs …/equipment-specs --unclaimed …/unclaimed-signals.md` | An **exact** swept denominator replacing the artifact's `~190`, and an exact residue replacing `~80`/`~40`. No known-correct answer to assert — the value is the exactness, not a catch | **Medium** — no oracle |

The three **Strong** rows are what justify the build. They are also, not coincidentally, the three
checks that read IR rather than markdown.

---

## 9. Recommended build order

1. **FI-36-min — guard-containment hop in `trace`.** Smallest item, existing subcommand, no new
   parser, no new format dependency, and it deterministically catches the REGRESSION — the autopsy's
   own ranked-#1. If only one thing gets built, build this.
2. **Check 1 — `candidate-scan` + `SignalInventory`.** The item FI-39 is named after. Produces the
   shared `SignalInventory` primitive that checks 4 and 5 both need, and delivers computed candidate
   sets for both round-1 SURVIVES. Reads IR only — no markdown-format exposure.
3. **Check 4 — `undriven-scan`.** Small once (2) exists; strong fixture case; closes the per-instance
   gap `cross-check` deliberately leaves open. Still IR-only.
4. **Checks 2 + 3 — `relation-reconcile` (one subcommand, two finding classes).** First item to depend
   on markdown formats, so it is the first that can rot. Cheap, and mechanizing hand-asserted counts is
   worth doing — just do not expect it to find the next defect.
5. **Check 5 — `signal-sweep`.** Largest parser, softest oracle, highest false-positive surface. Real
   value (exact counts replacing tildes) but it should follow, not lead.

**Rationale for the ordering.** Items 1-3 read `.ir` and depend on nothing that a skill edit can
break; items 4-5 read markdown and inherit the drift risk documented in §3. Value density falls
monotonically down the list, effort rises. Items 1-3 alone address three of the four graded failures
(REQ-003, REQ-012, REQ-017/019) — which is the whole point of the exercise.

**Total rough effort:** item 1 small; item 2 medium; item 3 small-to-medium; item 4 small; item 5
medium. Roughly two medium builds and three small ones, ~1800-2200 LOC across runners, models,
formatters and CLI handlers, plus 30-40 unit tests. Items 1-3 are about 45% of that and carry about
85% of the demonstrated value.

---

## 10. Not worth building

| Thing | Why not |
|---|---|
| **`--phrase` as a *filter* on check 1** | Keyword-matching tag names is the exact reasoning that produced the REQ-019 swap. Keep it advisory; never let it change the candidate-set size or the exit code. A tool that narrows by name resemblance launders bias into a computed-looking number. |
| **A markdown parser for the D3 render** (check 4's rung-D form) | D3 is a boolean expression language rendered as markdown. Parsing it means writing a second expression parser that will disagree with the IR one. Read the generated `.ir`. Accepted cost: the check moves from D1 to the check stage. |
| **Check 3 as its own subcommand** | Same file, same reader, same pass as check 2. Two subcommands parsing `code-structure.md` will drift apart. |
| **A parser for `unclaimed-signals.md`'s `### Q-Cnn` finding narratives** | They are argued prose with per-finding structure that varies (one has its own 3-column inner table). Only the per-DB disposition tables are machine-readable. Parse those; leave the findings to humans. |
| **FI-36's "strongest-available-guard" lattice** | Requires a hand-maintained semantic ontology of which signal is "broader". Check 1 already reports the mechanical half (N candidates, 1 chosen). Ranking them is a verdict. |
| **Breaking `trace`'s exit-0 contract** | `trace` and `cross-check` are declared facts providers that always exit 0. FI-36-min lands inside `trace`; leave the contract alone and let callers gate on the JSON. |
| **Extending `ProjectUsageGraph` with types/start values** | `TraceRunner` reads its shape as-is and its own comment declares the verbatim keying deliberate. Add a sibling reader (`SignalInventory`); do not perturb a load-bearing type for two extra fields. |
| **A `presumed-block:` parser** | It does not exist. The string appears nowhere except as unapplied recommendation R15 in `round2.md:598`. The artifacts carry `FB type: <FBName>` on the class line instead — parse that, or take the FB as a CLI argument (preferred: fewer format dependencies). |

---

## 11. Cross-cutting design rules for all six

1. **Facts, never verdicts.** "3 candidates for this binding", never "wrong signal chosen". No
   severity, no ranking, no recommendation. `CrossCheckRunner`'s doc comment is the model: *"Pure
   derivation over graph facts — no judgment, no severity, no verdict."*
2. **Every absence states its denominator.** "No writer among the 34 blocks in `ir/PlantAutoControl-bench/`",
   never "no writer". Partial exports are normal in this project and a bare absence claim manufactures
   false findings at scale. This is the single most important output rule in the study.
3. **A leg that parses zero rows is a hard error, not a clean pass.** The failure mode that would make
   the markdown checks worse than nothing is a format change producing a silent all-green.
4. **Every scan echoes its own narrowing.** Scope prefixes, type filters and phrase terms print on the
   first output line, so an artifact quoting the tool also quotes the narrowing that produced the
   answer.
5. **House shape.** `XxxModel.cs` (records) + `XxxRunner.cs` (pure derivation, `static Run(...)`) +
   `XxxOutputFormatter.cs` (`FormatText` / `FormatJson`) + a `RunXxx` handler in `Program.cs` + a
   usage line in the `Main` help block + `Converter.Tests/XxxTests.cs` + a `src/converter/README.md`
   section + a `CLAUDE.md` command-table line.
6. **`ignoreErrors` tolerance.** One unparseable corpus file surfaces as a warning and never sinks a
   scan — the contract `DigestBuilder`, `ReuseScanRunner`, `TargetScanRunner` and `ProjectIndex` all
   already honour.
7. **Never hand-split a dotted path.** `AccessNode.FromDottedPath(0, "GlobalVariable", path)` — a
   literal-dot tag (`Clock_0.5Hz`) exists in this corpus and a naive `IndexOf('.')` gets it wrong.
8. **Purity.** All six are file-in / stdout-out. No process launch, no git, no Portal. The owner-held
   invariant (`project_converter_no_external_process`) holds without effort — nothing here wants an
   external process.
