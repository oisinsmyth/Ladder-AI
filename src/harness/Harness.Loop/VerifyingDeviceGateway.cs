using Harness.Map;
using Harness.Results;
using Harness.Wire;

namespace Harness.Loop;

/// <summary>
/// 🔴 <b>A GATEWAY THAT MAKES THE DEVICE CARRY THE CODE BY PROVING IT ALREADY DOES — AND WHICH CANNOT
/// SAY <c>Loaded</c> FROM A DECLARATION.</b>
///
/// <para><b>The problem it solves.</b> <see cref="LoopRun.Execute"/> deploys unconditionally, and the
/// only real gateway runs import-all → compile-all → <c>download-probe --disruptive</c>. So running a
/// wave against an <i>already-deployed</i> program was impossible without a gateway that reported a
/// deployment it did not perform — and *** A GATEWAY REPORTING <c>Loaded</c> WITHOUT LOADING IS ONE EDIT
/// FROM A GATEWAY THAT LIES. *** That objection is why this is not a "skip deploy" flag.</para>
///
/// <para><b>What it actually is:</b> <c>Deploy</c> means <i>make the device carry this code and prove
/// it</i>. <see cref="OpennessDeviceGateway"/>-shaped implementations do that by CHANGING the device.
/// This one does it by MEASURING the device and changing nothing.</para>
///
/// <para>*** THE EVIDENCE IS THE BUILD STAMP, AND IT IS STRICTLY STRONGER THAN A LOAD MANIFEST. *** The
/// stamp is a constant generated into the copy layer's own code — it is on the wire <b>only if that
/// exact code is executing</b>. A load manifest says what TIA <i>reported sending</i>; the stamp says
/// <b>what is running now</b>. A manifest cannot distinguish a download that landed from one that landed
/// and was then overwritten, stopped, or replaced; the stamp reads differently in every one of those
/// cases.</para>
///
/// <para><b>The invariant, and there is no way around it:</b> <c>Loaded</c> is true only when a stamp was
/// READ FROM THE DEVICE and equals the staged build's. There is no parameter, no override, no
/// environment variable and no constructor argument that asserts it otherwise — the field is computed
/// inside <see cref="Deploy"/> from the comparison and from nothing else, and
/// <c>VerifyingDeviceGatewayTests</c> pins that by reflection.</para>
///
/// <para><b>Unreadable is NOT CHECKED, and NOT CHECKED refuses.</b> A transport that will not open, a
/// read that throws, a span too short to hold the stamp: every one of them returns
/// <c>Attempted = true, Loaded = false</c> with the reason. <i>Empty is not clean</i> — an unreachable
/// device must never read as a verified one, which is the same rule that makes an absent manifest
/// <see cref="ManifestPresence.NotAvailable"/> rather than <c>Loaded</c>.</para>
///
/// <para><b>WHAT EACH CONSUMER GETS, AND HOW IT DIFFERS FROM THE DEPLOYING PATH — a caller must not be
/// able to confuse the two.</b></para>
/// <list type="bullet">
/// <item><b><see cref="VersionCheck"/> gets exactly what it gets on the deploying path</b>, and it is
/// the same fact measured twice: it reads the version register itself, off the same device, after the
/// same map. Nothing here weakens it, and nothing here substitutes for it — this gateway does not set a
/// flag the version check then trusts.</item>
/// <item><b><see cref="ManifestPresence"/> is where the difference lives.</b> On the deploying path the
/// manifest is TIA's own report of what it sent, and <c>Loaded</c> means <i>the download named these
/// objects</i>. Here the manifest is the STAGED SET, and <c>Loaded</c> means <i>a copy layer generated
/// from exactly this set is executing</i> — because <c>BuildStamp.Of</c> hashes the map, the bindings,
/// the naming AND the program-under-test's IR TEXT into the stamp.</item>
/// <item>🔴 <b>THE HONEST LIMIT OF THAT, STATED RATHER THAN GLOSSED.</b> The stamp is published by the
/// COPY LAYER. A match proves a copy layer built from this exact set is running; it does <b>not</b>
/// independently prove each program-under-test block is present and consistent on the device — a
/// download that landed the copy layer and dropped a block would still match. The deploying path's
/// manifest is the check for that, and this path does not have one. <b>It is stronger about what is
/// EXECUTING and weaker about what is PRESENT</b>, and a caller relying on the second should deploy.</item>
/// </list>
///
/// <para><b>It never writes.</b> The only device operation is
/// <see cref="IRegisterTransport.ReadHoldingRegisters"/> — no import, no compile, no download, no CPU
/// state change, and no <see cref="MirrorClient"/> write path is reachable from here. Verification that
/// mutates what it verifies is not verification.</para>
/// </summary>
public sealed class VerifyingDeviceGateway : IDeviceGateway
{
    private readonly Func<IRegisterTransport> _open;
    private readonly RegisterMap _map;
    private readonly RegisterWordOrder _wordOrder;

    private IRegisterTransport? _transport;

