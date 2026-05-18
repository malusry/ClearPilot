using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class QuickSafeCleanerTests
{
    [Fact]
    public void RunDeletesOnlyS0Files()
    {
        using var workspace = TestWorkspace.Create();
        var s0Root = workspace.CreateDirectory("s0");
        var s1Root = workspace.CreateDirectory("s1");
        var s0File = workspace.CreateOldFile(Path.Combine("s0", "delete.tmp"), "12345");
        var s1File = workspace.CreateOldFile(Path.Combine("s1", "keep.tmp"), "12345");
        var cleaner = CreateCleaner(workspace.LogsPath);

        var result = cleaner.Run(
            [
                CreateRule("test.s0", RiskLevel.S0VeryLowRisk, s0Root),
                CreateRule("test.s1", RiskLevel.S1LowRisk, s1Root)
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.False(File.Exists(s0File));
        Assert.True(File.Exists(s1File));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.NotNull(result.LogPath);
        Assert.True(File.Exists(result.LogPath));
    }

    [Fact]
    public void RunInDryRunModeDoesNotDeleteFiles()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("s0");
        var file = workspace.CreateOldFile(Path.Combine("s0", "would-delete.tmp"), "12345");
        var cleaner = CreateCleaner(workspace.LogsPath);

        var result = cleaner.Run([CreateRule("test.s0", RiskLevel.S0VeryLowRisk, root)], dryRun: true, now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(file));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.DryRunCount);
        Assert.Equal(5, result.DryRunBytes);
    }

    [Fact]
    public void RunSkipsNewFilesBecauseRuleAgeThresholdStillApplies()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("s0");
        var file = workspace.CreateFile(Path.Combine("s0", "new.tmp"), "12345");
        var cleaner = CreateCleaner(workspace.LogsPath);

        var result = cleaner.Run([CreateRule("test.s0", RiskLevel.S0VeryLowRisk, root)], dryRun: false, now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(file));
        Assert.Empty(result.Items);
    }

    [Fact]
    public void RunRecordsFailureForLockedFileAndContinues()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("s0");
        var lockedFile = workspace.CreateOldFile(Path.Combine("s0", "locked.tmp"), "12345");
        var deletableFile = workspace.CreateOldFile(Path.Combine("s0", "delete.tmp"), "12345");
        var cleaner = CreateCleaner(workspace.LogsPath);

        using var stream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var result = cleaner.Run([CreateRule("test.s0", RiskLevel.S0VeryLowRisk, root)], dryRun: false, now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(lockedFile));
        Assert.False(File.Exists(deletableFile));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(1, result.FailedCount);
    }

    private static QuickSafeCleaner CreateCleaner(string logPath)
    {
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        return new QuickSafeCleaner(
            new CleanupFileScanner(protectedPathPolicy),
            new CleanupLogStore(logPath));
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
