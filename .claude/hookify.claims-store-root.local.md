---
name: claims-store-root
enabled: true
event: bash
action: block
pattern: --claims[=\s]+[^\n]{0,200}?[\\/]claims[\\/][^\s"'\n]
---

🚫 **Blocked: `--claims` points past the store root**

`--claims` takes the store **root** — `C:\ProgramData\Ladder-AI\claims` — and nothing deeper.
The tool appends the project name itself, so a path like `…\claims\test-project001` creates a
**second, empty store**, and an empty store grants every claim it is asked for. Nothing fails,
nothing warns; two agents simply both believe they hold the same block.

- Pass the root: `--claims C:\ProgramData\Ladder-AI\claims`
- **Read the `store=` line the tool echoes; do not trust the argument you passed.** That echo is
  the only confirmation that the store you got is the store you meant.
- Every agent must share one store. Worktrees get their own, which is always empty — which is
  exactly the failure this guards.
- If this is a genuine false positive — a real store root that legitimately contains a `claims`
  path segment — say so. The pattern needs narrowing, not a one-off bypass.

**What this does NOT cover.** It keys on the conventional store path containing a `claims`
segment, per CLAUDE.md's routing rule. A store root sited somewhere without that segment, or a
path assembled from a shell variable, will not match — the routing rule remains the statement of
record, and this only catches the shape that has actually cost time.

**This rule is not load-bearing on its own.** Hookify fails open: its hook scripts always exit 0,
and an import error, a missing `python3` on `PATH`, or a wrong working directory silently disables
every rule here with no indication. Treat a silent run as "unproven", not "clean" — the same
reason `EMPTY IS NOT CLEAN` applies across the mechanical floor.

---

## Rule history

**Written and executed 2026-08-21.** 12 vectors, both directions, 0 wrong — the two shadowed-store
forms (backslash and `--claims=` with forward slashes) block; the bare root blocks nothing, in
either separator style, with or without a trailing space.

**Known residual, recorded rather than hidden: it fires on writing *about* the bad command, not
just running it.** Measured immediately — a `Bash` heredoc authoring this rule's own test vectors
was denied, because the vector text contains the shadowed path. The field is the whole command
line, so any documentation, test fixture, or commit message written through `Bash` that quotes the
bad shape trips the block.

Not fixed, deliberately. Distinguishing "runs this" from "mentions this" needs to know whether the
path is in argument position, which the available operators cannot express, and every narrowing
tried so far weakens the guard against the real case. **The workaround is to author such content
with the `Write` tool** — these rules are `event: bash`, so file writes are unaffected. That is a
better trade than a hole in the guard, and it costs one tool choice.
