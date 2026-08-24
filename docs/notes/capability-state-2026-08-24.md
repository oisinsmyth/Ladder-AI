# Capability state — 2026-08-24

**A dated snapshot, not a living document.** It describes the range `dc8308d..063d1d8` and is true as of
`063d1d8`. It will go stale, and it is dated so that a reader can tell. Where it disagrees with the
code, the code wins — every entry below was verified against source rather than taken from a commit
message, because **several messages in this range describe intent more confidently than the code
delivers**. One described a manifest tie as running "in both directions" when it compared names twice.

🔴 **Entries are grouped by the GRADE OF EVIDENCE behind them, not by feature.** Six of them are
correct, complete, and reachable from nothing. A flat capability list hides exactly that distinction,
and it is the distinction this range spent two days paying for.

| grade | means |
|---|---|
| **MET REAL MATERIAL** | run against a committed artifact, a built binary, or real OS processes |
| **WIRED, NEVER FIRED** | on a production path, reached by tests; no live run has occurred since it landed |
| **REACHABLE FROM NOTHING** | built and tested; no production caller |
| **REPAIRED** | existed, was believed to work, did not |

Measured at `442bf2a` (the only later commit is documentation): harness **2,909** passing across 16
assemblies, converter **1,652**, evidence checker **52**, budget script **28**. `git diff --stat
dc8308d..063d1d8 -- ir/ gen/ patterns/ simatic-ml/` is **empty** — no PLC content was produced or
changed in this range.

---

## MET REAL MATERIAL

**Gate 5b — a latch claim must name a block the deployment loaded.** A vector declaring that a signal
is latched by block X now has X differenced against the download's object manifest. A case-only
near-miss refuses and is called out as one; no deployment, or one naming zero objects, reads NOT
CHECKED. ⚠️ *Does not establish:* "this closes **is it loaded**, not **does it latch**" — the latching
itself is still taken on trust. Run on the shipped deliverable: **five hand-authored latch claims, none
verifiable.** It has never returned a green pass on real material.

**The claim registry, raced by real processes.** Twelve rounds of eight OS processes contending one
block number through the built CLI against a real corpus: one winner, every loser's stderr naming the
actual winner. An exit 2 anywhere is a failure, not a quiet pass. ⚠️ *Does not establish:* the
workflow. "Nothing here dispatches an agent… it holds a claim across a long stage, releases it, retries
after a crash, and can simply forget to ask. None of that is exercised." **FI-65 requirement 8 is half
closed** — the primitive, not the workflow.

**Gate 5c — the map's author must not be the block's or a vector's.** Four outcomes demonstrated
through the CLI on a committed corpus, including a refusal on a case-and-space variant of the real
vector author. ⚠️ *Does not establish:* it "does not verify that a session id is real, that a handle
belongs to anybody, or that the party named did the work." On today's committed bindings its verdict is
NOT CHECKED — filling one in truthfully would refuse that wave, which is **owner question D1**, open.

**`harness-batch manifest --check` — three arms, three diagnoses.** Names, per-object content hash, and
the stamp value, kept distinguishable by a `[Flags]` enum so a rename, an in-place edit and a changed
binding do not collapse into one "refused". Reproduced against the pre-fix binary: a coil inverted with
no rename gave `AGREES` / exit 0 before and `CONTENT DRIFT` / exit 1 after. ⚠️ *Does not establish:* the
content arm compares only objects carrying a recorded hash. **No real lane has been produced by it.**

---

## WIRED, NEVER FIRED

**Assertion coverage counts distinct assertions, not vectors.** Nothing counted a numerator before, so
two vectors could cite one assertion and coverage would not move. Per subject, never summed, enumerator
printed beside the fraction. Deliberately **a report and not a gate** — "a threshold is satisfied by
citing whatever is cheapest." ⚠️ Three blind spots printed on every rendering, the sharpest being that
it counts what was **cited at admission, not what passed**. **No result package in the repo carries a
figure** — no wave has run since it landed.

**The block under test enters the build stamp.** Previously the stamp hashed the subject's instance DB
and not the subject: `16#F6276CA9` without, `16#F037F18D` with, both pinned, and the negative half
asserted — with only the instance DB, two programs whose logic differs carry one stamp. ⚠️ *Does not
establish:* the stamp covers a DB's IR **as staged**, its declared start values, while the controller
runs its **actual** values and every member is retentive. **The stamp can move while the plant keeps
the old presets** — the inverse of the failure it fixes, equally invisible.

