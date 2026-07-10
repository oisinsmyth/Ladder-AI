# 09 — Risk Register

Reviewed at every stage gate. Likelihood/impact: L/M/H.

| ID | Risk | L | I | Mitigation |
|----|------|---|---|------------|
| R-01 | SimaticML export/import is lossy for some LAD constructs (comments, pragmas, layout, rare instructions) | M | H | Golden round-trip suite from day one (S1); unknown elements are hard errors; grow the reference project as constructs are met in the wild |
| R-02 | TIA version churn — V21+ changes SimaticML schema or Openness API | H | M | Converter versioned per TIA version; schema version asserted at parse; IR shields everything above the converter |
| R-03 | S7-1200 G2 is new in V20 — Openness/SimaticML behavior may differ from S7-1500/classic 1200 (block features, instruction set) | M | M | Reference project on real G2 hardware config; test early in S0/S1, not after building on assumptions |
| R-04 | AI hallucination: invented tags, plausible-but-wrong logic | H | H | Grounding in exported tag data; pattern composition over freeform; type-checked slots; compile gate; human review; sim tests (S9) |
| R-05 | Silent semantic drift through convert→edit→convert cycles | L | H | IR self-stability test; untouched-network invariance check (S7); normalization rules individually documented |
| R-06 | Openness API friction: session drops, single-instance limits, slow project open, licence dialogs | H | L | CLI designed for batch operation; document quirks in `docs/notes/`; keep a warm Portal instance during dev |
| R-07 | PLCSIM Advanced may not support S7-1200 (historically S7-1500 only) — S9 blocked | M | M | Verify installed version's support early; fallbacks: TIA-integrated PLCSIM, or S7-1500 shadow project for logic-level tests |
| R-08 | Workplace/IP: project data sent to a cloud AI violates policy or confidentiality agreement | M | H | `13-data-boundary.md` written and agreed *before* real project data flows; sanitization step if needed |
| R-09 | Over-trust creep: review gets rubber-stampy as the pipeline earns trust | M | H | Review checklist is mandatory and short (`11-review-workflow.md`); stage gates re-examine review quality; S7 diffs stay small by design |
| R-10 | Safety exclusion fails structurally (F-block sneaks in via a call or shared DB) | L | H | Export-time filter + test (08 §safety); review checklist item; F-runtime groups never exported |
| R-11 | Solo-project bus factor / abandonment: half-finished tooling worse than none | M | M | Stage-gated vertical slices — every completed stage is independently useful (S2 explanations, S5 extractors have standalone value) |
| R-12 | Openness licence/entitlement issues on other machines when scaling beyond one PC | L | L | Document requirements; keep tooling machine-portable |

## Assumption log (verify, don't trust)

- A-01: TIA V20 Openness can export/import LAD blocks for S7-1200 G2 as SimaticML including comments. *(verify in S0)*
- A-02: Programmatic compile via Openness reports errors with usable diagnostics. *(verify in S0/S1)*
- A-03: PLCSIM (some variant) can be driven programmatically for the target CPU. *(verify before S9)*
- A-04: Claude Code can run on the engineering PC within IT policy. *(verify — ties to R-08)*
