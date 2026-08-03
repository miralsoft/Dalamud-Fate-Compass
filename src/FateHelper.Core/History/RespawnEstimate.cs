namespace FateHelper.Core.History;

/// <summary>
/// A rough expectation of when a FATE will come back.
/// </summary>
/// <remarks>
/// This is an average of observed gaps, not a prediction the game supports. It is only
/// produced once enough sightings exist, because two data points would present a guess as a
/// fact.
/// </remarks>
public sealed record RespawnEstimate
{
    public required uint DefinitionId { get; init; }

    public required TimeSpan AverageInterval { get; init; }

    public required DateTimeOffset LastSeenAt { get; init; }

    /// <summary>How many intervals the average is based on. Higher means more trustworthy.</summary>
    public required int SampleCount { get; init; }

    public DateTimeOffset ExpectedNextAt => LastSeenAt + AverageInterval;

    /// <summary>Time until the expectation, or zero once it has passed.</summary>
    public TimeSpan TimeUntilExpected(DateTimeOffset now)
    {
        var remaining = ExpectedNextAt - now;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}
