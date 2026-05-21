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
    public void PackageManagers_AllTargetsAreS1NotS0()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rulesById = RuleCatalog.CreateDefault(paths).ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);
        var packageRuleIds = new[]
        {
            "cp.s1.npm-cache",
            "cp.s1.pnpm-store",
            "cp.s1.yarn-cache",
            "cp.s1.nuget-http-cache",
            "cp.s1.nuget-global-packages",
            "cp.s1.pip-cache",
            "cp.s1.cargo-registry-cache",
            "cp.s1.cargo-git-cache",
            "cp.s1.gradle-dependency-cache",
            "cp.s1.maven-repository-cache",
            "cp.s1.deno-cache",
            "cp.s1.bun-install-cache",
            "cp.s1.composer-cache",
            "cp.s1.go-cache"
        };

        foreach (var ruleId in packageRuleIds)
        {
            var rule = Assert.Contains(ruleId, rulesById);
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.NotEqual(RiskLevel.S0VeryLowRisk, rule.RiskLevel);
        }
    }

    [Fact]
    public void PackageManagers_UserLevelCacheRootsCovered()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rulesById = RuleCatalog.CreateDefault(paths).ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "npm-cache"), rulesById["cp.s1.npm-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, "AppData", "Roaming", "npm-cache"), rulesById["cp.s1.npm-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".npm"), rulesById["cp.s1.npm-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "pnpm-store"), rulesById["cp.s1.pnpm-store"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".pnpm-store"), rulesById["cp.s1.pnpm-store"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "Yarn", "Cache"), rulesById["cp.s1.yarn-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".cache", "yarn"), rulesById["cp.s1.yarn-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "NuGet", "v3-cache"), rulesById["cp.s1.nuget-http-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.LocalAppData, "NuGet", "Cache"), rulesById["cp.s1.nuget-http-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.LocalAppData, "NuGet", "plugins-cache"), rulesById["cp.s1.nuget-http-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".nuget", "packages"), rulesById["cp.s1.nuget-global-packages"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "pip", "Cache"), rulesById["cp.s1.pip-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".cache", "pip"), rulesById["cp.s1.pip-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.UserProfile, ".cargo", "registry", "cache"), rulesById["cp.s1.cargo-registry-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".cargo", "git", "db"), rulesById["cp.s1.cargo-git-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.UserProfile, ".gradle", "caches", "modules-2", "files-2.1"), rulesById["cp.s1.gradle-dependency-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".gradle", "caches", "journal-1"), rulesById["cp.s1.gradle-dependency-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.UserProfile, ".m2", "repository"), rulesById["cp.s1.maven-repository-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "deno", "deps"), rulesById["cp.s1.deno-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".cache", "deno"), rulesById["cp.s1.deno-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.UserProfile, ".bun", "install", "cache"), rulesById["cp.s1.bun-install-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.UserProfile, "AppData", "Roaming", "Composer", "cache"), rulesById["cp.s1.composer-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.LocalAppData, "Composer", "cache"), rulesById["cp.s1.composer-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, ".composer", "cache"), rulesById["cp.s1.composer-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);

        Assert.Contains(Path.Combine(paths.LocalAppData, "go-build"), rulesById["cp.s1.go-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
        Assert.Contains(Path.Combine(paths.UserProfile, "go", "pkg", "mod", "cache"), rulesById["cp.s1.go-cache"].RootPaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void PackageManagers_ProjectLocalDependenciesExcluded()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var packageRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId.StartsWith("cp.s1.", StringComparison.OrdinalIgnoreCase) && (
                rule.RuleId.Contains("npm", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("pnpm", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("yarn", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("nuget", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("pip", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("cargo", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("gradle", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("maven", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("deno", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("bun", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("composer", StringComparison.OrdinalIgnoreCase)
                || rule.RuleId.Contains("go-cache", StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var forbiddenSegments = new[]
        {
            "node_modules", ".venv", "venv", "env", "vendor", "target", "bin", "obj", ".next", "dist", "build", "coverage", ".terraform"
        };

        Assert.NotEmpty(packageRules);
        foreach (var rootPath in packageRules.SelectMany(rule => rule.RootPaths))
        {
            foreach (var forbiddenSegment in forbiddenSegments)
            {
                Assert.DoesNotContain(
                    $"{Path.DirectorySeparatorChar}{forbiddenSegment}{Path.DirectorySeparatorChar}",
                    rootPath,
                    StringComparison.OrdinalIgnoreCase);
                Assert.False(
                    rootPath.EndsWith($"{Path.DirectorySeparatorChar}{forbiddenSegment}", StringComparison.OrdinalIgnoreCase),
                    $"Package cache root should not point to project-local folder: {rootPath}");
            }
        }
    }

    [Fact]
    public void PackageManagers_AgeThresholdsAreConservative()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rulesById = RuleCatalog.CreateDefault(paths).ToDictionary(rule => rule.RuleId, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(TimeSpan.FromDays(30), rulesById["cp.s1.maven-repository-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(30), rulesById["cp.s1.nuget-global-packages"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(14), rulesById["cp.s1.cargo-registry-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(14), rulesById["cp.s1.cargo-git-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(14), rulesById["cp.s1.gradle-dependency-cache"].MinimumAge);

        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.nuget-http-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.npm-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.pnpm-store"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.yarn-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.pip-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.deno-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.bun-install-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.composer-cache"].MinimumAge);
        Assert.Equal(TimeSpan.FromDays(7), rulesById["cp.s1.go-cache"].MinimumAge);
    }

    [Fact]
    public void PackageManagers_UnknownPackageLikeFoldersAreS2OrNeedsEvidence()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var packageRuleRoots = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId.StartsWith("cp.s1.", StringComparison.OrdinalIgnoreCase))
            .SelectMany(rule => rule.RootPaths)
            .ToArray();

        Assert.DoesNotContain(packageRuleRoots, root => root.Contains("packages-cache", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageRuleRoots, root => root.Contains("pkg-cache", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(packageRuleRoots, root => root.Contains("unknown-cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PackageManagers_NoBroadUserProfileScanning()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var packageRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.nuget-http-cache" or
                "cp.s1.nuget-global-packages" or
                "cp.s1.npm-cache" or
                "cp.s1.yarn-cache" or
                "cp.s1.pnpm-store" or
                "cp.s1.pip-cache" or
                "cp.s1.composer-cache" or
                "cp.s1.go-cache" or
                "cp.s1.cargo-registry-cache" or
                "cp.s1.cargo-git-cache" or
                "cp.s1.gradle-dependency-cache" or
                "cp.s1.maven-repository-cache" or
                "cp.s1.deno-cache" or
                "cp.s1.bun-install-cache")
            .ToArray();

        Assert.NotEmpty(packageRules);
        foreach (var rootPath in packageRules.SelectMany(rule => rule.RootPaths))
        {
            Assert.True(
                rootPath.StartsWith(paths.LocalAppData, StringComparison.OrdinalIgnoreCase)
                || rootPath.StartsWith(paths.UserProfile, StringComparison.OrdinalIgnoreCase),
                $"Unexpected broad root outside user scope: {rootPath}");
            Assert.NotEqual(paths.UserProfile, rootPath);
        }
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
    public void AppProfile_Discord_S1Target_HasProcessGuard()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.Contains("Discord.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DiscordCanary.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("DiscordPTB.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfile_Slack_S1Target_HasProcessGuard()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.Contains("Slack.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfile_Teams_S1Target_HasProcessGuard()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.Contains("Teams.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("ms-teams.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("MSTeams.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void VsCodeCacheRuleExcludesSettingsExtensionsAndWorkspaceStorage()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");

        Assert.NotEmpty(rule.RootPaths);
        Assert.All(rule.RootPaths, root =>
        {
            Assert.True(
                root.EndsWith("Cache", StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("Code Cache", StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("GPUCache", StringComparison.OrdinalIgnoreCase),
                $"Unexpected VS Code app profile root: {root}");
            Assert.DoesNotContain("extensions", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("settings", root, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("workspaceStorage", root, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Contains("Code.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Code - Insiders.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("VSCodium.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfile_VSCode_S1Target_HasProcessGuard()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.NotEmpty(rule.EffectiveProcessGuardNames);
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

            Assert.Contains("idea64.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("idea.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("rider64.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("rider.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
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
    public void AppProfile_JetBrains_S1Target_HasProcessGuard()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.jetbrains-cache");

        Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
        Assert.NotEmpty(rule.EffectiveProcessGuardNames);
    }

    [Fact]
    public void Teams_WebView2_NotBroadlyGuarded()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");

        Assert.DoesNotContain("msedgewebview2.exe", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("msedgewebview2", rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void JetBrains_ProcessGuard_Covers64AndNon64BitNames()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rule = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.jetbrains-cache");

        var expected = new[]
        {
            ("idea64.exe", "idea.exe"),
            ("pycharm64.exe", "pycharm.exe"),
            ("webstorm64.exe", "webstorm.exe"),
            ("rider64.exe", "rider.exe"),
            ("clion64.exe", "clion.exe"),
            ("datagrip64.exe", "datagrip.exe"),
            ("goland64.exe", "goland.exe"),
            ("phpstorm64.exe", "phpstorm.exe"),
            ("rubymine64.exe", "rubymine.exe"),
            ("dataspell64.exe", "dataspell.exe")
        };

        foreach (var (x64, x86) in expected)
        {
            Assert.Contains(x64, rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(x86, rule.EffectiveProcessGuardNames, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void AppProfile_BlockedIdentitySessionStorageData()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var electron = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var vscode = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");
        var jetbrains = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.jetbrains-cache");
        var required = new[] { "Local Storage", "Session Storage", "IndexedDB", "Cookies", "Login Data" };

        Assert.All(required, key =>
        {
            Assert.Contains(key, electron.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(key, vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(key, jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        });
        Assert.Contains("workspaceStorage", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("extensions", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("plugins", jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("settings", jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfile_BroadAppRoot_IsNotCleanupTarget()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var electron = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var vscode = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");

        Assert.DoesNotContain(electron.RootPaths, root =>
            root.EndsWith(Path.Combine("Discord"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Slack"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Microsoft", "Teams"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Microsoft", "MSTeams"), StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(vscode.RootPaths, root =>
            root.EndsWith(Path.Combine("Code"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Code - Insiders"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("VSCodium"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProfile_UnknownCacheLikeFolder_IsS2OrNeedsEvidence()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule =>
            rule.RootPaths.Any(root => root.Contains(Path.Combine("UnknownApp", "Cache"), StringComparison.OrdinalIgnoreCase)));
        Assert.DoesNotContain(rules, rule => rule.RuleId.Contains("zoom", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ZoomProfile_NotInRuleCatalogS0OrS1()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule => rule.RuleId.Contains("zoom", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            rules.SelectMany(rule => rule.RootPaths),
            root => root.Contains(Path.Combine("Zoom"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProfilesV1_Discord_CoverageIncludesExactCacheLogRoots()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);
        var cacheRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var logRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-logs");

        foreach (var rootName in new[] { "Discord", "DiscordCanary", "DiscordPTB" })
        {
            Assert.Contains(cacheRule.RootPaths, root => root.EndsWith(Path.Combine(rootName, "Cache"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logRule.RootPaths, root => root.EndsWith(Path.Combine(rootName, "logs"), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AppProfilesV1_Slack_CoverageIncludesExactCacheLogRoots()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);
        var cacheRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var logRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-logs");

        Assert.Contains(cacheRule.RootPaths, root => root.EndsWith(Path.Combine("Slack", "Cache"), StringComparison.OrdinalIgnoreCase));
        Assert.Contains(logRule.RootPaths, root => root.EndsWith(Path.Combine("Slack", "logs"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProfilesV1_Teams_CoverageIncludesExactCacheLogRoots()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);
        var cacheRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var logRule = Assert.Single(rules, rule => rule.RuleId == "cp.s1.electron-app-logs");

        foreach (var rootName in new[]
        {
            Path.Combine("Microsoft", "Teams"),
            Path.Combine("Microsoft", "MSTeams")
        })
        {
            Assert.Contains(cacheRule.RootPaths, root => root.EndsWith(Path.Combine(rootName, "Cache"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logRule.RootPaths, root => root.EndsWith(Path.Combine(rootName, "logs"), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AppProfilesV1_VSCode_CoverageIncludesExactCacheLogRoots()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);
        var cacheRule = Assert.Single(rules, candidate => candidate.RuleId == "cp.s1.vscode-cache");
        var logRule = Assert.Single(rules, candidate => candidate.RuleId == "cp.s1.vscode-logs");

        foreach (var appName in new[] { "Code", "Code - Insiders", "VSCodium" })
        {
            Assert.Contains(cacheRule.RootPaths, root => root.EndsWith(Path.Combine(appName, "Cache"), StringComparison.OrdinalIgnoreCase));
            Assert.Contains(logRule.RootPaths, root => root.EndsWith(Path.Combine(appName, "logs"), StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void AppProfilesV1_JetBrains_CoverageIncludesExactCacheLogRoots()
    {
        var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var localAppData = Path.Combine(root, "LocalAppData");
            Directory.CreateDirectory(Path.Combine(localAppData, "JetBrains", "Rider2026.1"));
            Directory.CreateDirectory(Path.Combine(localAppData, "JetBrains", "IdeaIC2026.1"));
            var paths = new EnvironmentPaths(Path.Combine(root, "Temp"), localAppData, Path.Combine(root, "User"));
            var rules = RuleCatalog.CreateDefault(paths);
            var cacheRule = Assert.Single(rules, candidate => candidate.RuleId == "cp.s1.jetbrains-cache");
            var logRule = Assert.Single(rules, candidate => candidate.RuleId == "cp.s1.jetbrains-logs");

            foreach (var product in new[] { "Rider2026.1", "IdeaIC2026.1" })
            {
                Assert.Contains(cacheRule.RootPaths, path => path.EndsWith(Path.Combine(product, "caches"), StringComparison.OrdinalIgnoreCase));
                Assert.Contains(logRule.RootPaths, path => path.EndsWith(Path.Combine(product, "log"), StringComparison.OrdinalIgnoreCase));
            }
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
    public void AppProfilesV1_AllS1TargetsHaveProcessGuards()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-ui-cache" or
                "cp.s1.electron-app-logs" or
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-cache" or
                "cp.s1.vscode-logs" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed" or
                "cp.s1.jetbrains-cache" or
                "cp.s1.jetbrains-logs")
            .ToArray();

        Assert.Equal(10, rules.Length);
        Assert.All(rules, rule =>
        {
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
        });
    }

    [Fact]
    public void AppProfilesV1_NoAppProfileTargetsAreS0()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule =>
            rule.RiskLevel == RiskLevel.S0VeryLowRisk
            && rule.RootPaths.Any(root =>
                root.Contains(Path.Combine("Discord"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Slack"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Microsoft", "Teams"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("MSTeams"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("Code"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("VSCodium"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("JetBrains"), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AppProfilesV1_NewS1Targets_NotQuickSafeOrS0()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-logs" or
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-logs" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed" or
                "cp.s1.jetbrains-logs")
            .ToArray();

        Assert.Equal(7, rules.Length);
        Assert.All(rules, rule =>
        {
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.False(rule.CanRunWithoutConfirmation);
        });
    }

    [Fact]
    public void AppProfilesV1_BlockedDataClassesRemainExcluded()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var electron = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var vscode = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");
        var jetbrains = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.jetbrains-cache");

        foreach (var key in new[] { "Local Storage", "Session Storage", "IndexedDB", "Cookies", "Login Data", "Web Data", "History", "Bookmarks" })
        {
            Assert.Contains(key, electron.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(key, vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(key, jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        }

        Assert.Contains("workspaceStorage", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("globalStorage", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("extensions", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("settings", vscode.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);

        Assert.Contains("plugins", jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("config", jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("settings", jetbrains.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void AppProfilesV1_UnknownCacheLikeFoldersAreS2OrNeedsEvidence()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var rules = RuleCatalog.CreateDefault(paths);

        Assert.DoesNotContain(rules, rule =>
            rule.RootPaths.Any(root =>
                root.Contains(Path.Combine("UnknownApp", "Cache"), StringComparison.OrdinalIgnoreCase)
                || root.Contains(Path.Combine("UnknownApp", "Logs"), StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void AppProfilesV1_NoBroadAppRootScanning()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var electron = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.electron-app-ui-cache");
        var vscode = Assert.Single(RuleCatalog.CreateDefault(paths), rule => rule.RuleId == "cp.s1.vscode-cache");

        Assert.DoesNotContain(electron.RootPaths, root =>
            root.EndsWith(Path.Combine("Discord"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("DiscordCanary"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("DiscordPTB"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Slack"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Microsoft", "Teams"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Microsoft", "MSTeams"), StringComparison.OrdinalIgnoreCase));

        Assert.DoesNotContain(vscode.RootPaths, root =>
            root.EndsWith(Path.Combine("Code"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("Code - Insiders"), StringComparison.OrdinalIgnoreCase)
            || root.EndsWith(Path.Combine("VSCodium"), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AppProfilesV1_CrashpadRoot_IsNotDeletedAsDirectory()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith("Crashpad", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain("*", rule.IncludeFilePatterns);
            Assert.Equal(TimeSpan.FromDays(7), rule.MinimumAge);
            Assert.False(rule.Recursive);
        });
    }

    [Fact]
    public void AppProfilesV1_CrashpadReports_AllowedDiagnosticFiles_AreS1()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.electron-app-crash-reports" or "cp.s1.vscode-crash-reports")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.Contains("*.dmp", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.mdmp", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.dump", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.log", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.txt", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "reports"), StringComparison.OrdinalIgnoreCase));
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
        });
    }

    [Fact]
    public void AppProfilesV1_CrashpadCompleted_AllowedDiagnosticFiles_AreS1()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.electron-app-crash-completed" or "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.Contains(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "completed"), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(TimeSpan.FromDays(7), rule.MinimumAge);
            Assert.DoesNotContain("*.dump", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
        });
    }

    [Fact]
    public void AppProfilesV1_CrashDiagnostics_HaveSevenDayAgeThreshold()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.Equal(4, crashRules.Length);
        Assert.All(crashRules, rule => Assert.Equal(TimeSpan.FromDays(7), rule.MinimumAge));
    }

    [Fact]
    public void AppProfilesV1_CrashpadPendingOrNew_IsNotS1()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "pending"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "new"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "uploads"), StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(rule.RootPaths, root => root.EndsWith(Path.Combine("Crashpad", "attachments"), StringComparison.OrdinalIgnoreCase));
        });
    }

    [Fact]
    public void AppProfilesV1_CrashpadUnknownExtensions_AreNotS1()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.DoesNotContain("*", rule.IncludeFilePatterns);
            Assert.DoesNotContain("*.json", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("*.sqlite", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("*.db", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("*.dat", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AppProfilesV1_CrashpadStateFiles_AreExcludedOrBlocked()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var crashRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is
                "cp.s1.electron-app-crash-reports" or
                "cp.s1.electron-app-crash-completed" or
                "cp.s1.vscode-crash-reports" or
                "cp.s1.vscode-crash-completed")
            .ToArray();

        Assert.NotEmpty(crashRules);
        Assert.All(crashRules, rule =>
        {
            Assert.Contains("metadata", rule.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("database", rule.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("databases", rule.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("settings", rule.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("config", rule.ExcludePathSegments, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("*.dat", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void AppProfilesV1_Logs_HaveSevenDayAgeThreshold()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var logRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.electron-app-logs" or "cp.s1.vscode-logs" or "cp.s1.jetbrains-logs")
            .ToArray();

        Assert.Equal(3, logRules.Length);
        Assert.All(logRules, rule =>
        {
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.Equal(TimeSpan.FromDays(7), rule.MinimumAge);
            Assert.Contains("*.log", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("*.txt", rule.IncludeFilePatterns, StringComparer.OrdinalIgnoreCase);
            Assert.False(rule.Recursive);
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
        });
    }

    [Fact]
    public void AppProfilesV1_CacheRoots_HaveProcessGuardsAndMinimumAge()
    {
        var paths = new EnvironmentPaths(@"C:\Temp", @"C:\Users\tester\AppData\Local", @"C:\Users\tester");
        var cacheRules = RuleCatalog.CreateDefault(paths)
            .Where(rule => rule.RuleId is "cp.s1.electron-app-ui-cache" or "cp.s1.vscode-cache" or "cp.s1.jetbrains-cache")
            .ToArray();

        Assert.Equal(3, cacheRules.Length);
        Assert.All(cacheRules, rule =>
        {
            Assert.Equal(RiskLevel.S1LowRisk, rule.RiskLevel);
            Assert.NotNull(rule.MinimumAge);
            Assert.True(rule.MinimumAge!.Value >= TimeSpan.FromDays(1));
            Assert.NotEmpty(rule.EffectiveProcessGuardNames);
        });
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
