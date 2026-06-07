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

        var commandOptions = ParseRecommendedJsonCommand(normalizedArgs);
        if (commandOptions.IsMatch)
        {
            try
            {
                var response = RunRecommendedCleanup(settings, commandOptions);
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

    private static NonInteractiveCleanupOptions ParseRecommendedJsonCommand(IReadOnlyList<string> args)
    {
        if (args.Count < 3
            || !string.Equals(args[0], "clean", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(args[1], "--recommended", StringComparison.OrdinalIgnoreCase)
            || !args.Any(arg => string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase)))
        {
            return NonInteractiveCleanupOptions.NoMatch;
        }

        var protectRunningAppCaches = false;
        var externalCaller = string.Empty;
        var dryRun = false;

        for (var index = 2; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, "--json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(arg, "--dry-run", StringComparison.OrdinalIgnoreCase))
            {
                dryRun = true;
                continue;
            }

            if (string.Equals(arg, "--protect-running-app-caches", StringComparison.OrdinalIgnoreCase))
            {
                protectRunningAppCaches = true;
                continue;
            }

            if (string.Equals(arg, "--external-caller", StringComparison.OrdinalIgnoreCase)
                && index + 1 < args.Count
                && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                externalCaller = args[++index];
                if (string.Equals(externalCaller, "bubblepet", StringComparison.OrdinalIgnoreCase))
                {
                    protectRunningAppCaches = true;
                }

                continue;
            }

            return NonInteractiveCleanupOptions.NoMatch;
        }

        return new NonInteractiveCleanupOptions(
            IsMatch: true,
            ProtectRunningAppCaches: protectRunningAppCaches,
            ExternalCaller: externalCaller,
            DryRun: dryRun);
    }

    private static CleanupCommandResponse RunRecommendedCleanup(AppSettings settings, NonInteractiveCleanupOptions commandOptions)
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
        var dryRun = settings.DryRun || commandOptions.DryRun;
        var quickSafeResult = quickSafeCleaner.Run(quickSafeRules, dryRun, now);

        var recommendedCandidates = recommendedService.Scan(rules, now).ToArray();
        var selectedRuleIds = SelectRecommendedRuleIds(recommendedCandidates, rules, processInspector);
        var selectedRules = rules
            .Where(rule => selectedRuleIds.Contains(rule.RuleId))
            .ToArray();
        var protectedSkippedItems = Array.Empty<CleanupItemResult>();
        if (commandOptions.ProtectRunningAppCaches)
        {
            var protectedFilterResult = ProtectRunningAppCacheRoots(selectedRules, now);
            selectedRules = protectedFilterResult.Rules.ToArray();
            protectedSkippedItems = protectedFilterResult.SkippedItems.ToArray();
        }

        var recommendedResult = selectedRules.Length == 0
            ? CreateEmptyRunResult(CleanupMode.RecommendedCleanup, dryRun, now)
            : recommendedService.Clean(selectedRules, confirmedByUser: true, dryRun: dryRun, now: now);
        if (protectedSkippedItems.Length > 0)
        {
            recommendedResult = AppendSkippedItemsAndWriteLog(logStore, recommendedResult, protectedSkippedItems);
        }

        return new CleanupCommandResponse(
            Success: true,
            Mode: "recommended",
            QuickSafe: CreateSummary(quickSafeResult),
            Recommended: CreateSummary(recommendedResult),
            TotalDeletedCount: quickSafeResult.DeletedCount + recommendedResult.DeletedCount,
            TotalDeletedBytes: quickSafeResult.DeletedBytes + recommendedResult.DeletedBytes,
            Message: "Cleanup completed.");
    }

    private static ProtectedRuleFilterResult ProtectRunningAppCacheRoots(
        IReadOnlyList<CleanupRule> selectedRules,
        DateTimeOffset now)
    {
        var filteredRules = new List<CleanupRule>();
        var skippedItems = new List<CleanupItemResult>();

        foreach (var rule in selectedRules)
        {
            var allowedRoots = new List<string>();
            foreach (var root in rule.RootPaths)
            {
                if (IsProtectedRunningAppCacheRoot(root))
                {
                    skippedItems.Add(CreateProtectedRunningAppCacheSkippedItem(rule, root, now));
                }
                else
                {
                    allowedRoots.Add(root);
                }
            }

            if (allowedRoots.Count > 0)
            {
                filteredRules.Add(rule with { RootPaths = allowedRoots });
            }
        }

        return new ProtectedRuleFilterResult(filteredRules.ToArray(), skippedItems.ToArray());
    }

    private static bool IsProtectedRunningAppCacheRoot(string root)
    {
        var normalized = NormalizePath(root);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return true;
        }

        var segments = normalized.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(IsProtectedRunningAppCacheSegment))
        {
            return true;
        }

        if (ContainsPathSegmentSequence(normalized, "com.bubblepet.translator"))
        {
            return true;
        }

        if (ContainsPathSegmentSequence(normalized, "Packages")
            && normalized.EndsWith($"{Path.DirectorySeparatorChar}LocalCache", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsProtectedRunningAppCacheSegment(string segment)
    {
        return string.Equals(segment, "GPUCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "GrShaderCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "ShaderCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "D3DSCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "DXCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "GLCache", StringComparison.OrdinalIgnoreCase)
            || string.Equals(segment, "ComputeCache", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsPathSegmentSequence(string normalizedPath, string segment)
    {
        return normalizedPath
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries)
            .Any(pathSegment => string.Equals(pathSegment, segment, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (ArgumentException)
        {
            return string.Empty;
        }
        catch (PathTooLongException)
        {
            return string.Empty;
        }
        catch (NotSupportedException)
        {
            return string.Empty;
        }
    }

    private static CleanupItemResult CreateProtectedRunningAppCacheSkippedItem(
        CleanupRule rule,
        string root,
        DateTimeOffset now)
    {
        const string reason = "protected-running-app-cache";
        var advice = RecommendationAdvisor.ForRule(rule);
        var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, sizeBytes: 0, processGuardResult: "ProtectedRunningAppCache");
        return new CleanupItemResult(
            rule.RuleId,
            rule.Category,
            rule.LauncherName,
            "ProtectedRunningAppCache",
            root,
            0,
            CleanupItemAction.Skipped,
            rule.RiskLevel,
            advice.Recommendation,
            decision.Decision,
            decision.DecisionReason,
            advice.AdviceKey,
            advice.PossibleImpact,
            advice.RecommendedAction,
            advice.SafetyNote,
            reason,
            new CleanupSafetyDecision(
                root,
                null,
                "NotEvaluated",
                "NotEvaluated",
                reason,
                "NotRun",
                $"Skipped at {now:O}: {reason}"));
    }

    private static CleanupRunResult AppendSkippedItemsAndWriteLog(
        CleanupLogStore logStore,
        CleanupRunResult result,
        IReadOnlyList<CleanupItemResult> skippedItems)
    {
        var mergedResult = new CleanupRunResult(
            result.Mode,
            result.StartedAt,
            DateTimeOffset.UtcNow,
            result.DryRun,
            result.Items.Concat(skippedItems).ToArray(),
            LogPath: null,
            result.LogError,
            string.IsNullOrWhiteSpace(result.RunId) ? Guid.NewGuid().ToString("N") : result.RunId);

        try
        {
            var logPath = logStore.Write(CleanupRunLog.FromResult(mergedResult));
            return mergedResult with { LogPath = logPath };
        }
        catch (IOException ex)
        {
            return mergedResult with { LogError = ex.Message };
        }
        catch (UnauthorizedAccessException ex)
        {
            return mergedResult with { LogError = ex.Message };
        }
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

    private sealed record NonInteractiveCleanupOptions(
        bool IsMatch,
        bool ProtectRunningAppCaches,
        string ExternalCaller,
        bool DryRun)
    {
        public static NonInteractiveCleanupOptions NoMatch { get; } = new(
            IsMatch: false,
            ProtectRunningAppCaches: false,
            ExternalCaller: string.Empty,
            DryRun: false);
    }

    private sealed record ProtectedRuleFilterResult(
        IReadOnlyList<CleanupRule> Rules,
        IReadOnlyList<CleanupItemResult> SkippedItems);
}
