# Deferred items

Work the owner has explicitly said "yes, eventually — not now" to, separated out from AITODO
(which is for *in-flight* work) and `docs/16-future-ideas.md` (which is for ideas still *under
debate*). An item here is already decided in principle; only its timing is open. Created
2026-07-17 at the owner's own suggestion (`docs/notes/owner-questions.md` D-5).

Each entry: what it is, why it's deferred, what would bring it back. An item leaves this doc when
the owner picks it back up — move it to AITODO as in-flight work, don't just delete the line.

## D-2 — Settings rework wave

**What:** the sub-struct settings design (`UDT.Set.X`) is fully live-verified and ready. Folds in:
nine `DB_Settings` scan-copy deletions (the `FC_ControlMain` trap, C-308), settings migrated into
per-instance UDTs with iDB start values as commissioning defaults (C-307), the `DB_Settings`
shrink-to-empty question, `InCycle` member removal, a C-605 member-comment pass, and buffer
`Snake_Case` renames (C-001/C-607) — or split into smaller waves, owner's call.

**Why deferred:** owner ruling, 2026-07-17 (`docs/notes/owner-questions.md` D-2) — "Hold-Off for
now."

**Revisit trigger:** none specified; owner's call to restart. Full detail preserved in
`gen/GenProject1/fix-wave-1-reviews.md`'s standing queue and `docs/notes/owner-questions.md` D-2.

## Q-04 (GenProject1) — Per-type overcurrent setpoint numbers

**What:** REQ-019 needs an overcurrent setpoint pair (`OvercurrentSetpointMedium`,
`OvercurrentSetpointHigh`, and by extension the per-type delays `OvercurrentMediumDelay`/
`OvercurrentHighDelay`, REQ-021/022) for each of the three machine types (K75/K100/K150, Q-04's
type-count question — resolved). No numbers exist yet.

**Why deferred:** owner ruling, 2026-07-17 (`gen/GenProject1/requirements.md` Q-04) — "I dont
have those at the min, leave 0 add to deferred." In the meantime the relevant `DB_Settings`
members get an explicit `0` start value as a deliberate placeholder (C-604 convention) rather
than staying unset.

**Revisit trigger:** owner has the real setpoint numbers (from commissioning data, motor
nameplate/drive data, or site input). Also blocked on the overcurrent family's disarmed
status generally (Q-11 — real AI hardware not on this prototype panel).

## D-5 — `chained-permissive-enable` pattern's blind-draft admission gap

**What:** pattern admission criterion 3 (a genuinely blind drafted instance, proving the pattern
generalizes) was accepted on partial evidence — the drafting session had prior knowledge of the
real answer, not blind. Full analysis: `docs/16-future-ideas.md` FI-05.

**Why deferred:** owner ruling, 2026-07-17 (`docs/notes/owner-questions.md` D-5) — "Record as
deferred." Blind test material is scarce (a real, untouched target within the recorded
data-boundary approval is needed).

**Revisit trigger:** none specified; owner's call, or a suitable blind target turning up.
`docs/16-future-ideas.md` FI-05 stays the analysis record — update its status there too if this
is ever picked back up or closed.
