namespace System.Runtime.CompilerServices;

// net48 predates C# 9 records/init-only setters at the runtime level; this empty marker
// type is the standard shim the Roslyn compiler looks for. No behavior, purely a compile-time hook.
internal static class IsExternalInit
{
}
