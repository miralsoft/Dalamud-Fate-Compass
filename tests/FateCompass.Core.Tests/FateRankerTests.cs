using FateCompass.Core.Configuration;
using FateCompass.Core.Model;
using FateCompass.Core.Ranking;

namespace FateCompass.Core.Tests;

public sealed class FateRankerTests
{
    private static FateCompassSettings Settings() => new();

    [Fact]
    public void EmptyZoneProducesEmptyList()
    {
        var result = FateRanker.Rank([], TestData.Player(), Settings());

        Assert.Empty(result);
    }

    [Fact]
    public void CloserFateOutranksDistantOneWhenAllElseIsEqual()
    {
        var near = TestData.Fate(id: 1, x: 50f);
        var far = TestData.Fate(id: 2, x: 400f);

        var result = FateRanker.Rank([far, near], TestData.Player(), Settings());

        Assert.Equal(1, result[0].Rank);
        Assert.Equal(near.Id, result[0].Fate.Id);
        Assert.Equal(2, result[1].Rank);
    }

    [Fact]
    public void NearlyCompleteFateIsExcludedEvenWhenItIsTheClosest()
    {
        var settings = Settings();
        var almostDone = TestData.Fate(id: 1, x: 5f, progressPercent: settings.NearlyDoneThresholdPercent);
        var fresh = TestData.Fate(id: 2, x: 300f);

        var result = FateRanker.Rank([almostDone, fresh], TestData.Player(), settings);

        var excluded = Assert.Single(result, entry => entry.Fate.Id == almostDone.Id);
        Assert.Equal(FateExclusionReason.NearlyComplete, excluded.ExclusionReason);
        Assert.Equal(0, excluded.Rank);
        Assert.Equal(1, result.Single(entry => entry.Fate.Id == fresh.Id).Rank);
    }

    [Fact]
    public void FateThatWouldExpireBeforeArrivalIsMarkedUnreachable()
    {
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 10f;

        // 1000 yalms at 10 yalms per second is 100 seconds of travel, but only 60 seconds remain.
        var tooFar = TestData.Fate(id: 1, x: 1000f, secondsRemaining: 60);

        var result = FateRanker.Rank([tooFar], TestData.Player(), settings);

        Assert.Equal(FateExclusionReason.Unreachable, result[0].ExclusionReason);
    }

    [Fact]
    public void FateBelowTheMinimumRemainingTimeIsExcludedBeforeTheReachabilityCheck()
    {
        var settings = Settings();
        settings.MinimumSecondsRemaining = 45;

        var expiring = TestData.Fate(id: 1, x: 1f, secondsRemaining: 44);

        var result = FateRanker.Rank([expiring], TestData.Player(), settings);

        Assert.Equal(FateExclusionReason.ExpiringSoon, result[0].ExclusionReason);
    }

    [Fact]
    public void FinishedFateIsNotJoinable()
    {
        var finished = TestData.Fate(id: 1, state: FateProgressState.Finished);

        var result = FateRanker.Rank([finished], TestData.Player(), Settings());

        Assert.Equal(FateExclusionReason.NotJoinable, result[0].ExclusionReason);
    }

    [Fact]
    public void AFateWaitingToBeStartedIsRankedRatherThanExcluded()
    {
        // One sitting in preparation has its full duration ahead of it and nobody has taken a
        // share of it yet, which makes it a good target, not one to skip.
        var waiting = TestData.Fate(id: 1, x: 400f, state: FateProgressState.Preparation, secondsRemaining: 0);

        var result = FateRanker.Rank([waiting], TestData.Player(), Settings());

        Assert.Equal(FateExclusionReason.None, result[0].ExclusionReason);
        Assert.Equal(1, result[0].Rank);
    }

    [Fact]
    public void ACloseWaitingFateOutranksADistantRunningOne()
    {
        // The case from the field: a FATE half a zone away was ranked above a closer one that
        // was merely waiting to be started.
        var closeWaiting = TestData.Fate(
            id: 1, x: 436f, state: FateProgressState.Preparation, secondsRemaining: 0);
        var farRunning = TestData.Fate(id: 2, x: 955f, secondsRemaining: 880);

        var result = FateRanker.Rank([farRunning, closeWaiting], TestData.Player(), Settings());

        Assert.Equal(closeWaiting.Id, result[0].Fate.Id);
    }

