using Harness.Map;
using Harness.Wire;

namespace Harness.Results;

/// <summary>What a submission declared about one vector — everything the package needs that a run cannot supply.</summary>
/// <param name="AssertedBehaviours">The behaviours this vector's assertions depend on. Set-differenced against the model's declaration (M4).</param>
/// <param name="CompletionSignal">The block's own done-signal, so the settling declaration can be checked against it.</param>
/// <param name="Observability">
/// The COMPUTED observability evaluation for this vector (ObservabilityCheck.Evaluate). Null means the
/// check did not run, which fails closed - it used to be a caller-supplied bool, and a gate whose
/// verdict its caller supplies is not a gate.
/// </param>
public sealed record VectorDeclaration(
    string VectorId,
    Basis? Basis,
    FidelityDeclaration? Fidelity,
    SettlingDeclaration? Settling,
    IReadOnlyCollection<string> AssertedBehaviours,
    string CompletionSignal,
    AgentIdentity VectorAuthor,
    AgentIdentity BlockAuthor,
    ObservabilityReport? Observability,
    /// <summary>
    /// AMB-19's finding for this vector. Null means the question was never asked, which is itself a
    /// caveat rather than a pass — see <see cref="BoundsCurrencyCheck"/>.
    /// </summary>
    VectorBoundsCurrency? BoundsCurrency = null);

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

        // *** NULL IS "NO CO-RUNNING SLICE WAS RECORDED", AND IT IS NOT AN EMPTY ONE. *** See
        // ResultPackage.CoRunners: an empty list is the measurement "this vector ran alone", which is a
        // claim, and the caller must not be able to make it by accident.
        IReadOnlyList<int>? coRunners,
        RegisterMap map,
        BuildStamp expectedBuild,
        int slotsCoveredByOneRead)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        ArgumentNullException.ThrowIfNull(enumeration);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(assertions);
        // `coRunners` is deliberately NOT null-checked: null is a meaningful value here (no slice was
        // recorded), and a throw would push every caller back to the empty list that hid the distinction.
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
            declaration.Observability);

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
                Caveats(declaration, stimulus, slotsCoveredByOneRead)),
            declaration.BoundsCurrency);
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

        // AMB-19's two NOT-CHECKED states. They do not change the VERDICT — the submission gate refuses
        // them before a run happens — but if one reaches a built package the gate was bypassed, and a
        // result whose bound nobody compared must not read as one whose bound agreed.
        if (declaration.BoundsCurrency is null)
        {
            caveats.Add(
                "NOTHING ASKED WHETHER THIS VECTOR STILL TESTS THE SPECIFIED BOUND (AMB-19). A retune moves no assertion ID, so no citation would dangle "
                + "and no other check here would notice — this result may be a green against a number the specification no longer states.");
        }
        else if (declaration.BoundsCurrency.State is BoundsCurrencyState.NotDeclared or BoundsCurrencyState.NoTable)
        {
            caveats.Add("BOUNDS CURRENCY WAS NOT ESTABLISHED (AMB-19). " + declaration.BoundsCurrency.Detail);
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
