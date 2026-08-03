namespace FateHelper.Core.Progress;

/// <summary>
/// Everything read from the game's FATE progress window in one go, with when it was read.
/// </summary>
public sealed record SharedFateSnapshot
{
    public required IReadOnlyList<SharedFateZone> Zones { get; init; }

    public required DateTimeOffset TakenAt { get; init; }

    /// <summary>Which of the window's tabs this came from. Each tab covers different zones.</summary>
    public required int Tab { get; init; }

    public static SharedFateSnapshot Empty { get; } = new()
    {
        Zones = [],
        TakenAt = DateTimeOffset.MinValue,
        Tab = 0,
    };

    public bool HasData => Zones.Count > 0;

    /// <summary>The zone matching a name, or null. Used to show the standing where the player is.</summary>
    public SharedFateZone? ForZone(string zoneName) =>
        string.IsNullOrEmpty(zoneName)
            ? null
            : Zones.FirstOrDefault(zone =>
                string.Equals(zone.ZoneName, zoneName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Advances the counter for one zone, used when a FATE completes there.
    /// </summary>
    public SharedFateSnapshot WithCompletedFateIn(string zoneName)
    {
        var zone = ForZone(zoneName);
        if (zone is null)
        {
            return this;
        }

        var updated = zone.WithOneMoreFate();
        if (ReferenceEquals(updated, zone))
        {
            return this;
        }

        return this with
        {
            Zones = [.. Zones.Select(entry => entry.ZoneIndex == zone.ZoneIndex ? updated : entry)],
        };
    }

    /// <summary>Merges a newer reading in, keeping zones from tabs that were not re-read.</summary>
    /// <remarks>
    /// The window shows one tab at a time, so a single reading only ever covers part of the
    /// world. Merging by zone name means opening the window and clicking through the tabs
    /// builds up a complete picture instead of each reading discarding the last.
    /// </remarks>
    public SharedFateSnapshot MergedWith(SharedFateSnapshot newer)
    {
        ArgumentNullException.ThrowIfNull(newer);

        if (!newer.HasData)
        {
            return this;
        }

        var byIndex = Zones.ToDictionary(zone => zone.ZoneIndex);
        foreach (var zone in newer.Zones)
        {
            byIndex[zone.ZoneIndex] = zone;
        }

        return new SharedFateSnapshot
        {
            Zones = [.. byIndex.Values.OrderBy(zone => zone.ZoneIndex)],
            TakenAt = newer.TakenAt,
            Tab = newer.Tab,
        };
    }
}
