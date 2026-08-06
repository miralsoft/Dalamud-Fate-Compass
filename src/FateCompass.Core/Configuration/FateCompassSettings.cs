using FateCompass.Core.Announce;
using FateCompass.Core.Localization;
using FateCompass.Core.Model;

namespace FateCompass.Core.Configuration;

/// <summary>
/// Every setting the plugin has. A plain object with no framework types, so the core owns the
/// shape and the plugin layer only handles persistence.
/// </summary>
/// <remarks>
/// Defaults matter here. FH-02 requires that every automatic behaviour ships switched off, so
/// <see cref="AutoEngageOnFateEnter"/> and <see cref="AutoRemountAfterFate"/> default to false.
/// </remarks>
public sealed class FateCompassSettings
{
    /// <summary>Schema version, so a later change can migrate rather than reset.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Master switch. When false the plugin observes nothing and acts on nothing.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Interface language as a lowercase tag, or "auto" to follow Dalamud. An unknown value
    /// resolves to English rather than leaving the window blank.
    /// </summary>
    public string Language { get; set; } = LanguageInfo.AutomaticCode;

    /// <summary>
    /// Show the clickable entry in the server info bar next to the minimap. On by default,
    /// because it is the only way to reach the plugin without typing a command.
    /// </summary>
    public bool ShowStatusBarEntry { get; set; } = true;

    /// <summary>
    /// Show a clickable icon stuck to the minimap's edge, in the manner of the weather glyph.
    /// On by default, because it is the least intrusive way to reach the plugin.
    /// </summary>
    public bool ShowMinimapButton { get; set; } = true;

    /// <summary>
    /// Show the compact card list instead of the full table.
    /// </summary>
    /// <remarks>
    /// Two views of the same data, deliberately in one window rather than two: the compact one
    /// answers "what next, go" while farming, the table answers "which of these is actually
    /// worth it" while deciding. Splitting them into separate windows would double the state
    /// for no gain and force the choice before it can be made.
    /// </remarks>
    public bool CompactView { get; set; } = true;

    /// <summary>
    /// Retired. The compact view shows as many cards as the window has room for.
    /// </summary>
    /// <remarks>
    /// Kept so an existing configuration file still loads, and ignored. It was never reachable
    /// from the interface, so it could only ever hold its old default of five, and a wide window
    /// went on hiding a seventh FATE behind a limit nobody had chosen and nobody could change.
    /// </remarks>
    [Obsolete("The compact view fits as many cards as the window allows.")]
    public int CompactEntries { get; set; } = 5;

    /// <summary>
    /// Horizontal nudge for the minimap icon, in pixels. Positive moves it right, towards the
    /// minimap.
    /// </summary>
    /// <remarks>
    /// The minimap addon's box is noticeably wider than the visible circle, so anchoring to the
    /// box alone leaves a gap. The default compensates for that; this exists because the exact
    /// inset depends on the HUD layout and is easier for the player to nudge than for anyone to
    /// calculate.
    /// </remarks>
    public float MinimapButtonOffsetX { get; set; } = 60f;

    /// <summary>Vertical nudge for the minimap icon, in pixels. Positive moves it down.</summary>
    public float MinimapButtonOffsetY { get; set; } = 18f;

    /// <summary>Edge length of the minimap icon in pixels, before HUD scaling.</summary>
    /// <remarks>
    /// These three defaults are not guesses. They were dialled in by hand against a live HUD
    /// and then read back out of the saved configuration, so a fresh install starts where the
    /// tuning ended rather than where the first estimate was.
    /// </remarks>
    public float MinimapButtonSize { get; set; } = 36f;

    // --- Engage sequence -------------------------------------------------------------

    /// <summary>
    /// Run the engage sequence automatically when the player enters a FATE. Ships disabled
    /// (FH-02). The manual trigger is always available regardless of this setting.
    /// </summary>
    public bool AutoEngageOnFateEnter { get; set; }

    /// <summary>Remount automatically once the FATE is over. Ships disabled (FH-02).</summary>
    public bool AutoRemountAfterFate { get; set; }

    public bool EngageDismount { get; set; } = true;

    public bool EngageLevelSync { get; set; } = true;

    public bool EngageTankStance { get; set; } = true;

    /// <summary>
    /// Seconds to wait after a FATE ends before attempting a remount, so the attempt does not
    /// collide with the tail of combat.
    /// </summary>
    public int RemountDelaySeconds { get; set; } = 2;

    // --- Ranking ---------------------------------------------------------------------

