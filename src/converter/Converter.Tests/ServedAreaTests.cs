using Converter.ServedArea;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-23, workbench Y2. `converter served-area` — the producer that reads the width off the
/// block that actually serves it.
///
/// <para><b>The defect it closes.</b> A harness binding's <c>declaredRegisters</c> was authored by
/// hand, per lane, and flowed unchecked into <c>MirrorGeometry</c>, into the allocator, into
/// <c>RegisterMap.MapHash</c> and therefore into the build stamp. The stamp does hash a declared
/// width; what nothing checked is whether that number is <b>true of the program</b>.</para>
///
/// <para>The truth lives in the program in <b>two places that can silently disagree</b> — the
/// readable <c>MB_HOLD_REG := P#M1000.0 WORD 1024</c> and the sidecar constant backing it. `to-xml`
/// rebuilds from the sidecar, so a readable line that drifted is invisible until the rig serves a
/// different area than the map was allocated against. Both are read here, and a disagreement is a
/// refusal naming both lines.</para>
///
/// <para>🔴 <b>WHAT THIS PRODUCER CANNOT POSSIBLY SEE: whether the block it read is the block on the
/// controller.</b> It reads the corpus, not the CPU. The mirror's widening to 1024 registers was
/// proven by probing the device from both sides, and nothing here substitutes for that. This buys
/// exactly one thing — that a binding cannot disagree with the program that was staged.</para>
/// </summary>
[Collection(TestCollections.ConsoleCapture)]
public class ServedAreaTests : IDisposable
{
    private readonly string _dir;

    public ServedAreaTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"servedarea-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string Corpus => Path.Combine(RepoRoot(), "ir", "test-project001");

    private string Write(string file, string text)
    {
        var path = Path.Combine(_dir, file);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>
    /// A minimal but REAL-SHAPED comms block: the readable area pointer and the sidecar constant that
    /// backs it, joined by the UId the fixed-shape port cites. Both halves parameterised so a test can
    /// desync them the way a hand edit would.
    /// </summary>
    private static string CommsBlock(string readablePointer, string sidecarPointer, string name = "FB_Comms_ModbusServer")
        => $"""
BLOCK FB {name}
ROOTID 0
NUMBER 9000
LANGUAGE LAD
MEMORYLAYOUT Optimized
TITLE "Modbus TCP Server"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    MbServer : MB_SERVER VERSION 5.3
    Comms : TCON_IP_v4 VERSION 1.0
      InterfaceId : HW_ANY = 64
  CONSTANT

NETWORK 1 "Serve The Harness Mirror"
  MB_SERVER(MbServer, EN := TRUE, DISCONNECT := MbServer.DISCONNECT, MB_HOLD_REG := {readablePointer}, CONNECT := Comms, NDR => OPEN, DR => OPEN, ERROR => OPEN, STATUS => MbServer.STATUS)

SIDECAR
NETWORK 1
  compileunit = 3
  access MbServer.DISCONNECT = 21 LocalVariable
  access Comms = 23 LocalVariable
  access MbServer.STATUS = 24 LocalVariable
  constant {sidecarPointer} = 22 Any
  fixedshape 0
    fixedshapeuid = 25
    instruction = MB_SERVER
    version = 5.3
    en = condition
      rail = 30
    instanceuid = 26
    instancescope = LocalVariable
    instancepath = MbServer
    port DISCONNECT tag = 21 31
    port MB_HOLD_REG literal = 22 32
    port CONNECT tag = 23 33
    port NDR open = 34 27
    port DR open = 35 28
    port ERROR open = 36 29
    port STATUS tag = 24 37

""";

    private const string PlainBlock = """
BLOCK FB FB_Nothing
ROOTID 0
NUMBER 500
LANGUAGE LAD
TITLE "Nothing"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK 1 "One rung"
  COIL Held := Held

""";

    // =============================================================================================
    // 1 — THE REAL COMMITTED ARTIFACT. Not a fixture written for this test.
    // =============================================================================================

    [Fact]
    public void DerivesTheServedAreaFromTheCommittedReferenceProject()
    {
        var report = ServedAreaRunner.Run(new[] { Corpus });

        Assert.Empty(report.Refusals);
        Assert.True(report.Derived, report.Denominator);
        Assert.Equal("M", report.Area);
        Assert.Equal(1000, report.BaseByte);
        Assert.Equal(1024, report.Registers);
        Assert.Equal("FB_Comms_ModbusServer", report.BlockName);
        Assert.EndsWith("FB_Comms_ModbusServer.ir", report.File, StringComparison.Ordinal);
    }

    /// <summary>
    /// The two lines the whole check is about, located in the real file. 31 is the readable
    /// <c>MB_SERVER(...)</c> statement; 39 is the sidecar constant backing it. Pinned so that a
    /// refusal naming them can be trusted to name the right ones.
    /// </summary>
    [Fact]
    public void NamesBothLinesOfTheRealFile()
    {
        var report = ServedAreaRunner.Run(new[] { Corpus });

        Assert.Equal(31, report.ReadableLine);
        Assert.Equal(39, report.SidecarLine);
        Assert.Equal("P#M1000.0 WORD 1024", report.ReadableText);
        Assert.Equal("P#M1000.0 WORD 1024", report.SidecarText);
    }

    /// <summary>The denominator, on the real corpus, in the shape the plan specified.</summary>
    [Fact]
    public void DenominatorStatesBaseWidthAndBothSources()
    {
        var report = ServedAreaRunner.Run(new[] { Corpus });

        Assert.StartsWith("served area: base 1000, 1024 register(s), derived from ", report.Denominator, StringComparison.Ordinal);
        Assert.Contains(":31 + sidecar ", report.Denominator, StringComparison.Ordinal);
        Assert.EndsWith(":39", report.Denominator, StringComparison.Ordinal);
    }

    // =============================================================================================
    // 2 — THE DESYNC. Readable says 37, sidecar says 40, and NOTHING in the pipeline notices.
    // =============================================================================================

    [Fact]
    public void RefusesWhenTheReadableLineAndTheSidecarDisagree()
    {
        var path = Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 40"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        var refusal = Assert.Single(report.Refusals);

        // BOTH numbers and BOTH lines, by line number, in one sentence. A refusal that names only the
        // disagreement sends the reader hunting for the second home of the number.
        Assert.Contains("P#M1000.0 WORD 37", refusal, StringComparison.Ordinal);
        Assert.Contains("P#M1000.0 WORD 40", refusal, StringComparison.Ordinal);
        Assert.Contains(path + ":18", refusal, StringComparison.Ordinal);
        Assert.Contains(path + ":26", refusal, StringComparison.Ordinal);
    }

    /// <summary>A refusal is not a NOT-DERIVED, and the denominator must not let it read as one.</summary>
    [Fact]
    public void ARefusalSaysTheDeclaredWidthStandsUnchecked()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 40"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.StartsWith("REFUSED — 1 block(s) in 1 file(s) scanned;", report.Denominator, StringComparison.Ordinal);
        Assert.Contains("stands unchecked", report.Denominator, StringComparison.Ordinal);
    }

