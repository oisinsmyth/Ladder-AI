using System.Text.Json;
using System.Text.Json.Serialization;

namespace Harness.MirrorRead;

/// <summary>What the run concluded about the width. Four states, and only one of them is a pass.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MirrorWidthVerdict
{
    /// <summary>
    /// *** NO CONCLUSION. *** The fence refused, the session would not open, the arguments described no
    /// measurement, or a probe that had to answer did not. <b>Deliberately the default</b>: a report
    /// constructed and never filled in must not read as a pass.
    /// </summary>
    NotEstablished,

    /// <summary>Measured from both sides: the declared registers answered and the next one was refused.</summary>
    ExactlyAsDeclared,

    /// <summary>*** THE HEADLINE FAILURE. *** The server refused a register the claim says it serves.</summary>
    NarrowerThanDeclared,

    /// <summary>A register past the claimed edge answered.</summary>
    WiderThanDeclared,
}

/// <summary>One boundary probe, in the form the verdict actually reasons over.</summary>
/// <param name="Outcome">
/// <c>Ok</c>, <c>RefusedByServer</c> or <c>TransportFailed</c>. <b>Three values, never two</b> — a
/// refusal is a measurement and a silence is the absence of one, and a consumer that collapsed them
/// would read a dropped packet as proof of a boundary.
/// </param>
public sealed record MirrorProbeRow(int Register, bool Declared, string Outcome, byte? ExceptionCode);

/// <summary>One control read: what the copy layer published at registers 0..3, undecoded and decoded.</summary>
public sealed record MirrorControlReading(bool Ok, string? BuildStamp, uint? ScanCounter);

/// <summary>The pair of control reads and what separates them.</summary>
/// <param name="MeasuredIntervalMs">
/// The time that actually elapsed between the two, not the requested wait — the probes in between cost
/// round trips, and dividing by the requested figure would report a scan period wrong by however long
/// they took.
/// </param>
public sealed record MirrorControl(
    MirrorControlReading? First,
    MirrorControlReading? Second,
    long MeasuredIntervalMs,
    long? ScanAdvance);

/// <summary>
/// 🔴 <b>THE DENOMINATOR, PRESENT ON EVERY RUN INCLUDING THE ZERO CASE.</b>
///
/// <para>Same rule as <c>drift-check</c>'s <c>COMPARED:</c> line and the prose report's <c>EXAMINED</c>:
/// every other number in this document is a conclusion, and this is the one that says how much was
/// looked at. A pass over a small denominator is still a pass — but a reader has to be able to see which
/// one they got, and a refused run's zeroes are how they tell.</para>
/// </summary>
public sealed record MirrorReadExamined(
    int RegistersReadWhole,
    int Pages,
    int BoundaryProbes,
    int BoundaryFrom,
    int BoundaryTo);

/// <summary>Where the run was pointed.</summary>
public sealed record MirrorReadTarget(string Address, int Port, int Unit);

