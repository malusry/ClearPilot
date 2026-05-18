using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Rules;

public sealed record CleanupRule(
    string RuleId,
    string Category,
    RiskLevel RiskLevel,
    IReadOnlyList<string> RootPaths,
    IReadOnlyList<string> IncludeFilePatterns,
    IReadOnlyList<string> ExcludePathSegments,
    TimeSpan? MinimumAge,
    string Explanation,
    bool Recursive = true)
{
    public bool CanRunWithoutConfirmation => RiskLevel == RiskLevel.S0VeryLowRisk;
}
