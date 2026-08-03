using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FateHelper.Adapters;
using FateHelper.Configuration;
using FateHelper.Core.Engage;
using FateHelper.Core.History;
using FateHelper.Core.Localization;
using FateHelper.Services;
using FateHelper.UI;

namespace FateHelper;

/// <summary>
/// Plugin entry point. Owns the lifecycle and unregisters everything it registers.
/// </summary>
public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/fh";

    private readonly WindowSystem windowSystem = new("FateHelper");
    private readonly PluginConfiguration configuration;
    private readonly FateHistoryStore history;
    private readonly Localizer localizer;
    private readonly FateHelperController controller;
    private readonly MainWindow mainWindow;
    private readonly ConfigWindow configWindow;
    private readonly ReleaseNotesWindow newsWindow;
    private readonly StatusBarEntry statusBar;
    private readonly MinimapButton minimapButton;
    private readonly DebugWindow? debugWindow;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        DalamudServices.Initialise(pluginInterface);

        configuration = PluginConfiguration.Load();

        history = new FateHistoryStore(configuration.Settings);
        history.Load(configuration.History);

        localizer = new Localizer();
        foreach (var catalog in EmbeddedCatalogs.LoadAll())
        {
            localizer.Register(catalog);
        }

        ApplyLanguage(configuration.Settings.Language);

        Actions = new GameActions();
        controller = new FateHelperController(configuration, history, Actions, localizer);

        configWindow = new ConfigWindow(
            configuration,
            localizer,
            ApplyLanguage,
            () => mainWindow!.IsOpen = true,
            moving => minimapButton!.IsRepositioning = moving);
        mainWindow = new MainWindow(
            controller, configuration, localizer, history, () => configWindow.IsOpen = true);
        windowSystem.AddWindow(mainWindow);
        windowSystem.AddWindow(configWindow);

        newsWindow = new ReleaseNotesWindow(configuration, localizer);
        windowSystem.AddWindow(newsWindow);

        mainWindow.AddNewsButton(
            () => newsWindow.HasUnseen,
            () => newsWindow.LatestVersion,
            ToggleNews);

        ShowReleaseNotesIfThisIsAnUpdate();

        // A clickable entry next to the minimap, so the plugin is reachable without a command.
        statusBar = new StatusBarEntry(configuration, localizer, ToggleMain, ToggleAutomationFromStatusBar);

        // The icon stuck to the minimap itself, which reads as part of the HUD rather than as
        // a line of text above it.
        minimapButton = new MinimapButton(
            configuration, localizer, ToggleMain, ToggleAutomationFromStatusBar);
        windowSystem.AddWindow(minimapButton);

        // Developer tools, wired up only while DebugWindow.Enabled is true.
        //
        // The compiler is right that this is unreachable in a release build, and that is the
        // point: the switch is a constant precisely so the branch disappears rather than being
        // decided at runtime. Suppressed here rather than turned into a runtime flag, because a
        // runtime flag is one that can be turned back on by whoever finds it.
#pragma warning disable CS0162 // Unreachable code detected
        if (DebugWindow.Enabled)
        {
            debugWindow = new DebugWindow(configuration, controller);
            windowSystem.AddWindow(debugWindow);

            mainWindow.AddDebugButton(() => debugWindow.IsOpen = !debugWindow.IsOpen);
        }
