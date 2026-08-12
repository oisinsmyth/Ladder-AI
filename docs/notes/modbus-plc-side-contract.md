# Modbus — the PLC-side contract (DEFERRED)

Status: **deferred, deliberately. Do not build this yet.**

Written 2026-08-11, split out of the PC-side client spec so that document stays buildable on its own.

**Sequencing decision (owner, 2026-08-11):** PC-side client first; then the IR for Modbus; then this.
The PLC side will be **IR-authored**, like everything else that runs on a controller — not
hand-assembled in TIA. That is why it waits for the IR work rather than being done first as a
scaffold.

This file is a requirements list and a set of open questions, not a design. Nothing here blocks the
PC-side client, which is deliberately data-driven so it can consume whatever this eventually declares.

## 1. What the PLC side has to provide

An `MB_SERVER` instance and a **mirror region** reachable through `MB_HOLD_REG`, carrying at minimum:

| Field | Why the harness needs it |
|---|---|
| Format / layout version | Read first; refuse the whole mirror on an unexpected value, since every other offset is justified only by that number |
| Identity marker | The unit-level identifier the write fence verifies — see §3 |
| Order number | Cross-check against a live order-code read: catches "right marker, wrong model of box" |
| Free-running scan counter | See §4. Without it, "wait N scans" is a wall-clock guess, not a measurement |
| Command / status words | Driving and observing the program under test |

## 2. The open decision: `%MW` or a standard-access DB

`MB_HOLD_REG` may reference *"a standard DB area or a memory area"*. Both work; they fail differently.

**For `%MW`:** M memory has **no "optimized" concept at all**, so it structurally cannot suffer the
failure measured on 2026-08-11 — a block's memory layout **silently reverts to Optimized on every
re-import**, invisible to `converter drift-check` in both directions, surviving a clean compile, and
showing up only as data going absent on the wire. Designing that out beats detecting it. The 1214C has
8 KB of `%M` and the current corpus uses none of it.

**For a DB:** `%MW` is a flat, untyped address space with no symbolic structure, no per-member
retentivity, and nothing preventing a future engineer colliding with it. A DB is self-documenting,
appears in cross-references, and is where a controls engineer expects to find structured data. The
trade is a *known, one-command* failure mode against an *unknown, silent* one — address collision —
paid for with a permanent loss of legibility.

**Unresolved and relevant:** whether a standard-access S7-1200 DB supports per-member retentivity at
all. It matters for any mirror carrying values that must survive a power cycle.

**Not yet decided.** The IR work may settle it — if the converter can express and verify the mirror's
layout, the DB's disadvantage shrinks considerably.

## 3. Identity is genuinely weaker over Modbus

Under S7, `OrderCodeIdentitySource` reads the order code from an **SZL record** — firmware-provided,
and MEASURED to work on 2026-08-11 *even on a CPU refusing all variable access*.

**Modbus has no equivalent.** No SZL, no device-identity service the user program does not implement.
So every identifier the fence checks becomes something the **program publishes**, and is therefore
only as trustworthy as the download that placed it. A program copied onto a different box carries its
marker with it.

Consequences for this contract:

- publish **both** the marker and the order number, so the two can disagree — that disagreement is
  the only cheap signal that a program is running on the wrong hardware;
- document plainly, wherever the fence is described, that a Modbus-only identity check verifies **what
  the program claims**, not what the silicon reports.

This is inherent to the protocol. It is not fixable by better engineering, and the right response is
to record it rather than let it be quietly assumed away.

## 4. The observability floor, and what the program must do about it

Already established in the harness (`TestVector.cs`): polling observes at roughly **100 ms against a
scan of ~10 ms**, so a one-scan event is about ten times below anything a sampler can see. **No
polling rate recovers it, and no protocol choice changes it** — this is not a Modbus limitation.

So the program must provide the instrumentation, or whole classes of vector are unrunnable and the
runner will correctly refuse them rather than return a meaningless green:

- a **free-running scan counter**, for `TransportCapabilities.ScanCounter` — without it, "wait N
  scans" degrades to a wall-clock guess on a non-real-time host;
- **latched transients**, so a one-scan pulse survives to be read;
- **event scan-stamps**, so a same-scan coincidence becomes a comparison of two persistent integers.

How much this costs depends on how many vectors are transient-shaped rather than asserting on
persistent state — most real ones assert on persistent state, so it may be a small burden. Worth
sizing before designing the mirror.

## 5. The process-image address model, and why the PC client excludes coils

Siemens' `MB_SERVER` routes function codes **03/06/16** to `MB_HOLD_REG`, and **01/02/04/05/15** to
the process image — so FC05/FC15 can write `%Q` directly.

