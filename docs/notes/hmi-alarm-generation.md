# HMI alarm generation — learnings from the first production run (2026-07-29)

Grounded notes from the first time this project's knowledge base was used to produce **engineering
output for a real job** rather than to build or validate the pipeline: extracting a live project's
alarm definitions into an HMI discrete-alarm import spreadsheet.

Status: **not a capability yet.** Nothing here is built. This note exists so that when alarm
generation is scoped (see `16-future-ideas.md` FI-35, and FI-18 for the PLC/HMI boundary it
depends on), it starts from what an actual run taught rather than from a blank page.

Data-boundary note: the run was against a real project under the scoped read-only approval recorded
in `13-data-boundary.md` (2026-07-29). Per that entry's output rule, **nothing verbatim** from it
appears here — this note records structure, mechanism, and process only.

---

## 1. What was actually done

Read-only, and entirely PC-side:

1. `openness-cli list` / `export` against the engineer's **already-open** Portal session (attach by
   project name — never re-opened, never written to, never compiled).
2. `converter to-ir` on each export; all converted clean, no unsupported-construct hard errors, no
   XML fallback needed.
3. `lad-coder` read the IR and returned a structured alarm inventory (bit positions, trigger
   conditions, timer delays, per-instance wiring, existing comment text).
4. The spreadsheet was written PC-side with `openpyxl`, preserving the workbook's sheet name,
   header row, and cell styling exactly.

Total Openness involvement: four read-only `export` calls. Everything else was file transformation.

## 2. The single most important structural finding

**A project has two distinct alarm surfaces, and they are not alternatives — both are correct, and
the conventions already say so.** Anything that generates alarms must handle both and must not
treat either as a defect:

| Surface | Where | Convention | Shape |
|---|---|---|---|
| **Category alarms** | `DB_Alarms`, packed Words per category (`<Category>Alarm0`, …) | C-501 | Written by `FC_AlarmsMain` (via its per-category FCs, C-502), one alarm bit per network, slice access `.%Xn`, network title states the alarm text |
| **Per-instance equipment alarms** | Inside the equipment FB's own interface UDT (an `Alarm` Word member alongside the discrete fault bits) | C-503 | Maintained by the FB itself, per instance; reaches the HMI through the UDT, **deliberately not duplicated into `DB_Alarms`** |

**This was got wrong live and is worth recording as a trap.** Seeing several motor instances whose
alarm words had no path into `DB_Alarms`, the first reading was "the mapping is missing." It is not
missing — C-503 states outright that category words are not duplicated per instance. The inventory
was correct; the interpretation was wrong, and only reading C-503 caught it.

The generalisable rule: **an unmapped per-instance alarm word is the expected state, not a
finding.** A future alarm tool that flags "instance alarm bits not wired to `DB_Alarms`" as a defect
would produce a false positive on every conformant project. The genuine defect in this area is the
inverse — a category-word bit duplicating an alarm the UDT already carries.

A related near-miss from the same run: one category-word alarm named a piece of equipment but was
sourced from a *sequencer's* view of that equipment, not from that equipment's own motor instance.
Title-based equipment attribution is a heuristic, not a fact — the source tag is the fact.

## 3. Why the extraction was mechanically easy (and what that implies)

The category-alarm surface is **regular enough to extract mechanically**, entirely because C-501's
two conditions are load-bearing in exactly the way the rule intends:

- exactly one alarm bit per network → the network is the unit of extraction, no rung analysis needed
- the network title states the alarm text → the alarm text is *already authored*, in the IR

In the run, `FC_AlarmsMain` was ten networks of one contact driving one slice-access coil. Every
piece of data the spreadsheet needed — trigger tag, bit index, alarm text — was directly readable.
No timers, no comparisons, no interpretation.

**So `converter alarm-scan` is a genuinely small tool, not a research project** (FI-35). What makes
it small is C-501 compliance; what it should do on *non*-compliant input is refuse, per design
philosophy #10 (hard error, never best-effort) — a network writing two alarm bits, or an alarm bit
whose network has no title, is exactly the case a human must see.

Two facts it must carry that were needed live:

- **Bit positions come from slice access** (`.%Xn`, `%X0` = LSB). That is PLC semantics and is in
  the export.
- **HMI-side bit numbering within a Word trigger tag is *not* in the export.** It is an HMI
  convention. The tool must emit PLC bit positions as facts and must not assert an HMI mapping the
  data doesn't contain.

## 4. Alarm text provenance — thinner than expected

Across the whole export set there were **zero member comments and zero network comments on the
alarm networks**. The only engineer-authored alarm wording that existed anywhere was the network
titles.

That is not a documentation gap — C-501 makes the title the alarm text *by design*, so the titles
are the intended source and were used verbatim. But it means:

- **Titles are the sole provenance.** There is no fallback. An alarm bit whose network lacks a title
  cannot have its text derived from anything else, and must be reported, never invented.
- For the per-instance (C-503) surface there is **no authored text at all** — the FB's alarm bits
  are named (`FTR`, `FTS`, fault-feedback), not described. Text for those rows had to be *composed*,
  by following C-505's format and mirroring the wording the same site used for the equivalent
  category alarms. That is authoring, not extraction, and a future tool should mark those rows as
  proposed text needing review rather than presenting them at the same confidence as extracted ones.

