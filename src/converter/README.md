# converter

SimaticML ↔ IR, bidirectional, lossless. C# (ADR-0002 revision — moved from the originally
planned Python once `openness-cli` made a .NET toolchain a hard requirement on this machine
anyway). Built in S1, `net8.0` (no `Siemens.Engineering` dependency, so unlike `openness-cli`
it isn't pinned to `net48`).

```
converter to-ir  <file>                                   # SimaticML → IR (keeps the stored SIDECAR)
converter to-ir  <file> --no-sidecar                      # SimaticML → readable-only IR — omits the SIDECAR, but only after VERIFYING the block is derivable; errors otherwise
converter to-xml <file>                                   # IR → SimaticML
converter to-xml <file> --synthesize                      # IR (no SIDECAR needed) → SimaticML — see "Sidecar synthesis" below
converter sanitize <file> --map <mapping.json> --out <path>  # SimaticML → sanitized SimaticML
converter diff <old.ir> <new.ir> [--only <network> ...] [--json]  # which networks changed, rest provably identical (S7 invariance)
```

`to-ir`/`to-xml`/`sanitize` all auto-detect DB vs code-block content (root element name for XML
input, first line for IR/text input) and route accordingly — no separate flag needed.

## What's in here

**This file is two documents interleaved**: a command reference, and a log of findings and
incidents recorded against each command. The table below is the reference half. Roughly half the
headings in the file are the other half — dated war stories, usually marked 🔴 or ⚠️, sitting under
the command they were found in. Both are worth reading; only one answers "how do I run this".

`converter --help` prints the authoritative flags and exit codes for every command. This table
says **where each one is documented and why you would reach for it**.

| command | what you use it for | section |
|---|---|---|
| `to-ir` / `to-xml` | SimaticML ↔ IR, both directions | `to-ir` / `to-xml` — output path and unresolved member types |
| `to-ir --no-sidecar` | readable-only IR, sidecar omitted after verifying derivability | `to-ir --no-sidecar` — store readable-only IR |
| `to-xml --synthesize` | IR → SimaticML with no donor XML | Sidecar synthesis — `--synthesize` |
| `sanitize` | de-identify a SimaticML export against a mapping | synopsis above |
| `preflight` | static checks **before** a Portal round trip — a filter, not the compile gate | `preflight` — static checks before any Portal round trip |
| `review` | mechanical convention checks (C-0xx–C-6xx) | `review` — harness scope / C-410 / C-603 / tag-table sections |
| `digest` | compact structural orientation; never review input | `digest` — compact structural summary |
| `digest --fingerprint` | collapse copy-pasted networks to structural signatures | `--fingerprint` — per-network structural signatures |
| `tagstatus` | classify names against the export — the hard-rule-3 anti-laundering gate | `tagstatus` — classify tag names exists/proposed |
| `diff` | network-level IR invariance; `--only` proves the rest untouched | `diff` — network-level IR invariance |
| `compare` | Normalizer-compare two SimaticML exports — the confirm loop's judgement half | `compare` — the confirm loop's judgement half |
| `drift-check` | ir ↔ simatic-ml export drift | `drift-check` — ir↔simatic-ml export-drift detector |
| `cross-check` | whole-project cross-block reference facts (never verdicts) | `cross-check` — whole-project cross-block facts |
| `trace` | forward-pass REQ trace over the reader/writer graph | `trace` — forward-pass REQ verdict tracer |
| `reuse-scan` | reuse-first: who already references this tag / implements this kind | `reuse-scan` — reuse-first duplicate-logic finder |
| `target-scan` | new-block gap hunting: REQ × tag-status × as-built | `target-scan` — S6 new-block target gap-hunter |
| `candidate-scan` | every signal that could satisfy a requirement | `candidate-scan` — compute the candidate set for a requirement |
| `undriven-scan` | per-instance interface drive states — what nothing writes | `undriven-scan` — per-instance interface drive states |
| `relation-reconcile` | reconcile (instance, relation-id) sets across the spec artifacts | `relation-reconcile` — relation-set reconciliation |
| `signal-sweep` | project-level residual signal coverage | `signal-sweep` — project-level residual signal coverage |
| `interface-check` | does the block carry the signals the spec names | `interface-check` — the static comparison D6's green rests on |
| `reachable-state` | computed slot disjointness — storage a block's CALL tree touches | `reachable-state` — D9's producer |
| `served-area` | the Modbus holding-register window, read off the `MB_SERVER` call **and** its sidecar constant | `served-area` — the Modbus window, derived from the block that serves it |
| `neighbours` | every `%M` claim inside a given area, and **the object that declares it** | `neighbours` — the neighbour list, derived from the program |
| `conflict-graph` | submission-scoped conflict edges for the harness gates | `conflict-graph` — the submission-scoped emission |
| `ir-hash` | stable readable-IR content hash, immune to SIDECAR/UId churn | `ir-hash` — stable readable-IR content hash |
| `claim` / `claims` | reserve a shared resource before writing IR | `claim` / `claims` — reserve a shared resource |

**EMPTY IS NOT CLEAN.** Across the mechanical floor — `candidate-scan`, `undriven-scan`,
`reuse-scan`, `relation-reconcile`, `signal-sweep` — **exit 1 = found something; exit 2 = EXAMINED
NOTHING**. Exit 2 is never a pass. When you read a green, read what it says it *compared*.

## The documents

This file is the index. The detail moved into `docs/` on 2026-09-17, because a 4,890-line README is
one nobody finishes — and two of its most important findings were buried where nobody would look:
a round-trip defect filed inside `## Build & test`, and a whole silent-loss class trailing the file
after it.

| document | what you come to it for |
|---|---|
| [`docs/construct-support.md`](docs/construct-support.md) | which LAD constructs convert, and what each was grounded against. **`## Current scope` is the coverage contract** and leads the file |
| [`docs/document-types.md`](docs/document-types.md) | DBs, PLC data types, tag tables, the sidecar, and the `to-ir` / `to-xml` file contract |
| [`docs/gates-and-review.md`](docs/gates-and-review.md) | the confirm loop — `preflight` → `diff` → `compare` → `drift-check` — and the `review` incident record |
| [`docs/analysis-commands.md`](docs/analysis-commands.md) | the mechanical floor: whole-project facts and spec coverage, the checks that survive an agent choosing not to look |
| [`docs/emitted-documents.md`](docs/emitted-documents.md) | the machine-readable artifacts the harness gates consume — **derived, never declared** |
| [`docs/claims-and-leases.md`](docs/claims-and-leases.md) | reserving a shared resource before writing IR; taking a real lock on Portal or the rig |
| [`docs/roundtrip-fidelity.md`](docs/roundtrip-fidelity.md) | 🔴 the silent losses — *a round-trip check is blind to any error the round trip preserves* |

The table below is the per-command reference and still names every command; follow it to the
document above that holds the command's section.

## Rules (docs/05-architecture.md, 04 §8/§10)

- Unknown elements are hard errors, never warnings or best-effort.
- Volatile attributes (UIDs, ordering, geometry) preserved in the IR sidecar — machine-owned,
  never hand-edited; the golden harness normalizer (`tests/golden/`) canonicalizes what's left
  (things TIA itself regenerates regardless of input) for diffing, each rule documented with an
  example.
- Every change reruns the golden round-trip suite (`tests/golden/`).
- Refuses safety (F-) content — inherited for free at this scope, since `openness-cli export`
  already refuses to export a safety-classified block in the first place.

## Build & test

```
dotnet build   # from this directory (converter.sln)
dotnet test
```

