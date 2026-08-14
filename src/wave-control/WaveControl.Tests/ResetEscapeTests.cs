using System;
using System.IO;
using Ladder.Wave;
using Ladder.Wave.Cli;
using Xunit;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// *** RESET MUST WORK ON A STORE NOTHING ELSE CAN READ. *** That is the only reason it exists.
    /// </summary>
    /// <remarks>
    /// These pin a defect that lived for about twenty minutes, in a fix for a much older one. The
    /// absent-slots-file guard (see <c>WaveStoreTests</c>) makes <c>Read()</c> refuse a store whose
    /// write died mid-replace, which is right — but <c>reset</c> read the outgoing slot count before
    /// clearing, so <c>reset</c> refused too. *** THE DELIBERATE ESCAPE WAS CLOSED BY THE GUARD IT
    /// EXISTS TO ESCAPE *** and the store could only be recovered by hand-editing files in ProgramData.
    /// <para>
    /// Found by running the tool, not by reading it: 90 kill-mid-write cycles left exactly that state,
    /// and the recovery step was the thing that failed. An over-firing guard does not stay respected —
    /// it gets worked around, and the workaround is worse than the state it prevented.
    /// </para>
    /// </remarks>
    public class ResetEscapeTests
    {
        private static (int Exit, string Output) Run(params string[] args)
        {
            var captured = new StringWriter();
            var previous = Console.Out;

            try
            {
                Console.SetOut(captured);
                return (Program.Main(args), captured.ToString());
            }
            finally
            {
                Console.SetOut(previous);
            }
        }

        private static void Seed(string store, params string[] ids)
        {
            foreach (var id in ids)
            {
                var result = Run(
                    "submit", "--store", store, "--allow-worktree-store", "--agent", "T",
                    "--slot", id, "--reaches", "state/" + id, "--reaches-from", "test",
                    "--width", "16", "--cap", "99", "--cap-provenance", "test");

                Assert.Equal(0, result.Exit);
            }
        }

        [Fact]
        public void Reset_CLEARS_a_store_whose_write_died_mid_replace_and_names_the_loss_as_unknown()
        {
            using (var dir = new TempDirectory())
            {
                Seed(dir.Path, "A", "B", "C");

                // Exactly what an interrupted File.Replace leaves: marker present, slots file gone.
                File.Delete(Path.Combine(dir.Path, WaveStore.SlotsFileName));
                Assert.True(File.Exists(Path.Combine(dir.Path, WaveStore.InitialisedFileName)));

                var reset = Run("reset", "--store", dir.Path, "--allow-worktree-store", "--agent", "T", "--yes");

                Assert.Equal(0, reset.Exit);

                // *** NOT "0 slot(s)". *** Printing zero would say "there was nothing to lose" about
                // precisely the case where there may have been a great deal.
                Assert.Contains("UNKNOWN number of slots", reset.Output, StringComparison.Ordinal);
                Assert.DoesNotContain("0 slot(s) removed", reset.Output, StringComparison.Ordinal);

                // And the store is genuinely usable again — the escape has to land, not merely exit 0.
                Seed(dir.Path, "AFTER");
            }
        }

        [Fact]
        public void Reset_on_a_HEALTHY_store_still_reports_the_REAL_outgoing_count()
        {
            // The did-not-run case. Without it the test above passes against a reset that reports
            // "UNKNOWN" unconditionally — which would throw away the count in the normal case, where
            // it is the only record of what the reset destroyed.
            using (var dir = new TempDirectory())
            {
                Seed(dir.Path, "H1", "H2", "H3");

                var reset = Run("reset", "--store", dir.Path, "--allow-worktree-store", "--agent", "T", "--yes");

                Assert.Equal(0, reset.Exit);
                Assert.Contains("3 slot(s) removed", reset.Output, StringComparison.Ordinal);
                Assert.DoesNotContain("UNKNOWN", reset.Output, StringComparison.Ordinal);
            }
        }
    }
}
