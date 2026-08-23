---
name: debug-binary-release-expected
enabled: true
event: bash
action: block
pattern: bin[\\/]Debug[\\/][^\n]{0,160}(converter|openness-cli)
---

🚫 **Blocked: invoking a `bin/Debug` build of `converter` / `openness-cli`**

The skills invoke `bin/Release/`. Running the Debug binary by hand means you are testing a
different program from the one every dispatched agent will actually run — and the two can differ
silently, because a Debug-only build leaves `bin/Release/` holding the previous version.

- Build Release after **any** converter change: `dotnet build -c Release src/converter/converter.sln`.
  It is free and safe at any time — the converter never touches Portal.
- Then invoke the Release path, which is what the skills use.
- If you are deliberately debugging the tool itself and need the Debug binary, say so — this
  should be a stated exception, not a silent one, because the next agent will not know which
  binary produced your result.

**`openness-cli` is different, and more dangerous to rebuild.** TIA's whitelist is keyed on
`(Path, FileHash)`, so every rebuild needs a fresh approval. **Never rebuild `openness-cli` while
Portal work is in flight.** Debug and Release hold **independent** approvals, which is why a Debug
`dotnet test` is safe while a sub-agent works on Release — `dotnet test src/openness-cli/openness-cli.sln`
*is* a rebuild; `converter.sln` is not.

**What this does NOT cover.** It fires on *invoking* a Debug binary, not on *producing* one. The
more common form of this trap is a bare `dotnet build` — which defaults to Debug — after a
converter change, leaving `bin/Release/` stale while everything still appears to work. No rule
here catches that; only the habit does.

**This rule is not load-bearing on its own.** Hookify fails open: its hook scripts always exit 0,
and an import error, a missing `python3` on `PATH`, or a wrong working directory silently disables
every rule here with no indication. A silent run means "unproven", not "clean".

---

## Rule history

**Written and executed 2026-08-21.** 12 vectors across both rules, both directions, 0 wrong. Debug
invocations of `converter` and `openness-cli` block in either separator style; the Release paths,
`dotnet build -c Release …converter.sln`, `dotnet test …openness-cli.sln` and even
`dotnet build -c Debug …converter.sln` are all correctly permitted — the last of those on purpose,
since it produces the trap rather than invoking it, and blocking a legitimate Debug build would
make the rule wrong more often than right.

**Known residual, shared with `claims-store-root` and recorded rather than hidden:** the matched
field is the whole command line, so writing *about* a Debug invocation through `Bash` — a heredoc,
a doc, a test fixture — trips the block just as running one does. Author that content with the
`Write` tool instead; these rules are `event: bash`, so file writes are unaffected.
