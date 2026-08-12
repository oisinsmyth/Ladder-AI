using System;
using System.Globalization;

namespace Ladder.Wave
{
    /// <summary>
    /// Who wrote a marker. Enough to answer, on startup, "is this MY marker or one a dead process
    /// left behind?" — which is the question X-C item 2 exists to make answerable.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A machine name and a process id are not enough on their own: process ids are reused, and a
    /// restarted coordinator can be handed the pid its own corpse had. The instance GUID is minted
    /// once per process at static-initialisation time and never reused, so equality of the whole
    /// token means the SAME RUNNING PROCESS and nothing weaker.
    /// </para>
    /// <para>
    /// The pid is carried for the human reading the log, not for the comparison.
    /// </para>
    /// </remarks>
    public sealed class CoordinatorIdentity : IEquatable<CoordinatorIdentity>
    {
        private static readonly Lazy<CoordinatorIdentity> CurrentIdentity =
            new Lazy<CoordinatorIdentity>(CreateForThisProcess);

        /// <param name="machineName">The machine the coordinator runs on.</param>
        /// <param name="processId">Its process id, for forensics. Never the basis of the comparison.</param>
        /// <param name="instanceId">A value minted once per process and never reused.</param>
        public CoordinatorIdentity(string machineName, int processId, Guid instanceId)
        {
            MachineName = string.IsNullOrWhiteSpace(machineName) ? "<unknown-host>" : machineName.Trim();
            ProcessId = processId;
            InstanceId = instanceId;
        }

        /// <summary>The identity of the process running right now.</summary>
        public static CoordinatorIdentity Current => CurrentIdentity.Value;

        /// <summary>The machine the coordinator runs on.</summary>
        public string MachineName { get; }

        /// <summary>The coordinator's process id. Forensics only.</summary>
        public int ProcessId { get; }

        /// <summary>Minted once per process, never reused. This is what equality turns on.</summary>
        public Guid InstanceId { get; }

        /// <summary>The single-line form written into the marker file.</summary>
        public string ToToken() =>
            MachineName + "/" + ProcessId.ToString(CultureInfo.InvariantCulture) + "/" + InstanceId.ToString("D");

        /// <summary>
        /// Parses <see cref="ToToken"/>. Returns null on anything it does not recognise — the caller
        /// treats an unparseable coordinator as "not me", which is the conservative reading.
        /// </summary>
        public static CoordinatorIdentity? TryParse(string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return null;
            }

            var parts = token!.Split('/');
            if (parts.Length != 3)
            {
                return null;
            }

            if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pid))
            {
                return null;
            }

            if (!Guid.TryParse(parts[2], out var instance))
            {
                return null;
            }

            return new CoordinatorIdentity(parts[0], pid, instance);
        }

        /// <inheritdoc />
        public bool Equals(CoordinatorIdentity? other) =>
            other != null &&
            InstanceId == other.InstanceId &&
            string.Equals(MachineName, other.MachineName, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as CoordinatorIdentity);

        /// <inheritdoc />
        public override int GetHashCode() => InstanceId.GetHashCode();

        /// <inheritdoc />
        public override string ToString() => ToToken();

        private static CoordinatorIdentity CreateForThisProcess()
        {
            var machine = "<unknown-host>";
            try
            {
                machine = Environment.MachineName;
            }
            catch (InvalidOperationException)
            {
                // Some hosts refuse it. A marker with an unknown host still identifies the process,
                // which is the part the comparison uses.
            }

            var pid = 0;
            try
            {
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    pid = process.Id;
                }
            }
            catch (PlatformNotSupportedException)
            {
                // Forensic detail only; its absence never changes a decision.
            }
            catch (InvalidOperationException)
            {
            }

            return new CoordinatorIdentity(machine, pid, Guid.NewGuid());
        }
    }
}
