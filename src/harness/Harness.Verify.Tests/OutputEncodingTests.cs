using System.Text;

namespace Harness.Verify.Tests;

/// <summary>
/// 🔴 <b>EVERY CHARACTER THIS TOOL PRINTS IS ASCII, AND IT IS CHECKED RATHER THAN INTENDED.</b>
///
/// <para><b>Measured on this tool's own first live run.</b> The report is the product: it gets captured,
/// pasted into a lane report and read by a person. Written with the repo's notation — em dashes, the
/// section sign, the red-circle marker — it came back through the Windows console with U+FFFD in place of
/// each one, in exactly the paragraph explaining what <c>ReadsToSettle</c> does and does not measure.</para>
///
/// <para><b><c>Console.OutputEncoding = UTF8</c> was tried first and removed.</b> It works, and it can
/// THROW on a handle that will not take it — after which the report is mangled again, silently. <i>A flag
/// whose omission yields a plausible artifact is not optional; it is a defect with a default</i>, and this
/// was the same shape one level down. ASCII text cannot fail that way.</para>
///
/// <para><b>Only EMITTED text is constrained.</b> The XML doc comments throughout keep the repo's
/// notation, because nothing prints them.</para>
///
/// <para><b>The paths swept are the ones with prose in them</b>, not merely the happy one: the usage
/// banner, every usage refusal, and the full report over three device states. A test that checked one path
/// would pass while the refusal a reader actually meets was mangled.</para>
/// </summary>
public class OutputEncodingTests : IDisposable
{
    private readonly string _allowlist;

    public OutputEncodingTests()
    {
        _allowlist = Path.Combine(Path.GetTempPath(), $"verify-ascii-{Guid.NewGuid():N}.json");
        File.WriteAllText(_allowlist, """
        { "entries": [ { "address": "10.0.0.1", "kind": "test-rig", "label": "scripted", "writeEligible": false } ] }
        """);
    }

    public void Dispose()
    {
        if (File.Exists(_allowlist)) File.Delete(_allowlist);
        GC.SuppressFinalize(this);
    }

    private static void AssertAscii(string what, string text)
    {
        var offenders = text
            .Where(c => c > '\u007F')
            .Select(c => $"U+{(int)c:X4} '{c}'")
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            offenders.Length == 0,
            $"{what} emits non-ASCII, which a Windows console renders as U+FFFD in the text somebody pastes into a report: "
            + string.Join(", ", offenders));

        // *** THE DENOMINATOR. *** An empty string is ASCII, and a path that printed nothing would satisfy
        // the assertion above while measuring nothing.
        Assert.True(text.Length > 200, $"{what} produced only {text.Length} character(s); this assertion swept almost nothing.");
    }

    /// <summary>The whole report, over three different device states, because the prose differs in each.</summary>
    [Theory]
    [InlineData(0xF52ECEADu)]  // stale  - the live shape
    [InlineData(0u)]           // absent - the acceptance control cannot be built
    public void The_report_is_ascii(uint deviceStamp)
    {
        var (map, _) = Fixtures.Composed();
        var output = new StringWriter();

        VerifyRun.Execute(
            Fixtures.Options(_allowlist),
            Fixtures.Files, Fixtures.Expand,
            new RecordingConnect(() => new ScriptedTransport(map, deviceStamp)).Factory,
            output);

        AssertAscii($"the report over a device publishing 16#{deviceStamp:X8}", output.ToString());
    }

    /// <summary>The confirmed path prints different prose again, so it is swept separately.</summary>
    [Fact]
    public void The_confirmed_report_is_ascii()
    {
        var (map, stamp) = Fixtures.Composed();
        var output = new StringWriter();

        VerifyRun.Execute(
            Fixtures.Options(_allowlist),
            Fixtures.Files, Fixtures.Expand,
            new RecordingConnect(() => new ScriptedTransport(map, stamp.Value)).Factory,
            output);

        AssertAscii("the confirmed report", output.ToString());
    }

    /// <summary>Refusal banners are what a reader meets when something has already gone wrong. They are swept too.</summary>
    [Theory]
    [InlineData("fence")]
    [InlineData("no-device")]
    [InlineData("bad-binding")]
    [InlineData("bad-program")]
    public void Every_refusal_banner_is_ascii(string shape)
    {
        var output = new StringWriter();

        Func<string, string> files = shape switch
        {
            "bad-binding" => path => path == Fixtures.BindingPath ? """{ "blockName": "FC_X", "slots": [] }""" : Fixtures.Files(path),
            "bad-program" => path => path == Fixtures.ProgramPath ? "not an IR header at all" : Fixtures.Files(path),
            _ => Fixtures.Files,
        };

        VerifyRun.Execute(
            Fixtures.Options(_allowlist, shape == "fence" ? "192.168.0.99" : "10.0.0.1"),
            files, Fixtures.Expand, new RecordingConnect().Factory, output);

        var text = output.ToString();

        Assert.True(
            text.All(c => c <= '\u007F'),
            $"the '{shape}' refusal emits non-ASCII: "
            + string.Join(", ", text.Where(c => c > '\u007F').Distinct().Select(c => $"U+{(int)c:X4}")));

        Assert.True(text.Length > 100, $"the '{shape}' refusal printed only {text.Length} character(s).");
    }

    /// <summary>
    /// <b>The control that proves the sweep can fail.</b> Without it, an assertion over an ASCII-only
    /// string set is equally what a broken comparison produces — and <c>c &gt; '\u007F'</c> is exactly the
    /// kind of expression that can be quietly wrong.
    /// </summary>
    [Fact]
    public void The_sweep_detects_a_planted_non_ascii_character()
    {
        var planted = new string('x', 300) + "—" + new string('y', 10);

        var thrown = Record.Exception(() => AssertAscii("a planted string", planted));

        Assert.NotNull(thrown);
        Assert.Contains("U+2014", thrown!.Message, StringComparison.Ordinal);
    }

    /// <summary>And the converse control: an ordinary ASCII report of adequate length must pass silently.</summary>
    [Fact]
    public void The_sweep_passes_an_ordinary_ascii_report()
    {
        AssertAscii("a plain string", new string('z', 300));
    }

    /// <summary>
    /// The usage banner, printed by <c>Program.Usage</c> — reached through the process's own entry point so
    /// the text an operator sees is the text swept.
    /// </summary>
    [Fact]
    public void The_usage_banner_is_ascii()
    {
        var captured = new StringWriter();
        var previous = Console.Out;

        try
        {
            Console.SetOut(captured);
            Program.Main(new[] { "--help" });
        }
        finally
        {
            Console.SetOut(previous);
        }

        AssertAscii("the usage banner", captured.ToString());
    }

    /// <summary>
    /// A guard against the obvious way this whole file could become vacuous: if the writer were ever handed
    /// an encoding that silently dropped high characters, every assertion above would pass over text that
    /// had already lost them. <see cref="StringWriter"/> holds UTF-16 and drops nothing.
    /// </summary>
    [Fact]
    public void The_capture_used_by_these_tests_preserves_non_ascii()
    {
        var writer = new StringWriter();
        writer.Write("§—");

        Assert.Equal("§—", writer.ToString());
        Assert.Equal(Encoding.Unicode.WebName, writer.Encoding.WebName);
    }
}
