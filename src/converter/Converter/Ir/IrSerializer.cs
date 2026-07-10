using System.Text;

namespace Converter.Ir;

public static class IrSerializer
{
    public static string SerializeBlock(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var sb = new StringBuilder();
        sb.Append("BLOCK ").Append(block.Kind).Append(' ').Append(block.Name).Append('\n');
        sb.Append("ROOTID ").Append(block.RootUId).Append('\n');
        sb.Append("NUMBER ").Append(block.Number).Append('\n');
        sb.Append("LANGUAGE ").Append(block.Language).Append('\n');
        if (!string.IsNullOrEmpty(block.Comment))
        {
            sb.Append("COMMENT \"").Append(EscapeString(block.Comment)).Append("\"\n");
        }

        foreach (var network in block.Networks)
        {
            sb.Append('\n');
            SerializeNetwork(sb, network);
        }

        sb.Append("\nSIDECAR\n");
        foreach (var sidecar in sidecars)
        {
            SerializeSidecarNetwork(sb, sidecar);
        }

        return sb.ToString();
    }

    /// <summary>Network-only form, for unit tests that operate at network granularity (no BLOCK wrapper).</summary>
    public static string SerializeNetworkOnly(IrNetwork network)
    {
        var sb = new StringBuilder();
        SerializeNetwork(sb, network);
        return sb.ToString();
    }

    private static void SerializeNetwork(StringBuilder sb, IrNetwork network)
    {
        sb.Append("NETWORK ").Append(network.Number).Append(" \"").Append(EscapeString(network.Title)).Append('"');
        if (network.IsEmpty)
        {
            sb.Append(" [empty]\n");
            return;
        }

        sb.Append('\n');
        foreach (var assignment in network.Assignments)
        {
            sb.Append("  COIL ").Append(assignment.CoilTag).Append(" := ").Append(SerializeExpr(assignment.Condition)).Append('\n');
        }
    }

    private static string SerializeExpr(Expr expr) => expr switch
    {
        Expr.TagRef tagRef => tagRef.Path,
        Expr.And { Operands.Count: 0 } => "TRUE",
        Expr.And and => string.Join(" AND ", and.Operands.Select(SerializeExpr)),
        Expr.Or { Operands.Count: 0 } => "TRUE",
        Expr.Or or => string.Join(" OR ", or.Operands.Select(SerializeExpr)),
        _ => throw new IrFormatException($"Unsupported expression node: {expr.GetType().Name}"),
    };

    private static void SerializeSidecarNetwork(StringBuilder sb, NetworkSidecar sidecar)
    {
        sb.Append("NETWORK ").Append(sidecar.NetworkNumber).Append('\n');
        sb.Append("  compileunit = ").Append(sidecar.CompileUnitUId).Append('\n');
        foreach (var access in sidecar.AccessUIds)
        {
            sb.Append("  access ").Append(access.TagPath).Append(" = ").Append(access.UId).Append('\n');
        }

        for (var a = 0; a < sidecar.Assignments.Count; a++)
        {
            var assignment = sidecar.Assignments[a];
            sb.Append("  assignment ").Append(a).Append('\n');
            sb.Append("    rail = ").Append(assignment.RailWireUId).Append('\n');

            for (var i = 0; i < assignment.ContactUIds.Count; i++)
            {
                sb.Append("    contact ").Append(i).Append(" = ").Append(assignment.ContactUIds[i]).Append('\n');
            }

            sb.Append("    coil = ").Append(assignment.CoilUId).Append('\n');

            for (var i = 0; i < assignment.WireUIds.Count; i++)
            {
                sb.Append("    wire ").Append(i).Append(" = ").Append(assignment.WireUIds[i]).Append('\n');
            }
        }
    }

    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
