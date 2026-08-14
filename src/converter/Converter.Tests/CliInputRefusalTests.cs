using Converter;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Tooling-hammer campaign, 2026-08-14. Two defects found by feeding the check tools inputs a caller
/// produces by accident rather than inputs a fixture produces on purpose.
///
/// 1. A MISSING INPUT FILE was an unhandled <c>FileNotFoundException</c> in <c>review</c>,
///    <c>digest</c> and <c>preflight</c> — a .NET stack trace and process exit 0xE0434352 (bash
///    renders that 127, conventionally "command not found"). A crash is loud without being NAMED,
///    and a caller cannot tell it from a refusal. <c>diff</c> and <c>ir-hash</c> already refused the
///    same input in one line with exit 1, so the correct behaviour existed next door in the same
///    binary.
///
/// 2. A TAG NAME CARRYING A CONTROL CHARACTER was CLASSIFIED rather than refused by <c>tagstatus</c>,
///    and the verdict it produced — <c>MEMBER-NOT-FOUND</c> — is the one this tool exists to raise
///    the alarm on. On a terminal the rendered line is byte-identical to the report for a genuinely
///    invented member, because the CR is swallowed by the renderer. This repo's tracked text is
///    CRLF, so piping a name list through <c>tr '\n' ' '</c> produces exactly this on every name but
///    the last: it turned 312 correct paths out of the real corpus into 168 MEMBER-NOT-FOUND and 143
///    PROPOSED, with nothing in the output saying an input was malformed.
///
/// Both are the "did-not-run" shape the working agreement asks for explicitly: the case each guard
/// exists for gets tested, the case where the guard itself is absent does not. These are that test.
/// </summary>
public class CliInputRefusalTests : IDisposable
{
    private readonly string _dir;
    private readonly string _block;

    public CliInputRefusalTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cli-refuse-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        _block = Path.Combine(_dir, "FB_Ok.ir");
        File.WriteAllText(_block,
            "BLOCK FB FB_Ok\nROOTID 0\nNUMBER 9500\nLANGUAGE LAD\nTITLE \"ok\"\nCOMMENT \"a block that parses\"\n\n"
            + "INTERFACE\n  INPUT\n  OUTPUT\n  STATIC\n    A : Bool COMMENT \"a\"\n    B : Bool COMMENT \"b\"\n\n"
            + "NETWORK 1 \"n\"\n  COMMENT \"c\"\n  COIL B := A\n");

        File.WriteAllText(Path.Combine(_dir, "DB_Real.ir"),
            "DB DB_Real\n  ROOTID 0\n  NUMBER 9501\n  COMMENT \"c\"\n  MEMBERS\n    Present : Bool\n");
    }

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Missing => Path.Combine(_dir, "does-not-exist.ir");

    // --- 1. the missing input file -------------------------------------------------------------

    [Theory]
    [InlineData("review")]
    [InlineData("digest")]
    [InlineData("preflight")]
    public void MissingInputFile_IsANamedRefusal_NotACrash(string command)
    {
        var args = command switch
        {
            "preflight" => new[] { Missing, "--project", _dir },
            _ => new[] { Missing },
        };

        // The assertion that matters is that this RETURNS AT ALL. Before the fix each of these threw
        // out of Main; xunit would report the exception rather than an exit code.
        var exit = command switch
        {
            "review" => Program.RunReview(args),
            "digest" => Program.RunDigest(args),
            _ => Program.RunPreflight(args),
        };

        Assert.Equal(1, exit);
    }

    // The unaffected case, tested as deliberately as the refused one — a guard that refuses ordinary
    // input is noise, and noise gets switched off.
    [Theory]
    [InlineData("review")]
    [InlineData("digest")]
    [InlineData("preflight")]
    public void PresentInputFile_IsStillProcessed(string command)
    {
        var args = command switch
        {
            "preflight" => new[] { _block, "--project", _dir },
            _ => new[] { _block },
        };

        var exit = command switch
        {
            "review" => Program.RunReview(args),
            "digest" => Program.RunDigest(args),
            _ => Program.RunPreflight(args),
        };

        // review may legitimately find convention defects in this minimal block; what must NOT
        // happen is the not-found refusal path, which returns before any analysis. digest and
        // preflight are clean on it.
        Assert.InRange(exit, 0, 1);
        if (command != "review")
        {
            Assert.Equal(0, exit);
        }
    }

    // A batch where only ONE file is missing must still refuse: a partial batch silently analysing
    // the files that happened to exist is the same laundering one level down.
    [Fact]
    public void OneMissingFileInABatch_RefusesTheWholeBatch()
    {
        Assert.Equal(1, Program.RunDigest(new[] { _block, Missing }));
    }

    // --- 2. the malformed tag name -------------------------------------------------------------

    [Fact]
    public void CleanName_IsClassified()
    {
        Assert.Equal(0, Program.RunTagStatus(new[] { "DB_Real.Present", "--project", _dir }));
    }

    // The defect. Exit 2 = nothing was classified: neither a clean pass nor a hard-rule-3 finding.
    [Theory]
    [InlineData("DB_Real.Present\r")]
    [InlineData("DB_Real\r.Present")]
    [InlineData("DB_Real.Present ")]
    [InlineData(" DB_Real.Present")]
    [InlineData("DB_Real.Present\t")]
    public void MalformedName_IsRefused_NotClassified(string name)
    {
        Assert.Equal(2, Program.RunTagStatus(new[] { name, "--project", _dir }));
    }

    // A malformed name must not be able to hide behind clean ones — the CRLF-list case produces
    // exactly this shape, one clean name (the last) among many malformed ones.
    [Fact]
    public void OneMalformedNameAmongCleanOnes_RefusesTheWholeRun()
    {
        Assert.Equal(2, Program.RunTagStatus(
            new[] { "DB_Real.Present", "DB_Real.Present\r", "DB_Real.Present", "--project", _dir }));
    }

    // And a genuinely invented member must still gate at 1, so the two verdicts stay distinguishable.
    [Fact]
    public void InventedMember_StillGatesAtOne_NotAtTheRefusalCode()
    {
        Assert.Equal(1, Program.RunTagStatus(new[] { "DB_Real.NotAMember", "--project", _dir }));
    }
}
