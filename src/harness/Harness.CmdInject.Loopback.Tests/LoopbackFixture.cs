using System.Text;
using Harness.CmdInject;
using Harness.Map;
using Harness.Wire;

namespace Harness.CmdInject.Loopback.Tests;

/// <summary>
/// A small, self-contained map + binding + allowlist in <b>invented vocabulary</b>, written to a temp
/// directory so the run paths that load from FILES are exercised end to end.
///
/// <para><b>Deliberately its own fixture rather than the pure suite's.</b> Two reasons. It keeps this
/// project from depending on another test assembly's internals; and its acknowledgement registers sit low
/// enough that one FC03 reaches them from register zero — which is the <i>shared-read</i> poll plan the
/// other fixture cannot exercise, and the case the plan claimed was the only one.</para>
///
/// <para>Not one job tag, register, channel or code appears here. <c>UNIT_L</c>, <c>LP_*</c> and these
/// register numbers are invented for this file.</para>
/// </summary>
internal sealed class LoopbackFixture : IDisposable
{
    internal const string ChannelName = "UNIT_L";

    internal const string HeartbeatTag = "LP_Beat";
    internal const string EnableTag = "LP_Enable";
    internal const string SeqTag = "LP_Seq";
    internal const string CodeTag = "LP_Code";
    internal const string Int1Tag = "LP_Int1";
    internal const string AckSeqTag = "LP_AckSeq";
    internal const string AckResultTag = "LP_AckResult";
    internal const string AckCountTag = "LP_AckCount";

    internal const int HeartbeatRegister = 10;
    internal const int EnableRegister = 11;
    internal const int SeqRegister = 12;
    internal const int CodeRegister = 13;
    internal const int Int1Register = 14;

    internal const int AckSeqRegister = 40;
    internal const int AckResultRegister = 41;
    internal const int AckCountRegister = 42;

    internal const int CommandBandFirst = 10;
    internal const int CommandBandCount = 20;

    internal static readonly BuildStamp Stamp = new(0x1A2B3C4D);

    internal string Directory { get; }
    internal string TagsPath { get; }
    internal string AreaPath { get; }
    internal string BindingPath { get; }
    internal string AllowlistPath { get; }

    internal LoopbackFixture()
    {
        Directory = System.IO.Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "cmd-inject-loopback-" + Guid.NewGuid().ToString("N"))).FullName;

        TagsPath = Write("tags.ir", TagTable());
        AreaPath = Write("area.ir", "FB_Comms\n  MB_HOLD_REG := P#M0.0 WORD 80\n");
        BindingPath = Write("binding.json", Binding());
        AllowlistPath = Write("allowlist.json", """
          {
            "entries": [
              { "address": "127.0.0.1", "kind": "test-rig",
                "writeEligible": true, "outputsIsolated": true,
                "isolationAssertedBy": "loopback: there is no plant on the other end of this socket" }
            ]
          }
          """);
    }

    /// <summary>
    /// The binding as the tool itself resolves it. Used so the tests take the enable's BIT POSITION from
    /// the same parser the tool does rather than assuming bit zero — the map's bit inference is marked
    /// inferred-not-measured in <c>MirrorTag</c>, and a fixture that hardcoded it could disagree with the
    /// code under test while both looked right.
    /// </summary>
    internal ResolvedBinding Resolved()
    {
        var resolution = Resolution.Load(TagsPath, AreaPath, BindingPath);
        Assert.True(resolution.Ok, "fixture invariant: the loopback binding must resolve. " + string.Join(" | ", resolution.Refusals));
        return resolution.Binding!;
    }

    /// <summary>Seed a slave's registers the way a running harness program presents itself.</summary>
    internal void Present(RecordedRegisters registers, BuildStamp stamp, bool enable, uint scan = 900)
    {
        var stampWords = RegisterWords.From32(stamp.Value, RegisterWordOrder.HighWordFirst);
        registers[ControlRegisters.BuildStamp] = stampWords[0];
        registers[ControlRegisters.BuildStamp + 1] = stampWords[1];

        var scanWords = RegisterWords.From32(scan, RegisterWordOrder.HighWordFirst);
        registers[ControlRegisters.ScanCounter] = scanWords[0];
        registers[ControlRegisters.ScanCounter + 1] = scanWords[1];

        var enableTag = Resolved().BandRoles[InjectionRole.Enable];
        registers[enableTag.Register] = enable ? (ushort)(1 << enableTag.BitInRegister) : (ushort)0;
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private static string TagTable()
    {
        var sb = new StringBuilder();
        sb.AppendLine("TAGS");
        var uid = 1;

        void Word(string name, string type, int register) => sb.AppendLine($"{name} {uid++:X} : {type} @ %MW{2 * register}");
        void Bit(string name, int register) => sb.AppendLine($"{name} {uid++:X} : Bool @ %M{2 * register + 1}.0");

        Word(HeartbeatTag, "UInt", HeartbeatRegister);
        Bit(EnableTag, EnableRegister);
        Word(SeqTag, "UInt", SeqRegister);
        Word(CodeTag, "Int", CodeRegister);
        Word(Int1Tag, "Int", Int1Register);
        Word(AckSeqTag, "UInt", AckSeqRegister);
        Word(AckResultTag, "Int", AckResultRegister);
        Word(AckCountTag, "UInt", AckCountRegister);

        return sb.ToString();
    }

    private static string Binding() =>
        $$"""
          {
            "commandBand": { "firstRegister": {{CommandBandFirst}}, "registerCount": {{CommandBandCount}} },
            "observationBands": [ { "firstRegister": {{AckSeqRegister}}, "registerCount": 4 } ],
            "heartbeat": "{{HeartbeatTag}}",
            "enable": "{{EnableTag}}",
            "channels": [
              {
                "name": "{{ChannelName}}",
                "seq": "{{SeqTag}}",
                "code": "{{CodeTag}}",
                "int1": "{{Int1Tag}}",
                "ackSeq": "{{AckSeqTag}}",
                "ackResult": "{{AckResultTag}}",
                "ackCount": "{{AckCountTag}}"
              }
            ]
          }
          """;

    /// <summary>The operands one loopback command carries.</summary>
    internal static IReadOnlyDictionary<InjectionRole, string> Operands => new Dictionary<InjectionRole, string>
    {
        [InjectionRole.Code] = "7",
        [InjectionRole.Int1] = "-9",
    };

    internal SendOptions Send(bool armed, int port, bool raiseEnable = false, int pollAttempts = 2) =>
        new(TagsPath, AreaPath, BindingPath, ChannelName, Operands, armed,
            Target: "127.0.0.1", AllowlistPath: AllowlistPath, ExpectedBuildStamp: Stamp.Value,
            Port: port, UnitId: 1, RaiseEnable: raiseEnable, PollAttempts: pollAttempts, PollIntervalMs: 0);

    public void Dispose()
    {
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch (IOException) { /* a leftover temp dir is not a test failure */ }
    }
}
