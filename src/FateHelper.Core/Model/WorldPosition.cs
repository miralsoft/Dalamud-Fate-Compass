namespace FateHelper.Core.Model;

/// <summary>
/// A position in world coordinates.
/// </summary>
/// <remarks>
/// Y is elevation. Travel estimates use <see cref="HorizontalDistanceTo"/>, because a height
/// difference rarely reflects how far the player actually has to travel.
/// </remarks>
public readonly record struct WorldPosition(float X, float Y, float Z)
{
    public static WorldPosition Origin => new(0f, 0f, 0f);

    /// <summary>Straight-line distance including elevation.</summary>
    public float DistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        var dz = Z - other.Z;
        return MathF.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>Distance on the ground plane, ignoring elevation.</summary>
    public float HorizontalDistanceTo(WorldPosition other)
    {
        var dx = X - other.X;
        var dz = Z - other.Z;
        return MathF.Sqrt((dx * dx) + (dz * dz));
    }

    /// <summary>
    /// Travel distance with a climb counted more heavily than level ground.
    /// </summary>
    /// <remarks>
    /// Gaining height costs real time even on a flying mount, and a FATE far above or below is
    /// further away than the map suggests. Straight-line 3D distance would understate that,
    /// because it treats a yalm of climb as a yalm of flight, so the vertical component gets a
    /// weight of its own.
    /// </remarks>
    public float TravelDistanceTo(WorldPosition other, float verticalWeight)
    {
        var flat = HorizontalDistanceTo(other);
        if (verticalWeight <= 0f)
        {
            return flat;
        }

        var climb = MathF.Abs(Y - other.Y) * verticalWeight;
        return MathF.Sqrt((flat * flat) + (climb * climb));
    }
}
