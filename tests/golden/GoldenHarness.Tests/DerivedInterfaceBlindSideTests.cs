using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// THE SECOND OPT-OUT, COUNTED. <see cref="MemoryLayoutFidelityTests"/> exists because
/// <c>Normalizer.AreSemanticallyEquivalent</c> carries an opt-out — <c>compareMemoryLayout</c> — and
/// its last <c>[Fact]</c> measures how much of the corpus sits on that opt-out's blind side. On
/// 2026-08-23, commit <c>2f7abba</c> added a SECOND, structurally identical opt-out one line below it:
/// <c>DerivedInterfacePlan.For(...)</c>, threaded into <c>Normalizer.Strip</c> as
/// <c>!derived.Drops(...)</c>. It has FOUR rules and, until this file, no counter anywhere —
/// <c>grep -rn "DerivedInterface" tests/golden/</c> returned nothing at all.
///
/// <para>*** ITS ONLY TESTS WERE <c>Converter.Tests/DerivedInterfaceExpansionTests.cs</c>, WHICH IS
/// THE SAME LANE WHOSE BLIND SPOT THE RULES CREATE. *** That is the failure mode
/// <see cref="BlockInterfaceFidelityTests"/> was built against and `tests/golden/README.md` states
/// outright. Those are good tests — each proves a rule fires where it should and does NOT fire where
/// it should not, on hand-built fixtures. What they cannot say is HOW MUCH OF THE REAL CORPUS the
/// rules are switching off, because they never look at the corpus. That is this file's only job.</para>
///
/// <para><b>This is a CENSUS, not a verdict.</b> Nothing here asserts a rule is right or wrong. Each
/// number is "how many places in the committed corpus this rule stops the comparison at", pinned to a
/// measured baseline, so that widening a rule — or the corpus growing into one — moves it and a human
/// sees the move. Over-ignoring is the one defect class that does not fail loudly: it produces a
/// permanent green. The only defence is a number somebody has to consciously raise.</para>
///
/// <para><b>🔴 AND THE MEASUREMENT, TAKEN 2026-08-23, IS THAT ALL THREE DROPPING RULES FIRE ZERO TIMES
/// ON THE PAIRED CORPUS.</b> That is not the same statement as "the rules are harmless", and the
/// difference matters more than the number does. The objects these rules were BUILT for are in
/// <c>ir/test-project001/</c> and every one of them is invisible to this suite for a different reason:
/// <list type="bullet">
/// <item><c>FB_Comms_ModbusServer.ir:13-14</c> declares <c>MbServer : MB_SERVER VERSION 5.3</c> and
/// <c>Comms : TCON_IP_v4 VERSION 1.0</c> BARE — the exact rule-1 and rule-2 shape — and it has NO
/// COMMITTED EXPORT, so <c>drift-check</c> reports it SKIPPED and nothing compares it.</item>
/// <item>Every instance DB that IS paired (<c>iDB_PusherControl</c>, <c>iDB_ShredderSequencer</c>,
/// <c>iDB_MotorFwdRevSystem_Shredder</c>, <c>iDB_HopperBlockageMonitor</c>) declares its members in
/// full, so rule 3's one-sided-empty precondition is never met. The instance DBs that DON'T declare
/// members are the harness ones (<c>iDB_Hx*</c>), and they have no export either.</item>
/// <item>The two <c>BAREPARAM</c>s the corpus does have — <c>Main.ir:10-11</c> and <c>OB100.ir</c> —
/// are <c>Bool</c>, and both rules require a NAMED type. They are correctly out of reach.</item>
/// </list>
/// So the whole measurable blind side of this opt-out sits on objects listed in
/// <see cref="CommittedBlocksRoundTripTests"/>' known-gap list. The counters are a tripwire set at the
/// measured floor: the day one of those blocks gains an export, or a rule widens, they move.</para>
///
/// <para><b>Zero is a measurement here, not an absence — and that is the one claim that has to be
/// earned.</b> A counter reading zero because nothing is wrong and a counter reading zero because the
/// probe cannot see are indistinguishable in a run log, and this project has been bitten by exactly
/// that (EMPTY IS NOT CLEAN). So the probe is demonstrated positive against real exports before any
/// zero is believed — see
/// <see cref="TheCensus_SeesADrop_WhenTheRuleIsGivenOneToPlan"/>.</para>
///
/// <para><b>Direction: <c>drift-check</c>'s, exactly.</b> Committed <c>.ir</c> → <c>to-xml</c> (no
/// <c>--synthesize</c>, sidecar left in place, the way <c>DriftCheckRunner</c> does it) against the
/// committed export. That is where the plan is actually consulted, and it is the green the plan can
/// buy.</para>
///
/// <para><b>How the drops are counted, and why not by asking the plan.</b> The plan's decisions are
/// private, and reading them back would be measuring the rules with the rules. Each export is stripped
/// TWICE through the public surface instead — once with <c>DerivedInterfacePlan.Nothing</c>, once with
/// the real plan — and whatever vanished between the two IS the blind side. It observes the EFFECT,
/// which survives the rules being rewritten.</para>
///
/// <para><b>🔴 WHAT THIS COUNTER CANNOT SEE.</b>
/// <list type="bullet">
/// <item><b>Anything without a paired export</b>, which today is the entire population the rules
/// touch. Stated first because it is the biggest hole and the counters read zero because of it.</item>
/// <item><b>A rule widened in a direction this corpus has no example of.</b> No <c>Struct</c>-bodied
/// member TIA expands differently, no <c>LReal</c> start value, no <c>BAREPARAM</c> on a named type.
/// Widening into any of those moves nothing here and stays a reviewer's job.</item>
/// <item><b>Whether a drop was CORRECT.</b> A rule firing on TIA boilerplate and a rule firing on a
/// genuine divergence count identically. This is a volume gauge, not an oracle.</item>
/// <item><b>The third leg.</b> Both sides are committed files; whether the LIVE controller agrees with
/// its own export needs Portal, which this lane does not have — the same limit
/// <see cref="ExportDriftDetectorTests"/> records against <c>DB_Settings</c>.</item>
/// </list></para>
/// </summary>
public class DerivedInterfaceBlindSideTests
{
    // The three rules that DROP content, one counter each. Rule 4 (Real-literal canonicalisation)
    // drops nothing and deliberately has no counter — see
    // RealLiteralCanonicalisation_IsArmed_AndItsBlindSideIsEmpty for the argument.
    private const string Rule1TypeExpansion = "R1 type expansion (<Sections> under a typed Member)";
    private const string Rule2SystemAttributes = "R2 system AttributeList (under a typed Member)";
    private const string Rule3InstanceDbMembers = "R3 instance-DB member list (<Member> under a top-level Section)";
    private const string Unclassified = "UNCLASSIFIED — a drop no documented rule accounts for";

