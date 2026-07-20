namespace Converter.IrHash;

// FI-17 (docs/16-future-ideas.md): a stable content hash of a block's READABLE logic — the key an
// explanation sidecar (docs/notes/explanation-sidecars.md) is derived-from and validated against
// (hash-on-read invalidation, ADR-0005). The hash is over IrSerializer.SerializeBlockReadable, so it
// invalidates on any logic/interface/comment change but is IMMUNE to SIDECAR/UId churn — the right
// key for "has this block's meaning changed since I last explained it?". One record set, two
// renderers, like Digest/TagStatus/ReuseScan.

// One input file and either its hash or the parse/read error that stopped it.
public sealed record IrHashEntry(string File, string? Hash, string? Error)
{
    public bool IsError => Error is not null;
}

public sealed record IrHashReport(IReadOnlyList<IrHashEntry> Entries)
{
    // Any file that failed to hash → non-zero exit (missing file / parse failure / non-block input).
    public bool HasErrors => Entries.Any(e => e.IsError);
}
