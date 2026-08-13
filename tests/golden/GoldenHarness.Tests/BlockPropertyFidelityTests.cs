using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// Every block-level property in a real TIA export's own <c>&lt;AttributeList&gt;</c>, compared RAW
/// against what comes back from <c>to-ir</c> → <c>to-xml</c>. The generalisation of
/// <see cref="MemoryLayoutFidelityTests"/>, built after the ignore-list audit of 2026-08-13 found that
/// *** MemoryLayout WAS NOT SPECIAL — IT WAS THE FIRST OF FIFTEEN. ***
///
/// <para>MEASURED, across all 38 committed exports:</para>
/// <list type="bullet">
/// <item><b>Of the 22 element names in <c>Normalizer.VolatileElementNames</c>, the converter emits
/// exactly ONE — <c>MemoryLayout</c> — and only since 2026-08-12, i.e. after it bit.</b> The other 21
/// are written by no writer in <c>src/converter/Converter/SimaticMl/</c>.</item>
/// <item><b>14 of them are present in real exports and absent from our regenerated output, every
/// time.</b> A real FC export carries 15 block-level properties and ours carries 6; a GlobalDB 16
/// versus 6; an FB 17 versus 6. Nothing is value-changed and nothing is added — the loss is total and
/// one-directional.</item>
/// </list>
///
/// <para>*** THAT IS THE MemoryLayout SHAPE, FOURTEEN TIMES OVER: *** the converter emits nothing, TIA
/// supplies its default on import, the two therefore "agree", and the Normalizer's ignore list is what
/// makes the agreement look like a match. It has not bitten yet only because every value in the corpus
/// is uniform — <c>IsIECCheckEnabled=false</c>, <c>SetENOAutomatically=false</c>,
/// <c>DBAccessibleFromOPCUA=true</c>, <c>MemoryReserve=100</c> and so on, one value each across the
/// whole corpus. MemoryLayout was equally quiet until someone deliberately set a NON-default value
/// (<c>block-layout --set Standard</c>) and watched it silently revert on the next import.</para>
///
/// <para>This suite does not un-ignore anything — that is the converter lane's call and a Normalizer
/// that ignores too little is as broken as one that ignores too much. It makes the 14 losses VISIBLE
/// and NAMED, so a 15th cannot join them unnoticed and each one closing is detected.</para>
/// </summary>
public class BlockPropertyFidelityTests
{
    /// <summary>
    /// Block-level properties a real export carries that our regenerated block does not, with what a
    /// consistent error under each would cost. Every one is in <c>Normalizer.VolatileElementNames</c>,
    /// so nothing in the toolchain reports the loss.
    ///
    /// SEMANTIC = a wrong value changes what the controller does, what it exposes, or what compiles.
    /// METADATA = documentation or engineering-tool bookkeeping; a loss is real but not behavioural.
    /// Each entry also records the single value seen across the whole corpus, because that uniformity
    /// is exactly why none of these has bitten yet.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDroppedProperties = new(StringComparer.Ordinal)
    {
        // ---- SEMANTIC: the MemoryLayout-shaped ones ------------------------------------------
        ["DBAccessibleFromOPCUA"] = "SEMANTIC (12 blocks, always 'true'). Whether a DB is visible to an OPC UA client — the same CLASS of fact as MemoryLayout (whether it is visible to classic S7comm), which is the one that bit. Deliberately restricting a DB for security, or exposing one for a client, silently reverts on the next import.",
        ["IsOnlyStoredInLoadMemory"] = "SEMANTIC (9 blocks, always 'false'). A DB held only in load memory is not in work memory. Reverting it silently changes the block's memory footprint on the controller.",
        ["IsWriteProtectedInAS"] = "SEMANTIC (9 blocks, always 'false'). Write protection on the controller; reverting it silently removes a protection someone applied on purpose.",
        ["MemoryReserve"] = "SEMANTIC (12 blocks, always '100'). Retain-memory reserve in bytes — it is what makes a later download-without-reinitialisation possible. Reverting it to the default silently removes that headroom.",
        ["IsRetainMemResEnabled"] = "SEMANTIC (12 blocks, always 'false'). Same family as MemoryReserve.",
        ["SetENOAutomatically"] = "SEMANTIC (18 blocks, always 'false'). Whether the block sets ENO automatically — a runtime behaviour of the generated code, not bookkeeping.",
        ["IsIECCheckEnabled"] = "SEMANTIC (18 blocks, always 'false'). IEC type-check strictness; it decides what compiles, so a silent flip changes whether a block builds.",

        // ---- METADATA -------------------------------------------------------------------------
        ["HeaderAuthor"] = "METADATA (30 blocks, EMPTY on every one — measured, not assumed, so the Normalizer's 'confirmed empty' claim still holds). A real value on a future block would be lost silently.",
        ["HeaderFamily"] = "METADATA (30 blocks, empty on every one).",
        ["HeaderName"] = "METADATA (30 blocks, empty on every one).",
        ["HeaderVersion"] = "METADATA (30 blocks, always '0.1'). A block-version bump would not survive.",
        ["UDABlockProperties"] = "METADATA (16 blocks, empty element on every one). User-defined attributes; nothing in the corpus populates it.",
        ["UDAEnableTagReadback"] = "METADATA (16 blocks, always 'false').",
        ["AutoNumber"] = "METADATA (30 blocks, always 'true'). LOWER STAKES THAN IT LOOKS, and this was measured rather than assumed: AutoNumber is 'true' even on the deliberately hand-numbered DBs (DB_Timers Number=1, FBTimers Number=30), so it is NOT what protects a chosen block number — <Number> is, and <Number> survives the round trip and IS compared.",
    };

