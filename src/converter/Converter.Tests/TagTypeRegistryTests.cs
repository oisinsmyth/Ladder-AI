using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// TagTypeRegistry (Gap B/E keystone, 2026-07-18) — resolves a dotted tag/member path to its
/// datatype from the batch's DB/UDT/tag-table sources, so synthesis can emit the type attributes the
/// readable IR omits (CONVERT Src/DestType, WAND/ABS/SWAP SrcType, comparison SrcType).
/// </summary>
public class TagTypeRegistryTests
{
    private static DbMember Scalar(string name, string type) => new(name, type, Retain: false, StartValue: null);

    [Fact]
    public void Resolve_TagTableTag_ReturnsItsType()
    {
        var registry = TagTypeRegistry.FromSources(
            dbs: Array.Empty<DbSource>(),
            udts: Array.Empty<PlcTypeSource>(),
            tags: new[] { new PlcTagSource("1", "StatusWord", "Word", "%MW10", true, true, true, null) });

        Assert.Equal("Word", registry.Resolve("StatusWord"));
    }

    [Fact]
    public void Resolve_DbScalarMember_ReturnsMemberType()
    {
        var db = new DbSource("1", "SignalData", 5, null, null, new[] { Scalar("SpeedRawA", "Real") });
        var registry = TagTypeRegistry.FromSources(new[] { db }, Array.Empty<PlcTypeSource>(), Array.Empty<PlcTagSource>());

        Assert.Equal("Real", registry.Resolve("SignalData.SpeedRawA"));
    }

    [Fact]
    public void Resolve_StructuredMemberViaNestedMembers_Descends()
    {
        // A TON_TIME instance member inlines its own sub-members (Q, ET, ...) one level deep.
        var timer = new DbMember("SampleTimer0", "TON_TIME", Retain: false, StartValue: null,
            NestedMembers: new[] { Scalar("Q", "Bool"), Scalar("ET", "Time") });
        var db = new DbSource("1", "DB_Timers", 6, null, null, new[] { timer });
        var registry = TagTypeRegistry.FromSources(new[] { db }, Array.Empty<PlcTypeSource>(), Array.Empty<PlcTagSource>());

        Assert.Equal("Bool", registry.Resolve("DB_Timers.SampleTimer0.Q"));
        Assert.Equal("Time", registry.Resolve("DB_Timers.SampleTimer0.ET"));
    }

    [Fact]
    public void Resolve_MemberTypedAsUdt_DescendsIntoUdt()
    {
        var udt = new PlcTypeSource("1", "MotorImage", null, new[] { Scalar("Ready", "Bool"), Scalar("Speed", "Real") });
        var db = new DbSource("1", "Plant", 7, null, null, new[] { new DbMember("M1", "\"MotorImage\"", false, null) });
        var registry = TagTypeRegistry.FromSources(new[] { db }, new[] { udt }, Array.Empty<PlcTagSource>());

        Assert.Equal("Real", registry.Resolve("Plant.M1.Speed"));
    }

    [Fact]
    public void Resolve_SubscriptedArrayMember_ReturnsElementType()
    {
        var db = new DbSource("1", "Edges", 8, null, null, new[] { Scalar("RisingEdgeFlags", "Array[0..14] of Bool") });
        var registry = TagTypeRegistry.FromSources(new[] { db }, Array.Empty<PlcTypeSource>(), Array.Empty<PlcTagSource>());

        Assert.Equal("Bool", registry.Resolve("Edges.RisingEdgeFlags[3]"));
        // The whole array, no subscript, keeps the array type.
        Assert.Equal("Array[0..14] of Bool", registry.Resolve("Edges.RisingEdgeFlags"));
    }

    [Fact]
    public void Resolve_SubscriptedBareArrayTag_ReturnsElementType()
    {
        var registry = TagTypeRegistry.FromSources(
            Array.Empty<DbSource>(), Array.Empty<PlcTypeSource>(),
            new[] { new PlcTagSource("1", "SpeedArray", "Array[1..4] of Int", "%MW20", true, true, true, null) });

        Assert.Equal("Int", registry.Resolve("SpeedArray[2]"));
    }

    [Fact]
    public void Resolve_UnknownRootOrMember_ReturnsNull()
    {
        var db = new DbSource("1", "SignalData", 5, null, null, new[] { Scalar("SpeedRawA", "Real") });
        var registry = TagTypeRegistry.FromSources(new[] { db }, Array.Empty<PlcTypeSource>(), Array.Empty<PlcTagSource>());

        Assert.Null(registry.Resolve("NoSuchDb.Foo"));
        Assert.Null(registry.Resolve("SignalData.NoSuchMember"));
        Assert.Null(registry.Resolve("LooseTag"));
    }

    [Fact]
    public void FromFiles_ParsesDbUdtAndTagTableText()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tagtype-registry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "SignalData.ir"),
                "DB SignalData\n  ROOTID 0\n  NUMBER 5\n  MEMBERS\n    SpeedRawA : Real\n    Mask : Word\n");
            File.WriteAllText(Path.Combine(dir, "IoTags.ir"),
                "TAGTABLE IoTags\n  ROOTID 0\n  TAGS\n    StatusWord 1 : Word @ %MW10\n");

            var registry = TagTypeRegistry.FromFiles(Directory.EnumerateFiles(dir, "*.ir"));

            Assert.Equal("Real", registry.Resolve("SignalData.SpeedRawA"));
            Assert.Equal("Word", registry.Resolve("SignalData.Mask"));
            Assert.Equal("Word", registry.Resolve("StatusWord"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
