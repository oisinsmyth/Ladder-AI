using System.Collections.Generic;
using System.Linq;

namespace Ladder.Download
{
    /// <summary>
    /// One message, with what it was recognised as and where in the tree it sat.
    /// </summary>
    public sealed class ClassifiedMessage
    {
        public ClassifiedMessage(
            DownloadMessageNode node,
            DownloadMessageKind kind,
            string? subject,
            int order,
            IEnumerable<string> path)
        {
            Node = node;
            Kind = kind;
            Subject = subject;
            Order = order;
            Path = path == null ? new List<string>() : path.ToList();
        }

        public DownloadMessageNode Node { get; }

        public DownloadMessageKind Kind { get; }

        /// <summary>
        /// The name the message is about: the object name for <see cref="DownloadMessageKind.ObjectLoad"/>,
        /// the configuration subject for <see cref="DownloadMessageKind.NonObjectLoad"/>, the device
        /// for a run-state message. Null when the shape carries no subject.
        /// </summary>
        public string? Subject { get; }

        /// <summary>Pre-order position in the whole tree, so ordering questions are answerable.</summary>
        public int Order { get; }

        /// <summary>
        /// Ancestor texts, outermost first. Preserved because the hardware download's loads sit
        /// under a group node and the software download's do not — the depth is information.
        /// </summary>
        public IReadOnlyList<string> Path { get; }

        public int Depth => Path.Count;

        public string Text => Node.Text;

        public override string ToString() => Kind + ": " + Node.Text;
    }
}
