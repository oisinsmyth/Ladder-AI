using System.Text;
using System.Text.Json;

namespace Converter.ServedArea;

/// <summary>
/// One record set, two renderers — the house style.
///
/// <para><b>The denominator line is printed FIRST and on every path.</b> Derived, refused or not
/// derived, the reader is told what was examined before being told what was found — the shape
/// <c>drift-check</c>'s <c>COMPARED:</c> and <c>preflight</c>'s <c>RESOLVED AGAINST:</c> already set,
/// and for their reason: a green that does not say what it compared is unreadable as evidence.</para>
///
/// <para>The JSON half is what <c>harness-batch</c> consumes. <b>A width nobody derived OMITS the
/// key</b> — it is never written as 0, because a lenient reader turns a 0 into a comparison that
/// passes against nothing.</para>
/// </summary>
public static class ServedAreaOutputFormatter
{
    /// <summary>
    /// 🔴 Printed under every outcome, including a derived one. It reads the corpus, not the CPU, and a
    /// reader who takes "37 registers, derived" as a statement about the controller has widened the
    /// claim this producer makes.
    /// </summary>
    internal const string CannotSee =
        "WHAT THIS CANNOT SEE: whether the block it read is the block on the controller. It reads the "
        + "program corpus, never the CPU — a corpus stale with respect to the device derives a "
        + "confident, agreed, WRONG number and is indistinguishable from a fresh one. This says only "
        + "that a binding cannot disagree with the program that was staged.";

    public static string FormatText(ServedAreaReport report)
    {
        var sb = new StringBuilder();
        sb.Append("SERVED AREA - the Modbus holding-register window, read off the MB_SERVER call that\n");
        sb.Append("serves it AND off the sidecar constant backing it. Both, or neither.\n\n");
        sb.Append(report.Denominator).Append('\n');

        if (report.Derived)
        {
            sb.Append($"  block     {report.BlockName}\n");
            sb.Append($"  readable  {report.File}:{report.ReadableLine}  {report.ReadableText}\n");
            sb.Append($"  sidecar   {report.File}:{report.SidecarLine}  {report.SidecarText}\n");
            sb.Append($"  area      %M{report.BaseByte} .. %M{report.BaseByte + (report.Registers * 2) - 1} "
                      + $"({report.Registers} register(s))\n");
        }

        foreach (var refusal in report.Refusals)
        {
            sb.Append("REFUSED  ").Append(refusal).Append('\n');
        }

        sb.Append('\n').Append(CannotSee).Append('\n');
        return sb.ToString();
    }

    public static string FormatJson(ServedAreaReport report)
    {
        object payload = report.Derived
            ? new
            {
                derived = true,
                area = report.Area,
                baseByte = report.BaseByte,
                registers = report.Registers,
                block = report.BlockName,
                file = report.File,
                readableLine = report.ReadableLine,
                sidecarLine = report.SidecarLine,
                readableText = report.ReadableText,
                sidecarText = report.SidecarText,
                denominator = report.Denominator,
                filesScanned = report.FilesScanned,
                blocksScanned = report.BlocksScanned,
                cannotSee = CannotSee,
            }
            : new
            {
                derived = false,
                refusals = report.Refusals,
                notDerived = report.Denominator,
                denominator = report.Denominator,
                filesScanned = report.FilesScanned,
                blocksScanned = report.BlocksScanned,
                cannotSee = CannotSee,
            };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }
}
