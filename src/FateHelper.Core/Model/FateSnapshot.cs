namespace FateHelper.Core.Model;

/// <summary>
/// One FATE at one moment in time. This is the only FATE shape the core and the UI see.
/// </summary>
/// <remarks>
/// Deliberately a plain value type with no behaviour beyond the two derived helpers below.
/// The adapter layer maps the game's FATE data onto this, so nothing downstream needs to know
/// how the game represents a FATE.
/// </remarks>
public sealed record FateSnapshot
{
    /// <summary>Instance id of the running FATE. Unique while it is active.</summary>
    public required uint Id { get; init; }

    /// <summary>Content id of the FATE definition. Stable across respawns of the same FATE.</summary>
    public required uint DefinitionId { get; init; }

    public required string Name { get; init; }

    /// <summary>The FATE's own level, as shown on the map.</summary>
    public required ushort Level { get; init; }

    /// <summary>
    /// The level the player is synced down to on entering, which is not the same as
    /// <see cref="Level"/> and is usually a little higher.
    /// </summary>
    public ushort MaxLevel { get; init; }

    /// <summary>The level actually synced to, falling back to the FATE level when unknown.</summary>
    public ushort SyncLevel => MaxLevel > 0 ? MaxLevel : Level;

    public required FateKind Kind { get; init; }

    public required FateProgressState State { get; init; }

    public required WorldPosition Position { get; init; }

    /// <summary>Completion percentage, 0 to 100.</summary>
    public required int ProgressPercent { get; init; }

    /// <summary>Seconds left before the FATE expires. Meaningless while <see cref="HasStarted"/> is false.</summary>
    public required int SecondsRemaining { get; init; }

    /// <summary>
    /// False for a FATE that has appeared but whose clock has not begun, which is the normal
    /// state of one waiting to be started by a player.
    /// </summary>
    /// <remarks>
    /// This distinction matters more than it looks. A FATE that has not started has no
    /// remaining time yet, not zero remaining time. Treating the two the same made the plugin
    /// call the freshest FATEs expired and every route to them too late, which is the exact
    /// opposite of the truth: they have their full duration ahead of them.
    /// </remarks>
    public bool HasStarted { get; init; } = true;

    /// <summary>Map icon id, for rendering. Zero when the game supplied none.</summary>
    public uint IconId { get; init; }

    /// <summary>Which of the game's systems this came from.</summary>
    public ActivitySource Source { get; init; } = ActivitySource.Fate;

    /// <summary>
    /// True while the registration window of a critical engagement is open. This is the moment
    /// the player has to act on, and it can be missed, so it is tracked separately from the
    /// running state rather than inferred from it.
    /// </summary>
    public bool IsRegistrationOpen { get; init; }

    /// <summary>
    /// True when this activity can only be joined during a registration window, after which the
    /// door closes for good.
    /// </summary>
    /// <remarks>
    /// The critical encounters of the Occult Crescent and the critical engagements of Bozja and
    /// Zadnor work this way: roughly three minutes to sign up, then the fight begins and nobody
    /// else gets in. An open-world FATE has no such door, and a skirmish can be joined at any
    /// point, so the distinction is carried per activity rather than assumed from the kind.
    /// </remarks>
    public bool HasRegistrationGate { get; init; }

    /// <summary>
    /// Seconds left to sign up, while the window is open. Null when the activity has no
    /// registration gate or the window has already closed.
    /// </summary>
    /// <remarks>
    /// This is the deadline that actually governs whether travelling is worth it. The encounter's
    /// own remaining time keeps running for several more minutes after the door has shut, so
    /// judging the journey by that number sends the player to a fight they can only watch.
    /// </remarks>
    public int? SecondsUntilRegistrationCloses { get; init; }

    /// <summary>
    /// The deadline to be measured against when deciding whether to set off: the registration
    /// cut-off where there is one, otherwise the activity's own expiry.
    /// </summary>
    public int? JoinDeadlineSeconds => IsRegistrationOpen && SecondsUntilRegistrationCloses is { } seconds
        ? seconds
        : HasStarted ? SecondsRemaining : null;

    /// <summary>Players currently taking part. Null when the system does not report it.</summary>
    public int? Participants { get; init; }

    /// <summary>Participant cap. Null when the system does not report it.</summary>
    public int? MaxParticipants { get; init; }

    /// <summary>
    /// How full the activity is, from 0 to 1, or null when participation is not reported.
    /// An engagement nobody has joined behaves very differently from a full one, which is why
    /// the ranking takes it into account.
    /// </summary>
    public float? Occupancy => Participants is { } current && MaxParticipants is { } max && max > 0
        ? Math.Clamp((float)current / max, 0f, 1f)
        : null;

    /// <summary>
    /// True when the FATE is worth travelling to.
    /// </summary>
    /// <remarks>
    /// Preparation counts. A FATE sitting there waiting to be started is not something to skip,
    /// it is often the best target on the map: it has its full duration ahead of it and nobody
    /// has taken a share of it yet. Excluding it as "not joinable" ranked a FATE half a zone
    /// away above one that was closer and untouched.
    /// </remarks>
    /// <remarks>
    /// <para>
    /// An activity behind a registration gate is only joinable while that gate is open. Once it
    /// shuts, the fight is still running and still reports time left, but no one else can enter,
    /// so continuing to recommend it would send the player somewhere they cannot take part.
    /// </para>
    /// </remarks>
    public bool IsJoinable =>
        (!HasRegistrationGate || IsRegistrationOpen)
        && State is FateProgressState.Running or FateProgressState.Preparation;

    /// <summary>
    /// True when the FATE is so far along that arriving is unlikely to earn a meaningful
    /// contribution. The threshold is a ranking concern, so it is passed in rather than fixed.
    /// </summary>
    public bool IsNearlyDone(int thresholdPercent) => ProgressPercent >= thresholdPercent;
}
