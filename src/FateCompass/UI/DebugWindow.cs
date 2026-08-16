// Developer tools. Present only in a build that defines FATECOMPASS_DEVTOOLS, which no
// committed file does: it comes from Directory.Build.local.props, which is ignored by git.
// In every published build this file compiles to nothing at all.
#if FATECOMPASS_DEVTOOLS
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FateCompass.Adapters;
using FateCompass.Configuration;
using FateCompass.Services;

// CA1305 asks for an explicit culture on every interpolated append below. Same reasoning as in
// Diagnostics: these dumps are read by a person and pasted into a chat, never parsed and never
// shown to a player, so the current culture is the right thing to format with and naming it a
// dozen times would only add noise.
#pragma warning disable CA1305

namespace FateCompass.UI;

/// <summary>
/// Developer tools: runs the diagnostic probes and shows their output where it can be read and
/// copied, instead of making the player dig through the Dalamud log.
/// </summary>
/// <remarks>
/// Gated behind <see cref="Enabled"/>, a compile-time constant. Set it to false before this
/// goes anywhere near another person: none of this is meant for a player, and the map probe
/// writes markers into the game's UI.
/// <para>
/// It is a constant rather than a setting on purpose. A setting can be switched on by anyone
/// who finds it; a constant cannot be reached at all in a build where it is false.
/// </para>
/// </remarks>
internal sealed class DebugWindow : Window, IDisposable
{
    /// <summary>Master switch for the whole developer surface. Set to false for a release.</summary>
    internal const bool Enabled = false;

    private readonly PluginConfiguration configuration;
    private readonly FateCompassController controller;

    private string output = string.Empty;

    /// <summary>Substring filter for the addon list. Empty lists everything.</summary>
    private string addonFilter = "Fate";

    /// <summary>First icon id of the probe range. The field marker glyphs are the first guess.</summary>
    private int iconProbeStart = 61241;

    internal DebugWindow(PluginConfiguration configuration, FateCompassController controller)
        : base("Fate Compass: diagnostics##FateCompassDebug")
    {
        this.configuration = configuration;
        this.controller = controller;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(560, 380),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }


    public void Dispose()
    {
        // Nothing unmanaged is held.
    }

