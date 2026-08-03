namespace FateHelper.Core.Engage;

/// <summary>
/// The ordered steps to run, or the reason there are none.
/// </summary>
public sealed record EngagePlan
{
    public required IReadOnlyList<EngageStep> Steps { get; init; }

    public required PlanBlockedReason BlockedReason { get; init; }

    public bool HasWork => Steps.Count > 0;

    public static EngagePlan Empty(PlanBlockedReason reason) =>
        new() { Steps = [], BlockedReason = reason };

    public static EngagePlan Of(params EngageStep[] steps) =>
        new() { Steps = steps, BlockedReason = PlanBlockedReason.None };
}