    public static IEnumerable<object[]> AllExports() =>
        ExportRoundTripRunner.CommittedExports().Select(e => new object[] { e.Name, e.ExportPath, e.IrDir });


    /// <summary>
    /// The direct children of the block's OWN <c>&lt;AttributeList&gt;</c> — the one that is a child of
    /// the <c>SW.Blocks.*</c> / <c>SW.Types.*</c> / <c>SW.Tags.*</c> root — as name → value.
    ///
    /// <c>Interface</c> is excluded: it is real content rather than a property, and
    /// <see cref="BlockInterfaceFidelityTests"/> owns it (with its own baseline, which must not be
    /// duplicated here). Nested AttributeLists — a MultilingualTextItem's, a NetworkSource's — are not
    /// block properties and are out of scope.
    /// </summary>
    internal static IReadOnlyDictionary<string, string> BlockProperties(XDocument doc)
    {
        var root = doc.Root?.Elements().FirstOrDefault(e =>
            e.Name.LocalName.StartsWith("SW.Blocks.", StringComparison.Ordinal)
            || e.Name.LocalName.StartsWith("SW.Types.", StringComparison.Ordinal)
            || e.Name.LocalName.StartsWith("SW.Tags.", StringComparison.Ordinal));

        var attributeList = root?.Elements().FirstOrDefault(e => e.Name.LocalName == "AttributeList");
        if (attributeList is null)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        return attributeList.Elements()
            .Where(e => e.Name.LocalName != "Interface")
            .GroupBy(e => e.Name.LocalName, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Value.Trim(), StringComparer.Ordinal);
    }

