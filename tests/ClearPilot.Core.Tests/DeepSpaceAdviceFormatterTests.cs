using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceAdviceFormatterTests
{
    [Theory]
    [InlineData(DeepSpaceAdviceKey.VideoFile, ".mp4", "大型视频文件", "手动查看视频内容")]
    [InlineData(DeepSpaceAdviceKey.DiskImage, ".iso", "旧磁盘镜像", "挂载或检查镜像内容")]
    [InlineData(DeepSpaceAdviceKey.NodeModules, "node_modules", "Node.js 项目依赖目录", "手动删除 node_modules")]
    [InlineData(DeepSpaceAdviceKey.PythonVirtualEnvironment, ".venv", "Python 虚拟环境", "手动删除它")]
    [InlineData(DeepSpaceAdviceKey.FrontendFrameworkOutput, ".next", "前端框架构建缓存或输出目录", "优先使用框架清理命令")]
    public void ZhCnAdviceFormatterReturnsLocalizedExplanationAndAction(
        DeepSpaceAdviceKey adviceKey,
        string subject,
        string expectedExplanation,
        string expectedAction)
    {
        var item = CreateItem(
            adviceKey,
            subject,
            DeepSpaceItemType.LargeFile,
            "English explanation fallback",
            "English action fallback");

        var explanation = DeepSpaceAdviceFormatter.FormatExplanation(Language.SimplifiedChinese, item);
        var action = DeepSpaceAdviceFormatter.FormatSuggestedAction(Language.SimplifiedChinese, item);

        Assert.Contains(expectedExplanation, explanation);
        Assert.Contains(expectedAction, action);
    }

    [Fact]
    public void ZhCnWindowsSystemManagedAdviceIsLocalized()
    {
        var item = CreateItem(
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            "cp.s2.windows-update-download",
            DeepSpaceItemType.SystemManagedWindowsArea,
            "System-managed Windows cleanup area.",
            "Use Windows Settings / Storage Sense / Disk Cleanup.");

        var explanation = DeepSpaceAdviceFormatter.FormatExplanation(Language.SimplifiedChinese, item);
        var action = DeepSpaceAdviceFormatter.FormatSuggestedAction(Language.SimplifiedChinese, item);
        var impact = DeepSpaceAdviceFormatter.FormatPossibleImpact(Language.SimplifiedChinese, item, "Manual deletion can disrupt state.");
        var safety = DeepSpaceAdviceFormatter.FormatSafetyNote(Language.SimplifiedChinese, item, "Analysis only.");

        Assert.Contains("Windows 管理的系统缓存", explanation);
        Assert.Contains("仅分析，不会删除", explanation);
        Assert.Contains("请使用 Windows 设置、存储感知或磁盘清理", action);
        Assert.Contains("可能干扰 Windows 更新", impact);
        Assert.Contains("仅分析，不清理", safety);
    }

    [Fact]
    public void ZhCnGameLauncherReviewAdviceIsLocalized()
    {
        var item = CreateItem(
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            "cp.s2.steam-shadercache",
            DeepSpaceItemType.GameLauncherReviewArea,
            "Launcher-managed game shader cache.",
            "Review size and activity before cleanup.");

        var explanation = DeepSpaceAdviceFormatter.FormatExplanation(Language.SimplifiedChinese, item);
        var action = DeepSpaceAdviceFormatter.FormatSuggestedAction(Language.SimplifiedChinese, item);
        var impact = DeepSpaceAdviceFormatter.FormatPossibleImpact(Language.SimplifiedChinese, item, "Cleanup may impact launcher behavior.");
        var safety = DeepSpaceAdviceFormatter.FormatSafetyNote(Language.SimplifiedChinese, item, "Analysis only.");

        Assert.Contains("游戏启动器相关的复核项", explanation);
        Assert.Contains("下载状态", explanation);
        Assert.Contains("启动器关闭后", action);
        Assert.Contains("着色器", impact);
        Assert.Contains("仅分析，不清理", safety);
    }

    [Fact]
    public void EnglishAdviceFormatterRemainsUnchanged()
    {
        const string explanation = "English explanation fallback";
        const string action = "English action fallback";
        const string impact = "English impact fallback";
        const string safety = "English safety fallback";
        var item = CreateItem(
            DeepSpaceAdviceKey.WindowsSystemManagedArea,
            "cp.s2.windows-update-download",
            DeepSpaceItemType.SystemManagedWindowsArea,
            explanation,
            action);

        Assert.Equal(explanation, DeepSpaceAdviceFormatter.FormatExplanation(Language.English, item));
        Assert.Equal(action, DeepSpaceAdviceFormatter.FormatSuggestedAction(Language.English, item));
        Assert.Equal(impact, DeepSpaceAdviceFormatter.FormatPossibleImpact(Language.English, item, impact));
        Assert.Equal(safety, DeepSpaceAdviceFormatter.FormatSafetyNote(Language.English, item, safety));
    }

    private static DeepSpaceItem CreateItem(
        DeepSpaceAdviceKey adviceKey,
        string subjectOrTargetId,
        DeepSpaceItemType type,
        string explanation,
        string action)
    {
        return new DeepSpaceItem(
            type,
            @"C:\Users\Example\Test\item",
            1234,
            DateTimeOffset.UtcNow,
            RiskLevel.S2ReviewRequired,
            explanation,
            action,
            adviceKey,
            subjectOrTargetId,
            subjectOrTargetId,
            "test");
    }
}
