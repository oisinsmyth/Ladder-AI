using Harness.Map;

namespace Harness.Device;

/// <summary>What one step of a deployment is for. The kind decides how its exit code is read.</summary>
public enum DeviceStepKind
{
    /// <summary><c>converter to-xml</c> — IR to SimaticML. Never touches Portal.</summary>
    Convert,

    /// <summary><c>openness-cli import-all</c> — the bulk restore, kind read from each file's root element.</summary>
    Import,

    /// <summary>
    /// <c>openness-cli block-layout --set Standard --yes</c>. <b>The repair, not the check</b> — and it
    /// is required after EVERY import of a data block, not once.
    /// </summary>
    LayoutSet,

    /// <summary><c>openness-cli block-layout --expect Standard</c>. The gate that makes the repair provable.</summary>
    LayoutExpect,

    /// <summary><c>openness-cli compile-all</c> — the bulk half of hard rule 4's gate.</summary>
    CompileAll,

    /// <summary><c>openness-cli sanity-check</c> — block AND type consistency plus compile health.</summary>
    SanityCheck,

    /// <summary><c>download-probe</c> — the only binary in the repo that can transfer a program.</summary>
    Download,
}

/// <summary>How a step's exit code was read. Three values, and the third is the one that matters.</summary>
public enum StepVerdict
{
    /// <summary>The step did what it says it did.</summary>
    Ok,

    /// <summary>The step ran and reported a problem.</summary>
    Failed,

    /// <summary>
    /// The step ran, reported no error, and <b>examined nothing</b> — or could not be run at all.
    /// Deliberately not <see cref="Ok"/>: empty is not clean (FI-44), and this is the state that
    /// looks most like a pass.
    /// </summary>
    NotProven,
}

/// <summary>One external invocation, with its argument vector and what it is for.</summary>
public sealed record DeviceStep(
    DeviceStepKind Kind,
    string Executable,
    IReadOnlyList<string> Arguments,
    string Purpose)
{
    /// <summary>The command as a rig operator would type it.</summary>
    public string CommandLineText => CommandLine.Render(Executable, Arguments);
}

