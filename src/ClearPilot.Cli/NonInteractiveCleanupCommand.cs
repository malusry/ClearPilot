using System.Text.Json;
using System.Text.Json.Serialization;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using ClearPilot.Core.Settings;

namespace ClearPilot.Cli;

internal static class NonInteractiveCleanupCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool TryRun(string[] args, AppSettings settings, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0)
        {
            return false;
        }

        var normalizedArgs = args
            .Where(arg => !string.IsNullOrWhiteSpace(arg))
            .Select(arg => arg.Trim())
            .ToArray();
        var hasJson = normalizedArgs.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase));

        if (IsRecommendedJsonCommand(normalizedArgs))
        {
            try
            {
                var response = RunRecommendedCleanup(settings);
                WriteJson(response);
                exitCode = 0;
                return true;
            }
            catch (Exception ex)
            {
                WriteJson(new CleanupCommandErrorResponse(
                    Success: false,
                    Mode: "recommended",
                    Message: "ClearPilot cleanup failed.",
                    Error: ex.Message));
                exitCode = 1;
                return true;
            }
        }

        if (hasJson)
        {
            WriteJson(new CleanupCommandErrorResponse(
                Success: false,
                Mode: "recommended",
                Message: "Invalid ClearPilot command.",
                Error: "Expected: clean --recommended --json"));
        }
        else
        {
            Console.Error.Write("Invalid ClearPilot command. Expected: clean --recommended --json");
        }

        exitCode = 2;
        return true;
    }

    private static bool IsRecommendedJsonCommand(IReadOnlyList<string> args)
    {
        return args.Count == 3
            && string.Equals(args[0], "clean", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[1], "--recommended", StringComparison.OrdinalIgnoreCase)
            && string.Equals(args[2], "--json", StringComparison.OrdinalIgnoreCase);
    }

    private static CleanupCommandResponse RunRecommendedCleanup(AppSettings settings)
    {
        var protectedPathPolicy = ProtectedPathPolicy.CreateDefault();
        var fileScanner = new CleanupFileScanner(protectedPathPolicy);
        var logStore = CleanupLogStore.CreateDefault();
        var pathSafetyEngine = new PathSafetyEngine(protectedPathPolicy);
        var cleanupScanner = new CleanupScanner(protectedPathPolicy);
        var processInspector = new SystemProcessInspector();
        var executor = new CleanupExecutor(fileScanner, logStore, pathSafetyEngine, processInspector);
        var quickSafeCleaner = new QuickSafeCleaner(fileScanner, logStore, pathSafetyEngine);
        var recommendedService = new RecommendedCleanupService(cleanupScanner, executor);
        var rules = RuleCatalog.CreateDefault();
        var now = DateTimeOffset.UtcNow;

        var quickSafeRules = rules
            .Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk)
            .ToArray();
        var quickSafeResult = quickSafeCleaner.Run(quickSafeRules, settings.DryRun, now);

        var recommendedCandidates = recommendedService.Scan(rules, now).ToArray();
        var selectedRuleIds = SelectRecommendedRuleIds(recommendedCandidates, rules, processInspector);
        var selectedRules = rules
            .Where(rule => selectedRuleIds.Contains(rule.RuleId))
            .ToArray();
        var recommendedResult = selectedRules.Length == 0
            ? CreateEmptyRunResult(CleanupMode.RecommendedCleanup, settings.DryRun, now)
            : recommendedService.Clean(selectedRules, confirmedByUser: true, dryRun: settings.DryRun, now: now);

        return new CleanupCommandResponse(
            Success: true,
            Mode: "recommended",
            QuickSafe: CreateSummary(quickSafeResult),
            Recommended: CreateSummary(recommendedResult),
            TotalDeletedCount: quickSafeResult.DeletedCount + recommendedResult.DeletedCount,
            TotalDeletedBytes: quickSafeResult.DeletedBytes + recommendedResult.DeletedBytes,
            Message: "Cleanup completed.");
    }

    private static HashSet<string> SelectRecommendedRuleIds(
        IReadOnlyList<CleanupCandidate> candidates,
        IReadOnlyList<CleanupRule> rules,
        IProcessInspector processInspector)
    {
        var ruleMap = rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
        var selectedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in candidates)
        {
            if (!ruleMap.TryGetValue(candidate.RuleId, out var rule))
            {
                continue;
            }

            var processGuardBlocked = rule.EffectiveProcessGuardNames.Count > 0
                && processInspector.IsAnyRunning(rule.EffectiveProcessGuardNames);
            if (!ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
                    candidate.RiskLevel,
                    candidate.CleanupDecision,
                    processGuardBlocked))
            {
                continue;
            }

            selectedRuleIds.Add(candidate.RuleId);
        }

        return selectedRuleIds;
    }

    private static CleanupRunResult CreateEmptyRunResult(CleanupMode mode, bool dryRun, DateTimeOffset now)
    {
        return new CleanupRunResult(
            mode,
            now,
            DateTimeOffset.UtcNow,
            dryRun,
            [],
            LogPath: null,
            RunId: Guid.NewGuid().ToString("N"));
    }

    private static CleanupCommandSectionSummary CreateSummary(CleanupRunResult result)
    {
        return new CleanupCommandSectionSummary(
            DeletedCount: result.DeletedCount,
            DeletedBytes: result.DeletedBytes,
            SkippedCount: result.SkippedCount,
            FailedCount: result.FailedCount,
            LogPath: result.LogPath ?? string.Empty);
    }

    private static void WriteJson<T>(T value)
    {
        Console.Out.Write(JsonSerializer.Serialize(value, JsonOptions));
    }

    internal sealed record CleanupCommandResponse(
        bool Success,
        string Mode,
        CleanupCommandSectionSummary QuickSafe,
        CleanupCommandSectionSummary Recommended,
        int TotalDeletedCount,
        long TotalDeletedBytes,
        string Message);

    internal sealed record CleanupCommandSectionSummary(
        int DeletedCount,
        long DeletedBytes,
        int SkippedCount,
        int FailedCount,
        string LogPath);

    internal sealed record CleanupCommandErrorResponse(
        bool Success,
        string Mode,
        string Message,
        string Error);
}
