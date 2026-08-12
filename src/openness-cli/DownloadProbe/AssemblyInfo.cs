using System.Runtime.CompilerServices;

// Portal is off limits for this tool's verification, so the tests carry the assurance — and almost
// everything worth asserting here (the scratch-path guard, the option literal parser, the selection
// policy and its deny list) is internal by design. The tests live in OpennessCli.Tests rather than a
// second suite so they inherit its assembly-wide DisableTestParallelization, which is a correctness
// requirement for anything that captures Console (see OpennessCli.Tests/AssemblyTestBehavior.cs).
[assembly: InternalsVisibleTo("OpennessCli.Tests")]
