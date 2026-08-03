using FateHelper.Core.Configuration;
using FateHelper.Core.Model;
using FateHelper.Core.Ranking;

namespace FateHelper.Core.Tests;

public sealed class FateFilterTests
{
    [Fact]
    public void UnknownKindIsNeverHiddenByTheKindFilter()
    {
        // A gap in classification must not silently hide content from the player.
        var settings = new FateHelperSettings();
        settings.ExcludedKinds.Add(FateKind.Unknown);

        var fate = TestData.Fate(kind: FateKind.Unknown);

        Assert.True(FateFilter.IsIncluded(fate, settings));
    }

    [Fact]
    public void ExcludedKindIsHidden()
    {
        var settings = new FateHelperSettings();
        settings.ExcludedKinds.Add(FateKind.Collect);

        Assert.False(FateFilter.IsIncluded(TestData.Fate(kind: FateKind.Collect), settings));
    }

    [Theory]
    [InlineData(20, false)]
    [InlineData(29, false)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    [InlineData(60, true)]
    [InlineData(61, false)]
    public void LevelRangeIsInclusiveAtBothEnds(ushort level, bool expected)
    {
        var settings = new FateHelperSettings { MinimumLevel = 30, MaximumLevel = 60 };
        var fate = TestData.Fate(level: level);

        Assert.Equal(expected, FateFilter.IsIncluded(fate, settings));
    }

    [Fact]
    public void NoBoundsMeansEverythingPasses()
    {
        var settings = new FateHelperSettings();

        Assert.True(FateFilter.IsIncluded(TestData.Fate(level: 1), settings));
        Assert.True(FateFilter.IsIncluded(TestData.Fate(level: 100), settings));
    }
}
