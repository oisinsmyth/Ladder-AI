using NModbus;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// WHAT THE READINGS LICENSE — each verdict tested against a server built to produce exactly that
/// situation.
///
/// <para>The pairing that matters: <see cref="AnAreaOfExactlyTheDeclaredWidth_Passes"/> and
/// <see cref="AnAreaNarrowerThanDeclared_FailsAndSaysTheWideningDidNotReachTheWire"/> differ by ONE
/// register in the fixture. A verdict that could not tell them apart would be the whole tool failing
/// silently, and it is the failure this lane exists to make impossible.</para>
/// </summary>
public class MirrorReadVerdictTests : IDisposable
{
    private readonly string _allowlist;
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mirror-read-verdict-" + Guid.NewGuid().ToString("N"))).FullName;

    public MirrorReadVerdictTests()
    {
        _allowlist = Path.Combine(_directory, "allowlist.json");
        File.WriteAllText(_allowlist, """
            { "entries": [ { "address": "10.10.10.10", "label": "fixture", "kind": "test-rig" } ] }
            """);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not a test failure.
        }
    }

    private MirrorReadOptions Options(int declared = 37) =>
        new("10.10.10.10", 503, 1, _allowlist, declared, declared - 3, declared + 1, IntervalMs: 0);

    private (MirrorReadExit Exit, string Output) Run(IRegisterSource source, int declared = 37)
    {
        var output = new StringWriter();
        var exit = MirrorReadRun.Execute(Options(declared), new RecordingFactory(() => source), output);
        return (exit, output.ToString());
    }

    // ---- the measurement this lane was built for -------------------------------------------------

    /// <summary>
    /// The area is exactly 37 registers: 0..36 answer, 37 and 38 are refused BY THE SERVER. Both sides
    /// measured, so the width is pinned rather than bounded from below.
    /// </summary>
    [Fact]
    public void AnAreaOfExactlyTheDeclaredWidth_Passes()
    {
        var (exit, output) = Run(new ScriptedSource(available: 37));

        Assert.Equal(MirrorReadExit.Ok, exit);
        Assert.Contains("EXACTLY 37 register(s) wide", output, StringComparison.Ordinal);
        Assert.Contains("registers 35 and 36 WERE READ", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE FINDING THE BRIEF ASKS FOR BY NAME. *** The server still holds only 35 registers, so the
    /// two latch registers the widening was for are refused. This must FAIL, and must not be repaired
    /// into a pass by reading a narrower range.
    /// </summary>
    [Fact]
    public void AnAreaNarrowerThanDeclared_FailsAndSaysTheWideningDidNotReachTheWire()
    {
        var (exit, output) = Run(new ScriptedSource(available: 35));

        Assert.Equal(MirrorReadExit.NarrowerThanDeclared, exit);
        Assert.Contains("NARROWER THAN DECLARED", output, StringComparison.Ordinal);
        Assert.Contains("did not reach the", output, StringComparison.Ordinal);
        Assert.Contains("does NOT pass", output, StringComparison.Ordinal);

        // The registers inside 0..34 still read perfectly. A tool that reported that as a success is
        // exactly the failure being guarded against.
        Assert.DoesNotContain("EXACTLY 37", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A server exposing more than declared. This is why reading 35 and 36 successfully is NOT on its
    /// own a proof of the width — the same reading is produced by a server exposing hundreds.
    /// </summary>
    [Fact]
    public void AnAreaWiderThanDeclared_Fails()
    {
        var (exit, output) = Run(new ScriptedSource(available: 64));

        Assert.Equal(MirrorReadExit.WiderThanDeclared, exit);
        Assert.Contains("WIDER THAN DECLARED", output, StringComparison.Ordinal);
    }

    // ---- a silence is not a refusal --------------------------------------------------------------

    /// <summary>
    /// *** THE PROBE THAT SAYS NOTHING MEASURES NOTHING. *** The server is wide enough, but register 37
    /// times out instead of answering. A tool that read that as "refused" would report a boundary it
    /// never observed — and would do so on precisely the run where the network was misbehaving.
    /// </summary>
    [Fact]
    public void AProbeThatTimesOut_IsNotReadAsARefusal()
    {
        var (exit, output) = Run(new ScriptedSource(37, silent: new[] { 37 }));

        Assert.Equal(MirrorReadExit.NotEstablished, exit);
        Assert.Contains("NOT ESTABLISHED", output, StringComparison.Ordinal);
        Assert.Contains("failed measurement, not a refusal", output, StringComparison.Ordinal);
        Assert.DoesNotContain("EXACTLY 37 register(s) wide", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A hand-built <c>SlaveException</c> still classifies as a server refusal. This pins the
    /// CLASSIFICATION as type-based rather than message-based or code-based: NModbus populates the code
    /// from a real response, and a fixture cannot, so a classifier that keyed on the code would treat
    /// every simulated refusal as a transport failure and every test above would be measuring the
    /// wrong branch.
    /// </summary>
    [Fact]
    public void ASlaveExceptionIsClassifiedAsARefusal_ByType()
    {
        var read = RegisterRead.Perform(new ThrowingSource(new SlaveException("scripted")), 0, 1);

        Assert.Equal(ReadOutcome.RefusedByServer, read.Outcome);
    }

    [Fact]
    public void AnyOtherFailureIsClassifiedAsTransportFailed()
    {
        var read = RegisterRead.Perform(new ThrowingSource(new TimeoutException("scripted")), 0, 1);

        Assert.Equal(ReadOutcome.TransportFailed, read.Outcome);
        Assert.Null(read.SlaveExceptionCode);
    }

    // ---- liveness --------------------------------------------------------------------------------

    /// <summary>
    /// A stopped copy layer: every register readable, the stamp correct, and the scan counter frozen.
    /// The stamp says WHICH program is loaded; only the counter says it is EXECUTING.
    /// </summary>
    [Fact]
    public void AFrozenScanCounter_Fails()
    {
        var (exit, output) = Run(new ScriptedSource(available: 37, scanPerRead: 0));

        Assert.Equal(MirrorReadExit.ScanCounterNotRising, exit);
        Assert.Contains("did not advance", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The raw words are on the page before any 32-bit interpretation of them. A decode that hides the
    /// bytes hides a wrong word-order assumption, and word order here is a MEASURED property of this
    /// rig rather than a derivation.
    /// </summary>
    [Fact]
    public void TheReportCarriesTheRawRegisters_NotOnlyTheDecode()
    {
        var (_, output) = Run(new ScriptedSource(available: 37, stamp: 0xF52ECEAD));

        Assert.Contains("r0 = 16#F52E", output, StringComparison.Ordinal);
        Assert.Contains("r1 = 16#CEAD", output, StringComparison.Ordinal);
        Assert.Contains("build stamp 16#F52ECEAD", output, StringComparison.Ordinal);
        Assert.Contains("HIGH-WORD-FIRST", output, StringComparison.Ordinal);
    }

    /// <summary>The two latch registers are reported with whatever is in them, zero included.</summary>
    [Fact]
    public void TheLatchRegistersAreReported_WithTheirRawValues()
    {
        var source = new ScriptedSource(available: 37);
        source.Poke(35, 0x1234);
        source.Poke(36, 0x5678);

        var (_, output) = Run(source);

        Assert.Contains("1234", output, StringComparison.Ordinal);
        Assert.Contains("5678", output, StringComparison.Ordinal);
    }

    // ---- the truncation announces itself ---------------------------------------------------------

    /// <summary>
    /// NModbus attaches a 150-word stock explanation of exception code 2 to every refusal, and printing
    /// it on every probe row buries the measurement. The device's own facts — the function code and the
    /// exception code — are kept, and *** THE DROP IS ANNOUNCED WITH ITS SIZE ***, because a narrowing
    /// nobody can see becomes a place to hide: a reader must be able to tell "the library said nothing
    /// more" from "something was cut".
    /// </summary>
    [Fact]
    public void TheStockExplanationIsDropped_AndTheDropIsAnnounced()
    {
        var message = "NModbus.SlaveException: thrown.\nFunction Code: 131\nException Code: 2 - " +
                      new string('x', 500);

        var kept = RegisterRead.DeviceFactsFrom(message);

        Assert.Contains("Function Code: 131", kept, StringComparison.Ordinal);
        Assert.Contains("chars of NModbus's stock explanation", kept, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 200), kept, StringComparison.Ordinal);
    }

    /// <summary>A short message loses nothing, and says so by NOT announcing a drop.</summary>
    [Fact]
    public void AShortMessageIsNotTruncated_AndAnnouncesNoDrop()
    {
        var kept = RegisterRead.DeviceFactsFrom("scripted refusal");

        Assert.Equal("scripted refusal", kept);
    }

    /// <summary>A source that throws whatever it was handed, for the classification tests.</summary>
    private sealed class ThrowingSource : IRegisterSource
    {
        private readonly Exception _failure;

        internal ThrowingSource(Exception failure) => _failure = failure;

        public ushort[] Read(int startRegister, int count) => throw _failure;

        public void Dispose()
        {
        }
    }
}
