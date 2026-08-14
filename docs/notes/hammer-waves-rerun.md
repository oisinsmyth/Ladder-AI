# THE CONCURRENCY RUN, RE-TAKEN ON A COMMITTED HARNESS

**2026-08-14.** The overnight campaign measured wave-cli from 1 to 128 concurrent submitters. Its
drivers were ad-hoc scripts in scratch directories and they are **gone** — `git log` confirms
`tools/hammer-waves.ps1` is the first wave-concurrency driver ever committed to this repository — and
the build has changed substantially since (gate parity rewired, `SlotJoin` inserted before deploy, a
settling wire field added, the six-slot model replaced, `LoopCli.Compose` reworked).

> **Those figures were provenance, not evidence.** This is the run re-taken, on a committed script,
> against today's build.

The harness is `tools/hammer-waves.ps1`. Every store it wrote is still on disk under
`C:\ProgramData\Ladder-AI\wave\hammer\run-*`.

---

## 1. THE HEADLINE, AND IT IS ABOUT THE INSTRUMENT

***The first two versions of this harness each manufactured a clean, plausible, entirely wrong
finding about the tool under test.*** Both were found by running the thing they accused.

| launcher | 2,565,395 bytes of output, one process, no concurrency |
|---|---|
| `cmd.exe` shell redirection to a file **(now used)** | **209 ms** |
| raw `Process` + `ReadToEndAsync` | 184–204 ms for one child; **starves at scale** |
| PowerShell pipe | 353 ms |
| `Start-Process -RedirectStandardOutput <file>` | **2,559 ms** |

**Why it mattered.** wave-cli's colouring report prints **one line per conflict edge**, which is
O(n²) in mutually-conflicting slots — so the edge-bearing shapes emit megabytes and the edge-free
shape emits almost nothing. Through `Start-Process` that difference read as:

> *"wave-cli gets super-linearly slower with conflict edges, and refuses submissions on lease
> timeouts as a result."*

It had a mechanism, a dose-response curve, and 42 refusals in a sustained run to point at. **It was
the launcher.** The second version (async pipe readers) reproduced a quieter version of the same
thing: the pipes a child gets are not overlapped, each async read parks a thread-pool thread, the
pool grows about one thread per 500 ms, and **a child whose pipe fills blocks** — at 16 submitters,
single agents reported 4–5 **second** service times against a 108 ms median.

**The fix was not to tune the readers but to delete them.** Measurement rule 1 is *reconcile against
the store on disk*, so stdout is not an input to any number in the table: it is read **after** the
run, off disk, only to establish that a refusal named itself.

The same-N comparison is what settles it:

| n=16, Simultaneous | old launcher wall | new launcher wall | old svc min/med/max | new svc min/med/max |
|---|---|---|---|---|
| Disjoint (edges) | 6,567 ms | **1,523 ms** | 82 / 126 / **5,302** | 87 / 110 / **140** |
| Conflicting (all edges) | 5,240 ms | **1,512 ms** | 72 / 108 / **4,102** | 82 / 113 / **165** |
| SyntheticDisjoint (no edges) | 1,340 ms | **1,450 ms** | 68 / 80 / 178 | 71 / 82 / 126 |

***Read the edge-free row against the other two.*** It barely moved; the two edge-bearing shapes fell
onto it. At the same submitter count the three shapes now cost **1,523 / 1,512 / 1,450 ms** — the
"conflict edges are super-linear" story was **entirely** the instrument.

⚠️ **A change to wave-cli was written and then not committed.** A buffering stdout writer, with
tests, 401 green, output byte-identical. Under sane redirection the unbuffered binary is 188–193 ms
and the buffered one 209 ms: **it bought nothing, and its whole justification was a property of the
harness.** *A defence built on a guarantee you already have cannot add safety and can subtract it* —
and a comment block full of numbers that mean something else is worse than no comment.

---

## 2. THE RUN TABLE — today's build, committed harness (`ba50664`)

Every row reconciled against `wave-slots.state` on disk. `ach` is **ACHIEVED CONCURRENCY** read back
from the store by `wave-cli status`, never the slot count. `svc` is per-agent elapsed **minus** the
lease queue, which is the only figure comparable across arrival modes. **Duration is reference only
and gates nothing.**

### Simultaneous arrival (parent holds the store's own lease, then releases)

