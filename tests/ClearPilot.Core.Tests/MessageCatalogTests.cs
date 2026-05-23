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
    public void SimplifiedChineseCatalogProvidesMainMenuAndDeepSpaceSafetyLabels()
    {
        var catalog = MessageCatalog.For(Language.SimplifiedChinese);

        Assert.Equal(Language.SimplifiedChinese, catalog.Language);
        Assert.Equal("\u5FEB\u901F\u5B89\u5168\u6E05\u7406", catalog.Get(StringKey.MainMenuQuickSafeClean));
        Assert.Equal("\u8BBE\u7F6E", catalog.Get(StringKey.MainMenuSettings));
        Assert.Contains("\u4EC5\u5206\u6790", catalog.Get(StringKey.DeepAnalysisReviewOnlyNotice), StringComparison.Ordinal);
        Assert.Contains("Downloads", catalog.Get(StringKey.DeepAnalysisReviewOnlyNotice), StringComparison.Ordinal);
        Assert.Contains("\u8F93\u5165 0 \u53D6\u6D88", catalog.Get(StringKey.RecommendedActionHint), StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSafeCatalogProvidesBoundaryAndFailurePolicyLabels()
    {
        var english = MessageCatalog.For(Language.English);
        var zhCn = MessageCatalog.For(Language.SimplifiedChinese);

        Assert.Contains("S0-only", english.Get(StringKey.QuickSafeCleanBoundaryS0Only), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not retry with elevated privileges", english.Get(StringKey.QuickSafeCleanFailureNoElevation), StringComparison.OrdinalIgnoreCase);

        Assert.Contains("\u4EC5 S0", zhCn.Get(StringKey.QuickSafeCleanBoundaryS0Only), StringComparison.Ordinal);
        Assert.Contains("\u4E0D\u4F1A\u4F7F\u7528\u63D0\u5347\u6743\u9650\u91CD\u8BD5", zhCn.Get(StringKey.QuickSafeCleanFailureNoElevation), StringComparison.Ordinal);
    }
}
