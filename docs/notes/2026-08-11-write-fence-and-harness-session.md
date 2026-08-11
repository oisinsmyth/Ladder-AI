# Session record — 2026-08-11: the write fence, the harness, and what is actually proven

Written at the end of the session, immediately before the conversation was cleared. Assume the
reader has none of the context that produced this.

## The one-line state

Two substantial pieces of tooling now exist and **190 tests pass**. But:

> **Nothing in the harness chain has ever moved a bit on a PLC.** One read, of one value, through the
> fence, is the entire sum of what has been proven against reality.

The rest of this document is organised by *evidence level* rather than by feature, because the gap
between "built and green" and "known to work" is the thing most likely to be misread later.

---

# 1. Proven end to end, against the real world

- **The read fence authorised a real target before a real connection.** `device-guard check` returned
  ALLOWED against a real allowlist file, and that authorisation preceded the socket opening. First
  live device read this project has done, and it was governed rather than ad hoc.
- **An S7 connection to a real CPU 1214C over a routed VPN**, rack 0 / slot 1, using Sharp7. The
  order code came back and matched the declared value exactly.
- **A substantial tested NEGATIVE: device identity is unobtainable on this CPU.** Four routes closed
  by measurement, not assumption — see §5.
- **Openness has no live-value access.** Reflection over the installed V20 assembly (2,182 exported
  types), independently re-verified by a second agent, and corroborated from the other side: the
  internal `Openness.OnlineServices.Interfaces.dll` in `Portal V20\bin` contains only Upload,
  Download and Legitimation interfaces. The public API's silence is a designed boundary, not a thin
  wrapper hiding riches.
- **Licence inventory**, decoded from Automation License Manager's own logging databases (the `.ekb`
  key files are encrypted; ALM's SQLite/SQL-CE stores hold the short-name→product mapping in clear):
  **no PLCSIM Advanced licence, no OPC UA licence.** STEP 7 Professional V19+V20 floating, Safety
  Advanced V20, WinCC Comfort V19+V20, WinCC Unified PC ES V20. Test Suite not installed. S7-PLCSIM
  *Standard* needs no separate licence — it is covered by STEP 7 Professional.
- **Live network findings** — see §6, and treat them as the reason the fence was redesigned.

# 2. Built and unit-tested — but has never met a device

**This is the largest category and the easiest to mistake for "done".**

- **`DeviceWriteGuard`** (`src/device-guard/`) — seven gates, 21 tests. Has never refused or allowed
  a single real write.
- **`VectorRunner`** (`src/harness/`) — 19 tests. Has never run a vector against a PLC; only against
  an in-memory `FakeTransport`.
- **`S7Transport` and friends** (`src/harness/Harness.S7/`) — 108 tests, every one against a fake S7
  client. No device was contacted while building it.

# 3. Decided, but never enacted

- **ADR-0009 is accepted** and no write has happened under it.
- **The per-run scope model** is implemented and has never scoped a real run.
- **The restore-point precondition** is enforced in code, and no restore point has ever been
  captured, let alone verified.
- **No device has ever been granted write eligibility.** The one allowlist entry that exists is
  deliberately `writeEligible: false`.

# 4. Explicitly unverified — six bench measurements

Each is a short test, not more research. The session's web-search budget was exhausted (200/200), so
these can only be settled at the machine.

1. Does an S7-protocol client reach a **PLCSIM Standard (Softbus)** instance? Undocumented by
   Siemens either way. **Note the experiment must be reshaped**: TCP 102 is already held at baseline
   by `s7oiehsx64.exe` (the S7DOS Help Service), so a port scan answers "yes" regardless. The real
   question is whether S7DOS routes a connection through to a Softbus instance, and the pass
   criterion must be a genuine S7 response carrying simulated-CPU data.
2. Does PLCSIM's **Pause** freeze virtual time and timers, and does it drop the TIA online connection?
3. Does **Trace** work against a PLCSIM-simulated S7-1200?
4. Does **minimum-cycle-time padding** count against the maximum-cycle watchdog? The one relevant
   Siemens sentence hints *yes*, against the earlier inference.
5. The **default communication load** percentage — readable straight off the CPU properties.
6. The **default trace sample event** — likewise.

