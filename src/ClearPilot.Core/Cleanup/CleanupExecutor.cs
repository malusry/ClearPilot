using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Scanning;

namespace ClearPilot.Core.Cleanup;

public sealed class CleanupExecutor
{
    private readonly CleanupFileScanner fileScanner;
    private readonly CleanupLogStore logStore;

    public CleanupExecutor(CleanupFileScanner fileScanner, CleanupLogStore logStore)
    {
        this.fileScanner = fileScanner;
        this.logStore = logStore;
    }

    public CleanupRunResult Run(
        CleanupMode mode,
        IEnumerable<CleanupRule> rules,
        IReadOnlySet<RiskLevel> allowedRiskLevels,
        bool dryRun,
        DateTimeOffset now,
        string disallowedRiskMessage)
    {
        var startedAt = now;
        var items = new List<CleanupItemResult>();

        foreach (var rule in rules)
        {
            if (!allowedRiskLevels.Contains(rule.RiskLevel))
            {
                items.Add(new CleanupItemResult(
                    rule.RuleId,
                    string.Join(Path.PathSeparator, rule.RootPaths),
                    0,
                    CleanupItemAction.Skipped,
                    disallowedRiskMessage));
                continue;
            }

            foreach (var root in rule.RootPaths)
            {
                var files = fileScanner.ScanFiles(rule, root, now);
                foreach (var file in files)
                {
                    items.Add(dryRun
                        ? CreateDryRunResult(file)
                        : DeleteFile(file));
                }
            }
        }

        var completedAt = DateTimeOffset.UtcNow;
        var resultWithoutLogPath = new CleanupRunResult(
            mode,
            startedAt,
            completedAt,
            dryRun,
            items,
            LogPath: null);

        try
        {
            var logPath = logStore.Write(CleanupRunLog.FromResult(resultWithoutLogPath));
            return resultWithoutLogPath with { LogPath = logPath };
        }
        catch (IOException ex)
        {
            return resultWithoutLogPath with { LogError = ex.Message };
        }
        catch (UnauthorizedAccessException ex)
        {
            return resultWithoutLogPath with { LogError = ex.Message };
        }
    }

    private static CleanupItemResult CreateDryRunResult(CleanupFileCandidate file)
    {
        return new CleanupItemResult(
            file.Rule.RuleId,
            file.FilePath,
            file.SizeBytes,
            CleanupItemAction.DryRun,
            "Dry-run mode: file was not deleted.");
    }

    private static CleanupItemResult DeleteFile(CleanupFileCandidate file)
    {
        try
        {
            File.Delete(file.FilePath);
            return new CleanupItemResult(
                file.Rule.RuleId,
                file.FilePath,
                file.SizeBytes,
                CleanupItemAction.Deleted);
        }
        catch (DirectoryNotFoundException ex)
        {
            return CreateFailedResult(file, ex);
        }
        catch (FileNotFoundException ex)
        {
            return CreateFailedResult(file, ex);
        }
        catch (IOException ex)
        {
            return CreateFailedResult(file, ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateFailedResult(file, ex);
        }
    }

    private static CleanupItemResult CreateFailedResult(CleanupFileCandidate file, Exception exception)
    {
        return new CleanupItemResult(
            file.Rule.RuleId,
            file.FilePath,
            file.SizeBytes,
            CleanupItemAction.Failed,
            exception.Message);
    }
}
