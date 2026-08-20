using Harness.CmdInject;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The sequence allocator: never a repeat in a row, never zero, wrap at the 16-bit edge, and the
/// device-ahead / client-ahead reconciliation.
/// </summary>
public class SequenceLedgerTests
{
    [Fact]
    public void FirstAllocation_IsOneNotZero()
    {
        var ledger = new SequenceLedger();
        Assert.Null(ledger.LastAllocated);
        Assert.Equal(1, ledger.Allocate());
        Assert.Equal((ushort)1, ledger.LastAllocated);
    }

    [Fact]
    public void ConsecutiveAllocations_AreNeverEqualAndNeverZero()
    {
        var ledger = new SequenceLedger();
        ushort previous = ledger.Allocate();

        for (var i = 0; i < 5000; i++)
        {
            var next = ledger.Allocate();
            Assert.NotEqual(previous, next);
            Assert.NotEqual((ushort)0, next);
            previous = next;
        }
    }

    [Fact]
    public void WithinACycle_EveryValueIsDistinctAndZeroNeverAppears()
    {
        var ledger = new SequenceLedger();
        var seen = new HashSet<ushort>();

        for (var i = 0; i < 65535; i++)
            Assert.True(seen.Add(ledger.Allocate()), "a value repeated inside a single cycle.");

        Assert.Equal(65535, seen.Count);
        Assert.DoesNotContain((ushort)0, seen);
    }

    [Fact]
    public void ItWrapsFrom65535ToOne_SkippingZero()
    {
        var ledger = new SequenceLedger();
        ushort last = 0;
        for (var i = 0; i < 65535; i++)
            last = ledger.Allocate();

        Assert.Equal((ushort)65535, last);
        Assert.Equal((ushort)1, ledger.Allocate()); // 65535 + 1 wraps to 0, which is skipped.
    }

    [Fact]
    public void Peek_ShowsTheNextWithoutConsumingIt()
    {
        var ledger = new SequenceLedger();
        Assert.Equal((ushort)1, ledger.Peek());
        Assert.Equal((ushort)1, ledger.Peek()); // still 1 — not consumed.
        Assert.Equal((ushort)1, ledger.Allocate());
        Assert.Equal((ushort)2, ledger.Peek());
    }

    [Fact]
    public void Reconcile_InSync_WhenDeviceAckEqualsOurLatest()
    {
        var ledger = new SequenceLedger();
        var mine = ledger.Allocate();
        Assert.Equal(SequenceStanding.InSync, ledger.Reconcile(mine));
    }

    [Fact]
    public void Reconcile_ClientAhead_WhenDeviceIsBehindOurLatest()
    {
        var ledger = new SequenceLedger();
        ledger.Allocate(); // last = 1
        Assert.Equal(SequenceStanding.ClientAhead, ledger.Reconcile(0)); // device one behind.
    }

    [Fact]
    public void Reconcile_DeviceAhead_WhenDeviceAckIsBeyondAnythingWeAllocated()
    {
        var ledger = new SequenceLedger();
        ledger.Allocate(); // last = 1
        Assert.Equal(SequenceStanding.DeviceAhead, ledger.Reconcile(5)); // device four ahead — a stranger, or a restart.
    }
}
