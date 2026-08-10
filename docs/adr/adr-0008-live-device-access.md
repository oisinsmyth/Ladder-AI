# ADR-0008 — Read-only live-device access, test rigs only

- **Status:** **Accepted in principle (read-only, test rigs) — 2026-08-10; build scope still open.**
  The owner narrowed `10-non-goals.md` permanent exclusion #3 the same day so it no longer forbids
  read-only online access (the write bans stay permanent). So question 1 below is answered **yes**.
  **Still undecided and blocking any build:** the *fence* (question 2 — how "test rig" is enforced)
  and the *surface* (question 3). **Still true regardless:** the tooling has **no** path to a device
  at an IP yet — nothing has been built — so until it is, the working answer to "read the log off
  10.10.10.15" remains "export it to disk yourself, then hand me the file". **Hard rule 6 resolved:**
  the owner chose to **remove** CLAUDE.md hard rule 6 outright (2026-08-10) rather than reword it — the
  blanket "No hardware access" is gone; a tombstone holds slot 6 so references to hard rules 7–8 still
  resolve. The permanent *write* ban now lives solely in `10-non-goals.md` #3.
- **Date:** 2026-08-10
- **Relates to:** CLAUDE.md **hard rule 6** (no hardware access; "you must not try to add support") ·
  CLAUDE.md **hard rule 2** (never touch safety) · `10-non-goals.md` Permanent #3 (download to
  hardware — *not touched by this ADR*) and "Not now" (version-comparison against PLC online state;
  hardware/network configuration via Openness) · `13-data-boundary.md` (what the log may contain) ·
  the immediate trigger: a request to read a WinCC trace log off a test-rig HMI to debug its
  JavaScript.

## Context

The concrete request that prompted this: an HMI on a **test rig** (not a production line) is running
JavaScript that needs debugging, and the trace log on the device is where the fault shows. The
engineer would rather debug against the rig than a live plant network — a sound instinct — and asked
for that access to become a real, permanent capability rather than a one-off verbal exception.

Two facts block that today, and they are different in kind:

1. **Policy.** Hard rule 6 forbids hardware access outright and forbids *building* support for it
   ("you must not try to add support"). It is stated with no exceptions. A chat-line override does not
   change a checked-in governing rule; changing it is what this ADR is for.
2. **Capability.** Nothing in the repo reaches a device at an IP. `openness-cli` talks to the TIA
   Portal Openness API against a **project on disk**, never out to a live controller or panel. Even
   with the rule lifted, the capability would have to be *built* — a network client, a credential
   story, a target-identity story — which is exactly what rule 6's second clause forbids creating.

So the decision is not "may I run a command". It is "do we open a new class of capability — the
tooling talking to a live device — and if so, how tightly is it fenced". That is an ADR-sized
decision, and the fence is the whole design.

**One boundary is not on the table, and this ADR must not appear to move it.** `10-non-goals.md`
item 3 — *download to hardware, online edits, tag forcing* — is a **permanent** exclusion, "never
revisited". Everything below is **read-only**: pulling a diagnostic artifact (a trace log, a runtime
log file, exported diagnostics) *off* a device. No write to a device, in any form, is proposed here,
and acceptance of this ADR does not create a route to one. Whether *write* access to test-rig
hardware should ever exist is a separate and heavier question — it would require reopening a
*permanent* exclusion, which this project has said it does not do — and it is explicitly **out of
scope** for this document.

**The safety rule is also untouched.** Hard rule 2 stands verbatim: no safety content is read off a
device any more than it is read from an export. A trace/log read must refuse and name safety-tagged
content, never silently include it — the same fail-closed posture the export path already takes.

## Decision

**Question 1 — decided yes (2026-08-10).** Read-only live-device access, restricted to test rigs,
moves into scope: `10-non-goals.md` #3 was narrowed the same day so its permanent ban is now the
*write* direction only (download / online-edit / tag-force), and read-only online access is explicitly
not excluded by it. **The remaining questions are open and each blocks a build:**

2. **How is "test rig" defined and enforced** — so the capability cannot be pointed at a production
   device by mistake or by drift? (Options under *The fence* below; this is the crux, not a detail.)
   *Recommendation: the allowlist floor, fail-closed.* **Undecided.**
