using FateCompass.Core.Configuration;
using FateCompass.Core.Model;
using FateCompass.Core.Routing;

namespace FateCompass.Core.Tests;

/// <summary>
/// The second leg of a route, from an aetheryte to the FATE, used to be measured flat because
/// the game data carries no height for an aetheryte. A FATE on a plateau therefore looked
/// closest to the aetheryte directly underneath it, which is the one place you cannot walk from.
/// </summary>
public sealed class AetheryteElevationTests
{
    /// <summary>One map unit is fifty yalms, the same conversion the plugin uses.</summary>
    private const float Scale = 50f;

    private static FateSnapshot FateAt(float x, float z) => TestData.Fate() with
    {
        Position = new WorldPosition(x, 0f, z),
    };

    private static PlayerSnapshot PlayerAt(float x, float z) => TestData.Player() with
    {
        Position = new WorldPosition(x, 0f, z),
    };

    /// <summary>
    /// Below the target and far above it, against beside the target and level with it. The flat
    /// distance says the first; the climb says the second, and the climb is right.
    /// </summary>
    [Fact]
    public void AClimbCanOutweighADistanceOnTheMap()
    {
        var underneath = new Aetheryte
        {
            Id = 1,
            Name = "Directly below",
            Position = new WorldPosition(10f, 0f, 10f),
            Elevation = 0f,
        };

        var alongside = new Aetheryte
        {
            Id = 2,
            Name = "Further, but level",
            Position = new WorldPosition(13f, 0f, 10f),
            Elevation = 200f,
        };

        var settings = new FateCompassSettings { VerticalTravelWeight = 2f };

        var hint = RouteHintCalculator.Calculate(
            FateAt(10f, 10f),
            PlayerAt(30f, 30f),
            [underneath, alongside],
            settings,
            Scale,
            directDistanceYalms: 2000f,
            fateElevation: 200f);

        Assert.NotNull(hint);
        Assert.Equal(alongside.Id, hint.NearestAetheryte.Id);
    }

    /// <summary>
    /// Without the height the old answer stands, which is what every aetheryte the player has
    /// not walked past yet still gets.
    /// </summary>
    [Fact]
    public void WithoutAKnownHeightTheFlatAnswerStands()
    {
        var underneath = new Aetheryte
        {
            Id = 1,
            Name = "Directly below",
            Position = new WorldPosition(10f, 0f, 10f),
        };

        var alongside = new Aetheryte
        {
            Id = 2,
            Name = "Further, but level",
            Position = new WorldPosition(13f, 0f, 10f),
        };

        var hint = RouteHintCalculator.Calculate(
            FateAt(10f, 10f),
            PlayerAt(30f, 30f),
            [underneath, alongside],
            new FateCompassSettings { VerticalTravelWeight = 2f },
            Scale,
            directDistanceYalms: 2000f,
            fateElevation: 200f);

        Assert.NotNull(hint);
        Assert.Equal(underneath.Id, hint.NearestAetheryte.Id);
    }

    /// <summary>
    /// One side knowing its height is not enough. Half a comparison is worse than none, because
    /// it would rank a measured aetheryte against an unmeasured one on different terms.
    /// </summary>
    [Fact]
    public void AHeightOnOnlyOneSideChangesNothing()
    {
        var known = new Aetheryte
        {
            Id = 1,
            Name = "Measured",
            Position = new WorldPosition(10f, 0f, 10f),
            Elevation = 0f,
        };

        var hint = RouteHintCalculator.Calculate(
            FateAt(10f, 10f),
            PlayerAt(30f, 30f),
            [known],
            new FateCompassSettings { VerticalTravelWeight = 2f },
            Scale,
            directDistanceYalms: 2000f,
            fateElevation: null);

        Assert.NotNull(hint);
        Assert.Equal(0f, hint.AetheryteToFateYalms, 3);
    }

    /// <summary>
    /// The climb lengthens the leg, so a route over a hill is no longer sold as a short one.
    /// </summary>
    [Fact]
    public void AClimbMakesTheLegLonger()
    {
        var aetheryte = new Aetheryte
        {
            Id = 1,
            Name = "Below",
            Position = new WorldPosition(10f, 0f, 10f),
            Elevation = 0f,
        };

        RouteHint Measure(float? fateHeight) => RouteHintCalculator.Calculate(
            FateAt(12f, 10f),
            PlayerAt(30f, 30f),
            [aetheryte],
            new FateCompassSettings { VerticalTravelWeight = 2f },
            Scale,
            directDistanceYalms: 2000f,
            fateElevation: fateHeight)!;

        var flat = Measure(0f);
        var uphill = Measure(150f);

        Assert.Equal(100f, flat.AetheryteToFateYalms, 3);
        Assert.True(
            uphill.AetheryteToFateYalms > flat.AetheryteToFateYalms,
            $"A climb of 150 yalms should lengthen the leg, but {uphill.AetheryteToFateYalms} is not more than {flat.AetheryteToFateYalms}.");
    }

    /// <summary>
    /// Turning the weight off restores the flat measure exactly, so somebody who dislikes the
    /// behaviour has a way back rather than an approximation of one.
    /// </summary>
    [Fact]
    public void AZeroWeightIsTheOldBehaviourExactly()
    {
        var aetheryte = new Aetheryte
        {
            Id = 1,
            Name = "Below",
            Position = new WorldPosition(10f, 0f, 10f),
            Elevation = 0f,
        };

        var hint = RouteHintCalculator.Calculate(
            FateAt(12f, 10f),
            PlayerAt(30f, 30f),
            [aetheryte],
            new FateCompassSettings { VerticalTravelWeight = 0f },
            Scale,
            directDistanceYalms: 2000f,
            fateElevation: 500f);

        Assert.NotNull(hint);
        Assert.Equal(100f, hint.AetheryteToFateYalms, 3);
    }
}