    [Fact]
    public void FilteredKindIsReportedAsFilteredRatherThanDropped()
    {
        var settings = Settings();
        settings.ExcludedKinds.Add(FateKind.Escort);

        var escort = TestData.Fate(id: 1, kind: FateKind.Escort);

        var result = FateRanker.Rank([escort], TestData.Player(), settings);

        Assert.Single(result);
        Assert.Equal(FateExclusionReason.Filtered, result[0].ExclusionReason);
    }

    [Fact]
    public void EveryInputFateAppearsInTheOutput()
    {
        var settings = Settings();
        settings.ExcludedKinds.Add(FateKind.Escort);

        FateSnapshot[] fates =
        [
            TestData.Fate(id: 1, x: 10f),
            TestData.Fate(id: 2, kind: FateKind.Escort),
            TestData.Fate(id: 3, state: FateProgressState.Finished),
            TestData.Fate(id: 4, progressPercent: 99),
        ];

        var result = FateRanker.Rank(fates, TestData.Player(), settings);

        Assert.Equal(4, result.Count);
        Assert.Equal([1u, 2u, 3u, 4u], result.Select(entry => entry.Fate.Id).OrderBy(id => id));
    }

    [Fact]
    public void RanksAreConsecutiveAndStartAtOne()
    {
        FateSnapshot[] fates =
        [
            TestData.Fate(id: 1, x: 300f),
            TestData.Fate(id: 2, x: 100f),
            TestData.Fate(id: 3, x: 200f),
        ];

        var result = FateRanker.Rank(fates, TestData.Player(), Settings());

        Assert.Equal([1, 2, 3], result.Where(entry => entry.IsRecommended).Select(entry => entry.Rank));
    }

    [Fact]
    public void ProgressWeightPushesAFreshFateAheadOfAClosetButAdvancedOne()
    {
        var settings = Settings();
        settings.Weights.Progress = 5f;
        settings.Weights.Distance = 1f;

        var advancedButClose = TestData.Fate(id: 1, x: 10f, progressPercent: 80);
        var freshButFar = TestData.Fate(id: 2, x: 200f, progressPercent: 0);

        var result = FateRanker.Rank([advancedButClose, freshButFar], TestData.Player(), settings);

        Assert.Equal(freshButFar.Id, result[0].Fate.Id);
    }

    [Fact]
    public void NonPositiveTravelSpeedDoesNotExcludeEverything()
    {
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 0f;

        var result = FateRanker.Rank([TestData.Fate(id: 1, x: 500f)], TestData.Player(), settings);

        // A misconfigured speed must not make the whole zone look unreachable.
        Assert.Equal(FateExclusionReason.None, result[0].ExclusionReason);
    }

    [Fact]
    public void ElevationCountsTowardsTheTravelDistance()
    {
        // Reported from play: a FATE high above the player is further away than the flat map
        // distance suggests, and ignoring that sent the player to the wrong aetheryte.
        var settings = Settings();
        settings.VerticalTravelWeight = 2f;

        var elevated = TestData.Fate(id: 1) with { Position = new WorldPosition(0f, 100f, 30f) };

        var result = FateRanker.Rank([elevated], TestData.Player(), settings);

        // 30 flat, 100 of climb weighted to 200: hypotenuse of 30 and 200.
        Assert.Equal(MathF.Sqrt((30f * 30f) + (200f * 200f)), result[0].DistanceYalms, precision: 2);
    }

    [Fact]
    public void AZeroVerticalWeightIgnoresElevationEntirely()
    {
        var settings = Settings();
        settings.VerticalTravelWeight = 0f;

        var elevated = TestData.Fate(id: 1) with { Position = new WorldPosition(0f, 500f, 30f) };

        var result = FateRanker.Rank([elevated], TestData.Player(), settings);

        Assert.Equal(30f, result[0].DistanceYalms, precision: 3);
    }

    [Fact]
    public void AHighFateRanksBelowACloserFlatOne()
    {
        var settings = Settings();
        settings.VerticalTravelWeight = 2f;

        var flat = TestData.Fate(id: 1, x: 200f);
        var high = TestData.Fate(id: 2, x: 150f) with { Position = new WorldPosition(150f, 300f, 0f) };

        var result = FateRanker.Rank([high, flat], TestData.Player(), settings);

        Assert.Equal(flat.Id, result[0].Fate.Id);
    }
}
