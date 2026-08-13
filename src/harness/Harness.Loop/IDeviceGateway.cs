using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Loop;

/// <summary>What a deployment attempt produced.</summary>
/// <param name="Attempted">
/// Whether the gateway tried at all. <b>False is not a failed download</b> — it is a download that did
/// not happen, and the two call for different actions.
/// </param>
/// <param name="Loaded">Whether every object supplied appears in the device's own load manifest (§9a).</param>
/// <param name="Manifest">
/// The objects the device reported loading, verbatim. <b>Positive evidence of transfer</b>, replacing an
/// inference from the absence of a failure message — §9c forbids the latter.
/// </param>
public sealed record DeploymentOutcome(bool Attempted, bool Loaded, IReadOnlySet<string> Manifest, string Detail);

/// <summary>
/// <b>The device boundary, and the only thing in the loop that is not pure arithmetic.</b>
///
/// <para>Import, compile, download and the socket all live behind this. Everything above it — the map,
/// the gate, the copy layer, the retention assertion, the wave sequence, the package — runs with no
/// device present, which is what lets the loop be exercised end to end while the rig is held by another
/// lane.</para>
/// </summary>
public interface IDeviceGateway : IDisposable
{
    /// <summary>Import, compile and download the generated objects, and report what the device says it loaded.</summary>
    DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp);

    /// <summary>Open a transport onto the mirror. Called only after a deployment that was attempted and loaded.</summary>
    IRegisterTransport Open();
}

/// <summary>
/// The default gateway: <b>it refuses, and says so.</b>
///
/// <para><b>Not a stub that returns success.</b> A gateway that quietly reported a deployment nobody
/// performed would manufacture exactly the green this project exists to prevent — the loop would run a
/// wave against a mirror nothing maintains, every register would agree with itself, and the frozen-mirror
/// trap would be reached from a new direction. So this refuses at the boundary, the loop reports
/// <see cref="LoopOutcome.NotDeployed"/>, and <b>no packages are produced at all</b> rather than packages
/// that describe nothing.</para>
///
/// <para>It exists because the rig is held by another lane. Replacing it is the device work, and what
/// that work owes is listed on <see cref="LoopResult.OwedOnTheDevice"/>.</para>
/// </summary>
public sealed class RefusingDeviceGateway : IDeviceGateway
{
    public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp) =>
        new(Attempted: false, Loaded: false, new HashSet<string>(StringComparer.Ordinal),
            $"no device gateway is configured, so the {objects.Count} generated object(s) were not deployed. "
            + "This is NOT a failed download: nothing was attempted, and nothing about the block has been tested. "
            + "Supply an IDeviceGateway that performs import, compile and download to run this loop against hardware.");

    public IRegisterTransport Open() =>
        throw new InvalidOperationException(
            "the refusing gateway opens no transport. Reaching this means the loop tried to run a wave after a deployment it was told did not happen, which would be a defect in the loop's own ordering rather than in the device.");

    public void Dispose() { }
}