    /// <summary>
    /// Retired. The rank numbers are no longer painted over the map.
    /// </summary>
    /// <remarks>
    /// Kept so an existing configuration file still loads, and ignored. Drawing over the map
    /// meant computing a screen position and drawing a frame late, so the digits drifted every
    /// time the map was dragged. The game's own markers do not, because they are part of the
    /// map, and carrying the number in the marker's own icon says the same thing without any of
    /// that.
    /// </remarks>
    [Obsolete("The running order is carried by the marker icons themselves.")]
    public bool ShowRankNumbersOnMap { get; set; }

    /// <summary>
    /// Types the game's <c>&lt;flag&gt;</c> placeholder into the chat box when the flag button
    /// is used, so the position is one keypress away from being shared.
    /// </summary>
    /// <remarks>
    /// Typing is not sending. The text is left in the box exactly as if the player had typed it,
    /// which leaves both the channel and the decision with them (FH-01). Anything already in the
    /// box is kept.
    /// </remarks>
    public bool TypeFlagIntoChat { get; set; } = true;

    /// <summary>
    /// Show a needle under each entry pointing the way to it, relative to where the character is
    /// facing.
    /// </summary>
    /// <remarks>
    /// On by default. It is a display, not an action, and it answers the question the window is
    /// otherwise silent about: the list says how far, the needle says which way.
    /// <para>
    /// It follows the character rather than the camera. The camera answers "where am I looking",
    /// the character answers "where would I go if I pressed forward", and the second is the
    /// question somebody flying is asking.
    /// </para>
    /// </remarks>
    public bool ShowCompassNeedle { get; set; } = true;

    // --- Announcing the leading FATE -----------------------------------------------------

    /// <summary>
    /// Whether the announcement feature exists at all. Off ships nothing: no switch in the
    /// window, no channel picker, nothing written anywhere.
    /// </summary>
    /// <remarks>
    /// A separate switch from <see cref="AnnounceNextFate"/> on purpose. This one decides
    /// whether the feature is available; that one is the everyday on and off, and lives where it
    /// is used rather than buried in a settings tab.
    /// </remarks>
    public bool AllowChatAnnounce { get; set; }

    /// <summary>
    /// Whether a new FATE raises the pop-up in the corner of the screen.
    /// </summary>
    /// <remarks>
    /// Ships off. The window already lists every FATE and the minimap icon already carries the
    /// count, so the pop-up repeats what is on screen while covering part of it. The sound is a
    /// separate switch and stays on: a chime says "look at the list" without taking any room.
    /// </remarks>
    public bool ShowNewFateNotification { get; set; }

    /// <summary>
    /// Answers the game's "return to your starting point?" prompt after the travel button cast
    /// Return.
    /// </summary>
    /// <remarks>
    /// Ships on, because it only ever completes a request the player just made: the prompt
    /// appears because they pressed the travel button, and the answer was decided by that press.
    /// It never begins anything, and outside the couple of seconds after such a cast it does
    /// nothing at all.
    /// <para>
    /// Off is a reasonable position too, which is why the switch is here. Dalamud's published
    /// restrictions name dialog boxes among the things plugins should not answer, and whether
    /// finishing your own request counts as that is a judgement rather than a fact.
    /// </para>
    /// </remarks>
    public bool ConfirmReturnPrompt { get; set; } = true;

    /// <summary>
    /// Opens the release notes once after an update that brings notes the player has not seen.
    /// Never on a first installation.
    /// </summary>
    /// <remarks>
    /// Ships on, unlike the pop-up above, and for the opposite reason: this happens once per
    /// version rather than once per FATE, and the moment after an update is exactly when someone
    /// wants to know what moved. It is also the only place that answers that question, so
    /// defaulting it off would mean the notes are only found by whoever already suspects they
    /// exist.
    /// </remarks>
    public bool ShowReleaseNotesOnUpdate { get; set; } = true;

    /// <summary>
    /// The newest version whose release notes have been shown.
    /// </summary>
    /// <remarks>
    /// Empty means nothing has been shown yet. On a first installation that is filled in
    /// straight away without showing anything, so the notes do not appear on the next start.
    /// </remarks>
    public string LastSeenReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// Types a line for the leading FATE into the chat box whenever the leader changes.
    /// </summary>
    /// <remarks>
    /// Typed, never sent. The player presses enter, which is what keeps this on the right side
    /// of the rule the plugin lives by (FH-01) and of Dalamud's own: nothing reaches the server
    /// that the player did not send themselves.
    /// <para>
    /// Only written into an empty chat box. Overwriting a half-typed message to save someone a
    /// keypress is not a trade worth making.
    /// </para>
    /// </remarks>
    public bool AnnounceNextFate { get; set; }

    /// <summary>
    /// Which channel the line is addressed to.
    /// </summary>
    /// <remarks>
    /// One value, shown both in the window and in the settings, rather than a "default" beside a
    /// "current". Two of them would be two things to keep in step, and the moment they disagreed
    /// the player would have no way to tell which one they were looking at.
    /// </remarks>
    public string AnnounceChannel { get; set; } = ChatChannels.DefaultCommand;

