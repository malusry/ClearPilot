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
        Assert.Equal("Recommended to clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.RecommendedToClean));
        Assert.Equal("Not recommended to clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.NotRecommendedToClean));
        Assert.Equal("Analysis only, do not clean", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal("Blocked", ConsolePresentationStyle.GetDecisionBadge(Language.English, CleanupDecision.Blocked));
        Assert.Equal("Reason", ConsolePresentationStyle.GetReasonLabel(Language.English));
        Assert.Equal("Possible impact if cleaned", ConsolePresentationStyle.GetImpactLabel(Language.English));
        Assert.Equal("Expected reclaim", ConsolePresentationStyle.GetExpectedReclaimLabel(Language.English));
        Assert.Equal("Risk", ConsolePresentationStyle.GetRiskLabel(Language.English));
        Assert.Equal("Safety note", ConsolePresentationStyle.GetSafetyNoteLabel(Language.English));
    }

    [Fact]
    public void DecisionLabelsAreLocalizedForSimplifiedChinese()
    {
        Assert.Equal("结论", ConsolePresentationStyle.GetDecisionLabel(Language.SimplifiedChinese));
        Assert.Equal("建议清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.RecommendedToClean));
        Assert.Equal("不建议清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.NotRecommendedToClean));
        Assert.Equal("仅分析，不清理", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal("已阻止", ConsolePresentationStyle.GetDecisionBadge(Language.SimplifiedChinese, CleanupDecision.Blocked));
        Assert.Equal("风险", ConsolePresentationStyle.GetRiskLabel(Language.SimplifiedChinese));
        Assert.Equal("原因", ConsolePresentationStyle.GetReasonLabel(Language.SimplifiedChinese));
        Assert.Equal("清理后的可能影响", ConsolePresentationStyle.GetImpactLabel(Language.SimplifiedChinese));
        Assert.Equal("预计可释放", ConsolePresentationStyle.GetExpectedReclaimLabel(Language.SimplifiedChinese));
        Assert.Equal("安全说明", ConsolePresentationStyle.GetSafetyNoteLabel(Language.SimplifiedChinese));
        Assert.Equal("状态", ConsolePresentationStyle.GetStatusLabel(Language.SimplifiedChinese));
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
}
