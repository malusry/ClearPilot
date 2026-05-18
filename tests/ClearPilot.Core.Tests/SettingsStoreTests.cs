using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class SettingsStoreTests
{
    [Fact]
    public void LoadReturnsDefaultsWhenFileDoesNotExist()
    {
        var store = new SettingsStore(CreateTempSettingsPath());

        var settings = store.Load();

        Assert.Equal(Language.English, settings.Language);
        Assert.Equal(AppSettings.DefaultLogRetentionDays, settings.LogRetentionDays);
        Assert.False(settings.AutoEmptyRecycleBin);
        Assert.False(settings.DryRun);
    }

    [Fact]
    public void SaveAndLoadRoundTripsSettings()
    {
        var path = CreateTempSettingsPath();
        var store = new SettingsStore(path);
        var settings = new AppSettings
        {
            Language = Language.SimplifiedChinese,
            LogRetentionDays = 14,
            AutoEmptyRecycleBin = true,
            DryRun = true
        };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(Language.SimplifiedChinese, loaded.Language);
        Assert.Equal(14, loaded.LogRetentionDays);
        Assert.True(loaded.AutoEmptyRecycleBin);
        Assert.True(loaded.DryRun);
    }

    [Theory]
    [InlineData(-10, AppSettings.MinimumLogRetentionDays)]
    [InlineData(0, AppSettings.MinimumLogRetentionDays)]
    [InlineData(999, AppSettings.MaximumLogRetentionDays)]
    public void SaveNormalizesLogRetentionDays(int requestedDays, int expectedDays)
    {
        var store = new SettingsStore(CreateTempSettingsPath());
        var settings = new AppSettings { LogRetentionDays = requestedDays };

        store.Save(settings);
        var loaded = store.Load();

        Assert.Equal(expectedDays, loaded.LogRetentionDays);
    }

    private static string CreateTempSettingsPath()
    {
        return Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"), "settings.json");
    }
}
