using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClearPilot.Cli;
using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using ClearPilot.Core.Settings;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;
ConfigureConsole();

var settingsStore = SettingsStore.CreateDefault();
var settings = settingsStore.Load();

if (NonInteractiveCleanupCommand.TryRun(args, settings, out var exitCode))
{
    Environment.ExitCode = exitCode;
    return;
}

RunMainMenu(settings, settingsStore);

static void RunMainMenu(AppSettings settings, SettingsStore settingsStore)
{
    while (true)
    {
        var text = MessageCatalog.For(settings.Language);

        StartMainMenuPage();
        Console.WriteLine();
        WriteCommandOption("1", text.Get(StringKey.MainMenuQuickSafeClean), text.Get(StringKey.MainMenuQuickSafeCleanDescription), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.QuickSafeClean));
        WriteCommandOption("2", text.Get(StringKey.MainMenuScanRecommendedItems), text.Get(StringKey.MainMenuScanRecommendedItemsDescription), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.RecommendedCleanup));
        WriteCommandOption("3", text.Get(StringKey.MainMenuDeepSpaceAnalysis), text.Get(StringKey.MainMenuDeepSpaceAnalysisDescription), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.DeepSpaceAnalysis));
        WriteCommandOption("4", text.Get(StringKey.MainMenuCleanupHistory), text.Get(StringKey.MainMenuCleanupHistoryDescription), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.ReportsHistory));
        WriteCommandOption("5", text.Get(StringKey.MainMenuSettings), text.Get(StringKey.MainMenuSettingsDescription), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.Settings));
        WriteCommandOption("0", text.Get(StringKey.MenuExit), text.Get(StringKey.MainMenuExitDescription), Theme.Subtle);
        Console.WriteLine();
        WritePrompt(text.Get(StringKey.PromptChooseOption));

        var choice = Console.ReadLine();
        Console.WriteLine();

        switch (choice)
        {
            case "1":
                RunQuickSafeClean(settings);
                break;
            case "2":
                RunRecommendedCleanup(settings);
                break;
            case "3":
                RunDeepSpaceAnalysis(settings);
                break;
            case "4":
                RunCleanupHistory(settings);
                break;
            case "5":
                RunSettingsMenu(settings, settingsStore);
                break;
            case "0":
            case null:
                return;
            default:
                ShowPlaceholder(text, StringKey.MessageInvalidOption);
                break;
        }
    }
}

static void RunCleanupHistory(AppSettings settings)
{
    var text = MessageCatalog.For(settings.Language);
    var logStore = CleanupLogStore.CreateDefault();
    var cutoff = DateTimeOffset.UtcNow.AddDays(-settings.LogRetentionDays);
    var removedCount = logStore.DeleteLogsOlderThan(cutoff);
    var entries = logStore.ReadRecent(maxCount: 20);

    StartPage(text.Get(StringKey.HistoryTitle));
    Console.WriteLine();
    WriteLabelValue(text.Get(StringKey.HistoryRetentionDeleted), removedCount.ToString(), Theme.Subtle);
    Console.WriteLine();

    if (entries.Count == 0)
    {
        WriteLineColor(text.Get(StringKey.HistoryNoLogs), Theme.Subtle);
        Pause(text);
        return;
    }

    for (var index = 0; index < entries.Count; index++)
    {
        var entry = entries[index];
        WriteResultCard(
            index + 1,
            entry.Mode.ToString(),
            $"{FormatBytes(entry.DeletedBytes)} reclaimed",
            [
                $"{text.Get(StringKey.HistoryStartedAt)}: {FormatDate(entry.StartedAt)}",
                $"{text.Get(StringKey.HistoryDeletedFiles)}: {entry.DeletedCount}   {text.Get(StringKey.HistorySkippedItems)}: {entry.SkippedCount}   {text.Get(StringKey.HistoryFailedItems)}: {entry.FailedCount}",
                $"{text.Get(StringKey.HistoryLogPath)}: {entry.Path}"
            ],
            entry.FailedCount == 0 ? Theme.Accent : Theme.Danger);
        Console.WriteLine();
    }

    Pause(text);
}

static void RunDeepSpaceAnalysis(AppSettings settings)
{
    var text = MessageCatalog.For(settings.Language);
    StartPage(text.Get(StringKey.DeepAnalysisStarted));
    WriteLineColor(GetDeepSpaceNoDeleteNoticeV45(text), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.SafetyBoundary));
    Console.WriteLine();

    var analyzer = new DeepSpaceAnalyzer(ProtectedPathPolicy.CreateDefault());
    var options = CreateDeepSpaceOptions();
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisScanScope)}:", Theme.Muted);
    foreach (var root in options.RootPaths)
    {
        WriteLineColor($"- {root}", Theme.Subtle);
    }
    Console.WriteLine();

    var result = analyzer.AnalyzeWithSummary(options, DateTimeOffset.UtcNow);
    var allItems = result.Items.ToArray();

    if (allItems.Length == 0)
    {
        WriteDeepAnalysisSummary(text, result.Summary, allItems);
        Console.WriteLine();
        WriteLineColor(text.Get(StringKey.DeepAnalysisNoItems), Theme.Subtle);
        Pause(text);
        return;
    }

    DeepSpaceItemType? currentFilter = null;
    var currentSort = DeepSpaceSortMode.SizeDescending;

    while (true)
    {
        var items = ApplyDeepSpaceView(allItems, currentFilter, currentSort).ToArray();
        RenderDeepAnalysisView(text, result.Summary, allItems, items, currentFilter, currentSort);

        WriteMenuOption("F", text.Get(StringKey.DeepAnalysisFilterCommand), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.DeepSpaceAnalysis));
        WriteMenuOption("S", text.Get(StringKey.DeepAnalysisSortCommand), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.DeepSpaceAnalysis));
        WriteMenuOption("R", text.Get(StringKey.DeepAnalysisReportCommand), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.ReportsHistory));
        WriteMenuOption("0", text.Get(StringKey.DeepAnalysisReturn), Theme.Subtle);
        WritePrompt(text.Get(StringKey.DeepAnalysisOpenPrompt));
        var selection = Console.ReadLine();
        Console.WriteLine();

        if (string.Equals(selection?.Trim(), "F", StringComparison.OrdinalIgnoreCase))
        {
            currentFilter = ChooseDeepSpaceFilter(text, currentFilter);
            continue;
        }

        if (string.Equals(selection?.Trim(), "S", StringComparison.OrdinalIgnoreCase))
        {
            currentSort = ChooseDeepSpaceSort(text, currentSort);
            continue;
        }

        if (string.Equals(selection?.Trim(), "R", StringComparison.OrdinalIgnoreCase))
        {
            ExportDeepSpaceReport(text, result, options.RootPaths);
            Pause(text);
            continue;
        }

        if (!int.TryParse(selection, out var itemNumber))
        {
            WriteLineColor(text.Get(StringKey.MessageInvalidOption), Theme.Danger);
            Pause(text);
            continue;
        }

        if (itemNumber == 0)
        {
            return;
        }

        var selectedIndex = itemNumber - 1;
        if (selectedIndex < 0 || selectedIndex >= items.Length)
        {
            WriteLineColor(text.Get(StringKey.MessageInvalidOption), Theme.Danger);
            Pause(text);
            continue;
        }

        try
        {
            OpenInExplorer(items[selectedIndex].Path);
            WriteLineColor(text.Get(StringKey.DeepAnalysisOpenSuccess), Theme.Success);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            WriteLineColor($"{text.Get(StringKey.DeepAnalysisOpenFailed)}: {ex.Message}", Theme.Danger);
        }

        Pause(text);
    }
}

