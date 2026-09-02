# Changelog

## 2026-09-03

**FI-88 — `signal-set` reported a UDT-typed STATIC used 251 times as `unused`; root cause established and step 1 fixed**

Converter only. No IR, no PLC content, no Portal.

- **Root cause, established rather than guessed.** `SignalInventory` expanded a member **iff** its
  sub-members were PHYSICALLY INLINED in the block's own `.ir`, and never attempted type resolution —
  it returned on every `TYPE ` file without parsing it and was never given a `TagTypeRegistry`. A
  `.ir` round-tripped through TIA carries the inlined body and expands; one a generation pipeline
  wrote does not. **The untested hypothesis FI-88 left standing — whether the file had been
  round-tripped — was the right one**: the member that expanded came from a re-export, the ones that
  did not came from the pipeline.
- **The other half is NOT a bug and was not touched.** `ProjectUsageGraph.UsagesReaching`'s
  descendant exclusion stays exactly as it is: once the member expands, the leaf keys match exactly
  and nothing needs a descendant rule. Relaxing it would have traded a false `unused` for a false
  `driven` — the direction that once turned 20 genuine undriven members into 168 driven ones.
- **New `Converter/Ir/MemberExpansion.cs`** — one shared classifier for "how far does this member
  open", replacing three copies of the decision. `InterfaceCheckRunner`'s elementary set and
  `ElementTypeOf`, and `ProjectUsageGraph`'s IEC-instance set, now forward to it: one list, not two.
- **The multi-instance discriminator reads the `BLOCK <KIND> <Name>` HEADER LINE, never
  `TagTypeRegistry._fbInterfaces`** — that index silently omits every re-exported FB (filed as
  FI-92), which would have made a multi-instance of a round-tripped block opaque: a false gate, on
  exactly the corpora this repair serves. Pinned by a test.
- **`signal-set` gates on two causes, reported separately.** A WARNING means a FILE could not be
  read; an OPAQUE MEMBER means a MEMBER'S TYPE could not be opened, so its leaves are missing from a
  set that reads complete. The opaque set is collected from the UNFILTERED entries so `--type` /
  `--direction` cannot suppress the gate; `opaqueMembers[{path,datatype,reason}]` sits beside
  `partial` in the JSON so a generator can say *which* member it may not trust; the reason names the
  search root, the file count and that the scan does not recurse. Exit stays 1.
- **Two branches added beyond the design**, both found by the corpus no-op proof rather than by
  argument: the S7 system identifier types (`HW_ANY`, `CONN_OUC`, …) joined the elementary list, and
  a `VERSION`-carrying declaration terminates as an instruction/library instance. `ir/test-project001`
  declares `InterfaceId : HW_ANY` and `MbServer : MB_SERVER VERSION 5.3` with no inlined body, and
  without these the gate fires on a healthy corpus — on a type whose definition lives in TIA's
  libraries and can therefore never be supplied, i.e. a gate nobody can clear.
- **Invariance is checked, not argued.** New `SignalInventoryTests` sweeps `ir/test-project001` for
  opaque leaves with a stated denominator; `InterfaceCheckTests`'s corpus assertion was KEPT, renamed
  to `RealCorpus_…_SoCommittedDataCannotExerciseTheCrossFileDescent`, and its comment rewritten — it
  had called the descent "fixture-only" and "a branch nothing reaches", and FI-88 is the field
  measurement that it decided whether six function blocks were testable.
- **New `SignalSetUdtExpansionTests`** (16 tests) covers the fix, the inlined/non-inlined identity
  control, the gate and its reason text, the CLI exit code, the multi-instance discriminator including
  the sidecar trap and the shared-name tie-break, IEC and array terminators, the depth cap, and the
  two false-green guards re-asserted on the NON-inlined path.
- **Blast radius: none observed on committed data.** `signal-set`, `harness-binding`,
  `candidate-scan` and `claims` all still exit 0 on `ir/test-project001`, and no new test failure
  appeared (the 9 red tests in `ServedAreaTests` / `NeighbourTests` / `ReachableStateTests` are
  pre-existing and were verified red before this change).
- **Two follow-ups filed rather than done: FI-91** (`undriven-scan` and `cross-check` still expand
  only inlined members, so they now disagree with `signal-set` about one corpus) and **FI-92**
  (`TagTypeRegistry`'s swallowed sidecar throw).

## 2026-08-17

**🛑 The staged development plan is SUSPENDED — docs marked, nothing else touched**

Project owner's decision: time constraints and real application needs. This entry records the
documentation change only; no code, IR, tooling, test or project artifact was modified.

- **Canonical notice lives in `docs/03-development-plan.md`.** Every other surface carries a pointer
  to it rather than its own wording, so there is one place to edit when the suspension lifts:
  `02-roadmap.md` (+ its S9 status block), `docs/notes/stage-gates.md`, `CLAUDE.md` (*Current
  stage*), `README.md`, `docs/00-README.md`, `docs/01-scope.md`, `AITODO.md` (*Project stage*,
  *Current task*, *Outstanding works*), `docs/15-generation-pipeline.md`, `docs/16-future-ideas.md`,
  `agent-tasks/README.md`.
- **Suspended:** the S0–S9 progression, the stage gates and their pending sign-offs, milestones
  M1–M9, and the queues that exist only to advance the plan (FI backlog, unbuilt pipeline skills,
  the dispatch board). S5 and S6 were ACTIVE and are now **frozen**.
- **Not suspended, deliberately and explicitly:** `CLAUDE.md`'s hard rules in full, the
  `docs/13-data-boundary.md` boundary in full including `Live Runs/` retention, the already-built
  tooling, and real application work — which is the reason for the suspension, not an exception to it.
- **Frozen, not waived, and said so in every place it could be misread.** S6's exit criterion stands
  at **1 of 10** fresh requests and its tally is closed to further counting; S0's gate sign-off stays
  pending; S7's `S6 done` entry gate is unchanged and unmet. A suspended stage has **not** passed its
  gate — while suspended the gate rule reads *more* restrictively, not less. The one existing
  exception, the **S9 live-project waiver** (owner, 2026-08-11), is untouched and still in force.
- **Historical records were left exactly as written** — `docs/evidence/`, `docs/audit/`, prior
  CHANGELOG entries, ADRs. They are dated accounts of what happened, and a suspension does not
  revise them.
- ⚠️ **One reading was assumed and is flagged in the notice for correction:** *"real application
  needs"* is taken to mean live delivery continues while pipeline development stops. If the owner
  meant a full stop including live jobs, only the "not suspended" list needs cutting back.
- ⚠️ **The `.claude/worktrees/JOB9004-marker-db` worktree holds its own copy of `docs/` and was not
  edited** — a separate branch; it inherits this when it merges or rebases.

## 2026-08-07

**Multi-instance: the write half was fixed and the read half was missed**

The 2026-08-06 fix made the converter able to WRITE a multi-instance FB call. A probe then proved
that path end-to-end through Portal — import and compile both clean, which is what the previous
fix could not claim. The same probe found the other half was still broken.

- **`to-ir` hard-errored on every block containing a multi-instance** — `"unrecognized Remanence ''"`.
  TIA re-exports the member with **no `Remanence`** (retention belongs to the *called* block's
  members, so there is nothing to state) but **with** an `AttributeList` and a `<Sections>` child
  carrying the callee's entire interface expanded inline. The bare-shape branch required both
  absent, so it fell through to the `Remanence` switch.
- **The consequence was a silently absent gate, not a nuisance.** It took out the re-export→IR diff
  leg of the per-block gate contract, plus `drift-check` and the round-trip harness, for any block
  with a multi-instance — which is every equipment-owning block on the current job.
- **The `<Sections>` child is the discriminator**: an anonymous struct nests `<Member>` directly and
  never has one. The expanded interface is **discarded on purpose** — it is the called block's own
  declaration, already exported with it, and reading it back into the caller would duplicate the
  declaration and let the two disagree. Name and datatype are the whole of what the caller declares,
  which is what the bare shape writes back out, so it round-trips as a fixed point.
- **Tests: 776 → 777.**

**`converter claim` / `claims` — reservations, so two agents on one project stop colliding**

FI-65 component 1. The collisions that break same-project multi-agent work are decided **while
writing IR** and never reach TIA: two agents each scan the corpus for the next free FB number and
both pick 51; both verify alarm bit `%X9` is free and both take it; both append "network 8" to the
same shared FC. Each agent's own `converter diff --only` invariance check passes — the conflict
exists only between them, and a Portal-side queue is structurally unable to see any of it. A log
cannot prevent it either: a log records what already happened, and prevention needs a reservation
taken **before** the work.

- **Six kinds, two semantics.** *Allocation* (`block-number`, `alarm-bit`, `db-member`,
  `block-network`, `tag`) reserves something not yet used and is refused if the corpus already uses
  it. *Exclusive* (`block-edit`) reserves write access to something that exists.
- **`--claims <dir>` is required and has no default.** Agents run in separate git worktrees, so a
  per-worktree claims directory is always empty, grants every claim, and turns the registry into a
  no-op that looks exactly like success — FI-44's "empty is not clean" in its purest form.
- **`--allocate`, not `--suggest`.** A read-only suggestion is the exact race the tool removes: two
  agents are both told "FB51 is free", both act, and one finds out after doing the work. Allocation
  takes the lowest free value atomically and prints what it took.
- **Acquisition is a temp-file write plus `File.Move`.** Opening the slot with `CreateNew` looked
  equivalent and was not — the winner holds its new file open while writing, so a loser hit a
  sharing violation and could not report who beat it. **Found by the parallel-acquisition test, not
  by review**, which is the argument for having written that test at all.
- **`--check` gates on conflicts only.** A fulfilled allocation claim (the block got written, so the
  resource now exists) is the normal end state and is reported without gating; a check that failed on
  success would be ignored on failure. What gates: an exclusive claim on a vanished block, a
  malformed value, and the **cross-kind conflict the filesystem cannot see** — A holds
  `block-edit FC_ControlMain` while B holds `block-network FC_ControlMain:8`, two different values,
  both acquisitions legitimately succeeding.
- `ProjectIndex` gained `(Kind, Number)` and per-block network slots **additively** — one corpus
  dispatch, per `SignalInventory`'s own stated invariant, rather than a second scanner that can
  disagree. `Rules`' three C-501 slice helpers went `private` → `internal` for the same reason.
- 46 tests (822 total). No Portal, no `openness-cli`, no `.ir` touched. **Adoption is not included**:
  nothing yet *requires* a claim before writing, and wiring that into the coding skills is what turns
  the registry from available into binding.

## 2026-08-06

**The converter could not express a multi-instance FB call — in two independent ways**

Found by a coding agent that read a settled architecture, hit the tool boundary, and **stopped
before writing any IR** rather than working around it. The construct — an FB called with one of the
calling block's own statics as its instance, `#ValveA` rather than a global instance DB —
appears **nowhere in the committed export corpus**. It had simply never been written, so nothing
had ever exercised the path.

- **`Remanence` was emitted on every static member.** TIA refuses it on an FB-typed one:
  `"The attribute 'Remanence' cannot be set."` A multi-instance's retentivity is a property of the
  **called** block's members, not of the calling member, so there is nothing for TIA to set. Such a
  member now takes the minimal shape (no `Remanence`, no `AttributeList`).
- **A call's instance scope was hardcoded to `GlobalVariable`**, with a comment stating
  multi-instance was out of scope. TIA resolved the name as a global DB and reported
  `"Missing instance DB"` on a block that had imported cleanly. **The timer path had always done
  this correctly** via its own `ScopeFor`, so the two were asymmetric for no reason beyond nobody
  having needed it.
- **The FB-vs-UDT distinction is derived, not declared.** The datatype cannot decide it — an
  FB-typed static and a UDT-typed one are both quoted names, and the UDT-typed one (the C-132
  interface member) genuinely *does* carry `Remanence`; that is where `RETAIN` on a whole interface
  is expressed. So the set is derived from the block's **own `CALL` statements**: a static named as
  a call's instance is an FB instance, and nothing else can be. That is a fact the file already
  contains rather than a token an author has to remember — the failure mode the `BAREPARAM` token
  already has.
- Timers are unaffected: they arrive as `TimerBinding`s, not `CallStatement`s, and their full
  shape (`VERSION`/`SETPOINT`) is confirmed real.
- **Tests: 772 → 776**, covering both defects and both regression guards — an ordinary
  single-instance call must still scope global, and an ordinary static must still carry `Remanence`.

**The C-501 checker enforced the superseded rule — a compliant block failed review, a
non-compliant one passed**

C-501 was amended by owner ruling on 2026-08-06 to **one network per alarm WORD** (it previously
said one alarm *bit* per network, which the proven site block never did — `patterns/motor-dol`
NETWORK 14 writes three bits of one word and always has). The doc was amended; the mechanised check
was not. `Rules.cs` still required `sliceWrites.Count == 1` plus a network Title, so **code written
to the current convention was flagged and code written to the retired one passed** — the rule base
and its enforcement disagreeing, silently, in the direction that punishes correctness.

- **The check now enforces the amended rule's own three conditions, in its order.** (1) One network
  per alarm word — both halves: all bits written in a network target one word, *and* no other
  network writes that word's bits. (2) Every bit driven by a **single named cause**, never an inline
  expression — a bare tag, or a negated bare tag, since either way you read one name and know what
  the bit is. (3) The **bit map lives in the network COMMENT**, not the Title: the alarm text went in
  the title while a network held exactly one bit, and a word-sized network has nowhere else to put
  it. Condition 3 checks that every written bit is mentioned, deliberately weaker than parsing the
  `%X0 = FTR = "…"` form — the point is that no bit is undocumented, and a format assertion would be
  brittle without being stronger.
- **Findings now say which condition failed**, and a split word names the other networks holding its
  bits, so the reader sees both halves rather than one arbitrary end.
- **It found two real things on first run, which is the argument for having fixed it.** A block whose
  alarm word had been consolidated correctly still drove two of its four bits from inline
  expressions — invisible to the old check, which could only ever see one bit at a time. And
  `patterns/motor-dol` itself now flags: its NETWORK 14 satisfies the new condition 1 (that is why
  the rule was amended) but has **no bit-map comment**, so `%X0`/`%X1`/`%X2` are undecodable from the
  network alone. Condition 3 is new and the pattern predates it; the pattern needs the comment.
- **Tests: 766 → 770.** Three tests encoding the superseded form were replaced rather than adjusted —
  one of them asserted that several bits of one word was a violation, which is now the *required*
  shape. Seven replace them, one per condition plus the negated-cause and clean-word cases.

**C-501 condition 2 permitted no suppressor term, which contradicted C-504 — corrected same day**

The fix above enforced condition 2 as written — "a single named cause, never an inline expression" —
and that clause was itself out of date. **C-504 places alarm filtering in the alarm-write network and
nowhere else**, which is the whole reason suppression cannot reach control: the condensed fault bit
control reads is written upstream of it. A suppressed alarm bit is therefore *necessarily*
`cause AND NOT suppressor`. The two rules together forbade the only correct implementation, and the
newly-corrected checker flagged compliant code on its first run against a real block.

- **The convention now permits one named cause ANDed with negated named suppressors, and nothing
  else** (owner ruling). The prohibition's target was always anonymous logic that hides what a bit
  means, not the uniform mandated decoration — the cause is still the first thing you read.
- **The check is shape-specific, not "does it contain an AND"**: exactly one bare named cause, every
  remaining operand a negated bare tag. `A AND B` still fails — two un-negated tags give the bit two
  plausible subjects — and an OR of causes still fails, since that is what C-130's condensed bit
  exists for.
- **Tests: 770 → 772**, adding the permitted-suppressor case and the two-un-negated-causes boundary
  that the ruling deliberately did not move.

## 2026-08-05

**The mechanical floor could exit 0 having examined nothing — FI-44, FI-45 fixed; FI-46 raised**

Found on the **first run of the four-rung pipeline against a plant it was not tuned on**. CLAUDE.md's
own caveat — *"exercised on one plant across three runs, never yet on a second; treat the rules as
well-fitted to that plant until a different one tests them"* — turned out to be exactly right.

- **FI-44, three silent false-cleans, all now exit 2.** `candidate-scan --scope <token>` returned 0
  candidates and **exit 0**: `--scope` was a path *prefix*, so under C-001 (`<DI|DQ|AI|AQ><n>_<Equipment>_<Signal>`,
  equipment in the middle) it could not address a piece of equipment at all, and the only way to get
  a finding was to name the disputed signals — i.e. to already know the answer. `undriven-scan --fb`
  returned **exit 0 for a block that did not exist** (verified with an invented name). And a D3
  render leg matching no other leg still parsed, so `relation-reconcile` compared it against nothing
  and was satisfied — which the `gen-code-structure` D3 **example itself produced**, keying on the
  iDB name where the parser keys on the spec instance. One principle: **a check that examined
  nothing must not exit 0.** Exit 2 throughout, distinct from a finding, because "the plant is
  wrong" and "the question was wrong" need different actions.
- **The scope fix answers the question, not just fails loudly.** `--scope <equipment>` now returns
  **7 candidates where it returned 0** on the corpus that raised it, and correctly exits 1 — a real
  ambiguity that had been reading as clean. Matching is C-001-position-specific, not generic segment
  matching, so `--scope DB` cannot match the whole corpus and DB-qualified behaviour is unchanged.
- **FI-45, three coverage gaps — failing at *correct* input, which is how a gate dies quietly.**
  `tagstatus` could not walk an ARRAY OF UDT, so **every per-instance binding in a multi-instance
  project read as invented** — precisely the laundering hard rule 3 exists to catch, fired at correct
  code. It ran deeper than reported: `TagTypeRegistry.Resolve` had the same blindness, so an operand
  inside an array of UDT was **typeless for `to-xml --synthesize` and C-118** too. New
  **INDEX-OUT-OF-RANGE** status (its own, because the member is real and the element is not), which
  speaks only when index and bounds are both integer literals — a bounds check that guessed would
  recreate the crying-wolf failure being fixed. `signal-sweep` could not disposition a tag-table
  signal *at all* (dispositions qualified, inventory bare, and the parser only leaves a token bare if
  it contains a dot — which a tag name never does): **38 falsely-unaccounted → 0** on the live
  artifacts. `relation-reconcile` could not tolerate five of its own seven ledger dispositions,
  making `render-BLOCKED` undeclarable.
- **Every fix guards the true positive as well as the false one.** Fixing a false clean by weakening
  the real check would be the worse outcome. Verified on real data, not only fixtures: 639 existing
  member paths still EXISTS, the 5 known true-positive MEMBER-NOT-FOUND names still fail, and 3 new
  INDEX-OUT-OF-RANGE hits were **correct** — a probe using `[0]` on arrays whose lower bound is not 0.
- **FI-46 raised** for three residuals found while fixing these and deliberately left alone: bit-slice
  components (`DB.Word.%X0`) still read as members; `review`/`preflight` never checked for the same
  array blindness; `ParseRender`'s `instance:` regex loose enough to build a phantom leg from prose.
- **Tests: 707 → 766 converter** (+59), golden 39, openness-cli 140 — all green, 0 failed. Four
  parallel workstreams in one tree; this is the single reconciled figure.

**Four-rung structured spec pipeline (A–D) — built, adversarially validated twice, re-run three times**

The autopsy's answer to a correlated-check failure (see the S6-Killer-Plan entry below): split the one
design step into four rungs whose artifacts are **contracts**, so what may enter at each rung is
bounded. Additive alongside `gen-architecture`, which stays validated and in use.

- **The four skills.** **A** `gen-pid-analysis` (topology + per-instance interlocks, process language
  only) → **B** `gen-functional-analysis` (plant behaviours, grouped and scoped) → **C**
  `gen-equipment-spec` (merge A+B+IO into per-equipment control specs; **signals enter here**) → **D**
  `gen-code-structure` (shape → block fit → discharge ledger → boolean render; **booleans and
  interface members enter here, nowhere earlier**). Mechanisms carried straight from the autopsy: one
  relation per line / no ambiguous conjunctions, completeness-by-construction from class references,
  deltas first-class, an A-vs-B conflict is a blocking Q and never a silent merge, and D's discharge
  ledger so a term is never *silently* dropped — the documented cause of the REGRESSION.
- **Two adversarial design validations before any run.** Round 1 walked the four real graded failures
  through A–D: 0 prevented, 2 caught (both conditional), **2 survives** — rung C's ban on interface
  members *forced* the wrong binding for one (the compliant answer was the buggy one), and the
  discharge ledger is a coverage check, not a correctness check, so a swapped pairing is invisible by
  construction. R1–R8 amendments applied. Round 2 closed both survives and found five new seam-level
  weaknesses: a lossy derived register making `review-functional` recommend deleting a *correct*
  interlock; the D3 render not reaching the coder at all; and RW-1 — "verified-cross-block" specified
  citation *form*, not probative *force*, demonstrated by a tag whose only occurrence in the corpus is
  a bare declaration. R11/R12 applied. Round 2's standing verdict — *every remaining check is
  self-judged* — is what produced the mechanical floor below.
  (`docs/evidence/four-rung-design-validation.md`, `…-round2.md`.)
- **Three empirical re-runs on the six failure machines**, each blind to `docs/evidence/`
  (`gen/PlantAutoControl-bench-rerun{,2,3}/`). Run 1 cleared all four graded failures — the unverifiable
  discharge rejected, the undriven rotation input a HARD FAIL, a raw fault bit rebound to the broader
  interface member, an ambiguous candidate set left blocking by design — and surfaced six friction
  items, fixed in the skills. Run 2 exercised all six amendments with no regression and raised twelve
  more, one of them the `tagstatus` member-blindness below. Run 3 exercised the wired mechanical floor
  and earned its keep twice over: it caught two real artifact defects (two global DBs attributed to the
  wrong files — not findable by reading) *and* three defects in the tools themselves.
- **Artifact formats promoted from illustration to CONTRACT.** The wiring had landed the checkers, but
  the skills never stated the formats those checkers *parse*, so a compliant run produced artifacts the
  tools rejected — run 3's first ledger parsed as **0 rows**. Now contractual: rung D's ledger heading,
  column header, `C1`/`P1` ids, em-dash empties, the STOPPED marker, and the requirement that an
  evidence cell carry a backticked identifier that **resolves** (a cell citing only a network number is
  an unfalsifiable claim — 60 such rows were flagged); rung C's derived-register and
  full-disposition-table shapes, on the stated ground that a member dispositioned in *prose* is not
  machine-checkable and reads as unaccounted. Vocabulary completed with **`render-stopped`** for the
  uncontested majority of a stopped instance: two consecutive runs invented their own marker for that
  case because the vocabulary lacked one, and a skill that forces invention is incomplete. Both C and D
  now self-check — run `signal-sweep` / `relation-reconcile` on your own artifacts before finishing.
- **Status, stated honestly in `CLAUDE.md`:** exercised on **one** plant across three runs, never yet on
  a second — treat the rules as well-fitted to that plant until a different one tests them. The
  `references/<class>/` library still self-disclaims as non-standards, so rung A's "completeness by
  construction" is nominal, not delivered (recorded in `docs/16`, not quietly dropped).

**The mechanical floor — five converter checks that survive an agent choosing not to look (FI-36 + FI-39)**

Round 2's verdict was that prose discipline has been pushed about as far as it goes: the residual holes
are ones an agent walks through by **choosing not to look**, and only computed facts survive that. A
design study (`docs/notes/fi-39-candidate-scan-design.md`) sized all five checks at ~1800–2200 LOC and
fixed the build order; items 1–3 read `.ir` only, so no skill edit can rot them (~45% of the effort for
~85% of the demonstrated value). Every check emits **facts, never verdicts**, and each was verified
against its own graded failure on the `PlantAutoControl-bench` fixture, not only in unit tests.

- **`converter trace --binding <bindings.json> --project <ir-dir> [--json]` — guard-containment hop
  (FI-36-min)** — every signal a REQ names as a condition must appear in that coil's guard: a
  set-difference over signal *identity*, **not** a re-reading of the requirement. That is the whole
  point — the REGRESSION shipped because the coder and the functional reviewer resolved the same
  ambiguous `/` identically, so the review *confirmed* the defect instead of catching it. Reported per
  writing site, never unioned (a term present in one network and absent in another is exactly the
  multi-instance shape a union hides); a present term on a disarmed writer is reported present and
  marked. `trace` stays exit 0 — a declared facts provider; a skill that wants to gate reads the JSON.
  ~110 LOC on the FI-25 v2 substrate. Verified on the real graded pair: MISSING on the generated block,
  OK on the sealed answer key, same binding.
- **`converter candidate-scan --project <ir-dir> --fb <FBName> [--scope <prefix>...] [--type <T>]
  [--direction status|command|any] [--json]`** — computes **every** signal that could satisfy a
  requirement: the IO half from a new shared `SignalInventory` primitive, the FB half from the block's
  own interface — so "more than one candidate" is a computed fact, not a judgment call. Two defects
  shipped because a phrase admitted more than one signal and the single reader resolving it never
  noticed a choice existed. Direction is **computed from the usage graph, never from the interface
  section**: on the real corpus an FB's reportable status members live under STATIC inside
  interface-UDT structs while INPUT/OUTPUT carry data-link words, so section-filtering is wrong on
  exactly the block the defect concerns. `--phrase` is advisory only and never narrows. Exit 1 when the
  IO half has more than one candidate — which is what makes an ambiguous binding non-discretionary.
- **`converter undriven-scan --project <ir-dir> --fb <FBName> [--instance <iDB>...]
  [--caller <file.ir>...] [--hints] [--json]`** — **per-instance** interface drive states: driven /
  disarmed (all writers provably-false-guarded) / undriven (default X) / dead-interface. Per-instance is
  the point: `cross-check` pools across every instance of an FB, so one driven instance masks an
  undriven sibling — and a bypass dropped on *one* instance of a shared block is exactly that shape.
  Exit 1 on undriven/disarmed; dead-interface reports and never gates.
- **`converter relation-reconcile --specs <dir> --ledger <code-structure.md>
  --register <requirements.md> [--project <ir-dir>] [--json]`** — reconciles (instance, relation-id)
  sets across the relation-bearing artifacts and checks that each verified-cross-block precondition
  cites something actually **written**, closing RW-1's citation-form-vs-probative-force loophole. Keyed
  on (instance, id), never the bare id — two instances' `C1` are different relations, so a bare union is
  vacuous. Two guards make it trustworthy rather than decorative: an **ABSENT** leg (a stopped D3)
  reports ABSENT and never gates, because reporting "0 differences" for an artifact that does not exist
  is the silent-green failure this check exists to avoid; and a leg parsing **zero rows is a hard
  error**, because format drift is the whole risk. Exit 1 on any difference.
- **`converter signal-sweep --project <ir-dir> --specs <dir> [--register <file>] [--unclaimed <file>]
  [--json]`** — the project-level residual: computes the exact denominator (every global-DB leaf plus
  tag-table tag) and classifies each signal claimed-by-spec / disposed / unaccounted, replacing an
  artifact's self-reported "~190 swept / ~80 out-of-scope / ~40 safety" with computed integers. An
  approximate denominator cannot support a completeness claim. Bare leaf names are qualified by their
  enclosing heading before comparison — without that every leaf silently mismatches and the whole sweep
  reads as unaccounted. Honest limitation stated in the output rather than papered over: a signal
  dispositioned only in *prose* reads as unaccounted, and that pressure is the point. Exit 1 if any
  signal is in no spec and no disposition table.
- **Wired into the skills, because a tool nothing invokes changes nothing.** A grep after the build
  showed **zero** skills referencing the new checks — the floor was inert. `review-functional` now
  requires a `guard: { coil, must_contain }` anchor on every REQ stating a condition, and a
  missing-term verdict is a finding to re-derive, never waved through (placed there deliberately: that
  pass is the one that graded the REGRESSION as a match). `gen-equipment-spec` computes its candidate
  set with `candidate-scan`, so exit 1 is the trigger that makes the blocking Q non-discretionary
  rather than self-judged. `gen-code-structure` runs `undriven-scan` per instance. Each carries the
  same honesty note: **the tool computes the FACT, the agent still owns the JUDGEMENT — a floor, not an
  oracle.**
- **Calibrations made against the real corpus** rather than in the abstract, each because the
  design-as-written would have shipped noise: `dead-interface` reports but never gates (gating yielded
  132 findings on one rich FB, since a reusable block legitimately exposes unused optional inputs);
  `undriven-scan`'s name-token hints are opt-in and never part of the exit condition (they fired on
  every undriven member and buried the real findings — a hint that is usually wrong trains readers to
  ignore output); and `candidate-scan`'s exit keys on the **IO half** (a total-size trigger fired on
  every query, degenerating the rule it drives into "always cite a basis"). A fourth followed from run
  3: `undriven-scan` now excludes any member the FB itself writes — FB outputs read back internally were
  being reported as "undriven (default FALSE)" from the caller's side, ~2/3 noise and actively
  misleading (204 → 144 pairs). Two refinements left deliberately open and written down in `docs/16`
  rather than quietly dropped.
