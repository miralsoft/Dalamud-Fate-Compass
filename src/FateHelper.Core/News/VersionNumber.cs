namespace FateHelper.Core.News;

/// <summary>
/// Compares version strings without ever throwing on one.
/// </summary>
/// <remarks>
/// Version strings arrive from a data file and from a saved setting, so both can be absent,
/// empty, or nonsense. Every one of those has to mean something rather than take the window
/// down (S-09), so anything unreadable sorts as the oldest possible version. That is the
/// harmless direction: an unreadable stored value makes the newest notes count as unseen,
/// which shows the player their notes once too often rather than never.
/// </remarks>
public static class VersionNumber
{
    private static readonly Version Oldest = new(0, 0, 0);

    /// <summary>
    /// Reads "1.2.3" into a comparable value. Any pre-release or build suffix is ignored, so
    /// "0.2.0-beta.1" and "0.2.0" compare as the same version.
    /// </summary>
    public static Version Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Oldest;
        }

        var core = text.Trim();

        var suffix = core.IndexOfAny(['-', '+', ' ']);
        if (suffix > 0)
        {
            core = core[..suffix];
        }

        if (!Version.TryParse(core, out var parsed))
        {
            return Oldest;
        }

        // Version leaves the parts that were not written at -1, which would sort below a
        // genuine zero and make "0.2" look older than "0.2.0".
        return new Version(parsed.Major, Math.Max(parsed.Minor, 0), Math.Max(parsed.Build, 0));
    }

    /// <summary>True when <paramref name="candidate"/> is a later version than <paramref name="than"/>.</summary>
    public static bool IsNewer(string? candidate, string? than) => Parse(candidate) > Parse(than);
}
