using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceAdviceFormatterTests
{
    [Theory]
    [InlineData(DeepSpaceAdviceKey.VideoFile, ".mp4", "个人数据", "手动确认")]
    [InlineData(DeepSpaceAdviceKey.DiskImage, ".iso", "旧磁盘镜像", "人工复核")]
    [InlineData(DeepSpaceAdviceKey.NodeModules, "node_modules", "项目依赖目录", "先人工复核")]
    [InlineData(DeepSpaceAdviceKey.PythonVirtualEnvironment, ".venv", "虚拟环境", "先人工复核")]
    [InlineData(DeepSpaceAdviceKey.FrontendFrameworkOutput, ".next", "前端构建输出", "先人工复核")]
    public void ZhCnAdviceFormatterReturnsReadableExplanationAndAction(
        DeepSpaceAdviceKey adviceKey,
        string subject,
        string expectedExplanationFragment,
        string expectedActionFragment)
    {
        var item = CreateItem(
            adviceKey,
            subject,
            DeepSpaceItemType.LargeFile,
            "English explanation fallback",
            "English action fallback");

        var explanation = DeepSpaceAdviceFormatter.FormatExplanation(Language.SimplifiedChinese, item);
        var action = DeepSpaceAdviceFormatter.FormatSuggestedAction(Language.SimplifiedChinese, item);

        Assert.Contains(expectedExplanationFragment, explanation);
        Assert.Contains(expectedActionFragment, action);
        Assert.DoesNotContain("\uFFFD", explanation, StringComparison.Ordinal);
        Assert.DoesNotContain("\uFFFD", action, StringComparison.Ordinal);
    }

    [Fact]
    public void ZhCnWindowsSystemManagedAdviceIsReadableAndReadOnly()
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

        Assert.Contains("Windows 系统管理区域", explanation);
        Assert.Contains("仅分析，不删除", explanation);
        Assert.Contains("不要直接删除", action);
        Assert.Contains("影响 Windows 更新", impact);
        Assert.Contains("仅分析，不清理", safety);
    }

    [Fact]
    public void ZhCnGameLauncherReviewAdviceIsReadableAndReadOnly()
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
        Assert.Contains("仅分析，不删除", explanation);
        Assert.Contains("不要直接删除", action);
        Assert.Contains("着色器重编译", impact);
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
