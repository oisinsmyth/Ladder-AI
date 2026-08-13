# ACCEPTED FINDINGS IN THE REFERENCE CORPUS — real, known, and deliberately not fixed

**Owner ruling, 2026-08-13.** This file exists for one reason: ***`converter preflight` and
`converter review` do not come back clean on the reference corpus, and that is a DECISION rather
than a backlog.*** Without this record the next reader sees findings, assumes neglect, and "fixes"
them — losing the thing the corpus is for.

> **Where a reader of a non-clean `preflight` should look: here, first.** If a finding below matches
> what you are seeing, it is known and accepted. If it does not, it is new and worth chasing.

**These are REAL findings. Accepted is not the same as wrong.** Every entry below would be a genuine
defect in a block being delivered. What makes them acceptable is *where they are* — a reference
corpus is a record of what a real project looked like, and correcting it makes it a record of what we
wished it had looked like.

---

## 1. `DefaultTagTable.ir` — 58 × C-001, accepted

**What the tool reports:** 58 C-001 (*error*) findings — TIA's placeholder tags `Tag_1 … Tag_54`
sitting at **real IO addresses**.

**Why they are real:** C-001 requires a physical IO tag to be
`<DI/DQ/AI/AQ><n>_<Equipment>_<Signal>`, built on the frozen equipment identifier (C-004). `Tag_17`
is not that, and on a delivered project it would be a defect — the name carries no equipment, no
signal and no direction, so nothing downstream can be read against it.

**Why they are accepted:**

- ***THEY ARE TIA'S OWN PLACEHOLDERS, NOT SOMEBODY'S NAMING CHOICE.*** TIA creates `Tag_n` when an
  address is used before a name is given. They are an artefact of how the project was built, and
  they are exactly the shape of thing a real export contains.
- **The corpus is evidence about reality, not a specimen of good practice.** Renaming them would
  make the reviewer skills validate against a corpus no real project resembles — and the reviewer
  skills are the thing this corpus exists to validate.
- **They are already doing useful work as a negative example:** a review run that *fails* to report
  them has a hole in it. Removing them removes a test.

**Consequence to accept knowingly:** any whole-corpus C-001 count is dominated by these 58, so
**quote C-001 counts excluding `DefaultTagTable` or not at all** — an unqualified total is an
uninformative number that will be quoted anyway.

---

## 2. The UDTs — C-201 and C-003 findings, accepted

**What the tool reports:** C-201 (comment quality) and C-003 (prefix/naming) findings against the
corpus UDTs.

**Why they are real:** both rules say what they say, and the UDTs do not satisfy them.

**Why they are accepted:** the same argument as §1, plus one of its own — **these are the rules'
own calibration data.** C-201 and C-003 were written *against* site practice, and the corpus is
site practice. A rule that its own source material fails is either a rule that has moved beyond the
site deliberately (which is the case here — see docs/06's *"generated code answers to a stricter
bar than existing site practice"*) or a rule that is wrong. **The findings are what makes that
distinction visible, and deleting them hides it.**

---

## The standing rule this file encodes

> ***A FINDING IN THE REFERENCE CORPUS IS NOT A TASK. Do not "clean up" `ir/` to make a tool go
> green.*** A green run against a corrected corpus proves the corpus was corrected; it proves
> nothing about the tool.

**If you believe a finding here should be fixed, the question to answer first is: what does the
corpus stop being able to demonstrate once it is?** Then ask the owner. **Do not fix it in passing.**

**Not covered by this file:** findings against `patterns/`, against generated output in `gen/`, or
against any block being prepared for delivery. Those are ordinary defects and are fixed normally.
This acceptance is scoped to the **reference corpus** and to the two items above.
