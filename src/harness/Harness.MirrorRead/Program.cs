namespace Harness.MirrorRead;

/// <summary>
/// <c>harness-mirror-read</c> — read holding registers off the rig's Modbus mirror and say how wide
/// the area actually is.
///
/// <para><b>Why it exists.</b> <c>Harness.Wire</c> has had a working Modbus transport and a map-aware
/// client since phase 2, and no executable exposed a read. Every rig observation therefore went over
/// S7 (<c>rig-read --marker</c>), which addresses <c>%M</c> directly and never consults
/// <c>MB_HOLD_REG</c> — so it can confirm bytes exist in marker memory while saying nothing at all
/// about whether a Modbus client can see them. When the question is precisely "does the area pointer's
/// width reach the wire", the S7 path is the one transport that cannot answer it.</para>
///
/// <para><b>Nothing new on the wire.</b> The transport is <c>Harness.Wire.NModbusTransport</c>,
/// including its <c>ModbusPolicy</c> — retries at zero, timeouts at 3,000 ms, both derived from
/// measurements on this rig. A second client with its own settings would be an uncalibrated one.</para>
///
/// <para><b>This class is the process edge and nothing else.</b> The real sockets, the two console
/// channels and the file write are the three things a unit test cannot supply, so they are supplied
/// here and everything above them lives in <see cref="MirrorReadCli"/>, where it is tested.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args) =>
        MirrorReadCli.Run(args, new ModbusRegisterSourceFactory(), Console.Out, Console.Error, File.WriteAllText);
}
