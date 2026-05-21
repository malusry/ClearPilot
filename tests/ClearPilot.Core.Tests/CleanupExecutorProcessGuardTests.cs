using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupExecutorProcessGuardTests
{
    [Fact]
    public void RunSkipsLauncherTargetWhenGuardProcessIsRunning()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("steam-cache");
        var filePath = workspace.CreateOldFile(Path.Combine("steam-cache", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s1.steam-httpcache",
            "Steam launcher cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test launcher cache cleanup rule.",
            LauncherName: "Steam",
            ProcessGuardNames: ["steam", "steamwebhelper"]);
        var executor = CreateExecutor(workspace.LogsPath, new StubProcessInspector(isRunning: true));

        var result = executor.Run(
            CleanupMode.RecommendedCleanup,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Steam", item.LauncherName);
        Assert.Equal("Blocked:LauncherRunning", item.ProcessGuardResult);
        Assert.Contains("launcher process is running", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(RecommendationLevel.Recommended, item.Recommendation);
        Assert.Equal(CleanupDecision.NotRecommendedToClean, item.CleanupDecision);
        Assert.Contains("app is running", item.CleanupDecisionReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RunDeletesLauncherTargetWhenGuardProcessIsNotRunning()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("steam-cache");
        var filePath = workspace.CreateOldFile(Path.Combine("steam-cache", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s1.steam-httpcache",
            "Steam launcher cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test launcher cache cleanup rule.",
            LauncherName: "Steam",
            ProcessGuardNames: ["steam", "steamwebhelper"]);
        var executor = CreateExecutor(workspace.LogsPath, new StubProcessInspector(isRunning: false));

        var result = executor.Run(
            CleanupMode.RecommendedCleanup,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.False(File.Exists(filePath));
        Assert.Equal(1, result.DeletedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Deleted, item.Action);
        Assert.Equal("Passed", item.ProcessGuardResult);
        Assert.Equal(CleanupDecision.RecommendedToClean, item.CleanupDecision);
    }

    [Fact]
    public void AppProfile_ProcessGuardHit_SkipsCleanup()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("discord-cache");
        var filePath = workspace.CreateOldFile(Path.Combine("discord-cache", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s1.electron-app-ui-cache",
            "Electron app UI caches",
            RiskLevel.S1LowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test app profile cache cleanup rule.",
            ProcessGuardNames: ["Discord.exe", "Slack.exe", "Teams.exe"]);
        var executor = CreateExecutor(workspace.LogsPath, new StubProcessInspector(isRunning: true));

        var result = executor.Run(
            CleanupMode.RecommendedCleanup,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Blocked:LauncherRunning", item.ProcessGuardResult);
        Assert.Equal(CleanupDecision.NotRecommendedToClean, item.CleanupDecision);
    }

    [Fact]
    public void AppProfile_ProcessGuardHit_DoesNotStopOrPrompt()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("vscode-cache");
        var filePath = workspace.CreateOldFile(Path.Combine("vscode-cache", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s1.vscode-cache",
            "Visual Studio Code cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test VS Code cache cleanup rule.",
            ProcessGuardNames: ["Code.exe", "Code - Insiders.exe", "VSCodium.exe"]);
        var executor = CreateExecutor(workspace.LogsPath, new StubProcessInspector(isRunning: true));

        var result = executor.Run(
            CleanupMode.RecommendedCleanup,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Blocked:LauncherRunning", item.ProcessGuardResult);
        Assert.DoesNotContain("stop", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("kill", item.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("close", item.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfilesV1_ProcessGuardHit_SkipOnly()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("app-profile-cache");
        var filePath = workspace.CreateOldFile(Path.Combine("app-profile-cache", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s1.electron-app-ui-cache",
            "Electron app UI caches",
            RiskLevel.S1LowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test app profile cache cleanup rule.",
            ProcessGuardNames: ["Discord.exe", "Slack.exe", "Teams.exe"]);
        var executor = CreateExecutor(workspace.LogsPath, new StubProcessInspector(isRunning: true));

        var result = executor.Run(
            CleanupMode.RecommendedCleanup,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S1LowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        Assert.Equal(0, result.DeletedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Blocked:LauncherRunning", item.ProcessGuardResult);
    }

    private static CleanupExecutor CreateExecutor(string logsPath, IProcessInspector processInspector)
    {
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        return new CleanupExecutor(
            new CleanupFileScanner(protectedPathPolicy),
            new CleanupLogStore(logsPath),
            new PathSafetyEngine(protectedPathPolicy),
            processInspector);
    }

    private sealed class StubProcessInspector(bool isRunning) : IProcessInspector
    {
        public bool IsAnyRunning(IReadOnlyList<string> processNames)
        {
            return processNames.Count > 0 && isRunning;
        }
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
            LogsPath = Path.Combine(root, "logs");
        }

        public string Root { get; }

        public string LogsPath { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateOldFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
