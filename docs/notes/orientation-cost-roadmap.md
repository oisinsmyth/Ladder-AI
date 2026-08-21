# Orientation cost, round two — what is left after the context cut

**Written 2026-08-21, immediately after the CLAUDE.md context cut closed
(`docs/notes/context-cut-handoff.md`).** That work fixed the always-loaded file. Measuring again
afterwards showed the cost did not go away so much as **move**. This page records where it moved to,
what is worth doing about it, and — for one item — why the obvious answer is wrong.

## The measurement

A `gen-block-new` dispatch loads:

| file | bytes |
|---|---|
| `CLAUDE.md` | 19,593 |
| `.claude/agents/lad-coder.md` | 8,028 |
| `.claude/skills/gen-block-new/SKILL.md` | 19,506 |
| **total** | **47,127 ≈ 11.8k tokens** |

The skill is the same size as the file a day was spent cutting. `design-for-testability` is
**59,081 bytes — three times `CLAUDE.md`**, ~14.8k tokens on its own; `review-conventions` is
29,514. `CLAUDE.md` is now budget-gated at 20,480 bytes and refuses commits over it. **Nothing gates
any skill or agent file.**

## The bottleneck is reasoning time, not tool time

This is the finding that orders everything below, and it contradicts the intuition that led to this
page. Committed `gen/*/telemetry.log` rows run **10–75 minutes per stage**, dominated by AI
reasoning and blocking questions. A cold TIA Portal open is **20–30 s**; a warm attach is **~1 s**
(`docs/notes/concurrent-portal-test-plan.md`, 2026-07-14).

So the levers that matter are the ones that shorten the *thinking* loop: cheaper and faster models
on mechanical work, fewer tool round-trips, less to read before starting. Levers that shave Portal
seconds are attacking a rounding error inside a forty-minute stage.

## Ranked

| # | item | axis | status |
|---|---|---|---|
| 1 | Dispatch efficiency + README entry points | time + tokens | **done 2026-08-21** |
| 2 | `design-for-testability` cut + per-skill budget gate | tokens | open |
| 3 | Warm/batched Portal round trips | time | open, low ceiling |
| 4 | Parallel Portal lanes | — | **measure first; probably don't** |
| 5 | A read-only agent variant | tokens | needs an owner ruling |

---

## 2 — Cut `design-for-testability`, then gate the skills

**Only one skill has the disease.** Measured on the two levers that made the `CLAUDE.md` cut work —
verbatim duplication of a doc the file itself names as authoritative, and narration:

| skill | duplication | narration | verdict |
|---|---|---|---|
| `design-for-testability` 59,081 B | **16.3% verbatim, 44–56% at 5-gram** vs `test-environment-contract.md` | 8.1% | same disease, **~22–26 KB cuttable** |
| `review-conventions` 29,514 B | 1.6% | 4.5% | **do not cut** |
| `gen-block-new` 19,506 B | 1.1% | 8.2% | **do not cut** |

`design-for-testability` writes *"Contract §2.4 is the single authoritative version of this table"*
at the foot of a **9.2 KB row-for-row paraphrase of that table**, several rows word-for-word
identical. Three sub-headings are verbatim clones of the contract's own headings. That is exactly
what `CLAUDE.md` was doing to the two tool READMEs.

