using Dalamud.Game.ClientState.Objects.Enums;
using FateCompass.Services;

namespace FateCompass.Adapters;

/// <summary>
/// Learns how high each aetheryte stands, by looking at the one in front of you.
/// </summary>
/// <remarks>
/// The game's data files carry no elevation for an aetheryte. <c>Aetheryte.Level</c> points at
/// row ids that are not in the Level sheet at all, and of 108 visible aetherytes only 15 can be
/// reached through <c>Level.Object</c>, so there is no static table to read. That is why the
/// second leg of every route was measured flat, and why a FATE on a plateau looked closest to
/// the aetheryte directly underneath it.
/// <para>
/// There is a source, though, and it is the obvious one: the aetheryte is a physical object
/// standing in the zone, and the object table gives its real position. The catch is that the
/// game only loads objects near the player, so this cannot be asked for a whole zone at once.
/// It is therefore learned rather than looked up: whatever is in range gets remembered, and the
/// table fills in as the zone gets played. What is not known yet simply measures flat, exactly
/// as before.
/// </para>
/// <para>
/// Remembered across sessions, because a table that empties every time the client restarts would
/// spend most of its life useless. Heights do not change; only patches move aetherytes, and a
/// stale entry costs a slightly wrong estimate rather than a wrong action.
/// </para>
/// </remarks>
internal static class AetheryteElevations
{
    /// <summary>
    /// How far a remembered height may drift before it is written down again.
    /// </summary>
    /// <remarks>
    /// Not zero. The reported position wobbles by fractions of a yalm, and rewriting the
    /// configuration every time the player walks past an aetheryte would be a lot of disk for a
    /// number that has not meaningfully changed.
    /// </remarks>
    private const float MeaningfulChangeYalms = 1f;

    private static Dictionary<uint, float> known = [];

    /// <summary>Set once at startup from the saved table.</summary>
    internal static void Restore(IReadOnlyDictionary<uint, float> saved)
    {
        known = saved is null ? [] : new Dictionary<uint, float>(saved);
    }

    /// <summary>The table as it stands, for saving.</summary>
    internal static IReadOnlyDictionary<uint, float> Export() => known;

    /// <summary>The remembered height of an aetheryte, or null when it has never been seen.</summary>
    internal static float? For(uint aetheryteId) =>
        aetheryteId != 0 && known.TryGetValue(aetheryteId, out var height) ? height : null;

    /// <summary>
    /// Reads whatever aetherytes are currently loaded and remembers their heights.
    /// </summary>
    /// <returns>True when something was learned or corrected, so the caller knows to save.</returns>
    /// <remarks>
    /// Called from the polling tick rather than per frame. Walking the object table is cheap, but
    /// it is not free, and an aetheryte does not move between one frame and the next.
    /// </remarks>

    internal static bool Learn()
    {
        var changed = false;

        try
        {
            foreach (var entity in DalamudServices.ObjectTable)
            {
                if (entity is null || entity.ObjectKind != ObjectKind.Aetheryte || entity.BaseId == 0)
                {
                    continue;
                }

                var height = entity.Position.Y;

                if (known.TryGetValue(entity.BaseId, out var previous)
                    && MathF.Abs(previous - height) < MeaningfulChangeYalms)
                {
                    continue;
                }

                known[entity.BaseId] = height;
                changed = true;

                DalamudServices.Log.Debug(
                    "AetheryteElevations: aetheryte {Id} stands at {Height:F1}", entity.BaseId, height);
            }
        }
        catch (Exception ex)
        {
            // Losing a height costs a flat measurement, which is the old behaviour (FH-07).
            DalamudServices.Log.Warning(ex, "AetheryteElevations: could not read the object table");
        }

        return changed;
    }
}
