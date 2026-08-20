# harness-cmd-inject

The write half of the virtual panel: a Modbus command-injection client for a bench PLC.

**This build can open a socket and write.** Phase 1's could not, deliberately, and its structural tests
said so by counting to zero. Those counts have moved up a level rather than been deleted: an IL walk over
the compiled assembly now asserts that **exactly one** method names the transport's write member
(`InjectionDispatch.ApplyEach`, the chokepoint every command, enable and restore write passes through),
**exactly one** names a write member of the wire stack (`ModbusInjectionTransport.WriteRegisters`), and
**exactly one** constructs a session (`ModbusInjectionTransport.Open`, which is therefore the only place
the calibrated `ModbusPolicy` could be got wrong). One count is unchanged and must stay a zero: nothing in
this assembly names `System.Net.Sockets`. `Harness.Wire` owns every socket, every FC03/FC16 bounds check
and the retry decision; a second client here would be an uncalibrated one.

`Retries = 0` is the setting that matters and it is **not** the library default. NModbus retries three
times. A retry after a timeout can duplicate a request whose original still landed — which on a command
injection is a second command executed, indistinguishable at this end from the first having been lost.

## It knows the protocol and must not know the map

Register numbers, tag names, channel names, command codes and result codes are live-run restricted data.
None of them is compiled in. The tool holds **roles** — which register plays `seq`, which plays `ackCount`,
which plays `enable` — and loads the map at runtime from a **binding file in the job folder**. The binding
says, in the job's own vocabulary, which tag plays each role; the map (the committed mirror tag table plus
the `MB_HOLD_REG` area pointer) says where each tag lives and what type it is. Run `map` to see the whole
resolution printed for a person, offline, before anyone arms anything.

There are exactly four registers the tool addresses without being told to: **0–1, the build stamp** and
**2–3, the free-running scan counter**. Those are protocol, not map — the copy layer's allocator places
them there for every job — and a check whose own address came out of the binding could not catch a binding
that describes the wrong program.

Every test in this project uses invented vocabulary (`UNIT_A`, `CH1_Seq`, `LP_Beat`, `%MW2000`). Not one
job value appears in the source or in a fixture.

## One connection

`MB_SERVER` accepts a **single** connection, and this is measured, not read off a datasheet: a viewer
attached alongside a running wave produced `SocketException: actively refused` and **0 of 22 vectors
attempted**. So:

- reading and writing share one session, because every command is write-then-poll-for-acknowledgement and
  a writer that cannot read is not half a loop — it is a tool that can never tell you whether anything
  happened;
- there is no `watch` verb and there will not be one. Publish a feed and point `harness-mirror-view
  --follow` at it;
- **do not run `harness-mirror-view` against the rig while this tool holds the socket.** Whichever
  connects second gets nothing, and the failure looks like a dead device rather than a busy one.

## What it refuses, and why

**The binding, against the map** (`map` and `send` both resolve first, and refuse by name):

- a tag the map does not carry;
- a tag whose type is not what the role requires (a `seq` that is not a `UInt`);
- a command member that lands outside the command band, or an ack member outside every observation band;
- two roles sharing a register — one command member writing over another;
- a channel whose command members are not contiguous, or whose sequence is not at the low address (the
  sequence is written alone and last, and an operand below it would be written *after* it);
- a declared band that disagrees with the union of what resolved into it;
- a command band **wider than one FC16** — refused before a socket is opened, because a band that cannot be
  put back in one write cannot be reversed, and a restore split across two writes can be interrupted
  between them.

**The device, before any write** (`InjectionFence`, consulted above every line that could open a socket):

- no allowlist configured — a **setup** error, not a governance decision about the device, and reported as
  such;
- an allowlist that cannot be read or parsed;
- a target that is not an approved test rig;
- a rig that is not marked **write-eligible** — a *human* authorisation nothing here may edit or stand in
  for: a person changes the allowlist, or the answer is no;
- outputs not asserted physically isolated, or an isolation nobody is named as having asserted;
- no expected build stamp declared.

**The device, after connect and still before any write:**

- a published build stamp that is not the declared one. An address is only a routing hint —`10.10.10.10`
  is the standard PLC address at multiple sites and which controller answers depends on which tunnel is
  up — so the CPU is confirmed by the stamp it publishes at registers 0–1. A mismatch means the map this
  tool is holding was derived for a different program, and **every address it would write to is a guess**.
  The comparison is repeated on **every poll batch**, because a download can land between the write and
  the acknowledgement. (Where the acknowledgement registers sit within one FC03 of register zero that
  re-check is genuinely free; where they do not it costs a second read, and the run says which case it is
  in. It is never skipped.)
