using System.Text.RegularExpressions;

namespace Converter.RelationReconcile;

// The D2 ledger's disposition vocabulary, and the only question this file answers about it:
// DOES THIS ROW BECOME A D3 TERM?
//
// The authoritative list is the D2 table in `.claude/skills/gen-code-structure/SKILL.md`:
//
//   rendered                 it becomes an explicit term in D3
//   rebind                   D1's binding audit moved it to a stronger signal
//   in-FB                    satisfied inside the reused block
//   discharged               covered by a declared D0 shape
//   render-BLOCKED           would be a term, but a blocking `Q-nn` contests it
//   render-stopped           would be a term; no Q contests it, but the instance's render is stopped
//   out-of-scope-obligation  the counterpart lives outside this run's scope
//
// Only the first two produce a D3 term. The other five each mean "this relation does NOT become a
// term" — and before FI-45 item 3 the reconciler demanded the render leg carry a key set IDENTICAL
// to the ledger's, so a single such row exited 1. `render-BLOCKED` was literally undeclarable in an
// artifact that had to pass its own self-check.
//
// Two deliberate leniencies, both measured against the committed corpus rather than assumed:
//   * `render` is accepted as `rendered` — the SKILL says `rendered`, every real ledger row and the
//     original tests say `render`. Where the SKILL and the artifact disagree the artifact wins
//     (the same precedent RelationArtifactParsers records).
//   * Cells are COMPOUND in practice (`in-FB + render(driver)`, `rebind → render`,
//     `render-BLOCKED [contested]`, `discharged (S5)`). So this classifies by scanning for tokens,
//     and a render-bound token anywhere in the cell wins: a row that renders a driver term must
//     still be held to producing one.
//
// An UNRECOGNIZED disposition is treated as render-bound — fail closed. An invented word must never
// be the cheap way out of the render obligation, and runs do invent them (the SKILL says so, and the
// corpus contains `Satisfied`, `interface-only, driver absent`, `in-FB-DECLARED-UNUSED`). It is
// reported as a warning so the vocabulary gap is visible rather than absorbed.
public enum DispositionClass
{
    RenderBound,   // must appear as a D3 term
    AccountedFor,  // documented, and explicitly NOT a D3 term
    Unrecognized,  // outside the vocabulary — held to the render obligation anyway
}

public sealed record DispositionVerdict(string Label, DispositionClass Class)
{
    // Unrecognized is held to the render obligation, so this is NOT `Class == RenderBound`.
    public bool IsRenderBound => Class != DispositionClass.AccountedFor;
}

public static class RelationDispositions
{
    // `rendered` first so the alternation cannot settle for `render` inside it. The `(?!-)` is what
    // keeps `render-BLOCKED` / `render-stopped` out: `\brender\b` happily matches their first word.
    private static readonly Regex RenderBoundToken =
        new(@"\b(rendered|render|rebind)\b(?!-)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AccountedForToken =
        new(@"\b(in-fb|discharged|render-blocked|render-stopped|out-of-scope-obligation)\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    // Canonical spelling for the report, so `render`/`RENDERED`/`**rebind**` group as one row.
    private static readonly Dictionary<string, string> Display = new(StringComparer.OrdinalIgnoreCase)
    {
        ["render"] = "rendered",
        ["rendered"] = "rendered",
        ["rebind"] = "rebind",
        ["in-fb"] = "in-FB",
        ["discharged"] = "discharged",
        ["render-blocked"] = "render-BLOCKED",
        ["render-stopped"] = "render-stopped",
        ["out-of-scope-obligation"] = "out-of-scope-obligation",
    };

    public static DispositionVerdict Classify(string cell)
    {
        var normalized = Whitespace.Replace(cell.Replace("`", string.Empty).Replace("*", string.Empty), " ").Trim();

        if (normalized.Length == 0)
        {
            return new DispositionVerdict("(blank)", DispositionClass.Unrecognized);
        }

        var rendered = RenderBoundToken.Match(normalized);
        if (rendered.Success)
        {
            return new DispositionVerdict(Canonical(rendered.Groups[1].Value), DispositionClass.RenderBound);
        }

        var accounted = AccountedForToken.Match(normalized);
        if (accounted.Success)
        {
            return new DispositionVerdict(Canonical(accounted.Groups[1].Value), DispositionClass.AccountedFor);
        }

        return new DispositionVerdict(normalized, DispositionClass.Unrecognized);
    }

    private static string Canonical(string token) =>
        Display.TryGetValue(token, out var display) ? display : token.ToLowerInvariant();
}
