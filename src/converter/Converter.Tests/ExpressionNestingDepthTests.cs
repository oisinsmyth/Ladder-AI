using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// Tooling-hammer campaign, 2026-08-14. The IR expression parser is recursive descent with no bound
/// on its own stack, and <c>.NET makes a StackOverflowException UNCATCHABLE</c> — no catch, no
/// finally, no exit code of ours. Measured: ~1,700 nested parentheses in one expression killed
/// <c>converter digest</c> outright (process exit 0xC00000FD, the runtime's own trace on stderr),
/// and with it every subcommand that parses IR — including <c>cross-check</c>, <c>drift-check</c>
/// and <c>preflight</c>, which otherwise degrade an <c>IrFormatException</c> to a per-file warning
/// and carry on. One pathological file ended the whole project-wide run, and a caller could not tell
/// it from a crash of any other kind.
///
/// The threshold was bisected: 1,500 levels parsed fine, 2,000 killed the process. The guard sits at
/// 500 — the deepest expression in any committed <c>.ir</c> is <b>3</b> levels.
/// </summary>
public class ExpressionNestingDepthTests
{
    private static string Block(string expr) =>
        "BLOCK FC FC_Nest\nROOTID 0\nNUMBER 9700\nLANGUAGE LAD\nTITLE \"nest\"\nCOMMENT \"c\"\n\n"
        + "INTERFACE\n  INPUT\n  OUTPUT\n  TEMP\n    A : Bool COMMENT \"a\"\n    B : Bool COMMENT \"b\"\n\n"
        + $"NETWORK 1 \"n\"\n  COMMENT \"c\"\n  COIL B := {expr}\n";

    private static string Nested(int depth)
    {
        var e = "A";
        for (var i = 0; i < depth; i++)
        {
            e = $"({e} OR A)";
        }

        return e;
    }

    // The did-not-run test. Without the guard this does not FAIL — it terminates the test host.
    [Fact]
    public void ParenNestingBeyondTheLimit_IsANamedRefusal_NotAProcessKill()
    {
        var ex = Assert.Throws<IrFormatException>(() => IrParser.ParseBlockWithoutSidecar(Block(Nested(2000))));

        Assert.Contains("nests deeper than", ex.Message);
        Assert.Contains("500", ex.Message);
    }

    // NOT recurses on its own path and must be bounded by the same counter.
    [Fact]
    public void NotNestingBeyondTheLimit_IsAlsoRefused()
    {
        var expr = string.Concat(Enumerable.Repeat("NOT ", 2000)) + "A";

        Assert.Throws<IrFormatException>(() => IrParser.ParseBlockWithoutSidecar(Block(expr)));
    }

    // The unaffected cases, tested as deliberately as the refused one. A limit that clips ordinary
    // logic is noise, and noise gets switched off — the real corpus's deepest expression is 3.
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(50)]
    [InlineData(400)]
    public void OrdinaryNesting_StillParses(int depth)
    {
        var block = IrParser.ParseBlockWithoutSidecar(Block(Nested(depth)));

        Assert.Single(block.Networks);
    }

    // Wide is not deep: a flat 5,000-term AND chain is iteration, not recursion, and must be
    // untouched by a DEPTH limit. Pins that the guard counts the right thing.
    [Fact]
    public void FlatChainOfManyTerms_IsNotDepth()
    {
        var expr = string.Join(" AND ", Enumerable.Repeat("A", 5000));

        var block = IrParser.ParseBlockWithoutSidecar(Block(expr));

        Assert.Single(block.Networks);
    }
}
