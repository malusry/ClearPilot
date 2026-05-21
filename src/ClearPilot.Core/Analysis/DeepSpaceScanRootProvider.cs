namespace ClearPilot.Core.Analysis;

public sealed class DeepSpaceScanRootProvider
{
    private readonly IDeepSpaceSpecialFolderProvider specialFolderProvider;

    public DeepSpaceScanRootProvider(IDeepSpaceSpecialFolderProvider? specialFolderProvider = null)
    {
        this.specialFolderProvider = specialFolderProvider ?? new UserSpecialFolderProvider();
    }

    public IReadOnlyList<string> GetUserControlledAnalysisRoots()
    {
        var roots = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        AddIfExistingDirectoryOrFile(roots, Path.GetTempPath());
        AddIfExistingDirectoryOrFile(roots, specialFolderProvider.TryGetDownloadsPath());
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "source"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "repos"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "Projects"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "dev"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "workspace"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "workspaces"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "code"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, ".cache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, ".nuget", "packages"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, ".gradle", "caches"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, ".cargo"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, ".m2", "repository"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(userProfile, "go", "pkg", "mod", "cache", "download"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "CrashDumps"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "Microsoft", "Windows", "WER"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "D3DSCache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "NVIDIA", "DXCache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "NVIDIA", "GLCache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "AMD", "DxCache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "AMD", "GLCache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "go-build"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "npm-cache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "pnpm", "store"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "Yarn", "Cache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "pip", "Cache"));
        AddIfExistingDirectoryOrFile(roots, Path.Combine(localAppData, "deno"));

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void AddIfExistingDirectoryOrFile(List<string> roots, string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && (Directory.Exists(path) || File.Exists(path)))
        {
            roots.Add(path);
        }
    }
}

