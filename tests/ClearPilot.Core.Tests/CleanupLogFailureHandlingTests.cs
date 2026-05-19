using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupLogFailureHandlingTests
{
    [Fact]
    public void CleanupContinuesWhenPersistentLogWriteFails()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("s0");
        var file = workspace.CreateOldFile(Path.Combine("s0", "cache.tmp"), "123");
        var rule = new CleanupRule(
            "cp.s0.user-temp",
            "Current user temporary files",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*.tmp"],
            [],
            TimeSpan.FromDays(1),
            "test");

        var fakeLogPathFile = workspace.CreateFile("not-a-directory.log-store", "placeholder");
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        var cleaner = new QuickSafeCleaner(
            new CleanupFileScanner(protectedPathPolicy),
            new CleanupLogStore(fakeLogPathFile),
            new PathSafetyEngine(protectedPathPolicy));

        var result = cleaner.Run([rule], dryRun: false, now: DateTimeOffset.UtcNow);

        Assert.False(File.Exists(file));
        Assert.Equal(1, result.DeletedCount);
        Assert.Null(result.LogPath);
        Assert.False(string.IsNullOrWhiteSpace(result.LogError));
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

        public string CreateOldFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            return path;
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
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
