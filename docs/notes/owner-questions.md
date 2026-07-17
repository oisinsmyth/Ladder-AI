# Open questions for the owner — consolidated (2026-07-17)

Everything currently waiting on your word, in one place, priority-ordered. Each item cites where
the evidence lives. Items leave this list only by a recorded answer (register/doc update or a
dated ruling in stage-gates) — never silently. Maintained by the coordinator; superseded versions
of these questions in AITODO/register/review notes point here from now on.

## A. Direction (blocks the next skill work — raised 2026-07-17)

- **A-1. gen-architecture's carving model.** Your stated intent: decompose by *library-first
  reuse order* — (1) whole library blocks, (2) blocks composed from patterns, (3) modified
  library blocks, (4) new/freeform — carving the project in that order and handing the list to
  the programming skill(s). The built skill derives blocks from REQs and maps them to patterns
  *afterwards*. Feedback delivered in conversation (2026-07-17); awaiting your ruling on the
  revised Method before the skill is edited.
- **A-2. The programming-skill split.** Your proposal: (1) New Block creation, (2) Modify block
  for new purpose, (3) Modify block for fix. Feedback delivered in conversation; the concrete
  question: adopt the 3-skill split (with a shared modification-choreography reference), and
  pull the coding skills forward in the docs/15 build order?
- **A-3. Bounding "manual to contract".** Three coding waves have now run without a coding
  skill (InCycle/DI4 fix, fix wave 1 phases 1–2). Proposal: once `gen-block-new` exists, manual
  coding is no longer acceptable except by explicit per-case owner waiver. Yes/no?
- **A-4. Adopt the S7-first ordering? (owner context 2026-07-17: the library is purposely thin —
  organic growth via S8 harvest — and S7 is the rush, to take production load off the owner.)**
  Proposed critical path: (a) `converter diff` tooling now, in parallel (S7's entry requirement;
  formalizes the practiced invariance proofs); (b) modification-choreography reference +
  `gen-block-modify-fix` then `gen-block-modify-purpose`, sandbox-scoped, exercised against the
  B-item defect docket; (c) D-4 ruling then close S6's ten requests with small deliberate asks
  (brings `gen-block-new` up; each approval = S8 harvest candidate); (d) `gen-architecture`
  reuse-first rewrite (A-1) demoted to a cheap in-passing edit. Also flagged for the horizon:
  S7 on private engineering projects needs a docs/13 approval extension (new activity class).

## B. Functional defect candidates from fix wave 1's check stage (tier-1 — each needs
   verify-intent or fix; evidence + suggested fixes: `gen/GenProject1/fix-wave-1-reviews.md`)

- **B-1.** Simultaneous jog buttons drive both solenoids (no cross-interlock on the jog paths).
- **B-2.** Power-cycle mid-run auto-resumes the plant unwarned (RETAIN `Step`/`RecentStart`/
  `RunFwd`, no OB100) — Q-01 made concrete. Ties to D-1.
- **B-3.** `BothSwitchesFault` is latched and alarmed but acts on nothing — implausible limit
  switches leave launch/jog/cycle permitted.
- **B-4.** `Fitted` dropped mid-cycle strands `Step` non-zero (no outputs — cosmetic — but state
  parks wrong and `Cycling` would read true if ever consumed).
- **B-5.** Reversal-window semantics: the comment says re-triggering window, the rung implements
  one fixed window from the first reversal. Which does the plant need for "5 in 3 minutes"
  (REQ-028)? (Whole family currently disarmed — decision can wait for D-3 but should be recorded.)
- **B-6.** `EndTravelTimer` runs through pressure holds — a long jam raises the travel-timeout
  alarm on top of the jam. Deliberate escalation or missing C-504 suppression?

## C. Rule-book rulings (doc 06 wording/letter vs practice — from the three blind reviews)

- **C-1.** C-604's two branches conflict: the "commented constant" branch licenses
  `FC_ControlMain` N3's three deliberate constants; the reservation clause ("`NOT AlwaysTrue`
  … reserved for known-unbuilt placeholders") forbids them. Rule on the wording; cleanest code
  fix regardless is named `…Fitted`-style proposed settings members.
- **C-2.** C-117: direction-change transitions carry no not-running-feedback terms — add
  `NOT ShredderRunFwdFB/RevFB` terms, or bless the imported FB's internal 8 s pause as
  satisfying C-117 for this integration (recorded either way).
- **C-3.** C-121's letter: does a same-scan named request bit *containing* `Step = 0` satisfy
  "EN contains `Step = <from>`" (pusher N7), or must the comparison be inline?
- **C-4.** C-123: alarm-only `ParkedTimeoutFault` ("no safer step to force") vs the rule's
  explicit-recovery-transition demand — bless as documented exception or specify handling.
