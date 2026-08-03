using System.Numerics;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;
using FateCompass.Core.Model;
using FateCompass.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace FateCompass.Adapters;

/// <summary>
/// Places the map flag. Client-side only, no contact with the game server.
/// </summary>
/// <remarks>
/// This is the whole mechanism behind the flag button, and it is worth being precise about why
/// it is clean: setting the flag is a local UI action. Sharing it is then the player's own
/// doing, because the game's <c>&lt;flag&gt;</c> chat placeholder expands to whatever flag is
/// set. The plugin never touches the chat box and never sends a message.
/// </remarks>
internal static unsafe class MapService
{
    /// <summary>Standard quest-style map marker icon.</summary>
    private const uint FlagIconId = 60561;

    /// <summary>
    /// The game's field marker glyphs, in rank order: 1, 2, 3, 4, then A, B, C, D.
    /// </summary>
    /// <remarks>
    /// The game cannot write text of our choosing onto a marker, so a marker that already looks
    /// like a character is the closest it gets. These are the waymark icons, and their ordering
    /// is nothing like the obvious one. Read off the map, the block runs:
    /// <code>
    ///   61241 61242 61243 61244 61245 61246 61247 61248
    ///     A     B     C     1     2     3     D     4
    /// </code>
    /// Six waymarks came first, A B C 1 2 3, and D and 4 were appended in that order when they
    /// were added to the game later. So the two sets interleave at the end: D sits between the
    /// three and the four. Assuming the digits ran on consecutively put a D where the fourth
    /// FATE should have been and left the four and the C unused.
    /// <para>
    /// Digits first, then letters, so a ninth entry is the first with nothing of its own. That
    /// is well past the point where a running order is still a plan.
    /// </para>
    /// </remarks>
    private static readonly uint[] RankIconIds =
    [
        61244, 61245, 61246, 61248,
        61241, 61242, 61243, 61247,
    ];

    /// <summary>
    /// The highest rank that can be given a marker, which is how many icons carry a character.
    /// </summary>
    /// <remarks>
    /// Anything past this could only get a blank pin. That would mark where a FATE is without
    /// saying which one it is, and the game already puts its own icon on every FATE, so it would
    /// add clutter and no information.
    /// </remarks>
    internal static int MaximumRankMarkers => RankIconIds.Length;

    private static uint IconFor(int rank) => RankIconIds[rank - 1];

    /// <summary>
    /// Places one marker per ranked FATE on the map and the minimap.
    /// </summary>
    /// <remarks>
    /// These are the game's own markers, given a world position directly, so the game performs
    /// the projection itself and they stay correct through panning, zooming, and a change of
    /// HUD scale. Nothing here computes a screen coordinate.
    /// <para>
    /// The tooltip pointer is deliberately null. That parameter is a raw byte pointer the game
    /// keeps and dereferences on a later frame, and handing it a pinned managed string is what
    /// took the client down once already (FH-10). A marker without a caption is worth more than
    /// a crash with one.
    /// </para>
    /// <para>
    /// Existing markers are cleared first, otherwise every refresh would stack another set on
    /// top of the last.
    /// </para>
    /// <para>
    /// The game keeps the list of markers and the nodes it draws them with apart, and only
    /// builds the second from the first when it is told to. Adding to the list while the map was
    /// already open therefore changed nothing on screen, and clearing it made the markers vanish
    /// for good: they were in the list all along, with nothing drawing them. Hence the rebuild
    /// at the end.
    /// </para>
    /// </remarks>
    internal static void DrawRankMarkers(IReadOnlyList<(int Rank, WorldPosition Position, string Name)> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // The native overlay first: those markers are part of the map rather than painted over
        // it, so they follow a drag exactly and survive a rebuild. It reports for itself whether
        // it worked, and when it has switched itself off the game's own markers take over below.
        var requests = entries
            .Select(entry => new MapMarkerRequest(
                entry.Position, IconFor(entry.Rank), $"{entry.Rank}. {entry.Name}"))
            .ToList();

        if (NativeMapMarkers.Apply(requests, DalamudServices.ClientState.MapId))
        {
            return;
        }

        try
        {
            var agent = AgentMap.Instance();
            if (agent is null)
            {
                return;
            }

            agent->ResetMapMarkers();
            agent->ResetMiniMapMarkers();

            foreach (var (rank, position, _) in entries)
            {
                var icon = IconFor(rank);
                var world = new Vector3(position.X, position.Y, position.Z);

                agent->AddMapMarker(world, icon, 0, null, 0, 0);
                agent->AddMiniMapMarker(world, icon, 0);
            }

            Rebuild(agent);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MapService: could not place the rank markers");
        }
    }

