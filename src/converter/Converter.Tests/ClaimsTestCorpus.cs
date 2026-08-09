using Converter.Ir;
using Converter.SimaticMl;

namespace Converter.Tests;

/// <summary>
/// A minimal but realistic corpus for the FI-65 claim tests: a shared FC with networks 1-7 (the
/// append-slot case), an FB holding number 50 (the block-number case), an alarm DB whose word has
/// bit 0 already driven (the %X9 case from the project's own telemetry), and a settings DB with an
/// existing member. Built from the IR model and serialized, never hand-written, so a grammar change
/// breaks these tests loudly instead of leaving them testing a stale text format.
/// </summary>
internal static class ClaimsTestCorpus
{
    public const string AlarmWord = "DB_Alarms.ShredderAlarm0";
    public const string SharedBlock = "FC_ControlMain";

    public static string Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"claims-corpus-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);

        // The shared FC: 7 networks, and network 1 drives alarm bit 0 so "already written" has a real
        // writer to name rather than a synthetic flag.
        var networks = Enumerable.Range(1, 7)
            .Select(n => new IrNetwork(
                n,
                $"Network {n}",
                n == 1
                    ? new[] { new CoilAssignment($"{AlarmWord}.%X0", new Expr.TagRef("SomeInput")) }
                    : Array.Empty<CoilAssignment>()))
            .ToList();

        Write(dir, SharedBlock + ".ir",
            IrSerializer.SerializeBlockReadable(
                new IrBlock("0", "FC", SharedBlock, 3, "LAD", "Shared control block", networks)));

        Write(dir, "FB_Existing.ir",
            IrSerializer.SerializeBlockReadable(
                new IrBlock("0", "FB", "FB_Existing", 50, "LAD", "Holds FB50",
                    new[] { new IrNetwork(1, "Only", Array.Empty<CoilAssignment>()) })));

        Write(dir, "DB_Alarms.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Alarms", 20, InstanceOfName: null, Comment: null,
            Members: new[] { new DbMember("ShredderAlarm0", "Word", Retain: false, StartValue: null) })));

        Write(dir, "DB_Settings.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "DB_Settings", 21, InstanceOfName: null, Comment: null,
            Members: new[] { new DbMember("Existing", "Bool", Retain: false, StartValue: null) })));

        return dir;
    }

    public static string CreateClaimsRoot() =>
        Path.Combine(Path.GetTempPath(), $"claims-store-{Guid.NewGuid():N}");

    public static void Delete(params string[] dirs)
    {
        foreach (var dir in dirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }

    private static void Write(string dir, string fileName, string content) =>
        File.WriteAllText(Path.Combine(dir, fileName), content);
}
