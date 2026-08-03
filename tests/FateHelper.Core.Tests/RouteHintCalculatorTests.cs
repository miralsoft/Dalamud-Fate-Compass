using FateHelper.Core.Configuration;
using FateHelper.Core.Routing;

namespace FateHelper.Core.Tests;

public sealed class RouteHintCalculatorTests
{
    private static FateHelperSettings Settings() => new();

    [Fact]
    public void ReturnsNullWhenTheZoneHasNoAetherytes()
    {
        var hint = RouteHintCalculator.Calculate(
            TestData.Fate(),
            TestData.Player(),
            [],
            Settings());

        Assert.Null(hint);
    }

    [Fact]
    public void PicksTheAetheryteNearestToTheFateNotToThePlayer()
    {
        var fate = TestData.Fate(x: 1000f);
        var player = TestData.Player(x: 0f);

        Aetheryte[] aetherytes =
        [
            TestData.Aetheryte(id: 1, name: "Next to the player", x: 10f),
            TestData.Aetheryte(id: 2, name: "Next to the FATE", x: 950f),
        ];

        var hint = RouteHintCalculator.Calculate(fate, player, aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.Equal(2u, hint.NearestAetheryte.Id);
        Assert.Equal(50f, hint.AetheryteToFateYalms, precision: 3);
    }

    [Fact]
    public void TeleportIsNotRecommendedWhenTheFateIsAlreadyClose()
    {
        var fate = TestData.Fate(x: 30f);
        var player = TestData.Player(x: 0f);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 25f)];

        var hint = RouteHintCalculator.Calculate(fate, player, aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.False(hint.TeleportIsFaster);
    }

    [Fact]
    public void TeleportIsRecommendedAcrossALongDistance()
    {
        var fate = TestData.Fate(x: 2000f);
        var player = TestData.Player(x: 0f);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 1990f)];

        var hint = RouteHintCalculator.Calculate(fate, player, aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.True(hint.TeleportIsFaster);
    }

    [Fact]
    public void TooLateWinsOverWhichRouteIsFaster()
    {
        // Once neither route arrives in time, saying "teleport is quicker" would send the
        // player through a loading screen to watch the FATE end.
        var settings = Settings();
        settings.TravelSpeedYalmsPerSecond = 10f;

        // Direct route is 500s, the teleport route 15s of overhead plus 10s of travel. Twenty
        // seconds left beats neither.
        var fate = TestData.Fate(x: 5000f, secondsRemaining: 20);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 4900f)];

        var hint = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, settings);

        Assert.NotNull(hint);
        Assert.Equal(TeleportVerdict.TooLate, hint.Verdict);
    }

    [Fact]
    public void WorthwhileWhenTheTeleportSavesTimeAndArrivesInTime()
    {
        var fate = TestData.Fate(x: 2000f, secondsRemaining: 900);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 1990f)];

        var hint = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.Equal(TeleportVerdict.Worthwhile, hint.Verdict);
        Assert.True(hint.SecondsSaved > 0f);
    }

    [Fact]
    public void TravelIsFasterNearAnAetheryte()
    {
        var fate = TestData.Fate(x: 30f, secondsRemaining: 900);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 25f)];

        var hint = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.Equal(TeleportVerdict.TravelIsFaster, hint.Verdict);
        Assert.True(hint.SecondsSaved < 0f);
    }

    [Fact]
    public void DistancesScaleWithTheSuppliedUnit()
    {
        // Aetheryte positions arrive in map coordinates, so the caller states the conversion.
        var fate = TestData.Fate(x: 10f);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 0f)];

        var plain = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, Settings());
        var scaled = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, Settings(), 50f);

        Assert.NotNull(plain);
        Assert.NotNull(scaled);
        Assert.Equal(plain.AetheryteToFateYalms * 50f, scaled.AetheryteToFateYalms, precision: 2);
    }

    [Fact]
    public void HeightAboveAFateCanMakeTheTeleportWorthwhile()
    {
        // Reported from play: standing high above a FATE, the flat distance said travelling was
        // faster while the climb made the teleport the better option. The caller supplies the
        // world-space distance so the verdict can see the height.
        var fate = TestData.Fate(x: 300f, secondsRemaining: 900);
        Aetheryte[] aetherytes = [TestData.Aetheryte(id: 1, x: 290f)];

        var flat = RouteHintCalculator.Calculate(
            fate, TestData.Player(), aetherytes, Settings());

        // Same layout, but the player is far above: the real travel distance is much larger.
        var withHeight = RouteHintCalculator.Calculate(
            fate, TestData.Player(), aetherytes, Settings(), 1f, directDistanceYalms: 1200f);

        Assert.NotNull(flat);
        Assert.NotNull(withHeight);
        Assert.Equal(TeleportVerdict.TravelIsFaster, flat.Verdict);
        Assert.Equal(TeleportVerdict.Worthwhile, withHeight.Verdict);
    }

    [Fact]
    public void TiesAreBrokenDeterministicallyByAetheryteId()
    {
        var fate = TestData.Fate(x: 0f);
        Aetheryte[] aetherytes =
        [
            TestData.Aetheryte(id: 7, x: 100f),
            TestData.Aetheryte(id: 3, x: 100f),
        ];

        var hint = RouteHintCalculator.Calculate(fate, TestData.Player(), aetherytes, Settings());

        Assert.NotNull(hint);
        Assert.Equal(3u, hint.NearestAetheryte.Id);
    }
}