- Run 3 also found all four new subcommands **missing from `--help`** — they worked but were
  undiscoverable. Fixed.
- Converter suite **707/707** (671 → 707 across this wave: +3 `tagstatus`, +6 guard-containment, +8
  `candidate-scan`, +6 `undriven-scan`, +7 `relation-reconcile`, +6 `signal-sweep`). openness-cli 122
  and the golden harness 37 unaffected.

**FI renumbering — this branch's FI-32..35 → FI-36..39**

Master independently allocated FI-32..FI-35 to four *different* ideas (IR-as-restricted-language,
authored object model, parameterized pattern library, `alarm-scan`) while the spec-pipeline branch was
running, so merging as-is would have left `docs/16` with two of each. Owner's call: master's numbers are
canonical — they are on the trunk and may already be cited — so the branch's four move.

- FI-32 → **FI-36** per-instance interlock-completeness trace; FI-33 → **FI-37** spec interlock
  explicitness; FI-34 → **FI-38** interlock-safety disposition; FI-35 → **FI-39** `candidate-scan` /
  the mechanical floor.
- 29 files updated (skills, `CLAUDE.md`, evidence docs, run artifacts, C# source and tests), and
  `docs/notes/fi-35-candidate-scan-design.md` renamed to `fi-39-…` with its inbound reference fixed —
  per `CLAUDE.md`'s rule to grep repo-wide after any doc rename. Verified: zero stale FI-32..35
  references and zero references to the old filename remain.

**`converter tagstatus` resolves DB members — a correctness fix to the hard-rule-3 anti-laundering gate**

**A user-visible behaviour change.** `tagstatus` classified at **DB-root level only**, so an invented
member passed the very gate that exists to stop it — `HMIControlSignals.CompletelyMadeUpMember → EXISTS
… 0 proposed`. `gen-block-new` gates its run on that result, so the production coding skill's
hard-rule-3 protection was weaker than documented. Found by a pipeline run that grep-verified members
independently.

- Member resolution reuses `TagTypeRegistry` (the cross-file DB/UDT index behind the C-118 rule) — one
  implementation of "does `DB.member` exist", none to drift. Root resolution is unchanged
  (`ProjectIndex` + `AccessNode.FromDottedPath`, so the `Clock_0.5Hz` literal-dot case still resolves
  whole-name-first). Plus `TagTypeRegistry.TryGetDb`, so a member-less DB stub is not mistaken for
  "member absent".
- **Four states, not two**, so the fix cannot manufacture false gaps in the other direction: `EXISTS` /
  `PROPOSED` (root absent) / `MEMBER-NOT-FOUND` (root real, member invented) / `MEMBER-UNCHECKED`
  (namespace not enumerable — an unexported UDT, or a `create-instance-db` stub with no member tree).
  Exit non-zero on `PROPOSED` or `MEMBER-NOT-FOUND`; `MEMBER-UNCHECKED` reports, never guesses. New
  `--roots-only` restores the old root-level behaviour, which is the Design stage's mode — it designs
  *against* gaps.
- Skills updated: `gen-block-new` (the gate is now real), `gen-equipment-spec` (it binds members),
  `gen-architecture` (`--roots-only` is its mode). Verified on the real corpus: the tag a run would have
  laundered plus two invented members → `MEMBER-NOT-FOUND`, exit 1; real members including nested
  instance-DB paths → `EXISTS`, exit 0. Converter suite 674/674 at this change.

**Live-run data boundary — full Red-confidentiality access, zero retention**

`Live Runs/` is now a different regime from the rest of the repo: the AI works against **everything** the
owner puts in a job folder, including material that would elsewhere be Red-tier on confidentiality
grounds (contract/NDA-restricted, commercially sensitive). No tier-triage, no per-file approval —
placing the job there *is* the decision. The entire boundary for live-run data is **retention, not
access**: use anything, commit nothing.

- `docs/13` — the tier table's Red row split into **Red—confidentiality** (opened inside `Live Runs/`)
  and **Red—safety** (never, everywhere; hard rule 2 stands, unchanged and explicitly *not* inferable
  from "full access"). Retention prohibition 1 hardened: never narrow or negate the ignore patterns, no
  `git add -f` — broad access is only safe because retention is zero, so the two halves are load-bearing
  on each other. "Live runs opened" is now a **job-code-only** register; job identity and scope stay in
  the gitignored folder.
- `.gitignore` — live-run patterns unanchored and spelling-tolerant, so a live-run folder at *any* depth
  is covered, not just the repo root. `CLAUDE.md` carries the same rule in short form so it lands
  without opening `docs/13`.

**S6-Killer-Plan — a blind regeneration graded against a sealed answer key (run 2026-07-20)**

Seal a real 20-network orchestration FC as an answer key, reverse-derive a requirements register from it,
hand a blind Candidate the complete given boundary, and grade what it produces against the original.
Score: **MATCH 14 / IMPROVEMENT 1 / DEFECT 3 / REGRESSION 1 / SPEC-GAP 3** — it fails its own
match-or-better bar. The failure *is* the value: everything above exists because of the autopsy it
forced.

- Phases 0–2: a sealed Green answer key plus six Green global DBs, all sanitized through the existing
  maps and leakage-gated; a **FROZEN** reverse-derived 22-REQ register (ban-list clean — no HOW in REQ
  text); and the complete Green given boundary, 28 dependencies (8 FB types + 20 instance DBs), as
  `ir/PlantAutoControl-bench/`. Then a blind Candidate architecture, gate-1 approval, and a generated
  20-network FC that **compiled clean, 0 errors**, through the real compile gate — composed from a
  tier-(b) chained-permissive-enable pattern, tier-(a) CALLs and ~5% freeform.
- The three review skills then ran fresh on it: functional cleared it 18/22 full with nothing
  contradicted, disarmed or gold-plated; conventions raised two error-tensions and a naming finding;
  simplicity cleared conditionally. **All three passed the block that carried the REGRESSION.**
- **The autopsy (`docs/evidence/PlantAutoControl-bench-autopsy.md`) is the load-bearing artifact.** Every gap
  traced back to the register — they were all in there, in varying forms. Three failure modes:
  caught-but-not-blocking (flagged partial, shipped); **correlated misreading** — coder and reviewer both
  collapsed the register's ambiguous `/` dual-hold, and the reviewer then recorded "verified match"
  against its own reading; and underspecification absorbed as inference. Root cause, in one line: *an AI
  reviewer reading the same register as the AI coder is a **correlated** check, not an independent one.*
- Raised in `docs/16` as the autopsy fixes (later renumbered, above): **FI-36** a mechanical per-instance
  interlock-completeness trace in `review-functional`, on the FI-22/FI-25 substrate; **FI-37** no
  ambiguous conjunctions in the spec interlock table and an underspecified interlock is a blocking Q;
  **FI-38** the coder surfaces interlock ambiguity instead of silently under-constraining, and a dropped
  guard is a blocking fail.
- Signal for the roadmap: pattern composition is strong; **freeform interlock reconstruction
  under-constrains and sheds guards**. None of this counts toward S6's ten fresh generation requests.

**`docs/16-future-ideas.md` — mechanical-tooling backlog marked cleared (milestone)**

An explicit milestone line in the Implementation-status section: the value/leverage build wave shipped
every ranked buildable-now item, and what remained was AI-by-design, externally gated, or owner-held.
(The mechanical floor above is a *new* band raised after that line, not a leftover from it.)

## 2026-07-29

**HMI alarm-generation learnings — the knowledge base's first use for delivery, not development**

First time this tooling produced output for a real job rather than advancing a stage of the project:
extracting a live project's alarm definitions into an HMI discrete-alarm import spreadsheet. The
spreadsheet itself is local-only (gitignored); what landed here is the learnings.

- **`docs/notes/hmi-alarm-generation.md`** (new) — a grounded write-up of the run. Key finding: a project
  has **two** correct alarm surfaces — C-501 category words in the alarms DB and C-503 per-instance UDT
  alarm words — and C-503 says explicitly that category words are *not* duplicated per instance. Read
  live as "the mapping is missing"; it wasn't. Recorded as a trap, because a tool flagging unmapped
  instance alarm words as a defect would false-positive on every conformant project. Also: C-501's two
  conditions (one bit per network, the title states the alarm text) are what make mechanical extraction
  cheap; network titles are the *sole* text provenance (zero member/network comments existed); and
  INCONSISTENT blocks cannot be exported at all, so an alarm sweep can be silently incomplete and must
  say what it could not read.
- **`docs/16-future-ideas.md` — FI-35 `converter alarm-scan` + HMI alarm-list generation.** Verdict is a
  split: build the extraction half now-ish (facts, C-501/C-503 aware, hard-errors on non-conformance,
  stands alone as an explain/review aid); hold the HMI-generation half behind FI-18, which owns the two
  boundary questions this hit — HMI-side bit numbering within a Word trigger tag, and how C-506 severity
  and C-507 acknowledgement both map onto the import format's single Class column. That last one bit
  live: a blanket non-acknowledging fill matches C-507's default but contradicts C-507's own named E-Stop
  exception. Open and deliberately not investigated: whether Openness can drive the HMI alarm list
  directly instead of via the UI spreadsheet — no API was used this run, so there is no compile-gate
  analogue on the HMI side.
- **`docs/13-data-boundary.md`** — a scoped read-only Amber approval for the live TIA project data (owner
  asked scoped-vs-broad, chose scoped). `lad-coder` correctly refused the work until the approval
  existed, and separately flagged that every prior approval was scoped to advancing a stage of *this*
  project while this one is production output for a real job — recorded as its own paragraph rather than
  folded in.
- **`docs/notes/openness-quirks.md`** — `export --out` must be **absolute**. Siemens's own `Export()`
  rejects relative paths, surfacing as a type-qualified engineering exception whose *last* line is the
  actual cause; a loop over several blocks then fails identically on all of them, which reads like a
  connection problem.
- Also carried three `docs/16` entries written 2026-07-27: **FI-32** replace IR with a restricted real
  programming language, **FI-33** an authored interface/object model, **FI-34** a programmatic pattern
  library.

## 2026-07-20

**FI-25 timing hop + FI-09 C-125 + FI-17 sidecars (three parallel tracks) — mechanical-tooling backlog cleared**

Three file-disjoint parallel tracks; the last three ranked buildable-now items.

- **`converter trace` timing hop (FI-25 v2 — tracer now complete, all 5 hops)** — `timing: { timer,
  seconds_member }` verifies the timer's PT is the ×1000 ms form of the bound seconds member. Keyed on the
  timer's UNIQUE PT ms-member name (`OvercurrentMediumDelayMS` ↔ `OvercurrentMediumDelay`), not the shared
  `Time` scratch tag — so a wrong-member binding is correctly `contradicted` (the shared-scratch
  pair-crossing). Walks the timer's block's Timers/Converts/Muls directly. Documented limitation: the exact
  MUL↔CONVERT `EN:=ENO` sidecar wire isn't verified (a sidecar-level refinement). `Trace/`.
- **`converter review` C-125 (FI-09)** — a C-122 dwell-timeout timer's fault bit must live in the interface
  UDT, not a private Static/DB. Safe contrapositive framing (fault bit = reads a C-122-subject timer's `.Q`,
  `CoilTag` ends `Fault`, self-latch cleared by `NOT …FaultReset`), then C-118's UDT-home check; warn,
  NotApplicable without `--project`. Clean on both real steppers, no `PressureHold` false positive. Completes
  the C-118–125 sequencer family (bar the by-design-AI clauses). `Review/`.
- **`converter ir-hash` + explanation-sidecar convention (FI-17 pilot)** — `SHA-256(SerializeBlockReadable)`,
  a stable readable-IR content key immune to SIDECAR/UId churn (not `digest --fingerprint`, which abstracts
  tags), + `docs/notes/explanation-sidecars.md` (orientation-only / never-review-input / hash-on-read
  invalidation, the ADR-0005 discipline) + an optional explain-plc-block caching note. `Converter/IrHash/`.
- Converter suite **671/671**; golden unaffected. The mechanical-tooling backlog from the future-ideas
  survey is now essentially cleared; the FI-09 remainder is AI-by-design.

**FI-25 v2 disarmed hop + FI-09 sequencer rules C-119/120/122 (two parallel tracks)**

Two file-disjoint parallel tracks (FI-25 v2 on the main track; FI-09 C-119/120/122 as a subagent).

- **`converter trace` disarmed hop (FI-25 v2)** — the forward tracer now flags logic that is *built but
  switched off*: a path whose every writer is gated `NOT AlwaysTrue` (the S7 always-on bit) → `disarmed`
  (which review-functional treats as NOT implemented). The graph carries each write's guard Expr (additive
  `Guard` on `DirectedTagUsage`/`UsageSite`); a pure three-valued constant-fold (`Trace/DisarmAnalysis.cs`,
  `AlwaysTrue⇒true`) detects `NOT AlwaysTrue` standalone or ANDed, leaving a bare `AlwaysTrue` armed. Real
  corpus: `IO.HandReverse` (`:= NOT AlwaysTrue`) → disarmed. Only the timing hop remains v2.
- **`converter review` C-119/C-120/C-122 (FI-09)** — sequencer structural rules on the C-118 `--project`
  seam: C-119 (idle = step 0 present, Error), C-120 (steps ascend ×10, Warn), C-122 (dwell-timer shape — IN
  gated `Step=<n>`, Static instance not `DB_Timers`, PT a UDT settings member; PT-home NotApplicable without
  `--project`). Careful subject-filtering (only Step-gated timers are C-122 subjects) → clean on the real
  steppers, no false positives.
- Converter suite **659/659**; golden suite unaffected.

**FI-25 forward-pass tracer + two parallel unblocked follow-ups (FI-09 C-118, FI-22 aliasing)**

The next actionable band after Tier C, built as three file-disjoint parallel tracks (FI-25 on the main
track; FI-09 C-118 and FI-22 aliasing as concurrent subagents), merged clean.

- **`converter trace --binding <bindings.json> --project <ir-dir>` (FI-25)** — the review-functional
  forward-pass verdict tracer. The reviewer emits a per-REQ binding (out_tag / iface_member /
  number-constraint); the tracer walks it over FI-22's `ProjectUsageGraph` (read-only) + DB start values
  and emits per-hop FACTS + candidate verdicts (never an adjudicated pass). v1 hops: output-path
  (unimplemented), interface-chain (broken-chain / in-cycle-lamp), number-constraint
  (ok/contradicted/partial). Disarmed + timing hops deferred to v2 (need write-condition storage /
  net-new dataflow). `Converter/Trace/`. Wired into review-functional Pass 1. 4 tests.
- **`converter review --project <ir-dir>` C-118 (FI-09)** — the first cross-file review rule: builds a
  `TagTypeRegistry` UDT/DB index and verifies a stepped sequence's phase is exactly one `Step:Int` in the
  block's interface UDT (flags bare-Static / DB_Controls/DB_Settings placement / non-Int). NotApplicable
  without `--project`. C-125 deferred (blocked on un-mechanized C-122).
- **cross-check dead-wiring completed (FI-22 aliasing follow-up)** — the dead-member table now covers FB
  interface-UDT members, not just global-DB: writers/readers are pooled across the FB-internal bare form
  and every `iDB.<suffix>` alias (via `DbSource.InstanceOfName`) before the deadness test, so a member
  written internally and read via an iDB is correctly not dead. `[global-db]`/`[interface]` scope labels in
  the output. Real corpus surfaces genuine dead interface members (e.g. `FB_ShredderSequencer.IO.InCycle`)
  with no false positives.
- Converter suite **639/639**; golden suite unaffected.

**Tier C review mechanization — cross-check facts, digest fingerprints, two more review rules (FI-22/23/09)**

