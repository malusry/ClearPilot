using ClearPilot.Core.Safety;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class PathSafetyEngineTests
{
    [Fact]
    public void ValidateCandidateBlocksEmptyPath()
    {
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([Path.GetTempPath()]);

        var decision = engine.ValidateCandidate(string.Empty, Path.GetTempPath(), whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("EmptyPath", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksRelativePath()
    {
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([Path.GetTempPath()]);

        var decision = engine.ValidateCandidate(@"temp\cache.tmp", Path.GetTempPath(), whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("RelativePath", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksTraversalPath()
    {
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([Path.GetTempPath()]);
        var traversal = Path.Combine(Path.GetTempPath(), "safe", "..", "escape.tmp");

        var decision = engine.ValidateCandidate(traversal, Path.GetTempPath(), whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("PathTraversal", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksDriveRoot()
    {
        var engine = CreateEngine([]);
        var driveRoot = Path.GetPathRoot(Path.GetTempPath())!;
        var whitelist = new KnownSafeCacheRootWhitelist([driveRoot]);

        var decision = engine.ValidateCandidate(driveRoot, driveRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("DriveRoot", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksUncPath()
    {
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([Path.GetTempPath()]);

        var decision = engine.ValidateCandidate(@"\\server\share\cache.tmp", Path.GetTempPath(), whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("UncPath", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksRegistryTargets()
    {
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([Path.GetTempPath()]);

        var decision = engine.ValidateCandidate(@"HKLM:\SOFTWARE\Microsoft", Path.GetTempPath(), whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("RegistryTarget", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksPathsOutsideCleanupRoot()
    {
        using var workspace = TestWorkspace.Create();
        var allowedRoot = workspace.CreateDirectory("allowed");
        var outsideRoot = workspace.CreateDirectory("outside");
        var filePath = workspace.CreateFile(Path.Combine("outside", "cache.tmp"), "123");
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([allowedRoot, outsideRoot]);

        var decision = engine.ValidateCandidate(filePath, allowedRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("OutsideCleanupRoot", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksPathOutsideKnownSafeWhitelist()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("cleanup");
        var otherRoot = workspace.CreateDirectory("other");
        var filePath = workspace.CreateFile(Path.Combine("cleanup", "cache.tmp"), "123");
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([otherRoot]);

        var decision = engine.ValidateCandidate(filePath, cleanupRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("NotInKnownSafeCacheRoot", decision.ResultCode);
    }

    [Fact]
    public void DenylistWinsOverAllowlist()
    {
        using var workspace = TestWorkspace.Create();
        var blockedRoot = workspace.CreateDirectory("blocked");
        var filePath = workspace.CreateFile(Path.Combine("blocked", "cache.tmp"), "123");
        var engine = CreateEngine([blockedRoot]);
        var whitelist = new KnownSafeCacheRootWhitelist([blockedRoot]);

        var decision = engine.ValidateCandidate(filePath, blockedRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("ProtectedRoot", decision.ResultCode);
        Assert.False(decision.DenylistAllowed);
    }

    [Fact]
    public void ValidateCandidateBlocksHardDeniedBrowserIdentitySegments()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("profile");
        var filePath = workspace.CreateFile(Path.Combine("profile", "Cookies", "cookie.bin"), "cookie");
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);

        var decision = engine.ValidateCandidate(filePath, cleanupRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("HardDeniedTarget", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksDefenderProtectedSegments()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("defender");
        var filePath = workspace.CreateFile(Path.Combine("defender", "ProtectionHistory", "history.bin"), "history");
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);

        var decision = engine.ValidateCandidate(filePath, cleanupRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("HardDeniedTarget", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksSteamLibraryAndWorkshopPaths()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("steam");
        var libraryFile = workspace.CreateFile(Path.Combine("steam", "steamapps", "common", "Game", "game.exe"), "x");
        var workshopFile = workspace.CreateFile(Path.Combine("steam", "steamapps", "workshop", "content.bin"), "x");
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);
        var engine = CreateEngine([]);

        var libraryDecision = engine.ValidateCandidate(libraryFile, cleanupRoot, whitelist);
        var workshopDecision = engine.ValidateCandidate(workshopFile, cleanupRoot, whitelist);

        Assert.False(libraryDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", libraryDecision.ResultCode);
        Assert.False(workshopDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", workshopDecision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksSteamManifestAndLibraryMetadataFiles()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("steam");
        var appManifest = workspace.CreateFile(Path.Combine("steam", "appmanifest_730.acf"), "manifest");
        var libraryFolders = workspace.CreateFile(Path.Combine("steam", "libraryfolders.vdf"), "metadata");
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);
        var engine = CreateEngine([]);

        var appManifestDecision = engine.ValidateCandidate(appManifest, cleanupRoot, whitelist);
        var libraryFoldersDecision = engine.ValidateCandidate(libraryFolders, cleanupRoot, whitelist);

        Assert.False(appManifestDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", appManifestDecision.ResultCode);
        Assert.False(libraryFoldersDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", libraryFoldersDecision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksBattleNetDataFolderPaths()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("Battle.net");
        var dataFile = workspace.CreateFile(Path.Combine("Battle.net", "Data", "agent.db"), "x");
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);
        var engine = CreateEngine([]);

        var decision = engine.ValidateCandidate(dataFile, cleanupRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("HardDeniedTarget", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksLauncherAccountSessionAndTokenPaths()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("launcher");
        var accountFile = workspace.CreateFile(Path.Combine("launcher", "Account", "token.bin"), "x");
        var sessionsFile = workspace.CreateFile(Path.Combine("launcher", "Sessions", "session.bin"), "x");
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);
        var engine = CreateEngine([]);

        var accountDecision = engine.ValidateCandidate(accountFile, cleanupRoot, whitelist);
        var sessionDecision = engine.ValidateCandidate(sessionsFile, cleanupRoot, whitelist);

        Assert.False(accountDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", accountDecision.ResultCode);
        Assert.False(sessionDecision.IsSafe);
        Assert.Equal("HardDeniedTarget", sessionDecision.ResultCode);
    }

    [Fact]
    public void RevalidateCandidateBlocksCanonicalPathChanges()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("cache");
        var filePath = workspace.CreateFile(Path.Combine("cache", "cache.tmp"), "123");
        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);
        var initial = engine.ValidateCandidate(filePath, cleanupRoot, whitelist);
        Assert.True(initial.IsSafe);

        var revalidated = engine.RevalidateCandidate(filePath, cleanupRoot, whitelist, expectedCanonicalPath: cleanupRoot);

        Assert.False(revalidated.IsSafe);
        Assert.Equal("CanonicalPathChanged", revalidated.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksSymlinkTargets()
    {
        using var workspace = TestWorkspace.Create();
        var cleanupRoot = workspace.CreateDirectory("cache");
        var targetFile = workspace.CreateFile(Path.Combine("cache", "target.tmp"), "123");
        var linkPath = Path.Combine(cleanupRoot, "link.tmp");

        try
        {
            File.CreateSymbolicLink(linkPath, targetFile);
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        var engine = CreateEngine([]);
        var whitelist = new KnownSafeCacheRootWhitelist([cleanupRoot]);

        var decision = engine.ValidateCandidate(linkPath, cleanupRoot, whitelist);

        Assert.False(decision.IsSafe);
        Assert.Equal("ReparsePoint", decision.ResultCode);
    }

    [Fact]
    public void ValidateCandidateBlocksKnownDangerousWindowsRoots()
    {
        var windowsRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var system32 = Path.Combine(windowsRoot, "System32");
        var winSxS = Path.Combine(windowsRoot, "WinSxS");
        var installer = Path.Combine(windowsRoot, "Installer");
        var whitelist = new KnownSafeCacheRootWhitelist([system32, winSxS, installer]);
        var engine = new PathSafetyEngine(ProtectedPathPolicy.CreateDefault());

        Assert.False(engine.ValidateRoot(system32, whitelist).IsSafe);
        Assert.False(engine.ValidateRoot(winSxS, whitelist).IsSafe);
        Assert.False(engine.ValidateRoot(installer, whitelist).IsSafe);
    }

    private static PathSafetyEngine CreateEngine(IReadOnlyList<string> blockedRoots)
    {
        return new PathSafetyEngine(new ProtectedPathPolicy(blockedRoots));
    }

    private sealed class TestWorkspace : IDisposable
    {
        private TestWorkspace(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TestWorkspace Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TestWorkspace(root);
        }

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath, string content)
        {
            var path = Path.Combine(Root, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