---

# 5. The finding that reshaped the design: identity is unobtainable

The write fence was originally specified to authorise on **address**. That is unsafe here, and the
reason is concrete rather than theoretical: on this engineering PC, `10.10.10.10` is a *standard* PLC
address across multiple site networks, several of which are reachable at once. Which physical
controller answers depends on which VPN tunnel is up — and that can change mid-session.

So the fence was redesigned to authorise on **device identity**, verified on every connection.

**Then identity turned out not to exist.** Four routes, all closed by measurement:

| Route | Result |
|---|---|
| S7 `SZL 0x0011` (module identification) | **Works** — returns the order number, twice, and nothing else |
| S7 `SZL 0x001C` (serial, plant designation) | Refused at every index tried |
| `GetCpuInfo`, `GetProtection`, `ReadSZLList` | Refused / not implemented |
| Openness API | **No `Serial` member anywhere** in 2,182 exported types |
| MAC via ARP / PROFINET DCP | Unavailable — the tunnel is *routed*, not bridged; no ARP entry for the target, only the gateway |
| CPU web server | Not reachable |

**An order code is a model number, not a device identifier.** Every CPU 1214C returns the same
string, so verifying it catches "a different *kind* of box answered" and not "a different box of the
same kind" — which is precisely the hazard.

**Root cause:** Sharp7 and every free S7 library speak **classic S7comm**, the S7-300/400 protocol.
The 1200/1500 accept it only as a compatibility path; their native protocol is **S7comm-plus**, which
is undocumented and carries session/integrity protection. That single fact explains the refused SZLs
*and* why optimized data blocks are invisible.

The S7comm-plus implementation **is on this machine** — `OMSp_core_managed.dll`, 11 MB in
`Portal V20\bin` — but it is internal, has no contract, changes between updates, and using it would
be reverse engineering under the EULA. **The protocol being on your disk does not make it an API.**

**Consequence:** identity must be *supplied by the program*. A marker DB (a known block holding a
unique string) is the near-term answer; publishing the CPU's real I&M0 serial via `Get_IM_Data` is
the better one, and needs PLC code.

# 6. Live network findings — keep these

- **Overlapping subnets, both populated.** The PC holds `10.10.10.241/24` on a local interface while
  a VPN tunnel injects `10.10.10.0/24` at metric 0. The tunnel wins. There is a real device on the
  local segment (`10.10.10.254`). If the tunnel drops, the same address silently resolves elsewhere.
- **Adapter names lie.** The tunnel was carried by an adapter labelled for one VPN vendor while
  actually belonging to another's client — proven by process parentage, not by the label. A fence
  keyed on "which VPN am I on" by adapter name would have been wrong at that moment.
- **`plcsim_ndislwf` is bound and enabled on `OpenVPN TAP-Windows6`**, which is a live trap: the
  filter blackholes traffic on newly created VPN adapters, and recovery needs the binding removed
  *plus* a full VPN client restart. The adapter that happened to carry the session was a previously
  remediated one — **safety by luck, not configuration**. Unbinding it is outstanding.
- The recorded "this PC fakes all TCP 80/443 connects" quirk **did not apply** over the tunnel — an
  HTTP attempt failed cleanly. Re-check that note before relying on it.

---

# 7. What was built

**`src/device-guard/` — the write fence (ADR-0009).** `DeviceWriteGuard`, structurally separate from
the read guard so no code path leads from a read-only entry to a write authorisation. Seven gates,
each failing closed: usable target and area → read gate passes → separately write-eligible → outputs
asserted physically isolated by a named person → **identity matches** → area within the run's declared
scope and any device cap → **verified restore point exists**. Does no I/O, which is what makes every
refusal path testable without a PLC.

Two subtleties worth not losing: an entry declaring *no* identity cannot be satisfied, and an
identifier the device did not report is a **mismatch, never a pass** — silence is not agreement.

**`src/harness/` — the vector model and runner.** The declarative shape `docs/08` already specified,
plus three fields experience added: `Basis` (the spec clause — a vector that cannot cite one is not
admissible, because that is what keeps conformance vectors distinguishable from regression vectors),
`Observability`, and `Kills` (the plausible wrong implementation it catches).

