using System.Text.Json;
using Converter.ConflictGraph;
using Converter.Ir;
using Converter.SimaticMl;
using Xunit;

namespace Converter.Tests;

/// <summary>
/// 2026-08-17. <b>THE DECLARED JOIN — <c>map.storage</c> — WHICH THIS TOOL DID NOT READ.</b>
///
/// <para><c>conflict-graph --submission</c> took the submission's <c>inputs</c> keys and
/// <c>expectations[].signal</c> values and fed them to a resolver expecting STORAGE PATHS. A submission
/// speaks the SPECIFICATION's vocabulary by design (D8), so on a real submission it returned <b>70 of
/// 70 unresolved</b> — while the field carrying the join sat in the same document, unread.</para>
///
/// <para>🔴 <b>THE FIXTURE TRAP THIS FILE EXISTS TO AVOID.</b> Every fixture in
/// <see cref="ConflictGraphTests"/> cites a signal whose name IS its storage path, and every fixture on
/// the harness side declared <c>specName == tag</c>. <b>With the two keys indistinguishable, no test
/// can fail on a join defect</b> — 1,800 of them did not. <b>Every cited name in this file differs from
/// the storage path it must reach</b>, so a resolver that ignores the declaration cannot pass.</para>
/// </summary>
public class ConflictGraphDeclaredJoinTests : IDisposable
{
    private readonly string _dir;

    public ConflictGraphDeclaredJoinTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"cg-join-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);

