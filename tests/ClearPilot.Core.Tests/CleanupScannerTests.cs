using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupScannerTests
{
    [Fact]
    public void ScanReturnsCandidateWithEstimatedSizeAndFileCount()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("temp");
        var oldFile = workspace.CreateFile(Path.Combine("temp", "old.tmp"), "12345");
        _ = workspace.CreateFile(Path.Combine("temp", "old.log"), "ignored");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-2));

        var rule = new CleanupRule(
            "test.s0.temp",
            "Test temp",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test explanation.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        var candidate = Assert.Single(candidates);
        Assert.Equal("test.s0.temp", candidate.RuleId);
        Assert.Equal(root, candidate.Path);
        Assert.Equal(5, candidate.EstimatedBytes);
        Assert.Equal(1, candidate.FileCount);
        Assert.Equal(RiskLevel.S0VeryLowRisk, candidate.RiskLevel);
        Assert.Equal(RecommendationLevel.Recommended, candidate.Recommendation);
        Assert.Equal(CleanupDecision.RecommendedToClean, candidate.CleanupDecision);
        Assert.False(string.IsNullOrWhiteSpace(candidate.CleanupDecisionReason));
        Assert.False(string.IsNullOrWhiteSpace(candidate.AdviceKey));
    }

    [Fact]
    public void ScanSkipsFilesNewerThanMinimumAge()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("temp");
        _ = workspace.CreateFile(Path.Combine("temp", "new.tmp"), "12345");
        var rule = new CleanupRule(
            "test.s0.temp",
            "Test temp",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test explanation.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ScanSkipsExcludedPathSegments()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("browser");
        var cacheFile = workspace.CreateFile(Path.Combine("browser", "Cache", "cache.bin"), "cache");
        var cookieFile = workspace.CreateFile(Path.Combine("browser", "Cookies", "cookie.bin"), "cookie");
        File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(cookieFile, DateTime.UtcNow.AddDays(-2));
        var rule = new CleanupRule(
            "test.s1.browser-cache",
            "Browser cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            ["Cookies"],
            TimeSpan.FromDays(1),
            "Browser cache only.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        var candidate = Assert.Single(candidates);
        Assert.Equal(1, candidate.FileCount);
        Assert.Equal(5, candidate.EstimatedBytes);
    }

    [Fact]
    public void ScanDoesNotReturnCandidatesUnderBlockedRoots()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("blocked");
        var oldFile = workspace.CreateFile(Path.Combine("blocked", "old.tmp"), "12345");
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-2));
        var rule = new CleanupRule(
            "test.bad-rule",
            "Bad rule",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*"],
            [],
            null,
            "This should be blocked.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([root]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        Assert.Empty(candidates);
    }

    [Fact]
    public void ScanSkipsClearPilotInternalArtifactsByFileName()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("temp");
        var internalLog = workspace.CreateOldFile(Path.Combine("temp", "20260517-QuickSafeClean.json"), "internal");
        var cacheFile = workspace.CreateOldFile(Path.Combine("temp", "cache.json"), "cache");
        var rule = new CleanupRule(
            "test.s1.json-cache",
            "JSON cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*.json"],
            [],
            TimeSpan.FromDays(1),
            "Test cache.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        var candidate = Assert.Single(candidates);
        Assert.Equal(new FileInfo(cacheFile).Length, candidate.EstimatedBytes);
        Assert.Equal(1, candidate.FileCount);
        Assert.True(File.Exists(internalLog));
    }

    [Fact]
    public void ScanGroupsMultipleRootsFromSameRuleIntoOneCandidate()
    {
        using var workspace = TestWorkspace.Create();
        var firstRoot = workspace.CreateDirectory("first");
        var secondRoot = workspace.CreateDirectory("second");
        workspace.CreateOldFile(Path.Combine("first", "one.tmp"), "111");
        workspace.CreateOldFile(Path.Combine("second", "two.tmp"), "2222");
        var rule = new CleanupRule(
            "test.s1.multi-root",
            "Multi-root cache",
            RiskLevel.S1LowRisk,
            [firstRoot, secondRoot],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "Test explanation.");
        var scanner = new CleanupScanner(new ProtectedPathPolicy([]));

        var candidates = scanner.Scan([rule], DateTimeOffset.UtcNow);

        var candidate = Assert.Single(candidates);
        Assert.Equal("test.s1.multi-root", candidate.RuleId);
        Assert.Equal(2, candidate.FileCount);
        Assert.Equal(7, candidate.EstimatedBytes);
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

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

        public string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public string CreateOldFile(string relativePath, string content)
        {
            var path = CreateFile(relativePath, content);
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
