namespace FateHelper.Core.Routing;

/// <summary>
/// Whether teleporting to a FATE is actually worth doing.
/// </summary>
/// <remarks>
/// A teleport costs gil, a cast, and a loading screen. Offering the button without saying
/// whether it helps leaves the player to guess, and close to an aetheryte the answer is
/// usually no.
/// </remarks>
public enum TeleportVerdict
{
    /// <summary>Teleporting and travelling the rest gets there sooner. Worth the gil.</summary>
    Worthwhile = 0,

    /// <summary>Travelling directly is faster. The loading screen would cost more than it saves.</summary>
    TravelIsFaster,

    /// <summary>
    /// Neither route arrives before the FATE ends. Nothing is worth doing here, which is more
    /// useful to know than which of two pointless options is marginally quicker.
    /// </summary>
    TooLate,
}
