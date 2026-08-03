using Dalamud.Configuration;
using FateCompass.Core.Configuration;
using FateCompass.Core.History;
using FateCompass.Core.Progress;
using FateCompass.Services;

namespace FateCompass.Configuration;

/// <summary>
/// Persistence wrapper around the core settings.
/// </summary>
/// <remarks>
/// The settings themselves live in the core so they can be tested and so no framework type
/// leaks into the logic (FH-04). This class exists only to satisfy Dalamud's storage interface
/// and to carry the saved FATE history alongside them.
/// </remarks>
[Serializable]
internal sealed class PluginConfiguration : IPluginConfiguration
{
    /// <summary>Layout version of the saved file, used to migrate settings whose meaning changed.</summary>
    public int Version { get; set; } = CurrentVersion;

    /// <summary>
    /// Bumped whenever a stored value stops meaning what it used to.
    /// </summary>
    /// <remarks>
    /// Version 2: the exploratory travel speed used to be one figure for Eureka, Bozja, and the
    /// Crescent together, and every measurement anywhere overwrote it. It is now the speed with
    /// the zone's riding map bought, with a second figure for without. A value carried over from
    /// the old scheme is a measurement from whichever zone happened to be last, which is not what
    /// either of the new fields means.
    /// </remarks>
    private const int CurrentVersion = 2;

    public FateCompassSettings Settings { get; set; } = new();

    /// <summary>Flattened sighting log, restored into the history store on load.</summary>
    public List<FateSighting> History { get; set; } = [];

    /// <summary>
    /// Last reading of the shared FATE standing. Persisted because it can only be read while
    /// the game's progress window is open, so without a cache it would be blank most of the
    /// time.
    /// </summary>
    public List<SharedFateZone> SharedFateZones { get; set; } = [];

    public DateTimeOffset SharedFateReadAt { get; set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// True when there was no saved configuration at all, so this is a first installation
    /// rather than an update.
    /// </summary>
    /// <remarks>
    /// Internal on purpose: it describes this run, not the file, and Dalamud only writes public
    /// members, so it cannot accidentally be persisted and become permanently true.
    /// <para>
    /// The distinction matters for the release notes. Somebody installing for the first time has
    /// no "before" to compare against, so a list of what changed is noise at the one moment they
    /// should be looking at the plugin itself.
    /// </para>
    /// </remarks>
    internal bool IsFirstRun { get; private set; }

    internal static PluginConfiguration Load()
    {
        var stored = DalamudServices.PluginInterface.GetPluginConfig() as PluginConfiguration;
        var loaded = stored ?? new PluginConfiguration();

        loaded.IsFirstRun = stored is null;
        loaded.Migrate();
        return loaded;
    }

    /// <summary>
    /// Brings a file written by an older version up to date.
    /// </summary>
    /// <remarks>
    /// Only for values whose meaning changed, never for values whose default changed. A new
    /// default is for new installations; overwriting a setting somebody chose because a later
    /// version prefers a different number would be taking their decision away.
    /// </remarks>
    private void Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return;
        }

        var fresh = new FateCompassSettings();
        Settings.ExploratoryTravelSpeedYalmsPerSecond = fresh.ExploratoryTravelSpeedYalmsPerSecond;
        Settings.ExploratorySlowTravelSpeedYalmsPerSecond = fresh.ExploratorySlowTravelSpeedYalmsPerSecond;

        DalamudServices.Log.Information(
            "Configuration: migrated from version {Old} to {New}, exploratory travel speeds reset",
            Version, CurrentVersion);

        Version = CurrentVersion;
        Save();
    }

    internal void Save() => DalamudServices.PluginInterface.SavePluginConfig(this);
}
