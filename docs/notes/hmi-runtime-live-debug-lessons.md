# HMI runtime scripting + live-device debugging — lessons (sanitized)

Captured from a live HMI debugging session on a real production WinCC Unified project. **All
site/tag/equipment specifics are withheld per `docs/13-data-boundary.md`** — only generalizable,
invented-vocabulary lessons are recorded here. Relates to **ADR-0007** (HMI engineering scope,
undecided) and **ADR-0008** (read-only live-device access).

The task: a screen script exported 24 h of two logged tags to a CSV on a network drive; the CSV came
out with null values. Root cause and the lessons below.

## 1. Methodology (project-wide — the most transferable lesson)

- The failure **presented as a code bug** and we chased three wrong hypotheses in turn — read
  concurrency (`Promise.all` of two reads), then null-handling, then "logging isn't configured." All
  wrong.
- The **actual** cause was a configuration/identity mismatch: the script addressed the *base tag*
  where the logged source was a *structured member* of it. A wrong logged-tag identifier returns a
  **benign null placeholder, not an error**, so the fault was completely silent.
- What resolved it: reading the **authoritative artifact** (the tag-logging database) instead of
  reasoning from symptoms — that gave the exact configured identifier and proved logging was healthy.
- **Takeaway** (the same lesson as `docs/evidence/PlantAutoControl-bench-autopsy.md`): when a check
  "passes" but the output is wrong, go to the **source of record early**. *Silent-null-on-wrong-
  identity* is a debugging trap — an `Error==0` with empty/placeholder data is not success.

## 2. `openness-cli` HMI tooling gaps (actionable)

- **No `--device` selector on the HMI write/inventory commands.** `hmi-edit-screen` and
  `hmi-inventory` **hard-refuse** on any project with more than one Unified HMI device
  (`More than one WinCC Unified HMI device found … Refusing to guess`). The read-only `hmi` walk
  handles multi-device fine; the write/inventory paths do not — this blocked *both* writing a script
  and reading the logging config on a two-HMI project. **Fix: add a `--device` flag to these
  commands** (promote to an FI / fix item — kept here rather than mis-numbered into `docs/16`).
- **`--event` file syntax is `Target:EventType@file`, not `=@file`.** With `=@file`, the `=` is taken
  as the *inline-script* separator and the literal path is stored as the script body (a ~55-char
  "script" that fails syntax check). Worth clearer erroring.
- **Live capability ≠ source capability.** The approved binary lagged its own source (README missing
  HMI write commands that `ArgumentParser` already had). A rebuild needs a fresh TIA `(Path,FileHash)`
  approval, so what the running binary can do is the constraint, not what the source can do.

## 3. WinCC Unified runtime JavaScript facts (bank against ADR-0007)

Only relevant if HMI engineering moves in-scope, but hard-won and easy to forget:

- **The runtime JS engine THROWS on unknown-property access** (`PROPERTY_GET GetIdsOfNames failed`,
  HRESULT `0x8000001c`) — it does **not** return `undefined` like standard JS. Consequence: never
  guess property names; enumerate fields dynamically (`for (let k in obj)`), then read known keys.
- **Parentheses inside string literals can break the syntax checker / paste path.** Keep log/trace
  strings paren-free.
- **Logged-tag read shape.** `HMIRuntime.TagLogging.LoggedTags(id).Read(from, to, 0)` resolves with an
  object that is array-like *and* carries `.Error` / `.Values`. An **empty archive** returns a single
  placeholder row: `Value=null, Quality=0, TimeStamp = 1601-01-01` (the `-11644473600000` ms
  sentinel). Recognise that as "no data," not a real reading.
- **Logged-tag identifier** is `<sourceTagPath>:<loggingTagName>`. Using the base tag instead of the
  actual logged (structured) member returns the empty placeholder above rather than an error.
- **The tag-logging store is a SQLite database** and is queryable ground truth: the `LoggingTag`
  table holds per-tag config (enabled/disabled, mode, cycle, start/end); the segment index
  (`iseg_segment`) holds time bounds and online/offline state. Most **historical segments may be
  offline** (`IsOnline=0`) while recent ones are online — a last-24 h read hits online data, but an
  older date range needs segments brought back online first.

## 4. Device-access architecture validated (ADR-0008)

- The **read-only fetch + human-executes-download + artifacts-land-on-a-shared-drive-the-agent-reads-
  back** loop was exercised end-to-end as a real debugging cycle. The **human-download boundary was
  not an impediment** — one operator action per iteration — which supports keeping that line
  (the agent instruments and reads back; a human puts code on the device). The `device-guard` fence
  and the share-as-dropbox pattern worked in practice, not just on paper.
- The harness classifier gated credentialed network mounts and device reconnaissance; that shaping is
  worth expecting in any future live-device work.
- Retention discipline held: real logging data and debug artifacts were pulled to scratch only and
  cleaned off the drive afterwards; nothing site-specific entered the repo.
