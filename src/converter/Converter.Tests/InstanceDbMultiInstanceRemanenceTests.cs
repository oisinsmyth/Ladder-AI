using System.Xml.Linq;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-102 (2026-09-03). <b>An instance DB of a block that NESTS other FB instances could not be
/// imported at all</b> — TIA answers "Cannot create the 'SW.Blocks.InstanceDB' object …" / "The
/// attribute 'Remanence' cannot be set" and refuses the whole object. Measured over five blocks: the
/// three FBs declaring ZERO FB-typed statics produced instance DBs that imported cleanly; the two
/// declaring TWO and FIVE were both refused. It blocked two conformance slots and it is not exotic —
/// every equipment block above the leaf level has this shape.
///
/// <para><b>The discriminator is the corpus, and it has to be.</b> <c>IO : "UDT_OwnerIO" RETAIN</c> and
/// <c>Inner : "FB_Inner"</c> are both quoted names; the datatype string cannot separate them, and an
/// <c>FB_</c> prefix test is not admissible (anything can be renamed into a prefix). So these tests
/// build a fixture corpus and check that the SAME instance DB is written two different ways depending
/// on what the corpus says the name is — which is the whole claim.</para>
///
/// <para><b>The must-not-regress direction is tested as hard as the fix.</b> A writer that simply
/// dropped <c>Remanence</c> from every instance DB member would pass every "no Remanence" assertion
/// here; <see cref="UdtTypedRetainStatic_StillCarriesRemanence"/>,
/// <see cref="ElementaryRetainMember_IsUnchanged"/> and the two whole-corpus sweeps are what refuse it.
/// The sweeps carry their own denominators so they cannot pass by examining nothing.</para>
/// </summary>
public class InstanceDbMultiInstanceRemanenceTests : IDisposable
{
    private readonly string _dir;

    public InstanceDbMultiInstanceRemanenceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"idb-remanence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private void Write(string name, string text) => File.WriteAllText(Path.Combine(_dir, name), text);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string CorpusDir() => Path.Combine(RepoRoot(), "ir", "test-project001");

    private static InstanceDbTypeResolution ResolutionOver(string? projectDir)
    {
        var tagTypes = Program.BuildTagTypeRegistry(Array.Empty<string>(), projectDir);
        return Program.BuildInstanceDbTypeResolution(Array.Empty<string>(), projectDir, tagTypes);
    }

    // ------------------------------------------------------------------ the fixture corpus
    //
    // GREEN THROUGHOUT: invented vocabulary, written here rather than borrowed, because
    // ir/test-project001 contains no multi-instance FB at all — the shape this defect is about is the
    // one the committed corpus happens not to have.

    private void WriteCorpus()
    {
        // The nested block. Only its `BLOCK FB <Name>` header line decides the question — that is
        // deliberately where MemberExpansion.BlockNamesFromHeaders reads block names from, because
        // TagTypeRegistry's FB index silently drops every sidecar-carrying (re-exported) block, which
        // is exactly the file a round-tripped corpus is made of.
        Write("FB_Inner.ir",
            "BLOCK FB FB_Inner\n" +
            "ROOTID 0\n" +
            "NUMBER 61\n" +
            "LANGUAGE LAD\n" +
            "COMMENT \"The nested unit.\"\n" +
            "\n" +
            "INTERFACE\n" +
            "  INPUT\n" +
            "  OUTPUT\n" +
            "  STATIC\n" +
            "    Held : Bool\n" +
            "\n" +
            "NETWORK 1 \"Hold\"\n" +
            "  COIL Held := Held\n");

        // The owning block, and the PLC data type its interface member is typed as. The UDT is what
        // makes the test sharp: it is quoted exactly like the FB above and must keep its Remanence.
        Write("FB_Owner.ir",
            "BLOCK FB FB_Owner\n" +
            "ROOTID 0\n" +
            "NUMBER 62\n" +
            "LANGUAGE LAD\n" +
            "COMMENT \"The owning unit.\"\n" +
            "\n" +
            "INTERFACE\n" +
            "  INPUT\n" +
            "  OUTPUT\n" +
            "  STATIC\n" +
            "    Latched : Bool\n" +
            "\n" +
            "NETWORK 1 \"Latch\"\n" +
            "  COIL Latched := Latched\n");

        Write("UDT_OwnerIO.ir",
            "TYPE UDT_OwnerIO\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    Command : Bool\n" +
            "    Response : Bool\n");
    }