    /// <summary>Whether the FATE's name goes in beside the flag.</summary>
    /// <remarks>
    /// The one thing about the message worth deciding. The flag alone is terse but complete; the
    /// name says at a glance what the flag points at. The order of the two is not offered,
    /// because both orders carry exactly the same information.
    /// </remarks>
    public bool AnnounceIncludeName { get; set; } = true;

    public RankingWeights Weights { get; set; } = RankingWeights.Default;

    /// <summary>At or above this completion, a FATE is treated as not worth travelling to.</summary>
    public int NearlyDoneThresholdPercent { get; set; } = 90;

    /// <summary>A FATE with less time left than this is never recommended.</summary>
    public int MinimumSecondsRemaining { get; set; } = 45;

    /// <summary>
    /// Assumed travel speed in yalms per second, used to judge whether a FATE is still
    /// reachable before it expires. Mounted flight is the common case while farming.
    /// </summary>
    public float TravelSpeedYalmsPerSecond { get; set; } = 20f;

    /// <summary>
    /// Assumed travel speed in the exploratory zones, where there is no flying.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="TravelSpeedYalmsPerSecond"/> because the difference is large
    /// enough to change the answer. Measured in the open world at roughly twenty yalms a second
    /// on a flying mount and in the Occult Crescent at about fifteen on the ground; using the
    /// flying figure there would have the plugin promise arrivals it cannot keep.
    /// </remarks>
    public float ExploratoryTravelSpeedYalmsPerSecond { get; set; } = 15.4f;

    /// <summary>
    /// Assumed speed in an exploratory zone whose riding map has not been bought.
    /// </summary>
    /// <remarks>
    /// The same zones differ by two thirds depending on this one purchase: measured at fifteen
    /// yalms a second in the Occult Crescent with its maps bought, and nine in Bozja without.
    /// One figure for both would be wrong in one of them by more than the figure itself.
    /// <para>
    /// Which of the two applies is never asked. The game records whether the map has been bought
    /// and the plugin reads it, so putting that question to the player would be asking them for
    /// something already known, and giving them a switch that could then disagree with the truth.
    /// </para>
    /// </remarks>
    public float ExploratorySlowTravelSpeedYalmsPerSecond { get; set; } = 9.3f;

    /// <summary>
    /// Travel speed actually measured in a zone, by territory.
    /// </summary>
    /// <remarks>
    /// Two buckets, open world and exploratory, turned out to be one too few. Measured in the
    /// Occult Crescent the peak was fifteen yalms a second; in Bozja, nine. Both are exploratory,
    /// so each measurement overwrote the other and left whichever zone was measured first with
    /// an estimate that was half again too fast or a third too slow.
    /// <para>
    /// A zone is the only unit that actually holds still here. What makes Bozja slow is its own
    /// riding map going unbought, which is a fact about that zone and nothing else. Measuring
    /// per zone also means buying the map fixes the estimate by itself, with nothing to adjust.
    /// </para>
    /// <para>
    /// The two buckets stay as the starting point for a zone never travelled.
    /// </para>
    /// </remarks>
    public Dictionary<uint, float> TravelSpeedByTerritory { get; set; } = [];

    /// <summary>
    /// The speed to plan with: what was measured here, or the closest starting guess.
    /// </summary>
    /// <remarks>
    /// A measurement from this very zone beats every guess, and once one exists none of the
    /// figures below are consulted again for it. They only answer for a zone never travelled.
    /// </remarks>
    public float SpeedFor(ContentKind content, uint territoryId, bool hasMountSpeedUpgrades)
    {
        if (territoryId != 0
            && TravelSpeedByTerritory.TryGetValue(territoryId, out var measured)
            && measured > 0f)
        {
            return measured;
        }

        if (!content.IsExploratory())
        {
            return TravelSpeedYalmsPerSecond;
        }

        return hasMountSpeedUpgrades
            ? ExploratoryTravelSpeedYalmsPerSecond
            : ExploratorySlowTravelSpeedYalmsPerSecond;
    }

    /// <summary>The speed to plan with for a player, from where they are.</summary>
    public float SpeedFor(PlayerSnapshot player)
    {
        ArgumentNullException.ThrowIfNull(player);

        return SpeedFor(player.Content, player.TerritoryId, player.HasMountSpeedUpgrades);
    }

