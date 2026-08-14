namespace Harness.RigControl;

/// <summary>
/// The composition root — the ONE place where the decision layer and a real socket are allowed to meet.
///
/// <para>Everything above it is a pure function of arguments and files, which is what lets the tests
/// drive the whole command line, every gate and every exit code with no PLC in the room.</para>
///
/// <para><b>When Sharp7 is not present at build time</b> the live transport is excluded from the
/// assembly and this hands the CLI a factory that THROWS with a named reason. It is never a silent
/// no-op and never a fake that reports success: a build that cannot reach a device must say so at the
/// moment somebody asks it to, and only after the fence has already allowed and <c>--yes</c> has
/// already been given — so the refusal message is read by the one person who needed it.</para>
/// </summary>
public static class Program
{
    public static int Main(string[] args) =>
        RigControlCli.Run(
            args,
            Environment.GetEnvironmentVariable,
            CreateTransport,
            Console.Out,
            Console.Error);

    private static IRunTransitionTransport CreateTransport()
    {
#if SHARP7
        return new Sharp7RunTransition();
#else
        throw new NotSupportedException(
            "This build of rig-control has NO device transport: Sharp7 was not present when it was " +
            "compiled, so Sharp7RunTransition is excluded from the assembly entirely. The fence, the " +
            "plan and every refusal path are intact and were just exercised; only the device half is " +
            "absent. Rebuild with -p:Sharp7Path=<path to Sharp7.dll>.");
#endif
    }
}
