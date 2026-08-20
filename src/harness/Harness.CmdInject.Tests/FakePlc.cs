using Harness.CmdInject;
using Harness.Map;
using Harness.Wire;

namespace Harness.CmdInject.Tests;

/// <summary>
/// L3 — <b>a model of the command block, written to reproduce the cases that MISLEAD a client.</b>
///
/// <para>🔴 <b>READ THIS BEFORE QUOTING ANYTHING THIS FILE PROVES. IT IS EVIDENCE ABOUT THE CLIENT AND
/// NEVER ABOUT THE PLC.</b> This model was written from the same protocol description the client was
/// written from, by the same hand, in the same afternoon. A client checked against a model built from its
/// own source of truth is a <i>self-consistent</i> check, not an independent one — and this repository has
/// a recorded instance of exactly that failure: an AI reviewer reading the same register as the AI coder
/// failed with it, together, on the same ambiguity. So what a green run here licenses is precise and
/// narrow: <b>given a device that behaves as described, the client draws the right conclusion.</b> It
/// licenses nothing at all about whether the device behaves as described. <b>The loop is not closed until
/// this runs on the rig, and until then nothing here may be written up as "verified".</b></para>
///
/// <para><b>What is modelled, and why each one is here:</b></para>
/// <list type="bullet">
/// <item><b>Arming by heartbeat, sampled ONCE PER SCAN.</b> Nothing is processed until the heartbeat
/// register has CHANGED twice — and a change is something a scan observes, not something a write makes, so
/// two writes with no scan between them are one change or none. The sample is reached only while the
/// master enable is up, so a heartbeat written under a clear enable counts for nothing. Once counted, a
/// change is never un-counted except by <see cref="Restart"/>, and further changes past the threshold are
/// harmless — which is why the client stamps every session instead of asking whether it needs to. The
/// client never learns what "unarmed" means numerically: it reads outcomes, not codes.</item>
/// <item><b>Sequence inequality.</b> A command is recognised when the sequence register differs from the
/// acknowledged one. Equality is not a command, and zero is the resting value.</item>
/// <item><b>The count moves only on success.</b> This is the entire basis of the client's verdict, and a
/// refusal that moved the count would make the two indistinguishable.</item>
/// <item><b>The acknowledged sequence is written LAST.</b> So a torn read can show a moved count under an
/// old sequence, but never the reverse.</item>
/// <item><b>The result is HELD until the next command is processed.</b> Which is why "the result register
/// did not change" can never be read as "the command was not seen".</item>
/// <item><b>The result can lag its own count by one read</b> (<see cref="ResultPublicationLagsOneRead"/>).
/// This is the confusable case the whole design exists for: a fresh success whose result register still
/// reads the PREVIOUS command's value. A client keying its verdict on the result gets it wrong here; a
/// client keying on the count gets it right.</item>
/// <item><b>The enable gates execution.</b> A command written while the enable is clear is seen and not
/// processed — which is what makes the restore's "drop the enable FIRST" ordering testable.</item>
/// <item><b>Restart.</b> Every channel register zeroes and the scan counter rewinds.</item>
/// </list>
/// </summary>
internal sealed class FakePlc : IInjectionTransport
{
    private readonly ResolvedBinding _binding;
    private readonly ResolvedChannel _channel;
    private readonly Dictionary<int, ushort> _store = new();

    private uint _scan = 1000;
    private ushort _lastHeartbeat;
    private int _heartbeatChanges;
    private ushort _ackSeq;
    private ushort _ackCount;
    private ushort _result;
    private ushort _publishedResult;
    private int _resultLagReadsRemaining;

    internal FakePlc(ResolvedBinding binding, ResolvedChannel channel, BuildStamp stamp)
    {
        _binding = binding;
        _channel = channel;
        Stamp = stamp;
        PublishStamp(stamp);
        PublishScan(_scan);
    }

    /// <summary>Codes are job data, so the model invents its own. The client never compares against either.</summary>
    internal const ushort SuccessResult = 0x0100;

    /// <summary>The refusal code this model reports. Invented, and deliberately not zero.</summary>
    internal const ushort RefusalResult = 0x0EEE;

    /// <summary>The stamp this model publishes at registers 0–1.</summary>
    internal BuildStamp Stamp { get; private set; }

    /// <summary>When true, the result register serves its PREVIOUS value for one read after a command is processed.</summary>
    internal bool ResultPublicationLagsOneRead { get; set; }

    /// <summary>Every command this model actually processed, in order — the denominator behind "it was not resent".</summary>
    internal List<ushort> Processed { get; } = new();

    /// <summary>Every command this model REFUSED, in order.</summary>
    internal List<ushort> Refused { get; } = new();

    /// <summary>Every write the client made, in order. A resend would appear here as a second sequence write.</summary>
    internal List<RecordedWrite> Writes { get; } = new();