#pragma warning restore CS0162

        DalamudServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Fate Helper window. Use /fh help for the full list.",
        });

        DalamudServices.PluginInterface.UiBuilder.Draw += DrawUi;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi += ToggleMain;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi += ToggleConfig;
        DalamudServices.PluginInterface.LanguageChanged += OnDalamudLanguageChanged;
    }

    /// <summary>
    /// The single gate to the game server (FH-01). Exposed for the window's teleport button so
    /// no other type has to hold its own instance.
    /// </summary>
    internal static IGameActions Actions { get; private set; } = null!;

    /// <summary>Everything registered above is unregistered here, in reverse order.</summary>
    private bool disposed;

    /// <summary>
    /// Tears down in the reverse order of setup, and stops the two callbacks that touch native
    /// memory first.
    /// </summary>
    /// <remarks>
    /// Order matters here more than it looks. A hot reload happens while the game is running
    /// and, in practice, while the window is open and drawing. If the framework tick or the
    /// draw callback runs after the plugin has begun unloading, it walks native pointers
    /// belonging to an assembly that is going away, and the game goes down with it. So: flag
    /// first, detach the two per-frame callbacks next, and only then release anything else.
    /// </remarks>
    public void Dispose()
    {
        disposed = true;

        controller.Dispose();
        DalamudServices.PluginInterface.UiBuilder.Draw -= DrawUi;

        // Straight after the two per-frame callbacks are detached and before anything else is
        // released. These are native UI nodes living inside the game's map window, so they have
        // to go while the plugin is still whole enough to take them down cleanly.
        NativeMapMarkers.Shutdown();

        DalamudServices.PluginInterface.LanguageChanged -= OnDalamudLanguageChanged;
        DalamudServices.PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfig;
        DalamudServices.PluginInterface.UiBuilder.OpenMainUi -= ToggleMain;

        DalamudServices.CommandManager.RemoveHandler(CommandName);

        statusBar.Dispose();

        windowSystem.RemoveAllWindows();
        mainWindow.Dispose();
        configWindow.Dispose();
        newsWindow.Dispose();
        minimapButton.Dispose();
        debugWindow?.Dispose();

        AetheryteProvider.ClearCache();
        ContentKindProvider.ClearCache();
        MountSpeedProvider.ClearCache();

        // Persist the history that was gathered this session.
        configuration.History = [.. history.Export()];
        configuration.Save();
    }

    private void DrawUi()
    {
        // The minimap button reads the game's own addon memory every frame, so this callback
        // must not run once teardown has begun.
        if (disposed)
        {
            return;
        }

        // Standing objectives are left out of the count. The badge answers "how many things are
        // worth doing right now", and a raid that runs all evening is not one of them.
        var recommended = controller.Ranked.Count(
            entry => entry.IsRecommended && !entry.IsSpecialObjective);
        statusBar.Update(recommended);
        minimapButton.RecommendedCount = recommended;
        windowSystem.Draw();
    }

    /// <summary>Right click on the status bar entry toggles the automatic preparation.</summary>
    private void ToggleAutomationFromStatusBar() => ToggleAutomation("toggle");

    private void ToggleMain() => mainWindow.IsOpen = !mainWindow.IsOpen;

    private void ToggleConfig() => configWindow.IsOpen = !configWindow.IsOpen;

    private void ToggleNews() => newsWindow.IsOpen = !newsWindow.IsOpen;

    /// <summary>
    /// Opens the release notes once after an update, and never on a first installation.
    /// </summary>
    /// <remarks>
    /// The two cases look identical from the settings alone — a fresh installation has seen no
    /// version, and so has somebody updating from a build that predates this window — so they are
    /// told apart by whether a configuration file existed at all.
    /// <para>
    /// A first installation is marked as read rather than left alone. Leaving it would make the
    /// very next start look like an update, and the notes would open then, which is worse than
    /// either alternative because it appears at random.
    /// </para>
    /// </remarks>
    private void ShowReleaseNotesIfThisIsAnUpdate()
    {
        if (configuration.IsFirstRun)
        {
            newsWindow.MarkSeen();
            return;
        }

        // The window marks itself as read when it opens, so this cannot repeat on the next start.
        if (configuration.Settings.ShowReleaseNotesOnUpdate && newsWindow.HasUnseen)
        {
            newsWindow.IsOpen = true;
        }
    }

    private void ApplyLanguage(string configured)
    {
        var resolved = LanguageResolver.Resolve(
            configured,
            DalamudServices.PluginInterface.UiLanguage,
            [.. localizer.AvailableCodes]);

        localizer.Use(resolved);
    }

    private void OnDalamudLanguageChanged(string language)
    {
        // Only follow Dalamud when the player asked for automatic.
        if (LanguageResolver.IsAutomatic(configuration.Settings.Language))
        {
            ApplyLanguage(configuration.Settings.Language);
        }
    }

    /// <summary>
    /// The text commands. Everything switchable is reachable from here, so a macro can drive
    /// the plugin without opening a window (FH-02).
    /// </summary>
    private void OnCommand(string command, string arguments)
    {
        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var verb = parts.Length > 0 ? parts[0].ToLowerInvariant() : string.Empty;
        var value = parts.Length > 1 ? parts[1].ToLowerInvariant() : string.Empty;

        switch (verb)
        {
            case "":
                ToggleMain();
                break;

            case "cfg":
            case "config":
                ToggleConfig();
                break;

            // Text commands arrive on the framework thread already, but routing them through
            // the same helper keeps every game call on one path.
            case "engage":
                DalamudServices.OnGameThread(RunEngageFromCommand, "engage command");
                break;

            case "auto":
                ToggleAutomation(value);
                break;

            case "news":
            case "whatsnew":
            case "changelog":
                ToggleNews();
                break;

            // Diagnostics, deliberately undocumented in the help text: it exists to work out
            // data layouts the game does not document, not for everyday use.
            case "debug":
                RunDiagnostics(value);
                break;

            case "help":
                DalamudServices.ChatGui.Print(localizer.Get(StringKeys.CommandHelp));
                break;

            default:
                DalamudServices.ChatGui.Print(localizer.Get(StringKeys.CommandUnknown));
                break;
        }
    }

    /// <summary>
    /// Reports the measured travel speed and offers to adopt it, replacing the estimate the
    /// ranking has been using.
    /// </summary>
    private void ReportSpeed()
    {
        var speed = controller.Speed;
        var average = speed.AverageYalmsPerSecond;

        if (average is null)
        {
            DalamudServices.ChatGui.Print("[Fate Helper] Not enough movement measured yet. Travel a while first.");
            return;
        }

        var content = ContentKindProvider.Current();

        DalamudServices.ChatGui.Print(
            $"[Fate Helper] Measured in {content}: average {average.Value:F1} y/s, " +
            $"peak {speed.PeakYalmsPerSecond:F1} y/s over {speed.SampleCount} samples.");

        // The peak is the useful number for route planning: it reflects how the distance between
        // FATEs actually gets covered, whereas the average is dragged down by fighting and
        // looting. Stored per zone, because what makes one slow is particular to it.
        var territory = DalamudServices.ClientState.TerritoryType;
        configuration.Settings.TravelSpeedByTerritory[territory] = speed.PeakYalmsPerSecond;
        configuration.Save();

        DalamudServices.ChatGui.Print(
            $"[Fate Helper] Stored {speed.PeakYalmsPerSecond:F1} y/s for this zone " +
            $"({configuration.Settings.TravelSpeedByTerritory.Count} measured so far).");
    }

    private void RunDiagnostics(string what)
    {
        switch (what)
        {
            case "fate":
            case "progress":
                Diagnostics.DumpFateProgress();
                DalamudServices.ChatGui.Print("[Fate Helper] FATE progress dumped to /xllog.");
                break;

            case "mount":
            case "ridingmap":
                Diagnostics.DumpMountSpeed();
                DalamudServices.ChatGui.Print("[Fate Helper] Mount speed dumped to /xllog.");
                break;

            case "events":
            case "dynamic":
                Diagnostics.DumpDynamicEvents();
                DalamudServices.ChatGui.Print("[Fate Helper] Dynamic events dumped to /xllog.");
                break;

            case "map":
            case "marker":
                Diagnostics.ProbeMapMarkers();
                DalamudServices.ChatGui.Print("[Fate Helper] Map marker probe written to /xllog.");
                break;

            case "speed":
                ReportSpeed();
                break;

            case "speedreset":
                controller.Speed.Reset();
                DalamudServices.ChatGui.Print("[Fate Helper] Speed measurement reset. Fly around, then /fh debug speed.");
                break;

            default:
                DalamudServices.ChatGui.Print(
                    "[Fate Helper] /fh debug progress  (open the FATE progress window first)");
                DalamudServices.ChatGui.Print(
                    "[Fate Helper] /fh debug map  (then open the map and see what appeared)");
                DalamudServices.ChatGui.Print(
                    "[Fate Helper] /fh debug speed  (measured travel speed, /fh debug speedreset to start over)");
                break;
        }
    }

    private void RunEngageFromCommand()
    {
        var plan = controller.RunEngage(ActionTrigger.Manual);
        if (plan.HasWork)
        {
            return;
        }

        var message = plan.BlockedReason switch
        {
            PlanBlockedReason.InCombat => localizer.Get(StringKeys.CommandEngageBlockedCombat),
            PlanBlockedReason.Occupied => localizer.Get(StringKeys.CommandEngageBlockedOccupied),
            PlanBlockedReason.NotPermittedHere => localizer.Get(StringKeys.ExcludedNotPermittedHere),
            _ => localizer.Get(StringKeys.CommandEngageNothing),
        };

        DalamudServices.ChatGui.Print(message);
    }

    private void ToggleAutomation(string value)
    {
        var settings = configuration.Settings;

        settings.AutoEngageOnFateEnter = value switch
        {
            "on" or "1" or "true" => true,
            "off" or "0" or "false" => false,
            _ => !settings.AutoEngageOnFateEnter,
        };

        configuration.Save();

        DalamudServices.ChatGui.Print(localizer.Get(
            settings.AutoEngageOnFateEnter ? StringKeys.CommandAutoOn : StringKeys.CommandAutoOff));
    }
}
