using FateCompass.Adapters;
using FateCompass.Configuration;
using FateCompass.Core.Announce;
using FateCompass.Core.Engage;
using FateCompass.Core.History;
using FateCompass.Core.Localization;
using FateCompass.Core.Model;
using FateCompass.Core.Progress;
using FateCompass.Core.Ranking;
using FateCompass.Core.Routing;
using FateCompass.Services;

namespace FateCompass;

/// <summary>
/// Ties the game to the core: polls, detects changes, and runs the engage sequence.
/// </summary>
/// <remarks>
/// Holds no logic of its own worth testing. Every decision (what to rank, which steps are
/// needed, whether a sighting counts) is made in the core; this class only supplies inputs and
/// carries out the answers.
/// </remarks>
internal sealed class FateCompassController : IDisposable
{
    /// <summary>
    /// Polling interval. Per-frame work is a stated platform concern, and FATE data does not
    /// change fast enough to justify reading it every frame.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// How long to keep retrying the engage sequence after it is triggered. Long enough to
    /// cover landing and a level sync, short enough that it can never become a loop.
    /// </summary>
    private const int EngageRetryWindowSeconds = 15;

    /// <summary>
    /// How often the engage plan is re-checked inside that window. Kept short so the dismount
    /// happens the moment the player touches the ground rather than up to a second later.
    /// This costs nothing while airborne: the planner does not plan a dismount in mid-air, so
    /// a fast retry re-reads the player state without sending anything.
    /// </summary>
    private static readonly TimeSpan EngageRetryInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// How long to keep watching for a chance to remount after a FATE. Generous, because
    /// lingering combat is normal, but still bounded.
    /// </summary>
    private const int RemountWindowSeconds = 60;

    private readonly PluginConfiguration configuration;
    private readonly FateHistoryStore history;
    private readonly IGameActions actions;
    private readonly Localizer localizer;

    private readonly HashSet<uint> knownFateIds = [];
    private DateTime lastPoll = DateTime.MinValue;
    private uint lastTerritory;
    /// <summary>
    /// The activity the player was last taking part in: a FATE id, or a dynamic event id shifted
    /// the way the snapshots shift theirs. Wide enough for both, since the two share this field.
    /// </summary>
    private uint lastFateId;
    private bool announceNewFates;
    private DateTime remountDueAt = DateTime.MaxValue;
    private DateTime remountGiveUpAt = DateTime.MinValue;
    private DateTime engageRetryUntil = DateTime.MinValue;
    private DateTime nextEngageAttempt = DateTime.MinValue;
    private ActionTrigger engageTrigger = ActionTrigger.Manual;
    private WorldPosition? lastSpeedPosition;
    private DateTime lastSpeedAt = DateTime.MinValue;

    internal FateCompassController(
        PluginConfiguration configuration,
        FateHistoryStore history,
        IGameActions actions,
        Localizer localizer)
    {
        this.configuration = configuration;
        this.history = history;
        this.actions = actions;
        this.localizer = localizer;

        // Restore the cached standing so the display is populated before the player ever opens
        // the game's progress window this session.
        SharedFate = new SharedFateSnapshot
        {
            Zones = [.. configuration.SharedFateZones],
            TakenAt = configuration.SharedFateReadAt,
            Tab = 0,
        };

        DalamudServices.Framework.Update += OnUpdate;
    }

    /// <summary>The most recent ranking, read by the window.</summary>
    internal IReadOnlyList<RankedFate> Ranked { get; private set; } = [];

    /// <summary>Route hints keyed by FATE instance id.</summary>
    internal IReadOnlyDictionary<uint, RouteHint> Routes { get; private set; } =
        new Dictionary<uint, RouteHint>();

    internal PlayerSnapshot? Player { get; private set; }

    /// <summary>Observed travel speed, gathered while the plugin runs.</summary>
    internal SpeedSampler Speed { get; } = new();

    /// <summary>Last known shared FATE standing. A snapshot, not live data.</summary>
    internal SharedFateSnapshot SharedFate { get; private set; } = SharedFateSnapshot.Empty;

    /// <summary>Name of the zone the player is in, used to match against the shared FATE list.</summary>
    internal string CurrentZoneName { get; private set; } = string.Empty;

