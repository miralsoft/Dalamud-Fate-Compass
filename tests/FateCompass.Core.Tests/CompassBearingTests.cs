using FateCompass.Core.Model;
using FateCompass.Core.Routing;

namespace FateCompass.Core.Tests;

public sealed class CompassBearingTests
{
    /// <summary>The game's zero facing looks along positive Z.</summary>
    private const float FacingNorthOfZ = 0f;

    private static readonly WorldPosition Origin = new(0f, 0f, 0f);

    private static float Degrees(float radians) => radians * 180f / MathF.PI;

    [Fact]
    public void SomethingStraightAheadNeedsNoTurn()
    {
        var relative = CompassBearing.Relative(Origin, FacingNorthOfZ, new WorldPosition(0f, 0f, 100f));

        Assert.Equal(0f, Degrees(relative), 3);
    }

    [Fact]
    public void SomethingBehindIsHalfATurnAway()
    {
        var relative = CompassBearing.Relative(Origin, FacingNorthOfZ, new WorldPosition(0f, 0f, -100f));

        Assert.Equal(180f, MathF.Abs(Degrees(relative)), 3);
    }

    /// <summary>
    /// The two sides have to come out opposite, whichever way round the game counts. A needle
    /// that mirrors is worse than no needle, because it is confidently wrong.
    /// </summary>
    [Fact]
    public void TheTwoSidesAreOpposite()
    {
        var left = CompassBearing.Relative(Origin, FacingNorthOfZ, new WorldPosition(-100f, 0f, 0f));
        var right = CompassBearing.Relative(Origin, FacingNorthOfZ, new WorldPosition(100f, 0f, 0f));

        Assert.Equal(90f, MathF.Abs(Degrees(left)), 3);
        Assert.Equal(90f, MathF.Abs(Degrees(right)), 3);
        Assert.NotEqual(MathF.Sign(left), MathF.Sign(right));
    }

    /// <summary>
    /// Turning the character turns the needle by the same amount the other way, which is the
    /// whole behaviour anybody will actually judge this by.
    /// </summary>
    [Fact]
    public void TurningTheCharacterTurnsTheNeedle()
    {
        var target = new WorldPosition(0f, 0f, 100f);
        var quarterTurn = MathF.PI / 2f;

        var ahead = CompassBearing.Relative(Origin, FacingNorthOfZ, target);
        var turned = CompassBearing.Relative(Origin, quarterTurn, target);

        Assert.Equal(0f, Degrees(ahead), 3);
        Assert.Equal(90f, MathF.Abs(Degrees(turned)), 3);
    }

    /// <summary>
    /// Facings wrap, and the needle has to take the short way round rather than spinning most of
    /// a circle to point somewhere just beside it.
    /// </summary>
    [Fact]
    public void TheNeedleTakesTheShortWayRound()
    {
        var target = new WorldPosition(0f, 0f, 100f);

        var justPastBehind = CompassBearing.Relative(Origin, (MathF.PI * 2f) - 0.1f, target);

        Assert.InRange(Degrees(justPastBehind), -180f, 180f);
        Assert.True(
            MathF.Abs(Degrees(justPastBehind)) < 10f,
            $"A facing a tenth of a radian short of a full turn is nearly ahead, not {Degrees(justPastBehind):F0} degrees off.");
    }

    [Theory]
    [InlineData(0f, 1f)]
    [InlineData(10f, 1f)]
    [InlineData(45f, 0f)]
    [InlineData(180f, 0f)]
    public void OnCourseIsFullAtTheTargetAndNothingWideOfIt(float degreesOff, float expected)
    {
        var radians = degreesOff * MathF.PI / 180f;

        Assert.Equal(expected, CompassBearing.OnCourse(radians), 3);
    }

    /// <summary>
    /// Between the two bounds it has to actually fade, in both directions, or the gradient is
    /// just a threshold with extra arithmetic.
    /// </summary>
    [Fact]
    public void OnCourseFadesBetweenTheBounds()
    {
        var middle = CompassBearing.OnCourse(27.5f * MathF.PI / 180f);

        Assert.InRange(middle, 0.4f, 0.6f);
        Assert.True(
            CompassBearing.OnCourse(20f * MathF.PI / 180f) > CompassBearing.OnCourse(35f * MathF.PI / 180f),
            "Closer to the heading has to read as more on course, not less.");
    }

    [Fact]
    public void TheSameFadeAppliesToEitherSide()
    {
        var left = CompassBearing.OnCourse(-30f * MathF.PI / 180f);
        var right = CompassBearing.OnCourse(30f * MathF.PI / 180f);

        Assert.Equal(left, right, 5);
    }

    [Theory]
    [InlineData(0f, true)]
    [InlineData(14f, true)]
    [InlineData(16f, false)]
    [InlineData(900f, false)]
    public void StandingOnTopOfItCountsAsArrived(float distance, bool arrived)
    {
        Assert.Equal(arrived, CompassBearing.IsAtTarget(distance));
    }
}
