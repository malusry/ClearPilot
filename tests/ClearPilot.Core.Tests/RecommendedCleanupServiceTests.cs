using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class RecommendedCleanupServiceTests
{
    [Fact]
    public void ScanReturnsOnlyS1Candidates()
    {
        using var workspace = TestWorkspace.Create();
        var s0Root = workspace.CreateDirectory("s0");
        var s1Root = workspace.CreateDirectory("s1");
        var s2Root = workspace.CreateDirectory("s2");
        workspace.CreateOldFile(Path.Combine("s0", "safe.tmp"), "s0");
        workspace.CreateOldFile(Path.Combine("s1", "recommended.tmp"), "s1");
        workspace.CreateOldFile(Path.Combine("s2", "review.tmp"), "s2");
        var service = CreateService(workspace.LogsPath);

        var candidates = service.Scan(
            [
                CreateRule("test.s0", RiskLevel.S0VeryLowRisk, s0Root),
                CreateRule("test.s1", RiskLevel.S1LowRisk, s1Root),
                CreateRule("test.s2", RiskLevel.S2ReviewRequired, s2Root)
            ],
            DateTimeOffset.UtcNow);

        var candidate = Assert.Single(candidates);
        Assert.Equal("test.s1", candidate.RuleId);
        Assert.Equal(RiskLevel.S1LowRisk, candidate.RiskLevel);
    }

    [Fact]
    public void CleanDeletesSelectedS1RulesOnly()
    {
        using var workspace = TestWorkspace.Create();
        var selectedRoot = workspace.CreateDirectory("selected");
        var unselectedRoot = workspace.CreateDirectory("unselected");
        var selectedFile = workspace.CreateOldFile(Path.Combine("selected", "cache.tmp"), "selected");
        var unselectedFile = workspace.CreateOldFile(Path.Combine("unselected", "cache.tmp"), "unselected");
        var service = CreateService(workspace.LogsPath);

        var result = service.Clean(
            [CreateRule("test.s1.selected", RiskLevel.S1LowRisk, selectedRoot)],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.False(File.Exists(selectedFile));
        Assert.True(File.Exists(unselectedFile));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(CleanupMode.RecommendedCleanup, result.Mode);
    }

    [Fact]
    public void CleanSkipsRulesThatAreNotS1()
    {
        using var workspace = TestWorkspace.Create();
        var s0Root = workspace.CreateDirectory("s0");
        var s2Root = workspace.CreateDirectory("s2");
        var s0File = workspace.CreateOldFile(Path.Combine("s0", "safe.tmp"), "s0");
        var s2File = workspace.CreateOldFile(Path.Combine("s2", "review.tmp"), "s2");
        var service = CreateService(workspace.LogsPath);

        var result = service.Clean(
            [
                CreateRule("test.s0", RiskLevel.S0VeryLowRisk, s0Root),
                CreateRule("test.s2", RiskLevel.S2ReviewRequired, s2Root)
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(s0File));
        Assert.True(File.Exists(s2File));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(2, result.SkippedCount);
    }

    private static RecommendedCleanupService CreateService(string logPath)
    {
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        var scanner = new CleanupScanner(protectedPathPolicy);
        var fileScanner = new CleanupFileScanner(protectedPathPolicy);
        var executor = new CleanupExecutor(fileScanner, new CleanupLogStore(logPath));
        return new RecommendedCleanupService(scanner, executor);
    }

    private static CleanupRule CreateRule(string ruleId, RiskLevel riskLevel, string root)
    {
        return new CleanupRule(
            ruleId,
            "Test files",
            riskLevel,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test rule.");
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