    private bool disposed;

    public void Dispose()
    {
        // Set the flag before unsubscribing. A tick already in flight then returns immediately
        // rather than walking native pointers while the plugin is being torn down.
        disposed = true;
        DalamudServices.Framework.Update -= OnUpdate;
    }

    /// <summary>
    /// Runs the engage sequence. Both the button and the command land here, and so does the
    /// automatic path, which is why the trigger is a parameter rather than an assumption.
    /// </summary>
    internal EngagePlan RunEngage(ActionTrigger trigger)
    {
        // Open a retry window. A single attempt at the moment of entering a FATE is not enough:
        // the player is often still airborne, still landing, or mid level sync, and every step
        // then quietly fails with nothing to correct it. Retrying for a bounded time fixes
        // dismount-after-landing and stance-after-sync without becoming an endless loop (FH-07).
        engageTrigger = trigger;
        engageRetryUntil = DateTime.UtcNow.AddSeconds(EngageRetryWindowSeconds);
        nextEngageAttempt = DateTime.MinValue;
        engageAttempts.Clear();

        return AttemptEngage(trigger);
    }

    /// <summary>
    /// How often one step has been sent during the current retry window.
    /// </summary>
    /// <remarks>
    /// The window exists because a step can legitimately fail once, while still airborne or
    /// mid-cast, and succeed a moment later. It must not turn into the plugin sending the same
    /// thing at the server every second for a quarter of a minute, which is what happened when a
    /// level sync had no effect and so never stopped being planned. Three tries is enough for a
    /// step that was merely early; a fourth means it is not going to work.
    /// </remarks>
    private readonly Dictionary<EngageStep, int> engageAttempts = [];

    /// <summary>Attempts per step within one retry window, after which it is abandoned.</summary>
    private const int MaximumEngageAttempts = 3;

    private EngagePlan AttemptEngage(ActionTrigger trigger)
    {
        var player = GameSnapshotProvider.Player();
        if (player is null)
        {
            return EngagePlan.Empty(PlanBlockedReason.Occupied);
        }

        Player = player;

        var fate = CurrentFateSnapshot();
        if (fate is null)
        {
            engageRetryUntil = DateTime.MinValue;
            return EngagePlan.Empty(PlanBlockedReason.AlreadyDone);
        }

        var plan = EngagePlanner.PlanEngage(player, fate, configuration.Settings);

        foreach (var step in plan.Steps)
        {
            var attempts = engageAttempts.GetValueOrDefault(step);
            if (attempts >= MaximumEngageAttempts)
            {
                continue;
            }

            engageAttempts[step] = attempts + 1;
            actions.Execute(step, trigger);

            if (attempts + 1 == MaximumEngageAttempts)
            {
                DalamudServices.Log.Information(
                    "Controller: {Step} had no effect after {Count} attempts, giving up on it",
                    step, MaximumEngageAttempts);
            }
        }

        return plan;
    }

    /// <summary>
    /// Re-runs the engage plan roughly once a second while the retry window is open, and stops
    /// as soon as there is nothing left to do.
    /// </summary>
    private void HandleEngageRetry(DateTime now)
    {
        if (now >= engageRetryUntil || now < nextEngageAttempt)
        {
            return;
        }

        nextEngageAttempt = now.Add(EngageRetryInterval);

        var plan = AttemptEngage(engageTrigger);

        if (plan.BlockedReason == PlanBlockedReason.AlreadyDone
            || plan.BlockedReason == PlanBlockedReason.Disabled)
        {
            DalamudServices.Log.Debug("Controller: engage complete, retry window closed");
            engageRetryUntil = DateTime.MinValue;
            return;
        }

        // The window is deliberately left to run its full length even when everything currently
        // planned is exhausted. Closing it early looked like an easy saving and was not: a step
        // that is not planned yet is not the same as a step that is finished. Entering a FATE
        // while still gliding down plans no dismount at all, because dismounting in mid-air
        // drops the character; if the window has already closed by the time the ground arrives,
        // the dismount never happens. The per-step cap above is what stops the repetition, and
        // it does that without deciding for the situation that nothing more can change.
    }