- **C-5.** C-115's applicability to consciously stepped (non-chained) architectures — neither
  generated FB exposes the enable/ready/running vocabulary; is that a defect or the paradigm?
- **C-6.** C-502 scale-down: no `FC_GeneralAlarms`/`FC_EStopAlarms` exist ("always exist" per
  rule); also no unhealthy/E-stop alarm at all. Instantiate the skeleton or record the
  scale-down?
- **C-7.** C-507: alarm bits X1–X8 are effectively ack-required (latched, FaultReset-cleared)
  with no documented per-alarm exceptions. Bless the list or change the latching?
- **C-8.** C-001 letter: rule says `<DI/DO/AI/AO>`; practice uses `DQ` (and would use `AQ`).
  Align the rule text or the tags.
- **C-9.** Vendor-default names: `Clock_0.5Hz` (dot breaches C-005) and `Default tag table`
  (spaces) — rename or tolerate-with-note.
- **C-10.** The imported motor FB's packed 3-bit alarm network (tool C-301/C-501 finding) — the
  standing propose-documented-exception question, carried since the S4 pilot.
- **C-11.** `RecentStart` two-writer handshake (FC latch + FB self-clear, documented and
  scan-sound) — admit as an idiom (candidate rule) or restructure to one writer?

## D. Project-scope decisions for GenProject1

- **D-1. Startup machinery** (OB100 + `DB_PLC` + C-111 simulation gating): the demo-panel
  omission waiver stands from 2026-07-16, but B-2's traced auto-resume is new evidence. Build it
  now, or re-affirm the waiver with B-2 explicitly accepted?
- **D-2. Settings rework timing**: the sub-struct design (`UDT.Set.X`) is fully live-verified;
  the C-308 scan-copies and C-307/C-122 homes are all queued on it. When do you want this wave?
  (It folds in: nine scan-copy deletions, settings→UDTs with iDB start values as commissioning
  defaults, `DB_Settings` shrink-to-empty question, `InCycle` member removal, C-605
  member-comment pass, buffer `Snake_Case` renames — or split, your call.)
- **D-3. Overcurrent arming path**: family stays disarmed pending Q-11 (no AI hardware) and
  Q-04 (machine-type structure). What's the plan — AI module on the real panel, setpoint source,
  typed-Compare converter work? (The converter's Real-compare gap is the tooling half.)
- **D-4. S6 exit-criterion posture**: roadmap says "ten plain-language requests produce
  compiling, human-approved LAD." Do the fix waves count as requests toward the ten, and do you
  want a running tally kept in stage-gates?
- **D-5. FI-05 (chained-permissive blind-draft gap)**: still open; blind material scarce.
  Schedule its session, or park with a trigger?

## E. Register questions still open (gen/GenProject1/requirements.md — carried, not re-argued)

Q-01 (restart posture — see B-2/D-1) · Q-03 (remote cycle button / dead DI16 selector) ·
Q-04 (machine-type selection: two vs three types, tags, values) · Q-05 (per-motor
instrumentation) · Q-06 (start press: momentary confirmed OK?) · Q-07 (upstream enable: 3 s
separate output confirmed OK?) · Q-08 (downstream recovery: manual restart confirmed OK?) ·
Q-10 (overload vs trip: one input — accept merged alarm?) · Q-11 (AI hardware — see D-3) ·
Q-12 (zeroable hour clock: wanted? where?) · Q-13 (reversal-count display + fault lamp
hardware) · Q-14 (DQ11 fault-reset pass-through: keep as the control-on story?) · Q-15
(hand/jog as Manual-mode overlay: confirm) · NEW-1 (register "no unwarned motion" as its own
REQ ID for S9 traceability?) · NEW-2 (jog while Blocked latched: allow or gate?) · NEW-3
(auto mode: level-triggered re-cycling vs one cycle per hopper-high event).

## F. Small/tooling (non-blocking)

- **F-1.** Converter reporter bug: `review` status line counted 2 C-301 findings, printed 1
  (imported FB) — ticket vs the reporter/counter.
- **F-2.** `FaultFB` cross-block contract (C-103): FC writes it per-scan, FB RCOILs it — verify
  the imported FB's intended contract before anyone "fixes" either side.
- **F-3.** review-conventions validation follow-ups (its note §2): DB_Settings empty-end-state
  question; C-003's `iDB_<FBName>_<Instance>` sub-clause unenforced (converter backlog).
- **F-4.** C-605 scope extension candidate: settings DBs (units/roles on HMI-written members) —
  fold into the C-6xx set?
- **F-5.** Simplicity review's candidate rule gaps: comment-contradicts-rung; redundant contacts
  need a stated reason; constants-by-omission; C-602 named-bit location. Adopt any as rules?
