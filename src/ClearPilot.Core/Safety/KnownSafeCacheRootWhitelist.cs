using ClearPilot.Core.Rules;

namespace ClearPilot.Core.Safety;

public sealed class KnownSafeCacheRootWhitelist
{
    private readonly IReadOnlyList<string> knownSafeRoots;

    public KnownSafeCacheRootWhitelist(IEnumerable<string> knownSafeRoots)
    {
        this.knownSafeRoots = knownSafeRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Normalize)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IReadOnlyList<string> Roots => knownSafeRoots;

    public static KnownSafeCacheRootWhitelist CreateFromRules(IEnumerable<CleanupRule> rules)
    {
        var roots = rules
            .SelectMany(rule => rule.RootPaths)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new KnownSafeCacheRootWhitelist(roots);
    }

    public bool IsAllowed(string canonicalPath)
    {
        if (string.IsNullOrWhiteSpace(canonicalPath))
        {
            return false;
        }

        string normalizedPath;
        try
        {
            normalizedPath = Normalize(canonicalPath);
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }

        return knownSafeRoots.Any(root => IsSameOrChild(normalizedPath, root));
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
