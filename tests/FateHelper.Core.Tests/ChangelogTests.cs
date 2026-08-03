using FateHelper.Core.News;

namespace FateHelper.Core.Tests;

/// <summary>
/// Binds <c>CHANGELOG.md</c> to the version actually being built.
/// </summary>
/// <remarks>
/// Every other link in the chain was already checked by something: the release notes against the
/// assembly version by <see cref="ReleaseNotesTests"/>, the two languages against each other,
/// and the git tag against the built manifest by the release workflow. The changelog was the one
/// document nothing looked at, so a version could ship without ever appearing in it.
/// <para>
/// Deliberately not generated from the release notes. The two describe the same releases for
/// different readers, in different words: this file says "test scaffold" and "dependency
/// pinning", the notes say "map flag" and "sign-up deadline". Generating one from the other would
/// give one of the two audiences the wrong text. Checking that they agree on which versions exist
/// gets the safety without the merge.
/// </para>
/// </remarks>
public sealed class ChangelogTests
{
    /// <summary>The heading for changes not yet released. It carries no version and is skipped.</summary>
    private const string UnreleasedHeading = "Unreleased";

    [Fact]
    public void NewestSectionIsTheVersionBeingBuilt()
    {
        var assembly = typeof(EmbeddedReleaseNotes).Assembly.GetName().Version;
        Assert.NotNull(assembly);

        var built = new Version(assembly.Major, assembly.Minor, assembly.Build);
        var versions = ReleasedVersions();

        Assert.True(
            versions.Count > 0,
            "CHANGELOG.md lists no released version at all. Every heading is still '## [Unreleased]'.");

        Assert.True(
            VersionNumber.Parse(versions[0]) == built,
            $"CHANGELOG.md's newest release is {versions[0]}, but this build is {built}. "
            + "Turn the Unreleased section into one for this version.");
    }

    /// <summary>
    /// Both documents have to know about the same releases. A version described in game but
    /// missing here means the repository forgot a release; the other way round means the players
    /// were never told about one.
    /// </summary>
    [Fact]
    public void EveryVersionWithReleaseNotesHasASection()
    {
        var documented = ReleasedVersions()
            .Select(VersionNumber.Parse)
            .ToHashSet();

        var missing = EmbeddedReleaseNotes.Load("en").Versions
            .Select(version => version.Version)
            .Where(version => !documented.Contains(VersionNumber.Parse(version)))
            .ToList();

        Assert.True(
            missing.Count == 0,
            $"CHANGELOG.md has no section for {string.Join(", ", missing)}, "
            + "although the release notes describe that version to players.");
    }

    /// <summary>
    /// Every released version, newest first, read from the <c>## [1.2.3]</c> headings.
    /// </summary>
    private static List<string> ReleasedVersions()
    {
        var versions = new List<string>();

        foreach (var line in ReadChangelog().Split('\n'))
        {
            var trimmed = line.TrimStart();

            if (!trimmed.StartsWith("## [", StringComparison.Ordinal))
            {
                continue;
            }

            var close = trimmed.IndexOf(']', 4);
            if (close < 0)
            {
                continue;
            }

            var version = trimmed[4..close];

            if (!version.Equals(UnreleasedHeading, StringComparison.OrdinalIgnoreCase))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    /// <summary>
    /// Finds the changelog by walking up from the test assembly.
    /// </summary>
    /// <remarks>
    /// A missing file fails rather than skips. The only way it can be absent is that the
    /// repository was rearranged, and a check that quietly stops checking is worse than no check.
    /// </remarks>
    private static string ReadChangelog()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "CHANGELOG.md");

            if (File.Exists(candidate))
            {
                return File.ReadAllText(candidate);
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            $"No CHANGELOG.md found above '{AppContext.BaseDirectory}'.");
    }
}
