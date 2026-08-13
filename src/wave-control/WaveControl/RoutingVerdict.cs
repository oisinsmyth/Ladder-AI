using System;

namespace Ladder.Wave
{
    /// <summary>
    /// What <see cref="ChangeRouter"/> decided about one change: a queue, or a refusal with a reason.
    /// Immutable and constructible only by the router.
    /// </summary>
    public sealed class RoutingVerdict
    {
        private RoutingVerdict(
            ChangedObject changedObject,
            DownloadQueue queue,
            RoutingRefusal refusal,
            string reason)
        {
            ChangedObject = changedObject;
            Queue = queue;
            Refusal = refusal;
            Reason = reason;
        }

        /// <summary>The change this verdict is about.</summary>
        public ChangedObject ChangedObject { get; }

        /// <summary>
        /// The queue it belongs in, or <see cref="DownloadQueue.Unassigned"/> when it was refused. The
        /// zero value means "not routed", so a caller that reads the queue without checking
        /// <see cref="Routed"/> gets a value that matches no queue rather than the permissive one.
        /// </summary>
        public DownloadQueue Queue { get; }

        /// <summary>Why it was refused, or <see cref="RoutingRefusal.None"/>.</summary>
        public RoutingRefusal Refusal { get; }

        /// <summary>The reason, in a sentence fit for an unattended log.</summary>
        public string Reason { get; }

        /// <summary>TRUE only when a queue was chosen.</summary>
        public bool Routed => Refusal == RoutingRefusal.None && Queue != DownloadQueue.Unassigned;

        /// <summary>TRUE when the change accumulates for a drain boundary rather than flowing through a wave.</summary>
        public bool Deferred => Queue == DownloadQueue.DeferredQueue;

        /// <summary>One line for the log.</summary>
        public string ToLogLine()
        {
            var name = ChangedObject.Name.Length == 0 ? "<unnamed>" : ChangedObject.Name;

            return Routed
                ? "ROUTE " + name + " -> " + Queue + " :: " + Reason
                : "ROUTE-REFUSED " + name + " (" + Refusal + ") :: " + Reason;
        }

        /// <inheritdoc />
        public override string ToString() => ToLogLine();

        internal static RoutingVerdict Route(ChangedObject changedObject, DownloadQueue queue, string reason)
        {
            if (queue == DownloadQueue.Unassigned)
            {
                throw new InvalidOperationException(
                    "A routed verdict must name a queue. Unreachable from ChangeRouter; kept so that a " +
                    "future edit which forgets the queue fails loudly instead of producing a verdict " +
                    "that reports Routed = false while claiming to have routed something.");
            }

            return new RoutingVerdict(changedObject, queue, RoutingRefusal.None, reason);
        }

        internal static RoutingVerdict Refuse(ChangedObject changedObject, RoutingRefusal refusal, string reason)
        {
            if (refusal == RoutingRefusal.None)
            {
                throw new InvalidOperationException(
                    "A refusal must name a reason code. Unreachable from ChangeRouter; kept so a refusal " +
                    "cannot be built that reads as a successful routing.");
            }

            return new RoutingVerdict(changedObject, DownloadQueue.Unassigned, refusal, reason);
        }
    }
}
