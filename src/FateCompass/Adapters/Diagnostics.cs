// Developer tools. Present only in a build that defines FATECOMPASS_DEVTOOLS, which no
// committed file does: it comes from Directory.Build.local.props, which is ignored by git.
// In every published build this file compiles to nothing at all.
#if FATECOMPASS_DEVTOOLS
using System.Numerics;
using System.Text;
using FateCompass.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;

// CA1305 asks for an explicit culture on every interpolated append below. This file produces a
// developer dump that is read by a human and pasted into a chat, never parsed and never shown
// to a player, so the current culture is exactly the right thing to format with and naming it
// eight times would only add noise.
#pragma warning disable CA1305

namespace FateCompass.Adapters;

/// <summary>
/// Dumps raw game state into the log so unknown data layouts can be worked out from a real
/// session instead of guessed at.
/// </summary>
/// <remarks>
/// This exists because two things in this plugin have no documented source: the shared FATE
/// rank, which lives only inside the game's own progress window, and the map marker call,
/// which silently does nothing. Both are faster to solve by reading what the client actually
/// holds than by trying values.
/// <para>
/// Everything here is read-only apart from the marker probe, which adds markers exactly as the
/// normal code would.
/// </para>
/// </remarks>
internal static unsafe class Diagnostics
{
    private const string FateProgressAddon = "FateProgress";

    /// <summary>Icon used for both probe markers, so they are easy to spot.</summary>
    private const uint ProbeIconId = 60093;