    private readonly record struct Drop(string Block, string Rule, string Path);

    /// <summary>
    /// Every committed <c>.ir</c> with a paired committed export — the population <c>drift-check</c>
    /// actually judges. Same filename pairing <see cref="BlockInterfaceFidelityTests"/> uses.
    /// </summary>
    private static IEnumerable<(string Name, string IrPath, string ExportPath, string IrDir)> Pairs()
    {
        var repo = ToolPaths.RepoRoot();
        foreach (var project in new[] { "reference", "test-project001" })
        {
            var irDir = Path.Combine(repo, "ir", project);
            var xmlDir = Path.Combine(repo, "simatic-ml", project);
            if (!Directory.Exists(irDir))
            {
                continue;
            }

            foreach (var irPath in Directory.EnumerateFiles(irDir, "*.ir").OrderBy(p => p, StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(irPath);
                var exportPath = Path.Combine(xmlDir, name + ".xml");
                if (File.Exists(exportPath))
                {
                    yield return (name, irPath, exportPath, irDir);
                }
            }
        }
    }

    /// <summary>
    /// The census, computed once. Four <c>[Fact]</c>s read it; <c>to-xml</c> takes a whole file list,
    /// so the whole thing costs one converter invocation per project.
    /// </summary>
    private static readonly Lazy<(IReadOnlyList<Drop> Drops, int PairCount)> Census = new(Measure);

    private static (IReadOnlyList<Drop> Drops, int PairCount) Measure()
    {
        var pairs = Pairs().ToList();
        var drops = new List<Drop>();

        foreach (var byDir in pairs.GroupBy(p => p.IrDir, StringComparer.Ordinal))
        {
            var workDir = Path.Combine(
                Path.GetTempPath(), "derived-interface-blind-side", Path.GetFileName(byDir.Key));
            if (Directory.Exists(workDir))
            {
                Directory.Delete(workDir, recursive: true);
            }

            Directory.CreateDirectory(workDir);

            // --out is not optional: the default is beside-the-input, which here IS the committed
            // corpus (FI-72). No --synthesize and no sidecar strip — DriftCheckRunner passes
            // `synthesize: false` over the .ir as committed, and the census has to measure the plan
            // the gate actually builds, not one built from a differently-derived document.
            var args = new List<string> { "to-xml" };
            args.AddRange(byDir.Select(p => p.IrPath));
            args.Add("--project");
            args.Add(byDir.Key);
            args.Add("--out");
            args.Add(workDir);

            var result = ProcessRunner.Run(ToolPaths.ConverterExe, args.ToArray());
            Assert.True(result.ExitCode == 0,
                $"to-xml over {Path.GetFileName(byDir.Key)} failed (exit {result.ExitCode}) — " +
                $"{result.StdErr}{result.StdOut}. A census that could not build one side must FAIL, not " +
                "report a small blind side.");

            foreach (var pair in byDir)
            {
                var builtPath = Path.Combine(workDir, pair.Name + ".xml");
                Assert.True(File.Exists(builtPath),
                    $"{pair.Name}: to-xml reported success but wrote no {builtPath}. Empty is not clean.");

                var export = XDocument.Load(pair.ExportPath).Root!;
                var built = XDocument.Load(builtPath).Root!;

                drops.AddRange(DropsBetween(pair.Name, export, built));
            }
        }

        return (drops, pairs.Count);
    }

    /// <summary>
    /// Build the plan from the two documents exactly as <c>AreSemanticallyEquivalent</c> does, then
    /// apply it to <paramref name="withTheContent"/> — the side that HOLDS the material, which is
    /// where the dropped material is. Applying it to the converter's own output would count almost
    /// nothing: the converter never emits these expansions in the first place.
    /// </summary>
    private static List<Drop> DropsBetween(string block, XElement withTheContent, XElement other)
    {
        var plan = DerivedInterfacePlan.For(withTheContent, other);
        var full = Normalizer.Strip(withTheContent, false, DerivedInterfacePlan.Nothing);
        var planned = Normalizer.Strip(withTheContent, false, plan);

        var drops = new List<Drop>();
        CollectDrops(block, full, planned, string.Empty, drops);
        return drops;
    }

    /// <summary>
    /// Walk the unplanned and the planned strip of the SAME document side by side. The plan only ever
    /// REMOVES children, so the planned child list is a subsequence of the unplanned one and a greedy
    /// key match aligns them; whatever is left unmatched on the full side is a drop. Only the
    /// OUTERMOST removed element is recorded — the members inside a dropped <c>&lt;Sections&gt;</c>
    /// are one loss, not a dozen, and counting them individually would make rule 1 look like whichever
    /// type happened to be biggest.
    /// </summary>
    private static void CollectDrops(string block, XElement full, XElement planned, string path, List<Drop> into)
    {
        var plannedChildren = planned.Elements().ToList();

        var j = 0;
        foreach (var child in full.Elements())
        {
            if (j < plannedChildren.Count && Key(child) == Key(plannedChildren[j]))
            {
                CollectDrops(block, child, plannedChildren[j], Extend(path, child), into);
                j++;
                continue;
            }

            // Classified by the PARENT as well as by the vanished element's name: every rule is a
            // statement about what a Member or a Section carries, so a <Sections> or an
            // <AttributeList> disappearing from anywhere else is not one of them and must not be
            // silently filed under one.
            var rule = child.Name.LocalName switch
            {
                "Sections" when full.Name.LocalName == "Member" => Rule1TypeExpansion,
                "AttributeList" when full.Name.LocalName == "Member" => Rule2SystemAttributes,
                "Member" when full.Name.LocalName == "Section" => Rule3InstanceDbMembers,
                _ => Unclassified,
            };

            into.Add(new Drop(block, rule, Extend(path, child)));
        }
    }

    private static string Key(XElement element) =>
        element.Name.LocalName + "|" + ((string?)element.Attribute("Name") ?? string.Empty);

    private static string Extend(string path, XElement element) => path + "/" + Key(element);

    private static IReadOnlyList<Drop> DropsFor(string rule) =>
        Census.Value.Drops.Where(d => d.Rule == rule)
            .OrderBy(d => d.Block + d.Path, StringComparer.Ordinal)
            .ToList();

    private static string Describe(IReadOnlyList<Drop> drops) =>
        drops.Count == 0
            ? "(none)"
            : string.Join(", ", drops.GroupBy(d => d.Block, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => $"{g.Key}×{g.Count()}"));

    /// <summary>
    /// The population guard — the half of "empty is not clean" that belongs on a CORPUS enumeration
    /// rather than on a problem list (the distinction <c>4593a87</c> drew: a population must never be
    /// empty; a list of things wrong with it should be trying to be). A census over no pairs would
    /// make all three counters read zero for the least interesting reason there is.
    /// </summary>
    [Fact]
    public void TheCensus_ExaminedThePairedCorpus()
    {
        var (_, pairCount) = Census.Value;

        Assert.True(pairCount > 0,
            "no committed .ir has a paired export, so the census compared nothing and every count below " +
            "is vacuous. Empty is not clean.");
    }

    /// <summary>
    /// *** THE POSITIVE CONTROL, AND THE THREE ZEROS BELOW ARE WORTH NOTHING WITHOUT IT. *** Each
    /// counter currently reads zero. A zero from "the rules never fire on this corpus" and a zero from
    /// "the walk cannot see a drop" look identical in a run log, and only one of them is a fact about
    /// the corpus.
    ///
    /// <para>So each rule is handed something to plan: a REAL committed export, and a copy of it with
    /// exactly the one thing that rule keys on removed — a typed member's <c>&lt;Sections&gt;</c>, its
    /// system <c>&lt;AttributeList&gt;</c>, an instance DB section's member list. That is precisely the
    /// one-sided absence each rule exists for, built out of TIA's own document rather than a fixture,
    /// and the census must both SEE the drop and FILE IT UNDER THE RIGHT RULE.</para>
    ///
    /// <para>The unmutated half is asserted too: the same export against ITSELF must produce no drops.
    /// Without it a walk that reported everything as dropped would pass this test.</para>
    /// </summary>
    [Theory]
    [InlineData(Rule1TypeExpansion)]
    [InlineData(Rule2SystemAttributes)]
    [InlineData(Rule3InstanceDbMembers)]
    public void TheCensus_SeesADrop_WhenTheRuleIsGivenOneToPlan(string rule)
    {
        var fixture = FindFixture(rule);
        Assert.True(fixture.Name is not null,
            $"no committed export provides a fixture for {rule}, so the probe for it is untested and " +
            "its zero below means nothing. Empty is not clean.");

        var original = XDocument.Parse(fixture.Doc!.ToString()).Root!;
        var self = DropsBetween(fixture.Name!, original, XDocument.Parse(fixture.Doc.ToString()).Root!);
        Assert.True(self.Count == 0,
            $"{fixture.Name}: a document compared against an identical copy of itself produced " +
            $"{self.Count} drop(s) [{string.Join(", ", self.Select(d => d.Rule + d.Path))}]. Nothing is " +
            "one-sidedly absent, so nothing may be planned — the walk is reporting drops that the plan " +
            "did not make, and every count in this file would be inflated by it.");

        var mutated = XDocument.Parse(fixture.Doc.ToString()).Root!;
        Mutate(rule, mutated);

        var drops = DropsBetween(fixture.Name!, original, mutated);

        Assert.True(drops.Count > 0,
            $"{fixture.Name}: the one thing {rule} keys on was removed from one side and the census saw " +
            "no drop at all. Either the rule no longer fires or the walk cannot detect one; either way " +
            "the baselines below are measuring nothing.");

        Assert.True(drops.All(d => d.Rule == rule),
            $"{fixture.Name}: expected every drop to be filed under {rule}, got " +
            $"[{string.Join(", ", drops.Select(d => d.Rule).Distinct())}]. Misfiling is worse than not " +
            "counting: it moves one rule's number when a different rule widened.");
    }

    /// <summary>
    /// RULE 1 — a typed member's <c>&lt;Sections&gt;</c> expansion, dropped when only one side carries
    /// it and the carrying side names a type.
    ///
    /// <para>🔴 BLIND TO (the rule's own words, restated as what this number would be the size of): a
    /// member declared bare whose expansion in the controller is not what the type says it should be.
    /// The escape is that such a divergence is still visible at the TYPE's own object — but only if
    /// that type is in the corpus, so every count here would be a place relying on that indirection.</para>
    ///
    /// <para>*** COUNTED IN SITES, NOT BLOCKS, AND THAT IS THE DIFFERENCE FROM THE MemoryLayout
    /// COUNTER. *** A layout is a per-object BOOLEAN — a file declares one or it does not, so counting
    /// files counts the whole quantity. A derived-expansion opt-out is a per-SITE quantity: a rule
    /// widened from "typed members" to "all members" would fire many more times inside the SAME blocks
    /// and a block count would not move at all.</para>
    /// </summary>
    [Fact]
    public void TypeExpansionDrops_MatchTheMeasuredBlindSide()
    {
        // MEASURED 2026-08-23 over 41 pairs, corpus as committed at 281dd4e, the day 2f7abba added the
        // rule. ZERO, and for a reason worth reading rather than assuming: the corpus's only bare typed
        // members are `MbServer : MB_SERVER VERSION 5.3` and `Comms : TCON_IP_v4 VERSION 1.0` in
        // FB_Comms_ModbusServer.ir, WHICH HAS NO COMMITTED EXPORT and is SKIPPED by drift-check. The two
        // BAREPARAMs that are paired (Main.ir:10-11, OB100.ir) are Bool, and the rule requires a named
        // type. So this is a tripwire at the floor, not a clean bill of health.
        const int MeasuredBlindSide = 0;

        var drops = DropsFor(Rule1TypeExpansion);

        Assert.True(drops.Count == MeasuredBlindSide,
            $"R1 type-expansion drops: {drops.Count}, measured baseline {MeasuredBlindSide}. " +
            $"By block: {Describe(drops)}.\n" +
            "UP = the rule was widened, or the corpus gained a bare typed member (the likeliest single " +
            "cause is FB_Comms_ModbusServer finally gaining an export): that much of the interface is " +
            "now outside every Normalizer-based comparison — drift-check, compare, the parity oracle. " +
            "Say which and raise the baseline deliberately.\n" +
            "DOWN is impossible from 0; if you see it, the census stopped examining something.");
    }

    /// <summary>
    /// RULE 2 — the system-maintained <c>&lt;AttributeList&gt;</c> on a typed member, dropped when only
    /// one side carries it.
    ///
    /// <para>🔴 THIS IS THE RULE WITH A REAL LOSS INSIDE IT, and it says so itself:
    /// <c>SystemDefined="true"</c> means "TIA maintains this slot", NOT "TIA chose the value" — an
    /// author-set <c>SETPOINT</c> reads back in exactly that form. What makes the drop defensible is
    /// the ABSENCE on the other side: the <c>BAREPARAM</c> IR shape cannot state these attributes, so
    /// there is nothing to hold it to. That defence expires the moment <c>BAREPARAM</c> learns to carry
    /// <c>SETPOINT</c>, and this number is what says how much would be at stake when it does.</para>
    /// </summary>
    [Fact]
    public void SystemAttributeListDrops_MatchTheMeasuredBlindSide()
    {
        // MEASURED 2026-08-23 over 41 pairs, corpus at 281dd4e. ZERO, same population reason as R1: the
        // paired corpus has no member that is typed on one side and attribute-less on the other. Every
        // paired instance DB declares its members WITH their SETPOINT, so both sides carry a list and
        // the comparison stays armed.
        const int MeasuredBlindSide = 0;

        var drops = DropsFor(Rule2SystemAttributes);

        Assert.True(drops.Count == MeasuredBlindSide,
            $"R2 system-AttributeList drops: {drops.Count}, measured baseline {MeasuredBlindSide}. " +
            $"By block: {Describe(drops)}.\n" +
            "UP = that many members whose External*/SetPoint attributes are no longer compared. If the " +
            "rise came from RELAXING one of the rule's three narrowings — all-children-SystemDefined, " +
            "named-type-only, one-sided-only — stop: those three are the whole of what keeps an " +
            "author-set attribute inside the comparison.");
    }

    /// <summary>
    /// RULE 3 — an instance DB's whole member list, dropped when its <c>.ir</c> declares
    /// <c>INSTANCEOF &lt;FB&gt;</c> with an empty <c>MEMBERS</c> and the export carries TIA's
    /// regeneration of it.
    ///
    /// <para>🔴 BLIND TO: the entire member list of such a DB. By volume this is far the largest of the
    /// three — one site here is one member of a whole regenerated interface — which is why it is
    /// counted separately rather than summed with the others. What survives is <c>InstanceOfName</c>, a
    /// plain element the plan never touches, so the FB the DB CLAIMS to instantiate is still compared;
    /// TIA's derivation from it is not.</para>
    ///
    /// <para>Self-sharpening, like the MemoryLayout rule: an instance DB whose <c>.ir</c> DOES declare
    /// its members is compared member by member, and this count falls as the corpus moves that way. It
    /// has already moved that way — which is why the number is zero.</para>
    /// </summary>
    [Fact]
    public void InstanceDbMemberListDrops_MatchTheMeasuredBlindSide()
    {
        // MEASURED 2026-08-23 over 41 pairs, corpus at 281dd4e. ZERO because all four PAIRED instance
        // DBs (iDB_PusherControl, iDB_ShredderSequencer, iDB_MotorFwdRevSystem_Shredder,
        // iDB_HopperBlockageMonitor) declare their members in full. The instance DBs that declare none
        // are the harness ones, iDB_Hx*/iDB_HarnessViolationLatch/iDB_HopperBlockageStim, and none of
        // them has an export — so the rule's whole live population is outside this census.
        const int MeasuredBlindSide = 0;

        var drops = DropsFor(Rule3InstanceDbMembers);

        Assert.True(drops.Count == MeasuredBlindSide,
            $"R3 instance-DB member-list drops: {drops.Count}, measured baseline {MeasuredBlindSide}. " +
            $"By block: {Describe(drops)}.\n" +
            "Each site is one member of an instance DB's interface leaving the comparison, and they " +
            "arrive a whole section at a time, so this number does not move by one. UP = an iDB .ir " +
            "stopped declaring its members, or a member-less one gained an export. DOWN = an .ir now " +
            "declares them and is being compared member by member.");
    }

    /// <summary>
    /// Nothing the plan drops may fall outside the four documented rules. The census classifies by the
    /// element that vanished AND its parent, so a fifth rule added without a counter lands here rather
    /// than nowhere — which is the state rules 1-4 were in until this file existed.
    /// </summary>
    [Fact]
    public void NoDropIsUnclassified()
    {
        var drops = DropsFor(Unclassified);

        Assert.True(drops.Count == 0,
            $"DerivedInterfacePlan dropped {drops.Count} element(s) that none of the documented rules " +
            $"describes: {string.Join(", ", drops.Select(d => d.Block + d.Path))}.\n" +
            "A rule was added without a counter. Give it one — an ignore with no number against it is " +
            "the thing this file exists because of.");
    }

    /// <summary>
    /// RULE 4 GETS NO COUNTER, AND THAT IS A DECISION RATHER THAN AN OVERSIGHT. The other three rules
    /// DROP content, so each has a blind side with a SIZE and the size is the thing worth watching.
    /// <c>NumericLiteral</c> drops nothing: it rewrites <c>0.10</c> to <c>0.1</c> on BOTH sides, and
    /// <c>0.1</c> against <c>0.2</c> stays a difference. Its blind side is empty by construction — the
    /// same property that makes <see cref="Normalizer"/>'s Access/Part content-keys rewrites rather
    /// than strips — so a count of how often it fired would measure ACTIVITY, not EXPOSURE, and would
    /// sit beside three numbers meaning something else entirely. A number that has to be interpreted
    /// differently from its neighbours is a number that gets read wrong.
    ///
    /// <para>What it gets instead is the assertion that the property excusing it is TRUE ON A REAL TIA
    /// ARTIFACT, not only on the hand-built fixtures in
    /// <c>Converter.Tests/DerivedInterfaceExpansionTests</c> — which is this suite's whole reason for
    /// existing. Modelled on
    /// <see cref="MemoryLayoutFidelityTests.MemoryLayoutValue_IsCarriedThrough_NotHardcoded"/>: a real
    /// committed export with exactly one literal edited, so the structure and the member are TIA's.</para>
    ///
    /// <para>Both halves are load-bearing. Trailing zeros must COMPARE EQUAL, or the canonicalisation
    /// is not armed on this corpus and the empty-blind-side claim is untested here. A different value
    /// must still DIFFER, or it is not a canonicalisation at all — it is an ignore wearing one's
    /// clothes, and it would need a counter after all.</para>
    /// </summary>
    [Fact]
    public void RealLiteralCanonicalisation_IsArmed_AndItsBlindSideIsEmpty()
    {
        var fixture = ExportRoundTripRunner.CommittedExports()
            .Select(e => (e.Name, Doc: XDocument.Load(e.ExportPath)))
            .Select(e => (e.Name, e.Doc, Value: e.Doc.Descendants()
                .FirstOrDefault(x => x.Name.LocalName == "StartValue"
                    && string.Equals((string?)x.Parent?.Attribute("Datatype"), "Real", StringComparison.Ordinal)
                    && x.Value.Contains('.', StringComparison.Ordinal))))
            .FirstOrDefault(e => e.Value is not null);

        Assert.True(fixture.Value is not null,
            "no committed export carries a Real member with a decimal <StartValue>, so this test has no " +
            "fixture and is measuring nothing about the Real-literal rule. Empty is not clean.");

        var original = fixture.Doc;
        var literalPath = PathOf(fixture.Value!);
        var literal = fixture.Value!.Value;

        var withTrailingZeros = XDocument.Parse(original.ToString());
        ElementAt(withTrailingZeros, literalPath).Value = literal + "00";

        Assert.True(Normalizer.AreSemanticallyEquivalent(original, withTrailingZeros),
            $"{fixture.Name}: '{literal}' and '{literal}00' are the same decimal number and compared as " +
            "different. The Real-literal canonicalisation is not reaching a real TIA export's " +
            "<StartValue> — and a re-rendered Real literal in a real export is the exact form the " +
            "deployment defect 2f7abba closed was measured in.");

        var bumped = Bump(literal);
        var withADifferentValue = XDocument.Parse(original.ToString());
        ElementAt(withADifferentValue, literalPath).Value = bumped;

        Assert.False(Normalizer.AreSemanticallyEquivalent(original, withADifferentValue),
            $"{fixture.Name}: the start value was changed from '{literal}' to '{bumped}' and the " +
            "comparison still says EQUIVALENT. Then rule 4 is not a value-preserving canonicalisation, " +
            "it is an ignore over Real literals — and an ignore belongs beside the other three with a " +
            "counter against it, not here.");
    }

    // ------------------------------------------------------------------------------- fixtures

    /// <summary>
    /// The first committed export that gives <paramref name="rule"/> something to key on. Real TIA
    /// documents throughout — the mutation is a REMOVAL of one element, so everything that remains is
    /// TIA's own.
    /// </summary>
    private static (string? Name, XDocument? Doc) FindFixture(string rule)
    {
        foreach (var export in ExportRoundTripRunner.CommittedExports())
        {
            var doc = XDocument.Load(export.ExportPath);
            if (Target(rule, doc.Root!) is not null)
            {
                return (export.Name, doc);
            }
        }

        return (null, null);
    }

    private static void Mutate(string rule, XElement root)
    {
        var target = Target(rule, root)!;
        if (rule == Rule3InstanceDbMembers)
        {
            // The whole member list, because that is the one-sided absence rule 3 describes: an .ir
            // declaring INSTANCEOF with an EMPTY MEMBERS. Removing one member would be a member
            // present on one side only, which the plan deliberately refuses to touch.
            target.Elements().Where(e => e.Name.LocalName == "Member").Remove();
            return;
        }

        target.Remove();
    }

    /// <summary>The element a rule keys on, or null if this document cannot exercise the rule.</summary>
    private static XElement? Target(string rule, XElement root) => rule switch
    {
        Rule1TypeExpansion => root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Sections"
                && e.Parent?.Name.LocalName == "Member"
                && NamesAType(e.Parent)),

        Rule2SystemAttributes => root.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "AttributeList"
                && e.Parent?.Name.LocalName == "Member"
                && NamesAType(e.Parent)
                && e.HasElements
                && e.Elements().All(a => a.Name.LocalName == "BooleanAttribute"
                    && string.Equals((string?)a.Attribute("SystemDefined"), "true", StringComparison.Ordinal))),

