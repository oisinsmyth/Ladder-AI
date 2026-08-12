namespace Converter.SimaticMl;

/// <summary>
/// Which way a fixed-shape instruction's named port faces.
///
/// This is the whole reason the registry exists. **Section and datatype do not exist at the call
/// site** — measured 2026-08-12 against a real V20 export of an S7-1200 Modbus TCP block: the
/// network XML states only a port NAME on `&lt;NameCon&gt;`. Nothing in the document says
/// `DATA_PTR` is an InOut, or a Variant; that lives in the instruction definition, which is not
/// exported. Wire *order* (an `&lt;IdentCon&gt;` before the `&lt;NameCon&gt;` vs after it) does
/// distinguish a read from a write, but it cannot distinguish an input from an InOut, and it says
/// nothing at all when the port is wired to an `&lt;OpenCon&gt;` on its own.
///
/// So the converter carries the port list itself, per (Part Name, Version) — exactly the
/// fixed-shape template `TON`/`MOVE_BLK_VARIANT` already use, generalized into a table instead of
/// being spelled out again per instruction in the reducer/builder.
///
/// **An InOut port is an `Input` here, and that is correct rather than a compromise.** Measured
/// 2026-08-12 on an `MB_SERVER` 5.3 export: its genuinely-InOut `MB_HOLD_REG` and `CONNECT` are
/// wired as ordinary symbolic `&lt;Access&gt;` operands in normal input wire order, with no pointer
/// syntax and no `&lt;Parameter Section=…&gt;` anywhere in the document. This enum names the WIRE
/// SHAPE, which is the only thing the export states and the only thing the rebuild needs.
/// </summary>
public enum PortDirection
{
    /// <summary>Read by the instruction: `&lt;IdentCon&gt;` (or `&lt;OpenCon&gt;`) first, then `&lt;NameCon&gt;`.</summary>
    Input,

    /// <summary>Written by the instruction: `&lt;NameCon&gt;` first, then `&lt;IdentCon&gt;` (or `&lt;OpenCon&gt;`).</summary>
    Output,
}

/// <summary>One named port of a fixed-shape instruction, in source declaration order.</summary>
public sealed record InstructionPort(string Name, PortDirection Direction);

/// <summary>
/// One fixed-shape instruction template: the ordered port list carried by the converter for a
/// specific (Part Name, Version) pair. `en` is never listed — every production here resolves it
/// through the shared <c>EnSource</c> mechanism, exactly as `Move`/`Call`/`Mul` already do.
/// </summary>
public sealed record InstructionTemplate(string PartName, string Version, IReadOnlyList<InstructionPort> Ports)
{
    public InstructionPort? PortNamed(string name) => Ports.FirstOrDefault(p => p.Name == name);
}

/// <summary>
/// The (Part Name, Version) → port-template registry.
///
/// ## Why VERSION participates in matching (decided 2026-08-12)
///
/// A Siemens instruction's *version* is precisely the thing that changes its port list — that is
/// what the version is for. Since the port list is not in the export and the converter supplies it
/// from this table, accepting an unrecognized version would mean applying one version's port
/// template to another version's wiring: an input silently read as an output, or a port that does
/// not exist on that version resolved anyway. That is a *worse* failure than a refusal, because it
/// converts and imports and only misbehaves on the controller. So an unknown version is a hard
/// error naming the versions that are known, in the same fail-closed spirit as
/// <c>FlgNetParser.SupportedPartNames</c> itself.
///
/// The measurement that settles it: across three real families TIA emits `MB_COMM_LOAD` **2.1**,
/// `MB_MASTER` **2.2** and `MB_SERVER` **5.3**. Version is plainly not incidental decoration, and a
/// template keyed on name alone would silently accept a port list that does not match.
///
/// ## Why BOTH name families are here
///
/// `MB_COMM_LOAD` / `MB_MASTER` and `Modbus_Comm_Load` / `Modbus_Master` are two different TIA
/// instruction families with the same functional role, not a spelling variation:
///
/// - **`MB_COMM_LOAD` 2.1 / `MB_MASTER` 2.2** — MEASURED 2026-08-12 from a genuine TIA V20 export
///   of a live production S7-1200 (classic 1214C) block. This is what a real project on this
///   hardware emits, and it is the pair that was missing.
/// - **`Modbus_Comm_Load` 5.0 / `Modbus_Master` 6.0** — the repo's own dated grounding record
///   (`docs/evidence/stage-S1.md`, Phase 2 Tier 4, 2026-07-14) has these taken from `FC ModbusComs`
///   and put through a live TIA `Import()` that SUCCEEDED, whose compile errors were
///   instance-DB/address errors — *not* the "An instruction with the name 'X' cannot be found"
///   that TIA raised for `WAIT` in that same session against that same project. TIA resolved the
///   instruction, so the name is real. They are therefore KEPT, not replaced.
///
/// The committed fixtures for the second pair are hand-authored (as every fixture in this repo is,
/// per the data boundary) — that makes them non-evidence for the *shape*, which is why
/// `docs/notes/modbus-plc-side-contract.md` downgraded its own tag; it does not make the *name*
/// invented, which the live import above settles independently.
///
/// The two families are registered separately and neither is an alias for the other: their port
/// lists coincide today, and nothing guarantees they will at the next version of either.
/// </summary>
public static class FixedShapeInstructions
{
    // MB_COMM_LOAD 2.1 — port list measured directly from the real export's own <NameCon> names,
    // in document order. Directions read off wire endpoint order in that same export: every input
    // has its IdentCon/OpenCon before the NameCon, every output after it.
    private static readonly InstructionTemplate MbCommLoad21 = new("MB_COMM_LOAD", "2.1", new[]
    {
        new InstructionPort("REQ", PortDirection.Input),
        new InstructionPort("PORT", PortDirection.Input),
        new InstructionPort("BAUD", PortDirection.Input),
        new InstructionPort("PARITY", PortDirection.Input),
        new InstructionPort("FLOW_CTRL", PortDirection.Input),
        new InstructionPort("RTS_ON_DLY", PortDirection.Input),
        new InstructionPort("RTS_OFF_DLY", PortDirection.Input),
        new InstructionPort("RESP_TO", PortDirection.Input),
        new InstructionPort("MB_DB", PortDirection.Input),
        new InstructionPort("DONE", PortDirection.Output),
        new InstructionPort("ERROR", PortDirection.Output),
        new InstructionPort("STATUS", PortDirection.Output),
    });

