using System.Reflection;
using System.Reflection.Emit;
using Converter.ConflictGraph;
using Converter.CrossCheck;
using Xunit;

namespace Converter.Tests;

/// <summary>What one walk over an assembly found, <b>and how much of it it looked at</b>.</summary>
/// <param name="BodiesExamined">
/// 🔴 <b>THE DENOMINATOR.</b> An empty hit set is true of <i>nothing found</i> and of <i>nothing looked
/// at</i>, and only this number separates them. Every assertion built on this walk asserts it.
/// </param>
/// <param name="Undecodable">
/// 🔴 <b>Method bodies the decoder could not read to the end, with the reason.</b>
///
/// <para><b>Reported, never skipped.</b> A body abandoned mid-walk is a slice of the assembly this scan
/// did NOT examine, and quietly dropping it is precisely the false negative that the old
/// slide-a-window-over-the-bytes approach was chosen to avoid. The gain from decoding properly is that
/// there are no spurious hits; it may not be paid for with silent gaps, so the caller asserts this is
/// empty.</para>
/// </param>
public sealed record IlScan(
    IReadOnlyList<string> Hits,
    IReadOnlyList<string> HitTypes,
    int TypesEnumerated,
    int MethodsEnumerated,
    int BodiesExamined,
    IReadOnlyList<string> Undecodable);

/// <summary>
/// 🔴 <b>THE CHECK THAT MAKES A SHARED RESOLVER MORE THAN A GOOD INTENTION.</b>
///
/// <para><b>Five times</b> in this codebase a join between "the name a document cites" and "the location
/// the program uses" has failed, and each was fixed at its own call site: a slot id, a vector-target
/// prefix, an observable vocabulary, a completion signal, and <c>SlotBinding.ResultRegisterOf</c>
/// matching on the tag while the runner passed the cited name. <b>The sixth was <c>conflict-graph
/// --submission</c> — a tool built AFTER the seam was diagnosed, still carrying the assumption.</b></para>
///
/// <para>*** A SHARED HELPER IS NECESSARY AND NOT SUFFICIENT, AND THE PROOF IS IN THIS REPOSITORY. ***
/// <c>Harness.Map.MirroredSignal.JoinKey</c> carries the doc comment <i>"one definition, used by every
/// path that joins the two documents"</i> — <b>and the observe path was not one of them.</b> A comment
/// claiming universality is documentation wearing a check's clothes. So: one resolver, plus a walk over
/// the compiled assembly that goes RED when a type outside the declared set reaches
/// <see cref="StorageGroup"/>.</para>
///
/// <para><b>What this does NOT cover, said where a reader of results meets it.</b> It is keyed on
/// <see cref="StorageGroup"/> — the corrected storage grouping every storage-identity join must go
/// through. A join built directly on <c>ProjectUsageGraph.Usages</c> would not be caught, and neither
/// would one written in another assembly. It is a fence around one gate, not around the field.</para>
///
/// <para>The scanner is a deliberate copy of <c>Harness.MirrorView.Tests.IlWalk</c> — the converter
/// solution cannot reference the harness one, and two independently-rotting copies is still better than
/// one structural claim with no walk behind it. Its design notes live there.</para>
/// </summary>
public class JoinSiteWalkTests
{
    /// <summary>
    /// 🔴 <b>THE ALLOWLIST, AND EVERY ENTRY IS A DECISION.</b> Adding a name here is how a reviewer
    /// finds out a sixth join site was written — which is the entire point of the check. Removing the
    /// resolver from it would make the test pass while the seam re-opened, so the resolver's PRESENCE is
    /// asserted separately below.
    /// </summary>
    private static readonly HashSet<string> DeclaredJoinSites = new(StringComparer.Ordinal)
    {
        // The grouping itself, and its own record type.
        "Converter.CrossCheck.StorageGroups",
        "Converter.CrossCheck.StorageGroup",

        // Consumes the grouping WHOLESALE — it emits a fact table keyed on storage path and never joins
        // a cited NAME to a location, so it is not a join site.
        "Converter.CrossCheck.CrossCheckRunner",

        // *** THE ONE JOIN SITE. ***
        "Converter.ConflictGraph.SignalStorageResolver",
    };

