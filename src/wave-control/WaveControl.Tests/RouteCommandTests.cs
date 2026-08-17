using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ladder.Wave;
using Ladder.Wave.Cli;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// The <c>route</c> verb through the entry point a caller actually uses.
    ///
    /// <para>*** THESE DRIVE <see cref="RouteCommand.Run"/>, NOT THE PIECES UNDER IT. *** A gate held in
    /// one <c>if</c> is only as good as the test that routes through it: this repo has a measured case
    /// where a refusal helper was called DIRECTLY, pinning the message and not the routing, and the
    /// gate could be disconnected with a one-token change while 712 tests stayed green. So the exit
    /// code AND the observable output are asserted from the outside.</para>
    ///
    /// <para>The last test sweeps the REAL 43-object corpus. A whole-corpus sweep reaches defects no
    /// synthetic fixture can, because the input you would otherwise have to invent is already in the
    /// repository.</para>
    /// </summary>
    public sealed class RouteCommandTests
    {
        private const string RealCorpus = "ir/test-project001";

        private sealed class Run
        {
            public int ExitCode;
            public string Out = string.Empty;
            public string Error = string.Empty;
            public string All => Out + Error;
        }

        private static Run Invoke(params string[] args)
        {
            var stdout = new StringWriter();
            var stderr = new StringWriter();
            var run = new Run();
            run.ExitCode = RouteCommand.Run(args, stdout, stderr);
            run.Out = stdout.ToString();
            run.Error = stderr.ToString();
            return run;
        }

        private static string Corpus(TempDirectory dir, params string[] nameThenContent)
        {
            for (var i = 0; i < nameThenContent.Length; i += 2)
            {
                File.WriteAllText(Path.Combine(dir.Path, nameThenContent[i]), nameThenContent[i + 1]);
            }

            return dir.Path;
        }

        /// <summary>Locate the repository root by walking up for the real IR corpus.</summary>
        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, RealCorpus)))
            {
                dir = dir.Parent;
            }

            Assert.True(dir != null, "could not find " + RealCorpus + " above " + AppContext.BaseDirectory);
            return dir.FullName;
        }

        // ===========================================================================================
        // EMPTY IS NOT CLEAN
        // ===========================================================================================

        [Fact]
        public void AnEmptyCorpusIsExit2_NotAGreenRunThatClassifiedNothing()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke("route", "--project", dir.Path, "--changed", "X=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("NOTHING EXAMINED", run.All);
            }
        }

        [Fact]
        public void AChangeSetWhoseEveryRowIsANonChangeIsExit2_AndSaysSo()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":2,\"entries\":[" +
                    "{\"name\":\"FB_X\",\"status\":\"Match\"},{\"name\":\"FB_Y\",\"status\":\"Match\"}]}");

                var run = Invoke("route", "--project", project, "--drift-check", report,
                    "--exports-are", "committed-corpus");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("NOTHING ROUTED - this is not a pass", run.All);
                // The denominator survives the refusal: 2 rows were read, both non-changes.
                Assert.Contains("2 row(s) were read", run.All);
            }
        }

        [Fact]
        public void ADriftReportThatComparedNothingIsRefused()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":0,\"entries\":[]}");

                var run = Invoke("route", "--project", project, "--drift-check", report,
                    "--exports-are", "committed-corpus");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("NOTHING WAS COMPARED", run.All);
            }
        }

        [Fact]
        public void ADocumentWithNoEntriesArrayIsRefused_AbsentIsNotEmpty()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"somethingElse\":1}");

                var run = Invoke("route", "--project", project, "--drift-check", report,
                    "--exports-are", "committed-corpus");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("ABSENT is not EMPTY", run.All);
            }
        }

        [Fact]
        public void EveryRunPrintsTheDenominator()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var run = Invoke("route", "--project", project, "--changed", "FB_X=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("EXAMINED: 1 object(s) from the change set", run.Out);
                Assert.Contains(".ir file(s) read", run.Out);
            }
        }

        // ===========================================================================================
        // FAIL CLOSED
        // ===========================================================================================

        [Fact]
        public void AnUnclassifiableObjectIsNamedAndTheRunFailsClosed()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var run = Invoke("route", "--project", project,
                    "--changed", "FB_X=modified", "--changed", "Ghost=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRefused, run.ExitCode);
                Assert.Contains("REFUSED (1)", run.Out);
                Assert.Contains("Ghost", run.Out);
                Assert.Contains("ObjectNotInCorpus", run.Out);
                Assert.Contains("NOTHING was defaulted to RUN", run.Out);

                // And the classifiable one still got routed — a refusal is per-object, not a run-wide
                // veto that hides what WAS established.
                Assert.Contains("FB_X", run.Out);
                Assert.Contains("RunQueue", run.Out);
            }
        }

        [Fact]
        public void AStopClassChangeGoesToTheDeferredQueueAndIsCalledOut()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK OB Main\n");
                var run = Invoke("route", "--project", project, "--changed", "Main=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("-> STOP -> DeferredQueue", run.Out);
                Assert.Contains("do NOT flow through a wave boundary", run.Out);
            }
        }

        [Fact]
        public void TheTwoChangeSetPathsAreMutuallyExclusive()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":1,\"entries\":[{\"name\":\"FB_X\",\"status\":\"Drifted\"}]}");

                var run = Invoke("route", "--project", project, "--drift-check", report,
                    "--exports-are", "committed-corpus", "--changed", "FB_X=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("cannot be combined", run.All);
            }
        }

        [Fact]
        public void ADeclaredChangeSetWithoutProvenanceIsRefused()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var run = Invoke("route", "--project", project, "--changed", "FB_X=modified");

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("--changed-from is required", run.All);
            }
        }

        [Fact]
        public void ADriftReportWithoutExportsAreIsRefused_BecauseSkippedMeansOppositeThings()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":1,\"entries\":[{\"name\":\"FB_X\",\"status\":\"Skipped\"}]}");

                var run = Invoke("route", "--project", project, "--drift-check", report);

                Assert.Equal(RouteCommand.ExitUnusable, run.ExitCode);
                Assert.Contains("--exports-are was not given", run.All);
            }
        }

        [Fact]
        public void ASkippedRowMeansOppositeThingsUnderTheTwoExportMeanings()
        {
            // The whole reason --exports-are has no default. Same document, same row, opposite outcome.
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":1,\"entries\":[{\"name\":\"FB_X\",\"status\":\"Skipped\"}]}");

                var committed = Invoke("route", "--project", project, "--drift-check", report, "--exports-are", "committed-corpus");
                Assert.Equal(RouteCommand.ExitUnusable, committed.ExitCode);
                Assert.Contains("NOTHING ROUTED", committed.All);

                var controller = Invoke("route", "--project", project, "--drift-check", report, "--exports-are", "controller-dump");
                Assert.Equal(RouteCommand.ExitRouted, controller.ExitCode);
                Assert.Contains("[FunctionBlock, Added]", controller.Out);
            }
        }

        [Fact]
        public void AnExportOnlyRowIsRefused_BecauseTheDocumentCannotSayWhetherItIsADeletion()
        {
            using (var dir = new TempDirectory())
            using (var reports = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n");
                var report = Path.Combine(reports.Path, "drift.json");
                File.WriteAllText(report, "{\"comparedCount\":1,\"entries\":[" +
                    "{\"name\":\"FB_X\",\"status\":\"Drifted\"},{\"name\":\"MotorIOSet\",\"status\":\"ExportOnly\"}]}");

                var run = Invoke("route", "--project", project, "--drift-check", report, "--exports-are", "controller-dump");

                Assert.Equal(RouteCommand.ExitRefused, run.ExitCode);
                Assert.Contains("MotorIOSet", run.Out);
            }
        }

        // ===========================================================================================
        // BLAST RADIUS
        // ===========================================================================================

        [Fact]
        public void AModifiedUdtCarriesEveryDbBuiltOnIt_AsRoutedObjects()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir,
                    "t.ir", "TYPE UDT_Shared\nMEMBERS\n  A : Bool\n",
                    "fb.ir", "BLOCK FB FB_Owner\nINTERFACE\n  STATIC\n    IO : \"UDT_Shared\"\n",
                    "idb.ir", "DB iDB_Owner\nINSTANCEOF FB_Owner\nMEMBERS\n  IO : \"UDT_Shared\"\n",
                    "gdb.ir", "DB DB_Plain\nMEMBERS\n  IO : \"UDT_Shared\"\n");

                var run = Invoke("route", "--project", project, "--changed", "UDT_Shared=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("EXAMINED: 1 object(s) from the change set + 2 entailed by blast radius = 3 total", run.Out);
                Assert.Contains("BLAST RADIUS", run.Out);
                Assert.Contains("DB_Plain", run.Out);
                Assert.Contains("iDB_Owner", run.Out);
                Assert.Contains("RUN (Init)", run.Out);
                // The two routes disagree here (the FB route cannot see the plain DB) and it is REPORTED.
                Assert.Contains("THEY DISAGREE", run.Out);
            }
        }

        [Fact]
        public void AUdtWithNoDbsBuiltOnItReportsAComputedZero_NotSilence()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "t.ir", "TYPE UDT_Orphan\nMEMBERS\n  A : Bool\n");
                var run = Invoke("route", "--project", project, "--changed", "UDT_Orphan=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("NONE in this corpus", run.Out);
                Assert.Contains("not an unasked question", run.Out);
            }
        }

        [Fact]
        public void AnObjectAlreadyInTheChangeSetIsNotCountedTwiceByBlastRadius()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir,
                    "t.ir", "TYPE UDT_Shared\nMEMBERS\n  A : Bool\n",
                    "gdb.ir", "DB DB_Plain\nMEMBERS\n  IO : \"UDT_Shared\"\n");

                var run = Invoke("route", "--project", project,
                    "--changed", "UDT_Shared=modified", "--changed", "DB_Plain=modified", "--changed-from", "test");

                Assert.Contains("EXAMINED: 2 object(s) from the change set + 0 entailed by blast radius = 2 total", run.Out);
                Assert.Contains("already in the change set: DB_Plain", run.Out);
            }
        }

        // ===========================================================================================
        // JSON AND DEPENDENCY CLOSURE
        // ===========================================================================================

        [Fact]
        public void JsonCarriesTheSameVerdictAndDenominatorAsTheText()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir, "a.ir", "BLOCK FB FB_X\n", "b.ir", "BLOCK OB Main\n");
                var run = Invoke("route", "--project", project, "--json",
                    "--changed", "FB_X=modified", "--changed", "Main=modified",
                    "--changed", "Ghost=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRefused, run.ExitCode);

                using (var document = JsonDocument.Parse(run.Out))
                {
                    var root = document.RootElement;
                    Assert.Equal(3, root.GetProperty("changeSet").GetProperty("examined").GetInt32());
                    Assert.Equal(2, root.GetProperty("routed").GetArrayLength());
                    Assert.Equal(1, root.GetProperty("refused").GetArrayLength());
                    Assert.Equal(1, root.GetProperty("queues").GetProperty("runQueue").GetInt32());
                    Assert.Equal(1, root.GetProperty("queues").GetProperty("deferredQueue").GetInt32());
                    Assert.Equal(RouteCommand.ExitRefused, root.GetProperty("exitCode").GetInt32());
                    Assert.Equal(2, root.GetProperty("corpus").GetProperty("objectsIdentified").GetInt32());
                }
            }
        }

        [Fact]
        public void ADependencyOutsideTheChangeSetIsNamed()
        {
            using (var dir = new TempDirectory())
            {
                var project = Corpus(dir,
                    "fb.ir", "BLOCK FB FB_Owner\n",
                    "idb.ir", "DB iDB_Owner\nINSTANCEOF FB_Owner\nMEMBERS\n  A : Bool\n");

                var run = Invoke("route", "--project", project, "--changed", "iDB_Owner=modified", "--changed-from", "test");

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("DB-4 closure: depends on FB_Owner", run.Out);
                Assert.Contains("must ALREADY BE PRESENT", run.Out);
            }
        }

        [Fact]
        public void OverTwentyObjectsIsReportedAgainstDb4sBudget()
        {
            using (var dir = new TempDirectory())
            {
                var files = new List<string>();
                for (var i = 0; i < 21; i++)
                {
                    files.Add("fb" + i + ".ir");
                    files.Add("BLOCK FB FB_" + i + "\n");
                }

                var project = Corpus(dir, files.ToArray());
                var args = new List<string> { "route", "--project", project, "--changed-from", "test" };
                for (var i = 0; i < 21; i++)
                {
                    args.Add("--changed");
                    args.Add("FB_" + i + "=modified");
                }

                var run = Invoke(args.ToArray());

                Assert.Equal(RouteCommand.ExitRouted, run.ExitCode);
                Assert.Contains("BUDGET: 21 object(s) against DB-4's <=20 - OVER", run.Out);
            }
        }

        // ===========================================================================================
        // THE REAL CORPUS
        // ===========================================================================================

        [Fact]
        public void TheRealCorpusIsReadWithoutAnUnreadableOrAmbiguousObject()
        {
            var root = RepoRoot();
            var corpus = IrCorpus.Read(Path.Combine(root, RealCorpus));

            Assert.True(corpus.FilesSeen > 40, "expected the full corpus, saw " + corpus.FilesSeen + " file(s)");
            Assert.Equal(corpus.FilesSeen, corpus.Objects.Count);
            Assert.Empty(corpus.Unreadable);
            Assert.Empty(corpus.AmbiguousNames);

            // Spot-check the two kinds a name heuristic would get wrong.
            CorpusObject? motorIoSet;
            Assert.True(corpus.TryLookup("MotorFwdRevIOSet", out motorIoSet));
            Assert.Equal(ObjectKind.DataType, motorIoSet!.Kind);

            CorpusObject? main;
            Assert.True(corpus.TryLookup("Main", out main));
            Assert.Equal(ObjectKind.OrganizationBlock, main!.Kind);

            // TIA's own name for the default tag table, from the file's content and not its filename.
            Assert.True(corpus.TryLookup("Default tag table", out _));
        }

        [Fact]
        public void EveryObjectInTheRealCorpusClassifiesOrIsNamed_NoSilentRun()
        {
            // The sweep. 43 objects nobody wrote for this classifier, each put through the table as a
            // MODIFIED change. Anything the table has no row for must appear as a REFUSAL by name — the
            // one outcome that must never happen is a quiet RUN.
            var root = RepoRoot();
            var corpus = IrCorpus.Read(Path.Combine(root, RealCorpus));

            var classified = new List<string>();
            var refused = new List<string>();

            foreach (var o in corpus.Objects)
            {
                var verdict = ChangeClassifier.Classify(o.Name, ChangeNature.Modified, corpus);
                if (verdict.Classified)
                {
                    classified.Add(o.Name + " -> " + verdict.ChangeClass);
                    Assert.Contains("DB-1", verdict.Rule);
                }
                else
                {
                    refused.Add(o.Name + " -> " + verdict.Refusal);
                    Assert.NotEqual(string.Empty, verdict.Reason);
                }
            }

            Assert.Equal(corpus.Objects.Count, classified.Count + refused.Count);

            // The two tag tables are the corpus's only rows DB-1's table does not cover, and they are
            // REFUSED rather than routed. Pinned as an exact set: if a future edit gives tag tables a
            // computed class this goes red and demands the vendor row that justifies it.
            Assert.Equal(2, refused.Count);
            Assert.All(refused, r => Assert.Contains("NoRuleForThisKindAndNature", r));
        }
    }
}
