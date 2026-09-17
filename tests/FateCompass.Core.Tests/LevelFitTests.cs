using FateCompass.Core.Configuration;
using FateCompass.Core.Model;
using FateCompass.Core.Ranking;

namespace FateCompass.Core.Tests;

/// <summary>
/// The level bands, and the cases where no honest answer exists.
/// </summary>
/// <remarks>
/// <para>
/// Every FATE level and band top named in this file was read out of the game's own Fate sheet
/// on 2026-09-16, with Lumina against the installed client, not written from memory (T-07).
/// The four that appear here are real rows:
/// </para>
/// <list type="bullet">
///   <item>Sprig Cleaning, level 30, band top 35.</item>
///   <item>The Serpentlord Seethes, level 100, band top 104, which is the modern shape.</item>
///   <item>Excitable Boys, level 60, band top 60, a FATE with no band at all.</item>
///   <item>An Eureka row at level 1 with band top 255, the sentinel for "no cap".</item>
/// </list>
/// <para>
/// The thresholds under test are not read out of anything, because there is nothing to read: the
/// game carries no minimum level for a FATE. They are the shipped defaults, and the tests
/// construct them rather than inheriting them (T-05).
/// </para>
/// </remarks>
public sealed class LevelFitTests
{
    private static FateCompassSettings Settings(
        bool enabled = true,
        int marginal = 5,
        int tight = 10) => new()
        {
            LevelFitEnabled = enabled,
            LevelFitMarginalBelow = marginal,
            LevelFitTightBelow = tight,
        };

    [Theory]
    [InlineData(100, LevelFit.Comfortable)]   // exactly at it
    [InlineData(120, LevelFit.Comfortable)]   // far above, level sync handles it
    [InlineData(99, LevelFit.Marginal)]       // one under
    [InlineData(95, LevelFit.Marginal)]       // five under, the last of the band
    [InlineData(94, LevelFit.Tight)]          // six under, the first of the next
    [InlineData(90, LevelFit.Tight)]          // ten under, the last of that band
    [InlineData(89, LevelFit.OutOfReach)]     // eleven under
    [InlineData(1, LevelFit.OutOfReach)]
    public void BandsAreDecidedByHowFarUnderThePlayerIs(ushort playerLevel, LevelFit expected)
    {
        // The Serpentlord Seethes: level 100, band to 104.
        var fate = TestData.Fate(level: 100, maxLevel: 104);
        var player = TestData.Player(level: playerLevel);

        Assert.Equal(expected, LevelFitEvaluator.Evaluate(fate, player, Settings()));
    }

    [Theory]
    [InlineData(ContentKind.Eureka)]
    [InlineData(ContentKind.Bozja)]
    [InlineData(ContentKind.OccultCrescent)]
    public void ExploratoryZonesAreNeverJudged(ContentKind content)
    {
        // The trap this guards. An Eureka notorious monster is an ordinary FATE and arrives
        // through the FATE table with ActivitySource.Fate, so a check on the source would let it
        // through. There an elemental level decides the fight, and the job level says nothing.
        var fate = TestData.Fate(level: 60, source: ActivitySource.Fate);
        var player = TestData.Player(level: 5, content: content);

        Assert.Equal(
            LevelFit.NotApplicable,
            LevelFitEvaluator.Evaluate(fate, player, Settings()));
    }

    [Fact]
    public void DynamicEventsAreNeverJudged()
    {
        var fate = TestData.Fate(level: 60, source: ActivitySource.DynamicEvent);
        var player = TestData.Player(level: 20);

        Assert.Equal(
            LevelFit.NotApplicable,
            LevelFitEvaluator.Evaluate(fate, player, Settings()));
    }

