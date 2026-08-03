using System.Reflection;
using System.Text.Json;
using FateCompass.Core.Localization;

namespace FateCompass.Core.News;

/// <summary>
/// Loads the release notes that ship inside this assembly.
/// </summary>
/// <remarks>
/// Same arrangement as the language catalogues: one JSON file per language under
/// <c>News/Notes</c>, named after the language code, embedded into the assembly. Writing the
/// notes for a version therefore means editing data, not code.
/// <para>
/// These are deliberately not the repository's <c>CHANGELOG.md</c>. That file is written for
/// whoever works on the plugin and says things like which project the test scaffold lives in;
/// this one is written for whoever plays with it. They cover the same releases and say
/// different things about them, which is why one is not generated from the other.
/// </para>
/// </remarks>
public static class EmbeddedReleaseNotes
{
    private const string ResourcePrefix = "FateCompass.Core.News.Notes.";
    private const string ResourceSuffix = ".json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Loads the notes for a language, falling back to English and then to nothing.
    /// </summary>
    /// <remarks>
    /// A missing or broken notes file costs the player a window, never the plugin (S-09).
    /// </remarks>
    public static ReleaseNotes Load(string? languageCode)
    {
        var assembly = typeof(EmbeddedReleaseNotes).Assembly;

        return Read(assembly, languageCode)
            ?? Read(assembly, LanguageInfo.FallbackCode)
            ?? ReleaseNotes.Empty;
    }

    /// <summary>The language codes that have a notes file. Used by the tests.</summary>
    public static IReadOnlyList<string> AvailableCodes() =>
    [
        .. typeof(EmbeddedReleaseNotes).Assembly
            .GetManifestResourceNames()
            .Where(resource =>
                resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
                && resource.EndsWith(ResourceSuffix, StringComparison.Ordinal))
            .Select(resource => resource[ResourcePrefix.Length..^ResourceSuffix.Length])
            .Order(StringComparer.Ordinal),
    ];

    private static ReleaseNotes? Read(Assembly assembly, string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            using var stream = assembly.GetManifestResourceStream(
                $"{ResourcePrefix}{code}{ResourceSuffix}");

            if (stream is null)
            {
                return null;
            }

            var file = JsonSerializer.Deserialize<NotesFile>(stream, Options);
            return file?.Versions is null ? null : new ReleaseNotes(file.Versions.Select(Convert));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static ReleaseVersion Convert(VersionFile file) => new()
    {
        Version = file.Version ?? "0.0.0",
        Date = file.Date,
        Summary = file.Summary,
        Notes =
        [
            // Order is fixed rather than taken from the file: what is new comes before what
            // changed, which comes before what was repaired. That is the order the reader cares
            // about, and it means the notes file cannot accidentally bury a new feature under a
            // list of fixes.
            .. Lines(ReleaseNoteKind.Added, file.Added),
            .. Lines(ReleaseNoteKind.Changed, file.Changed),
            .. Lines(ReleaseNoteKind.Fixed, file.Fixed),
            .. Lines(ReleaseNoteKind.Removed, file.Removed),
        ],
    };

    private static IEnumerable<ReleaseNote> Lines(ReleaseNoteKind kind, IReadOnlyList<string>? texts) =>
        (texts ?? [])
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => new ReleaseNote(kind, text.Trim()));

    /// <summary>Shape of a notes file. Only used for reading.</summary>
    private sealed class NotesFile
    {
        public List<VersionFile>? Versions { get; set; }
    }

    /// <summary>
    /// One version in a notes file. The changes are grouped by kind rather than tagged one by
    /// one, because a list of strings under a heading is what a person can edit without
    /// getting it wrong.
    /// </summary>
    private sealed class VersionFile
    {
        public string? Version { get; set; }

        public string? Date { get; set; }

        public string? Summary { get; set; }

        public List<string>? Added { get; set; }

        public List<string>? Changed { get; set; }

        public List<string>? Fixed { get; set; }

        public List<string>? Removed { get; set; }
    }
}
