namespace ClearPilot.Core.Safety;

public class PathSafetyEngine
{
    private static readonly string[] RegistryPrefixes =
    [
        "HKLM:",
        "HKCU:",
        "HKCR:",
        "HKU:",
        "HKEY_LOCAL_MACHINE",
        "HKEY_CURRENT_USER",
        "HKEY_CLASSES_ROOT",
        "HKEY_USERS"
    ];

    private static readonly string[] HardDeniedSegments =
    [
        "Cookies",
        "Login Data",
        "Web Data",
        "History",
        "Bookmarks",
        "Sessions",
        "Session Storage",
        "Local Storage",
        "IndexedDB",
        "Saved Games",
        "steamapps",
        "workshop",
        "downloading",
        "manifest",
        "manifests",
        "librarymetadata",
        "savegames",
        "saves",
        "mods",
        "screenshots",
        "recordings",
        "userdata",
        "account",
        "accounts",
        "token",
        "tokens",
        "entitlement",
        "entitlements",
        "quarantine",
        "ProtectionHistory",
        "Scans",
        "Engine",
        "Platform",
        "Signatures",
        "Definition Updates",
        "windows defender",
        "microsoft defender",
        "security intelligence"
    ];

    private readonly ProtectedPathPolicy protectedPathPolicy;

    public PathSafetyEngine(ProtectedPathPolicy protectedPathPolicy)
    {
        this.protectedPathPolicy = protectedPathPolicy;
    }

    public virtual PathSafetyDecision ValidateRoot(string rootPath, KnownSafeCacheRootWhitelist whitelist)
    {
        return ValidatePath(rootPath, rootPath, whitelist, requireExistingPath: true);
    }

    public virtual PathSafetyDecision ValidateCandidate(string candidatePath, string cleanupRoot, KnownSafeCacheRootWhitelist whitelist)
    {
        return ValidatePath(candidatePath, cleanupRoot, whitelist, requireExistingPath: true);
    }

    public virtual PathSafetyDecision RevalidateCandidate(
        string candidatePath,
        string cleanupRoot,
        KnownSafeCacheRootWhitelist whitelist,
        string expectedCanonicalPath)
    {
        var decision = ValidatePath(candidatePath, cleanupRoot, whitelist, requireExistingPath: true);
        if (!decision.IsSafe)
        {
            return decision;
        }

        if (!string.Equals(decision.CanonicalPath, expectedCanonicalPath, StringComparison.OrdinalIgnoreCase))
        {
            return decision with
            {
                IsSafe = false,
                ResultCode = "CanonicalPathChanged",
                Reason = "Path canonicalization changed between scan and delete."
            };
        }

        return decision;
    }

    private PathSafetyDecision ValidatePath(
        string candidatePath,
        string cleanupRoot,
        KnownSafeCacheRootWhitelist whitelist,
        bool requireExistingPath)
    {
        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "EmptyPath", "Path is empty.");
        }

        if (LooksLikeRegistryPath(candidatePath))
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, false, "RegistryTarget", "Registry targets are blocked.");
        }

        if (!Path.IsPathRooted(candidatePath))
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "RelativePath", "Path must be absolute.");
        }

        if (ContainsTraversalSegment(candidatePath))
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "PathTraversal", "Path traversal segments are not allowed.");
        }

        string canonicalPath;
        string canonicalCleanupRoot;

        try
        {
            canonicalPath = Normalize(candidatePath);
            canonicalCleanupRoot = Normalize(cleanupRoot);
        }
        catch (ArgumentException ex)
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "InvalidPath", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "InvalidPath", ex.Message);
        }
        catch (PathTooLongException ex)
        {
            return PathSafetyDecision.Blocked(candidatePath, null, false, true, "PathTooLong", ex.Message);
        }

        if (IsUncPath(canonicalPath))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, true, "UncPath", "UNC and network paths are blocked.");
        }

        if (IsDriveRoot(canonicalPath))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, true, "DriveRoot", "Drive roots are blocked.");
        }

        if (!IsSameOrChild(canonicalPath, canonicalCleanupRoot))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, true, "OutsideCleanupRoot", "Path is outside the cleanup root.");
        }

        var denyDecision = protectedPathPolicy.Evaluate(canonicalPath);
        if (denyDecision.IsBlocked)
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, false, "ProtectedRoot", denyDecision.Reason);
        }

        if (HasHardDeniedSegment(canonicalPath))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, false, "HardDeniedTarget", "Path matches a hard-denied target segment.");
        }

        var allowlistAllowed = whitelist.IsAllowed(canonicalPath);
        if (!allowlistAllowed)
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, false, true, "NotInKnownSafeCacheRoot", "Path is outside known-safe cache roots.");
        }

        if (requireExistingPath && !File.Exists(canonicalPath) && !Directory.Exists(canonicalPath))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, true, true, "MissingPath", "Path no longer exists.");
        }

        if (HasReparsePoint(canonicalPath))
        {
            return PathSafetyDecision.Blocked(candidatePath, canonicalPath, true, true, "ReparsePoint", "Symlinks, junctions, and reparse points are blocked.");
        }

        return PathSafetyDecision.Allowed(candidatePath, canonicalPath);
    }

    private static bool HasReparsePoint(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static bool ContainsTraversalSegment(string path)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Contains("..", StringComparer.Ordinal);
    }

    private static bool IsDriveRoot(string path)
    {
        if (path.Length == 2 && path[1] == ':')
        {
            return true;
        }

        if (path.Length == 3
            && path[1] == ':'
            && (path[2] == Path.DirectorySeparatorChar || path[2] == Path.AltDirectorySeparatorChar))
        {
            return true;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var normalizedRoot = Normalize(root);
        return string.Equals(path, normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUncPath(string path)
    {
        return path.StartsWith(@"\\", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
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

    private static bool LooksLikeRegistryPath(string candidatePath)
    {
        return RegistryPrefixes.Any(prefix =>
            candidatePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasHardDeniedSegment(string canonicalPath)
    {
        var segments = canonicalPath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Any(segment => HardDeniedSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
        {
            return true;
        }

        var fileName = Path.GetFileName(canonicalPath);
        if (fileName.StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".acf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(fileName, "libraryfolders.vdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (canonicalPath.Contains(Path.Combine("Battle.net", "Data") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || canonicalPath.EndsWith(Path.Combine("Battle.net", "Data"), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

public sealed record PathSafetyDecision(
    bool IsSafe,
    string OriginalPath,
    string? CanonicalPath,
    bool AllowlistAllowed,
    bool DenylistAllowed,
    string ResultCode,
    string? Reason)
{
    public static PathSafetyDecision Allowed(string originalPath, string canonicalPath)
    {
        return new PathSafetyDecision(
            true,
            originalPath,
            canonicalPath,
            true,
            true,
            "Allowed",
            null);
    }

    public static PathSafetyDecision Blocked(
        string originalPath,
        string? canonicalPath,
        bool allowlistAllowed,
        bool denylistAllowed,
        string resultCode,
        string? reason)
    {
        return new PathSafetyDecision(
            false,
            originalPath,
            canonicalPath,
            allowlistAllowed,
            denylistAllowed,
            resultCode,
            reason);
    }
}
