# Stage gates

**Active stage: S0 — Foundation** (see `docs/02-roadmap.md` for entry/exit criteria).

Claude Code: do not perform capabilities from stages that haven't passed their gate.

| Stage | Status | Gate review date | Notes |
|-------|--------|------------------|-------|
| S0 — Foundation | **ACTIVE** | — | Entry criteria met: TIA V20 + Openness installed. TODO: Windows group membership confirmed; repo skeleton (this commit); openness-cli `list` with safety filter; reference project |
| S1 — Lossless round-trip | not started | — | |
| S2 — Read and explain | not started | — | |
| S3 — Comment generation | not started | — | |
| S4 — Convention review | not started | — | Blocker cleared early: 06-lad-conventions.md is populated |
| S5 — Data extraction | not started | — | Can run parallel with S3/S4 once S1 done |
| S6 — Generation | not started | — | |
| S7 — Modify existing | not started | — | |
| S8 — Pattern maturation | not started | — | |
| S9 — Sim verification | not started | — | Blocked on R-07 (PLCSIM vs S7-1200 G2) |

## Exit-criteria evidence

Record acceptance-test results here at each gate (10 accurate explanations, 10/10 compiling generations, etc. — see `docs/08-testing-strategy.md`).

### S0
- [ ] One command lists all reference-project blocks, F-blocks flagged and never opened
- [ ] First-connect approval dialog documented in `docs/notes/`
- [ ] A-01 verified (LAD export/import for S7-1200 G2 incl. comments) — result:
- [ ] A-02 verified (programmatic compile gives usable diagnostics) — result:
