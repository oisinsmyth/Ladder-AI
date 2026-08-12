using System.Xml.Linq;
using Xunit;

namespace GoldenHarness;

/// <summary>
/// Pins the measured fact the wire-direction fix rests on (2026-08-12): in SimaticML a wire's FIRST
/// endpoint is its PRODUCER and the rest are its CONSUMERS, and nothing else in the document encodes
/// that — no attribute, no child element.
///
/// The consequence is that a given (part kind, port name) sits EXCLUSIVELY at index 0 (an output port)
/// or EXCLUSIVELY in the tail (an input port), never both. Measured across every real TIA export
/// committed under <c>simatic-ml/</c>: 106 such pairs, none mixed.
///
/// These are raw-order guards, deliberately not Normalizer-based ones — the same reasoning
/// <see cref="FlgNetWriterPartOrderTests"/> gives for Part order. Until 2026-08-12 the Normalizer sorted
/// a wire's whole endpoint list, so a producer/consumer swap was invisible to every comparison in the
/// project; <c>FlgNetBuilder</c> was meanwhile ordering endpoints by UId NUMBERING, which inverted 100%
/// of the Contact/Coil operand wires in a regenerated block against its own export. Both blindnesses
/// cancelled and nothing went red — so a test reading the Normalizer's output could not have caught it.
/// </summary>
public class WireEndpointDirectionTests
{
    private static string Local(XElement e) => e.Name.LocalName;

    private static IEnumerable<string> XmlFiles(params string[] parts)
    {
        var dir = Path.Combine(new[] { ToolPaths.RepoRoot() }.Concat(parts).ToArray());
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.xml", SearchOption.AllDirectories)
            : Enumerable.Empty<string>();
    }

    /// <summary>A real TIA export carries a <c>DocumentInfo</c> envelope; converter output never does.</summary>
    private static bool IsRealTiaExport(string file) => File.ReadAllText(file).Contains("<DocumentInfo");

    /// <summary>
    /// Every "<c>PartKind.port</c>" seen at endpoint index 0, and every one seen in the tail.
    /// </summary>
    private static (HashSet<string> AtIndex0, HashSet<string> InTail) PortSlots(IEnumerable<string> files)
    {
        var atIndex0 = new HashSet<string>(StringComparer.Ordinal);
        var inTail = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in files)
        {
            var root = XDocument.Load(file).Root;
            if (root is null)
            {
                continue;
            }

            foreach (var flgNet in root.Descendants().Where(e => Local(e) == "FlgNet"))
            {
                var partKind = flgNet.Elements().FirstOrDefault(e => Local(e) == "Parts")?.Elements()
                    .Where(e => Local(e) is "Part" or "Call")
                    .ToDictionary(
                        e => (string)e.Attribute("UId")!,
                        e => Local(e) == "Call" ? "Call" : (string?)e.Attribute("Name") ?? "?")
                    ?? new Dictionary<string, string>();

                var wires = flgNet.Elements().FirstOrDefault(e => Local(e) == "Wires")?.Elements()
                    .Where(e => Local(e) == "Wire") ?? Enumerable.Empty<XElement>();

                foreach (var wire in wires)
                {
                    var endpoints = wire.Elements().ToList();
                    for (var i = 0; i < endpoints.Count; i++)
                    {
                        if (Local(endpoints[i]) != "NameCon")
                        {
                            continue;
                        }

                        var uid = (string?)endpoints[i].Attribute("UId") ?? "?";
                        var kind = partKind.TryGetValue(uid, out var k) ? k : "?";
                        (i == 0 ? atIndex0 : inTail).Add($"{kind}.{(string?)endpoints[i].Attribute("Name")}");
                    }
                }
            }
        }

        return (atIndex0, inTail);
    }

    /// <summary>TIA's own exports are the authority on which ports produce and which consume.</summary>
    private static (HashSet<string> Producers, HashSet<string> Consumers) RolesFromRealExports()
    {
        var files = XmlFiles("simatic-ml").Where(IsRealTiaExport).ToList();
        Assert.True(files.Count > 0, "no real TIA exports found under simatic-ml/ — empty is not clean");

        var (atIndex0, inTail) = PortSlots(files);
        var producers = new HashSet<string>(atIndex0.Except(inTail), StringComparer.Ordinal);
        var consumers = new HashSet<string>(inTail.Except(atIndex0), StringComparer.Ordinal);
        return (producers, consumers);
    }

    [Fact]
    public void RealTiaExports_NoPortIsBothProducerAndConsumer()
    {
        // The measurement itself. A pair turning up in both slots means either endpoint order has been
        // scrambled, or a genuine InOut has appeared in the corpus — equally worth stopping for, since
        // an InOut is precisely the shape wire order CANNOT express (ir/SPEC.md §Interface).
        var files = XmlFiles("simatic-ml").Where(IsRealTiaExport).ToList();
        Assert.True(files.Count > 0, "no real TIA exports found under simatic-ml/ — empty is not clean");

        var (atIndex0, inTail) = PortSlots(files);
        var mixed = atIndex0.Intersect(inTail).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.True(
            mixed.Count == 0,
            $"{mixed.Count} (part, port) pair(s) appear BOTH as a wire's producer and as a consumer in " +
            $"real TIA exports:{Environment.NewLine}  " + string.Join(Environment.NewLine + "  ", mixed));
    }

    [Fact]
    public void FrozenAnswerKeys_ObeyTheProducerConsumerRolesTiaUses()
    {
        // The converter's OWN output, held to TIA's roles. This is the guard that would have caught the
        // FlgNetBuilder defect: before the fix these keys placed `Add.out`, `Mul.out`, `Convert.out` and
        // `Move.out1` in the TAIL — ports TIA puts at index 0 without exception — because the builder
        // sorted a wire's endpoints by UId and an Access happened to outrank the Part driving it.
        var (producers, consumers) = RolesFromRealExports();
        var keys = XmlFiles("tests", "golden", "answer-keys").ToList();
        Assert.True(keys.Count > 0, "no frozen answer keys found — empty is not clean");

        var (atIndex0, inTail) = PortSlots(keys);
        var consumerDrivingAWire = atIndex0.Intersect(consumers).OrderBy(s => s, StringComparer.Ordinal).ToList();
        var producerDemotedToTail = inTail.Intersect(producers).OrderBy(s => s, StringComparer.Ordinal).ToList();

        Assert.True(
            consumerDrivingAWire.Count == 0 && producerDemotedToTail.Count == 0,
            "converter output disagrees with TIA about which end of a wire produces." +
            $"{Environment.NewLine}  input port listed FIRST (driving the wire): " +
            $"{string.Join(", ", consumerDrivingAWire)}" +
            $"{Environment.NewLine}  output port listed in the TAIL (being driven): " +
            $"{string.Join(", ", producerDemotedToTail)}");
    }
}
