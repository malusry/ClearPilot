using System.Diagnostics;
using System.Text;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceCliRenderingTests
{
    [Fact]
    public void DeepSpaceCli_English_DoesNotShowSuggestedOrRecommendedAction()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096);

        var output = workspace.RunDeepSpaceCli(Language.English);

        Assert.DoesNotContain("Recommended action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suggested action", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeepSpaceCli_English_ShowsReadOnlyNoDeleteFraming()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096);

        var output = workspace.RunDeepSpaceCli(Language.English);

        Assert.Contains("Analysis only", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not delete files", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Downloads is scanned only for storage understanding", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeepSpaceCli_ZhCn_DoesNotShowSuggestedOrRecommendedAction()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096);

        var output = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.DoesNotContain("Recommended action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Suggested action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("建议操作", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_ZhCn_ShowsReadablePathAndReadOnlyFraming()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096);

        var output = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.Contains("路径", output, StringComparison.Ordinal);
        Assert.Contains("仅分析", output, StringComparison.Ordinal);
        Assert.Contains("不会执行删除", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_ZhCn_HasNoMojibake()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096);

        var output = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.DoesNotContain("\uFFFD", output, StringComparison.Ordinal);
        Assert.DoesNotContain("娣", output, StringComparison.Ordinal);
        Assert.DoesNotContain("绌", output, StringComparison.Ordinal);
        Assert.DoesNotContain("鍒", output, StringComparison.Ordinal);
        Assert.DoesNotContain("銆", output, StringComparison.Ordinal);
        Assert.DoesNotContain("涓", output, StringComparison.Ordinal);
        Assert.DoesNotContain("璺", output, StringComparison.Ordinal);
        Assert.DoesNotContain("緞", output, StringComparison.Ordinal);
    }

    private sealed class CliTestWorkspace : IDisposable
    {
        private const int CliTimeoutMs = 30000;

        private CliTestWorkspace(string root)
        {
            Root = root;
            AnalysisRoot = Path.Combine(root, "analysis-root");
            SettingsPath = Path.Combine(root, "settings", "settings.json");
            Directory.CreateDirectory(AnalysisRoot);
        }

        public string Root { get; }

        public string AnalysisRoot { get; }

        public string SettingsPath { get; }

        public static CliTestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Cli.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new CliTestWorkspace(root);
        }

        public void CreateFile(string relativePath, int sizeBytes)
        {
            var fullPath = Path.Combine(AnalysisRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using var stream = File.Create(fullPath);
            stream.SetLength(sizeBytes);
            File.SetLastWriteTimeUtc(fullPath, DateTime.UtcNow.AddDays(-2));
        }

        public string RunDeepSpaceCli(Language language)
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
            startInfo.Environment["CLEARPILOT_ANALYSIS_ROOTS"] = AnalysisRoot;
            startInfo.Environment["NO_COLOR"] = "1";

            using var process = Process.Start(startInfo);
            Assert.NotNull(process);

            process!.StandardInput.Write("3\n");
            process.StandardInput.Write("0\n");
            process.StandardInput.Write("0\n");
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
