# converter — the mechanical floor

Whole-project facts and spec-coverage checks: the commands that survive an agent choosing not to look. Every one states the denominator it examined.

> Split out of `src/converter/README.md` on 2026-09-17. That file remains the index.

## `reuse-scan` — reuse-first duplicate-logic finder (2026-07-20, FI-29)

### The denominator, and when the question landed on nothing (migrated from CLAUDE.md 2026-08-21)

**Exit 0 here licenses "nothing to reuse, write a new block"** — so an exit 0 that examined nothing
is a licence issued in error. Until 2026-08-14 a `--tag` naming nothing in the corpus produced
`SUMMARY: 0 block(s) matched`, exit 0, **and no denominator at all**, so a typo'd tag or a wrong
`--project` read exactly like a thorough scan of 43 files. Note the asymmetry it already had:
`--kind` IS validated against a known set and refuses an unknown value by name; `--tag` was
validated against nothing.

Now: **`SUMMARY: … of <n> file(s) scanned` on every run**, an **`ABSENT:`** line naming roots that
appear in no block, and **exit 2 when EVERY queried root is absent** (the question landed on
nothing).

Deliberately keyed on **ALL** roots, not any — a Design-stage query legitimately mixes existing tags
with proposed ones (`gen-architecture` designs AGAINST gaps), and refusing that would be a gate
firing outside its scope.

`--tag` matching is **ROOT-level by design** (digest aggregates tag roots per block), so
`--tag DB_X.Member` matches every block touching `DB_X`; the MATCH line shows the root it actually
matched.

`converter reuse-scan --project <ir-dir> [--tag <tag> ...] [--kind <kind> ...] [--json]`

