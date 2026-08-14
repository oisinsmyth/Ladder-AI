using System.IO;
using Ladder.Wave.Cli;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// 2026-08-14. The reader that turns `converter reachable-state --json` into a
    /// <see cref="TestSlot"/>'s two D9 fields.
    ///
    /// <para>*** THE DEFECT CLASS THIS FILE EXISTS FOR LIVES STRICTLY BETWEEN THE FILE AND THE
    /// PARSER. *** Measured on this very component: 375 unit tests round-tripped through OBJECTS and
    /// were all green, and the first time a CLI wrote a real store and read it back, a trim ate a
    /// trailing tab and every store written by an ordinary slot was unreadable. An assertion about
    /// objects cannot enter that region — so every case here starts from BYTES.</para>
    ///
    /// <para><b>Most of these are absences.</b> The producer omits <c>reachableState</c> and
    /// <c>provenance</c> together when it could not compute a closure, and a reader that defaulted
    /// either one would hand admission a slot that looks independent of everything — silently
    /// converting <c>ColouringDefect.ReachableStateNotComputed</c> from a refusal into a pass.</para>
    /// </summary>
    public class ReachableStateDocumentTests
    {
        private static string Write(TempDirectory dir, string name, string text)
        {
            var path = Path.Combine(dir.Path, name);
            File.WriteAllText(path, text);
            return path;
        }

        [Fact]
        public void AComputedBlock_YieldsItsClosureAndItsProvenance()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [
                { ""block"": ""FB_A"",
                  ""reachableState"": [ ""FB_A|IO.Step"", ""DB_Input.Test"" ],
                  ""provenance"": ""converter reachable-state @ ABC123, 2026-08-14T00:00:00Z"" }
              ]
            }");

            var result = ReachableStateDocument.Read(path, "FB_A");

            Assert.True(result.Ok, result.Refusal);
            Assert.Equal(new[] { "FB_A|IO.Step", "DB_Input.Test" }, result.ReachableState);
            Assert.Contains("ABC123", result.Provenance);

            // The point of reading it at all: admission must accept the slot it builds.
            var plan = WaveSetAdmission.Admit(
                new[] { new TestSlot("S", "agent", result.ReachableState, result.Provenance) },
                System.Array.Empty<ConflictEdge>(),
                maxSlotsPerWaveSet: 4,
                capProvenance: "test");
            Assert.DoesNotContain(plan.Findings, f => f.Defect == ColouringDefect.ReachableStateNotComputed);
        }

        /// <summary>
        /// <c>reachableState: []</c> is the POSITIVE claim "computed, reaches nothing". It must be
        /// accepted — and it must be distinguishable from the next test, which looks identical to any
        /// consumer that only reads the resulting list.
        /// </summary>
        [Fact]
        public void AComputedButEmptyClosure_IsAccepted()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [
                { ""block"": ""FB_A"", ""reachableState"": [], ""provenance"": ""converter reachable-state @ ABC123, X"" }
              ]
            }");

            var result = ReachableStateDocument.Read(path, "FB_A");

            Assert.True(result.Ok, result.Refusal);
            Assert.Empty(result.ReachableState);
            Assert.NotEqual(string.Empty, result.Provenance);
        }

        /// <summary>
        /// *** AND THE ONE THAT LOOKS THE SAME AND MEANS THE OPPOSITE. *** Both keys absent is the
        /// producer saying it could not compute the closure. Defaulting to <c>[]</c> here is the whole
        /// failure mode.
        /// </summary>
        [Fact]
        public void AWithheldClosure_IsRefusedByName_NotDefaultedToEmpty()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [
                { ""block"": ""FB_A"", ""notComputed"": ""the call closure reaches FC_Absent"" }
              ]
            }");

            var result = ReachableStateDocument.Read(path, "FB_A");

            Assert.False(result.Ok);
            Assert.Contains("WITHHELD", result.Refusal);
            Assert.Contains("FB_A", result.Refusal);
            Assert.Contains("FC_Absent", result.Refusal);
        }

        /// <summary>
        /// A closure present but with an EMPTY provenance is the same absence wearing a value. It must
        /// not reach <see cref="TestSlot"/>, where an empty provenance is what admission refuses on —
        /// refusing here names the file; refusing there names only the slot.
        /// </summary>
        [Fact]
        public void AnEmptyProvenance_IsRefusedAtTheReader()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [ { ""block"": ""FB_A"", ""reachableState"": [ ""X"" ], ""provenance"": ""   "" } ]
            }");

            Assert.False(ReachableStateDocument.Read(path, "FB_A").Ok);
        }

        /// <summary>
        /// The producer withholds the WHOLE report by writing <c>notComputed</c> INSTEAD OF
        /// <c>blocks</c>. That refusal must arrive intact rather than becoming "no entry for this
        /// block", which reads as a typo rather than as a partial corpus.
        /// </summary>
        [Fact]
        public void AWholeReportRefusal_ArrivesWithItsReason()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json",
                @"{ ""notComputed"": ""3 project file(s) could not be indexed"" }");

            var result = ReachableStateDocument.Read(path, "FB_A");

            Assert.False(result.Ok);
            Assert.Contains("could not be indexed", result.Refusal);
        }

        /// <summary>
        /// FI-44 at the reader. A block the producer never examined is a different fact from one whose
        /// closure is empty, and the refusal says which by quoting the denominator it was not in.
        /// </summary>
        [Fact]
        public void ABlockTheProducerNeverExamined_IsRefusedAgainstADenominator()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [ { ""block"": ""FB_A"", ""reachableState"": [], ""provenance"": ""p"" } ]
            }");

            var result = ReachableStateDocument.Read(path, "FB_Other");

            Assert.False(result.Ok);
            Assert.Contains("FB_Other", result.Refusal);
            Assert.Contains("1 block(s)", result.Refusal);
        }

        /// <summary>Two entries for one block: refuse rather than pick which closure to believe.</summary>
        [Fact]
        public void DuplicateEntriesForOneBlock_AreRefused()
        {
            using var dir = new TempDirectory();
            var path = Write(dir, "rs.json", @"{
              ""blocks"": [
                { ""block"": ""FB_A"", ""reachableState"": [ ""X"" ], ""provenance"": ""p"" },
                { ""block"": ""FB_A"", ""reachableState"": [ ""Y"" ], ""provenance"": ""p"" }
              ]
            }");

            Assert.False(ReachableStateDocument.Read(path, "FB_A").Ok);
        }

        [Fact]
        public void AMissingOrUnparseableFile_IsRefusedByName()
        {
            using var dir = new TempDirectory();

            Assert.False(ReachableStateDocument.Read(Path.Combine(dir.Path, "nope.json"), "FB_A").Ok);
            Assert.False(ReachableStateDocument.Read(Write(dir, "bad.json", "{ not json"), "FB_A").Ok);
        }
    }
}