    private void OnUpdate(Dalamud.Plugin.Services.IFramework framework)
    {
        try
        {
            // Everything below reads native game memory. During teardown or a framework unload
            // those pointers are not safe to follow, and a hot reload with the window open is
            // exactly when that window is open.
            if (disposed || framework.IsFrameworkUnloading)
            {
                return;
            }

#if FATECOMPASS_DEVTOOLS
            // Sampled here rather than on demand: a warp lasts a second or two, and the question
            // being answered is partly whether the value survives it. Developer builds only, and
            // it acts on nothing. Ahead of the Enabled check so a switched-off plugin can still
            // be used to observe.
            Adapters.WarpProbe.Poll();
#endif

            if (!configuration.Settings.Enabled)
            {
                return;
            }

            // Started here rather than in the constructor. Creating native UI nodes is only
            // legal from the main thread, and a plugin is constructed off it, so the first
            // attempt failed outright and switched the feature off for the session (FH-08).
            NativeMapMarkers.Initialise();

            var now = DateTime.UtcNow;
            HandleEngageRetry(now);
            HandlePendingRemount(now);

            if (now - lastPoll < PollInterval)
            {
                return;
            }

            lastPoll = now;
            Poll(now);
        }
        catch (Exception ex)
        {
            // Nothing may escape into the game's frame loop (S-09).
            DalamudServices.Log.Error(ex, "Controller: update failed");
        }
    }

    private void Poll(DateTime now)
    {
        var player = GameSnapshotProvider.Player();
        SampleSpeed(player, now);
        Player = player;
        if (player is null)
        {
            Ranked = [];
            return;
        }

        var territory = DalamudServices.ClientState.TerritoryType;
        if (territory != lastTerritory)
        {
            lastTerritory = territory;
            knownFateIds.Clear();
            CurrentZoneName = GameSnapshotProvider.ZoneName(territory);

            // The first poll after arriving sees every running FATE at once. Those are not new,
            // they were simply not known yet, so announcing them would fire a burst of alerts on
            // every zone change and on every plugin load. Seed the set silently instead.
            announceNewFates = false;
        }

        // Both systems feed the same list. Open-world FATEs (including Eureka notorious
        // monsters) come from the FATE table, the instanced engagements of Bozja, Zadnor, and
        // the Occult Crescent from the dynamic event container. Everything downstream treats
        // them identically.
        ReadSharedFateProgress();

        var fates = new List<FateSnapshot>(GameSnapshotProvider.Fates());
        fates.AddRange(DynamicEventProvider.Events());

        DetectNewFates(fates, territory, now);
        DetectFateExit();

        Ranked = FateRanker.Rank(fates, player, configuration.Settings);

        // Aetheryte positions come from the game's map markers and are in map coordinates, so
        // the player and the FATEs are converted into the same space before anything is
        // measured. Mixing world and map coordinates is what made every aetheryte look absent.
        var mapId = DalamudServices.ClientState.MapId;

        // Aetheryte heights exist nowhere in the game data, so they are picked up from whichever
        // aetheryte happens to be loaded around the player. Done here, on the poll, because an
        // aetheryte does not move between frames.
        if (AetheryteElevations.Learn())
        {
            configuration.AetheryteElevations = new Dictionary<uint, float>(AetheryteElevations.Export());
            configuration.Save();
        }

        var aetherytes = AetheryteProvider.ForTerritory(territory);
        var playerOnMap = AetheryteProvider.ToMapSpace(player.Position, mapId);
        var routes = new Dictionary<uint, RouteHint>();

        if (playerOnMap is { } playerMapPosition)
        {
            var playerInMapSpace = player with { Position = playerMapPosition };

            foreach (var entry in Ranked)
            {
                if (AetheryteProvider.ToMapSpace(entry.Fate.Position, mapId) is not { } fateMapPosition)
                {
                    continue;
                }

                // The ranker already measured the direct route in world space with elevation
                // weighted in. Handing that figure over keeps the teleport verdict aware of
                // height, which the flat map space alone cannot be.
                // The FATE's own height goes over separately, because the position handed to the
                // calculator is in map space and map space has no height. Without it the second
                // leg of the route is flat, and a target on a plateau looks closest to the
                // aetheryte directly underneath it.
                var hint = RouteHintCalculator.Calculate(
                    entry.Fate with { Position = fateMapPosition },
                    playerInMapSpace,
                    aetherytes,
                    configuration.Settings,
                    AetheryteProvider.YalmsPerMapUnit,
                    entry.DistanceYalms,
                    entry.Fate.Position.Y);

                if (hint is not null)
                {
                    routes[entry.Fate.Id] = hint;
                }
            }
        }

        Routes = routes;
        DrawRankMarkers();
        AnnounceLeadingFate(territory);
    }

