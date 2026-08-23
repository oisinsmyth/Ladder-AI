using Converter.Neighbours;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-23, workbench Y1. <c>converter neighbours</c> — <b>the neighbour list, DERIVED from the
/// program corpus</b> instead of typed into a binding by somebody who happened to know.
///
/// <para><b>The defect it closes, measured live on the rig.</b> A generated mirror and a
/// hand-authored virtual panel both claimed registers 256–323 of one <c>%M</c> area on a running
/// controller — <b>53 tags overwritten bit for bit every scan</b>, among them the panel's master
/// enable. Nothing caught it: the allocator bounds the mirror against the declared area and the
/// register map proves the mirror disjoint <i>from itself</i>. Both ran, both passed, and neither
/// knows a neighbour exists. <c>ReservedRegion</c> closed the hole and its author wrote down what
/// it does not do — <i>"a place to put the knowledge rather than a way to obtain it."</i> This is
/// the thing that obtains it.</para>
///
/// <para>🔴 <b>THE COMMITTED REFERENCE PROJECT HAS NO NEIGHBOUR, SO THE POSITIVE FIXTURE HERE IS
/// INVENTED, AND A GREEN SUITE IS NOT EVIDENCE THAT THE DERIVATION REPRODUCES THE REAL
/// COLLISION.</b> <c>DefaultTagTable.ir</c>'s only <c>%M</c> tags are <c>%M0.7</c>, <c>%M1.0</c> and
/// <c>%M1.2</c> — every one of them below base 1000. Everything below that asserts a positive
/// neighbour was written for this file. The real evidence is a re-run against the live corpus, in
/// the job folder, never committed: it must reproduce the 256–323 band and the 53 tags. Fewer, or a
/// different band, stops the item. That sentence is deliberately in three places — the plan, these
/// tests, and the report — because a fixture that agrees with its author is the one thing a green
/// suite cannot tell you about.</para>
///
/// <para>The two real-artifact tests are the ZERO case and the AREA-DECLARATION case, and they run
/// against <c>ir/test-project001</c> exactly as committed.</para>
/// </summary>
public class NeighbourTests : IDisposable
{
    private readonly string _dir;

