using System.Diagnostics;
using System.Text.Json;
using Harness.Gate;
using Harness.Map;

namespace Harness.Results.Tests;

/// <summary>
/// THE SEAM BETWEEN <c>converter harness-binding</c> AND THIS SOLUTION'S BINDING SCHEMA.
///
/// <para>The scaffold is emitted by the CONVERTER — that is where the usage graph, the typed signal
/// inventory and the served-area derivation live, and putting a second IR reader in this solution would
/// give the project two answers to "does this block write this member" that could disagree. The cost of
/// that choice is that the converter writes a wire format whose C# schema lives HERE, in
/// <see cref="BindingDocument"/>, and nothing but this file connects the two. <b>A drift between them
/// would not fail a build; it would produce a document that loads with fields silently dropped</b> —
/// which is the exact defect gate 0b was widened to the binding to catch (a misspelt <c>specName</c>,
/// 1 of 17 signals resolving, three checks failing in one run).</para>
///
/// <para>So this suite runs the real binary over the real corpus and parses the result with the real
/// reader. <b>Absent is a FAILURE, not a skip</b>, following
/// <c>Harness.Map.Tests.CopyLayerConverterRoundTripTests</c>: an optional check is one that stops
/// running, and the defects this file exists for get through when nothing outside a component can
/// fail.</para>
/// </summary>
public class ScaffoldedBindingParityTests
{
    // The committed hand-typed binding, and the invocation that scaffolds the same slot. Both are
    // named here rather than parameterised: this suite is a statement about ONE pair.
    private const string Slot = "HBA";
    private const string Stimulus = "FB_HopperBlockageStim";

    private static readonly string[] Observed = { "FB_HopperBlockageMonitor", "FB_HarnessViolationLatch" };

