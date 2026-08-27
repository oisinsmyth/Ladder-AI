using System.Text;
using System.Text.RegularExpressions;

namespace Harness.Map;

/// <summary>
/// 🔴 <b>THE THREE NUMBERS THAT IDENTIFY ONE SERVER INSTANCE ON ONE CPU. Required, never defaulted.</b>
///
/// <para><see cref="InstanceDbGenerator"/> already records the measured hazard from the other side: a
/// listening port and a connection ID are FB start values, and <b>both identify the instance</b>. Two
/// servers on one CPU cannot share either. A generator that supplied a default would hand the second
/// instance the first one's port, and <b>the failure is not a compile error</b> — it is a connection that
/// never establishes, on a rig, hours later.</para>
///
/// <para>The interface ID is worse than a collision: it names a piece of hardware. CLAUDE.md hard rule 3
/// forbids inventing hardware outright, so there is nothing to default it to even in principle.</para>
/// </summary>
/// <param name="LocalPort">
/// The TCP port the server listens on. 502 is the Modbus registered port and this is exactly why it is
/// NOT a default here: the number a rig actually listens on is a commissioning decision, and a wrong one
/// is silent.
/// </param>
/// <param name="ConnectionId">
/// The <c>CONN_OUC</c> connection ID, verbatim as IR writes it (<c>16#0010</c>, or a decimal). Unique per
/// connection on the CPU.
/// </param>
/// <param name="InterfaceId">
/// The <c>HW_ANY</c> hardware identifier of the PROFINET interface to listen on. <b>A hardware address:
/// read it off the device's own configuration, never from another project's block.</b>
/// </param>
public sealed record CommsEndpoint(int LocalPort, string ConnectionId, int InterfaceId);

/// <summary>
/// The connection shapes this generator will emit. <b>One member, and that is a statement about the
/// evidence rather than an unfinished enum</b> — the same rule
/// <see cref="InstanceDbGenerator"/>'s <c>ProjectableVersionedTypes</c> holds: what is emitted is what a
/// committed, compiled, deployed block can be pointed at.
/// </summary>
public enum CommsConnectionShape
{
    /// <summary>
    /// 🔴 <b>Passive TCP, accepting any client address</b> — <c>ConnectionType 16#0B</c>,
    /// <c>ActiveEstablished FALSE</c>, a zero remote address and a zero remote port.
    ///
    /// <para>Those four values are NOT derived from anything; they are <b>read off the one committed
    /// server block in <c>ir/test-project001/</c></b>, which compiles, imports and runs. They are emitted
    /// together, as one named decision, precisely so that no caller has to know what <c>16#0B</c> means
    /// and no generator has to pick it. A different connection shape is a different set of four values
    /// and needs its own export to ground it.</para>
    /// </summary>
    PassiveAnyClient,
}

/// <summary>Naming and prose the generator will not originate.</summary>
/// <param name="BlockName">The generated FB's name.</param>
/// <param name="BlockNumber">
/// Required, with no default. Hard rule 3 forbids inventing a block number, and X-J reserves 9000–9999
/// for harness objects, which the CALLER allocates from
/// (<c>converter claim --allocate --kind block-number --type FB --floor 9000</c>).
/// </param>
/// <param name="Title">The block's TITLE line. Required.</param>
/// <param name="Comment">
/// The block's own COMMENT. <b>Required, and never defaulted</b> — the rule
/// <see cref="InstanceDbNaming"/> already holds. An invented comment passes review while telling a reader
/// nothing.
/// </param>
public sealed record CommsFbNaming(string BlockName, int BlockNumber, string Title, string Comment);

/// <summary>The one network's own title and comment. Prose, so declared.</summary>
public sealed record CommsNetworkText(string Title, string Comment);

