using Converter.SimaticMl;

namespace Converter.Ir;

/// <summary>What an instance DB's Static member does with the <c>Remanence</c> attribute.</summary>
public enum RemanenceRuling
{
    /// <summary>Write <c>Remanence</c>, exactly as before. Elementary, array, UDT-typed, IEC instance.</summary>
    Emit,

    /// <summary>Omit it. The member is an INSTANCE of something else, and TIA refuses the attribute there.</summary>
    Omit,

    /// <summary>
    /// The member's declared type resolves to nothing this corpus knows, so <b>neither answer can be
    /// justified</b> — and the two are not interchangeable (see <see cref="InstanceDbTypeResolution"/>).
    /// The caller refuses.
    /// </summary>
    Unclassifiable,
}

/// <summary>One ruling, plus the sentence a refusal prints.</summary>
public readonly record struct MemberRemanence(RemanenceRuling Ruling, string? Reason);

/// <summary>
/// <b>Whether an instance DB's Static member may carry <c>Remanence</c> — the DB-side half of the rule
/// <see cref="BlockSourceWriter"/> already applies to a block (FI-102, 2026-09-03).</b>
///
/// <para><b>THE DEFECT.</b> TIA refuses an instance DB whose Static section states <c>Remanence</c> on a
/// MULTI-INSTANCE member:
/// <code>
/// Cannot create the 'SW.Blocks.InstanceDB' object with Simatic ML ID '0' …
/// &lt;name&gt;.&lt;member&gt;: The Openness import failed: The attribute 'Remanence' cannot be set.
/// </code>
/// Measured over five blocks: an FB declaring ZERO FB-typed statics produced an instance DB that
/// imported cleanly; the two declaring TWO and FIVE were both refused. Retention belongs to the CALLED
/// block's own members, so there is nothing for the owner to state.</para>
///
/// <para><b>WHY <see cref="BlockSourceWriter"/>'S GUARD DOES NOT REACH HERE.</b> That writer derives its
/// multi-instance name set from the block's own CALL and fixed-shape instruction statements — facts the
/// file contains. <b>An instance DB has no statements at all</b>, so there is nothing to derive from and
/// <see cref="DbSourceWriter"/> emitted the attribute unguarded.</para>
///
/// <para>🔴 <b>THE DATATYPE STRING CANNOT DECIDE IT, AND NEITHER CAN THE NAME.</b> <c>IO :
/// "UDT_Something" RETAIN</c> and <c>Inner : "FB_Something"</c> are both quoted names; the first is a
/// PLC data type that legitimately carries <c>Remanence</c> and the second is an instance that must
/// not. An <c>FB_</c> prefix test is not a discriminator either — this repo already rules that out for
/// reachability, for the reason that holds here too: anything can be renamed into a prefix. What
/// separates them is <b>which namespace the name resolves in</b>, which is a fact about the corpus and
/// therefore about <c>--project</c>.</para>
///
/// <para><b>SO THE DISCRIMINATOR IS <see cref="MemberExpansion.Classify"/>, NOT A SECOND COPY OF IT.</b>
/// That classifier already makes exactly this distinction — <see cref="MemberShape.MultiInstance"/> for
/// a datatype that resolves to a BLOCK NAME, <see cref="MemberShape.NamedTypeOpened"/> for one that
/// resolves to a PLC DATA TYPE — with block names read off each <c>.ir</c>'s <c>BLOCK &lt;KIND&gt;
/// &lt;Name&gt;</c> header rather than from <c>TagTypeRegistry</c>'s FB index, which silently drops every
/// sidecar-carrying (i.e. re-exported) FB. A re-exported FB is precisely the file a round-tripped corpus
/// contains, so keying this on that index would classify a real multi-instance as unresolvable. See
/// <see cref="MemberExpansion.BlockNamesFromHeaders"/>.</para>
///
/// <para>🔴 <b>AND THE THIRD ANSWER IS A REFUSAL, NOT A DEFAULT.</b> When the declared type resolves to
/// neither a block nor a type in the corpus — which is what <b>converting with no <c>--project</c> and
/// no sibling file</b> looks like — there is no safe fallback:
/// <list type="bullet">
/// <item>emitting <c>Remanence</c> reproduces FI-102 exactly, and the failure surfaces only at IMPORT,
/// after a whole convert/preflight/review cycle has passed;</item>
/// <item>omitting it always would strip a legitimate attribute from every UDT-typed member, changing
/// retention silently, on a document that imports and compiles.</item>
/// </list>
/// One is a loud failure with a named cause and one is a quiet wrong answer, so this refuses.
/// <c>to-xml</c> already refuses rather than emitting on the same reasoning (FI-71's
/// <c>--allow-blind-types</c> gate: "this file is destined for import"), and this is the same
/// direction — a converter that cannot type a member does not guess.</para>
/// </summary>
public sealed class InstanceDbTypeResolution
{
    private readonly IReadOnlySet<string> _blockNames;
    private readonly TagTypeRegistry _types;
    private readonly string _searchScope;

