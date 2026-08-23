# `harness-mirror-view` — a live browser view of the Modbus register mirror

A locally-served page showing **the register map and the current value of every register**, polled off
the rig's `MB_SERVER` mirror. No install, works when the rig is remote, trivially screenshottable.

```powershell
dotnet run --project src\harness\Harness.MirrorView -c Release -- `
  --address 10.10.10.10 --port 503 --unit 1 `
  --map  ir\test-project001\HarnessMirror.ir `
  --area ir\test-project001\FB_Comms_ModbusServer.ir `
  --allowlist "$env:USERPROFILE\.ladder\device-allowlist.json"
```

Then open **`http://127.0.0.1:8137/`**. The same data is at **`/api/mirror`** as JSON — the page has no
other source, so the two cannot disagree.

## 🔴 TWO MODES, AND WITH A WAVE RUNNING YOU MUST USE THE SECOND ONE

**`MB_SERVER` accepts ONE connection per instance.** With this viewer connected to the rig, a conformance
wave against the same device failed with `SocketException: No connection could be made because the target
machine actively refused it` and reported **0 of 22 vectors attempted**. Stopping the viewer fixed it
immediately.

A second `MB_SERVER` instance is **not** the escape: `LocalPort` and the connection `ID` are FB **static
start values**, so a second instance inherits both — two servers on one port with one connection id,
***which is exactly the collision being escaped and which would import and compile without complaint.***

So: **do not open a second connection. The wave is already polling** — 914 round trips in its last run,
each reading the whole control and result region — so the PC already holds the live data. Follow it.

```powershell
# 1. the wave, publishing every read it makes
dotnet run --project src\harness\Harness.Run -c Release -- `
  --submission <submission.json> --binding <binding.json> `
  --program ir\test-project001 `
  --verify --host 10.10.10.10 --port 503 --unit 1 `
  --allowlist "$env:USERPROFILE\.ladder\device-allowlist.json" `
  --publish .\run.mirrorfeed

# 2. the viewer, following it. NO SOCKET, no --address, no allowlist.
dotnet run --project src\harness\Harness.MirrorView -c Release -- `
  --follow .\run.mirrorfeed `
  --map  ir\test-project001\HarnessMirror.ir `
  --area ir\test-project001\FB_Comms_ModbusServer.ir
```

`--follow` and `--address` are **mutually exclusive** and neither is a default.

### *** THE ARGUMENT IS TRUTH, NOT COST ***

Two sockets means **two samples at two different instants**: the page could show a value the harness never
acted on, and the two could then disagree about what the device did. One source means the page shows
**exactly the bytes the harness made its decisions from**.

This viewer already holds that property internally — the page and `/api/mirror` render from one object, so
they cannot disagree. Follow mode **extends the same property across the process boundary**, and that is
deliberate rather than incidental:

- the feed is published **at the moment a read RETURNS**, inside `Harness.Wire.MirrorClient.Read`, which is
  where *every* read in this system lands;
- each frame carries the **raw registers as read** and the **UTC instant that read returned** — never a
  publish time and never a render time;
- **the viewer decodes**, so there is exactly one decoder and the two halves cannot drift;
- 🔴 **nothing on the viewer's side can cause a read.** There is no host, no port, no socket, no fence and
  no refresh on that path. A gateway that refreshed when the page asked, or a publisher that sampled on its
  own clock, would bring the two-instants problem back *minus the second socket* — and it would look
  exactly like the fix. `FollowStructureTests` asserts the shipped assembly never names the publisher.

The **device fence governs the publisher**, which is the process that actually contacts the rig. Fencing a
file read would authorise nothing and would refuse a viewer whose wave is properly authorised.

### A composed picture has rows of different ages, and each states its own

A wave reads the **control region** and **each slot's results** separately, so the picture is composed from
several real reads rather than one FC03 over the whole area. Every row therefore carries **its own
`observedUtc` and its own age**, goes grey **individually** when it passes the window, and a register that
**no read covered** shows **`NOT READ`** — never the zero underneath it. The server's own zero and this
program's default are the same bytes and completely different facts.

