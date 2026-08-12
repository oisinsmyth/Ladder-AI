using System.Runtime.CompilerServices;

// The test suite reaches internals for two reasons, both of them load-bearing:
//
//   1. WaveMarkerFormat — the torn-write behaviour is a property of the FORMAT, and testing it only
//      through the store would mean writing files by hand and hoping they match what Serialize emits.
//
//   2. ConfigurationVerdict's factories — the classifier's unknown branch is negative-tested by a
//      deliberately WRONG classifier living in the test project (one that treats an unrecognised
//      configuration as Class A). Building that mutant needs the same construction route the real
//      classifier uses, or the negative test would be measuring a different thing than the guard.
[assembly: InternalsVisibleTo("Ladder.Wave.Tests")]
