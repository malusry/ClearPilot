using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Rules;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupDecisionAdvisorTests
{
    [Fact]
    public void S0UserTempMapsToRecommendedToClean()
    {
        var rule = CreateRule("cp.s0.user-temp", RiskLevel.S0VeryLowRisk);
        var advice = RecommendationAdvisor.ForRule(rule);

        var decision = CleanupDecisionAdvisor.ForCandidate(rule, advice, estimatedBytes: 1024, fileCount: 3, launcherRunning: false);

        Assert.Equal(CleanupDecision.RecommendedToClean, decision.Decision);
    }

    [Fact]
    public void RebuildableS1CacheMapsToRecommendedToClean()
    {
        var rule = CreateRule("cp.s1.steam-httpcache", RiskLevel.S1LowRisk, launcherName: "Steam", minAge: TimeSpan.FromDays(1));
        var advice = RecommendationAdvisor.ForRule(rule);

        var decision = CleanupDecisionAdvisor.ForCandidate(rule, advice, estimatedBytes: 8 * 1024 * 1024, fileCount: 50, launcherRunning: false);

        Assert.Equal(CleanupDecision.RecommendedToClean, decision.Decision);
    }

    [Fact]
    public void RecentOrSmallCrashDumpsMapToNotRecommendedToClean()
    {
        var rule = CreateRule("cp.s1.user-crash-dumps", RiskLevel.S1LowRisk, minAge: TimeSpan.FromDays(7));
        var advice = RecommendationAdvisor.ForRule(rule);

        var decision = CleanupDecisionAdvisor.ForCandidate(rule, advice, estimatedBytes: 32 * 1024 * 1024, fileCount: 1, launcherRunning: false);

        Assert.Equal(CleanupDecision.NotRecommendedToClean, decision.Decision);
    }

    [Fact]
    public void WindowsUpdateAndDeliveryOptimizationMapToAnalysisOnly()
    {
        var windowsUpdate = CreateS2Item("cp.s2.windows-update-download");
        var delivery = CreateS2Item("cp.s2.delivery-optimization-cache");

        var windowsDecision = CleanupDecisionAdvisor.ForDeepSpaceItem(windowsUpdate, RecommendationAdvisor.ForDeepSpaceItem(windowsUpdate));
        var deliveryDecision = CleanupDecisionAdvisor.ForDeepSpaceItem(delivery, RecommendationAdvisor.ForDeepSpaceItem(delivery));

        Assert.Equal(CleanupDecision.AnalysisOnlyDoNotClean, windowsDecision.Decision);
        Assert.Equal(CleanupDecision.AnalysisOnlyDoNotClean, deliveryDecision.Decision);
    }

    [Fact]
    public void SteamShaderAndDepotMapToAnalysisOnlyInDeepSpace()
    {
        var shader = CreateS2Item("cp.s2.steam-shadercache");
        var depot = CreateS2Item("cp.s2.steam-depotcache");

        var shaderDecision = CleanupDecisionAdvisor.ForDeepSpaceItem(shader, RecommendationAdvisor.ForDeepSpaceItem(shader));
        var depotDecision = CleanupDecisionAdvisor.ForDeepSpaceItem(depot, RecommendationAdvisor.ForDeepSpaceItem(depot));

        Assert.Equal(CleanupDecision.AnalysisOnlyDoNotClean, shaderDecision.Decision);
        Assert.Equal(CleanupDecision.AnalysisOnlyDoNotClean, depotDecision.Decision);
    }

    [Fact]
    public void BlockedRiskMapsToBlockedDecision()
    {
        var rule = CreateRule("cp.blocked.test", RiskLevel.Blocked);
        var advice = RecommendationAdvisor.ForRule(rule);

        var decision = CleanupDecisionAdvisor.ForCandidate(rule, advice, estimatedBytes: 1024, fileCount: 1, launcherRunning: false);

        Assert.Equal(CleanupDecision.Blocked, decision.Decision);
    }

    [Fact]
    public void RunningLauncherTargetMapsToNotRecommendedToClean()
    {
        var rule = CreateRule("cp.s1.epic-webcache", RiskLevel.S1LowRisk, launcherName: "Epic Games Launcher", minAge: TimeSpan.FromDays(1));
        var advice = RecommendationAdvisor.ForRule(rule);

        var decision = CleanupDecisionAdvisor.ForCandidate(rule, advice, estimatedBytes: 16 * 1024 * 1024, fileCount: 4, launcherRunning: true);

        Assert.Equal(CleanupDecision.NotRecommendedToClean, decision.Decision);
        Assert.Contains("running", decision.DecisionReason, StringComparison.OrdinalIgnoreCase);
    }

    private static CleanupRule CreateRule(string ruleId, RiskLevel riskLevel, string launcherName = "", TimeSpan? minAge = null)
    {
        return new CleanupRule(
            ruleId,
            "Test",
            riskLevel,
            [@"C:\Temp\Root"],
            ["*"],
            [
                "Cookies",
                "History",
                "Sessions",
                "Login Data",
                "Bookmarks",
                "Local Storage",
                "IndexedDB",
                "Session Storage",
                "LocalState",
                "RoamingState",
                "Settings",
                "SystemAppData"
            ],
            minAge,
            "test",
            LauncherName: launcherName);
    }

    private static DeepSpaceItem CreateS2Item(string targetId)
    {
        return new DeepSpaceItem(
            DeepSpaceItemType.SystemManagedWindowsArea,
            @"C:\Windows\Temp\placeholder",
            10 * 1024 * 1024,
            DateTimeOffset.UtcNow.AddDays(-3),
            RiskLevel.S2ReviewRequired,
            "Analysis-only area.",
            "Use Windows Settings / Storage Sense / Disk Cleanup.",
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            targetId,
            targetId,
            "System-managed");
    }
}
