namespace ClearPilot.Core.Safety;

public sealed class ProtectedPathPolicy
{
    private readonly IReadOnlyList<string> blockedRoots;

    public ProtectedPathPolicy(IEnumerable<string> blockedRoots)
    {
        this.blockedRoots = blockedRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ProtectedPathPolicy CreateDefault()
    {
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        return new ProtectedPathPolicy(
        [
            Path.Combine(windows, "System32"),
            programFiles,
            programFilesX86,
            @"C:\Windows\System32",
            @"C:\Program Files",
            @"C:\Program Files (x86)"
        ]);
    }

    public bool IsBlocked(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return true;
        }

        var normalized = Normalize(candidatePath);
        return blockedRoots.Any(root => IsSameOrChild(normalized, root));
    }

    private static bool IsSameOrChild(string path, string root)
    {
        if (string.Equals(path, root, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