    /// <summary>The FATE the chat line was last written for, so it is written once per change.</summary>
    private uint announcedFateId;

    /// <summary>
    /// Writes a ready-to-send line for the leading FATE into the chat box, and puts the map flag
    /// on it so the flag placeholder in that line points somewhere.
    /// </summary>
    /// <remarks>
    /// The flag moving is the whole point here rather than a side effect, which is what makes it
    /// different from the automatic flagging that was taken out. That one shifted the player's
    /// own marker silently and for nobody's benefit; this one sets it precisely so the message
    /// about to be sent has something to point at, and only while the feature is switched on.
    /// <para>
    /// The order matters: flag first, then the text. Written the other way round the placeholder
    /// would expand to wherever the flag was before.
    /// </para>
    /// <para>
    /// Nothing is sent. The line sits in the box until the player presses enter (FH-01).
    /// </para>
    /// </remarks>
    private void AnnounceLeadingFate(uint territory)
    {
        var settings = configuration.Settings;

        if (!settings.AllowChatAnnounce || !settings.AnnounceNextFate)
        {
            announcedFateId = 0;
            return;
        }

        var top = Ranked.FirstOrDefault(entry => entry.IsRecommended && !entry.IsSpecialObjective);
        if (top is null)
        {
            announcedFateId = 0;
            return;
        }

        if (top.Fate.Id == announcedFateId)
        {
            return;
        }

        // Only into an empty box. Checked before the flag is touched, so a player in the middle
        // of typing has neither their message nor their marker disturbed.
        if (!ChatInput.IsEmpty())
        {
            return;
        }

        var line = ChatAnnouncement.Build(
            settings.AnnounceChannel, top.Fate.Name, settings.AnnounceIncludeName);

        if (string.IsNullOrEmpty(line))
        {
            return;
        }

        if (!MapService.SetFlag(territory, DalamudServices.ClientState.MapId, top.Fate.Position))
        {
            return;
        }

        if (ChatInput.Append(line))
        {
            announcedFateId = top.Fate.Id;
            DalamudServices.Log.Debug("Controller: announced {Name} in the chat box", top.Fate.Name);
        }
    }

    /// <summary>What was last handed to the map, so identical sets are not re-sent.</summary>
    private string lastMarkerSignature = string.Empty;

    /// <summary>
    /// Puts the running order onto the map and the minimap.
    /// </summary>
    /// <remarks>
    /// Only when the set actually changes, and this matters more than it looks. The native
    /// overlay queues additions and removals and works through them on its own schedule; clearing
    /// and refilling it twice a second meant a marker could be removed again before it had ever
    /// been drawn. What survived that race was arbitrary, which is why a rank went missing and
    /// why the numbers stopped agreeing with the list once FATEs came and went.
    /// <para>
    /// The game's own markers are the other way round: it wipes them whenever it rebuilds the
    /// map, so those do have to be re-sent each cycle. Hence the two paths.
    /// </para>
    /// </remarks>
    private void DrawRankMarkers()
    {
        if (!configuration.Settings.ShowRankMarkersOnMap)
        {
            return;
        }

        // As many as there are labelled icons for, and no setting in between. The count used to
        // be configurable and defaulted to three, which meant a saved configuration went on
        // numbering only the first three long after the default had been raised, with nothing in
        // the interface to change it. There is no taste involved here anyway: a marker past the
        // labelled icons could only be a blank pin, which marks a spot without answering the one
        // question these markers exist for.
        var entries = Ranked
            .Where(entry => entry.IsRecommended && entry.Rank >= 1)
            .Take(MapService.MaximumRankMarkers)
            .Select(entry => (entry.Rank, entry.Fate.Position, entry.Fate.Name))
            .ToList();

        // Which FATE holds which rank. Position is left out on purpose: a FATE does not move, so
        // including it would only add noise from floating point.
        var signature = string.Join(
            '|', Ranked.Where(entry => entry.Rank >= 1).Take(MapService.MaximumRankMarkers)
                .Select(entry => $"{entry.Rank}:{entry.Fate.Id}"));

        // The native overlay holds its markers until told otherwise, so an unchanged set needs
        // nothing done to it. Without the overlay the game wipes them on every map rebuild, so
        // there the set has to be re-sent regardless.
        if (NativeMapMarkers.IsAvailable && signature == lastMarkerSignature)
        {
            return;
        }

        lastMarkerSignature = signature;

        if (entries.Count == 0)
        {
            MapService.ClearRankMarkers();
            return;
        }

        MapService.DrawRankMarkers(entries);
    }

