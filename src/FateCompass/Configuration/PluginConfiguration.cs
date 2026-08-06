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
    /// <remarks>
    /// Version 3: the teleport overhead shipped at fifteen seconds, which was an estimate and
    /// too high by a third. That number is not a preference somebody expressed, it is a
    /// measurement the plugin got wrong and then wrote into every configuration it created, so
    /// leaving it would mean the correction never reaches anybody who already installed. It is
    /// replaced only where it still stands at exactly the old default: a value somebody moved is
    /// a decision, and that stays theirs.
    /// </remarks>
    /// <remarks>
    /// Version 4: what a trip through an exploratory zone costs used to be the return plus an
    /// ordinary teleport, thirty seconds together. That decomposition was invented rather than
    /// observed, because the second hop cannot be timed on its own, and it came out half again
    /// too expensive. It is now one figure, walked through end to end, and the old pair is
    /// retired rather than carried forward: the sum meant something different from what the new
    /// value means, so there is nothing to convert.
    /// </remarks>
    private const int CurrentVersion = 4;

    /// <summary>The teleport overhead as it shipped before it was timed.</summary>
    private const float PreviousTeleportOverhead = 15f;

    public FateCompassSettings Settings { get; set; } = new();

    /// <summary>Flattened sighting log, restored into the history store on load.</summary>
    public List<FateSighting> History { get; set; } = [];

    /// <summary>
    /// Last reading of the shared FATE standing. Persisted because it can only be read while
    /// the game's progress window is open, so without a cache it would be blank most of the
    /// time.
    /// </summary>
    public List<SharedFateZone> SharedFateZones { get; set; } = [];

    /// <summary>
    /// How high each aetheryte stands, in world yalms, keyed by its row id.
    /// </summary>
    /// <remarks>
    /// Learned rather than read: the game data has no elevation for an aetheryte, so it comes
    /// from having stood near one. Persisted because a table that empties on every restart would
    /// spend most of its life useless, and because heights only move when a patch moves them.
    /// </remarks>
    public Dictionary<uint, float> AetheryteElevations { get; set; } = [];

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

        if (Version < 2)
        {
            Settings.ExploratoryTravelSpeedYalmsPerSecond = fresh.ExploratoryTravelSpeedYalmsPerSecond;
            Settings.ExploratorySlowTravelSpeedYalmsPerSecond = fresh.ExploratorySlowTravelSpeedYalmsPerSecond;
        }

        // Only where it is still the old default. Somebody who moved the slider expressed an
        // opinion, and correcting a number is not licence to overwrite one.
        if (Version < 3
            && Math.Abs(Settings.TeleportOverheadSeconds - PreviousTeleportOverhead) < 0.01f)
        {
            Settings.TeleportOverheadSeconds = fresh.TeleportOverheadSeconds;
        }

        // Nothing to carry over. The old pair added up to something the new single figure is
        // not, so a converted value would be a wrong number wearing the right name.
        if (Version < 4)
        {
            Settings.ExploratoryTravelOverheadSeconds = fresh.ExploratoryTravelOverheadSeconds;
        }

        DalamudServices.Log.Information(
            "Configuration: migrated from version {Old} to {New}", Version, CurrentVersion);

        Version = CurrentVersion;
        Save();
    }

    internal void Save() => DalamudServices.PluginInterface.SavePluginConfig(this);
}