A 32-bit element needs **both** halves observed; half a value is not a value, and reassembling one read
half with one default produces a plausible number. The build-stamp card and the scan-counter card refuse on
the same rule.

**Direct mode composes too, above 125 registers.** FC03 carries no more, so a declared area of 300 is three
transactions and one of 1,024 is nine — separate moments on a live mirror, with the scan counter moving
between them. Each register therefore carries the instant **its own page returned**, exactly as in follow
mode, and a 32-bit element straddling a page boundary takes the **older** of its two halves. The source card
states how many transactions composed the reading and says plainly that it is a **reassembly, not a
snapshot**.

Only where the whole area fits in **one** FC03 does every row share one instant — and only there do the
per-row treatment and the whole-table one always agree. That case looks exactly as it did.

## What each row shows

| column | where it comes from |
|---|---|
| register index `0..n-1` | derived from the `%M` address and the `MB_HOLD_REG` base |
| `%M` address | the tag table |
| tag name | the tag table |
| type (`Bool` / `Int` / `Time` / …) | the tag table |
| **raw**, as `16#XXXX` | the wire — **every register's own word, always** |
| **decoded** | the wire + the type |
| comment / meaning | the tag's own `COMMENT` text |

Above the table: the **data age**, the **scan counter** and whether it is **advancing**, the **build
stamp**, and the **last attempt** with its outcome and detail.

## 🔴 The map comes from the committed artifacts. There is no built-in table.

Two files, and they check each other:

- `--map` — the mirror **tag table** (`ir/<project>/HarnessMirror.ir`): names, types, addresses, comments.
- `--area` — the block carrying **`MB_HOLD_REG := P#M<base>.0 WORD <n>`**: the base byte and the declared
  width, which is what turns a `%M` address into a register index.

Every tag must land inside the declared area, no two may overlap, and each tag's address form must agree
with its type name. **Any failure is a refusal (exit 9) and the viewer does not start.** A hardcoded map
would be wrong the first time the copy layer is regenerated *and would look right while being wrong*.

The register arithmetic is the **inverse of `Harness.Map.MirrorGeometry`** — the class the copy layer's
own addresses were generated by — and every real tag is round-tripped through it in the tests. That is a
check against an authority outside this component, not a restatement of its own belief.

## 🔴 A stale value must never look current

| state | when | on screen |
|---|---|---|
| `Live` | the last poll succeeded **and** its reading is inside the freshness window | green |
| `Stale` | nothing failed, but the newest reading is older than the window — the poller is not delivering | amber, values greyed and struck |
| `Failing` | the last attempt produced no reading; values shown are from an **earlier** poll | red, values greyed and struck |
| `Refused` | **the device fence refused the target — no socket was opened** | red |
| `FenceFault` | the fence itself threw; nothing examined the target | red |
| `NeverRead` | no poll has ever succeeded | grey, no values at all |

**Green needs both halves.** Keying on the last outcome alone leaves a dead loop looking healthy; keying
on the age alone leaves a failing poller green until the window passes.

### 🔴 Follow mode adds seven more, and none of them may be merged

*** "NO DATA" AND "OLD DATA" AND "DEAD DEVICE" ARE THREE DIFFERENT FACTS *** — and following a wave splits
the first into three again. Each has its own banner and its own words.

| state | when | on screen |
|---|---|---|
| `NoFeed` | **no publisher has ever written to this path** — no wave has run with `--publish` here | grey, no values. Nothing is wrong |
| `FeedInterrupted` | **something published here and the feed is gone** — a publish did not complete, or the file was deleted | red, no values |
| `FeedUnreadable` | the feed is present and cannot be fully accounted for — truncated, no terminator, a frame count that does not match | red, no values |
| `FeedMismatch` | **the feed and this viewer's map describe different mirrors** — a different width, or the other word order | red, no values |
| `FeedCarriesNoReading` | a publisher has begun and its first poll has not returned | grey, no values |
| `PublisherEnded` | **the wave FINISHED and said so.** The values are FINAL, which is not CURRENT | amber, values kept and struck |
| `PublisherStopped` | **the wave was RUNNING and stopped writing without ending.** It died, was killed, or is wedged | red, values kept and struck |
| `ClockDisagreement` | the feed's instants are in this viewer's future, so an age would be negative | red |

