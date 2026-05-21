using ClearPilot.Core.Safety;
using ClearPilot.Core.Rules;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class KnownSafeCacheRootWhitelistTests
{
    [Fact]
    public void ProgramFilesExactWhitelistedCacheSubdirectoryIsAllowed()
    {
        var safeCacheRoot = @"C:\Program Files\Steam\appcache\httpcache";
        var whitelist = new KnownSafeCacheRootWhitelist([safeCacheRoot]);

        Assert.True(whitelist.IsAllowed(Path.Combine(safeCacheRoot, "cache.bin")));
    }

    [Fact]
    public void ProgramFilesInstallRootIsNotAllowedWhenOnlyCacheChildIsWhitelisted()
    {
        var safeCacheRoot = @"C:\Program Files\Steam\appcache\httpcache";
        var steamRoot = @"C:\Program Files\Steam";
        var whitelist = new KnownSafeCacheRootWhitelist([safeCacheRoot]);

        Assert.False(whitelist.IsAllowed(steamRoot));
    }

    [Fact]
    public void ProgramFilesSiblingDirectoriesRemainBlockedWithoutExplicitWhitelist()
    {
        var safeCacheRoot = @"C:\Program Files\Steam\appcache\httpcache";
        var whitelist = new KnownSafeCacheRootWhitelist([safeCacheRoot]);

        Assert.False(whitelist.IsAllowed(@"C:\Program Files\Steam\steamapps"));
        Assert.False(whitelist.IsAllowed(@"C:\Program Files\Steam\steamapps\common"));
        Assert.False(whitelist.IsAllowed(@"C:\Program Files\Steam\appcache"));
    }

    [Fact]
    public void KnownSafeCacheRootWhitelist_DoesNotIncludeDownloads()
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
        var whitelist = KnownSafeCacheRootWhitelist.CreateFromRules(rules);

        Assert.DoesNotContain(
            whitelist.Roots,
            root => root.Contains(Path.Combine("Users", "tester", "Downloads"), StringComparison.OrdinalIgnoreCase)
                || root.EndsWith("Downloads", StringComparison.OrdinalIgnoreCase));
    }
}
