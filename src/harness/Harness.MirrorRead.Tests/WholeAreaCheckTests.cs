using Harness.Map;
using Harness.Wire;
using NModbus;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// 🔴 <b>THE VERDICT THAT COULD NOT BE REACHED.</b>
///
/// <para>Step 2 asks whether a Modbus client can see the whole declared area. It asked it as ONE
/// <c>FC03(0, n)</c>, and FC03 carries at most 125 registers — so on every real area here (576, then
/// 1024) the request was rejected by the client's own bounds check before a packet left, the run
/// recorded a finding, and <c>exit 0</c> was unattainable. Two live runs on 2026-08-23 produced a
/// perfect boundary measurement under <c>RESULT: 1 finding(s). This run does NOT pass.</c></para>
///
/// <para><b>Why these tests use a source that enforces the quantity limit.</b>
/// <see cref="ScriptedSource"/> deliberately does not — it exists to measure the ADDRESS boundary, and
/// a quantity rule in it would change what every other test in this file's neighbours is measuring. So
/// the defect was invisible to the whole existing suite: every fixture answered a 1,024-register read
/// happily, and the one thing that would not was the real transport.</para>
///
/// <para>⚠️ And the limit is enforced the way the REAL path enforces it: <c>NModbusTransport.Bounds</c>
/// throws a <see cref="WireException"/> <i>before</i> the socket, which
/// <see cref="RegisterRead.Perform"/> classifies as <see cref="ReadOutcome.TransportFailed"/> — not as
/// a server refusal. A fixture that raised a <c>SlaveException</c> here would exercise the wrong branch
/// of the verdict and would have let the paging tests pass while the tool stayed broken.</para>
/// </summary>
public class WholeAreaCheckTests : IDisposable
{
    private readonly string _allowlist;
    private readonly string _directory =
        Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "mirror-read-wide-" + Guid.NewGuid().ToString("N"))).FullName;

    public WholeAreaCheckTests()
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

    /// <summary>
    /// A server holding <paramref name="available"/> registers that enforces BOTH protocol rules the
    /// real path enforces, and keeps them apart the way the real path does.
    ///
    /// <para>The quantity rule is the client's (a <see cref="WireException"/>, thrown before any
    /// packet); the address rule is the server's (a <see cref="SlaveException"/>, exception code 2). One
    /// is a failed measurement and the other IS the measurement, and a fixture that blurred them would
    /// make the boundary probe untestable.</para>
    /// </summary>
    private sealed class ProtocolLimitedSource : IRegisterSource
    {
        private readonly ushort[] _registers;

        internal ProtocolLimitedSource(int available)
        {
            _registers = new ushort[available];
            _registers[0] = 0xF52E;
            _registers[1] = 0xCEAD;
        }

        internal List<(int Start, int Count)> Requests { get; } = new();

        public ushort[] Read(int startRegister, int count)
        {
            Requests.Add((startRegister, count));

            if (count > ModbusLimits.MaxReadRegisters)
            {
                throw new WireException(
                    $"FC03 read of {count} register(s) exceeds the {ModbusLimits.MaxReadRegisters}-register protocol limit. "
                    + "Splitting it here would hide a map that was derived wrong; the map refuses this at derivation time.");
            }

            if (startRegister < 0 || startRegister + count > _registers.Length)
            {
                throw new SlaveException(
                    $"scripted: Function Code: 3, Exception Code: 2 - Illegal Data Address "
                    + $"(asked for {count} at {startRegister}; this server holds 0..{_registers.Length - 1}).");
            }

            var answer = _registers[startRegister..(startRegister + count)];
            Advance();
            return answer;
        }

        private void Advance()
        {
            var scan = ((uint)_registers[2] << 16) | _registers[3];
            scan = unchecked(scan + 7);
            _registers[2] = (ushort)(scan >> 16);
            _registers[3] = (ushort)(scan & 0xFFFF);
        }

        public void Dispose() { }
    }

    private (MirrorReadExit Exit, string Output) Run(IRegisterSource source, int declared)
    {
        var output = new StringWriter();
        var options = new MirrorReadOptions(
            "10.10.10.10", 503, 1, _allowlist, declared, declared - 3, declared + 1, IntervalMs: 0);
        var exit = MirrorReadRun.Execute(options, new RecordingFactory(() => source), output);
        return (exit, output.ToString());
    }

    // ---- the verdict that must become reachable --------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE TEST THAT FAILS AGAINST THE OLD BINARY.</b> A healthy area of the rig's real width:
    /// 1,024 registers present, register 1024 refused by the server. Nothing is wrong with it, and the
    /// run must be able to say so.
    /// </summary>
    [Fact]
    public void A_healthy_area_far_wider_than_one_FC03_reaches_a_PASSING_verdict()
    {
        var (exit, output) = Run(new ProtocolLimitedSource(1024), declared: 1024);

        Assert.Equal(MirrorReadExit.Ok, exit);
        Assert.Contains("EXACTLY 1024 register(s) wide", output, StringComparison.Ordinal);
        Assert.DoesNotContain("does NOT pass", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// And it reaches it by asking about EVERY register, not by asking about fewer. The paged read is a
    /// change of transport, not a change of question — a pass obtained by narrowing the span would be
    /// the tool answering something easier and reporting it as the same result.
    /// </summary>
    [Fact]
    public void The_pass_covers_every_declared_register_in_transactions_the_protocol_allows()
    {
        var source = new ProtocolLimitedSource(1024);

        var (exit, output) = Run(source, declared: 1024);

        Assert.Equal(MirrorReadExit.Ok, exit);

        // Every bulk page is legal...
        var bulk = source.Requests.Where(r => r.Count > 1).ToList();
        Assert.All(bulk, r => Assert.True(r.Count <= ModbusLimits.MaxReadRegisters,
            $"FC03({r.Start}, {r.Count}) is past the {ModbusLimits.MaxReadRegisters}-register limit."));

        // ...and together they cover 0..1023 with no gap, which is the claim the step makes.
        var wholeArea = source.Requests.Where(r => r.Start + r.Count <= 1024 && r.Count > 1).ToList();
        var covered = new bool[1024];
        foreach (var (start, count) in wholeArea)
            for (var i = start; i < start + count; i++)
                covered[i] = true;
        Assert.DoesNotContain(false, covered);

        Assert.Contains("registers 0..1023", output, StringComparison.Ordinal);
    }

    // ---- the negative controls, which must survive the fix ---------------------------------------

    /// <summary>
    /// *** THE HEADLINE FAILURE, AT A WIDTH ABOVE ONE FC03. *** The server holds 1,000; the IR declares
    /// 1,024. Paging must not turn that into a pass by quietly succeeding on the pages that fit — the
    /// widening did not reach the wire and the run must exit 6.
    /// </summary>
    [Fact]
    public void An_area_NARROWER_than_declared_still_fails_at_a_width_above_one_FC03()
    {
        var (exit, output) = Run(new ProtocolLimitedSource(1000), declared: 1024);

        Assert.Equal(MirrorReadExit.NarrowerThanDeclared, exit);
        Assert.Contains("NARROWER THAN DECLARED", output, StringComparison.Ordinal);
        Assert.Contains("does NOT pass", output, StringComparison.Ordinal);
        Assert.DoesNotContain("EXACTLY 1024 register(s) wide", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// And the other side. The server holds 2,048; the IR declares 1,024. Every declared register reads
    /// perfectly, which is exactly why the far side is probed — exit 7.
    /// </summary>
    [Fact]
    public void An_area_WIDER_than_declared_still_fails_at_a_width_above_one_FC03()
    {
        var (exit, output) = Run(new ProtocolLimitedSource(2048), declared: 1024);

        Assert.Equal(MirrorReadExit.WiderThanDeclared, exit);
        Assert.Contains("WIDER THAN DECLARED", output, StringComparison.Ordinal);
        Assert.DoesNotContain("EXACTLY 1024 register(s) wide", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// A silence in the middle of the bulk read is still a failed measurement, not a boundary. The
    /// server holds 1,024 but says nothing about the page starting at 500: no verdict about the width
    /// may rest on that, so the run reports NOT ESTABLISHED rather than either boundary finding.
    /// </summary>
    [Fact]
    public void A_page_that_says_nothing_is_NOT_ESTABLISHED_not_a_boundary()
    {
        var (exit, output) = Run(new SilentPageSource(1024, silentPageStart: 500), declared: 1024);

        Assert.Equal(MirrorReadExit.NotEstablished, exit);
        Assert.DoesNotContain("EXACTLY 1024 register(s) wide", output, StringComparison.Ordinal);
        Assert.DoesNotContain("NARROWER THAN DECLARED", output, StringComparison.Ordinal);
    }

    // ---- the report names the transaction that was actually issued -------------------------------

    /// <summary>
    /// 🔴 <b>A FAILING PAGE IS NAMED AS ITSELF, AND THE WHOLE SPAN IS NOT.</b>
    ///
    /// <para>The step-2 label was hardcoded <c>FC03(0, n)</c> whatever came back, so the page that
    /// actually failed — <c>FC03(1000, 24)</c> — was printed as a 1,024-register read from register 0: a
    /// request nobody made, at an address nobody asked about. <c>PerformPaged</c> returns the failing
    /// page's own <c>Start</c> and <c>Count</c> precisely so this cannot happen, and the printer was
    /// discarding them. That is the same false attribution paging exists to remove, left standing in the
    /// line that reports it.</para>
    /// </summary>
    [Fact]
    public void A_failing_page_is_named_as_itself_and_the_whole_span_is_not()
    {
        var (exit, output) = Run(new ProtocolLimitedSource(1000), declared: 1024);

        Assert.Equal(MirrorReadExit.NarrowerThanDeclared, exit);
        Assert.Contains("FC03(1000, 24)", output, StringComparison.Ordinal);
        Assert.Contains("page 9 of 9", output, StringComparison.Ordinal);

        // The transaction that was never issued must not be reported as the one that failed.
        Assert.DoesNotContain("FC03(0, 1024)", output, StringComparison.Ordinal);

        // And the finding no longer misdescribes nine transactions as one.
        Assert.DoesNotContain("could not be read in one FC03", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The passing run states its DENOMINATOR — how many registers were read whole and in how many
    /// transactions. Every other number in the report is a reason something did not happen; this is the
    /// one that says how much was looked at.
    /// </summary>
    [Fact]
    public void A_passing_run_states_how_much_it_examined()
    {
        var (exit, output) = Run(new ProtocolLimitedSource(1024), declared: 1024);

        Assert.Equal(MirrorReadExit.Ok, exit);
        Assert.Contains("EXAMINED    : 1024 register(s) read whole in 9 FC03 transaction(s)", output, StringComparison.Ordinal);
        Assert.Contains("5 single-register probe(s)", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>What the pass cannot see, written down where a reader of the pass will meet it.</b> Nine
    /// transactions are nine moments on a live mirror, so the reassembled words are not a snapshot. The
    /// step measures REACHABILITY, which is per-register and survives that; nothing about it licenses
    /// reading two registers from different pages as a coherent pair.
    /// </summary>
    [Fact]
    public void A_multi_page_pass_says_the_words_are_a_reassembly_and_not_a_snapshot()
    {
        var (_, output) = Run(new ProtocolLimitedSource(1024), declared: 1024);

        Assert.Contains("NOT SEEN", output, StringComparison.Ordinal);
        Assert.Contains("NOT one snapshot", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// ⚠️ <b>AN AREA THAT FITS IN ONE FC03 IS STILL ONE FC03, LABELLED THE WAY IT ALWAYS WAS.</b> The
    /// live 37-register run must not acquire paging vocabulary it did not earn — and the snapshot caveat
    /// above is FALSE of a single transaction, so it must not appear on one.
    /// </summary>
    [Fact]
    public void An_area_inside_one_FC03_keeps_its_single_transaction_report()
    {
        var source = new ProtocolLimitedSource(37);

        var (exit, output) = Run(source, declared: 37);

        Assert.Equal(MirrorReadExit.Ok, exit);
        Assert.Contains("FC03(0, 37)", output, StringComparison.Ordinal);
        Assert.Contains("in 1 FC03(s)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("NOT SEEN", output, StringComparison.Ordinal);
        Assert.DoesNotContain("x FC03 ->", output, StringComparison.Ordinal);

        // One bulk transaction, over the whole width. Not one page of one.
        Assert.Single(source.Requests.Where(r => r.Count == 37));
    }

    /// <summary>A wide, healthy server with one page that times out instead of answering.</summary>
    private sealed class SilentPageSource : IRegisterSource
    {
        private readonly ProtocolLimitedSource _inner;
        private readonly int _silentPageStart;

        internal SilentPageSource(int available, int silentPageStart)
        {
            _inner = new ProtocolLimitedSource(available);
            _silentPageStart = silentPageStart;
        }

        public ushort[] Read(int startRegister, int count)
        {
            if (startRegister == _silentPageStart && count > 1)
                throw new TimeoutException($"scripted: the server said nothing about the page at {startRegister}.");

            return _inner.Read(startRegister, count);
        }

        public void Dispose() => _inner.Dispose();
    }
}
