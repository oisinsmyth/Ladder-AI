using System.Text.RegularExpressions;
using System.Xml.Linq;
using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE OUTSIDE AUTHORITY, AND ITS ABSENCE IS WHY A DEFECT REACHED A CONTROLLER.</b>
///
/// <para><c>Harness.Map</c> is dependency-free and device-free by design — a good property, and one this
/// component keeps. But it meant that <b>every check on the copy-layer generator asked the generator
/// what it emits and compared it against what the generator was expected to emit</b>. Ninety-odd tests
/// agreed with each other and none of them could disagree, because the only authority in the loop was
/// the same author. <b>TIA was the first party able to disagree, and it did so immediately</b>, on the
/// first live import: <c>Data type Bool is not permitted here.</c></para>
///
/// <para><b>So this file puts a FOREIGN ARTIFACT in the loop.</b> The facts asserted below are read out
/// of <c>simatic-ml/test-project001/</c> — the committed corpus of RAW TIA EXPORTS, which CLAUDE.md
/// names as validation data rather than a regenerable cache. Nobody on this lane wrote them; TIA did.
/// Every expectation about a Bool's tag shape and rung shape is DERIVED from those files at test time,
/// not typed in beside the generator.</para>
///
/// <para><b>What this cannot do, stated so nobody over-reads it:</b> a corpus is a sample. It shows what
/// TIA DOES produce, never the whole of what TIA WILL accept, and it says nothing about a construct no
/// block in the corpus happens to use. It is strictly stronger than a self-comparison and strictly
/// weaker than an import. <b>The compile gate is still the gate</b> (hard rule 4).</para>
/// </summary>
public class CopyLayerAgainstRealTiaExportTests
{
    private static readonly CopyLayerNaming Naming = new(BlockNumber: 900);
    private static readonly BuildStamp Stamp = new(0xA93F2C71);

