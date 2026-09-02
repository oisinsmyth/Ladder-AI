using Xunit;

namespace Converter.Tests;

/// <summary>
/// The CORPUS-LEVEL proof that FI-88's repair is a no-op on committed data.
///
/// <para>The repair teaches <c>SignalInventory</c> to open a member whose type is named rather than
/// inlined. Every UDT-typed interface member in <c>ir/test-project001</c> carries its members INLINED,
/// so branch 1 of <see cref="Converter.Ir.MemberExpansion"/> fires on all of them and the new branch
/// cannot execute — but "cannot execute" is a claim, and this is where it is checked rather than
/// asserted in a comment.</para>
///
/// <para><b>The denominator is asserted as well as the emptiness</b>, because an empty sweep and a
/// clean sweep produce the identical green: a wrong path, a renamed directory or a glob that matched
/// nothing would otherwise pass this test forever.</para>
/// </summary>
public class SignalInventoryTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string CorpusDir() => Path.Combine(RepoRoot(), "ir", "test-project001");

    [Fact]
    public void RealCorpus_YieldsNoOpaqueLeaf_SoTheGateCannotFireOnCommittedData()
    {
        var corpus = CorpusDir();
        var files = Directory.EnumerateFiles(corpus, "*.ir", SearchOption.TopDirectoryOnly).ToList();

        // The denominator. Empty is not clean: without this the assertion below passes on a corpus
        // directory that does not exist.
        Assert.True(files.Count >= 15, $"denominator: {files.Count} .ir file(s) swept from {corpus}");

        var inventory = Converter.SignalInventory.SignalInventory.Build(corpus);

        Assert.Empty(inventory.OpaqueLeaves.Select(o => $"{o.Path} : {o.Datatype} — {o.Reason}"));
    }

    /// <summary>
    /// The other half of the denominator: the sweep must actually have produced signals. An inventory
    /// that collected nothing has no opaque leaves either.
    /// </summary>
    [Fact]
    public void RealCorpus_SweepProducesLeaves_SoTheEmptyOpaqueSetIsEarned()
    {
        var inventory = Converter.SignalInventory.SignalInventory.Build(CorpusDir());

        Assert.True(inventory.Leaves.Count > 100, $"denominator: {inventory.Leaves.Count} leaf/leaves");
        Assert.Equal(inventory.FilesScanned, Directory.EnumerateFiles(CorpusDir(), "*.ir").Count());
    }

    /// <summary>
    /// The invariance proof, stated on the inventory itself: every leaf the corpus yields today is
    /// reached WITHOUT the cross-file descent, because nothing in it is a named type with no inlined
    /// body. If this ever goes red, the descent has started carrying real data and both this file and
    /// <c>InterfaceCheckTests</c>'s corpus assertion want re-reading against it.
    /// </summary>
    [Fact]
    public void RealCorpus_NoLeafIsAnUnexpandedNamedType()
    {
        var inventory = Converter.SignalInventory.SignalInventory.Build(CorpusDir());

        var quotedTypeLeaves = inventory.Leaves
            .Where(l => l.Type.TrimStart().StartsWith("\"", StringComparison.Ordinal))
            .Select(l => $"{l.Path} : {l.Type}")
            .ToList();

        Assert.Empty(quotedTypeLeaves);
    }
}
