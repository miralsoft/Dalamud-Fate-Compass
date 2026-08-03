namespace FateHelper.Core.Engage;

/// <summary>
/// Why a plan came back empty. Carried so the executor can log a useful line instead of
/// silently doing nothing (FH-06, FH-07).
/// </summary>
public enum PlanBlockedReason
{
    /// <summary>Not blocked. An empty plan here simply means nothing needed doing.</summary>
    None = 0,

    /// <summary>The plugin or the individual steps are switched off.</summary>
    Disabled,

    /// <summary>Combat prevents the action.</summary>
    InCombat,

    /// <summary>The player is casting or otherwise busy.</summary>
    Occupied,

    /// <summary>The desired end state already holds.</summary>
    AlreadyDone,

    /// <summary>
    /// The zone does not allow it, for example mounting inside parts of the instanced
    /// engagement content.
    /// </summary>
    NotPermittedHere,
}