    private static Assembly ConverterAssembly => typeof(StorageGroup).Assembly;

    private static bool TouchesStorageGroup(MemberInfo member) =>
        IlWalkScanner.DeclaringName(member) is "Converter.CrossCheck.StorageGroup" or "Converter.CrossCheck.StorageGroups";

    // ---- THE GUARD --------------------------------------------------------------------------------

    // *** ADD A SECOND PLACE THAT RESOLVES A NAME TO STORAGE AND THIS GOES RED. *** It is keyed on types
    // rather than methods so that ordinary refactoring inside the resolver costs nothing, while a NEW
    // type reaching the grouping cannot land unnoticed.
    [Fact]
    public void NoTypeOutsideTheDeclaredSetReachesTheStorageGrouping()
    {
        var scan = IlWalkScanner.Scan(ConverterAssembly, TouchesStorageGroup);

        // THE DENOMINATOR, asserted before the finding: zero hits over zero bodies is not a pass.
        Assert.True(scan.BodiesExamined > 0, "the walk examined no method bodies at all, so it can claim nothing");

        // *** AND THE SECOND HALF OF THE DENOMINATOR, added when the walk stopped sliding a window over
        // the bytes and started decoding them. *** Precise decoding buys no false positives; it may not be
        // paid for with silent gaps, so a body abandoned part-way through is a finding in its own right.
        // Without this, one unrecognised opcode would quietly hide every join site after it.
        Assert.True(
            scan.Undecodable.Count == 0,
            $"{scan.Undecodable.Count} method body(ies) could not be decoded to the end, so the walk did not examine them: "
            + string.Join(" | ", scan.Undecodable));

        var undeclared = scan.HitTypes
            .Where(t => !DeclaredJoinSites.Contains(StripCompilerSuffix(t)))
            .OrderBy(t => t, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            undeclared.Count == 0,
            "*** A SIXTH NAME-TO-STORAGE JOIN SITE. *** These types reach StorageGroup and are not in the declared set: "
            + string.Join(", ", undeclared)
            + ". Route the join through SignalStorageResolver, or add the type here WITH A REASON — the join between a "
            + "document's vocabulary and the program's storage has failed five times in this codebase, once per call site.");
    }

    // *** AND THE RESOLVER IS ACTUALLY IN THE HIT SET. *** Without this, deleting the resolver's use of
    // the grouping — or the walk quietly failing to see it — leaves the test above green over an empty
    // finding. A positive control and a denominator catch different failures; neither subsumes the other.
    [Fact]
    public void TheWalkSeesTheResolverItself()
    {
        var scan = IlWalkScanner.Scan(ConverterAssembly, TouchesStorageGroup);

        Assert.Contains(
            "Converter.ConflictGraph.SignalStorageResolver",
            scan.HitTypes.Select(StripCompilerSuffix));
    }

    // The NEGATIVE CONTROL, and the predicate is a PARAMETER rather than a literal inside the walk — a
    // check whose exercise requires editing the check will not be exercised. Retargeted at a type used
    // all over the assembly, the same walk must find offenders; if it finds none, the walk is blind and
    // every zero above means nothing.
    [Fact]
    public void TheWalkDetects_RetargetedAtATypeThatIsUsedEverywhere()
    {
        var scan = IlWalkScanner.Scan(
            ConverterAssembly,
            m => IlWalkScanner.DeclaringName(m) == "Converter.CrossCheck.ProjectUsageGraph");

        Assert.True(scan.BodiesExamined > 0);
        Assert.NotEmpty(scan.Hits);
        Assert.Contains("Converter.ConflictGraph.ConflictGraphRunner", scan.HitTypes.Select(StripCompilerSuffix));
    }

    // The runner is where the defect lived, and it must no longer touch the grouping AT ALL: it consumes
    // the resolver's answer. This is the fix stated as a structural fact rather than as a diff.
    [Fact]
    public void TheConflictGraphRunnerNoLongerReachesTheGroupingDirectly()
    {
        var scan = IlWalkScanner.Scan(ConverterAssembly, TouchesStorageGroup);

        Assert.DoesNotContain(
            "Converter.ConflictGraph.ConflictGraphRunner",
            scan.HitTypes.Select(StripCompilerSuffix));
    }

