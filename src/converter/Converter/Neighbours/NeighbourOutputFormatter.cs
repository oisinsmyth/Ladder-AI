using System.Text;
using System.Text.Json;

namespace Converter.Neighbours;

/// <summary>
/// One record set, two renderers — the house style.
///
/// <para><b>The denominator and the exclusions are printed FIRST and on every path.</b> Derived,
/// refused or not derived, the reader is told what was examined before being told what was found:
/// the shape <c>drift-check</c>'s <c>COMPARED:</c>, <c>preflight</c>'s <c>RESOLVED AGAINST:</c> and
/// <c>served-area</c>'s <c>NOT DERIVED — n block(s) …</c> already set, and for their reason — a green
/// that does not say what it examined is unreadable as evidence.</para>
///
/// <para>🔴 <b>The JSON half is a document over a subprocess, and a list nobody derived OMITS THE
/// KEY.</b> It is never written as <c>[]</c>, because an empty array reads as "no neighbours" — the
/// precise silent pass this verb exists to prevent. <c>[]</c> appears only under
/// <c>"derived": true</c>, where it is the earned zero and a positive claim.</para>
/// </summary>
public static class NeighbourOutputFormatter
{
    /// <summary>
    /// 🔴 Printed under every outcome, including a derived one — that is the run whose reader is
    /// likeliest to widen the claim into "the area is free".
    /// </summary>
    internal const string CannotSee =
        "WHAT THIS CANNOT SEE: (1) an occupant that reaches %M WITHOUT DECLARING IT — an indirect "
        + "access, a runtime-computed pointer, an offset arrived at by arithmetic. This derivation is "
        + "over declarations in the IR, not over execution, and no amount of scanning changes that. "
        + "(2) anything outside the corpus it was handed. (3) whether that corpus is the program on "
        + "the controller — it reads files, never the CPU. A zero here means 'nothing in the corpus I "
        + "read DECLARED a claim', never 'the area is free'.";

    public static string FormatText(NeighbourReport report)
    {
        var sb = new StringBuilder();
        sb.Append("NEIGHBOURS - every %M claim inside the given area, and the object that declares it.\n");
        sb.Append("A mirror bounded against its own area and proved disjoint from itself cannot see one.\n\n");
        sb.Append(report.Denominator).Append('\n');
        sb.Append(report.Exclusions).Append('\n');

        if (report.Derived && report.Neighbours.Count > 0)
        {
            sb.Append('\n');
            foreach (var claim in report.Neighbours)
            {
                sb.Append($"  %M{claim.StartByte}..%M{claim.EndByteExclusive - 1}  ({claim.ByteLength} byte(s))  ")
                    .Append(claim.Owner).Append("  ").Append(claim.File).Append(':').Append(claim.Line).Append('\n');
            }
        }

        foreach (var declaration in report.AreaDeclarations)
        {
            sb.Append("  (the area's own declaration, not an occupant)  ")
                .Append(declaration.Owner).Append("  ").Append(declaration.File).Append(':').Append(declaration.Line)
                .Append('\n');
        }

        foreach (var refusal in report.Refusals)
        {
            sb.Append("REFUSED  ").Append(refusal).Append('\n');
        }

        sb.Append('\n').Append(CannotSee).Append('\n');
        return sb.ToString();
    }

    public static string FormatJson(NeighbourReport report)
    {
        var area = new
        {
            baseByte = report.Area.BaseByte,
            bytes = report.Area.ByteLength,
            registers = report.Area.Registers,
            topByteExclusive = report.Area.TopByteExclusive,
        };

        var scanned = new
        {
            files = report.FilesScanned,
            tagTables = report.TagTablesScanned,
            blocks = report.BlocksScanned,
            otherObjects = report.OtherObjectsScanned,
            unparseable = report.Unparseable.Count,
        };

        var excluded = new
        {
            belowBase = report.ExcludedBelowBase,
            atOrAboveTop = report.ExcludedAtOrAboveTop,
            nonMarkerPointers = report.ExcludedNonMarkerPointers,
            areaDeclarations = report.AreaDeclarations.Count,
            boundedByAddressAlone = report.BoundedByAddressAlone,
        };

        object payload = report.Derived
            ? new
            {
                verb = "neighbours",
                derived = true,
                area,
                scanned,
                excluded,
                neighbours = report.Neighbours.Select(Render).ToList(),
                areaDeclarations = report.AreaDeclarations.Select(Render).ToList(),
                denominator = report.Denominator,
                exclusions = report.Exclusions,
                cannotSee = CannotSee,
            }
            : report.Refused
                ? new
                {
                    verb = "neighbours",
                    derived = false,
                    area,
                    scanned,
                    excluded,
                    refusals = report.Refusals,
                    denominator = report.Denominator,
                    exclusions = report.Exclusions,
                    cannotSee = CannotSee,
                }
                : new
                {
                    verb = "neighbours",
                    derived = false,
                    area,
                    scanned,
                    excluded,
                    notDerived = report.NotDerivedReason,
                    unparseable = report.Unparseable,
                    denominator = report.Denominator,
                    exclusions = report.Exclusions,
                    cannotSee = CannotSee,
                };

        return JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true });
    }

    private static object Render(MarkerClaim claim) => new
    {
        owner = claim.Owner,
        kind = claim.Kind == NeighbourKind.Tag ? "tag" : "areaPointer",
        container = claim.Container,
        name = claim.Name,
        address = claim.Address,
        startByte = claim.StartByte,
        byteLength = claim.ByteLength,
        endByteExclusive = claim.EndByteExclusive,
        file = claim.File,
        line = claim.Line,
    };
}
