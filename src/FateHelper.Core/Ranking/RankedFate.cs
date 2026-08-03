using FateHelper.Core.Model;

namespace FateHelper.Core.Ranking;

/// <summary>
/// A FATE with everything the UI needs to display and order it.
/// </summary>
public sealed record RankedFate
{
    public required FateSnapshot Fate { get; init; }

    /// <summary>One-based position in the recommendation order. Zero when excluded.</summary>
    public required int Rank { get; init; }

    /// <summary>Higher is better. Zero when excluded.</summary>
    public required float Score { get; init; }

    public required float DistanceYalms { get; init; }

    public required float EstimatedTravelSeconds { get; init; }

    public required FateExclusionReason ExclusionReason { get; init; }

    public bool IsRecommended => ExclusionReason == FateExclusionReason.None;

    /// <summary>
    /// True for a standing objective that is worth showing but does not belong in the running
    /// order, such as the Forked Tower. These carry no rank number.
    /// </summary>
    public bool IsSpecialObjective => Fate.Kind == FateKind.SpecialObjective;

    /// <summary>
    /// True for something that is running and worth seeing, but is not a target: either a
    /// standing objective, or an engagement whose sign-up window has already shut.
    /// </summary>
    /// <remarks>
    /// These belong on screen and out of the way at the same time. Dropping them entirely would
    /// have the plugin claim a zone is empty while a fight is plainly under way in it; leaving
    /// them in the numbered order would send the player somewhere they cannot take part.
    /// Off to the side, unnumbered, says both things at once.
    /// </remarks>
    public bool IsSidelined =>
        IsSpecialObjective || ExclusionReason == FateExclusionReason.RegistrationClosed;
}
