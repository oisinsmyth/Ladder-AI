using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ladder.Wave.Cli;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// `wave-cli batch` — DB-4's entry point.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These tests are about the DRIVER, not about the packer: <see cref="BatchPlannerTests"/> owns the
    /// closure rule and the twenty-object limit. What is asserted here is everything that only exists
    /// once the library has a caller — the denominator on every path, the refusal that an empty change
    /// set gets instead of a green plan, the exit-code contract, the JSON being JSON and nothing else,
    /// and the one rule the CLI adds on top of the library: *** THE LIMIT MAY BE LOWERED AND NEVER
    /// RAISED. ***
    /// </para>
    /// <para>
    /// Every case runs <see cref="BatchCommand.Run(string[], TextWriter, TextWriter)"/> — the same
    /// method <c>Program</c>'s switch reaches — and asserts on what it PRINTS as well as on what it
    /// returns. An exit code alone is a thing a disconnected check can also produce.
    /// </para>
    /// </remarks>
    public sealed class BatchCommandTests
    {
        // =============================================================================================
        // THE DENOMINATOR, ON EVERY OUTCOME
        // =============================================================================================

        [Fact]
        public void Every_outcome_prints_how_many_objects_were_examined_and_how_big_the_baseline_was()
        {
            using (var dir = new TempDirectory())
            {
                var planned = Run(dir, Objects(Fb("FB_A")), Baseline("UDT_Other"));
                var refused = Run(dir, Objects(Ob("OB100")), Baseline("UDT_Other"));
                var nothing = Run(dir, Objects(), Baseline("UDT_Other"));

                foreach (var run in new[] { planned, refused, nothing })
                {
                    Assert.Contains("EXAMINED:", run.Out, StringComparison.Ordinal);
                    Assert.Contains("BASELINE: 1 object(s)", run.Out, StringComparison.Ordinal);
                    Assert.Contains("LIMIT: 20 object(s) per batch", run.Out, StringComparison.Ordinal);
                }

                Assert.Contains("EXAMINED: 1 changed object(s)", planned.Out, StringComparison.Ordinal);
                Assert.Contains("EXAMINED: 1 changed object(s)", refused.Out, StringComparison.Ordinal);
                Assert.Contains("EXAMINED: 0 changed object(s)", nothing.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void The_baseline_provenance_is_printed_because_a_baseline_nobody_can_trace_is_one_nobody_can_falsify()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(dir, Objects(Fb("FB_A")), Baseline("UDT_Other"));
                Assert.Contains("a test fixture baseline", run.Out, StringComparison.Ordinal);
                Assert.Contains("a test fixture change set", run.Out, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // EMPTY IS NOT CLEAN
        // =============================================================================================

        [Fact]
        public void An_empty_change_set_is_exit_2_with_a_reason_never_a_plan_of_zero_batches()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(dir, Objects(), Baseline("FB_Something"));

                Assert.Equal(2, run.Exit);
                Assert.Contains("NOTHING TO BATCH - this is not a pass", run.Out, StringComparison.Ordinal);
                Assert.DoesNotContain("BATCH 0", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_document_with_no_objects_key_is_a_different_refusal_from_one_declaring_no_changes()
        {
            using (var dir = new TempDirectory())
            {
                var path = dir.File("no-key.json");
                File.WriteAllText(path, "{\"provenance\":\"a test fixture change set\"}");

                var run = RunRaw(dir, path, Write(dir, "baseline.json", Baseline("FB_Something")));

                Assert.Equal(2, run.Exit);
                Assert.Contains("carries no `objects` array", run.Err, StringComparison.Ordinal);
                Assert.Contains("a document that never said", run.Err, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_change_set_that_states_no_provenance_is_refused()
        {
            using (var dir = new TempDirectory())
            {
                var path = dir.File("anonymous.json");
                File.WriteAllText(path, "{\"objects\":[{\"name\":\"FB_A\",\"kind\":\"FunctionBlock\",\"changeClass\":\"Run\"}]}");

                var run = RunRaw(dir, path, Write(dir, "baseline.json", Baseline("FB_Something")));

                Assert.Equal(2, run.Exit);
                Assert.Contains("states no `provenance`", run.Err, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_baseline_file_naming_nothing_is_refused_rather_than_read_as_an_empty_device()
        {
            using (var dir = new TempDirectory())
            {
                var baseline = dir.File("empty-baseline.json");
                File.WriteAllText(baseline, "{\"provenance\":\"a read that returned nothing\",\"objects\":[]}");

                var run = RunRaw(dir, Write(dir, "cs.json", Objects(Fb("FB_A"))), baseline);

                Assert.Equal(2, run.Exit);
                Assert.Contains("names no objects", run.Err, StringComparison.Ordinal);
                Assert.Contains("--deployed-empty", run.Err, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_baseline_must_be_given_and_the_two_ways_of_giving_one_are_mutually_exclusive()
        {
            using (var dir = new TempDirectory())
            {
                var changeSet = Write(dir, "cs.json", Objects(Fb("FB_A")));

                var neither = Invoke(new[] { "batch", "--change-set", changeSet });
                Assert.Equal(2, neither.Exit);
                Assert.Contains("a baseline is required", neither.Err, StringComparison.Ordinal);

                var both = Invoke(new[]
                {
                    "batch", "--change-set", changeSet,
                    "--deployed", Write(dir, "base.json", "{\"provenance\":\"p\",\"objects\":[\"X\"]}"),
                    "--deployed-empty", "a bare rig",
                });

                Assert.Equal(2, both.Exit);
                Assert.Contains("mutually exclusive", both.Err, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_declared_empty_baseline_needs_a_reason()
        {
            using (var dir = new TempDirectory())
            {
                var changeSet = Write(dir, "cs.json", Objects(Fb("FB_A")));

                var run = Invoke(new[] { "batch", "--change-set", changeSet, "--deployed-empty" });

                Assert.Equal(2, run.Exit);
                Assert.Contains("takes a REASON", run.Err, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // THE LIMIT MAY BE LOWERED AND NEVER RAISED
        // =============================================================================================

        [Fact]
        public void A_limit_above_db4s_twenty_is_refused_before_anything_is_examined()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke(new[]
                {
                    "batch",
                    "--change-set", Write(dir, "cs.json", Objects(Enumerable.Range(1, 45).Select(i => Fb("FB_" + i)).ToArray())),
                    "--deployed-empty", "a bare rig",
                    "--max-objects", "45",
                    "--max-objects-provenance", "an agent that wanted one download",
                });

                Assert.Equal(2, run.Exit);
                Assert.Contains("above DB-4's limit of 20", run.Err, StringComparison.Ordinal);
                Assert.Contains("NOTHING WAS EXAMINED", run.Err, StringComparison.Ordinal);

                // *** A PLAN OF ONE BATCH OF 45 IS AN ERROR, NOT A REPORT. *** Nothing is printed on
                // stdout at all, so there is no partial plan for a caller to read past the refusal.
                Assert.Equal(string.Empty, run.Out);
            }
        }

        [Fact]
        public void Lowering_the_limit_works_and_requires_a_stated_reason()
        {
            using (var dir = new TempDirectory())
            {
                var changeSet = Write(dir, "cs.json", Objects(Enumerable.Range(1, 6).Select(i => Fb("FB_" + i)).ToArray()));

                var unattributed = Invoke(new[]
                {
                    "batch", "--change-set", changeSet, "--deployed-empty", "a bare rig", "--max-objects", "2",
                });

                Assert.Equal(2, unattributed.Exit);
                Assert.Contains("requires --max-objects-provenance", unattributed.Err, StringComparison.Ordinal);

                var attributed = Invoke(new[]
                {
                    "batch", "--change-set", changeSet, "--deployed-empty", "a bare rig",
                    "--max-objects", "2", "--max-objects-provenance", "exercising the boundary cheaply",
                });

                Assert.Equal(0, attributed.Exit);
                Assert.Contains("LIMIT: 2 object(s) per batch [exercising the boundary cheaply]", attributed.Out, StringComparison.Ordinal);
                Assert.Contains("3 batch(es)", attributed.Out, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// *** THE BACKSTOP, RUN RATHER THAN READ. *** The CLI cannot produce an over-budget batch, so
        /// the only way to exercise the check is to build one through the library — which permits a
        /// larger limit so a test can do exactly this. Both directions are asserted: it must NAME a
        /// 45-object batch, and it must name NOTHING about an ordinary plan, because a check that fires
        /// on working input is noise and noise gets switched off.
        /// </summary>
        [Fact]
        public void The_over_budget_backstop_names_an_oversized_batch_and_stays_silent_on_a_legitimate_one()
        {
            var objects = Enumerable.Range(1, 45)
                .Select(i => new ChangedObject("FB_" + i, ObjectKind.FunctionBlock, ChangeClass.Run))
                .ToArray();

            var baseline = DeployedProgram.Empty("a bare rig, for the backstop test");

            var oversized = WaveBoundaryBatchPlanner.Plan(objects, baseline, maxObjectsPerBatch: 45);
            Assert.Single(oversized.Batches);
            Assert.Equal(45, oversized.Batches[0].Count);

            var caught = BatchCommand.OverBudgetBatches(oversized);
            Assert.Single(caught);
            Assert.Equal(45, caught[0].Count);

            var legitimate = WaveBoundaryBatchPlanner.Plan(objects, baseline);
            Assert.Equal(3, legitimate.Batches.Count);
            Assert.Empty(BatchCommand.OverBudgetBatches(legitimate));
        }

        // =============================================================================================
        // WHAT THE VERB IS FOR: DEPENDENCY-CLOSED BATCHES
        // =============================================================================================

        [Fact]
        public void An_fb_its_instance_db_and_its_udt_land_in_one_batch_and_the_closure_is_reported()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(
                    dir,
                    Objects(
                        Fb("FB_Motor", "UDT_Motor"),
                        Object("iDB_Motor", "InstanceDataBlock", "Run", "FB_Motor"),
                        Object("UDT_Motor", "DataType", "Run")),
                    Baseline("FC_Something"));

                Assert.Equal(0, run.Exit);
                Assert.Contains("BATCH 0 (3 object(s)", run.Out, StringComparison.Ordinal);
                Assert.Contains("CLOSURE: 0 violation(s)", run.Out, StringComparison.Ordinal);
                Assert.Contains("CPU STOP: UNDETERMINED for every batch", run.Out, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// *** THE CASE THAT SEPARATES A PACKER THAT GROUPS FROM ONE THAT DOES NOT. *** With the limit
        /// at three, the FB/iDB/UDT test above passes even for a packer that packs singletons, because
        /// first-fit happens to put them in one bin anyway. Squeeze the limit to two and the two
        /// behaviours diverge: a packer that knows about components REFUSES the group outright, while a
        /// packer that does not splits it and gets caught one step later by the closure check. Both exit
        /// 1, so the REASON is what is asserted — the exit code alone cannot tell them apart.
        /// </summary>
        [Fact]
        public void A_group_that_cannot_fit_is_refused_as_a_group_not_split_and_caught_afterwards()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke(new[]
                {
                    "batch",
                    "--change-set", Write(dir, "cs.json", Objects(
                        Fb("FB_Motor", "UDT_Motor"),
                        Object("iDB_Motor", "InstanceDataBlock", "Run", "FB_Motor"),
                        Object("UDT_Motor", "DataType", "Run"))),
                    "--deployed-empty", "a bare rig",
                    "--max-objects", "2",
                    "--max-objects-provenance", "squeezing the limit so a three-object group cannot fit",
                });

                Assert.Equal(1, run.Exit);
                Assert.Contains("DependencyGroupExceedsObjectLimit", run.Out, StringComparison.Ordinal);
                Assert.DoesNotContain("PlanFailedItsOwnClosureCheck", run.Out, StringComparison.Ordinal);
                Assert.DoesNotContain("DependencyInAnotherBatch", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_dependency_group_larger_than_the_limit_is_exit_1_naming_the_objects()
        {
            using (var dir = new TempDirectory())
            {
                var objects = new List<string> { Object("UDT_Shared", "DataType", "Run") };
                objects.AddRange(Enumerable.Range(1, 20)
                    .Select(i => Object("DB_" + i, "GlobalDataBlock", "RunInit", "UDT_Shared")));

                var run = Run(dir, Objects(objects.ToArray()), Baseline("FC_Something"));

                Assert.Equal(1, run.Exit);
                Assert.Contains("DependencyGroupExceedsObjectLimit", run.Out, StringComparison.Ordinal);
                Assert.Contains("UDT_Shared", run.Out, StringComparison.Ordinal);
                Assert.Contains("EXAMINED: 21 changed object(s)", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_stop_class_object_is_refused_by_the_wave_boundary_planner()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(dir, Objects(Fb("FB_A"), Ob("OB100")), Baseline("FC_Something"));

                Assert.Equal(1, run.Exit);
                Assert.Contains("StopClassObjectInAWaveBoundaryBatch", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_run_init_batch_says_it_invalidates_earlier_results()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(dir, Objects(Object("DB_Recipe", "GlobalDataBlock", "RunInit")), Baseline("FC_Something"));

                Assert.Equal(0, run.Exit);
                Assert.Contains("RESETS DATA", run.Out, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // ABSENT IS NOT EMPTY, INSIDE AN OBJECT AS WELL AS AROUND IT
        // =============================================================================================

        [Fact]
        public void An_unrecognised_kind_or_class_token_is_refused_here_naming_the_token()
        {
            using (var dir = new TempDirectory())
            {
                var badKind = Run(dir, Objects(Object("FB_A", "Frobnicator", "Run")), Baseline("X"));
                Assert.Equal(2, badKind.Exit);
                Assert.Contains("'Frobnicator'", badKind.Err, StringComparison.Ordinal);
                Assert.Contains("FunctionBlock", badKind.Err, StringComparison.Ordinal);

                var ordinal = Run(dir, Objects(Object("FB_A", "2", "Run")), Baseline("X"));
                Assert.Equal(2, ordinal.Exit);
                Assert.Contains("'2'", ordinal.Err, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void An_absent_class_reads_as_unknown_and_is_refused_by_the_planner_with_the_planners_reason()
        {
            using (var dir = new TempDirectory())
            {
                // The distinction the reader exists to preserve: this document did not SAY, which is a
                // different mistake from saying something nobody recognises, and it is refused a level
                // further down with a reason about routing rather than about a token.
                var run = Run(dir, Objects("{\"name\":\"FB_A\",\"kind\":\"FunctionBlock\"}"), Baseline("X"));

                Assert.Equal(1, run.Exit);
                Assert.Contains("StopClassObjectInAWaveBoundaryBatch", run.Out, StringComparison.Ordinal);
                Assert.Contains("(Unknown)", run.Out, StringComparison.Ordinal);
            }
        }

        [Fact]
        public void A_malformed_dependency_list_is_refused_rather_than_read_as_no_dependencies()
        {
            using (var dir = new TempDirectory())
            {
                var run = Run(
                    dir,
                    Objects("{\"name\":\"FB_A\",\"kind\":\"FunctionBlock\",\"changeClass\":\"Run\",\"dependsOn\":\"UDT_X\"}"),
                    Baseline("X"));

                Assert.Equal(2, run.Exit);
                Assert.Contains("declares `dependsOn` as a String", run.Err, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // --json IS JSON AND NOTHING ELSE
        // =============================================================================================

        [Fact]
        public void The_json_output_parses_and_carries_the_denominator()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke(new[]
                {
                    "batch",
                    "--change-set", Write(dir, "cs.json", Objects(
                        Fb("FB_Motor", "UDT_Motor"),
                        Object("iDB_Motor", "InstanceDataBlock", "Run", "FB_Motor"),
                        Object("UDT_Motor", "DataType", "Run"))),
                    "--deployed", Write(dir, "base.json", "{\"provenance\":\"a test fixture baseline\",\"objects\":[\"FC_Something\"]}"),
                    "--json",
                });

                Assert.Equal(0, run.Exit);

                using (var parsed = JsonDocument.Parse(run.Out))
                {
                    var root = parsed.RootElement;
                    Assert.Equal("batch", root.GetProperty("verb").GetString());
                    Assert.Equal("Planned", root.GetProperty("outcome").GetString());
                    Assert.Equal(3, root.GetProperty("objectsExamined").GetInt32());
                    Assert.Equal(1, root.GetProperty("baselineObjectCount").GetInt32());
                    Assert.Equal(20, root.GetProperty("db4Limit").GetInt32());
                    Assert.Equal(1, root.GetProperty("batchCount").GetInt32());
                    Assert.Equal(3, root.GetProperty("plannedObjectCount").GetInt32());
                    Assert.Empty(root.GetProperty("closureViolations").EnumerateArray());
                    Assert.Empty(root.GetProperty("overBudgetBatches").EnumerateArray());
                    Assert.Equal("Undetermined", root.GetProperty("batches")[0].GetProperty("cpuStop").GetString());
                }
            }
        }

        [Fact]
        public void A_refusal_in_json_still_parses_and_names_its_reasons()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke(new[]
                {
                    "batch",
                    "--change-set", Write(dir, "cs.json", Objects(Fb("FB_A"), Ob("OB100"))),
                    "--deployed-empty", "a bare rig",
                    "--json",
                });

                Assert.Equal(1, run.Exit);

                using (var parsed = JsonDocument.Parse(run.Out))
                {
                    Assert.Equal("Refused", parsed.RootElement.GetProperty("outcome").GetString());
                    Assert.True(parsed.RootElement.GetProperty("baselineDeclaredEmpty").GetBoolean());
                    Assert.Contains(
                        parsed.RootElement.GetProperty("refusals").EnumerateArray(),
                        r => r.GetProperty("reason").GetString() == "StopClassObjectInAWaveBoundaryBatch");
                }
            }
        }

        [Fact]
        public void An_empty_change_set_in_json_is_still_exit_2()
        {
            using (var dir = new TempDirectory())
            {
                var run = Invoke(new[]
                {
                    "batch",
                    "--change-set", Write(dir, "cs.json", Objects()),
                    "--deployed-empty", "a bare rig",
                    "--json",
                });

                Assert.Equal(2, run.Exit);

                using (var parsed = JsonDocument.Parse(run.Out))
                {
                    Assert.Equal("NothingToBatch", parsed.RootElement.GetProperty("outcome").GetString());
                    Assert.Equal(0, parsed.RootElement.GetProperty("objectsExamined").GetInt32());
                    Assert.Empty(parsed.RootElement.GetProperty("batches").EnumerateArray());
                }
            }
        }

        // =============================================================================================
        // THE OPTION GRAMMAR THIS VERB SHARES WITH THE REST OF wave-cli
        // =============================================================================================

        [Fact]
        public void A_bare_option_is_a_flag_and_a_missing_option_is_absent_and_they_are_not_the_same()
        {
            using (var dir = new TempDirectory())
            {
                // `--deployed-empty` with no value is PRESENT-but-blank: refused for want of a reason.
                var blank = Invoke(new[]
                {
                    "batch", "--change-set", Write(dir, "cs.json", Objects(Fb("FB_A"))), "--deployed-empty",
                });
                Assert.Contains("takes a REASON", blank.Err, StringComparison.Ordinal);

                // Omitted entirely: refused for want of a baseline at all. Different message, different fault.
                var absent = Invoke(new[] { "batch", "--change-set", Write(dir, "cs2.json", Objects(Fb("FB_A"))) });
                Assert.Contains("a baseline is required", absent.Err, StringComparison.Ordinal);
            }
        }

        // =============================================================================================
        // FIXTURES
        // =============================================================================================

        private sealed class Invocation
        {
            public Invocation(int exit, string output, string error)
            {
                Exit = exit;
                Out = output;
                Err = error;
            }

            public int Exit { get; }
            public string Out { get; }
            public string Err { get; }
        }

        private static Invocation Invoke(string[] args)
        {
            using (var output = new StringWriter())
            using (var error = new StringWriter())
            {
                var exit = BatchCommand.Run(args, output, error);
                return new Invocation(exit, output.ToString(), error.ToString());
            }
        }

        private static Invocation Run(TempDirectory dir, string changeSetJson, string baselineJson) =>
            RunRaw(dir, Write(dir, "change-set.json", changeSetJson), Write(dir, "baseline.json", baselineJson));

        private static Invocation RunRaw(TempDirectory dir, string changeSetPath, string baselinePath) =>
            Invoke(new[] { "batch", "--change-set", changeSetPath, "--deployed", baselinePath });

        private static string Write(TempDirectory dir, string name, string content)
        {
            var path = dir.File(name);
            File.WriteAllText(path, content);
            return path;
        }

        private static string Objects(params string[] objects) =>
            "{\"provenance\":\"a test fixture change set\",\"objects\":[" + string.Join(",", objects) + "]}";

        private static string Baseline(params string[] names) =>
            "{\"provenance\":\"a test fixture baseline\",\"objects\":[" +
            string.Join(",", names.Select(n => "\"" + n + "\"").ToArray()) + "]}";

        private static string Object(string name, string kind, string changeClass, params string[] dependsOn) =>
            "{\"name\":\"" + name + "\",\"kind\":\"" + kind + "\",\"changeClass\":\"" + changeClass +
            "\",\"dependsOn\":[" + string.Join(",", dependsOn.Select(d => "\"" + d + "\"").ToArray()) +
            "],\"artifactHash\":\"sha256:aaaa\"}";

        private static string Fb(string name, params string[] dependsOn) =>
            Object(name, "FunctionBlock", "Run", dependsOn);

        private static string Ob(string name) => Object(name, "OrganizationBlock", "Stop");
    }
}
