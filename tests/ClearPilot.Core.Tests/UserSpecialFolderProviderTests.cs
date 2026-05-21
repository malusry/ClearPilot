using ClearPilot.Core.Analysis;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class UserSpecialFolderProviderTests
{
    [Fact]
    public void UserSpecialFolderProvider_FallsBackToUserProfileDownloads()
    {
        var userProfile = @"C:\Users\tester";
        var expectedDownloads = Path.Combine(userProfile, "Downloads");
        var provider = new UserSpecialFolderProvider(
            knownFolderDownloadsPathResolver: () => null,
            userProfilePathResolver: () => userProfile,
            directoryExists: path => PathEquals(path, expectedDownloads));

        var resolved = provider.TryGetDownloadsPath();

        Assert.Equal(NormalizePath(expectedDownloads), resolved);
    }

    [Fact]
    public void UserSpecialFolderProvider_KnownFolderRedirectedDownloads()
    {
        var redirectedDownloads = @"D:\UserData\DownloadsRedirected";
        var provider = new UserSpecialFolderProvider(
            knownFolderDownloadsPathResolver: () => redirectedDownloads,
            userProfilePathResolver: () => @"C:\Users\tester",
            directoryExists: path => PathEquals(path, redirectedDownloads));

        var resolved = provider.TryGetDownloadsPath();

        Assert.Equal(NormalizePath(redirectedDownloads), resolved);
    }

    [Fact]
    public void UserSpecialFolderProvider_MissingOrUnavailable_ReturnsNull()
    {
        var provider = new UserSpecialFolderProvider(
            knownFolderDownloadsPathResolver: () => null,
            userProfilePathResolver: () => @"C:\Users\tester",
            directoryExists: _ => false);

        var resolved = provider.TryGetDownloadsPath();

        Assert.Null(resolved);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}