/// <summary>What was declared for one comms FB.</summary>
/// <param name="Naming">See <see cref="CommsFbNaming"/>.</param>
/// <param name="Endpoint">
/// See <see cref="CommsEndpoint"/>. <b>Null is a REFUSAL naming all three fields</b>, never a default.
/// </param>
/// <param name="Network">See <see cref="CommsNetworkText"/>.</param>
/// <param name="Shape">See <see cref="CommsConnectionShape"/>.</param>
/// <param name="MemoryLayout">
/// The <c>MEMORYLAYOUT</c> line, or null to omit it. <b>Absent means NOT DECLARED and is reported on a
/// positive line</b>, the rule <see cref="LaneDeclaration"/> follows — it is not a claim that the block
/// has no layout. <see cref="InstanceDbGenerator"/> omits the line unconditionally for a reason worth
/// reading before declaring one here: a block's layout is asserted after every import by
/// <c>openness-cli block-layout --set Standard --yes</c>, and a line here is a second, weaker statement
/// of the same fact, free to disagree with the one that actually runs.
/// </param>
public sealed record CommsFbDeclaration(
    CommsFbNaming Naming,
    CommsEndpoint? Endpoint,
    CommsNetworkText Network,
    CommsConnectionShape Shape = CommsConnectionShape.PassiveAnyClient,
    string? MemoryLayout = null);

/// <summary>The generated comms FB, and what the caller must be told about it.</summary>
/// <param name="Ir">The block, as IR — <b>including its SIDECAR section</b>. See <see cref="CommsFbGenerator"/>.</param>
/// <param name="BlockName">Echoed so a caller building a manifest does not re-derive it.</param>
/// <param name="AreaPointer">
/// The served window exactly as the block states it (<c>P#M1000.0 WORD 37</c>). <b>The string
/// <c>converter served-area</c> reads back out of the emitted file</b>, carried here so a caller can
/// compare the two without re-deriving either.
/// </param>
/// <param name="BaseByte">The window's base, from the geometry.</param>
/// <param name="Registers">The window's width, from the geometry.</param>
/// <param name="Obligations">What the generator cannot do and a person must.</param>
public sealed record CommsFbResult(
    string Ir,
    string BlockName,
    string AreaPointer,
    int BaseByte,
    int Registers,
    IReadOnlyList<string> Obligations);

