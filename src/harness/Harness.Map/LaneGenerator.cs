namespace Harness.Map;

/// <summary>
/// 🔴 <b>ONE SLOT'S TEST SIDE, DECLARED AS DATA — the surface that exists so the slot FC and the stimulus
/// shell stop being typed as IR.</b>
///
/// <para><b>Every field is optional and ABSENT MEANS NOT GENERATED, never a guess.</b> A lane that
/// declares nothing keeps working exactly as it did and is REPORTED as authored — see
/// <see cref="LaneSlotGeneration.NotDeclared"/>, which says so per slot rather than leaving a reader to
/// infer it from an absence.</para>
///
/// <para><b>A PARTIAL declaration is a REFUSAL rather than a partial generation.</b> The slot FC's whole
/// content is its two calls, so a declaration naming the FC and not the block under test describes no
/// block at all; <see cref="LaneGenerator"/> names the missing half rather than filling it in.</para>
/// </summary>
/// <param name="SlotId">Which slot this declares for. Echoed into every refusal, so a reader knows where to look.</param>
/// <param name="SlotFc">The generated slot FC's name and number, or null.</param>
/// <param name="StimulusHead">The block the slot calls FIRST. See <see cref="SlotFcGenerator"/> — the order is not a parameter.</param>
/// <param name="BlockUnderTest">The block the slot calls SECOND, so it observes this scan's commands.</param>
/// <param name="StimHead">
/// The head spec the 18-network shell is derived from, or null. <b>Independent of the three above</b>: a
/// lane may generate its slot FC while its head stays wholly authored, and the report says which.
/// </param>
public sealed record LaneDeclaration(
    string SlotId,
    SlotFcNaming? SlotFc = null,
    SlotCall? StimulusHead = null,
    SlotCall? BlockUnderTest = null,
    StimHeadSpec? StimHead = null)
{
    /// <summary>True when this declaration asks for nothing at all — which is a legitimate lane.</summary>
    public bool Empty => SlotFc is null && StimulusHead is null && BlockUnderTest is null && StimHead is null;
}

/// <summary>
/// 🔴 <b>The stimulus shell, and it is a FRAGMENT — networks, not a block.</b>
///
/// <para><see cref="StimShellGenerator"/> emits networks 1..N of a head and nothing else: no header, no
/// interface, no statics. Everything that makes a head a head — the drives, the disarm lever, the scenario
/// timeline — is authored and stays authored (that generator's own <c>Obligations</c> enumerate it). <b>So
/// this is NOT a deployable object</b>: it carries no <see cref="HarnessObject"/>, it never enters the
/// build stamp, and it is never named in a lane manifest's program set. Recording it as a program object
/// would put a file that cannot be imported into the list a lane deploys from.</para>
///
/// <para><b>Written to a SUBDIRECTORY by every caller that writes it</b>, for the same reason: the loaders
/// that turn an emit directory back into a <c>--program</c> list glob <c>*.ir</c> non-recursively, and a
/// fragment sitting beside the deployables would be picked up as one.</para>
/// </summary>
/// <param name="HeadName">The head this is the shell OF. From the spec; never invented.</param>
/// <param name="SlotId">The slot that declared it.</param>
/// <param name="Shell">Everything the generator produced, including what the author still owes.</param>
public sealed record StimShellFragment(string HeadName, string SlotId, StimShellResult Shell)
{
    /// <summary>
    /// 🔴 <b>Where a fragment is written, relative to whatever emit directory a caller chose — and it is a
    /// SUBDIRECTORY for a reason, not for tidiness.</b>
    ///
    /// <para>Every loader that turns an emit directory back into a <c>--program</c> list globs <c>*.ir</c>
    /// NON-RECURSIVELY (<c>Harness.Batch.BatchCli.ManifestExpand</c> is the one to read). A fragment sitting
    /// beside the deployables would be picked up as a block. It is held here, beside the thing it describes,
    /// so the two callers that write fragments cannot come to disagree about it.</para>
    /// </summary>
    public const string Subdirectory = "stim-shell";

    /// <summary>The shell's own file name — a fragment's name, deliberately not <c>&lt;head&gt;.ir</c>.</summary>
    public string FileName => HeadName + ".networks.ir";

    /// <summary>The companion listing what the shell references and does not create.</summary>
    public string RequirementsFileName => HeadName + ".requires.txt";

    /// <summary>The companion's body. Derived from the shell, so it cannot disagree with the rungs beside it.</summary>
    public string Requirements()
    {
        var text = new System.Text.StringBuilder();
        text.Append($"# {HeadName} — what the generated shell REFERENCES and does not CREATE.\n");
        text.Append("# Derived from the emitted rungs. This file is a checklist, not IR.\n\n");

        text.Append($"UDT MEMBERS ({Shell.RequiredUdtMembers.Count}):\n");
        foreach (var member in Shell.RequiredUdtMembers)
            text.Append("  ").Append(member).Append('\n');

        text.Append($"\nSTATICS ({Shell.RequiredStatics.Count}):\n");
        foreach (var stat in Shell.RequiredStatics)
            text.Append("  ").Append(stat).Append('\n');

        text.Append($"\nOBLIGATIONS ({Shell.Obligations.Count}):\n");
        foreach (var owed in Shell.Obligations)
            text.Append("  - ").Append(owed).Append('\n');

        return text.ToString();
    }
}