    /// <summary>
    /// The repo root, found by walking up for the export corpus itself.
    ///
    /// <para><b>A missing corpus FAILS rather than skipping.</b> A check that quietly disappears when its
    /// input is absent is a check that stops running, and this whole file exists because a check that was
    /// not there let a defect through. Empty is not clean.</para>
    /// </summary>
    private static string ExportDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "simatic-ml", "test-project001");
                if (File.Exists(Path.Combine(candidate, "DefaultTagTable.xml")))
                    return candidate;

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "the committed TIA export corpus was not found by walking up from " + AppContext.BaseDirectory
                + " for simatic-ml/test-project001/DefaultTagTable.xml. *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** "
                + "These are the only checks on the copy layer that a party other than its author can fail, and a copy "
                + "layer verified only against its own expectations is exactly what put `Data type Bool is not permitted "
                + "here` on a controller.");
        }
    }

    private static XDocument Export(string file) => XDocument.Load(Path.Combine(ExportDirectory, file));

    /// <summary>Every (data type, logical address) pair TIA itself wrote into the reference tag table.</summary>
    private static IReadOnlyList<(string Type, string Address)> RealTagRows()
    {
        var doc = Export("DefaultTagTable.xml");

        return doc.Descendants()
            .Where(e => e.Name.LocalName == "AttributeList" && e.Elements().Any(c => c.Name.LocalName == "LogicalAddress"))
            .Select(e => (
                Type: e.Elements().FirstOrDefault(c => c.Name.LocalName == "DataTypeName")?.Value ?? string.Empty,
                Address: e.Elements().FirstOrDefault(c => c.Name.LocalName == "LogicalAddress")?.Value ?? string.Empty))
            .Where(r => r.Type.Length > 0 && r.Address.Length > 0)
            .ToArray();
    }

    // ---------------------------------------------------------------------------------------------
    // Fact 1 — how TIA itself addresses a Bool, versus a word
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TIA_ADDRESSES_A_BOOL_AS_BYTE_DOT_BIT_AND_A_WORD_AS_W_BYTE()
    {
        var rows = RealTagRows();

        // The corpus has to actually contain both, or the comparison below proves nothing.
        var bools = rows.Where(r => r.Type == "Bool").ToArray();
        var words = rows.Where(r => r.Type is "Word" or "Int").ToArray();

        Assert.NotEmpty(bools);
        Assert.NotEmpty(words);

        Assert.All(bools, r => Assert.Matches(@"^%[A-Z]+\d+\.\d+$", r.Address));
        Assert.All(words, r => Assert.Matches(@"^%[A-Z]+W\d+$", r.Address));
    }

    [Fact]
    public void And_the_generator_addresses_ITS_bools_and_words_the_same_way()
    {
        // The shapes are not restated here — they are taken from the export at run time, so a change in
        // what TIA does turns this red rather than leaving a stale literal agreeing with itself.
        var rows = RealTagRows();
        var boolShape = new Regex(@"^%[A-Z]+\d+\.\d+$");
        var wordShape = new Regex(@"^%[A-Z]+W\d+$");

        Assert.All(rows.Where(r => r.Type == "Bool"), r => Assert.Matches(boolShape, r.Address));

        var plan = Mixed().Require();

        foreach (var tag in plan.Tags.Where(t => t.DataType == "Bool"))
            Assert.Matches(boolShape, tag.Address);

        foreach (var tag in plan.Tags.Where(t => t.DataType == "Int"))
            Assert.Matches(wordShape, tag.Address);

        // And both kinds are actually present, so neither loop passed by being empty.
        Assert.NotEmpty(plan.Tags.Where(t => t.DataType == "Bool"));
        Assert.NotEmpty(plan.Tags.Where(t => t.DataType == "Int"));
    }

    // ---------------------------------------------------------------------------------------------
    // Fact 2 — how TIA itself copies a Bool
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_REAL_TIA_BLOCK_WHOSE_WHOLE_JOB_IS_COPYING_BOOLS_USES_COILS_AND_NOT_ONE_MOVE()
    {
        // FC_Outputs maps Bool members onto Bool output tags and does nothing else. It is the closest
        // thing in the corpus to what the copy layer's results-out network does, and it was produced by
        // TIA rather than by anybody here.
        var parts = Export("FC_Outputs.xml").Descendants()
            .Where(e => e.Name.LocalName == "Part")
            .Select(e => e.Attribute("Name")?.Value ?? string.Empty)
            .ToArray();

        Assert.NotEmpty(parts);

        // Every WRITE in the block is a Coil. (Contacts are also parts — they are how the guard is
        // expressed — so the claim is about what writes, not about the whole part list.)
        Assert.NotEmpty(parts.Where(p => p == "Coil"));

        // *** AND THE BLOCK CONTAINS NO Move AT ALL. *** This is the direction the defect went:
        // `MOVE(IN := <Bool>)` is what the generator emitted, and it is what TIA refused. A block whose
        // entire job is copying Bools, written by TIA, reaches for Move exactly zero times.
        Assert.DoesNotContain("Move", parts);

        // Corroborated from the other side: blocks in this same corpus DO use Move — so the absence
        // above is a property of copying Bools, not a property of this corpus not using Move at all.
        var moversElsewhere = Export("FC_ControlMain.xml").Descendants()
            .Where(e => e.Name.LocalName == "Part")
            .Select(e => e.Attribute("Name")?.Value ?? string.Empty)
            .Count(p => p == "Move");

        Assert.True(moversElsewhere > 0,
            "FC_ControlMain was expected to contain Move parts. Without that, 'FC_Outputs contains no Move' "
            + "would be evidence about the corpus rather than about how a Bool is copied.");
    }

    [Fact]
    public void And_the_generator_copies_ITS_bools_with_COIL_and_its_ints_with_MOVE()
    {
        var ir = Mixed().Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        var resultLines = NetworkBody(ir, "Results out - slot S0 - Bool");
        Assert.NotEmpty(resultLines);
        Assert.All(resultLines, l => Assert.StartsWith("COIL ", l, StringComparison.Ordinal));

        var intLines = NetworkBody(ir, "Results out - slot S0 - Int");
        Assert.NotEmpty(intLines);
        Assert.All(intLines, l => Assert.StartsWith("MOVE(", l, StringComparison.Ordinal));

        // *** THE REGRESSION, NAMED. *** No Bool source may appear inside a MOVE anywhere in the block.
        foreach (var move in ir.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("MOVE(", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("DB_Unit.Alarm", move, StringComparison.Ordinal);
            Assert.DoesNotContain("DB_Unit.StopReq", move, StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Fact 3 — the exact case that was measured on the rig
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void THE_HOPPER_SHAPE_THAT_TIA_REJECTED_NOW_RENDERS_AS_COILS()
    {
        // The rejected artifact mirrored two Bool outputs of FB_HopperBlockageMonitor with plain MOVEs.
        // Both conformance vector sets observe only Bools, so this was not a corner case — it was every
        // signal either set asserts on.
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 1000),
            new[] { new SlotRequest("HBA", 0, 2) })).Require();

        var binding = new SlotBinding("HBA", Array.Empty<MirroredSignal>(), null, MirroredSignal.Bools(
            "iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm",
            "iDB_HopperBlockageMonitor.IO.HopperBlockStopReq"));

        var ir = CopyLayerGenerator.Generate(map, binding, Naming, Stamp)
            .Objects.Single(o => o.Kind == HarnessObjectKind.Block).Ir;

        Assert.Contains("COIL HX_HBA_R000 := iDB_HopperBlockageMonitor.IO.HopperBlockedAlarm", ir, StringComparison.Ordinal);
        Assert.Contains("COIL HX_HBA_R001 := iDB_HopperBlockageMonitor.IO.HopperBlockStopReq", ir, StringComparison.Ordinal);

        Assert.DoesNotContain("MOVE(EN := TRUE, IN := iDB_HopperBlockageMonitor", ir, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------

    private static CopyLayerResult Mixed()
    {
        var map = MapAllocator.Allocate(new WaveSetRequest(
            MirrorGeometry.ForCpu1214C(retentiveBytes: 256, baseByte: 4000),
            new[] { new SlotRequest("S0", 2, 3) })).Require();

        var binding = new SlotBinding(
            "S0",
            new[] { MirroredSignal.Int("DB_Unit.Setpoint"), MirroredSignal.Bool("DB_Unit.Enable") },
            "DB_Unit.StartCmd",
            new[] { MirroredSignal.Bool("DB_Unit.Alarm"), MirroredSignal.Int("DB_Unit.Actual"), MirroredSignal.Bool("DB_Unit.StopReq") });

        return CopyLayerGenerator.Generate(map, binding, Naming, Stamp);
    }

    private static IReadOnlyList<string> NetworkBody(string ir, string title)
    {
        var blocks = ir.Split("NETWORK ").Skip(1).Where(b => b.Contains($"\"{title}\"", StringComparison.Ordinal)).ToArray();

        return blocks
            .SelectMany(b => b.Split('\n').Skip(1))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
    }
}
