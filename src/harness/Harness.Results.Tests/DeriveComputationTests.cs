using Harness.Gate;
using Harness.Results;
using Xunit;

namespace Harness.Results.Tests;

/// <summary>
/// 🔴 <b>PHASE 1: the deriver RECOMPUTES and COMPARES, and refuses an artifact that is the wrong kind
/// or is itself a report of failure.</b>
///
/// <para>*** THE TWO CASES THAT DROVE THIS WERE FOUND ON A REAL JOB, NOT REASONED ABOUT. *** A conflict
/// graph that says <c>notComputed</c>, and a deployment result that says <c>NotDeployed</c>. Both exist,
/// both are readable, both hash — and attribution alone would have stamped them.</para>
///
/// <para><b>Both directions everywhere.</b> Each refusal is paired with the same input, corrected,
/// passing — otherwise a deriver that refused everything would satisfy the whole file.</para>
/// </summary>
public class DeriveComputationTests
{
    // ---------------------------------------------------------------------------------------------
    // 1.1 — recompute the map and compare
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_MAP_THAT_MATCHES_THE_BINDING_IS_COMPUTED_not_merely_attributed()
    {
        var (exit, output, _) = Derive(binding: Binding);

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("COMPUTED   " + DerivableField.Map, output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_MAP_CLAIMING_A_SIGNAL_THE_COPY_LAYER_DOES_NOT_CARRY_IS_REFUSED_BY_NAME()
    {
        // OVER-CLAIMING is the dangerous direction: a vector could be admitted observing something that
        // is not actually mirrored, which is exactly what gate 5 exists to prevent. Before this phase
        // that submission was stamped and passed.
        var (exit, output, written) = Derive(binding: Binding.Replace("Demo_Count", "Demo_Other", StringComparison.Ordinal));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("MISMATCH   " + DerivableField.Map, output, StringComparison.Ordinal);
        Assert.Contains("signals the copy layer does not carry: Demo_Count", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void BUT_A_BINDING_PROVIDING_MORE_THAN_THE_MAP_LISTS_IS_INCOMPLETE_RATHER_THAN_WRONG()
    {
        // 🔴 *** THE ASYMMETRY, AND IT WAS FOUND ON REAL DATA. *** A live submission's binding provided
        // ~70 signals its map did not list. Refusing that would refuse every honest submission in the
        // job, over a difference that cannot produce a wrong result: nothing can rely on a signal the
        // map does not name. It passes, and the incompleteness is REPORTED rather than waved through.
        var extra = Binding.Replace(
            """{ "tag": "Demo_Count", "type": "Int", "specName": "Demo_Count" }""",
            """{ "tag": "Demo_Count", "type": "Int", "specName": "Demo_Count" }, { "tag": "Demo_Spare", "type": "Int", "specName": "Demo_Spare" }""",
            StringComparison.Ordinal);

        var (exit, output, _) = Derive(binding: extra);

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("COMPUTED   " + DerivableField.Map, output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_MODE_THE_COPY_LAYER_DOES_NOT_PROVIDE_IS_REFUSED_TOO()
    {
        // The minimal copy layer emits result-register MOVEs and no per-signal latch, so a map claiming
        // Latched on a result is claiming instrumentation that does not exist.
        var (exit, output, _) = Derive(
            submission: GateCliTests.Good.Replace("""["Sampled"]""", """["Sampled", "Latched"]""", StringComparison.Ordinal));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("instrumentation the copy layer does not provide", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 1.5 — the artifact must be the right kind, and must not be a failure report
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_DEPLOYMENT_ARTIFACT_REPORTING_NotDeployed_IS_REFUSED_AS_A_FAILURE_REPORT()
    {
        // 🔴 MEASURED ON THE LIVE JOB. The device was not running the staged build and the artifact said
        // so in its own detail; every address the submission carried was derived for a different program.
        var (exit, output, written) = Derive(deploy: """
            { "outcome": "NotDeployed", "detail": "the device's version register does not match the staged build." }
            """);

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("BAD SOURCE " + DerivableField.Deployment, output, StringComparison.Ordinal);
        Assert.Contains("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
        Assert.Contains("version register", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void AND_THE_SAME_ARTIFACT_REPORTING_Ran_IS_ACCEPTED()
    {
        var (exit, output, _) = Derive(deploy: """{ "outcome": "Ran" }""");

        Assert.True(exit == DeriveExit.Derived, output);
    }

    [Fact]
    public void A_DOWNLOAD_PROBE_LOG_IS_ACCEPTED_because_that_is_what_actually_performs_a_download()
    {
        // 🔴 *** MEASURED: `deployment` WAS UNATTRIBUTABLE IN PRACTICE. *** This check originally took
        // only a serialized LoopResult, on the reasoning that the loop's gateway deploys. That gateway has
        // never run; every real download on the rig is done by `download-probe`, whose evidence is a text
        // log. A field nothing can satisfy is a gate that refuses correct work.
        var (exit, output, _) = Derive(deploy: ProbeLog("TRANSFERRED"));

        Assert.True(exit == DeriveExit.Derived, output);
    }

    [Fact]
    public void AND_A_PROBE_LOG_THAT_MOVED_NOTHING_IS_REFUSED_even_though_TRANSFERRED_is_a_substring()
    {
        // *** THE SUBSTRING TRAP, PINNED. *** The rendered failure value is NOTHINGTRANSFERRED, which
        // CONTAINS "TRANSFERRED" - so a Contains check would read "nothing was transferred" as success.
        // The probe's own docs say it outright: "transfers NOTHING. Read the TRANSFER VERDICT, never the
        // state." A download can report state=Success having moved nothing.
        var (exit, output, _) = Derive(deploy: ProbeLog("NOTHINGTRANSFERRED"));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
        Assert.Contains("NOTHINGTRANSFERRED", output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_A_PROBE_LOG_WITH_NO_VERDICT_LINE_IS_REFUSED()
    {
        var (exit, output, _) = Derive(deploy: "==== download-probe ====\nstarted : whenever\n");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("no TRANSFER VERDICT line", output, StringComparison.Ordinal);
    }

    /// <summary>A download-probe log carrying one rendered TRANSFER VERDICT value.</summary>
    private static string ProbeLog(string verdict) =>
        "==== download-probe ============================================================\n"
        + "project        : somewhere/Scratch.ap20\n"
        + $"TRANSFER VERDICT : {verdict}\n"
        + "Download completed: state=Success, errors=0, warnings=0.\n";

    [Fact]
    public void AN_UNRECOGNISED_DEPLOY_OUTCOME_IS_REFUSED_because_this_check_fails_closed()
    {
        // A new outcome value will be added by somebody who is not thinking about this check.
        var (exit, output, _) = Derive(deploy: """{ "outcome": "SomethingNewNobodyToldUsAbout" }""");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_CONFLICT_GRAPH_REPORTING_notComputed_IS_REFUSED_AS_A_FAILURE_REPORT()
    {
        // 🔴 The other live-job case. The graph resolved nothing and said so; the submission declared the
        // edges anyway.
        var (exit, output, _) = Derive(conflicts: """{ "notComputed": "68 of 68 signals unresolved." }""");

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
        Assert.Contains("68 of 68", output, StringComparison.Ordinal);
    }

    [Fact]
    public void A_WRONG_KIND_ARTIFACT_IS_A_DIFFERENT_REFUSAL_FROM_A_FAILURE_REPORT()
    {
        // *** TWO CLASSES, KEPT APART BECAUSE THEY ARE DIFFERENT MISTAKES. *** "You pointed it at the
        // wrong file" and "the right file says it has no answer" need different fixes.
        var (exit, output, _) = Derive(reachable: Binding);

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("WRONG KIND", output, StringComparison.Ordinal);
        Assert.DoesNotContain("ARTIFACT REPORTS FAILURE", output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // storage — a suffix is not an identity
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void A_CLOSURE_THAT_ONLY_SHARES_A_SUFFIX_DOES_NOT_COUNT_AS_A_MATCH()
    {
        // 🔴 *** THE FALSE POSITIVE THIS REPLACED, MEASURED ON A REAL JOB. *** A first cut also indexed
        // every path with its root dropped, so `iDB_UnderTest.IO.Fault` "matched"
        // `FB_Owner|MemberBlock.IO.Fault` - a different location, in a closure covering none of the
        // block under test. Three of four declarations matched that way against an artifact that mentions
        // none of them. A suffix is not an identity.
        //
        // (Names invented 2026-08-23. They were the job's own until then - a leak in the documented
        // shape: an identifier arriving inside an explanatory comment, which does not feel like job
        // content, it feels like rigour. The shape is the lesson; the vocabulary never was.)
        //
        // The submission declares `DemoUnit.Demo_Count`; this closure carries only `SomethingElse.Demo_Count`.
        var (exit, output, _) = Derive(
            submission: GateCliTests.Good.Replace(
                """{ "owner": "DemoUnit", "path": "Demo_Count" }""",
                """{ "owner": "DemoUnit", "path": "DemoUnit.Demo_Count" }""",
                StringComparison.Ordinal),
            reachable: """{ "blocks": [ { "block": "Other", "reachableState": [ "SomethingElse.Demo_Count" ] } ] }""");

        // Not a match, and not an accusation either: the honest answer is that nothing was checked.
        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("ATTRIBUTED " + DerivableField.Storage, output, StringComparison.Ordinal);
        Assert.DoesNotContain("COMPUTED   " + DerivableField.Storage, output, StringComparison.Ordinal);
    }

    [Fact]
    public void AND_THE_SAME_PATH_SPELLED_WITH_A_BAR_STILL_MATCHES()
    {
        // The one normalisation that IS sound: a closure writes `FB_X|A.B` for what a submission writes
        // as `FB_X.A.B`. Same root, separator only.
        var (exit, output, _) = Derive(
            submission: GateCliTests.Good.Replace(
                """{ "owner": "DemoUnit", "path": "Demo_Count" }""",
                """{ "owner": "DemoUnit", "path": "FB_Demo.IO.Demo_Count" }""",
                StringComparison.Ordinal),
            reachable: """{ "blocks": [ { "block": "FB_Demo", "reachableState": [ "FB_Demo|IO.Demo_Count" ] } ] }""");

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("COMPUTED   " + DerivableField.Storage, output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 1.4 — hashed over bytes, and it says so
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void WITH_A_BYTE_READER_THE_HASH_IS_TAKEN_OVER_BYTES_AND_THE_RECORD_SAYS_SO()
    {
        var (exit, output, written) = Derive(withBytes: true);

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("[bytes]", output, StringComparison.Ordinal);

        var derived = SubmissionDocument.Read(written["derived.json"]);
        Assert.All(derived.Derivation!.Where(d => !string.IsNullOrEmpty(d.Artifact)), d => Assert.True(d.HashedOverBytes));
    }

    [Fact]
    public void AND_WITHOUT_ONE_IT_FALLS_BACK_TO_TEXT_AND_SAYS_THAT_INSTEAD()
    {
        // The weaker check is never silently substituted: a text hash cannot see a change below the text
        // layer, and a reader has to be able to tell which one they got.
        var (exit, output, written) = Derive(withBytes: false);

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("[text]", output, StringComparison.Ordinal);

        var derived = SubmissionDocument.Read(written["derived.json"]);
        Assert.All(derived.Derivation!.Where(d => !string.IsNullOrEmpty(d.Artifact)), d => Assert.False(d.HashedOverBytes));
    }

    [Fact]
    public void A_BYTE_STAMPED_RECORD_IS_RE_HASHED_OVER_BYTES_BY_THE_GATE_TOO()
    {
        // 🔴 *** THE CROSS-HASH HAZARD, PINNED. *** A record stamped over BYTES compared against a hash
        // taken over TEXT can never match, so mixing the two would report every derived field as STALE -
        // a gate refusing everything for a reason having nothing to do with the submission. Caught while
        // wiring the byte reader, before it shipped.
        var (exit, output, written) = Derive(withBytes: true);
        Assert.True(exit == DeriveExit.Derived, output);

        var writer = new StringWriter();
        var gateExit = GateCli.Run(
            new[] { "check", "derived.json", "--binding", "binding.json" }, writer,
            path => Files(path, written),
            path => System.Text.Encoding.UTF8.GetBytes(Files(path, written)));

        Assert.True(gateExit == GateExit.AdmissibleSubjectToJudgement, writer.ToString());
    }

    [Fact]
    public void AND_WITHOUT_A_BYTE_READER_THE_GATE_SAYS_IT_COULD_NOT_VERIFY_rather_than_calling_it_stale()
    {
        // "I cannot check this the way it was stamped" is NOT CHECKED. Calling it stale would blame the
        // submission for the caller's missing capability.
        var (_, _, written) = Derive(withBytes: true);

        var writer = new StringWriter();
        var gateExit = GateCli.Run(
            new[] { "check", "derived.json", "--binding", "binding.json" }, writer,
            path => Files(path, written));

        Assert.Equal(GateExit.NotAdmissible, gateExit);
        Assert.Contains("NOT CHECKED", writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("CHANGED since", writer.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // 1.2 — the by-rule field
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void COMPRESSION_ABOVE_ONE_WITH_NO_DECLARED_BOUNDS_IS_REFUSED()
    {
        // An unbacked compression factor is exactly the typed number this whole mechanism exists to
        // stop: it is a claim about how far the plant's timing was squeezed, with nothing behind it.
        var (exit, output, written) = Derive(
            submission: GateCliTests.Good.Replace("\"runtimeCompression\": 1", "\"runtimeCompression\": 4", StringComparison.Ordinal));

        Assert.Equal(DeriveExit.Refused, exit);
        Assert.Contains("no blockCompression", output, StringComparison.Ordinal);
        Assert.Empty(written);
    }

    [Fact]
    public void AND_UNCOMPRESSED_NEEDS_NO_BOUNDS_AT_ALL()
    {
        var (exit, output, _) = Derive();

        Assert.True(exit == DeriveExit.Derived, output);
        Assert.Contains("BY RULE    " + DerivableField.RuntimeCompression, output, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------------------------------
    // fixtures
    // ---------------------------------------------------------------------------------------------

    private const string Binding = """
    {
      "declaredBy": "agent-k",
      "slots": [{
        "slotId": "S0",
        "vectorTargets": [{ "tag": "Demo_Step", "type": "Int" }],
        "startCondition": "Demo_Start",
        "resultSources": [{ "tag": "Demo_Count", "type": "Int", "specName": "Demo_Count" }]
      }]
    }
    """;

    private const string Reachable = """
    { "blocks": [ { "block": "DemoUnit", "reachableState": [ "Demo_Count", "Demo_Done" ] } ] }
    """;

    private const string Conflicts = """{ "edges": [] }""";
    private const string Deploy = """{ "outcome": "Ran" }""";

    /// <summary>The artifact set as the GATE sees it, including the deriver's own output.</summary>
    private static string Files(string path, IReadOnlyDictionary<string, string> written) => path switch
    {
        "derived.json" => written["derived.json"],
        "binding.json" => Binding,
        "reachable.json" => Reachable,
        "conflicts.json" => Conflicts,
        "deploy.json" => Deploy,
        "tags.json" => GateCliTests.TagMap,
        _ => throw new FileNotFoundException(path),
    };

    private static (int Exit, string Output, Dictionary<string, string> Written) Derive(
        string? submission = null,
        string? binding = null,
        string? reachable = null,
        string? conflicts = null,
        string? deploy = null,
        bool withBytes = false)
    {
        var sub = submission ?? GateCliTests.Good;
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sub.json"] = sub,
            ["binding.json"] = binding ?? Binding,
            ["reachable.json"] = reachable ?? Reachable,
            ["conflicts.json"] = conflicts ?? Conflicts,
            ["deploy.json"] = deploy ?? Deploy,
            ["tags.json"] = GateCliTests.TagMap,
        };

        var args = new List<string> { "derive", "--submission", "sub.json", "--out", "derived.json" };
        foreach (var field in DerivableField.All)
        {
            var path = DerivationProducer.KindFor(DeriveCli.DefaultProducerFor(field)) switch
            {
                ArtifactKind.Binding => "binding.json",
                ArtifactKind.ReachableState => "reachable.json",
                ArtifactKind.ConflictGraph => "conflicts.json",
                ArtifactKind.DeployResult => "deploy.json",
                ArtifactKind.TagMap => "tags.json",
                _ => null,
            };

            if (path is not null)
            {
                args.Add("--artifact");
                args.Add($"{field}={path}");
            }
        }

        var writer = new StringWriter();
        var written = new Dictionary<string, string>(StringComparer.Ordinal);

        var exit = DeriveCli.Run(
            args, writer,
            path => files.TryGetValue(path, out var c) ? c : throw new FileNotFoundException(path),
            (path, content) => written[path] = content,
            withBytes
                ? path => files.TryGetValue(path, out var c)
                    ? System.Text.Encoding.UTF8.GetBytes(c)
                    : throw new FileNotFoundException(path)
                : null);

        return (exit, writer.ToString(), written);
    }
}
