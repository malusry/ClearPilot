using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceReportWriterTests
{
    [Fact]
    public void RenderCreatesStructuredEnglishReportWithVisualBreakdown()
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
        Assert.Contains("`████", report);
        Assert.Contains("## Top Space Sources", report);
        Assert.Contains("S2 REVIEW", report);
        Assert.Contains("Analysis only", report);
    }

    [Fact]
    public void RenderLocalizesExplanationsAndSuggestedActionsInChinese()
    {
        var result = CreateResult();

        var report = DeepSpaceReportWriter.Render(
            result,
            ["C:\\Users\\Example\\Downloads"],
            Language.SimplifiedChinese,
            new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero));

        Assert.Contains("# ClearPilot 深度空间分析报告", report);
        Assert.Contains("## 概览", report);
        Assert.Contains("| 指标 | 值 |", report);
        Assert.Contains("| 排名 | 类型 | 大小 | 位置 |", report);
        Assert.Contains("视频通常是真实个人数据", report);
        Assert.Contains("外置存储", report);
        Assert.Contains("仅分析", report);
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
        Assert.Contains("Deep Space Analysis Report", File.ReadAllText(path));
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
                ".iso")
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
