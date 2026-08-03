namespace FateCompass.Core.News;

/// <summary>
/// The release notes for one language, newest version first.
/// </summary>
public sealed class ReleaseNotes
{
    /// <summary>Stands in when no notes file could be read at all.</summary>
    public static ReleaseNotes Empty { get; } = new([]);

    public ReleaseNotes(IEnumerable<ReleaseVersion> versions)
    {
        ArgumentNullException.ThrowIfNull(versions);

        // Sorted here rather than trusted from the file. The notes are hand-written, and a new
        // version appended to the bottom instead of the top is the obvious way to get that
        // wrong; it would then be shown collapsed underneath older ones.
        Versions =
        [
            .. versions
                .OrderByDescending(version => VersionNumber.Parse(version.Version))
                .ThenByDescending(version => version.Version, StringComparer.Ordinal),
        ];
    }

    /// <summary>Every version that has notes, newest first.</summary>
    public IReadOnlyList<ReleaseVersion> Versions { get; }

    /// <summary>The newest version that has notes, or null when there are none.</summary>
    public string? LatestVersion => Versions.Count == 0 ? null : Versions[0].Version;

    /// <summary>
    /// True when the newest notes are newer than what the player has already been shown.
    /// </summary>
    /// <remarks>
    /// Having seen nothing counts as unseen, which is what makes an update from a build that
    /// predates this window behave correctly. A first installation looks the same from here and
    /// is told apart by the caller, which knows whether a configuration file existed.
    /// </remarks>
    public bool HasUnseen(string? lastSeenVersion) =>
        LatestVersion is not null && VersionNumber.IsNewer(LatestVersion, lastSeenVersion);
}
