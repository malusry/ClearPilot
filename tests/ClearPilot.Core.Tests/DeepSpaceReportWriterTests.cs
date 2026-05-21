using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceReportWriterTests
{
    [Fact]
    public void ReportsV2_UsesDecisionReasonImpactRiskFields()
    {
        var report = RenderReport(Language.English);

        Assert.Contains("Decision", report);
        Assert.Contains("Reason", report);
        Assert.Contains("Possible impact if cleaned", report);
        Assert.Contains("Expected reclaim", report);
        Assert.Contains("Risk", report);
        Assert.Contains("Safety note", report);
    }

    [Fact]
    public void ReportsV2_DoesNotUseSuggestedActionPrimaryField()
    {
        var report = RenderReport(Language.English);

        Assert.DoesNotContain("Suggested action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommended action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("建议操作", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsV2_SeparatesCleanedSkippedFailed()
    {
        var report = RenderReport(Language.English);

        Assert.Contains("## Execution Status", report);
        Assert.Contains("| Status | Count |", report);
        Assert.Contains("| Cleaned | 0 |", report);
        Assert.Contains("| Skipped | 0 |", report);
        Assert.Contains("| Failed | 0 |", report);
        Assert.Contains("| Intentionally untouched |", report);
    }

    [Fact]
    public void ReportsV2_SeparatesRecommendedNotRecommendedAnalysisOnlyBlocked()
    {
        var report = RenderReport(Language.English);

        Assert.Contains("## Decision Breakdown", report);
        Assert.Contains("| Decision | Count |", report);
        Assert.Contains("| Recommended to clean |", report);
        Assert.Contains("| Not recommended to clean |", report);
        Assert.Contains("| Analysis only, do not clean |", report);
        Assert.Contains("| Blocked |", report);
        Assert.Contains("| Intentionally untouched |", report);
    }

    [Fact]
    public void ReportsV2_DeepSpaceReport_IsReadOnlyNoDelete()
    {
        var report = RenderReport(Language.English);

        Assert.Contains("Analysis only.", report);
        Assert.Contains("does not delete files in Deep Space", report);
        Assert.Contains("Downloads is scanned only for storage understanding", report);
        Assert.Contains("Desktop/Documents/Pictures/Videos/Music", report);
        Assert.Contains("blocked; left unchanged", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ZoomProfile_ReportStatesReadOnlyEvidence()
    {
        var result = new DeepSpaceAnalysisResult(
            [
                new DeepSpaceItem(
                    DeepSpaceItemType.SystemManagedWindowsArea,
                    "C:\\Users\\Example\\AppData\\Roaming\\Zoom",
                    256L * 1024 * 1024,
                    new DateTimeOffset(2026, 5, 18, 8, 30, 0, TimeSpan.Zero),
                    RiskLevel.S2ReviewRequired,
                    "Zoom app data may include logs, cache, meeting diagnostics, and app state. ClearPilot reports size only for evidence and does not clean Zoom data in v0.4.",
                    "Review only. Keep recordings, account/session data, settings, and databases unchanged unless Zoom guidance explicitly recommends maintenance.",
                    DeepSpaceAdviceKey.WindowsSystemManagedArea,
                    "cp.s2.zoom-appdata",
                    "cp.s2.zoom-appdata",
                    "Zoom app data (analysis-only evidence)")
            ],
            new DeepSpaceAnalysisSummary(
                ScannedRootCount: 1,
                ScannedDirectoryCount: 1,
                ScannedFileCount: 4,
                FindingCount: 1,
                FindingBytes: 256L * 1024 * 1024));

        var report = DeepSpaceReportWriter.Render(
            result,
            ["C:\\Users\\Example\\AppData\\Roaming\\Zoom"],
            Language.English,
            new DateTimeOffset(2026, 5, 21, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("Zoom", report, StringComparison.Ordinal);
        Assert.Contains("Zoom app data may include logs, cache, meeting diagnostics, and app state.", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not clean Zoom data in v0.4", report, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Analysis only.", report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReportsV2_ZhCn_UsesReadableSafetyLabels()
    {
        var report = RenderReport(Language.SimplifiedChinese);

        Assert.Contains("执行状态", report, StringComparison.Ordinal);
        Assert.Contains("已清理", report, StringComparison.Ordinal);
        Assert.Contains("已跳过", report, StringComparison.Ordinal);
        Assert.Contains("失败", report, StringComparison.Ordinal);
        Assert.Contains("有意保持不变", report, StringComparison.Ordinal);
        Assert.Contains("结论", report, StringComparison.Ordinal);
        Assert.Contains("原因", report, StringComparison.Ordinal);
        Assert.Contains("清理后的可能影响", report, StringComparison.Ordinal);
        Assert.Contains("预计可释放", report, StringComparison.Ordinal);
        Assert.Contains("风险", report, StringComparison.Ordinal);
        Assert.Contains("安全说明", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsV2_ZhCn_BlockedStatesNeverCleanedInAnyMode()
    {
        var report = RenderReport(Language.SimplifiedChinese);

        Assert.Contains("任何模式下都不会清理", report, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsV2_DoesNotExposeLegacyPrimaryFields()
    {
        var report = RenderReport(Language.English);
        var zhReport = RenderReport(Language.SimplifiedChinese);

        Assert.DoesNotContain("Recommendation:", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Advice key:", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suggested action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommended action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("建议操作", zhReport, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportsV2_ZhCn_NoMojibake()
    {
        var report = RenderReport(Language.SimplifiedChinese);

        Assert.DoesNotContain("\uFFFD", report, StringComparison.Ordinal);
        Assert.DoesNotContain("娣", report, StringComparison.Ordinal);
        Assert.DoesNotContain("绌", report, StringComparison.Ordinal);
        Assert.DoesNotContain("鍒", report, StringComparison.Ordinal);
        Assert.DoesNotContain("銆", report, StringComparison.Ordinal);
        Assert.DoesNotContain("涓", report, StringComparison.Ordinal);
        Assert.DoesNotContain("璺", report, StringComparison.Ordinal);
        Assert.DoesNotContain("緞", report, StringComparison.Ordinal);
    }

    [Fact]
    public void WriteStoresMarkdownReportInConfiguredDirectory()
    {
        using var workspace = TestWorkspace.Create();
        var writer = new DeepSpaceReportWriter(workspace.Root);

        var path = writer.Write(
            CreateResult(),
            ["C:\\Users\\Example\\Downloads"],
            Language.English,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.True(File.Exists(path));
        Assert.EndsWith(".md", path, StringComparison.OrdinalIgnoreCase);
        var content = File.ReadAllText(path);
        Assert.Contains("Deep Space Analysis Report", content);
        Assert.False(content.Contains("\u001b[", StringComparison.Ordinal));
    }

    private static string RenderReport(Language language)
    {
        return DeepSpaceReportWriter.Render(
            CreateResult(),
            ["C:\\Users\\Example\\Downloads"],
            language,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));
    }

    private static DeepSpaceAnalysisResult CreateResult()
    {
        var items = new[]
        {
            new DeepSpaceItem(
                DeepSpaceItemType.LargeFile,
                "C:\\Users\\Example\\Downloads\\clip.mp4",
                4L * 1024 * 1024 * 1024,
                new DateTimeOffset(2026, 5, 16, 8, 30, 0, TimeSpan.Zero),
                RiskLevel.S2ReviewRequired,
                "Large video file in a user-controlled location. Video files are often real personal data, not cache.",
                "Review the video manually and consider moving it to external storage or an archive drive instead of deleting it.",
                DeepSpaceAdviceKey.VideoFile,
                ".mp4"),
            new DeepSpaceItem(
                DeepSpaceItemType.OldArchiveOrInstaller,
                "C:\\Users\\Example\\Downloads\\installer.iso",
                2L * 1024 * 1024 * 1024,
                new DateTimeOffset(2026, 4, 1, 8, 30, 0, TimeSpan.Zero),
                RiskLevel.S3DoNotCleanAutomatically,
                "Old disk image. It may be an installer image, operating system image, or archived media that still matters.",
                "Mount or inspect the image if unsure, then archive it elsewhere or remove it manually only after confirming it is no longer needed.",
                DeepSpaceAdviceKey.DiskImage,
                ".iso"),
            new DeepSpaceItem(
                DeepSpaceItemType.SystemManagedWindowsArea,
                "C:\\Windows\\SoftwareDistribution\\Download",
                512L * 1024 * 1024,
                new DateTimeOffset(2026, 5, 10, 8, 30, 0, TimeSpan.Zero),
                RiskLevel.S2ReviewRequired,
                "System-managed Windows cleanup area. ClearPilot reports this as review-only and does not delete it.",
                "Use Windows Settings Storage, Storage Sense, Disk Cleanup, or built-in maintenance tools instead of direct deletion.",
                DeepSpaceAdviceKey.WindowsSystemManagedArea,
                "cp.s2.windows-update-download",
                "cp.s2.windows-update-download",
                "Windows Update download cache (analysis-only)"),
            new DeepSpaceItem(
                DeepSpaceItemType.GameLauncherReviewArea,
                "C:\\Program Files (x86)\\Steam\\steamapps\\shadercache",
                768L * 1024 * 1024,
                new DateTimeOffset(2026, 5, 12, 8, 30, 0, TimeSpan.Zero),
                RiskLevel.Blocked,
                "Launcher-managed game shader cache. ClearPilot reports it as blocked by policy.",
                "Review size and recent activity first. If you choose to clean it, use Steam or launcher-native maintenance while Steam is closed.",
                DeepSpaceAdviceKey.WindowsSystemManagedArea,
                "cp.s2.steam-shadercache",
                "cp.s2.steam-shadercache",
                "Steam shader cache (analysis-only)")
        };

        return new DeepSpaceAnalysisResult(
            items,
            new DeepSpaceAnalysisSummary(
                ScannedRootCount: 1,
                ScannedDirectoryCount: 3,
                ScannedFileCount: 12,
                FindingCount: items.Length,
                FindingBytes: items.Sum(item => item.SizeBytes)));
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
