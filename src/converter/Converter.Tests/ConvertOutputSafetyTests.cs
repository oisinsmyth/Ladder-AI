using System;
using System.IO;
using System.Linq;
using Converter;
using Xunit;

namespace Converter.Tests;

// FI-71 and FI-72 — the two traps a fix wave walked into on a live job, both about where converter
// output goes and what it is allowed to guess.
public class ConvertOutputSafetyTests : IDisposable
{
    private readonly string _dir;

    public ConvertOutputSafetyTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"convert-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    // FI-72. Both converters write BESIDE their input by default, so converting X.xml writes X.ir —
    // right in the ordinary export-and-read-back loop, destructive when the .ir beside it is
    // hand-authored. It silently overwrote hand-edited IR for two separate agents.
    [Fact]
    public void OutDir_RedirectsOutputAwayFromTheInputsOwnDirectory()
    {
        var source = Path.Combine(_dir, "FB_X.xml");
        var elsewhere = Path.Combine(_dir, "scratch");

        Assert.Equal(Path.Combine(_dir, "FB_X.ir"), Program.ResolveOutPath(source, ".ir", outDir: null));
        Assert.Equal(Path.Combine(elsewhere, "FB_X.ir"), Program.ResolveOutPath(source, ".ir", elsewhere));
    }

    [Fact]
    public void OutDir_IsCreatedRatherThanRequiringItToExistFirst()
    {
        var target = Path.Combine(_dir, "made", "up", "path");
        Program.ResolveOutPath(Path.Combine(_dir, "FB_X.xml"), ".ir", target);

        Assert.True(Directory.Exists(target));
    }

    // Writing beside the input stays the DEFAULT on purpose: overwriting is the normal case in the
    // export-and-read-back loop, so a refusal would break every routine run and be switched off within
    // a day. What changed is that the event is visible when it happens.
    [Fact]
    public void DefaultBehaviourIsUnchanged_SoTheOrdinaryLoopStillWorks()
    {
        var source = Path.Combine(_dir, "DB_Y.xml");

        Assert.Equal(Path.ChangeExtension(source, ".ir"), Program.ResolveOutPath(source, ".ir", outDir: null));
        Assert.Equal(Path.ChangeExtension(source, ".xml"), Program.ResolveOutPath(source, ".xml", outDir: null));
    }

    // FI-71. Converting a block without --project cannot type a comparison against another DB's
    // member, so it falls back to a type inferred from the literal — which TIA rejects when the real
    // member is unsigned. FI-57 answered this with a warning; the same mistake then cost a third
    // import-and-compile cycle, on a file that was about to be imported. These tests pin the detector
    // that the to-xml refusal is built on.
    [Fact]
    public void BlindTypeDetector_NamesTheRootsItCannotSee()
    {
        var file = Path.Combine(_dir, "FB_Blind.ir");
        File.WriteAllText(file, "BLOCK FB FB_Blind\nNETWORK 1 \"T\"\n  COIL Out := DB_HmiCmd.Heartbeat <> 0\n");

        var roots = Program.WarnIfConvertingBlindToExternalTypes("to-xml", new[] { file }, projectDir: null);

        Assert.Contains("DB_HmiCmd", roots);
    }

    [Fact]
    public void BlindTypeDetector_IsSilentWhenTheReferencedRootIsInTheSameBatch()
    {
        var block = Path.Combine(_dir, "FB_Local.ir");
        var db = Path.Combine(_dir, "DB_Local.ir");
        File.WriteAllText(block, "BLOCK FB FB_Local\nNETWORK 1 \"T\"\n  COIL Out := DB_Local.Flag\n");
        File.WriteAllText(db, "DB DB_Local 1\n");

        Assert.Empty(Program.WarnIfConvertingBlindToExternalTypes("to-xml", new[] { block, db }, projectDir: null));
    }

    [Fact]
    public void BlindTypeDetector_IsSilentWhenAProjectWasSupplied()
    {
        var file = Path.Combine(_dir, "FB_Blind.ir");
        File.WriteAllText(file, "BLOCK FB FB_Blind\nNETWORK 1 \"T\"\n  COIL Out := DB_HmiCmd.Heartbeat <> 0\n");

        // --project is the whole remedy: with the other DB in scope the type is knowable.
        Assert.Empty(Program.WarnIfConvertingBlindToExternalTypes("to-xml", new[] { file }, projectDir: _dir));
    }

    // FI-73. An unrecognised --flag used to fall through and be treated as a FILENAME, so
    // `to-ir x.xml --out dir` on a build that predated --out converted x.xml BESIDE ITS INPUT — the
    // destructive act FI-72 had just fixed — and only then died on a file literally called "--out".
    // The damage happens before the crash, and any flag typo does the same thing.
    //
    // Verified against the real stale build before fixing: it wrote the file, then threw
    // FileNotFoundException on 'C:\...\--out'.
    [Fact]
    public void AnUnknownFlagIsRefusedRatherThanTreatedAsAFileName()
    {
        var refusal = RunConvertArgs(new[] { "to-ir", "x.xml", "--nosuchflag" });

        Assert.Equal(1, refusal.ExitCode);
        Assert.Contains("Unknown flag '--nosuchflag'", refusal.Stderr);
        // The message must say what the alternative WAS, or the reader learns nothing from it.
        Assert.Contains("treating it as a file name", refusal.Stderr);
    }

    [Fact]
    public void TheKnownFlagsAreStillAccepted()
    {
        // A guard that refuses correct input is worse than the defect it replaces, so each real flag
        // is named here — including the two added the same day, which is exactly how --out became a
        // filename on a build that had not caught up.
        foreach (var flag in new[] { "--project", "--out" })
        {
            var result = RunConvertArgs(new[] { "to-ir", "missing.xml", flag, "somewhere" });
            Assert.DoesNotContain("Unknown flag", result.Stderr);
        }

        Assert.DoesNotContain("Unknown flag", RunConvertArgs(new[] { "to-ir", "missing.xml", "--no-sidecar" }).Stderr);
        Assert.DoesNotContain("Unknown flag", RunConvertArgs(new[] { "to-xml", "missing.ir", "--synthesize" }).Stderr);
        Assert.DoesNotContain("Unknown flag", RunConvertArgs(new[] { "to-xml", "missing.ir", "--allow-blind-types" }).Stderr);
    }

    // Only ARGUMENT HANDLING is under test here, so a throw from the conversion itself (these inputs
    // name files that do not exist) counts as "the flag was accepted and parsing moved on" — which is
    // exactly the property being asserted. Swallowing it keeps the test about the flag rather than
    // requiring a valid export fixture per flag.
    private static (int ExitCode, string Stderr) RunConvertArgs(string[] args)
    {
        var stderr = new StringWriter();
        var previous = Console.Error;
        Console.SetError(stderr);
        try
        {
            return (Program.RunConvert(args), stderr.ToString());
        }
        catch (Exception)
        {
            return (-1, stderr.ToString());
        }
        finally
        {
            Console.SetError(previous);
        }
    }

    // A block referencing nothing external must not be caught by a check aimed at cross-DB typing —
    // a gate that fires on correct input gets disabled, and then it protects nothing.
    [Fact]
    public void BlindTypeDetector_IsSilentOnABlockWithNoExternalReferences()
    {
        var file = Path.Combine(_dir, "FB_SelfContained.ir");
        File.WriteAllText(file, "BLOCK FB FB_SelfContained\nNETWORK 1 \"T\"\n  COIL IO.Out := IO.In\n");

        Assert.Empty(Program.WarnIfConvertingBlindToExternalTypes("to-xml", new[] { file }, projectDir: null));
    }
}