3. **At what surface** — the minimal "fetch a named diagnostic/log artifact to disk" only, or a
   broader read capability (online-state comparison, upload-for-inspection) as the "Not now" line on
   online-state comparison anticipated? *Recommendation: option 2, the minimal fetch, to serve the
   trace-log case first.* **Undecided.**
4. **Hard rule 6 — decided: removed (2026-08-10).** Rather than reword it, the owner removed CLAUDE.md
   hard rule 6 entirely. The blanket "No hardware access" no longer exists; a tombstone holds slot 6 so
   the repo's many by-number references to hard rules 7 and 8 keep resolving (renumbering was rejected
   as too wide a blast radius). The `lad-coder` sub-agent's local hard-rules recap was updated the same
   way. The permanent write ban is unaffected — it lives in `10-non-goals.md` #3.

## The near-term use case, concretely

Debugging HMI JavaScript from a trace log needs exactly one thing the repo cannot do today: **get the
trace artifact from the device onto disk.** Everything after that — reading the log, correlating
errors to script objects/lines, proposing a fix — is ordinary in-scope software work on a local file
and needs no new capability at all. `openness-cli hmi --screen` already reads script bodies and
`SyntaxCheck()` already locates syntax faults *from a project*, so the analysis half is mostly built;
the missing half is purely the fetch.

That narrowness matters for scoping: the smallest useful capability is **"fetch a named diagnostic
artifact from an allowlisted test-rig device to a local path, read-only"** — not a general device
shell. Option 2 below is built around that.

## The fence — how "test rig only" is made real

This is the load-bearing part. "Test rigs only" enforced by good intentions is not a fence; the whole
value over a blanket rule is that the tooling *cannot* be aimed at production. Realistic mechanisms,
weakest to strongest:

- **Explicit device allowlist (config file), fail-closed.** A committed/opt-in registry of
  `(address, label, "test-rig")` entries; any target not on it is refused, and an empty registry
  grants nothing. Mirrors the `LaunchedInstanceRegistry` and the multi-agent claims-dir posture
  already in this repo ("empty is not clean" — FI-44). Cheap, auditable, and the failure mode is
  refusal, not accident. **Recommended floor.**
- **Network/subnet confinement.** Only addresses on a designated rig subnet are permissible. Stronger
  in theory, but this PC already routes to a 10.10.10.0/24 that is *not* obviously a lab segment
  (the earlier reachability check traced production-looking public hops), so subnet alone would not
  have distinguished the rig from a plant here. Use as a *second* gate, not the only one.
- **A positive "this is a rig" assertion recorded per session/target**, logged with who asserted it —
  because ultimately only a human knows whether a box is a rig, and the registry entry is that
  assertion made durable and reviewable rather than re-typed each time.
- **Read-only enforced structurally**, not by discipline: the client exposes *fetch/read* verbs only
  and links no write/download/force capability at all — so "read-only" is a property of what was
  built, not a promise about how it is used (the same reasoning `Validate()` failed in ADR-0007: a
  gate you can bypass is not a gate).

None of these is safety-grade; a test rig is chosen precisely so that a mistake is survivable. The
fence's job is to make the *routine* case impossible to get wrong, and to leave an audit trail when
it is overridden.

## Options considered

1. **Accept — broad read-only live-device access on allowlisted test rigs.** Fetch logs/diagnostics
   *and* online-state reads/upload-for-inspection. Buys the most, commits to building and maintaining
   a live-device client and its whole identity/credential story, and widens the blast radius of any
   bug in that client to every readable object on the rig.
2. **Accept — minimal "fetch a named diagnostic artifact to disk", allowlisted test rigs, read-only,
   nothing else.** Directly serves the trace-log case and no more. Smallest new surface; the fence is
   a single allowlist; the analysis stays on local files where the existing rules already apply. The
   guard rail is a written scope decision, because on a live connection "fetch a log" is a short step
   from "read anything".
3. **Reject — keep hard rule 6 whole.** The answer stays "export the artifact off the device yourself
   (Unified RT web client → diagnostics/trace export, or copy the runtime log files), then give me the
   path." Costs the engineer a manual export step each time; costs the project nothing to build or
   maintain; keeps the "the tooling never talks to a live device" invariant intact and simple.
