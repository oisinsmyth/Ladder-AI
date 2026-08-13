using System.Text;
using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// A block's <c>&lt;Interface&gt;</c> compared RAW against its real TIA export — deliberately NOT
/// through <see cref="Normalizer"/>, for the same reason <see cref="WireEndpointDirectionTests"/>
/// reads raw wire order: *** THE NORMALIZER THROWS THIS ELEMENT AWAY. ***
///
/// <para><c>Normalizer.IsVolatile</c> drops any <c>&lt;Interface&gt;</c> that has no
/// <c>&lt;Section Name="Static"&gt;</c>. Its comment justifies that with
/// "BlockSourceParser.RequireDefaultInterface already hard-errored upstream if it was anything but the
/// standard parameterless-FC boilerplate, so by the time a document reaches here it's known-inert" —
/// and that premise no longer holds. Two whole classes of block now reach the comparison with real,
/// non-boilerplate content in exactly that element:</para>
///
/// <list type="bullet">
/// <item><b>An FC with parameters.</b> <c>simatic-ml/reference/ScaleValue.xml</c> declares
/// <c>RawInput : Real</c>, <c>ScaleFactor : Real</c>, <c>ScaledResult : Real</c> and has no Static
/// section, so the whole declaration is discarded. MEASURED 2026-08-13: changing <c>ScaleFactor</c>
/// to <c>Int</c> in the IR and deriving the XML, <c>converter compare</c> against the real export
/// returns <c>VERDICT: EQUIVALENT</c>.</item>
/// <item><b>A UDT.</b> Its interface is <c>&lt;Section Name="None"&gt;</c> — no Static section either —
/// and *** A UDT IS NOTHING BUT ITS INTERFACE ***, so the comparison is left with the name and the
/// block number and reports MATCH on anything. MEASURED 2026-08-13, and it is not hypothetical:
/// <c>ir/test-project001/UDT_PusherIO.ir</c> and <c>UDT_ShredderSequencerIO.ir</c> each carry a
/// member (<c>AutoStartSignal : Bool</c>) that is ABSENT from their committed exports, and
/// <c>converter drift-check</c> prints <c>MATCH</c> for both. <see cref="ExportDriftDetectorTests"/>
/// inherits that verdict and has been green over the drift the whole time.</item>
/// </list>
///
/// <para>This is the failure mode the working agreement names — a check that shares its subject's
/// blind spot. The converter is responsible for writing interfaces; the comparison that is supposed
/// to police it discards them, so the two cancel and nothing goes red. Fixing
/// <c>Normalizer.IsVolatile</c> is the converter lane's call and would change every comparison in the
/// toolchain at once; this suite's job is to have an authority that does not depend on it.</para>
///
/// <para>Direction: committed <c>.ir</c> → <c>to-xml --synthesize</c> → compare against the real
/// export, i.e. the same direction as <c>drift-check</c>, because that is where the blindness bites.
/// The expected value is read straight out of TIA's own file with <see cref="XDocument"/> — no
/// converter code produces it — so a defect that is consistent across our read and write paths
/// cannot cancel here.</para>
/// </summary>
public class BlockInterfaceFidelityTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;

    public BlockInterfaceFidelityTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Blocks whose regenerated Interface does NOT match its committed export today, each with the
    /// reason. Asserted in BOTH directions: a block not listed here must match, and a block listed
    /// here must still differ — so a closed gap cannot sit here pretending to still be one.
    /// </summary>
    private static readonly Dictionary<string, string> KnownInterfaceDrift = new(StringComparer.Ordinal)
    {
        // ---- 🔴 SIX ENTRIES REMOVED HERE, 2026-08-13 - THE REPAIRS LANDED ----------------------
        //
        // `UDT_PusherIO` and `UDT_ShredderSequencerIO` were FOUND BY THIS TEST: each declared an
        // `AutoStartSignal : Bool` member (plus C-115 member comments) that its committed export did
        // not, while `drift-check` reported MATCH for both, because the Normalizer discarded a UDT's
        // whole Interface. Owner ruling (b): the IR was right and the EXPORT was stale - the 2026-07-17
        // task-09 pass had never been carried into the corpus. A lad-coder lane exported the live
        // project, found task 09 ALREADY IN THE CONTROLLER (so the repair was re-export only, nothing
        // to import), and committed six fresh .xml files (`f0fb0cb`). Both now match, INCLUDING the
        // member - its return was the repair, not new drift.
        //
        // `FB_PusherControl` and `FB_ShredderSequencer` cleared on the same re-export.
        //
        // `iDB_MotorFwdRevSystem_Shredder`, `iDB_PusherControl`, `iDB_ShredderSequencer` were never
        // listed here (this suite compares the Interface, and their defect was inside it) but are worth
        // naming: their drift was OUR writer omitting the empty <Section Name="InOut"/> that every real
        // TIA instance-DB export declares - fixed in converter `f2a548a`.
        //
        // Left as the record of what the staleness guard is for: all four of these went RED as
        // "listed ... but its interface now MATCHES", which is how this lane learned the repair had
        // landed rather than being told.

        // ---- a provenance difference, not a content one -------------------------------------
        // Four committed exports (AlarmWords, CommsProcessData, EquipmentStatus, NodeStatusAlarms)
        // are the 2026-07-10 seed artifacts and carry no <DocumentInfo> envelope — they were trimmed
        // of TIA's scaffolding. On this one, an FC, the <Interface> went with it: the export has NO
        // Interface element at all, while a regenerated FC always emits the standard boilerplate.
        // An ADDITION of TIA's own defaults, not a loss of real content. Kept listed rather than
        // filtered out, so the trim stays visible.
        ["NodeStatusAlarms"] = "committed export is an early TRIMMED artifact with no <DocumentInfo> and no <Interface> at all; the regenerated block emits the standard FC boilerplate.",
    };

    /// <summary>Every committed <c>.ir</c> in a covered project that has a paired committed export.</summary>
    private static IEnumerable<(string Name, string IrPath, string ExportPath)> Pairs()
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
                    yield return (name, irPath, exportPath);
                }
            }
        }
    }

    public static IEnumerable<object[]> PairedBlocks() =>
        Pairs().Select(p => new object[] { p.Name, p.IrPath, p.ExportPath });


    // ---------------------------------------------------------------------------------------
    // The raw comparison. Deliberately dumber than Normalizer.Strip: local element names,
    // attributes sorted by name (attribute ORDER is not meaningful in XML, everything else here
    // is), children in DOCUMENT ORDER (a member list is ordered — it lays out memory), text
    // trimmed. No ignore list of any kind: the whole point is that nothing gets to be exempt.
    // ---------------------------------------------------------------------------------------
    internal static string? CanonicalInterface(XDocument doc)
    {
        var iface = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Interface");
        return iface is null ? null : Canonical(iface);
    }

    private static string Canonical(XElement root)
    {
        var builder = new StringBuilder();

        void Walk(XElement node, int depth)
        {
            builder.Append(' ', depth * 2).Append(node.Name.LocalName).Append(" [");
            builder.Append(string.Join(
                " ",
                node.Attributes()
                    .OrderBy(a => a.Name.LocalName, StringComparer.Ordinal)
                    .Select(a => $"{a.Name.LocalName}={a.Value}")));
            builder.Append(']');

            if (!node.HasElements)
            {
                var text = node.Value.Trim();
                if (text.Length > 0)
                {
                    builder.Append(' ').Append(text);
                }
            }

            builder.Append('\n');
            foreach (var child in node.Elements())
            {
                Walk(child, depth + 1);
            }
        }

        Walk(root, 0);
        return builder.ToString();
    }

    /// <summary>
    /// Derives SimaticML from a committed <c>.ir</c> exactly the way <c>drift-check</c> and
    /// <see cref="SynthesisParityRunner"/> do. Every failure here is a FAILURE, never a skip — a
    /// derivation that produced nothing must not read as a block whose interface matched.
    /// </summary>
    private static XDocument DeriveXml(string name, string irPath)
    {
        var irDir = Path.GetDirectoryName(irPath)!;
        var workDir = Path.Combine(Path.GetTempPath(), "interface-fidelity", name);
        if (Directory.Exists(workDir))
        {
            Directory.Delete(workDir, recursive: true);
        }

        Directory.CreateDirectory(workDir);

        var readablePath = Path.Combine(workDir, name + ".ir");
        File.WriteAllText(readablePath, SynthesisParityRunner.StripSidecar(File.ReadAllText(irPath)));

        var result = ProcessRunner.Run(
            ToolPaths.ConverterExe, "to-xml", readablePath, "--synthesize", "--project", irDir, "--out", workDir);
        Assert.True(result.ExitCode == 0,
            $"{name}: to-xml --synthesize failed (exit {result.ExitCode}) — {result.StdErr}{result.StdOut}");

        var xmlPath = Path.Combine(workDir, name + ".xml");
        Assert.True(File.Exists(xmlPath),
            $"{name}: to-xml reported success but wrote no {xmlPath}. Empty is not clean — this must fail, " +
            "not pass as 'nothing differed'.");

        return XDocument.Load(xmlPath);
    }

    [Theory]
    [MemberData(nameof(PairedBlocks))]
    public void RegeneratedInterface_MatchesTheRealExport(string block, string irPath, string exportPath)
    {
        var expected = CanonicalInterface(XDocument.Load(exportPath));
        var actual = CanonicalInterface(DeriveXml(block, irPath));

        var matches = string.Equals(expected, actual, StringComparison.Ordinal);
        var known = KnownInterfaceDrift.TryGetValue(block, out var reason);

        if (!known)
        {
            Assert.True(matches,
                $"{block}: the interface derived from the committed .ir does not match its real TIA export, " +
                "and NOTHING ELSE IN THIS PROJECT WOULD SAY SO — the Normalizer discards this element, so " +
                "drift-check/compare/the parity suite all report a match (see this class's summary)." +
                $"{Environment.NewLine}--- export ---{Environment.NewLine}{expected ?? "(no Interface element)"}" +
                $"{Environment.NewLine}--- derived from .ir ---{Environment.NewLine}{actual ?? "(no Interface element)"}");
            return;
        }

        // Staleness guard: a listed block that now matches must be delisted, or the list starts
        // documenting a world that no longer exists.
        Assert.False(matches,
            $"{block} is listed in KnownInterfaceDrift ({reason}) but its interface now MATCHES its export. " +
            "Remove the entry — it is covered by the assertion above now.");
    }

    /// <summary>
    /// The did-not-run case. A Theory over an empty population passes silently, and a
    /// <c>KnownInterfaceDrift</c> entry naming a block that is no longer in the corpus would sit
    /// there forever asserting nothing.
    /// </summary>
    [Fact]
    public void ThePopulation_IsNotEmpty()
    {
        var pairs = Pairs().ToList();
        Assert.True(pairs.Count > 0,
            "no committed .ir / simatic-ml export pairs found — empty is not clean, and an empty Theory " +
            "population is indistinguishable in the run output from a corpus that all passed.");
    }

    /// <summary>
    /// A [Fact] over a KNOWN-PROBLEM LIST, deliberately: as a [Theory] it would fail with "No data
    /// found" the moment the list empties, which is the state we are working towards. Sibling of the
    /// same defect in CommittedBlocksRoundTripTests, which did exactly that on 2026-08-13.
    /// </summary>
    [Fact]
    public void EveryKnownDriftEntry_NamesABlockThatIsStillInTheCorpus()
    {
        var names = Pairs().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        var orphans = KnownInterfaceDrift.Keys
            .Where(b => !names.Contains(b))
            .OrderBy(b => b, StringComparer.Ordinal)
            .ToList();

        Assert.True(orphans.Count == 0,
            $"KnownInterfaceDrift names block(s) with no committed .ir/export pair: [{string.Join(", ", orphans)}]. " +
            "Remove them — an entry for a block that is not in the corpus can never come back into sync.");
    }

    /// <summary>
    /// THE MEASUREMENT — the reason this file compares raw instead of asking the Normalizer.
    ///
    /// Real corpus content, one attribute changed: <c>ScaleValue</c>'s <c>ScaleFactor</c> parameter
    /// retyped <c>Real</c> → <c>Int</c>. That changes what the block IS; TIA compiles it differently
    /// and the FlgNet is untouched, so no wiring comparison can see it either.
    ///
    /// <para>DELIBERATELY ASSERTS NOTHING ABOUT THE NORMALIZER'S VERDICT. It returned <c>true</c> here
    /// when this file was written (2026-08-13) — a code block's whole Interface was discarded — and the
    /// converter lane closed that the same day. The point of a raw guard is that it holds under BOTH
    /// contracts and does not have to be rewritten when the instrument changes, so the Normalizer's
    /// answer is REPORTED and not asserted. What is asserted is the thing this suite owns: that the raw
    /// comparison sees the retype. That assertion has been shown to go red (negative-tested by
    /// neutering the canonicalizer).</para>
    /// </summary>
    [Fact]
    public void RawComparison_SeesAnFcParameterRetype_WhereTheNormalizerDoesNot()
    {
        var exportPath = Path.Combine(ToolPaths.RepoRoot(), "simatic-ml", "reference", "ScaleValue.xml");
        Assert.True(File.Exists(exportPath), $"missing real export {exportPath} — empty is not clean");

        var real = XDocument.Load(exportPath);
        var mutated = new XDocument(real);

        var member = mutated.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Member" && (string?)e.Attribute("Name") == "ScaleFactor");
        Assert.True(member is not null,
            "ScaleValue's export no longer declares a 'ScaleFactor' parameter — this test's fixture is gone, " +
            "so it is measuring nothing. Re-point it at another real FC with a typed parameter.");
        Assert.Equal("Real", (string?)member!.Attribute("Datatype"));
        member.SetAttributeValue("Datatype", "Int");

        _output.WriteLine(
            "Normalizer.AreSemanticallyEquivalent on an FC parameter retyped Real -> Int: " +
            $"{Normalizer.AreSemanticallyEquivalent(real, mutated)} " +
            "(true = the Interface is being discarded, which is what this file was built for; " +
            "false = the converter lane's IsVolatile fix is in effect. Reported, not asserted.)");

        Assert.False(
            string.Equals(CanonicalInterface(real), CanonicalInterface(mutated), StringComparison.Ordinal),
            "the raw interface comparison did NOT see an FC parameter retyped Real -> Int. That is the one " +
            "thing this file exists to do.");
    }

    /// <summary>
    /// The same measurement on the sharper case: a UDT, whose interface is its entire definition.
    /// Deletes a member outright rather than retyping one, because losing a member from a type is the
    /// severest form and must not be mistaken for volatile scaffolding. Same rule as above — the
    /// Normalizer's verdict is reported, only the raw comparison is asserted.
    /// </summary>
    [Fact]
    public void RawComparison_SeesAUdtLosingAMember_WhereTheNormalizerDoesNot()
    {
        var exportPath = Path.Combine(ToolPaths.RepoRoot(), "simatic-ml", "test-project001", "UDT_PusherIO.xml");
        Assert.True(File.Exists(exportPath), $"missing real export {exportPath} — empty is not clean");

        var real = XDocument.Load(exportPath);
        var mutated = new XDocument(real);

        var members = mutated.Descendants().Where(e => e.Name.LocalName == "Member").ToList();
        Assert.True(members.Count > 1,
            $"UDT_PusherIO's export declares {members.Count} member(s) — too few for this test to remove one " +
            "and still be measuring a type. Re-point it at another real UDT.");
        var removedName = (string?)members[0].Attribute("Name");
        members[0].Remove();

        _output.WriteLine(
            $"Normalizer.AreSemanticallyEquivalent on a UDT minus member '{removedName}': " +
            $"{Normalizer.AreSemanticallyEquivalent(real, mutated)} " +
            "(true = a UDT's whole definition is being discarded; false = the IsVolatile fix is in " +
            "effect. Reported, not asserted.)");

        Assert.False(
            string.Equals(CanonicalInterface(real), CanonicalInterface(mutated), StringComparison.Ordinal),
            $"the raw interface comparison did NOT see the UDT lose member '{removedName}'.");
    }
}