/// <summary>The ordered steps of one deployment, or every reason there are none.</summary>
public sealed record DeploymentPlan(
    IReadOnlyList<DeviceStep> Steps,
    IReadOnlyList<HarnessObject> DownloadableObjects,
    IReadOnlyList<string> DataBlockNames,
    IReadOnlyList<string> Refusals,
    string LayoutNote)
{
    public bool Planned => Refusals.Count == 0;

    /// <summary>
    /// Build the plan. <b>Pure</b> — no filesystem, no process, no Portal — so the argument vectors a
    /// rig session will execute are the ones a unit test asserts.
    /// </summary>
    public static DeploymentPlan For(IReadOnlyList<HarnessObject> objects, DeviceGatewayOptions options)
    {
        ArgumentNullException.ThrowIfNull(objects);
        ArgumentNullException.ThrowIfNull(options);

        var refusals = options.Refusals.ToList();

        if (objects.Count == 0)
        {
            refusals.Add(
                "no objects were supplied, so there is nothing to deploy. A download of nothing would still stop the CPU and would still report a state; refusing is the only reading that is not a false green.");
        }

        var names = objects.Select(o => o.Name).ToArray();
        foreach (var duplicate in names.GroupBy(n => n, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            refusals.Add(
                $"two objects are called '{duplicate.Key}'. They would be written to the same staged file name and one would silently replace the other.");
        }

        // A tag table is not a downloadable object and never appears in a load manifest. Naming that
        // here — rather than letting the manifest comparison quietly fail — is the whole point of
        // carrying the set separately.
        //
        // A PLC DATA TYPE is on the same side of this one line and the other side of the import one: it IS
        // staged, converted, imported and compiled (types first), and it produces NO load message. Both
        // halves matter — leaving it out of the deployment would drop it from the download, and leaving it
        // IN the manifest comparison would report Absent on every healthy download that carries a UDT.
        var downloadable = objects
            .Where(o => o.Kind is not (HarnessObjectKind.TagTable or HarnessObjectKind.DataType))
            .ToArray();

        if (downloadable.Length == 0 && objects.Count > 0)
        {
            refusals.Add(
                "no object supplied produces a load message — every one is a tag table or a PLC data type. Both are imported and "
                + "compiled and neither appears in a load manifest, so a download of this set could produce no positive evidence of transfer at all.");
        }

        var dataBlocks = objects.Where(o => o.Kind == HarnessObjectKind.DataBlock).Select(o => o.Name).ToArray();

        if (refusals.Count > 0)
        {
            return new DeploymentPlan(
                Array.Empty<DeviceStep>(), downloadable, dataBlocks, refusals,
                "no plan was built, so no layout assertion was scheduled.");
        }

        var steps = new List<DeviceStep>
        {
            Convert(objects, options),
            Import(options),
        };

        // ---- THE LAYOUT RE-ASSERTION, IMMEDIATELY AFTER THE IMPORT THAT BREAKS IT ----------------
        //
        // A re-import reverts a block to Optimized (measured 2026-08-11): the exported .xml carries no
        // MemoryLayout element, so the import states no opinion and TIA applies the S7-1200 default.
        // `Normalizer` ignores the attribute, so `drift-check` is blind in BOTH directions. An
        // optimized DB is not an error on the wire — it is simply ABSENT to classic S7comm, and the
        // read fails at the first DATA access rather than at connect, which presents as wiring.
        //
        // `--expect` only tells you it broke; `--set` is what repairs it. So both, in that order,
        // per data block, after EVERY import.
        foreach (var db in dataBlocks)
        {
            steps.Add(LayoutSet(db, options));
            steps.Add(LayoutExpect(db, options));
        }

        steps.Add(CompileAll(options));
        steps.Add(SanityCheck(options));
        steps.Add(Download(options));

        return new DeploymentPlan(steps, downloadable, dataBlocks, Array.Empty<string>(), DescribeLayout(dataBlocks));
    }

    /// <summary>
    /// What the layout re-assertion did or did not have to work on — <b>stated on every plan,
    /// including the ones where it was a no-op</b>.
    ///
    /// <para>A guard that reports only when it fires teaches a reader that its silence means it ran
    /// clean, and this one is currently a no-op on every wave the harness generates: the mirror lives
    /// in <c>%MW</c> bit memory precisely BECAUSE a DB-backed mirror is one re-import away from being
    /// invisible (spec §6, <c>MirrorGeometry</c>). So the sequence exists for the program under test,
    /// which may carry a DB, and the note says which case this run was.</para>
    /// </summary>
    private static string DescribeLayout(IReadOnlyList<string> dataBlocks) =>
        dataBlocks.Count == 0
            ? "NO DATA BLOCKS in this deployment, so the Standard-layout re-assertion had nothing to apply to. That is the expected case: the harness mirror is %MW bit memory, not a DB, exactly so that a re-import cannot silently revert it to Optimized. It is NOT evidence that the re-assertion works."
            : $"{dataBlocks.Count} data block(s) — {string.Join(", ", dataBlocks)} — each re-asserted to Standard and then gated with --expect, AFTER the import that reverts them to Optimized.";

    // ---------------------------------------------------------------------------------------------
    // The argument vectors. Every flag here is one the target binary's own parser has a case arm for;
    // ArgumentVocabularyTests reads those parsers and asserts it.
    // ---------------------------------------------------------------------------------------------

    private static DeviceStep Convert(IReadOnlyList<HarnessObject> objects, DeviceGatewayOptions options)
    {
        var args = new List<string> { "to-xml" };

        // Every file in ONE invocation: the converter builds its callee and tag-type registries from
        // the whole batch, so the copy layer's references into its own tag table resolve without
        // --allow-blind-types. `to-xml` FAILS CLOSED on an unresolved member type (FI-71) and that
        // refusal is wanted — a guessed member type is rejected by TIA at compile, after a round trip.
        args.AddRange(objects.Select(o => Path.Combine(options.IrStagingDirectory, o.Name + ".ir")));

        if (!string.IsNullOrWhiteSpace(options.IrProjectDirectory))
        {
            args.Add("--project");
            args.Add(options.IrProjectDirectory!);
        }

        // FI-72: without --out the result lands BESIDE THE INPUT. Here that would be harmless, but
        // naming the destination is what lets import-all be handed a directory containing the XML and
        // nothing else.
        args.Add("--out");
        args.Add(options.XmlStagingDirectory);

        return new DeviceStep(DeviceStepKind.Convert, options.ConverterExe, args,
            "IR to SimaticML. No Portal, no TIA — this step is free and safe at any time.");
    }

    private static DeviceStep Import(DeviceGatewayOptions options)
    {
        // import-all rather than import: `import` takes its files as ONE kind, in the order given, and
        // stops at the first failure. A harness deployment is a MIXED set (tag table + block, and a DB
        // whenever the program under test has one) in a dependency order not derivable from filenames.
        // import-all reads the kind from each file's SimaticML root element and retries to a fixpoint.
        var args = new List<string>
        {
            "import-all",
            options.ProjectPath,
            "--group", options.GroupPath,
            options.XmlStagingDirectory,
            "--timeout-connect", options.TimeoutConnectSeconds.ToString(),
            "--timeout-open", options.TimeoutOpenSeconds.ToString(),
        };

        return new DeviceStep(DeviceStepKind.Import, options.OpennessCliExe, args,
            "Bulk import. NOT a compile gate: everything imported is flagged inconsistent, so the gate follows.");
    }

    private static DeviceStep LayoutSet(string block, DeviceGatewayOptions options)
    {
        var args = new List<string> { "block-layout", options.ProjectPath, "--block", block, "--set", "Standard" };
        AddDevice(args, options);
        args.Add("--yes");

        return new DeviceStep(DeviceStepKind.LayoutSet, options.OpennessCliExe, args,
            "Re-assert Standard block access. REQUIRED after every import, not precautionary: the import reverts it to Optimized and nothing else in the toolchain can see that happen.");
    }

    private static DeviceStep LayoutExpect(string block, DeviceGatewayOptions options)
    {
        var args = new List<string> { "block-layout", options.ProjectPath, "--block", block, "--expect", "Standard" };
        AddDevice(args, options);

        return new DeviceStep(DeviceStepKind.LayoutExpect, options.OpennessCliExe, args,
            "Gate the re-assertion. Exit 15 means the block is not Standard and a classic-S7comm read of it would find it simply ABSENT.");
    }

    private static DeviceStep CompileAll(DeviceGatewayOptions options)
    {
        var args = new List<string> { "compile-all", options.ProjectPath };
        AddDevice(args, options);
        args.Add("--timeout-connect");
        args.Add(options.TimeoutConnectSeconds.ToString());
        args.Add("--timeout-open");
        args.Add(options.TimeoutOpenSeconds.ToString());

        return new DeviceStep(DeviceStepKind.CompileAll, options.OpennessCliExe, args,
            "Hard rule 4, bulk half. FI-52: only a per-item compile clears an imported block's inconsistent flag. Keys on ERRORS, never on State.");
    }

    private static DeviceStep SanityCheck(DeviceGatewayOptions options)
    {
        var args = new List<string>
        {
            "sanity-check",
            options.ProjectPath,
            "--timeout-connect", options.TimeoutConnectSeconds.ToString(),
            "--timeout-open", options.TimeoutOpenSeconds.ToString(),
        };

        return new DeviceStep(DeviceStepKind.SanityCheck, options.OpennessCliExe, args,
            "Block AND type consistency (FI-62) plus compile health. Catches the per-item converse a clean per-block compile does not.");
    }

    private static DeviceStep Download(DeviceGatewayOptions options)
    {
        var args = new List<string>
        {
            options.ProjectPath,
            "--options", options.DownloadOption.ToString(),
        };

        if (!string.IsNullOrWhiteSpace(options.Device))
        {
            args.Add("--device");
            args.Add(options.Device!);
        }

        args.Add("--pc-interface");
        args.Add(options.PcInterface);

        if (!string.IsNullOrWhiteSpace(options.TargetInterface))
        {
            args.Add("--target");
            args.Add(options.TargetInterface!);
        }

        args.Add("--log-dir");
        args.Add(options.ResolvedProbeLogDirectory);

        // --disruptive is the only flag that lets the download COMPLETE. Options.Refusals has already
        // made AllowCpuStop=false unplannable, so reaching here means the caller stated it.
        args.Add("--disruptive");

        // --json puts the machine-readable report on stdout ALONE and pushes the verbatim log to
        // stderr; the embedded `log` array is what the manifest is recovered from.
        args.Add("--json");

        args.Add("--timeout-connect");
        args.Add(options.TimeoutConnectSeconds.ToString());
        args.Add("--timeout-open");
        args.Add(options.TimeoutOpenSeconds.ToString());

        return new DeviceStep(DeviceStepKind.Download, options.DownloadProbeExe, args,
            "The real download. DEVICE-LEVEL: there is no per-block download, so this transfers the whole PLC software. EXPECT THE CPU TO BE LEFT STOPPED.");
    }

    private static void AddDevice(List<string> args, DeviceGatewayOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Device))
            return;

        args.Add("--device");
        args.Add(options.Device!);
    }
}