static void RunRecommendedCleanup(AppSettings settings)
{
    var text = MessageCatalog.For(settings.Language);
    StartPage(text.Get(StringKey.RecommendedScanStarted));
    Console.WriteLine();

    var protectedPathPolicy = ProtectedPathPolicy.CreateDefault();
    var scanner = new CleanupScanner(protectedPathPolicy);
    var fileScanner = new CleanupFileScanner(protectedPathPolicy);
    var logStore = CleanupLogStore.CreateDefault();
    var executor = new CleanupExecutor(fileScanner, logStore, new PathSafetyEngine(protectedPathPolicy));
    var service = new RecommendedCleanupService(scanner, executor);
    var rules = RuleCatalog.CreateDefault();
    var ruleMap = rules.ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
    var processInspector = new SystemProcessInspector();
    var candidates = service.Scan(rules, DateTimeOffset.UtcNow).ToArray();

    if (candidates.Length == 0)
    {
        WriteLineColor(text.Get(StringKey.RecommendedScanNoItems), Theme.Subtle);
        Pause(text);
        return;
    }

    WriteCleanupPreviewSummary(text, candidates, text.Get(StringKey.RecommendedPreviewSafety));
    WriteLineColor(text.Get(StringKey.RecommendedScanFound), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.RecommendedCleanup));
    WriteBadgeNotice(FormatRiskBadge(RiskLevel.S1LowRisk), GetRiskColor(RiskLevel.S1LowRisk), text.Get(StringKey.RecommendedActionHint));
    Console.WriteLine();

    for (var index = 0; index < candidates.Length; index++)
    {
        var candidate = candidates[index];
        var launcherRunning = IsProcessGuardBlocked(candidate, ruleMap, processInspector);
        var displayDecision = launcherRunning ? CleanupDecision.NotRecommendedToClean : candidate.CleanupDecision;
        var displayDecisionReason = launcherRunning
            ? GetAppRunningSkipReason(text)
            : GetDecisionReasonForDisplayV46(text, candidate, displayDecision);
        WriteResultCard(
            index + 1,
            FormatCleanupCandidateCategory(text, candidate),
            $"{text.Get(StringKey.RecommendedScanFiles)}: {candidate.FileCount}",
            [
                CardDetailLine.WithHighlight(
                    $"{GetDecisionLabelV46(text)}: ",
                    FormatCleanupDecisionBadgeV46(text, displayDecision),
                    GetDecisionColorV46(displayDecision),
                    prefixColor: ConsolePresentationStyle.GetRecommendedDecisionLabelColor()),
                CardDetailLine.WithHighlight(
                    $"{GetReasonLabelV46(text)}: ",
                    displayDecisionReason,
                    ConsolePresentationStyle.GetRecommendedReasonColor(),
                    prefixColor: ConsolePresentationStyle.GetRecommendedReasonLabelColor()),
                CardDetailLine.WithHighlight(
                    $"{GetImpactLabelV46(text)}: ",
                    GetPossibleImpactForDisplayV46(text, candidate, displayDecision),
                    ConsolePresentationStyle.GetRecommendedImpactColor(),
                    prefixColor: ConsolePresentationStyle.GetRecommendedImpactLabelColor()),
                CardDetailLine.WithHighlight(
                    $"{GetExpectedReclaimLabelV46(text)}: ",
                    FormatBytes(candidate.EstimatedBytes),
                    ConsolePresentationStyle.GetRecommendedExpectedReclaimColor(),
                    prefixColor: ConsolePresentationStyle.GetRecommendedExpectedReclaimLabelColor()),
                CardDetailLine.WithHighlight(
                    $"{GetRiskLabelV46(text)}: ",
                    FormatRiskBadge(candidate.RiskLevel),
                    ConsolePresentationStyle.GetRecommendedRiskColor(candidate.RiskLevel),
                    prefixColor: ConsolePresentationStyle.GetRecommendedRiskLabelColor())
            ],
            GetRiskColor(candidate.RiskLevel));
        Console.WriteLine();
    }

    WriteMenuOption("A", text.Get(StringKey.RecommendedSelectionAll), ConsolePresentationStyle.GetModeColor(ConsolePresentationStyle.ModeColorRole.RecommendedCleanup));
    WriteMenuOption("0", text.Get(StringKey.MenuCancel), Theme.Subtle);
    Console.WriteLine();
    WriteLineColor(GetRecommendedSelectionExecutionLineV47(text), ConsolePresentationStyle.GetRecommendedPrimarySafetyColor());
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.RecommendedSelectionPrompt));
    var selection = Console.ReadLine();
    Console.WriteLine();

    var selectedRuleIds = ParseRecommendedSelection(selection, candidates, ruleMap, processInspector);
    if (selectedRuleIds.Count == 0)
    {
        WriteLineColor(text.Get(StringKey.RecommendedSelectionCancelled), Theme.Subtle);
        Pause(text);
        return;
    }

    var selectedRules = rules
        .Where(rule => selectedRuleIds.Contains(rule.RuleId, StringComparer.OrdinalIgnoreCase))
        .ToArray();

    StartPage(text.Get(StringKey.RecommendedCleanStarted));
    Console.WriteLine();

    var result = service.Clean(
        selectedRules,
        confirmedByUser: true,
        dryRun: settings.DryRun,
        now: DateTimeOffset.UtcNow);
    StartPage(text.Get(StringKey.RecommendedCleanCompleted));
    Console.WriteLine();
    WriteLabelValue(text.Get(StringKey.RecommendedCleanedFiles), result.DeletedCount.ToString(), Theme.Success);
    WriteLabelValue(text.Get(StringKey.RecommendedReclaimedSpace), FormatBytes(result.DeletedBytes), Theme.Success);

    if (result.DryRun)
    {
        WriteLabelValue(text.Get(StringKey.QuickSafeCleanDryRunFiles), result.DryRunCount.ToString(), Theme.Warning);
        WriteLabelValue(text.Get(StringKey.QuickSafeCleanDryRunSpace), FormatBytes(result.DryRunBytes), Theme.Warning);
    }

    WriteLabelValue(text.Get(StringKey.QuickSafeCleanSkippedItems), result.SkippedCount.ToString(), Theme.Note);
    WriteLabelValue(text.Get(StringKey.QuickSafeCleanFailedItems), result.FailedCount.ToString(), result.FailedCount == 0 ? Theme.Muted : Theme.Danger);

    if (!string.IsNullOrWhiteSpace(result.LogPath))
    {
        WriteCleanupLogLocation(text, result.LogPath);
    }
    else if (!string.IsNullOrWhiteSpace(result.LogError))
    {
        WriteLineColor($"{text.Get(StringKey.QuickSafeCleanLogError)}: {result.LogError}", Theme.Danger);
    }

    Pause(text);
}

static void RunQuickSafeClean(AppSettings settings)
{
    var text = MessageCatalog.For(settings.Language);
    StartPage(text.Get(StringKey.QuickSafeCleanStarted));
    Console.WriteLine();

    var protectedPathPolicy = ProtectedPathPolicy.CreateDefault();
    var fileScanner = new CleanupFileScanner(protectedPathPolicy);
    var logStore = CleanupLogStore.CreateDefault();
    var cleaner = new QuickSafeCleaner(fileScanner, logStore);
    var s0Rules = RuleCatalog.CreateDefault()
        .Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk)
        .ToArray();
    var scanner = new CleanupScanner(protectedPathPolicy);
    var now = DateTimeOffset.UtcNow;
    var previewCandidates = scanner.Scan(s0Rules, now).ToArray();

    WriteCleanupPreviewSummary(text, previewCandidates, text.Get(StringKey.QuickSafeCleanPreviewSafety));
    WriteLineColor(text.Get(StringKey.QuickSafeCleanBoundaryS0Only), Theme.Warning);
    WriteLineColor(text.Get(StringKey.QuickSafeCleanBoundaryKnownTargetsOnly), Theme.Warning);
    WriteLineColor(text.Get(StringKey.QuickSafeCleanBoundaryBroaderElsewhere), Theme.Warning);
    Console.WriteLine();

    var result = cleaner.Run(s0Rules, settings.DryRun, now);

    StartPage(text.Get(StringKey.QuickSafeCleanCompleted));
    Console.WriteLine();
    if (result.Items.Count == 0)
    {
        WriteLineColor(text.Get(StringKey.QuickSafeCleanNoItems), Theme.Subtle);
    }

    WriteLabelValue(text.Get(StringKey.QuickSafeCleanDeletedFiles), result.DeletedCount.ToString(), Theme.Success);
    WriteLabelValue(text.Get(StringKey.QuickSafeCleanReclaimedSpace), FormatBytes(result.DeletedBytes), Theme.Success);

    if (result.DryRun)
    {
        WriteLabelValue(text.Get(StringKey.QuickSafeCleanDryRunFiles), result.DryRunCount.ToString(), Theme.Warning);
        WriteLabelValue(text.Get(StringKey.QuickSafeCleanDryRunSpace), FormatBytes(result.DryRunBytes), Theme.Warning);
    }

    WriteLabelValue(text.Get(StringKey.QuickSafeCleanSkippedItems), result.SkippedCount.ToString(), Theme.Note);
    WriteLabelValue(text.Get(StringKey.QuickSafeCleanFailedItems), result.FailedCount.ToString(), result.FailedCount == 0 ? Theme.Muted : Theme.Danger);

    if (result.SkippedCount > 0)
    {
        WriteLineColor(text.Get(StringKey.QuickSafeCleanSkippedUnchangedNote), Theme.Note);
        WriteQuickSafeSkippedReasonSummary(text, result);
    }

    if (!string.IsNullOrWhiteSpace(result.LogPath))
    {
        WriteCleanupLogLocation(text, result.LogPath);
    }
    else if (!string.IsNullOrWhiteSpace(result.LogError))
    {
        WriteLineColor($"{text.Get(StringKey.QuickSafeCleanLogError)}: {result.LogError}", Theme.Danger);
    }

    Console.WriteLine();
    WriteLineColor(text.Get(StringKey.QuickSafeCleanFailureNoElevation), Theme.Warning);

    Pause(text);
}

static void RunSettingsMenu(AppSettings settings, SettingsStore settingsStore)
{
    while (true)
    {
        var text = MessageCatalog.For(settings.Language);

        StartPage(text.Get(StringKey.SettingsTitle));
        Console.WriteLine();
        WriteMenuOption("1", $"{text.Get(StringKey.SettingsLanguage)} ({text.Get(StringKey.SettingsCurrentValue)}: {FormatLanguage(settings.Language, text)})", Theme.Accent);
        WriteMenuOption("2", $"{text.Get(StringKey.SettingsLogRetentionDays)} ({text.Get(StringKey.SettingsCurrentValue)}: {settings.LogRetentionDays})", Theme.Accent);
        WriteMenuOption("3", $"{text.Get(StringKey.SettingsAutoEmptyRecycleBin)} ({text.Get(StringKey.SettingsCurrentValue)}: {FormatEnabled(settings.AutoEmptyRecycleBin, text)})", Theme.Warning);
        WriteMenuOption("0", text.Get(StringKey.SettingsBack), Theme.Subtle);
        Console.WriteLine();
        WritePrompt(text.Get(StringKey.PromptChooseOption));

        var choice = Console.ReadLine();
        Console.WriteLine();

        switch (choice)
        {
            case "1":
                ChangeLanguage(settings, settingsStore);
                break;
            case "2":
                ChangeLogRetention(settings, settingsStore);
                break;
            case "3":
                ChangeRecycleBinSetting(settings, settingsStore);
                break;
            case "0":
            case null:
                return;
            default:
                ShowPlaceholder(text, StringKey.MessageInvalidOption);
                break;
        }
    }
}

static void ChangeLanguage(AppSettings settings, SettingsStore settingsStore)
{
    var text = MessageCatalog.For(settings.Language);

    StartPage(text.Get(StringKey.SettingsLanguageTitle));
    Console.WriteLine();
    WriteMenuOption("1", text.Get(StringKey.SettingsLanguageEnglish), Theme.Accent);
    WriteMenuOption("2", text.Get(StringKey.SettingsLanguageSimplifiedChinese), Theme.Accent);
    WriteMenuOption("0", text.Get(StringKey.SettingsBack), Theme.Subtle);
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.PromptChooseOption));

    var choice = Console.ReadLine();
    Console.WriteLine();

    settings.Language = choice switch
    {
        "1" => Language.English,
        "2" => Language.SimplifiedChinese,
        _ => settings.Language
    };

    if (choice is "1" or "2")
    {
        settingsStore.Save(settings);
        WriteLineColor(MessageCatalog.For(settings.Language).Get(StringKey.SettingsLanguageUpdated), Theme.Success);
        Pause(MessageCatalog.For(settings.Language));
    }
}

static void ChangeLogRetention(AppSettings settings, SettingsStore settingsStore)
{
    var text = MessageCatalog.For(settings.Language);

    StartPage(text.Get(StringKey.SettingsLogRetentionDays));
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.SettingsRetentionPrompt));
    var input = Console.ReadLine();
    Console.WriteLine();

    if (int.TryParse(input, out var days))
    {
        settings.LogRetentionDays = days;
        settings.Normalize();
        settingsStore.Save(settings);
        WriteLineColor(text.Get(StringKey.SettingsRetentionUpdated), Theme.Success);
    }
    else
    {
        WriteLineColor(text.Get(StringKey.MessageInvalidOption), Theme.Danger);
    }

    Pause(text);
}

static void ChangeRecycleBinSetting(AppSettings settings, SettingsStore settingsStore)
{
    var text = MessageCatalog.For(settings.Language);

    StartPage(text.Get(StringKey.SettingsAutoEmptyRecycleBin));
    Console.WriteLine();
    WriteLineColor(text.Get(StringKey.SettingsRecycleBinWarning), Theme.Warning);
    Console.WriteLine();
    WriteMenuOption("1", text.Get(StringKey.SettingsRecycleBinEnable), Theme.Warning);
    WriteMenuOption("2", text.Get(StringKey.SettingsRecycleBinDisable), Theme.Accent);
    WriteMenuOption("0", text.Get(StringKey.SettingsBack), Theme.Subtle);
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.PromptChooseOption));

    var choice = Console.ReadLine();
    Console.WriteLine();

    if (choice is "1" or "2")
    {
        settings.AutoEmptyRecycleBin = choice == "1";
        settingsStore.Save(settings);
        WriteLineColor(text.Get(StringKey.SettingsRecycleBinUpdated), Theme.Success);
        Pause(text);
    }
}