    /// <param name="map">
    /// The map the staged build was generated for. <b>The stamp's registers come from here</b>, never
    /// from a caller-supplied address — an address arriving as data is how a verifier ends up reading
    /// the right value out of the wrong place.
    /// </param>
    /// <param name="open">
    /// Opens the transport. A factory rather than an instance so that a failure to CONNECT is observed
    /// inside <see cref="Deploy"/> and reported as unverified, instead of throwing before the gateway
    /// exists and looking like a configuration error.
    /// </param>
    /// <param name="wordOrder">
    /// ⚠️ The 32-bit order the stamp is reassembled under — <b>the same uncalibrated transform as every
    /// other 32-bit value in this system</b>. Measured on the rig as <c>HighWordFirst</c>, which is the
    /// default; a mismatch under one order that would have MATCHED under the other is reported as its own
    /// outcome rather than as a failed verification, because those are different facts.
    /// </param>
    public VerifyingDeviceGateway(
        RegisterMap map,
        Func<IRegisterTransport> open,
        RegisterWordOrder wordOrder = RegisterWordOrder.HighWordFirst)
    {
        _map = map ?? throw new ArgumentNullException(nameof(map));
        _open = open ?? throw new ArgumentNullException(nameof(open));
        _wordOrder = wordOrder;
    }

    /// <summary>
    /// <b>Reads the stamp off the device and compares it against the staged build.</b> Nothing is
    /// imported, compiled, downloaded or written.
    ///
    /// <para><c>objects</c> is not ignored — it is what the manifest reports, so a caller can see that
    /// the SAME set was verified. But <b>membership of that list is never what makes <c>Loaded</c>
    /// true</b>: the list is the claim, and the stamp is the evidence.</para>
    /// </summary>
    public DeploymentOutcome Deploy(IReadOnlyList<HarnessObject> objects, BuildStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(objects);

        var staged = objects.Select(o => o.Name).ToHashSet(StringComparer.Ordinal);
        var empty = new HashSet<string>(StringComparer.Ordinal);

        if (stamp.Value == 0)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
                "the staged build stamp is zero, so there is nothing to verify against. Unwritten bit memory reads as zero, "
                + "so a zero stamp would 'match' a CPU that never ran the copy layer at all — the exact half-applied download "
                + "the version register exists to catch.");
        }

        IRegisterTransport transport;
        try
        {
            transport = _transport ??= _open();
        }
        catch (Exception ex)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
                $"NOT CHECKED — the transport would not open, so nothing was read from the device: {ex.GetType().Name}: {ex.Message}. "
                + "*** AN UNREACHABLE DEVICE IS NOT A VERIFIED ONE. *** This gateway proves what is RUNNING and it proved nothing.");
        }

        ushort[] registers;
        try
        {
            registers = transport.ReadHoldingRegisters(_map.Version.Register, _map.Version.Length);
        }
        catch (Exception ex)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
                $"NOT CHECKED — the version register could not be read: {ex.GetType().Name}: {ex.Message}. "
                + "Empty is not clean: a read that failed is not a stamp that matched.");
        }

        // Empty is not clean, again: a short read is not a stamp. Reassembling from whatever arrived would
        // produce a number, and a number here is indistinguishable from evidence.
        if (registers is null || registers.Length < _map.Version.Length)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
                $"NOT CHECKED — the version read returned {registers?.Length ?? 0} register(s) where the map places the stamp in "
                + $"{_map.Version.Length}. A short read cannot be reassembled into a stamp, and doing it anyway would turn a "
                + "truncated read into a plausible number.");
        }

        var observed = RegisterWords.To32(registers[0], registers[1], _wordOrder);

        if (observed == stamp.Value)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: true, staged,
                $"VERIFIED, NOT DEPLOYED. The device's version register reads 16#{observed:X8}, which is the staged build's own "
                + $"stamp — so the code that publishes it IS EXECUTING. Nothing was imported, compiled, downloaded or written. "
                + $"The manifest lists the {staged.Count} object(s) this verification covered; membership of that list is the "
                + "claim, and the stamp is the evidence.");
        }

        // A stamp equal to the expectation with its halves swapped is a CALIBRATION result, not a failed
        // verification, and saying so is what stops somebody re-downloading a device that is already right.
        if (RegisterWords.Swapped(observed) == stamp.Value)
        {
            return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
                $"REFUSED — the device reads 16#{observed:X8} and the staged build is 16#{stamp.Value:X8}, which are the same value "
                + $"WITH THE TWO 16-BIT HALVES SWAPPED. That is almost certainly the uncalibrated word order ({_wordOrder}) rather "
                + "than a different build — see RegisterWordOrder. It is still a REFUSAL: a verifier that accepted a value it had to "
                + "reinterpret would accept anything.");
        }

        return new DeploymentOutcome(Attempted: true, Loaded: false, empty,
            $"REFUSED — the device's version register reads 16#{observed:X8} and the staged build is 16#{stamp.Value:X8}. "
            + "*** THE DEVICE IS NOT RUNNING THIS BUILD. *** Every address this loop holds was derived for the staged map, so a wave "
            + "run now would read the right registers of the wrong program and report a confident wrong answer. Deploy the staged "
            + "build, or point this loop at the build the device is actually carrying.");
    }

    /// <summary>The transport the verification already used. Opened once; the loop never gets a second socket.</summary>
    public IRegisterTransport Open() =>
        _transport ?? throw new InvalidOperationException(
            "no transport is open. Reaching this means the loop tried to run a wave after a verification that did not succeed, "
            + "which would be a defect in the loop's own ordering rather than in the device.");

    public void Dispose()
    {
        _transport?.Dispose();
        _transport = null;
    }
}
