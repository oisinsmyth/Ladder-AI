# LIVE-PROJECT READINESS — read this first

**Written 2026-08-14, overnight, at the end of the hammer campaign.** The owner's ask was: *have the
tooling ready to use in the morning on a live project.* This is the answer, and it is written to be
**read once, quickly, before you start** — not to be complete.

> 🔴 **THE ONE-LINE ANSWER.** The **deployment, read-back and analysis** path is ready and has been
> run against a real controller. The **closed-loop test path is NOT** — no conformance wave has ever
> executed end to end. Use the first. Do not build a plan on the second today.

---

## USE THESE — proven against the controller, not just against their own tests

| | what it is proven to do |
|---|---|
| **Deploy** (`Harness.Device`: stage → `to-xml` → `import-all` → layout re-assert → `compile-all` → `sanity-check`) | **45 objects loaded by name**, CPU read back `Running (8)` **from the device** |
| **`rig-read`** | Run state, device identity, DB read. **Needs a device allowlist or it opens no socket at all** |
| **The mirror + copy layer** | Deployed and **read back over the wire**. `Bool`→bit/1 reg, `Int`→word/1, `Time`→double word/**2** |
| **`download-probe`** | The only binary that can transfer a program. **Fenced; exit 3 before Portal is contacted** on a refusal |
| **The converter's analysis set** | `preflight`, `drift-check`, `compare`, `review`, `tagstatus`, `cross-check` — the daily working tools |
| **The claims registry** | **Verified connected by observing a refusal**, not by assuming one. See the trap below |

## DO NOT RELY ON THESE TODAY

| | why |
|---|---|
| **The conformance loop end to end** | ***A WAVE HAS NEVER RUN.*** The 27 vectors were authored, admitted as far as the gates, and never executed. There is no result package in existence |
| **The phase-armed latch** | **Not built.** The generator now **refuses by name** rather than emitting the unconditional form — *which would silently delete any finding that turns on a signal FALLING* |
| **Wave duration as a planning figure** | Measured on one rig over one tunnel. **Reference only.** Nothing is scheduled against it |
| **The permit half of the write fences** | Tonight's fence work attacked **refusals**. The permit path is `NOT CHECKED` |

---

## 🔴 THE FOUR TRAPS THAT HAVE EACH COST A DAY

**1. `--claims <dir>` must be the SHARED ROOT — `C:\ProgramData\Ladder-AI\claims` — and NOT the
project folder.** The tool appends the project name itself. Passing `…\claims\<project>` yields a
**second, empty store that grants every claim**, and it is invisible because *two agents making the
same mistake agree with each other perfectly.* ***READ THE `store=` LINE THE TOOL ECHOES. DO NOT TRUST
THE ARGUMENT.***

**2. Portal is a token, not a component.** One lane at a time. Two Openness sessions on one project
has already produced `Collection was modified` with every block reporting inconsistent.

**3. A bare whole-device `compile` is not the gate.** Use `sanity-check` — and read **both** the
`BLOCKS:` and `TYPES:` lines.

**4. Re-assert `--set Standard --yes` after EVERY import of a block that must be readable over classic
S7.** A re-import silently reverts it to `Optimized`, `drift-check` is structurally blind to it, and
the block goes invisible on the wire while every check stays green. **It bites on the second import.**

---

## WHAT THE CAMPAIGN FOUND, THAT YOU SHOULD EXPECT TO MATTER

- 🔴 ***COMMITTED WAVE SETS WERE BEING LOST SILENTLY, AND THE SURVIVING SUBMISSION REPORTED SUCCESS.***
  Found by killing agents mid-write. `File.Replace` is not atomic against process death; a reader
  landing in the window read the store as **empty** — *correct exactly once, before anyone has ever
  submitted, and catastrophic every time after.* **Fixed and demonstrated in the live race** (90 kills,
  guard fired 30 times, zero loss). ***If you ever see a wave store report zero slots after a
  submission has been made, STOP — do not resubmit*** — the newest `.tmp-*` beside it is a complete
  store and restoring it has been shown to recover cleanly.
- ⚠️ **A killed agent LEAKS ITS SLOT permanently.** The colouring stays consistent and fails in the
  safe direction, but there is **no withdraw verb**, so clearing one dead agent's slot currently costs
  **every other agent's submission**. **Known, raised, deliberately not fixed overnight** — it is a
  change to the multi-agent ownership contract, not a defect.
- ***THE LEASE CRASHED AT 48 CONCURRENT AGENTS AND WAS CLEAN AT 8, 16 AND 32.*** Fixed. Now measured
  flat to 128 (69–122 ms per agent across a 16× range, no knee). **The practical ceiling is the
  caller's timeout — roughly 330 agents at the 30 s default — and the failure there is a NAMED
  retryable refusal, not a breakdown.**
- **There is no stale-lease state to detect.** The lock is the OS handle; a killed holder releases
  immediately. **A hung holder and a busy one are indistinguishable, permanently** — if a wait is long,
  that is a fact for a human, and the tool is no longer allowed to claim otherwise.
- **A slot set that all drives one FB instance is ONE slot, not N.** If you submit N slots against one
  block and see `SERIALISED`, ***that is a correct result, not a failure.***
- **The gate count is 25**, not the 23 quoted in the test plan.

## HOW TO READ A GREEN FROM THIS TOOLING

This is the habit the whole project is built around, and it is worth thirty seconds:

1. ***EMPTY IS NOT CLEAN.*** A check that examined nothing must not exit 0, and most of this
   codebase's worst defects were exactly that. If a result looks clean, **ask what its denominator
   was.**
2. **`NOT CHECKED` is not a pass.** It is printed separately, before the gate list, deliberately.
3. ***A LOG LINE IS A CLAIM, NOT EVIDENCE.*** `docs/notes/test-log.tsv` records that a run happened
   and what it said. **When it matters, re-measure — do not cite the line.**
4. **Prefer the artifact to any report about it** — and not to your own model of what it contains
   either.

---

## THE STANDING MEASURED FACTS

Rig `10.10.10.10:503` unit 1 via the Talk2m tunnel; `:102` open, `:502` refused. Scan **23.33 ms**.
Round trip **min 63 / med 72 / max 106 ms**. **32-bit word order is HIGH-WORD-FIRST — measured off a
build stamp with distinguishable halves.** Mirror: **35 registers at `%M1000`, exactly full, zero
spare.** ***S7 variable access is refused CPU-wide on this rig, so every data read goes over Modbus.***
Harness objects reserve block numbers **9000–9999** per number space; **OBs excluded.**

⚠️ **X-D's compression ceiling of ~4.3× is HALF-MEASURED** — the scan is measured, `k ≈ 5` is X-D's own
number and has never been. Treat it as *derived from one measured and one assumed input.*

## WHERE TO GO NEXT

| for | read |
|---|---|
| What the tooling is vs what the spec says | `spec-reconciliation.md` |
| What must be tested, and what deliberately is not | `tooling-test-plan.md` |
| The build history and why the vectors did not run | `test-environment-build-plan.md` (**CLOSED** — *it quotes the implementation; a vector author must not read it*) |
| What has actually been run, and when | `test-log.tsv` |
