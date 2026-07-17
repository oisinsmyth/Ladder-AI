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