The last band from the value/leverage survey of `docs/16-future-ideas.md`: review-mechanization tooling
that compounds across future reviews. Built in three parallel tracks (FI-22 on the main track; FI-09 and
FI-23 as concurrent subagents), merged clean. Each new mechanical output is verbatim facts the AI reviewer
reasons over, never a verdict.

- **`converter cross-check --project <ir-dir>` (FI-22)** — whole-project cross-block reference-graph facts:
  a multi-writer table (C-308), a dead global-DB-member table (review-functional Pass-2 dead-wiring, both
  directions), physical-IO references (C-304), and per-block sibling references (C-127). New
  `TagReferences.AllDirectedUsages` (direction-tagged reader/writer extractor) + `CrossCheck/ProjectUsageGraph`
  (whole-export usage index — the substrate FI-25 will reuse). Exit 0 (facts dump). Dead-wiring scoped to
  global-DB members; iDB/interface-UDT aliasing is a documented follow-up. Surfaces real signals on the corpus
  (a dead mapped input; unused overcurrent setpoints). 5 tests.
- **`converter digest --fingerprint` (FI-23)** — per-network normalized structural signature (SHA-256 of a
  canonical, tag-abstracted walk of the statement/`Expr` structure; literal values kept to catch a template
  copy that changed a constant), so copy-pasted networks collapse to one hash and the outlier stands out —
  serving `explain-plc-block`'s "verify every instance" method. Default output unchanged; orientation-only
  (not review input). `Digest/NetworkSignature.cs`. 7 tests.
- **`converter review` C-103 + C-121 (FI-09)** — C-103 (Set/Reset pairing, Warn) and C-121 inline form
  (Step-transition = MOVE guarded by `Step = <from>`, Error), both candidate-worded where the doc-06 exception
  is cross-block/judgment. The audit re-scoped the rest of FI-09: C-107/C-402 is subsumed by FI-22's
  multi-writer table, C-118/C-125 need cross-file UDT resolution, C-119/120/122 need AI sequencer-identification
  first — all deferred with dependencies recorded. `Review/Rules.cs`. +15 tests.
- Converter suite **625/625**; golden suite unaffected (still green).

**Tier B correctness/friction wins — export-drift detector, preflight flow-order check, read-only portal-status (FI-26/27/28)**

The next band from the value/leverage survey of `docs/16-future-ideas.md`: three cheap, session-proven
items, each with a documented "this cost real time this session" grounding, all reusing built
machinery. Converter track (FI-26/27) and openness-cli track (FI-28) built in parallel.

- **`converter drift-check --project <ir-dir> --exports <simatic-ml-dir>` (FI-26)** — detects silent
  ir↔simatic-ml export drift (a fix that landed in the `.ir` but was never re-exported). Rebuilds each
  `.ir` in-memory via the shared `BuildXmlFromIrText` (factored out of `ConvertToXml` — one code path)
  and `Normalizer`-compares to the committed `.xml`: MATCH / DRIFTED / SKIPPED / ERROR, exit non-zero
  on drift. `Converter/DriftCheck/`. Plus an `ExportDriftDetectorTests` golden guard asserting the
  drift set equals a documented KNOWN-drift baseline (green now, red on new drift). On the real corpus
  it correctly surfaces the 6 test-project001 blocks stale from the re-arming fix; `reference` clean.
- **`preflight` `flow-order` check (FI-27)** — validates each synthesized network's serialized
  instruction `<Parts>` are in TIA's DFS-from-rail flow order, catching offline (the Normalizer masks
  raw Part order from every equivalence oracle) a regression that would otherwise only surface as a
  live-import rejection. Exposed `FlgNetWriter.FlowOrderedPartUIds` as the single source of truth;
  pure `FlowOrderCheck` (`Converter/Preflight/`). Playbook entry added for "must be sorted according
  to the current flow".
- **`openness-cli portal-status` (FI-28)** — read-only Portal-process diagnostic: classifies
  `TiaPortal.GetProcesses()` output vs `LaunchedInstanceRegistry` into in-use / self-launched-orphan /
  stray-empty, with a pileup-vs-first-connect note; reads `ProjectPath`/`Id` without `Attach()`; never
  attaches/launches/kills; always exits 0. Split into a Siemens-free `PortalProcessInfo` POCO + pure
  `PortalStatusClassifier` behind a thin gateway enumerator. The safe read-only half of FI-07 (Parked).
- Tests: 3 `DriftCheckTests` + 4 `FlowOrderCheckTests` + 2 `ExportDriftDetectorTests` golden (converter
  605/605), and 21 `PortalStatusTests` (openness-cli 122/122).

**Tier A S6-loop accelerators — `reuse-scan` + `target-scan` converter subcommands, `gen-architecture` adopts `tagstatus` (FI-24/29/30)**

The three highest-leverage items from a value/leverage survey of `docs/16-future-ideas.md`, chosen
because each shaves cost off a step at the front of *every* S6 generation request (9 still to run
before the S6-exit gate). All reuse existing primitives (`ProjectIndex`, `DigestBuilder`,
`tagstatus`) — tooling-side only, no roadmap/ADR change.

- **`converter reuse-scan --project <ir-dir> [--tag …] [--kind …]` (FI-29)** — digest-backed
  reuse-first query: which blocks reference a tag and/or implement a statement kind. `--tag`
  (block-level roots) and `--kind` (network-level statement) ANDed across groups; unknown `--kind`
  is a hard error. Surfaces candidates, never rules "duplicate". Exit non-zero if any candidate
  found. `src/converter/Converter/ReuseScan/`. Would have caught the `discharge-conv-monitor`
  overlap with `FB_ShredderSequencer` up front (a full analysis cycle was abandoned to it).
- **`converter target-scan --requirements <register.md> --project <ir-dir>` (FI-30)** — the S6
  new-block target gap-hunter. Cross-joins register REQs against a fresh tag-status classification
  and the corpus, bucketing each REQ Candidate / LikelyImplemented / Disqualified / Withdrawn with
  the mechanical disqualifier (hmi-only / out-of-scope / proposed-tag-blocked / q-open) pre-computed.
  The "already implemented inline" signal is a *separate heuristic* bucket, never folded into the
  mechanical layer. Exit non-zero if zero candidates. Register parsing is a focused reader of the
  register's strict `## Format` contract. `src/converter/Converter/TargetScan/`. On the real 69-REQ
  test-project001 register: 50 of 69 pre-cleared, 19 to confirm.
- **`gen-architecture` adopts `converter tagstatus` (FI-24 tag-status half)** — the design skill's
  tag-status pass (Inputs, Method step 7, section-9 output, mini-manifest) now calls the built
  `tagstatus` tool instead of hand-grepping; `tagstatus:*` added to its Bash allowance. A `PROPOSED`
  result stays an expected section-9 outcome (a named gap), not a run-stopping gate.
- 10 new tests (`ReuseScanTests`, `TargetScanTests`); converter suite **598/598** green.

## 2026-07-18

**`converter diff` — sidecar-less inputs + `--only` multi-network parsing (gen-block-modify-fix findings)**

- `diff` now accepts **sidecar-less** inputs (a freshly-authored / validation-corpus block), not only
  real TIA exports — it branches on `HasSidecarSection` like `review`/`preflight`; the comparison was
  already sidecar-free, so a sidecar-less as-built diffs against a sidecar-carrying fixed block fine.
- **`--only` now accepts space- or comma-separated numbers and repeated flags** (`--only 1 2`,
  `--only 1,2`, `--only 1 --only 2`). The docs implied space-separated but the parser took only a single
  value, which blocked a multi-network scoped fix — parser fixed to match.
- Both found in the first `gen-block-modify-fix` validation (2026-07-18). 2 new tests; converter suite
  529/529. The `gen-block-modify-fix` / `gen-block-new` skills folded the run's other findings too (the
  compound-operand-must-lead synthesis rule; the UDT/`TYPE` has-no-network-invariance note; the
  FB-with-timers instance-DB compile note; the validation-corpus telemetry note).

**`SidecarSynthesizer` mints wired-argument CALLs — reusable formal-parameter FBs are now integrable**

- `converter to-xml --synthesize` previously supported **zero-argument CALLs only** (the site's
  STATIC-struct convention), so a brand-new orchestrator block could not CALL a reusable FB that
  exposes formal INPUT/OUTPUT parameters. Found blocking during the genval2 (ShredderControlSystem) blind
  validation, whose owner-chosen reusable / C-304/C-127-clean architecture gave the control FB formal
  parameters.
