using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OpennessCli.Tests")]

// The download probe (src/openness-cli/DownloadProbe, exe `download-probe`) is a SEPARATE binary,
// deliberately: it is the one program in this repo that calls DownloadProvider.Download, and
// `openness-cli` must stay the tool that provably cannot (DownloadPlanTests'
// NoMethodInTheAssemblyReferencesDownloadProviderDownload walks this assembly's IL and asserts so).
//
// What it borrows through this grant is the READ half it must not fork: PLC device resolution, the
// safety refusal, and the outward GetService<DownloadProvider>() walk with its per-object report. A
// second copy of device resolution would be a probe that downloads to a device chosen by different
// rules from the one `download-plan` describes and `compile` gates — which is how you gate one
// device and write another. See OpennessGateway.ResolveDownloadProviderForExternalProbe.
[assembly: InternalsVisibleTo("download-probe")]
