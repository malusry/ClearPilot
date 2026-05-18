using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClearPilot.Core.Settings;

public sealed class SettingsStore
{
    public const string SettingsPathEnvironmentVariable = "CLEARPILOT_SETTINGS_PATH";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore(string settingsPath)
    {
        SettingsPath = settingsPath;
    }

    public string SettingsPath { get; }

    public static SettingsStore CreateDefault()
    {
        var overridePath = Environment.GetEnvironmentVariable(SettingsPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return new SettingsStore(overridePath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;

        return new SettingsStore(Path.Combine(basePath, "ClearPilot", "settings.json"));
    }

    public AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
        {
            return new AppSettings();
        }

        try
        {
            using var stream = File.OpenRead(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(stream, JsonOptions) ?? new AppSettings();
            settings.Normalize();
            return settings;
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();

        var directory = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(SettingsPath);
        JsonSerializer.Serialize(stream, settings, JsonOptions);
    }
}
