namespace Harness.Cleanup.Tests;

/// <summary>
/// A throwaway directory holding a real IR corpus and real input files.
///
/// <para>Real files rather than an injected filesystem, deliberately: the corpus walk, the
/// <c>TopDirectoryOnly</c> rule and the "named a path that does not exist" refusal are all properties
/// of the filesystem, and a fake would let all three pass while the shipped tool did something else.
/// The seam a fake would add is exactly the seam that can silently stop matching production.</para>
/// </summary>
public sealed class TempCorpus : IDisposable
{
    public TempCorpus()
    {
        Root = Path.Combine(Path.GetTempPath(), "harness-cleanup-tests", Guid.NewGuid().ToString("N"));
        Ir = Path.Combine(Root, "ir");
        Directory.CreateDirectory(Ir);
    }

    public string Root { get; }

    public string Ir { get; }

    /// <summary>An FB with a number and no interesting body.</summary>
    public TempCorpus Fb(string name, int number)
    {
        File.WriteAllText(Path.Combine(Ir, name + ".ir"), $"BLOCK FB {name}\nROOTID 0\nNUMBER {number}\nLANGUAGE LAD\n");
        return this;
    }

    public TempCorpus Fc(string name, int number)
    {
        File.WriteAllText(Path.Combine(Ir, name + ".ir"), $"BLOCK FC {name}\nROOTID 0\nNUMBER {number}\nLANGUAGE LAD\n");
        return this;
    }

    public TempCorpus InstanceDb(string name, int number, string instanceOf)
    {
        File.WriteAllText(Path.Combine(Ir, name + ".ir"), $"DB {name}\n  ROOTID 0\n  NUMBER {number}\n  INSTANCEOF {instanceOf}\n");
        return this;
    }

    public TempCorpus GlobalDb(string name, int number)
    {
        File.WriteAllText(Path.Combine(Ir, name + ".ir"), $"DB {name}\n  ROOTID 0\n  NUMBER {number}\n  MEMBERS\n");
        return this;
    }

    public TempCorpus Type(string name)
    {
        File.WriteAllText(Path.Combine(Ir, name + ".ir"), $"TYPE {name}\n  ROOTID 0\n  MEMBERS\n");
        return this;
    }

    /// <summary>The declared name may differ from the basename — <c>Default tag table</c> does.</summary>
    public TempCorpus TagTable(string declaredName, string baseName)
    {
        File.WriteAllText(Path.Combine(Ir, baseName + ".ir"), $"TAGTABLE {declaredName}\n  ROOTID 0\n  TAGS\n");
        return this;
    }

    public string File_(string name, string content)
    {
        var path = Path.Combine(Root, name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    /// <summary>A valid, complete drain report declaring nothing outstanding.</summary>
    public string DrainedReport(string name = "drain.txt") => File_(name,
        "format=1\ncomputed-by=the tests\ncomputed-at=2026-08-17T00:00:00Z\nin-flight=0\nend\n");

    /// <summary>A minimal but structurally valid cross-check document.</summary>
    public string CrossCheck(string name, string siblingRefsJson) => File_(name,
        $"{{\"multiWriters\":[],\"deadMembers\":[],\"ioBoundary\":[],\"siblingRefs\":{siblingRefsJson},\"warnings\":[],\"soleWriters\":[]}}");

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A leftover temp directory is not a test failure.
        }
    }
}
