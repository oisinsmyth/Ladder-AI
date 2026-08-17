using System;
using System.IO;
using System.Linq;
using Ladder.Wave;
using Ladder.Wave.Cli;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// The corpus reader — DB-1's source for the KIND every change class is keyed on.
    ///
    /// <para>These are written against a SYNTHETIC corpus on purpose, so that each property is pinned by
    /// a file built to exercise it. The whole-corpus sweep against the real 43-object
    /// <c>ir/test-project001</c> is in <see cref="RouteCommandTests"/>, and the two answer different
    /// questions: this one says the reader does what it claims, that one says the claims survive contact
    /// with content nobody wrote for them.</para>
    /// </summary>
    public sealed class IrCorpusTests
    {
        private static string Write(TempDirectory dir, string file, string content)
        {
            var path = Path.Combine(dir.Path, file);
            File.WriteAllText(path, content);
            return path;
        }

        [Fact]
        public void EveryKindIsReadFromTheFilesOwnDeclaration()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "a.ir", "BLOCK OB Main\nNUMBER 1\n");
                Write(dir, "b.ir", "BLOCK FB FB_Thing\nNUMBER 2\n");
                Write(dir, "c.ir", "BLOCK FC FC_Thing\nNUMBER 3\n");
                Write(dir, "d.ir", "DB DB_Thing\nMEMBERS\n  X : Bool\n");
                Write(dir, "e.ir", "TYPE UDT_Thing\nMEMBERS\n  X : Bool\n");
                Write(dir, "f.ir", "TAGTABLE Default tag table\nTAGS\n  T 1 : Bool @ %I0.0\n");

                var corpus = IrCorpus.Read(dir.Path);

                Assert.Equal(6, corpus.FilesSeen);
                Assert.Equal(6, corpus.Objects.Count);
                Assert.Empty(corpus.Unreadable);

                Assert.Equal(ObjectKind.OrganizationBlock, Kind(corpus, "Main"));
                Assert.Equal(ObjectKind.FunctionBlock, Kind(corpus, "FB_Thing"));
                Assert.Equal(ObjectKind.Function, Kind(corpus, "FC_Thing"));
                Assert.Equal(ObjectKind.GlobalDataBlock, Kind(corpus, "DB_Thing"));
                Assert.Equal(ObjectKind.DataType, Kind(corpus, "UDT_Thing"));

                // TIA's own name for the default tag table contains SPACES and the filename does not.
                // Pairing on the filename is the defect that made drift-check report one object twice in
                // two contradictory directions; the identity here comes from the content.
                Assert.Equal(ObjectKind.TagTable, Kind(corpus, "Default tag table"));
                Assert.False(corpus.TryLookup("f", out _));
            }
        }

        [Fact]
        public void AnInstanceDbIsDistinguishedFromAGlobalOneByItsOwnInstanceofLine()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "g.ir", "DB DB_Global\nMEMBERS\n  X : Bool\n");
                Write(dir, "i.ir", "DB iDB_Thing\nINSTANCEOF FB_Thing\nMEMBERS\n  X : Bool\n");

                var corpus = IrCorpus.Read(dir.Path);

                Assert.Equal(ObjectKind.GlobalDataBlock, Kind(corpus, "DB_Global"));
                Assert.Equal(ObjectKind.InstanceDataBlock, Kind(corpus, "iDB_Thing"));
                Assert.Contains("FB_Thing", corpus.DependenciesOf("iDB_Thing"));
            }
        }

        [Fact]
        public void KindIsNotInferredFromTheName()
        {
            // The corpus this tool actually runs against contains MotorFwdRevIOSet — a UDT with no `UDT_`
            // prefix — so a prefix heuristic would misclassify a real object today, not hypothetically.
            using (var dir = new TempDirectory())
            {
                Write(dir, "x.ir", "TYPE MotorFwdRevIOSet\nMEMBERS\n  X : Bool\n");
                Write(dir, "y.ir", "BLOCK FC UDT_NotAType\n");

                var corpus = IrCorpus.Read(dir.Path);

                Assert.Equal(ObjectKind.DataType, Kind(corpus, "MotorFwdRevIOSet"));
                Assert.Equal(ObjectKind.Function, Kind(corpus, "UDT_NotAType"));
            }
        }

        [Fact]
        public void TwoFilesClaimingOneNameAreAmbiguous_NotFirstWins()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "a.ir", "BLOCK FB FB_Thing\n");
                Write(dir, "b.ir", "DB FB_Thing\nMEMBERS\n  X : Bool\n");

                var corpus = IrCorpus.Read(dir.Path);

                Assert.True(corpus.IsAmbiguous("FB_Thing"));
                Assert.False(corpus.TryLookup("FB_Thing", out _));
                Assert.Contains("FB_Thing", corpus.AmbiguousNames);
            }
        }

        [Fact]
        public void AFileWithNoDeclarationIsNamedUnreadable_NotSilentlyDropped()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "good.ir", "BLOCK FB FB_Thing\n");
                Write(dir, "junk.ir", "this is not IR\n");

                var corpus = IrCorpus.Read(dir.Path);

                Assert.Equal(2, corpus.FilesSeen);
                Assert.Single(corpus.Objects);
                Assert.Single(corpus.Unreadable);
                Assert.Contains("junk.ir", corpus.Unreadable[0]);
            }
        }

        [Fact]
        public void AnEmptyDirectoryReportsZeroFilesSeen_WhichIsTheDenominator()
        {
            using (var dir = new TempDirectory())
            {
                var corpus = IrCorpus.Read(dir.Path);
                Assert.Equal(0, corpus.FilesSeen);
                Assert.Empty(corpus.Objects);
            }
        }

        [Fact]
        public void ATypeNameInsideACommentIsNotReadAsAMemberDeclaration()
        {
            // The anchored regex earns its keep here. An unanchored `:\s*"` would take the quoted string
            // out of the comment and invent a blast-radius edge to an object that does not exist.
            using (var dir = new TempDirectory())
            {
                Write(dir, "d.ir",
                    "DB DB_Thing\n" +
                    "  COMMENT \"see: \\\"UDT_Imaginary\\\" for the shape\"\n" +
                    "  MEMBERS\n" +
                    "    Real : \"UDT_Actual\"\n");

                var corpus = IrCorpus.Read(dir.Path);
                CorpusObject? found;
                Assert.True(corpus.TryLookup("DB_Thing", out found));

                Assert.Equal(new[] { "UDT_Actual" }, found!.DeclaredTypes.ToArray());
            }
        }

        [Fact]
        public void CallsAreReadOnlyFromNetworks()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "m.ir",
                    "BLOCK OB Main\n" +
                    "INTERFACE\n" +
                    "  INPUT\n" +
                    "NETWORK 1 \"x\"\n" +
                    "  CALL FC_Inputs(EN := TRUE)\n" +
                    "  CALL FB_Thing(iDB_Thing, EN := TRUE)\n");

                var corpus = IrCorpus.Read(dir.Path);
                CorpusObject? found;
                Assert.True(corpus.TryLookup("Main", out found));

                Assert.Equal(new[] { "FB_Thing", "FC_Inputs" }, found!.Calls.OrderBy(c => c).ToArray());
            }
        }

        [Fact]
        public void TheTwoBlastRadiusRoutesAreComputedSeparately()
        {
            using (var dir = new TempDirectory())
            {
                Write(dir, "t.ir", "TYPE UDT_Shared\nMEMBERS\n  X : Bool\n");
                Write(dir, "fb.ir", "BLOCK FB FB_Owner\nINTERFACE\n  STATIC\n    IO : \"UDT_Shared\"\n");
                Write(dir, "idb.ir", "DB iDB_Owner\nINSTANCEOF FB_Owner\nMEMBERS\n  IO : \"UDT_Shared\"\n");
                Write(dir, "gdb.ir", "DB DB_Plain\nMEMBERS\n  IO : \"UDT_Shared\"\n");

                var corpus = IrCorpus.Read(dir.Path);

                // Direct sees both data blocks; the FB route sees only the instance DB. They DISAGREE,
                // and that disagreement must be visible rather than averaged away — the union is the
                // radius, and a route that finds LESS is the one that would under-invalidate.
                Assert.Equal(new[] { "DB_Plain", "iDB_Owner" }, corpus.DataBlocksDeclaring("UDT_Shared").ToArray());
                Assert.Equal(new[] { "iDB_Owner" }, corpus.InstanceDbsOfBlocksDeclaring("UDT_Shared").ToArray());
            }
        }

        private static ObjectKind Kind(IrCorpus corpus, string name)
        {
            CorpusObject? found;
            Assert.True(corpus.TryLookup(name, out found), "corpus did not contain " + name);
            return found!.Kind;
        }
    }
}