static void ShowPlaceholder(MessageCatalog text, StringKey key)
{
    WriteLineColor(text.Get(key), Theme.Subtle);
    Pause(text);
}

static string FormatCleanupCandidateCategory(MessageCatalog text, CleanupCandidate candidate)
{
    if (text.Language != Language.SimplifiedChinese)
    {
        return candidate.Category;
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-logs" or
        "cp.s1.vscode-logs" or
        "cp.s1.jetbrains-logs")
    {
        return "\u5E94\u7528\u65E5\u5FD7";
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-crash-reports" or
        "cp.s1.electron-app-crash-completed" or
        "cp.s1.vscode-crash-reports" or
        "cp.s1.vscode-crash-completed")
    {
        return "\u5E94\u7528\u5D29\u6E83\u8BCA\u65AD";
    }

    return candidate.RuleId switch
    {
        "cp.s0.user-temp" => "当前用户临时文件",
        "cp.s1.user-crash-dumps" => "当前用户崩溃转储",
        "cp.s1.windows-error-reports" => "Windows 错误报告文件",
        "cp.s1.directx-shader-cache" => "DirectX 和显卡着色器缓存",
        "cp.s1.nuget-http-cache" => "NuGet HTTP 缓存",
        "cp.s1.npm-cache" => "npm 缓存",
        "cp.s1.yarn-cache" => "Yarn 缓存",
        "cp.s1.pnpm-store" => "pnpm store 缓存",
        "cp.s1.pip-cache" => "pip 缓存",
        "cp.s1.composer-cache" => "Composer 缓存",
        "cp.s1.go-cache" => "Go 构建和模块缓存",
        "cp.s1.cargo-registry-cache" => "Cargo registry 缓存",
        "cp.s1.cargo-git-cache" => "Cargo git 缓存",
        "cp.s1.gradle-dependency-cache" => "Gradle 依赖缓存",
        "cp.s1.maven-repository-cache" => "Maven 本地仓库缓存",
        "cp.s1.nuget-global-packages" => "NuGet 全局包缓存",
        "cp.s1.deno-cache" => "Deno 缓存",
        "cp.s1.bun-install-cache" => "Bun 安装缓存",
        "cp.s1.python-bytecode-cache" => "Python 字节码缓存",
        "cp.s1.vscode-cache" => "Visual Studio Code 缓存",
        "cp.s1.jetbrains-cache" => "JetBrains IDE 缓存",
        "cp.s1.electron-app-ui-cache" => "Electron 应用界面缓存",
        "cp.s1.windows-thumbnail-cache" => "Windows 缩略图缓存",
        "cp.s1.edge-cache" => "Microsoft Edge 缓存",
        "cp.s1.chrome-cache" => "Google Chrome 缓存",
        "cp.s1.brave-cache" => "Brave 浏览器缓存",
        "cp.s1.chromium-cache" => "Chromium 浏览器缓存",
        "cp.s1.vivaldi-cache" => "Vivaldi 浏览器缓存",
        "cp.s1.opera-cache" => "Opera 浏览器缓存",
        "cp.s1.firefox-cache" => "Firefox 缓存",
        "cp.s1.steam-httpcache" => "Steam 启动器缓存",
        "cp.s1.steam-logs" => "Steam 启动器日志",
        "cp.s1.steam-dumps" => "Steam 启动器转储",
        "cp.s1.epic-webcache" => "Epic 启动器缓存",
        "cp.s1.epic-logs" => "Epic 启动器日志",
        "cp.s1.battlenet-cache" => "Battle.net 启动器缓存",
        "cp.s1.battlenet-logs" => "Battle.net 启动器日志",
        "cp.s1.riot-client-cache" => "Riot Client 缓存",
        "cp.s1.riot-client-logs" => "Riot Client 日志",
        "cp.s1.ea-app-cache" => "EA App 缓存",
        "cp.s1.ea-app-logs" => "EA App 日志",
        "cp.s1.ubisoft-connect-cache" => "Ubisoft Connect 缓存",
        "cp.s1.ubisoft-connect-logs" => "Ubisoft Connect 日志",
        _ => candidate.Category
    };
}

#pragma warning disable CS8321 // local function is declared but never used
static string FormatCleanupCandidateExplanation(MessageCatalog text, CleanupCandidate candidate)
{
    if (text.Language != Language.SimplifiedChinese)
    {
        return candidate.Explanation;
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-logs" or
        "cp.s1.vscode-logs" or
        "cp.s1.jetbrains-logs")
    {
        return "\u65E7\u5E94\u7528\u65E5\u5FD7\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406\uff1B\u8D26\u53F7\u3001\u4F1A\u8BDD\u3001\u8BBE\u7F6E\u548C\u5DE5\u4F5C\u533A\u6570\u636E\u5DF2\u6392\u9664\u3002";
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-crash-reports" or
        "cp.s1.electron-app-crash-completed" or
        "cp.s1.vscode-crash-reports" or
        "cp.s1.vscode-crash-completed")
    {
        return "\u65E7\u7684\u5DF2\u5B8C\u6210\u5D29\u6E83\u8BCA\u65AD\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406\u3002";
    }

    return candidate.RuleId switch
    {
        "cp.s0.user-temp" => "当前用户拥有的临时文件。最近修改的文件会被跳过。",
        "cp.s1.user-crash-dumps" => "旧的用户模式应用崩溃转储。通常只对排查最近崩溃有用。",
        "cp.s1.windows-error-reports" => "旧的用户态 Windows 错误报告文件。它们主要用于诊断，未来崩溃时可以重新生成。",
        "cp.s1.directx-shader-cache" => "显卡驱动和 DirectX 可以重建着色器缓存。清理后首次启动游戏或图形应用可能更慢。",
        "cp.s1.nuget-http-cache" => "NuGet 可以重建下载缓存。清理后可能需要重新下载包。",
        "cp.s1.npm-cache" => "npm 可以重建包缓存。清理后可能需要重新下载包。",
        "cp.s1.yarn-cache" => "Yarn 可以重建包缓存。清理后可能需要重新下载包。",
        "cp.s1.pnpm-store" => "pnpm 可以重建 store 缓存。清理后可能需要重新下载包。",
        "cp.s1.pip-cache" => "pip 可以重建包缓存。清理后可能需要重新下载包。",
        "cp.s1.composer-cache" => "Composer 可以重建包缓存。清理后可能需要重新下载包。",
        "cp.s1.go-cache" => "Go 可以重建构建缓存和模块下载缓存。清理后未来构建可能变慢或需要重新下载模块。",
        "cp.s1.cargo-registry-cache" => "Cargo 可以重建已下载的 registry 包归档。清理后未来构建可能变慢。",
        "cp.s1.cargo-git-cache" => "Cargo 可以重建 git 依赖缓存。清理后未来构建可能变慢。",
        "cp.s1.gradle-dependency-cache" => "Gradle 可以重建依赖缓存。清理后未来构建可能变慢。",
        "cp.s1.maven-repository-cache" => "Maven 可以重建已下载依赖。清理较旧缓存后未来构建可能变慢或需要下载。",
        "cp.s1.nuget-global-packages" => "NuGet 包归档可以重新下载。清理后未来构建可能变慢。",
        "cp.s1.deno-cache" => "Deno 可以重建依赖和转译缓存。清理后可能需要重新下载或重新编译。",
        "cp.s1.bun-install-cache" => "Bun 可以重建包安装缓存。清理后可能需要重新下载包。",
        "cp.s1.python-bytecode-cache" => "Python 可以重建字节码缓存文件。此规则会排除源码文件和虚拟环境。",
        "cp.s1.vscode-cache" => "VS Code 可以重建这些界面缓存。扩展、设置和工作区存储会被排除。",
        "cp.s1.jetbrains-cache" => "JetBrains IDE 可以重建缓存目录。配置、插件和项目会被排除。",
        "cp.s1.electron-app-ui-cache" => "Electron 应用可以重建 Cache、Code Cache 和 GPUCache。设置、本地存储、会话和数据库会被排除。",
        "cp.s1.windows-thumbnail-cache" => "Windows 可以重建缩略图和图标缓存数据库。清理后文件夹缩略图可能会重新生成。",
        "cp.s1.edge-cache" or
        "cp.s1.chrome-cache" or
        "cp.s1.brave-cache" or
        "cp.s1.chromium-cache" or
        "cp.s1.vivaldi-cache" or
        "cp.s1.opera-cache" => "浏览器缓存文件可以重建。身份、历史记录、书签、Cookie、密码和会话数据会被排除。",
        "cp.s1.firefox-cache" => "Firefox 可以重建这些缓存目录。配置文件、Cookie、登录信息、书签、历史记录和会话会被排除。",
        "cp.s1.steam-httpcache" => "Steam 启动器 HTTP 缓存。已排除游戏库、工坊和账户/会话数据路径。",
        "cp.s1.steam-logs" => "Steam 启动器日志。已排除游戏内容和身份状态路径。",
        "cp.s1.steam-dumps" => "Steam 启动器转储文件。用于崩溃诊断，清理前请确认不再需要。",
        "cp.s1.epic-webcache" => "Epic 启动器 Web/UI 缓存。已排除身份、会话和存储状态路径。",
        "cp.s1.epic-logs" => "Epic 启动器日志文件。",
        "cp.s1.battlenet-cache" => "Battle.net 启动器缓存目录（仅限明确缓存路径）。",
        "cp.s1.battlenet-logs" => "Battle.net 启动器日志目录。",
        "cp.s1.riot-client-cache" => "Riot Client 启动器缓存目录。已排除游戏/配置/账户状态路径。",
        "cp.s1.riot-client-logs" => "Riot Client 启动器日志目录。",
        "cp.s1.ea-app-cache" => "EA App 启动器缓存目录。",
        "cp.s1.ea-app-logs" => "EA App 启动器日志目录。",
        "cp.s1.ubisoft-connect-cache" => "Ubisoft Connect 启动器缓存目录。",
        "cp.s1.ubisoft-connect-logs" => "Ubisoft Connect 启动器日志目录。",
        _ => candidate.Explanation
    };
}

static void Pause(MessageCatalog text)
{
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.MessagePressEnterToContinue));
    _ = Console.ReadLine();
    Console.WriteLine();
}

static string FormatLanguage(Language language, MessageCatalog text)
{
    return language switch
    {
        Language.SimplifiedChinese => text.Get(StringKey.SettingsLanguageSimplifiedChinese),
        _ => text.Get(StringKey.SettingsLanguageEnglish)
    };
}

