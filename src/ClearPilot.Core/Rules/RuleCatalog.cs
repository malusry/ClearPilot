using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Rules;

public static class RuleCatalog
{
    public static IReadOnlyList<CleanupRule> CreateDefault()
    {
        return CreateDefault(EnvironmentPaths.Current());
    }

    public static IReadOnlyList<CleanupRule> CreateDefault(EnvironmentPaths paths)
    {
        var rules = new List<CleanupRule>();

        AddIfPathProvided(rules, paths.UserTemp, root => new CleanupRule(
            "cp.s0.user-temp",
            "Current user temporary files",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*"],
            ["ClearPilot", "ClearPilot.Tests"],
            TimeSpan.FromDays(1),
            "Temporary files owned by the current user. Recently modified files are skipped."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "CrashDumps"), root => new CleanupRule(
            "cp.s0.user-crash-dumps",
            "Current user crash dumps",
            RiskLevel.S0VeryLowRisk,
            [root],
            ["*.dmp", "*.mdmp"],
            [],
            TimeSpan.FromDays(7),
            "Old crash dump files from user-mode applications. These are normally only useful for debugging recent crashes."));

        AddIfAnyRootProvided(rules, GetWindowsErrorReportRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.windows-error-reports",
            "Windows Error Reporting files",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(14),
            "Old user-mode Windows Error Reporting files. They are mainly useful for diagnostics and can be recreated by future crashes."));

