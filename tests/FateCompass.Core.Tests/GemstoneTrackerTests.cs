using FateCompass.Core.Configuration;
using FateCompass.Core.Gemstones;

namespace FateCompass.Core.Tests;

public sealed class GemstoneTrackerTests
{
    private static FateCompassSettings Settings(int headroom = 150) =>
        new() { GemstoneWarningHeadroom = headroom };

    [Fact]
    public void FullPurseIsReportedAsFull()
    {
        Assert.Equal(GemstoneStatus.Full, GemstoneTracker.Evaluate(1500, 1500, Settings()));
    }

    [Fact]
    public void OverfullPurseIsStillJustFull()
    {
        Assert.Equal(GemstoneStatus.Full, GemstoneTracker.Evaluate(1600, 1500, Settings()));
    }

    [Fact]
    public void WarnsOnceHeadroomReachesTheThreshold()
    {
        Assert.Equal(GemstoneStatus.Warning, GemstoneTracker.Evaluate(1350, 1500, Settings(150)));
    }

    [Fact]
    public void StaysQuietAboveTheThreshold()
    {
        Assert.Equal(GemstoneStatus.Ok, GemstoneTracker.Evaluate(1349, 1500, Settings(150)));
    }

    [Fact]
    public void UnknownCapIsTreatedAsNoProblemRatherThanAWarning()
    {
        // The adapter reports zero when it could not read the cap. Warning on that would nag
        // the player over a read failure (S-09, fail closed but quiet).
        Assert.Equal(GemstoneStatus.Ok, GemstoneTracker.Evaluate(900, 0, Settings()));
    }

    [Theory]
    [InlineData(0, 1500, 1500)]
    [InlineData(1400, 1500, 100)]
    [InlineData(1500, 1500, 0)]
    [InlineData(1700, 1500, 0)]
    [InlineData(500, 0, 0)]
    public void HeadroomIsNeverNegative(int current, int cap, int expected)
    {
        Assert.Equal(expected, GemstoneTracker.Headroom(current, cap));
    }
}
