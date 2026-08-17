# `harness-cleanup` — DB-7's cleanup stage, given an entry point

`Harness.Results/Cleanup.cs` has implemented DB-7 in full since it was written, and **nothing called
it**: zero references anywhere in `src/` outside its own file and `CleanupTests.cs`. No executable
reached it, no production code called it. This is the caller.

**It PLANS and cannot delete.** `--yes`, `--force`, `--confirm`, `--delete` and `--execute` are
**refused by name** (exit 5) rather than ignored — silently accepting a confirmation flag would let a
caller believe a deletion happened. Deletion lives in `openness-cli delete`, behind Portal and its own
fences; a second path to the same destructive act would be a second fence to keep correct. The plan
emits the exact command per removal instead.

`CleanupStructureTests` walks the compiled assembly asserting no `File.Delete`/`File.Write*`/
`Directory.Delete`/`Process.Start` reference exists in it, with a denominator (`BodiesExamined > 0`)
and a live positive control (`PlantedDestructiveControl` in the test assembly, which the same walk
must find). A claim about an assembly needs a walk over the assembly.

## Run it

```
harness-cleanup
  --project        ir/test-project001
  --authority      "<who authorised this cleanup>"
  --cross-check    <converter cross-check --json output>
  --drain-report   <the drain report, format below>
  --claims-json    <converter claims --json output>
  --test-artifact  <vector or binding artifact>      (repeatable, at least one)
  --model          <block name>                      (repeatable, optional)
```

Produce the two derived inputs first — neither is invoked by this binary, so provenance stays visible
on the command line:

```
converter cross-check --project ir/test-project001 --json                              > xcheck.json
converter claims --project ir/test-project001 --claims C:\ProgramData\Ladder-AI\claims --json > claims.json
```

> Pass the claims store **ROOT**. The tool appends the project name itself, and
> `…\claims\test-project001` yields a second empty store with the same name that grants every claim.
> Read the `store=` line the report echoes, not your argument.

## Exit codes

| code | meaning |
|---|---|
| 0 | a batch was planned. **May contain zero removals** — read the `SCOPE` denominator |
| 1 | usage |
| 2 | refused: a required input was absent |
| 3 | **NOTHING EXAMINED** — the corpus held no objects, or none this stage owns. Not a pass |
| 4 | tests not drained |
| 5 | a confirmation flag was passed |
| 6 | an input existed but could not be read |

## The drain report — DB-7 rule 1

```
format=1
computed-by=<who established this>
computed-at=<ISO-8601 UTC>
in-flight=<n>            # must equal the number of test= lines
test=<id>                # zero or more
end
```

An empty file and a truncated write are byte-identical at zero length, and reading the second as
"nothing in flight" is the failure mode that ends with a running test's instance DB deleted. So the
document carries its own count and its own terminator, an unknown key is a hard error rather than a
skip, and a file that does not add up is **unusable** rather than empty.

🔴 **It is a DECLARATION, not a measurement, and the tool says so on every run.** Nothing in this
repository computes the in-flight set. This reader can demand an author, demand a timestamp and detect
a truncation. *It cannot make a false declaration true.*

## How eligibility is proven — DB-7 rule 2

Every referrer is an edge, from five sources, and **all five are applied in the retaining direction**:

| source | edge |
|---|---|
| `siblingRefs[].calls` | `B` calls `X` |
| `siblingRefs[].instanceDbRoots` | `B` names instance DB `X` |
| `multiWriters` / `soleWriters` / `deadMembers` / `ioBoundary` | a writer **or reader** of any path rooted at `X` |
| the corpus's own `INSTANCEOF` lines | `iDB_X` instantiates `FB_X` — cross-check emits no such edge |
| the admitted test artifacts | a whole-word mention of `X` |

The `INSTANCEOF` edge is what makes cleanup **converge across batches** rather than inverting the
order: an FB whose instance DB still exists is retained, the iDB goes in one batch, the FB becomes
eligible in the next — which is also the only order TIA accepts.

**The one subtraction is the self-edge**, and it is reported by name on every run. An FB's STATIC
members are paths rooted at the FB's own name, so every FB with statics "references" itself; left in,
nothing in any corpus could ever be eligible and this tool would return a clean-looking zero forever.

## Scope — X-J's reserved range, and the hole in it

DB-7's cleanup owns the objects §16.10 reserved for the harness: **numbers 9000–9999**, per number
space. Ownership by number is a convention about *scope*; eligibility stays graph-proven. Conflating
them would be the count-based rule DB-7's second rule forbids, wearing a different name.

🔴 **A `TYPE` and a `TAGTABLE` have no number space at all**, so X-J's rule cannot reach them **in
either direction** — they are neither owned nor provably not owned. Found on the first run against the
real corpus, where `HarnessMirror` (the harness's own tag table) and `UDT_HopperBlockageStim` (the
stimulus model's interface) both land there. They are listed under `SCOPE` on every run so the gap is
visible rather than absent. Closing it needs a declarer that is not a number; §16.10 named none.

A harness-owned **global DB** is likewise none of DB-7's three kinds (instance DB, model, block), so it
maps to `RemovalKind.Unstated` and `Cleanup.Plan` refuses it by that route. Fail-closed: filing it as a
block would have the stage removing a class of object its own rule never sanctioned.

## §16.12c — the claim release

> *"Every removal path must release its claim, or numbers leak until the range is exhausted."*

The release verb has always existed (`converter claims --release`); NB-18's missing half is the
**caller**. This plan reads the shared store, states per removal whether a claim would be stranded
(`Held` / `NoClaimHeld` / `NotChecked` — *not looked* is not *nothing there*), and emits the exact
release command.

**It does not run it, and that is a position rather than an omission.** This binary cannot delete, so a
release issued here would free a number for a removal that has not happened — handing it to the next
allocator while the block is still in the project. **The leak is slow; an early release is an immediate
collision.** So the emitted order is delete first, release second, and a test asserts that order.