- a master enable that reads **clear**. The block acts on a command only while the enable is set, so the
  command would be written and ignored — and an ignored command is indistinguishable from a rejected one
  at this end.

**The values:** an integer operand that does not fit its element is a refusal by name, never a truncation.
What it does **not** refuse is command-code or operand *validity*: that knowledge is job data that changes
with the plant, and a second copy on the PC would drift from the block and eventually refuse what the block
would accept. The tool sends what you ask and reports what comes back.

**And it never resends.** Not on a timeout, not on a restart, not on a failed read, not on Ctrl-C. A
command whose fate is unknown is reported as unknown; sending it again is how one command becomes two.

## Reversibility: the restore point, and one thing it cannot promise

The restore point is **the command band as this session's own opening read found it** — not a file, not a
previous run's idea of it. It is captured before the first write, and it is put back on **every exit path
that wrote anything**: success, refusal after a write, a thrown exception, and Ctrl-C (the interrupt
handler cancels the poll rather than killing the process, precisely so the restore still runs). The restore
then **re-reads the band and verifies itself**. A restore that cannot be verified is an **error**, not a
warning, and it outranks every other outcome — a run that acknowledged perfectly and could not put the band
back exits 19.

The order is fixed and is tested: **drop the enable first, then write the band, then re-read.** Restoring a
live command surface is the failure mode. The captured image carries the sequence value from *before* this
session, and a sequence *inequality* is exactly what makes the block execute — so writing that image back
under a live enable would re-execute a stale command. The band is therefore written with the enable bit
held clear.

The heartbeat register is inside the band, so it goes back with everything else. **Whether putting it back
changes anything about the block's own state is not something this tool knows, and it relies on neither
answer** — it stamps again next session, which is why the arming step is unconditional.

⚠️ **The consequence, stated plainly rather than buried: the enable is left DOWN, so the device is not
left exactly as it was found, and the enable cannot persist between invocations.** A separate `enable`
verb would have its own session, and its own restore would drop what it had just raised. A `send` that
needs the enable up must therefore raise it inside the same session — `--raise-enable`, off by default,
because raising a plant's master enable is not something to do because a flag was omitted. There is no
ordering in which a one-write restore both puts an old sequence back and leaves the surface live without
risking that execution; the choice is which of the two to give up, and reversibility of the *values* plus
an inert surface is the safer half.

## Two different gates, both called arming

There are **two** gates and confusing them is how the tool shipped unable to do its job:

- **`--arm` is OUR gate.** It decides whether this binary may write at all.
- **The heartbeat is THE BLOCK's gate.** It decides whether the thing on the other end will act on what we
  wrote.

A run can pass the first and fail the second. Until the heartbeat step existed that is what *every* run
did: the role was declared, parsed, resolved, type-checked, band-checked and printed by `map`, the write
target had a factory — **and the factory had zero callers.** Nothing ever wrote it. On the rig that
surfaces as a command refused as unarmed, reported honestly as `NotAcknowledged`, with no path to any other
answer and no indication which end was at fault.

### The tool's gate

Every write verb requires `--arm`, and its presence is read **before** the verb is parsed, so flag order
cannot change the answer. Without it, `send` prints the plan and the exact frames byte by byte, constructs
no transport, and exits 10 — printing `current: NOT READ` rather than inventing a before-picture, because a
dry run that opened a socket "just to read" would turn the arming gate into a decoration. The dry run
prints the **arming plan** too: it is the only description of the writes a real run makes before the
command, and leaving it out would have the dry run describe the smaller half of what `--arm` does.

### The block's gate

**The arming path, in order, inside one session:**

1. the enable is up — found up, or raised by `--raise-enable`;
2. the enable is **read back from the device**, because a heartbeat written while it is clear lands in
   memory and never reaches the block, and from this end that is indistinguishable from a write that
   worked. A clear enable stamps *nothing* and refuses;
3. stamp: one register, one value;
4. **wait for the scan counter to advance**, read from the control registers this session already reads
   and already trusts;
5. repeat 3–4 until the plan's changes are made — the wait happens after the **last** stamp too, so the
   block has sampled it before the command's sequence arrives;
6. *then* read the prior acknowledgement count, allocate a sequence, and write the command.

