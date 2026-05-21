using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Safety;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceScanRootProviderTests
{
    [Fact]
    public void DeepSpace_DownloadsRoot_UsesInjectedProvider()
    {
        using var workspace = TestWorkspace.Create();
        var downloadsRoot = workspace.CreateDirectory("redirected-downloads");
        var provider = new FakeSpecialFolderProvider(downloadsRoot);

        var options = DeepSpaceAnalyzer.CreateDefaultOptions(provider);

        Assert.Contains(options.RootPaths, path => PathEquals(path, downloadsRoot));
    }

    [Fact]
    public void DeepSpace_DownloadsRoot_MissingOrUnavailable_IsSkipped()
    {
        var provider = new FakeSpecialFolderProvider(Path.Combine(Path.GetTempPath(), "ClearPilot", Guid.NewGuid().ToString("N"), "missing-downloads"));

        var options = DeepSpaceAnalyzer.CreateDefaultOptions(provider);
        var missing = provider.TryGetDownloadsPath()!;

        Assert.DoesNotContain(options.RootPaths, path => PathEquals(path, missing));

        var analyzer = new DeepSpaceAnalyzer(ProtectedPathPolicy.CreateDefault(), null, provider);
        var missingOnlyOptions = new DeepSpaceAnalysisOptions
        {
            RootPaths = [missing],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 2048,
            FileTypeSummaryThresholdBytes = 1024,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 2,
            MaxResults = 10
        };
        var result = analyzer.Analyze(missingOnlyOptions, DateTimeOffset.UtcNow);

        Assert.Empty(result);
    }

    [Fact]
    public void DeepSpace_DownloadsFinding_RemainsS2AnalysisOnly()
    {
        using var workspace = TestWorkspace.Create();
        var downloadsRoot = workspace.CreateDirectory("redirected-downloads");
        var downloadsFile = workspace.CreateFile(Path.Combine("redirected-downloads", "large.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var provider = new FakeSpecialFolderProvider(downloadsRoot);
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [downloadsRoot],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 4096,
            FileTypeSummaryThresholdBytes = 4096,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 3,
            MaxResults = 20
        };

        var analyzer = new DeepSpaceAnalyzer(ProtectedPathPolicy.CreateDefault(), null, provider);
        var items = analyzer.Analyze(options, DateTimeOffset.UtcNow);
        var item = Assert.Single(items, candidate => candidate.Path.Equals(downloadsFile, StringComparison.OrdinalIgnoreCase));
        var advice = RecommendationAdvisor.ForDeepSpaceItem(item);
        var decision = CleanupDecisionAdvisor.ForDeepSpaceItem(item, advice);

        Assert.Equal(RiskLevel.S2ReviewRequired, item.RiskLevel);
        Assert.Equal(CleanupDecision.AnalysisOnlyDoNotClean, decision.Decision);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private sealed class FakeSpecialFolderProvider : IDeepSpaceSpecialFolderProvider
    {
        private readonly string? downloadsPath;

        public FakeSpecialFolderProvider(string? downloadsPath)
        {
            this.downloadsPath = downloadsPath;
        }

        public string? TryGetDownloadsPath()
        {
            return downloadsPath;
        }
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.DeepSpace.RootProvider.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, long sizeBytes, DateTime lastWriteTimeUtc)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            using (var stream = File.Create(path))
            {
                stream.SetLength(sizeBytes);
            }

            File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
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