/// <summary>What one slot's declaration produced, and what it deliberately did not.</summary>
/// <param name="SlotId">The slot.</param>
/// <param name="SlotFc">The generated slot FC, or null when none was declared.</param>
/// <param name="StimShell">The generated shell fragment, or null when no head spec was declared.</param>
/// <param name="NotDeclared">
/// 🔴 <b>What this slot did NOT declare, in words, one line each.</b> Present so a report can say
/// <i>AUTHORED</i> rather than printing nothing — an absent line reads as a lane with nothing left to
/// generate, which is the opposite of the truth on every lane that exists today.
/// </param>
public sealed record LaneSlotGeneration(
    string SlotId,
    SlotFcResult? SlotFc,
    StimShellFragment? StimShell,
    IReadOnlyList<string> NotDeclared);

/// <summary>
/// Everything <see cref="LaneGenerator"/> produced for one request. <b><see cref="Refusals"/> non-empty
/// means NOTHING may be used from this result</b>, the same rule <see cref="CopyLayerResult"/> follows.
/// </summary>
public sealed record LaneGenerationResult(
    IReadOnlyList<LaneSlotGeneration> Slots,
    IReadOnlyList<string> Obligations,
    IReadOnlyList<string> Refusals)
{
    /// <summary>Nothing declared anywhere. An empty result is legitimate and is not a refusal.</summary>
    public static readonly LaneGenerationResult Nothing =
        new(Array.Empty<LaneSlotGeneration>(), Array.Empty<string>(), Array.Empty<string>());

    public bool Refused => Refusals.Count > 0;

    /// <summary>
    /// 🔴 <b>The DEPLOYABLE generated objects — the slot FCs and nothing else.</b> A shell fragment is
    /// deliberately absent: see <see cref="StimShellFragment"/>.
    /// </summary>
    public IReadOnlyList<HarnessObject> Objects =>
        Slots.Where(s => s.SlotFc is not null)
             .Select(s => new HarnessObject(s.SlotFc!.BlockName, HarnessObjectKind.Block, s.SlotFc.Ir))
             .ToArray();

    /// <summary>The shell fragments, which are NOT objects.</summary>
    public IReadOnlyList<StimShellFragment> Fragments =>
        Slots.Where(s => s.StimShell is not null).Select(s => s.StimShell!).ToArray();

    /// <summary>Every "this was not declared, so it is AUTHORED" line, across every slot.</summary>
    public IReadOnlyList<string> NotDeclared => Slots.SelectMany(s => s.NotDeclared).ToArray();

    /// <summary>
    /// The counts, on the passing path as well as the refusing one. <b>Both halves are printed</b>: a bare
    /// "2 generated" is true of a lane whose other six objects nobody looked at.
    /// </summary>
    public string Summary() =>
        $"{Objects.Count} slot FC(s) and {Fragments.Count} stimulus shell(s) GENERATED across "
        + $"{Slots.Count} declared slot(s); {NotDeclared.Count} object(s) NOT generated because nothing declared them "
        + "— those remain AUTHORED.";
}

