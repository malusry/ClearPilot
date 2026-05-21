using ClearPilot.Core.Cleanup;

namespace ClearPilot.Core.Rules;

public static class RuleCatalog
{
    private static readonly IReadOnlyList<string> AppProfileIdentityAndSessionExclusions =
    [
        "Local Storage",
        "Session Storage",
        "IndexedDB",
        "Cookies",
        "Login Data",
        "Web Data",
        "History",
        "Bookmarks",
        "account",
        "accounts",
        "auth",
        "token",
        "tokens",
        "credential",
        "credentials",
        "profile",
        "profiles"
    ];

    private static readonly IReadOnlyList<string> DiscordProcessGuardNames =
    [
        "Discord.exe",
        "DiscordCanary.exe",
        "DiscordPTB.exe"
    ];

    private static readonly IReadOnlyList<string> SlackProcessGuardNames =
    [
        "slack.exe",
        "Slack.exe"
    ];

    private static readonly IReadOnlyList<string> TeamsProcessGuardNames =
    [
        "Teams.exe",
        "ms-teams.exe",
        "MSTeams.exe"
    ];

    private static readonly IReadOnlyList<string> VsCodeProcessGuardNames =
    [
        "Code.exe",
        "Code - Insiders.exe",
        "VSCodium.exe"
    ];

    private static readonly IReadOnlyList<string> JetBrainsProcessGuardNames =
    [
        "idea64.exe", "idea.exe",
        "pycharm64.exe", "pycharm.exe",
        "webstorm64.exe", "webstorm.exe",
        "rider64.exe", "rider.exe",
        "clion64.exe", "clion.exe",
        "datagrip64.exe", "datagrip.exe",
        "goland64.exe", "goland.exe",
        "phpstorm64.exe", "phpstorm.exe",
        "rubymine64.exe", "rubymine.exe",
        "dataspell64.exe", "dataspell.exe"
    ];

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

