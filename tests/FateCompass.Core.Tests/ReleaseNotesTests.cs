using FateCompass.Core.News;

namespace FateCompass.Core.Tests;

public sealed class ReleaseNotesTests
{
    [Fact]
    public void BothShippedLanguagesHaveNotes()
    {
        var codes = EmbeddedReleaseNotes.AvailableCodes();

        Assert.Contains("en", codes);
        Assert.Contains("de", codes);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("de")]
    public void EveryLanguageParsesAndHasContent(string code)
    {
        var notes = EmbeddedReleaseNotes.Load(code);

        Assert.NotEmpty(notes.Versions);
        Assert.All(notes.Versions, version =>
        {
            Assert.False(string.IsNullOrWhiteSpace(version.Version));
            Assert.NotEmpty(version.Notes);
            Assert.All(version.Notes, note => Assert.False(string.IsNullOrWhiteSpace(note.Text)));
        });
    }

    /// <summary>
    /// The two languages describe the same releases. A version written into one file and
    /// forgotten in the other would show a German player an English-only release or, worse,
    /// nothing at all for the version they just installed.
    /// </summary>
    [Fact]
    public void EveryLanguageCoversTheSameVersions()
    {
        var english = EmbeddedReleaseNotes.Load("en").Versions.Select(version => version.Version);

        foreach (var code in EmbeddedReleaseNotes.AvailableCodes())
        {
            var other = EmbeddedReleaseNotes.Load(code).Versions.Select(version => version.Version);
            Assert.Equal(english, other);
        }
    }

    /// <summary>
    /// The newest notes must describe the version being built. This is the reminder that a
    /// version bump without notes is unfinished: the window is the only place a player can find
    /// out what changed, so shipping a version it does not mention makes it a liar.
    /// </summary>
    [Fact]
    public void NewestNotesMatchTheAssemblyVersion()
    {
        var assembly = typeof(EmbeddedReleaseNotes).Assembly.GetName().Version;
        Assert.NotNull(assembly);

        var built = new Version(assembly.Major, assembly.Minor, assembly.Build);
        var newest = VersionNumber.Parse(EmbeddedReleaseNotes.Load("en").LatestVersion);

        Assert.True(
            newest == built,
            $"The newest release notes are for {newest}, but this build is {built}. "
            + "Add a section for it in src/FateCompass.Core/News/Notes/*.json.");
    }

    [Fact]
    public void UnknownLanguageFallsBackToEnglish()
    {
        var notes = EmbeddedReleaseNotes.Load("xx");

        Assert.Equal(EmbeddedReleaseNotes.Load("en").LatestVersion, notes.LatestVersion);
    }

    [Fact]
    public void NewestVersionComesFirstWhateverOrderTheFileUses()
    {
        var notes = new ReleaseNotes(
        [
            new ReleaseVersion { Version = "0.2.0" },
            new ReleaseVersion { Version = "1.0.0" },
            new ReleaseVersion { Version = "0.10.0" },
        ]);

        Assert.Equal(["1.0.0", "0.10.0", "0.2.0"], notes.Versions.Select(version => version.Version));
        Assert.Equal("1.0.0", notes.LatestVersion);
    }

    [Fact]
    public void NotesAreGroupedNewFirstThenChangedThenFixed()
    {
        var kinds = EmbeddedReleaseNotes.Load("de").Versions[0].Notes
            .Select(note => note.Kind)
            .Distinct()
            .ToList();

        Assert.Equal([ReleaseNoteKind.Added, ReleaseNoteKind.Changed, ReleaseNoteKind.Fixed], kinds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0.0.9")]
    public void NotesCountAsUnseenForAnythingOlder(string? lastSeen)
    {
        var notes = new ReleaseNotes([new ReleaseVersion { Version = "0.1.0" }]);

        Assert.True(notes.HasUnseen(lastSeen));
    }

    [Theory]
    [InlineData("0.1.0")]
    [InlineData("0.2.0")]
    public void NotesCountAsSeenOnceTheStoredVersionCaughtUp(string lastSeen)
    {
        var notes = new ReleaseNotes([new ReleaseVersion { Version = "0.1.0" }]);

        Assert.False(notes.HasUnseen(lastSeen));
    }

    [Fact]
    public void NoNotesMeansNothingUnseen()
    {
        Assert.False(ReleaseNotes.Empty.HasUnseen(null));
        Assert.Null(ReleaseNotes.Empty.LatestVersion);
    }

    /// <summary>
    /// The resource names the loader looks for have to match what the build actually embeds.
    /// A renamed folder would leave the loader silently finding nothing, which looks exactly
    /// like a plugin that simply has no notes.
    /// </summary>
    [Fact]
    public void NotesAreEmbeddedUnderTheExpectedNames()
    {
        var resources = typeof(EmbeddedReleaseNotes).Assembly.GetManifestResourceNames();

        Assert.Contains("FateCompass.Core.News.Notes.en.json", resources);
        Assert.Contains("FateCompass.Core.News.Notes.de.json", resources);
    }
}
