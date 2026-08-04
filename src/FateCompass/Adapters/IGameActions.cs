using FateCompass.Core.Engage;

namespace FateCompass.Adapters;

/// <summary>
/// The only component in this plugin allowed to send anything to the game server (FH-01).
/// </summary>
/// <remarks>
/// Every method here is a deliberate, auditable act. If you are looking for what this plugin
/// can do to a live account, this interface is the complete list, and the implementation is
/// the only place it happens.
/// <para>
/// Each call reports whether it was triggered by the player or by the automatic path, so the
/// log stays honest about which side of the automation line a request came from (FH-06).
/// </para>
/// </remarks>
internal interface IGameActions
{
    /// <summary>Executes one planned step. Returns false when the step could not be sent.</summary>
    bool Execute(EngageStep step, ActionTrigger trigger);

    /// <summary>Teleports to an aetheryte. Always player-triggered, never automatic.</summary>
    bool Teleport(uint aetheryteId);

    /// <summary>
    /// Casts Return, which is how travel begins inside an exploratory zone.
    /// </summary>
    /// <remarks>
    /// Eureka, Bozja and the Occult Crescent have no aetherytes that can be teleported to: their
    /// travel points carry no identifier, and the way across is to return to camp and travel out
    /// from there. Return is the teleport of those zones, so the same button has to send a
    /// different action depending on where the player is standing.
    /// <para>
    /// Always player-triggered. Nothing automatic ever moves the player (FH-05).
    /// </para>
    /// </remarks>
    bool Return();
}

/// <summary>
/// What caused an action. Recorded rather than inferred, because the distinction is the whole
/// point of the automation boundary.
/// </summary>
internal enum ActionTrigger
{
    /// <summary>The player pressed a button or ran a command.</summary>
    Manual,

    /// <summary>A game event fired while the automatic option was switched on.</summary>
    Automatic,
}
