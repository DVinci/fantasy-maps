using FantasyMaps.Core.Rendering;
using Xunit;

namespace FantasyMaps.Core.Tests;

public class ViridisColorTests
{
    [Fact]
    public void Interpolate_AtZero_ReturnsPurple()
    {
        string color = ViridisColor.Interpolate(0.0);
        Assert.StartsWith("#", color);
        Assert.Equal(7, color.Length);
    }

    [Fact]
    public void Interpolate_AtOne_ReturnsYellow()
    {
        string color = ViridisColor.Interpolate(1.0);
        Assert.StartsWith("#", color);
        Assert.Equal(7, color.Length);
    }

    [Fact]
    public void Interpolate_MidRange_ReturnsValidHex()
    {
        for (double t = 0; t <= 1.0; t += 0.1)
        {
            string color = ViridisColor.Interpolate(t);
            Assert.Matches(@"^#[0-9a-f]{6}$", color);
        }
    }
}