## 5. The HMI side: no API was used, and that is the main open question

**The entire HMI half of this job was a spreadsheet.** TIA's own UI-exposed discrete-alarm
export/import (a fixed 14-column `DiscreteAlarms` sheet) was the interface; it was written with a
generic Python library. No Openness HMI API was involved, and none was investigated. Tag matching on
the HMI side was left to the engineer by explicit instruction.

That was the right call for one run, and it is the correct default under current scope
(`10-non-goals.md` keeps HMI engineering out; FI-18 is the entry that would define the boundary).
But it has consequences that must be stated plainly in any future capability:

1. **There is no compile gate on the HMI side.** Hard rule 4's whole model — nothing is presented
   until the tooling has *proven* it valid — has no analogue here. A generated spreadsheet is
   unverified against the HMI tag list, the alarm class list, or anything else. Nothing catches a
   trigger tag that doesn't exist HMI-side except the import failing in front of the engineer.
2. **PLC-side facts were verified; HMI-side names were assumed.** The trigger tags were taken from
   PLC member names under a stated assumption that HMI tags match. That assumption is the engineer's
   to check, and it is the direct analogue of hard rule 3 (never invent tags) applied to a surface
   where the tool cannot see the tag list. It must be surfaced as an assumption every time, not
   buried.
3. **The workbook's `Class` column collapses two independent axes** the conventions keep separate:
   C-506's severity taxonomy (Fault / Warning / Info-Event, assigned by the operator-action test)
   and C-507's acknowledgement behaviour (self-clearing by default, per-alarm documented
   exceptions). One spreadsheet field cannot carry both, so a generator must decide — with an owner
   ruling, not a default — how the site's class list maps onto that column. **This bit live:** all
   rows were written as non-acknowledging, which matches C-507's default, but C-507 names E-Stop
   events as exactly the documented exception where acknowledgement is expected. The E-Stop row was
   written as non-acknowledging and is wrong on that reading. A per-alarm class decision has to be
   part of generation, not a blanket fill.
4. **The bit-numbering and class questions are both HMI-boundary questions** — i.e. FI-18's
   territory. Alarm generation is a concrete, arrived-in-practice use case for FI-18, and probably
   the best one to scope it against: it is small, bounded, and produces a contract (trigger tag
   naming, bit numbering, class mapping) rather than screens.

**Open, deliberately not investigated:** whether Openness can drive the HMI alarm list directly
(removing the spreadsheet hop and giving something to verify against) is unknown. It was not looked
at because the spreadsheet route already worked and widening scope mid-job wasn't warranted. It
should be answered before FI-35 is built, since it changes the shape of the deliverable.

> **ANSWERED 2026-08-07 — `openness-hmi-api-survey.md` (reflection survey, type-shape only).** It
> depends entirely on the panel family, and is absolute in both directions. **Classic**
> (Comfort/Advanced/Professional): **no** — there is no alarm type anywhere in
> `Siemens.Engineering.Hmi.*`, so the spreadsheet route taken here was the *only* route, not a
> missed one. **Unified**: **yes** — `HmiSoftware.DiscreteAlarms`/`.AnalogAlarms`/`.AlarmClasses`
> are creatable with typed trigger and bit-number properties. That also revises points 1–3 above
> for Unified specifically: `Validate()` supplies the missing gate; `RaisedStateTagBitNumber` puts
> HMI-side bit numbering *in* the API; and `HmiAlarmClass` keeps C-506 severity and C-507
> acknowledgement on separate fields, so the collapsed `Class` column is a spreadsheet artifact,
> not a domain one. Scope is unchanged — the survey is reconnaissance, not a capability.

## 6. Process learnings

- **The data-boundary refusal worked exactly as designed, and should not be softened.** `lad-coder`
  refused to read the blocks with no recorded approval, cited the specific prior entry that had
  declined this very project as a data source, and correctly noted that an instruction from a
  dispatching agent is not owner consent. The owner was asked, approved scoped-over-broad, and the
  approval was recorded *before* any block was read. Cost: one round-trip. Value: a real record.
- **But the check belongs earlier.** The dispatching side should read `13-data-boundary.md` before
  dispatching whenever the target is a real project, not discover the block via a refusal. The
  refusal is the safety net, not the process.
- **The sub-agent also flagged a scope question the boundary rule doesn't cover**: every prior
  approval was scoped to advancing a stage of *this* project; this one was production output for a
  real job. That distinction is real, was put to the owner separately rather than folded into the
  data question, and is now recorded in the approval entry. Worth repeating whenever the knowledge
  base is used for delivery rather than development.
- **`INCONSISTENT` blocks cannot be exported at all**, so an alarm sweep can be *silently
  incomplete*: one HMI-related DB in the run could not be exported for this reason. Any project-wide
  alarm scan must enumerate what it could not read and say so — an empty result from an unexportable
  block is indistinguishable from "no alarms here" otherwise.
- **Preserve the target workbook.** The supplied file's single example row would have imported as a
  real alarm; it was replaced, and the original copied aside first. A generator writing into a
  human-supplied template should always back up and should never assume template rows are inert.
