using System.Numerics;
using Dalamud.Utility;
using FateCompass.Core.Model;
using FateCompass.Services;
using Lumina.Excel.Sheets;
using LuminaAetheryte = Lumina.Excel.Sheets.Aetheryte;

namespace FateCompass.Adapters;

/// <summary>
/// Reads the teleport destinations of the current zone from the game's own data files.
/// </summary>
/// <remarks>
/// Positions come from the <c>MapMarker</c> sheet, not from <c>Aetheryte.Level</c>. The Level
/// references in the aetheryte rows point at row ids far outside the Level sheet (for example
/// 3661246 against roughly 61000 rows), so every lookup through them failed and the plugin
/// reported that no zone had any aetheryte at all. The map markers carry the positions
/// reliably: 107 of them, one per visible aetheryte.
/// <para>
/// Everything here works in **map coordinates**, the 1 to 42 range the game shows, because that
/// is the space the markers are in and converting back to world coordinates has no supported
/// inverse. FATE and player positions are converted into the same space before any distance is
/// measured, so the comparison is consistent. One map unit is about 50 yalms, which is what the
/// distance column uses to stay readable.
/// </para>
/// </remarks>
internal static class AetheryteProvider
{
    /// <summary>Marker data type for an aetheryte.</summary>
    private const byte AetheryteMarkerType = 3;

    /// <summary>Map textures are 2048 wide and span 41 coordinate units.</summary>
    private const float MapTextureSize = 2048f;

    private const float MapCoordinateSpan = 41f;

    /// <summary>Roughly how many yalms one map coordinate unit covers.</summary>
    internal const float YalmsPerMapUnit = 50f;

    private static readonly Dictionary<uint, IReadOnlyList<Core.Routing.Aetheryte>> Cache = [];

    /// <summary>
    /// Aetherytes of the given territory, positioned in map coordinates. Cached per zone,
    /// because the game data cannot change while the client runs.
    /// </summary>
    /// <remarks>
    /// The elevations are attached on the way out rather than cached with the rest. Positions and
    /// names come from data files that cannot change while the client runs, so caching them is
    /// free; heights are learned as the player moves around and would be frozen at whatever was
    /// known the first time this zone was asked about.
    /// </remarks>
    internal static IReadOnlyList<Core.Routing.Aetheryte> ForTerritory(uint territoryId)
    {
        if (!Cache.TryGetValue(territoryId, out var cached))
        {
            cached = Load(territoryId);
            Cache[territoryId] = cached;
        }

        return [.. cached.Select(a => a with { Elevation = AetheryteElevations.For(a.Id) })];
    }