static string FormatEnabled(bool value, MessageCatalog text)
{
    return value
        ? text.Get(StringKey.SettingsValueEnabled)
        : text.Get(StringKey.SettingsValueDisabled);
}

static void WriteCleanupPreviewSummary(MessageCatalog text, IReadOnlyList<CleanupCandidate> candidates, string safetyMessage)
{
    var totalFiles = candidates.Sum(candidate => candidate.FileCount);
    var totalBytes = candidates.Sum(candidate => candidate.EstimatedBytes);

    WriteLineColor($"{text.Get(StringKey.CleanupPreviewTitle)}:", Theme.Heading);
    WriteLabelValue(text.Get(StringKey.CleanupPreviewRules), candidates.Count.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.CleanupPreviewFiles), totalFiles.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.CleanupPreviewSpace), FormatBytes(totalBytes), totalBytes > 0 ? Theme.Success : Theme.Muted);
    WriteLabelValue(text.Get(StringKey.CleanupPreviewSafety), safetyMessage, Theme.Warning);

    if (candidates.Count == 0)
    {
        Console.WriteLine();
        return;
    }

    Console.WriteLine();
    WriteLineColor($"{text.Get(StringKey.CleanupPreviewTopItems)}:", Theme.Heading);
    foreach (var candidate in candidates
        .OrderByDescending(candidate => candidate.EstimatedBytes)
        .ThenBy(candidate => candidate.Category, StringComparer.OrdinalIgnoreCase)
        .Take(3))
    {
        WriteLabelValue(
            FormatCleanupCandidateCategory(text, candidate),
            $"{FormatBytes(candidate.EstimatedBytes)}   {text.Get(StringKey.RecommendedScanFiles)}: {candidate.FileCount}",
            GetRiskColor(candidate.RiskLevel));
    }

    Console.WriteLine();
}

static void WriteHeader(string text)
{
    WritePanel(text, []);
}

static void StartPage(string title)
{
    ClearScreen();
    Console.WriteLine();
    WriteHeader(title);
}

static void StartMainMenuPage()
{
    ResetMainMenuViewport();
    ClearScreen(clearFullBuffer: true);
    WriteAppBanner();
}

static void WriteAppBanner()
{
    WritePanel(
        "ClearPilot",
        [
            "Windows cleanup assistant",
            "S0 auto-clean   S1 confirm-first   S2 review-only"
        ]);
}

static void WriteMenuOption(string number, string label, ConsoleColor color)
{
    WriteColor($"  {number.PadLeft(2)}", color);
    Console.Write("  ");
    WriteLineColor(label, color == Theme.Subtle ? Theme.Muted : Theme.Text);
}

static void WriteCommandOption(string number, string label, string description, ConsoleColor color)
{
    var isExit = color == Theme.Subtle;
    WriteColor($"  {number.PadLeft(2)}", color);
    WriteColor("  │ ", isExit ? Theme.Subtle : color);
    WriteColor(FitCell(label, 28), isExit ? Theme.Muted : color);
    WriteLineColor(FitCell(description, 34), Theme.Subtle);
}

static void WriteBadgeNotice(string badge, ConsoleColor badgeColor, string text)
{
    Console.Write("   ");
    WriteColor(FitCell(badge, 12), badgeColor);
    WriteColor(" ", Theme.Subtle);
    WriteLineColor(text, Theme.Muted);
}

static void WriteResultCard(
    int number,
    string title,
    string meta,
    IReadOnlyList<CardDetailLine> details,
    ConsoleColor accentColor,
    ConsoleColor? metaColor = null)
{
    var detailWidth = GetCardDetailWidth();
    var metaWidth = string.IsNullOrWhiteSpace(meta)
        ? 0
        : Math.Clamp(14, 10, Math.Max(10, detailWidth / 4));
    if (detailWidth - metaWidth < 12)
    {
        metaWidth = Math.Max(0, detailWidth - 12);
    }

    var titleWidth = Math.Max(12, detailWidth - metaWidth);

    var effectiveMetaColor = metaColor ?? accentColor;
    WriteColor($"  {number,2}", accentColor);
    WriteColor(" │ ", Theme.Subtle);
    WriteColor(FitCell(title, titleWidth), Theme.Text);
    if (!string.IsNullOrWhiteSpace(meta))
    {
        WriteColor(FitCell(meta, metaWidth, alignRight: true), effectiveMetaColor);
    }
    Console.WriteLine();

    foreach (var detail in details)
    {
        WriteCardDetail(detail, detailWidth);
    }
}

static void WriteCardDetail(CardDetailLine detail, int width)
{
    if (!detail.HasHighlight)
    {
        WriteWrappedCardDetailText(detail.Prefix, width);
        return;
    }

    foreach (var line in ConsoleTextLayout.WrapHighlightedDetail(detail.Prefix, detail.HighlightText, detail.Suffix, width))
    {
        WriteColor("     │ ", Theme.Subtle);
        var prefix = line.Prefix;
        var body = line.Body;
        var remainingWidth = width;

        if (!string.IsNullOrEmpty(prefix))
        {
            WriteColor(prefix, detail.PrefixColor);
            remainingWidth -= GetTextDisplayWidth(prefix);
        }

        if (remainingWidth > 0 && !string.IsNullOrEmpty(body))
        {
            WriteColor(FitCell(body, remainingWidth), detail.HighlightColor);
            remainingWidth = 0;
        }
        else if (remainingWidth > 0)
        {
            Console.Write(new string(' ', remainingWidth));
        }

        Console.WriteLine();
    }
}

static void WriteWrappedCardDetailText(string text, int width)
{
    var lines = WrapToDisplayWidth(text, width);
    foreach (var line in lines)
    {
        WriteColor("     │ ", Theme.Subtle);
        WriteLineColor(FitCell(line, width), Theme.Muted);
    }
}

static void WritePanel(string title, IReadOnlyList<string> lines)
{
    const int width = 66;
    var topTitle = $" {title} ";
    var topTitleWidth = GetTextDisplayWidth(topTitle);
    var top = topTitleWidth >= width - 2
        ? "╭" + new string('─', width - 2) + "╮"
        : "╭─" + topTitle + new string('─', width - topTitleWidth - 3) + "╮";

    WriteLineColor(top, Theme.Frame);
    for (var index = 0; index < lines.Count; index++)
    {
        var line = lines[index];
        var lineColor = index == 0 ? Theme.Text : Theme.Muted;

        WriteColor("│ ", Theme.Frame);
        WriteColor(FitCell(line, width - 4), lineColor);
        WriteLineColor(" │", Theme.Frame);
    }
    WriteLineColor("╰" + new string('─', width - 2) + "╯", Theme.Frame);
}

static string FitCell(string value, int width, bool alignRight = false)
{
    return ConsoleTextLayout.FitCell(value, width, alignRight);
}

static string TruncateToDisplayWidth(string value, int maxWidth)
{
    return ConsoleTextLayout.TruncateToDisplayWidth(value, maxWidth);
}

static IReadOnlyList<string> WrapToDisplayWidth(string value, int maxWidth)
{
    return ConsoleTextLayout.WrapToDisplayWidth(value, maxWidth);
}

static int GetTextDisplayWidth(string value)
{
    return ConsoleTextLayout.GetTextDisplayWidth(value);
}

static int GetRuneDisplayWidth(Rune rune)
{
    return ConsoleTextLayout.GetRuneDisplayWidth(rune);
}

static int GetCardDetailWidth()
{
    const int fallbackWidth = 70;
    const int minWidth = 40;

    if (Console.IsOutputRedirected)
    {
        return fallbackWidth;
    }

    try
    {
        return Math.Max(minWidth, Console.WindowWidth - 8);
    }
    catch (IOException)
    {
        return fallbackWidth;
    }
    catch (PlatformNotSupportedException)
    {
        return fallbackWidth;
    }
}

static void WritePrompt(string text)
{
    var prompt = text
        .Replace(": ", " › ", StringComparison.Ordinal)
        .Replace("：", " › ", StringComparison.Ordinal);

    WriteColor(prompt, Theme.Accent);
}

static void WriteLabelValue(string label, string? value, ConsoleColor valueColor)
{
    const int labelWidth = 26;
    var labelColor = valueColor == Theme.Subtle
        ? Theme.Muted
        : Theme.Text;

    Console.Write("   ");
    WriteColor(FitCell(label, labelWidth), labelColor);
    WriteColor(" : ", Theme.Subtle);
    WriteLineColor(value ?? string.Empty, valueColor);
}

static string FormatRiskBadge(RiskLevel riskLevel)
{
    return riskLevel switch
    {
        RiskLevel.S0VeryLowRisk => "S0 SAFE",
        RiskLevel.S1LowRisk => "S1 CONFIRM",
        RiskLevel.S2ReviewRequired => "S2 REVIEW",
        RiskLevel.S3DoNotCleanAutomatically => "S3 MANUAL",
        RiskLevel.Blocked => "BLOCKED",
        _ => riskLevel.ToString()
    };
}

static string FormatRecommendationBadge(MessageCatalog text, RecommendationLevel recommendation)
{
    if (text.Language == Language.SimplifiedChinese)
    {
        return recommendation switch
        {
            RecommendationLevel.Recommended => "推荐",
            RecommendationLevel.Optional => "可选",
            RecommendationLevel.NotRecommended => "不推荐",
            RecommendationLevel.ReviewOnly => "仅复核",
            RecommendationLevel.Blocked => "已阻止",
            _ => recommendation.ToString()
        };
    }

    return recommendation switch
    {
        RecommendationLevel.Recommended => "Recommended",
        RecommendationLevel.Optional => "Optional",
        RecommendationLevel.NotRecommended => "Not Recommended",
        RecommendationLevel.ReviewOnly => "Review Only",
        RecommendationLevel.Blocked => "Blocked",
        _ => recommendation.ToString()
    };
}

static string GetQuickSafetyBoundaryMessage(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "仅包含 S0 且推荐的项目。不会删除文档、已安装游戏、浏览器身份数据或系统管理缓存。"
        : "S0 + Recommended only. Will not remove documents, installed games, browser identity data, or system-managed caches.";
}

static string GetRecommendationBoundaryMessage(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "推荐清理仅包含 S1 项目；S2/S3/BLOCKED 永不删除。"
        : "Recommended Cleanup contains S1 items only; S2/S3/BLOCKED are never deleted.";
}

static string GetRecommendedConfirmationBoundaryLine1(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "本次操作仅包含 S1 目标。"
        : "Only S1 targets are included in this operation.";
}

static string GetRecommendedConfirmationBoundaryLine2(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "提交选择后才会开始清理。"
        : "Cleanup starts only after you submit the selection.";
}

static string GetRecommendedConfirmationBoundaryLine3(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "S2/S3/BLOCKED 目标不会被删除。"
        : "S2/S3/BLOCKED targets will not be deleted.";
}

