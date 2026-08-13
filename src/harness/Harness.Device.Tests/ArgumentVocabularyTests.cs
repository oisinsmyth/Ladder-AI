using System.Text.RegularExpressions;
using Harness.Map;

namespace Harness.Device.Tests;

/// <summary>
/// *** THE TEST THIS LANE EXISTS TO NOT SKIP. ***
///
/// <para>Every subcommand and flag the plan emits is checked against <b>the argument parser that will
/// receive it</b> — <c>OpennessCli/Cli/ArgumentParser.cs</c>, <c>DownloadProbe/ProbeArguments.cs</c>
/// and <c>Converter/Program.cs</c> — by reading those files and finding the arm that accepts it.
/// Not against a README, not against CLAUDE.md's command table, and not against this project's own
/// memory of what the flags are. A lane briefed from a README recently invoked three flags that did
/// not exist in the binary it was calling.</para>
///
/// <para><b>It fails closed in three separate ways</b>, because a source-reading test is exactly the
/// kind that quietly stops testing: the repository root must be locatable, each named source file
/// must exist, and each file must contain a marker proving it is the file we think it is. A missing
/// file is a FAILURE, never a skip.</para>
///
/// <para><b>It cannot be satisfied by a comment.</b> Flags are matched as <c>case "--x":</c> switch
/// arms — the construct that actually accepts an argument — and the converter's flags as string
/// comparisons in its own parse loop. A mention in a usage string or a comment does not count, which
/// is the same discipline as the stale-binary check: pick a string the code ACTS on.</para>
/// </summary>
public class ArgumentVocabularyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
            dir = dir.Parent;

        Assert.True(dir is not null,
            "the repository root (the directory holding CLAUDE.md) could not be found from "
            + AppContext.BaseDirectory
            + ". This test verifies the gateway's flags against the REAL argument parsers, so being unable to reach them is a failure, not a reason to pass.");

        return dir!.FullName;
    }

    private static string ReadSource(string relativePath, string marker)
    {
        var path = Path.Combine(RepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path),
            $"'{relativePath}' does not exist. It is the authority this test checks the gateway's argument vectors against; without it nothing is verified.");

        var text = File.ReadAllText(path);

        Assert.True(text.Contains(marker, StringComparison.Ordinal),
            $"'{relativePath}' does not contain '{marker}', so it is not the file this test believes it is reading. "
            + "A source-reading check that reads the wrong file is worse than none: it passes for the wrong reason.");

        return text;
    }

    private static string OpennessCliParser() =>
        ReadSource("src/openness-cli/OpennessCli/Cli/ArgumentParser.cs", "public static ParseResult Parse(string[] args)");

    private static string ProbeParser() =>
        ReadSource("src/openness-cli/DownloadProbe/ProbeArguments.cs", "internal static class ProbeArgumentParser");

    private static string ConverterProgram() =>
        ReadSource("src/converter/Converter/Program.cs", "internal static int RunConvert(string[] args)");

    private static void AssertSwitchArm(string source, string literal, string where)
    {
        var pattern = "case\\s+\"" + Regex.Escape(literal) + "\"\\s*:";
        Assert.True(Regex.IsMatch(source, pattern),
            $"'{literal}' has no `case \"{literal}\":` arm in {where}. The gateway emits it, so it would be rejected — "
            + "and on download-probe an unknown option is a hard usage error naming the flag, while on openness-cli an unknown "
            + "positional can be silently swallowed as a file name.");
    }

    // ---------------------------------------------------------------------------------------------
    // The plan under test — one representative deployment with a data block, so every step kind fires.
    // ---------------------------------------------------------------------------------------------

    internal static DeviceGatewayOptions Options(bool allowCpuStop = true, string? project = null) =>
        new(
            ConverterExe: @"C:\bin\converter.exe",
            OpennessCliExe: @"C:\bin\openness-cli.exe",
            DownloadProbeExe: @"C:\bin\download-probe.exe",
            ProjectPath: project ?? @"D:\Rig\Harness scratch\Harness scratch.ap20",
            GroupPath: "PLC1 6ES7 214-1AG40-0XB0/Program blocks",
            PcInterface: "Intel(R) Ethernet Connection #2",
            DownloadOption: DownloadOption.SoftwareOnlyChanges,
            AllowCpuStop: allowCpuStop,
            ModbusHost: "192.0.2.10",
            StagingDirectory: @"C:\staging",
            Device: "PLC1",
            TargetInterface: "PROFINET interface_1",
            IrProjectDirectory: @"C:\repo\ir\test-project001");

    internal static IReadOnlyList<HarnessObject> Objects(bool withDataBlock = true)
    {
        var objects = new List<HarnessObject>
        {
            new("HarnessMirror", HarnessObjectKind.TagTable, "TAGTABLE HarnessMirror\n"),
            new("FC_HarnessCopyLayer", HarnessObjectKind.Block, "FC FC_HarnessCopyLayer\n"),
            new("FC_DemoRamp", HarnessObjectKind.Block, "FC FC_DemoRamp\n"),
        };

        if (withDataBlock)
            objects.Add(new HarnessObject("DB_Program", HarnessObjectKind.DataBlock, "DB DB_Program\n"));

        return objects;
    }

    private static DeploymentPlan Plan() => DeploymentPlan.For(Objects(), Options());

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Every_openness_cli_subcommand_the_plan_emits_is_dispatched_by_the_real_parser()
    {
        var source = OpennessCliParser();
        var plan = Plan();

        var subcommands = plan.Steps
            .Where(s => s.Executable.EndsWith("openness-cli.exe", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.Arguments[0])
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(subcommands);

        foreach (var subcommand in subcommands)
        {
            Assert.True(
                Regex.IsMatch(source, "\"" + Regex.Escape(subcommand) + "\"\\s*=>"),
                $"'{subcommand}' is not an arm of ArgumentParser.Parse's subcommand switch. Every unknown subcommand is a usage error.");
        }
    }

    /// <summary>
    /// The subcommand each flag must be accepted BY. Whole-file matching is not enough: <c>--yes</c>
    /// has a case arm under <c>delete</c>, so a <c>block-layout</c> that did not accept it would still
    /// have passed a file-wide search — and the flag would be swallowed as a positional at runtime.
    /// </summary>
    private static readonly Dictionary<string, string> ParseMethodOf = new(StringComparer.Ordinal)
    {
        ["import-all"] = "ParseImportAll",
        ["compile-all"] = "ParseCompileAll",
        ["sanity-check"] = "ParseSanityCheck",
        ["block-layout"] = "ParseBlockLayout",
    };

    /// <summary>The body of one <c>Parse*</c> method, brace-matched from its signature.</summary>
    private static string MethodBody(string source, string methodName)
    {
        var signature = new Regex(@"ParseResult\s+" + Regex.Escape(methodName) + @"\s*\(");
        var match = signature.Match(source);

        Assert.True(match.Success,
            $"'{methodName}' does not exist in openness-cli's ArgumentParser. The gateway's flags are checked against that method's own case arms, so a renamed method must fail here rather than fall back to a file-wide search that would pass for the wrong reason.");

        var open = source.IndexOf('{', match.Index);
        Assert.True(open > 0, $"'{methodName}' has no body.");

        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0)
                return source.Substring(open, i - open + 1);
        }

        Assert.Fail($"'{methodName}''s body is unbalanced.");
        return string.Empty;
    }

    /// <summary>
    /// The method's own body, plus any <c>ParseX(args)</c> it delegates to. <c>ParseSanityCheck</c>
    /// really does delegate to <c>ParseList</c> — "same flag shape as list" — and pretending otherwise
    /// would make this test refuse a correct command.
    /// </summary>
    private static string AcceptingSource(string source, string subcommand)
    {
        Assert.True(ParseMethodOf.TryGetValue(subcommand, out var method),
            $"the plan emits subcommand '{subcommand}' and this test does not know which Parse* method receives it. Add it to ParseMethodOf rather than widening the search.");

        var body = MethodBody(source, method!);
        var text = body;

        foreach (Match delegated in Regex.Matches(body, @"\b(Parse[A-Za-z]+)\(args\)"))
        {
            var name = delegated.Groups[1].Value;
            if (name != method)
                text += MethodBody(source, name);
        }

        return text;
    }

    [Fact]
    public void Every_openness_cli_flag_is_accepted_by_the_SUBCOMMANDS_OWN_parse_method()
    {
        var source = OpennessCliParser();

        foreach (var step in Plan().Steps.Where(s => s.Executable.EndsWith("openness-cli.exe", StringComparison.OrdinalIgnoreCase)))
        {
            var accepting = AcceptingSource(source, step.Arguments[0]);

            foreach (var flag in step.Arguments.Where(a => a.StartsWith("--", StringComparison.Ordinal)))
                AssertSwitchArm(accepting, flag, $"{step.Arguments[0]}'s own parse method (step {step.Kind})");
        }
    }

    /// <summary>
    /// The scoping above is only worth anything if it can refuse. <c>--tagtables</c> is a REAL flag
    /// with a real case arm — under <c>list</c> — and it is not one <c>block-layout</c> accepts.
    /// </summary>
    [Fact]
    public void The_scoped_search_REFUSES_a_real_flag_that_belongs_to_a_different_subcommand()
    {
        var source = OpennessCliParser();

        Assert.Matches("case\\s+\"--tagtables\"\\s*:", source);
        Assert.DoesNotMatch("case\\s+\"--tagtables\"\\s*:", AcceptingSource(source, "block-layout"));
    }

    [Fact]
    public void Every_download_probe_flag_the_plan_emits_has_a_case_arm_in_the_probes_own_parser()
    {
        var source = ProbeParser();
        var download = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Download);

        foreach (var flag in download.Arguments.Where(a => a.StartsWith("--", StringComparison.Ordinal)))
            AssertSwitchArm(source, flag, "download-probe's ProbeArgumentParser");
    }

    [Fact]
    public void The_probe_option_literal_is_one_the_probe_actually_parses()
    {
        var source = ReadSource("src/openness-cli/DownloadProbe/DownloadOptionChoice.cs", "TryParseLiteral");
        var download = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Download);
        var literal = download.Arguments[download.Arguments.ToList().IndexOf("--options") + 1];

        Assert.True(
            Regex.IsMatch(source, "Equals\\(text,\\s*\"" + Regex.Escape(literal) + "\""),
            $"'{literal}' is not one of the three literals DownloadOptionChoices.TryParseLiteral matches. --options is REQUIRED and has no default.");
    }

    [Fact]
    public void The_layout_value_is_one_the_real_memory_layout_parser_accepts()
    {
        var source = ReadSource("src/openness-cli/OpennessCli/Cli/ArgumentParser.cs", "TryParseMemoryLayout");
        var set = Plan().Steps.First(s => s.Kind == DeviceStepKind.LayoutSet);
        var value = set.Arguments[set.Arguments.ToList().IndexOf("--set") + 1];

        // TryParseMemoryLayout deliberately does NOT use Enum.TryParse (which would accept "0"/"1"),
        // so the accepted spelling is a literal in that method.
        Assert.Equal("Standard", value);
        Assert.Contains("Standard", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_converter_flag_the_plan_emits_is_read_by_the_converters_own_parse_loop()
    {
        var source = ConverterProgram();
        var convert = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Convert);

        Assert.Equal("to-xml", convert.Arguments[0]);
        Assert.Contains("\"to-ir\" or \"to-xml\"", source, StringComparison.Ordinal);

        // The converter reads its flags with `rest[i] == "--x"` rather than a switch, so the pattern
        // is different — but it is still the CODE that acts, never the usage text.
        foreach (var flag in convert.Arguments.Where(a => a.StartsWith("--", StringComparison.Ordinal)))
        {
            Assert.True(
                Regex.IsMatch(source, "rest\\[i\\]\\s*==\\s*\"" + Regex.Escape(flag) + "\""),
                $"'{flag}' is not read by RunConvert's parse loop. An unrecognised --flag there is a hard refusal (FI-73).");
        }
    }

    // ---- THE NEGATIVES: flags that MUST NOT be emitted -------------------------------------------

    [Fact]
    public void The_gateway_never_passes_yes_or_force_to_download_probe()
    {
        var download = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Download);

        // download-probe has no --yes, no --force and no --confirm: every run of it is a real download
        // attempt, and its gate is the scratch-path guard. Emitting one would be an outright usage error.
        Assert.DoesNotContain("--yes", download.Arguments);
        Assert.DoesNotContain("--force", download.Arguments);
        Assert.DoesNotContain("--confirm", download.Arguments);
    }

    [Fact]
    public void The_gateway_never_arms_the_deliberate_throw_injection()
    {
        var download = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Download);

        // Experiment 1.7's flags break the download on purpose; a POST throw can leave the CPU
        // stopped with no route to start it from inside that download.
        Assert.DoesNotContain("--throw-from-pre-delegate", download.Arguments);
        Assert.DoesNotContain("--throw-from-post-delegate", download.Arguments);
        Assert.DoesNotContain("--to-folder", download.Arguments);
    }

    [Fact]
    public void The_gateway_never_passes_allow_blind_types_to_the_converter()
    {
        var convert = Plan().Steps.Single(s => s.Kind == DeviceStepKind.Convert);

        // FI-71's refusal is wanted: a guessed member type imports and is rejected by TIA at compile,
        // after a full round trip. Silencing it here would restore the three-round-trip bug.
        Assert.DoesNotContain("--allow-blind-types", convert.Arguments);
    }

    [Fact]
    public void The_gateway_never_uses_download_plan_which_cannot_download()
    {
        // `openness-cli download-plan` is READ-ONLY AND DRY-RUN ONLY — an IL test walks every method
        // in that assembly asserting nothing reaches DownloadProvider.Download. A gateway that called
        // it would report a plan and deploy nothing.
        Assert.DoesNotContain(Plan().Steps, s => s.Arguments.Contains("download-plan"));
    }

    [Fact]
    public void The_gateways_scratch_fence_is_the_same_string_as_the_probes_own_guard()
    {
        var guard = ReadSource("src/openness-cli/DownloadProbe/ScratchProjectGuard.cs", "RequiredSuffix");

        Assert.True(
            guard.Contains("\"" + DeviceGatewayOptions.RequiredProjectSuffix + "\"", StringComparison.Ordinal),
            $"the gateway fences on '{DeviceGatewayOptions.RequiredProjectSuffix}' and ScratchProjectGuard does not use that literal. "
            + "A gateway fence LOOSER than the probe's would pass its own guard and then be refused at exit 3 — after a full import and compile had already been written into a real project.");
    }
}
