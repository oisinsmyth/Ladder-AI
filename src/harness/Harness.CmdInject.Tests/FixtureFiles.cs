using System.Text;
using Harness.MirrorView;

namespace Harness.CmdInject.Tests;

/// <summary>
/// Writes a self-consistent map (tag table + area pointer) and a binding to a temp directory, all in the
/// invented vocabulary, so the run paths that load from FILES can be exercised end to end with no device.
///
/// <para>The %M addresses are formatted from the canonical registers with base 0, so
/// <c>MirrorMapParser</c> places every tag back exactly where the fixtures put it — the same parser the
/// tool uses, not a second one.</para>
/// </summary>
internal sealed class FixtureFiles : IDisposable
{
    internal string Directory { get; }
    internal string TagsPath { get; }
    internal string AreaPath { get; }
    internal string BindingPath { get; }

    internal FixtureFiles(string? bindingJson = null)
    {
        Directory = System.IO.Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "cmd-inject-files-" + Guid.NewGuid().ToString("N"))).FullName;

        TagsPath = Write("tags.ir", TagTableText());
        AreaPath = Write("area.ir", "FB_Comms\n  MB_HOLD_REG := P#M0.0 WORD 300\n");
        BindingPath = Write("binding.json", bindingJson ?? DefaultBindingJson());
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string TagTableText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TAGS");
        var uid = 1;
        foreach (var tag in Fixtures.DefaultTags())
            sb.AppendLine($"{tag.Name} {uid++:X} : {tag.TypeName} @ {AddressOf(tag.Register, tag.Width)}");
        return sb.ToString();
    }

    private static string AddressOf(int register, MirrorWidth width) => width switch
    {
        MirrorWidth.Word => $"%MW{2 * register}",
        MirrorWidth.DoubleWord => $"%MD{2 * register}",
        MirrorWidth.Bit => $"%M{2 * register + 1}.0",
        _ => throw new ArgumentOutOfRangeException(nameof(width), width, null),
    };

    internal static string DefaultBindingJson() =>
        $$"""
          {
            "commandBand": { "firstRegister": 100, "registerCount": 88 },
            "observationBands": [ { "firstRegister": 200, "registerCount": 20 } ],
            "heartbeat": "{{Fixtures.HeartbeatTag}}",
            "enable": "{{Fixtures.EnableTag}}",
            "safetyPermissive": "{{Fixtures.SafetyTag}}",
            "channels": [
              {
                "name": "{{Fixtures.ChannelName}}",
                "seq": "{{Fixtures.SeqTag}}",
                "code": "{{Fixtures.CodeTag}}",
                "int1": "{{Fixtures.Int1Tag}}",
                "int2": "{{Fixtures.Int2Tag}}",
                "real1": "{{Fixtures.Real1Tag}}",
                "real2": "{{Fixtures.Real2Tag}}",
                "ackSeq": "{{Fixtures.AckSeqTag}}",
                "ackCode": "{{Fixtures.AckCodeTag}}",
                "ackResult": "{{Fixtures.AckResultTag}}",
                "ackCount": "{{Fixtures.AckCountTag}}"
              }
            ]
          }
          """;

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }
}
