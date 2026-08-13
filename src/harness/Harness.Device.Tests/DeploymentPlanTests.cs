using Harness.Map;

namespace Harness.Device.Tests;

/// <summary>
/// The ORDERING and the exact argument vectors. Pure — no process, no Portal, no controller — which
/// is what lets the rig-session command sequence be pinned before anyone types it.
/// </summary>
public class DeploymentPlanTests
{
    private static DeviceGatewayOptions Options(bool allowCpuStop = true, string? project = null) =>
        ArgumentVocabularyTests.Options(allowCpuStop, project);

    private static DeploymentPlan Plan(bool withDataBlock = true, DeviceGatewayOptions? options = null) =>
        DeploymentPlan.For(ArgumentVocabularyTests.Objects(withDataBlock), options ?? Options());

    [Fact]
    public void The_order_is_convert_import_layout_gate_download()
    {
        Assert.Equal(
            new[]
            {
                DeviceStepKind.Convert,
                DeviceStepKind.Import,
                DeviceStepKind.LayoutSet,
                DeviceStepKind.LayoutExpect,
                DeviceStepKind.CompileAll,
                DeviceStepKind.SanityCheck,
                DeviceStepKind.Download,
            },
            Plan().Steps.Select(s => s.Kind));
    }

    [Fact]
    public void The_layout_reassertion_comes_AFTER_the_import_that_reverts_it_and_BEFORE_the_gate()
    {
        var kinds = Plan().Steps.Select(s => s.Kind).ToList();

        Assert.True(kinds.IndexOf(DeviceStepKind.Import) < kinds.IndexOf(DeviceStepKind.LayoutSet),
            "the import is what reverts a block to Optimized, so re-asserting before it would be a no-op.");
        Assert.True(kinds.IndexOf(DeviceStepKind.LayoutSet) < kinds.IndexOf(DeviceStepKind.LayoutExpect),
            "--expect only tells you it broke; --set is what repairs it. Gating before repairing would refuse every run.");
        Assert.True(kinds.IndexOf(DeviceStepKind.LayoutExpect) < kinds.IndexOf(DeviceStepKind.CompileAll));
        Assert.True(kinds.IndexOf(DeviceStepKind.SanityCheck) < kinds.IndexOf(DeviceStepKind.Download),
            "hard rule 4: the gate precedes anything that reaches a controller.");
    }

    [Fact]
    public void Every_data_block_gets_a_set_AND_an_expect()
    {
        var objects = ArgumentVocabularyTests.Objects(withDataBlock: false)
            .Append(new HarnessObject("DB_One", HarnessObjectKind.DataBlock, "DB DB_One\n"))
            .Append(new HarnessObject("DB_Two", HarnessObjectKind.DataBlock, "DB DB_Two\n"))
            .ToArray();

        var plan = DeploymentPlan.For(objects, Options());

        Assert.Equal(new[] { "DB_One", "DB_Two" }, plan.DataBlockNames);
        Assert.Equal(2, plan.Steps.Count(s => s.Kind == DeviceStepKind.LayoutSet));
        Assert.Equal(2, plan.Steps.Count(s => s.Kind == DeviceStepKind.LayoutExpect));
        Assert.Contains("DB_One", plan.LayoutNote, StringComparison.Ordinal);
        Assert.Contains("DB_Two", plan.LayoutNote, StringComparison.Ordinal);
    }

    /// <summary>
    /// *** THE NO-OP CASE IS STATED, NOT SILENT. ***
    ///
    /// <para>The harness mirror is <c>%MW</c> bit memory precisely so a re-import cannot silently
    /// revert it, so on a wave with no DB the re-assertion has nothing to do. A plan that simply
    /// omitted the steps would read identically to one where the sequence had been deleted — which is
    /// the failure mode this project keeps finding. The note says which case it was.</para>
    /// </summary>
    [Fact]
    public void With_no_data_block_the_plan_SAYS_the_layout_assertion_had_nothing_to_apply_to()
    {
        var plan = Plan(withDataBlock: false);

        Assert.Empty(plan.DataBlockNames);
        Assert.DoesNotContain(plan.Steps, s => s.Kind is DeviceStepKind.LayoutSet or DeviceStepKind.LayoutExpect);
        Assert.Contains("NO DATA BLOCKS", plan.LayoutNote, StringComparison.Ordinal);
        Assert.Contains("NOT evidence that the re-assertion works", plan.LayoutNote, StringComparison.Ordinal);
    }

    [Fact]
    public void The_convert_step_names_every_object_and_writes_somewhere_other_than_beside_the_input()
    {
        var options = Options();
        var convert = Plan(options: options).Steps.Single(s => s.Kind == DeviceStepKind.Convert);

        Assert.Equal("to-xml", convert.Arguments[0]);

        foreach (var obj in ArgumentVocabularyTests.Objects())
            Assert.Contains(Path.Combine(options.IrStagingDirectory, obj.Name + ".ir"), convert.Arguments);

        var outIndex = convert.Arguments.ToList().IndexOf("--out");
        Assert.True(outIndex >= 0);
        Assert.Equal(options.XmlStagingDirectory, convert.Arguments[outIndex + 1]);
    }

