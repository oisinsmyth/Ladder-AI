using System.Diagnostics;

namespace Harness.Cleanup.Tests;

/// <summary>
/// <b>THE LIVE POSITIVE CONTROL for <see cref="CleanupStructureTests"/>, and the reason its zeros mean
/// anything.</b>
///
/// <para>It really does delete a file and really does launch a process. The same walk with the same
/// predicate must FIND it here — otherwise "no hits over the shipped assembly" is equally what a
/// mistyped needle, a namespace filter that excludes its own subject, or a resolver that never runs
/// would produce.</para>
///
/// <para>Nothing calls this. That is deliberate: the mutation that walked past <c>Harness.RigWrite</c>'s
/// entire suite on 2026-08-14 was a class nothing called, so the control has to be findable by the same
/// means — a walk over every method body in the assembly, reachable or not.</para>
///
/// <para>🔴 It must stay in the TEST assembly. Moving it into <c>Harness.Cleanup</c> is the exact thing
/// the assertion forbids.</para>
/// </summary>
public static class PlantedDestructiveControl
{
    public static void DeleteSomething(string path) => File.Delete(path);

    public static void LaunchSomething(string exe) => Process.Start(exe);
}
