using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>
/// 🔴 <b>The readable IR is authoritative; a stored sidecar is only ever a faithful DESCRIPTION of it.</b>
///
/// <para><c>FlgNetBuilder</c> rebuilds a network from its sidecar alone — the readable statements are
/// consulted for their counts and nothing else. So when an operand was edited in the readable text of a
/// sidecar'd block (a box's destination, a comparator input, a contact), the sidecar's access records
/// still named the old one and <c>to-xml</c> wrote the ORIGINAL logic back out: exit 0, <c>compare</c>
/// EQUIVALENT to the unedited block, every edit gone. That is ADR-0010's failure exactly — a fact the AI
/// changed in the readable IR, overruled by the sidecar — and the worst kind, because it looks like
/// success.</para>
///
/// <para>So a stored sidecar is used only after it is PROVEN to describe its readable network: the network
/// is built from the sidecar, reduced back to IR, and the two readable renderings compared. Identical
/// (every untouched network, byte for byte as before) → the sidecar is used as-is, source UIds and all.
/// Different → the sidecar is stale for that network, and the network is re-derived from its readable text
/// by the same synthesis a sidecar-less block uses (fresh UIds for that one network; TIA renumbers on
/// import regardless). If that network cannot be synthesized, the conversion is REFUSED, naming the network
/// and the first line that differs. Silently keeping the sidecar's version is never an outcome.</para>
/// </summary>
public static class StoredSidecarReconciler
{
    /// <summary>Called once per network whose stored sidecar was replaced — lets the CLI say so.</summary>
    public delegate void RederivedNotice(int networkNumber, string readableLine, string sidecarLine);

    public static IReadOnlyList<NetworkSidecar> Reconcile(
        IrBlock block, IReadOnlyList<NetworkSidecar> stored,
        CalleeInterfaceRegistry? callees, TagTypeRegistry? tagTypes, RederivedNotice? onRederived = null)
    {
        var result = new List<NetworkSidecar>(stored.Count);
        for (var i = 0; i < block.Networks.Count; i++)
        {
            var network = block.Networks[i];
            var sidecar = i < stored.Count ? stored[i] : null;
            var mismatch = sidecar is null
                ? ("(no stored sidecar for this network)", "(none)")
                : FirstDifference(network, sidecar);

            if (mismatch is null)
            {
                result.Add(sidecar!);
                continue;
            }

            var (readableLine, sidecarLine) = mismatch.Value;
            NetworkSidecar rederived;
            try
            {
                rederived = SidecarSynthesizer.SynthesizeNetwork(block, i, callees, tagTypes)
                    with { CompileUnitUId = sidecar?.CompileUnitUId ?? network.Number.ToString() };
            }
            catch (Exception ex) when (ex is UnsupportedSynthesisConstructException or IrFormatException or UnsupportedConstructException)
            {
                throw new IrFormatException(
                    $"Network {network.Number}: the readable IR no longer matches the stored SIDECAR, and the network " +
                    $"cannot be re-derived from its readable text ({ex.Message}). Refusing rather than writing the " +
                    $"sidecar's stale version. Readable: '{readableLine}'. Sidecar describes: '{sidecarLine}'. Either " +
                    "revert the edit, or re-run to-ir on an export that already carries it.");
            }

            onRederived?.Invoke(network.Number, readableLine, sidecarLine);
            result.Add(rederived);
        }

        return result;
    }

    // Null when the sidecar describes the readable network exactly; otherwise the first differing line of
    // the two readable renderings (the text as written, and what the sidecar would build).
    private static (string Readable, string Sidecar)? FirstDifference(IrNetwork network, NetworkSidecar sidecar)
    {
        var readable = IrSerializer.SerializeNetworkOnly(network);
        string described;
        try
        {
            var built = FlgNetBuilder.Build(network, sidecar);
            // Readable IR in plain kind order states NO rung order — it is the layout every to-ir wrote
            // before statement order became expressible, so the committed sidecar'd corpus is full of it —
            // and there the stored sidecar's order stands, compared kind-grouped. Readable IR in any other
            // order states one, and it must match the sidecar's exactly.
            var reduced = GraphReducer.Reduce(
                built, network.Number, network.Title, sidecar.CompileUnitUId,
                sourceStatementOrder: network.StatementOrder is not null);
            described = IrSerializer.SerializeNetworkOnly(reduced.Network with { Comment = network.Comment });
        }
        catch (Exception ex) when (ex is IrFormatException or NonReducibleNetworkException or UnsupportedConstructException or SimaticMlFormatException)
        {
            return (FirstStatementLine(readable), $"(the sidecar does not fit this network: {ex.Message})");
        }

        if (readable == described)
        {
            return null;
        }

        var a = readable.Split('\n');
        var b = described.Split('\n');
        for (var k = 0; k < Math.Max(a.Length, b.Length); k++)
        {
            var left = k < a.Length ? a[k] : "(end of network)";
            var right = k < b.Length ? b[k] : "(end of network)";
            if (left != right)
            {
                return (left.Trim(), right.Trim());
            }
        }

        return (FirstStatementLine(readable), "(differs)");
    }

    private static string FirstStatementLine(string readable) =>
        readable.Split('\n').Skip(1).FirstOrDefault(l => l.Length > 0)?.Trim() ?? readable.Trim();
}
