# Authoring Claude Code agents and skills — frontmatter hazards and diagnosis

Working notes on the harness side of this project: the `.claude/agents/*.md` and
`.claude/skills/*/SKILL.md` files that carry `lad-coder` and the twelve pipeline skills. Written
2026-08-11 after `lad-coder` silently stopped existing and the cause took a full investigation —
including one confidently-stated wrong diagnosis — to find.

Evidence grading used throughout, same convention as `openness-quirks.md`: **MEASURED** = observed
live on this machine this session; **DOCUMENTED** = read in the official docs; **REPORTED** = a
third party's claim in a public issue; **INFERRED** = neither, and flagged as such.

---

## The one rule

**Always quote the `description:` scalar in agent and skill frontmatter.** Single quotes, with any
internal apostrophe doubled:

```yaml
---
name: my-agent
description: 'Does the thing. Use when: X, Y, or Z. See docs/15 skill #7.'
---
```

Both failure modes below disappear the moment the value is quoted, and quoting costs nothing. There
is no case where an unquoted description is better.

## The two failure modes, both silent

Frontmatter is parsed as **strict YAML**. Two ordinary characters in English prose are YAML syntax,
and neither failure produces a message anywhere in the session.

### 1. `: ` (colon-space) — the file is dropped entirely

MEASURED. `lad-coder.md` carried:

```yaml
description: The ONLY agent ... no matter how small — never do these inline yourself: writing or ...
```

YAML reads `yourself: writing` as a nested mapping key inside a plain scalar, which is illegal:

```
ScannerError: mapping values are not allowed here
  line 2, column 222: ... never do these inline yourself: writing or editing any .ir fil ...
```

The whole definition fails to parse and the agent is **not registered** — `Agent type 'lad-coder'
not found`, with the file sitting right there, git-tracked and readable.

