# gen-block-modify-fix validation corpus — ShredderControlSystem subsystem

**What this is.** A Green (sanitized) generated subsystem plus a set of **named, scoped defects** in it —
the validation corpus for the `gen-block-modify-fix` skill (docs/15 skill #9). The blocks here were
produced by a blind `gen-block-new` run against a spec reverse-derived from a real JOB9002 block
(`ShredderControlSystem`), then found to carry these defects during the genval2 Stage-C review
(`docs/evidence/stage-S6.md`, "gen-block-new second validation"). They are real generator output with real
findings — an honest fix-wave to prove the modify-fix skill on.

**Files.** `FB_ShredderControl.ir` (the block the fixes target) + its compile deps (3 UDTs, 3 buffer DBs,
`FC_ShredderInputMap`/`FC_ShredderOutputMap`, `FC_ControlMain`) + `requirements.md` (the register). All
Green; no real restricted identifiers. The quarantined answer key is deliberately not included — a fix corpus
doesn't need it.

**Sidecar-realism note (for the validator).** These `.ir` are sidecar-less (freshly synthesized). A
realistic modify-fix run should first import + re-export the FB through TIA to give it a *real* sidecar,
so the D-6 whole-file-strip choreography and the `converter diff --only` invariance proof are exercised as
they would be on a genuine as-built block. Fixing the sidecar-less form directly is a valid lighter test
(still exercises the diff-only invariance gate) but skips the D-6 reality.

**Acceptance for every fix:** the touched block compiles clean; `converter diff --only <target networks>`
between the as-built and fixed IR exits 0 (every untouched network's readable form provably identical); and
a fresh-context re-review confirms the defect is gone with no regression.

---

## FR-1 — REQ-030: restore the Hand-intervention block on upstream-enable  *(blind-fixable)*

- **Defect.** `FB_ShredderControl` N23 (UPSEnable): REQ-030 requires the upstream-enable to be *"not
  blocked by a Hand-intervention condition"*, but the timer `IN` has no such term. As written, the FB would
  release the upstream feeder even during hand intervention — a real functional miss (flagged independently
  by both Stage-C reviewers).
- **Target network(s):** FB N23 only.
- **Intended fix.** Add `AND (NOT InHand OR NOT HandIntervention)` (equivalently `AND NOT (InHand AND
  HandIntervention)`) to the `UPSEnableTimer` `IN`. Confirm against REQ-030's stated release conditions.
- **Acceptance:** compile clean; `converter diff --only 23` exits 0; re-review shows REQ-030 implemented.

## FR-2 — C-610: the four mapped-but-undriven outbound bits  *(blind-fixable)*

- **Defect.** `HoldAuto`, `Release4Op`, `StartHorn`, `RadioControlPermit` are copied to physical outputs by
  `FC_ShredderOutputMap` but written by no logic and carry no known-gap comment — indistinguishable from a
  wiring mistake (C-610, error), the dead-wiring class.
- **Target network(s):** the declaration site (`UDT_ShredderOutImage` / `FB` interface) and/or the mapping
  network(s) in `FC_ShredderOutputMap` — whichever the register indicates. **No REQ assigns these bits
  behavior**, so the correct fix is most likely the **known-gap comment** at declaration + mapping (not
  inventing drive logic). If the register *does* imply a driver, drive them instead.
- **Acceptance:** compile clean; `converter diff --only <touched nets>` exits 0; re-review: C-610 clears
  (each bit is either driven or explicitly documented as an intentional gap).
- *Note:* this fix may touch two blocks (the UDT/interface + the map FC) — a good test of a multi-file,
  still-scoped fix.

## FR-3 — REQ-032: non-retentive hours timer  *(NEEDS OWNER RULING — not a blind fix)*

- **Tension.** REQ-032 specifies a *retentive* 1-hour totaliser; the FB (N24) uses a self-resetting `TON`
  (C-406 forbids `TONR`). Non-retentive loses the partial hour across a stop — wrong for burst-operated
  equipment. But the C-406-compliant retentive-from-`TON`-accumulator construction (carry elapsed across
  interruptions) is a design choice, and the site retentive-timer FB isn't built.
- **This is a stop-and-route item, not a blind edit.** It exercises the skill's discipline of *flagging a
  requirement-vs-convention tension for an owner ruling* rather than guessing. Do not fix until the owner
  rules: (a) build the C-406-compliant retentive accumulator, or (b) accept the simplification with
  sign-off. Target network would be FB N24 once ruled.

---

*Provenance: genval2 Stage C, 2026-07-18. Full record: `docs/evidence/stage-S6.md`.*
