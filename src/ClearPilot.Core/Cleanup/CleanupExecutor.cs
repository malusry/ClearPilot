using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Scanning;
using ClearPilot.Core.Safety;

namespace ClearPilot.Core.Cleanup;

public sealed class CleanupExecutor
{
    private readonly CleanupFileScanner fileScanner;
    private readonly CleanupLogStore logStore;
    private readonly PathSafetyEngine pathSafetyEngine;
    private readonly IProcessInspector processInspector;

    public CleanupExecutor(CleanupFileScanner fileScanner, CleanupLogStore logStore, PathSafetyEngine pathSafetyEngine)
        : this(fileScanner, logStore, pathSafetyEngine, new SystemProcessInspector())
    {
    }

    public CleanupExecutor(
        CleanupFileScanner fileScanner,
        CleanupLogStore logStore,
        PathSafetyEngine pathSafetyEngine,
        IProcessInspector processInspector)
    {
        this.fileScanner = fileScanner;
        this.logStore = logStore;
        this.pathSafetyEngine = pathSafetyEngine;
        this.processInspector = processInspector;
    }

    public CleanupRunResult Run(
        CleanupMode mode,
        IEnumerable<CleanupRule> rules,
        IReadOnlySet<RiskLevel> allowedRiskLevels,
        bool dryRun,
        DateTimeOffset now,
        string disallowedRiskMessage)
    {
        var ruleArray = rules.ToArray();
        var whitelist = KnownSafeCacheRootWhitelist.CreateFromRules(ruleArray);
        var startedAt = now;
        var items = new List<CleanupItemResult>();

        foreach (var rule in ruleArray)
        {
            var advice = RecommendationAdvisor.ForRule(rule);
            if (!allowedRiskLevels.Contains(rule.RiskLevel))
            {
                var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, sizeBytes: 0, processGuardResult: "NotRun");
                items.Add(new CleanupItemResult(
                    rule.RuleId,
                    rule.Category,
                    rule.LauncherName,
                    "NotRun",
                    string.Join(Path.PathSeparator, rule.RootPaths),
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
                    disallowedRiskMessage,
                    new CleanupSafetyDecision(
                        string.Join(Path.PathSeparator, rule.RootPaths),
                        null,
                        "NotEvaluated",
                        "NotEvaluated",
                        "SkippedByRiskGate",
                        "NotRun",
                        disallowedRiskMessage)));
                continue;
            }

            var processGuardResult = EvaluateProcessGuard(rule);
            if (processGuardResult.IsBlocked)
            {
                var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, sizeBytes: 0, processGuardResult: processGuardResult.ResultCode);
                items.Add(new CleanupItemResult(
                    rule.RuleId,
                    rule.Category,
                    rule.LauncherName,
                    processGuardResult.ResultCode,
                    string.Join(Path.PathSeparator, rule.RootPaths),
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
                    processGuardResult.Reason,
                    new CleanupSafetyDecision(
                        string.Join(Path.PathSeparator, rule.RootPaths),
                        null,
                        "NotEvaluated",
                        "NotEvaluated",
                        "SkippedByProcessGuard",
                        "NotRun",
                        processGuardResult.Reason)));
                continue;
            }