A digest-backed corpus query for the reuse-first carving pass (`gen-architecture` /
`gen-spec-analysis`): "which blocks reference tag T / implement a statement kind (a timeout, a coil,
a call) — anything I might be about to re-build?" Surfaces the **candidate** blocks a human/AI then
checks for semantic duplication; it never rules "already exists" (orientation only, matching
`digest`'s policy — `docs/15` isolation model). Composition on top of `DigestBuilder` — no new IR
traversal; tag roots are extracted with the *same* `AccessNode.FromDottedPath` primitive, so a
literal-dot tag (`Clock_0.5Hz`) is never mis-split.

`--tag` matches at **block level** (the block's tag roots — that's digest's granularity); `--kind`
matches at **network level** (a network whose statement summary contains that kind). Each flag may
repeat; within a group the match is "any", and the two groups are ANDed — so `--tag T --kind timer`
finds blocks that reference `T` *and* have a timer network. `--kind` must be one of the digest
statement labels (`coil timer move wand call arith convert swap abs limit tsub tconv calc moveblk
wait fillblk mb-master mb-commload`); an unknown kind is a hard error listing the valid set. At
least one `--tag` or `--kind` is required. **Exit non-zero if any candidate is found** (the
reuse-first alarm — `reuse-scan … && <build>` stops you to look before building new logic).

```
$ converter reuse-scan --project ir/test-project001/ --kind timer
QUERY: tags=[(none)] kinds=[timer]
MATCH: FB_ShredderSequencer (FB)  ir/test-project001/FB_ShredderSequencer.ir
  network 8 "Step 20 (DischargeStart) - Timer, Timeout Fault, And Transitions" — timer
  ...
SUMMARY: 4 block(s) matched          # exit 1
```

## `target-scan` — S6 new-block target gap-hunter (2026-07-20, FI-30)

`converter target-scan --requirements <register.md> --project <ir-dir> [--json]`

Cross-joins a `requirements.md` register against a **fresh** tag-status classification
(anti-laundering — never trusts the register's own `exists`/`proposed` marks; re-derived via the
same `ProjectIndex` primitive `tagstatus`/`preflight` use) and the as-built corpus, pre-computing
each REQ's *mechanical* disqualifiers so a human confirms a short filtered list instead of surveying
every REQ by hand. Each REQ lands in one bucket:

- **DISQUALIFIED** (mechanical, precise): class `HMI` (`hmi-only`) or `out-of-scope`; any named tag
  classifies `PROPOSED` (`proposed-tag-blocked`, with the specific names); or a linked `Q-nn` is
  `Still open` / `Partially resolved` (`q-open`, with the question + status).
- **LIKELY-IMPLEMENTED** (heuristic — kept deliberately *separate* from the mechanical layer):
  mechanically clean, but the REQ's `exists`-tags already appear in an as-built block's tag roots.
  Shown with the block(s) and the overlapping roots so the human judges the hint's strength. This is
  a soft, semantic signal — never a mechanical verdict; confirm with `reuse-scan` or a read.
- **CANDIDATE**: mechanically clean *and* no as-built block references its tags — a genuine
  new-block-only target the human then confirms.
- **WITHDRAWN**: the register marks the REQ withdrawn (carried for completeness).

The register parser is a focused line/regex reader of the register's own strict `## Format` contract
(`### REQ-nnn — <title>` headers, `- **Class:**` / `- **Notes:**` bullets, `## Open questions` with
`- **Q-nn — … <STATUS>`), not a general markdown parser. Named tags are the back-ticked identifiers
in each REQ's text/notes; classification is **root-level** (like `preflight`) — a bare interface
member may over-report as proposed, which the shown name lets the human sanity-check. **Exit
non-zero if there are zero candidates** — the fast "no clean new-block target here" signal the
manual survey used to reach by hand.

```
$ converter target-scan --requirements gen/test-project001/requirements.md --project ir/test-project001/
REQUIREMENTS: 69 parsed | CANDIDATES: 19
...
DISQUALIFIED (mechanical):
  REQ-020  [control]  Overcurrent on either motor
    reasons: q-open [Q-05 (Still open)]
  REQ-061  [out-of-scope]  E-stop stops everything (hardwired)
    reasons: out-of-scope
...
SUMMARY: 19 candidate(s), 35 likely-implemented, 15 disqualified, 0 withdrawn          # exit 0
```

## `cross-check` — whole-project cross-block facts (2026-07-20, FI-22)

### Multi-writer lines count writes from blocks that may never execute (migrated from CLAUDE.md 2026-08-21)

A path written by two blocks, one of which no OB can reach, was reported as a C-308 multi-writer
with **exactly one runtime writer** — and the call graph that settles it was already in the same
report, under SIBLING REFERENCES, unused. Lines now carry
`[NOT REACHABLE from any OB: <blocks> — <n> writing block(s) actually execute]`.

**REPORTED, NEVER SUBTRACTED:** an unreachable block is usually one somebody means to call, and
dropping its write would hide the conflict that appears the moment it is wired up.

Reachability is derived from each block's **KIND** — an OB is called by the operating system and
nothing else is. Never from a name prefix (anything can be renamed into one), and never from
"nothing calls it", which would make every uncalled block its own root, i.e. exactly the state being
detected. **A corpus with NO OB reports `unreachableKnown: false` — that is UNKNOWN, not "all
reachable".**

`converter cross-check --project <ir-dir> [--json]`

`converter review` is per-file; the review skills build cross-block tables by hand. `cross-check`
walks every block's networks across the whole export and emits the reference-graph **facts** those
tables need — as **verbatim facts the reviewer reasons over, never adjudicated verdicts** (same
discipline as the `review` dump). Substrate: `TagReferences.AllDirectedUsages` (a direction-tagged
reader/writer extractor, sibling of `AllTagPaths`) + `CrossCheck/ProjectUsageGraph` (the whole-export
reader/writer index, reused by FI-25). Four fact tables:

- **Multi-writer paths** (C-308): every full path written by >1 site, each writer with its block +
  network + Set/Reset kind — the AI distinguishes a legit S/R pair from conflicting writes.
- **Dead members** (review-functional Pass-2 dead-wiring): each member with no writer
  (consumed-but-never-written / fully unused) or no reader (written-but-never-consumed) — the
  in-cycle-lamp / dead-selector bug classes as a mechanical set-difference. Covers **global-DB
  members** (`[global-db]`, unambiguous full-path) **and FB interface-UDT members** (`[interface]`,
  FI-22 aliasing follow-up): an interface member's writers/readers are pooled across the FB-internal
  bare form and every `iDB.<suffix>` alias (via `DbSource.InstanceOfName`) before the deadness test,
  so a member written inside the FB and read via an iDB is correctly *not* dead. Real corpus surfaces
  e.g. `FB_ShredderSequencer.IO.InCycle` ("superseded, no longer wired") with no false positives.
- **Physical-IO references** (C-304): each `DI/DQ/AI/AQ…`-rooted or raw `%I`/`%Q` reference with its
  block + direction — the AI excludes the Map FCs and flags any other block touching raw IO.
- **Sibling references** (C-127): per block, the blocks it CALLs and any `iDB_*` root it references.

**Exit 0 always** — a facts provider, not a gate. On `ir/test-project001` it surfaces real signals
(e.g. `DB_Input.Pusher_Local_Remote` written-but-never-consumed; the unused overcurrent setpoints).

## `signal-sweep` — project-level residual signal coverage (2026-08-05, FI-39 check 5)

```
converter signal-sweep --project <ir-dir> --specs <equipment-specs-dir>
                       [--register <requirements.md>] [--unclaimed <unclaimed-signals.md>] [--json]
```

Computes the **denominator** (every global-DB leaf and tag-table tag in the corpus) and classifies each
signal `claimed-by-spec` / `disposed` (listed in the residual artifact's per-DB disposition tables) /
`unaccounted`.

**Its value is exactness, not a catch** — it has no oracle, and the design study grades it Medium. What
it converts is an artifact's own self-reported approximations into computed integers: on the fixture,
**201 swept** against the artifact's `~190`, with an exact per-DB breakdown and an exact residue. An
approximate denominator cannot support a completeness claim; an exact one can.

- **Qualification matters:** the disposition tables list **bare leaf names** under a `### \`Db\``
  heading, so each is qualified by its enclosing heading before comparison — without that, every leaf
  silently fails to match and the whole sweep reads as unaccounted.
- **"Mentioned in a spec" is the claimed test, deliberately coarse.** A spec that names a tag without
  binding it is a different problem, and this check must not pretend to detect it.
- **A signal dispositioned only in PROSE reads as unaccounted** (a row like `E-stop members | safety`).
  That is not a false positive so much as a fact about the artifact: prose is not machine-checkable, and
  backticking the member names is the fix. The output says so explicitly.
- **Hard errors, never silent coverage:** an empty corpus, or an `--unclaimed` artifact whose tables
  cannot be found, both fail loudly rather than reporting full coverage or blanket-unaccounted.

**Exit 1** if any swept signal is in neither a spec nor a disposition table. Whether an unaccounted
signal *implies control* is the engineer's call — the tool reports coverage, never a verdict.

## `relation-reconcile` — relation-set reconciliation + probative citations (2026-08-05, FI-39 checks 2+3)

```
converter relation-reconcile --specs <equipment-specs-dir> --ledger <code-structure.md>
                             --register <requirements.md> [--project <ir-dir>] [--json]
```

Reconciles the `(instance, relation-id)` sets across the four relation-bearing artifacts — the specs'
`- **C1**` bullets, the D2 ledger rows, the derived register's `Rel` column, and the D3 render's `[C1]`
term tags — and reports every pairwise difference.

**The key is `(instance, relation-id)`, never the bare id:** two instances' `C1` are different
relations, so a bare union across specs is vacuous and would let one instance's relation satisfy
another's.

What it buys is honest: on a clean artifact set it is a **regression guard, not a catch** — but it
converts three **hand-asserted counts inside the artifact** (`| Rows in this ledger | 174 |`) into
computed ones, which is the "computed rather than asserted" principle this tooling exists for.

- **An `ABSENT` leg is not a reconciling leg.** A stopped D3 reports `ABSENT`; reporting "0 differences"
  for an artifact that does not exist is the silent-green failure the check exists to avoid. An absent
  leg alone does **not** gate.
- **A leg that parses zero rows is a HARD ERROR**, never a clean pass — format drift is the whole risk
  here (five documented divergences between a SKILL written this month and an artifact produced this
  week), so the parsers are strict and say so loudly when the shape is missing.
- **A leg that MATCHED NOTHING exits 2** (FI-44, 2026-08-05). A leg that parsed relations but shares
  **not one key** with any other present leg took part in comparisons that examined nothing. The
  demonstrated cause was the SKILL's own D3 example: it wrote `instance: iDB_MotorDOL_Conv07` while
  every other leg is keyed on the **spec instance**, so following the documentation produced a render
  that could not intersect anything. *Absent* and *matched nothing* are different situations and report
  differently — absence is a parse fact about an artifact that is not there and still never gates;
  matched-nothing is a comparison fact about an artifact that is. Computed on the raw keys, before the
  disposition partition below narrows anything.
- **The ledger is partitioned by disposition** (FI-45, 2026-08-05). Only `rendered` and `rebind` are
  **render-bound**; `in-FB`, `discharged`, `render-BLOCKED`, `render-stopped` and
  `out-of-scope-obligation` all mean *"this relation does not become a D3 term"*. Key identity is
  required only **into** the render, against that subset; the other five report in a `ledger
  dispositions` block as *accounted for*. Before this, one such row exited 1 and `render-BLOCKED` was
  undeclarable in an artifact that had to pass its own self-check. Out of the render nothing is
  filtered: a term tagged with a relation no artifact declares is still a difference. Cells are matched
  as they really occur — `render`, `**rebind**`, `discharged (S5)`, ``render-BLOCKED `[contested]` ``,
  `in-FB + render(driver)` — and an **unrecognized** disposition is treated as render-bound (fail
  closed) and warned about.
- **Citations (check 3):** for each `verified-cross-block` row, every backticked identifier-shaped token
  in the evidence cell is classified — *resolves with N writers* / *writers all disarmed* / *declared
  with no writer in this export* / *does not resolve*. A row where **no** token resolves to a written
  member is the finding. The cell is free prose (file names, network labels, expression fragments), so
  the tool never guesses which token is "the tag" — it reports per token. It also never judges whether
  the guard *entails* the claim; that stays a human duty. Needs `--project`.
- **Denominator, always:** "no writer" means *no writer among the N blocks in this export* — partial
  exports are normal here, so that is a scope fact, not a defect.

**Exit 1** on any non-empty set-difference or citation finding; **exit 2** when a leg was compared
against nothing (that question is unanswerable, not clean). On the fixture: 174/174/174 with
`render ABSENT`, exit 0; delete one ledger row and it names `FilterUnitInst2.C5`, exit 1.

## `undriven-scan` — per-instance interface drive states (2026-08-05, FI-39 check 4)

### What counts as an instance, and exit 2 (migrated from CLAUDE.md 2026-08-21)

Per-instance is the point — `cross-check` pools across instances, so one driven instance masks an
undriven sibling. Exit 1 on undriven/disarmed; dead-interface reports, never gates.

**EXIT 2 = nothing was examined** — `--fb` names no block in the corpus, or names one with no
instances. Under FI-44 both used to exit 0, so a block that had never been written passed the check.

**An INSTANCE means an instance DB *or* a MULTI-INSTANCE** (an FB placed as a STATIC of another FB,
`ValveA : "FB_Valve"`) — FI-50. It used to mean instance DBs only, so on a corpus following
C-132's single-STATIC-UDT house style *every* block reported "no instances" and the check examined
nothing. IEC timer/counter statics are excluded (a TON's `Q` is written by the instruction, not a
caller); **an owner with no instance DB yet reports its placements under a declaration-site path
`FB_X/Member`.**

```
converter undriven-scan --project <ir-dir> --fb <FBName>
                        [--instance <iDB> ...] [--caller <file.ir> ...] [--hints] [--json]
```

For each **instance** of an FB, which of its interface members actually receive a value. The new value
over `cross-check` is **per-instance resolution**: that command canonicalizes to `(FB, member)` and pools
across every instance — correct for its own question, wrong for this one, because if one instance drives
a member and another does not the pooled view shows the member alive and the gap disappears. A dropped
bypass on one instance of a shared block is exactly that shape.

States, all computed: **`driven`** (an armed writer) · **`disarmed`** (writers exist, all
provably-false-guarded) · **`undriven (default X)`** (no writer, but the instance DB's start value is
then the effective constant) · **`undriven`** · **`dead-interface`** (no writer **and** the FB never
reads it either — inert on both sides).

`--caller <file.ir>` merges a block outside the export into the graph (the `ProjectIndex` batch idiom),
so a freshly generated block can be analysed before import. Consequence: this runs at the check stage,
after IR exists — it reads IR, never a markdown render.

**Exit 1 on `undriven` or `disarmed` only.** `dead-interface` is reported but does **not** gate: a
reusable library block legitimately exposes optional inputs a given instance doesn't use, so gating on it
would emit findings by the hundred and train readers to ignore the output. It is a fact to cross against
a spec that required the capability — which is what makes it useful for the dropped-bypass case, where
`IO.RotationSensor` is declared, unwired, and unread on every instance.

`--hints` opts into a name-token match between an unreferenced project signal and an undriven member
(`RotationSensor` ↔ `…RotSen`). **Off by default and never part of the exit condition** — it is a
labelled heuristic, and on a real corpus it fires often enough to bury the findings it sits beside.

### Multi-instances count as instances (2026-08-07, FI-50)

An **instance** here is an instance DB *or* a **multi-instance** — an FB placed as a `STATIC` member of
another FB (`ValveA : "FB_Valve"`) rather than given a DB of its own. Both carry per-instance state
and both can be left undriven.

Originally only instance DBs were counted, because instances were read off DB sources carrying an
`InstanceOf`. That is not a corner case under **C-132**, which makes the single-`STATIC`-UDT interface
the house style: a whole corpus can consist of nothing but multi-instances, and this scan would examine
**zero** members of **zero** instances while exiting cleanly. Found on a live job where every FB in the
project reported `no instances` — FI-44's "empty is not clean" failure re-entering through a shape
FI-44 did not consider.

Two details the implementation has to get right, both of which are the reason this is not a one-liner:

- **A multi-instance is addressed by its bare static name from inside its owner** (`ValveA.IO.Cmd`),
  with no instance root in the text at all — unlike an iDB, which callers address by name. So the usage
  lookup is made on the local form and then **restricted to the owning block**, or two FBs that happen
  to share a static name pool each other's writers and each masks the other's gap.
- **Resolution iterates to a fixpoint**, because multi-instances nest: if `FB_CellSequence` owns an
  `FB_CellCycle` and is itself reached through an instance DB, the cycle's real path is
  `iDB_SeqW.Cycle`. A single pass would root it at the declaration site and lose exactly the
  per-instance resolution this command exists for.

Where the owning FB has no instance DB **yet**, the placement is reported under a **declaration-site**
path — `FB_Cell/ValveA`, with a `/` that can never be mistaken for a member path. That keeps a
block written before its caller judgeable instead of unexaminable; the alternative is reporting nothing,
which is the failure this fix exists to remove.

**IEC timer and counter statics are excluded.** A `TON_TIME`'s `Q` and `ET` are written by the timer
instruction, not by any caller, so "who drives this" is not a meaningful question for them. Left in,
they were the loudest false positive in the output — every dwell in a sequencer carries one.

### 🔴 Two write mechanisms it could not see — 136 of 228 rows were false (2026-08-18)

Found by running this across a **101-file live corpus** and hand-verifying every line. `cross-check`,
reading the **same graph**, got both joins right — two tools contradicting each other over one corpus
is what made it findable, and is the strongest available evidence that this was the scan's defect and
not a corpus quirk. **A tool wrong 60% of the time in one direction cannot be trusted in the other**,
so its output was unusable without hand-verifying every line.

Both repairs went into **`ProjectUsageGraph.UsagesReaching`** — shared, on the graph — rather than
into a second resolver here, because the join is a property of how storage nests and not of any one
check's question.

1. **An ABSOLUTE instance-path write to a MULTI-INSTANCE member was not joined.** The bullet above
   explains that a multi-instance is addressed *bare and local* from inside its owner. It is also
   addressed **absolutely, rooted on the owner's instance DB, from everywhere else** —
   `iDB_Cell_North.ValveB.IO.InHand`. Only the local form was ever looked up, so every
   write from an orchestrator, a command decoder or a startup block was invisible. Both forms are now
   resolved and unioned; the **owner restriction stays on the local form only**, and must — a bare
   `ValveB.IO.InHand` could belong to any FB declaring a `ValveB`, while the absolute form
   names one placement, which is what makes it absolute.

2. **A WHOLE-STRUCT write was not attributed to the struct's members.** `MOVE(…) => Selected` drives
   `Selected.SetId`, `Selected.TargetGrade` and every other member under a key that mentions none of
   them, so a verbatim lookup found nothing. Worse than noise on the measured case: those members are
   ones the **FB itself** writes, so they should never have been in the caller-driven scope at all —
   a reader was being told a caller had failed to wire an FB's own outputs.

Measured across the corpus, old binary → new: `FB_ProfileSelect` **40 undriven → 0** (48 rows leave
the scope entirely), `FB_MotorDOL` **58 → 10**, `FB_Valve` **100 → 68**. The remaining findings are
genuine — a per-valve member written for one placement and not its siblings.

#### The floor, and why an ancestor rule without one is worse than the bug

`CALL FB_Rack(iDB_Rack_RackA, EN := TRUE)` records a **write at the bare instance path** — the CALL
naming its own state store, not a data write of the interface. A naive ancestor rule admits it, and
then every member of every instance reads as `driven`. **Measured live while building this fix:** a
block with 20 genuine undriven members reported **168 driven and exit 0**. So `UsagesReaching` takes
a `notAbove` floor and admits an ancestor only strictly below it; the caller passes the instance
root. A test pins this, and it is the test that matters most here — a fix that makes everything look
driven passes every test that only checks the two joins.

### 🔴 `NOTHING EXAMINED` — the third shape of FI-44 (2026-08-18)

The first two ways this scan could examine nothing and exit 0 were closed by name: an unknown
`--fb`, and an FB nothing instantiates. **This is the way that was not thought of.** The block
EXISTS, it HAS an instance DB, and every one of its interface members is one the FB itself writes —
so the caller-driven scope is empty and nothing is resolved. On the live corpus **two of the three
largest blocks** reported `0 member/instance pair(s), 0 undriven` and **exit 0**, and both were read
as passes.

That is an ordinary state for a block that only publishes. It is not a pass and it is not a finding:
it is a statement that the question does not apply to this block. It now exits **2**, prints
`NOTHING EXAMINED - this is not a pass:` with the reason, and carries `scope` +
`examinedNothing` in `--json` (which previously carried neither, so a consumer reading
`members: []` + `hasFindings: false` could not tell the two apart either).

A fourth shape — `--instance` matching none of the block's instances — is closed with it.

**And the gate now keys on the ROW COUNT as well as on the scope enum.** The enum enumerates the
ways of examining nothing that somebody has already thought of, and the third one arrived three
weeks after the first two were called complete. `Members.Count == 0` is the fact rather than a
catalogue of its causes, so a fifth shape gates on arrival instead of on being noticed.

## `candidate-scan` — compute the candidate set for a requirement (2026-08-05, FI-39 check 1)

### What `--scope` matches, and exit 2 (migrated from CLAUDE.md 2026-08-21)

**`--scope` matches a path prefix OR a C-001 physical-IO equipment token.** `--scope UnitA` reaches
`DQ3_UnitA_...`, whose equipment field is in the MIDDLE and which no prefix could address.

**EXIT 2 = a scope was given and matched NOTHING** — unjudgeable, not clean. Under FI-44 it used to
exit 0, so scoping to a piece of equipment silently cleared a genuinely contested binding.

`--instance` is **PROVENANCE ONLY**: it labels the report and never narrows the set, because a
candidate set is a property of the FB class, not an instance. (Unlike `undriven-scan`, where
per-instance is the whole point.) `--phrase` is advisory only and never narrows.

```
converter candidate-scan --project <ir-dir> --fb <FBName> [--instance <name>]
                         [--scope <path-prefix> ...] [--type <TypeName>]
                         [--direction status|command|any] [--phrase <word> ...] [--json]
```

Given a requirement's target scope and the FB an instance uses, **computes every signal that could
satisfy it** — the IO half from the project's signal inventory, the FB half from that block's own
interface. Makes *"more than one candidate"* a computed fact instead of a judgement call.

Why: two defects shipped because a requirement phrase ("not faulted", "running feedback") admitted more
than one signal and the single reader who resolved it never noticed there was a choice
(`docs/evidence/PlantAutoControl-bench-autopsy.md` §2-C). Nothing computed a candidate set, so nothing could
flag the ambiguity. **This tool reports what is in scope; it never says which one the requirement
means** — that is the engineer's call.

- **Direction is COMPUTED** (does the FB write this member, or read it?), never taken from the
  interface section: on the real corpus a block's reportable status members sit under `STATIC` inside
  interface-UDT structs while `INPUT`/`OUTPUT` carry data-link words, so section-filtering gets the
  wrong answer on exactly the block the narrowed-fault-gate defect concerns.
- **`family`** reports N same-typed in-scope IO signals vs N FB members of matching direction — the
  transposition signature a 1:1 by-name-resemblance assignment silently gets wrong.
- **`--phrase` is advisory only.** Filtering by name resemblance is precisely the reasoning that
  produced the swapped-pairing defect, so the phrase subset is reported but the exit code keys off the
  **unfiltered** size.
- **`--instance` is provenance only** — it labels the report header and the JSON, and never narrows
  the candidate set (which is a property of the FB *class*, not of any one instance). Verified
  against `CandidateScanRunner.Run` 2026-08-05: the value reaches the report record and nothing else;
  neither `CollectIoCandidates` nor `CollectFbCandidates` receives it. Called out because the sibling
  `undriven-scan` is emphatically per-instance, so the same flag name reads as if it filtered here
  too.
- The header states the **denominator** (files scanned) — in a partial export an empty set is a scope
  fact, not a finding.

**Exit 1 when the candidate set size > 1** (the `tagstatus` convention) — the mechanical trigger that
makes an ambiguous binding non-discretionary. On the fixture: `--fb TomraControlSystem --scope
DiscreteInputs.Tomra --type Bool --direction status` surfaces `DiscreteInputs.TomraComFlt` **and**
`Outputs.FaultActive` (the two candidates the defect was about); `--fb FilterUnitSystem --scope
DiscreteInputs.FilterUnit1` surfaces the `Flt`/`Op`/`Ready` family.

Shared primitive: `Converter/SignalInventory/SignalInventory.cs` — a typed signal walk
(`Path, Root, Leaf, Type, IsRetain, Origin`) that keeps what `ProjectUsageGraph.CollectLeafPaths`
discards. Deliberately a **sibling** of that graph, not an extension: `TraceRunner` reads its shape
as-is and its comment declares the verbatim keying deliberate.

## `trace` — forward-pass REQ verdict tracer (2026-07-20, FI-25)

`converter trace --binding <bindings.json> --project <ir-dir> [--json]`

Layer B of the functional review's forward pass. The `review-functional` reviewer emits a per-REQ
**binding** (Layer A — the semantic anchor: REQ → concrete IR anchors); `trace` walks it over FI-22's
`ProjectUsageGraph` (read-only) + DB start values and emits **facts + candidate verdicts per hop —
never an adjudicated pass** (the reviewer confirms each candidate semantically). Binding is a
snake_case JSON list of `{ req, out_tag?, iface_member?, number?: { member, expected } }`.

Hops:
- **output-path**: `out_tag` written anywhere? no → `unimplemented (no output path)`.
- **interface-chain**: `iface_member` written anywhere? no → `broken-chain` (the in-cycle-lamp class).
- **disarmed** (v2, on output-path/interface-chain): every writer gated `NOT AlwaysTrue` (the S7 always-on
  bit) → `disarmed` (built but switched off — NOT implemented). A pure three-valued constant-fold
  (`Trace/DisarmAnalysis.cs`, `AlwaysTrue⇒true`); catches `NOT AlwaysTrue` standalone or ANDed; a bare
  `AlwaysTrue` stays armed. A mixed path (some armed) stays `ok` with the disarmed count noted.
- **number-constraint**: DB member start value vs spec → `ok` / `contradicted` / `partial` (no start value).
- **timing** (v2, `timing: { timer, seconds_member }`): the timer's PT must be the ×1000 ms form of the
  bound seconds member — keyed on the timer's *unique* PT ms-member name corresponding to the seconds
  member (`OvercurrentMediumDelayMS` ↔ `OvercurrentMediumDelay`), since the shared `Time` scratch tag can't
  distinguish which member feeds which timer. Name mismatch → `contradicted`; corresponding + the
  MUL/CONVERT idiom present → `ok`; corresponding but chain absent → `partial`; no such timer →
  `unimplemented`. **Documented limitation:** the specific MUL↔CONVERT `EN:=ENO` wire (sidecar-only) is not
  verified — a sidecar-level refinement.
- **guard-containment** (FI-36-min, 2026-08-05, `guard: { coil, must_contain: [...] }`): every signal the
  spec lists as a condition on `coil` must appear in the guard of **each** write to it. Reported **per
  writing site**, never unioned — a term present in one network and absent in another is exactly the
  multi-instance shape a union would hide. Missing → `missingterm`, naming the site; a coil with no writer
  → `unimplemented` (the output-path fact, *not* "every term missing"); a present term whose writer is
  disarmed is reported present and marked `[disarmed]`. Terms nested in a comparison count.

  This hop is a **set-difference over signal identity, not a re-interpretation** — which is the point:
  a dropped cascade-hold term shipped as a REGRESSION because the coder and the functional reviewer
  resolved the same ambiguous source the same way, so the review confirmed the error instead of catching
  it (`docs/evidence/PlantAutoControl-bench-autopsy.md`). This check cannot be defeated by how anyone reads the
  requirement. Verified on the real graded pair: against the generated block it reports
  `AirStarInst1.Outputs.ShutdownComplete` MISSING at `PlantAutoControl N3` (and the cyclone's at N19);
  against the sealed answer key, the same binding reports `ok`.

**Exit 0 always** — a facts provider. Examples on `ir/test-project001`: a binding for REQ-004 shows
`DQ5_DIS_Run` written by `FC_Outputs` and `DischargeConveyorTimeout`=10.0 matching spec;
`IO.HandReverse` (`:= NOT AlwaysTrue`) → disarmed; `OvercurrentMediumTimer` + `OvercurrentMediumDelay` →
timing ok, but + `OvercurrentHighDelay` (wrong member) → contradicted.

## 🔴 `cross-check` — FB-internal paths were unqualified, so it reported multi-writers that do not exist (2026-08-14)

The usage graph keys every path **verbatim**, and an FB addresses its own interface member with **no
root at all** — `IO.Step`, `Time`. So three FBs, each with its own `IO` static of its own UDT type
and its own `Time : Real` temp, all landed on **one key**, and `cross-check` reported them as
**cross-block multi-writers**. They are different members of different instances that share a leaf
name and nothing else.

**Measured on `ir/test-project001`.** Of the four multi-writer paths spanning more than one block:

| path | writers | verdict |
|---|---|---|
| `IO.Step` | FB_PusherControl + FB_ShredderSequencer | ❌ **fictitious** — members of `UDT_PusherIO` and `UDT_ShredderSequencerIO` |
| `Time` | FB_MotorFwdRevSystem + FB_PusherControl + FB_ShredderSequencer | ❌ **fictitious** — a `Real` temp declared separately in each |
| `iDB_MotorFwdRevSystem_Shredder.IO.FaultFB` | FC_ControlMain + OB100 | ✅ real |
| `iDB_MotorFwdRevSystem_Shredder.IO.RecentStart` | FC_ControlMain + OB100 | ✅ real |

After the fix the cross-block set is **exactly the two real ones**. *A false finding is the equal of
a false green here — the first one is what gets a check switched off* — and this was caught only
because a lane refused to feed the output into a submission gate it did not trust.

**The fix keys on whether the block DECLARES the root**, never on whether the path has a dot. *"Is
this bare name the block's own member or a global PLC tag?"* cannot be answered from the name —
`PressureTripCount` (an FB static) and `Start_PB` (a tag-table tag) are both bare single-component
references — and is answered exactly by the block's own declarations, which the IR states outright.
Temps and constants are included: a temp named `Time` **is** the collision.

`multiWriters` and `soleWriters` now regroup by **storage identity** and carry an `owner` field —
the owning block for a block-local path, `null` for a global one. **A consumer must key on `owner`
rather than parse the path: an emitted string is not a schema.**

⚠️ **What it deliberately does NOT do: it does not pool an FB-internal member with the
`iDB.<suffix>` form.** Those are one storage when the FB has one instance, but an FB with **two** has
an internal write landing in **both**, and pooling with either would invent a conflict exactly as the
bug did. The aliases are **reported** on the fact (`instanceAliases`) so a consumer can join them
knowingly. Every FB in `test-project001` has exactly one instance DB — *which is precisely why
designing only for that would be designing for the case that happens to exist.*

🔴 **2026-08-21 — reporting the alias was not enough, because the line beside it asserted a verdict
that contradicted it.** A block-local row printed `[block-local to FB_X — not a cross-block conflict;
also addressable as iDB_X.member]` **while another block wrote that very alias.** **8 of the 27**
block-local multi-writer rows in `ir/test-project001` read that way, with `OB100` and
`FC_ControlMain` writing the aliases through the instance DBs.

The key spaces are **still not pooled** — the reasoning above is unchanged and still right. What is
new is that each group now carries `aliasWriters`: the writers of its `iDB_…` aliases that live
**outside** the owning block (the owner's own writes are excluded, or every FB would self-conflict).
Consequences:

- The `— not a cross-block conflict` half is asserted **only when `aliasWriters` is empty**. Where it
  is not, the row instead names the outside writers and says plainly that this IS cross-block
  contention. On the reference corpus **19 rows keep the annotation and 8 lose it** — counted off the
  tool's own JSON, after a first pass eyeballed three name patterns and undercounted it as 4.
- **`soleWriters` had the same blindness on EIGHT rows, and that is the more dangerous half.** One
  internal writer plus an outside alias writer gives `Writers.Count == 1`, so such a row appears
  **only** in the sole-writer table and never in the multi-writer one — and that table exists to
  answer *"what loses its only writer if I delete this?"*. `FB_MotorFwdRevSystem.IO.FaultFB` is
  written by `FC_ControlMain` **and** `OB100` through the iDB, and read as sole-written.
- **Reported, never subtracted** — the same discipline `unreachableWriterBlocks` follows. Such rows
  stay in `soleWriters` carrying `aliasWriters`, because dropping them would hide them entirely.
- A consumer that dismissed a row on `owner is not null` **must now also check `aliasWriters`.**

**Sibling analyses checked.** `deadMembers`' interface half already restricted the bare form to the
owning FB; `ioBoundary` and `siblingRefs` carry the block on every row. All three are
**byte-identical across the fix** on the committed corpus, so `multiWriters`/`soleWriters` were the
only two affected.

**`soleWriters` was under-reporting**, which is the more dangerous direction: two FBs each writing
their own member once pooled into a two-writer path, so it read as multi-written — *not vulnerable to
a deletion* — when each was its FB's **sole** writer. That did **not** bite on this corpus (no path
moved between the tables) and is recorded as constructed-not-observed; a test builds the case
deliberately.

11 new tests, **every aliasing assertion paired with a genuine cross-block multi-writer that must
still be found** — a fix that silences the false one by silencing everything is the obvious failure
mode. Mutation-tested three ways: qualification disconnected (6 red), everything qualified (6 red,
including two pre-existing tests), and the plausible **name-shape heuristic** (2 red — exactly the
two tests written for it). *The pre-existing suite stayed green through both the defect and the fix
and could not tell them apart.*

## `reachable-state` — D9's producer: slot disjointness COMPUTED, not declared (2026-08-14)

### Consumers, closure direction, and canonicalisation (migrated from CLAUDE.md 2026-08-21)

`converter reachable-state --project <ir-dir> [--block <name>]... [--json]`

Per block: the transitive closure through its CALL tree of every storage location it touches, keyed
on STORAGE IDENTITY, with a corpus-stamped provenance. Feeds
`wave-cli submit --reachable-state <file> --reachable-block <name>`, after which
`SlotConflictDerivation.OverlappingReachableState` makes the edges by set intersection.
`--reaches`/`--reaches-from` survive as the DECLARED path and are **REFUSED in combination** with
the computed one.

- **READS COUNT AS WELL AS WRITES** — two slots cannot share a signal one drives and the other
  observes.
- **CLOSES DOWNWARD ONLY.** Closing upward through callers reaches OB1 from any leaf and would make
  every pair conflict; coupling that exists only in a common caller is the author's add-only
  blacklist.
- **An `iDB.<suffix>` reference is canonicalised onto `<FB>|<suffix>` before intersecting.** Without
  it, a slot testing an FB and one testing its caller read as DISJOINT while driving one location.
  This deliberately POOLS WHERE `QualifiedPath` DOES NOT, because the error points the other way:
  there, pooling invents a multi-writer (a false accusation); here it can only ADD an overlap, i.e.
  separate two slots that might have run together. Computed disjointness is the FLOOR, so more of it
  is the safe direction — and every rewrite is REPORTED, never silent.
- Array subscripts are stripped (`DB_Input.Test[0]` disconnects every terminal for both tests).
- **ABSENT IS NOT EMPTY, AT BOTH LEVELS:** `reachableState: []` is the positive claim "computed,
  reaches nothing"; a closure nobody could compute OMITS the key AND its provenance, so admission
  raises `ReachableStateNotComputed` and refuses rather than admitting the most independent-looking
  slot in the set.

Exit **0** computed / **2** NOT COMPUTED, key withheld / **3** emitted with at least one block
withheld BY NAME.

```
converter reachable-state --project <ir-dir> [--block <name>]... [--json]
```

Per block, the **transitive closure through its CALL tree of every storage location it touches** —
the set `Ladder.Wave.SlotConflictDerivation.OverlappingReachableState` intersects to produce a
slot↔slot conflict edge, and the provenance `Ladder.Wave.WaveSetAdmission` refuses a slot for
lacking. **Every consumer already existed; nothing computed the sets.** They arrived from
`wave-cli submit --reaches`, i.e. from the submitting agent — *ask of any rule: who computes its
inputs? If the answer is "the party the rule constrains", it is not a rule.*

**Reads count as well as writes.** Two slots cannot share a signal one of them drives and the other
observes, whichever way round; the question is *"could these two tests see each other?"*, never
*"would they collide on a write?"*.

### *** The direction of error is chosen, and it is not symmetric ***

An **over-large** closure separates two slots that could have run together: concurrency lost, nothing
unsafe. An **under-large** one produces the positive claim *"the slots are disjoint on every computed
relation"* about a hazard it cannot see, and puts two agents on one FB instance. Where the two are
traded off, **the larger closure wins.** Three consequences:

- **Instance aliases are canonicalised before intersecting.** An FB writes `IO.Step`; its caller
  writes `iDB_X.IO.Step`. One location, two strings — and left alone a slot testing the FB and a slot
  testing its caller read as **disjoint while driving the same storage**. This deliberately **pools
  where `ProjectUsageGraph.QualifiedPath` does not**: there, pooling *invents* a multi-writer (a false
  accusation against correct work); here it can only *add* an overlap. *Computed disjointness is the
  FLOOR* (D22). Every rewrite is reported, never applied silently — and the converse is pinned:
  `FB_PusherControl|IO.Step` and `FB_ShredderSequencer|IO.Step` stay distinct.
- **Array subscripts are stripped.** Two tests driving different elements of one injection array are
  not independent — `DB_Input.Test[0]` disconnects every physical terminal for both.
- **It closes DOWNWARD only.** Closing upward through callers reaches OB1 from any leaf and would make
  every pair of slots in a plant program conflict. Coupling that exists only in a common caller is the
  author's blacklist to state (§2.5 / D22, add-only).

### *** Absent is not empty, at both levels ***

`reachableState: []` is the positive claim *"computed, and it reaches nothing"*. A closure nobody could
compute **omits the key, and its `provenance` with it** — which is what makes admission raise
`ColouringDefect.ReachableStateNotComputed` and refuse, instead of admitting the most
independent-looking slot in the set. An unparseable corpus file withholds the **whole report**; a call
to a block the corpus lacks withholds **that block, by name**, and the rest are still emitted.

Exit **0** computed · **2** NOT COMPUTED, key withheld · **3** emitted with at least one block withheld.

### The proofs, against `ir/test-project001` rather than a fixture

- **Six slots on `FB_HopperBlockageMonitor` → 15 `OverlappingReachableState` edges → SIX WAVE SETS OF
  ONE.** That answer was reached by hand and written into `conformance-vectors-b.json` as a correction
  (`slotsInWaveSet` 6 → 1) **by an author who said outright it was not verified against `ir/`.**
- **The four `FB_Hx*` blocks → NO edges → one wave set of four.** Not optional: *a producer that finds
  conflicts everywhere passes every test that only checks for conflicts.* Each closure is asserted
  non-empty, so neither green can be the empty-set one.

Mutation-tested: disconnecting the alias canonicalisation reds exactly the test written for it (the
corpus proofs stay green — that rule needed its own); emitting a withheld closure as `Computed: true`
reds two.

## `interface-check` — the static comparison D6's green rests on (2026-08-14, NB-30)

### Its exit 1 is a new kind, and two traps (migrated from CLAUDE.md 2026-08-21)

`converter interface-check --project <ir-dir> --block <name> --enumeration <file> [--json]`

Reads the enumeration's own `response_signal:` values as the REQUIRED set — so the requirement has a
PRODUCER rather than a hand-typed list — walks the block's INPUT/OUTPUT/STATIC (TEMP excluded) and
reports each required signal PRESENT or MISSING.

**Its outcome is a new kind: `exit 1` is a FAIL AGAINST THE BLOCK, not a refusal of the
submission.** Every other gate in this system blames the submission; a missing response signal is
the BLOCK's defect. Measured on the deliverable: `INPUT 0, OUTPUT 0, STATIC 25` →
`PRESENT HopperBlockedAlarm`, `MISSING HopperBlockedInhibit`, exit 1.

⚠️ **Design trap:** the block's INPUT and OUTPUT sections are BOTH EMPTY (it uses a single STATIC
interface UDT, C-132 house style), so a naive check that reads only INPUT/OUTPUT reports *every*
signal missing and looks like a catastrophic finding.

⚠️ **Its cross-file UDT descent is UNPROVEN BY ITS OWN TESTS** — every UDT member in this corpus is
INLINED, so that branch has never run; disabling it left all 20 tests green. A test now ASSERTS that
corpus-wide absence, so the first genuinely non-inlined member forces a re-proof instead of silently
exercising untested code.

```
converter interface-check --project <ir-dir> --block <name> (--requires <n1,n2,...> | --requires-file <path>) [--json]
```

*** YOU DO NOT NEED TO DRIVE A BLOCK TO DISCOVER IT LACKS AN OUTPUT THE SPEC NAMES. *** D1 — a
response signal the enumeration names and the block does not provide — rode inside a *relational*
conformance assertion for a week, and a relational assertion is **structurally blind to any error its
two operands share**. A misbound operand is exactly such an error. The comparison that finds it is a
**two-name set difference** over the block's interface, answerable before a scan elapses, and
**nothing in the system made it**.

Measured on the real artifacts, through the Release binary:

```
EXAMINED: 25 interface member name(s) - INPUT 0, OUTPUT 0, INOUT 0, STATIC 25, CONSTANT 0
  PRESENT HopperBlockedAlarm  @ STATIC/IO/HopperBlockedAlarm
  MISSING HopperBlockedInhibit
*** FAIL AGAINST THE BLOCK: 1 of 2 required response signal(s) are absent ***
```

### 🔴 Exit 1 is a FAIL AGAINST THE BLOCK, and that is the point of the subcommand

Every other outcome in this pipeline blames the **submission** — a `REFUSED` means *fix the vector*.
A missing response signal is a fail against the **block**, and the submission is correct. Reusing a
submission-blaming outcome here would send an author to edit the artifact that is right. The finding
also says outright **not to close it by renaming in the block** — that closes the finding and
destroys it as evidence.

`0` all present · `1` **the block does not carry a required signal** · `2` NOT CHECKED.

### The two wrong implementations were predicted before the build, and are pinned by tests

On this corpus's house style (C-132) an FB's `INPUT` and `OUTPUT` sections are **both empty** and the
whole caller interface is one STATIC member of a UDT — the measured line above says so.

1. **Reading INPUT/OUTPUT only** reports *both* signals missing, including the one that exists. *A
   gate that accuses correct work of the most serious offence in the project is one that gets
   disbelieved, and the day it is right nobody looks.*
2. **Matching comments** finds the word *"inhibit"* in the block's own comment and **passes the
   defect**. So: member names only — never a comment, never a network title.

Both are asserted, and both assert their own **premise** first (that the sections really are empty,
that the word really is in the text), so neither test can quietly stop being about anything.

### Where it declines to judge — three verdicts, not two

- A **dotted path** is `NotAMemberName`, not a fail: the same member is reachable by different paths
  from different callers, so a path makes the answer depend on who is asking. The leaf name is named
  in the refusal.
- A **case-only difference is PRESENT** (TIA identifiers are case-insensitive) **with the block's own
  spelling reported** — two artifacts spelling one member differently is how a name drifts.
- A member whose **type could not be opened** withdraws any MISSING into NOT CHECKED, naming it. A
  MISSING is the positive claim that a block does not carry a name, and that is sound only over a
  **complete** member set. PRESENT survives a partial walk; only the negative half is withdrawn —
  the same asymmetry `reachable-state` chooses on a partial corpus.
- **TEMP is excluded by name and COUNTED ON EVERY RUN, including zero.** A temp cannot be observed
  from outside the block. A requirement found *only* in TEMP is MISSING **and says so**, rather than
  reading as absent for no stated reason.

`--requires-file` reads a plain name list **or an assertion-enumeration YAML directly** (its
`response_signal:` values), and **reports which form it read with a count**. That second form is the
one with a producer: a hand-typed required set is the same self-referential check this exists to
break — *ask of any rule: who computes its inputs?*

The report carries the block's **`ir-hash`** as the stamp it was established against, so a consumer
holds the stamp rather than a bool and *"nobody ran it"* stays distinct from *"ran it against a
different version of the block"*.

### Testing — 22 tests, mutated five ways, and the fifth mutation found a missing test

Both whole-corpus sweeps run against `ir/test-project001` with **closed denominators**
(`swept + skipped == blocks`), because *a gate that refuses everything passes every test that only
checks refusals*: 18 blocks, 12 with an interface — every one accepts its own members, every one
reports an impossible name as a fail.

| mutation | result |
|---|---|
| STATIC dropped (wrong implementation 1) | **16 red** |
| always MISSING | **7 red** |
| always PRESENT | **8 red** |
| TEMP included in the walk | **1 red** |
| **cross-file UDT descent disabled** | 🔴 **0 red — every test stayed green** |

*** THE LAST ONE IS THE FINDING. *** Every UDT-typed interface member in the committed corpus carries
its members **inlined** in the block's own IR, so `TryGetUdt` never ran and the branch was a note
about a branch. Two tests were added: one fixture with a non-inlined member that **exercises** it
(the mutation now goes red), and one that **asserts the absence** — every corpus member is inlined
*today*, so the day a real non-inlined one appears, that test fails and demands the resolution be
re-checked against real data instead of a hand-written fixture.

### 🔴 `--subject` — the wrong enumeration was a FALSE ACCUSATION AGAINST A BLOCK (2026-08-18)

```
converter interface-check --project <ir-dir> --block <name> --requires-file <path> [--subject <text>] [--json]
```

`--requires-file` scraped every `response_signal:` value out of **one** enumeration and **never read
that file's own declared `subject:`**, nor compared it to `--block`. Two enumerations now exist
carrying **28 and 15** distinct response signals with an **overlap of 2**, so pointing a block at the
wrong one demands about **26 signals that cannot be present**: ~26 `MISSING`, **exit 1** — which in
this subcommand's contract is a **FAIL AGAINST THE BLOCK**, the one outcome in the whole pipeline
that blames the code rather than the submission.

**What made it credible rather than obviously wrong** is the house-style trap two sections up: on
C-132 an FB's `INPUT` and `OUTPUT` are both empty, so *"nearly every signal missing"* is a shape this
tool's own documentation predicts as a **genuine** output. A wrong-file run and a catastrophic block
defect rendered identically, and the provenance line named **the path only** — which is precisely the
thing that had been mis-typed.

Two repairs, cheapest first:

- **Always, unguarded: the file's own words are reported.** The `REQUIRED FROM:` line now carries the
  document's top-level `subject:` (folded/literal block scalars included, per-assertion `subject:`
  keys deliberately ignored — they are indented, and pooling them would make the document's subject
  depend on which assertion came last), or states outright that *the file declares NO top-level
  `subject:`*. That line is also printed on a **NOT CHECKED** run, which it was not before: the
  outcome most likely to have been caused by the wrong input file was the one that never said which
  file it read.
- **Opt-in gate: `--subject <text>`.** The caller states what the file must be about. Disagreement —
  or a file that declares no subject at all — is **exit 2, NOT CHECKED**, never 1. *A wrong
  enumeration is an unjudgeable INPUT, not a defective block*, the same ruling already applied to an
  unparseable required name, and this subcommand's exit contract already reserves 2 for exactly that.
  `--subject` without `--requires-file` is refused rather than ignored: silently accepting it would
  hand a caller a guard they believe they have.

Agreement is **containment either way, case- and whitespace-insensitive**, and the rule is stated
rather than tuned. A declared subject is a paragraph of prose and an asserted one is a phrase, so
equality would refuse every real file. The coarseness is safe because of the **direction of error**:
disagreement costs a re-run with a better string, and the flag is opt-in, so nothing is forced
through it.

Finally, on a FAIL where a **majority** of required signals are absent, a `NOTE` names the other
thing that produces that shape and states that this run did not establish the file is about this
block. It **does not decide** — a threshold would be a guess, and the house-style trap means
majority-absent really can be genuine. It puts the alternative explanation in front of the reader at
the moment it matters.

Tested at the CLI boundary where the gate lives, with the positive control that matters: **subject
agreement does not suppress a real FAIL**. Mutations: `Agrees` forced true → 3 red; disagreement
returning 1 instead of 2 → red; the top-level parse relaxed to accept an indented per-assertion
`subject:` → red. Semantics-preserving rewrite (containment operands swapped) → green.

