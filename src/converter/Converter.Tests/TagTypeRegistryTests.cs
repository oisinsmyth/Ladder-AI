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

    // An INSTANCE DB's member tree is defined by its FB's interface, so INSTANCEOF has to be
    // followed to type a path through one (2026-08-24).
    //
    // Measured on a real TIA import: `iDB.Silo.StableElapsed >= iDB.Settings.StabilityTimeout` is
    // Time >= Time. Neither operand resolved, InferCompareSrcType's no-tags-no-literals fallback
    // emitted `SrcType Int`, and TIA rejected the block — 12 compile errors on one FC. The
    // IDENTICAL comparison written from inside the FB emitted the right type, because there the
    // local-member namespace answered it. FromFiles indexed DB/TYPE/TAGTABLE files and skipped
    // BLOCK files entirely, so no FB interface was ever in the registry to follow INSTANCEOF into.
    //
    // The instance DB here declares NO members, which is the case that matters: a scaffolded iDB
    // legitimately has an empty member section because TIA populates it on import. Requiring the
    // member tree would make correctness depend on whether anyone had re-exported the project.
    private static string WriteInstanceDbCorpus(string dir, bool includeFb)
    {
        File.WriteAllText(Path.Combine(dir, "UDT_Silo.ir"),
            "TYPE UDT_Silo\n  ROOTID 0\n  MEMBERS\n    Weight : Real\n    StableElapsed : Time\n");
        File.WriteAllText(Path.Combine(dir, "iDB_Silo_W.ir"),
            "DB iDB_Silo_W\n  ROOTID 0\n  NUMBER 11\n  INSTANCEOF FB_Silo\n  MEMBERS\n");
        if (includeFb)
        {
            File.WriteAllText(Path.Combine(dir, "FB_Silo.ir"),
                "BLOCK FB FB_Silo\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\n\n" +
                "INTERFACE\n  STATIC\n    Silo : \"UDT_Silo\"\n");
        }

        return dir;
    }

    [Fact]
    public void Resolve_ThroughAnInstanceDbRoot_FollowsInstanceOfIntoTheFbInterface()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tagtype-idb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteInstanceDbCorpus(dir, includeFb: true);
            var registry = TagTypeRegistry.FromFiles(Directory.EnumerateFiles(dir, "*.ir"));

            // The whole point: a Time member two levels down, through an iDB that declares nothing.
            Assert.Equal("Time", registry.Resolve("iDB_Silo_W.Silo.StableElapsed"));
            Assert.Equal("Real", registry.Resolve("iDB_Silo_W.Silo.Weight"));

            // A member the FB genuinely does not have still returns null — the fallback resolves,
            // it does not invent.
            Assert.Null(registry.Resolve("iDB_Silo_W.Silo.NoSuchMember"));
            Assert.Null(registry.Resolve("iDB_Silo_W.NoSuchStatic"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // The control, and it is the one that pins WHY this needed a fix rather than a config change:
    // with the FB absent from the corpus there is nothing to follow, and the path must resolve to
    // null rather than to a guess. Null is what makes the caller fall back honestly; a guess is what
    // put `Int` on a Time comparison.
    [Fact]
    public void Resolve_ThroughAnInstanceDbRoot_WithoutTheFb_ReturnsNull()
    {
        var dir = Path.Combine(Path.GetTempPath(), "tagtype-idb-nofb-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            WriteInstanceDbCorpus(dir, includeFb: false);
            var registry = TagTypeRegistry.FromFiles(Directory.EnumerateFiles(dir, "*.ir"));

            Assert.Null(registry.Resolve("iDB_Silo_W.Silo.StableElapsed"));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
