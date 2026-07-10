using System;
using System.Collections.Generic;

namespace OpennessCli.Openness;

/// <summary>
/// Classifies a block's Openness ProgrammingLanguage as safety (F-) or not.
/// Goal 11 / design philosophy #9: safety blocks are identified but never opened.
/// </summary>
public static class SafetyClassifier
{
    private const string SafetyPrefix = "F_";

    // Every non-safety Siemens.Engineering.SW.Blocks.ProgrammingLanguage member in the
    // installed V20 API, confirmed by reflecting on Siemens.Engineering.dll. Anything not
    // in this list and not F_-prefixed is unrecognized and must fail loudly rather than
    // silently pass as "not safety" (design philosophy #10; risk R-10: safety exclusion
    // must never fail structurally).
    private static readonly HashSet<string> KnownNonSafetyLanguages = new(StringComparer.Ordinal)
    {
        "Undef", "STL", "LAD", "FBD", "SCL", "DB", "GRAPH", "CPU_DB", "CFC", "SFC",
        "FBD_IEC", "LAD_IEC", "SDB", "S7_PDIAG", "RSE", "FCP", "FLD",
        "ProDiag", "ProDiag_OB", "Motion_DB", "CEM",
    };

    public static bool IsSafety(string programmingLanguage)
    {
        if (programmingLanguage is null)
        {
            throw new ArgumentNullException(nameof(programmingLanguage));
        }

        if (programmingLanguage.StartsWith(SafetyPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        if (KnownNonSafetyLanguages.Contains(programmingLanguage))
        {
            return false;
        }

        throw new UnrecognizedProgrammingLanguageException(programmingLanguage);
    }
}

public sealed class UnrecognizedProgrammingLanguageException : Exception
{
    public UnrecognizedProgrammingLanguageException(string language)
        : base(
            $"Unrecognized block programming language '{language}'. Refusing to classify: this could be " +
            $"an undocumented fail-safe (F-) variant. Add it to {nameof(SafetyClassifier)} explicitly, " +
            "after confirming with the engineer whether it is safety-related.")
    {
        Language = language;
    }

    public string Language { get; }
}
