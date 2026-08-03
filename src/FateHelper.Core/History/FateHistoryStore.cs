using FateHelper.Core.Configuration;

namespace FateHelper.Core.History;

/// <summary>
/// Records when each FATE was last seen and derives a rough respawn expectation.
/// </summary>
/// <remarks>
/// Local data only, kept alongside the settings. Nothing here is sent anywhere (FH-03).
/// <para>
/// Time is always passed in rather than read from the clock, so the behaviour is deterministic
/// and can be tested without waiting.
/// </para>
/// </remarks>
public sealed class FateHistoryStore
{
    private readonly Dictionary<uint, List<FateSighting>> sightingsByDefinition = [];
    private readonly FateHelperSettings settings;

    public FateHistoryStore(FateHelperSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    /// <summary>
    /// Records an appearance. Repeated calls for the same still-running FATE are ignored, so a
    /// polling caller does not need to track what it has already reported.
    /// </summary>
    public void Record(uint definitionId, ushort territoryId, DateTimeOffset seenAt)
    {
        if (!settings.TrackHistory)
        {
            return;
        }

        if (!sightingsByDefinition.TryGetValue(definitionId, out var sightings))
        {
            sightings = [];
            sightingsByDefinition[definitionId] = sightings;
        }

        // A FATE that is already the most recent sighting is the same appearance, not a new one.
        if (sightings.Count > 0 && sightings[^1].SeenAt == seenAt)
        {
            return;
        }

        sightings.Add(new FateSighting
        {
            DefinitionId = definitionId,
            TerritoryId = territoryId,
            SeenAt = seenAt,
        });

        var depth = Math.Max(settings.HistoryDepthPerFate, 1);
        if (sightings.Count > depth)
        {
            sightings.RemoveRange(0, sightings.Count - depth);
        }
    }

    /// <summary>All recorded sightings for a FATE, oldest first.</summary>
    public IReadOnlyList<FateSighting> SightingsFor(uint definitionId) =>
        sightingsByDefinition.TryGetValue(definitionId, out var sightings)
            ? sightings
            : [];

    /// <summary>
    /// The average gap between observed appearances, or null while there is not enough data.
    /// Withholding a weak estimate is deliberate: a number based on one gap would look like
    /// knowledge and would not be.
    /// </summary>
    public RespawnEstimate? EstimateFor(uint definitionId)
    {
        if (!sightingsByDefinition.TryGetValue(definitionId, out var sightings))
        {
            return null;
        }

        var required = Math.Max(settings.MinimumSightingsForEstimate, 2);
        if (sightings.Count < required)
        {
            return null;
        }

        var totalTicks = 0L;
        for (var i = 1; i < sightings.Count; i++)
        {
            totalTicks += (sightings[i].SeenAt - sightings[i - 1].SeenAt).Ticks;
        }

        var intervals = sightings.Count - 1;
        return new RespawnEstimate
        {
            DefinitionId = definitionId,
            AverageInterval = TimeSpan.FromTicks(totalTicks / intervals),
            LastSeenAt = sightings[^1].SeenAt,
            SampleCount = intervals,
        };
    }

    /// <summary>Replaces the contents, used when loading persisted history.</summary>
    public void Load(IEnumerable<FateSighting> persisted)
    {
        ArgumentNullException.ThrowIfNull(persisted);

        sightingsByDefinition.Clear();
        foreach (var sighting in persisted.OrderBy(entry => entry.SeenAt))
        {
            if (!sightingsByDefinition.TryGetValue(sighting.DefinitionId, out var sightings))
            {
                sightings = [];
                sightingsByDefinition[sighting.DefinitionId] = sightings;
            }

            sightings.Add(sighting);
        }
    }

    /// <summary>Flattens the store for persistence.</summary>
    public IReadOnlyList<FateSighting> Export() =>
        [.. sightingsByDefinition.Values.SelectMany(sightings => sightings).OrderBy(entry => entry.SeenAt)];
}
