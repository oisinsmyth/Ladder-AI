# Block memory layout (optimized vs standard), and why converter XML cannot answer it

Written 2026-08-11. Everything here is measured against a real TIA V20 project unless marked otherwise.

## Why anyone cares

A PC-side reader speaking **classic S7comm** (Sharp7, and anything else using the S7-300/400 protocol)
**cannot see an optimized block at all.** Not an error — an absence. The block is simply not there on
the wire, and the failure lands at the first DATA read, not at connect. So a successful connection
proves nothing about whether the data will be readable.

For an **S7-1200 the TIA default is Optimized**. Anything that must be read externally over S7comm has
to be positively set to Standard, and positively verified.

## What the pipeline can and cannot express

Measured by source audit, all four negative:

| Component | Handles `MemoryLayout`? |
|---|---|
| `ir/SPEC.md` (the IR grammar) | **No** — zero mentions of MemoryLayout, optimized, or block access |
| `converter` DB writer (`SimaticMl/DbSourceWriter.cs`) | **No** — emits exactly `Interface`, `Name`, `Namespace`, `Number`, `ProgrammingLanguage` |
| `converter` DB parser (`SimaticMl/DbSourceParser.cs`) | **No** — never reads it |
| `converter` `Normalizer.cs` | **Ignore-listed** — so `drift-check` cannot detect a difference in it, in *either* direction |
| `openness-cli` (before 2026-08-11) | **No** — zero mentions |

`DbSource` in `SimaticMl/DbModel.cs` has no layout field at all, so there is nowhere for the
information to live even in memory.

**Consequence: a DB authored in IR and imported gets TIA's default, which on an S7-1200 is Optimized.**
No error at import, no error at compile, no drift-check finding. The first symptom is a failed read
from whatever was supposed to consume it. This is the "a warning is not a gate" shape, except there is
not even a warning.

The `Normalizer` ignore-list entry is deliberate and documented, under a comment reading *"Block-level
configuration TIA assigns sensible defaults for on Import() regardless of source content"*. That
assumption is correct as far as it goes — TIA does assign a default — but "assigns a sensible default"
and "assigns the one you need" are different claims, and the second is false for anything read over
S7comm.

### Do not "fix" this by un-ignoring it in the Normalizer

Tempting and wrong, at least on its own. Converter output never emits `MemoryLayout`, so un-ignoring it
would make **every** real-export-vs-converter-output comparison differ, breaking the drift check
wholesale. That change only becomes correct once the converter can *emit* the attribute. Sequence
matters: emit first, then compare.

## The trap: converter `to-xml` output is NOT a TIA export

This one nearly produced a confidently wrong answer, so it is worth stating flatly.

The `.xml` files that sit beside `.ir` files are **converter re-renders**, not exports from TIA. Their
block-level `AttributeList` is the five reduced elements listed above. A genuine TIA export of the very
same block carries far more:

```xml
<IsOnlyStoredInLoadMemory>false</IsOnlyStoredInLoadMemory>
<IsRetainMemResEnabled>false</IsRetainMemResEnabled>
<MemoryLayout>Optimized</MemoryLayout>
<MemoryReserve>100</MemoryReserve>
```

plus `DBAccessibleFromOPCUA` and a `DocumentInfo`/`Product` envelope.

So grepping converter output for `MemoryLayout` finds **nothing**, and nothing is easy to read as
"not optimized". It is neither. It is **silent** — evidence about the converter, not about the block.

> **An absent attribute is "unanswered", never "false".** If you need to know a block's memory layout,
> read it from a genuine export (`openness-cli export` / `export-all`) or from the API. Never from
> converter output.

Telling the two apart is easy: a genuine export has a `DocumentInfo` envelope and the
`IsOnlyStoredInLoadMemory` / `MemoryReserve` / `DBAccessibleFromOPCUA` cluster. Converter output has
none of them.

## Where the answer can actually be read

- **Openness API:** `Siemens.Engineering.SW.Blocks.PlcBlock.MemoryLayout`, of enum type
  `Siemens.Engineering.SW.Blocks.MemoryLayout` with members `Standard` and `Optimized`. **Read/write**
  — verified by reflection against the installed V20 assembly and documented in the shipped
  `Siemens.Engineering.xml`.
- **Cheapest, no Portal at all:** any genuine export already on disk.

## Consequences worth remembering

- **Switching an existing block's layout destroys its retained data on the next download.** On a plant
  with long-running batches that is a serious operation, and it is a legitimate reason for a project to
  hold *all* its DBs optimized as a standing decision. Do not propose converting existing blocks as a
  casual fix; it may be reopening a closed engineering ruling.
- The safe shape is a **separate, purpose-built standard-access block** carrying only the data an
  external reader needs, leaving existing blocks untouched.
- **Whether setting the layout survives a re-import of the same block is UNVERIFIED.** The converter
  carries no opinion about layout and `drift-check` is blind to it, so if a re-import does revert it,
  everything stays green while the block goes silently unreadable — and it would bite on the *second*
  import, not the first. Treat re-asserting the layout after an import as required until someone
  measures otherwise.

## See also

- `src/openness-cli/README.md` — the `block-layout` subcommand, which sets and verifies this property.
- `docs/notes/2026-08-11-write-fence-and-harness-session.md` — why an external reader needs a
  standard-access block in the first place.
