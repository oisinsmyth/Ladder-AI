using System;
using System.IO;
using Ladder.Wave;
using Ladder.Wave.Cli;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// DB-1's change-class table, row by row, plus every route out of it that is NOT a class.
    ///
    /// <para>*** THE REFUSAL TESTS ARE THE POINT, NOT THE COVERAGE. *** A classifier that returned RUN
    /// for everything would pass the four RUN rows below and fail every refusal — which is the
    /// asymmetry these are arranged around. Each refusal names a distinct reason a class could not be
    /// established, and each one is a case that has actually occurred or that the spec itself records as
    /// an open gap.</para>
    /// </summary>
    public sealed class ChangeClassifierTests
    {
        private static IrCorpus Corpus(TempDirectory dir, params string[] files)
        {
            for (var i = 0; i < files.Length; i += 2)
            {
                File.WriteAllText(Path.Combine(dir.Path, files[i]), files[i + 1]);
            }

            return IrCorpus.Read(dir.Path);
        }

        // -------------------------------------------------------------------------------------------
        // THE ROWS THAT EXIST
        // -------------------------------------------------------------------------------------------

        [Theory]
        [InlineData("BLOCK FB FB_X\n", "FB_X", ChangeNature.Modified, ChangeClass.Run)]
        [InlineData("BLOCK FB FB_X\n", "FB_X", ChangeNature.Added, ChangeClass.Run)]
        [InlineData("BLOCK FB FB_X\n", "FB_X", ChangeNature.Deleted, ChangeClass.Run)]
        [InlineData("BLOCK FC FB_X\n", "FB_X", ChangeNature.Modified, ChangeClass.Run)]
        [InlineData("DB FB_X\nMEMBERS\n  A : Bool\n", "FB_X", ChangeNature.Added, ChangeClass.Run)]
        [InlineData("DB FB_X\nMEMBERS\n  A : Bool\n", "FB_X", ChangeNature.Deleted, ChangeClass.Run)]
        [InlineData("DB FB_X\nMEMBERS\n  A : Bool\n", "FB_X", ChangeNature.Modified, ChangeClass.RunInit)]
        [InlineData("DB FB_X\nINSTANCEOF FB_Y\n", "FB_X", ChangeNature.Modified, ChangeClass.RunInit)]
        [InlineData("TYPE FB_X\nMEMBERS\n  A : Bool\n", "FB_X", ChangeNature.Added, ChangeClass.Run)]
        [InlineData("TYPE FB_X\nMEMBERS\n  A : Bool\n", "FB_X", ChangeNature.Modified, ChangeClass.RunInit)]
        [InlineData("TAGTABLE FB_X\nTAGS\n", "FB_X", ChangeNature.Added, ChangeClass.Run)]
        [InlineData("BLOCK OB FB_X\n", "FB_X", ChangeNature.Modified, ChangeClass.Stop)]
        [InlineData("BLOCK OB FB_X\n", "FB_X", ChangeNature.Added, ChangeClass.Stop)]
        [InlineData("BLOCK OB FB_X\n", "FB_X", ChangeNature.Deleted, ChangeClass.Stop)]
        public void EachRowOfDb1sTableProducesItsClass(string ir, string name, ChangeNature nature, ChangeClass expected)
        {
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify(name, nature, Corpus(dir, "o.ir", ir));

                Assert.True(verdict.Classified, verdict.Reason);
                Assert.Equal(expected, verdict.ChangeClass);
                Assert.NotEqual(string.Empty, verdict.Rule);
            }
        }

        [Fact]
        public void AnObHasAClassWithoutANature_BecauseR1FixesItFromTheKindAlone()
        {
            // The one place an UNKNOWN nature does not refuse. It is the fail-closed direction: R1 says
            // "an OB change ... is a STOP-class change", so no nature can make it a RUN, and the worst
            // outcome is an agent waiting for a drain boundary.
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("OB100", ChangeNature.Unknown, Corpus(dir, "o.ir", "BLOCK OB OB100\n"));

                Assert.True(verdict.Classified);
                Assert.Equal(ChangeClass.Stop, verdict.ChangeClass);
            }
        }

        // -------------------------------------------------------------------------------------------
        // THE ROUTES OUT THAT ARE NOT A CLASS
        // -------------------------------------------------------------------------------------------

        [Fact]
        public void AnObjectNoIrDescribesIsRefusedByName_NotGivenAKind()
        {
            // MEASURED, not hypothetical: MotorIOSet and MotorVSDIOSet were found in the controller on
            // 2026-08-14 with no .ir at all.
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("MotorIOSet", ChangeNature.Modified, Corpus(dir, "o.ir", "BLOCK FB FB_X\n"));

                Assert.False(verdict.Classified);
                Assert.Equal(ClassificationRefusal.ObjectNotInCorpus, verdict.Refusal);
                Assert.Equal(ChangeClass.Unknown, verdict.ChangeClass);
                Assert.Contains("MotorIOSet", verdict.Reason);
            }
        }

        [Fact]
        public void AnAmbiguousIdentityIsItsOwnRefusal_NotAnAbsence()
        {
            using (var dir = new TempDirectory())
            {
                var corpus = Corpus(dir, "a.ir", "BLOCK FB Thing\n", "b.ir", "DB Thing\nMEMBERS\n  A : Bool\n");
                var verdict = ChangeClassifier.Classify("Thing", ChangeNature.Modified, corpus);

                Assert.Equal(ClassificationRefusal.AmbiguousCorpusIdentity, verdict.Refusal);
            }
        }

        [Fact]
        public void AnUnstatedNatureRefuses_BecauseTheTableIsKeyedOnBoth()
        {
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("DB_X", ChangeNature.Unknown,
                    Corpus(dir, "o.ir", "DB DB_X\nMEMBERS\n  A : Bool\n"));

                Assert.Equal(ClassificationRefusal.UnknownChangeNature, verdict.Refusal);
                Assert.NotEqual(ChangeClass.Run, verdict.ChangeClass);
            }
        }

        [Theory]
        [InlineData(ChangeNature.Modified)]
        [InlineData(ChangeNature.Deleted)]
        public void AModifiedTagTableHasNoRowAndIsRefused(ChangeNature nature)
        {
            // DB-1's only tag row is "comments, NEW PLC tags ... RUN". A modified table may carry a
            // retyped or readdressed tag and no row covers that. This is the row most likely to be
            // "tidied" into RUN by someone who reads the table quickly.
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("Tags", nature, Corpus(dir, "o.ir", "TAGTABLE Tags\nTAGS\n"));

                Assert.Equal(ClassificationRefusal.NoRuleForThisKindAndNature, verdict.Refusal);
                Assert.Equal(ChangeClass.Unknown, verdict.ChangeClass);
            }
        }

        // -------------------------------------------------------------------------------------------
        // --class: AN ESCAPE, NEVER AN OVERRIDE
        // -------------------------------------------------------------------------------------------

        [Fact]
        public void ADeclaredClassFillsAHoleTheTableHasNoRowFor()
        {
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("Tags", ChangeNature.Modified,
                    Corpus(dir, "o.ir", "TAGTABLE Tags\nTAGS\n"), ChangeClass.Run);

                Assert.True(verdict.Classified);
                Assert.Equal(ChangeClass.Run, verdict.ChangeClass);
                Assert.True(verdict.FromDeclaration);
            }
        }

        [Fact]
        public void ADeclaredClassThatContradictsARowIsRefused_NotApplied()
        {
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("OB100", ChangeNature.Modified,
                    Corpus(dir, "o.ir", "BLOCK OB OB100\n"), ChangeClass.Run);

                Assert.False(verdict.Classified);
                Assert.Equal(ClassificationRefusal.DeclaredClassContradictsTheTable, verdict.Refusal);
            }
        }

        [Fact]
        public void ADeclaredClassThatAgreesWithARowIsNotMarkedAsADeclaration()
        {
            // It matters downstream: the report says which classes came from the table and which from a
            // person, and a declaration that merely echoes the table is the table's answer.
            using (var dir = new TempDirectory())
            {
                var verdict = ChangeClassifier.Classify("OB100", ChangeNature.Modified,
                    Corpus(dir, "o.ir", "BLOCK OB OB100\n"), ChangeClass.Stop);

                Assert.True(verdict.Classified);
                Assert.False(verdict.FromDeclaration);
            }
        }

        // -------------------------------------------------------------------------------------------
        // THE PROPERTY THE WHOLE VERB RESTS ON
        // -------------------------------------------------------------------------------------------

        [Fact]
        public void NoUnclassifiableInputEverProducesRun()
        {
            // Stated as a property over every refusal route rather than as four separate assertions,
            // because the failure this guards against is a NEW route out of the classifier that someone
            // wires to RUN "for now". Any such route lands here.
            using (var dir = new TempDirectory())
            {
                var corpus = Corpus(dir,
                    "a.ir", "TAGTABLE Tags\nTAGS\n",
                    "b.ir", "DB DB_X\nMEMBERS\n  A : Bool\n",
                    "c.ir", "BLOCK FB Dup\n",
                    "d.ir", "DB Dup\nMEMBERS\n  A : Bool\n");

                var unclassifiable = new[]
                {
                    ChangeClassifier.Classify("NotInCorpus", ChangeNature.Modified, corpus),
                    ChangeClassifier.Classify("Dup", ChangeNature.Modified, corpus),
                    ChangeClassifier.Classify("DB_X", ChangeNature.Unknown, corpus),
                    ChangeClassifier.Classify("Tags", ChangeNature.Modified, corpus),
                    ChangeClassifier.Classify(string.Empty, ChangeNature.Modified, corpus),
                };

                Assert.Equal(5, unclassifiable.Length);
                foreach (var verdict in unclassifiable)
                {
                    Assert.False(verdict.Classified);
                    Assert.Equal(ChangeClass.Unknown, verdict.ChangeClass);
                    Assert.NotEqual(ClassificationRefusal.None, verdict.Refusal);
                    Assert.NotEqual(string.Empty, verdict.Reason);
                }
            }
        }

        [Fact]
        public void EveryClassifiedVerdictCitesTheRowThatDecidedIt()
        {
            // A verdict without a citation cannot be argued with, and DB-1's table is transcribed from a
            // vendor document this project has already found two holes in (G2, G7).
            using (var dir = new TempDirectory())
            {
                var corpus = Corpus(dir,
                    "a.ir", "BLOCK OB OB100\n",
                    "b.ir", "BLOCK FB FB_X\n",
                    "c.ir", "DB DB_X\nMEMBERS\n  A : Bool\n",
                    "d.ir", "TYPE UDT_X\nMEMBERS\n  A : Bool\n");

                foreach (var name in new[] { "OB100", "FB_X", "DB_X", "UDT_X" })
                {
                    var verdict = ChangeClassifier.Classify(name, ChangeNature.Modified, corpus);
                    Assert.True(verdict.Classified);
                    Assert.Contains("DB-1", verdict.Rule);
                }
            }
        }
    }
}
