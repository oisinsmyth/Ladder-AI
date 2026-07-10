using System;
using System.Collections.Generic;
using OpennessCli.Model;

namespace OpennessCli.Openness;

public interface IOpennessGateway : IDisposable
{
    void Connect(TimeSpan timeout);

    void OpenProject(string projectIdentifier, TimeSpan timeout);

    IReadOnlyList<BlockInfo> EnumerateBlocks();
}

public sealed class ConnectTimeoutException : Exception
{
    public ConnectTimeoutException(TimeSpan timeout)
        : base(
            $"TIA Portal did not respond to attach/launch within {timeout.TotalMinutes:0} minute(s). " +
            "This is usually the first-connect approval dialog waiting inside TIA Portal — check Portal, " +
            "accept the dialog if it's there, then re-run. Not retrying automatically (single-session rule).")
    {
    }
}

public sealed class ProjectOpenTimeoutException : Exception
{
    public ProjectOpenTimeoutException(TimeSpan timeout)
        : base(
            $"Opening the project did not complete within {timeout.TotalMinutes:0} minute(s). Large TIA " +
            "projects can genuinely take several minutes — this can be normal. Do not kill TIA Portal; " +
            "pass --timeout-open with a larger value if this project is known to be large, then re-run.")
    {
    }
}
