# Stage S3 — Comment generation — full evidence record

> Narrative and acceptance-test evidence for stage S3, split out of `docs/notes/stage-gates.md` on 2026-07-18 per `docs/16-future-ideas.md` FI-20.
> `stage-gates.md` remains the live status index; this file is the append-only
> detail record. Content below is verbatim as it stood in `stage-gates.md` at
> commit d8572c2. Append new S3 entries here, not to the index.

---

## S3 first proof: a real title written through the IR layer, end to end (2026-07-14)

Planned properly before touching anything (two Explore agents + a Plan-agent review, each finding
verified directly rather than trusted secondhand): both network- and block-level Title/Comment were
already fully supported by the write path (`IrParser`/`IrSerializer`/`BlockSourceWriter` → real
SimaticML `<MultilingualText>` emission), but two real gaps existed — no guard against embedded
newlines in Title/Comment text, and no test anywhere for the actual "edit an existing title,
verify the new value is what gets written" scenario (every prior test was parse, fresh
construction, or unchanged-round-trip). Fixed both first: `IrSerializer.EscapeString` now rejects
embedded `\n`/`\r` with a clear error (`docs/04-design-philosophy.md`'s "fail loudly and early");
4 new tests added to `NetworkTitleCommentTests.cs` (network- and block-level edit round-trips, plus
the newline-guard itself). Also fixed a stale comment in `Normalizer.IsVolatile` (`tests/golden/`)
that justified skipping Title content on "always empty" grounds — an assumption S1 items 16/17
already disproved; the skip behavior itself was still correct, just for a different reason than
stated. 485 tests green (370 converter + 14 golden + 101 openness-cli) before touching TIA.

Research also surfaced that the exact live-TIA mechanics needed (export, edit a Title, re-import
with `Override`, clear the known `IsConsistent` refusal via `compile`, re-export, confirm the new
value landed) had already been proven live the day before, on this same block, verifying
`openness-cli`'s own import-overwrite behavior — narrowing what this proof actually needed to
verify to one specific, previously-untested link: editing through the *IR layer* itself.

Target, confirmed with the project owner ahead of time: `TimerSample` (`ir/reference/`) — the one
reference-corpus block with zero real-site lineage (built directly in TIA, not sanitized from
production logic), lowest possible risk for a first write-path proof.

**Hit a real, pre-existing bug immediately, unrelated to the edit itself**: `to-xml` on the
committed `TimerSample.ir` — original file, before any edit — threw `Malformed sidecar constant
line`. Isolated properly (reran the plain `git show HEAD:...` original before assuming anything):
confirmed the same failure on the untouched file, so not a regression from this session's own
Phase 0 changes. Root cause: the exact same "stale sidecar format" class of bug already found and
fixed for `NodeStatusAlarms`/`PerimeterSafetyAlarms` earlier this session — `TimerSample.ir` simply
wasn't swept up in that pass. The sidecar's `constant` line grammar gained a mandatory type suffix
and timer sidecars gained a `kind` field at some point after this file was last generated; the
committed text predates both. Fixed the same proven way: exported `TimerSample` fresh from
`SampleProject`, regenerated the `.ir`, confirmed zero semantic drift against the committed XML
(`Normalizer.AreSemanticallyEquivalent` = true, checked directly) before applying the title edit to
that current-format version instead of the stale one.

From there: title written into the IR for the block's 3 non-empty networks (network 4 is genuinely
empty, no Parts — left untitled, nothing to describe) → `to-xml` → `import` (`Override`) → the
expected `IsConsistent` refusal → cleared via `compile --block TimerSample`, 0 errors (one
unrelated device-level hardware-config warning, the same benign class already documented) →
re-exported → confirmed the new titles are what's actually in the re-exported IR, not just what
was written locally → `Normalizer` confirmed the logic itself is untouched, only Title differs →
full 14-block `RunAll` still passes.

**Presented for review — the project owner caught a real, valid gap before approving**: the
proof only wrote *network*-level titles; the block itself (`FC TimerSample`) was left with no
title at all, despite block-level Title being the same already-proven write path. Not a nitpick —
directly relevant to what S3 is actually supposed to produce. Fixed properly rather than deferred:
added `TITLE "Chained On-Delay Timer Sequence"` to the block header, reran the full live cycle a
second time (import → clear `IsConsistent` → compile clean → re-export → confirm both block- and
network-level titles present → `Normalizer` confirms logic still untouched → full `RunAll` still
passes, all 14 blocks). Committed corpus pair (`ir/reference/TimerSample.ir`,
`simatic-ml/reference/TimerSample.xml`) reflects the block-titled version, not the intermediate
network-only one.

S3's own exit criterion — an undocumented block gets useful comments end-to-end, generated,
imported, compiled, human-approved — is met by this one block. Richer, non-synthetic candidates
(`PerimeterSafetyAlarms`, `NodeStatusAlarms`) are the natural next targets, not part of this proof.

## S3 second proof: `PerimeterSafetyAlarms` — title *and* comment, both levels (2026-07-14)

