using Harness.Map;

namespace Harness.Run;

/// <summary>
/// 🔴 <b>The program under test, read off disk as IR — the input <c>harness-run</c> had no way to take.</b>
///
/// <para>*** IT WAS <c>Array.Empty&lt;HarnessObject&gt;()</c>, HARD-CODED, WITH NO FLAG BEHIND IT. *** Two
/// consequences, and neither of them raised anything: the <b>build stamp</b> is a hash of what is about to
/// run, so a stamp taken over no program <i>cannot match a rig carrying the block under test</i> — the
/// verify path would refuse every healthy device and a deploy path would publish a stamp naming a program
/// nobody loaded; and <c>LoopRun.ManifestOf</c> returns <c>NotAvailable</c> over an empty downloadable set,
/// which puts a permanent caveat on every result package. <i>A manifest says what TIA reported sending;
/// the stamp says what is EXECUTING.</i></para>
///
/// <para><b>Every failure here is a REFUSAL NAMING THE FILE, never a skip.</b> A file quietly left out of
/// the set is a file left out of the stamp, and the version register would then confirm a build that is
/// not the one running — which is the single thing the stamp exists to prevent. So an unreadable file, a
/// header this loader cannot classify, and two files declaring one object name are all hard errors.</para>
///
/// <para><b>It classifies from the IR's own header line, not from the filename.</b> The filename is what
/// the deploy path <i>writes</i> (<c>OpennessDeviceGateway</c> emits <c>&lt;Name&gt;.ir</c>), so reading
/// the name back out of it would be this tool agreeing with itself; the header is the artifact.</para>
/// </summary>
public static class ProgramUnderTest
{
    /// <summary>
    /// Load every declared path. <paramref name="expand"/> turns a directory into its <c>.ir</c> files and
    /// leaves anything else alone, so the whole loader is exercisable without a filesystem.
    /// </summary>
    /// <exception cref="InvalidDataException">
    /// A path that expanded to nothing, a header that could not be classified, or a duplicate object name.
    /// </exception>
    public static IReadOnlyList<HarnessObject> Load(
        IReadOnlyList<string> paths,
        Func<string, string> readFile,
        Func<string, IReadOnlyList<string>> expand)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(readFile);
        ArgumentNullException.ThrowIfNull(expand);

        var objects = new List<HarnessObject>();
        var seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in paths)
        {
            var files = expand(path);

            // *** A PATH THAT NAMED NOTHING IS A REFUSAL. *** An empty directory passed as `--program`
            // otherwise contributes nothing, silently, and the run proceeds with a stamp over a program
            // the operator believes is in it. Empty is not clean.
            if (files.Count == 0)
            {
                throw new InvalidDataException(
                    $"'{path}' expanded to no .ir file. That is a refusal rather than a contribution of nothing: a program the "
                    + "operator believes is under test, silently absent from the build stamp, is a stamp that names the wrong build.");
            }

            foreach (var file in files)
            {
                var ir = readFile(file);
                var obj = Classify(file, ir);

                if (seen.TryGetValue(obj.Name, out var first))
                {
                    throw new InvalidDataException(
                        $"'{file}' declares object '{obj.Name}', which '{first}' already declared. Two files for one object cannot both be "
                        + "downloaded, and the stamp would hash both — so which one is running would not be recoverable from the stamp that "
                        + "confirmed it. Names are compared case-insensitively, because TIA resolves them that way.");
                }

                seen[obj.Name] = file;
                objects.Add(obj);
            }
        }

        return objects;
    }

    /// <summary>
    /// One file's kind and name, from the IR's first meaningful line.
    ///
    /// <para>The four top-level forms <c>ir/SPEC.md</c> declares are <c>BLOCK &lt;FB|FC|OB&gt; &lt;Name&gt;</c>,
    /// <c>DB &lt;Name&gt;</c>, <c>TYPE &lt;Name&gt;</c> and <c>TAGTABLE &lt;Name&gt;</c>. <b>A tag table's
    /// name may contain spaces</b> (a real export reads <c>TAGTABLE Default tag table</c>), so it is the
    /// rest of the line rather than the next token.</para>
    ///
    /// <para><b><c>TYPE</c> is refused rather than mapped onto a kind.</b> <see cref="HarnessObjectKind"/>
    /// has three members and none of them is a PLC data type; picking the nearest would make
    /// <c>RetentionCheck</c> apply a data block's rules to a UDT and would put it in the downloadable set
    /// that the load-manifest comparison uses. The refusal names it, which is a build-list item rather
    /// than a silent misclassification.</para>
    /// </summary>
    private static HarnessObject Classify(string file, string ir)
    {
        var header = (ir ?? string.Empty)
            .Split('\n')
            .Select(l => l.TrimEnd('\r').Trim())
            .FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));

        if (header is null)
        {
            throw new InvalidDataException(
                $"'{file}' holds no IR at all. An empty file is not an object that contributes nothing to the stamp; it is a file "
                + "somebody expected to carry a program.");
        }

        var parts = header.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        switch (parts[0])
        {
            case "BLOCK" when parts.Length >= 3:
                return new HarnessObject(parts[2], HarnessObjectKind.Block, ir!);

            case "DB" when parts.Length >= 2:
                return new HarnessObject(parts[1], HarnessObjectKind.DataBlock, ir!);

            case "TAGTABLE" when parts.Length >= 2:
                // The REST of the line: a real tag table is called `Default tag table`.
                return new HarnessObject(header["TAGTABLE".Length..].Trim(), HarnessObjectKind.TagTable, ir!);

            // ✅ A PLC DATA TYPE. This was a REFUSAL until 2026-08-14, correctly — HarnessObjectKind had no
            // member for one, and mapping it onto DataBlock would have handed it a data block's retention
            // rules AND put it in the downloadable set the load manifest is compared against.
            //
            // *** BUT THE REFUSAL BLOCKED THE DELIVERABLE FROM BEING SUPPLIED AT ALL: *** the hopper
            // program's FB and its instance DB both reference UDT_HopperBlockageIO, so `--program` could
            // not name the program under test whole, and the BUILD STAMP is a hash of what is about to
            // run. The fix is the missing member, not a nearest-fit — see HarnessObjectKind.DataType for
            // which side of each line a type sits on.
            case "TYPE" when parts.Length >= 2:
                return new HarnessObject(parts[1], HarnessObjectKind.DataType, ir!);

            default:
                throw new InvalidDataException(
                    $"'{file}' begins `{header}`, which is not one of the IR top-level forms this loader reads: `BLOCK <FB|FC|OB> <Name>`, "
                    + "`DB <Name>`, `TYPE <Name>` or `TAGTABLE <Name>`. It is refused rather than skipped: a file silently left out of the "
                    + "program set is left out of the BUILD STAMP too, and the version register would then confirm a build that is not the "
                    + "one running.");
        }
    }
}