Three of those deserve saying out loud:

- **`NoFeed` and `FeedInterrupted` are not one state.** The first is benign and the second is an incident,
  and they are the *same absence on disk*. The publisher writes a `.initialised` marker **before** its first
  publish, so an absent feed with the marker present means a publish did not complete —
  `File.Replace` is not atomic against process death and leaves a window in which the destination does not
  exist. Reading a lost feed as "nothing ever ran" hands a reader the most reassuring answer available at
  the exact moment something has gone wrong.
- **`PublisherEnded` is never green, not even one second after the wave finished.** *Final* and *current*
  are different facts; nothing further is coming.
- **A live feed of a dead CPU is a real combination.** The publisher is writing, the readings are fresh, and
  the scan counter has not moved: the banner reads `Live` and the scan card reads `NOT ADVANCING`. Those are
  two independent facts and the page shows both.

The viewer polls the feed on its own rhythm and legitimately sees the **same document twice**. That is *not*
two readings: rotating on it would put a reading against itself and report *"the counter read N twice, 0 ms
apart"* — this page's wording for a stopped CPU, as a false alarm about the most serious thing the card can
say. The published instant is the key, and it is identical only when it is the same read.

**The browser adds two more red states of its own**: if a fetch fails, the banner says *CANNOT REACH THE
VIEWER* and greys everything; and if the page's own elapsed time passes the window between fetches it
goes stale without waiting to be told. The age ticks from the server's `ageSeconds` plus **local**
elapsed time — never a wall-clock subtraction across two machines, which a skewed browser clock would
corrupt in either direction.

## 🔴 Three failures, three different words

`REFUSED BY THE FENCE` (a governance decision, no socket opened) · `REFUSED BY THE SERVER` (a Modbus
exception response — the server received the request and turned it down) · `TRANSPORT FAILED` (silence: a
timeout, a dropped socket). **A silence is not a refusal**, and no conclusion here rests on one.

## The scan counter, prominently

A **build stamp is static**: it cannot tell *"the layer is running"* from *"a value from a past download
is sitting in memory"*. A **rising counter can**, and nothing else on this page can. So the counter is a
headline panel with three states, and the third is real:

- `ADVANCING` — with the advance, the interval and the derived ms-per-scan;
- `NOT ADVANCING` — the counter read the same twice;
- **`NOT ESTABLISHED`** — *one reading cannot answer this*. Answering it anyway is exactly how a value
  left in memory comes to look like a running program.

The difference is modular (`Harness.Wire.ScanCount`), so a wrap is a small forward advance, and an
implausibly large one is reported as a counter that went **backwards** rather than absorbed as liveness.

## Decoding

- **32-bit values are HIGH-WORD-FIRST** — measured on this rig off a build stamp with distinguishable
  halves (2026-08-13), re-confirmed over both transports (2026-08-14). The transform is
  `Harness.Wire.RegisterWords`, the same one the harness proper uses.
- `Bool` → one bit of its register; `Int` → signed 16-bit; **`Time` → 32-bit milliseconds across two
  registers**, inheriting the word order, printed as `T#1m30s` with the raw millisecond count beside it.
- **The raw hex is beside every decode**, and each decode states the transform that produced it (hover
  the value column). A decode that hides its bytes hides a wrong assumption.
- ⚠️ A `Bool`'s **bit position inside its register** rests on `Harness.Map.MirrorGeometry.BitAddressOf`,
  which that class marks `[I]` — **inferred, not measured**. The *register* is not in doubt. The decode
  says so and prints the whole word.
- A type this tool has no decode for produces **no value**, not a plausible one.

## Read-only, and how that is checked

This points at a physical PLC, so read-only is a property of the **assembly**, not an intention. The only
wire handle it holds is `Harness.MirrorRead.IRegisterSource`, which carries a read verb and nothing else
and never hands its transport out.

