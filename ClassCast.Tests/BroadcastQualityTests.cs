using ClassCast.Common.Media;

namespace ClassCast.Tests;

/// <summary>
/// Verifies the selectable broadcast quality ladder used by the Teacher Server.
/// </summary>
public class BroadcastQualityTests
{
    [Fact]
    public void All_ContainsTheFourTiers()
    {
        Assert.Equal(4, BroadcastQuality.All.Count);
        Assert.Equal(new[] { "10Mb", "56Mb", "100Mb", "1000Mb" },
            BroadcastQuality.All.Select(q => q.Key).ToArray());
    }

    [Fact]
    public void All_IsOrderedByIncreasingResolutionAndFrameRate()
    {
        for (int i = 1; i < BroadcastQuality.All.Count; i++)
        {
            BroadcastQuality lower = BroadcastQuality.All[i - 1];
            BroadcastQuality higher = BroadcastQuality.All[i];
            Assert.True(higher.Width >= lower.Width);
            Assert.True(higher.Height >= lower.Height);
            Assert.True(higher.Fps >= lower.Fps);
            // Lower JPEG -q:v means better quality, so higher tiers compress less.
            Assert.True(higher.JpegQuality <= lower.JpegQuality);
        }
    }

    [Fact]
    public void Default_IsTheWiFiBaseline()
    {
        Assert.Equal("56Mb", BroadcastQuality.Default.Key);
        Assert.Equal(854, BroadcastQuality.Default.Width);
        Assert.Equal(15, BroadcastQuality.Default.Fps);
    }

    [Theory]
    [InlineData("100Mb", "100Mb")]
    [InlineData("1000mb", "1000Mb")] // case-insensitive
    public void FromKey_ResolvesKnownKeys(string key, string expected)
    {
        Assert.Equal(expected, BroadcastQuality.FromKey(key).Key);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    public void FromKey_FallsBackToDefault(string? key)
    {
        Assert.Equal(BroadcastQuality.Default.Key, BroadcastQuality.FromKey(key).Key);
    }
}
