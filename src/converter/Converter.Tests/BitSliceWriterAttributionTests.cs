using System;
using System.IO;
using Converter.Ir;
using Converter.SignalSet;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 <b>A MEMBER WRITTEN ONLY AS BIT SLICES IS WRITTEN, AND IT USED TO REPORT AS <c>unused</c>.</b>
///
/// <para><b>MEASURED ON A REAL JOB, 2026-09-01.</b> A motor block drives its alarm word one bit at a
/// time — <c>COIL IO.Alarm.%X0 := …</c>, <c>%X1</c>, <c>%X2</c> — and never as a whole. Asked about
/// <c>IO.Alarm</c>, <c>signal-set</c> returned <c>direction: "unused", writers: [], readers: []</c>.
/// Its whole-written sibling <c>IO.FTR</c> resolved correctly in the same run, which is what pinned
/// the blindness to the bit-slice case rather than to the block.</para>
///
/// <para><b>WHY IT MATTERS MORE THAN ONE MEMBER.</b> <c>signal-set</c>'s stated reader is a
/// GENERATOR, which is why that command gates on PARTIAL rather than warning — "a generator never
/// sees a warning line". This defect does not produce a partial. It produces a confident
/// <c>unused</c> for a member that is driven, and a harness binding scaffolded from it calls the
/// signal undriven. On the job that found it, EVERY alarm word on the plant is written this way.</para>
///
/// <para><b>THE FIX IS ON THE GRAPH, NOT IN <c>signal-set</c>.</b>
/// <c>ProjectUsageGraph.UsagesReaching</c> admits a usage that is the target EXACTLY or an ANCESTOR
/// of it. A bit slice is neither: <c>IO.Alarm.%X0</c> is a DESCENDANT. Fixing it in the one place
/// fixes every caller — <c>harness-binding</c> and <c>cross-check</c> read the same graph.</para>
///
/// <para><b>ONLY <c>%X</c>, AND NOT EVERY DESCENDANT.</b> A bit slice is not a child member; it is
/// part of its parent's own storage, which is why admitting it is correct. <c>IO.Run</c> under
/// <c>IO</c> IS a separate member, and admitting that direction would mark a struct driven because
/// one field of it is — the "168 driven members" false green <c>notAbove</c> exists to prevent.</para>
/// </summary>
public class BitSliceWriterAttributionTests : IDisposable
{
    private readonly string _dir;

    public BitSliceWriterAttributionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"bitslice-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        var iface = new[]
        {
            // Written ONLY as bit slices, never as a whole. The subject.
            new DbMember("Alarm", "Word", Retain: false, StartValue: null),
            // The CONTROL: same block, written whole. If this ever regresses the fault is not
            // bit-slices, and separating the two is the only reason this member is here.
            new DbMember("Fault", "Bool", Retain: false, StartValue: null),
            // Never touched at all — proves the fix does not simply mark everything written.
            new DbMember("Spare", "Word", Retain: false, StartValue: null),
            // A struct whose FIELD is written. `Group` itself must stay unused: a field is a
            // separate member, unlike a bit slice, and admitting it would be the false green.
            new DbMember("Group", "Struct", false, null, NestedMembers: new[]
            {
                new DbMember("Field", "Bool", Retain: false, StartValue: null),
            }),
            // Prefix siblings: AlarmExtra is driven as a slice, AlarmExtraUntouched by nothing.
            // A prefix-matching fix that forgot the dot would fold both into `Alarm`.
            new DbMember("AlarmExtra", "Word", Retain: false, StartValue: null),
            new DbMember("AlarmExtraUntouched", "Word", Retain: false, StartValue: null),
        };