`MirrorViewStructureTests` walks **every method body** in `harness-mirror-view.dll` and asserts:

- no method names a **write member** of `Harness.Wire`, `Harness.Map` or `NModbus`;
- no method touches `System.Net.Sockets.*` directly;
- no method names the write-capable `Harness.Wire.MirrorClient`;
- no method names **`IPAddress.Any` / `IPAddress.IPv6Any`** — the viewer must not be reachable from a
  network interface.

Each with a **denominator** (`BodiesExamined > 0` — `Hits == 0` is true of *nothing found* and of
*nothing looked at*), a **live positive control** (`PlantedWriteControl` really does call
`WriteHoldingRegisters`; `PlantedBindAnyControl` really does name `IPAddress.Any`; the same walk with the
same predicate must find both), and a **resolution control in the target module** (the walk must find the
`RegisterWords.To32` and `IPAddress.Loopback` the shipped assembly certainly does name — otherwise a
module whose tokens resolve to nothing would report a clean sweep). Every predicate is a **parameter**,
so a control can be exercised without editing the check.

This replaces `Assert.False(SomeConst)`. Measured 2026-08-14 on `Harness.RigWrite`: planting a class that
constructed a live socket client left all 202 tests green.

**What it does NOT claim.** `Harness.Wire` *does* contain a write — the harness proper must write vectors
and raise start bools — and this binary references that assembly transitively. The property checked is
that nothing here **names** it. `TheResidualIsReal_…` asserts the write is still in `Harness.Wire`, so if
it ever goes away this description stops being true *loudly*.

## The fence

`DeviceAccessGuard` is consulted **on every poll cycle, above every line that can open a socket**, and
the ordering is observable rather than asserted: `IRegisterSourceFactory` is the only route to a
connection and every refusal test asserts `Opens == 0`. Re-reading the allowlist each cycle is
deliberate — authorisation checked once at startup cannot be withdrawn while a long-lived page is open;
a refusal mid-run drops the session as well as reddening the page.

⚠️ **`DeviceAccessGuard`, never `DeviceWriteGuard`.** The live allowlist entry reads
`"writeEligible": false`, which is correct and **must not block a read**. Consulting the write guard here
would refuse a device properly authorised for exactly what this tool does.

**No allowlist is a refusal, never a pass.** There is no default path.

## Bound to loopback only

Kestrel listens on `127.0.0.1` and `::1` and nothing else. **Observed, not only asserted:** with the
viewer running, all seven of this machine's non-loopback IPv4 addresses refuse a connection on the port
while both loopback addresses answer 200 (2026-08-14).

## What it deliberately does NOT do

- **It is not a debugger and not an online-monitoring tool.** No stepping, no breakpoints, no forcing, no
  watch tables, no cross-reference.
- **It cannot write anything** — not a register, not a start bool, not a tag.
- **It sees only the mirror.** Anything not copied into `%M` by the copy layer is invisible to it: DB
  contents, IO images, block-local data, the block logic that produced these values.
- **It says nothing about whether the values are CORRECT** — only what they are, when they were read, and
  whether the program producing them is executing.
- **It does not check the area's width.** That is `harness-mirror-read`'s job, from both sides; a wide
  read failing here is reported as a failing poll, not as a boundary finding.

## Flags

| flag | default |
|---|---|
| `--address` | **required** |
| `--map` | **required** — the mirror tag table `.ir` |
| `--area` | **required** — the `.ir` carrying `MB_HOLD_REG` |
| `--allowlist` | `LADDER_DEVICE_ALLOWLIST`, else refused |
| `--port` | `503` (this rig; `502` is refused there) |
| `--unit` | `1` |
| `--http-port` | `8137`, loopback only |
| `--poll-ms` | `1000` |
| `--stale-after-ms` | `3 ×` the poll interval, minimum 1000 |

## Exit codes

| code | meaning |
|---|---|
| 0 | ran and was shut down |
| 2 | usage — the arguments do not describe a viewer; nothing contacted |
| 9 | **the MAP could not be built from the artifacts** — refused, no fallback table |
| 10 | could not bind the HTTP listener |