In practice this is a weak hazard, and the owner's field experience confirms it: a PLC scan overwrites
any output the program actually drives, so an external coil write is a **one-scan transient rather
than control**, and it only persists on outputs the program never writes. The conventional pattern —
client reads and writes holding registers, program copies between the mirror and its own data — never
issues those function codes at all.

The PC client excludes coil writes anyway, as absent capability rather than policy. It costs nothing
and makes "our tooling cannot write `%Q`" a property of the binary.

If `MB_SERVER` were ever to ship in a program going to site, that is the point at which the exposure
deserves a deliberate answer — gating the call behind a commissioning flag is the obvious lever.
Statics named `QB_*` / `IB_*` reportedly narrow the exposed process-image window; verify before
relying on it.

## 6. Open questions — for the reference projects, or for measurement

The owner is sourcing worked Modbus TCP projects. These are what they should settle:

- where `MB_SERVER` maps the base of `MB_HOLD_REG`, and the register ↔ `%MW` offset relation;
- 32-bit word order for `DInt` and `Real`;
- how Bools are packed;
- whether `MB_HOLD_REG` against `%MW` behaves identically to a standard-access DB;
- how many `MB_SERVER` instances are needed for N concurrent clients, and whether the harness should
  hold one long-lived connection or reconnect per operation;
- scan-time cost of `MB_SERVER`, and which OB it belongs in.

Until answered, the PC-side client treats every one of them as configuration with no default — which
is the correct posture regardless of what the answers turn out to be.

## 7. The converter question this waits on

`MB_SERVER` cannot currently be expressed in IR. The likely shape of the work is **not** the feared
InOut-`CALL` extension: the already-supported `Modbus_Comm_Load` renders in SimaticML as
`<Part Name="Modbus_Comm_Load" Version="5.0"><Instance/></Part>` with **no `<Parameter Section=…>`
elements at all**, wiring its InOut parameter as an ordinary operand. If `MB_SERVER` renders the same
way, this is the proven "add one fixed-shape Part" template rather than a grammar change.

> ### 🔴 THE EVIDENCE TAG ON THE PARAGRAPH ABOVE WAS WRONG — CORRECTED 2026-08-12
>
> That claim was carried as **`[M] MEASURED`**. It is not measured, and the correction matters
> because *this paragraph is what the 5-day-versus-12-day estimate rests on*.
>
> The only two files in the repo containing `Modbus_Comm_Load` or `Modbus_Master` are
> **hand-authored unit-test fixtures** — bare `<FlgNet>` fragments with no `<Document>` /
> `<DocumentInfo>` wrapper. Confirmed by copying them to scratch and running `to-ir`, which
> **rejects both**: `SimaticMlFormatException: Could not find an SW.Blocks.* element`.
>
> *** SO NO GENUINE TIA EXPORT OF ANY MODBUS INSTRUCTION EXISTS ON DISK. *** The "it is a `<Part>`,
> not a `<Call>`" premise is an assumption about what TIA emits, tested only against what a
> developer wrote by hand. Correct tag: **`[I]` — inferred from a fixture**.
>
> The paragraph is kept because the inference is still reasonable and still the likely answer. It is
> the *tag* that was doing unearned work.

*** WHAT IS NOW GENUINELY MEASURED (2026-08-12), AND IT NARROWS THE CHANGE SHARPLY *** — established
by mutating a real green-tier export and running the shipping converter:

  - The parser throws at **`to-ir` parse time**, not merely on the synthesize path. Confirmed.
    `UnsupportedConstructException: <Call>'s <Parameter Section="InOut"> — only Input/Output have
    been observed.`
  - *** `Type="Variant"` IS NOT THE PROBLEM AT ALL. *** A `Variant`-typed **Output** parameter
    parses to IR with no complaint (exit 0). **The refusal keys on `Section` alone.**
  - *** THE DECLARATION SIDE ALREADY WORKS, INCLUDING `Variant`, IN BOTH DIRECTIONS. *** A populated
    `<Section Name="InOut">` interface member — `Word` and `Variant` alike — converts to IR and back
    to XML byte-for-byte. The corpus never contains one because every real block's InOut section is
    empty, which is why this had never been exercised.

  ➜ **The gap is confined to `FlgNetParser`'s call-parameter section whitelist.** That materially
    shrinks the change *if* `MB_SERVER` is a `<Call>`, and makes it irrelevant *if* it is a `<Part>`.
    **The fork is still open and only the missing export decides it.**

**Zero occurrences of `MB_SERVER` or `Section="InOut"` exist anywhere in the repo**, so nothing on
disk can answer it. Settling it needs one scratch export of a block containing `MB_SERVER` — which
must be authored by hand in TIA, precisely because the converter cannot yet produce it.

That single export is the difference between roughly a 5-day and a 12-day estimate, and it is the
first thing worth doing when this work starts.
