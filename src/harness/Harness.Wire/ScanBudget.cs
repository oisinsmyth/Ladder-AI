namespace Harness.Wire;

/// <summary>
/// The compression factor a wave <b>actually runs at</b> — X-D's <c>comp</c>, as a value that cannot be
/// absent and cannot be zero.
///
/// <para><b>It is a reference type on purpose.</b> <c>default(RuntimeCompression)</c> is <c>null</c>, which
/// throws at the point of use; a struct would have handed every caller who forgot it a factor of 0, and a
/// factor of 0 divides. That is the same shape as <c>AssertionForm.Unstated = 0</c> — <b>the zero value
/// must be unusable, not permissive.</b></para>
///
/// <para><b>Why compression has a type at all, and it is the whole reason this file exists:</b> the spec's
/// §2 note, restated at X-D — <i>a scan count is meaningless without the compression factor it was stated
/// at. A 20-scan window at <c>comp = 1</c> is a 2-scan window at <c>comp = 10</c>, crossing the
/// observability floor with nobody editing the vector.</i> A declaration that was sound when written is
/// void when it runs, and nothing about the vector changed.</para>
/// </summary>
public sealed record RuntimeCompression(int Factor)
{
    /// <summary>The factor. <b>Below 1 is not a compression factor</b> and is refused at construction.</summary>
    public int Factor { get; } = Factor >= 1
        ? Factor
        : throw new ArgumentOutOfRangeException(nameof(Factor), Factor, "a compression factor below 1 is not a compression factor. X-D's rule is USE comp_min, and the least comp_min can be is 1.");

    /// <summary>No compression: plant time and real time run at the same rate.</summary>
    /// <remarks>
    /// Named rather than written as <c>new RuntimeCompression(1)</c> at forty call sites, and named
    /// <b>Uncompressed</b> rather than <c>None</c> — "none" reads as <i>no factor was declared</i>, which is
    /// exactly the state this type exists to make unrepresentable.
    /// </remarks>
    public static readonly RuntimeCompression Uncompressed = new(1);

    public override string ToString() => $"comp={Factor}";
}

/// <summary>
/// A count of scans <b>and the compression factor it was stated at</b>, inseparably.
///
/// <para><b>THIS TYPE EXISTS SO THAT A BARE SCAN COUNT CANNOT BE PASSED ANYWHERE THAT WILL BOUND
/// SOMETHING.</b> Both of the harness's scan-shaped declarations — X-B's per-test maximum duration and
/// §4.3's sampled observation window — are stated in scans at the author's compression factor, and both are
/// consumed at the factor the wave actually runs. Consuming either without re-expressing it is a silent
/// error in one of two directions:</para>
///
/// <list type="bullet">
/// <item><b>The window</b> shrinks as <c>comp</c> RISES, and crossing the observability floor produces a
/// <i>missed assertion reported as a pass</i> — the most expensive outcome this design has.</item>
/// <item><b>The duration</b> grows as <c>comp</c> FALLS, and a backstop computed on the declared figure
/// fires on a healthy test and reports TIMED-OUT — which is believed, because it is meant to be rare.</item>
/// </list>
///
/// <para><b>They are the same arithmetic and they are computed here once</b>, so the timeout and the
/// observability check cannot disagree about what a vector's scan counts mean. The precedent is
/// <c>MirrorClient</c>'s split read: an operation that must not happen is best made
/// <i>unaddressable</i> rather than validated, because a validator can be skipped by a later change and a
/// missing overload cannot.</para>
/// </summary>
/// <param name="Scans">The declared count. Below 1 is not a declaration.</param>
/// <param name="DeclaredCompression">The <c>comp</c> the count was stated at.</param>
public sealed record ScanBudget(int Scans, int DeclaredCompression)
{
    public int Scans { get; } = Scans >= 1
        ? Scans
        : throw new ArgumentOutOfRangeException(nameof(Scans), Scans, "a scan count below 1 is not a declaration. Undeclared is not exempt: the caller must decide what an absent declaration means before it reaches the arithmetic.");

    public int DeclaredCompression { get; } = DeclaredCompression >= 1
        ? DeclaredCompression
        : throw new ArgumentOutOfRangeException(nameof(DeclaredCompression), DeclaredCompression, "a scan count with no compression factor is meaningless — that is the whole reason this type pairs them.");

    /// <summary>
    /// The same declaration re-expressed at the factor the wave will run: <c>scans x declared / runtime</c>.
    /// </summary>
    public double At(RuntimeCompression runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        return Scans * (double)DeclaredCompression / runtime.Factor;
    }

    /// <summary>
    /// <see cref="At"/> <b>rounded UP</b>, for use where the figure is a BOUND.
    ///
    /// <para>A bound that is a little too generous costs nothing — §12a derivation 4: a timeout only ever
    /// elapses on a request that has already failed. A bound that is a little too tight produces a spurious
    /// TIMED-OUT, which is worse than a spurious FAILED because it is believed. So the rounding is not a
    /// convenience, it is the direction the asymmetry points.</para>
    /// </summary>
    public int BoundAt(RuntimeCompression runtime) => (int)Math.Ceiling(At(runtime));

    /// <summary>The declaration in PLANT scans — what it would be at <c>comp = 1</c>. X-D's <c>T_event</c>, in scans.</summary>
    public double PlantScans => Scans * (double)DeclaredCompression;

    /// <summary>The declaration in PLANT milliseconds, at the loaded scan period. X-D's <c>T_event</c>.</summary>
    public double PlantMs => PlantScans * WireTiming.ScanPeriodMs;

    public override string ToString() => $"{Scans} scan(s) at comp={DeclaredCompression}";
}
