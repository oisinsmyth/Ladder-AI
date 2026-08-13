using System;
using System.IO;
using System.Linq;
using OpennessCli.Cli;
using Xunit;

namespace OpennessCli.Tests;

/// <summary>
/// *** THE RESTORE PATH, AGAINST FILES TIA ACTUALLY PRODUCED. ***
///
/// `import-all` is documented as "BULK RESTORE, the other half of export-all". On 2026-08-13 it was
/// measured against a complete restore point and planned <b>0 of 126 files</b>, rejecting every one.
/// Every restore point taken that week was unusable, and nobody found out, because <b>the restore
/// path is the one thing you only exercise when something has already gone wrong.</b>
///
/// **The mechanism was a deny-list where a search belonged.** `ReadObjectElement` (then
/// `ReadRootElement`) walked elements, skipped the two names it knew — `Document`, `Engineering` —
/// and returned the next one. Real exports carry a third header element, `DocumentInfo`, so it
/// returned `"DocumentInfo"`, which classifies as nothing.
///
/// **And the reason the existing tests all passed is the reason this file exists.** They build their
/// input by hand:
/// <code>
/// "&lt;Document&gt;\r\n  &lt;Engineering version=\"V20\" /&gt;\r\n  &lt;SW.Blocks.FB …
/// </code>
/// — a document TIA does not produce. The fixture agreed with the code about a shape neither had
/// checked against reality, so the pair was self-consistent and wrong. A test that cannot fail
/// because its input was written to match the implementation is not a test of anything.
///
/// So these tests read the <b>committed `simatic-ml/test-project001/` corpus</b>: genuine TIA
/// exports, de-identified, already in the repo, each carrying the `DocumentInfo` element that broke
/// the planner. Nothing here is hand-written, and that is the entire point. (`simatic-ml/` is
/// deliberately a committed corpus rather than a regenerable cache — see CLAUDE.md — so this cannot
/// silently lose its fixtures.)
/// </summary>
public class ImportAllRealExportTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "simatic-ml")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            "Could not find repo root (no 'simatic-ml' directory found above the test output).");
    }

    private static string[] RealExports()
    {
        var corpus = Path.Combine(RepoRoot(), "simatic-ml", "test-project001");
        var files = Directory.GetFiles(corpus, "*.xml", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        // Empty is not clean (FI-44). A corpus that has moved or been emptied must fail this suite
        // loudly rather than let every assertion below pass over nothing.
        Assert.True(files.Length > 0, $"No real exports found in {corpus} — this suite proves nothing without them.");
        return files;
    }

    /// <summary>
    /// THE TEST THAT WOULD HAVE CAUGHT IT. Not one hand-written string: real exports in, and a
    /// non-zero planned count that must equal the number of files offered.
    /// </summary>
    [Fact]
    public void EveryRealExport_IsPlanned_AndNothingIsRejected()
    {
        var files = RealExports();

        var plan = ImportAllPlanner.Build(files);

        Assert.Empty(plan.Rejections);
        Assert.Equal(files.Length, plan.Files.Count);
    }

    /// <summary>
    /// The specific header element that did it, named so a future reader knows what the corpus is
    /// carrying and does not "simplify" these fixtures back into hand-written ones.
    /// </summary>
    [Fact]
    public void TheRealExports_CarryTheDocumentInfoHeader_ThatBrokeThePlanner()
    {
        var withHeader = RealExports().Count(f => File.ReadAllText(f).IndexOf("<DocumentInfo>", StringComparison.Ordinal) >= 0);

        Assert.True(withHeader > 0, "The corpus no longer carries <DocumentInfo>; this suite has stopped testing the defect it exists for.");
    }

    /// <summary>
    /// Locating rather than skipping means the object element is found wherever the header grows.
    /// This is the property a longer skip-list would NOT have: it holds for a header element that
    /// did not exist when the code was written, which is exactly how `DocumentInfo` got through.
    /// </summary>
    [Fact]
    public void AnUnknownHeaderElement_DoesNotHideTheObject()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
        File.WriteAllText(
            path,
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n" +
            "<Document>\r\n" +
            "  <Engineering version=\"V20\" />\r\n" +
            "  <DocumentInfo><Created>x</Created></DocumentInfo>\r\n" +
            "  <SomeHeaderNobodyHasSeenYet><Nested /></SomeHeaderNobodyHasSeenYet>\r\n" +
            "  <SW.Blocks.FC ID=\"0\"><AttributeList><Name>FC_X</Name></AttributeList></SW.Blocks.FC>\r\n" +
            "</Document>\r\n");

        try
        {
            Assert.Equal("SW.Blocks.FC", ImportAllPlanner.ReadObjectElement(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// A document with no SW.* object is still a rejection, and the message NAMES what it did see.
    /// "No SW.* element" over a 3 MB file is not diagnosable on its own.
    /// </summary>
    [Fact]
    public void ADocumentWithNoObject_IsRejected_AndSaysWhatItSaw()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".xml");
        File.WriteAllText(path, "<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<Document>\r\n  <Engineering version=\"V20\" />\r\n  <DocumentInfo />\r\n</Document>\r\n");

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() => ImportAllPlanner.ReadObjectElement(path));
            Assert.Contains("DocumentInfo", ex.Message, StringComparison.Ordinal);
            Assert.Contains("Engineering", ex.Message, StringComparison.Ordinal);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The nested-object case, which is why "first SW.* in document order" is safe rather than
    /// merely convenient: a real FC export contains `SW.Blocks.CompileUnit` elements INSIDE the
    /// `SW.Blocks.FC`, and the outer one always comes first in document order. Asserted against the
    /// real corpus, so it cannot be argued from a hand-made file.
    /// </summary>
    [Fact]
    public void ARealBlockExport_ResolvesToTheOuterObject_NotANestedOne()
    {
        var blocks = RealExports()
            .Where(f => File.ReadAllText(f).IndexOf("SW.Blocks.CompileUnit", StringComparison.Ordinal) >= 0)
            .ToArray();

        Assert.True(blocks.Length > 0, "No corpus file contains a nested SW.Blocks.CompileUnit — this test is not exercising the nesting it names.");

        foreach (var file in blocks)
        {
            Assert.NotEqual("SW.Blocks.CompileUnit", ImportAllPlanner.ReadObjectElement(file));
        }
    }
}