    [Fact]
    public void The_import_step_uses_import_all_and_hands_it_the_directory_verbatim()
    {
        var options = Options();
        var import = Plan(options: options).Steps.Single(s => s.Kind == DeviceStepKind.Import);
        var args = import.Arguments.ToList();

        Assert.Equal("import-all", args[0]);
        Assert.Equal(options.ProjectPath, args[1]);
        Assert.Equal(options.GroupPath, args[args.IndexOf("--group") + 1]);
        Assert.Contains(options.XmlStagingDirectory, args);

        // `import` (singular) takes its files as ONE kind in the order given and stops at the first
        // failure; a harness deployment is a MIXED set in a dependency order filenames cannot express.
        Assert.DoesNotContain("import", args.Take(1));
        Assert.DoesNotContain("--type", args);
        Assert.DoesNotContain("--tagtable", args);
    }

    [Fact]
    public void The_group_path_survives_quoting_with_its_spaces_and_article_number_intact()
    {
        var import = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Import);

        Assert.Contains("\"PLC1 6ES7 214-1AG40-0XB0/Program blocks\"", import.CommandLineText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_download_step_carries_disruptive_and_json_and_the_exact_pc_interface()
    {
        var options = Options();
        var download = Plan(options: options).Steps.Single(s => s.Kind == DeviceStepKind.Download);
        var args = download.Arguments.ToList();

        Assert.Equal(options.ProjectPath, args[0]);
        Assert.Equal("SoftwareOnlyChanges", args[args.IndexOf("--options") + 1]);
        Assert.Equal(options.PcInterface, args[args.IndexOf("--pc-interface") + 1]);
        Assert.Equal(options.TargetInterface, args[args.IndexOf("--target") + 1]);
        Assert.Contains("--disruptive", args);
        Assert.Contains("--json", args);
    }

    [Fact]
    public void A_pc_interface_carrying_a_hash_number_is_passed_whole_and_quoted()
    {
        var download = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Download);

        // The value is tried WHOLE as a name first, so a name containing '#' is never split — but it
        // must survive the shell, and '#' is not special to cmd inside quotes.
        Assert.Contains("\"Intel(R) Ethernet Connection #2\"", download.CommandLineText, StringComparison.Ordinal);
    }

    // ---- REFUSALS --------------------------------------------------------------------------------

    [Fact]
    public void A_project_NOBODY_ALLOWLISTED_yields_NO_STEPS_AT_ALL()
    {
        var plan = Plan(options: Options(project: @"C:\Jobs\RealProject\RealProject.ap20"));

        Assert.False(plan.Planned);
        Assert.Empty(plan.Steps);
        Assert.Contains(plan.Refusals, r => r.Contains("IS NOT AN ALLOWLISTED PROJECT", StringComparison.Ordinal));
    }

    /// <summary>
    /// *** THE FENCE IS A LIST OF PATHS, NOT A NAME. *** Every one of these would have satisfied the
    /// file-name-suffix fence this gateway shipped with, and a convention is something anything can be
    /// renamed into — on a machine carrying about nineteen private engineering projects beside the scratch
    /// ones.
    /// </summary>
    [Theory]
    [InlineData(@"D:\Jobs\Site scratch.ap20")]
    [InlineData(@"D:\Jobs\Anything Scratch.AP20")]
    [InlineData(@"D:\x\myscratch.ap20")]
    [InlineData("")]
    public void A_name_that_LOOKS_like_a_scratch_project_allows_nothing(string path)
    {
        Assert.False(ScratchAllowlist.Evaluate(path, ArgumentVocabularyTests.FenceRoot).Allowed);
    }

    [Fact]
    public void The_fence_accepts_exactly_what_the_allowlist_names()
    {
        Assert.True(ScratchAllowlist.Evaluate(ArgumentVocabularyTests.AllowedProject, ArgumentVocabularyTests.FenceRoot).Allowed);
    }

    [Fact]
    public void Without_AllowCpuStop_there_is_no_plan_because_a_download_could_not_complete()
    {
        var plan = Plan(options: Options(allowCpuStop: false));

        Assert.False(plan.Planned);
        Assert.Empty(plan.Steps);
        Assert.Contains(plan.Refusals, r => r.Contains("AllowCpuStop", StringComparison.Ordinal));
    }

    [Fact]
    public void An_empty_object_set_is_refused_rather_than_downloaded()
    {
        var plan = DeploymentPlan.For(Array.Empty<HarnessObject>(), Options());

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("nothing to deploy", StringComparison.Ordinal));
    }

    [Fact]
    public void A_set_of_only_tag_tables_is_refused_because_it_could_produce_no_manifest_evidence()
    {
        var plan = DeploymentPlan.For(
            new[] { new HarnessObject("HarnessMirror", HarnessObjectKind.TagTable, "TAGTABLE HarnessMirror\n") },
            Options());

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("tag table", StringComparison.Ordinal));
    }

    [Fact]
    public void Two_objects_with_the_same_name_are_refused_because_one_would_replace_the_other()
    {
        var plan = DeploymentPlan.For(
            new[]
            {
                new HarnessObject("FC_Same", HarnessObjectKind.Block, "FC FC_Same\n"),
                new HarnessObject("fc_same", HarnessObjectKind.Block, "FC fc_same\n"),
            },
            Options());

        Assert.False(plan.Planned);
        Assert.Contains(plan.Refusals, r => r.Contains("same file name", StringComparison.Ordinal) || r.Contains("silently replace", StringComparison.Ordinal));
    }

    [Fact]
    public void Tag_tables_are_not_counted_among_the_objects_the_manifest_must_account_for()
    {
        var plan = Plan();

        Assert.DoesNotContain(plan.DownloadableObjects, o => o.Kind == HarnessObjectKind.TagTable);
        Assert.Equal(new[] { "FC_HarnessCopyLayer", "FC_DemoRamp", "DB_Program" }, plan.DownloadableObjects.Select(o => o.Name));
    }
}