    // =============================================================================================
    // 3 — THE NEGATIVE CONTROLS, RUN. Both of them.
    // =============================================================================================

    [Fact]
    public void NoCommsBlockIsNotDerivedRatherThanEmptyAndClean()
    {
        Write("FC_Nothing.ir", PlainBlock);

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Empty(report.Refusals);
        Assert.Equal("NOT DERIVED — 1 block(s) in 1 file(s) scanned, no MB_SERVER call", report.Denominator);
    }

    /// <summary>
    /// An empty directory is NOT the same absence as a corpus with no comms block, and the denominator
    /// is the only thing that separates them.
    /// </summary>
    [Fact]
    public void AnEmptyCorpusSaysZeroBlocksScanned()
    {
        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Equal("NOT DERIVED — 0 block(s) in 0 file(s) scanned, no MB_SERVER call", report.Denominator);
    }

    [Fact]
    public void TwoMbServerCallsAreAmbiguousAndRefused()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37"));
        Write("FB_Comms_Second.ir", CommsBlock("P#M2000.0 WORD 64", "P#M2000.0 WORD 64", "FB_Comms_Second"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        var refusal = Assert.Single(report.Refusals);
        Assert.Contains("2 MB_SERVER call(s)", refusal, StringComparison.Ordinal);
        Assert.Contains("FB_Comms_ModbusServer", refusal, StringComparison.Ordinal);
        Assert.Contains("FB_Comms_Second", refusal, StringComparison.Ordinal);

        // Guessing which one serves the mirror would invent the answer, and the refusal says so.
        Assert.Contains("invent", refusal, StringComparison.OrdinalIgnoreCase);
    }

    // =============================================================================================
    // 4 — THE DIRECTION OF ERROR. A derivation that guesses WIDE allocates a map that overflows the
    //     real window and fails on the wire as a device fault. Every uncertainty refuses instead.
    // =============================================================================================

    [Fact]
    public void ANonMarkerAreaIsRefusedRatherThanTranslated()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#DB99.DBX0.0 WORD 37", "P#DB99.DBX0.0 WORD 37"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("marker memory", StringComparison.Ordinal));
    }