        AddIfPathProvided(rules, GetWindowsTempRoot(paths.Windows), root => new CleanupRule(
            "cp.s1.windows-temp",
            "Windows temporary files (accessible scope)",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            ["ClearPilot", "ClearPilot.Tests"],
            TimeSpan.FromDays(1),
            "Windows temporary files in accessible non-admin scope. Cleaning may remove temporary installer or diagnostics leftovers."));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "CrashDumps"), root => new CleanupRule(
            "cp.s1.user-crash-dumps",
            "Current user crash dumps",
            RiskLevel.S1LowRisk,
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

        AddIfAnyRootProvided(rules, GetInternetCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.windows-inet-cache",
            "Windows internet temporary cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [
                "Cookies",
                "History",
                "Sessions",
                "Login Data",
                "Web Data",
                "Bookmarks",
                "Local Storage",
                "IndexedDB",
                "Session Storage",
                "User Data",
                "Profiles"
            ],
            TimeSpan.FromDays(1),
            "Temporary web cache files under Windows internet cache directories. Identity, session, and profile data are excluded."));

        AddIfAnyRootProvided(rules, GetMicrosoftStoreLocalCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.msstore-localcache",
            "Microsoft Store package LocalCache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [
                "LocalState",
                "RoamingState",
                "Settings",
                "SystemAppData",
                "TempState",
                "AC"
            ],
            TimeSpan.FromDays(1),
            "Per-package LocalCache folders used by Microsoft Store apps. Durable app state and settings paths are excluded."));

        AddIfAnyRootProvided(rules, GetSteamHttpCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.steam-httpcache",
            "Steam launcher HTTP cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["steamapps", "common", "downloading", "workshop", "userdata", "config", "manifests"],
            TimeSpan.FromDays(1),
            "Steam launcher HTTP cache files. Installed games, workshop content, and account/config data are excluded.",
            LauncherName: "Steam",
            ProcessGuardNames: ["steam", "steamwebhelper"]));

        AddIfAnyRootProvided(rules, GetSteamLogsRoots(paths), roots => new CleanupRule(
            "cp.s1.steam-logs",
            "Steam launcher logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt", "*.old"],
            ["steamapps", "common", "downloading", "workshop", "userdata", "config", "manifests"],
            TimeSpan.FromDays(1),
            "Steam launcher logs useful for troubleshooting but usually safe to clear after review.",
            LauncherName: "Steam",
            ProcessGuardNames: ["steam", "steamwebhelper"]));

        AddIfAnyRootProvided(rules, GetSteamDumpRoots(paths), roots => new CleanupRule(
            "cp.s1.steam-dumps",
            "Steam launcher dump files",
            RiskLevel.S1LowRisk,
            roots,
            ["*.dmp", "*.mdmp", "*.tmp"],
            ["steamapps", "common", "downloading", "workshop", "userdata", "config", "manifests"],
            TimeSpan.FromDays(1),
            "Steam launcher dump files generated for crash diagnostics.",
            LauncherName: "Steam",
            ProcessGuardNames: ["steam", "steamwebhelper"]));

        AddIfAnyRootProvided(rules, GetEpicWebCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.epic-webcache",
            "Epic Games Launcher web cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["Cookies", "History", "Sessions", "Login Data", "Web Data", "Bookmarks", "Local Storage", "IndexedDB", "Session Storage", "Manifests"],
            TimeSpan.FromDays(1),
            "Epic Games Launcher web/UI cache under Saved\\webcache*. Identity and session state are excluded.",
            LauncherName: "Epic Games Launcher",
            ProcessGuardNames: ["EpicGamesLauncher"]));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "EpicGamesLauncher", "Saved", "Logs"), root => new CleanupRule(
            "cp.s1.epic-logs",
            "Epic Games Launcher logs",
            RiskLevel.S1LowRisk,
            [root],
            ["*.log", "*.txt"],
            ["Manifests", "Config"],
            TimeSpan.FromDays(1),
            "Epic Games Launcher diagnostic logs.",
            LauncherName: "Epic Games Launcher",
            ProcessGuardNames: ["EpicGamesLauncher"]));

        AddIfAnyRootProvided(rules, GetBattleNetCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.battlenet-cache",
            "Battle.net launcher cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["Data", "Manifests", "Config", "Cookies", "History", "Sessions", "Login Data", "Local Storage", "IndexedDB", "Session Storage"],
            TimeSpan.FromDays(1),
            "Battle.net launcher cache folders that are clearly cache-scoped.",
            LauncherName: "Battle.net",
            ProcessGuardNames: ["Battle.net", "Agent"]));

        AddIfAnyRootProvided(rules, GetBattleNetLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.battlenet-logs",
            "Battle.net launcher logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt"],
            ["Data", "Manifests", "Config"],
            TimeSpan.FromDays(1),
            "Battle.net launcher logs.",
            LauncherName: "Battle.net",
            ProcessGuardNames: ["Battle.net", "Agent"]));

        AddIfAnyRootProvided(rules, GetRiotClientCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.riot-client-cache",
            "Riot Client cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["Cookies", "History", "Sessions", "Login Data", "Web Data", "Bookmarks", "Local Storage", "IndexedDB", "Session Storage", "Config", "Saves"],
            TimeSpan.FromDays(1),
            "Riot Client launcher cache directories only.",
            LauncherName: "Riot Client",
            ProcessGuardNames: ["RiotClientServices", "RiotClientUx", "RiotClientUxRender"]));

        AddIfAnyRootProvided(rules, GetRiotClientLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.riot-client-logs",
            "Riot Client logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt"],
            ["Config", "Saves", "Manifests"],
            TimeSpan.FromDays(1),
            "Riot Client diagnostic logs.",
            LauncherName: "Riot Client",
            ProcessGuardNames: ["RiotClientServices", "RiotClientUx", "RiotClientUxRender"]));

        AddIfAnyRootProvided(rules, GetEaAppCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.ea-app-cache",
            "EA App cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["Cookies", "History", "Sessions", "Login Data", "Web Data", "Bookmarks", "Local Storage", "IndexedDB", "Session Storage", "Config", "Saves"],
            TimeSpan.FromDays(1),
            "EA App launcher cache directories only.",
            LauncherName: "EA App",
            ProcessGuardNames: ["EADesktop", "EABackgroundService"]));

        AddIfAnyRootProvided(rules, GetEaAppLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.ea-app-logs",
            "EA App logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt"],
            ["Config", "Saves", "Manifests"],
            TimeSpan.FromDays(1),
            "EA App launcher logs.",
            LauncherName: "EA App",
            ProcessGuardNames: ["EADesktop", "EABackgroundService"]));

        AddIfAnyRootProvided(rules, GetUbisoftCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.ubisoft-connect-cache",
            "Ubisoft Connect cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["Cookies", "History", "Sessions", "Login Data", "Web Data", "Bookmarks", "Local Storage", "IndexedDB", "Session Storage", "Config", "Savegames", "Saves"],
            TimeSpan.FromDays(1),
            "Ubisoft Connect launcher cache directories only.",
            LauncherName: "Ubisoft Connect",
            ProcessGuardNames: ["UbisoftConnect", "upc"]));

        AddIfAnyRootProvided(rules, GetUbisoftLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.ubisoft-connect-logs",
            "Ubisoft Connect logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt"],
            ["Config", "Savegames", "Saves", "Manifests"],
            TimeSpan.FromDays(1),
            "Ubisoft Connect launcher logs.",
            LauncherName: "Ubisoft Connect",
            ProcessGuardNames: ["UbisoftConnect", "upc"]));

        AddIfAnyRootProvided(rules, GetDirectXShaderCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.directx-shader-cache",
            "DirectX and GPU shader caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Graphics drivers and DirectX can recreate shader caches. Cleaning them may cause slower first launches or stutter while shaders rebuild."));

        AddIfAnyRootProvided(rules, GetNuGetHttpAndPluginCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.nuget-http-cache",
            "NuGet HTTP and plugin caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "NuGet can recreate HTTP and plugin caches. Cleaning may require network downloads for restore operations."));

        AddIfAnyRootProvided(rules, GetNpmCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.npm-cache",
            "npm cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "npm can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetYarnCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.yarn-cache",
            "Yarn cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Yarn can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetPnpmStoreRoots(paths), roots => new CleanupRule(
            "cp.s1.pnpm-store",
            "pnpm store cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "pnpm can recreate this package store. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetPipCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.pip-cache",
            "pip cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "pip can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetComposerCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.composer-cache",
            "Composer cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            ["vendor"],
            TimeSpan.FromDays(7),
            "Composer can recreate this package cache. Cleaning it may require packages to be downloaded again."));

        AddIfAnyRootProvided(rules, GetGoCacheRoots(paths), roots => new CleanupRule(
            "cp.s1.go-cache",
            "Go build and module cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(7),
            "Go can recreate build and module download caches. Cleaning them may slow future builds or require module downloads."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".cargo", "registry", "cache"), root => new CleanupRule(
            "cp.s1.cargo-registry-cache",
            "Cargo registry cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(14),
            "Cargo can recreate downloaded registry package archives. Cleaning them may slow future builds."));

        AddIfPathProvided(rules, Path.Combine(paths.UserProfile, ".cargo", "git", "db"), root => new CleanupRule(
            "cp.s1.cargo-git-cache",
            "Cargo git cache",
            RiskLevel.S1LowRisk,
            [root],
            ["*"],
            [],
            TimeSpan.FromDays(14),
            "Cargo can recreate git dependency caches. Cleaning them may slow future builds."));

        AddIfAnyRootProvided(rules, GetGradleCacheRoots(paths.UserProfile), roots => new CleanupRule(
            "cp.s1.gradle-dependency-cache",
            "Gradle dependency cache",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            [],
            TimeSpan.FromDays(14),
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

        AddIfAnyRootProvided(rules, GetDenoCacheRoots(paths.LocalAppData, paths.UserProfile), roots => new CleanupRule(
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
            BuildVsCodeExclusions(),
            TimeSpan.FromDays(1),
            "VS Code can recreate these UI caches. Extensions, settings, and workspace storage are excluded.",
            ProcessGuardNames: VsCodeProcessGuardNames));

        AddIfAnyRootProvided(rules, GetVsCodeLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.vscode-logs",
            "Visual Studio Code logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt", "*.old"],
            BuildVsCodeExclusions(),
            TimeSpan.FromDays(7),
            "VS Code logs are diagnostic files that are usually safe to remove after review.",
            Recursive: false,
            ProcessGuardNames: VsCodeProcessGuardNames));

        AddIfAnyRootProvided(rules, GetVsCodeCrashReportRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.vscode-crash-reports",
            "Visual Studio Code crash reports",
            RiskLevel.S1LowRisk,
            roots,
            ["*.dmp", "*.mdmp", "*.dump", "*.log", "*.txt"],
            BuildCrashDiagnosticExclusions(),
            TimeSpan.FromDays(7),
            "Completed crash reports can be removed after review. Pending uploads and state data are excluded.",
            Recursive: false,
            ProcessGuardNames: VsCodeProcessGuardNames));

        AddIfAnyRootProvided(rules, GetVsCodeCrashCompletedRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.vscode-crash-completed",
            "Visual Studio Code completed crash diagnostics",
            RiskLevel.S1LowRisk,
            roots,
            ["*.dmp", "*.mdmp", "*.log", "*.txt"],
            BuildCrashDiagnosticExclusions(),
            TimeSpan.FromDays(7),
            "Completed crash diagnostics can be removed after review. Pending uploads and state data are excluded.",
            Recursive: false,
            ProcessGuardNames: VsCodeProcessGuardNames));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "JetBrains"), root => new CleanupRule(
            "cp.s1.jetbrains-cache",
            "JetBrains IDE caches",
            RiskLevel.S1LowRisk,
            GetJetBrainsCacheRoots(root),
            ["*"],
            BuildJetBrainsExclusions(),
            TimeSpan.FromDays(1),
            "JetBrains IDEs can recreate cache directories. Configurations, plugins, and projects are excluded.",
            ProcessGuardNames: JetBrainsProcessGuardNames));

        AddIfPathProvided(rules, Path.Combine(paths.LocalAppData, "JetBrains"), root => new CleanupRule(
            "cp.s1.jetbrains-logs",
            "JetBrains IDE logs",
            RiskLevel.S1LowRisk,
            GetJetBrainsLogRoots(root),
            ["*.log", "*.txt", "*.old"],
            BuildJetBrainsExclusions(),
            TimeSpan.FromDays(7),
            "JetBrains diagnostic logs are usually safe to clear after review.",
            Recursive: false,
            ProcessGuardNames: JetBrainsProcessGuardNames));

        AddIfAnyRootProvided(rules, GetElectronAppCacheRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.electron-app-ui-cache",
            "Electron app UI caches",
            RiskLevel.S1LowRisk,
            roots,
            ["*"],
            BuildElectronAppExclusions(),
            TimeSpan.FromDays(1),
            "Electron apps can recreate Cache, Code Cache, and GPUCache folders. Settings, local storage, sessions, and databases are excluded.",
            ProcessGuardNames: BuildElectronAppProcessGuardNames()));

        AddIfAnyRootProvided(rules, GetElectronAppLogRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.electron-app-logs",
            "Electron app logs",
            RiskLevel.S1LowRisk,
            roots,
            ["*.log", "*.txt", "*.old"],
            BuildElectronAppExclusions(),
            TimeSpan.FromDays(7),
            "Old app logs are diagnostic files that are usually safe to remove after review.",
            Recursive: false,
            ProcessGuardNames: BuildElectronAppProcessGuardNames()));

        AddIfAnyRootProvided(rules, GetElectronAppCrashReportRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.electron-app-crash-reports",
            "Electron app crash reports",
            RiskLevel.S1LowRisk,
            roots,
            ["*.dmp", "*.mdmp", "*.dump", "*.log", "*.txt"],
            BuildCrashDiagnosticExclusions(),
            TimeSpan.FromDays(7),
            "Completed crash reports can be removed after review. Pending uploads and state data are excluded.",
            Recursive: false,
            ProcessGuardNames: BuildElectronAppProcessGuardNames()));

        AddIfAnyRootProvided(rules, GetElectronAppCrashCompletedRoots(paths.LocalAppData), roots => new CleanupRule(
            "cp.s1.electron-app-crash-completed",
            "Electron app completed crash diagnostics",
            RiskLevel.S1LowRisk,
            roots,
            ["*.dmp", "*.mdmp", "*.log", "*.txt"],
            BuildCrashDiagnosticExclusions(),
            TimeSpan.FromDays(7),
            "Completed crash diagnostics can be removed after review. Pending uploads and state data are excluded.",
            Recursive: false,
            ProcessGuardNames: BuildElectronAppProcessGuardNames()));

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
            Path.Combine(paths.UserProfile, "go", "pkg", "mod", "cache"),
            Path.Combine(paths.UserProfile, "go", "pkg", "mod", "cache", "download")
        ];
    }

    private static IReadOnlyList<string> GetNpmCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "npm-cache"),
            Path.Combine(paths.UserProfile, "AppData", "Roaming", "npm-cache"),
            Path.Combine(paths.UserProfile, ".npm")
        ];
    }

    private static IReadOnlyList<string> GetPnpmStoreRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "pnpm", "store"),
            Path.Combine(paths.LocalAppData, "pnpm-store"),
            Path.Combine(paths.UserProfile, ".pnpm-store")
        ];
    }

    private static IReadOnlyList<string> GetYarnCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "Yarn", "Cache"),
            Path.Combine(paths.UserProfile, ".cache", "yarn")
        ];
    }

    private static IReadOnlyList<string> GetPipCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "pip", "Cache"),
            Path.Combine(paths.UserProfile, ".cache", "pip")
        ];
    }

    private static IReadOnlyList<string> GetComposerCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.UserProfile, "AppData", "Roaming", "Composer", "cache"),
            Path.Combine(paths.LocalAppData, "Composer", "cache"),
            Path.Combine(paths.UserProfile, ".composer", "cache")
        ];
    }

    private static IReadOnlyList<string> GetGradleCacheRoots(string userProfile)
    {
        return
        [
            Path.Combine(userProfile, ".gradle", "caches", "modules-2", "files-2.1"),
            Path.Combine(userProfile, ".gradle", "caches", "journal-1")
        ];
    }

    private static IReadOnlyList<string> GetNuGetHttpAndPluginCacheRoots(EnvironmentPaths paths)
    {
        return
        [
            Path.Combine(paths.LocalAppData, "NuGet", "v3-cache"),
            Path.Combine(paths.LocalAppData, "NuGet", "Cache"),
            Path.Combine(paths.LocalAppData, "NuGet", "plugins-cache")
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

    private static IReadOnlyList<string> GetVsCodeLogRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Code", "logs"),
            Path.Combine(localAppData, "Code - Insiders", "logs"),
            Path.Combine(localAppData, "VSCodium", "logs")
        ];
    }

    private static IReadOnlyList<string> GetVsCodeCrashReportRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Code", "Crashpad", "reports"),
            Path.Combine(localAppData, "Code - Insiders", "Crashpad", "reports"),
            Path.Combine(localAppData, "VSCodium", "Crashpad", "reports"),
        ];
    }

    private static IReadOnlyList<string> GetVsCodeCrashCompletedRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Code", "Crashpad", "completed"),
            Path.Combine(localAppData, "Code - Insiders", "Crashpad", "completed"),
            Path.Combine(localAppData, "VSCodium", "Crashpad", "completed")
        ];
    }

    private static IReadOnlyList<string> GetElectronAppCacheRoots(string localAppData)
    {
        var roots = new List<string>();
        foreach (var appRoot in GetElectronAppProfileRoots(localAppData))
        {
            roots.AddRange(GetSingleProfileChromiumCacheRoots(appRoot));
        }

        return roots;
    }

    private static IReadOnlyList<string> GetElectronAppLogRoots(string localAppData)
    {
        return GetElectronAppProfileRoots(localAppData)
            .Select(appRoot => Path.Combine(appRoot, "logs"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetElectronAppCrashReportRoots(string localAppData)
    {
        return GetElectronAppProfileRoots(localAppData)
            .Select(appRoot => Path.Combine(appRoot, "Crashpad", "reports"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetElectronAppCrashCompletedRoots(string localAppData)
    {
        return GetElectronAppProfileRoots(localAppData)
            .Select(appRoot => Path.Combine(appRoot, "Crashpad", "completed"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetElectronAppProfileRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Discord"),
            Path.Combine(localAppData, "DiscordCanary"),
            Path.Combine(localAppData, "DiscordPTB"),
            Path.Combine(localAppData, "Slack"),
            Path.Combine(localAppData, "Microsoft", "Teams"),
            Path.Combine(localAppData, "Microsoft", "TeamsMeetingAddin"),
            Path.Combine(localAppData, "Microsoft", "MSTeams")
        ];
    }

    private static IReadOnlyList<string> BuildElectronAppProcessGuardNames()
    {
        return DiscordProcessGuardNames
            .Concat(SlackProcessGuardNames)
            .Concat(TeamsProcessGuardNames)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildElectronAppExclusions()
    {
        return AppProfileIdentityAndSessionExclusions
            .Concat(
            [
                "settings",
                "config",
                "workspaceStorage",
                "extensions",
                "plugins",
                "databases"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildVsCodeExclusions()
    {
        return AppProfileIdentityAndSessionExclusions
            .Concat(
            [
                "workspaceStorage",
                "extensions",
                "settings",
                "config",
                "User",
                "globalStorage"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildJetBrainsExclusions()
    {
        return AppProfileIdentityAndSessionExclusions
            .Concat(
            [
                "config",
                "plugins",
                "projects",
                "workspace",
                "options",
                "settings"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> BuildCrashDiagnosticExclusions()
    {
        return AppProfileIdentityAndSessionExclusions
            .Concat(
            [
                "pending",
                "new",
                "uploads",
                "attachments",
                "metadata",
                "database",
                "databases",
                "settings",
                "config",
                "state"
            ])
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static IReadOnlyList<string> GetDenoCacheRoots(string localAppData, string userProfile)
    {
        var denoRoot = Path.Combine(localAppData, "deno");
        return
        [
            Path.Combine(denoRoot, "deps"),
            Path.Combine(denoRoot, "gen"),
            Path.Combine(denoRoot, "npm"),
            Path.Combine(userProfile, ".cache", "deno")
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

        return
        [
            Path.Combine(jetBrainsRoot, "caches")
        ];
    }

    private static IReadOnlyList<string> GetJetBrainsLogRoots(string jetBrainsRoot)
    {
        if (Directory.Exists(jetBrainsRoot))
        {
            try
            {
                var discoveredLogs = Directory
                    .EnumerateDirectories(jetBrainsRoot, "*", SearchOption.TopDirectoryOnly)
                    .Select(directory => Path.Combine(directory, "log"))
                    .ToArray();

                if (discoveredLogs.Length > 0)
                {
                    return discoveredLogs;
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
            Path.Combine(jetBrainsRoot, "log")
        ];
    }

    private static string GetWindowsTempRoot(string windowsRoot)
    {
        return string.IsNullOrWhiteSpace(windowsRoot)
            ? string.Empty
            : Path.Combine(windowsRoot, "Temp");
    }

    private static IReadOnlyList<string> GetInternetCacheRoots(string localAppData)
    {
        var windowsRoot = Path.Combine(localAppData, "Microsoft", "Windows");
        return
        [
            Path.Combine(windowsRoot, "INetCache")
        ];
    }

    private static IReadOnlyList<string> GetMicrosoftStoreLocalCacheRoots(string localAppData)
    {
        var packagesRoot = Path.Combine(localAppData, "Packages");
        if (Directory.Exists(packagesRoot))
        {
            try
            {
                return Directory
                    .EnumerateDirectories(packagesRoot)
                    .Select(packageRoot => Path.Combine(packageRoot, "LocalCache"))
                    .ToArray();
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return [];
    }

    private static IReadOnlyList<string> GetSteamHttpCacheRoots(EnvironmentPaths paths)
    {
        return GetSteamLauncherRoots(paths)
            .Select(root => Path.Combine(root, "appcache", "httpcache"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetSteamLogsRoots(EnvironmentPaths paths)
    {
        return GetSteamLauncherRoots(paths)
            .Select(root => Path.Combine(root, "logs"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetSteamDumpRoots(EnvironmentPaths paths)
    {
        return GetSteamLauncherRoots(paths)
            .Select(root => Path.Combine(root, "dumps"))
            .ToArray();
    }

    private static IReadOnlyList<string> GetSteamLauncherRoots(EnvironmentPaths paths)
    {
        return new[]
        {
            CombineIfBaseProvided(paths.ProgramFiles, "Steam"),
            CombineIfBaseProvided(paths.ProgramFilesX86, "Steam"),
            CombineIfBaseProvided(paths.LocalAppData, "Steam")
        }
        .Where(path => !string.IsNullOrWhiteSpace(path))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    }

    private static IReadOnlyList<string> GetEpicWebCacheRoots(string localAppData)
    {
        var savedRoot = Path.Combine(localAppData, "EpicGamesLauncher", "Saved");
        if (Directory.Exists(savedRoot))
        {
            try
            {
                var discovered = Directory
                    .EnumerateDirectories(savedRoot)
                    .Where(path => Path.GetFileName(path).StartsWith("webcache", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                if (discovered.Length > 0)
                {
                    return discovered;
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return [Path.Combine(savedRoot, "webcache")];
    }

    private static IReadOnlyList<string> GetBattleNetCacheRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Battle.net", "Cache")
        ];
    }

    private static IReadOnlyList<string> GetBattleNetLogRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Battle.net", "Logs")
        ];
    }

    private static IReadOnlyList<string> GetRiotClientCacheRoots(string localAppData)
    {
        var root = Path.Combine(localAppData, "Riot Games", "Riot Client");
        return
        [
            Path.Combine(root, "Cache"),
            Path.Combine(root, "Code Cache"),
            Path.Combine(root, "GPUCache")
        ];
    }

    private static IReadOnlyList<string> GetRiotClientLogRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Riot Games", "Riot Client", "Logs")
        ];
    }

    private static IReadOnlyList<string> GetEaAppCacheRoots(string localAppData)
    {
        var root = Path.Combine(localAppData, "Electronic Arts", "EA Desktop");
        return
        [
            Path.Combine(root, "Cache"),
            Path.Combine(root, "Code Cache"),
            Path.Combine(root, "GPUCache")
        ];
    }

    private static IReadOnlyList<string> GetEaAppLogRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Electronic Arts", "EA Desktop", "Logs")
        ];
    }

    private static IReadOnlyList<string> GetUbisoftCacheRoots(string localAppData)
    {
        var root = Path.Combine(localAppData, "Ubisoft Game Launcher");
        return
        [
            Path.Combine(root, "cache"),
            Path.Combine(root, "Cache")
        ];
    }

    private static IReadOnlyList<string> GetUbisoftLogRoots(string localAppData)
    {
        return
        [
            Path.Combine(localAppData, "Ubisoft Game Launcher", "logs"),
            Path.Combine(localAppData, "Ubisoft Game Launcher", "Logs")
        ];
    }

    private static string CombineIfBaseProvided(string basePath, params string[] segments)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return string.Empty;
        }

        return Path.Combine(new[] { basePath }.Concat(segments).ToArray());
    }
}
