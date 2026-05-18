using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Safety;

namespace ClearPilot.Core.Analysis;

public sealed class DeepSpaceAnalyzer
{
    private static readonly HashSet<string> ArchiveAndInstallerExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip",
        ".7z",
        ".rar",
        ".tar",
        ".gz",
        ".tgz",
        ".iso",
        ".msi",
        ".exe"
    };

    private static readonly HashSet<string> ProjectDependencyFolderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "node_modules",
        ".venv",
        "venv",
        "target",
        "bin",
        "obj",
        ".gradle",
        ".next",
        ".nuxt",
        ".svelte-kit",
        ".angular",
        ".parcel-cache",
        ".turbo",
        ".cache",
        ".pytest_cache",
        ".mypy_cache",
        ".ruff_cache",
        ".tox",
        ".terraform",
        "dist",
        "build",
        "coverage",
        "out",
        "vendor"
    };

    private readonly ProtectedPathPolicy protectedPathPolicy;

    public DeepSpaceAnalyzer(ProtectedPathPolicy protectedPathPolicy)
    {
        this.protectedPathPolicy = protectedPathPolicy;
    }

    public IReadOnlyList<DeepSpaceItem> Analyze(DeepSpaceAnalysisOptions options, DateTimeOffset now)
    {
        return AnalyzeWithSummary(options, now).Items;
    }

    public DeepSpaceAnalysisResult AnalyzeWithSummary(DeepSpaceAnalysisOptions options, DateTimeOffset now)
    {
        var buckets = new AnalysisBuckets();
        var visitedDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in options.RootPaths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(root);
            }
            catch (ArgumentException)
            {
                continue;
            }

            if (!Directory.Exists(normalizedRoot)
                || protectedPathPolicy.IsBlocked(normalizedRoot)
                || ClearPilotInternalPathPolicy.IsInternalArtifact(normalizedRoot))
            {
                continue;
            }

            buckets.ScannedRootCount++;
            AnalyzeDirectory(normalizedRoot, normalizedRoot, depth: 0, options, now, buckets, visitedDirectories);
        }

        buckets.Items.AddRange(CreateFileTypeSummaryItems(buckets.FileTypeStats, options));

        var items = LimitByType(ReduceDuplicateLargeFolders(buckets.Items), options)
            .OrderByDescending(item => item.SizeBytes)
            .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxResults)
            .ToArray();

        var summary = new DeepSpaceAnalysisSummary(
            buckets.ScannedRootCount,
            buckets.ScannedDirectoryCount,
            buckets.ScannedFileCount,
            items.Length,
            items.Sum(item => item.SizeBytes));

        return new DeepSpaceAnalysisResult(items, summary);
    }

    public static DeepSpaceAnalysisOptions CreateDefaultOptions()
    {
        var roots = GetDefaultUserControlledRoots();
        return new DeepSpaceAnalysisOptions
        {
            RootPaths = roots,
            ExcludePathSegments = ["ClearPilot", "ClearPilot.Tests"]
        };
    }

    private DirectoryScanSummary AnalyzeDirectory(
        string directoryPath,
        string rootPath,
        int depth,
        DeepSpaceAnalysisOptions options,
        DateTimeOffset now,
        AnalysisBuckets buckets,
        HashSet<string> visitedDirectories)
    {
        string normalizedDirectory;
        try
        {
            normalizedDirectory = Path.GetFullPath(directoryPath);
        }
        catch (ArgumentException)
        {
            return DirectoryScanSummary.Empty;
        }

        if (!visitedDirectories.Add(normalizedDirectory)
            || protectedPathPolicy.IsBlocked(normalizedDirectory)
            || ClearPilotInternalPathPolicy.IsInternalArtifact(normalizedDirectory)
            || IsExcluded(normalizedDirectory, options.ExcludePathSegments))
        {
            return DirectoryScanSummary.Empty;
        }

        var directoryInfo = TryGetDirectoryInfo(normalizedDirectory);
        if (directoryInfo is null || directoryInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            return DirectoryScanSummary.Empty;
        }

        buckets.ScannedDirectoryCount++;

        if (ProjectDependencyFolderNames.Contains(directoryInfo.Name))
        {
            var dependencySummary = CalculateDirectorySummary(normalizedDirectory, buckets);
            if (dependencySummary.SizeBytes > 0)
            {
                buckets.Items.Add(new DeepSpaceItem(
                    DeepSpaceItemType.ProjectDependencyFolder,
                    normalizedDirectory,
                    dependencySummary.SizeBytes,
                    dependencySummary.LastWriteTime,
                    RiskLevel.S2ReviewRequired,
                    GetProjectDependencyExplanation(directoryInfo.Name),
                    GetProjectDependencySuggestedAction(directoryInfo.Name),
                    GetProjectDependencyAdviceKey(directoryInfo.Name),
                    directoryInfo.Name));
            }

            return dependencySummary;
        }

        long directorySizeBytes = 0;
        DateTimeOffset? latestWriteTime = new DateTimeOffset(directoryInfo.LastWriteTimeUtc, TimeSpan.Zero);

        foreach (var file in EnumerateFiles(normalizedDirectory))
        {
            if (IsExcluded(file.FullName, options.ExcludePathSegments))
            {
                continue;
            }

            if (ClearPilotInternalPathPolicy.IsInternalArtifact(file.FullName))
            {
                continue;
            }

            var fileSummary = AnalyzeFile(file, rootPath, options, now, buckets);
            directorySizeBytes += fileSummary.SizeBytes;
            latestWriteTime = Max(latestWriteTime, fileSummary.LastWriteTime);
        }

        if (depth < options.MaxDepth)
        {
            foreach (var childDirectory in EnumerateDirectories(normalizedDirectory))
            {
                var childSummary = AnalyzeDirectory(childDirectory.FullName, rootPath, depth + 1, options, now, buckets, visitedDirectories);
                directorySizeBytes += childSummary.SizeBytes;
                latestWriteTime = Max(latestWriteTime, childSummary.LastWriteTime);
            }
        }

        if (depth > 0 && directorySizeBytes >= options.LargeFolderThresholdBytes)
        {
            buckets.Items.Add(new DeepSpaceItem(
                DeepSpaceItemType.LargeFolder,
                normalizedDirectory,
                directorySizeBytes,
                latestWriteTime,
                RiskLevel.S2ReviewRequired,
                "Large user-controlled folder. Review manually before deleting, archiving, or moving anything.",
                "Open the folder and decide whether to archive, move to another drive, or clean it with the owning app or tool.",
                DeepSpaceAdviceKey.LargeFolder));
        }

        return new DirectoryScanSummary(directorySizeBytes, latestWriteTime);
    }

    private static DirectoryScanSummary AnalyzeFile(
        FileInfo file,
        string rootPath,
        DeepSpaceAnalysisOptions options,
        DateTimeOffset now,
        AnalysisBuckets buckets)
    {
        long sizeBytes;
        DateTimeOffset lastWriteTime;
        try
        {
            sizeBytes = file.Length;
            lastWriteTime = new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero);
        }
        catch (IOException)
        {
            return DirectoryScanSummary.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return DirectoryScanSummary.Empty;
        }

        buckets.ScannedFileCount++;
        var extension = NormalizeExtension(file.Extension);
        AddFileTypeStat(buckets.FileTypeStats, rootPath, extension, sizeBytes, lastWriteTime);

        var isOldArchiveOrInstaller = ArchiveAndInstallerExtensions.Contains(extension)
            && now - lastWriteTime >= options.OldArchiveAge;

        if (sizeBytes >= options.LargeFileThresholdBytes && !isOldArchiveOrInstaller)
        {
            buckets.Items.Add(new DeepSpaceItem(
                DeepSpaceItemType.LargeFile,
                file.FullName,
                sizeBytes,
                lastWriteTime,
                RiskLevel.S2ReviewRequired,
                GetLargeFileExplanation(extension),
                GetLargeFileSuggestedAction(extension),
                GetLargeFileAdviceKey(extension),
                extension));
        }

        if (isOldArchiveOrInstaller)
        {
            buckets.Items.Add(new DeepSpaceItem(
                DeepSpaceItemType.OldArchiveOrInstaller,
                file.FullName,
                sizeBytes,
                lastWriteTime,
                RiskLevel.S2ReviewRequired,
                GetOldArchiveOrInstallerExplanation(extension),
                GetOldArchiveOrInstallerSuggestedAction(extension),
                GetOldArchiveOrInstallerAdviceKey(extension),
                extension));
        }

        return new DirectoryScanSummary(sizeBytes, lastWriteTime);
    }

    private static IReadOnlyList<DeepSpaceItem> CreateFileTypeSummaryItems(
        IReadOnlyDictionary<FileTypeStatKey, FileTypeStat> stats,
        DeepSpaceAnalysisOptions options)
    {
        return stats
            .Where(pair => pair.Value.SizeBytes >= options.FileTypeSummaryThresholdBytes)
            .Select(pair => new DeepSpaceItem(
                DeepSpaceItemType.FileTypeSummary,
                pair.Key.RootPath,
                pair.Value.SizeBytes,
                pair.Value.LastWriteTime,
                RiskLevel.S2ReviewRequired,
                GetFileTypeSummaryExplanation(pair.Key.Extension),
                GetFileTypeSummarySuggestedAction(pair.Key.Extension),
                GetFileTypeSummaryAdviceKey(pair.Key.Extension),
                pair.Key.Extension))
            .ToArray();
    }

    private static IReadOnlyList<DeepSpaceItem> LimitByType(IReadOnlyList<DeepSpaceItem> items, DeepSpaceAnalysisOptions options)
    {
        return items
            .GroupBy(item => item.Type)
            .SelectMany(group => group
                .OrderByDescending(item => item.SizeBytes)
                .ThenBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .Take(GetTypeLimit(group.Key, options)))
            .ToArray();
    }

    private static int GetTypeLimit(DeepSpaceItemType type, DeepSpaceAnalysisOptions options)
    {
        return type switch
        {
            DeepSpaceItemType.LargeFile => options.MaxLargeFiles,
            DeepSpaceItemType.LargeFolder => options.MaxLargeFolders,
            DeepSpaceItemType.OldArchiveOrInstaller => options.MaxOldArchivesAndInstallers,
            DeepSpaceItemType.ProjectDependencyFolder => options.MaxProjectDependencyFolders,
            DeepSpaceItemType.FileTypeSummary => options.MaxFileTypeSummaries,
            _ => options.MaxResults
        };
    }

    private static IReadOnlyList<DeepSpaceItem> ReduceDuplicateLargeFolders(IReadOnlyList<DeepSpaceItem> items)
    {
        var largeFolders = items
            .Where(item => item.Type == DeepSpaceItemType.LargeFolder)
            .ToArray();

        if (largeFolders.Length == 0)
        {
            return items;
        }

        var dominantDescendants = items
            .Where(item => item.Type is DeepSpaceItemType.LargeFolder or DeepSpaceItemType.ProjectDependencyFolder)
            .ToArray();

        return items
            .Where(item => item.Type != DeepSpaceItemType.LargeFolder || !HasDominantDescendant(item, dominantDescendants))
            .ToArray();
    }

    private static bool HasDominantDescendant(DeepSpaceItem parent, IReadOnlyList<DeepSpaceItem> possibleDescendants)
    {
        foreach (var descendant in possibleDescendants)
        {
            if (ReferenceEquals(parent, descendant)
                || string.Equals(parent.Path, descendant.Path, StringComparison.OrdinalIgnoreCase)
                || descendant.SizeBytes < parent.SizeBytes * 0.8)
            {
                continue;
            }

            if (IsChildPath(descendant.Path, parent.Path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsChildPath(string candidatePath, string parentPath)
    {
        string candidate;
        string parent;
        try
        {
            candidate = Path.GetFullPath(candidatePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            parent = Path.GetFullPath(parentPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return false;
        }

        return candidate.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith(parent + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcluded(string path, IReadOnlyList<string> excludedSegments)
    {
        if (excludedSegments.Count == 0)
        {
            return false;
        }

        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => excludedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static DirectoryScanSummary CalculateDirectorySummary(string directoryPath, AnalysisBuckets buckets)
    {
        long sizeBytes = 0;
        DateTimeOffset? latestWriteTime = null;

        foreach (var file in EnumerateFiles(directoryPath, recursive: true))
        {
            try
            {
                sizeBytes += file.Length;
                buckets.ScannedFileCount++;
                latestWriteTime = Max(latestWriteTime, new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return new DirectoryScanSummary(sizeBytes, latestWriteTime);
    }

    private static IReadOnlyList<string> GetDefaultUserControlledRoots()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new List<string>();

        AddIfProvided(roots, Path.GetTempPath());
        AddIfProvided(roots, Path.Combine(userProfile, "Downloads"));
        AddIfProvided(roots, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
        AddIfProvided(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfProvided(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
        AddIfProvided(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
        AddIfProvided(roots, Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
        AddIfProvided(roots, Path.Combine(userProfile, "source"));
        AddIfProvided(roots, Path.Combine(userProfile, "repos"));
        AddIfProvided(roots, Path.Combine(userProfile, "Projects"));
        AddIfProvided(roots, Path.Combine(userProfile, "dev"));
        AddIfProvided(roots, Path.Combine(userProfile, "workspace"));
        AddIfProvided(roots, Path.Combine(userProfile, "workspaces"));
        AddIfProvided(roots, Path.Combine(userProfile, "code"));
        AddIfProvided(roots, Path.Combine(userProfile, ".cache"));
        AddIfProvided(roots, Path.Combine(userProfile, ".nuget", "packages"));
        AddIfProvided(roots, Path.Combine(userProfile, ".gradle", "caches"));
        AddIfProvided(roots, Path.Combine(userProfile, ".cargo"));
        AddIfProvided(roots, Path.Combine(userProfile, ".m2", "repository"));
        AddIfProvided(roots, Path.Combine(userProfile, "go", "pkg", "mod", "cache", "download"));
        AddIfProvided(roots, Path.Combine(localAppData, "CrashDumps"));
        AddIfProvided(roots, Path.Combine(localAppData, "Microsoft", "Windows", "WER"));
        AddIfProvided(roots, Path.Combine(localAppData, "D3DSCache"));
        AddIfProvided(roots, Path.Combine(localAppData, "NVIDIA", "DXCache"));
        AddIfProvided(roots, Path.Combine(localAppData, "NVIDIA", "GLCache"));
        AddIfProvided(roots, Path.Combine(localAppData, "AMD", "DxCache"));
        AddIfProvided(roots, Path.Combine(localAppData, "AMD", "GLCache"));
        AddIfProvided(roots, Path.Combine(localAppData, "go-build"));
        AddIfProvided(roots, Path.Combine(localAppData, "npm-cache"));
        AddIfProvided(roots, Path.Combine(localAppData, "pnpm", "store"));
        AddIfProvided(roots, Path.Combine(localAppData, "Yarn", "Cache"));
        AddIfProvided(roots, Path.Combine(localAppData, "pip", "Cache"));
        AddIfProvided(roots, Path.Combine(localAppData, "deno"));

        return roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddIfProvided(List<string> roots, string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            roots.Add(path);
        }
    }

    private static DirectoryInfo? TryGetDirectoryInfo(string directoryPath)
    {
        try
        {
            return new DirectoryInfo(directoryPath);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (PathTooLongException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static IReadOnlyList<DirectoryInfo> EnumerateDirectories(string directoryPath)
    {
        try
        {
            return Directory
                .EnumerateDirectories(directoryPath)
                .Select(path => new DirectoryInfo(path))
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static IReadOnlyList<FileInfo> EnumerateFiles(string directoryPath, bool recursive = false)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            return Directory
                .EnumerateFiles(directoryPath, "*", options)
                .Select(path => new FileInfo(path))
                .ToArray();
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static void AddFileTypeStat(
        Dictionary<FileTypeStatKey, FileTypeStat> stats,
        string rootPath,
        string extension,
        long sizeBytes,
        DateTimeOffset lastWriteTime)
    {
        var key = new FileTypeStatKey(rootPath, extension);
        if (stats.TryGetValue(key, out var existing))
        {
            stats[key] = new FileTypeStat(
                existing.SizeBytes + sizeBytes,
                Max(existing.LastWriteTime, lastWriteTime));
            return;
        }

        stats[key] = new FileTypeStat(sizeBytes, lastWriteTime);
    }

    private static string NormalizeExtension(string extension)
    {
        return string.IsNullOrWhiteSpace(extension)
            ? "(no extension)"
            : extension.ToLowerInvariant();
    }

    private static string GetProjectDependencyExplanation(string folderName)
    {
        return folderName.ToLowerInvariant() switch
        {
            "node_modules" => "Node.js project dependencies. This folder can often be regenerated from package manifests, but it may be large and project-specific.",
            ".venv" or "venv" => "Python virtual environment. It can often be recreated from project dependency files, but local interpreter and package state may be useful.",
            "target" => "Build output commonly created by Rust, Maven, or similar project tooling. It may be regenerated, but builds may take longer afterward.",
            "bin" or "obj" => ".NET or compiled project build output. It is usually generated, but deleting it may affect an active workspace until the next build.",
            ".gradle" => "Gradle project cache or build state. It may be regenerated, but deleting it can make the next Gradle run slower.",
            ".next" or ".nuxt" or ".svelte-kit" or ".angular" => "Frontend framework build cache or output. It is usually generated, but deleting it may slow the next dev server or build.",
            ".parcel-cache" or ".turbo" => "Frontend build acceleration cache. It can usually be regenerated, but the next build may be slower.",
            ".cache" => "Project-local cache folder. It may be generated by build tools, but the owning project should be reviewed first.",
            ".pytest_cache" or ".mypy_cache" or ".ruff_cache" or ".tox" => "Python tooling cache or test environment state. It can often be regenerated by the tool that created it.",
            ".terraform" => "Terraform working directory. It may contain downloaded providers and module state; review carefully before removing.",
            "dist" or "build" or "out" => "Project build output folder. It is often generated, but may contain deliverables you intentionally kept.",
            "coverage" => "Test coverage output. It can usually be regenerated by rerunning tests.",
            "vendor" => "Project dependency folder. It may be recreated by a package manager, but some projects intentionally keep vendor content.",
            _ => "Project dependency or build-output folder. Review manually before deleting because it may affect a project workspace."
        };
    }

    private static DeepSpaceAdviceKey GetProjectDependencyAdviceKey(string folderName)
    {
        return folderName.ToLowerInvariant() switch
        {
            "node_modules" => DeepSpaceAdviceKey.NodeModules,
            ".venv" or "venv" => DeepSpaceAdviceKey.PythonVirtualEnvironment,
            "target" => DeepSpaceAdviceKey.TargetBuildOutput,
            "bin" or "obj" => DeepSpaceAdviceKey.DotNetBuildOutput,
            ".gradle" => DeepSpaceAdviceKey.GradleProjectCache,
            ".next" or ".nuxt" or ".svelte-kit" or ".angular" => DeepSpaceAdviceKey.FrontendFrameworkOutput,
            ".parcel-cache" or ".turbo" => DeepSpaceAdviceKey.FrontendBuildCache,
            ".cache" => DeepSpaceAdviceKey.ProjectLocalCache,
            ".pytest_cache" or ".mypy_cache" or ".ruff_cache" or ".tox" => DeepSpaceAdviceKey.PythonToolCache,
            ".terraform" => DeepSpaceAdviceKey.TerraformWorkingDirectory,
            "dist" or "build" or "out" => DeepSpaceAdviceKey.ProjectBuildOutput,
            "coverage" => DeepSpaceAdviceKey.CoverageOutput,
            "vendor" => DeepSpaceAdviceKey.VendorDependencies,
            _ => DeepSpaceAdviceKey.GenericProjectDependency
        };
    }

    private static string GetProjectDependencySuggestedAction(string folderName)
    {
        return folderName.ToLowerInvariant() switch
        {
            "node_modules" => "Open the project folder, confirm package manifests are present, then remove node_modules manually if the project can reinstall dependencies.",
            ".venv" or "venv" => "Confirm the Python environment is not needed or can be recreated, then remove it manually from the project folder if appropriate.",
            "target" => "Confirm no build artifacts need to be kept, then clean with the project tool or remove the folder manually.",
            "bin" or "obj" => "Prefer using the project build clean command. Remove manually only after confirming no generated output needs to be kept.",
            ".gradle" => "Prefer Gradle cleanup commands or remove manually only when the project is closed and the cache can be rebuilt.",
            ".next" or ".nuxt" or ".svelte-kit" or ".angular" => "Confirm the project is not actively running, then clean with the framework or remove the generated folder manually if it can be rebuilt.",
            ".parcel-cache" or ".turbo" => "Remove manually only after confirming the next build can rebuild the cache.",
            ".cache" => "Open the project first and identify the owning tool before removing this cache folder manually.",
            ".pytest_cache" or ".mypy_cache" or ".ruff_cache" or ".tox" => "Confirm the Python tool state is no longer needed, then remove manually or let the tool recreate it later.",
            ".terraform" => "Do not remove while Terraform is running. Confirm provider/module state can be restored before deleting manually.",
            "dist" or "build" or "out" => "Confirm no packaged deliverables need to be kept, then clean with the project tool or move/remove manually.",
            "coverage" => "Remove manually if you do not need the saved coverage report; it can usually be regenerated by rerunning tests.",
            "vendor" => "Confirm dependencies can be reinstalled and no vendored source was intentionally kept before removing manually.",
            _ => "Open the project folder, confirm it can be regenerated, then remove it manually if you no longer need the local build or dependency state."
        };
    }

    private static string GetLargeFileExplanation(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => "Large video file in a user-controlled location. Video files are often real personal data, not cache.",
            ".log" => "Large log file in a user-controlled location. Logs can be useful for diagnostics, but old logs may be disposable.",
            ".tmp" or ".temp" => "Large temporary-looking file in a user-controlled location. The name suggests it may be disposable, but ClearPilot cannot know its owner.",
            ".bak" or ".backup" => "Large backup file in a user-controlled location. It may be important if it is the only backup copy.",
            _ => "Large file in a user-controlled location. It may be important personal or project data."
        };
    }

    private static DeepSpaceAdviceKey GetLargeFileAdviceKey(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => DeepSpaceAdviceKey.VideoFile,
            ".log" => DeepSpaceAdviceKey.LogFile,
            ".tmp" or ".temp" => DeepSpaceAdviceKey.TemporaryFile,
            ".bak" or ".backup" => DeepSpaceAdviceKey.BackupFile,
            _ => DeepSpaceAdviceKey.GenericLargeFile
        };
    }

    private static string GetLargeFileSuggestedAction(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => "Review the video manually and consider moving it to external storage or an archive drive instead of deleting it.",
            ".log" => "Check which app created the log. If it is old and no longer needed for troubleshooting, remove it manually or rotate logs with the owning app.",
            ".tmp" or ".temp" => "Close related apps first, then verify the file is stale before removing it manually.",
            ".bak" or ".backup" => "Confirm a newer backup exists before deleting or moving this file.",
            _ => "Open the containing folder and decide manually whether to keep it, move it, archive it, or delete it."
        };
    }

    private static string GetOldArchiveOrInstallerExplanation(string extension)
    {
        return extension switch
        {
            ".iso" => "Old disk image. It may be an installer image, operating system image, or archived media that still matters.",
            ".msi" or ".exe" => "Old installer. It may be disposable if the app is installed and the installer can be downloaded again.",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => "Old archive. It may contain user files, project snapshots, exports, or installer payloads.",
            _ => "Old archive, disk image, or installer. It may be disposable, but only you can decide whether it is still needed."
        };
    }

    private static DeepSpaceAdviceKey GetOldArchiveOrInstallerAdviceKey(string extension)
    {
        return extension switch
        {
            ".iso" => DeepSpaceAdviceKey.DiskImage,
            ".msi" or ".exe" => DeepSpaceAdviceKey.Installer,
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => DeepSpaceAdviceKey.Archive,
            _ => DeepSpaceAdviceKey.GenericArchiveOrInstaller
        };
    }

    private static string GetOldArchiveOrInstallerSuggestedAction(string extension)
    {
        return extension switch
        {
            ".iso" => "Mount or inspect the image if unsure, then archive it elsewhere or remove it manually only after confirming it is no longer needed.",
            ".msi" or ".exe" => "Confirm the installer is not needed for repair, rollback, or offline install, then remove it manually if it can be redownloaded.",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => "Inspect the archive contents before deleting. If it is only a duplicate export or old package, move or remove it manually.",
            _ => "Open the containing folder, verify the app or archive is no longer needed, then remove it manually if appropriate."
        };
    }

    private static string GetFileTypeSummaryExplanation(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => $"Video files ({extension}) account for significant space in this scan root.",
            ".iso" => "Disk images (.iso) account for significant space in this scan root.",
            ".msi" or ".exe" => $"Installer files ({extension}) account for significant space in this scan root.",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => $"Archive files ({extension}) account for significant space in this scan root.",
            ".log" => "Log files (.log) account for significant space in this scan root.",
            ".tmp" or ".temp" => $"Temporary-looking files ({extension}) account for significant space in this scan root.",
            "(no extension)" => "Files without extensions account for significant space in this scan root.",
            _ => $"Files with extension {extension} account for significant space in this scan root."
        };
    }

    private static DeepSpaceAdviceKey GetFileTypeSummaryAdviceKey(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => DeepSpaceAdviceKey.VideoFileTypeSummary,
            ".iso" => DeepSpaceAdviceKey.DiskImageFileTypeSummary,
            ".msi" or ".exe" => DeepSpaceAdviceKey.InstallerFileTypeSummary,
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => DeepSpaceAdviceKey.ArchiveFileTypeSummary,
            ".log" => DeepSpaceAdviceKey.LogFileTypeSummary,
            ".tmp" or ".temp" => DeepSpaceAdviceKey.TemporaryFileTypeSummary,
            "(no extension)" => DeepSpaceAdviceKey.NoExtensionFileTypeSummary,
            _ => DeepSpaceAdviceKey.GenericFileTypeSummary
        };
    }

    private static string GetFileTypeSummarySuggestedAction(string extension)
    {
        return extension switch
        {
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => "Filter to large files and review videos individually; moving them to external storage is usually safer than deleting.",
            ".iso" => "Review disk images individually and keep only images that are still needed for install, recovery, or archival purposes.",
            ".msi" or ".exe" => "Review installers individually and remove only those that can be downloaded again or are no longer needed.",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".tgz" => "Inspect archives before deleting because they may contain unique exports or project snapshots.",
            ".log" => "Identify the owning app first, then use its log rotation or remove old logs manually when they are no longer useful.",
            ".tmp" or ".temp" => "Close related apps, then review stale temporary-looking files manually before removal.",
            "(no extension)" => "Open the scan root and review these files manually; files without extensions may still be important app or project data.",
            _ => "Open the scan root and review this file type manually; ClearPilot only reports the aggregate size."
        };
    }

    private static DateTimeOffset? Max(DateTimeOffset? left, DateTimeOffset? right)
    {
        if (left is null)
        {
            return right;
        }

        if (right is null)
        {
            return left;
        }

        return left > right ? left : right;
    }

    private sealed class AnalysisBuckets
    {
        public List<DeepSpaceItem> Items { get; } = [];

        public Dictionary<FileTypeStatKey, FileTypeStat> FileTypeStats { get; } = [];

        public int ScannedRootCount { get; set; }

        public int ScannedDirectoryCount { get; set; }

        public int ScannedFileCount { get; set; }
    }

    private readonly record struct DirectoryScanSummary(long SizeBytes, DateTimeOffset? LastWriteTime)
    {
        public static DirectoryScanSummary Empty { get; } = new(0, null);
    }

    private readonly record struct FileTypeStatKey(string RootPath, string Extension);

    private readonly record struct FileTypeStat(long SizeBytes, DateTimeOffset? LastWriteTime);
}
