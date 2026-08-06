using FateCompass.Core.Model;

namespace FateCompass.Core.Routing;

/// <summary>
/// Which way to turn to be heading at something.
/// </summary>
/// <remarks>
/// Deliberately about the direction and nothing else. It is a compass, not a route: it points
/// along the straight line, and the straight line can run into a cliff. That is the accepted
/// limit of the idea rather than a defect in it, and it is the reason the needle is drawn beside
/// a distance rather than instead of one.
/// </remarks>
public static class CompassBearing
{
    /// <summary>Beyond this the needle is fully grey, in degrees off course.</summary>
    private const float FullyOffDegrees = 45f;

    /// <summary>Within this the needle is fully lit, in degrees off course.</summary>
    private const float FullyOnDegrees = 10f;

    /// <summary>
    /// Turn needed to face the target, in radians, from -pi to pi. Zero is dead ahead.
    /// </summary>
    /// <remarks>
    /// Positive is clockwise, which is to say to the right on screen. The game reports a facing
    /// where zero looks along positive Z and the angle grows anticlockwise, so the clockwise turn
    /// is the facing minus the bearing rather than the other way round.
    /// <para>
    /// If a needle ever points at the mirror image of the truth, this sign is the whole of the
    /// bug and the whole of the fix.
    /// </para>
    /// </remarks>
    public static float Relative(WorldPosition from, float facingRadians, WorldPosition to)
    {
        var bearing = MathF.Atan2(to.X - from.X, to.Z - from.Z);
        return Normalise(facingRadians - bearing);
    }

    /// <summary>
    /// How much on course the heading is, from 0 for hopeless to 1 for dead on.
    /// </summary>
    /// <remarks>
    /// A gradient rather than a threshold. A hard switch at some angle flickers between its two
    /// states whenever the heading sits on the boundary, which it does exactly when the player is
    /// turning onto the target and looking at it. Fading also says "warmer" while turning, where
    /// a threshold only ever says "no" and then "yes".
    /// </remarks>
    public static float OnCourse(float relativeRadians)
    {
        var degrees = MathF.Abs(relativeRadians) * 180f / MathF.PI;

        if (degrees <= FullyOnDegrees)
        {
            return 1f;
        }

        if (degrees >= FullyOffDegrees)
        {
            return 0f;
        }

        return 1f - ((degrees - FullyOnDegrees) / (FullyOffDegrees - FullyOnDegrees));
    }

    /// <summary>
    /// True once the target is close enough that a direction stops meaning anything.
    /// </summary>
    /// <remarks>
    /// A bearing is an angle, and an angle from almost the same point swings wildly for a step
    /// in any direction. Standing on top of a FATE the needle would spin, which reads as broken
    /// rather than as arrived, so the caller shows that it is arrived instead.
    /// </remarks>
    public static bool IsAtTarget(float distanceYalms) => distanceYalms <= ArrivedYalms;

    /// <summary>Close enough that a bearing is noise.</summary>
    private const float ArrivedYalms = 15f;

    /// <summary>Folds any angle into -pi to pi, so the needle turns the short way round.</summary>
    private static float Normalise(float radians)
    {
        var turn = MathF.PI * 2f;
        var wrapped = radians % turn;

        return wrapped switch
        {
            > MathF.PI => wrapped - turn,
            < -MathF.PI => wrapped + turn,
            _ => wrapped,
        };
    }
}