    [Fact]
    public void SwitchedOffMeansNoOpinionAtAll()
    {
        var fate = TestData.Fate(level: 100);
        var player = TestData.Player(level: 1);

        Assert.Equal(
            LevelFit.NotApplicable,
            LevelFitEvaluator.Evaluate(fate, player, Settings(enabled: false)));
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(50, 0)]
    public void AnUnknownLevelOnEitherSideIsNotGuessedAt(ushort playerLevel, ushort fateLevel)
    {
        // A snapshot taken before the game had an answer. Zero is not level zero.
        var fate = TestData.Fate(level: fateLevel);
        var player = TestData.Player(level: playerLevel);

        Assert.Equal(
            LevelFit.NotApplicable,
            LevelFitEvaluator.Evaluate(fate, player, Settings()));
    }

    [Fact]
    public void ThresholdsInTheWrongOrderDoNotSwallowTheMiddleBand()
    {
        // Somebody drags the upper bound below the lower one. Read literally that would make
        // every under-levelled FATE out of reach without saying why.
        var settings = Settings(marginal: 10, tight: 3);
        var fate = TestData.Fate(level: 50);

        Assert.Equal(
            LevelFit.Marginal,
            LevelFitEvaluator.Evaluate(fate, TestData.Player(level: 45), settings));
        Assert.Equal(
            LevelFit.OutOfReach,
            LevelFitEvaluator.Evaluate(fate, TestData.Player(level: 39), settings));
    }

    [Fact]
    public void TheThresholdsAreHonoured()
    {
        // The bands follow the settings rather than the shipped defaults, which is the whole
        // reason they are settings: there is no published figure to hard-code.
        var settings = Settings(marginal: 2, tight: 4);
        var fate = TestData.Fate(level: 50);

        Assert.Equal(
            LevelFit.Marginal,
            LevelFitEvaluator.Evaluate(fate, TestData.Player(level: 48), settings));
        Assert.Equal(
            LevelFit.Tight,
            LevelFitEvaluator.Evaluate(fate, TestData.Player(level: 46), settings));
        Assert.Equal(
            LevelFit.OutOfReach,
            LevelFitEvaluator.Evaluate(fate, TestData.Player(level: 45), settings));
    }

    [Fact]
    public void LevelsBelowCountsOnlyDownwards()
    {
        var fate = TestData.Fate(level: 100);

        Assert.Equal(12, LevelFitEvaluator.LevelsBelow(fate, TestData.Player(level: 88)));
        Assert.Equal(0, LevelFitEvaluator.LevelsBelow(fate, TestData.Player(level: 100)));
        Assert.Equal(0, LevelFitEvaluator.LevelsBelow(fate, TestData.Player(level: 120)));
    }

    [Fact]
    public void OutOfReachIsSetAsideWithItsOwnReason()
    {
        var settings = Settings();
        var fate = TestData.Fate(level: 100, maxLevel: 104);
        var player = TestData.Player(level: 40);

        var ranked = FateRanker.Rank([fate], player, settings).Single();

        Assert.Equal(FateExclusionReason.LevelTooLow, ranked.ExclusionReason);
        Assert.False(ranked.IsRecommended);
        Assert.Equal(LevelFit.OutOfReach, ranked.LevelFit);
        Assert.Equal(60, ranked.LevelsBelow);
    }

    [Fact]
    public void AHardButPossibleFateStaysInTheRunningOrder()
    {
        // The point of having a middle band at all. This one is marked and kept, not set aside.
        var settings = Settings();
        var fate = TestData.Fate(level: 100, maxLevel: 104);
        var player = TestData.Player(level: 92);

        var ranked = FateRanker.Rank([fate], player, settings).Single();

        Assert.Equal(FateExclusionReason.None, ranked.ExclusionReason);
        Assert.True(ranked.IsRecommended);
        Assert.Equal(LevelFit.Tight, ranked.LevelFit);
        Assert.Equal(1, ranked.Rank);
    }

    [Fact]
    public void SwitchedOffNothingIsSetAside()
    {
        var settings = Settings(enabled: false);
        var fate = TestData.Fate(level: 100);
        var player = TestData.Player(level: 1);

        var ranked = FateRanker.Rank([fate], player, settings).Single();

        Assert.Equal(FateExclusionReason.None, ranked.ExclusionReason);
        Assert.Equal(LevelFit.NotApplicable, ranked.LevelFit);
    }
}
