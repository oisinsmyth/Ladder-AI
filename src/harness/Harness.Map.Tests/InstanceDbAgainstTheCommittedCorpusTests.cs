using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b>THE OUTSIDE AUTHORITY FOR THE INSTANCE-DB PROJECTION — the committed IR corpus, which nobody on
/// this lane wrote.</b>
///
/// <para><see cref="CopyLayerAgainstRealTiaExportTests"/>'s opening paragraph applies here word for word: a
/// generator checked only against what its author expected it to emit has one opinion in the loop. So these
/// tests read <c>ir/test-project001/</c> — the hand-authored instance DBs this generator exists to replace —
/// and require the projection to REPRODUCE them, or to differ only in ways stated out loud below.</para>
///
/// <para><b>What this cannot do:</b> a corpus is a sample, and reproducing a committed <c>.ir</c> is not an
/// import. <b>The compile gate is still the gate</b> (hard rule 4).</para>
/// </summary>
public class InstanceDbAgainstTheCommittedCorpusTests
{
    /// <summary>
    /// The committed IR corpus. <b>A missing corpus FAILS rather than skipping</b> — a check that quietly
    /// disappears when its input is absent is a check that stopped running.
    /// </summary>
    private static string IrDirectory
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);

            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, "ir", "test-project001");
                if (File.Exists(Path.Combine(candidate, "Main.ir")))
                    return candidate;

                dir = dir.Parent;
            }

            throw new InvalidOperationException(
                "the committed IR corpus was not found by walking up from " + AppContext.BaseDirectory
                + " for ir/test-project001/Main.ir. *** THIS IS A FAILURE, NOT A REASON TO SKIP. *** These are the only "
                + "checks on the instance-DB projection that a party other than its author can fail.");
        }
    }

    private static string Ir(string file) => File.ReadAllText(Path.Combine(IrDirectory, file)).Replace("\r\n", "\n");

    // ---------------------------------------------------------------------------------------------
    // THE PROJECTION REPRODUCES A HAND-AUTHORED INSTANCE DB
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE WHOLE CLAIM, ON A REAL PAIR: 33 hand-typed lines come back byte for byte from the FB
    /// beside them plus TWO declared presets.</b>
    ///
    /// <para>The two are <c>BlockedTimeThreshold</c> and <c>ClearDebounceTime</c> — commissioning defaults,
    /// which is to say claims about the plant. They are the only part of the file that is not derivable, and
    /// they are exactly the part the generator refuses to originate.</para>
    /// </summary>
    [Fact]
    public void THE_MONITOR_INSTANCE_DB_IS_REPRODUCED_FROM_ITS_FB_PLUS_TWO_DECLARED_PRESETS()
    {
        var committed = Ir("iDB_HopperBlockageMonitor.ir");

        var result = InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_HopperBlockageMonitor", 51, "FB_HopperBlockageMonitor", CommentOf(committed)),
                InstanceDbMemberSource.ProjectedFromFb,
                new[]
                {
                    new InstanceDbPreset("IO.BlockedTimeThreshold", "T#60S"),
                    new InstanceDbPreset("IO.ClearDebounceTime", "T#2S"),
                }),
            Ir("FB_HopperBlockageMonitor.ir"));

        Assert.Equal(committed, result.Ir);
    }

    /// <summary>
    /// 🔴 <b>AND THE OTHER HALF OF THE CASE FOR GENERATING THESE AT ALL: the committed stimulus instance DB
    /// IS ALREADY STALE.</b>
    ///
    /// <para>Its FB has grown nine statics since the file was typed, and the file does not have them. The
    /// projection does — which is what makes the two differ, and the difference is asserted here rather than
    /// worked around, because a hand-authored projection of a moving interface drifts silently and this is
    /// the measurement that says so.</para>
    ///
    /// <para>(It compiles today because TIA re-projects an instance DB from its FB. That is the same
    /// mechanism <see cref="InstanceDbMemberSource.LeftToTia"/> declares deliberately — the difference being
    /// that here nobody declared it, so the file on disk says something about the block that is not true.)</para>
    /// </summary>
    [Fact]
    public void THE_COMMITTED_STIMULUS_INSTANCE_DB_IS_STALE_AGAINST_ITS_OWN_FB()
    {
        var committed = Ir("iDB_HopperBlockageStim.ir");

        var result = InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_HopperBlockageStim", 9002, "FB_HopperBlockageStim", CommentOf(committed)),
                InstanceDbMemberSource.ProjectedFromFb,
                new[] { new InstanceDbPreset("Stim.ModelThreshold", "T#60S") }),
            Ir("FB_HopperBlockageStim.ir"));

        var generatedNames = TopLevelMemberNames(result.Ir);
        var committedNames = TopLevelMemberNames(committed);

        // Every member the hand-authored file has, the projection has.
        Assert.Empty(committedNames.Except(generatedNames, StringComparer.Ordinal));

        // And nine it does not — read off the FB, not typed here.
        var missing = generatedNames.Except(committedNames, StringComparer.Ordinal).ToArray();
        Assert.Equal(
            new[]
            {
                "ClearDownReset", "HopperClearedInStop", "HopperClearedMidRun", "HopperHeldHigh", "RunHeldOn",
                "RunStoppedMidRun", "RunStoppedRoundClear", "RunT", "ScenarioReset",
            },
            missing.OrderBy(n => n, StringComparer.Ordinal).ToArray());
    }

    /// <summary>
    /// 🔴 <b><see cref="InstanceDbMemberSource.LeftToTia"/> REPRODUCES ITS COMMITTED FILE EXACTLY — which is
    /// what makes it a declared shape rather than a fallback.</b>
    ///
    /// <para><c>iDB_Comms_ModbusServer.ir</c> is 8 lines with an empty <c>MEMBERS</c> section, and that is
    /// deliberate: the FB holds an <c>MB_SERVER</c> instance and a <c>TCON_IP_v4</c> connection struct, and
    /// TIA fills the instance from the FB at compile.</para>
    /// </summary>
    [Fact]
    public void THE_COMMS_INSTANCE_DB_IS_REPRODUCED_BY_THE_LEFT_TO_TIA_SHAPE()
    {
        var committed = Ir("iDB_Comms_ModbusServer.ir");

        var result = InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_Comms_ModbusServer", 9000, "FB_Comms_ModbusServer", CommentOf(committed)),
                InstanceDbMemberSource.LeftToTia),
            Ir("FB_Comms_ModbusServer.ir"));

        Assert.Equal(committed, result.Ir);
    }

    /// <summary>
    /// 🔴 <b>AND IT REPORTS WHAT THAT SHAPE COSTS: the eight start values the instance INHERITS, including
    /// the listening port and the connection ID.</b>
    ///
    /// <para>Read off the FB's own text, so the report cannot disagree with the block it describes. These are
    /// the values that make a SECOND <c>leftToTia</c> instance of this FB a silent collision —
    /// <see cref="ProgramGeneratorTests"/> holds that refusal.</para>
    /// </summary>
    [Fact]
    public void THE_LEFT_TO_TIA_SHAPE_REPORTS_THE_START_VALUES_THE_INSTANCE_INHERITS()
    {
        var result = InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_Comms_ModbusServer", 9000, "FB_Comms_ModbusServer", "declared"),
                InstanceDbMemberSource.LeftToTia),
            Ir("FB_Comms_ModbusServer.ir"));

        Assert.Contains("Comms.LocalPort (= 503)", result.InheritedStartValues);
        Assert.Contains("Comms.ID (= 16#0010)", result.InheritedStartValues);
        Assert.Contains("Comms.RemoteAddress.ADDR[1] (= 16#00)", result.InheritedStartValues);
    }

    /// <summary>
    /// 🔴 <b>AND THE SAME FB PROJECTED IS A REFUSAL NAMING THE MEMBER — not a silent copy of a port into a
    /// second instance.</b>
    /// </summary>
    [Fact]
    public void PROJECTING_A_SYSTEM_TYPE_INSTANCE_IS_REFUSED_BY_NAME()
    {
        var error = Assert.Throws<ArgumentException>(() => InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_Second_ModbusServer", 9050, "FB_Comms_ModbusServer", "declared"),
                InstanceDbMemberSource.ProjectedFromFb),
            Ir("FB_Comms_ModbusServer.ir")));

        Assert.Contains("MbServer : MB_SERVER", error.Message, StringComparison.Ordinal);
        Assert.Contains("leftToTia", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // helpers — every expectation DERIVED from the corpus, never typed beside the generator
    // ---------------------------------------------------------------------------------------------

    /// <summary>The DB comment, lifted out of the committed file so the reproduction test does not retype it.</summary>
    private static string CommentOf(string dbIr)
    {
        var line = dbIr.Split('\n').Single(l => l.StartsWith("  COMMENT \"", StringComparison.Ordinal));
        var body = line["  COMMENT \"".Length..^1];
        return body.Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string[] TopLevelMemberNames(string dbIr)
    {
        var lines = dbIr.Split('\n');
        var start = Array.IndexOf(lines, "  MEMBERS") + 1;

        return lines.Skip(start)
            .Where(l => l.StartsWith("    ", StringComparison.Ordinal) && !l.StartsWith("     ", StringComparison.Ordinal))
            .Select(l => l.Trim().Split(" : ")[0])
            .ToArray();
    }
}
