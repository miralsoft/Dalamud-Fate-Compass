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
