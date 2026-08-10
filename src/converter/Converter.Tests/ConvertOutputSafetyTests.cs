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
