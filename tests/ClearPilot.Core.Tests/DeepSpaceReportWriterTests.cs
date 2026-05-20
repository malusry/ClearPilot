using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceReportWriterTests
{
    [Fact]
    public void RenderCreatesStructuredEnglishReportWithoutSuggestedActionField()
    {
        var result = CreateResult();

        var report = DeepSpaceReportWriter.Render(
            result,
            ["C:\\Users\\Example\\Downloads"],
            Language.English,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("# ClearPilot Deep Space Analysis Report", report);
        Assert.Contains("## At a Glance", report);
        Assert.Contains("| Metric | Value |", report);
        Assert.Contains("## Type Breakdown", report);
        Assert.Contains("## Top Space Sources", report);
        Assert.Contains("Decision", report);
        Assert.Contains("Decision reason", report);
        Assert.Contains("S2 REVIEW", report);
        Assert.Contains("Recommendation", report);
        Assert.Contains("Possible impact", report);
        Assert.Contains("Safety note", report);
        Assert.Contains("Analysis only", report);
        Assert.DoesNotContain("Suggested action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommended action", report, StringComparison.OrdinalIgnoreCase);
        Assert.False(report.Contains("\u001b[", StringComparison.Ordinal));
    }

    [Fact]
    public void RenderIncludesReadableChineseAndReadOnlyFramingWhenSimplifiedChineseIsSelected()
    {
        var result = CreateResult();

        var report = DeepSpaceReportWriter.Render(
            result,
            ["C:\\Users\\Example\\Downloads"],
            Language.SimplifiedChinese,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("# ClearPilot 深度空间分析报告", report);
        Assert.Contains("## 概览", report);
        Assert.Contains("| 指标 | 数值 |", report);
        Assert.Contains("| 排名 | 类型 | 大小 | 位置 |", report);
        Assert.Contains("结论", report);
        Assert.Contains("仅分析", report);
        Assert.Contains("不会删除文件", report);
        Assert.Contains("Downloads 仅用于存储占用了解", report);
        Assert.Contains("Desktop/Documents/Pictures/Videos/Music 默认不扫描", report);
        Assert.Contains("安全说明", report);
        Assert.DoesNotContain("建议操作", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Recommended action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suggested action", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u001b[", report, StringComparison.Ordinal);
        Assert.DoesNotContain("\uFFFD", report, StringComparison.Ordinal);
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

    [Fact]
    public void DeepSpace_Report_StatesReadOnlyNoDelete()
    {
        var result = CreateResult();
        var report = DeepSpaceReportWriter.Render(
            result,
            ["C:\\Users\\Example\\Downloads"],
            Language.English,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("Analysis only.", report);
        Assert.Contains("does not delete files in Deep Space", report);
        Assert.Contains("Downloads is scanned only for storage understanding", report);
        Assert.Contains("Desktop/Documents/Pictures/Videos/Music", report);
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
                RiskLevel.S2ReviewRequired,
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
                RiskLevel.S2ReviewRequired,
                "Launcher-managed game shader cache. ClearPilot reports it as review-only because cleanup can trigger expensive shader recompilation.",
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
