---
name: explain-plc-block
description: Read a converted PLC ladder-logic IR block and produce a plain-language explanation for a controls engineer to review (Ladder-AI project, Stage S2). Use when asked to explain, walk through, or summarize what a LAD block/network does, from ir/ or from a fresh SimaticML export.
user-invocable: true
allowed-tools:
  - Read
  - Bash
  - Glob
  - Grep
---

# /explain-plc-block — S2 read-and-explain playbook

Ladder-AI project, Stage S2 (`docs/02-roadmap.md`, `docs/notes/stage-gates.md`): read a PLC
block's IR and produce a plain-language explanation, grounded and hedged, suitable for the
engineer to review. Read `CLAUDE.md` at the repo root first if you haven't already — its hard
rules (never invent tags/addresses/hardware, no safety/F-block content, LAD only) apply
throughout.

## Setup

- Converter CLI lives at `src/converter/Converter`. Check `src/converter/Converter/bin/` for an
  already-built `converter.dll`/`.exe` before running `dotnet build` — it's usually already built.
  Convert with `converter to-ir <file.xml>` (or `dotnet run --project Converter -- to-ir <file.xml>`
  if running from source). It writes `<file>.ir` next to the input — if the source folder is
  shared with other work, write your own output to a distinctly-named copy rather than
  overwriting whatever's already there.
- An IR file's readable content is everything before the `SIDECAR` line — that's the logic.
  Everything after it is machine-owned round-trip bookkeeping (wire/UId tracking) needed only to
  regenerate the original SimaticML losslessly — you don't need to read it to explain the block.
  Grep for `^SIDECAR` to find the split; see `ir/SPEC.md` if a construct's shape is unfamiliar.

## Data boundary — check before using real tag names

If this is real project data (not the synthetic `ir/reference/` corpus): check
`docs/13-data-boundary.md`'s "Per-project approvals" section for the current recorded scope
before using real tag/equipment names in your explanation. If the instructions you were given
already cite a specific approval entry, that's sufficient — no need to re-derive it, just note
what you're relying on. If not, check the doc yourself and cite what you found. Never extend an
approval's scope yourself, and flag anything that looks like it falls outside a recorded approval
rather than proceeding on your own judgment.

## Method — this is where the real findings are or aren't

- **Describe a repeating pattern once, but verify every instance of it, not a sample.** Blocks
  like this are usually built from one template repeated across many networks/instances. Confirm
  the pattern from a couple of examples, then explicitly re-check every remaining instance against
  it. Most real findings in this shape of block are a single instance quietly breaking the
  pattern (copy-paste drift) — invisible if you only describe the template from representative
  examples instead of checking all of them.
- **If the block calls out to other blocks/FBs and a shared field's meaning isn't obvious from
  naming alone, check whether those dependencies already have exports/conversions sitting nearby**
  (same scratch/job folder, or ask whoever briefed you for the paths) **and read the actual logic
  that consumes the field**, rather than inferring meaning from the name alone. This is usually
  the difference between "flagged as a naming inconsistency, unresolved" and "confirmed
  correct/incorrect by checking the source."
- **Separate fact from inference explicitly, throughout — not just in a closing disclaimer.** It's
  fine to infer what equipment physically is, what an abbreviation means, or what a process-flow
  direction looks like — say so plainly when you do, rather than stating it as read fact. Never
  invent a tag, value, or behavior that isn't in the source.
- **Grep/read the raw export directly for anything that looks like it could be a conversion
  artifact** (an odd character in a tag name, a typo, an unusual field) before reporting it as
  real — confirm it's genuinely in the source, not a parsing quirk.

## Output

Explanations happen in conversation, not committed to any file, unless you were explicitly told
otherwise. Return the explanation directly as your response — organize it however best serves a
controls engineer reviewing it. For a repeating-template block, that's usually: one-paragraph
summary, the template described once, a per-instance table, then genuinely distinctive logic and
findings called out separately from the boilerplate.