        // FB_Owner writes its OWN interface member as a bare path; FC_Caller writes the SAME location
        // through the instance path. Two spellings, one storage, two writing blocks — a genuine
        // cross-block multi-writer that is invisible to anything resolving only one spelling.
        Write("FB_Owner.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Owner", 60, "LAD", null,
            new[]
            {
                new IrNetwork(1, "Drive the alarm", new[]
                {
                    new CoilAssignment("IO.Alarm", new Expr.TagRef("IO.Level")),
                }),
            },
            StaticMembers: new[]
            {
                new DbMember("IO", "\"UDT_Io\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Alarm", "Bool", Retain: false, StartValue: null),
                    new DbMember("Level", "Bool", Retain: false, StartValue: null),
                }),
            })));

        Write("iDB_Owner.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Owner", 61, InstanceOfName: "FB_Owner", Comment: null, Members: new[]
            {
                new DbMember("IO", "\"UDT_Io\"", Retain: true, StartValue: null, SetPoint: true, NestedMembers: new[]
                {
                    new DbMember("Alarm", "Bool", Retain: false, StartValue: null),
                    new DbMember("Level", "Bool", Retain: false, StartValue: null),
                }),
            })));

        Write("FC_Caller.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FC", "FC_Caller", 62, "LAD", null,
            new[]
            {
                new IrNetwork(1, "Also drives it", new[]
                {
                    new CoilAssignment("iDB_Owner.IO.Alarm", new Expr.TagRef("PlantEnable")),
                }),
            })));

        // FB_Solo's member is written ONLY from inside the owning block — nothing anywhere references
        // the instance path. This is the SECOND measured failure mode's shape: the harness cites
        // `iDB_Solo.Echo` and the corpus contains only `FB_Solo.Echo`.
        Write("FB_Solo.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FB", "FB_Solo", 63, "LAD", null,
            new[]
            {
                new IrNetwork(1, "Echo", new[]
                {
                    new CoilAssignment("Echo", new Expr.TagRef("Command")),
                }),
            },
            StaticMembers: new[]
            {
                new DbMember("Echo", "Bool", Retain: false, StartValue: null),
                new DbMember("Command", "Bool", Retain: false, StartValue: null),
            })));

        Write("iDB_Solo.ir", DbIrSerializer.Serialize(new DbSource(
            "0", "iDB_Solo", 64, InstanceOfName: "FB_Solo", Comment: null, Members: new[]
            {
                new DbMember("Echo", "Bool", Retain: false, StartValue: null),
                new DbMember("Command", "Bool", Retain: false, StartValue: null),
            })));

        // A plain global tag two plant blocks write — the shape that already worked, kept so the
        // OVER-FIRE CONVERSE is testable: what resolved before must still resolve.
        Write("FC_PlantA.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FC", "FC_PlantA", 65, "LAD", null,
            new[] { new IrNetwork(1, "W", new[] { new CoilAssignment("SharedPlantTag", new Expr.TagRef("Enable")) }) })));
        Write("FC_PlantB.ir", IrSerializer.SerializeBlockReadable(new IrBlock(
            "0", "FC", "FC_PlantB", 66, "LAD", null,
            new[] { new IrNetwork(1, "W", new[] { new CoilAssignment("SharedPlantTag", new Expr.TagRef("Enable")) }) })));
    }

    private void Write(string file, string content) => File.WriteAllText(Path.Combine(_dir, file), content);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private string Submission(string body)
    {
        var path = Path.Combine(_dir, $"submission-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, body);
        return path;
    }

    private ConflictGraphReport RunSubmission(string body, bool allowUnresolved = false)
    {
        var path = Submission(body);
        var signals = ConflictGraphRunner.SignalsFromSubmission(path)
            .Select(s => new CitedSignal(s, SignalOrigin.Submission)).ToList();
        return ConflictGraphRunner.Run(
            _dir, signals, SubmissionSignalMap.ReadFromSubmission(path), allowUnresolved);
    }

    private static SignalResolutionFact Fact(ConflictGraphReport report, string signal) =>
        Assert.Single(report.Signals.Where(s => s.Signal == signal));

    // ---- THE DEFECT ------------------------------------------------------------------------------

    // *** THE HEADLINE. *** `SPEC.BlockageAlarm` is not a storage path, is not a suffix of one, and
    // shares no substring with `FB_Owner.IO.Alarm`. Only the declaration can carry it there.
    [Fact]
    public void ASpecNameReachesItsStorage_ThroughTheDeclaredJoinAndNothingElse()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.BlockageAlarm" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": { "owner": "FB_Owner", "path": "IO.Alarm" } } }
        }
        """);

        Assert.True(report.Computed);
        var fact = Fact(report, "SPEC.BlockageAlarm");
        Assert.Equal(SignalResolution.Resolved, fact.Resolution);
        Assert.Equal(SignalJoinKind.DeclaredStorage, fact.Join);

        // And it is a REAL resolution, not a label: the two writers of that one storage become an edge.
        var edge = Assert.Single(report.Edges);
        Assert.Equal("FB_Owner", edge.BlockA);
        Assert.Equal("FC_Caller", edge.BlockB);
        Assert.Equal("SPEC.BlockageAlarm", edge.Signal);
        Assert.Equal(EdgeSignalClass.Deliverable, edge.Class);
    }

    // A global declaration reaches the same storage by the instance path — the other half of the join.
    [Fact]
    public void AGlobalDeclarationReachesTheSameStorage_ByTheInstancePath()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.BlockageAlarm" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": { "path": "iDB_Owner.IO.Alarm" } } }
        }
        """);

        Assert.True(report.Computed);
        Assert.Equal(SignalJoinKind.DeclaredStorage, Fact(report, "SPEC.BlockageAlarm").Join);
        Assert.Single(report.Edges);
    }

    // ---- THE REFUSAL, WHICH IS NOT WEAKENED ------------------------------------------------------

    // *** A NAME WITH NO DECLARED MAPPING STAYS UNRESOLVED AND THE GRAPH IS STILL WITHHELD. *** This is
    // the property that made the defect findable rather than absorbed, and the fix must not spend it.
    [Fact]
    public void ASignalTheMapDoesNotName_StaysUnresolved_AndTheKeyIsStillWithheld()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.BlockageAlarm" }, { "signal": "SPEC.Undeclared" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": { "owner": "FB_Owner", "path": "IO.Alarm" } } }
        }
        """);

        Assert.False(report.Computed);
        var fact = Fact(report, "SPEC.Undeclared");
        Assert.Equal(SignalResolution.Unresolved, fact.Resolution);
        Assert.Equal(SignalJoinKind.NotDeclared, fact.Join);

        using var doc = JsonDocument.Parse(ConflictGraphOutputFormatter.FormatJson(report));
        Assert.False(doc.RootElement.TryGetProperty("conflictEdges", out _));
    }

    // *** AND IT IS NOT QUIETLY RESCUED BY A NAME MATCH. *** `SharedPlantTag` IS a storage path in this
    // corpus and would resolve by name in a heartbeat. Once the document declares a map, a signal
    // absent from it is a HOLE IN THE MAP, and filling it by name shape is the aliasing the declared
    // join replaced — two of the four cross-block multi-writers this project ever recorded were fiction
    // produced exactly that way.
    [Fact]
    public void AnUndeclaredSignalIsNotRescuedByNameShape_EvenWhenTheNameWouldHaveMatched()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": { "SharedPlantTag": "1" }, "expectations": [ { "signal": "SPEC.BlockageAlarm" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": { "owner": "FB_Owner", "path": "IO.Alarm" } } }
        }
        """, allowUnresolved: true);

        var fact = Fact(report, "SharedPlantTag");
        Assert.Equal(SignalResolution.Unresolved, fact.Resolution);
        Assert.Equal(SignalJoinKind.NotDeclared, fact.Join);

        // The proof that it WOULD have matched: the same name, with no map declared anywhere, resolves.
        var undeclared = RunSubmission("""
        { "vectors": [ { "inputs": { "SharedPlantTag": "1" }, "expectations": [] } ] }
        """);
        Assert.Equal(SignalResolution.Resolved, Fact(undeclared, "SharedPlantTag").Resolution);
        Assert.Equal(SignalJoinKind.ProjectPathMatch, Fact(undeclared, "SharedPlantTag").Join);
    }

    // A declaration that names a location the corpus does not contain is unresolved WITH ITS OWN
    // REASON — "the join was stated and is wrong" is a different repair from "nobody stated a join".
    [Fact]
    public void ADeclarationNamingNothingInTheCorpus_IsUnresolvedAsADeclarationFault()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.BlockageAlarm" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": { "owner": "FB_Nonexistent", "path": "IO.Alarm" } } }
        }
        """);

        Assert.False(report.Computed);
        var fact = Fact(report, "SPEC.BlockageAlarm");
        Assert.Equal(SignalResolution.Unresolved, fact.Resolution);
        Assert.Equal(SignalJoinKind.DeclaredStorage, fact.Join);
        Assert.Contains("NOT falling back to a name match", fact.Reason);
    }

    // ---- `harnessOnly`: the third state, and it is a CLAIM ---------------------------------------

    // A signal declared to occupy no PLC storage does not count against the scope — no edge is
    // possible, and that is COMPUTED. A submission whose signals are all mirror-side names is
    // legitimately computable, and refusing it would refuse an author who did exactly what was asked.
    [Fact]
    public void ASignalDeclaredHarnessOnly_IsAComputedFact_NotAGap()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.ScenarioDone" } ] } ],
          "map": { "harnessOnly": [ "SPEC.ScenarioDone" ] }
        }
        """);

        Assert.True(report.Computed);
        Assert.Empty(report.Edges);
        Assert.Empty(report.Unresolved);
        var fact = Assert.Single(report.HarnessOnly);
        Assert.Equal(SignalJoinKind.DeclaredHarnessOnly, fact.Join);
    }

    // *** A CONTRADICTION IS REFUSED, AND `--allow-unresolved` DOES NOT COVER IT. *** That flag accepts
    // names nobody looked at; it cannot accept a document that answers one question twice, differently.
    [Fact]
    public void ASignalDeclaredBothWays_IsRefused_AndTheNamedEscapeDoesNotReachIt()
    {
        const string body = """
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.Both" } ] } ],
          "map": {
            "storage": { "SPEC.Both": { "owner": "FB_Owner", "path": "IO.Alarm" } },
            "harnessOnly": [ "SPEC.Both" ]
          }
        }
        """;

        Assert.False(RunSubmission(body).Computed);

        var escaped = RunSubmission(body, allowUnresolved: true);
        Assert.False(escaped.Computed);
        Assert.Contains("--allow-unresolved DOES NOT COVER THIS", escaped.NotComputedReason);
        Assert.Equal(SignalResolution.Refused, Assert.Single(escaped.RefusedSignals).Resolution);
    }

    // A `map.storage` entry written as a bare dotted string is REJECTED, never read as a global path.
    // Owner and path are separate keys because an emitted string is not a schema — and a rejection
    // makes the run NOT COMPUTED rather than quietly judging a smaller scope.
    [Fact]
    public void AMalformedMapEntry_IsARejectionThatWithholdsTheGraph_NotASkip()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.BlockageAlarm" } ] } ],
          "map": { "storage": { "SPEC.BlockageAlarm": "FB_Owner.IO.Alarm" } }
        }
        """, allowUnresolved: true);

        Assert.False(report.Computed);
        Assert.Contains("OWNER AND PATH ARE SEPARATE KEYS", report.NotComputedReason);
    }

    // ---- THE SECOND MEASURED FAILURE MODE --------------------------------------------------------

    // *** MEASURED SEPARATELY AND REPORTED SEPARATELY: with the binding's storage tags supplied
    // DIRECTLY, a real slot still resolved 0 of 68 — because the corpus references those members only
    // from INSIDE the owning block, never through the instance path the harness uses. *** The repair is
    // an IDENTITY join, not a name shape: the instance DB's own declared members say which
    // `iDB.<suffix>` names the FB's member.
    [Fact]
    public void AnInstancePathResolves_EvenWhenTheCorpusOnlyTouchesTheMemberInsideItsOwningBlock()
    {
        var report = ConflictGraphRunner.Run(
            _dir, new[] { "iDB_Solo.Echo" }, allowUnresolved: false);

        Assert.True(report.Computed);
        var fact = Assert.Single(report.Signals);
        Assert.Equal(SignalResolution.Resolved, fact.Resolution);
        Assert.Equal(new[] { "FB_Solo.Echo" }, fact.Candidates);
    }

    // The same through the DECLARED join, which is how a submission would actually reach it.
    [Fact]
    public void TheSameInstancePathResolvesWhenDECLARED_WhichIsHowASubmissionReachesIt()
    {
        var report = RunSubmission("""
        {
          "vectors": [ { "inputs": {}, "expectations": [ { "signal": "SPEC.EchoBack" } ] } ],
          "map": { "storage": { "SPEC.EchoBack": { "path": "iDB_Solo.Echo" } } }
        }
        """);

        Assert.True(report.Computed);
        Assert.Equal(SignalResolution.Resolved, Fact(report, "SPEC.EchoBack").Resolution);
    }

    // *** THE WRITER SET IS THE UNION ACROSS SPELLINGS, AND WITHOUT THAT THE EDGE DOES NOT EXIST. ***
    // Resolving `iDB_Owner.IO.Alarm` to the group keyed on that spelling alone finds ONE writer
    // (FC_Caller) and reports SINGLE-WRITER — the FB's own internal write is in the other group. That
    // is a MISSED conflict, which is the direction that costs something.
    [Fact]
    public void WritersAreUnionedAcrossEverySpellingOfOneStorage()
    {
        var report = ConflictGraphRunner.Run(_dir, new[] { "iDB_Owner.IO.Alarm" }, allowUnresolved: false);

        var edge = Assert.Single(report.Edges);
        Assert.Equal("FB_Owner", edge.BlockA);
        Assert.Equal("FC_Caller", edge.BlockB);
        Assert.Contains("pooled", Assert.Single(report.Signals).Reason);
    }

    // *** THE UNION HAS A BOUNDARY, AND IT IS REFUSED RATHER THAN GUESSED. *** An FB with TWO instance
    // DBs has an internal write landing in BOTH, so pooling `iDB_A.x` with `iDB_B.x` would invent a
    // conflict between blocks that touch genuinely different storage — the fiction the corrected
    // grouping exists to remove. Built in its own corpus because adding a second instance to the shared
    // fixture would (correctly) make every other test's signal ambiguous.
    [Fact]
    public void AMemberReachedThroughTwoInstanceDbs_IsRefused_NotPooled()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"cg-join2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "FB_Twin.ir"), IrSerializer.SerializeBlockReadable(new IrBlock(
                "0", "FB", "FB_Twin", 70, "LAD", null,
                new[] { new IrNetwork(1, "W", new[] { new CoilAssignment("Flag", new Expr.TagRef("In")) }) },
                StaticMembers: new[]
                {
                    new DbMember("Flag", "Bool", Retain: false, StartValue: null),
                    new DbMember("In", "Bool", Retain: false, StartValue: null),
                })));

            foreach (var (instance, number) in new[] { ("iDB_TwinA", 71), ("iDB_TwinB", 72) })
            {
                File.WriteAllText(Path.Combine(dir, $"{instance}.ir"), DbIrSerializer.Serialize(new DbSource(
                    "0", instance, number, InstanceOfName: "FB_Twin", Comment: null, Members: new[]
                    {
                        new DbMember("Flag", "Bool", Retain: false, StartValue: null),
                        new DbMember("In", "Bool", Retain: false, StartValue: null),
                    })));
            }

            File.WriteAllText(Path.Combine(dir, "FC_Drives.ir"), IrSerializer.SerializeBlockReadable(new IrBlock(
                "0", "FC", "FC_Drives", 73, "LAD", null,
                new[]
                {
                    new IrNetwork(1, "Both", new[]
                    {
                        new CoilAssignment("iDB_TwinA.Flag", new Expr.TagRef("Enable")),
                        new CoilAssignment("iDB_TwinB.Flag", new Expr.TagRef("Enable")),
                    }),
                })));

            var report = ConflictGraphRunner.Run(dir, new[] { "FB_Twin.Flag" }, allowUnresolved: false);

            Assert.False(report.Computed);
            var fact = Assert.Single(report.Ambiguous);
            Assert.Equal(new[] { "iDB_TwinA.Flag", "iDB_TwinB.Flag" }, fact.Candidates.OrderBy(c => c, StringComparer.Ordinal));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    // ---- THE OVER-FIRE CONVERSE ------------------------------------------------------------------

    // *** A SUBMISSION THAT DECLARES NO MAP AT ALL BEHAVES EXACTLY AS BEFORE. *** A gate that refuses
    // submissions outside its scope is noise, and noise gets switched off — after which the cases it
    // was right about go through unchecked.
    [Fact]
    public void ASubmissionWithNoMapAtAll_StillResolvesByProjectPath_AndSaysSo()
    {
        var report = RunSubmission("""
        { "vectors": [ { "inputs": { "SharedPlantTag": "1" }, "expectations": [] } ] }
        """);

        Assert.True(report.Computed);
        Assert.Single(report.Edges);
        var fact = Assert.Single(report.Signals);
        Assert.Equal(SignalJoinKind.ProjectPathMatch, fact.Join);
        Assert.Contains("declares NO `map.storage`", fact.Reason);
    }

    // `--signals` is an operator's list of storage paths. It has no document declaring a join, so the
    // declared-join rule must never reach it — otherwise the flag refuses every use of itself.
    [Fact]
    public void AnOperatorSignalList_IsNeverHeldToADeclaredJoin()
    {
        var report = ConflictGraphRunner.Run(
            _dir,
            new[] { new CitedSignal("SharedPlantTag", SignalOrigin.OperatorList) },
            SubmissionSignalMap.Of(new[] { ("SPEC.X", new DeclaredStorage(null, "iDB_Owner.IO.Alarm")) }),
            allowUnresolved: false);

        Assert.True(report.Computed);
        Assert.Equal(SignalJoinKind.ProjectPathMatch, Assert.Single(report.Signals).Join);
    }

    // ---- READING THE MAP -------------------------------------------------------------------------

    [Fact]
    public void ReadFromSubmission_TakesOwnerAndPathSeparately_AndTheHarnessOnlyList()
    {
        var map = SubmissionSignalMap.ReadFromSubmission(Submission("""
        {
          "map": {
            "providedFor": { "SPEC.A": [ "sampled" ] },
            "storage": {
              "SPEC.A": { "owner": "FB_Owner", "path": "IO.Alarm" },
              "SPEC.B": { "path": "DB_X.Member" }
            },
            "harnessOnly": [ "SPEC.C" ]
          }
        }
        """));

        Assert.Equal(new DeclaredStorage("FB_Owner", "IO.Alarm"), map.StorageOf("SPEC.A"));
        Assert.Equal(new DeclaredStorage(null, "DB_X.Member"), map.StorageOf("SPEC.B"));
        Assert.True(map.StorageOf("SPEC.B")!.IsGlobal);
        Assert.Equal(DeclaredJoin.HarnessOnly, map.Resolve("SPEC.C"));
        Assert.Equal(DeclaredJoin.NotStated, map.Resolve("SPEC.D"));
    }

    // A document with no `map` is the empty map — NOT a throw, because this reads another lane's
    // evolving schema — and `Declared` is false, which is what routes it to the legacy behaviour.
    [Fact]
    public void ReadFromSubmission_ADocumentWithNoMap_IsTheEmptyMapAndNotAThrow()
    {
        var map = SubmissionSignalMap.ReadFromSubmission(Submission("""{ "vectors": [] }"""));

        Assert.False(map.Declared);
        Assert.Empty(map.Storage);
    }

    // Entries are a LIST so a signal declared twice is EXPRESSIBLE and therefore refusable. A
    // dictionary would silently have kept one — the resolution the contract forbids.
    [Fact]
    public void ASignalDeclaredTwiceWithDifferentStorage_IsAnAmbiguityRatherThanALastWriterWins()
    {
        var map = SubmissionSignalMap.Of(new[]
        {
            ("SPEC.A", new DeclaredStorage(null, "iDB_Owner.IO.Alarm")),
            ("SPEC.A", new DeclaredStorage(null, "iDB_Solo.Echo")),
        });

        Assert.Empty(map.Storage);
        Assert.Equal(2, Assert.Single(map.Ambiguities).Candidates.Count);

        // And it is REFUSED rather than merely unresolved, so `--allow-unresolved` cannot wave it
        // through: an author who declared one signal in two places has not left a gap, they have
        // written a document that answers the question twice.
        var report = ConflictGraphRunner.Run(
            _dir, new[] { new CitedSignal("SPEC.A", SignalOrigin.Submission) }, map, allowUnresolved: true);
        Assert.False(report.Computed);
        var fact = Assert.Single(report.Signals);
        Assert.Equal(SignalResolution.Refused, fact.Resolution);
        Assert.Equal(2, fact.Candidates.Count);
    }
}