    /// <summary>
    /// Fixed cost of a teleport in seconds: the cast plus the loading screen. Used to judge
    /// whether teleporting actually beats simply travelling there.
    /// </summary>
    /// <remarks>
    /// Ten, timed rather than estimated. It stood at fifteen, which is a third too much, and a
    /// third of the fixed cost is exactly the amount that decides the close calls: at twenty
    /// yalms a second it moved the break-even point by a hundred yalms, and every route inside
    /// that band was advised the wrong way round.
    /// </remarks>
    public float TeleportOverheadSeconds { get; set; } = 10f;

    /// <summary>
    /// Fixed cost of the return spell, in seconds: its cast plus the trip back to camp.
    /// </summary>
    /// <remarks>
    /// In the exploratory zones there is no teleporting to a destination from where you stand.
    /// The way across is to return to camp and travel out from the aetheryte there, which is two
    /// waits rather than one, and the second alone made the comparison say the trip was half as
    /// expensive as it is.
    /// </remarks>
    public float ReturnOverheadSeconds { get; set; } = 20f;

    /// <summary>
    /// What a trip through the aetheryte network costs before any running, where the player is.
    /// </summary>
    public float TeleportOverheadFor(ContentKind content) => content.IsExploratory()
        ? ReturnOverheadSeconds + TeleportOverheadSeconds
        : TeleportOverheadSeconds;

    /// <summary>
    /// How much a yalm of climb counts against a yalm of level travel.
    /// </summary>
    /// <remarks>
    /// Gaining height is slower than covering flat ground, so a FATE high above or far below is
    /// further than the map makes it look. Two means a climb is treated as twice the distance.
    /// Zero restores the old behaviour of ignoring elevation entirely.
    /// <para>
    /// This only applies where elevation is known, which is the player and the FATEs. Aetheryte
    /// positions come from the game's 2D map markers and carry no height, so the leg from an
    /// aetheryte to a FATE is still judged flat. See open-points.
    /// </para>
    /// </remarks>
    public float VerticalTravelWeight { get; set; } = 2f;

    // --- Filtering -------------------------------------------------------------------

    /// <summary>FATE kinds to hide. <see cref="FateKind.Unknown"/> is never excluded by default.</summary>
    public HashSet<FateKind> ExcludedKinds { get; set; } = [];

    public ushort? MinimumLevel { get; set; }

    public ushort? MaximumLevel { get; set; }

    // --- Notifications ---------------------------------------------------------------

    public bool PlaySoundOnNewFate { get; set; } = true;

    /// <summary>When true, only FATEs that survive the filter trigger a notification.</summary>
    public bool NotifyOnlyIncludedKinds { get; set; } = true;

    /// <summary>Zero means no distance limit on notifications.</summary>
    public float MaximumNotificationDistance { get; set; }

    // --- Gemstones -------------------------------------------------------------------

    /// <summary>
    /// Keep the map flag on whichever FATE currently ranks first. Ships disabled (FH-02),
    /// because the map flag is shared with the player's own use of it.
    /// </summary>
    /// <summary>
    /// Retired. The flag is never set automatically any more.
    /// </summary>
    /// <remarks>
    /// Kept as a property so an existing configuration file still loads without complaint, and
    /// ignored everywhere. The flag is the player's own marker and there is only one per map,
    /// so moving it under them overwrote whatever they had put there. The numbered markers do
    /// the same job without taking anything over.
    /// </remarks>
    [Obsolete("The flag is only ever set by hand. Retained so old configuration files still load.")]
    public bool AutoFlagTopFate { get; set; }

    /// <summary>
    /// Draw numbered markers on the map for the top-ranked FATEs, so the running order is
    /// visible there and not only in the window.
    /// </summary>
    public bool ShowRankMarkersOnMap { get; set; } = true;

    /// <summary>
    /// Retired. Every rank that has a labelled icon gets a marker.
    /// </summary>
    /// <remarks>
    /// Kept so an existing configuration file still loads, and ignored. It was never reachable
    /// from the interface, so the only value it could ever hold was its old default of three,
    /// and that quietly outlived the reasoning behind it.
    /// </remarks>
    [Obsolete("Every rank with a labelled icon is marked; there is nothing left to choose.")]
    public int RankMarkersToShow { get; set; } = 8;

    public bool TrackGemstones { get; set; } = true;

    /// <summary>Show the shared FATE rank for the current zone. A cached snapshot, not live.</summary>
    public bool TrackSharedFateRank { get; set; } = true;

    /// <summary>Remaining capacity at or below which the player is warned.</summary>
    public int GemstoneWarningHeadroom { get; set; } = 150;

    // --- History ---------------------------------------------------------------------

    public bool TrackHistory { get; set; } = true;

    /// <summary>Sightings kept per FATE definition before the oldest are dropped.</summary>
    public int HistoryDepthPerFate { get; set; } = 10;

    /// <summary>Sightings needed before a respawn estimate is shown at all.</summary>
    public int MinimumSightingsForEstimate { get; set; } = 3;
}