            foreach (var root in rule.RootPaths)
            {
                var rootDecision = pathSafetyEngine.ValidateRoot(root, whitelist);
                if (!rootDecision.IsSafe)
                {
                    items.Add(CreateSkippedForSafety(rule, root, 0, rootDecision, advice, "NotRun", null, processGuardResult.ResultCode));
                    continue;
                }

                var files = fileScanner.ScanFiles(rule, root, now);
                foreach (var file in files)
                {
                    items.Add(ProcessFile(rule, file, whitelist, dryRun, processGuardResult.ResultCode, advice));
                }
            }
        }

        var completedAt = DateTimeOffset.UtcNow;
        var runId = Guid.NewGuid().ToString("N");
        var resultWithoutLogPath = new CleanupRunResult(
            mode,
            startedAt,
            completedAt,
            dryRun,
            items,
            LogPath: null,
            RunId: runId);

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

    private CleanupItemResult ProcessFile(
        CleanupRule rule,
        CleanupFileCandidate file,
        KnownSafeCacheRootWhitelist whitelist,
        bool dryRun,
        string processGuardResult,
        TargetAdvice advice)
    {
        var initialSafety = pathSafetyEngine.ValidateCandidate(file.FilePath, file.RootPath, whitelist);
        if (!initialSafety.IsSafe)
        {
            return CreateSkippedForSafety(rule, file.FilePath, file.SizeBytes, initialSafety, advice, "NotRun", null, processGuardResult);
        }

        if (dryRun)
        {
            return CreateDryRunResult(rule, file, initialSafety, processGuardResult, advice);
        }

        var revalidation = pathSafetyEngine.RevalidateCandidate(
            file.FilePath,
            file.RootPath,
            whitelist,
            initialSafety.CanonicalPath!);

        if (!revalidation.IsSafe)
        {
            return CreateSkippedForSafety(rule, file.FilePath, file.SizeBytes, revalidation, advice, $"Blocked:{revalidation.ResultCode}", revalidation.Reason, processGuardResult);
        }

        return DeleteFile(rule, file, initialSafety, processGuardResult, advice);
    }

    private static CleanupItemResult CreateDryRunResult(CleanupRule rule, CleanupFileCandidate file, PathSafetyDecision safetyDecision, string processGuardResult, TargetAdvice advice)
    {
        var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, file.SizeBytes, processGuardResult);
        return new CleanupItemResult(
            file.Rule.RuleId,
            rule.Category,
            rule.LauncherName,
            processGuardResult,
            file.FilePath,
            file.SizeBytes,
            CleanupItemAction.DryRun,
            rule.RiskLevel,
            advice.Recommendation,
            decision.Decision,
            decision.DecisionReason,
            advice.AdviceKey,
            advice.PossibleImpact,
            advice.RecommendedAction,
            advice.SafetyNote,
            "Dry-run mode: file was not deleted.",
            CreateSafetyDecision(safetyDecision, "DryRunNotExecuted", null));
    }

    private static CleanupItemResult DeleteFile(CleanupRule rule, CleanupFileCandidate file, PathSafetyDecision safetyDecision, string processGuardResult, TargetAdvice advice)
    {
        try
        {
            var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, file.SizeBytes, processGuardResult);
            File.Delete(file.FilePath);
            return new CleanupItemResult(
                file.Rule.RuleId,
                rule.Category,
                rule.LauncherName,
                processGuardResult,
                file.FilePath,
                file.SizeBytes,
                CleanupItemAction.Deleted,
                rule.RiskLevel,
                advice.Recommendation,
                decision.Decision,
                decision.DecisionReason,
                advice.AdviceKey,
                advice.PossibleImpact,
                advice.RecommendedAction,
                advice.SafetyNote,
                null,
                CreateSafetyDecision(safetyDecision, "Passed", null));
        }
        catch (DirectoryNotFoundException ex)
        {
            return CreateSkippedResult(rule, file, ex.Message, safetyDecision, "Skipped:MissingPath", processGuardResult, advice);
        }
        catch (FileNotFoundException ex)
        {
            return CreateSkippedResult(rule, file, ex.Message, safetyDecision, "Skipped:MissingPath", processGuardResult, advice);
        }
        catch (IOException ex)
        {
            if (IsLockedFileException(ex))
            {
                return CreateSkippedResult(rule, file, ex.Message, safetyDecision, "Skipped:LockedFile", processGuardResult, advice);
            }

            return CreateFailedResult(rule, file, ex, safetyDecision, processGuardResult, advice);
        }
        catch (UnauthorizedAccessException ex)
        {
            return CreateSkippedResult(rule, file, ex.Message, safetyDecision, "Skipped:AccessDenied", processGuardResult, advice);
        }
    }

    private static CleanupItemResult CreateFailedResult(
        CleanupRule rule,
        CleanupFileCandidate file,
        Exception exception,
        PathSafetyDecision safetyDecision,
        string processGuardResult,
        TargetAdvice advice)
    {
        var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, file.SizeBytes, processGuardResult);
        return new CleanupItemResult(
            file.Rule.RuleId,
            rule.Category,
            rule.LauncherName,
            processGuardResult,
            file.FilePath,
            file.SizeBytes,
            CleanupItemAction.Failed,
            rule.RiskLevel,
            advice.Recommendation,
            decision.Decision,
            decision.DecisionReason,
            advice.AdviceKey,
            advice.PossibleImpact,
            advice.RecommendedAction,
            advice.SafetyNote,
            exception.Message,
            CreateSafetyDecision(safetyDecision, "Passed", exception.Message));
    }

    private static CleanupItemResult CreateSkippedResult(
        CleanupRule rule,
        CleanupFileCandidate file,
        string reason,
        PathSafetyDecision safetyDecision,
        string revalidationResult,
        string processGuardResult,
        TargetAdvice advice)
    {
        var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, file.SizeBytes, processGuardResult);
        return new CleanupItemResult(
            file.Rule.RuleId,
            rule.Category,
            rule.LauncherName,
            processGuardResult,
            file.FilePath,
            file.SizeBytes,
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
            CreateSafetyDecision(safetyDecision, revalidationResult, reason));
    }

    private static CleanupItemResult CreateSkippedForSafety(
        CleanupRule rule,
        string path,
        long sizeBytes,
        PathSafetyDecision safetyDecision,
        TargetAdvice advice,
        string revalidationResult,
        string? skippedReasonOverride = null,
        string processGuardResult = "NotRun")
    {
        var skippedReason = skippedReasonOverride ?? safetyDecision.Reason ?? safetyDecision.ResultCode;
        var decision = CleanupDecisionAdvisor.ForExecutionResult(rule, advice, sizeBytes, processGuardResult);

        return new CleanupItemResult(
            rule.RuleId,
            rule.Category,
            rule.LauncherName,
            processGuardResult,
            path,
            sizeBytes,
            CleanupItemAction.Skipped,
            rule.RiskLevel,
            advice.Recommendation,
            decision.Decision,
            decision.DecisionReason,
            advice.AdviceKey,
            advice.PossibleImpact,
            advice.RecommendedAction,
            advice.SafetyNote,
            skippedReason,
            CreateSafetyDecision(safetyDecision, revalidationResult, skippedReason));
    }

    private static CleanupSafetyDecision CreateSafetyDecision(
        PathSafetyDecision safetyDecision,
        string revalidationResult,
        string? skippedReason)
    {
        return new CleanupSafetyDecision(
            safetyDecision.OriginalPath,
            safetyDecision.CanonicalPath,
            safetyDecision.AllowlistAllowed ? "Allowed" : "Blocked",
            safetyDecision.DenylistAllowed ? "Allowed" : "Blocked",
            safetyDecision.IsSafe ? "Allowed" : $"Blocked:{safetyDecision.ResultCode}",
            revalidationResult,
            skippedReason);
    }

    private static bool IsLockedFileException(IOException exception)
    {
        var win32Code = exception.HResult & 0xFFFF;
        return win32Code is 32 or 33;
    }

    private ProcessGuardEvaluation EvaluateProcessGuard(CleanupRule rule)
    {
        if (rule.EffectiveProcessGuardNames.Count == 0)
        {
            return new ProcessGuardEvaluation(false, "NotApplicable", null);
        }

        if (processInspector.IsAnyRunning(rule.EffectiveProcessGuardNames))
        {
            var names = string.Join(", ", rule.EffectiveProcessGuardNames);
            return new ProcessGuardEvaluation(
                true,
                "Blocked:LauncherRunning",
                $"Skipped because launcher process is running: {names}");
        }

        return new ProcessGuardEvaluation(false, "Passed", null);
    }

    private sealed record ProcessGuardEvaluation(
        bool IsBlocked,
        string ResultCode,
        string? Reason);
}
