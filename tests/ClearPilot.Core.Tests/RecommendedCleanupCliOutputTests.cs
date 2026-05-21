using System.Diagnostics;
using System.Text;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class RecommendedCleanupCliOutputTests
{
    [Fact]
    public void RecommendedCleanup_Output_UsesConclusionFirstFields()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateRecommendedAndNotRecommendedFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.English, "2\n0\n\n0\n");

        Assert.Contains("Decision:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reason:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Possible impact if cleaned:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Expected reclaim:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Risk:", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecommendedCleanup_PrimaryOutput_DoesNotUseLegacyActionHelpers()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateRecommendedAndNotRecommendedFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.English, "2\n0\n\n0\n");

        Assert.DoesNotContain("Recommended action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suggested action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\u5EFA\u8BAE\u64CD\u4F5C", output, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendedCleanup_ZhCn_UsesConclusionFirstFields()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateRecommendedAndNotRecommendedFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.SimplifiedChinese, "2\n0\n\n0\n");
        var focused = ExtractRecommendedPrimaryLines(output);

        Assert.Contains("\u7ED3\u8BBA:", focused, StringComparison.Ordinal);
        Assert.Contains("\u539F\u56E0:", focused, StringComparison.Ordinal);
        Assert.Contains("\u6E05\u7406\u540E\u7684\u53EF\u80FD\u5F71\u54CD:", focused, StringComparison.Ordinal);
        Assert.Contains("\u9884\u8BA1\u53EF\u91CA\u653E:", focused, StringComparison.Ordinal);
        Assert.Contains("\u98CE\u9669:", focused, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendedCleanup_ZhCn_NoMojibake()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateRecommendedAndNotRecommendedFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.SimplifiedChinese, "2\n0\n\n0\n");
        var focused = ExtractRecommendedPrimaryLines(output);

        Assert.DoesNotContain("\uFFFD", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("娣", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("绌", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("鍒", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("銆", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("涓", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("璺", focused, StringComparison.Ordinal);
        Assert.DoesNotContain("緞", focused, StringComparison.Ordinal);
    }

    [Fact]
    public void RecommendedCleanup_ZhCn_AppProfileLogs_DoesNotFallbackToGenericAdvice()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateAppProfileLogAndCrashFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.SimplifiedChinese, "2\n0\n\n0\n");

        Assert.Contains("\u65E7\u5E94\u7528\u65E5\u5FD7\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406", output, StringComparison.Ordinal);
        Assert.Contains("\u5386\u53F2\u95EE\u9898\u6392\u67E5\u65E5\u5FD7", output, StringComparison.Ordinal);
        Assert.DoesNotContain("cp.s1.electron-app-logs", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecommendedCleanup_ZhCn_AppProfileCrashDiagnostics_DoesNotFallbackToGenericAdvice()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        workspace.CreateAppProfileLogAndCrashFixtures();

        var output = workspace.RunRecommendedCleanupCli(Language.SimplifiedChinese, "2\n0\n\n0\n");

        Assert.Contains("\u65E7\u7684\u5DF2\u5B8C\u6210\u5D29\u6E83\u8BCA\u65AD\u53EF\u5728\u5E94\u7528\u5173\u95ED\u540E\u6E05\u7406", output, StringComparison.Ordinal);
        Assert.Contains("\u5386\u53F2\u5D29\u6E83\u6392\u67E5\u6570\u636E", output, StringComparison.Ordinal);
        Assert.DoesNotContain("cp.s1.electron-app-crash-reports", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecommendedCleanup_AllSelection_OnlyEligibleRecommendedS1()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        var (recommendedFile, notRecommendedFile, _) = workspace.CreateRecommendedAndNotRecommendedFixtures();

        _ = workspace.RunRecommendedCleanupCli(Language.English, "2\nA\nY\n\n0\n");

        Assert.False(File.Exists(recommendedFile));
        Assert.True(File.Exists(notRecommendedFile));
    }

    [Fact]
    public void RecommendedCleanup_AllSelection_ExcludesProcessGuardBlockedItems()
    {
        using var workspace = RecommendedCliTestWorkspace.Create();
        var (unblockedRecommendedFile, _, processGuardBlockedRecommendedFile) = workspace.CreateRecommendedAndNotRecommendedFixtures();
        using var fakeSteam = workspace.StartFakeSteamProcess();

        _ = workspace.RunRecommendedCleanupCli(Language.English, "2\nA\nY\n\n0\n");

        Assert.False(File.Exists(unblockedRecommendedFile));
        Assert.True(File.Exists(processGuardBlockedRecommendedFile));
    }

    [Fact]
    public void RecommendedCleanup_IneligibleFindings_NotSelectable()
    {
        Assert.False(ClearPilot.Cli.ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S2ReviewRequired,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ClearPilot.Cli.ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S3DoNotCleanAutomatically,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ClearPilot.Cli.ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.Blocked,
            CleanupDecision.RecommendedToClean,
            processGuardBlocked: false));

        Assert.False(ClearPilot.Cli.ConsolePresentationStyle.IsBulkSelectableRecommendedItem(
            RiskLevel.S1LowRisk,
            CleanupDecision.NotRecommendedToClean,
            processGuardBlocked: false));
    }

    private static string ExtractRecommendedPrimaryLines(string output)
    {
        var builder = new StringBuilder();
        var lines = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.Contains("Decision:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Reason:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Possible impact if cleaned:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Expected reclaim:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Risk:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Safety note:", StringComparison.OrdinalIgnoreCase)
                || line.Contains("\u7ED3\u8BBA:", StringComparison.Ordinal)
                || line.Contains("\u539F\u56E0:", StringComparison.Ordinal)
                || line.Contains("\u6E05\u7406\u540E\u7684\u53EF\u80FD\u5F71\u54CD:", StringComparison.Ordinal)
                || line.Contains("\u9884\u8BA1\u53EF\u91CA\u653E:", StringComparison.Ordinal)
                || line.Contains("\u98CE\u9669:", StringComparison.Ordinal)
                || line.Contains("\u5B89\u5168\u8BF4\u660E:", StringComparison.Ordinal))
            {
                builder.AppendLine(line);
            }
        }

        return builder.ToString();
    }

    private sealed class RecommendedCliTestWorkspace : IDisposable
    {
        private const int CliTimeoutMs = 30000;

        private RecommendedCliTestWorkspace(string root)
        {
            Root = root;
            LocalAppDataRoot = Path.Combine(root, "LocalAppData");
            UserProfileRoot = Path.Combine(root, "UserProfile");
            TempRoot = Path.Combine(root, "Temp");
            SettingsPath = Path.Combine(root, "settings", "settings.json");
            LogDirectory = Path.Combine(root, "logs");

            Directory.CreateDirectory(LocalAppDataRoot);
            Directory.CreateDirectory(UserProfileRoot);
            Directory.CreateDirectory(TempRoot);
            Directory.CreateDirectory(LogDirectory);
        }

        public string Root { get; }

        public string LocalAppDataRoot { get; }

        public string UserProfileRoot { get; }

        public string TempRoot { get; }

        public string SettingsPath { get; }

        public string LogDirectory { get; }

        public static RecommendedCliTestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Recommended.Cli.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new RecommendedCliTestWorkspace(root);
        }

        public (string recommendedFile, string notRecommendedFile, string processGuardBlockedRecommendedFile) CreateRecommendedAndNotRecommendedFixtures()
        {
            var recommendedFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "Microsoft", "Windows", "INetCache", "cache.bin"),
                sizeBytes: 4096,
                lastWriteUtc: DateTime.UtcNow.AddDays(-2));

            var notRecommendedFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "CrashDumps", "app.dmp"),
                sizeBytes: 1024,
                lastWriteUtc: DateTime.UtcNow.AddDays(-10));

            var processGuardBlockedRecommendedFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "Steam", "appcache", "httpcache", "steam-cache.bin"),
                sizeBytes: 4096,
                lastWriteUtc: DateTime.UtcNow.AddDays(-2));

            return (recommendedFile, notRecommendedFile, processGuardBlockedRecommendedFile);
        }

        public (string appLogFile, string crashReportFile, string crashCompletedFile) CreateAppProfileLogAndCrashFixtures()
        {
            var appLogFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "Discord", "logs", "old.log"),
                sizeBytes: 4096,
                lastWriteUtc: DateTime.UtcNow.AddDays(-10));

            var crashReportFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "Discord", "Crashpad", "reports", "old.dmp"),
                sizeBytes: 4096,
                lastWriteUtc: DateTime.UtcNow.AddDays(-10));

            var crashCompletedFile = CreateFile(
                Path.Combine(LocalAppDataRoot, "Discord", "Crashpad", "completed", "old.mdmp"),
                sizeBytes: 4096,
                lastWriteUtc: DateTime.UtcNow.AddDays(-10));

            return (appLogFile, crashReportFile, crashCompletedFile);
        }

        public ProcessGuardHandle StartFakeSteamProcess()
        {
            var fakeSteamPath = Path.Combine(Root, "steam.exe");
            var sourceCmdPath = Environment.GetEnvironmentVariable("ComSpec");
            Assert.False(string.IsNullOrWhiteSpace(sourceCmdPath));
            Assert.True(File.Exists(sourceCmdPath), $"ComSpec not found: {sourceCmdPath}");

            File.Copy(sourceCmdPath!, fakeSteamPath, overwrite: true);

            var startInfo = new ProcessStartInfo
            {
                FileName = fakeSteamPath,
                Arguments = "/c ping -n 60 127.0.0.1 > nul",
                WorkingDirectory = Root,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(startInfo);
            Assert.NotNull(process);

            Thread.Sleep(300);
            return new ProcessGuardHandle(process!);
        }

        public string RunRecommendedCleanupCli(Language language, string scriptedInput)
        {
            var store = new SettingsStore(SettingsPath);
            store.Save(new AppSettings
            {
                Language = language,
                LogRetentionDays = AppSettings.DefaultLogRetentionDays,
                AutoEmptyRecycleBin = false,
                DryRun = false
            });

            var repoRoot = FindRepoRoot();
            var dotnetPath = Path.Combine(repoRoot, ".dotnet", "dotnet.exe");
            var cliDllPath = Path.Combine(repoRoot, "src", "ClearPilot.Cli", "bin", "Debug", "net10.0", "ClearPilot.dll");
            Assert.True(File.Exists(cliDllPath), $"CLI binary not found: {cliDllPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(dotnetPath) ? dotnetPath : "dotnet",
                Arguments = $"\"{cliDllPath}\"",
                WorkingDirectory = repoRoot,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.Environment["CLEARPILOT_SETTINGS_PATH"] = SettingsPath;
            startInfo.Environment["CLEARPILOT_LOG_DIR"] = LogDirectory;
            startInfo.Environment["LOCALAPPDATA"] = LocalAppDataRoot;
            startInfo.Environment["USERPROFILE"] = UserProfileRoot;
            startInfo.Environment["TEMP"] = TempRoot;
            startInfo.Environment["TMP"] = TempRoot;
            startInfo.Environment["NO_COLOR"] = "1";

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            process!.StandardInput.Write(scriptedInput);
            process.StandardInput.Close();

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            var exited = process.WaitForExit(CliTimeoutMs);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
            }

            Assert.True(exited, "CLI process timed out.");
            Assert.Equal(0, process.ExitCode);
            return stdout + stderr;
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

        private static string CreateFile(string fullPath, int sizeBytes, DateTime lastWriteUtc)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var stream = File.Create(fullPath))
            {
                stream.SetLength(sizeBytes);
            }

            File.SetLastWriteTimeUtc(fullPath, lastWriteUtc);
            return fullPath;
        }

        private static string FindRepoRoot()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current is not null)
            {
                var solutionPath = Path.Combine(current.FullName, "ClearPilot.sln");
                if (File.Exists(solutionPath))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("Could not locate ClearPilot.sln from test base directory.");
        }
    }

    private sealed class ProcessGuardHandle : IDisposable
    {
        private readonly Process process;

        public ProcessGuardHandle(Process process)
        {
            this.process = process;
        }

        public void Dispose()
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(3000);
                }
            }
            catch (InvalidOperationException)
            {
            }
            catch (System.ComponentModel.Win32Exception)
            {
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
