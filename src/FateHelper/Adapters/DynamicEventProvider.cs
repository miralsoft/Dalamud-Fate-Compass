using System.Runtime.CompilerServices;
using FateHelper.Core.Model;
using FateHelper.Services;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace FateHelper.Adapters;

/// <summary>
/// Reads the instanced engagements of Bozja, Zadnor, and the Occult Crescent.
/// </summary>
/// <remarks>
/// These are a different system from open-world FATEs: skirmishes, critical engagements, and
/// critical encounters live in <see cref="DynamicEventContainer"/> rather than in the FATE
/// table. They map onto the same snapshot type, so everything downstream (ranking, filtering,
/// history, routes) works on them unchanged.
/// <para>
/// Eureka needs nothing here. Its notorious monsters are ordinary FATEs and already arrive
/// through <see cref="GameSnapshotProvider"/>.
/// </para>
/// <para>
/// Field layout verified against the installed client on 2026-08-01. States are
/// <c>Inactive = 0, Register = 1, Warmup = 2, Battle = 3</c>.
/// </para>
/// </remarks>
internal static unsafe class DynamicEventProvider
{
    /// <summary>
    /// Active engagements, or an empty list when the player is not in content that has any.
    /// The container simply reports nothing outside those zones, so no zone check is needed.
    /// </summary>
    internal static IReadOnlyList<FateSnapshot> Events()
    {
        var snapshots = new List<FateSnapshot>();

        try
        {
            var container = DynamicEventContainer.GetInstance();
            if (container is null)
            {
                return snapshots;
            }

            var panel = ReadPanel();

            for (var index = 0; index < container->Events.Length; index++)
            {
                ref var dynamicEvent = ref container->Events[index];

                var entry = panel.Find(dynamicEvent.DynamicEventId, dynamicEvent.Name.ToString());

                // The panel's clock is the only one that moves. The container reports the
                // engagement's full length and leaves it there: a live dump caught the tower
                // sitting at 1200 of 1200 seconds while the panel counted 1187 and falling. Read
                // from the container, every running fight would show the time it had when it
                // started, for as long as it lasted.
                var running = entry?.State == MycDynamicEventState.Underway;
                var remaining = running && entry is { TimeLeft: > 0 }
                    ? entry.Value.TimeLeft
                    : (int)Math.Min(dynamicEvent.SecondsLeft, int.MaxValue);

                // The timestamp is what separates a slot that is happening from one that is
                // merely defined. Every dormant slot in both zones carries a zero there, and
                // every live one a real time, whatever the state byte beside it says: the Occult
                // Crescent calls an announced encounter Inactive for part of its sign-up window,
                // and reading that literally left the zone reporting no FATEs at all while a
                // deadline was visibly ticking on the map.
                var announced = dynamicEvent.StartTimestamp != 0
                    || dynamicEvent.State is DynamicEventState.Register or DynamicEventState.Warmup;

                var deadline =
                    entry is { State: MycDynamicEventState.Register or MycDynamicEventState.Commence, TimeLeft: > 0 }
                        ? entry.Value.TimeLeft

                        // Working it out is allowed to fall back on the configured registration
                        // length, but only for a slot that is actually in play. That length is
                        // filled in everywhere whether or not anything is happening, so asking
                        // for it unconditionally handed a three minute window to fourteen dormant
                        // encounters at once.
                        : announced ? SignUpSecondsFrom(ref dynamicEvent) : 0;

                if (dynamicEvent.State == DynamicEventState.Inactive && !announced && deadline == 0)
                {
                    continue;
                }

                snapshots.Add(Map(ref dynamicEvent, index, deadline, remaining));
            }
        }
        catch (Exception ex)
        {
            // Outside this content the container is simply absent, which is normal, so this
            // stays a warning rather than an error and never costs the player the FATE list.
            DalamudServices.Log.Warning(ex, "DynamicEventProvider: could not read the events");
        }

        return snapshots;
    }

