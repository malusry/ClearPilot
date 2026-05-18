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
    public void SimplifiedChineseCatalogProvidesMainMenuLabels()
    {
        var catalog = MessageCatalog.For(Language.SimplifiedChinese);

        Assert.Equal(Language.SimplifiedChinese, catalog.Language);
        Assert.Equal("快速安全清理", catalog.Get(StringKey.MainMenuQuickSafeClean));
        Assert.Equal("设置", catalog.Get(StringKey.MainMenuSettings));
    }
}