    internal static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Converts a world position into the same map coordinate space the aetherytes use, so the
    /// two can be compared. Returns null when the map row cannot be read.
    /// </summary>
    internal static WorldPosition? ToMapSpace(WorldPosition world, uint mapId)
    {
        try
        {
            var maps = DalamudServices.DataManager.GetExcelSheet<Map>();
            if (!maps.TryGetRow(mapId, out var map))
            {
                return null;
            }

            var coords = MapUtil.WorldToMap(new Vector2(world.X, world.Z), map);
            return new WorldPosition(coords.X, 0f, coords.Y);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "AetheryteProvider: could not convert to map space");
            return null;
        }
    }

    private static List<Core.Routing.Aetheryte> Load(uint territoryId)
    {
        var found = new List<Core.Routing.Aetheryte>();

        try
        {
            var aetherytes = DalamudServices.DataManager.GetExcelSheet<LuminaAetheryte>();
            var maps = DalamudServices.DataManager.GetExcelSheet<Map>();
            var positions = MarkerPositions();

            foreach (var row in aetherytes)
            {
                if (!row.IsAetheryte || row.Invisible || row.Territory.RowId != territoryId)
                {
                    continue;
                }

                if (!positions.TryGetValue(row.RowId, out var marker))
                {
                    continue;
                }

                if (!maps.TryGetRow(row.Map.RowId, out var map))
                {
                    continue;
                }

                var name = row.PlaceName.ValueNullable?.Name.ExtractText();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                found.Add(new Core.Routing.Aetheryte
                {
                    Id = row.RowId,
                    Name = name,
                    Position = new WorldPosition(
                        MarkerToMapCoordinate(marker.X, map.SizeFactor),
                        0f,
                        MarkerToMapCoordinate(marker.Y, map.SizeFactor)),
                });
            }

            if (found.Count == 0)
            {
                found.AddRange(LoadInZoneWaypoints());
            }

            DalamudServices.Log.Debug(
                "AetheryteProvider: territory {Territory} has {Count} aetheryte(s)", territoryId, found.Count);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "AetheryteProvider: could not read territory {Territory}", territoryId);
        }

        return found;
    }

    /// <summary>
    /// Icon the exploratory zones use for the points you can travel between inside them.
    /// </summary>
    private const uint InZoneWaypointIcon = 60959;

    /// <summary>
    /// The travel points of an exploratory zone, which are not aetherytes as the game files
    /// them but are what the player uses as one.
    /// </summary>
    /// <remarks>
    /// The Occult Crescent has six of these and not a single row in the aetheryte sheet, so the
    /// ordinary reader came back with nothing at all and no route could be worked out there. They
    /// live in the map markers instead, distinguished only by their icon, and a live dump matched
    /// all six names against the game's own travel menu: the base camp, Fortress Karnak, the
    /// Sunken Church entrance, the Floating Ruin, the Corrupted Quarter entrance, and the
    /// Fishing Village.
    /// <para>
    /// Their identifier is left at zero on purpose. These cannot be teleported to from outside,
    /// only travelled between from within, so nothing may offer them to the teleport action; the
    /// zero is what the button checks before it appears.
    /// </para>
    /// <para>
    /// Whether the player has attuned to each of them is not readable here, so all are offered.
    /// A route to one they have not unlocked yet is a wrong suggestion, not a wrong action: the
    /// travel menu simply will not list it.
    /// </para>
    /// </remarks>
    private static List<Core.Routing.Aetheryte> LoadInZoneWaypoints()
    {
        var found = new List<Core.Routing.Aetheryte>();

        var maps = DalamudServices.DataManager.GetExcelSheet<Map>();
        if (!maps.TryGetRow(DalamudServices.ClientState.MapId, out var map))
        {
            return found;
        }

        var markers = DalamudServices.DataManager.GetSubrowExcelSheet<MapMarker>();
        if (!markers.TryGetRow(map.MapMarkerRange, out var subrows))
        {
            return found;
        }

        foreach (var marker in subrows)
        {
            if (marker.Icon != InZoneWaypointIcon)
            {
                continue;
            }

            var name = marker.PlaceNameSubtext.ValueNullable?.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            found.Add(new Core.Routing.Aetheryte
            {
                Id = 0,
                Name = name,
                Position = new WorldPosition(
                    MarkerToMapCoordinate(marker.X, map.SizeFactor),
                    0f,
                    MarkerToMapCoordinate(marker.Y, map.SizeFactor)),
            });
        }

        return found;
    }

    /// <summary>Aetheryte row id to its raw marker position, built once per call.</summary>
    private static Dictionary<uint, (short X, short Y)> MarkerPositions()
    {
        var result = new Dictionary<uint, (short X, short Y)>();
        var markers = DalamudServices.DataManager.GetSubrowExcelSheet<MapMarker>();

        foreach (var subrows in markers)
        {
            foreach (var marker in subrows)
            {
                if (marker.DataType == AetheryteMarkerType)
                {
                    result.TryAdd(marker.DataKey.RowId, (marker.X, marker.Y));
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Converts a raw marker position into the map coordinate the game displays.
    /// </summary>
    /// <remarks>
    /// Checked against known in-game positions rather than taken on faith: Bentbranch Meadows
    /// resolves to roughly 21.8 / 22.2, Camp Tranquil to 16.9 / 28.6, and Limsa Lominsa Lower
    /// Decks to 9.6 / 11.2, all matching what the game shows.
    /// </remarks>
    private static float MarkerToMapCoordinate(short markerPosition, ushort sizeFactor)
    {
        var scale = Math.Max(sizeFactor, (ushort)1) / 100f;
        return (markerPosition / MapTextureSize * MapCoordinateSpan / scale) + 1f;
    }
}
