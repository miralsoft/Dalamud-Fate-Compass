namespace FateCompass.Core.Progress;

/// <summary>
/// How many FATEs each shared FATE rank costs, so a total can be worked out.
/// </summary>
/// <remarks>
/// The game reports only the current rank and the counter within it, never the thresholds of
/// the others. Without them, "how far to done" cannot be answered at all, so the known values
/// are written down here.
/// <para>
/// Two schemes exist. Zones up to Endwalker finish at rank 3; Dawntrail zones go to rank 4 with
/// smaller steps. The scheme is detected from what the game reports rather than from a zone
/// list, because a list would need updating with every expansion while the numbers speak for
/// themselves: a zone showing 20 or 40 as its target is on the newer scheme.
/// </para>
/// <para>
/// Confirmed against a live client on 2026-08-01: rank 1 at 4/6, rank 2 at 38/60, rank 3
/// complete on the older scheme; 19/20 and 6/40 on the newer.
/// </para>
/// </remarks>
public static class RankThresholds
{
    /// <summary>Zones finishing at rank 3: six, then sixty.</summary>
    private static readonly int[] Classic = [6, 60];

    /// <summary>Dawntrail zones finishing at rank 4: six, twenty, then forty.</summary>
    private static readonly int[] Modern = [6, 20, 40];

    /// <summary>
    /// Total FATEs the zone needs from nothing to complete, or null when the scheme cannot be
    /// told apart yet.
    /// </summary>
    public static int? TotalFor(SharedFateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return SchemeFor(zone)?.Sum();
    }

    /// <summary>
    /// FATEs completed across every rank so far, or null when the scheme is unknown.
    /// </summary>
    /// <remarks>
    /// Earlier ranks are counted as fully done, because reaching rank <c>n</c> means every step
    /// below it was finished.
    /// </remarks>
    public static int? CompletedFor(SharedFateZone zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var scheme = SchemeFor(zone);
        if (scheme is null)
        {
            return null;
        }

        if (zone.IsComplete)
        {
            return scheme.Sum();
        }

        // Rank n means ranks 1 to n-1 are behind us, and the counter belongs to rank n.
        var finishedRanks = Math.Clamp(zone.Rank - 1, 0, scheme.Length);
        var carried = scheme.Take(finishedRanks).Sum();

        return carried + Math.Max(zone.Completed, 0);
    }

    /// <summary>Overall progress from 0 to 1, or null when the scheme is unknown.</summary>
    public static float? FractionFor(SharedFateZone zone)
    {
        var total = TotalFor(zone);
        var done = CompletedFor(zone);

        return total is > 0 && done is not null
            ? Math.Clamp((float)done.Value / total.Value, 0f, 1f)
            : null;
    }

    /// <summary>
    /// Works out which scheme a zone is on from the target it reports.
    /// </summary>
    /// <remarks>
    /// Twenty or forty can only come from the newer scheme, sixty only from the older. A zone
    /// showing six is on rank 1, which both share, so it stays undecided until it advances,
    /// and a completed zone is read from how many ranks it took to get there.
    /// </remarks>
    private static int[]? SchemeFor(SharedFateZone zone)
    {
        if (zone.IsComplete)
        {
            return zone.Rank >= 4 ? Modern : zone.Rank == 3 ? Classic : null;
        }

        return zone.Needed switch
        {
            60 => Classic,
            20 or 40 => Modern,
            _ => null,
        };
    }
}
