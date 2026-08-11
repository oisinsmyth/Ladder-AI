using Xunit;

// Test classes in this assembly run ONE AT A TIME.
//
// Not a performance concession and not caution — a correctness requirement. Two test classes here
// capture stdout/stderr by swapping `Console.Out`/`Console.Error` (BlockLayoutTests and
// DownloadPlanTests, each around a `Program.Run*` call), and those are PROCESS-GLOBAL. xUnit runs
// test classes in parallel by default, so with two of them installing and restoring writers
// concurrently, one class's `finally` restores the writer it captured while the other's was
// installed — and the other test's output goes to a writer nobody reads.
//
// Measured here, 2026-08-11, rather than reasoned about: `Read_Json_CarriesTheLayoutAndNoRequest`
// failed with `String: ""` — its captured stdout was empty, while the code under test had certainly
// written to the console. It passed on the run before and the run after. That intermittency is the
// signature, and it appeared the moment a SECOND console-capturing class was added; until then the
// assembly had exactly one, and was safe by accident rather than by design.
//
// Serializing the assembly is the fix rather than putting the two classes in a shared collection,
// because the hazard is not specific to those two. Any future class that writes to the console —
// or asserts on what was written — is unsafe against any class that redirects it, and a collection
// attribute only protects the classes someone remembers to add it to. The whole suite runs in
// about six seconds, so there is nothing to trade away.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