The approach: collapse `### An absent field is decided per case` and the Gate 3/4/5/6/7/8/11 bodies
to pointers into contract §2.4/§2.5/§2.8/§3/§4/§4.5/§6/§7/§8. **Keep** Step 0 ("verify the
verifier" — the contract's own preamble defers to it and says so), the "verifier today" column of
the Step 1 table (the contract has the requirement column, not what exists in code), `## Step 5 —
the report`, and `## What I had to guess` (5.1 KB, ~3% overlap, entirely original live escalations).

`review-conventions` and `gen-block-new` are large because they carry per-rule and per-mechanic
*procedure* that exists nowhere else — the skill converts each convention into a grep and a verdict,
rather than restating it. `gen-block-new` is already smaller than post-cut `CLAUDE.md`. Cutting
either would delete instruction, not restatement.

Then generalise `tools/check-claude-md-budget.py` to a per-file budget table and widen
`hooks/pre-commit`, so the skills cannot regrow the way `CLAUDE.md` did.

### The bigger cost is what the skills tell agents to go and READ

Found 2026-08-21 while sweeping for the `CLAUDE.md` double-load. A skill's own size is only half its
orientation cost; the other half is the reading it commands before work starts, and that half is
larger and entirely ungated:

| instruction | target | bytes |
|---|---|---|
| `design-for-testability/SKILL.md:16` — *"Read the contract this run — never from memory"* | `docs/notes/test-environment-contract.md` | **117,593**, unscoped |
| `hmi-designer.md:13` — *"Read before you start"*, four files | doc 17 + three `hmi/` docs | **99,292**, unscoped |
| `assertion-enumerator.md:10` | `assertion-enumeration.md` (unscoped) + contract §3 | 36,164 + 117,593 |
| `enumerate-assertions/SKILL.md:16` — *"Read the definition this run"* | `assertion-enumeration.md` | 36,164, unscoped |
| six skills, *"read fresh this run, never from memory"* | `docs/06-lad-conventions.md` | **82,615** each time |
| `CLAUDE.md:111` — *"One page"* | `docs/notes/live-project-readiness.md` | 42,290 — it is not one page |

So a `design-for-testability` dispatch loads its own 59 KB **and is told to read a 117 KB contract it
already paraphrases**. That is the same duplication measured above, paid twice in the same run.

The "never from memory" instructions exist for a real reason — those documents are drafts and a
remembered figure is a wrong figure — so the fix is **scoping, not deletion**.

### DONE 2026-08-21 — and most of them turned out NOT to be scopable

Four were scoped, two were rule-range **bugs** rather than size wins, and **three were examined and
deliberately left alone**. That last group is the useful part of this record: the reasons are not
obvious, and without them the idea gets retried.

**Scoped:** `hmi-designer` → `target-differences.md` (the four intro sections and the whole Ledger
table, ~120 lines of 586, narrative on demand); `assertion-enumerator` → `assertion-enumeration.md`
§1/§3.2/§4.3/§7; `gen-code-structure` and `gen-architecture` → `docs/06` **by rule ID**.

**Fixed as bugs:** `gen-architecture` scoped `C-113–C-127`, one short of **C-128/C-130/C-131/C-132**
— all error-severity and all deciding manifest content. `gen-code-structure` named only the preamble
while choosing interface members and rendering boolean logic, the surface of C-115/C-132/C-601/
C-603/C-605. Both now cite the full set.

**🔴 DO NOT SCOPE these three — examined and rejected:**

- **`design-for-testability:16` → the contract.** The weakest candidate despite being the biggest
  file. The skill cites 17 sections spanning §2.1–§10; volatility is 16 inline date-stamps with **no
  changelog**; §2 alone is 59% of the document; and the constants the instruction actually protects
  live in **`PC-Client-Modbus-Spec-Draft-final.txt` §12a**, a different file, referenced 12 times
  from the contract. Scoping does not touch the real risk.
- **`review-conventions:47` → `docs/06`.** The rule IDs in that sentence are named as rules that were
  *revised* on 2026-07-16 — the reason memory is untrustworthy, not the reading list. The skill
  reviews six of the eight rule sections. Scoping to those IDs inverts the sentence.
- **`gen-block-new:87` → `docs/06`.** Its named list omits **C-201, C-403, C-406, C-410, C-132,
  C-127–C-133, C-508** and all of C-601–C-611, every one error-severity. Unscoped, the reader meets
  them on the way past; scoped, they disappear. A correct scope here is "everything except the
  not-in-force block" — a 6% saving that is not worth the edit.

Two traps found while doing it, both worth carrying forward: **`C-204` is a Commenting rule that
physically sits under `## Data`**, so `docs/06` must be cited **by rule ID and never by heading**;
and **`target-differences.md`'s Ledger must be read whole** — row 32 is struck through and retracted
by row 41 nine rows later, so a reader who greps for one row can read a dead row as live.

## 3 — Warm and batch the Portal round trips

Prefer `import-all` + one `sanity-check` over per-block loops. Worth doing as hygiene, but know the
ceiling before spending time on it: `openness-cli` never closes a project it did not open, so opens
already amortise. Batching a 40-block run does not save 40 project opens — it saves ~39 attaches, or
about 39 seconds.

## 4 — Parallel Portal lanes: measure the queue wait before building anything

Recorded at length because it is the obvious idea, it looks like the biggest win, and the evidence
says otherwise. **It was ranked #1 during planning and demoted on the evidence.**

- **`docs/16-future-ideas.md` FI-65 already analysed it**, endorsed copies-per-agent as the right
  design, and named the blocker: *"The hard part of the copy design is **not tooling, it is
  merge-back**: which agent's diff enters the real project, in what order, reviewed by whom — an
  engineering-review question (hard rule 5), not a scheduler."*
- **It has already been run on a live job, and it went badly.** `Live Runs/JOB9004/PORTAL-QUEUE.txt`
  records two scratch copies producing a lane that claimed the slot against the wrong project — and
  failed with an error pointing at device disambiguation rather than the real cause. The
  reconciliation that followed found **40 drifted objects**, 21 of them cases where `ir/` was
  genuinely ahead and had never been imported. It also found the silent trap: the `.xml` beside
  each `.ir` was **8–10 days older**, so a wholesale `import-all ir/` would have pushed the
  project's own old state back over itself and discarded two gated waves, **with every mechanical
  check staying green**.
- **RAM is a hard ceiling**, not a soft one: ~1.5–2 GB per lane, against 7 Portal processes already
  holding ~10 GB of this machine's 23.9 GB. That is 2–3 extra lanes, not N.
- **Per-lane setup is not free.** ~50 `settings.local.json` permission entries per lane *per binary
  path*, or agents stall on a permission prompt at every Portal call — the exact interactive stall
  that adding lanes was meant to remove. Each lane also needs an owner decision and a named restore
  point in `tools/confirm-roundtrip.allowlist` and `tools/download-probe.allowlist`, which carry no
  override by design (ADR-0011 requirement 6).
- **The claims store keys on the IR directory, not the Portal project** (`ClaimStore.cs`, slug from
  `--project`, which is the `ir/` dir). Lanes sharing one `ir/` keep working correctly — that is
  what the registry was built for. Lanes forking their own `ir/` get independent namespaces and the
  registry **silently stops protecting the shared block-number space**: two lanes both allocate
  FB51, both exit 0, and it surfaces at merge-back. That is "empty is not clean" in a new costume.

Note what is *not* the problem: concurrent sessions themselves. The recorded verdict is *"the
concurrent-session feature is not the cause of instability"* — stray process pileup is, and 5/5
fresh concurrent launches from a clean baseline ran without a hang. The blocker is merge-back, and
it is unsolved.

**Before building this, measure how long lanes actually spend queueing.** If the answer is thirty
seconds inside a forty-minute stage, there is nothing here worth the divergence risk.

## 5 — A read-only agent variant

Explain and review dispatches carry the whole write-path contract — `evidence.json`, the compile
gate, claims, the Portal queue — that they never use. A measured ~80% of the resident file was inert
for a read-only explain. A `lad-reader` sibling with a much smaller brief would cut those
dispatches substantially.

Needs a hard-rule-8 amendment (rule 8 routes *all* LAD/IR work, reads included, through
`lad-coder`), so it is an owner decision rather than a tooling change.

---

## What section 1 did, 2026-08-21

- **The model-tiering split, narrowed by measurement.** The first draft of this convention said
  "mechanical work runs on a cheaper model". An A/B on the same sweep prompt showed that is too
  broad. Both runs returned an identical, correct 13-file list for a **literal string match**. On
  the half that required judging *"is this an instruction to read a large file in full"* — varied
  wording, each candidate needing a size check — the cheap run reported **"No instances found"**
  and the control found five, including a 117 KB contract read unscoped on every run. It reported
  none, not fewer.

  The rule that survives: cheap is for **retrieving a known literal pattern**, not for
  **classifying against a fuzzy criterion**. The test is whether the agent has to decide what
  counts. And **a cheap model's "no hits" is UNPROVEN, not clean** — the mechanical floor's exit-2
  rule, applied to models.

  The economics did not favour it either: 37 tool calls against the control's 15, for 13% fewer
  tokens and 75 seconds saved. Thrashing ate the saving.

  No `model:` frontmatter was added to `lad-coder`, `hmi-designer` or `assertion-enumerator` — all
  three are judgement work. Mechanism caveat: an unavailable model falls back to **the parent**, so
  the failure direction is expensive rather than cheap.
- **`agent-tasks/DISPATCH-TEMPLATE.md` was still instructing every board dispatch to read
  `CLAUDE.md` "first, in full"** — re-introducing the 19.6 KB double-load that `c078fab` removed
  from `lad-coder.md`, which by then said the opposite. Fixed, plus batching guidance and a
  start-here list.
- **Both migration-target READMEs got real entry points.** `src/converter/README.md` opened with a
  synopsis listing **7 of ~27** subcommands — `interface-check`, `conflict-graph`, `tagstatus`,
  `preflight` and ~13 others absent, which is precisely the material the cut moved in.
  `src/openness-cli/README.md` covered ~14 of 25, missing `download-probe` entirely. A reader
  grepping the synopsis for a command that lives 2,000 lines down found nothing.
