using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>Two calls, and the order is the whole content.</b>
///
/// <para>The call order is asserted from the PARSED sequence of CALL statements, not from string
/// positions: a test that searched for one substring before another would pass on a block whose calls
/// were in one network, or commented out, or duplicated.</para>
/// </summary>
public class SlotFcGeneratorTests
{
    private static readonly SlotFcNaming Naming = new("FC_HarnessSlot", 9010);

    private static SlotCall Head => new("FB_DemoStim", "iDB_DemoStim", "Drive The Plant Model");

    private static SlotCall Uut => new("FB_DemoUnderTest", "iDB_DemoUnderTest", "The Block Under Test");

    /// <summary>Every CALL in the emitted block, in emission order, as (block, instance).</summary>
    private static List<(string Block, string? Instance)> CallsIn(string ir)
    {
        var calls = new List<(string, string?)>();

        foreach (var raw in ir.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("CALL ", StringComparison.Ordinal))
                continue;

            var open = line.IndexOf('(');
            var block = line[5..open].Trim();
            var args = line[(open + 1)..line.LastIndexOf(')')].Split(',', StringSplitOptions.TrimEntries);

            // Per ir/SPEC.md the instance is the FIRST POSITIONAL argument when present, disambiguated by
            // the next argument starting with the reserved "EN := " prefix.
            calls.Add((block, args[0].StartsWith("EN :=", StringComparison.Ordinal) ? null : args[0]));
        }