static string GetRecommendedConfirmationBoundaryLine4(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "不会删除游戏/存档，也不会删除浏览器身份/会话数据。"
        : "Games/saves and browser identity/session data are not removed.";
}

static string GetProcessGuardPreviewText(
    MessageCatalog text,
    CleanupCandidate candidate,
    IReadOnlyDictionary<string, CleanupRule> ruleMap,
    IProcessInspector processInspector,
    bool? launcherRunningOverride = null)
{
    if (!ruleMap.TryGetValue(candidate.RuleId, out var rule) || rule.EffectiveProcessGuardNames.Count == 0)
    {
        return text.Language == Language.SimplifiedChinese
            ? "进程守卫：不适用。"
            : "Process guard: not applicable.";
    }

    var running = launcherRunningOverride ?? processInspector.IsAnyRunning(rule.EffectiveProcessGuardNames);
    if (running)
    {
        return text.Language == Language.SimplifiedChinese
            ? "由于应用正在运行，已跳过。"
            : "Skipped because the app is running.";
    }

    return text.Language == Language.SimplifiedChinese
        ? "进程守卫：通过。"
        : "Process guard: passed.";
}

static bool IsProcessGuardBlocked(
    CleanupCandidate candidate,
    IReadOnlyDictionary<string, CleanupRule> ruleMap,
    IProcessInspector processInspector)
{
    if (!ruleMap.TryGetValue(candidate.RuleId, out var rule) || rule.EffectiveProcessGuardNames.Count == 0)
    {
        return false;
    }

    return processInspector.IsAnyRunning(rule.EffectiveProcessGuardNames);
}

static string FormatCleanupDecisionBadge(MessageCatalog text, CleanupDecision decision)
{
    if (text.Language == Language.SimplifiedChinese)
    {
        return decision switch
        {
            CleanupDecision.RecommendedToClean => "建议清理",
            CleanupDecision.NotRecommendedToClean => "不建议清理",
            CleanupDecision.AnalysisOnlyDoNotClean => "仅分析，不清理",
            CleanupDecision.Blocked => "已阻止",
            _ => decision.ToString()
        };
    }

    return decision switch
    {
        CleanupDecision.RecommendedToClean => "Recommended to clean",
        CleanupDecision.NotRecommendedToClean => "Not recommended to clean",
        CleanupDecision.AnalysisOnlyDoNotClean => "Analysis only, do not clean",
        CleanupDecision.Blocked => "Blocked",
        _ => decision.ToString()
    };
}

static ConsoleColor GetDecisionColor(CleanupDecision decision)
{
    return decision switch
    {
        CleanupDecision.RecommendedToClean => Theme.Success,
        CleanupDecision.NotRecommendedToClean => Theme.Warning,
        CleanupDecision.AnalysisOnlyDoNotClean => Theme.Review,
        CleanupDecision.Blocked => ConsoleColor.DarkRed,
        _ => Theme.Muted
    };
}

static string GetDecisionLabel(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese ? "结论" : "Decision";
}

static string GetReasonLabel(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese ? "原因" : "Reason";
}

static string GetImpactLabel(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese ? "影响" : "Possible impact";
}

static string GetSafetyNoteLabel(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese ? "安全说明" : "Safety note";
}

#pragma warning restore CS8321
static string GetAppRunningSkipReason(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "已跳过，因为相关应用正在运行"
        : "Skipped because the app is running.";
}

static string FormatCleanupDecisionBadgeV46(MessageCatalog text, CleanupDecision decision)
{
    return ConsolePresentationStyle.GetDecisionBadge(text.Language, decision);
}

static ConsoleColor GetDecisionColorV46(CleanupDecision decision)
{
    return ConsolePresentationStyle.GetDecisionColor(decision);
}

static string GetDecisionLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetDecisionLabel(text.Language);
}

static string GetRiskLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetRiskLabel(text.Language);
}

static string GetPathLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetPathLabel(text.Language);
}

static string GetInsightLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetInsightLabel(text.Language);
}

static string GetBoundaryLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetBoundaryLabel(text.Language);
}

static string GetReasonLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetReasonLabel(text.Language);
}

static string GetImpactLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetImpactLabel(text.Language);
}

static string GetExpectedReclaimLabelV46(MessageCatalog text)
{
    return ConsolePresentationStyle.GetExpectedReclaimLabel(text.Language);
}

static string GetDeepSpaceInsightForDisplayV46(MessageCatalog text, DeepSpaceItem item)
{
    if (text.Language == Language.SimplifiedChinese)
    {
        if (IsLikelyDownloadsPath(item.Path))
        {
            return "Downloads 常包含个人文件、安装包、压缩包和工作导出文件。";
        }

        if (IsLikelyZoomPath(item.Path, item.TargetId))
        {
            return "Zoom 应用数据可能包含日志、缓存、会议诊断、设置、数据库和应用状态。";
        }

        if (item.Type == DeepSpaceItemType.ProjectDependencyFolder)
        {
            return "项目依赖或构建输出目录；通常可重建，但可能影响当前活跃工作区。";
        }

        if (item.Type == DeepSpaceItemType.SystemManagedWindowsArea)
        {
            return "Windows 管理维护区域；直接改动可能影响更新或诊断状态。";
        }

        if (item.RiskLevel == RiskLevel.Blocked)
        {
            return "受保护或包含身份相关数据的区域。";
        }
    }
    else
    {
        if (IsLikelyDownloadsPath(item.Path))
        {
            return "Downloads often contains personal files, installers, archives, and work exports.";
        }

        if (IsLikelyZoomPath(item.Path, item.TargetId))
        {
            return "Zoom app data may include logs, cache, meeting diagnostics, settings, databases, and app state.";
        }

        if (item.Type == DeepSpaceItemType.ProjectDependencyFolder)
        {
            return "Project dependency or build-output folder; it is often rebuildable but can affect an active workspace.";
        }

        if (item.Type == DeepSpaceItemType.SystemManagedWindowsArea)
        {
            return "Windows-managed maintenance area; direct changes can disrupt update or diagnostic state.";
        }

        if (item.RiskLevel == RiskLevel.Blocked)
        {
            return "Protected or identity-bearing area.";
        }
    }

    return FormatDeepSpaceExplanation(text, item);
}

static string GetDeepSpaceBoundaryForDisplayV46(MessageCatalog text, DeepSpaceItem item, CleanupDecisionResult decision, TargetAdvice advice)
{
    if (decision.Decision == CleanupDecision.Blocked)
    {
        return text.Language == Language.SimplifiedChinese
            ? "受策略阻止。ClearPilot 在任何模式下都不会清理该目标。"
            : "Blocked by policy. ClearPilot will not clean this target in any mode.";
    }

    if (text.Language == Language.SimplifiedChinese)
    {
        if (IsLikelyDownloadsPath(item.Path))
        {
            return "只读结果。ClearPilot 仅报告大小，不会从 Downloads 执行删除。";
        }

        if (IsLikelyZoomPath(item.Path, item.TargetId))
        {
            return "仅证据可见（v0.4）。ClearPilot 不会清理 Zoom 数据。";
        }

        if (item.Type == DeepSpaceItemType.ProjectDependencyFolder)
        {
            return "仅复核。若需处理，请在 ClearPilot 之外使用项目所属工具执行。";
        }

        if (item.Type == DeepSpaceItemType.SystemManagedWindowsArea)
        {
            return "ClearPilot 不会清理该区域。请使用 Windows 设置、存储感知或磁盘清理。";
        }

        return "只读结果。ClearPilot 不会删除它；打开位置只会在文件资源管理器中显示。";
    }

    if (IsLikelyDownloadsPath(item.Path))
    {
        return "Read-only insight. ClearPilot reports size only and does not delete from Downloads.";
    }

    if (IsLikelyZoomPath(item.Path, item.TargetId))
    {
        return "Evidence only in v0.4. ClearPilot does not clean Zoom data.";
    }

    if (item.Type == DeepSpaceItemType.ProjectDependencyFolder)
    {
        return "Review-only. Use the owning project tool outside ClearPilot if you decide to change it.";
    }

    if (item.Type == DeepSpaceItemType.SystemManagedWindowsArea)
    {
        return "ClearPilot will not clean it. Use Windows Settings, Storage Sense, or Disk Cleanup.";
    }

    return "Read-only finding. ClearPilot will not delete it; open location only shows it in File Explorer.";
}

