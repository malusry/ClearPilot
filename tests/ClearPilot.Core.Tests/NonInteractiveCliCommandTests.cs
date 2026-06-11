using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class NonInteractiveCliCommandTests
{
    [Fact]
    public void NoArgs_RemainsInteractive()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();

        var result = workspace.RunCli(Language.English, arguments: "", scriptedInput: "0\n");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("ClearPilot", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanRecommendedJson_DoesNotWaitForStdin_AndOutputsJson()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);
        workspace.CreateRecommendedFile();

        var result = workspace.RunCli(
            Language.English,
            arguments: "clean --recommended --json",
            scriptedInput: null,
            closeInput: false,
            timeoutMs: 5000);

        Assert.True(result.Exited, "CLI process timed out, likely waiting for input.");
        Assert.Equal(0, result.ExitCode);
        Assert.StartsWith("{", result.StdOut.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scan Recommended Items", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Choose an option", result.StdOut, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(result.StdOut);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("recommended", json.RootElement.GetProperty("mode").GetString());
    }

    [Fact]
    public void CleanRecommendedJson_UsesStableJsonShape_WithoutMenuOutput()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        workspace.CreateOldTempFile("clean.tmp", 4096);
        workspace.CreateRecommendedFile();
        workspace.CreateNotRecommendedCrashDump();

        var result = workspace.RunCli(Language.English, "clean --recommended --json");

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("Windows cleanup assistant", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scan Recommended Items", result.StdOut, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(result.StdOut);
        var root = json.RootElement;
        Assert.True(root.TryGetProperty("success", out _));
        Assert.True(root.TryGetProperty("mode", out _));
        Assert.True(root.TryGetProperty("quickSafe", out var quickSafe));
        Assert.True(root.TryGetProperty("recommended", out var recommended));
        Assert.True(root.TryGetProperty("totalDeletedCount", out _));
        Assert.True(root.TryGetProperty("totalDeletedBytes", out _));
        Assert.True(root.TryGetProperty("message", out _));

        Assert.True(quickSafe.TryGetProperty("deletedCount", out _));
        Assert.True(quickSafe.TryGetProperty("deletedBytes", out _));
        Assert.True(quickSafe.TryGetProperty("skippedCount", out _));
        Assert.True(quickSafe.TryGetProperty("failedCount", out _));
        Assert.True(quickSafe.TryGetProperty("logPath", out _));

        Assert.True(recommended.TryGetProperty("deletedCount", out _));
        Assert.True(recommended.TryGetProperty("deletedBytes", out _));
        Assert.True(recommended.TryGetProperty("skippedCount", out _));
        Assert.True(recommended.TryGetProperty("failedCount", out _));
        Assert.True(recommended.TryGetProperty("logPath", out _));
    }

    [Fact]
    public void CleanRecommendedJson_CleansOnlyEligibleRecommendedS1()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var recommendedFile = workspace.CreateRecommendedFile();
        var notRecommendedCrashDump = workspace.CreateNotRecommendedCrashDump();
        var processGuardBlockedFile = workspace.CreateProcessGuardBlockedRecommendedFile();
        using var fakeSteam = workspace.StartFakeSteamProcess();

        var result = workspace.RunCli(Language.English, "clean --recommended --json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(recommendedFile));
        Assert.True(File.Exists(notRecommendedCrashDump));
        Assert.True(File.Exists(processGuardBlockedFile));

        using var json = JsonDocument.Parse(result.StdOut);
        var recommended = json.RootElement.GetProperty("recommended");
        Assert.Equal(1, recommended.GetProperty("deletedCount").GetInt32());
    }

    [Fact]
    public void CleanRecommendedJson_RunsQuickSafeBeforeRecommended_AndTotalsMatch()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var quickSafeFile = workspace.CreateOldTempFile("clean.tmp", 4096);
        var recommendedFile = workspace.CreateRecommendedFile();

        var result = workspace.RunCli(Language.English, "clean --recommended --json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(quickSafeFile));
        Assert.False(File.Exists(recommendedFile));

        using var json = JsonDocument.Parse(result.StdOut);
        var root = json.RootElement;
        var quickSafe = root.GetProperty("quickSafe");
        var recommended = root.GetProperty("recommended");
        Assert.Equal(1, quickSafe.GetProperty("deletedCount").GetInt32());
        Assert.Equal(1, recommended.GetProperty("deletedCount").GetInt32());
        Assert.Equal(
            quickSafe.GetProperty("deletedCount").GetInt32() + recommended.GetProperty("deletedCount").GetInt32(),
            root.GetProperty("totalDeletedCount").GetInt32());
        Assert.Equal(
            quickSafe.GetProperty("deletedBytes").GetInt64() + recommended.GetProperty("deletedBytes").GetInt64(),
            root.GetProperty("totalDeletedBytes").GetInt64());
    }

    [Fact]
    public void CleanRecommendedJson_PartialFailureStillReturnsExitCodeZero()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var lockedFile = workspace.CreateOldTempFile("locked.tmp", 4096);
        var recommendedFile = workspace.CreateRecommendedFile();
        using var lockedStream = new FileStream(lockedFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = workspace.RunCli(Language.English, "clean --recommended --json");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(lockedFile));
        Assert.False(File.Exists(recommendedFile));

        using var json = JsonDocument.Parse(result.StdOut);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        var quickSafe = json.RootElement.GetProperty("quickSafe");
        Assert.True(
            quickSafe.GetProperty("skippedCount").GetInt32() > 0
            || quickSafe.GetProperty("failedCount").GetInt32() > 0);
    }

    [Theory]
    [InlineData("clean --bad --json")]
    [InlineData("unknown --json")]
    public void InvalidJsonCommands_ReturnExitCodeTwoAndJsonError(string arguments)
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();

        var result = workspace.RunCli(Language.English, arguments);

        Assert.Equal(2, result.ExitCode);
        Assert.StartsWith("{", result.StdOut.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scan Recommended Items", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Choose an option", result.StdOut, StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(result.StdOut);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("recommended", json.RootElement.GetProperty("mode").GetString());
        Assert.Equal("Invalid ClearPilot command.", json.RootElement.GetProperty("message").GetString());
        Assert.Equal("Expected: clean --recommended --json", json.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public void MissingJsonFlag_ReturnsExitCodeTwo_WithoutInteractiveMenu()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();

        var result = workspace.RunCli(Language.English, "clean --recommended");

        Assert.Equal(2, result.ExitCode);
        Assert.DoesNotContain("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Scan Recommended Items", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Choose an option", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Expected: clean --recommended --json", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanRecommendedJson_ExternalCallerBubblePet_ProtectsAppAndShaderCaches()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var localBubblePetCache = workspace.CreateLocalBubblePetAppDataCache();
        var roamingBubblePetCache = workspace.CreateRoamingBubblePetAppDataCache();
        var directXShaderCache = workspace.CreateDirectXShaderCacheFile();
        var shaderCacheFiles = workspace.CreateShaderCacheFiles();

        var result = workspace.RunCli(Language.English, "clean --recommended --json --external-caller bubblepet");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(localBubblePetCache));
        Assert.True(File.Exists(roamingBubblePetCache));
        Assert.True(File.Exists(directXShaderCache));
        Assert.All(shaderCacheFiles, path => Assert.True(File.Exists(path), path));

        using var json = JsonDocument.Parse(result.StdOut);
        var recommended = json.RootElement.GetProperty("recommended");
        Assert.Equal(0, recommended.GetProperty("deletedCount").GetInt32());
        Assert.True(recommended.GetProperty("skippedCount").GetInt32() > 0);
        var logs = workspace.ReadRecommendedCleanupLogs();
        Assert.Contains("protected-running-app-cache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("com.bubblepet.translator", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GPUCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GrShaderCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ShaderCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("D3DSCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("DXCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GLCache", logs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ComputeCache", logs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanRecommendedJson_ProtectRunningAppCaches_ProtectsStoreLocalCache()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var storeCache = workspace.CreateStoreLocalCacheFile();

        var result = workspace.RunCli(Language.English, "clean --recommended --json --protect-running-app-caches");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(storeCache));

        using var json = JsonDocument.Parse(result.StdOut);
        var recommended = json.RootElement.GetProperty("recommended");
        Assert.Equal(0, recommended.GetProperty("deletedCount").GetInt32());
        Assert.True(recommended.GetProperty("skippedCount").GetInt32() > 0);
        Assert.Contains("protected-running-app-cache", workspace.ReadRecommendedCleanupLogs(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanRecommendedJson_DefaultCommand_DoesNotApplyRunningAppCacheProtection()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var storeCache = workspace.CreateStoreLocalCacheFile();

        var result = workspace.RunCli(Language.English, "clean --recommended --json");

        Assert.Equal(0, result.ExitCode);
        Assert.False(File.Exists(storeCache));

        using var json = JsonDocument.Parse(result.StdOut);
        var recommended = json.RootElement.GetProperty("recommended");
        Assert.True(recommended.GetProperty("deletedCount").GetInt32() >= 1);
        Assert.DoesNotContain("protected-running-app-cache", workspace.ReadRecommendedCleanupLogs(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CleanRecommendedJson_ExternalCallerBubblePet_DryRunKeepsFilesAndOutputsJson()
    {
        using var workspace = NonInteractiveCliTestWorkspace.Create();
        var storeCache = workspace.CreateStoreLocalCacheFile();
        var recommendedFile = workspace.CreateRecommendedFile();

        var result = workspace.RunCli(Language.English, "clean --recommended --json --external-caller bubblepet --dry-run");

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(storeCache));
        Assert.True(File.Exists(recommendedFile));
        Assert.DoesNotContain("Quick Safe Clean", result.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommended Cleanup", result.StdOut, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(result.StdOut);
        Assert.True(json.RootElement.GetProperty("success").GetBoolean());
        Assert.True(json.RootElement.TryGetProperty("mode", out _));
        Assert.True(json.RootElement.TryGetProperty("quickSafe", out _));
        Assert.True(json.RootElement.TryGetProperty("recommended", out _));
        Assert.True(json.RootElement.TryGetProperty("totalDeletedCount", out _));
        Assert.True(json.RootElement.TryGetProperty("totalDeletedBytes", out _));
        Assert.True(json.RootElement.TryGetProperty("message", out _));
        var recommended = json.RootElement.GetProperty("recommended");
        Assert.Equal(0, recommended.GetProperty("deletedCount").GetInt32());
        Assert.True(recommended.GetProperty("skippedCount").GetInt32() > 0);
    }

    private sealed class NonInteractiveCliTestWorkspace : IDisposable
    {
        private const int DefaultCliTimeoutMs = 30000;

        private NonInteractiveCliTestWorkspace(string root)
        {
            Root = root;
            LocalAppDataRoot = Path.Combine(root, "LocalAppData");
            RoamingAppDataRoot = Path.Combine(root, "RoamingAppData");
            UserProfileRoot = Path.Combine(root, "UserProfile");
            TempRoot = Path.Combine(root, "Temp");
            SettingsPath = Path.Combine(root, "settings", "settings.json");
            LogDirectory = Path.Combine(root, "logs");

            Directory.CreateDirectory(LocalAppDataRoot);
            Directory.CreateDirectory(RoamingAppDataRoot);
            Directory.CreateDirectory(UserProfileRoot);
            Directory.CreateDirectory(TempRoot);
            Directory.CreateDirectory(LogDirectory);
        }

        public string Root { get; }

        public string LocalAppDataRoot { get; }

        public string RoamingAppDataRoot { get; }

        public string UserProfileRoot { get; }

        public string TempRoot { get; }

        public string SettingsPath { get; }

        public string LogDirectory { get; }

        public static NonInteractiveCliTestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.NonInteractive.Cli.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new NonInteractiveCliTestWorkspace(root);
        }

        public string CreateOldTempFile(string fileName, int sizeBytes)
        {
            var fullPath = Path.Combine(TempRoot, fileName);
            return CreateFile(fullPath, sizeBytes, DateTime.UtcNow.AddDays(-2));
        }

        public string CreateRecommendedFile()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "Microsoft", "Windows", "INetCache", "cache.bin"),
                4096,
                DateTime.UtcNow.AddDays(-2));
        }

        public string CreateNotRecommendedCrashDump()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "CrashDumps", "app.dmp"),
                1024,
                DateTime.UtcNow.AddDays(-10));
        }

        public string CreateProcessGuardBlockedRecommendedFile()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "Steam", "appcache", "httpcache", "steam-cache.bin"),
                4096,
                DateTime.UtcNow.AddDays(-2));
        }

        public string CreateLocalBubblePetAppDataCache()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "com.bubblepet.translator", "GPUCache", "bubblepet-cache.bin"),
                4096,
                DateTime.UtcNow.AddDays(-2));
        }

        public string CreateRoamingBubblePetAppDataCache()
        {
            return CreateFile(
                Path.Combine(RoamingAppDataRoot, "com.bubblepet.translator", "Cache", "bubblepet-cache.bin"),
                4096,
                DateTime.UtcNow.AddDays(-2));
        }

        public string CreateDirectXShaderCacheFile()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "D3DSCache", "webview-shader.bin"),
                4096,
                DateTime.UtcNow.AddDays(-8));
        }

        public string CreateStoreLocalCacheFile()
        {
            return CreateFile(
                Path.Combine(LocalAppDataRoot, "Packages", "BubblePet.Test_123", "LocalCache", "webview-cache.bin"),
                4096,
                DateTime.UtcNow.AddDays(-2));
        }

        public IReadOnlyList<string> CreateShaderCacheFiles()
        {
            var segments = new[]
            {
                "GPUCache",
                "GrShaderCache",
                "ShaderCache",
                "D3DSCache",
                "DXCache",
                "GLCache",
                "ComputeCache"
            };

            return segments
                .Select(segment => CreateFile(
                    Path.Combine(LocalAppDataRoot, segment, "cache.bin"),
                    4096,
                    DateTime.UtcNow.AddDays(-2)))
                .ToArray();
        }

        public string ReadRecommendedCleanupLogs()
        {
            if (!Directory.Exists(LogDirectory))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var path in Directory.EnumerateFiles(LogDirectory, "*-RecommendedCleanup.json"))
            {
                builder.AppendLine(File.ReadAllText(path, Encoding.UTF8));
            }

            return builder.ToString();
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

        public CliCommandResult RunCli(
            Language language,
            string arguments,
            string? scriptedInput = "",
            bool closeInput = true,
            int timeoutMs = DefaultCliTimeoutMs,
            bool dryRun = false)
        {
            var store = new SettingsStore(SettingsPath);
            store.Save(new AppSettings
            {
                Language = language,
                LogRetentionDays = AppSettings.DefaultLogRetentionDays,
                AutoEmptyRecycleBin = false,
                DryRun = dryRun
            });

            var repoRoot = FindRepoRoot();
            var dotnetPath = Path.Combine(repoRoot, ".dotnet", "dotnet.exe");
            var cliDllPath = Path.Combine(repoRoot, "src", "ClearPilot.Cli", "bin", "Debug", "net10.0", "ClearPilot.dll");
            Assert.True(File.Exists(cliDllPath), $"CLI binary not found: {cliDllPath}");

            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(dotnetPath) ? dotnetPath : "dotnet",
                Arguments = string.IsNullOrWhiteSpace(arguments)
                    ? $"\"{cliDllPath}\""
                    : $"\"{cliDllPath}\" {arguments}",
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
            startInfo.Environment["APPDATA"] = RoamingAppDataRoot;
            startInfo.Environment["USERPROFILE"] = UserProfileRoot;
            startInfo.Environment["TEMP"] = TempRoot;
            startInfo.Environment["TMP"] = TempRoot;
            startInfo.Environment["NO_COLOR"] = "1";

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            if (scriptedInput is not null)
            {
                process!.StandardInput.Write(scriptedInput);
            }

            if (closeInput)
            {
                process!.StandardInput.Close();
            }

            var exited = process!.WaitForExit(timeoutMs);
            if (!exited)
            {
                process.Kill(entireProcessTree: true);
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            return new CliCommandResult(exited, process.ExitCode, stdout, stderr);
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

    private sealed record CliCommandResult(bool Exited, int ExitCode, string StdOut, string StdErr);

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
