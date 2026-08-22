using Harness.Run;
using Harness.Wire;

namespace Harness.Loop.Tests;

/// <summary>
/// <b><c>harness-run --publish</c> — the flag that lets a viewer watch a wave WITHOUT taking its
/// connection.</b>
///
/// <para>Exercised through the CLI with an injected publisher, so the whole decision — when the feed is
/// opened, when it is ended, what the run reports about it — is tested without a process, a socket or a
/// file. A decision only reachable through a real run is a decision nobody tests.</para>
/// </summary>
public class PublishFlagTests
{
    private const string Submission = """
    {
      "blockAuthor": "agent-a",
      "runtimeCompression": 1,
      "slotsInWaveSet": 1,
      "resultRegistersPerSlot": 4,
      "computedConflicts": [],
      "model": { "id": "M", "represents": ["ramp"], "validatedAgainstPlantData": true },
      "enumeration": { "clauses": ["REQ-1"], "assertions": ["REQ-1:aaaaaa"] },
      "map": { "providedFor": { "Demo_Count": ["Sampled"] } },
      "vectors": []
    }
    """;

    private const string Binding = """
    {
      "blockNumber": 9001,
      "baseByte": 1000,
      "declaredRegisters": 576,
      "slots": [{
        "slotId": "HBA",
        "vectorTargets": [{ "tag": "Stim_Total", "type": "Time" }],
        "startCondition": "Stim_Start",
        "resultSources": [{ "tag": "Alarm", "type": "Bool",
                           "inertRest": { "value": "false", "basis": "the alarm coil is ANDed with the start condition, so it is off at inert" } }]
      }]
    }
    """;

    private static (int Exit, string Output, RecordingPublisher? Feed) Run(params string[] args)
    {
        var writer = new StringWriter();
        RecordingPublisher? feed = null;

        var exit = LoopCli.Run(
            args.Concat(new[] { "--no-program-under-test" }).ToArray(),
            writer,
            path => path switch
            {
                "sub.json" => Submission,
                "binding.json" => Binding,
                _ => throw new FileNotFoundException(path),
            },
            (_, _) => { },
            connect: null,
            env: _ => null,
            expandProgramPath: path => new[] { path },
            openFeed: path =>
            {
                feed = new RecordingPublisher(path);
                return feed;
            });

        return (exit, writer.ToString(), feed);
    }

    // ---- OFF UNLESS STATED ---------------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>NO FLAG, NO FEED.</b> A run that silently wrote a file somewhere is a surprise, and two runs
    /// publishing to one guessed path would overwrite each other with nothing to say which a viewer was
    /// watching.
    /// </summary>
    [Fact]
    public void Without_the_flag_no_feed_is_opened_at_all()
    {
        var (_, output, feed) = Run("--submission", "sub.json", "--binding", "binding.json");

        Assert.Null(feed);
        Assert.DoesNotContain("PUBLISHING", output, StringComparison.Ordinal);
        Assert.DoesNotContain("FEED:", output, StringComparison.Ordinal);
    }

    [Fact]
    public void With_the_flag_a_feed_is_opened_at_the_stated_path()
    {
        var (_, output, feed) = Run("--submission", "sub.json", "--binding", "binding.json",
            "--publish", "run.mirrorfeed");

        Assert.NotNull(feed);
        Assert.Equal("run.mirrorfeed", feed!.Destination);
        Assert.Contains("PUBLISHING every read to run.mirrorfeed", output, StringComparison.Ordinal);
    }

    // ---- ENDED, WHATEVER HAPPENED --------------------------------------------------------------------