    public override void Draw()
    {
        ImGui.TextDisabled(
            "Developer tools. Run a probe, then copy the output. Everything is also written to /xllog.");
        ImGui.Separator();

        // Every probe reads game memory, so it runs on the framework thread and hands the text
        // back afterwards. Calling into the game from the draw callback is the mistake that
        // took the client down once already.
        if (ImGui.Button("FATE progress"))
        {
            DalamudServices.OnGameThread(
                () => output = Diagnostics.DumpFateProgress(), "dump fate progress");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open the game's FATE progress window first, then press this.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Find addon"))
        {
            var filter = addonFilter;
            DalamudServices.OnGameThread(
                () => output = Diagnostics.ListAddons(filter), "list addons");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Open the game window you are looking for, then press this.");
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(120f);
        ImGui.InputText("##addonFilter", ref addonFilter, 64);

        ImGui.SameLine();
        if (ImGui.Button("Map markers"))
        {
            DalamudServices.OnGameThread(
                () => output = Diagnostics.ProbeMapMarkers(), "probe map markers");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Adds two probe markers, then open the map and report which one you can see.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Icon probe"))
        {
            var position = controller.Player?.Position;
            var first = (uint)Math.Max(iconProbeStart, 1);

            if (position is { } around)
            {
                DalamudServices.OnGameThread(
                    () => output = Adapters.MapService.ProbeMarkerIcons(around, first, 8),
                    "probe marker icons");
            }
            else
            {
                output = "No local player to place the probe markers around.";
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Drops eight markers next to you, one per icon id from the number on the right.\n" +
                "Open the map and see which of them are digits.");
        }

        ImGui.SameLine();
        ImGui.SetNextItemWidth(90f);
        ImGui.InputInt("##iconProbeStart", ref iconProbeStart, 0);

        ImGui.SameLine();
        if (ImGui.Button("Mount speed"))
        {
            DalamudServices.OnGameThread(
                () => output = Diagnostics.DumpMountSpeed(), "dump mount speed");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Which riding maps this zone has, and which of them you own.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Dynamic events"))
        {
            DalamudServices.OnGameThread(
                () => output = Diagnostics.DumpDynamicEvents(), "dump dynamic events");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Occult Crescent, Bozja, Zadnor: every engagement slot with its raw fields.");
        }

        // Not run on the game thread: the probe samples on the framework thread every tick and
        // this only formats what it already captured. Nothing here follows a game pointer.
        ImGui.SameLine();
        if (ImGui.Button("Warp state"))
        {
            output = DescribeWarpState();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "What kind of warp the client reports. Press after teleporting to see what it saw.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear warps"))
        {
            Adapters.WarpProbe.Clear();
            output = "Warp history cleared. Teleport, then press \"Warp state\".";
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Empty the history so the next teleport stands alone.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Travel speed"))
        {
            output = DescribeSpeed();
        }

        ImGui.SameLine();
        if (ImGui.Button("Reset speed"))
        {
            controller.Speed.Reset();
            output = "Speed measurement reset. Travel a while, then press Travel speed.";
        }

        ImGui.SameLine();
        if (ImGui.Button("Aetherytes"))
        {
            DalamudServices.OnGameThread(() => output = DescribeAetherytes(), "list aetherytes");
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("Which aetherytes this zone has, as the plugin can see them.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Ranked list"))
        {
            output = DescribeRanked();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip("What the plugin itself sees, and why each entry is where it is.");
        }

        ImGui.SameLine();
        if (ImGui.Button("Native markers"))
        {
            output = DescribeNativeMarkers();
        }

        ImGui.SameLine();
        if (ImGui.Button("Current settings"))
        {
            output = DescribeSettings();
        }

        ImGui.Separator();

        if (ImGui.Button("Copy to clipboard") && output.Length > 0)
        {
            ImGui.SetClipboardText(output);
        }

        ImGui.SameLine();
        ImGui.TextDisabled($"{output.Length} characters");

        ImGui.Separator();

        // Read-only rather than plain text, so it can be selected and scrolled.
        var buffer = output;
        ImGui.InputTextMultiline(
            "##debugOutput",
            ref buffer,
            64 * 1024,
            new Vector2(-1, -1),
            ImGuiInputTextFlags.ReadOnly);
    }

    /// <summary>
    /// The aetherytes the plugin can see in this zone.
    /// </summary>
    /// <remarks>
    /// The one thing a two-hop route out of an exploratory zone needs and might not have. The
    /// return spell lands the player at camp, and from there the trip continues by aetheryte, so
    /// the whole idea rests on those aetherytes being in the data the plugin already reads. If
    /// this comes back empty, no amount of arithmetic will help.
    /// </remarks>
    private static string DescribeAetherytes()
    {
        var territory = DalamudServices.ClientState.TerritoryType;
        var report = new System.Text.StringBuilder();

        report.AppendLine($"=== Aetherytes in territory {territory} ===");

        var found = AetheryteProvider.ForTerritory(territory);
        report.AppendLine($"found: {found.Count}");
        report.AppendLine();

        foreach (var aetheryte in found)
        {
            report.AppendLine(
                $"  id={aetheryte.Id,-6} {aetheryte.Name}");
            report.AppendLine(
                $"          map position {aetheryte.Position.X:F1}, {aetheryte.Position.Z:F1}");
        }

        if (found.Count == 0)
        {
            report.AppendLine(
                "None. The next section shows what this map does carry, so the aetherytes can be");
            report.AppendLine(
                "found under whatever the game files them as instead.");
        }

        report.AppendLine();
        Diagnostics.AppendMapMarkers(report, DalamudServices.ClientState.MapId);

        return report.ToString();
    }

    /// <summary>
    /// Every entry the plugin holds, with the fields that decide where it lands.
    /// </summary>
    /// <remarks>
    /// The probes so far all report what the game says. This one reports what the plugin made of
    /// it, which is the other half and the half that has been guessed at repeatedly: when an
    /// engagement fails to appear, the question is never "what does the game hold" but "which of
    /// my own rules dropped it", and that was being answered by reading code rather than by
    /// looking.
    /// </remarks>
    private string DescribeRanked()
    {
        var ranked = controller.Ranked;
        var report = new System.Text.StringBuilder();

        report.AppendLine($"=== Ranked list: {ranked.Count} entrie(s) ===");
        report.AppendLine($"zone={controller.CurrentZoneName} territory={DalamudServices.ClientState.TerritoryType}");
        report.AppendLine();

        if (controller.Player is { } player)
        {
            report.AppendLine(
                $"player: content={player.Content} level={player.Level} " +
                $"speed={configuration.Settings.SpeedFor(player):F1} y/s " +
                $"ridingMaps={player.HasMountSpeedUpgrades}");
            report.AppendLine();
        }

        foreach (var entry in ranked)
        {
            var fate = entry.Fate;

            report.AppendLine(
                $"[{(entry.Rank > 0 ? entry.Rank.ToString(System.Globalization.CultureInfo.InvariantCulture) : "-")}] " +
                $"{fate.Name}");
            report.AppendLine(
                $"      kind={fate.Kind} state={fate.State} source={fate.Source} level={fate.Level}");
            report.AppendLine(
                $"      reason={entry.ExclusionReason} recommended={entry.IsRecommended} " +
                $"sidelined={entry.IsSidelined} score={entry.Score:F2}");
            report.AppendLine(
                $"      started={fate.HasStarted} left={fate.SecondsRemaining}s progress={fate.ProgressPercent}%");
            report.AppendLine(
                $"      gate={fate.HasRegistrationGate} open={fate.IsRegistrationOpen} " +
                $"signUp={(fate.SecondsUntilRegistrationCloses?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "none")} " +
                $"distance={entry.DistanceYalms:F0}y travel={entry.EstimatedTravelSeconds:F0}s");
        }

        if (ranked.Count == 0)
        {
            report.AppendLine("Nothing at all. Either the zone has none, or nothing survived the providers.");
        }

        return report.ToString();
    }

    private string DescribeSpeed()
    {
        var speed = controller.Speed;
        var average = speed.AverageYalmsPerSecond;

        if (average is null)
        {
            return "Not enough movement measured yet. Travel a while first.";
        }

        // The peak is the number that matters for routing: it reflects the fastest the player
        // actually travels, where the average is dragged down by fighting and standing still.
        // It is stored per kind of content, because the exploratory zones have no flying and
        // measure roughly a quarter slower than the open world.
        var content = ContentKindProvider.Current();
        var territory = DalamudServices.ClientState.TerritoryType;

        // Kept per zone. Two buckets were one too few: the Occult Crescent measured fifteen and
        // Bozja nine, both exploratory, so each measurement threw the other away.
        configuration.Settings.TravelSpeedByTerritory[territory] = speed.PeakYalmsPerSecond;
        configuration.Save();

        return $"""
            Travel speed
              average   : {average.Value:F1} yalms/s
              peak      : {speed.PeakYalmsPerSecond:F1} yalms/s
              samples   : {speed.SampleCount}
              content   : {content}
              territory : {territory}

            Stored for this zone. Zones never measured fall back to:
              open world  : {configuration.Settings.TravelSpeedYalmsPerSecond:F1} yalms/s
              exploratory : {configuration.Settings.ExploratoryTravelSpeedYalmsPerSecond:F1} yalms/s

            Measured so far: {configuration.Settings.TravelSpeedByTerritory.Count} zone(s).
            """;
    }

    /// <summary>
    /// What the client reported about recent warps.
    /// </summary>
    /// <remarks>
    /// The two questions this is meant to settle: which <c>WarpType</c> an aetheryte hop inside
    /// an exploratory zone produces, and whether the value survives the warp or is only set
    /// during it. Both decide how a later "mount after a teleport" feature detects anything at
    /// all, and neither can be answered from outside the game.
    /// </remarks>
    private static string DescribeWarpState()
    {
        var last = Adapters.WarpProbe.Last;
        var history = Adapters.WarpProbe.History;

        var lines = new System.Text.StringBuilder();
        lines.AppendLine("Warp state");
        lines.AppendLine($"  probe    : {Adapters.WarpProbe.Status}");
        lines.AppendLine(
            $"  now      : warp={last.Warp} ({(int)last.Warp})  transition={last.TransitionState}  " +
            $"load={last.LoadState}  territory={last.TerritoryId}");
        lines.AppendLine();

        if (history.Count == 0)
        {
            lines.AppendLine("  No changes recorded yet. Teleport, then press this again.");
            return lines.ToString();
        }

        lines.AppendLine($"  Changes, newest first ({history.Count}):");
        var now = DateTime.UtcNow;
        foreach (var o in history)
        {
            lines.AppendLine(
                $"    -{(now - o.AtUtc).TotalSeconds,6:F1}s  warp={o.Warp,-22} ({(int)o.Warp,2})  " +
                $"transition={o.TransitionState,3}  load={o.LoadState,3}  territory={o.TerritoryId}");
        }

        lines.AppendLine();
        lines.AppendLine("  What to look for: whether a warp value other than None appears at all,");
        lines.AppendLine("  which one it is for an aetheryte hop inside this zone, and whether it");
        lines.AppendLine("  stays set afterwards or falls back to None once you have arrived.");

        return lines.ToString();
    }

    private static string DescribeNativeMarkers() => $"""
        Native map markers (KamiToolKit)
          available   : {Adapters.NativeMapMarkers.IsAvailable}
          status      : {Adapters.NativeMapMarkers.Status}
          last placed : {Adapters.NativeMapMarkers.LastMarkerCount} marker(s) on map {Adapters.NativeMapMarkers.LastMapId}
          client map  : {DalamudServices.ClientState.MapId}

        The two map numbers have to agree. A marker is tagged with the map it belongs to, and one
        tagged for a different map is simply not drawn.

        When this is off, the markers fall back to the game's own marker system. That still
        places them, it just cannot follow a map drag as smoothly.
        """;

    private string DescribeSettings()
    {
        var s = configuration.Settings;

        return $"""
            Current values, for baking into the defaults

              MinimapButtonOffsetX      = {s.MinimapButtonOffsetX:F1}
              MinimapButtonOffsetY      = {s.MinimapButtonOffsetY:F1}
              MinimapButtonSize         = {s.MinimapButtonSize:F1}
              TravelSpeedYalmsPerSecond = {s.TravelSpeedYalmsPerSecond:F1}
              TeleportOverheadSeconds   = {s.TeleportOverheadSeconds:F1}
              NearlyDoneThresholdPercent= {s.NearlyDoneThresholdPercent}
              MinimumSecondsRemaining   = {s.MinimumSecondsRemaining}
              VerticalTravelWeight      = {s.VerticalTravelWeight:F1}

              Weights: distance {s.Weights.Distance:F2}, time {s.Weights.TimeRemaining:F2},
                       progress {s.Weights.Progress:F2}, occupancy {s.Weights.Occupancy:F2},
                       registration bonus {s.Weights.RegistrationOpenBonus:F2}
            """;
    }
}
#endif