- Wired Input/Output args now synthesize: each argument's `Type` comes from the callee's own `.ir`
  interface via a new `CalleeInterfaceRegistry` (ADR-0001 — the callee `.ir` is the source of truth
  for its interface; the readable CALL deliberately omits types), mirroring exactly what the read side
  records from a source `<Parameter Type=…>` element (`GraphReducer.ReduceCall`). `to-xml --synthesize`
  builds the registry from the batch's own block files plus an optional `--project <ir-dir>` export.
  A missing callee/param or a section mismatch is a clear hard-error, never a silent guess. InOut
  params and the Word→Int CONVERT typing gap remain separate follow-ups. **No writer change** — the
  existing wired-CALL `<Parameter>` emission (proven by PlantAutoControl's round-trip) is reused.
- 6 new tests (`WiredCallSynthesisTests`); converter suite 527/527. **Live TIA gate PASSED
  (2026-07-18):** a synthesized wired CALL to the formal-parameter `FB_ShredderControl` imported and
  compiled clean in `SampleProject` (0 errors, wired call resolved: "Number of updated calls … 1") as
  part of the full genval2 subsystem build (10 blocks: 3 UDTs, 3 buffer DBs, 2 Map FCs, the FB, the
  orchestrator). Two follow-ups surfaced during that build: the **Word→Int CONVERT** typing gap
  (REQ-002 telemetry, deferred — only Real→DInt is typed) and a **new UDT-typed-param inline-nesting
  bug** (an INPUT/OUTPUT param typed as a UDT with inline nested members mints leading-whitespace
  member names, e.g. `Name="  Ready"`; workaround: declare UDT params as bare type refs, no inline
  expansion — worth a converter fix).

**`SidecarSynthesizer` infers integer/comparison types by magnitude — fixes UDInt synthesis**

- `converter to-xml --synthesize` previously hardcoded every comparison's `SrcType` and every
  non-decimal literal's `ConstantType` to `Int` (`SidecarSynthesizer.BuildCompareStep` /
  `InferLiteralConstantType`), so a UDInt comparison against a large constant (e.g. a rollover guard
  `HrsRun >= 4294967295`) synthesized as Int and was rejected at TIA import ("The value '4294967295'
  cannot be set for the parameter of the type 'Int'"). Found 2026-07-18 during the blind
  `gen-block-new` validation of `FB_FilterUnitSystem` (REQ-023).
- Now the integer type is the narrowest that holds the literal — `Int` for in-range values (Step
  numbers, counter increments — unchanged), widening to `DInt`/`UDInt`/`LInt`/`ULInt` only when the
  value genuinely exceeds Int. A comparison's `SrcType` is inferred from its literal operand's type
  (widest if both are literals), defaulting to `Int` for a tag-vs-tag comparison (no symbol table, so
  a wide tag-vs-tag comparison stays unimplemented, not silently mistyped); a decimal literal still
  types as Real.
- 2 new tests (small-Int path unchanged; UDInt widens both `SrcType` and `ConstantType` and still
  feeds `FlgNetBuilder`); converter suite 521/521.

**C-001 member/variable naming mechanized in `converter review` — FI-09 rule #2**

- `converter review` now checks C-001 (members/variables are short PascalCase, underscore-free) as a
  deterministic rule (`Rules.CheckC001MemberNames`, recursing nested struct members), moved out of
  the `review-conventions` hand-sweep. A name is clean iff non-empty, starts uppercase, and is
  otherwise letters/digits only. Applies to **DB members (including TIA-generated iDB members — not
  exempted, owner's call), UDT (`TYPE`) members, and block interface variables**
  (Static/Temp/In/Out/InOut/Constant). Tag-table entries stay exempt — physical-IO tags keep their
  underscores by design.
- To check UDTs, the TYPE dispatch (previously blanket-NotApplicable) now parses the type and runs
  C-001, marking every other rule NotApplicable; TAGTABLE stays blanket-NotApplicable. First rule to
  fire on the real corpus: 26 findings — the `Snake_Case` buffer members in `DB_Input`/`DB_Output`.
  iDB and UDT members are checked and clean. **TIA-informative system params are exempt** (a
  member's `Informative` flag — e.g. an OB's `Initial_Call`/`Remanence` startup-info inputs): they're
  TIA-provided and not renameable, so C-001 skips them (owner ruling 2026-07-18).
- 7 new tests (5 rule fixtures + TYPE-reviewed and tag-table-exempt runner tests); suite 510/510.
  Also fixed a latent nullable warning on the C-406 `TempMembers` arg (now null-coalesced like
  `StaticMembers` beside it). Forward-note added to the 2026-07-16 conventions-validation evidence.

**`converter review` now reviews sidecar-less blocks (found while verifying C-408)**

- `converter review` used to fail a `.ir` with no `SIDECAR` section ("Expected a 'SIDECAR' section")
  and, with `--ignore-errors`, mark it "could not be reviewed" — so a committed sidecar-less block
  (`OB100.ir`, hand-authored without one) was silently invisible to convention review. Fixed:
  `ReviewRunner.ReviewFile` now branches on sidecar presence and parses via `ParseBlockWithoutSidecar`
  when absent — the same branch `preflight`/`ProjectIndex` already used. The whole
  `ir/test-project001` corpus now reviews (23 files, 0 unreviewable; OB100 clean).
- The `HasSidecarSection` helper was consolidated into `IrParser` (single source; `ProjectIndex`
  delegates to it) so review and preflight can't diverge — Review can't depend on Preflight (that
  would cycle, since preflight folds in review findings). 1 new test; suite 503/503.

**C-408 mechanized in `converter review` — first of FI-09's rule set**

- `converter review` now checks C-408 (a timer's `ET` compared to produce a boolean trigger) as a
  deterministic rule (`Rules.CheckC408EtComparison`), moved out of `review-conventions`' hand-sweep.
  Scope is the skill recipe's broader, owner-confirmed form: `.ET` inside **any** comparison, not
  only against a literal constant; the permitted value read (`MOVE(IN := timer.ET) => namedVar`)
  carries no comparison and stays clean. Per the skill's self-retiring clause, the hand-sweep
  auto-retires once the tool reports C-408 `checked` — no skill edit.
- New reusable seam: `TagReferences.AllExpressions(network)` yields each network Expr with structure
  intact (the flattening `AllTagPaths` loses), for rules that need expression shape. Future FI-09
  rules build on it. 5 new fixtures; suite 502/502. Corpus regression: C-408 reports clean on every
  reviewable `ir/test-project001` file (zero false positives, SUMMARY totals unchanged). Forward-note
  added to the 2026-07-16 conventions-validation evidence for the extra per-file status line.

**`converter tagstatus` — mechanical exists/proposed tag classifier (FI-24, easiest win from the skill/tooling audit)**

- New subcommand `converter tagstatus <name…> --project <ir-dir> [--json]` classifies each tag name
  `EXISTS`/`PROPOSED` against a project export — mechanizing the anti-laundering step (hard rule 3,
  `docs/15` "Artifacts") that `gen-architecture` did by hand-grep. Pure composition: reuses
  `ProjectIndex` + the `AccessNode.FromDottedPath` root extraction `preflight` already uses, so the
  two never disagree; whole-name-first resolution handles dotted tag-table names (`Clock_0.5Hz`).
  Exit 1 if any name is proposed, so `tagstatus … && <build>` is a usable "all tags exist" gate.
- `src/converter/Converter/TagStatus/` (Model/Runner/Formatter, mirroring Review/Preflight/Digest);
  4 new tests (`TagStatusTests`); suite 497/497. Documented in `src/converter/README.md` and
  CLAUDE.md's command list. FI-24's provenance-header wrapper half stays open.

**`converter review` F-1 fixed: per-rule count now matches the findings it prints**

- The per-rule status line (`C-301: checked, N finding(s)`) over-counted when one check co-emits
  another rule's findings: `CheckC301AbsoluteAddressing` returns both C-301 and C-501 findings, and
  `ReviewRunner.Record` set C-301's count to the whole returned list (C-301 + C-501). Fixed to count
  only the recording rule's own findings (`ReviewRunner.cs`, one line) — a general single-source fix,
  not special-cased. Only C-301 was affected (e.g. `FB_MotorFwdRevSystem` 2 → 1); finding lists and
  every SUMMARY total unchanged.
- Regression guard added (`ReviewRunnerTests.cs`): the C-301/C-501 co-emission case + a general
  invariant (every Checked status's count == its own printed findings). Suite 493/493. Corpus-wide
  check: 88 Checked statuses, zero count-vs-printed mismatches.
- The 2026-07-16 conventions-validation evidence is left verbatim (it records the pre-fix tool); a
  dated forward-note in that doc's §1 explains the single shifted count line so it isn't misread as
  drift.

## 2026-07-17

**Direction reset (owner) + session-close housekeeping: owner-questions gate, S7-first path**

- Owner course-correction recorded (stage-gates has the full entry): gen-architecture is meant to
  carve reuse-first (library-whole -> pattern-composed -> library-modified -> freeform);
  programming splits into three skills (new / modify-for-purpose / modify-for-fix, the modify
  pair being S7's deliverable shape); the library is purposely thin (organic S8 growth); and S7
  is the rush - it takes production load off the owner. Coordinator conceded the
  manual-to-contract drift with evidence.
- docs/notes/owner-questions.md created (priority-ordered A-F: direction rulings, the fix-wave
  defect docket, rule-book rulings, scope decisions, register questions, tooling) - the single
  gate for all next work. AITODO pruned to a lean recovery doc pointing at it.
- Workspace cleaned: six agent worktrees removed, branches deleted (all verified fully merged);
  suites green (converter 491/491, cli 101/101, golden 14/14). Agent memory updated for session
  clearance (stricter-bar, S7-rush/thin-library, drift lesson, environment facts).

**GenProject1 fix wave 1: signed, imported, compiled 0/0, invariance-proven, triple-reviewed**

- All four owner rulings + two mid-wave tension rulings implemented (reverse timed from confirmed
  feedback via new ShredderRunRevFB member; SystemHealthy into StopCmd; stop reaches the pusher
  via the two-tier PusherModeForceOff/CycleInhibit split - jog survives downstream absence;
  RecentStart strap replaced by one-press-one-arm MotorStartArm handshake; spec-true jog/repark
  with warned motion; Fitted gates everything; five commissioning defaults signed as tabled).
- Phase 2: 9 imports in the corrected dependency order (new playbook entry: UDT type-compile
  before dependent iDBs, FBs before iDBs on interface changes - live-proven), every block 0
  errors, device Success 0/0, all nine re-exports readable-identical to the signed drafts.
- Check stage (three blind reviewers vs merged corpus): functional verdict 45 implemented /
  7 partial / 6 unimplemented / 10 disarmed / 0 contradicted (was 28/18/6/11/4); mechanical
  conventions 17 -> 16; new tier-1 candidates and rule rulings queued (fix-wave-1-reviews.md) -
  incl. simultaneous-jog dual solenoids, traced power-cycle auto-resume, a reintroduced C-601
  near-match in our own new code, and a C-117 error-class finding. The reviewers work on the
  pipeline's own output - as designed.

Git doesn't generate this on its own — `git log` gives raw commit history, not a curated record
of what changed and why. This is that record, maintained by hand. Entries are grouped by date,
newest first, one bullet group per commit (or per uncommitted round of work, until committed).

For the detailed story behind any entry — the investigation, the evidence, the live-verification
results — see `docs/notes/stage-gates.md` (stage-gate status) and `docs/notes/openness-quirks.md`
(TIA/Openness findings). This doc is the short index; those are the record.

## 2026-07-16

**Four-stream parallel batch: reviewer suite complete; corpus corrected; plant-level findings**

- Ran as plan agents -> owner approval -> four executor agents in isolated worktrees (zero
  Portal). Delivered: `review-conventions` skill (blind-validated; drift check byte-identical);
  `review-functional` skill + the first pipeline artifacts (gen/GenProject1/: 69-REQ requirements
  register + telemetry rows; two-phase validation - the pre-fix-corpus phase blindly re-caught
  both known bugs); converter TYPE/UDT member-comment support + recursive TypeIr (two silent-loss
  bugs fixed) + sanitizer coverage + SPEC pairing note (491/491); motor-dol pattern C-126/C-201
  fix, landed after its provenance gate exposed that the committed twin was a sanitize product -
  owner-ruled regeneration (05996de) made to-xml canonical.
- Corrections: stage-gates' member-comment "persistence possibly broken" claim withdrawn - TIA
  persisted them; the corpus .ir was stale from the outdated Release binary; both FB .ir files
  regenerated (a11c6c4). CRLF worktree materialization pinned via .gitattributes eol=lf (f737fb9).
- The functional review's plant-level verdict on the "working" build (28/18/11/7/4/1 over 69
  REQs): cannot start as committed (live-zero settings), reverse never runs (6s window inside the
  8s motor pause), E-stop recovery auto-restarts, pusher exempt from stop-all, disabled-pusher
  contradictions. Owner-rulings queue in AITODO. FI-07 parked (owner) with revisit trigger.

**Tier-1 FI adoption: preflight/playbook/digest/telemetry wired into the S6 loop (docs-only)**

- CLAUDE.md workflow step 4: `converter preflight` mandatory before every import (zero-findings
  bar; filter before the compile gate, never a substitute - hard rule 4); compile-error playbook
  is the first lookup on any compile failure; every stage run appends a telemetry line.
- docs/15: new "Inner-loop tooling" section; digest-vs-full-IR policy in the isolation model
  (reviewers always full IR); telemetry.log added to gen/<project>/ artifacts.
- docs/16: FI-13/14/15/16 status lines gain adoption pointers. CLAUDE.md command block now lists
  review/digest/preflight (review had never been listed despite existing since S4).
- Release converter rebuilt post-merge (pre-merge binary lacked the new verbs); both commands
  verified live against the corpus before documenting (preflight pass + findings cases, digest).

**Fix InCycle lamp + DI4 stop polarity (owner-ruled tier-1 bugs from the blind review)**

- FC_ControlMain N7: DB_Output.In_Cycle = OR of the five field running feedbacks (owner
  definition: lamp on whenever any equipment is running; feedbacks not commands) - also gives
  Infeed_Conv_Running its consumer. iDB_ShredderSequencer.IO.InCycle superseded/unwired; member
  removal deferred to the settings-rework request.
- FC_Inputs N1: DI4_SYS_CycleStop negated at the map layer per the input-mapping pattern's
  negated variant; buffer stays active-high; network comment reconciles the "(NC)" tag doc.
- Discipline: S6 sandbox iteration with S7-style rigor rehearsed - block + device compiles clean
  (0 errors; device 0 warnings), untouched-network invariance proven by pre/post readable-IR
  diff (exactly 2 hunks per block), corpus updated in place. Sidecar UIds regenerated by
  synthesize+TIA account for the larger git diff; the semantic diff is the 4 hunks.

**FI-13 implemented: `converter preflight` — static checks before any Portal round trip**

- New `Preflight/` (model + project index + runner + formatter): parse/convert checks (real
  writers — `FlgNetBuilder`/`SidecarSynthesizer`), unresolved-tag-root resolution against locals +
  the project export dir + the batch itself, CALL-callee and `INSTANCEOF` resolution, and
  `converter review` findings folded in (`review:C-xxx`). Explicitly labeled not-the-compile-gate
  in every text report (hard rule 4). 9 new tests (477 green; the 12 pre-existing CRLF
  `PatternExampleTests` failures unchanged).
- Piloted with zero false positives: all 21 GenProject1 files resolve clean against their own
  export (17 genuine review findings only); reference-corpus blocks reproduce the S4 pilot's known
  findings exactly. A fixture attempt with placeholder sidecars was correctly rejected by the
  convert check — the check catching its own test's invalid content before TIA would have.

**FI-15 implemented: `converter digest` — compact structural summaries of .ir content**

- New `Digest/` (model + builder + formatter, mirroring `Review/`'s shape and batch contract):
  blocks report kind/name/number/title, interface sections, CALL sites grouped by callee with
  instance paths, per-network statement counts, and deduplicated tag roots (via
  `AccessNode.FromDottedPath`, so literal-dot names like `Clock_0.5Hz` stay atomic); DBs/UDTs/tag
  tables get member/tag listings. Works on sidecar'd and sidecar-less IR alike. 8 new tests;
  piloted clean against all 14 `ir/reference/*.ir` plus pattern-example and `GenProject1` content.
- Known pre-existing issue surfaced while verifying (not caused by, not fixed here):
  `PatternExampleTests` (12 tests) fail in any *fresh* checkout because the committed
  pattern-example `.ir` files get CRLF-normalized by git on checkout while the serializer emits
  LF — the raw-text comparison then fails on line endings alone. Passes in the original working
  copy (files still LF on disk there). Flagged for a deliberate fix (`.gitattributes` `*.ir eol`
  policy or normalized comparison) rather than patched unilaterally in a side branch.

**FI-16 (convention) implemented: `docs/notes/gen-telemetry.md`**

- Proposed per-run telemetry format for generation projects: one append-only
  `gen/<project>/telemetry.log`, one pipe-separated line per skill/stage run (date, skill,
  wall-clock, Portal round trips, tokens, outcome, note). Explicitly a proposal for the pipeline
  work stream to adopt — instrumentation lands with pipeline runs, not here. Columns chosen to
  answer the FI-12/FI-13/FI-15 sizing questions; discipline section keeps it a log, not a dashboard.

**FI-14 implemented: `docs/notes/compile-error-playbook.md`**

- 19 grounded error→fix entries curated from `openness-quirks.md`, `stage-gates.md`, `CHANGELOG.md`,
  and CLAUDE.md's environment notes, keyed by verbatim message fragment (or symptom), grouped by
  cycle stage (compile / import / connect-open-export). Every entry cites its dated source; header
  states the discipline — entries are hypotheses to verify in context, and only proven fixes get
  recorded. No existing file modified; curation only.

**Future-ideas doc: renumber 15 → 16 (master claimed 15 for the generation pipeline); add FI-06…FI-16**

- Renumbered to `docs/16-future-ideas.md` after `docs/15-generation-pipeline.md` (ADR-0004) landed
  on master; index row and `10-non-goals.md` cross-reference updated to match.
- Eleven new entries, all awaiting owner review. From conversation suggestions: FI-06 constructed
  edge-detection as a fourth pattern kind, FI-07 `openness-cli cleanup` Portal-instance janitor,
  FI-08 engineer-side approval path for proposed tags, FI-09 convention rules Phase 2, FI-10
  HMI-importable alarm exports from S5, FI-11 presentation-bundling tool (largely absorbed by
  pipeline skill 13 — recorded with that noted). On generation/token-usage/response-speed: FI-12
  persistent Portal session or batch mode for the import–compile inner loop, FI-13 static
  pre-flight gate before Portal round trips, FI-14 compile-error→fix playbook, FI-15 generated
  block digests for cheap structural context, FI-16 per-stage token/latency telemetry. Entries
  written against ADR-0004's pipeline so they complement rather than duplicate it.
- FI-17 added after discussion: explanation sidecars — cached AI block explanations keyed to the
  IR hash they were derived from, orientation use only, never review input (the analysis-cache
  counterpart to FI-15's mechanical digests).

**Add `docs/16-future-ideas.md` — a durable home for candidate ideas under debate**

- New doc fills the gap between `AITODO.md` (in-flight, ephemeral), `02-roadmap.md` (committed),
  and `10-non-goals.md` (excluded): ideas get an `FI-xx` ID, a merits/costs analysis, and a
  lifecycle (Raised → Under debate → Accepted | Rejected | Parked). Promotion into the plan is
  ADR-gated, consistent with `10-non-goals.md`'s existing "revisit only via ADR" rule; Parked
  entries must carry an explicit revisit trigger; ideas are never silently deleted.
- Seeded with five entries already parked around the repo (sources linked, nothing moved or
  deleted from `AITODO.md`): FI-01 pattern testing hook (S9), FI-02 `Main`/OB1 round-trip,
  FI-03 Modbus multi-instance form, FI-04 `WAIT`/`Jump` (Rejected — not needed, owner's call
  2026-07-14), FI-05 `chained-permissive-enable` blind-draft gap (Under debate, nearest-term).
- Indexed in `docs/00-README.md`; one cross-reference sentence added to `docs/10-non-goals.md`
  defining the boundary between the two docs.

**review-simplicity skill built + blind-validated; UDT member-comment gap grounded**

- `.claude/skills/review-simplicity/SKILL.md` - the docs/15 tier-2 reviewer (one-reading walk,
  C-601-C-607/C-203/C-126 sweep recipes, calibration list, stricter-bar disposition). Validated
  the way docs/15 defines reviewers to run: fresh-context subagent, skill + corpus only, barred
  from the retrospective. Every material known finding independently reproduced; verdict +
  verbatim report in docs/notes/review-simplicity-validation-2026-07-16.md.
- The blind run also surfaced 10 new findings, incl. two tier-1 candidates on the "working"
  build (IO.InCycle consumed into DQ8 but written nowhere; DI4_SYS_CycleStop "(NC)" comment vs
  non-negated mapping) and a pattern-vs-rule tension (motor-dol's own example lacks the scheme
  comment C-126's new exception requires). Owner rulings queued in AITODO.
- Grounded: TYPE/UDT members cannot carry comments through the converter at all
  (UnsupportedConstructException) - C-605 (error) unsatisfiable on interface UDTs until built;
  and fresh exports contain zero member comments anywhere, so member-comment persistence through
  TIA is unproven. Both queued as converter work. Release converter rebuilt (review verb present).
- Owner's settings sub-struct idea: structure-only leg of the nested-Struct-in-UDT round-trip
  proof passed IR->XML; live TIA leg in flight at commit time.

**Retrospective owner pass folded: C-601-C-607 + C-203 live in doc 06; settings policy resolved**

- All 7 candidate simplicity rules accepted (C-605 bumped to error by owner; C-602 with a
  within-network fan-out exception; C-606/C-607 justification directed to comments) - doc 06
  gains a "Simplicity & readability" section, plus new C-203 (titles short / comments detailed)
  and the stricter-bar principle in the preamble (generated LAD must survive a skeptic's single
  reading; site quirks are never a defense).
- Owner's faceplate context resolved the settings adjudication against the draft: per-instance
  settings live in the instance UDT (C-307 sharpened), one writer - the HMI (C-308 extended:
  logic never writes, orchestrating FCs never scan-copy; names GenProject1's MOVE-over-UDT trap).
  C-001 members -> PascalCase; C-109 IO-mapping direct-call exception; C-126 HMI-Times batch
  exception with mandatory pairing comment; C-122/C-304 aligned.
- Queued: Struct-in-UDT round-trip proof; converter HeaderAuthor/Version/Family capture;
  SPEC.md pairing note; GenProject1 settings rework + buffer renames (future S6 requests);
  OB100/DB_PLC when the demo-panel waiver lifts. Retrospective SS10 records the full resolution.

**Adopt staged S6 generation pipeline (ADR-0004); GenProject1 corpus + simplicity retrospective**

- Owner verdict on the Kestrel build: functionally right, but overly complex/obtuse; stated
  priority order for generated LAD - function -> readability & simplicity -> efficiency. Adopted
  response: `docs/15-generation-pipeline.md` + ADR-0004 - a 13-skill staged pipeline (analyse/
  design/build/check/entry) with committed artifact handoffs, fresh-context read-only reviewers,
  a tag-status (`exists`/`proposed`) anti-invention rule, two hard engineer gates, and a
  scale-down rule. CLAUDE.md's 5-step workflow wrapped as the inner loop; doc 06 preamble carries
  the priority order. ADR-0003 left reserved for the data-boundary decision per docs/13.
- GenProject1 exported to a committed corpus: `.gitignore` anchored to `/GenProject1/`; all 17
  blocks + 3 UDTs + tag table in `ir/GenProject1/` + `simatic-ml/GenProject1/` (21/21 clean
  `to-ir`, incl. OB1). Known `IsConsistent` export refusals cleared by block-level compile
  (0 errors). Data-boundary scan vs the Kestrel Shredder Systems map: zero real-name hits.
- `docs/notes/test-project001-retrospective.md` (delivered as `genproject1-retrospective.md`, since renamed) - mechanical baseline (17 findings; both
  generated FBs clean, every in-block finding on the real `FB_MotorFwdRevSystem`), 12 readability
  findings, 7 candidate C-6xx simplicity rules, 6 rule-vs-practice adjudications. **Owner
  accept/reject pass pending - rules enter doc 06 only after it.** Converter suite 463/463.

## 2026-07-15

**Open S6 (generation from plain language) - entry criteria met, two seed patterns admitted**

- Built and verified a `template.ir` + `<SlotName>` reserved-sentinel mechanism for pattern
  parameter slots (collision-free against the IR grammar, confirmed by direct code reading;
  synthetic example round-tripped and compiled clean) - then abandoned it. It duplicated
  parameterization/type-checking this project's IR model and TIA's own compiler already provide via
  ordinary FB/FC interfaces and `CALL`, and never reconciled with `06-lad-conventions.md` C-106
  (FB+UDT for repeated equipment, already the site's own convention). Reverted cleanly: the
  slot-syntax sections in `docs/07-pattern-library-spec.md`, the pointer added to `ir/SPEC.md`
  (kept as a short historical note, not deleted), `docs/13-data-boundary.md`'s mechanism wording.
- Redesigned around two pattern kinds instead, no new IR mechanism: equipment-instance (a whole
  proven FB/FC reused via `CALL`) and repeated rung-shape (the same logical shape repeated within
  one sequencing FC, documented from a real example, not templated). Explicitly not a closed
  taxonomy - constructed edge-detection is a real third kind, not built this round.
- `patterns/motor-dol/` - ADMITTED, all 5 criteria met. `MotorDOL`/`MotorStarter`, 8 real instances.
  Along the way, found `SampleProject` already had this content plus `PlantAutoControl` sitting from
  earlier S1 work, all `IsConsistent = false` despite a clean device compile - the known
  device-vs-block-level-compile quirk (`docs/notes/openness-quirks.md`). Cleared by compiling 21
  blocks individually in dependency order; whole project now compiles clean, 0 errors, 0 warnings.
- `patterns/chained-permissive-enable/` - ADMITTED, criterion 3 (a newly-drafted instance) accepted
  on partial evidence rather than fully met. Drafted for a genuine chain-head case
  (`MotorStarterInst5`, "Drum Separator"), explicitly labeled as composed with prior knowledge of
  the real answer, not blind - a genuinely blind attempt on JOB9002's separate, untouched station_1
  `PlantAutoControl` hit a partial accidental read and a structurally-different (VSD, edge-detected
  fault-reset) case this pattern doesn't document yet. Project owner's own call: accept and move on,
  recorded as an outstanding gap rather than silently closed.
- `patterns/_templates/` added (two `pattern.md` templates, one per kind) so the next pattern has a
  real starting point. `Converter.Tests/PatternExampleTests.cs` gives `patterns/` the same standing
  round-trip regression guarantee `ir/reference/` already has.
- Full history: `docs/notes/stage-gates.md`'s "S6 unlock" section.

**S4 gate reviewed and signed off**

- Validated Phase 1 against real, previously-untouched JOB9002 content (`FC StatusAlarms`, station_1)
  on top of the reference-corpus pilot - same finding pattern held (naming, header comment, network
  titles, packed alarm-word bits). Signed off on a "shown, then confirmed" match rather than a
  genuinely blind one - flagged explicitly before revealing the tool's output and again before
  closing the gate; project owner's own informed call to accept it. Full detail:
  `docs/notes/stage-gates.md`.

**Open S5 (structured data extraction), in parallel with S4**

- Roadmap explicitly allows this: S5's entry is just S1 done (S4 not required). S4 stays ACTIVE at
  Phase 1 - opening S5 isn't a replacement for finishing it, just parallel work. No S5 work started
  yet - detailed plan next, same process as S3/S4.

**S4 Phase 1: mechanical rule-checking built and pilot-proven**

- `converter review <file> [<file> ...] [--ignore-errors] [--json]` - 8 rules: C-003 (naming
  prefix), C-005 (charset), C-201 (title/comment presence), C-301+C-501 (absolute addressing, all
  three documented exceptions), C-406 (timer kind, both declaration and usage form), plus
  C-102/C-401/C-404 built and explicitly labeled `CheckedVacuous` (no jump/counter/built-in-edge
  construct exists in the current IR model at all, so these can't structurally fire).
  `--ignore-errors` records a per-file error and continues a batch instead of aborting on the first
  bad file.
- 46 new tests (true-positive/true-negative per rule); full converter suite green (412/412).
- Live pilot against all 14 `ir/reference/*.ir` files matched every finding predicted during
  planning exactly (13/14 naming-prefix violations, 5 DB-kind header-comment gaps plus
  `TimerSample`'s own, the `NodeStatusAlarms`/`PerimeterSafetyAlarms` alarm-word C-301/C-501 pair,
  the `FBTimers`/`TimingAndCalls` C-406 declaration/usage split). Confirmed `DataHandling.ir`'s
  C-301 exception mechanism against its real content (3 genuine slice-access writes, correctly
  exempted), not just the synthetic unit test.
- Exit criterion (`02-roadmap.md`: matches the engineer's own review, false-positive rate
  acceptable) explicitly not yet met - needs a genuine blind comparison pass, not done this round.
  S4 stays ACTIVE, Phase 1 only. Full detail and the durable checkability findings (severity/
  checkability mismatches, C-301's three-exception scope, C-406's dual-form split, a rough
  re-estimate of the remaining ~42 rules): `docs/notes/stage-gates.md`'s "S4 Phase 1" section.

## 2026-07-14

**Open S4 (convention review)**

- Entry blocker already cleared earlier in the project (`06-lad-conventions.md` populated). No
  work started yet - detailed plan being built the same way S3's was (plan mode, research
  agents, explicit sign-off on real open questions before writing code).

**S3 gate reviewed and signed off**

- Exit criterion (an undocumented block gets useful comments end-to-end, human-approved) met three
  times over: `TimerSample` and `PerimeterSafetyAlarms` against the Green-tier reference corpus,
  `PlantAutoControl` against real JOB9002 content at full 20-network scale. Swept all 13 other committed
  reference-corpus `.ir` files to confirm the stale-sidecar bug found in `TimerSample` wasn't a
  wider gap - it wasn't, all clean. S4 not opened yet (separate decision); its own entry blocker
  was already cleared earlier in the project. Full history: `docs/notes/stage-gates.md`.

**S3 third proof: real JOB9002 content, block + all 20 networks (`PlantAutoControl`)**

- Extended the JOB9002 data-boundary approval to cover S3 write activity first, recorded as its own
  dated entry, before touching anything - the recorded scope covered S1/S2 but never write/comment
  generation.
- First round: `PlantAutoControl` block-level Title + Comment only (every network already had a real
  title from the original engineer, so the block itself was the genuine gap). Compiled clean, 0
  errors/warnings, on a fully-configured real device.
- Asked to redo network-level titles and comments too, including replacing the existing titles -
  clarified the exact scope (keep-vs-replace, comprehensive-vs-selective) before starting, given
  the stakes of rewriting an original engineer's own real production documentation at 20x the
  scale of anything done before. Found and fixed two genuine spelling errors along the way.
- Verification scaled the same Comment-aware structural check (strip both trees, blank Comment
  text, confirm byte-identical) across the full 20-network redo - the only differences anywhere
  are the intended new Title/Comment text. Compiled clean both rounds. Reviewed and approved
  before committing. Full story: `docs/notes/stage-gates.md`. Real content (tag paths, exact
  comment wording) stays in conversation and inside JOB9002's own gitignored project, per the
  genericization rule.

**S3 second proof: title + comment, both block and network level (`PerimeterSafetyAlarms`)**

- Explicit instruction this round, directly responding to the gap caught in the first proof:
  short title + longer why-comment, at both block and network level this time. Picked
  `PerimeterSafetyAlarms` over `NodeStatusAlarms` for richer logic (an `OR`-merge of 3 negated
  contacts) — genuine "why" to write about, not just "what."
- Comment content grounded directly against the real rungs: bits 0-4 negate because their source
  tags read true-when-intact (a break reads false, needs `NOT` to alarm); bits 5-7 don't negate,
  the opposite field-device convention (already true-when-triggered); bit 0 is a separate summary
  coil for one HMI indicator rather than decoding three bits individually.
- Verification needed a different approach than the first proof: `Normalizer.IsVolatile` treats
  Title as ignorable but deliberately *not* Comment, so a plain equivalence check would correctly
  report a difference here. Instead stripped both documents, additionally blanked just the Comment
  text in both, and confirmed the results are then byte-identical — proving the only difference
  anywhere is the intended new text, nothing structural. Full live cycle (import → clear
  `IsConsistent` → compile clean → re-export → confirm content landed → full 14-block `RunAll`)
  otherwise matches the first proof. Reviewed and approved before committing.

**S3 first proof: write a real title through the IR layer, end to end (`TimerSample`)**

- Two small converter fixes first: `IrSerializer`/`IrParser` now reject embedded `\n`/`\r` in
  Title/Comment text with a clear error instead of risking a corrupted `.ir` file; 4 new tests in
  `NetworkTitleCommentTests.cs` cover the previously-untested "edit an existing title" scenario at
  both network and block level. Also fixed a stale comment in `Normalizer.IsVolatile` (the
  "Title is always empty" justification predated S1 items 16/17, which disproved it — the
  skip-Title behavior itself was already correct, just documented for the wrong reason).
- Live proof against `ir/reference/TimerSample.ir` (zero real-site lineage, lowest-risk
  target): hit a real pre-existing bug immediately — the committed file's sidecar predates a later
  mandatory-type-suffix format change, same class of staleness already fixed for
  `NodeStatusAlarms`/`PerimeterSafetyAlarms` earlier this session. Fixed by regenerating from a
  fresh live export (confirmed zero semantic drift first) before applying the title edit.
- Full cycle proven twice: once for network-level titles, then again after the project owner
  caught that the block itself (`FC TimerSample`) still had no title — block-level Title uses the
  same write path but was missed on the first pass. Both rounds: edit IR → `to-xml` → `import`
  (`Override`) → clear the known `IsConsistent` refusal via `compile` → re-export → confirm the new
  text is genuinely present → `Normalizer.AreSemanticallyEquivalent` confirms logic-only change →
  full 14-block `RunAll` passes. Committed corpus pair reflects the block-titled final version.
- S3's exit criterion (an undocumented block gets useful comments end-to-end, human-approved) met.
  Full story: `docs/notes/stage-gates.md`.

**S2 gate reviewed and signed off; move to S3 (comment generation)**

- Exit criterion (10 sampled networks judged accurate, no hallucinated tags/behavior) cleared
  several times over: 4 real JOB9002 blocks explained in conversation (`PerimeterSafetyAlarms`, `MotorDOL`,
  `PlantAutoControl`, `MotorFwdRevSystem`, 51+ networks combined), all confirmed accurate by the project
  owner. `WAIT`/`Jump` — carried forward from S1's own sign-off as open questions needing input —
  explicitly closed as not needed, project owner's own call; Modbus's separate live-compile gap is
  unaffected and stays open. S3's own entry criterion (`docs/11-review-workflow.md` agreed, not
  just drafted) also cleared — summarized for the project owner and agreed as-is, unchanged.
  `docs/notes/stage-gates.md` and `AITODO.md` updated; `AITODO.md` wiped of S2-era in-flight detail
  per the same pattern as the S1→S2 transition.

**Build S2's explanation-quality checklist from direct multi-agent comparison, not invented solo**

- Produced three full real-JOB9002-block explanations in conversation (`PerimeterSafetyAlarms`, `MotorDOL`,
  `PlantAutoControl`), then re-explained `PlantAutoControl` with five subagents at varying context levels to
  isolate what actually drives explanation quality vs. what just costs tokens. Cost didn't track
  context linearly; the most reproducible quality lever was an explicit "check every instance
  exhaustively, don't sample" instruction, which caught real cross-instance bugs sampling-based
  descriptions missed. Checklist committed as `docs/14-s2-explanation-checklist.md` (8 items,
  `E-01`…`E-08`), indexed in `00-README.md`. A `.claude/skills/explain-plc-block/` skill file was
  also written, but confirmed *not* auto-discoverable by a fresh subagent's Skill tool in this
  harness — works today only as a plain reference doc, not a real invocable skill. Full
  methodology in `docs/notes/stage-gates.md`.

**Fix `Normalizer`'s Part-UId volatility gap with real graph-based identity matching**

- A bare `Part` (`Contact`/`Coil`/`TON`/etc.) has no distinguishing content of its own the way
  `Access` does via its own `Symbol` path — two `Contact`s in the same network can be
  byte-identical XML except for `UId`, so the existing content-key-map approach couldn't
  disambiguate them. Built `BuildPartContentKeyMap`: iterative structural refinement
  (Weisfeiler-Leman-style color refinement) using each Part's own content plus its wired
  neighbors' current signatures (resolved through stable `Access` content-keys, `Powerrail`,
  `OpenCon`, or other Parts) until the whole set stabilizes. Found and fixed a real bug live: naive
  round-to-round string concatenation grows multiplicatively and overflowed `Int32` on
  `MotorStarter` — fixed with a SHA256 hash per round, the way color refinement is meant to work.
  3 new tests (including the real proof: two same-kind Parts with numbering *swapped* between
  documents, showing identity comes from topology, not document order). Live-verified against all
  7 real blocks originally confirmed to hit this gap — all now compare equal; the full 14-block
  reference corpus still round-trips cleanly. 14 golden-harness tests total.

**Fix Sanitizer's `ExternalAccessible=False` hard-error; backfill data-boundary approval record**

- `DbMember` gained `ExternalAccessible`/`ExternalVisible`/`ExternalWritable` fields (default
  true), captured and regenerated verbatim like `SetPoint` rather than hard-refused when false —
  confirmed real (`FB VSDSim`'s own Static member). New IR-text markers
  (`EXTERNALACCESSIBLE=FALSE` etc.), shown only when false. Live verification found a real
  constraint: `ExternalAccessible=false` requires the other two false as well (TIA's own
  `Import()` rejects `ExternalVisible=true` alongside it); `ExternalWritable=false` alone is
  independently valid. 7 new/updated tests, 366 converter tests total.
- Backfilled `docs/13-data-boundary.md`'s JOB9002 approval-scope record, which had drifted behind
  the actual scope of Amber-tier work done since (already separately confirmed with the project
  owner at the time, just never recorded in this doc).

**Fix `RunAll`'s `IsConsistent` cascade; wire `TimerSample`/`DB_Timers` into it for the first time**

- Growing the reference corpus surfaced a real gap: re-importing any block re-flags every block
  that references it as inconsistent again, regardless of dependency order — a naive per-block
  batch run corrupts earlier blocks' baseline exports partway through. Fixed with a proper
  three-phase `RunAllSettled` (capture every baseline export first, then import everything, then
  compile+re-export each one last). `TimerSample`/`DB_Timers` (committed since 2026-07-11) were
  also never wired into `RunAll`'s own block list — added now. All 14 reference-project blocks
  verified together in one pass for the first time.

**Reference corpus: add `FBTimers`/`ScaleValue`/`TimingAndCalls` (TONR/TOF/CALL)**

- `FBTimers` (DB30): hand-numbered standalone-timer DB generalizing the `DB_Timers` precedent to
  `TONR_TIME`/`TOF_TIME`, avoiding the confirmed-broken `create-instance-db`/`InstanceDB` path.
  `ScaleValue`: new, self-authored callee FC. `TimingAndCalls`: TONR/TOF + CALL. Two real findings:
  `TONR_TIME`'s DB-member shape has no `R` field despite `R` being a real wired port; a bare
  `T#5S`-style time literal is rejected on a TONR/TOF `PT` port (reverted to the already-grounded
  tag-fed `PT`). The real Siemens "Scale" FC referenced in `ir/SPEC.md` turned out to be real
  restricted content from `JOB9002`, not a library instruction — hence the new callee. All live-verified.

**Reference corpus: add `BooleanExtras` (standalone Not, SCoil/RCoil)**

- First-ever live-TIA verification of standalone `Not` — `ir/SPEC.md` had flagged it as unverified
  (every real instance pairs it with an unbuilt `CALL`). Used a plain linear chain rather than the
  unit fixture's own shared-wire-tap shape, sidestepping the same non-rail fan-out ordering risk
  found for `Move`. Clean compile, 0 errors.

**Reference corpus: add `SignalConditioning` + `DataHandling` (arithmetic/box family)**

- Mul/Convert (ENO-chained), Sub/Div (Lt-gated), Abs, Swap, WAND, Calc, T_SUB/T_CONV,
  MOVE_BLK_VARIANT, Move. The richer "telescoping" Move shape (two taps sharing chain positions)
  was tried and rejected by live TIA import even after `FlgNetBuilder`'s own Part-UId-sort fix —
  left as a real, separately-flagged open question; used the simpler, safe Contact-tap shape
  instead. All live-verified.

**Reference corpus: add `ThresholdAlarms` (Eq/Ge/Lt/Ne/Gt/Le); fix two stale sidecar-format `.ir` files**

- New FC exercising the full IEC comparison family — none were previously in the committed
  golden-harness corpus despite long-standing converter support. Found and fixed along the way:
  `NodeStatusAlarms.ir`/`PerimeterSafetyAlarms.ir` predated a sidecar-format change and could no
  longer be parsed by the current `IrParser` — no semantic drift (confirmed via `Normalizer`),
  just stale encoding, regenerated from a fresh export. Also fixed `RoundTripRunner.RunFull`'s own
  compile-stage check, which treated a benign hardware-config warning as a failure.

**Phase 2 Tier 4: `Modbus_Master`/`Modbus_Comm_Load` built and tested; live compile blocked by a confirmed general Openness limitation**

- Built across all 7 converter files: `TraceChain` reused as-is for `Modbus_Master`'s chain-fed
  `REQ` port; `ResolveOptionalOutputPort` generalized to `ResolveOptionalOpenPort` for
  `Modbus_Comm_Load`'s three deliberately-unconnected ports (`FLOW_CTRL`/`RTS_ON_DLY`/
  `RTS_OFF_DLY`); multi-output-tag productions extended to 4 and 3 outputs respectively. 15 new
  tests, 359 converter tests total, all green.
- Live verification: import clean first try; compile narrowed 6 errors -> 2 across two fixture
  fixes (verification-only `GateBit`/`TriggerBit` scope, matching the established pattern) and one
  genuinely new real finding — `Modbus_Comm_Load`'s `PORT` parameter needs the actual Siemens
  system datatype `PORT`, not a generic integer.
- The remaining 2 errors trace to a confirmed general Openness limitation, not a converter bug:
  standalone system-FB instance DBs (`Modbus_Master_DB`/`MB_Master_Comm`) are invisible to
  `SW.Blocks` entirely — `create-instance-db` deterministically assigns an invalid `DB0`, and
  neither DB is exportable from `JOB9002` by name. Same class of finding as `CycleDelayReset`
  (Phase 1 of the `PlantAutoControl` plan) — now confirmed to generalize beyond one instruction.
  Documented as a standing limitation in `docs/notes/openness-quirks.md`.
- Left honestly unverified for full live compile, same standard as `WAIT`. Scratch state (broken
  instance DBs, synthetic test block, temp harness) fully cleaned up. See
  `docs/notes/stage-gates.md`/`AITODO.md` for full detail.

**Phase 2 tiers 5/6 live verification: `MOVE_BLK_VARIANT`/`FillBlockI` closed, `WAIT` hit a distinct new blocker**

- TIA Portal recovered on its own overnight (confirmed via `sanity-check`) — the earlier outage was
  transient. `MOVE_BLK_VARIANT` and `FillBlockI` both now fully live-verified: a synthetic composed
  FC imported and compiled clean (0 errors) in `SampleProject`, byte-identical round-trip.
- Found one more real fixture gap along the way, same "only live TIA catches it" pattern as the
  whole night before: `FillBlockI`'s real `out` destination (`CommsProcessData.NodeFaultCount[3]`)
  is array-indexed, not a plain scalar — the verification fixture had used a plain scalar, which
  TIA's compiler correctly rejected. No converter code changed (general array-index support already
  existed); only `FillBlockIFedByRail.xml` was corrected.
- `WAIT` hit a genuinely different, still-open blocker: TIA's `Import()` says "An instruction with
  the name 'WAIT' cannot be found" in `SampleProject` specifically, even though the shape faithfully
  matches the real `JOB9002` export — confirmed not a converter bug (`MOVE_BLK_VARIANT`/`FillBlockI`
  imported cleanly into the same project). Likely a missing library/technology-object dependency —
  flagged for the project owner rather than guessed at further.
- 345 converter tests, all green. See `docs/notes/stage-gates.md`/`AITODO.md` for full detail.

**Phase 2 Tier 6: `WAIT`/`FillBlockI` built and unit-tested — `Jump` deliberately not (needs a real design decision)**

- `WAIT` (`en`/`WT`, no destination at all — the first production modeled with no output) and
  `FillBlockI` (Move-shaped plus a `count` input) both re-grounded precisely and built, 13 new
  tests, 344 converter tests total.
- **Not live-verified**: by the time these were ready, TIA Portal had stopped responding to *any*
  command at all (confirmed via the lightest possible one, `sanity-check`) — a genuine outage, not
  specific to these instructions. Left unverified rather than retried blindly.
- **`Jump` investigated, deliberately not built.** Its `label` port references a new `Access
  Scope="Label"` node, and the actual jump target is a network-level `<Labels><LabelDeclaration>`
  element confirmed real in a *different* network than the `Jump` Part itself — genuine
  cross-network control flow, which no production this converter models has ever needed to
  represent. A real IR-format design question, flagged for the project owner rather than decided
  unilaterally. See `ir/SPEC.md`'s own `Jump` entry for the full grounding.

**Phase 2 Tier 5: `MOVE_BLK_VARIANT` built and unit-tested — live TIA verification still pending**

- Re-grounded properly (per the Tier 1–3 lesson above): the earlier "only `en` ever wired" note
  was a `head_limit`-truncated read, not the real shape. All 4 real instances (`FC MoveData`/
  `FC VSDDataSequence`) are fully wired and simple — four plain-tag inputs, no chains, no
  `OpenCon`-optional ports, unlike Tier 4. Built the same way as tiers 1–3: the first instruction
  this converter reduces with **two** destination writes (`Ret_Val`/`DEST`) instead of one. 8 new
  tests (`MoveBlkVariantTests.cs`), 331 converter tests total, all green.
- **Not live-verified**: two consecutive `openness-cli import` attempts against a synthetic
  composed FC hit the first-connect Portal timeout, with no one awake to check for the approval
  dialog. Explicitly not presented as closed/live-verified — next session should run the
  import/compile/re-export cycle before treating this tier as done.
- See `docs/notes/stage-gates.md`/`ir/SPEC.md`/`AITODO.md` for full detail.

**Instruction-coverage sweep of the full `JOB9002` inventory + Phase 2 tiers 1–3: `Abs`/`LIMIT`/`T_SUB`/`T_CONV`/`Calc` built and live-verified**

- Grounded all 36 remaining `JOB9002` blocks (both PLC stations) against the full instruction
  vocabulary, one export per block; found 11 real, currently-unsupported instructions
  (`MOVE_BLK_VARIANT`, `LIMIT`, `Calc`, `T_SUB`, `T_CONV`, `WAIT`, `Modbus_Master`,
  `Modbus_Comm_Load`, `Jump`, `FillBlockI`, `Abs`); ranked into a 6-tier plan by confidence/effort.
- Built and live-verified tiers 1–3 (5 instructions): `Abs` (identical shape to `Swap`), `LIMIT`
  (a new 3-fixed-input arity, `MN`/`IN`/`MX`), `T_SUB`/`T_CONV` (time-arithmetic `Sub`/`Convert`
  variants, extending the ENO-chain allowlist), `Calc` (Cardinality-driven inputs plus a verbatim
  free-text `Equation` string). `PartNode.TonVersion` generalized to `Version` (no longer
  TON-specific). 40 new converter tests, 323 total.
- Two real bugs found only by live TIA import — neither caught by unit tests, since the hand-built
  fixtures were self-consistently wrong along with the code: `LIMIT`'s `DisabledENO="true"` was
  misread from the raw export as absent; a fixture reused one `Access` UId across two wires, a
  shape TIA's own export never produces (confirmed real: it always declares a fresh Access UId per
  wire reference, even for repeat reads of the same tag).
- Live-verified via a synthetic composed FC (not the real `VSDSim`/`VibratorCycle` blocks
  themselves — `VSDSim` also hit an unrelated, pre-existing Sanitizer gap, `ExternalAccessible=
  False`, flagged for later, not fixed) — imported/compiled clean (0 errors) in `SampleProject`,
  byte-identical readable IR before/after the full cycle, then deleted (synthetic scaffolding, not
  reference content).
- See `docs/notes/stage-gates.md` for the full story, `ir/SPEC.md` for the grammar, `AITODO.md`
  for tiers 4–6 (not yet started).

**Full-cycle verification pass: every block in `SampleProject` — 5 more real converter bugs found and fixed; 47 of 48 blocks now round-trip completely**

- Project owner's own explicit ask: run the complete export → `to-ir` → `to-xml` → import →
  compile → re-export cycle against all 49 blocks already sitting in `SampleProject` (the full
  accumulated project history, not just the `PlantAutoControl` dependency set), one at a time. First
  time the converter's own `to-ir`/`to-xml` round trip — not just `sanitize`, which bypasses the IR
  text layer — was exercised this broadly against real content in one pass.
- Deleted one broken, unrepairable scaffold artifact (`MotorStarter_Instance`, stuck at an invalid
  DB number `0` from the earliest `create-instance-db` spike, superseded by the real
  `MotorStarterInst1`-`9` DBs) — project owner's explicit confirmation.
- **5 real bugs found and fixed**: (1) `Sanitizer.SanitizeAccessNode` and (2)
  `AccessNode.FromDottedPath` both corrupted a real tag whose own name contains a literal `.`
  (`Clock_0.5Hz`, a genuine Siemens system clock tag) via naive `Split('.')`. (3) `FlgNetBuilder`
  grouped Parts by production kind instead of true document order whenever multiple chains
  interleave (`Mul`/`Convert` pairs; independent Coil-assignment chains) — fixed generally by
  sorting the final `parts` list by `UId` rather than special-casing each pair of kinds. (4) The
  same general fix extended to each wire's own endpoint list. (5) `IsBareParameter` (`FC Scale`/
  `AnalogScale`) was never carried by the IR text format, silently reverting on the `to-ir`/`to-xml`
  round trip.
- Also added (not fully completing) OB support: `SecondaryType` and `Informative`/
  `InformativeComment`, needed for `OB1 Main` — deliberately not pursued further after a third
  OB-specific quirk surfaced (`Main` is TIA's own template block, not restricted content; OB support
  was never a project goal).
- Confirmed a new TIA behavior exposing a real gap in `tests/golden/Normalizer` itself, not the
  converter: Part UId (not just Wire/Access) can also be reassigned by TIA on import/compile —
  flagged as a well-scoped follow-on (needs graph-based Part identity matching), not fixed under
  time pressure.
- **47 of 48 `SampleProject` blocks now round-trip completely**, including `PlantAutoControl`
  (`PlantAutoControl` itself) with a byte-level `Normalizer` equivalence pass. 291 converter tests (up
  from 283), 101 openness-cli, 11 golden-harness — all green. Commit `60ac96f`. Full story:
  `docs/notes/stage-gates.md`.

**Instance DB Input/Output support; `PlantAutoControl` proves the full TIA round trip — the actual goal of this whole multi-session effort**

- `DbSource`/`DbSourceParser`/`DbSourceWriter`/`Sanitizer.ApplyToDb`/`DbIrSerializer`/`DbIrParser`
  only modeled a DB's own `Static` section — correct for every Instance DB grounded so far, but
  `TomraControlInst1` (needed for `PlantAutoControl`'s own dependency closure) genuinely persists its own
  FB's Input/Output formal-parameter storage too. Extended to model `Input`/`Output`/`InOut`
  sections on a DB the same way `BlockSource` already does for FC/FB blocks. New fixture + 4 tests,
  287/287 converter tests.
- **Phase 2 closed**: built the exact, current dependency list via `Sanitizer.Apply` against an
  empty map (collects every missing entry in one pass) — 523 missing entries, 26 real DB/tag-table
  roots (20 Instance DBs of the 8 already-proven dependency FBs, 6 GlobalDBs/tag tables), 343
  unique tag paths. All 26 exported, sanitized (maps generated programmatically off the
  missing-entries list), imported, and block-level compiled clean.
- **Phase 3 closed**: built `PlantAutoControl`'s own sanitization map (343 tags, 35 names, 20 network
  titles) the same way, driven directly off the missing-entries list. Sanitized, imported, and
  **compiled clean on the first attempt — 0 errors.** Re-exported and confirmed
  `Normalizer.AreSemanticallyEquivalent` = **true**.
- This is the actual Layer 1 assertion (`docs/08-testing-strategy.md`) S1 exists to prove,
  demonstrated end-to-end for `PlantAutoControl` itself — the real site master-control block, its
  complete real dependency closure, imported into an independent TIA project, compiling clean, and
  round-tripping losslessly. Commit `a1f46d3`. Full story: `docs/notes/stage-gates.md` ("Phase 2"/
  "Phase 3").

**`Sub`/`Div`/`Le` converter support; fix a real `Sanitizer` gap — a Call Part's own callee name was never sanitized; `MotorFwdRevSystem` fixed at the source — all 8 `PlantAutoControl` dependency FBs now compile clean**

- `Sub`/`Div` (confirmed real via `FC Scale`) round out the arithmetic family alongside `Mul`/`Add`
  — the key structural difference is they never carry a `Card` `TemplateValue` (always binary, no
  chaining), modeled via a nullable `PartNode.Cardinality`. `Le` completes the IEC comparison
  family (`Eq`/`Ge`/`Lt`/`Ne`/`Gt`/`Le`), none left unconfirmed. 283/283 converter tests.
- **Real bug found and fixed**: `Sanitizer.SanitizeNetwork` renamed a Call Part's own `Instance`
  reference but never its `BlockName` (the callee's own name) — found live via `MotorVSDSystem`
  still failing to compile ("Scale no longer exists") after its own `Scale` dependency (renamed
  `AnalogScale`) already compiled clean standalone. Fixed by renaming `BlockName` via `map.Names`,
  same treatment as `Instance`.
- **`MotorFwdRevSystem` fixed at the source**: the `CycleDelayReset` blocker (a standalone
  single-instance `TON`, invisible to the whole `SW.Blocks` object model — no `create-instance-db`
  path, not listed, not exportable by name) was confirmed exhausted from the Openness side. Project
  owner then fixed the actual root cause directly in `JOB9002`: converted `CycleDelayReset` to a
  proper multi-instance `Static`-section timer, the same shape every other working timer in the FB
  already uses. One small map gap (a stale identity mapping that should have followed the
  `FaultTripTimer2`→`FaultTripTimer` rename) fixed alongside it.
- **All 8 of `PlantAutoControl`'s dependency FBs now compile clean** — `MotorStarter`/
  `EquipmentControlSystem`/`ShredderControlSystem`(bar one hw-config tag)/`FilterUnitSystem`/
  `AirStarSystem`/`TomraControlSystem`/`MotorVSDSystem`/`MotorFwdRevSystem`. Commit `e2fb301`. Full
  story: `docs/notes/stage-gates.md`.

**Phase 1 continued: `FilterUnitSystem`/`AirStarSystem` compile clean; `Gt` comparison + a fourth Input/Output member shape added; `MotorVSDSystem`/`MotorFwdRevSystem` blocked on two new, real, deliberately-deferred gaps**

- `FilterUnitSystem` (`FilterUnitSystem`) compiled clean first try — fully self-contained, reuses the
  already-imported `TypeDOL`/`MotorIOSet` UDT.
- `AirStarSystem` (`AirStar`) compiled clean after adding 5 more tag-table entries
  (`AirStarWord0IN`/`2IN`/`0OUT`/`2OUT`, `FirstScan`) and reusing the already-imported
  `PlantControl.Test` reference — no new DB needed. First real block-level `TITLE` seen since
  `MotorVSDSystem` (new map field `Titles`, distinct from `NetworkTitles`).
- **`Gt` (greater-than) comparison added** — a real, previously-unconfirmed member of the
  `Eq`/`Ge`/`Lt`/`Ne` comparison family, confirmed real via `FC Scale` (a dependency of `MotorVSDSystem`).
  Identical shape to the other four; mechanical addition across `FlgNetParser`/`GraphReducer`. 4
  new tests.
- **A fourth, genuinely minimal Input/Output/InOut member shape added** — `FC Scale`'s own params
  have no `Remanence` attribute and no `<AttributeList>` at all (distinct from every shape already
  modeled). New `DbMember.IsBareParameter` flag to round-trip the writer's own shape choice. 1 new
  test. **272/272 converter tests** (up from 267).
- **`MotorFwdRevSystem` blocked**: needs a new UDT (`MotorFwdRevIOSet1`, built/imported successfully), but
  one network uses `CycleDelayReset`, a standalone named `TON` instance (`GlobalVariable`-scoped) —
  distinct from the FB's own multi-instance Static timers. `create-instance-db --instance-of TON`
  fails (`PlcBlockComposition.Create` only resolves user FBs, not built-in system instructions).
  Flagged, not chased — a genuinely different case from anything solved so far.
- **`MotorVSDSystem` blocked**: needs a new UDT (`TypeVSD`, built/imported successfully) and calls `FC
  Scale` (grounded above), but `Scale`'s own internal logic also uses `Sub` (subtraction) — an
  unsupported arithmetic instruction, a bigger addition (like `Add`/`Mul` were) than time/usage
  budget allowed this session. Flagged, not chased.
- 6 of `PlantAutoControl`'s 8 dependency FBs now proven (`MotorStarter`/`EquipmentControlSystem`/
  `ShredderControlSystem`(bar one tag)/`FilterUnitSystem`/`AirStarSystem`), 2 blocked on real,
  clearly-scoped, deliberately-deferred gaps (`MotorFwdRevSystem`/`MotorVSDSystem`). Full story:
  `docs/notes/stage-gates.md`.

**Converter: fix two more real data-loss bugs (arbitrary-depth anonymous-struct nesting; a duplicated, independently-broken IR-text serializer) — `ShredderControlSystem` imports and compiles clean bar one flagged hardware-config dependency**

- Phase 1, third dependency FB: `ShredderControlSystem` needed a much larger external footprint than
  `MotorDOL`/`EquipmentControlSystem` — the real `Control` DB (renamed `PlantControl`, sanitized properly after
  an initial identity-only map was caught and corrected) and 44 more PLC tag-table entries
  (`Tag_1`-`Tag_44`, combined with the existing 10 into one 54-tag minimal table).
- **A deeper anonymous-struct bug**: `ComsOutByte501` nests four further `Struct`-typed members,
  one nesting a third level again — the single-level `EquipmentControlSystem` fix didn't recurse. Made
  `ParseTypeMember`/`WriteTypeMember` genuinely recursive (arbitrary depth).
- **A separate, independently-broken duplicate**: the IR *text* format (`to-ir`/`to-xml`) had its
  own two-level-only member serializer in `IrSerializer.cs` (block STATIC sections), never fixed
  alongside `DbIrSerializer.cs`'s own copy. Consolidated both into shared
  `DbMemberLineFormat.SerializeMemberRecursive`/`ParseMemberRecursive`, used everywhere. New tests
  at both the DB and block level (the block-level test exists specifically because the DB-level
  ones alone wouldn't have caught `IrSerializer`'s own separate bug). 267/267 converter tests (up
  from 265).
- **Live-verified, iteratively**: cleared a `PlantControl`-not-yet-compiled error (compile it
  first) and a `Clock_0.5Hz`-not-defined error (a genuine PLC tag, added to the minimal table).
  **One gap deliberately left open**: `Clock_0.5Hz` turned out to be Siemens's own auto-generated
  "Clock memory byte" system tag — a plain imported `PlcTag` with the same name/address doesn't
  carry the internal registration TIA's compiler expects; the real fix needs a CPU hardware-config
  change, a capability category this project has never touched (CLAUDE.md hard rule 6). Flagged to
  the project owner, who chose to stop here rather than build hardware-config support.
- `ShredderControlSystem` imports and compiles clean except for this one flagged tag — everything
  else (its own logic, the `Control`/`PlantControl` reference, all 54 needed tags) is proven.

**Converter: fix a real data-loss bug — an anonymous `Struct` member's nested fields were silently dropped; `EquipmentControlSystem` (`EquipmentControlSystem`) is the second `PlantAutoControl` dependency FB to compile clean**

- Phase 1 of the approved `PlantAutoControl` round-trip plan (get the remaining 7 dependency FBs
  importing/compiling standalone): exported `EquipmentControlSystem` from `JOB9002`, and its own `Inputs`/
  `Outputs : Struct` members (a plain anonymous struct, no UDT type name) compiled with **zero
  nested fields** after import — `"Interface: A structure without components is not allowed"` /
  `"Tag #Inputs.InHand not defined"`.
- Root cause: this is a third real structured-member shape, distinct from the two already known
  (UDT-typed / system-function-block instance): nested `<Member>` elements are direct children of
  the owning member (no `<Sections><Section Name="None">` wrapper), each carrying its own full
  `<AttributeList>`. The existing parser only checked for the `<Sections>` wrapper to detect
  structured content, so `Datatype="Struct"` fell through to the plain-scalar path and its nested
  members were silently discarded — no error, no warning, `to-ir` looked entirely successful.
- Fixed: detect direct nested `<Member>` children and parse them via the same helper `TYPE`'s own
  members already use (`ParseTypeMember`/`WriteTypeMember` — the shapes are identical). 3 new
  tests, genericized fixture. 265/265 converter tests (up from 262).
- **Live-verified**: re-sanitized and re-imported `EquipmentControlSystem` (as `EquipmentControlSystem`) into
  `SampleProject` — `compile --block EquipmentControlSystem`: `STATE: Success, ERRORS: 0`, on the
  first attempt after the fix, no instance-DB step needed. Whole-project compile also clean.
  Second of `PlantAutoControl`'s 8 dependency FBs proven to round-trip *and* compile.

**`openness-cli`: new `create-instance-db` command — `MotorStarter` is now the first `PlantAutoControl` dependency FB proven to compile, not just import, in a target project**

- Phase 0.2 of the approved `PlantAutoControl` round-trip plan: ground `PlcBlockComposition.
  CreateInstanceDB` live before assuming it's the right tool for the "Missing instance DB" gap
  blocking `MotorStarter` (and, eventually, `PlantAutoControl`'s own 20 call-site instances).
- Confirmed real signature via `Siemens.Engineering.xml` (TIA V20 `PublicAPI`):
  `CreateInstanceDB(name, isAutoNumbered, number, instanceOfName) -> InstanceDB`. Not S6+ logic
  generation or tag/hardware invention (CLAUDE.md hard rule 3) — the DB number is always
  auto-assigned, never a literal passed in; the DB's own content is entirely derived from the
  existing FB's own declaration.
- Built `OpennessGateway.CreateInstanceDb` + a new `create-instance-db` subcommand
  (`ArgumentParser`/`Program.cs`), 5 new argument-parser tests (101/101 `openness-cli` tests, up
  from 96).
- **Live-verified**: created `MotorStarter_Instance` (instance of `MotorStarter`) in
  `SampleProject`. `compile --block MotorStarter` went from `"Missing instance DB"` (two networks)
  to `STATE: Success, ERRORS: 0`; a subsequent whole-project compile was also clean. This closes
  the "Tier 1" gap flagged when the `PlantAutoControl` round-trip plan was scoped — `MotorStarter` now
  round-trips *and* compiles, the first of `PlantAutoControl`'s 8 dependency FBs to do so.

**Converter + `openness-cli`: PLC tag table support (`TAGTABLE`), deliberately minimal, live-verified**

- Found while confirming `PlantAutoControl`'s own exact dependency list: 10 of its ~36 referenced
  top-level roots (`Tag_45`-`Tag_54`) turned out to be genuine PLC tag-table entries (bare
  single-component `Access`, absent from `list`'s own block enumeration), not DB members — a
  construct this project had never touched before.
- Grounded first against the real "Default tag table" (`station_2/JOB9002_PLC`, 900+ tags): root
  `SW.Tags.PlcTagTable`, simpler than a DB or UDT (no nesting, no `BooleanAttribute`-wrapping, no
  table-level Comment/Title).
- Built: converter parse/write/IR/sanitize (`PlcTagTableModel.cs`, `PlcTagTableSourceParser/
  Writer.cs`, `Ir/TagTableIr.cs`, `Sanitizer.ApplyToTagTable`); `openness-cli` `EnumerateTagTables`/
  `ExportTagTable`/`ImportTagTables`, `list --tagtables`, `export`/`import --tagtable`.
- **Deliberately minimal, not a general tag-table framework** (project owner's own explicit call)
  — covers only what `PlantAutoControl`'s own 10 dependency tags need. Full, dated list of intentional
  gaps (no constants, no folder support, `DataTypeName` never sanitized, no `delete`/`compile
  --tagtable`, whole-table-only Openness import/export): `src/converter/README.md`, "PLC tag table
  support".
- **Live-verified end to end**: `export --tagtable "Default tag table" --device JOB9002_PLC`
  against `JOB9002` succeeded first try. Built a minimal 10-tag table by filtering the real export's
  own IR text down to `Tag_45`-`Tag_54` (editing IR, not raw SimaticML) rather than sanitizing the
  full 900+-tag table. `import --tagtable` against `SampleProject`'s root tag-table group
  succeeded first try; `list --tagtables` confirmed it present. 262/262 converter tests, 96/96
  `openness-cli` tests (up from 255/94).

**Converter: fix a real `Sanitizer` bug — Part instances (Timer/CALL) were never sanitized**

- Project owner spotted it directly, reading the sanitized `MotorStarter` output: timer names were
  renamed at their Interface declaration but the same timers' own `<Instance>` references in the
  network body were left as the real names.
- Root cause: `SanitizeNetwork` only ever walked `network.AccessNodes`, never `network.Parts` — so
  a `PartNode.Instance` (every `TON`/`TONR`/`TOF`/`CALL`'s own instance reference) was never
  reached. Invisible until now because every previously-tested renamed tag happened to be an
  ordinary Access reference, never a Part's own Instance.
- Fixed: `SanitizeNetwork` now also sanitizes each `Part.Instance`. Verified as a genuine
  regression (not just plausible) — the new test was confirmed to fail against the pre-fix code
  via `git stash`, then pass with the fix. 255/255 converter tests pass. Full story:
  `docs/notes/stage-gates.md`.

**`tests/golden`: fix a real gap in `Normalizer` — wire-endpoint order isn't semantically meaningful**

- Requested full round-trip proof for `MotorStarter` (`export → to-ir → to-xml → import&compile →
  export → compare`) hit a real blocker: the block is currently `INCONSISTENT` (missing-instance-DB
  gap, see below), and `Export()` refuses any inconsistent block — blocking both ends of the live
  round trip the same way. Did the pure converter round trip instead (`to-ir → to-xml → compare`,
  no live TIA), confirmed with the project owner.
- IR round trip was byte-identical, but XML-level `Normalizer.AreSemanticallyEquivalent` reported
  `false`. Investigated: 100% of the difference was endpoint order within individual `<Wire>`
  elements — the same wires, same endpoints, different XML order, nothing else.
- `Normalizer` already treats Wire-vs-Wire order and Access UId numbering as non-semantic (TIA
  relocates/renumbers freely) but had never extended that to a wire's *own* endpoint list — never
  exercised before since no prior comparison ran a converter-only round trip twice against a
  network with genuine wire fan-out. Fixed (`Wire` added to the existing order-independent sort).
  Confirmed the fix doesn't mask real differences (the existing differing-endpoint-set test still
  correctly fails). `MotorStarter`'s round trip now confirms `true`. Full story:
  `docs/notes/stage-gates.md`.

**Converter: `FlgNetBuilder` Part-ordering fix — `MotorDOL` now imports into `SampleProject`**

- Two real bugs found and fixed, root-caused against a fresh `MotorDOL` export: (1) `OrStep`'s own
  Part was emitted before its branches — fixed to mirror `NotStep`'s children-then-self order. (2)
  Timer builds were phase-hoisted (all built in one global phase) rather than inline with the
  production that needs them — real TIA export order is contiguous per rung, not grouped by
  construct type; fixed with a new `EnsureTimerBuilt` helper.
- Live-verified: regenerated `MotorDOL`'s sanitized XML, confirmed Part order now matches the real
  TIA export exactly, and the import into `SampleProject` succeeded (`MotorStarter`, FB2) — the
  blocker this investigation existed to resolve.
- A new, separate, expected finding on compile (not a regression): "Missing instance DB" — same
  category of gap already known for `PlantAutoControl` itself (S1 items 16/17), not a converter bug.
- 254/254 converter tests, 11/11 golden-harness. Full story: `docs/notes/stage-gates.md`.

**`openness-cli`: concurrent-Portal stability audit — two real bugs found and fixed, feature itself cleared**

- Project owner asked for a rigorous test schedule after the "second Portal instance sometimes
  won't connect" symptom recurred twice in one session. Full walkthrough, phase by phase, real
  PIDs/timings recorded: `docs/notes/concurrent-portal-test-plan.md`.
- **Verdict: the concurrent-session feature is not the cause of instability.** Every stress
  scenario tried behaved correctly; the symptom correlates with stale-process pileup, not
  concurrency (5/5 fresh launches succeeded from a clean baseline; every real hang this session
  happened with processes already piled up).
- **Two real bugs found and fixed, both closed same-day**: (1) `OpenProject()` was silently
  repurposing a human's own freshly-launched, empty Portal window — fixed and retested live. (2) A
  client killed mid-launch left a permanently orphaned process behind — fixed with a new
  `LaunchedInstanceRegistry` that lets the tool recognize its own past orphans (via
  `TiaPortalProcess.Id`, missed entirely by the original 2026-07-10 API survey) without ever
  touching a human's window. Live-verified end to end.
- 89/89 openness-cli tests pass (up from 79). Full story: `docs/notes/stage-gates.md`.

**UDT / PLC data type support (S1 item 26) — converter + `openness-cli`**

- Closes a real gap the paused `MotorDOL` full-cycle test found: even a fully self-contained FB
  still depends on its own declared UDT, and neither the converter nor `openness-cli` had any
  PLC-data-type support at all. Grounded against a real export (`TypeDOL`) before writing any
  parser code, per this project's own discipline.
- New converter pipeline: `PlcTypeSource`/`PlcTypeSourceParser`/`PlcTypeSourceWriter`/`TypeIr.cs`
  (mirroring the DB pipeline, reusing `DbMember`/`DbMemberLineFormat` directly),
  `Sanitizer.ApplyToType`. New `openness-cli` `--type` support on `export`/`import`/`compile`
  (`ExportType`/`ImportTypes`/`CompileType`, using the real `PlcType`/`PlcTypeComposition`/
  `PlcTypeGroup` API — no safety check needed, `PlcType` has no `ProgrammingLanguage` at all).
- 10 new converter tests, 6 new openness-cli tests — 254 converter, 79 openness-cli, 11
  golden-harness, all green.
- **Live-verified**: sanitized `TypeDOL`→`MotorIOSet` imports and compiles cleanly in
  `SampleProject`; re-importing `MotorDOL` no longer hits `Data type "MotorIOSet" is unknown` —
  the original blocker is resolved. A new, separate `FlgNetWriter` Part-ordering bug now blocks
  `MotorDOL`'s own import (`"The elements must be sorted according to the current flow"`) —
  flagged as a distinct open item, not fixed here. Full story: `docs/notes/stage-gates.md` ("S1
  item 26").

**`openness-cli`: fix a real Portal-instance-pileup bug in the concurrent-session fix**

- The concurrent-session fix (2026-07-13) traded "force-close whatever's open" for "always launch
  a fresh instance if occupied" — safe, but caused a real instance pileup during actual use: a
  Bash-tool-level timeout killed a mid-import CLI process (twice), likely leaving Portal stuck
  server-side, and each retry's `Connect()` only ever checked one arbitrary process before
  launching yet another rather than looking for any other already-usable one. Process count grew
  3 → 4 → 5.
- Fixed in two rounds, both live-verified against the real messy state this produced:
  `OpenProject()` now does two full passes over every running process — an exact already-open
  match anywhere wins first, only then is an empty process considered fair game, only then does
  it fall back to launching fresh. A separate bug (forward-slash vs. backslash path comparison
  causing a spurious extra launch) was also found and fixed.
- All 73 openness-cli tests + converter/golden-harness suites green throughout. Cleaned up
  confirmed-idle stray Portal processes with explicit go-ahead. Full story:
  `docs/notes/openness-quirks.md` ("Follow-up, 2026-07-14").

## 2026-07-13

**`openness-cli`: safe concurrent Portal sessions on different projects**

- Picked up per the project owner's own ask: they need to do manual PLC engineering in TIA Portal
  tomorrow, on a different project, while this tool keeps working a separate one at the same time.
- Investigating surfaced a real, pre-existing safety gap: `OpenProject()` used to save-and-close
  *whatever* project was open in the attached Portal process before opening its own target — safe
  under the single-operator assumption CLAUDE.md documented, unsafe the moment a human runs Portal
  manually alongside this tool. Fixed: never force-close a project this tool didn't open itself —
  launch a dedicated fresh Portal instance instead when the attached one is occupied by something
  else. No new flag; strictly safer, no downside for existing solo use.
- **Live-verified for real**, not simulated: launched TIA Portal directly with `SampleProject`
  open (bypassing Openness, to genuinely mimic an independent human session), then ran
  `openness-cli` against `JOB9002` while that session stayed up. Confirmed via `tasklist` and a
  before/after content diff: `SampleProject`'s session was never touched, `JOB9002` opened and
  listed successfully via its own separate Portal instance — six Portal processes coexisted with
  zero interference. All three suites green: 73 openness-cli, 244 converter, 11 golden-harness.
- Updated `CLAUDE.md`'s own environment note (no longer "single Portal instance/session
  assumption"). One residual, non-fixable caveat documented: the very first attach to an
  already-running process can still trigger TIA's own one-time approval dialog, even if that
  process turns out to be someone else's — no data risk, just a one-time visual interruption.
  Full story: `docs/notes/stage-gates.md`.

**`openness-cli`: block deletion, import-overwrite confirmation, API surface survey**

- Picked up per the project owner's own ask: add a way to delete blocks, confirm live whether
  `import` actually overwrites a pre-existing block, and survey the Openness API for other unused
  capabilities. Formally planned given the scope (new gateway method, new CLI subcommand, a new
  irreversible-action safety pattern).
- Research (installed `Siemens.Engineering.xml` doc comments, not just reflection): confirmed
  `PlcBlock.Delete()` exists; `ImportOptions.Override` is documented "Override existing". Broader
  survey found several other unused members (`SWImportOptions`, `Find`/`Create`, `CreateFB`/
  `CreateInstanceDB` — S6+ scope, `GetAttribute`/`SetAttribute`) — full list in
  `docs/notes/openness-api-surface-v20.md`.
- New `IOpennessGateway.DeleteBlock`/`openness-cli delete <project> --block <name> [--device
  <name>] --yes` — mirrors `export`/`compile`'s own resolution and safety refusal. `--yes` is
  required to actually delete (a new safety pattern — this is the first irreversible operation
  this CLI exposes); without it, prints a dry-run preview and exits a new `NotConfirmed` code.
  7 new argument-parser tests. All three suites green: 73 openness-cli (up from 68), 244
  converter, 11 golden-harness.
- **Live-verified against `SampleProject` only** (never `JOB9002`): confirmed `import`'s `Override`
  genuinely overwrites a block's content in place (via `TimerSample`, Green-tier, fully reversed
  afterward — byte-identical to the original except the export's own timestamp). Confirmed
  `delete`'s dry-run and confirmed paths both work correctly, then used it to remove the leftover,
  uncompiled `PlantAutoControl` block left over from an earlier cross-project import test — completing
  outstanding cleanup that had been waiting since S1 items 16/17. The confirmed delete itself was
  first blocked by Claude Code's own safety classifier (the plan had named the target and been
  approved, but the classifier required the user to name it directly) — stopped and asked, project
  owner confirmed explicitly, then proceeded. Full story: `docs/notes/stage-gates.md`.

**S1 item 25: `SWAP` (byte-swap box instruction) — built, tested, live-verified; `TomraControlSystem` fully round-trips, all 8 `PlantAutoControl` dependency FBs closed**

- Picked up per the project owner's own choice ("let's ground Swap for TomraControlSystem"), the last
  remaining gap in `PlantAutoControl`'s own 8 dependency FBs. Grounded against real `TomraControlSystem` (2
  instances, identical shape): `<Part Name="Swap" UId="N" DisabledENO="true"><TemplateValue
  Name="SrcType" Type="Type">Word</TemplateValue></Part>` — structurally identical to `Convert`
  minus `DestType`.
- A genuine design fork surfaced (standalone `SwapStatement` vs. folding into `ConvertStatement`
  with a nullable `DestType`) — resolved via a clarifying question before any code; project owner
  chose the standalone type, keeping each source Part Name mapped to its own IR construct.
- Every touchpoint mirrors `Convert`'s own exactly, minus the `DestType` field/line/group:
  `Ir.Model`, `FlgNetParser`/`FlgNetWriter`, `GraphReducer.ReduceSwap`/`FlgNetBuilder.BuildSwap`,
  and the `SWAP(EN := ..., IN := ...) => <dest>` readable-form grammar in `IrSerializer`/
  `IrParser`.
- 8 new tests, one new fixture genericized from the real Contact-gated topology. All three suites
  green: 244 converter (up from 236), 68 openness-cli, 11 golden-harness.
- **Live-verified**: fresh `TomraControlSystem` export (real `JOB9002_PLC` device) converted **completely,
  no errors at all**, and round-trips `to-ir → to-xml → to-ir` **byte-identical**, confirmed
  genuinely exercising both real `Swap` occurrences via direct grep. `TomraControlSystem` is now the
  **eighth and final** of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip — all 8 are now
  fully instruction-level round-trippable end to end. Full story: `docs/notes/stage-gates.md`
  ("S1 item 25").

## 2026-07-12

**S1 item 24: `CALL` without `<Instance>` (a real FC call) — built, tested, live-verified; `MotorVSDSystem` fully round-trips**

- Picked up per the project owner's own choice, to close `MotorVSDSystem`'s own hard error on `<Call
  UId="52">` missing `<Instance>`. Every prior `<Call>` (S1 item 14, 20 real instances) called an
  FB and carried one, so this was a genuinely new shape — formally planned, with mandatory Phase 0
  grounding before any code.
- Phase 0 confirmed the working hypothesis exactly: `<CallInfo Name="Scale" BlockType="FC">` — a
  call to Siemens' own standard-library `Scale` function, stateless, genuinely no `<Instance>`
  element at all (5 Input + 1 Output `Real` parameters, `en` rail-fed).
- `CallStatement.InstancePath` and `CallStatementSidecar`'s three Instance fields all made
  nullable (all-or-nothing group, not a discriminated union). `FlgNetParser.ParseCall` checks
  `<Instance>`'s presence directly rather than gating on `BlockType`; `FlgNetWriter`/
  `GraphReducer`/`FlgNetBuilder` all made null-safe symmetrically. Readable-form grammar omits the
  instance argument entirely when absent (`CALL Scale(EN := ..., ...)`), disambiguated on parse by
  checking whether the first argument itself starts with the reserved `EN := ` prefix.
- 6 new tests, one new fixture. All three suites green: 236 converter (up from 230), 68
  openness-cli, 11 golden-harness.
- **Live-verified**: fresh `MotorVSDSystem` export (real `JOB9002_PLC` device) converted **completely, no
  errors at all**, and round-trips `to-ir → to-xml → to-ir` **byte-identical**, confirmed
  genuinely exercising the no-Instance `Scale` call via direct grep. `MotorVSDSystem` is now the
  **seventh** of `PlantAutoControl`'s own 8 dependency FBs to fully round-trip. Only `TomraControlSystem`
  (`Swap`) remains blocked. Full story: `docs/notes/stage-gates.md` ("S1 item 24").

**S1 item 23: `TOF` (off-delay timer) — built, tested, live-verified; `AirStar` fully round-trips**

- Picked up per the project owner's own choice, asked directly what `TOF` was likely to be —
  answered from prior knowledge before any grounding: an off-delay timer, IEC sibling of
  `TON`/`TONR`. Grounded directly against real `AirStar` (the block that first surfaced it).
  Confirmed structurally identical to `TON` — same `Version`/`Instance`/`time_type` shape, same
  `IN`/`PT`/`ET` ports, no reset port (unlike `TONR`), no `EN`/`ENO`.
- `TimerKind` gains a third variant, `Tof` — needing zero new fields, simpler than `TONR`'s own
  addition. Every touchpoint `TONR` already generalized just needed a third case added.
- 5 new tests, one new fixture. All three suites green: 230 converter (up from 225), 68
  openness-cli, 11 golden-harness.
- **Live-verified — a genuine milestone**: fresh `AirStar` export converted **completely, no
  errors at all**, and round-trips `to-ir → to-xml → to-ir` **byte-identical**, confirmed
  genuinely exercising both `TOF`/`Ne`. `AirStar` is now the **sixth** of `PlantAutoControl`'s own 8
  dependency FBs to fully round-trip (S1 item 19 already got 5 there; this closes `AirStar`,
  the last of the three the S1 item 20 Interface-modeling sweep found). Only `TomraControlSystem`
  (`Swap`) and `MotorVSDSystem` (a `<Call>` missing its `<Instance>`) remain blocked. Full story:
  `docs/notes/stage-gates.md` ("S1 item 23").

**S1 item 22: `Ne` (not-equal comparison) — built, tested, live-verified**

- Picked up per the project owner's own choice, asked directly what `Ne` was likely to be —
  answered from prior evidence already in the repo (an existing `ir/SPEC.md` table row, an unused
  `<>` token already wired up in `IrParser`) before any grounding: the not-equal sibling of
  `Eq`/`Ge`/`Lt`, all three already fully supported.
- Grounded directly against real `AirStar` (the block that first surfaced `Ne`, S1 item 21's own
  live verification) rather than a fresh plan-mode cycle — small, well-precedented, third
  comparison-family addition this session. Confirmed identical shape to `Eq`/`Ge`/`Lt`.
- `FlgNetParser`/`GraphReducer` each gained a fourth comparison case. Everything downstream needed
  **zero changes** — `CompareStep.PartName`/`Expr.Compare.Operator` are both carried verbatim, not
  derived from a hardcoded switch.
- 4 new tests, one new fixture. All three suites green: 225 converter (up from 221), 68
  openness-cli, 11 golden-harness.
- **Live-verified:** the `Ne` error is gone from `AirStar`. Doesn't fully round-trip yet —
  progresses to `TOF` (an off-delay timer, spotted alongside `Ne` during grounding — a new,
  separate, unaddressed gap). Full story: `docs/notes/stage-gates.md` ("S1 item 22").

**S1 item 21: `Access Scope="LocalConstant"` — built, tested, live-verified**

- Picked up per the project owner's own choice — the last of the two real gaps S1 item 20's live
  verification found. Grounded against `MotorVSDSystem`/`AirStar` (4 independent instances): a genuine
  fourth Access shape, `<Access Scope="LocalConstant"><Constant Name="X" /></Access>` — a bare
  reference by name to the block's own declared `Constant`-section member (S1 item 20), no value
  at the reference site at all, neither `AccessNode`'s `<Symbol>` shape nor `ConstantAccessNode`'s
  `<ConstantType>`/`<ConstantValue>` shape.
- Modeled as an `AccessNode` with a one-element `ComponentPath` — `DottedPath`/`FromDottedPath`
  needed zero changes. `FlgNetParser.ParseAccess`/`FlgNetWriter`'s `AccessNode`-writing loop both
  gained a scope-conditional branch. `GraphReducer.cs` needed zero changes (UId-lookup-based
  dispatch, not scope-based).
- 5 new tests, one new fixture. All three suites green: 221 converter (up from 216), 68
  openness-cli, 11 golden-harness.
- **Live-verified:** the `LocalConstant` error is gone from both `MotorVSDSystem` and `AirStar`. Neither
  fully round-trips yet — each hits a different, new, unrelated gap (`MotorVSDSystem`: a `<Call>`
  missing its own `<Instance>`; `AirStar`: `Ne`, not-equal, an unsupported comparison Part Name —
  the first confirmed-real sibling of `Eq`/`Ge`/`Lt`). Neither addressed by this item. Full story:
  `docs/notes/stage-gates.md` ("S1 item 21").

**Follow-up: `Mul`'s own `SrcType` fixed — correction to already-committed S1 item 18 code**

- Picked up immediately after S1 item 20's own live verification found it (`FB AirStar`'s own
  `Mul` carries an ordinary `<TemplateValue Name="SrcType">` instead of the self-closing
  `<AutomaticTyped />` shape S1 item 18 confirmed universal from `MotorDOL`/`EquipmentControlSystem`).
- `FlgNetParser.ParseMulFixedShape` now accepts either shape, hard-erroring only if both or
  neither is present. No new `PartNode` field needed — the existing `AutomaticSrcType`/`SrcType`
  fields already coexist generically. `MulStatementSidecar` gained a nullable `SrcType` field
  (sidecar-only). `FlgNetWriter` needed zero changes.
- 5 new tests, one new fixture. All three suites green: 216 converter (up from 211), 68
  openness-cli, 11 golden-harness.
- **Live re-verified against the real `AirStar` export**: the `Mul`-specific error is gone — the
  block now progresses to the same `Access Scope="LocalConstant"` gap `MotorVSDSystem` also hits
  (unrelated, still open). Full story: `docs/notes/stage-gates.md` ("Follow-up... `Mul`'s own
  `SrcType` fixed").

**S1 item 20: FC/FB parameter-interface modeling (`Input`/`Output`/`InOut`/`Constant`) — built,
tested, live-verified; closes the last real gap from the original 8-FB dependency sweep**

- Picked up per the project owner's own request. Zero real Input/Output/InOut/Constant member XML
  had ever been captured anywhere before this item — the strongest Phase 0 mandate of any item
  this session. Grounded against `TomraControlSystem` (Input/Output) and `MotorVSDSystem`/`AirStar`
  (Constant, two independent instances).
- `Input`/`Output`: same shape as `Static`'s own full member shape, but missing the `SetPoint`
  BooleanAttribute `Static` always carries — `DbInterfaceMembers.ParseMember`/`WriteMember`
  gained a `requireSetPoint`/`includeSetPoint` parameter rather than a parallel type.
- `Constant`: a genuinely distinct third shape (no `AttributeList`, required `StartValue`) — new
  `ParseConstantMember`/`WriteConstantMember`.
- Deliberately a block-level concept only, per ADR-0001 — `CALL`'s own call-site wiring untouched.
- Found and fixed a related bug in the same path: `Return`'s boilerplate was written
  unconditionally for every block; real FBs never have a `Return` section at all — now emitted
  only for non-FB blocks.
- Also corrected `ir/SPEC.md`'s own stale `INTERFACE` grammar sketch, which had never listed
  `STATIC` despite it being the one section actually implemented since S1 item 7 Phase B.
- 10 new/changed converter tests, one hard-error test repurposed into a positive test (its old
  fixture was synthetic, never sourced from a real export). All three suites green: 211 converter
  (up from 207), 68 openness-cli, 11 golden-harness.
- **Live-verified:** whole-block `to-ir` on all 3 previously-blocked FBs confirms the Interface
  gap is genuinely closed for all three — none hit an Interface-section error anymore. None fully
  round-trips yet: each hits a different, new, unrelated gap (`TomraControlSystem`: `Swap`; `MotorVSDSystem`:
  `Access Scope="LocalConstant"`; `AirStar`: a **real correction needed to already-committed S1
  item 18 code** — `Mul`'s own `SrcType` isn't always the self-closing `AutomaticTyped` shape,
  contradicting what S1 item 18 confirmed universal). Flagged as new open items, not fixed
  speculatively. Full story: `docs/notes/stage-gates.md` ("S1 item 20").

**S1 item 19: `TONR`/`Add`/`Lt` — built, tested, live-verified; closes every instruction-level
gap in `PlantAutoControl`'s 5 previously-blocked dependency FBs**

- Picked up per the project owner's choice (via `AskUserQuestion`) over the FC/FB
  parameter-interface gap. Phase 0 grounding (`MotorDOL`/`FilterUnitSystem`) found `TONR`'s shape
  identical to `TON` plus one genuine new port, `R` (reset) — tag-fed, no chain, same shape as
  `PT`. `TimerKind` (`Ton`/`Tonr`) enum added, mirroring `CoilKind`.
- **Scope expanded mid-grounding, confirmed via `AskUserQuestion`**: the same real networks also
  needed `Add` (identical shape to `Mul`) and `Lt` (a third comparison operator alongside
  `Eq`/`Ge`) to fully round-trip. Checked directly against the real wiring that `Add`'s own `en`
  (fed by `Lt`'s `out`) is an ordinary condition, not ENO-chained — no speculative new branch
  added to the `Mul`/`Convert` ENO-chain check.
- `MulKind` (`Multiply`/`Add`) enum added, same pattern as `TimerKind`/`CoilKind`. `Lt` needed no
  new model shape — `ChainStepSidecar.CompareStep` already carries `PartName` generically.
- 16 new converter tests, three fixtures genericized from the real grounded shapes. All three
  suites green: 207 converter (up from 191), 68 openness-cli, 11 golden-harness.
- **Live-verified:** whole-block `to-ir` on all 5 previously-`TONR`-blocked dependency FBs
  (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`/`FilterUnitSystem`/`MotorFwdRevSystem`) all fully converted —
  confirmed genuinely exercising the new code via grep counts, not a lucky no-op.
  `MotorDOL`/`FilterUnitSystem` additionally round-trip `to-ir → to-xml → to-ir` byte-identical. All 5
  of `PlantAutoControl`'s dependency FBs previously blocked by `Mul`/`Convert`/`TONR` now fully
  round-trip as whole blocks — the remaining 3 (`TomraControlSystem`/`MotorVSDSystem`/`AirStar`) are blocked
  only by the separate, already-known FC/FB parameter-interface gap. Full story:
  `docs/notes/stage-gates.md` ("S1 item 19").

**S1 item 18: `MUL`/`CONVERT` (arithmetic) — built, tested, live-verified**

- Picked up per the project owner's sequencing after Title ("scope arithmetic support next"),
  planned formally in plan mode. Phase 0 grounding (`MotorDOL`/`EquipmentControlSystem`/`ShredderControlSystem`)
  found a real surprise contradicting the plan's own inherited Move/WAND-based assumption:
  despite `DisabledENO="true"` on both `Mul` and `Convert`, real networks chain `Mul`'s own `eno`
  directly into the following `Convert`'s own `en` — a genuine control-flow dependency, not a
  boolean condition. Flagged to the project owner before implementing; approved design: "Go with
  that design."
- New `EnSource` discriminated union (`Condition` | `PrecedingEno`) rather than folding the chain
  into `Expr` — there's no tag to reference "the preceding instruction's own success" by. New
  reserved readable-form sentinel `EN := ENO`, mirroring the existing `TRUE` sentinel precedent.
- `Mul`'s own type is `<AutomaticTyped Name="SrcType" />` — self-closing, no value (TIA infers the
  type from the connected operands), modeled as `PartNode.AutomaticSrcType: bool`. `Convert`
  carries an ordinary `SrcType`/`DestType` `TemplateValue` pair, converting *between* two types.
- 14 new converter tests, two fixtures genericized from the real grounded shapes. All three
  suites green: 191 converter (up from 177), 68 openness-cli, 11 golden-harness.
- **Live-verified:** both the ENO-chained case (`MotorDOL`) and the standalone rail-fed case
  (`ShredderControlSystem`) reduce and round-trip correctly. Whole-block `to-ir` sweep of all 5
  previously-`Mul`/`Convert`-blocked dependency FBs confirmed none hit that error anymore — all
  five now hit `TONR` instead, confirming it as real (previously only a name noticed during an
  earlier sweep). Arithmetic beyond `Mul`/`Convert` stays out of scope; nothing observed needing
  it. Full story: `docs/notes/stage-gates.md` ("S1 item 18").

**S1 items 16/17: network- and block-level `Title` — `PlantAutoControl` now fully round-trips**

- Picked up immediately after `SCoil`/`RCoil` surfaced the gap. Found a real design correction
  needed first, not just a new field: the IR's own `NETWORK <n> "<title>"` line was actually
  sourced from the network's `Comment` field, not `Title` — invisible until now since every real
  network grounded this session had empty Comment; `PlantAutoControl` has the opposite (Title
  populated, Comment empty everywhere). Confirmed the fix with the project owner first
  (`AskUserQuestion`, not assumed): `NETWORK`'s own label now carries `Title`; `Comment` gets its
  own new, separate `COMMENT "..."` line.
- `SimaticMl.CompileUnitSource`/`BlockSource` and `Ir.IrNetwork`/`IrBlock` all gained `Title`/
  `Comment` fields as appropriate. `DbSourceWriter.WriteComment` generalized to
  `WriteMultilingualText(text, compositionName, ...)` once a second real `CompositionName`
  confirmed the shape is shared. New optional `TITLE "..."`/`COMMENT "..."` IR lines at both
  block and network level, surviving an `[empty]`-marked network too.
- Block-level Title was unexpected: initially assumed always-empty (network-level grounding only
  found it populated), confirmed real while grounding `PlantAutoControl`'s own 8 dependency FBs —
  `MotorVSDSystem`/`AirStar` both carry a real, identical block-level Title ("VSD Motor").
- Sanitization: both Titles are identifying free text (equipment/process names) — same
  hard-error-if-unmapped treatment as Comment, via two new `SanitizationMap` dictionaries
  (`NetworkTitles`, `Titles`).
- 13 new converter tests. All three suites green: 177 converter (up from 164), 68 openness-cli,
  11 golden-harness.
- **Live-verified, same session:** `converter to-ir` on a fresh whole-block `PlantAutoControl` export
  now succeeds completely — the first real production block this whole session to fully convert as
  a whole block — and `to-ir → to-xml → to-ir` round-trips byte-identical.
- **Attempted the true TIA cycle, per the project owner's explicit instruction to route through
  `SampleProject` rather than `JOB9002`.** Import succeeded (TIA accepted the regenerated XML into
  a completely unrelated project); compile failed with 502 errors, entirely missing tags (323
  distinct paths) and missing FB library blocks (8 dependency FBs) — `SampleProject` has neither.
  Not a converter defect — a block doesn't carry its project dependencies with it. Left the
  imported-but-uncompiled block in `SampleProject` for manual cleanup, as agreed.
- Grounded the 8 dependency FBs directly afterward: 0 of 8 convert cleanly today — 5 blocked by
  arithmetic (`Mul`/`Convert`), the rest by the same already-known FC/FB parameter-interface gap.
  Project owner's decision: scope arithmetic support next. Full story: `docs/notes/stage-gates.md`
  ("S1 items 16/17").

**S1 item 15: `SCoil`/`RCoil` (set/reset coils) — built, tested, surfaced a new whole-block gap**

- Picked up per the project owner's own sequencing after `CALL`: 3 of each real in
  `FC PlantAutoControl`, also seen alongside TON in `FB MotorDOL`'s own earlier grounding. Grounded
  against two independent real instances of each before any code — both completely bare
  (`<Part Name="SCoil"/"RCoil" UId="N" />`), exact same `in`/`operand` wire shape as a plain
  `Coil`, never a producer. Structurally identical to `Coil` in every respect.
- Smallest diff of any S1 item this session: `GraphReducer.ReduceOneChain`/
  `FlgNetBuilder.BuildOneChain` reused verbatim for all three kinds. Only addition: a new
  `CoilAssignment.Kind` field (`Assign`/`Set`/`Reset`), mirroring `CompareStep`'s own
  `PartName`↔`Operator` split. `CoilAssignmentSidecar` needed no new field — `BuildOneChain`
  already takes the model alongside its sidecar, so `Kind` is derived directly.
- Readable-form keywords `SCOIL`/`RCOIL` chosen to match every other keyword's own
  mirror-the-source-Part-Name convention (`WAND` is the one exception, for a collision that
  doesn't apply here).
- 6 new converter tests, one fixture interleaving all three kinds on a shared rail — passed on
  first run. All three suites green: 164 converter (up from 158), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated the real network already grounded for `CALL` (it also
  has the block's own `SCoil`/`RCoil` pair) — reduces and round-trips completely.
- **Then attempted a whole-block round-trip of `PlantAutoControl`**, since this should have closed its
  last instruction-level gap. Hit a different, already-known wall instead: every one of its 20
  real networks carries a non-empty `Title` (distinct from `Comment`), which `BlockSourceParser`
  already hard-errors on (documented earlier, `tests/golden/README.md` — not a new discovery,
  just newly encountered on this specific real block). `PlantAutoControl` has no remaining
  instruction-level gap but still doesn't round-trip as a whole block. Full story:
  `docs/notes/stage-gates.md` ("S1 item 15").

**S1 item 14: `CALL` (FB/FC block calls) — built, tested, live-verified in-memory with `Not`**

- Picked up per the project owner's own decision at the close of S1 item 13: build block calls
  next, since `Not` and `<Call>` co-occur in every one of `FC PlantAutoControl`'s 20 real networks.
  Planned with plan-mode rigor first, including a Phase 0 grounding pass before any design was
  finalized (no `<Call>` XML had actually been inspected before this item).
- Grounding found `<Call>` genuinely isn't a `<Part Name="Call">` — it's its own sibling element
  under `<Parts>` (`<Call UId="N"><CallInfo Name="..." BlockType="FB"><Instance/><Parameter/>...
  </CallInfo></Call>`). `FlgNetParser`/`FlgNetWriter` adapt this into/from an ordinary
  `PartNode(Name="Call")` so the rest of the pipeline never needs a parallel type.
- Also found: arguments are sparse, not a full interface snapshot — 19 of 20 real calls have
  zero wired parameters at all (not present-but-empty, simply absent); the one wired example has
  10 (8 Input, 2 Output). Instance reuses the exact same shape as TON's own `<Instance>`,
  confirmed identical — the reuse `ir/SPEC.md`'s TON section had already anticipated.
- Design mirrors Move/WAND (`en` via the same `TraceChain` fan-out mechanism, all 20 real
  instances directly rail-fed) crossed with TON's own Instance reference (factored
  `ParseInstanceReference` out of `ParseTon` for reuse). Readable-form syntax:
  `CALL BlockName(Instance, EN := <expr>, Param := <expr>, ... => OutParam, ...)` — `EN` always
  shown explicitly, matching MOVE/WAND's own convention.
- 14 new converter tests, two fixtures genericized from the two real shapes found — passed on
  first run. All three suites green: 158 converter (up from 144), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated `FC PlantAutoControl`'s richest real network (`Not`
  wrapping an OR-of-comparisons, 8 independent Contact→Coil rungs, and the fully-wired
  10-parameter call, all sharing one rail wire) — reduces and round-trips completely through the
  in-memory pipeline, the first real network combining `Not` and `Call` together to do so. Still
  not verified through the true TIA `import → compile → re-export → Normalizer` cycle:
  `openness-cli import` has no per-network granularity, and `PlantAutoControl` as a whole still has
  `SCoil`/`RCoil` elsewhere. Per the project owner's own sequencing, `SCoil`/`RCoil` is next.
  Full story: `docs/notes/stage-gates.md` ("S1 item 14").

**S1 item 13: `Not` (standalone boolean inverter) — built, tested, gold-standard gap documented**

- Investigated whether the converter as built could handle `FC PlantAutoControl` (a real, complex
  orchestrator block); its very first network hits `Not` first. Grounded twice, independently,
  against two different real instances in that block before writing any code — identical bare
  shape both times (`in`/`out` only, no operand/Access, no TemplateValue).
- Design: no new top-level production — `Not` is purely a new chain-position kind, discovered
  only when some other production's own `TraceChain` walk hits one. Resolved via the same
  "chain-terminal via recursive `TraceChain`" pattern OR-merge branches established (S1 item 11):
  a fully self-contained recursive call on `Not`'s own `in`, wrapped in the already-existing
  `Expr.Not`. `ChainStepSidecar.NotStep` mirrors `OrBranch`'s `(Steps, RailWireUId)` shape. Zero
  new IR-text grammar needed; `PartNode`/`FlgNetParser`/`FlgNetWriter` needed zero new fields or
  code — falls through to existing generic bare-part handling both ways.
- Both real instances tap a shared wire via genuine fan-out (same mechanism proven for Move's
  `en` tap, S1 item 10), here feeding back into a boolean chain instead of a side-effect write.
- 6 new converter tests, fixture built directly from the real `PlantAutoControl` shape (genericized) —
  passed on first run. All three suites green: 144 converter (up from 138), 68 openness-cli, 11
  golden-harness.
- **Mid-session course-correction from the project owner:** "the gold standard is a lossless full
  cycle" — flagged that every prior "live-verified" claim (items 10–12) was only ever the
  in-memory pipeline, never the true TIA `import → compile → re-export → Normalizer` cycle.
  Investigated whether `Not` could reach that bar: no viable reference-project block to extend
  without either overwriting committed content or requiring new-block authorship (the project
  owner's own TIA-UI action, per the `TimerSample` precedent); a full sweep of all 20
  `PlantAutoControl` networks found **every one pairs `Not` with a `<Call>`** (block calls, not yet
  built) — no real network can be isolated to prove `Not` alone through the full cycle yet.
  Reported honestly rather than settling silently; project owner chose to build block calls next
  to close this out for both constructs together. Full story: `docs/notes/stage-gates.md`
  ("S1 item 13").

**S1 item 12: WAND (bitwise word AND) — corrects the AND-merge premise, live-verified**

- Project owner picked "AND-merge" as the next S1 item. Grounded first, per hard rule 3: searched
  28 real LAD blocks specifically for a boolean parallel-branch AND-merge (an `O`-sibling) —
  **found none anywhere.** Architecturally expected in hindsight: boolean AND in ladder logic is
  always plain series Contacts, never needing an explicit merge Part the way OR genuinely does.
- What the sweep found real instead: `Part Name="And"` (`FB VSDUpdateComs`,
  `Word AND 16#89 -> ControlWord`) — an entirely different thing, a bitwise/word-level box
  instruction, not a boolean chain position. Reported to the project owner before building
  anything (corrects the task's own premise, not just an implementation detail) — chose to build
  the real instruction instead of the speculated one.
- Design mirrors `Move` closely (same `TraceChain` fan-out-tap mechanism for `en`) crossed with
  `O`'s own `Cardinality`-driven shape (here driving input count, `IN1`..`INn`, not branch count)
  and a comparison's own `SrcType`. `PartNode` needed zero new fields — `Cardinality`/`SrcType`
  already existed, just never co-occurring on one Part before; `ParseCardinality`/`ParseSrcType`
  (renamed from `ParseOrCardinality`/`ParseComparisonSrcType`) needed a real fix to look their
  `TemplateValue` up by `Name` rather than assuming it's the only one present.
- IR keyword is `WAND`, not `AND` — deliberately avoids colliding with the boolean `AND` infix
  operator, a converter-owned vendor-neutral name mapping (same precedent as `MOVE_BLK_VARIANT` →
  `MOVE`).
- Real, previously-unexercised parser gap found along the way: Siemens' `<base>#<value>` numeric
  literal notation (`16#89`) wasn't recognized by `ParseLeaf`'s shape-based literal detection —
  fixed generically (not hardcoded to base 16).
- 8 new converter tests, fixture built directly from the real `VSDUpdateComs` shape (genericized)
  — passed on first run. All three suites green: 138 converter (up from 130), 68 openness-cli, 11
  golden-harness.
- **Live-verified, same session:** isolated `FB VSDUpdateComs`'s real network — reduces and
  round-trips completely, including the `16#89` literal round-tripping cleanly through the text
  grammar. Full story: `docs/notes/stage-gates.md` ("S1 item 12").

**S1 item 11: OR-merge branches generalized to recursive chains, live-verified**

- Closed the gap items 9 and 10 both left open: `GraphReducer.ResolveOrMerge` required every
  branch to be a single Contact fed directly by Powerrail — real data hit this twice
  (`ControlDelays`' `O(41)` combining two comparisons fed by a further OR-merge; `MotorDOL`'s
  `O(45)` fed by a shared, non-rail-fed Contact). Planned via `/plan`, approved before any code.
- Design decision confirmed with the project owner first (`AskUserQuestion`): once a branch can
  be a compound expression, the IR text needs real operator precedence — `AND` binds tighter than
  `OR`, parens only where precedence alone would misparse.
- The fix: a branch is resolved via the exact same `TraceChain` mechanism as a Coil's condition/a
  TON's `IN`/a Move's `en` — recursion, not a new algorithm. `ResolveOrMerge` shrank from a
  hand-rolled single-hop walker (three separate hard-error checks) to a thin per-branch loop.
  `ChainStepSidecar.OrStep.Branches` changed from a flat `ContactStep` list to `OrBranch`
  (Steps + nullable RailWireUId, mirroring every other production's own chain shape); the outer
  chain's own `RailWireUId` is now `null` whenever it terminates at an `OrStep` (mirrors the
  existing `TimerOutputStep` precedent). `FlgNetBuilder` needed no new accumulation mechanism —
  the MOVE-era shared, de-duplicating endpoint accumulator already handles it.
- `IrParser.ParseExpr` was a real, previously-unexercised bug (naive `.Contains(" AND ")` checked
  before `.Contains(" OR ")` regardless of actual precedence) — rewritten as a proper
  precedence-climbing recursive descent parser; `IrSerializer.SerializeExpr` became
  precedence-aware to match.
- Repurposed 3 existing hard-error fixtures/tests into positive ones (`NestedOrMerge.xml`,
  `OrMergeMultiContactBranch.xml`, `OrMergeOfComparisons.xml`) rather than inventing new test
  data — same pattern already used twice this project. One new fixture
  (`OrMergeSharedPrefixBranches.xml`, the one real shape none of the three already covered) —
  **passed on first run**. 14 new standalone grammar tests, also all passing on first run,
  including a deeply-nested mixed-precedence case. All three suites green: 130 converter tests
  (up from 106), 68 openness-cli, 11 golden-harness.
- **Live-verified, same session:** isolated both real networks that were previously blocked
  exactly at this OR-merge limitation (`ControlDelays`' `CompileUnit "8"`, `MotorDOL`'s "HMI Motor
  Status Telemetry" network) via fresh exports and a throwaway test (real data deleted after use).
  **Both now reduce and round-trip completely.** `ControlDelays`' Coil condition turned out
  deeply nested (`Or` of two `And`s, each containing a nested `Or`) — confirms the precedence
  grammar renders genuinely real, richer-than-any-fixture expressions correctly. `MotorDOL`'s
  telemetry network now reduces all 5 Moves (previously only 3 of 5 got past `Reduce()` before
  the OR-merge blocked the rest). Full story: `docs/notes/stage-gates.md` ("S1 item 11" + its live
  verification section).

## 2026-07-11

**S1 item 10: MOVE support, live-verified (uncommitted)**

- Added `Part Name="Move"` support, grounded against a real export (`FB MotorDOL`'s "HMI Motor
  Status Telemetry" network) before building anything. Real finding: a Move is neither a boolean
  chain position (like Eq/Ge) nor a self-contained production like TON — it's a side effect
  *tapped off* a chain position's own output via genuine wire fan-out (the same wire feeds both
  the Move's `en` and the chain's real continuation).
- Core `GraphReducer.TraceChain` redesign: the fan-out check changed from "exactly one other
  endpoint on a wire, else throw" to "find the single genuine producer endpoint, ignore every
  other endpoint" — needed because every prior capability assumed exactly two endpoints per wire,
  which a Move's tap genuinely breaks.
- `FlgNetBuilder` rebuilt around a shared, de-duplicating endpoint accumulator (`AddPart`/
  `AddEndpoint`, keyed by UId) — needed because multiple Moves' own `en` chains telescope through
  the same upstream Contacts a Coil's (or another Move's) chain already walked, and the reducer
  deliberately re-derives duplicate chain-step data per production (correct, not a bug); only the
  builder's de-duplication prevents that from becoming duplicate XML on rebuild.
- Readable-form syntax: `MOVE(EN := <expr>, IN := <expr>) => <dest>`.
- 12 new converter tests (`MoveTests.cs`), including a fixture built from the real telescoping/
  fan-out shape (genericized) — **passed on its first run**, proving the redesign against the
  exact shape it was built for. 106 converter tests pass (up from 94); `openness-cli`/
  golden-harness suites unaffected, not re-run this pass.
- **Live-verified, same session:** isolated the real "HMI Motor Status Telemetry" network from a
  fresh `FB MotorDOL` export (throwaway test, real data deleted after use). The real topology is
  richer than any fixture — one Contact's outgoing wire fans out to **three** consumers, not two
  — and parsing/fan-out-producer-identification handled it correctly. `Reduce()` failed on the
  whole network, but for a pre-existing, already-deferred reason unrelated to MOVE (a
  multi-contact OR-merge branch not fed directly from Powerrail — same class of gap already known
  from comparisons); confirmed via Part document order that 3 of the network's 5 real Moves
  (including one tapping the genuine 3-way fan-out) reduced successfully before the throw. A
  fully self-contained real sub-network (`Contact47 → Move48`, one necessary rail-wire trim,
  otherwise byte-identical to the export) round-tripped cleanly end to end.
- Docs updated: `ir/SPEC.md` (readable-form entry), `src/converter/README.md` (new section, plus
  fixed a stale scope line that hadn't been updated since before TON/comparisons landed),
  `docs/notes/stage-gates.md` (S1 item 10 + live-verification sections, refreshed summary table
  row).

**S1 item 9: comparisons (Eq/Ge)**

- Added `Part Name="Eq"`/`"Ge"` support, grounded against a real export (`FC ControlDelays`)
  before building anything. Real finding: a comparison behaves like a `Contact` (a pass-through
  chain position with its own rail-facing/continuation port, `pre`), not a terminal leaf like an
  OR-merge or TON — `GraphReducer.TraceChain`'s upstream dispatch now varies both the "out"- and
  "in"-equivalent port names by part kind (previously only the "out" side varied, since TON).
- New top-level `Access Scope="LiteralConstant"` (a comparison's literal operand, e.g. `1`) —
  always carries a `<ConstantType>`, the mirror image of TON's `TypedConstant` (never has one).
  `ConstantAccessNode`/`SidecarConstantEntry` gained a nullable `ConstantType` field;
  `Expr.TimeLiteral` generalized to `Expr.Literal` (both literal kinds render identically in text).
- Scope decision made during implementation: `ControlDelays`' own network composes two
  comparisons via a second OR-merge, each fed by a first OR-merge rather than Powerrail directly.
  Rather than build a larger, riskier OR-merge-branches-as-recursive-chains generalization this
  session, confirmed the *existing* OR-merge branch/fan-out checks already safely refuse this
  shape without new code — verified with a dedicated test against the real shape, not assumed.
  Same deferred status as the already-known multi-contact-OR-branch/nested-OR-merge cases.
- 11 new converter tests, 1 repurposed (`LiteralConstant` was the old "unrecognized scope"
  example — now real, moved to a scope name that's still genuinely unsupported). 94 converter /
  68 openness-cli / 11 golden-harness tests all pass.

**S1 item 9 continued: live re-verification against real data**

- Cleared 3 stale TIA Portal processes (project owner's go-ahead) and confirmed a fresh single
  instance launches fine — settling at 3 processes again turned out to be normal for this
  machine, not itself the earlier problem.
- Fresh `export`/`to-ir` of `FC ControlDelays` reproduced the predicted result exactly: hard-errors
  on `Mul` in Network 1 (unrelated to Eq/Ge, which live entirely in Networks 2–4) before ever
  reaching the comparison content — a real, honest caveat (Networks 2–4 weren't exercised by that
  run) rather than declaring victory early.
- Isolated Network 2 directly (`FlgNetParser` → `GraphReducer` → `FlgNetBuilder` → `FlgNetWriter`,
  a throwaway test against the real extracted XML, deleted after) and got a precise, better-than-
  expected result: `Eq(32) → Contact(33) → TON(34).IN` reduced successfully against genuinely
  live, unmodified data; the Coil's own chain (needing `O(41)`, the OR-merge of two comparisons)
  threw exactly the predicted, already-tested error, confirming the OR-merge-composition scope
  decision is correct on the exact real network that motivated it.

**S1 item 8, closed out: TON live round trip, direct-`Q`-wiring support, a second Normalizer fix**

- Project owner built `FC TimerSample`/`DB_Timers` directly in the reference project
  specifically to close out TON — a TON-only block, free of the comparisons/Move/RCoil that
  blocked every prior grounding example from a full live round trip. Confirmed a second real
  `Instance Scope="GlobalVariable"` shape along the way: a two-component path
  (`DB_Timers.SampleTimerN`), needing no code change.
- Built `ChainStepSidecar.TimerOutputStep`: a TON's `Q` wired *directly* into a downstream `Coil`
  (no ordinary Access in between) — the one shape left unmodeled after `FB MotorDOL`'s only
  example fed an out-of-scope `RCoil`. Always chain-terminal, like an OR-merge, except it never
  touches Powerrail — `RailWireUId` is now nullable (`none` in the IR sidecar) for this case.
- Live round trip passed: `export → to-ir → to-xml → import → compile → re-export →
  Normalizer.AreSemanticallyEquivalent` — **true**, first full live proof for TON (3 networks,
  including chained timers — one network's `IN` reads back two other TONs' `Q`, exercised for
  free). Surfaced and fixed a real, second golden-harness gap: TIA reassigns `Access` element
  UIds on its own import/compile cycle too (parallel to the already-known Wire UId volatility) —
  `Normalizer` now resolves Access/IdentCon references by content, scoped per network (a
  document-wide map would silently collide entries across networks, since UId numbering restarts
  per network — caught live by this exact 3-network test).
- Net +3 converter tests versus the entry below (83 total: repurposed an obsolete hard-error test
  into a positive one, added 4 new). `tests/golden`'s `NormalizerTests` unchanged (11, all still
  pass) plus the live proof itself.
- Committed `TimerSample`/`DB_Timers` to the reference corpus (6th/7th artifacts) — re-verified
  against the exact committed files before committing, not assumed from the earlier scratchpad run.

**S1 item 8: TON support, both instance scopes**

- Added `Part Name="TON"` support, grounded against two real exports at the user's request to
  confirm both instance scopes before building anything: `FB MotorDOL` (`Instance
  Scope="LocalVariable"`, multi-instance in the calling FB's own iDB) and `FC ControlDelays`
  (`Instance Scope="GlobalVariable"`, a standalone instance DB named directly). Both reuse the
  existing `AccessNode` model rather than a new type, so a future FC/FB call's own instance
  argument can reuse it too.
- IR design, agreed with the user before coding: no bound name (`ir/SPEC.md`'s original `timer :=
  TON(...)` sketch) — TIA has no timer-name concept to draw from, so a TON statement is
  `TON(<instance path>, IN := <expr>, PT := <expr>)`, and later reads of its output reuse the
  instance's own dotted path as a plain tag reference (e.g. `GeneralEnableDelay.Q`) — not a new
  IR construct, since that's exactly how the source itself reads a standalone TON's output back.
- New `Access Scope="TypedConstant"` for a literal-fed `PT` (`T#100MS`); `Q`/`ET` confirmed valid
  both entirely unwired and `OpenCon`-wired — a `Q`/`ET` wired directly to a downstream part is a
  real shape (`FB MotorDOL`, feeding an out-of-scope `RCoil`) but not modeled, since it can't be
  proven end to end regardless of how much is built for it.
- Fixed a real, pre-existing bug found while grounding PT-as-tag: `FlgNetBuilder` rebuilt every
  ordinary tag Access as hardcoded `GlobalVariable`, ignoring the real source scope — harmless
  until now (every tag seen before was GlobalVariable), now fixed to carry the real scope.
- 12 new converter tests, 1 obsolete one removed; 80 converter / 68 openness-cli / 11
  golden-harness tests all pass. Not yet live-round-trip-proven at the block level — both
  grounding blocks also use comparisons/Move, a separate unbuilt capability, and `to-ir` requires
  a whole block in scope at once.

**S1 item 7 Phase A: OR-merge + negated contacts, live-proven in the reference project**

- Added `Expr.Not` to the IR (`NOT <tag>` notation) and generalized `GraphReducer`'s backward
  trace to recognize `Part Name="O"` (OR-merge) as a valid rail-facing chain position — grounded
  against two fresh real exports (`PerimeterSafetyAlarms`: 3-way OR of negated contacts; `GeneralAlarms`:
  33-way OR of plain contacts), both confirming every OR-merge branch is exactly one Contact
  (optionally negated via `<Negated Name="operand" />`) fed directly by the shared rail wire.
  Rebuilt `CoilAssignmentSidecar`'s flat contact/wire lists as a recursive `ChainStepSidecar`
  (`ContactStep`/`OrStep`) to represent a chain position that fans out. A multi-contact OR branch
  or a nested OR-merge — both real but unconfirmed — hard-error rather than guess.
- Live proof: `PerimeterSafetyAlarms` (8 independent rungs — the 3-way negated OR plus 7 more
  single-contact rungs, 4 negated) taken through `sanitize → to-ir → to-xml → import →
  block-compile → re-export → Normalizer.AreSemanticallyEquivalent` — **true**, the first real
  confirmation that OR-merge/negated-contact SimaticML output actually imports and compiles in
  TIA. Committed as `PerimeterSafetyAlarms`, the reference project's second FC block — every tag
  it needed was already in the sanitization map from the earlier 12-block search that found this
  gap in the first place.
- 10 new converter tests (45 total), all three suites still green.

**S1 item 7 Phase B: Instance DB + one-level structured members**

- Added `SW.Blocks.InstanceDB` support (`InstanceOfName`/`InstanceOfType`, the latter regenerated
  as a constant rather than carried as an IR field) and one-level structured-member round-trip
  for both Global and Instance DBs — UDT-typed (e.g. `"TypeDOL"`) and system-function-block
  instance-typed (e.g. `TON_TIME`, `PT`/`ET`/`IN`/`Q`) members are inlined directly in the IR
  rather than referenced by name, a deliberate call (`ir/SPEC.md` "Structured members") made
  because there's no UDT/`PlcType` export capability yet to safely derive a canonical
  reference-by-name shape from. Grounded against a real `ConveyorMotor1` instance DB (`FB MotorDOL`).
- New source files `DbMemberLineFormat.cs`/`DbInterfaceMembers.cs`; new fixtures for
  Instance DBs with structured members, UDT-typed and doubly-nested members (hard-error case),
  non-`None` nested sections (hard-error case), and non-empty FB interfaces; new
  `BlockInterfaceTests.cs` plus 263 new lines in `DbConverterTests.cs`.
- Doubly-nested structured members and non-`None` nested sections remain hard errors
  (real-but-unconfirmed shapes).
- Verified 2026-07-11 (during a docs audit that flagged this phase as undocumented): 68 converter
  tests (up from 45), 68 openness-cli tests, 11 golden-harness tests, all green.

## 2026-07-10

**Seed the reference project: sanitizer, DB round-trip support, auto project-switching**

- Added `converter sanitize` — a new subcommand that renames every identifying value in a
  parsed block/DB via a hand-authored, never-committed mapping file (`docs/13-data-boundary.md`),
  hard-erroring on anything left unmapped. Reuses the existing parse/write pipeline; the
  transform is just a rename pass on the parsed model.
- Seeded S1's purpose-built Green reference project (`ir/reference/`, `simatic-ml/reference/`) —
  didn't exist before this; everything proven so far ran against `JOB9002`, explicitly barred from
  becoming the committed corpus. One FC (`NodeStatusAlarms`) plus three complete DBs
  (`CommsProcessData`/11 members, `AlarmWords`/4, `EquipmentStatus`/47) are now committed, each
  proven through a real `export → sanitize → to-ir → to-xml → import → compile → re-export`
  cycle ending in `Normalizer.AreSemanticallyEquivalent` — the actual Layer 1 losslessness
  assertion, not a manual spot-check — returning true.
- Built real DB round-trip support (`DbSourceParser`/`DbSourceWriter`/DB IR format) — the two
  DBs the FC depends on were hand-made placeholders until now. Found and fixed three real bugs
  along the way: `Member`/`AttributeList`/`StartValue` XML-namespace inheritance silently
  dropping every attribute on parse and write; a structural-section scan that wrongly recursed
  into a structured member's own nested content; and a blanket `Normalizer` strip rule that
  would have made every DB round-trip trivially pass without comparing real member content —
  correct for code blocks, wrong for DBs, now a structural check instead of a name match.
- Fixed a second real bug independent of DB work: `openness-cli compile`'s diagnostic messages
  were silently incomplete (a nested Siemens API message tree only ever read one level deep) —
  every past compile failure this session showed a bare error count with no explanation.
- Added automatic TIA project-switching to `openness-cli` — every session up to this point
  required manually closing whichever project was open before touching a different one; now it
  saves and closes automatically (`Project.Close()`/`Save()`, confirmed to leave Portal itself
  running).
- Searched 12 real blocks for a second FC to bring into the reference project; none fit without
  either an unsupported instruction (OR-merge, comparisons) or a cascade of new Instance-DB
  dependencies — a real finding about this codebase's converter-scope gaps, not a dead end.

**Fix converter data-loss bugs found via live TIA testing; add block-level compile support** (`d3cb494`)

- Fixed: array-subscript addressing (`Node_Error[1]`, `[2]`, `[3]`) was silently dropped by the
  converter, collapsing distinct array elements into one ambiguous tag path in the IR — a real
  data-loss bug, caught by the project owner reviewing round-tripped LAD directly in TIA, not by
  a test. This was also the root cause of a `FlgNetBuilder` crash hit earlier in the same testing
  session (two genuinely different `<Access>` elements colliding on one ambiguous tag path).
  Ground truth for the fix was pulled from a real, untouched sibling block rather than guessed.
- Resolved: the project's `IsConsistent = false` after `Import()` mystery. `PlcBlock` exposes its
  own `ICompilable` service — confirmed live, not documented anywhere — distinct from the
  whole-device compile `openness-cli` already used. Device-level compile reports `Success` but
  never clears a block's `IsConsistent` flag after import; block-level compile does. Added
  `openness-cli compile --block <name>`. Used it to clear every remaining inconsistent block in
  the scratch project, including ones that predate this session entirely — same root cause.
  Project now reports `OVERALL: HEALTHY, 0/180 blocks inconsistent` for the first time.
- Stage 6 of the S1 walking skeleton (`SimaticML → IR → SimaticML'`, re-exported and compared
  against the original) reached end-to-end for the first time, on real production LAD data.

**Build S1 walking skeleton: converter, export/import/compile, sanity-check** (`36cbbcd`)

- Added the SimaticML↔IR converter (`src/converter/`, C# per ADR-0002), `openness-cli`
  `export`/`import`/`compile`, and golden-harness machinery (`tests/golden/`) for a
  Contact/Coil-only slice. Live-verified end-to-end against real project data.
- Found and fixed four real LAD patterns along the way (not guessed at): multiple independent
  rungs per network, a shared multi-endpoint rail wire, empty placeholder networks, and
  slice-access (bit-within-word) alarm addressing. Found three real `Import()` requirements the
  same way: a root block ID, unique comment-wrapper IDs, a required `Namespace` element.
- Added `openness-cli sanity-check` after the final re-export was refused with "inconsistent
  block" — diagnoses per-block `IsConsistent` + every device's compile state directly.

**Accept ADR-0001 and write `ir/SPEC.md` v1** (`206233e`)

- Decided the IR's concrete syntax against real S7-1200 G2 SimaticML structure: networks are
  wiring graphs, not flat rungs, so the IR uses a readable-expression form for the reducible
  common case with an explicit node/wire fallback otherwise. Block calls are reference-only;
  stateful instructions carry their instance inline; DBs/UDTs get a tabular sub-format.

**Implement `openness-cli list` and close out S0 exit criteria** (`0dcf0f7`)

- Added the first Openness touchpoint: `list` attaches to (or launches) TIA Portal, opens a
  project by name or path, and enumerates every block with a structural, never-open safety filter
  (`ProgrammingLanguage` `F_`-prefix). Targets `net48`, not `net8.0-windows` (`Siemens.Engineering.dll`
  needs a .NET-Framework-only `Assembly.Load` overload). All four S0 exit-criteria items evidenced.

**Repo Skeleton + Design Doc Suite** (`52731d2`)

- Initial commit: the full pre-design document suite (`docs/`), `CLAUDE.md`, repo skeleton.