    // The instance DB under test. Four top-level statics, one per shape the ruling has to separate:
    // an FB instance, a UDT-typed RETAIN member with nothing inlined (so it must resolve through the
    // type registry), an elementary RETAIN member, and an IEC timer instance with its members inlined.
    private const string OwnerInstanceDb =
        "DB iDB_Owner\n" +
        "  ROOTID 0\n" +
        "  NUMBER 900\n" +
        "  INSTANCEOF FB_Owner\n" +
        "  INPUT\n" +
        "  OUTPUT\n" +
        "  MEMBERS\n" +
        "    Inner : \"FB_Inner\"\n" +
        "    IO : \"UDT_OwnerIO\" RETAIN\n" +
        "    Latched : Bool RETAIN\n" +
        "    Dwell : TON_TIME VERSION 1.0 SETPOINT\n" +
        "      PT : Time\n" +
        "      ET : Time\n" +
        "      IN : Bool\n" +
        "      Q : Bool\n";

    private static XDocument WriteDb(string irText, InstanceDbTypeResolution? resolution) =>
        DbSourceWriter.Write(DbIrParser.ParseDb(irText), resolution);

    private static XElement StaticMember(XDocument doc, string name) =>
        doc.Descendants()
            .Single(e => e.Name.LocalName == "Section" && (string?)e.Attribute("Name") == "Static")
            .Elements()
            .Single(e => e.Name.LocalName == "Member" && (string?)e.Attribute("Name") == name);

    // ------------------------------------------------------------------ 1. the defect

    [Fact]
    public void FbTypedStatic_IsWrittenWithNoRemanenceAtAll()
    {
        WriteCorpus();

        var member = StaticMember(WriteDb(OwnerInstanceDb, ResolutionOver(_dir)), "Inner");

        // Not "NonRetain" — ABSENT. TIA refuses the attribute's presence, not one of its values.
        Assert.Null(member.Attribute("Remanence"));

        // And the rest of the multi-instance shape survives: FI-59's remedy was once wider than its
        // evidence and dropped Version and the whole AttributeList too.
        Assert.Equal("\"FB_Inner\"", (string?)member.Attribute("Datatype"));
        Assert.Contains(member.Elements(), e => e.Name.LocalName == "AttributeList");
    }

    [Fact]
    public void TheSameMemberKeepsRemanenceWhenTheCorpusCallsItATypeInsteadOfABlock()
    {
        // The claim in one test: nothing about the member, the name or the quoting changed — only what
        // the corpus says "FB_Inner" IS. Declared as a PLC data type, it is a UDT and keeps Remanence.
        Write("FB_Inner.ir",
            "TYPE FB_Inner\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    Held : Bool\n");
        Write("UDT_OwnerIO.ir",
            "TYPE UDT_OwnerIO\n" +
            "  ROOTID 0\n" +
            "  MEMBERS\n" +
            "    Command : Bool\n");

        var member = StaticMember(WriteDb(OwnerInstanceDb, ResolutionOver(_dir)), "Inner");

        Assert.Equal("NonRetain", (string?)member.Attribute("Remanence"));
    }

    // ------------------------------------------------------------------ 2/3. what must not move

    [Fact]
    public void UdtTypedRetainStatic_StillCarriesRemanence()
    {
        WriteCorpus();

        var member = StaticMember(WriteDb(OwnerInstanceDb, ResolutionOver(_dir)), "IO");

        Assert.Equal("Retain", (string?)member.Attribute("Remanence"));
    }

