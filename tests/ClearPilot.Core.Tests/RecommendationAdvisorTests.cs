using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Rules;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class RecommendationAdvisorTests
{
    [Fact]
    public void S0DefaultsToRecommended()
    {
        var advice = RecommendationAdvisor.ForRule(CreateRule("cp.s0.user-temp", RiskLevel.S0VeryLowRisk));
        Assert.Equal(RecommendationLevel.Recommended, advice.Recommendation);
    }

    [Fact]
    public void S1CrashDumpsMapsToOptional()
    {
        var advice = RecommendationAdvisor.ForRule(CreateRule("cp.s1.user-crash-dumps", RiskLevel.S1LowRisk));
        Assert.Equal(RecommendationLevel.Optional, advice.Recommendation);
    }

    [Fact]
    public void S1WindowsTempRemainsS1AndRecommended()
    {
        var rule = CreateRule("cp.s1.windows-temp", RiskLevel.S1LowRisk);
        var advice = RecommendationAdvisor.ForRule(rule);

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.Equal(RecommendationLevel.Recommended, advice.Recommendation);
    }

    [Fact]
    public void SteamLauncherWebCacheAndLogsMapToSafeS1Recommendations()
    {
        Assert.Equal(
            RecommendationLevel.Recommended,
            RecommendationAdvisor.ForRule(CreateRule("cp.s1.steam-httpcache", RiskLevel.S1LowRisk)).Recommendation);
        Assert.Equal(
            RecommendationLevel.Optional,
            RecommendationAdvisor.ForRule(CreateRule("cp.s1.steam-logs", RiskLevel.S1LowRisk)).Recommendation);
    }

    [Fact]
    public void SteamShaderAndDepotMapToReviewOnlyOrNotRecommended()
    {
        var shader = RecommendationAdvisor.ForDeepSpaceItem(CreateDeepSpaceItem("cp.s2.steam-shadercache"));
        var depot = RecommendationAdvisor.ForDeepSpaceItem(CreateDeepSpaceItem("cp.s2.steam-depotcache"));

        Assert.Equal(RecommendationLevel.NotRecommended, shader.Recommendation);
        Assert.Equal(RecommendationLevel.NotRecommended, depot.Recommendation);
    }

    [Fact]
    public void WindowsSystemManagedAreasMapToReviewOnly()
    {
        var windowsUpdate = RecommendationAdvisor.ForDeepSpaceItem(CreateSystemManagedItem("cp.s2.windows-update-download"));
        var delivery = RecommendationAdvisor.ForDeepSpaceItem(CreateSystemManagedItem("cp.s2.delivery-optimization-cache"));

        Assert.Equal(RecommendationLevel.ReviewOnly, windowsUpdate.Recommendation);
        Assert.Equal(RecommendationLevel.ReviewOnly, delivery.Recommendation);
    }

    [Fact]
    public void BlockedRiskMapsToBlockedRecommendation()
    {
        var advice = RecommendationAdvisor.ForRule(CreateRule("any.blocked", RiskLevel.Blocked));
        Assert.Equal(RecommendationLevel.Blocked, advice.Recommendation);
    }

    [Fact]
    public void RecommendationNormalizationNeverMakesS2S3BlockedDeletable()
    {
        var s2 = RecommendationAdvisor.ForRule(CreateRule("cp.s0.user-temp", RiskLevel.S2ReviewRequired));
        var s3 = RecommendationAdvisor.ForRule(CreateRule("cp.s0.user-temp", RiskLevel.S3DoNotCleanAutomatically));
        var blocked = RecommendationAdvisor.ForRule(CreateRule("cp.s0.user-temp", RiskLevel.Blocked));

        Assert.DoesNotContain(s2.Recommendation, new[] { RecommendationLevel.Recommended, RecommendationLevel.Optional });
        Assert.Equal(RecommendationLevel.NotRecommended, s3.Recommendation);
        Assert.Equal(RecommendationLevel.Blocked, blocked.Recommendation);
    }

    private static CleanupRule CreateRule(string id, RiskLevel riskLevel)
    {
        return new CleanupRule(
            id,
            "Test",
            riskLevel,
            [@"C:\Temp"],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "test");
    }

    private static DeepSpaceItem CreateDeepSpaceItem(string targetId)
    {
        return new DeepSpaceItem(
            DeepSpaceItemType.GameLauncherReviewArea,
            @"C:\Temp\path",
            1024,
            DateTimeOffset.UtcNow,
            RiskLevel.S2ReviewRequired,
            "reason",
            "action",
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            targetId,
            targetId,
            "Category");
    }

    private static DeepSpaceItem CreateSystemManagedItem(string targetId)
    {
        return new DeepSpaceItem(
            DeepSpaceItemType.SystemManagedWindowsArea,
            @"C:\Temp\path",
            1024,
            DateTimeOffset.UtcNow,
            RiskLevel.S2ReviewRequired,
            "reason",
            "action",
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            targetId,
            targetId,
            "Category");
    }
}