    private InstanceDbTypeResolution(IReadOnlySet<string> blockNames, TagTypeRegistry types, string searchScope)
    {
        _blockNames = blockNames;
        _types = types;
        _searchScope = searchScope;
    }

    /// <summary>
    /// Build from a set of <c>.ir</c> paths — the command's own batch plus whatever <c>--project</c>
    /// named. <paramref name="searchScope"/> is the human-readable description of WHERE names were
    /// looked for, and a refusal quotes it: "one directory down" and "does not exist anywhere" need
    /// different actions from whoever reads the message.
    /// </summary>
    public static InstanceDbTypeResolution FromCorpus(
        IEnumerable<string> irPaths, TagTypeRegistry types, string searchScope) =>
        new(MemberExpansion.BlockNamesFromHeaders(irPaths), types, searchScope);

    /// <summary>The size of the block-name namespace this resolution searched. A denominator, for tests and callers.</summary>
    public int BlockNameCount => _blockNames.Count;

    /// <summary>Where names were looked for, as a refusal renders it.</summary>
    public string SearchScope => _searchScope;

    /// <summary>Rule on one TOP-LEVEL Static member of an instance DB. Nested members never carry
    /// <c>Remanence</c> in any shape this writer emits, so they are not asked about.</summary>
    public MemberRemanence RuleOn(DbMember member)
    {
        var element = MemberExpansion.ElementTypeOf(member.Datatype);

        // A FIXED-SHAPE INSTRUCTION INSTANCE, ahead of everything else and WITHOUT consulting the
        // corpus. `MbServer : MB_SERVER VERSION 5.3` is an instance in exactly the sense that matters
        // here, and BlockSourceWriter already omits Remanence for it in the OWNING FB — derived from
        // the same registry, so the FB and its own instance DB cannot disagree about one member. It is
        // checked FIRST, and on the raw element type rather than on a classification, because a member
        // TIA re-exported with its interface expanded inline would otherwise read as an ordinary
        // structured member and take the Emit branch.
        //
        // IEC TIMERS AND COUNTERS ARE DELIBERATELY NOT IN THIS SET AND MUST NOT BE. `FaultTripTimer :
        // TON_TIME VERSION 1.0 SETPOINT` carries `Remanence` in every real TIA export in the committed
        // corpus, and `HrTotaliserTimer : TONR_TIME … RETAIN` carries `Retain` — a retentive hours-run
        // totaliser is the whole point of the member. They reach the Emit branch below through
        // MemberShape.IecInstance, which is why the two families are separated rather than merged into
        // one "instruction state" rule.
        if (element.Length > 0 && FixedShapeInstructions.IsFixedShapePartName(element))
        {
            return new MemberRemanence(RemanenceRuling.Omit, null);
        }

        var classification = MemberExpansion.Classify(
            member, _types, _blockNames, depth: 0, _searchScope);

        return classification.Shape switch
        {
            // THE FIX. The datatype resolves to a block in this corpus, so the member is an FB
            // instantiated as a static of another FB — a multi-instance, and TIA refuses Remanence on it.
            MemberShape.MultiInstance => new MemberRemanence(RemanenceRuling.Omit, null),

            MemberShape.Opaque => new MemberRemanence(
                RemanenceRuling.Unclassifiable,
                $"declares type '{element}', which resolves to NEITHER a block nor a PLC data type in "
                + $"{_searchScope}. TIA REFUSES 'Remanence' on a multi-instance static and REQUIRES it on a "
                + "UDT-typed one, so this member cannot be emitted without knowing which it is — and the "
                + "name is not a discriminator (anything can be renamed into an 'FB_' prefix). Re-run with "
                + "--project <ir-dir> pointing at the corpus that declares this type, rather than emitting "
                + "a document whose failure would surface only at import."),

            // Everything else keeps today's shape exactly: inlined structures, UDT-typed members opened
            // through the registry, arrays, IEC timer/counter instances, and elementary members.
            _ => new MemberRemanence(RemanenceRuling.Emit, null),
        };
    }
}
