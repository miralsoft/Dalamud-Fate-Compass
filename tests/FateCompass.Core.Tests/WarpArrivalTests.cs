using FateCompass.Core.Routing;

namespace FateCompass.Core.Tests;

public sealed class WarpArrivalTests
{
    /// <summary>
    /// The sequence measured in the Occult Crescent: nothing, then the warp kind for a while,
    /// then nothing again. The arrival is the last step, not the first.
    /// </summary>
    [Theory]
    [InlineData(WarpArrival.TownTranslate)]
    [InlineData(WarpArrival.Return)]
    [InlineData(WarpArrival.Teleport)]
    [InlineData(WarpArrival.EnterInstanceContent)]
    public void ArrivalIsReportedWhenTheWarpEnds(uint kind)
    {
        var warp = new WarpArrival();

        Assert.False(warp.Observe(WarpArrival.None));
        Assert.False(warp.Observe(kind));
        Assert.False(warp.Observe(kind));
        Assert.True(warp.Observe(WarpArrival.None));
        Assert.Equal(kind, warp.LastArrivalKind);
    }

    /// <summary>
    /// Once per journey. The game reports nothing for many ticks after a warp, and every one of
    /// them would otherwise look like another arrival.
    /// </summary>
    [Fact]
    public void ArrivalIsReportedOnceAndNotOnEveryQuietTickAfterwards()
    {
        var warp = new WarpArrival();

        warp.Observe(WarpArrival.TownTranslate);
        Assert.True(warp.Observe(WarpArrival.None));

        for (var i = 0; i < 20; i++)
        {
            Assert.False(warp.Observe(WarpArrival.None));
        }
    }

    /// <summary>
    /// Being raised and taking a chocobo taxi both warp the character, and neither is a journey
    /// the player chose to end here. Leaving instanced content is a departure, not an arrival
    /// anywhere this cares about.
    /// </summary>
    [Theory]
    [InlineData(8u)]   // Resurrection
    [InlineData(10u)]  // ChocoboTaxi
    [InlineData(13u)]  // LeaveInstanceContent
    [InlineData(3u)]   // Translate
    public void KindsThatAreNotAJourneyNeverReportAnArrival(uint kind)
    {
        var warp = new WarpArrival();

        Assert.False(warp.Observe(kind));
        Assert.False(warp.Observe(WarpArrival.None));
        Assert.Equal(WarpArrival.None, warp.LastArrivalKind);
    }

    /// <summary>
    /// The plugin is loaded and unloaded repeatedly within one session, and a reload that lands
    /// mid-warp must not announce an arrival that already happened or never will.
    /// </summary>
    [Fact]
    public void AFreshTrackerDoesNotReportAnArrivalFromNothing()
    {
        var warp = new WarpArrival();

        Assert.False(warp.Observe(WarpArrival.None));
        Assert.False(warp.IsTravelling);
    }

    [Fact]
    public void ResetForgetsAWarpInProgress()
    {
        var warp = new WarpArrival();

        warp.Observe(WarpArrival.Return);
        Assert.True(warp.IsTravelling);

        warp.Reset();

        Assert.False(warp.IsTravelling);
        Assert.False(warp.Observe(WarpArrival.None));
    }

    /// <summary>
    /// Two journeys in a row, which is the normal case: hop to an aetheryte, then Return.
    /// </summary>
    [Fact]
    public void EachJourneyReportsItsOwnArrival()
    {
        var warp = new WarpArrival();

        warp.Observe(WarpArrival.TownTranslate);
        Assert.True(warp.Observe(WarpArrival.None));
        Assert.Equal(WarpArrival.TownTranslate, warp.LastArrivalKind);

        warp.Observe(WarpArrival.Return);
        Assert.True(warp.Observe(WarpArrival.None));
        Assert.Equal(WarpArrival.Return, warp.LastArrivalKind);
    }

    /// <summary>
    /// One warp kind giving way directly to another without a gap. Unobserved in play, but the
    /// tracker should not invent an arrival for a journey that is still running.
    /// </summary>
    [Fact]
    public void OneWarpKindFollowedByAnotherIsNotAnArrival()
    {
        var warp = new WarpArrival();

        warp.Observe(WarpArrival.Teleport);

        Assert.False(warp.Observe(WarpArrival.EnterInstanceContent));
        Assert.True(warp.IsTravelling);
        Assert.True(warp.Observe(WarpArrival.None));
    }
}
