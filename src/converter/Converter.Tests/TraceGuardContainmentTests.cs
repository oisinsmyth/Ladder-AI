using Converter.Ir;
using Converter.Trace;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// FI-32-min (2026-08-05): `converter trace`'s guard-containment hop — every signal the spec lists as a
/// condition on a coil must actually appear in that write's guard. A set-difference over signal identity,
/// which is what makes it immune to how anyone *reads* an ambiguous requirement.
///
/// Why it exists: a cascade-hold term was dropped on three filter units and shipped as a REGRESSION,
/// because the coder and the functional reviewer resolved the same ambiguous source ("Fans-shutdown-ready
/// / Air-separator VSD shut down") the same way — a correlated check, not an independent one
/// (`docs/evidence/PlantAutoControl-bench-autopsy.md`). Verified against the real graded pair: the generated
/// block reports MISSING, the sealed answer key reports OK.
/// </summary>
public class TraceGuardContainmentTests : IDisposable
{
    private readonly string _dir;

    public TraceGuardContainmentTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"trace-guard-{Guid.NewGuid():N}");
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

    private void WriteBlock(string fileName, params IrNetwork[] networks) =>
        File.WriteAllText(
            Path.Combine(_dir, fileName),
            IrSerializer.SerializeBlockReadable(
                new IrBlock("0", "FC", "FC_Guards", 1, "LAD", null, networks)));

    private TraceReport RunBinding(string bindingJson)
    {
        var path = Path.Combine(_dir, "binding.json");
        File.WriteAllText(path, bindingJson);
        return TraceRunner.Run(path, _dir);
    }

    private static Expr Guard(params Expr[] operands) => new Expr.And(operands);

    [Fact]
    public void AllRequiredTermsPresent_IsOk()
    {
        WriteBlock("FC_Guards.ir", new IrNetwork(1, "Hold", new[]
        {
            new CoilAssignment("Inst.IO.Shutdown",
                Guard(new Expr.TagRef("Plant.FansReady"), new Expr.Not(new Expr.TagRef("Neighbour.ShutdownComplete")))),
        }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.FansReady", "Neighbour.ShutdownComplete"] } } ] }
        """);

        var hop = Assert.Single(Assert.Single(report.Requirements).Hops);
        Assert.Equal(HopKind.GuardContainment, hop.Hop);
        Assert.Equal(Verdict.Ok, hop.Verdict);
    }

    // The documented regression, in miniature: the guard keeps the plant-wide flag and drops the
    // specific neighbour's completion.
    [Fact]
    public void MissingTerm_IsReported_AndNamesTheWritingSite()
    {
        WriteBlock("FC_Guards.ir", new IrNetwork(4, "Hold", new[]
        {
            new CoilAssignment("Inst.IO.Shutdown", Guard(new Expr.TagRef("Plant.FansReady"))),
        }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.FansReady", "Neighbour.ShutdownComplete"] } } ] }
        """);

        var hop = Assert.Single(Assert.Single(report.Requirements).Hops);
        Assert.Equal(Verdict.MissingTerm, hop.Verdict);
        var evidence = Assert.Single(hop.Evidence);
        Assert.Contains("FC_Guards N4", evidence);
        Assert.Contains("MISSING Neighbour.ShutdownComplete", evidence);
        Assert.DoesNotContain("Plant.FansReady", evidence); // the present term is not reported missing
    }

    // A coil nothing writes is an output-path fact. Reporting every required term as "missing" off the
    // back of a path that simply isn't written would be a false finding of a different class.
    [Fact]
    public void CoilWithNoWriter_IsUnimplemented_NotAllTermsMissing()
    {
        WriteBlock("FC_Guards.ir", new IrNetwork(1, "Other", new[]
        {
            new CoilAssignment("Something.Else", new Expr.TagRef("X")),
        }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.FansReady"] } } ] }
        """);

        var hop = Assert.Single(Assert.Single(report.Requirements).Hops);
        Assert.Equal(Verdict.Unimplemented, hop.Verdict);
        Assert.Contains("no output path", hop.Detail);
        Assert.Empty(hop.Evidence);
    }

    // Containment and armed-ness are different questions: the term IS in the guard, but the write can
    // never fire. Report both facts rather than collapsing them.
    [Fact]
    public void TermPresentButWriterDisarmed_ReportsPresent_AndMarksDisarmed()
    {
        WriteBlock("FC_Guards.ir", new IrNetwork(7, "Disarmed", new[]
        {
            new CoilAssignment("Inst.IO.Shutdown",
                Guard(new Expr.TagRef("Plant.FansReady"), new Expr.Not(new Expr.TagRef("AlwaysTrue")))),
        }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.FansReady"] } } ] }
        """);

        var hop = Assert.Single(Assert.Single(report.Requirements).Hops);
        Assert.Equal(Verdict.Ok, hop.Verdict);
        Assert.Contains("[disarmed]", Assert.Single(hop.Evidence));
    }

    // The multi-instance shape a union across sites would hide: present in one network, absent in another.
    [Fact]
    public void TwoWriters_ReportsPerSite_NotAUnion()
    {
        WriteBlock("FC_Guards.ir",
            new IrNetwork(3, "Good", new[]
            {
                new CoilAssignment("Inst.IO.Shutdown",
                    Guard(new Expr.TagRef("Plant.FansReady"), new Expr.TagRef("Neighbour.ShutdownComplete"))),
            }),
            new IrNetwork(9, "Gap", new[]
            {
                new CoilAssignment("Inst.IO.Shutdown", Guard(new Expr.TagRef("Plant.FansReady"))),
            }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.FansReady", "Neighbour.ShutdownComplete"] } } ] }
        """);

        var hop = Assert.Single(Assert.Single(report.Requirements).Hops);
        Assert.Equal(Verdict.MissingTerm, hop.Verdict);
        Assert.Equal(2, hop.Evidence.Count);
        Assert.Contains(hop.Evidence, e => e.Contains("N3") && e.Contains("all 2 required term(s) present"));
        Assert.Contains(hop.Evidence, e => e.Contains("N9") && e.Contains("MISSING Neighbour.ShutdownComplete"));
    }

    // Terms nested inside a comparison are still terms.
    [Fact]
    public void TermInsideComparison_IsFound()
    {
        WriteBlock("FC_Guards.ir", new IrNetwork(1, "Cmp", new[]
        {
            new CoilAssignment("Inst.IO.Shutdown",
                Guard(new Expr.Compare(">=", new Expr.TagRef("Plant.Status"), new Expr.Literal("1")))),
        }));

        var report = RunBinding("""
        { "bindings": [ { "req": "R1", "guard": { "coil": "Inst.IO.Shutdown",
          "must_contain": ["Plant.Status"] } } ] }
        """);

        Assert.Equal(Verdict.Ok, Assert.Single(Assert.Single(report.Requirements).Hops).Verdict);
    }
}
