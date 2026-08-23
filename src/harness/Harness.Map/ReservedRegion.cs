namespace Harness.Map;

/// <summary>
/// A run of holding registers inside the declared Modbus area that <b>something other than the mirror
/// already occupies</b>, and the name of whoever occupies it.
///
/// <para>🔴 <b>Why this type exists — measured 2026-08-23, live on the rig.</b> A generated mirror and a
/// hand-authored "virtual panel" both claimed registers 256–323 of the same <c>%M</c> area.
/// <b>53 panel tags collided bit for bit</b>, among them the panel's master enable and a
/// safety-healthy substitution, both of which the copy layer overwrote every scan from unrelated fault
/// flags. The panel's own comment said it was inert until a PC enabled it; in the deployed program that
/// was false. It was safe only because the rig cannot actuate.</para>
///
/// <para><b>Nothing caught it, and the reason is worth stating exactly, because it is not "a check was
/// missing".</b> <see cref="MapAllocator"/> bounds the mirror against
/// <see cref="MirrorGeometry.DeclaredRegisters"/> — the whole area — and <see cref="RegisterMap"/>
/// proves the mirror's regions disjoint <i>from each other</i>. Both ran. Both passed. Both examined
/// something real that was not the thing at risk: <b>a CLOSED check, which is never empty, never
/// silent, and looks healthiest exactly when it is wrong.</b> The mirror's extent is computed — roughly
/// 165 registers a lane — so it grew past a band nobody had written down, on the day a batch went from
/// one lane to two.</para>
///
/// <para><b>Registers, not bytes.</b> <see cref="Register"/> is numbered from the area base, the same
/// numbering as <see cref="MirrorGeometry.DeclaredRegisters"/> and every range in
/// <see cref="RegisterMap"/> — <i>not</i> a <c>%M</c> byte address. A neighbour is normally known by its
/// <c>%M</c> addresses, so the conversion is the likeliest way to get this silently wrong and is
/// therefore derived rather than typed: see <see cref="MirrorGeometry.ReservingBytes"/>.</para>
/// </summary>
/// <param name="Register">First holding register of the region, numbered from the area base.</param>
/// <param name="Length">How many registers it covers. At least one — see <see cref="Refusals"/>.</param>
/// <param name="Owner">
/// Who holds it, in the words a reader would search for — <c>"virtual panel command band"</c>, not
/// <c>"reserved"</c>. A refusal that cannot say WHOSE space was hit sends the reader looking in the
/// wrong place, which is most of what went wrong above, so this is required and a blank is refused.
/// </param>
public sealed record ReservedRegion(int Register, int Length, string Owner)
{
    /// <summary>One past the last register in the region.</summary>
    public int End => Register + Length;

    /// <summary>The region as a <see cref="RegisterRange"/>, so it prints and overlaps like every other range.</summary>
    public RegisterRange Range => new(Register, Length);

    /// <summary>How the region names itself in a refusal: owner first, because that is what the reader needs.</summary>
    public string Describe() => $"'{Owner}' {Range}";

    /// <summary>
    /// Everything wrong with this reservation, or empty.
    ///
    /// <para><b>Each of these is refused rather than skipped, and that is the whole difference between
    /// this guard and no guard.</b> A malformed reservation that is quietly ignored leaves a caller who
    /// declared protection with none — the same shape as the defect the type exists to prevent, one
    /// level up. Empty is not clean (FI-44).</para>
    /// </summary>
    public IReadOnlyList<string> Refusals
    {
        get
        {
            var refusals = new List<string>();

            if (Length <= 0)
            {
                refusals.Add($"reserved region '{Owner}' is {Length} register(s) long. A reservation of nothing protects nothing — and a caller who declared it believes some part of the area is off limits, so it is refused rather than skipped.");
            }

            if (Register < 0)
            {
                refusals.Add($"reserved region '{Owner}' starts at register {Register}. Reserved regions are numbered in HOLDING REGISTERS from the area base — the same numbering as DeclaredRegisters — never in %M byte addresses; a negative start is either that mix-up or a band lying below the mirror's own base. Use MirrorGeometry.ReservingBytes to derive the register numbers from %M addresses.");
            }

            if (string.IsNullOrWhiteSpace(Owner))
            {
                refusals.Add($"the reserved region {Range} declares no owner. The owner label is the point of the check: a refusal that cannot say whose space was hit sends the reader to the mirror, which is the one place the problem is not.");
            }

            return refusals;
        }
    }

    /// <summary>
    /// True when this region and <paramref name="other"/> share at least one register.
    /// A zero-length range touches nothing — but a zero-length RESERVATION is refused above, so this
    /// can only be reached by an empty MIRROR region, which is a region the mirror does not use.
    /// </summary>
    public bool Overlaps(RegisterRange other) =>
        Length > 0 && other.Length > 0 && Register < other.End && other.Register < End;

    /// <summary>The registers this region and <paramref name="other"/> both cover. Length zero when they do not.</summary>
    public RegisterRange OverlapWith(RegisterRange other)
    {
        var start = Math.Max(Register, other.Register);
        var end = Math.Min(End, other.End);
        return new RegisterRange(start, Math.Max(0, end - start));
    }
}
