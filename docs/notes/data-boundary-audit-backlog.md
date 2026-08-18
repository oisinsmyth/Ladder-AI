# DATA-BOUNDARY AUDIT BACKLOG — findings deferred, not dismissed

**Opened 2026-08-18.** This file exists so a finding that was consciously *not acted on* still meets
the next audit. Everything here is a **deferral with a named decision-maker**, never a backlog item
that quietly accumulated.

> 🔴 **THIS DOCUMENT DELIBERATELY DOES NOT LIST THE IDENTIFIERS IT IS ABOUT.**
> Writing them down here would commit the very strings the finding is about, into a new committed
> file, in the name of recording that they are committed. **The finding is reproducible by running
> the check below**, which regenerates the list on demand from the job folder — where the vocabulary
> already lives and is already ignored. *A record of a leak must not be a copy of it.*

---

## AB-1. Plant-specific identifiers in committed source — DEFERRED BY THE OWNER, 2026-08-18

**Found:** a repo-wide sweep on 2026-08-18 matched **82 sites across 15 tracked files** against the
vocabulary of the live job `JOB9004`. Bare job codes are **not** the issue — `docs/13-data-boundary.md`
permits those explicitly, and they were deliberately restored the same day. The issue is
**block, type, DB and member names** specific to that site's plant.

**Where.** The file list is stable and is safe to record, because a path names no site:

```
CLAUDE.md
CHANGELOG.md
docs/16-future-ideas.md
docs/notes/compile-error-playbook.md
src/converter/Converter/CrossCheck/ProjectUsageGraph.cs
src/converter/Converter/Ir/SidecarSynthesizer.cs
src/converter/Converter/Program.cs
src/converter/Converter/UndrivenScan/UndrivenScanRunner.cs
src/converter/Converter.Tests/ConvertOutputSafetyTests.cs
src/converter/Converter.Tests/DbConverterTests.cs
src/converter/Converter.Tests/MultiInstanceCallTests.cs
src/converter/Converter.Tests/NamedTypeExpansionReadBackTests.cs
src/converter/README.md
src/device-guard/DeviceGuard.Tests/DeviceWriteGuardTests.cs
src/harness/Harness.Tests/VectorRunnerTests.cs
```

**Owner's decision (2026-08-18):** *do not remediate now; record it for the next audit's attention.*
Grounds, as far as they were stated: no git remote is configured, so nothing has left this machine,
and the remediation touches `CLAUDE.md` and `CHANGELOG.md` — a rewrite with real cost and real risk
of destroying load-bearing explanation. **That is a deferral of the CLEANUP. It is not a finding that
the content is acceptable, and it must not be read as one.**

### How this got in, which is the part worth keeping

Every lane involved was briefed that nothing from the job folder is committed, and every lane honoured
that **for artifacts**. The identifiers arrived in **explanatory comments** — as the concrete case a
comment reaches for when it explains a defect it actually measured. ***That does not feel like job
content; it feels like rigour.*** The same mechanism produced an earlier, smaller instance
(13 files, remediated 2026-08-17) — so this is a **recurrence, not a one-off**, and the recurrence is
the argument for a mechanical check rather than a briefing.

Note also *where* it recurred: the 2026-08-17 remediation swept `src/harness/`, because that is where
that day's work happened. This finding is mostly in `src/converter/` and `docs/`. **A sweep scoped to
the lane that was working catches the lane; only a repo-wide sweep catches the repo.**

### The check — re-run it, do not trust this list's age

```bash
# 1. The job's vocabulary, from the job folder (gitignored — it never enters the repo)
ls "Live Runs/<JOB>/ir/"*.ir | xargs -n1 basename | sed 's/\.ir$//' | sort -u > /tmp/job.txt

# 2. Everything Green-tier, so conventional names do not read as leaks
{ ls ir/*/ patterns/*/ gen/*/ simatic-ml/*/ ; } | sed 's/\.[a-z]*$//' | sort -u > /tmp/green.txt

# 3. Names the job has and the Green corpora do not
comm -23 /tmp/job.txt /tmp/green.txt | awk 'length($0)>=8' > /tmp/candidates.txt

# 4. Whole-word match against TRACKED files only
grep -n -w -F -f /tmp/candidates.txt $(git ls-files '*.cs' '*.md' '*.py' '*.ps1' '*.json')
```

### ⚠️ THE CHECK IS A CANDIDATE GENERATOR, NOT A VERDICT — and it cannot be made into one

Step 3 yields names absent from every Green corpus. **That is not the same as "specific to a
site".** Measured on this run: names like the DOL-motor block, the motor UDT, and the alarm
category FCs came out as "job-only" purely because no `.ir` file of that name is committed — yet they
are **conventional**, some of them *mandated by the convention rules themselves*, and one appears in
the S6 sandbox's own architecture document, written weeks before this job existed.

Separating *"this job happens to use the standard name for a motor starter"* from *"this is the
site's own equipment"* is **a judgement about what identifies a plant**, and no name-matching rule
reaches it. So:

- **Treat the output as a worklist to triage, never as a count to report.** A raw match total
  overstates the leak, and quoting one as a headline would be its own small dishonesty.
- **The plant-specific subset is the real finding**, and on this run it was roughly half the
  candidates. Establish it by asking, per name, *"could this plausibly be the standard name for this
  kind of object on somebody else's plant?"* — if yes it is convention, if no it is the site's.
  The subset is not written down here for the reason given at the top of this file.
- Same shape as the rest of this project's tooling: **facts, not verdicts.** Automate the assembly of
  the inputs; leave the call to a person.

### What would actually close this

Not a bigger grep. The durable fix is the one recorded as `M-5` in
[`mechanisation-backlog.md`](mechanisation-backlog.md): **run the sweep before any commit during a
live run**, so the question is asked while the comment is being written and the author still
remembers whether the concrete case was load-bearing. Retrofitting it afterwards means re-deriving
intent from prose, which is why this deferral is cheap to record and expensive to discharge.
