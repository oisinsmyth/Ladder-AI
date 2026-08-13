using System;
using System.Collections.Generic;
using System.Linq;

namespace Ladder.Wave.Tests
{
    /// <summary>
    /// Builders for the objects the admission, batching and queue tests are written against. Kept in
    /// one place so a test reads as the SITUATION it is about rather than as a page of constructor
    /// arguments.
    /// </summary>
    internal static class ChangeSets
    {
        /// <summary>A hash stands for "the content this evidence is about"; any stable string will do.</summary>
        public const string Hash = "sha256:aaaa";

        /// <summary>A second, different hash — for the stale-evidence case.</summary>
        public const string OtherHash = "sha256:bbbb";

        public static ChangedObject Fb(string name, string? hash = Hash, params string[] dependsOn) =>
            new ChangedObject(name, ObjectKind.FunctionBlock, ChangeClass.Run, dependsOn, hash);

        public static ChangedObject InstanceDb(string name, string ofFb, string? hash = Hash) =>
            new ChangedObject(name, ObjectKind.InstanceDataBlock, ChangeClass.Run, new[] { ofFb }, hash);

        public static ChangedObject Udt(string name, string? hash = Hash) =>
            new ChangedObject(name, ObjectKind.DataType, ChangeClass.Run, null, hash);

        public static ChangedObject GlobalDb(string name, string? hash = Hash, params string[] dependsOn) =>
            new ChangedObject(name, ObjectKind.GlobalDataBlock, ChangeClass.RunInit, dependsOn, hash);

        public static ChangedObject Ob(string name, string? hash = Hash) =>
            new ChangedObject(name, ObjectKind.OrganizationBlock, ChangeClass.Stop, null, hash);

        public static Submission Submission(string id, params ChangedObject[] objects) =>
            new Submission(id, "agent-" + id, objects);

        /// <summary>Evidence that PASSES both checks, about the content each object actually carries.</summary>
        public static IEnumerable<AdmissionEvidence> GoodEvidence(params ChangedObject[] objects) =>
            objects.Select(o => new AdmissionEvidence(
                o.Name,
                o.ArtifactHash,
                EvidenceOutcome.Passed,
                EvidenceOutcome.Passed,
                "test fixture"));

        /// <summary>Everything the submission carries, with passing evidence, admitted.</summary>
        public static AdmissionDecision AdmitAll(Submission submission) =>
            AdmissionController.Admit(submission, GoodEvidence(submission.Objects.ToArray()));

        public static DeployedProgram Deployed(params string[] names) =>
            names.Length == 0
                ? DeployedProgram.Empty("test fixture: a bare test project, nothing loaded yet")
                : DeployedProgram.From(names, "test fixture: load manifest");
    }
}
