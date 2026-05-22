using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;

namespace ClearPilot.Cli;

public static class ConsolePresentationStyle
{
    public enum ModeColorRole
    {
        QuickSafeClean,
        RecommendedCleanup,
        DeepSpaceAnalysis,
        ReportsHistory,
        Settings,
        SafetyBoundary,
        Blocked,
        MutedDetail
    }

    public static bool ShouldUseColor(bool isOutputRedirected, string? noColorEnvironmentValue)
    {
        if (isOutputRedirected)
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(noColorEnvironmentValue);
    }

    public static ConsoleColor GetDecisionColor(CleanupDecision decision)
    {
        return decision switch
        {
            CleanupDecision.RecommendedToClean => ConsoleColor.Green,
            CleanupDecision.NotRecommendedToClean => ConsoleColor.DarkYellow,
            CleanupDecision.AnalysisOnlyDoNotClean => ConsoleColor.Cyan,
            CleanupDecision.Blocked => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    public static ConsoleColor GetRiskColor(RiskLevel riskLevel)
    {
        return riskLevel switch
        {
            RiskLevel.S0VeryLowRisk => ConsoleColor.Green,
            RiskLevel.S1LowRisk => ConsoleColor.Yellow,
            RiskLevel.S2ReviewRequired => ConsoleColor.DarkMagenta,
            RiskLevel.S3DoNotCleanAutomatically => ConsoleColor.Red,
            RiskLevel.Blocked => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    public static ConsoleColor GetModeColor(ModeColorRole role)
    {
        return role switch
        {
            ModeColorRole.QuickSafeClean => ConsoleColor.DarkGreen,
            ModeColorRole.RecommendedCleanup => ConsoleColor.DarkYellow,
            ModeColorRole.DeepSpaceAnalysis => ConsoleColor.Cyan,
            ModeColorRole.ReportsHistory => ConsoleColor.Blue,
            ModeColorRole.Settings => ConsoleColor.Magenta,
            ModeColorRole.SafetyBoundary => ConsoleColor.Yellow,
            ModeColorRole.Blocked => ConsoleColor.Red,
            ModeColorRole.MutedDetail => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray
        };
    }

    public static string GetDecisionBadge(Language language, CleanupDecision decision)
    {
        if (language == Language.SimplifiedChinese)
        {
            return decision switch
            {
                CleanupDecision.RecommendedToClean => "建议清理",
                CleanupDecision.NotRecommendedToClean => "不建议清理",
                CleanupDecision.AnalysisOnlyDoNotClean => "仅分析，不清理",
                CleanupDecision.Blocked => "已阻止",
                _ => decision.ToString()
            };
        }

        return decision switch
        {
            CleanupDecision.RecommendedToClean => "Recommended to clean",
            CleanupDecision.NotRecommendedToClean => "Not recommended to clean",
            CleanupDecision.AnalysisOnlyDoNotClean => "Analysis only, do not clean",
            CleanupDecision.Blocked => "Blocked",
            _ => decision.ToString()
        };
    }

    public static string GetDecisionLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "结论" : "Decision";
    }

    public static string GetRiskLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "风险" : "Risk";
    }

    public static string GetPathLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "路径" : "Path";
    }

    public static string GetInsightLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "说明" : "Insight";
    }

    public static string GetBoundaryLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "边界" : "Boundary";
    }

    public static string GetReasonLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "原因" : "Reason";
    }

    public static string GetImpactLabel(Language language)
    {
        return language == Language.SimplifiedChinese
            ? "影响"
            : "Impact";
    }

    public static string GetExpectedReclaimLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "预计可释放" : "Expected reclaim";
    }

    public static string GetSafetyNoteLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "安全说明" : "Safety note";
    }

    public static ConsoleColor GetDeepSpacePrimaryColor()
    {
        return ConsoleColor.Cyan;
    }

    public static ConsoleColor GetDeepSpaceCardColor()
    {
        return ConsoleColor.DarkMagenta;
    }

    public static ConsoleColor GetDeepSpaceSizeColor()
    {
        return ConsoleColor.Cyan;
    }

    public static ConsoleColor GetDeepSpacePathColor()
    {
        return GetPathValueColor();
    }

    public static ConsoleColor GetDeepSpaceFieldLabelColor()
    {
        return GetFieldLabelColor();
    }

    public static ConsoleColor GetDeepSpaceInsightColor()
    {
        return GetExplanationValueColor();
    }

    public static ConsoleColor GetDeepSpaceBoundaryColor(CleanupDecision decision)
    {
        return decision == CleanupDecision.Blocked ? ConsoleColor.Red : ConsoleColor.Yellow;
    }

    public static ConsoleColor GetFieldLabelColor()
    {
        return ConsoleColor.DarkCyan;
    }

    public static ConsoleColor GetPathValueColor()
    {
        return ConsoleColor.DarkGray;
    }

    public static ConsoleColor GetExplanationValueColor()
    {
        return ConsoleColor.White;
    }

    public static ConsoleColor GetRecommendedFieldLabelColor()
    {
        return GetFieldLabelColor();
    }

    public static ConsoleColor GetRecommendedExpectedReclaimColor()
    {
        return ConsoleColor.Cyan;
    }

    public static ConsoleColor GetRecommendedImpactColor()
    {
        return ConsoleColor.DarkYellow;
    }

    public static ConsoleColor GetRecommendedReasonColor()
    {
        return GetExplanationValueColor();
    }

    public static ConsoleColor GetRecommendedRiskColor(RiskLevel riskLevel)
    {
        return GetRiskColor(riskLevel);
    }

    public static ConsoleColor GetRecommendedPromptColor()
    {
        return ConsoleColor.Cyan;
    }

    public static ConsoleColor GetRecommendedPrimarySafetyColor()
    {
        return ConsoleColor.Yellow;
    }

    public static ConsoleColor GetRecommendedSecondarySafetyColor()
    {
        return ConsoleColor.DarkGray;
    }

    public static bool IsBulkSelectableRecommendedItem(RiskLevel riskLevel, CleanupDecision decision, bool processGuardBlocked)
    {
        if (processGuardBlocked)
        {
            return false;
        }

        return riskLevel == RiskLevel.S1LowRisk
            && decision == CleanupDecision.RecommendedToClean;
    }
}