    /// <summary>How many heartbeat CHANGES the model requires before it will process anything.</summary>
    internal const int ChangesToArm = 2;

    /// <summary>Whether the model considers itself armed. Heartbeat CHANGES, not heartbeat writes.</summary>
    internal bool Armed => _heartbeatChanges >= ChangesToArm;

    /// <summary>How many changes the model has counted — the denominator behind <see cref="Armed"/>.</summary>
    internal int HeartbeatChangesSeen => _heartbeatChanges;

    /// <summary>The acknowledgement count, for a test that wants the number rather than the register.</summary>
    internal ushort AckCount => _ackCount;

    /// <summary>Whether the master enable currently reads set.</summary>
    internal bool EnableIsSet
    {
        get
        {
            var tag = _binding.BandRoles[InjectionRole.Enable];
            return (Peek(tag.Register) & (1 << tag.BitInRegister)) != 0;
        }
    }

    /// <summary>
    /// 🔴 <b>WRITE THE HEARTBEAT REGISTER BEHIND THE CLIENT'S BACK.</b> Setup only, and the name is long
    /// on purpose so a call site cannot use it without saying what it is doing.
    ///
    /// <para><b>No test may arm the model with this in order to send a command through the client.</b>
    /// That was how the arming gate stayed invisible: the suite supplied the exact capability the tool was
    /// missing, so 99 tests passed over a client that could not arm anything, and the first place it could
    /// have failed was the rig. The client arms the model or the test fails. This exists for the two cases
    /// where the DEVICE's prior state is the subject — a surface some earlier session left armed, and the
    /// negative control proving that two identical writes are not two changes.</para>
    /// </summary>
    internal void PokeHeartbeatBypassingTheClient(ushort value) =>
        WriteRegisters(_binding.BandRoles[InjectionRole.Heartbeat].Register, new[] { value });

    /// <summary>
    /// 🔴 <b>PUT THE MODEL IN AN ALREADY-ARMED STATE WITHOUT THE CLIENT.</b> Only for tests whose subject
    /// is what happens to a surface somebody else left armed — never as a way of getting a command through.
    /// See <see cref="PokeHeartbeatBypassingTheClient"/>.
    /// </summary>
    internal void ArmBypassingTheClient()
    {
        for (var i = 1; i <= ChangesToArm; i++)
        {
            PokeHeartbeatBypassingTheClient((ushort)i);
            Scan();
        }
    }

    /// <summary>
    /// Run one scan: the counter advances and the block SAMPLES its inputs.
    ///
    /// <para>🔴 <b>THE HEARTBEAT IS SAMPLED HERE AND NOWHERE ELSE, WHICH IS THE WHOLE POINT.</b> A write
    /// puts a value in memory; only a scan compares it with the previous one. So two writes with no scan
    /// between them are ONE change — or none, if the second put the first's value back — and a client that
    /// writes twice quickly arms nothing. Modelling this in the write handler instead would have made
    /// every double write look like two changes, which is exactly the confusion that must not be possible
    /// to have.</para>
    /// </summary>
    internal void Scan()
    {
        _scan++;
        PublishScan(_scan);
        SampleHeartbeat();
    }

    /// <summary>Set the master enable directly, as a previous session or a person at the panel would have.</summary>
    internal void SetEnable(bool set)
    {
        var tag = _binding.BandRoles[InjectionRole.Enable];
        var word = Peek(tag.Register);
        word = set
            ? (ushort)(word | (1 << tag.BitInRegister))
            : (ushort)(word & ~(1 << tag.BitInRegister));
        _store[tag.Register] = word;
    }

    /// <summary>Publish a different build stamp — a download landing under a running client.</summary>
    internal void Redownload(BuildStamp stamp)
    {
        Stamp = stamp;
        PublishStamp(stamp);
    }

    /// <summary>
    /// A CPU restart: every channel register zeroes and the scan counter rewinds to zero. The stamp
    /// survives, because the program is the same one — a restart is not a download.
    /// </summary>
    internal void Restart()
    {
        foreach (var register in _channel.CommandRegisters)
            _store[register] = 0;

        foreach (var tag in _channel.ObservationRoles.Values)
        {
            for (var i = 0; i < tag.RegisterCount; i++)
                _store[tag.Register + i] = 0;
        }

        var enableTag = _binding.BandRoles[InjectionRole.Enable];
        _store[enableTag.Register] = 0;

        _ackSeq = 0;
        _ackCount = 0;
        _result = 0;
        _publishedResult = 0;
        _resultLagReadsRemaining = 0;
        _heartbeatChanges = 0;
        _lastHeartbeat = 0;

        // A REWIND, not a reset to some large number: the client's restart detector reads a backward step as
        // an implausibly large forward one (ScanCount is modular and has no operator -), so the model must
        // start LOW and go to zero for the difference to exceed the plausible-advance ceiling.
        _scan = 0;
        PublishScan(_scan);
    }