    [Theory]
    [MemberData(nameof(AllExports))]
    public void EveryBlockProperty_EitherSurvivesTheRoundTrip_OrIsANamedLoss(
        string block, string exportPath, string irDir)
    {
        var expected = BlockProperties(XDocument.Load(exportPath));
        var actual = BlockProperties(ExportRoundTripRunner.Run("block-property-fidelity", block, exportPath, irDir));

        var unexpectedlyLost = expected.Keys
            .Where(k => !actual.ContainsKey(k) && !KnownDroppedProperties.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(unexpectedlyLost.Count == 0,
            $"{block}: block-level propert(ies) present in the real TIA export and ABSENT from our " +
            $"regenerated block, and not on the known-loss list: [{string.Join(", ", unexpectedlyLost)}]." +
            Environment.NewLine +
            "Every one of these is in Normalizer.VolatileElementNames, so no Normalizer-based check " +
            "reports it — this is the MemoryLayout shape (we emit nothing, TIA defaults it, both sides " +
            "'agree'). Either carry it through the IR, or add it to KnownDroppedProperties with what a " +
            "wrong value would cost.");

        var valueChanged = expected
            .Where(kv => actual.TryGetValue(kv.Key, out var v) && v != kv.Value)
            .Select(kv => $"{kv.Key}: TIA='{kv.Value}' ours='{actual[kv.Key]}'")
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();

        Assert.True(valueChanged.Count == 0,
            $"{block}: block-level propert(ies) survived the round trip with a DIFFERENT value — " +
            $"[{string.Join("; ", valueChanged)}]. Surviving with the wrong value is worse than being " +
            "dropped, because it looks like it was carried.");

        // The other direction: inventing a property TIA did not state. Same defect as dropping one,
        // pointed the other way — an import that asserts a value nobody chose.
        var invented = actual.Keys
            .Where(k => !expected.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(invented.Count == 0,
            $"{block}: our regenerated block declares block-level propert(ies) the real export does not — " +
            $"[{string.Join(", ", invented)}]. Stating a value TIA never stated is an assertion we are not " +
            "entitled to make.");
    }

    /// <summary>
    /// The did-not-run case, in both of its forms: an empty export population makes the Theory vacuous,
    /// and a population carrying no properties beyond the handful that always survive would make every
    /// case a trivial pass while looking identical in the run output.
    /// </summary>
    [Fact]
    public void TheCorpus_ActuallyCarriesBlockPropertiesToCompare()
    {
        var exports = ExportRoundTripRunner.CommittedExports().ToList();
        Assert.True(exports.Count > 0, "no committed exports found — empty is not clean");

        var withProperties = exports
            .Select(e => BlockProperties(XDocument.Load(e.ExportPath)))
            .Count(p => p.Count > 4);

        Assert.True(withProperties > 0,
            $"none of the {exports.Count} committed exports carries more than the 4 always-present " +
            "block properties (Name/Namespace/Number/ProgrammingLanguage), so this suite is comparing " +
            "nothing that could be lost.");
    }

    /// <summary>
    /// Staleness guard on the list itself: an entry that is no longer being dropped anywhere must go,
    /// or the list starts documenting a world that no longer exists. This is the test that will notice
    /// the day the converter starts carrying one of these through.
    /// </summary>
    /// <para>*** A [Fact] OVER A KNOWN-PROBLEM LIST, DELIBERATELY. *** As a [Theory] it would fail with
    /// "No data found" the moment the list empties — i.e. the day all 14 losses are fixed, which is the
    /// state this file exists to drive towards. Two siblings did exactly that on 2026-08-13
    /// (`KnownMissingExport_IsStillMissing`, `EveryReadableOnlyBlock_IsEitherCovered_OrAKnownGap`), and
    /// a test that punishes its own fix is a test people route around. A theory over a CORPUS
    /// enumeration is a different thing and stays a theory: an empty corpus really is broken.</para>
    ///
    /// <para>Also one round trip per EXPORT rather than per (export × property) — 14 full corpus scans
    /// collapse to one.</para>
    /// </summary>
    [Fact]
    public void EveryKnownDroppedProperty_IsStillBeingDropped()
    {
        var stillDropped = new HashSet<string>(StringComparer.Ordinal);

        foreach (var e in ExportRoundTripRunner.CommittedExports())
        {
            var expected = BlockProperties(XDocument.Load(e.ExportPath));
            if (!expected.Keys.Any(KnownDroppedProperties.ContainsKey))
            {
                continue;
            }

            var actual = BlockProperties(ExportRoundTripRunner.Run("block-property-fidelity", e.Name, e.ExportPath, e.IrDir));
            foreach (var property in expected.Keys.Where(k => KnownDroppedProperties.ContainsKey(k) && !actual.ContainsKey(k)))
            {
                stillDropped.Add(property);
            }
        }

        var stale = KnownDroppedProperties.Keys
            .Where(p => !stillDropped.Contains(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(stale.Count == 0,
            $"KnownDroppedProperties entr(ies) no longer dropped by any block in the corpus: " +
            $"[{string.Join(", ", stale)}]. Either they are now carried through (good — remove them, " +
            "EveryBlockProperty_EitherSurvivesTheRoundTrip_OrIsANamedLoss guards them from here on) or no " +
            "export carries them any more, in which case the entry is measuring nothing.");
    }

    /// <summary>
    /// The positive control, and the reason the Theory above is not vacuous: some block properties DO
    /// survive, value-identical, so a failure there means a real regression rather than "the converter
    /// drops everything". <c>IsFailsafeCompliant</c> is the sharpest of them — it is NOT on any ignore
    /// list, it is carried, and it is compared.
    /// </summary>
    [Fact]
    public void TheSurvivingProperties_AreCarriedValueIdentical()
    {
        var core = new[] { "Name", "Namespace", "Number", "ProgrammingLanguage" };
        var checkedAny = false;

        foreach (var e in ExportRoundTripRunner.CommittedExports())
        {
            var expected = BlockProperties(XDocument.Load(e.ExportPath));
            if (!core.Any(expected.ContainsKey))
            {
                continue;
            }

            var actual = BlockProperties(ExportRoundTripRunner.Run("block-property-fidelity", e.Name, e.ExportPath, e.IrDir));
            foreach (var name in core.Where(expected.ContainsKey))
            {
                checkedAny = true;
                Assert.True(actual.TryGetValue(name, out var got) && got == expected[name],
                    $"{e.Name}: '{name}' is a property the converter is supposed to carry — TIA says " +
                    $"'{expected[name]}', we produced '{(actual.TryGetValue(name, out var g) ? g : "(absent)")}'.");
            }
        }

        Assert.True(checkedAny, "no export carried any of the core properties — empty is not clean");
    }
}