    [Fact]
    public void ElementaryRetainMember_IsUnchanged()
    {
        WriteCorpus();

        var doc = WriteDb(OwnerInstanceDb, ResolutionOver(_dir));

        Assert.Equal("Retain", (string?)StaticMember(doc, "Latched").Attribute("Remanence"));

        // An IEC timer instance is NOT a multi-instance for this purpose and must keep the attribute —
        // every real TIA export in the committed corpus states it, and TONR_TIME's is genuinely Retain.
        Assert.Equal("NonRetain", (string?)StaticMember(doc, "Dwell").Attribute("Remanence"));
    }

    [Fact]
    public void AFixedShapeInstructionInstanceLosesRemanence_AndAnIecTimerKeepsIt()
    {
        // The two families are adjacent and must not be merged. `MbServer : MB_SERVER VERSION 5.3` is
        // an instance in the sense that matters — BlockSourceWriter already omits Remanence for it in
        // the OWNING FB, from this same registry, so the FB and its instance DB cannot disagree about
        // one member. `Dwell : TON_TIME VERSION 1.0` is instruction state that TIA states Remanence on
        // in every real export in the committed corpus.
        //
        // Neither needs a corpus, so this is asserted with NO --project at all: whether a name is a
        // fixed-shape instruction is a fact about the instruction registry, not about the project.
        const string db =
            "DB iDB_Comms\n" +
            "  ROOTID 0\n" +
            "  NUMBER 902\n" +
            "  INSTANCEOF FB_Comms\n" +
            "  INPUT\n" +
            "  OUTPUT\n" +
            "  MEMBERS\n" +
            "    MbServer : MB_SERVER VERSION 5.3\n" +
            "    Dwell : TON_TIME VERSION 1.0\n";

        var doc = WriteDb(db, ResolutionOver(projectDir: null));

        Assert.Null(StaticMember(doc, "MbServer").Attribute("Remanence"));
        Assert.Equal("5.3", (string?)StaticMember(doc, "MbServer").Attribute("Version"));
        Assert.Equal("NonRetain", (string?)StaticMember(doc, "Dwell").Attribute("Remanence"));
    }

    // ------------------------------------------------------------------ 4/5. the refusals

    [Fact]
    public void QuotedTypeNamingNeitherABlockNorAType_IsRefusedRatherThanGuessed()
    {
        WriteCorpus();
        File.Delete(Path.Combine(_dir, "FB_Inner.ir"));

        var ex = Assert.Throws<UnsupportedConstructException>(
            () => WriteDb(OwnerInstanceDb, ResolutionOver(_dir)));

        Assert.Contains("iDB_Owner", ex.Message);
        Assert.Contains("Inner", ex.Message);
        Assert.Contains("FB_Inner", ex.Message);
        Assert.Contains("--project", ex.Message);
    }

    [Fact]
    public void WithNoProject_TheConversionIsRefusedAndTheMessageSaysSo()
    {
        // THE NO---PROJECT DECISION, PINNED. With no corpus there is no honest answer: emitting
        // Remanence is FI-102 itself and only import would catch it; omitting it always would strip a
        // legitimate attribute off every UDT-typed member and nothing at all would catch that. So the
        // conversion refuses, and the refusal names the missing flag rather than the missing type.
        var ex = Assert.Throws<UnsupportedConstructException>(
            () => WriteDb(OwnerInstanceDb, ResolutionOver(projectDir: null)));

        Assert.Contains("NO --project WAS GIVEN", ex.Message);
        Assert.Contains("--project <ir-dir>", ex.Message);
    }

