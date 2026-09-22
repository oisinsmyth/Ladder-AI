# THE TEST LOG — what the tooling records about its own runs

**Owner's ask, 2026-08-14:** *"Add a small test log that the tooling records."* This is that format.
**Small is the requirement, not a compromise** — a log nobody reads is a log nobody maintains.

**File:** `docs/notes/test-log.tsv` — committed, append-only, tab-separated, CRLF.

---

## WHY A FILE AND NOT A REPORT

Every result this project produces today lives in a **lane report in a scratch directory** that is
gitignored and disposable. So *nothing survives the session that produced it*, and **the question
"has this ever passed, and when?" is unanswerable** — which is exactly the question a live project
asks first.

Two consequences deliberately accepted:

- **It records that a run HAPPENED and what it said. It is not evidence about the block.** The result
  package is that. The log's job is provenance and history, and it must not grow into a second
  verdict surface competing with the package.
- ***A LOG LINE IS A CLAIM, NOT EVIDENCE*** — the same standing rule as a commit message, and for the
  same reason: it is written in the same breath as the work rather than after verifying it. **When a
  line matters, re-measure; do not cite the line.**

---

## THE COLUMNS

```
utc          RFC3339, second resolution
tool         harness-run | harness-gate | wave-cli | rig-read | download-probe | converter | dotnet-test
target       project or store the run acted on, or "-" 
outcome      one token, the tool's OWN vocabulary - never normalised
detail       counts, verdicts, exit code. free text, tabs forbidden
agent        the agent id that ran it, so a concurrent run is attributable
elapsed_ms   integer, or "-" if the tool does not measure it
```

🔴 **`outcome` is NOT normalised across tools, on purpose.** `NotAdmissible`, `REFUSED`, `HEALTHY`
and `exit 2` mean different things, and a shared vocabulary would flatten distinctions the tools were
built to keep apart — *`PASS`/`FAIL`/`TIMED-OUT`/`UNSETTLED`/`STALE`/`REFUSED` license different
conclusions*, and so do their equivalents elsewhere. **Record the token the tool emitted.**

## THE RULES THAT MAKE IT WORTH KEEPING

1. **Append only. Never edit a past line.** A correction is a **new line** whose `detail` names the
   line it corrects. *A log that can be tidied is a log that can be made to agree with a story.*
2. **A run that was REFUSED, blocked or examined nothing is logged like any other.** *** EMPTY IS NOT
   CLEAN, AND A LOG THAT ONLY RECORDS SUCCESSES IS A LOG THAT CANNOT SHOW A REGRESSION. *** The most
   valuable lines in this file will be the refusals.
3. **`elapsed_ms` is a reference figure, never a gate.** Nothing is scheduled against it. Quoting it
   as a capability number would be dishonest — one rig, one tunnel, a ~72 ms median round trip.
4. **Never log anything from `Live Runs/`** — no restricted path, tag, block or project name. `target`
   for such a run is the literal `live-run-redacted`. The file is committed; docs/13 governs it.

## WHO APPENDS

Whichever lane ran the tool, **at the time it ran it** — not reconstructed afterwards. A lane that
cannot append (no shell) says so in its report and the orchestrator appends, **naming that it did**.
