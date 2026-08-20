using Harness.CmdInject;
using Harness.Map;

namespace Harness.CmdInject.Tests;

/// <summary>
/// The poll's read plan — <b>the correction to the plan's "the identity re-check is free".</b>
///
/// <para>It is free when the acknowledgement registers lie within one FC03 of register zero, and it is a
/// second round trip when they do not. Both cases are computed rather than assumed, and the run says which
/// one it is in. What is NOT conditional is whether the re-check happens: a cheap identity check is still
/// worth more than a write against a device that has been re-downloaded underneath you.</para>
/// </summary>
public class PollPlanTests
{
    [Fact]
    public void AnAckSpanInsideOneFC03_RidesOnThePollsOwnRead()
    {
        var plan = PollPlan.For(ackFirst: 40, ackLast: 43);

        Assert.True(plan.ControlSharesTheAckRead);
        Assert.Single(plan.Reads);
        Assert.Equal(new RegisterSpan(0, 44), plan.Reads[0]);
        Assert.Contains("cost nothing", plan.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAckSpanAtTheExactLimit_StillRidesAlong()
    {
        var plan = PollPlan.For(ackFirst: 0, ackLast: ModbusLimits.MaxReadRegisters - 1);

        Assert.True(plan.ControlSharesTheAckRead);
        Assert.Single(plan.Reads);
    }

    [Fact]
    public void AnAckSpanOneRegisterPastTheLimit_CostsASecondRead()
    {
        var plan = PollPlan.For(ackFirst: 100, ackLast: ModbusLimits.MaxReadRegisters);

        Assert.False(plan.ControlSharesTheAckRead);
        Assert.Equal(2, plan.Reads.Count);
        Assert.Equal(new RegisterSpan(0, ControlRegisters.Count), plan.Reads[0]);
        Assert.Equal(new RegisterSpan(100, ModbusLimits.MaxReadRegisters - 100 + 1), plan.Reads[1]);
        Assert.Contains("It is still made", plan.Basis, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryPlan_CoversTheControlRegistersAndTheWholeAckSpan()
    {
        foreach (var (first, last) in new[] { (4, 7), (200, 203), (60, 130) })
        {
            var plan = PollPlan.For(first, last);

            for (var register = 0; register < ControlRegisters.Count; register++)
                Assert.Contains(plan.Reads, span => span.Covers(register));

            for (var register = first; register <= last; register++)
                Assert.Contains(plan.Reads, span => span.Covers(register));
        }
    }

    [Fact]
    public void ASpanThatEndsBeforeItBegins_IsRefused() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PollPlan.For(ackFirst: 10, ackLast: 9));
}
