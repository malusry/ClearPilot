namespace ClearPilot.Core.Safety;

public sealed class ProtectedPathPolicy
{
    private readonly IReadOnlyList<string> blockedRecursiveRoots;
    private readonly IReadOnlyList<string> blockedExactRoots;

    public ProtectedPathPolicy(IEnumerable<string> blockedRoots)
        : this(blockedRoots, [])
    {
    }

    public ProtectedPathPolicy(IEnumerable<string> blockedRecursiveRoots, IEnumerable<string> blockedExactRoots)
    {
        this.blockedRecursiveRoots = blockedRecursiveRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        this.blockedExactRoots = blockedExactRoots
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
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(userProfile, "Downloads");
        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        var videos = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        var savedGames = Path.Combine(userProfile, "Saved Games");

        return new ProtectedPathPolicy(
        [
            Path.Combine(windows, "System32"),
            Path.Combine(windows, "SysWOW64"),
            Path.Combine(windows, "WinSxS"),
            Path.Combine(windows, "Installer"),
            Path.Combine(windows, "servicing"),
            @"C:\Windows\System32",
            @"C:\Windows\SysWOW64",
            @"C:\Windows\WinSxS",
            @"C:\Windows\Installer",
            @"C:\Windows\servicing"
        ],
        [
            windows,
            programFiles,
            programFilesX86,
            programData,
            userProfile,
            desktop,
            documents,
            downloads,
            pictures,
            videos,
            music,
            savedGames,
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Users",
            @"C:\Users\Default",
            @"C:\Users\Public"
        ]);
    }

    public bool IsBlocked(string candidatePath)
    {
        return Evaluate(candidatePath).IsBlocked;
    }

    public ProtectedPathDecision Evaluate(string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return new ProtectedPathDecision(true, null, false, "Empty path");
        }

        var normalized = Normalize(candidatePath);

        var exactMatch = blockedExactRoots.FirstOrDefault(root =>
            string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase));
        if (exactMatch is not null)
        {
            return new ProtectedPathDecision(true, exactMatch, false, "Blocked exact root");
        }

        var recursiveMatch = blockedRecursiveRoots.FirstOrDefault(root => IsSameOrChild(normalized, root));
        if (recursiveMatch is not null)
        {
            return new ProtectedPathDecision(true, recursiveMatch, true, "Blocked protected subtree");
        }

        return new ProtectedPathDecision(false, null, false, null);
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

public sealed record ProtectedPathDecision(
    bool IsBlocked,
    string? BlockingRoot,
    bool IsRecursiveBlock,
    string? Reason);
