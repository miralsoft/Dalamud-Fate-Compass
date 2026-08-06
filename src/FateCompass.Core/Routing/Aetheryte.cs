using FateCompass.Core.Model;

namespace FateCompass.Core.Routing;

/// <summary>
/// A teleport destination in the current zone.
/// </summary>
/// <remarks>
/// Supplied by the adapter layer from the game's own data files. The core only needs the id to
/// hand back when the player asks to teleport, the name to display, and the position to measure
/// against.
/// </remarks>
public sealed record Aetheryte
{
    public required uint Id { get; init; }

    public required string Name { get; init; }

    public required WorldPosition Position { get; init; }

    /// <summary>
    /// How high the aetheryte stands, in world yalms, or null while that is not known.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="Position"/> rather than folded into its Y. The
    /// position is in map coordinates, the elevation is in world yalms, and quietly mixing two
    /// units inside one value is the kind of thing that reads correctly and computes nonsense.
    /// <para>
    /// Null is the ordinary case, not an error: the game's data files carry no height for an
    /// aetheryte, so it can only be learned by having stood near one. Everything that uses this
    /// falls back to a flat measurement, which is what the plugin did for every aetheryte before.
    /// </para>
    /// </remarks>
    public float? Elevation { get; init; }
}