| shape | n | admitted | refused | on disk | waves | ach | wall ms | svc min/med/max ms |
|---|---|---|---|---|---|---|---|---|
| Disjoint | 1 | 1 | 0 | 1 | 1 | 1 | 97 | 79 / 79 / 79 |
| Disjoint | 2 | 2 | 0 | 2 | 1 | 2 | 157 | 79 / 90 / 102 |
| Disjoint | 4 | 4 | 0 | 4 | 1 | 4 | 317 | 69 / 94 / 100 |
| Disjoint | 8 | 8 | 0 | 8 | 2 | 4 | 796 | 100 / 110 / 149 |
| Disjoint | 16 | 16 | 0 | 16 | 4 | 4 | 1,523 | 87 / 110 / 140 |
| Disjoint | 32 | 32 | 0 | 32 | 8 | 4 | 2,933 | 93 / 115 / 216 |
| Disjoint | 48 | 48 | 0 | 48 | 12 | 4 | 5,064 | 97 / 126 / 492 |
| Disjoint | 64 | 64 | 0 | 64 | 16 | 4 | 7,344 | 99 / 149 / 581 |
| Disjoint | 128 | 128 | 0 | 128 | 32 | 4 | 17,795 | 104 / 182 / 854 |
| Conflicting | 1 | 1 | 0 | 1 | 1 | 1 | 69 | 89 / 89 / 89 |
| Conflicting | 2 | 2 | 0 | 2 | 2 | **1** | 175 | 83 / 87 / 91 |
| Conflicting | 4 | 4 | 0 | 4 | 4 | **1** | 419 | 96 / 102 / 127 |
| Conflicting | 8 | 8 | 0 | 8 | 8 | **1** | 721 | 73 / 95 / 117 |
| Conflicting | 16 | 16 | 0 | 16 | 16 | **1** | 1,512 | 82 / 113 / 165 |
| Conflicting | 32 | 32 | 0 | 32 | 32 | **1** | 3,084 | 87 / 110 / 255 |
| Conflicting | 48 | 48 | 0 | 48 | 48 | **1** | 5,112 | 100 / 154 / 459 |
| Conflicting | 64 | 64 | 0 | 64 | 64 | **1** | 8,132 | 101 / 161 / 658 |
| Conflicting | 128 | 128 | 0 | 128 | 128 | **1** | 21,449 | 106 / 214 / 915 |
| SyntheticDisjoint | 1 | 1 | 0 | 1 | 1 | 1 | 110 | 74 / 74 / 74 |
| SyntheticDisjoint | 2 | 2 | 0 | 2 | 1 | 2 | 162 | 58 / 68 / 77 |
| SyntheticDisjoint | 4 | 4 | 0 | 4 | 1 | 4 | 347 | 68 / 76 / 80 |
| SyntheticDisjoint | 8 | 8 | 0 | 8 | 1 | 8 | 760 | 69 / 82 / 92 |
| SyntheticDisjoint | 16 | 16 | 0 | 16 | 1 | 16 | 1,450 | 71 / 82 / 126 |
| SyntheticDisjoint | 32 | 32 | 0 | 32 | 1 | 32 | 2,862 | 64 / 86 / 168 |
| SyntheticDisjoint | 48 | 48 | 0 | 48 | 1 | 48 | 5,095 | 75 / 110 / 387 |
| SyntheticDisjoint | 64 | 64 | 0 | 64 | 1 | 64 | 7,023 | 78 / 107 / 489 |
| SyntheticDisjoint | 128 | 128 | 0 | 128 | 1 | **128** | 15,234 | 70 / 98 / 621 |

### Staggered arrival (40 ms apart, no barrier)

| shape | n | admitted | refused | on disk | waves | ach | wall ms | svc min/med/max ms |
|---|---|---|---|---|---|---|---|---|
| Disjoint | 1 / 2 / 4 / 8 | 1 / 2 / 4 / 8 | 0 | same | 1 / 1 / 1 / 2 | 1 / 2 / 4 / 4 | 106 / 149 / 181 / 356 | med 70 / 86 / 92 / 108 |
| Disjoint | 16 / 32 / 48 / 64 / 128 | all | 0 | same | 4 / 8 / 12 / 16 / 32 | 4 | 516 / 1,071 / 1,486 / 1,962 / 5,794 | med 110 / 113 / 123 / 130 / 139 |
| Conflicting | 1 / 2 / 4 / 8 | all | 0 | same | 1 / 2 / 4 / 8 | 1 | 139 / 147 / 197 / 312 | med 86 / 91 / 106 / 112 |
| Conflicting | 16 / 32 / 48 / 64 / 128 | all | 0 | same | = n | **1** | 602 / 1,059 / 1,704 / 2,314 / 8,167 | med 113 / 126 / 126 / 130 / 170 |
| SyntheticDisjoint | 1 / 2 / 4 / 8 | all | 0 | same | 1 | = n | 105 / 116 / 222 / 377 | med 57 / 71 / 80 / 90 |
| SyntheticDisjoint | 16 / 32 / 48 / 64 / 128 | all | 0 | same | 1 | = n | 558 / 1,058 / 1,498 / 2,398 / 3,776 | med 86 / 97 / 92 / 110 / 106 |

### Sustained — 32 submitters x 8 rounds, accumulating in one store

| shape | submitted | admitted | refused | on disk | waves | ach | total wall | per-round wall, r1 → r8 |
|---|---|---|---|---|---|---|---|---|
| Disjoint | 256 | **256** | 0 | **256** | 64 | 4 | 36,126 ms | 2,851 → 6,884 ms |
| SyntheticDisjoint | 256 | **256** | 0 | **256** | 8 | 32 | 25,008 ms | 2,770 → 3,562 ms |

**2,330 submissions across 56 scenarios. 2,330 admitted. 0 refused. 0 unusable. 0 no-result. 0
unnamed verdicts. 0 vanished. On-disk count equals admitted count in every scenario, with no strays.**