static bool IsLikelyDownloadsPath(string path)
{
    return path.Contains($"{Path.DirectorySeparatorChar}Downloads{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.AltDirectorySeparatorChar}Downloads{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith($"{Path.DirectorySeparatorChar}Downloads", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith($"{Path.AltDirectorySeparatorChar}Downloads", StringComparison.OrdinalIgnoreCase);
}

static bool IsLikelyZoomPath(string path, string targetId)
{
    return targetId.Contains("zoom", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.DirectorySeparatorChar}Zoom{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith($"{Path.DirectorySeparatorChar}Zoom", StringComparison.OrdinalIgnoreCase)
        || path.Contains($"{Path.AltDirectorySeparatorChar}Zoom{Path.AltDirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith($"{Path.AltDirectorySeparatorChar}Zoom", StringComparison.OrdinalIgnoreCase);
}

static string GetDecisionReasonForDisplayV46(MessageCatalog text, CleanupCandidate candidate, CleanupDecision decision)
{
    if (text.Language != Language.SimplifiedChinese)
    {
        return candidate.CleanupDecisionReason;
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-logs" or
        "cp.s1.vscode-logs" or
        "cp.s1.jetbrains-logs")
    {
        return "\u65E7\u5E94\u7528\u65E5\u5FD7\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406\uff1B\u8D26\u53F7\u3001\u4F1A\u8BDD\u3001\u8BBE\u7F6E\u548C\u5DE5\u4F5C\u533A\u6570\u636E\u5DF2\u6392\u9664\u3002";
    }

    if (candidate.RuleId is
        "cp.s1.electron-app-crash-reports" or
        "cp.s1.electron-app-crash-completed" or
        "cp.s1.vscode-crash-reports" or
        "cp.s1.vscode-crash-completed")
    {
        return "\u65E7\u7684\u5DF2\u5B8C\u6210\u5D29\u6E83\u8BCA\u65AD\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406\u3002";
    }

    return decision switch
    {
        CleanupDecision.RecommendedToClean => candidate.RuleId switch
        {
            "cp.s0.user-temp" => "这是可重建的用户临时文件。",
            "cp.s1.windows-temp" => "这是可访问范围内的旧临时文件。",
            "cp.s1.windows-inet-cache" => "这是明确的缓存数据，已排除身份与会话数据。",
            "cp.s1.msstore-localcache" => "这是应用 LocalCache 缓存路径，已排除持久状态目录。",
            "cp.s1.steam-httpcache" => "这是 Steam 可重建的界面缓存。",
            _ when candidate.RuleId.Contains("cache", StringComparison.OrdinalIgnoreCase) => "这是可重建的缓存数据。",
            _ => "该项目满足当前清理建议条件。"
        },
        CleanupDecision.NotRecommendedToClean => candidate.RuleId switch
        {
            "cp.s1.user-crash-dumps" => "这些崩溃转储可能仍有助于排查问题。",
            _ when candidate.RuleId.Contains("log", StringComparison.OrdinalIgnoreCase) => "这些日志可能仍有诊断价值。",
            _ when candidate.RuleId.Contains("dump", StringComparison.OrdinalIgnoreCase) => "这些转储文件可能仍有诊断价值。",
            _ => "当前不建议清理该项目。"
        },
        CleanupDecision.AnalysisOnlyDoNotClean => "这是仅分析项，不会执行删除。",
        CleanupDecision.Blocked => "该项目已被安全策略阻止。",
        _ => candidate.CleanupDecisionReason
    };
}

static string GetPossibleImpactForDisplayV46(MessageCatalog text, CleanupCandidate candidate, CleanupDecision decision)
{
    if (text.Language != Language.SimplifiedChinese)
    {
        return candidate.PossibleImpact;
    }

    if (decision is CleanupDecision.RecommendedToClean or CleanupDecision.NotRecommendedToClean)
    {
        if (candidate.RuleId is
            "cp.s1.electron-app-logs" or
            "cp.s1.vscode-logs" or
            "cp.s1.jetbrains-logs")
        {
            return "\u5386\u53F2\u95EE\u9898\u6392\u67E5\u65E5\u5FD7\u53EF\u80FD\u4E0D\u518D\u53EF\u7528\u3002";
        }

        if (candidate.RuleId is
            "cp.s1.electron-app-crash-reports" or
            "cp.s1.electron-app-crash-completed" or
            "cp.s1.vscode-crash-reports" or
            "cp.s1.vscode-crash-completed")
        {
            return "\u5386\u53F2\u5D29\u6E83\u6392\u67E5\u6570\u636E\u53EF\u80FD\u4E0D\u518D\u53EF\u7528\u3002";
        }
    }

    return decision switch
    {
        CleanupDecision.RecommendedToClean => "相关应用下次启动时可能会重建缓存或重新加载资源。",
        CleanupDecision.NotRecommendedToClean when candidate.RuleId.Contains("log", StringComparison.OrdinalIgnoreCase)
            || candidate.RuleId.Contains("dump", StringComparison.OrdinalIgnoreCase)
            || candidate.RuleId.Contains("crash", StringComparison.OrdinalIgnoreCase)
            => "清理后可能丢失用于排查问题的诊断信息。",
        CleanupDecision.NotRecommendedToClean => "当前清理收益可能低于潜在影响。",
        CleanupDecision.AnalysisOnlyDoNotClean => "这是复核项，ClearPilot 不会删除。",
        CleanupDecision.Blocked => "该路径受保护，无法执行清理。",
        _ => candidate.PossibleImpact
    };
}

static void WriteQuickSafeSkippedReasonSummary(MessageCatalog text, CleanupRunResult result)
{
    var skippedItems = result.Items
        .Where(item => item.Action == CleanupItemAction.Skipped)
        .ToArray();
    if (skippedItems.Length == 0)
    {
        return;
    }

    var buckets = skippedItems
        .GroupBy(item => ClassifyQuickSafeSkippedReasonKey(text, item), StringComparer.Ordinal)
        .Select(group => new { Label = group.Key, Count = group.Count() })
        .OrderByDescending(entry => entry.Count)
        .ThenBy(entry => entry.Label, StringComparer.Ordinal)
        .ToArray();

    WriteLineColor($"{text.Get(StringKey.QuickSafeCleanSkippedReasonSummary)}:", Theme.Muted);
    foreach (var bucket in buckets)
    {
        WriteLabelValue(bucket.Label, bucket.Count.ToString(), Theme.Subtle);
    }
}

static string ClassifyQuickSafeSkippedReasonKey(MessageCatalog text, CleanupItemResult item)
{
    var reason = item.SafetyDecision?.SkippedReason ?? item.Message ?? item.ProcessGuardResult ?? string.Empty;
    if (string.IsNullOrWhiteSpace(reason))
    {
        return text.Get(StringKey.QuickSafeCleanSkippedReasonOther);
    }

    var normalized = reason.Trim().ToLowerInvariant();
    if (normalized.Contains("locked", StringComparison.Ordinal)
        || normalized.Contains("in use", StringComparison.Ordinal)
        || normalized.Contains("used by", StringComparison.Ordinal)
        || normalized.Contains("running", StringComparison.Ordinal))
    {
        return text.Get(StringKey.QuickSafeCleanSkippedReasonLocked);
    }

    if (normalized.Contains("access denied", StringComparison.Ordinal)
        || normalized.Contains("unauthorized", StringComparison.Ordinal)
        || normalized.Contains("forbidden", StringComparison.Ordinal)
        || normalized.Contains("permission", StringComparison.Ordinal))
    {
        return text.Get(StringKey.QuickSafeCleanSkippedReasonInaccessible);
    }

    if (normalized.Contains("outsidecleanuproot", StringComparison.Ordinal)
        || normalized.Contains("outside the cleanup root", StringComparison.Ordinal)
        || normalized.Contains("notinknownsafecacheroot", StringComparison.Ordinal)
        || normalized.Contains("protected", StringComparison.Ordinal)
        || normalized.Contains("harddeniedtarget", StringComparison.Ordinal)
        || normalized.Contains("blocked", StringComparison.Ordinal)
        || normalized.Contains("safety", StringComparison.Ordinal))
    {
        return text.Get(StringKey.QuickSafeCleanSkippedReasonOutsideBoundary);
    }

    return text.Get(StringKey.QuickSafeCleanSkippedReasonOther);
}

static string GetRecommendedSelectionExecutionLineV47(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "回车后将立即清理所选 S1 项目。"
        : "Press Enter to clean the selected S1 items.";
}

static string GetDeepSpaceNoDeleteNoticeV45(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "仅分析，不清理：Deep Space 不会执行删除。Downloads 仅用于存储占用了解；Desktop/Documents/Pictures/Videos/Music 默认不扫描。"
        : "Analysis only, do not clean. Deep Space does not delete files. Downloads is scanned only for storage understanding, and Desktop/Documents/Pictures/Videos/Music are not scanned by default.";
}

static string GetDeepSpaceActionHintV45(MessageCatalog text)
{
    return text.Language == Language.SimplifiedChinese
        ? "仅分析，不清理：输入编号仅打开位置。"
        : "Analysis only: enter an item number to open location; no deletion will occur.";
}

static ConsoleColor GetRiskColor(RiskLevel riskLevel)
{
    return ConsolePresentationStyle.GetRiskColor(riskLevel);
}

static void WriteLineColor(string text, ConsoleColor color)
{
    WriteColor(text, color);
    Console.WriteLine();
}

static void WriteColor(string text, ConsoleColor color)
{
    if (!ConsolePresentationStyle.ShouldUseColor(Console.IsOutputRedirected, Environment.GetEnvironmentVariable("NO_COLOR")))
    {
        Console.Write(text);
        return;
    }

    var previousColor = Console.ForegroundColor;
    Console.ForegroundColor = color;
    Console.Write(text);
    Console.ForegroundColor = previousColor;
}

static void ConfigureConsole()
{
    Console.Title = "ClearPilot";

    try
    {
        Console.BackgroundColor = ConsoleColor.Black;
        Console.ForegroundColor = Theme.Muted;

        if (OperatingSystem.IsWindows() && !Console.IsOutputRedirected)
        {
            EnableVirtualTerminalProcessing();

            var width = Math.Min(Math.Max(Console.WindowWidth, 92), Console.LargestWindowWidth);
            var height = Math.Min(Math.Max(Console.WindowHeight, 32), Console.LargestWindowHeight);
            Console.SetWindowSize(width, height);
            Console.SetBufferSize(Math.Max(width, Console.BufferWidth), Math.Max(height, Console.BufferHeight));
        }
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
    catch (PlatformNotSupportedException)
    {
    }
}

static void ClearScreen(bool clearFullBuffer = false)
{
    if (Console.IsOutputRedirected)
    {
        return;
    }

    Console.ResetColor();
    EnableVirtualTerminalProcessing();

    var usedNativeFullClear = false;

    try
    {
        if (OperatingSystem.IsWindows())
        {
            if (clearFullBuffer)
            {
                ClearNativeConsoleBuffer();
                Console.Write("\u001b[0m\u001b[3J\u001b[2J\u001b[H");
                usedNativeFullClear = true;
            }
            else
            {
                Console.Write("\u001b[0m\u001b[2J\u001b[H");
            }
        }

        Console.Clear();
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
    finally
    {
        if (clearFullBuffer)
        {
            if (!usedNativeFullClear)
            {
                ClearFullBufferFallback();
            }
        }
        else
        {
            ClearVisibleWindowFallback();
        }
    }
}

static void EnableVirtualTerminalProcessing()
{
    if (!OperatingSystem.IsWindows() || Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        var handle = GetStdHandle(-11);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return;
        }

        if (GetConsoleMode(handle, out var mode))
        {
            SetConsoleMode(handle, mode | 0x0004);
        }
    }
    catch (DllNotFoundException)
    {
    }
    catch (EntryPointNotFoundException)
    {
    }
}

static void ClearFullBufferFallback()
{
    if (Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        var width = Math.Max(1, Console.BufferWidth);
        var height = Math.Max(1, Console.BufferHeight);
        var blank = new string(' ', Math.Max(1, width - 1));

        Console.SetCursorPosition(0, 0);
        for (var row = 0; row < height; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(blank);
        }

        Console.SetCursorPosition(0, 0);
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
        ClearVisibleWindowFallback();
    }
    catch (PlatformNotSupportedException)
    {
    }
}

static void ClearNativeConsoleBuffer()
{
    if (!OperatingSystem.IsWindows() || Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        var handle = GetStdHandle(-11);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return;
        }

        if (!GetConsoleScreenBufferInfo(handle, out var info))
        {
            return;
        }

        var home = new ConsoleCoordinate(0, 0);
        var cells = (uint)Math.Max(0, info.Size.X * info.Size.Y);
        FillConsoleOutputCharacter(handle, ' ', cells, home, out _);
        FillConsoleOutputAttribute(handle, info.Attributes, cells, home, out _);
        SetConsoleCursorPosition(handle, home);
    }
    catch (DllNotFoundException)
    {
    }
    catch (EntryPointNotFoundException)
    {
    }
    catch (OverflowException)
    {
    }
}

static void ResetMainMenuViewport()
{
    if (Console.IsOutputRedirected || !OperatingSystem.IsWindows())
    {
        return;
    }

    try
    {
        var currentWindowWidth = Math.Max(1, Console.WindowWidth);
        var currentWindowHeight = Math.Max(1, Console.WindowHeight);
        var targetWidth = Math.Min(Math.Max(currentWindowWidth, 92), Console.LargestWindowWidth);
        var targetHeight = Math.Min(Math.Max(currentWindowHeight, 32), Console.LargestWindowHeight);

        if (Console.BufferWidth < targetWidth || Console.BufferHeight < targetHeight)
        {
            Console.SetBufferSize(
                Math.Max(Console.BufferWidth, targetWidth),
                Math.Max(Console.BufferHeight, targetHeight));
        }

        if (Console.WindowWidth != targetWidth || Console.WindowHeight != targetHeight)
        {
            Console.SetWindowSize(targetWidth, targetHeight);
        }

        Console.SetWindowPosition(0, 0);
        Console.SetBufferSize(targetWidth, targetHeight);
        Console.SetCursorPosition(0, 0);
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
    catch (PlatformNotSupportedException)
    {
    }
}

[DllImport("kernel32.dll", SetLastError = true)]
static extern IntPtr GetStdHandle(int nStdHandle);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetConsoleMode(IntPtr hConsoleHandle, out int lpMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetConsoleMode(IntPtr hConsoleHandle, int dwMode);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool GetConsoleScreenBufferInfo(IntPtr hConsoleOutput, out ConsoleScreenBufferInfo lpConsoleScreenBufferInfo);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool FillConsoleOutputCharacter(IntPtr hConsoleOutput, char character, uint length, ConsoleCoordinate writeCoordinate, out uint numberOfCharsWritten);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool FillConsoleOutputAttribute(IntPtr hConsoleOutput, short attribute, uint length, ConsoleCoordinate writeCoordinate, out uint numberOfAttrsWritten);

[DllImport("kernel32.dll", SetLastError = true)]
static extern bool SetConsoleCursorPosition(IntPtr hConsoleOutput, ConsoleCoordinate cursorPosition);

static void ClearVisibleWindowFallback()
{
    if (Console.IsOutputRedirected)
    {
        return;
    }

    try
    {
        var width = Math.Max(1, Console.WindowWidth);
        var height = Math.Max(1, Console.WindowHeight);
        var blank = new string(' ', Math.Max(1, width - 1));

        Console.SetCursorPosition(0, 0);
        for (var row = 0; row < height; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(blank);
        }

        Console.SetCursorPosition(0, 0);
    }
    catch (IOException)
    {
    }
    catch (ArgumentOutOfRangeException)
    {
    }
    catch (PlatformNotSupportedException)
    {
    }
}

static IReadOnlySet<string> ParseRecommendedSelection(
    string? selection,
    IReadOnlyList<CleanupCandidate> candidates,
    IReadOnlyDictionary<string, CleanupRule> ruleMap,
    IProcessInspector processInspector)
{
    if (string.IsNullOrWhiteSpace(selection) || selection.Trim() == "0")
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    if (string.Equals(selection.Trim(), "A", StringComparison.OrdinalIgnoreCase))
    {
        return candidates
            .Where(candidate => IsBulkSelectableRecommendedCandidate(candidate, ruleMap, processInspector))
            .Select(candidate => candidate.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    var selectedRuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var tokens = selection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    foreach (var token in tokens)
    {
        if (!int.TryParse(token, out var number))
        {
            continue;
        }

        var index = number - 1;
        if (index >= 0 && index < candidates.Count)
        {
            var candidate = candidates[index];
            if (IsBulkSelectableRecommendedCandidate(candidate, ruleMap, processInspector))
            {
                selectedRuleIds.Add(candidate.RuleId);
            }
        }
    }

    return selectedRuleIds;
}

static bool IsBulkSelectableRecommendedCandidate(
    CleanupCandidate candidate,
    IReadOnlyDictionary<string, CleanupRule> ruleMap,
    IProcessInspector processInspector)
{
    if (!ruleMap.ContainsKey(candidate.RuleId))
    {
        return false;
    }

    var processGuardBlocked = IsProcessGuardBlocked(candidate, ruleMap, processInspector);
    return ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
        candidate.RiskLevel,
        candidate.CleanupDecision,
        processGuardBlocked);
}

static DeepSpaceAnalysisOptions CreateDeepSpaceOptions()
{
    var overrideRoots = Environment.GetEnvironmentVariable("CLEARPILOT_ANALYSIS_ROOTS");
    if (string.IsNullOrWhiteSpace(overrideRoots))
    {
        return DeepSpaceAnalyzer.CreateDefaultOptions();
    }

    var roots = overrideRoots
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToArray();

    return new DeepSpaceAnalysisOptions
    {
        RootPaths = roots,
        ExcludePathSegments = ["ClearPilot", "ClearPilot.Tests"],
        LargeFileThresholdBytes = 1024,
        LargeFolderThresholdBytes = 2048,
        FileTypeSummaryThresholdBytes = 1024,
        OldArchiveAge = TimeSpan.FromDays(1),
        MaxDepth = 5,
        MaxResults = 80
    };
}

static void OpenInExplorer(string path)
{
    var fullPath = Path.GetFullPath(path);
    string arguments;

    if (File.Exists(fullPath))
    {
        arguments = $"/select,\"{fullPath}\"";
    }
    else if (Directory.Exists(fullPath))
    {
        arguments = $"\"{fullPath}\"";
    }
    else
    {
        throw new InvalidOperationException("The selected path no longer exists.");
    }

    Process.Start(new ProcessStartInfo
    {
        FileName = "explorer.exe",
        Arguments = arguments,
        UseShellExecute = false
    });
}

static string FormatDeepSpaceType(MessageCatalog text, DeepSpaceItemType type)
{
    return type switch
    {
        DeepSpaceItemType.LargeFile => text.Get(StringKey.DeepAnalysisTypeLargeFile),
        DeepSpaceItemType.LargeFolder => text.Get(StringKey.DeepAnalysisTypeLargeFolder),
        DeepSpaceItemType.OldArchiveOrInstaller => text.Get(StringKey.DeepAnalysisTypeOldArchiveOrInstaller),
        DeepSpaceItemType.ProjectDependencyFolder => text.Get(StringKey.DeepAnalysisTypeProjectDependencyFolder),
        DeepSpaceItemType.FileTypeSummary => text.Get(StringKey.DeepAnalysisTypeFileTypeSummary),
        DeepSpaceItemType.SystemManagedWindowsArea => text.Language == Language.SimplifiedChinese
            ? "Windows 系统管理区域"
            : "Windows system-managed area",
        DeepSpaceItemType.GameLauncherReviewArea => text.Language == Language.SimplifiedChinese
            ? "游戏启动器复核区域"
            : "Game launcher review-only area",
        _ => type.ToString()
    };
}

static void ExportDeepSpaceReport(MessageCatalog text, DeepSpaceAnalysisResult result, IReadOnlyList<string> scanRoots)
{
    try
    {
        var writer = DeepSpaceReportWriter.CreateDefault();
        var path = writer.Write(result, scanRoots, text.Language, DateTimeOffset.UtcNow);
        WriteLabelValue(text.Get(StringKey.DeepAnalysisReportExportSuccess), path, Theme.Success);

        try
        {
            OpenInExplorer(path);
            WriteLineColor(text.Get(StringKey.DeepAnalysisOpenSuccess), Theme.Success);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            WriteLineColor($"{text.Get(StringKey.DeepAnalysisReportOpenLocationFailed)}: {ex.Message}", Theme.Warning);
        }
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
    {
        WriteLineColor($"{text.Get(StringKey.DeepAnalysisReportExportFailed)}: {ex.Message}", Theme.Danger);
    }
}

static string FormatDeepSpaceExplanation(MessageCatalog text, DeepSpaceItem item)
{
    return DeepSpaceAdviceFormatter.FormatExplanation(text.Language, item);
}

static void RenderDeepAnalysisView(
    MessageCatalog text,
    DeepSpaceAnalysisSummary summary,
    IReadOnlyList<DeepSpaceItem> allItems,
    IReadOnlyList<DeepSpaceItem> visibleItems,
    DeepSpaceItemType? currentFilter,
    DeepSpaceSortMode currentSort)
{
    StartPage(text.Get(StringKey.DeepAnalysisFound));
    WriteDeepAnalysisSummary(text, summary, allItems);
    Console.WriteLine();
    WriteLabelValue(
        text.Get(StringKey.DeepAnalysisCurrentView),
        $"{FormatDeepSpaceFilter(text, currentFilter)} / {FormatDeepSpaceSort(text, currentSort)}",
        Theme.Muted);
    WriteBadgeNotice(FormatRiskBadge(RiskLevel.S2ReviewRequired), GetRiskColor(RiskLevel.S2ReviewRequired), GetDeepSpaceActionHintV45(text));
    Console.WriteLine();

    if (visibleItems.Count == 0)
    {
        WriteLineColor(text.Get(StringKey.DeepAnalysisNoFilteredItems), Theme.Subtle);
        Console.WriteLine();
        return;
    }

    WriteDeepSpaceItems(text, visibleItems);
}

static void WriteDeepSpaceItems(MessageCatalog text, IReadOnlyList<DeepSpaceItem> items)
{
    var displayNumber = 1;
    foreach (var group in items.GroupBy(item => item.Type).OrderBy(group => GetDeepSpaceTypeOrder(group.Key)))
    {
        WriteLineColor($"{FormatDeepSpaceType(text, group.Key)} ({group.Count()}, {FormatBytes(group.Sum(item => item.SizeBytes))})", Theme.Muted);

        foreach (var item in group)
        {
            var advice = RecommendationAdvisor.ForDeepSpaceItem(item);
            var decision = CleanupDecisionAdvisor.ForDeepSpaceItem(item, advice);
            var displayTitle = Path.GetFileName(item.Path);
            if (string.IsNullOrWhiteSpace(displayTitle))
            {
                displayTitle = item.Path;
            }

            WriteResultCard(
                displayNumber,
                displayTitle,
                FormatBytes(item.SizeBytes),
                [
                    CardDetailLine.WithHighlight(
                        $"{GetDecisionLabelV46(text)}: ",
                        FormatCleanupDecisionBadgeV46(text, decision.Decision),
                        GetDecisionColorV46(decision.Decision),
                        string.Empty,
                        prefixColor: ConsolePresentationStyle.GetDeepSpaceDecisionLabelColor(decision.Decision)),
                    CardDetailLine.WithHighlight(
                        $"{GetRiskLabelV46(text)}: ",
                        FormatRiskBadge(item.RiskLevel),
                        GetRiskColor(item.RiskLevel),
                        string.Empty,
                        prefixColor: ConsolePresentationStyle.GetDeepSpaceRiskLabelColor(item.RiskLevel)),
                    CardDetailLine.WithHighlight(
                        $"{GetPathLabelV46(text)}: ",
                        item.Path,
                        ConsolePresentationStyle.GetDeepSpacePathColor(),
                        prefixColor: ConsolePresentationStyle.GetDeepSpacePathLabelColor()),
                    CardDetailLine.WithHighlight(
                        $"{GetInsightLabelV46(text)}: ",
                        GetDeepSpaceInsightForDisplayV46(text, item),
                        ConsolePresentationStyle.GetDeepSpaceInsightColor(),
                        prefixColor: ConsolePresentationStyle.GetDeepSpaceInsightLabelColor()),
                    CardDetailLine.WithHighlight(
                        $"{GetBoundaryLabelV46(text)}: ",
                        GetDeepSpaceBoundaryForDisplayV46(text, item, decision, advice),
                        ConsolePresentationStyle.GetDeepSpaceBoundaryColor(decision.Decision),
                        prefixColor: ConsolePresentationStyle.GetDeepSpaceBoundaryLabelColor(decision.Decision)),
                ],
                ConsolePresentationStyle.GetDeepSpaceCardColor(),
                metaColor: ConsolePresentationStyle.GetDeepSpaceSizeColor());
            displayNumber++;
            Console.WriteLine();
        }

        Console.WriteLine();
    }
}

static void WriteDeepAnalysisSummary(MessageCatalog text, DeepSpaceAnalysisSummary summary, IReadOnlyList<DeepSpaceItem> items)
{
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisSummary)}:", Theme.Text);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedRoots), summary.ScannedRootCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedDirectories), summary.ScannedDirectoryCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedFiles), summary.ScannedFileCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisReviewItems), summary.FindingCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisReviewFootprint), FormatBytes(summary.FindingBytes), Theme.SpaceAccent);

    if (items.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisTypeTotals)}:", Theme.Text);
    foreach (var group in items.GroupBy(item => item.Type).OrderBy(group => GetDeepSpaceTypeOrder(group.Key)))
    {
        WriteLabelValue(FormatDeepSpaceType(text, group.Key), $"{group.Count()}   {FormatBytes(group.Sum(item => item.SizeBytes))}", Theme.SpaceAccent);
    }

    Console.WriteLine();
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisTopSources)}:", Theme.Text);
    foreach (var item in items.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(3))
    {
        WriteLabelValue(FormatDeepSpaceType(text, item.Type), $"{FormatBytes(item.SizeBytes)}   {item.Path}", Theme.SpaceAccent);
    }
}

static IReadOnlyList<DeepSpaceItem> ApplyDeepSpaceView(
    IReadOnlyList<DeepSpaceItem> items,
    DeepSpaceItemType? filter,
    DeepSpaceSortMode sortMode)
{
    var filteredItems = filter is null
        ? items
        : items.Where(item => item.Type == filter.Value).ToArray();

    return OrderDeepSpaceItemsForDisplay(filteredItems, sortMode);
}

static IReadOnlyList<DeepSpaceItem> OrderDeepSpaceItemsForDisplay(IReadOnlyList<DeepSpaceItem> items, DeepSpaceSortMode sortMode)
{
    var query = items
        .OrderBy(item => GetDeepSpaceTypeOrder(item.Type))
        .ThenByDescending(item => item.SizeBytes)
        .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase);

    if (sortMode == DeepSpaceSortMode.LastModifiedDescending)
    {
        query = items
            .OrderBy(item => GetDeepSpaceTypeOrder(item.Type))
            .ThenByDescending(item => item.LastWriteTime ?? DateTimeOffset.MinValue)
            .ThenByDescending(item => item.SizeBytes)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase);
    }

    return query.ToArray();
}

