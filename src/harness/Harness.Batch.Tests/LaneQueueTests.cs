using Harness.Batch;

namespace Harness.Batch.Tests;

/// <summary>
/// The queue two agents submit into. Same atomic-file primitive as the claims registry and the lease,
/// for the same reason: a directory of disjoint files is conflict-free under concurrent writers by
/// construction, and two agents enqueueing at once must not be able to lose one another's lane.
/// </summary>
public sealed class LaneQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "batch-queue-" + Guid.NewGuid().ToString("N"));
    private readonly string _binding;
    private readonly string _submission;

    public LaneQueueTests()
    {
        Directory.CreateDirectory(_root);
        _binding = Path.Combine(_root, "binding.json");
        _submission = Path.Combine(_root, "submission.json");
        File.WriteAllText(_binding, "{}");
        File.WriteAllText(_submission, "{}");
    }

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private LaneQueue Queue() => new(Path.Combine(_root, "queue"));

    private Lane Lane(string name) => new(name, _binding, _submission, new[] { Path.Combine(_root, "ir") });

    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_queued_lane_comes_back_out()
    {
        var queue = Queue();

        Assert.True(queue.Enqueue(Lane("valve")).Ok);

        var all = queue.All();
        Assert.Single(all);
        Assert.Equal("valve", all[0].Name);
        Assert.Equal(_binding, all[0].BindingPath);
    }

    /// <summary>
    /// <b>Refused, never overwritten.</b> Whoever queued the lane already there believes it is in the
    /// batch; replacing it would drop a lane and report success to the wrong agent.
    /// </summary>
    [Fact]
    public void Enqueueing_a_name_that_is_ALREADY_queued_is_refused()
    {
        var queue = Queue();
        queue.Enqueue(Lane("valve"));

        var second = queue.Enqueue(Lane("valve"));

        Assert.Equal(EnqueueResult.AlreadyQueued, second.Result);
        Assert.Single(queue.All());
    }

    [Fact]
    public void A_lane_naming_no_program_is_refused()
    {
        var queue = Queue();

        var outcome = queue.Enqueue(new Lane("valve", _binding, _submission, Array.Empty<string>()));

        Assert.Equal(EnqueueResult.Invalid, outcome.Result);
        Assert.Contains("names no program", outcome.Detail);
        Assert.Empty(queue.All());
    }

    /// <summary>
    /// A lane pointing at a document that is not there would be discovered at plan time, after other
    /// agents had queued behind it. Checked at submission instead, where the agent that can fix it is
    /// still present.
    /// </summary>
    [Fact]
    public void A_lane_whose_binding_does_not_exist_is_refused_at_enqueue()
    {
        var queue = Queue();

        var outcome = queue.Enqueue(new Lane("valve", Path.Combine(_root, "nope.json"), _submission, new[] { "ir" }));

        Assert.Equal(EnqueueResult.Invalid, outcome.Result);
        Assert.Contains("binding not found", outcome.Detail);
    }

    /// <summary>
    /// <b>An empty queue reads exactly like a mistyped root</b>, so the listing says so and the store
    /// echoes where it looked. There is nothing else that can distinguish them from inside.
    /// </summary>
    [Fact]
    public void A_queue_that_does_not_exist_yet_lists_as_empty_rather_than_throwing()
    {
        Assert.Empty(Queue().All());
    }

    [Fact]
    public void A_dequeued_lane_is_gone_and_dequeuing_it_again_says_so()
    {
        var queue = Queue();
        queue.Enqueue(Lane("valve"));

        Assert.True(queue.Dequeue("valve"));
        Assert.Empty(queue.All());
        Assert.False(queue.Dequeue("valve"));
    }

    /// <summary>
    /// 🔴 <b>Eight threads released together, eight distinct lanes, and all eight must survive.</b>
    ///
    /// <para>The failure being excluded is not a crash — it is a lane that was accepted and then lost,
    /// which the submitting agent has no way to detect and which would show up only as a batch quietly
    /// smaller than the queue. The barrier is there because a staggered version of this test would pass
    /// against a store that serialises badly.</para>
    /// </summary>
    [Fact]
    public void Eight_concurrent_submitters_all_keep_their_lanes()
    {
        const int agents = 8;

        var queue = Queue();
        var barrier = new Barrier(agents);
        var outcomes = new EnqueueOutcome[agents];
        var threads = new Thread[agents];

        for (var i = 0; i < agents; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                barrier.SignalAndWait();
                outcomes[index] = queue.Enqueue(Lane($"lane-{index}"));
            });
            threads[index].Start();
        }

        foreach (var thread in threads)
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "a submitter never returned.");

        Assert.All(outcomes, o => Assert.Equal(EnqueueResult.Queued, o.Result));
        Assert.Equal(agents, queue.All().Count);
        Assert.Equal(
            Enumerable.Range(0, agents).Select(i => $"lane-{i}").OrderBy(x => x, StringComparer.Ordinal).ToArray(),
            queue.All().Select(l => l.Name).ToArray());
    }

    /// <summary>
    /// The other half: eight threads racing for the SAME name. Exactly one may win, or two agents both
    /// believe their lane is in a batch that can only hold one of them.
    /// </summary>
    [Fact]
    public void Eight_concurrent_submitters_of_ONE_name_produce_exactly_one_winner()
    {
        const int agents = 8;

        var queue = Queue();
        var barrier = new Barrier(agents);
        var outcomes = new EnqueueOutcome[agents];
        var threads = new Thread[agents];

        for (var i = 0; i < agents; i++)
        {
            var index = i;
            threads[index] = new Thread(() =>
            {
                barrier.SignalAndWait();
                outcomes[index] = queue.Enqueue(Lane("contested"));
            });
            threads[index].Start();
        }

        foreach (var thread in threads)
            Assert.True(thread.Join(TimeSpan.FromSeconds(30)), "a submitter never returned.");

        Assert.Equal(1, outcomes.Count(o => o.Result == EnqueueResult.Queued));
        Assert.Equal(agents - 1, outcomes.Count(o => o.Result == EnqueueResult.AlreadyQueued));
        Assert.Single(queue.All());
    }
}