    // MB_MASTER 2.2 — same provenance as MB_COMM_LOAD 2.1 above, same export, adjacent network.
    // DATA_PTR is an InOut on the real instruction; nothing in the export says so (see
    // PortDirection's own doc comment) and nothing needs to, because the wiring is a read.
    private static readonly InstructionTemplate MbMaster22 = new("MB_MASTER", "2.2", new[]
    {
        new InstructionPort("REQ", PortDirection.Input),
        new InstructionPort("MB_ADDR", PortDirection.Input),
        new InstructionPort("MODE", PortDirection.Input),
        new InstructionPort("DATA_ADDR", PortDirection.Input),
        new InstructionPort("DATA_LEN", PortDirection.Input),
        new InstructionPort("DATA_PTR", PortDirection.Input),
        new InstructionPort("DONE", PortDirection.Output),
        new InstructionPort("BUSY", PortDirection.Output),
        new InstructionPort("ERROR", PortDirection.Output),
        new InstructionPort("STATUS", PortDirection.Output),
    });

    // MB_SERVER 5.3 — ADDED 2026-08-12, on the condition the previous note set for it: "it goes in
    // when the block it lives in can round-trip." That block needed an `Array[1..10] of Struct`
    // interface member, a doubly-nested `TCON_IP_v4` member and `<Subelement>` array start values;
    // all three are now modelled, so the port list below is EXERCISED END TO END against the real
    // export rather than asserted.
    //
    // Ports and order read directly off that export's own <Wires>: `en` from the power rail, then
    // DISCONNECT / MB_HOLD_REG / CONNECT each with their <IdentCon> BEFORE the <NameCon> (a read),
    // then NDR / DR / ERROR to <OpenCon> and STATUS to an <IdentCon>, each with the <NameCon> FIRST
    // (a write). This is the CONNECT-**structure** branch of the instruction — there is no
    // CONNECT_ID / IP_PORT pair on 5.3.
    //
    // MB_HOLD_REG and CONNECT are genuinely InOut on the instruction and are `Input` here, which is
    // correct rather than a compromise — see PortDirection's own doc comment. Measured: they are
    // wired as ordinary symbolic <Access> operands in normal input order, with no pointer syntax,
    // and the whole 55,783-byte export contains ZERO <Parameter Section=…> elements. MB_HOLD_REG
    // points at a Static `Array[1..90] of Int`; CONNECT at a Static `TCON_IP_v4`.
    private static readonly InstructionTemplate MbServer53 = new("MB_SERVER", "5.3", new[]
    {
        new InstructionPort("DISCONNECT", PortDirection.Input),
        new InstructionPort("MB_HOLD_REG", PortDirection.Input),
        new InstructionPort("CONNECT", PortDirection.Input),
        new InstructionPort("NDR", PortDirection.Output),
        new InstructionPort("DR", PortDirection.Output),
        new InstructionPort("ERROR", PortDirection.Output),
        new InstructionPort("STATUS", PortDirection.Output),
    });

    private static readonly IReadOnlyList<InstructionTemplate> Templates = new[]
    {
        MbCommLoad21,
        MbMaster22,
        MbServer53,
    };

    /// <summary>Every Part Name this registry knows, at any version.</summary>
    public static IReadOnlyCollection<string> PartNames { get; } =
        Templates.Select(t => t.PartName).Distinct(StringComparer.Ordinal).ToList();

    public static bool IsFixedShapePartName(string partName) =>
        Templates.Any(t => t.PartName == partName);

    /// <summary>
    /// The template for an exact (Part Name, Version) pair, or null when the name is known but the
    /// version is not — callers turn that into a hard error naming the known versions rather than
    /// guessing a port list. See this type's own doc comment for why.
    /// </summary>
    public static InstructionTemplate? Lookup(string partName, string version) =>
        Templates.FirstOrDefault(t => t.PartName == partName && t.Version == version);

    public static IReadOnlyList<string> KnownVersionsOf(string partName) =>
        Templates.Where(t => t.PartName == partName).Select(t => t.Version).ToList();

    /// <summary>
    /// Resolves a template or throws. Shared by the parser (at read time), the reducer and the
    /// builder so the three can never disagree about what a port list is.
    /// </summary>
    public static InstructionTemplate Require(string partName, string version)
    {
        var template = Lookup(partName, version);
        if (template is not null)
        {
            return template;
        }

        var known = KnownVersionsOf(partName);
        if (known.Count == 0)
        {
            throw new UnsupportedConstructException(
                $"'{partName}' is not a fixed-shape instruction this converter carries a port template for.");
        }

        throw new UnsupportedConstructException(
            $"Unsupported version '{version}' of instruction '{partName}' — this converter carries port templates " +
            $"for version(s) {string.Join(", ", known)} only. A port list is NOT present in the exported network " +
            "(only port names are), so it is supplied per (name, version) by the converter; accepting an unknown " +
            "version would mean applying the wrong port template, which imports cleanly and misbehaves on the " +
            "controller. Ground the new version against a real export and add it to FixedShapeInstructions.");
    }
}
