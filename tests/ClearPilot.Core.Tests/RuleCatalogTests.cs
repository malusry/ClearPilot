using ClearPilot.Core.Cleanup;
using ClearPilot.Core.Rules;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class RuleCatalogTests
{
    [Fact]
    public void DefaultCatalogContainsOnlyS0AndS1RulesForPhaseTwo()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester");

        var rules = RuleCatalog.CreateDefault(paths);

        Assert.NotEmpty(rules);
        Assert.Contains(rules, rule => rule.RiskLevel == RiskLevel.S0VeryLowRisk);
        Assert.Contains(rules, rule => rule.RiskLevel == RiskLevel.S1LowRisk);
        Assert.All(rules, rule =>
        {
            Assert.True(
                rule.RiskLevel is RiskLevel.S0VeryLowRisk or RiskLevel.S1LowRisk,
                $"Unexpected risk level: {rule.RiskLevel}");
        });
    }

    [Fact]
    public void OnlyS0RulesCanRunWithoutConfirmation()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\LocalAppData", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.All(rules, rule => Assert.Equal(rule.RiskLevel == RiskLevel.S0VeryLowRisk, rule.CanRunWithoutConfirmation));
    }

    [Fact]
    public void UserTempRuleExcludesClearPilotInternalFolders()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\LocalAppData", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s0.user-temp");

        Assert.Contains("ClearPilot", rule.ExcludePathSegments);
        Assert.Contains("ClearPilot.Tests", rule.ExcludePathSegments);
    }

    [Fact]
    public void ChapterTwoWindowsTargetsUseExpectedRiskClassification()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData");

        var rules = RuleCatalog.CreateDefault(paths);

        Assert.Equal(
            RiskLevel.S0VeryLowRisk,
            Assert.Single(rules, rule => rule.RuleId == "cp.s0.user-temp").RiskLevel);
        Assert.Equal(
            RiskLevel.S1LowRisk,
            Assert.Single(rules, rule => rule.RuleId == "cp.s1.windows-temp").RiskLevel);
        Assert.Equal(
            RiskLevel.S1LowRisk,
            Assert.Single(rules, rule => rule.RuleId == "cp.s1.windows-error-reports").RiskLevel);
        Assert.Equal(
            RiskLevel.S1LowRisk,
            Assert.Single(rules, rule => rule.RuleId == "cp.s1.user-crash-dumps").RiskLevel);
    }

    [Fact]
    public void InternetTemporaryCacheRuleExcludesIdentityAndSessionDataSegments()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.windows-inet-cache");

        Assert.Contains("Cookies", rule.ExcludePathSegments);
        Assert.Contains("History", rule.ExcludePathSegments);
        Assert.Contains("Sessions", rule.ExcludePathSegments);
        Assert.Contains("Login Data", rule.ExcludePathSegments);
        Assert.Contains("Bookmarks", rule.ExcludePathSegments);
        Assert.Contains("Local Storage", rule.ExcludePathSegments);
        Assert.Contains("IndexedDB", rule.ExcludePathSegments);
        Assert.Contains("Session Storage", rule.ExcludePathSegments);
    }

    [Fact]
    public void MicrosoftStoreLocalCacheRuleUsesPackageLocalCacheRootsOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var localAppData = Path.Combine(root, "LocalAppData");
            var packagesRoot = Path.Combine(localAppData, "Packages");
            Directory.CreateDirectory(Path.Combine(packagesRoot, "Contoso.App_123", "LocalCache"));
            Directory.CreateDirectory(Path.Combine(packagesRoot, "Fabrikam.App_456", "LocalCache"));
            var paths = new EnvironmentPaths(Path.Combine(root, "Temp"), localAppData, Path.Combine(root, "User"));

            var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.msstore-localcache");

            Assert.NotEmpty(rule.RootPaths);
            Assert.All(rule.RootPaths, rootPath => Assert.EndsWith("LocalCache", rootPath, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, rootPath => rootPath.Contains("LocalState", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void WindowsSystemManagedAreasAreNotCleanupDeletionTargets()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester", @"C:\Windows", @"C:\ProgramData");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule =>
            rule.RootPaths.Any(root =>
                root.Contains(Path.Combine("Windows", "SoftwareDistribution", "Download"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("DeliveryOptimization", "Cache"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "Logs", "CBS"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "Logs", "DISM"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "MEMORY.DMP"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "WinSxS"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Windows", "Installer"), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void BrowserCacheRulesPointAtCacheFoldersOnly()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.edge-cache" or "cp.s1.chrome-cache")
            .ToArray();

        Assert.NotEmpty(rules);
        Assert.All(rules.SelectMany(rule => rule.RootPaths), root =>
        {
            Assert.Contains("Cache", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Cookies", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Login Data", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sessions", root, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void DefaultCatalogIncludesCommonUserOwnedPackageCaches()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var ruleIds = RuleCatalog.CreateDefault(paths)
            .Select(rule => rule.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("cp.s1.npm-cache", ruleIds);
        Assert.Contains("cp.s1.yarn-cache", ruleIds);
        Assert.Contains("cp.s1.pnpm-store", ruleIds);
        Assert.Contains("cp.s1.pip-cache", ruleIds);
        Assert.Contains("cp.s1.composer-cache", ruleIds);
        Assert.Contains("cp.s1.go-cache", ruleIds);
        Assert.Contains("cp.s1.cargo-registry-cache", ruleIds);
        Assert.Contains("cp.s1.gradle-dependency-cache", ruleIds);
        Assert.Contains("cp.s1.maven-repository-cache", ruleIds);
        Assert.Contains("cp.s1.deno-cache", ruleIds);
        Assert.Contains("cp.s1.bun-install-cache", ruleIds);
        Assert.Contains("cp.s1.python-bytecode-cache", ruleIds);
    }

    [Fact]
    public void DefaultCatalogIncludesConservativeApplicationCacheRules()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var ruleIds = RuleCatalog.CreateDefault(paths)
            .Select(rule => rule.RuleId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("cp.s1.vscode-cache", ruleIds);
        Assert.Contains("cp.s1.jetbrains-cache", ruleIds);
        Assert.Contains("cp.s1.windows-thumbnail-cache", ruleIds);
        Assert.Contains("cp.s1.electron-app-ui-cache", ruleIds);
        Assert.Contains("cp.s1.directx-shader-cache", ruleIds);
        Assert.Contains("cp.s1.windows-error-reports", ruleIds);
    }

    [Fact]
    public void DefaultCatalogIncludesAdditionalBrowserCacheRulesWithoutIdentityData()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var browserRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.brave-cache" or
                "cp.s1.chromium-cache" or
                "cp.s1.vivaldi-cache" or
                "cp.s1.opera-cache" or
                "cp.s1.firefox-cache")
            .ToArray();

        Assert.Equal(5, browserRules.Length);
        Assert.All(browserRules.SelectMany(rule => rule.RootPaths), root =>
        {
            Assert.True(
                root.Contains("Cache", StringComparison.OrdinalIgnoreCase)
                || root.Contains("GPUCache", StringComparison.OrdinalIgnoreCase)
                || root.Contains("cache2", StringComparison.OrdinalIgnoreCase),
                $"Unexpected browser cache root: {root}");
            Assert.DoesNotContain("Cookies", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Login", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Bookmarks", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("History", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Sessions", root, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void PythonBytecodeRuleExcludesCommonDependencyAndVirtualEnvironmentFolders()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.python-bytecode-cache");

        Assert.Contains("*.pyc", rule.IncludeFilePatterns);
        Assert.Contains("*.pyo", rule.IncludeFilePatterns);
        Assert.Contains(".venv", rule.ExcludePathSegments);
        Assert.Contains("venv", rule.ExcludePathSegments);
        Assert.Contains("node_modules", rule.ExcludePathSegments);
        Assert.DoesNotContain("*", rule.IncludeFilePatterns);
    }

    [Fact]
    public void ElectronAppRuleTargetsOnlyUiCacheFolders()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");

        Assert.NotEmpty(rule.RootPaths);
        Assert.All(rule.RootPaths, root =>
        {
            Assert.True(
                root.EndsWith("Cache", StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("Code Cache", StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("GPUCache", StringComparison.OrdinalIgnoreCase),
                $"Unexpected Electron cache root: {root}");
            Assert.DoesNotContain("Local Storage", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Session", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("IndexedDB", root, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void VsCodeCacheRuleExcludesSettingsExtensionsAndWorkspaceStorage()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");

        Assert.NotEmpty(rule.RootPaths);
        Assert.All(rule.RootPaths, root =>
        {
            Assert.Contains("Cache", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("extensions", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("settings", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspaceStorage", root, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void JetBrainsRulePointsOnlyAtCachesDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var localAppData = Path.Combine(root, "LocalAppData");
            Directory.CreateDirectory(Path.Combine(localAppData, "JetBrains", "Rider2026.1"));
            Directory.CreateDirectory(Path.Combine(localAppData, "JetBrains", "IdeaIC2026.1"));
            var paths = new EnvironmentPaths(Path.Combine(root, "Temp"), localAppData, Path.Combine(root, "User"));

            var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.jetbrains-cache");

            Assert.All(rule.RootPaths, rootPath =>
            {
                Assert.EndsWith($"{Path.DirectorySeparatorChar}caches", rootPath, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("plugins", rootPath, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("config", rootPath, StringComparison.OrdinalIgnoreCase);
            });
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void WindowsThumbnailRuleMatchesOnlyCacheDatabaseFiles()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.windows-thumbnail-cache");

        Assert.Contains("thumbcache_*.db", rule.IncludeFilePatterns);
        Assert.Contains("iconcache_*.db", rule.IncludeFilePatterns);
        Assert.DoesNotContain("*", rule.IncludeFilePatterns);
    }

    [Fact]
    public void GoCacheRuleIncludesBuildAndModuleDownloadCaches()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.go-cache");

        Assert.Contains(rule.RootPaths, root => root.EndsWith(Path.Combine("AppData", "Local", "go-build"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(rule.RootPaths, root => root.EndsWith(Path.Combine("go", "pkg", "mod", "cache", "download"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void BrowserCacheRulesDiscoverMultipleChromiumProfiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var localAppData = Path.Combine(root, "LocalAppData");
            var edgeUserData = Path.Combine(localAppData, "Microsoft", "Edge", "User Data");
            Directory.CreateDirectory(Path.Combine(edgeUserData, "Default"));
            Directory.CreateDirectory(Path.Combine(edgeUserData, "Profile 1"));
            Directory.CreateDirectory(Path.Combine(edgeUserData, "System Profile"));
            var paths = new EnvironmentPaths(Path.Combine(root, "Temp"), localAppData, Path.Combine(root, "User"));

            var edgeRule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.edge-cache");

            Assert.Contains(edgeRule.RootPaths, path => path.Contains(Path.Combine("Default", "Cache"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(edgeRule.RootPaths, path => path.Contains(Path.Combine("Profile 1", "Cache"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(edgeRule.RootPaths, path => path.Contains("System Profile", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public void ChapterThreeGameLauncherRulesAreS1AndRequireProcessGuard()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)");

        var rules = RuleCatalog.CreateDefault(paths);
        var expectedRules = new Dictionary<string, string[]>
        {
            ["cp.s1.steam-httpcache"] = ["steam", "steamwebhelper"],
            ["cp.s1.steam-logs"] = ["steam", "steamwebhelper"],
            ["cp.s1.steam-dumps"] = ["steam", "steamwebhelper"],
            ["cp.s1.epic-webcache"] = ["EpicGamesLauncher"],
            ["cp.s1.epic-logs"] = ["EpicGamesLauncher"],
            ["cp.s1.battlenet-cache"] = ["Battle.net", "Agent"],
            ["cp.s1.battlenet-logs"] = ["Battle.net", "Agent"],
            ["cp.s1.riot-client-cache"] = ["RiotClientServices", "RiotClientUx", "RiotClientUxRender"],
            ["cp.s1.riot-client-logs"] = ["RiotClientServices", "RiotClientUx", "RiotClientUxRender"],
            ["cp.s1.ea-app-cache"] = ["EADesktop", "EABackgroundService"],
            ["cp.s1.ea-app-logs"] = ["EADesktop", "EABackgroundService"],
            ["cp.s1.ubisoft-connect-cache"] = ["UbisoftConnect", "upc"],
            ["cp.s1.ubisoft-connect-logs"] = ["UbisoftConnect", "upc"]
        };

        foreach (var expected in expectedRules)
        {
            var rule = Assert.Single(rules, candidate => candidate.RuleId == expected.Key);
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.False(string.IsNullOrWhiteSpace(rule.LauncherName));
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
            Assert.All(expected.Value, name =>
                Assert.Contains(name, rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase));
            Assert.False(rule.CanRunWithoutConfirmation);
        }
    }

    [Fact]
    public void SteamRulesDoNotTargetGameLibraryOrManifestLocations()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)");
        var rules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.steam-httpcache" or "cp.s1.steam-logs" or "cp.s1.steam-dumps")
            .ToArray();

        Assert.Equal(3, rules.Length);
        Assert.All(rules, rule =>
        {
            Assert.Contains(rule.ExcludePathSegments, segment => segment.Equals("steamapps", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rule.ExcludePathSegments, segment => segment.Equals("workshop", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(rule.ExcludePathSegments, segment => segment.Equals("downloading", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("steamapps", "common"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("steamapps", "downloading"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("steamapps", "workshop"), StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void ChapterThreeS2LauncherReviewRootsAreNotDeletionRules()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule => rule.RootPaths.Any(root =>
            root.EndsWith(Path.Combine("steamapps", "shadercache"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith("depotcache", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void RuleCatalog_DoesNotContainDownloadsS0OrS1Rule()
    {
        var paths = new EnvironmentPaths(
            @"C:\Users\tester\AppData\Local\Temp",
            @"C:\Users\tester\AppData\Local",
            @"C:\Users\tester",
            @"C:\Windows",
            @"C:\ProgramData",
            @"C:\Program Files",
            @"C:\Program Files (x86)");

        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(
            rules,
            rule =>
                (rule.RiskLevel is RiskLevel.S0VeryLowRisk or RiskLevel.S1LowRisk)
                && rule.RootPaths.Any(root =>
                    root.Contains(Path.Combine("Users", "tester", "Downloads"), StringComparison.OrdinalIgnoreCase)
                    || root.EndsWith("Downloads", StringComparison.OrdinalIgnoreCase)));
    }
}