    [Fact]
    public void AUnitOtherThanWordIsRefusedByName()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 BYTE 74", "P#M1000.0 BYTE 74"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("BYTE", StringComparison.Ordinal));
    }

    [Fact]
    public void ABitOffsetInsideTheByteIsRefused()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.4 WORD 37", "P#M1000.4 WORD 37"));

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("bit 4", StringComparison.Ordinal));
    }

    /// <summary>
    /// A block with no <c>SIDECAR</c> section has only ONE of the two homes of the number. Reading one
    /// and calling it derived is exactly the single-source trust this producer exists to remove.
    /// </summary>
    [Fact]
    public void ABlockWithNoSidecarIsRefusedBecauseOnlyOneHomeExists()
    {
        var full = CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37");
        Write("FB_Comms_ModbusServer.ir", full[..full.IndexOf("SIDECAR", StringComparison.Ordinal)]);

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("sidecar", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// A file that did not parse could hold the MB_SERVER call, or a SECOND one. Both the "exactly one"
    /// claim and the "none at all" claim depend on having read everything, so a partial corpus refuses
    /// rather than answering from the part it could read.
    /// </summary>
    [Fact]
    public void APartialCorpusRefusesRatherThanAnsweringFromWhatItCouldRead()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37"));
        Write("FB_Broken.ir", "BLOCK FB FB_Broken\nthis is not IR at all\n");

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("FB_Broken.ir", StringComparison.Ordinal));
    }

    /// <summary>A path naming nothing must not resolve to a silent empty scan (FI-44).</summary>
    [Fact]
    public void APathThatNamesNothingIsRefused()
    {
        var report = ServedAreaRunner.Run(new[] { Path.Combine(_dir, "no-such-place") });

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("no-such-place", StringComparison.Ordinal));
    }

    /// <summary>DBs, UDTs and tag tables are legitimately not code and are skipped, not refused.</summary>
    [Fact]
    public void NonCodeObjectsAreSkippedWithoutRefusing()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37"));
        Write("DB_Thing.ir", "DB DB_Thing\nROOTID 0\nNUMBER 12\n\nINTERFACE\n  STATIC\n    A : Bool = FALSE\n");

        var report = ServedAreaRunner.Run(new[] { _dir });

        Assert.True(report.Derived, report.Denominator);
        Assert.Equal(37, report.Registers);
        Assert.Equal(2, report.FilesScanned);
        Assert.Equal(1, report.BlocksScanned);
    }

    /// <summary>A single .ir file is a legitimate scope, not only a directory.</summary>
    [Fact]
    public void AFilePathIsAScopeOfItsOwn()
    {
        var path = Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37"));

        var report = ServedAreaRunner.Run(new[] { path });

        Assert.True(report.Derived, report.Denominator);
        Assert.Equal(37, report.Registers);
    }

    // =============================================================================================
    // 5 — THE EXIT-CODE CONTRACT. It is the whole interface `harness-batch` sees, and 2 IS NOT A PASS.
    // =============================================================================================

    private static (int Exit, string Output) Cli(params string[] args)
    {
        var captured = new StringWriter();
        var previous = Console.Out;
        try
        {
            Console.SetOut(captured);
            return (Program.RunServedArea(args), captured.ToString());
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    [Fact]
    public void CliExitsZeroAndPrintsTheDenominatorWhenDerived()
    {
        var (exit, output) = Cli("--project", Corpus);

        Assert.Equal(0, exit);
        Assert.Contains("served area: base 1000, 1024 register(s), derived from ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CliExitsOneOnADisagreementBetweenTheTwoLines()
    {
        Write("FB_Comms_ModbusServer.ir", CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 40"));

        var (exit, output) = Cli("--project", _dir);

        Assert.Equal(1, exit);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CliExitsTwoWhenNothingWasDerived()
    {
        Write("FB_Nothing.ir", PlainBlock);

        var (exit, output) = Cli("--project", _dir);

        Assert.Equal(2, exit);
        Assert.Contains("NOT DERIVED — 1 block(s) in 1 file(s) scanned, no MB_SERVER call", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The corpus is the UNION of several lanes' program paths, and materialising it first would put
    /// the check after the point it is needed. So the scope repeats.
    /// </summary>
    [Fact]
    public void CliTakesARepeatedProjectScope()
    {
        var other = Path.Combine(_dir, "other");
        Directory.CreateDirectory(other);
        Write("FB_Nothing.ir", PlainBlock);
        File.WriteAllText(Path.Combine(other, "FB_Comms_ModbusServer.ir"), CommsBlock("P#M1000.0 WORD 37", "P#M1000.0 WORD 37"));

        var (exit, output) = Cli("--project", _dir, "--project", other, "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"registers\": 37", output, StringComparison.Ordinal);
        Assert.Contains("\"baseByte\": 1000", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A width nobody derived OMITS the key rather than writing 0. A lenient reader turns a 0 into a
    /// comparison that passes against nothing — the same failure `reachable-state` withholds its
    /// closure to avoid.
    /// </summary>
    [Fact]
    public void JsonOmitsTheWidthWhenNothingWasDerived()
    {
        Write("FB_Nothing.ir", PlainBlock);

        var (_, output) = Cli("--project", _dir, "--json");

        Assert.DoesNotContain("\"registers\"", output, StringComparison.Ordinal);
        Assert.Contains("\"derived\": false", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The limit is written into the OUTPUT, not only into a design note — on every run, including a
    /// derived one, because that is the run whose reader is most likely to widen the claim.
    /// </summary>
    [Fact]
    public void EveryRunStatesWhatItCannotSee()
    {
        var (_, derived) = Cli("--project", Corpus);
        Write("FB_Nothing.ir", PlainBlock);
        var (_, notDerived) = Cli("--project", _dir);

        foreach (var output in new[] { derived, notDerived })
        {
            Assert.Contains("whether the block it read is the block on the controller", output, StringComparison.Ordinal);
            Assert.Contains("never the CPU", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CliRefusesAScopeItWasNotGiven()
    {
        Assert.Equal(1, Cli().Exit);
    }
}
