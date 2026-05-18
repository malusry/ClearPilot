namespace ClearPilot.Core.Safety;

public static class ClearPilotInternalPathPolicy
{
    private static readonly string[] InternalDirectoryNames =
    [
        "ClearPilot"
    ];

    private static readonly string[] InternalFileSuffixes =
    [
        "-QuickSafeClean.json",
        "-RecommendedCleanup.json"
    ];

    private static readonly string[] InternalFilePrefixes =
    [
        "ClearPilot-DeepSpaceAnalysis-"
    ];

    public static bool IsInternalArtifact(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return HasInternalDirectorySegment(path) || HasInternalFileName(path);
    }

    private static bool HasInternalDirectorySegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        return segments.Any(segment => InternalDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase));
    }

    private static bool HasInternalFileName(string path)
    {
        var fileName = Path.GetFileName(path);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        return InternalFileSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            || InternalFilePrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
