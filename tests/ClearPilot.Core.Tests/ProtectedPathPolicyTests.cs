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
}
