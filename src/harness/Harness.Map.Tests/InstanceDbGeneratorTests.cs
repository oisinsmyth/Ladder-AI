using Harness.Map;

namespace Harness.Map.Tests;

/// <summary>
/// 🔴 <b><see cref="InstanceDbGenerator"/> — the structure is a projection, THE START VALUES ARE NOT.</b>
///
/// <para><see cref="InstanceDbAgainstTheCommittedCorpusTests"/> holds the grounding half: two committed
/// instance DBs reproduced byte for byte from the FBs beside them. This file holds the REFUSALS, and every
/// FB in it is <b>INVENTED</b> — obviously-synthetic structural material, making no claim about any plant.
/// A green run here is not evidence about a real block.</para>
///
/// <para>The line these tests are about is CLAUDE.md's: <b>MECHANISM vs A CLAIM ABOUT THE PLANT</b>. An
/// instance DB's members are mechanism. Its start values are presets, presets say what the equipment does,
/// and the file boundary of something a generator emitted does not change that.</para>
/// </summary>
public class InstanceDbGeneratorTests
{
    private const string Fb = """
        BLOCK FB FB_Widget
        ROOTID 0
        NUMBER 9100
        LANGUAGE LAD
        TITLE "Widget"

        INTERFACE
          INPUT
          OUTPUT
          STATIC
            Cmd : "UDT_WidgetIO" RETAIN SETPOINT COMMENT "Caller interface, written by the map."
              Start : Bool
              DwellTime : Time
            Running : Bool COMMENT "The widget is under way."
            Held : Bool RETAIN
            DwellTimer : TON_TIME VERSION 1.0 SETPOINT
              PT : Time
              ET : Time
              IN : Bool
              Q : Bool
          CONSTANT

        NETWORK 1 "Run"
          COIL Running := Cmd.Start
        """;

    /// <summary>An FB carrying its own start values — the shape whose presets must not be inherited quietly.</summary>
    private const string FbWithStartValues = """
        BLOCK FB FB_Listener
        ROOTID 0
        NUMBER 9101
        LANGUAGE LAD
        TITLE "Listener"

        INTERFACE
          INPUT
          OUTPUT
          STATIC
            ListenPort : UInt = 503
            Retries : Int
          CONSTANT

        NETWORK 1 "Listen"
          COIL Retries := ListenPort
        """;

    private static InstanceDbNaming Naming(string db = "iDB_Widget", int number = 9200, string fb = "FB_Widget") =>
        new(db, number, fb, "One widget instance. Synthetic test material.");

    private static InstanceDbResult Generate(
        IReadOnlyList<InstanceDbPreset>? presets = null,
        InstanceDbMemberSource members = InstanceDbMemberSource.ProjectedFromFb,
        string fbIr = Fb,
        InstanceDbNaming? naming = null) =>
        InstanceDbGenerator.Generate(new InstanceDbDeclaration(naming ?? Naming(), members, presets), fbIr);

    // ---------------------------------------------------------------------------------------------
    // WHAT IT PROJECTS
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// The whole mechanical claim: header, <c>INSTANCEOF</c>, the FB's present-but-empty INPUT/OUTPUT
    /// sections, and its STATIC section as the DB's MEMBERS — markers, nesting and all.
    /// </summary>
    [Fact]
    public void THE_STRUCTURE_IS_PROJECTED_FROM_THE_FBS_STATIC_SECTION()
    {
        var ir = Generate().Ir;

        Assert.Equal(
            """
            DB iDB_Widget
              ROOTID 0
              NUMBER 9200
              INSTANCEOF FB_Widget
              COMMENT "One widget instance. Synthetic test material."
              INPUT
              OUTPUT
              MEMBERS
                Cmd : "UDT_WidgetIO" RETAIN SETPOINT
                  Start : Bool
                  DwellTime : Time
                Running : Bool
                Held : Bool RETAIN
                DwellTimer : TON_TIME VERSION 1.0 SETPOINT
                  PT : Time
                  ET : Time
                  IN : Bool
                  Q : Bool

            """.Replace("\r\n", "\n"),
            ir);
    }

    /// <summary>
    /// 🔴 <b>MEMBER COMMENTS ARE DROPPED, and that is the projection's shape rather than an omission.</b>
    /// No committed instance DB carries one; the FB is where a member is explained, and copying the prose
    /// into the instance creates a second copy free to disagree with the first.
    /// </summary>
    [Fact]
    public void MEMBER_COMMENTS_DO_NOT_CROSS_INTO_THE_INSTANCE()
    {
        Assert.DoesNotContain("COMMENT \"Caller interface", Generate().Ir, StringComparison.Ordinal);
        Assert.DoesNotContain("COMMENT \"The widget is under way", Generate().Ir, StringComparison.Ordinal);
    }