The runner does two things that make it more than a test loop. It **refuses what it cannot observe** —
a coincidence assertion with no event scan-stamps, a transient with no latching, a scan wait with no
scan counter — reporting `NotObservable`, which makes a run **not green**. And it **waits by observing
a scan counter, not by sleeping**, because an S7-1200 cannot be stepped and the host is not real-time.

Write scope is **derived from the vector set**, so the fence's per-run declaration is exactly what the
tests touch. `Harness` deliberately holds no reference to `DeviceGuard`.

**`src/harness/Harness.S7/` — the composition root.** Where the runner and the fence meet. `IS7Client`
abstraction (deliberately a *reduction* of Sharp7's surface — no `PlcStop`, no `Download`, so a test
runner that can stop a CPU is not constructible), a symbolic↔absolute tag map, a big-endian codec, a
pluggable identity-source list, and the first restore-point store that can say yes. Identity is
re-read on every connection including reconnects; the guard is consulted before every write and its
decision travels out intact.

# 8. Governance that moved today

- **ADR-0009 accepted** — write access to test rigs. The gate is the **target**, not the kind of
  write: on an allowlisted rig whose outputs are physically incapable of actuating, *every* write
  class is permitted; on a device in service, none is.
- **`10-non-goals.md` #3 re-scoped** from a blanket permanent write ban to that target gate.
- **`10-non-goals.md` #4 re-scoped** from "no autonomous operation, ever" to two clauses: **(a)** no
  promotion across an environment boundary without review — iterate freely inside one; **(b)** no
  write without a **verified restore point** captured first.
- **Hard rule 1** scoped explicitly to PLC program content.
- **Hard rule 5** amended — the real project is human-gated, everything before it is yours; do not
  import into the real project *without permission*.
- **Stage S9 waived for live projects only**; the pipeline project keeps the gate.
- **ADR-0008 carries a superseded-in-part notice** — it declared the write ban permanent and named
  write appetite as explicitly *not* a revisit trigger, and was overtaken the next day.

# 9. The verification pass — eight claims refuted

Two agents audited every claim made during this and the preceding day's work, against local primary
sources (the PLCSIM V20 Operating Manual and TIA V20 Information System shipped on this machine, the
Openness assembly by reflection, the PLCSIM Advanced V7.0 API header). **The conclusions survived;
eight supporting statements did not.** Corrections are merged.

- **PLCSIM Advanced does not simulate S7-1200 in any version** — quoted verbatim from §2.3
  *"Unsupported CPUs"* across seven editions including V8.0. G2 gains support at V8.0/TIA V21; the
  classic family never does. A licence would change nothing. *Trap:* V8.0's supported-CPU table lists
  a "CPU 1214C" — in the **G2** row, because G2 reuses classic type names.
- `InitializeMemory` does **not** wipe retentive data; its entire documented description is *"This
  datatype is used to initialize memory."* **`DataBlockReinitialization`** is the destructive one and
  worse than recorded — it takes retain data *and* forces a PLC STOP.
- "An unanswered download configuration blocks or throws" is too strong; only ones that would
  *prevent* the download throw.
- `GetAccessibleDevices` is on `ConfigurationPcInterface`, not `ConnectionConfiguration`.
- `StationUpload` returns an `UploadResult` carrying an `UploadedStation`, not a `Device`.
- The Openness trace hook under the safety validation assistant is a **V21** page. **V20 Openness has
  no trace item at all.**
- S7-1200 Trace supports **2** installed traces, not 21 (16 signals each).
- Minimum and maximum cycle time were conflated: 1–6000 ms is the **maximum** range, default 150 ms;
  the minimum is "1 ms to the maximum" and is **disabled by default**.
- Work memory is **revision-dependent for the same order number**: 150 kB / 14 kB retentive at
  current firmware, 100 kB / 10 kB on older revisions. Never quote it without the firmware.

---

# 10. Lessons

**Absence of a prohibition is not permission.** Reflection over an assembly revealed
`RegisterCustomInstance(vplcDll, …)` and a `Vplc1200.dll` on disk, and that was read as an opening.
The same manual excludes the CPU family in plain words. Reflection gives ground truth about *shape*,
never about whether the vendor supports the thing.

