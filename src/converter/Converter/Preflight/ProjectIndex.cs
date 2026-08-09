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

    // FI-65 component 1. Numbers and network slots are author-allocated in the IR (`NUMBER 50` is a
    // line an agent writes), so they are the two things two agents pick independently and collide on.
    // Recorded here rather than by a second scanner because this class already parses every block and
    // DB to get their names — a separate walk would be a second corpus dispatch, and SignalInventory's
    // own header states the invariant that the corpus is read one way "so the three can never disagree
    // about what a corpus contains".
    private readonly Dictionary<(string Kind, int Number), string> _numberOwners = new();
    private readonly Dictionary<string, IReadOnlyList<int>> _blockNetworks = new(StringComparer.Ordinal);

    public IReadOnlyList<string> Warnings => _warnings;

    // Keyed on (kind, number) because the number spaces are per-kind: FB50 and DB50 coexist, FB50 and
    // a second FB50 do not.
    public string? NumberOwner(string kind, int number) =>
        _numberOwners.TryGetValue((kind, number), out var name) ? name : null;

    public IReadOnlyList<int> NetworksOf(string blockName) =>
        _blockNetworks.TryGetValue(blockName, out var networks) ? networks : Array.Empty<int>();

    // FI-44 — "empty is not clean". A caller that validates against this index needs to distinguish
    // "checked the corpus, the resource is free" from "the corpus was empty, so everything looks free".
    // The second is not an answer, and treating it as one is how a check passes by examining nothing.
    public bool IndexedAnything =>
        _blockNames.Count + _dbNames.Count + _typeNames.Count + _tagNames.Count > 0;

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
                var db = DbIrParser.ParseDb(text);
                _dbNames.Add(db.Name);
                _numberOwners[("DB", db.Number)] = db.Name;
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
            _numberOwners[(block.Kind, block.Number)] = block.Name;
            _blockNetworks[block.Name] = block.Networks.Select(n => n.Number).ToList();
        }
        catch (Exception ex) when (ex is SimaticMlFormatException or UnsupportedConstructException or NonReducibleNetworkException or IrFormatException)
        {
            _warnings.Add($"{path}: not indexed ({ex.GetType().Name}: {ex.Message})");
        }
    }

    internal static bool HasSidecarSection(string text) => IrParser.HasSidecarSection(text);
}
