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

    [Fact]
    public void RecommendationMapping_AppProfileLogs_EnglishAndZhCn()
    {
        foreach (var ruleId in new[]
        {
            "cp.s1.electron-app-logs",
            "cp.s1.vscode-logs",
            "cp.s1.jetbrains-logs"
        })
        {
            var advice = RecommendationAdvisor.ForRule(CreateRule(ruleId, RiskLevel.S1LowRisk));
            Assert.Equal(RecommendationLevel.Optional, advice.Recommendation);
            Assert.Equal("advice.app-profile-logs", advice.AdviceKey);
            Assert.Contains("Old app logs can be removed after the app is closed", advice.Reason, StringComparison.Ordinal);
            Assert.Contains("Historical troubleshooting logs may no longer be available", advice.PossibleImpact, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void RecommendationMapping_AppProfileCrashDiagnostics_EnglishAndZhCn()
    {
        foreach (var ruleId in new[]
        {
            "cp.s1.electron-app-crash-reports",
            "cp.s1.electron-app-crash-completed",
            "cp.s1.vscode-crash-reports",
            "cp.s1.vscode-crash-completed"
        })
        {
            var advice = RecommendationAdvisor.ForRule(CreateRule(ruleId, RiskLevel.S1LowRisk));
            Assert.Equal(RecommendationLevel.Optional, advice.Recommendation);
            Assert.Equal("advice.app-profile-crash-diagnostics", advice.AdviceKey);
            Assert.Contains("Old completed crash diagnostics can be removed after the app is closed", advice.Reason, StringComparison.Ordinal);
            Assert.Contains("Historical crash investigation data may no longer be available", advice.PossibleImpact, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void PackageManagers_ImpactTextMentionsRedownloadOrRebuild()
    {
        foreach (var ruleId in new[]
        {
            "cp.s1.nuget-http-cache",
            "cp.s1.nuget-global-packages",
            "cp.s1.npm-cache",
            "cp.s1.pnpm-store",
            "cp.s1.yarn-cache",
            "cp.s1.pip-cache",
            "cp.s1.cargo-registry-cache",
            "cp.s1.cargo-git-cache",
            "cp.s1.gradle-dependency-cache",
            "cp.s1.maven-repository-cache",
            "cp.s1.deno-cache",
            "cp.s1.bun-install-cache",
            "cp.s1.composer-cache",
            "cp.s1.go-cache"
        })
        {
            var advice = RecommendationAdvisor.ForRule(CreateRule(ruleId, RiskLevel.S1LowRisk));
            Assert.Equal(RecommendationLevel.Optional, advice.Recommendation);
            Assert.Equal("advice.package-manager-cache", advice.AdviceKey);
            Assert.True(
                advice.Reason.Contains("rebuild", StringComparison.OrdinalIgnoreCase)
                || advice.Reason.Contains("rebuilt", StringComparison.OrdinalIgnoreCase)
                || advice.Reason.Contains("recreate", StringComparison.OrdinalIgnoreCase),
                $"Reason should mention rebuild/recreate semantics for {ruleId}: {advice.Reason}");
            Assert.Contains("redownload", advice.PossibleImpact, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("network", advice.PossibleImpact, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void WindowsDiagnostics_ImpactTextMentionsTroubleshootingLoss()
    {
        foreach (var ruleId in new[]
        {
            "cp.s1.user-crash-dumps",
            "cp.s1.windows-error-reports",
            "cp.s1.windows-error-report-queue"
        })
        {
            var advice = RecommendationAdvisor.ForRule(CreateRule(ruleId, RiskLevel.S1LowRisk));
            Assert.Equal(RecommendationLevel.Optional, advice.Recommendation);
            Assert.True(
                advice.PossibleImpact.Contains("troubleshooting", StringComparison.OrdinalIgnoreCase)
                || advice.PossibleImpact.Contains("diagnostic", StringComparison.OrdinalIgnoreCase),
                $"Impact should mention troubleshooting/diagnostic loss for {ruleId}: {advice.PossibleImpact}");
        }
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
