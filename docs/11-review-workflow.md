# 11 — Human Review Workflow

"Human review" is a defined procedure, not a vibe. This is the checklist that stands between AI output and the TIA project. It is deliberately short so it actually gets done (risk R-09).

## Flow

```
AI produces change → IR diff generated → engineer reviews diff →
approve → openness-cli import → compile gate → done
reject  → feedback to AI or manual fix → repeat
```

The unit of review is the **IR diff**, plus the AI's one-paragraph intent statement ("what I changed and why"). If a change is too big to review comfortably, that is a rejection reason by itself — split it.

## Review checklist

**Every change:**

- [ ] The diff touches only what the task required (no drive-by edits).
- [ ] Every tag referenced exists in the project export (spot-check any unfamiliar one).
- [ ] No safety blocks or F-tags anywhere in the diff.
- [ ] Compile gate has passed (green in the tool output — reviewer verifies it ran, not just that the AI claims it).

**Logic changes (S6/S7) additionally:**

- [ ] I can explain what the changed logic does without reading the AI's explanation, and the two match.
- [ ] Interlocks/permissives: nothing bypassed, weakened, or reordered unintentionally.
- [ ] Edge cases considered: first scan, power-cycle/restart state, simultaneous inputs, timer preset values sane.
- [ ] Untouched-network invariance check passed (S7).
- [ ] Pattern-based portions identified; any freeform rungs get line-by-line reading.
- [ ] Sim test exists and passes (once S9 is live).

**Comments/documentation changes (S3):**

- [ ] Comments are accurate, not just plausible — spot-check ≥3 against the actual rungs.

## Rules of engagement

- Review happens in the diff, before import — never "import it and eyeball it in TIA" (TIA's LAD view hides what changed).
- Rejections go back with a reason; recurring rejection reasons become convention rules (`06`) or CLAUDE.md instructions.
- If reviewing feels rubber-stampy, stop and re-read R-09. The checklist exists for the day the AI is wrong *convincingly*.
