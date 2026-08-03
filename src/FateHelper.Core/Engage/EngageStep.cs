namespace FateHelper.Core.Engage;

/// <summary>
/// A single preparation step around a FATE.
/// </summary>
/// <remarks>
/// Each of these reaches the game server when executed, so the set is deliberately small and
/// closed. Adding a value here is a scope decision, not a refactor (FH-05).
/// </remarks>
public enum EngageStep
{
    Dismount,
    LevelSync,
    TankStance,
    Mount,
}