/// <summary>
/// 🔴 <b>THE COMMS FB IS MECHANISM END TO END — a Modbus server serving a window whose geometry the map
/// already computes — AND ITS ONE DERIVABLE NUMBER WAS BEING TYPED IN FOUR PLACES.</b>
///
/// <para><b>The measurement.</b> <c>ir/test-project001/FB_Comms_ModbusServer.ir</c> is 55 hand-typed
/// lines. The served window appears in it <b>four times</b>: the <c>MB_HOLD_REG</c> area pointer, the
/// sidecar <c>constant</c> backing that pointer, and twice more in prose (the block comment and the
/// network comment both state the width and the base in words). <c>converter served-area</c> exists
/// because the first two can silently disagree — <c>to-xml</c> rebuilds the operand from the SIDECAR, so
/// a readable line that drifted is invisible to every existing check and shows up only when the
/// controller serves a different area than the map was allocated against. Nothing at all checked the
/// other two. <b>Here the pointer and the sidecar constant are one expression evaluated once</b>, and the
/// prose is checked against the geometry rather than trusted (see <see cref="CheckProse"/>).</para>
///
/// <para>🔴 <b>WHY THIS GENERATOR EMITS A SIDECAR WHEN NO OTHER ONE DOES — measured, not preferred.</b>
/// Every other generator in this component emits readable-only IR and lets the converter synthesize the
/// machine half (ADR-0005). <c>MB_SERVER</c> is a FIXED-SHAPE instruction, and
/// <c>SidecarSynthesizer</c> refuses those outright: <c>converter to-xml</c> on a readable-only copy of
/// the committed block fails with <c>UnsupportedSynthesisConstructException: Network 1: sidecar synthesis
/// does not support: FixedShapes</c>. So the choice is not "readable-only is tidier"; it is "emit the
/// sidecar or emit a block the converter will not convert". <b>The UIds below are the committed block's
/// own</b>, kept rather than re-allocated so this generator can be checked against a file that has been
/// through TIA — see <c>CommsFbAgainstTheCommittedCorpusTests</c>. The block has exactly one network and
/// one instruction, so there is nothing in it for them to collide with.</para>
///
/// <para><b>What is NOT mechanism, and is therefore refused rather than defaulted:</b> the listening
/// port, the connection ID and the hardware interface ID (see <see cref="CommsEndpoint"/>); the block
/// number; and all four pieces of prose. Everything else — the whole <c>TCON_IP_v4</c> shape, the seven
/// <c>MB_SERVER</c> ports in their template order, the wiring, and the area pointer — is derived or read
/// off the corpus.</para>
///
/// <para><b>Contract copied from <see cref="CopyLayerGenerator"/> unchanged:</b> pure text-in/text-out,
/// no filesystem, no Portal, block number required, and every uncertainty a refusal rather than a guess.</para>
/// </summary>
public static class CommsFbGenerator
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    /// <summary>A CONN_OUC literal as IR writes one: <c>16#0010</c>, or a plain decimal.</summary>
    private static readonly Regex ConnectionIdLiteral = new("^(16#[0-9A-Fa-f]{1,8}|[0-9]{1,10})$", RegexOptions.Compiled);

    /// <summary>The instruction, its version, and the two static names its call and its sidecar both cite.</summary>
    private const string Instruction = "MB_SERVER";
    private const string InstructionVersion = "5.3";
    private const string ServerStatic = "MbServer";
    private const string ConnectStatic = "Comms";

    /// <summary>
    /// 🔴 <b>The seven ports, in the order and with the wire kinds the converter's own template declares
    /// — and this is a SECOND COPY of that template, kept honest the way <see cref="IrMemberLine"/> is.</b>
    ///
    /// <para><c>Harness.Map</c> takes no converter project reference (see its <c>.csproj</c>, and
    /// <c>Directory.Build.props</c> for ruling C2), so the port list cannot be read from
    /// <c>Converter.SimaticMl.FixedShapeInstructions.MbServer53</c> at runtime. It is copied, and
    /// <c>CommsFbAgainstTheCommittedCorpusTests</c> pins it against the committed block's own call
    /// statement — a drift in either direction fails there rather than producing a block whose ports are
    /// wired to the wrong things.</para>
    /// </summary>
    private static readonly (string Port, PortWiring Wiring)[] Ports =
    {
        ("DISCONNECT", PortWiring.Tag),
        ("MB_HOLD_REG", PortWiring.Literal),
        ("CONNECT", PortWiring.Tag),
        ("NDR", PortWiring.Open),
        ("DR", PortWiring.Open),
        ("ERROR", PortWiring.Open),
        ("STATUS", PortWiring.Tag),
    };

    private enum PortWiring { Tag, Literal, Open }

    /// <summary>
    /// 🔴 <b>THE COMMITTED BLOCK'S OWN UIds, NAMED.</b> Arbitrary as numbers and load-bearing as a set:
    /// they must be distinct, and the <c>port</c> lines must cite the <c>access</c>/<c>constant</c>
    /// entries declared above them. Named rather than left as literals so a reader can see which is
    /// which; asserted distinct by test.
    /// </summary>
    private const int CompileUnitUId = 3;
    private const int DisconnectAccessUId = 21;
    private const int AreaConstantUId = 22;
    private const int ConnectAccessUId = 23;
    private const int StatusAccessUId = 24;
    private const int PartUId = 25;
    private const int InstanceUId = 26;
    private const int RailUId = 30;

    /// <summary>Wire UIds, in <see cref="Ports"/> order: DISCONNECT, MB_HOLD_REG, CONNECT, NDR, DR, ERROR, STATUS.</summary>
    private static readonly int[] WireUIds = { 31, 32, 33, 34, 35, 36, 37 };

    /// <summary>OpenCon UIds for the three unconnected outputs, in <see cref="Ports"/> order.</summary>
    private static readonly int[] OpenConUIds = { 27, 28, 29 };

    /// <summary>
    /// Placeholders a declared comment may use to state the served window without stating a number. The
    /// generator fills them from the geometry, so the prose cannot fall behind the pointer.
    /// </summary>
    public const string BasePlaceholder = "{base}";

    /// <inheritdoc cref="BasePlaceholder"/>
    public const string RegistersPlaceholder = "{registers}";

    /// <param name="declaration">What the caller declares. Null throws — there is nothing to generate.</param>
    /// <param name="geometry">
    /// 🔴 <b>THE TWO NUMBERS, TAKEN AND NOT INVENTED.</b> <see cref="MirrorGeometry.BaseByte"/> and
    /// <see cref="MirrorGeometry.DeclaredRegisters"/> ARE the served window; this generator computes
    /// neither and refuses a geometry that <see cref="MirrorGeometry.Refusals"/> already rejects rather
    /// than restating its checks. That type's own rules cover the word-aligned base, the positive width
    /// and the window fitting inside bit memory, each with a refusal written for a reader.
    /// </param>
    /// <exception cref="ArgumentException">
    /// Every refusal. The caller-facing contract is <see cref="CopyLayerGenerator"/>'s: a refusal names
    /// what is missing and who resolves it, and NOTHING usable comes back beside it.
    /// </exception>
    public static CommsFbResult Generate(CommsFbDeclaration declaration, MirrorGeometry geometry)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(geometry);

        var naming = declaration.Naming ?? throw new ArgumentException(
            "no naming was declared. A block needs a name, a number, a title and a comment, and this generator "
            + "originates none of them.", nameof(declaration));

        ValidateNaming(naming);

        if (declaration.Shape != CommsConnectionShape.PassiveAnyClient)
        {
            throw new ArgumentException(
                $"connection shape '{declaration.Shape}' is not one this generator can emit. Only "
                + $"{nameof(CommsConnectionShape.PassiveAnyClient)} is grounded in a committed, compiled block; a different "
                + "shape is a different set of TCON_IP_v4 values and needs its own export behind it.", nameof(declaration));
        }

        // 🔴 THE GEOMETRY IS CHECKED BY ITS OWN TYPE AND THE REFUSALS ARE PASSED THROUGH VERBATIM. A second
        // copy of "the base must be word-aligned" here would be a second rule free to disagree with the one
        // the allocator actually runs.
        if (geometry.Refusals is { Count: > 0 } broken)
        {
            throw new ArgumentException(
                $"the mirror geometry this server would serve is refused by {nameof(MirrorGeometry)} itself, so there is no "
                + $"window to point at: {string.Join(" | ", broken)}", nameof(geometry));
        }

        var endpoint = declaration.Endpoint ?? throw new ArgumentException(
            $"'{naming.BlockName}' declares no endpoint, so the listening port, the connection ID and the hardware "
            + "interface ID are all missing. NONE of the three is defaulted here. The port and the connection ID each "
            + "IDENTIFY this server instance — two servers on one CPU cannot share either — and a default would hand a "
            + "second instance the first one's, which is not a compile error but a connection that never establishes. "
            + "The interface ID names a piece of hardware, which hard rule 3 forbids inventing outright. The engineer "
            + "who commissioned the CPU resolves all three.", nameof(declaration));

        ValidateEndpoint(endpoint, naming.BlockName);

        var network = declaration.Network ?? throw new ArgumentException(
            $"'{naming.BlockName}' declares no network title or comment. The generator writes neither: a comment that is "
            + "subtly wrong about what this server exposes is worse than none, which is the rule "
            + $"{nameof(StimShellGenerator)} already holds for the same reason.", nameof(declaration));

        RequireProse(network.Title, "the network title", naming.BlockName);
        RequireProse(network.Comment, "the network comment", naming.BlockName);

        var areaPointer = $"P#M{geometry.BaseByte}.0 WORD {geometry.DeclaredRegisters}";

        var blockComment = CheckProse(naming.Comment, "the block comment", naming.BlockName, geometry);
        var networkComment = CheckProse(network.Comment, "the network comment", naming.BlockName, geometry);

        var ir = new StringBuilder();
        AppendHeader(ir, naming, declaration.MemoryLayout, blockComment);
        AppendInterface(ir, endpoint);
        AppendNetwork(ir, network.Title, networkComment, areaPointer);
        AppendSidecar(ir, areaPointer);

        return new CommsFbResult(
            ir.ToString(),
            naming.BlockName,
            areaPointer,
            geometry.BaseByte,
            geometry.DeclaredRegisters,
            Obligations(naming, endpoint, declaration.MemoryLayout, areaPointer));
    }

    // --- the block ------------------------------------------------------------------------------------

    private static void AppendHeader(StringBuilder ir, CommsFbNaming naming, string? memoryLayout, string comment)
    {
        ir.Append("BLOCK FB ").Append(naming.BlockName).Append('\n');
        ir.Append("ROOTID 0\n");
        ir.Append("NUMBER ").Append(naming.BlockNumber).Append('\n');
        ir.Append("LANGUAGE LAD\n");

        if (memoryLayout is { Length: > 0 })
            ir.Append("MEMORYLAYOUT ").Append(memoryLayout).Append('\n');

        ir.Append("TITLE \"").Append(Escape(naming.Title)).Append("\"\n");
        ir.Append("COMMENT \"").Append(Escape(comment)).Append("\"\n");
        ir.Append('\n');
    }

    /// <summary>
    /// 🔴 <b>The whole interface is MECHANISM and is emitted whole.</b> The server's own instance, the
    /// connection settings block, and the four-octet remote address — the shape is the instruction's, not
    /// a design decision, and the only values a caller supplies are the three in
    /// <see cref="CommsEndpoint"/>. <c>INPUT</c>, <c>OUTPUT</c> and <c>CONSTANT</c> are emitted as empty
    /// section headers because present-but-empty is a distinction the IR grammar preserves on purpose.
    /// </summary>
    private static void AppendInterface(StringBuilder ir, CommsEndpoint endpoint)
    {
        ir.Append("INTERFACE\n");
        ir.Append("  INPUT\n");
        ir.Append("  OUTPUT\n");
        ir.Append("  STATIC\n");
        ir.Append("    ").Append(ServerStatic).Append(" : ").Append(Instruction)
          .Append(" VERSION ").Append(InstructionVersion).Append('\n');
        ir.Append("    ").Append(ConnectStatic).Append(" : TCON_IP_v4 VERSION 1.0\n");
        ir.Append("      InterfaceId : HW_ANY = ").Append(endpoint.InterfaceId).Append('\n');
        ir.Append("      ID : CONN_OUC = ").Append(endpoint.ConnectionId).Append('\n');

        // The four PassiveAnyClient values, together, as one named decision — see CommsConnectionShape.
        ir.Append("      ConnectionType : Byte = 16#0B\n");
        ir.Append("      ActiveEstablished : Bool = FALSE\n");
        ir.Append("      RemoteAddress : IP_V4 VERSION 1.0\n");
        ir.Append("        ADDR : Array[1..4] of Byte\n");
        for (var octet = 1; octet <= 4; octet++)
            ir.Append("          [").Append(octet).Append("] = 16#00\n");
        ir.Append("      RemotePort : UInt = 0\n");

        ir.Append("      LocalPort : UInt = ").Append(endpoint.LocalPort).Append('\n');
        ir.Append("  CONSTANT\n");
        ir.Append('\n');
    }

    private static void AppendNetwork(StringBuilder ir, string title, string comment, string areaPointer)
    {
        ir.Append("NETWORK 1 \"").Append(Escape(title)).Append("\"\n");
        ir.Append("  COMMENT \"").Append(Escape(comment)).Append("\"\n");
        ir.Append("  ").Append(Instruction).Append('(').Append(ServerStatic).Append(", EN := TRUE");

        foreach (var (port, wiring) in Ports)
        {
            ir.Append(", ").Append(port);
            ir.Append(wiring == PortWiring.Open || IsOutput(port) ? " => " : " := ");
            ir.Append(OperandOf(port, wiring, areaPointer));
        }

        ir.Append(")\n");
        ir.Append('\n');
    }

    /// <summary>
    /// The machine half. <b>Every UId cited here is declared above it</b>, and the two orders differ on
    /// purpose: a <c>tag</c>/<c>literal</c> port line is <c>&lt;access|constant&gt; &lt;wire&gt;</c>,
    /// an <c>open</c> port line is <c>&lt;wire&gt; &lt;opencon&gt;</c> — the converter's own
    /// <c>PortBindingSidecar</c> record order, which is not the same both ways.
    /// </summary>
    private static void AppendSidecar(StringBuilder ir, string areaPointer)
    {
        ir.Append("SIDECAR\n");
        ir.Append("NETWORK 1\n");
        ir.Append("  compileunit = ").Append(CompileUnitUId).Append('\n');
        ir.Append("  access ").Append(ServerStatic).Append(".DISCONNECT = ").Append(DisconnectAccessUId).Append(" LocalVariable\n");
        ir.Append("  access ").Append(ConnectStatic).Append(" = ").Append(ConnectAccessUId).Append(" LocalVariable\n");
        ir.Append("  access ").Append(ServerStatic).Append(".STATUS = ").Append(StatusAccessUId).Append(" LocalVariable\n");
        ir.Append("  constant ").Append(areaPointer).Append(" = ").Append(AreaConstantUId).Append(" Any\n");
        ir.Append("  fixedshape 0\n");
        ir.Append("    fixedshapeuid = ").Append(PartUId).Append('\n');
        ir.Append("    instruction = ").Append(Instruction).Append('\n');
        ir.Append("    version = ").Append(InstructionVersion).Append('\n');
        ir.Append("    en = condition\n");
        ir.Append("      rail = ").Append(RailUId).Append('\n');
        ir.Append("    instanceuid = ").Append(InstanceUId).Append('\n');
        ir.Append("    instancescope = LocalVariable\n");
        ir.Append("    instancepath = ").Append(ServerStatic).Append('\n');

        var open = 0;
        for (var i = 0; i < Ports.Length; i++)
        {
            var (port, wiring) = Ports[i];
            ir.Append("    port ").Append(port).Append(' ');

            switch (wiring)
            {
                case PortWiring.Tag:
                    ir.Append("tag = ").Append(AccessUIdOf(port)).Append(' ').Append(WireUIds[i]);
                    break;
                case PortWiring.Literal:
                    ir.Append("literal = ").Append(AreaConstantUId).Append(' ').Append(WireUIds[i]);
                    break;
                default:
                    ir.Append("open = ").Append(WireUIds[i]).Append(' ').Append(OpenConUIds[open++]);
                    break;
            }

            ir.Append('\n');
        }
    }

    private static bool IsOutput(string port) => port is "NDR" or "DR" or "ERROR" or "STATUS";

    private static string OperandOf(string port, PortWiring wiring, string areaPointer) => (port, wiring) switch
    {
        (_, PortWiring.Open) => "OPEN",
        ("MB_HOLD_REG", _) => areaPointer,
        ("CONNECT", _) => ConnectStatic,
        ("DISCONNECT", _) => ServerStatic + ".DISCONNECT",
        ("STATUS", _) => ServerStatic + ".STATUS",
        _ => throw new ArgumentException($"no operand is defined for {Instruction} port '{port}'.", nameof(port)),
    };

    private static int AccessUIdOf(string port) => port switch
    {
        "DISCONNECT" => DisconnectAccessUId,
        "CONNECT" => ConnectAccessUId,
        "STATUS" => StatusAccessUId,
        _ => throw new ArgumentException($"{Instruction} port '{port}' is not wired to a tag.", nameof(port)),
    };

    // --- refusals -------------------------------------------------------------------------------------

    private static void ValidateNaming(CommsFbNaming naming)
    {
        if (!SafeIdentifier.IsMatch(naming.BlockName ?? string.Empty))
            throw new ArgumentException($"'{naming.BlockName}' is not a usable block name.", nameof(naming));

        if (naming.BlockNumber <= 0)
        {
            throw new ArgumentException(
                $"'{naming.BlockName}' declares block number {naming.BlockNumber}. A block number is required and never "
                + "defaulted (hard rule 3); harness objects are allocated from 9000–9999 with "
                + "`converter claim --allocate --kind block-number --type FB --floor 9000`.", nameof(naming));
        }

        RequireProse(naming.Title, "a title", naming.BlockName!);
        RequireProse(naming.Comment, "a comment", naming.BlockName!);
    }

    private static void RequireProse(string? text, string what, string blockName)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException(
                $"'{blockName}' declares {what} that is empty. This generator writes no prose: every comment in the "
                + "committed corpus says what the block does and why, generated code is held to a stricter bar than site "
                + "code, and an invented comment passes review while telling a reader nothing.", nameof(blockName));
        }
    }

    private static void ValidateEndpoint(CommsEndpoint endpoint, string blockName)
    {
        if (endpoint.LocalPort is < 1 or > 65535)
        {
            throw new ArgumentException(
                $"'{blockName}' declares listening port {endpoint.LocalPort}, which is not a TCP port.", nameof(endpoint));
        }

        if (!ConnectionIdLiteral.IsMatch(endpoint.ConnectionId ?? string.Empty))
        {
            throw new ArgumentException(
                $"'{blockName}' declares connection ID '{endpoint.ConnectionId}', which is not a CONN_OUC literal as IR "
                + "writes one (`16#0010`, or a decimal). It is emitted verbatim into the interface, so a form the "
                + "converter cannot read would be a block that fails to convert rather than a value that is wrong.",
                nameof(endpoint));
        }

        if (endpoint.InterfaceId <= 0)
        {
            throw new ArgumentException(
                $"'{blockName}' declares hardware interface ID {endpoint.InterfaceId}. A HW_ANY identifier is read off the "
                + "device's own configuration; hard rule 3 forbids inventing hardware, so there is nothing to fall back to.",
                nameof(endpoint));
        }
    }

    /// <summary>
    /// 🔴 <b>PROSE THAT STATES THE SERVED WINDOW IS CHECKED AGAINST THE WINDOW, NOT TRUSTED.</b>
    ///
    /// <para>The committed block states the width and the base twice in words — <i>"exposing 37 holding
    /// registers"</i> and <i>"covers 37 words from M1000.0"</i> — beside the two machine copies
    /// <c>converter served-area</c> already reconciles. <b>Nothing checked the prose</b>, so widening the
    /// window meant remembering to edit four places, and a reader of a stale comment is told the wrong
    /// thing by the block itself.</para>
    ///
    /// <para><see cref="BasePlaceholder"/> and <see cref="RegistersPlaceholder"/> are substituted from the
    /// geometry, so a comment written with them cannot fall behind. A comment that states a number
    /// literally is compared, and a disagreement is a refusal — <b>the number is not corrected</b>: which
    /// of the two the author meant is not something to assume.</para>
    /// </summary>
    private static string CheckProse(string text, string what, string blockName, MirrorGeometry geometry)
    {
        var filled = text
            .Replace(BasePlaceholder, geometry.BaseByte.ToString(), StringComparison.Ordinal)
            .Replace(RegistersPlaceholder, geometry.DeclaredRegisters.ToString(), StringComparison.Ordinal);

        foreach (var (pattern, expected, describes) in new[]
                 {
                     (@"WORD\s+(\d+)", geometry.DeclaredRegisters, "the served width"),
                     (@"(\d+)\s+(?:holding\s+)?registers", geometry.DeclaredRegisters, "the served width"),
                     (@"(\d+)\s+words", geometry.DeclaredRegisters, "the served width"),
                     (@"\bM(\d+)\.0\b", geometry.BaseByte, "the window's base byte"),
                 })
        {
            foreach (Match match in Regex.Matches(filled, pattern, RegexOptions.IgnoreCase))
            {
                if (!int.TryParse(match.Groups[1].Value, out var stated) || stated == expected)
                    continue;

                throw new ArgumentException(
                    $"{what} of '{blockName}' states `{match.Value.Trim()}`, and {describes} this block will actually serve "
                    + $"is {expected} (`P#M{geometry.BaseByte}.0 WORD {geometry.DeclaredRegisters}`, from the geometry). The "
                    + "window has FOUR homes in this block — the area pointer, the sidecar constant behind it, and both "
                    + "comments — and `converter served-area` reconciles only the first two. A comment that disagrees is a "
                    + $"reader told the wrong thing by the block itself. Write `{RegistersPlaceholder}` and "
                    + $"`{BasePlaceholder}` instead and they are filled from the geometry, or correct the number — this "
                    + "generator will not choose which of the two you meant.", nameof(blockName));
            }
        }

        return filled;
    }

    private static IReadOnlyList<string> Obligations(
        CommsFbNaming naming, CommsEndpoint endpoint, string? memoryLayout, string areaPointer)
    {
        var owed = new List<string>
        {
            $"'{naming.BlockName}' SERVES `{areaPointer}` AND NOTHING CHECKED THAT AGAINST THE CONTROLLER. The pointer is "
            + "derived from the geometry this run allocated against, and `converter served-area` reads the same two numbers "
            + "back out of the emitted file — but both read the corpus, not the CPU. A corpus that is stale with respect to "
            + "the device derives a confident, agreed, wrong number and is indistinguishable from a fresh one.",

            $"THIS BLOCK NEEDS AN INSTANCE DB, and it is the one FB in this component that must be declared with "
            + $"`{nameof(InstanceDbMemberSource.LeftToTia)}` members: MB_SERVER and TCON_IP_v4 are system-type instances "
            + $"{nameof(InstanceDbGenerator)} refuses to project. That means the instance INHERITS this block's start "
            + $"values — including LocalPort {endpoint.LocalPort} and ID {endpoint.ConnectionId}, both of which identify "
            + $"it — so {nameof(ProgramGenerator)} will refuse a SECOND instance of it, and that refusal is correct.",

            $"'{naming.BlockName}' MUST BE CALLED from the cyclic OB. Nothing here checks that: a server that is never "
            + "called listens on nothing, and every client read then fails as a connection error rather than as a wrong "
            + "value — which reads like a network fault and is not one.",
        };

        owed.Add(memoryLayout is { Length: > 0 }
            ? $"MEMORYLAYOUT WAS DECLARED as '{memoryLayout}' and is emitted. Note that a TIA import can silently revert a "
              + "block's layout, and `openness-cli block-layout --set Standard --yes` is what actually asserts it after "
              + "every import — the line in this block is the weaker of the two statements and is free to disagree with it."
            : "NO MEMORYLAYOUT WAS DECLARED, so no line is emitted and TIA decides. That is not a claim that the block has "
              + "no layout. If a PC-side harness reads this block over classic S7, assert it after every import with "
              + "`openness-cli block-layout --set Standard --yes` and gate with `--expect Standard`.");

        return owed;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
