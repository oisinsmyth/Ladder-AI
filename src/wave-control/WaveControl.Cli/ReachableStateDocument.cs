using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Ladder.Wave.Cli
{
    /// <summary>
    /// Reads one block's closure out of a `converter reachable-state --json` document.
    ///
    /// <para>🔴 <b>THIS IS THE SEAM THAT MAKES D9's RULE A RULE.</b> `--reaches` / `--reaches-from`
    /// let the submitting agent STATE its own closure — and *ask of any rule: who computes its inputs?
    /// If the answer is "the party the rule constrains", it is not a rule.* This reads the same two
    /// values out of a document a TOOL produced, from the IR, with a corpus stamp attached.</para>
    ///
    /// <para>*** IT DOES NOT MAKE FORGERY IMPOSSIBLE AND MUST NOT BE READ AS DOING SO. *** A
    /// declaration is a transferred responsibility, not a verification: `wave-cli` cannot run the
    /// converter and cannot re-derive the closure, so it can demand that the answer come from a
    /// document, name the corpus it was computed against, and refuse an absence — it cannot make a
    /// false document true. What it removes is the case that actually happens: an agent typing the
    /// closure it believes it has.</para>
    ///
    /// <para><b>ABSENT IS NOT EMPTY, AND THAT IS WHAT THIS READER IS MOSTLY FOR.</b> The producer omits
    /// <c>reachableState</c> and <c>provenance</c> for a block whose closure it could not compute. A
    /// reader that treated a missing key as <c>[]</c> would hand admission a slot that looks
    /// independent of everything — which is exactly the refusal
    /// <c>ColouringDefect.ReachableStateNotComputed</c> exists to raise. Every absence here is a
    /// REFUSAL WITH A REASON, never a default.</para>
    /// </summary>
    public static class ReachableStateDocument
    {
        /// <summary>What a read produced, or why it produced nothing.</summary>
        public sealed class Result
        {
            internal Result(bool ok, IReadOnlyList<string> reachableState, string provenance, string refusal)
            {
                Ok = ok;
                ReachableState = reachableState;
                Provenance = provenance;
                Refusal = refusal;
            }

            /// <summary>TRUE when a computed closure was found for the named block.</summary>
            public bool Ok { get; }

            /// <summary>The closure. Meaningful only when <see cref="Ok"/>.</summary>
            public IReadOnlyList<string> ReachableState { get; }

            /// <summary>Where it came from. Meaningful only when <see cref="Ok"/>.</summary>
            public string Provenance { get; }

            /// <summary>Why not, when <see cref="Ok"/> is false. Always names the file and the block.</summary>
            public string Refusal { get; }
        }

        /// <summary>
        /// Reads <paramref name="block"/>'s closure from <paramref name="path"/>.
        /// </summary>
        public static Result Read(string path, string block)
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return Refuse("could not read '" + path + "': " + ex.GetType().Name + ": " + ex.Message);
            }

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(text);
            }
            catch (JsonException ex)
            {
                return Refuse("'" + path + "' is not readable JSON: " + ex.Message);
            }

            using (doc)
            {
                // The producer's whole-report refusal. It writes `notComputed` INSTEAD OF `blocks`,
                // never alongside it, so an absent `blocks` key is the refusal arriving intact.
                if (!doc.RootElement.TryGetProperty("blocks", out var blocks)
                    || blocks.ValueKind != JsonValueKind.Array)
                {
                    var why = doc.RootElement.TryGetProperty("notComputed", out var reason)
                        ? reason.GetString()
                        : "the document carries no `blocks` key and no `notComputed` reason either, so it is "
                          + "not a reachable-state document at all.";

                    return Refuse(
                        "'" + path + "' contains no computed closures. The producer withheld the whole report: " + why);
                }

                var entries = blocks.EnumerateArray()
                    .Where(b => b.TryGetProperty("block", out var name)
                                && string.Equals(name.GetString(), block, StringComparison.Ordinal))
                    .ToList();

                if (entries.Count == 0)
                {
                    return Refuse(
                        "'" + path + "' has no entry for block '" + block + "'. It was not among the "
                        + blocks.GetArrayLength() + " block(s) the producer examined — which is a different fact from "
                        + "its closure being empty, and only one of them permits a submission.");
                }

                if (entries.Count > 1)
                {
                    // Two entries for one block cannot both be authoritative, and picking one is the
                    // aliasing shape this project has already paid for. Refuse rather than choose.
                    return Refuse(
                        "'" + path + "' has " + entries.Count + " entries for block '" + block
                        + "'. Resolving that by picking one would be choosing which closure to believe.");
                }

                var entry = entries[0];

                // *** THE LOAD-BEARING CHECK. *** A withheld closure omits BOTH keys. Defaulting either
                // one silently converts admission's refusal into a pass.
                if (!entry.TryGetProperty("reachableState", out var state)
                    || state.ValueKind != JsonValueKind.Array
                    || !entry.TryGetProperty("provenance", out var provenance)
                    || provenance.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(provenance.GetString()))
                {
                    var why = entry.TryGetProperty("notComputed", out var reason)
                        ? reason.GetString()
                        : "no reason was given.";

                    return Refuse(
                        "'" + path + "' WITHHELD the closure for block '" + block + "': " + why
                        + " A withheld closure and an empty one arrive as the same empty set and call for opposite "
                        + "actions, so this is a refusal rather than a submission with no reachable state.");
                }

                var values = state.EnumerateArray()
                    .Where(v => v.ValueKind == JsonValueKind.String)
                    .Select(v => v.GetString() ?? string.Empty)
                    .Where(v => v.Length > 0)
                    .ToArray();

                return new Result(true, values, provenance.GetString() ?? string.Empty, string.Empty);
            }
        }

        private static Result Refuse(string reason) =>
            new Result(false, Array.Empty<string>(), string.Empty, reason);
    }
}
