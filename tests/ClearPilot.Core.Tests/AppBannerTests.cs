using ClearPilot.Core;
using Xunit;

namespace ClearPilot.Core.Tests;

public sealed class AppBannerTests
{
    [Fact]
    public void CreateReturnsProductName()
    {
        Assert.Equal("ClearPilot", AppBanner.Create());
    }
}
