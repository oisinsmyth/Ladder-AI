# Future-ideas triage — buildable, coding/tooling only

> **STALE AS A RANKING — RE-RUN BEFORE USING IT. Landed 2026-08-09 for the method, not the order.**
> This ranks **FI-01…FI-47**. `docs/16-future-ideas.md` is now at **FI-62**: fifteen entries were added
> after this was written (FI-48…FI-62), and several of the items ranked below have since been built —
> FI-50, FI-51, FI-52, FI-53 among them. So the *ordering* is a snapshot of a repo that has moved, and
> reading it as current would be the same mistake this repo keeps finding in its own documents: a page
> that reads authoritative and is not.
>
> What survives and is worth keeping: the **filter** (actionable today + coding/tooling only, excluding
> prose, skills, patterns and governance), the **inputs checked before ranking**, and the
> revision-1-vs-2 delta section, which shows how to make a re-run auditable instead of silently
> replacing the previous order. Re-running against current `16-future-ideas.md` is cheap; trusting this
> order is not.

**First pass 2026-08-06 (base `fbe8a6b`). Re-run 2026-08-07 against `436cf00`** — 12 commits later.
Revision 2 is the live ranking; the deltas from revision 1 are recorded in the next section so the
movement is auditable rather than silent.

Scope of this pass: every entry in `docs/16-future-ideas.md` (FI-01…FI-47), filtered to items that
are **(a) actionable today** — no external gate, no owner ruling, no stage that hasn't opened — and
**(b) coding or tooling work** — converter/openness-cli/test code, not prose, skills, patterns or
governance. Ordered by impact.

This is a reading of the doc, not a new decision: every item below is already `Open`/`Raised` in its
own entry. Nothing here is committed to the roadmap; the ordering is the contribution.

Checked before ranking (re-run): `AITODO.md`, `agent-tasks/` (empty), `docs/notes/owner-questions.md`
(13 open `decide` items, none gating anything below), and `git log fbe8a6b..master`.

## What changed between revision 1 and revision 2

Four input changes and two judgement changes. The input changes are the substantive ones — the first
pass was taken against a doc and a rule base that have both moved.

**Input changes (the repo moved under the first ranking):**

1. **FI-47 is new** (`3b56ca5`, 2026-08-06) — ` RETAIN` on a UDT member parses clean and is silently
   dropped crossing to XML. It enters the list at **#1**. Reasoning under the entry below.
2. **C-501 was redefined, and the first pass repeated a stale premise.** The rule is now *one network
   per alarm **word*** — all bits of a word written in one network as slice coils, with the alarm text
   in the **network comment bit map**, no longer in the network title. Revision 1 justified FI-35's
   low cost by quoting the FI-35 entry's "one alarm bit per network, and the network title *is* the
   alarm text" — which was already superseded at the base commit (`fbe8a6b` is itself the C-501
   change) and is now further refined at master (`8356648`/`cead867`: the C-504 suppressor term is
   permitted, and the mechanised checker was flagging compliant code while passing the superseded
   form). **FI-35 drops to #11 and is flagged entry-stale.**
