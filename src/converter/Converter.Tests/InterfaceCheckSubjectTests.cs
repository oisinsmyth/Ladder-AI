using Converter.InterfaceCheck;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 2026-08-18 — <b><c>interface-check</c> COULD PRODUCE A FALSE ACCUSATION AGAINST A BLOCK.</b>
///
/// <para><c>--requires-file</c> scraped every <c>response_signal:</c> out of ONE enumeration and never
/// read that file's own declared <c>subject:</c>, nor compared it to <c>--block</c>. Two enumerations
/// now exist carrying 28 and 15 distinct response signals with an overlap of 2, so aiming a block at
/// the wrong one demands ~26 signals that cannot be present: ~26 MISSING and <c>exit 1</c>, which in
/// this tool's contract is a <b>FAIL AGAINST THE BLOCK</b>.</para>
///
/// <para>WHAT MADE IT CREDIBLE RATHER THAN OBVIOUSLY WRONG: on this corpus's house style (C-132) an
/// FB's INPUT and OUTPUT are both empty and the whole interface is one STATIC UDT, so "nearly every
/// signal missing" is a shape the runner's own documentation predicts as a GENUINE output. Nothing
/// distinguished a wrong-file run from a catastrophic block defect — the provenance line named the
/// path, which is exactly the thing that had been mis-typed.</para>
///
/// <para>THE OUTCOME IS 2, NOT 1. A wrong enumeration is an unjudgeable INPUT, not a defective block.</para>
/// </summary>
public class InterfaceCheckSubjectTests : IDisposable
{
    private readonly string _dir;

    public InterfaceCheckSubjectTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"ifc-subject-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // A block in the C-132 house style: INPUT and OUTPUT empty, one STATIC interface struct.
        File.WriteAllText(Path.Combine(_dir, "FB_Damper.ir"), IrSerializer.SerializeBlockReadable(
            new IrBlock("0", "FB", "FB_Damper", 1, "LAD", null,
                new[]
                {
                    new IrNetwork(1, "act", new[] { new CoilAssignment("IO.Out", new Expr.TagRef("IO.Cmd")) }),
                },
                StaticMembers: new[]
                {
                    new DbMember("IO", "Struct", false, null, NestedMembers: new[]
                    {
                        new DbMember("Cmd", "Bool", Retain: false, StartValue: null),
                        new DbMember("Out", "Bool", Retain: false, StartValue: null),
                        new DbMember("DamperJammedAlarm", "Bool", Retain: false, StartValue: null),
                    }),
                })));
    }

    private string WriteEnumeration(string fileName, string? subject, params string[] signals)
    {
        var path = Path.Combine(_dir, fileName);
        var lines = new List<string> { "enumerator: assertion-enumerator" };
        if (subject is not null)
        {
            lines.Add("subject: >-");
            lines.Add("  " + subject);
        }

        lines.Add("clauses:");
        foreach (var signal in signals)
        {
            lines.Add("  - assertion:");
            lines.Add($"      response_signal: {signal}");
            lines.Add("      subject: \"a per-assertion subject, indented, which is NOT the document's\"");
        }

        File.WriteAllLines(path, lines);
        return path;
    }

    // ---------------------------------------------------------------------------------------------
    // The parser.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Parse_ReadsAFoldedBlockScalarAndIgnoresThePerAssertionSubject()
    {
        var path = WriteEnumeration("enum-damper.yaml", "DAMPER BEHAVIOUR. Every obligation whose response is a damper.", "DamperJammedAlarm");

        var subject = EnumerationSubject.Parse(File.ReadAllLines(path));

        Assert.NotNull(subject);
        Assert.StartsWith("DAMPER BEHAVIOUR.", subject);
        Assert.DoesNotContain("per-assertion", subject);
    }

    [Fact]
    public void Parse_ReadsAPlainScalar()
    {
        var path = Path.Combine(_dir, "plain.yaml");
        File.WriteAllLines(path, new[] { "enumerator: x", "subject: DAMPER BEHAVIOUR", "clauses:" });

        Assert.Equal("DAMPER BEHAVIOUR", EnumerationSubject.Parse(File.ReadAllLines(path)));
    }

    [Fact]
    public void Parse_ReturnsNullWhenTheFileDeclaresNoSubject()
    {
        var path = WriteEnumeration("no-subject.yaml", subject: null, "DamperJammedAlarm");

        Assert.Null(EnumerationSubject.Parse(File.ReadAllLines(path)));
    }

    [Theory]
    [InlineData("DAMPER BEHAVIOUR. Every obligation whose response is a damper.", "DAMPER BEHAVIOUR", true)]
    [InlineData("DAMPER  BEHAVIOUR", "damper behaviour", true)]
    [InlineData("DAMPER BEHAVIOUR", "CONVEYOR BEHAVIOUR", false)]
    [InlineData("", "DAMPER BEHAVIOUR", false)]
    public void Agrees_IsContainmentEitherWayCaseAndWhitespaceInsensitive(string declared, string asserted, bool expected) =>
        Assert.Equal(expected, EnumerationSubject.Agrees(declared, asserted));

    // ---------------------------------------------------------------------------------------------
    // The gate, at the CLI boundary where it lives.
    // ---------------------------------------------------------------------------------------------

    private int Run(params string[] args) => Program.RunInterfaceCheck(args);

    [Fact]
    public void WrongEnumerationWithoutSubject_StillProducesTheFalseAccusation_AndThatIsWhyTheFlagExists()
    {
        // The unguarded path is unchanged BY DESIGN — --subject is opt-in, and this test records what
        // that costs, so nobody mistakes the reported provenance line for a gate.
        var wrong = WriteEnumeration("enum-conveyor.yaml", "CONVEYOR BEHAVIOUR", "BeltSlipAlarm", "BeltTornAlarm");

        Assert.Equal(1, Run("--project", _dir, "--block", "FB_Damper", "--requires-file", wrong));
    }

    [Fact]
    public void WrongEnumerationWithSubject_IsNotCheckedRatherThanAFailAgainstTheBlock()
    {
        var wrong = WriteEnumeration("enum-conveyor.yaml", "CONVEYOR BEHAVIOUR", "BeltSlipAlarm", "BeltTornAlarm");

        var exit = Run("--project", _dir, "--block", "FB_Damper", "--requires-file", wrong,
            "--subject", "DAMPER BEHAVIOUR");

        Assert.Equal(2, exit); // NOT CHECKED — never 1, which would blame the block
    }

    [Fact]
    public void RightEnumerationWithSubject_RunsAndPasses()
    {
        var right = WriteEnumeration("enum-damper.yaml",
            "DAMPER BEHAVIOUR. Every obligation whose response is a damper.", "DamperJammedAlarm");

        Assert.Equal(0, Run("--project", _dir, "--block", "FB_Damper", "--requires-file", right,
            "--subject", "DAMPER BEHAVIOUR"));
    }

    /// <summary>
    /// THE POSITIVE CONTROL FOR THE GATE ITSELF. Agreement must not become a blanket pass: a genuine
    /// missing signal, in a file whose subject agrees, is still a FAIL against the block.
    /// </summary>
    [Fact]
    public void SubjectAgreement_DoesNotSuppressARealFail()
    {
        var right = WriteEnumeration("enum-damper-2.yaml",
            "DAMPER BEHAVIOUR. Every obligation whose response is a damper.",
            "DamperJammedAlarm", "DamperJammedInhibit");

        Assert.Equal(1, Run("--project", _dir, "--block", "FB_Damper", "--requires-file", right,
            "--subject", "DAMPER BEHAVIOUR"));
    }

    [Fact]
    public void SubjectAssertedAgainstAFileThatDeclaresNone_IsNotChecked()
    {
        var silent = WriteEnumeration("silent.yaml", subject: null, "DamperJammedAlarm");

        Assert.Equal(2, Run("--project", _dir, "--block", "FB_Damper", "--requires-file", silent,
            "--subject", "DAMPER BEHAVIOUR"));
    }

    [Fact]
    public void SubjectWithoutARequiresFile_IsRefused()
    {
        // --subject asks whether a FILE is about the right thing; a --requires list has no subject to
        // compare, and silently ignoring the flag would be a guard the caller believes they have.
        Assert.Equal(1, Run("--project", _dir, "--block", "FB_Damper",
            "--requires", "DamperJammedAlarm", "--subject", "DAMPER BEHAVIOUR"));
    }

    /// <summary>The provenance line states what the file says it is about, guarded or not.</summary>
    [Fact]
    public void TheDeclaredSubjectReachesTheReport()
    {
        var right = WriteEnumeration("enum-damper-3.yaml",
            "DAMPER BEHAVIOUR. Every obligation whose response is a damper.", "DamperJammedAlarm");

        var stdout = new StringWriter();
        var original = Console.Out;
        Console.SetOut(stdout);
        try
        {
            Run("--project", _dir, "--block", "FB_Damper", "--requires-file", right);
        }
        finally
        {
            Console.SetOut(original);
        }

        Assert.Contains("the file declares subject: \"DAMPER BEHAVIOUR.", stdout.ToString());
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
}