    /// <summary>
    /// Lists every marker the game files against a map, with the type it files it under.
    /// </summary>
    /// <remarks>
    /// The aetheryte reader only looks at one marker type, because that is the type ordinary
    /// aetherytes use. An exploratory zone came back with none at all, which means either it has
    /// none or its aetherytes are filed as something else. This prints the lot so the answer can
    /// be read off rather than assumed, which is how the last three of these went wrong.
    /// <para>
    /// The map row is the key, not the territory: a zone's markers hang off the map it draws.
    /// </para>
    /// </remarks>
    internal static void AppendMapMarkers(StringBuilder report, uint mapId)
    {
        ArgumentNullException.ThrowIfNull(report);

        report.AppendLine($"=== Map markers for map {mapId} ===");

        try
        {
            var maps = DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Map>();
            if (!maps.TryGetRow(mapId, out var map))
            {
                report.AppendLine("Map row not found.");
                return;
            }

            report.AppendLine($"markerRange={map.MapMarkerRange} sizeFactor={map.SizeFactor}");

            var markers = DalamudServices.DataManager
                .GetSubrowExcelSheet<Lumina.Excel.Sheets.MapMarker>();

            if (!markers.TryGetRow(map.MapMarkerRange, out var subrows))
            {
                report.AppendLine("No marker rows for this map's range.");
                return;
            }

            var count = 0;
            foreach (var marker in subrows)
            {
                var label = marker.PlaceNameSubtext.ValueNullable?.Name.ExtractText() ?? string.Empty;

                report.AppendLine(
                    $"  type={marker.DataType,-3} key={marker.DataKey.RowId,-6} " +
                    $"icon={marker.Icon,-6} x={marker.X,-6} y={marker.Y,-6} \"{label}\"");

                count++;
            }

            report.AppendLine($"total: {count}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"Could not read the markers: {ex.Message}");
        }
    }

    /// <summary>
    /// Dumps the zone's mount speed row and whether its riding map quests are complete.
    /// </summary>
    /// <remarks>
    /// The sheet names one quest outright and carries two further columns the client structs
    /// layer has not identified. Zones show up to two stars in the game's own window, so at
    /// least one of those columns is probably a second riding map. Printing all three next to
    /// their completion state is how that gets settled, rather than by assuming.
    /// </remarks>
    internal static string DumpMountSpeed()
    {
        var report = new StringBuilder();
        var territoryId = DalamudServices.ClientState.TerritoryType;

        report.AppendLine($"=== Mount speed, territory {territoryId} ===");

        var territories = DalamudServices.DataManager.GetExcelSheet<Lumina.Excel.Sheets.TerritoryType>();
        if (!territories.TryGetRow(territoryId, out var territory))
        {
            return Finish(report.AppendLine("Territory row not found."));
        }

        report.AppendLine($"place        {territory.PlaceName.ValueNullable?.Name.ExtractText() ?? "?"}");
        report.AppendLine($"mountSpeed   row {territory.MountSpeed.RowId}");

        if (territory.MountSpeed.ValueNullable is not { } speed)
        {
            return Finish(report.AppendLine("No mount speed row, so this zone has no riding maps."));
        }

        report.AppendLine($"  Quest      {speed.Quest.RowId}  complete={IsComplete(speed.Quest.RowId)}");
        report.AppendLine($"  Unknown0   {speed.Unknown0}");
        report.AppendLine($"  Unknown1   {speed.Unknown1}");

        var upgrades = MountSpeedProvider.Current();
        report.AppendLine();
        report.AppendLine($"plugin says  {upgrades.Owned}/{upgrades.Available} owned, missing={upgrades.HasMissing}");

        return Finish(report);
    }

    private static bool IsComplete(uint questId)
    {
        try
        {
            return questId != 0
                && FFXIVClientStructs.FFXIV.Client.Game.QuestManager.IsQuestComplete(questId);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Dumps every slot of the dynamic event container, which is what the Occult Crescent,
    /// Bozja, and Zadnor put their engagements into.
    /// </summary>
    /// <remarks>
    /// The names in this container are not self-explanatory and the type numbering is not
    /// documented anywhere, so this prints the raw fields rather than the plugin's interpretation
    /// of them. It is the only way to tell what an unexpected entry actually is.
    /// </remarks>
    internal static string DumpDynamicEvents()
    {
        var report = new StringBuilder();
        report.AppendLine("=== DynamicEventContainer ===");

        var container = FFXIVClientStructs.FFXIV.Client.Game.InstanceContent.DynamicEventContainer
            .GetInstance();

        if (container is null)
        {
            return Finish(report.AppendLine("Container is null. You are not in content that has one."));
        }

        report.AppendLine(
            $"currentEventId={container->CurrentEventId} currentIndex={container->CurrentEventIndex}");

        for (var index = 0; index < container->Events.Length; index++)
        {
            ref var dynamicEvent = ref container->Events[index];

            report.AppendLine(
                $"[{index}] state={dynamicEvent.State} id={dynamicEvent.DynamicEventId} " +
                $"type={dynamicEvent.DynamicEventType} eventType={dynamicEvent.EventType} " +
                $"enemyType={dynamicEvent.EnemyType} single={dynamicEvent.SingleBattle}");
            report.AppendLine(
                $"      name=\"{dynamicEvent.Name}\"");
            // The registration and warm-up durations sit in the same struct but are not exposed
            // by the client structs layer, so this reports what is reachable and lets the state
            // plus the countdown say the rest.
            report.AppendLine(
                $"      left={dynamicEvent.SecondsLeft}s duration={dynamicEvent.SecondsDuration}s " +
                $"startedAt={dynamicEvent.StartTimestamp}");
            report.AppendLine(
                $"      progress={dynamicEvent.Progress}% participants={dynamicEvent.Participants}" +
                $"/{dynamicEvent.MaxParticipants} (alt {dynamicEvent.MaxParticipants2}) " +
                $"level={dynamicEvent.MapMarker.RecommendedLevel} icon={dynamicEvent.MapMarker.IconId}");
        }

        DumpBattleAreaPanel(report);
        return Finish(report);
    }

    /// <summary>
    /// Dumps the battle area panel, which is the window the game shows the sign-up deadline in.
    /// </summary>
    /// <remarks>
    /// The engagement container and this panel are two views of the same fights, and they do not
    /// agree on everything: the countdown the player reads lives here. Both are printed together
    /// because the open question is which field in which of them carries it, and answering that
    /// from two separate dumps taken at different moments has already cost two attempts.
    /// </remarks>
    private static void DumpBattleAreaPanel(StringBuilder report)
    {
        report.AppendLine();
        report.AppendLine("=== AgentMycBattleAreaInfo ===");

        try
        {
            var agent = FFXIVClientStructs.FFXIV.Client.UI.Agent.AgentMycBattleAreaInfo.Instance();
            if (agent is null)
            {
                report.AppendLine("Agent is null.");
                return;
            }

            if (agent->MycDynamicEventData is null)
            {
                report.AppendLine("Agent is there, but its event data pointer is null.");
                return;
            }

            var events = agent->MycDynamicEventData->Array;
            report.AppendLine($"entries: {events.Length}");

            for (var i = 0; i < events.Length; i++)
            {
                ref var entry = ref events[i];

                report.AppendLine(
                    $"[{i}] state={entry.State} id={entry.Id} timeLeft={entry.TimeLeft} " +
                    $"participants={entry.ParticipantCount} name=\"{entry.Name}\"");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"Could not read the panel: {ex.Message}");
        }
    }

    /// <summary>
    /// Dumps the FATE progress window's data values. The window has to be open, since the
    /// values only exist while it is.
    /// </summary>
    internal static string DumpFateProgress()
    {
        var report = new StringBuilder();
        report.AppendLine("=== FateProgress addon ===");

        var handle = DalamudServices.GameGui.GetAddonByName(FateProgressAddon);
        var addon = (AtkUnitBase*)handle.Address;

        if (addon is null)
        {
            return Finish(report.AppendLine("Addon not found. Open the FATE progress window first."));
        }

        report.AppendLine($"visible={addon->IsVisible} atkValueCount={addon->AtkValuesCount}");

        // A non-zero count with a null array is possible while the addon is being built.
        if (addon->AtkValues is null)
        {
            return Finish(report.AppendLine("AtkValues is null, nothing to read yet."));
        }

        for (var i = 0; i < addon->AtkValuesCount; i++)
        {
            var value = addon->AtkValues[i];
            report.AppendLine($"  [{i,3}] {value.Type,-14} {Describe(value)}");
        }

        return Finish(report);
    }

    /// <summary>
    /// Reports the state of the map agent and tries adding a marker at the player's position,
    /// so it can be seen whether the call takes effect at all.
    /// </summary>
    internal static string ProbeMapMarkers()
    {
        var report = new StringBuilder();
        report.AppendLine("=== Map markers ===");

        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return Finish(report.AppendLine("AgentMap instance is null."));
        }

        // These are span capacities, not usage counts, so they never move. Reported only to
        // show the agent is alive; do not read a before/after difference into them.
        report.AppendLine(
            $"capacities (not counts): flags={agent->FlagMarkerCount} markers={agent->MapMarkers.Length} " +
            $"temp={agent->TempMapMarkers.Length} mini={agent->MiniMapMarkers.Length}");
        report.AppendLine(
            $"territory={DalamudServices.ClientState.TerritoryType} map={DalamudServices.ClientState.MapId}");

        var player = DalamudServices.ObjectTable.LocalPlayer;
        if (player is null)
        {
            return Finish(report.AppendLine("No local player, skipping the add probe."));
        }

        var world = new Vector3(player.Position.X, player.Position.Y, player.Position.Z);

        // The raw-pointer AddMapMarker probe used to live here and has been removed. It takes a
        // byte* that the game stores and dereferences on a later frame, so handing it a pinned
        // managed array crashed the client from inside AddonAreaMap.OnRequestedUpdate. Not worth
        // probing something that is known to be the wrong path.
        report.AppendLine($"player world position: {world.X:F0},{world.Y:F0},{world.Z:F0}");

        var onMap = AetheryteProvider.ToMapSpace(
            new Core.Model.WorldPosition(world.X, world.Y, world.Z),
            DalamudServices.ClientState.MapId);

        if (onMap is { } mapPosition)
        {
            agent->AddGatheringTempMarker(
                (int)mapPosition.X, (int)mapPosition.Z, 20, ProbeIconId, 0, "FateCompass probe");

            report.AppendLine(
                $"AddGatheringTempMarker(map {mapPosition.X:F0},{mapPosition.Z:F0}) sent");
        }
        else
        {
            report.AppendLine("skipped, could not convert to map space");
        }

        report.AppendLine();
        report.AppendLine("Now open the map. Is the probe marker visible, and where?");

        return Finish(report);
    }

    /// <summary>
    /// Lists every addon the game currently has loaded, with its name and visibility.
    /// </summary>
    /// <remarks>
    /// Needed because an addon's internal name is not its window title and is not documented.
    /// Guessing "FateProgress" produced nothing; reading the list while the window is open
    /// gives the real name in one step.
    /// </remarks>
    internal static string ListAddons(string filter)
    {
        var report = new StringBuilder();
        report.AppendLine($"=== Loaded addons (filter: '{filter}') ===");

        var stage = AtkStage.Instance();
        if (stage is null || stage->RaptureAtkUnitManager is null)
        {
            return Finish(report.AppendLine("AtkStage or the unit manager is null."));
        }

        var units = &stage->RaptureAtkUnitManager->AtkUnitManager.AllLoadedUnitsList;
        var shown = 0;

        for (var i = 0; i < units->Count; i++)
        {
            var unit = units->Entries[i].Value;
            if (unit is null)
            {
                continue;
            }

            var name = unit->NameString;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (filter.Length > 0 && !name.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            report.AppendLine($"  {name,-32} visible={unit->IsVisible} values={unit->AtkValuesCount}");
            shown++;
        }

        report.AppendLine();
        report.AppendLine($"{shown} of {units->Count} addons listed.");

        return Finish(report);
    }

    /// <summary>Mirrors the report into the log as well, so it survives the window closing.</summary>
    private static string Finish(StringBuilder report)
    {
        var text = report.ToString();
        DalamudServices.Log.Information("FateCompass diagnostics:\n{Report}", text);
        return text;
    }

    private static string Describe(AtkValue value)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        return value.Type switch
        {
            AtkValueType.Int => value.Int.ToString(culture),
            AtkValueType.Int64 => value.Int64.ToString(culture),
            AtkValueType.UInt => value.UInt.ToString(culture),
            AtkValueType.UInt64 => value.UInt64.ToString(culture),
            AtkValueType.Bool => value.Bool ? "true" : "false",
            AtkValueType.Float => value.Float.ToString(culture),

            // ConstString and String8 share a value, so they cannot both appear as cases.
            AtkValueType.String or AtkValueType.ManagedString or AtkValueType.ConstString =>
                value.String.Value is null ? "(null)" : $"\"{ReadString(value.String.Value)}\"",

            _ => "-",
        };
    }

    private static string ReadString(byte* pointer)
    {
        var length = 0;
        while (pointer[length] != 0 && length < 512)
        {
            length++;
        }

        return Encoding.UTF8.GetString(pointer, length);
    }
}
#endif
