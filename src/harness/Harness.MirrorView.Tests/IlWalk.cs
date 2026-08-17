using System.Reflection;

namespace Harness.MirrorView.Tests;

/// <summary>What one walk over an assembly found, <b>and how much of it it looked at</b>.</summary>
/// <param name="Hits">Methods naming a forbidden member, with what they named.</param>
/// <param name="TypesEnumerated">Types walked.</param>
/// <param name="MethodsEnumerated">Methods walked.</param>
/// <param name="BodiesExamined">
/// 🔴 <b>THE DENOMINATOR.</b> <c>Hits.Count == 0</c> is true of <i>nothing found</i> and of <i>nothing
/// looked at</i>, and only this number separates them. Every assertion built on this walk asserts it.
/// </param>
public sealed record IlScan(
    IReadOnlyList<string> Hits,
    int TypesEnumerated,
    int MethodsEnumerated,
    int BodiesExamined);

/// <summary>
/// The IL walk, <b>extracted so that every structural claim in this assembly uses ONE scanner.</b>
///
/// <para>It lived as a private helper inside <c>MirrorViewStructureTests</c>. A second structural claim
/// arrived and the choice was to copy it or to share it — and two walks are two things that can rot
/// independently, with the copy rotting silently because its own tests keep passing. <i>When a pattern
/// already exists in the repo, the question is which siblings did not get it.</i></para>
///
/// <para><b>The predicate is a PARAMETER, never a literal inside the walk.</b> A check whose exercise
/// requires editing the check will not be exercised — that is how the equivalent control in
/// <c>openness-cli</c> decayed into a comment describing a manual run somebody once did.</para>
/// </summary>
public static class IlWalk
{
    /// <summary>Walk every method body in <paramref name="assembly"/>, reporting those naming a forbidden member.</summary>
    public static IlScan Scan(Assembly assembly, Func<MemberInfo, bool> forbidden)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(forbidden);

        var hits = new List<string>();
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
            Assert.Fail($"{ex.LoaderExceptions.Length} type(s) in {assembly.GetName().Name} could not be loaded, " +
                        "so the walk cannot claim to have examined the assembly: " +
                        string.Join("; ", ex.LoaderExceptions.Select(e => e?.Message)));
            return new IlScan(hits, 0, 0, 0);
        }

        foreach (var type in loadable)
        {
            types++;
            foreach (var method in type.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                methods++;

                byte[]? il;
                try
                {
                    il = method.GetMethodBody()?.GetILAsByteArray();
                }
                catch (Exception)
                {
                    // Abstract, extern and generated members have no readable body.
                    continue;
                }

                if (il is null) continue;

                bodies++;
                if (References(il, method, forbidden, out var detail))
                    hits.Add($"{type.FullName}.{method.Name}: {detail}");
            }
        }

        return new IlScan(hits, types, methods, bodies);
    }

    /// <summary>The declaring type's full name, or the type's own when the member IS a type.</summary>
    public static string? DeclaringName(MemberInfo member) =>
        member is Type type ? type.FullName : member.DeclaringType?.FullName;

    /// <summary>
    /// Deliberately coarse: every 4-byte window is offered to the token resolver rather than the IL being
    /// decoded precisely. That OVER-reports candidate tokens and UNDER-reports nothing, and since the
    /// assertions are that a count is ZERO, over-reporting is the safe direction — a false positive fails
    /// a test and gets read, a false negative lets the one thing this guards against through unnoticed.
    ///
    /// <para>⚠️ The direction reverses for the CONTROLS, which assert a count above zero: there a spurious
    /// token could make a broken walk look armed. Every control matches on a specific expected member
    /// rather than on "found something".</para>
    /// </summary>
    private static bool References(byte[] il, MethodInfo owner, Func<MemberInfo, bool> forbidden, out string detail)
    {
        detail = string.Empty;
        var module = owner.Module;
        var typeArgs = SafeGenericArguments(owner.DeclaringType);
        var methodArgs = owner.IsGenericMethodDefinition ? owner.GetGenericArguments() : Type.EmptyTypes;

        for (var i = 0; i + 4 <= il.Length; i++)
        {
            var token = BitConverter.ToInt32(il, i);

            try
            {
                var member = module.ResolveMember(token, typeArgs, methodArgs);
                if (member is not null && forbidden(member))
                {
                    detail = $"references {DeclaringName(member)}.{member.Name}";
                    return true;
                }
            }
            catch (Exception)
            {
                // Not a member token. Expected constantly — see the remarks on coarseness.
            }
        }

        return false;
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
