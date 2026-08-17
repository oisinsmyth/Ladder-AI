namespace Harness.Verify;

/// <summary>
/// Everything one run needs that it cannot derive. <b>No target is defaulted</b> — not the host, not the
/// port, not the allowlist — for the reason <c>harness-run</c> records: 502 is the port every other
/// Modbus device on a network answers on, so a wrong default is not reliably a loud failure; the quiet
/// one is reading a different device and believing it.
/// </summary>
/// <param name="StableReads">
/// Consecutive identical reads that count as settled. Passed to <c>VersionCheck.Confirm</c> and PRINTED,
/// because <c>ReadsToSettle</c> is meaningless without it: a settle "after 1 read" means the value was
/// already stable when the first of <c>StableReads</c> samples was taken.
/// </param>
public sealed record VerifyOptions(
    string SubmissionPath,
    string BindingPath,
    IReadOnlyList<string> ProgramPaths,
    string Address,
    int Port,
    byte Unit,
    string AllowlistPath,
    int StableReads = 3,
    int MaxReads = 60);