**The deployment manifest is recomputed rather than cited.** Over-claiming refuses; loading more than
claimed is *incomplete, not wrong*. ⚠️ Its own test header: the fixtures' "object names are invented…
this arm has **never been exercised against a real deployment**." No probe artifact survives either
recorded download — the log directory defaulted to temp. And nothing joins a probe log to a submission,
so pairing them is the operator's act.

**Two caveats stopped being string constants.** Both were assembled from the request before any gate
ran, so they asserted what they could not observe and were byte-identical across five rig events. ⚠️
CLOSED is scoped to the submission, never the system; the device caveat "does not know whether a
gateway has ever run."

---

## REACHABLE FROM NOTHING

**Claiming before writing is binding.** The evidence checker resolves the shared registry, confirms each
claim is held by the declared agent, and joins it to the file on disk in **both directions** — a claim
naming an unlisted file fails, and an edited block no claim covers fails. 🔴 *Does not establish:*
**adoption has never run end to end.** No generation run has gone through a claiming skill and been
checked; the live registry's newest reservation predates the rule by four days. Its five defects were
found by inspection, not by a run.

**Cross-vocabulary identity comparison.** Role labels and instance labels can never be string-equal, so
four independence gates were reporting "different parties" having compared two namespaces. A third
form, `owner:<handle>`, makes a human-authored artifact comparable again. The predicate **throws** on a
cross-form pair rather than returning false, because false is the vacuous pass being removed. ⚠️ The
first real verdict is owed: committed artifacts are deliberately not retrofitted, so it comes from new
material. And `owner:` is "the one D6 answer no keystroke can defeat, and the only one a single
keystroke can manufacture" — eligibility is a stated rule, not a check.

**Seven coordinator types, recorded as unreachable.** Not a new capability — a newly recorded one. 🔴
**The backlog's own proposed check for this defect class would have passed all seven:** "at least one
call site in the shipped assembly" is satisfied by a closed loop, so the check must be forward
reachability from a declared entry point. And the packing rule that says admission precedes slot
storage runs without it — not unenforced, **inverted**.

---

## REPAIRED

**The evidence gate was inverted** — it refused compliant work and passed non-compliant work. Five
measured defects in a gate that shipped the same morning: release-before-verify emptied the store
before the check ran, and the message blamed a store root that was fine; the `.ir` join never executed
on the modify path, so its own proof counter read zero *by construction*; the environment override
failed open while its docstring argued it failed closed; one character of path case printed `VERIFIED`
over an unchecked run; and an instance DB could neither be listed nor omitted. 14 mutations, 13 caught,
**one retained and not rounded up** — no fixture can drive it, and it is what stops the hash exemption
going silent.

**The overlap guard could not fail.** Both race-test files timed the exit in the drain loop, which runs
after every racer is launched, so the assertion was true by construction; serialising the launches left
every test green. Its own comment read "a race that did not race proves nothing" — the comment was
right and the code did not implement it. ⚠️ Limit kept: this removes competition inside the test run
and cannot remove it from other processes on the box.

**The budget gate measured one commit two ways** — normalised bytes on the staged path, raw on the
worktree path, so it was **more permissive on the path a human runs by hand**. Fixing it turned five
pre-existing tests red, correctly: their fixtures had encoded the bug as the expectation. And it
exposed a test green on the wrong number — `"over by 1"` asserted as a substring against a fixture 17
bytes over, passing for six weeks on a prefix match.

---

## What none of this establishes

- **No controller was touched in this range.** No rig event, download or Portal session. Two commits
  exist specifically to retract claims that hardware had been exercised.
- **The deployment gateway has never run.** "A gateway that compiles is not a gateway that deploys."
- **Assertion coverage is unmoved** — the same two and three assertions since 2026-08-18. The
  third-party enumerator exists, is graded by a wired gate, and **has been run once, on one block.**
- **C = 0 is structural.** The delivered plant program has never been executed, so no defect only a
  running controller could expose can appear in any corpus read here. That is an argument for feeding
  the instrument. ***It is not an argument that the plant is clean.***

> **Each correction in this range was sound. Each was needed because the one before it described work
> instead of exercising it.**

Four consecutive passes corrected the pass before them. Not carelessness — **a capability written down
reads identical to a capability exercised, and only one of them holds when someone leans on it.** That
is why this file grades rather than lists, and why the grade is the first thing in every entry.
