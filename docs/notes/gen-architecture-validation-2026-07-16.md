# gen-architecture skill — retroactive validation against test-project001 (2026-07-16)

**What this is.** Build-order step 5 of `docs/15-generation-pipeline.md`: the
`.claude/skills/gen-architecture/SKILL.md` Design-stage skill, validated by performing its own
procedure retroactively on test-project001 — producing `gen/test-project001/architecture.md` as the
as-SHOULD-BE design from the 69-REQ register alone (greenfield posture), then comparing it
against the as-built corpus and the functional review's independently-established findings
(`docs/notes/review-functional-validation-2026-07-16.md`). The validation signal is
**reconvergence**: a design derived from the register + doc 06 + patterns, with no as-built
design contact, should *structurally forbid* the defect classes the functional review found —
without copying the findings.

**Run form and ordering (the honesty record):**

- **Inline run, not a subagent** — the build brief left the choice open. Reason: a fresh-context
  agent would read the same required inputs (doc 06, docs/15, the sibling skills) whose rule
  rationales already cite test-project001 history, so its marginal blindness gain is small; the real
  defense is structural (below). Recorded as the weaker form deliberately chosen.
- **Ordering, provable from git:** skill committed (`f4bf3bb`) → design derived from the register
  and written → `architecture.md` committed (`1d338d9`) → only then were
  `review-functional-validation-2026-07-16.md` and any as-built block logic opened → this note.
  Pre-commit corpus contact was tag-status greps only (tag table matches, DB member-name lists,
  file inventory) with all design decisions locked first; no block logic was read before
  `1d338d9`.
- **Brief-informed, not blind — declared plainly:** the build brief for this run itself named
  five expected findings (settings scan-copy, missing sequencer healthy input, pusher's missing
  stop path, absent OB100/DB_PLC, the InCycle member), and two of those are also named in
  required inputs regardless (`review-functional/SKILL.md` motivates itself with the in-cycle
  lamp and the stop polarity; doc 06's C-308 names the `FC_ControlMain` scan-copy, C-127/C-126
  their own origins). The reconvergence claim therefore does **not** rest on blindness. It rests
  on derivation: every forbidding element in `architecture.md` cites the register REQ or C-rule
  that forces it, and the mandatory 69/69 REQ trace (skill section 7) makes it impossible to
  quietly skip the REQs whose traces produce those elements. A reader can check each chain
  without trusting the author's memory state.
