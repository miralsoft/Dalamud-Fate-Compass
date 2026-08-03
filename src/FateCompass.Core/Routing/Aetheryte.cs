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
}
