using System;
using Siemens.Engineering.Download;

namespace DownloadProbe;

/// <summary>
/// The single place this program names <c>Siemens.Engineering.Download.DownloadOptions</c>.
///
/// One total mapping from the three command-line literals, with no default arm that could silently
/// yield a value nobody asked for — the <c>_</c> case throws rather than picking one. In particular
/// <c>DownloadOptions.None</c> is never produced: it downloads nothing, so a run that selected it
/// would report the most reassuring possible outcome having transferred nothing.
///
/// The values are NOT combined. Siemens documents <c>Software</c> and <c>SoftwareOnlyChanges</c> as
/// mutually exclusive ("Do not combine"), and <c>Hardware | Software</c> is a legal combination this
/// tool deliberately cannot express: each run exercises exactly one option so that its configuration
/// log belongs to exactly one option.
/// </summary>
internal static class SiemensDownloadOptions
{
    internal static DownloadOptions ToSiemens(DownloadOptionChoice choice) => choice switch
    {
        DownloadOptionChoice.Software => DownloadOptions.Software,
        DownloadOptionChoice.SoftwareOnlyChanges => DownloadOptions.SoftwareOnlyChanges,
        DownloadOptionChoice.Hardware => DownloadOptions.Hardware,
        _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, "Unhandled download option."),
    };
}
