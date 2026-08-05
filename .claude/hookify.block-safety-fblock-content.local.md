---
name: block-safety-fblock-content
enabled: true
event: file
action: block
pattern: \bF_[A-Za-z]
---

🚫 **Blocked: content references an F_-prefixed (fail-safe/safety) identifier**

CLAUDE.md hard rule 2: **never touch safety.** F-blocks, F-runtime groups, and the
safety program must never be read, written, converted, explained, or referenced —
`docs/10-non-goals.md` and `docs/04-design-philosophy.md` call this permanent and
tooling-enforced, not a convention.

`F_` is the actual safety marker (`SafetyClassifier.SafetyPrefix` in
`src/openness-cli/OpennessCli/Openness/SafetyClassifier.cs`, e.g. `F_LAD`/`F_FBD`/
`F_DB`-style programming languages and F-prefixed block names). `openness-cli` already
refuses to export/open these blocks, so seeing this pattern here most likely means:

- You're about to write or quote content that names/describes an F-block — stop and
  report it instead of proceeding.
- This is a false positive (an unrelated identifier that happens to start `F_`) — tell
  the user so the pattern can be tightened rather than bypassing the block.

**Scope of this guard — recorded 2026-08-05 (audit F-49), so nobody mistakes it for total
coverage.** A bare `pattern:` under `event: file` is expanded to `field: new_text`, so this rule
matches **content being written** and cannot fire on a `Read`. It is the *write-side* half of hard
rule 2. The read-side guarantee comes from a different place: `SafetyClassifier`
(`src/openness-cli/OpennessCli/Openness/SafetyClassifier.cs`) refuses to export or open safety
blocks at all, and fails loud on an unrecognised language rather than assuming it is safe. Combined,
the two cover the rule; neither does alone. Two mechanical notes: patterns are compiled
case-insensitively, so `\bF_` also matches `f_` (widens, never narrows — harmless here); and rule
discovery globs `.claude/hookify.*.local.md` **relative to the current working directory**, so a
hook process whose CWD is not the repo root loads *no* rules at all.
