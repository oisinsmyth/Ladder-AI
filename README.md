# Ladder-AI

[![CI](https://github.com/oisinsmyth/Ladder-AI/actions/workflows/ci.yml/badge.svg)](https://github.com/oisinsmyth/Ladder-AI/actions/workflows/ci.yml)

**An AI system that programs Siemens PLCs — and, more to the point, an attempt to make that safe
enough to mean it.**

Ladder logic runs physical plant. A conveyor starts, a vessel fills, an interlock refuses. Code that
is *nearly* right does not throw an exception; it moves a machine. So the interesting problem here
was never "can a language model emit ladder logic" — it can — but **what has to be true before
anybody should let it.**

This repository is the answer I built: a vendor-neutral intermediate representation for LAD, a
toolchain that round-trips it losslessly through TIA Portal, a set of sub-agents with enforced
separation of duties, and — the part I would actually defend — a collection of gates designed on the
assumption that *the model, the tools and their author will all be wrong at some point.*

---

## The design problem, stated honestly

A generated block is not trustworthy because a model produced it carefully. It is trustworthy
because something mechanical refused to let it through otherwise. Three ideas run through everything
here:

**1. Separation of duties, enforced rather than requested.**
The orchestrating model may never author or read PLC content itself — that work is routed to
dedicated sub-agents, with reads and writes separated. The agent that enumerates what a requirement
*demands* never sees the implementation, so coverage cannot be made unfalsifiable by the author of
the thing being measured.

**2. Safety rails as mechanism, not instruction.**
"Please check the compile" is not a gate. A named non-zero exit code is. Every check states the
denominator it examined, and the house rule is **`EMPTY IS NOT CLEAN`** — exit 1 means *found
something*, exit 2 means *examined nothing*, and **exit 2 is never a pass**. A check that looked at
an empty directory must be louder than one that found a fault, because the first is the one that
silently passes forever.

**3. Facts, not verdicts.**
Tools assemble evidence; people make calls. Where a tool cannot honestly decide, it refuses and says
what it could not see — on every run, including the ones that pass.

---

## What is here

| | |
|---|---|
| **`src/converter/`** | SimaticML ↔ IR, lossless, golden round-trip corpus, network-level invariance diffing. Pure in-process transformer — **builds and tests with no TIA Portal installed** |
| **`src/openness-cli/`** | The only TIA Openness touchpoint: export, import, compile gate, consistency checks. Requires TIA Portal V20 |
| **`src/harness/`** | Conformance-test harness — vector submission, observation windows, result packages |
| **`.claude/`** | The agent architecture: 4 sub-agents and 14 skills that carry the operating rules |
| **`docs/`** | The design suite — 12 ADRs, a risk register, audits, and a data-boundary regime |
| **`tools/`** | Mechanical checks, each with its own test suite and a documented negative-test result |

**Scale:** ~5,810 tests across 24 test projects; ~1,000 C# source files; 266 documents.

---

## What I would show a reviewer first

**[`docs/notes/data-boundary-audit-backlog.md`](docs/notes/data-boundary-audit-backlog.md)** — a
record of a leak of restricted identifiers into committed source, written while the cause was still
open. It names its decision-maker, records what was deliberately *not* fixed and why, and opens with
a rule I still think is right:

> *A record of a leak must not be a copy of it.*

It is the most persuasive document here precisely because it is not a success story.

**[`tools/README.md`](tools/README.md)** — the de-identification pipeline built to publish this
repository. Two deliberately independent tools: one emits rewrite rules, one decides whether a
rewritten clone is clean. They share no derivation code, because if both computed their vocabulary
the same way, a bug would produce a rule that misses something and then hunt for it the same wrong
way and find nothing — *a confident, earned-looking zero over the wrong population.*

The verifier carries a self-test: a synthetic object containing every search term, pushed through the
same matcher the real corpus goes through. On its first run it reported that **1,505 of 1,719 terms
could not match anything at all** — a bug in the tool, caught by the tool, on a run that had already
printed a plausible-looking result. Nothing else would have found it.

---

## Running it

**The demo — one command, no TIA Portal, no PLC:**

```bash
python demo/run-demo.py
```

It walks one block through six commands — `to-ir`, `digest`, `review`, `diff --only`, `to-xml`,
`compare` — printing the intermediate representation in full, then round-trips the whole reference
corpus and reports the result.

**That result is 14 of 15, and the exception is the interesting part.** `NodeStatusAlarms` is one of
four seed artifacts committed with TIA's scaffolding trimmed, so the `<Interface>` element is missing
from the *answer key*. `converter compare` localises exactly one difference and it is an addition of
TIA's own defaults — the output is right and the key is incomplete. A demo that printed 15 of 15
would be hiding that, and the number it hid would be the one worth reading.

**What the demo does not show**, because nothing here goes through TIA: import and compile behaviour.
A block that round-trips cleanly can still be refused on import — that is the class the `MemoryLayout`
defect belonged to, where a DB round-tripped *equal and still wrong* and the first symptom was a
runtime Modbus status code. The loop that catches it needs Portal exclusively and is deliberately
not runnable here.

**The tests:**

```bash
dotnet test src/converter/converter.sln
```

**Everything else** needs TIA Portal V20 and a licensed `Siemens.Engineering.dll`, which cannot be
redistributed. Those projects are excluded from CI for that reason, not because they are unfinished —
the exclusions are named, with their reasons, in `.github/workflows/ci.yml`, and the two targets that
cannot build are recorded in `tests/ci-baseline.json` so CI notices if a third joins them.

---

## Status

**Active development is paused** (2026-08-17), for time constraints and competing real-world work.
The staged plan is frozen rather than abandoned — exit criteria are not waived, and the hard rules,
data boundary and built tooling are unaffected. `docs/03-development-plan.md` holds the canonical
notice.

What is here works and is tested. It is not a product, and it was never trying to be: it is domain
tooling that requires a specific, expensive, licensed environment to run against real hardware.

---

## A note on the history

This repository was developed privately against private engineering projects. Publishing it meant removing
every restricted identifier — job codes, site and company names, and the block, tag and DB names taken
from live plant — from **every commit in the history**, over 1,300 of them, not merely from the
current files. Deleting them at the tip would have left every one of them one `git log` away.

So the history was rewritten with `git-filter-repo`, and **every commit hash changed**. Documentation
throughout this repo cites commits by their short hash — `80098e7`, `eba7033` and several hundred
more. Those references are accurate about the work and **will not resolve here**: they name commits
in the original private history. They are kept rather than rewritten because each one is load-bearing
in an argument — several byte budgets, test baselines and design decisions are justified by pointing
at the commit that caused them, and replacing those with prose would have removed the evidence to
tidy the citation.

The de-identification is not a search-and-replace. It is [two independent
tools](tools/README.md) — one that derives the rewrite rules, one that decides whether the result is
clean — which **share no derivation code on purpose**, because if both worked out their vocabulary
the same way, a bug would produce a rule that misses something and then hunt for it the same wrong
way and find nothing: a confident, earned-looking zero over the wrong population. The verifier is
run in both directions on every pass: it must return zero on the rewritten clone **and** still fail
the unscrubbed original.

What it proves is narrower than it looks, and the tool says so on every run: **closure over a
supplied vocabulary, not the absence of identifiers.** No string-matching gate can find a plant
described precisely enough to be recognised without being named.

---

## Licence

MIT — see [`LICENSE`](LICENSE). The licence covers this repository's tooling and documentation. It
grants nothing in respect of Siemens TIA Portal, the Openness API, or Siemens-shipped artwork, none
of which are included here.

---

*Orientation for working inside the repository — the design suite's reading order, the operating
rules and the document map — is in [`docs/00-README.md`](docs/00-README.md) and
[`CLAUDE.md`](CLAUDE.md).*