        // Top-level section of an instance DB: no Section or Member above it, matching the rule's own
        // one-path-segment restriction without re-deriving InterfacePath here.
        Rule3InstanceDbMembers => root.DescendantsAndSelf()
                .Any(e => e.Name.LocalName == "SW.Blocks.InstanceDB")
            ? root.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "Section"
                    && e.Attribute("Name") is not null
                    && e.Elements().Any(m => m.Name.LocalName == "Member")
                    && !e.Ancestors().Any(a => a.Name.LocalName is "Section" or "Member"))
            : null,

        _ => null,
    };

    // Same test DerivedInterfacePlan applies: a quoted project type ("UDT_Valve") or a versioned
    // system/library type (MB_SERVER 5.3). Deliberately a restatement and not a call into the
    // converter — a fixture picked by the code under test would find only what that code already sees.
    private static bool NamesAType(XElement member) =>
        member.Attribute("Version") is not null
        || ((string?)member.Attribute("Datatype"))?.Contains('"') == true;

    /// <summary>Positional address of an element, so the same node can be found in a re-parsed copy.</summary>
    private static IReadOnlyList<int> PathOf(XElement element)
    {
        var indices = new List<int>();
        for (var node = element; node.Parent is not null; node = node.Parent)
        {
            indices.Insert(0, node.Parent.Elements().ToList().IndexOf(node));
        }

        return indices;
    }

    private static XElement ElementAt(XDocument doc, IReadOnlyList<int> path)
    {
        var element = doc.Root!;
        foreach (var index in path)
        {
            element = element.Elements().ElementAt(index);
        }

        return element;
    }

    /// <summary>
    /// Change the VALUE, not the rendering: step the last fractional digit. Deliberately not "append a
    /// digit" — that is the trailing-zero case, which must compare EQUAL.
    /// </summary>
    private static string Bump(string literal)
    {
        var last = literal[^1];
        return literal[..^1] + (last == '9' ? '8' : (char)(last + 1));
    }
}
