using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Diff;

// Compares the before/after IR of one block at network granularity. See DiffModel for the semantic-
// equality definition (sidecar-free readable form). Both inputs are parsed with IrParser.ParseBlock,
// i.e. real exported IR carrying a SIDECAR — the S7 shape (before = exported block, after = the same
// block edited then reconverted). A sidecar-less input throws IrFormatException, surfaced by the
// caller as a clean exit 1; supporting sidecar-less diffs isn't needed for the invariance use.
public static class DiffRunner
{
    public static DiffReport Run(string oldPath, string newPath, IReadOnlyList<int> onlyNetworks)
    {
        var (oldBlock, oldSidecars) = IrParser.ParseBlock(File.ReadAllText(oldPath));
        var (newBlock, newSidecars) = IrParser.ParseBlock(File.ReadAllText(newPath));

        var header = DiffHeader(oldBlock, oldSidecars, newBlock, newSidecars);
        var networks = DiffNetworks(oldBlock, newBlock);

        return new DiffReport(
            BlockName: newBlock.Name,
            BlockNameMismatch: !string.Equals(oldBlock.Name, newBlock.Name, StringComparison.Ordinal),
            OtherBlockName: oldBlock.Name,
            Header: header,
            Networks: networks,
            AllowedNetworks: onlyNetworks);
    }

    private static IReadOnlyList<NetworkDiff> DiffNetworks(IrBlock oldBlock, IrBlock newBlock)
    {
        // Networks are matched by Number — S7 edits in place, so numbering is stable. A wholesale
        // renumbering would misreport (a known, documented limitation, not handled here).
        var oldByNumber = oldBlock.Networks.ToDictionary(n => n.Number);
        var newByNumber = newBlock.Networks.ToDictionary(n => n.Number);

        var results = new List<NetworkDiff>();
        foreach (var number in oldByNumber.Keys.Union(newByNumber.Keys).OrderBy(k => k))
        {
            var hasOld = oldByNumber.TryGetValue(number, out var oldNet);
            var hasNew = newByNumber.TryGetValue(number, out var newNet);

            if (hasOld && hasNew)
            {
                var oldText = IrSerializer.SerializeNetworkOnly(oldNet!);
                var newText = IrSerializer.SerializeNetworkOnly(newNet!);
                if (string.Equals(oldText, newText, StringComparison.Ordinal))
                {
                    results.Add(new NetworkDiff(number, NetworkChangeKind.Identical, newNet!.Title, null, null));
                }
                else
                {
                    results.Add(new NetworkDiff(number, NetworkChangeKind.Changed, newNet!.Title, oldText, newText));
                }
            }
            else if (hasOld)
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Removed, oldNet!.Title,
                    IrSerializer.SerializeNetworkOnly(oldNet!), null));
            }
            else
            {
                results.Add(new NetworkDiff(number, NetworkChangeKind.Added, newNet!.Title,
                    null, IrSerializer.SerializeNetworkOnly(newNet!)));
            }
        }

        return results;
    }

    private static HeaderDiff DiffHeader(
        IrBlock oldBlock, IReadOnlyList<NetworkSidecar> oldSidecars,
        IrBlock newBlock, IReadOnlyList<NetworkSidecar> newSidecars)
    {
        var oldTitle = Normalize(oldBlock.Title);
        var newTitle = Normalize(newBlock.Title);
        var oldComment = Normalize(oldBlock.Comment);
        var newComment = Normalize(newBlock.Comment);

        var oldInterface = InterfaceCanonical(oldBlock, oldSidecars);
        var newInterface = InterfaceCanonical(newBlock, newSidecars);

        return new HeaderDiff(
            TitleChanged: !string.Equals(oldTitle, newTitle, StringComparison.Ordinal),
            TitleBefore: oldTitle,
            TitleAfter: newTitle,
            CommentChanged: !string.Equals(oldComment, newComment, StringComparison.Ordinal),
            CommentBefore: oldComment,
            CommentAfter: newComment,
            InterfaceChanged: !string.Equals(oldInterface, newInterface, StringComparison.Ordinal));
    }

    // The block's INTERFACE section as sidecar-free, UId-free text, extracted from the canonical
    // whole-block serialization. Everything from "SIDECAR" onward (volatile UIds) and every network
    // is dropped; RootUId/Number/etc. header scalars are dropped too by keeping only the INTERFACE
    // slice. Empty string when the block has no interface (SerializeBlock omits the section).
    private static string InterfaceCanonical(IrBlock block, IReadOnlyList<NetworkSidecar> sidecars)
    {
        var full = IrSerializer.SerializeBlock(block, sidecars);
        var preSidecar = full.Split("\nSIDECAR\n", 2)[0];
        var headerAndInterface = preSidecar.Split("\nNETWORK ", 2)[0];
        var idx = headerAndInterface.IndexOf("\nINTERFACE\n", StringComparison.Ordinal);
        return idx < 0 ? string.Empty : headerAndInterface[idx..];
    }

    private static string? Normalize(string? value) => string.IsNullOrEmpty(value) ? null : value;
}
