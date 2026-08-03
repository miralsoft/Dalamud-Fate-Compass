namespace FateCompass.Core.Progress;

/// <summary>
/// Shared FATE standing in one zone, as the game's own progress window reports it.
/// </summary>
/// <remarks>
/// This is a snapshot, not live data. The values exist only inside that window, so they are
/// read when it happens to be open and cached with the time they were taken. Anything showing
/// them has to say when they are from, because presenting a possibly stale number as current
/// would be worse than showing nothing.
/// </remarks>
public sealed record SharedFateZone
{
    public required string ZoneName { get; init; }

    /// <summary>
    /// The game's own ordering index for the zone, stable across tabs and languages.
    /// </summary>
    /// <remarks>
    /// Used as the merge key rather than the name. The window shows one tab at a time, so
    /// readings have to be stitched together, and an index cannot be thrown off by a localised
    /// or renamed zone the way a string can.
    /// </remarks>
    public required int ZoneIndex { get; init; }

    /// <summary>Rank in this zone. Zero when the game reported no data for it.</summary>
    public required int Rank { get; init; }

    /// <summary>
    /// The rank exactly as the game writes it, for example "Rang 2".
    /// </summary>
    /// <remarks>
    /// Displayed in preference to deriving text from <see cref="Rank"/>. The number and the
    /// label do not always line up the way one would guess, and reproducing the game's own
    /// wording avoids inventing a scheme that disagrees with what the player sees in game.
    /// </remarks>
    public string RankText { get; init; } = string.Empty;

    /// <summary>FATEs completed towards the next rank.</summary>
    public required int Completed { get; init; }

    /// <summary>FATEs needed for the next rank. Zero when there is nothing left to reach.</summary>
    public required int Needed { get; init; }

    /// <summary>True when the zone is finished and nothing further can be earned there.</summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// False for a zone the game returned nothing for, which is not the same as a zone at
    /// zero progress and must not be displayed as one.
    /// </summary>
    public bool HasData => Rank > 0 || IsComplete || !string.IsNullOrEmpty(ZoneName);

    /// <summary>How much of the way to the next rank is done, or null when that has no meaning.</summary>
    public float? Fraction => Needed > 0 ? Math.Clamp((float)Completed / Needed, 0f, 1f) : null;

    /// <summary>Remaining FATEs for the next rank. Zero when complete or unknown.</summary>
    public int Remaining => IsComplete || Needed <= 0 ? 0 : Math.Max(Needed - Completed, 0);

    /// <summary>Overall progress across every rank, or null when the rank scheme is unknown.</summary>
    public float? TotalFraction => RankThresholds.FractionFor(this);

    /// <summary>FATEs done across every rank, or null when the scheme is unknown.</summary>
    public int? TotalCompleted => RankThresholds.CompletedFor(this);

    /// <summary>FATEs needed from nothing to complete, or null when the scheme is unknown.</summary>
    public int? TotalNeeded => RankThresholds.TotalFor(this);

    /// <summary>
    /// Advances the counter by one, used when a FATE completes and the window is not open.
    /// </summary>
    /// <remarks>
    /// The values can only be read from the game's own progress window, so without this the
    /// display would sit stale until the player happened to open it. Counting locally keeps it
    /// honest between readings, and the next real reading corrects any drift.
    /// <para>
    /// Stops at the rank's target rather than rolling over: the thresholds for the next rank
    /// are known, but whether the game grants the rank on the same tick is not, so guessing a
    /// rank up could show a rank the player has not been given.
    /// </para>
    /// </remarks>
    public SharedFateZone WithOneMoreFate()
    {
        if (IsComplete || Needed <= 0 || Completed >= Needed)
        {
            return this;
        }

        return this with { Completed = Completed + 1 };
    }
}