    /// <summary>
    /// 🔴 <b>THE FEED IS ENDED EVEN ON THE PATH WHERE THE RUN STOPS EARLY.</b> A publisher that simply
    /// stopped writing is indistinguishable from one that was killed, and the viewer reports those as
    /// DIFFERENT states precisely because they are. Ending on a stopped run is what stops a viewer
    /// reporting an incident about an orderly refusal.
    /// </summary>
    [Fact]
    public void The_feed_is_ended_even_when_the_run_stops_before_the_wave()
    {
        // No --verify, so the gateway REFUSES and the loop stops at deployment. That is a real outcome.
        var (exit, _, feed) = Run("--submission", "sub.json", "--binding", "binding.json",
            "--publish", "run.mirrorfeed");

        Assert.Equal(LoopExit.DidNotRun, exit);
        Assert.NotNull(feed);
        Assert.True(feed!.Ended, "the feed was left RUNNING after the run finished, so a viewer would report " +
                                 "the wave as having died rather than as having stopped for a stated reason.");
    }

    // ---- THE COUNTS ARE REPORTED, INCLUDING ZERO -----------------------------------------------------

    /// <summary>
    /// <b>Printed on every publishing run, whatever the counts.</b> A line that appears only on failure
    /// teaches a reader that its absence means everything was written — and a feed that quietly stopped
    /// writing is otherwise indistinguishable from a wave that quietly stopped reading.
    /// </summary>
    [Fact]
    public void The_run_reports_the_feeds_publishes_and_failures()
    {
        var (_, output, _) = Run("--submission", "sub.json", "--binding", "binding.json",
            "--publish", "run.mirrorfeed");

        Assert.Contains("FEED:", output, StringComparison.Ordinal);
        Assert.Contains("publish(es) FAILED", output, StringComparison.Ordinal);
    }

    // ---- CONTRADICTIONS ARE REFUSED BY NAME ----------------------------------------------------------

    /// <summary>
    /// <b>A generate-only run makes no reads, so a feed from it would show a publisher that is running and
    /// will never read anything</b> — a live-looking state for a run that cannot produce data. Refused by
    /// name rather than ignored: a flag that silently does nothing is one somebody will believe was honoured.
    /// </summary>
    [Fact]
    public void Publish_and_generate_only_contradict_each_other_and_are_refused()
    {
        var (exit, output, feed) = Run("--submission", "sub.json", "--binding", "binding.json",
            "--generate-only", "--publish", "run.mirrorfeed");

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Null(feed);
        Assert.Contains("contradict each other", output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_publish_path_is_refused()
    {
        var (exit, output, feed) = Run("--submission", "sub.json", "--binding", "binding.json",
            "--publish", "   ");

        Assert.Equal(LoopExit.NothingExamined, exit);
        Assert.Null(feed);
        Assert.Contains("no default feed location", output, StringComparison.Ordinal);
    }

    /// <summary>The usage text names the flag and says why it exists rather than only what it takes.</summary>
    [Fact]
    public void The_usage_text_explains_why_a_second_socket_is_not_an_option()
    {
        var writer = new StringWriter();
        LoopCli.Run(Array.Empty<string>(), writer, _ => string.Empty, (_, _) => { });

        var usage = writer.ToString();

        Assert.Contains("--publish", usage, StringComparison.Ordinal);
        Assert.Contains("ONE connection per instance", usage, StringComparison.Ordinal);
        Assert.Contains("TWO SAMPLES AT TWO INSTANTS", usage, StringComparison.Ordinal);
    }

    /// <summary>A publisher that records what was asked of it and touches no filesystem.</summary>
    private sealed class RecordingPublisher : IMirrorFeedPublisher
    {
        public RecordingPublisher(string destination) => Destination = destination;

        public string Destination { get; }

        public bool Began { get; private set; }

        public bool Ended { get; private set; }

        public int Publishes { get; private set; }

        public int Failures => 0;

        public string? LastFailure => null;

        public long Published => Publishes;

        public void Begin(MirrorFeedIdentity identity) => Began = true;

        public void Publish(int startRegister, ushort[] values, DateTimeOffset observedUtc) => Publishes++;

        public void End() => Ended = true;

        public void Dispose() => End();
    }
}