    // ---- the transport --------------------------------------------------------------------------

    public void WriteRegisters(int startRegister, IReadOnlyList<ushort> values)
    {
        Writes.Add(new RecordedWrite(startRegister, values.ToList()));

        for (var i = 0; i < values.Count; i++)
            _store[startRegister + i] = values[i];

        Evaluate();
    }

    public ushort[] ReadRegisters(int startRegister, int count)
    {
        // One scan between observations: the counter advances whether or not anything happened, which is what
        // makes "the counter stalled" and "the command was not processed" different findings. The block also
        // samples its inputs here, so a client that wants two heartbeat CHANGES has to leave a scan between
        // them — and the only way it can observe one is by reading.
        Scan();

        var answer = new ushort[count];
        for (var i = 0; i < count; i++)
            answer[i] = Serve(startRegister + i);

        if (_resultLagReadsRemaining > 0 && CoversResult(startRegister, count))
            _resultLagReadsRemaining--;

        return answer;
    }

    public void Dispose() { }

    // ---- the block ------------------------------------------------------------------------------

    /// <summary>
    /// The once-per-scan heartbeat sample.
    ///
    /// <para>🔴 <b>THE HEARTBEAT REACHES THE BLOCK ONLY THROUGH THE ENABLE.</b> A heartbeat written while
    /// the enable is clear lands in memory and is never sampled, so it counts for nothing — and from the
    /// client's side it looks exactly like a write that worked. That is why the client reads the enable
    /// back before it stamps rather than believing its own earlier write.</para>
    ///
    /// <para><b>Nothing here ever un-counts a change.</b> Once counted, a change stays counted until
    /// <see cref="Restart"/>, and further changes past the threshold are harmless — which is why the
    /// client stamps unconditionally instead of asking whether it needs to.</para>
    /// </summary>
    private void SampleHeartbeat()
    {
        if (!EnableIsSet) return;

        var heartbeat = Peek(_binding.BandRoles[InjectionRole.Heartbeat].Register);
        if (heartbeat == _lastHeartbeat) return;

        _lastHeartbeat = heartbeat;
        _heartbeatChanges++;
    }

    private void Evaluate()
    {
        var seq = Peek(_channel.SequenceRegister);

        // Equality is not a command, and zero is the resting value — a command carrying it would be invisible.
        if (seq == _ackSeq || seq == 0) return;

        var code = Peek(_channel.CommandRoles[InjectionRole.Code].Register);
        var accepted = Armed && EnableIsSet;

        _publishedResult = _result;
        _result = accepted ? SuccessResult : RefusalResult;
        _resultLagReadsRemaining = ResultPublicationLagsOneRead ? 1 : 0;

        Write(_channel.ObservationRoles, InjectionRole.AckCode, code);
        Write(_channel.ObservationRoles, InjectionRole.AckResult, _result);

        if (accepted)
        {
            _ackCount++;
            Processed.Add(seq);
        }
        else
        {
            Refused.Add(seq);
        }

        Write(_channel.ObservationRoles, InjectionRole.AckCount, _ackCount);

        // LAST, always. A torn read may show a moved count under an old sequence; it may never show a new
        // sequence under an unmoved count for a command that succeeded.
        _ackSeq = seq;
        Write(_channel.ObservationRoles, InjectionRole.AckSeq, _ackSeq);
    }

    private void Write(IReadOnlyDictionary<InjectionRole, Harness.MirrorView.MirrorTag> roles, InjectionRole role, ushort value)
    {
        if (roles.TryGetValue(role, out var tag))
            _store[tag.Register] = value;
    }

    private ushort Serve(int register)
    {
        if (_resultLagReadsRemaining > 0 &&
            _channel.ObservationRoles.TryGetValue(InjectionRole.AckResult, out var resultTag) &&
            resultTag.Register == register)
        {
            return _publishedResult;
        }

        return Peek(register);
    }

    private bool CoversResult(int startRegister, int count) =>
        _channel.ObservationRoles.TryGetValue(InjectionRole.AckResult, out var tag) &&
        tag.Register >= startRegister && tag.Register <= startRegister + count - 1;

    private ushort Peek(int register) => _store.TryGetValue(register, out var value) ? value : (ushort)0;

    private void PublishStamp(BuildStamp stamp)
    {
        var words = RegisterWords.From32(stamp.Value, RegisterWordOrder.HighWordFirst);
        _store[ControlRegisters.BuildStamp] = words[0];
        _store[ControlRegisters.BuildStamp + 1] = words[1];
    }

    private void PublishScan(uint scans)
    {
        var words = RegisterWords.From32(scans, RegisterWordOrder.HighWordFirst);
        _store[ControlRegisters.ScanCounter] = words[0];
        _store[ControlRegisters.ScanCounter + 1] = words[1];
    }
}
