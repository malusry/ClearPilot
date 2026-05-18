using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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

RunMainMenu(settings, settingsStore);

static void RunMainMenu(AppSettings settings, SettingsStore settingsStore)
{
    while (true)
    {
        var text = MessageCatalog.For(settings.Language);

        StartMainMenuPage();
        Console.WriteLine();
        WriteCommandOption("1", text.Get(StringKey.MainMenuQuickSafeClean), text.Get(StringKey.MainMenuQuickSafeCleanDescription), Theme.Success);
        WriteCommandOption("2", text.Get(StringKey.MainMenuScanRecommendedItems), text.Get(StringKey.MainMenuScanRecommendedItemsDescription), Theme.Warning);
        WriteCommandOption("3", text.Get(StringKey.MainMenuDeepSpaceAnalysis), text.Get(StringKey.MainMenuDeepSpaceAnalysisDescription), Theme.Accent);
        WriteCommandOption("4", text.Get(StringKey.MainMenuCleanupHistory), text.Get(StringKey.MainMenuCleanupHistoryDescription), Theme.History);
        WriteCommandOption("5", text.Get(StringKey.MainMenuSettings), text.Get(StringKey.MainMenuSettingsDescription), Theme.Settings);
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
    WriteLineColor(text.Get(StringKey.DeepAnalysisReviewOnlyNotice), Theme.Warning);
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

        WriteMenuOption("F", text.Get(StringKey.DeepAnalysisFilterCommand), Theme.Accent);
        WriteMenuOption("S", text.Get(StringKey.DeepAnalysisSortCommand), Theme.Accent);
        WriteMenuOption("R", text.Get(StringKey.DeepAnalysisReportCommand), Theme.Success);
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
    var executor = new CleanupExecutor(fileScanner, logStore);
    var service = new RecommendedCleanupService(scanner, executor);
    var rules = RuleCatalog.CreateDefault();
    var candidates = service.Scan(rules, DateTimeOffset.UtcNow).ToArray();

    if (candidates.Length == 0)
    {
        WriteLineColor(text.Get(StringKey.RecommendedScanNoItems), Theme.Subtle);
        Pause(text);
        return;
    }

    WriteCleanupPreviewSummary(text, candidates, text.Get(StringKey.RecommendedPreviewSafety));
    WriteLineColor(text.Get(StringKey.RecommendedScanFound), Theme.Warning);
    WriteBadgeNotice(FormatRiskBadge(RiskLevel.S1LowRisk), GetRiskColor(RiskLevel.S1LowRisk), text.Get(StringKey.RecommendedActionHint));
    Console.WriteLine();

    for (var index = 0; index < candidates.Length; index++)
    {
        var candidate = candidates[index];
        WriteResultCard(
            index + 1,
            FormatCleanupCandidateCategory(text, candidate),
            FormatBytes(candidate.EstimatedBytes),
            [
                CardDetailLine.WithHighlight(
                    $"{text.Get(StringKey.RecommendedScanRisk)}: ",
                    FormatRiskBadge(candidate.RiskLevel),
                    GetRiskColor(candidate.RiskLevel),
                    $"   {text.Get(StringKey.RecommendedScanFiles)}: {candidate.FileCount}"),
                FormatCleanupCandidateExplanation(text, candidate)
            ],
            GetRiskColor(candidate.RiskLevel));
        Console.WriteLine();
    }

    WriteMenuOption("A", text.Get(StringKey.RecommendedSelectionAll), Theme.Warning);
    WriteMenuOption("0", text.Get(StringKey.MenuCancel), Theme.Subtle);
    Console.WriteLine();
    WritePrompt(text.Get(StringKey.RecommendedSelectionPrompt));
    var selection = Console.ReadLine();
    Console.WriteLine();

    var selectedRuleIds = ParseRecommendedSelection(selection, candidates);
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

    var result = service.Clean(selectedRules, settings.DryRun, DateTimeOffset.UtcNow);
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

    return candidate.RuleId switch
    {
        "cp.s0.user-temp" => "当前用户临时文件",
        "cp.s0.user-crash-dumps" => "当前用户崩溃转储",
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
        _ => candidate.Category
    };
}

static string FormatCleanupCandidateExplanation(MessageCatalog text, CleanupCandidate candidate)
{
    if (text.Language != Language.SimplifiedChinese)
    {
        return candidate.Explanation;
    }

    return candidate.RuleId switch
    {
        "cp.s0.user-temp" => "当前用户拥有的临时文件。最近修改的文件会被跳过。",
        "cp.s0.user-crash-dumps" => "旧的用户模式应用崩溃转储。通常只对排查最近崩溃有用。",
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

static void WriteResultCard(int number, string title, string meta, IReadOnlyList<CardDetailLine> details, ConsoleColor accentColor)
{
    const int titleWidth = 56;
    const int metaWidth = 14;
    const int detailWidth = titleWidth + metaWidth;

    WriteColor($"  {number,2}", accentColor);
    WriteColor(" │ ", Theme.Subtle);
    WriteColor(FitCell(title, titleWidth), Theme.Text);
    if (!string.IsNullOrWhiteSpace(meta))
    {
        WriteColor(FitCell(meta, metaWidth, alignRight: true), accentColor);
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

    WriteColor("     │ ", Theme.Subtle);

    var remainingWidth = width;
    var prefix = TruncateToDisplayWidth(detail.Prefix, remainingWidth);
    WriteColor(prefix, Theme.Muted);
    remainingWidth -= GetTextDisplayWidth(prefix);

    if (remainingWidth > 0)
    {
        var highlight = TruncateToDisplayWidth(detail.HighlightText, remainingWidth);
        WriteColor(highlight, detail.HighlightColor);
        remainingWidth -= GetTextDisplayWidth(highlight);
    }

    if (remainingWidth > 0 && !string.IsNullOrWhiteSpace(detail.Suffix))
    {
        var suffix = TruncateToDisplayWidth(detail.Suffix, remainingWidth);
        WriteColor(suffix, Theme.Muted);
        remainingWidth -= GetTextDisplayWidth(suffix);
    }

    if (remainingWidth > 0)
    {
        Console.Write(new string(' ', remainingWidth));
    }

    Console.WriteLine();
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
    var truncated = TruncateToDisplayWidth(value, width);
    var padding = width - GetTextDisplayWidth(truncated);
    if (padding <= 0)
    {
        return truncated;
    }

    return alignRight
        ? new string(' ', padding) + truncated
        : truncated + new string(' ', padding);
}

static string TruncateToDisplayWidth(string value, int maxWidth)
{
    if (maxWidth <= 0 || GetTextDisplayWidth(value) <= maxWidth)
    {
        return value;
    }

    var builder = new StringBuilder();
    var width = 0;
    var ellipsisWidth = 1;

    foreach (var rune in value.EnumerateRunes())
    {
        var runeWidth = GetRuneDisplayWidth(rune);
        if (width + runeWidth + ellipsisWidth > maxWidth)
        {
            break;
        }

        builder.Append(rune.ToString());
        width += runeWidth;
    }

    builder.Append('…');
    return builder.ToString();
}

static IReadOnlyList<string> WrapToDisplayWidth(string value, int maxWidth)
{
    if (maxWidth <= 0 || string.IsNullOrEmpty(value))
    {
        return [string.Empty];
    }

    var lines = new List<string>();
    var builder = new StringBuilder();
    var width = 0;

    foreach (var rune in value.EnumerateRunes())
    {
        var runeText = rune.ToString();
        var runeWidth = GetRuneDisplayWidth(rune);

        if (width > 0 && width + runeWidth > maxWidth)
        {
            lines.Add(builder.ToString().TrimEnd());
            builder.Clear();
            width = 0;

            if (rune.Value == ' ')
            {
                continue;
            }
        }

        builder.Append(runeText);
        width += runeWidth;
    }

    if (builder.Length > 0 || lines.Count == 0)
    {
        lines.Add(builder.ToString().TrimEnd());
    }

    return lines;
}

static int GetTextDisplayWidth(string value)
{
    var width = 0;
    foreach (var rune in value.EnumerateRunes())
    {
        width += GetRuneDisplayWidth(rune);
    }

    return width;
}

static int GetRuneDisplayWidth(Rune rune)
{
    var value = rune.Value;
    if (value == 0)
    {
        return 0;
    }

    if (value < 32 || value is >= 0x7F and < 0xA0)
    {
        return 0;
    }

    return IsWideRune(value) ? 2 : 1;
}

static bool IsWideRune(int value)
{
    return value is
        >= 0x1100 and <= 0x115F or
        >= 0x2329 and <= 0x232A or
        >= 0x2E80 and <= 0xA4CF or
        >= 0xAC00 and <= 0xD7A3 or
        >= 0xF900 and <= 0xFAFF or
        >= 0xFE10 and <= 0xFE19 or
        >= 0xFE30 and <= 0xFE6F or
        >= 0xFF00 and <= 0xFF60 or
        >= 0xFFE0 and <= 0xFFE6;
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

static ConsoleColor GetRiskColor(RiskLevel riskLevel)
{
    return riskLevel switch
    {
        RiskLevel.S0VeryLowRisk => Theme.Success,
        RiskLevel.S1LowRisk => Theme.Warning,
        RiskLevel.S2ReviewRequired => Theme.Review,
        RiskLevel.S3DoNotCleanAutomatically => Theme.Danger,
        RiskLevel.Blocked => ConsoleColor.DarkRed,
        _ => Theme.Muted
    };
}

static void WriteLineColor(string text, ConsoleColor color)
{
    WriteColor(text, color);
    Console.WriteLine();
}

static void WriteColor(string text, ConsoleColor color)
{
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

static IReadOnlySet<string> ParseRecommendedSelection(string? selection, IReadOnlyList<CleanupCandidate> candidates)
{
    if (string.IsNullOrWhiteSpace(selection) || selection.Trim() == "0")
    {
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    if (string.Equals(selection.Trim(), "A", StringComparison.OrdinalIgnoreCase))
    {
        return candidates
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
            selectedRuleIds.Add(candidates[index].RuleId);
        }
    }

    return selectedRuleIds;
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

static string FormatDeepSpaceSuggestedAction(MessageCatalog text, DeepSpaceItem item)
{
    return DeepSpaceAdviceFormatter.FormatSuggestedAction(text.Language, item);
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
        Theme.Accent);
    WriteBadgeNotice(FormatRiskBadge(RiskLevel.S2ReviewRequired), GetRiskColor(RiskLevel.S2ReviewRequired), text.Get(StringKey.DeepAnalysisActionHint));
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
        WriteLineColor($"{FormatDeepSpaceType(text, group.Key)} ({group.Count()}, {FormatBytes(group.Sum(item => item.SizeBytes))})", Theme.Text);

        foreach (var item in group)
        {
            WriteResultCard(
                displayNumber,
                item.Path,
                FormatBytes(item.SizeBytes),
                [
                    CardDetailLine.WithHighlight(
                        $"{text.Get(StringKey.DeepAnalysisType)}: {FormatDeepSpaceType(text, item.Type)}   {text.Get(StringKey.DeepAnalysisRisk)}: ",
                        FormatRiskBadge(item.RiskLevel),
                        GetRiskColor(item.RiskLevel)),
                    $"{text.Get(StringKey.DeepAnalysisLastModified)}: {FormatDate(item.LastWriteTime)}",
                    $"{text.Get(StringKey.DeepAnalysisExplanation)}: {FormatDeepSpaceExplanation(text, item)}",
                    $"{text.Get(StringKey.DeepAnalysisSuggestedAction)}: {FormatDeepSpaceSuggestedAction(text, item)}"
                ],
                GetRiskColor(item.RiskLevel));
            displayNumber++;
            Console.WriteLine();
        }

        Console.WriteLine();
    }
}

static void WriteDeepAnalysisSummary(MessageCatalog text, DeepSpaceAnalysisSummary summary, IReadOnlyList<DeepSpaceItem> items)
{
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisSummary)}:", Theme.Heading);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedRoots), summary.ScannedRootCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedDirectories), summary.ScannedDirectoryCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisScannedFiles), summary.ScannedFileCount.ToString(), Theme.Muted);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisReviewItems), summary.FindingCount.ToString(), Theme.Review);
    WriteLabelValue(text.Get(StringKey.DeepAnalysisReviewFootprint), FormatBytes(summary.FindingBytes), Theme.Review);

    if (items.Count == 0)
    {
        return;
    }

    Console.WriteLine();
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisTypeTotals)}:", Theme.Heading);
    foreach (var group in items.GroupBy(item => item.Type).OrderBy(group => GetDeepSpaceTypeOrder(group.Key)))
    {
        WriteLabelValue(FormatDeepSpaceType(text, group.Key), $"{group.Count()}   {FormatBytes(group.Sum(item => item.SizeBytes))}", Theme.Muted);
    }

    Console.WriteLine();
    WriteLineColor($"{text.Get(StringKey.DeepAnalysisTopSources)}:", Theme.Heading);
    foreach (var item in items.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(3))
    {
        WriteLabelValue(FormatDeepSpaceType(text, item.Type), $"{FormatBytes(item.SizeBytes)}   {item.Path}", GetRiskColor(item.RiskLevel));
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
    string Suffix)
{
    public bool HasHighlight => !string.IsNullOrWhiteSpace(HighlightText);

    public static CardDetailLine Text(string text)
    {
        return new CardDetailLine(text, string.Empty, Theme.Subtle, string.Empty);
    }

    public static CardDetailLine WithHighlight(string prefix, string highlightText, ConsoleColor highlightColor, string suffix = "")
    {
        return new CardDetailLine(prefix, highlightText, highlightColor, suffix);
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
