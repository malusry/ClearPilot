using ClearPilot.Core.Analysis;
using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Safety;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class DeepSpaceAnalyzerTests
{
    [Fact]
    public void DefaultOptionsUseUsefulButStillConservativeThresholds()
    {
        var options = new DeepSpaceAnalysisOptions();

        Assert.Equal(100L * 1024 * 1024, options.LargeFileThresholdBytes);
        Assert.Equal(500L * 1024 * 1024, options.LargeFolderThresholdBytes);
        Assert.Equal(100L * 1024 * 1024, options.FileTypeSummaryThresholdBytes);
        Assert.Equal(TimeSpan.FromDays(30), options.OldArchiveAge);
        Assert.Empty(options.ExcludePathSegments);
    }

    [Fact]
    public void AnalyzeReportsLargeFilesWithoutDeletingThem()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateFile("large.bin", 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.LargeFile && item.Path == file);
        Assert.Equal(RiskLevel.S2ReviewRequired, item.RiskLevel);
        Assert.False(string.IsNullOrWhiteSpace(item.Explanation));
        Assert.False(string.IsNullOrWhiteSpace(item.SuggestedAction));
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void AnalyzeReportsOldArchivesAndInstallers()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateFile("old-installer.iso", 128, DateTime.UtcNow.AddDays(-10));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.OldArchiveOrInstaller);
        Assert.Equal(file, item.Path);
        Assert.Equal(RiskLevel.S2ReviewRequired, item.RiskLevel);
        Assert.Contains("disk image", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mount", item.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeepSpaceAdviceKey.DiskImage, item.AdviceKey);
        Assert.Equal(".iso", item.AdviceSubject);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void AnalyzeReportsProjectDependencyFolders()
    {
        using var workspace = TestWorkspace.Create();
        var dependencyFile = workspace.CreateFile(Path.Combine("project", "node_modules", "package", "index.js"), 2048, DateTime.UtcNow.AddDays(-2));
        var dependencyFolder = Path.Combine(workspace.Root, "project", "node_modules");
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.ProjectDependencyFolder && item.Path == dependencyFolder);
        Assert.Contains("Node.js", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("package manifests", item.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeepSpaceAdviceKey.NodeModules, item.AdviceKey);
        Assert.Equal("node_modules", item.AdviceSubject);
        Assert.True(File.Exists(dependencyFile));
    }

    [Fact]
    public void AnalyzeReportsVirtualEnvironmentDependencyFolders()
    {
        using var workspace = TestWorkspace.Create();
        var dependencyFile = workspace.CreateFile(Path.Combine("project", ".venv", "Lib", "site-packages", "package.py"), 2048, DateTime.UtcNow.AddDays(-2));
        var dependencyFolder = Path.Combine(workspace.Root, "project", ".venv");
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.ProjectDependencyFolder && item.Path == dependencyFolder);
        Assert.Contains("Python", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("recreated", item.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeepSpaceAdviceKey.PythonVirtualEnvironment, item.AdviceKey);
        Assert.Equal(".venv", item.AdviceSubject);
        Assert.True(File.Exists(dependencyFile));
    }

    [Fact]
    public void AnalyzeReportsFrontendBuildAndCacheFolders()
    {
        using var workspace = TestWorkspace.Create();
        var buildFile = workspace.CreateFile(Path.Combine("app", ".next", "cache", "bundle.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var buildFolder = Path.Combine(workspace.Root, "app", ".next");
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.ProjectDependencyFolder && item.Path == buildFolder);
        Assert.Equal(DeepSpaceAdviceKey.FrontendFrameworkOutput, item.AdviceKey);
        Assert.Contains("Frontend", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(buildFile));
    }

    [Fact]
    public void AnalyzeUsesMediaSpecificAdviceForLargeVideoFiles()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateFile("large-video.mp4", 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        var item = Assert.Single(items, item => item.Type == DeepSpaceItemType.LargeFile && item.Path == file);
        Assert.Contains("Video", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("external storage", item.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(DeepSpaceAdviceKey.VideoFile, item.AdviceKey);
        Assert.Equal(".mp4", item.AdviceSubject);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void AnalyzeReportsLargeNestedFolders()
    {
        using var workspace = TestWorkspace.Create();
        var folderFile = workspace.CreateFile(Path.Combine("Documents", "Videos", "clip.mp4"), 4096, DateTime.UtcNow.AddDays(-2));
        var folder = Path.Combine(workspace.Root, "Documents", "Videos");
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.Contains(items, item => item.Type == DeepSpaceItemType.LargeFolder && item.Path == folder);
        Assert.True(File.Exists(folderFile));
    }

    [Fact]
    public void AnalyzeSuppressesParentLargeFolderWhenDominantChildExplainsTheSpace()
    {
        using var workspace = TestWorkspace.Create();
        var childFile = workspace.CreateFile(Path.Combine("Downloads", "Archive", "large.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var parentFolder = Path.Combine(workspace.Root, "Downloads");
        var childFolder = Path.Combine(workspace.Root, "Downloads", "Archive");
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.DoesNotContain(items, item => item.Type == DeepSpaceItemType.LargeFolder && item.Path == parentFolder);
        Assert.Contains(items, item => item.Type == DeepSpaceItemType.LargeFolder && item.Path == childFolder);
        Assert.True(File.Exists(childFile));
    }

    [Fact]
    public void AnalyzeReportsFileTypeSpaceSummaries()
    {
        using var workspace = TestWorkspace.Create();
        var zipFile = workspace.CreateFile("archive.zip", 2048, DateTime.UtcNow);
        var isoFile = workspace.CreateFile("image.iso", 4096, DateTime.UtcNow);
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [workspace.Root],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 2048,
            FileTypeSummaryThresholdBytes = 1024,
            OldArchiveAge = TimeSpan.FromDays(30),
            MaxDepth = 5,
            MaxResults = 20
        };

        var items = analyzer.Analyze(options, DateTimeOffset.UtcNow);

        Assert.Contains(items, item =>
            item.Type == DeepSpaceItemType.FileTypeSummary
            && item.Path == workspace.Root
            && item.Explanation.Contains(".iso", StringComparison.OrdinalIgnoreCase)
            && item.SuggestedAction.Contains("disk images", StringComparison.OrdinalIgnoreCase));
        Assert.True(File.Exists(zipFile));
        Assert.True(File.Exists(isoFile));
    }

    [Fact]
    public void AnalyzeWithSummaryReportsScanCountsAndFindingFootprint()
    {
        using var workspace = TestWorkspace.Create();
        var largeFile = workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var oldInstaller = workspace.CreateFile(Path.Combine("Downloads", "old.msi"), 2048, DateTime.UtcNow.AddDays(-10));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var result = analyzer.AnalyzeWithSummary(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.True(result.Summary.ScannedRootCount >= 1);
        Assert.True(result.Summary.ScannedDirectoryCount >= 2);
        Assert.True(result.Summary.ScannedFileCount >= 2);
        Assert.Equal(result.Items.Count, result.Summary.FindingCount);
        Assert.Equal(result.Items.Sum(item => item.SizeBytes), result.Summary.FindingBytes);
        Assert.Contains(result.Items, item => item.Path == largeFile);
        Assert.Contains(result.Items, item => item.Path == oldInstaller);
    }

    [Fact]
    public void AnalyzeSkipsBlockedRoots()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateFile("large.bin", 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([workspace.Root]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.Empty(items);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void AnalyzeSkipsConfiguredExcludedPathSegments()
    {
        using var workspace = TestWorkspace.Create();
        var skippedFile = workspace.CreateFile(Path.Combine("InternalClearPilot", "logs", "20260517-QuickSafeClean.json"), 4096, DateTime.UtcNow.AddDays(-2));
        var keptFile = workspace.CreateFile(Path.Combine("Downloads", "large.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [workspace.Root],
            ExcludePathSegments = ["InternalClearPilot"],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 2048,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 5,
            MaxResults = 20
        };

        var items = analyzer.Analyze(options, DateTimeOffset.UtcNow);

        Assert.DoesNotContain(items, item => item.Path.Contains("QuickSafeClean", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(items, item => item.Path == keptFile);
        Assert.True(File.Exists(skippedFile));
    }

    [Fact]
    public void AnalyzeSkipsClearPilotInternalArtifactsByFileName()
    {
        using var workspace = TestWorkspace.Create();
        var internalLog = workspace.CreateFile(Path.Combine("RandomCache", "20260517-QuickSafeClean.json"), 4096, DateTime.UtcNow.AddDays(-2));
        var keptFile = workspace.CreateFile(Path.Combine("RandomCache", "large.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var items = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.DoesNotContain(items, item => item.Path == internalLog);
        Assert.Contains(items, item => item.Path == keptFile);
        Assert.True(File.Exists(internalLog));
    }

    [Fact]
    public void AnalyzeReturnsLargestItemsFirstAndRespectsMaxResults()
    {
        using var workspace = TestWorkspace.Create();
        var small = workspace.CreateFile("small.bin", 2048, DateTime.UtcNow.AddDays(-2));
        var large = workspace.CreateFile("large.bin", 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [workspace.Root],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 2048,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 5,
            MaxResults = 1
        };

        var items = analyzer.Analyze(options, DateTimeOffset.UtcNow);

        var item = Assert.Single(items);
        Assert.Equal(large, item.Path);
        Assert.True(File.Exists(small));
        Assert.True(File.Exists(large));
    }

    [Fact]
    public void AnalyzeClassifiesWindowsSystemManagedAreasAsS2ReviewOnlyWhenPresent()
    {
        var windowsRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var cbsLogs = Path.Combine(windowsRoot, "Logs", "CBS");
        if (!Directory.Exists(cbsLogs))
        {
            return;
        }

        var analyzer = new DeepSpaceAnalyzer(ProtectedPathPolicy.CreateDefault());
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [cbsLogs],
            LargeFileThresholdBytes = long.MaxValue,
            LargeFolderThresholdBytes = long.MaxValue,
            FileTypeSummaryThresholdBytes = long.MaxValue,
            MaxDepth = 1,
            MaxResults = 20
        };

        var result = analyzer.Analyze(options, DateTimeOffset.UtcNow);
        var item = Assert.Single(result, item => item.Path.Equals(cbsLogs, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(DeepSpaceItemType.SystemManagedWindowsArea, item.Type);
        Assert.Equal(RiskLevel.S2ReviewRequired, item.RiskLevel);
        Assert.Contains("review-only", item.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Storage Sense", item.SuggestedAction, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("cp.s2.cbs-logs", item.TargetId);
    }

    [Fact]
    public void AnalyzeClassifiesGameLauncherReviewAreasAsS2AndDoesNotDelete()
    {
        using var workspace = TestWorkspace.Create();
        var shaderCacheRoot = Path.Combine(workspace.Root, "Steam", "steamapps", "shadercache");
        var cacheFile = workspace.CreateFile(Path.Combine("Steam", "steamapps", "shadercache", "cache.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(
            new ProtectedPathPolicy([]),
            [
                new DeepSpaceAnalyzer.ReviewOnlyAreaDefinition(
                    "cp.s2.steam-shadercache",
                    "Steam shader cache (analysis-only)",
                    shaderCacheRoot,
                    "Launcher-managed shader cache. Review-only.",
                    "Review manually with launcher closed.",
                    DeepSpaceItemType.GameLauncherReviewArea)
            ]);
        var options = new DeepSpaceAnalysisOptions
        {
            RootPaths = [shaderCacheRoot],
            LargeFileThresholdBytes = long.MaxValue,
            LargeFolderThresholdBytes = long.MaxValue,
            FileTypeSummaryThresholdBytes = long.MaxValue,
            MaxDepth = 1,
            MaxResults = 20
        };

        var result = analyzer.Analyze(options, DateTimeOffset.UtcNow);
        var item = Assert.Single(result, candidate => candidate.Path.Equals(shaderCacheRoot, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(DeepSpaceItemType.GameLauncherReviewArea, item.Type);
        Assert.Equal(RiskLevel.S2ReviewRequired, item.RiskLevel);
        Assert.Equal("cp.s2.steam-shadercache", item.TargetId);
        Assert.True(File.Exists(cacheFile));
    }

    [Fact]
    public void DeepSpace_DefaultRoots_ExcludePersonalLibraries()
    {
        var options = DeepSpaceAnalyzer.CreateDefaultOptions();
        var roots = options.RootPaths.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var personalLibraries = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
        };

        foreach (var library in personalLibraries.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            Assert.DoesNotContain(NormalizePath(library), roots);
        }
    }

    [Fact]
    public void DeepSpace_DefaultRoots_CanIncludeDownloadsReadOnly()
    {
        var options = DeepSpaceAnalyzer.CreateDefaultOptions();
        var roots = options.RootPaths.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var downloads = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        downloads = string.IsNullOrWhiteSpace(downloads) ? string.Empty : Path.Combine(downloads, "Downloads");

        if (!string.IsNullOrWhiteSpace(downloads) && Directory.Exists(downloads))
        {
            Assert.Contains(NormalizePath(downloads), roots);
        }
    }

    [Fact]
    public void DeepSpace_DoesNotDeleteFiles()
    {
        using var workspace = TestWorkspace.Create();
        var file = workspace.CreateFile(Path.Combine("Downloads", "keep.bin"), 4096, DateTime.UtcNow.AddDays(-2));
        var analyzer = new DeepSpaceAnalyzer(new ProtectedPathPolicy([]));

        var result = analyzer.Analyze(CreateOptions(workspace.Root), DateTimeOffset.UtcNow);

        Assert.NotEmpty(result);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public void DeepSpace_PersonalLibraries_NotDefaultScanScope()
    {
        var options = DeepSpaceAnalyzer.CreateDefaultOptions();
        var roots = options.RootPaths.Select(NormalizePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blockedSegments = new[] { "Desktop", "Documents", "Pictures", "Videos", "Music" };

        foreach (var root in roots)
        {
            var segments = root.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
            Assert.DoesNotContain(segments, segment => blockedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
        }
    }

    private static DeepSpaceAnalysisOptions CreateOptions(string root)
    {
        return new DeepSpaceAnalysisOptions
        {
            RootPaths = [root],
            LargeFileThresholdBytes = 1024,
            LargeFolderThresholdBytes = 2048,
            OldArchiveAge = TimeSpan.FromDays(1),
            MaxDepth = 5,
            MaxResults = 20
        };
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
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