---

## 3. WHAT THE TABLE SAYS

- **No knee to 128, and no crash.** Per-agent *service* time rises from ~75 ms at n=1 to ~100–210 ms
  median at n=128 — roughly 2x across a 128x range in submitters. **The lease serialises cleanly and
  the lease-crash-at-48 found overnight did not recur** at 32, 48, 64 or 128, in any shape.
- **The three shapes cost the same at the same submitter count.** Once the launcher stopped
  interfering, conflict-edge volume has no visible effect on wall time. Compare n=64 Simultaneous:
  7,344 (4-block) / 8,132 (all-conflicting, 2,016 edges) / 7,023 (edge-free) ms.
- ***`ACHIEVED CONCURRENCY` behaves exactly as D9 requires, in both directions.*** N slots on one
  block colour to **N waves of 1 — `SERIALISED`, a correct result** — at every level to 128. Four
  disjoint blocks colour to 4 regardless of submitter count, which is **a ceiling of the corpus, not
  of wave-cli**, and the harness prints it as such. The synthetic shape reaches **128**.
- **Store size costs something; conflict edges cost a little more.** Per-submit, 32 → 256 accumulated
  slots: edge-bearing 89 → 215 ms, edge-free 87 → 111 ms. Real, modest, and reference only.
- **Arrival mode matters and the barrier earns its place.** Simultaneous is 2–3x the wall of
  Staggered at the same n, because it is genuinely all-at-once: `lease-contended` is **yes for
  128 of 128** submitters under the barrier and for 115–118 under a 40 ms stagger.

### The connectivity probes — 112 of 112 refused by name

Every scenario ends with two probes made by an agent that submitted nothing:

- **duplicate slot id** against a slot another agent wrote → refused, *"a slot named 'X' is already in
  the store"*. ***This is the only demonstration that a second agent can see the first agent's
  state***, and it is the wave-store analogue of the campaign plan's §2.1 assertion.
- **submission with no `--cap`** → refused, `WidthCapNotSupplied`.

The store is re-read after both, and its count is unchanged: **a refusal writes nothing.**

---

## 4. AGAINST LAST NIGHT — WHAT STANDS, WHAT CANNOT BE SETTLED

| overnight claim | this run | verdict |
|---|---|---|
| Flat 1→128, **no knee** | Flat 1→128, no knee | **Confirmed** |
| **69–122 ms per agent** | 75–130 ms median to n=64; 98–214 at n=128 | **Consistent** to 64; higher at 128 |
| **Crash at 48** (fixed by keeping the lease file) | 0 crashes at 32 / 48 / 64 / 128, all shapes | **Fix confirmed under a stronger condition** — an arrival barrier, not a launch loop |
| Sustained 32x8 = **256 submissions, all exit 0** | 256/256 admitted, 0 refused, 256 on disk, twice | **Confirmed** |
| 4 disjoint → **CONCURRENCY 4**; 4 on one block → **SERIALISED** | Both, at every level to 128 | **Confirmed and extended** |
| Store scaling **flat** (87 / 104 / 101 ms at 1 / 64 / 256) | Edge-free 87 → 111 ms at 32 → 256 | **Consistent**; the edge-bearing case grows more (89 → 215) |

🔴 ***WHETHER LAST NIGHT'S DRIVERS SHARED THIS LAUNCHER CANNOT BE ESTABLISHED.*** They were never
committed — this is the first wave-concurrency driver in the repository's history — so there is
nothing to inspect. What can be said:

- The **structural** results (colouring in both directions, the crash, refusals by name, nothing
  lost) are **launcher-independent** and stand.
- The **timing** results are *consistent with* this run, which is weak evidence against
  contamination: a driver with this defect would have produced the same false super-linear pattern,
  and **no such claim appears in the overnight record.**
- ***That is an absence of the symptom, not a clean instrument.*** The overnight timings should be
  read as superseded by this table rather than as independently confirmed.

---

## 5. WHAT THIS RUN DOES NOT MEASURE

- **The `--cap` used throughout is AUTO** — set to the submitter count so it does not bound the
  measurement. It is **not** a D29 poll-bandwidth figure and no capability claim rests on it. The
  harness prints that provenance on every submission.
- **The Disjoint shape's ceiling of 4 is the corpus**, not the tool. Nothing here measures achieved
  concurrency above 4 on *computed* closures; the 128-wide figure comes from the
  **`SyntheticDisjoint`** shape, whose closures are **DECLARED** — the shape D9 forbids for real work
  — and every row it produces is labelled `DECLARED-NOT-COMPUTED`.
- **No ceiling was found.** The overnight record put it at "~330 agents at the 30 s lease default,
  set by the caller's own timeout, not a breakdown". This run did not reach a refusal at all, so it
  says nothing about where that is.
- **Wave duration is a reference measurement on one machine (4 logical processors), one build, one
  store medium.** It gates nothing and must not be quoted as a capability figure.
- **Portal was never involved**, and nothing here says anything about concurrent Portal work, which
  is a token held by one lane at a time.
