# ADR-0001 — Intermediate Representation format

- **Status:** Accepted
- **Date:** 2026-07-10

## Context

The IR is the project's central artifact: AI reads/writes it, humans diff it, converters must round-trip it losslessly, and its semantics must stay PLCopen-compatible so other vendors can plug in later. It must serve three masters: machine losslessness, human readability, and AI legibility — and per the project owner, AI legibility is the primary one (`TIA → converter → IR → AI → IR → converter → TIA` is the actual round trip in daily use; a human reviews the diff, but doesn't hand-author IR line by line the way the AI does).

Decided against real S7-1200 G2 SimaticML exports (`JOB9002 - Tom White Waste`, scratch copy, per the re-scoped approval in `docs/13-data-boundary.md`) rather than speculatively, per the original plan. Three throwaway spikes (structure inspected, files deleted, no restricted values retained — see that doc's rule on genericizing committed examples) exported representative blocks and revealed the actual shape of a SimaticML network:

- A network (`SW.Blocks.CompileUnit` → `NetworkSource` → `FlgNet` XML) is a **wiring graph**, not a flat instruction list: `<Parts>` are typed nodes with named ports (`Contact`, `Coil`, `TON`, `Eq`/`Ge` comparisons, `O` for an OR-merge with a `Cardinality`, boxed instructions like `MOVE_BLK_VARIANT` with named parameters, `Call` for FB/FC invocations).
- `<Wires>` connect ports explicitly and can fan out to multiple destinations from a single source (e.g. one power-rail wire feeding three parts' enable pins at once).
- Parallel branches are a real graph construct (an `O`/AND-merge node with named inputs), not a text-layout convention.
- Stateful instructions carry their own instance-data reference inline (`TON`'s `<Instance Scope="GlobalVariable"><Component Name="..."/></Instance>` — a named tag, exactly like any other operand).
- Block calls (`Call`/`CallInfo`) snapshot their own parameter interface (name/section/type) at the call site in the source XML.
- Comments (`MultilingualText`/`MultilingualTextItem`/`Culture`/`Text`) use the same shape at both block-header and per-network level.
- Endpoints are one of: `Powerrail` (left rail), `IdentCon` (wired to a specific `Access`/part output by UId), `NameCon` (a named port), `OpenCon` (deliberately unconnected — e.g. an unread `ET`).

This ruled out the naive "IR = one line per rung" mental model implicit in the original framing — a network with two parallel branches feeding a comparison feeding a timer is a small DAG.

## Decision

**Option 3 (custom structured text format)**, confirmed. Concrete shape:

1. **Network body:** a reconstructed, readable expression form for the common case, with an explicit node/wire fallback for any network whose graph doesn't reduce cleanly to series/parallel form. This isn't a coin-flip between "pretty" and "general" — site convention (`06-lad-conventions.md` C-101, C-114: one function per network, chained permissives, no circular enables) already pushes real logic toward reducible structures, so the fallback should be rare in practice. When it's needed, it's an explicit, differently-shaped block for that one network — never a silent best-effort guess (design philosophy #10).
2. **Block calls:** reference only — block name + wired arguments (`CALL FC_Scale(Input := ..., Output_Min := ...)`), no inline parameter-interface snapshot. The callee's own IR file is the single source of truth for its interface; duplicating it at every call site is a second place to go stale.
3. **Stateful instruction instances:** inline at the point of use, matching the source (`TON(Conveyor1.RunEnableDelay, IN := ..., PT := ...)`) — it's just another operand, not a separate declaration.
4. **Tag tables, UDTs, DBs:** a simpler tabular sub-format, not the network-graph form — they're typed member lists with no wiring, structurally distinct from code blocks (resolves the open question in `05-architecture.md`).
5. **Sidecar:** UIds (for exact node/wire identity on regeneration) and multilingual culture data live in a sidecar section, out of the primary body. Geometry/layout is a deferred call — whether TIA's own re-layout-on-import is good enough to skip storing it, or whether specific cases need it preserved, gets settled empirically in the golden-file harness (S1 overall plan item 6), not guessed here.

## Options considered

1. **PLCopen XML (TC6) directly.** Standard, vendor-neutral by definition. Rejected: XML diffs poorly, and the actual wiring-graph shape found above would still need a bespoke concrete syntax on top to be AI/human legible — XML doesn't buy that for free.
2. **Constrained YAML/JSON schema, PLCopen-aligned semantics.** Diffable, parseable everywhere. Rejected: a generic nested-map format doesn't have a native way to express "readable expression when reducible, explicit graph when not" — it would end up reinventing a DSL inside YAML anyway, with worse ergonomics than just writing the DSL.
3. **Custom structured text format.** Chosen. Costs a parser and an owned spec, but is the only option that can express the readable-expression/explicit-graph duality directly, and reads the way a controls engineer (and the AI) actually thinks about the logic.

## Consequences

Owning a format means owning its parser, spec (`ir/SPEC.md`), and stability tests forever. The golden suite (`08-testing-strategy.md`, Layer 1) is the enforcement mechanism. The reducible/non-reducible split means the converter needs a real graph-reduction algorithm (detect series/parallel structure, emit the readable form; detect non-reduction, emit the explicit form) — this is real complexity, accepted because the alternative (always-explicit) was available and explicitly not chosen, in favor of readability where the site's own conventions make it achievable. Revisit if the non-reducible fallback turns out to be common in practice rather than rare — that would suggest either the reduction algorithm is too conservative, or the "simple by convention" assumption doesn't hold as broadly as `06-lad-conventions.md` implies.
