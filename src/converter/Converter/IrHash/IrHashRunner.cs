using System.Security.Cryptography;
using System.Text;
using Converter.Ir;

namespace Converter.IrHash;

// Computes the FI-17 readable-IR hash for each input .ir block file. The hash is the same
// SHA-256-hex form Normalizer.Hash uses (stable across processes, unlike string.GetHashCode), taken
// over IrSerializer.SerializeBlockReadable — the no-SIDECAR canonical form. So the same block hashes
// identically whether its stored text carries a SIDECAR section or not, and any change to logic,
// interface, or comments changes the hash.
public static class IrHashRunner
{
    public static IrHashReport Run(IReadOnlyList<string> files)
    {
        var entries = new List<IrHashEntry>();
        foreach (var file in files)
        {
            entries.Add(HashFile(file));
        }

        return new IrHashReport(entries);
    }

    // Public so tests can hash an already-parsed block without a temp file round trip.
    public static string HashBlock(IrBlock block) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(IrSerializer.SerializeBlockReadable(block))));

    private static IrHashEntry HashFile(string file)
    {
        if (!File.Exists(file))
        {
            return new IrHashEntry(file, Hash: null, Error: "file not found");
        }

        string text;
        try
        {
            text = File.ReadAllText(file);
        }
        catch (IOException ex)
        {
            return new IrHashEntry(file, Hash: null, Error: $"read failed: {ex.Message}");
        }

        // ir-hash keys a block explanation; a DB/TYPE/TAGTABLE file has no block logic to explain, so
        // report a clear error rather than hashing something the sidecar convention never covers.
        if (!text.StartsWith("BLOCK ", StringComparison.Ordinal))
        {
            return new IrHashEntry(file, Hash: null,
                Error: "not a block file (ir-hash keys code blocks only; expected content starting with 'BLOCK ')");
        }

        try
        {
            var block = IrParser.HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            return new IrHashEntry(file, HashBlock(block), Error: null);
        }
        catch (IrFormatException ex)
        {
            return new IrHashEntry(file, Hash: null, Error: $"parse failed: {ex.Message}");
        }
    }
}
