using ClearPilot.Core.Analysis;
using ClearPilot.Core.Rules;

namespace ClearPilot.Core.Cleanup;

public static class CleanupDecisionAdvisor
{
    private const long LogOrDiagnosticRecommendBytes = 128L * 1024 * 1024;
    private const long CrashDumpRecommendBytes = 256L * 1024 * 1024;

    public static CleanupDecisionResult ForCandidate(
        CleanupRule rule,
        TargetAdvice advice,
        long estimatedBytes,
        int fileCount,
        bool launcherRunning)
    {
        if (launcherRunning)
        {
            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                "Skipped because the app is running.");
        }

        var byRisk = ByRisk(rule.RiskLevel);
        if (byRisk is not null)
        {
            return byRisk;
        }

        if (string.Equals(rule.RuleId, "cp.s1.user-crash-dumps", StringComparison.OrdinalIgnoreCase))
        {
            if (IsOlderThan(rule, TimeSpan.FromDays(7)) && estimatedBytes >= CrashDumpRecommendBytes)
            {
                return new CleanupDecisionResult(
                    CleanupDecision.RecommendedToClean,
                    "Old crash dumps are taking significant space.");
            }

            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                "Crash dumps may still be useful for troubleshooting.");
        }

        if (string.Equals(rule.RuleId, "cp.s1.windows-temp", StringComparison.OrdinalIgnoreCase))
        {
            if (IsOlderThan(rule, TimeSpan.FromHours(24)) && estimatedBytes > 0)
            {
                return new CleanupDecisionResult(
                    CleanupDecision.RecommendedToClean,
                    "Old accessible temporary leftovers are usually safe to remove.");
            }

            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                "Temp files are recent, uncertain, or not confidently disposable.");
        }

        if (string.Equals(rule.RuleId, "cp.s1.windows-inet-cache", StringComparison.OrdinalIgnoreCase))
        {
            return ContainsIdentityExclusions(rule)
                ? new CleanupDecisionResult(
                    CleanupDecision.RecommendedToClean,
                    "This target is cache-only and excludes identity/session data.")
                : new CleanupDecisionResult(
                    CleanupDecision.NotRecommendedToClean,
                    "Cache-only confidence is low for this path.");
        }

        if (string.Equals(rule.RuleId, "cp.s1.msstore-localcache", StringComparison.OrdinalIgnoreCase))
        {
            return IsStoreLocalCacheRule(rule)
                ? new CleanupDecisionResult(
                    CleanupDecision.RecommendedToClean,
                    "Store LocalCache paths are cache-scoped with durable state excluded.")
                : new CleanupDecisionResult(
                    CleanupDecision.NotRecommendedToClean,
                    "This Store target may include durable app data.");
        }

        if (IsLauncherCacheRule(rule))
        {
            return new CleanupDecisionResult(
                CleanupDecision.RecommendedToClean,
                "Launcher cache data is rebuildable when the launcher is closed.");
        }

        if (IsLogOrDiagnosticRule(rule))
        {
            if (estimatedBytes >= LogOrDiagnosticRecommendBytes && IsOlderThan(rule, TimeSpan.FromDays(1)))
            {
                return new CleanupDecisionResult(
                    CleanupDecision.RecommendedToClean,
                    "Old diagnostic files are large enough to justify cleanup.");
            }

            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                "Diagnostic files may still be useful unless they are clearly old and large.");
        }

        if (advice.Recommendation is RecommendationLevel.Recommended or RecommendationLevel.Optional)
        {
            return new CleanupDecisionResult(
                CleanupDecision.RecommendedToClean,
                "This is low-risk rebuildable cleanup data.");
        }

        if (advice.Recommendation == RecommendationLevel.NotRecommended)
        {
            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                advice.Reason);
        }

        if (advice.Recommendation == RecommendationLevel.Blocked)
        {
            return new CleanupDecisionResult(
                CleanupDecision.Blocked,
                advice.Reason);
        }

        return new CleanupDecisionResult(
            CleanupDecision.AnalysisOnlyDoNotClean,
            "This target is review-only.");
    }

    public static CleanupDecisionResult ForExecutionResult(
        CleanupRule rule,
        TargetAdvice advice,
        long sizeBytes,
        string processGuardResult)
    {
        var launcherRunning = processGuardResult.StartsWith("Blocked:LauncherRunning", StringComparison.OrdinalIgnoreCase);
        return ForCandidate(rule, advice, sizeBytes, sizeBytes > 0 ? 1 : 0, launcherRunning);
    }

    public static CleanupDecisionResult ForDeepSpaceItem(DeepSpaceItem item, TargetAdvice advice)
    {
        if (item.RiskLevel == RiskLevel.Blocked)
        {
            return new CleanupDecisionResult(
                CleanupDecision.Blocked,
                advice.Reason);
        }

        if (item.RiskLevel == RiskLevel.S3DoNotCleanAutomatically)
        {
            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                advice.Reason);
        }

        if (item.RiskLevel == RiskLevel.S2ReviewRequired)
        {
            return new CleanupDecisionResult(
                CleanupDecision.AnalysisOnlyDoNotClean,
                advice.Reason);
        }

        if (advice.Recommendation == RecommendationLevel.Blocked)
        {
            return new CleanupDecisionResult(
                CleanupDecision.Blocked,
                advice.Reason);
        }

        if (advice.Recommendation == RecommendationLevel.NotRecommended)
        {
            return new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                advice.Reason);
        }

        return new CleanupDecisionResult(
            CleanupDecision.AnalysisOnlyDoNotClean,
            "This deep-space item is shown for manual review only.");
    }

    private static CleanupDecisionResult? ByRisk(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.S0VeryLowRisk => new CleanupDecisionResult(
                CleanupDecision.RecommendedToClean,
                "Very-low-risk temporary/cache cleanup target."),
            RiskLevel.S2ReviewRequired => new CleanupDecisionResult(
                CleanupDecision.AnalysisOnlyDoNotClean,
                "This target is review-only and cannot be deleted by ClearPilot."),
            RiskLevel.S3DoNotCleanAutomatically => new CleanupDecisionResult(
                CleanupDecision.NotRecommendedToClean,
                "This target is too risky for cleanup."),
            RiskLevel.Blocked => new CleanupDecisionResult(
                CleanupDecision.Blocked,
                "This target is explicitly blocked by safety policy."),
            _ => null
        };
    }

    private static bool IsOlderThan(CleanupRule rule, TimeSpan threshold)
    {
        return rule.MinimumAge is not null && rule.MinimumAge.Value >= threshold;
    }

    private static bool ContainsIdentityExclusions(CleanupRule rule)
    {
        string[] required =
        [
            "Cookies",
            "History",
            "Sessions",
            "Login Data",
            "Bookmarks",
            "Local Storage",
            "IndexedDB",
            "Session Storage"
        ];

        return required.All(name => rule.ExcludePathSegments.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsStoreLocalCacheRule(CleanupRule rule)
    {
        var hasLocalCacheRoot = rule.RootPaths.Any(path => path.Contains($"{Path.DirectorySeparatorChar}LocalCache", StringComparison.OrdinalIgnoreCase)
            || path.Contains($"{Path.AltDirectorySeparatorChar}LocalCache", StringComparison.OrdinalIgnoreCase));

        if (!hasLocalCacheRoot)
        {
            return false;
        }

        string[] durableExclusions =
        [
            "LocalState",
            "RoamingState",
            "Settings",
            "SystemAppData"
        ];

        return durableExclusions.All(name => rule.ExcludePathSegments.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    private static bool IsLogOrDiagnosticRule(CleanupRule rule)
    {
        var id = rule.RuleId;
        return id.Contains("log", StringComparison.OrdinalIgnoreCase)
            || id.Contains("dump", StringComparison.OrdinalIgnoreCase)
            || id.Contains("crash", StringComparison.OrdinalIgnoreCase)
            || id.Contains("error-report", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLauncherCacheRule(CleanupRule rule)
    {
        if (string.IsNullOrWhiteSpace(rule.LauncherName))
        {
            return false;
        }

        var id = rule.RuleId;
        return id.Contains("cache", StringComparison.OrdinalIgnoreCase)
            || id.Contains("httpcache", StringComparison.OrdinalIgnoreCase)
            || id.Contains("webcache", StringComparison.OrdinalIgnoreCase);
    }
}
