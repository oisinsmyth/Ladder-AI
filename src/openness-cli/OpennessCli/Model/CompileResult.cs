namespace OpennessCli.Model;

public enum CompileState
{
    Success,
    Information,
    Warning,
    Error,
}

public sealed record CompileMessage(CompileState State, string Description, string Path);

public sealed record CompileResult(CompileState State, int ErrorCount, int WarningCount, IReadOnlyList<CompileMessage> Messages);