**Why the scan wait rather than a delay.** The block samples the heartbeat once per scan and compares it
with what it read last time, so two writes that land inside one scan are **one** change — or **none**, if
the second put the first's value back. Wall-clock spacing makes separation *probable*; it does not make it
*observed*, and this tool does not ship probable. The counter is already on the wire and already carries
the restart check, so the separation costs a read and is a fact rather than an assumption. A counter that
will not advance is `ScanStalled`: a refusal that names the reason and writes no command, never a further
stamp issued hopefully.

**Why every stamp is a change by construction.** The values are generated from what the session's opening
read *found* in the register: the first differs from that, and each one after differs from the one before.
**There is no method anywhere in this tool that takes a heartbeat value**, so "the client wrote the same
number twice" — which satisfies a write count and arms nothing — is not a mistake that can be made at a
call site. Any inequality counts; nothing depends on the sequence being monotonic, and the resting value
zero is skipped because a change *to* the value a never-written register serves is the one change a
liveness gate might decline to count.

**Why it is unconditional.** Nothing readable reports whether the device is already armed, so a "do we
need to?" branch would be a guess — and a wrong guess is a command that cannot succeed against a device
that looks healthy. Redundant stamps are cheap. What this buys is the case that matters: a run works
**from cold**, against a device nobody has touched since it started scanning.

**There is no keepalive, and there is not going to be one.** No background thread, no liveness loop.
Arming is stamped inside the session that needs it and this tool maintains nothing between invocations.

⚠️ **Two numbers here are parameters and not constants, because the code does not settle them.**
`--heartbeat-changes` defaults to the number the protocol model requires — the model's number, not one read
off a block — and `--heartbeat-scans` defaults to **2 rather than 1**: one advance would be enough if we
knew where inside a scan the counter is incremented relative to where the heartbeat is sampled, and nothing
available to this tool establishes that. Two is the smallest number sufficient whatever that answer turns
out to be, and it costs at most one extra read against a round trip already an order of magnitude longer
than a scan. `--heartbeat-scan-attempts` bounds the wait (the first read is the baseline, so fewer than two
is refused). The gap *between* those reads is a code-level parameter left at zero and deliberately not
given a flag: each attempt is itself a round trip, so there is nothing to add a wait on top of.
`--heartbeat-changes 0` opts out entirely, and the run says so rather than looking like a run that armed.

**What arming this against the bench rig actually takes**, in order, none of which this tool can supply
itself:

1. a person edits `tools/…allowlist` so the rig's entry reads `"writeEligible": true`. No flag stands in
   for that field, and nothing in this binary writes it;
2. the same entry asserts `outputsIsolated` **and names who asserted it** — field wiring disconnected or
   interposing relays unpowered. This is the one gate no software bug can cross;
3. `--expect-stamp <the stamp the deployed build publishes>`, obtained from the build that was downloaded
   — `harness-mirror-view` reads it off the device, and it is also the stamp `Harness.Map.BuildStamp`
   derived at generation time;
4. `--arm` on the invocation;
5. `--raise-enable` if the enable is not already up when the session opens — **required for the arming to
   work at all**, not only for the command: the heartbeat reaches the block through the same gate;
6. **separate** consent for the safety-permissive substitution. `--arm` authorises injection; it does not
   stand in for a safety contact, and nothing here writes that register today.

## The command is two transactions

Operands and code first; the sequence register **alone**, second. The sequence sits at the low address of
each channel, so a single write of the whole channel applied in address order would land a new sequence
*before* its operands — executing a command against the previous one's operands, silently and plausibly.
Split, a tear between the two can only leave a new sequence against operands never written, or new operands
under an old sequence; the block acts on neither until a fresh sequence appears. Tearing cannot manufacture
a command. The cost is one round trip.

## The acknowledgement model is evidence about the client, never about the PLC

A poll has four outcomes and no fifth — `Acknowledged`, `Pending`, `Superseded`, and the incoherent cell
(the device echoes our sequence but its processed-count did not move). It is keyed on the **count**; the
result code and the **echoed command code** are read, printed verbatim and never branched on. Both are
carried as a `string` so a call site comparing one to an integer would not compile.

The code echo is the fourth acknowledgement member and it earns its place: the result register is **held**
until the next command is processed, so the code beside it is the only thing that says *which* command a
held result belongs to. It is reported and never reasoned from, for a second reason as well — **a refusal
cascade publishes one value and the check written last wins**, so one refusal value in a protocol of this
shape masks every other reason a command was declined. A reader who saw it and concluded "the cause was X"
would be right only by luck about everything except X.