    /// <summary>
    /// Puts the running order on the map itself, so the next target is visible without opening
    /// the window.
    /// </summary>
    /// <summary>
    /// Measures how fast the player actually travels, so the estimate the ranking uses can be
    /// replaced by an observation.
    /// </summary>
    private void SampleSpeed(PlayerSnapshot? player, DateTime now)
    {
        if (player is null || lastSpeedPosition is null)
        {
            lastSpeedPosition = player?.Position;
            lastSpeedAt = now;
            return;
        }

        var seconds = (float)(now - lastSpeedAt).TotalSeconds;
        var distance = lastSpeedPosition.Value.HorizontalDistanceTo(player.Position);
        Speed.Add(distance, seconds);

        lastSpeedPosition = player.Position;
        lastSpeedAt = now;
    }

    /// <summary>
    /// Picks up the shared FATE standing whenever the game's progress window happens to be
    /// open, and remembers it.
    /// </summary>
    /// <remarks>
    /// The player never has to ask for this. Opening the window at any point updates the cache,
    /// and clicking through its tabs fills in the rest of the world, because readings are merged
    /// by zone rather than replacing each other.
    /// </remarks>
    private void ReadSharedFateProgress()
    {
        var reading = SharedFateProvider.Read();
        if (!reading.HasData)
        {
            return;
        }

        var merged = SharedFate.MergedWith(reading);
        if (merged.Zones.Count == SharedFate.Zones.Count && merged.TakenAt == SharedFate.TakenAt)
        {
            return;
        }

        SharedFate = merged;
        configuration.SharedFateZones = [.. merged.Zones];
        configuration.SharedFateReadAt = merged.TakenAt;
        configuration.Save();
    }

    /// <summary>
    /// Advances the local tally for the current zone by one.
    /// </summary>
    /// <remarks>
    /// A local count, not a reading. It can drift, for example when a FATE is left before
    /// contributing enough to earn credit, and the next time the game's window is opened the
    /// real figure replaces it. Between readings, a number that moves is far more useful than
    /// one frozen since the last time the player happened to look.
    /// </remarks>
    private void CountCompletedFate()
    {
        if (!configuration.Settings.TrackSharedFateRank || string.IsNullOrEmpty(CurrentZoneName))
        {
            return;
        }

        var updated = SharedFate.WithCompletedFateIn(CurrentZoneName);
        if (ReferenceEquals(updated, SharedFate))
        {
            return;
        }

        SharedFate = updated;
        configuration.SharedFateZones = [.. updated.Zones];
        configuration.Save();
    }

    private void DetectNewFates(IReadOnlyList<FateSnapshot> fates, uint territory, DateTime now)
    {
        var seen = new HashSet<uint>();

        foreach (var fate in fates)
        {
            seen.Add(fate.Id);

            if (!knownFateIds.Add(fate.Id))
            {
                continue;
            }

            history.Record(fate.DefinitionId, (ushort)territory, new DateTimeOffset(now, TimeSpan.Zero));

            if (announceNewFates && ShouldNotify(fate))
            {
                // An open registration window gets its own wording, because missing it means
                // missing the engagement entirely rather than just arriving late.
                var message = fate.IsRegistrationOpen
                    ? localizer.Format(StringKeys.NotificationRegistrationOpen, fate.Name)
                    : localizer.Format(StringKeys.NotificationNewFate, fate.Name);

                NotificationService.Announce(
                    localizer.Get(StringKeys.WindowMainTitle),
                    message,
                    configuration.Settings.PlaySoundOnNewFate,
                    configuration.Settings.ShowNewFateNotification);
            }
        }

        knownFateIds.IntersectWith(seen);

        // From the second poll onward, anything not already known really is new.
        announceNewFates = true;
    }

