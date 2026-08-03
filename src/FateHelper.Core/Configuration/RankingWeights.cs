namespace FateHelper.Core.Configuration;

/// <summary>
/// How much each factor counts when ordering FATEs. Weights are relative, they do not need to
/// sum to anything.
/// </summary>
/// <remarks>
/// These are a matter of taste and will want tuning after real use, which is exactly why they
/// are configuration rather than constants in the ranker.
/// </remarks>
public sealed class RankingWeights
{
    /// <summary>Closer is better.</summary>
    public float Distance { get; set; } = 1.0f;

    /// <summary>More time left is better, because it leaves room to actually contribute.</summary>
    public float TimeRemaining { get; set; } = 0.6f;

    /// <summary>Less progress is better, because a nearly finished FATE pays little.</summary>
    public float Progress { get; set; } = 0.8f;

    /// <summary>
    /// Distance in yalms at which the distance score has fallen to half. Larger values flatten
    /// the curve and make distance matter less across a zone.
    /// </summary>
    public float DistanceHalfLifeYalms { get; set; } = 250f;

    /// <summary>
    /// How much time on arrival counts as comfortable, in seconds.
    /// </summary>
    /// <remarks>
    /// Measured against the time left <em>after</em> travelling there, not against the raw
    /// countdown. Rewarding a longer countdown outright once ranked a FATE half a zone away
    /// above a much nearer one, purely because it had eleven minutes left instead of four.
    /// Beyond enough time to take part, more time is worth nothing.
    /// </remarks>
    public float ComfortableParticipationSeconds { get; set; } = 180f;

    /// <summary>
    /// How much spare time before a registration cut-off counts as a comfortable margin, in
    /// seconds.
    /// </summary>
    /// <remarks>
    /// A sign-up window is a door, not a countdown, so the rule here is different from
    /// <see cref="ComfortableParticipationSeconds"/>: getting there before it shuts is worth
    /// full marks, and arriving with two minutes to spare is no better than arriving with one.
    /// Only a genuinely tight arrival should lose points.
    /// </remarks>
    public float ComfortableRegistrationMarginSeconds { get; set; } = 30f;

    /// <summary>
    /// How much an empty activity is preferred over a crowded one. Only applies where the game
    /// reports participation, which today means the instanced engagements of Bozja, Zadnor,
    /// and the Occult Crescent. Open-world FATEs are unaffected.
    /// </summary>
    public float Occupancy { get; set; } = 0.5f;

    /// <summary>
    /// Extra score for a critical encounter or engagement that can still be reached in time.
    /// </summary>
    /// <remarks>
    /// These are the reason to be in the zone at all, and unlike a FATE they cannot be caught on
    /// the next pass: miss the sign-up and the whole fight is gone for that cycle. So one that
    /// is reachable belongs at the front of the order, ahead of a nearer FATE that will still be
    /// there in two minutes.
    /// </remarks>
    public float CriticalPriorityBonus { get; set; } = 1.5f;

    /// <summary>
    /// Extra score for an engagement whose registration window is open. This is the one state
    /// the player can genuinely miss, so it outranks the usual factors on purpose.
    /// </summary>
    public float RegistrationOpenBonus { get; set; } = 2.0f;

    public static RankingWeights Default => new();
}
