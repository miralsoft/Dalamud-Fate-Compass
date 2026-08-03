namespace FateCompass.Core.Model;

/// <summary>
/// The player state the core needs in order to plan the engage sequence and rank FATEs.
/// </summary>
/// <remarks>
/// Everything here is read from the game by the adapter layer. Passing it as a value means the
/// planner is a pure function of its input, which is what makes the automation boundary
/// testable.
/// </remarks>
public sealed record PlayerSnapshot
{
    public required WorldPosition Position { get; init; }

    public required ushort Level { get; init; }

    public required bool IsMounted { get; init; }

    public required bool IsInCombat { get; init; }

    /// <summary>Null when the player is not on a tank job.</summary>
    public TankJob? TankJob { get; init; }

    /// <summary>True when the tank stance for the current job is already active.</summary>
    public bool IsTankStanceActive { get; init; }

    /// <summary>True when the game currently offers level sync, which it does inside a FATE.</summary>
    public bool IsLevelSyncAvailable { get; init; }

    /// <summary>True when the player is already synced down.</summary>
    public bool IsLevelSynced { get; init; }

    /// <summary>True when the player is casting or otherwise occupied.</summary>
    public bool IsOccupied { get; init; }

    /// <summary>
    /// What kind of content the player is standing in.
    /// </summary>
    /// <remarks>
    /// Part of the player snapshot rather than of the settings, because it describes the
    /// situation rather than a preference. The ranking needs it to pick the right travel speed:
    /// the exploratory zones have no flying, and planning a route there at flying speed promises
    /// arrivals that cannot happen.
    /// </remarks>
    public ContentKind Content { get; init; } = ContentKind.Overworld;

    /// <summary>
    /// Which zone the player is in, so measurements can be kept per zone rather than per kind.
    /// </summary>
    public uint TerritoryId { get; init; }

    /// <summary>
    /// Whether this zone's riding maps have been bought, which decides how fast the ground is
    /// crossed where there is no flying.
    /// </summary>
    /// <remarks>
    /// Defaults to true so a snapshot that does not care about it plans at the ordinary speed
    /// rather than the reduced one.
    /// </remarks>
    public bool HasMountSpeedUpgrades { get; init; } = true;

    /// <summary>
    /// False where the game does not allow mounting. The remount step is then skipped rather
    /// than attempted, so the plugin does not fire an action the zone will refuse (FH-07).
    /// </summary>
    public bool CanUseMount { get; init; } = true;

    /// <summary>
    /// False while flying or jumping. Dismounting in the air drops the character, which is
    /// both jarring and can be dangerous, so the dismount step waits for the ground.
    /// </summary>
    public bool IsOnGround { get; init; } = true;

    public bool IsTank => TankJob is not null;
}
