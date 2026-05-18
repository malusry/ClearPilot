using ClearPilot.Core.Rules;
using ClearPilot.Core.Safety;

namespace ClearPilot.Core.Scanning;

public sealed class CleanupFileScanner
{
    private readonly ProtectedPathPolicy protectedPathPolicy;

    public CleanupFileScanner(ProtectedPathPolicy protectedPathPolicy)
    {
        this.protectedPathPolicy = protectedPathPolicy;
    }

    public IReadOnlyList<CleanupFileCandidate> ScanFiles(CleanupRule rule, string root, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(root)
            || protectedPathPolicy.IsBlocked(root)
            || ClearPilotInternalPathPolicy.IsInternalArtifact(root)
            || !Directory.Exists(root))
        {
            return [];
        }

        var files = new List<CleanupFileCandidate>();
        foreach (var filePath in EnumerateFiles(root, rule.Recursive))
        {
            if (protectedPathPolicy.IsBlocked(filePath)
                || ClearPilotInternalPathPolicy.IsInternalArtifact(filePath)
                || IsExcluded(filePath, rule.ExcludePathSegments))
            {
                continue;
            }

            var fileName = Path.GetFileName(filePath);
            if (!MatchesAnyPattern(fileName, rule.IncludeFilePatterns))
            {
                continue;
            }

            var fileInfo = TryGetFileInfo(filePath);
            if (fileInfo is null)
            {
                continue;
            }

            if (rule.MinimumAge is not null && now - new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero) < rule.MinimumAge)
            {
                continue;
            }

            long sizeBytes;
            try
            {
                sizeBytes = fileInfo.Length;
            }
            catch (IOException)
            {
                continue;
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }

            files.Add(new CleanupFileCandidate(rule, root, filePath, sizeBytes));
        }

        return files;
    }

    private static FileInfo? TryGetFileInfo(string filePath)
    {
        try
        {
            return new FileInfo(filePath);
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

    private static IEnumerable<string> EnumerateFiles(string root, bool recursive)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            return Directory.EnumerateFiles(root, "*", options).ToArray();
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

    private static bool IsExcluded(string filePath, IReadOnlyList<string> excludedSegments)
    {
        if (excludedSegments.Count == 0)
        {
            return false;
        }

        var segments = filePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => excludedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool MatchesAnyPattern(string fileName, IReadOnlyList<string> patterns)
    {
        return patterns.Count == 0
            || patterns.Any(pattern => WildcardMatcher.IsMatch(fileName, pattern));
    }
}
