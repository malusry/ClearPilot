using System.Diagnostics;
using System.Text;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Localization;
using ClearPilot.Core.Settings;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceCliRenderingTests
{
    [Fact]
    public void DeepSpaceCli_UsesSimplifiedInsightBoundaryCard()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

        var output = workspace.RunDeepSpaceCli(Language.English);

        Assert.Contains("Decision:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Risk:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Path:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Insight:", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Boundary:", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeepSpaceCli_ZhCn_UsesSimplifiedInsightBoundaryCard()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

        var output = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.Contains("结论", output, StringComparison.Ordinal);
        Assert.Contains("风险", output, StringComparison.Ordinal);
        Assert.Contains("路径", output, StringComparison.Ordinal);
        Assert.Contains("说明", output, StringComparison.Ordinal);
        Assert.Contains("边界", output, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_DoesNotShowActionFields()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

        var output = workspace.RunDeepSpaceCli(Language.English);
        var zhOutput = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.DoesNotContain("Suggested action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Recommended action", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("建议操作", zhOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_DoesNotUseSeparateVerboseFieldsAsPrimary()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

        var output = workspace.RunDeepSpaceCli(Language.English);
        var zhOutput = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.DoesNotContain("Possible impact if cleaned:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Safety note:", output, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("清理后的可能影响", zhOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("安全说明", zhOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_ReadOnlyBoundaryStillVisible()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

        var output = workspace.RunDeepSpaceCli(Language.English);
        var zhOutput = workspace.RunDeepSpaceCli(Language.SimplifiedChinese);

        Assert.Contains("Read-only", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("will not delete", output, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("只读", zhOutput, StringComparison.Ordinal);
        Assert.Contains("不会删除", zhOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void DeepSpaceCli_ZhCn_NoMojibake()
    {
        using var workspace = CliTestWorkspace.Create();
        workspace.CreateFile(Path.Combine("Downloads", "archive.iso"), 4 * 1024 * 1024);

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

    [Fact]
    public void DeepSpaceCli_UsesFieldLevelColorSemantics()
    {
        Assert.Equal(ConsoleColor.DarkCyan, ClearPilot.Cli.ConsolePresentationStyle.GetDeepSpaceFieldLabelColor());
        Assert.Equal(ConsoleColor.Cyan, ClearPilot.Cli.ConsolePresentationStyle.GetDeepSpaceSizeColor());
        Assert.Equal(ConsoleColor.DarkGray, ClearPilot.Cli.ConsolePresentationStyle.GetDeepSpacePathColor());
        Assert.Equal(ConsoleColor.Cyan, ClearPilot.Cli.ConsolePresentationStyle.GetDecisionColor(CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal(ConsoleColor.DarkMagenta, ClearPilot.Cli.ConsolePresentationStyle.GetRiskColor(RiskLevel.S2ReviewRequired));
        Assert.Equal(ConsoleColor.Yellow, ClearPilot.Cli.ConsolePresentationStyle.GetDeepSpaceBoundaryColor(CleanupDecision.AnalysisOnlyDoNotClean));
        Assert.Equal(ConsoleColor.Red, ClearPilot.Cli.ConsolePresentationStyle.GetDeepSpaceBoundaryColor(CleanupDecision.Blocked));
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