Two things follow that are easy to get backwards, and both are tested:

- **"the result register did not change" is not evidence that nothing happened.** The block holds its
  result until the next command it processes, so an unchanged result is evidence of nothing at all. What
  separates *refused* from *never seen* is the echoed **sequence**.
- **a fresh success whose result register still reads the previous command's value is still a success.**
  Every fact on the wire is true and reading the result gives you the opposite of what happened. This case
  is the reason the verdict is keyed on the count, and it is the case the whole design exists for.

🔴 **And one thing this suite used to get wrong, recorded because it is the failure this project exists to
avoid.** Every protocol test used to arm the model by calling a helper on it directly — *the test harness
supplying the exact capability the tool was missing*. Ninety-nine tests passed over a client that could not
arm anything, and the first place that could have failed was the rig. Every command in that file is now
armed **by the client, inside its own session**; if the client stops being able to arm, those tests stop
passing. The two helpers that still write the heartbeat directly are named
`PokeHeartbeatBypassingTheClient` and `ArmBypassingTheClient`, so a call site cannot use one without saying
what it is doing, and they are used only where the *device's prior state* is the subject — never to get a
command through.

🔴 **The protocol model in `FakePlcProtocolTests` is evidence about the CLIENT and never about the PLC.**
The model and the client were written from one document by one hand. A green run says only: *given a device
that behaves as the document says, this client draws the right conclusion.* It says nothing about whether
the device behaves that way — a client checked against a model built from its own source of truth is a
self-consistent check, and this repository has a recorded instance of two components failing together
because they read the same register. **The loop closes on the rig and nowhere else. Nothing here may be
written up as "verified" until it has run there.**

## The test layers

| layer | where | what it licenses |
|---|---|---|
| L1 pure decisions | `Harness.CmdInject.Tests` | binding resolution, frame building, sequence ledger, ack classification, poll planning |
| L2 recording port | same | **opens == 0** on every refusal and dry run; write ordering; band containment; restore ordering and self-verification |
| L3 protocol model | `FakePlcProtocolTests` | the client's conclusions **given a device that behaves as described** — nothing about the device |
| L4 IL walk | `InjectionStructureTests` | the chokepoint counts above, each with a denominator and a live positive control |
| L5 real Modbus over loopback | `Harness.CmdInject.Loopback.Tests` | the printed frames are what a real server **received**, through real PDU encode/decode over a real socket |

L5 is a separate project on purpose: the pure suite must stay socket-free, because its value is that
"nothing opened" is asserted against a counter on a port that *cannot* open anything. Its listener binds
loopback and an ephemeral port — nothing is reachable off the machine, and no fixed port can collide with a
real service.

## Verbs

```
map  --tags <path> --area <path> --binding <path>
     Resolve the binding against the mirror map and print it. OFFLINE — takes no address.

send --tags <path> --area <path> --binding <path> --channel <name> [--set <role>=<value>]...
     [--target <host>] [--allowlist <path>] [--expect-stamp <v>] [--port n] [--unit n]
     [--raise-enable] [--poll-attempts n] [--poll-interval-ms n] [--arm]
     [--heartbeat-changes n] [--heartbeat-scans n] [--heartbeat-scan-attempts n]
     Without --arm: print the plan and the exact frames, construct nothing, exit 10.

operand roles for --set: code, int1, int2, real1, real2.
```

Exit codes: `0` ok · `2` usage · `3` map/binding refused · `4` allowlist unusable · `5` not a test rig ·
`6` not write-eligible · `7` not isolated · `8` no expected stamp · `9` fence fault · `10` dry run ·
`11` frame refused · `12` no transport supplied · `13` connect failed · `14` build stamp mismatch ·
`15` band not restorable · `16` enable clear · `17` not acknowledged · `18` aborted (restart / failed read
/ Ctrl-C / **a scan counter that would not advance under the arming**) · **`19` restore failed — the band
may be dirty, and this outranks every other outcome.**

An arming failure exits at the code for its own cause — `16` for a clear enable, `14` for a stamp that
changed under it, `18` for a stalled or restarted CPU — and **never** `17`. A block that was never
commanded because it could not be armed is not a command that went unacknowledged, and the whole point of
refusing there is that those two answers stay apart.