        return calls;
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>The head is called FIRST.</b> The block under test must see THIS scan's commands. Reversed,
    /// every transition costs a scan of latency — which compiles, imports, and shows up only as vectors
    /// timing out near their backstop.
    /// </summary>
    [Fact]
    public void The_stimulus_head_is_called_before_the_block_under_test()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, Uut);
        var calls = CallsIn(result.Ir);

        Assert.Equal(2, calls.Count);
        Assert.Equal(("FB_DemoStim", "iDB_DemoStim"), calls[0]);
        Assert.Equal(("FB_DemoUnderTest", "iDB_DemoUnderTest"), calls[1]);
    }

    /// <summary>
    /// 🔴 <b>And the order is NOT A PARAMETER — there is no argument that reverses it.</b> Handing the
    /// blocks over in the other order produces a block that calls them in the other order, because they
    /// are different roles rather than a list; what is asserted here is that the ROLE decides position.
    /// </summary>
    [Fact]
    public void Swapping_the_ROLES_swaps_the_calls_because_position_follows_role_not_argument_index()
    {
        var result = SlotFcGenerator.Generate(Naming, stimulusHead: Uut, blockUnderTest: Head);
        var calls = CallsIn(result.Ir);

        Assert.Equal("FB_DemoUnderTest", calls[0].Block);
        Assert.Equal("FB_DemoStim", calls[1].Block);
    }

    /// <summary>An FC has no instance, and the readable form omits the positional argument entirely.</summary>
    [Fact]
    public void An_FC_under_test_is_called_with_no_instance_argument()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, new SlotCall("FC_Stateless", null, "The Block Under Test"));
        var calls = CallsIn(result.Ir);

        Assert.Equal(("FC_Stateless", (string?)null), calls[1]);
        Assert.Contains("CALL FC_Stateless(EN := TRUE)", result.Ir);
    }

    /// <summary>The block header carries the number it was given, and the block is an FC.</summary>
    [Fact]
    public void The_emitted_block_is_an_FC_carrying_the_allocated_number()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, Uut);

        Assert.Contains("BLOCK FC FC_HarnessSlot", result.Ir);
        Assert.Contains("NUMBER 9010", result.Ir);
        Assert.Contains("LANGUAGE LAD", result.Ir);
    }

    /// <summary>Every network gets a title (C-201), and it is the caller's, not an invented one.</summary>
    [Fact]
    public void Both_networks_carry_the_titles_they_were_given()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, Uut);

        Assert.Contains("NETWORK 1 \"Drive The Plant Model\"", result.Ir);
        Assert.Contains("NETWORK 2 \"The Block Under Test\"", result.Ir);
    }

    /// <summary>
    /// 🔴 <b>The obligation is emitted WITH the block.</b> This is the whole by-construction answer to the
    /// orphan: a generated FC that nothing calls is loaded, healthy in every artifact, and never runs.
    /// </summary>
    [Fact]
    public void The_result_carries_the_call_site_obligation_naming_the_block()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, Uut);

        Assert.Contains("FC_HarnessSlot", result.CallSiteObligation);
        Assert.Contains("cyclic OB", result.CallSiteObligation);
        Assert.Contains("never executes", result.CallSiteObligation);
    }

    // --- refusals, each one a real way to get this wrong ------------------------------------------

    /// <summary>Hard rule 3: the generator does not invent a block number.</summary>
    [Fact]
    public void A_missing_block_number_is_refused_and_says_where_to_get_one()
    {
        var error = Assert.Throws<ArgumentException>(
            () => SlotFcGenerator.Generate(new SlotFcNaming("FC_HarnessSlot", 0), Head, Uut));

        Assert.Contains("9000-9999", error.Message);
        Assert.Contains("converter claim --allocate", error.Message);
    }

    /// <summary>
    /// 🔴 One instance cannot hold two blocks' state. They would overwrite each other's statics every
    /// scan, and the results would be neither block's.
    /// </summary>
    [Fact]
    public void The_SAME_instance_for_both_calls_is_refused()
    {
        var shared = new SlotCall("FB_DemoUnderTest", "iDB_DemoStim", "The Block Under Test");

        var error = Assert.Throws<ArgumentException>(() => SlotFcGenerator.Generate(Naming, Head, shared));
        Assert.Contains("iDB_DemoStim", error.Message);
    }

    /// <summary>A block cannot drive itself — the head exists to command what the block under test reads.</summary>
    [Fact]
    public void The_SAME_block_in_both_roles_is_refused()
    {
        var error = Assert.Throws<ArgumentException>(
            () => SlotFcGenerator.Generate(Naming, Head, new SlotCall("FB_DemoStim", "iDB_Other", "x")));

        Assert.Contains("cannot drive itself", error.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("FB Demo")]
    [InlineData("9Demo")]
    public void An_unusable_block_name_is_refused(string name)
    {
        Assert.Throws<ArgumentException>(
            () => SlotFcGenerator.Generate(Naming, Head, new SlotCall(name, "iDB_X", "t")));
    }

    [Theory]
    [InlineData("iDB X")]
    [InlineData("iDB_X.")]
    [InlineData(".iDB_X")]
    public void An_unusable_instance_path_is_refused(string path)
    {
        Assert.Throws<ArgumentException>(
            () => SlotFcGenerator.Generate(Naming, Head, new SlotCall("FB_Other", path, "t")));
    }

    /// <summary>A dotted multi-instance placement IS usable, and must not be caught by the rule above.</summary>
    [Fact]
    public void A_dotted_multi_instance_path_is_accepted()
    {
        var result = SlotFcGenerator.Generate(
            Naming, Head, new SlotCall("FB_Other", "iDB_Owner.Inner", "The Block Under Test"));

        Assert.Contains("CALL FB_Other(iDB_Owner.Inner, EN := TRUE)", result.Ir);
    }

    /// <summary>C-201: the generator refuses rather than inventing a title nobody chose.</summary>
    [Fact]
    public void A_missing_network_title_is_refused_rather_than_defaulted()
    {
        var error = Assert.Throws<ArgumentException>(
            () => SlotFcGenerator.Generate(Naming, Head, new SlotCall("FB_Other", "iDB_Other", "  ")));

        Assert.Contains("will not invent one", error.Message);
    }

    /// <summary>
    /// 🔴 <b>THE NEGATIVE CONTROL.</b> Six refusal tests above; a generator that threw on everything would
    /// pass all of them. The ordinary case must produce a block.
    /// </summary>
    [Fact]
    public void An_ordinary_lane_generates_without_complaint()
    {
        var result = SlotFcGenerator.Generate(Naming, Head, Uut);

        Assert.NotEmpty(result.Ir);
        Assert.Equal("FC_HarnessSlot", result.BlockName);
        Assert.Equal(2, CallsIn(result.Ir).Count);
    }
}