/// <summary>
/// 🔴 <b>THE PRODUCTION CALL SITE FOR <see cref="SlotFcGenerator"/> AND <see cref="StimShellGenerator"/>,
/// AND UNTIL THIS EXISTED NEITHER HAD ONE.</b>
///
/// <para>Both generators were reachable only from their own tests. A generator with no production caller
/// removes no work at all: standing a conformance lane up still meant hand-authoring the slot FC and the
/// whole of the head, and the two blocks whose content is <i>entirely</i> derivable from three declared
/// lists were the two being typed.</para>
///
/// <para><b>Contract copied from <see cref="CopyLayerGenerator"/>, unchanged:</b> pure text-out, no
/// filesystem, no Portal, block number required, and <b>every uncertainty a refusal rather than a
/// guess</b>. This class adds one rule of its own, which is the whole reason it is not a loop over two
/// generators: <b>a declaration is honoured WHOLE or refused NAMED</b>. A slot FC generated against half a
/// declaration would be a block whose missing call is invisible in exactly the way the orphan was.</para>
/// </summary>
public static class LaneGenerator
{
    /// <param name="declarations">
    /// One per slot, INCLUDING the slots that declare nothing — pass those as
    /// <see cref="LaneDeclaration"/>s with every field null. <b>Omitting them would make an undeclared slot
    /// indistinguishable from a slot that does not exist</b>, and the report's whole job here is to say
    /// which parts of a lane are still hand-built.
    /// </param>
    public static LaneGenerationResult Generate(IReadOnlyList<LaneDeclaration>? declarations)
    {
        if (declarations is null || declarations.Count == 0)
            return LaneGenerationResult.Nothing;

        var slots = new List<LaneSlotGeneration>();
        var obligations = new List<string>();
        var refusals = new List<string>();

        // Two slots emitting one block name is two files, one import and one survivor. Tracked across the
        // whole request rather than per slot, because that is the only level the collision is visible at.
        var emitted = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var declaration in declarations)
        {
            if (declaration is null)
            {
                refusals.Add("a null lane declaration was supplied. Every slot needs a declaration object, even an empty one — "
                           + "omitting a slot makes 'nothing was declared for it' indistinguishable from 'it does not exist'.");
                continue;
            }

            var notDeclared = new List<string>();
            SlotFcResult? slotFc = null;
            StimShellFragment? shell = null;

            // ---- the slot FC ------------------------------------------------------------------------
            var named = new (string What, bool Present)[]
            {
                ("slotFc (name and number)", declaration.SlotFc is not null),
                ("stimulusHead", declaration.StimulusHead is not null),
                ("blockUnderTest", declaration.BlockUnderTest is not null),
            };

            if (named.All(n => !n.Present))
            {
                notDeclared.Add($"slot '{declaration.SlotId}': no slot FC was declared, so none was generated. "
                              + "The slot FC for this lane is AUTHORED.");
            }
            else if (named.Any(n => !n.Present))
            {
                // 🔴 NAMED, NOT FILLED IN. The FC's entire content is the two calls in order; a declaration
                // missing one of them describes no block, and inventing the missing half is how a generated
                // block comes to call something nobody chose.
                refusals.Add(
                    $"slot '{declaration.SlotId}' declares a slot FC only in part — missing: "
                    + string.Join(", ", named.Where(n => !n.Present).Select(n => n.What))
                    + ". The slot FC's whole content is its two CALLs in order, so a declaration missing one of them "
                    + "describes no block at all. Declare all three, or declare none and author the FC.");
            }
            else
            {
                try
                {
                    slotFc = SlotFcGenerator.Generate(
                        declaration.SlotFc!, declaration.StimulusHead!, declaration.BlockUnderTest!);

                    if (emitted.TryGetValue(slotFc.BlockName, out var owner))
                    {
                        refusals.Add(
                            $"slot '{declaration.SlotId}' and slot '{owner}' both generate a block called '{slotFc.BlockName}'. "
                            + "TIA matches an import by NAME, so the second would replace the first and one lane would run the "
                            + "other's calls — with both files present on disk and nothing saying so.");
                        slotFc = null;
                    }
                    else
                    {
                        emitted[slotFc.BlockName] = declaration.SlotId;

                        // 🔴 THE OBLIGATION TRAVELS WITH THE BLOCK. It is emitted BY the generator precisely
                        // so the omission becomes a thing somebody declined to do rather than a thing nobody
                        // was told about; dropping it here would restore the silence it was built to remove.
                        obligations.Add(slotFc.CallSiteObligation);
                    }
                }
                catch (ArgumentException error)
                {
                    refusals.Add($"slot '{declaration.SlotId}' could not generate its slot FC: {error.Message}");
                }
            }

            // ---- the stimulus shell -----------------------------------------------------------------
            if (declaration.StimHead is null)
            {
                notDeclared.Add($"slot '{declaration.SlotId}': no stimulus head spec was declared, so no shell was "
                              + "generated. The stimulus head for this lane is AUTHORED in full.");
            }
            else
            {
                try
                {
                    var generated = StimShellGenerator.Generate(declaration.StimHead);
                    shell = new StimShellFragment(declaration.StimHead.HeadName, declaration.SlotId, generated);

                    obligations.Add(
                        $"'{shell.HeadName}' IS NOT A COMPLETE BLOCK. What was generated is networks 1..{generated.Networks.Count} "
                        + "— the index shell — and nothing else: no header, no interface, no statics, and none of the drives, the "
                        + "disarm lever or the scenario timeline. Append your head-specific networks from "
                        + $"{generated.Networks.Count + 1} onward, NEVER inserted among these, and create the "
                        + $"{generated.RequiredUdtMembers.Count} UDT member(s) and {generated.RequiredStatics.Count} static(s) the "
                        + $"shell references. They are listed in {shell.RequirementsFileName}.");

                    foreach (var owed in generated.Obligations)
                        obligations.Add($"{shell.HeadName}: {owed}");
                }
                catch (ArgumentException error)
                {
                    refusals.Add($"slot '{declaration.SlotId}' could not generate its stimulus shell: {error.Message}");
                }
            }

            slots.Add(new LaneSlotGeneration(declaration.SlotId, slotFc, shell, notDeclared));
        }

        // 🔴 A REFUSED REQUEST YIELDS NOTHING USABLE, INCLUDING THE PARTS THAT WORKED. Handing back the two
        // objects that generated beside a refusal about the third invites a caller to deploy two thirds of
        // a lane — and a lane missing its slot FC is the orphan, which is the failure this whole file
        // exists downstream of.
        return refusals.Count > 0
            ? new LaneGenerationResult(Array.Empty<LaneSlotGeneration>(), Array.Empty<string>(), refusals)
            : new LaneGenerationResult(slots, obligations, refusals);
    }
}