Explicit instruction this round: title (short) and comment (longer, explains *why*) together, on
whichever block, both block- and network-level this time — the project owner's own direct response
to the block-title gap caught in the first proof. Picked `PerimeterSafetyAlarms` over
`NodeStatusAlarms` deliberately: richer logic (an `OR`-merge of 3 negated safety-zone contacts, not
just a flat bit-mapping) means there's genuine "why" to write a comment about, not just "what" —
matching `06-lad-conventions.md` C-202 ("comments say why, not what").

Learned from the first proof's own stale-sidecar surprise: sanity-checked `to-xml` on the committed
file *before* editing anything, this time — clean, so no repeat of the `TimerSample` detour.

Grounded the actual comment content directly from the real rungs, not from memory of explaining
this same block earlier in S2: bits 0-4 (`AlarmWord1.%X0`-`%X4`) all negate their source
(`SafetyZone1-3`/`SafetyGate1`) — consistent only with those tags reading true-when-intact, so a
break reads false and needs `NOT` to alarm. Bits 5-7 (`PullCord1`/`FireDamper1`/`FireDamper2`) carry
no negation at all — the opposite field-device convention, already true-when-triggered. Bit 0 is a
separate summary coil (`OR` of the three zone bits), not a re-read of an already-computed value —
existing purely so the HMI can show one "perimeter breached" indicator without decoding three bits
individually. Wrote both a short block-level `TITLE` ("Perimeter Safety Alarms") + higher-level
block `COMMENT` (what the block is for), and a network-level `TITLE` + the detailed polarity/summary
`COMMENT` above (the specific why). Network 2 (genuinely empty, no Parts) got neither — nothing to
document.

**Verification needed a different check than the first proof, and this was worked out properly, not
glossed over**: `Normalizer.IsVolatile` deliberately treats Title as ignorable but *not* Comment —
real comment content is exactly the kind of difference that check exists to catch. A plain
`AreSemanticallyEquivalent` call would have correctly returned false here, which isn't a failure,
just not the right tool for "did anything besides my intended documentation change." Instead:
stripped both the original and re-exported XML via `Normalizer.Strip`, then additionally blanked
the `Comment` `MultilingualTextItem` text in both stripped trees, and confirmed the two are then
byte-identical — proving the *only* difference anywhere in the document is the new Title/Comment
text, nothing structural. Full live cycle otherwise identical to the first proof: import
(`Override`) → cleared the expected `IsConsistent` refusal via `compile`, 0 errors → re-exported →
confirmed all four new strings are what's actually in the re-exported IR → full 14-block `RunAll`
still passes. Presented for review (title, comment, and the reasoning behind the comment, so it
could actually be checked against the real rungs) before committing.

## S3 third proof: real JOB9002 content, `PlantAutoControl` (2026-07-14)

First two proofs used the Green-tier reference corpus deliberately, precisely to avoid this
question until it needed answering: the recorded JOB9002 data-boundary approval covered A-01/A-02
spikes, S1 grounding, and S2 (read-only) explanation work, but never S3 write activity. Asked
before touching anything, per `CLAUDE.md`'s own "check the recorded scope, don't extend it
yourself, flag before proceeding" instruction, rather than assuming either way. Extended and
recorded as its own dated entry in `13-data-boundary.md` once confirmed.

Target: `PlantAutoControl`, the same block already extensively read and explained during S2. Genuinely
different shape of gap than the reference-corpus proofs: every one of its 20 networks already
carried a real title from the original engineer, so the first-round gap was the block itself,
which had neither a title nor a comment. Grounded fresh (a new export, not carried forward from
memory) before writing anything, matching the same discipline as the S2 explanation work. Compiled
clean on the first attempt — 0 errors, 0 warnings, a fully-configured real device, unlike the
synthetic scratch project's own benign hardware-config warning seen on the first two proofs.

Presented for review; the project owner then asked for a full redo covering all 20 networks, not
just the block, including replacing the existing titles rather than only adding comments alongside
them — a materially bigger, more consequential change than anything attempted previously (real
production content, an original engineer's own prior work, 20 networks instead of one). Clarified
the exact scope explicitly before touching anything — keep-vs-replace the existing titles,
comprehensive-vs-selective network coverage — rather than assuming either reading of an ambiguous
instruction, given the stakes.

Rewrote all 20 network titles and added a comment to each, grounded in the same verified
understanding built during S2's own explanation of this block, re-confirmed against the fresh
export rather than trusted from memory. Found and fixed two genuine spelling errors already flagged
during that S2 explanation, and otherwise converged close to the original engineer's own naming
wherever it was already accurate — authored fresh rather than either a blind rewrite or a rubber
stamp. Verification scaled up the same Comment-aware structural check used on the second proof:
stripped both the true pre-edit baseline and the fully-redone re-export, blanked all Comment text
in both, confirmed the results are byte-identical — across all 20 networks, the only differences
anywhere are the intended new Title/Comment text. Compiled clean, 0 errors, 0 warnings, on both the
block-only and full-redo rounds.

Per the data-boundary entry's own scope: the real content itself (specific tag paths, exact
comment wording) stays in conversation and inside JOB9002's own gitignored project, not reproduced
here.