3. **Three new conventions landed unmechanized** — C-131 (feedback proves non-arrival), C-132 (an
   FB's whole interface is one STATIC UDT member), C-133 (a momentary event holds for a fixed time).
   `ReviewRunner.AllRuleIds` is still exactly 18, so FI-40's headline assertion is unchanged and
   still true. C-132 **cites FI-47 by name** as the reason retention must live on the FB static —
   which raises FI-47's frequency, not just its severity (see #1).
4. **A converter capability gap was fixed directly, never via this doc** (`436cf00`, multi-instance
   FB calls). It moves nothing ranked, but it is the same family as FI-42/FI-43: build-side gaps that
   only appear when the tooling is used to author rather than to read. Third instance in three days.

**Judgement changes on re-reading (independent of the above):**

5. **FI-46 (1) rises above FI-39 (1)** — swapping revision 1's #3 and #4. Revised C-501 makes the
   slice-access coil the *mandated* shape for every alarm word, so `DB.SomeWord.%X0` is no longer an
   occasional path — it is how every alarm bit is now written. FI-46 (1)'s false `MEMBER-NOT-FOUND`
   on the hard-rule-3 gate therefore fires on the shape the rule base now requires. It is also XS
   against FI-39 (1)'s M.
6. **FI-35 drops from #8 to #11**, per input change 2 — and with a build-order caveat it did not
   carry before.

**Unchanged:** everything else holds its relative position, and every ranked defect was re-verified
present at `436cf00` rather than assumed (FI-41's vacuous pass at `ReviewRunner.cs:58`; FI-42's two
refusals at `DbInterfaceMembers.cs:205,309`; FI-47's own documented-but-unenforced comment at
`TypeIr.cs:37`; no `%X` handling anywhere in `TagStatus/`).

## The ranking

| # | Item | What it is | Size | Δ |
|---|------|-----------|------|---|
| 1 | **FI-47** | ` RETAIN` in a `TYPE` member parses clean, then vanishes into the XML | S | **new** |
| 2 | **FI-40** | Mechanical status check — a test asserting the derivable facts docs claim | S | ▼1 |
| 3 | **FI-41** | `converter review` TAGTABLE rules — C-001/C-005 currently pass vacuously | S | ▼1 |
| 4 | **FI-46 (1)+(2)** | Member-resolution residuals: bit-slice false positive; unaudited member walks | XS + ? | ▲1 |
| 5 | **FI-39 (1)** | `relation-reconcile` citation check rewards a *vaguer* citation | M | ▼2 |
| 6 | **FI-42 (1)+(2)** | `to-ir` refuses UDT-typed arrays and DTL `Version` — blocks IR round-trip | M | ▼1 |
| 7 | **FI-43** | `openness-cli delete --type` and `import --overwrite` | S | ▼1 |
| 8 | **FI-39 (2)** | `signal-sweep` token regex excludes a real `/` member name | XS | ▼1 |
| 9 | **FI-42 (3)** | IR cannot express block access (Standard/Optimized) or `MemoryReserve` | L | — |
| 10 | **FI-46 (3)** | `ParseRender`'s `instance:` regex is too loose | S, gated | — |
| 11 | **FI-35** | `converter alarm-scan` — extraction half; **entry stale, rewrite first** | M | ▼3 |
| 12 | **FI-25** | Sidecar-exact MUL↔CONVERT timing-hop refinement | S | ▼1 |

### Tier 1 — checks that are believed and are wrong (1–5)

The project's own stated lesson, from FI-44: *a check whose failure mode is silent success is worse
than no check, because it is believed.* Five items are in that family and none are closed. A missing
capability is visible; a lying check is not.

**1. FI-47 — ` RETAIN` on a UDT member is silently dropped.** A UDT member line shares
`DbMemberLineFormat`'s grammar with a DB's, so ` : Bool RETAIN` inside a `TYPE` parses successfully —
but a real UDT member's XML carries no `Remanence` attribute, so `WriteTypeMember` has nowhere to put
it and the token vanishes. Known, deliberate, and recorded as prose in `TypeIr.cs`'s own header and
`ir/SPEC.md` rather than enforced in code.

It takes #1 on failure mode, and the entry argues it better than a summary can: retention is the one
DB property whose loss is invisible until a power cycle. The block compiles, imports, exports and
round-trips clean, and the omission surfaces as data that did not survive an outage — on site,
months later, in a site's plant. Every other item on this list fails where someone can see it.
This one is *worse than the FI-44 family it belongs to*, by FI-44's own standard: those silent cleans
were caught by re-running a fixed check; this is caught by an outage.

It also corrupts the verification substrate. Import a UDT whose author wrote `RETAIN`, export it, and
the IR comes back without it — so `drift-check` and the golden harness both report agreement between
two files that say different things, because the difference was destroyed on the way in. A tool that
reports agreement it cannot have is the thing this project builds mechanical floors to prevent.

**Its frequency just went up, which is what moved it above FI-40.** C-132 (owner ruling, 2026-08-06)
makes the single `STATIC` interface UDT *mandatory* for equipment FBs, and cites FI-47 by name as the
reason retention must be declared on the FB static rather than in the type. So the shape that invites
the mistake is now the required shape, and the mitigation is that every author remembers the rule by
hand every time — which is the mitigation FI-44 exists to stop relying on.

Fix is a hard error at parse time on ` RETAIN` (and ` VERSION`) in a `TYPE` member line, naming the
member and stating where retention *is* declared. A hard error rather than a review finding is right:
there is no valid program in which the token means anything, so there is nothing to weigh. This is
enforcement of an already-written rule, not a new one.

**2. FI-40 — mechanical status check.** A test asserting the *derivable* facts the docs restate by
hand: mechanized-rule count = `ReviewRunner.AllRuleIds.Length`; every `docs/16` entry marked
Implemented naming a `converter <cmd>` has that subcommand dispatched in `Program.cs` and is absent
from `AITODO.md`'s open list; every skill in `docs/15`'s tables exists in `.claude/skills/` and vice
versa; every artifact filename a SKILL consumes is one some SKILL produces.

Ranked on evidence, not size. Three consecutive audits found the same defect class; the 2026-08-05
audit re-derived 14 of the prior round's 19 items as still valid, and 11 of its own 53 findings trace
to one merge that updated some documents and not others. The reading discipline meant to fix it has
failed twice — and the *correction* failed too: an audit set out to fix a stale rule count and wrote
24, because it counted `C-nnn` mentions rather than dispatches (the real figure is 18). Two audits,
three documents, one confident wrong number. Only executable code fixes a measurement that is easy to
take incorrectly.

Cheapest item here — one test file in an existing suite, no new subcommand, no new primitive, no
dependencies. **Re-run note:** `AllRuleIds` is still exactly 18 across the last 12 commits, so the
headline assertion is unchanged. But three new conventions (C-131/132/133) landed in those commits
with no mechanization, and revision 1 of *this document* repeated a stale C-501 premise — the drift
this item exists to catch is actively accumulating, including in the triage of it.

Constraint to carry in: assert identifiers and filenames only, never prose, and hard-error on parsing
zero rows. A noisy check gets deleted.

**3. FI-41 — TAGTABLE files pass vacuously.** `converter review` returns zero findings on a tag table
and reports every rule `not applicable (TAGTABLE rule support not implemented in Phase 1)` —
*including C-001 and C-005, the two rules that actually govern a tag table*. Zero findings reads as
clean; it means unchecked. Verified still present at `436cf00` (`ReviewRunner.cs:58`).

Impact is disproportionate to the fix because of *when* it fires. C-004 freezes the equipment
identifier list at project start and C-001 fixes the physical-IO tag format; those names propagate
into every instance DB, HMI tag, alarm text and cross-reference for the life of the job. Renaming is
free the day the table is written and expensive forever after. On greenfield the tag table is the
*first* artifact, so the vacuous pass happens before any other check exists to catch it.

Items 1–3 of the proposal (C-001 physical-IO form incl. `DQ`/`AQ` never `DO`/`AO`; C-005 charset;
duplicate name and duplicate address within and across a device's tables) are string and set
operations on a file the parser already reads. No dependencies. Item 4 (C-004 agreed-identifier set)
needs an input that doesn't exist — defer it.

Do the reporting fix in the same change: distinguish *does not apply to this file kind* from *not
implemented for this file kind*. That distinction is what made the vacuous pass invisible, and it
protects every future file kind.

**4. FI-46 (1)+(2) — member-resolution residuals.** Same family as FI-45 item 1, left open
deliberately when that wave shipped.

(1) `DB.SomeWord.%X0` reports `MEMBER-NOT-FOUND`. `AccessNode.FromDottedPath` already models a
trailing bit slice; the member walk treats `%X0` as a member name. A real path reads as invented, on
the hard-rule-3 anti-laundering gate — the crying-wolf failure FI-45 was written about (*"which is
how an anti-laundering gate dies"*), live today. **Promoted this revision:** revised C-501 writes all
of a word's bits in one network as slice coils, so `IO.Alarm.%X0` is now the mandated form for every
alarm bit rather than an occasional shape. The false positive fires on exactly what the rule base now
requires. Fix is a guard clause — drop a leading-`%` final component before walking. Cheapest item in
this document. Confirmed at `436cf00`: nothing in `TagStatus/` handles `%` at all.

(2) `TagTypeRegistry` is fixed for array-of-UDT, so anything resolving through it inherits the fix —
but any rule doing its *own* member walk still has the defect and nobody has looked. Unknown extent
is the argument for looking, and `Rules.cs` has since gained ~130 lines (the C-501 rewrite), so the
surface has grown since the residual was recorded. Same sitting as (1).

**5. FI-39 refinement (1) — the citation check rewards vagueness.** `relation-reconcile`'s
probative-citation check accepts a bare `IO.UPSEnable` ("4 writers") while rejecting the more precise
`FilterUnitInst2.IO.UPSEnable` as declaration-only — because the pooled FB-local path has writers and
the instance-qualified one does not. The incentive is inverted: the vaguer citation passes.

This is a hole in the newest floor, and specifically in the check built to close the *demonstrated*
loophole — `FansShutdownReady`'s only corpus occurrence being a bare declaration, which satisfies
"cite the block, file and line" while proving nothing, and which is the exact false argument that
would have licensed the REQ-003 regression. A check that prefers the weaker citation doesn't merely
fail to close that hole; it teaches the artifact-writer to widen it.

Fifth rather than third this revision on cost and blast radius: the entry says explicitly this needs
instance↔FB path resolution, not a patch, and it is confined to one check on the spec pipeline. The
wrong fix re-creates the loophole in a new shape.

### Tier 2 — capability gaps that block a workflow (6–8)

**6. FI-42 (1)+(2) — `to-ir` refuses two ordinary shapes.** A UDT whose member is a UDT-typed array
(TIA expands it into `<Sections>`) and a nested DTL member carrying `Version="1.0"` are both refused.
Both refusals are *correct* as written — unobserved shapes are hard errors, never guesses — but both
shapes are now observed, and the first is ordinary design vocabulary: a history buffer inside a
per-instance record.

Not cosmetic. Until it parses, **an IR-level re-export diff is impossible for any object containing
one**, so round-trip verification has to be done as XML instead — dropping out of the mechanism the
whole verification story rests on. That is why it outranks the CLI gaps below. Both are the same
work: extend the observed set, add a fixture. Keep the refusal for genuinely unobserved shapes. Both
verified still present at `436cf00` (`DbInterfaceMembers.cs:205` and `:309`); the multi-instance fix
that touched this file did not relax either.

**7. FI-43 — `delete --type` and `import --overwrite`.** `delete` has only `--block`, so a PLC data
type imported by mistake needs a human in the TIA UI; the greenfield job that authored 14 UDTs and
renamed all of them stranded 14 superseded types with no programmatic way to clear them. And there is
no update path at all — delete-then-import is destructive for blocks and impossible for types.

Owner-directed, and small: (1) near-copies the existing `--block` path with the same safety shape and
the same refusal on anything safety-related. (2) is the real value — it removes a destructive round
trip from the normal edit loop, and Openness already has the overwrite semantics; the CLI just
doesn't surface them. Default stays non-destructive: importing over an existing object without the
flag refuses and says so. Both report created / replaced / deleted, because an automated stage needs
to verify the outcome rather than assume it. Supersedes FI-42's item 4.

**8. FI-39 refinement (2) — `signal-sweep`'s `/` exclusion.** Its token regex excludes a real member
name containing `/`, so its residue can never reach zero on this project. Tiny, but it matters more
than "tiny" suggests: a coverage check that structurally cannot reach clean trains its readers to
accept a non-zero residue, which is how the number stops being read.

### Tier 3 — additive, larger, or needs work before it can start (9–12)

**9. FI-42 (3) — block access and `MemoryReserve`.** `DbSourceWriter` emits no `MemoryLayout`, so
every imported DB takes TIA's default (Optimized, reserve 100). A design that needs Standard —
absolute-offset addressing, or *removing* the download-without-reinitialisation hazard rather than
detecting it — cannot get there through this pipeline at all. Largest item here and the only one that
is a real IR capability decision: it touches `ir/SPEC.md`, the converter, and probably wants an ADR.
Ranked below the repairs because nothing is *wrong* today — the capability is absent and visibly so,
which is a categorically better failure than 1–5.

**10. FI-46 (3) — `ParseRender`'s `instance:` regex.** It matches any line containing that substring,
so prose in a legitimately-stopped D3 artifact can build a *phantom* render leg. The corpus
measurement it needs is a small local job, but the entry is explicit that tightening to true D3
network headers needs a measured pattern first, and guessing re-creates the problem. Doesn't fire on
the committed corpus, and FI-44's matched-nothing check catches the common form. Measure, then decide.

**11. FI-35 — `alarm-scan`, extraction half. Rewrite the entry before building.** *(Demoted three
places this revision.)* The entry's cost case rests on C-501 as it stood before 2026-08-06: "one
alarm bit per network, and the network title *is* the alarm text", so trigger tag, bit index and text
were all directly readable with no rung analysis. **That premise is gone.** C-501 is now one network
per alarm *word*: all bits written in one network as slice coils, each optionally ANDed with negated
named suppressors per C-504, and the alarm text living in the network **comment** bit map rather than
the title. Extraction now means parsing a bit map out of a comment and tolerating a suppressor term —
still very doable, no longer trivially cheap.

The build-order caveat is the important part, and it has a precedent from this week: `cead867` fixed
the mechanised C-501 checker for *exactly this* — it "enforced the superseded one-bit-per-network
rule", flagging compliant code while passing the superseded form. Building `alarm-scan` to the FI-35
entry as currently written would reproduce that mistake in a new tool. Update the entry against
current C-501/C-504/C-130, then build.

Everything else in the entry stands: facts not verdicts, same shape as `cross-check`/`trace`, a free
conformance edge, the C-503 trap (flagging "instance alarm bits not wired into `DB_Alarms`"
false-positives on every conformant project — the genuine defect is the inverse), and the generation
half staying behind FI-18.

**12. FI-25 — sidecar-exact MUL↔CONVERT timing refinement.** The shipped hop verifies the timer's PT
is the ×1000 ms form of the bound seconds member by name/operand correspondence; the deepening would
resolve the exact `EN:=ENO` wire to also catch a subtle shared-scratch wire-crossing. The entry's own
verdict, unchanged since 2026-07-20: only worth doing if that trap ever bites. It hasn't.

## Excluded, and why

Filtered for **not actionable now** — an external gate, a stage that hasn't opened, or an owner call:

- **FI-01** (pattern testing hook) — blocked on S9 and the unresolved PLCSIM story (R-07).
- **FI-02** (OB1 round-trip) — needs real production OB content; owner-deferred.
- **FI-03** (Modbus multi-instance) — needs a grounded example; none exists.
- **FI-06** (edge-detection pattern kind) — needs a grounded example, likely an S8 harvest.
- **FI-07** (Portal-instance janitor) — owner-parked; FI-28 shipped its read-only half.
- **FI-08** (proposed-tag approval path) — gated on the first real S6 tag-proposal loop.
- **FI-10** (HMI alarm exports) — gated on S5 extractors, which don't exist.
- **FI-11** (presentation bundler) — gated on the `generate` orchestrator's design.
- **FI-12** (persistent Portal session) — gated on FI-16 telemetry showing project-open still
  dominates. Building on an assumed bottleneck is what the entry warns against.
- **FI-18** (HMI interface skill) — owner scoping.
- **FI-24** provenance-wrapper half — owner-HELD to keep the converter free of external-process
  shell-out.
- **FI-31** (telemetry validator) — parked against `gen-telemetry.md`'s own stated discipline.
- **FI-32 / FI-33** (replace IR with a real language; authored interface model) — live owner debates,
  both undecided, and the analysis in each argues against the framing as raised.
- **FI-34** (programmatic pattern library) — the *safe* half (executable, type-checked tag-slot
  binding) is genuinely buildable tooling and is the closest call on the list; excluded because it is
  an open owner debate and its value is tied to library-filling (FI-21), which is owner-held. Worth
  putting to the owner rather than building unasked. **Re-run note:** two patterns landed this week
  (`valve-two-state`, admitted unproven by owner instruction; `motor-dol`'s missing interface UDT), so
  library-filling is moving — this may be closer to ready than it was.
- **FI-36-full** — blocked on R12, the D3 render reaching the coder.
- **FI-05** (blind-draft gap) — deferred D-5, and needs an honest blind target, not code.

Filtered for **not coding/tooling** — real work, wrong category for this list:

- **FI-09** remainder — AI-by-design (C-113 paradigm, C-124, C-123, the judgment clauses of C-103 and
  C-121). Over-mechanizing manufactures false confidence. *Not the same thing as the three new
  unmechanized conventions — see the observation below.*
- **FI-17** open half — the pilot is hand-caching during normal work, not building anything.
- **FI-21** (harvest-assist skill) — a skill, and owner-held as "tomorrow's work".
- **FI-38** gate-rule half — *"a functional partial that drops a stated interlock is a blocking
  fail"*, verified 2026-08-05 as enforced nowhere. Arguably the most consequential open item in the
  whole file, and it is **prose**: a rule in the reviewer skills and `docs/15`. It belongs on the
  owner's list; flagged here so the category filter doesn't bury it.

Terminal, no action: FI-04 (Rejected); FI-13/14/15/16/19/20/22/23/26/27/28/29/30/37/44/45
(Implemented); FI-39 (Implemented but for the two refinements above); FI-36-min.

## One observation outside the filter

C-131, C-132 and C-133 were ruled on 2026-08-06 and none is mechanized — `AllRuleIds` is unchanged at
18. Parts of each look mechanical on their face (C-133's *"do not give a momentary event a
`FaultReset` term"* is a structural check; C-132's *"an equipment FB declares no INPUT/OUTPUT/IN_OUT
parameters"* is an interface-section check that would have caught the 33-parameter block C-132 was
written about). None of this is in `docs/16` yet, so it is outside the filter this list was asked
for — but it is buildable coding work, in the FI-09 family, and it is the kind of thing that quietly
never gets raised. Worth an FI entry either way, if only to record a decision not to.
