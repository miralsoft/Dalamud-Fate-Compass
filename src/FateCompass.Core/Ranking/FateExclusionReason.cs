namespace FateCompass.Core.Ranking;

/// <summary>
/// Why a FATE did not make it into the ranked list.
/// </summary>
/// <remarks>
/// Kept as an explicit reason rather than silently dropping the FATE, so the window can tell
/// the player why something is greyed out instead of leaving them guessing.
/// </remarks>
public enum FateExclusionReason
{
    /// <summary>Not excluded.</summary>
    None = 0,

    /// <summary>Not running, so it cannot be joined right now.</summary>
    NotJoinable,

    /// <summary>
    /// The registration window has shut. The fight goes on, but nobody else gets in.
    /// </summary>
    RegistrationClosed,

    /// <summary>
    /// The registration window would shut before the player could get there.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Unreachable"/> on purpose. Being too slow for the sign-up
    /// deadline and being too slow for the whole fight are different situations, and only the
    /// first one leaves a fight that is still visibly running on the map.
    /// </remarks>
    RegistrationTooLate,

    /// <summary>Hidden by the player's kind or level filter.</summary>
    Filtered,

    /// <summary>So far along that arriving would earn little.</summary>
    NearlyComplete,

    /// <summary>Too little time left to be worth starting.</summary>
    ExpiringSoon,

    /// <summary>Would expire before the player could get there.</summary>
    Unreachable,
}