    // Compiler-generated closure/iterator types are nested inside their owner (`Outer+<>c`), and they
    // are the owner's code. Attributing them anywhere else would either hide a real join site or
    // manufacture a false one.
    private static string StripCompilerSuffix(string typeName)
    {
        var nested = typeName.IndexOf('+');
        return nested < 0 ? typeName : typeName[..nested];
    }
}

/// <summary>
/// The walk. A deliberate copy of <c>Harness.MirrorView.Tests.IlWalk</c>; see
/// <see cref="JoinSiteWalkTests"/> for why it is copied rather than referenced.
/// </summary>
public static class IlWalkScanner
{
    public static IlScan Scan(Assembly assembly, Func<MemberInfo, bool> forbidden)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(forbidden);

        var hits = new List<string>();
        var undecodable = new List<string>();
        var hitTypes = new List<string>();
        var types = 0;
        var methods = 0;
        var bodies = 0;

        Type[] loadable;
        try
        {
            loadable = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // Reported, never absorbed: a type that would not load is a slice of the assembly this walk
            // did NOT examine, and a clean sweep over the remainder would be a green with an unstated
            // denominator.
            Assert.Fail($"{ex.LoaderExceptions.Length} type(s) in {assembly.GetName().Name} could not be loaded, "
                        + "so the walk cannot claim to have examined the assembly.");
            return new IlScan(hits, hitTypes, 0, 0, 0, undecodable);
        }

        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic
                                 | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var type in loadable)
        {
            types++;

            // Constructors are walked too. A join written in a constructor is still a join, and the
            // scanner this was copied from omitted them.
            foreach (MethodBase method in type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All)))
            {
                methods++;

                byte[]? il;
                try
                {
                    il = method.GetMethodBody()?.GetILAsByteArray();
                }
                catch (Exception)
                {
                    continue; // abstract, extern and generated members have no readable body
                }

                if (il is null)
                {
                    continue;
                }

                bodies++;
                if (References(il, method, forbidden, out var detail, out var couldNotDecode))
                {
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
                    if (type.FullName is { } name && !hitTypes.Contains(name, StringComparer.Ordinal))
                    {
                        hitTypes.Add(name);
                    }
                }

                if (couldNotDecode is not null)
                {
                    undecodable.Add($"{type.FullName}.{method.Name}: {couldNotDecode}");
                }
            }
        }

        return new IlScan(hits, hitTypes, types, methods, bodies, undecodable);
    }

    /// <summary>The declaring type's full name, or the type's own when the member IS a type.</summary>
    public static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    /// <summary>
    /// 🔴 <b>THE IL IS DECODED, NOT SLID OVER — and it used to be slid over, which cost a red build for
    /// a join that does not exist.</b>
    ///
    /// <para><b>What this replaces.</b> Every 4-byte window was offered to the token resolver, on the
    /// argument that it <i>"OVER-reports candidate tokens and UNDER-reports nothing"</i>, so a false
    /// positive would merely fail a test and get read. That is sound reasoning about a guard nobody
    /// perturbs — and unsound in practice: <b>a random four bytes inside an unrelated method can resolve
    /// to a real member, and which four bytes do that CHANGES whenever the assembly does.</b> Measured
    /// 2026-08-22: adding an unrelated feature made this test name two types as join sites, neither of
    /// which mentions the guarded type anywhere in its source.</para>
    ///
    /// <para><b>Why that is worse than a nuisance.</b> The pressure a spurious red creates is to widen
    /// the allowlist, and the allowlist is the guard. An entry added for a coincidence would then mask a
    /// genuine join site written in that type later — the check would still be green and would no longer
    /// be checking. A guard that cries wolf gets disarmed.</para>
    ///
    /// <para><b>The no-false-negative property is kept, and strengthened.</b> Only real operand tokens
    /// are resolved, so nothing that IS a reference is missed — and an opcode this decoder does not
    /// recognise is <b>reported rather than skipped</b> (see <see cref="IlScan.Undecodable"/>), because
    /// silently abandoning a method body is exactly the false negative the coarse walk was protecting
    /// against.</para>
    /// </summary>
    private static bool References(byte[] il, MethodBase owner, Func<MemberInfo, bool> forbidden, out string detail, out string? undecodable)
    {
        detail = string.Empty;
        undecodable = null;

        var module = owner.Module;
        var typeArgs = SafeGenericArguments(owner.DeclaringType);
        var methodArgs = owner is MethodInfo { IsGenericMethodDefinition: true } m ? m.GetGenericArguments() : Type.EmptyTypes;

        var i = 0;

        while (i < il.Length)
        {
            OpCode op;

            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length)
                {
                    undecodable = $"a two-byte opcode prefix at offset {i} with nothing after it";
                    return false;
                }

                if (!TwoByteKnown[il[i + 1]])
                {
                    undecodable = $"unknown two-byte opcode 0xFE{il[i + 1]:X2} at offset {i}";
                    return false;
                }

                op = TwoByte[il[i + 1]];
                i += 2;
            }
            else
            {
                if (!OneByteKnown[il[i]])
                {
                    undecodable = $"unknown opcode 0x{il[i]:X2} at offset {i}";
                    return false;
                }

                op = OneByte[il[i]];
                i += 1;
            }

            // Only these operand kinds ARE metadata tokens for a member. InlineString and InlineSig are
            // tokens too, but resolve to a string literal and a standalone signature — offering them to
            // ResolveMember would only manufacture noise of the kind this rewrite exists to remove.
            var isMemberToken = op.OperandType is OperandType.InlineField
                or OperandType.InlineMethod or OperandType.InlineTok or OperandType.InlineType;

            if (isMemberToken)
            {
                if (i + 4 > il.Length)
                {
                    undecodable = $"a token operand at offset {i} runs off the end of the body";
                    return false;
                }

                try
                {
                    var member = module.ResolveMember(BitConverter.ToInt32(il, i), typeArgs, methodArgs);
                    if (member is not null && forbidden(member))
                    {
                        detail = $"references {DeclaringName(member)}.{member.Name}";
                        return true;
                    }
                }
                catch (Exception)
                {
                    // A token in a genuine operand slot that will not resolve — a generic parameter this
                    // context cannot close, most often. Not a hit, and not a decode failure either.
                }
            }

            var operandSize = SizeOf(op.OperandType, il, i, out var switchOverflow);

            if (switchOverflow)
            {
                undecodable = $"a switch table at offset {i} runs off the end of the body";
                return false;
            }

            i += operandSize;
        }

        return false;
    }

    /// <summary>Operand width in bytes. <c>InlineSwitch</c> is variable and reads its own count.</summary>
    private static int SizeOf(OperandType operand, byte[] il, int at, out bool overflow)
    {
        overflow = false;

        switch (operand)
        {
            case OperandType.InlineNone:
                return 0;

            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;

            case OperandType.InlineVar:
                return 2;

            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;

            case OperandType.InlineSwitch:
                if (at + 4 > il.Length)
                {
                    overflow = true;
                    return 0;
                }

                var count = BitConverter.ToInt32(il, at);
                if (count < 0 || at + 4 + (4L * count) > il.Length)
                {
                    overflow = true;
                    return 0;
                }

                return 4 + (4 * count);

            default:
                // InlineBrTarget, InlineField, InlineI, InlineMethod, InlineSig, InlineString,
                // InlineTok, InlineType, ShortInlineR — all four bytes.
                return 4;
        }
    }

    private static readonly OpCode[] OneByte = new OpCode[0x100];
    private static readonly OpCode[] TwoByte = new OpCode[0x100];
    private static readonly bool[] OneByteKnown = new bool[0x100];
    private static readonly bool[] TwoByteKnown = new bool[0x100];

    static IlWalkScanner()
    {
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op)
            {
                continue;
            }

            var low = (byte)(op.Value & 0xFF);

            if (op.Size == 1)
            {
                OneByte[low] = op;
                OneByteKnown[low] = true;
            }
            else
            {
                TwoByte[low] = op;
                TwoByteKnown[low] = true;
            }
        }
    }

    private static Type[] SafeGenericArguments(Type? type)
    {
        try
        {
            return type is { IsGenericTypeDefinition: true } ? type.GetGenericArguments() : Type.EmptyTypes;
        }
        catch (Exception)
        {
            return Type.EmptyTypes;
        }
    }
}
