using Harness.MirrorView;

namespace Harness.CmdInject;

/// <summary>
/// Loads a map and a binding and resolves the one against the other — <b>the offline half every verb
/// shares.</b> Contacts nothing; the mirror map and the binding are both files in the job folder.
/// </summary>
public sealed record Resolution(bool Ok, ResolvedBinding? Binding, MirrorMap? Map, IReadOnlyList<string> Refusals)
{
    /// <summary>
    /// Read the tag table, the area pointer and the binding, then resolve. Every failure is a list of
    /// reasons, never a throw.
    /// </summary>
    public static Resolution Load(string tagTablePath, string areaPointerPath, string bindingPath)
    {
        ArgumentNullException.ThrowIfNull(tagTablePath);
        ArgumentNullException.ThrowIfNull(areaPointerPath);
        ArgumentNullException.ThrowIfNull(bindingPath);

        var mapLoad = MirrorMapParser.Load(tagTablePath, areaPointerPath);
        if (!mapLoad.Ok)
            return new Resolution(false, null, null, Prefix("map: ", mapLoad.Refusals));

        var bindingLoad = InjectionBinding.Load(bindingPath);
        if (!bindingLoad.Ok)
            return new Resolution(false, null, mapLoad.Map, Prefix("binding: ", bindingLoad.Refusals));

        var resolve = BindingResolver.Resolve(bindingLoad.Binding!, mapLoad.Map!);
        if (!resolve.Ok)
            return new Resolution(false, null, mapLoad.Map, Prefix("resolve: ", resolve.Refusals));

        return new Resolution(true, resolve.Binding, mapLoad.Map, Array.Empty<string>());
    }

    private static IReadOnlyList<string> Prefix(string prefix, IReadOnlyList<string> refusals) =>
        refusals.Select(r => prefix + r).ToList();
}
