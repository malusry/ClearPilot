using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Logging;
using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;
using ClearPilot.Core.Scanning;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class CleanupRiskGateTests
{
    [Fact]
    public void QuickSafeCleanExecutesOnlyS0AndSkipsS1S2S3AndBlocked()
    {
        using var workspace = TestWorkspace.Create();
        var s0Root = workspace.CreateDirectory("s0");
        var s1Root = workspace.CreateDirectory("s1");
        var s2Root = workspace.CreateDirectory("s2");
        var s3Root = workspace.CreateDirectory("s3");
        var blockedRoot = workspace.CreateDirectory("blocked");
        var s0File = workspace.CreateOldFile(Path.Combine("s0", "s0.tmp"), "0");
        var s1File = workspace.CreateOldFile(Path.Combine("s1", "s1.tmp"), "1");
        var s2File = workspace.CreateOldFile(Path.Combine("s2", "s2.tmp"), "2");
        var s3File = workspace.CreateOldFile(Path.Combine("s3", "s3.tmp"), "3");
        var blockedFile = workspace.CreateOldFile(Path.Combine("blocked", "blocked.tmp"), "4");
        var quick = CreateQuickCleaner(workspace.LogsPath);

        var result = quick.Run(
            [
                CreateRule("r.s0", RiskLevel.S0VeryLowRisk, s0Root),
                CreateRule("r.s1", RiskLevel.S1LowRisk, s1Root),
                CreateRule("r.s2", RiskLevel.S2ReviewRequired, s2Root),
                CreateRule("r.s3", RiskLevel.S3DoNotCleanAutomatically, s3Root),
                CreateRule("r.blocked", RiskLevel.Blocked, blockedRoot)
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.False(File.Exists(s0File));
        Assert.True(File.Exists(s1File));
        Assert.True(File.Exists(s2File));
        Assert.True(File.Exists(s3File));
        Assert.True(File.Exists(blockedFile));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(4, result.SkippedCount);
    }

    [Fact]
    public void RecommendedCleanupRequiresExplicitConfirmation()
    {
        using var workspace = TestWorkspace.Create();
        var root = workspace.CreateDirectory("s1");
        var file = workspace.CreateOldFile(Path.Combine("s1", "cache.tmp"), "123");
        var service = CreateRecommendedService(workspace.LogsPath);

        var result = service.Clean(
            [CreateRule("r.s1", RiskLevel.S1LowRisk, root)],
            confirmedByUser: false,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(file));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void RecommendedCleanupExecutesOnlyS1AndSkipsS2S3AndBlocked()
    {
        using var workspace = TestWorkspace.Create();
        var s1Root = workspace.CreateDirectory("s1");
        var s2Root = workspace.CreateDirectory("s2");
        var s3Root = workspace.CreateDirectory("s3");
        var blockedRoot = workspace.CreateDirectory("blocked");
        var s1File = workspace.CreateOldFile(Path.Combine("s1", "s1.tmp"), "1");
        var s2File = workspace.CreateOldFile(Path.Combine("s2", "s2.tmp"), "2");
        var s3File = workspace.CreateOldFile(Path.Combine("s3", "s3.tmp"), "3");
        var blockedFile = workspace.CreateOldFile(Path.Combine("blocked", "blocked.tmp"), "4");
        var service = CreateRecommendedService(workspace.LogsPath);

        var result = service.Clean(
            [
                CreateRule("r.s1", RiskLevel.S1LowRisk, s1Root),
                CreateRule("r.s2", RiskLevel.S2ReviewRequired, s2Root),
                CreateRule("r.s3", RiskLevel.S3DoNotCleanAutomatically, s3Root),
                CreateRule("r.blocked", RiskLevel.Blocked, blockedRoot)
            ],
            confirmedByUser: true,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.False(File.Exists(s1File));
        Assert.True(File.Exists(s2File));
        Assert.True(File.Exists(s3File));
        Assert.True(File.Exists(blockedFile));
        Assert.Equal(1, result.DeletedCount);
        Assert.Equal(3, result.SkippedCount);
    }

    [Fact]
    public void S3AndBlockedAreNeverDeletedInAnyCleanupMode()
    {
        using var workspace = TestWorkspace.Create();
        var s3Root = workspace.CreateDirectory("s3");
        var blockedRoot = workspace.CreateDirectory("blocked");
        var s3File = workspace.CreateOldFile(Path.Combine("s3", "s3.tmp"), "3");
        var blockedFile = workspace.CreateOldFile(Path.Combine("blocked", "blocked.tmp"), "4");
        var quick = CreateQuickCleaner(workspace.LogsPath);
        var recommended = CreateRecommendedService(workspace.LogsPath);

        var quickResult = quick.Run(
            [
                CreateRule("r.s3", RiskLevel.S3DoNotCleanAutomatically, s3Root),
                CreateRule("r.blocked", RiskLevel.Blocked, blockedRoot)
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        var recommendedResult = recommended.Clean(
            [
                CreateRule("r.s3", RiskLevel.S3DoNotCleanAutomatically, s3Root),
                CreateRule("r.blocked", RiskLevel.Blocked, blockedRoot)
            ],
            confirmedByUser: true,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(s3File));
        Assert.True(File.Exists(blockedFile));
        Assert.Equal(0, quickResult.DeletedCount);
        Assert.Equal(0, recommendedResult.DeletedCount);
    }

    [Fact]
    public void DeepSpaceAnalysisNeverDeletesFiles()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateOldFile(Path.Combine("scan", "large.bin"), new string('A', 2048));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [workspace.Root],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 4096,
            FileTypeSummaryThresholdBytes = 4096,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 5,
            MaxResults = 20
        };

        var result = analyzer.Analyze(options, DateTimeOffset.UtcNow);

        Assert.NotEmpty(result);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void QuickSafeCleanSkipsGameLauncherS1Rules()
    {
        using var workspace = TestWorkspace.Create();
        var steamRoot = workspace.CreateDirectory("steam-httpcache");
        var steamFile = workspace.CreateOldFile(Path.Combine("steam-httpcache", "cache.tmp"), "launcher-cache");
        var quick = CreateQuickCleaner(workspace.LogsPath);

        var result = quick.Run(
            [
                new CleanupRule(
                    "cp.s1.steam-httpcache",
                    "Steam launcher HTTP cache",
                    RiskLevel.S1LowRisk,
                    [steamRoot],
                    ["*.tmp"],
                    [],
                    TimeSpan.FromDays(1),
                    "Steam launcher cache",
                    LauncherName: "Steam",
                    ProcessGuardNames: ["steam", "steamwebhelper"])
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(steamFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void RecommendationLabelCannotMakeS2OrBlockedTargetsDeletable()
    {
        using var workspace = TestWorkspace.Create();
        var s2Root = workspace.CreateDirectory("s2");
        var blockedRoot = workspace.CreateDirectory("blocked");
        var s2File = workspace.CreateOldFile(Path.Combine("s2", "s2.tmp"), "2");
        var blockedFile = workspace.CreateOldFile(Path.Combine("blocked", "blocked.tmp"), "4");
        var recommended = CreateRecommendedService(workspace.LogsPath);

        var result = recommended.Clean(
            [
                CreateRule("cp.s0.user-temp", RiskLevel.S2ReviewRequired, s2Root),
                CreateRule("cp.s0.user-temp", RiskLevel.Blocked, blockedRoot)
            ],
            confirmedByUser: true,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(s2File));
        Assert.True(File.Exists(blockedFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(2, result.SkippedCount);
    }

    [Fact]
    public void QuickSafeClean_DoesNotIncludeAppProfileS1Targets()
    {
        using var workspace = TestWorkspace.Create();
        var appRoot = workspace.CreateDirectory("discord-cache");
        var appFile = workspace.CreateOldFile(Path.Combine("discord-cache", "cache.tmp"), "123");
        var quick = CreateQuickCleaner(workspace.LogsPath);

        var result = quick.Run(
            [
                new CleanupRule(
                    "cp.s1.electron-app-ui-cache",
                    "Electron app UI caches",
                    RiskLevel.S1LowRisk,
                    [appRoot],
                    ["*.tmp"],
                    [],
                    TimeSpan.FromDays(1),
                    "App profile cache.",
                    ProcessGuardNames: ["Discord.exe", "Slack.exe", "Teams.exe"])
            ],
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(appFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void AppProfilesV1_QuickSafeDoesNotIncludeAppProfiles()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var quickRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk).ToArray();
        Assert.NotEmpty(quickRules);
        Assert.DoesNotContain(
            quickRules.SelectMany(rule => rule.RootPaths),
            root =>
                root.Contains(Path.Combine("Discord"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Slack"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Microsoft", "Teams"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("MSTeams"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Code"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("VSCodium"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("JetBrains"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecommendedCleanup_AppProfileS1_RequiresConfirmation()
    {
        using var workspace = TestWorkspace.Create();
        var appRoot = workspace.CreateDirectory("vscode-cache");
        var appFile = workspace.CreateOldFile(Path.Combine("vscode-cache", "cache.tmp"), "123");
        var service = CreateRecommendedService(workspace.LogsPath);

        var result = service.Clean(
            [
                new CleanupRule(
                    "cp.s1.vscode-cache",
                    "Visual Studio Code cache",
                    RiskLevel.S1LowRisk,
                    [appRoot],
                    ["*.tmp"],
                    [],
                    TimeSpan.FromDays(1),
                    "App profile cache.",
                    ProcessGuardNames: ["Code.exe", "Code - Insiders.exe", "VSCodium.exe"])
            ],
            confirmedByUser: false,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(appFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void QuickSafeClean_DoesNotIncludeDownloads()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var quickRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk).ToArray();

        Assert.NotEmpty(quickRules);
        Assert.DoesNotContain(
            quickRules.SelectMany(rule => rule.RootPaths),
            root => root.Contains(Path.Combine("Users", "tester", "Downloads"), StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("Downloads", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RecommendedCleanup_DoesNotIncludeDownloads()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var recommendedRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S1LowRisk).ToArray();

        Assert.NotEmpty(recommendedRules);
        Assert.DoesNotContain(
            recommendedRules.SelectMany(rule => rule.RootPaths),
            root => root.Contains(Path.Combine("Users", "tester", "Downloads"), StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("Downloads", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZoomProfile_NotInQuickSafeOrRecommendedCleanup()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var quickRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk).ToArray();
        var recommendedRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S1LowRisk).ToArray();

        Assert.NotEmpty(quickRules);
        Assert.NotEmpty(recommendedRules);
        Assert.DoesNotContain(
            quickRules.SelectMany(rule => rule.RootPaths),
            root => root.Contains(Path.Combine("Zoom"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            recommendedRules.SelectMany(rule => rule.RootPaths),
            root => root.Contains(Path.Combine("Zoom"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackageManagers_QuickSafeDoesNotIncludePackageCaches()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var quickRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk).ToArray();
        Assert.NotEmpty(quickRules);
        Assert.DoesNotContain(
            quickRules.SelectMany(rule => rule.RootPaths),
            root =>
                root.Contains("npm", StringComparison.OrdinalIgnoreCase)
                || root.Contains("pnpm", StringComparison.OrdinalIgnoreCase)
                || root.Contains("yarn", StringComparison.OrdinalIgnoreCase)
                || root.Contains("nuget", StringComparison.OrdinalIgnoreCase)
                || root.Contains("pip", StringComparison.OrdinalIgnoreCase)
                || root.Contains("cargo", StringComparison.OrdinalIgnoreCase)
                || root.Contains(".gradle", StringComparison.OrdinalIgnoreCase)
                || root.Contains(".m2", StringComparison.OrdinalIgnoreCase)
                || root.Contains("deno", StringComparison.OrdinalIgnoreCase)
                || root.Contains(".bun", StringComparison.OrdinalIgnoreCase)
                || root.Contains("composer", StringComparison.OrdinalIgnoreCase)
                || root.Contains("go-build", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackageManagers_RecommendedCleanupRequiresConfirmation()
    {
        using var workspace = TestWorkspace.Create();
        var npmCacheRoot = workspace.CreateDirectory(Path.Combine("LocalAppData", "npm-cache"));
        var npmCacheFile = workspace.CreateOldFile(Path.Combine("LocalAppData", "npm-cache", "entry.bin"), "cache-data");
        var service = CreateRecommendedService(workspace.LogsPath);
        var rule = new CleanupRule(
            "cp.s1.npm-cache",
            "npm cache",
            RiskLevel.S1LowRisk,
            [npmCacheRoot],
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "npm cache");

        var result = service.Clean(
            [rule],
            confirmedByUser: false,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(npmCacheFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    [Fact]
    public void WindowsDiagnostics_QuickSafeDoesNotIncludeUserDiagnostics()
    {
        var rules = RuleCatalog.CreateDefault(new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)"));

        var quickRules = rules.Where(rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk).ToArray();
        Assert.NotEmpty(quickRules);
        Assert.DoesNotContain(
            quickRules.SelectMany(rule => rule.RootPaths),
            root =>
                root.Contains(Path.Combine("CrashDumps"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "WER"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("ReportArchive"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("ReportQueue"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WindowsDiagnostics_RecommendedCleanupRequiresConfirmation()
    {
        using var workspace = TestWorkspace.Create();
        var crashDumpRoot = workspace.CreateDirectory(Path.Combine("LocalAppData", "CrashDumps"));
        var crashDumpFile = workspace.CreateOldFile(Path.Combine("LocalAppData", "CrashDumps", "old.dmp"), "123");
        var service = CreateRecommendedService(workspace.LogsPath);
        var rule = new CleanupRule(
            "cp.s1.user-crash-dumps",
            "Current user crash dumps",
            RiskLevel.S1LowRisk,
            [crashDumpRoot],
            ["*.dmp", "*.mdmp"],
            [],
            TimeSpan.FromDays(14),
            "Old crash dump files.");

        var result = service.Clean(
            [rule],
            confirmedByUser: false,
            dryRun: false,
            now: DateTimeOffset.UtcNow);

        Assert.True(File.Exists(crashDumpFile));
        Assert.Equal(0, result.DeletedCount);
        Assert.Equal(1, result.SkippedCount);
    }

    private static QuickSafeCleaner CreateQuickCleaner(string logPath)
    {
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        var pathSafetyEngine = new PathSafetyEngine(protectedPathPolicy);
        return new QuickSafeCleaner(
            new CleanupFileScanner(protectedPathPolicy),
            new CleanupLogStore(logPath),
            pathSafetyEngine);
    }

    private static RecommendedCleanupService CreateRecommendedService(string logPath)
    {
        var protectedPathPolicy = new ProtectedPathPolicy([]);
        var scanner = new CleanupScanner(protectedPathPolicy);
        var fileScanner = new CleanupFileScanner(protectedPathPolicy);
        var executor = new CleanupExecutor(fileScanner, new CleanupLogStore(logPath), new PathSafetyEngine(protectedPathPolicy));
        return new RecommendedCleanupService(scanner, executor);
    }

    private static CleanupRule CreateRule(string ruleId, RiskLevel riskLevel, string root)
    {
        return new CleanupRule(
            ruleId,
            "Test files",
            riskLevel,
            [root],
            ["*.tmp", "*.bin"],
            [],
            TimeSpan.FromDays(1),
            "Test rule.");
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
            LogsPath = Path.Combine(root, "logs");
        }

        public string Root { get; }

        public string LogsPath { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateOldFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2));
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
