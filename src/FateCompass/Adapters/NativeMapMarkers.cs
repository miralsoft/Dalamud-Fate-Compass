using System.Numerics;
using FateCompass.Core.Model;
using FateCompass.Services;
using KamiToolKit;
using KamiToolKit.Classes;
using KamiToolKit.MapOverlay;

namespace FateCompass.Adapters;

/// <summary>
/// Places the running order into the game's map as real UI nodes, through KamiToolKit.
/// </summary>
/// <remarks>
/// The whole point of going native is that the markers become part of the map rather than
/// something painted over it. They then move with a drag exactly like the game's own icons,
/// because they are the game's own kind of icon, and they survive the map being rebuilt.
/// <para>
/// This is the only third-party code in the plugin and it is the code that touches native UI
/// memory, so it is fenced off (FH-12). Every call goes through here, every call is guarded, and
/// the first sign of trouble switches the whole feature off for the rest of the session rather
/// than being retried. A feature that stops working is a nuisance; a client that stops working
/// is not.
/// </para>
/// <para>
/// Being honest about what that guard is worth: a <c>try</c> block catches a managed exception,
/// not an access violation. What it actually buys is three things. A library that is missing,
/// or whose shape has changed, fails on the first call and is never called again. A single
/// failure latches the feature off instead of repeating twice a second. And teardown runs in a
/// fixed order with the feature already disabled, which is where a hot reload would otherwise
/// go wrong. Beyond that, the fallback below keeps the markers working through the game's own
/// marker system, so switching this off costs presentation, not function.
/// </para>
/// </remarks>
internal static class NativeMapMarkers
{
    /// <summary>Name the library reports itself under.</summary>
    private const string PluginName = "FateCompass";

    private static MapOverlayController? controller;

    /// <summary>Once true, nothing here ever runs again this session.</summary>
    private static bool failed;

    /// <summary>True while the native path can be used.</summary>
    internal static bool IsAvailable => controller is not null && !failed;

    /// <summary>Why it is unavailable, for the diagnostics window.</summary>
    internal static string Status { get; private set; } = "not started";

    /// <summary>How many markers were handed over on the last attempt.</summary>
    internal static int LastMarkerCount { get; private set; }

    /// <summary>The map id those markers were tagged with.</summary>
    internal static uint LastMapId { get; private set; }

    /// <summary>
    /// Brings the overlay up. Safe to call when it is already up, and safe to fail.
    /// </summary>
    internal static void Initialise()
    {
        if (failed || controller is not null)
        {
            return;
        }

        try
        {
            // The library resolves Dalamud's services through a plugin interface it has to be
            // handed first. Without this its own service lookup returns null and the very first
            // call fails, which is exactly what happened.
            KamiToolKitLibrary.Initialize(DalamudServices.PluginInterface, PluginName);

            var created = new MapOverlayController();
            created.Enable();

            // Attaching is not the same as showing. Without this the overlay sits on the map
            // holding markers that are never drawn, which looks exactly like the markers not
            // having been placed at all.
            created.IsVisible = true;

            controller = created;
            Status = "ready";
            DalamudServices.Log.Information("NativeMapMarkers: map overlay enabled");
        }
        catch (Exception ex)
        {
            Fail("could not start the map overlay", ex);
        }
    }

    /// <summary>
    /// Replaces the markers with the given set. Does nothing when the native path is off.
    /// </summary>
    internal static bool Apply(IReadOnlyList<MapMarkerRequest> markers, uint mapId)
    {
        ArgumentNullException.ThrowIfNull(markers);

        if (!IsAvailable || DalamudServices.Framework.IsFrameworkUnloading)
        {
            return false;
        }

        try
        {
            controller!.IsVisible = true;
            controller.RemoveAllMarkers();

            foreach (var marker in markers)
            {
                controller.AddMarker(new MapMarkerInfo
                {
                    MapId = mapId,
                    Position = new Vector2(marker.Position.X, marker.Position.Z),
                    IconId = marker.IconId,
                    Tooltip = new Lumina.Text.SeStringBuilder()
                        .Append(marker.Tooltip)
                        .ToReadOnlySeString(),
                });
            }

            LastMarkerCount = markers.Count;
            LastMapId = mapId;
            return true;
        }
        catch (Exception ex)
        {
            Fail("could not place the markers", ex);
            return false;
        }
    }

    /// <summary>Removes every marker, leaving the overlay itself running.</summary>
    internal static void Clear()
    {
        if (!IsAvailable || DalamudServices.Framework.IsFrameworkUnloading)
        {
            return;
        }

        try
        {
            controller!.RemoveAllMarkers();
        }
        catch (Exception ex)
        {
            Fail("could not clear the markers", ex);
        }
    }

    /// <summary>
    /// Tears the overlay down. Each step is independent, because a step that throws must not
    /// stop the ones after it from running.
    /// </summary>
    /// <remarks>
    /// The order matters and is the same reasoning as in the plugin's own teardown: mark the
    /// feature dead first, so nothing else can call in while this is happening, then release.
    /// A hot reload runs this while the map may be open and drawing.
    /// </remarks>
    internal static void Shutdown()
    {
        var owned = controller;

        failed = true;
        controller = null;
        Status = "shut down";

        if (owned is null)
        {
            return;
        }

        Try(owned.RemoveAllMarkers, "remove markers");
        Try(owned.Disable, "disable overlay");
        Try(owned.Dispose, "dispose overlay");
        Try(KamiToolKitLibrary.Shutdown, "shut the library down");
    }

    /// <summary>Runs one teardown step, swallowing and logging whatever it does.</summary>
    private static void Try(Action step, string what)
    {
        try
        {
            step();
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "NativeMapMarkers: {What} failed during shutdown", what);
        }
    }

    /// <summary>
    /// Switches the feature off for good and says why, once.
    /// </summary>
    /// <remarks>
    /// Logged as a warning, not an error. Nothing here is broken from the player's side: the
    /// markers fall back to the game's own system and everything else carries on. An error line
    /// in the log reads as "something is wrong with your client" and sends people looking for a
    /// problem that is already handled.
    /// </remarks>
    private static void Fail(string what, Exception ex)
    {
        Status = $"disabled: {what}";
        DalamudServices.Log.Warning(
            ex,
            "NativeMapMarkers: {What}. Falling back to the game's own map markers for this session.",
            what);

        var owned = controller;
        controller = null;
        failed = true;

        if (owned is not null)
        {
            Try(owned.Dispose, "dispose after failure");
        }
    }
}

/// <summary>One marker to place: where, which icon, and what it says on hover.</summary>
internal readonly record struct MapMarkerRequest(WorldPosition Position, uint IconId, string Tooltip);