    /// <summary>
    /// The engagement the player is currently taking part in, or zero.
    /// </summary>
    /// <remarks>
    /// The equivalent of the FATE manager's current FATE, for content the FATE manager knows
    /// nothing about. Without it, entering and leaving a critical encounter in the Occult
    /// Crescent looked to the plugin like nothing happening at all, so neither the preparation
    /// nor the remount ever ran there.
    /// <para>
    /// The identifier is shifted the same way as in <see cref="Map"/>, so the value can be
    /// compared against the ids the snapshots carry.
    /// </para>
    /// </remarks>
    internal static uint CurrentEventId()
    {
        try
        {
            var container = DynamicEventContainer.GetInstance();
            if (container is null)
            {
                return 0;
            }

            var index = container->CurrentEventIndex;
            if (index < 0 || index >= container->Events.Length)
            {
                return 0;
            }

            ref var dynamicEvent = ref container->Events[index];
            return dynamicEvent.State == DynamicEventState.Inactive
                ? 0
                : ((uint)dynamicEvent.DynamicEventId << 8) | (uint)(index & 0xFF);
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "DynamicEventProvider: could not read the current event");
            return 0;
        }
    }

    /// <summary>
    /// True while any engagement is accepting registrations. Used to decide whether the
    /// notification deserves to be loud.
    /// </summary>
    internal static bool AnyRegistrationOpen()
    {
        foreach (var snapshot in Events())
        {
            if (snapshot.IsRegistrationOpen)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The sign-up countdown per event id, read from the panel the game drives its own
    /// "registration closes in" text from.
    /// </summary>
    /// <remarks>
    /// This is where the number actually lives. The battle area agent keeps a UI-facing copy of
    /// every engagement whose <c>TimeLeft</c> counts down to the sign-up cut-off while its state
    /// is Register, which is exactly what the player is shown on screen.
    /// <para>
    /// The engagement struct itself was the obvious place to look and was wrong: its
    /// <c>SecondsLeft</c> counts the whole engagement down, so a critical encounter with a
    /// sign-up window closing in two minutes reported nineteen minutes and read as relaxed.
    /// Its registration duration would answer the question, but that field is not public.
    /// </para>
    /// </remarks>
    private static PanelLookup ReadPanel()
    {
        var lookup = new PanelLookup();

        try
        {
            var battleArea = AgentMycBattleAreaInfo.Instance();
            if (battleArea is null || battleArea->MycDynamicEventData is null)
            {
                return lookup;
            }

            var events = battleArea->MycDynamicEventData->Array;

            for (var i = 0; i < events.Length; i++)
            {
                ref var entry = ref events[i];

                if (entry.State == MycDynamicEventState.None)
                {
                    continue;
                }

                var record = new PanelEntry(
                    entry.State, (int)Math.Clamp(entry.TimeLeft, 0, int.MaxValue));

                lookup.ById[entry.Id] = record;

                // Also by name, as a second way in. The identifiers line up today, but the two
                // views are maintained separately and a name is the one thing both must agree on.
                var name = entry.Name.ToString();
                if (!string.IsNullOrEmpty(name))
                {
                    lookup.ByName[name] = record;
                }
            }
        }
        catch (Exception ex)
        {
            // Absent outside this content, which is normal. Without it the engagements still
            // appear, they simply carry no sign-up countdown.
            DalamudServices.Log.Warning(ex, "DynamicEventProvider: could not read the sign-up panel");
        }

        return lookup;
    }

    /// <summary>One engagement as the panel sees it: which phase, and how long is left of it.</summary>
    private readonly record struct PanelEntry(MycDynamicEventState State, int TimeLeft);

    /// <summary>The panel's view of every engagement, reachable by either identifier.</summary>
    private sealed class PanelLookup
    {
        internal Dictionary<uint, PanelEntry> ById { get; } = [];

        internal Dictionary<string, PanelEntry> ByName { get; } = new(StringComparer.Ordinal);

        internal PanelEntry? Find(uint id, string name)
        {
            if (ById.TryGetValue(id, out var byId))
            {
                return byId;
            }

            return !string.IsNullOrEmpty(name) && ByName.TryGetValue(name, out var byName)
                ? byName
                : null;
        }
    }

    /// <summary>
    /// Offset of the registration duration inside <see cref="DynamicEvent"/>.
    /// </summary>
    /// <remarks>
    /// The field is there and is exactly what is wanted, but the client structs layer marks it
    /// private, so it is read by offset from a pointer that has already been checked. That is a
    /// deliberate trade: the offset is published in the same struct definition this file already
    /// depends on, it sits far inside a structure whose size is known, and the alternative was
    /// to keep guessing at the answer from fields that do not hold it.
    /// <para>
    /// If a game patch moves it, the value read is wrong rather than dangerous, and the sanity
    /// check below throws it away.
    /// </para>
    /// </remarks>
    private const int RegistrationSecondsOffset = 0x6C;

    /// <summary>Longest sign-up window treated as believable, as a guard on a bad read.</summary>
    private const uint MaximumSignUpSeconds = 30 * 60;

    /// <summary>
    /// Seconds from now until a unix timestamp, falling back when the timestamp is unusable.
    /// </summary>
    /// <remarks>
    /// The fallback matters. A timestamp of zero, or one already in the past, or one further
    /// out than any sign-up window could be, all mean the same thing here: this is not the
    /// number being looked for. Reporting the full window then is a poor answer but a safe one,
    /// where trusting a nonsense timestamp would put a wrong countdown on screen.
    /// </remarks>
    private static int SecondsUntil(long timestamp, int fallback)
    {
        if (timestamp <= 0)
        {
            return fallback;
        }

        var remaining = timestamp - DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        return remaining > 0 && remaining <= MaximumSignUpSeconds
            ? (int)remaining
            : fallback;
    }

    /// <summary>
    /// Seconds left to sign up, worked out from the engagement's own timings. Zero when the
    /// window is closed or the numbers do not make sense.
    /// </summary>
    /// <remarks>
    /// The engagement publishes its total length and how much of it is left, so the time already
    /// elapsed is the difference. Subtracting that from the registration duration gives what
    /// remains of the window, which is the number the game shows as the participation deadline.
    /// </remarks>
    private static int SignUpSecondsFrom(ref DynamicEvent dynamicEvent)
    {
        // The one thing that settles it. An engagement counting its own time down is under way,
        // and an engagement under way has no sign-up window left, whatever any other field says.
        if (dynamicEvent.SecondsLeft > 0)
        {
            return 0;
        }

        // The timestamp is the answer, and it needs nothing else. A live dump of Bozja caught an
        // engagement mid sign-up with no time left, no readable registration duration, and
        // startedAt holding a unix timestamp a minute into the future: exactly the moment the
        // fight commences, which is exactly when signing up stops being possible.
        //
        // The earlier version asked for the registration duration first and gave up when it read
        // as zero, so it never got this far. Reaching for the harder number before the easy one
        // is what kept this broken through two attempts.
        var untilStart = SecondsUntil(dynamicEvent.StartTimestamp, 0);
        if (untilStart > 0)
        {
            return untilStart;
        }

        try
        {
            var registration = *(uint*)((byte*)Unsafe.AsPointer(ref dynamicEvent) + RegistrationSecondsOffset);

            return registration > 0 && registration <= MaximumSignUpSeconds ? (int)registration : 0;
        }
        catch (Exception ex)
        {
            DalamudServices.Log.Warning(ex, "DynamicEventProvider: could not work out the sign-up window");
            return 0;
        }
    }

    /// <param name="dynamicEvent">The engagement slot to map.</param>
    /// <param name="index">Its slot number, used to keep ids apart.</param>
    /// <param name="signUpSeconds">
    /// Seconds until the sign-up window closes, or zero when this engagement is not in one.
    /// </param>
    /// <param name="remainingSeconds">
    /// Seconds the fight itself has left, taken from the panel where it is actually counting.
    /// </param>
    private static FateSnapshot Map(
        ref DynamicEvent dynamicEvent, int index, int signUpSeconds, int remainingSeconds)
    {
        var marker = dynamicEvent.MapMarker;
        var participants = (int)dynamicEvent.Participants;
        var maxParticipants = (int)dynamicEvent.MaxParticipants;

        var kind = ClassifyKind(
            dynamicEvent.DynamicEventType,
            dynamicEvent.SecondsDuration,
            ContentKindProvider.Current());

        // Whether the door is open comes down to one thing: has the fight begun. Before it does,
        // signing up is exactly what is possible; once it has, it is exactly what is not.
        //
        // The earlier attempt worked the window out from how much of the engagement's duration
        // had elapsed, and that clock only starts when the fight does. So a fight two minutes in
        // was reported as having two minutes left to sign up, at the very moment nobody could
        // join it any more. That is worse than saying nothing: it invited the player to set off
        // for something they had already missed.
        var running = remainingSeconds > 0;

        // Everything in this container is signed up for. Bozja's type one was being read as a
        // free-join skirmish and left ungated, while the game was plainly showing a sign-up
        // deadline for it. There is no ungated entry in either zone's container.
        var hasGate = kind != FateKind.SpecialObjective;

        // Open for as long as it has not started, whether or not a countdown could be read. The
        // number is nice to have; the fact that joining is still possible is not optional, and
        // suppressing the whole entry for want of a figure hid an encounter that was open.
        var registering = hasGate && !running;

        return new FateSnapshot
        {
            // The event id is stable while it runs; the index keeps entries apart if two ever
            // report the same id.
            Id = ((uint)dynamicEvent.DynamicEventId << 8) | (uint)(index & 0xFF),
            DefinitionId = dynamicEvent.DynamicEventId,
            Name = dynamicEvent.Name.ToString(),
            Level = marker.RecommendedLevel,
            Kind = kind,
            State = MapState(dynamicEvent.State),
            Position = new WorldPosition(marker.Position.X, marker.Position.Y, marker.Position.Z),
            ProgressPercent = Math.Clamp((int)dynamicEvent.Progress, 0, 100),

            // An engagement that has appeared but not begun reports no time left, which is not
            // the same as no time remaining. Treating the two alike showed "0s" and had the
            // ranking write off the very engagements that were freshest.
            HasStarted = running,
            SecondsRemaining = Math.Max(remainingSeconds, 0),
            IconId = marker.IconId,
            Source = ActivitySource.DynamicEvent,
            IsRegistrationOpen = registering,
            HasRegistrationGate = hasGate,

            SecondsUntilRegistrationCloses = signUpSeconds > 0 ? signUpSeconds : null,
            Participants = participants,
            MaxParticipants = maxParticipants > 0 ? maxParticipants : null,
        };
    }

    /// <summary>
    /// Maps the engagement lifecycle onto the shared state.
    /// </summary>
    /// <remarks>
    /// Register, Warmup, and Battle all count as running, because in each of them the activity
    /// is live and worth showing. The distinction that actually matters to the player, whether
    /// registration is still open, is carried separately on the snapshot rather than squeezed
    /// into this enum.
    /// </remarks>
    private static FateProgressState MapState(DynamicEventState state) => state switch
    {
        DynamicEventState.Register => FateProgressState.Running,
        DynamicEventState.Warmup => FateProgressState.Running,
        DynamicEventState.Battle => FateProgressState.Running,
        _ => FateProgressState.Finished,
    };

    /// <summary>
    /// Classifies by the event type byte. The exact numbering is not documented, so an
    /// unrecognised value falls to <see cref="FateKind.CriticalEngagement"/> rather than to
    /// Unknown: everything in this container is an engagement of some sort, and guessing
    /// "engagement" is closer to the truth than guessing "no idea".
    /// </summary>

    /// <param name="eventType">The event's own type byte.</param>
    /// <param name="secondsDuration">Its total length.</param>
    /// <param name="content">Which system's zone this is, which decides what the byte means.</param>
    /// <remarks>
    /// The type byte is not comparable across content. A live dump of the Occult Crescent shows
    /// every critical encounter reporting type 1, the same value Bozja uses for its skirmishes,
    /// and the plugin duly labelled them "skirmish" and treated them as freely joinable. The
    /// zone is what settles it: the Crescent has critical encounters and nothing else of that
    /// shape, so there is no byte to interpret there.
    /// </remarks>
    private static FateKind ClassifyKind(byte eventType, uint secondsDuration, ContentKind content)
    {
        // Type four is the zone's large-scale raid, confirmed in both places it exists: the
        // Forked Tower in the Occult Crescent and Castrum Lacus Litore in Bozja. Nothing else in
        // either container uses it, and unlike the length it does not need the two of them to
        // agree on a duration, which they do not: one runs an hour, the other twenty minutes.
        if (eventType == SpecialObjectiveType || secondsDuration >= SpecialObjectiveSeconds)
        {
            return FateKind.SpecialObjective;
        }

        // Everything else is the zone's own word for the same thing.
        return content == ContentKind.OccultCrescent
            ? FateKind.CriticalEncounter
            : FateKind.CriticalEngagement;
    }

    /// <summary>Event type of the zone's large-scale raid.</summary>
    private const byte SpecialObjectiveType = 4;

    /// <summary>
    /// Total duration from which an engagement is treated as a standing objective rather than
    /// a stop on a route. Three quarters of an hour sits well clear of both: a critical
    /// encounter runs for minutes, the Forked Tower for well over an hour.
    /// </summary>
    private const uint SpecialObjectiveSeconds = 45 * 60;
}