        AddIfAnyRootProvided(rules, GetDirectXShaderCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.directx-shader-cache",
            "DirectX and GPU shader caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Graphics drivers and DirectX can recreate shader caches. Cleaning them may cause slower first launches or stutter while shaders rebuild."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "NuGet", "v3-cache"), root => new CleanupRule(
            "cp.s1.nuget-http-cache",
            "NuGet HTTP cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "NuGet can recreate this download cache. Cleaning it may require packages to be downloaded again."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "npm-cache"), root => new CleanupRule(
            "cp.s1.npm-cache",
            "npm cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "npm can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "Yarn", "Cache"), root => new CleanupRule(
            "cp.s1.yarn-cache",
            "Yarn cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Yarn can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "pnpm", "store"), root => new CleanupRule(
            "cp.s1.pnpm-store",
            "pnpm store cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "pnpm can recreate this package store. Cleaning it may require packages to be downloaded again."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "pip", "Cache"), root => new CleanupRule(
            "cp.s1.pip-cache",
            "pip cache",
            RiskLevel.S1LowRisk,
            [
                root,
                Path.Combine(paths.UserProfile, ".cache", "pip")
            ],
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "pip can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "Composer"), root => new CleanupRule(
            "cp.s1.composer-cache",
            "Composer cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            ["vendor"],
            TimeSpan.FromDays(1),
            "Composer can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetGoCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.go-cache",
            "Go build and module cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Go can recreate build and module download caches. Cleaning them may slow future builds or require module downloads."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".cargo", "registry", "cache"), root => new CleanupRule(
            "cp.s1.cargo-registry-cache",
            "Cargo registry cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Cargo can recreate downloaded registry package archives. Cleaning them may slow future builds."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".cargo", "git", "db"), root => new CleanupRule(
            "cp.s1.cargo-git-cache",
            "Cargo git cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Cargo can recreate git dependency caches. Cleaning them may slow future builds."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".gradle", "caches", "modules-2", "files-2.1"), root => new CleanupRule(
            "cp.s1.gradle-dependency-cache",
            "Gradle dependency cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Gradle can recreate dependency caches. Cleaning them may slow future builds."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".m2", "repository"), root => new CleanupRule(
            "cp.s1.maven-repository-cache",
            "Maven local repository cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(30),
            "Maven can recreate downloaded dependencies. Cleaning older cached artifacts may slow future builds or require downloads."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".nuget", "packages"), root => new CleanupRule(
            "cp.s1.nuget-global-packages",
            "NuGet global packages cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*.nupkg"],
            [],
            TimeSpan.FromDays(30),
            "NuGet package archives can be downloaded again. Cleaning them may slow future builds."));

        AddIfAnyRootProvided(rules, GetDenoCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.deno-cache",
            "Deno cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Deno can recreate dependency and transpile caches. Cleaning them may require downloads or recompilation."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".bun", "install", "cache"), root => new CleanupRule(
            "cp.s1.bun-install-cache",
            "Bun install cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Bun can recreate package install cache entries. Cleaning them may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetPythonBytecodeRoots(paths), roots => new CleanupRule(
            "cp.s1.python-bytecode-cache",
            "Python bytecode caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*.pyc", "*.pyo"],
            [".venv", "venv", "node_modules", "vendor"],
            TimeSpan.FromDays(7),
            "Python can recreate bytecode cache files. Source files and virtual environments are excluded from this rule."));

        AddIfAnyRootProvided(rules, GetVsCodeCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.vscode-cache",
            "Visual Studio Code cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "VS Code can recreate these UI caches. Extensions, settings, and workspace storage are excluded."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "JetBrains"), root => new CleanupRule(
            "cp.s1.jetbrains-cache",
            "JetBrains IDE caches",
            RiskLevel.S1LowRisk,
            GetJetBrainsCacheRoots(root),
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "JetBrains IDEs can recreate cache directories. Configurations, plugins, and projects are excluded."));

        AddIfAnyRootProvided(rules, GetElectronAppCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.electron-app-ui-cache",
            "Electron app UI caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Electron apps can recreate Cache, Code Cache, and GPUCache folders. Settings, local storage, sessions, and databases are excluded."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "Microsoft", "Windows", "Explorer"), root => new CleanupRule(
            "cp.s1.windows-thumbnail-cache",
            "Windows thumbnail cache",
            RiskLevel.S1LowRisk,
            [root],
            ["thumbcache_*.db", "iconcache_*.db"],
            [],
            TimeSpan.FromDays(1),
            "Windows can recreate thumbnail and icon cache databases. Folder thumbnails may regenerate after cleanup."));

        AddIfAnyRootProvided(rules, GetChromiumCacheRoots(Path.Combine(paths.LocalAppData, "Microsoft", "Edge", "User Data")), roots => new CleanupRule(
            "cp.s1.edge-cache",
            "Microsoft Edge cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetChromiumCacheRoots(Path.Combine(paths.LocalAppData, "Google", "Chrome", "User Data")), roots => new CleanupRule(
            "cp.s1.chrome-cache",
            "Google Chrome cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetChromiumCacheRoots(Path.Combine(paths.LocalAppData, "BraveSoftware", "Brave-Browser", "User Data")), roots => new CleanupRule(
            "cp.s1.brave-cache",
            "Brave browser cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetChromiumCacheRoots(Path.Combine(paths.LocalAppData, "Chromium", "User Data")), roots => new CleanupRule(
            "cp.s1.chromium-cache",
            "Chromium browser cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetChromiumCacheRoots(Path.Combine(paths.LocalAppData, "Vivaldi", "User Data")), roots => new CleanupRule(
            "cp.s1.vivaldi-cache",
            "Vivaldi browser cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetSingleProfileChromiumCacheRoots(Path.Combine(paths.LocalAppData, "Opera Software", "Opera Stable")), roots => new CleanupRule(
            "cp.s1.opera-cache",
            "Opera browser cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Browser cache files can be recreated. Identity, history, bookmark, cookie, password, and session data are excluded."));

        AddIfAnyRootProvided(rules, GetFirefoxCacheRoots(Path.Combine(paths.LocalAppData, "Mozilla", "Firefox", "Profiles")), roots => new CleanupRule(
            "cp.s1.firefox-cache",
            "Firefox cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(1),
            "Firefox can recreate these cache folders. Profiles, cookies, logins, bookmarks, history, and sessions are excluded."));

        return rules;
    }

    private static void AddIfPathProvided(List<CleanupRule> rules, string path, Func<string, CleanupRule> factory)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            rules.Add(factory(path));
        }
    }

    private static void AddIfAnyRootProvided(List<CleanupRule> rules, IReadOnlyList<string> roots, Func<IReadOnlyList<string>, CleanupRule> factory)
    {
        var providedRoots = roots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (providedRoots.Length > 0)
        {
            rules.Add(factory(providedRoots));
        }
    }

    private static IReadOnlyList<string> GetChromiumCacheRoots(string userDataRoot)
    {
        var profileRoots = GetChromiumProfileRoots(userDataRoot);
        var cacheRoots = new List<string>();

        foreach (var profileRoot in profileRoots)
        {
            cacheRoots.AddRange(GetSingleProfileChromiumCacheRoots(profileRoot));
        }

        return cacheRoots;
    }

    private static IReadOnlyList<string> GetSingleProfileChromiumCacheRoots(string profileRoot)
    {
        return
        [
            Path.Combine(profileRoot, "Cache"),
            Path.Combine(profileRoot, "Code Cache"),
            Path.Combine(profileRoot, "GPUCache")
        ];
    }

    private static IReadOnlyList<string> GetChromiumProfileRoots(string userDataRoot)
    {
        if (Directory.Exists(userDataRoot))
        {
            try
            {
                var discoveredProfiles = Directory
                    .EnumerateDirectories(userDataRoot)
                    .Where(IsChromiumProfileDirectory)
                    .ToArray();

                if (discoveredProfiles.Length > 0)
                {
                    return discoveredProfiles;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return [Path.Combine(userDataRoot, "Default")];
    }

    private static bool IsChromiumProfileDirectory(string path)
    {
        var name = Path.GetFileName(path);
        return string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "Guest Profile", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetGoCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "go-build"),
            Path.Combine(paths.UserProfile, "go", "pkg", "mod", "cache", "download")
        ];
    }

    private static IReadOnlyList<string> GetVsCodeCacheRoots(string localAppData)
    {
        var roots = new List<string>();
        foreach (var appName in new[] { "Code", "Code - Insiders", "VSCodium" })
        {
            var appRoot = Path.Combine(localAppData, "Programs", appName);
            roots.Add(Path.Combine(appRoot, "Cache"));
            roots.Add(Path.Combine(appRoot, "Code Cache"));
            roots.Add(Path.Combine(appRoot, "GPUCache"));

            var userDataRoot = Path.Combine(localAppData, appName);
            roots.Add(Path.Combine(userDataRoot, "Cache"));
            roots.Add(Path.Combine(userDataRoot, "Code Cache"));
            roots.Add(Path.Combine(userDataRoot, "GPUCache"));
        }

        return roots;
    }

    private static IReadOnlyList<string> GetElectronAppCacheRoots(string localAppData)
    {
        var roots = new List<string>();
        foreach (var appRoot in new[]
        {
            Path.Combine(localAppData, "Discord"),
            Path.Combine(localAppData, "DiscordCanary"),
            Path.Combine(localAppData, "Slack"),
            Path.Combine(localAppData, "Microsoft", "Teams"),
            Path.Combine(localAppData, "Microsoft", "TeamsMeetingAddin"),
            Path.Combine(localAppData, "Microsoft", "MSTeams")
        })
        {
            roots.AddRange(GetSingleProfileChromiumCacheRoots(appRoot));
        }

        return roots;
    }

    private static IReadOnlyList<string> GetDirectXShaderCacheRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "D3DSCache"),
            Path.Combine(localAppData, "NVIDIA", "DXCache"),
            Path.Combine(localAppData, "NVIDIA", "GLCache"),
            Path.Combine(localAppData, "NVIDIA", "ComputeCache"),
            Path.Combine(localAppData, "AMD", "DxCache"),
            Path.Combine(localAppData, "AMD", "GLCache"),
            Path.Combine(localAppData, "Intel", "ShaderCache")
        ];
    }

    private static IReadOnlyList<string> GetWindowsErrorReportRoots(string localAppData)
    {
        var werRoot = Path.Combine(localAppData, "Microsoft", "Windows", "WER");
        return
        [
            Path.Combine(werRoot, "ReportArchive"),
            Path.Combine(werRoot, "ReportQueue"),
            Path.Combine(werRoot, "Temp")
        ];
    }

    private static IReadOnlyList<string> GetPythonBytecodeRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.UserProfile, "source"),
            Path.Combine(paths.UserProfile, "repos"),
            Path.Combine(paths.UserProfile, "Projects"),
            Path.Combine(paths.UserProfile, "dev"),
            Path.Combine(paths.UserProfile, "workspace"),
            Path.Combine(paths.UserProfile, "code")
        ];
    }

    private static IReadOnlyList<string> GetDenoCacheRoots(string localAppData)
    {
        var denoRoot = Path.Combine(localAppData, "deno");
        return
        [
            Path.Combine(denoRoot, "deps"),
            Path.Combine(denoRoot, "gen"),
            Path.Combine(denoRoot, "npm")
        ];
    }

    private static IReadOnlyList<string> GetFirefoxCacheRoots(string profilesRoot)
    {
        if (Directory.Exists(profilesRoot))
        {
            try
            {
                var discoveredProfiles = Directory
                    .EnumerateDirectories(profilesRoot)
                    .ToArray();

                if (discoveredProfiles.Length > 0)
                {
                    return discoveredProfiles
                        .SelectMany(profileRoot => new[]
                        {
                            Path.Combine(profileRoot, "cache2"),
                            Path.Combine(profileRoot, "startupCache")
                        })
                        .ToArray();
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return
        [
            Path.Combine(profilesRoot, "default", "cache2"),
            Path.Combine(profilesRoot, "default", "startupCache")
        ];
    }

    private static IReadOnlyList<string> GetJetBrainsCacheRoots(string jetBrainsRoot)
    {
        if (Directory.Exists(jetBrainsRoot))
        {
            try
            {
                var discoveredCaches = Directory
                    .EnumerateDirectories(jetBrainsRoot, "*", SearchOption.TopDirectoryOnly)
                    .Select(directory => Path.Combine(directory, "caches"))
                    .ToArray();

                if (discoveredCaches.Length > 0)
                {
                    return discoveredCaches;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return [Path.Combine(jetBrainsRoot, "caches")];
    }
}