    /// <summary>
    /// Places a row of markers around a position, one per icon id in a range, so it can be seen
    /// which of them actually look like digits.
    /// </summary>
    /// <remarks>
    /// The game has no way to write a number onto a marker, so the running order has to be
    /// carried by an icon that already is a number. Which icon ids those are is not documented
    /// anywhere, and trying them one at a time through the map is slow. This drops a whole range
    /// at once, spread out enough to tell apart.
    /// </remarks>
    internal static string ProbeMarkerIcons(WorldPosition around, uint firstIconId, int count)
    {
        var report = new System.Text.StringBuilder();
        report.AppendLine(
            System.Globalization.CultureInfo.InvariantCulture,
            $"Placed icons {firstIconId} to {firstIconId + count - 1}, left to right:");

        try
        {
            var agent = AgentMap.Instance();
            if (agent is null)
            {
                return "AgentMap is not available.";
            }

            agent->ResetMapMarkers();
            agent->ResetMiniMapMarkers();

            for (var i = 0; i < count; i++)
            {
                var icon = firstIconId + (uint)i;

                // Spread along one axis so the order on the map matches the order listed here.
                var world = new Vector3(around.X + (i * 30f), around.Y, around.Z);

                agent->AddMapMarker(world, icon, 0, null, 0, 0);
                report.AppendLine(
                    System.Globalization.CultureInfo.InvariantCulture, $"  {i + 1}. icon {icon}");
            }

            report.AppendLine();
            report.AppendLine("Open the map and report which of them show a digit.");
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MapService: icon probe failed");
            return $"Icon probe failed: {ex.Message}";
        }

        return report.ToString();
    }

    /// <summary>
    /// Turns the marker list into the nodes that are actually drawn.
    /// </summary>
    /// <remarks>
    /// Kept in its own guarded block. It is the one call here whose parameter is not understood,
    /// so a failure must not cost the markers that were just placed.
    /// </remarks>
    private static void Rebuild(AgentMap* agent)
    {
        try
        {
            agent->CreateMapMarkers(false);
            agent->CreateMiniMapMarkers(false);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MapService: could not rebuild the marker nodes");
        }
    }

    /// <summary>Removes the rank markers, so none linger once there is nothing to show.</summary>
    internal static void ClearRankMarkers()
    {
        NativeMapMarkers.Clear();

        try
        {
            var agent = AgentMap.Instance();
            if (agent is not null)
            {
                agent->ResetMapMarkers();
                agent->ResetMiniMapMarkers();
                Rebuild(agent);
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MapService: could not clear the rank markers");
        }
    }

    /// <summary>
    /// Sets the map flag on a position without opening the map. Returns false when the agent
    /// is unavailable, for example during a zone change.
    /// </summary>
    internal static bool SetFlag(uint territoryId, uint mapId, WorldPosition position)
    {
        try
        {
            var agent = AgentMap.Instance();
            if (agent is null)
            {
                return false;
            }

            agent->SetFlagMapMarker(
                territoryId,
                mapId,
                new Vector3(position.X, position.Y, position.Z),
                FlagIconId);

            DalamudServices.Log.Debug(
                "MapService: flag set in territory {Territory} at {X}, {Z}",
                territoryId, position.X, position.Z);

            return true;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Error(ex, "MapService: could not set the map flag");
            return false;
        }
    }

    /// <summary>
    /// Removes the map flag, used once the FATE it pointed at is over so a stale marker does
    /// not linger.
    /// </summary>
    internal static void ClearFlag()
    {
        try
        {
            var agent = AgentMap.Instance();
            if (agent is not null)
            {
                agent->FlagMarkerCount = 0;
            }
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "MapService: could not clear the map flag");
        }
    }

    /// <summary>
    /// Sets the flag and echoes a clickable map link into the player's own chat log.
    /// </summary>
    /// <remarks>
    /// The echo goes through the chat service's local print, which writes only into this
    /// client's log. Nothing is sent to the server and nobody else sees it, so this stays a
    /// display feature.
    /// <para>
    /// To share the position with a party, the player still types the game's own
    /// <c>&lt;flag&gt;</c> placeholder and presses enter. The flag this method sets is what that
    /// placeholder expands to, so the two work together.
    /// </para>
    /// </remarks>
    /// <param name="territoryId">Territory the position belongs to.</param>
    /// <param name="mapId">Map row used to turn the position into link coordinates.</param>
    /// <param name="position">Where to put the flag, in world coordinates.</param>
    /// <param name="label">Name shown in front of the link in the echoed line.</param>
    /// <param name="typeIntoChat">
    /// Also types the game's flag placeholder into the chat box, ready to send. Nothing is
    /// transmitted: the player still chooses the channel and presses enter.
    /// </param>
    internal static bool SetFlagAndEcho(
        uint territoryId, uint mapId, WorldPosition position, string label, bool typeIntoChat)
    {
        if (!SetFlag(territoryId, mapId, position))
        {
            return false;
        }

        // Typed after the flag is set, so the placeholder expands to this position when the
        // player sends it. Doing it the other way round would post the previous flag.
        if (typeIntoChat)
        {
            ChatInput.Append(ChatInput.FlagPlaceholder);
        }

        try
        {
            var sheet = DalamudServices.DataManager.GetExcelSheet<Map>();
            if (!sheet.TryGetRow(mapId, out var map))
            {
                return true;
            }

            // Dalamud converts world coordinates to the map coordinates a link expects, so no
            // offset or scale arithmetic is done by hand here.
            var mapCoords = MapUtil.WorldToMap(new Vector2(position.X, position.Z), map);

            var message = new SeStringBuilder()
                .AddUiForeground(62)
                .AddText($"[Fate Compass] {label} ")
                .AddUiForegroundOff()
                .AddMapLink(territoryId, mapId, mapCoords.X, mapCoords.Y)
                .Build();

            DalamudServices.ChatGui.Print(message);
        }
        catch (Exception ex)
        {
            // The flag itself is already set, so a failed echo is cosmetic.
            DalamudServices.Log.Warning(ex, "MapService: could not echo the map link");
        }

        return true;
    }
}
