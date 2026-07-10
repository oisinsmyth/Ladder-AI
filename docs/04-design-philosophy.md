# 04 — Design Philosophy

Ten principles. When a design question comes up, the answer that satisfies more of these wins.

## 1. The human is the engineer of record

AI drafts, explains, and checks; the engineer decides. Every artifact is designed to be reviewed: text-based, diffable, small. If a capability can't produce a reviewable diff, it isn't ready.

## 2. Compile-clean or it doesn't exist

AI output that hasn't been imported and compiled in TIA Portal is a draft, never a deliverable. The compile gate is automated and unbypassable — a failed compile loops back to the AI, not forward to the human.

## 3. Text is the interface, XML is the transport

The IR is the medium AI and humans share: readable, diff-friendly, git-native. SimaticML is a wire format the converters handle; nobody hand-edits it, and AI never reasons over raw SimaticML when IR exists.

## 4. Vendor-neutral core, vendor adapters at the edges

Everything above the converter layer (IR, patterns, review rules, extractors, AI prompts) is vendor-agnostic and PLCopen-aligned. Supporting a new PLC platform means writing one converter pair, not touching the core.

## 5. Read before write, annotate before generate, generate before modify

Capabilities are earned in order of blast radius. Each stage proves trust the next stage spends. Modifying live logic is the last capability unlocked, not the first demoed.

## 6. Compose from proven patterns, don't freestyle

Generated logic is assembled from a library of tested, human-approved LAD patterns with parameters filled from real tags. Freeform rung invention is the exception, opt-in per request, and reviewed harder.

## 7. Never invent reality

No invented tags, addresses, block numbers, or hardware. Generation is grounded in exported project data. Where something is missing, the AI names the gap and stops rather than papering over it.

## 8. Losslessness is an invariant, not a feature

Round-trip integrity is regression-tested forever. Any converter change reruns the golden suite. A transform that can't be verified by diff doesn't ship.

## 9. Safety logic is untouchable

F-blocks and the safety program are outside the pipeline permanently — enforced by tooling (export refuses them), not by convention. This is not revisited when the pipeline gets good.

## 10. Fail loudly and early

Unknown XML elements, unmapped instructions, ambiguous tags: hard errors, not warnings. Silent best-effort conversion is how a debounce timer becomes a latch. If the pipeline isn't sure, it says so and stops.
