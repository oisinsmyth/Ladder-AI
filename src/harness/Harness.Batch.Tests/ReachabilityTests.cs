using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// 🔴 <b>The check that would have caught the orphan.</b>
///
/// <para>Measured 2026-08-22: a slot FC was in the controller and called by nothing. Its stimulus
/// model never executed, every vector in that lane timed out, and it took an IR read to find — three
/// hours after the deploy that could have refused it.</para>
///
/// <para><b>Nothing at runtime could have caught it</b>, which is why this is static. The load
/// manifest said <c>Loaded</c>, which is true and is not "in the scan". X-E's start echo said
/// <i>"observed to run"</i>, because both the start coil and the echo live in the copy layer — the
/// loop closes without the block ever executing.</para>
/// </summary>
public sealed class ReachabilityTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "reach-" + Guid.NewGuid().ToString("N"));

    public ReachabilityTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true); } catch (IOException) { }
    }

    private string Block(string kind, string name, params string[] calls)
    {
        var path = Path.Combine(_dir, name + ".ir");
        var body = string.Join("\n", calls.Select((c, i) => $"NETWORK {i + 1} \"call\"\n  CALL {c}(EN := TRUE)"));
        File.WriteAllText(path, $"BLOCK {kind} {name}\n  NUMBER 1\n{body}\n");
        return path;
    }

    private ReachabilityReport Of() => Reachability.Of(new[] { _dir });

    // ---------------------------------------------------------------------------------------------

    /// <summary>The shape that actually happened: one slot called, its sibling not.</summary>
    [Fact]
    public void An_FC_that_NOTHING_calls_is_refused_and_named()
    {
        Block("OB", "Main", "FC_HarnessVesselSlot", "FC_HarnessCopyLayer");
        Block("FC", "FC_HarnessVesselSlot");
        Block("FC", "FC_HarnessValveSlot");          // the orphan
        Block("FC", "FC_HarnessCopyLayer");

        var report = Of();

        Assert.True(report.Verified);
        Assert.Equal(new[] { "FC_HarnessValveSlot" }, report.Unreachable);
        Assert.Contains(report.Refusals, r => r.Contains("FC_HarnessValveSlot") && r.Contains("NOT REACHABLE from any OB"));
    }

    /// <summary>The refusal explains why runtime could not have told you — or it reads as pedantry.</summary>
    [Fact]
    public void The_refusal_says_why_the_load_manifest_and_the_start_echo_do_not_catch_this()
    {
        Block("OB", "Main", "FC_Used");
        Block("FC", "FC_Used");
        Block("FC", "FC_Orphan");

        var refusal = Assert.Single(Of().Refusals);

        Assert.Contains("load manifest says Loaded", refusal);
        Assert.Contains("start echo", refusal);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> A check that refuses everything would satisfy both tests above.
    /// A fully wired program must pass, including transitively — a block called only by another FC.
    /// </summary>
    [Fact]
    public void A_fully_wired_program_passes_INCLUDING_transitively()
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot", "FB_Stim");
        Block("FB", "FB_Stim");

        var report = Of();

        Assert.True(report.Verified);
        Assert.Empty(report.Unreachable);
        Assert.Empty(report.Refusals);
        Assert.Contains("3 of 3", report.Summary);
    }

    /// <summary>
    /// 🔴 <b>NO OB MEANS UNKNOWN, NOT CLEAN — and it says so rather than staying silent.</b> A lane
    /// legitimately supplies only its own blocks, so this does not refuse; but an unstated "could not
    /// check" reads as "checked", which is the blind spot the orphan hid in.
    /// </summary>
    [Fact]
    public void A_union_with_no_OB_reports_NOT_VERIFIED_and_does_not_refuse()
    {
        Block("FC", "FC_Slot", "FB_Stim");
        Block("FB", "FB_Stim");

        var report = Of();

        Assert.False(report.Verified);
        Assert.Empty(report.Refusals);
        Assert.Contains("REACHABILITY NOT VERIFIED", report.Summary);
        Assert.Contains("NOT 'ALL REACHABLE'", report.Summary);
    }

    /// <summary>
    /// Data blocks, UDTs and tag tables are not scan participants and must not be reported as orphans —
    /// a check that accused every DB would be switched off within a day.
    /// </summary>
    [Fact]
    public void Non_code_objects_are_not_treated_as_unreachable()
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot");
        File.WriteAllText(Path.Combine(_dir, "DB_Thing.ir"), "DB DB_Thing\n  NUMBER 5\n");
        File.WriteAllText(Path.Combine(_dir, "HarnessMirror.ir"), "TAGTABLE HarnessMirror\n  TAGS\n");

        Assert.Empty(Of().Unreachable);
    }

    /// <summary>
    /// A call to something the union does not contain is not this check's business. Treating it as a
    /// graph node would invent a member and then report it missing.
    /// </summary>
    [Fact]
    public void A_call_to_a_block_outside_the_union_is_ignored_rather_than_invented()
    {
        Block("OB", "Main", "FC_Slot", "FC_SomethingElseEntirely");
        Block("FC", "FC_Slot");

        var report = Of();

        Assert.Empty(report.Unreachable);
        Assert.DoesNotContain("FC_SomethingElseEntirely", string.Join(" ", report.Reached));
    }

    /// <summary>
    /// 🔴 <b>The plan states the verdict on EVERY run</b>, pass or refuse — see the summary line. A
    /// silent pass and a silent "could not check" are indistinguishable to a reader.
    /// </summary>
    [Fact]
    public void The_summary_is_never_empty()
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot");

        Assert.False(string.IsNullOrWhiteSpace(Of().Summary));
    }

    // ---------------------------------------------------------------------------------------------
    // THE THIRD OUTCOME. A file that declares nothing this walk can identify used to be `continue`d
    // silently under a comment saying "a DB, a UDT or a tag table" — true of the intended case and not
    // of the whole set.
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>A file declaring NOTHING gates, because it is a hole in the denominator.</b> A truncated or
    /// half-written code block lands in exactly this branch: it drops out of the block set, out of the
    /// <c>n of m</c> count and out of every refusal, while the summary still reads VERIFIED.
    /// </summary>
    [Fact]
    public void A_file_that_declares_NOTHING_is_named_and_gates()
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot");
        File.WriteAllText(Path.Combine(_dir, "Truncated.ir"), "BLOCK F");   // a write that died mid-header

        var report = Of();

        Assert.Contains(report.Refusals, r => r.Contains("Truncated.ir", StringComparison.Ordinal));
        Assert.Contains(report.Refusals, r => r.Contains("NOT in the denominator", StringComparison.OrdinalIgnoreCase)
                                           || r.Contains("SMALLER than the one supplied", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL, and the reason the third outcome needed a positive match rather than a
    /// negative one.</b> DBs, UDTs and tag tables legitimately have no <c>BLOCK</c> header and legitimately
    /// are not scan participants. They declare themselves, so they are recognised rather than assumed —
    /// and a check that gated on all three would refuse every real corpus.
    /// </summary>
    [Theory]
    [InlineData("DB DB_Thing\n  MEMBERS\n    A : Bool\n")]
    [InlineData("TYPE UDT_Thing\n  MEMBERS\n    A : Bool\n")]
    [InlineData("TAGTABLE HarnessMirror\n  TAG HX_A : Bool %M1000.0\n")]
    public void A_DB_a_UDT_and_a_tag_table_are_recognised_and_do_NOT_gate(string content)
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot");
        File.WriteAllText(Path.Combine(_dir, "NotCode.ir"), content);

        Assert.Empty(Of().Refusals);
    }

    /// <summary>
    /// The verdict states which derivation produced it. Two exist by necessity — the harness is
    /// dependency-free by design — so a reader has to be able to tell which one they are reading.
    /// </summary>
    [Fact]
    public void The_summary_names_itself_as_the_weaker_derivation()
    {
        Block("OB", "Main", "FC_Slot");
        Block("FC", "FC_Slot");

        Assert.Contains("weaker", Of().Summary);
    }
}
