using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Preflight;

// Name index over a project's exported IR directory (ir/<project>/), plus the batch under
// pre-flight itself (so a new DB and the block referencing it can be checked together before
// either exists in the project). Same .ir prefix dispatch as ReviewRunner/DigestBuilder.
public sealed class ProjectIndex
{
    private readonly HashSet<string> _tagNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dbNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _typeNames = new(StringComparer.Ordinal);
    private readonly HashSet<string> _blockNames = new(StringComparer.Ordinal);
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Warnings => _warnings;

    public static ProjectIndex Build(string projectDir, IReadOnlyList<string> batchPaths)
    {
        var index = new ProjectIndex();

        foreach (var path in Directory.EnumerateFiles(projectDir, "*.ir", SearchOption.TopDirectoryOnly))
        {
            index.AddFile(path);
        }

        // The batch itself counts: a new block plus its new DB/tag table are pre-flighted as a
        // set, the same way they'd be imported as a set.
        foreach (var path in batchPaths)
        {
            index.AddFile(path);
        }

        return index;
    }

    public bool ResolvesAsTagRoot(string root) =>
        _tagNames.Contains(root) || _dbNames.Contains(root);

    public bool ResolvesAsBlock(string name) => _blockNames.Contains(name);

    public bool ResolvesAsType(string name) => _typeNames.Contains(name);

    private void AddFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);

            if (text.StartsWith("DB ", StringComparison.Ordinal))
            {
                _dbNames.Add(DbIrParser.ParseDb(text).Name);
                return;
            }

            if (text.StartsWith("TYPE ", StringComparison.Ordinal))
            {
                _typeNames.Add(TypeIrParser.ParseType(text).Name);
                return;
            }

            if (text.StartsWith("TAGTABLE ", StringComparison.Ordinal))
            {
                foreach (var tag in TagTableIrParser.ParseTagTable(text).Tags)
                {
                    _tagNames.Add(tag.Name);
                }

                return;
            }

            var block = HasSidecarSection(text)
                ? IrParser.ParseBlock(text).Block
                : IrParser.ParseBlockWithoutSidecar(text);
            _blockNames.Add(block.Name);
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
        {
            _warnings.Add($"{path}: not indexed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    internal static bool HasSidecarSection(string text) =>
        text.Replace("\r\n", "\n").Split('\n').Any(line => line == "SIDECAR");
}
