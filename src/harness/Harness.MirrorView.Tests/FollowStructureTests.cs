using System.Reflection;

namespace Harness.MirrorView.Tests;

/// <summary>
/// 🔴 <b>THE FOLLOW MODE'S OWN STRUCTURAL CLAIM: THIS VIEWER CANNOT CAUSE A READ.</b>
///
/// <para>The truthfulness argument for following a wave's feed is that the page shows the bytes the
/// harness acted on, stamped with when the harness read them. <b>The one edit that would silently undo
/// it</b> is a path by which the viewer obtains fresher data — a publisher held here and sampled, a
/// gateway that refreshes when the page asks, a client constructed on this side. Every one of those
/// brings back two samples at two instants MINUS the second socket, and the page would look exactly as it
/// does now.</para>
///
/// <para>So it is asserted about the compiled assembly rather than argued in a comment, with the same
/// three halves as its siblings in <see cref="MirrorViewStructureTests"/>: a denominator, a live positive
/// control, and a resolution control proving tokens resolve in this module at all. The walk itself is
/// that file's, reused rather than re-written.</para>
/// </summary>
public class FollowStructureTests
{
    private static Assembly ShippedAssembly => typeof(MirrorFollowPoller).Assembly;

    private static Assembly ThisTestAssembly => typeof(PlantedFeedPublisherControl).Assembly;

    /// <summary>
    /// *** THE CLAIM. *** No method in the shipped viewer names the PUBLISHER — the type that would let
    /// this process produce feed content of its own. A viewer that could publish could publish something
    /// it had not read, and the page's whole guarantee is that it did not.
    /// </summary>
    [Fact]
    public void NothingInTheShippedAssembly_NamesTheFeedPublisher()
    {
        var scan = IlWalk.Scan(ShippedAssembly, IsFeedPublisherMember);

        Assert.True(scan.BodiesExamined > 0,
            "The walk examined NO method bodies, so finding nothing proves nothing. " +
            $"Types: {scan.TypesEnumerated}, methods: {scan.MethodsEnumerated}.");

        Assert.True(scan.Hits.Count == 0,
            "harness-mirror-view holds the feed PUBLISHER. It is a reader of feeds and must not be able to " +
            "produce one: " + string.Join("; ", scan.Hits));
    }

    /// <summary>
    /// *** THE LIVE POSITIVE CONTROL. *** The same walk with the same predicate, over an assembly that
    /// really does construct a publisher, must FIND it. Without this, "no hits" is equally what a mistyped
    /// needle produces — and that is the most reassuring output this file could emit.
    /// </summary>
    [Fact]
    public void TheWalk_FindsAPlantedFeedPublisher()
    {
        var scan = IlWalk.Scan(ThisTestAssembly, IsFeedPublisherMember);

        Assert.True(scan.BodiesExamined > 0, "the control examined no bodies either — the walk is broken.");
        Assert.Contains(scan.Hits, hit => hit.Contains(nameof(PlantedFeedPublisherControl), StringComparison.Ordinal));
    }

    /// <summary>
    /// *** THE RESOLUTION CONTROL IN THE MODULE UNDER TEST. *** The planted control proves the predicate
    /// fires; it does not prove tokens resolve inside this assembly's own module. <c>MirrorFollowPoller</c>
    /// certainly names <c>MirrorFeedDocument.Compose</c>, so the walk must see it there.
    /// </summary>
    [Fact]
    public void TheWalk_ResolvesFeedTokensInsideTheShippedAssembly()
    {
        var scan = IlWalk.Scan(ShippedAssembly, m =>
            m is not Type &&
            m.Name == nameof(Harness.Wire.MirrorFeedDocument.Compose) &&
            m.DeclaringType?.FullName == "Harness.Wire.MirrorFeedDocument");

        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies.");
        Assert.True(scan.Hits.Count > 0,
            "The walk found no reference to MirrorFeedDocument.Compose, which MirrorFollowPoller certainly " +
            "makes. The scanner cannot resolve Harness.Wire tokens in this module, so the zero above proves nothing.");
    }

    /// <summary>
    /// <b>The residual, pinned.</b> <c>Harness.Wire</c> DOES carry a publisher — the harness proper must
    /// publish — and this binary references that assembly. The property checked is that nothing here NAMES
    /// it, not that the capability has been deleted from the process. If the publisher ever disappears,
    /// this goes red and forces somebody to re-read the claim rather than leaving a caveat that has
    /// quietly stopped being true.
    /// </summary>
    [Fact]
    public void TheResidualIsReal_HarnessWireStillCarriesThePublisherThisBinaryAvoids()
    {
        Assert.NotNull(typeof(Harness.Wire.MirrorFeedPublisher)
            .GetMethod(nameof(Harness.Wire.MirrorFeedPublisher.Publish)));
    }

    /// <summary>
    /// <b>The follow poller is given a feed SOURCE and nothing else that touches the outside world.</b>
    /// Asserted on the constructor's parameter types: a socket factory, a transport, a device fence or a
    /// publisher appearing here is the shape of the regression, and it would be invisible in behaviour
    /// because a poller that could refresh would simply look fresher.
    /// </summary>
    [Fact]
    public void TheFollowPoller_TakesNothingThatCouldReachADevice()
    {
        var parameters = typeof(MirrorFollowPoller)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(p => p.ParameterType.Name)
            .ToArray();

        Assert.Equal(
            new[] { nameof(MirrorViewOptions), nameof(MirrorMap), nameof(MirrorState), "IMirrorFeedSource", "Func`1" },
            parameters);
    }

    private static bool IsFeedPublisherMember(MemberInfo member) =>
        (member is Type type ? type.FullName : member.DeclaringType?.FullName)
        ?.StartsWith("Harness.Wire.MirrorFeedPublisher", StringComparison.Ordinal) == true
        || (member is Type t && t.FullName == "Harness.Wire.IMirrorFeedPublisher")
        || member.DeclaringType?.FullName == "Harness.Wire.IMirrorFeedPublisher";
}

/// <summary>
/// *** THE LIVE POSITIVE CONTROL FOR THE PUBLISHER WALK. *** This class deliberately does the thing the
/// shipped assembly must never do. It must stay in the TEST assembly and must never move.
/// </summary>
internal static class PlantedFeedPublisherControl
{
    public static void Publish(string path)
    {
        using var publisher = new Harness.Wire.MirrorFeedPublisher(path);
        publisher.Begin(new Harness.Wire.MirrorFeedIdentity(1, "x", 1, Harness.Wire.RegisterWordOrder.HighWordFirst));
        publisher.Publish(0, new ushort[] { 1 }, DateTimeOffset.UtcNow);
    }
}
