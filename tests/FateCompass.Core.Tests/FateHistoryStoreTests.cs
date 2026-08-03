using FateCompass.Core.Configuration;
using FateCompass.Core.History;

namespace FateCompass.Core.Tests;

public sealed class FateHistoryStoreTests
{
    private static readonly DateTimeOffset Start = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private static FateCompassSettings Settings() => new();

    [Fact]
    public void WithholdsAnEstimateUntilThereAreEnoughSightings()
    {
        var settings = Settings();
        settings.MinimumSightingsForEstimate = 3;

        var store = new FateHistoryStore(settings);
        store.Record(100, 1, Start);
        store.Record(100, 1, Start.AddMinutes(20));

        // Two sightings is one interval. Reporting that as an average would dress a guess up
        // as knowledge.
        Assert.Null(store.EstimateFor(100));
    }

    [Fact]
    public void AveragesTheObservedIntervals()
    {
        var settings = Settings();
        settings.MinimumSightingsForEstimate = 3;

        var store = new FateHistoryStore(settings);
        store.Record(100, 1, Start);
        store.Record(100, 1, Start.AddMinutes(10));
        store.Record(100, 1, Start.AddMinutes(30));

        var estimate = store.EstimateFor(100);

        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.FromMinutes(15), estimate.AverageInterval);
        Assert.Equal(2, estimate.SampleCount);
        Assert.Equal(Start.AddMinutes(45), estimate.ExpectedNextAt);
    }

    [Fact]
    public void IgnoresARepeatedRecordForTheSameAppearance()
    {
        var store = new FateHistoryStore(Settings());
        store.Record(100, 1, Start);
        store.Record(100, 1, Start);
        store.Record(100, 1, Start);

        Assert.Single(store.SightingsFor(100));
    }

    [Fact]
    public void DropsTheOldestSightingsBeyondTheConfiguredDepth()
    {
        var settings = Settings();
        settings.HistoryDepthPerFate = 3;

        var store = new FateHistoryStore(settings);
        for (var i = 0; i < 6; i++)
        {
            store.Record(100, 1, Start.AddMinutes(i * 10));
        }

        var sightings = store.SightingsFor(100);

        Assert.Equal(3, sightings.Count);
        Assert.Equal(Start.AddMinutes(30), sightings[0].SeenAt);
        Assert.Equal(Start.AddMinutes(50), sightings[^1].SeenAt);
    }

    [Fact]
    public void RecordsNothingWhenHistoryTrackingIsOff()
    {
        var settings = Settings();
        settings.TrackHistory = false;

        var store = new FateHistoryStore(settings);
        store.Record(100, 1, Start);

        Assert.Empty(store.SightingsFor(100));
    }

    [Fact]
    public void KeepsFatesApartByDefinitionId()
    {
        var store = new FateHistoryStore(Settings());
        store.Record(100, 1, Start);
        store.Record(200, 1, Start.AddMinutes(5));

        Assert.Single(store.SightingsFor(100));
        Assert.Single(store.SightingsFor(200));
    }

    [Fact]
    public void ExportAndLoadRoundTrip()
    {
        var store = new FateHistoryStore(Settings());
        store.Record(100, 1, Start);
        store.Record(100, 1, Start.AddMinutes(10));
        store.Record(200, 2, Start.AddMinutes(5));

        var exported = store.Export();

        var restored = new FateHistoryStore(Settings());
        restored.Load(exported);

        Assert.Equal(2, restored.SightingsFor(100).Count);
        Assert.Single(restored.SightingsFor(200));
        Assert.Equal(exported.Count, restored.Export().Count);
    }

    [Fact]
    public void UnknownFateHasNoSightingsAndNoEstimate()
    {
        var store = new FateHistoryStore(Settings());

        Assert.Empty(store.SightingsFor(999));
        Assert.Null(store.EstimateFor(999));
    }

    [Fact]
    public void TimeUntilExpectedIsClampedAtZeroOnceThePredictionHasPassed()
    {
        var settings = Settings();
        settings.MinimumSightingsForEstimate = 3;

        var store = new FateHistoryStore(settings);
        store.Record(100, 1, Start);
        store.Record(100, 1, Start.AddMinutes(10));
        store.Record(100, 1, Start.AddMinutes(20));

        var estimate = store.EstimateFor(100);

        Assert.NotNull(estimate);
        Assert.Equal(TimeSpan.Zero, estimate.TimeUntilExpected(Start.AddHours(5)));
    }
}