/// <summary>
/// 🔴 <b>ONE RUN OF <c>harness-mirror-read</c>, IN A FORM SOMETHING OTHER THAN A PERSON CAN READ.</b>
///
/// <para><b>Why it exists.</b> Until this record, the only structured output of this tool was its exit
/// code: the width, the denominator, the probe outcomes and every finding reached a reader as console
/// prose and reached a CALLER not at all. That was survivable while a person typed the width in by hand.
/// It stopped being survivable when a batch began feeding it a DERIVED width — the verdict is then
/// evidence about the deployment, and evidence that lives in scrollback is evidence nobody can cite.</para>
///
/// <para><b>The rule the shape is built around, and it is the only one that matters:</b> <i>a
/// measurement that did not happen must not read as one that passed.</i> So <see cref="Measured"/> is a
/// field rather than an inference, <see cref="WidthVerdict"/> defaults to
/// <see cref="MirrorWidthVerdict.NotEstablished"/>, <see cref="Examined"/> is present with zeroes on the
/// paths that examined nothing, and the fence-refused and connect-failed paths produce a report rather
/// than no report at all. A consumer that finds no file cannot tell a refusal from a run nobody
/// started.</para>
///
/// <para><b>Nothing here is computed for the report.</b> Every value is one <see cref="MirrorReadRun"/>
/// already had in hand to print its prose; this is a second rendering of the same measurement, not a
/// second measurement. Two derivations of one number is how this repository acquired its parity tests.</para>
/// </summary>
/// <param name="Measured">
/// <b>Whether the run opened a session and took readings at all.</b> It does NOT mean the width was
/// established — <see cref="WidthVerdict"/> is what says that, and a run can be <c>true</c> here and
/// <see cref="MirrorWidthVerdict.NotEstablished"/> there (a probe that timed out mid-sweep).
/// </param>
/// <param name="Attempts">
/// How many times the whole measurement was taken. More than one only ever means the scan counter had
/// not started moving yet — the just-restarted CPU a download leaves behind — and the count is REPORTED
/// because "asked once" and "asked twelve times over a minute" are different evidence about one device.
/// </param>
/// <param name="NotSeen">
/// What this measurement cannot see, carried WITH the verdict. A consumer holding a passing file must be
/// able to read the limits without going anywhere else; a caveat kept in the tool's documentation is a
/// caveat the artifact does not have.
/// </param>
public sealed record MirrorReadReport(
    MirrorReadTarget Target,
    int DeclaredRegisters,
    int LastDeclaredRegister,
    int Exit,
    string ExitName,
    MirrorWidthVerdict WidthVerdict,
    bool Measured,
    int Attempts,
    MirrorReadExamined Examined,
    IReadOnlyList<MirrorProbeRow> Probes,
    MirrorControl Control,
    IReadOnlyList<string> Findings,
    IReadOnlyList<string> NotSeen)
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,

        // camelCase, like every other machine-readable document this project emits — the converter's
        // --json output, the harness bindings, the result packages. A consumer switching between them
        // must not have to switch conventions.
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>The document, indented, because a person reads it too when the tool that should have is broken.</summary>
    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>
    /// 🔴 <b>A run that never reached the device — and it still produces a report.</b>
    ///
    /// <para>The examined counts are zero and <see cref="Measured"/> is false, which is the whole point:
    /// this is the state that most resembles a pass, and the file exists so a consumer can tell it from
    /// one rather than inferring it from a missing artifact.</para>
    /// </summary>
    public static MirrorReadReport NotMeasured(
        MirrorReadOptions options, MirrorReadExit exit, IReadOnlyList<string> findings) =>
        new(new MirrorReadTarget(options.Address, options.Port, options.UnitId),
            options.DeclaredRegisters,
            options.LastDeclaredRegister,
            (int)exit,
            exit.ToString(),
            MirrorWidthVerdict.NotEstablished,
            Measured: false,
            Attempts: 0,
            new MirrorReadExamined(0, 0, 0, options.BoundaryFrom, options.BoundaryTo),
            Array.Empty<MirrorProbeRow>(),
            new MirrorControl(null, null, 0, null),
            findings,
            NotSeenBy(options, pages: 0));

    /// <summary>
    /// What no run of this tool can see, plus the one limit that is specific to a paged read.
    ///
    /// <para>Written once, here, so the prose report and the document cannot drift into claiming
    /// different limits for the same measurement.</para>
    /// </summary>
    public static IReadOnlyList<string> NotSeenBy(MirrorReadOptions options, int pages)
    {
        var limits = new List<string>
        {
            "REACHABILITY, NOT CORRECTNESS: a register that answers is one a Modbus client can see. Nothing here says "
            + "its CONTENTS are what the map intends, and a mirror serving the right width of the wrong values passes "
            + "every check in this run.",

            "NOT WHETHER THE CLAIM IS THE RIGHT CLAIM: --declared-registers is tested AGAINST the device, and the "
            + "device agreeing with it says nothing about where the number came from. A width derived from a corpus "
            + "that is not the program on the controller can be confirmed here and still be the wrong question.",

            "NOT WHO ELSE OCCUPIES THE AREA: this asks the server how wide the window is. Which other objects write "
            + "into those %M bytes is a question about the program, answered over the corpus by `converter neighbours`, "
            + "and a clean run here is not evidence about it.",
        };

        if (pages > 1)
        {
            limits.Add($"NOT ONE SNAPSHOT: the declared area was read in {pages} transactions, which were {pages} "
                + "separate moments on a live mirror. The words are a reassembly; two registers from different pages "
                + "may never have held those values at the same instant.");
        }

        return limits;
    }
}

/// <summary>One run's exit code and the document describing how it got there.</summary>
public sealed record MirrorReadOutcome(MirrorReadExit Exit, MirrorReadReport Report);