4. **Defer — park as a future idea (16-future-ideas.md) pending a real second occurrence.** Debug this
   log via the manual export in option 3 now; only build if the manual step proves a recurring tax.
   The project's own rule ("build the skill on the *second* manual run") points here unless the owner
   already knows this will be routine.

## Consequences

**Accepting (options 1–2) opens the first channel from this tooling to a live device, and that is a
genuine category change:**

- **A new invariant is retired.** "The tooling only ever touches project files on disk" has been true
  since S1 and is part of why the safety and review guarantees are easy to reason about. Reads are far
  less dangerous than writes, but the category — *code in this repo initiating a connection to a live
  control-system device* — is new, and every future reviewer inherits it.
- **The fence is now a maintained thing.** An allowlist that is easy to add to is easy to add a
  production device to. It needs a review discipline of its own, and "how did this address get on the
  rig list" has to have an answer.
- **Credentials/trust enter the repo's threat model.** Even read-only device access implies auth to
  the device. Where those live, how they are scoped, and that none of it is ever committed
  (`13-data-boundary.md` retention rules) become live concerns, not hypotheticals.
- **Data boundary applies to whatever comes off the device.** A trace log can carry tag names, operator
  text, and identifying strings. If the rig mirrors a real job, the log is treated under the
  `Live Runs/` retention rule (use freely, commit nothing); if it is synthetic, ordinary rules apply.
  Either way the fetched artifact lands in a job/scratch path, never the knowledge base.
- **Safety stays fail-closed (hard rule 2).** The fetch/read path must refuse and name safety-tagged
  content, never silently include it. This has to be built into the client, not assumed.
- **Option 2 keeps all of the above small**: one artifact, one direction (device → disk), one fence.
  It does **not** avoid the retired-invariant or the credential concerns — those come with *any* live
  read — but it avoids owning an online-state model and a general device browser.

**Rejecting (option 3) costs:**

- **A manual export step every time**, and the friction the engineer explicitly wanted to remove.
- **Nothing else.** No new invariant, no fence to maintain, no credentials in the model. The analysis
  work (debugging the JS from the exported log) is fully available today.

**Deferring (option 4)** costs the same manual step once and defers the build decision to evidence —
appropriate if it is genuinely unclear whether this recurs.

**Hard rule 6 was removed, and that shifts where the write ban is enforced.** The blanket rule is
gone; the *only* remaining statement of the permanent write ban (no download / online-edit / tag
force) is `10-non-goals.md` #3. That is deliberate and sufficient — #3 is a permanent exclusion — but
it means the hard-rules list no longer restates it, so any future reader who treats the hard-rules
list as the complete operational contract must be pointed at #3. The tombstone at slot 6 does that.

**A fence must still exist before any read is built** — removing hard rule 6 lifted the *prohibition*,
it did not create the *safeguard*. Until the test-rig allowlist (or equivalent fence, question 2) and
a read-only-by-construction client exist, there is nothing that stops a live read from being aimed at
a production device, so no live read should be performed yet. The removal unblocks the build; it is
not itself permission to connect to arbitrary hardware.

**Dangling references to "hard rule 6" now exist in secondary docs** (e.g.
`docs/notes/hmi-ai-design-options.md`, `docs/audit/README.md`) plus historical/evidence/changelog
mentions that correctly record what was true when written. The historical ones are left as-is; the
live design/audit references should be reconciled in a follow-up sweep, not silently.

**Either way, one thing is already true and should be recorded:** the request has been made and the
capability gap is real. Rejecting the *build* still requires saying so explicitly (and keeping the
manual-export answer documented), not leaving the question to be re-litigated each time it comes up.

## Revisit triggers

Revisit if any of: the manual-export step (option 3) proves a recurring tax across several debugging
sessions (option 4's own trigger); a second, different live-read need appears (online-state
comparison bites, per the existing "Not now" line); or the owner decides test-rig debugging is going
to be routine enough that the fetch capability pays for its fence. **Not** a revisit trigger: any
appetite for *writing* to a device — that route is closed by a permanent exclusion and is not reopened
by this ADR.
