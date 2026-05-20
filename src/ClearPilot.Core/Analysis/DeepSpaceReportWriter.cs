using System.Text;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;

namespace ClearPilot.Core.Analysis;

public sealed class DeepSpaceReportWriter
{
    public const string ReportDirectoryEnvironmentVariable = "CLEARPILOT_REPORT_DIR";

    public DeepSpaceReportWriter(string reportDirectory)
    {
        ReportDirectory = reportDirectory;
    }

    public string ReportDirectory { get; }

    public static DeepSpaceReportWriter CreateDefault()
    {
        var overridePath = Environment.GetEnvironmentVariable(ReportDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return new DeepSpaceReportWriter(overridePath);
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var basePath = string.IsNullOrWhiteSpace(localAppData)
            ? AppContext.BaseDirectory
            : localAppData;

        return new DeepSpaceReportWriter(Path.Combine(basePath, "ClearPilot", "reports"));
    }

    public string Write(
        DeepSpaceAnalysisResult result,
        IReadOnlyList<string> scanRoots,
        Language language,
        DateTimeOffset generatedAt)
    {
        Directory.CreateDirectory(ReportDirectory);

        var fileName = $"deep-space-analysis-{generatedAt.UtcDateTime:yyyyMMdd-HHmmss}.md";
        var path = Path.Combine(ReportDirectory, fileName);
        File.WriteAllText(path, Render(result, scanRoots, language, generatedAt), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    public static string Render(
        DeepSpaceAnalysisResult result,
        IReadOnlyList<string> scanRoots,
        Language language,
        DateTimeOffset generatedAt)
    {
        var text = ReportText.For(language);
        var items = result.Items
            .OrderBy(item => GetTypeOrder(item.Type))
            .ThenByDescending(item => item.SizeBytes)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var builder = new StringBuilder();

        builder.AppendLine($"# {text.Title}");
        builder.AppendLine();
        builder.AppendLine($"> {text.ReviewOnlyNotice}");
        builder.AppendLine();
        builder.AppendLine($"**{text.GeneratedAt}:** {generatedAt.ToLocalTime():yyyy-MM-dd HH:mm}");
        builder.AppendLine();

        builder.AppendLine($"## {text.AtAGlance}");
        builder.AppendLine();
        builder.AppendLine($"| {text.Metric} | {text.Value} |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| {text.ScannedRoots} | {result.Summary.ScannedRootCount} |");
        builder.AppendLine($"| {text.ScannedDirectories} | {result.Summary.ScannedDirectoryCount} |");
        builder.AppendLine($"| {text.ScannedFiles} | {result.Summary.ScannedFileCount} |");
        builder.AppendLine($"| {text.ReviewItems} | {result.Summary.FindingCount} |");
        builder.AppendLine($"| {text.ReviewFootprint} | {FormatBytes(result.Summary.FindingBytes)} |");
        builder.AppendLine();

        var decisionStats = items
            .Select(item => CleanupDecisionAdvisor.ForDeepSpaceItem(item, RecommendationAdvisor.ForDeepSpaceItem(item)).Decision)
            .ToArray();
        var recommendedCount = decisionStats.Count(decision => decision == CleanupDecision.RecommendedToClean);
        var notRecommendedCount = decisionStats.Count(decision => decision == CleanupDecision.NotRecommendedToClean);
        var analysisOnlyCount = decisionStats.Count(decision => decision == CleanupDecision.AnalysisOnlyDoNotClean);
        var blockedCount = decisionStats.Count(decision => decision == CleanupDecision.Blocked);
        var intentionallyUntouchedCount = items.Length;

        builder.AppendLine($"## {text.ExecutionStatus}");
        builder.AppendLine();
        builder.AppendLine($"| {text.Status} | {text.Count} |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| {text.Cleaned} | 0 |");
        builder.AppendLine($"| {text.Skipped} | 0 |");
        builder.AppendLine($"| {text.Failed} | 0 |");
        builder.AppendLine($"| {text.IntentionallyUntouched} | {intentionallyUntouchedCount} |");
        builder.AppendLine();

        builder.AppendLine($"## {text.DecisionBreakdown}");
        builder.AppendLine();
        builder.AppendLine($"| {text.Decision} | {text.Count} |");
        builder.AppendLine("| --- | ---: |");
        builder.AppendLine($"| {text.RecommendedToClean} | {recommendedCount} |");
        builder.AppendLine($"| {text.NotRecommendedToClean} | {notRecommendedCount} |");
        builder.AppendLine($"| {text.AnalysisOnlyDoNotClean} | {analysisOnlyCount} |");
        builder.AppendLine($"| {text.Blocked} | {blockedCount} |");
        builder.AppendLine($"| {text.IntentionallyUntouched} | {intentionallyUntouchedCount} |");
        builder.AppendLine();

        builder.AppendLine($"## {text.ScanScope}");
        builder.AppendLine();
        foreach (var root in scanRoots)
        {
            builder.AppendLine($"- `{root}`");
        }

        builder.AppendLine();
        builder.AppendLine($"## {text.TypeBreakdown}");
        builder.AppendLine();
        builder.AppendLine($"| {text.Type} | {text.Count} | {text.Size} | {text.Share} |");
        builder.AppendLine("| --- | ---: | ---: | --- |");
        foreach (var group in items.GroupBy(item => item.Type).OrderBy(group => GetTypeOrder(group.Key)))
        {
            var size = group.Sum(item => item.SizeBytes);
            builder.AppendLine($"| {FormatType(language, group.Key)} | {group.Count()} | {FormatBytes(size)} | {FormatBar(size, result.Summary.FindingBytes)} |");
        }

        builder.AppendLine();
        builder.AppendLine($"## {text.TopSources}");
        builder.AppendLine();
        builder.AppendLine($"| {text.Rank} | {text.Type} | {text.Size} | {text.Location} |");
        builder.AppendLine("| ---: | --- | ---: | --- |");
        var rank = 1;
        foreach (var item in items.OrderByDescending(item => item.SizeBytes).ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase).Take(5))
        {
            builder.AppendLine($"| {rank} | {FormatType(language, item.Type)} | {FormatBytes(item.SizeBytes)} | `{EscapePipes(item.Path)}` |");
            rank++;
        }

        builder.AppendLine();
        builder.AppendLine($"## {text.Findings}");

        foreach (var group in items.GroupBy(item => item.Type).OrderBy(group => GetTypeOrder(group.Key)))
        {
            builder.AppendLine();
            builder.AppendLine($"### {FormatType(language, group.Key)}");
            builder.AppendLine();

            foreach (var item in group)
            {
                var advice = RecommendationAdvisor.ForDeepSpaceItem(item);
                var decision = CleanupDecisionAdvisor.ForDeepSpaceItem(item, advice);
                builder.AppendLine($"#### {EscapeMarkdown(Path.GetFileName(item.Path).Length == 0 ? item.Path : Path.GetFileName(item.Path))}");
                builder.AppendLine();
                builder.AppendLine($"`{item.Path}`");
                builder.AppendLine();
                builder.AppendLine($"- **{text.Size}:** {FormatBytes(item.SizeBytes)}");
                builder.AppendLine($"- **{text.Decision}:** `{FormatDecision(language, decision.Decision)}`");
                builder.AppendLine($"- **{text.Reason}:** {FormatDecisionReason(language, decision)}");
                builder.AppendLine($"- **{text.PossibleImpactIfCleaned}:** {DeepSpaceAdviceFormatter.FormatPossibleImpact(language, item, advice.PossibleImpact)}");
                builder.AppendLine($"- **{text.ExpectedReclaim}:** {FormatExpectedReclaim(language, item, decision)}");
                builder.AppendLine($"- **{text.Risk}:** `{FormatRisk(item.RiskLevel)}`");
                builder.AppendLine($"- **{text.LastModified}:** {FormatDate(item.LastWriteTime)}");
                builder.AppendLine($"- **{text.Explanation}:** {DeepSpaceAdviceFormatter.FormatExplanation(language, item)}");
                builder.AppendLine($"- **{text.SafetyNote}:** {DeepSpaceAdviceFormatter.FormatSafetyNote(language, item, advice.SafetyNote)}");
                builder.AppendLine();
            }
        }

        builder.AppendLine("---");
        builder.AppendLine();
        builder.AppendLine(text.Footer);
        return builder.ToString();
    }

    private static string FormatBar(long value, long total)
    {
        if (total <= 0 || value <= 0)
        {
            return "`[..........]` 0%";
        }

        var ratio = Math.Clamp((double)value / total, 0, 1);
        var filled = Math.Clamp((int)Math.Round(ratio * 10), 1, 10);
        var empty = 10 - filled;
        return $"`[{new string('#', filled)}{new string('.', empty)}]` {ratio:P0}";
    }

    private static string FormatType(Language language, DeepSpaceItemType type)
    {
        if (language == Language.SimplifiedChinese)
        {
            return type switch
            {
                DeepSpaceItemType.LargeFile => "大文件",
                DeepSpaceItemType.LargeFolder => "大目录",
                DeepSpaceItemType.OldArchiveOrInstaller => "旧压缩包或安装包",
                DeepSpaceItemType.ProjectDependencyFolder => "项目依赖目录",
                DeepSpaceItemType.FileTypeSummary => "文件类型统计",
                DeepSpaceItemType.SystemManagedWindowsArea => "Windows 系统管理区域",
                DeepSpaceItemType.GameLauncherReviewArea => "游戏启动器复核区域",
                _ => type.ToString()
            };
        }

        return type switch
        {
            DeepSpaceItemType.LargeFile => "Large file",
            DeepSpaceItemType.LargeFolder => "Large folder",
            DeepSpaceItemType.OldArchiveOrInstaller => "Old archive or installer",
            DeepSpaceItemType.ProjectDependencyFolder => "Project dependency folder",
            DeepSpaceItemType.FileTypeSummary => "File type summary",
            DeepSpaceItemType.SystemManagedWindowsArea => "Windows system-managed area",
            DeepSpaceItemType.GameLauncherReviewArea => "Game launcher review-only area",
            _ => type.ToString()
        };
    }

    private static int GetTypeOrder(DeepSpaceItemType type)
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

    private static string FormatRisk(RiskLevel riskLevel)
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

    private static string FormatDecision(Language language, CleanupDecision decision)
    {
        if (language == Language.SimplifiedChinese)
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

    private static string FormatDecisionReason(Language language, CleanupDecisionResult decision)
    {
        if (language != Language.SimplifiedChinese)
        {
            return decision.DecisionReason;
        }

        return decision.Decision switch
        {
            CleanupDecision.AnalysisOnlyDoNotClean => "这是复核项，仅分析不清理。",
            CleanupDecision.NotRecommendedToClean => "不建议直接清理，请先评估影响。",
            CleanupDecision.Blocked => "受安全策略阻止。ClearPilot 在任何模式下都不会清理该目标。",
            CleanupDecision.RecommendedToClean => "建议清理。",
            _ => decision.DecisionReason
        };
    }

    private static string FormatExpectedReclaim(Language language, DeepSpaceItem item, CleanupDecisionResult decision)
    {
        if (decision.Decision == CleanupDecision.Blocked)
        {
            return language == Language.SimplifiedChinese
                ? "不适用（已阻止，保持不变）。"
                : "Not applicable (blocked; left unchanged).";
        }

        return language == Language.SimplifiedChinese
            ? $"仅供估算：{FormatBytes(item.SizeBytes)}（Deep Space 仅分析，不执行删除）"
            : $"Estimate only: {FormatBytes(item.SizeBytes)} (Deep Space is analysis-only and does not delete files)";
    }

    private static string FormatDate(DateTimeOffset? value)
    {
        return value is null
            ? "n/a"
            : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    }

    private static string FormatBytes(long bytes)
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

    private static string EscapePipes(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal);
    }

    private static string EscapeMarkdown(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("*", "\\*", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    private sealed record ReportText(
        string Title,
        string ReviewOnlyNotice,
        string GeneratedAt,
        string AtAGlance,
        string Metric,
        string Value,
        string ScannedRoots,
        string ScannedDirectories,
        string ScannedFiles,
        string ReviewItems,
        string ReviewFootprint,
        string ScanScope,
        string TypeBreakdown,
        string TopSources,
        string Findings,
        string ExecutionStatus,
        string Status,
        string Cleaned,
        string Skipped,
        string Failed,
        string IntentionallyUntouched,
        string DecisionBreakdown,
        string RecommendedToClean,
        string NotRecommendedToClean,
        string AnalysisOnlyDoNotClean,
        string Blocked,
        string Count,
        string Share,
        string Rank,
        string Type,
        string Location,
        string Size,
        string Decision,
        string Reason,
        string PossibleImpactIfCleaned,
        string ExpectedReclaim,
        string Risk,
        string LastModified,
        string Explanation,
        string SafetyNote,
        string Footer)
    {
        public static ReportText For(Language language)
        {
            return language == Language.SimplifiedChinese
                ? Chinese
                : English;
        }

        private static ReportText English { get; } = new(
            "ClearPilot Deep Space Analysis Report",
            "Analysis only. ClearPilot does not delete files in Deep Space. Downloads is scanned only for storage understanding, and personal libraries (Desktop/Documents/Pictures/Videos/Music) are not scanned by default.",
            "Generated at",
            "At a Glance",
            "Metric",
            "Value",
            "Scanned roots",
            "Scanned directories",
            "Scanned files",
            "Review items",
            "Review footprint",
            "Scan Scope",
            "Type Breakdown",
            "Top Space Sources",
            "Findings",
            "Execution Status",
            "Status",
            "Cleaned",
            "Skipped",
            "Failed",
            "Intentionally untouched",
            "Decision Breakdown",
            "Recommended to clean",
            "Not recommended to clean",
            "Analysis only, do not clean",
            "Blocked",
            "Count",
            "Share",
            "Rank",
            "Type",
            "Location",
            "Size",
            "Decision",
            "Reason",
            "Possible impact if cleaned",
            "Expected reclaim",
            "Risk",
            "Last modified",
            "Explanation",
            "Safety note",
            "ClearPilot reports review candidates only. Open locations and decide manually before changing files.");

        private static ReportText Chinese { get; } = new(
            "ClearPilot 深度空间分析报告",
            "仅分析：Deep Space 不会删除文件。Downloads 仅用于存储占用了解；Desktop/Documents/Pictures/Videos/Music 默认不扫描。",
            "生成时间",
            "概览",
            "指标",
            "数值",
            "扫描根目录",
            "扫描目录",
            "扫描文件",
            "复核项",
            "复核占用",
            "扫描范围",
            "类型分布",
            "主要空间来源",
            "结果详情",
            "执行状态",
            "状态",
            "已清理",
            "已跳过",
            "失败",
            "有意保持不变",
            "结论分布",
            "建议清理",
            "不建议清理",
            "仅分析，不清理",
            "已阻止",
            "数量",
            "占比",
            "排名",
            "类型",
            "位置",
            "大小",
            "结论",
            "原因",
            "清理后的可能影响",
            "预计可释放",
            "风险",
            "最后修改",
            "说明",
            "安全说明",
            "ClearPilot 只报告需要复核的候选项，请手动确认后再处理。");
    }
}