    public NeighbourTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"neighbours-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        // Cleanup, not an assertion. A scratch directory that Windows briefly refuses to unlink is a
        // failed TEST if it throws here, and a test that fails for a reason unrelated to what it
        // checks is worse than a stray temp folder.
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        GC.SuppressFinalize(this);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "patterns")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Could not find repo root.");
    }

    private static string Corpus => Path.Combine(RepoRoot(), "ir", "test-project001");

    /// <summary>The mirror's own area, exactly as the committed comms block serves it.</summary>
    private static MarkerArea ServedArea => MarkerArea.OfRegisters(1000, 37);

    /// <summary>A wider area, the shape a grown mirror would ask about.</summary>
    private static MarkerArea WideArea => MarkerArea.OfRegisters(1000, 1024);

    private string Write(string file, string text)
    {
        var path = Path.Combine(_dir, file);
        File.WriteAllText(path, text);
        return path;
    }

    /// <summary>
    /// 🔴 INVENTED. No such object exists in the committed project — see the class remark.
    /// A hand-authored panel's tag table, in the shape the real one had.
    /// </summary>
    private static string PanelTags(params string[] tagLines) => "TAGTABLE Virtual panel\n  ROOTID 0\n  TAGS\n"
        + string.Join("\n", tagLines.Select(l => "    " + l)) + "\n";

    /// <summary>🔴 INVENTED. A block body carrying an area pointer at a stated address.</summary>
    private static string PointerBlock(string pointer, string name = "FB_Panel", int network = 3) => $"""
BLOCK FB {name}
ROOTID 0
NUMBER 501
LANGUAGE LAD
TITLE "Panel"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK {network} "Panel band"
  MOVE(EN := TRUE, IN := {pointer}) => Held

""";

    private const string PlainBlock = """
BLOCK FB FB_Nothing
ROOTID 0
NUMBER 500
LANGUAGE LAD
TITLE "Nothing"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK 1 "One rung"
  COIL Held := Held

""";

    // =============================================================================================
    // 1 — THE REAL COMMITTED ARTIFACTS. Only two things can be asked of them, and both are here.
    // =============================================================================================

    /// <summary>
    /// 🔴 <b>A correction to the plan, found by running this against the committed corpus.</b> Y1's
    /// prerequisite table says the reference project has no neighbour, on the strength of
    /// <c>DefaultTagTable.ir</c>'s three <c>%M</c> tags all sitting below base 1000. That is true of
    /// that FILE and false of the corpus: <c>HarnessMirror.ir</c> is a second tag table declaring
    /// <b>26 tags inside the served area</b>, every one of them the mirror's own. So the derivation
    /// has a real positive fixture after all — for the mirror's own occupancy — and the thing that
    /// remains invented is a FOREIGN occupant, which is the case that matters and the case the
    /// committed project genuinely does not have.
    ///
    /// <para>The consumer excludes these by container name against the closed set it already has;
    /// this producer reports them, because a producer that decided which owners do not count would be
    /// making the caller's judgement with none of the caller's information.</para>
    /// </summary>
    [Fact]
    public void TheRealCorpusYieldsTheMirrorsOwnTagsEachWithItsOwner()
    {
        var report = NeighbourRunner.Run(new[] { Corpus }, ServedArea);

        Assert.Empty(report.Refusals);
        Assert.True(report.Derived, report.Denominator);
        Assert.Equal(26, report.Neighbours.Count);
        Assert.All(report.Neighbours, n => Assert.Equal("HarnessMirror", n.Container));
        Assert.Equal(1000, report.Neighbours.Min(n => n.StartByte));
        Assert.Equal(1074, report.Neighbours.Max(n => n.EndByteExclusive));
        Assert.Contains("neighbours: 26 region(s) derived from ", report.Denominator, StringComparison.Ordinal);
        Assert.Contains(" tag table(s) + ", report.Denominator, StringComparison.Ordinal);
        Assert.Contains(" block(s)", report.Denominator, StringComparison.Ordinal);
    }

    /// <summary>
    /// The comms block's own <c>MB_HOLD_REG</c> pointer spans exactly the area. That is the area
    /// DECLARING ITSELF, not an occupant of it — reported in its own list and counted, never as a
    /// neighbour, because a producer that returned it would make every map refuse against its own
    /// window. The separation is DERIVED (span equal to the area), never a name.
    /// </summary>
    [Fact]
    public void TheAreasOwnDeclarationIsSeparatedFromItsOccupants()
    {
        var report = NeighbourRunner.Run(new[] { Corpus }, ServedArea);

        var declaration = Assert.Single(report.AreaDeclarations);
        Assert.Equal("P#M1000.0 WORD 37", declaration.Address);
        Assert.Contains("FB_Comms_ModbusServer", declaration.Owner, StringComparison.Ordinal);
        Assert.Equal(1000, declaration.StartByte);
        Assert.Equal(74, declaration.ByteLength);
    }

    /// <summary>
    /// The same pointer against a WIDER area is a strict subset of it, and therefore a genuine claim
    /// — the area it was given is not the area it serves. Reported, not silently swallowed.
    /// </summary>
    [Fact]
    public void APointerThatIsOnlyPartOfTheGivenAreaIsAClaimNotADeclaration()
    {
        var report = NeighbourRunner.Run(new[] { Corpus }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Empty(report.AreaDeclarations);
        Assert.Contains(report.Neighbours, n => n.Address == "P#M1000.0 WORD 37" && n.ByteLength == 74);
    }

    // =============================================================================================
    // 2 — THE POSITIVE DERIVATION. 🔴 EVERY FIXTURE BELOW IS INVENTED.
    // =============================================================================================

    [Fact]
    public void DerivesANeighbourFromATagTableEntryInsideTheArea()
    {
        Write("PanelTags.ir", PanelTags("PanelEnable A3 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        var claim = Assert.Single(report.Neighbours);
        Assert.Equal(1512, claim.StartByte);
        Assert.Equal(1, claim.ByteLength);
        Assert.Equal("%M1512.0", claim.Address);
        Assert.Equal(NeighbourKind.Tag, claim.Kind);
    }

    /// <summary>
    /// 🔴 The owner is not optional. A refusal that cannot say WHOSE space was hit sends the reader
    /// to the mirror, which is the one place the problem is not.
    /// </summary>
    [Fact]
    public void EveryClaimCarriesTheObjectThatDeclaresIt()
    {
        var path = Write("PanelTags.ir", PanelTags("PanelEnable A3 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var claim = Assert.Single(NeighbourRunner.Run(new[] { _dir }, WideArea).Neighbours);

        Assert.Contains("Virtual panel", claim.Owner, StringComparison.Ordinal);
        Assert.Contains("PanelEnable", claim.Owner, StringComparison.Ordinal);
        Assert.Equal("Virtual panel", claim.Container);
        Assert.Equal("PanelEnable", claim.Name);
        Assert.Equal(path, claim.File);
        Assert.Equal(4, claim.Line);
    }

    [Theory]
    [InlineData("%M1512.3", 1512, 1, "Bool")]
    [InlineData("%MB1512", 1512, 1, "Byte")]
    [InlineData("%MW1512", 1512, 2, "Word")]
    [InlineData("%MD1512", 1512, 4, "DWord")]
    public void TheSpanComesFromTheAddressWidth(string address, int start, int length, string type)
    {
        Write("PanelTags.ir", PanelTags($"PanelThing A3 : {type} @ {address} ACCESSIBLE VISIBLE WRITABLE"));

        var claim = Assert.Single(NeighbourRunner.Run(new[] { _dir }, WideArea).Neighbours);

        Assert.Equal(start, claim.StartByte);
        Assert.Equal(length, claim.ByteLength);
    }

    [Fact]
    public void DerivesANeighbourFromAnAreaPointerInABlockBody()
    {
        Write("FB_Panel.ir", PointerBlock("P#M1600.0 WORD 4"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        var claim = Assert.Single(report.Neighbours);
        Assert.Equal(NeighbourKind.AreaPointer, claim.Kind);
        Assert.Equal(1600, claim.StartByte);
        Assert.Equal(8, claim.ByteLength);
        Assert.Contains("FB_Panel", claim.Owner, StringComparison.Ordinal);
        Assert.Contains("network 3", claim.Owner, StringComparison.Ordinal);
    }

    /// <summary>
    /// The extraction is over the file's TEXT, after a successful parse gates it — deliberately, and
    /// this is the test that says why. <c>IrNetwork</c> carries about twenty statement kinds and
    /// grows; a structural walk that forgot one would drop a real claim and report a short list as
    /// clean, which is the exact shape (<c>Reachability.cs</c>'s silent <c>continue</c>) this repo
    /// has already paid for. A pointer sitting in a shape nobody taught this verb about is still
    /// found.
    /// </summary>
    [Fact]
    public void APointerIsFoundWhateverStatementShapeHoldsIt()
    {
        Write("FB_Odd.ir", """
BLOCK FC FC_Odd
ROOTID 0
NUMBER 502
LANGUAGE LAD
TITLE "Odd"

INTERFACE
  INPUT
  OUTPUT
  TEMP
    Sink : Byte
  CONSTANT

NETWORK 7 "A statement shape this verb was never taught about"
  FILLBLOCKI(EN := TRUE, IN := P#M1700.0 BYTE 6, COUNT := 1) => Sink

""");

        var claim = Assert.Single(NeighbourRunner.Run(new[] { _dir }, WideArea).Neighbours);

        Assert.Equal(1700, claim.StartByte);
        Assert.Equal(6, claim.ByteLength);
        Assert.Contains("network 7", claim.Owner, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 THE MEASURED SHAPE, REPRODUCED FROM AN INVENTED FIXTURE. 53 bool tags across registers
    /// 256–323 of a 1024-register area — bytes 1512..1647. <b>This test passing is not evidence that
    /// the derivation reproduces the real collision</b>; it is evidence that a band of that shape,
    /// written by hand here, is found and bounded. The real proof re-runs against the live corpus.
    /// </summary>
    [Fact]
    public void AnInventedPanelBandIsDerivedWholeAndBounded()
    {
        var tags = Enumerable.Range(0, 53)
            .Select(i => $"PanelBit{i} {i:X2} : Bool @ %M{1512 + (i / 8)}.{i % 8} ACCESSIBLE VISIBLE WRITABLE")
            .ToArray();
        Write("PanelTags.ir", PanelTags(tags));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Equal(53, report.Neighbours.Count);
        Assert.Equal(1512, report.Neighbours.Min(n => n.StartByte));
        Assert.Equal(1518, report.Neighbours.Max(n => n.StartByte));
        Assert.All(report.Neighbours, n => Assert.Contains("Virtual panel", n.Owner, StringComparison.Ordinal));
    }

    // =============================================================================================
    // 3 — THE NEGATIVE CONTROLS. Every one of them RUN, not written.
    // =============================================================================================

    /// <summary>
    /// A real corpus with no <c>%M</c> user in the area answers ZERO, out loud, with what it read.
    /// The zero line is the whole point: an empty result that says nothing is indistinguishable from
    /// a check that never ran.
    /// </summary>
    [Fact]
    public void ZeroNeighboursIsPrintedWithItsDenominatorNotLeftSilent()
    {
        Write("FB_Nothing.ir", PlainBlock);
        Write("PanelTags.ir", PanelTags("Elsewhere A3 : Bool @ %M4.0 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Empty(report.Neighbours);
        Assert.Equal(
            "neighbours: 0 region(s) derived from 1 tag table(s) + 1 block(s) + 0 other object(s) in 2 file(s); "
            + "0 file(s) unparseable; area %M1000..%M3047 (base 1000, 1024 register(s))",
            report.Denominator);
    }

    /// <summary>
    /// 🔴 NOTHING EXAMINED IS NEVER A PASS. An empty corpus and a corpus with no occupant are the
    /// same list and different facts, and only this separates them.
    /// </summary>
    [Fact]
    public void AnEmptyCorpusIsNotDerivedBecauseNothingWasExamined()
    {
        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Empty(report.Refusals);
        Assert.StartsWith("NOT DERIVED — 0 tag table(s) + 0 block(s) + 0 other object(s) in 0 file(s)",
            report.Denominator, StringComparison.Ordinal);
        Assert.Contains("NOTHING WAS EXAMINED", report.Denominator, StringComparison.Ordinal);
    }

    /// <summary>
    /// An unparseable file could hold the very claim the list is meant to contain, so the list is
    /// WITHHELD and the file is NAMED — never a short denominator reported as clean.
    /// </summary>
    [Fact]
    public void AnUnparseableFileIsNotDerivedAndIsNamed()
    {
        Write("PanelTags.ir", PanelTags("PanelEnable A3 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));
        Write("FB_Broken.ir", "BLOCK FB FB_Broken\nthis is not IR at all\n");

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Empty(report.Neighbours);
        Assert.Contains("FB_Broken.ir", report.NotDerivedReason, StringComparison.Ordinal);
        Assert.Contains("FB_Broken.ir", report.Denominator, StringComparison.Ordinal);
        Assert.Contains("1 of 2 file(s)", report.Denominator, StringComparison.Ordinal);
    }

    /// <summary>
    /// An over-broad derivation refuses maps that are fine, so both edges are pinned. Below the base
    /// and at-or-above the top are EXCLUDED — and COUNTED, because a claim silently dropped is a
    /// claim nobody can audit.
    /// </summary>
    [Fact]
    public void AClaimBelowTheAreaBaseIsExcludedAndCounted()
    {
        Write("PanelTags.ir", PanelTags(
            "JustBelow A3 : Word @ %MW998 ACCESSIBLE VISIBLE WRITABLE",
            "Inside A6 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.Single(report.Neighbours);
        Assert.Equal(1, report.ExcludedBelowBase);
        Assert.Equal(0, report.ExcludedAtOrAboveTop);
        Assert.Contains("1 %M claim(s) below base 1000", report.Exclusions, StringComparison.Ordinal);
    }

    [Fact]
    public void AClaimAtOrAboveTheAreaTopIsExcludedAndCounted()
    {
        Write("PanelTags.ir", PanelTags(
            "JustAbove A3 : Word @ %MW3048 ACCESSIBLE VISIBLE WRITABLE",
            "Inside A6 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.Single(report.Neighbours);
        Assert.Equal(0, report.ExcludedBelowBase);
        Assert.Equal(1, report.ExcludedAtOrAboveTop);
        Assert.Contains("1 at or above %M3048", report.Exclusions, StringComparison.Ordinal);
    }

    /// <summary>A word ENDING inside the area is inside it, however its base reads.</summary>
    [Fact]
    public void AClaimStraddlingTheBaseIsInsideTheArea()
    {
        Write("PanelTags.ir", PanelTags("Straddle A3 : Word @ %MW999 ACCESSIBLE VISIBLE WRITABLE"));

        var claim = Assert.Single(NeighbourRunner.Run(new[] { _dir }, WideArea).Neighbours);

        Assert.Equal(999, claim.StartByte);
        Assert.Equal(2, claim.ByteLength);
    }

    [Fact]
    public void ANonMarkerAreaPointerIsExcludedAndCounted()
    {
        Write("FB_Panel.ir", PointerBlock("P#DB99.DBX0.0 BYTE 2"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Empty(report.Neighbours);
        Assert.Equal(1, report.ExcludedNonMarkerPointers);
        Assert.Contains("1 area pointer(s) outside marker memory", report.Exclusions, StringComparison.Ordinal);
    }

    /// <summary>
    /// Quoted text is stripped before the scan. A comment describing an area pointer is prose, and
    /// an over-inclusive derivation refuses maps that are fine.
    /// </summary>
    [Fact]
    public void APointerInsideAQuotedCommentIsNotAClaim()
    {
        Write("FB_Panel.ir", """
BLOCK FB FB_Panel
ROOTID 0
NUMBER 501
LANGUAGE LAD
TITLE "Serves P#M1600.0 WORD 4 in an earlier revision"
COMMENT "The band at P#M1900.0 BYTE 8 was moved out of this block."

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK 1 "P#M2000.0 WORD 2 is only a title"
  COIL Held := Held

""");

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Empty(report.Neighbours);
    }

    // =============================================================================================
    // 4 — UNBOUNDABLE CLAIMS. Seen and not measurable is worse than unseen: it makes a SHORT list
    //     look complete. Refused, unless the claim provably cannot reach the area at all.
    // =============================================================================================

    [Fact]
    public void AMarkerAddressFormThisVerbCannotBoundIsRefusedNotDropped()
    {
        Write("PanelTags.ir", PanelTags("Odd A3 : Bool @ %MX1512 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("%MX1512", StringComparison.Ordinal));
        Assert.Contains(report.Refusals, r => r.Contains("Odd", StringComparison.Ordinal));
    }

    [Fact]
    public void AnAreaPointerUnitThisVerbCannotBoundIsRefused()
    {
        Write("FB_Panel.ir", PointerBlock("P#M1600.0 WIDGET 4"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("WIDGET", StringComparison.Ordinal));
    }

    /// <summary>
    /// An occupancy runs UPWARD from its start, so a claim starting at or above the area top cannot
    /// reach it however wide it is. Excluding that one is sound; refusing it would be noise.
    /// </summary>
    [Fact]
    public void AnUnboundableClaimThatCannotReachTheAreaIsExcludedNotRefused()
    {
        Write("PanelTags.ir", PanelTags("FarAway A3 : Bool @ %MX9000 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Empty(report.Refusals);
        Assert.Equal(1, report.ExcludedAtOrAboveTop);
    }

    /// <summary>
    /// The two homes of a tag's width — its address and its declared type — can disagree, and the
    /// address is what the CPU uses while the type is what a reader believes. A disagreement is
    /// refused naming both, in the same shape `served-area` refuses a desynced sidecar.
    /// </summary>
    [Fact]
    public void ATypeWiderThanItsAddressIsRefusedNamingBoth()
    {
        Write("PanelTags.ir", PanelTags("Wrong A3 : Real @ %MW1512 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("Real", StringComparison.Ordinal)
            && r.Contains("%MW1512", StringComparison.Ordinal));
    }

    /// <summary>A type this verb does not know is bounded by its address, and that is said out loud.</summary>
    [Fact]
    public void ATypeThisVerbDoesNotKnowIsBoundedByItsAddressAndCounted()
    {
        Write("PanelTags.ir", PanelTags("Exotic A3 : UDT_Panel @ %MW1512 ACCESSIBLE VISIBLE WRITABLE"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Single(report.Neighbours);
        Assert.Equal(1, report.BoundedByAddressAlone);
        Assert.Contains("1 claim(s) bounded by their address alone", report.Exclusions, StringComparison.Ordinal);
    }

    /// <summary>A `P#` this scan cannot read at all is named, never skipped past.</summary>
    [Fact]
    public void AnAreaPointerThisScanCannotReadIsRefused()
    {
        Write("FB_Panel.ir", PointerBlock("P#M1600.0"));

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("P#", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 5 — THE TWO HOMES OF A POINTER. `to-xml` rebuilds the operand FROM THE SIDECAR.
    // =============================================================================================

    /// <summary>
    /// A pointer appears twice in a block that carries a sidecar — readable and constant. One
    /// occupancy, counted once.
    /// </summary>
    [Fact]
    public void ThePointerAndItsSidecarConstantAreOneClaim()
    {
        Write("FB_Panel.ir", """
BLOCK FB FB_Panel
ROOTID 0
NUMBER 501
LANGUAGE LAD
TITLE "Panel"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK 1 "Panel band"
  MOVE(EN := TRUE, IN := P#M1600.0 WORD 4) => Held

SIDECAR
NETWORK 1
  compileunit = 3
  constant P#M1600.0 WORD 4 = 22 Any

""");

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.True(report.Derived, report.Denominator);
        Assert.Single(report.Neighbours);
    }

    /// <summary>
    /// When the two homes DISAGREE, both are reported. `served-area` is the verb that refuses the
    /// desync; this one is an occupancy list, and the honest occupancy of a desynced block is both
    /// spans — the readable one every reader believes, and the sidecar one that reaches the CPU.
    /// </summary>
    [Fact]
    public void ADesyncedPointerAndSidecarAreTwoClaims()
    {
        Write("FB_Panel.ir", """
BLOCK FB FB_Panel
ROOTID 0
NUMBER 501
LANGUAGE LAD
TITLE "Panel"

INTERFACE
  INPUT
  OUTPUT
  STATIC
    Held : Bool
  CONSTANT

NETWORK 1 "Panel band"
  MOVE(EN := TRUE, IN := P#M1600.0 WORD 4) => Held

SIDECAR
NETWORK 1
  compileunit = 3
  constant P#M1600.0 WORD 8 = 22 Any

""");

        var report = NeighbourRunner.Run(new[] { _dir }, WideArea);

        Assert.Equal(2, report.Neighbours.Count);
        Assert.Contains(report.Neighbours, n => n.ByteLength == 8);
        Assert.Contains(report.Neighbours, n => n.ByteLength == 16);
    }

    // =============================================================================================
    // 6 — THE AREA ITSELF. A question asked about nothing gets no answer.
    // =============================================================================================

    [Fact]
    public void AnAreaOfNoRegistersIsRefusedRatherThanAnswered()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerArea.OfRegisters(1000, 0));
    }

    [Fact]
    public void ANegativeBaseIsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MarkerArea.OfRegisters(-1, 4));
    }

    [Fact]
    public void AnAreaGivenInBytesIsTheSameAreaAsOneGivenInRegisters()
    {
        Assert.Equal(MarkerArea.OfRegisters(1000, 37), MarkerArea.OfBytes(1000, 74) with { Registers = 37 });
        Assert.Equal(3048, MarkerArea.OfRegisters(1000, 1024).TopByteExclusive);
    }

    /// <summary>A path naming nothing is not a corpus with nothing in it (FI-44).</summary>
    [Fact]
    public void APathThatNamesNothingIsRefused()
    {
        var report = NeighbourRunner.Run(new[] { Path.Combine(_dir, "no-such-place") }, WideArea);

        Assert.False(report.Derived);
        Assert.Contains(report.Refusals, r => r.Contains("no-such-place", StringComparison.Ordinal));
    }

    // =============================================================================================
    // 7 — THE INTERFACE `harness-batch` SEES. A document over a subprocess, and the exit code.
    // =============================================================================================

    private static (int Exit, string Output) Cli(params string[] args)
    {
        var captured = new StringWriter();
        var previous = Console.Out;
        try
        {
            Console.SetOut(captured);
            return (Program.RunNeighbours(args), captured.ToString());
        }
        finally
        {
            Console.SetOut(previous);
        }
    }

    [Fact]
    public void CliExitsZeroAndPrintsTheDenominatorOnTheZeroCase()
    {
        Write("FB_Nothing.ir", PlainBlock);

        var (exit, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024");

        Assert.Equal(0, exit);
        Assert.Contains("neighbours: 0 region(s) derived from ", output, StringComparison.Ordinal);
    }

    /// <summary>And the real corpus, through the CLI, reports its 26 with their owner.</summary>
    [Fact]
    public void CliDerivesTheRealCorpusAndNamesTheOwner()
    {
        var (exit, output) = Cli("--project", Corpus, "--base", "1000", "--registers", "37");

        Assert.Equal(0, exit);
        Assert.Contains("neighbours: 26 region(s) derived from ", output, StringComparison.Ordinal);
        Assert.Contains("tag table 'HarnessMirror' tag 'HX_ProgramVersion'", output, StringComparison.Ordinal);
        Assert.Contains("the area's own declaration, not an occupant", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CliExitsOneOnAnUnboundableClaim()
    {
        Write("PanelTags.ir", PanelTags("Odd A3 : Bool @ %MX1512 ACCESSIBLE VISIBLE WRITABLE"));

        var (exit, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024");

        Assert.Equal(1, exit);
        Assert.Contains("REFUSED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void CliExitsTwoWhenTheCorpusWasPartial()
    {
        Write("FB_Broken.ir", "BLOCK FB FB_Broken\nthis is not IR at all\n");

        var (exit, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024");

        Assert.Equal(2, exit);
        Assert.Contains("NOT DERIVED", output, StringComparison.Ordinal);
        Assert.Contains("FB_Broken.ir", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 A LIST NOBODY DERIVED OMITS THE KEY rather than emitting an empty array. An empty array
    /// reads as "no neighbours", which is the exact silent pass this verb exists to prevent.
    /// </summary>
    [Fact]
    public void JsonOmitsTheListWhenNothingWasDerived()
    {
        Write("FB_Broken.ir", "BLOCK FB FB_Broken\nthis is not IR at all\n");

        var (_, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024", "--json");

        Assert.DoesNotContain("\"neighbours\":", output, StringComparison.Ordinal);
        Assert.Contains("\"derived\": false", output, StringComparison.Ordinal);
        Assert.Contains("\"notDerived\"", output, StringComparison.Ordinal);
    }

    /// <summary>And an EARNED zero emits the empty list, which is the positive claim.</summary>
    [Fact]
    public void JsonEmitsAnEmptyListForAnEarnedZero()
    {
        Write("FB_Nothing.ir", PlainBlock);

        var (exit, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"neighbours\": []", output, StringComparison.Ordinal);
        Assert.Contains("\"derived\": true", output, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonCarriesEveryFieldTheConsumerNeedsToBuildAReservedRegion()
    {
        Write("PanelTags.ir", PanelTags("PanelEnable A3 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var (_, output) = Cli("--project", _dir, "--base", "1000", "--registers", "1024", "--json");

        foreach (var key in new[]
                 {
                     "\"owner\"", "\"kind\"", "\"container\"", "\"name\"", "\"address\"",
                     "\"startByte\"", "\"byteLength\"", "\"endByteExclusive\"", "\"file\"", "\"line\"",
                 })
        {
            Assert.Contains(key, output, StringComparison.Ordinal);
        }

        Assert.Contains("\"baseByte\": 1000", output, StringComparison.Ordinal);
        Assert.Contains("\"registers\": 1024", output, StringComparison.Ordinal);
    }

    /// <summary>
    /// The limit is written into the OUTPUT, on every run — including the derived one, whose reader
    /// is the likeliest to widen the claim.
    /// </summary>
    [Fact]
    public void EveryRunStatesWhatItCannotSee()
    {
        Write("FB_Nothing.ir", PlainBlock);
        var (_, derived) = Cli("--project", _dir, "--base", "1000", "--registers", "1024");
        Write("FB_Broken.ir", "BLOCK FB FB_Broken\nthis is not IR at all\n");
        var (_, notDerived) = Cli("--project", _dir, "--base", "1000", "--registers", "1024");

        foreach (var output in new[] { derived, notDerived })
        {
            Assert.Contains("WITHOUT DECLARING IT", output, StringComparison.Ordinal);
            Assert.Contains("an indirect access", output, StringComparison.Ordinal);
            Assert.Contains("declarations in the IR, not over execution", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CliRefusesAnAreaItWasNotGiven()
    {
        Assert.Equal(1, Cli("--project", Corpus).Exit);
        Assert.Equal(1, Cli("--base", "1000", "--registers", "37").Exit);
        Assert.Equal(1, Cli("--project", Corpus, "--base", "1000").Exit);
    }

    /// <summary>Two ways to say the same area is one way too many to leave unchecked.</summary>
    [Fact]
    public void CliRefusesBothRegistersAndBytesAtOnce()
    {
        Assert.Equal(1, Cli("--project", Corpus, "--base", "1000", "--registers", "37", "--bytes", "74").Exit);
    }

    [Fact]
    public void CliTakesARepeatedProjectScope()
    {
        var other = Path.Combine(_dir, "other");
        Directory.CreateDirectory(other);
        Write("FB_Nothing.ir", PlainBlock);
        File.WriteAllText(
            Path.Combine(other, "PanelTags.ir"),
            PanelTags("PanelEnable A3 : Bool @ %M1512.0 ACCESSIBLE VISIBLE WRITABLE"));

        var (exit, output) = Cli("--project", _dir, "--project", other, "--base", "1000", "--bytes", "2048", "--json");

        Assert.Equal(0, exit);
        Assert.Contains("\"startByte\": 1512", output, StringComparison.Ordinal);
    }
}
