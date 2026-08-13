using Harness.Map;
using Harness.Wire;

namespace Harness.Results;

/// <summary>What a submission declared about one vector — everything the package needs that a run cannot supply.</summary>
/// <param name="AssertedBehaviours">The behaviours this vector's assertions depend on. Set-differenced against the model's declaration (M4).</param>
/// <param name="CompletionSignal">The block's own done-signal, so the settling declaration can be checked against it.</param>
/// <param name="ObservabilitySupported">Whether the transport can support the declared observability. False is a refusal, not a warning.</param>
public sealed record VectorDeclaration(
    string VectorId,
    Basis? Basis,
    FidelityDeclaration? Fidelity,
    SettlingDeclaration? Settling,
    IReadOnlyCollection<string> AssertedBehaviours,
    string CompletionSignal,
    string VectorAuthor,
    string BlockAuthor,
    bool ObservabilitySupported);

/// <summary>
/// Assembles DB-8's package from what a run produced and what the submission declared.
///
/// <para><b>The builder supplies no defaults.</b> Every not-conclusive state that can arise here has a
/// value in the enums, so nothing is left to a null that a reader would take for "fine".</para>
/// </summary>
public static class ResultPackageBuilder
{
    /// <summary>
    /// Build one vector's package.
    /// </summary>
    /// <param name="settling">
    /// Whether the declared settling condition was met. Required as a judgement the caller has made —
    /// <see cref="SettlingState.NotEstablished"/> is what to pass when nothing established it, and it is
    /// deliberately not the same as <see cref="SettlingState.Settled"/>.
    /// </param>
    public static ResultPackage Build(
        VectorDeclaration declaration,
        AssertionEnumeration enumeration,
        SlotRunResult run,
        int slotIndex,
        int waveIndex,
        StimulusEvidence? stimulus,
        StimulusExpectation? expectation,
        SettlingState settling,
        IReadOnlyList<AssertionOutcome> assertions,
        IReadOnlyList<int> coRunners,
        RegisterMap map,
        BuildStamp expectedBuild,
        int slotsCoveredByOneRead)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(enumeration);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(assertions);
        ArgumentNullException.ThrowIfNull(coRunners);
        ArgumentNullException.ThrowIfNull(map);

        var admissibility = Admissibility.Check(
            declaration.Basis,
            enumeration,
            declaration.Fidelity,
            declaration.AssertedBehaviours,
            declaration.Settling,
            declaration.CompletionSignal,
            declaration.VectorAuthor,
            declaration.BlockAuthor,
            declaration.ObservabilitySupported);

        var stimulusReport = StimulusCheck.Check(stimulus, expectation, expectedBuild);

        return new ResultPackage(
            declaration.VectorId,
            slotIndex,
            waveIndex,
            declaration.Basis,
            admissibility,
            declaration.Fidelity,
            stimulusReport,
            settling,
            run.Outcome,
            assertions,
            coRunners,
            new ValidityStamp(
                stimulus?.Version?.Observed ?? expectedBuild.Value,
                map.MapHash,
                Caveats(declaration, stimulus, slotsCoveredByOneRead)));
    }

    /// <summary>
    /// Everything this result's validity rests on that has NOT been measured.
    ///
    /// <para>Assembled per result rather than documented elsewhere, because a caveat somebody has to go
    /// and look up is a caveat nobody reads — and because which caveats apply DEPENDS ON HOW THE RUN WAS
    /// PERFORMED. A result read one slot at a time does not rest on F-1's premise; one read out of a
    /// six-slot group does.</para>
    /// </summary>
    private static IReadOnlyList<string> Caveats(VectorDeclaration declaration, StimulusEvidence? stimulus, int slotsCoveredByOneRead)
    {
        var caveats = new List<string>();

        if (slotsCoveredByOneRead > 1)
        {
            caveats.Add(
                $"THIS RESULT WAS READ OUT OF A {slotsCoveredByOneRead}-SLOT GROUP READ, AND F-1's PREMISE IS UNMEASURED. "
                + "X-A's amended rule says one FC03 covering several WHOLE slots is still one coherent transaction; that MB_SERVER "
                + "serves a wide multi-slot read as coherently as a narrow single-slot one is REASONED, NOT MEASURED. If the rig lane's "
                + "measurement comes back against it, this result is one of the ones affected.");
        }

        if (stimulus is null)
        {
            caveats.Add("NO LIVENESS EVIDENCE WAS SUPPLIED, so nothing here distinguishes this run from a frozen mirror.");
        }
        else if (stimulus.Manifest == ManifestPresence.NotAvailable)
        {
            caveats.Add(
                "NO LOAD MANIFEST WAS AVAILABLE, so transfer is not positively evidenced. Section 9c forbids inferring transfer from the "
                + "absence of a failure message, and this result does not: it records that the question was not answered.");
        }

        if (stimulus?.Version is null)
        {
            caveats.Add("THE VERSION REGISTER WAS NOT READ for this result, so what was RUNNING is asserted from the plan rather than from the device.");
        }

        if (declaration.Fidelity is { ValidatedAgainstPlantData: false } fidelity)
        {
            caveats.Add(
                $"MODEL '{fidelity.ModelId}' HAS NOT BEEN VALIDATED AGAINST REAL PLANT DATA (M5), and its declaration names "
                + (fidelity.DoesNotRepresent.Count == 0
                    ? "NO absences at all — which is itself a caveat, because every model omits something."
                    : $"these absences: {string.Join(", ", fidelity.DoesNotRepresent)}."));
        }

        return caveats;
    }
}