**Reporting drift is its own failure mode.** Several refuted claims were cases where the raw
reflection output was correct and the *prose written from it* was not — a member attributed to the
wrong type, a return type simplified, enum values described as methods. The dump was right; the
sentence was wrong. Quote the tool, don't paraphrase it.

**The label lies.** An adapter named for one vendor carried another's tunnel. This is the same class
of error as trusting an IP address to identify a device, and it appeared twice in one session in
different guises.

**Check what a device will actually tell you before a design rests on it.** The fence was built
around identity verification, and only afterwards did it emerge that no unique identifier is
readable. The design survived, but only because identity could be manufactured; a different
dependency would have collapsed it.

**A suite can be green because nothing was looked at.** The single most useful property in the
harness is that it refuses to evaluate what it cannot observe. A vector asserting on a one-scan event
against a transport with a 100 ms floor is not a passing test; it is a test that never ran.

**A rule restated in five documents costs five edits per change, and the restatements drift.** The
audit checklist was auditing a hard rule that had ceased to exist a day earlier. When a rule moves,
grep for every restatement in the same commit.

**The bootstrap gap is a recurring shape.** The first write cannot be governed by the mechanism it
installs — the marker DB that enables identity verification must itself be downloaded without it.
Name these explicitly and hand them to a human rather than weakening the mechanism to fit.

**Operational:** a git worktree held by the orchestrating session **breaks subagents' shell access** —
two agents lost Bash and PowerShell mid-run and could only report it in passing. Do not hold a
worktree while agents run. And a stopped agent cannot be resumed; relaunch with its findings as a
head start.

---

# 11. What is left

**Blocked on `lad-coder`** (the agent was unavailable for most of the session; its definition file is
valid, so this is a registry-load problem, not a content one):
- The **marker DB** — a standard-access block holding a unique rig identifier. *Must be
  non-optimized*: classic S7comm cannot see optimized blocks at all, so an optimized DB makes the
  identity mechanism fail silently.
- Then: a **free-running scan counter**, **event scan-stamps**, and an **I/O-layer external-injection
  mode**. The runner already reports vectors needing these as unevaluable, which is the honest state.
- Eventually: publish the CPU's real I&M0 serial via `Get_IM_Data`, retiring the hand-typed marker.

**Tooling, unblocked:**
- A `write-check` verb for the `device-guard` CLI (only `check`, the read path, exists).
- A JSON loader for vectors (the model is the contract; serialization was deliberately deferred).
- **A latent bug found by review, not yet fixed:** `VectorRunner.WaitScans` computes
  `ReadScanCounter() - start`; a UDInt counter wrapping at 2³² makes that hugely negative and the
  wait spins to its poll limit and errors. Not a false pass, but baffling. The S7 transport absorbs
  it by returning a monotonic value, but **the runner is the layer holding the assumption.**

**Decisions outstanding:**
- **How Sharp7 should be referenced.** It is deliberately *not committed* — it sits at
  `%USERPROFILE%\.ladder\lib\Sharp7.dll` behind an overridable path property, and if absent the
  adapter file is excluded and everything else still builds. Three options (vendored DLL / NuGet /
  vendored source — it is one MIT `.cs` file) are laid out in `src/harness/Harness.S7/README.md`.
- **Where the real device allowlist should live.** Currently outside the repo. Outside keeps
  environment-specific config out of version control; inside would answer ADR-0009's *"how did this
  address get on the write list"*.
- **Whether to buy an OPC UA licence.** Its real advantage is not speed — the 100 ms floor is fine
  for almost every vector — but that it works with **optimized** block access, so the DBs the harness
  touches would not have to be converted to standard access and lose per-tag retentivity granularity.
  Check first whether those DBs are *already* standard access; if so the advantage largely evaporates.
  **Do not buy PLCSIM Advanced** — the CPU family is excluded at any price.

**Still owed from a previous session:** six documents (`01-scope`, `02-roadmap`,
`08-testing-strategy`, `09-risk-register`, `12-glossary`, `16-future-ideas`) still treat "does PLCSIM
Advanced support S7-1200?" as an open risk (R-07). It is now answered — *no, in every version* — and
three of them gate stage S9's entry on resolving it.
