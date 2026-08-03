namespace FateHelper.Core.News;

/// <summary>
/// What a single line of the release notes is.
/// </summary>
/// <remarks>
/// The same four categories the repository's changelog uses, so a line never has to be
/// reclassified on its way from the changelog into the window.
/// </remarks>
public enum ReleaseNoteKind
{
    /// <summary>Something that did not exist before.</summary>
    Added,

    /// <summary>Something that existed and now behaves or looks different.</summary>
    Changed,

    /// <summary>Something that was broken and is not any more.</summary>
    Fixed,

    /// <summary>Something that is gone.</summary>
    Removed,
}