        WriteBlock("FB_Alarms", "FB", iface, new[]
        {
            new IrNetwork(1, "alarm word", new[]
            {
                new CoilAssignment("Alarm.%X0", new Expr.TagRef("Trip_A")),
                new CoilAssignment("Alarm.%X1", new Expr.TagRef("Trip_B")),
                new CoilAssignment("Fault", new Expr.TagRef("Trip_A")),
                new CoilAssignment("Group.Field", new Expr.TagRef("Trip_B")),
                new CoilAssignment("AlarmExtra.%X0", new Expr.TagRef("Trip_A")),
            }),
        });
    }

    private void WriteBlock(string name, string kind, DbMember[] statics, IrNetwork[] networks) =>
        File.WriteAllText(Path.Combine(_dir, name + ".ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", kind, name, 1, "LAD", null, networks, StaticMembers: statics)));

    private SignalEntry Entry(string member) =>
        SignalSetRunner.Run(_dir, "FB_Alarms", "any", null, "any")
            .Entries.Single(e => e.Member == member);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    /// <summary>
    /// 🔴 THE HARMFUL DIRECTION. Red before the fix — it reported <c>Unused</c>, which is a
    /// confident statement that nothing drives a member three coils drive.
    /// </summary>
    [Fact]
    public void MemberWrittenOnlyAsBitSlices_IsWritten_NotUnused()
    {
        var alarm = Entry("Alarm");

        Assert.NotEqual(SignalDirection.Unused, alarm.Direction);
        Assert.Equal(SignalDirection.Written, alarm.Direction);
    }

    /// <summary>And the writers are NAMED, not merely counted — a direction with no site behind it
    /// cannot be checked by whoever reads the document.</summary>
    [Fact]
    public void TheBitSliceWritersAreNamed()
    {
        Assert.NotEmpty(Entry("Alarm").Writers);
    }

    /// <summary>THE CONTROL: a member written whole was never broken and must stay correct. Without
    /// this the case above could be satisfied by marking everything written.</summary>
    [Fact]
    public void MemberWrittenWhole_IsStillWritten()
    {
        Assert.Equal(SignalDirection.Written, Entry("Fault").Direction);
    }

    /// <summary>THE OTHER CONTROL, and the one that stops the fix from being too broad: a member
    /// nothing touches is still <c>unused</c>.</summary>
    [Fact]
    public void MemberNothingTouches_IsStillUnused()
    {
        Assert.Equal(SignalDirection.Unused, Entry("Spare").Direction);
    }

    /// <summary>
    /// Normal attribution is untouched: a nested LEAF that is written reports written.
    ///
    /// <para>This replaced a boundary test I first wrote about the parent STRUCT. It failed both
    /// before and after the fix, and the reason was neither direction — <c>signal-set</c> emits a
    /// row per LEAF and none for the struct itself, so <c>Entry("Group")</c> threw. Recorded because
    /// I would otherwise have read a passing struct assertion as evidence about a boundary this
    /// surface cannot see at all.</para>
    /// </summary>
    [Fact]
    public void NestedLeafThatIsWritten_IsStillWritten() =>
        Assert.Equal(SignalDirection.Written, Entry("Group.Field").Direction);

    /// <summary>
    /// 🔴 THE REAL BOUNDARY FOR A PREFIX-MATCHING FIX: a sibling whose name merely BEGINS with the
    /// target's must not be attributed to it.
    ///
    /// <para><c>AlarmExtra.%X0</c> starts with <c>Alarm</c>, and a fix comparing prefixes without
    /// requiring the dot would fold it into <c>Alarm</c> — marking a member written by a coil that
    /// drives something else entirely. That is the false-green direction and it is the mistake this
    /// shape of fix invites, which is why <c>IsBitSliceOf</c> requires <c>target + "."</c> and one
    /// whole <c>%X&lt;n&gt;</c> component rather than a bare prefix.</para>
    /// </summary>
    [Fact]
    public void ASiblingWhoseNameMerelyBeginsWithTheTarget_IsNotFoldedIntoIt()
    {
        // AlarmExtra is driven; Untouched shares its prefix and is driven by nothing. If prefix
        // matching leaked, Untouched would come back written off AlarmExtra's coil.
        Assert.Equal(SignalDirection.Written, Entry("AlarmExtra").Direction);
        Assert.Equal(SignalDirection.Unused, Entry("AlarmExtraUntouched").Direction);
    }
}
