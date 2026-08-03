namespace FateHelper.Core.News;

/// <summary>
/// Everything that changed in one released version.
/// </summary>
public sealed class ReleaseVersion
{
    /// <summary>The version this describes, as written in the notes file, for example "0.1.0".</summary>
    public required string Version { get; init; }

    /// <summary>
    /// The release date as an ISO day, or null while a version has not been dated yet.
    /// </summary>
    public string? Date { get; init; }

    /// <summary>
    /// One sentence saying what this version is about, shown above the individual lines.
    /// Optional: a version that is a list of small things does not need one.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>The individual changes, in the order the notes file lists them.</summary>
    public IReadOnlyList<ReleaseNote> Notes { get; init; } = [];
}
