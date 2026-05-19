using ClearPilot.Core.Localization;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class MessageCatalogTests
{
    [Fact]
    public void EnglishCatalogProvidesDefaultMainMenuLabels()
    {
        var catalog = MessageCatalog.For(Language.English);

        Assert.Equal(Language.English, catalog.Language);
        Assert.Equal("Quick Safe Clean", catalog.Get(StringKey.MainMenuQuickSafeClean));
        Assert.Equal("Settings", catalog.Get(StringKey.MainMenuSettings));
        Assert.Contains("affects the entire Recycle Bin", catalog.Get(StringKey.SettingsRecycleBinWarning));
    }

    [Fact]
    public void SimplifiedChineseCatalogProvidesMainMenuAndSafetyLabels()
    {
        var catalog = MessageCatalog.For(Language.SimplifiedChinese);

        Assert.Equal(Language.SimplifiedChinese, catalog.Language);
        Assert.Equal("快速安全清理", catalog.Get(StringKey.MainMenuQuickSafeClean));
        Assert.Equal("设置", catalog.Get(StringKey.MainMenuSettings));
        Assert.Equal("仅分析，不清理：不会执行删除。", catalog.Get(StringKey.DeepAnalysisReviewOnlyNotice));
        Assert.Equal("操作：选择项目编号后，必须明确确认。默认不执行。", catalog.Get(StringKey.RecommendedActionHint));
    }
}