    /// <summary>A declared preset lands on its path, and only on its path.</summary>
    [Fact]
    public void A_DECLARED_PRESET_LANDS_ON_ITS_MEMBER()
    {
        var ir = Generate(new[] { new InstanceDbPreset("Cmd.DwellTime", "T#12S") }).Ir;

        Assert.Contains("    DwellTime : Time = T#12S\n", ir, StringComparison.Ordinal);
        Assert.Contains("    Start : Bool\n", ir, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // WHAT IT REFUSES — every one of them a start value
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE CENTRAL REFUSAL. An FB start value the declaration says nothing about stops the whole
    /// generation, and the message names the path and the value.</b>
    ///
    /// <para>Both available defaults are wrong. Copying it hands every instance of the FB the same value —
    /// and a listening port or a connection ID IDENTIFIES an instance, so the second one silently collides,
    /// not at compile time but as a connection that never establishes. Dropping it silently changes what the
    /// instance starts at. Only declaring is right.</para>
    /// </summary>
    [Fact]
    public void AN_UNDECLARED_FB_START_VALUE_IS_REFUSED_BY_PATH()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(
            fbIr: FbWithStartValues, naming: Naming("iDB_Listener", 9201, "FB_Listener")));

        Assert.Contains("ListenPort (= 503)", error.Message, StringComparison.Ordinal);
        Assert.Contains("claim about the plant", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Declared, it is carried — by somebody who knows the plant, not by the generator.</summary>
    [Fact]
    public void A_DECLARED_START_VALUE_IS_CARRIED()
    {
        var ir = Generate(
            new[] { new InstanceDbPreset("ListenPort", "504") },
            fbIr: FbWithStartValues,
            naming: Naming("iDB_Listener", 9201, "FB_Listener")).Ir;

        Assert.Contains("    ListenPort : UInt = 504\n", ir, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 <b><c>cleared</c> is the explicit "this instance deliberately starts at the type default".</b> It
    /// exists so that DROPPING a start value is also something somebody typed — otherwise the only way to
    /// drop one would be silence.
    /// </summary>
    [Fact]
    public void A_CLEARED_PRESET_DROPS_THE_FBS_START_VALUE()
    {
        var ir = Generate(
            new[] { new InstanceDbPreset("ListenPort", null, Cleared: true) },
            fbIr: FbWithStartValues,
            naming: Naming("iDB_Listener", 9201, "FB_Listener")).Ir;

        Assert.Contains("    ListenPort : UInt\n", ir, StringComparison.Ordinal);
        Assert.DoesNotContain("503", ir, StringComparison.Ordinal);
    }

    /// <summary>A preset that lands nowhere is a start value nobody would notice missing.</summary>
    [Fact]
    public void A_PRESET_PATH_THAT_NAMES_NOTHING_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(new[] { new InstanceDbPreset("Cmd.DwelTime", "T#1S") }));

        Assert.Contains("Cmd.DwelTime", error.Message, StringComparison.Ordinal);
        Assert.Contains("lands nowhere", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Both `value` and `cleared` are opposite claims about what the instance starts at.</summary>
    [Fact]
    public void A_PRESET_THAT_IS_BOTH_VALUED_AND_CLEARED_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(
            new[] { new InstanceDbPreset("Running", "TRUE", Cleared: true) }));

        Assert.Contains("BOTH", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An absent value is not a start value of zero — say which.</summary>
    [Fact]
    public void A_PRESET_WITH_NEITHER_A_VALUE_NOR_CLEARED_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(new[] { new InstanceDbPreset("Running", null) }));

        Assert.Contains("neither a `value` nor `cleared: true`", error.Message, StringComparison.Ordinal);
    }

    /// <summary>One member has one start value; two declarations of it is two answers to a question with one.</summary>
    [Fact]
    public void THE_SAME_PRESET_PATH_DECLARED_TWICE_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(new[]
        {
            new InstanceDbPreset("Cmd.DwellTime", "T#1S"),
            new InstanceDbPreset("Cmd.DwellTime", "T#2S"),
        }));

        Assert.Contains("twice", error.Message, StringComparison.Ordinal);
    }

    /// <summary><c>leftToTia</c> and presets cannot both hold — an empty MEMBERS section carries no member to set.</summary>
    [Fact]
    public void PRESETS_DECLARED_ALONGSIDE_LEFT_TO_TIA_MEMBERS_ARE_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(
            new[] { new InstanceDbPreset("Running", "TRUE") }, InstanceDbMemberSource.LeftToTia));

        Assert.Contains("cannot both hold", error.Message, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // WHAT IT REFUSES — the things hard rule 3 forbids inventing
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DB_WITH_NO_NUMBER_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(naming: Naming(number: 0)));
        Assert.Contains("converter claim --allocate", error.Message, StringComparison.Ordinal);
    }

    /// <summary>Generated code is held to a stricter bar than site code; an invented comment tells a reader nothing.</summary>
    [Fact]
    public void A_DB_WITH_NO_COMMENT_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => InstanceDbGenerator.Generate(
            new InstanceDbDeclaration(
                new InstanceDbNaming("iDB_Widget", 9200, "FB_Widget", "   "),
                InstanceDbMemberSource.ProjectedFromFb),
            Fb));

        Assert.Contains("has no comment", error.Message, StringComparison.Ordinal);
    }

    /// <summary>The INSTANCEOF would name one block and the members would come from another.</summary>
    [Fact]
    public void AN_FB_WHOSE_IR_IS_A_DIFFERENT_BLOCK_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(fbIr: FbWithStartValues));

        Assert.Contains("is block 'FB_Listener'", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An instance DB is an instance of an FB; only a `BLOCK FB` header can be projected from.</summary>
    [Fact]
    public void AN_FC_SUPPLIED_AS_THE_FB_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(fbIr: "BLOCK FC FB_Widget\nROOTID 0\n"));

        Assert.Contains("BLOCK FB <name>", error.Message, StringComparison.Ordinal);
    }

    /// <summary>An FB with no statics has no instance data to project — asserted rather than assumed from an absence.</summary>
    [Fact]
    public void AN_FB_WITH_NO_STATIC_SECTION_IS_REFUSED()
    {
        var error = Assert.Throws<ArgumentException>(() => Generate(
            fbIr: "BLOCK FB FB_Widget\nROOTID 0\nNUMBER 1\nLANGUAGE LAD\n\nINTERFACE\n  INPUT\n  OUTPUT\n  CONSTANT\n"));

        Assert.Contains("declares no STATIC section", error.Message, StringComparison.Ordinal);
    }
}