static int GetDeepSpaceTypeOrder(DeepSpaceItemType type)
{
    return type switch
    {
        DeepSpaceItemType.LargeFile => 0,
        DeepSpaceItemType.LargeFolder => 1,
        DeepSpaceItemType.OldArchiveOrInstaller => 2,
        DeepSpaceItemType.ProjectDependencyFolder => 3,
        DeepSpaceItemType.FileTypeSummary => 4,
        DeepSpaceItemType.SystemManagedWindowsArea => 5,
        DeepSpaceItemType.GameLauncherReviewArea => 6,
        _ => 100
    };
}

static DeepSpaceItemType? ChooseDeepSpaceFilter(MessageCatalog text, DeepSpaceItemType? currentFilter)
{
    StartPage(text.Get(StringKey.DeepAnalysisFilterCommand));
    Console.WriteLine();
    WriteMenuOption("1", text.Get(StringKey.DeepAnalysisFilterAll), Theme.Accent);
    WriteMenuOption("2", text.Get(StringKey.DeepAnalysisTypeLargeFile), Theme.Accent);
    WriteMenuOption("3", text.Get(StringKey.DeepAnalysisTypeLargeFolder), Theme.Accent);
    WriteMenuOption("4", text.Get(StringKey.DeepAnalysisTypeOldArchiveOrInstaller), Theme.Accent);
    WriteMenuOption("5", text.Get(StringKey.DeepAnalysisTypeProjectDependencyFolder), Theme.Accent);
    WriteMenuOption("6", text.Get(StringKey.DeepAnalysisTypeFileTypeSummary), Theme.Accent);
    WriteMenuOption("7", FormatDeepSpaceType(text, DeepSpaceItemType.SystemManagedWindowsArea), Theme.Accent);
    WriteMenuOption("8", FormatDeepSpaceType(text, DeepSpaceItemType.GameLauncherReviewArea), Theme.Accent);
    WriteMenuOption("0", text.Get(StringKey.DeepAnalysisReturn), Theme.Subtle);
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.DeepAnalysisFilterPrompt));

    var selection = Console.ReadLine();
    Console.WriteLine();

    return selection switch
    {
        "1" => null,
        "2" => DeepSpaceItemType.LargeFile,
        "3" => DeepSpaceItemType.LargeFolder,
        "4" => DeepSpaceItemType.OldArchiveOrInstaller,
        "5" => DeepSpaceItemType.ProjectDependencyFolder,
        "6" => DeepSpaceItemType.FileTypeSummary,
        "7" => DeepSpaceItemType.SystemManagedWindowsArea,
        "8" => DeepSpaceItemType.GameLauncherReviewArea,
        _ => currentFilter
    };
}

