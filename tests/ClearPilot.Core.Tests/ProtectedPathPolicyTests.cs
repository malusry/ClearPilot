using ClearPilot.Core.Safety;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class ProtectedPathPolicyTests
{
    [Fact]
    public void IsBlockedReturnsTrueForBlockedRoot()
    {
        var blockedRoot = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", "BlockedRoot");
        var policy = new ProtectedPathPolicy([blockedRoot]);

        Assert.True(policy.IsBlocked(blockedRoot));
    }

    [Fact]
    public void IsBlockedReturnsTrueForChildOfBlockedRoot()
    {
        var blockedRoot = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests", "BlockedRoot");
        var policy = new ProtectedPathPolicy([blockedRoot]);
        var child = Path.Combine(blockedRoot, "nested", "file.tmp");

        Assert.True(policy.IsBlocked(child));
    }

    [Fact]
    public void IsBlockedReturnsFalseForSiblingPath()
    {
        var basePath = Path.Combine(Path.GetTempPath(), "ClearPilot.Tests");
        var policy = new ProtectedPathPolicy([Path.Combine(basePath, "BlockedRoot")]);
        var sibling = Path.Combine(basePath, "BlockedRootSibling", "file.tmp");

        Assert.False(policy.IsBlocked(sibling));
    }

    [Fact]
    public void DefaultPolicyBlocksRegressionSensitiveRoots()
    {
        var policy = ProtectedPathPolicy.CreateDefault();
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(userProfile, "Downloads");
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        var paths = new[]
        {
            Path.Combine(windows, "System32"),
            Path.Combine(windows, "SysWOW64"),
            Path.Combine(windows, "Installer"),
            Path.Combine(windows, "WinSxS"),
            programFiles,
            programFilesX86,
            programData,
            userProfile,
            desktop,
            documents,
            downloads
        }.Where(path => !string.IsNullOrWhiteSpace(path));

        Assert.All(paths, path => Assert.True(policy.IsBlocked(path), path));
    }
}