    [Fact]
    public void AGlobalDbIsNeverAskedTheQuestion()
    {
        // A global DB cannot hold an FB instance, so the ruling does not apply to one — and must not,
        // or an unresolvable member type would start refusing conversions TIA accepts today.
        const string globalDb =
            "DB DB_Loose\n" +
            "  ROOTID 0\n" +
            "  NUMBER 901\n" +
            "  MEMBERS\n" +
            "    Thing : \"UDT_NotInAnyCorpus\" RETAIN\n";

        var doc = WriteDb(globalDb, ResolutionOver(projectDir: null));

        Assert.Equal("Retain", (string?)StaticMember(doc, "Thing").Attribute("Remanence"));
    }

    // ------------------------------------------------------------------ 6/7. the no-op proofs

    [Fact]
    public void EveryBlockInTheCommittedCorpus_ConvertsByteIdenticallyWithAndWithoutTheResolution()
    {
        // BlockSourceWriter's own multi-instance guard is derived from the block's CALL and fixed-shape
        // statements and was NOT touched. This proves it: supplying the new resolution changes nothing
        // a block emits, over every block in the corpus.
        var corpus = CorpusDir();
        var callees = Program.BuildCalleeRegistry(Array.Empty<string>(), corpus);
        var tagTypes = Program.BuildTagTypeRegistry(Array.Empty<string>(), corpus);
        var resolution = Program.BuildInstanceDbTypeResolution(Array.Empty<string>(), corpus, tagTypes);

        var blocks = 0;
        foreach (var path in Directory.EnumerateFiles(corpus, "*.ir").OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(path);
            if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
            {
                continue;
            }

            blocks++;
            Assert.Equal(
                Outcome(() => Program.BuildXmlFromIrText(text, synthesize: false, callees, tagTypes, null)),
                Outcome(() => Program.BuildXmlFromIrText(text, synthesize: false, callees, tagTypes, resolution)));
        }

        // The denominator. Without it a broken enumeration passes by comparing nothing.
        Assert.True(blocks >= 15, $"expected the committed corpus to hold 15+ blocks, walked {blocks}");
        Assert.True(resolution.BlockNameCount >= 15,
            $"expected 15+ block names resolvable, got {resolution.BlockNameCount}");
    }

    [Fact]
    public void EveryDbInTheCommittedCorpus_ConvertsByteIdenticallyWithAndWithoutTheResolution()
    {
        var corpus = CorpusDir();
        var resolution = ResolutionOver(corpus);

        var instanceDbs = 0;
        var globalDbs = 0;
        var namedTypeMembers = 0;

        foreach (var path in Directory.EnumerateFiles(corpus, "*.ir").OrderBy(p => p, StringComparer.Ordinal))
        {
            var text = File.ReadAllText(path);
            if (!text.StartsWith("DB ", StringComparison.Ordinal))
            {
                continue;
            }

            var db = DbIrParser.ParseDb(text);
            if (db.InstanceOfName is null)
            {
                globalDbs++;
            }
            else
            {
                instanceDbs++;
                namedTypeMembers += db.Members.Count(m => m.Datatype.TrimStart().StartsWith("\"", StringComparison.Ordinal));
            }

            Assert.Equal(
                Outcome(() => DbSourceWriter.Write(db, null)),
                Outcome(() => DbSourceWriter.Write(db, resolution)));
        }

        // Three denominators, because three different things could make this pass vacuously: no DBs
        // walked, no INSTANCE DBs among them, or no member whose datatype is a quoted name at all —
        // and it is only the quoted-name members the ruling can possibly change.
        Assert.True(instanceDbs >= 13, $"expected 13+ instance DBs in the corpus, walked {instanceDbs}");
        Assert.True(globalDbs >= 7, $"expected 7+ global DBs in the corpus, walked {globalDbs}");
        Assert.True(namedTypeMembers >= 5,
            $"expected 5+ quoted-datatype members across those instance DBs, saw {namedTypeMembers}");
    }

    // Compares the OUTCOME, not just the output: a change that made one side throw and the other
    // succeed would otherwise surface as a test error rather than as the inequality it is.
    private static string Outcome(Func<XDocument> build)
    {
        try
        {
            return build().ToString();
        }
        catch (Exception ex)
        {
            return $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