static DeepSpaceSortMode ChooseDeepSpaceSort(MessageCatalog text, DeepSpaceSortMode currentSort)
{
    StartPage(text.Get(StringKey.DeepAnalysisSortCommand));
    Console.WriteLine();
    WriteMenuOption("1", text.Get(StringKey.DeepAnalysisSortBySize), Theme.Accent);
    WriteMenuOption("2", text.Get(StringKey.DeepAnalysisSortByLastModified), Theme.Accent);
    WriteMenuOption("0", text.Get(StringKey.DeepAnalysisReturn), Theme.Subtle);
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.DeepAnalysisSortPrompt));

    var selection = Console.ReadLine();
    Console.WriteLine();

    return selection switch
    {
        "1" => DeepSpaceSortMode.SizeDescending,
        "2" => DeepSpaceSortMode.LastModifiedDescending,
        _ => currentSort
    };
}

static string FormatDeepSpaceFilter(MessageCatalog text, DeepSpaceItemType? filter)
{
    return filter is null
        ? text.Get(StringKey.DeepAnalysisFilterAll)
        : FormatDeepSpaceType(text, filter.Value);
}

static string FormatDeepSpaceSort(MessageCatalog text, DeepSpaceSortMode sortMode)
{
    return sortMode switch
    {
        DeepSpaceSortMode.LastModifiedDescending => text.Get(StringKey.DeepAnalysisSortByLastModified),
        _ => text.Get(StringKey.DeepAnalysisSortBySize)
    };
}

static string FormatDate(DateTimeOffset? value)
{
    return value is null
        ? "n/a"
        : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

static void WriteCleanupLogLocation(MessageCatalog text, string logPath)
{
    var logDirectory = Path.GetDirectoryName(logPath);
    WriteLabelValue(
        text.Get(StringKey.QuickSafeCleanLogPath),
        string.IsNullOrWhiteSpace(logDirectory) ? logPath : logDirectory,
        Theme.Subtle);
}

static string FormatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    var value = (double)bytes;
    var unitIndex = 0;

    while (value >= 1024 && unitIndex < units.Length - 1)
    {
        value /= 1024;
        unitIndex++;
    }

    return unitIndex == 0
        ? $"{bytes} {units[unitIndex]}"
        : $"{value:0.##} {units[unitIndex]}";
}

internal readonly record struct CardDetailLine(
    string Prefix,
    string HighlightText,
    ConsoleColor HighlightColor,
    string Suffix,
    ConsoleColor PrefixColor,
    ConsoleColor SuffixColor)
{
    public bool HasHighlight => !string.IsNullOrWhiteSpace(HighlightText);

    public static CardDetailLine Text(string text)
    {
        return new CardDetailLine(text, string.Empty, Theme.Subtle, string.Empty, Theme.Muted, Theme.Muted);
    }

    public static CardDetailLine WithHighlight(
        string prefix,
        string highlightText,
        ConsoleColor highlightColor,
        string suffix = "",
        ConsoleColor? prefixColor = null,
        ConsoleColor? suffixColor = null)
    {
        return new CardDetailLine(
            prefix,
            highlightText,
            highlightColor,
            suffix,
            prefixColor ?? Theme.Muted,
            suffixColor ?? Theme.Muted);
    }

    public static implicit operator CardDetailLine(string text)
    {
        return Text(text);
    }
}

internal enum DeepSpaceSortMode
{
    SizeDescending,
    LastModifiedDescending
}

internal static class Theme
{
    public const ConsoleColor Text = ConsoleColor.White;
    public const ConsoleColor Muted = ConsoleColor.Gray;
    public const ConsoleColor Subtle = ConsoleColor.DarkGray;
    public const ConsoleColor Frame = ConsoleColor.DarkCyan;
    public const ConsoleColor Accent = ConsoleColor.Cyan;
    public const ConsoleColor Heading = ConsoleColor.Cyan;
    public const ConsoleColor History = ConsoleColor.Blue;
    public const ConsoleColor Settings = ConsoleColor.Magenta;
    public const ConsoleColor Success = ConsoleColor.Green;
    public const ConsoleColor Warning = ConsoleColor.Yellow;
    public const ConsoleColor Note = ConsoleColor.DarkYellow;
    public const ConsoleColor Review = ConsoleColor.Magenta;
    public const ConsoleColor SpaceAccent = ConsoleColor.DarkMagenta;
    public const ConsoleColor Danger = ConsoleColor.Red;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ConsoleCoordinate(short x, short y)
{
    public short X = x;
    public short Y = y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ConsoleScreenBufferInfo
{
    public ConsoleCoordinate Size;
    public ConsoleCoordinate CursorPosition;
    public short Attributes;
    public ConsoleSmallRect Window;
    public ConsoleCoordinate MaximumWindowSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ConsoleSmallRect
{
    public short Left;
    public short Top;
    public short Right;
    public short Bottom;
}
