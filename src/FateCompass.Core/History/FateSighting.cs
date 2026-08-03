namespace FateCompass.Core.History;

/// <summary>
/// One observed appearance of a FATE.
/// </summary>
public sealed record FateSighting
{
    /// <summary>Definition id, stable across respawns, unlike the instance id.</summary>
    public required uint DefinitionId { get; init; }

    public required ushort TerritoryId { get; init; }

    public required DateTimeOffset SeenAt { get; init; }
}
