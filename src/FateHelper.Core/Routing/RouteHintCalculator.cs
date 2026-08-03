using FateHelper.Core.Configuration;
using FateHelper.Core.Model;

namespace FateHelper.Core.Routing;

/// <summary>
/// Works out the best approach to a FATE.
/// </summary>
public static class RouteHintCalculator
{
    /// <summary>
    /// Returns the nearest aetheryte to the FATE and a comparison of the two routes, or null
    /// when the zone has no aetherytes at all.
    /// </summary>
    /// <remarks>
    /// <c>yalmsPerUnit</c> states how many yalms one unit of the supplied positions represents.
    /// Aetheryte positions come from the game's map markers and are therefore in map
    /// coordinates, not world coordinates, so the caller states the conversion rather than this
    /// method assuming one.
    /// <para>
    /// <c>directDistanceYalms</c> lets the caller supply the player-to-FATE distance it already
    /// worked out in world space, elevation included. Without it, that leg would be measured in
    /// the flat map space the aetherytes live in, and standing high above a FATE would look the
    /// same as standing beside it. That is exactly the case where a teleport is worth it and the
    /// flat measure says otherwise.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Close enough to an aetheryte to count as standing at it.
    /// </summary>
    /// <remarks>
    /// Generous on purpose. The marker sits on the crystal and the player stands beside it, so a
    /// few yalms either way must not flip the answer between a one-hop trip and a two-hop one.
    /// </remarks>
    private const float AtAetheryteYalms = 40f;

    public static RouteHint? Calculate(
        FateSnapshot fate,
        PlayerSnapshot player,
        IReadOnlyCollection<Aetheryte> aetherytes,
        FateHelperSettings settings,
        float yalmsPerUnit = 1f,
        float? directDistanceYalms = null)
    {
        ArgumentNullException.ThrowIfNull(fate);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(aetherytes);
        ArgumentNullException.ThrowIfNull(settings);

        if (aetherytes.Count == 0)
        {
            return null;
        }


        var nearest = aetherytes
            .OrderBy(aetheryte => aetheryte.Position.HorizontalDistanceTo(fate.Position))
            .ThenBy(aetheryte => aetheryte.Id)
            .First();

        var scale = MathF.Max(yalmsPerUnit, 0.0001f);
        var aetheryteToFate = nearest.Position.HorizontalDistanceTo(fate.Position) * scale;

        // Prefer the caller's world-space figure, which knows about elevation. Fall back to the
        // flat map measure only when none was supplied.
        var playerToFate = directDistanceYalms
            ?? player.Position.HorizontalDistanceTo(fate.Position) * scale;

        var speed = MathF.Max(settings.SpeedFor(player), 0.01f);

        // Standing at one of them already, the trip back is not part of the journey. In an
        // exploratory zone the way across is normally return to camp and travel out from the
        // aetheryte there, and that first leg was being charged even when the player was stood
        // beside the crystal: it made a one-hop trip look like a two-hop one and advised walking
        // from the very spot where teleporting is at its cheapest.
        var atAetheryte = aetherytes.Any(aetheryte =>
            aetheryte.Position.HorizontalDistanceTo(player.Position) * scale <= AtAetheryteYalms);

        var overhead = atAetheryte
            ? settings.TeleportOverheadSeconds
            : settings.TeleportOverheadFor(player.Content);

        var teleportSeconds = overhead + (aetheryteToFate / speed);
        var directSeconds = playerToFate / speed;

        return new RouteHint
        {
            NearestAetheryte = nearest,
            AetheryteToFateYalms = aetheryteToFate,
            PlayerToFateYalms = playerToFate,
            EstimatedTeleportRouteSeconds = teleportSeconds,
            EstimatedDirectRouteSeconds = directSeconds,
            FateSecondsRemaining = fate.SecondsRemaining,
            FateHasStarted = fate.HasStarted,
        };
    }
}
