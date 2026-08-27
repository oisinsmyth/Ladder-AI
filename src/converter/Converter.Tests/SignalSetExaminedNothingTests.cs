using Converter;
using Converter.Ir;
using Converter.SignalSet;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 EMPTY IS NOT CLEAN — `signal-set`'s exit contract, tested at the CLI boundary because the exit
/// code is the only part of it a generator downstream ever reads.
///
/// <para>This is the class of bug this repo keeps being bitten by, and an EMITTER is not exempt from
/// it: `candidate-scan --fb &lt;a name in no block&gt;` scanned all 43 files, printed
/// <c>CANDIDATE SET SIZE: 0</c> and exited 0 — byte-identical to a real block with an unambiguous
/// answer (FI-44, 2026-08-14). `undriven-scan` then found a THIRD shape three weeks after the first
/// two were called complete, which is why the gate here keys on the ROW COUNT as well as on the scope
/// enum.</para>
///
/// <para>The unaffected case is tested as deliberately as the refused one: a guard that refuses
/// ordinary input is noise, and noise gets switched off.</para>
/// </summary>
public class SignalSetExaminedNothingTests : IDisposable
{
    private readonly string _dir;

    public SignalSetExaminedNothingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"signalset-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        File.WriteAllText(Path.Combine(_dir, "FB_Unit.ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FB", "FB_Unit", 1, "LAD", null, new[]
            {
                new IrNetwork(1, "act", new[]
                {
                    new CoilAssignment("Status", new Expr.TagRef("Cmd")),
                }),
            },
            StaticMembers: new[]
            {
                new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                new DbMember("Status", "Bool", Retain: false, StartValue: null),
            })));
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

    private string[] Args(params string[] extra) =>
        new[] { "--project", _dir, "--block", "FB_Unit" }.Concat(extra).ToArray();

    [Fact]
    public void RealBlockWithSignals_ExitsZero() =>
        Assert.Equal(0, Program.RunSignalSet(Args()));

    /// <summary>A typo, a not-yet-written block, or a wrong --project: none of them is a pass.</summary>
    [Fact]
    public void UnknownBlock_ExitsTwo() =>
        Assert.Equal(2, Program.RunSignalSet(new[] { "--project", _dir, "--block", "FB_NotHere" }));

    /// <summary>
    /// The second door, and the one a filter typo opens: `--type Word` on a block of Bools returns an
    /// empty document indistinguishable from a block that has no signals.
    /// </summary>
    [Fact]
    public void FiltersMatchedNothing_ExitsTwo() =>
        Assert.Equal(2, Program.RunSignalSet(Args("--type", "Nonesuch")));

    /// <summary>
    /// The gate keys on the ROW COUNT, not only on the scope enum — so a shape nobody has enumerated
    /// yet still cannot report a pass.
    /// </summary>
    [Fact]
    public void FiltersMatchedNothing_ScopeIsNotScanned() =>
        Assert.NotEqual(SignalSetScope.Scanned,
            SignalSetRunner.Run(_dir, "FB_Unit", "any", "Nonesuch", "any").Scope);

    /// <summary>
    /// A --json consumer must be able to tell "no signals" from "nothing examined" WITHOUT reading the
    /// exit code — the gap candidate-scan's JSON carried until 2026-08-14.
    /// </summary>
    [Fact]
    public void UnknownBlock_IsFlaggedInTheReportItself() =>
        Assert.True(SignalSetRunner.Run(_dir, "FB_NotHere", "any", null, "any").ExaminedNothing);

    /// <summary>
    /// The denominator survives the refusal: an absence claim over an unstated file count is not an
    /// absence claim at all.
    /// </summary>
    [Fact]
    public void UnknownBlock_StillStatesTheDenominator() =>
        Assert.Equal(1, SignalSetRunner.Run(_dir, "FB_NotHere", "any", null, "any").FilesScanned);

    /// <summary>
    /// A file the corpus cannot parse makes the set INCOMPLETE, and that gates rather than warns:
    /// the reader of this document is a binding generator that never sees a warning line.
    /// </summary>
    [Fact]
    public void UnparseableFileInTheCorpus_ExitsOnePartial()
    {
        File.WriteAllText(Path.Combine(_dir, "FB_Broken.ir"), "BLOCK FB FB_Broken\nnot ir at all\n");

        Assert.Equal(1, Program.RunSignalSet(Args()));
    }

    [Fact]
    public void CleanCorpus_IsNotReportedPartial() =>
        Assert.False(SignalSetRunner.Run(_dir, "FB_Unit", "any", null, "any").Partial);

    // --- invocation refusals: a malformed question, distinct from an empty answer -----------------

    [Fact]
    public void MissingBlock_IsAUsageRefusal() =>
        Assert.Equal(1, Program.RunSignalSet(new[] { "--project", _dir }));

    [Fact]
    public void UnknownDirectionValue_IsRefusedNotIgnored() =>
        Assert.Equal(1, Program.RunSignalSet(Args("--direction", "sideways")));

    [Fact]
    public void UnknownOriginValue_IsRefusedNotIgnored() =>
        Assert.Equal(1, Program.RunSignalSet(Args("--origin", "elsewhere")));

    [Fact]
    public void MissingProjectDirectory_IsARefusal() =>
        Assert.Equal(1, Program.RunSignalSet(
            new[] { "--project", Path.Combine(_dir, "nope"), "--block", "FB_Unit" }));
}
