using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Ladder.Wave
{
    /// <summary>
    /// The marker's on-disk form: a line-oriented UTF-8 text file terminated by a sentinel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// *** THE TRAILING <c>end</c> SENTINEL IS THE WHOLE POINT OF CHOOSING THIS FORMAT. *** A
    /// truncated write leaves a file that parses fine up to the point it stops, and any format
    /// without a terminator would read a half-written marker as a complete one carrying fewer slots.
    /// With the sentinel, a torn write is DETECTABLE, and a detected tear reads as "a wave was in
    /// progress, details unreadable" — the direction X-C requires.
    /// </para>
    /// <para>
    /// No JSON, and deliberately: the library references nothing, so it can be referenced from
    /// <c>openness-cli</c> (net48) without dragging a package graph into the one tool whose every
    /// rebuild costs a TIA Openness (Path,FileHash) re-approval. The format is also readable by a
    /// human standing at the machine at 3am, which is exactly who finds one of these.
    /// </para>
    /// <para>
    /// UNKNOWN KEYS ARE A PARSE FAILURE, NOT A SKIP. At <c>format=1</c> there are no optional keys;
    /// a key this build does not know is either corruption or a writer it does not understand, and
    /// both are cases where claiming to have read the marker would be a lie. A future format bumps
    /// the version, which is refused here for the same reason and by the same branch.
    /// </para>
    /// </remarks>
    internal static class WaveMarkerFormat
    {
        internal const int CurrentFormatVersion = 1;
        internal const string Sentinel = "end";

        private const string HeaderComment =
            "# ladder wave-in-progress marker (spec 16.3 X-C). Its PRESENCE means a wave was in flight.\n" +
            "# Found on startup, this is not an error: it is the expected signal after a coordinator death.\n" +
            "# Every test and result from that wave is INVALID and is discarded, never re-read.\n";

        internal static string Serialize(WaveMarker marker)
        {
            var sb = new StringBuilder();
            sb.Append(HeaderComment);
            Append(sb, "format", CurrentFormatVersion.ToString(CultureInfo.InvariantCulture));
            Append(sb, "wave-id", marker.WaveId);
            Append(sb, "started-utc", marker.StartedUtc.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture));
            Append(sb, "program-version", marker.ProgramVersion);
            Append(sb, "coordinator", marker.Coordinator.ToToken());

            foreach (var slot in marker.Slots)
            {
                Append(sb, "slot", slot);
            }

            sb.Append(Sentinel).Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Parses marker text. Returns null and sets <paramref name="problem"/> for anything it does
        /// not fully understand — the caller turns that into "a wave was in progress, details
        /// unreadable", never into "no wave".
        /// </summary>
        internal static WaveMarker? TryParse(string text, out string problem)
        {
            problem = string.Empty;

            if (string.IsNullOrEmpty(text))
            {
                problem = "the marker file is present but empty — a wave was begun and the write did not complete";
                return null;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

            var meaningful = lines
                .Select(l => l.Trim())
                .Where(l => l.Length > 0 && !l.StartsWith("#", StringComparison.Ordinal))
                .ToList();

            if (meaningful.Count == 0)
            {
                problem = "the marker file carries no content lines — the write did not get past its header";
                return null;
            }

            if (!string.Equals(meaningful[meaningful.Count - 1], Sentinel, StringComparison.Ordinal))
            {
                problem = "the marker file has no '" + Sentinel + "' terminator — the write was torn off part way";
                return null;
            }

            meaningful.RemoveAt(meaningful.Count - 1);

            string? formatText = null;
            string? waveId = null;
            string? startedText = null;
            string? programVersion = null;
            string? coordinatorToken = null;
            var slots = new List<string>();

            foreach (var line in meaningful)
            {
                var split = line.IndexOf('=');
                if (split <= 0)
                {
                    problem = "the marker file carries a line that is not 'key=value': '" + Truncate(line) + "'";
                    return null;
                }

                var key = line.Substring(0, split).Trim();
                var value = Unescape(line.Substring(split + 1));

                switch (key)
                {
                    case "format": formatText = value; break;
                    case "wave-id": waveId = value; break;
                    case "started-utc": startedText = value; break;
                    case "program-version": programVersion = value; break;
                    case "coordinator": coordinatorToken = value; break;
                    case "slot": slots.Add(value); break;
                    default:
                        problem = "the marker file carries a key this build does not know ('" + Truncate(key) +
                                  "') — either corruption, or a marker written by a newer format that did " +
                                  "not bump its version";
                        return null;
                }
            }

            if (formatText == null ||
                !int.TryParse(formatText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var format))
            {
                problem = "the marker file states no readable format version";
                return null;
            }

            if (format != CurrentFormatVersion)
            {
                problem = "the marker file is format " + format + "; this build reads format " + CurrentFormatVersion;
                return null;
            }

            if (string.IsNullOrEmpty(waveId) || string.IsNullOrEmpty(programVersion) || slots.Count == 0)
            {
                problem = "the marker file is missing a required field (wave-id, program-version, or at least one slot)";
                return null;
            }

            if (startedText == null ||
                !DateTimeOffset.TryParse(
                    startedText,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out var startedUtc))
            {
                problem = "the marker file states no readable start time";
                return null;
            }

            var coordinator = CoordinatorIdentity.TryParse(coordinatorToken);
            if (coordinator == null)
            {
                problem = "the marker file states no readable coordinator identity";
                return null;
            }

            try
            {
                return new WaveMarker(waveId!, slots, programVersion!, startedUtc, coordinator);
            }
            catch (ArgumentException ex)
            {
                problem = "the marker file's contents do not form a valid marker: " + ex.Message;
                return null;
            }
        }

        private static void Append(StringBuilder sb, string key, string value)
        {
            sb.Append(key).Append('=').Append(Escape(value)).Append('\n');
        }

        private static string Escape(string value) =>
            value.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string Unescape(string value)
        {
            var sb = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] != '\\' || i + 1 >= value.Length)
                {
                    sb.Append(value[i]);
                    continue;
                }

                i++;
                switch (value[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    default: sb.Append(value[i]); break;
                }
            }

            return sb.ToString();
        }

        private static string Truncate(string value) =>
            value.Length <= 40 ? value : value.Substring(0, 40) + "...";
    }
}
