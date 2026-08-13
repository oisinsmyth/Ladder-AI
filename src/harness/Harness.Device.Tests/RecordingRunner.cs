namespace Harness.Device.Tests;

/// <summary>
/// A process runner that records what it was asked to run and returns scripted results.
///
/// <para><b>It is a fake, and it proves nothing about a device.</b> What it does prove is the
/// gateway's ORDERING and its refusals: that an import failure means NO DOWNLOAD IS ATTEMPTED, that a
/// gate that could not run stops the deployment exactly as a failing one does, and that a transport
/// is never opened after a deployment that did not load. Those are properties of this code, and this
/// is where they belong.</para>
/// </summary>
internal sealed class RecordingRunner : IProcessRunner
{
    private readonly Dictionary<DeviceStepKind, ProcessResult> _scripted = new();

    public List<(string Executable, IReadOnlyList<string> Arguments)> Calls { get; } = new();

    /// <summary>Exit code for anything not scripted. 0, so a test only has to state the exception.</summary>
    public int DefaultExitCode { get; set; }

    public string DefaultStdout { get; set; } = string.Empty;

    public void Script(DeviceStepKind kind, ProcessResult result) => _scripted[kind] = result;

    public void ScriptExit(DeviceStepKind kind, int exitCode, string stdout = "") =>
        _scripted[kind] = new ProcessResult(true, false, exitCode, stdout, string.Empty, $"exit {exitCode}");

    public ProcessResult Run(string executable, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        Calls.Add((executable, arguments));

        var kind = Classify(executable, arguments);
        return _scripted.TryGetValue(kind, out var scripted)
            ? scripted
            : new ProcessResult(true, false, DefaultExitCode, kind == DeviceStepKind.Download ? DefaultStdout : string.Empty, string.Empty, $"exit {DefaultExitCode}");
    }

    /// <summary>Which step this invocation is, worked out from the argv the gateway actually produced.</summary>
    private static DeviceStepKind Classify(string executable, IReadOnlyList<string> arguments)
    {
        if (arguments.Count > 0 && arguments[0] == "to-xml")
            return DeviceStepKind.Convert;

        if (executable.EndsWith("download-probe.exe", StringComparison.OrdinalIgnoreCase))
            return DeviceStepKind.Download;

        return arguments[0] switch
        {
            "import-all" => DeviceStepKind.Import,
            "compile-all" => DeviceStepKind.CompileAll,
            "sanity-check" => DeviceStepKind.SanityCheck,
            "block-layout" when arguments.Contains("--set") => DeviceStepKind.LayoutSet,
            "block-layout" => DeviceStepKind.LayoutExpect,
            var other => throw new InvalidOperationException($"the gateway ran an unrecognised subcommand '{other}'."),
        };
    }
}
