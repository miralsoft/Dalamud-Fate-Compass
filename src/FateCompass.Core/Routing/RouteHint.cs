namespace FateCompass.Core.Routing;

/// <summary>
/// How to get to a FATE: which aetheryte is closest to it, and whether teleporting there
/// actually beats simply travelling.
/// </summary>
public sealed record RouteHint
{
    public required Aetheryte NearestAetheryte { get; init; }

    /// <summary>Remaining distance from that aetheryte to the FATE.</summary>
    public required float AetheryteToFateYalms { get; init; }

    /// <summary>Direct distance from where the player stands to the FATE.</summary>
    public required float PlayerToFateYalms { get; init; }

    public required float EstimatedTeleportRouteSeconds { get; init; }

    public required float EstimatedDirectRouteSeconds { get; init; }

    /// <summary>Seconds left on the FATE at the moment this hint was calculated.</summary>
    public required int FateSecondsRemaining { get; init; }

    /// <summary>False when the FATE's clock has not begun, so its remaining time is unknown.</summary>
    public required bool FateHasStarted { get; init; }

    /// <summary>
    /// True when teleporting and travelling the rest is faster than travelling the whole way.
    /// Close to an aetheryte this is often false, which is worth telling the player rather than
    /// sending them through a loading screen for nothing.
    /// </summary>
    public bool TeleportIsFaster => EstimatedTeleportRouteSeconds < EstimatedDirectRouteSeconds;

    /// <summary>How much the teleport saves. Negative when it costs time instead.</summary>
    public float SecondsSaved => EstimatedDirectRouteSeconds - EstimatedTeleportRouteSeconds;

    /// <summary>The time of the better of the two routes.</summary>
    public float BestRouteSeconds => MathF.Min(EstimatedTeleportRouteSeconds, EstimatedDirectRouteSeconds);

    /// <summary>
    /// The recommendation. Reachability is decided first: if the FATE ends before either route
    /// could get there, which is faster stops mattering.
    /// </summary>
    public TeleportVerdict Verdict => FateHasStarted && BestRouteSeconds > FateSecondsRemaining
        ? TeleportVerdict.TooLate
        : TeleportIsFaster
            ? TeleportVerdict.Worthwhile
            : TeleportVerdict.TravelIsFaster;
}
