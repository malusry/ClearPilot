using ClearPilot.Cli;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class ConsolePresentationStyleTests
{
    [Fact]
    public void ShouldUseColorReturnsFalseWhenOutputIsRedirected()
    {
        Assert.False(ConsolePresentationStyle.ShouldUseColor(isOutputRedirected: true, noColorEnvironmentValue: null));
    }

    [Fact]
    public void ShouldUseColorReturnsFalseWhenNoColorIsSet()
    {
        Assert.False(ConsolePresentationStyle.ShouldUseColor(isOutputRedirected: false, noColorEnvironmentValue: "1"));
    }

    [Fact]
    public void ShouldUseColorReturnsTrueForInteractiveOutputWithoutNoColor()
    {
        Assert.True(ConsolePresentationStyle.ShouldUseColor(isOutputRedirected: false, noColorEnvironmentValue: null));
    }

    [Fact]
    public void DecisionLabelsAreLocalizedForEnglish()
    {
        Assert.Equal("Decision", ConsolePresentationStyle.GetDecisionLabel(Language.English));
        Assert.Equal("Path", ConsolePresentationStyle.GetPathLabel(Language.English));
        Assert.Equal("Insight", ConsolePresentationStyle.GetInsightLabel(Language.English));
        Assert.Equal("Boundary", ConsolePresentationStyle.GetBoundaryLabel(Language.English));
        Assert.Equal("Recommended to clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.RecommendedToClean));
        Assert.Equal("Not recommended to clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.NotRecommendedToClean));
        Assert.Equal("Analysis only, do not clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal("Blocked", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.Blocked));
        Assert.Equal("Reason", ConsolePresentationStyle.GetReasonLabel(Language.English));
        Assert.Equal("Impact", ConsolePresentationStyle.GetImpactLabel(Language.English));
        Assert.Equal("Expected reclaim", ConsolePresentationStyle.GetExpectedReclaimLabel(Language.English));
        Assert.Equal("Risk", ConsolePresentationStyle.GetRiskLabel(Language.English));
        Assert.Equal("Safety note", ConsolePresentationStyle.GetSafetyNoteLabel(Language.English));
    }

    [Fact]
    public void DecisionLabelsAreLocalizedForSimplifiedChinese()
    {
        Assert.Equal("结论", ConsolePresentationStyle.GetDecisionLabel(Language.SimplifiedChinese));
        Assert.Equal("路径", ConsolePresentationStyle.GetPathLabel(Language.SimplifiedChinese));
        Assert.Equal("说明", ConsolePresentationStyle.GetInsightLabel(Language.SimplifiedChinese));
        Assert.Equal("边界", ConsolePresentationStyle.GetBoundaryLabel(Language.SimplifiedChinese));
        Assert.Equal("建议清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.RecommendedToClean));
        Assert.Equal("不建议清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.NotRecommendedToClean));
        Assert.Equal("仅分析，不清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal("已阻止", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.Blocked));
        Assert.Equal("风险", ConsolePresentationStyle.GetRiskLabel(Language.SimplifiedChinese));
        Assert.Equal("原因", ConsolePresentationStyle.GetReasonLabel(Language.SimplifiedChinese));
        Assert.Equal("影响", ConsolePresentationStyle.GetImpactLabel(Language.SimplifiedChinese));
        Assert.Equal("预计可释放", ConsolePresentationStyle.GetExpectedReclaimLabel(Language.SimplifiedChinese));
        Assert.Equal("安全说明", ConsolePresentationStyle.GetSafetyNoteLabel(Language.SimplifiedChinese));
    }

    [Fact]
    public void BulkSelectableRecommendedItemRequiresEligibleS1RecommendedAndNoProcessGuardBlock()
    {
        Assert.True(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S1LowRisk,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S1LowRisk,
            CleanupDecision.NotRecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S2ReviewRequired,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S3DoNotCleanAutomatically,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.Blocked,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S1LowRisk,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: true));
    }

    [Fact]
    public void ConsolePresentationStyle_ModeColors_AreDistinct()
    {
        Assert.Equal(ConsoleColor.DarkGreen, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.QuickSafeClean));
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.RecommendedCleanup));
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.DeepSpaceAnalysis));
        Assert.Equal(ConsoleColor.Blue, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.ReportsHistory));
        Assert.Equal(ConsoleColor.Magenta, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.Settings));
        Assert.Equal(ConsoleColor.Yellow, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.SafetyBoundary));
        Assert.Equal(ConsoleColor.Red, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.Blocked));
        Assert.Equal(ConsoleColor.DarkGray, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.MutedDetail));
    }

    [Fact]
    public void ConsolePresentationStyle_DeepSpace_DoesNotUseGreenForSizeOrPrimaryMode()
    {
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetDeepSpacePrimaryColor());
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetDeepSpaceSizeColor());
        Assert.Equal(ConsoleColor.DarkMagenta, ConsolePresentationStyle.GetDeepSpaceCardColor());

        Assert.NotEqual(ConsoleColor.Green, ConsolePresentationStyle.GetDeepSpacePrimaryColor());
        Assert.NotEqual(ConsoleColor.DarkGreen, ConsolePresentationStyle.GetDeepSpacePrimaryColor());
        Assert.NotEqual(ConsoleColor.Green, ConsolePresentationStyle.GetDeepSpaceSizeColor());
        Assert.NotEqual(ConsoleColor.DarkGreen, ConsolePresentationStyle.GetDeepSpaceSizeColor());
    }

    [Fact]
    public void ConsolePresentationStyle_RecommendedCleanup_UsesDistinctModeColors()
    {
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.RecommendedCleanup));
        Assert.Equal(ConsoleColor.Green, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.RecommendedToClean));
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.NotRecommendedToClean));
        Assert.Equal(ConsoleColor.DarkCyan, ConsolePresentationStyle.GetRecommendedFieldLabelColor());
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetRecommendedExpectedReclaimColor());
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetRecommendedImpactColor());
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetRecommendedPromptColor());
        Assert.Equal(ConsoleColor.Yellow, ConsolePresentationStyle.GetRecommendedPrimarySafetyColor());
        Assert.Equal(ConsoleColor.DarkGray, ConsolePresentationStyle.GetRecommendedSecondarySafetyColor());
    }

    [Fact]
    public void ConsolePresentationStyle_FieldLabel_IsDarkCyan()
    {
        Assert.Equal(ConsoleColor.DarkCyan, ConsolePresentationStyle.GetFieldLabelColor());
        Assert.Equal(ConsoleColor.DarkCyan, ConsolePresentationStyle.GetRecommendedFieldLabelColor());
        Assert.Equal(ConsoleColor.DarkCyan, ConsolePresentationStyle.GetDeepSpaceFieldLabelColor());
    }

    [Fact]
    public void ConsolePresentationStyle_ExpectedReclaim_IsCyan()
    {
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetRecommendedExpectedReclaimColor());
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetDeepSpaceSizeColor());
    }

    [Fact]
    public void ConsolePresentationStyle_Impact_IsDarkYellow()
    {
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetRecommendedImpactColor());
    }

    [Fact]
    public void ConsolePresentationStyle_DecisionColors_AreSemantic()
    {
        Assert.Equal(ConsoleColor.Green, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.RecommendedToClean));
        Assert.Equal(ConsoleColor.DarkYellow, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.NotRecommendedToClean));
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal(ConsoleColor.Red, ConsolePresentationStyle.GetDecisionColor(CleanupDecision.Blocked));
    }

    [Fact]
    public void ConsolePresentationStyle_RiskColors_AreSemantic()
    {
        Assert.Equal(ConsoleColor.Yellow, ConsolePresentationStyle.GetRiskColor(RiskLevel.S1LowRisk));
        Assert.Equal(ConsoleColor.DarkMagenta, ConsolePresentationStyle.GetRiskColor(RiskLevel.S2ReviewRequired));
        Assert.Equal(ConsoleColor.Red, ConsolePresentationStyle.GetRiskColor(RiskLevel.S3DoNotCleanAutomatically));
        Assert.Equal(ConsoleColor.Red, ConsolePresentationStyle.GetRiskColor(RiskLevel.Blocked));
    }

    [Fact]
    public void ConsolePresentationStyle_Path_IsMuted()
    {
        Assert.Equal(ConsoleColor.DarkGray, ConsolePresentationStyle.GetPathValueColor());
        Assert.Equal(ConsoleColor.DarkGray, ConsolePresentationStyle.GetDeepSpacePathColor());
    }

    [Fact]
    public void ConsolePresentationStyle_DeepSpace_SizeIsNotGreen()
    {
        Assert.Equal(ConsoleColor.Cyan, ConsolePresentationStyle.GetDeepSpaceSizeColor());
        Assert.NotEqual(ConsoleColor.Green, ConsolePresentationStyle.GetDeepSpaceSizeColor());
        Assert.NotEqual(ConsoleColor.DarkGreen, ConsolePresentationStyle.GetDeepSpaceSizeColor());
    }
}