    private bool ShouldNotify(FateSnapshot fate)
    {
        var settings = configuration.Settings;

        if (settings.NotifyOnlyIncludedKinds && !FateFilter.IsIncluded(fate, settings))
        {
            return false;
        }

        if (settings.MaximumNotificationDistance > 0f && Player is { } player)
        {
            var distance = player.Position.HorizontalDistanceTo(fate.Position);
            if (distance > settings.MaximumNotificationDistance)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Watches for entering and leaving a FATE. Entering optionally triggers the engage
    /// sequence, leaving schedules the remount.
    /// </summary>
    private void DetectFateExit()
    {
        // Falls back to the dynamic event the player is taking part in. The FATE manager knows
        // nothing about the Occult Crescent or Bozja, so relying on it alone meant that entering
        // and leaving a critical encounter went unnoticed and the remount never fired there.
        uint currentFateId = GameActions.CurrentFateId();
        if (currentFateId == 0)
        {
            currentFateId = DynamicEventProvider.CurrentEventId();
        }

        if (currentFateId == lastFateId)
        {
            return;
        }

        var entered = lastFateId == 0 && currentFateId != 0;
        var left = lastFateId != 0 && currentFateId == 0;
        lastFateId = currentFateId;

        if (entered && configuration.Settings.AutoEngageOnFateEnter)
        {
            RunEngage(ActionTrigger.Automatic);
        }

        // Leaving a FATE the player took part in counts towards the zone's shared FATE tally.
        // Without this the display sits stale until the game's progress window is opened, which
        // is exactly the complaint: you finish a FATE and nothing moves.
        if (left)
        {
            CountCompletedFate();
        }

        if (left && configuration.Settings.AutoRemountAfterFate)
        {
            remountDueAt = DateTime.UtcNow.AddSeconds(
                Math.Max(configuration.Settings.RemountDelaySeconds, 0));

            // Combat can drag on well past the FATE, so keep watching for a chance rather than
            // giving up after one look. Bounded, so it can never become a loop (FH-07).
            remountGiveUpAt = DateTime.UtcNow.AddSeconds(RemountWindowSeconds);
        }
    }

    /// <summary>
    /// Attempts the scheduled remount. Mounting fails in combat and its cast can be
    /// interrupted, so a blocked attempt is dropped rather than retried in a loop (FH-07).
    /// </summary>
    private void HandlePendingRemount(DateTime now)
    {
        if (now < remountDueAt || now > remountGiveUpAt)
        {
            if (now > remountGiveUpAt && remountDueAt != DateTime.MaxValue)
            {
                DalamudServices.Log.Debug("Controller: remount window expired without a chance to mount");
                remountDueAt = DateTime.MaxValue;
            }

            return;
        }

        var player = GameSnapshotProvider.Player();
        if (player is null)
        {
            return;
        }

        var plan = EngagePlanner.PlanRemount(player, configuration.Settings);

        // Combat often runs on well past the end of a FATE, so a single attempt at a fixed
        // delay usually lands while it is still impossible. Keep watching instead, and mount
        // the moment it becomes possible.
        if (!plan.HasWork)
        {
            if (plan.BlockedReason is PlanBlockedReason.InCombat or PlanBlockedReason.Occupied)
            {
                return;
            }

            DalamudServices.Log.Debug("Controller: remount finished or skipped, {Reason}", plan.BlockedReason);
            remountDueAt = DateTime.MaxValue;
            return;
        }

        foreach (var step in plan.Steps)
        {
            actions.Execute(step, ActionTrigger.Automatic);
        }
    }

    private FateSnapshot? CurrentFateSnapshot()
    {
        var currentId = GameActions.CurrentFateId();
        if (currentId == 0)
        {
            return null;
        }

        return Ranked.Select(entry => entry.Fate).FirstOrDefault(fate => fate.Id == currentId)
            ?? GameSnapshotProvider.Fates().FirstOrDefault(fate => fate.Id == currentId);
    }
}
