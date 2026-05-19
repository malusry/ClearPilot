using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupExecutorRevalidationTests
{
    [Fact]
    public void RunSkipsDeletionWhenPathBecomesUnsafeDuringRevalidation()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("cache");
        var filePath = workspace.CreateOldFile(Path.Combine("cache", "cache.tmp"), "123");
        var rule = CreateRule("test.s0", RiskLevel.S0VeryLowRisk, root);
        var engine = new StubPathSafetyEngine(revalidationResultCode: "MissingPath", revalidationReason: "Path no longer exists.");
        var executor = new CleanupExecutor(
            new CleanupFileScanner(new ProtectedPathPolicy([])),
            new CleanupLogStore(workspace.LogsPath),
            engine);

        var result = executor.Run(
            CleanupMode.QuickSafeClean,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S0VeryLowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Path no longer exists.", item.Message);
        Assert.NotNull(item.SafetyDecision);
        Assert.Equal("Blocked:MissingPath", item.SafetyDecision!.RevalidationResult);
        Assert.Equal("Path no longer exists.", item.SafetyDecision.SkippedReason);
    }

    [Fact]
    public void RunSkipsDeletionWhenPathBecomesReparsePointDuringRevalidation()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("cache");
        var filePath = workspace.CreateOldFile(Path.Combine("cache", "cache.tmp"), "123");
        var rule = CreateRule("test.s0", RiskLevel.S0VeryLowRisk, root);
        var engine = new StubPathSafetyEngine(
            revalidationResultCode: "ReparsePoint",
            revalidationReason: "Symlinks, junctions, and reparse points are blocked.");
        var executor = new CleanupExecutor(
            new CleanupFileScanner(new ProtectedPathPolicy([])),
            new CleanupLogStore(workspace.LogsPath),
            engine);

        var result = executor.Run(
            CleanupMode.QuickSafeClean,
            [rule],
            new HashSet<RiskLevel> { RiskLevel.S0VeryLowRisk },
            dryRun: false,
            now: DateTimeOffset.UtcNow,
            disallowedRiskMessage: "risk");

        Assert.True(File.Exists(filePath));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        var item = Assert.Single(result.Items);
        Assert.Equal(CleanupItemAction.Skipped, item.Action);
        Assert.Equal("Symlinks, junctions, and reparse points are blocked.", item.Message);
        Assert.NotNull(item.SafetyDecision);
        Assert.Equal("Blocked:ReparsePoint", item.SafetyDecision!.RevalidationResult);
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

    private sealed class StubPathSafetyEngine : PathSafetyEngine
    {
        private readonly string revalidationResultCode;
        private readonly string revalidationReason;

        public StubPathSafetyEngine(string revalidationResultCode, string revalidationReason)
            : base(new ProtectedPathPolicy([]))
        {
            this.revalidationResultCode = revalidationResultCode;
            this.revalidationReason = revalidationReason;
        }

        public override PathSafetyDecision ValidateRoot(string rootPath, KnownSafeCacheRootWhitelist whitelist)
        {
            return PathSafetyDecision.Allowed(rootPath, Path.GetFullPath(rootPath));
        }

        public override PathSafetyDecision ValidateCandidate(string candidatePath, string cleanupRoot, KnownSafeCacheRootWhitelist whitelist)
        {
            return PathSafetyDecision.Allowed(candidatePath, Path.GetFullPath(candidatePath));
        }

        public override PathSafetyDecision RevalidateCandidate(
            string candidatePath,
            string cleanupRoot,
            KnownSafeCacheRootWhitelist whitelist,
            string expectedCanonicalPath)
        {
            return PathSafetyDecision.Blocked(
                candidatePath,
                Path.GetFullPath(candidatePath),
                allowlistAllowed: true,
                denylistAllowed: true,
                resultCode: revalidationResultCode,
                reason: revalidationReason);
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
