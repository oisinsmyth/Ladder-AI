using System.Collections.Generic;
using System.Linq;

namespace OpennessCli.Model;

public sealed record BlockConsistencyIssue(string Name, string Path, string Language);

public sealed record DeviceCompileSummary(string DevicePath, CompileResult Compile);

public sealed record SanityCheckResult(
    int TotalBlocks,
    IReadOnlyList<BlockConsistencyIssue> InconsistentBlocks,
    IReadOnlyList<DeviceCompileSummary> DeviceCompiles)
{
    public bool IsHealthy => InconsistentBlocks.Count == 0 && DeviceCompiles.All(d => d.Compile.State == CompileState.Success);
}
