using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupLogStoreTests
{
    [Fact]
    public void WriteCreatesJsonLogFile()
    {
        var logDirectory = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"), "logs");
        var store = new CleanupLogStore(logDirectory);
        var log = new CleanupRunLog(
            Guid.NewGuid().ToString("N"),
            CleanupMode.QuickSafeClean,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DryRun: false,
            DeletedCount: 1,
            DeletedBytes: 5,
            DryRunCount: 0,
            DryRunBytes: 0,
            SkippedCount: 0,
            FailedCount: 0,
            [new CleanupItemResult(
                "test.s0",
                "Test",
                "",
                "NotApplicable",
                @"C:\Temp\file.tmp",
                5,
                CleanupItemAction.Deleted,
                Recommendation: RecommendationLevel.Recommended,
                CleanupDecision: CleanupDecision.RecommendedToClean,
                CleanupDecisionReason: "safe cache target",
                AdviceKey: "advice.test")]);

        var path = store.Write(log);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("\"Mode\": \"QuickSafeClean\"", content);
        Assert.Contains("\"Category\": \"Test\"", content);
        Assert.Contains("\"Recommendation\": \"Recommended\"", content);
        Assert.Contains("\"CleanupDecision\": \"RecommendedToClean\"", content);
        Assert.Contains("\"CleanupDecisionReason\": \"safe cache target\"", content);
        Assert.Contains("\"AdviceKey\": \"advice.test\"", content);
        Assert.Contains("\"DeletedCount\": 1", content);
        Assert.False(content.Contains("\u001b[", StringComparison.Ordinal));
    }

    [Fact]
    public void ReadRecentReturnsNewestLogsFirstAndSkipsCorruptLogs()
    {
        var logDirectory = CreateTempLogDirectory();
        var store = new CleanupLogStore(logDirectory);
        var older = CreateLog(DateTimeOffset.UtcNow.AddHours(-2), CleanupMode.QuickSafeClean, deletedBytes: 10);
        var newer = CreateLog(DateTimeOffset.UtcNow.AddHours(-1), CleanupMode.RecommendedCleanup, deletedBytes: 20);
        store.Write(older);
        store.Write(newer);
        File.WriteAllText(Path.Combine(logDirectory, "corrupt.json"), "{ not json");

        var entries = store.ReadRecent(maxCount: 10);

        Assert.Equal(2, entries.Count);
        Assert.Equal(CleanupMode.RecommendedCleanup, entries[0].Mode);
        Assert.Equal(20, entries[0].DeletedBytes);
        Assert.Equal(CleanupMode.QuickSafeClean, entries[1].Mode);
    }

    [Fact]
    public void ReadRecentRespectsMaxCount()
    {
        var logDirectory = CreateTempLogDirectory();
        var store = new CleanupLogStore(logDirectory);
        store.Write(CreateLog(DateTimeOffset.UtcNow.AddHours(-3), CleanupMode.QuickSafeClean, deletedBytes: 1));
        store.Write(CreateLog(DateTimeOffset.UtcNow.AddHours(-2), CleanupMode.QuickSafeClean, deletedBytes: 2));
        store.Write(CreateLog(DateTimeOffset.UtcNow.AddHours(-1), CleanupMode.QuickSafeClean, deletedBytes: 3));

        var entries = store.ReadRecent(maxCount: 2);

        Assert.Equal(2, entries.Count);
        Assert.Equal(3, entries[0].DeletedBytes);
        Assert.Equal(2, entries[1].DeletedBytes);
    }

    [Fact]
    public void DeleteLogsOlderThanRemovesOldLogsAndKeepsRecentLogs()
    {
        var logDirectory = CreateTempLogDirectory();
        var store = new CleanupLogStore(logDirectory);
        var oldPath = store.Write(CreateLog(DateTimeOffset.UtcNow.AddDays(-10), CleanupMode.QuickSafeClean, deletedBytes: 1));
        var recentPath = store.Write(CreateLog(DateTimeOffset.UtcNow.AddDays(-1), CleanupMode.RecommendedCleanup, deletedBytes: 2));

        var deletedCount = store.DeleteLogsOlderThan(DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(oldPath));
        Assert.True(File.Exists(recentPath));
    }

    [Fact]
    public void DeleteLogsOlderThanCanRemoveOldCorruptLogsByFileTimestamp()
    {
        var logDirectory = CreateTempLogDirectory();
        Directory.CreateDirectory(logDirectory);
        var corruptPath = Path.Combine(logDirectory, "corrupt.json");
        File.WriteAllText(corruptPath, "{ not json");
        File.SetLastWriteTimeUtc(corruptPath, DateTime.UtcNow.AddDays(-10));
        var store = new CleanupLogStore(logDirectory);

        var deletedCount = store.DeleteLogsOlderThan(DateTimeOffset.UtcNow.AddDays(-7));

        Assert.Equal(1, deletedCount);
        Assert.False(File.Exists(corruptPath));
    }

    private static string CreateTempLogDirectory()
    {
        return Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"), "logs");
    }

    private static CleanupRunLog CreateLog(DateTimeOffset startedAt, CleanupMode mode, long deletedBytes)
    {
        return new CleanupRunLog(
            Guid.NewGuid().ToString("N"),
            mode,
            startedAt,
            startedAt.AddSeconds(2),
            DryRun: false,
            DeletedCount: deletedBytes > 0 ? 1 : 0,
            DeletedBytes: deletedBytes,
            DryRunCount: 0,
            DryRunBytes: 0,
            SkippedCount: 0,
            FailedCount: 0,
            [new CleanupItemResult(
                "test.rule",
                "Test",
                "",
                "NotApplicable",
                @"C:\Temp\file.tmp",
                deletedBytes,
                CleanupItemAction.Deleted,
                Recommendation: RecommendationLevel.Optional,
                CleanupDecision: CleanupDecision.NotRecommendedToClean,
                CleanupDecisionReason: "diagnostic data",
                AdviceKey: "advice.test")]);
    }
}
