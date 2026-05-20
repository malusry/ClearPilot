using System.Diagnostics;
using System.Text;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class QuickSafeCliOutputTests
{
    [Fact]
    public void QuickSafeClean_Output_StatesS0OnlyBoundary()
    {
        using var workspace = QuickSafeCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);

        var output = workspace.RunQuickSafeCli(Language.English, "1\n\n0\n");

        Assert.Contains("Safety boundary: S0-only.", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("known very-low-risk temporary/cache targets", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Broader cleanup opportunities are not included in Quick Safe Clean", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuickSafeClean_ZhCn_Output_StatesS0OnlyBoundary()
    {
        using var workspace = QuickSafeCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);

        var output = workspace.RunQuickSafeCli(Language.SimplifiedChinese, "1\n\n0\n");

        Assert.Contains("\u5B89\u5168\u8FB9\u754C\uFF1A\u4EC5 S0\u3002", output, StringComparison.Ordinal);
        Assert.Contains("\u66F4\u5E7F\u6CDB\u7684\u6E05\u7406\u673A\u4F1A\u4E0D\u4F1A\u8FDB\u5165\u5FEB\u901F\u5B89\u5168\u6E05\u7406\u3002", output, StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSafeClean_Output_DoesNotImplyAggressiveCleanup()
    {
        using var workspace = QuickSafeCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);

        var output = workspace.RunQuickSafeCli(Language.English, "1\n\n0\n");

        Assert.DoesNotContain("deep clean", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aggressive", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("clean everything", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuickSafeClean_Output_SummarizesSkippedAndFailures()
    {
        using var workspace = QuickSafeCliTestWorkspace.Create();
        var lockedPath = workspace.CreateOldTempFile("locked.tmp", 4096);
        workspace.CreateOldTempFile("clean.tmp", 4096);

        using var lockedStream = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        var output = workspace.RunQuickSafeCli(Language.English, "1\n\n0\n");

        Assert.Contains("Skipped items", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failed items", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Skipped reasons", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Locked or in use", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not retry with elevated privileges", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuickSafeClean_ZhCn_NoMojibake()
    {
        using var workspace = QuickSafeCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);

        var output = workspace.RunQuickSafeCli(Language.SimplifiedChinese, "1\n\n0\n");

        Assert.DoesNotContain("\uFFFD", output, StringComparison.Ordinal);
        Assert.DoesNotContain("娣", output, StringComparison.Ordinal);
        Assert.DoesNotContain("绌", output, StringComparison.Ordinal);
        Assert.DoesNotContain("鍒", output, StringComparison.Ordinal);
        Assert.DoesNotContain("銆", output, StringComparison.Ordinal);
        Assert.DoesNotContain("涓", output, StringComparison.Ordinal);
        Assert.DoesNotContain("璺", output, StringComparison.Ordinal);
        Assert.DoesNotContain("緞", output, StringComparison.Ordinal);
    }

    private sealed class QuickSafeCliTestWorkspace : IDisposable
    {
        private const int CliTimeoutMs = 30000;

        private QuickSafeCliTestWorkspace(string root)
        {
            Root = root;
            TempRoot = Path.Combine(root, "Temp");
            LocalAppDataRoot = Path.Combine(root, "LocalAppData");
            UserProfileRoot = Path.Combine(root, "UserProfile");
            LogDirectory = Path.Combine(root, "logs");
            SettingsPath = Path.Combine(root, "settings", "settings.json");

            Directory.CreateDirectory(TempRoot);
            Directory.CreateDirectory(LocalAppDataRoot);
            Directory.CreateDirectory(UserProfileRoot);
            Directory.CreateDirectory(LogDirectory);
        }

        public string Root { get; }

        public string TempRoot { get; }

        public string LocalAppDataRoot { get; }

        public string UserProfileRoot { get; }

        public string LogDirectory { get; }

        public string SettingsPath { get; }

        public static QuickSafeCliTestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.QuickSafe.Cli.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new QuickSafeCliTestWorkspace(root);
        }

        public string CreateOldTempFile(string fileName, int sizeBytes)
        {
            var fullPath = Path.Combine(TempRoot, fileName);
            using (var stream = File.Create(fullPath))
            {
                stream.SetLength(sizeBytes);
            }

            File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow.AddDays(-2));
            return fullPath;
        }

        public string RunQuickSafeCli(Language language, string scriptedInput)
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
            startInfo.Environment["TEMP"] = TempRoot;
            startInfo.Environment["TMP"] = TempRoot;
            startInfo.Environment["LOCALAPPDATA"] = LocalAppDataRoot;
            startInfo.Environment["USERPROFILE"] = UserProfileRoot;
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
}
