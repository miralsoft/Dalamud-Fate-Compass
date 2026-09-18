using FateCompass.Core.Configuration;
using FateCompass.Core.Model;
using FateCompass.Core.Ranking;

namespace FateCompass.Core.Tests;

/// <summary>
/// Levelling mode: the level fit deciding the order rather than only the colour.
/// </summary>
public sealed class LevellingOrderTests
{
    private static FateCompassSettings Settings(bool levellingMode) => new()
    {
        LevelFitEnabled = true,
        LevelFitIdealBand = 3,
        LevelFitMarginalBelow = 6,
        LevelFitTightBelow = 10,
        LevelFitShowFarBelow = true,
        LevelFitFarBelowBy = 10,
        LevelFitOrdersList = levellingMode,
    };

    /// <summary>
    /// A FATE at the player's level, but further away than the one far below it.
    /// </summary>
    private static FateSnapshot AtMyLevel() => TestData.Fate(id: 1, level: 50, x: 300f);

    /// <summary>Far below the player and right next to them.</summary>
    private static FateSnapshot FarBelowButClose() => TestData.Fate(id: 2, level: 20, x: 10f);

    [Fact]
    public void WithoutTheModeTheNearOneLeads()
    {
        // The ordinary order: distance carries it, and thirty levels of difference say nothing
        // about where to walk.
        var ranked = FateRanker.Rank(
            [AtMyLevel(), FarBelowButClose()],
            TestData.Player(level: 50),
            Settings(levellingMode: false));

        Assert.Equal(2u, ranked[0].Fate.Id);
        Assert.Equal(1, ranked[0].Rank);
    }

    [Fact]
    public void WithTheModeTheOneAtMyLevelLeadsDespiteTheWalk()
    {
        var ranked = FateRanker.Rank(
            [AtMyLevel(), FarBelowButClose()],
            TestData.Player(level: 50),
            Settings(levellingMode: true));

        Assert.Equal(1u, ranked[0].Fate.Id);
        Assert.Equal(LevelFit.Ideal, ranked[0].LevelFit);
        Assert.Equal(LevelFit.FarBelow, ranked[1].LevelFit);
    }

    [Fact]
    public void TheModeNeverReachesTheExploratoryZones()
    {
        // No comparison exists in Eureka, so none may move a FATE up or down there. Every entry
        // scores NotApplicable and the order falls back to what it would have been.
        var player = TestData.Player(level: 50, content: ContentKind.Eureka);

        var withMode = FateRanker.Rank(
            [AtMyLevel(), FarBelowButClose()], player, Settings(levellingMode: true));
        var withoutMode = FateRanker.Rank(
            [AtMyLevel(), FarBelowButClose()], player, Settings(levellingMode: false));

        Assert.Equal(withoutMode[0].Fate.Id, withMode[0].Fate.Id);
        Assert.Equal(withoutMode[1].Fate.Id, withMode[1].Fate.Id);
        Assert.All(withMode, entry => Assert.Equal(LevelFit.NotApplicable, entry.LevelFit));
    }

    [Fact]
    public void TheModeCannotRescueAFateThatIsAboutToExpire()
    {
        // The claim the help text makes, locked down. A FATE at the player's level with seconds
        // left is still not the one to walk to, however much the mode likes its level.
        var expiring = TestData.Fate(id: 1, level: 50, x: 200f, secondsRemaining: 40);
        var healthy = TestData.Fate(id: 2, level: 20, x: 200f, secondsRemaining: 900);

        var ranked = FateRanker.Rank(
            [expiring, healthy],
            TestData.Player(level: 50),
            Settings(levellingMode: true));

        Assert.Equal(2u, ranked[0].Fate.Id);
    }
}