**The trigger is the interaction with line endings, not the colon alone.** REPORTED
([claude-code #80890](https://github.com/anthropics/claude-code/issues/80890)): an unquoted `: ` is
*tolerated with LF endings and fatal with CRLF*. MEASURED here, consistent with that: a probe agent
written with LF and an unquoted `colon: writing` in its description registered fine, while the CRLF
`lad-coder.md` did not.

**This repo is permanently in the failing half.** Tracked text here is CRLF by convention (see
`CLAUDE.md`, "Environment notes") and `core.autocrlf` normalises on staging, so an LF file that
works today becomes a CRLF file that doesn't the next time git touches it. Do not rely on LF as the
fix — quote the scalar.

### 2. ` #` (space-hash) — the description is silently truncated

MEASURED, and the nastier of the two because nothing appears broken. An unquoted ` #` starts a YAML
comment, so this:

```yaml
description: The Build-stage skill (docs/15 pipeline skill #7) that turns ONE gate-1-signed ...
```

parses successfully to the 45-character string `The Build-stage skill (docs/15 pipeline skill`.
Everything after the `#` — including the entire *"Use whenever asked to code, write, build, or
generate a NEW ladder block…"* trigger text that decides when the skill fires — is discarded.

The skill still lists. It still runs when invoked by exact name. Only its selection behaviour is
crippled, which is invisible until you notice it not firing when it should.

Five of this project's skills were in this state on 2026-08-11, losing 160–927 characters each:

| File | Raw | Parsed | Lost |
|---|---|---|---|
| `.claude/skills/gen-block-modify-purpose/SKILL.md` | 972 | 45 | 927 |
| `.claude/skills/gen-block-modify-fix/SKILL.md` | 880 | 45 | 835 |
| `.claude/skills/gen-block-new/SKILL.md` | 827 | 45 | 782 |
| `.claude/skills/gen-architecture/SKILL.md` | 594 | 46 | 548 |
| `.claude/skills/review-conventions/SKILL.md` | 715 | 555 | 160 |

That is the whole Design rung, both Build-modify paths, the new-block coder and one reviewer. How
long they had been mis-triggering is **unknown** — nothing records it, and the truncation leaves no
trace in git (the file on disk is correct; only the parse is short).

Other YAML metacharacters that will bite the same way if a description starts with one: `>`, `|`,
`[`, `{`, `&`, `*`, `!`, `%`, `@`. INFERRED from YAML syntax, not individually tested here.

## What is NOT the problem

MEASURED with throwaway probe agents, each varying exactly one thing against a working baseline.
All four registered fine, which is worth knowing because all four are plausible suspects that would
otherwise burn an investigation:

| Suspected | Verdict |
|---|---|
| CRLF line endings *on their own* | fine — registers (only fatal combined with an unquoted `: `) |
| A long description (1098 chars tested) | fine — no length cap; `lad-coder`'s own is 839 |
| `tools: Read, Grep, Glob, Bash, Edit, Write, Skill` | fine — `Skill` is a valid tool name |
| Non-ASCII in the description (em dashes) | fine |

DOCUMENTED corroboration on two of these: `Skill` is listed among the built-in tools a subagent may
hold, and no length cap on an agent `description` is documented at any value. The caps that do
exist (1,536 chars, `skillListingMaxDescChars`) apply to the *skills listing* and truncate the
display rather than de-registering anything.

Also ruled out MEASURED: settings files (nothing filters agents), project trust (skills in the same
`.claude/` tree loaded fine throughout), git worktrees (none active), and duplicate names.

## Diagnosis

### `/doctor` — the only surface

REPORTED ([#17154](https://github.com/anthropics/claude-code/issues/17154)), which pastes actual
output showing a dedicated section with a per-file reason:

```
Agent Parse Errors
└ Failed to parse 8 agent file(s):
  └ ~/.claude/agents/code-reviewer.md: Missing required "name" field in frontmatter
```

Caveats worth keeping: that paste is from v2.1.2 and `/doctor` was rewritten at 2.1.205, so the
rendering may differ; the docs do **not** list agent parse errors among `/doctor`'s checks, so this
rests on the issue paste alone; and the reason string shown is a post-parse *validation* message —
no source quotes the wording for a YAML *syntax* failure. **Run `/doctor` after any edit to an agent
or skill file.** It is the cheapest standing guard and it costs one command.

### `/context` — what actually loaded

DOCUMENTED: lists custom subagents *with the source path each was loaded from*. If the agent is
absent here, it did not register, and you have separated "not loaded" from "loaded but misbehaving"
in one step.

### Parse it yourself — the check that needs no harness

MEASURED, and the one that found this. Works on any machine, gives the exact error and column:

```bash
python -c "
import re, glob, yaml
for p in sorted(glob.glob('.claude/skills/*/SKILL.md')) + sorted(glob.glob('.claude/agents/*.md')):
    raw = open(p,'rb').read().decode('utf-8')
    fm  = re.match(r'^---\r?\n(.*?)\r?\n---\r?\n', raw, re.S).group(1)
    val = [l for l in fm.split('\n') if l.startswith('description:')][0][12:].strip()
    try:
        got = str(yaml.safe_load(fm).get('description',''))
    except Exception as e:
        print('%-48s PARSE FAIL  %s' % (p, str(e).split(chr(10))[0])); continue
    q    = chr(39)
    core = val[1:-1].replace(q*2, q) if val[:1] == q else val
    ok   = (got == core)
    print('%-48s %-6d %s' % (p, len(got), 'ok' if ok else 'TRUNCATED, %d lost' % (len(core)-len(got))))
"
```

Catches both failure modes: mode 1 raises and prints `PARSE FAIL` with the scanner error, mode 2
shows as a gap between what the file says and what YAML returned. It compares against the
*unquoted* value, so an already-fixed file reads `ok` rather than showing the quote characters and
any doubled apostrophes as loss — a naive length comparison reports 2–5 chars lost on a correct
file, which is how this snippet was wrong on first writing. Assumes unquoted or single-quoted
values, which is what this note recommends; a double-quoted value with backslash escapes would need
its own unescaping.

MEASURED: run against this repo on 2026-08-11 after the fixes, all 13 files report `ok` — and
negative-tested the same day against two deliberately broken fixtures, which it caught as
`TRUNCATED, 43 lost` and `PARSE FAIL mapping values are not allowed here` respectively. A green run
from a check that has never been shown to go red is not evidence (`docs/evidence` FI-44, "empty is
not clean").

### `--debug` — worth a look, unproven for this

DOCUMENTED, with a correction that matters: `claude --debug-file <path>` implicitly enables debug
mode and takes precedence over `CLAUDE_CODE_DEBUG_LOGS_DIR` (which, "despite the name, is a file
path, not a directory", and does **not** enable logging on its own). Default location
`~/.claude/debug/<session-id>.txt`; on Windows `%USERPROFILE%\.claude\debug\`, INFERRED from the
documented default rather than stated. Set `CLAUDE_CODE_DEBUG_LOG_LEVEL=verbose`.

Run it **unfiltered**. A category filter binds only in the `=` form (`--debug='startup'`); a
space-separated one silently enables everything instead. There is no published list of category
names and no documented category for agent discovery — guessing one filters out the line you want.

Whether a YAML syntax failure reaches the debug log at all is **not documented**. The docs promise
debug-log entries for three narrower cases only (a `name` containing `:`, a missing `skills:` entry,
frontmatter hooks in an untrusted folder). Treat `/doctor` as the evidenced path and the debug log
as a maybe.

## Discovery and reload behaviour

- DOCUMENTED: project agents are found by walking **up** from the working directory to the repo
  root, scanning every `.claude/agents/` on the way. Nearest to the working directory wins on a name
  collision. No settings declaration is needed.
- DOCUMENTED: the file watcher covers **only directories that existed when the session started**. A
  brand-new `agents/` directory needs a restart before its first file is seen.
- MEASURED: an edit to an existing file in an already-watched directory is picked up **live**, no
  restart — fixing `lad-coder.md` mid-session made it dispatchable within the same session, and the
  five skill fixes reloaded with their full descriptions immediately.
- MEASURED, unexplained: the watcher did not behave as a continuous watch. A batch of new files
  created at 14:01 registered; a second batch at 14:04 in the same directory never did, across
  several minutes and many tool calls. Do not trust mid-session creation of *new* files; edits to
  existing ones were reliable. Cause unknown — do not write this up as a rule without more evidence.

## Frontmatter reference

DOCUMENTED. Only `name` and `description` are required.

`name` must be lowercase letters and hyphens, and **cannot contain `:`** — since v2.1.218 a name
containing one causes the file not to load, with an error to the debug log. Optional: `tools`,
`disallowedTools`, `model`, `permissionMode`, `maxTurns`, `skills`, `mcpServers`, `hooks`, `memory`,
`background`, `effort`, `isolation`, `color`, `initialPrompt`.

`tools` takes a comma-separated string. DOCUMENTED behaviour if an entry doesn't resolve: since
v2.1.208, when *nothing* in the list resolves the subagent refuses to launch with an error naming
the unresolved entries — a launch-time failure with a stated reason, **not** a silent
de-registration. So a missing agent is never a `tools:` problem. To preload skill *content* rather
than grant the tool, use the `skills:` field instead of listing `Skill`.

## Authoring checklist

1. Quote the `description:` scalar. Always.
2. Keep `:` out of `name` entirely.
3. Run `/doctor` after the edit — expect no `Agent Parse Errors` section.
4. Confirm with `/context` that the agent loaded, and from the path you expected.
5. If it's a new directory rather than a new file, restart before concluding anything.
6. For skills, sanity-check that the description you *see* in the listing ends where you wrote it.
   A description that stops mid-sentence is mode 2, not a display truncation.

## Upstream status

This is a known, open, unacknowledged class of bug — not a regression, and not specific to any
version we ran. REPORTED:

- [#80890](https://github.com/anthropics/claude-code/issues/80890) — agent silently skipped, CRLF +
  unquoted colon. Open. Frames it as line-ending-dependent.
- [#78270](https://github.com/anthropics/claude-code/issues/78270) — the skill equivalent, on
  Windows. Open. Its "expected behaviour" ask is literally *"at minimum — fail loud."*
- [#16916](https://github.com/anthropics/claude-code/issues/16916) — same, January 2026. Stale.
- [#7943](https://github.com/anthropics/claude-code/issues/7943) — asks for exactly this to be
  detected and surfaced. **Closed as not planned.** Expect the silence to persist.
- [#22843](https://github.com/anthropics/claude-code/issues/22843) — malformed agent files
  surfacing later as `API Error: 500` rather than a local parse error.

No maintainer response on any of them.

## The wrong diagnosis, recorded on purpose

The first conclusion this session was that Claude Code 2.1.227 (installed 08:30 that morning) had
broken project-scoped agent loading, and the recommended action was a rollback to 2.1.226. The
supporting evidence was real: `lad-coder` had dispatched 67 times on 2.1.226 the previous day, every
session after the update failed, four fresh sessions ruled out a stale-session explanation, and the
`/agents` wizard had visibly disappeared.

All of it was circumstantial. The changelogs for 2.1.225/226/227 touch nothing related to agent
discovery, frontmatter or YAML, and the wizard was removed back at 2.1.198 — a red herring from
nine versions earlier. Rolling back would have "fixed" it by restoring a laxer parse of a file that
was genuinely malformed, and the five truncated skills would never have been found.

Why the same file worked on 2.1.226 and not 2.1.227 is **still unexplained**. The honest answer is
that we don't know; per the line-ending interaction in #80890, something as mundane as a git
checkout normalising endings is enough to flip it with no version change at all. Do not write up a
cause here without new evidence — see `MEMORY.md`, "Don't infer cause from sequence".

**The transferable lesson:** when a config file stops working, parse it before blaming the harness.
A version bump sitting next to a breakage is a coincidence until something links them.
