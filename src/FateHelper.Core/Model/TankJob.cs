namespace FateHelper.Core.Model;

/// <summary>
/// The tank jobs, each with its own stance action.
/// </summary>
/// <remarks>
/// The core deliberately does not know job ids or action ids. The adapter layer resolves the
/// player's job against the game's own class job data and maps it onto this enum, so no
/// unverified numeric constants live in testable code (I-10).
/// </remarks>
public enum TankJob
{
    Paladin,
    Warrior,
    DarkKnight,
    Gunbreaker,
}