- `converter digest` was not exercised (no Release binary in this worktree; the run was
  greenfield-posture anyway, and the skill's fallback clause covers the missing binary).
- **Same-session examiner caveat** (the sibling validations' recurring note): the skill, the
  design, and this comparison share an authoring session. The stronger form exists and is
  built into the process itself: gate 1 — the owner reads `architecture.md` cold and signs or
  rejects it.

## 1. Reconvergence — both directions

### 1a. As-built defects the as-should-be design structurally forbids

Grounding: functional-review findings from
`docs/notes/review-functional-validation-2026-07-16.md` (its §1/§2 and the embedded blind
reports); as-built quotes verified directly in `ir/test-project001/` after `1d338d9`.

| Finding (as-built, per the functional review) | As-should-be design element that forbids it | Forced by (the derivation the claim rests on) |
|---|---|---|
| **Settings scan-copy** — `FC_ControlMain` N5: nine `MOVE(EN := TRUE, IN := DB_Settings.X) => iDB_PusherControl.IO.X`; the setting exists twice with a cyclic copy in between (C-308's named trap; verified first-hand in the corpus) | §2 one-home construction: every setting lives in exactly one place (owning instance UDT per C-307; `DB_Settings` keeps only ownerless items) — "no second copy to synchronize, so the trap is impossible by construction" | C-307/C-308 rule text (2026-07-16 sharpening). Note: the rule postdates the corpus — this deviation is rule-evolution, not (only) a build miss |
| **Missing sequencer healthy input** — as-built `FC_ControlMain` N1 wires no `Control_Healthy` into the sequencer; review: REQ-062 contradicted, `RecentStart := AlwaysTrue` strap load-bearing; owner follow-up "give the sequencer a healthy input and retire the `RecentStart` strap" | `UDT_ShredderSeq.ControlHealthy` input; §8 wire `DB_Input.ControlHealthy → SEQ.ControlHealthy`; stop path drops to idle on healthy loss, restart only via fresh start edge | REQ-062's trace ("no restart by itself after E-stop recovery — fresh start command required") cannot be written without the healthy signal reaching the sequencer |
| **Pusher outside the stop path** — pusher FB has no stop input; both blind runs found it unprompted (Phase 2 graded REQ-012 *partial* post-fix on exactly this) | `UDT_Pusher.StopCmd`; §8 wires the derived `StopCmd` to sequencer, pusher, and every motor's `Shutdown`; pusher stop → outputs off, step 0 | REQ-012's text ("stops **all** equipment at the same time") — the trace row must show the pusher hop or the REQ is unmapped |
| **Absent OB100/`DB_PLC` machinery** — no `DB_PLC`, no OB100-class block anywhere (grep-confirmed pre-commit) | §3: "PRESENT, not waived — no owner waiver is recorded": `OB100 → FC_StartupReset`, `DB_PLC.Simulation` force-FALSE, both `Step`s and transient state force-idle, C-124 fault-latch carve-out honored | C-305/C-403/C-124 rule text; the skill's section-3 contract makes absence expressible only as a cited owner waiver |
| **In-cycle lamp dead member** — pre-fix: `IO.InCycle` consumed-but-never-written (the review's must-catch P1-1); post-fix: `FC_ControlMain` N7 five-feedback OR with the ruling comment, member left dead-but-documented | §8/REQ-069: `DB_Output.InCycle` := OR of the five field feedbacks, computed in `FC_ControlMain`; **no sequencer interface member exists at all** — the dead-member failure mode has no home | REQ-069's own text (register: owner ruling — field feedbacks, not PLC commands) + the register's classification ("combinational — a pure OR"): the ruling text alone forces the fixed form. The design **converged with the owner's fix without having seen it** — the cleanest single reconvergence in the run |
| **Overcurrent coded-but-disarmed** — the 11-REQ group behind `NOT AlwaysTrue` gates in sequencer N12, setpoints consumed nowhere, current member dead (review B-1: "disarmed, never implemented") | §7 marks REQ-017's *arming* BLOCKED on Q-11 (no AI hardware) and REQ-018/019 BLOCKED on Q-04 — blocked *at design time, before coding*; detection homed in the motor FB (C-503) consuming the buffer member with real comparisons when armed | Hard rule 3 + the skill's stop conditions ("missing info → open question, never invention"): gate 1 surfaces "cannot arm" as an owner decision instead of shipping placeholder-gated logic that looks complete |
| **No per-motor supervision** — conveyors/power pack driven directly by the sequencer; infeed has no failed-to-run check (DI9 unconsumed pre-fix); REQ-057's "identify which motor" unsatisfiable; per-instance `FTTime`=0 trips instantly (review N-1) | §1 motor-dol ×3 + `FB_MotorFwdRev`: four instances → four `FTR` bits (REQ-057's identification), `MotorFaultAny → sequencer stop` (REQ-063), running feedbacks consumed per instance | REQ-057 ("identify which motor") + REQ-063 ("**any** motor fails to run … stops completely") + C-106/C-108 (standard FB per repeated equipment) |
| **Disabled-pusher contradictions** — REQ-043/044 contradicted: with `Fitted` FALSE the auto-park still fires, pump runs, switch supervision continues | §7 REQ-043: `Fitted` FALSE gates **all** pusher outputs, the pack request, feedback supervision, and (by placement in the FB) the park transitions; REQ-044: fault bits suppressed in the FB and the whole `FC_PusherAlarms` category gated | REQ-043/044's texts, traced as their own rows — each clause needs a gate the trace can point at |
| **Jog-release self-motion** — REQ-039 contradicted as-built: releasing jog off-home fires an auto-park; the pusher moves unbidden | §1 pusher sketch: park-return (S10) is entered only on four *named* triggers (mode-off, pre-start-complete, blocked-return, sequencer park request) — jog release is not one; jog is combinational hold-to-move from step 0, release = stop where it is | REQ-039's text ("on release it stops where it is") traced as its own row |
| **No E-stop/healthy annunciation, single alarm word, no category FCs** — as-built `DB_Alarms` = one `ShredderAlarm0` word; `FC_AlarmsMain` carries 9 alarm networks inline; C-502's always-pair absent | §1/§3: `FC_AlarmsMain` calls `FC_EStopAlarms`/`FC_GeneralAlarms`/`FC_PusherAlarms`; three category words; control-unhealthy annunciated in the EStop category | C-501/C-502 rule text ("`FC_GeneralAlarms` … and `FC_EStopAlarms` always exist") |

### 1b. Expected convergences (unremarkable — named in committed inputs)

The C-127 orchestration shape (all wiring in `FC_ControlMain` — the as-built already does this
correctly), the stepped-FB decisions for both sequences (the register's own C-113 table is an
input to both designs), the buffer-DB landscape (`DB_Input`/`DB_Output`/`DB_AnalogInput`, map
FCs first/last in OB1 with C-109's direct-call exception — as-built OB1 `Main.ir` matches this
shape exactly), and block-name convergence (`FB_ShredderSequencer`, `FB_PusherControl`,
`FC_ControlMain` appear verbatim in doc 06/pattern docs). These match, as the brief predicted,
and prove little beyond shared inputs — listed so they aren't mistaken for validation signal.

### 1c. The other direction — findings the design does NOT structurally forbid (skill gaps)

Recorded per the validation contract: these are the honest misses, each a candidate improvement
to the skill's output contract.

1. **The reverse-window race (REQ-005/006 — the review's "reverse never runs," statically proven
   from 6.0 s < 8.0 s).** The design *reduces* this class — the 8 s lives in exactly one place
   (motor `ReverseDelay`, C-409), exposed as `Ready`, and S40 explicitly waits on it — but the
   manifest's S30 sketch does not state whether the 6 s reverse-run window runs on *command* or
   on *confirmed reverse feedback*. A coding stage could still implement a command-clocked
   window that expires inside a lockout (the S90→S30 re-entry after an overcurrent stop is the
   exact exposure). **Skill-gap candidate:** section-1 step sketches should declare, per timed
   step that commands equipment, whether the timer clocks on command or on confirmed feedback —
   one column, cheap, and it would have forced this hazard into view at gate 1.
2. **Per-instance value gaps beyond Q-02's `DB_Settings` scope (review NEW N-1: instance
   `FTTime` = 0 ⇒ instant failed-to-run trip).** The design homes the known unconfigured values
   and flags SHR/DIS windows, but does not enumerate *every* per-instance timing member of the
   pattern blocks (IFC/PSH `FTTime` etc.) as a commissioning-value checklist. **Skill-gap
   candidate:** the tag-status/settings tables should sweep all settings members of every
   instantiated pattern block, not only the ones the register names.
3. **Reversal-window semantics (review S9 candidate).** The design says "5 within
   `ReversalWindowTime`" without fixing re-arm semantics — same openness the as-built has.
   Acceptable at design tier, but the manifest could have named it as a coding-stage decision
   plus S9 test explicitly.
4. **Cycle-trigger-while-off-park recovery** is unspecified in the pusher sketch (REQ-032 says a
   cycle starts from parked; the design doesn't say whether an off-park trigger parks-then-
   cycles or is refused). Coding-level, but the as-built's defect cluster (auto-park as a
   catch-all) grew in exactly this unspecified corner.
5. **If AQ-03 resolves to importing the site fwd/rev block**, the constant-strap class
   (`InHand`/`RecentStart := AlwaysTrue` — one of which the review proved load-bearing) re-enters
   through the imported interface. The manifest defers that to gate 1 (AQ-03) rather than
   forbidding the strap class outright; a future revision could require every unused imported
   input to be wired from a named, commented source (C-604's spirit) as a manifest obligation.

Two process-level convergences worth recording (neither direction's tables capture them): the
review's headline "the build as committed cannot start" traces to unconfigured live-zero
settings — the design's answer is structural (values are homed, flagged, and gate-1-visible with
REQ-derived defaults where a source number exists, per C-309) rather than logical; and the
review's REQ-012 NC-polarity must-catch is addressed in the design by *naming* the NC fact at
the derived `StopCmd` (§8) — polarity remains a coding-stage hazard, but the manifest puts the
fact where the coder and gate-1 reviewer both meet it.

## 2. Verdict

**Validated, with the declared caveats.** The skill's procedure, executed from the register
alone, produced a design that:

- structurally forbids **all five brief-named findings** and at least four more the brief did
  not name (disarmed-overcurrent class, per-motor supervision, disabled-pusher gating,
  jog-release self-motion, alarm-skeleton absence) — each forbidding element traceable to a REQ
  or C-rule, which is the defense against the run's non-blindness;
- independently converged on the owner's own post-review fix for the in-cycle lamp (five field
  feedbacks OR'd in the orchestrator, no interface member) from the register text alone — the
  strongest single signal in the run, since that shape was ruled *after* the as-built was coded;
- surfaced the >20% freeform reality (~78%) loudly at the gate instead of downstream — docs/15's
  cause 3 made visible at design price;
- and missed, or left under-specified, the five items in §1c — real skill gaps, now recorded,
  none of which invalidates the section contract (each is an addition, not a rework).

**Caveats, restated:** brief-informed run (not blind); inline (not subagent); same-session
examiner; the gate-1 owner read of `architecture.md` is the standing stronger check, and AQ-01
(settings re-homing) in particular must not be treated as validated by this note — it is a
design *proposal* whose HMI-surface consequences only the owner can weigh.

**Cost:** ~45 min / ~90k tokens for the stage run (telemetry row appended at run end,
2026-07-16T22:38); comparison and this note additional (~30 min). One telemetry row total — the
comparison is skill-build work, not a pipeline stage run.

## 3. The artifact

The design itself: `gen/test-project001/architecture.md` @ `1d338d9` — 33 manifest items, 69/69
REQs traced (9 BLOCKED on carried register questions, 1 out-of-scope), freeform ~78% with the
gate-1 opt-in flag raised, five new AQ-nn questions. Not duplicated here; it ends with the
pending gate-1 sign-off block, which is where its life as a proposal actually starts.
