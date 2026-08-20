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

⚠️ **The consequence, stated plainly rather than buried: the enable is left DOWN, so the device is not
left exactly as it was found, and the enable cannot persist between invocations.** A separate `enable`
verb would have its own session, and its own restore would drop what it had just raised. A `send` that
needs the enable up must therefore raise it inside the same session — `--raise-enable`, off by default,
because raising a plant's master enable is not something to do because a flag was omitted. There is no
ordering in which a one-write restore both puts an old sequence back and leaves the surface live without
risking that execution; the choice is which of the two to give up, and reversibility of the *values* plus
an inert surface is the safer half.

## Arming

Every write verb requires `--arm`, and its presence is read **before** the verb is parsed, so flag order
cannot change the answer. Without it, `send` prints the plan and the exact frames byte by byte, constructs
no transport, and exits 10 — printing `current: NOT READ` rather than inventing a before-picture, because a
dry run that opened a socket "just to read" would turn the arming gate into a decoration.

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
5. `--raise-enable` if the enable is not already up when the session opens;
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
result code is read, printed verbatim and never branched on. It is carried as a `string` so a call site
comparing it to an integer would not compile.

Two things follow that are easy to get backwards, and both are tested:

- **"the result register did not change" is not evidence that nothing happened.** The block holds its
  result until the next command it processes, so an unchanged result is evidence of nothing at all. What
  separates *refused* from *never seen* is the echoed **sequence**.
- **a fresh success whose result register still reads the previous command's value is still a success.**
  Every fact on the wire is true and reading the result gives you the opposite of what happened. This case
  is the reason the verdict is keyed on the count, and it is the case the whole design exists for.

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
     Without --arm: print the plan and the exact frames, construct nothing, exit 10.

operand roles for --set: code, int1, int2, real1, real2.
```

Exit codes: `0` ok · `2` usage · `3` map/binding refused · `4` allowlist unusable · `5` not a test rig ·
`6` not write-eligible · `7` not isolated · `8` no expected stamp · `9` fence fault · `10` dry run ·
`11` frame refused · `12` no transport supplied · `13` connect failed · `14` build stamp mismatch ·
`15` band not restorable · `16` enable clear · `17` not acknowledged · `18` aborted (restart / failed read
/ Ctrl-C) · **`19` restore failed — the band may be dirty, and this outranks every other outcome.**
