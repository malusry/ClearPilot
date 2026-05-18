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
}
