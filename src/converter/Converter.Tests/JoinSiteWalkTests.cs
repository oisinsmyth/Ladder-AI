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
public sealed record IlScan(
    IReadOnlyList<string> Hits,
    IReadOnlyList<string> HitTypes,
    int TypesEnumerated,
    int MethodsEnumerated,
    int BodiesExamined);

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
            return new IlScan(hits, hitTypes, 0, 0, 0);
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
                if (References(il, method, forbidden, out var detail))
                {
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
                    if (type.FullName is { } name && !hitTypes.Contains(name, StringComparer.Ordinal))
                    {
                        hitTypes.Add(name);
                    }
                }
            }
        }

        return new IlScan(hits, hitTypes, types, methods, bodies);
    }

    /// <summary>The declaring type's full name, or the type's own when the member IS a type.</summary>
    public static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    /// <summary>
    /// 🔴 <b>DECODES THE IL. It used to slide a 4-byte window over every byte offset</b> and offer each
    /// to the token resolver, on the stated reasoning that over-reporting is the safe direction for an
    /// assertion that a set is EMPTY — <i>"a false positive fails a test and gets read."</i>
    ///
    /// <para><b>That reasoning was sound and the consequence was not.</b> A misaligned window inside an
    /// unrelated instruction stream can resolve to a real member token by coincidence, so the guard
    /// passed or failed on <b>metadata token layout</b> — i.e. on luck, changing with any edit anywhere
    /// in the assembly. Measured 2026-08-21: an edit confined to <c>Converter.Diff</c> turned the walk
    /// red naming <c>Converter.Digest.NetworkSignature</c>, a type whose source contains no reference to
    /// <see cref="StorageGroup"/> at all. Read, as the design intended — and what reading it found was
    /// the scanner, not a sixth join site.</para>
    ///
    /// <para><b>A guard that flips on unrelated edits gets its allowlist grown to quiet it</b>, and each
    /// entry is a permanent hole in exactly the check this file exists to be. So: walk the instruction
    /// stream, resolve ONLY the operands of token-carrying opcodes. Precise in both directions — it
    /// cannot invent a reference, and it still cannot miss one, because every token an instruction can
    /// carry is now visited deliberately rather than stumbled upon.</para>
    /// </summary>
    private static bool References(byte[] il, MethodBase owner, Func<MemberInfo, bool> forbidden, out string detail)
    {
        detail = string.Empty;
        var module = owner.Module;
        var typeArgs = SafeGenericArguments(owner.DeclaringType);
        var methodArgs = owner is MethodInfo { IsGenericMethodDefinition: true } m ? m.GetGenericArguments() : Type.EmptyTypes;

        foreach (var token in TokenOperands(il))
        {
            MemberInfo? member;
            try
            {
                member = module.ResolveMember(token, typeArgs, methodArgs);
            }
            catch (Exception)
            {
                // A token this module cannot resolve in this context — a generic-context edge case, not
                // a random byte window any more. Skipped, not counted as a reference.
                continue;
            }

            if (member is not null && forbidden(member))
            {
                detail = $"references {DeclaringName(member)}.{member.Name}";
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Every metadata token carried as an operand, in instruction order. Throws rather than guessing if
    /// the stream does not decode — a body this walk could not read is a body it must not silently
    /// claim to have examined (the denominator discipline, applied one level down).
    /// </summary>
    private static IEnumerable<int> TokenOperands(byte[] il)
    {
        var tokens = new List<int>();

        for (var i = 0; i < il.Length;)
        {
            OpCode op;
            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length)
                {
                    throw new InvalidOperationException("IL ends mid-opcode (0xFE prefix with no second byte)");
                }

                op = TwoByte.TryGetValue(il[i + 1], out var two)
                    ? two
                    : throw new InvalidOperationException($"unknown two-byte opcode 0xFE{il[i + 1]:X2}");
                i += 2;
            }
            else
            {
                op = OneByte.TryGetValue(il[i], out var one)
                    ? one
                    : throw new InvalidOperationException($"unknown opcode 0x{il[i]:X2} at offset {i}");
                i += 1;
            }

            // InlineSwitch is the only variable-length operand: a 4-byte count then that many 4-byte
            // branch targets. Get it wrong and every subsequent instruction decodes as garbage.
            if (op.OperandType == OperandType.InlineSwitch)
            {
                var count = BitConverter.ToUInt32(il, i);
                i += 4 + ((int)count * 4);
                continue;
            }

            var size = OperandSize(op.OperandType);
            if (IsTokenOperand(op.OperandType))
            {
                tokens.Add(BitConverter.ToInt32(il, i));
            }

            i += size;
        }

        return tokens;
    }

    // InlineString and InlineSig are tokens too, but never resolve to a MEMBER — a user-string handle
    // and a standalone signature respectively. Excluded so ResolveMember is not asked a question it can
    // only answer by throwing.
    private static bool IsTokenOperand(OperandType type) => type
        is OperandType.InlineMethod
        or OperandType.InlineField
        or OperandType.InlineType
        or OperandType.InlineTok;

    private static int OperandSize(OperandType type) => type switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI
            or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        _ => throw new InvalidOperationException($"unhandled operand type {type}"),
    };

    // Built from OpCodes itself rather than hand-tabulated, so the table cannot drift from the runtime's
    // own view of the instruction set.
    private static readonly Dictionary<byte, OpCode> OneByte = BuildTable(single: true);

    private static readonly Dictionary<byte, OpCode> TwoByte = BuildTable(single: false);

    private static Dictionary<byte, OpCode> BuildTable(bool single)
    {
        var table = new Dictionary<byte, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op)
            {
                continue;
            }

            var value = (ushort)op.Value;
            var isSingle = value <= 0xFF;
            if (isSingle == single)
            {
                table[(byte)(value & 0xFF)] = op;
            }
        }

        return table;
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