    private static readonly string[] Scopes =
    {
        "iDB_HopperBlockageStim.Stim.",
        "iDB_HopperBlockageMonitor.IO.",
        "iDB_HarnessViolationLatch.",
    };

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "ir", "test-project001")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "ir/test-project001 was not found by walking up from " + AppContext.BaseDirectory
                + ". *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** Without the corpus there is nothing "
                + "connecting the converter's wire format to this solution's schema.");
        }
    }

    private static string ConverterExe
    {
        get
        {
            foreach (var configuration in new[] { "Release", "Debug" })
            {
                var candidate = Path.Combine(RepoRoot, "src", "converter", "Converter", "bin",
                    configuration, "net8.0", "converter.exe");

                if (File.Exists(candidate))
                    return candidate;
            }

            throw new InvalidOperationException(
                "converter.exe was not found under " + RepoRoot
                + "/src/converter/Converter/bin/{Release,Debug}/net8.0/. *** THIS IS A FAILURE, NOT A "
                + "REASON TO SKIP. *** Build it with: dotnet build -c Release src/converter/converter.sln "
                + "(free and safe at any time - the converter never touches Portal).");
        }
    }

    /// <summary>
    /// Runs the scaffold and returns the emitted document. Redirected, never piped - safe here because
    /// the converter is a pure in-process file transformer with no child process, so it carries none of
    /// openness-cli's inherited-handle hazard.
    /// </summary>
    private static string Scaffold()
    {
        var emitted = Path.Combine(Path.GetTempPath(), $"scaffold-{Guid.NewGuid():N}.json");

        var info = new ProcessStartInfo(ConverterExe)
        {
            WorkingDirectory = RepoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var arg in new[]
                 {
                     "harness-binding",
                     "--project", Path.Combine("ir", "test-project001"),
                     "--stimulus", Stimulus,
                     "--slot", Slot,
                     "--emit", emitted,
                 })
        {
            info.ArgumentList.Add(arg);
        }

        foreach (var block in Observed)
        {
            info.ArgumentList.Add("--observe");
            info.ArgumentList.Add(block);
        }

        foreach (var scope in Scopes)
        {
            info.ArgumentList.Add("--scope");
            info.ArgumentList.Add(scope);
        }

        using var process = Process.Start(info)
                            ?? throw new InvalidOperationException("converter.exe did not start.");

        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0,
            $"harness-binding exited {process.ExitCode} over the committed corpus. Exit 1 is a REFUSAL or a "
            + $"PARTIAL corpus and exit 2 is NOTHING EXAMINED - neither is a pass.{Environment.NewLine}{output}");

        try
        {
            return File.ReadAllText(emitted);
        }
        finally
        {
            File.Delete(emitted);
        }
    }

    private static BindingDocument Committed() =>
        BindingDocument.Read(File.ReadAllText(Path.Combine(
            RepoRoot, "gen", "test-project001", "hopper-blockage-alarm", "harness-binding.json")));

    // --- the seam itself --------------------------------------------------------------------------

    [Fact]
    public void TheEmittedDocument_ParsesWithThisSolutionsOwnReader()
    {
        var document = BindingDocument.Read(Scaffold());
        Assert.NotNull(document.Slots);
        Assert.Single(document.Slots!);
        Assert.Equal(Slot, document.Slots![0].SlotId);
    }

    /// <summary>
    /// The drift check with teeth: every row the scaffold wrote has to arrive with a TYPE the schema
    /// recognises. <c>MirrorValueType.Unstated</c> is what a misspelt or renamed <c>type</c> key
    /// deserialises to, and it is refused downstream by name — so a silent wire-format divergence
    /// shows up here as Unstated rather than as a green.
    /// </summary>
    [Fact]
    public void EveryScaffoldedRow_ArrivesWithATypeTheSchemaRecognises()
    {
        var slot = BindingDocument.Read(Scaffold()).Slots![0];
        var rows = (slot.VectorTargets ?? new()).Concat(slot.ResultSources ?? new()).ToList();

        Assert.NotEmpty(rows);

        foreach (var row in rows)
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Tag), "a scaffolded row carries no tag.");
            Assert.NotEqual(MirrorValueType.Unstated, row.Type);
        }
    }

    /// <summary>
    /// 🔴 THE HOLES FAIL CLOSED. <c>unresolvedHoles</c> is deliberately NOT underscore-prefixed, so
    /// <see cref="SubmissionDocument.Split"/> puts it in the UNKNOWN half rather than the annotation
    /// half — and gate 0b refuses unknown fields. An unfinished scaffold is therefore unrunnable, not
    /// merely commented. <i>A warning is not a gate.</i>
    /// </summary>
    [Fact]
    public void UnresolvedHoles_LandsInGate0bsUnknownHalfAndNotItsAnnotationHalf()
    {
        var (unknown, annotations) = BindingDocument.Read(Scaffold()).AllExtraFieldPaths();

        Assert.Contains("binding.unresolvedHoles", unknown);
        Assert.DoesNotContain("binding.unresolvedHoles", annotations);
    }

    /// <summary>The provenance the scaffold writes about itself IS an annotation, and must not gate.</summary>
    [Fact]
    public void TheScaffoldsOwnProvenance_IsAnAnnotationAndDoesNotGate()
    {
        var (unknown, annotations) = BindingDocument.Read(Scaffold()).AllExtraFieldPaths();

        Assert.Contains("binding._generatedBy", annotations);
        Assert.DoesNotContain("binding._generatedBy", unknown);
    }

    // --- parity with the hand-typed document ------------------------------------------------------

    /// <summary>
    /// 🔴 THE PROPERTY THAT MAKES THE SCAFFOLD SAFE TO START FROM: it never MISSES a signal the
    /// hand-typed binding carries. It offers more than the deliverable needs — deleting a row a human
    /// did not want is safe, and silently lacking one they did is not — so this asserts recall and
    /// deliberately does not assert the converse.
    /// </summary>
    [Fact]
    public void EverySignalTheHandTypedBindingCarries_IsAlsoDerived()
    {
        var derived = Tags(BindingDocument.Read(Scaffold()));
        var committed = Tags(Committed());

        Assert.NotEmpty(committed);

        var missed = committed.Except(derived, StringComparer.Ordinal).OrderBy(t => t, StringComparer.Ordinal).ToList();

        Assert.True(missed.Count == 0,
            $"the scaffold missed {missed.Count} of {committed.Count} signal(s) the committed binding "
            + $"carries: {string.Join(", ", missed)}. A missing row reads exactly like a block that does "
            + "not have that signal.");
    }

    /// <summary>
    /// A misclassification is worse than an omission: a signal the harness DRIVES that the deliverable
    /// only OBSERVES would be written to on a live block. Over the rows both documents carry, the two
    /// must agree on which side of the wire each sits.
    /// </summary>
    [Fact]
    public void NoSignalIsDrivenByOneDocumentAndOnlyObservedByTheOther()
    {
        var derived = Sides(BindingDocument.Read(Scaffold()));
        var committed = Sides(Committed());
        var shared = committed.Keys.Intersect(derived.Keys, StringComparer.Ordinal).ToList();

        Assert.NotEmpty(shared);

        var crossed = shared.Where(t => committed[t] != derived[t]).ToList();

        Assert.True(crossed.Count == 0,
            $"{crossed.Count} of {shared.Count} shared signal(s) sit on opposite sides: "
            + string.Join(", ", crossed));
    }

    /// <summary>
    /// <c>latchedBy</c> is derivable, and the committed binding's own note records a human deriving it
    /// by hand — "the latch provenance was in the IR all along ... a whole-corpus search finds no other
    /// writer". Both halves are asserted: where the deliverable names a latch the scaffold must name the
    /// same block, and where the deliverable claims NO latch the scaffold must not invent one.
    /// </summary>
    [Fact]
    public void LatchProvenance_AgreesWithTheHandTypedBindingInBothDirections()
    {
        var derived = Rows(BindingDocument.Read(Scaffold()));
        var committed = Rows(Committed());
        var shared = committed.Keys.Intersect(derived.Keys, StringComparer.Ordinal).ToList();

        Assert.NotEmpty(shared);

        var disagreements = shared
            .Where(t => committed[t].LatchedBy != derived[t].LatchedBy)
            .Select(t => $"{t}: committed={committed[t].LatchedBy ?? "(none)"} derived={derived[t].LatchedBy ?? "(none)"}")
            .ToList();

        Assert.True(disagreements.Count == 0,
            $"over {shared.Count} shared signal(s): {string.Join("; ", disagreements)}");
    }

    /// <summary>
    /// <c>baseByte</c> and <c>declaredRegisters</c> come from <c>converter served-area</c>, which reads
    /// the window off the MB_SERVER call AND its sidecar and refuses on disagreement. They must match
    /// the numbers the deliverable states — and this is the one place the scaffold writes a value the
    /// hand-typed document also carries, so a divergence here means one of the two is stale.
    /// </summary>
    [Fact]
    public void TheServedWindow_MatchesTheHandTypedBinding()
    {
        var derived = BindingDocument.Read(Scaffold());
        var committed = Committed();

        Assert.Equal(committed.BaseByte, derived.BaseByte);
        Assert.Equal(committed.DeclaredRegisters, derived.DeclaredRegisters);
    }

    /// <summary>
    /// The four claims are NEVER written. Asserted against the raw JSON rather than the parsed
    /// document, because an absent key and a null one parse identically and only one of them is what
    /// the scaffold emitted.
    /// </summary>
    [Theory]
    [InlineData("specName")]
    [InlineData("encoding")]
    [InlineData("inertRest")]
    [InlineData("startCondition")]
    public void NoClaimAboutThePlantOrASpecification_AppearsAsASchemaKey(string field)
    {
        using var document = JsonDocument.Parse(Scaffold());
        var slot = document.RootElement.GetProperty("slots")[0];

        Assert.False(slot.TryGetProperty(field, out _));

        foreach (var list in new[] { "vectorTargets", "resultSources" })
        {
            foreach (var row in slot.GetProperty(list).EnumerateArray())
                Assert.False(row.TryGetProperty(field, out _));
        }
    }

    private static Dictionary<string, MirroredSignalDocument> Rows(BindingDocument document)
    {
        var rows = new Dictionary<string, MirroredSignalDocument>(StringComparer.Ordinal);

        foreach (var slot in document.Slots ?? new())
        {
            foreach (var row in (slot.VectorTargets ?? new()).Concat(slot.ResultSources ?? new()))
            {
                if (row.Tag is not null)
                    rows[row.Tag] = row;
            }
        }

        return rows;
    }

    private static HashSet<string> Tags(BindingDocument document) =>
        Rows(document).Keys.ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> Sides(BindingDocument document)
    {
        var sides = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var slot in document.Slots ?? new())
        {
            foreach (var row in slot.VectorTargets ?? new())
            {
                if (row.Tag is not null)
                    sides[row.Tag] = "driven";
            }

            foreach (var row in slot.ResultSources ?? new())
            {
                if (row.Tag is not null)
                    sides[row.Tag] = "observed";
            }
        }

        return sides;
    }
}
