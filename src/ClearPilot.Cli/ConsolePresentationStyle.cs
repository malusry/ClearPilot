using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;

namespace ClearPilot.Cli;

public static class ConsolePresentationStyle
{
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
            CleanupDecision.RecommendedToClean => ConsoleColor.DarkGreen,
            CleanupDecision.NotRecommendedToClean => ConsoleColor.DarkYellow,
            CleanupDecision.AnalysisOnlyDoNotClean => ConsoleColor.Cyan,
            CleanupDecision.Blocked => ConsoleColor.Red,
            _ => ConsoleColor.Gray
        };
    }

    public static string GetDecisionBadge(Language language, CleanupDecision decision)
    {
        if (language == Language.SimplifiedChinese)
        {
            return decision switch
            {
                CleanupDecision.RecommendedToClean => "\u5EFA\u8BAE\u6E05\u7406",
                CleanupDecision.NotRecommendedToClean => "\u4E0D\u5EFA\u8BAE\u6E05\u7406",
                CleanupDecision.AnalysisOnlyDoNotClean => "\u4EC5\u5206\u6790\uFF0C\u4E0D\u6E05\u7406",
                CleanupDecision.Blocked => "\u5DF2\u963B\u6B62",
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
        return language == Language.SimplifiedChinese ? "\u7ED3\u8BBA" : "Decision";
    }

    public static string GetRiskLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u98CE\u9669" : "Risk";
    }

    public static string GetReasonLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u539F\u56E0" : "Reason";
    }

    public static string GetImpactLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u5F71\u54CD" : "Possible impact";
    }

    public static string GetRecommendedActionLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u5EFA\u8BAE\u64CD\u4F5C" : "Recommended action";
    }

    public static string GetSafetyNoteLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u5B89\u5168\u8BF4\u660E" : "Safety note";
    }

    public static string GetStatusLabel(Language language)
    {
        return language == Language.SimplifiedChinese ? "\u72B6\u6001" : "Status";
    }
}
