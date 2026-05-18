using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClearPilot.Core.Logging;

public sealed class CleanupLogStore
{
    public const string LogDirectoryEnvironmentVariable = "CLEARPILOT_LOG_DIR";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public CleanupLogStore(string logDirectory)
    {
        LogDirectory = logDirectory;
    }

    public string LogDirectory { get; }

    public static CleanupLogStore CreateDefault()
    {
        var overridePath = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return new CleanupLogStore(overridePath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;

        return new CleanupLogStore(Path.Combine(basePath, "ClearPilot", "logs"));
    }

    public string Write(CleanupRunLog log)
    {
        Directory.CreateDirectory(LogDirectory);

        var fileName = $"{log.StartedAt.UtcDateTime:yyyyMMdd-HHmmss}-{log.Mode}.json";
        var path = Path.Combine(LogDirectory, fileName);

        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, log, JsonOptions);
        return path;
    }

    public IReadOnlyList<CleanupLogEntry> ReadRecent(int maxCount)
    {
        if (!Directory.Exists(LogDirectory) || maxCount <= 0)
        {
            return [];
        }

        return EnumerateLogFiles()
            .Select(ReadEntry)
            .Where(entry => entry is not null)
            .Cast<CleanupLogEntry>()
            .OrderByDescending(entry => entry.StartedAt)
            .ThenByDescending(entry => entry.Path, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToArray();
    }

    public int DeleteLogsOlderThan(DateTimeOffset cutoff)
    {
        if (!Directory.Exists(LogDirectory))
        {
            return 0;
        }

        var deletedCount = 0;
        foreach (var path in EnumerateLogFiles())
        {
            var startedAt = ReadEntry(path)?.StartedAt ?? GetFileTimestamp(path);
            if (startedAt >= cutoff)
            {
                continue;
            }

            try
            {
                File.Delete(path);
                deletedCount++;
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return deletedCount;
    }

    private IReadOnlyList<string> EnumerateLogFiles()
    {
        try
        {
            return Directory.EnumerateFiles(LogDirectory, "*.json", SearchOption.TopDirectoryOnly).ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static CleanupLogEntry? ReadEntry(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var log = JsonSerializer.Deserialize<CleanupRunLog>(stream, JsonOptions);
            if (log is null)
            {
                return null;
            }

            return new CleanupLogEntry(
                log.Mode,
                log.StartedAt,
                log.CompletedAt,
                log.DryRun,
                log.DeletedCount,
                log.DeletedBytes,
                log.DryRunCount,
                log.DryRunBytes,
                log.SkippedCount,
                log.FailedCount,
                path);
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset GetFileTimestamp(string path)
    {
        try
        {
            return new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero);
        }
        catch (IOException)
        {
            return DateTimeOffset.MinValue;
        }
        catch (UnauthorizedAccessException)
        {
            return DateTimeOffset.MinValue;
        }
    }
}
