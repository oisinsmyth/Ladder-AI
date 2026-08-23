using Converter.Diff;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 🔴 <b>An INSERTION used to read as "n changed, 1 added", naming networks nobody touched.</b>
///
/// <para>Networks were matched purely by <c>Number</c>. Insert one at 11 in a 20-network block and 11..20
/// all shift, so <c>--only {11}</c> exited 1 listing nine untouched networks — from the S7 modification
/// gate, whose entire job is <i>"prove the rest is identical"</i>. It was answering a question about
/// numbers while claiming to answer one about content: CLAUDE.md's CLOSED-check class, which is never
/// empty and never silent and looks healthiest when it is wrong.</para>
///
/// <para>Matching now anchors on content that is unique on both sides, and a position change is its own
/// kind. <b>A move GATES</b> — LAD executes in network order — and <c>--insert &lt;n&gt;</c> is the named
/// escape, checked against the observed shift rather than believed.</para>
/// </summary>
public class DiffInsertionTests : IDisposable
{
    private readonly List<string> _tempFiles = new();

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            try { File.Delete(path); } catch (IOException) { }
        }
    }

    private static IrNetwork Coil(int number, string title, string coilTag, string condition) =>
        new(number, title, new[] { new CoilAssignment(coilTag, new Expr.TagRef(condition)) });

    private static IrBlock Block(IReadOnlyList<IrNetwork> networks) =>
        new("0", "FB", "FB_X", 1, "LAD", null, networks);

    private string Write(IReadOnlyList<IrNetwork> networks)
    {
        var block = Block(networks);
        var sidecars = block.Networks
            .Select((n, i) => new NetworkSidecar(n.Number, (100 + i).ToString(),
                Array.Empty<SidecarAccessEntry>(), Array.Empty<CoilAssignmentSidecar>()))
            .ToArray();

        var path = Path.Combine(Path.GetTempPath(), $"diff-insert-{Guid.NewGuid():N}.ir");
        File.WriteAllText(path, IrSerializer.SerializeBlock(block, sidecars));
        _tempFiles.Add(path);
        return path;
    }

    /// <summary>Twenty networks, each with distinct content so every one is a valid anchor.</summary>
    private static List<IrNetwork> Twenty() =>
        Enumerable.Range(1, 20).Select(i => Coil(i, $"N{i}", $"Out{i}", $"In{i}")).ToList();

    /// <summary>The same twenty with a new network inserted at 11 and 11..20 renumbered to 12..21.</summary>
    private static List<IrNetwork> TwentyWithInsertAt11()
    {
        var networks = new List<IrNetwork>();
        for (var i = 1; i <= 10; i++)
            networks.Add(Coil(i, $"N{i}", $"Out{i}", $"In{i}"));

        networks.Add(Coil(11, "Inserted", "OutNew", "InNew"));

        for (var i = 11; i <= 20; i++)
            networks.Add(Coil(i + 1, $"N{i}", $"Out{i}", $"In{i}"));

        return networks;
    }

    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE DEFECT AND THE FIX, IN ONE ASSERTION.</b> The old matcher reported <b>10 changed,
    /// 1 added, 10 identical</b>. It now reports <b>1 added, 10 moved, 10 identical, 0 changed</b> —
    /// nothing "changed", because nothing did.
    /// </summary>
    [Fact]
    public void An_insertion_reports_ONE_added_and_TEN_moved_not_TEN_changed()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(TwentyWithInsertAt11()), Array.Empty<int>());

        Assert.Equal(0, report.ChangedCount);
        Assert.Equal(1, report.AddedCount);
        Assert.Equal(0, report.RemovedCount);
        Assert.Equal(10, report.MovedCount);
        Assert.Equal(10, report.IdenticalCount);

        // Every move carries where it came from, and the shift is exactly one.
        foreach (var moved in report.Networks.Where(n => n.Kind == NetworkChangeKind.Moved))
            Assert.Equal(1, moved.Number - moved.MovedFrom);
    }

    /// <summary>
    /// 🔴 <b>Undeclared, the moves still GATE.</b> The fix is that they are NAMED, not that they are
    /// forgiven — a network that runs later than it used to runs later than it used to.
    /// </summary>
    [Fact]
    public void Without_the_declaration_an_insertion_still_violates_invariance()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(TwentyWithInsertAt11()), new[] { 11 });

        Assert.True(report.HasInvarianceViolation);
        Assert.Equal(10, report.InvarianceViolations.Count);
        Assert.All(report.InvarianceViolations, v => Assert.Equal(NetworkChangeKind.Moved, v.Kind));
    }

    /// <summary>
    /// <c>--only 11 --insert 11</c>: the added network is inside the declared set, the shift matches the
    /// declaration exactly, and the check passes — over a remainder of ten it actually proved.
    /// </summary>
    [Fact]
    public void With_the_declaration_the_shift_is_accepted_and_the_remainder_is_real()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(TwentyWithInsertAt11()), new[] { 11 }, declaredInsertAt: 11);

        Assert.True(report.MovesAreDeclared);
        Assert.False(report.HasInvarianceViolation);
        Assert.Equal(10, report.IdenticalCount);
    }

    /// <summary>
    /// 🔴 <b>NEGATIVE CONTROL: the declaration is CHECKED, not believed.</b> Declaring the insertion at
    /// the wrong position leaves moves that do not fit the claim, and they gate. A flag that silenced
    /// whatever it was pointed at would be an off switch, not an escape.
    /// </summary>
    [Fact]
    public void A_declaration_at_the_WRONG_position_does_not_excuse_the_moves()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(TwentyWithInsertAt11()), new[] { 11 }, declaredInsertAt: 15);

        Assert.False(report.MovesAreDeclared);
        Assert.True(report.HasInvarianceViolation);
    }

    /// <summary>
    /// 🔴 <b>NEGATIVE CONTROL: a real content change alongside a declared insertion still gates, and the
    /// moved networks are NOT blamed for it.</b> This is the property the whole feature must not weaken —
    /// an insertion must never become cover for an edit.
    ///
    /// <para><b>DOCUMENTED LIMITATION, asserted rather than hidden: a network that is BOTH edited AND
    /// moved reports as a REMOVE plus an ADD, not as one CHANGED network.</b> Nothing can pair the two —
    /// their content differs (so the anchor declines) and their numbers differ (so the fallback declines)
    /// — and inventing a pairing would be a guess about which network the author meant. The gate is
    /// unharmed: it fires, and it names 14 and 15, which is where a reader needs to look. It is simply
    /// less tidy than a single CHANGED row, and that is the honest price of refusing to guess.</para>
    /// </summary>
    [Fact]
    public void A_content_change_under_a_declared_insertion_gates_and_does_NOT_blame_the_moves()
    {
        var edited = TwentyWithInsertAt11();

        // Network 15 in the NEW numbering was N14 before; give it different content.
        var index = edited.FindIndex(n => n.Number == 15);
        edited[index] = Coil(15, "N14", "Out14", "InSomethingElse");

        var report = DiffRunner.Run(Write(Twenty()), Write(edited), new[] { 11 }, declaredInsertAt: 11);

        Assert.True(report.HasInvarianceViolation);

        // Exactly the two networks the edit touched, and nothing else — in particular, none of the nine
        // networks that merely shifted.
        Assert.Equal(new[] { 14, 15 }, report.InvarianceViolations.Select(v => v.Number).OrderBy(n => n));
        Assert.DoesNotContain(report.InvarianceViolations, v => v.Kind == NetworkChangeKind.Moved);

        // The other nine still shifted, were still declared, and still did not gate.
        Assert.Equal(9, report.MovedCount);
        Assert.True(report.MovesAreDeclared);
    }

    /// <summary>
    /// 🔴 <b>NEGATIVE CONTROL ON THE ANCHOR ITSELF — the new false green this feature could have
    /// created.</b> Two networks with identical bodies cannot be told apart by content, so they anchor to
    /// NOTHING and fall back to number matching. A content anchor that guessed which duplicate was which
    /// would be a mis-pairing inside a gate whose job is proving identity, which is worse than the defect
    /// it replaced.
    /// </summary>
    [Fact]
    public void Duplicate_content_declines_to_anchor_and_falls_back_to_number()
    {
        // 1 and 2 are byte-identical apart from their number, which the serializer does not include.
        var before = new List<IrNetwork>
        {
            Coil(1, "Same", "OutSame", "InSame"),
            Coil(2, "Same", "OutSame", "InSame"),
            Coil(3, "N3", "Out3", "In3"),
        };

        var after = new List<IrNetwork>
        {
            Coil(1, "Same", "OutSame", "InSame"),
            Coil(2, "Same", "OutSame", "InSame"),
            Coil(3, "N3", "Out3", "InChanged"),
        };

        var report = DiffRunner.Run(Write(before), Write(after), Array.Empty<int>());

        // The duplicates are still correctly identical — the fallback compares CONTENT, it does not
        // assume a difference just because the anchor declined.
        Assert.Equal(2, report.IdenticalCount);
        Assert.Equal(0, report.MovedCount);
        Assert.Equal(1, report.ChangedCount);
    }

    /// <summary>
    /// 🔴 <b>A PURE REORDER IS TWO MOVES AND IT GATES.</b> Nothing was inserted, so there is no shift to
    /// declare and <c>--insert</c> cannot rescue it. Order is behaviour; this must never become a pass.
    /// </summary>
    [Fact]
    public void A_pure_reorder_is_reported_as_moves_and_gates()
    {
        var before = new List<IrNetwork>
        {
            Coil(1, "N1", "Out1", "In1"),
            Coil(2, "N2", "Out2", "In2"),
            Coil(3, "N3", "Out3", "In3"),
        };

        // 1 and 3 swap places; 2 stays.
        var after = new List<IrNetwork>
        {
            Coil(1, "N3", "Out3", "In3"),
            Coil(2, "N2", "Out2", "In2"),
            Coil(3, "N1", "Out1", "In1"),
        };

        var report = DiffRunner.Run(Write(before), Write(after), new[] { 1 });

        Assert.Equal(2, report.MovedCount);
        Assert.Equal(1, report.IdenticalCount);
        Assert.Equal(0, report.ChangedCount);
        Assert.True(report.HasInvarianceViolation);

        // And a declaration cannot save it: nothing was added or removed, so the shift is zero.
        var declared = DiffRunner.Run(Write(before), Write(after), new[] { 1 }, declaredInsertAt: 1);
        Assert.False(declared.MovesAreDeclared);
        Assert.True(declared.HasInvarianceViolation);
    }

    /// <summary>
    /// 🔴 <b>THE REGRESSION GUARD.</b> Everything above is about detecting motion; a matcher that reported
    /// every network as moved would satisfy several of them. An unchanged block must still be entirely
    /// identical, with no move and no violation.
    /// </summary>
    [Fact]
    public void An_UNCHANGED_block_is_entirely_identical_and_nothing_moved()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(Twenty()), new[] { 1 });

        Assert.Equal(20, report.IdenticalCount);
        Assert.Equal(0, report.MovedCount);
        Assert.Equal(0, report.ChangedCount);
        Assert.False(report.HasAnyChange);
        Assert.False(report.HasInvarianceViolation);
    }

    /// <summary>
    /// The ordinary in-place edit — the case this gate handles all day — is unaffected: one CHANGED
    /// network at its own number, no moves.
    /// </summary>
    [Fact]
    public void An_in_place_edit_is_still_a_plain_CHANGED_network()
    {
        var after = Twenty();
        after[4] = Coil(5, "N5", "Out5", "InEdited");

        var report = DiffRunner.Run(Write(Twenty()), Write(after), new[] { 5 });

        Assert.Equal(1, report.ChangedCount);
        Assert.Equal(0, report.MovedCount);
        Assert.Equal(19, report.IdenticalCount);
        Assert.False(report.HasInvarianceViolation);
    }

    /// <summary>
    /// 🔴 <b><c>--insert</c> without <c>--only</c> is REFUSED, exit 2 — nothing was judged.</b>
    ///
    /// <para><c>--insert</c> exists to modify a GATE, and without <c>--only</c> there is no gate: the
    /// report is informational and exits 0 whatever it finds. Silently accepting the flag there would let
    /// it be pasted into a command line where it does nothing, and a named escape that can sit harmlessly
    /// in a script is one step from becoming a default nobody notices.</para>
    ///
    /// <para>Exit 2 rather than 1, matching the house meaning: 1 is "judged and failed", 2 is
    /// "NOT JUDGED".</para>
    /// </summary>
    [Fact]
    public void Insert_without_only_is_refused_because_there_is_no_gate_to_modify()
    {
        var before = Write(Twenty());
        var after = Write(TwentyWithInsertAt11());

        Assert.Equal(2, Program.RunDiff(new[] { before, after, "--insert", "11" }));

        // NEGATIVE CONTROL: the same flag WITH --only is accepted and judged (exit 0 here, because the
        // declaration matches). Without this, a parser that rejected --insert outright would pass above.
        Assert.Equal(0, Program.RunDiff(new[] { before, after, "--only", "11", "--insert", "11" }));
    }

    /// <summary>The text report names the move and where it came from, rather than dumping the body twice.</summary>
    [Fact]
    public void The_text_report_names_the_move_and_its_origin()
    {
        var report = DiffRunner.Run(Write(Twenty()), Write(TwentyWithInsertAt11()), new[] { 11 }, declaredInsertAt: 11);
        var text = DiffOutputFormatter.FormatText(report);

        Assert.Contains("10 moved", text);
        Assert.Contains("MOVED network 12 \"N11\" — was network 11", text);
        Assert.Contains("DECLARED INSERTION at network 11", text);
        Assert.Contains("shifted by 1 and do not gate", text);
    }
}
