using Harness.Map;
using NModbus;

namespace Harness.MirrorRead.Tests;

/// <summary>
/// 🔴 <b>FC03 carries at most 125 registers, and until 2026-08-22 neither this tool nor
/// <c>harness-mirror-view</c> mentioned that limit anywhere.</b>
///
/// <para>Both issued ONE request over the whole declared area. It worked only because the live run
/// declared 37. At the rig's real <b>576</b> that read is 4.6x the ceiling: the server refuses it, and
/// the viewer renders the refusal as <i>"the area is narrower than the map declares"</i> — accusing the
/// area pointer of a fault that is in the request.</para>
/// </summary>
public class PagedReadTests
{
    /// <summary>
    /// A server that enforces the protocol's own quantity limit, which <see cref="ScriptedSource"/>
    /// deliberately does not — it exists to test the ADDRESS boundary, and adding a quantity rule to it
    /// would change what every other test in this project is measuring.
    /// </summary>
    private sealed class LimitEnforcingSource : IRegisterSource
    {
        private readonly ushort[] _registers;

        internal LimitEnforcingSource(int available)
        {
            _registers = new ushort[available];
            for (var i = 0; i < available; i++)
                _registers[i] = (ushort)(i + 1);
        }

        internal List<(int Start, int Count)> Requests { get; } = new();

        public ushort[] Read(int startRegister, int count)
        {
            Requests.Add((startRegister, count));

            if (count > ModbusLimits.MaxReadRegisters)
            {
                throw new SlaveException(
                    $"scripted: Function Code: 3, Exception Code: 3 - Illegal Data Value "
                    + $"(asked for {count} registers; FC03 carries at most {ModbusLimits.MaxReadRegisters}).");
            }

            if (startRegister < 0 || startRegister + count > _registers.Length)
            {
                throw new SlaveException(
                    $"scripted: Function Code: 3, Exception Code: 2 - Illegal Data Address "
                    + $"(asked for {count} at {startRegister}; this server holds 0..{_registers.Length - 1}).");
            }

            return _registers[startRegister..(startRegister + count)];
        }

        public void Dispose() { }
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>The defect, reproduced: the old single-request form cannot read the rig's real area.</summary>
    [Fact]
    public void An_UNPAGED_read_of_576_registers_is_REFUSED_by_the_protocol_limit()
    {
        var source = new LimitEnforcingSource(576);

        var read = RegisterRead.Perform(source, 0, 576);

        Assert.Equal(ReadOutcome.RefusedByServer, read.Outcome);
        Assert.Single(source.Requests);
    }

    /// <summary>And the paged form gets the same 576 registers, in the transactions the protocol allows.</summary>
    [Fact]
    public void A_PAGED_read_of_576_registers_succeeds_in_five_transactions()
    {
        var source = new LimitEnforcingSource(576);

        var read = RegisterRead.PerformPaged(source, 0, 576);

        Assert.Equal(ReadOutcome.Ok, read.Outcome);
        Assert.Equal(576, read.Values.Length);
        Assert.Equal(5, source.Requests.Count);                 // 125 x 4 + 76
        Assert.All(source.Requests, r => Assert.True(r.Count <= ModbusLimits.MaxReadRegisters));
    }

    /// <summary>
    /// <b>The values must be the SAME values, in the same order.</b> Paging is a transport detail; a
    /// reassembly that dropped or reordered a page would be a silently wrong measurement rather than a
    /// failed one.
    /// </summary>
    [Fact]
    public void The_reassembled_answer_is_identical_to_what_the_registers_hold()
    {
        var source = new LimitEnforcingSource(576);

        var read = RegisterRead.PerformPaged(source, 0, 576);

        Assert.Equal(Enumerable.Range(1, 576).Select(i => (ushort)i).ToArray(), read.Values);

        // And the whole request is what gets reported, not the last page.
        Assert.Equal(0, read.Start);
        Assert.Equal(576, read.Count);
    }

    /// <summary>
    /// A read that already fits is passed straight through — one transaction, not one page of one. The
    /// live 37-register read must keep behaving exactly as it did.
    /// </summary>
    [Fact]
    public void A_read_within_the_limit_is_still_ONE_transaction()
    {
        var source = new LimitEnforcingSource(576);

        var read = RegisterRead.PerformPaged(source, 0, 37);

        Assert.Equal(ReadOutcome.Ok, read.Outcome);
        Assert.Single(source.Requests);
        Assert.Equal((0, 37), source.Requests[0]);
    }

    /// <summary>
    /// 🔴 <b>A refusal inside a page is reported as ITSELF.</b> Claiming the whole span would assert a
    /// boundary at an address the tool never asked about — the same class of false finding paging is
    /// being introduced to remove.
    /// </summary>
    [Fact]
    public void A_page_that_is_refused_reports_its_OWN_span_and_names_the_whole_request()
    {
        // The server holds 300; the caller believes 576. The overflow lands in the third page.
        var source = new LimitEnforcingSource(300);

        var read = RegisterRead.PerformPaged(source, 0, 576);

        Assert.Equal(ReadOutcome.RefusedByServer, read.Outcome);
        Assert.Equal(250, read.Start);
        Assert.Equal(125, read.Count);
        Assert.Contains("of the 576-register read from 0", read.Failure);
    }

    /// <summary>
    /// The boundary probe's own read must NOT be paged, and this pins the shape it relies on: one
    /// request for one register past the area, answered with an exception. If <c>Perform</c> ever
    /// started paging, that measurement would be split and the refusal masked.
    /// </summary>
    [Fact]
    public void The_UNPAGED_single_register_probe_past_the_area_still_reads_as_a_refusal()
    {
        var source = new LimitEnforcingSource(576);

        var read = RegisterRead.Perform(source, 576, 1);

        Assert.Equal(ReadOutcome.RefusedByServer, read.Outcome);

        // The ADDRESS refusal, not the quantity one — a single register is inside every limit, so the
        // only thing that can refuse it is being past the area. Asserted on the message rather than
        // SlaveExceptionCode because this fixture raises the exception directly, and NModbus only
        // populates that property on a response it decoded off a wire.
        Assert.Contains("Exception Code: 2", read.Failure);
        Assert.Single(source.Requests);
    }

    /// <summary>A page size below one reads nothing, and is refused rather than looping forever.</summary>
    [Fact]
    public void A_page_size_of_zero_is_refused()
    {
        var source = new LimitEnforcingSource(10);

        Assert.Throws<ArgumentOutOfRangeException>(() => RegisterRead.PerformPaged(source, 0, 10, pageSize: 0));
    }
}
